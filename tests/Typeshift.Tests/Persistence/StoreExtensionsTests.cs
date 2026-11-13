using Typeshift.Tests.Persistence.Support;
using FluentAssertions;

namespace Typeshift.Tests.Persistence;

public class StoreExtensionsTests : UnitTestBase
{
    private Guid Id => Data.Persistence.SettingsId;

    private Guid OtherId => Data.Persistence.OtherId;

    private string Tenant => Data.Persistence.TenantId;

    private string OtherTenant => Data.Persistence.OtherTenantId;

    [Fact]
    public async Task Given_The_Store_In_Memory_It_Passes()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();

        // Act
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Typed_As_A_Settings_Store_It_Passes()
    {
        // Arrange
        ISettingsStore<Guid> store = new InMemoryTypeshiftStore<Guid>();

        // Act
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_The_Store_In_Memory_Typed_As_A_Conversation_Store_It_Passes()
    {
        // Arrange
        IConversationStore<Guid> store = new InMemoryTypeshiftStore<Guid>();

        // Act
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

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
        var verify = () => new OneAgentDocumentStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Saving a document of another kind conflicted: key documents by kind*");
    }

    [Fact]
    public async Task Given_A_Store_With_A_Document_Under_The_Id_It_Fails_Asking_For_A_New_Id()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();
        await store.SaveAsync(Id, "typeshift_check", "{}", 0, Ct);

        // Act
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LoadAsync returned a document for*give VerifyAsync a new id*");
    }

    [Fact]
    public async Task Given_A_Store_With_A_Document_Under_The_Other_Id_It_Fails_Asking_For_A_New_Id()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();
        await store.SaveAsync(OtherId, "typeshift_check", "{}", 0, Ct);

