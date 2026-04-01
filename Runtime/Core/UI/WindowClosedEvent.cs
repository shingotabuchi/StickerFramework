namespace StickerFwk.Core.UI
{
    public readonly struct WindowClosedEvent
    {
        public readonly string Key;
        public readonly UILayer Layer;

        public WindowClosedEvent(string key, UILayer layer)
        {
            Key = key;
            Layer = layer;
        }
    }
}
