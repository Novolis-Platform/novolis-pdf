<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-pdf/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-pdf/) · [Source](https://github.com/Novolis-Platform/novolis-pdf)
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Parsing

Ground-up PDF syntax: cross-reference, indirect objects, streams, and typed views for the catalog, trailer, pages, and fonts.

## Install

```bash
dotnet add package Novolis.Pdf.Parsing
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;

await using var source = PdfSources.OpenFile(@"C:\Users\frank\Documents\sample.pdf");
PdfParsedDocument document = await PdfParser.ParseAsync(source);
```

Known names live on `PdfName` / `PdfNames`. Catalog and page access go through `PdfCatalog` and `PdfPageNode`.

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Core` | File and stream sources |
| `Novolis.Pdf.Text` | Unicode spans and outlines |
| `Novolis.Pdf.Rendering` | Display lists for painting |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
