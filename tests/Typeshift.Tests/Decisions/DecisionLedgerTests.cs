using Typeshift.Tests.Decisions.Support;
using FluentAssertions;

namespace Typeshift.Tests.Decisions;

public class DecisionLedgerTests : LedgerTestBase
{
    [Fact]
    public async Task Given_No_Batch_Opening_One_Stores_It_With_Its_Decisions_Open()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId), Sibling(Data.Tool.SecondCallId));

        // Act
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var document = await DocumentAsync();

        // Assert
        document.Batch!.State.Should().Be(BatchState.Open);
        document.Open.Select(decision => decision.Id).Should().Equal(batch.Ids);
    }

    [Fact]
    public async Task Given_A_Batch_Another_Run_Opened_Opening_One_Throws_And_Leaves_It()
    {
        // Arrange
        var first = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, first, Ct);

        // Act
        var open = () => Ledger.OpenAsync(Conversation, Batch(Approval(Data.Tool.SecondCallId)), Ct);

        // Assert
        await open.Should().ThrowAsync<InvalidOperationException>().WithMessage("*another run asked*");
        (await DocumentAsync()).Batch!.Ids.Should().Equal(first.Ids);
    }

    [Fact]
    public async Task Given_An_Answer_To_One_Of_Two_Decisions_The_Batch_Stays_Open_And_Lists_The_Other()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId), Approval(Data.Tool.SecondCallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var document = await Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(batch.Ids.First())], Ct);

        // Assert
        document.ToDecisions().CanResume.Should().BeFalse();
        document.Open.Select(decision => decision.Id).Should().Equal(batch.Ids.Last());
    }

    [Fact]
    public async Task Given_The_Last_Answer_The_Batch_Is_Answered_And_Can_Resume()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId), Approval(Data.Tool.SecondCallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);
        await Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(batch.Ids.First())], Ct);

        // Act
        var document = await Ledger.RecordAsync(Conversation, [DecisionAnswer.Reject(batch.Ids.Last())], Ct);

        // Assert
        document.Batch!.State.Should().Be(BatchState.Answered);
        document.ToDecisions().CanResume.Should().BeTrue();
        document.Open.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_The_Same_Answer_Twice_The_Second_Changes_And_Saves_Nothing()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId), Approval(Data.Tool.SecondCallId));
        var answer = DecisionAnswer.Approve(batch.Ids.First());
        await Ledger.OpenAsync(Conversation, batch, Ct);
        await Ledger.RecordAsync(Conversation, [answer], Ct);
        var saves = Store.Saves;

        // Act
        var document = await Ledger.RecordAsync(Conversation, [answer], Ct);

        // Assert
        Store.Saves.Should().Be(saves);
        document.Batch!.Asks.First().Answer.Should().Be(answer);
    }

    [Fact]
    public async Task Given_A_Different_Second_Answer_It_Throws_Naming_The_Decision_Answered()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId), Approval(Data.Tool.SecondCallId));
        var decision = batch.Ids.First();
        await Ledger.OpenAsync(Conversation, batch, Ct);
        await Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(decision)], Ct);

        // Act
        var second = () => Ledger.RecordAsync(Conversation, [DecisionAnswer.Reject(decision, Data.Agent.Reason)], Ct);

        // Assert
        await second.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Answered);
        (await DocumentAsync()).Batch!.Asks.First().Answer!.Approved.Should().BeTrue();
    }

    [Fact]
    public async Task Given_An_Answer_To_A_Decision_The_Conversation_Never_Asked_It_Throws_Naming_It_Unknown()
    {
        // Arrange
        await Ledger.OpenAsync(Conversation, Batch(Approval(Data.Tool.CallId)), Ct);

        // Act
        var answer = () => Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(Data.Tool.SecondCallId)], Ct);

        // Assert
        await answer.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[Data.Tool.SecondCallId] == DecisionClosedReason.Unknown);
    }

    [Fact]
    public async Task Given_One_Closed_Answer_Among_Open_Ones_Nothing_Of_The_Call_Is_Recorded()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var answers = () => Ledger.RecordAsync(
            Conversation,
            [DecisionAnswer.Approve(batch.Ids.Single()), DecisionAnswer.Approve(Data.Tool.SecondCallId)],
            Ct);

        // Assert
        await answers.Should().ThrowAsync<DecisionClosedException>();
        (await DocumentAsync()).Open.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_Two_Answers_To_One_Decision_In_One_Call_It_Throws_And_Records_Neither()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        var decision = batch.Ids.Single();
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var answers = () => Ledger.RecordAsync(
            Conversation,
            [DecisionAnswer.Approve(decision), DecisionAnswer.Reject(decision)],
            Ct);

        // Assert
        await answers.Should().ThrowAsync<ArgumentException>().WithMessage("*answered twice*");
        (await DocumentAsync()).Open.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_No_Answers_Recording_Throws()
    {
        // Arrange
        await Ledger.OpenAsync(Conversation, Batch(Approval(Data.Tool.CallId)), Ct);

        // Act
        var answers = () => Ledger.RecordAsync(Conversation, [], Ct);

        // Assert
        await answers.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Given_A_Plan_Approval_For_A_Tool_Call_It_Throws_And_Records_Nothing()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var answer = () => Ledger.RecordAsync(
            Conversation,
            [DecisionAnswer.ApprovePlan(batch.Ids.Single(), AgentMode.Auto)],
            Ct);

        // Assert
        await answer.Should().ThrowAsync<ArgumentException>().WithMessage("*is a tool call*");
        (await DocumentAsync()).Open.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Plan_Approved_For_The_Conversation_It_Throws()
    {
        // Arrange
        var batch = Batch(Plan(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var answer = () => Ledger.RecordAsync(
            Conversation,
            [DecisionAnswer.ApproveForConversation(batch.Ids.Single())],
            Ct);

        // Assert
        await answer.Should().ThrowAsync<ArgumentException>().WithMessage("*is a plan*");
    }

    [Fact]
    public async Task Given_A_Posted_Answer_That_Approves_A_Plan_Into_Plan_Mode_It_Throws()
    {
        // Arrange
        var batch = Batch(Plan(Data.Tool.CallId));
        var posted = new DecisionAnswer(batch.Ids.Single()) { Approved = true, Mode = AgentMode.Plan };
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var answer = () => Ledger.RecordAsync(Conversation, [posted], Ct);

        // Assert
        await answer.Should().ThrowAsync<ArgumentException>().WithMessage("*default or the auto mode*");
    }

    [Fact]
    public async Task Given_A_Posted_Answer_That_Approves_A_Plan_Without_A_Mode_It_Continues_In_The_Default_Mode()
    {
        // Arrange
        var batch = Batch(Plan(Data.Tool.CallId));
        var posted = new DecisionAnswer(batch.Ids.Single()) { Approved = true };
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var document = await Ledger.RecordAsync(Conversation, [posted], Ct);

        // Assert
        document.Batch!.Asks.Single().Answer!.Mode.Should().Be(AgentMode.Default);
    }

    [Fact]
    public async Task Given_A_Posted_Rejection_Marked_For_The_Conversation_It_Grants_Nothing()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        var posted = new DecisionAnswer(batch.Ids.Single()) { ForConversation = true, Reason = Data.Agent.Reason };
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var answered = await Ledger.RecordAsync(Conversation, [posted], Ct);

        // Act
        await Ledger.StartAsync(Conversation, answered.Batch!, Ct);

        // Assert
        (await DocumentAsync()).Standing.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Answered_Batch_Starting_It_Marks_It_Started()
    {
        // Arrange
        var answered = await AnsweredAsync(Batch(Approval(Data.Tool.CallId), Sibling(Data.Tool.SecondCallId)));

        // Act
        var started = await Ledger.StartAsync(Conversation, answered, Ct);

        // Assert
        started.Batch!.State.Should().Be(BatchState.Started);
        (await DocumentAsync()).Batch!.State.Should().Be(BatchState.Started);
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_Nothing_Is_Granted_Before_The_Batch_Starts()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        var answered = await Ledger.RecordAsync(
            Conversation,
            [DecisionAnswer.ApproveForConversation(batch.Ids.Single())],
            Ct);

        // Assert
        answered.Standing.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_Starting_The_Batch_Grants_It()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var answered = await Ledger.RecordAsync(
            Conversation,
            [DecisionAnswer.ApproveForConversation(batch.Ids.Single())],
            Ct);

        // Act
        await Ledger.StartAsync(Conversation, answered.Batch!, Ct);

        // Assert
        (await DocumentAsync()).Standing.Should().Equal(new StandingApproval("close_ticket", null, Time.GetUtcNow()));
    }

    [Fact]
    public async Task Given_Two_Calls_Of_One_Tool_Approved_For_The_Conversation_The_Tool_Stands_Once()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId), Approval(Data.Tool.SecondCallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var answered = await Ledger.RecordAsync(
            Conversation,
            [.. batch.Ids.Select(DecisionAnswer.ApproveForConversation)],
            Ct);

        // Act
        await Ledger.StartAsync(Conversation, answered.Batch!, Ct);

        // Assert
        (await DocumentAsync()).Standing.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Batch_Another_Process_Started_Starting_It_Throws_Naming_Its_Decisions_Answered()
    {
        // Arrange
        var answered = await AnsweredAsync(Batch(Approval(Data.Tool.CallId)));
        await OtherProcess.StartAsync(Conversation, answered, Ct);

        // Act
        var start = () => Ledger.StartAsync(Conversation, answered, Ct);

        // Assert
        await start.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[answered.Ids.Single()] == DecisionClosedReason.Answered);
    }

    [Fact]
    public async Task Given_Two_Processes_Starting_One_Batch_At_Once_One_Wins_And_The_Other_Throws()
    {
        // Arrange
        var answered = await AnsweredAsync(Batch(Approval(Data.Tool.CallId)));
        Store.BeforeNextSave(() => OtherProcess.StartAsync(Conversation, answered, Ct));

        // Act
        var start = () => Ledger.StartAsync(Conversation, answered, Ct);

        // Assert
        await start.Should().ThrowAsync<DecisionClosedException>();
        (await DocumentAsync()).Batch!.State.Should().Be(BatchState.Started);
    }

    [Fact]
    public async Task Given_An_Answer_After_The_Batch_Started_It_Throws_Naming_The_Decision_Answered()
    {
        // Arrange
        var answered = await AnsweredAsync(Batch(Approval(Data.Tool.CallId)));
        var decision = answered.Ids.Single();
        await Ledger.StartAsync(Conversation, answered, Ct);

        // Act
        var late = () => Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(decision)], Ct);

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Answered);
    }

    [Fact]
    public async Task Given_An_Open_Batch_Expiring_It_Marks_It_Expired()
    {
        // Arrange
        await Ledger.OpenAsync(Conversation, Batch(Approval(Data.Tool.CallId)), Ct);

        // Act
        var expired = await Ledger.ExpireAsync(Conversation, Ct);

        // Assert
        expired!.State.Should().Be(BatchState.Expired);
        (await DocumentAsync()).Open.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Answered_Batch_Expiring_Leaves_It_Answered()
    {
        // Arrange
        await AnsweredAsync(Batch(Approval(Data.Tool.CallId)));

        // Act
        var batch = await Ledger.ExpireAsync(Conversation, Ct);

        // Assert
        batch!.State.Should().Be(BatchState.Answered);
    }

    [Fact]
    public async Task Given_The_Last_Answer_Arriving_While_A_Run_Expires_The_Batch_The_Answer_Stands()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var answers = batch.Ids.Select(DecisionAnswer.Approve).ToList();
        Store.BeforeNextSave(() => OtherProcess.RecordAsync(Conversation, answers, Ct));

        // Act
        var settled = await Ledger.ExpireAsync(Conversation, Ct);

        // Assert
        settled!.State.Should().Be(BatchState.Answered);
    }

    [Fact]
    public async Task Given_An_Expired_Batch_An_Answer_Throws_Naming_The_Decision_Expired()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);
        await Ledger.ExpireAsync(Conversation, Ct);

        // Act
        var late = () => Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(batch.Ids.Single())], Ct);

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[batch.Ids.Single()] == DecisionClosedReason.Expired);
    }

    [Fact]
    public async Task Given_A_Started_Batch_Closing_It_Removes_It_And_Remembers_Its_Decisions_As_Answered()
    {
        // Arrange
        var answered = await AnsweredAsync(Batch(Approval(Data.Tool.CallId)));
        var started = (await Ledger.StartAsync(Conversation, answered, Ct)).Batch!;

        // Act
        await Ledger.CloseAsync(Conversation, started, Ct);
        var document = await DocumentAsync();

        // Assert
        document.Batch.Should().BeNull();
        document.ClosedReason(started.Ids.Single()).Should().Be(DecisionClosedReason.Answered);
    }

    [Fact]
    public async Task Given_An_Expired_Batch_Closed_A_Late_Answer_Throws_Naming_The_Decision_Expired()
    {
        // Arrange
        await Ledger.OpenAsync(Conversation, Batch(Approval(Data.Tool.CallId)), Ct);
        var expired = await Ledger.ExpireAsync(Conversation, Ct);
        await Ledger.CloseAsync(Conversation, expired!, Ct);

        // Act
        var late = () => Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(expired!.Ids.Single())], Ct);

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[expired!.Ids.Single()] == DecisionClosedReason.Expired);
    }

    [Fact]
    public async Task Given_An_Open_Batch_Closing_It_Changes_Nothing()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);

        // Act
        await Ledger.CloseAsync(Conversation, batch, Ct);

        // Assert
        (await DocumentAsync()).Open.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_Two_Batches_Closed_Only_The_Decisions_Of_The_Last_Are_Remembered()
    {
        // Arrange
        var first = await StartedAsync(Batch(Approval(Data.Tool.CallId)));
        await Ledger.CloseAsync(Conversation, first, Ct);
        var second = await StartedAsync(Batch(Approval(Data.Tool.SecondCallId)));

        // Act
        await Ledger.CloseAsync(Conversation, second, Ct);
        var document = await DocumentAsync();

        // Assert
        document.ClosedReason(first.Ids.Single()).Should().Be(DecisionClosedReason.Unknown);
        document.ClosedReason(second.Ids.Single()).Should().Be(DecisionClosedReason.Answered);
    }

    [Fact]
    public async Task Given_Standing_Approvals_Of_One_Tool_Name_Withdrawing_One_Keeps_The_Other_Servers()
    {
        // Arrange
        await GrantedAsync(Approval(Data.Tool.CallId), Approval(Data.Tool.SecondCallId) with { Server = "deploy" });

        // Act
        var document = await Ledger.RevokeAsync(Conversation, "close_ticket", null, Ct);

        // Assert
        document.Standing.Select(approval => approval.Server).Should().Equal("deploy");
    }

    [Fact]
    public async Task Given_A_Standing_Approval_The_Conversation_Does_Not_Hold_Withdrawing_It_Saves_Nothing()
    {
        // Arrange
        await GrantedAsync(Approval(Data.Tool.CallId));
        var saves = Store.Saves;

        // Act
        await Ledger.RevokeAsync(Conversation, "close_ticket", "deploy", Ct);

        // Assert
        Store.Saves.Should().Be(saves);
    }

    [Fact]
    public async Task Given_A_Mode_Setting_It_Stores_It_With_The_Decisions()
    {
        // Act
        await Ledger.SetModeAsync(Conversation, AgentMode.Auto, Ct);

        // Assert
        (await DocumentAsync()).Mode.Should().Be(AgentMode.Auto);
    }

    [Fact]
    public async Task Given_The_Mode_The_Document_Names_Setting_It_Again_Saves_Nothing()
    {
        // Arrange
        await Ledger.SetModeAsync(Conversation, AgentMode.Plan, Ct);
        var saves = Store.Saves;

        // Act
        await Ledger.SetModeAsync(Conversation, AgentMode.Plan, Ct);

        // Assert
        Store.Saves.Should().Be(saves);
    }

    [Fact]
    public async Task Given_A_Mode_Set_While_Decisions_Wait_They_Stay_Open()
    {
        // Arrange
        await Ledger.OpenAsync(Conversation, Batch(Approval(Data.Tool.CallId)), Ct);

        // Act
        var document = await Ledger.SetModeAsync(Conversation, AgentMode.Auto, Ct);

        // Assert
        document.Open.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Plan_Approved_Into_Auto_Mode_Starting_Its_Batch_Switches_The_Mode_In_The_Same_Save()
    {
        // Arrange
        var batch = Batch(Plan(Data.Tool.CallId));
        await Ledger.SetModeAsync(Conversation, AgentMode.Plan, Ct);
        await Ledger.OpenAsync(Conversation, batch, Ct);

        var answered = await Ledger.RecordAsync(
            Conversation,
            [DecisionAnswer.ApprovePlan(batch.Ids.Single(), AgentMode.Auto)],
            Ct);

        var saves = Store.Saves;

        // Act
        var started = await Ledger.StartAsync(Conversation, answered.Batch!, Ct);

        // Assert
        started.Mode.Should().Be(AgentMode.Auto);
        (Store.Saves - saves).Should().Be(1);
    }

    [Fact]
    public async Task Given_A_Rejected_Plan_Starting_Its_Batch_Leaves_The_Mode()
    {
        // Arrange
        var batch = Batch(Plan(Data.Tool.CallId));
        await Ledger.SetModeAsync(Conversation, AgentMode.Plan, Ct);
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var answered = await Ledger.RecordAsync(Conversation, [DecisionAnswer.Reject(batch.Ids.Single())], Ct);

        // Act
        var started = await Ledger.StartAsync(Conversation, answered.Batch!, Ct);

        // Assert
        started.Mode.Should().Be(AgentMode.Plan);
    }

    [Fact]
    public async Task Given_A_Mode_An_Earlier_Version_Stored_On_The_Entries_The_Ledgers_First_Save_Copies_It()
    {
        // Arrange
        var ledger = LedgerAfter(AgentMode.Plan);

        // Act
        await ledger.OpenAsync(Conversation, Batch(Approval(Data.Tool.CallId)), Ct);

        // Assert
        (await DocumentAsync()).Mode.Should().Be(AgentMode.Plan);
        ledger.LegacyMode.Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Mode_An_Earlier_Version_Stored_On_The_Entries_A_Document_That_Names_One_Keeps_Its_Own()
    {
        // Arrange
        await Ledger.SetModeAsync(Conversation, AgentMode.Auto, Ct);

        // Act
        await LedgerAfter(AgentMode.Plan).OpenAsync(Conversation, Batch(Approval(Data.Tool.CallId)), Ct);

        // Assert
        (await DocumentAsync()).Mode.Should().Be(AgentMode.Auto);
    }

    [Fact]
    public async Task Given_A_Mode_An_Earlier_Version_Stored_On_The_Entries_A_Change_That_Saves_Nothing_Keeps_It()
    {
        // Arrange
        var ledger = LedgerAfter(AgentMode.Plan);

        // Act
        await ledger.ExpireAsync(Conversation, Ct);

        // Assert
        ledger.LegacyMode.Should().Be(AgentMode.Plan);
        Store.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Save_That_Lost_To_Another_Answer_Recording_Decides_Again_On_What_The_Winner_Stored()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId), Approval(Data.Tool.SecondCallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);

        Store.BeforeNextSave(() => OtherProcess.RecordAsync(
            Conversation,
            [DecisionAnswer.Reject(batch.Ids.Last(), Data.Agent.Reason)],
            Ct));

        // Act
        var document = await Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(batch.Ids.First())], Ct);

        // Assert
        document.Batch!.State.Should().Be(BatchState.Answered);
        document.Batch.Asks.Last().Answer!.Reason.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_Ten_Lost_Saves_In_A_Row_Recording_Throws_A_StoreConflictException()
    {
        // Arrange
        var batch = Batch(Approval(Data.Tool.CallId));
        await Ledger.OpenAsync(Conversation, batch, Ct);
        var saves = Store.Saves;
        Store.LosesEverySave = true;

        // Act
        var answer = () => Ledger.RecordAsync(Conversation, [DecisionAnswer.Approve(batch.Ids.Single())], Ct);

        // Assert
        await answer.Should().ThrowAsync<StoreConflictException>();
        (Store.Saves - saves).Should().Be(10);
    }
}
