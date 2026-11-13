namespace Typeshift.Generators.Tests.Diagnostics.Support;

// A constructor body as it stands while a declaration is typed, replaced or misspelled, one per entry. Whatever the
// compiler says about it, the generator and the analyzer run to the end. A statement that crashed one of them gets
// an entry here
public static class HalfTypedSources
{
    private const string Agent =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;
        using Alias = Demo.Tools;

        namespace Demo.Tools
        {
            internal record S
            {
                public int Limit { get; init; }
            }

            internal partial class EchoToolset : Toolset
            {
                public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

                public static string Echo(string text) => text;

                public static string Echo2(string text, S s) => text;

                internal class Nested;
            }
        }

        namespace Demo
        {
            using Demo.Tools;

            internal partial class OtherAgent : Agent
            {
                public OtherAgent() => WithDescription("Another agent.");
            }

            internal partial class EchoAgent : Agent
            {
                public EchoAgent()
                {
                    BODY
                }
            }
        }
        """;

    private const string Toolset =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        namespace Demo;

        internal record S
        {
            public int Limit { get; init; }
        }

        internal partial class GoodToolset : Toolset
        {
            public GoodToolset() => AddTool(Good).WithDescription("Works.");

            public static string Good(string text) => text;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                BODY
            }

            public static string Echo(string text) => text;

            public static string Echo2(string text, S s) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                WithDescription("Uses the toolset that works.");
                UseToolset<GoodToolset>();
            }
        }
        """;

    private const string Registration =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        namespace Demo;

