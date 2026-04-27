using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;

namespace StickerFwk.Core.Presentation
{
    public interface IWindowPresenter<in TView> : IPresenter<TView> where TView : WindowView
    {
        UniTask InitializeAsync(CancellationToken ct);
        void OnBeforeShow();
        void OnShow();
        void OnBeforeHide();
        void OnHide();
    }
}
