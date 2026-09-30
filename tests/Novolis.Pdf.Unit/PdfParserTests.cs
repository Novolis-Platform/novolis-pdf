using System.Buffers.Binary;
using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Text;

namespace Novolis.Pdf.Unit;

public sealed class PdfParserTests
{
    [Test]
    public async Task DocumentsOutlineSampleExposesChapterBookmarks()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".novolis", "artifacts", "toc-outline", "sample.pdf");
        if (!File.Exists(path))
            return;

        await using var source = PdfSources.OpenFile(path);
        var document = await PdfParser.ParseAsync(source);
        var outlines = new PdfNavigationExtractor().ExtractOutlines(document);

        await Assert.That(outlines.Count).IsGreaterThanOrEqualTo(2);
        var sectionOne = outlines.First(item => item.Title == "Section One");
        var sectionTwo = outlines.First(item => item.Title == "Section Two");
        await Assert.That(sectionOne.PageIndex).IsNotNull();
        await Assert.That(sectionTwo.PageIndex).IsNotNull();
        await Assert.That(sectionTwo.PageIndex!.Value).IsGreaterThan(sectionOne.PageIndex!.Value);
    }

    [Test]
    public async Task ParsesTypedObjectsAndClassicXref()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildFixture()),
            "fixture.pdf",
            "fixture");

        var document = await PdfParser.ParseAsync(source);

        await Assert.That(document.Objects.Count).IsEqualTo(4);
        await Assert.That(document.GetTrailer()?.RootReference?.ObjectNumber).IsEqualTo(1);
        await Assert.That(document.Objects[4].Value.Kind).IsEqualTo(PdfObjectKind.Stream);
        await Assert.That(document.Objects[4].Value).IsTypeOf<PdfStreamObject>();
    }

    [Test]
    public async Task ResolvesPageTreeAndTextCommands()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildFixture()),
            "fixture.pdf",
            "fixture");
        var document = await PdfParser.ParseAsync(source);

        var pages = PdfPageTree.Resolve(document);
        var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);

        await Assert.That(pages.Count).IsEqualTo(1);
        await Assert.That(pages[0].Info.CropBox.Width).IsEqualTo(200);
        await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text).IsEqualTo("Hello PDF");
    }

    [Test]
    public async Task AdvancesTextMatrixAfterConsecutiveTj()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildTwoTjFixture()),
            "two-tj.pdf",
            "two-tj");
        var document = await PdfParser.ParseAsync(source);
        var pages = PdfPageTree.Resolve(document);
        var texts = PdfContentStreamInterpreter.BuildPlan(document, pages[0])
            .Commands
            .OfType<PdfTextCommand>()
            .ToArray();

        await Assert.That(texts.Length).IsEqualTo(2);
        await Assert.That(texts[0].Text).IsEqualTo("Hi");
        await Assert.That(texts[1].Text).IsEqualTo("There");
        await Assert.That(texts[1].Origin.X).IsGreaterThan(texts[0].Origin.X);
    }

    [Test]
    public async Task RejectsInvalidHeader()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(Encoding.ASCII.GetBytes("not a pdf")),
            "invalid.pdf",
            "invalid");

        await Assert.That(async () => await PdfParser.ParseAsync(source))
            .Throws<PdfReaderException>();
    }

    [Test]
    public async Task TaggedObjectsAndTypedResolutionPreservePdfKinds()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildFixture()),
            "fixture.pdf",
            "fixture");
        var document = await PdfParser.ParseAsync(source);
        var root = document.GetTrailer()?.RootReference;

        await Assert.That(root?.Kind).IsEqualTo(PdfObjectKind.IndirectReference);
        await Assert.That(document.TryGetCatalog(out var catalog)).IsTrue();
        await Assert.That(catalog.Type).IsEqualTo(PdfNames.Catalog);
        await Assert.That(catalog.IsCatalog).IsTrue();
        await Assert.That(catalog.PagesReference?.Generation).IsEqualTo(0);
        await Assert.That(document.Resolve(new PdfIndirectReference(999, 0))).IsNull();
    }

    [Test]
    public async Task ParsesXrefStreamAndUsesItsTrailer()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildXrefStreamFixture()),
            "xref-stream.pdf",
            "xref-stream");

        var document = await PdfParser.ParseAsync(source);
        var pages = PdfPageTree.Resolve(document);

        await Assert.That(document.Objects.ContainsKey(6)).IsTrue();
        await Assert.That(document.GetTrailer()?.RootReference?.ObjectNumber).IsEqualTo(1);
        await Assert.That(pages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ResolvesCompressedObjectsFromObjectStream()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildObjectStreamFixture()),
            "object-stream.pdf",
            "object-stream");

        var document = await PdfParser.ParseAsync(source);

        await Assert.That(document.Objects[4].Value.Kind).IsEqualTo(PdfObjectKind.Dictionary);
        await Assert.That(document.Info.Title).IsEqualTo("Compressed info");
    }

    [Test]
    public async Task TypedCatalogAndPageNodesAvoidStringKeys()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildFixture()),
            "fixture.pdf",
            "fixture");
        var document = await PdfParser.ParseAsync(source);

        await Assert.That(document.TryGetCatalog(out var catalog)).IsTrue();
        var pages = catalog.ResolvePages();
        var kids = pages?.ResolveKids() ?? [];

        await Assert.That(pages?.IsPages).IsTrue();
        await Assert.That(kids.Count).IsEqualTo(1);
        await Assert.That(kids[0].IsPage).IsTrue();
        await Assert.That(PdfRectangleReader.TryRead(kids[0].MediaBox, out var mediaBox)).IsTrue();
        await Assert.That(mediaBox.Width).IsEqualTo(200);
        await Assert.That(mediaBox.Height).IsEqualTo(200);
    }

    internal static byte[] BuildClassicHello() => BuildFixture();

    private static byte[] BuildTwoTjFixture() =>
        BuildPage("BT /F1 12 Tf 20 160 Td (Hi) Tj (There) Tj ET");

    private static byte[] BuildFixture() =>
        BuildPage("BT /F1 12 Tf 20 160 Td (Hello PDF) Tj ET");

    private static byte[] BuildPage(string content)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>",
        };
        var bodies = objects
            .Append($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream")
            .ToArray();
        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < bodies.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
            output.Append($"{index + 1} 0 obj\n{bodies[index]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(output.ToString());
        output.Append($"xref\n0 {bodies.Length + 1}\n");
        output.Append("0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
            output.Append($"{offsets[index]:D10} 00000 n \n");
        output.Append($"trailer\n<< /Size {bodies.Length + 1} /Root 1 0 R >>\n");
        output.Append($"startxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static byte[] BuildXrefStreamFixture()
    {
        var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var offsets = new int[7];
        AppendObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        AppendObject(output, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendObject(output, offsets, 3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>");
        var content = "BT /F1 12 Tf 10 80 Td (XRef stream) Tj ET";
        AppendObject(
            output,
            offsets,
            4,
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");

        var entries = new byte[7 * 7];
        WriteXrefEntry(entries, 0, 0, 0, 65535);
        for (var objectNumber = 1; objectNumber <= 4; objectNumber++)
            WriteXrefEntry(entries, objectNumber, 1, offsets[objectNumber], 0);
        WriteXrefEntry(entries, 5, 0, 0, 65535);
        var xrefOffset = output.Count;
        var dictionary =
            $"<< /Type /XRef /Size 7 /Root 1 0 R /W [1 4 2] /Length {entries.Length} >>";
        AppendAscii(output, $"6 0 obj\n{dictionary}\nstream\n");
        output.AddRange(entries);
        AppendAscii(output, "\nendstream\nendobj\n");
        WriteXrefEntry(entries, 6, 1, xrefOffset, 0);
        var correctedStreamStart = output.Count - Encoding.ASCII.GetByteCount("endstream\nendobj\n") - entries.Length - 1;
        output.RemoveRange(correctedStreamStart, entries.Length);
        output.InsertRange(correctedStreamStart, entries);
        AppendAscii(output, $"startxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();

        void AppendAscii(List<byte> bytes, string text) =>
            bytes.AddRange(Encoding.ASCII.GetBytes(text));
    }

    private static byte[] BuildObjectStreamFixture()
    {
        var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var offsets = new int[7];
        AppendObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R /Info 4 0 R >>");
        AppendObject(output, offsets, 2, "<< /Type /Pages /Kids [] /Count 0 >>");
        var embedded = "4 0 << /Title (Compressed info) >>";
        AppendObject(
            output,
            offsets,
            5,
            $"<< /Type /ObjStm /N 1 /First 4 /Length {Encoding.ASCII.GetByteCount(embedded)} >>\nstream\n{embedded}\nendstream");

        var entries = new byte[7 * 7];
        WriteXrefEntry(entries, 0, 0, 0, 65535);
        WriteXrefEntry(entries, 1, 1, offsets[1], 0);
        WriteXrefEntry(entries, 2, 1, offsets[2], 0);
        WriteXrefEntry(entries, 3, 0, 0, 65535);
        WriteXrefEntry(entries, 4, 2, 5, 0);
        WriteXrefEntry(entries, 5, 1, offsets[5], 0);
        var xrefOffset = output.Count;
        var dictionary =
            $"<< /Type /XRef /Size 7 /Root 1 0 R /Info 4 0 R /W [1 4 2] /Length {entries.Length} >>";
        AppendAscii(output, $"6 0 obj\n{dictionary}\nstream\n");
        WriteXrefEntry(entries, 6, 1, xrefOffset, 0);
        output.AddRange(entries);
        AppendAscii(output, "\nendstream\nendobj\n");
        AppendAscii(output, $"startxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();

        void AppendAscii(List<byte> bytes, string text) =>
            bytes.AddRange(Encoding.ASCII.GetBytes(text));
    }

    private static void AppendObject(
        List<byte> output,
        IList<int> offsets,
        int number,
        string body)
    {
        offsets[number] = output.Count;
        output.AddRange(Encoding.ASCII.GetBytes($"{number} 0 obj\n{body}\nendobj\n"));
    }

    private static void WriteXrefEntry(
        byte[] entries,
        int index,
        byte type,
        long field2,
        int field3)
    {
        var offset = index * 7;
        entries[offset] = type;
        BinaryPrimitives.WriteUInt32BigEndian(
            entries.AsSpan(offset + 1, 4),
            checked((uint)field2));
        BinaryPrimitives.WriteUInt16BigEndian(
            entries.AsSpan(offset + 5, 2),
            checked((ushort)field3));
    }
}
