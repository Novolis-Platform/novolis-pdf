using System.Text;
using System.Text.Json;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Novolis.Pdf.Text;
using SkiaSharp;

namespace Novolis.Pdf.Unit;

public sealed class PdfCompatibilityCorpusTests
{
    [Test]
    public async Task RequiredManifestAndFixturesArePresent()
    {
        var root = FixtureRoot();
        var manifestPath = Path.Combine(root, "manifest.json");
        await Assert.That(File.Exists(manifestPath)).IsTrue();

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var files = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        await Assert.That(files.Length).IsGreaterThanOrEqualTo(17);
        await Assert.That(manifest.RootElement.GetRawText()).DoesNotContain("secret");

        foreach (var file in files)
        {
            var name = file.GetProperty("name").GetString();
            await Assert.That(name).IsNotNull();
            var path = Path.Combine(root, name!);
            await Assert.That(File.Exists(path)).IsTrue().Because(path);
        }
    }

    [Test]
    public async Task CommonCompatibilityFixturesParseAndRasterWithoutBlankPages()
    {
        var root = FixtureRoot();
        foreach (var name in new[]
                 {
                     "indirect-length.pdf",
                     "rotated-crop.pdf",
                     "multi-content-stream.pdf",
                     "leading-preamble.pdf",
                     "jpeg-image.pdf",
                     "inline-image.pdf",
                     "monochrome-scan.pdf",
                     "xref-stream.pdf",
                     "object-stream.pdf",
                     "hybrid-xref.pdf",
                     "incremental-update.pdf",
                     "font-cmap.pdf",
                 })
        {
            var path = RequiredFixture(root, name);
            await using var source = PdfSources.OpenFile(path);
            var document = await PdfParser.ParseAsync(source);
            var pages = PdfPageTree.Resolve(document);
            await Assert.That(pages.Count).IsEqualTo(1).Because(name);
            var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);
            if (name == "indirect-length.pdf")
            {
                await Assert.That(
                        Encoding.ASCII.GetString(pages[0].Contents.Single().EncodedBytes))
                    .Contains("BT ET");
            }
            else if (name is "jpeg-image.pdf" or "inline-image.pdf" or "monochrome-scan.pdf")
            {
                await Assert.That(plan.Commands.OfType<PdfImageCommand>().Any())
                    .IsTrue()
                    .Because(name);
            }
            else
            {
                await Assert.That(plan.Commands.OfType<PdfTextCommand>().Any())
                    .IsTrue()
                    .Because(name);
            }

            if (name != "indirect-length.pdf")
            {
                var rendered = await new PdfSkiaPageRenderer().RenderAsync(
                    document,
                    new PdfRenderRequest(0, 72));
                await Assert.That(
                        rendered.Status is PdfRenderStatus.Rendered or PdfRenderStatus.Partial)
                    .IsTrue()
                    .Because(name);
                await Assert.That(rendered.PngBytes.Length).IsGreaterThan(100).Because(name);
                await Assert.That(ContainsVisibleInk(rendered.PngBytes)).IsTrue().Because(name);
            }

            if (name == "rotated-crop.pdf")
            {
                await Assert.That(pages[0].Info.DisplayWidth).IsEqualTo(300);
                await Assert.That(pages[0].Info.DisplayHeight).IsEqualTo(200);
            }

            if (name == "multi-content-stream.pdf")
            {
                await Assert.That(
                        plan.Commands.OfType<PdfTextCommand>().Single().Text)
                    .Contains("First");
                await Assert.That(
                        plan.Commands.OfType<PdfTextCommand>().Single().Text)
                    .Contains("stream");
            }

            if (name == "object-stream.pdf")
            {
                await Assert.That(document.Objects.ContainsKey(8)).IsTrue();
            }

            if (name == "incremental-update.pdf")
            {
                await Assert.That(document.Info.Producer).Contains("incremental");
            }

            if (name == "font-cmap.pdf")
            {
                var decoded = PdfFontDecoder.Decode(
                    document,
                    pages[0],
                    plan.Commands.OfType<PdfTextCommand>().Single());
                await Assert.That(decoded.Text).IsEqualTo("A");
            }
        }
    }

    [Test]
    public async Task EmptyPasswordFixtureOpensWithoutAPrompt()
    {
        var path = RequiredFixture(FixtureRoot(), "protected-empty.pdf");
        await using var source = PdfSources.OpenFile(path);
        var document = await PdfParser.ParseAsync(source);
        var pages = PdfPageTree.Resolve(document);
        var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);

        await Assert.That(document.Info.Capabilities.HasFlag(PdfDocumentCapabilities.Encrypted))
            .IsTrue();
        await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text)
            .Contains("Empty password");
    }

    [Test]
    public async Task Aes256AndEncryptedObjectStreamFixturesUnlock()
    {
        await AssertGeneratedPassword(
            "protected-aes256.pdf",
            "AES256 generated",
            "AES256 generated title");
        await AssertGeneratedPassword(
            "protected-object-stream.pdf",
            "Encrypted generated object stream");
    }

    [Test]
    public async Task ProtectedFixtureSupportsRequiredPasswordOutcomes()
    {
        var path = RequiredFixture(FixtureRoot(), "protected-r3.pdf");

        await using (var noPassword = PdfSources.OpenFile(path))
        {
            var failure = await Assert.ThrowsAsync<PdfReaderException>(
                async () => await PdfParser.ParseAsync(noPassword));
            await Assert.That(failure!.FailureKind).IsEqualTo(PdfReaderFailureKind.PasswordRequired);
        }

        var attempts = 0;
        await using (var wrongPassword = PdfSources.OpenFile(path))
        {
            var failure = await Assert.ThrowsAsync<PdfReaderException>(
                async () => await PdfParser.ParseAsync(
                    wrongPassword,
                    new PdfOpenOptions
                    {
                        MaximumPasswordAttempts = 2,
                        PasswordProvider = (_, _) =>
                        {
                            attempts++;
                            return ValueTask.FromResult<string?>("wrong");
                        },
                    }));
            await Assert.That(failure!.FailureKind).IsEqualTo(PdfReaderFailureKind.InvalidPassword);
        }

        await Assert.That(attempts).IsEqualTo(2);

        await using (var correctPassword = PdfSources.OpenFile(path))
        {
            var document = await PdfParser.ParseAsync(
                correctPassword,
                new PdfOpenOptions
                {
                    PasswordProvider = static (_, _) =>
                        ValueTask.FromResult<string?>("secret"),
                });
            var pages = PdfPageTree.Resolve(document);
            var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);

            await Assert.That(
                    document.Info.Capabilities.HasFlag(PdfDocumentCapabilities.PasswordUnlocked))
                .IsTrue();
            await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text)
                .Contains("Protected generated");
        }
    }

    [Test]
    public async Task PasswordProviderCancellationRemainsCancellation()
    {
        var path = RequiredFixture(FixtureRoot(), "protected-r3.pdf");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var source = PdfSources.OpenFile(path);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await PdfParser.ParseAsync(
                source,
                new PdfOpenOptions
                {
                    PasswordProvider = static (_, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        return ValueTask.FromResult<string?>("secret");
                    },
                },
                cancellation.Token));
    }

    [Test]
    public async Task UnsupportedSecurityHandlerHasStructuredOutcome()
    {
        var path = RequiredFixture(FixtureRoot(), "protected-r3.pdf");
        var bytes = await File.ReadAllBytesAsync(path);
        var marker = Encoding.ASCII.GetBytes("/Filter /Standard");
        var replacement = Encoding.ASCII.GetBytes("/Filter /Bogus___");
        var markerIndex = bytes.AsSpan().IndexOf(marker);
        await Assert.That(markerIndex).IsGreaterThanOrEqualTo(0);
        replacement.CopyTo(bytes.AsSpan(markerIndex));

        await using var source = PdfSources.FromStream(
            new MemoryStream(bytes, writable: false),
            "unsupported-security.pdf",
            "unsupported-security");
        var failure = await Assert.ThrowsAsync<PdfReaderException>(
            async () => await PdfParser.ParseAsync(source));

        await Assert.That(failure!.FailureKind)
            .IsEqualTo(PdfReaderFailureKind.UnsupportedEncryption);
        await Assert.That(failure!.Diagnostic.Message).DoesNotContain("secret");
    }

    private static async Task AssertGeneratedPassword(
        string fixture,
        string pageText,
        string? title = null)
    {
        var path = RequiredFixture(FixtureRoot(), fixture);
        await using var source = PdfSources.OpenFile(path);
        var document = await PdfParser.ParseAsync(
            source,
            new PdfOpenOptions
            {
                PasswordProvider = static (_, _) => ValueTask.FromResult<string?>("secret"),
            });
        var pages = PdfPageTree.Resolve(document);
        var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);

        await Assert.That(
                document.Info.Capabilities.HasFlag(PdfDocumentCapabilities.PasswordUnlocked))
            .IsTrue()
            .Because(fixture);
        await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text)
            .Contains(pageText)
            .Because(fixture);
        if (title is not null)
            await Assert.That(document.Info.Title).IsEqualTo(title);
    }

    private static string FixtureRoot() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "generated");

    private static string RequiredFixture(string root, string name)
    {
        var path = Path.Combine(root, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required PDF compatibility fixture is missing: {path}", path);
        return path;
    }

    private static bool ContainsVisibleInk(byte[] png)
    {
        using var image = SKImage.FromEncodedData(png);
        using var bitmap = SKBitmap.FromImage(image);
        var step = System.Math.Max(1, System.Math.Max(bitmap.Width, bitmap.Height) / 2048);
        for (var y = 0; y < bitmap.Height; y += step)
        for (var x = 0; x < bitmap.Width; x += step)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Alpha > 0
                && (pixel.Red < 240 || pixel.Green < 240 || pixel.Blue < 240))
                return true;
        }

        return false;
    }
}
