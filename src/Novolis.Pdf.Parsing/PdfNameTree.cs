namespace Novolis.Pdf.Parsing;

/// <summary>Walks a PDF name tree without exposing raw key strings to callers.</summary>
public static class PdfNameTree
{
    /// <summary>Finds a named value in a name tree.</summary>
    public static PdfObject? Find(
        PdfParsedDocument document,
        PdfDictionaryObject? node,
        string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (node is null)
            return null;

        var values = document.ResolveArray(node.Get(PdfNames.Names));
        if (values is not null)
        {
            for (var index = 0; index + 1 < values.Items.Count; index += 2)
            {
                if (document.Resolve(values.Items[index]) is PdfStringObject key
                    && string.Equals(key.GetText(), name, StringComparison.Ordinal))
                {
                    return document.Resolve(values.Items[index + 1]);
                }
            }
        }

        var kids = document.ResolveArray(node.Get(PdfNames.Kids));
        if (kids is null)
            return null;

        foreach (var child in kids.Items)
        {
            var result = Find(document, document.ResolveDictionary(child), name);
            if (result is not null)
                return result;
        }

        return null;
    }
}
