---
title: Releasing
description: How a version tag becomes a NuGet package and a GitHub release, and what to set up before the first one.
---

# Releasing

A release is a version tag. Pushing one runs the `release` workflow, which checks the tag, runs everything CI runs on
the tagged commit, and publishes the package with its symbols to nuget.org and GitHub.

## Versions

[MinVer](https://github.com/adamralph/minver) reads the version from the latest `v` tag, so `v0.2.0` packs `0.2.0`
and `v0.2.0-alpha.1` packs `0.2.0-alpha.1`. An untagged commit packs as a prerelease of the next patch, such as
`0.2.1-alpha.0.3` for the third commit after `v0.2.0`, so a local pack never looks like a release.

## Before the first release

This is one-time setup, and a fork or a move to another account has to redo it. It needs admin rights on the
repository and the nuget.org account. The package ID is `Typeshift` (D48), and reserving the `Typeshift.*` prefix on
nuget.org keeps later packages under the name.

1. **Set up trusted publishing on nuget.org.** Under the account's *Trusted Publishing*, add a policy for the
   repository owner `weboost-at`, the repository `Typeshift`, the workflow file `release.yml` and the environment
   `release`. Then add the repository secret `NUGET_USER` with the nuget.org user name, not the email. No API key is
   stored, because each run trades its GitHub identity for a key that lives one hour.
2. **Protect the `release` environment** under *Settings > Environments*, with required reviewers or a rule that only
   `v*` tags may deploy.
3. **Turn on GitHub Pages** under *Settings > Pages*, with *GitHub Actions* as the source, so the docs workflow can
   deploy the site.
4. **Turn on private vulnerability reporting** in the repository's security settings. `SECURITY.md` sends a reporter
   to the form this adds to the *Security* tab, and without the setting the form does not exist.
5. **Turn on Dependabot alerts** on the same settings page. `.github/dependabot.yml` needs no setting to propose new
   versions of the packages and the workflows' actions once a week. The alerts add what a schedule cannot: word of
   an advisory against a version in use.

## Cutting a release

1. On `main`, with CI green: in `CHANGELOG.md`, rename `[Unreleased]` to the version and date, and open a new empty
   `[Unreleased]` above it. The release workflow fails without a section for the tag's version, and that section
   becomes the notes of the GitHub release. The docs site, which is built from `main`, shows the version of the
   newest section beside its name and in `llms.txt` as the newest package, so it names the release from this commit
   on.
2. Move the rows of `src/Typeshift.Generators/AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` under a
   `## Release <version>` heading. From then on, a shipped diagnostic ID is never reused or renumbered. A prerelease
   skips this step, because the heading takes only a plain version (RS2007), so the rules ship with the next stable
   release. Its IDs are taken all the same: a number any tag shipped is never given to another diagnostic, though
   what it reports may still change before 1.0, with a line in the changelog (D49).
3. Compare the public API with the last release, with that release's tag in place of `v0.1.0`:

   ```shell
   git diff v0.1.0 -- tests/Typeshift.Generators.Tests/Snapshots/PublicApi.verified.txt
   ```

   Every line the diff removes or changes is a break. Each needs its entry under Changed or Removed in the
   changelog's section, as [the alpha's rules](#breaking-changes-during-the-alpha) ask. From the first beta on, the
   diff only adds, as [Compatibility from the first beta](#compatibility-from-the-first-beta) says, and
   `PackageValidationBaselineVersion` makes the pack fail on a break nobody meant.

4. Check that the last run of the `demos` workflow on `main` is green. No pull request waits for that workflow, so
   a broken demo shows only there. A demo that broke gets fixed before the tag, or the release notes say so. Without
   access to the runs, do what the workflow does: `dotnet build demos/Typeshift.Demos.slnx -c Release -warnaserror`,
   then run each demo offline as its README shows.
5. Measure the generator and cut its input apart, with the tool outside the solution:

   ```shell
   dotnet run --project tools/Typeshift.Tools.Generators -c Release -- measure
   dotnet run --project tools/Typeshift.Tools.Generators -c Release -- fuzz
   ```

   `measure` prints what a generator run costs after an unrelated edit, by the number of toolsets and agents, and
   what the analyzer costs in code that uses nothing of the library. Compare it with the table in
   [Status](./status.md#known-gaps), and update the table when a number moved for a reason the changelog names.
   `fuzz` runs the generator and the analyzer over every source of the generator tests and the docs samples, cut
   after each token of each constructor, as the code stands while it is typed, and must end with no cut that made
   either throw; it takes a few minutes, and `fuzz 4` runs it on four threads. A cut that does becomes an entry of
   `HalfTypedSources` with its fix. Run both also after a change to the parsers or to `ConstructorChains`.

6. Commit, then tag and push the tag:

   ```shell
   git tag -a v0.2.0 -m "Typeshift 0.2.0"
   git push origin v0.2.0
   ```

   The tag sits on a commit of `main` and reads `vMAJOR.MINOR.PATCH`, with an optional prerelease part such as
   `-alpha.1`. The workflow fails for a tag on any other commit. It also fails for any other `v` tag, such as `v0.2`:
   MinVer cannot read one, and would pack the version that follows the last tag it can read.

7. Approve the `release` environment if it asks, then check the package on nuget.org and the GitHub release. If the
   GitHub release fails, rerun the failed job. The push skips packages nuget.org already has.

A tag with a hyphen, such as `v0.2.0-alpha.1`, creates a prerelease on GitHub, and nuget.org marks it prerelease
from its version.

## Breaking changes during the alpha

A 0.x release may break its users, and a prerelease says so in its version. Three rules keep a break from being a
surprise:

- Every break is listed under Changed or Removed in the changelog, with the call before and the call after.
- No break goes unseen: `PublicApiSurfaceTests` compares the public API with
  `tests/Typeshift.Generators.Tests/Snapshots/PublicApi.verified.txt`, so each change is a line of a diff someone
  accepted and a reviewer read.
- A break in what generated code calls raises the stamp version (D7). A project built with the earlier version then
  gets [TSH020](../guide/diagnostics.md#tsh020) when your app builds, where it would fail when your app starts.

## Compatibility from the first beta

0.1.0-alpha.8 is the last release that may break its users (D91). Every `1.0.0-beta.N`, 1.0 and every 1.x release
only adds, and these hold it to that:

- **The package's API.** In the first commit after the tag of 0.1.0-alpha.8, set `PackageValidationBaselineVersion`
  in `src/Typeshift/Typeshift.csproj` to `0.1.0-alpha.8`, so every pack fails on a removed or changed member. After
  1.0 it names the latest stable release. `PublicApiSurfaceTests` still shows each addition as a diff someone
  accepted.
- **What generated code binds to.** `KnownTypes.StampVersion` stays at 25. `Typeshift.Generated`, `IStorable<TSelf>`
  and `Agent.Declaration` only gain members, so a project built with an earlier 1.x keeps working beside a later
  package. A fact a stamp must carry from then on is an optional property, which older stamps leave unset.
- **Interfaces your app implements.** A store, a middleware, a rule or a connector gains a member only with a default
  body. `IAgentHost` is implemented by Typeshift alone and grows the same way.
- **Diagnostics.** A new diagnostic is an error only for code that would fail at run time anyway; anything else starts
  as a warning. A severity never rises, and no number is ever given to another diagnostic.
- **Stored data.** Every document 0.1.0-alpha.8 writes reads in every later version. A new member is optional, and a
  change of shape raises the document's schema version while the earlier one still reads.
- **Experimental API.** A member marked `[Experimental]`, such as the model policy, may change in a minor release, with
  a line under Changed in the changelog. The mark comes off once the API held against real providers.

## A dry run

Run the `release` workflow by hand, under *Actions > release > Run workflow*. It runs everything CI runs on the
branch and uploads the packages as an artifact, but publishes nothing. The checks of the tag are skipped, since a run
by hand has none.

## Related

- [Contributing](./index.md): building and testing before you tag.
- [Changelog](https://github.com/weboost-at/Typeshift/blob/main/CHANGELOG.md): what each release changed.
