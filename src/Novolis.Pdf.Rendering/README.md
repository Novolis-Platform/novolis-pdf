<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-pdf">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Rendering

Host-neutral page graphics: content-stream plans, a per-page plan cache, and view matrices that map PDF user space onto a device pane. No Skia types.

## Install

```bash
dotnet add package Novolis.Pdf.Rendering
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Rendering;

var view = PdfViewportTransform.FitPage(
    pageWidth: 612,
    pageHeight: 792,
    viewportWidth: 800,
    viewportHeight: 600,
    zoom: 1);
```

Zoom and pan change this matrix. They do not rebuild the page plan.

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Rendering.Skia` | Paint the plan to PNG or a Skia canvas |
| `Novolis.Pdf.Text` | Extract text from the same page |
| `Novolis.Math.Geometry` | Geometry primitives used by the plan |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
