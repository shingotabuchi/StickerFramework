using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.Presentation;
using VContainer;

namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Base class for windows that delegate their lifecycle to an <see cref="IWindowPresenter{TView}"/>.
    /// Forwards <see cref="WindowView.OnInitialize"/>, the show/hide hooks, and — critically —
    /// <see cref="WindowView.OnDispose"/> to the presenter so consumers can't forget to dispose it
    /// and leak the presenter (and any subscriptions it owns).
    /// </summary>
    /// <remarks>
    /// The presenter is automatically resolved and bound by VContainer via the <c>[Inject]</c>
    /// method on this base class — derived types just need to register their concrete presenter
    /// type in the parent scope. If you need to bind a presenter manually (tests, non-DI scenarios),
    /// call <see cref="BindPresenter"/> explicitly before <see cref="WindowView.OnInitialize"/>
    /// runs.
    /// </remarks>
    /// <typeparam name="TSelf">The concrete window type (CRTP).</typeparam>
    /// <typeparam name="TPresenter">The presenter type bound to this window.</typeparam>
    public abstract class WindowView<TSelf, TPresenter> : WindowView
        where TSelf : WindowView<TSelf, TPresenter>
        where TPresenter : class, IWindowPresenter<TSelf>
    {
        TPresenter _presenter;

        protected TPresenter Presenter => _presenter;

        /// <summary>
        /// VContainer construct injection entry point. Resolves the presenter from the active
        /// scope and binds it. Idempotent: if a presenter is already bound (e.g. via a manual
        /// <see cref="BindPresenter"/> call from a test) and the resolved instance matches, the
        /// call is a no-op.
        /// </summary>
        [Inject]
        public void InjectPresenter(TPresenter presenter)
        {
            BindPresenter(presenter);
        }

        /// <summary>
        /// Stores the presenter and binds it to this view. Normally invoked automatically through
        /// the <c>[Inject]</c> hook, but exposed for tests and non-DI scenarios where a presenter
        /// must be supplied directly.
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
