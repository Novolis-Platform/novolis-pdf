using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Parsing;

/// <summary>Implements the common Standard Security Handler revisions.</summary>
internal sealed class PdfSecurityHandler
{
    private static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    private readonly byte[] _fileKey;
    private readonly int _revision;
    private readonly int _encryptionObjectNumber;
    private readonly PdfCryptMethod _streamMethod;
    private readonly PdfCryptMethod _stringMethod;

    private PdfSecurityHandler(
        byte[] fileKey,
        int revision,
        int encryptionObjectNumber,
        PdfCryptMethod streamMethod,
        PdfCryptMethod stringMethod,
        bool permissionsRestricted)
    {
        _fileKey = fileKey;
        _revision = revision;
        _encryptionObjectNumber = encryptionObjectNumber;
        _streamMethod = streamMethod;
        _stringMethod = stringMethod;
        Capabilities = PdfDocumentCapabilities.Encrypted
            | PdfDocumentCapabilities.PasswordUnlocked
            | (permissionsRestricted ? PdfDocumentCapabilities.PermissionsRestricted : PdfDocumentCapabilities.None);
    }

    /// <summary>Capabilities discovered after unlocking the document.</summary>
    public PdfDocumentCapabilities Capabilities { get; }

    /// <summary>Creates a handler, asking the caller for a password when needed.</summary>
    public static async ValueTask<PdfSecurityHandler?> CreateAsync(
        PdfDictionaryObject? dictionary,
        byte[] documentId,
        PdfOpenOptions options,
        string displayName,
        int encryptionObjectNumber,
        CancellationToken cancellationToken)
    {
        if (dictionary is null)
            return null;

        var revision = ReadRevision(dictionary);
        if (revision is < 2 or > 6)
        {
            throw CreateFailure(
                PdfReaderFailureKind.UnsupportedEncryption,
                $"The PDF uses unsupported Standard Security Handler revision {revision}.",
                "PDFSECURITY");
        }

        if (dictionary.GetName(PdfNames.Filter) is { } filter
            && !string.Equals(filter.Value, "Standard", StringComparison.Ordinal))
        {
            throw CreateFailure(
                PdfReaderFailureKind.UnsupportedEncryption,
                $"The PDF uses unsupported security handler '{filter.Value}'.",
                "PDFSECURITY");
        }

        var emptyPassword = TryUnlock(
            dictionary,
            documentId,
            string.Empty,
            revision,
            encryptionObjectNumber);
        if (emptyPassword is not null)
            return emptyPassword;

        if (options.PasswordProvider is null)
        {
            throw CreateFailure(
                PdfReaderFailureKind.PasswordRequired,
                "The PDF requires a password.",
                "PDFPASSWORD");
        }

        var previousFailure = PdfPasswordFailureReason.Required;
        for (var attempt = 1; attempt <= options.MaximumPasswordAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var password = await options.PasswordProvider(
                    new PdfPasswordRequest(displayName, attempt, previousFailure),
                    cancellationToken)
                .ConfigureAwait(false);
            if (password is null)
            {
                throw CreateFailure(
                    PdfReaderFailureKind.PasswordRequired,
                    "Password entry was cancelled.",
                    "PDFPASSWORD");
            }

            var unlocked = TryUnlock(
                dictionary,
                documentId,
                password,
                revision,
                encryptionObjectNumber);
            if (unlocked is not null)
                return unlocked;
            previousFailure = PdfPasswordFailureReason.Invalid;
        }

        throw CreateFailure(
            PdfReaderFailureKind.InvalidPassword,
            "The supplied password was not accepted.",
            "PDFPASSWORD");
    }

    /// <summary>Decrypts an object using its PDF object number and generation.</summary>
    public PdfObject DecryptObject(PdfObject value, int objectNumber, int generation)
    {
        if (objectNumber == _encryptionObjectNumber)
            return value;

        return value switch
        {
            PdfStringObject text => new PdfStringObject(
                DecryptBytes(text.Bytes, objectNumber, generation, _stringMethod),
                text.IsHexadecimal),
            PdfArrayObject array => new PdfArrayObject(
                array.Items
                    .Select(item => DecryptObject(item, objectNumber, generation))
                    .ToArray()),
            PdfDictionaryObject dictionary => new PdfDictionaryObject(
                dictionary.Values.ToDictionary(
                    pair => pair.Key,
                    pair => DecryptObject(pair.Value, objectNumber, generation),
                    StringComparer.Ordinal)),
            PdfStreamObject stream => new PdfStreamObject(
                DecryptDictionary(stream.Dictionary, objectNumber, generation),
                DecryptBytes(stream.EncodedBytes, objectNumber, generation, _streamMethod)),
            _ => value,
        };
    }

