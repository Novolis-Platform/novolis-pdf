<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-pdf">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Rendering.Skia

SkiaSharp raster adapter for a parsed page. Callers in MAUI or a console host take PNG bytes or paint onto an existing canvas. This package is the only PDF assembly that references Skia.

## Install

```bash
dotnet add package Novolis.Pdf.Rendering.Skia
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering.Skia;

await using var source = PdfSources.OpenFile(@"C:\Users\frank\Documents\sample.pdf");
var document = await PdfParser.ParseAsync(source);
var page = await new PdfSkiaPageRenderer().RenderAsync(document, new PdfRenderRequest(0, 144));
await File.WriteAllBytesAsync("page0.png", page.PngBytes);
```

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Rendering` | View transforms without Skia |
| `Novolis.Maui.PdfViewer` | Continuous reader chrome (MAUI, not this repo) |
| `Novolis.Documents.Skia` | Write a new book PDF (different repository) |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
