using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Emitting;

internal static class ToolsetEmitter
{
    private const string ToolParameterList =
        "global::System.Collections.Generic.IReadOnlyList<global::Typeshift.Tools.ToolParameter>";

    private const string ToolBinding = "global::Typeshift.Tools.ToolBinding";

    private const string ToolDefinition = "global::Typeshift.Toolsets.ToolDefinition";

    private const string ToolName = "global::Typeshift.Tools.ToolName";

    public static string Emit(ToolsetModel toolset) =>
        TypeEmitter.EmitPartial(
            toolset.Type,
            toolset.Tools.Select(GetStamp),
            writer =>
            {
                WriteToolReferences(writer, toolset);

                foreach (var tool in toolset.Tools.Where(HasOverrides))
                {
                    writer.Line();
                    WriteOverrides(writer, tool, tool.Settings!);
                }

                writer.Line();
                WriteGetBinding(writer, toolset);

                foreach (var tool in toolset.Tools)
                {
                    writer.Line();
                    WriteBinding(writer, tool);
                    writer.Line();
                    WriteFunction(writer, tool);
                }
            },
            writer => WriteToolList(writer, toolset));

    private static bool HasOverrides(ToolModel tool) => tool.Settings?.HasConfigurable is true;

    // A tool whose method takes no settings is warned about (TSH009) and created without them
    private static bool ReadsSettings(ToolModel tool) =>
        tool.Parameters.Any(parameter => parameter.Kind == ParameterKind.Settings);

    // Other compilations read the tools of this toolset from these stamps
    private static string GetStamp(ToolModel tool)
    {
        var settings = tool.Settings is { } model ? $"typeof({model.Type})" : "null";

        var configurable = tool.Settings?.Properties
            .Where(property => property.IsConfigurable)
            .Select(property => $", {Names.Literal(property.Name)}") ?? [];

        return $"global::Typeshift.Modules.GeneratedTool({Names.Literal(tool.Name)}, {settings}{string.Concat(configurable)})";
    }

    private static void WriteToolReferences(SourceWriter writer, ToolsetModel toolset)
    {
        Docs.WriteSummary(writer, "Typed references to the tools of this toolset.");

        using (writer.Block("public static class Tools"))
        {
            foreach (var tool in toolset.Tools)
            {
                if (tool != toolset.Tools[0]) writer.Line();

                Docs.WriteSummary(writer, $"The <c>{tool.Name}</c> tool.");
                writer.Line(
                    $"public static {RefType(toolset, tool)} {Names.ToolProperty(tool.Name)} {{ get; }} = "
                    + $"new({Names.Literal(tool.Name)});");
            }
        }
    }

    private static string RefType(ToolsetModel toolset, ToolModel tool) =>
        HasOverrides(tool)
            ? $"global::Typeshift.Tools.ToolRef<{tool.Settings!.Type}, "
                + $"{toolset.Type.FullName}.{Names.OverridesName(tool.Name)}>"
            : "global::Typeshift.Tools.ToolRef";

    // The tool's configurable members, nullable: what a UI edits, a store keeps and a run passes
    private static void WriteOverrides(SourceWriter writer, ToolModel tool, SettingsModel settings)
    {
        var type = Names.OverridesName(tool.Name);
        var configurable = settings.Properties.Where(property => property.IsConfigurable).ToList();

        // A value never clashes with a member of the record or the record itself
        string[] reserved =
        [
            type, "Deserialize", "EqualityContract", "Equals", "GetHashCode", "GetType", "PrintMembers", "Serialize",
            "ToString"
        ];

        var members = configurable
            .Select(property => new StoredMember(
                Names.Unreserved(property.Name, "Value", reserved),
                Names.JsonName(property.Name),
                property.Storage!))
            .ToList();

        Docs.WriteSummary(
            writer,
            $"Overrides the configurable settings of the <c>{tool.Name}</c> tool, as stored tool defaults, as "
            + "an agent's stored settings or for a single run. A null value keeps what the layer below sets.");
        writer.Lines(
            $"""
            public sealed record {type} :
                global::Typeshift.Settings.IToolOverrides<{settings.Type}>,
                global::Typeshift.Persistence.IStorable<{type}>
            """);

        using (writer.Block())
        {
            for (var index = 0; index < configurable.Count; index++)
            {
                var property = configurable[index];
                var summary = property.Summary is { } text ? Docs.Escape(text) : $"Overrides <c>{property.Name}</c>.";

                Docs.WriteSummary(writer, $"{summary} Null keeps the value of the layer below.");
                writer.Line($"public {property.NullableType} {Names.Identifier(members[index].Property)} {{ get; init; }}");
                writer.Line();
            }

            writer.Line(
                $"{ToolName} global::Typeshift.Settings.IToolOverrides.Tool => new({Names.Literal(tool.Name)});");
            writer.Line();
            StorableEmitter.WriteSerialize(writer, members, []);
            writer.Line();
            StorableEmitter.WriteDeserialize(writer, type, members, []);
            writer.Line();
            writer.Lines(
                $"""
                {settings.Type} global::Typeshift.Settings.IToolOverrides<{settings.Type}>.ApplyTo(
                    {settings.Type} settings) =>
                """);

            using (writer.Indent())
            {
                writer.Line("settings with");

                using (writer.Block(close: "};"))
                {
                    for (var index = 0; index < configurable.Count; index++)
                    {
                        var setting = Names.Identifier(configurable[index].Name);
                        var value = Names.Identifier(members[index].Property);
                        var separator = index < configurable.Count - 1 ? "," : "";

                        writer.Line($"{setting} = {value} ?? settings.{setting}{separator}");
                    }
                }
            }
        }
    }

