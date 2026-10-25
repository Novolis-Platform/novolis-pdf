#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property IsPackable=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:package SkiaSharp
#:package SkiaSharp.NativeAssets.Win32

using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;

var compatibilityOnly = args.Any(static argument =>
    string.Equals(argument, "--compatibility-only", StringComparison.OrdinalIgnoreCase));
var outputArgument = args.FirstOrDefault(static argument => !argument.StartsWith("-", StringComparison.Ordinal));
var output = outputArgument is not null
    ? Path.GetFullPath(outputArgument)
    : Path.Combine(Path.GetTempPath(), "novolis-pdf-stress");
Directory.CreateDirectory(output);

if (!compatibilityOnly)
{
    Write(Path.Combine(output, "many-pages-1000.pdf"), BuildManyPages(1_000));
    Write(Path.Combine(output, "font-heavy.pdf"), BuildFontHeavy(12_000));
    Write(Path.Combine(output, "image-heavy-2400.pdf"), BuildImageHeavy(2_400, 2_400));
    Write(Path.Combine(output, "oversized-page.pdf"), BuildOversizedPage());
}

Write(Path.Combine(output, "indirect-length.pdf"), BuildIndirectLength());
Write(Path.Combine(output, "rotated-crop.pdf"), BuildRotatedCrop());
Write(Path.Combine(output, "multi-content-stream.pdf"), BuildMultiContent());
Write(Path.Combine(output, "protected-r3.pdf"), BuildProtectedR3());
Write(Path.Combine(output, "protected-empty.pdf"), BuildProtectedR3(string.Empty, "Empty password PDF"));
Write(Path.Combine(output, "protected-aes256.pdf"), BuildProtectedAes256());
Write(Path.Combine(output, "protected-object-stream.pdf"), BuildProtectedObjectStream());
Write(Path.Combine(output, "jpeg-image.pdf"), BuildJpegImage());
Write(Path.Combine(output, "inline-image.pdf"), BuildInlineImage());
Write(Path.Combine(output, "monochrome-scan.pdf"), BuildMonochromeScan());
Write(Path.Combine(output, "xref-stream.pdf"), BuildXrefStreamPage());
Write(Path.Combine(output, "object-stream.pdf"), BuildObjectStreamPage());
Write(Path.Combine(output, "hybrid-xref.pdf"), BuildHybridXrefPage());
Write(Path.Combine(output, "incremental-update.pdf"), BuildIncrementalUpdate());
Write(Path.Combine(output, "font-cmap.pdf"), BuildFontCmap());

var valid = BuildSimplePage();
Write(Path.Combine(output, "malformed-truncated-xref.pdf"), valid[..^24]);
Write(Path.Combine(output, "leading-preamble.pdf"), BuildLeadingPreamble(valid));
IReadOnlyList<ManifestEntry> manifestEntries = compatibilityOnly
    ? new[]
    {
        new ManifestEntry("indirect-length.pdf", "stream-length", 1),
        new ManifestEntry("rotated-crop.pdf", "page-geometry", 1),
        new ManifestEntry("multi-content-stream.pdf", "content-arrays", 1),
        new ManifestEntry("protected-r3.pdf", "password", 1, PasswordProtected: true),
        new ManifestEntry("protected-empty.pdf", "empty-password", 1, PasswordProtected: true),
        new ManifestEntry("protected-aes256.pdf", "aes256-password", 1, PasswordProtected: true),
        new ManifestEntry("protected-object-stream.pdf", "encrypted-object-stream", 1, PasswordProtected: true),
        new ManifestEntry("jpeg-image.pdf", "jpeg-image", 1),
        new ManifestEntry("inline-image.pdf", "inline-image", 1),
        new ManifestEntry("monochrome-scan.pdf", "monochrome-scan", 1),
        new ManifestEntry("xref-stream.pdf", "xref-stream", 1),
        new ManifestEntry("object-stream.pdf", "object-stream", 1),
        new ManifestEntry("hybrid-xref.pdf", "hybrid-xref", 1),
        new ManifestEntry("incremental-update.pdf", "incremental-update", 1),
        new ManifestEntry("font-cmap.pdf", "font-cmap", 1),
        new ManifestEntry("leading-preamble.pdf", "header-recovery", 1),
        new ManifestEntry("malformed-truncated-xref.pdf", "recovery", 1),
    }
    : new[]
    {
        new ManifestEntry("many-pages-1000.pdf", "scale", 1_000),
        new ManifestEntry("font-heavy.pdf", "text", 1),
        new ManifestEntry("image-heavy-2400.pdf", "image", 1),
        new ManifestEntry("oversized-page.pdf", "limits", 1),
        new ManifestEntry("indirect-length.pdf", "stream-length", 1),
        new ManifestEntry("rotated-crop.pdf", "page-geometry", 1),
        new ManifestEntry("multi-content-stream.pdf", "content-arrays", 1),
        new ManifestEntry("protected-r3.pdf", "password", 1, PasswordProtected: true),
        new ManifestEntry("protected-empty.pdf", "empty-password", 1, PasswordProtected: true),
        new ManifestEntry("protected-aes256.pdf", "aes256-password", 1, PasswordProtected: true),
        new ManifestEntry("protected-object-stream.pdf", "encrypted-object-stream", 1, PasswordProtected: true),
        new ManifestEntry("jpeg-image.pdf", "jpeg-image", 1),
        new ManifestEntry("inline-image.pdf", "inline-image", 1),
        new ManifestEntry("monochrome-scan.pdf", "monochrome-scan", 1),
        new ManifestEntry("xref-stream.pdf", "xref-stream", 1),
        new ManifestEntry("object-stream.pdf", "object-stream", 1),
        new ManifestEntry("hybrid-xref.pdf", "hybrid-xref", 1),
        new ManifestEntry("incremental-update.pdf", "incremental-update", 1),
        new ManifestEntry("font-cmap.pdf", "font-cmap", 1),
        new ManifestEntry("leading-preamble.pdf", "header-recovery", 1),
        new ManifestEntry("malformed-truncated-xref.pdf", "recovery", 1),
    };
