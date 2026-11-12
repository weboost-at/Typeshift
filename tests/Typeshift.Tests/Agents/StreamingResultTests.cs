using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Agents;

public class StreamingResultTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Result_Type_A_Streaming_Run_Holds_The_Typed_Answer_Once_Read()
    {
        // Arrange
        var expected = new ProbeSettings { Limit = Data.Tool.Limit, Verbose = true };
        ChatClient.Reply(Text(JsonSerializer.Serialize(expected, Json.GetTypeInfo<ProbeSettings>())));

        var run = Host<ProbeAgent>().RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct);

        // Act
        await run.ToListAsync(Ct);

        // Assert
        run.Response.Result.Should().Be(expected);
    }

    [Fact]
    public async Task Given_A_Result_Type_A_Streaming_Run_Yields_The_Models_Updates()
    {
        // Arrange
        var expected = new ProbeSettings { Limit = Data.Tool.Limit };
        var json = JsonSerializer.Serialize(expected, Json.GetTypeInfo<ProbeSettings>());
        ChatClient.Reply(Text(json[..5]), Text(json[5..]));

        var run = Host<ProbeAgent>().RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct);

        // Act
        var updates = await run.ToListAsync(Ct);

        // Assert
        updates.ToChatResponse().Text.Should().Be(json);
    }

    [Fact]
    public async Task Given_A_Result_Type_A_Streaming_Run_Asks_The_Model_For_Its_Schema()
    {
        // Arrange
        ChatClient.Reply(Text(JsonSerializer.Serialize(new ProbeSettings(), Json.GetTypeInfo<ProbeSettings>())));

        // Act
        await Host<ProbeAgent>().RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        ResponseSchema().Assert.Properties("limit", "verbose");
    }

    [Fact]
    public async Task Given_A_Number_Result_A_Streaming_Run_Reads_It_From_Under_Data()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { data = Data.Tool.Lives }, Json.SerializerOptions);
        ChatClient.Reply(Text(json[..4]), Text(json[4..]));

        var run = Host<ProbeAgent>().RunStreamingAsync<int>(Data.Tool.Question, ct: Ct);

        // Act
        await run.ToListAsync(Ct);

        // Assert
        run.Response.Result.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_A_Tool_Call_A_Streaming_Run_Asks_For_The_Schema_Again_After_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(JsonSerializer.Serialize(new ProbeSettings(), Json.GetTypeInfo<ProbeSettings>())));

        // Act
        await Host<ProbeAgent>().RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        ChatClient.Options[1]!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
    }

    [Fact]
    public async Task Given_A_Conversation_A_Streaming_Run_Stores_Its_Typed_Answer()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new ProbeSettings { Limit = 1 }, Json.GetTypeInfo<ProbeSettings>());
        ChatClient.Reply(Text(json));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunStreamingAsync<ProbeSettings>(
            Data.Tool.Question,
            ct: Ct);

        // Act
        await run.ToListAsync(Ct);

        // Assert
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        entries.SelectMany(entry => entry.Messages).Should().Contain(message => message.Text == json);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Left_Early_Its_Response_Throws()
    {
        // Arrange
        ChatClient.Reply(Text("{"), Text("}"));
        var run = Host<ProbeAgent>().RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct);

        await foreach (var _ in run.WithCancellation(Ct))
        {
            break;
        }

        // Act
        var read = () => run.Response;

        // Assert
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Given_A_Streaming_Run_Not_Read_Yet_Its_Response_Throws()
    {
        // Arrange
        var run = Host<ProbeAgent>().RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct);

        // Act
        var read = () => run.Response;

        // Assert
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Read_Twice_The_Second_Read_Throws()
    {
        // Arrange
        ChatClient.Reply(Text(JsonSerializer.Serialize(new ProbeSettings(), Json.GetTypeInfo<ProbeSettings>())));
        var run = Host<ProbeAgent>().RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct);
        await run.ToListAsync(Ct);

        // Act
        var again = () => run.ToListAsync(Ct).AsTask();

        // Assert
        await again.Should().ThrowAsync<InvalidOperationException>();
    }
}
