using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.Initialization;
using UnityEngine;
using VContainer;

namespace StickerFwk.Infrastructure.Initialization
{
    /// <summary>
    /// Bootstrap-phase task that sets <see cref="Application.targetFrameRate"/>. Register via
    /// <see cref="UseTargetFrameRate"/>.
    /// </summary>
    internal sealed class TargetFrameRateInitTask : IInitTask
    {
        private readonly int _targetFrameRate;

        public InitPhase Phase => InitPhase.Bootstrap;

        public TargetFrameRateInitTask(int targetFrameRate)
        {
            _targetFrameRate = targetFrameRate;
        }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            Application.targetFrameRate = _targetFrameRate;
            return UniTask.CompletedTask;
        }
    }

    public static class TargetFrameRateInitExtensions
    {
        /// <summary>
        /// Sets <see cref="Application.targetFrameRate"/> during the Bootstrap phase of root init.
        /// Pass <c>-1</c> to leave Unity's platform default in place (no reason to register in that case).
        /// </summary>
        public static void UseTargetFrameRate(this IContainerBuilder builder, int frameRate)
        {
            builder.AddInitTask(new TargetFrameRateInitTask(frameRate));
        }
    }
}
