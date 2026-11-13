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

## Cutting a release

1. On `main`, with CI green: in `CHANGELOG.md`, rename `[Unreleased]` to the version and date, and open a new empty
   `[Unreleased]` above it. The release workflow fails without a section for the tag's version, and that section
   becomes the notes of the GitHub release.
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
   changelog's section, as [the alpha's rules](#breaking-changes-during-the-alpha) ask. From 0.1.0 on, also set
   `PackageValidationBaselineVersion` in `src/Typeshift/Typeshift.csproj` to the last released version, so the pack
   fails on a break nobody meant, and keep it on the latest release. It stays unset during the alpha.

4. Check the demos, which CI never builds: `dotnet build demos/Typeshift.Demos.slnx`, then run each one offline as
   `demos/README.md` shows. A demo that broke gets fixed before the tag, or the release notes say so.
5. Commit, then tag and push the tag:

   ```shell
   git tag -a v0.2.0 -m "Typeshift 0.2.0"
   git push origin v0.2.0
   ```

   The tag sits on a commit of `main` and reads `vMAJOR.MINOR.PATCH`, with an optional prerelease part such as
   `-alpha.1`. The workflow fails for a tag on any other commit. It also fails for any other `v` tag, such as `v0.2`:
   MinVer cannot read one, and would pack the version that follows the last tag it can read.

6. Approve the `release` environment if it asks, then check the package on nuget.org and the GitHub release. If the
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

## A dry run

Run the `release` workflow by hand, under *Actions > release > Run workflow*. It runs everything CI runs on the
branch and uploads the packages as an artifact, but publishes nothing. The checks of the tag are skipped, since a run
by hand has none.

## Related

- [Contributing](./index.md): building and testing before you tag.
- [Changelog](https://github.com/weboost-at/Typeshift/blob/main/CHANGELOG.md): what each release changed.