    private PdfDictionaryObject DecryptDictionary(
        PdfDictionaryObject dictionary,
        int objectNumber,
        int generation) =>
        new(
            dictionary.Values.ToDictionary(
                pair => pair.Key,
                pair => DecryptObject(pair.Value, objectNumber, generation),
                StringComparer.Ordinal));

    private byte[] DecryptBytes(
        byte[] bytes,
        int objectNumber,
        int generation,
        PdfCryptMethod method)
    {
        if (method == PdfCryptMethod.None || bytes.Length == 0)
            return bytes.ToArray();

        if (_revision >= 5)
        {
            if (method != PdfCryptMethod.Aes256)
                throw new InvalidDataException("An AES-256 PDF uses an unsupported crypt filter.");
            return DecryptAes(bytes, _fileKey);
        }

        var objectKey = BuildObjectKey(objectNumber, generation, method);
        return method switch
        {
            PdfCryptMethod.Rc4 => Rc4(bytes, objectKey),
            PdfCryptMethod.Aes128 => DecryptAes(bytes, objectKey),
            _ => throw new InvalidOperationException($"Unknown PDF crypt method '{method}'."),
        };
    }

    private byte[] BuildObjectKey(int objectNumber, int generation, PdfCryptMethod method)
    {
        var input = new byte[_fileKey.Length + 5 + (method == PdfCryptMethod.Aes128 ? 4 : 0)];
        _fileKey.CopyTo(input, 0);
        input[_fileKey.Length] = (byte)objectNumber;
        input[_fileKey.Length + 1] = (byte)(objectNumber >> 8);
        input[_fileKey.Length + 2] = (byte)(objectNumber >> 16);
        BinaryPrimitives.WriteInt16LittleEndian(
            input.AsSpan(_fileKey.Length + 3),
            checked((short)generation));
        if (method == PdfCryptMethod.Aes128)
            Encoding.ASCII.GetBytes("sAlT").CopyTo(input.AsSpan(_fileKey.Length + 5));
        using var md5 = MD5.Create();
        var digest = md5.ComputeHash(input);
        return digest[..Math.Min(_fileKey.Length + 5, 16)];
    }

    private static PdfSecurityHandler? TryUnlock(
        PdfDictionaryObject dictionary,
        byte[] documentId,
        string password,
        int revision,
        int encryptionObjectNumber)
    {
        if (revision >= 5)
            return TryUnlockModern(
                dictionary,
                password,
                revision,
                encryptionObjectNumber);

        var ownerEntry = dictionary.GetString(PdfNames.O)?.Bytes;
        var userEntry = dictionary.GetString(PdfNames.U)?.Bytes;
        if (ownerEntry is null || userEntry is null || ownerEntry.Length < 32 || userEntry.Length < 16)
            throw CreateFailure(
                PdfReaderFailureKind.UnsupportedEncryption,
                "The PDF encryption dictionary is incomplete.",
                "PDFSECURITY");

        var permissions = ReadRequiredInt(dictionary, PdfNames.P);
        var keyLength = revision == 2
            ? 5
            : Math.Clamp(
                ReadOptionalInt(dictionary, PdfNames.Length, 128) / 8,
                5,
                16);
        var encryptMetadata = dictionary.Get<PdfBooleanObject>(PdfNames.EncryptMetadata)?.Value != false;
        var streamMethod = revision < 4
            ? PdfCryptMethod.Rc4
            : ReadCryptMethod(dictionary, PdfNames.StmF);
        var stringMethod = revision < 4
            ? PdfCryptMethod.Rc4
            : ReadCryptMethod(dictionary, PdfNames.StrF);

        var userPadding = PadPassword(password);
        var fileKey = ComputeFileKey(
            userPadding,
            ownerEntry,
            permissions,
            documentId,
            encryptMetadata,
            revision,
            keyLength);
        if (MatchesUserEntry(userEntry, fileKey, documentId, revision))
        {
            return new PdfSecurityHandler(
                fileKey,
                revision,
                encryptionObjectNumber,
                streamMethod,
                stringMethod,
                permissions != -1);
        }

        var ownerKey = ComputeOwnerKey(password, revision, keyLength);
        var recoveredUserPadding = DecryptOwnerEntry(ownerEntry, ownerKey, revision);
        fileKey = ComputeFileKey(
            recoveredUserPadding,
            ownerEntry,
            permissions,
            documentId,
            encryptMetadata,
            revision,
            keyLength);
        return MatchesUserEntry(userEntry, fileKey, documentId, revision)
            ? new PdfSecurityHandler(
                fileKey,
                revision,
                encryptionObjectNumber,
                streamMethod,
                stringMethod,
                permissions != -1)
            : null;
    }

