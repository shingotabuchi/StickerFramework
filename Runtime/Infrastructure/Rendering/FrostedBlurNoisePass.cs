using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class FrostedBlurNoisePass : ScriptableRenderPass
    {
        private static readonly int FrostedNoiseTypeId = Shader.PropertyToID("_FrostedNoiseType");
        private static readonly int FrostedNoiseStrengthId = Shader.PropertyToID("_FrostedNoiseStrength");
        private static readonly int FrostedNoiseScaleId = Shader.PropertyToID("_FrostedNoiseScale");
        private static readonly int FrostedNoiseSeedId = Shader.PropertyToID("_FrostedNoiseSeed");

        private readonly Material _material;
        private readonly int _passIndex;

        private FrostedBlurNoiseType _noiseType;
        private float _noiseStrength;
        private float _noiseScale;
        private float _noiseSeed;

        public FrostedBlurNoisePass(Material material, int passIndex)
        {
            _material = material;
            _passIndex = passIndex;
        }

        public void Setup(FrostedBlurNoiseType noiseType, float noiseStrength, float noiseScale, float noiseSeed)
        {
            _noiseType = noiseType;
            _noiseStrength = noiseStrength;
            _noiseScale = noiseScale;
            _noiseSeed = noiseSeed;
        }

        private sealed class CopyPassData
        {
            public TextureHandle source;
        }

        private sealed class CompositePassData
        {
            public TextureHandle source;
            public Material material;
            public int passIndex;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _noiseType == FrostedBlurNoiseType.None || _noiseStrength <= 0f)
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraColor = resourceData.activeColorTexture;

            if (!cameraColor.IsValid())
            {
                return;
            }

            _material.SetInt(FrostedNoiseTypeId, (int)_noiseType);
            _material.SetFloat(FrostedNoiseStrengthId, _noiseStrength);
            _material.SetFloat(FrostedNoiseScaleId, _noiseScale);
            _material.SetFloat(FrostedNoiseSeedId, _noiseSeed);

            var desc = renderGraph.GetTextureDesc(cameraColor);
            desc.depthBufferBits = 0;
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = false;
            desc.name = "_FrostedNoiseSource";

            var sourceCopy = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("FrostedNoiseCopy", out var passData))
            {
                passData.source = cameraColor;

                builder.UseTexture(cameraColor);
                builder.SetRenderAttachment(sourceCopy, 0);
                builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0f, false);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("FrostedNoiseComposite", out var passData))
            {
                passData.source = sourceCopy;
                passData.material = _material;
                passData.passIndex = _passIndex;

                builder.UseTexture(sourceCopy);
                builder.SetRenderAttachment(cameraColor, 0);
                builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                });
            }
        }
    }
}
