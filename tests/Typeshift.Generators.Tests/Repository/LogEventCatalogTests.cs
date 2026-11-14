using System.Reflection;
using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Typeshift.Generators.Tests.Repository;

// D36: a filter or an alert names an event by its id and its name, so the guide lists every event the library logs
public partial class LogEventCatalogTests
{
    // Each event as the start of its row in the guide, in the order of the ids
    private static readonly IReadOnlyList<(int Id, string Row)> Events =
    [
        .. typeof(Agent).Assembly.GetType("Typeshift.LogEvents", throwOnError: true)!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => (method.Name, Event: method.GetCustomAttribute<LoggerMessageAttribute>()))
            .Where(method => method.Event is not null)
            .OrderBy(method => method.Event!.EventId)
            .Select(method => (
                method.Event!.EventId,
                $"| {method.Event.EventId} | `{method.Name}` | {method.Event.Level} |"))
    ];

    [Fact]
    public void Every_Log_Event_Has_Its_Row_In_The_Docs_And_Every_Row_Its_Event()
    {
        // Arrange
        var docs = RepositoryFiles.Read("docs/guide/observability.md");

        // Act
        var rows = EventRow().Matches(docs).Select(match => match.Value);

        // Assert
        rows.Should().Equal(Events.Select(logged => logged.Row));
    }

    [Fact]
    public void Every_Log_Event_Has_An_Id_Of_Its_Own()
    {
        // Act
        var ids = Events.Select(logged => logged.Id);

        // Assert
        ids.Should().OnlyHaveUniqueItems();
    }

    // An event's row starts with its id, its name as code and its level
    [GeneratedRegex(@"^\| \d+ \| `\w+` \| \w+ \|", RegexOptions.Multiline)]
    private static partial Regex EventRow();
}
