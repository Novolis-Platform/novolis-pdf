namespace Novolis.Pdf.Abstractions;

/// <summary>Why a PDF password provider is being called.</summary>
public enum PdfPasswordFailureReason
{
    /// <summary>No previous attempt failed.</summary>
    None,

    /// <summary>The document requires a password.</summary>
    Required,

    /// <summary>The previous password was rejected.</summary>
    Invalid,

    /// <summary>The document uses a security handler that is not supported.</summary>
    UnsupportedEncryption,
}
