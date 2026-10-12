# Releasing

A release is a version tag. The `release` workflow builds and tests the tagged commit, packs it, runs the package
consumer against the package, publishes the package and its symbols to nuget.org, and creates the GitHub release with
the packages attached.

## Versions

[MinVer](https://github.com/adamralph/minver) derives the version from the latest `v` tag: `v0.2.0` packs `0.2.0`,
`v0.2.0-alpha.1` packs `0.2.0-alpha.1`. Untagged commits pack as a prerelease of the next patch,
`0.2.1-alpha.0.3` for the third commit after `v0.2.0`, so a local pack never looks like a release.

## Before the first release

These need someone with admin rights on the repository and the nuget.org account.

1. **Choose the package ID.** `Agentic` belongs to another author on nuget.org. Set `PackageId` in
   `src/Agentic/Agentic.csproj`; the `buildTransitive` props are packed under the ID automatically. Then update the
   package consumer (`samples/Agentic.Samples.PackageConsumer`: its `PackageReference`, `PackageVersion` and the
   `NuGet.config` source mapping), the install command in `README.md` and `docs/guide/getting-started.md`, and the
   badge in `README.md`. Namespaces may keep their names; [renaming](./renaming.md) lists everything for a full
   rename.
2. **Set up trusted publishing on nuget.org.** Under the account's *Trusted Publishing*, add a policy for the
   repository owner `weboost-at`, the repository `Agentic`, the workflow file `release.yml` and the environment
   `nuget`. Then add the repository secret `NUGET_USER` with the nuget.org user name (not the email). No API key is
   stored: each run exchanges its GitHub identity for a key that lives one hour.
3. **Protect the `nuget` environment** under *Settings > Environments*, with required reviewers or a rule that only
   `v*` tags may deploy.
4. **Turn on GitHub Pages** under *Settings > Pages*, with *GitHub Actions* as the source, so the docs workflow can
   deploy the site.

## Cutting a release

1. On `main`, with CI green: in `CHANGELOG.md`, rename `[Unreleased]` to the version and date, and open a new empty
   `[Unreleased]` above it.
2. Move the rows of `src/Agentic.Generators/AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` under a
   `## Release <version>` heading. From then on, a shipped diagnostic ID is never reused or renumbered.
3. After the first release, set `PackageValidationBaselineVersion` in `src/Agentic/Agentic.csproj` to the released
   version, so the next pack fails on a breaking change that was not meant. Keep it on the latest release.
4. Commit, then tag and push the tag:

   ```shell
   git tag -a v0.2.0 -m "Agentic 0.2.0"
   git push origin v0.2.0
   ```

5. Approve the `nuget` environment if it asks, and check the package on nuget.org and the GitHub release.

A tag with a hyphen (`v0.2.0-alpha.1`) creates a prerelease on GitHub; nuget.org marks it prerelease from its version.

## A dry run

Run the `release` workflow by hand (*Actions > release > Run workflow*). It builds, tests and packs the branch and
uploads the packages as an artifact, without publishing anything.
