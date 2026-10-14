using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// The base of every generated tool: binds the model's arguments through JSON contracts and passes the run's services.
/// </summary>
/// <param name="json">The options arguments and results are bound with.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class GeneratedToolFunction(ToolJson json) : AIFunction
{
    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions => json.SerializerOptions;

    /// <inheritdoc />
    public override JsonElement JsonSchema => json.GetSchema(GetType(), Parameters);

    /// <summary>
    /// The model-facing parameters, in declaration order.
    /// </summary>
    protected abstract IReadOnlyList<ToolParameter> Parameters { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The arguments carry no services.</exception>
    protected sealed override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken ct) =>
        // D25: the run's services, those of the scope its host was resolved from
        InvokeToolAsync(arguments, arguments.Services ?? throw NoServices(), ct);

    /// <summary>
    /// Binds the arguments and calls the tool method.
    /// </summary>
    /// <param name="arguments">The model's arguments.</param>
    /// <param name="services">The run's services.</param>
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
    protected object GetResult<T>(T result) => JsonSerializer.SerializeToElement(result, json.GetTypeInfo<T>());

    private T Bind<T>(object? value) =>
        value switch
        {
            T typed => typed,
            _ when IsNull(value) => default!,
            JsonElement element => element.Deserialize(json.GetTypeInfo<T>())!,
            string text when IsJsonText(text) => JsonSerializer.Deserialize(text, json.GetTypeInfo<T>())!,
            _ => JsonSerializer
                .SerializeToElement(value, json.SerializerOptions.GetTypeInfo(value!.GetType()))
                .Deserialize(json.GetTypeInfo<T>())!
        };

    private InvalidOperationException NoServices() =>
        new($"The tool '{Name}' got no services: run it through an AgentHost, or pass them in its arguments.");

    private static bool IsNull(object? value) => value is null or JsonElement { ValueKind: JsonValueKind.Null };

    // Some clients pass objects and arrays as JSON text; any other string is a value of its own
    private static bool IsJsonText(string text) => text.TrimStart() is ['{' or '[', ..];
}
