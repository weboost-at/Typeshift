using System.Collections.Generic;
using System.Linq;
using Agentic.Generators.Models;

namespace Agentic.Generators.Emitting;

internal static class AgentEmitter
{
    private const string Task = "global::System.Threading.Tasks.Task";

    private const string CancellationToken = "global::System.Threading.CancellationToken";

    private const string MissingStore = "No store with ids of <typeparamref name=\"TId\"/> is registered.";

    public static string Emit(AgentOutput output) =>
        TypeEmitter.EmitPartial(
            output.Agent.Type,
            [],
            writer => WriteMembers(writer, output),
            writer => WriteRuntimeExtensions(writer, output.Agent));

    private static void WriteMembers(SourceWriter writer, AgentOutput output)
    {
        var settingsTypes = output.Tools
            .Where(tool => tool.Settings is not null)
            .GroupBy(tool => tool.Settings!)
            .ToList();

        if (settingsTypes.Count > 0)
        {
            WriteToolSettings(writer, settingsTypes);
            writer.Line();
        }

        WriteSettings(writer, output.Agent, output.Tools);
    }

    // A field and a WithToolSettings overload per settings type the agent's tools read, nothing for any other type
    private static void WriteToolSettings(SourceWriter writer, IReadOnlyList<IGrouping<string, ResolvedTool>> types)
    {
        var fields = GetFieldNames(types.Select(type => type.Key).ToList());

        foreach (var type in types)
        {
            writer.Line($"private {type.Key}? {fields[type.Key]};");
        }

        foreach (var type in types)
        {
            var tools = string.Join(", ", type.Select(tool => $"<c>{tool.Name}</c>"));

            writer.Line();
            Docs.WriteSummary(
                writer,
                $"Sets the settings the agent's tools reading them run with, {tools}, as a whole object. Stored "
                + "settings and run settings then change only their configurable members.");
            Docs.WriteParam(writer, "settings", "The settings.");
            writer.Line($"private void WithToolSettings({type.Key} settings) =>");

            using (writer.Indent())
            {
                writer.Line($"{fields[type.Key]} = settings;");
            }
        }

        writer.Line();
        Docs.WriteInheritDoc(writer);
        writer.Line("protected override object? GetToolSettings(global::Agentic.Tools.ToolName tool) =>");

        using (writer.Indent())
        {
            writer.Line("tool.Value switch");

            using (writer.Block(close: "};"))
            {
                foreach (var tool in types.SelectMany(type => type))
                {
                    writer.Line($"{Names.Literal(tool.Name)} => {fields[tool.Settings!]},");
                }

                writer.Line("_ => null");
            }
        }
    }

    // _findCompaniesSettings for FindCompaniesSettings, numbered when two namespaces share a type name
    private static Dictionary<string, string> GetFieldNames(IReadOnlyList<string> types)
    {
        var fields = new Dictionary<string, string>();

        foreach (var type in types)
        {
            var name = type.Substring(type.LastIndexOf('.') + 1);
            var field = "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
            var taken = fields.Values.Count(existing => existing == field || existing.StartsWith(field + "_"));

            fields[type] = taken == 0 ? field : $"{field}_{taken + 1}";
        }

        return fields;
    }

