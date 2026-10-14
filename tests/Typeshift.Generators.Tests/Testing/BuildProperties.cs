using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Typeshift.Generators.Tests.Testing;

// The MSBuild properties a project passes to the generator and the analyzer, build_property.TypeshiftIdType say
public sealed class BuildProperties(IReadOnlyDictionary<string, string> global) : AnalyzerConfigOptionsProvider
{
    public static readonly BuildProperties None = new(new Dictionary<string, string>());

    public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(global);

    public static BuildProperties WithIdType(string idType) =>
        new(new Dictionary<string, string> { ["build_property.TypeshiftIdType"] = idType });

    public static BuildProperties PublishingAot() =>
        new(new Dictionary<string, string> { ["build_property.PublishAot"] = "true" });

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public static readonly Options Empty = new(new Dictionary<string, string>());

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
