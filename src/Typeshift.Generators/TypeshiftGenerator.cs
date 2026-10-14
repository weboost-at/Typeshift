using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators;

// D2: writes code only; the analyzer reports every diagnostic
[Generator(LanguageNames.CSharp)]
public sealed class TypeshiftGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var toolsets = context.SyntaxProvider
            .CreateSyntaxProvider(Discovery.IsCandidate, Discovery.Classify)
            .Where(static toolset => toolset is not null)
            .Select(static (toolset, _) => toolset!)
            .WithTrackingName(TrackingNames.Toolsets);

        context.RegisterSourceOutput(toolsets, static (output, toolset) => Emit(output, toolset));
    }

    private static void Emit(SourceProductionContext output, ToolsetModel toolset) =>
        output.AddSource(Names.HintName(toolset.Type.FullName), ToolsetEmitter.Emit(toolset));
}
