using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.Initialization;
using VContainer.Unity;

namespace StickerFwk.Infrastructure.Initialization
{
    /// <summary>
    /// Orchestrates the root initialization pipeline. Runs all registered <see cref="IInitTask"/>s
    /// in three phases (Bootstrap → Load → Warmup), wraps the entire run with any registered
    /// <see cref="IInitObserver"/>s, and exposes a single completion handshake via
    /// <see cref="IRootInitService.Initialization"/>.
    /// </summary>
    /// <remarks>
    /// This service is registered automatically the first time <c>AddInitTask</c>,
    /// <c>AddInitObserver</c>, or any framework <c>Use*</c> helper is called on the container —
    /// projects never construct or register it directly.
    /// </remarks>
    public sealed class RootInitService : IRootInitService, IAsyncStartable
    {
        private const string Tag = "RootInitService";

        private readonly IReadOnlyList<IInitTask> _tasks;
        private readonly IReadOnlyList<IInitObserver> _observers;
        private readonly UniTaskCompletionSource _completionSource = new();

        public UniTask Initialization => _completionSource.Task;

        public RootInitService(IEnumerable<IInitTask> tasks, IEnumerable<IInitObserver> observers)
        {
            _tasks = tasks != null ? new List<IInitTask>(tasks) : (IReadOnlyList<IInitTask>)Array.Empty<IInitTask>();
            _observers = observers != null ? new List<IInitObserver>(observers) : (IReadOnlyList<IInitObserver>)Array.Empty<IInitObserver>();
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            var startedObservers = new List<IInitObserver>(_observers.Count);
            try
            {
                Log.Info(Tag, $"Root initialization started ({_tasks.Count} task(s), {_observers.Count} observer(s)).");

                for (var i = 0; i < _observers.Count; i++)
                {
                    await _observers[i].OnStartingAsync(cancellation);
                    startedObservers.Add(_observers[i]);
                }

                await RunPhaseSequential(InitPhase.Bootstrap, cancellation);
                await RunPhaseParallel(InitPhase.Load, cancellation);
                await RunPhaseSequential(InitPhase.Warmup, cancellation);

                _completionSource.TrySetResult();
                Log.Info(Tag, "Root initialization complete.");
            }
            catch (OperationCanceledException)
            {
                _completionSource.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                _completionSource.TrySetException(ex);
                throw;
            }
            finally
            {
                for (var i = startedObservers.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        await startedObservers[i].OnCompletedAsync(cancellation);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Tag, $"Observer OnCompletedAsync threw: {ex}");
                    }
                }
            }
        }

        private async UniTask RunPhaseSequential(InitPhase phase, CancellationToken ct)
        {
            for (var i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Phase != phase) continue;
                await _tasks[i].ExecuteAsync(ct);
            }
        }

        private async UniTask RunPhaseParallel(InitPhase phase, CancellationToken ct)
        {
            List<UniTask> running = null;
            for (var i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Phase != phase) continue;
                running ??= new List<UniTask>();
                running.Add(_tasks[i].ExecuteAsync(ct));
            }

            if (running != null)
            {
                await UniTask.WhenAll(running);
            }
        }
    }
}
