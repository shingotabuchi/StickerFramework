using VContainer;

namespace StickerFwk.Core.UI
{
    public class WindowOptions
    {
        public bool? IsBlocking { get; set; }
        public TransitionType? ShowTransition { get; set; }
        public TransitionType? HideTransition { get; set; }
        public float? TransitionDuration { get; set; }
        public IObjectResolver Resolver { get; set; }
    }
}
