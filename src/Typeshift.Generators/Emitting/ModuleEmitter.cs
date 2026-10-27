using System.Linq;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;

namespace Typeshift.Generators.Emitting;

internal static class ModuleEmitter
{
    private const string Registration = "global::Typeshift.Generated.ModuleRegistration";

    private const string Services = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    // An identifier per assembly, so the modules of several assemblies live side by side in one namespace
    public static string GetName(string assemblyName)
    {
        var name = string.Concat(assemblyName.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        return name.Length == 0 || char.IsDigit(name[0]) ? $"_{name}Module" : $"{name}Module";
    }

    public static string Emit(ModuleModel model)
    {
        var writer = new SourceWriter();
        var module = $"global::{Names.GeneratedNamespace}.{model.Name}";

        writer.Line(SourceWriter.Header);
        writer.Lines(
            $"""
            [assembly: global::Typeshift.Generated.GeneratedModule(
                typeof({module}),
                {ReferenceReader.StampVersion},
                {FormatStampedType(model, model.IdType)},
                {FormatStampedType(model, model.ProviderIdType)})]

            namespace {Names.GeneratedNamespace};

            /// <summary>
            /// Registers the toolsets and agents of {Docs.Escape(model.AssemblyName)}.
            /// </summary>
            {Names.Hidden}
            """);

        foreach (var toolset in model.Toolsets)
        {
            writer.Line($"[global::Typeshift.Generated.ModuleToolset(typeof({toolset}))]");
        }

        foreach (var agent in model.Agents)
        {
            writer.Line(
                $"[global::Typeshift.Generated.ModuleAgent(typeof({agent.Type}), {Names.Literal(agent.Kind)})]");
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

    // D11, D56: an assembly without agents was built with no id type that could clash
    private static string FormatStampedType(ModuleModel model, string type) =>
        model.Agents.Count == 0
            ? "null"
            : $"typeof({type})";
}
