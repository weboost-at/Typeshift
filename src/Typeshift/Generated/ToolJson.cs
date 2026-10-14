using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// How generated tools serialize their arguments and results and describe their parameters: the modules' JSON
/// contexts first, then Microsoft.Extensions.AI's defaults, which reflect outside Native AOT.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ToolJson
{
    private readonly ConcurrentDictionary<Type, JsonElement> _schemas = [];

    /// <summary>
    /// Creates the options from the resolvers that come first.
    /// </summary>
    /// <param name="resolvers">The JSON contexts the modules register, asked in order.</param>
    public ToolJson(IEnumerable<IJsonTypeInfoResolver> resolvers)
    {
        // D45: outside Native AOT the defaults reflect, so tools need no context of their own
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                [.. resolvers, AIJsonUtilities.DefaultOptions.TypeInfoResolver])
        };

        options.MakeReadOnly();
        SerializerOptions = options;
    }

    /// <summary>
    /// The options every tool argument and result is serialized with.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// Finds the JSON contract of a type.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <returns>The contract, from the first resolver that knows the type.</returns>
    /// <exception cref="NotSupportedException">
    /// No resolver knows the type: under Native AOT, list it in a <c>JsonSerializerContext</c> of the tool's assembly.
    /// </exception>
    public JsonTypeInfo<T> GetTypeInfo<T>() => (JsonTypeInfo<T>)SerializerOptions.GetTypeInfo(typeof(T));

    // Built once per function type from the STJ contracts, so naming policies and converters match the binding
    internal JsonElement GetSchema(Type function, IReadOnlyList<ToolParameter> parameters) =>
        _schemas.GetOrAdd(
            function,
            static (_, state) => state.Json.CreateSchema(state.Parameters),
            (Json: this, Parameters: parameters));

    private JsonElement CreateSchema(IReadOnlyList<ToolParameter> parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in parameters)
        {
            properties[parameter.Name] = CreateParameterSchema(parameter);
            if (!parameter.HasDefaultValue) required.Add((JsonNode?)JsonValue.Create(parameter.Name));
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0) schema["required"] = required;

        return JsonSerializer.SerializeToElement(schema, ToolJsonContext.Default.JsonObject);
    }

    // System.Type carries no reference nullability, the generator passes it from the parameter's annotation
    private JsonObject CreateParameterSchema(ToolParameter parameter)
    {
        var element = AIJsonUtilities.CreateJsonSchema(
            parameter.Type,
            parameter.Description,
            parameter.HasDefaultValue,
            parameter.DefaultValue,
            SerializerOptions);

        // A type that takes any JSON gets the schema true, which {} matches
        var schema = element.ValueKind is JsonValueKind.Object ? JsonObject.Create(element)! : new JsonObject();
        return parameter.IsNullable ? AsNullable(schema) : schema;
    }

    // D48: a nullable reference is typed [type, "null"], as the reflection path types it
    private static JsonObject AsNullable(JsonObject schema)
    {
        if (schema["type"] is JsonValue type && type.GetValue<string>() is not "null" and var name)
            schema["type"] = new JsonArray(JsonValue.Create(name), JsonValue.Create("null"));

        return schema;
    }
}
