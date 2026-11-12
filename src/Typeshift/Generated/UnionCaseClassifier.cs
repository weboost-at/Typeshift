using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Typeshift.Generated;

// D62: a union's case by the JSON token first, so that numbers read from strings leave a string case unambiguous,
// then as the web defaults read it, and an object's case by its properties; a union it cannot tell apart keeps the
// serializer's own classification
internal sealed class UnionCaseClassifier(JsonSerializerOptions serializerOptions) : JsonTypeClassifierFactory
{
    private static readonly JsonUnionTypeStructuralClassifier Structural = new();

    private readonly ConcurrentDictionary<Type, JsonTypeClassifier?> _classifiers = [];
    private readonly Lazy<JsonSerializerOptions> _exact = new(() => Exact(serializerOptions));

    public override bool CanClassify(JsonTypeClassifierContext context) =>
        context.Kind is JsonTypeClassifierKind.Union && Get(context) is not null;

    public override JsonTypeClassifier CreateJsonClassifier(
        JsonTypeClassifierContext context,
        JsonSerializerOptions options) =>
        Get(context)!;

    private JsonTypeClassifier? Get(JsonTypeClassifierContext context) =>
        _classifiers.GetOrAdd(
            context.DeclaringType,
            static (_, state) => state.Classifier.Create(state.Context),
            (Classifier: this, Context: context));

    private JsonTypeClassifier? Create(JsonTypeClassifierContext context)
    {
        var exact = TryCreate(context, _exact.Value);
        var lenient = TryCreate(context, serializerOptions);

        if (exact is null && lenient is null) return null;

        var cases = string.Join(", ", context.UnionCases.Select(@case => @case.CaseType.Name));
        var union = context.DeclaringType.Name;

        // The model reads the message (D53), so it names the cases a value may take
        return (ref Utf8JsonReader reader) =>
        {
            var copy = reader;
            var token = Describe(reader.TokenType);

            return exact?.Invoke(ref copy)
                ?? lenient?.Invoke(ref reader)
                ?? throw new JsonException($"A JSON {token} fits no single case of {union}: {cases}.");
        };
    }

    private static string Describe(JsonTokenType token) =>
        token switch
        {
            JsonTokenType.StartObject => "object",
            JsonTokenType.StartArray => "array",
            JsonTokenType.String => "string",
            JsonTokenType.Number => "number",
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => "null"
        };

    private static JsonTypeClassifier? TryCreate(JsonTypeClassifierContext context, JsonSerializerOptions options)
    {
        try
        {
            return Structural.CreateJsonClassifier(context, options);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions Exact(JsonSerializerOptions options)
    {
        var exact = new JsonSerializerOptions(options) { NumberHandling = JsonNumberHandling.Strict };
        exact.TypeClassifiers.Clear();
        exact.MakeReadOnly();
        return exact;
    }
}
