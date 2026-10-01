<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-pdf/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-pdf/) · [Source](https://github.com/Novolis-Platform/novolis-pdf)
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Platform

Platform-neutral activation inbox and user-data paths for a PDF host. No MAUI, WinUI, or Android types.

## Install

```bash
dotnet add package Novolis.Pdf.Platform
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Platform;

var paths = new PdfUserDataPaths(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "pdf-reader"));
Directory.CreateDirectory(paths.StatePath);
```

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Documents` | Persist recent files under `StatePath` |
| `Novolis.Pdf.Core` | Open the activated file |
| `Novolis.Maui.PdfViewer` | Reader chrome (MAUI package, other repository) |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
