using Microsoft.Extensions.Hosting;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Hosting;

// begin-snippet: background-host
internal sealed class TicketDigest(TypeshiftRuntime runtime, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DigestAsync(stoppingToken);
        }
    }

    public async Task<string> DigestAsync(CancellationToken ct)
    {
        // A host for each job, so the run gets scoped services of its own, as a request would
        await using var support = runtime.Agent<SupportAgent>();

        var answer = await support.RunAsync("Summarize the tickets opened today.", ct: ct);
        return answer.Text;
    }
}
// end-snippet
