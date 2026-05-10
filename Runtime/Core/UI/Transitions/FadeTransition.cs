using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using UnityEngine;

namespace StickerFwk.Core.UI
{
    [Serializable]
    public sealed class FadeTransition : ITransition
    {
        public async UniTask Play(WindowView view, bool isShow, float duration, CancellationToken ct)
        {
            var canvasGroup = view.CanvasGroup;
            var startAlpha = isShow ? 0f : 1f;
            var endAlpha = isShow ? 1f : 0f;
            canvasGroup.alpha = startAlpha;
            UnityEngine.Debug.Log($"[FadeDbg] " + $"Fade.start view='{view.name}' isShow={isShow} duration={duration} startAlpha={startAlpha} endAlpha={endAlpha} canvasEnabled={(canvasGroup.GetComponentInParent<Canvas>()?.enabled.ToString() ?? "null")} ctCancelled={ct.IsCancellationRequested}");

            if (duration <= 0f)
            {
                canvasGroup.alpha = endAlpha;
                UnityEngine.Debug.Log($"[FadeDbg] " + $"Fade.short-circuit (duration<=0) view='{view.name}' isShow={isShow}");
                return;
            }

            var elapsed = 0f;
            // Yield once before starting the timer so a giant first-frame deltaTime
            // (e.g. right after SceneManager.LoadSceneAsync completes) doesn't consume
            // the entire transition in a single tick.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            // Cap per-frame progress so a single hitched frame (scene-load Awake/OnEnable
            // bursts can easily spike to 200-300ms) can't consume the entire fade in one
            // step and snap the alpha to near-end. This means the fade may take longer
            // in real time on a hitched frame, but it stays visible.
            const float MaxStepSeconds = 1f / 30f;
            var iterations = 0;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - t) * (1f - t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                iterations++;
                if (!isShow)
                {
                    var canvas = canvasGroup.GetComponentInParent<Canvas>();
                    UnityEngine.Debug.Log($"[FadeDbg] Fade.tick view='{view.name}' iter={iterations} elapsed={elapsed:F3} alpha={canvasGroup.alpha:F3} cgEnabled={canvasGroup.enabled} cgInteractable={canvasGroup.interactable} goActive={view.gameObject.activeInHierarchy} canvasEnabled={canvas?.enabled} canvasSortOrder={canvas?.sortingOrder} worldCam='{canvas?.worldCamera?.name ?? "null"}'");
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            canvasGroup.alpha = endAlpha;
            UnityEngine.Debug.Log($"[FadeDbg] " + $"Fade.complete view='{view.name}' isShow={isShow} iterations={iterations} elapsed={elapsed}");
        }
    }
}
