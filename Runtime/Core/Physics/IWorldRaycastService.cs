using UnityEngine;

namespace StickerFwk.Core
{
    public interface IWorldRaycastService
    {
        bool TryRaycast(Ray ray, out RaycastHit hit, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal);
    }
}
