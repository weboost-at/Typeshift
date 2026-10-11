using System.Collections.Generic;
using System.Linq;
using Agentic.Generators.Models;

namespace Agentic.Generators.Emitting;

internal static class AgentEmitter
{
    private const string ChatOptionProperty = "global::Agentic.Agents.ChatOptionProperty";

    private const string ReadOnlyList = "global::System.Collections.Generic.IReadOnlyList";

    public static string Emit(AgentOutput output)
    {
        var agent = output.Agent;

        return TypeEmitter.EmitPartial(
            agent.Type,
            [],
            writer => WriteMembers(writer, output),
            writer => WriteConfigureExtension(writer, agent));
    }

    private static void WriteMembers(SourceWriter writer, AgentOutput output)
    {
        var agent = output.Agent;

        Docs.WriteSummary(writer, "The unique name of this agent.");
        writer.Line($"public const string Name = {Names.Literal(agent.UniqueName!)};");
        writer.Line();
        WriteToolNames(writer, output.Tools);
        writer.Line();
        WriteConfigurableChatOptions(writer, agent.ConfigurableOptions);
        writer.Line();
        WriteConfiguration(writer, agent, output.Tools);
    }

    private static void WriteToolNames(SourceWriter writer, IReadOnlyList<ResolvedTool> tools)
    {
        Docs.WriteSummary(writer, "The names of the tools this agent offers, in declaration order.");
        WriteList(
            writer,
            $"public static {ReadOnlyList}<global::Agentic.Tools.ToolName> ToolNames {{ get; }}",
            tools.Select(tool => $"new({Names.Literal(tool.Tool.Name)})").ToList());
    }

    private static void WriteConfigurableChatOptions(SourceWriter writer, IReadOnlyList<ChatOptionModel> options)
    {
        Docs.WriteInheritDoc(writer);
        var declaration = $"protected override {ReadOnlyList}<{ChatOptionProperty}> ConfigurableChatOptions {{ get; }}";

        var properties = options
            .Select(option => string.Join(
                "\n",
                $"{ChatOptionProperty}.Create<{option.Type}>(",
                $"    {Names.Literal(option.Name)},",
                $"    static options => options.{Names.Identifier(option.Name)},",
                $"    static (options, value) => options.{Names.Identifier(option.Name)} = value)"))
            .ToList();

        WriteList(writer, declaration, properties);
    }

    private static void WriteConfiguration(SourceWriter writer, AgentModel agent, IReadOnlyList<ResolvedTool> tools)
    {
        var stored = "global::Agentic.Agents.AgentConfiguration";
        // A tool property never hides a member of the base view or shares a name with a chat option
        var reserved = new HashSet<string>(agent.ConfigurableOptions.Select(option => option.Name))
        {
            "Model",
            "Stored"
        };

        Docs.WriteSummary(writer, "The stored configuration of this agent, limited to what may change at runtime.");
        ViewEmitter.WriteDeclaration(
            writer,
            "Configuration",
            "global::Agentic.Agents.AgentConfigurationView",
            "global::Agentic.Agents.IAgentConfigurationView");

        using (writer.Block())
        {
            ViewEmitter.WriteConstructor(writer, "Configuration", stored, "configuration");

            foreach (var option in agent.ConfigurableOptions)
            {
                writer.Line();
                ViewEmitter.WriteValue(writer, option.Name, option.NullableType, "GetChatOption", "SetChatOption");
            }

            foreach (var tool in tools.Where(tool => tool.Tool.Settings is not null))
            {
                var view = $"{tool.Toolset}.{ToolsetEmitter.GetConfigurationName(tool.Tool.Name)}";
                var property = Names.ToPascalCase(tool.Tool.Name);

                writer.Line();
                Docs.WriteSummary(writer, $"The stored settings of the <c>{tool.Tool.Name}</c> tool for this agent.");
                writer.Line($"public {view} {(reserved.Contains(property) ? property + "Tool" : property)} =>");

                using (writer.Indent())
                {
                    writer.Line($"GetToolSettings<{view}>();");
                }
            }

            writer.Line();
            ViewEmitter.WriteCreate(writer, "Configuration", stored, "configuration");
        }
    }

    // An extension block on the agent's runtime, so ConfigureAsync infers the view from the lambda alone
    private static void WriteConfigureExtension(SourceWriter writer, AgentModel agent)
    {
        writer.Line();
        var name = agent.Type.FullName.Replace("global::", "");

        Docs.WriteSummary(writer, $"Configures <see cref=\"{name}\"/> through its typed view.");

        using (writer.Block($"{agent.Type.Accessibility} static class {agent.Type.Name}ConfigurationExtensions"))
        {
            using (writer.Block($"extension(global::Agentic.Agents.AgentRuntime<{agent.Type.FullName}> runtime)"))
            {
                Docs.WriteSummary(
                    writer,
                    "Changes the agent's stored configuration through its typed, allowlisted view and stores it.");
                writer.Line("public global::System.Threading.Tasks.Task ConfigureAsync(");

                using (writer.Indent())
                {
                    writer.Line($"global::System.Action<{agent.Type.FullName}.Configuration> configure,");
                    writer.Line("global::System.Threading.CancellationToken ct = default) =>");
                    writer.Line("runtime.UpdateConfigurationAsync(configure, ct);");
                }
            }
        }
    }

    // A property initialized with a collection expression, one item per line
    private static void WriteList(SourceWriter writer, string declaration, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            writer.Line($"{declaration} = [];");
            return;
        }

        writer.Line($"{declaration} =");

        using (writer.Block(open: "[", close: "];"))
        {
            for (var index = 0; index < items.Count; index++)
            {
                var lines = (items[index] + (index < items.Count - 1 ? "," : "")).Split('\n');

                foreach (var line in lines)
                {
                    writer.Line(line);
                }
            }
        }
    }
}
