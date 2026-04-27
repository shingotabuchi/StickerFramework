using System;

namespace StickerFwk.Core.Presentation
{
    public interface IPresenter<in TView> : IDisposable where TView : class
    {
        void Bind(TView view);
        void Unbind();
    }
}
