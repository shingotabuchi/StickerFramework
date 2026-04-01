using System.Collections.Generic;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraService : ICameraService
    {
        readonly CameraModel _model;
        readonly IPublisher<CameraRegisteredEvent> _registeredPublisher;

        public CameraService(
            CameraModel model,
            IPublisher<CameraRegisteredEvent> registeredPublisher)
        {
            _model = model;
            _registeredPublisher = registeredPublisher;
        }

        public void Register(CameraId id, UnityEngine.Camera camera)
        {
            Log.Info($"Registering camera '{id}' with game object '{camera.gameObject.name}'.");

            if (!_model.Register(id, camera))
            {
                Log.Warning($"[CameraService] Camera '{id}' is already registered.");
                return;
            }

            _registeredPublisher.Publish(new CameraRegisteredEvent(id, true));
        }

        public void Unregister(CameraId id)
        {
            Log.Info($"Unregistering camera '{id}'.");

            if (!_model.Unregister(id))
            {
                return;
            }

            _registeredPublisher.Publish(new CameraRegisteredEvent(id, false));
        }

        public bool IsRegistered(CameraId id)
        {
            return _model.IsRegistered(id);
        }

        public UnityEngine.Camera GetCamera(CameraId id)
        {
            if (_model.TryGet(id, out var camera))
            {
                return camera;
            }
            return null;
        }

        public UnityEngine.Camera GetCameraForRenderer(Renderer renderer)
        {
            foreach (var kvp in _model.GetAll())
            {
                var cam = kvp.Value;
                if (cam == null || !cam.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if ((cam.cullingMask & (1 << renderer.gameObject.layer)) != 0)
                {
                    return cam;
                }
            }

            return null;
        }

        public UnityEngine.Camera GetCameraForGameObject(GameObject gameObject)
        {
            foreach (var kvp in _model.GetAll())
            {
                var cam = kvp.Value;
                if (cam == null || !cam.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if ((cam.cullingMask & (1 << gameObject.layer)) != 0)
                {
                    return cam;
                }
            }

            return null;
        }

        public IReadOnlyList<CameraId> GetRegisteredIds()
        {
            return _model.GetRegisteredIds();
        }
    }
}
