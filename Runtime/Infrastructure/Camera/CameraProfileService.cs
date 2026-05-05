using System;
using System.Collections.Generic;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraProfileService : ICameraProfileService, IDisposable
    {
        readonly CameraSystemSettings _settings;
        readonly ICameraService _cameraService;
        readonly IPublisher<CameraProfileAppliedEvent> _appliedPublisher;
        readonly List<UnityEngine.Camera> _createdCameras = new List<UnityEngine.Camera>();
        CameraFactory _factory;
        Transform _root;
        GameObject _audioListener;
        CameraProfile _active;
        CameraProfileId? _activeId;

        public CameraProfileService(
            CameraSystemSettings settings,
            ICameraService cameraService,
            IPublisher<CameraProfileAppliedEvent> appliedPublisher)
        {
            _settings = settings;
            _cameraService = cameraService;
            _appliedPublisher = appliedPublisher;
        }

        public bool IsApplied => _active != null;
        public CameraProfileId? ActiveProfileId => _activeId;
        public CameraProfile ActiveProfile => _active;

        public void Apply(CameraProfileId profileId)
        {
            if (_settings == null)
            {
                throw new InvalidOperationException(
                    "[CameraProfileService] CameraSystemSettings is not assigned on RootLifetimeScope.");
            }

            if (_active != null && _activeId == profileId)
            {
                return;
            }

            if (_active != null)
            {
                Release();
            }

            if (!_settings.TryGetProfile(profileId, out var profile))
            {
                throw new InvalidOperationException(
                    $"[CameraProfileService] Profile '{profileId}' not found in CameraSystemSettings.");
            }

            EnsureRoot();
            EnsureAudioListener();

            for (var i = 0; i < profile.Cameras.Count; i++)
            {
                var def = profile.Cameras[i];
                if (def == null)
                {
                    continue;
                }

                var camera = _factory.Create(def);
                _createdCameras.Add(camera);
                _cameraService.Register(def.Id, camera);

                camera.gameObject.SetActive(def.ActivationPolicy == CameraActivationPolicy.AlwaysOn);
            }

            ApplyDefaultStack(profile);

            _active = profile;
            _activeId = profileId;
            Log.Info($"[CameraProfileService] Applied profile '{profileId}' with {_createdCameras.Count} camera(s).");
            _appliedPublisher.Publish(new CameraProfileAppliedEvent(profileId, true));
        }

        public void Release()
        {
            if (_active == null)
            {
                return;
            }

            var releasedId = _activeId;

            for (var i = 0; i < _active.Cameras.Count; i++)
            {
                var def = _active.Cameras[i];
                if (def != null)
                {
                    _cameraService.Unregister(def.Id);
                }
            }

            for (var i = 0; i < _createdCameras.Count; i++)
            {
                if (_createdCameras[i] != null)
                {
                    UnityEngine.Object.Destroy(_createdCameras[i].gameObject);
                }
            }
            _createdCameras.Clear();

            _active = null;
            _activeId = null;

            if (releasedId.HasValue)
            {
                _appliedPublisher.Publish(new CameraProfileAppliedEvent(releasedId.Value, false));
            }
        }

        public void Dispose()
        {
            Release();

            if (_audioListener != null)
            {
                UnityEngine.Object.Destroy(_audioListener);
                _audioListener = null;
            }

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
                _factory = null;
            }
        }

        void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            var go = new GameObject(_settings.CameraRootName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            _root = go.transform;
            _factory = new CameraFactory(_root);
        }

        void EnsureAudioListener()
        {
            if (_audioListener != null)
            {
                return;
            }

            _audioListener = new GameObject(_settings.AudioListenerName);
            _audioListener.AddComponent<AudioListener>();
            UnityEngine.Object.DontDestroyOnLoad(_audioListener);
        }

        void ApplyDefaultStack(CameraProfile profile)
        {
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
                if (_cameraService.TryGetCamera(id, out var overlayCamera) && overlayCamera != null)
                {
                    baseUrp.cameraStack.Add(overlayCamera);
                }
            }
        }
    }
}
