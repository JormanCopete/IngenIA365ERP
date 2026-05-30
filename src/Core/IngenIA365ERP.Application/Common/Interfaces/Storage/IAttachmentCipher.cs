namespace IngenIA365ERP.Application.Common.Interfaces.Storage;

/// <summary>
/// Cifrado AES-256-GCM para adjuntos (T106). Vive en Application para que
/// los handlers no se acoplen a System.Security.Cryptography ni a
/// DataProtection — la impl real está en Infrastructure.Storage.
/// </summary>
public interface IAttachmentCipher
{
    /// <summary>Genera DEK random + cifra el payload. Devuelve blob + DEK envuelta.</summary>
    AttachmentCipherPayload Encrypt(byte[] plaintext);

    /// <summary>Desenvuelve la DEK y descifra el blob. Lanza si el blob fue alterado.</summary>
    byte[] Decrypt(byte[] blob, string wrappedDekBase64);
}

/// <summary>Resultado del cifrado: blob completo + DEK envuelta persistible.</summary>
public sealed record AttachmentCipherPayload(byte[] EncryptedBlob, string WrappedDekBase64);
