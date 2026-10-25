namespace Novolis.Pdf.Parsing;

/// <summary>Known PDF names used by the typed reader.</summary>
public static class PdfNames
{
    /// <summary>Document catalog root.</summary>
    public static PdfName Root { get; } = new("Root");

    /// <summary>Document information dictionary.</summary>
    public static PdfName Info { get; } = new("Info");

    /// <summary>Object type.</summary>
    public static PdfName Type { get; } = new("Type");

    /// <summary>Catalog page tree.</summary>
    public static PdfName Pages { get; } = new("Pages");

    /// <summary>Leaf page type.</summary>
    public static PdfName Page { get; } = new("Page");

    /// <summary>Page-tree children.</summary>
    public static PdfName Kids { get; } = new("Kids");

    /// <summary>Page or outline count.</summary>
    public static PdfName Count { get; } = new("Count");

    /// <summary>Page media box.</summary>
    public static PdfName MediaBox { get; } = new("MediaBox");

    /// <summary>Page crop box.</summary>
    public static PdfName CropBox { get; } = new("CropBox");

    /// <summary>Page contents.</summary>
    public static PdfName Contents { get; } = new("Contents");

    /// <summary>Page or node resources.</summary>
    public static PdfName Resources { get; } = new("Resources");

    /// <summary>Page rotation.</summary>
    public static PdfName Rotate { get; } = new("Rotate");

    /// <summary>Page user unit.</summary>
    public static PdfName UserUnit { get; } = new("UserUnit");

    /// <summary>Page parent.</summary>
    public static PdfName Parent { get; } = new("Parent");

    /// <summary>Outline root.</summary>
    public static PdfName Outlines { get; } = new("Outlines");

    /// <summary>First child.</summary>
    public static PdfName First { get; } = new("First");

    /// <summary>Next sibling.</summary>
    public static PdfName Next { get; } = new("Next");

    /// <summary>Title string.</summary>
    public static PdfName Title { get; } = new("Title");

    /// <summary>Author string.</summary>
    public static PdfName Author { get; } = new("Author");

    /// <summary>Subject string.</summary>
    public static PdfName Subject { get; } = new("Subject");

    /// <summary>Creator string.</summary>
    public static PdfName Creator { get; } = new("Creator");

    /// <summary>Producer string.</summary>
    public static PdfName Producer { get; } = new("Producer");

    /// <summary>Language string.</summary>
    public static PdfName Lang { get; } = new("Lang");

    /// <summary>Explicit destination.</summary>
    public static PdfName Dest { get; } = new("Dest");

    /// <summary>Named or explicit destination dictionary.</summary>
    public static PdfName Dests { get; } = new("Dests");

    /// <summary>Name tree or name-tree values.</summary>
    public static PdfName Names { get; } = new("Names");

    /// <summary>Annotation list.</summary>
    public static PdfName Annots { get; } = new("Annots");

    /// <summary>Annotation or font subtype.</summary>
    public static PdfName Subtype { get; } = new("Subtype");

    /// <summary>Annotation rectangle.</summary>
    public static PdfName Rect { get; } = new("Rect");

    /// <summary>Annotation action.</summary>
    public static PdfName Action { get; } = new("A");

    /// <summary>Action destination.</summary>
    public static PdfName Destination { get; } = new("D");

    /// <summary>URI action target.</summary>
    public static PdfName Uri { get; } = new("URI");

    /// <summary>Link annotation subtype.</summary>
    public static PdfName Link { get; } = new("Link");

    /// <summary>Stream filter.</summary>
    public static PdfName Filter { get; } = new("Filter");

    /// <summary>Stream decode parameters.</summary>
    public static PdfName DecodeParms { get; } = new("DecodeParms");

    /// <summary>Document encryption dictionary.</summary>
    public static PdfName Encrypt { get; } = new("Encrypt");

    /// <summary>Encryption handler name.</summary>
    public static PdfName Standard { get; } = new("Standard");

    /// <summary>Encryption algorithm version.</summary>
    public static PdfName V { get; } = new("V");

