#if STICKER_DEBUG
using System;
using System.Globalization;
using UnityEngine;

namespace StickerFwk.Core.Debug
{
    internal sealed class SliderWidget : DebugWidget
    {
        public string Text;
        public Func<float> Get;
        public Action<float> Set;
        public float Min;
        public float Max;
        private string _editText;

        public override void Render(DebugMenuRenderContext ctx)
        {
            var stored = Get();
            var current = stored;
            var controlName = $"debug-slider-value-{Text}";
            var hasFocus = GUI.GetNameOfFocusedControl() == controlName;
            if (!hasFocus)
            {
                _editText = current.ToString("0.###", CultureInfo.InvariantCulture);
            }

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            var labelText = $"{Text}:";
            var labelWidth = Mathf.Max(ctx.Styles.LabelWidth, ctx.Styles.SliderLabel.CalcSize(new GUIContent(labelText)).x + 8f);
            OutlinedLabel(labelText, ctx.Styles.SliderLabel, ctx, GUILayout.Width(labelWidth));
            GUILayout.FlexibleSpace();
            GUI.SetNextControlName(controlName);
            var valueText = GUILayout.TextField(
                _editText ?? string.Empty,
                ctx.Styles.TextField,
                GUILayout.Width(Mathf.Max(400f, ctx.Styles.LabelWidth * 0.32f)));
            if (!string.Equals(valueText, _editText, StringComparison.Ordinal))
            {
                _editText = valueText;
            }

            if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var typedValue))
            {
                current = Mathf.Clamp(typedValue, Min, Max);
            }

            GUILayout.EndHorizontal();
            var sliderRect = GUILayoutUtility.GetRect(0f, ctx.Styles.SliderHeightValue, GUILayout.ExpandWidth(true), ctx.Styles.SliderHeight);
            var next = DrawCenteredSlider(sliderRect, current, Min, Max, ctx);
            GUILayout.EndVertical();
            GUILayout.Space(SliderBottomMargin);
            if (!Mathf.Approximately(next, stored))
            {
                Set(next);
            }
        }
    }
}
#endif
