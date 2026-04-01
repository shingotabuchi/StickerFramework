using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Infrastructure.Input
{
    public class WorldRaycastService : IWorldRaycastService
    {
        private readonly IInputLockService _inputLockService;

        public WorldRaycastService(IInputLockService inputLockService)
        {
            _inputLockService = inputLockService;
        }

        public bool TryRaycast(Ray ray, out RaycastHit hit, float maxDistance = Mathf.Infinity,
            int layerMask = Physics.DefaultRaycastLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (_inputLockService.IsLocked)
            {
                hit = default;
                return false;
            }

            return Physics.Raycast(ray, out hit, maxDistance, layerMask, queryTriggerInteraction);
        }
    }
}