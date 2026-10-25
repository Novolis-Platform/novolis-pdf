using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;

namespace Novolis.Pdf.Unit;

public sealed class PdfSecurityTests
{
    [Test]
    public async Task ProtectedDocumentRequiresPassword()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildEncryptedFixture()),
            "protected.pdf",
            "protected");

        PdfReaderException? failure = null;
        try
        {
            await PdfParser.ParseAsync(source);
        }
        catch (PdfReaderException exception)
        {
            failure = exception;
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.FailureKind).IsEqualTo(PdfReaderFailureKind.PasswordRequired);
    }

    [Test]
    public async Task CorrectPasswordDecryptsContentAndInfo()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildEncryptedFixture()),
            "protected.pdf",
            "protected");
        var document = await PdfParser.ParseAsync(
            source,
            new PdfOpenOptions
            {
                PasswordProvider = static (_, _) => ValueTask.FromResult<string?>("secret"),
            });

        await Assert.That(document.Info.Capabilities.HasFlag(PdfDocumentCapabilities.Encrypted)).IsTrue();
        await Assert.That(document.Info.Capabilities.HasFlag(PdfDocumentCapabilities.PasswordUnlocked)).IsTrue();
        await Assert.That(document.Info.Title).IsEqualTo("Protected title");
        var pages = PdfPageTree.Resolve(document);
        var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);

        await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text).IsEqualTo("Protected PDF");
        await Assert.That(plan.Diagnostics.Any(diagnostic => diagnostic.Severity == PdfDiagnosticSeverity.Error))
            .IsFalse();
    }

    [Test]
    public async Task WrongPasswordProducesStructuredFailure()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildEncryptedFixture()),
            "protected.pdf",
            "protected");
        PdfReaderException? failure = null;
        try
        {
            await PdfParser.ParseAsync(
                source,
                new PdfOpenOptions
                {
                    PasswordProvider = static (_, _) => ValueTask.FromResult<string?>("wrong"),
                });
        }
        catch (PdfReaderException exception)
        {
            failure = exception;
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.FailureKind).IsEqualTo(PdfReaderFailureKind.InvalidPassword);
    }

    [Test]
    public async Task Aes256RevisionFiveDocumentUnlocks()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildAes256Fixture()),
            "aes256.pdf",
            "aes256");
        var document = await PdfParser.ParseAsync(
            source,
            new PdfOpenOptions
            {
                PasswordProvider = static (_, _) => ValueTask.FromResult<string?>("secret"),
            });

        await Assert.That(document.Info.Capabilities.HasFlag(PdfDocumentCapabilities.Encrypted)).IsTrue();
        var pages = PdfPageTree.Resolve(document);
        var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);
        await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text).IsEqualTo("AES256 PDF");
    }

    [Test]
    public async Task EncryptedObjectStreamUnlocksEmbeddedPageResources()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildEncryptedObjectStreamFixture()),
            "encrypted-object-stream.pdf",
            "encrypted-object-stream");
        var document = await PdfParser.ParseAsync(
            source,
            new PdfOpenOptions
            {
                PasswordProvider = static (_, _) =>
                    ValueTask.FromResult<string?>("secret"),
            });

        var pages = PdfPageTree.Resolve(document);
        var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[0]);

        await Assert.That(document.Objects.ContainsKey(7)).IsTrue();
        await Assert.That(plan.Commands.OfType<PdfTextCommand>().Single().Text)
            .IsEqualTo("Encrypted object stream");
    }

    private static byte[] BuildEncryptedFixture()
    {
        const string userPassword = "secret";
        const string ownerPassword = "owner";
        var documentId = Encoding.ASCII.GetBytes("novolis-protected-fixture");
        var ownerEntry = ComputeOwnerEntry(ownerPassword, userPassword);
        var fileKey = ComputeFileKey(userPassword, ownerEntry, documentId);
        var content = Encoding.ASCII.GetBytes("BT /F1 12 Tf 20 160 Td (Protected PDF) Tj ET");
        var info = EncryptString(Encoding.ASCII.GetBytes("Protected title"), fileKey, 7);
        var encryptedContent = EncryptString(content, fileKey, 4);
        var bodies = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes(
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] " +
                "/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> " +
                "/Contents 4 0 R >>"),
            StreamBody(encryptedContent),
            Encoding.ASCII.GetBytes(
                $"<< /Filter /Standard /V 2 /R 3 /Length 128 /O <{Convert.ToHexString(ownerEntry)}> " +
                $"/U <{Convert.ToHexString(ComputeUserEntry(fileKey, documentId))}> /P -4 >>"),
            Encoding.ASCII.GetBytes("<< /Producer (Novolis) >>"),
            Encoding.ASCII.GetBytes(
                $"<< /Title <{Convert.ToHexString(info)}> >>"),
        };

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
            $"trailer\n<< /Size {offsets.Length} /Root 1 0 R /Info 7 0 R /Encrypt 5 0 R " +
            $"/ID [<{Convert.ToHexString(documentId)}> <{Convert.ToHexString(documentId)}>] >>\n" +
            $"startxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();

        static byte[] StreamBody(byte[] encrypted)
        {
            var output = new List<byte>();
            AppendAscii(output, $"<< /Length {encrypted.Length} >>\nstream\n");
            output.AddRange(encrypted);
            AppendAscii(output, "\nendstream");
            return output.ToArray();
        }

        static void AppendAscii(List<byte> output, string value) =>
            output.AddRange(Encoding.ASCII.GetBytes(value));
    }

    private static byte[] BuildEncryptedObjectStreamFixture()
    {
        var documentId = Encoding.ASCII.GetBytes("novolis-encrypted-object-stream");
        var ownerEntry = ComputeOwnerEntry("owner", "secret");
        var fileKey = ComputeFileKey("secret", ownerEntry, documentId);
        var embedded = "7 0 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";
        var objectStream = EncryptString(
            Encoding.ASCII.GetBytes(embedded),
            fileKey,
            objectNumber: 5);
        var content = EncryptString(
            Encoding.ASCII.GetBytes("BT /F1 12 Tf 20 80 Td (Encrypted object stream) Tj ET"),
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
            $"<< /Type /ObjStm /N 1 /First 4 /Length {objectStream.Length} >>\n"
                + "stream\n",
            objectStream,
            "\nendstream");
        AppendObject(
            output,
            offsets,
            6,
            $"<< /Length {content.Length} >>\nstream\n",
            content,
            "\nendstream");
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
        AppendAscii(
            output,
            $"8 0 obj\n<< /Type /XRef /Size 10 /Root 1 0 R /Encrypt 9 0 R "
                + $"/ID [<{Convert.ToHexString(documentId)}> <{Convert.ToHexString(documentId)}>] "
                + $"/W [1 4 2] /Length {entries.Length} >>\nstream\n");
        output.AddRange(entries);
        AppendAscii(output, "\nendstream\nendobj\n");
        AppendAscii(output, $"startxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();

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
            AppendAscii(output, $"{suffix}\n");
        }
    }

    private static byte[] BuildAes256Fixture()
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
            Encoding.ASCII.GetBytes("BT /F1 12 Tf 20 160 Td (AES256 PDF) Tj ET"),
            fileKey,
            Bytes(0x66, 16));
        var info = EncryptAes(
            Encoding.ASCII.GetBytes("AES256 title"),
            fileKey,
            Bytes(0x77, 16));
        var bodies = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes(
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] "
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

        var documentId = Encoding.ASCII.GetBytes("novolis-aes256-fixture");
        return BuildClassicDocument(bodies, documentId);

        static byte[] Bytes(byte value, int count) => Enumerable.Repeat(value, count).ToArray();

        static byte[] StreamBody(byte[] encrypted)
        {
            var output = new List<byte>();
            AppendAscii(output, $"<< /Length {encrypted.Length} >>\nstream\n");
            output.AddRange(encrypted);
            AppendAscii(output, "\nendstream");
            return output.ToArray();
        }

        static void AppendAscii(List<byte> output, string value) =>
            output.AddRange(Encoding.ASCII.GetBytes(value));
    }

    private static byte[] BuildClassicDocument(
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
            $"trailer\n<< /Size {offsets.Length} /Root 1 0 R /Info 7 0 R /Encrypt 5 0 R " +
            $"/ID [<{Convert.ToHexString(documentId)}> <{Convert.ToHexString(documentId)}>] >>\n" +
            $"startxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();

        static void AppendAscii(List<byte> output, string value) =>
            output.AddRange(Encoding.ASCII.GetBytes(value));
    }

    private static byte[] EncryptAesZeroIv(byte[] value, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = new byte[16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(value, 0, value.Length);
    }

    private static byte[] EncryptAes(byte[] value, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        return iv.Concat(encryptor.TransformFinalBlock(value, 0, value.Length)).ToArray();
    }

    private static byte[] ComputeOwnerEntry(string ownerPassword, string userPassword)
    {
        var digest = MD5.HashData(Pad(ownerPassword));
        for (var index = 0; index < 50; index++)
            digest = MD5.HashData(digest);
        var key = digest[..16];
        var encrypted = Rc4(Pad(userPassword), key);
        for (var index = 1; index <= 19; index++)
            encrypted = Rc4(encrypted, XorKey(key, index));
        return encrypted;
    }

    private static byte[] ComputeFileKey(
        string userPassword,
        byte[] ownerEntry,
        byte[] documentId)
    {
        var input = new List<byte>();
        input.AddRange(Pad(userPassword));
        input.AddRange(ownerEntry);
        Span<byte> permissions = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(permissions, -4);
        input.AddRange(permissions.ToArray());
        input.AddRange(documentId);
        var digest = MD5.HashData(input.ToArray());
        for (var index = 0; index < 50; index++)
            digest = MD5.HashData(digest);
        return digest[..16];
    }

    private static byte[] ComputeUserEntry(byte[] fileKey, byte[] documentId)
    {
        var digest = MD5.HashData(PasswordPadding().Concat(documentId).ToArray());
        var encrypted = Rc4(digest, fileKey);
        for (var index = 1; index <= 19; index++)
            encrypted = Rc4(encrypted, XorKey(fileKey, index));
        return encrypted.Concat(new byte[16]).ToArray();
    }

    private static byte[] EncryptString(byte[] value, byte[] fileKey, int objectNumber)
    {
        var input = new byte[fileKey.Length + 5];
        fileKey.CopyTo(input, 0);
        input[fileKey.Length] = (byte)objectNumber;
        input[fileKey.Length + 1] = (byte)(objectNumber >> 8);
        input[fileKey.Length + 2] = (byte)(objectNumber >> 16);
        BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(fileKey.Length + 3), 0);
        var key = MD5.HashData(input)[..16];
        return Rc4(value, key);
    }

    private static byte[] Pad(string value)
    {
        var bytes = new byte[32];
        var source = Encoding.Latin1.GetBytes(value);
        var copy = System.Math.Min(source.Length, bytes.Length);
        source.AsSpan(0, copy).CopyTo(bytes);
        PasswordPadding().AsSpan(0, bytes.Length - copy).CopyTo(bytes.AsSpan(copy));
        return bytes;
    }

    private static byte[] PasswordPadding() =>
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    private static byte[] Rc4(byte[] input, byte[] key)
    {
        var state = Enumerable.Range(0, 256).Select(static value => (byte)value).ToArray();
        var j = 0;
        for (var index = 0; index < state.Length; index++)
        {
            j = (j + state[index] + key[index % key.Length]) & 0xFF;
            (state[index], state[j]) = (state[j], state[index]);
        }

        var output = new byte[input.Length];
        var i = 0;
        j = 0;
        for (var index = 0; index < input.Length; index++)
        {
            i = (i + 1) & 0xFF;
            j = (j + state[i]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
            output[index] = (byte)(input[index] ^ state[(state[i] + state[j]) & 0xFF]);
        }

        return output;
    }

    private static byte[] XorKey(byte[] key, int value) =>
        key.Select(item => (byte)(item ^ value)).ToArray();

    private static void AppendAscii(List<byte> output, string value) =>
        output.AddRange(Encoding.ASCII.GetBytes(value));

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
