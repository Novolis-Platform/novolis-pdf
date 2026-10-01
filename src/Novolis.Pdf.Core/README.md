<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-pdf/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-pdf/) · [Source](https://github.com/Novolis-Platform/novolis-pdf)
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Core

Opens local files and streams and holds a bounded reader session. Parsing lives in `Novolis.Pdf.Parsing`.

## Install

```bash
dotnet add package Novolis.Pdf.Core
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;

await using var source = PdfSources.OpenFile(@"C:\Users\frank\Documents\sample.pdf");
var document = await PdfParser.ParseAsync(source);
```

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Parsing` | Object graph, catalog, and page tree |
| `Novolis.Pdf.Rendering.Skia` | Rasterize a page |
| `Novolis.Pdf.Platform` | User-data directories for a host |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
