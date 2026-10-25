using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Unit;

public sealed class PdfStreamDecoderTests
{
    [Test]
    public async Task DecodesLzwStream()
    {
        var dictionary = new PdfDictionaryObject(
            new Dictionary<string, PdfObject>(StringComparer.Ordinal)
            {
                [PdfNames.Filter.Value] = PdfNames.LzwDecode.ToObject(),
            });
        var stream = new PdfStreamObject(dictionary, PackCodes(256, 65, 66, 67, 257));

        var decoded = PdfStreamDecoder.Decode(stream);

        await Assert.That(Encoding.ASCII.GetString(decoded)).IsEqualTo("ABC");
    }

    [Test]
    public async Task AppliesDecodeParametersToEachFilter()
    {
        var dictionary = new PdfDictionaryObject(
            new Dictionary<string, PdfObject>(StringComparer.Ordinal)
            {
                [PdfNames.Filter.Value] = new PdfArrayObject(
                [
                    PdfNames.AsciiHexDecode.ToObject(),
                    PdfNames.FlateDecode.ToObject(),
                ]),
                [PdfNames.DecodeParms.Value] = new PdfArrayObject(
                [
                    PdfNullObject.Instance,
                    new PdfDictionaryObject(
                        new Dictionary<string, PdfObject>(StringComparer.Ordinal)
                        {
                            [PdfNames.Predictor.Value] = new PdfNumberObject(12, "12"),
                            [PdfNames.Columns.Value] = new PdfNumberObject(3, "3"),
                            [PdfNames.Colors.Value] = new PdfNumberObject(1, "1"),
                            [PdfNames.BitsPerComponent.Value] = new PdfNumberObject(8, "8"),
                        }),
                ]),
            });
        using var compressed = new MemoryStream();
        using (var deflate = new System.IO.Compression.ZLibStream(
                   compressed,
                   System.IO.Compression.CompressionLevel.Fastest,
                   leaveOpen: true))
        {
            deflate.Write([0, 1, 2, 3]);
        }

        var encoded = Encoding.ASCII.GetBytes(Convert.ToHexString(compressed.ToArray()) + ">");
        var decoded = PdfStreamDecoder.Decode(new PdfStreamObject(dictionary, encoded));

        await Assert.That(decoded).IsEquivalentTo(new byte[] { 1, 2, 3 });
    }

    [Test]
    public async Task UnsupportedFilterHasAStableFailureKind()
    {
        var dictionary = new PdfDictionaryObject(
            new Dictionary<string, PdfObject>(StringComparer.Ordinal)
            {
                [PdfNames.Filter.Value] = PdfNames.JpxDecode.ToObject(),
            });

        var failure = await Assert.ThrowsAsync<PdfReaderException>(
            () => Task.Run(() => PdfStreamDecoder.Decode(
                new PdfStreamObject(dictionary, [0x00, 0x01]))));

        await Assert.That(failure!.FailureKind)
            .IsEqualTo(PdfReaderFailureKind.UnsupportedFilter);
    }

    private static byte[] PackCodes(params int[] codes)
    {
        var output = new List<byte>();
        var pending = 0;
        var bitCount = 0;
        foreach (var code in codes)
        {
            pending = (pending << 9) | code;
            bitCount += 9;
            while (bitCount >= 8)
            {
                bitCount -= 8;
                output.Add((byte)(pending >> bitCount));
                pending &= (1 << bitCount) - 1;
            }
        }

        if (bitCount > 0)
            output.Add((byte)(pending << (8 - bitCount)));
        return output.ToArray();
    }
}
