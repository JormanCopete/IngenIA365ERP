using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Los dos errores de la idempotencia (feature 012, T13; contracts/api.md §2.1, §2.3; §2.17): sin clave es
/// 400 (<c>ErrorEnvelopeFilter</c> lo mapea aparte, porque no empieza con <c>Validation.</c>) y la clave
/// reutilizada con otro contenido es 422 con <c>data { operation, firstUsedAt }</c>.
/// </summary>
public static class ErroresDeOperacion
{
    public const string CodigoClaveRequerida = "Operation.KeyRequired";
    public const string CodigoClaveReutilizada = "Operation.KeyReused";

    public static Error ClaveRequerida() => new(CodigoClaveRequerida,
        "Falta la clave de la operación (cabecera Idempotency-Key con un UUID). Volvé a abrir la pantalla e intentá de nuevo.");

    public static Error ClaveReutilizada(string operacion, DateTime primerUso) => new ErrorConDatos(CodigoClaveReutilizada,
        "Esa clave de operación ya se usó para otra solicitud. Recargá la pantalla para empezar una operación nueva.",
        new { operation = operacion, firstUsedAt = DateTime.SpecifyKind(primerUso, DateTimeKind.Utc) });
}
