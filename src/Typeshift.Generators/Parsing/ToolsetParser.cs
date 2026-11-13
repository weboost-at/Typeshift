using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Generators.Parsing;

internal static class ToolsetParser
{
    // Null for a toolset the generator cannot extend; its tools are still parsed, so every mistake is reported
    public static ToolsetModel? Parse(
        INamedTypeSymbol toolset,
        IEnumerable<IOperation> constructors,
        bool documented,
        Reporter reporter,
        CancellationToken ct) =>
        Parse(toolset, constructors, documented, reporter, ct, out _);

    // The analyzer also needs each tool's declaration, to report names that clash across toolsets
    public static ToolsetModel? Parse(
        INamedTypeSymbol toolset,
        IEnumerable<IOperation> constructors,
        bool documented,
        Reporter reporter,
        CancellationToken ct,
        out IReadOnlyList<(ToolModel Tool, IInvocationOperation AddTool)> declarations)
    {
        var notPartial = toolset.GetNotPartial(ct);

        if (notPartial is not null)
            reporter.Report(Descriptors.NotPartial, notPartial.Locations.FirstOrDefault(), notPartial.Name);

        var unreachable = toolset.GetUnreachableReason();
        var isReachable = unreachable is null;

        if (unreachable is not null)
            reporter.Report(Descriptors.Unreachable, toolset.Locations.FirstOrDefault(), toolset.Name, unreachable);

        declarations = ParseTools(toolset, constructors, documented, reporter, ct);
        var tools = declarations.Select(declaration => declaration.Tool).ToEquatableArray();

        // A partial in another file would declare another class than a file-local one
        return notPartial is null && !toolset.IsFileLocalOrNestedInOne()
            ? new ToolsetModel(TypeModels.From(toolset), isReachable, tools, toolset.GetExperimentalIds())
            : null;
    }

    // The tools a toolset or an agent declares in its constructors; a tool that clashes is reported and left out
    public static IReadOnlyList<(ToolModel Tool, IInvocationOperation AddTool)> ParseTools(
        INamedTypeSymbol owner,
        IEnumerable<IOperation> constructors,
        bool documented,
        Reporter reporter,
        CancellationToken ct)
    {
        var declared = new List<(ToolModel Tool, IInvocationOperation AddTool)>();

        foreach (var addTool in ConstructorChains.GetInvocations(constructors).Where(IsAddTool))
        {
            if (ParseTool(addTool, owner, documented, reporter, ct) is not { } tool) continue;

            var shared = declared
                .Select(offered => Names.SharedMember(offered.Tool.Name, tool.Name))
                .FirstOrDefault(name => name is not null);

            if (shared is not null)
            {
                reporter.Report(Descriptors.DuplicateToolName, addTool.Syntax, tool.Name, shared);
                continue;
            }

            // A class cannot hold a member of its own name
            if (GetGeneratedMembers(tool).Contains(owner.Name))
            {
                reporter.Report(
                    Descriptors.GeneratedNameClash,
                    owner.Locations.FirstOrDefault(),
                    $"The class '{owner.Name}'",
                    tool.Name);
                continue;
            }

            if (ClashingMember(owner, tool) is { } member)
            {
                reporter.Report(
                    Descriptors.GeneratedNameClash,
                    member.Locations.FirstOrDefault(),
                    $"The member '{member.Name}'",
                    tool.Name);
                continue;
            }

            declared.Add((tool, addTool));
        }

        return declared;
    }

