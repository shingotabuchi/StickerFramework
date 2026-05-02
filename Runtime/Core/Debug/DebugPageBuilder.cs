#if STICKER_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class DebugPageBuilder : IDebugPageBuilder
    {
        public readonly List<DebugWidget> Widgets = new List<DebugWidget>(16);

        public IDebugPageBuilder Label(string text)
        {
            Widgets.Add(new LabelWidget { Text = text });
            return this;
        }

        public IDebugPageBuilder Label(Func<string> text)
        {
            Widgets.Add(new LabelWidget { DynamicText = text });
            return this;
        }

        public IDebugPageBuilder Button(string text, Action onClick)
        {
            Widgets.Add(new ButtonWidget { Text = text, OnClick = onClick });
            return this;
        }

        public IDebugPageBuilder Toggle(string text, Func<bool> get, Action<bool> set)
        {
            Widgets.Add(new ToggleWidget { Text = text, Get = get, Set = set });
            return this;
        }

        public IDebugPageBuilder Slider(string text, Func<float> get, Action<float> set, float min, float max)
        {
            Widgets.Add(new SliderWidget { Text = text, Get = get, Set = set, Min = min, Max = max });
            return this;
        }

        public IDebugPageBuilder IntSlider(string text, Func<int> get, Action<int> set, int min, int max)
        {
            Widgets.Add(new IntSliderWidget { Text = text, Get = get, Set = set, Min = min, Max = max });
            return this;
        }

        public IDebugPageBuilder TextField(string text, Func<string> get, Action<string> set)
        {
            Widgets.Add(new TextFieldWidget { Text = text, Get = get, Set = set });
            return this;
        }

        public IDebugPageBuilder EnumDropdown<TEnum>(string text, Func<TEnum> get, Action<TEnum> set) where TEnum : struct, Enum
        {
            var names = Enum.GetNames(typeof(TEnum));
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            int IndexOf(TEnum value)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (EqualityComparer<TEnum>.Default.Equals(values[i], value))
                    {
                        return i;
                    }
                }
                return 0;
            }
            Widgets.Add(new EnumDropdownWidget
            {
                Text = text,
                Names = names,
                GetIndex = () => IndexOf(get()),
                SetIndex = i => set(values[Mathf.Clamp(i, 0, values.Length - 1)])
            });
            return this;
        }

        public IDebugPageBuilder PageLink(string text, IDebugPage target)
        {
            Widgets.Add(new PageLinkWidget { Text = text, Target = target });
            return this;
        }

        public IDebugPageBuilder PageLink(string text, Func<IDebugPage> targetFactory)
        {
            Widgets.Add(new PageLinkWidget { Text = text, Factory = targetFactory });
            return this;
        }

        public IDebugPageBuilder Separator()
        {
            Widgets.Add(new SeparatorWidget());
            return this;
        }

        internal IDebugPageBuilder Custom(Action<DebugMenuRenderContext> render)
        {
            Widgets.Add(new CustomWidget { Render_ = render });
            return this;
        }
    }

}
#endif
