using System.IO.Compression;
using System.Text;

var output = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), "novolis-pdf-stress");
Directory.CreateDirectory(output);

Write(Path.Combine(output, "many-pages-1000.pdf"), BuildManyPages(1_000));
Write(Path.Combine(output, "font-heavy.pdf"), BuildFontHeavy(12_000));
Write(Path.Combine(output, "image-heavy-2400.pdf"), BuildImageHeavy(2_400, 2_400));
Write(Path.Combine(output, "oversized-page.pdf"), BuildOversizedPage());

var valid = BuildSimplePage();
Write(Path.Combine(output, "malformed-truncated-xref.pdf"), valid[..^24]);

Console.WriteLine($"Generated PDF stress fixtures in {output}");
foreach (var file in Directory.EnumerateFiles(output, "*.pdf").OrderBy(path => path))
    Console.WriteLine($"{Path.GetFileName(file)} {new FileInfo(file).Length:N0} bytes");

static void Write(string path, byte[] bytes) =>
    File.WriteAllBytes(path, bytes);

static byte[] BuildManyPages(int pageCount)
{
    var fontObject = 3 + (pageCount * 2);
    var objects = new List<string>
    {
        $"<< /Type /Catalog /Pages 2 0 R >>",
        $"<< /Type /Pages /Kids [{string.Join(
            " ",
            Enumerable.Range(0, pageCount).Select(index => $"{3 + (index * 2)} 0 R"))}] /Count {pageCount} >>",
    };

    for (var page = 0; page < pageCount; page++)
    {
        var pageObject = 3 + (page * 2);
        var contentObject = pageObject + 1;
        var content = $"BT /F1 12 Tf 36 744 Td (Stress page {page + 1} of {pageCount}) Tj ET";
        objects.Add(
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            + $"/Contents {contentObject} 0 R /Resources << /Font << /F1 {fontObject} 0 R >> >> >>");
        objects.Add(
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
    }

    objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
    return BuildClassic(objects, root: 1);
}

static byte[] BuildFontHeavy(int lineCount)
{
    var content = new StringBuilder();
    for (var index = 0; index < lineCount; index++)
    {
        var x = 24 + ((index % 8) * 72);
        var y = 760 - (((index / 8) % 95) * 8);
        content.Append("BT /F1 7 Tf ")
            .Append(x)
            .Append(' ')
            .Append(y)
            .Append(" Td (Text stress ")
            .Append(index + 1)
            .Append(") Tj ET\n");
    }

    var text = content.ToString();
    return BuildClassic(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(text)} >>\nstream\n{text}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ],
        root: 1);
}

static byte[] BuildImageHeavy(int width, int height)
{
    var pixels = new byte[checked(width * height * 3)];
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var offset = ((y * width) + x) * 3;
            pixels[offset] = (byte)(x * 255 / width);
            pixels[offset + 1] = (byte)(y * 255 / height);
            pixels[offset + 2] = (byte)((x + y) * 255 / (width + height));
        }
    }

    using var compressed = new MemoryStream();
    using (var deflate = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        deflate.Write(pixels);
    var image = compressed.ToArray();
    var content = "q 600 0 0 780 6 6 cm /Im1 Do Q";
    var objects = new[]
    {
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            + "/Contents 4 0 R /Resources << /XObject << /Im1 5 0 R >> >> >>",
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
        $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} "
            + $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {image.Length} >>\n"
            + "stream\n",
    };
    return BuildClassicWithStream(objects, image, root: 1);
}

static byte[] BuildOversizedPage()
{
    const string content = "BT /F1 48 Tf 1000 99000 Td (Oversized media box) Tj ET";
    return BuildClassic(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100000 100000] "
                + "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ],
        root: 1);
}

static byte[] BuildSimplePage()
{
    const string content = "BT /F1 12 Tf 36 744 Td (Simple stress page) Tj ET";
    return BuildClassic(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ],
        root: 1);
}

static byte[] BuildClassic(IReadOnlyList<string> objects, int root)
{
    var bytes = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[objects.Count + 1];
    for (var index = 0; index < objects.Count; index++)
    {
        offsets[index + 1] = bytes.Count;
        AppendAscii(bytes, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
    }

    AppendXref(bytes, offsets, root);
    return bytes.ToArray();
}

static byte[] BuildClassicWithStream(
    IReadOnlyList<string> objects,
    byte[] stream,
    int root)
{
    var bytes = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[objects.Count + 1];
    for (var index = 0; index < objects.Count; index++)
    {
        offsets[index + 1] = bytes.Count;
        AppendAscii(bytes, $"{index + 1} 0 obj\n{objects[index]}");
        if (index == objects.Count - 1)
        {
            bytes.AddRange(stream);
            AppendAscii(bytes, "\nendstream\n");
        }

        AppendAscii(bytes, "endobj\n");
    }

    AppendXref(bytes, offsets, root);
    return bytes.ToArray();
}

static void AppendXref(List<byte> bytes, IReadOnlyList<int> offsets, int root)
{
    var xrefOffset = bytes.Count;
    AppendAscii(bytes, $"xref\n0 {offsets.Count}\n");
    AppendAscii(bytes, "0000000000 65535 f \n");
    for (var index = 1; index < offsets.Count; index++)
        AppendAscii(bytes, $"{offsets[index]:D10} 00000 n \n");
    AppendAscii(
        bytes,
        $"trailer\n<< /Size {offsets.Count} /Root {root} 0 R >>\n"
        + $"startxref\n{xrefOffset}\n%%EOF\n");
}

static void AppendAscii(List<byte> bytes, string text) =>
    bytes.AddRange(Encoding.ASCII.GetBytes(text));
