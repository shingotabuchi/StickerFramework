using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.Haptics;
using StickerFwk.Infrastructure.Haptics.Platform;
using UnityEngine;

[assembly: InternalsVisibleTo("StickerFwk.Tests.Runtime")]

namespace StickerFwk.Infrastructure.Haptics
{
    public sealed class HapticService : IHapticService, IDisposable
    {
        private const string IntensityPrefsKey = "HapticIntensity";
        private const string ProfileSourceKey = "__profile__";

        private readonly IAssetRequester _assetRequester;
        private readonly HapticServiceRoot _root;
        private readonly IHapticBackend _backend;
        private readonly Dictionary<string, LoadedCueSheet> _cueSheets = new();
        private readonly HashSet<string> _loadingCueSheets = new();
        private readonly Dictionary<string, HapticPattern> _patternsByName = new();
        private readonly Dictionary<string, string> _patternSourceKeys = new();

        private float _intensity;
        private bool _intensityLoaded;
        private bool _disposed;

        public HapticService(IAssetRequester assetRequester, HapticServiceRoot root)
            : this(assetRequester, root, profile: null, backendOverride: null)
        {
        }

        public HapticService(IAssetRequester assetRequester, HapticServiceRoot root, HapticProfile profile)
            : this(assetRequester, root, profile, backendOverride: null)
        {
        }

        internal HapticService(IAssetRequester assetRequester, HapticServiceRoot root, IHapticBackend backendOverride)
            : this(assetRequester, root, profile: null, backendOverride)
        {
        }

        internal HapticService(
            IAssetRequester assetRequester,
            HapticServiceRoot root,
            HapticProfile profile,
            IHapticBackend backendOverride)
        {
            _assetRequester = assetRequester ?? throw new ArgumentNullException(nameof(assetRequester));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _backend = backendOverride ?? SelectBackend();
            AddPatternsFromProfile(profile);
        }

        public float Intensity
        {
            get
            {
                if (!_intensityLoaded)
                {
                    _intensity = Mathf.Clamp01(PlayerPrefs.GetFloat(IntensityPrefsKey, 1.0f));
                    _intensityLoaded = true;
                }
                return _intensity;
            }
            set
            {
                ThrowIfDisposed();
                var clamped = Mathf.Clamp01(value);
                _intensity = clamped;
                _intensityLoaded = true;
                PlayerPrefs.SetFloat(IntensityPrefsKey, clamped);
                PlayerPrefs.Save();
            }
        }

        public async UniTask LoadCueSheetAsync(string addressableKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(addressableKey))
                throw new ArgumentException("Cue sheet addressable key must not be empty.", nameof(addressableKey));

            if (_cueSheets.ContainsKey(addressableKey))
            {
                Log.Warning("HapticService", $"Cue sheet '{addressableKey}' is already loaded.");
                return;
            }

            if (!_loadingCueSheets.Add(addressableKey))
            {
                Log.Warning("HapticService", $"Cue sheet '{addressableKey}' is already loading.");
                return;
            }

            IAssetHandle<HapticCueSheet> handle = null;
            try
            {
                handle = await _assetRequester.RequestAsset<HapticCueSheet>(addressableKey, cancellationToken);
                var cueSheet = handle.Asset;
                if (cueSheet == null)
                    throw new InvalidOperationException($"Cue sheet '{addressableKey}' loaded with a null asset.");

                AddPatternsFromCueSheet(cueSheet, addressableKey);
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
                Log.Warning("HapticService", $"Cue sheet '{addressableKey}' is not loaded.");
                return;
            }

            RemovePatternsForKey(addressableKey);
            loadedCueSheet.Dispose();
            _cueSheets.Remove(addressableKey);
        }

        public bool IsCueSheetLoaded(string addressableKey)
        {
            if (string.IsNullOrEmpty(addressableKey)) return false;
            return _cueSheets.ContainsKey(addressableKey);
        }

        public bool HasPattern(string patternName)
        {
            if (string.IsNullOrEmpty(patternName)) return false;
            return _patternsByName.ContainsKey(patternName);
        }

        public void PlayOneShot(string patternName, float intensityScale = 1.0f)
        {
            ThrowIfDisposed();

            var clampedScale = Mathf.Clamp01(intensityScale);
            // FR-007 / SC-004: master Intensity acts as a gate; 0 → suppress.
            var effectiveIntensity = Intensity * clampedScale;
            if (effectiveIntensity <= 0f) return;
            if (!_backend.IsSupported) return;

            if (!_patternsByName.TryGetValue(patternName, out var pattern))
            {
                Log.Warning("HapticService", $"Pattern '{patternName}' not found.");
                return;
            }

            _backend.PlayOneShot(in pattern, effectiveIntensity);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try { _backend?.Dispose(); }
            catch (Exception e) { Log.Error("HapticService", $"Backend dispose failed: {e}"); }

            foreach (var loadedCueSheet in _cueSheets.Values)
            {
                loadedCueSheet.Dispose();
            }

            _cueSheets.Clear();
            _loadingCueSheets.Clear();
            _patternsByName.Clear();
            _patternSourceKeys.Clear();
        }

