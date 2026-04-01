namespace StickerFwk.Core
{
    public readonly struct InputLockChangedEvent
    {
        public readonly bool IsLocked;

        public InputLockChangedEvent(bool isLocked)
        {
            IsLocked = isLocked;
        }
    }
}
