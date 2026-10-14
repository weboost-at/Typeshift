using System.Linq;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;

namespace Typeshift.Generators.Emitting;

internal static class ModuleEmitter
{
    private const string Registration = "global::Typeshift.Generated.ModuleRegistration";

    private const string Services = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private const string Hidden =
        "global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)";

    // An identifier per assembly, so the modules of several assemblies live side by side in one namespace
    public static string GetName(string assemblyName)
    {
        var name = string.Concat(assemblyName.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        return char.IsDigit(name[0]) ? $"_{name}Module" : $"{name}Module";
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
                {(model.Agents.Count == 0 ? "null" : $"typeof({model.IdType})")})]

            namespace {Names.GeneratedNamespace};

            /// <summary>
            /// Registers the toolsets and agents of {Docs.Escape(model.AssemblyName)}.
            /// </summary>
            [{Hidden}]
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
            }
        }

        return writer.ToString();
    }
}
