# Release

## Versioning

CalVer via `d:\novolis\novolis-pdf\build\version.json` (`2026.1.*`). Packable projects share the repo stamp.

## Publish path

Maintainers ship on **`main`** (no maintainer PR for normal work):

1. Commit + `git push origin main`
2. `merge.yml` builds, tests, packs, and publishes to **GitHub Packages**
3. Consumers restore from nuget.org + `https://nuget.pkg.github.com/Novolis-Platform/index.json`

Do **not** publish via a local folder feed. See [nuget-only-policy](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/nuget-only-policy.md).

## Local verification before push

```powershell
dotnet test d:\novolis\novolis-pdf\tests\Novolis.Pdf.Unit\Novolis.Pdf.Unit.csproj -p:NovolisUseProjectReferences=true
pwsh -File d:\novolis\novolis-governance\scripts\verify-nuget-only.ps1
```

## Governance

- [release.md](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/release.md)
- [contribution-policy.md](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/contribution-policy.md)
- [documentation-policy.md](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/documentation-policy.md)
