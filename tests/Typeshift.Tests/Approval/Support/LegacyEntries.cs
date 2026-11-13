namespace Typeshift.Tests.Approval.Support;

// Entries of the guarded agent as the version before the decisions document stored them, as JSON:
// Microsoft.Extensions.AI's approval contents among the messages, an answer's mark in its additional properties, and
// a summary that keeps the standing approvals
public static class LegacyEntries
{
    private const string Head = """{"agent":"guarded_agent","at":"2026-10-11T08:00:00+00:00",""";

    private const string ForConversation = ""","additionalProperties":{"typeshift.approve_for_conversation":true}""";

    public static string Input(string text) =>
        $$"""{{Head}}"messages":[{"role":"user","contents":[{"$type":"text","text":"{{text}}"}]}]}""";

    public static string Answer(string text) =>
        $$"""
        {{Head}}"model":"guarded-model","messages":[{"authorName":"guarded_agent","role":"assistant",
        "contents":[{"$type":"text","text":"{{text}}"}]}]}
        """;

    public static string Calling(string callId, int number) =>
        $$"""
        {{Head}}"model":"guarded-model","messages":[{"authorName":"guarded_agent","role":"assistant",
        "contents":[{{Call(callId, number)}}]}]}
        """;

    public static string Request(string callId, int number) =>
        $$"""
        {{Head}}"messages":[{"authorName":"guarded_agent","role":"assistant","contents":[
        {"$type":"toolApprovalRequest","toolCall":{{Call(callId, number)}},"requiresConfirmation":true,
        "requestId":"ficc_{{callId}}"}]}]}
        """;

    public static string Approval(string callId, int number, bool forConversation = false) =>
        $$"""
        {{Head}}"messages":[{"role":"user","contents":[
        {"$type":"toolApprovalResponse","approved":true,"toolCall":{{Call(callId, number)}},
        "requestId":"ficc_{{callId}}"{{(forConversation ? ForConversation : "")}}}]}]}
        """;

    public static string Result(string callId, int number) =>
        $$"""
        {{Head}}"messages":[{"role":"tool","contents":[
        {"$type":"functionResult","result":"closed {{number}}","callId":"{{callId}}"}]}]}
        """;

    public static string End(AgentMode mode) => $$"""{{Head}}"endsRun":true,"mode":"{{mode}}","messages":[]}""";

    public static string Summary(string text, AgentMode mode, string standing) =>
        $$"""
        {{Head}}"mode":"{{mode}}","isSummary":true,"standingApprovals":["{{standing}}"],
        "messages":[{"role":"user","contents":[{"$type":"text","text":"{{text}}"}]}]}
        """;

    private static string Call(string callId, int number) =>
        $$"""
        {"$type":"functionCall","name":"close_ticket","arguments":{"number":{{number}}},"informationalOnly":false,
        "callId":"{{callId}}"}
        """;
}
