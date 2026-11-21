using System.Text.Json;
using Typeshift.Generated;
using FluentAssertions;

namespace Typeshift.Tests.Persistence;

public class DecisionDocumentJsonTests : UnitTestBase
{
    [Fact]
    public void Given_A_Document_With_Every_Member_Set_It_Reads_Back_The_Same()
    {
        // Arrange
        var document = Data.Decisions.Document;

        // Act
        var read = DecisionDocument.Deserialize(document.Serialize());

        // Assert
        read.Serialize().Should().Be(document.Serialize());
        read.Batch!.Asks.First().Answer.Should().Be(document.Batch!.Asks.First().Answer);
    }

    [Fact]
    public void Given_A_Batch_With_An_Answered_Approval_It_Is_Stored_In_Its_Documented_Shape()
    {
        // Arrange
        var asked = Time.GetUtcNow();
        var ask = new Ask(Data.Decisions.DecisionId, AskKind.Approval)
        {
            Answer = DecisionAnswer.Approve(Data.Decisions.DecisionId)
        };

        var call = new BatchCall(Data.Tool.CallId, "close_ticket", JsonElement.Parse("""{"number":1234}"""))
        {
            Ask = ask
        };

        var document = new DecisionDocument
        {
            Standing = [new StandingApproval("add_note", null, asked)],
            Batch = new DecisionBatch("support_agent", asked, BatchState.Open, [call])
        };

        // Act
        var json = document.Serialize();

        // Assert
        json.Should().Be(Compact(
            $$"""
            {
              "standing": [{ "tool": "add_note", "at": "2026-10-11T08:00:00+00:00" }],
              "batch": {
                "agent": "support_agent",
                "askedAt": "2026-10-11T08:00:00+00:00",
                "state": "open",
                "calls": [
                  {
                    "callId": "{{Data.Tool.CallId}}",
                    "tool": "close_ticket",
                    "arguments": { "number": 1234 },
                    "ask": { "id": "{{Data.Decisions.DecisionId}}", "kind": "approval", "answer": { "approved": true } }
                  }
                ]
              },
              "closed": []
            }
            """));
    }

    [Fact]
    public void Given_Answered_Questions_Their_Answers_Are_Stored_Each_With_Its_Labels_And_Its_Words()
    {
        // Arrange
        var answer = DecisionAnswer.Answer(Data.Decisions.DecisionId, new QuestionAnswer(["Dapper"]), "Orders");
        var ask = new Ask(Data.Decisions.DecisionId, AskKind.Question) { Answer = answer };

        // Act
        var json = StorableJson.Write(ask.Write);

        // Assert
        json.Should().Be(Compact(
            $$"""
            {
              "id": "{{Data.Decisions.DecisionId}}",
              "kind": "question",
              "answer": {
                "approved": true,
                "answers": [{ "selected": ["Dapper"] }, { "selected": [], "text": "Orders" }]
              }
            }
            """));
    }

    [Fact]
    public void Given_Stored_Answers_To_Questions_They_Read_Back_Equal()
    {
        // Arrange
        var asks = Data.Decisions.Document.Batch!.Asks.ToList();

        // Act
        var read = DecisionDocument.Deserialize(Data.Decisions.Document.Serialize()).Batch!.Asks.ToList();

        // Assert
        read[^1].Answer!.Answers.Should().HaveCount(2);
        read[^1].Answer.Should().Be(asks[^1].Answer);
    }

    [Fact]
    public void Given_A_Document_That_Names_A_Mode_It_Is_Stored_By_Its_Name()
    {
        // Arrange
        var document = new DecisionDocument { Mode = AgentMode.Auto };

        // Act
        var json = document.Serialize();

        // Assert
        json.Should().Be("""{"mode":"Auto","standing":[],"closed":[]}""");
    }

