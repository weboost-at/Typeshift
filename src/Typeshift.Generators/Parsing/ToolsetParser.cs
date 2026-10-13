using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Generators.Parsing;

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

        var tools = ConstructorChains.GetInvocations(toolset, compilation, ct)
            .Where(IsAddTool)
            .Select(addTool => ReadTool(addTool, diagnostics))
            .OfType<DeclaredTool>()
            .Select(tool => ParseTool(tool, toolset, documented, diagnostics, ct))
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

                case "WithSettings" or "WithConfigurableSettings":
                    tool.SettingsType = call.TargetMethod.TypeArguments[0];
                    tool.SettingsCall = call;
                    tool.ConfigurableByDefault = call.TargetMethod.Name == "WithConfigurableSettings";
                    break;

                case "Configurable":
                    tool.Picks.Add(call);
                    break;

                case "NotConfigurable":
                    tool.Exclusions.Add(ConstructorChains.GetPickedProperty(call)?.Name);
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

        var settings = tool.SettingsCall is null ? null : ParseSettings(tool, toolset, diagnostics, ct);
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
            .Select(parameter => ParseParameter(parameter, tool.SettingsType, docs))
            .ToEquatableArray();

        var returns = ParseReturn(tool.Method.ReturnType);

        if (settings is not null && parameters.All(parameter => parameter.Kind != ParameterKind.Settings))
        {
            diagnostics.Report(
                Descriptors.UnreadSettings,
                ConstructorChains.GetCalledName(tool.SettingsCall!),
                name,
                tool.SettingsType!.Name);
        }

        return new ToolModel(
            Name: name,
            Method: tool.Method.Name,
            Target: tool.Method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Description: description,
            Settings: settings,
            Parameters: parameters,
            Return: returns,
            Result: GetResultType(tool.Method.ReturnType, returns)?.ToTypeOf(),
            Location: LocationInfo.From(tool.AddTool.Syntax.GetLocation()));
    }

    // Shared with the reader of referenced toolsets, which reads no docs. An obsolete member that is not configurable
    // stays out, so no generated member uses it and the consumer gets no CS0618
    public static SettingsModel CreateSettings(
        ITypeSymbol type,
        ICollection<string> configurable,
        Func<IPropertySymbol, string?> summary) =>
        new(
            type.ToTypeOf(),
            type.GetSettableProperties()
                .Where(property => configurable.Contains(property.Name) || !IsObsolete(property))
                .Select(property => new SettingsPropertyModel(
                    Name: property.Name,
                    Type: property.Type.ToFullyQualified(),
                    NullableType: property.Type.ToNullableFullyQualified(),
                    IsConfigurable: configurable.Contains(property.Name),
                    Storage: configurable.Contains(property.Name) ? Storage.Of(property.Type) : null,
                    Summary: summary(property)))
                .ToEquatableArray());

    private static string? ReadConstant(IInvocationOperation call, string what, DiagnosticBag diagnostics)
    {
        if (ConstructorChains.GetConstantString(call) is { } text) return text;

        diagnostics.Report(Descriptors.NotConstant, call.Arguments[0].Syntax, what);
        return null;
    }

    private static bool IsObsolete(IPropertySymbol property) =>
        property.GetAttributes().Any(attribute => attribute.AttributeClass.Is("System.ObsoleteAttribute"));

    private static SettingsModel? ParseSettings(
        DeclaredTool tool,
        INamedTypeSymbol toolset,
        DiagnosticBag diagnostics,
        CancellationToken ct)
    {
        var type = tool.SettingsType!;
        var settingsName = ConstructorChains.GetCalledName(tool.SettingsCall!);

        if (!type.IsRecord)
        {
            diagnostics.Report(Descriptors.SettingsNotRecord, settingsName, type.Name);
            return null;
        }

        var picks = tool.Picks
            .Select(pick => (Call: pick, Property: ConstructorChains.GetPickedProperty(pick)?.Name))
            .ToList();

        // D43: allowed one by one, or all but the excluded and the obsolete ones
        var configurable = type.GetSettableProperties()
            .Where(property => tool.ConfigurableByDefault
                ? !tool.Exclusions.Contains(property.Name) && !IsObsolete(property)
                : picks.Any(pick => pick.Property == property.Name))
            .ToList();

        // A public toolset exposes its settings through Tools and the generated overrides
        var exposed = configurable.Select(property => property.Type).Prepend(type);

        if (toolset.IsEffectivelyPublic() && !exposed.All(member => member.IsEffectivelyPublic()))
        {
            diagnostics.Report(Descriptors.SettingsLessAccessible, settingsName, type.Name, toolset.Name);
            return null;
        }

        if (configurable.FirstOrDefault(property => Storage.Of(property.Type) is null) is { } unsupported)
        {
            diagnostics.Report(
                Descriptors.UnsupportedConfigurableType,
                ConstructorChains.GetCalledName(
                    picks.FirstOrDefault(pick => pick.Property == unsupported.Name).Call ?? tool.SettingsCall!),
                unsupported.Name,
                unsupported.Type.ToDisplayString());

            return null;
        }

        return CreateSettings(
            type,
            [.. configurable.Select(property => property.Name)],
            property => XmlDocs.Read(property, ct).Summary);
    }

    private static ParameterModel ParseParameter(IParameterSymbol parameter, ITypeSymbol? settings, XmlDocs docs)
    {
        var kind = parameter.Type switch
        {
            // D40: the settings are bound like the services, never by the model
            var type when SymbolEqualityComparer.Default.Equals(type, settings) => ParameterKind.Settings,
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

    private static ITypeSymbol? GetResultType(ITypeSymbol type, ReturnKind returns) =>
        returns switch
        {
            ReturnKind.Value => type,
            ReturnKind.AwaitableValue => ((INamedTypeSymbol)type).TypeArguments[0],
            _ => null
        };

    // What one AddTool chain declares, before it is checked
    private sealed class DeclaredTool(IMethodSymbol method, IInvocationOperation addTool)
    {
        public IMethodSymbol Method { get; } = method;

        public IInvocationOperation AddTool { get; } = addTool;

        public string? Name { get; set; }

        public IInvocationOperation? NameCall { get; set; }

        public string? Description { get; set; }

        public ITypeSymbol? SettingsType { get; set; }

        public IInvocationOperation? SettingsCall { get; set; }

        public List<IInvocationOperation> Picks { get; } = [];

        public bool ConfigurableByDefault { get; set; }

        public List<string?> Exclusions { get; } = [];
    }
}
