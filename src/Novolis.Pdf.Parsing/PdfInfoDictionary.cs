namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over the document information dictionary.</summary>
public sealed class PdfInfoDictionary
{
    /// <summary>Creates an information-dictionary view.</summary>
    public PdfInfoDictionary(PdfDictionaryObject dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        Dictionary = dictionary;
    }

    /// <summary>Underlying information dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Document title.</summary>
    public string? Title => Dictionary.GetString(PdfNames.Title)?.GetText();

    /// <summary>Document author.</summary>
    public string? Author => Dictionary.GetString(PdfNames.Author)?.GetText();

    /// <summary>Document subject.</summary>
    public string? Subject => Dictionary.GetString(PdfNames.Subject)?.GetText();

    /// <summary>Creating application.</summary>
    public string? Creator => Dictionary.GetString(PdfNames.Creator)?.GetText();

    /// <summary>Producing library or application.</summary>
    public string? Producer => Dictionary.GetString(PdfNames.Producer)?.GetText();

    /// <summary>Document language.</summary>
    public string? Language => Dictionary.GetString(PdfNames.Lang)?.GetText();
}