    private static PdfSecurityHandler? TryUnlockModern(
        PdfDictionaryObject dictionary,
        string password,
        int revision,
        int encryptionObjectNumber)
    {
        var ownerEntry = dictionary.GetString(PdfNames.O)?.Bytes;
        var userEntry = dictionary.GetString(PdfNames.U)?.Bytes;
        var ownerEncryptedKey = dictionary.GetString(PdfNames.OE)?.Bytes;
        var userEncryptedKey = dictionary.GetString(PdfNames.UE)?.Bytes;
        var permissionsEntry = dictionary.GetString(PdfNames.Perms)?.Bytes;
        if (ownerEntry is null
            || userEntry is null
            || ownerEncryptedKey is null
            || userEncryptedKey is null
            || permissionsEntry is null
            || ownerEntry.Length < 48
            || userEntry.Length < 48
            || ownerEncryptedKey.Length < 32
            || userEncryptedKey.Length < 32
            || permissionsEntry.Length < 16)
        {
            throw CreateFailure(
                PdfReaderFailureKind.UnsupportedEncryption,
                "The AES-256 PDF encryption dictionary is incomplete.",
                "PDFSECURITY");
        }

        var passwordBytes = PasswordBytes(password);
        byte[]? fileKey = null;
        if (MatchesModernUserPassword(passwordBytes, userEntry, revision))
        {
            fileKey = DecryptModernKey(
                passwordBytes,
                userEntry.AsSpan(40, 8).ToArray(),
                [],
                userEncryptedKey,
                revision);
        }
        else if (MatchesModernOwnerPassword(passwordBytes, ownerEntry, userEntry, revision))
        {
            fileKey = DecryptModernKey(
                passwordBytes,
                ownerEntry.AsSpan(40, 8).ToArray(),
                userEntry[..48],
                ownerEncryptedKey,
                revision);
        }

        if (fileKey is null || !MatchesModernPermissions(permissionsEntry, fileKey, dictionary))
            return null;

        var streamMethod = ReadCryptMethod(dictionary, PdfNames.StmF, PdfCryptMethod.Aes256);
        var stringMethod = ReadCryptMethod(dictionary, PdfNames.StrF, PdfCryptMethod.Aes256);
        return new PdfSecurityHandler(
            fileKey,
            revision,
            encryptionObjectNumber,
            streamMethod,
            stringMethod,
            permissionsRestricted: true);
    }

    private static bool MatchesModernUserPassword(
        byte[] password,
        byte[] userEntry,
        int revision)
    {
        var hash = ComputeModernHash(
            password,
            userEntry.AsSpan(32, 8).ToArray(),
            [],
            revision);
        return CryptographicOperations.FixedTimeEquals(
            hash.AsSpan(0, 32),
            userEntry.AsSpan(0, 32));
    }

    private static bool MatchesModernOwnerPassword(
        byte[] password,
        byte[] ownerEntry,
        byte[] userEntry,
        int revision)
    {
        var hash = ComputeModernHash(
            password,
            ownerEntry.AsSpan(32, 8).ToArray(),
            userEntry[..48],
            revision);
        return CryptographicOperations.FixedTimeEquals(
            hash.AsSpan(0, 32),
            ownerEntry.AsSpan(0, 32));
    }

    private static byte[] DecryptModernKey(
        byte[] password,
        byte[] keySalt,
        byte[] userData,
        byte[] encryptedKey,
        int revision)
    {
        var hash = ComputeModernHash(password, keySalt, userData, revision);
        return DecryptAesZeroIv(encryptedKey[..32], hash[..32]);
    }

