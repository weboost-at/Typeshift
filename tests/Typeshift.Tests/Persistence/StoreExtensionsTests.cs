using Typeshift.Tests.Persistence.Support;
using FluentAssertions;

namespace Typeshift.Tests.Persistence;

public class StoreExtensionsTests : UnitTestBase
{
    [Fact]
    public async Task Given_The_Store_In_Memory_It_Passes()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore();

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Typed_As_A_Settings_Store_It_Passes()
    {
        // Arrange
        ISettingsStore store = new InMemoryTypeshiftStore();

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Typed_As_A_Conversation_Store_It_Passes()
    {
        // Arrange
        IConversationStore store = new InMemoryTypeshiftStore();

        // Act
        var verify = () => store.VerifyAsync(Data.Agent.ConversationId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_A_Store_With_A_Document_Under_The_Id_It_Fails_Asking_For_A_New_Id()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore();
        await store.SaveAsync(Data.Persistence.SettingsId, "typeshift_check", "{}", 0, Ct);

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*give VerifyAsync a new id*");
    }

    [Fact]
    public async Task Given_A_Store_That_Keys_Documents_By_Id_Alone_It_Fails_Naming_The_Kind()
    {
        // Act
        var verify = () => new IdOnlyStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*id and kind together*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Both_Typed_As_A_Conversation_Store_It_Fails_On_Its_Settings()
    {
        // Arrange
        IConversationStore store = new IdOnlyStore();

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*id and kind together*");
    }

    [Fact]
    public async Task Given_A_Store_That_Never_Conflicts_It_Fails_Naming_StoreConflictException()
    {
        // Act
        var verify = () => new NoConflictStore().VerifyAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*StoreConflictException*");
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
        ISettingsStore store = new NewestFirstStore();

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
}
