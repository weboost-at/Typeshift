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
        Models.Should().Equal(ProbeAgent.Model);
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
            Model = Data.Agent.Model,
            Temperature = Data.Agent.Temperature,
            Instructions = Data.Agent.Instructions
        };

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings, Ct);
        var options = ChatClient.Options[0]!;

        // Assert
        Models.Should().Equal(Data.Agent.Model);
        options.Temperature.Should().Be(Data.Agent.Temperature);
        options.Instructions.Should().Be(Data.Agent.Instructions);
    }

    [Fact]
    public async Task Given_Run_Settings_That_Leave_An_Option_Null_The_Agents_Code_Holds()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, new() { Temperature = Data.Agent.Temperature }, Ct);

        // Assert
        Models.Should().Equal(ProbeAgent.Model);
        ChatClient.Options[0]!.StopSequences.Should().Equal(ProbeAgent.Stop);
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
            .Equal("probe", "find_companies", new HostedWebSearchTool().Name);
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
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings, Ct);
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
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, new() { MaxToolIterations = 1 }, Ct);

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
        var options = await host.ResolveOptionsAsync(new() { Model = Data.Agent.Model }, Ct);

        // Act
        using var client = await host.CreateChatClientAsync(options, Ct);
        var response = await client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], options, Ct);

        // Assert
        Models.Should().Equal(Data.Agent.Model);
        response.Messages.Should().OnlyContain(message => message.AuthorName == "probe_agent");
    }
}
