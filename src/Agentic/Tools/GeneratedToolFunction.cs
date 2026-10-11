using System.Text.Json;
using Agentic.Json;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tools;

/// <summary>
/// The base of every generated tool: binds arguments through JSON contracts and runs each call in its own DI scope.
/// </summary>
public abstract class GeneratedToolFunction : AIFunction
{
    private readonly AgentJson _json;

    /// <summary>
    /// Creates the function of a tool, running with the given resolved settings.
    /// </summary>
    protected GeneratedToolFunction(ToolDefinition definition, object? settings, AgentJson json)
    {
        Definition = definition;
        Settings = settings;
        _json = json;
    }

    /// <summary>
    /// The tool as its toolset declares it.
    /// </summary>
    public ToolDefinition Definition { get; }

    /// <summary>
    /// The resolved settings every call of this function runs with.
    /// </summary>
    public object? Settings { get; }

    /// <inheritdoc />
    public override string Name => Definition.Name.Value;

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions => _json.SerializerOptions;

    /// <inheritdoc />
    public override JsonElement JsonSchema => _json.GetToolSchema(GetType(), Parameters);

    /// <summary>
    /// The model-facing parameters, in declaration order.
    /// </summary>
    protected abstract IReadOnlyList<ToolParameter> Parameters { get; }

    /// <inheritdoc />
    protected sealed override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken ct)
    {
        // FunctionInvokingChatClient hands every tool the root provider, scoped services need a scope per call
        if (arguments.Services?.GetService<IServiceScopeFactory>() is not { } scopes)
            throw new InvalidOperationException(
                $"The tool '{Name}' needs the services of a container built with AddAgentic().");

        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ToolSettingsAccessor>().Settings = Settings;

        return await InvokeToolAsync(arguments, scope.ServiceProvider, ct);
    }

    /// <summary>
    /// Binds the arguments and calls the tool method with the call's scoped services.
    /// </summary>
    protected abstract ValueTask<object?> InvokeToolAsync(
        AIFunctionArguments arguments,
        IServiceProvider services,
        CancellationToken ct);

    /// <summary>
    /// The value of a required argument; a missing one fails the call.
    /// </summary>
    protected T GetArgument<T>(AIFunctionArguments arguments, string name) =>
        arguments.TryGetValue(name, out var value)
            ? Bind<T>(value)
            : throw new ArgumentException($"The tool '{Name}' requires the argument '{name}'.", name);

    /// <summary>
    /// The value of an optional argument, or its default when it is absent or null.
    /// </summary>
    protected T GetArgument<T>(AIFunctionArguments arguments, string name, T defaultValue) =>
        arguments.TryGetValue(name, out var value) && !IsNull(value) ? Bind<T>(value) : defaultValue;

    /// <summary>
    /// The result as JSON, serialized through the result type's contract.
    /// </summary>
    protected object GetResult<T>(T result) => JsonSerializer.SerializeToElement(result, _json.GetTypeInfo<T>());

    private T Bind<T>(object? value) =>
        value switch
        {
            T typed => typed,
            _ when IsNull(value) => default!,
            JsonElement element => element.Deserialize(_json.GetTypeInfo<T>())!,
            string text when IsJsonText(text) => JsonSerializer.Deserialize(text, _json.GetTypeInfo<T>())!,
            _ => JsonSerializer
                .SerializeToElement(value, _json.SerializerOptions.GetTypeInfo(value!.GetType()))
                .Deserialize(_json.GetTypeInfo<T>())!
        };

    private static bool IsNull(object? value) => value is null or JsonElement { ValueKind: JsonValueKind.Null };

    // Some clients pass objects and arrays as JSON text; any other string is a value of its own
    private static bool IsJsonText(string text) => text.TrimStart() is ['{' or '[', ..];
}
