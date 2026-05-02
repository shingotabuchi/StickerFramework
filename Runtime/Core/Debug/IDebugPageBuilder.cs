#if STICKER_DEBUG
using System;

namespace StickerFwk.Core.Debug
{
    /// <summary>
    /// Fluent builder used by <see cref="IDebugPage.Build"/> to declare the widget list for a page.
    /// Widgets are rendered top-to-bottom in the order they are added.
    /// </summary>
    /// <remarks>
    /// All stateful widgets bind via <c>get</c>/<c>set</c> delegates so pages keep ownership of their
    /// state. Getters are polled each frame; setters are invoked only when the user changes the value.
    /// </remarks>
    public interface IDebugPageBuilder
    {
        /// <summary>Static text label.</summary>
        IDebugPageBuilder Label(string text);

        /// <summary>Dynamic label whose text is recomputed every frame via <paramref name="text"/>.</summary>
        IDebugPageBuilder Label(Func<string> text);

        /// <summary>Tappable button. <paramref name="onClick"/> fires once per click.</summary>
        IDebugPageBuilder Button(string text, Action onClick);

        /// <summary>Boolean toggle bound to <paramref name="get"/>/<paramref name="set"/>.</summary>
        IDebugPageBuilder Toggle(string text, Func<bool> get, Action<bool> set);

        /// <summary>Float slider clamped to <paramref name="min"/>..<paramref name="max"/>.</summary>
        IDebugPageBuilder Slider(string text, Func<float> get, Action<float> set, float min, float max);

        /// <summary>Integer slider clamped to <paramref name="min"/>..<paramref name="max"/>.</summary>
        IDebugPageBuilder IntSlider(string text, Func<int> get, Action<int> set, int min, int max);

        /// <summary>Single-line text input.</summary>
        IDebugPageBuilder TextField(string text, Func<string> get, Action<string> set);

        /// <summary>Selection grid over the values of <typeparamref name="TEnum"/>.</summary>
        IDebugPageBuilder EnumDropdown<TEnum>(string text, Func<TEnum> get, Action<TEnum> set) where TEnum : struct, Enum;

        /// <summary>Pushes <paramref name="target"/> onto the navigation stack when tapped.</summary>
        IDebugPageBuilder PageLink(string text, IDebugPage target);

        /// <summary>Lazily-resolved variant of <see cref="PageLink(string, IDebugPage)"/> for sub-pages built on demand.</summary>
        IDebugPageBuilder PageLink(string text, Func<IDebugPage> targetFactory);

        /// <summary>Thin horizontal divider for visual grouping.</summary>
        IDebugPageBuilder Separator();
    }
}
#endif
