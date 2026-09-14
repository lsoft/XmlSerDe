; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
XMLSERDE001 | XmlSerDe | Info | A type falls back to System.Xml.Serialization.XmlSerializer instead of generated code; the message carries the reason. Raised to Warning or Error by the XmlSerDeCompatStrict build property.
XMLSERDE002 | XmlSerDe | Warning | Code generation was abandoned after the graph walk had already accepted the type — a gap in the generator rather than a refusal by design.
XMLSERDE010 | XmlSerDe | Warning | The type named in [XmlExhauster] / [XmlInjector] is not sealed, so the generated call cannot be devirtualized.
