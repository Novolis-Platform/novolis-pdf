# Design

## Position

`novolis-pdf` is an **orthogonal island** for reading arbitrary PDF files:

```text
source  →  typed object graph  →  page plan  →  text or Skia raster
```

It is not on the Math → Physics → Simulation → Gaming → Avalonia spine. The only Math dependency is `Novolis.Math.Geometry` from `Novolis.Pdf.Rendering`.

[novolis-documents](https://github.com/Novolis-Platform/novolis-documents) writes one-column books and reports. This repository does not author documents and does not replace that writer.

## Packages

```text
Novolis.Pdf.Abstractions
        ↓
Novolis.Pdf.Parsing  →  Novolis.Pdf.Core
        ↓
Novolis.Pdf.Rendering  →  Novolis.Pdf.Text
        ↓
Novolis.Pdf.Rendering.Skia

Novolis.Pdf.Documents  →  Novolis.Pdf.Platform
```

| Package | Role |
| --- | --- |
| `Novolis.Pdf.Abstractions` | Sources, diagnostics, page metadata, render records |
| `Novolis.Pdf.Parsing` | Syntax, xref, streams, typed catalog/page/font views |
| `Novolis.Pdf.Core` | Lazy session, limits, `PdfSources` |
| `Novolis.Pdf.Rendering` | Display lists, view matrices, page tree |
| `Novolis.Pdf.Rendering.Skia` | Skia paint of a cached page plan |
| `Novolis.Pdf.Text` | Spans, search, outline extraction |
| `Novolis.Pdf.Documents` | Recent files and reading position on disk |
| `Novolis.Pdf.Platform` | Activation inbox and user-data directories |

Skia types stay inside `Novolis.Pdf.Rendering.Skia`. Avalonia and MAUI stay out of this repository. `Novolis.Maui.PdfViewer` and product hosts compose the packages.

## Hard non-goals

- Third-party PDF engines (PDFium, QuestPDF, and similar)
- An Avalonia viewer
- Writing or laying out new documents (`novolis-documents`)
- Full ISO coverage of images, Type3, annotations, and CMYK in the first reader