Write(
    Path.Combine(output, "manifest.json"),
    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
        new
        {
            version = 1,
            files = manifestEntries,
        },
        new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        })));

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

static byte[] BuildIndirectLength()
{
    return BuildClassic(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>",
            "<< /Length 5 0 R >>\nstream\nBT ET\nendstream",
            "5",
        ],
        root: 1);
}

static byte[] BuildRotatedCrop()
{
    const string content = "BT /F1 12 Tf 30 100 Td (Rotated crop) Tj ET";
    return BuildClassic(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] "
                + "/CropBox [10 20 210 320] /Rotate 90 /Contents 4 0 R "
                + "/Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ],
        root: 1);
}

static byte[] BuildMultiContent()
{
    const string first = "BT /F1 12 Tf 20 80 Td (First ";
    const string second = "stream) Tj ET";
    return BuildClassic(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
                + "/Contents [4 0 R 5 0 R] /Resources << /Font << /F1 6 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(first)} >>\nstream\n{first}\nendstream",
            $"<< /Length {Encoding.ASCII.GetByteCount(second)} >>\nstream\n{second}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ],
        root: 1);
}

static byte[] BuildJpegImage()
{
    using var bitmap = new SKBitmap(1, 1);
    bitmap.SetPixel(0, 0, SKColors.Black);
    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 100);
    var jpeg = encoded.ToArray();
    var content = "q 100 0 0 100 0 0 cm /Im1 Do Q";
    var objects = new[]
    {
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] "
            + "/Contents 4 0 R /Resources << /XObject << /Im1 5 0 R >> >> >>",
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
        $"<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB "
            + $"/BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n",
    };
    return BuildClassicWithStream(objects, jpeg, root: 1);
}

