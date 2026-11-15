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
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// <c>NonElectronicDebitNote</c>, la nota débito en papel (2026-11-12, decisión del dueño): la de la cooperativa <b>no obligada</b> a facturar
/// electrónicamente, sobre su comprobante de venta no electrónico. Es a la nota débito electrónica (<see cref="EfectoDeNotaDebito"/>) lo que la
/// nota de venta no electrónica es a la nota crédito:
/// <list type="bullet">
/// <item>antes de la aprobación, en orden: el veredicto de <c>GuardiaDeEmisionFiscal</c> —sólo confirma con <c>NonElectronic</c> (la clase está
/// en <c>ClasesNoElectronicas</c>); una cooperativa obligada recibe <c>Inventory.Sales.FiscalClassMismatch</c>—; el original por <c>NoteOf</c>, un
/// <c>NonElectronicSalesReceipt</c> confirmado (<c>Inventory.DebitNote.OriginInvalid</c>); el concepto de corrección es <b>opcional</b> y, si
/// viene, es uno del catálogo de nota débito (<c>Inventory.CreditNote.CorrectionConceptRequired</c>); y las reglas de venta de
/// <see cref="ReglasDeConfirmacionDeVenta"/> (impuestos, pagos que suman <c>AmountDue</c>, crédito provisional);</item>
/// <item>la aprobación propia del crédito provisional, como la electrónica;</item>
/// <item>no escribe kardex ni va a la DIAN: numera con su consecutivo (T16) y no tiene paso fiscal;</item>
/// <item>es <b>derivada</b> del comprobante que corrige: copia su modo de paso y emite los mismos mensajes que la electrónica
/// (<c>NotaDebitoEmitida</c> y, por pago de crédito, <c>AjusteDeVentaACredito</c>): Contabilidad la asienta igual;</item>
/// <item>se <b>anula</b> con su documento contrario, como todo documento en papel (<see cref="ClasesDeDocumento.SeAnulaConAnulacion"/>):
/// <see cref="AnulacionDeVenta"/> emite <c>DocumentoAnulado</c> con su contenido en signo contrario.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class EfectoDeNotaDebitoNoElectronica(
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    public override DocumentClass Clase => DocumentClass.NonElectronicDebitNote;

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await anulacion.ValidarAsync(contexto, ct);
        var nota = contexto.Documento;

        var fiscal = await reglas.VeredictoFiscalAsync(contexto, ct);
        if (fiscal is not null) return Result.Failure(fiscal);

        var original = await NotasDeVenta.OriginalDeAsync(db, nota, ct);
        if (original is null || original.Status != DocumentStatus.Confirmed || original.Class != DocumentClass.NonElectronicSalesReceipt)
            return Result.Failure(ErroresDelCicloComercial.NonElectronicDebitNoteOriginInvalid());

        // En papel el concepto es opcional; si se indica, es uno del catálogo de la DIAN para nota débito (el mismo de la electrónica).
        if (!string.IsNullOrWhiteSpace(nota.CorrectionConceptCode)
            && CatalogoDian.Embebido.ConceptoDeCorreccion(ClaseDeNotaDian.NotaDebito, nota.CorrectionConceptCode, nota.OperationDate) is null)
            return Result.Failure(ErroresDeVentas.CorrectionConceptRequired());

        var productos = await ReglasDeLineasDeVenta.ProductosAsync(nota, maestros, ct);
        if (productos.IsFailure) return Result.Failure(productos.Error);
        return await reglas.ValidarVentaAsync(contexto, ct);
    }

    /// <summary>Cobrada con un medio de crédito, pide la aprobación del crédito provisional (como la electrónica).</summary>
    public override Task<Result<ApprovalRequest?>> AprobacionPropiaAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        contexto.EsAnulacion ? Task.FromResult(Result.Success<ApprovalRequest?>(null)) : reglas.AprobacionDeCreditoAsync(contexto, ct);

    /// <summary>Deriva del comprobante que corrige: copia su modo y lo nombra como relacionado.</summary>
    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return [];
        var original = await NotasDeVenta.OriginalDeAsync(db, contexto.Documento, ct);
        return original is { Status: DocumentStatus.Confirmed } ? [original] : [];
    }

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        contexto.EsAnulacion ? anulacion.Cerrojo(contexto, base.Cerrojo(contexto)) : base.Cerrojo(contexto);

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

    public override Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.RevertirAsync(contexto, ct);

    public override Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.MensajesAsync(contexto, ct);
}
