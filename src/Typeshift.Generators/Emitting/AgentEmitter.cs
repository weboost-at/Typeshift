using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Emitting;

internal static class AgentEmitter
{
    private const string Hidden =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";

    private const string ChatOptions = "global::Microsoft.Extensions.AI.ChatOptions";

    private const string RunOptions = "global::Typeshift.RunOptions";

    private const string Task = "global::System.Threading.Tasks.Task";

    private const string CancellationToken = "global::System.Threading.CancellationToken";

    private const string Runs = "global::Typeshift.Generated.AgentRuns";

    private const string Tools =
        "global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.AI.AITool>";

    private const string Updates =
        "global::System.Collections.Generic.IAsyncEnumerable<global::Microsoft.Extensions.AI.ChatResponseUpdate>";

    // The members of a record that its properties must not be named like
    private static readonly string[] RecordMembers =
        ["Clone", "Deconstruct", "EqualityContract", "Equals", "GetHashCode", "PrintMembers", "Settings", "ToString"];

    public static string Emit(AgentOutput output)
    {
        var agent = output.Agent;
        var configured = output.Tools.Where(tool => HasConfigure(agent, tool.Tool)).ToList();

        return TypeEmitter.EmitPartial(
            agent.Type,
            [],
            writer =>
            {
                WriteSettings(writer, output);
                writer.Line();
                writer.Line("/// <inheritdoc />");
                writer.Line(Hidden);
                writer.Line(
                    "protected override global::Typeshift.Generated.AgentDeclaration Declaration => "
                    + "TypeshiftGenerated.Instance;");

                if (configured.Count > 0)
                {
                    writer.Line();
                    writer.Line("private readonly TypeshiftGenerated.Configured _typeshift = new();");
                }

                foreach (var tool in configured)
                {
                    writer.Line();
                    Docs.WriteSummary(
                        writer,
                        $"Sets the settings <c>{tool.Tool.Name}</c> runs with in this agent, over the code defaults "
                        + "of its settings record.");
                    Docs.WriteReturns(writer, "The settings, one <c>With...</c> per member.");
                    writer.Line(
                        $"private TypeshiftGenerated.{Builder(tool.Tool)} {Configure(tool.Tool)}() => "
                        + "new(_typeshift);");
                }

                writer.Line();
                WriteDeclaration(writer, output, configured);
            },
            writer =>
            {
                if (agent.IsReachable) WriteHostMembers(writer, agent);
            });
    }

    // The user's own Configure{Tool}() wins; a tool without settings has nothing to configure
    private static bool HasConfigure(AgentModel agent, ToolSummary tool) =>
        tool.Settings is not null && !agent.Configures.Contains(Configure(tool));

    private static string Configure(ToolSummary tool) => $"Configure{Names.ToPascalCase(tool.Name)}";

    private static string Builder(ToolSummary tool) => $"{Names.ToPascalCase(tool.Name)}Builder";

    private static string Field(ToolSummary tool)
    {
        var pascal = Names.ToPascalCase(tool.Name);
        return Names.Identifier(char.ToLowerInvariant(pascal[0]) + pascal.Substring(1));
    }

    // The tool's resolved settings in CreateTools; ends in Settings, so never one of its parameters
    private static string Local(ToolSummary tool) => $"{Field(tool).TrimStart('@')}Settings";

    private static IEnumerable<OptionMember> Picked(EquatableArray<string> picks, OptionMember[] options) =>
        options.Where(option => picks.Contains(option.Option));

    // D27: a tool member named like an option or one of the record's own gets a suffix instead of clashing with it
    private static string SettingsMember(AgentModel agent, ToolSummary tool)
    {
        string[] reserved =
        [
            .. RecordMembers,
            .. Picked(agent.ChatOptions, OptionMembers.Chat).Select(option => option.Setting),
            .. Picked(agent.RunOptions, OptionMembers.Run).Select(option => option.Setting)
        ];

        return Names.Identifier(Names.Unreserved(Names.ToPascalCase(tool.Name), "Tool", reserved));
    }

    private static void WriteSettings(SourceWriter writer, AgentOutput output)
    {
        var agent = output.Agent;

        Docs.WriteSummary(
            writer,
            "What may change at runtime: passed for one run, over the agent's code. Null keeps the layer below.");

        using (writer.Block("public sealed record Settings"))
        {
            var members = new List<(string Summary, string Declaration)>();

            foreach (var option in Picked(agent.ChatOptions, OptionMembers.Chat))
            {
                var summary = option.Option == "ModelId"
                    ? "The model, over <c>WithModel</c>. Null keeps the layer below."
                    : $"The chat option <c>{option.Option}</c>. Null keeps the layer below.";

                members.Add((summary, $"public {option.Type} {option.Setting} {{ get; init; }}"));
            }

            foreach (var option in Picked(agent.RunOptions, OptionMembers.Run))
            {
                members.Add((
                    $"The run option <c>{option.Option}</c>. Null keeps the layer below.",
                    $"public {option.Type} {option.Setting} {{ get; init; }}"));
            }

            foreach (var tool in output.Tools.Select(offered => offered.Tool).Where(tool => tool.Overrides is not null))
            {
                members.Add((
                    $"The configurable settings of the <c>{tool.Name}</c> tool; its null values keep the layer below.",
                    $"public {tool.Overrides} {SettingsMember(agent, tool)} {{ get; init; }} = new();"));
            }

            foreach (var (summary, declaration) in members)
            {
                if (declaration != members[0].Declaration) writer.Line();

                Docs.WriteSummary(writer, summary);
                writer.Line(declaration);
            }
        }
    }

