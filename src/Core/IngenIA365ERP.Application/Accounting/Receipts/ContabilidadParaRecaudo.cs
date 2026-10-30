using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Receipts;

/// <summary>
/// Lo que Contabilidad responde a Recaudo (feature 015, T0091, T0259; R-22, R-23; FR-004, FR-209; contracts/interfaces.md §3):
/// <list type="bullet">
/// <item>la validación previa por la matriz <c>ACC_ReceiptsPostingRules</c> (<see cref="EvaluacionPreviaDeModulo"/>), con la política
/// <c>Contabilidad.FaltaDeRegla</c> de <c>RCD</c>;</item>
/// <item>el estado del período es el de siempre (<see cref="GetAccountingPeriodStatusQuery"/>, el mismo que lee Cartera);</item>
/// <item>el saldo contable de un rol (caja por oficina, partidas, saldos a favor) a una fecha, por <see cref="SaldosPorRol"/>: sin regla para el rol, «sin dato».</item>
/// </list>
/// Recaudo no conoce nada de esto: sólo la interfaz (<c>RecaudoNoConoceContabilidad</c>). Las cuentas por cobrar comerciales llegan con R1. Sin evaluador (las
/// pruebas que construyen la clase a mano) responde como antes de la fase 16: «contabilizable» y saldo «sin dato». (nuevo)
/// </summary>
public sealed class ContabilidadParaRecaudo(
    ISender sender, IApplicationDbContext? db = null, ResolutorDeLaMatriz? resolutor = null, EvaluacionPreviaDeModulo? evaluacion = null)
    : IContabilidadParaRecaudo
{
    /// <summary>La política que informa la validación previa sin evaluador.</summary>
    public const string PoliticaProvisional = "SinMatriz";

    public Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct) =>
        evaluacion is null
            ? Task.FromResult(new EvaluacionContableDto(true, PoliticaProvisional, []))
            : evaluacion.EvaluarAsync(MatricesDeRecaudoYTesoreria.Recaudo, combinaciones, ct);

    public async Task<EstadoDelPeriodoDto> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct)
    {
        var estado = await sender.Send(new GetAccountingPeriodStatusQuery(fecha), ct);
        return estado.IsSuccess ? estado.Value : new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null);
    }

    public Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct) =>
        db is null || resolutor is null
            ? Task.FromResult<IReadOnlyList<SaldoContableDto>>([])
            : SaldosPorRol.SaldoAsync(db, resolutor, MatricesDeRecaudoYTesoreria.Recaudo, solicitud, ct);

    /// <summary>
    /// Feature 015, R1 (T0396; FR-049, FR-167; E-04): las cuentas por cobrar comerciales pendientes de la persona, por factura (documento cruce), sobre las cuentas que la
    /// matriz de Recaudo tiene (o tuvo) para el rol <c>CuentasPorCobrarComerciales</c>: lo contabilizado por los demás módulos (la venta de Comercial, sus notas) neto
    /// de débitos y créditos. Lo que cobró Recaudo no entra aquí —Recaudo lo descuenta con sus propias aplicaciones, contabilizadas o no—, así un recibo que espera su lote
    /// no deja cobrar dos veces. Sin base o sin cuentas, nada. (nuevo)
    /// </summary>
    public async Task<IReadOnlyList<CuentaPorCobrarComercialDto>> CuentasPorCobrarComercialesAsync(Guid personPublicId, CancellationToken ct)
    {
        if (db is null) return [];
        // Toda cuenta que la matriz tuvo para el rol, vigente o no: una factura vieja sigue pendiente en la cuenta donde se cargó.
        var cuentas = await db.ReceiptsPostingRules.AsNoTracking().Where(r => r.Role == RolCuentasPorCobrarComerciales).Select(r => r.AccountId).Distinct()
            .ToListAsync(ct);
        if (cuentas.Count == 0) return [];
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == personPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return [];
        var filas = await (from l in db.JournalEntries.AsNoTracking()
                           join d in db.AccountingDocuments.AsNoTracking() on l.DocumentId equals d.Id
                           where l.IsPosted && l.PersonId == persona && cuentas.Contains(l.AccountId) && l.CrossDocumentNumber != null
                                 && d.OriginModule != Posting.ModuloContable.Recaudo
                           select new { l.CrossDocumentNumber, l.Date, l.Debit, l.Credit, l.Id, d.PublicId }).ToListAsync(ct);
        return filas.GroupBy(f => f.CrossDocumentNumber!)
            .Select(g => new CuentaPorCobrarComercialDto(g.OrderBy(x => x.Id).First().PublicId, g.Key, g.Min(x => x.Date), g.Sum(x => x.Debit - x.Credit)))
            .Where(c => c.Pending > 0m).OrderBy(c => c.Date).ToList();
    }

    /// <summary>El rol de la matriz de Recaudo de las cuentas por cobrar comerciales (contabilidad.md §5).</summary>
    public const string RolCuentasPorCobrarComerciales = "CuentasPorCobrarComerciales";

    /// <summary>La completitud de la matriz de Recaudo (fase 19, T0303): la misma consulta que la pantalla de completitud.</summary>
    public async Task<CompletitudContableDeModuloDto> CompletitudDeLaMatrizAsync(DateOnly fecha, CancellationToken ct) =>
        await CompletitudDeModulo.LeerAsync(sender, Posting.ModuloContable.Recaudo, await EstadoDelPeriodoAsync(fecha, ct), fecha, ct);
}

/// <summary>Lo común de la completitud de Recaudo y Tesorería para su activación (fase 19, T0303). (nuevo)</summary>
public static class CompletitudDeModulo
{
    public static async Task<CompletitudContableDeModuloDto> LeerAsync(ISender sender, string modulo, EstadoDelPeriodoDto estado, DateOnly fecha, CancellationToken ct)
    {
        if (!estado.AccountingInitialized) return new CompletitudContableDeModuloDto(false, false, 100m, 0);
        var r = await sender.Send(new GetModuleCompletenessQuery(modulo, fecha), ct);
        if (r.IsFailure) return new CompletitudContableDeModuloDto(true, false, 0m, 0);
        return new CompletitudContableDeModuloDto(true, r.Value.NotPosted, r.Value.Percent, r.Value.Total - r.Value.Complete);
    }
}
