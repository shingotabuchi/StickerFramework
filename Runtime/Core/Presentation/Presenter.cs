using System;

namespace StickerFwk.Core.Presentation
{
    public abstract class Presenter<TView> : IPresenter<TView> where TView : class
    {
        bool _disposed;

        protected TView View { get; private set; }
        protected bool IsBound => View != null;

        public void Bind(TView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (ReferenceEquals(View, view))
            {
                return;
            }

            if (View != null)
            {
                throw new InvalidOperationException($"{GetType().Name} is already bound to a view.");
            }

            View = view;
            OnBind(view);
        }

        public void Unbind()
        {
            if (View == null)
            {
                return;
            }

            var view = View;
            View = null;
            OnUnbind(view);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            OnDispose();
            Unbind();
        }

        protected virtual void OnBind(TView view) { }

        protected virtual void OnUnbind(TView view) { }

        protected virtual void OnDispose() { }
    }
}
