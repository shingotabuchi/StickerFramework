using System;
using System.Reflection;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;

namespace StickerFwk.Core.Editor
{
    /// <summary>
    /// Material inspector for "StickerFwk/TextMeshPro/Distance Field Two Outlines".
    /// Reproduces the stock TMP SDF inspector panel-for-panel, but draws the
    /// <b>Outline 2</b> panel itself so it can include a dedicated Softness slider
    /// (<c>_Outline2Softness</c>) right alongside Color and Thickness. The stock GUI
    /// only exposes a single shared softness (drawn as "Softness" under the Face
    /// panel) and never draws <c>_Outline2Softness</c> at all.
    ///
    /// Every panel other than Outline 2 is delegated to the stock TMP private panel
    /// methods via reflection, so they remain identical to the built-in inspector and
    /// keep working if their internals change.
    /// </summary>
    public class TMP_SDFTwoOutlinesShaderGUI : TMP_SDFShaderGUI
    {
        private static bool s_Face = true, s_Outline = true, s_Outline2 = true, s_Underlay = true, s_Glow;

        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly MethodInfo s_DoFacePanel = GetPanel("DoFacePanel");
        private static readonly MethodInfo s_DoOutlinePanel = GetPanel("DoOutlinePanel", Type.EmptyTypes);
        private static readonly MethodInfo s_DoUnderlayPanel = GetPanel("DoUnderlayPanel");
        private static readonly MethodInfo s_DrawLightingPanelLegacy = GetPanel("DrawLightingPanelLegacy");
        private static readonly MethodInfo s_DoGlowPanel = GetPanel("DoGlowPanel");
        private static readonly MethodInfo s_DoDebugPanel = GetPanel("DoDebugPanel");

        private static readonly FieldInfo s_OutlineFeatureField =
            typeof(TMP_SDFShaderGUI).GetField("s_OutlineFeature", StaticFlags);
        private static readonly FieldInfo s_UnderlayFeatureField =
            typeof(TMP_SDFShaderGUI).GetField("s_UnderlayFeature", StaticFlags);
        private static readonly FieldInfo s_GlowFeatureField =
            typeof(TMP_SDFShaderGUI).GetField("s_GlowFeature", StaticFlags);

        private static MethodInfo GetPanel(string name)
        {
            return typeof(TMP_SDFShaderGUI).GetMethod(name, InstanceFlags);
        }

        private static MethodInfo GetPanel(string name, Type[] argTypes)
        {
            return typeof(TMP_SDFShaderGUI).GetMethod(name, InstanceFlags, null, argTypes, null);
        }

        protected override void DoGUI()
        {
            // SRP / multi-outline material variants aren't produced by this shader,
            // so fall back to the stock inspector rather than reproducing those paths.
            if (m_Material.HasProperty(ShaderUtilities.ID_IsoPerimeter) ||
                s_DoFacePanel == null || s_DoOutlinePanel == null)
            {
                base.DoGUI();
                return;
            }

            var outlineFeature = (ShaderFeature)s_OutlineFeatureField.GetValue(null);
            var underlayFeature = (ShaderFeature)s_UnderlayFeatureField.GetValue(null);
            var glowFeature = (ShaderFeature)s_GlowFeatureField.GetValue(null);

            s_Face = BeginPanel("Face", s_Face);
            if (s_Face)
            {
                Invoke(s_DoFacePanel);
            }
            EndPanel();

            // Outline 1: this shader always has _OutlineTex, so it uses the plain
            // (non-feature) panel header, matching stock TMP_SDFShaderGUI.
            s_Outline = BeginPanel("Outline", s_Outline);
            if (s_Outline)
            {
                Invoke(s_DoOutlinePanel);
            }
            EndPanel();

            if (m_Material.HasProperty(ShaderUtilities.ID_Outline2Color))
            {
                s_Outline2 = BeginPanel("Outline 2", outlineFeature, s_Outline2);
                if (s_Outline2)
                {
                    DoOutline2Panel();
                }
                EndPanel();
            }

            if (m_Material.HasProperty(ShaderUtilities.ID_UnderlayColor) && s_DoUnderlayPanel != null)
            {
                s_Underlay = BeginPanel("Underlay", underlayFeature, s_Underlay);
                if (s_Underlay)
                {
                    Invoke(s_DoUnderlayPanel);
                }
                EndPanel();
            }

            if (m_Material.HasProperty("_SpecularColor") && s_DrawLightingPanelLegacy != null)
            {
                Invoke(s_DrawLightingPanelLegacy);
            }

            if (m_Material.HasProperty(ShaderUtilities.ID_GlowColor) && s_DoGlowPanel != null)
            {
                s_Glow = BeginPanel("Glow", glowFeature, s_Glow);
                if (s_Glow)
                {
                    Invoke(s_DoGlowPanel);
                }
                EndPanel();
            }

            s_DebugExtended = BeginPanel("Debug Settings", s_DebugExtended);
            if (s_DebugExtended && s_DoDebugPanel != null)
            {
                Invoke(s_DoDebugPanel);
            }
            EndPanel();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        // Mirrors stock DoOutline2Panel (Color + Thickness) plus the added Softness slider.
        private void DoOutline2Panel()
        {
            EditorGUI.indentLevel += 1;
            DoColor("_Outline2Color", "Color");
            DoSlider("_Outline2Width", "Thickness");
            if (m_Material.HasProperty("_Outline2Softness"))
            {
                DoSlider("_Outline2Softness", "Softness");
            }
            EditorGUI.indentLevel -= 1;
            EditorGUILayout.Space();
        }

        private void Invoke(MethodInfo panel)
        {
            panel.Invoke(this, null);
        }
    }
}
