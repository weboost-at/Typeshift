using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Hosting;

// begin-snippet: job-scoped-state
internal sealed class TenantDigest(ITypeshiftRuntime runtime)
{
    public async Task<string> DigestAsync(string tenant, CancellationToken ct)
    {
        await using var support = runtime.CreateHost<SupportAgent>();
        support.Services.GetRequiredService<CurrentTenant>().Id = tenant;

        var answer = await support.RunAsync("Summarize the tickets opened today.", ct);
        return answer.Text;
    }
}
// end-snippet
