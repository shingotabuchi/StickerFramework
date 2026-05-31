using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    /// Custom Timeline track that hosts <see cref="TimeScaleClipAsset"/> clips to apply a speed
    /// multiplier to the entire timeline (not just one clip). The mixer drives the graph's root
    /// playable speed, so every track is scaled together.
    /// </summary>
    [TrackColor(0.262745f, 0.843137f, 0.788235f)]
    [TrackClipType(typeof(TimeScaleClipAsset))]
    [DisplayName("Sticker/Time Scale Track")]
    public class TimeScaleTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<TimeScaleClipMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