    /// <summary>Encryption revision.</summary>
    public static PdfName R { get; } = new("R");

    /// <summary>Owner encryption entry.</summary>
    public static PdfName O { get; } = new("O");

    /// <summary>User encryption entry.</summary>
    public static PdfName U { get; } = new("U");

    /// <summary>Owner-encrypted file key.</summary>
    public static PdfName OE { get; } = new("OE");

    /// <summary>User-encrypted file key.</summary>
    public static PdfName UE { get; } = new("UE");

    /// <summary>Encrypted permissions block.</summary>
    public static PdfName Perms { get; } = new("Perms");

    /// <summary>Permissions encryption entry.</summary>
    public static PdfName P { get; } = new("P");

    /// <summary>Encryption key length in bits.</summary>
    public static PdfName KeyLength { get; } = new("Length");

    /// <summary>Encryption metadata flag.</summary>
    public static PdfName EncryptMetadata { get; } = new("EncryptMetadata");

    /// <summary>Crypt filter dictionary.</summary>
    public static PdfName CF { get; } = new("CF");

    /// <summary>Stream crypt filter.</summary>
    public static PdfName StmF { get; } = new("StmF");

    /// <summary>String crypt filter.</summary>
    public static PdfName StrF { get; } = new("StrF");

    /// <summary>Crypt filter method.</summary>
    public static PdfName CFM { get; } = new("CFM");

    /// <summary>Trailer file identifier array.</summary>
    public static PdfName ID { get; } = new("ID");

    /// <summary>Stream length.</summary>
    public static PdfName Length { get; } = new("Length");

    /// <summary>Previous xref offset.</summary>
    public static PdfName Prev { get; } = new("Prev");

    /// <summary>Hybrid-reference xref stream offset.</summary>
    public static PdfName XRefStm { get; } = new("XRefStm");

    /// <summary>Trailer or xref size.</summary>
    public static PdfName Size { get; } = new("Size");

    /// <summary>Xref stream field widths.</summary>
    public static PdfName W { get; } = new("W");

    /// <summary>Xref stream subsection index.</summary>
    public static PdfName Index { get; } = new("Index");

    /// <summary>Object-stream object count.</summary>
    public static PdfName N { get; } = new("N");

    /// <summary>Catalog type value.</summary>
    public static PdfName Catalog { get; } = new("Catalog");

    /// <summary>Xref stream type value.</summary>
    public static PdfName XRef { get; } = new("XRef");

    /// <summary>Object stream type value.</summary>
    public static PdfName ObjStm { get; } = new("ObjStm");

    /// <summary>Font resource dictionary.</summary>
    public static PdfName Font { get; } = new("Font");

    /// <summary>ToUnicode CMap stream.</summary>
    public static PdfName ToUnicode { get; } = new("ToUnicode");

    /// <summary>Font encoding.</summary>
    public static PdfName Encoding { get; } = new("Encoding");

    /// <summary>Base simple-font encoding.</summary>
    public static PdfName BaseEncoding { get; } = new("BaseEncoding");

    /// <summary>Simple-font encoding differences.</summary>
    public static PdfName Differences { get; } = new("Differences");

    /// <summary>CID to glyph mapping.</summary>
    public static PdfName CidToGidMap { get; } = new("CIDToGIDMap");

    /// <summary>Type 0 font subtype.</summary>
    public static PdfName Type0 { get; } = new("Type0");

    /// <summary>Identity-H encoding.</summary>
    public static PdfName IdentityH { get; } = new("Identity-H");

    /// <summary>Identity-V encoding.</summary>
    public static PdfName IdentityV { get; } = new("Identity-V");

    /// <summary>Type 0 descendant fonts.</summary>
    public static PdfName DescendantFonts { get; } = new("DescendantFonts");

    /// <summary>Simple-font widths.</summary>
    public static PdfName Widths { get; } = new("Widths");

    /// <summary>CID-font default width.</summary>
    public static PdfName Dw { get; } = new("DW");

    /// <summary>XObject resource dictionary.</summary>
    public static PdfName XObject { get; } = new("XObject");

