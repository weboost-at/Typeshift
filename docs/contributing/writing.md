---
title: Writing style
description: How the pages of these docs are written, shaped and formatted, for the people and the coding agents who change them.
---

# Writing style

These docs are read by .NET developers who want to get something done, and are often written by coding agents. The
rules below make that work: pages a reader can skim in a minute and trust, and that an agent can change without
breaking them. If you remember one thing: **lead with the answer, say why, and show code that compiles.**

::: tip The short version

- Start every page and every section with the answer. The detail comes after.
- Say **why**, not only what. A rule without a reason gets broken.
- Short sentences in the present tense. "You" for the reader, "Typeshift" for the library.
- One fact lives on one page; everywhere else links to it. The fact map in `AGENTS.md` names the page.
- C# comes from the samples through mdsnippets. Never write it by hand.
- `npm run docs:check` checks what a machine can. It must pass.

:::

## Voice

Write like a colleague explaining the library at a whiteboard: direct, friendly and precise. The
[Marten](https://martendb.io) and [Wolverine](https://wolverinefx.net) docs are the model: conversational,
opinionated, generous with code and honest about trade-offs.

| Do | Don't |
|---|---|
| "A tool's name is its method's name in snake_case." | "The name is, by default, derived from the method in snake_case form." |
| "Resolve the host from a request's scope, so the tools see that request's services." | "Resolve it from a request's or a job's scope." |
| "A UI can forward the updates over SignalR, for example." | "A UI forwards them over SignalR, say." |
| "The root is not a sandbox. Run the host in a container when commands must not reach further." | "Like the shell's, the root is not a sandbox: a link swapped in while a tool runs still leads outside, so give each customer a container." |

- **Present tense**, describing what the library does today. What is not built yet belongs in
  [Status](./status.md), not in the guide.
- **"You"** for instructions, **"Typeshift"** for what the library does, **"we"** only for the project's own choices.
- **Short sentences.** If a sentence has two commas and an "and", split it. If it needs a colon and a semicolon,
  make it a list.
- **The subject first.** "`ConfigureAsync` retries on a conflict", not "On a conflict, which another save causes,
  retrying is what `ConfigureAsync` does".
- **Examples say so.** "for example" or "such as", never a trailing "say".
- **No filler.** No "basically", "simply", "just", "note that", "it's worth noting", and no sentence that only
  announces the next one.
- **The words of the [glossary](../guide/glossary.md)**, used the same way everywhere: a *toolset* declares *tools*,
  an *agent* offers them, a *host* runs it.

## Page anatomy

Every page has the same shape, so readers always know where to look.

1. **Frontmatter** with a title and a description.
2. **H1**, the same as the title.
3. **Lead paragraph**, two to four sentences: what this is, why it exists, when you need it.
4. **"The short version"**, a tip box with three to five bullets, on pages longer than a screen.
5. **A diagram**, when a picture explains it faster than prose.
6. **Sections** (`##`), one idea each: the answer, the code, then what you need to know about it.
7. **Related**, a short list of links at the end.

### Kinds of page

| Kind | Answers | Lives in | Template |
|---|---|---|---|
| Tutorial | "Show me it working." Only [Getting started](../guide/getting-started.md). | `docs/guide/` | none |
| Concept | "How does this fit together, and why?" | `docs/guide/`, the Concepts group | [concept](#concept) |
| Guide | "How do I use this feature?" | `docs/guide/` | [guide](#guide) |
| Reference | "What exactly does this call take and do?" | `docs/guide/`, the Reference group | [reference](#reference) |
| Contributing | "How do I work on the repository?" | `docs/contributing/` | [concept](#concept) |

A guide page teaches the common path and links to the reference for the full catalog. A reference page lists every
call, member or option in a table and explains little. When a guide page and a reference page would say the same
thing, the fact map in [`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md) says which one
owns it.

## Frontmatter

```yaml
---
title: Tool approval
description: Pause a run until the host approves a tool call, and let rules decide the calls you are sure about.
---
```

| Key | Required | Meaning |
|---|---|---|
| `title` | Yes | The page's name, as people would say it. Shown in the browser tab and in search. |
| `description` | Yes | One sentence of at most 200 characters. Shown in search results and link previews. |

## Formatting

### Callouts

VitePress containers carry meaning. Use them for what they mean, never for decoration, and give each a title that
alone tells the reader whether to stop: `::: warning A tool can be called twice`.

| Container | Use for |
|---|---|
| `::: tip` | The short version, shortcuts, good practice |
| `::: info` | Context that helps but is not required: background, a comparison |
| `::: warning` | What goes wrong silently or surprises people |
| `::: danger` | What opens a security hole or loses data: a sandbox that is not one, untrusted input |
| `::: details` | Long reference material, collapsed by default |

### Tables

Tables beat prose for anything with the same shape repeated: the calls of a builder, the options of a run, the
diagnostics. Options get a `Default` column. A table that explains settings says what each one does and, where
people expect more, what it does not.

### Code

- **C# comes from the samples.** Every C# block is a region of `samples/Typeshift.Samples.Docs`, copied in by
  mdsnippets; see [Documentation](./documentation.md). The check fails on a hand-written C# block.
- **Every code block has a language**: `bash`, `xml`, `json`, `yaml`, `text`, `mermaid`.
- **Shell commands are copy-pasteable.** Placeholders look like `<version>`, never like real values.
- **Show the code before you explain it**, then say what the reader needs to know about it, in the order the code
  reads.

### Links

- Link the first mention of another page's topic in a section, and not again in that section.
- Link pages relatively, with the extension: `[tool settings](./tool-settings.md)`. Inside the guide, a
  section is `./page.md#section`.
- Link a diagnostic by its ID: `[TSH007](./diagnostics.md#tsh007)`.
- Link repository files with their full GitHub URL. VitePress fails on links that leave `docs/`.
- Link upstream documentation, Microsoft.Extensions.AI's for example, for anything Typeshift did not invent.
- The build fails on a dead page link, and `npm run docs:lint` on a dead anchor. Fix them; never ignore them.

### Diagrams

Draw a diagram when a picture explains faster than prose: the order of a run's steps, what the generator reads and
writes. Use a `mermaid` block, with at most about ten nodes, and say in the text what it shows, since a screen
reader cannot read it. Labels use the names the code uses.

## One fact, one page

Every fact has one canonical page. Change it there, and link to it from everywhere else. When you find the same fact
on two pages, make one of them a link. Repeating a code snippet is fine when a page needs it to make sense; repeating
its explanation is not.

The fact map in [`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md) names the canonical page
of each kind of fact, and its change checklist names the pages a kind of change touches.

## What never goes in

| Never | Instead |
|---|---|
| Hand-written C# | A region of the samples, through mdsnippets |
| Features that are not built, described as if they were | [Status](./status.md) |
| Decision IDs (`D25`) on guide pages | Plain reasons; the IDs belong to the contributing pages |
| The same explanation on two pages | One canonical page and links |
| "Updated on …" or "new in …" notes | Git history and the [changelog](https://github.com/weboost-at/Typeshift/blob/main/CHANGELOG.md) |
| A page named `agents.md` | Any other name: mdsnippets skips files of that name |

## Templates

Copy a template from
[`docs/contributing/templates/`](https://github.com/weboost-at/Typeshift/tree/main/docs/contributing/templates),
fill it in, delete the `<!-- hints -->`, and add the page to the sidebar in `docs/.vitepress/config.mts`.

### Guide

::: details templates/guide.md
<<< ./templates/guide.md
:::

### Concept

::: details templates/concept.md
<<< ./templates/concept.md
:::

### Reference

::: details templates/reference.md
<<< ./templates/reference.md
:::

## What the check enforces

`npm run docs:check` runs markdownlint (`.markdownlint-cli2.jsonc`), the docs rules (`docs/.vitepress/lint.mjs`) and
the build. The docs rules check what a machine can:

- every page has a title and a description, and its H1 matches the title;
- every page ends with `## Related`;
- every page is in the sidebar, and every sidebar link has a page;
- every code block has a language, and C# appears only where mdsnippets wrote it;
- every callout has a title;
- every link into the docs reaches its page and its anchor.

Everything else on this page is for the reviewer.

## Related

- [Documentation](./documentation.md): how the snippets, the checks and the site work.
- [Glossary](../guide/glossary.md): the words the docs use.
- [`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md): the fact map and the change checklist.