    [Fact]
    public void Given_A_Document_Stored_Before_The_Mode_Joined_It_It_Reads_As_Naming_No_Mode()
    {
        // Arrange
        var json = """{"standing":[{"tool":"add_note","at":"2026-10-11T08:00:00+00:00"}],"closed":[]}""";

        // Act
        var document = DecisionDocument.Deserialize(json);

        // Assert
        document.Mode.Should().BeNull();
        document.ToDecisions().Mode.Should().BeNull();
    }

    [Fact]
    public void Given_A_Cancelled_Batch_And_A_Decision_Closed_As_Cancelled_Both_Are_Stored_By_Name_And_Read_Back()
    {
        // Arrange
        var document = new DecisionDocument
        {
            Batch = new DecisionBatch("support_agent", Time.GetUtcNow(), BatchState.Cancelled, []),
            Closed = [new ClosedDecision(Data.Decisions.DecisionId, DecisionClosedReason.Cancelled)]
        };

        // Act
        var json = document.Serialize();
        var read = DecisionDocument.Deserialize(json);

        // Assert
        json.Should().Contain("\"state\":\"cancelled\"");
        json.Should().Contain("\"reason\":\"cancelled\"");
        read.Batch!.State.Should().Be(BatchState.Cancelled);
        read.ClosedReason(Data.Decisions.DecisionId).Should().Be(DecisionClosedReason.Cancelled);
    }

    [Fact]
    public void Given_No_Stored_Members_An_Empty_Object_Reads_As_A_Document_Without_A_Batch()
    {
        // Act
        var document = DecisionDocument.Deserialize("{}");

        // Assert
        document.Batch.Should().BeNull();
        document.Standing.Should().BeEmpty();
    }

    [Fact]
    public void Given_Members_This_Version_Does_Not_Know_They_Are_Ignored()
    {
        // Arrange
        var json = """{"later":{"x":1},"standing":[{"tool":"add_note","at":"2026-11-13T09:12:00Z","scope":1}]}""";

        // Act
        var document = DecisionDocument.Deserialize(json);

        // Assert
        document.Standing.Select(approval => approval.Tool).Should().Equal("add_note");
    }

    [Fact]
    public void Given_A_Document_Of_A_Newer_Schema_Version_Reading_Throws_A_JsonException()
    {
        // Arrange
        var json = $$"""{"$schemaVersion":{{DecisionDocument.SchemaVersion + 1}}}""";

        // Act
        var read = () => DecisionDocument.Deserialize(json);

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*newer*");
    }

    [Fact]
    public void Given_An_Approval_Decision_System_Text_Json_Writes_Its_Kind_Its_Tool_And_Its_Arguments()
    {
        // Arrange
        var call = Data.Decisions.Document.Batch!.Calls[0];
        var decision = call.ToDecision(Data.Decisions.Document.Batch)!;

        // Act
        var json = JsonDocument.Parse(JsonSerializer.Serialize(decision)).RootElement;

        // Assert
        json.GetProperty("kind").GetString().Should().Be("approval");
        json.GetProperty("tool").GetString().Should().Be(call.Tool);
        json.GetProperty("server").GetString().Should().Be(call.Server);
        json.GetProperty("arguments").GetRawText().Should().Be(call.Arguments.GetRawText());
    }

    [Fact]
    public void Given_The_Json_Of_A_Decision_System_Text_Json_Reads_It_Back_As_Its_Kind()
    {
        // Arrange
        var decision = Data.Decisions.Document.Batch!.Calls[1].ToDecision(Data.Decisions.Document.Batch)!;
        var json = JsonSerializer.Serialize(decision);

        // Act
        var read = JsonSerializer.Deserialize<Decision>(json);

        // Assert
        read.Should().BeOfType<PlanDecision>();
        JsonSerializer.Serialize(read).Should().Be(json);
    }

