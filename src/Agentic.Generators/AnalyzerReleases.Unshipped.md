; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AGT001 | Agentic | Error | Toolset or agent is not partial
AGT002 | Agentic | Error | Tool name or description is not a constant
AGT003 | Agentic | Error | Tool has no name
AGT004 | Agentic | Error | Tool is not a static method group
AGT005 | Agentic | Error | Settings type is not a record
AGT006 | Agentic | Error | Toolset was not compiled with the generator
AGT007 | Agentic | Error | Tool name is not unique
AGT008 | Agentic | Warning | Tool has no description
AGT009 | Agentic | Error | Tool takes its settings as a parameter
AGT010 | Agentic | Warning | Tool relies on XML docs that are not generated
AGT011 | Agentic | Warning | Referenced assembly was not compiled with the generator
AGT012 | Agentic | Error | Tool name is not lower_snake_case
AGT013 | Agentic | Error | Settings type is less accessible than its toolset
AGT014 | Agentic | Error | Configurable member has a type settings cannot store
