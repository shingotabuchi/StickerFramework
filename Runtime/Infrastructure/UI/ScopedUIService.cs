using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.UI;

namespace StickerFwk.Infrastructure.UI
{
    // Per-scope wrapper around the root IUIService. Records every window pushed
    // through this instance and pops any survivors on Dispose, so windows opened by a
    // service do not outlive the LifetimeScope that owns it.
    //
    // Registered As<IUIService>() in a child LifetimeScope so it transparently shadows
    // the root singleton for any services in that scope. To avoid the wrapper resolving
    // back into itself, the registration must construct it with the parent scope's
    // IUIService, e.g. via `Parent.Container.Resolve<IUIService>()` in a factory.
    //
    // Pop / Replace / PopAll calls are forwarded as-is. Tracked windows whose GameObjects
    // have been destroyed externally (e.g. popped via direct calls) are skipped on
    // dispose via Unity's null-equality check.
    public sealed class ScopedUIService : IUIService, IDisposable
    {
        readonly IUIService _inner;
        readonly List<WindowView> _tracked = new List<WindowView>();
        bool _disposed;

        public ScopedUIService(IUIService inner)
        {
            _inner = inner;
        }

        public async UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView
        {
            ThrowIfDisposed();
            var view = await _inner.Push<T>(tag, options, ct);
            if (view != null)
            {
                _tracked.Add(view);
            }
            return view;
        }

        public UniTask Pop(UILayer layer = UILayer.UI, CancellationToken ct = default)
        {
            return _inner.Pop(layer, ct);
        }

        public UniTask Pop<T>(CancellationToken ct = default) where T : WindowView
        {
            return _inner.Pop<T>(ct);
        }

        public UniTask Pop(WindowView view, CancellationToken ct = default)
        {
            return _inner.Pop(view, ct);
        }

        public async UniTask<T> Replace<T>(UILayer layer, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView
        {
            ThrowIfDisposed();
            var view = await _inner.Replace<T>(layer, tag, options, ct);
            if (view != null)
            {
                _tracked.Add(view);
            }
            return view;
        }

        public UniTask PopAll(UILayer layer, CancellationToken ct = default)
        {
            return _inner.PopAll(layer, ct);
        }

        public UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView
        {
            return _inner.Preload<T>(tag, ct);
        }

        public bool IsOpen<T>() where T : WindowView
        {
            return _inner.IsOpen<T>();
        }

        public T GetWindow<T>() where T : WindowView
        {
            return _inner.GetWindow<T>();
        }

        public int GetStackCount(UILayer layer)
        {
            return _inner.GetStackCount(layer);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var view = _tracked[i];
                if (view == null)
                {
                    continue;
                }

                _inner.Pop(view).Forget();
            }

            _tracked.Clear();
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ScopedUIService));
            }
        }
    }
}
