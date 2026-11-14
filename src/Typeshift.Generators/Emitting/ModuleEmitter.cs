using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;

namespace Typeshift.Generators.Emitting;

internal static class ModuleEmitter
{
    private const string Registration = "global::Typeshift.Generated.ModuleRegistration";

    private const string Services = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    // An identifier per assembly, so the modules of several assemblies live side by side in one namespace. A name
    // of letters, digits and dots maps to one identifier alone; any other character becomes a _ as a dot does, so
    // such a name carries a hash of itself, and Acme-Tools is not Acme.Tools (D6)
    public static string GetName(string assemblyName)
    {
        var name = string.Concat(assemblyName.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        var isPlain = assemblyName.All(character => char.IsLetterOrDigit(character) || character == '.');
        var unique = isPlain ? name : $"{name}_{Names.GetStableHash(assemblyName)}";

        return unique.Length == 0 || char.IsDigit(unique[0]) ? $"_{unique}Module" : $"{unique}Module";
    }

    public static string Emit(ModuleModel model)
    {
        var writer = new SourceWriter();
        var module = $"global::{Names.GeneratedNamespace}.{model.Name}";

        writer.Line(SourceWriter.GetHeader(model.ExperimentalIds));
        writer.Lines(
            $"""
            [assembly: global::Typeshift.Generated.GeneratedModule(typeof({module}), {ReferenceReader.StampVersion})]

            namespace {Names.GeneratedNamespace};

            /// <summary>
            /// Registers the toolsets and agents of {Docs.Escape(model.AssemblyName)}.
            /// </summary>
            {Names.Hidden}
            {Names.Generated}
            """);

        foreach (var toolset in model.Toolsets)
        {
            writer.Line($"[global::Typeshift.Generated.ModuleToolset(typeof({toolset}))]");
        }

        foreach (var agent in model.Agents)
        {
            writer.Line(GetStamp(agent));
        }

        using (writer.Block($"public static class {model.Name}"))
        {
            writer.Lines(
                $"""
                /// <summary>
                /// Registers this assembly's toolsets, agents and JSON contexts, once per service collection.
                /// </summary>
                /// <param name="services">The services.</param>
                public static void Register({Services} services)
                """);

            using (writer.Block())
            {
                writer.Line($"if (!{Registration}.TryAddModule(services, typeof({model.Name}))) return;");
                writer.Line();

                foreach (var toolset in model.Toolsets)
                {
                    writer.Line($"{Registration}.AddToolset(services, static () => new {toolset}());");
                }

                foreach (var agent in model.Agents)
                {
                    writer.Line($"{Registration}.AddAgent(services, static () => new {agent.Type}());");
                }

                foreach (var context in model.JsonContexts)
                {
                    writer.Line($"{Registration}.AddJsonContext(services, {context}.Default);");
                }

                foreach (var annotations in model.SchemaAnnotations)
                {
                    writer.Line($"{Registration}.AddSchemaAnnotations(services, {annotations}.Source);");
                }
            }
        }

        return writer.ToString();
    }

    // D33, D34: what the agents of other assemblies check when they use this one
    private static string GetStamp(ModuleAgent agent)
    {
        List<string> arguments =
        [
            $"typeof({agent.Type})",
            Names.Literal(agent.Kind),
            Names.Literal(agent.Name),
            .. agent.ApprovalTools.Select(Names.Literal)
        ];

        if (agent.Described) arguments.Add("Described = true");
        if (agent.Tools.Count > 0) arguments.Add($"Tools = {Names.Array(agent.Tools)}");
        if (agent.OwnTools.Count > 0) arguments.Add($"OwnTools = {Names.Array(agent.OwnTools)}");
        if (agent.AsksUser) arguments.Add("AsksUser = true");

        return $"[global::Typeshift.Generated.ModuleAgent({string.Join(", ", arguments)})]";
    }
}
