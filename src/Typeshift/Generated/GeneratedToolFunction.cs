using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    public override JsonElement JsonSchema => json.GetSchema(GetType(), Name, Parameters);

    /// <summary>
    /// Whether the tool only reads, so plan mode offers it.
    /// </summary>
    public virtual bool IsReadOnly => false;

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
    /// <exception cref="ArgumentException">
    /// The model did not pass the argument, passed null for a parameter that takes none, or passed JSON that does not
    /// fit the parameter's type.
    /// </exception>
    protected T GetArgument<T>(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value))
            throw new ArgumentException($"The tool '{Name}' requires the argument '{name}'.", name);

        // A parameter that takes any JSON, such as a JsonElement, takes a JSON null as the value it is
        if (IsNull(value) && value is not T && !AcceptsNull<T>(name))
            throw new ArgumentException($"The tool '{Name}' requires the argument '{name}', and it was null.", name);

        return Bind<T>(value, name);
    }

    /// <summary>
    /// Reads an optional argument.
    /// </summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="arguments">The model's arguments.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="defaultValue">The parameter's default.</param>
    /// <returns>The value, or <paramref name="defaultValue"/> when the argument is absent or null.</returns>
    /// <exception cref="ArgumentException">The model passed JSON that does not fit the parameter's type.</exception>
    protected T GetArgument<T>(AIFunctionArguments arguments, string name, T defaultValue) =>
        arguments.TryGetValue(name, out var value) && !IsNull(value) ? Bind<T>(value, name) : defaultValue;

    /// <summary>
    /// Turns a tool's result into the JSON the model receives.
    /// </summary>
    /// <typeparam name="T">The tool method's result type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The result as a <see cref="JsonElement"/>, serialized through its type's contract.</returns>
    protected object GetResult<T>(T result) => JsonSerializer.SerializeToElement(result, json.GetTypeInfo<T>());

    /// <summary>
    /// Reads a tool's stream to its end and turns its items into the JSON the model receives, as one array.
    /// </summary>
    /// <typeparam name="T">The type of the stream's items.</typeparam>
    /// <param name="stream">The stream the tool method returned.</param>
    /// <param name="ct">Cancels the reading, also of a stream that takes no token itself.</param>
    /// <returns>The items as a <see cref="JsonElement"/>, serialized through the contract of their list.</returns>
    /// <exception cref="OperationCanceledException">The call was cancelled before the stream ended.</exception>
    protected async ValueTask<object?> GetResultAsync<T>(IAsyncEnumerable<T> stream, CancellationToken ct)
    {
        var items = new List<T>();

        await foreach (var item in stream.WithCancellation(ct))
        {
            // An iterator without [EnumeratorCancellation] never sees the token
            ct.ThrowIfCancellationRequested();
            items.Add(item);
        }

        return GetResult(items);
    }

    // D62: null is the value of a nullable parameter and of a union, whose empty case it binds
    private bool AcceptsNull<T>(string name) =>
        Nullable.GetUnderlyingType(typeof(T)) is not null
        || default(T) is IUnion
        || Parameters.Any(parameter => parameter.Name == name && parameter.IsNullable);

    private T Bind<T>(object? value, string name)
    {
        if (value is T typed) return typed;
        if (IsNull(value)) return default!;

        var element = value as JsonElement?
            ?? JsonSerializer.SerializeToElement(value, json.SerializerOptions.GetTypeInfo(value!.GetType()));

        try
        {
            return Read<T>(element);
        }
        catch (JsonException exception)
        {
            // D53: the model corrects its call only when it reads which argument of which tool was wrong
            throw new ArgumentException(
                $"The argument '{name}' of the tool '{Name}' does not fit its schema: {exception.Message}",
                name,
                exception);
        }
    }

    // Some clients pass an object or an array as JSON text. The value is read as it is first, so that a text which
    // only starts like JSON still binds a string, also as a case of a union
    private T Read<T>(JsonElement element)
    {
        var info = json.GetArgumentInfo<T>();

        try
        {
            return element.Deserialize(info)!;
        }
        catch (JsonException) when (TryParseText(element, out var parsed))
        {
            return parsed.Deserialize(info)!;
        }
    }

    private InvalidOperationException NoServices() =>
        new($"The tool '{Name}' got no services: run it through an AgentHost, or pass them in its arguments.");

    private static bool IsNull(object? value) => value is null or JsonElement { ValueKind: JsonValueKind.Null };

    private static bool TryParseText(JsonElement element, out JsonElement parsed)
    {
        parsed = default;

        if (element.ValueKind is not JsonValueKind.String) return false;
        if (element.GetString()!.TrimStart() is not ['{' or '[', ..] text) return false;

        try
        {
            parsed = JsonElement.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
