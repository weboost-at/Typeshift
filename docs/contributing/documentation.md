---
title: Documentation
description: How the docs site is built, where its code comes from, and the checks that keep pages and code in step.
---

# Documentation

The documentation is a [VitePress](https://vitepress.dev) site in `docs/`, published to GitHub Pages from `main`.
Every C# block on it comes from code that compiles and runs: [mdsnippets](https://github.com/SimonCropp/MarkdownSnippets)
copies marked regions of the samples into the Markdown, and CI fails when the two disagree. How to write a page is in
[Writing style](./writing.md); this page covers the mechanics.

::: tip The short version

- Change the sample code, run `dotnet mdsnippets`, commit both. Never edit a snippet in the Markdown.
- `npm run docs:check` runs markdownlint, the docs rules and the build. CI runs the same.
- A new page starts from a template and goes into the sidebar.
- The fact map in `AGENTS.md` says which page owns which fact.

:::

## Where the code comes from

| Source | Shown on |
|---|---|
| `samples/Typeshift.Samples.Docs` | The guide pages and the README. It builds with the solution, and CI runs its program, which executes each documented scenario and throws when one breaks. |

A region is marked in the source with comments, in any file type:

```text
// begin-snippet: settings-picks
...
// end-snippet
```

A page uses it by its name, on a line of its own: `snippet: settings-picks`. mdsnippets replaces that line with the
code, between `<!-- snippet: settings-picks -->` and `<!-- endSnippet -->`, and a link to the source. A whole file is
used by its path from the repository root.

## Changing a page or its code

1. Change the sample code, keeping the markers, and run the docs program:
   `dotnet run --project samples/Typeshift.Samples.Docs -c Release`.
2. Run `dotnet mdsnippets` from the repository root. It rewrites every snippet in place. Never edit between the
   markers by hand.
3. Write the prose to [Writing style](./writing.md). Find the page that owns the fact in the fact map of
   [`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md).
4. Run `npm ci` once, then `npm run docs:check`, and `npm run docs:dev` to look at the page.
5. Commit the code and the regenerated Markdown together.

## Adding a page

1. Copy a template from `docs/contributing/templates/`: a guide, a concept or a reference page.
2. Add it to the sidebar in `docs/.vitepress/config.mts`, in reading order. The check fails on a page the sidebar
   leaves out.
3. Link it from the `## Related` of its neighbours.
4. When it becomes the home of a kind of fact, add a row to the fact map in `AGENTS.md`.

::: warning No page may be named agents.md
mdsnippets skips every file named `agents.md`, whatever its folder, so its snippets would never update.
:::

## Diagrams

Diagrams are Mermaid blocks in the page, rendered in the browser by
[vitepress-plugin-mermaid](https://github.com/emersonbottero/vitepress-plugin-mermaid). Use them for flows and
pipelines, with at most about ten nodes, and look at them with `npm run docs:dev` before you commit.

## The checks

| Command | Checks | Fails on |
|---|---|---|
| `dotnet mdsnippets && git diff --exit-code` | The snippets match their source | Any C# in the Markdown that differs from the samples |
| `npm run docs:lint` | markdownlint (`.markdownlint-cli2.jsonc`) and the docs rules (`docs/.vitepress/lint.mjs`) | Missing frontmatter, a stray H1, a missing `## Related`, a page outside the sidebar, a code block without a language, hand-written C#, a callout without a title, a dead anchor |
| `npm run docs:build` | The site builds | A dead page link |
| `npm run docs:check` | `docs:lint`, then `docs:build` | Any of the above |

The `docs` workflow runs mdsnippets, `docs:lint` and the build on every pull request, and deploys the site from
`main`.

## Related

- [Writing style](./writing.md): voice, page anatomy, callouts and the templates.
- [Contributing](./index.md): building and testing the library.
- [`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md): the fact map and the change checklist.
