namespace Typeshift.Generators.Tests.Registration.Support;

public static class RegistrationSources
{
    public const string EchoToolset =
        """
        using Typeshift.Toolsets;

        namespace Tools;

        /// <summary>
        /// Echoes text.
        /// </summary>
        public partial class EchoToolset : Toolset
        {
            /// <summary>
            /// Declares the tool.
            /// </summary>
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes the text.");

            /// <summary>
            /// Echoes the text.
            /// </summary>
            /// <param name="text">The text to echo.</param>
            public static string Echo(string text) => text;
        }
        """;

    public const string EchoAgent =
        """
        using Typeshift.Agents;
        using Tools;

        namespace App;

        /// <summary>
        /// Echoes what it is told.
        /// </summary>
        public partial class EchoAgent : AgentDefinition
        {
            /// <summary>
            /// Declares the agent.
            /// </summary>
            public EchoAgent()
            {
                WithName("echo_agent");
                UseToolset<EchoToolset>();
            }
        }
        """;

    public const string Host =
        """
        using Microsoft.Extensions.DependencyInjection;

        namespace App;

        /// <summary>
        /// Wires the host.
        /// </summary>
        public static class Setup
        {
            /// <summary>
            /// Registers everything this project sees.
            /// </summary>
            public static IServiceCollection Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    public const string ShoutToolset =
        """
        using Typeshift.Toolsets;

        namespace App;

        /// <summary>
        /// Shouts text.
        /// </summary>
        public partial class ShoutToolset : Toolset
        {
            /// <summary>
            /// Declares the tool, under a name another toolset uses already.
            /// </summary>
            public ShoutToolset() => AddTool(Shout).WithName("echo").WithDescription("Shouts the text.");

            /// <summary>
            /// Shouts the text.
            /// </summary>
            /// <param name="text">The text to shout.</param>
            public static string Shout(string text) => text.ToUpperInvariant();
        }
        """;

    public const string TestHost =
        """
        using Microsoft.Extensions.DependencyInjection;

        namespace Tests;

        /// <summary>
        /// Wires the tests.
        /// </summary>
        public static class TestSetup
        {
            /// <summary>
            /// Registers everything the tests see.
            /// </summary>
            public static IServiceCollection Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    public const string VisibleToTests =
        """
        [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tests")]
        """;

    public const string LookupToolset =
        """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Typeshift.Toolsets;

        namespace Tools;

        /// <summary>
        /// Looks entries up.
        /// </summary>
        public partial class LookupToolset : Toolset
        {
            /// <summary>
            /// Declares the tool.
            /// </summary>
            public LookupToolset() => AddTool(Lookup).WithName("lookup").WithDescription("Looks entries up.");

            /// <summary>
            /// Looks entries up.
            /// </summary>
            /// <param name="query">What to look up.</param>
            public static Task<IReadOnlyList<Entry>> Lookup(Query query) => Task.FromResult<IReadOnlyList<Entry>>([]);
        }

        /// <summary>
        /// What to look up.
        /// </summary>
        /// <param name="Text">The text to look up.</param>
        public sealed record Query(string Text);

        /// <summary>
        /// An entry found.
        /// </summary>
        /// <param name="Title">The entry's title.</param>
        public sealed record Entry(string Title);
        """;

    public static readonly string QueryJsonContext = JsonContext("QueryJsonContext", "Query");

    public static readonly string EntriesJsonContext =
        JsonContext("EntriesJsonContext", "System.Collections.Generic.IReadOnlyList<Entry>");

    public static readonly string UriJsonContext = JsonContext("UriJsonContext", "System.Uri");

    public static readonly string PrivateJsonContext =
        $$"""
        namespace Tools;

        internal static class Outer
        {
        {{DeclareJsonContext("private", "PrivateJsonContext", "Query")}}
        }
        """;

    private static string JsonContext(string name, string type) =>
        $"namespace Tools;\n\n{DeclareJsonContext("internal", name, type)}";

    // A context with the members the System.Text.Json generator adds, which the test compilation does not run
    private static string DeclareJsonContext(string accessibility, string name, string type) =>
        $$"""
        [System.Text.Json.Serialization.JsonSerializable(typeof({{type}}))]
        {{accessibility}} partial class {{name}}() : System.Text.Json.Serialization.JsonSerializerContext(null)
        {
            public static {{name}} Default { get; } = new();

            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) =>
                null;
        }
        """;
}