    private static void WriteSettings(SourceWriter writer, AgentModel agent, IReadOnlyList<ResolvedTool> tools)
    {
        var configurableTools = tools.Where(tool => tool.HasOverrides).ToList();

        // A tool property never clashes with the model, a chat option or a member of the record
        var reserved = new HashSet<string>(agent.ConfigurableOptions.Select(option => option.Name))
        {
            "Deserialize", "EqualityContract", "Equals", "GetHashCode", "GetType", "Model", "PrintMembers",
            "Serialize", "Settings", "ToString"
        };

        var members = new[] { new StoredMember("Model", "model", new StorageModel("Write", "ReadString")) }
            .Concat(agent.ConfigurableOptions.Select(option =>
                new StoredMember(option.Name, Names.JsonName(option.Name), option.Storage)))
            .ToList();

        var stored = configurableTools
            .Select(tool => new StoredTool(
                Names.Unreserved(Names.ToPascalCase(tool.Name), "Tool", reserved),
                tool.Name,
                $"{tool.Toolset}.{Names.OverridesName(tool.Name)}"))
            .ToList();

        Docs.WriteSummary(
            writer,
            $"The settings of <see cref=\"{agent.Type.FullName.Replace("global::", "")}\"/> that may change at runtime: "
            + "its model, its configurable chat options and the configurable settings of its tools. A null value "
            + "keeps what the layer below sets. Store them per id through the agent's runtime, or pass them for a "
            + "single run.");
        writer.Line("public sealed record Settings :");

        using (writer.Indent())
        {
            writer.Line("global::Agentic.Agents.IAgentSettings,");
            writer.Line("global::Agentic.Persistence.IStorable<Settings>");
        }

        using (writer.Block())
        {
            Docs.WriteSummary(
                writer,
                "The model the agent runs on, as the registered chat client provider names it. Null keeps the "
                + "<c>ModelId</c> of the agent's chat options.");
            writer.Line("public string? Model { get; init; }");

            foreach (var option in agent.ConfigurableOptions)
            {
                writer.Line();
                Docs.WriteSummary(
                    writer,
                    $"Overrides the <c>{option.Name}</c> of the agent's chat options. Null keeps the value of the layer "
                    + "below.");
                writer.Line($"public {option.NullableType} {Names.Identifier(option.Name)} {{ get; init; }}");
            }

            for (var index = 0; index < configurableTools.Count; index++)
            {
                // The overrides of an internal toolset cannot be part of a public agent's settings
                var accessibility = configurableTools[index].IsPublic ? "public" : "internal";

                writer.Line();
                Docs.WriteSummary(
                    writer,
                    $"Overrides the configurable settings of the agent's <c>{stored[index].Name}</c> tool.");
                writer.Line($"{accessibility} {stored[index].Type} {stored[index].Property} {{ get; init; }} = new();");
            }

            writer.Line();
            StorableEmitter.WriteSerialize(writer, members, stored);
            writer.Line();
            StorableEmitter.WriteDeserialize(writer, "Settings", members, stored);
            writer.Line();
            WriteApplyTo(writer, agent);
            writer.Line();
            WriteGetToolOverrides(writer, stored);
        }
    }

    private static void WriteApplyTo(SourceWriter writer, AgentModel agent)
    {
        writer.Line(
            "void global::Agentic.Agents.IAgentSettings.ApplyTo(global::Microsoft.Extensions.AI.ChatOptions options)");

        using (writer.Block())
        {
            writer.Line("if (Model is { } model) options.ModelId = model;");

            foreach (var option in agent.ConfigurableOptions)
            {
                var property = Names.Identifier(option.Name);
                var local = Names.Identifier(Names.JsonName(option.Name));

                writer.Line($"if ({property} is {{ }} {local}) options.{property} = {local};");
            }
        }
    }

    private static void WriteGetToolOverrides(SourceWriter writer, IReadOnlyList<StoredTool> tools)
    {
        writer.Line("global::Agentic.Settings.IToolOverrides? global::Agentic.Agents.IAgentSettings.GetToolOverrides(");

        using (writer.Indent())
        {
            if (tools.Count == 0)
            {
                writer.Line("global::Agentic.Tools.ToolName tool) =>");
                writer.Line("null;");
                return;
            }

            writer.Line("global::Agentic.Tools.ToolName tool) =>");
            writer.Line("tool.Value switch");

            using (writer.Block(close: "};"))
            {
                foreach (var tool in tools)
                {
                    writer.Line($"{Names.Literal(tool.Name)} => {tool.Property},");
                }

                writer.Line("_ => null");
            }
        }
    }

    // The typed members of the agent's runtime, as an extension block so the settings type never needs naming
    private static void WriteRuntimeExtensions(SourceWriter writer, AgentModel agent)
    {
        var name = agent.Type.FullName.Replace("global::", "");
        var settings = $"{agent.Type.FullName}.Settings";

        writer.Line();
        Docs.WriteSummary(writer, $"The typed members of the runtime of <see cref=\"{name}\"/>.");

        using (writer.Block($"{agent.Type.Accessibility} static class {agent.Type.FlatName}RuntimeExtensions"))
        {
            using (writer.Block($"extension(global::Agentic.Agents.AgentRuntime<{agent.Type.FullName}> runtime)"))
            {
                WriteLoadSettings(writer, settings);
                writer.Line();
                WriteSaveSettings(writer, settings);
                writer.Line();
                WriteConfigure(writer, settings);
                writer.Line();
                WriteResolveChatOptions(writer, settings);
                writer.Line();
                WriteResolveStoredChatOptions(writer, settings);
            }
        }
    }

    private static void WriteLoadSettings(SourceWriter writer, string settings)
    {
        Docs.WriteSummary(writer, "Loads the agent's settings stored under an id.");
        Docs.WriteTypeParam(writer, "TId", "The id type of the registered store.");
        Docs.WriteParam(writer, "id", "The id the settings were saved under.");
        Docs.WriteParam(writer, "ct", "Cancels the load.");
        Docs.WriteReturns(writer, "The stored settings, or empty settings when none are stored.");
        Docs.WriteException(writer, "System.InvalidOperationException", MissingStore);
        writer.Line($"public {Task}<{settings}> LoadSettingsAsync<TId>(");

        using (writer.Indent())
        {
            writer.Line("TId id,");
            writer.Line($"{CancellationToken} ct = default)");
            writer.Line("where TId : notnull =>");
            writer.Line($"runtime.LoadSettingsCoreAsync<{settings}, TId>(id, ct);");
        }
    }

