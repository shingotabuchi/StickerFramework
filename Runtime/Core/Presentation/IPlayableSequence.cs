using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Core.Presentation
{
    public interface IPlayableSequence<in TContext>
    {
        UniTask PlayAsync(TContext context, CancellationToken cancellationToken);
        void Stop();
    }
}
