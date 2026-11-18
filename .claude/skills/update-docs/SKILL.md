---
name: update-docs
description: Change the Typeshift documentation site (docs/, VitePress), the README, or code the docs show. Use when a guide page needs a new section or example, when an API change breaks a snippet, or when mdsnippets or the docs build fails in CI.
---

# Update the docs

Read `docs/contributing/writing.md` first: the voice, the page anatomy, and the mechanics of snippets and checks.

1. Find where the fact lives: the fact map under "Docs" in `AGENTS.md` names the canonical page. Change it there and
   link to it from elsewhere. A correction also reads the pages in the row's "Also mentioned in": each links the
   canonical page or says the same, never something else. For a change to the library, the change checklist next to
   the fact map names every page to touch.
2. Change the sample code in `samples/Typeshift.Samples.Docs`, keeping its `begin-snippet` and `end-snippet` markers,
   and run its program: `dotnet run --project samples/Typeshift.Samples.Docs -c Release`. A new region runs in the
   scenario of its page under `Scenarios/`, which checks what the page says about it and throws when that breaks; a
   scenario of a new page is called from `Program.cs`. The failing examples of `docs/guide/diagnostics.md` are the
   one exception: each is a region `tshNNN` of
   `tests/Typeshift.Generators.Tests/Diagnostics/Support/DiagnosticSources.cs`, which the generator tests compile
   (the `add-diagnostic` playbook).
3. Run `npm run docs:snippets`, never `dotnet mdsnippets` itself: the script runs the tool and prints the uncommitted
   lines it replaced, the owner's drafts included (the mdsnippets trap in `AGENTS.md`). Never edit between
   `<!-- snippet: x -->` and `<!-- endSnippet -->` by hand. C# never goes into a hand-written block. Commit the
   regenerated Markdown with the code.
4. Write the prose to `writing.md`: a lead that answers first, varied sentences, the reason for each rule, callouts
   with a title, `## Related` last. Read a neighbouring page and match it. Then read your text as a newcomer would:
   explain each term on first use or link it, and rewrite any sentence you had to read twice. Run the `humanizer`
   skill over the prose you changed, with the Voice section of `writing.md` as its voice sample, and keep an edit only
   where `writing.md` agrees. The README is the owner's text: propose a change to it in your report or pull
   request, and edit it only when asked; a snippet `npm run docs:snippets` regenerated in it is the one exception.
5. A new page starts from `docs/contributing/templates/`, goes into the sidebar in `docs/.vitepress/config.mts` and
   into the `## Related` of its neighbours, and a guide page gets a row of the fact map in `AGENTS.md`, which the docs
   rules check. No page may be named `agents.md`, `claude.md`, `gemini.md` or `copilot-instructions.md`: mdsnippets
   skips files of these names.
6. Check as `AGENTS.md`, "Build and test", says: `npm ci` once, then `npm run docs:check`, which runs the snippets
   against their sources, markdownlint, the docs rules (frontmatter, headings, sidebar, links and anchors, code
   blocks, callouts, the words `writing.md` bans by name) and a build that fails on dead links. A change of a sample,
   of `DiagnosticSources.cs` or of XML docs in `src/` is a change of code as well, and the generator tests read
   `docs/guide/diagnostics.md`, `docs/guide/observability.md` and `docs/contributing/decisions.md`: after a change of
   any of them, build and run the suite too, as that section shows.
7. For a page with a diagram, run `npm run docs:dev` and look at it.
