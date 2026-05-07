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
    // Pop / PopAll calls are forwarded to the inner service and the corresponding entries
    // are removed from the tracking list so long-lived scopes do not accumulate dead
    // references. Tracked windows whose GameObjects have been destroyed externally are
    // pruned on subsequent Push/Replace/Pop calls and skipped on dispose via Unity's
    // null-equality check.
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
            PruneDead();
            var view = await _inner.Push<T>(tag, options, ct);
            if (view != null)
            {
                _tracked.Add(view);
            }
            return view;
        }

        public async UniTask Pop(UILayer layer = UILayer.UI, CancellationToken ct = default)
        {
            await _inner.Pop(layer, ct);
            RemoveLastMatch(v => v.Layer == layer);
            PruneDead();
        }

        public async UniTask Pop<T>(CancellationToken ct = default) where T : WindowView
        {
            await _inner.Pop<T>(ct);
            RemoveLastMatch(v => v is T);
            PruneDead();
        }

        public async UniTask Pop(WindowView view, CancellationToken ct = default)
        {
            await _inner.Pop(view, ct);
            if (view != null)
            {
                for (var i = _tracked.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(_tracked[i], view))
                    {
                        _tracked.RemoveAt(i);
                        break;
                    }
                }
            }
            PruneDead();
        }

        public async UniTask<T> Replace<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView
        {
            ThrowIfDisposed();
            PruneDead();
            var view = await _inner.Replace<T>(tag, options, ct);
            if (view != null)
            {
                _tracked.Add(view);
            }
            return view;
        }

        public async UniTask PopAll(UILayer layer, CancellationToken ct = default)
        {
            await _inner.PopAll(layer, ct);
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var view = _tracked[i];
                if (view == null || view.Layer == layer)
                {
                    _tracked.RemoveAt(i);
                }
            }
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

        void PruneDead()
        {
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                if (_tracked[i] == null)
                {
                    _tracked.RemoveAt(i);
                }
            }
        }

        void RemoveLastMatch(Func<WindowView, bool> predicate)
        {
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var view = _tracked[i];
                if (view == null)
                {
                    continue;
                }
                if (predicate(view))
                {
                    _tracked.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
