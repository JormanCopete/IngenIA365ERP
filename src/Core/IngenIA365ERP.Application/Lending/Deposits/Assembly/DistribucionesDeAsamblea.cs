using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Assembly;

/// <summary>Lo que la entidad fija para la asamblea (<c>Asamblea.*</c>), leído a una fecha con sus defectos. (nuevo)</summary>
public sealed record ParametrosDeAsamblea(
    RevaluationBase Base,
    decimal UnidadDeRedondeo,
    string SerieDelIpc,
    SurplusDestination Destino,
    MemberAccountConcept ConceptoDeExcedentes,
    bool AmortizacionExcluyeDeudores);

/// <summary>
/// La simulación calculada (T203): el resultado del motor, la cuenta de aportes de cada línea y su cuenta de destino (excedentes a ahorro), lo amortizable,
/// el límite legal leído, los asociados sin cuenta de destino y el DTO con su huella. (nuevo)
/// </summary>
public sealed record SimulacionDeAsamblea(
    DistribucionCalculada Calculo,
    AssemblyDistributionSimulationDto Dto,
    IReadOnlyDictionary<int, MemberAccount> Cuentas,
    IReadOnlyDictionary<int, MemberAccount> Destinos,
    IReadOnlyList<int> PersonasSinDestino, // los que no tienen cuenta de destino: su excedente se capitaliza en aportes (E257)
    decimal? LimiteLegal,
    string? FuenteDelLimite,
    ParametrosDeAsamblea Parametros);

