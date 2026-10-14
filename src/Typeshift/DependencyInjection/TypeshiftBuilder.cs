using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

/// <summary>
/// The registration chain <c>AddTypeshift()</c> starts.
/// </summary>
public sealed class TypeshiftBuilder
{
    private readonly IServiceCollection _services;

    internal TypeshiftBuilder(IServiceCollection services) => _services = services;

    /// <summary>
    /// Registers the provider that maps model names to clients, scoped.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithChatClientProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IChatClientProvider
    {
        _services.AddScoped<IChatClientProvider, TProvider>();
        return this;
    }
}
