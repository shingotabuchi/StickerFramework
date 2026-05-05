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
            var profile = _profileService.ActiveProfile;
            if (profile == null)
            {
                return;
            }

            var mode = _modeService.CurrentMode;

            // Step 1: enable/disable each camera based on mode + (lease or AlwaysOn).
            for (var i = 0; i < profile.Cameras.Count; i++)
            {
                var def = profile.Cameras[i];
                if (def == null)
                {
                    continue;
                }

                if (!_cameraService.TryGetCamera(def.Id, out var camera) || camera == null)
                {
                    continue;
                }

                var shouldBeEnabled = ShouldEnable(mode, def);
                if (camera.gameObject.activeSelf != shouldBeEnabled)
                {
                    camera.gameObject.SetActive(shouldBeEnabled);
                }
                if (camera.enabled != shouldBeEnabled)
                {
                    camera.enabled = shouldBeEnabled;
                }
            }

            // Step 2: rebuild base camera stack to contain only enabled overlays in declared order.
            if (!_cameraService.TryGetCamera(profile.BaseCamera, out var baseCamera) || baseCamera == null)
            {
                return;
            }

            var baseUrp = baseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (baseUrp == null)
            {
                return;
            }

            baseUrp.cameraStack.Clear();
            for (var i = 0; i < profile.DefaultStackOrder.Count; i++)
            {
                var id = profile.DefaultStackOrder[i];
                if (id == profile.BaseCamera)
                {
                    continue;
                }

                if (!profile.TryGetDefinition(id, out var def))
                {
                    continue;
                }

                if (!ShouldEnable(mode, def))
                {
                    continue;
                }

                if (_cameraService.TryGetCamera(id, out var overlay) && overlay != null)
                {
                    baseUrp.cameraStack.Add(overlay);
                }
            }
        }

        bool ShouldEnable(CameraMode mode, CameraDefinition def)
        {
            if (!_modeService.ModeIncludes(mode, def.Id))
            {
                return false;
            }

            if (def.ActivationPolicy == CameraActivationPolicy.AlwaysOn)
            {
                return true;
            }

            return _refCounts.TryGetValue(def.Id, out var count) && count > 0;
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
