# Documentation

The documentation is a [VitePress](https://vitepress.dev) site in `docs/`, published to GitHub Pages from `main`.
Every code block that shows C# comes from code that compiles and runs:
[mdsnippets](https://github.com/SimonCropp/MarkdownSnippets) copies marked regions of the samples into the Markdown,
and CI fails when the two disagree.

## Where the code comes from

| Source | Shown on |
|---|---|
| `samples/Typeshift.Samples.Console` | Getting started, the README and Native AOT. CI publishes it as Native AOT and runs it. |
| `samples/Typeshift.Samples.Docs` | Every other guide page. It builds with the solution, and CI runs its program, which executes each documented scenario and throws when one breaks. |

A region is marked in the source with comments, in any file type:

```text
// begin-snippet: tool-defaults
...
// end-snippet
```

and used in Markdown by its name on a line of its own, `snippet: tool-defaults`. A whole file is used by its path
from the repository root, `snippet: samples/Typeshift.Samples.Docs/Examples/support-agent-settings.json`.

## Changing a page or its code

1. Change the sample code, keeping the markers, and run the docs program:
   `dotnet run --project samples/Typeshift.Samples.Docs -c Release`.
2. Run `dotnet mdsnippets` from the repository root. It rewrites every `snippet:` reference, and every snippet
   between `<!-- snippet: name -->` and `<!-- endSnippet -->`, in place. Never edit inside those markers by hand.
3. Preview the site with `npm ci` once, then `npm run docs:dev`.
4. Commit the code and the regenerated Markdown together.

CI runs `dotnet mdsnippets` and fails on any difference, then builds the site, which fails on dead links.

## Style

Follow the Marten and Wolverine documentation: a page per topic, a short paragraph of what and why, then the code,
then what the reader needs to know about it. No filler, no sentences that only announce the next one. C# never
appears in a hand-written code block; shell commands and output may. Link diagnostics as
`[TSH007](../guide/diagnostics.md#tsh007)` and repository files with their full GitHub URL, since VitePress fails on
links that leave `docs/`.
