namespace StickerFwk.Core
{
    public readonly struct ActiveBaseChangedEvent
    {
        public readonly CameraId Previous;
        public readonly CameraId Current;

        public ActiveBaseChangedEvent(CameraId previous, CameraId current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
