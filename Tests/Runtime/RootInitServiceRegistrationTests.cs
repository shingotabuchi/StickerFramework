using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.Initialization;
using StickerFwk.Core.MasterData;
using StickerFwk.Infrastructure.Initialization;
using StickerFwk.Infrastructure.MasterData;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Tests.Runtime
{
    public class RootInitServiceRegistrationTests
    {
        [Test]
        public void AddInitTask_RegistersOrchestrator_AsImplementedInterfaces()
        {
            var builder = new ContainerBuilder();
            builder.AddInitTask(new RecordingTask(InitPhase.Bootstrap));

            using var container = builder.Build();

            var asInterface = container.Resolve<IRootInitService>();
            var asAsyncStartable = container.Resolve<IAsyncStartable>();

            Assert.AreSame(asInterface, asAsyncStartable,
                "Orchestrator must be the same instance across IRootInitService and IAsyncStartable.");
            Assert.IsInstanceOf<RootInitService>(asInterface);
        }

        [Test]
        public void AddInitTask_DoesNotExposeConcreteOrchestrator()
        {
            var builder = new ContainerBuilder();
            builder.AddInitTask(new RecordingTask(InitPhase.Load));

            using var container = builder.Build();

            Assert.Throws<VContainerException>(() => container.Resolve<RootInitService>());
        }

        [Test]
        public void MultipleAddCalls_RegisterOrchestratorOnlyOnce()
        {
            var builder = new ContainerBuilder();
            builder.AddInitTask(new RecordingTask(InitPhase.Bootstrap));
            builder.AddInitTask(new RecordingTask(InitPhase.Load));
            builder.AddInitObserver(new RecordingObserver());

            using var container = builder.Build();

            // Resolve all IAsyncStartable; should be exactly one orchestrator.
            var startables = container.Resolve<IReadOnlyList<IAsyncStartable>>();
            var orchestratorCount = 0;
            foreach (var s in startables)
            {
                if (s is RootInitService) orchestratorCount++;
            }
            Assert.AreEqual(1, orchestratorCount);
        }

        [Test]
        public void StartAsync_RunsTasksInPhaseOrder_BootstrapLoadWarmup()
        {
            var log = new List<string>();
            var bootstrap = new RecordingTask(InitPhase.Bootstrap, log, "boot");
            var load1 = new RecordingTask(InitPhase.Load, log, "load1", delayMs: 30);
            var load2 = new RecordingTask(InitPhase.Load, log, "load2", delayMs: 10);
            var warmup = new RecordingTask(InitPhase.Warmup, log, "warm");

            var service = new RootInitService(
                new IInitTask[] { warmup, load1, bootstrap, load2 },
                Array.Empty<IInitObserver>());

            service.StartAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

            Assert.AreEqual("boot", log[0], "Bootstrap must run first.");
            Assert.AreEqual("warm", log[3], "Warmup must run last.");
            // load1 / load2 run in parallel — both must appear before warm and after boot.
            Assert.That(log.IndexOf("load1"), Is.InRange(1, 2));
            Assert.That(log.IndexOf("load2"), Is.InRange(1, 2));
        }

        [Test]
        public void StartAsync_FailureCompletesInitializationWithException()
        {
            var expected = new InvalidOperationException("load failed");
            var failing = new RecordingTask(InitPhase.Load, throwOnExecute: expected);
            var service = new RootInitService(new[] { failing }, Array.Empty<IInitObserver>());

            Assert.CatchAsync<InvalidOperationException>(async () => await service.StartAsync(CancellationToken.None).AsTask());
            Assert.CatchAsync<InvalidOperationException>(async () => await service.Initialization.AsTask());
        }

        [Test]
        public void StartAsync_CancellationCompletesInitializationAsCanceled()
        {
            var canceling = new RecordingTask(InitPhase.Load, executeImpl: ct => UniTask.FromCanceled(ct));
            var service = new RootInitService(new[] { canceling }, Array.Empty<IInitObserver>());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await service.StartAsync(cts.Token).AsTask());
            Assert.CatchAsync<OperationCanceledException>(async () => await service.Initialization.AsTask());
        }

        [Test]
        public void Observers_StartBeforeAndCompleteAfter_EvenOnFailure()
        {
            var log = new List<string>();
            var observer = new RecordingObserver(log);
            var failing = new RecordingTask(InitPhase.Bootstrap, throwOnExecute: new InvalidOperationException("fail"));
            var service = new RootInitService(new[] { failing }, new IInitObserver[] { observer });

            try { service.StartAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { /* expected */ }

            Assert.AreEqual("starting", log[0]);
            Assert.AreEqual("completed", log[^1]);
        }

        [Test]
        public void UseMasterDataInit_RegistersMasterDataLoadAsInitTask()
        {
            var repo = new StubMasterDataRepository(isLoaded: false);
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IMasterDataRepository>(repo);
            builder.UseMasterDataInit();

            using var container = builder.Build();

            var tasks = container.Resolve<IReadOnlyList<IInitTask>>();
            Assert.That(tasks, Has.Some.InstanceOf<MasterDataInitTask>());
        }

        private sealed class RecordingTask : IInitTask
        {
            private readonly List<string> _log;
            private readonly string _name;
            private readonly int _delayMs;
            private readonly Exception _throw;
            private readonly Func<CancellationToken, UniTask> _executeImpl;

            public InitPhase Phase { get; }

            public RecordingTask(
                InitPhase phase,
                List<string> log = null,
                string name = null,
                int delayMs = 0,
                Exception throwOnExecute = null,
                Func<CancellationToken, UniTask> executeImpl = null)
            {
                Phase = phase;
                _log = log;
                _name = name;
                _delayMs = delayMs;
                _throw = throwOnExecute;
                _executeImpl = executeImpl;
            }

            public async UniTask ExecuteAsync(CancellationToken ct)
            {
                if (_executeImpl != null) { await _executeImpl(ct); return; }
                if (_delayMs > 0) await UniTask.Delay(_delayMs, cancellationToken: ct);
                if (_throw != null) throw _throw;
                if (_log != null && _name != null) _log.Add(_name);
            }
        }

        private sealed class RecordingObserver : IInitObserver
        {
            private readonly List<string> _log;
            public RecordingObserver(List<string> log = null) => _log = log;

            public UniTask OnStartingAsync(CancellationToken ct)
            {
                _log?.Add("starting");
                return UniTask.CompletedTask;
            }

            public UniTask OnCompletedAsync(CancellationToken ct)
            {
                _log?.Add("completed");
                return UniTask.CompletedTask;
            }
        }

        private sealed class StubMasterDataRepository : IMasterDataRepository
        {
            public StubMasterDataRepository(bool isLoaded = true) => IsLoaded = isLoaded;
            public bool IsLoaded { get; }
            public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public IReadOnlyList<T> GetAll<T>() where T : class, IMasterData => Array.Empty<T>();
            public T Get<T>(string id) where T : class, IMasterData => null;
            public bool TryGet<T>(string id, out T data) where T : class, IMasterData
            {
                data = null;
                return false;
            }
        }
    }
}
