using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.AssetManagement;
using UnityEngine;

namespace StickerFwk.Infrastructure.Sound
{
    public sealed class SoundService : ISoundService, IDisposable
    {
        private const string VolumePrefsKeyPrefix = "SoundVolume_";

        private readonly IAssetRequester _assetRequester;
        private readonly SoundServiceRoot _root;
        private readonly Dictionary<string, LoadedCueSheet> _cueSheets = new();
        private readonly HashSet<string> _loadingCueSheets = new();
        private readonly Dictionary<string, ISoundData> _soundDataByName = new();
        private readonly Dictionary<SoundType, List<SoundPlayer>> _players = new();
        private readonly Dictionary<SoundType, HashSet<SoundPlayer>> _playingPlayers = new();
        private readonly Dictionary<int, SoundPlayer> _bgmChannels = new();
        private readonly Dictionary<int, ISoundData> _pausedBgmInfo = new();
        private readonly Dictionary<SoundType, float> _volumeCache = new();
        private bool _disposed;

        public SoundService(IAssetRequester assetRequester, SoundServiceRoot root)
        {
            _assetRequester = assetRequester ?? throw new ArgumentNullException(nameof(assetRequester));
            _root = root ?? throw new ArgumentNullException(nameof(root));

            InitializePlayers();
        }

        public float SEVolume
        {
            get => GetVolume(SoundType.SE);
            set => SetVolume(SoundType.SE, value);
        }

        public float BGMVolume
        {
            get => GetVolume(SoundType.BGM);
            set => SetVolume(SoundType.BGM, value);
        }

        public float GetVolume(SoundType soundType)
        {
            if (_volumeCache.TryGetValue(soundType, out var cachedVolume)) return cachedVolume;

            var volume = PlayerPrefs.GetFloat(GetVolumePrefsKey(soundType), 1.0f);
            _volumeCache[soundType] = volume;
            return volume;
        }

        public void SetVolume(SoundType soundType, float volume)
        {
            ThrowIfDisposed();

            volume = Mathf.Clamp01(volume);
            _volumeCache[soundType] = volume;
            PlayerPrefs.SetFloat(GetVolumePrefsKey(soundType), volume);
            PlayerPrefs.Save();

            if (soundType != SoundType.BGM) return;

            foreach (var player in _playingPlayers[SoundType.BGM])
            {
                player.UpdateVolume(volume);
            }
        }

        public async UniTask LoadCueSheetAsync(string addressableKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(addressableKey))
                throw new ArgumentException("Cue sheet addressable key must not be empty.", nameof(addressableKey));

            if (_cueSheets.ContainsKey(addressableKey))
            {
                Log.Warning("SoundService", $"Cue sheet '{addressableKey}' is already loaded.");
                return;
            }

            if (!_loadingCueSheets.Add(addressableKey))
            {
                Log.Warning("SoundService", $"Cue sheet '{addressableKey}' is already loading.");
                return;
            }

            IAssetHandle<SoundCueSheet> handle = null;
            try
            {
                handle = await _assetRequester.RequestAsset<SoundCueSheet>(addressableKey, cancellationToken);
                var cueSheet = handle.Asset;
                if (cueSheet == null)
                    throw new InvalidOperationException($"Cue sheet '{addressableKey}' loaded with a null asset.");

                AddSoundDataFromCueSheet(cueSheet);
                _cueSheets.Add(addressableKey, new LoadedCueSheet(cueSheet, handle));
                handle = null;
            }
            finally
            {
                handle?.Dispose();
                _loadingCueSheets.Remove(addressableKey);
            }
        }

        public void UnloadCueSheet(string addressableKey)
        {
            ThrowIfDisposed();

            if (!_cueSheets.TryGetValue(addressableKey, out var loadedCueSheet))
            {
                Log.Warning("SoundService", $"Cue sheet '{addressableKey}' is not loaded.");
                return;
            }

            RemoveSoundDataFromCueSheet(loadedCueSheet.CueSheet);
            loadedCueSheet.Dispose();
            _cueSheets.Remove(addressableKey);
        }

        public bool IsCueSheetLoaded(string addressableKey)
        {
            return _cueSheets.ContainsKey(addressableKey);
        }

        public bool HasSound(string soundName)
        {
            return _soundDataByName.ContainsKey(soundName);
        }

        public void PlaySeOneShot(string soundName, float volume = 1.0f)
        {
            ThrowIfDisposed();

            if (!_soundDataByName.TryGetValue(soundName, out var soundData))
            {
                Log.Warning("SoundService", $"Sound '{soundName}' not found.");
                return;
            }

            var player = GetOrCreateSePlayer();
            player.PlayOneShot(soundData, volume * GetVolume(SoundType.SE));
        }

