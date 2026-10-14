using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Emitting;

internal static class AgentEmitter
{
    private const string ChatOptions = "global::Microsoft.Extensions.AI.ChatOptions";

    private const string RunOptions = "global::Typeshift.RunOptions";

    private const string Task = "global::System.Threading.Tasks.Task";

    private const string CancellationToken = "global::System.Threading.CancellationToken";

    private const string Runs = "global::Typeshift.Generated.AgentRuns";

    private const string Tools =
        "global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.AI.AITool>";

    private const string Updates =
        "global::System.Collections.Generic.IAsyncEnumerable<global::Microsoft.Extensions.AI.ChatResponseUpdate>";

    private const string Json = StorageEmitter.Json;

    // The members of the record that its properties must not be named like
    private static readonly string[] RecordMembers =
    [
        "Clone", "Deconstruct", "Deserialize", "EqualityContract", "Equals", "GetHashCode", "Kind", "PrintMembers",
        "Serialize", "Settings", "ToString", "TryReadJson", "Version", "WriteJson"
    ];

    public static string Emit(AgentOutput output, string idType)
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
                writer.Line(Names.Hidden);
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
                if (agent.IsReachable) WriteHostMembers(writer, agent, idType);
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

    // D6: a tool member named like an option or one of the record's own gets a suffix instead of clashing with it
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
        var chat = Picked(agent.ChatOptions, OptionMembers.Chat).ToList();
        var run = Picked(agent.RunOptions, OptionMembers.Run).ToList();
        var tools = output.Tools.Select(offered => offered.Tool).Where(tool => tool.Overrides is not null).ToList();

        Docs.WriteSummary(
            writer,
            "What may change at runtime: stored under a settings id, or passed for one run. Null keeps the layer "
            + "below.");
        writer.Line(
            "[global::System.Text.Json.Serialization.JsonConverter("
            + "typeof(global::Typeshift.Generated.StorableJsonConverter<Settings>))]");
        writer.Line("public sealed record Settings");

        using (writer.Indent())
        {
            writer.Line(": global::Typeshift.IStorable<Settings>, global::Typeshift.Generated.IJsonStorable<Settings>");
        }

