; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TSH001 | Typeshift | Error | Toolset or agent is not partial
TSH002 | Typeshift | Error | Tool name or description is not a constant
TSH003 | Typeshift | Error | Tool has no name
TSH004 | Typeshift | Error | Tool is not a static method group
TSH005 | Typeshift | Error | Settings type is not a record
TSH006 | Typeshift | Error | Toolset was not compiled with the generator
TSH007 | Typeshift | Error | Tool name is not unique
TSH008 | Typeshift | Warning | Tool has no description
TSH009 | Typeshift | Warning | Tool does not take its settings
TSH010 | Typeshift | Warning | Tool relies on XML docs that are not generated
TSH011 | Typeshift | Warning | Referenced assembly was not compiled with the generator
TSH012 | Typeshift | Error | Tool name is not lower_snake_case
TSH013 | Typeshift | Error | Settings type is less accessible than its toolset
TSH014 | Typeshift | Error | Configurable member has a type settings cannot store
