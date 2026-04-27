using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;

namespace StickerFwk.Core.Presentation
{
    public abstract class WindowPresenter<TView> : Presenter<TView>, IWindowPresenter<TView> where TView : WindowView
    {
        public virtual UniTask InitializeAsync(CancellationToken ct) => UniTask.CompletedTask;

        public virtual void OnBeforeShow() { }

        public virtual void OnShow() { }

        public virtual void OnBeforeHide() { }

        public virtual void OnHide() { }
    }
}