/// <summary>
/// Lo común de las decisiones de asamblea (feature 014, US12, T203; FR-045 a FR-047, D13; research §22): el alcance —una decisión de asamblea es de toda
/// la cooperativa, así que sólo la ve y la opera quien tiene alcance total (oficinas y líneas); fuera, la misma respuesta que lo inexistente—, los parámetros
/// de la entidad, el límite legal (el valor nacional <c>IPC</c> del ejercicio), la simulación sobre el libro de cuentas con
/// <see cref="RevalorizacionYExcedentes"/> y el DTO. Sólo lee. (nuevo)
///
/// <para>
/// Quiénes entran: los titulares de una cuenta de aportes <b>activa o inactiva</b> con base positiva en el ejercicio. Quedan fuera, con su aviso, los que
/// tienen un retiro aprobado, liquidado o por rehacer (su liquidación ya los cuenta) y, en la amortización con <c>Asamblea.AmortizacionExcluyeDeudores</c>,
/// los que tienen créditos vivos (FR-044: sus aportes los respaldan).
/// </para>
/// </summary>
public sealed class DistribucionesDeAsamblea(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeParametrosDeCartera parametros,
    IValoresNacionales valores,
    ReglasDeAportes reglas)
{
    /// <summary>El expediente de la segregación (radicar × decidir).</summary>
    public const string Expediente = SegregacionDeCaptaciones.DecisionDeAsamblea;

    /// <summary>El prefijo del número de la decisión (<c>LendingNumberKind.AssemblyDistribution</c>).</summary>
    public const string Prefijo = "ASM-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// ¿Tiene quien pregunta alcance total? Una decisión de asamblea abona o devuelve a asociados de todas las oficinas y líneas; sin alcance total la
    /// decisión no existe para él (<c>Lending.AssemblyDistribution.NotFound</c>, con el evento de fuera de alcance).
    /// </summary>
    public async Task<Result> AlcanceTotalAsync(Guid? recurso, CancellationToken ct)
    {
        var vigente = await alcance.ObtenerAsync(ct);
        if (vigente.TodasLasOficinas && vigente.TodasLasLineas) return Result.Success();
        await auditoria.EmitirFueraDeAlcanceAsync(Expediente, recurso, ct);
        return Result.Failure(ErroresDeAsamblea.Inexistente());
    }

    /// <summary>La decisión por su <c>PublicId</c>, con el alcance total de quien pregunta.</summary>
    public async Task<Result<AssemblyDistribution>> LeerAsync(Guid publicId, bool seguir, CancellationToken ct)
    {
        if (await AlcanceTotalAsync(publicId, ct) is { IsFailure: true } fuera) return Result.Failure<AssemblyDistribution>(fuera.Error);
        var consulta = seguir ? db.AssemblyDistributions : db.AssemblyDistributions.AsNoTracking();
        var d = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        return d is null ? Result.Failure<AssemblyDistribution>(ErroresDeAsamblea.Inexistente()) : Result.Success(d);
    }

    // ----------------------------------------------------------------------------------------------------- parámetros --

    /// <summary>Los parámetros <c>Asamblea.*</c> a la fecha; sin lectura (antes de K10 o sin valor), sus defectos del catálogo.</summary>
    public async Task<ParametrosDeAsamblea> ParametrosAsync(DateOnly fecha, CancellationToken ct)
    {
        var baseTexto = await TextoAsync(ParametrosDeCartera.AsambleaBaseDeRevalorizacion, fecha, ct);
        var redondeo = await TextoAsync(ParametrosDeCartera.AsambleaRedondeo, fecha, ct);
        var serie = await TextoAsync(ParametrosDeCartera.AsambleaSerieDelIpc, fecha, ct);
        var destino = await TextoAsync(ParametrosDeCartera.AsambleaDestinoDeExcedentes, fecha, ct);
        var clase = await TextoAsync(ParametrosDeCartera.AsambleaClaseDeAporteDeExcedentes, fecha, ct);
        var excluye = await TextoAsync(ParametrosDeCartera.AsambleaAmortizacionExcluyeDeudores, fecha, ct);
        return new ParametrosDeAsamblea(
            Enum.TryParse<RevaluationBase>(baseTexto, out var b) ? b : RevaluationBase.AverageBalance,
            string.Equals(redondeo, "Centavo", StringComparison.Ordinal) ? 1m / 100m : 1m,
            string.IsNullOrWhiteSpace(serie) ? "ANUAL" : serie.Trim(),
            Enum.TryParse<SurplusDestination>(destino, out var de) ? de : SurplusDestination.Contributions,
            string.Equals(clase, nameof(ContributionClass.Extraordinary), StringComparison.Ordinal)
                ? MemberAccountConcept.ContributionExtraordinary
                : MemberAccountConcept.ContributionOrdinary,
            !string.Equals(excluye, "false", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> TextoAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        return leido.IsSuccess ? leido.Value.Texto : ParametrosDeCartera.Definiciones.FirstOrDefault(d => d.Clave == clave)?.DefectoSeguro ?? string.Empty;
    }

    /// <summary>
    /// El límite legal de la revalorización (Decreto 3081 de 1990 art. 1; CBCF Título I Cap. V num. 5): el valor nacional <c>IPC</c> de la serie de la entidad
    /// que rige el 31 de enero siguiente al ejercicio —la variación anual de diciembre ya publicada por el DANE—. Sin él, <c>NationalValues.Missing</c>.
    /// </summary>
    public async Task<Result<ValorNacionalVigente>> LimiteLegalAsync(int ejercicio, string serie, CancellationToken ct) =>
        await valores.LeerAsync(CodigosDeValorNacional.Ipc, serie, new DateOnly(ejercicio + 1, 1, 31), ct: ct);

    // ----------------------------------------------------------------------------------------------------- simulación --

    /// <summary>
    /// La simulación de la decisión a <paramref name="hoy"/> sobre el libro de cuentas: quiénes entran, su base del ejercicio, lo amortizable, la cuenta de
    /// destino de los excedentes a ahorro, el límite del IPC (revalorización) y el cálculo del motor, con su huella. Sólo lee.
    /// </summary>
    public async Task<Result<SimulacionDeAsamblea>> SimularAsync(AssemblyDistribution d, DateOnly hoy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(d);
        var p = await ParametrosAsync(hoy, ct);
        var unidad = d.Status == AssemblyDistributionStatus.Simulated ? p.UnidadDeRedondeo : d.RoundingUnit;
        decimal? limite = null;
        string? fuente = null;
        DateOnly? referencia = null;
        if (d.Kind == AssemblyDistributionKind.Revaluation)
        {
            var ipc = await LimiteLegalAsync(d.FiscalYear, p.SerieDelIpc, ct);
            if (ipc.IsFailure) return Result.Failure<SimulacionDeAsamblea>(ipc.Error);
            limite = ipc.Value.Valor;
            referencia = ipc.Value.ReferenceDate ?? ipc.Value.ValidFrom;
            fuente = string.Create(CultureInfo.InvariantCulture, $"IPC {p.SerieDelIpc} {referencia:yyyy-MM-dd}: {ipc.Value.Fuente}");
        }

        var avisos = new List<string>();
        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.Nature == LineNature.Contribution && !a.IsDeleted
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
            .ToListAsync(ct);
        var personas = cuentas.Select(c => c.HolderPersonId).Distinct().ToList();

        // Fuera, con aviso: el retiro en curso (su liquidación ya cuenta sus aportes) y, en la amortización, los deudores (FR-044).
        var enRetiro = await db.MembershipWithdrawals.AsNoTracking()
            .Where(w => personas.Contains(w.PersonId) && !w.IsDeleted
                        && (w.Status == MembershipWithdrawalStatus.Approved || w.Status == MembershipWithdrawalStatus.Settled
                            || w.Status == MembershipWithdrawalStatus.ToRedo))
            .Select(w => w.PersonId).Distinct().ToListAsync(ct);
        if (enRetiro.Count > 0)
            avisos.Add($"{enRetiro.Count} asociado(s) con el retiro aprobado, liquidado o por rehacer quedan fuera: su liquidación ya cuenta sus aportes.");
        var fuera = enRetiro.ToHashSet();
        if (d.Kind == AssemblyDistributionKind.Amortization && p.AmortizacionExcluyeDeudores)
        {
            var deudores = await db.Loans.AsNoTracking()
                .Where(l => personas.Contains(l.DebtorPersonId) && !l.IsDeleted
                            && (l.Status == LoanStatus.Active || l.Status == LoanStatus.PendingDisbursement || l.Status == LoanStatus.WrittenOff))
                .Select(l => l.DebtorPersonId).Distinct().ToListAsync(ct);
            var nuevos = deudores.Where(x => !fuera.Contains(x)).ToList();
            if (nuevos.Count > 0)
                avisos.Add($"{nuevos.Count} asociado(s) con créditos vivos quedan fuera de la amortización: sus aportes los respaldan (FR-044).");
            fuera.UnionWith(nuevos);
        }
        cuentas = cuentas.Where(c => !fuera.Contains(c.HolderPersonId)).OrderBy(c => c.HolderPersonId).ThenBy(c => c.Id).ToList();
        var ids = cuentas.Select(c => c.Id).ToList();

        // La base: los movimientos de aportes por fecha valor hasta el cierre del ejercicio.
        var cierre = new DateOnly(d.FiscalYear, 12, 31);
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToArray();
        var movimientos = (await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && conceptos.Contains(t.Concept) && t.ValueDate <= cierre)
                .GroupBy(t => new { t.MemberAccountId, t.ValueDate })
                .Select(g => new { g.Key.MemberAccountId, g.Key.ValueDate, Monto = g.Sum(t => t.Amount) })
                .ToListAsync(ct))
            .GroupBy(m => m.MemberAccountId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MovimientoDeAportes>)g.Select(m => new MovimientoDeAportes(m.ValueDate, m.Monto)).ToList());

        // Lo amortizable: el saldo de hoy menos el mínimo no reducible de la versión vigente (FR-042).
        var amortizable = new Dictionary<int, decimal>();
        if (d.Kind == AssemblyDistributionKind.Amortization)
        {
            var saldos = await LectorDeCuentas.SaldosAsync(db, ids, ct);
            foreach (var c in cuentas)
            {
                var version = await reglas.VersionAsync(c, hoy, ct);
                var minimo = version is null ? 0m : await reglas.MinimoAsync(version, c.HolderPersonId, hoy, ct);
                amortizable[c.Id] = Math.Max(0m, LectorDeCuentas.SaldoDelTitular(LineNature.Contribution, saldos.GetValueOrDefault(c.Id) ?? []) - minimo);
            }
        }

        var calculo = RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(d.Kind, d.FiscalYear, d.BaseMethod, d.DecidedFraction, d.DecidedValue,
            d.FundAmount, limite, unidad,
            cuentas.Select(c => new AsociadoEnLaDistribucion(c.HolderPersonId, c.Id, movimientos.GetValueOrDefault(c.Id) ?? [],
                d.Kind == AssemblyDistributionKind.Amortization ? amortizable.GetValueOrDefault(c.Id) : null)).ToList()));
        var porId = cuentas.ToDictionary(c => c.Id);

        // Los excedentes a ahorro: la cuenta viva del titular en la línea de destino (la más reciente).
        var destinos = new Dictionary<int, MemberAccount>();
        var sinDestino = new List<int>();
        if (d.Kind == AssemblyDistributionKind.SurplusDistribution && d.SurplusDestination == SurplusDestination.Savings && d.DestinationLineId is { } lineaDestino)
        {
            var titulares = calculo.Lineas.Where(l => l.Aplicado > 0m).Select(l => l.PersonaId).ToList();
            var deAhorro = await db.MemberAccounts.AsNoTracking()
                .Where(a => a.CreditLineId == lineaDestino && titulares.Contains(a.HolderPersonId) && !a.IsDeleted
                            && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
                .ToListAsync(ct);
            foreach (var l in calculo.Lineas.Where(l => l.Aplicado > 0m))
            {
                var cuenta = deAhorro.Where(a => a.HolderPersonId == l.PersonaId).OrderByDescending(a => a.Id).FirstOrDefault();
                if (cuenta is null) sinDestino.Add(l.PersonaId);
                else destinos[l.CuentaId] = cuenta;
            }
            if (sinDestino.Count > 0)
                avisos.Add($"{sinDestino.Count} asociado(s) no tienen cuenta viva en la línea de ahorro de destino: su excedente se capitaliza en sus aportes, sin abrirles cuenta.");
        }
        if (calculo.Lineas.Count(l => l.Aplicado > 0m) == 0) avisos.Add("Con esta decisión ningún asociado recibe nada.");
        if (d.Kind == AssemblyDistributionKind.Revaluation && limite is null)
            avisos.Add("El IPC del ejercicio no trae valor: la revalorización no se limita.");

        var huella = Huella(d, unidad, limite, calculo, destinos);
        var lineas = await LineasDtoAsync(calculo.Lineas.Select(l => (l, porId[l.CuentaId], destinos.GetValueOrDefault(l.CuentaId), (string?)null, (string?)null))
            .ToList(), ct);
        var dto = new AssemblyDistributionSimulationDto(d.PublicId, hoy, calculo.TasaDecidida, calculo.TasaAplicada, lineas,
            new LimiteLegalDto(calculo.LimitadaPorElLimiteLegal, limite, fuente, referencia), new ProrrateoDto(calculo.Prorrateada, calculo.Factor),
            new TotalesDeAsambleaDto(calculo.BaseTotal, calculo.TotalCalculado, calculo.TotalAplicado, calculo.Remanente, calculo.Lineas.Count),
            calculo.Explicacion, avisos, huella);
        return Result.Success(new SimulacionDeAsamblea(calculo, dto, porId, destinos, sinDestino, limite, fuente, p));
    }

    /// <summary>La huella de lo que se aplicaría: la decisión, la unidad, el límite y cada línea con su destino.</summary>
    private static string Huella(AssemblyDistribution d, decimal unidad, decimal? limite, DistribucionCalculada calculo,
        IReadOnlyDictionary<int, MemberAccount> destinos) =>
        new HuellaDeCartera()
            .Identificador("distribution", d.PublicId)
            .Enumeracion("kind", (AssemblyDistributionKind?)d.Kind)
            .Entero("year", d.FiscalYear)
            .Enumeracion("base", (RevaluationBase?)d.BaseMethod)
            .Decimal("percent", d.DecidedFraction, 6)
            .Decimal("value", d.DecidedValue, 2)
            .Decimal("fund", d.FundAmount, 2)
            .Decimal("unit", unidad, 2)
            .Decimal("limit", limite, 6)
            .Enumeracion("destination", d.SurplusDestination)
            .Entero("destinationLine", d.DestinationLineId)
            .Lista("lines", calculo.Lineas, (h, l) => h
                .Entero("person", l.PersonaId)
                .Entero("account", l.CuentaId)
                .Decimal("base", l.Base, 2)
                .Decimal("calculated", l.Calculado, 2)
                .Decimal("applied", l.Aplicado, 2)
                .Entero("destinationAccount", destinos.GetValueOrDefault(l.CuentaId)?.Id))
            .Calcular();

    // ------------------------------------------------------------------------------------------------------------ DTO --

    private async Task<IReadOnlyList<AssemblyDistributionLineDto>> LineasDtoAsync(
        IReadOnlyList<(LineaDeDistribucion Linea, MemberAccount Cuenta, MemberAccount? Destino, string? Operacion, string? Orden)> lineas, CancellationToken ct)
    {
        var personas = await LectorDeCuentas.PersonasAsync(db, lineas.Select(l => l.Linea.PersonaId).Distinct().ToList(), ct);
        return lineas.Select(l => new AssemblyDistributionLineDto(personas[l.Linea.PersonaId], l.Cuenta.PublicId, l.Cuenta.Number, l.Linea.Base, l.Linea.Calculado,
            l.Linea.Aplicado, l.Linea.LimitadaPorMinimo, l.Destino?.PublicId, l.Destino?.Number, l.Operacion, l.Orden)).ToList();
    }

    /// <summary>La decisión con (o sin) sus líneas aplicadas.</summary>
    public async Task<AssemblyDistributionDto> DtoAsync(AssemblyDistribution d, bool conLineas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(d);
        var punto = d.DecisionSessionItemId is { } item
            ? await (from i in db.SessionItems.AsNoTracking()
                     join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                     where i.Id == item
                     select new { i.PublicId, s.Number }).FirstOrDefaultAsync(ct)
            : null;
        var corrige = d.CorrectsDistributionId is { } c
            ? await db.AssemblyDistributions.AsNoTracking().Where(x => x.Id == c).Select(x => new { x.PublicId, x.Number }).FirstOrDefaultAsync(ct)
            : null;
        var linea = d.DestinationLineId is { } l
            ? await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == l).Select(x => x.Code).FirstOrDefaultAsync(ct)
            : null;
        IReadOnlyList<AssemblyDistributionLineDto> lineas = [];
        if (conLineas && d.SimulationVersion > 0)
        {
            var filas = await db.AssemblyDistributionLines.AsNoTracking()
                .Where(x => x.AssemblyDistributionId == d.Id && x.SimulationVersion == d.SimulationVersion).OrderBy(x => x.PersonId).ToListAsync(ct);
            var idsCuentas = filas.Select(f => f.ContributionAccountId).Concat(filas.Where(f => f.DestinationAccountId != null).Select(f => f.DestinationAccountId!.Value))
                .Distinct().ToList();
            var cuentas = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => idsCuentas.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            var idsOps = filas.Where(f => f.OperationId != null).Select(f => f.OperationId!.Value).ToList();
            var ops = await db.MemberAccountOperations.AsNoTracking().Where(o => idsOps.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
            var idsOrdenes = filas.Where(f => f.PayoutOrderId != null).Select(f => f.PayoutOrderId!.Value).ToList();
            var ordenes = await db.AccountPayoutOrders.AsNoTracking().Where(o => idsOrdenes.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
            lineas = await LineasDtoAsync(filas.Select(f => (new LineaDeDistribucion(f.PersonId, f.ContributionAccountId, f.BaseAmount, f.CalculatedAmount,
                    f.ProratedAmount, f.LimitedByMinimum), cuentas[f.ContributionAccountId],
                f.DestinationAccountId is { } dId ? cuentas[dId] : null,
                f.OperationId is { } o ? ops.GetValueOrDefault(o) : null,
                f.PayoutOrderId is { } po ? ordenes.GetValueOrDefault(po) : null)).ToList(), ct);
        }
        return new AssemblyDistributionDto(d.PublicId, d.Number, d.Kind, d.FiscalYear, d.Status, d.BaseMethod, d.DecidedFraction, d.DecidedValue, d.FundAmount,
            d.SurplusDestination, linea, d.RoundingUnit, punto?.PublicId, punto?.Number, corrige?.PublicId, corrige?.Number, d.LegalLimitFraction,
            d.LegalLimitSource, d.CalculatedTotal, d.ProrationFactor, d.AppliedTotal, Soportes(d.SupportAttachmentPublicIdsJson), d.Reason, d.RegisteredAt,
            d.AppliedAt, d.AppliedOn, d.ReversedAt, d.ReversalReason, d.VoidedAt, lineas);
    }

    /// <summary>Los soportes de la decisión (lista JSON de <c>PublicId</c>).</summary>
    public static IReadOnlyList<Guid> Soportes(string json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Guid>>(json, Json) ?? [];

    public static string SoportesJson(IEnumerable<Guid> soportes) => JsonSerializer.Serialize(soportes.Distinct().ToList(), Json);
}
