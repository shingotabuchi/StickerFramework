namespace StickerFwk.Core.UI
{
    public readonly struct WindowOpenedEvent
    {
        public readonly string Key;
        public readonly UILayer Layer;

        public WindowOpenedEvent(string key, UILayer layer)
        {
            Key = key;
            Layer = layer;
        }
    }
}
