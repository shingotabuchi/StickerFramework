namespace StickerFwk.Core
{
    public readonly struct CameraProfileAppliedEvent
    {
        public readonly CameraProfileId ProfileId;
        public readonly bool IsApplied;

        public CameraProfileAppliedEvent(CameraProfileId profileId, bool isApplied)
        {
            ProfileId = profileId;
            IsApplied = isApplied;
        }
    }
}
