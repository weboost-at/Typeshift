using Typeshift.Tests.Persistence.Support;
using FluentAssertions;

namespace Typeshift.Tests.Persistence;

public class StoreExtensionsTests : UnitTestBase
{
    [Fact]
    public async Task Given_The_Store_In_Memory_It_Passes()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Typed_As_A_Settings_Store_It_Passes()
    {
        // Arrange
        ISettingsStore<Guid> store = new InMemoryTypeshiftStore<Guid>();

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Typed_As_A_Conversation_Store_It_Passes()
    {
        // Arrange
        IConversationStore<Guid> store = new InMemoryTypeshiftStore<Guid>();

        // Act
        var verify = () => store.VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Typed_As_A_Store_Of_Agent_Settings_It_Passes()
    {
        // Arrange
        IAgentSettingsStore store = new InMemoryTypeshiftStore<Guid>();

        // Act
        var verify = () => store.VerifyAgentSettingsAsync(Data.Persistence.Kind, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_A_Store_With_A_Document_Of_The_Kind_The_Agent_Settings_Check_Asks_For_A_New_Kind()
    {
        // Arrange
        IAgentSettingsStore store = new InMemoryTypeshiftStore<Guid>();
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);

        // Act
        var verify = () => store.VerifyAgentSettingsAsync(Data.Persistence.Kind, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*a kind nothing is stored under*");
    }

    [Fact]
    public async Task Given_A_Store_That_Keeps_One_Agent_Document_For_Every_Kind_Its_Check_Fails_Naming_The_Kind()
    {
        // Act
        var verify = () => new OneAgentDocumentStore().VerifyAgentSettingsAsync(Data.Persistence.Kind, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Saving a document of another kind conflicted: key documents by kind*");
    }

    [Fact]
    public async Task Given_A_Store_Of_All_Three_That_Breaks_Agent_Settings_VerifyAsync_Fails_On_Them()
    {
        // Act
        var verify = () => new OneAgentDocumentStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Saving a document of another kind conflicted: key documents by kind*");
    }

    [Fact]
    public async Task Given_A_Store_With_A_Document_Under_The_Id_It_Fails_Asking_For_A_New_Id()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();
        await store.SaveAsync(Data.Persistence.SettingsId, "typeshift_check", "{}", 0, Ct);

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LoadAsync returned a document for*give VerifyAsync a new id*");
    }

    [Fact]
    public async Task Given_A_Store_That_Keys_Documents_By_Id_Alone_It_Fails_Naming_The_Kind()
    {
        // Act
        var verify = () => new IdOnlyStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*another kind under the same id conflicted: key documents by id and kind together*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Both_Typed_As_A_Conversation_Store_It_Fails_On_Its_Settings()
    {
        // Arrange
        IConversationStore<Guid> store = new IdOnlyStore();

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*another kind under the same id conflicted: key documents by id and kind together*");
    }

    [Fact]
    public async Task Given_A_Store_That_Never_Conflicts_It_Fails_Naming_StoreConflictException()
    {
        // Act
        var verify = () => new NoConflictStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*A save at version 0 of a document stored already did not throw a StoreConflictException*");
    }

    [Fact]
    public async Task Given_A_Store_That_Saves_Over_A_Document_At_Version_Zero_It_Fails_Naming_Version_Zero()
    {
        // Act
        var verify = () => new UpsertAtZeroStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*A save at version 0 of a document stored already*");
    }

    [Fact]
    public async Task Given_A_Store_That_Ignores_A_Stale_Version_It_Fails_Naming_The_Replaced_Version()
    {
        // Act
        var verify = () => new StaleVersionStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*a version another save replaced*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_By_Id_Alone_It_Fails_Naming_Another_Kinds_Document()
    {
        // Act
        var verify = () => new LoadByIdAloneStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LoadAsync returned another kind's document*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_Another_Version_Than_It_Saved_It_Fails_Naming_The_Version()
    {
        // Act
        var verify = () => new OtherVersionStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*the last save and version*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_An_Ids_Document_As_An_Agents_Own_It_Fails_Naming_Them_Apart()
    {
        // Act
        var verify = () => new AgentLoadFallbackStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*An agent's own load returned a document saved under an id*");
    }

    [Fact]
    public async Task Given_A_Store_That_Writes_Before_It_Conflicts_It_Fails_Naming_The_Changed_Document()
    {
        // Act
        var verify = () => new WriteThenConflictStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed the document*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_Newest_First_It_Fails_Naming_The_Order()
    {
        // Act
        var verify = () => new NewestFirstStore().VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*oldest first*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Both_Typed_As_A_Settings_Store_It_Fails_On_Its_Conversations()
    {
        // Arrange
        ISettingsStore<Guid> store = new NewestFirstStore();

        // Act
        var verify = () => store.VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*oldest first*");
    }

    [Fact]
    public async Task Given_A_Store_That_Starts_At_The_First_Summary_It_Fails_Naming_The_Last_Summary()
    {
        // Act
        var verify = () => new FirstSummaryStore().VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*last summary*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_Nothing_Until_A_Summary_It_Fails_Naming_A_Conversation_With_No_Summary()
    {
        // Act
        var verify = () => new NothingUntilSummaryStore().VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*a conversation with no summary*");
    }

    [Fact]
    public async Task Given_A_Store_That_Drops_A_Member_Of_An_Entry_It_Fails_Naming_The_Changed_Entry()
    {
        // Act
        var verify = () => new DroppedMemberStore().VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*An entry came back changed*");
    }

    [Fact]
    public async Task Given_A_Store_Checked_Under_One_Id_It_Passes_Again_Under_Another()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();
        await store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Act
        var verify = () => store.VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }
}
