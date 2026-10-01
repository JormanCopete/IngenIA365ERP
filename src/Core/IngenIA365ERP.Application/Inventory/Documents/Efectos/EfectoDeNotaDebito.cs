using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// <c>DebitNote</c>, la nota débito electrónica (feature 012, I6, T884, T885, T886; FR-052, FR-060, FR-061, FR-066; contracts/api.md §18.4,
/// §23; contracts/dian.md, fila <c>DebitNote</c>; mensajes.md §6.12, §8.2):
/// <list type="bullet">
/// <item>antes de la aprobación, en orden: el veredicto de <c>GuardiaDeEmisionFiscal</c> —sólo confirma con <c>Electronic</c>; otra respuesta,
/// <c>Inventory.Sales.FiscalClassMismatch</c> o <c>ElectronicInvoicing.NotReady</c>—; el original por <c>NoteOf</c>, una <c>SalesInvoice</c> o
/// <c>SalesInvoiceFromShipments</c> confirmada (<c>Inventory.DebitNote.OriginInvalid</c>) y expedida (no enviada sin respuesta ni rechazada);
/// el concepto de corrección del catálogo DIAN de nota débito (<c>Inventory.CreditNote.CorrectionConceptRequired</c>); y las reglas de venta
/// de <see cref="ReglasDeConfirmacionDeVenta"/>: impuestos, pagos <c>Received</c> que suman <c>AmountDue</c> (<c>Payments.TotalMismatch</c>) y,
/// si se cobra con un medio de crédito, el crédito provisional —<see cref="CreditoEnLaVenta"/> consulta a Cartera por
/// <c>IConsultasDeCartera.EstadoCrediticioAsync</c>, que mientras IC esté pendiente responde <c>CarteraNoHabilitada</c> (T886), y marca el pago
/// <c>PendingValidation</c>—;</item>
/// <item>la aprobación propia del crédito provisional (sujeto <c>ProvisionalCredit</c>, §23): la nota queda <c>PendingApproval</c> y la última
/// aprobación la confirma;</item>
/// <item>no escribe kardex; numera con su consecutivo propio (T16) y el paso fiscal registra su documento electrónico tipo 92 con la referencia a
/// la factura;</item>
/// <item>es <b>derivada</b> de la venta que corrige: copia su modo de paso y emite <c>NotaDebitoEmitida</c> con <c>related</c> = la venta y, por
/// cada pago de crédito, <c>AjusteDeVentaACredito</c> (<c>DebitNote</c>, monto positivo) hacia <c>Lending</c> con <c>DeliveryMode.Always</c>
/// (<see cref="EmisionDeInventario.CreditoDeLaNotaDebitoAsync"/>);</item>
/// <item>un documento electrónico emitido no se anula: <c>Inventory.Document.FiscalUseCorrection</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class EfectoDeNotaDebito(
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    ReglasDeConfirmacionDeVenta reglas,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    public override DocumentClass Clase => DocumentClass.DebitNote;

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return Result.Failure(InventoryErrors.FiscalUseCorrection());
        var nota = contexto.Documento;

        var fiscal = await reglas.VeredictoFiscalAsync(contexto, ct);
        if (fiscal is not null) return Result.Failure(fiscal);

        var original = await NotasDeVenta.OriginalDeAsync(db, nota, ct);
        if (original is null || original.Status != DocumentStatus.Confirmed
            || original.Class is not (DocumentClass.SalesInvoice or DocumentClass.SalesInvoiceFromShipments))
            return Result.Failure(ErroresDelCicloComercial.DebitNoteOriginInvalid());
        if (await EstadoElectronicoDeInventario.CorreccionImpedidaAsync(db, original.PublicId, ct) is { } impedida) return Result.Failure(impedida);

        if (CatalogoDian.Embebido.ConceptoDeCorreccion(ClaseDeNotaDian.NotaDebito, nota.CorrectionConceptCode, nota.OperationDate) is null)
            return Result.Failure(ErroresDeVentas.CorrectionConceptRequired());

        var productos = await ReglasDeLineasDeVenta.ProductosAsync(nota, maestros, ct);
        if (productos.IsFailure) return Result.Failure(productos.Error);
        return await reglas.ValidarVentaAsync(contexto, ct);
    }

    /// <summary>T885: cobrada con un medio de crédito, pide la aprobación del crédito provisional.</summary>
    public override Task<Result<ApprovalRequest?>> AprobacionPropiaAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        reglas.AprobacionDeCreditoAsync(contexto, ct);

    /// <summary>Deriva de la venta que corrige (data-model §5.3; FR-075): copia su modo y la nombra como relacionada.</summary>
    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return [];
        var original = await NotasDeVenta.OriginalDeAsync(db, contexto.Documento, ct);
        return original is { Status: DocumentStatus.Confirmed } ? [original] : [];
    }

    /// <summary>Dentro del cerrojo: la foto de impuestos, el toque de las sesiones de los pagos y el vencimiento del crédito.</summary>
    public override Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct) => reglas.AlConfirmarVentaAsync(contexto, ct);

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var nota = contexto.Documento;
        var impuestos = await reglas.ImpuestosDeAsync(nota, ct);
        var pagos = await reglas.PagosDeAsync(nota, PaymentDirection.Received, ct);
        var contenidos = new List<object> { await emision.NotaDebitoAsync(nota, impuestos, pagos, ct) };
        if (await NotasDeVenta.OriginalDeAsync(db, nota, ct) is { } original)
            contenidos.AddRange(await emision.CreditoDeLaNotaDebitoAsync(nota, original, pagos, ct));
        return contenidos;
    }
}