    public static bool IsAddTool(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "AddTool"
        && (invocation.TargetMethod.ContainingType.Is(KnownTypes.Toolset)
            || invocation.TargetMethod.ContainingType.Is(KnownTypes.Agent));

    // The analyzer sees the generated members too; only the user's own can clash
    private static ISymbol? ClashingMember(INamedTypeSymbol owner, ToolModel tool) =>
        GetGeneratedMembers(tool)
            .SelectMany(name => owner.GetMembers(name))
            .FirstOrDefault(member => member.DeclaringSyntaxReferences
                .Any(reference => !Names.IsGenerated(reference.SyntaxTree)));

    private static IEnumerable<string> GetGeneratedMembers(ToolModel tool) =>
        Names.ToolsetMembers(tool.Name, tool.Settings?.HasConfigurable is true);

    private static ToolModel? ParseTool(
        IInvocationOperation addTool,
        INamedTypeSymbol owner,
        bool documented,
        Reporter reporter,
        CancellationToken ct)
    {
        var argument = addTool.Arguments[0].Value;

        // A static local function is no method the generated code can call, nor is a generic one it would have to
        // close, by itself or as a member of a generic extension block, nor one that takes __arglist
        if (ConstructorChains.Unwrap(argument) is not IDelegateCreationOperation
            {
                Target: IMethodReferenceOperation
                {
                    Method:
                    {
                        IsStatic: true,
                        MethodKind: MethodKind.Ordinary,
                        IsGenericMethod: false,
                        IsVararg: false
                    } method
                }
            }
            || method.ContainingType is { IsExtension: true, IsGenericType: true })
        {
            reporter.Report(
                Descriptors.NotStaticMethodGroup,
                argument.Syntax,
                argument.Syntax.ToString(),
                "pass a static method that is not generic, AddTool(Method)");

            return null;
        }

        // A static member of a C# 14 extension block is called on the class that declares the block
        var target = method.ContainingType;

        while (target.IsExtension)
        {
            target = target.ContainingType;
        }

        // A file-local owner is reported itself (TSH016), and generates nothing that would call the method
        if (target.IsFileLocalOrNestedInOne() && !owner.IsFileLocalOrNestedInOne())
        {
            reporter.Report(
                Descriptors.NotStaticMethodGroup,
                argument.Syntax,
                argument.Syntax.ToString(),
                $"its class '{target.Name}' is file-local, and generated code is written to other files");

            return null;
        }

        // A static abstract or virtual member of an interface is the compiler's CS8926 where AddTool names it, and a
        // member of a generic class without its type argument, AddTool(Finder<>.Find) while it is typed, its CS0305
        if (method.IsAbstract || method.IsVirtual || method.ContainingType.ContainsTypeParameter()) return null;

        if (!ConstructorChains.EndsInStatement(addTool))
        {
            reporter.Report(Descriptors.UnreadableChain, addTool.Syntax, "tool chain");
            return null;
        }

        if (!ConstructorChains.IsTopLevelStatement(addTool))
        {
            reporter.Report(Descriptors.OutsideConstructor, ConstructorChains.GetCalledName(addTool), "AddTool");
            return null;
        }

        var chain = ReadChain(addTool, reporter);
        if (chain is null) return null;

        var name = chain.Name ?? ToolNames.FromMethod(method.Name);

        if (!ToolNames.IsLowerSnakeCase(name))
        {
            reporter.Report(
                Descriptors.NotLowerSnakeCase,
                chain.NameCall?.Arguments[0].Syntax ?? addTool.Arguments[0].Syntax,
                name);

            return null;
        }

        var settings = chain.SettingsCall is null ? null : ParseSettings(chain, owner, reporter, ct);
        if (chain.SettingsCall is not null && settings is null) return null;

        // WithDescription("") describes as little as no call
        var docs = XmlDocs.Read(method, ct);
        var stated = string.IsNullOrWhiteSpace(chain.Description) ? null : chain.Description;
        var description = stated ?? docs.Summary;

        // Without generated docs a summary is lost unless WithDescription replaces it, a <param> always is
        var unread = documented ? "" : XmlDocs.ReadUnparsed(method, ct);

        if (unread.Contains("<param ") || (stated is null && unread.Length > 0))
            reporter.Report(Descriptors.DocumentationOff, addTool.Syntax, $"The tool '{name}'");
        else if (description is null)
            reporter.Report(
                Descriptors.MissingDescription,
                chain.DescriptionCall?.Arguments[0].Syntax ?? addTool.Syntax,
                name);

        // Generated code would name a type that does not resolve yet, or a file-local one in the signature of a class
        // that is not (CS9051), and repeat the compiler's error
        if (IsCompilersToReport(method.ReturnType) || method.Parameters.Any(each => IsCompilersToReport(each.Type)))
            return null;

        var parameters = new List<ParameterModel>();

        foreach (var parameter in method.Parameters)
        {
            if (GetUnboundReason(parameter) is { } unbound)
            {
                // A method of a reference has no source here
                var declared = parameter.Locations.FirstOrDefault(location => location.IsInSource);

                reporter.Report(
                    Descriptors.NotBindable,
                    declared ?? argument.Syntax.GetLocation(),
                    $"parameter '{parameter.Name}'",
                    name,
                    "bound from the model's arguments",
                    unbound);

                return null;
            }

            parameters.Add(ParseParameter(parameter, chain.SettingsType, docs));
        }

        if (GetUnreturnedReason(method.ReturnType) is { } unreturned)
        {
            reporter.Report(
                Descriptors.NotBindable,
                GetReturnType(method, ct)?.GetLocation() ?? argument.Syntax.GetLocation(),
                "result",
                name,
                "returned to the model",
                unreturned);

            return null;
        }

        if (settings is not null && parameters.All(parameter => parameter.Kind != ParameterKind.Settings))
        {
            reporter.Report(
                Descriptors.UnreadSettings,
                ConstructorChains.GetCalledName(chain.SettingsCall!),
                name,
                chain.SettingsType!.Name);
        }

        var returns = ParseReturn(method.ReturnType);
        ISymbol[] named = [method, method.ReturnType, .. method.Parameters.Select(parameter => parameter.Type)];

        // The target's type arguments keep their nullable annotations, as the method's parameters expect them
        return new ToolModel(
            Name: name,
            Method: Names.Identifier(method.Name),
            Target: target.ToFullyQualified(),
            Description: description,
            IsReadOnly: chain.IsReadOnly,
            RequiresApproval: chain.RequiresApproval,
            Settings: settings,
            Parameters: parameters.ToEquatableArray(),
            Return: returns,
            Result: GetResultType(method.ReturnType, returns),
            ExperimentalIds: named.GetExperimentalIds());
    }

    private static ToolChain? ReadChain(IInvocationOperation addTool, Reporter reporter)
    {
        var chain = new ToolChain();
        var valid = true;

        foreach (var call in ConstructorChains.GetChainedCalls(addTool))
        {
            switch (call.TargetMethod.Name)
            {
                case "WithName":
                    chain.Name = ReadConstant(call, "tool name", reporter);
                    chain.NameCall = call;
                    valid &= chain.Name is not null;
                    break;

                case "WithDescription":
                    chain.Description = ReadConstant(call, "tool description", reporter);
                    chain.DescriptionCall = call;
                    valid &= chain.Description is not null;
                    break;

                case "RequiresApproval":
                    chain.RequiresApproval = true;
                    break;

                case "ReadOnly":
                    chain.IsReadOnly = true;
                    break;

                case "WithSettings" or "WithConfigurableSettings":
                    chain.SettingsType = call.TargetMethod.TypeArguments[0];
                    chain.SettingsCall = call;
                    chain.ConfigurableByDefault = call.TargetMethod.Name == "WithConfigurableSettings";
                    break;

                case "Configurable" or "NotConfigurable":
                    if (ConstructorChains.GetPickedProperty(call) is { } property)
                    {
                        chain.Picks.Add((call, property));
                        break;
                    }

                    // No pick binds while the settings type does not resolve, which the compiler reports
                    if (chain.SettingsType?.ContainsErrorType() is not true)
                        reporter.Report(Descriptors.UnreadableChain, call.Arguments[0].Syntax, "settings pick");

                    valid = false;
                    break;
            }
        }

        return valid ? chain : null;
    }

    private static string? ReadConstant(IInvocationOperation call, string what, Reporter reporter)
    {
        if (ConstructorChains.GetConstantString(call) is { } text) return text;

        reporter.Report(Descriptors.NotConstant, call.Arguments[0].Syntax, what);
        return null;
    }

    private static SettingsModel? ParseSettings(
        ToolChain chain,
        INamedTypeSymbol owner,
        Reporter reporter,
        CancellationToken ct)
    {
        var type = chain.SettingsType!;
        var settingsName = ConstructorChains.GetCalledName(chain.SettingsCall!);

        // A type that does not resolve yet is the compiler's to report, here and for a member below
        if (type.ContainsErrorType()) return null;

        if (!type.IsRecord)
        {
            reporter.Report(Descriptors.SettingsNotRecord, settingsName, type.Name);
            return null;
        }

        // The overrides, the factories and the agents name the settings wherever the toolset or the agent can be
        // named; a file-local owner is named nowhere else (TSH016)
        var reachable = owner.IsFileLocalOrNestedInOne()
            || (owner.IsEffectivelyPublic() ? type.IsEffectivelyPublic() : type.IsVisibleInAssembly());

        if (!reachable)
        {
            reporter.Report(Descriptors.SettingsLessAccessible, settingsName, type.Name, owner.Name);
            return null;
        }

        var picked = chain.Picks.Where(pick => pick.Call.TargetMethod.Name == "Configurable").ToList();
        var excluded = chain.Picks.Where(pick => pick.Call.TargetMethod.Name == "NotConfigurable").ToList();

        if (picked.FirstOrDefault(pick => !pick.Property.IsSettable) is { Call: { } call, Property: { } readOnly })
        {
            reporter.Report(Descriptors.NotSettable, call.Arguments[0].Syntax, readOnly.Name);
            return null;
        }

        // D18: allowed one by one, or all but the excluded and the obsolete ones
        bool IsConfigurable(IPropertySymbol property) =>
            chain.ConfigurableByDefault
                ? excluded.All(pick => pick.Property.Name != property.Name) && !property.IsObsolete
                : picked.Any(pick => pick.Property.Name == property.Name);

        // An obsolete member that is not configurable stays out, so no generated member uses it and the consumer
        // gets no CS0618
        var properties = new List<SettingsPropertyModel>();

        foreach (var property in type.GetPublicProperties().Where(property => property.IsSettable))
        {
            var configurable = IsConfigurable(property);
            if (!configurable && property.IsObsolete) continue;

            // D19: what a store keeps; a pick names the member, a deny list the call that allowed it
            var storage = configurable ? Storage.Of(property.Type) : null;

            if (configurable && storage is null)
            {
                if (property.Type.ContainsErrorType()) return null;

                var pick = picked.FirstOrDefault(candidate => candidate.Property.Name == property.Name).Call;
                var location = pick?.Arguments[0].Syntax ?? settingsName;

                reporter.Report(Descriptors.NotStorable, location, property.Name);
                return null;
            }

            properties.Add(new SettingsPropertyModel(
                Name: property.Name,
                Type: property.Type.ToFullyQualified(),
                NullableType: property.Type.ToNullableFullyQualified(),
                Storage: storage,
                Summary: XmlDocs.Read(property, ct).Summary));
        }

        return new SettingsModel(type.ToTypeOf(), properties.ToEquatableArray(), type.GetSettingsExperimentalIds());
    }

    private static bool IsCompilersToReport(ITypeSymbol type) =>
        type.ContainsErrorType() || type.ContainsFileLocalType();

    // The model's JSON fills a value; it cannot fill a reference, a pointer, a stack-only type or dynamic. Null for a
    // parameter it can fill
    private static string? GetUnboundReason(IParameterSymbol parameter) =>
        parameter.RefKind is RefKind.None or RefKind.In
            ? GetUnwrittenReason(parameter.Type, "take a JsonElement or the value's own type")
            : "it is passed by reference";

    // A task or a stream is awaited or read, so it must be there; the value it yields, or the method returns, is
    // serialized as its type. Null for a result the generated code can return
    private static string? GetUnreturnedReason(ITypeSymbol type)
    {
        var awaited = Unwrap(type);
        var kind = ParseReturn(awaited);
        var isNullable = type.NullableAnnotation == NullableAnnotation.Annotated
            || !SymbolEqualityComparer.Default.Equals(awaited, type);

        if (kind is not (ReturnKind.Void or ReturnKind.Value) && isNullable)
        {
            return "the task or the stream itself may be null; return one always and make its value nullable, as in "
                + "Task<string?>";
        }

        return kind switch
        {
            ReturnKind.Value => GetUnwrittenReason(type, "return object or the value's own type"),
            ReturnKind.AwaitableValue => GetUnwrittenReason(
                ((INamedTypeSymbol)type).TypeArguments[0],
                "return object or the value's own type"),
            _ => null
        };
    }

    // dynamic inside another type, as in List<dynamic>, is object there
    private static string? GetUnwrittenReason(ITypeSymbol type, string instead) =>
        type switch
        {
            _ when IsPointer(type) => "its type is a pointer or an array of pointers",
            { IsRefLikeType: true } => "its type is a ref struct",
            { TypeKind: TypeKind.Dynamic } => $"its type is dynamic; {instead}",
            _ => null
        };

    private static bool IsPointer(ITypeSymbol type) =>
        type is IPointerTypeSymbol or IFunctionPointerTypeSymbol
        || (type is IArrayTypeSymbol array && IsPointer(array.ElementType));

    private static ITypeSymbol Unwrap(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    // Null for a method of a reference, which has no syntax here
    private static TypeSyntax? GetReturnType(IMethodSymbol method, CancellationToken ct) =>
        method.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(ct))
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault()
            ?.ReturnType;

    private static ParameterModel ParseParameter(IParameterSymbol parameter, ITypeSymbol? settings, XmlDocs docs)
    {
        // What the call fills is told by its type, nullable or not: CancellationToken? takes the call's token too
        var kind = Unwrap(parameter.Type) switch
        {
            // D16: the settings are bound like the services, never by the model
            var type when SymbolEqualityComparer.Default.Equals(type, settings) => ParameterKind.Settings,
            var type when type.Is(KnownTypes.ServiceProvider) => ParameterKind.Services,
            var type when type.Is(KnownTypes.CancellationToken) => ParameterKind.CancellationToken,
            var type when type.Is(KnownTypes.FunctionArguments) => ParameterKind.Arguments,
            _ => ParameterKind.Argument
        };

        var defaultValue = parameter.HasExplicitDefaultValue ? DefaultValues.Format(parameter) : null;

        // The schema allows the null the method itself defaults to, with a ? or without
        return new ParameterModel(
            Name: parameter.Name,
            Kind: kind,
            Type: parameter.Type.ToFullyQualified(),
            TypeOf: parameter.Type.ToTypeOf(),
            IsNullable: parameter.Type is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated }
                || defaultValue == ParameterModel.ForgivenNull,
            DefaultValue: defaultValue,
            Description: docs.Parameters.TryGetValue(parameter.Name, out var description) ? description : null);
    }

