using Agentic.Generators.Emitting;
using Agentic.Generators.Models;
using Agentic.Generators.Parsing;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AgentGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .CreateSyntaxProvider(Discovery.IsCandidate, Discovery.Classify)
            .Where(static model => model is not null)
            .WithTrackingName(TrackingNames.Classes);

        var toolsets = classes
            .Select(static (model, _) => model!.Toolset)
            .Where(static toolset => toolset is not null)
            .Select(static (toolset, _) => toolset!)
            .WithTrackingName(TrackingNames.Toolsets);

        context.RegisterSourceOutput(toolsets, static (output, toolset) => EmitToolset(output, toolset));
    }

    private static void EmitToolset(SourceProductionContext output, ToolsetModel toolset)
    {
        foreach (var diagnostic in toolset.Diagnostics)
        {
            output.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (toolset.IsPartial) output.AddSource(Names.HintName(toolset.Type.FullName), ToolsetEmitter.Emit(toolset));
    }
}
