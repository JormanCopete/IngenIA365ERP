using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

// Feature 015, T2 (fase 42, T0455; FR-124 a FR-127, FR-220; contracts/contabilidad.md §4; mensajes.md §5): los pagos de T2, el aviso del pago a Comercial y el cobro tardío
// de un cheque pasado a cuenta por pagar. Sin guardar: nacen en el mismo guardado que el resultado del giro o el cobro.
public sealed partial class EmisionDeTesoreria
{
    public const string ClaseChequeCobradoTarde = "IssuedCheckLateCashing";

    /// <summary>
    /// <c>PagoDeProveedorGirado</c> (PPG): <c>CuentasPorPagarProveedores</c> (el proveedor como tercero, la factura de Comercial como cruce) → <c>Banco</c> por lo girado, con
    /// la fecha del débito.
    /// </summary>
    public async Task<IntegrationMessage> PagoDeProveedorGiradoAsync(OwnBankAccount cuenta, PaymentOrder orden, DisbursementResult resultado, CancellationToken ct)
    {
        var dim = await DimensionesAsync(cuenta, orden, ct);
        var proveedor = await BeneficiarioAsync(orden, ct);
        var contenido = new PagoDeProveedorGiradoV1
        {
            PaymentOrderPublicId = orden.PublicId, OrderNumber = orden.Number, InvoicePublicId = orden.OriginOrderPublicId, InvoiceNumber = orden.OriginOrderNumber,
            SupplierPersonPublicId = proveedor, Amount = resultado.Amount, DebitDate = resultado.OccurredOn, TreasuryReference = resultado.TreasuryReference,
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreriaT2.CuentasPorPagarProveedores, RolesDeTesoreria.Debito, resultado.Amount, dim, proveedor, orden.OriginOrderNumber),
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, resultado.Amount, dim, cruce: orden.Number),
            ],
        };
        return await EmitirAsync(contenido, orden.PublicId, ClasePagoGirado, TipoDeOrden, orden.Number, resultado.OccurredOn, cuenta.BranchId, proveedor,
            ClavesDeEvento.Confirmacion, ct);
    }

    /// <summary>
    /// <c>PagoDeCuentaPorPagarGirado</c> (PCG): <c>CuentaPorPagarContable</c> (el tercero y el documento cruce de la cuenta por pagar) → <c>Banco</c> por lo girado.
    /// </summary>
    public async Task<IntegrationMessage> PagoDeCuentaPorPagarGiradoAsync(OwnBankAccount cuenta, PaymentOrder orden, DisbursementResult resultado, CancellationToken ct)
    {
        var dim = await DimensionesAsync(cuenta, orden, ct);
        var tercero = await BeneficiarioAsync(orden, ct);
        var contenido = new PagoDeCuentaPorPagarGiradoV1
        {
            PaymentOrderPublicId = orden.PublicId, OrderNumber = orden.Number, PayableDocumentPublicId = orden.SourceDocumentPublicId ?? orden.OriginOrderPublicId,
            CrossDocument = orden.OriginOrderNumber, ThirdPartyPersonPublicId = tercero, Amount = resultado.Amount, DebitDate = resultado.OccurredOn,
            TreasuryReference = resultado.TreasuryReference, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreriaT2.CuentaPorPagarContable, RolesDeTesoreria.Debito, resultado.Amount, dim, tercero, orden.OriginOrderNumber),
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, resultado.Amount, dim, cruce: orden.Number),
            ],
        };
        return await EmitirAsync(contenido, orden.PublicId, ClasePagoGirado, TipoDeOrden, orden.Number, resultado.OccurredOn, cuenta.BranchId, tercero,
            ClavesDeEvento.Confirmacion, ct);
    }

    /// <summary>
    /// <c>PagoDeFacturaDeProveedor</c> v1 (<c>TES → Inventory</c>; E-03): el aviso a Comercial del pago de su factura. Origen la orden, clave <c>Confirmation:{orden:N}</c>,
    /// modo <c>Always</c>. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> PagoDeFacturaAComercialAsync(OwnBankAccount cuenta, PaymentOrder orden, DisbursementResult resultado, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BranchId).Select(b => b.PublicId).FirstAsync(ct);
        var contenido = new PagoDeFacturaDeProveedorV1
        {
            InvoicePublicId = orden.OriginOrderPublicId, InvoiceNumber = orden.OriginOrderNumber, PaymentOrderPublicId = orden.PublicId, OrderNumber = orden.Number,
            Amount = resultado.Amount, PaidOn = resultado.OccurredOn, TreasuryReference = resultado.TreasuryReference, RegisteredBy = await UsuarioAsync(ct),
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, orden.PublicId, ClasePagoGirado, TipoDeOrden, orden.Number, resultado.OccurredOn, oficina),
            ClavesDeEvento.ConfirmacionPor(orden.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return mensajes[0];
    }

    /// <summary>
    /// <c>ChequeNoCobradoPagado</c> (CTP; FR-220; pedido del dueño, pendiente de confirmar): el cheque que pasó a cuenta por pagar y se cobra después:
    /// <c>CuentaPorPagarChequesNoCobrados</c> (el beneficiario, el número del cheque como cruce) → <c>Banco</c>, con la fecha del cobro.
    /// </summary>
    public async Task<IntegrationMessage> ChequeNoCobradoPagadoAsync(OwnBankAccount cuenta, IssuedCheck cheque, DateOnly cobrado, Guid? linea, CancellationToken ct)
    {
        var dim = await DimensionesDeCuentaAsync(cuenta, "LateCashing", ct);
        var beneficiario = cheque.BeneficiaryPersonId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == p).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var numero = cheque.CheckNumber.ToString(CultureInfo.InvariantCulture);
        var contenido = new ChequeNoCobradoPagadoV1
        {
            IssuedCheckPublicId = cheque.PublicId, CheckNumber = cheque.CheckNumber, BankAccountPublicId = cuenta.PublicId, BeneficiaryPersonPublicId = beneficiario,
            Amount = cheque.Amount, CashedOn = cobrado, MovedToPayableOn = cheque.MovedToPayableOn ?? cobrado, StatementLinePublicId = linea,
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreriaT1.CuentaPorPagarChequesNoCobrados, RolesDeTesoreria.Debito, cheque.Amount, dim, beneficiario, numero),
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, cheque.Amount, dim, cruce: numero),
            ],
        };
        return await EmitirAsync(contenido, cheque.PublicId, ClaseChequeCobradoTarde, TipoDeTraslado, numero, cobrado, cuenta.BranchId, beneficiario,
            ClavesDeEvento.Confirmacion, ct);
    }
}
