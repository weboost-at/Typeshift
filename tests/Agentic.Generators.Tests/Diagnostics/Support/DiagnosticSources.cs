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

    public const string AgentNotPartial =
        """
        using Agentic.Agents;

        namespace Demo;

        public class EchoAgent : AgentDefinition
        {
            public EchoAgent() => WithUniqueName("echo_agent");
        }
        """;

    public const string AgentWithoutName =
        """
        using Agentic.Agents;

        namespace Demo;

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent() => WithDescription("Echoes.");
        }
        """;

    public const string AgentNotLowerSnakeCase =
        """
        using Agentic.Agents;

        namespace Demo;

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent() => WithUniqueName("Echo Agent");
        }
        """;

    public const string ForeignTool =
        """
        using Agentic.Agents;
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        public partial class ShoutToolset : Toolset
        {
            public ShoutToolset() => AddTool(Shout).WithName("shout").WithDescription("Shouts.");

            public static string Shout(string text) => text;
        }

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent()
            {
                WithUniqueName("echo_agent");
                UseToolset<EchoToolset>(ShoutToolset.Shout);
            }
        }
        """;

    public const string UngeneratedToolset =
        """
        using Agentic.Toolsets;

        namespace Tools;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string AgentOfUngeneratedToolset =
        """
        using Agentic.Agents;
        using Tools;

        namespace Demo;

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent()
            {
                WithUniqueName("echo_agent");
                UseToolset<EchoToolset>();
            }
        }
        """;

    public const string TwinAgents =
        """
        using Agentic.Agents;

        namespace Demo;

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent() => WithUniqueName("echo_agent");
        }

        public partial class OtherEchoAgent : AgentDefinition
        {
            public OtherEchoAgent() => WithUniqueName("echo_agent");
        }
        """;

    public const string UnreadToolSettings =
        """
        using Agentic.Agents;
        using Agentic.Toolsets;

        namespace Demo;

        public record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent()
            {
                WithUniqueName("echo_agent");
                UseToolset<EchoToolset>();
                WithToolSettings(new EchoSettings());
            }
        }
        """;

    public const string DescribedWithDocumentationOff =
        """
        using Agentic.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes the text.");

            /// <summary>
            /// Echoes the text.
            /// </summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string InternalSettingsOfPublicToolset =
        """
        using System;
        using Agentic.Toolsets;

        namespace Demo;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                AddTool(Echo)
                    .WithName("echo")
                    .WithDescription("Echoes the text.")
                    .WithSettings<EchoSettings>(settings => settings.Configurable(s => s.Repeat));
            }

            public static string Echo(string text, IServiceProvider services) => text;
        }
        """;
}
