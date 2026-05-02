#if STICKER_DEBUG
using System.Collections.Generic;

namespace StickerFwk.Core.Debug
{
    internal sealed class RootDebugPage : IDebugPage
    {
        private readonly IReadOnlyList<IDebugPage> _pages;

        public RootDebugPage(IReadOnlyList<IDebugPage> pages)
        {
            _pages = pages;
        }

        public string Title => "Debug Menu";
        public string Id => "stickerfwk.root";
        public int Order => 0;

        public void Build(IDebugPageBuilder builder)
        {
            var sorted = new List<IDebugPage>(_pages);
            sorted.Sort((a, b) =>
            {
                var c = a.Order.CompareTo(b.Order);
                return c != 0 ? c : string.CompareOrdinal(a.Title, b.Title);
            });

            if (sorted.Count == 0)
            {
                builder.Label("(no debug pages registered)");
                return;
            }

            for (var i = 0; i < sorted.Count; i++)
            {
                var page = sorted[i];
                builder.PageLink(page.Title, page);
            }
        }
    }
}
#endif
