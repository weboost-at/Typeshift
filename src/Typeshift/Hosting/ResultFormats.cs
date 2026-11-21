using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

// D62: a typed run's answer format, built once per type as a tool's parameters are, and held under data unless it is
// an object of properties, since providers take nothing else as an answer's schema
internal static class ResultFormats
{
    public static ResultFormat Create<T>(ToolJson json)
    {
        var element = AIJsonUtilities.CreateJsonSchema(
            typeof(T),
            serializerOptions: json.SerializerOptions,
            inferenceOptions: json.Schemas.CreateOptions);

        var schema = element.ValueKind is JsonValueKind.Object ? JsonObject.Create(element)! : new JsonObject();
        var wrapped = !IsObjectOfProperties(schema);
        if (wrapped) schema = UnderData(schema);

        var format = ChatResponseFormat.ForJsonSchema(
            JsonSerializer.SerializeToElement(schema, ToolJsonContext.Default.JsonObject),
            typeof(T).GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? SchemaName(typeof(T)),
            typeof(T).GetCustomAttribute<DescriptionAttribute>()?.Description);

        return new ResultFormat(format, wrapped ? WithResultUnderData<T>(json) : json.SerializerOptions);
    }

    private static bool IsObjectOfProperties(JsonObject schema) =>
        schema["type"] is JsonValue type
        && type.GetValue<string>() is "object"
        && schema["properties"] is JsonObject;

    private static JsonObject UnderData(JsonObject schema)
    {
        ToolSchemas.RebaseReferences(schema, "#/properties/data");

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["data"] = schema },
            ["additionalProperties"] = false,
            ["required"] = new JsonArray(JsonValue.Create("data"))
        };
    }

    // As Microsoft.Extensions.AI names the schema of a typed answer
    private static string SchemaName(Type type) => Regex.Replace(type.Name, "[^0-9A-Za-z_]", "_");

    // A contract of its own for T, since a converter in the options loses to an enum's converter and a polymorphic
    // type's metadata
    private static JsonSerializerOptions WithResultUnderData<T>(ToolJson json)
    {
        var options = new JsonSerializerOptions(json.SerializerOptions)
        {
            TypeInfoResolver = new DataResolver<T>(json.SerializerOptions.TypeInfoResolver!, json.GetTypeInfo<T>())
        };

        options.MakeReadOnly();
        return options;
    }

    private sealed class DataResolver<T>(IJsonTypeInfoResolver resolver, JsonTypeInfo<T> result) : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type != typeof(T)) return resolver.GetTypeInfo(type, options);

            var info = JsonMetadataServices.CreateValueInfo<T>(options, new DataConverter<T>(result));
            info.PolymorphismOptions = null;
            return info;
        }
    }

    // A typed answer under data, read as Microsoft.Extensions.AI reads one it wrapped itself
    private sealed class DataConverter<T>(JsonTypeInfo<T> result) : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("The answer is no JSON object holding a data property.");

            while (reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
            {
                var isData = reader.ValueTextEquals("data"u8);
                reader.Read();

                if (isData) return Finish(ref reader, JsonSerializer.Deserialize(ref reader, result));

                reader.Skip();
            }

            throw new JsonException("The answer holds no data property.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("data"u8);
            JsonSerializer.Serialize(writer, value, result);
            writer.WriteEndObject();
        }

        // A converter leaves the reader on the end of its value
        private static T? Finish(ref Utf8JsonReader reader, T? value)
        {
            while (reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
            {
                reader.Read();
                reader.Skip();
            }

            return value;
        }
    }
}

// A typed run's response format, and the options that read its answer
internal sealed record ResultFormat(ChatResponseFormatJson Format, JsonSerializerOptions SerializerOptions);
