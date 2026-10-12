namespace Typeshift.Generators.Tests.Diagnostics.Support;

// One toolset per diagnostic, each wrong in exactly the way its diagnostic names
public static class DiagnosticSources
{
    public const string NotPartial =
        """
        using Typeshift.Toolsets;

        namespace Demo;

        public class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string NotConstant =
        """
        using Typeshift.Toolsets;

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
        using Typeshift.Toolsets;

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
        using Typeshift.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool((Func<string, string>)(text => text)).WithName("echo").WithDescription("Echoes.");
        }
        """;

    public const string SettingsNotRecord =
        """
        using Typeshift.Toolsets;

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
        using Typeshift.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo");

            public static string Echo(string text) => text;
        }
        """;

    public const string UnreadSettings =
        """
        using Typeshift.Toolsets;

        namespace Demo;

        public record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithName("echo").WithDescription("Echoes.").WithSettings<EchoSettings>();

            public static string Echo(string text) => text;
        }
        """;

    public const string DocumentationOff =
        """
        using Typeshift.Toolsets;

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
        using Typeshift.Toolsets;

        namespace Demo;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("EchoText").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string AgentNotPartial =
        """
        using Typeshift.Agents;

        namespace Demo;

        public class EchoAgent : AgentDefinition
        {
            public EchoAgent() => WithName("echo_agent");
        }
        """;

    public const string ForeignTool =
        """
        using Typeshift.Agents;
        using Typeshift.Toolsets;

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
                WithName("echo_agent");
                UseToolset<EchoToolset>().Include(t => t.Shout);
            }
        }
        """;

    public const string UngeneratedToolset =
        """
        using Typeshift.Toolsets;

        namespace Tools;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string AgentOfUngeneratedToolset =
        """
        using Typeshift.Agents;
        using Tools;

        namespace Demo;

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent()
            {
                WithName("echo_agent");
                UseToolset<EchoToolset>();
            }
        }
        """;

    public const string SettingsOfAnUnofferedTool =
        """
        using Typeshift.Agents;
        using Typeshift.Toolsets;

        namespace Demo;

        public record ToneSettings
        {
            public bool Loud { get; init; }
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                AddTool(Echo).WithName("echo").WithDescription("Echoes.").WithSettings<ToneSettings>();
                AddTool(Shout).WithName("shout").WithDescription("Shouts.").WithSettings<ToneSettings>();
            }

            public static string Echo(string text, ToneSettings settings) => text;

            public static string Shout(string text, ToneSettings settings) => text;
        }

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent()
            {
                UseToolset<EchoToolset>().Include(t => t.Echo);
                ConfigureShout().WithLoud(true);
            }
        }
        """;

    public const string DescribedWithDocumentationOff =
        """
        using Typeshift.Toolsets;

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
        using Typeshift.Toolsets;

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
                    .WithSettings<EchoSettings>()
                    .Configurable(s => s.Repeat);
            }

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string UnsupportedSetting =
        """
        using System.Collections.Generic;
        using Typeshift.Toolsets;

        namespace Demo;

        public record EchoSettings
        {
            public IReadOnlyDictionary<string, int> Weights { get; init; } = new Dictionary<string, int>();
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo)
                    .WithName("echo")
                    .WithDescription("Echoes.")
                    .WithSettings<EchoSettings>()
                    .Configurable(s => s.Weights);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string UnsupportedChatOption =
        """
        using Typeshift.Agents;

        namespace Demo;

        public partial class EchoAgent : AgentDefinition
        {
            public EchoAgent() => WithChatOptions().Configurable(o => o.ResponseFormat);
        }
        """;

    public const string UnsupportedConfigurableSetting =
        """
        using System.Collections.Generic;
        using Typeshift.Toolsets;

        namespace Demo;

        public record EchoSettings
        {
            public int Repeat { get; init; } = 1;

            public IReadOnlyDictionary<string, int> Weights { get; init; } = new Dictionary<string, int>();
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo)
                    .WithName("echo")
                    .WithDescription("Echoes.")
                    .WithConfigurableSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string UnsupportedSettingNotConfigurable =
        """
        using System.Collections.Generic;
        using Typeshift.Toolsets;

        namespace Demo;

        public record EchoSettings
        {
            public int Repeat { get; init; } = 1;

            public IReadOnlyDictionary<string, int> Weights { get; init; } = new Dictionary<string, int>();
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo)
                    .WithName("echo")
                    .WithDescription("Echoes.")
                    .WithConfigurableSettings<EchoSettings>()
                    .NotConfigurable(s => s.Weights);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;
}
