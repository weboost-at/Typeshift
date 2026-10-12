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
    /// <param name="definition">The tool as its toolset declares it.</param>
    /// <param name="settings">The settings every call runs with, or null for a tool without settings.</param>
    /// <param name="json">The options arguments and results are bound with.</param>
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
    /// <exception cref="InvalidOperationException">
    /// The arguments carry no services of a container built with <c>AddAgentic()</c>.
    /// </exception>
    protected sealed override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken ct)
    {
        // D11: FunctionInvokingChatClient hands every tool the root provider, scoped services need a scope per call
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
    /// <param name="arguments">The model's arguments.</param>
    /// <param name="services">The services of the call's own scope.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool's result as JSON, or null for a tool that returns nothing.</returns>
    protected abstract ValueTask<object?> InvokeToolAsync(
        AIFunctionArguments arguments,
        IServiceProvider services,
        CancellationToken ct);

    /// <summary>
    /// Reads a required argument.
    /// </summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="arguments">The model's arguments.</param>
    /// <param name="name">The parameter's name.</param>
    /// <returns>The value, bound through the type's JSON contract.</returns>
    /// <exception cref="ArgumentException">The model did not pass the argument.</exception>
    protected T GetArgument<T>(AIFunctionArguments arguments, string name) =>
        arguments.TryGetValue(name, out var value)
            ? Bind<T>(value)
            : throw new ArgumentException($"The tool '{Name}' requires the argument '{name}'.", name);

    /// <summary>
    /// Reads an optional argument.
    /// </summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="arguments">The model's arguments.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="defaultValue">The parameter's default.</param>
    /// <returns>The value, or <paramref name="defaultValue"/> when the argument is absent or null.</returns>
    protected T GetArgument<T>(AIFunctionArguments arguments, string name, T defaultValue) =>
        arguments.TryGetValue(name, out var value) && !IsNull(value) ? Bind<T>(value) : defaultValue;

    /// <summary>
    /// Turns a tool's result into the JSON the model receives.
    /// </summary>
    /// <typeparam name="T">The tool method's result type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The result as a <see cref="JsonElement"/>, serialized through its type's contract.</returns>
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
