using System.Text.Json;
using Agentic.Json;
using Agentic.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Testing.Bases;

// A container wired like a host, built on first use with ValidateOnBuild and ValidateScopes
public abstract class HostTestBase : UnitTestBase, IAsyncDisposable
{
    private readonly Lazy<ServiceProvider> _services;

    protected HostTestBase() => _services = new(BuildServices);

    protected ServiceProvider Services => _services.Value;

    protected AgentJson Json => Services.GetRequiredService<AgentJson>();

    protected ScriptedChatClient ChatClient { get; } = new();

    public async ValueTask DisposeAsync()
    {
        if (_services.IsValueCreated) await _services.Value.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    // The function loop a provider client sits under, driven by the scripted client
    protected IChatClient FunctionLoop() => ChatClient.AsBuilder().UseFunctionInvocation().Build(Services);

    protected AIFunctionArguments Arguments(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value))
        {
            Services = Services
        };

    protected T ReadResult<T>(object? result) => ((JsonElement)result!).Deserialize(Json.GetTypeInfo<T>())!;

    // A fresh content per scripted call, the function loop never invokes a call whose result it already has
    protected ChatResponseUpdate Call(string callId, string tool, params (string Name, object? Value)[] arguments) =>
        new(ChatRole.Assistant, [new FunctionCallContent(callId, tool, AsJson(arguments))]);

    protected ChatResponseUpdate Calls(params ChatResponseUpdate[] calls) =>
        new(ChatRole.Assistant, [.. calls.SelectMany(call => call.Contents)]);

    protected static ChatResponseUpdate Text(string text) => new(ChatRole.Assistant, text);

    // Arguments arrive as JSON, as a provider client delivers them
    private Dictionary<string, object?> AsJson((string Name, object? Value)[] arguments) =>
        arguments.ToDictionary(argument => argument.Name, argument => (object?)AsJson(argument.Value));

    private JsonElement AsJson(object? value) => JsonSerializer.SerializeToElement(value, Json.SerializerOptions);

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddAgentic();
        services.AddSingleton<TimeProvider>(Time);
        ConfigureServices(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
