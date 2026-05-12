using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StickerFwk.Core.Editor.UI
{
    public static class AutoResizingTextContextMenu
    {
        private const string MenuPath = "GameObject/UI (Canvas)/Auto Resizing Text";
        private const string CreateUndoName = "Create Auto Resizing Text";

        [MenuItem(MenuPath, false, 11)]
        private static void CreateAutoResizingText(MenuCommand command)
        {
            GameObject textObject = new(
                "AutoResizingText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(ContentSizeFitter));

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160f, 36f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = "New Text";
            text.fontSize = 36f;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            ContentSizeFitter contentSizeFitter = textObject.GetComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            EditorUiMenuHelper.PlaceUiElementRoot(textObject, command);
            Undo.SetCurrentGroupName(CreateUndoName);
            Selection.activeGameObject = textObject;
        }
    }
}
