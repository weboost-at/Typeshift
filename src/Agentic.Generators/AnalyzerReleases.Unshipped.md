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
AGT008 | Agentic | Error | Tool name is not unique
AGT009 | Agentic | Warning | Tool has no description
AGT011 | Agentic | Error | Tool takes its settings as a parameter
AGT013 | Agentic | Warning | Tool relies on XML docs that are not generated
AGT014 | Agentic | Warning | Referenced assembly was not compiled with the generator
AGT015 | Agentic | Error | Tool name is not lower_snake_case
