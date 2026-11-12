using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;

namespace Typeshift.Tests.Agents;

public class UnionResultTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Union_Result_Type_The_Response_Schema_Holds_It_Under_Data()
    {
        // Arrange
        var cat = new Cat(Data.Tool.PetName, Data.Tool.Lives);
        ChatClient.Reply(Text(JsonSerializer.Serialize(new { data = cat }, Json.SerializerOptions)));

        // Act
        await Host<ProbeAgent>().RunAsync<Pet>(Data.Tool.Question, ct: Ct);

        // Assert
        ResponseSchema().Assert.ParameterCaseTypes("data", "object", "object");
    }

    [Fact]
    public async Task Given_A_Union_Result_Type_The_Run_Reads_The_Case_Under_Data()
    {
        // Arrange
        var cat = new Cat(Data.Tool.PetName, Data.Tool.Lives);
        ChatClient.Reply(Text(JsonSerializer.Serialize(new { data = cat }, Json.SerializerOptions)));

        // Act
        var response = await Host<ProbeAgent>().RunAsync<Pet>(Data.Tool.Question, ct: Ct);

        // Assert
        response.Result.Value.Should().Be(cat);
    }

    [Fact]
    public async Task Given_A_Result_Type_With_A_Union_Property_Each_Case_Of_Its_Schema_Names_Its_Type()
    {
        // Arrange
        var adoption = new Adoption(Data.Tool.Query, new Dog(Data.Tool.PetName, Data.Tool.Breed));
        ChatClient.Reply(Text(JsonSerializer.Serialize(adoption, Json.GetTypeInfo<Adoption>())));

        // Act
        await Host<ProbeAgent>().RunAsync<Adoption>(Data.Tool.Question, ct: Ct);

        // Assert
        ResponseSchema().Assert.ParameterCaseTypes("pet", "object", "object");
    }
}
