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
    public async Task Given_A_Failing_Tool_Call_The_Model_Reads_Why_It_Failed()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        var result = response.Assert.ToolResult(Data.Tool.CallId).Result;

        // Assert
        result.As<string>().Should().Contain("The tool 'probe' requires the argument 'query'.");
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
}
