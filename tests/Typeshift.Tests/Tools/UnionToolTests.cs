using System.Text.Json;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Tools;

public class UnionToolTests : HostTestBase
{
    [Fact]
    public void Given_A_Union_Parameter_Each_Case_Of_Its_Schema_Names_Its_Type()
    {
        // Act
        var adopt = UnionToolset.CreateAdopt(Json);

        // Assert
        adopt.JsonSchema.Assert.ParameterCaseTypes("pet", "object", "object");
    }

    [Fact]
    public async Task Given_A_Union_Of_Records_The_Argument_Binds_The_Case_Whose_Properties_It_Has()
    {
        // Arrange
        var cat = new Cat(Data.Tool.PetName, Data.Tool.Lives);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "adopt", ("pet", cat)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [UnionToolset.CreateAdopt(Json)] };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<Cat>(Data.Tool.CallId, Json).Should().Be(cat);
    }

    [Fact]
    public async Task Given_A_Number_Or_Text_Union_A_Json_String_Binds_The_Text_Case()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "find_ticket", ("key", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [UnionToolset.CreateFindTicket(Json)] };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<TicketLookup>(Data.Tool.CallId, Json).Title.Should().Be(Data.Tool.Query);
    }

    [Fact]
    public async Task Given_A_Number_Or_Text_Union_A_Json_Number_Binds_The_Number_Case()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "find_ticket", ("key", Data.Tool.Lives)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [UnionToolset.CreateFindTicket(Json)] };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<TicketLookup>(Data.Tool.CallId, Json).Number.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_A_Union_With_A_Classifier_Of_Its_Own_That_Classifier_Picks_The_Case()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "find_ticket_by_number", ("number", $"{Data.Tool.Lives}")))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [UnionToolset.CreateFindTicketByNumber(Json)] };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<TicketLookup>(Data.Tool.CallId, Json).Number.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_Two_Cases_That_Read_A_Json_String_A_String_Fails_The_Call()
    {
        // Arrange
        var redeem = UnionToolset.CreateRedeem(Json);
        var code = JsonSerializer.SerializeToElement(Data.Tool.Query);

        // Act
        var failure = await Record.ExceptionAsync(() => redeem.InvokeAsync(Arguments(("code", code)), Ct).AsTask());

        // Assert
        failure.Should().BeOfType<JsonException>();
    }

    [Fact]
    public async Task Given_Json_That_Fits_No_Case_The_Failure_Names_The_Cases()
    {
        // Arrange
        var find = UnionToolset.CreateFindTicket(Json);
        var key = JsonSerializer.SerializeToElement(true);

        // Act
        var failure = await Record.ExceptionAsync(() => find.InvokeAsync(Arguments(("key", key)), Ct).AsTask());

        // Assert
        failure!.Message.Should().Be("A JSON true fits no single case of TicketKey: Int32, String.");
    }

    [Fact]
    public async Task Given_A_Union_Without_A_Text_Case_A_Json_String_Of_A_Number_Binds_The_Number_Case()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "reserve", ("seats", $"{Data.Tool.Lives}")))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [UnionToolset.CreateReserve(Json)] };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<int>(Data.Tool.CallId, Json).Should().Be(Data.Tool.Lives);
    }
}