        public async UniTask PlayBgm(
            string soundName,
            int channel = 0,
            float volume = 1.0f,
            float crossfadeDuration = 1.0f)
        {
            ThrowIfDisposed();

            if (!_soundDataByName.TryGetValue(soundName, out var soundData))
            {
                Log.Warning("SoundService", $"Sound '{soundName}' not found.");
                return;
            }

            if (crossfadeDuration <= 0f)
            {
                PlayBgmImmediate(soundName, channel, volume);
                return;
            }

            var player = GetOrCreateBgmPlayer(channel);
            var finalVolume = volume * GetVolume(SoundType.BGM);
            _pausedBgmInfo.Remove(channel);
            await player.CrossfadeBgm(soundData, crossfadeDuration, finalVolume);
            _playingPlayers[SoundType.BGM].Add(player);
        }

        public void PlayBgmImmediate(string soundName, int channel = 0, float volume = 1.0f)
        {
            ThrowIfDisposed();

            if (!_soundDataByName.TryGetValue(soundName, out var soundData))
            {
                Log.Warning("SoundService", $"Sound '{soundName}' not found.");
                return;
            }

            var player = GetOrCreateBgmPlayer(channel);
            var finalVolume = volume * GetVolume(SoundType.BGM);
            _pausedBgmInfo.Remove(channel);
            player.PlayBgm(soundData, finalVolume);
            _playingPlayers[SoundType.BGM].Add(player);
        }

        public async UniTask StopBgm(int channel = 0, float fadeDuration = 1.0f)
        {
            ThrowIfDisposed();

            if (!_bgmChannels.TryGetValue(channel, out var player)) return;

            await player.StopBgm(fadeDuration);
            _playingPlayers[SoundType.BGM].Remove(player);
            _pausedBgmInfo.Remove(channel);
        }

        public void StopBgmImmediate(int channel = 0)
        {
            ThrowIfDisposed();

            if (!_bgmChannels.TryGetValue(channel, out var player)) return;

            player.StopBgmImmediate();
            _playingPlayers[SoundType.BGM].Remove(player);
            _pausedBgmInfo.Remove(channel);
        }

        public async UniTask StopAllBgm(float fadeDuration = 1.0f)
        {
            ThrowIfDisposed();

            var tasks = new List<UniTask>();
            foreach (var player in _playingPlayers[SoundType.BGM])
            {
                tasks.Add(player.StopBgm(fadeDuration));
            }

            await UniTask.WhenAll(tasks);
            _playingPlayers[SoundType.BGM].Clear();
            _pausedBgmInfo.Clear();
        }

        public void StopAllBgmImmediate()
        {
            ThrowIfDisposed();

            foreach (var player in _playingPlayers[SoundType.BGM])
            {
                player.StopBgmImmediate();
            }

            _playingPlayers[SoundType.BGM].Clear();
            _pausedBgmInfo.Clear();
        }

        public async UniTask PauseBgm(int channel = 0, float fadeDuration = 1.0f)
        {
            ThrowIfDisposed();

            if (!_bgmChannels.TryGetValue(channel, out var player))
            {
                Log.Warning("SoundService", $"No BGM playing on channel {channel} to pause.");
                return;
            }

            if (_pausedBgmInfo.ContainsKey(channel))
            {
                Log.Warning("SoundService", $"BGM on channel {channel} is already paused.");
                return;
            }

            _pausedBgmInfo[channel] = player.CurrentSoundData;
            await player.PauseBgm(fadeDuration);
        }

        public void PauseBgmImmediate(int channel = 0)
        {
            ThrowIfDisposed();

            if (!_bgmChannels.TryGetValue(channel, out var player))
            {
                Log.Warning("SoundService", $"No BGM playing on channel {channel} to pause.");
                return;
            }

            _pausedBgmInfo[channel] = player.CurrentSoundData;
            player.PauseBgmImmediate();
        }

        public async UniTask ResumeBgm(int channel = 0, float fadeDuration = 1.0f)
        {
            ThrowIfDisposed();

            if (!_pausedBgmInfo.ContainsKey(channel))
            {
                Log.Warning("SoundService", $"No paused BGM found on channel {channel}.");
                return;
            }

            if (!_bgmChannels.TryGetValue(channel, out var player))
            {
                Log.Warning("SoundService", $"No BGM player found for channel {channel}.");
                return;
            }

            _pausedBgmInfo.Remove(channel);
            await player.ResumeBgm(fadeDuration);
        }

        public void ResumeBgmImmediate(int channel = 0)
        {
            ThrowIfDisposed();

            if (!_pausedBgmInfo.ContainsKey(channel))
            {
                Log.Warning("SoundService", $"No paused BGM found on channel {channel}.");
                return;
            }

            if (!_bgmChannels.TryGetValue(channel, out var player))
            {
                Log.Warning("SoundService", $"No BGM player found for channel {channel}.");
                return;
            }

            player.ResumeBgmImmediate();
            _pausedBgmInfo.Remove(channel);
        }

        public async UniTask PauseAllBgm(float fadeDuration = 1.0f)
        {
            ThrowIfDisposed();

            var tasks = new List<UniTask>();
            foreach (var entry in _bgmChannels)
            {
                var player = entry.Value;
                if (!player.IsPlaying) continue;

                _pausedBgmInfo[entry.Key] = player.CurrentSoundData;
                if (fadeDuration <= 0f)
                    player.PauseBgmImmediate();
                else
                    tasks.Add(player.PauseBgm(fadeDuration));
            }

            await UniTask.WhenAll(tasks);
        }