    // D29: what agents pick in UseToolset<T>().Include(t => t.X), next to the toolset in its namespace
    private static void WriteToolList(SourceWriter writer, ToolsetModel toolset)
    {
        var list = $"global::Typeshift.Tools.ToolList<{toolset.Type.FullName}>";

        writer.Line();
        Docs.WriteSummary(
            writer,
            $"The tools of <see cref=\"{toolset.Type.FullName.Replace("global::", "")}\"/>, to include in or exclude "
            + $"from an agent: <c>UseToolset&lt;{toolset.Type.Name}&gt;().Include(t => t.Name)</c>.");

        using (writer.Block($"{toolset.Type.Accessibility} static class {toolset.Type.FlatName}ToolList"))
        {
            using (writer.Block($"extension({list} tools)"))
            {
                foreach (var tool in toolset.Tools)
                {
                    if (tool != toolset.Tools[0]) writer.Line();

                    var description = tool.Description is { } text ? $": {Docs.Escape(text)}" : ".";
                    var property = Names.ToolProperty(tool.Name);

                    Docs.WriteSummary(writer, $"The <c>{tool.Name}</c> tool{description}");
                    writer.Line(
                        $"public {RefType(toolset, tool)} {property} => {toolset.Type.FullName}.Tools.{property};");
                }
            }
        }
    }

    private static void WriteGetBinding(SourceWriter writer, ToolsetModel toolset)
    {
        Docs.WriteInheritDoc(writer);
        writer.ToolSwitch(
            $"protected override {ToolBinding} GetBinding({ToolDefinition} tool) =>",
            "tool.Name.Value",
            [.. toolset.Tools.Select(tool => (tool.Name, $"{Names.ToPascalCase(tool.Name)}Binding"))],
            "base.GetBinding(tool)");
    }

    private static void WriteBinding(SourceWriter writer, ToolModel tool)
    {
        var name = Names.ToPascalCase(tool.Name);
        var settings = ReadsSettings(tool) ? "settings, " : "";

        var create = tool.Settings switch
        {
            null => "Create",
            { } model when HasOverrides(tool) => $"Create<{model.Type}, {Names.OverridesName(tool.Name)}>",
            { } model => $"Create<{model.Type}>"
        };

        var function = tool.Settings is null
            ? $"static (tool, json) => new {name}Function(tool, json)"
            : $"static (tool, settings, json) => new {name}Function(tool, {settings}json)";

        writer.Lines(
            $"""
            private static readonly {ToolBinding} {name}Binding =
                {ToolBinding}.{create}(
                    {function});
            """);
    }

    private static void WriteFunction(SourceWriter writer, ToolModel tool)
    {
        var name = Names.ToPascalCase(tool.Name);

        var settings = ReadsSettings(tool) ? $"\n    {tool.Settings!.Type} settings," : "";

        writer.Lines(
            $"""
            private sealed class {name}Function(
                {ToolDefinition} tool,{settings}
                global::Typeshift.Json.AgentJson json)
                : global::Typeshift.Tools.GeneratedToolFunction(tool, json)
            """);

        using (writer.Block())
        {
            WriteParameters(writer, tool);
            writer.Line();

            if (tool.Description is { } description)
            {
                writer.Line($"public override string Description => {Names.Literal(description)};");
                writer.Line();
            }

            writer.Line($"protected override {ToolParameterList} Parameters => ModelParameters;");
            writer.Line();
            WriteInvoke(writer, tool);
        }
    }