    private static void WriteSaveSettings(SourceWriter writer, string settings)
    {
        Docs.WriteSummary(writer, "Saves the agent's settings under an id, replacing what was stored there.");
        Docs.WriteTypeParam(writer, "TId", "The id type of the registered store.");
        Docs.WriteParam(writer, "id", "The id to save the settings under.");
        Docs.WriteParam(writer, "settings", "The settings; a null value keeps what the agent's code sets.");
        Docs.WriteParam(writer, "ct", "Cancels the save.");
        Docs.WriteException(writer, "System.InvalidOperationException", MissingStore);
        writer.Line($"public {Task} SaveSettingsAsync<TId>(");

        using (writer.Indent())
        {
            writer.Line("TId id,");
            writer.Line($"{settings} settings,");
            writer.Line($"{CancellationToken} ct = default)");
            writer.Line("where TId : notnull =>");
            writer.Line("runtime.SaveSettingsCoreAsync(id, settings, ct);");
        }
    }

    private static void WriteConfigure(SourceWriter writer, string settings)
    {
        Docs.WriteSummary(writer, "Changes the agent's settings stored under an id and saves them.");
        Docs.WriteTypeParam(writer, "TId", "The id type of the registered store.");
        Docs.WriteParam(writer, "id", "The id the settings are stored under.");
        Docs.WriteParam(
            writer,
            "configure",
            "Returns the changed settings, <c>settings => settings with { Temperature = 0.4f }</c>.");
        Docs.WriteParam(writer, "ct", "Cancels the load and the save.");
        Docs.WriteReturns(writer, "The saved settings.");
        Docs.WriteException(writer, "System.InvalidOperationException", MissingStore);
        writer.Line($"public {Task}<{settings}> ConfigureAsync<TId>(");

        using (writer.Indent())
        {
            writer.Line("TId id,");
            writer.Line($"global::System.Func<{settings}, {settings}> configure,");
            writer.Line($"{CancellationToken} ct = default)");
            writer.Line("where TId : notnull =>");
            writer.Line("runtime.ConfigureSettingsCoreAsync(id, configure, ct);");
        }
    }

    private static void WriteResolveChatOptions(SourceWriter writer, string settings)
    {
        Docs.WriteSummary(
            writer,
            "Resolves the chat options of a run from the agent's code alone, without a store.");
        Docs.WriteParam(writer, "run", "Settings for this run only; a null value keeps what the code sets.");
        Docs.WriteParam(writer, "conversationId", "The conversation the run continues, or null.");
        Docs.WriteReturns(writer, "The options, with the agent's tools, to create the chat client with and call it with.");
        writer.Line("public global::Microsoft.Extensions.AI.ChatOptions ResolveChatOptions(");

        using (writer.Indent())
        {
            writer.Line($"{settings}? run = null,");
            writer.Line("string? conversationId = null) =>");
            writer.Line("runtime.ResolveChatOptionsCore(run, conversationId);");
        }
    }

    private static void WriteResolveStoredChatOptions(SourceWriter writer, string settings)
    {
        Docs.WriteSummary(
            writer,
            "Resolves the chat options of a run from the agent's code, the settings stored under an id and the "
            + "defaults stored under the same id for its tools.");
        Docs.WriteTypeParam(writer, "TId", "The id type of the registered store.");
        Docs.WriteParam(writer, "id", "The id the agent's settings and its tools' defaults are stored under.");
        Docs.WriteParam(writer, "run", "Settings for this run only; a null value keeps what the layers below set.");
        Docs.WriteParam(writer, "conversationId", "The conversation the run continues, or null.");
        Docs.WriteParam(writer, "ct", "Cancels the loads.");
        Docs.WriteReturns(writer, "The options, with the agent's tools, to create the chat client with and call it with.");
        Docs.WriteException(writer, "System.InvalidOperationException", MissingStore);
        writer.Line($"public {Task}<global::Microsoft.Extensions.AI.ChatOptions> ResolveChatOptionsAsync<TId>(");

        using (writer.Indent())
        {
            writer.Line("TId id,");
            writer.Line($"{settings}? run = null,");
            writer.Line("string? conversationId = null,");
            writer.Line($"{CancellationToken} ct = default)");
            writer.Line("where TId : notnull =>");
            writer.Line($"runtime.ResolveChatOptionsCoreAsync<{settings}, TId>(id, run, conversationId, ct);");
        }
    }
}
