using Typeshift.Samples.Docs.Agents;
using Typeshift.Samples.Docs.Knowledge;
using Typeshift.Samples.Docs.Tickets;

namespace Typeshift.Samples.Docs.Stores.EfCore;

// begin-snippet: settings-columns
internal sealed class SupportAgentSettingsRow
{
    public required string Tenant { get; init; }

    public string? Model { get; set; }

    public string? Instructions { get; set; }

    public float? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public int? TicketLimit { get; set; }

    public bool? IncludeClosedTickets { get; set; }

    public string? ArticleLanguage { get; set; }

    public static SupportAgentSettingsRow From(string tenant, SupportAgent.Settings settings) =>
        new()
        {
            Tenant = tenant,
            Model = settings.Model,
            Instructions = settings.Instructions,
            Temperature = settings.Temperature,
            MaxOutputTokens = settings.MaxOutputTokens,
            TicketLimit = settings.SearchTickets.Limit,
            IncludeClosedTickets = settings.SearchTickets.IncludeClosed,
            ArticleLanguage = settings.SearchArticles.Language
        };

    public SupportAgent.Settings ToSettings() =>
        new()
        {
            Model = Model,
            Instructions = Instructions,
            Temperature = Temperature,
            MaxOutputTokens = MaxOutputTokens,
            SearchTickets = new TicketToolset.SearchTicketsOverrides
            {
                Limit = TicketLimit,
                IncludeClosed = IncludeClosedTickets
            },
            SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = ArticleLanguage }
        };
}
// end-snippet
