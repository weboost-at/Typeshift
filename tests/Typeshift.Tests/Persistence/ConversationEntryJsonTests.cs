using System.Text.Json;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace Typeshift.Tests.Persistence;

public class ConversationEntryJsonTests : UnitTestBase
{
    [Fact]
    public void Given_An_Entry_With_Every_Member_Set_It_Reads_Back_The_Same()
    {
        // Arrange
        var entry = Data.Persistence.Entry;

        // Act
        var read = ConversationEntry.Deserialize(entry.Serialize());

        // Assert
        read.Serialize().Should().Be(entry.Serialize());
    }

    [Fact]
    public void Given_An_Entry_System_Text_Json_Writes_What_The_Store_Keeps()
    {
        // Arrange
        var entry = Data.Persistence.Entry;

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().Be(entry.Serialize());
    }

    [Fact]
    public void Given_Stored_Json_System_Text_Json_Reads_The_Entry()
    {
        // Arrange
        var entry = Data.Persistence.Entry;
        var json = entry.Serialize();

        // Act
        var read = JsonSerializer.Deserialize<ConversationEntry>(json);

        // Assert
        read!.Serialize().Should().Be(json);
    }

    [Fact]
    public void Given_Stored_Json_With_A_Member_The_Entry_Lacks_System_Text_Json_Reads_The_Entry()
    {
        // Arrange
        var json = Data.Persistence.Entry.Serialize();
        var unknown = json.Insert(1, "\"renamed\":1,");

        // Act
        var read = JsonSerializer.Deserialize<ConversationEntry>(unknown);

        // Assert
        read!.Serialize().Should().Be(json);
    }

    [Fact]
    public void Given_An_Entry_Newtonsoft_Json_Reads_Back_The_Same()
    {
        // Arrange
        var entry = Data.Persistence.Entry;

        // Act
        var read = JsonConvert.DeserializeObject<ConversationEntry>(JsonConvert.SerializeObject(entry));

        // Assert
        read!.Serialize().Should().Be(entry.Serialize());
    }

    [Fact]
    public void Given_A_Model_Response_Stored_Before_Entries_Kept_An_Estimate_It_Reads_With_Its_Usage_And_None()
    {
        // Arrange
        const string json =
            """
            {"agent":"compacting_agent","at":"2026-10-11T08:00:00+00:00","model":"compacting-model",
            "usage":{"inputTokenCount":2000,"outputTokenCount":12},
            "messages":[{"authorName":"compacting_agent","role":"assistant",
            "contents":[{"$type":"text","text":"Done."}]}]}
            """;

        // Act
        var entry = ConversationEntry.Deserialize(json);

        // Assert
        entry.Usage!.InputTokenCount.Should().Be(2_000);
        entry.EstimatedInputTokens.Should().BeNull();
    }

    [Fact]
    public void Given_A_Model_Response_Stored_Before_Entries_Named_Their_Provider_It_Reads_With_Its_Model_And_None()
    {
        // Arrange
        const string json =
            """
            {"agent":"support_agent","at":"2026-11-14T09:00:00+00:00","model":"gpt-5-mini",
            "usage":{"inputTokenCount":2000,"outputTokenCount":12},"estimatedInputTokens":1900,
            "messages":[{"authorName":"support_agent","role":"assistant",
            "contents":[{"$type":"text","text":"Done."}]}]}
            """;

        // Act
        var entry = ConversationEntry.Deserialize(json);

        // Assert
        entry.Model.Should().Be("gpt-5-mini");
        entry.Provider.Should().BeNull();
        entry.Serialize().Should().Be(json.ReplaceLineEndings(""));
    }

    [Fact]
    public void Given_An_Entry_Stored_Before_Entries_Were_Numbered_It_Reads_With_No_Number()
    {
        // Arrange
        const string json =
            """
            {"agent":"support_agent","at":"2026-11-14T09:00:00+00:00","model":"gpt-5-mini",
            "usage":{"inputTokenCount":2000,"outputTokenCount":12},"endsRun":true,
            "messages":[{"authorName":"support_agent","role":"assistant",
            "contents":[{"$type":"text","text":"Done."}]}]}
            """;

        // Act
        var entry = ConversationEntry.Deserialize(json);

        // Assert
        entry.Sequence.Should().Be(0);
        entry.Serialize().Should().Be(json.ReplaceLineEndings(""));
    }

    [Fact]
    public void Given_A_Numbered_Entry_Its_Json_Names_No_Schema_Version_An_Earlier_Version_Refuses()
    {
        // Arrange
        var entry = Data.Persistence.Entry;

        // Act
        var json = entry.Serialize();

        // Assert
        json.Should().NotContain("$schemaVersion");
    }

    [Fact]
    public void Given_An_Entry_Without_A_Provider_Its_Json_Has_No_Member_For_It()
    {
        // Arrange
        var entry = Data.Persistence.Entry with { Provider = null };

        // Act
        var json = entry.Serialize();

        // Assert
        json.Should().NotContain("\"provider\"");
    }

    [Fact]
    public void Given_An_Entry_Without_An_Estimate_Its_Json_Has_No_Member_For_It()
    {
        // Arrange
        var entry = Data.Persistence.Entry with { EstimatedInputTokens = null };

        // Act
        var json = entry.Serialize();

        // Assert
        json.Should().NotContain("estimatedInputTokens");
    }

    [Fact]
    public void Given_An_Entry_Of_A_Newer_Schema_Version_Reading_Throws_A_JsonException()
    {
        // Arrange
        var json = $$"""{"$schemaVersion":{{ConversationEntry.SchemaVersion + 1}}}""";

        // Act
        var read = () => ConversationEntry.Deserialize(json);

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*newer*");
    }

