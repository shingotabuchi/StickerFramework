using System.Collections.Generic;
using UnityEngine;

namespace StickerFwk.Infrastructure.Sound
{
    [DisallowMultipleComponent]
    public sealed class SoundServiceRoot : MonoBehaviour
    {
        [SerializeField] private int _initialSePlayerCount = 1;
        [SerializeField] private int _initialBgmPlayerCount = 2;

        private readonly List<GameObject> _ownedPlayerObjects = new();

        internal int InitialSePlayerCount => Mathf.Max(1, _initialSePlayerCount);
        internal int InitialBgmPlayerCount => Mathf.Max(1, _initialBgmPlayerCount);

        internal SoundPlayer CreatePlayer(SoundType type, int index)
        {
            var playerObject = new GameObject($"SoundPlayer_{type}_{index}");
            playerObject.transform.SetParent(transform, false);
            _ownedPlayerObjects.Add(playerObject);

            var primarySource = CreateSource(playerObject, "PrimarySource");
            var crossfadeSource = CreateSource(playerObject, "CrossfadeSource");

            return new SoundPlayer(primarySource, crossfadeSource);
        }

        internal void ClearPlayers()
        {
            foreach (var playerObject in _ownedPlayerObjects)
            {
                if (playerObject == null) continue;

                if (Application.isPlaying)
                    Destroy(playerObject);
                else
                    DestroyImmediate(playerObject);
            }

            _ownedPlayerObjects.Clear();
        }

        private static AudioSource CreateSource(GameObject parent, string name)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent.transform, false);

            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            return source;
        }
    }
}