        /// <summary>
        /// Test seam: lets EditMode tests preload patterns without going through the
        /// Addressable cue-sheet pipeline. Internal so only the Tests assembly can call it.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("UNITY_INCLUDE_TESTS"), Conditional("DEVELOPMENT_BUILD")]
        internal void Test_AddPattern(in HapticPattern pattern)
        {
            _patternsByName[pattern.Name] = pattern;
            _patternSourceKeys[pattern.Name] = "__test__";
        }

        private void AddPatternsFromProfile(HapticProfile profile)
        {
            if (profile?.Patterns == null) return;

            foreach (var pattern in profile.Patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern.Name))
                {
                    Log.Warning("HapticService", "Ignoring unnamed pattern from haptic profile.");
                    continue;
                }

                if (_patternsByName.ContainsKey(pattern.Name))
                {
                    Log.Warning("HapticService",
                        $"Pattern '{pattern.Name}' already registered; ignoring duplicate from haptic profile.");
                    continue;
                }

                _patternsByName[pattern.Name] = pattern;
                _patternSourceKeys[pattern.Name] = ProfileSourceKey;
            }
        }

        private void AddPatternsFromCueSheet(IHapticCueSheet cueSheet, string sourceKey)
        {
            if (cueSheet.HapticDatas == null) return;

            foreach (var data in cueSheet.HapticDatas)
            {
                if (data == null) continue;

                if (_patternsByName.ContainsKey(data.Name))
                {
                    Log.Warning("HapticService",
                        $"Pattern '{data.Name}' already registered; ignoring duplicate from cue sheet '{cueSheet.Name}'.");
                    continue;
                }

                var intensityCurve = BuildCurve(data.IntensityCurvePoints);
                var sharpnessCurve = BuildCurve(data.SharpnessCurvePoints);
                var pattern = new HapticPattern(
                    data.Name,
                    data.DurationSeconds,
                    intensityCurve,
                    sharpnessCurve,
                    data.PresetHint);

                _patternsByName[data.Name] = pattern;
                _patternSourceKeys[data.Name] = sourceKey;
            }
        }

        private void RemovePatternsForKey(string sourceKey)
        {
            // Snapshot first — we mutate the dictionary inside the loop.
            List<string> toRemove = null;
            foreach (var entry in _patternSourceKeys)
            {
                if (entry.Value != sourceKey) continue;
                toRemove ??= new List<string>();
                toRemove.Add(entry.Key);
            }

            if (toRemove == null) return;

            foreach (var name in toRemove)
            {
                _patternsByName.Remove(name);
                _patternSourceKeys.Remove(name);
            }
        }

        private static HapticPatternCurve BuildCurve(IReadOnlyList<HapticCurvePoint> points)
        {
            if (points == null || points.Count == 0)
            {
                var fallback = new[] { new HapticCurvePoint(0f, 1f) };
                return new HapticPatternCurve(fallback);
            }
            return new HapticPatternCurve(points);
        }

        private static IHapticBackend SelectBackend()
        {
            try
            {
                if (Application.isEditor) return new NoOpHapticBackend();

                switch (Application.platform)
                {
                    case RuntimePlatform.IPhonePlayer:
#if UNITY_IOS
                        return new IOSHapticBackend();
#else
                        return new NoOpHapticBackend();
#endif
                    case RuntimePlatform.Android:
#if UNITY_ANDROID
                    {
                        var android = new AndroidHapticBackend();
                        return android.IsSupported ? (IHapticBackend)android : new NoOpHapticBackend();
                    }
#else
                        return new NoOpHapticBackend();
#endif
                    default:
                        return new NoOpHapticBackend();
                }
            }
            catch (Exception e)
            {
                Log.Error("HapticService", $"Backend init failed, falling back to no-op: {e}");
                return new NoOpHapticBackend();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HapticService));
        }

        private sealed class LoadedCueSheet : IDisposable
        {
            private readonly IAssetHandle<HapticCueSheet> _handle;

            public LoadedCueSheet(HapticCueSheet cueSheet, IAssetHandle<HapticCueSheet> handle)
            {
                CueSheet = cueSheet;
                _handle = handle;
            }

            public HapticCueSheet CueSheet { get; }

            public void Dispose()
            {
                _handle?.Dispose();
            }
        }
    }
}
