using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Typeshift.Generators.Parsing;

internal static class ToolNames
{
    // D35, D82: the tools a run offers itself, which a tool of a toolset or an agent may not be named like. The
    // runtime holds the same literals, and a test compares the two
    public const string AskUser = "ask_user";

    public const string ExitPlanMode = "exit_plan_mode";

    public const string HandoffPrefix = "handoff_to_";

    // \z, since $ also matches before a line break that ends the name
    private static readonly Regex LowerSnakeCase = new(@"^[a-z][a-z0-9]*(_[a-z0-9]+)*\z");

    public static bool IsLowerSnakeCase(string name) => LowerSnakeCase.IsMatch(name);

    // What a run offers under the name itself, or null for a name a tool may have
    public static string? GetReservedFor(string name) =>
        name switch
        {
            AskUser => "the tool that asks the user, which UseQuestions() offers",
            ExitPlanMode => "the tool that presents a plan in plan mode",
            _ when name.StartsWith(HandoffPrefix, StringComparison.Ordinal) => "the tools UseHandoff<T>() offers",
            _ => null
        };

    // D13: SearchTickets is search_tickets, CloseTicketAsync is close_ticket, ParseHTMLPage is parse_html_page
    public static string FromMethod(string method) =>
        ToSnakeCase(
            method.EndsWith("Async", StringComparison.Ordinal) && method.Length > 5
                ? method.Substring(0, method.Length - 5)
                : method);

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

            // A leading _ appends nothing, so the first letter may follow none
            var follows = snake.Length > 0 && snake[snake.Length - 1] != '_';
            if (char.IsUpper(character) && follows && StartsWord(name, index)) snake.Append('_');

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
