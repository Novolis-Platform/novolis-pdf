<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-pdf">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Pdf.Documents

Local recent-document list and reading position, stored as JSON under a directory the host chooses. This package does not parse PDF bytes. Book authoring is `Novolis.Documents` in a different repository.

## Install

```bash
dotnet add package Novolis.Pdf.Documents
```

Requires .NET 10 (`net10.0`). Restore from nuget.org + GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Pdf.Documents;

var store = new JsonPdfDocumentStateStore(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "pdf-reader", "state"));
var recent = await store.LoadRecentAsync();
```

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-pdf/blob/main/docs/design.md)

## Related packages

| Package | When to use |
| --- | --- |
| `Novolis.Pdf.Platform` | Standard user-data path layout |
| `Novolis.Pdf.Parsing` | Read the file the user opened |
| `Novolis.Documents` | Write a new paged PDF |

## Support

- Docs: [novolis-pdf](https://github.com/Novolis-Platform/novolis-pdf)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-pdf/issues)
