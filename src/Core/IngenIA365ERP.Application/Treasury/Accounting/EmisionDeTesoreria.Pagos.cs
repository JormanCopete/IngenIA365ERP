using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;

namespace IngenIA365ERP.Application.Treasury.Accounting;

// Feature 015, US11 (T0159; contracts/contabilidad.md §4; FR-142, FR-144): el hecho del pago girado.
public sealed partial class EmisionDeTesoreria
{
    /// <summary>La clase de documento del origen del pago girado (nuevo).</summary>
    public const string ClasePagoGirado = "PaymentOrderDisbursed";

    /// <summary>
    /// <c>PagoDeOrdenGirado</c> (POG): al registrar «girada» de una orden pagada desde una cuenta propia, débito <c>PuenteDeGiroDelOrigen</c> (el puente que el origen
    /// acreditó al emitir la orden, resuelto por la matriz con <c>OrderOrigin</c> y <c>OrderKind</c>) y crédito <c>Banco</c> de la cuenta, con la <b>fecha del débito</b>
    /// (Q3): la que el banco informa en la respuesta o la del soporte. Un rechazo antes de girar no deja hecho: el dinero no salió. Sin guardar. (nuevo)
    /// </summary>
    public async Task<IntegrationMessage> PagoDeOrdenGiradoAsync(OwnBankAccount cuenta, PaymentOrder orden, DisbursementResult resultado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(resultado);
        var dim = await DimensionesAsync(cuenta, orden, ct);
        var beneficiario = await BeneficiarioAsync(orden, ct);
        var contenido = new PagoDeOrdenGiradoV1
        {
            PaymentOrderPublicId = orden.PublicId, OrderNumber = orden.Number, OriginOrderPublicId = orden.OriginOrderPublicId,
            OriginOrderNumber = orden.OriginOrderNumber, OrderOrigin = orden.Origin.ToString(), OrderKind = orden.OrderKind,
            BeneficiaryPersonPublicId = beneficiario, Amount = resultado.Amount, DebitDate = resultado.OccurredOn, TreasuryReference = resultado.TreasuryReference,
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreria.PuenteDeGiroDelOrigen, RolesDeTesoreria.Debito, resultado.Amount, dim, beneficiario, orden.OriginOrderNumber),
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, resultado.Amount, dim, cruce: orden.Number),
            ],
        };
        return await EmitirAsync(contenido, orden.PublicId, ClasePagoGirado, TipoDeOrden, orden.Number, resultado.OccurredOn, cuenta.BranchId, beneficiario,
            ClavesDeEvento.Confirmacion, ct);
    }
}
