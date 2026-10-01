<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-pdf/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-pdf/) · [Source](https://github.com/Novolis-Platform/novolis-pdf)
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Abstractions

UI-neutral contracts and records for reading a PDF: sources, diagnostics, page metadata, navigation, and render requests. No parser and no Skia.

## Install

```bash
dotnet add package Novolis.Pdf.Abstractions
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Abstractions;

var request = new PdfRenderRequest(pageIndex: 0, dpi: 144);
var limits = PdfLimits.Default;
```

Hosts pass `PdfRenderRequest` into a renderer. They do not parse bytes in this package.

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Parsing` | Turn a source into a typed object graph |
| `Novolis.Pdf.Core` | Open a file or stream |
| `Novolis.Pdf.Rendering` | Page plans and view matrices |
| `Novolis.Documents` | Author a new paged document (different repository) |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
