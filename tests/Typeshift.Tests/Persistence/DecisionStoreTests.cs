using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// A store of conversations without one of decisions is what these tests register
#pragma warning disable TSH036

namespace Typeshift.Tests.Persistence;

public class DecisionStoreTests : UnitTestBase
{
    private const string Json = """{"standing":[]}""";

    [Fact]
    public async Task Given_A_Store_Of_All_Three_The_Decisions_Of_Its_Id_Type_Are_Saved_In_It()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>();

        await using var provider = services.BuildServiceProvider();
        var stores = provider.GetRequiredService<StoreAccess>();
        var conversation = new StoreId<Guid>(Data.Agent.ConversationId);

        // Act
        var version = await stores.SaveDecisionsAsync(conversation, Json, 0, Ct);
        var stored = await provider.GetRequiredService<IDecisionStore<Guid>>()
            .LoadDecisionsAsync(Data.Agent.ConversationId, Ct);

        // Assert
        stored.Should().Be(new StoredDocument(Json, version));
    }

    [Fact]
    public async Task Given_No_Store_Decisions_Are_Kept_In_Memory_And_The_First_Save_Warns_Once()
    {
        // Arrange
        var logs = new WarningLog();
        var services = new ServiceCollection();
        services.AddTypeshift();
        services.AddSingleton<ILoggerFactory>(logs);

        await using var provider = services.BuildServiceProvider();
        var stores = provider.GetRequiredService<StoreAccess>();
        var conversation = new StoreId<Guid>(Data.Agent.ConversationId);

        // Act
        var first = await stores.SaveDecisionsAsync(conversation, Json, 0, Ct);
        var second = await stores.SaveDecisionsAsync(conversation, Json, first, Ct);
        var stored = await stores.LoadDecisionsAsync(conversation, Ct);

        // Assert
        stored.Should().Be(new StoredDocument(Json, second));
        logs.Warnings.Should().ContainSingle();
        logs.Warnings[0].Should().Contain("decisions");
    }

    [Fact]
    public async Task Given_A_Store_Of_Conversations_Alone_Loading_Decisions_Answers_None()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<CountingLoadStore>();

        await using var provider = services.BuildServiceProvider();
        var stores = provider.GetRequiredService<StoreAccess>();

        // Act
        var stored = await stores.LoadDecisionsAsync(new StoreId<Guid>(Data.Agent.ConversationId), Ct);

        // Assert
        stored.Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Store_Of_Conversations_Alone_Saving_Decisions_Fails_Naming_The_Store_It_Needs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<CountingLoadStore>();

        await using var provider = services.BuildServiceProvider();
        var stores = provider.GetRequiredService<StoreAccess>();

        // Act
        var save = () => stores.SaveDecisionsAsync(new StoreId<Guid>(Data.Agent.ConversationId), Json, 0, Ct);

        // Assert
        await save.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("No IDecisionStore<Guid> is registered*WithStore<T>()*");
    }
}
