using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    /// Loop clip asset for Timeline.
    /// When timeline playback reaches the end of the clip, the timeline rewinds to the beginning.
    ///
    /// The clip determines whether to continue looping by calling the condition method on the
    /// bound <see cref="LoopTrackComponent"/>.
    /// </summary>
    [Serializable]
    public class LoopClipAsset : PlayableAsset
    {
        [SerializeField] private string _tag;

#if UNITY_EDITOR
        [SerializeField] private PreviewMode _previewMode = PreviewMode.NotLoop;
#endif

        public double StartTime { get; private set; }
        public double EndTime { get; private set; }
        public TrackAsset Track { get; private set; }

        public void Initialize(TrackAsset trackAsset, double startTime, double endTime)
        {
            Track = trackAsset;
            StartTime = startTime;
            EndTime = endTime;
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<LoopClipBehaviour>.Create(graph);

            var director = owner.GetComponent<PlayableDirector>();
            var target = director.GetGenericBinding(Track) as LoopTrackComponent;

            var behaviour = playable.GetBehaviour();
            behaviour.Target = target;
            behaviour.Tag = _tag;
            behaviour.StartTime = StartTime;
            behaviour.EndTime = EndTime;
#if UNITY_EDITOR
            behaviour.EnablePreview = EnablePreview;
            behaviour.PreviewMode = _previewMode;
#endif

            return playable;
        }

#if UNITY_EDITOR
        public enum PreviewMode
        {
            NotLoop,
            Loop
        }

        private const string EnablePreviewInEditModeKey = "Sticker.LoopClipAsset.EnablePreviewInEditMode";

        private static bool _enablePreviewInPlayMode;
        private static bool? _cachedEnablePreviewInEditMode;

        public static bool EnablePreviewInEditMode
        {
            get
            {
                if (_cachedEnablePreviewInEditMode == null)
                {
                    _cachedEnablePreviewInEditMode =
                        UnityEditor.EditorUserSettings.GetConfigValue(EnablePreviewInEditModeKey) != "false";
                }

                return _cachedEnablePreviewInEditMode.Value;
            }
            set
            {
                _cachedEnablePreviewInEditMode = value;
                UnityEditor.EditorUserSettings.SetConfigValue(EnablePreviewInEditModeKey, value ? "true" : "false");
            }
        }

        public static bool EnablePreviewInPlayMode
        {
            get => _enablePreviewInPlayMode;
            set => _enablePreviewInPlayMode = value;
        }

        public static bool EnablePreview
        {
            get
            {
                if (UnityEditor.EditorApplication.isPlaying)
                {
                    return EnablePreviewInPlayMode;
                }

                return EnablePreviewInEditMode;
            }
        }
#endif
    }
}

