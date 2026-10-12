using Agentic.Tests.Toolsets.Support;
using Agentic.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Tools;

public class ToolInvocationTests : HostTestBase
{
    protected override void ConfigureServices(IServiceCollection services) => services.AddScoped<CallScope>();

    [Fact]
    public async Task Given_A_Scripted_Call_The_Tool_Receives_Its_Arguments()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions
        {
            Tools = [.. new ProbeToolset().CreateTools(Json)]
        };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json).Query.Should().Be(Data.Tool.Query);
    }

    [Fact]
    public async Task Given_Two_Scripted_Calls_Each_Runs_In_A_Scope_Of_Its_Own()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)),
                Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query))))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions
        {
            Tools = [.. new ProbeToolset().CreateTools(Json)]
        };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);
        var first = response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json);
        var second = response.Assert.ToolResult<ProbeResult>(Data.Tool.SecondCallId, Json);

        // Assert
        first.Scope.Should().NotBeNull();
        second.Scope.Should().NotBeNull();
        second.Scope.Should().NotBe(first.Scope!.Value);
    }

    [Fact]
    public async Task Given_A_Scripted_Call_The_Tool_Runs_With_Its_Resolved_Settings()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var overrides = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit };

        var options = new ChatOptions
        {
            Tools = [.. new ProbeToolset().CreateTools(Json, overrides)]
        };

        // Act
        var response = await FunctionLoop().GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json)
            .Settings
            .Should()
            .Be(new ProbeSettings { Limit = Data.Tool.Limit.Default });
    }

    [Fact]
    public async Task Given_A_Scripted_Call_The_Result_Reaches_The_Model_As_Json()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var options = new ChatOptions
        {
            Tools = [.. new ProbeToolset().CreateTools(Json)]
        };

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
    public async Task Given_A_Missing_Required_Argument_The_Call_Fails_With_An_ArgumentException()
    {
        // Arrange
        var count = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "count_words");

        // Act
        var invoke = async () => await count.InvokeAsync(Arguments(), Ct);

        // Assert
        await invoke.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Given_An_Absent_Optional_Argument_The_Tool_Takes_Its_Default()
    {
        // Arrange
        var search = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "search_companies");

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
    public async Task Given_A_Struct_Parameter_Defaulting_To_Default_The_Tool_Runs_Without_It()
    {
        // Arrange
        var locate = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "locate");

        // Act
        var result = await locate.InvokeAsync(Arguments(), Ct);

        // Assert
        ReadResult<GeoPoint>(result).Should().Be(default(GeoPoint));
    }
}
