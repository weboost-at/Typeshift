using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agentic.Generators.Diagnostics;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Agentic.Generators.Parsing;

internal static class ToolsetParser
{
    public static ToolsetModel Parse(
        INamedTypeSymbol toolset,
        Compilation compilation,
        bool documented,
        CancellationToken ct)
    {
        var diagnostics = new DiagnosticBag();
        var isPartial = toolset.IsPartial(ct);

        if (!isPartial) diagnostics.Report(Descriptors.NotPartial, toolset.Locations.FirstOrDefault(), toolset.Name);

        var declared = ConstructorChains.GetInvocations(toolset, compilation, ct)
            .Where(IsAddTool)
            .Select(addTool => ReadTool(addTool, diagnostics))
            .OfType<DeclaredTool>()
            .ToList();

        var settingsTypes = declared
            .Select(tool => tool.SettingsType)
            .OfType<ITypeSymbol>()
            .ToList();

        var tools = declared
            .Select(tool => ParseTool(tool, toolset, settingsTypes, documented, diagnostics, ct))
            .OfType<ToolModel>()
            .ToEquatableArray();

        return new ToolsetModel(TypeModels.From(toolset), isPartial, tools, diagnostics.ToEquatableArray());
    }

    private static bool IsAddTool(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "AddTool" && invocation.TargetMethod.ContainingType.Is(KnownTypes.Toolset);

    private static DeclaredTool? ReadTool(IInvocationOperation addTool, DiagnosticBag diagnostics)
    {
        var argument = addTool.Arguments[0].Value;

        if (ConstructorChains.Unwrap(argument) is not IDelegateCreationOperation
            {
                Target: IMethodReferenceOperation { Method.IsStatic: true } reference
            })
        {
            diagnostics.Report(Descriptors.NotStaticMethodGroup, argument.Syntax, argument.Syntax.ToString());
            return null;
        }

        var tool = new DeclaredTool(reference.Method, addTool);

        foreach (var call in ConstructorChains.GetChainedCalls(addTool))
        {
            switch (call.TargetMethod.Name)
            {
                case "WithName":
                    tool.Name = ReadConstant(call, "name", diagnostics);
                    tool.NameCall = call;
                    break;

                case "WithDescription":
                    tool.Description = ReadConstant(call, "description", diagnostics);
                    break;

                case "WithSettings":
                    tool.SettingsType = call.TargetMethod.TypeArguments[0];
                    tool.SettingsCall = call;
                    break;
            }
        }

        if (tool.NameCall is null)
            diagnostics.Report(Descriptors.MissingToolName, addTool.Syntax, reference.Method.Name);

        return tool;
    }

    private static ToolModel? ParseTool(
        DeclaredTool tool,
        INamedTypeSymbol toolset,
        IReadOnlyList<ITypeSymbol> settingsTypes,
        bool documented,
        DiagnosticBag diagnostics,
        CancellationToken ct)
    {
        if (tool.Name is not { } name) return null;

        if (!LowerSnakeCase.Matches(name))
        {
            diagnostics.Report(Descriptors.NotLowerSnakeCase, tool.NameCall!.Arguments[0].Syntax, name);
            return null;
        }

        var settings = tool.SettingsCall is { } call
            ? ParseSettings(call, tool.SettingsType!, toolset, diagnostics, ct)
            : null;
        if (tool.SettingsCall is not null && settings is null) return null;

        var docs = XmlDocs.Read(tool.Method, ct);
        var description = tool.Description ?? docs.Summary;

        // Without generated docs a summary is lost unless WithDescription replaces it, a <param> always is
        var unread = documented ? "" : XmlDocs.ReadUnparsed(tool.Method, ct);

        if (unread.Contains("<param ") || (tool.Description is null && unread.Length > 0))
            diagnostics.Report(Descriptors.DocumentationOff, tool.AddTool.Syntax, name);
        else if (description is null)
            diagnostics.Report(Descriptors.MissingDescription, tool.AddTool.Syntax, name);

        var parameters = tool.Method.Parameters
            .Select(parameter => ParseParameter(parameter, docs))
            .ToEquatableArray();

        var settingsParameters = tool.Method.Parameters
            .Where(parameter => settingsTypes.Any(type => IsSameType(type, parameter.Type)));

        foreach (var parameter in settingsParameters)
        {
            diagnostics.Report(
                Descriptors.SettingsAsParameter,
                parameter.Locations.FirstOrDefault(),
                parameter.Name,
                name,
                parameter.Type.Name);
        }

        return new ToolModel(
            Name: name,
            Method: tool.Method.Name,
            Target: tool.Method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Description: description,
            Settings: settings,
            Parameters: parameters,
            Return: ParseReturn(tool.Method.ReturnType),
            Location: LocationInfo.From(tool.AddTool.Syntax.GetLocation()));
    }

    private static string? ReadConstant(IInvocationOperation call, string what, DiagnosticBag diagnostics)
    {
        if (ConstructorChains.GetConstantString(call) is { } text) return text;

        diagnostics.Report(Descriptors.NotConstant, call.Arguments[0].Syntax, what);
        return null;
    }

    private static bool IsSameType(ITypeSymbol type, ITypeSymbol parameter) =>
        SymbolEqualityComparer.Default.Equals(type, parameter.WithNullableAnnotation(NullableAnnotation.NotAnnotated));

    private static SettingsModel? ParseSettings(
        IInvocationOperation call,
        ITypeSymbol type,
        INamedTypeSymbol toolset,
        DiagnosticBag diagnostics,
        CancellationToken ct)
    {
        if (!type.IsRecord)
        {
            diagnostics.Report(Descriptors.SettingsNotRecord, ConstructorChains.GetCalledName(call), type.Name);
            return null;
        }

        var picked = ConstructorChains.GetPickedProperties(call, KnownTypes.ConfigurableSettingsPicker).ToList();

        var settable = type.GetSettableProperties().ToList();

        // A public toolset exposes its settings through Tools and the generated overrides
        var exposed = settable.Where(property => picked.Contains(property.Name)).Select(property => property.Type);

        if (toolset.IsEffectivelyPublic() && !exposed.Prepend(type).All(member => member.IsEffectivelyPublic()))
        {
            diagnostics.Report(
                Descriptors.SettingsLessAccessible,
                ConstructorChains.GetCalledName(call),
                type.Name,
                toolset.Name);

            return null;
        }

        var unsupported = settable.FirstOrDefault(property =>
            picked.Contains(property.Name) && Storage.Of(property.Type) is null);

        if (unsupported is not null)
        {
            diagnostics.Report(
                Descriptors.UnsupportedConfigurableType,
                ConstructorChains.GetCalledName(call),
                unsupported.Name,
                unsupported.Type.ToDisplayString());

            return null;
        }

        var properties = settable
            .Select(property => new SettingsPropertyModel(
                Name: property.Name,
                NullableType: property.Type.ToNullableFullyQualified(),
                IsConfigurable: picked.Contains(property.Name),
                Storage: picked.Contains(property.Name) ? Storage.Of(property.Type) : null,
                Summary: XmlDocs.Read(property, ct).Summary))
            .ToEquatableArray();

        return new SettingsModel(type.ToTypeOf(), properties);
    }

    private static ParameterModel ParseParameter(IParameterSymbol parameter, XmlDocs docs)
    {
        var kind = parameter.Type switch
        {
            var type when type.Is(KnownTypes.ServiceProvider) => ParameterKind.Services,
            var type when type.Is(KnownTypes.CancellationToken) => ParameterKind.CancellationToken,
            var type when type.Is(KnownTypes.FunctionArguments) => ParameterKind.Arguments,
            _ => ParameterKind.Argument
        };

        return new ParameterModel(
            Name: parameter.Name,
            Kind: kind,
            Type: parameter.Type.ToFullyQualified(),
            TypeOf: parameter.Type.ToTypeOf(),
            IsNullable: parameter.Type is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated },
            HasDefaultValue: parameter.HasExplicitDefaultValue,
            DefaultValue: parameter.HasExplicitDefaultValue ? DefaultValues.Format(parameter) : null,
            Description: docs.Parameters.TryGetValue(parameter.Name, out var description) ? description : null);
    }

    private static ReturnKind ParseReturn(ITypeSymbol type) =>
        type switch
        {
            { SpecialType: SpecialType.System_Void } => ReturnKind.Void,
            _ when type.Is(KnownTypes.Task) || type.Is(KnownTypes.ValueTask) => ReturnKind.Awaitable,
            _ when type.Is(KnownTypes.TaskOfT) || type.Is(KnownTypes.ValueTaskOfT) => ReturnKind.AwaitableValue,
            _ => ReturnKind.Value
        };

    // What one AddTool chain declares, before it is checked against the toolset's other tools
    private sealed class DeclaredTool(IMethodSymbol method, IInvocationOperation addTool)
    {
        public IMethodSymbol Method { get; } = method;

        public IInvocationOperation AddTool { get; } = addTool;

        public string? Name { get; set; }

        public IInvocationOperation? NameCall { get; set; }

        public string? Description { get; set; }

        public ITypeSymbol? SettingsType { get; set; }

        public IInvocationOperation? SettingsCall { get; set; }
    }
}
