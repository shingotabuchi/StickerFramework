using System;
using System.Threading;

namespace StickerFwk.Core
{
    public sealed class ScopeCancellation : IScopeCancellation
    {
        private readonly CancellationHandle _handle;

        public ScopeCancellation(CancellationToken scopeCancellationToken = default)
        {
            _handle = new CancellationHandle(scopeCancellationToken);
        }

        public CancellationToken Token => _handle.Token;
        public bool IsCancellationRequested => _handle.IsCancellationRequested;

        public ICancellationHandle CreateLinked(CancellationToken cancellationToken = default)
        {
            return new CancellationHandle(Token, cancellationToken);
        }

        public void Dispose()
        {
            _handle.Dispose();
        }

        private sealed class CancellationHandle : ICancellationHandle
        {
            private readonly CancellationTokenSource _cts;
            private bool _disposed;

            public CancellationHandle(CancellationToken cancellationToken)
            {
                _cts = cancellationToken.CanBeCanceled
                    ? CancellationTokenSource.CreateLinkedTokenSource(new[] { cancellationToken })
                    : new CancellationTokenSource();
            }

            public CancellationHandle(CancellationToken first, CancellationToken second)
            {
                if (first.CanBeCanceled && second.CanBeCanceled)
                {
                    _cts = CancellationTokenSource.CreateLinkedTokenSource(first, second);
                }
                else if (first.CanBeCanceled)
                {
                    _cts = CancellationTokenSource.CreateLinkedTokenSource(new[] { first });
                }
                else if (second.CanBeCanceled)
                {
                    _cts = CancellationTokenSource.CreateLinkedTokenSource(new[] { second });
                }
                else
                {
                    _cts = new CancellationTokenSource();
                }
            }

            public CancellationToken Token => IsCancellationRequested ? new CancellationToken(true) : _cts.Token;

            public bool IsCancellationRequested => _disposed || _cts.IsCancellationRequested;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _cts.Cancel();
                _cts.Dispose();
            }
        }
    }
}
