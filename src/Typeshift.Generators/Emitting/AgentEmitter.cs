using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Emitting;

internal static class AgentEmitter
{
    private const string Task = "global::System.Threading.Tasks.Task";

    private const string CancellationToken = "global::System.Threading.CancellationToken";

    private const string InvalidOperationException = "global::System.InvalidOperationException";

    private const string MissingStore = "No store with ids of <typeparamref name=\"TId\"/> is registered.";

    public static string Emit(AgentOutput output) =>
        TypeEmitter.EmitPartial(
            output.Agent.Type,
            [],
            writer => WriteMembers(writer, output),
            writer => WriteRuntimeExtensions(writer, output.Agent));

    private static void WriteMembers(SourceWriter writer, AgentOutput output)
    {
        var tools = output.Tools.Where(tool => tool.Settings?.Properties.Count > 0).ToList();

        if (tools.Count > 0)
        {
            WriteToolSettings(writer, output.Agent, tools);
            writer.Line();
        }

        WriteSettings(writer, output.Agent, output.Tools);
    }

    // D30: per tool with settings a field, set through its Configure{Tool}() builder; nothing for any other tool
    private static void WriteToolSettings(SourceWriter writer, AgentModel agent, IReadOnlyList<ResolvedTool> tools)
    {
        foreach (var tool in tools)
        {
            writer.Line($"private {tool.Settings!.Type}? {Field(tool)};");
        }

        writer.Line();
        Docs.WriteInheritDoc(writer);
        writer.ToolSwitch(
            "protected override object? GetToolSettings(global::Typeshift.Tools.ToolName tool) =>",
            "tool.Value",
            [.. tools.Select(tool => (tool.Name, Field(tool)))],
            "null");

        foreach (var tool in tools)
        {
            var name = Names.ToolProperty(tool.Name);

            writer.Line();
            Docs.WriteSummary(
                writer,
                $"Sets the settings of the <c>{tool.Name}</c> tool in code, over its code defaults. The tool then "
                + "ignores its stored defaults; the agent's stored settings and run settings still change its "
                + "configurable values.");
            Docs.WriteReturns(writer, "A builder of the tool's settings.");
            writer.Line($"private {name}SettingsBuilder Configure{name}() => new(this);");
            writer.Line();
            WriteToolSettingsBuilder(writer, agent, tool, $"{name}SettingsBuilder");
        }
    }

    private static void WriteToolSettingsBuilder(SourceWriter writer, AgentModel agent, ResolvedTool tool, string type)
    {
        var field = $"agent.{Field(tool)}";

        Docs.WriteSummary(writer, $"Sets the settings of the <c>{tool.Name}</c> tool.");

        using (writer.Block($"private sealed class {type}({agent.Type.FullName} agent)"))
        {
            foreach (var property in tool.Settings!.Properties)
            {
                if (property != tool.Settings.Properties[0]) writer.Line();

                Docs.WriteSummary(
                    writer,
                    $"Sets <c>{property.Name}</c>{(property.Summary is { } summary ? $": {Docs.Escape(summary)}" : ".")}");
                Docs.WriteParam(writer, "value", "The value.");
                Docs.WriteReturns(writer, "The same builder, to set further values.");
                writer.Line($"public {type} With{property.Name}({property.Type} value)");

                using (writer.Block())
                {
                    writer.Line($"{field} = ({field} ?? new()) with {{ {Names.Identifier(property.Name)} = value }};");
                    writer.Line("return this;");
                }
            }
        }
    }

    // _searchWebSettings for search_web; tool names are unique, so are the fields
    private static string Field(ResolvedTool tool)
    {
        var name = Names.ToPascalCase(tool.Name);
        return $"_{char.ToLowerInvariant(name[0])}{name.Substring(1)}Settings";
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
            $"The settings of <see cref=\"{agent.Type.FullName}\"/> that may change at runtime: "
            + "its model, its configurable chat options and the configurable settings of its tools. A null value "
            + "keeps what the layer below sets. Store them per id through the agent's runtime, or pass them for a "
            + "single run.");
        writer.Lines(
            """
            public sealed record Settings :
                global::Typeshift.Agents.IAgentSettings,
                global::Typeshift.Persistence.IStorable<Settings>
            """);

