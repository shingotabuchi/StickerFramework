using System;
using UnityEngine;

namespace StickerFwk.Core
{
    public interface IWipeCameraService
    {
        /// <summary>
        /// Ensures the wipe camera GameObject exists and returns it WITHOUT enabling the overlay
        /// in the URP stack. Use this to set up the camera transform and any director / view state
        /// before calling <see cref="Acquire"/>, so the very first frame the wipe overlay joins the
        /// URP stack already renders with the correct pose and content (no stale-pose flash).
        /// </summary>
        Camera EnsureCamera();

        IWipeCameraLease Acquire();
    }

    public interface IWipeCameraLease : IDisposable
    {
        Camera Camera { get; }
    }
}
