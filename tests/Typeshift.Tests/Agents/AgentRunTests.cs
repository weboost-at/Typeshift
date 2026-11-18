using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents;

public class AgentRunTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Run_The_Model_Receives_The_Agents_Code_Options()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        var options = ChatClient.Options[0]!;

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model));
        options.Temperature.Should().Be(ProbeAgent.Temperature);
        options.Instructions.Should().Be(ProbeAgent.Prompt);
        options.StopSequences.Should().Equal(ProbeAgent.Stop);
    }

    [Fact]
    public async Task Given_Run_Settings_They_Override_The_Configurable_Options()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var settings = new ProbeAgent.Settings
        {
            Provider = Data.Agent.Provider,
            Model = Data.Agent.Model,
            Temperature = Data.Agent.Temperature,
            Instructions = Data.Agent.Instructions
        };

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings: settings, ct: Ct);
        var options = ChatClient.Options[0]!;

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));
        options.Temperature.Should().Be(Data.Agent.Temperature);
        options.Instructions.Should().Be(Data.Agent.Instructions);
    }

    [Fact]
    public async Task Given_Run_Settings_That_Leave_An_Option_Null_The_Agents_Code_Holds()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var settings = new ProbeAgent.Settings { Temperature = Data.Agent.Temperature };

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings: settings, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model));
        ChatClient.Options[0]!.StopSequences.Should().Equal(ProbeAgent.Stop);
    }

    [Fact]
    public async Task Given_A_Provider_And_A_Model_The_Request_Names_Only_The_Model()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var settings = new ProbeAgent.Settings { Provider = Data.Agent.Provider, Model = Data.Agent.Model };

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings: settings, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ModelId.Should().Be(Data.Agent.Model);
    }

    [Fact]
    public async Task Given_The_Agents_Toolsets_The_Model_Is_Offered_Their_Tools_Then_Its_Own()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name)
            .Should()
            .Equal("probe", "count_companies", "find_companies", new HostedWebSearchTool().Name);
    }

    [Fact]
    public async Task Given_A_Run_The_Providers_Client_Outlives_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_Exclude_Every_Other_Tool_Is_Offered_In_Declaration_Order()
    {
        // Act
        var options = await Host<SchemaAgent>().ResolveOptionsAsync(ct: Ct);

        // Assert
        options.Tools!.Select(tool => tool.Name).Should().Equal("search_companies", "locate", "count_words");
    }

    [Fact]
    public void Given_WithName_The_Agent_Goes_By_It()
    {
        // Act
        var name = Host<SchemaAgent>().Agent.Name;

        // Assert
        name.Should().Be(SchemaAgent.AgentName);
    }

    [Fact]
    public async Task Given_Configure_The_Tool_Runs_With_The_Agents_Settings()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        var result = response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json);

        // Assert
        result.Settings.Should().Be(new ProbeSettings { Limit = ProbeAgent.ConfiguredLimit, Verbose = true });
    }

    [Fact]
    public async Task Given_Run_Overrides_Of_A_Tool_They_Apply_Over_The_Agents_Settings()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var settings = new ProbeAgent.Settings { Probe = new() { Limit = Data.Tool.Limit } };

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings: settings, ct: Ct);
        var result = response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json);

        // Assert
        result.Settings.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit, Verbose = true });
    }

    [Fact]
    public async Task Given_A_Toolset_Of_A_Referenced_Assembly_Its_Tool_Runs_With_Its_Settings()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "find_companies", ("name", Data.Agent.CompanyName)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        var result = (JsonElement)response.Assert.ToolResult(Data.Tool.CallId).Result!;

        // Assert
        result.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void Given_No_WithDescription_The_Summary_Of_The_Agents_Docs_Describes_It()
    {
        // Act
        var description = Host<SchemaAgent>().Agent.Description;

        // Assert
        description.Should().Be(SchemaAgent.Summary);
    }

    [Fact]
    public void Given_WithDescription_And_A_Summary_WithDescription_Describes_The_Agent()
    {
        // Act
        var description = Host<ProbeAgent>().Agent.Description;

        // Assert
        description.Should().Be("Probes what a run resolves.");
    }

    [Fact]
    public async Task Given_Two_Tool_Calls_Of_A_Run_Both_Resolve_From_The_Hosts_Scope()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)),
                Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query))))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        var first = response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json);
        var second = response.Assert.ToolResult<ProbeResult>(Data.Tool.SecondCallId, Json);

        // Assert
        first.Scope.Should().Be(Scope.GetRequiredService<CallScope>().Id);
        second.Scope.Should().Be(first.Scope);
    }

    [Fact]
    public async Task Given_A_Run_Its_Messages_Carry_The_Agents_Name_As_Their_Author()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Messages.Should().OnlyContain(message => message.AuthorName == "probe_agent");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Its_Updates_Carry_The_Text_And_The_Agents_Name()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Host<ProbeAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        updates.ToChatResponse().Text.Should().Be(Data.Tool.Answer);
        updates.Should().OnlyContain(update => update.AuthorName == "probe_agent");
    }

    [Fact]
    public async Task Given_A_Result_Type_The_Run_Returns_The_Models_Json_Typed()
    {
        // Arrange
        var expected = new ProbeSettings { Limit = Data.Tool.Limit, Verbose = true };
        ChatClient.Reply(Text(JsonSerializer.Serialize(expected, Json.GetTypeInfo<ProbeSettings>())));

        // Act
        var response = await Host<ProbeAgent>().RunAsync<ProbeSettings>(Data.Tool.Question, ct: Ct);

        // Assert
        response.Result.Should().Be(expected);
    }

    [Fact]
    public async Task Given_A_Number_Result_Type_The_Run_Reads_It_Under_Data()
    {
        // Arrange
        var answer = new { data = Data.Tool.Limit.Default };
        ChatClient.Reply(Text(JsonSerializer.Serialize(answer, Json.SerializerOptions)));

        // Act
        var response = await Host<ProbeAgent>().RunAsync<int>(Data.Tool.Question, ct: Ct);

        // Assert
        response.Result.Should().Be(Data.Tool.Limit.Default);
    }

    [Fact]
    public async Task Given_An_Enum_Result_Type_The_Run_Reads_It_Under_Data()
    {
        // Arrange
        var answer = new { data = SearchMode.Thorough };
        ChatClient.Reply(Text(JsonSerializer.Serialize(answer, Json.SerializerOptions)));

        // Act
        var response = await Host<ProbeAgent>().RunAsync<SearchMode>(Data.Tool.Question, ct: Ct);

        // Assert
        response.Result.Should().Be(SearchMode.Thorough);
    }

    [Fact]
    public async Task Given_A_Polymorphic_Result_Type_The_Run_Reads_It_Under_Data()
    {
        // Arrange
        var circle = new Circle(Data.Tool.Lives);
        var answer = new { data = (Shape)circle };
        ChatClient.Reply(Text(JsonSerializer.Serialize(answer, Json.SerializerOptions)));

        // Act
        var response = await Host<ProbeAgent>().RunAsync<Shape>(Data.Tool.Question, ct: Ct);

        // Assert
        response.Result.Should().Be(circle);
    }

    [Fact]
    public async Task Given_A_Dictionary_Result_Type_The_Response_Schema_Holds_It_Under_Data()
    {
        // Arrange
        var answer = new { data = new Dictionary<string, int> { [Data.Tool.Query] = Data.Tool.Lives } };
        ChatClient.Reply(Text(JsonSerializer.Serialize(answer, Json.SerializerOptions)));

        // Act
        await Host<ProbeAgent>().RunAsync<Dictionary<string, int>>(Data.Tool.Question, ct: Ct);

        // Assert
        ResponseSchema().Assert.Properties("data");
    }

    [Fact]
    public async Task Given_A_Result_Type_With_An_Exception_The_Run_Fails_Naming_The_Member()
    {
        // Act
        var run = () => Host<ProbeAgent>().RunAsync<FailedLookup>(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<NotSupportedException>().WithMessage("*'exception.targetSite'*[JsonIgnore]*");
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Answer_Without_Data_Its_Result_Cannot_Be_Read()
    {
        // Arrange
        var answer = new { value = Data.Tool.Lives };
        ChatClient.Reply(Text(JsonSerializer.Serialize(answer, Json.SerializerOptions)));

        // Act
        var response = await Host<ProbeAgent>().RunAsync<int>(Data.Tool.Question, ct: Ct);

        // Assert
        response.TryGetResult(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Result_Type_With_Xml_Docs_The_Response_Schema_Describes_Its_Properties()
    {
        // Arrange
        var company = new Company(Data.Agent.CompanyName, null);
        ChatClient.Reply(Text(JsonSerializer.Serialize(company, Json.GetTypeInfo<Company>())));

        // Act
        await Host<ProbeAgent>().RunAsync<Company>(Data.Tool.Question, ct: Ct);

        // Assert
        ResponseSchema().Assert.ParameterDescription("name", "The company's registered name.");
    }

    [Fact]
    public async Task Given_Run_Settings_Of_A_Run_Option_The_Tool_Loop_Stops_After_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings: new() { MaxToolIterations = 1 }, ct: Ct);

        // Assert
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(nameof(RunOptions.MaxToolIterations), 0)]
    [InlineData(nameof(RunOptions.MaxConsecutiveToolErrors), -1)]
    [InlineData(nameof(RunOptions.MaxInputTokens), 0)]
    [InlineData(nameof(RunOptions.TargetInputTokens), 0)]
    public async Task Given_A_Run_Setting_Out_Of_Range_The_Run_Throws_Naming_The_Option(string option, int value)
    {
        // Arrange
        var settings = LimitsAgent.Limit(option, value);

        // Act
        var run = () => Host<LimitsAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{LimitsAgent.AgentName}'*{option} {value}*");
    }

    [Fact]
    public async Task Given_A_Target_Of_One_Layer_Not_Below_The_Maximum_Of_Another_The_Run_Throws_Naming_Both()
    {
        // Arrange
        var host = Host<LimitsAgent>();
        await host.SaveSettingsAsync(new() { MaxInputTokens = 1_000 }, 0, Ct);

        // Act
        var run = () => host.RunAsync(Data.Tool.Question, new() { TargetInputTokens = 1_000 }, Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"The agent '{LimitsAgent.AgentName}' resolved TargetInputTokens 1000 from the settings of its code, its "
            + "stored settings, the run: it must be below MaxInputTokens, 1000.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Given_Run_Settings_With_A_Blank_Model_The_Run_Throws_Naming_It(string model)
    {
        // Act
        var run = () => Host<LimitsAgent>().RunAsync(Data.Tool.Question, new() { Model = model }, Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*Model '{model}'*name a model*");
    }

    [Fact]
    public async Task Given_Run_Settings_With_A_Blank_Summary_Model_The_Run_Throws_Naming_It()
    {
        // Act
        var run = () => Host<LimitsAgent>().RunAsync(Data.Tool.Question, new() { SummaryModel = " " }, Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SummaryModel ' '*");
    }

    [Theory]
    [InlineData(0, 3, 1_000, 500, "MaxToolIterations")]
    [InlineData(40, -1, 1_000, 500, "MaxConsecutiveToolErrors")]
    [InlineData(40, 3, 0, 500, "MaxInputTokens")]
    [InlineData(40, 3, 1_000, 0, "TargetInputTokens")]
    public void Given_A_Limit_Out_Of_Range_The_Agent_Fails_To_Declare_Naming_It(
        int iterations,
        int errors,
        int maximum,
        int target,
        string option)
    {
        // Act
        var declare = () => new LimitsAgent(iterations, errors, maximum, target);

        // Assert
        declare.Should().Throw<ArgumentOutOfRangeException>().WithMessage($"{option} must *");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Given_A_Blank_Model_The_Agent_Fails_To_Declare(string? model)
    {
        // Act
        var declare = () => new LimitsAgent(LimitsAgent.ModelProvider, model!);

        // Assert
        declare.Should().Throw<ArgumentException>().WithParameterName("model");
    }

    [Fact]
    public void Given_A_Blank_Provider_The_Agent_Fails_To_Declare()
    {
        // Act
        var declare = () => new LimitsAgent(" ", LimitsAgent.Model);

        // Assert
        declare.Should().Throw<ArgumentException>().WithParameterName("provider");
    }

    [Fact]
    public void Given_A_Blank_Summary_Model_The_Agent_Fails_To_Declare()
    {
        // Act
        var declare = () => new LimitsAgent(summaryModel: " ");

        // Assert
        declare.Should().Throw<ArgumentException>().WithParameterName("model");
    }

    [Fact]
    public async Task Given_Instructions_Set_In_The_Constructor_A_Run_Sends_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<InstructedAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Instructions.Should().Be(InstructedAgent.Prompt);
    }

    [Fact]
    public async Task Given_Instructions_Set_In_The_Constructor_A_Runs_Own_Replace_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var run = new InstructedAgent.Settings { Instructions = Data.Agent.Reason };
        await Host<InstructedAgent>().RunAsync(Data.Tool.Question, settings: run, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Instructions.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Generic_Method_Over_Any_Agent_It_Runs_The_Agent()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var answer = await AskAsync(Host<InstructedAgent>(), Data.Tool.Question, Ct);

        // Assert
        answer.Should().Be(Data.Tool.Answer);
        ChatClient.Options[0]!.Instructions.Should().Be(InstructedAgent.Prompt);
    }

    [Fact]
    public async Task Given_A_Token_Passed_Positionally_The_Run_Takes_It()
    {
        // Arrange
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        ChatClient.Reply(new TaskCompletionSource());

        var run = Host<ProbeAgent>().RunAsync(Data.Tool.Question, stopping.Token);
        await ChatClient.Requested;

        // Act
        await stopping.CancelAsync();
        var ending = () => run;

        // Assert
        await ending.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Given_The_Untyped_Stream_Read_Twice_The_Second_Read_Throws()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var chat = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        var run = chat.RunStreamingAsync(Data.Tool.Question, Ct);
        await run.ToListAsync(Ct);

        // Act
        var again = () => run.ToChatResponseAsync(Ct);

        // Assert
        await again.Should().ThrowAsync<InvalidOperationException>().WithMessage("*streams its updates once*");
        ChatClient.Requests.Should().ContainSingle();
        (await EntriesAsync(Data.Agent.ConversationId)).Count(entry => entry.EndsRun).Should().Be(1);
    }

    [Fact]
    public async Task Given_An_Untyped_Stream_Read_To_Its_End_Its_Response_Holds_The_Answer()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().RunStreamingAsync(Data.Tool.Question, Ct);

        // Act
        await run.ToListAsync(Ct);

        // Assert
        run.Response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Generic_Method_Over_Any_Agent_It_Resumes_Typed()
    {
        // Arrange
        var expected = new ProbeSettings { Limit = Data.Tool.Limit };
        var input = new ChatMessage(ChatRole.User, Data.Tool.Question);
        var interrupted = new ConversationEntry("probe_agent", [input], Time.GetUtcNow());

        await Services.GetRequiredService<ITypeshiftStore<Guid>>()
            .AppendConversationAsync(Data.Agent.ConversationId, interrupted, Ct);

        ChatClient.Reply(Text(JsonSerializer.Serialize(expected, Json.GetTypeInfo<ProbeSettings>())));

        // Act
        var resumed = await ResumeAsync<ProbeAgent, ProbeSettings>(
            Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId),
            Ct);

        // Assert
        resumed.Should().Be(expected);
    }

    [Fact]
    public async Task Given_Instructions_Built_From_The_Runs_Services_The_Model_Receives_Them()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await using var other = Services.CreateAsyncScope();
        var otherHost = other.ServiceProvider.GetRequiredService<AgentHost<ScopedInstructionsAgent>>();

        // Act
        await Host<ScopedInstructionsAgent>().RunAsync(Data.Tool.Question, Ct);
        await otherHost.RunAsync(Data.Tool.Question, Ct);

        // Assert
        ChatClient.Options.Select(options => options!.Instructions).Should().Equal(
            ScopedInstructionsAgent.For(Scope.GetRequiredService<CallScope>().Id),
            ScopedInstructionsAgent.For(other.ServiceProvider.GetRequiredService<CallScope>().Id));
    }

    [Fact]
    public async Task Given_No_Model_The_Run_Fails_Saying_How_To_Set_One()
    {
        // Act
        var run = () => Host<SchemaAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithModel*");
    }

    [Fact]
    public async Task Given_Resolved_Options_The_Created_Client_Runs_The_Agent()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var options = await host.ResolveOptionsAsync(settings: new() { Model = Data.Agent.Model }, ct: Ct);

        // Act
        using var client = await host.CreateChatClientAsync(options, Ct);
        var response = await client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], options, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, Data.Agent.Model));
        response.Messages.Should().OnlyContain(message => message.AuthorName == "probe_agent");
    }

    [Fact]
    public async Task Given_Options_Of_Its_Own_The_Created_Client_Asks_The_Agents_Provider()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var options = new ChatOptions { ModelId = Data.Agent.Model };

        // Act
        using var client = await host.CreateChatClientAsync(options, Ct);
        await client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], options, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_The_Tool_Loop_Taken_Out_Of_The_Created_Client_A_Call_On_It_Fails_Naming_The_Created_Client()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);
        var loop = client.GetService<FunctionInvokingChatClient>()!;

        // Act
        var call = () => loop.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], new(), Ct);

        // Assert
        await call.Should().ThrowAsync<InvalidOperationException>().WithMessage("*call the created client*");
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Two_Calls_On_One_Created_Client_Each_Keeps_Its_Own_Limit()
    {
        // Arrange
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();

        ChatClient
            .Reply(first, Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(second, Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var unlimited = await host.ResolveOptionsAsync(ct: Ct);
        var limited = await host.ResolveOptionsAsync(settings: new() { MaxToolIterations = 1 }, ct: Ct);
        using var client = await host.CreateChatClientAsync(limited, Ct);

        var running = client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], limited, Ct);
        await ChatClient.RequestedAsync(1);
        var other = client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], unlimited, Ct);
        await ChatClient.RequestedAsync(2);

        // Act
        first.SetResult();
        await running;
        second.SetResult();
        await other;

        // Assert
        ChatClient.Requests.Should().HaveCount(3);
    }

    // Code generic over the agent, as an endpoint or a test helper for every agent is
    private static async Task<string> AskAsync<TAgent>(AgentHost<TAgent> host, string input, CancellationToken ct)
        where TAgent : Agent =>
        (await host.RunAsync(input, ct)).Text;

    private static async Task<TResult> ResumeAsync<TAgent, TResult>(AgentHost<TAgent> host, CancellationToken ct)
        where TAgent : Agent =>
        (await host.ResumeAsync<TResult>(ct)).Result;
}
