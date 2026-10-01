<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-pdf/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-pdf/) · [Source](https://github.com/Novolis-Platform/novolis-pdf)
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Text

Page text spans, search, font decoding (including ToUnicode), and outline extraction. Geometry stays in PDF user space.

## Install

```bash
dotnet add package Novolis.Pdf.Text
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Text;

await using var source = PdfSources.OpenFile(@"C:\Users\frank\Documents\sample.pdf");
var document = await PdfParser.ParseAsync(source);
var spans = new PdfTextExtractor().ExtractPage(document, pageIndex: 0);
```

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Parsing` | Raw object graph |
| `Novolis.Pdf.Rendering.Skia` | Paint the same page |
| `Novolis.Pdf.Abstractions` | `PdfTextSpan` and `PdfSearchMatch` records |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
