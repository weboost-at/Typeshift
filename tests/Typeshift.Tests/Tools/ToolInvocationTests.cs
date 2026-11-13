using System.Text.Json;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Tools;

public class ToolInvocationTests : HostTestBase
{
    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddScoped<CallScope>().AddSingleton<StreamGate>();

    [Fact]
    public async Task Given_A_Scripted_Call_The_Tool_Receives_Its_Arguments()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [ProbeToolset.CreateProbe(new ProbeSettings(), Json)] };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json).Query.Should().Be(Data.Tool.Query);
    }

    [Fact]
    public async Task Given_Two_Calls_Of_One_Run_Both_Resolve_From_The_Runs_Scope()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)),
                Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query))))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [ProbeToolset.CreateProbe(new ProbeSettings(), Json)] };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);
        var first = response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json);
        var second = response.Assert.ToolResult<ProbeResult>(Data.Tool.SecondCallId, Json);

        // Assert
        first.Scope.Should().Be(Scope.GetRequiredService<CallScope>().Id);
        second.Scope.Should().Be(first.Scope);
    }

    [Fact]
    public async Task Given_A_Call_The_Tool_Receives_The_Services_Its_Arguments_Carry()
    {
        // Arrange
        var probe = ProbeToolset.CreateProbe(new ProbeSettings(), Json);

        // Act
        var result = ReadResult<ProbeResult>(await probe.InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct));

        // Assert
        result.ArgumentsCarryServices.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Call_The_Tool_Receives_The_Calls_Token()
    {
        // Arrange
        var probe = ProbeToolset.CreateProbe(new ProbeSettings(), Json);

        // Act
        var result = ReadResult<ProbeResult>(await probe.InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct));

        // Assert
        result.Cancellable.Should().BeTrue();
    }

    [Fact]
    public async Task Given_Settings_The_Tool_Runs_With_Them()
    {
        // Arrange
        var settings = new ProbeSettings { Limit = Data.Tool.Limit };
        var probe = ProbeToolset.CreateProbe(settings, Json);

        // Act
        var result = ReadResult<ProbeResult>(await probe.InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct));

        // Assert
        result.Settings.Should().Be(settings);
    }

    [Fact]
    public async Task Given_A_Scripted_Call_The_Result_Reaches_The_Model_As_Json()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [ProbeToolset.CreateProbe(new ProbeSettings(), Json)] };

        // Act
        await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        var sent = ChatClient.Requests[1]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single();

        // Assert
        ReadResult<ProbeResult>(sent.Result).Query.Should().Be(Data.Tool.Query);
    }

    [Fact]
    public async Task Given_No_Services_The_Call_Fails_Saying_How_To_Run_It()
    {
        // Arrange
        var probe = ProbeToolset.CreateProbe(new ProbeSettings(), Json);
        var arguments = new AIFunctionArguments(new Dictionary<string, object?> { ["query"] = Data.Tool.Query });

        // Act
        var invoke = async () => await probe.InvokeAsync(arguments, Ct);

        // Assert
        await invoke.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AgentHost*");
    }

    [Fact]
    public async Task Given_A_Tool_That_Streams_Its_Results_The_Model_Receives_Them_As_One_Array()
    {
        // Arrange
        var split = StreamingToolset.CreateSplitWords(Json);

        // Act
        var result = ReadResult<string[]>(await split.InvokeAsync(Arguments(("text", "one two three")), Ct));

        // Assert
        result.Should().Equal("one", "two", "three");
    }

    [Fact]
    public async Task Given_A_Stream_Tool_Without_A_Token_A_Cancelled_Call_Stops_Reading()
    {
        // Arrange
        var count = StreamingToolset.CreateCountSlowly(Json);
        var gate = Scope.GetRequiredService<StreamGate>();
        using var cancelling = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        // Act
        var reading = count.InvokeAsync(Arguments(), cancelling.Token).AsTask();
        await gate.FirstRead.Task.WaitAsync(Ct);
        await cancelling.CancelAsync();
        gate.Opened.SetResult();
        var read = async () => await reading;

        // Assert
        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Given_A_Missing_Required_Argument_The_Call_Fails_With_An_ArgumentException()
    {
        // Arrange
        var count = SchemaToolset.CreateCountWords(Json);

        // Act
        var invoke = async () => await count.InvokeAsync(Arguments(), Ct);

        // Assert
        await invoke.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("number", true)]
    [InlineData("number", false)]
    [InlineData("reason", true)]
    [InlineData("reason", false)]
    public async Task Given_A_Null_For_A_Required_Argument_The_Call_Fails_Naming_It(string argument, bool asJson)
    {
        // Arrange
        var refund = BindingToolset.CreateRefund(Json);
        var arguments = Arguments(("number", Data.Tool.Lives), ("reason", Data.Tool.Query));
        arguments[argument] = asJson ? JsonElement.Parse("null") : null;

        // Act
        var failure = await Record.ExceptionAsync(() => refund.InvokeAsync(arguments, Ct).AsTask());

        // Assert
        failure!.Message.Should().StartWith($"The tool 'refund' requires the argument '{argument}', and it was");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_A_Null_For_A_Nullable_Required_Argument_The_Tool_Receives_Null(bool asJson)
    {
        // Arrange
        var remind = BindingToolset.CreateRemind(Json);
        object? nothing = asJson ? JsonElement.Parse("null") : null;

        // Act
        var result = await remind.InvokeAsync(Arguments(("days", nothing), ("note", nothing)), Ct);

        // Assert
        ReadResult<Reminder>(result).Should().Be(new Reminder(null, null));
    }

    [Fact]
    public async Task Given_A_Json_Null_For_An_Argument_That_Takes_Any_Json_The_Tool_Receives_It()
    {
        // Arrange
        var echo = SchemaToolset.CreateEchoJson(Json);

        // Act
        var result = await echo.InvokeAsync(Arguments(("value", JsonElement.Parse("null"))), Ct);

        // Assert
        ((JsonElement)result!).ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Given_An_Argument_Of_The_Wrong_Type_The_Failure_Names_The_Argument_And_The_Tool()
    {
        // Arrange
        var refund = BindingToolset.CreateRefund(Json);
        var arguments = Arguments(("number", JsonElement.Parse("\"abc\"")), ("reason", Data.Tool.Query));

        // Act
        var failure = await Record.ExceptionAsync(() => refund.InvokeAsync(arguments, Ct).AsTask());

        // Assert
        failure!.Message.Should().StartWith("The argument 'number' of the tool 'refund' does not fit its schema:");
    }

    [Fact]
    public async Task Given_An_Argument_Of_The_Wrong_Type_The_Model_Reads_The_Tools_And_The_Arguments_Name()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "refund", ("number", Data.Tool.Lives), ("reason", Data.Tool.Lives)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions { Tools = [BindingToolset.CreateRefund(Json)] };
        var loop = new FunctionInvokingChatClient(ChatClient, null, Scope) { IncludeDetailedErrors = true };

        // Act
        var response = await loop.GetResponseAsync(Data.Tool.Question, options, Ct);
        var result = response.Assert.ToolResult(Data.Tool.CallId).Result;

        // Assert
        result.As<string>().Should().Contain("The argument 'reason' of the tool 'refund' does not fit");
    }

    [Fact]
    public async Task Given_An_Object_Argument_Without_A_Required_Member_The_Failure_Names_The_Member()
    {
        // Arrange
        var ship = BindingToolset.CreateShip(Json);
        var parcel = JsonElement.Parse("""{"id":"p1"}""");

        // Act
        var failure = await Record.ExceptionAsync(() => ship.InvokeAsync(Arguments(("parcel", parcel)), Ct).AsTask());

        // Assert
        failure!.Message.Should().Contain("'destination'");
    }

    [Fact]
    public async Task Given_An_Object_Argument_With_A_Null_For_A_Member_That_Takes_None_The_Failure_Names_The_Member()
    {
        // Arrange
        var ship = BindingToolset.CreateShip(Json);
        var parcel = JsonElement.Parse("""{"id":"p1","destination":null}""");

        // Act
        var failure = await Record.ExceptionAsync(() => ship.InvokeAsync(Arguments(("parcel", parcel)), Ct).AsTask());

        // Assert
        failure!.Message.Should().Contain("'Destination'");
    }

    [Fact]
    public async Task Given_An_Absent_Optional_Argument_The_Tool_Takes_Its_Default()
    {
        // Arrange
        var search = SchemaToolset.CreateSearchCompanies(Json);

        var arguments = Arguments(
            ("query", Data.Tool.Query),
            ("country", null),
            ("known", Array.Empty<Company>()));

        // Act
        var result = await search.InvokeAsync(arguments, Ct);

        // Assert
        ReadResult<int>(result).Should().Be(10);
    }

    [Fact]
    public async Task Given_A_Json_Null_For_An_Optional_Argument_The_Tool_Takes_Its_Default()
    {
        // Arrange
        var search = SchemaToolset.CreateSearchCompanies(Json);

        var arguments = Arguments(
            ("query", Data.Tool.Query),
            ("country", null),
            ("known", Array.Empty<Company>()),
            ("limit", JsonElement.Parse("null")));

        // Act
        var result = await search.InvokeAsync(arguments, Ct);

        // Assert
        ReadResult<int>(result).Should().Be(10);
    }

    [Fact]
    public async Task Given_A_Number_Of_Another_Type_The_Tool_Receives_It_As_Its_Parameter_Type()
    {
        // Arrange
        var search = SchemaToolset.CreateSearchCompanies(Json);

        var arguments = Arguments(
            ("query", Data.Tool.Query),
            ("country", null),
            ("known", Array.Empty<Company>()),
            ("limit", (long)Data.Tool.Limit.Default));

        // Act
        var result = await search.InvokeAsync(arguments, Ct);

        // Assert
        ReadResult<int>(result).Should().Be(Data.Tool.Limit.Default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_An_Object_Argument_As_Json_Text_The_Tool_Receives_The_Object(bool asJson)
    {
        // Arrange
        var locate = SchemaToolset.CreateLocate(Json);
        var text = JsonSerializer.Serialize(Data.Tool.Point, Json.SerializerOptions);
        object point = asJson ? JsonSerializer.SerializeToElement(text) : text;

        // Act
        var result = await locate.InvokeAsync(Arguments(("point", point)), Ct);

        // Assert
        ReadResult<GeoPoint>(result).Should().Be(Data.Tool.Point);
    }

    [Fact]
    public async Task Given_A_Struct_Parameter_Defaulting_To_Default_The_Tool_Runs_Without_It()
    {
        // Arrange
        var locate = SchemaToolset.CreateLocate(Json);

        // Act
        var result = await locate.InvokeAsync(Arguments(), Ct);

        // Assert
        ReadResult<GeoPoint>(result).Should().Be(default(GeoPoint));
    }
}
