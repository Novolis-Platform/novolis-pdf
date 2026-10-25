namespace Novolis.Pdf.Abstractions;

/// <summary>Structured category for a reader failure.</summary>
public enum PdfReaderFailureKind
{
    /// <summary>The failure does not have a more specific category.</summary>
    Unknown,

    /// <summary>A password is required before the document can be read.</summary>
    PasswordRequired,

    /// <summary>The supplied password was rejected.</summary>
    InvalidPassword,

    /// <summary>The document uses an unsupported security handler.</summary>
    UnsupportedEncryption,

    /// <summary>The document uses an unsupported PDF feature.</summary>
    UnsupportedFeature,

    /// <summary>The document uses a stream filter this reader cannot decode.</summary>
    UnsupportedFilter,

    /// <summary>A configured resource limit was reached.</summary>
    ResourceLimit,

    /// <summary>A page could not be rasterized.</summary>
    Render,
}
