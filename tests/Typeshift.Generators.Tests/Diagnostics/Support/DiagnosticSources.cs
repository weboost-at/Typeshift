namespace Typeshift.Generators.Tests.Diagnostics.Support;

// One mistake each, in an otherwise valid toolset or agent
public static class DiagnosticSources
{
    public const string NotPartial =
        """
        using Typeshift;

        internal class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string NotConstant =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            private static readonly string Prefix = "say_";

            public EchoToolset() => AddTool(Echo).WithName(Prefix + "echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string Lambda =
        """
        using System;
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool((Func<string, string>)(text => text)).WithDescription("Echoes.");
        }
        """;

    public const string RefParameter =
        """
        using Typeshift;

        internal partial class CounterToolset : Toolset
        {
            public CounterToolset() => AddTool(Bump).WithDescription("Bumps.");

            public static void Bump(ref int value) => value++;
        }
        """;

    public const string SettingsNotRecord =
        """
        using Typeshift;

        internal class EchoSettings
        {
            public int Repeat { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string SettingsLessAccessible =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; }
        }

        /// <summary>Echoes.</summary>
        public partial class EchoToolset : Toolset
        {
            /// <summary>Declares the tool.</summary>
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>();

            internal static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string DuplicateName =
        """
        using Typeshift;

        internal partial class VersionToolset : Toolset
        {
            public VersionToolset()
            {
                AddTool(Get).WithName("get_v2").WithDescription("Gets.");
                AddTool(GetOther).WithName("get_v_2").WithDescription("Gets.");
            }

            public static string Get(string key) => key;

            public static string GetOther(string key) => key;
        }
        """;

    public const string DuplicateAcrossToolsets =
        """
        using Typeshift;

        internal partial class FirstToolset : Toolset
        {
            public FirstToolset() => AddTool(Find).WithDescription("Finds.");

            public static string Find(string query) => query;
        }

        internal partial class SecondToolset : Toolset
        {
            public SecondToolset() => AddTool(Find).WithDescription("Finds.");

            public static string Find(string query) => query;
        }
        """;

    public const string NotLowerSnakeCase =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("Echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string MissingDescription =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo);

            public static string Echo(string text) => text;
        }
        """;

    public const string UnreadSettings =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>();

            public static string Echo(string text) => text;
        }
        """;

    public const string SummaryOnly =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo);

            /// <summary>
            /// Echoes the text.
            /// </summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string ReadOnlyPick =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;

            public int Twice => Repeat * 2;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                AddTool(Echo)
                    .WithDescription("Echoes.")
                    .WithSettings<EchoSettings>()
                    .Configurable(s => s.Twice);
            }

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string OutsideConstructor =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => Declare();

            public static string Echo(string text) => text;

            private void Declare() => AddTool(Echo).WithDescription("Echoes.");
        }
        """;

    public const string ConditionalTool =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset(bool loud)
            {
                if (loud) AddTool(Echo).WithDescription("Echoes.");
            }

            public static string Echo(string text) => text;
        }
        """;

    public const string GenericMethod =
        """
        using System;
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo<int>).WithDescription("Echoes.");

            public static T Echo<T>(T value) => value;
        }
        """;

    public const string ChainOnVariable =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                var tool = AddTool(Echo);
                tool.WithDescription("Echoes.");
            }

            /// <summary>Echoes.</summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string StoredChain =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                var tool = AddTool(Echo);
            }

            /// <summary>Echoes.</summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string ComputedPick =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                AddTool(Echo)
                    .WithDescription("Echoes.")
                    .WithSettings<EchoSettings>()
                    .Configurable(s => s.Repeat + 1);
            }

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string PrivateNested =
        """
        using Typeshift;

        internal partial class Outer
        {
            private partial class EchoToolset : Toolset
            {
                public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

                public static string Echo(string text) => text;
            }
        }
        """;

    public const string Derived =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class LoudToolset : EchoToolset
        {
        }
        """;

    public const string Generic =
        """
        using Typeshift;

        internal partial class EchoToolset<T> : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string ClashingMember =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;

            public static void CreateEcho()
            {
            }
        }
        """;

    public const string AgentNotPartial =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;

    public const string ComputedAgentName =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            private static readonly string Prefix = "echo";

            public EchoAgent() => WithName(Prefix + "_agent");
        }
        """;

    public const string UnstorableOption =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => WithChatOptions().Configurable(o => o.Tools);
        }
        """;

    public const string DeclarationInHelper =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => Declare();

            private void Declare() => UseToolset<EchoToolset>();
        }
        """;

    public const string ConditionalDeclaration =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent(bool echo)
            {
                if (echo) UseToolset<EchoToolset>();
            }

            public EchoAgent()
                : this(true)
            {
            }
        }
        """;

    public const string UseOnVariable =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                var use = UseToolset<EchoToolset>();
                use.Include(t => t.Echo);
            }
        }
        """;

    public const string ComputedToolPick =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>().Include(t => Pick(t));

            private static ToolName Pick(ToolList<EchoToolset> tools) => tools.Echo;
        }
        """;

    public const string AgentWithoutParameterlessConstructor =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent(string model) => WithChatOptions().WithModel(model);
        }
        """;

    public const string DerivedAgent =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal abstract class BaseAgent : Agent;

        internal partial class EchoAgent : BaseAgent
        {
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;

    public const string AgentClashingMember =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>();

            public int Settings { get; init; }
        }
        """;

    public const string ToolsetUsedTwice =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                UseToolset<EchoToolset>();

                UseToolset<EchoToolset>();
            }
        }
        """;

    public const string PublicAgentOfInternalSettings =
        """
        #pragma warning disable CS1591

        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Repeat);

            public static string Echo(string text, EchoSettings settings) => text;
        }

        public partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;
}
