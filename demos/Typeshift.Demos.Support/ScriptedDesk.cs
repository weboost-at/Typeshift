using Microsoft.Extensions.AI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Support;

// Canned models for runs without a network, one per agent, each deciding from the input and what it did so far
internal static class ScriptedDesk
{
    public static ScriptedChatClient Triage() => new(call =>
        call.LastUserText.Contains("invoice", StringComparison.OrdinalIgnoreCase)
        || call.LastUserText.Contains("charged", StringComparison.OrdinalIgnoreCase)
            ? ScriptedCall.Call("handoff_to_billing_agent")
            : call.Offers("handoff_to_support_agent")
                ? ScriptedCall.Call("handoff_to_support_agent")
                : ScriptedCall.Text("How can I help you today?"));

    public static ScriptedChatClient Billing() => new(call =>
    {
        if (call.LastUserText.Contains("ticket", StringComparison.OrdinalIgnoreCase) && call.Offers("handoff_to_support_agent"))
            return ScriptedCall.Call("handoff_to_support_agent");

        if (call.CallsOf("list_invoices") == 0) return ScriptedCall.Call("list_invoices");

        if (call.CallsOf("issue_refund") == 0)
            return ScriptedCall.Call("issue_refund", ("invoice", "INV-8"), ("amountCents", 4900), ("reason", "INV-7 was charged twice as INV-8."));

        var refund = call.LastResultOf("issue_refund") ?? "";

        return ScriptedCall.Text(refund.Contains("Refunded", StringComparison.Ordinal)
            ? "I refunded the duplicate charge of 49.00 on INV-8; it reaches your card within five days."
            : $"I could not refund it: {refund}");
    });

    public static ScriptedChatClient Support() => new(call =>
    {
        var asked = call.LastUserText;

        if (asked.Contains("down", StringComparison.OrdinalIgnoreCase) && call.CallsOf("service_status") == 0)
            return ScriptedCall.Call("service_status");

        if (call.CallsOf("service_status") == 1 && call.LastIsToolResult)
            return ScriptedCall.Text($"The status page says: {call.LastResultOf("service_status")}");

        if (call.CallsOf("search_tickets") == 0) return ScriptedCall.Call("search_tickets", ("query", "log in"));

        if (call.CallsOf("knowledge_agent") == 0)
            return ScriptedCall.Call("knowledge_agent", ("input", "How do I log in after the update?"));

        var tickets = call.LastResultOf("search_tickets") ?? "[]";
        var advice = call.LastResultOf("knowledge_agent") ?? "";
        var number = tickets.Contains("1001", StringComparison.Ordinal) ? "1001" : "none";

        return ScriptedCall.Text($"Your ticket {number} about logging in is open. The help center says: {advice}");
    });

    public static ScriptedChatClient Knowledge() => new(call =>
        call.CallsOf("search_articles") == 0
            ? ScriptedCall.Call("search_articles", ("query", "log in update"))
            : ScriptedCall.Text("Clear the browser's site data and log in again; the session format changed with the update."));

    public static ScriptedChatClient Report() => new(call =>
        ScriptedCall.Text(
            """
            { "customer": "Ada", "issue": "Charged twice for INV-7, and cannot log in since the update",
              "outcome": "Duplicate charge refunded; login fix from the help center", "refundIssued": true,
              "nextStep": "Close ticket 1003 once the refund arrives" }
            """));
}
