using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Infrastructure.Sound
{
    internal sealed class SoundPlayer : IDisposable
    {
        private readonly AudioSource _audioSource0;
        private readonly AudioSource _audioSource1;
        private CancellationTokenSource _fadeCts = new();
        private bool _isFading;
        private ISoundData _currentSoundData;
        private AudioClip _currentClip;
        private float _currentVolumeMultiplier = 1.0f;

        internal SoundPlayer(AudioSource audioSource0, AudioSource audioSource1)
        {
            _audioSource0 = audioSource0;
            _audioSource1 = audioSource1;
        }

        public ISoundData CurrentSoundData => _currentSoundData;
        public bool IsPaused { get; private set; }
        public bool IsPlaying => IsSourcePlaying(_audioSource0) || IsSourcePlaying(_audioSource1);

        public void Dispose()
        {
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
            _fadeCts = null;
        }

        public void PlayOneShot(ISoundData soundData, float volume = 1.0f)
        {
            var clip = soundData?.Clip;
            if (clip == null || _audioSource0 == null) return;

            _audioSource0.PlayOneShot(clip, soundData.Volume * volume);
        }

        public void PlayBgm(ISoundData soundData, float volume = 1.0f)
        {
            var clip = soundData?.Clip;
            if (clip == null) return;

            CancelFade();

            _currentSoundData = soundData;
            _currentClip = clip;
            _currentVolumeMultiplier = volume;
            soundData.PlayedVolume = volume;
            IsPaused = false;

            StopSource(_audioSource1);
            _audioSource0.clip = clip;
            _audioSource0.volume = soundData.Volume * volume;
            _audioSource0.loop = true;
            _audioSource0.Play();
        }

        public async UniTask CrossfadeBgm(ISoundData newSoundData, float duration = 1.0f, float volume = 1.0f)
        {
            var clip = newSoundData?.Clip;
            if (clip == null) return;

            CancelFade();
            await WaitForFadeToFinish();

            _isFading = true;
            IsPaused = false;
            _currentVolumeMultiplier = volume;
            newSoundData.PlayedVolume = volume;

            var playingSource = IsSourcePlaying(_audioSource0) ? _audioSource0 :
                IsSourcePlaying(_audioSource1) ? _audioSource1 : null;
            var nextSource = playingSource == _audioSource0 ? _audioSource1 : _audioSource0;

            nextSource.clip = clip;
            nextSource.volume = 0f;
            nextSource.loop = true;
            nextSource.Play();

            var token = _fadeCts.Token;
            var startTime = UnityEngine.Time.time;
            var startVolume = playingSource != null ? playingSource.volume : 0f;
            var endVolume = newSoundData.Volume * volume;

            _currentSoundData = newSoundData;
            _currentClip = clip;

            try
            {
                while (UnityEngine.Time.time - startTime < duration)
                {
                    var t = (UnityEngine.Time.time - startTime) / duration;
                    if (playingSource != null) playingSource.volume = Mathf.Lerp(startVolume, 0f, t);
                    nextSource.volume = Mathf.Lerp(0f, endVolume, t);
                    await UniTask.Yield(token);
                }

                if (playingSource != null) playingSource.Stop();
                nextSource.volume = endVolume;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isFading = false;
            }
        }

        public async UniTask StopBgm(float fadeDuration = 1.0f)
        {
            CancelFade();
            await WaitForFadeToFinish();

            IsPaused = false;

            if (fadeDuration <= 0f || !IsPlaying)
            {
                StopBgmImmediate();
                return;
            }

            _isFading = true;

            var token = _fadeCts.Token;
            var source0 = IsSourcePlaying(_audioSource0) ? _audioSource0 : null;
            var source1 = IsSourcePlaying(_audioSource1) ? _audioSource1 : null;
            var startTime = UnityEngine.Time.time;
            var startVolume0 = source0 != null ? source0.volume : 0f;
            var startVolume1 = source1 != null ? source1.volume : 0f;

            try
            {
                while (UnityEngine.Time.time - startTime < fadeDuration)
                {
                    var t = (UnityEngine.Time.time - startTime) / fadeDuration;
                    if (source0 != null) source0.volume = Mathf.Lerp(startVolume0, 0f, t);
                    if (source1 != null) source1.volume = Mathf.Lerp(startVolume1, 0f, t);
                    await UniTask.Yield(token);
                }

                StopBgmImmediate(cancelFade: false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isFading = false;
            }
        }

        public void StopBgmImmediate()
        {
            StopBgmImmediate(cancelFade: true);
        }

        public async UniTask PauseBgm(float fadeDuration = 1.0f)
        {
            if (IsPaused)
            {
                Log.Warning("SoundService", "BGM is already paused.");
                return;
            }

            if (fadeDuration <= 0f || !IsPlaying)
            {
                PauseBgmImmediate();
                return;
            }

            CancelFade();
            await WaitForFadeToFinish();

            IsPaused = true;
            _isFading = true;

            var token = _fadeCts.Token;
            var source0 = IsSourcePlaying(_audioSource0) ? _audioSource0 : null;
            var source1 = IsSourcePlaying(_audioSource1) ? _audioSource1 : null;
            var startTime = UnityEngine.Time.time;
            var startVolume0 = source0 != null ? source0.volume : 0f;
            var startVolume1 = source1 != null ? source1.volume : 0f;

            try
            {
                while (UnityEngine.Time.time - startTime < fadeDuration)
                {
                    var t = (UnityEngine.Time.time - startTime) / fadeDuration;
                    if (source0 != null) source0.volume = Mathf.Lerp(startVolume0, 0f, t);
                    if (source1 != null) source1.volume = Mathf.Lerp(startVolume1, 0f, t);
                    await UniTask.Yield(token);
                }

                if (source0 != null) source0.Pause();
                if (source1 != null) source1.Pause();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isFading = false;
            }
        }

        public void PauseBgmImmediate()
        {
            if (IsPaused)
            {
                Log.Warning("SoundService", "BGM is already paused.");
                return;
            }

            IsPaused = true;
            CancelFade();

            if (IsSourcePlaying(_audioSource0)) _audioSource0.Pause();
            if (IsSourcePlaying(_audioSource1)) _audioSource1.Pause();
        }

        public async UniTask ResumeBgm(float fadeDuration = 1.0f)
        {
            if (!IsPaused)
            {
                Log.Warning("SoundService", "BGM is not paused. Cannot resume.");
                return;
            }

            if (fadeDuration <= 0f)
            {
                ResumeBgmImmediate();
                return;
            }

            CancelFade();
            await WaitForFadeToFinish();

            IsPaused = false;
            _isFading = true;

            var token = _fadeCts.Token;
            var targetVolume = GetCurrentBgmVolume();
            var startVolume0 = _audioSource0 != null ? _audioSource0.volume : 0f;
            var startVolume1 = _audioSource1 != null ? _audioSource1.volume : 0f;
            var startTime = UnityEngine.Time.time;

            if (_audioSource0 != null && _audioSource0.clip != null)
            {
                _audioSource0.volume = 0f;
                _audioSource0.UnPause();
            }

            if (_audioSource1 != null && _audioSource1.clip != null)
            {
                _audioSource1.volume = 0f;
                _audioSource1.UnPause();
            }

            try
            {
                while (UnityEngine.Time.time - startTime < fadeDuration)
                {
                    var t = (UnityEngine.Time.time - startTime) / fadeDuration;
                    if (_audioSource0 != null && _audioSource0.clip != null)
                        _audioSource0.volume = Mathf.Lerp(startVolume0, targetVolume, t);
                    if (_audioSource1 != null && _audioSource1.clip != null)
                        _audioSource1.volume = Mathf.Lerp(startVolume1, targetVolume, t);
                    await UniTask.Yield(token);
                }

                if (_audioSource0 != null && _audioSource0.clip != null) _audioSource0.volume = targetVolume;
                if (_audioSource1 != null && _audioSource1.clip != null) _audioSource1.volume = targetVolume;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isFading = false;
            }
        }

        public void ResumeBgmImmediate()
        {
            if (!IsPaused)
            {
                Log.Warning("SoundService", "BGM is not paused. Cannot resume.");
                return;
            }

            IsPaused = false;
            var volume = GetCurrentBgmVolume();

            if (_audioSource0 != null && _audioSource0.clip != null && !_audioSource0.isPlaying)
            {
                _audioSource0.volume = volume;
                _audioSource0.UnPause();
            }

            if (_audioSource1 != null && _audioSource1.clip != null && !_audioSource1.isPlaying)
            {
                _audioSource1.volume = volume;
                _audioSource1.UnPause();
            }
        }

        public void UpdateVolume(float volumeMultiplier)
        {
            if (_currentSoundData == null) return;

            _currentVolumeMultiplier = volumeMultiplier;
            _currentSoundData.PlayedVolume = volumeMultiplier;
            var targetVolume = _currentSoundData.Volume * volumeMultiplier;

            if (_audioSource0 != null && _audioSource0.clip == _currentClip)
                _audioSource0.volume = targetVolume;
            if (_audioSource1 != null && _audioSource1.clip == _currentClip)
                _audioSource1.volume = targetVolume;
        }

        private async UniTask WaitForFadeToFinish()
        {
            while (_isFading) await UniTask.Yield();
        }

        private void CancelFade()
        {
            if (_fadeCts == null) return;

            _fadeCts.Cancel();
            _fadeCts.Dispose();
            _fadeCts = new CancellationTokenSource();
        }

        private void StopBgmImmediate(bool cancelFade)
        {
            if (cancelFade) CancelFade();

            StopSource(_audioSource0);
            StopSource(_audioSource1);
            _currentSoundData = null;
            _currentClip = null;
            IsPaused = false;
        }

        private float GetCurrentBgmVolume()
        {
            return _currentSoundData != null ? _currentSoundData.Volume * _currentVolumeMultiplier : 0f;
        }

        private static bool IsSourcePlaying(AudioSource source)
        {
            return source != null && source.isPlaying;
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null) return;

            source.Stop();
            source.clip = null;
            source.loop = false;
        }
    }
}