    [Fact]
    public void Given_Text_Outside_Ascii_The_Entry_Stores_It_As_Written()
    {
        // Arrange
        var message = new ChatMessage(ChatRole.User, Data.Persistence.TextOutsideAscii);
        var entry = new ConversationEntry("probe_agent", [message], Time.GetUtcNow());

        // Act
        var json = entry.Serialize();

        // Assert
        json.Should().Contain(Data.Persistence.TextOutsideAscii);
    }

    [Fact]
    public void Given_Markup_In_Text_The_Entry_Stores_It_Escaped()
    {
        // Arrange
        var message = new ChatMessage(ChatRole.User, $"<script>{Data.Tool.Query}</script>");
        var entry = new ConversationEntry("probe_agent", [message], Time.GetUtcNow());

        // Act
        var json = entry.Serialize();

        // Assert
        json.Should().Contain($@"\u003Cscript\u003E{Data.Tool.Query}\u003C/script\u003E");
    }

    [Fact]
    public void Given_An_Entry_With_Every_Character_Outside_Ascii_Escaped_Its_Text_Reads_Back()
    {
        // Arrange
        var json = """
            {"agent":"probe_agent","at":"2026-10-11T08:00:00+00:00","messages":[{"role":"user","contents":[
              {"$type":"text","text":"Gr\u00F6\u00DFe: \u4E2D\u6587 it\u0027s \u0022ok\u0022"}]}]}
            """;

        // Act
        var read = ConversationEntry.Deserialize(json);

        // Assert
        read.Messages[0].Text.Should().Be("Größe: 中文 it's \"ok\"");
    }

    [Fact]
    public void Given_An_Entry_With_A_Content_It_Cannot_Read_The_Others_Load()
    {
        // Arrange
        var json = $$"""
            {"agent":"probe_agent","at":"2026-10-11T08:00:00+00:00","messages":[{"role":"assistant","contents":[
              {"$type":"text","text":"{{Data.Tool.Answer}}"},
              {"$type":"functionApprovalRequest","id":"r1","functionCall":{"callId":"c1","name":"probe"} }]}]}
            """;

        // Act
        var read = ConversationEntry.Deserialize(json);

        // Assert
        read.Messages.Should().ContainSingle(message => message.Text == Data.Tool.Answer);
    }

    [Fact]
    public void Given_A_Message_Whose_Every_Content_It_Cannot_Read_The_Entry_Loads_Without_The_Message()
    {
        // Arrange
        var json = $$"""
            {"agent":"probe_agent","at":"2026-10-11T08:00:00+00:00","messages":[
              {"role":"assistant","contents":[{"$type":"text","text":"{{Data.Tool.Answer}}"}]},
              {"role":"assistant","contents":[{"$type":"vendorThought","text":"{{Data.Tool.Query}}"}]}]}
            """;

        // Act
        var read = ConversationEntry.Deserialize(json);

        // Assert
        read.Messages.Should().ContainSingle(message => message.Text == Data.Tool.Answer);
    }

    [Fact]
    public void Given_A_Tool_Result_Of_Content_It_Reads_Back_As_The_Content()
    {
        // Arrange
        var picture = ContentToolset.Drawn(Data.Tool.Query);
        var entry = new ConversationEntry(
            "probe_agent",
            [new ChatMessage(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, picture)])],
            DateTimeOffset.UnixEpoch);

        // Act
        var read = ConversationEntry.Deserialize(entry.Serialize());

        // Assert
        ((ResultOf(read) as DataContent)?.Uri).Should().Be(picture.Uri);
    }

    [Fact]
    public void Given_A_Tool_Result_Of_Several_Contents_It_Reads_Back_As_Each()
    {
        // Arrange
        AIContent[] contents = [new TextContent(Data.Tool.Query), ContentToolset.Drawn(Data.Tool.Query)];
        var entry = new ConversationEntry(
            "probe_agent",
            [new ChatMessage(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, contents)])],
            DateTimeOffset.UnixEpoch);

        // Act
        var read = ConversationEntry.Deserialize(entry.Serialize());

        // Assert
        (ResultOf(read) as IEnumerable<AIContent> ?? [])
            .Select(content => content is TextContent text ? text.Text : (content as DataContent)?.Uri)
            .Should()
            .Equal(Data.Tool.Query, ContentToolset.Drawn(Data.Tool.Query).Uri);
    }

    [Fact]
    public void Given_A_Tool_Result_Of_Content_The_Content_Reads_Back_Without_The_Mark()
    {
        // Arrange
        var entry = new ConversationEntry(
            "probe_agent",
            [
                new ChatMessage(
                    ChatRole.Tool,
                    [new FunctionResultContent(Data.Tool.CallId, ContentToolset.Drawn(Data.Tool.Query))])
            ],
            DateTimeOffset.UnixEpoch);

        // Act
        var read = ConversationEntry.Deserialize(entry.Serialize());

        // Assert
        read.Messages[0].Contents[0].AdditionalProperties.Should().BeNull();
    }

    [Fact]
    public void Given_A_Tool_Result_Of_Json_That_Looks_Like_Content_It_Reads_Back_As_Json()
    {
        // Arrange
        var json = JsonElement.Parse("""{"$type":"text","text":"not content"}""");
        var entry = new ConversationEntry(
            "probe_agent",
            [new ChatMessage(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, json)])],
            DateTimeOffset.UnixEpoch);

        // Act
        var read = ConversationEntry.Deserialize(entry.Serialize());

        // Assert
        ResultOf(read).Should().BeOfType<JsonElement>();
    }

    private static object? ResultOf(ConversationEntry entry) =>
        entry.Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single().Result;
}
