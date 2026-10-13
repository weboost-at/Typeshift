using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Typeshift.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Typeshift.Json;

/// <summary>
/// The serializer and schema options the library runs with: the developer's resolvers, then the library's own
/// context, then the resolver of <see cref="AIJsonUtilities.DefaultOptions"/>.
/// </summary>
public sealed class AgentJson
{
    private readonly ConcurrentDictionary<Type, JsonElement> _toolSchemas = [];

    /// <summary>
    /// Composes the options from the configured <see cref="AgentJsonOptions"/>.
    /// </summary>
    /// <param name="options">The configured options; <c>AddTypeshift()</c> registers them.</param>
    public AgentJson(IOptions<AgentJsonOptions> options)
    {
        SerializerOptions = LibraryJson.CreateOptions(options.Value.TypeInfoResolverChain);
        SchemaCreateOptions = options.Value.SchemaCreateOptions;
    }

    /// <summary>
    /// The options every tool argument and result is serialized with.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// The options tool schemas are created with.
    /// </summary>
    public AIJsonSchemaCreateOptions SchemaCreateOptions { get; }

    /// <summary>
    /// Finds the JSON contract of a type.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <returns>The contract, from the first resolver that knows the type.</returns>
    /// <exception cref="NotSupportedException">
    /// No resolver knows the type: under Native AOT, add it to a context in
    /// <see cref="AgentJsonOptions.TypeInfoResolverChain"/>.
    /// </exception>
    public JsonTypeInfo<T> GetTypeInfo<T>() => (JsonTypeInfo<T>)SerializerOptions.GetTypeInfo(typeof(T));

    // Built once per function type, from the STJ contracts, so naming policies and converters match the binding
    internal JsonElement GetToolSchema(Type function, IReadOnlyList<ToolParameter> parameters) =>
        _toolSchemas.GetOrAdd(
            function,
            static (_, state) => state.Json.CreateToolSchema(state.Parameters),
            (Json: this, Parameters: parameters));

    private JsonElement CreateToolSchema(IReadOnlyList<ToolParameter> parameters)
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

        var element = JsonSerializer.SerializeToElement(schema, AgentJsonContext.Default.JsonObject);
        return SchemaCreateOptions.TransformOptions is { } transform
            ? AIJsonUtilities.TransformSchema(element, transform)
            : element;
    }

    // System.Type carries no reference nullability, the generator passes it from the parameter's annotation
    private JsonObject CreateParameterSchema(ToolParameter parameter)
    {
        var element = AIJsonUtilities.CreateJsonSchema(
            parameter.Type,
            parameter.Description,
            parameter.HasDefaultValue,
            parameter.DefaultValue,
            SerializerOptions,
            SchemaCreateOptions with { TransformOptions = null });

        var schema = JsonObject.Create(element)!;
        return parameter.IsNullable ? AsNullable(schema) : schema;
    }

    // D12: a nullable reference is typed [type, "null"], as the reflection path types it
    private static JsonObject AsNullable(JsonObject schema)
    {
        if (schema["type"] is JsonValue type && type.GetValue<string>() is not "null" and var name)
            schema["type"] = new JsonArray(JsonValue.Create(name), JsonValue.Create("null"));

        return schema;
    }
}
