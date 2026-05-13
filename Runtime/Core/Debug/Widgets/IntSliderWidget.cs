#if STICKER_DEBUG
using System;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class IntSliderWidget : DebugWidget
    {
        public string Text;
        public Func<int> Get;
        public Action<int> Set;
        public int Min;
        public int Max;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var current = Get();
            GUILayout.BeginVertical();
            OutlinedLabel($"{Text}: {current}", ctx.Styles.SliderLabel, ctx, GUILayout.ExpandWidth(true));
            var sliderRect = GUILayoutUtility.GetRect(0f, ctx.Styles.SliderHeightValue, GUILayout.ExpandWidth(true), ctx.Styles.SliderHeight);
            var next = Mathf.RoundToInt(DrawCenteredSlider(sliderRect, current, Min, Max, ctx));
            GUILayout.EndVertical();
            GUILayout.Space(SliderBottomMargin);
            if (next != current)
            {
                Set(next);
            }
        }
    }
}
#endif
