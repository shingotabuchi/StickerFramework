using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Core.UI
{
    public class CoolButton : Button
    {
        private sealed class ClickSubscription : IDisposable
        {
            private readonly CoolButton _button;
            private readonly Action _listener;
            private bool _isDisposed;

            public ClickSubscription(CoolButton button, Action listener)
            {
                _button = button;
                _listener = listener;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _button.RemoveClickListener(_listener);
            }
        }

        [SerializeField] private string _seName = "click1";
        private Action _onClickAction;

        public event Action Clicked;

        public bool Interactable
        {
            get => interactable;
            set => interactable = value;
        }

        public static void SetSoundResolver(IObjectResolver resolver)
        {
            ClickSoundPlayer.SetResolver(resolver);
        }

        protected override void Awake()
        {
            base.Awake();
            onClick.AddListener(HandleClick);
        }

        protected override void OnDestroy()
        {
            onClick.RemoveListener(HandleClick);
            base.OnDestroy();
        }

        public void SetOnClickAction(Action action)
        {
            _onClickAction = action;
        }

        public IDisposable AddClickListener(Action listener)
        {
            Clicked += listener;
            return new ClickSubscription(this, listener);
        }

        public virtual void SetInteractable(bool interactable)
        {
            base.interactable = interactable;
        }

        private void RemoveClickListener(Action listener)
        {
            Clicked -= listener;
        }

        private void HandleClick()
        {
            PlayClickSound();
            Clicked?.Invoke();
            _onClickAction?.Invoke();
        }

        private void PlayClickSound()
        {
            if (string.IsNullOrEmpty(_seName))
            {
                return;
            }

            if (ClickSoundPlayer.TryPlay(_seName))
            {
                return;
            }

            Debug.LogWarning($"[CoolButton] Click SFX '{_seName}' was requested, but no sound player is available.");
        }

        private static class ClickSoundPlayer
        {
            private const string SoundServiceTypeName = "StickerFwk.Infrastructure.Sound.ISoundService, StickerFwk.Infrastructure";
            private static Type _soundServiceType;
            private static MethodInfo _playSeOneShotMethod;
            private static IObjectResolver _resolver;
            private static object _soundService;
            private static bool _isInitialized;

            public static void SetResolver(IObjectResolver resolver)
            {
                _resolver = resolver;
                _soundService = null;
            }

            public static bool TryPlay(string soundName)
            {
                Initialize();
                var soundService = GetSoundService();
                if (soundService == null || _playSeOneShotMethod == null)
                {
                    return false;
                }

                try
                {
                    _playSeOneShotMethod.Invoke(soundService, new object[] { soundName, 1.0f });
                    return true;
                }
                catch (TargetInvocationException ex)
                {
                    Debug.LogWarning($"[CoolButton] Click SFX '{soundName}' failed: {ex.InnerException?.Message ?? ex.Message}");
                    _soundService = null;
                    return false;
                }
            }

            private static void Initialize()
            {
                if (_isInitialized)
                {
                    return;
                }

                _isInitialized = true;
                _soundServiceType = Type.GetType(SoundServiceTypeName);
                if (_soundServiceType == null)
                {
                    return;
                }

                _playSeOneShotMethod = _soundServiceType.GetMethod(
                    "PlaySeOneShot",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(float) },
                    null);
            }

            private static object GetSoundService()
            {
                if (_soundService != null)
                {
                    return _soundService;
                }

                if (_soundServiceType == null)
                {
                    return null;
                }

                if (TryResolve(_resolver, out var resolved))
                {
                    _soundService = resolved;
                    return _soundService;
                }

                foreach (var scope in UnityEngine.Object.FindObjectsByType<LifetimeScope>())
                {
                    if (scope == null || scope.Container == null)
                    {
                        continue;
                    }

                    if (TryResolve(scope.Container, out resolved))
                    {
                        _soundService = resolved;
                        return _soundService;
                    }
                }

                return null;
            }

            private static bool TryResolve(IObjectResolver resolver, out object resolved)
            {
                resolved = null;
                if (resolver == null || _soundServiceType == null)
                {
                    return false;
                }

                try
                {
                    return resolver.TryResolve(_soundServiceType, out resolved);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CoolButton] Failed to resolve sound service: {ex.Message}");
                    return false;
                }
            }
        }
    }
}
