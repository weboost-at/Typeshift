namespace Typeshift.Samples.Hosting;

// begin-snippet: run-generic
internal static class AgentAnswers
{
    public static async Task<string> AskAsync<TAgent>(AgentHost<TAgent> host, string question, CancellationToken ct)
        where TAgent : Agent
    {
        var answer = await host.RunAsync(question, ct);
        return $"{host.Agent.Name}: {answer.Text}";
    }
}
// end-snippet
