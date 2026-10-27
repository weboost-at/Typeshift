---
name: review-docs
description: Audit the Typeshift docs for facts that drifted from the code, the same fact on two pages, broken page anatomy and style, and either report the findings or fix them. Use for periodic maintenance, after a large change to the library, or when the user asks whether the docs are still accurate.
---

# Review the docs

The goal is a list of concrete problems, each with a file, what is wrong and a proposed fix. Fix only what the user
asked you to fix; otherwise report.

## 1. Mechanical checks

mdsnippets rewrites every Markdown file, so check `git status` first and set aside any `.md` change you did not make.

```bash
dotnet mdsnippets && git diff --exit-code
npm run docs:check
```

Anything failing is the first finding.

## 2. Drift from the code

For each guide page, compare what it states with the code it describes: defaults and limits in
`src/Typeshift/Agents/RunOptions.cs` and the builders, the chain calls in `TypeshiftBuilder.cs`, the members the
emitters write, the diagnostics in `src/Typeshift.Generators/Diagnostics/`. A stated default, limit, name or order
that the code contradicts is **wrong**.

## 3. One fact, one page

For each row of the fact map under "Docs" in `AGENTS.md`, compare the canonical page with the pages in "Also
mentioned in". They may link or show the same snippet; they may not explain the same thing twice, and never
differently.

## 4. Anatomy and style

Read each section in sidebar order against `docs/contributing/writing.md`:

- a lead that answers what and why, and no summary box;
- pitch lines and the README following "The pitch" in `writing.md`, and short paragraphs;
- varied sentences that read like a person wrote them, the reason for each rule, no filler, no trailing "say";
- explanations rather than compressed lines: no sentence the reader has to read twice, no slogan as a heading, no
  tell from "No staging" in `writing.md` or from the `humanizer` skill (with the Voice section of `writing.md` as its
  voice sample), and "your app" where the application is meant;
- no fact a page explains that another page owns, and no page longer than its topic needs;
- callouts for what they mean: security cautions as `danger`, silent surprises as `warning`;
- `## Related` that links forward to details and back to the concept;
- every term used as the glossary defines it.

## 5. Reader test

Hand each sidebar group to a fresh agent that knows nothing of your work: give it only the page paths, read-only
access to `src/` and `samples/`, and the role of a .NET developer who is new to Typeshift. Ask it for every sentence
it had to read twice, every term used before it is explained, every question a page raises and never answers, and
every claim that looks wrong against the code, each quoted with a proposed rewrite. Don't tell it what you expect it
to find. Its findings are claims: check each one against the code before you report or fix it.

## 6. Report

Group the findings by severity:

1. **Wrong**: contradicts the code or another page.
2. **Duplicated**: the same fact explained on two pages.
3. **Unclear**: style, structure or missing links.

For each: file, finding, proposed fix. Offer to apply the fixes with the `update-docs` playbook.
