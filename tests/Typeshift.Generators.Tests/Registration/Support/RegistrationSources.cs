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
}
