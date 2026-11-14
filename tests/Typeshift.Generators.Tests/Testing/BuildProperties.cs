using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Typeshift.Generators.Tests.Testing;

// The MSBuild properties a project passes to the generator and the analyzer, build_property.PublishAot for example
public sealed class BuildProperties(IReadOnlyDictionary<string, string> global) : AnalyzerConfigOptionsProvider
{
    public static readonly BuildProperties None = new(new Dictionary<string, string>());

    public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(global);

    public static BuildProperties PublishingAot() => With("PublishAot", "true");

    // What the SDK sets to false for an app published trimmed, and the Blazor WebAssembly SDK back to true
    public static BuildProperties JsonReflection(bool enabled) =>
        With("JsonSerializerIsReflectionEnabledByDefault", enabled ? "true" : "false");

    public static BuildProperties With(string property, string value) =>
        new(new Dictionary<string, string> { [$"build_property.{property}"] = value });

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public static readonly Options Empty = new(new Dictionary<string, string>());

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
