using Typeshift.Tests.Persistence.Support;
using FluentAssertions;

namespace Typeshift.Tests.Persistence;

public class StoreCheckTests : UnitTestBase
{
    [Fact]
    public async Task Given_The_Store_In_Memory_Its_Settings_Pass()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore();

        // Act
        var check = () => StoreCheck.CheckSettingsAsync(store, Data.Persistence.SettingsId, Ct);

        // Assert
        await check.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Its_Conversations_Pass()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore();

        // Act
        var check = () => StoreCheck.CheckConversationsAsync(store, Data.Agent.ConversationId, Ct);

        // Assert
        await check.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_A_Store_With_A_Document_Under_The_Id_It_Fails_Asking_For_A_New_Id()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore();
        await store.SaveAsync(Data.Persistence.SettingsId, "typeshift_check", "{}", 0, Ct);

        // Act
        var check = () => StoreCheck.CheckSettingsAsync(store, Data.Persistence.SettingsId, Ct);

        // Assert
        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*give the check a new id*");
    }

    [Fact]
    public async Task Given_A_Store_That_Keys_Documents_By_Id_Alone_It_Fails_Naming_The_Kind()
    {
        // Act
        var check = () => StoreCheck.CheckSettingsAsync(new IdOnlyStore(), Data.Persistence.SettingsId, Ct);

        // Assert
        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*id and kind together*");
    }

    [Fact]
    public async Task Given_A_Store_That_Never_Conflicts_It_Fails_Naming_StoreConflictException()
    {
        // Act
        var check = () => StoreCheck.CheckSettingsAsync(new NoConflictStore(), Data.Persistence.SettingsId, Ct);

        // Assert
        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*StoreConflictException*");
    }

    [Fact]
    public async Task Given_A_Store_That_Writes_Before_It_Conflicts_It_Fails_Naming_The_Changed_Document()
    {
        // Act
        var check = () => StoreCheck.CheckSettingsAsync(new WriteThenConflictStore(), Data.Persistence.SettingsId, Ct);

        // Assert
        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed the document*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_Newest_First_It_Fails_Naming_The_Order()
    {
        // Act
        var check = () => StoreCheck.CheckConversationsAsync(new NewestFirstStore(), Data.Agent.ConversationId, Ct);

        // Assert
        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*oldest first*");
    }

    [Fact]
    public async Task Given_A_Store_That_Starts_At_The_First_Summary_It_Fails_Naming_The_Last_Summary()
    {
        // Act
        var check = () => StoreCheck.CheckConversationsAsync(new FirstSummaryStore(), Data.Agent.ConversationId, Ct);

        // Assert
        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*last summary*");
    }
}
