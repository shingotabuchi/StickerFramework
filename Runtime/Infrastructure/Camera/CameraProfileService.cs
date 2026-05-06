using System;
using System.Collections.Generic;
using MessagePipe;
using StickerFwk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Camera
{
    // Owns the lifecycle of cameras declared by CameraProfile assets.
    //
    // Activation model: a camera renders iff it is declared by at least one currently-pushed
    // profile, AND it survives the resolver's "losing-base" rule (only the lowest-depth Base
    // renders; other Bases in the union are forced off).
    //
    // No mode, no per-camera lease — pushing/popping profiles is the only way to turn cameras
    // on or off.
    public class CameraProfileService : ICameraProfileService, IDisposable
    {
        readonly CameraSystemSettings _settings;
        readonly ICameraService _cameraService;
        readonly IPublisher<CameraProfileAppliedEvent> _appliedPublisher;

        readonly Dictionary<CameraProfileId, ProfileEntry> _profiles = new Dictionary<CameraProfileId, ProfileEntry>();
        readonly Dictionary<CameraId, CameraEntry> _cameras = new Dictionary<CameraId, CameraEntry>();
        readonly List<CameraProfileId> _activeIds = new List<CameraProfileId>();
        readonly List<CameraSlot> _slotBuffer = new List<CameraSlot>();
        readonly List<CameraId> _enabledBuffer = new List<CameraId>();
        readonly List<CameraId> _stackBuffer = new List<CameraId>();

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
                return new ProfileHandle(this, profileId);
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
                var id = profile.Cameras[i];
                if (!_settings.TryGetDefinition(id, out var def))
                {
                    throw new InvalidOperationException(
                        $"[CameraProfileService] Profile '{profileId}' references CameraId '{id}' but no definition exists in CameraSystemSettings.");
                }
                AcquireCamera(def);
            }

            _profiles[profileId] = new ProfileEntry { Profile = profile, RefCount = 1 };
            _activeIds.Add(profileId);

            Log.Info($"[CameraProfileService] Pushed profile '{profileId}'. Active profiles: {_activeIds.Count}.");
            Recompute();
            _appliedPublisher.Publish(new CameraProfileAppliedEvent(profileId, true));

            return new ProfileHandle(this, profileId);
        }

        public void Dispose()
        {
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
                ReleaseCamera(entry.Profile.Cameras[i]);
            }

            Log.Info($"[CameraProfileService] Popped profile '{profileId}'. Active profiles: {_activeIds.Count}.");
            Recompute();
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

        // Apply enabled state to every registered camera and rebuild the winning base's stack.
        // Driven by every push/pop — no per-frame work, no event subscriptions.
        void Recompute()
        {
            _slotBuffer.Clear();
            foreach (var kvp in _cameras)
            {
                var def = kvp.Value.Definition;
                _slotBuffer.Add(new CameraSlot(def.Id, def.RenderType, def.Depth));
            }

            var result = CameraStackResolver.Resolve(_slotBuffer, _enabledBuffer, _stackBuffer);

            foreach (var kvp in _cameras)
            {
                var camera = kvp.Value.Camera;
                if (camera == null)
                {
                    continue;
                }
                var shouldBeEnabled = Contains(_enabledBuffer, kvp.Key);
                if (camera.gameObject.activeSelf != shouldBeEnabled)
                {
                    camera.gameObject.SetActive(shouldBeEnabled);
                }
                if (camera.enabled != shouldBeEnabled)
                {
                    camera.enabled = shouldBeEnabled;
                }
            }

            if (!result.HasBase)
            {
                return;
            }

            if (!_cameras.TryGetValue(result.WinningBase, out var baseEntry) || baseEntry.Camera == null)
            {
                return;
            }

            var baseUrp = baseEntry.Camera.GetComponent<UniversalAdditionalCameraData>();
            if (baseUrp == null)
            {
                return;
            }

            baseUrp.cameraStack.Clear();
            for (var i = 0; i < _stackBuffer.Count; i++)
            {
                if (_cameras.TryGetValue(_stackBuffer[i], out var overlayEntry) && overlayEntry.Camera != null)
                {
                    baseUrp.cameraStack.Add(overlayEntry.Camera);
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

        void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            var go = new GameObject(_settings.CameraRootName);
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
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
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_audioListener);
            }
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

        sealed class ProfileHandle : IDisposable
        {
            CameraProfileService _service;
            readonly CameraProfileId _id;
            bool _disposed;

            public ProfileHandle(CameraProfileService service, CameraProfileId id)
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
