---
name: update-docs
description: Change the Typeshift documentation site (docs/, VitePress), the README, or code the docs show. Use when a guide page needs a new section or example, when an API change breaks a snippet, or when mdsnippets or the docs build fails in CI.
---

# Update the docs

`docs/contributing/documentation.md` is the full description; this is the working order.

1. **Code first.** C# in the docs is never typed into Markdown. Write it in `samples/Typeshift.Samples.Docs` (or the
   console sample for getting started and the README), between `// begin-snippet: <name>` and `// end-snippet`.
   When it shows behavior, make a scenario in `Scenarios/` run it and `Expect.That(...)` the outcome the page claims.
2. **Run it:** `dotnet run --project samples/Typeshift.Samples.Docs -c Release`. It throws when a scenario breaks.
3. **Reference it** in the page with a line `snippet: <name>`, or a whole file by its path from the repository root.
4. **Regenerate:** `dotnet tool restore` once, then `dotnet mdsnippets` from the repository root. It rewrites the
   blocks between `<!-- snippet: ... -->` and `<!-- endSnippet -->` in every Markdown file. Never edit inside them.
5. **Build the site:** `npm ci` once, then `npm run docs:build` (fails on dead links) or `npm run docs:dev`.
6. **Commit** the code and the regenerated Markdown together.

## Writing

- A page per topic: what it is and why in a short paragraph, the code, then what the reader must know about it.
  No filler, no sentence that only announces the next.
- Generic types in prose go in backticks; VitePress reads a bare `<T>` as an HTML tag.
- Link diagnostics as `./diagnostics.md#tsh007` from guide pages; link repository files with full GitHub URLs.
- New pages go in the sidebar in `docs/.vitepress/config.mts`.
- The package README (`src/Typeshift/PACKAGE.md`) has no code on purpose (D39).
