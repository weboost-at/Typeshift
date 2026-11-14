using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.MultiAgent.Support;
using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Decisions.Support;

// The approval tests' host with an agent that asks the user, and a sub-agent that would, on a scripted client of
// its own
public abstract class QuestionTestBase : ApprovalTestBase
{
    protected QuestionTestBase() => HelperClient = new(Ct);

    protected ScriptedChatClient HelperClient { get; }

    // The asking agent's host for the test's conversation
    protected AgentHost<AskingAgent> Asking => Host<AskingAgent>().WithConversationId(Data.Agent.ConversationId);

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift().WithChatClientProvider<TwoClientProvider>();
        services.AddSingleton(new HelperChatClient(HelperClient));
    }

    // A call of ask_user with the test's question: one choice among its three options
    protected ChatResponseUpdate Ask(string callId) => Ask(callId, Question());

    protected ChatResponseUpdate Ask(string callId, params object[] questions) =>
        Call(callId, "ask_user", ("questions", questions));

    protected object Question(bool multiSelect = false) =>
        Question(Data.Questions.Header, Data.Questions.Text, multiSelect, [.. Data.Questions.Labels]);

    protected object SecondQuestion() =>
        Question(Data.Questions.SecondHeader, Data.Questions.SecondText, multiSelect: false, "Everything", "A part");

    protected static object Question(string header, string text, bool multiSelect, params string[] labels) =>
        new
        {
            question = text,
            header,
            options = labels.Select(label => new { label, description = $"Go with {label}." }),
            multiSelect
        };

    // The run that asks the test's question, and the decision it ends with
    protected async Task<QuestionDecision> AskAsync(params object[] questions)
    {
        ChatClient.Reply(Ask(Data.Tool.CallId, questions is [] ? [Question()] : questions));

        var response = await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        return response.Decisions.OfType<QuestionDecision>().Single();
    }

    protected Task<ChatResponse> AnswerQuestionsAsync(params DecisionAnswer[] answers) =>
        Asking.AnswerAsync(answers, ct: Ct);
}

// Asks the user, beside a tool behind approval, one that reads and one that does neither. The tests name the mode
// of a run in its settings, to see that it asks in plan mode and in auto mode too
#pragma warning disable TSH037
public partial class AskingAgent : Agent
{
    public AskingAgent()
    {
        WithDescription("Settles what the customer wants.");

        WithChatOptions().WithModel("scripted", "asking-model");

        WithRunOptions().Configurable(r => r.Mode);

        UseToolset<GuardedToolset>();

        UseQuestions();
    }
}
#pragma warning restore TSH037

// Would ask the user, and runs as a sub-agent, on the helper's model
public partial class AskingHelperAgent : Agent
{
    public AskingHelperAgent()
    {
        WithDescription("Answers one question about a ticket.");

        WithChatOptions().WithModel("scripted", HelperAgent.Model);

        UseQuestions();
    }
}

// A sub-agent that declares questions is the point: the tests see that it is not offered them
#pragma warning disable TSH034
public partial class AskingLeadAgent : Agent
{
    public AskingLeadAgent()
    {
        WithChatOptions().WithModel("scripted", "lead-model");

        UseAgent<AskingHelperAgent>();
    }
}

public partial class BackgroundAskingLeadAgent : Agent
{
    public BackgroundAskingLeadAgent()
    {
        WithChatOptions().WithModel("scripted", "lead-model");

        UseAgent<AskingHelperAgent>().InBackground();
    }
}
#pragma warning restore TSH034

// Hands the conversation to the agent that asks
public partial class AskingFrontAgent : Agent
{
    public AskingFrontAgent()
    {
        WithChatOptions().WithModel("scripted", "front-model");

        UseHandoff<AskingAgent>();
    }
}
