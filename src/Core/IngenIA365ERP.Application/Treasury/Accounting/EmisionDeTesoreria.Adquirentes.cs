using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

// Feature 015, T2 (fase 44, T0464; FR-133, FR-218; contracts/contabilidad.md §4): la conciliación de una red de tarjetas explicada o aprobada, en línea y con el
// comprobante sugerido TB. Sin guardar.
public sealed partial class EmisionDeTesoreria
{
    public const string ClaseConciliacionDeAdquirente = "AcquirerSettlement";

    /// <summary>
    /// <c>ConciliacionDeAdquirenteRegistrada</c> (CAQ): <c>Banco</c> (lo abonado) + <c>ComisionesDeRed</c> + <c>IvaDescontable</c> + <c>RetencionesAFavor</c> (la red como
    /// tercero) → <c>MedioPorCobrar</c> (el bruto liquidado), con la liquidación como cruce.
    /// </summary>
    public async Task<IntegrationMessage> ConciliacionDeAdquirenteAsync(OwnBankAccount cuenta, AcquirerSettlement liquidacion, Guid redPublicId, string redCodigo,
        int? personaDeLaRed, bool aprobada, CancellationToken ct)
    {
        var dim = await DimensionesDeCuentaAsync(cuenta, "AcquirerSettlement", ct);
        var tercero = personaDeLaRed is { } t
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == t).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var cruce = liquidacion.Number;
        var retenciones = liquidacion.WithholdingTaxAmount + liquidacion.WithholdingVatAmount + liquidacion.WithholdingIcaAmount;
        var lineas = new List<TreasuryFactLineV1> { Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Debito, liquidacion.NetAmount, dim, cruce: cruce) };
        if (liquidacion.CommissionAmount > 0m)
            lineas.Add(Linea(RolesDeTesoreriaT2.ComisionesDeRed, RolesDeTesoreria.Debito, liquidacion.CommissionAmount, dim, tercero, cruce));
        if (liquidacion.CommissionVatAmount > 0m)
            lineas.Add(Linea(RolesDeTesoreriaT2.IvaDescontable, RolesDeTesoreria.Debito, liquidacion.CommissionVatAmount, dim, tercero, cruce));
        if (retenciones > 0m) lineas.Add(Linea(RolesDeTesoreriaT2.RetencionesAFavor, RolesDeTesoreria.Debito, retenciones, dim, tercero, cruce));
        lineas.Add(Linea(RolesDeRecaudo.MedioPorCobrar, RolesDeTesoreria.Credito, liquidacion.GrossAmount, dim, tercero, cruce));
        var contenido = new ConciliacionDeAdquirenteRegistradaV1
        {
            SettlementPublicId = liquidacion.PublicId, Number = liquidacion.Number, BankAccountPublicId = cuenta.PublicId, AcquirerPublicId = redPublicId,
            AcquirerCode = redCodigo, SettlementDate = liquidacion.SettlementDate, GrossAmount = liquidacion.GrossAmount, CommissionAmount = liquidacion.CommissionAmount,
            CommissionVatAmount = liquidacion.CommissionVatAmount, WithholdingAmount = retenciones, NetAmount = liquidacion.NetAmount,
            CollectedAmount = liquidacion.CollectedAmount, Difference = liquidacion.Difference, StatementLinePublicId = liquidacion.StatementLinePublicId,
            ThirdPartyPersonPublicId = tercero, RegisteredBy = await UsuarioAsync(ct), ApprovedBy = aprobada ? await UsuarioAsync(ct) : null, Lines = lineas,
        };
        return await EmitirAsync(contenido, liquidacion.PublicId, ClaseConciliacionDeAdquirente, TipoDeTraslado, liquidacion.Number, liquidacion.SettlementDate,
            cuenta.BranchId, tercero, ClavesDeEvento.Confirmacion, ct);
    }
}