    private static void WriteDeclaration(SourceWriter writer, AgentOutput output, IReadOnlyList<OfferedTool> configured)
    {
        var agent = output.Agent;
        var declaration = $"global::Typeshift.Generated.AgentDeclaration<{agent.Type.FullName}, Settings>";

        writer.Line("private sealed class TypeshiftGenerated");

        using (writer.Indent())
        {
            writer.Line($": {declaration}");
        }

        using (writer.Block())
        {
            writer.Line("public static readonly TypeshiftGenerated Instance = new();");
            writer.Line();
            writer.Line($"public override string Name => {Names.Literal(agent.Name)};");
            writer.Line();

            var description = agent.Description is { } text ? Names.Literal(text) : "null";
            writer.Line($"public override string? Description => {description};");
            writer.Line();
            WriteApplySettings(writer, agent);
            writer.Line();
            WriteCreateTools(writer, output, configured);

            if (configured.Count == 0) return;

            writer.Line();
            WriteConfigured(writer, configured);
        }
    }

    private static void WriteApplySettings(SourceWriter writer, AgentModel agent)
    {
        var chat = Picked(agent.ChatOptions, OptionMembers.Chat).ToList();
        var run = Picked(agent.RunOptions, OptionMembers.Run).ToList();

        writer.Lines(
            $"""
            protected override {RunOptions} ApplySettings(
                {ChatOptions} options,
                {RunOptions} run,
                Settings settings)
            """);

        if (chat.Count == 0 && run.Count == 0)
        {
            writer.Line("    => run;");
            return;
        }

        using (writer.Block())
        {
            foreach (var option in chat)
            {
                var value = Names.Identifier(char.ToLowerInvariant(option.Setting[0]) + option.Setting.Substring(1));
                var applied = string.Format(option.Apply, value);

                writer.Line($"if (settings.{option.Setting} is {{ }} {value}) options.{option.Option} = {applied};");
            }

            if (chat.Count > 0) writer.Line();

            if (run.Count == 0)
            {
                writer.Line("return run;");
                return;
            }

            writer.Line("return run with");

            using (writer.Block(close: "};"))
            {
                writer.List(
                [
                    .. run.Select(option =>
                        $"{option.Option} = settings.{option.Setting} ?? run.{option.Option}")
                ]);
            }
        }
    }

    // D27: the agent's Configure{Tool}() over the record's defaults, then each layer's overrides
    private static void WriteCreateTools(
        SourceWriter writer,
        AgentOutput output,
        IReadOnlyList<OfferedTool> configured)
    {
        var agent = output.Agent;

        writer.Lines(
            $"""
            protected override {Tools} CreateTools(
                {agent.Type.FullName} agent,
                global::System.Collections.Generic.IReadOnlyList<Settings> layers,
                global::Typeshift.Generated.ToolJson json)
            """);

        using (writer.Block())
        {
            foreach (var offered in output.Tools.Where(offered => offered.Tool.Settings is not null))
            {
                var tool = offered.Tool;
                var local = Local(tool);
                var code = configured.Contains(offered) ? $"agent._typeshift.{Field(tool)} ?? new()" : "new()";

                writer.Line($"{tool.Settings} {local} = {code};");

                if (tool.Overrides is not null)
                {
                    var member = SettingsMember(agent, tool);

                    writer.Line();

                    using (writer.Block("foreach (var layer in layers)"))
                    {
                        writer.Line($"{local} = layer.{member}?.ApplyTo({local}) ?? {local};");
                    }
                }

                writer.Line();
            }

            if (output.Tools.Count == 0)
            {
                writer.Line("return [];");
                return;
            }

            writer.Line("return");

            using (writer.Block(open: "[", close: "];"))
            {
                writer.List(
                [
                    .. output.Tools.Select(offered =>
                        offered.Tool.Settings is null
                            ? $"{offered.Toolset}.{offered.Tool.Factory}(json)"
                            : $"{offered.Toolset}.{offered.Tool.Factory}({Local(offered.Tool)}, json)")
                ]);
            }
        }
    }

