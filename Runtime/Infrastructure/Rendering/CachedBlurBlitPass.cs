using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class CachedBlurBlitPass : ScriptableRenderPass
    {
        private RTHandle _cachedBlur;

        public void Setup(RTHandle cachedBlur)
        {
            _cachedBlur = cachedBlur;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_cachedBlur == null)
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var importedCache = renderGraph.ImportTexture(_cachedBlur);
            resourceData.cameraColor = importedCache;
        }
    }
}
