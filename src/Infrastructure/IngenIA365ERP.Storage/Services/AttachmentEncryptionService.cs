using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using Microsoft.AspNetCore.DataProtection;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// T106 — Cifrado AES-256-GCM para adjuntos (FR-031).
///
/// <para>
/// <b>Esquema</b>: DEK per-blob random de 256 bits. La DEK se envuelve con
/// DataProtection (KEK) y se persiste en <c>Attachment.EncryptedDek</c>;
/// la KEK no abandona DataProtection. Rotar la KEK no requiere re-cifrar
/// blobs — sólo re-envolver las DEK con la nueva clave.
/// </para>
///
/// <para>
/// <b>Formato del blob</b>: <c>NONCE(12) || TAG(16) || CIPHERTEXT</c>. El
/// IV es random por blob (nunca reutilizado con la misma DEK — invariante
/// crítico de GCM). El tag GCM se verifica al descifrar; falla con
/// <see cref="CryptographicException"/> si el blob fue alterado.
/// </para>
/// </summary>
public sealed class AttachmentEncryptionService : IAttachmentCipher
{
    private const int DekBytes = 32;   // AES-256
    private const int NonceBytes = 12; // GCM standard
    private const int TagBytes = 16;   // GCM standard
    private const string DekProtectorPurpose = "IngenIA365ERP.Attachments.Dek.v1";

    private readonly IDataProtector _dekProtector;

    public AttachmentEncryptionService(IDataProtectionProvider dataProtection)
    {
        _dekProtector = dataProtection.CreateProtector(DekProtectorPurpose);
    }

    /// <summary>
    /// Genera DEK random + cifra <paramref name="plaintext"/> con AES-GCM +
    /// devuelve los bytes a persistir y la DEK envuelta para guardar.
    /// </summary>
    public AttachmentCipherPayload Encrypt(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var dek = RandomNumberGenerator.GetBytes(DekBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];

        using (var aes = new AesGcm(dek, TagBytes))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var blob = new byte[NonceBytes + TagBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceBytes);
        Buffer.BlockCopy(tag, 0, blob, NonceBytes, TagBytes);
        Buffer.BlockCopy(ciphertext, 0, blob, NonceBytes + TagBytes, ciphertext.Length);

        var wrappedDek = _dekProtector.Protect(Convert.ToBase64String(dek));
        // Limpieza defensiva — la DEK ya no se necesita en memoria.
        CryptographicOperations.ZeroMemory(dek);

        return new AttachmentCipherPayload(blob, wrappedDek);
    }

    /// <summary>
    /// Desenvuelve la DEK y descifra el blob. Lanza
    /// <see cref="CryptographicException"/> si el blob fue alterado (tag GCM
    /// no coincide) o si la KEK ya no puede desenvolver la DEK (clave rotada
    /// fuera de su ring de descifrado).
    /// </summary>
    public byte[] Decrypt(byte[] blob, string wrappedDek)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentException.ThrowIfNullOrEmpty(wrappedDek);
        if (blob.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException(
                "Blob inválido: tamaño insuficiente para nonce+tag GCM.");
        }

        var dek = Convert.FromBase64String(_dekProtector.Unprotect(wrappedDek));
        try
        {
            var nonce = new ReadOnlySpan<byte>(blob, 0, NonceBytes);
            var tag = new ReadOnlySpan<byte>(blob, NonceBytes, TagBytes);
            var ciphertext = new ReadOnlySpan<byte>(blob, NonceBytes + TagBytes,
                blob.Length - NonceBytes - TagBytes);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(dek, TagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }
}

