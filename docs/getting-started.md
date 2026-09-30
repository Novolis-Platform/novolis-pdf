# Getting started

## Prerequisites

- .NET 10 (`net10.0`)
- NuGet sources: **nuget.org** + **GitHub Packages** (`https://nuget.pkg.github.com/Novolis-Platform/index.json`)
- Local multi-repo work: ProjectReference mode via `-p:NovolisUseProjectReferences=true` (see [platform-project-ref-mode](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/platform-project-ref-mode.md))

## Install

```bash
dotnet add package Novolis.Pdf.Parsing
dotnet add package Novolis.Pdf.Rendering.Skia
```

`Novolis.Pdf.Core`, `Novolis.Pdf.Rendering`, `Novolis.Pdf.Text`, and `Novolis.Pdf.Abstractions` come along as dependencies. Add `Novolis.Pdf.Documents` and `Novolis.Pdf.Platform` when a host stores recent files or user-data paths.

## Open and rasterize

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

## Build

```powershell
dotnet build d:\novolis\novolis-pdf\Novolis.Pdf.slnx -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-pdf\tests\Novolis.Pdf.Unit\Novolis.Pdf.Unit.csproj -p:NovolisUseProjectReferences=true
```
