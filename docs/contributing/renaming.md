# Renaming

The library was renamed from `Agentic` to Typeshift (D38, [naming](./naming.md)). This page lists where each name lives
and how to rename it. Each name that code reads at runtime or compile time is declared once; the rest are names of
folders, projects and namespaces, which a search and replace covers.

Rename in this order, then run the checks at the end.

## 1. The interceptor namespace `Typeshift.Generated`

| Where | What |
|---|---|
| `src/Typeshift.Generators/Emitting/Names.cs` | `GeneratedNamespace`, the one constant the emitters use for the modules and the interceptor. |
| `src/Typeshift/buildTransitive/Typeshift.props` | The `InterceptorsNamespaces` opt-in that consumers receive. |
| `src/Typeshift/DependencyInjection/TypeshiftServiceCollectionExtensions.cs` | The exception message that tells developers to keep the opt-in. |
| `tests/Typeshift.Generators.Tests/Testing/GeneratorTestBase.cs` | The `InterceptorsNamespaces` feature of the test compilations. |
| `tests/Typeshift.Generators.Tests/Snapshots/*.verified.txt` | Regenerate with `ACCEPT_SNAPSHOTS=1` after reviewing the received files. |

## 2. The registration call `AddTypeshift()`

| Where | What |
|---|---|
| `src/Typeshift/DependencyInjection/TypeshiftServiceCollectionExtensions.cs` | The method, its class name and its exception message. |
| `src/Typeshift.Generators/Parsing/KnownTypes.cs` | `ServiceCollectionExtensions` (the class's metadata name) and `RegistrationMethod`, which the parser matches and the interceptor emits. |
| `src/Typeshift.Generators/Diagnostics/Descriptors.cs` | The TSH011 message. |
| `src/Typeshift/Tools/GeneratedToolFunction.cs` | The exception message for a container without scopes. |
| `src/Typeshift/Persistence/Stores.cs` | The exception message that names `AddTypeshiftStore<TStore, TId>()`, which section 5 renames with the root name. |
| Comments | `git grep -n AddTypeshift -- src` lists them. |
| Callers | The samples, `tests/Typeshift.Tests/Testing/Bases/HostTestBase.cs`, the registration tests and their generator sources, the interceptor snapshot (its file name too). |
| Docs | `README.md`, `AGENTS.md`, `docs/`, the package description in `src/Typeshift/Typeshift.csproj`. |

## 3. The diagnostic prefix `TSH`

| Where | What |
|---|---|
| `src/Typeshift.Generators/Diagnostics/Descriptors.cs` | The IDs, and `Category`, which is the library's name. |
| `src/Typeshift.Generators/AnalyzerReleases.Unshipped.md` | The release-tracking table; the analyzer checks it against the descriptors. |
| `tests/Typeshift.Generators.Tests/Diagnostics/DiagnosticTests.cs` | One test per ID, named after it. |
| Docs | `docs/guide/diagnostics.md` (headings and anchors), `docs/contributing/`, `README.md`. |

## 4. The generator project `Typeshift.Generators`

| Where | What |
|---|---|
| `src/Typeshift.Generators/` | The folder, the project file and the root namespace of every file in it. |
| `src/Typeshift/Typeshift.csproj` | The project reference and the packed `analyzers/dotnet/cs/Typeshift.Generators.dll` path. |
| `build/UseGenerator.props` | The analyzer project reference. |
| `tests/Typeshift.Generators.Tests/` | The folder, the project file, its namespaces and its reference. |
| `src/Typeshift.Generators/Typeshift.Generators.csproj` | `InternalsVisibleTo Typeshift.Generators.Tests`. |
| `tests/Typeshift.Tests/Testing/Conventions/TestSources.cs`, `TestConventionTests.cs` | The test project names the conventions read. |
| `Typeshift.slnx`, `AGENTS.md`, `docs/contributing/architecture.md` | Paths and prose. |

## 5. The root namespace and package `Typeshift`

| Where | What |
|---|---|
| `src/Typeshift/` | The folder, the project file (`PackageId`), `PACKAGE.md`, and the namespace of every file. |
| `src/Typeshift/buildTransitive/Typeshift.props` | Packed as `buildTransitive/$(PackageId).props`, whatever its name here; it holds the interceptor namespace. |
| `src/Typeshift.Generators/Parsing/KnownTypes.cs` | `LibraryAssembly` and the metadata names of every library type the generator recognizes. |
| `src/Typeshift.Generators/Emitting/` | The `global::Typeshift.` prefixes in generated code. |
| `src/Typeshift.Generators/Diagnostics/Descriptors.cs` | `Category`. |
| `src/Typeshift/Typeshift.csproj` | `InternalsVisibleTo Typeshift.Tests`. |
| `tests/` | The project names `Typeshift.Tests` and `Typeshift.Tests.Toolsets`, their folders, namespaces and `<Using>` items, the generator test sources and the snapshots. |
| `samples/` | The project names, `RootNamespace`, namespaces, and the package consumer's `PackageReference`, `PackageVersion` and `NuGet.config` source mapping. |
| `build/UseGenerator.props` | The project reference and the props import. |
| `.github/workflows/ci.yml` | The pack and run paths. |
| Repository files | `Typeshift.slnx`, `Directory.Build.props` (`PackageProjectUrl`, `RepositoryUrl`: the repository and the docs site), `README.md`, `AGENTS.md`, `CHANGELOG.md`, `docs/`; the repository URL in `mdsnippets.json` (`UrlPrefix`) and `docs/.vitepress/config.mts`, whose Pages base is the repository name. |

`git grep -n "Typeshift"` lists every occurrence; `git ls-files | grep Typeshift` every path.

## Checks

```bash
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet publish samples/Typeshift.Samples.Console -c Release -r linux-x64 -o artifacts/sample
./artifacts/sample/Typeshift.Samples.Console
dotnet pack src/Typeshift -c Release --no-build -o artifacts/packages
dotnet run --project samples/Typeshift.Samples.PackageConsumer -c Release
```

The package consumer is the check that matters most after a rename: it fails when the package ID, the props file name
and the interceptor namespace disagree.