    private static ReturnKind ParseReturn(ITypeSymbol type) =>
        type switch
        {
            { SpecialType: SpecialType.System_Void } => ReturnKind.Void,
            _ when type.Is(KnownTypes.Task) || type.Is(KnownTypes.ValueTask) => ReturnKind.Awaitable,
            _ when type.Is(KnownTypes.TaskOfT) || type.Is(KnownTypes.ValueTaskOfT) => ReturnKind.AwaitableValue,
            _ when type.Is(KnownTypes.AsyncEnumerableOfT) => ReturnKind.Stream,
            _ => ReturnKind.Value
        };

    private static string? GetResultType(ITypeSymbol type, ReturnKind returns) =>
        returns switch
        {
            ReturnKind.Value => type.ToTypeOf(),
            ReturnKind.AwaitableValue => TypeArgument(type),
            ReturnKind.Stream => $"global::System.Collections.Generic.List<{TypeArgument(type)}>",
            _ => null
        };

    private static string TypeArgument(ITypeSymbol type) => ((INamedTypeSymbol)type).TypeArguments[0].ToTypeOf();

    // What one AddTool chain declares, before it is checked
    private sealed class ToolChain
    {
        public string? Name { get; set; }

        public IInvocationOperation? NameCall { get; set; }

        public string? Description { get; set; }

        public IInvocationOperation? DescriptionCall { get; set; }

        public bool RequiresApproval { get; set; }

        public bool IsReadOnly { get; set; }

        public ITypeSymbol? SettingsType { get; set; }

        public IInvocationOperation? SettingsCall { get; set; }

        public bool ConfigurableByDefault { get; set; }

        public List<(IInvocationOperation Call, IPropertySymbol Property)> Picks { get; } = [];
    }
}
