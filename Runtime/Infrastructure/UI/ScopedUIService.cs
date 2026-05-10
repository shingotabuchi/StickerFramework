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

        public async UniTask<T> Push<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>
        {
            ThrowIfDisposed();
            PruneDead();
            var view = await _inner.Push<T, TArgs>(args, tag, options, ct);
            if (view != null)
            {
                _tracked.Add(view);
            }
            return view;
        }

        public async UniTask<bool> Pop(UILayer layer = UILayer.UI, CancellationToken ct = default)
        {
            var popped = await _inner.Pop(layer, ct);
            // Intentionally do NOT call RemoveLastMatch here. The inner service pops the TOP of
            // the layer stack, while RemoveLastMatch would remove the most-recently-tracked
            // entry matching the predicate. When the same layer holds windows from multiple
            // scopes (or this scope pushed more than one window to the layer), those two are
            // not guaranteed to refer to the same instance, so we would corrupt tracking.
            // PruneDead clears destroyed views on the next interaction, which is sufficient.
            PruneDead();
            return popped;
        }

        public async UniTask<bool> Pop<T>(CancellationToken ct = default) where T : WindowView
        {
            var popped = await _inner.Pop<T>(ct);
            if (popped)
            {
                for (var i = _tracked.Count - 1; i >= 0; i--)
                {
                    if (_tracked[i] is T)
                    {
                        _tracked.RemoveAt(i);
                        break;
                    }
                }
            }
            PruneDead();
            return popped;
        }

        public async UniTask<bool> Pop(WindowView view, CancellationToken ct = default)
        {
            return await Pop(view, immediate: false, ct);
        }

        public async UniTask<bool> Pop(WindowView view, bool immediate, CancellationToken ct = default)
        {
            var popped = await _inner.Pop(view, immediate, ct);
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

        public async UniTask<T> Replace<T, TArgs>(TArgs args, string tag = null, WindowOptions options = null, CancellationToken ct = default)
            where T : WindowView, IWindowWithArgs<TArgs>
        {
            ThrowIfDisposed();
            PruneDead();
            var view = await _inner.Replace<T, TArgs>(args, tag, options, ct);
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
                // throws after the scope has already begun tearing down. Cancellation
                // and ObjectDisposed are expected when the inner service is being
                // disposed alongside this scope (its own CTS races our Pop calls, or
                // the root scope has already torn it down), so swallow those to avoid
                // spamming the error channel on every shutdown.
                // Tear down without playing the hide transition. Scope dispose typically runs
                // alongside scene unload, so a 0.3s fade after the scene has begun wiping is a
                // visible artifact and a load-time hitch. The buried-window path inside
                // PopViewLocked already skips transitions; this also forces the top-of-stack
                // path through HideWithoutTransition.
                _inner.Pop(view, immediate: true).Forget(static ex =>
                {
                    if (ex is OperationCanceledException || ex is ObjectDisposedException)
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
    }
}
