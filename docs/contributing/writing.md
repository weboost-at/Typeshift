---
title: Writing the docs
description: How the pages of these docs are written and shaped, where their code comes from, and the checks that keep pages and code in step.
---

# Writing the docs

These docs are for .NET developers who want to get something done, and are often written by coding agents. If you
remember one thing: **lead with the answer, explain why, and talk to the reader.**

## Voice

Write like a colleague who sits next to the reader and walks them through the library: direct, friendly and precise.
Talk about the reader's code, explain what each step is for, and say so when a choice is theirs. The
[Marten](https://martendb.io) and [Wolverine](https://wolverinefx.net) docs are the model.

Two habits make a page sound generated. One is compression: sentences so short and clever that the reader has to
decode them, such as "An agent is a constructor". The other is sameness: every sentence the same length, every
section the same shape, and the same fact said three times. Read the page aloud. If it sounds like a spec or a
slogan, rewrite it.

| Do | Don't |
|---|---|
| "You create an agent in its constructor: a few chained calls pick the model, the instructions and the tools." | "An agent is a constructor." |
| "You may group tools by feature, by domain or by permission level. How you group them is up to you." | A rule where the choice is the reader's: "Group tools by domain." |
| "The analyzer rejects `ref` and `out` parameters with TSH004." | "`ref` and `out` parameters are TSH004." |
| "A tool is named after its method in snake_case, without `Async`." | "The name is, by default, derived from the method in snake_case form." |
| "Stored settings are keyed by the tool's name, so renaming the method orphans them." | "A tool's name keys its stored settings. Rename the method. The settings no longer apply." |
| "Write the exception's message as a hint, and the model can correct its call." | "It's worth noting that exception messages are seamlessly surfaced to the model." |
| "A UI can forward the updates over SignalR, for example." | "A UI forwards them over SignalR, say." |

- **Present tense**, describing what the library does today. What is not built yet belongs in
  [Status](./status.md), not in the guide.
- **"You"** for instructions, **"your app"** for the application that uses Typeshift, **"Typeshift"** for what the
  library does, **"we"** only for the project's own choices. "The host" is always `AgentHost<TAgent>`, never your app.
- **Explain the step between two facts.** Spell out what a call does for the reader and what follows from it. If the
  reader has to read a sentence twice, it is too short, however correct it is. A term gets a few words of
  explanation or a link the first time a page uses it.
- **Leave the reader their choices.** Where Typeshift leaves a decision to the reader, say so and name the common
  options, as the toolset example above does.
- **Vary the rhythm.** Join two short sentences that belong together with "so", "because" or "but", and split one
  that needs a colon and a semicolon. Most sentences stay under 25 words.
- **The subject first.** "`ConfigureAsync` retries on a conflict", not "On a conflict, which another save causes,
  retrying is what `ConfigureAsync` does".
- **Concrete over abstract.** Name the example in the prose: "closing a ticket", not "an operation with side
  effects".
- **Short paragraphs** of whole sentences that make sense on their own. Several facts about a snippet become a list,
  not one long paragraph.
- **Say why once.** A rule without a reason gets broken, and a reason given three times gets skipped.
- **Examples say so.** "for example" or "such as", never a trailing "say".
- **No filler.** No "basically", "simply", "note that", "it's worth noting", "seamlessly", "robust", no "just" as a
  softener ("just call `RunAsync`"), no em dashes, no rhetorical questions, no sentence that only announces the next
  one, and no closing summary. "Just" in the sense of "only" is fine: "with just a few lines of code".
- **No staging.** No "not X but Y" contrast with something nobody claimed, no one-line paragraph that repeats the one
  before, no list of three when the meaning has two, and no "serves as" or "acts as" where "is" works. The
  [Humanizer](https://github.com/blader/humanizer) skill lists more of these tells.
- **The words of the [glossary](../guide/glossary.md)**, used the same way everywhere: a *toolset* declares *tools*,
  an *agent* offers them, a *host* runs it.

## The pitch

The README and the home page sell Typeshift and get a reader running. The guide holds the details, so the README
stays light: a short pitch, a quick start, and a table of links into the guide.

- **Lead with what the reader does.** Name the task, such as "Create an agent" or "Design your toolsets", and say
  how little it takes. What the generator emits, such as functions, records, one registration call or Native AOT
  support, backs the pitch but never leads it.
- **Plain words over slogans.** A tagline or a feature title says what Typeshift does in words a .NET developer
  would use. A title such as "A tool is a method" makes the reader decode it first.
- **Never "plain C#".** It reads as classes without a base class, and toolsets and agents derive from `Toolset` and
  `Agent`.
- **Getting started stays at about four steps**, because Typeshift is meant to be easy and a long first page says
  otherwise.

## Page anatomy

Every page has the same shape, so readers always know where to look:

1. **Frontmatter** with a title and a description.
2. **H1**, the same as the title.
3. **Lead paragraph**, two or three sentences: what this is, why it exists, when you need it. It does the job a
   summary box would, so pages have none.
4. **A diagram**, when a picture explains it faster than prose.
5. **Sections** (`##`), one idea each: the code, then what you need to know about it.
6. **Related**, a short list of links at the end.

A page that grows past a few screens usually explains a fact another page owns. Link to that page instead.

### Kinds of page

| Kind | Answers | Lives in | Template |
|---|---|---|---|
| Tutorial | "Show me it working." Only [Getting started](../guide/getting-started.md). | `docs/guide/` | none |
| Concept | "How does this fit together, and why?" | `docs/guide/`, next to the guides it explains | [concept](#concept) |
| Guide | "How do I use this feature?" | `docs/guide/` | [guide](#guide) |
| Reference | "What exactly does this call take and do?" | `docs/guide/`, the Reference group | [reference](#reference) |
| Contributing | "How do I work on the repository?" | `docs/contributing/` | [concept](#concept) |

A guide page teaches the common path and links to the reference for the full catalog. A reference page lists every
call, member or option in a table and explains little.

## Frontmatter

```yaml
---
title: Approval and modes
description: Pause a run until the host approves a tool call, and let rules decide the calls you are sure about.
---
```

`title` is the page's name as people would say it, shown in the browser tab and in search. `description` is one
sentence of at most 200 characters, shown in search results and link previews. Both are required.

## Formatting

### Callouts

VitePress containers carry meaning, so use them for what they mean and never for decoration. Give each a title that
alone tells the reader whether to stop: `::: warning A tool can be called twice`.

| Container | Use for |
|---|---|
| `::: tip` | Shortcuts and good practice |
| `::: info` | Context that helps but is not required: background, a comparison |
| `::: warning` | What goes wrong silently or surprises people |
| `::: danger` | What opens a security hole or loses data: a sandbox that is not one, untrusted input |
| `::: details` | Long reference material, collapsed by default |

### Tables

Tables beat prose for anything with the same shape repeated: the calls of a builder, the options of a run, the
diagnostics. They also beat a list of bullets that each start with a bold call name. Options get a `Default` column.

### Code

- **C# comes from the samples**, through mdsnippets (see [Where the code comes from](#where-the-code-comes-from)).
  The check fails on a hand-written C# block.
- **Every code block has a language**: `bash`, `xml`, `json`, `yaml`, `text`, `mermaid`.
- **Shell commands are copy-pasteable.** Placeholders look like `<version>`, never like real values.
- **Show the code before you explain it**, then say what the reader needs to know, in the order the code reads.

### Links

- Link the first mention of another page's topic in a section, and not again in that section.
- Link pages relatively, with the extension: `[tool settings](./tool-settings.md)`, or `./page.md#section`.
- Link a diagnostic by its ID: `[TSH007](./diagnostics.md#tsh007)`.
- Link repository files with their full GitHub URL, because VitePress fails on links that leave `docs/`.
- Link upstream documentation, Microsoft.Extensions.AI's for example, for anything Typeshift did not invent.

### Diagrams

Draw a diagram when a picture explains faster than prose: the order of a run's steps, what the generator reads and
writes. Use a `mermaid` block with at most about ten nodes, labelled with the names the code uses.
[vitepress-plugin-mermaid](https://github.com/emersonbottero/vitepress-plugin-mermaid) renders it in the browser,
so look at it with `npm run docs:dev` before you commit. Say in the text what it shows, since a screen reader cannot
read it.

## One fact, one page

Every fact has one canonical page. Change it there and link to it from everywhere else, and when you find the same
fact on two pages, make one of them a link. Repeating a code snippet is fine when a page needs it to make sense;
repeating its explanation is not.

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
| A page named `agents.md` | Any other name: mdsnippets skips every file of that name, whatever its folder |

## Where the code comes from

Every C# block is a region of `samples/Typeshift.Samples.Docs`, which builds with the solution. CI runs its program,
which executes each documented scenario and throws when one breaks. A region is marked in the source with comments,
in any file type:

```text
// begin-snippet: settings-picks
...
// end-snippet
```

A page uses it by its name, on a line of its own: `snippet: settings-picks`.
[mdsnippets](https://github.com/SimonCropp/MarkdownSnippets) replaces that line with the code, between
`<!-- snippet: settings-picks -->` and `<!-- endSnippet -->`, plus a link to the source. A whole file is used by its
path from the repository root.

## Changing a page or its code

1. Change the sample code, keeping the markers, and run the docs program:
   `dotnet run --project samples/Typeshift.Samples.Docs -c Release`.
2. Run `dotnet mdsnippets` from the repository root. It rewrites every snippet in place, so never edit between the
   markers by hand.
3. Write the prose to this page, on the page the fact map names.
4. Run `npm ci` once, then `npm run docs:check`, and `npm run docs:dev` to look at the page.
5. Commit the code and the regenerated Markdown together.

## Adding a page

1. Copy a [template](#templates): a guide, a concept or a reference page.
2. Add it to the sidebar in `docs/.vitepress/config.mts`, in reading order.
3. Link it from the `## Related` of its neighbours.
4. When it becomes the home of a kind of fact, add a row to the fact map in `AGENTS.md`.

Before you add one, check whether the topic fits as a section of an existing page. Fewer, fuller pages are easier
to find than many thin ones.

## The checks

| Command | Fails on |
|---|---|
| `dotnet mdsnippets && git diff --exit-code` | Any C# in the Markdown that differs from the samples |
| `npm run docs:lint` | markdownlint (`.markdownlint-cli2.jsonc`), then the docs rules (`docs/.vitepress/lint.mjs`) |
| `npm run docs:build` | A dead page link |
| `npm run docs:check` | `docs:lint`, then `docs:build` |

The docs rules check what a machine can:

- every page has a title and a description, and its H1 matches the title;
- every page ends with `## Related`;
- every page is in the sidebar, and every sidebar link has a page;
- every code block has a language, and C# appears only where mdsnippets wrote it;
- every callout has a title;
- every link into the docs reaches its page and its anchor.

The `docs` workflow runs mdsnippets, `docs:lint` and the build on every pull request, and deploys the site from
`main`. Everything else on this page is for the reviewer.

## Templates

Copy a template from
[`docs/contributing/templates/`](https://github.com/weboost-at/Typeshift/tree/main/docs/contributing/templates),
fill it in, and delete the `<!-- hints -->`.

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

## Related

- [Contributing](./index.md): building and testing the library.
- [Glossary](../guide/glossary.md): the words the docs use.
- [`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md): the fact map and the change checklist.
