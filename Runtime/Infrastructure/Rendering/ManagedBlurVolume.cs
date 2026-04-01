using StickerFwk.Core.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace StickerFwk.Infrastructure.Rendering
{
    [RequireComponent(typeof(Volume))]
    public class ManagedBlurVolume : MonoBehaviour
    {
        IBlurService _blurService;
        Volume _volume;
        bool _isRegistered;

        [Inject]
        public void Construct(IBlurService blurService)
        {
            _blurService = blurService;
            TryRegister();
        }

        void Awake()
        {
            _volume = GetComponent<Volume>();
        }

        void OnEnable()
        {
            TryRegister();
        }

        void OnDisable()
        {
            if (_isRegistered && _blurService != null)
            {
                _blurService.Unregister(_volume);
                _isRegistered = false;
            }
        }

        void TryRegister()
        {
            if (_blurService == null || _volume == null || !isActiveAndEnabled)
            {
                return;
            }

            _blurService.Register(_volume);
            _isRegistered = true;
        }
    }
}
