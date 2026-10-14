using System.Text;
using System.Text.RegularExpressions;

namespace Typeshift.Generators.Parsing;

internal static class ToolNames
{
    private static readonly Regex LowerSnakeCase = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");

    public static bool IsLowerSnakeCase(string name) => LowerSnakeCase.IsMatch(name);

    // D13: SearchTickets is search_tickets, CloseTicketAsync is close_ticket, ParseHTMLPage is parse_html_page
    public static string FromMethod(string method) =>
        ToSnakeCase(method.EndsWith("Async") && method.Length > 5 ? method.Substring(0, method.Length - 5) : method);

    // D22: SupportAgent is support_agent
    public static string ToSnakeCase(string name)
    {
        var snake = new StringBuilder();

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];

            if (character == '_')
            {
                if (snake.Length > 0 && snake[snake.Length - 1] != '_') snake.Append('_');
                continue;
            }

            if (char.IsUpper(character) && index > 0 && StartsWord(name, index) && snake[snake.Length - 1] != '_')
                snake.Append('_');

            snake.Append(char.ToLowerInvariant(character));
        }

        return snake.ToString().TrimEnd('_');
    }

    // An upper-case letter after a lower-case one or a digit starts a word, and so does the last of a run of
    // capitals that a lower-case letter follows
    private static bool StartsWord(string name, int index) =>
        !char.IsUpper(name[index - 1])
        || (index + 1 < name.Length && char.IsLower(name[index + 1]));
}
