using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    /// Custom Timeline track that hosts <see cref="LoopClipAsset"/> clips.
    /// Bind a <see cref="LoopTrackComponent"/> to control loop conditions via UnityEvents.
    /// </summary>
    [TrackColor(0.7366781f, 0.3261246f, 0.8529412f)]
    [TrackClipType(typeof(LoopClipAsset))]
    [TrackBindingType(typeof(LoopTrackComponent), TrackBindingFlags.None)]
    [DisplayName("Sticker/Loop Track")]
    public class LoopTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixerPlayable = ScriptPlayable<LoopClipMixerBehaviour>.Create(graph, inputCount);

            foreach (var clip in GetClips())
            {
                if (clip.asset is LoopClipAsset loopClipAsset)
                {
                    loopClipAsset.Initialize(this, clip.start, clip.end);
                }
            }

            return mixerPlayable;
        }
    }
}

