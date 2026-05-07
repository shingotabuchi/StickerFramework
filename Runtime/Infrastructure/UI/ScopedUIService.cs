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

        public async UniTask<bool> Pop(UILayer layer = UILayer.UI, CancellationToken ct = default)
        {
            var popped = await _inner.Pop(layer, ct);
            if (popped)
            {
                RemoveLastMatch(v => v.Layer == layer);
            }
            PruneDead();
            return popped;
        }

        public async UniTask<bool> Pop<T>(CancellationToken ct = default) where T : WindowView
        {
            var popped = await _inner.Pop<T>(ct);
            if (popped)
            {
                RemoveLastMatch(v => v is T);
            }
            PruneDead();
            return popped;
        }

        public async UniTask<bool> Pop(WindowView view, CancellationToken ct = default)
        {
            var popped = await _inner.Pop(view, ct);
            if (popped && view != null)
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
            return popped;
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

        public async UniTask<int> PopAll(UILayer layer, bool immediate = false, CancellationToken ct = default)
        {
            var popped = await _inner.PopAll(layer, immediate, ct);
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var view = _tracked[i];
                if (view == null || view.Layer == layer)
                {
                    _tracked.RemoveAt(i);
                }
            }
            return popped;
        }

        public UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView
        {
            return _inner.Preload<T>(tag, ct);
        }

        public void Unload<T>(string tag = null) where T : WindowView
        {
            ThrowIfDisposed();
            _inner.Unload<T>(tag);
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

                // Pop is fire-and-forget at scope teardown. Surface failures via the
                // framework Log so they aren't silently swallowed if the inner service
                // throws after the scope has already begun tearing down. Cancellation is
                // expected when the inner service is being disposed alongside this scope
                // (its own CTS races our Pop calls), so swallow OperationCanceledException
                // to avoid spamming the error channel on every shutdown.
                _inner.Pop(view).Forget(static ex =>
                {
                    if (ex is OperationCanceledException)
                    {
                        return;
                    }
                    Log.Error("ScopedUIService", $"Pop during scope dispose failed: {ex}");
                });
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
