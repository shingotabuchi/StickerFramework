using System;
using System.Collections.Generic;

namespace StickerFwk.Core
{
    public interface ICameraProfileService
    {
        IDisposable Push(CameraProfileId profileId);
        bool IsActive(CameraProfileId profileId);
        bool TryGetDefinition(CameraId cameraId, out CameraDefinition definition);
        IReadOnlyCollection<CameraProfileId> ActiveProfiles { get; }
    }
}
