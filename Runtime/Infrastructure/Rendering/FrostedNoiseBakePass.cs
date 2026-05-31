using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StickerFwk.Infrastructure.Rendering
{
    // Renders the frosted displacement direction field into a persistent RG
    // target. Only enqueued when the configuration (type/scale/seed/resolution)
    // changes, so the per-frame composite is reduced to a single texture fetch.
    public sealed class FrostedNoiseBakePass : ScriptableRenderPass
    {
        private static readonly int FrostedNoiseTypeId = Shader.PropertyToID("_FrostedNoiseType");
        private static readonly int FrostedNoiseScaleId = Shader.PropertyToID("_FrostedNoiseScale");
        private static readonly int FrostedNoiseSeedId = Shader.PropertyToID("_FrostedNoiseSeed");
        private static readonly int FrostedAspectId = Shader.PropertyToID("_FrostedAspect");

        private readonly Material _material;
        private readonly int _passIndex;

        private RTHandle _target;
        private FrostedBlurNoiseType _noiseType;
        private float _noiseScale;
        private float _noiseSeed;
        private Vector2 _aspect;

        public FrostedNoiseBakePass(Material material, int passIndex)
        {
            _material = material;
            _passIndex = passIndex;
        }

        public void Setup(RTHandle target, FrostedBlurNoiseType noiseType, float noiseScale, float noiseSeed, Vector2 aspect)
        {
            _target = target;
            _noiseType = noiseType;
            _noiseScale = noiseScale;
            _noiseSeed = noiseSeed;
            _aspect = aspect;
        }

        private sealed class BakePassData
        {
            public Material material;
            public int passIndex;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _target == null)
            {
                return;
            }

            _material.SetInt(FrostedNoiseTypeId, (int)_noiseType);
            _material.SetFloat(FrostedNoiseScaleId, _noiseScale);
            _material.SetFloat(FrostedNoiseSeedId, _noiseSeed);
            _material.SetVector(FrostedAspectId, new Vector4(_aspect.x, _aspect.y, 0f, 0f));

            var target = renderGraph.ImportTexture(_target);

            using (var builder = renderGraph.AddRasterRenderPass<BakePassData>("FrostedNoiseBake", out var passData))
            {
                passData.material = _material;
                passData.passIndex = _passIndex;

                builder.SetRenderAttachment(target, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (BakePassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                });
            }
        }
    }
}
