using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Clave de idempotencia de una operación de pantalla (<c>COR_OperationKeys</c>; feature 012, data-model §19,
/// decisiones-transversales T13). La inserta <c>IdempotencyBehavior</c> dentro de la transacción del comando,
/// antes de ejecutarlo, y le escribe el resultado sólo si el comando tuvo éxito; si falla, la transacción se
/// revierte y la fila no queda. Por eso una fila visible es siempre una operación confirmada.
///
/// <para>
/// El índice único de <see cref="Key"/> es lo que hace esperar a un duplicado concurrente: la segunda
/// inserción queda bloqueada hasta que la primera confirma o revierte. La fila <b>nunca se borra ni cambia
/// después del commit</b> (pregunta B3: retención indefinida). <c>[SinDiffDeAuditoria]</c> porque es una fila
/// técnica: el evento del comando que protege ya se audita, y la repetición deja <c>Operation.Replayed</c>.
/// </para>
/// </summary>
[SinDiffDeAuditoria]
public class OperationKey : AuditableEntity
{
    /// <summary>
    /// Nombre del índice único de <see cref="Key"/>. Lo usa la configuración EF y lo busca
    /// <c>IdempotencyBehavior</c> en la excepción para reconocer un duplicado concurrente.
    /// </summary>
    public const string IndiceUnicoDeLaClave = "UK_COR_OperationKeys_Key";

    /// <summary>La cabecera <c>Idempotency-Key</c>; única en la base de la cooperativa, sin filtro.</summary>
    public Guid Key { get; set; }

    /// <summary>Nombre del comando (máx. 120).</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>SHA-256 en hexadecimal (64) de la operación y el cuerpo canónico; otra huella con la misma clave es <c>Operation.KeyReused</c>.</summary>
    public string RequestSha256 { get; set; } = string.Empty;

    /// <summary>La identidad central de quien la hizo; <see cref="Guid.Empty"/> si no la hay (proceso).</summary>
    public Guid CentralUserId { get; set; }

    /// <summary><c>SEC_Users.Id</c> de la cooperativa, de <c>IActorActual</c>; nulo si no se resolvió.</summary>
    public int? UserId { get; set; }

    /// <summary>Nombre a mostrar del actor (máx. 150).</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>El resultado exitoso serializado; se escribe en la misma transacción, antes del commit.</summary>
    public string? ResultJson { get; set; }
}
