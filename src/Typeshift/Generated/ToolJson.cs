using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using Weboost.SchemaAnnotations;

namespace Typeshift.Generated;

/// <summary>
/// How generated tools serialize their arguments and results and describe their parameters: the modules' JSON
/// contexts first, then Microsoft.Extensions.AI's defaults, which reflect unless the app is published with Native AOT
/// or trimmed.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ToolJson
{
    private readonly ConcurrentDictionary<Type, JsonElement> _schemas = [];
    private readonly ConcurrentDictionary<Type, ResultFormat> _results = [];
    private readonly JsonSerializerOptions _argumentOptions;

    /// <summary>
    /// Creates the options from the resolvers that come first, and the schemas from the docs of the modules.
    /// </summary>
    /// <param name="resolvers">The JSON contexts the modules register, asked in order.</param>
    /// <param name="annotations">The XML docs the modules register, which describe the parameters' properties.</param>
    public ToolJson(IEnumerable<IJsonTypeInfoResolver> resolvers, IEnumerable<ISchemaAnnotationSource> annotations)
    {
        // D45: the defaults reflect in an app that keeps JSON reflection, where tools need no context of their own.
        // A result is written without the defaults' indentation, so its text is the text the model reads
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                [.. resolvers, AIJsonUtilities.DefaultOptions.TypeInfoResolver]),
            WriteIndented = false
        };

        options.TypeClassifiers.Add(new UnionCaseClassifier(options));

        options.MakeReadOnly();
        SerializerOptions = options;

        // An argument holds what its schema requires, so a member left out, or null where its type takes none, fails
        // naming the member. Results keep the options above, since the annotations also fail a null being written
        _argumentOptions = new JsonSerializerOptions(options)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true
        };

        _argumentOptions.MakeReadOnly();

        Schemas = new ToolSchemas(options, annotations);
    }

    /// <summary>
    /// The options every tool result is serialized with. Arguments are read with a copy that also holds the members
    /// of a record or class to their nullability, and to what its constructor requires.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// Finds the JSON contract of a type.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <returns>The contract, from the first resolver that knows the type.</returns>
    /// <exception cref="NotSupportedException">
    /// No resolver knows the type: in an app published with Native AOT or trimmed, list it in a
    /// <c>JsonSerializerContext</c> of the tool's assembly.
    /// </exception>
    public JsonTypeInfo<T> GetTypeInfo<T>() => (JsonTypeInfo<T>)SerializerOptions.GetTypeInfo(typeof(T));

    internal JsonTypeInfo<T> GetArgumentInfo<T>() => (JsonTypeInfo<T>)_argumentOptions.GetTypeInfo(typeof(T));

    internal ToolSchemas Schemas { get; }

    // D62: built once per type and container
    internal ResultFormat GetResultFormat<T>() =>
        _results.GetOrAdd(typeof(T), static (_, json) => ResultFormats.Create<T>(json), this);

    // Built once per function type and container
    internal JsonElement GetSchema(Type function, string tool, IReadOnlyList<ToolParameter> parameters) =>
        _schemas.GetOrAdd(
            function,
            static (_, state) => state.Schemas.CreateSchema(state.Tool, state.Parameters),
            (Schemas, Tool: tool, Parameters: parameters));
}
