---
title: Releasing
description: How a version tag becomes a NuGet package and a GitHub release, and what to set up before the first one.
---

# Releasing

A release is a version tag. Pushing one runs the `release` workflow, which tests the tagged commit, checks the package
with the consumer sample, and publishes it with its symbols to nuget.org and GitHub.

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
   `[Unreleased]` above it.
2. Move the rows of `src/Typeshift.Generators/AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` under a
   `## Release <version>` heading. From then on, a shipped diagnostic ID is never reused or renumbered. A prerelease
   skips this step, because the heading takes only a plain version (RS2007), so the rules ship with the next stable
   release.
3. Set `PackageValidationBaselineVersion` in `src/Typeshift/Typeshift.csproj` to the last released version, so the
   next pack fails on a breaking change nobody meant. It is not set yet; once it is, keep it on the latest release.
4. Commit, then tag and push the tag:

   ```shell
   git tag -a v0.2.0 -m "Typeshift 0.2.0"
   git push origin v0.2.0
   ```

5. Approve the `release` environment if it asks, then check the package on nuget.org and the GitHub release. If the
   GitHub release fails, rerun the failed job. The push skips packages nuget.org already has.

A tag with a hyphen, such as `v0.2.0-alpha.1`, creates a prerelease on GitHub, and nuget.org marks it prerelease
from its version.

## A dry run

Run the `release` workflow by hand, under *Actions > release > Run workflow*. It builds, tests and packs the branch
and uploads the packages as an artifact, but publishes nothing.

## Related

- [Contributing](./index.md): building and testing before you tag.
- [Changelog](https://github.com/weboost-at/Typeshift/blob/main/CHANGELOG.md): what each release changed.
