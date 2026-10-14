; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TSH001 | Typeshift | Error | Toolset or agent is not partial
TSH002 | Typeshift | Error | Name or description is not a constant
TSH003 | Typeshift | Error | Tool is not a static method group
TSH004 | Typeshift | Error | Tool parameter cannot be bound
TSH005 | Typeshift | Error | Settings type is not a record with a parameterless constructor
TSH006 | Typeshift | Error | Settings are less accessible than what exposes them
TSH007 | Typeshift | Error | Tool name is not unique
TSH008 | Typeshift | Error | Tool name is not lower_snake_case
TSH009 | Typeshift | Warning | Tool has no description
TSH010 | Typeshift | Warning | Tool does not take its settings
TSH011 | Typeshift | Warning | Tool relies on XML docs that are not generated
TSH012 | Typeshift | Error | Configurable member has a type settings cannot store
TSH013 | Typeshift | Error | Configurable member cannot be set
TSH014 | Typeshift | Error | Declaration outside the constructor
TSH015 | Typeshift | Error | Chain the generator cannot read
TSH016 | Typeshift | Error | Toolset or agent is out of the generated code's reach
TSH017 | Typeshift | Error | Toolset or agent is generic or derives from another
TSH018 | Typeshift | Error | Member clashes with a generated member
TSH019 | Typeshift | Error | Toolset was not compiled with the generator
TSH020 | Typeshift | Warning | Referenced assembly was not compiled with the generator
TSH025 | Typeshift | Error | Tool is offered twice
