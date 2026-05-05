using StickerFwk.Core;
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
            var cameraColor = resourceData.activeColorTexture;
            if (!cameraColor.IsValid())
            {
                Log.Error(nameof(CachedBlurBlitPass), "activeColorTexture is invalid; skipping cached blur blit.");
                return;
            }

            var importedCache = renderGraph.ImportTexture(_cachedBlur);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CachedBlurBlit", out var passData))
            {
                passData.source = importedCache;
                builder.UseTexture(importedCache);
                builder.SetRenderAttachment(cameraColor, 0);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new UnityEngine.Vector4(1, 1, 0, 0), 0f, false);
                });
            }
        }

        private class PassData
        {
            public TextureHandle source;
        }
    }
}
