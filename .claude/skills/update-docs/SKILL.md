---
name: update-docs
description: Change the Typeshift documentation site (docs/, VitePress), the README, or code the docs show. Use when a guide page needs a new section or example, when an API change breaks a snippet, or when mdsnippets or the docs build fails in CI.
---

# Update the docs

Read `docs/contributing/documentation.md` first.

1. Change the sample code in `samples/Typeshift.Samples.Docs`, keeping its `begin-snippet` and `end-snippet` markers.
2. Run `dotnet mdsnippets` from the repository root; never edit between `<!-- snippet: x -->` and
   `<!-- endSnippet -->` by hand.
3. A new page goes into the sidebar in `docs/.vitepress/config.mts`. No page may be named `agents.md`: mdsnippets
   skips files of that name.
4. Build the site with `npm ci` once, then `npm run docs:build`; it fails on dead links.
