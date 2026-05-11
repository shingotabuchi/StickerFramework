using System;
using System.Collections.Generic;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraService : ICameraService
    {
        readonly CameraModel _model;
        readonly IPublisher<ActiveBaseChangedEvent> _publisher;

        public event Action<ActiveBaseChangedEvent> ActiveBaseChanged;

        public CameraService(
            CameraModel model,
            IPublisher<ActiveBaseChangedEvent> publisher)
        {
            _model = model;
            _publisher = publisher;
        }

        public CameraId ActiveBase => _model.ActiveBase;

        public void Register(CameraId id, UnityEngine.Camera camera)
        {
            ValidateId(id);
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            Log.Info($"Registering camera '{id}' with game object '{camera.gameObject.name}'.");

            var previous = ActiveBase;
            var kind = Classify(camera);
            _model.Register(id, camera, kind);

            if (kind == CameraRenderType.Base)
            {
                if (_model.PendingDefaultBase == id)
                {
                    _model.SetDefaultBase(id);
                    _model.ClearPendingDefaultBase();
                }
                else if (!_model.PendingDefaultBase.IsValid && !previous.IsValid)
                {
                    _model.SetDefaultBase(id);
                }
            }

            Recompute();
            PublishActiveBaseChanged(previous, ActiveBase);
        }

        public void Unregister(CameraId id)
        {
            Log.Info($"Unregistering camera '{id}'.");

            var previous = ActiveBase;
            if (!_model.Unregister(id))
            {
                return;
            }

            Recompute();
            PublishActiveBaseChanged(previous, ActiveBase);
        }

        public bool TryGetCamera(CameraId id, out UnityEngine.Camera camera)
        {
            return _model.TryGet(id, out camera);
        }

        public UnityEngine.Camera GetCamera(CameraId id)
        {
            return _model.TryGet(id, out var camera) ? camera : null;
        }

        public UnityEngine.Camera GetRequiredCamera(CameraId id)
        {
            if (!_model.TryGet(id, out var camera) || camera == null)
            {
                throw new InvalidOperationException($"[CameraService] Required camera '{id}' is not registered.");
            }

            return camera;
        }

        public UnityEngine.Camera GetCameraForRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            foreach (var kvp in _model.Registered)
            {
                var camera = kvp.Value;
                if (camera == null || !camera.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if ((camera.cullingMask & (1 << renderer.gameObject.layer)) != 0)
                {
                    return camera;
                }
            }

            return null;
        }

        public IReadOnlyList<CameraId> GetRegisteredIds()
        {
            return _model.GetRegisteredIds();
        }

        public bool IsRegistered(CameraId id)
        {
            return _model.IsRegistered(id);
        }

        public void SetDefaultBase(CameraId id)
        {
            ValidateId(id);

            var previous = ActiveBase;
            if (!_model.IsRegistered(id))
            {
                _model.SetPendingDefaultBase(id);
                return;
            }

            EnsureBase(id);
            _model.SetDefaultBase(id);
            Recompute();
            PublishActiveBaseChanged(previous, ActiveBase);
        }

        public IDisposable PushBase(CameraId id)
        {
            ValidateId(id);
            EnsureRegisteredBase(id);

            var previous = ActiveBase;
            var handle = _model.PushBase(id);
            Recompute();
            PublishActiveBaseChanged(previous, ActiveBase);
            return new BaseLease(this, handle);
        }

        public IDisposable DisableOverlay(CameraId id)
        {
            ValidateId(id);

            _model.DisableOverlay(id);
            Recompute();
            return new OverlayLease(this, id);
        }

        void RemoveBaseByHandle(long handle)
        {
            var previous = ActiveBase;
            if (!_model.PopBase(handle))
            {
                return;
            }

            Recompute();
            PublishActiveBaseChanged(previous, ActiveBase);
        }

        void EnableOverlay(CameraId id)
        {
            _model.EnableOverlay(id);
            Recompute();
        }

        void Recompute()
        {
            var slots = new List<CameraSlot>();
            var disabledOverlays = new HashSet<CameraId>();

            foreach (var kvp in _model.Registered)
            {
                var id = kvp.Key;
                var camera = kvp.Value;
                if (camera == null || !_model.TryGetKind(id, out var kind))
                {
                    continue;
                }

                var isBase = kind == CameraRenderType.Base;
                slots.Add(new CameraSlot(id, isBase, camera.depth));
                if (!isBase && _model.GetOverlayDisableRefCount(id) > 0)
                {
                    disabledOverlays.Add(id);
                }
            }

            var resolution = CameraStackResolver.Resolve(slots, _model.BaseStack, disabledOverlays);
            var enabledIds = new HashSet<CameraId>(resolution.EnabledIds);
            UnityEngine.Camera activeBaseCamera = null;

            foreach (var kvp in _model.Registered)
            {
                var id = kvp.Key;
                var camera = kvp.Value;
                if (camera == null)
                {
                    continue;
                }

                camera.enabled = enabledIds.Contains(id);
                if (resolution.ActiveBase.HasValue && id == resolution.ActiveBase.Value)
                {
                    activeBaseCamera = camera;
                }
            }

            if (activeBaseCamera == null)
            {
                return;
            }

            var activeBaseUrp = activeBaseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (activeBaseUrp == null)
            {
                return;
            }

            activeBaseUrp.cameraStack.Clear();
            foreach (var overlayId in resolution.CameraStackOrder)
            {
                if (_model.TryGet(overlayId, out var overlay) && overlay != null)
                {
                    activeBaseUrp.cameraStack.Add(overlay);
                }
            }
        }

        static CameraRenderType Classify(UnityEngine.Camera camera)
        {
            var urp = camera.GetComponent<UniversalAdditionalCameraData>();
            return urp == null ? CameraRenderType.Base : urp.renderType;
        }

        static void ValidateId(CameraId id)
        {
            if (!id.IsValid)
            {
                throw new InvalidOperationException("CameraId must be valid.");
            }
        }

        void EnsureRegisteredBase(CameraId id)
        {
            if (!_model.IsRegistered(id))
            {
                throw new InvalidOperationException($"Camera '{id}' is not registered.");
            }

            EnsureBase(id);
        }

        void EnsureBase(CameraId id)
        {
            if (!_model.TryGetKind(id, out var kind) || kind != CameraRenderType.Base)
            {
                throw new InvalidOperationException($"Camera '{id}' is not a Base camera.");
            }
        }

        void PublishActiveBaseChanged(CameraId previous, CameraId current)
        {
            if (previous == current)
            {
                return;
            }

            var evt = new ActiveBaseChangedEvent(previous, current);
            ActiveBaseChanged?.Invoke(evt);
            _publisher?.Publish(evt);
        }

        sealed class BaseLease : IDisposable
        {
            readonly CameraService _svc;
            readonly long _handle;
            bool _disposed;

            public BaseLease(CameraService svc, long handle)
            {
                _svc = svc;
                _handle = handle;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _svc.RemoveBaseByHandle(_handle);
            }
        }

        sealed class OverlayLease : IDisposable
        {
            readonly CameraService _svc;
            readonly CameraId _id;
            bool _disposed;

            public OverlayLease(CameraService svc, CameraId id)
            {
                _svc = svc;
                _id = id;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _svc.EnableOverlay(_id);
            }
        }
    }
}
