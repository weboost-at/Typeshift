using System.Linq;
using Agentic.Generators.Models;

namespace Agentic.Generators.Emitting;

internal static class ModuleEmitter
{
    private const string Registration = "global::Agentic.Modules.ModuleRegistration";

    private const string HiddenFromEditors =
        "global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)";

    public static string Emit(ModuleModel model)
    {
        var writer = new SourceWriter();
        var module = model.Name;

        writer.Line(SourceWriter.Header);
        writer.Lines(
            $"""
            [assembly: global::Agentic.Modules.GeneratedModule(typeof({Names.Module(module)}))]

            namespace {Names.GeneratedNamespace};

            /// <summary>
            /// Registers the toolsets and agents of {model.AssemblyName}.
            /// </summary>
            [{HiddenFromEditors}]
            """);

        foreach (var toolset in model.Toolsets)
        {
            writer.Line($"[global::Agentic.Modules.ModuleToolset(typeof({toolset}))]");
        }

        foreach (var agent in model.Agents)
        {
            writer.Line($"[global::Agentic.Modules.ModuleAgent(typeof({agent}))]");
        }

        using (writer.Block($"public static class {module}"))
        {
            writer.Lines(
                """
                /// <summary>
                /// Registers this assembly's toolsets and agents, once per service collection.
                /// </summary>
                public static void Register(
                    global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                """);

            using (writer.Block())
            {
                writer.Line($"if (!{Registration}.TryAddModule(services, typeof({module}))) return;");
                writer.Line();

                foreach (var toolset in model.Toolsets)
                {
                    writer.Line($"{Registration}.AddToolset(services, static () => new {toolset}());");
                }

                foreach (var agent in model.Agents)
                {
                    writer.Line($"{Registration}.AddAgent(services, static () => new {agent}());");
                }
            }
        }

        return writer.ToString();
    }

    // An identifier per assembly, so the modules of several assemblies live side by side in one namespace
    public static string GetName(string assemblyName)
    {
        var name = string.Concat(assemblyName.Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        return char.IsDigit(name[0]) ? $"_{name}Module" : $"{name}Module";
    }
}
