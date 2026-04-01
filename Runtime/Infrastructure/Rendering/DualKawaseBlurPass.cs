using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class DualKawaseBlurPass : ScriptableRenderPass
    {
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");

        private readonly Material _material;
        private readonly int _maxIterations;

        private int _iterations;
        private float _offset;
        private int _downsample;
        private RTHandle _cacheTarget;

        public DualKawaseBlurPass(Material material, int maxIterations)
        {
            _material = material;
            _maxIterations = maxIterations;
        }

        public void Setup(float intensity, int iterations, float offset, int downsample, RTHandle cacheTarget = null)
        {
            _iterations = Mathf.Min(iterations, _maxIterations);
            _offset = offset * intensity;
            _downsample = downsample;
            _cacheTarget = cacheTarget;
        }

        private class PassData
        {
            public TextureHandle source;
            public Material material;
            public int passIndex;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _iterations <= 0)
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraColor = resourceData.activeColorTexture;

            if (!cameraColor.IsValid())
            {
                return;
            }

            _material.SetFloat(OffsetId, _offset);

            var baseDesc = renderGraph.GetTextureDesc(cameraColor);
            baseDesc.depthBufferBits = 0;
            baseDesc.msaaSamples = MSAASamples.None;
            baseDesc.clearBuffer = false;

            var width = Mathf.Max(1, baseDesc.width >> _downsample);
            var height = Mathf.Max(1, baseDesc.height >> _downsample);

            // Create downsample chain
            var downTextures = new TextureHandle[_iterations];
            for (var i = 0; i < _iterations; i++)
            {
                var w = Mathf.Max(1, width >> i);
                var h = Mathf.Max(1, height >> i);
                var desc = baseDesc;
                desc.width = w;
                desc.height = h;
                desc.name = $"_BlurDown{i}";
                downTextures[i] = renderGraph.CreateTexture(desc);
            }

            // Downsample passes
            var lastDown = cameraColor;
            for (var i = 0; i < _iterations; i++)
            {
                AddBlitPass(renderGraph, lastDown, downTextures[i], _material, 0, $"KawaseBlurDown{i}");
                lastDown = downTextures[i];
            }

            // Create upsample chain and run upsample passes
            var lastUp = lastDown;
            for (var i = _iterations - 2; i >= 0; i--)
            {
                var w = Mathf.Max(1, width >> i);
                var h = Mathf.Max(1, height >> i);
                var desc = baseDesc;
                desc.width = w;
                desc.height = h;
                desc.name = $"_BlurUp{i}";
                var upTexture = renderGraph.CreateTexture(desc);

                AddBlitPass(renderGraph, lastUp, upTexture, _material, 1, $"KawaseBlurUp{i}");
                lastUp = upTexture;
            }

            // Final blit back to camera-sized texture
            TextureHandle finalTexture;
            if (_cacheTarget != null)
            {
                finalTexture = renderGraph.ImportTexture(_cacheTarget);
            }
            else
            {
                var finalDesc = baseDesc;
                finalDesc.width = baseDesc.width;
                finalDesc.height = baseDesc.height;
                finalDesc.name = "_BlurFinal";
                finalTexture = renderGraph.CreateTexture(finalDesc);
            }

            AddBlitPass(renderGraph, lastUp, finalTexture, _material, 1, "KawaseBlurFinal");

            resourceData.cameraColor = finalTexture;
        }

        private static void AddBlitPass(RenderGraph renderGraph, TextureHandle source, TextureHandle destination,
            Material material, int passIndex, string passName)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                passData.source = source;
                passData.material = material;
                passData.passIndex = passIndex;

                builder.UseTexture(source);
                builder.SetRenderAttachment(destination, 0);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source,
                        new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                });
            }
        }

    }
}