        public void PauseAllBgmImmediate()
        {
            ThrowIfDisposed();

            foreach (var entry in _bgmChannels)
            {
                var player = entry.Value;
                if (!player.IsPlaying) continue;

                _pausedBgmInfo[entry.Key] = player.CurrentSoundData;
                player.PauseBgmImmediate();
            }
        }

        public async UniTask ResumeAllBgm(float fadeDuration = 1.0f)
        {
            ThrowIfDisposed();

            var tasks = new List<UniTask>();
            var channelsToResume = new List<int>(_pausedBgmInfo.Keys);
            foreach (var channel in channelsToResume)
            {
                if (!_bgmChannels.TryGetValue(channel, out var player)) continue;

                if (fadeDuration <= 0f)
                    player.ResumeBgmImmediate();
                else
                    tasks.Add(player.ResumeBgm(fadeDuration));

                _pausedBgmInfo.Remove(channel);
            }

            await UniTask.WhenAll(tasks);
        }

        public void ResumeAllBgmImmediate()
        {
            ThrowIfDisposed();

            var channelsToResume = new List<int>(_pausedBgmInfo.Keys);
            foreach (var channel in channelsToResume)
            {
                if (!_bgmChannels.TryGetValue(channel, out var player)) continue;

                player.ResumeBgmImmediate();
                _pausedBgmInfo.Remove(channel);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            StopAllPlayers();

            foreach (var loadedCueSheet in _cueSheets.Values)
            {
                loadedCueSheet.Dispose();
            }

            _cueSheets.Clear();
            _loadingCueSheets.Clear();
            _soundDataByName.Clear();
            _pausedBgmInfo.Clear();
            _bgmChannels.Clear();
            _root.ClearPlayers();
        }

        private static string GetVolumePrefsKey(SoundType soundType)
        {
            return $"{VolumePrefsKeyPrefix}{soundType}";
        }

        private void InitializePlayers()
        {
            _players[SoundType.SE] = new List<SoundPlayer>();
            _players[SoundType.BGM] = new List<SoundPlayer>();
            _playingPlayers[SoundType.SE] = new HashSet<SoundPlayer>();
            _playingPlayers[SoundType.BGM] = new HashSet<SoundPlayer>();

            for (var i = 0; i < _root.InitialSePlayerCount; i++) CreateNewPlayer(SoundType.SE);
            for (var i = 0; i < _root.InitialBgmPlayerCount; i++) CreateNewPlayer(SoundType.BGM);
        }

        private SoundPlayer CreateNewPlayer(SoundType type)
        {
            var player = _root.CreatePlayer(type, _players[type].Count);
            _players[type].Add(player);
            return player;
        }

        private SoundPlayer GetOrCreateSePlayer()
        {
            if (_players[SoundType.SE].Count == 0) return CreateNewPlayer(SoundType.SE);
            return _players[SoundType.SE][0];
        }

        private SoundPlayer GetOrCreateBgmPlayer(int channel)
        {
            if (_bgmChannels.TryGetValue(channel, out var existingPlayer)) return existingPlayer;

            SoundPlayer availablePlayer = null;
            foreach (var player in _players[SoundType.BGM])
            {
                if (player.IsPlaying || player.IsPaused) continue;

                availablePlayer = player;
                break;
            }

            availablePlayer ??= CreateNewPlayer(SoundType.BGM);
            _bgmChannels[channel] = availablePlayer;
            return availablePlayer;
        }

        private void AddSoundDataFromCueSheet(ISoundCueSheet cueSheet)
        {
            if (cueSheet.SoundDatas == null) return;

            foreach (var soundData in cueSheet.SoundDatas)
            {
                if (soundData == null || _soundDataByName.ContainsKey(soundData.Name)) continue;
                _soundDataByName[soundData.Name] = soundData;
            }
        }

        private void RemoveSoundDataFromCueSheet(ISoundCueSheet cueSheet)
        {
            if (cueSheet.SoundDatas == null) return;

            foreach (var soundData in cueSheet.SoundDatas)
            {
                if (soundData == null) continue;
                _soundDataByName.Remove(soundData.Name);
            }
        }

        private void StopAllPlayers()
        {
            foreach (var playersByType in _players.Values)
            {
                foreach (var player in playersByType)
                {
                    player.StopBgmImmediate();
                    player.Dispose();
                }
            }

            _playingPlayers[SoundType.SE].Clear();
            _playingPlayers[SoundType.BGM].Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SoundService));
        }

        private sealed class LoadedCueSheet : IDisposable
        {
            private readonly IAssetHandle<SoundCueSheet> _handle;

            public LoadedCueSheet(SoundCueSheet cueSheet, IAssetHandle<SoundCueSheet> handle)
            {
                CueSheet = cueSheet;
                _handle = handle;
            }

            public SoundCueSheet CueSheet { get; }

            public void Dispose()
            {
                _handle?.Dispose();
            }
        }
    }
}
