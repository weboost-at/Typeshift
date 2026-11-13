namespace Typeshift.Tests.Compaction.Support;

// Runs as 0.1.0-alpha.6 stored them, in its JSON, before a model response kept compaction's estimate of its call
internal static class StoredRuns
{
    // One read, answered with a report of 2,000 input tokens for a call of a quarter of that
    public static IEnumerable<ConversationEntry> ReportedWithoutEstimate(string result) =>
        new[]
        {
            """
            {"agent":"compacting_agent","at":"2026-10-11T08:00:00+00:00",
            "messages":[{"role":"user","contents":[{"$type":"text","text":"Read it."}]}]}
            """,
            """
            {"agent":"compacting_agent","at":"2026-10-11T08:00:00+00:00","model":"compacting-model",
            "messages":[{"authorName":"compacting_agent","role":"assistant","contents":[{"$type":"functionCall",
            "name":"read","arguments":{"length":2000},"informationalOnly":false,"callId":"call_1"}]}]}
            """,
            $$"""
            {"agent":"compacting_agent","at":"2026-10-11T08:00:00+00:00",
            "messages":[{"authorName":"compacting_agent","role":"tool","contents":[{"$type":"functionResult",
            "result":"{{result}}","callId":"call_1"}]}]}
            """,
            """
            {"agent":"compacting_agent","at":"2026-10-11T08:00:00+00:00","model":"compacting-model",
            "usage":{"inputTokenCount":2000,"outputTokenCount":12},
            "messages":[{"authorName":"compacting_agent","role":"assistant",
            "contents":[{"$type":"text","text":"Done."}]}]}
            """,
            """
            {"agent":"compacting_agent","at":"2026-10-11T08:00:00+00:00","endsRun":true,"mode":"Default","messages":[]}
            """
        }.Select(ConversationEntry.Deserialize);
}
