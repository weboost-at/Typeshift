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
    public void Given_A_Streaming_Run_Not_Read_To_Its_End_Its_Response_Throws()
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
