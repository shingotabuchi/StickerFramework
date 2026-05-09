namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Implemented by <see cref="WindowView"/> subclasses that need to receive a
    /// strongly-typed args object before <see cref="WindowView.OnInitialize"/> runs.
    /// Use the <see cref="IUIService.Push{T,TArgs}"/> overload to pass args.
    /// </summary>
    public interface IWindowWithArgs<in TArgs>
    {
        void SetArgs(TArgs args);
    }
}