        internal static class Startup
        {
            public static void Register(IServiceCollection services)
            {
                BODY
            }
        }
        """;

    private static readonly string[] AgentBodies =
    [
        "UseToolset<>();",
        "UseToolset<Missing>();",
        "UseToolset<>().Include(t => t.Echo);",
        "UseToolset<EchoToolset>().Include();",
        "UseToolset<EchoToolset>().Include(t => t.);",
        "UseToolset<EchoToolset>().Include(t => );",
        "UseToolset<EchoToolset>().",
        "UseToolset<EchoToolset>()",
        "UseToolset<",
        "UseToolset<EchoToolset",
        "UseToolset();",
        "UseToolset<EchoToolset[]>();",
        "UseToolset<dynamic>();",
        "UseToolset<Toolset>();",
        "UseToolset<Missing.Inner>();",
        "UseToolset<global::Missing>();",
        "UseToolset<System.Collections.Generic.List<>>();",
        "UseToolset<EchoToolset, EchoToolset>();",
        "UseToolset<int>();",
        "UseToolset<(EchoToolset, int)>();",
        "UseAgent<>();",
        "UseAgent<Missing>();",
        "UseAgent();",
        "UseAgent<OtherAgent[]>();",
        "UseAgent<dynamic>();",
        "UseAgent<OtherAgent>().",
        "UseHandoff<>();",
        "UseHandoff<Missing>();",
        "UseHandoff();",
        "WithName();",
        "WithDescription();",
        "WithStorageKind();",
        "WithSchemaVersion();",
        "WithSchemaVersion(\"x\");",
        "WithChatOptions().WithModel();",
        "WithChatOptions().WithModel(\"p\");",
        "WithChatOptions().WithModel(, \"m\");",
        "WithChatOptions().WithModel<>(\"p\", \"m\");",
        "WithChatOptions().WithModel(Missing.P, \"m\");",
        "WithChatOptions().Configurable();",
        "WithChatOptions().Configurable(o => o.);",
        "WithChatOptions().Configurable(o => );",
        "WithChatOptions().WithTemperature().Configurable(o => o.Temperature);",
        "WithRunOptions().Configurable();",
        "WithChatOptions().WithModel(\"p\", \"m\").Configurable(o => o.ModelId).Configurable();",
        "AddTool();",
        "AddTool(",
        "AddTool(Missing);",
        "AddTool(EchoToolset.Echo).WithName();",
        "AddTool(EchoToolset.Echo).WithSettings<>();",
        "AddTool(EchoToolset.Echo2).WithSettings<S>().Configurable();",
        "AddTool(EchoToolset.Echo2).WithSettings<Missing>().Configurable(s => s.Limit);",
        "UseMcpServer();",
        "UseContext<>();",
        "UseToolset<EchoToolset>().Exclude(t => t.Echo).Include();",
        "this.UseToolset<>();",
        "base.UseToolset<>();",
        "UseToolset<EchoAgent>();",
        "UseAgent<EchoToolset>();",
        "UseAgent<EchoAgent>();",
        "UseToolset<Demo.>();",
        "UseToolset<EchoToolset>().Include(t => t.Echo",
        "var x = UseToolset<>();",
        "UseToolset<>()?.Include(t => t.Echo);",
        "WithChatOptions().WithModel(default, \"m\");",
        "WithChatOptions().WithModel(null, \"m\");",
        "WithChatOptions().WithModel(null!, null!).Configurable(o => o.ModelId);",
        "WithChatOptions().WithModel<dynamic>(\"p\", \"m\").Configurable(o => o.ModelId);",
        "WithChatOptions().WithModel(new object(), \"m\").Configurable(o => o.ModelId);",
        "WithChatOptions().WithModel((1, 2), \"m\").Configurable(o => o.ModelId);",
        "WithChatOptions().WithModel(model: \"m\", provider: \"p\").Configurable(o => o.ModelId);",
        "WithChatOptions().WithModel<Missing>(\"p\", \"m\").Configurable(o => o.ModelId);",
        "WithChatOptions().WithModel<>(, ).Configurable(o => o.ModelId);",
        "UseToolset<>(); UseToolset<>();",
        "UseToolset<>().Exclude();",
        "UseAgent<>(); UseAgent<>();",
        "UseAgent<>().WithResult<>();",
        "UseAgent<OtherAgent>().WithResult<>();",
        "UseAgent<OtherAgent>().WithResult();",
        "UseHandoff<OtherAgent[]>();",
        "UseToolset<EchoToolset?>();",
        "UseToolset<@>();",
        "UseToolset<,>();",
        "UseAgent<,>();",
        "UseToolset<Demo.EchoToolset.>();",
        "UseToolset<System.>();",
        "UseToolset<global::>();",
        "UseToolset<EchoToolset>().Include(t => t.Echo).",
        "UseToolset<EchoToolset>().Include(t => t.Echo).Include(",
        "UseToolset<EchoToolset>().Include(t => t.Echo)(t => t.Echo);",
        "UseToolset<EchoToolset>()();",
        "UseToolset<EchoToolset>()[0];",
        "UseToolset<EchoToolset>().Include(t => t.Echo)[0].Include(t => t.Echo);",
        "WithSchemaVersion(,);",
        "WithSchemaVersion(version: );",
        "WithName(name: \"a\", );",
        "WithName(\"a\", \"b\");",
        "WithDescription(null);",
        "WithName(null!);",
        "UseToolset<Tools.EchoToolset>();",
        "UseToolset<Tools.EchoToolse>();",
        "UseToolset<Tools.E>();",
        "UseToolset<Tools.>();",
        "UseToolset<Demo.Tools.EchoToolse>();",
        "UseToolset<global::Demo.Tools.EchoToolse>();",
        "UseToolset<Demo.Missing>();",
        "UseToolset<Typeshift.Missing>();",
        "UseToolset<System.Missing>();",
        "UseToolset<Alias.EchoToolse>();",
        "UseToolset<Alias::EchoToolse>();",
        "UseToolset<Tools.EchoToolset.Missing>();",
        "UseToolset<Tools.EchoToolset.Nested>();",
        "UseToolset<EchoToolse>();",
        "UseToolset<Tools.EchoToolse>().Include(t => t.Echo);",
        "UseAgent<Demo.Missing>();",
        "UseAgent<global::Missing>();",
        "UseHandoff<Demo.Missing>();",
        "UseHandoff<global::Missing>();",
        "AddTool(Tools.EchoToolset.Echo2).WithSettings<Tools.Missing>();",
        "AddTool(Tools.EchoToolset.Echo2).WithSettings<global::Missing>().Configurable(s => s.Limit);",
        "AddTool(Tools.EchoToolset.Echo2).WithConfigurableSettings<Tools.Missing>();",
        "AddTool(Tools.EchoToolset.Echo2).WithConfigurableSettings<>();",
        "WithChatOptions().WithModel<Tools.Missing>(default, \"m\").Configurable(o => o.ModelId);",
        "WithChatOptions().WithModel<global::Missing>(default, \"m\").Configurable(o => o.ModelId);",
        "UseContext<Tools.Missing>();",
        "UseToolset<Tools.EchoToolset<int>>();",
        "UseToolset<Missing<int>>();",
        "UseToolset<Tools.Missing<int>>();",
        "UseToolset<System.Collections.Generic.Missing<int>>();",
        "UseToolset<Demo.Tools.EchoToolset>(); UseToolset<Tools.Missing>();"
    ];

    private static readonly string[] ToolsetBodies =
    [
        "AddTool();",
        "AddTool(",
        "AddTool(Echo).",
        "AddTool(Echo).WithName();",
        "AddTool(Echo).WithName(",
        "AddTool(Echo).WithDescription();",
        "AddTool(Echo).WithSettings<>();",
        "AddTool(Echo2).WithSettings<>().Configurable(s => s.Limit);",
        "AddTool(Echo2).WithSettings<S>().Configurable();",
        "AddTool(Echo2).WithSettings<S>().Configurable(s => s.);",
        "AddTool(Echo2).WithSettings<S>().Configurable(s => );",
        "AddTool(Echo2).WithSettings<Missing>();",
        "AddTool(Echo2).WithConfigurableSettings<>();",
        "AddTool(Echo2).WithConfigurableSettings<S>().NotConfigurable();",
        "AddTool(Echo2).WithSettings<S[]>();",
        "AddTool(Echo2).WithSettings<dynamic>();",
        "AddTool(Echo2).WithSettings<object>();",
        "AddTool(Echo2).WithSettings<string>();",
        "AddTool(Missing);",
        "AddTool(Echo, Echo);",
        "AddTool(() => \"x\");",
        "AddTool(null);",
        "AddTool(default);",
        "AddTool(Echo).WithName(\"\").WithDescription(\"d\");",
        "AddTool(Echo).WithName(\"a__b\").WithDescription(\"d\");",
        "AddTool(Echo).WithName(\"_a\").WithDescription(\"d\");",
        "AddTool(Echo).WithName(null).WithDescription(\"d\");",
        "AddTool(Echo).WithName(null!).WithDescription(\"d\");",
        "AddTool(Echo).WithDescription(null!);",
        "AddTool(Echo2).WithSettings<S>().Configurable<>(s => s.Limit);",
        "AddTool(Echo2).WithSettings<S>().Configurable(s => s.Limit, );",
        "AddTool(Echo).WithName(\"x\") AddTool(Echo2);",
        "AddTool(Echo<>);",
        "AddTool(AddTool);",
        "AddTool(new System.Func<string, string>(Echo));",
        "AddTool((System.Func<string,string>)Echo);",
        "AddTool(Echo).WithName(name: );",
        "Typeshift.ToolBuilder.WithName(\"x\");",
        "AddTool(Echo)?.WithDescription(\"d\");",
        "AddTool(Echo)!.WithDescription(\"d\");",
        "(AddTool(Echo)).WithDescription(\"d\");",
        "AddTool(Echo).WithDescription(\"d\").ToString();",
        "_ = AddTool(Echo).WithDescription(\"d\");",
        "AddTool(Echo2).WithSettings<S>().Configurable(s => s.Limit).Configurable(",
        "AddTool(Echo2).WithSettings<S>(",
        "AddTool(Echo2).WithSettings<S",
        "AddTool(Echo2).WithSettings<",
        "AddTool(Echo2).WithSettings<S>().Configurable(null);",
        "AddTool(Echo2).WithSettings<S>().Configurable(default);",
        "AddTool(Echo2).WithSettings<S>().Configurable<int>(null!);",
        "AddTool(Echo2).WithSettings<S>().Configurable(Pick);",
        "AddTool(Echo2).WithSettings<S>().Configurable(delegate(S s) { return s.Limit; });",
        "AddTool(Echo2).WithSettings<S>().Configurable(s => { });",
        "AddTool(Echo2).WithSettings<S>().Configurable(s => { return; });",
        "AddTool(Echo2).WithSettings<S>().Configurable((s) => s.Limit, );",
        "AddTool(Echo2).WithSettings<S>().Configurable(static s => s.Missing);",
        "AddTool(Echo).WithName(\"x\" + );",
        "AddTool(Echo).WithName($\"x{}\");",
        "AddTool(Echo).WithName(nameof());",
        "AddTool(Echo).WithName(nameof(Echo));",
        "AddTool(Echo).RequiresApproval(",
        "AddTool(Echo).ReadOnly().",
        "AddTool(EchoToolset.);",
        "AddTool(Demo.EchoToolset.Echo).WithDescription();",
        "AddTool(this.Echo);",
        "AddTool(Echo).WithDescription(\"d\"); AddTool(Echo).WithDescription();",
        "WithDisplayName();",
        "WithDisplayName("
    ];

    private static readonly string[] RegistrationBodies =
    [
        "services.AddTypeshift().WithStore<>();",
        "services.AddTypeshift().WithStore<Demo.Missing>();",
        "services.AddTypeshift().WithStore<global::Missing>();",
        "services.AddTypeshift().WithChatClientProvider<>();",
        "services.AddTypeshift().WithChatClientProvider<global::Missing>();",
        "services.AddTypeshift().",
        "services.AddTypeshift(",
        "services.AddTypeshift().WithOptions(",
        "services.AddTypeshift().WithOptions(options => options.);"
    ];

    public static IEnumerable<(string Into, string Body)> Entries =>
    [
        .. AgentBodies.Select(body => (nameof(Agent), body)),
        .. ToolsetBodies.Select(body => (nameof(Toolset), body)),
        .. RegistrationBodies.Select(body => (nameof(Registration), body))
    ];

    // The entry's source: the body in the constructor of the agent or the toolset, or in the method that registers
    public static string Typed(string into, string body)
    {
        var template = into switch
        {
            nameof(Agent) => Agent,
            nameof(Toolset) => Toolset,
            _ => Registration
        };

        return template.Replace("BODY", body);
    }
}
