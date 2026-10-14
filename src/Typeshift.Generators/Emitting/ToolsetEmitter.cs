using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Emitting;

internal static class ToolsetEmitter
{
    private const string ToolParameterList =
        "global::System.Collections.Generic.IReadOnlyList<global::Typeshift.Generated.ToolParameter>";

    private const string ToolJson = "global::Typeshift.Generated.ToolJson";

    private const string AIFunction = "global::Microsoft.Extensions.AI.AIFunction";

    private const string Hidden =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";

    // The members of a record the overrides' properties must not be named like
    private static readonly string[] RecordMembers =
        ["ApplyTo", "Clone", "Deconstruct", "EqualityContract", "Equals", "GetHashCode", "PrintMembers", "ToString"];

    public static string Emit(ToolsetModel toolset) =>
        TypeEmitter.EmitPartial(
            toolset.Type,
            [],
            writer =>
            {
                foreach (var tool in toolset.Tools)
                {
                    if (tool != toolset.Tools[0]) writer.Line();

                    if (HasOverrides(tool))
                    {
                        WriteOverrides(writer, tool, tool.Settings!);
                        writer.Line();
                    }

                    WriteFactory(writer, tool);
                    writer.Line();
                    WriteFunction(writer, tool);
                }
            },
            writer =>
            {
                if (toolset.IsReachable) WriteToolList(writer, toolset);
            });

    private static bool HasOverrides(ToolModel tool) => tool.Settings?.HasConfigurable is true;

    // The tool's configurable members, nullable: what a settings form edits, a store keeps and a run passes
    private static void WriteOverrides(SourceWriter writer, ToolModel tool, SettingsModel settings)
    {
        var type = Names.OverridesName(tool.Name);
        var configurable = settings.Properties.Where(property => property.IsConfigurable).ToList();
        string[] reserved = [type, .. RecordMembers];

        Docs.WriteSummary(
            writer,
            $"The configurable settings of the <c>{tool.Name}</c> tool, as an agent's stored settings or a run "
            + "override them. A null value keeps what the layer below sets.");

        using (writer.Block($"public sealed record {type}"))
        {
            foreach (var property in configurable)
            {
                var summary = property.Summary is { } text ? Docs.Escape(text) : $"Overrides <c>{property.Name}</c>.";

                Docs.WriteSummary(writer, $"{summary} Null keeps the value of the layer below.");
                writer.Line(
                    $"public {property.NullableType} {Member(property, reserved)} {{ get; init; }}");
                writer.Line();
            }

            Docs.WriteSummary(writer, "Applies the values this layer sets over the settings of the layer below.");
            Docs.WriteParam(writer, "settings", "The settings of the layer below.");
            Docs.WriteReturns(writer, "The settings with every value this layer sets.");
            writer.Line(Hidden);
            writer.Line($"public {settings.Type} ApplyTo({settings.Type} settings) =>");

            using (writer.Indent())
            {
                writer.Line("settings with");

                using (writer.Block(close: "};"))
                {
                    writer.List(
                    [
                        .. configurable.Select(property =>
                            $"{Names.Identifier(property.Name)} = {Member(property, reserved)} ?? "
                            + $"settings.{Names.Identifier(property.Name)}")
                    ]);
                }
            }
        }
    }

    // D6: a member named like one of the record's own gets a suffix instead of clashing with it
    private static string Member(SettingsPropertyModel property, string[] reserved) =>
        Names.Identifier(Names.Unreserved(property.Name, "Value", reserved));

    private static void WriteFactory(SourceWriter writer, ToolModel tool)
    {
        var arguments = tool.Settings is not null && ReadsSettings(tool) ? "settings, json" : "json";

        Docs.WriteSummary(writer, $"Creates the <c>{tool.Name}</c> tool; generated agents call it.");

        if (tool.Settings is not null) Docs.WriteParam(writer, "settings", "The settings the tool runs with.");

        Docs.WriteParam(writer, "json", "How the tool binds its arguments and serializes its result.");
        Docs.WriteReturns(writer, "The tool.");
        writer.Line(Hidden);
        writer.Line($"public static {AIFunction} {Names.FactoryName(tool.Name)}(");

        using (writer.Indent())
        {
            if (tool.Settings is { } model) writer.Line($"{model.Type} settings,");

            writer.Line($"{ToolJson} json) =>");
            writer.Line($"new {Names.FunctionName(tool.Name)}({arguments});");
        }
    }

    // A tool whose method takes no settings is warned about (TSH010) and runs without them
    private static bool ReadsSettings(ToolModel tool) =>
        tool.Parameters.Any(parameter => parameter.Kind == ParameterKind.Settings);

    private static void WriteFunction(SourceWriter writer, ToolModel tool)
    {
        writer.Line($"private sealed class {Names.FunctionName(tool.Name)}(");

        using (writer.Indent())
        {
            if (ReadsSettings(tool)) writer.Line($"{tool.Settings!.Type} settings,");

            writer.Line($"{ToolJson} json)");
            writer.Line(": global::Typeshift.Generated.GeneratedToolFunction(json)");
        }

        using (writer.Block())
        {
            WriteParameters(writer, tool);
            writer.Line();
            writer.Line($"public override string Name => {Names.Literal(tool.Name)};");
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
                        parameter.DefaultValue is null ? null : "HasDefaultValue = true",
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
                    writer.List(initializers);
                }
            }
        }
    }

    private static void WriteInvoke(SourceWriter writer, ToolModel tool)
    {
        var kind = tool.Return;
        var isAsync = kind is ReturnKind.Awaitable or ReturnKind.AwaitableValue;
        var isExpression = kind is ReturnKind.Value or ReturnKind.AwaitableValue;
        var call = $"{tool.Target}.{tool.Method}";
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
            writer.List(arguments, end: suffix);
        }
    }

    private static string GetArgument(ParameterModel parameter) =>
        parameter.Kind switch
        {
            ParameterKind.Services => "services",
            ParameterKind.Settings => "settings",
            ParameterKind.CancellationToken => "ct",
            ParameterKind.Arguments => "arguments",
            _ when parameter.DefaultValue is { } value =>
                $"GetArgument<{parameter.Type}>(arguments, {Names.Literal(parameter.Name)}, {value})",
            _ => $"GetArgument<{parameter.Type}>(arguments, {Names.Literal(parameter.Name)})"
        };

    // D4: what agents pick in UseToolset<T>().Include(t => t.X), next to the toolset in its namespace
    private static void WriteToolList(SourceWriter writer, ToolsetModel toolset)
    {
        var list = $"global::Typeshift.ToolList<{toolset.Type.FullName}>";

        writer.Line();
        Docs.WriteSummary(
            writer,
            $"The tools of <see cref=\"{toolset.Type.FullName}\"/>, to include in or exclude from an agent.");

        using (writer.Block($"{toolset.Type.Accessibility} static class {toolset.Type.FlatName}ToolList"))
        {
            using (writer.Block($"extension({list} tools)"))
            {
                foreach (var tool in toolset.Tools)
                {
                    if (tool != toolset.Tools[0]) writer.Line();

                    var description = tool.Description is { } text ? $": {Docs.Escape(text)}" : ".";

                    Docs.WriteSummary(writer, $"The <c>{tool.Name}</c> tool{description}");
                    writer.Line(
                        $"public global::Typeshift.ToolName {Names.ToolProperty(tool.Name)} => "
                        + $"new({Names.Literal(tool.Name)});");
                }
            }
        }
    }
}
