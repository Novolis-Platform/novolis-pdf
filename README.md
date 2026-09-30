# novolis-pdf

Ground-up, UI-neutral PDF reading infrastructure for Novolis products.

Repository: [Novolis-Platform/novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf). Docs: [getting started](docs/getting-started.md), [design](docs/design.md), [release](docs/release.md).

This repository is deliberately separate from `novolis-documents`. The existing
document repository authors predictable paged documents and writes PDFs; this
repository reads arbitrary PDF files for local viewers.

## Packages

| Package | Purpose |
| --- | --- |
| `Novolis.Pdf.Abstractions` | Reader contracts, page metadata, diagnostics, navigation, and render records |
| `Novolis.Pdf.Core` | Lazy document lifetime, limits, object cache, and reader orchestration |
| `Novolis.Pdf.Parsing` | PDF syntax, cross-reference, indirect-object, and stream parsing |
| `Novolis.Pdf.Rendering` | UI-neutral page graphics and render-target contracts |
| `Novolis.Pdf.Rendering.Skia` | Contained Skia raster adapter for page output |
| `Novolis.Pdf.Text` | Text spans, search, selection geometry, and navigation extraction |
| `Novolis.Pdf.Documents` | Local recent-document and reading-position records |
| `Novolis.Pdf.Platform` | Platform-neutral activation, local-storage, and document-source contracts |

Parsed documents are a typed object union (`PdfObjectKind` plus records), not
string dictionaries. Known keys and name values live on `PdfName` / `PdfNames`.
Catalog, trailer, page-tree, font, outline, and annotation access go through
typed views (`PdfCatalog`, `PdfTrailer`, `PdfPageNode`, `PdfFontResource`) so
hosts resolve `/Root` and `/Pages` without raw key lookups.

The reader core has no Avalonia or MAUI dependency. The first presentation
surface is `Novolis.Maui.PdfViewer`; no Avalonia PDF viewer is part of this
repository or the product scope.

## Policy

- No third-party PDF parser or viewer engine.
- Platform and low-level graphics dependencies stay behind Novolis package
  boundaries where practical.
- User documents and reader state remain local to the host.
- Unsupported constructs produce structured diagnostics rather than silent
  corruption.

## Build

```powershell
dotnet build d:\novolis\novolis-pdf\Novolis.Pdf.slnx -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-pdf\tests\Novolis.Pdf.Unit\Novolis.Pdf.Unit.csproj -p:NovolisUseProjectReferences=true
```
