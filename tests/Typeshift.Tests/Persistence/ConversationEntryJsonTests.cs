using System.Text.Json;
using FluentAssertions;
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
}