    /// <summary>Form XObject subtype.</summary>
    public static PdfName Form { get; } = new("Form");

    /// <summary>Image XObject subtype.</summary>
    public static PdfName Image { get; } = new("Image");

    /// <summary>Form or text matrix.</summary>
    public static PdfName Matrix { get; } = new("Matrix");

    /// <summary>Image width.</summary>
    public static PdfName Width { get; } = new("Width");

    /// <summary>Image height.</summary>
    public static PdfName Height { get; } = new("Height");

    /// <summary>Image color space.</summary>
    public static PdfName ColorSpace { get; } = new("ColorSpace");

    /// <summary>Image mask flag.</summary>
    public static PdfName ImageMask { get; } = new("ImageMask");

    /// <summary>Image sample decode array.</summary>
    public static PdfName Decode { get; } = new("Decode");

    /// <summary>First encoded character.</summary>
    public static PdfName FirstChar { get; } = new("FirstChar");

    /// <summary>Font descriptor dictionary.</summary>
    public static PdfName FontDescriptor { get; } = new("FontDescriptor");

    /// <summary>Missing-width metric.</summary>
    public static PdfName MissingWidth { get; } = new("MissingWidth");

    /// <summary>PostScript font name.</summary>
    public static PdfName BaseFont { get; } = new("BaseFont");

    /// <summary>Simple embedded font program.</summary>
    public static PdfName FontFile { get; } = new("FontFile");

    /// <summary>Embedded TrueType font program.</summary>
    public static PdfName FontFile2 { get; } = new("FontFile2");

    /// <summary>Embedded CFF or OpenType font program.</summary>
    public static PdfName FontFile3 { get; } = new("FontFile3");

    /// <summary>Flate filter.</summary>
    public static PdfName FlateDecode { get; } = new("FlateDecode");

    /// <summary>Flate filter abbreviation.</summary>
    public static PdfName Fl { get; } = new("Fl");

    /// <summary>ASCII hex filter.</summary>
    public static PdfName AsciiHexDecode { get; } = new("ASCIIHexDecode");

    /// <summary>ASCII hex filter abbreviation.</summary>
    public static PdfName AHx { get; } = new("AHx");

    /// <summary>ASCII85 filter.</summary>
    public static PdfName Ascii85Decode { get; } = new("ASCII85Decode");

    /// <summary>ASCII85 filter abbreviation.</summary>
    public static PdfName A85 { get; } = new("A85");

    /// <summary>Run-length filter.</summary>
    public static PdfName RunLengthDecode { get; } = new("RunLengthDecode");

    /// <summary>Run-length filter abbreviation.</summary>
    public static PdfName RL { get; } = new("RL");

    /// <summary>LZW filter.</summary>
    public static PdfName LzwDecode { get; } = new("LZWDecode");

    /// <summary>LZW filter abbreviation.</summary>
    public static PdfName Lzw { get; } = new("LZW");

    /// <summary>Crypt filter.</summary>
    public static PdfName Crypt { get; } = new("Crypt");

    /// <summary>JPEG filter.</summary>
    public static PdfName DctDecode { get; } = new("DCTDecode");

    /// <summary>CCITT fax filter.</summary>
    public static PdfName CcittFaxDecode { get; } = new("CCITTFaxDecode");

    /// <summary>JPEG 2000 filter.</summary>
    public static PdfName JpxDecode { get; } = new("JPXDecode");

    /// <summary>JBIG2 filter.</summary>
    public static PdfName Jbig2Decode { get; } = new("JBIG2Decode");

    /// <summary>Predictor algorithm.</summary>
    public static PdfName Predictor { get; } = new("Predictor");

    /// <summary>LZW code-size transition mode.</summary>
    public static PdfName EarlyChange { get; } = new("EarlyChange");

    /// <summary>Predictor columns.</summary>
    public static PdfName Columns { get; } = new("Columns");

    /// <summary>Predictor colors.</summary>
    public static PdfName Colors { get; } = new("Colors");

    /// <summary>Predictor bits per component.</summary>
    public static PdfName BitsPerComponent { get; } = new("BitsPerComponent");
}
