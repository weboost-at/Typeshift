using System.Collections.Generic;
using System.Linq;
using Agentic.Generators.Models;

namespace Agentic.Generators.Emitting;

internal static class ToolsetEmitter
{
    private const string ToolParameterList =
        "global::System.Collections.Generic.IReadOnlyList<global::Agentic.Tools.ToolParameter>";

    private const string ToolBinding = "global::Agentic.Tools.ToolBinding";

    private const string ToolDefinition = "global::Agentic.Toolsets.ToolDefinition";

    public static string Emit(ToolsetModel toolset) =>
        TypeEmitter.EmitPartial(toolset.Type, writer =>
        {
            WriteToolReferences(writer, toolset);
            writer.Line();
            WriteGetBinding(writer, toolset);

            foreach (var tool in toolset.Tools)
            {
                writer.Line();
                WriteBinding(writer, tool);
                writer.Line();
                WriteFunction(writer, toolset, tool);
            }
        });

    private static void WriteToolReferences(SourceWriter writer, ToolsetModel toolset)
    {
        WriteSummary(writer, "Typed references to the tools of this toolset.");

        using (writer.Block("public static class Tools"))
        {
            foreach (var tool in toolset.Tools)
            {
                var type = tool.Settings is { } settings
                    ? $"global::Agentic.Tools.ToolRef<{settings.Type}>"
                    : "global::Agentic.Tools.ToolRef";

                if (tool != toolset.Tools[0]) writer.Line();

                var property = Names.ToPascalCase(tool.Name);

                WriteSummary(writer, $"The <c>{tool.Name}</c> tool.");
                writer.Line($"public static {type} {property} {{ get; }} = new({Names.Literal(tool.Name)});");
            }
        }
    }

    private static void WriteGetBinding(SourceWriter writer, ToolsetModel toolset)
    {
        writer.Line("/// <inheritdoc />");
        writer.Line($"protected override {ToolBinding} GetBinding({ToolDefinition} tool) =>");

        using (writer.Indent())
        {
            writer.Line("tool.Name.Value switch");

            using (writer.Block(close: "};"))
            {
                foreach (var tool in toolset.Tools)
                {
                    writer.Line($"{Names.Literal(tool.Name)} => {Names.ToPascalCase(tool.Name)}Binding,");
                }

                writer.Line("_ => base.GetBinding(tool)");
            }
        }
    }

    private static void WriteBinding(SourceWriter writer, ToolModel tool)
    {
        var name = Names.ToPascalCase(tool.Name);

        writer.Line($"private static readonly {ToolBinding} {name}Binding =");

        using (writer.Indent())
        {
            if (tool.Settings is not { } settings)
            {
                writer.Line($"{ToolBinding}.Create(static (tool, json) => new {name}Function(tool, json));");
                return;
            }

            writer.Line($"{ToolBinding}.Create(");

            using (writer.Indent())
            {
                writer.Line($"new global::Agentic.Settings.ToolSettingsBinder<{settings.Type}>(");
                WriteSettingsProperties(writer, settings);
                writer.Line($"static (tool, settings, json) => new {name}Function(tool, settings, json));");
            }
        }
    }

    private static void WriteSettingsProperties(SourceWriter writer, SettingsModel settings)
    {
        using (writer.Block(open: "[", close: "]),"))
        {
            foreach (var property in settings.Properties)
            {
                var member = Names.Identifier(property.Name);
                var separator = property == settings.Properties[settings.Properties.Count - 1] ? "" : ",";

                writer.Line($"global::Agentic.Settings.SettingsProperty.Create<{settings.Type}, {property.Type}>(");

                using (writer.Indent())
                {
                    writer.Line($"{Names.Literal(property.Name)},");
                    writer.Line($"isConfigurable: {(property.IsConfigurable ? "true" : "false")},");
                    writer.Line($"static settings => settings.{member},");
                    writer.Line($"static (settings, value) => settings with {{ {member} = value }}){separator}");
                }
            }
        }
    }

    private static void WriteFunction(SourceWriter writer, ToolsetModel toolset, ToolModel tool)
    {
        var name = Names.ToPascalCase(tool.Name);
        var settings = tool.Settings is { } model ? $"{model.Type} settings" : null;
        var passedSettings = settings is null ? "null" : "settings";

        writer.Line($"private sealed class {name}Function(");

        using (writer.Indent())
        {
            writer.Line($"{ToolDefinition} tool,");
            if (settings is not null) writer.Line($"{settings},");
            writer.Line("global::Agentic.Json.AgentJson json)");
            writer.Line($": global::Agentic.Tools.GeneratedToolFunction(tool, {passedSettings}, json)");
        }

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
        var kind = tool.Return.Kind;
        var isAsync = kind is ReturnKind.Awaitable or ReturnKind.AwaitableValue;
        var isExpression = kind is ReturnKind.Value or ReturnKind.AwaitableValue;
        var call = $"{tool.Target}.{Names.Identifier(tool.Method)}";
        var arguments = tool.Parameters.Select(GetArgument).ToList();

        var modifiers = isAsync ? "override async" : "override";

        writer.Line($"protected {modifiers} global::System.Threading.Tasks.ValueTask<object?> InvokeToolAsync(");

        using (writer.Indent())
        {
            writer.Line("global::Microsoft.Extensions.AI.AIFunctionArguments arguments,");
            writer.Line("global::System.IServiceProvider services,");
            writer.Line($"global::System.Threading.CancellationToken ct){(isExpression ? " =>" : "")}");

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

    // One argument per line once there are several, closing on the last one
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
            ParameterKind.CancellationToken => "ct",
            ParameterKind.Arguments => "arguments",
            _ when parameter.HasDefaultValue =>
                $"GetArgument<{parameter.Type}>(arguments, {Names.Literal(parameter.Name)}, {parameter.DefaultValue})",
            _ => $"GetArgument<{parameter.Type}>(arguments, {Names.Literal(parameter.Name)})"
        };

    private static void WriteSummary(SourceWriter writer, string text)
    {
        writer.Line("/// <summary>");
        writer.Line($"/// {text}");
        writer.Line("/// </summary>");
    }
}
