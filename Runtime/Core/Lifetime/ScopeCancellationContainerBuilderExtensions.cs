using System;
using System.Threading;
using VContainer;
using VContainer.Unity;

namespace StickerFwk.Core
{
    public static class ScopeCancellationContainerBuilderExtensions
    {
        public static IContainerBuilder UseScopeCancellation(this IContainerBuilder builder, LifetimeScope scope)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return builder.UseScopeCancellation(scope.destroyCancellationToken);
        }

        public static IContainerBuilder UseScopeCancellation(
            this IContainerBuilder builder,
            CancellationToken cancellationToken = default)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Register<IScopeCancellation>(_ => new ScopeCancellation(cancellationToken), Lifetime.Scoped);
            return builder;
        }
    }
}
