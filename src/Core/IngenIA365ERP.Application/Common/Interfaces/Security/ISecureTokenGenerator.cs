namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Genera tokens criptográficamente seguros para flujos de un solo uso
/// (invitaciones US1, password reset Phase 4b). Devuelve la representación
/// plana en Base64Url (sin padding) que viaja en el enlace al destinatario,
/// y el hash SHA-256 que se persiste en BD — el plano nunca se almacena.
///
/// <para>
/// Patrón típico:
/// <code>
/// var (plain, hash) = _tokens.Generate(); // 32 bytes / 256 bits
/// invitation.SetTokenHash(hash);
/// await _emailDispatcher.DispatchAsync(new(..., PlainTokenBase64Url: plain), ct);
/// // 'plain' sale del proceso al SMTP; nunca toca disco.
/// </code>
/// </para>
/// </summary>
public interface ISecureTokenGenerator
{
    /// <summary>
    /// Genera <paramref name="byteCount"/> bytes random crypto-safe y
    /// devuelve la pareja (plano Base64Url, hash SHA-256). Default 32
    /// bytes — 256 bits de entropía, ampliamente suficiente para tokens
    /// de invitación de 7 días de TTL.
    /// </summary>
    SecureTokenResult Generate(int byteCount = 32);

    /// <summary>
    /// Recalcula el hash SHA-256 de un token plano. Usado por los handlers
    /// de Accept/Preview para resolver el token recibido por enlace contra
    /// el hash persistido. Mismo algoritmo que <see cref="Generate"/>.
    /// </summary>
    byte[] HashPlainToken(string plainTokenBase64Url);
}

/// <summary>
/// Resultado de <see cref="ISecureTokenGenerator.Generate"/>. Las propiedades
/// están deliberadamente como tipos primitivos (no record) para que el
/// caller pueda destruir el plano sobreescribiendo si la política de
/// seguridad lo exige.
/// </summary>
public sealed record SecureTokenResult(string PlainTokenBase64Url, byte[] Sha256Hash);
