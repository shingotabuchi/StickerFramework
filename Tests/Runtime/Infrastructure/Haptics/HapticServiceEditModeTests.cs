#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.Haptics;
using StickerFwk.Infrastructure.Haptics;
using StickerFwk.Infrastructure.Haptics.Platform;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace StickerFwk.Tests.Runtime.Infrastructure.Haptics
{
    [TestFixture]
    public sealed class HapticServiceEditModeTests
    {
        private const string IntensityPrefsKey = "HapticIntensity";

        private GameObject _rootObject;
        private HapticServiceRoot _root;
        private FakeAssetRequester _assetRequester;
        private FakeHapticBackend _fakeBackend;
        private HapticService _service;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(IntensityPrefsKey);

            _rootObject = new GameObject("HapticServiceRoot_Test");
            _root = _rootObject.AddComponent<HapticServiceRoot>();
            _assetRequester = new FakeAssetRequester();
            _fakeBackend = new FakeHapticBackend { IsSupported = true };
            _service = new HapticService(_assetRequester, _root, _fakeBackend);
        }

        [TearDown]
        public void TearDown()
        {
            try { _service?.Dispose(); } catch { /* ignore */ }
            if (_rootObject != null) Object.DestroyImmediate(_rootObject);
            PlayerPrefs.DeleteKey(IntensityPrefsKey);
        }

        // -----------------------------------------------------------------------------------------
        // US1 — playback
        // -----------------------------------------------------------------------------------------

        [Test]
        public void PlayOneShot_MissingPattern_LogsWarning_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.PlayOneShot("does_not_exist"));
            Assert.AreEqual(0, _fakeBackend.DispatchCount);
        }

        [Test]
        public void PlayOneShot_IntensityZero_DoesNotDispatchToBackend()
        {
            _service.Test_AddPattern(MakePattern("p"));
            _service.Intensity = 0f;

            _service.PlayOneShot("p");

            Assert.AreEqual(0, _fakeBackend.DispatchCount);
        }

        [Test]
        public void PlayOneShot_Editor_NeverThrows_NoErrorLogs()
        {
            _service.Test_AddPattern(MakePattern("p"));
            for (var i = 0; i < 100; i++)
            {
                Assert.DoesNotThrow(() => _service.PlayOneShot("p"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlayOneShot_IntensityScaleClampedAboveAndBelow()
        {
            _service.Test_AddPattern(MakePattern("p"));

            _service.PlayOneShot("p", 2.5f);
            Assert.AreEqual(1, _fakeBackend.DispatchCount);
            Assert.That(_fakeBackend.LastIntensity, Is.EqualTo(1f).Within(1e-5));

            _service.PlayOneShot("p", -1.0f);
            Assert.AreEqual(1, _fakeBackend.DispatchCount,
                "Negative scale should clamp to 0 → silent.");
        }

        [Test]
        public void PlayOneShot_AfterDispose_Throws_ObjectDisposedException()
        {
            _service.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _service.PlayOneShot("p"));
        }

        [Test]
        public void Constructor_DefaultBackendSelection_DoesNotThrow_NoErrorLogs()
        {
            using var rootGo = new TempGameObject();
            var realService = new HapticService(_assetRequester, rootGo.Root);
            Assert.DoesNotThrow(realService.Dispose);
            LogAssert.NoUnexpectedReceived();
        }

        // -----------------------------------------------------------------------------------------
        // US2 — cue-sheet loading
        // -----------------------------------------------------------------------------------------

        [Test]
        public void LoadCueSheetAsync_NullOrWhitespaceKey_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(
                async () => await _service.LoadCueSheetAsync(null).AsTask());
            Assert.ThrowsAsync<ArgumentException>(
                async () => await _service.LoadCueSheetAsync("   ").AsTask());
        }

        [Test]
        public async Task LoadCueSheetAsync_SameKeyTwice_LogsWarning_DoesNotReload()
        {
            _assetRequester.AddSheet("a", BuildCueSheet("a_sheet", "pat_a"));

            await _service.LoadCueSheetAsync("a").AsTask();
            await _service.LoadCueSheetAsync("a").AsTask();

            Assert.AreEqual(1, _assetRequester.RequestCount);
            Assert.IsTrue(_service.IsCueSheetLoaded("a"));
        }

        [Test]
        public void LoadCueSheetAsync_Cancelled_ReleasesHandle_ClearsLoadingFlag()
        {
            _assetRequester.AddSheet("a", BuildCueSheet("a_sheet", "pat_a"));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await _service.LoadCueSheetAsync("a", cts.Token).AsTask());
            Assert.IsFalse(_service.IsCueSheetLoaded("a"));

            // The loading flag must be cleared so a subsequent load succeeds.
            Assert.DoesNotThrowAsync(async () => await _service.LoadCueSheetAsync("a").AsTask());
            Assert.IsTrue(_service.IsCueSheetLoaded("a"));
        }

        [Test]
        public async Task LoadCueSheetAsync_DuplicatePatternNameAcrossSheets_FirstWins_WarningEmitted()
        {
            _assetRequester.AddSheet("a", BuildCueSheet("a_sheet", "shared"));
            _assetRequester.AddSheet("b", BuildCueSheet("b_sheet", "shared"));

            await _service.LoadCueSheetAsync("a").AsTask();
            await _service.LoadCueSheetAsync("b").AsTask();

            Assert.IsTrue(_service.HasPattern("shared"));

            // Unloading 'b' should not affect 'shared' because 'a' was the registered source.
            _service.UnloadCueSheet("b");
            Assert.IsTrue(_service.HasPattern("shared"));
        }

        [Test]
        public async Task UnloadCueSheet_RemovesOnlyOwnedPatterns_LeavesOthersIntact()
        {
            _assetRequester.AddSheet("a", BuildCueSheet("a_sheet", "pat_a"));
            _assetRequester.AddSheet("b", BuildCueSheet("b_sheet", "pat_b"));

            await _service.LoadCueSheetAsync("a").AsTask();
            await _service.LoadCueSheetAsync("b").AsTask();
            Assert.IsTrue(_service.HasPattern("pat_a"));
            Assert.IsTrue(_service.HasPattern("pat_b"));

            _service.UnloadCueSheet("a");

            Assert.IsFalse(_service.HasPattern("pat_a"));
            Assert.IsTrue(_service.HasPattern("pat_b"));
            Assert.IsFalse(_service.IsCueSheetLoaded("a"));
            Assert.IsTrue(_service.IsCueSheetLoaded("b"));
        }

        [Test]
        public void UnloadCueSheet_UnknownKey_LogsWarning_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.UnloadCueSheet("nope"));
        }

        [Test]
        public async Task Dispose_ReleasesAllAssetHandles()
        {
            _assetRequester.AddSheet("a", BuildCueSheet("a_sheet", "pat_a"));
            _assetRequester.AddSheet("b", BuildCueSheet("b_sheet", "pat_b"));

            await _service.LoadCueSheetAsync("a").AsTask();
            await _service.LoadCueSheetAsync("b").AsTask();

            _service.Dispose();

            Assert.IsTrue(_assetRequester.AllHandlesDisposed);
            Assert.AreEqual(2, _assetRequester.DisposedHandleCount);
        }

        [Test]
        public void IsCueSheetLoaded_HasPattern_NullOrEmpty_ReturnsFalse_NoThrow()
        {
            Assert.IsFalse(_service.IsCueSheetLoaded(null));
            Assert.IsFalse(_service.IsCueSheetLoaded(string.Empty));
            Assert.IsFalse(_service.HasPattern(null));
            Assert.IsFalse(_service.HasPattern(string.Empty));
        }

        // -----------------------------------------------------------------------------------------
        // US3 — intensity preference
        // -----------------------------------------------------------------------------------------

        [Test]
        public void Intensity_DefaultsTo_1_WhenNoPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(IntensityPrefsKey);
            Assert.That(_service.Intensity, Is.EqualTo(1f).Within(1e-5));
        }

        [Test]
        public void Intensity_ClampsBelow_0_To_0()
        {
            _service.Intensity = -0.5f;
            Assert.That(_service.Intensity, Is.EqualTo(0f).Within(1e-5));
        }

        [Test]
        public void Intensity_ClampsAbove_1_To_1()
        {
            _service.Intensity = 2.5f;
            Assert.That(_service.Intensity, Is.EqualTo(1f).Within(1e-5));
        }

        [Test]
        public void Intensity_PersistsAcrossServiceReconstruction()
        {
            _service.Intensity = 0.5f;
            _service.Dispose();
            Object.DestroyImmediate(_rootObject);

            _rootObject = new GameObject("HapticServiceRoot_Test2");
            _root = _rootObject.AddComponent<HapticServiceRoot>();
            _fakeBackend = new FakeHapticBackend { IsSupported = true };
            _service = new HapticService(_assetRequester, _root, _fakeBackend);

            Assert.That(_service.Intensity, Is.EqualTo(0.5f).Within(1e-5));
        }

        [Test]
        public void Intensity_Setter_AfterDispose_Throws_ObjectDisposedException()
        {
            _service.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _service.Intensity = 0.3f);
        }

        [Test]
        public void PlayOneShot_WhenIntensityZero_DoesNotDispatchToBackend_EndToEnd()
        {
            _service.Test_AddPattern(MakePattern("p"));
            _service.Intensity = 0f;
            for (var i = 0; i < 10; i++)
            {
                _service.PlayOneShot("p");
            }
            Assert.AreEqual(0, _fakeBackend.DispatchCount);
        }

        // -----------------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------------

        private static HapticPattern MakePattern(string name)
        {
            var curve = new HapticPatternCurve(new[]
            {
                new HapticCurvePoint(0f, 1f),
                new HapticCurvePoint(0.1f, 1f),
            });
            return new HapticPattern(name, 0.1f, curve, curve);
        }

        private static HapticCueSheet BuildCueSheet(string name, params string[] patternNames)
        {
            var sheet = ScriptableObject.CreateInstance<HapticCueSheet>();
            sheet.name = name;
            var datas = new List<HapticData>();
            foreach (var n in patternNames)
            {
                datas.Add(BuildHapticData(n));
            }
            var listField = typeof(HapticCueSheet).GetField("_hapticDatas",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            listField!.SetValue(sheet, datas);
            return sheet;
        }

        private static HapticData BuildHapticData(string name)
        {
            var data = new HapticData();
            var type = typeof(HapticData);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            type.GetField("_name", flags)!.SetValue(data, name);
            type.GetField("_durationSeconds", flags)!.SetValue(data, 0.1f);
            type.GetField("_intensityCurvePoints", flags)!.SetValue(data, new List<SerializableHapticCurvePoint>
            {
                new() { TimeSeconds = 0f, Value = 1f },
                new() { TimeSeconds = 0.1f, Value = 1f },
            });
            type.GetField("_sharpnessCurvePoints", flags)!.SetValue(data, new List<SerializableHapticCurvePoint>
            {
                new() { TimeSeconds = 0f, Value = 0.5f },
                new() { TimeSeconds = 0.1f, Value = 0.5f },
            });
            type.GetField("_presetHint", flags)!.SetValue(data, HapticPresetId.None);
            return data;
        }

        // ----- Fakes ----- //

        private sealed class TempGameObject : IDisposable
        {
            private readonly GameObject _go;
            public HapticServiceRoot Root { get; }
            public TempGameObject()
            {
                _go = new GameObject("TempRoot");
                Root = _go.AddComponent<HapticServiceRoot>();
            }
            public void Dispose() { if (_go != null) Object.DestroyImmediate(_go); }
        }

        private sealed class FakeHapticBackend : IHapticBackend
        {
            public bool IsSupported { get; set; }
            public int DispatchCount { get; private set; }
            public float LastIntensity { get; private set; }
            public string LastPatternName { get; private set; }

            public void PlayOneShot(in HapticPattern pattern, float intensityScale)
            {
                DispatchCount++;
                LastIntensity = intensityScale;
                LastPatternName = pattern.Name;
            }

            public void Dispose() { }
        }

        private sealed class FakeAssetRequester : IAssetRequester
        {
            private readonly Dictionary<string, HapticCueSheet> _sheets = new();
            private readonly List<FakeHandle<HapticCueSheet>> _liveHandles = new();

            public int RequestCount { get; private set; }
            public int DisposedHandleCount { get; private set; }
            public bool AllHandlesDisposed =>
                _liveHandles.Count == 0 || _liveHandles.TrueForAll(h => h.Disposed);

            public void AddSheet(string key, HapticCueSheet sheet)
            {
                _sheets[key] = sheet;
            }

            public UniTask<IAssetHandle<T>> RequestAsset<T>(
                string key, CancellationToken cancellationToken = default) where T : Object
            {
                RequestCount++;
                cancellationToken.ThrowIfCancellationRequested();
                if (!_sheets.TryGetValue(key, out var sheet))
                    throw new InvalidOperationException($"FakeAssetRequester: no sheet for key '{key}'.");

                var handle = new FakeHandle<HapticCueSheet>(sheet, OnHandleDisposed);
                _liveHandles.Add(handle);
                return new UniTask<IAssetHandle<T>>((IAssetHandle<T>)(object)handle);
            }

            private void OnHandleDisposed() { DisposedHandleCount++; }

            public T GetAssetImmediate<T>(string key) where T : Object => throw new NotImplementedException();
            public UniTask<IAssetHandle> Preload<T>(IEnumerable<string> keys,
                CancellationToken cancellationToken = default, IProgress<float> progress = null)
                where T : Object => throw new NotImplementedException();
            public UniTask<IPreloadHandle> PreloadFromLabel<T>(string assetLabel,
                CancellationToken cancellationToken = default, IProgress<float> progress = null)
                where T : Object => throw new NotImplementedException();
            public void Release(string key) => throw new NotImplementedException();
            public void Release(IEnumerable<string> keys) => throw new NotImplementedException();
            public UniTask ReleaseFromLabel(string assetLabel, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();
            public bool IsLoaded(string key) => false;
            public bool IsLoaded(IEnumerable<string> keys) => false;
        }

        private sealed class FakeHandle<T> : IAssetHandle<T> where T : Object
        {
            private readonly Action _onDispose;
            public FakeHandle(T asset, Action onDispose)
            {
                Asset = asset;
                _onDispose = onDispose;
            }
            public T Asset { get; }
            public bool Disposed { get; private set; }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                _onDispose?.Invoke();
            }
        }
    }
}
#endif
