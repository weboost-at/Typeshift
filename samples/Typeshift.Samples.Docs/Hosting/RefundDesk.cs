using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Hosting;

// begin-snippet: inject-host
internal sealed class RefundDesk(AgentHost<SupportAgent> support, ICurrentCustomer customer)
{
    public async Task<string> AnswerAsync(string question, CancellationToken ct)
    {
        var answer = await support.WithSettingsId(customer.Id).RunAsync(question, ct: ct);
        return answer.Text;
    }
}
// end-snippet
