using VContainer;

namespace StickerFwk.Core.UI
{
    public class WindowOptions
    {
        public bool? IsBlocking { get; set; }
        public ITransition ShowTransition { get; set; }
        public ITransition HideTransition { get; set; }
        public float? TransitionDuration { get; set; }
        public IObjectResolver Resolver { get; set; }
    }
}
