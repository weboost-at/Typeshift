---
name: update-docs
description: Change the Typeshift documentation site (docs/, VitePress), the README, or code the docs show. Use when a guide page needs a new section or example, when an API change breaks a snippet, or when mdsnippets or the docs build fails in CI.
---

# Update the docs

Read `docs/contributing/writing.md` first: the voice, the page anatomy, and the mechanics of snippets and checks.

1. Find where the fact lives: the fact map under "Docs" in `AGENTS.md` names the canonical page. Change it there and
   link to it from elsewhere. For a change to the library, the change checklist next to it names every page to touch.
2. Change the sample code in `samples/Typeshift.Samples.Docs`, keeping its `begin-snippet` and `end-snippet` markers,
   and run its program: `dotnet run --project samples/Typeshift.Samples.Docs -c Release`.
3. Check `git status` for Markdown you did not change (the mdsnippets trap in `AGENTS.md`), then run
   `dotnet mdsnippets` from the repository root; never edit between `<!-- snippet: x -->` and
   `<!-- endSnippet -->` by hand. C# never goes into a hand-written block.
4. Write the prose to `writing.md`: a lead that answers first, varied sentences, the reason for each rule, callouts
   with a title, `## Related` last. Read a neighbouring page and match it.
5. A new page starts from `docs/contributing/templates/`, goes into the sidebar in `docs/.vitepress/config.mts` and
   into the `## Related` of its neighbours. No page may be named `agents.md`: mdsnippets skips files of that name.
6. Run `npm ci` once, then `npm run docs:check`: markdownlint, the docs rules (frontmatter, headings, sidebar,
   anchors, code blocks, callouts) and a build that fails on dead links.
7. For a page with a diagram, run `npm run docs:dev` and look at it.
