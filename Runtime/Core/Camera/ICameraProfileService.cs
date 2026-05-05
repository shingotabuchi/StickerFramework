using StickerFwk.Core;

namespace StickerFwk.Core
{
    public interface ICameraProfileService
    {
        bool IsApplied { get; }
        CameraProfileId? ActiveProfileId { get; }
        CameraProfile ActiveProfile { get; }
        void Apply(CameraProfileId profileId);
        void Release();
    }
}
