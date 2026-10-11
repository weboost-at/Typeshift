namespace Agentic.Generators.Tests.Diagnostics.Support;

// One toolset per diagnostic, each wrong in exactly the way its diagnostic names
public static class DiagnosticSources
{
    public const string NotPartial =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string NotConstant =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            private static readonly string Name = "echo";

            public EchoToolset() => AddTool(Echo).WithName(Name).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string MissingToolName =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string NotStaticMethodGroup =
        """
        using System;
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool((Func<string, string>)(text => text)).WithName("echo").WithDescription("Echoes.");
        }
        """;

    public const string SettingsNotRecord =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public class EchoSettings
        {
            public int Repeat { get; set; } = 1;
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithName("echo").WithDescription("Echoes.").WithSettings<EchoSettings>();

            public static string Echo(string text) => text;
        }
        """;

    public const string MissingDescription =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo");

            public static string Echo(string text) => text;
        }
        """;

    public const string SettingsAsParameter =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithName("echo").WithDescription("Echoes.").WithSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string DocumentationOff =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo");

            /// <summary>
            /// Echoes the text.
            /// </summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string NotLowerSnakeCase =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("EchoText").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;
}
