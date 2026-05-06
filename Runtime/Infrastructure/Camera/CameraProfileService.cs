using System;
using System.Collections.Generic;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Infrastructure.Camera
{
    public class CameraProfileService : ICameraProfileService, IDisposable
    {
        readonly CameraSystemSettings _settings;
        readonly ICameraService _cameraService;
        readonly IPublisher<CameraProfileAppliedEvent> _appliedPublisher;

        readonly Dictionary<CameraProfileId, ProfileEntry> _profiles = new Dictionary<CameraProfileId, ProfileEntry>();
        readonly Dictionary<CameraId, CameraEntry> _cameras = new Dictionary<CameraId, CameraEntry>();
        readonly List<CameraProfileId> _activeIds = new List<CameraProfileId>();

        CameraFactory _factory;
        Transform _root;
        GameObject _audioListener;

        public CameraProfileService(
            CameraSystemSettings settings,
            ICameraService cameraService,
            IPublisher<CameraProfileAppliedEvent> appliedPublisher)
        {
            _settings = settings;
            _cameraService = cameraService;
            _appliedPublisher = appliedPublisher;
        }

        public IReadOnlyCollection<CameraProfileId> ActiveProfiles => _activeIds;

        public bool IsActive(CameraProfileId profileId) => _profiles.ContainsKey(profileId);

        public bool TryGetDefinition(CameraId cameraId, out CameraDefinition definition)
        {
            if (_cameras.TryGetValue(cameraId, out var entry))
            {
                definition = entry.Definition;
                return true;
            }

            definition = null;
            return false;
        }

        public IDisposable Push(CameraProfileId profileId)
        {
            if (_settings == null)
            {
                throw new InvalidOperationException(
                    "[CameraProfileService] CameraSystemSettings is not assigned.");
            }

            if (_profiles.TryGetValue(profileId, out var entry))
            {
                entry.RefCount++;
                _profiles[profileId] = entry;
                return new Lease(this, profileId);
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
                AcquireCamera(def);
            }

            _profiles[profileId] = new ProfileEntry { Profile = profile, RefCount = 1 };
            _activeIds.Add(profileId);

            Log.Info($"[CameraProfileService] Pushed profile '{profileId}'. Active profiles: {_activeIds.Count}.");
            _appliedPublisher.Publish(new CameraProfileAppliedEvent(profileId, true));

            return new Lease(this, profileId);
        }

        public void Dispose()
        {
            // Pop all active profiles. Iterate by index to avoid mutating during enumeration.
            while (_activeIds.Count > 0)
            {
                ReleaseInternal(_activeIds[_activeIds.Count - 1], force: true);
            }

            if (_audioListener != null)
            {
                DestroyGameObject(_audioListener);
                _audioListener = null;
            }

            if (_root != null)
            {
                DestroyGameObject(_root.gameObject);
                _root = null;
                _factory = null;
            }
        }

        // Use DestroyImmediate in EditMode (e.g. tests) so cleanup is synchronous; use Destroy
        // in PlayMode so it follows Unity's normal deferred-destroy lifecycle.
        static void DestroyGameObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(go);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        void Pop(CameraProfileId profileId)
        {
            ReleaseInternal(profileId, force: false);
        }

        void ReleaseInternal(CameraProfileId profileId, bool force)
        {
            if (!_profiles.TryGetValue(profileId, out var entry))
            {
                return;
            }

            if (!force)
            {
                entry.RefCount--;
                if (entry.RefCount > 0)
                {
                    _profiles[profileId] = entry;
                    return;
                }
            }

            _profiles.Remove(profileId);
            _activeIds.Remove(profileId);

            for (var i = 0; i < entry.Profile.Cameras.Count; i++)
            {
                var def = entry.Profile.Cameras[i];
                if (def == null)
                {
                    continue;
                }
                ReleaseCamera(def.Id);
            }

            Log.Info($"[CameraProfileService] Popped profile '{profileId}'. Active profiles: {_activeIds.Count}.");
            _appliedPublisher.Publish(new CameraProfileAppliedEvent(profileId, false));
        }

        void AcquireCamera(CameraDefinition def)
        {
            if (_cameras.TryGetValue(def.Id, out var entry))
            {
                entry.RefCount++;
                _cameras[def.Id] = entry;
                return;
            }

            var camera = _factory.Create(def);
            _cameras[def.Id] = new CameraEntry
            {
                Definition = def,
                Camera = camera,
                RefCount = 1,
            };
            _cameraService.Register(def.Id, camera);
            // Activation handled by CameraUsageService.Recompute() (driven by profile-applied event).
            camera.gameObject.SetActive(def.ActivationPolicy == CameraActivationPolicy.AlwaysOn);
        }

        void ReleaseCamera(CameraId id)
        {
            if (!_cameras.TryGetValue(id, out var entry))
            {
                return;
            }

            entry.RefCount--;
            if (entry.RefCount > 0)
            {
                _cameras[id] = entry;
                return;
            }

            _cameras.Remove(id);
            _cameraService.Unregister(id);
            if (entry.Camera != null)
            {
                DestroyGameObject(entry.Camera.gameObject);
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

        struct ProfileEntry
        {
            public CameraProfile Profile;
            public int RefCount;
        }

        struct CameraEntry
        {
            public CameraDefinition Definition;
            public UnityEngine.Camera Camera;
            public int RefCount;
        }

        sealed class Lease : IDisposable
        {
            CameraProfileService _service;
            readonly CameraProfileId _id;
            bool _disposed;

            public Lease(CameraProfileService service, CameraProfileId id)
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
                _service?.Pop(_id);
                _service = null;
            }
        }
    }
}
