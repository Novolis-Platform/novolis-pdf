using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;

namespace Novolis.Pdf.Unit;

public sealed class PdfContentStreamInterpreterTests
{
    [Test]
    public async Task Continues_after_marked_content_and_clip_path_operators()
    {
        const string content =
            "20 20 m 20 30 40 40 60 20 c S "
            + "q 0 0 200 200 re W* n "
            + "/Span << /ActualText <FEFF002B> >> BDC "
            + "BT /F1 12 Tf 20 100 Td (Hello) Tj ET EMC Q";
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildPage(content)),
            "marked-content.pdf",
            "marked-content");

        var document = await PdfParser.ParseAsync(source);
        var page = PdfPageTree.Resolve(document).Single();
        var plan = PdfContentStreamInterpreter.BuildPlan(document, page);

        await Assert.That(plan.Diagnostics).IsEmpty();
        await Assert.That(plan.Commands.OfType<PdfPathCommand>()).Count().IsEqualTo(1);
        await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text)
            .IsEqualTo("Hello");
    }

    private static byte[] BuildPage(string content)
    {
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
        };
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
}
