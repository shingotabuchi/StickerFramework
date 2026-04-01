using UnityEngine;
using UnityEngine.Playables;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    ///     Playable behaviour that handles loop logic for a single loop clip.
    ///     Rewinds the <see cref="PlayableDirector" /> to the clip's start time
    ///     when the clip ends and the loop condition is satisfied.
    ///     Supports all three <see cref="DirectorWrapMode" /> values: None, Hold, and Loop.
    /// </summary>
    public class LoopClipBehaviour : PlayableBehaviour
    {
        private PlayableDirector _director;
        private bool _looped;
        private bool _played;
        public double EndTime;
        public double StartTime;
        public string Tag;
        public LoopTrackComponent Target;

        public override void OnGraphStart(Playable playable)
        {
            _director = playable.GetGraph().GetResolver() as PlayableDirector;
            _played = false;
            _looped = false;
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            HandleLoopPlayback(playable);
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            if (!_played)
            {
                return;
            }

            // When the director is in Hold or None mode and the clip ends at the timeline's
            // duration boundary, OnBehaviourPause may not fire reliably.
            // Handle the looping process here instead.
            if (_director.extrapolationMode != DirectorWrapMode.Loop
                && _director.time >= _director.duration - Mathf.Epsilon)
            {
                _played = false;

                if (TryLoopFromPrepareFrame(playable))
                {
                    HandleLoopPlayback(playable);
                }
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (!_played)
            {
                return;
            }

            _played = false;

            // When wrap mode is None, the graph stops before OnBehaviourPause fires,
            // so IsPlaying() returns false. Use effectivePlayState to detect that this
            // pause was triggered by the clip ending during normal playback
            // (as opposed to scrubbing or an external stop).
            var wasPlayingNormally = playable.GetGraph().IsPlaying()
                                     || info.effectivePlayState == PlayState.Paused;

            if (!wasPlayingNormally)
            {
                return;
            }

            if (!Target)
            {
                return;
            }

            if (!IsNaturalEnd(info))
            {
                return;
            }

            if (ShouldLoop())
            {
                _director.time = StartTime;
                _looped = true;

                // When wrap mode is None, the director has already stopped itself.
                // Re-play so the timeline continues from the rewound position.
                if (_director.state != PlayState.Playing)
                {
                    _director.Play();
                }

                HandleLoopPlayback(playable);
            }
            else
            {
                _looped = false;
                Target.OnFinishLoop(Tag);
            }
        }

        private void HandleLoopPlayback(Playable playable)
        {
            _played = true;

            // Avoid firing callbacks when scrubbing the time manipulator in preview.
            // After a None-mode re-play the graph IS playing, so this check still passes.
            var playing = playable.GetGraph().IsPlaying()
                          || _director.state == PlayState.Playing;

            if (!playing)
            {
                return;
            }

            if (Target && !_looped)
            {
                Target.OnStartLoop(Tag);
            }

            if (Target && _looped)
            {
                Target.OnLoop(Tag);
            }
        }

        /// <summary>
        ///     Loop attempt from <see cref="PrepareFrame" /> — used for Hold/None modes
        ///     when the clip ends exactly at the timeline's duration boundary.
        /// </summary>
        private bool TryLoopFromPrepareFrame(Playable playable)
        {
            if (!playable.GetGraph().IsPlaying() && _director.state != PlayState.Playing)
            {
                return false;
            }

            if (!Target)
            {
                return false;
            }

            if (ShouldLoop())
            {
                _director.time = StartTime;
                _looped = true;

                if (_director.state != PlayState.Playing)
                {
                    _director.Play();
                }

                return true;
            }

            _looped = false;
            Target.OnFinishLoop(Tag);
            return false;
        }

        /// <summary>
        ///     Checks whether the pause was caused by the clip naturally reaching its end,
        ///     rather than by a seek/scrub or external jump.
        /// </summary>
        private bool IsNaturalEnd(FrameData info)
        {
            var maxEndTime = EndTime + info.deltaTime;

            // Adjust when the director has looped around.
            if (_director.extrapolationMode == DirectorWrapMode.Loop
                && maxEndTime > _director.duration && _director.time < EndTime)
            {
                maxEndTime -= _director.duration;
            }
            else if (_director.time < StartTime)
            {
                // Director time is before our clip — this was a seek, not a natural end.
                return false;
            }

            if (_director.time > maxEndTime)
            {
                return false;
            }

            return true;
        }

        private bool ShouldLoop()
        {
#if UNITY_EDITOR
            if (LoopClipAsset.EnablePreview)
            {
                return PreviewMode == LoopClipAsset.PreviewMode.Loop;
            }
#endif
            return Target.ShouldLoop(Tag);
        }

#if UNITY_EDITOR
        public bool EnablePreview;
        public LoopClipAsset.PreviewMode PreviewMode;
#endif
    }
}