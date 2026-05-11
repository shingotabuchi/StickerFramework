using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.UI
{
    public readonly struct WindowPushHandle<T> where T : WindowView
    {
        readonly IUIService _service;
        readonly PopState _state;

        public T View { get; }

        internal WindowPushHandle(T view, IUIService service)
        {
            View = view;
            _service = service;
            _state = new PopState();
        }

        public UniTask PopAsync(CancellationToken ct = default)
        {
            return PopCore(immediate: false, ct);
        }

        public UniTask PopImmediateAsync(CancellationToken ct = default)
        {
            return PopCore(immediate: true, ct);
        }

        async UniTask PopCore(bool immediate, CancellationToken ct)
        {
            if (_service == null || View == null || _state == null || Interlocked.Exchange(ref _state.Popped, 1) != 0)
            {
                return;
            }

            await _service.Pop(View, immediate, ct);
        }

        sealed class PopState
        {
            // Shared by struct copies so repeated Pop calls remain idempotent.
            public int Popped;
        }
    }
}
