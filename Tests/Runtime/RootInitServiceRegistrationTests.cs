using System.Collections.Generic;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.Initialization;
using StickerFwk.Core.MasterData;
using StickerFwk.Infrastructure.Initialization;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Tests.Runtime
{
    public class RootInitServiceRegistrationTests
    {
        [Test]
        public void UseRootInit_SingletonShared_AcrossConcreteAndInterfaces()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IMasterDataRepository>(new StubMasterDataRepository());
            builder.UseRootInit();

            using var container = builder.Build();

            var asInterface = container.Resolve<IRootInitService>();
            var asAsyncStartable = container.Resolve<IAsyncStartable>();

            Assert.AreSame(asInterface, asAsyncStartable,
                "AsImplementedInterfaces with Singleton lifetime should share the same instance across all registered interfaces.");
            Assert.IsInstanceOf<RootInitService>(asInterface);
        }

        [Test]
        public void UseRootInit_DoesNotExposeConcreteType()
        {
            // AsImplementedInterfaces() on its own does not register the concrete type, so consumers
            // cannot accidentally pull a separate instance via Resolve<RootInitService>().
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IMasterDataRepository>(new StubMasterDataRepository());
            builder.UseRootInit();

            using var container = builder.Build();

            Assert.Throws<VContainerException>(() => container.Resolve<RootInitService>());
        }

        [Test]
        public void UseRootInit_RegistersDefaultSettings_WhenNoneProvided()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IMasterDataRepository>(new StubMasterDataRepository());
            builder.UseRootInit();

            using var container = builder.Build();

            var settings = container.Resolve<RootInitSettings>();
            Assert.AreSame(RootInitSettings.Default, settings);
            Assert.That(settings.TargetFrameRate, Is.EqualTo(-1));
        }

        [Test]
        public void StartAsync_FailureCompletesInitializationWithException()
        {
            var expected = new InvalidOperationException("load failed");
            var service = new RootInitService(
                new StubMasterDataRepository(_ => UniTask.FromException(expected), isLoaded: false),
                RootInitSettings.Default);

            Assert.CatchAsync<InvalidOperationException>(async () => await service.StartAsync(CancellationToken.None).AsTask());
            Assert.CatchAsync<InvalidOperationException>(async () => await service.Initialization.AsTask());
        }

        [Test]
        public void StartAsync_CancellationCompletesInitializationAsCanceled()
        {
            var service = new RootInitService(
                new StubMasterDataRepository(ct => UniTask.FromCanceled(ct), isLoaded: false),
                RootInitSettings.Default);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await service.StartAsync(cts.Token).AsTask());
            Assert.CatchAsync<OperationCanceledException>(async () => await service.Initialization.AsTask());
        }

        private sealed class StubMasterDataRepository : IMasterDataRepository
        {
            readonly Func<CancellationToken, UniTask> _load;

            public StubMasterDataRepository(Func<CancellationToken, UniTask> load = null, bool isLoaded = true)
            {
                _load = load ?? (_ => UniTask.CompletedTask);
                IsLoaded = isLoaded;
            }

            public bool IsLoaded { get; }
            public UniTask LoadAsync(CancellationToken ct = default) => _load(ct);
            public IReadOnlyList<T> GetAll<T>() where T : class, IMasterData => System.Array.Empty<T>();
            public T Get<T>(string id) where T : class, IMasterData => null;
            public bool TryGet<T>(string id, out T data) where T : class, IMasterData
            {
                data = null;
                return false;
            }
        }
    }
}
