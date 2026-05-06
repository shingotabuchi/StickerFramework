using System;
using System.Collections.Generic;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraUsageService : ICameraUsageService, IDisposable
    {
        readonly ICameraProfileService _profileService;
        readonly ICameraModeService _modeService;
        readonly ICameraService _cameraService;
        readonly Dictionary<CameraId, int> _refCounts = new Dictionary<CameraId, int>();
        readonly List<CameraSlot> _slotBuffer = new List<CameraSlot>();
        readonly List<CameraId> _enabledBuffer = new List<CameraId>();
        readonly List<CameraId> _stackBuffer = new List<CameraId>();
        readonly Func<CameraMode, CameraId, bool> _modeIncludesDelegate;
        readonly IDisposable _profileSubscription;

        public CameraUsageService(
            ICameraProfileService profileService,
            ICameraModeService modeService,
            ICameraService cameraService,
            ISubscriber<CameraProfileAppliedEvent> profileAppliedSubscriber)
        {
            _profileService = profileService;
            _modeService = modeService;
            _cameraService = cameraService;
            _modeIncludesDelegate = _modeService.ModeIncludes;

            _modeService.ModeChanged += OnModeChanged;
            _profileSubscription = profileAppliedSubscriber.Subscribe(OnProfileApplied);
        }

        public IDisposable Acquire(CameraId cameraId)
        {
            _refCounts.TryGetValue(cameraId, out var count);
            _refCounts[cameraId] = count + 1;
            Recompute();
            return new Lease(this, cameraId);
        }

        public bool IsActive(CameraId cameraId)
        {
            if (!_cameraService.TryGetCamera(cameraId, out var camera) || camera == null)
            {
                return false;
            }
            return camera.gameObject.activeInHierarchy && camera.enabled;
        }

        public void Dispose()
        {
            _modeService.ModeChanged -= OnModeChanged;
            _profileSubscription?.Dispose();
        }

        void Release(CameraId cameraId)
        {
            if (!_refCounts.TryGetValue(cameraId, out var count) || count <= 0)
            {
                Log.Warning($"[CameraUsageService] Release called for '{cameraId}' but no active lease exists.");
                return;
            }

            count--;
            if (count == 0)
            {
                _refCounts.Remove(cameraId);
            }
            else
            {
                _refCounts[cameraId] = count;
            }
            Recompute();
        }

        void OnModeChanged(CameraMode _) => Recompute();

        void OnProfileApplied(CameraProfileAppliedEvent _) => Recompute();

        void Recompute()
        {
            var registered = _cameraService.GetRegisteredIds();
            if (registered == null || registered.Count == 0)
            {
                return;
            }

            // Build slot snapshot from the registry + lease counts.
            _slotBuffer.Clear();
            for (var i = 0; i < registered.Count; i++)
            {
                var id = registered[i];
                if (!_profileService.TryGetDefinition(id, out var def))
                {
                    continue;
                }
                _refCounts.TryGetValue(id, out var leaseCount);
                _slotBuffer.Add(new CameraSlot(id, def.RenderType, def.Depth, def.ActivationPolicy, leaseCount));
            }

            var result = CameraStackResolver.Resolve(
                _slotBuffer,
                _modeService.CurrentMode,
                _modeIncludesDelegate,
                _enabledBuffer,
                _stackBuffer);

            // Apply enabled state. A camera in _enabledBuffer is on; everything else is off
            // (including losing Bases and unwanted overlays).
            for (var i = 0; i < registered.Count; i++)
            {
                var id = registered[i];
                if (!_cameraService.TryGetCamera(id, out var camera) || camera == null)
                {
                    continue;
                }

                var shouldBeEnabled = Contains(_enabledBuffer, id);
                if (camera.gameObject.activeSelf != shouldBeEnabled)
                {
                    camera.gameObject.SetActive(shouldBeEnabled);
                }
                if (camera.enabled != shouldBeEnabled)
                {
                    camera.enabled = shouldBeEnabled;
                }
            }

            // Rebuild the winning base's overlay stack.
            if (!result.HasBase)
            {
                return;
            }

            if (!_cameraService.TryGetCamera(result.WinningBase, out var baseCamera) || baseCamera == null)
            {
                return;
            }

            var baseUrp = baseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (baseUrp == null)
            {
                return;
            }

            baseUrp.cameraStack.Clear();
            for (var i = 0; i < _stackBuffer.Count; i++)
            {
                if (_cameraService.TryGetCamera(_stackBuffer[i], out var overlay) && overlay != null)
                {
                    baseUrp.cameraStack.Add(overlay);
                }
            }
        }

        static bool Contains(List<CameraId> list, CameraId id)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                {
                    return true;
                }
            }
            return false;
        }

        sealed class Lease : IDisposable
        {
            CameraUsageService _service;
            readonly CameraId _id;
            bool _disposed;

            public Lease(CameraUsageService service, CameraId id)
            {
                _service = service;
                _id = id;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _service?.Release(_id);
                _service = null;
            }
        }
    }
}
