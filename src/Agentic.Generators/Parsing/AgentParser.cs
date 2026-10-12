using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agentic.Generators.Diagnostics;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Agentic.Generators.Parsing;

// Toolsets stay names here and a later step resolves them, so a toolset edit reaches the agents using it
internal static class AgentParser
{
    public static AgentModel Parse(INamedTypeSymbol agent, Compilation compilation, CancellationToken ct)
    {
        var diagnostics = new DiagnosticBag();
        var isPartial = agent.IsPartial(ct);
        var location = agent.Locations.FirstOrDefault();
        var options = new List<ChatOptionModel>();
        var uses = new List<ToolsetUseModel>();
        var toolSettings = new List<ToolSettingsUseModel>();
        string? uniqueName = null;
        var hasNameCall = false;

        if (!isPartial) diagnostics.Report(Descriptors.NotPartial, location, agent.Name);

        var calls = ConstructorChains.GetInvocations(agent, compilation, ct)
            .Where(invocation => invocation.TargetMethod.ContainingType.Is(KnownTypes.AgentDefinition));

        foreach (var call in calls)
        {
            switch (call.TargetMethod.Name)
            {
                case "WithUniqueName":
                    uniqueName = ReadUniqueName(agent, call, diagnostics);
                    hasNameCall = true;
                    break;

                case "WithChatOptions":
                    options.AddRange(ReadConfigurableOptions(call, compilation));
                    break;

                case "UseToolset":
                    uses.Add(ReadUse(call, diagnostics));
                    break;

                case "WithToolSettings":
                    toolSettings.Add(ReadToolSettings(call, uses.Count));
                    break;
            }
        }

        if (!hasNameCall) diagnostics.Report(Descriptors.InvalidUniqueName, location, agent.Name);

        return new AgentModel(
            Type: TypeModels.From(agent),
            IsPartial: isPartial,
            UniqueName: uniqueName,
            ConfigurableOptions: options.ToEquatableArray(),
            Uses: uses.ToEquatableArray(),
            ToolSettings: toolSettings.ToEquatableArray(),
            Diagnostics: diagnostics.ToEquatableArray(),
            Location: LocationInfo.From(location));
    }

    private static string? ReadUniqueName(INamedTypeSymbol agent, IInvocationOperation call, DiagnosticBag diagnostics)
    {
        if (ConstructorChains.GetConstantString(call) is { } name && LowerSnakeCase.Matches(name)) return name;

        diagnostics.Report(Descriptors.InvalidUniqueName, call.Arguments[0].Syntax, agent.Name);
        return null;
    }

    private static IEnumerable<ChatOptionModel> ReadConfigurableOptions(
        IInvocationOperation call,
        Compilation compilation)
    {
        var chatOptions = compilation.GetTypeByMetadataName(KnownTypes.ChatOptions);

        return ConstructorChains.GetPickedProperties(call, KnownTypes.ConfigurableSettingsPicker)
            .Select(name => chatOptions?.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault())
            .OfType<IPropertySymbol>()
            .Select(property => new ChatOptionModel(
                property.Name,
                property.Type.ToFullyQualified(),
                property.Type.ToNullableFullyQualified()));
    }

    private static ToolsetUseModel ReadUse(IInvocationOperation call, DiagnosticBag diagnostics)
    {
        var toolset = call.TargetMethod.TypeArguments[0];
        var selected = new List<SelectedMethodModel>();

        var arguments = call.Arguments
            .SelectMany(argument => argument.Value is IArrayCreationOperation { Initializer: { } initializer }
                ? initializer.ElementValues
                : [argument.Value]);

        foreach (var argument in arguments)
        {
            if (GetMethodGroup(argument) is { } method)
                selected.Add(new SelectedMethodModel(method.Name, LocationInfo.From(argument.Syntax.GetLocation())));
            else
                diagnostics.Report(
                    Descriptors.UnknownTool,
                    argument.Syntax,
                    $"{argument.Syntax} is no tool of {toolset.Name}");
        }

        return new ToolsetUseModel(
            Toolset: toolset.ToTypeOf(),
            IsPublic: toolset.IsEffectivelyPublic(),
            All: call.Arguments.Length == 0,
            Methods: selected.ToEquatableArray(),
            Location: LocationInfo.From(ConstructorChains.GetCalledName(call).GetLocation()));
    }

    private static IMethodSymbol? GetMethodGroup(IOperation argument) =>
        ConstructorChains.Unwrap(argument) is IDelegateCreationOperation { Target: IMethodReferenceOperation reference }
            ? reference.Method
            : null;

    private static ToolSettingsUseModel ReadToolSettings(IInvocationOperation call, int usesBefore)
    {
        var argument = ConstructorChains.Unwrap(call.Arguments[0].Value);

        return new ToolSettingsUseModel(
            argument.Type?.ToTypeOf() ?? "",
            usesBefore,
            LocationInfo.From(call.Arguments[0].Syntax.GetLocation()));
    }
}
