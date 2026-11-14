using System.Text;
using System.Text.RegularExpressions;

namespace Typeshift.Generators.Tests.Repository.Support;

// The links from XML docs to the docs site, which an editor shows in a tooltip: those of the library and the
// generator, and those the generator writes, as the snapshots hold them. docs/.vitepress/lint.mjs checks the same
// links, so that a heading renamed in a docs-only change fails too
public static partial class GuideLinks
{
    private static readonly IReadOnlyList<string> Sources =
    [
        .. RepositoryFiles.Find("*.cs", "src"),
        .. RepositoryFiles.Find("*.verified.txt", "tests/Typeshift.Generators.Tests/Snapshots")
    ];

    // One line for each link whose page or anchor the docs do not have
    public static IReadOnlyList<string> Dead { get; } =
    [
        .. Sources.SelectMany(file => Link()
            .Matches(RepositoryFiles.Read(file))
            .Select(link => (Address: link.Value, Missing: Missing(link)))
            .Where(link => link.Missing is not null)
            .Select(link => $"{file}: {link.Address} reaches no {link.Missing} of the docs"))
    ];

    private static string? Missing(Match link)
    {
        var page = $"docs/{link.Groups["page"].Value}.md";
        var anchor = link.Groups["anchor"].Value;

        if (!File.Exists(Path.Combine(RepositoryFiles.Root, page))) return "page";
        return anchor.Length > 0 && !Anchors(page).Contains(anchor) ? "heading or anchor" : null;
    }

    // What a link's fragment can name: an id written as HTML, a heading's {#id}, or its slug, numbered from the
    // second heading of the same text on
    private static HashSet<string> Anchors(string page)
    {
        var text = Fence().Replace(RepositoryFiles.Read(page), "");
        var seen = new Dictionary<string, int>();

        var headings = Heading().Matches(text).Select(heading => heading.Groups["text"].Value).Select(heading =>
        {
            if (CustomId().Match(heading) is { Success: true } custom) return custom.Groups["id"].Value;

            var slug = Slug(heading);
            var count = seen[slug] = seen.GetValueOrDefault(slug, -1) + 1;
            return count == 0 ? slug : $"{slug}-{count}";
        });

        return [.. headings, .. Id().Matches(text).Select(id => id.Groups["id"].Value)];
    }

    // VitePress's slug of a heading, as docs/.vitepress/lint.mjs computes it
    private static string Slug(string heading)
    {
        var text = Tag().Replace(MarkdownLink().Replace(heading, "$1"), "").Replace("`", "").Replace("*", "").Trim();

        var letters = text.Normalize(NormalizationForm.FormKD)
            .Where(letter => letter is > '\u001F' and not (>= '̀' and <= 'ͯ'));

        var slug = Separators().Replace(string.Concat(letters), "-").Trim('-').ToLowerInvariant();
        return slug.Length > 0 && char.IsAsciiDigit(slug[0]) ? $"_{slug}" : slug;
    }

    [GeneratedRegex(@"https://weboost-at\.github\.io/Typeshift/(?<page>[\w/-]+)(?:#(?<anchor>[\w-]*))?")]
    private static partial Regex Link();

    [GeneratedRegex(@"(?ms)^\s*(?<fence>`{3,}|~{3,}).*?^\s*\k<fence>\s*$")]
    private static partial Regex Fence();

    [GeneratedRegex(@"^#{1,6} (?<text>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"\{#(?<id>[^}]+)\}$")]
    private static partial Regex CustomId();

    [GeneratedRegex(@"\bid=['""](?<id>[^'""]+)['""]")]
    private static partial Regex Id();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[\s~`!@#$%^&*()\-_+=\[\]{}|\\;:""'“”‘’<>,.?/]+")]
    private static partial Regex Separators();
}
