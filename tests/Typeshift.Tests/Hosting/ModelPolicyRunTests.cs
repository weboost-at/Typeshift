#pragma warning disable TYPESHIFT001 // the model policy is experimental, and these tests pin how a run goes by it
using System.Net;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Hosting;

// D89: a failed model call goes to the first rule of the run's model policy that handles it and is not used up,
// below the checkpoints, so the conversation holds none of a failed attempt
public class ModelPolicyRunTests : AgentTestBase
{
    private const string OtherProvider = "other-provider";

    private const string OtherModel = "other-model";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private Action<TypeshiftBuilder> _register = _ => { };

    private WarningLog Warnings { get; } = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSingleton<ILoggerFactory>(Warnings);
        _register(services.AddTypeshift());
    }

    [Fact]
    public async Task Given_A_Failure_A_Retry_Handles_The_Run_Answers()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Retried_Failure_The_Conversation_Holds_The_Input_Once()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await Conversation().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        entries.SelectMany(entry => entry.Messages).Where(message => message.Role == ChatRole.User)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Given_A_Retried_Failure_The_Conversation_Holds_No_Error()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await Conversation().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        entries.SelectMany(entry => entry.Messages).SelectMany(message => message.Contents).OfType<ErrorContent>()
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Given_A_Failure_A_Fallback_To_A_Model_Handles_The_Call_Goes_To_That_Model()
    {
        // Arrange
        ChatClient.Reply(Rejected()).Reply(Text(Data.Tool.Answer));

        // Act
        await Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(
            new ProvidedModel(PolicyAgent.ModelProvider, PolicyAgent.Model),
            new ProvidedModel(PolicyAgent.ModelProvider, PolicyAgent.FallbackModel));
    }

    [Fact]
    public async Task Given_A_Failure_A_Fallback_To_A_Provider_Handles_The_Call_Asks_It_For_The_Same_Model()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackToProvider(OtherProvider);
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(
            new ProvidedModel(PolicyAgent.ModelProvider, PolicyAgent.Model),
            new ProvidedModel(OtherProvider, PolicyAgent.Model));
    }

    [Fact]
    public async Task Given_A_Failure_A_Fallback_To_A_Provider_And_A_Model_Handles_The_Call_Goes_To_Both()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Auth).FallbackTo(OtherProvider, OtherModel);
        ChatClient.Reply(Failed(HttpStatusCode.Unauthorized)).Reply(Text(Data.Tool.Answer));

        // Act
        await Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models[^1].Should().Be(new ProvidedModel(OtherProvider, OtherModel));
    }

    [Fact]
    public async Task Given_Two_Rules_That_Handle_A_Failure_The_First_Applies()
    {
        // Arrange
        var policy = ModelPolicy
            .Handle(ModelFailure.Unavailable).FallbackToModel(OtherModel)
            .Or(ModelFailure.Transient).Retry(1);

        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models[^1].Should().Be(new ProvidedModel(PolicyAgent.ModelProvider, OtherModel));
    }

    [Fact]
    public async Task Given_A_Retry_Used_Up_The_Next_Rule_Applies_And_The_Retry_Counts_Again_On_Its_Pair()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Unavailable()).Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        var policy = ModelPolicy
            .Handle(ModelFailure.Unavailable).Retry(1)
            .Or(ModelFailure.Unavailable | ModelFailure.Rejected).FallbackToModel(OtherModel);

        // Act
        await Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Select(model => model.Model)
            .Should()
            .Equal(PolicyAgent.Model, PolicyAgent.Model, OtherModel, OtherModel);
    }

    [Fact]
    public async Task Given_A_Failure_No_Rule_Handles_The_Run_Throws_The_Clients_Exception()
    {
        // Arrange
        var failure = Failed(HttpStatusCode.Forbidden);
        ChatClient.Reply(failure);

        // Act
        var thrown = await Record.ExceptionAsync(() => Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        thrown.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task Given_Rules_Used_Up_The_Run_Throws_The_Last_Failure()
    {
        // Arrange
        var last = Unavailable();
        ChatClient.Reply(Unavailable()).Reply(last);

        // Act
        var thrown = await Record.ExceptionAsync(() => Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        thrown.Should().BeSameAs(last);
    }

    [Fact]
    public async Task Given_A_Failure_After_The_First_Update_The_Stream_Throws_It_And_Tries_Nothing_Again()
    {
        // Arrange
        var failure = Unavailable();
        ChatClient.Reply(Text(Data.Tool.Answer), failure);

        // Act
        var thrown = await Record.ExceptionAsync(async () =>
            await Host<PolicyAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct));

        // Assert
        thrown.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task Given_A_Failure_Before_The_First_Update_A_Stream_Retries_And_Answers()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Host<PolicyAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        updates.ToChatResponse().Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_No_Update_Within_The_Timeout_The_Attempt_Fails_As_A_Timeout()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Timeout).Retry(1).Timeout(Patience);
        ChatClient.Reply(new TaskCompletionSource()).Reply(Text(Data.Tool.Answer));

        var running = Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        Time.Advance(Patience);
        var response = await running;

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_No_First_Update_Within_The_Timeout_A_Stream_Tries_The_Call_Again()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Timeout).Retry(1).Timeout(Patience);
        ChatClient.Reply(new TaskCompletionSource()).Reply(Text(Data.Tool.Answer));

        var running = Host<PolicyAgent>()
            .WithModelPolicy(policy)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        await ChatClient.Requested;

        // Act
        Time.Advance(Patience);
        var updates = await running;

        // Assert
        updates.ToChatResponse().Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Copy_With_A_Model_Policy_The_Host_It_Came_From_Keeps_The_Agents()
    {
        // Arrange
        var host = Host<PolicyAgent>();
        _ = host.WithModelPolicy(ModelPolicy.None);
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        var response = await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Timeout_No_Rule_Handles_The_Run_Throws_A_Timeout_Naming_The_Pair()
    {
        // Arrange
        var policy = ModelPolicy.None.Timeout(Patience);
        ChatClient.Reply(new TaskCompletionSource());

        var running = Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        Time.Advance(Patience);
        var run = () => running;

        // Assert
        await run.Should().ThrowAsync<TimeoutException>().WithMessage(
            "The model call of the agent 'policy_agent' to 'policy-model' of 'policy-provider' sent no answer within "
            + "00:00:30, the timeout of its model policy.");
    }

    [Fact]
    public async Task Given_A_First_Update_In_Time_The_Timeout_Leaves_A_Long_Answer_Whole()
    {
        // Arrange
        var policy = ModelPolicy.None.Timeout(Patience);
        var gate = new TaskCompletionSource();
        ChatClient.Reply(Text(Data.Tool.Answer), gate, Text(Data.Agent.FollowUp));

        await using var updates = Host<PolicyAgent>()
            .WithModelPolicy(policy)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .GetAsyncEnumerator(Ct);

        await updates.MoveNextAsync();
        Time.Advance(Patience * 2);
        gate.SetResult();

        // Act
        var rest = new List<ChatResponseUpdate>();

        while (await updates.MoveNextAsync())
        {
            rest.Add(updates.Current);
        }

        // Assert
        rest.ToChatResponse().Text.Should().Be(Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_The_Callers_Cancellation_No_Rule_Handles_It()
    {
        // Arrange
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var policy = ModelPolicy.Handle(ModelFailure.Any).Retry(3);
        ChatClient.Reply(new TaskCompletionSource());

        var running = Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: cancel.Token);
        await ChatClient.Requested;

        // Act
        await cancel.CancelAsync();
        var run = () => running;

        // Assert
        await run.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Given_A_Fallback_The_Runs_Later_Model_Calls_Stay_On_Its_Pair()
    {
        // Arrange
        ChatClient
            .Reply(Rejected())
            .Reply(Call(Data.Tool.CallId, "look", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Select(model => model.Model)
            .Should()
            .Equal(PolicyAgent.Model, PolicyAgent.FallbackModel, PolicyAgent.FallbackModel);
    }

    [Fact]
    public async Task Given_A_Fallback_Applied_Once_It_Handles_No_Later_Failure_Of_The_Run()
    {
        // Arrange
        var again = Rejected();

        ChatClient
            .Reply(Rejected())
            .Reply(Call(Data.Tool.CallId, "look", ("query", Data.Tool.Query)))
            .Reply(again);

        // Act
        var thrown = await Record.ExceptionAsync(() => Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        thrown.Should().BeSameAs(again);
    }

    [Fact]
    public async Task Given_A_Fallback_The_Entry_Of_The_Answer_Names_The_Pair_That_Answered()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackTo(OtherProvider, OtherModel);
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await Conversation().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        entries.Where(entry => entry.Model is not null).Select(entry => (entry.Provider, entry.Model))
            .Should()
            .Equal((OtherProvider, OtherModel));
    }

    [Fact]
    public async Task Given_A_Retry_With_A_Delay_The_Call_Is_Tried_Again_Once_The_Delay_Passed()
    {
        // Arrange
        var clock = new WatchedClock(Time, Patience);
        _register = typeshift => typeshift.Services.AddSingleton<TimeProvider>(clock);

        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).Retry(1, Patience);
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        var running = Host<PolicyAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);
        await clock.Started.WaitAsync(Ct);

        // Act
        Time.Advance(Patience - TimeSpan.FromTicks(1));
        var early = ChatClient.Requests.Count;
        Time.Advance(TimeSpan.FromTicks(1));
        await running;

        // Assert
        early.Should().Be(1);
    }

    [Fact]
    public async Task Given_A_Policy_At_The_Call_Site_It_Replaces_The_Agents_Own()
    {
        // Arrange
        var failure = Unavailable();
        ChatClient.Reply(failure);

        // Act
        var thrown = await Record.ExceptionAsync(() =>
            Host<PolicyAgent>().WithModelPolicy(ModelPolicy.None).RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        thrown.Should().BeSameAs(failure);
    }

    [Fact]
    public void Given_An_Agent_That_Declares_A_Policy_Its_Agent_Has_It()
    {
        // Act
        var policy = Host<PolicyAgent>().Agent.ModelPolicy;

        // Assert
        policy.Should().Be(PolicyAgent.Policy);
    }

    [Fact]
    public async Task Given_The_Apps_Classification_A_Client_Librarys_Exception_Is_Handled_By_Its_Kind()
    {
        // Arrange
        _register = typeshift => typeshift.WithOptions(options =>
            options.ClassifyModelFailure = exception => exception is VendorException ? ModelFailure.RateLimit : null);

        ChatClient.Reply(new VendorException()).Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Failure_Of_An_Unknown_Kind_A_Warning_Names_Its_Type_And_The_Classification()
    {
        // Arrange
        ChatClient.Reply(new VendorException());

        // Act
        await Record.ExceptionAsync(() => Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        Warnings.Warnings.Should().Equal(
            "A model call of agent policy_agent failed with Typeshift.Tests.Hosting.Support.VendorException, whose "
            + "kind Typeshift cannot tell, so no rule of its model policy handles it: tell the kind with "
            + "TypeshiftOptions.ClassifyModelFailure. Each type is named once.");
    }

    [Fact]
    public async Task Given_Two_Failures_Of_One_Unknown_Type_The_Warning_Names_It_Once()
    {
        // Arrange
        ChatClient.Reply(new VendorException()).Reply(new VendorException());

        // Act
        await Record.ExceptionAsync(() => Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct));
        await Record.ExceptionAsync(() => Host<PolicyAgent>().RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        Warnings.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Fallback_To_A_Provider_Nothing_Serves_The_Run_Fails_At_Its_Start()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackToProvider(Vendor.Other);

        // Act
        var run = () => Conversation().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            "No chat client serves the model policy of the agent 'policy_agent' on 'policy-model' of 'Other':*");
    }

    [Fact]
    public async Task Given_A_Fallback_To_A_Provider_Nothing_Serves_The_Conversation_Stays_Empty()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackToProvider(Vendor.Other);

        // Act
        await Record.ExceptionAsync(() => Conversation().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId)).Should().BeEmpty();
    }

    private AgentHost<PolicyAgent> Conversation() =>
        Host<PolicyAgent>().WithConversationId(Data.Agent.ConversationId);

    private HttpRequestException Unavailable() => Failed(HttpStatusCode.ServiceUnavailable);

    private HttpRequestException Rejected() => Failed(HttpStatusCode.BadRequest);

    private HttpRequestException Failed(HttpStatusCode status) => new(Data.Agent.Reason, null, status);
}
