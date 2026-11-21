namespace Typeshift.Generators.Tests.Repository.Support;

// A guard's violations as the reason of its assertion, one a line: BeEmpty alone names the first
public static class Listing
{
    public static string Of(IEnumerable<string> violations) =>
        $"every entry breaks the rule:{Environment.NewLine}"
        + string.Join(Environment.NewLine, violations).Replace("{", "{{").Replace("}", "}}");
}
