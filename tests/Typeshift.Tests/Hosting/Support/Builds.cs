using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Typeshift.Tests.Hosting.Support;

// How often the container built the services counted: a scope keeps each disposable it builds until the scope ends
public sealed class Builds
{
    private int _count;

    public int Count => _count;

    public void Add() => Interlocked.Increment(ref _count);
}

public static class BuildCountingExtensions
{
    extension(IServiceCollection services)
    {
        // Wraps the factory the service is registered with, so each build reaches the count
        public void CountBuilds<TService>(Builds builds) where TService : class
        {
            var registration = services.Single(service => service.ServiceType == typeof(TService));
            var build = registration.ImplementationFactory!;

            services.Replace(ServiceDescriptor.Describe(
                typeof(TService),
                provider =>
                {
                    builds.Add();
                    return build(provider);
                },
                registration.Lifetime));
        }
    }
}
