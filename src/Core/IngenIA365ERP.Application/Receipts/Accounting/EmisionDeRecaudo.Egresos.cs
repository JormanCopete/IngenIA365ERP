using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Egresos;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, R1, US4 (T0373; FR-051 a FR-056, FR-061, FR-205; contracts/contabilidad.md §3, §8): los hechos del egreso de caja, en línea, con el tipo sugerido EG.
// Ningún hecho lleva una cuenta: roles y dimensiones. El retiro y la cancelación de Captaciones y el saldo a favor de Cartera salen del puente del módulo (el módulo
// escribió su lado con su contrato); el saldo a favor de un pagador sale de SaldoAFavorDePagadores; el egreso vario, de su concepto. La anulación es el espejo.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseEgresoDeCaja = "CashDisbursement";
    public const string ClaseAnulacionDeEgreso = "CashDisbursementVoiding";

    /// <summary><c>EgresoDeCajaEntregado</c>: débito al rol de lo que se paga y crédito a <c>Caja</c> (el medio efectivo sellado en la sesión). Sin guardar.</summary>
    public async Task<IntegrationMessage> EgresoDeCajaEntregadoAsync(CashDisbursement egreso, string codigoDelTipo, CancellationToken ct)
    {
        var (lineas, beneficiario, recibe) = await LineasDelEgresoAsync(egreso, ct);
        var contenido = new EgresoDeCajaEntregadoV1
        {
            CashDisbursementPublicId = egreso.PublicId, Number = egreso.Number, Kind = egreso.Kind.ToString(), BeneficiaryPersonPublicId = beneficiario,
            ReceivedByPersonPublicId = recibe, ProductPublicId = egreso.ProductPublicId, ProductNumber = egreso.ProductNumber,
            ModuleVoucherNumber = egreso.ModuleAuthorizationNumber, Amount = egreso.Amount, OperationDate = egreso.OperationDate,
            DeliveredBy = await UsuarioAsync(egreso.CashierUserId, ct), Lines = lineas,
        };
        return await EmitirAsync(contenido, egreso.PublicId, ClaseEgresoDeCaja, codigoDelTipo, egreso.Number, egreso.OperationDate, egreso.BranchId, beneficiario, ct);
    }

    /// <summary><c>GmfDeEgresoCausado</c>: <c>GmfGasto</c> → <c>GmfPorPagar</c>, por la oficina del egreso. Sin guardar.</summary>
    public async Task<IntegrationMessage> GmfDeEgresoCausadoAsync(CashDisbursement egreso, GmfDeEgreso gmf, decimal tarifa, string codigoDelTipo, CancellationToken ct)
    {
        var contenido = new GmfDeEgresoCausadoV1
        {
            CashDisbursementPublicId = egreso.PublicId, Number = egreso.Number, Kind = egreso.Kind.ToString(), TaxBase = egreso.Amount, Rate = tarifa, Amount = gmf.Valor,
            Reason = gmf.Motivo, OperationDate = egreso.OperationDate, Lines = await LineasDelGmfAsync(egreso, gmf.Valor, ct),
        };
        return await EmitirAsync(contenido, egreso.PublicId, ClaseEgresoDeCaja, codigoDelTipo, egreso.Number, egreso.OperationDate, egreso.BranchId, null, ct);
    }

    /// <summary><c>EgresoDeCajaAnulado</c>: el espejo del egreso y, si Recaudo lo causó, de su GMF. Sin guardar.</summary>
    public async Task<IntegrationMessage> EgresoDeCajaAnuladoAsync(CashDisbursement egreso, CashDisbursementVoiding anulacion, string codigoDelTipo, CancellationToken ct)
    {
        var (lineas, beneficiario, _) = await LineasDelEgresoAsync(egreso, ct);
        var todas = lineas.ToList();
        if (!egreso.GmfByOrigin && egreso.GmfAmount is > 0m) todas.AddRange(await LineasDelGmfAsync(egreso, egreso.GmfAmount.Value, ct));
        var espejo = todas.Select(l => l with { Side = l.Side == RolesDeRecaudo.Debito ? RolesDeRecaudo.Credito : RolesDeRecaudo.Debito, CrossDocument = anulacion.Number })
            .ToList();
        var contenido = new EgresoDeCajaAnuladoV1
        {
            CashDisbursementVoidingPublicId = anulacion.PublicId, Number = anulacion.Number, CashDisbursementPublicId = egreso.PublicId,
            CashDisbursementNumber = egreso.Number, ModuleReversalNumber = anulacion.ModuleReversalNumber, Amount = egreso.Amount, Reason = anulacion.Reason,
            VoidedOn = DateOnly.FromDateTime(anulacion.VoidedAt), VoidedBy = await UsuarioAsync(anulacion.RequestedByUserId, ct), Lines = espejo,
        };
        return await EmitirAsync(contenido, anulacion.PublicId, ClaseAnulacionDeEgreso, codigoDelTipo, anulacion.Number, contenido.VoidedOn, egreso.BranchId, beneficiario, ct,
            new DocumentoRelacionado(egreso.PublicId, ClaseEgresoDeCaja, egreso.Number));
    }

    private async Task<(IReadOnlyList<FactLineV1> Lineas, Guid Beneficiario, Guid? Recibe)> LineasDelEgresoAsync(CashDisbursement egreso, CancellationToken ct)
    {
        var ids = new[] { egreso.BeneficiaryPersonId, egreso.ReceivedByPersonId ?? 0 }.Where(x => x != 0).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var beneficiario = publicos[egreso.BeneficiaryPersonId];
        var sesion = await db.ReceiptsCashSessions.AsNoTracking().FirstAsync(s => s.Id == egreso.CashSessionId, ct);
        var dimCaja = await DimensionesDeCajaAsync(egreso.CashBoxId, ValoresSelladosDeLaSesion.De(sesion).CashMeansId, ct);
        var debito = egreso.Kind switch
        {
            CashDisbursementKind.PayerCreditBalanceRefund => Linea(RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.Debito, egreso.Amount, dimCaja, beneficiario,
                egreso.ProductNumber ?? egreso.Number),
            CashDisbursementKind.MiscExpense => Linea(RolesDeRecaudo.ConceptoVario, RolesDeRecaudo.Debito, egreso.Amount, dimCaja with
            {
                MiscConceptCode = await db.MiscConcepts.AsNoTracking().Where(c => c.Id == egreso.MiscConceptId).Select(c => c.Code).FirstOrDefaultAsync(ct),
            }, beneficiario, egreso.Number),
            _ => Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Debito, egreso.Amount, dimCaja with
            {
                TargetModule = ModulosDeOrigen.Cartera,
                TargetNature = egreso.ProductNature ?? (egreso.Kind == CashDisbursementKind.LendingCreditBalanceRefund ? "Loan" : "Deposit"),
            }, cruce: egreso.Number),
        };
        var credito = Linea(RolesDeRecaudo.Caja, RolesDeRecaudo.Credito, egreso.Amount, dimCaja, cruce: egreso.Number);
        return ([debito, credito], beneficiario, egreso.ReceivedByPersonId is { } r ? publicos[r] : null);
    }

    private async Task<IReadOnlyList<FactLineV1>> LineasDelGmfAsync(CashDisbursement egreso, decimal valor, CancellationToken ct)
    {
        var dim = await DimensionesDeOficinaAsync(egreso.BranchId, ct);
        return
        [
            Linea(RolesDeRecaudo.GmfGasto, RolesDeRecaudo.Debito, valor, dim, cruce: egreso.Number),
            Linea(RolesDeRecaudo.GmfPorPagar, RolesDeRecaudo.Credito, valor, dim, cruce: egreso.Number),
        ];
    }
}
