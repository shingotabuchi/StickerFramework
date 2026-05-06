using System;
using StickerFwk.Core;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Infrastructure.Camera
{
    // Attach next to a LifetimeScope to push a camera profile while that scope is alive.
    // Implements VContainer's IInstaller so any LifetimeScope that iterates GetComponents<IInstaller>()
    // picks it up automatically — no per-scope wiring required.
    [DisallowMultipleComponent]
    public sealed class CameraProfileScopeBinding : MonoBehaviour, IInstaller
    {
        [SerializeField] private CameraProfileId _profileId = CameraProfileId.Gameplay;

        public CameraProfileId ProfileId => _profileId;

        public void Install(IContainerBuilder builder)
        {
            var profileId = _profileId;
            builder.Register<CameraProfileScopeHandle>(Lifetime.Singleton)
                .WithParameter(profileId);
            builder.RegisterBuildCallback(container => container.Resolve<CameraProfileScopeHandle>());
        }

        private sealed class CameraProfileScopeHandle : IDisposable
        {
            private readonly IDisposable _handle;

            public CameraProfileScopeHandle(ICameraProfileService service, CameraProfileId profileId)
            {
                _handle = service.Push(profileId);
            }

            public void Dispose()
            {
                _handle?.Dispose();
            }
        }
    }
}

