using UnityEngine;

namespace StickerFwk.Core.Initialization
{
    /// <summary>
    /// ScriptableObject configuring the root initialization sequence (currently the startup
    /// <see cref="Application.targetFrameRate"/>). Create one via
    /// <c>Assets &gt; Create &gt; Sticker &gt; Framework &gt; Root Init Settings</c>, then register
    /// it on your root <c>LifetimeScope</c>.
    /// </summary>
    /// <remarks>
    /// If no asset is registered, <c>RootInitService</c> falls back to <see cref="Default"/>, an
    /// in-memory instance that leaves <see cref="Application.targetFrameRate"/> at the platform
    /// default (-1).
    /// </remarks>
    [CreateAssetMenu(menuName = "Sticker/Framework/Root Init Settings", fileName = "RootInitSettings")]
    public sealed class RootInitSettings : ScriptableObject
    {
        [Tooltip("Value applied to Application.targetFrameRate at startup. -1 leaves Unity's platform default in place.")]
        [SerializeField] private int _targetFrameRate = -1;

        /// <summary>
        /// Value applied to <see cref="Application.targetFrameRate"/> during root initialization.
        /// Use <c>-1</c> to keep Unity's platform default.
        /// </summary>
        public int TargetFrameRate => _targetFrameRate;

        private static RootInitSettings _default;

        /// <summary>In-memory instance with built-in defaults; used when no asset is registered.</summary>
        public static RootInitSettings Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<RootInitSettings>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    _default.name = "RootInitSettings (Default)";
                    _default._targetFrameRate = 60;
                }
                return _default;
            }
        }
    }
}
