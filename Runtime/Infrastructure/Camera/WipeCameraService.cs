using System;
using StickerFwk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace StickerFwk.Infrastructure.Camera
{
    public sealed class WipeCameraService : IWipeCameraService, IDisposable
    {
        const int WipeLayer = 8;
        const int UiLayer = 5;
        const float WipeCameraDepth = 1000f;
        const float NearClipPlane = 0.01f;
        const float FarClipPlane = 10f;

        readonly ICameraService _cameraService;

        GameObject _root;
        UnityEngine.Camera _camera;
        IDisposable _idleDisableLease;
        int _leaseCount;
        bool _disposed;

        public WipeCameraService(ICameraService cameraService)
        {
            _cameraService = cameraService;
        }

        public IWipeCameraLease Acquire()
        {
            ThrowIfDisposed();
            EnsureCamera();

            if (_leaseCount == 0)
            {
                _idleDisableLease?.Dispose();
                _idleDisableLease = null;
            }

            _leaseCount++;
            return new Lease(this, _camera);
        }

        void Release(Lease lease)
        {
            if (lease == null || lease.Camera != _camera || _leaseCount <= 0)
            {
                return;
            }

            _leaseCount--;
            if (_leaseCount == 0 && _idleDisableLease == null && !_disposed)
            {
                _idleDisableLease = _cameraService.DisableOverlay(CameraId.Wipe);
            }
        }

        void EnsureCamera()
        {
            if (_camera != null)
            {
                return;
            }

            if (_cameraService.TryGetCamera(CameraId.Wipe, out var existingCamera) && existingCamera != null)
            {
                throw new InvalidOperationException(
                    $"Camera '{CameraId.Wipe}' is already registered. The wipe camera must be owned by {nameof(WipeCameraService)}.");
            }

            _root = new GameObject("[CameraSystem] Wipe Camera");
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(_root);
            }

            _camera = _root.AddComponent<UnityEngine.Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.Nothing;
            _camera.backgroundColor = Color.clear;
            _camera.nearClipPlane = NearClipPlane;
            _camera.farClipPlane = FarClipPlane;
            _camera.depth = WipeCameraDepth;
            // Layer 8 is the NotebookWipe visual rig; layer 5 is Unity's UI layer for UILayer.Wipe.
            _camera.cullingMask = (1 << WipeLayer) | (1 << UiLayer);
            _camera.allowHDR = true;
            _camera.allowMSAA = true;

            var urpCameraData = _root.AddComponent<UniversalAdditionalCameraData>();
            urpCameraData.renderType = CameraRenderType.Overlay;

            _cameraService.Register(CameraId.Wipe, _camera);
            _idleDisableLease = _cameraService.DisableOverlay(CameraId.Wipe);
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WipeCameraService));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _idleDisableLease?.Dispose();
            _idleDisableLease = null;

            if (_camera != null && _cameraService.TryGetCamera(CameraId.Wipe, out var registeredCamera) && registeredCamera == _camera)
            {
                _cameraService.Unregister(CameraId.Wipe);
            }

            if (_root != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_root);
                }
                else
                {
                    Object.DestroyImmediate(_root);
                }

                _root = null;
                _camera = null;
            }
        }

        sealed class Lease : IWipeCameraLease
        {
            readonly WipeCameraService _owner;
            bool _disposed;

            public Lease(WipeCameraService owner, UnityEngine.Camera camera)
            {
                _owner = owner;
                Camera = camera;
            }

            public UnityEngine.Camera Camera { get; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.Release(this);
            }
        }
    }
}
