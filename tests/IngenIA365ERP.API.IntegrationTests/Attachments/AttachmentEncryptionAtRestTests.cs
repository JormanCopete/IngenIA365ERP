using System.Security.Cryptography;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// T103 — Verifica que el blob en reposo NO contiene el plaintext (FR-031).
///
/// Test unitario sobre el stack real <see cref="AttachmentEncryptionService"/>
/// + <see cref="LocalEncryptedFileStore"/>. NO requiere Docker — usa
/// DataProtection ephemeral en proceso + un directorio temporal.
///
/// Validaciones:
/// <list type="bullet">
///   <item>Tras cifrar+escribir, el archivo en disk NO contiene <c>%PDF</c>.</item>
///   <item>El descifrado devuelve los bytes idénticos al input.</item>
///   <item>Modificar 1 byte del blob hace fallar la verificación GCM.</item>
/// </list>
/// </summary>
public class AttachmentEncryptionAtRestTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IAttachmentCipher _cipher;
    private readonly IBlobStore _store;

    public AttachmentEncryptionAtRestTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ingenia-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        _cipher = new AttachmentEncryptionService(sp.GetRequiredService<IDataProtectionProvider>());
        _store = new LocalEncryptedFileStore(Options.Create(
            new AttachmentStorageSettings { LocalRootPath = _tempRoot }));
    }

    [Fact]
    public async Task Encrypted_blob_on_disk_does_not_contain_pdf_magic_bytes()
    {
        var plaintext = "%PDF-1.7\nThis is the body of a fake PDF for testing."u8.ToArray();

        var cipher = _cipher.Encrypt(plaintext);
        using var ms = new MemoryStream(cipher.EncryptedBlob);
        var reference = await _store.PutAsync(ms, BuildMetadata(plaintext), CancellationToken.None);

        var fullPath = Path.Combine(_tempRoot, reference.Uri.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();
        var rawBytes = await File.ReadAllBytesAsync(fullPath);

        // El payload tenía %PDF al inicio — el blob cifrado NUNCA debe contenerlo.
        ContainsAscii(rawBytes, "%PDF").Should().BeFalse(
            "el blob cifrado no debe revelar la signature MIME original");
        rawBytes.Should().NotEqual(plaintext);
    }

    [Fact]
    public async Task Encrypt_then_decrypt_round_trip_recovers_original_bytes()
    {
        var plaintext = RandomNumberGenerator.GetBytes(2048);

        var cipher = _cipher.Encrypt(plaintext);
        using var ms = new MemoryStream(cipher.EncryptedBlob);
        var reference = await _store.PutAsync(ms, BuildMetadata(plaintext), CancellationToken.None);

        await using var readStream = await _store.GetAsync(reference, CancellationToken.None);
        await using var buffer = new MemoryStream();
        await readStream.CopyToAsync(buffer);
        var roundTripped = _cipher.Decrypt(buffer.ToArray(), cipher.WrappedDekBase64);

        roundTripped.Should().Equal(plaintext);
    }

    [Fact]
    public async Task Tampering_one_byte_of_blob_makes_decryption_fail()
    {
        var plaintext = "datos confidenciales"u8.ToArray();
        var cipher = _cipher.Encrypt(plaintext);
        using var ms = new MemoryStream(cipher.EncryptedBlob);
        var reference = await _store.PutAsync(ms, BuildMetadata(plaintext), CancellationToken.None);

        // Alterar un byte directamente en disk.
        var fullPath = Path.Combine(_tempRoot, reference.Uri.Replace('/', Path.DirectorySeparatorChar));
        var raw = await File.ReadAllBytesAsync(fullPath);
        raw[raw.Length - 1] ^= 0xFF;
        await File.WriteAllBytesAsync(fullPath, raw);

        await using var readStream = await _store.GetAsync(reference, CancellationToken.None);
        await using var buf = new MemoryStream();
        await readStream.CopyToAsync(buf);

        var act = () => _cipher.Decrypt(buf.ToArray(), cipher.WrappedDekBase64);
        act.Should().Throw<CryptographicException>(
            "el tag GCM detecta la manipulación");
    }

    private static BlobMetadata BuildMetadata(byte[] payload) => new(
        TenantId: "1",
        OwnerEntityType: "User",
        OwnerEntityPublicId: Guid.NewGuid(),
        OriginalFileName: "test.pdf",
        ContentType: "application/pdf",
        SizeBytes: payload.LongLength,
        Sha256Hex: Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());

    private static bool ContainsAscii(byte[] haystack, string needle)
    {
        var needleBytes = System.Text.Encoding.ASCII.GetBytes(needle);
        for (var i = 0; i <= haystack.Length - needleBytes.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needleBytes.Length; j++)
            {
                if (haystack[i + j] != needleBytes[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
        catch { /* best-effort */ }
    }
}