        using (writer.Block())
        {
            Docs.WriteSummary(
                writer,
                "The model the agent runs on, as the registered chat client provider names it. Null keeps the "
                + "model of the agent's chat options.");
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
            "void global::Typeshift.Agents.IAgentSettings.ApplyTo("
            + "global::Microsoft.Extensions.AI.ChatOptions options)");

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

    private static void WriteGetToolOverrides(SourceWriter writer, IReadOnlyList<StoredTool> tools) =>
        writer.ToolSwitch(
            """
            global::Typeshift.Settings.IToolOverrides? global::Typeshift.Agents.IAgentSettings.GetToolOverrides(
                global::Typeshift.Tools.ToolName tool) =>
            """,
            "tool.Value",
            [.. tools.Select(tool => (tool.Name, tool.Property))],
            "null");

    // The typed members of the agent's runtime, as an extension block so the settings type never needs naming
    private static void WriteRuntimeExtensions(SourceWriter writer, AgentModel agent)
    {
        var settings = $"{agent.Type.FullName}.Settings";

        writer.Line();
        Docs.WriteSummary(writer, $"The typed members of the runtime of <see cref=\"{agent.Type.FullName}\"/>.");

        using (writer.Block($"{agent.Type.Accessibility} static class {agent.Type.FlatName}RuntimeExtensions"))
        {
            using (writer.Block($"extension(global::Typeshift.Agents.AgentRuntime<{agent.Type.FullName}> runtime)"))
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
        Docs.WriteException(writer, InvalidOperationException, MissingStore);
        writer.Lines(
            $"""
            public {Task}<{settings}> LoadSettingsAsync<TId>(
                TId id,
                {CancellationToken} ct = default)
                where TId : notnull =>
                runtime.LoadSettingsCoreAsync<{settings}, TId>(id, ct);
            """);
    }

    private static void WriteSaveSettings(SourceWriter writer, string settings)
    {
        Docs.WriteSummary(writer, "Saves the agent's settings under an id, replacing what was stored there.");
        Docs.WriteTypeParam(writer, "TId", "The id type of the registered store.");
        Docs.WriteParam(writer, "id", "The id to save the settings under.");
        Docs.WriteParam(writer, "settings", "The settings; a null value keeps what the agent's code sets.");
        Docs.WriteParam(writer, "ct", "Cancels the save.");
        Docs.WriteException(writer, InvalidOperationException, MissingStore);
        writer.Lines(
            $"""
            public {Task} SaveSettingsAsync<TId>(
                TId id,
                {settings} settings,
                {CancellationToken} ct = default)
                where TId : notnull =>
                runtime.SaveSettingsCoreAsync(id, settings, ct);
            """);
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
        Docs.WriteException(writer, InvalidOperationException, MissingStore);
        writer.Lines(
            $"""
            public {Task}<{settings}> ConfigureAsync<TId>(
                TId id,
                global::System.Func<{settings}, {settings}> configure,
                {CancellationToken} ct = default)
                where TId : notnull =>
                runtime.ConfigureSettingsCoreAsync(id, configure, ct);
            """);
    }

    private static void WriteResolveChatOptions(SourceWriter writer, string settings)
    {
        Docs.WriteSummary(
            writer,
            "Resolves the chat options of a run from the agent's code alone, without a store.");
        Docs.WriteParam(writer, "run", "Settings for this run only; a null value keeps what the code sets.");
        Docs.WriteParam(writer, "conversationId", "The conversation the run continues, or null.");
        Docs.WriteReturns(writer, "The options, with the agent's tools, to create the chat client with and call it with.");
        writer.Lines(
            $"""
            public global::Microsoft.Extensions.AI.ChatOptions ResolveChatOptions(
                {settings}? run = null,
                string? conversationId = null) =>
                runtime.ResolveChatOptionsCore(run, conversationId);
            """);
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
        Docs.WriteException(writer, InvalidOperationException, MissingStore);
        writer.Lines(
            $"""
            public {Task}<global::Microsoft.Extensions.AI.ChatOptions> ResolveChatOptionsAsync<TId>(
                TId id,
                {settings}? run = null,
                string? conversationId = null,
                {CancellationToken} ct = default)
                where TId : notnull =>
                runtime.ResolveChatOptionsCoreAsync<{settings}, TId>(id, run, conversationId, ct);
            """);
    }
}
