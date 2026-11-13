namespace Typeshift;

// D80: the only code that loads and saves a conversation's decisions document. Every change is a compare-and-set on
// the version it loaded: after a lost save it loads again and checks its precondition again, so of two writers one
// wins and the other sees what the winner wrote
internal sealed class DecisionLedger(StoreAccess stores, TimeProvider time)
{
    // A store that conflicts every time would keep a change going forever
    private const int MaxAttempts = 10;

    public async Task<DecisionDocument> LoadAsync(StoreId conversation, CancellationToken ct) =>
        (await ReadAsync(conversation, ct)).Document;

    // T1: a run asks. A conversation waits for one batch at a time, which every run settles before its first model
    // call, so a batch found here was opened by a run of another process
    public Task OpenAsync(StoreId conversation, DecisionBatch batch, CancellationToken ct) =>
        ChangeAsync(
            conversation,
            document => document.Batch is null
                ? document with { Batch = batch }
                : throw new InvalidOperationException(
                    $"Conversation {conversation} waits for decisions another run asked meanwhile: one run of a "
                    + "conversation at a time, across processes too."),
            ct);

    // T2: answers are recorded one by one or together; the batch is answered with the last. The same answer again
    // changes nothing, and one answer to a decision that is not open records none of the call
    public Task<DecisionDocument> RecordAsync(
        StoreId conversation,
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct)
    {
        if (answers.Count == 0)
            throw new ArgumentException("No answer to record: pass one answer for each decision.", nameof(answers));

        if (answers.GroupBy(answer => answer.DecisionId).FirstOrDefault(group => group.Count() > 1) is { } twice)
            throw new ArgumentException($"The decision {twice.Key} is answered twice in one call.", nameof(answers));

        return ChangeAsync(conversation, document => Record(conversation, document, answers), ct);
    }

    // T3, the commit point: from here on every call the batch would run counts as started, so no later run starts
    // one again. Of two runs that continue one answered batch, one wins and the other learns its answers are closed
    public async Task<DecisionBatch> StartAsync(StoreId conversation, DecisionBatch batch, CancellationToken ct)
    {
        var started = await ChangeAsync(
            conversation,
            document => document.Batch is { State: BatchState.Answered } answered && answered.IsSame(batch)
                ? document with
                {
                    Batch = answered with { State = BatchState.Started },
                    Standing = WithGranted(document.Standing, answered)
                }
                : throw DecisionClosedException.For(
                    conversation,
                    batch.Ids.ToDictionary(id => id, id => ClosedReason(document, id))),
            ct);

        return started.Batch!;
    }

    // T5: new input passes an open batch by. The batch as it stands afterwards: expired, or what answers that came
    // first made of it, which the caller then carries out
    public async Task<DecisionBatch?> ExpireAsync(StoreId conversation, CancellationToken ct)
    {
        var document = await ChangeAsync(
            conversation,
            document => document.Batch is { State: BatchState.Open } open
                ? document with { Batch = open with { State = BatchState.Expired } }
                : document,
            ct);

        return document.Batch;
    }

    // T4, T7 and the end of an expiry: every call of the batch has its result in the conversation, so the batch
    // goes, and its decisions are remembered as closed until the next batch closes
    public Task CloseAsync(StoreId conversation, DecisionBatch batch, CancellationToken ct) =>
        ChangeAsync(
            conversation,
            document => document.Batch is { State: BatchState.Started or BatchState.Expired } settled
                && settled.IsSame(batch)
                    ? document with
                    {
                        Batch = null,
                        Closed = [.. settled.Ids.Select(id => new ClosedDecision(id, ClosedAs(settled.State)))]
                    }
                    : document,
            ct);

    private static DecisionDocument Record(
        StoreId conversation,
        DecisionDocument document,
        IReadOnlyList<DecisionAnswer> answers)
    {
        var closed = new Dictionary<string, DecisionClosedReason>();
        var recorded = new Dictionary<string, DecisionAnswer>();

        foreach (var answer in answers)
        {
            var ask = document.Batch?.Asks.FirstOrDefault(ask => ask.Id == answer.DecisionId);

            if (ask is null || document.Batch!.State is BatchState.Started or BatchState.Expired)
            {
                closed[answer.DecisionId] = ClosedReason(document, answer.DecisionId);
                continue;
            }

            var normalized = ask.Normalize(answer);

            if (ask.Answer is null) recorded[ask.Id] = normalized;
            else if (ask.Answer != normalized) closed[ask.Id] = DecisionClosedReason.Answered;
        }

        if (closed.Count > 0) throw DecisionClosedException.For(conversation, closed);
        if (recorded.Count == 0) return document;

        var batch = document.Batch!;

        List<BatchCall> calls =
        [
            .. batch.Calls.Select(call => call.Ask is { } ask && recorded.TryGetValue(ask.Id, out var answer)
                ? call with { Ask = ask with { Answer = answer } }
                : call)
        ];

        var state = calls.All(call => call.Ask is null or { Answer: not null })
            ? BatchState.Answered
            : BatchState.Open;

        return document with { Batch = batch with { Calls = calls, State = state } };
    }

    // Why a decision cannot be answered or carried out any more: its batch is under way or expired, or gone
    private static DecisionClosedReason ClosedReason(DecisionDocument document, string decision) =>
        document.Batch is { } batch && batch.Ids.Contains(decision)
            ? ClosedAs(batch.State)
            : document.ClosedReason(decision);

    private static DecisionClosedReason ClosedAs(BatchState state) =>
        state is BatchState.Expired ? DecisionClosedReason.Expired : DecisionClosedReason.Answered;

    // The tools the batch's answers approved for the conversation join those it has, each once
    private List<StandingApproval> WithGranted(IReadOnlyList<StandingApproval> standing, DecisionBatch batch)
    {
        var granted = batch.Calls
            .Where(call => call.Ask is { Kind: AskKind.Approval, Answer: { Approved: true, ForConversation: true } })
            .Select(call => new StandingApproval(call.Tool, call.Server, time.GetUtcNow()))
            .Where(approval => !standing.Any(held => held.Tool == approval.Tool && held.Server == approval.Server))
            .DistinctBy(approval => (approval.Tool, approval.Server));

        return [.. standing, .. granted];
    }

    private async Task<DecisionDocument> ChangeAsync(
        StoreId conversation,
        Func<DecisionDocument, DecisionDocument> change,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (document, version) = await ReadAsync(conversation, ct);
            var changed = change(document);

            if (ReferenceEquals(changed, document)) return document;

            try
            {
                await stores.SaveDecisionsAsync(conversation, changed.Serialize(), version, ct);
                return changed;
            }
            catch (StoreConflictException) when (attempt < MaxAttempts)
            {
                ct.ThrowIfCancellationRequested();
            }
        }
    }

    private async Task<(DecisionDocument Document, long Version)> ReadAsync(
        StoreId conversation,
        CancellationToken ct) =>
        await stores.LoadDecisionsAsync(conversation, ct) is { } stored
            ? (DecisionDocument.Deserialize(stored.Json), stored.Version)
            : (DecisionDocument.Empty, 0);
}
