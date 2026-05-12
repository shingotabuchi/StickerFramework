using System.Runtime.CompilerServices;
using StickerFwk.Core.Initialization;
using VContainer;

namespace StickerFwk.Infrastructure.Initialization
{
    /// <summary>
    /// VContainer extensions for assembling the root initialization pipeline. Each
    /// <c>AddInitTask</c> / <c>AddInitObserver</c> call (and the framework's <c>Use*</c> helpers
    /// that build on them) idempotently registers <see cref="RootInitService"/> as the singleton
    /// orchestrator — projects never need to register the orchestrator themselves.
    /// </summary>
    public static class InitContainerBuilderExtensions
    {
        // Tracks builders that have already had RootInitService registered so we don't double-register
        // (which would create two orchestrator instances and double-run every task).
        private static readonly ConditionalWeakTable<IContainerBuilder, object> Registered = new();

        /// <summary>Registers <typeparamref name="T"/> as a singleton <see cref="IInitTask"/>.</summary>
        public static RegistrationBuilder AddInitTask<T>(this IContainerBuilder builder)
            where T : class, IInitTask
        {
            EnsurePipeline(builder);
            return builder.Register<T>(Lifetime.Singleton).As<IInitTask>();
        }

        /// <summary>Registers a pre-constructed <see cref="IInitTask"/> instance.</summary>
        public static RegistrationBuilder AddInitTask(this IContainerBuilder builder, IInitTask task)
        {
            EnsurePipeline(builder);
            return builder.RegisterInstance(task).As<IInitTask>();
        }

        /// <summary>Registers <typeparamref name="T"/> as a singleton <see cref="IInitObserver"/>.</summary>
        public static RegistrationBuilder AddInitObserver<T>(this IContainerBuilder builder)
            where T : class, IInitObserver
        {
            EnsurePipeline(builder);
            return builder.Register<T>(Lifetime.Singleton).As<IInitObserver>();
        }

        /// <summary>Registers a pre-constructed <see cref="IInitObserver"/> instance.</summary>
        public static RegistrationBuilder AddInitObserver(this IContainerBuilder builder, IInitObserver observer)
        {
            EnsurePipeline(builder);
            return builder.RegisterInstance(observer).As<IInitObserver>();
        }

        internal static void EnsurePipeline(IContainerBuilder builder)
        {
            if (Registered.TryGetValue(builder, out _)) return;
            builder.Register<RootInitService>(Lifetime.Singleton).AsImplementedInterfaces();
            Registered.Add(builder, new object());
        }
    }
}
