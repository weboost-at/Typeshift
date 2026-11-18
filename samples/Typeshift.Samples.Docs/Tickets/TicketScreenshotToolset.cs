using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Tickets;

// begin-snippet: content-result
internal partial class TicketScreenshotToolset : Toolset
{
    public TicketScreenshotToolset()
    {
        AddTool(GetScreenshotAsync)
            .ReadOnly();
    }

    /// <summary>
    /// Shows the screenshot the customer attached to a ticket.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static async Task<DataContent> GetScreenshotAsync(
        int number,
        IServiceProvider services,
        CancellationToken ct)
    {
        var png = await services.GetRequiredService<ITicketClient>().GetScreenshotAsync(number, ct);

        return new DataContent(png, "image/png");
    }
}
// end-snippet
