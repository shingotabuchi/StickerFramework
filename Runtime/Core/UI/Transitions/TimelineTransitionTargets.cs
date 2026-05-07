using UnityEngine;
using UnityEngine.Playables;

namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Companion MonoBehaviour for <see cref="TimelineTransition"/> that holds
    /// the per-window <see cref="PlayableDirector"/> references.
    /// Required because Unity does not remap UnityEngine.Object references stored
    /// inside [SerializeReference] graphs when a prefab is instantiated, so directors
    /// must live on a regular MonoBehaviour to participate in instance remapping.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimelineTransitionTargets : MonoBehaviour
    {
        [SerializeField] PlayableDirector _showDirector;
        [SerializeField] PlayableDirector _hideDirector;

        public PlayableDirector ShowDirector => _showDirector;
        public PlayableDirector HideDirector => _hideDirector;
    }
}
