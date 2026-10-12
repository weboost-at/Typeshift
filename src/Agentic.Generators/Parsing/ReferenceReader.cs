using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Parsing;

// D9: only an assembly that uses the library but carries no module stamp has its types read, to report AGT011
internal static class ReferenceReader
{
    public static ReferenceModel Read(Compilation compilation, CancellationToken ct)
    {
        var modules = new List<ModuleReference>();
        var ungenerated = new List<string>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            if (!UsesLibrary(assembly)) continue;

            if (GetStampedModule(assembly) is { } module) modules.Add(ReadModule(module));
            else if (DeclaresToolsetsOrAgents(assembly.GlobalNamespace, ct)) ungenerated.Add(assembly.Name);
        }

        return new ReferenceModel(modules.ToEquatableArray(), ungenerated.ToEquatableArray());
    }

    public static ToolSummary ReadTool(AttributeData stamp)
    {
        var arguments = stamp.ConstructorArguments;

        return new ToolSummary(
            Name: (string)arguments[0].Value!,
            Settings: (arguments[1].Value as ITypeSymbol)?.ToTypeOf(),
            Configurable: arguments[2].Values.Select(value => (string)value.Value!).ToEquatableArray(),
            Location: null);
    }

    private static bool UsesLibrary(IAssemblySymbol assembly) =>
        assembly.Modules
            .SelectMany(module => module.ReferencedAssemblies)
            .Any(identity => identity.Name == KnownTypes.LibraryAssembly);

    private static INamedTypeSymbol? GetStampedModule(IAssemblySymbol assembly) =>
        assembly.GetAttributes()
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.GeneratedModuleAttribute))
            .Select(attribute => attribute.ConstructorArguments[0].Value)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();

    private static ModuleReference ReadModule(INamedTypeSymbol module)
    {
        var stamps = module.GetAttributes();

        var toolsets = stamps
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.ModuleToolsetAttribute))
            .Select(attribute => attribute.ConstructorArguments[0].Value)
            .OfType<INamedTypeSymbol>()
            .Select(ReadToolset)
            .ToEquatableArray();

        var agents = stamps
            .Where(attribute => attribute.AttributeClass.Is(KnownTypes.ModuleAgentAttribute))
            .Select(attribute => ((ITypeSymbol)attribute.ConstructorArguments[0].Value!).ToTypeOf())
            .ToEquatableArray();

        return new ModuleReference(module.ToTypeOf(), toolsets, agents);
    }

    private static ToolsetSummary ReadToolset(INamedTypeSymbol toolset) =>
        new(
            toolset.ToTypeOf(),
            toolset.GetAttributes()
                .Where(attribute => attribute.AttributeClass.Is(KnownTypes.GeneratedToolAttribute))
                .Select(ReadTool)
                .ToEquatableArray());

    private static bool DeclaresToolsetsOrAgents(INamespaceSymbol ns, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return ns.GetTypeMembers().Any(IsToolsetOrAgent)
            || ns.GetNamespaceMembers().Any(child => DeclaresToolsetsOrAgents(child, ct));
    }

    private static bool IsToolsetOrAgent(INamedTypeSymbol type) =>
        type is { TypeKind: TypeKind.Class, IsAbstract: false }
        && (type.DerivesFrom(KnownTypes.Toolset) || type.DerivesFrom(KnownTypes.AgentDefinition))
        || type.GetTypeMembers().Any(IsToolsetOrAgent);
}