static byte[] BuildInlineImage()
{
    var pixels = new byte[]
    {
        0, 0, 0,
        255, 0, 0,
        0, 255, 0,
        0, 0, 255,
    };
    var content = Encoding.ASCII.GetBytes(
            "q 100 0 0 100 0 0 cm BI /W 2 /H 2 /BPC 8 /CS /RGB ID\n")
        .Concat(pixels)
        .Concat(Encoding.ASCII.GetBytes("\nEI Q"))
        .ToArray();
    var objects = new List<byte[]>
    {
        Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
        Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
        Encoding.ASCII.GetBytes(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>"),
        StreamBody(content),
    };
    return BuildClassicBinary(objects, root: 1, trailerSuffix: string.Empty);

    static byte[] StreamBody(byte[] data)
    {
        var output = new List<byte>();
        AppendAscii(output, $"<< /Length {data.Length} >>\nstream\n");
        output.AddRange(data);
        AppendAscii(output, "\nendstream");
        return output.ToArray();
    }
}

static byte[] BuildLeadingPreamble(byte[] valid)
{
    var preamble = Encoding.ASCII.GetBytes("% generated preamble\n");
    return preamble.Concat(valid).ToArray();
}

static byte[] BuildProtectedR3(
    string userPassword = "secret",
    string pageText = "Protected generated PDF")
{
    const string ownerPassword = "owner";
    var documentId = Encoding.ASCII.GetBytes("novolis-generated-protected");
    var ownerEntry = ComputeOwnerEntry(ownerPassword, userPassword);
    var fileKey = ComputeFileKey(userPassword, ownerEntry, documentId);
    var content = Rc4ForObject(
        Encoding.ASCII.GetBytes($"BT /F1 12 Tf 20 80 Td ({pageText}) Tj ET"),
        fileKey,
        4);
    var bodies = new List<byte[]>
    {
        Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
        Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
        Encoding.ASCII.GetBytes(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R "
                + "/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> >>"),
        StreamBody(content),
        Encoding.ASCII.GetBytes(
            $"<< /Filter /Standard /V 2 /R 3 /Length 128 /O <{Convert.ToHexString(ownerEntry)}> "
                + $"/U <{Convert.ToHexString(ComputeUserEntry(fileKey, documentId))}> /P -4 >>"),
    };
    return BuildClassicBinary(
        bodies,
        root: 1,
        trailerSuffix:
            $"/Encrypt 5 0 R /ID [<{Convert.ToHexString(documentId)}> <{Convert.ToHexString(documentId)}>]");

    static byte[] StreamBody(byte[] bytes)
    {
        var output = new List<byte>();
        AppendAscii(output, $"<< /Length {bytes.Length} >>\nstream\n");
        output.AddRange(bytes);
        AppendAscii(output, "\nendstream");
        return output.ToArray();
    }

    static byte[] Pad(string value)
    {
        var padding = PasswordPadding();
        var source = Encoding.Latin1.GetBytes(value);
        var result = new byte[padding.Length];
        var count = System.Math.Min(source.Length, result.Length);
        source.AsSpan(0, count).CopyTo(result);
        padding.AsSpan(0, result.Length - count).CopyTo(result.AsSpan(count));
        return result;
    }

    static byte[] ComputeOwnerEntry(string owner, string user)
    {
        var digest = MD5.HashData(Pad(owner));
        for (var index = 0; index < 50; index++)
            digest = MD5.HashData(digest);
        var key = digest[..16];
        var encrypted = Rc4(Pad(user), key);
        for (var index = 1; index <= 19; index++)
            encrypted = Rc4(encrypted, XorKey(key, index));
        return encrypted;
    }

    static byte[] ComputeFileKey(string user, byte[] owner, byte[] id)
    {
        var input = Pad(user)
            .Concat(owner)
            .Concat(BitConverter.GetBytes(-4))
            .Concat(id)
            .ToArray();
        var digest = MD5.HashData(input);
        for (var index = 0; index < 50; index++)
            digest = MD5.HashData(digest);
        return digest[..16];
    }

    static byte[] ComputeUserEntry(byte[] key, byte[] id)
    {
        var digest = MD5.HashData(PasswordPadding().Concat(id).ToArray());
        var encrypted = Rc4(digest, key);
        for (var index = 1; index <= 19; index++)
            encrypted = Rc4(encrypted, XorKey(key, index));
        return encrypted.Concat(new byte[16]).ToArray();
    }

    static byte[] PasswordPadding() =>
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    static byte[] XorKey(byte[] key, int value) =>
        key.Select(item => (byte)(item ^ value)).ToArray();

    static byte[] Rc4ForObject(byte[] data, byte[] fileKey, int objectNumber)
    {
        var objectKey = fileKey
            .Concat(
                new byte[]
                {
                    (byte)(objectNumber & 0xFF),
                    (byte)((objectNumber >> 8) & 0xFF),
                    (byte)((objectNumber >> 16) & 0xFF),
                    0,
                    0,
                })
            .ToArray();
        var digest = MD5.HashData(objectKey);
        return Rc4(data, digest[..System.Math.Min(16, digest.Length)]);
    }

    static byte[] Rc4(byte[] data, byte[] key)
    {
        var state = Enumerable.Range(0, 256).Select(index => (byte)index).ToArray();
        var j = 0;
        for (var index = 0; index < state.Length; index++)
        {
            j = (j + state[index] + key[index % key.Length]) & 0xFF;
            (state[index], state[j]) = (state[j], state[index]);
        }

        var output = new byte[data.Length];
        var i = 0;
        j = 0;
        for (var index = 0; index < data.Length; index++)
        {
            i = (i + 1) & 0xFF;
            j = (j + state[i]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
            output[index] = (byte)(data[index] ^ state[(state[i] + state[j]) & 0xFF]);
        }

        return output;
    }
}

static byte[] BuildProtectedAes256()
{
    const string userPassword = "secret";
    const string ownerPassword = "owner";
    var userValidationSalt = Bytes(0x11, 8);
    var userKeySalt = Bytes(0x22, 8);
    var ownerValidationSalt = Bytes(0x33, 8);
    var ownerKeySalt = Bytes(0x44, 8);
    var fileKey = Bytes(0x55, 32);
    var userEntry = SHA256.HashData(
            Encoding.UTF8.GetBytes(userPassword)
                .Concat(userValidationSalt)
                .ToArray())
        .Concat(userValidationSalt)
        .Concat(userKeySalt)
        .ToArray();
    var ownerEntry = SHA256.HashData(
            Encoding.UTF8.GetBytes(ownerPassword)
                .Concat(ownerValidationSalt)
                .Concat(userEntry)
                .ToArray())
        .Concat(ownerValidationSalt)
        .Concat(ownerKeySalt)
        .ToArray();
    var userEncryptionKey = SHA256.HashData(
        Encoding.UTF8.GetBytes(userPassword).Concat(userKeySalt).ToArray());
    var ownerEncryptionKey = SHA256.HashData(
        Encoding.UTF8.GetBytes(ownerPassword)
            .Concat(ownerKeySalt)
            .Concat(userEntry)
            .ToArray());
    var userEncryptedKey = EncryptAesZeroIv(fileKey, userEncryptionKey);
    var ownerEncryptedKey = EncryptAesZeroIv(fileKey, ownerEncryptionKey);
    var permissions = new byte[16];
    BinaryPrimitives.WriteInt32LittleEndian(permissions, -4);
    permissions[4] = 0xFF;
    permissions[5] = 0xFF;
    permissions[6] = 0xFF;
    permissions[7] = 0xFF;
    permissions[8] = (byte)'T';
    permissions[9] = (byte)'a';
    permissions[10] = (byte)'d';
    permissions[11] = (byte)'b';
    permissions[12] = 1;
    permissions[13] = 2;
    permissions[14] = 3;
    permissions[15] = 4;
    var encryptedPermissions = EncryptAesZeroIv(permissions, fileKey);
    var content = EncryptAes(
        Encoding.ASCII.GetBytes("BT /F1 12 Tf 20 80 Td (AES256 generated PDF) Tj ET"),
        fileKey,
        Bytes(0x66, 16));
    var info = EncryptAes(
        Encoding.ASCII.GetBytes("AES256 generated title"),
        fileKey,
        Bytes(0x77, 16));
    var bodies = new List<byte[]>
    {
        Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
        Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
        Encoding.ASCII.GetBytes(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
                + "/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> "
                + "/Contents 4 0 R >>"),
        StreamBody(content),
        Encoding.ASCII.GetBytes(
            $"<< /Filter /Standard /V 5 /R 5 /Length 256 /P -4 "
                + $"/O <{Convert.ToHexString(ownerEntry)}> /U <{Convert.ToHexString(userEntry)}> "
                + $"/OE <{Convert.ToHexString(ownerEncryptedKey)}> /UE <{Convert.ToHexString(userEncryptedKey)}> "
                + $"/Perms <{Convert.ToHexString(encryptedPermissions)}> "
                + "/CF << /StdCF << /Type /CryptFilter /CFM /AESV3 /Length 32 >> >> "
                + "/StmF /StdCF /StrF /StdCF >>"),
        Encoding.ASCII.GetBytes("<< /Producer (Novolis) >>"),
        Encoding.ASCII.GetBytes($"<< /Title <{Convert.ToHexString(info)}> >>"),
    };
    var documentId = Encoding.ASCII.GetBytes("novolis-generated-aes256");
    return BuildClassicDocument(bodies, documentId);

    static byte[] Bytes(byte value, int count) =>
        Enumerable.Repeat(value, count).ToArray();

    static byte[] StreamBody(byte[] encrypted)
    {
        var output = new List<byte>();
        AppendAscii(output, $"<< /Length {encrypted.Length} >>\nstream\n");
        output.AddRange(encrypted);
        AppendAscii(output, "\nendstream");
        return output.ToArray();
    }
}

static byte[] BuildProtectedObjectStream()
{
    var documentId = Encoding.ASCII.GetBytes("novolis-generated-encrypted-object-stream");
    var ownerEntry = ComputeOwnerEntry("owner", "secret");
    var fileKey = ComputeFileKey("secret", ownerEntry, documentId);
    var embedded = "7 0 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";
    var objectStream = Rc4ForObject(
        Encoding.ASCII.GetBytes(embedded),
        fileKey,
        objectNumber: 5);
    var content = Rc4ForObject(
        Encoding.ASCII.GetBytes("BT /F1 12 Tf 20 80 Td (Encrypted generated object stream) Tj ET"),
        fileKey,
        objectNumber: 6);

    var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[10];
    AppendObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
    AppendObject(output, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
    AppendObject(
        output,
        offsets,
        3,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 6 0 R "
            + "/Resources << /Font << /F1 7 0 R >> >> >>");
    AppendObject(
        output,
        offsets,
        5,
        $"<< /Type /ObjStm /N 1 /First 4 /Length {objectStream.Length} >>\nstream\n",
        objectStream,
        "\nendstream\nendobj\n");
    AppendObject(
        output,
        offsets,
        6,
        $"<< /Length {content.Length} >>\nstream\n",
        content,
        "\nendstream\nendobj\n");
    AppendObject(
        output,
        offsets,
        9,
        $"<< /Filter /Standard /V 2 /R 3 /Length 128 /O <{Convert.ToHexString(ownerEntry)}> "
            + $"/U <{Convert.ToHexString(ComputeUserEntry(fileKey, documentId))}> /P -4 >>");

    var xrefOffset = output.Count;
    var entries = new byte[10 * 7];
    WriteXrefEntry(entries, 0, 0, 0, 65535);
    WriteXrefEntry(entries, 1, 1, offsets[1], 0);
    WriteXrefEntry(entries, 2, 1, offsets[2], 0);
    WriteXrefEntry(entries, 3, 1, offsets[3], 0);
    WriteXrefEntry(entries, 4, 0, 0, 65535);
    WriteXrefEntry(entries, 5, 1, offsets[5], 0);
    WriteXrefEntry(entries, 6, 1, offsets[6], 0);
    WriteXrefEntry(entries, 7, 2, 5, 0);
    WriteXrefEntry(entries, 8, 1, xrefOffset, 0);
    WriteXrefEntry(entries, 9, 1, offsets[9], 0);
    AppendObject(
        output,
        offsets,
        8,
        $"<< /Type /XRef /Size 10 /Root 1 0 R /Encrypt 9 0 R "
            + $"/ID [<{Convert.ToHexString(documentId)}> <{Convert.ToHexString(documentId)}>] "
            + $"/W [1 4 2] /Length {entries.Length} >>\nstream\n",
        entries,
        "\nendstream\nendobj\n");
    AppendAscii(output, $"startxref\n{xrefOffset}\n%%EOF\n");
    return output.ToArray();
}

static byte[] BuildMonochromeScan()
{
    const int width = 16;
    const int height = 16;
    var pixels = new byte[width * height / 8];
    for (var row = 0; row < height; row++)
    {
        pixels[row * 2] = (byte)(row % 2 == 0 ? 0xF0 : 0x0F);
        pixels[(row * 2) + 1] = (byte)(row % 2 == 0 ? 0x0F : 0xF0);
    }

    using var compressed = new MemoryStream();
    using (var deflate = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        deflate.Write(pixels);
    var image = compressed.ToArray();
    var content = "q 160 0 0 160 20 20 cm /Im1 Do Q";
    var objects = new[]
    {
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] "
            + "/Contents 4 0 R /Resources << /XObject << /Im1 5 0 R >> >> >>",
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
        $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} "
            + $"/ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /FlateDecode /Length {image.Length} >>\nstream\n",
    };
    return BuildClassicWithStream(objects, image, root: 1);
}

static byte[] BuildXrefStreamPage()
{
    var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[7];
    AppendObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
    AppendObject(output, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
    AppendObject(
        output,
        offsets,
        3,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
            + "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");
    var content = "BT /F1 12 Tf 20 80 Td (XRef stream generated) Tj ET";
    AppendObject(
        output,
        offsets,
        4,
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
    AppendObject(output, offsets, 5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

    var xrefOffset = output.Count;
    var entries = new byte[7 * 7];
    WriteXrefEntry(entries, 0, 0, 0, 65535);
    for (var number = 1; number <= 5; number++)
        WriteXrefEntry(entries, number, 1, offsets[number], 0);
    WriteXrefEntry(entries, 6, 1, xrefOffset, 0);
    AppendObject(
        output,
        offsets,
        6,
        $"<< /Type /XRef /Size 7 /Root 1 0 R /W [1 4 2] /Length {entries.Length} >>\nstream\n",
        entries,
        "\nendstream\nendobj\n");
    AppendAscii(output, $"startxref\n{xrefOffset}\n%%EOF\n");
    return output.ToArray();
}

static byte[] BuildObjectStreamPage()
{
    var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[9];
    AppendObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
    AppendObject(output, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
    AppendObject(
        output,
        offsets,
        3,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
            + "/Contents 4 0 R /Resources << /Font << /F1 8 0 R >> >> >>");
    var content = "BT /F1 12 Tf 20 80 Td (Object stream generated) Tj ET";
    AppendObject(
        output,
        offsets,
        4,
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
    var embedded = "8 0 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";
    AppendObject(
        output,
        offsets,
        5,
        $"<< /Type /ObjStm /N 1 /First 4 /Length {Encoding.ASCII.GetByteCount(embedded)} >>\n"
            + $"stream\n{embedded}\nendstream");

    var xrefOffset = output.Count;
    var entries = new byte[9 * 7];
    WriteXrefEntry(entries, 0, 0, 0, 65535);
    WriteXrefEntry(entries, 1, 1, offsets[1], 0);
    WriteXrefEntry(entries, 2, 1, offsets[2], 0);
    WriteXrefEntry(entries, 3, 1, offsets[3], 0);
    WriteXrefEntry(entries, 4, 1, offsets[4], 0);
    WriteXrefEntry(entries, 5, 1, offsets[5], 0);
    WriteXrefEntry(entries, 6, 0, 0, 65535);
    WriteXrefEntry(entries, 7, 1, xrefOffset, 0);
    WriteXrefEntry(entries, 8, 2, 5, 0);
    AppendObject(
        output,
        offsets,
        7,
        $"<< /Type /XRef /Size 9 /Root 1 0 R /W [1 4 2] /Length {entries.Length} >>\nstream\n",
        entries,
        "\nendstream\nendobj\n");
    AppendAscii(output, $"startxref\n{xrefOffset}\n%%EOF\n");
    return output.ToArray();
}

static byte[] BuildHybridXrefPage()
{
    var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[7];
    AppendObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
    AppendObject(output, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
    AppendObject(
        output,
        offsets,
        3,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
            + "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");
    var content = "BT /F1 12 Tf 20 80 Td (Hybrid xref generated) Tj ET";
    AppendObject(
        output,
        offsets,
        4,
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
    AppendObject(output, offsets, 5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

    var xrefStreamOffset = output.Count;
    var entries = new byte[7 * 7];
    WriteXrefEntry(entries, 0, 0, 0, 65535);
    for (var number = 1; number <= 5; number++)
        WriteXrefEntry(entries, number, 1, offsets[number], 0);
    WriteXrefEntry(entries, 6, 1, xrefStreamOffset, 0);
    AppendObject(
        output,
        offsets,
        6,
        $"<< /Type /XRef /Size 7 /Root 1 0 R /W [1 4 2] /Length {entries.Length} >>\nstream\n",
        entries,
        "\nendstream\nendobj\n");

    var classicXrefOffset = output.Count;
    AppendAscii(output, "xref\n0 6\n0000000000 65535 f \n");
    for (var number = 1; number <= 5; number++)
        AppendAscii(output, $"{offsets[number]:D10} 00000 n \n");
    AppendAscii(
        output,
        $"<< /Size 7 /Root 1 0 R /XRefStm {xrefStreamOffset} >>\n"
            .Replace("<<", "trailer\n<<", StringComparison.Ordinal)
        + $"startxref\n{classicXrefOffset}\n%%EOF\n");
    return output.ToArray();
}

static byte[] BuildIncrementalUpdate()
{
    var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[5];
    AppendObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
    AppendObject(output, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
    AppendObject(
        output,
        offsets,
        3,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
            + "/Contents 4 0 R /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 "
            + "/BaseFont /Helvetica >> >> >> >>");
    var baseContent = "BT /F1 12 Tf 20 80 Td (Incremental base) Tj ET";
    AppendObject(
        output,
        offsets,
        4,
        $"<< /Length {Encoding.ASCII.GetByteCount(baseContent)} >>\nstream\n{baseContent}\nendstream");
    var baseXrefOffset = output.Count;
    AppendAscii(output, "xref\n0 5\n0000000000 65535 f \n");
    for (var number = 1; number <= 4; number++)
        AppendAscii(output, $"{offsets[number]:D10} 00000 n \n");
    AppendAscii(
        output,
        "<< /Size 5 /Root 1 0 R >>\n"
            .Replace("<<", "trailer\n<<", StringComparison.Ordinal)
        + $"startxref\n{baseXrefOffset}\n%%EOF\n");

    var infoOffset = output.Count;
    AppendAscii(output, "5 0 obj\n<< /Producer (Novolis incremental fixture) >>\nendobj\n");
    var updateXrefOffset = output.Count;
    AppendAscii(
        output,
        $"xref\n5 1\n{infoOffset:D10} 00000 n \n"
            + $"trailer\n<< /Size 6 /Root 1 0 R /Info 5 0 R /Prev {baseXrefOffset} >>\n"
            + $"startxref\n{updateXrefOffset}\n%%EOF\n");
    return output.ToArray();
}

static byte[] BuildFontCmap()
{
    const string content = "BT /F1 18 Tf 20 80 Td <41> Tj ET";
    const string cmap =
        "/CIDInit /ProcSet findresource begin\n"
        + "12 dict begin\n"
        + "begincmap\n"
        + "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n"
        + "/CMapName /Adobe-Identity-UCS def\n"
        + "/CMapType 2 def\n"
        + "1 begincodespacerange\n<00> <FF>\nendcodespacerange\n"
        + "1 beginbfchar\n<41> <0041>\nendbfchar\n"
        + "endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend";
    var objects = new[]
    {
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
            + "/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
        $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /ToUnicode 6 0 R >>",
        $"<< /Length {Encoding.ASCII.GetByteCount(cmap)} >>\nstream\n{cmap}\nendstream",
    };
    return BuildClassic(objects, root: 1);
}

static byte[] BuildClassicDocument(
    IReadOnlyList<byte[]> bodies,
    byte[] documentId)
{
    var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[bodies.Count + 1];
    for (var index = 0; index < bodies.Count; index++)
    {
        offsets[index + 1] = output.Count;
        AppendAscii(output, $"{index + 1} 0 obj\n");
        output.AddRange(bodies[index]);
        AppendAscii(output, "\nendobj\n");
    }

    var xrefOffset = output.Count;
    AppendAscii(output, $"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n");
    for (var index = 1; index < offsets.Length; index++)
        AppendAscii(output, $"{offsets[index]:D10} 00000 n \n");
    AppendAscii(
        output,
        $"trailer\n<< /Size {offsets.Length} /Root 1 0 R /Info 7 0 R /Encrypt 5 0 R "
            + $"/ID [<{Convert.ToHexString(documentId)}> <{Convert.ToHexString(documentId)}>] >>\n"
            + $"startxref\n{xrefOffset}\n%%EOF\n");
    return output.ToArray();
}

static byte[] EncryptAesZeroIv(byte[] value, byte[] key)
{
    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV = new byte[16];
    aes.Mode = CipherMode.CBC;
    aes.Padding = PaddingMode.None;
    using var encryptor = aes.CreateEncryptor();
    return encryptor.TransformFinalBlock(value, 0, value.Length);
}

static byte[] EncryptAes(byte[] value, byte[] key, byte[] iv)
{
    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV = iv;
    aes.Mode = CipherMode.CBC;
    aes.Padding = PaddingMode.PKCS7;
    using var encryptor = aes.CreateEncryptor();
    return iv.Concat(encryptor.TransformFinalBlock(value, 0, value.Length)).ToArray();
}

static byte[] ComputeOwnerEntry(string ownerPassword, string userPassword)
{
    var digest = MD5.HashData(PadPassword(ownerPassword));
    for (var index = 0; index < 50; index++)
        digest = MD5.HashData(digest);
    var key = digest[..16];
    var encrypted = Rc4(PadPassword(userPassword), key);
    for (var index = 1; index <= 19; index++)
        encrypted = Rc4(encrypted, XorKey(key, index));
    return encrypted;
}

static byte[] ComputeFileKey(
    string userPassword,
    byte[] ownerEntry,
    byte[] documentId)
{
    var input = PadPassword(userPassword)
        .Concat(ownerEntry)
        .Concat(BitConverter.GetBytes(-4))
        .Concat(documentId)
        .ToArray();
    var digest = MD5.HashData(input);
    for (var index = 0; index < 50; index++)
        digest = MD5.HashData(digest);
    return digest[..16];
}

static byte[] ComputeUserEntry(byte[] fileKey, byte[] documentId)
{
    var digest = MD5.HashData(PasswordPadding().Concat(documentId).ToArray());
    var encrypted = Rc4(digest, fileKey);
    for (var index = 1; index <= 19; index++)
        encrypted = Rc4(encrypted, XorKey(fileKey, index));
    return encrypted.Concat(new byte[16]).ToArray();
}

static byte[] PadPassword(string value)
{
    var padding = PasswordPadding();
    var result = new byte[padding.Length];
    var source = Encoding.Latin1.GetBytes(value);
    var count = Math.Min(source.Length, result.Length);
    source.AsSpan(0, count).CopyTo(result);
    padding.AsSpan(0, result.Length - count).CopyTo(result.AsSpan(count));
    return result;
}

static byte[] PasswordPadding() =>
[
    0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
    0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
    0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
    0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
];

static byte[] Rc4ForObject(byte[] data, byte[] fileKey, int objectNumber)
{
    var objectKey = fileKey
        .Concat(
            new byte[]
            {
                (byte)(objectNumber & 0xFF),
                (byte)((objectNumber >> 8) & 0xFF),
                (byte)((objectNumber >> 16) & 0xFF),
                0,
                0,
            })
        .ToArray();
    var digest = MD5.HashData(objectKey);
    return Rc4(data, digest[..Math.Min(16, digest.Length)]);
}

static byte[] Rc4(byte[] data, byte[] key)
{
    var state = Enumerable.Range(0, 256).Select(index => (byte)index).ToArray();
    var j = 0;
    for (var index = 0; index < state.Length; index++)
    {
        j = (j + state[index] + key[index % key.Length]) & 0xFF;
        (state[index], state[j]) = (state[j], state[index]);
    }

    var output = new byte[data.Length];
    var i = 0;
    j = 0;
    for (var index = 0; index < data.Length; index++)
    {
        i = (i + 1) & 0xFF;
        j = (j + state[i]) & 0xFF;
        (state[i], state[j]) = (state[j], state[i]);
        output[index] = (byte)(data[index] ^ state[(state[i] + state[j]) & 0xFF]);
    }

    return output;
}

static byte[] XorKey(byte[] key, int value) =>
    key.Select(item => (byte)(item ^ value)).ToArray();

static void AppendObject(
    List<byte> output,
    IList<int> offsets,
    int number,
    string prefix,
    byte[]? binary = null,
    string suffix = "\nendobj\n")
{
    offsets[number] = output.Count;
    AppendAscii(output, $"{number} 0 obj\n{prefix}");
    if (binary is not null)
        output.AddRange(binary);
    AppendAscii(output, suffix);
}

static void WriteXrefEntry(
    byte[] entries,
    int index,
    byte type,
    long field2,
    int field3)
{
    var offset = checked(index * 7);
    entries[offset] = type;
    BinaryPrimitives.WriteUInt32BigEndian(
        entries.AsSpan(offset + 1, 4),
        checked((uint)field2));
    BinaryPrimitives.WriteUInt16BigEndian(
        entries.AsSpan(offset + 5, 2),
        checked((ushort)field3));
}

static byte[] BuildClassicBinary(
    IReadOnlyList<byte[]> objects,
    int root,
    string trailerSuffix)
{
    var bytes = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
    var offsets = new int[objects.Count + 1];
    for (var index = 0; index < objects.Count; index++)
    {
        offsets[index + 1] = bytes.Count;
        AppendAscii(bytes, $"{index + 1} 0 obj\n");
        bytes.AddRange(objects[index]);
        AppendAscii(bytes, "\nendobj\n");
    }

    var xrefOffset = bytes.Count;
    AppendAscii(bytes, $"xref\n0 {offsets.Length}\n0000000000 65535 f \n");
    for (var index = 1; index < offsets.Length; index++)
        AppendAscii(bytes, $"{offsets[index]:D10} 00000 n \n");
    AppendAscii(
        bytes,
        $"trailer\n<< /Size {offsets.Length} /Root {root} 0 R {trailerSuffix} >>\n"
        + $"startxref\n{xrefOffset}\n%%EOF\n");
    return bytes.ToArray();
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

file sealed record ManifestEntry(
    string Name,
    string Kind,
    int Pages,
    bool PasswordProtected = false);
