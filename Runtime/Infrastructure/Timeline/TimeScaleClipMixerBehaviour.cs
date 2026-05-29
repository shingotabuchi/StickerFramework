using UnityEngine;
using UnityEngine.Playables;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    /// Mixer behaviour for <see cref="TimeScaleTrack"/>. Blends the active clips' speed multipliers
    /// (treating un-covered regions as speed 1) and applies the result to the graph's root playable,
    /// so the multiplier scales the whole timeline. Restores speed to 1 when playback stops.
    /// </summary>
    public class TimeScaleClipMixerBehaviour : PlayableBehaviour
    {
        private bool _speedApplied;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var inputCount = playable.GetInputCount();
            var weightedSpeed = 0f;
            var totalWeight = 0f;

            for (var i = 0; i < inputCount; i++)
            {
                var weight = playable.GetInputWeight(i);
                if (weight <= 0f)
                {
                    continue;
                }

                var input = (ScriptPlayable<TimeScaleClipBehaviour>)playable.GetInput(i);
                weightedSpeed += weight * input.GetBehaviour().Speed;
                totalWeight += weight;
            }

            // Any remaining (un-clipped) weight plays at normal speed.
            var speed = weightedSpeed + (1f - Mathf.Clamp01(totalWeight));
            SetRootSpeed(playable, Mathf.Max(0f, speed));
            _speedApplied = true;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (!_speedApplied)
            {
                return;
            }

            SetRootSpeed(playable, 1d);
            _speedApplied = false;
        }

        private static void SetRootSpeed(Playable playable, double speed)
        {
            var graph = playable.GetGraph();
            if (graph.IsValid() && graph.GetRootPlayableCount() > 0)
            {
                graph.GetRootPlayable(0).SetSpeed(speed);
            }
        }
    }
}
