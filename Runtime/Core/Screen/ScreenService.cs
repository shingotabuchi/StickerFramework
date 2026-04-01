using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace StickerFwk.Core
{
    public sealed class ScreenService : ITickable
    {
        readonly IPublisher<ScreenChangedEvent> _publisher;

        Vector2Int _lastScreenSize;
        Rect _lastSafeArea;
        ScreenOrientation _lastOrientation;

        public ScreenService(IPublisher<ScreenChangedEvent> publisher)
        {
            _publisher = publisher;
        }

        public void Tick()
        {
            if (_lastScreenSize.x == Screen.width
                && _lastScreenSize.y == Screen.height
                && _lastSafeArea == Screen.safeArea
                && _lastOrientation == Screen.orientation)
            {
                return;
            }

            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastSafeArea = Screen.safeArea;
            _lastOrientation = Screen.orientation;

            _publisher.Publish(new ScreenChangedEvent());
        }
    }
}
