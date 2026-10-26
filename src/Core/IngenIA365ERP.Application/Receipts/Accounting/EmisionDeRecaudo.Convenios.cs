using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Receipts.Convenios;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, R2 (fase 38, T0434; FR-093, FR-218; contracts/contabilidad.md §3): la conciliación neta de un convenio, en línea, con el comprobante sugerido RB. Sin guardar.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseConciliacionDeConvenio = "AgreementReconciliation";
    public const string TipoConciliacionDeConvenio = "AC";

    /// <summary>
    /// <c>ComisionDeConvenioRegistrada</c> (CCR): <c>ComisionesDeConvenio</c>, <c>IvaDescontable</c> y cada retención en <c>RetencionesAFavor</c> con su tercero (el aliado
    /// si la retención no trae otro) → <c>BancoRecaudo</c> por lo explicado, fechado el día del abono. Lo sin explicar que se aprobó o cabe en la tolerancia va a
    /// <c>ComisionesDeConvenio</c> con su signo. Nulo si no hay nada que registrar (el abono igual a lo informado).
    /// </summary>
    public async Task<IntegrationMessage?> ComisionDeConvenioAsync(AgreementReconciliation conciliacion, CollectionAgreement convenio, string? numeroDelArchivo,
        Guid? archivo, IReadOnlyList<RetencionPracticada> retenciones, int? aprobadoPor, CancellationToken ct)
    {
        var total = conciliacion.CommissionAmount + conciliacion.VatAmount + conciliacion.WithholdingsAmount + conciliacion.UnexplainedAmount;
        if (total == 0m && conciliacion.CommissionAmount == 0m && conciliacion.UnexplainedAmount == 0m) return null;
        var dim = await DimensionesDeBancoAsync(convenio.BranchId, convenio.OwnBankAccountPublicId, null, "Agreement", ct);
        var aliado = convenio.PartnerPersonId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == p).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var cruce = numeroDelArchivo ?? conciliacion.Number;
        var lineas = new List<FactLineV1>();
        if (conciliacion.CommissionAmount > 0m)
            lineas.Add(Linea(RolesDeRecaudoR2.ComisionesDeConvenio, RolesDeRecaudo.Debito, conciliacion.CommissionAmount, dim, aliado, cruce));
        if (conciliacion.VatAmount > 0m) lineas.Add(Linea(RolesDeRecaudoR2.IvaDescontable, RolesDeRecaudo.Debito, conciliacion.VatAmount, dim, aliado, cruce));
        foreach (var r in retenciones.Where(r => r.Valor > 0m))
            lineas.Add(Linea(RolesDeRecaudoR2.RetencionesAFavor, RolesDeRecaudo.Debito, r.Valor, dim, r.TerceroPublicId ?? aliado, cruce));
        if (conciliacion.UnexplainedAmount != 0m)
            lineas.Add(Linea(RolesDeRecaudoR2.ComisionesDeConvenio, conciliacion.UnexplainedAmount > 0m ? RolesDeRecaudo.Debito : RolesDeRecaudo.Credito,
                Math.Abs(conciliacion.UnexplainedAmount), dim, aliado, cruce));
        var neto = lineas.Sum(l => l.Side == RolesDeRecaudo.Debito ? l.Amount : -l.Amount);
        if (neto == 0m) return null;
        lineas.Add(Linea(RolesDeRecaudo.BancoRecaudo, neto > 0m ? RolesDeRecaudo.Credito : RolesDeRecaudo.Debito, Math.Abs(neto), dim, cruce: cruce));
        var contenido = new ComisionDeConvenioRegistradaV1
        {
            ReconciliationPublicId = conciliacion.PublicId, Number = conciliacion.Number, AgreementCode = convenio.Code, FilePublicId = archivo, FileNumber = numeroDelArchivo,
            StatementLinePublicId = conciliacion.StatementLinePublicId, OwnBankAccountPublicId = convenio.OwnBankAccountPublicId, CreditedOn = conciliacion.CreditedOn,
            InformedAmount = conciliacion.InformedAmount, CreditedAmount = conciliacion.CreditedAmount, CommissionAmount = conciliacion.CommissionAmount,
            VatAmount = conciliacion.VatAmount,
            Withholdings = retenciones.Select(r => new RetencionDelHechoV1 { Kind = r.Clase, Amount = r.Valor, ThirdPartyPersonPublicId = r.TerceroPublicId ?? aliado }).ToList(),
            UnexplainedAmount = conciliacion.UnexplainedAmount, PartnerPersonPublicId = aliado, RegisteredBy = await UsuarioAsync(conciliacion.RegisteredByUserId, ct),
            ApprovedBy = aprobadoPor is { } u ? await UsuarioAsync(u, ct) : null, Lines = lineas,
        };
        return await EmitirAsync(contenido, conciliacion.PublicId, ClaseConciliacionDeConvenio, TipoConciliacionDeConvenio, conciliacion.Number, conciliacion.CreditedOn,
            convenio.BranchId, aliado, ct);
    }

    /// <summary>Las retenciones guardadas de una conciliación (JSON de <c>WithholdingsJson</c>).</summary>
    public static IReadOnlyList<RetencionPracticada> Retenciones(AgreementReconciliation conciliacion) =>
        JsonSerializer.Deserialize<List<RetencionPracticada>>(conciliacion.WithholdingsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
}
