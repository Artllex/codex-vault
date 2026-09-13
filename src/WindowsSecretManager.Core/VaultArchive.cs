using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WindowsSecretManager.Core;

public sealed record VaultArchiveItem(string Name, string Value);

public sealed record VaultArchiveContents(
    IReadOnlyList<VaultArchiveItem> Items,
    DateTimeOffset CreatedAt,
    bool PasswordProtected);

public sealed record VaultArchiveInfo(bool PasswordProtected, int? ItemCount);

public static class VaultArchive
{
    private const int FormatVersion = 1;
    private const int KdfIterations = 600_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    public const int MaximumSizeBytes = 16 * 1024 * 1024;
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("CodexVault/v1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Create(IEnumerable<VaultArchiveItem> items, string? password)
    {
        ArgumentNullException.ThrowIfNull(items);
        var materialized = items.ToArray();
        ValidateItems(materialized);

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(
            new ArchivePayload(FormatVersion, DateTimeOffset.UtcNow, materialized), JsonOptions);
        try
        {
            if (string.IsNullOrEmpty(password))
            {
                return JsonSerializer.SerializeToUtf8Bytes(
                    new ArchiveEnvelope(FormatVersion, "none", materialized.Length, null, null, null, null,
                        Convert.ToBase64String(payloadBytes)), JsonOptions);
            }

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipherText = new byte[payloadBytes.Length];
            var key = DeriveKey(password, salt, KdfIterations);
            try
            {
                using var aes = new AesGcm(key);
                aes.Encrypt(nonce, payloadBytes, cipherText, tag, AssociatedData);
                return JsonSerializer.SerializeToUtf8Bytes(
                    new ArchiveEnvelope(FormatVersion, "aes-256-gcm", materialized.Length, KdfIterations,
                        Convert.ToBase64String(salt), Convert.ToBase64String(nonce),
                        Convert.ToBase64String(tag), Convert.ToBase64String(cipherText)), JsonOptions);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(cipherText);
                CryptographicOperations.ZeroMemory(tag);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payloadBytes);
        }
    }

    public static VaultArchiveContents Open(ReadOnlySpan<byte> archiveBytes, string? password)
    {
        if (archiveBytes.Length == 0 || archiveBytes.Length > MaximumSizeBytes)
            throw new InvalidDataException("The archive is empty or too large.");

        ArchiveEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ArchiveEnvelope>(archiveBytes, JsonOptions)
                ?? throw new InvalidDataException("The archive header is missing.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("This is not a valid Codex Vault archive.", ex);
        }

        if (envelope.Version != FormatVersion)
            throw new InvalidDataException("This archive version is not supported.");

        byte[] payloadBytes;
        var passwordProtected = string.Equals(envelope.Encryption, "aes-256-gcm", StringComparison.Ordinal);
        if (string.Equals(envelope.Encryption, "none", StringComparison.Ordinal))
        {
            payloadBytes = Decode(envelope.Payload, "payload");
        }
        else if (passwordProtected)
        {
            if (string.IsNullOrEmpty(password))
                throw new UnauthorizedAccessException("This archive requires a password.");
            if (envelope.Iterations != KdfIterations)
                throw new InvalidDataException("The archive uses unsupported key-derivation settings.");

            var salt = Decode(envelope.Salt, "salt", SaltSize);
            var nonce = Decode(envelope.Nonce, "nonce", NonceSize);
            var tag = Decode(envelope.Tag, "authentication tag", TagSize);
            var cipherText = Decode(envelope.Payload, "payload");
            payloadBytes = new byte[cipherText.Length];
            var key = DeriveKey(password, salt, envelope.Iterations.Value);
            try
            {
                using var aes = new AesGcm(key);
                aes.Decrypt(nonce, cipherText, tag, payloadBytes, AssociatedData);
            }
            catch (CryptographicException ex)
            {
                CryptographicOperations.ZeroMemory(payloadBytes);
                throw new UnauthorizedAccessException("The password is incorrect or the archive is damaged.", ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(cipherText);
                CryptographicOperations.ZeroMemory(tag);
            }
        }
        else
        {
            throw new InvalidDataException("The archive uses an unsupported encryption method.");
        }

        try
        {
            ArchivePayload payload;
            try
            {
                payload = JsonSerializer.Deserialize<ArchivePayload>(payloadBytes, JsonOptions)
                    ?? throw new InvalidDataException("The archive contents are missing.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("The archive contents are damaged.", ex);
            }
            if (payload.Version != FormatVersion)
                throw new InvalidDataException("The archive contents use an unsupported version.");
            ValidateItems(payload.Items);
            if (envelope.ItemCount is not null && envelope.ItemCount != payload.Items.Length)
                throw new InvalidDataException("The archive entry count does not match its contents.");
            return new VaultArchiveContents(payload.Items, payload.CreatedAt, passwordProtected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payloadBytes);
        }
    }

    public static VaultArchiveInfo Inspect(ReadOnlySpan<byte> archiveBytes)
    {
        if (archiveBytes.Length == 0 || archiveBytes.Length > MaximumSizeBytes)
            throw new InvalidDataException("The archive is empty or too large.");
        try
        {
            var envelope = JsonSerializer.Deserialize<ArchiveEnvelope>(archiveBytes, JsonOptions)
                ?? throw new InvalidDataException("The archive header is missing.");
            if (envelope.Version != FormatVersion)
                throw new InvalidDataException("This archive version is not supported.");
            if (envelope.ItemCount is < 0 or > 10_000)
                throw new InvalidDataException("The archive entry count is invalid.");
            if (string.Equals(envelope.Encryption, "aes-256-gcm", StringComparison.Ordinal))
                return new VaultArchiveInfo(true, envelope.ItemCount);
            if (string.Equals(envelope.Encryption, "none", StringComparison.Ordinal))
                return new VaultArchiveInfo(false, envelope.ItemCount);
            throw new InvalidDataException("The archive uses an unsupported encryption method.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("This is not a valid Codex Vault archive.");
        }
    }

    public static bool IsPasswordProtected(ReadOnlySpan<byte> archiveBytes) => Inspect(archiveBytes).PasswordProtected;

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations,
                HashAlgorithmName.SHA256, KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] Decode(string? value, string field, int? expectedLength = null)
    {
        if (string.IsNullOrEmpty(value)) throw new InvalidDataException($"The archive {field} is missing.");
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (expectedLength is not null && bytes.Length != expectedLength)
                throw new InvalidDataException($"The archive {field} has an invalid length.");
            return bytes;
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"The archive {field} is damaged.", ex);
        }
    }

    private static void ValidateItems(IReadOnlyList<VaultArchiveItem> items)
    {
        if (items is null) throw new InvalidDataException("The archive entry list is missing.");
        if (items.Count > 10_000) throw new InvalidDataException("The archive contains too many entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item is null) throw new InvalidDataException("The archive contains an empty entry.");
            SecretNames.Validate(item.Name);
            if (string.IsNullOrEmpty(item.Value) || item.Value.Length > 2560)
                throw new InvalidDataException($"The value for '{item.Name}' has an invalid length.");
            if (!names.Add(item.Name))
                throw new InvalidDataException($"The archive contains the duplicate name '{item.Name}'.");
        }
    }

    private sealed record ArchiveEnvelope(
        int Version,
        string Encryption,
        int? ItemCount,
        int? Iterations,
        string? Salt,
        string? Nonce,
        string? Tag,
        string Payload);

    private sealed record ArchivePayload(
        int Version,
        DateTimeOffset CreatedAt,
        VaultArchiveItem[] Items);
}
