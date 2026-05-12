using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace StickerFwk.Core.Editor.UI
{
    public static class EditorUiMenuHelper
    {
        public static void PlaceUiElementRoot(GameObject element, MenuCommand command)
        {
            Type menuOptionsType = Type.GetType("UnityEditor.UI.MenuOptions, UnityEditor.UI");
            MethodInfo placeUiElementRootMethod = menuOptionsType?.GetMethod(
                "PlaceUIElementRoot",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (placeUiElementRootMethod == null)
            {
                throw new InvalidOperationException("Could not find UnityEditor.UI.MenuOptions.PlaceUIElementRoot.");
            }

            placeUiElementRootMethod.Invoke(null, new object[] { element, command });
        }
    }
}
