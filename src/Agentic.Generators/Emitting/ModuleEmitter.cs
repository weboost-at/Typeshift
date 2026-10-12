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
        writer.Line($"[assembly: global::Agentic.Modules.GeneratedModule(typeof({Names.Module(module)}))]");
        writer.Line();
        writer.Line($"namespace {Names.GeneratedNamespace};");
        writer.Line();
        writer.Line("/// <summary>");
        writer.Line($"/// Registers the toolsets and agents of {model.AssemblyName}.");
        writer.Line("/// </summary>");
        writer.Line($"[{HiddenFromEditors}]");

        foreach (var toolset in model.Toolsets)
        {
            writer.Line($"[global::Agentic.Modules.ModuleToolset(typeof({toolset}))]");
        }

        foreach (var agent in model.Agents)
        {
            var name = Names.Literal(agent.UniqueName);
            writer.Line($"[global::Agentic.Modules.ModuleAgent(typeof({agent.Type}), {name})]");
        }

        using (writer.Block($"public static class {module}"))
        {
            writer.Line("/// <summary>");
            writer.Line("/// Registers this assembly's toolsets and agents, once per service collection.");
            writer.Line("/// </summary>");

            writer.Line("public static void Register(");

            using (writer.Indent())
            {
                writer.Line("global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
            }

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
                    writer.Line($"{Registration}.AddAgent(services, static () => new {agent.Type}());");
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