    private static void WriteParameters(SourceWriter writer, ToolModel tool)
    {
        var parameters = tool.Parameters.Where(parameter => parameter.Kind == ParameterKind.Argument).ToList();

        if (parameters.Count == 0)
        {
            writer.Line($"private static readonly {ToolParameterList} ModelParameters = [];");
            return;
        }

        writer.Line($"private static readonly {ToolParameterList} ModelParameters =");

        using (writer.Block(open: "[", close: "];"))
        {
            foreach (var parameter in parameters)
            {
                var separator = parameter == parameters[parameters.Count - 1] ? "" : ",";
                var initializers = new[]
                    {
                        parameter.Description is { } description ? $"Description = {Names.Literal(description)}" : null,
                        parameter.HasDefaultValue ? "HasDefaultValue = true" : null,
                        parameter.DefaultValue is { } value and not "null" ? $"DefaultValue = {value}" : null,
                        parameter.IsNullable ? "IsNullable = true" : null
                    }
                    .OfType<string>()
                    .ToList();

                var construction = $"new({Names.Literal(parameter.Name)}, typeof({parameter.TypeOf}))";

                if (initializers.Count == 0)
                {
                    writer.Line(construction + separator);
                    continue;
                }

                writer.Line(construction);

                using (writer.Block(close: "}" + separator))
                {
                    for (var index = 0; index < initializers.Count; index++)
                    {
                        writer.Line(initializers[index] + (index < initializers.Count - 1 ? "," : ""));
                    }
                }
            }
        }
    }

    private static void WriteInvoke(SourceWriter writer, ToolModel tool)
    {
        var kind = tool.Return;
        var isAsync = kind is ReturnKind.Awaitable or ReturnKind.AwaitableValue;
        var isExpression = kind is ReturnKind.Value or ReturnKind.AwaitableValue;
        var call = $"{tool.Target}.{Names.Identifier(tool.Method)}";
        var arguments = tool.Parameters.Select(GetArgument).ToList();

        var modifiers = isAsync ? "override async" : "override";

        writer.Lines(
            $"""
            protected {modifiers} global::System.Threading.Tasks.ValueTask<object?> InvokeToolAsync(
                global::Microsoft.Extensions.AI.AIFunctionArguments arguments,
                global::System.IServiceProvider services,
                global::System.Threading.CancellationToken ct){(isExpression ? " =>" : "")}
            """);

        using (writer.Indent())
        {
            if (kind == ReturnKind.Value) WriteCall(writer, "new(GetResult(", call, arguments, ")));");
            if (kind == ReturnKind.AwaitableValue) WriteCall(writer, "GetResult(await ", call, arguments, "));");
        }

        if (isExpression) return;

        using (writer.Block())
        {
            WriteCall(writer, kind == ReturnKind.Awaitable ? "await " : "", call, arguments, ");");
            if (arguments.Count > 1) writer.Line();
            writer.Line(kind == ReturnKind.Awaitable ? "return null;" : "return default;");
        }
    }

    private static void WriteCall(
        SourceWriter writer,
        string prefix,
        string call,
        IReadOnlyList<string> arguments,
        string suffix)
    {
        if (arguments.Count < 2)
        {
            writer.Line($"{prefix}{call}({string.Concat(arguments)}{suffix}");
            return;
        }

        writer.Line($"{prefix}{call}(");

        using (writer.Indent())
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                writer.Line(arguments[index] + (index < arguments.Count - 1 ? "," : suffix));
            }
        }
    }

    private static string GetArgument(ParameterModel parameter) =>
        parameter.Kind switch
        {
            ParameterKind.Services => "services",
            ParameterKind.Settings => "settings",
            ParameterKind.CancellationToken => "ct",
            ParameterKind.Arguments => "arguments",
            _ when parameter.HasDefaultValue =>
                $"GetArgument<{parameter.Type}>(arguments, {Names.Literal(parameter.Name)}, {parameter.DefaultValue})",
            _ => $"GetArgument<{parameter.Type}>(arguments, {Names.Literal(parameter.Name)})"
        };
}
