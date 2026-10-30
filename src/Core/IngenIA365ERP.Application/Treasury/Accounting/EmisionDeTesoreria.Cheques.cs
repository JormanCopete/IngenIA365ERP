using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

// Feature 015, US19 (T0228; FR-105, FR-134 a FR-136; contracts/contabilidad.md §4): los cheques girados y las remesas, en línea. Sin guardar.
public sealed partial class EmisionDeTesoreria
{
    public const string ClaseChequeGirado = "IssuedCheck";
    public const string ClaseRemesa = "CashRemittance";
    public const string TipoDeRemesa = "RM";

    /// <summary><c>ChequeGiradoEmitido</c> (CHE): al emitir, <c>PuenteDeGiroDelOrigen</c> → <c>Banco</c> de la cuenta de la chequera, con la fecha de emisión.</summary>
    public async Task<IntegrationMessage> ChequeGiradoEmitidoAsync(OwnBankAccount cuenta, PaymentOrder orden, IssuedCheck cheque, CancellationToken ct)
    {
        var dim = await DimensionesAsync(cuenta, orden, ct);
        var beneficiario = await BeneficiarioAsync(orden, ct);
        var contenido = new ChequeGiradoEmitidoV1
        {
            IssuedCheckPublicId = cheque.PublicId, CheckNumber = cheque.CheckNumber, PaymentOrderPublicId = orden.PublicId, OrderNumber = orden.Number,
            OriginOrderNumber = orden.OriginOrderNumber, OrderOrigin = orden.Origin.ToString(), OrderKind = orden.OrderKind, BeneficiaryPersonPublicId = beneficiario,
            Amount = cheque.Amount, IssuedOn = cheque.IssuedOn, Crossed = cheque.Crossed, FirstBeneficiaryOnly = cheque.FirstBeneficiaryOnly, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreria.PuenteDeGiroDelOrigen, RolesDeTesoreria.Debito, cheque.Amount, dim, beneficiario, orden.OriginOrderNumber),
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, cheque.Amount, dim, cruce: cheque.CheckNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ],
        };
        return await EmitirAsync(contenido, cheque.PublicId, ClaseChequeGirado, TipoDeOrden, orden.Number, cheque.IssuedOn, cuenta.BranchId, beneficiario,
            ClavesDeEvento.Confirmacion, ct);
    }

    /// <summary><c>ChequeGiradoAnulado</c> (CHA): el espejo del emitido —<c>Banco</c> → <c>PuenteDeGiroDelOrigen</c>— con su causa y la fecha de la anulación.</summary>
    public async Task<IntegrationMessage> ChequeGiradoAnuladoAsync(OwnBankAccount cuenta, PaymentOrder orden, IssuedCheck cheque, string causa, DateOnly fecha, CancellationToken ct)
    {
        var dim = await DimensionesAsync(cuenta, orden, ct) with { Cause = causa };
        var beneficiario = await BeneficiarioAsync(orden, ct);
        var contenido = new ChequeGiradoAnuladoV1
        {
            IssuedCheckPublicId = cheque.PublicId, CheckNumber = cheque.CheckNumber, PaymentOrderPublicId = orden.PublicId, OrderNumber = orden.Number, Cause = causa,
            Reason = cheque.VoidReason, Amount = cheque.Amount, VoidedOn = fecha, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Debito, cheque.Amount, dim, cruce: cheque.CheckNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Linea(RolesDeTesoreria.PuenteDeGiroDelOrigen, RolesDeTesoreria.Credito, cheque.Amount, dim, beneficiario, orden.OriginOrderNumber),
            ],
        };
        return await EmitirAsync(contenido, cheque.PublicId, ClaseChequeGirado, TipoDeOrden, orden.Number, fecha, cuenta.BranchId, beneficiario,
            ClavesDeEvento.Confirmacion, ct);
    }

    /// <summary>
    /// <c>RemesaConfirmada</c> (RMC): de la bóveda al banco, <c>Banco</c> ← <c>EfectivoEnTransito</c> al confirmarla; del banco a la bóveda, <c>EfectivoEnTransito</c> ←
    /// <c>Banco</c> al sacar el efectivo.
    /// </summary>
    public async Task<IntegrationMessage> RemesaConfirmadaAsync(OwnBankAccount cuenta, CashRemittance remesa, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == remesa.BranchId).Select(b => b.LegacyCode).FirstOrDefaultAsync(ct);
        var dim = new TreasuryFactDimensionsV1
        {
            BranchCode = CodigoDeOficina(oficina, remesa.BranchId), OwnBankAccountPublicId = cuenta.PublicId, OwnBankAccountCode = cuenta.Code,
            Cause = remesa.Direction.ToString(),
        };
        var alBanco = remesa.Direction == RemittanceDirection.VaultToBank;
        var fecha = alBanco ? remesa.ConfirmedOn ?? remesa.DispatchDate : remesa.DispatchDate;
        var contenido = new RemesaConfirmadaV1
        {
            RemittancePublicId = remesa.PublicId, Number = remesa.Number, Direction = remesa.Direction.ToString(), VaultPublicId = remesa.VaultPublicId, Amount = remesa.Amount,
            BagSeal = remesa.BagSeal, ConfirmedOn = fecha, StatementLinePublicId = remesa.ConfirmedByStatementLinePublicId, RegisteredBy = await UsuarioAsync(ct),
            Lines = alBanco
                ?
                [
                    Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Debito, remesa.Amount, dim, cruce: remesa.Number),
                    Linea(RolesDeTesoreria.EfectivoEnTransito, RolesDeTesoreria.Credito, remesa.Amount, dim, cruce: remesa.Number),
                ]
                :
                [
                    Linea(RolesDeTesoreria.EfectivoEnTransito, RolesDeTesoreria.Debito, remesa.Amount, dim, cruce: remesa.Number),
                    Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, remesa.Amount, dim, cruce: remesa.Number),
                ],
        };
        return await EmitirAsync(contenido, remesa.PublicId, ClaseRemesa, TipoDeRemesa, remesa.Number, fecha, remesa.BranchId, null, ClavesDeEvento.Confirmacion, ct);
    }
}
