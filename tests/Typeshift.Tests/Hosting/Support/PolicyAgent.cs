#pragma warning disable TYPESHIFT001 // the agent declares a model policy, which is experimental
namespace Typeshift.Tests.Hosting.Support;

// D89: declares its model policy in code: a transient failure is tried once more, a rejected call moves to another
// model. It offers a tool of its own, so a run makes more than one model call
/// <summary>
/// Looks things up, and recovers from a failed model call as its policy says.
/// </summary>
public partial class PolicyAgent : Agent
{
    public const string ModelProvider = "policy-provider";

    public const string Model = "policy-model";

    public const string FallbackModel = "fallback-model";

    public PolicyAgent()
    {
        WithChatOptions()
            .WithProvider(ModelProvider)
            .WithModel(Model);

        WithRunOptions()
            .WithModelPolicy(Policy);

        AddTool(Look);
    }

    public static ModelPolicy Policy { get; } =
        ModelPolicy
            .Handle(ModelFailure.Transient).Retry(1)
            .Or(ModelFailure.Rejected).FallbackToModel(FallbackModel);

    /// <summary>
    /// Looks something up.
    /// </summary>
    /// <param name="query">What to look up.</param>
    private static string Look(string query) => query;
}

#pragma warning restore TYPESHIFT001
