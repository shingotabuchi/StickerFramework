using UnityEngine;

namespace StickerFwk.Core.UI
{
    /// <summary>
    /// Companion MonoBehaviour for <see cref="AnimatorTransition"/>. Holds the
    /// per-window <see cref="UnityEngine.Animator"/> reference when it lives on
    /// a child GameObject. References stored on a MonoBehaviour are remapped to
    /// the prefab instance on Instantiate, unlike fields inside [SerializeReference]
    /// graphs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatorTransitionTargets : MonoBehaviour
    {
        [SerializeField] Animator _animator;

        public Animator Animator => _animator;
    }
}
