using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Paso 4 del flujo canónico (decisiones-transversales §1.3): la guardia de emisión fiscal de las clases fiscales. La
/// confirmación lo omite mientras no haya implementación registrada (I3/I4 registran la que llama a
/// <c>GuardiaDeEmisionFiscal</c>). (nuevo)
/// </summary>
public interface IPasoFiscalDeConfirmacion
{
    Task<Result> EvaluarAsync(ContextoDeEfecto contexto, CancellationToken ct);
}

/// <summary>
/// Paso 5 del flujo canónico: la validación previa contable, fuera del cerrojo, sólo si el modo que se sellará no es
/// <c>NotPosted</c>. La confirmación lo omite mientras no haya implementación registrada (I2 registra la que llama a
/// <c>IContabilidadParaInventario.EvaluarAsync</c>); entonces <c>prevalidation.outcome = NotApplicable</c>. (nuevo)
/// </summary>
public interface IPasoDeValidacionPrevia
{
    /// <summary>El resultado a sellar en los mensajes, o el error que deja el documento en borrador.</summary>
    Task<Result<ResultadoDeValidacionPrevia>> EvaluarAsync(ContextoDeEfecto contexto, IReadOnlyList<object> contenidos, CancellationToken ct);
}

/// <summary>Lo que devuelve la validación previa: el desenlace y sus avisos. (nuevo)</summary>
public sealed record ResultadoDeValidacionPrevia(PrevalidationOutcome Outcome, IReadOnlyList<AvisoDto> Warnings);

/// <summary>
/// Lo que la <b>última aprobación</b> de un documento confirma además de él, en la misma transacción del aprobador (feature
/// 012, US9, T344): la factura de una compra directa queda en borrador enlazada a su recepción en aprobación, y se confirma
/// cuando la recepción se aprueba. La llama <c>FuenteDeAprobacionDeDocumento</c> después de confirmar. (nuevo)
/// </summary>
public interface IConfirmacionEncadenada
{
    Task<Result> AlConfirmarPorAprobacionAsync(Guid documentoPublicId, CancellationToken ct);
}
