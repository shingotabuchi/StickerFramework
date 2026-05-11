using System;
using UnityEngine;

namespace StickerFwk.Infrastructure.Sound
{
    [Serializable]
    public class SoundData : ISoundData
    {
        [SerializeField] private AudioClip[] _clips;
        [SerializeField] private float _volume = 1.0f;

        public string Name => _clips != null && _clips.Length > 0 ? _clips[0].name : "Empty";

        public AudioClip Clip
        {
            get
            {
                if (_clips == null || _clips.Length == 0) return null;
                if (_clips.Length == 1) return _clips[0];

                var randomIndex = UnityEngine.Random.Range(0, _clips.Length);
                return _clips[randomIndex];
            }
        }

        public float Volume => _volume;
        public float PlayedVolume { get; set; } = 1.0f;
    }
}
