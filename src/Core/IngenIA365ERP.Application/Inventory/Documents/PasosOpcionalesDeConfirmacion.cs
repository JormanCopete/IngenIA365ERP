using IngenIA365ERP.Application.Common.Integration.Accounting;
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
/// <c>NotPosted</c>. La confirmación lo omite mientras no haya implementación registrada; desde I2 la registrada es
/// <c>ValidacionPreviaContable</c> (T520), que llama a <c>IContabilidadParaInventario.EvaluarAsync</c>. Recibe los sobres tal como
/// se emitirían (<c>MensajesDelDocumento.Sobres</c>), con los costos provisionales. (nuevo)
/// </summary>
public interface IPasoDeValidacionPrevia
{
    /// <summary>El resultado a sellar en los mensajes, o el error que deja el documento en borrador.</summary>
    Task<Result<ResultadoDeValidacionPrevia>> EvaluarAsync(
        ContextoDeEfecto contexto, IReadOnlyList<MensajeContableDto> mensajes, CancellationToken ct);
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

/// <summary>
/// Lo que se prepara en un documento <b>antes</b> de que su última aprobación lo confirme, en la misma transacción del aprobador
/// (feature 012, US11, T396): la fecha del ajuste de un conteo con <c>Conteo.FechaDelAjuste = Aprobacion</c> es la de la aprobación, y
/// si el mes de su fecha cerró mientras esperaba, el primer día abierto (FR-041). La llama <c>FuenteDeAprobacionDeDocumento</c> antes de
/// reentrar al flujo canónico; la huella de lo aprobado ya se comprobó. (nuevo)
/// </summary>
public interface IAntesDeConfirmarPorAprobacion
{
    Task<Result> PrepararAsync(Guid documentoPublicId, CancellationToken ct);
}

/// <summary>
/// Un aviso que la confirmación da <b>después</b> de su guardado, en la misma transacción (feature 012, I3, T611): la alerta de venta bajo
/// costo con <c>Ventas.BajoCosto = Alertar</c>. Levanta sus alertas (que guardan solas) y devuelve lo que va a <c>warnings[]</c>. Nunca
/// bloquea; mira la clase del documento para saber si le toca. (nuevo)
/// </summary>
public interface IAvisoAlConfirmar
{
    Task<IReadOnlyList<AvisoDto>> AvisarAsync(Domain.Entities.Inventory.Documents.InventoryDocument documento, CancellationToken ct);
}
