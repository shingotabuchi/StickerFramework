using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    /// Time-scale clip asset for Timeline. While this clip is active, the whole timeline plays at
    /// the configured speed multiplier (1 = normal). Multiple clips may sit on the same track and
    /// blend; regions with no clip play at normal speed.
    /// </summary>
    [Serializable]
    public class TimeScaleClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField, Min(0f)] private float _speed = 1f;

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<TimeScaleClipBehaviour>.Create(graph);
            playable.GetBehaviour().Speed = _speed;
            return playable;
        }
    }
}