    [Fact]
    public void Given_A_Question_Decision_System_Text_Json_Writes_Its_Questions_With_Their_Options()
    {
        // Arrange
        var batch = Data.Decisions.Document.Batch!;
        var decision = batch.Calls[^1].ToDecision(batch)!;

        // Act
        var json = JsonDocument.Parse(JsonSerializer.Serialize(decision)).RootElement;
        var question = json.GetProperty("questions")[0];

        // Assert
        json.GetProperty("kind").GetString().Should().Be("question");
        question.GetProperty("header").GetString().Should().Be("Libraries");
        question.GetProperty("text").GetString().Should().Be("Which libraries may the data layer use?");
        question.GetProperty("multiSelect").GetBoolean().Should().BeTrue();
        question.GetProperty("options")[1].GetRawText().Should().Be(
            """{"label":"EF Core","description":"An object mapper."}""");
    }

    [Fact]
    public void Given_The_Json_Of_A_Question_Decision_System_Text_Json_Reads_Its_Questions_Back()
    {
        // Arrange
        var batch = Data.Decisions.Document.Batch!;
        var json = JsonSerializer.Serialize(batch.Calls[^1].ToDecision(batch)!);

        // Act
        var read = JsonSerializer.Deserialize<Decision>(json);

        // Assert
        read.Should().BeOfType<QuestionDecision>();
        JsonSerializer.Serialize(read).Should().Be(json);
    }

    [Fact]
    public void Given_A_Decision_Typed_As_Its_Kind_System_Text_Json_Writes_The_Same_Json()
    {
        // Arrange
        var batch = Data.Decisions.Document.Batch!;
        var decision = (ApprovalDecision)batch.Calls[0].ToDecision(batch)!;

        // Act
        var json = JsonSerializer.Serialize(decision);

        // Assert
        json.Should().Be(JsonSerializer.Serialize<Decision>(decision));
    }

    [Fact]
    public void Given_A_Decision_Of_A_Kind_This_Version_Does_Not_Know_Reading_Throws_A_JsonException()
    {
        // Act
        var read = () => JsonSerializer.Deserialize<Decision>("""{"kind":"vote","id":"1"}""");

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*no kind of decision*");
    }

    [Fact]
    public void Given_An_Answer_A_Page_Posts_System_Text_Json_Reads_It_With_The_Web_Defaults()
    {
        // Arrange
        var json = $$"""{"decisionId":"{{Data.Decisions.DecisionId}}","approved":true,"mode":"Auto"}""";

        // Act
        var answer = JsonSerializer.Deserialize<DecisionAnswer>(json, JsonSerializerOptions.Web);

        // Assert
        answer.Should().Be(DecisionAnswer.ApprovePlan(Data.Decisions.DecisionId, AgentMode.Auto));
    }

    [Fact]
    public void Given_Answers_To_Questions_A_Page_Posts_System_Text_Json_Reads_Labels_And_Words()
    {
        // Arrange
        var id = Data.Decisions.DecisionId;
        var json = $$"""{"decisionId":"{{id}}","answers":[{"selected":["Dapper"]},{"text":"Orders"}]}""";

        // Act
        var answer = JsonSerializer.Deserialize<DecisionAnswer>(json, JsonSerializerOptions.Web)!;

        // Assert
        answer.Answers.Should().Equal(new QuestionAnswer(["Dapper"]), new QuestionAnswer([], "Orders"));
    }

    [Fact]
    public void Given_An_Answer_System_Text_Json_Writes_Its_Mode_By_Name()
    {
        // Arrange
        var answer = DecisionAnswer.ApprovePlan(Data.Decisions.DecisionId, AgentMode.Auto);

        // Act
        var json = JsonSerializer.Serialize(answer, JsonSerializerOptions.Web);

        // Assert
        json.Should().Contain("\"mode\":\"Auto\"");
    }

    // The JSON as a store keeps it, without the white space the expectation is laid out with
    private static string Compact(string json) => string.Concat(json.Where(character => !char.IsWhiteSpace(character)));
}
