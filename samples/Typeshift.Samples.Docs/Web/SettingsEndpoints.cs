using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Web;

// begin-snippet: support-preferences
// What a customer may change of the support agent's settings; the model and the instructions stay your staff's
internal sealed record SupportPreferences(string? ArticleLanguage);
// end-snippet

internal static class SettingsEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapSettings()
        {
            // begin-snippet: settings-endpoint
            app.MapPut("/settings", async (
                SupportPreferences preferences,
                AgentHost<SupportAgent> support,
                ICurrentCustomer customer,
                CancellationToken ct) =>
            {
                await support
                    .WithSettingsId(customer.Id)
                    .UpdateSettingsAsync(settings => settings with
                    {
                        SearchArticles = settings.SearchArticles with { Language = preferences.ArticleLanguage }
                    }, ct);

                return TypedResults.NoContent();
            });
            // end-snippet
        }
    }
}
