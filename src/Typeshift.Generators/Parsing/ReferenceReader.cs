using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Parsing;

// D7: the modules of the referenced assemblies and the tools their toolsets stamp
internal static class ReferenceReader
{
    // Raised whenever the stamps change shape; a reference with another version is TSH020
    public const int StampVersion = 2;

    public static ReferenceModel Read(Compilation compilation, CancellationToken ct)
    {
        var modules = new List<string>();
        var toolsets = new List<ToolsetSummary>();

        foreach (var assembly in GetLibraryUsers(compilation))
        {
            ct.ThrowIfCancellationRequested();

            if (GetModule(assembly) is not { } module) continue;

            modules.Add(module.ToTypeOf());
            toolsets.AddRange(GetToolsets(module).Select(ReadToolset));
        }

        return new ReferenceModel(modules.ToEquatableArray(), toolsets.ToEquatableArray());
    }

    // TSH020: the references that use the library, declare toolsets or agents, and carry no module of this version
    public static IEnumerable<IAssemblySymbol> GetUnstamped(Compilation compilation, CancellationToken ct) =>
        GetLibraryUsers(compilation)
            .Where(assembly => GetModule(assembly) is null && DeclaresToolsetsOrAgents(assembly.GlobalNamespace, ct));

    // TSH021: the id type a reference was built with, when it differs from the build's
    public static IEnumerable<(IAssemblySymbol Assembly, ITypeSymbol IdType)> GetOtherIdTypes(
        Compilation compilation,
        ITypeSymbol? idType) =>
        GetLibraryUsers(compilation)
            .Select(assembly => (Assembly: assembly, Stamp: GetModuleStamp(assembly)))
            .Where(stamped => stamped.Stamp?.ConstructorArguments[2].Value is ITypeSymbol other
                && !SymbolEqualityComparer.Default.Equals(other, idType))
            .Select(stamped => (stamped.Assembly, (ITypeSymbol)stamped.Stamp!.ConstructorArguments[2].Value!));

    // D39: the kinds of the referenced agents' stored settings
    public static IEnumerable<(string Kind, string Agent)> GetAgentKinds(Compilation compilation) =>
        GetAgentStamps(compilation)
            .Select(attribute => (
                (string)attribute.ConstructorArguments[1].Value!,
                ((ITypeSymbol)attribute.ConstructorArguments[0].Value!).ToDisplayString()));

    // D33, D34: the referenced agents as their modules stamp them, for the agents that use them
    public static IEnumerable<ModuleAgent> GetAgents(Compilation compilation) =>
        GetAgentStamps(compilation)
            .Select(ReadAgent);

    private static IEnumerable<AttributeData> GetAgentStamps(Compilation compilation) =>
        GetLibraryUsers(compilation)
            .Select(GetModule)
            .OfType<INamedTypeSymbol>()
            .SelectMany(module => module.GetAttributes())
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.ModuleAgentAttribute));

    private static ModuleAgent ReadAgent(AttributeData stamp)
    {
        var arguments = stamp.ConstructorArguments;

        var approvalTools = arguments[3].Values
            .Select(tool => (string)tool.Value!)
            .ToEquatableArray();

        return new ModuleAgent(
            Type: ((ITypeSymbol)arguments[0].Value!).ToTypeOf(),
            Kind: (string)arguments[1].Value!,
            Name: (string)arguments[2].Value!,
            Described: stamp.NamedArguments.Any(argument => argument is { Key: "Described", Value.Value: true }),
            ApprovalTools: approvalTools);
    }

    public static INamedTypeSymbol? GetModule(IAssemblySymbol assembly) =>
        GetModuleStamp(assembly)?.ConstructorArguments[0].Value as INamedTypeSymbol;

    private static AttributeData? GetModuleStamp(IAssemblySymbol assembly) =>
        assembly.GetAttributes()
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.GeneratedModuleAttribute))
            .FirstOrDefault(attribute => attribute.ConstructorArguments.Length == 3
                && attribute.ConstructorArguments[1].Value is StampVersion);

    // Every module stamps its toolsets, so the toolset's own assembly is never searched
    private static IEnumerable<INamedTypeSymbol> GetToolsets(INamedTypeSymbol module) =>
        module.GetAttributes()
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.ModuleToolsetAttribute))
            .Select(attribute => attribute.ConstructorArguments[0].Value)
            .OfType<INamedTypeSymbol>();

    private static ToolsetSummary ReadToolset(INamedTypeSymbol toolset) =>
        new(
            toolset.ToTypeOf(),
            toolset.IsEffectivelyPublic(),
            toolset.GetAttributes()
                .Where(attribute => attribute.AttributeClass.Is(KnownTypes.GeneratedToolAttribute))
                .Select(ReadTool)
                .ToEquatableArray());

    private static ToolSummary ReadTool(AttributeData stamp)
    {
        var arguments = stamp.ConstructorArguments;
        var named = stamp.NamedArguments.ToDictionary(argument => argument.Key, argument => argument.Value.Value);
        var settings = named.TryGetValue("Settings", out var type) ? type as ITypeSymbol : null;
        var overrides = named.TryGetValue("Overrides", out var other) ? other as ITypeSymbol : null;

        return new ToolSummary(
            Name: (string)arguments[0].Value!,
            Property: (string)arguments[1].Value!,
            Factory: (string)arguments[2].Value!,
            Settings: settings?.ToTypeOf(),
            Overrides: overrides?.ToTypeOf(),
            Members: settings is null ? default : GetMembers(settings),
            RequiresApproval: named.TryGetValue("RequiresApproval", out var asks) && asks is true);
    }

    // The tool list properties of the tools with overrides, from the stamps the generated code puts on a toolset
    public static IEnumerable<string> GetOverridden(ITypeSymbol toolset) =>
        toolset.GetAttributes()
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.GeneratedToolAttribute))
            .Where(attribute => attribute.NamedArguments.Any(argument => argument.Key == "Overrides"))
            .Select(attribute => (string)attribute.ConstructorArguments[1].Value!);

    // What Configure{Tool}() sets: every settable member but the obsolete ones
    public static EquatableArray<SettingMember> GetMembers(ITypeSymbol settings) =>
        settings.GetPublicProperties()
            .Where(property => property.IsSettable && !property.IsObsolete)
            .Select(property => new SettingMember(property.Name, property.Type.ToFullyQualified()))
            .ToEquatableArray();

    // The library is among them: its module registers the built-in toolsets (D37)
    private static IEnumerable<IAssemblySymbol> GetLibraryUsers(Compilation compilation) =>
        compilation.SourceModule.ReferencedAssemblySymbols.Where(assembly => assembly.Name == KnownTypes.LibraryAssembly
            || assembly.Modules
                .SelectMany(module => module.ReferencedAssemblies)
                .Any(identity => identity.Name == KnownTypes.LibraryAssembly));

    private static bool DeclaresToolsetsOrAgents(INamespaceSymbol ns, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return ns.GetTypeMembers().Any(IsToolsetOrAgent)
            || ns.GetNamespaceMembers().Any(child => DeclaresToolsetsOrAgents(child, ct));
    }

    private static bool IsToolsetOrAgent(INamedTypeSymbol type) =>
        (type is { TypeKind: TypeKind.Class, IsAbstract: false }
            && (type.DerivesFrom(KnownTypes.Toolset) || type.DerivesFrom(KnownTypes.Agent)))
        || type.GetTypeMembers().Any(IsToolsetOrAgent);
}