    // What Configure{Tool}() sets, kept per agent instance; the builders write it
    private static void WriteConfigured(SourceWriter writer, IReadOnlyList<OfferedTool> configured)
    {
        using (writer.Block("public sealed class Configured"))
        {
            foreach (var offered in configured)
            {
                writer.Line($"public {offered.Tool.Settings}? {Field(offered.Tool)};");
            }
        }

        foreach (var offered in configured)
        {
            var tool = offered.Tool;
            var builder = Builder(tool);

            writer.Line();

            using (writer.Block($"public sealed class {builder}(Configured configured)"))
            {
                foreach (var member in tool.Members)
                {
                    if (member != tool.Members[0]) writer.Line();

                    using (writer.Block($"public {builder} With{member.Name}({member.Type} value)"))
                    {
                        writer.Line(
                            $"configured.{Field(tool)} = (configured.{Field(tool)} ?? new()) with "
                            + $"{{ {Names.Identifier(member.Name)} = value }};");
                        writer.Line("return this;");
                    }
                }
            }
        }
    }

    // D12: the typed members of the agent's host, next to the agent in its namespace
    private static void WriteHostMembers(SourceWriter writer, AgentModel agent)
    {
        var host = $"global::Typeshift.AgentHost<{agent.Type.FullName}>";
        var settings = $"{agent.Type.FullName}.Settings";
        var response = "global::Microsoft.Extensions.AI.ChatResponse";
        var message = "global::Microsoft.Extensions.AI.ChatMessage";
        var user = "new(global::Microsoft.Extensions.AI.ChatRole.User, input)";

        writer.Line();
        Docs.WriteSummary(writer, $"The runs of <see cref=\"{agent.Type.FullName}\"/>.");

        using (writer.Block($"{agent.Type.Accessibility} static class {agent.Type.FlatName}HostExtensions"))
        using (writer.Block($"extension({host} host)"))
        {
            WriteRun(writer, "Runs the agent on a message", $"{Task}<{response}>", "RunAsync", "string", settings,
                $"{Runs}.RunAsync(host, {user}, settings, ct);");
            writer.Line();
            WriteRun(writer, "Runs the agent on a message, which may carry images or approvals", $"{Task}<{response}>",
                "RunAsync", message, settings, $"{Runs}.RunAsync(host, input, settings, ct);");
            writer.Line();

            Docs.WriteSummary(
                writer,
                "Runs the agent on a message, asking the model for JSON of <typeparamref name=\"TResult\"/>'s schema.");
            Docs.WriteTypeParam(writer, "TResult", "The result's type.");
            WriteRun(writer, null, $"{Task}<{response}<TResult>>", "RunAsync<TResult>", "string", settings,
                $"{Runs}.RunAsync<{agent.Type.FullName}, TResult>(host, {user}, settings, ct);");
            writer.Line();
            WriteRun(writer, "Runs the agent on a message, streaming the updates", Updates, "RunStreamingAsync",
                "string", settings, $"{Runs}.RunStreamingAsync(host, {user}, settings, ct);");
            writer.Line();
            WriteRun(writer, "Runs the agent on a message, streaming the updates", Updates, "RunStreamingAsync",
                message, settings, $"{Runs}.RunStreamingAsync(host, input, settings, ct);");
            writer.Line();

            Docs.WriteSummary(
                writer,
                "Resolves the options of a run, for the raw path: <c>CreateChatClientAsync</c>, then the client.");
            Docs.WriteParam(writer, "settings", "What the run changes; null keeps the agent's code.");
            Docs.WriteParam(writer, "ct", "Cancels the resolution.");
            Docs.WriteReturns(writer, "The options, with the agent's tools.");
            writer.Lines(
                $"""
                public {Task}<{ChatOptions}> ResolveOptionsAsync(
                    {settings}? settings = null,
                    {CancellationToken} ct = default) =>
                    {Runs}.ResolveOptionsAsync(host, settings, ct);
                """);
        }
    }

    private static void WriteRun(
        SourceWriter writer,
        string? summary,
        string returns,
        string name,
        string input,
        string settings,
        string call)
    {
        if (summary is not null)
            Docs.WriteSummary(writer, $"{summary}, with the run's settings over the agent's code.");

        Docs.WriteParam(writer, "input", "The user's message.");
        Docs.WriteParam(writer, "settings", "What the run changes; null keeps the agent's code.");
        Docs.WriteParam(writer, "ct", "Cancels the run.");
        Docs.WriteReturns(
            writer,
            returns.Contains("IAsyncEnumerable")
                ? "The updates; they carry the agent's name as their author."
                : "The response; its messages carry the agent's name as their author.");
        writer.Lines(
            $"""
            public {returns} {name}(
                {input} input,
                {settings}? settings = null,
                {CancellationToken} ct = default) =>
                {call}
            """);
    }
}
