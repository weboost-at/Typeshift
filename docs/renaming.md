# Renaming

The alpha's names are placeholders. This page lists where each one lives and how to rename it. Each name that code
reads at runtime or compile time is declared once; the rest are names of folders, projects and namespaces, which a
search and replace covers.

Rename in this order, then run the checks at the end.

## 1. The interceptor namespace `Agentic.Generated`

| Where | What |
|---|---|
| `src/Agentic.Generators/Emitting/Names.cs` | `GeneratedNamespace`, the one constant the emitters use for the modules and the interceptor. |
| `src/Agentic/buildTransitive/Agentic.props` | The `InterceptorsNamespaces` opt-in that consumers receive. |
| `src/Agentic/DependencyInjection/AgenticServiceCollectionExtensions.cs` | The exception message that tells developers to keep the opt-in. |
| `tests/Agentic.Generators.Tests/Testing/GeneratorTestBase.cs` | The `InterceptorsNamespaces` feature of the test compilations. |
| `tests/Agentic.Generators.Tests/Snapshots/*.verified.txt` | Regenerate with `ACCEPT_SNAPSHOTS=1` after reviewing the received files. |

## 2. The registration call `AddAgentic()`

| Where | What |
|---|---|
| `src/Agentic/DependencyInjection/AgenticServiceCollectionExtensions.cs` | The method, its class name and its exception message. |
| `src/Agentic.Generators/Parsing/KnownTypes.cs` | `ServiceCollectionExtensions` (the class's metadata name) and `RegistrationMethod`, which the parser matches and the interceptor emits. |
| `src/Agentic.Generators/Diagnostics/Descriptors.cs` | The AGT014 message. |
| `src/Agentic/Tools/GeneratedToolFunction.cs` | The exception message for a container without scopes. |
| Comments | `git grep -n AddAgentic -- src` lists them. |
| Callers | The samples, `tests/Agentic.Tests/Testing/Bases/HostTestBase.cs`, the registration tests and their generator sources, the interceptor snapshot (its file name too). |
| Docs | `README.md`, `AGENTS.md`, `docs/`, the package description in `src/Agentic/Agentic.csproj`. |

## 3. The diagnostic prefix `AGT`

| Where | What |
|---|---|
| `src/Agentic.Generators/Diagnostics/Descriptors.cs` | The IDs, and `Category`, which is the library's name. |
| `src/Agentic.Generators/AnalyzerReleases.Unshipped.md` | The release-tracking table; the analyzer checks it against the descriptors. |
| `tests/Agentic.Generators.Tests/Diagnostics/DiagnosticTests.cs` | One test per ID, named after it. |
| Docs | `docs/diagnostics.md` (headings and anchors), `docs/status.md`, `docs/decisions.md`, `README.md`. |

## 4. The generator project `Agentic.Generators`

| Where | What |
|---|---|
| `src/Agentic.Generators/` | The folder, the project file and the root namespace of every file in it. |
| `src/Agentic/Agentic.csproj` | The project reference and the packed `analyzers/dotnet/cs/Agentic.Generators.dll` path. |
| `build/UseGenerator.props` | The analyzer project reference. |
| `tests/Agentic.Generators.Tests/` | The folder, the project file, its namespaces and its reference. |
| `src/Agentic.Generators/Agentic.Generators.csproj` | `InternalsVisibleTo Agentic.Generators.Tests`. |
| `tests/Agentic.Tests/Testing/Conventions/TestSources.cs`, `TestConventionTests.cs` | The test project names the conventions read. |
| `Agentic.slnx`, `AGENTS.md`, `docs/architecture.md` | Paths and prose. |

## 5. The root namespace and package `Agentic`

| Where | What |
|---|---|
| `src/Agentic/` | The folder, the project file (`PackageId`), and the namespace of every file. |
| `src/Agentic/buildTransitive/Agentic.props` | The file name must equal the package ID, or NuGet does not import it. |
| `src/Agentic.Generators/Parsing/KnownTypes.cs` | `LibraryAssembly` and the metadata names of every library type the generator recognizes. |
| `src/Agentic.Generators/Emitting/` | The `global::Agentic.` prefixes in generated code. |
| `src/Agentic.Generators/Diagnostics/Descriptors.cs` | `Category`. |
| `src/Agentic/Agentic.csproj` | `InternalsVisibleTo Agentic.Tests`. |
| `tests/` | The project names `Agentic.Tests` and `Agentic.Tests.Toolsets`, their folders, namespaces and `<Using>` items, the generator test sources and the snapshots. |
| `samples/` | The project names, `RootNamespace`, namespaces, and the package consumer's `PackageReference`, `NuGet.config` source mapping and `Version`. |
| `build/UseGenerator.props` | The project reference and the props import. |
| `.github/workflows/ci.yml` | The pack and run paths. |
| Repository files | `Agentic.slnx`, `Directory.Build.props` (`PackageProjectUrl`, `RepositoryUrl`, which also carry the repository name `agentic-dotnet`), `README.md`, `AGENTS.md`, `CHANGELOG.md`, `docs/`. |

`git grep -n "Agentic"` lists every occurrence; `git ls-files | grep Agentic` every path.

## Checks

```bash
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet publish samples/Agentic.Samples.Console -c Release -r linux-x64 -o artifacts/sample
./artifacts/sample/Agentic.Samples.Console
dotnet pack src/Agentic -c Release --no-build -o artifacts/packages
dotnet run --project samples/Agentic.Samples.PackageConsumer -c Release
```

The package consumer is the check that matters most after a rename: it fails when the package ID, the props file name
and the interceptor namespace disagree.
