using System;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    public class WindowOptions
    {
        public bool? IsBlocking { get; set; }
        public bool LockInputDuringPush { get; set; }
        public ITransition ShowTransition { get; set; }
        public ITransition HideTransition { get; set; }
        public float? TransitionDuration { get; set; }

        // DI-agnostic injection hook. If supplied, UIService invokes this on the
        // instantiated window GameObject instead of using its own resolver. Callers
        // wire this to whatever DI container is in use (e.g. for VContainer:
        // `Inject = scope.Container.InjectGameObject`).
        public Action<GameObject> Inject { get; set; }
    }
}
