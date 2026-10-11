using System.Text.Json;
using System.Text.Json.Nodes;
using Agentic.StoredAgents;
using Agentic.Tests.Agents.Support;
using Agentic.Tests.Toolsets.Support;
using Agentic.Tools;
using FluentAssertions;

namespace Agentic.Tests.StoredAgents;

public class StoredAgentTests : AgentTestBase
{
    [Fact]
    public void Given_A_Code_Agent_Its_Exported_Record_Round_Trips_Through_Json()
    {
        // Arrange
        var json = JsonSerializer.Serialize(Catalog.Export(new ProbeAgent()), Json.GetTypeInfo<StoredAgent>());

        // Act
        var restored = JsonSerializer.Deserialize(json, Json.GetTypeInfo<StoredAgent>());
        var reserialized = JsonSerializer.Serialize(restored!, Json.GetTypeInfo<StoredAgent>());

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(reserialized), JsonNode.Parse(json)).Should().BeTrue();
    }

    [Fact]
    public async Task Given_An_Exported_Code_Agent_Restored_From_Json_It_Offers_The_Same_Tools()
    {
        // Arrange
        var restored = RoundTrip(Catalog.Export(new ProbeAgent()));

        // Act
        var options = await Catalog.CreateRuntime(restored).ResolveChatOptionsAsync(ct: Ct);

        // Assert
        options.Tools!.Select(tool => tool.Name).Should().Equal(ProbeAgent.ToolNames.Select(tool => tool.Value));
    }

    [Fact]
    public async Task Given_An_Exported_Code_Agent_Restored_From_Json_Its_Tools_Run_With_The_Same_Settings()
    {
        // Arrange
        var restored = RoundTrip(Catalog.Export(new ProbeAgent()));
        var options = await Catalog.CreateRuntime(restored).ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = 5, Mode = "thorough" });
    }

    [Fact]
    public void Given_A_Stored_Agent_Naming_An_Unknown_Tool_Creating_Its_Runtime_Throws_Which_Tool()
    {
        // Arrange
        var agent = StoredAgent.Create(Agent.DisplayName, Time) with
        {
            Tools = [new ToolName(Agent.UnknownTool)]
        };

        // Act
        var create = () => Catalog.CreateRuntime(agent);

        // Assert
        create.Should().Throw<InvalidOperationException>().WithMessage($"*'{Agent.UnknownTool}'*");
    }

    [Fact]
    public void Given_A_New_Stored_Agent_Its_Generated_Name_Can_Never_Be_A_Code_Agents_Name()
    {
        // Act
        var agent = StoredAgent.Create(Agent.DisplayName, Time);

        // Assert
        agent.UniqueName.Should().NotMatchRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");
    }

    [Fact]
    public async Task Given_A_Stored_Agent_Named_Like_A_Code_Agent_Saving_It_Throws()
    {
        // Arrange
        var agent = StoredAgent.Create(Agent.DisplayName, Time) with
        {
            UniqueName = ProbeAgent.Name
        };

        // Act
        var save = () => Catalog.SaveAsync(agent, Ct);

        // Assert
        await save.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Given_A_Saved_Stored_Agent_The_Catalog_Lists_It_After_The_Code_Agents()
    {
        // Arrange
        var agent = StoredAgent.Create(Agent.DisplayName, Time);
        await Catalog.SaveAsync(agent, Ct);

        // Act
        var listed = await Catalog.ListAsync(Ct);

        // Assert
        listed.Select(listing => listing.UniqueName).Should().EndWith(agent.UniqueName);
    }

    [Fact]
    public async Task Given_A_Saved_Stored_Agent_Its_Runtime_Resolves_Its_Options()
    {
        // Arrange
        var agent = StoredAgent.Create(Agent.DisplayName, Time) with
        {
            Options = new StoredAgentOptions { Instructions = Agent.Instructions },
            Tools = [ProbeToolset.Tools.Probe.Name]
        };

        await Catalog.SaveAsync(agent, Ct);
        var runtime = await Catalog.GetRuntimeAsync(agent.UniqueName, Ct);

        // Act
        var options = await runtime.ResolveChatOptionsAsync(ct: Ct);

        // Assert
        options.Instructions.Should().Be(Agent.Instructions);
    }

    private StoredAgent RoundTrip(StoredAgent agent)
    {
        var json = JsonSerializer.Serialize(agent, Json.GetTypeInfo<StoredAgent>());
        return JsonSerializer.Deserialize(json, Json.GetTypeInfo<StoredAgent>())!;
    }
}