        // Act
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"LoadAsync returned a document for {OtherId}: give VerifyAsync a new id*");
    }

    [Fact]
    public async Task Given_A_Store_With_Entries_Under_The_Other_Id_It_Fails_Asking_For_A_New_Id()
    {
        // Arrange
        IConversationStore<Guid> store = new NewestFirstStore();
        await store.AppendConversationAsync(OtherId, Data.Persistence.Entry, Ct);

        // Act
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"LoadConversationAsync returned entries for {OtherId}: give VerifyAsync a new id*");
    }

    [Fact]
    public async Task Given_The_Same_Id_Twice_It_Fails_Asking_For_Two_Different_Ids()
    {
        // Act
        var verify = () => new InMemoryTypeshiftStore<Guid>().VerifyAsync(Id, Id, Ct);

        // Assert
        await verify.Should().ThrowAsync<ArgumentException>().WithMessage("*two different ids*");
    }

    [Fact]
    public async Task Given_A_Store_That_Forgets_The_Settings_Id_It_Fails_Naming_A_Document_Of_Another_Id()
    {
        // Act
        var verify = () => new KindOnlyStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LoadAsync returned a document saved under another id*");
    }

    [Fact]
    public async Task Given_A_Store_That_Updates_Every_Ids_Document_It_Fails_Naming_The_One_Under_Another_Id()
    {
        // Act
        var verify = () => new UpdateEveryIdStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*changed the one under another id*");
    }

    [Fact]
    public async Task Given_A_Store_That_Keys_Documents_By_Id_Alone_It_Fails_Naming_The_Kind()
    {
        // Act
        var verify = () => new IdOnlyStore().VerifyAsync(Id, OtherId, Ct);

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
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*another kind under the same id conflicted: key documents by id and kind together*");
    }

    [Fact]
    public async Task Given_A_Store_That_Never_Conflicts_It_Fails_Naming_StoreConflictException()
    {
        // Act
        var verify = () => new NoConflictStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*A save at version 0 of a document stored already did not throw a StoreConflictException*");
    }

    [Fact]
    public async Task Given_A_Store_That_Saves_Over_A_Document_At_Version_Zero_It_Fails_Naming_Version_Zero()
    {
        // Act
        var verify = () => new UpsertAtZeroStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*A save at version 0 of a document stored already*");
    }

    [Fact]
    public async Task Given_A_Store_That_Ignores_A_Stale_Version_It_Fails_Naming_The_Replaced_Version()
    {
        // Act
        var verify = () => new StaleVersionStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*a version another save replaced*");
    }

    [Fact]
    public async Task Given_A_Store_That_Upserts_A_Missing_Row_It_Fails_Naming_A_Document_That_Is_Not_Stored()
    {
        // Act
        var verify = () => new UpsertMissingStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*a document that is not stored*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_By_Id_Alone_It_Fails_Naming_Another_Kinds_Document()
    {
        // Act
        var verify = () => new LoadByIdAloneStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LoadAsync returned another kind's document*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_Another_Version_Than_It_Saved_It_Fails_Naming_The_Version()
    {
        // Act
        var verify = () => new OtherVersionStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*the last save and version*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_An_Ids_Document_As_An_Agents_Own_It_Fails_Naming_Them_Apart()
    {
        // Act
        var verify = () => new AgentLoadFallbackStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*An agent's own load returned a document saved under an id*");
    }

    [Fact]
    public async Task Given_A_Store_That_Writes_Before_It_Conflicts_It_Fails_Naming_The_Changed_Document()
    {
        // Act
        var verify = () => new WriteThenConflictStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed the document*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_Newest_First_It_Fails_Naming_The_Order()
    {
        // Act
        var verify = () => new NewestFirstStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*oldest first*");
    }

    [Fact]
    public async Task Given_A_Store_That_Orders_By_Time_It_Fails_Naming_The_Order()
    {
        // Act
        var verify = () => new TimeOrderedStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*never by their times*");
    }

    [Fact]
    public async Task Given_A_Store_That_Forgets_The_Conversation_Id_It_Fails_Naming_Another_Conversations_Entries()
    {
        // Act
        var verify = () => new OneLogStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entries appended to another conversation*");
    }

    [Fact]
    public async Task Given_A_Store_That_Starts_At_Any_Conversations_Summary_It_Fails_Naming_The_Conversation()
    {
        // Act
        var verify = () => new SharedSummaryStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at another conversation's summary*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Both_Typed_As_A_Settings_Store_It_Fails_On_Its_Conversations()
    {
        // Arrange
        ISettingsStore<Guid> store = new NewestFirstStore();

        // Act
        var verify = () => store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*oldest first*");
    }

    [Fact]
    public async Task Given_A_Store_That_Starts_At_The_First_Summary_It_Fails_Naming_The_Last_Summary()
    {
        // Act
        var verify = () => new FirstSummaryStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*last summary*");
    }

    [Fact]
    public async Task Given_A_Store_That_Loads_Nothing_Until_A_Summary_It_Fails_Naming_A_Conversation_With_No_Summary()
    {
        // Act
        var verify = () => new NothingUntilSummaryStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*a conversation with no summary*");
    }

    [Fact]
    public async Task Given_A_Store_That_Drops_A_Member_Of_An_Entry_It_Fails_Naming_The_Changed_Entry()
    {
        // Act
        var verify = () => new DroppedMemberStore().VerifyAsync(Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*An entry came back changed*");
    }

    [Fact]
    public async Task Given_A_Store_Checked_Under_One_Id_It_Passes_Again_Under_Another()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();
        await store.VerifyAsync(Id, OtherId, Ct);

        // Act
        var verify = () => store.VerifyAsync(Data.Persistence.LaterId, Data.Persistence.LaterOtherId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_A_Store_Of_Agent_Settings_Too_Their_Check_Writes_Kinds_Named_After_The_Id()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();

        // Act
        await store.VerifyAsync(Id, OtherId, Ct);

        // Assert
        (await store.LoadAsync($"typeshift_check_{Id}", Ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task Given_A_Store_Of_Two_Id_Types_Verify_With_Both_Ids_Passes()
    {
        // Arrange
        var store = new TwoIdTypesStore();

        // Act
        var verify = () => store.VerifyAsync(Tenant, OtherTenant, Id, OtherId, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_A_Store_Of_Two_Id_Types_With_Conversations_Out_Of_Order_Verify_With_Both_Ids_Throws()
    {
        // Arrange
        var store = new TwoIdTypesStore(newestFirst: true);

        // Act
        var verify = () => store.VerifyAsync(Tenant, OtherTenant, Id, OtherId, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*oldest first*");
    }

    [Fact]
    public async Task Given_Conversation_Ids_Of_A_Type_The_Store_Does_Not_Keep_Verify_Fails_Naming_Its_Store()
    {
        // Arrange
        var store = new TwoIdTypesStore();

        // Act
        var verify = () => store.VerifyAsync(Tenant, OtherTenant, Data.Persistence.NumberId, 0, Ct);

        // Assert
        await verify.Should().ThrowAsync<InvalidOperationException>().WithMessage("*is no IConversationStore<int>*");
    }

    [Fact]
    public async Task Given_The_Same_Conversation_Id_Twice_Verify_With_Both_Id_Types_Asks_For_Two_Different_Ids()
    {
        // Arrange
        var store = new TwoIdTypesStore();

        // Act
        var verify = () => store.VerifyAsync(Tenant, OtherTenant, Id, Id, Ct);

        // Assert
        await verify.Should().ThrowAsync<ArgumentException>().WithMessage("*two different ids*");
    }
}