    private static bool MatchesModernPermissions(
        byte[] permissionsEntry,
        byte[] fileKey,
        PdfDictionaryObject dictionary)
    {
        var clear = DecryptAesZeroIv(permissionsEntry[..16], fileKey);
        if (clear.Length < 12)
            return false;
        var permissions = ReadRequiredInt(dictionary, PdfNames.P);
        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, permissions);
        if (!CryptographicOperations.FixedTimeEquals(clear.AsSpan(0, 4), expected))
            return false;
        if (clear[4] != 0xFF
            || clear[5] != 0xFF
            || clear[6] != 0xFF
            || clear[7] != 0xFF)
            return false;
        var metadata = dictionary.Get<PdfBooleanObject>(PdfNames.EncryptMetadata)?.Value != false;
        if (clear[8] != (metadata ? (byte)'T' : (byte)'F'))
            return false;
        return clear[9] == (byte)'a'
            && clear[10] == (byte)'d'
            && clear[11] == (byte)'b';
    }

    private static byte[] ComputeModernHash(
        byte[] password,
        byte[] salt,
        byte[] userData,
        int revision)
    {
        var initial = password.Concat(salt).Concat(userData).ToArray();
        var key = SHA256.HashData(initial);
        if (revision == 5)
            return key;

        var round = 0;
        while (true)
        {
            round++;
            var block = password.Concat(key).Concat(userData).ToArray();
            var repeated = new byte[block.Length * 64];
            for (var index = 0; index < 64; index++)
                block.CopyTo(repeated, index * block.Length);
            var encrypted = EncryptAesCbc(
                repeated,
                key[..16],
                key[16..32]);
            var selector = encrypted
                .Take(16)
                .Aggregate(0, static (sum, value) => sum + value) % 3;
            key = selector switch
            {
                0 => SHA256.HashData(encrypted),
                1 => SHA384.HashData(encrypted),
                _ => SHA512.HashData(encrypted),
            };
            if (round >= 64 && encrypted[^1] <= round - 32)
                return key[..32];
            if (round >= 2048)
                return key[..32];
        }
    }

    private static byte[] PasswordBytes(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        return bytes.Length <= 127 ? bytes : bytes[..127];
    }

    private static byte[] EncryptAesCbc(byte[] input, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(input, 0, input.Length);
    }

    private static byte[] DecryptAesZeroIv(byte[] input, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = new byte[16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(input, 0, input.Length);
    }

    private static byte[] ComputeFileKey(
        byte[] userPadding,
        byte[] ownerEntry,
        int permissions,
        byte[] documentId,
        bool encryptMetadata,
        int revision,
        int keyLength)
    {
        using var md5 = MD5.Create();
        var input = new List<byte>(userPadding.Length + ownerEntry.Length + 24)
        {
        };
        input.AddRange(userPadding);
        input.AddRange(ownerEntry);
        Span<byte> permissionBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(permissionBytes, permissions);
        input.AddRange(permissionBytes.ToArray());
        input.AddRange(documentId);
        if (revision >= 4 && !encryptMetadata)
            input.AddRange([0xFF, 0xFF, 0xFF, 0xFF]);

        var digest = md5.ComputeHash(input.ToArray());
        if (revision >= 3)
        {
            for (var index = 0; index < 50; index++)
                digest = md5.ComputeHash(digest);
        }

        return digest[..keyLength];
    }

    private static byte[] ComputeOwnerKey(string password, int revision, int keyLength)
    {
        using var md5 = MD5.Create();
        var digest = md5.ComputeHash(PadPassword(password));
        if (revision >= 3)
        {
            for (var index = 0; index < 50; index++)
                digest = md5.ComputeHash(digest);
        }

        var key = digest[..keyLength];
        return key;
    }

    private static byte[] DecryptOwnerEntry(byte[] ownerEntry, byte[] ownerKey, int revision)
    {
        var recovered = ownerEntry.ToArray();
        if (revision == 2)
            return Rc4(recovered, ownerKey);

        for (var index = 19; index >= 1; index--)
            recovered = Rc4(recovered, XorKey(ownerKey, index));
        return Rc4(recovered, ownerKey);
    }

    private static bool MatchesUserEntry(
        byte[] userEntry,
        byte[] fileKey,
        byte[] documentId,
        int revision)
    {
        using var md5 = MD5.Create();
        if (revision == 2)
        {
            var expected = Rc4(PasswordPadding, fileKey);
            return userEntry.Length >= expected.Length
                && CryptographicOperations.FixedTimeEquals(
                    userEntry.AsSpan(0, expected.Length),
                    expected);
        }

        var digest = md5.ComputeHash(PasswordPadding.Concat(documentId).ToArray());
        digest = Rc4(digest, fileKey);
        for (var index = 1; index <= 19; index++)
            digest = Rc4(digest, XorKey(fileKey, index));
        return userEntry.Length >= 16
            && CryptographicOperations.FixedTimeEquals(userEntry.AsSpan(0, 16), digest);
    }

    private static byte[] PadPassword(string password)
    {
        var source = Encoding.Latin1.GetBytes(password);
        var result = new byte[PasswordPadding.Length];
        var copy = Math.Min(source.Length, result.Length);
        source.AsSpan(0, copy).CopyTo(result);
        if (copy < result.Length)
            PasswordPadding.AsSpan(0, result.Length - copy).CopyTo(result.AsSpan(copy));
        return result;
    }

    private static byte[] Rc4(byte[] input, byte[] key)
    {
        var state = new byte[256];
        for (var index = 0; index < state.Length; index++)
            state[index] = (byte)index;

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

    private static byte[] DecryptAes(byte[] input, byte[] key)
    {
        if (input.Length < 16 || (input.Length - 16) % 16 != 0)
            throw new InvalidDataException("Encrypted PDF data has an invalid AES length.");

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = input[..16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(input, 16, input.Length - 16);
        if (plaintext.Length == 0)
            return plaintext;
        var padding = plaintext[^1];
        if (padding is < 1 or > 16 || padding > plaintext.Length)
            throw new InvalidDataException("Encrypted PDF data has invalid AES padding.");
        for (var index = plaintext.Length - padding; index < plaintext.Length; index++)
        {
            if (plaintext[index] != padding)
                throw new InvalidDataException("Encrypted PDF data has invalid AES padding.");
        }

        return plaintext.AsSpan(0, plaintext.Length - padding).ToArray();
    }

    private static PdfCryptMethod ReadCryptMethod(
        PdfDictionaryObject dictionary,
        PdfName field,
        PdfCryptMethod fallback = PdfCryptMethod.Rc4)
    {
        var filterName = dictionary.GetName(field) is { } name
            ? name.Value
            : null;
        if (string.Equals(filterName, "Identity", StringComparison.Ordinal))
            return PdfCryptMethod.None;
        filterName ??= "StdCF";

        var cryptFilters = dictionary.GetDictionary(PdfNames.CF);
        var filter = cryptFilters?.GetDictionary(filterName);
        var method = filter?.GetName(PdfNames.CFM) is { } methodName
            ? methodName.Value
            : null;
        return method switch
        {
            null => fallback,
            "V2" => PdfCryptMethod.Rc4,
            "AESV2" => PdfCryptMethod.Aes128,
            "AESV3" => PdfCryptMethod.Aes256,
            "None" => PdfCryptMethod.None,
            _ => throw CreateFailure(
                PdfReaderFailureKind.UnsupportedEncryption,
                $"The PDF uses unsupported crypt method '{method}'.",
                "PDFSECURITY"),
        };
    }

    private static int ReadRevision(PdfDictionaryObject dictionary)
    {
        var revision = dictionary.GetNumber(PdfNames.R);
        if (revision is null
            || !double.IsFinite(revision.Value)
            || revision.Value != Math.Truncate(revision.Value)
            || revision.Value < int.MinValue
            || revision.Value > int.MaxValue)
        {
            throw CreateFailure(
                PdfReaderFailureKind.UnsupportedEncryption,
                "The PDF encryption dictionary has no valid revision.",
                "PDFSECURITY");
        }

        return (int)revision.Value;
    }

    private static int ReadRequiredInt(PdfDictionaryObject dictionary, PdfName name)
    {
        var value = dictionary.GetNumber(name);
        if (value is null
            || !double.IsFinite(value.Value)
            || value.Value != Math.Truncate(value.Value)
            || value.Value < int.MinValue
            || value.Value > int.MaxValue)
        {
            throw CreateFailure(
                PdfReaderFailureKind.UnsupportedEncryption,
                $"The PDF encryption dictionary entry {name} is invalid.",
                "PDFSECURITY");
        }

        return (int)value.Value;
    }

    private static int ReadOptionalInt(PdfDictionaryObject dictionary, PdfName name, int fallback)
    {
        var value = dictionary.GetNumber(name);
        return value is { } number
            && double.IsFinite(number)
            && number == Math.Truncate(number)
            && number >= int.MinValue
            && number <= int.MaxValue
            ? (int)number
            : fallback;
    }

    private static byte[] XorKey(byte[] key, int value)
    {
        var result = new byte[key.Length];
        for (var index = 0; index < key.Length; index++)
            result[index] = (byte)(key[index] ^ value);
        return result;
    }

    private static PdfReaderException CreateFailure(
        PdfReaderFailureKind kind,
        string message,
        string code) =>
        new(
            new PdfDiagnosticEntry(PdfDiagnosticSeverity.Error, code, message),
            kind);

    private enum PdfCryptMethod
    {
        None,
        Rc4,
        Aes128,
        Aes256,
    }
}
