using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.UI
{
    public interface IUIService
    {
        UniTask<T> Push<T>(string tag = null, WindowOptions options = null, CancellationToken ct = default) where T : WindowView;
        UniTask Pop(UILayer layer = UILayer.Window, CancellationToken ct = default);
        UniTask Pop<T>(CancellationToken ct = default) where T : WindowView;
        UniTask<T> Replace<T>(UILayer layer, string tag = null, WindowOptions options = null, CancellationToken ct = default) where T : WindowView;
        UniTask PopAll(UILayer layer, CancellationToken ct = default);
        UniTask Preload<T>(string tag = null, CancellationToken ct = default) where T : WindowView;
        bool IsOpen<T>() where T : WindowView;
        T GetWindow<T>() where T : WindowView;
        int GetStackCount(UILayer layer);
    }
}
