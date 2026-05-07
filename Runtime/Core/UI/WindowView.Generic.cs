using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.Presentation;

namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Base class for windows that delegate their lifecycle to an <see cref="IWindowPresenter{TView}"/>.
    /// Forwards <see cref="WindowView.OnInitialize"/>, the show/hide hooks, and — critically —
    /// <see cref="WindowView.OnDispose"/> to the presenter so consumers can't forget to dispose it
    /// and leak the presenter (and any subscriptions it owns).
    /// </summary>
    /// <typeparam name="TSelf">The concrete window type (CRTP).</typeparam>
    /// <typeparam name="TPresenter">The presenter type bound to this window.</typeparam>
    public abstract class WindowView<TSelf, TPresenter> : WindowView
        where TSelf : WindowView<TSelf, TPresenter>
        where TPresenter : class, IWindowPresenter<TSelf>
    {
        TPresenter _presenter;

        protected TPresenter Presenter => _presenter;

        /// <summary>
        /// Stores the presenter and binds it to this view. Call this from the consumer's
        /// DI construction method (e.g. a VContainer <c>[Inject]</c> method). The framework
        /// then takes care of forwarding every lifecycle hook automatically.
        /// </summary>
        protected void BindPresenter(TPresenter presenter)
        {
            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            if (_presenter != null)
            {
                if (ReferenceEquals(_presenter, presenter))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"{GetType().Name} already has a presenter bound.");
            }

            _presenter = presenter;
            _presenter.Bind((TSelf)this);
        }

        public sealed override UniTask OnInitialize(CancellationToken ct)
        {
            return _presenter != null ? _presenter.InitializeAsync(ct) : UniTask.CompletedTask;
        }

        protected sealed override void OnBeforeShowInternal()
        {
            _presenter?.OnBeforeShow();
        }

        protected sealed override void OnShowInternal()
        {
            _presenter?.OnShow();
        }

        protected sealed override void OnBeforeHideInternal()
        {
            _presenter?.OnBeforeHide();
        }

        protected sealed override void OnHideInternal()
        {
            _presenter?.OnHide();
        }

        protected sealed override void OnDisposeInternal()
        {
            if (_presenter == null)
            {
                return;
            }

            var presenter = _presenter;
            _presenter = null;
            presenter.Dispose();
        }
    }
}
