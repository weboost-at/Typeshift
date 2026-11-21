using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;
using Typeshift.Generators.Naming;

namespace Typeshift.Generators.Emitting;

internal static class ModuleEmitter
{
    private const string Registration = "global::Typeshift.Generated.ModuleRegistration";

    private const string Services = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    public static string Emit(ModuleModel model)
    {
        var writer = new SourceWriter();
        var module = $"global::{Names.GeneratedNamespace}.{model.Name}";

        writer.Line(SourceWriter.GetHeader(model.ExperimentalIds));
        writer.Lines(
            $"""
            [assembly: global::Typeshift.Generated.GeneratedModule(typeof({module}), {KnownTypes.StampVersion})]

            namespace {Names.GeneratedNamespace};

            /// <summary>
            /// Registers the toolsets and agents of {Docs.Escape(model.AssemblyName)}.
            /// </summary>
            {Names.Hidden}
            {Names.Generated}
            """);

        foreach (var toolset in model.Toolsets)
        {
            writer.Line($"[global::Typeshift.Generated.ModuleToolset(typeof({toolset.Type}))]");
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
                    WriteToolset(writer, toolset);
                }

                foreach (var agent in model.Agents)
                {
                    var kind = Names.Literal(agent.Kind);
                    writer.Line($"{Registration}.AddAgent(services, static () => new {agent.Type}(), {kind});");
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

    // D83: the toolset with what the catalog lists of its tools, which a tool picker shows
    private static void WriteToolset(SourceWriter writer, ModuleToolset toolset)
    {
        var create = $"{Registration}.AddToolset(services, static () => new {toolset.Type}()";

        if (toolset.Tools.Count == 0)
        {
            writer.Line($"{create});");
            return;
        }

        writer.Line($"{create},");

        using (writer.Indent())
        {
            writer.List([.. toolset.Tools.Select(GetToolInfo)], end: ");");
        }
    }

    private static string GetToolInfo(ModuleTool tool) =>
        $"new global::Typeshift.ToolInfo {{ Name = {Names.Literal(tool.Name)}, "
        + $"Description = {(tool.Description is { } description ? Names.Literal(description) : "null")}, "
        + $"IsReadOnly = {(tool.IsReadOnly ? "true" : "false")}, "
        + $"RequiresApproval = {(tool.RequiresApproval ? "true" : "false")} }}";

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