        using (writer.Block())
        {
            var members = new List<(string Summary, string Declaration)>();

            foreach (var option in chat)
            {
                var summary = option.Option == "ModelId"
                    ? "The model, over <c>WithModel</c>. Null keeps the layer below."
                    : $"The chat option <c>{option.Option}</c>. Null keeps the layer below.";

                members.Add((summary, $"public {option.Type} {option.Setting} {{ get; init; }}"));
            }

            foreach (var option in run)
            {
                members.Add((
                    $"The run option <c>{option.Option}</c>. Null keeps the layer below.",
                    $"public {option.Type} {option.Setting} {{ get; init; }}"));
            }

            foreach (var tool in tools)
            {
                members.Add((
                    $"The configurable settings of the <c>{tool.Name}</c> tool; its null values keep the layer below.",
                    $"public {tool.Overrides} {SettingsMember(agent, tool)} {{ get; init; }} = new();"));
            }

            foreach (var (summary, declaration) in members)
            {
                Docs.WriteSummary(writer, summary);
                writer.Line(declaration);
                writer.Line();
            }

            // D39, D40, D44
            writer.Lines(
                $$"""
                /// <inheritdoc />
                public static string Kind => {{Names.Literal(agent.Kind)}};

                /// <inheritdoc />
                public static int Version => {{agent.Version}};

                /// <inheritdoc />
                public string Serialize() => {{Json}}.Write(WriteJson);

                /// <inheritdoc />
                public static Settings Deserialize(string json, int version) =>
                    version > Version
                        ? throw new global::System.Text.Json.JsonException(
                            $"{Kind} settings of version {version} are newer than this code's {Version}.")
                        : {{Json}}.Read<Settings>(json, TryReadJson);
                """);

            var stored = chat.Concat(run)
                .Select(option => (option.Setting, Parsing.Storage.JsonName(option.Setting), option.Storage));

            StorageEmitter.WriteRecordJson(
                writer,
                "Settings",
                stored,
                tools.Count == 0 ? null : more => WriteTools(more, agent, tools),
                [
                    .. tools.Select((tool, index) =>
                        $"{SettingsMember(agent, tool)} = {tool.Overrides}.TryReadJson("
                        + $"{Json}.Member({Json}.Member(element, \"tools\"), {Names.Literal(tool.Name)}), "
                        + $"out var tool{index}) ? tool{index} : new()")
                ]);
        }
    }

    // Tools are stored under "tools" by name, each left out while it overrides nothing
    private static void WriteTools(SourceWriter writer, AgentModel agent, IReadOnlyList<ToolSummary> tools)
    {
        for (var index = 0; index < tools.Count; index++)
        {
            var tool = tools[index];

            writer.Line(
                $"var tool{index} = {SettingsMember(agent, tool)} is {{ }} set{index} && set{index} != new "
                + $"{tool.Overrides}() ? set{index} : null;");
        }

        writer.Line();

        using (writer.Block($"if ({string.Join(" || ", tools.Select((_, index) => $"tool{index} is not null"))})"))
        {
            writer.Line("writer.WriteStartObject(\"tools\");");

            for (var index = 0; index < tools.Count; index++)
            {
                writer.Line();

                using (writer.Block($"if (tool{index} is not null)"))
                {
                    writer.Line($"writer.WritePropertyName({Names.Literal(tools[index].Name)});");
                    writer.Line($"tool{index}.WriteJson(writer);");
                }
            }

            writer.Line();
            writer.Line("writer.WriteEndObject();");
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
    private static void WriteHostMembers(SourceWriter writer, AgentModel agent, string idType)
    {
        var host = $"global::Typeshift.AgentHost<{agent.Type.FullName}>";
        var settings = $"{agent.Type.FullName}.Settings";
        var response = "global::Microsoft.Extensions.AI.ChatResponse";
        var message = "global::Microsoft.Extensions.AI.ChatMessage";
        var user = "new(global::Microsoft.Extensions.AI.ChatRole.User, input)";
        var types = $"{agent.Type.FullName}, {settings}";
        var run = new HostRun(idType, settings);

        writer.Line();
        Docs.WriteSummary(writer, $"The runs and stored settings of <see cref=\"{agent.Type.FullName}\"/>.");

        using (writer.Block($"{agent.Type.Accessibility} static class {agent.Type.FlatName}HostExtensions"))
        using (writer.Block($"extension({host} host)"))
        {
            run.Write(writer, "Runs the agent on a message", $"{Task}<{response}>", "RunAsync", "string",
                $"{Runs}.RunAsync(host, {user}, settingsId, conversationId, settings, ct);");
            writer.Line();
            run.Write(writer, "Runs the agent on a message, which may carry images or approvals", $"{Task}<{response}>",
                "RunAsync", message, $"{Runs}.RunAsync(host, input, settingsId, conversationId, settings, ct);");
            writer.Line();

            Docs.WriteSummary(
                writer,
                "Runs the agent on a message, asking the model for JSON of <typeparamref name=\"TResult\"/>'s schema.");
            Docs.WriteTypeParam(writer, "TResult", "The result's type.");
            run.Write(writer, null, $"{Task}<{response}<TResult>>", "RunAsync<TResult>", "string",
                $"{Runs}.RunAsync<{agent.Type.FullName}, TResult>"
                + $"(host, {user}, settingsId, conversationId, settings, ct);");
            writer.Line();
            run.Write(writer, "Runs the agent on a message, streaming the updates", Updates, "RunStreamingAsync",
                "string", $"{Runs}.RunStreamingAsync(host, {user}, settingsId, conversationId, settings, ct);");
            writer.Line();
            run.Write(writer, "Runs the agent on a message, streaming the updates", Updates, "RunStreamingAsync",
                message, $"{Runs}.RunStreamingAsync(host, input, settingsId, conversationId, settings, ct);");
            writer.Line();

            WriteResume(writer, "ResumeAsync", $"{Task}<{response}>", idType, settings, "ResumeAsync");
            writer.Line();
            WriteResume(writer, "ResumeStreamingAsync", Updates, idType, settings, "ResumeStreamingAsync");
            writer.Line();

            Docs.WriteSummary(
                writer,
                "Resolves the options of a run, for the raw path: <c>CreateChatClientAsync</c>, then the client. "
                + "<c>ChatOptions.ConversationId</c> names the conversation there.");
            Docs.WriteParam(writer, "settingsId", "The id of the stored settings to apply, or null for none.");
            Docs.WriteParam(writer, "settings", "What the run changes over the stored settings; null for nothing.");
            Docs.WriteParam(writer, "ct", "Cancels the resolution.");
            Docs.WriteReturns(writer, "The options, with the agent's tools.");
            writer.Lines(
                $"""
                public {Task}<{ChatOptions}> ResolveOptionsAsync(
                    {idType}? settingsId = null,
                    {settings}? settings = null,
                    {CancellationToken} ct = default) =>
                    {Runs}.ResolveOptionsAsync(host, settingsId, settings, ct);
                """);
            writer.Line();

            // D41: revisions guard every save
            Docs.WriteSummary(writer, "Loads the settings stored under an id.");
            Docs.WriteParam(writer, "settingsId", "The id.");
            Docs.WriteParam(writer, "ct", "Cancels the load.");
            Docs.WriteReturns(
                writer,
                "The settings and the revision <c>SaveSettingsAsync</c> takes; empty settings at revision 0 when none "
                + "are stored.");
            writer.Lines(
                $"""
                public {Task}<global::Typeshift.Stored<{settings}>> LoadSettingsAsync(
                    {idType} settingsId,
                    {CancellationToken} ct = default) =>
                    {Runs}.LoadSettingsAsync<{types}>(host, settingsId, ct);
                """);
            writer.Line();

            Docs.WriteSummary(writer, "Saves settings under an id, unless another save came since they were loaded.");
            Docs.WriteParam(writer, "settingsId", "The id.");
            Docs.WriteParam(writer, "settings", "The settings.");
            Docs.WriteParam(writer, "revision", "The revision <c>LoadSettingsAsync</c> returned.");
            Docs.WriteParam(writer, "ct", "Cancels the save.");
            Docs.WriteReturns(writer, "The new revision.");
            Docs.WriteException(writer, "global::Typeshift.StoreConflictException", "Another save came first.");
            writer.Lines(
                $"""
                public {Task}<long> SaveSettingsAsync(
                    {idType} settingsId,
                    {settings} settings,
                    long revision,
                    {CancellationToken} ct = default) =>
                    {Runs}.SaveSettingsAsync<{types}>(host, settingsId, settings, revision, ct);
                """);
            writer.Line();

            Docs.WriteSummary(
                writer,
                "Changes the settings stored under an id, reloading and changing them again whenever another save "
                + "came first.");
            Docs.WriteParam(writer, "settingsId", "The id.");
            Docs.WriteParam(writer, "configure", "Returns the changed settings; called again after a lost save.");
            Docs.WriteParam(writer, "ct", "Cancels the change.");
            Docs.WriteReturns(writer, "The settings saved.");
            writer.Lines(
                $"""
                public {Task}<{settings}> ConfigureAsync(
                    {idType} settingsId,
                    global::System.Func<{settings}, {settings}> configure,
                    {CancellationToken} ct = default) =>
                    {Runs}.ConfigureAsync<{types}>(host, settingsId, configure, ct);
                """);
        }
    }

    // D50: a conversation's interrupted run, continued without input
    private static void WriteResume(
        SourceWriter writer,
        string name,
        string returns,
        string idType,
        string settings,
        string run)
    {
        Docs.WriteSummary(
            writer,
            "Continues the conversation's run that was interrupted, from its last stored step, without new input.");
        Docs.WriteParam(writer, "conversationId", "The conversation's id.");
        Docs.WriteParam(writer, "settingsId", "The id of the stored settings to apply, or null for none.");
        Docs.WriteParam(writer, "settings", "What the run changes over the stored settings; null for nothing.");
        Docs.WriteParam(writer, "ct", "Cancels the run.");
        Docs.WriteReturns(
            writer,
            returns.Contains("IAsyncEnumerable") ? "The updates." : "The response.");
        Docs.WriteException(
            writer,
            "global::System.InvalidOperationException",
            "The conversation has no interrupted run.");
        writer.Lines(
            $"""
            public {returns} {name}(
                {idType} conversationId,
                {idType}? settingsId = null,
                {settings}? settings = null,
                {CancellationToken} ct = default) =>
                {Runs}.{run}(host, conversationId, settingsId, settings, ct);
            """);
    }

    // The parameters every run takes, after its input
    private sealed class HostRun(string idType, string settings)
    {
        public void Write(SourceWriter writer, string? summary, string returns, string name, string input, string call)
        {
            if (summary is not null)
                Docs.WriteSummary(writer, $"{summary}, with the stored and the run's settings over the agent's code.");

            Docs.WriteParam(writer, "input", "The user's message.");
            Docs.WriteParam(writer, "settingsId", "The id of the stored settings to apply, or null for none.");
            Docs.WriteParam(
                writer,
                "conversationId",
                "The id of the conversation the run resumes and is stored in, or null for none.");
            Docs.WriteParam(writer, "settings", "What the run changes over the stored settings; null for nothing.");
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
                    {idType}? settingsId = null,
                    {idType}? conversationId = null,
                    {settings}? settings = null,
                    {CancellationToken} ct = default) =>
                    {call}
                """);
        }
    }
}
