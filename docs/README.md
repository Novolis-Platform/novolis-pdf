# Novolis.Pdf documentation

Ground-up PDF reading. This repository parses arbitrary PDF files. [novolis-documents](https://github.com/Novolis-Platform/novolis-documents) authors paged documents and writes PDFs.

| Doc | What it covers |
| --- | --- |
| [getting-started.md](getting-started.md) | Install, open a file, build and test |
| [design.md](design.md) | Package stack, boundaries, non-goals |
| [release.md](release.md) | CalVer and GitHub Packages |

## Packages

| Package | Role |
| --- | --- |
| [`Novolis.Pdf.Abstractions`](../src/Novolis.Pdf.Abstractions/README.md) | Reader contracts and records |
| [`Novolis.Pdf.Core`](../src/Novolis.Pdf.Core/README.md) | Source lifetime and session |
| [`Novolis.Pdf.Parsing`](../src/Novolis.Pdf.Parsing/README.md) | Syntax and typed object graph |
| [`Novolis.Pdf.Rendering`](../src/Novolis.Pdf.Rendering/README.md) | Page plans and view transforms |
| [`Novolis.Pdf.Rendering.Skia`](../src/Novolis.Pdf.Rendering.Skia/README.md) | Skia raster adapter |
| [`Novolis.Pdf.Text`](../src/Novolis.Pdf.Text/README.md) | Text, search, and outlines |
| [`Novolis.Pdf.Documents`](../src/Novolis.Pdf.Documents/README.md) | Recent documents and positions |
| [`Novolis.Pdf.Platform`](../src/Novolis.Pdf.Platform/README.md) | Activation and user-data paths |
