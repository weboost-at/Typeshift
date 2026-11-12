using System.Text.Json;
using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.MultiAgent;

public class SubAgentResultTests : MultiAgentTestBase
{
    [Fact]
    public async Task Given_A_Sub_Agent_With_A_Result_Type_It_Is_Asked_For_The_Types_Schema()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Finding()));

        // Act
        await Host<TypedLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var format = (ChatResponseFormatJson)HelperClient.Options[0]!.ResponseFormat!;
        format.Schema!.Value.Assert.Properties("ticket", "reason");
    }

    [Fact]
    public async Task Given_A_Sub_Agent_With_A_Result_Type_Its_Caller_Reads_The_Result_As_Json()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Finding()));

        // Act
        await Host<TypedLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var result = (JsonElement)Result(ChatClient.Requests[1])!;
        result.Deserialize(Json.GetTypeInfo<HelperFinding>()).Should().Be(Expected());
    }

    [Fact]
    public async Task Given_A_Sub_Agent_Answering_Outside_Its_Result_Type_Its_Caller_Reads_Why()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        await Host<TypedLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Result(ChatClient.Requests[1])!.ToString().Should().Contain("did not answer in the JSON schema it was given");
    }

    [Fact]
    public async Task Given_A_Sub_Agent_With_A_Number_Result_Its_Caller_Reads_The_Number_Without_Data()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(JsonSerializer.Serialize(new { data = Data.Tool.Lives }, Json.SerializerOptions)));

        // Act
        await Host<CountingLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var result = (JsonElement)Result(ChatClient.Requests[1])!;
        result.GetInt32().Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_With_A_Result_Type_Its_Answer_Arrives_As_The_Json()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Finding()));

        // Act
        await Host<BackgroundTypedLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Requests[^1][^1].Text.Should().EndWith(Finding());
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_Answering_Outside_Its_Result_Type_Its_Caller_Reads_Why()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        await Host<BackgroundTypedLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Requests[^1][^1].Text.Should().Contain("did not answer in the JSON schema it was given");
    }

    private HelperFinding Expected() => new(Data.Agent.Ticket.Default, Data.Agent.Reason);

    private string Finding() => JsonSerializer.Serialize(Expected(), Json.GetTypeInfo<HelperFinding>());

    private static object? Result(IEnumerable<ChatMessage> request) =>
        request.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single().Result;
}
