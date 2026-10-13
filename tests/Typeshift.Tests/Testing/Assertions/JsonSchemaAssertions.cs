using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace Typeshift.Tests.Testing.Assertions;

// A tool's parameter schema: {"type":"object","properties":{...},"required":[...]}
public sealed class JsonSchemaAssertions(JsonElement schema)
{
    public JsonElement Properties(params string[] names)
    {
        schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Should().Equal(names);
        return schema;
    }

    public JsonElement ParameterTypes(string parameter, params string[] types)
    {
        var type = Parameter(parameter).GetProperty("type");
        type.ValueKind.Should().Be(JsonValueKind.Array);
        type.EnumerateArray().Select(element => element.GetString()).Should().Equal(types);
        return schema;
    }

    public JsonElement ParameterDescription(string parameter, string description)
    {
        Parameter(parameter).GetProperty("description").GetString().Should().Be(description);
        return schema;
    }

    public JsonElement ParameterSchema(string parameter, string json) => Equivalent(Parameter(parameter), json);

    public JsonElement ParameterDefault(string parameter, string json) =>
        Equivalent(Parameter(parameter).GetProperty("default"), json);

    // The generated schemas carry <param> descriptions, which the reflection path does not read
    public JsonElement MatchesWithoutDescriptions(JsonElement expected)
    {
        var actual = WithoutParameterDescriptions(schema);
        var reflected = WithoutParameterDescriptions(expected);

        JsonNode.DeepEquals(actual, reflected)
            .Should()
            .BeTrue($"{actual.ToJsonString()} is {reflected.ToJsonString()}");
        return schema;
    }

    private JsonElement Parameter(string name) => schema.GetProperty("properties").GetProperty(name);

    private JsonElement Equivalent(JsonElement element, string json)
    {
        var actual = JsonNode.Parse(element.GetRawText());
        var expected = JsonNode.Parse(json);

        JsonNode.DeepEquals(actual, expected).Should().BeTrue($"{json} is expected, not {actual?.ToJsonString()}");
        return schema;
    }

    private static JsonNode WithoutParameterDescriptions(JsonElement schema)
    {
        var node = JsonNode.Parse(schema.GetRawText())!;

        foreach (var (_, parameter) in node["properties"]!.AsObject())
        {
            parameter!.AsObject().Remove("description");
        }

        return node;
    }
}

public static class JsonSchemaAssertionExtensions
{
    extension(JsonElement schema)
    {
        public JsonSchemaAssertions Assert => new(schema);
    }
}
