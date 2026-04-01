using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace StickerFwk.Infrastructure.Timeline
{
    /// <summary>
    ///     Component bound to a <see cref="LoopTrack" />.
    ///     Exposes UnityEvents for determining loop conditions and receiving loop lifecycle callbacks.
    /// </summary>
    [DisallowMultipleComponent]
    [Serializable]
    public class LoopTrackComponent : MonoBehaviour
    {
        [SerializeField] private UnityEvent<LoopResult, string> _shouldLoopEvent;
        [SerializeField] private UnityEvent<string> _startLoopEvent;
        [SerializeField] private UnityEvent<string> _finishLoopEvent;
        [SerializeField] private UnityEvent<string> _loopEvent;

        private readonly LoopResult _loopResult = new();

        public UnityEvent<LoopResult, string> ShouldLoopEvent => _shouldLoopEvent;
        public UnityEvent<string> StartLoopEvent => _startLoopEvent;
        public UnityEvent<string> FinishLoopEvent => _finishLoopEvent;
        public UnityEvent<string> LoopEvent => _loopEvent;

        /// <summary>
        ///     Evaluates whether the loop should continue by invoking <see cref="ShouldLoopEvent" />.
        ///     Returns true if the last registered handler votes to continue (or if no handlers exist).
        /// </summary>
        public bool ShouldLoop(string clipTag)
        {
            return EvaluateShouldLoop(clipTag);
        }

        public void OnStartLoop(string clipTag)
        {
            _startLoopEvent?.Invoke(clipTag);
        }

        public void OnFinishLoop(string clipTag)
        {
            _finishLoopEvent?.Invoke(clipTag);
        }

        public void OnLoop(string clipTag)
        {
            _loopEvent?.Invoke(clipTag);
        }

        private bool EvaluateShouldLoop(string clipTag)
        {
            if (_shouldLoopEvent == null || _loopResult == null)
            {
                return true;
            }

            if (_shouldLoopEvent.GetPersistentEventCount() == 0)
            {
                return true;
            }

            _loopResult.Clear();
            _shouldLoopEvent.Invoke(_loopResult, clipTag);

            // Default to true (keep looping) when no handler added a result.
            var shouldLoop = true;
            foreach (var value in _loopResult)
            {
                shouldLoop = value;
            }

            return shouldLoop;
        }

        /// <summary>
        ///     Collects boolean results from all loop condition handlers.
        ///     The last value added wins (allows multiple handlers to vote).
        /// </summary>
        public class LoopResult : IEnumerable<bool>
        {
            private readonly List<bool> _values = new();

            public IEnumerator<bool> GetEnumerator()
            {
                foreach (var value in _values)
                {
                    yield return value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Clear()
            {
                _values.Clear();
            }

            public void Add(bool value)
            {
                _values.Add(value);
            }
        }
    }
}