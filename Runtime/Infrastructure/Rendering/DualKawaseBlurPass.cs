using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    public sealed class DualKawaseBlurPass : ScriptableRenderPass
    {
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly string[] DownTextureNames =
        {
            "_BlurDown0",
            "_BlurDown1",
            "_BlurDown2",
            "_BlurDown3",
            "_BlurDown4",
            "_BlurDown5",
            "_BlurDown6",
            "_BlurDown7"
        };

        private static readonly string[] UpTextureNames =
        {
            "_BlurUp0",
            "_BlurUp1",
            "_BlurUp2",
            "_BlurUp3",
            "_BlurUp4",
            "_BlurUp5",
            "_BlurUp6",
            "_BlurUp7"
        };

        private static readonly string[] DownPassNames =
        {
            "KawaseBlurDown0",
            "KawaseBlurDown1",
            "KawaseBlurDown2",
            "KawaseBlurDown3",
            "KawaseBlurDown4",
            "KawaseBlurDown5",
            "KawaseBlurDown6",
            "KawaseBlurDown7"
        };

        private static readonly string[] UpPassNames =
        {
            "KawaseBlurUp0",
            "KawaseBlurUp1",
            "KawaseBlurUp2",
            "KawaseBlurUp3",
            "KawaseBlurUp4",
            "KawaseBlurUp5",
            "KawaseBlurUp6",
            "KawaseBlurUp7"
        };

        private readonly Material _material;
        private readonly int _maxIterations;
        private readonly TextureHandle[] _downTextures;

        private int _iterations;
        private float _offset;
        private int _downsample;
        private RTHandle _cacheTarget;

        public DualKawaseBlurPass(Material material, int maxIterations)
        {
            _material = material;
            _maxIterations = maxIterations;
            _downTextures = new TextureHandle[maxIterations];
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

            for (var i = 0; i < _iterations; i++)
            {
                var w = Mathf.Max(1, width >> i);
                var h = Mathf.Max(1, height >> i);
                var desc = baseDesc;
                desc.width = w;
                desc.height = h;
                desc.name = DownTextureNames[i];
                _downTextures[i] = renderGraph.CreateTexture(desc);
            }

            var lastDown = cameraColor;
            for (var i = 0; i < _iterations; i++)
            {
                AddBlitPass(renderGraph, lastDown, _downTextures[i], _material, 0, DownPassNames[i]);
                lastDown = _downTextures[i];
            }

            var lastUp = lastDown;
            for (var i = _iterations - 2; i >= 0; i--)
            {
                var w = Mathf.Max(1, width >> i);
                var h = Mathf.Max(1, height >> i);
                var desc = baseDesc;
                desc.width = w;
                desc.height = h;
                desc.name = UpTextureNames[i];
                var upTexture = renderGraph.CreateTexture(desc);

                AddBlitPass(renderGraph, lastUp, upTexture, _material, 1, UpPassNames[i]);
                lastUp = upTexture;
            }

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

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source,
                        new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                });
            }
        }

    }
}
