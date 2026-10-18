using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>Un crédito de la novedad, cargado y reconstruido a la fecha de la decisión. (nuevo)</summary>
public sealed record CreditoDeLaNovedad(Loan Credito, NoveltyLoanRole Papel, CreditoCargado Cargado, EstadoDelCredito Estado);

/// <summary>
/// Una novedad calculada a una fecha (feature 013, K3, T0878, T0881; FR-091, T29): sus créditos reconstruidos, lo que capitaliza por concepto y cuota,
/// el saldo de capital del principal y el de los recogidos, la entrada del motor y el plan nuevo (las cuotas desde la fecha), el control de usura
/// vigente a esa fecha, el conteo de reestructuraciones que deja y la calificación hoy y después (un reestructurado nunca mejora: el piso es la de
/// hoy, FR-093). Simular y aplicar calculan lo mismo con esta pieza. (nuevo)
/// </summary>
public sealed record NovedadCalculada(
    LoanNovelty Novedad,
    DateOnly Fecha,
    IReadOnlyList<CreditoDeLaNovedad> Creditos,
    NewTermsInput Terminos,
    IReadOnlyList<PartidaDeNovedad> Capitalizaciones,
    decimal SaldoDeCapital,
    decimal Recogido,
    EntradaDeNovedadDePlan Entrada,
    PlanDePagos Plan,
    ResultadoDeUsura Usura,
    int ReestructuracionesDespues,
    RatingCategory CalificacionHoy,
    RatingCategory CalificacionDespues,
    IReadOnlyList<string> Avisos)
{
    public CreditoDeLaNovedad Principal => Creditos[0];

    public IEnumerable<CreditoDeLaNovedad> Recogidos => Creditos.Skip(1);

    public decimal Capitalizado => Capitalizaciones.Sum(c => c.Monto);

    public decimal CapitalNuevo => SaldoDeCapital + Recogido + Capitalizado;

    public bool CreaEnlazado => Novedad.CreaCreditoEnlazado;

    /// <summary>La huella de la simulación (<c>SimulationSha256</c>): lo que se decide es exactamente esto.</summary>
    public string Huella => new HuellaDeCartera()
        .Identificador("novelty", Novedad.PublicId)
        .Enumeracion<NoveltyMechanism>("mechanism", Novedad.Mechanism)
        .Enumeracion<NoveltyCause>("cause", Novedad.Cause)
        .Fecha("date", Fecha)
        .Lista("loans", Creditos, (h, c) => h.Texto("number", c.Credito.Number).Enumeracion<NoveltyLoanRole>("role", c.Papel))
        .Decimal("principalBalance", SaldoDeCapital)
        .Decimal("collected", Recogido)
        .Lista("capitalized", Capitalizaciones, (h, p) => h.Enumeracion<LoanConcept>("concept", p.Concepto).Entero("installment", p.Cuota).Decimal("amount", p.Monto))
        .Lista("schedule", Plan.Cuotas, (h, q) => h.Entero("n", q.Numero).Fecha("due", q.Vencimiento).Decimal("principal", q.Capital).Decimal("interest", q.Interes))
        .Enumeracion<VeredictoDeUsura>("usury", Usura.Veredicto)
        .Entero("restructuringCount", ReestructuracionesDespues)
        .Enumeracion<RatingCategory>("ratingAfter", CalificacionDespues)
        .Calcular();

    /// <summary>La simulación tal como la ven la pantalla y el acta.</summary>
    public NoveltySimulationDto Dto(string prevalidacion, string? concentracion) => new(
        Novedad.Cause,
        ExplicacionDeLaCausa(Novedad.CauseDeterminationJson),
        Fecha,
        SaldoDeCapital,
        Recogido,
        Capitalizado,
        CapitalNuevo,
        Plan.PrimeraCuotaRegenerada,
        Plan.Cuotas.Select(q => new NoveltyScheduleRowDto(q.Numero, q.Vencimiento, q.Dias, q.Capital, q.Interes, q.Seguro, q.SaldoFinal)).ToList(),
        CalificacionHoy.ToString(),
        CalificacionDespues.ToString(),
        ReestructuracionesDespues,
        new NoveltyUsuryDto(Usura.Veredicto.ToString(), Usura.EfectivaConCargos, Usura.Usura, Usura.Holgura),
        concentracion,
        prevalidacion,
        Avisos,
        Huella);

    private static string ExplicacionDeLaCausa(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            return d.RootElement.TryGetProperty("explanation", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}

/// <summary>Lo que el enrutamiento de una novedad necesita de su simulación (se guarda con ella en <c>SimulationJson</c>). (nuevo)</summary>
public sealed record DatosDeEnrutamientoDeNovedad(decimal Capitalized, int Installments, string Periodicity, int DaysPastDue);

/// <summary>Lo que se guarda en <c>SimulationJson</c> al radicar: la simulación, los datos del enrutamiento y el enrutamiento de ese día. (nuevo)</summary>
public sealed record SimulacionGuardada(NoveltySimulationDto Simulation, DatosDeEnrutamientoDeNovedad Routing, SubjectRoutingDto? RoutingResult,
    int? MatrixId = null, int? LevelId = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static SimulacionGuardada? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return null;
        try
        {
            return JsonSerializer.Deserialize<SimulacionGuardada>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// El cálculo de una novedad (feature 013, K3, T0878, T0881; FR-090, FR-091, FR-093; T29): la causa por <see cref="ClasificadorDeNovedad"/> y el plan
/// nuevo por <see cref="MotorDeCartera.SimularNovedad"/> sobre la reconstrucción de cada crédito a la fecha de la decisión. Lo usan la radicación (la
/// causa), la simulación, el radicado (la huella), la decisión (la usura de hoy) y la aplicación (la misma cuenta, ahora escrita). No escribe. (nuevo)
/// </summary>
public sealed class CalculoDeNovedad(IApplicationDbContext db, CargadorDeReconstruccion cargador, INormativaVigente normativa)
{
    /// <summary>Los conceptos de interés que la novedad capitaliza cuando lo pide (<c>capitalizeInterest</c>).</summary>
    public static readonly LoanConcept[] InteresesCapitalizables =
        [LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense];

    /// <summary>Los créditos cargados y reconstruidos a <paramref name="fecha"/> (el principal primero). Un crédito que no se puede reconstruir falla.</summary>
    public async Task<Result<IReadOnlyList<CreditoDeLaNovedad>>> CargarAsync(IReadOnlyList<(Loan Credito, NoveltyLoanRole Papel)> creditos, DateOnly fecha,
        CancellationToken ct, DateOnly? seriesHasta = null)
    {
        var cargados = await cargador.CargarAsync(creditos.Select(c => c.Credito.Id).ToList(), null, seriesHasta is { } h && h > fecha ? h : fecha, ct);
        var salida = new List<CreditoDeLaNovedad>();
        foreach (var (credito, papel) in creditos)
        {
            if (!cargados.TryGetValue(credito.Id, out var cargado)) return Result.Failure<IReadOnlyList<CreditoDeLaNovedad>>(Settlement.LecturaDelCredito.Inexistente());
            if (!cargado.Listo) return Result.Failure<IReadOnlyList<CreditoDeLaNovedad>>(cargado.Error!);
            try
            {
                salida.Add(new CreditoDeLaNovedad(credito, papel, cargado, MotorDeCartera.Reconstruir(cargado.Entrada!, fecha)));
            }
            catch (ReglaDeCarteraException ex)
            {
                return Result.Failure<IReadOnlyList<CreditoDeLaNovedad>>(ErroresDeGobierno.De(ex.Problema));
            }
        }
        return Result.Success<IReadOnlyList<CreditoDeLaNovedad>>(salida);
    }

    /// <summary>
    /// La causa (FR-090): el Anexo 1 vigente, el segmento SARC del principal, su mora hoy, el historial de mora de los meses que la norma observa (las
    /// posiciones diarias y el historial migrado), y las marcas que la hacen reestructuración (acuerdo de insolvencia, recogida en mora).
    /// </summary>
    public async Task<Result<ClasificacionDeNovedad>> ClasificarAsync(NoveltyMechanism mecanismo, NoveltyRequestMotive motivo, CreditoDeLaNovedad principal,
        IReadOnlyList<CreditoDeLaNovedad> recogidos, bool acuerdoDeInsolvencia, DateOnly fecha, CancellationToken ct)
    {
        var anexo = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo1, fecha, Anexo1.Leer, ct: ct);
        if (anexo.IsFailure)
            return Result.Failure<ClasificacionDeNovedad>(Risk.ErroresDeRiesgo.SinNorma($"no hay Anexo 1 del SARC vigente el {fecha:yyyy-MM-dd}. {anexo.Error.Message}"));
        var segmento = principal.Cargado.Series?.SegmentoSarc ?? principal.Cargado.Vigentes.ModalityCode;
        var historial = await HistorialAsync(principal.Credito.Id, fecha, ct);
        var entrada = new NovedadParaClasificar(mecanismo, motivo, segmento, principal.Estado.AlturaDeMora.Dias, fecha)
        {
            Historial = historial,
            AcuerdoDeInsolvencia = acuerdoDeInsolvencia,
            RecogeCreditosEnMora = recogidos.Any(r => r.Estado.AlturaDeMora.Dias > 0),
        };
        return Result.Success(ClasificadorDeNovedad.Clasificar(anexo.Value.Documento, entrada));
    }

    /// <summary>
    /// La mora máxima de cada mes de los últimos doce: el historial migrado o cargado (<c>LND_LoanArrearsHistory</c>) y las posiciones diarias (la
    /// última versión de cada día). El clasificador toma los meses que la norma observa.
    /// </summary>
    private async Task<IReadOnlyList<MoraDelMes>> HistorialAsync(int loanId, DateOnly fecha, CancellationToken ct)
    {
        var desde = new DateOnly(fecha.Year, fecha.Month, 1).AddMonths(-12);
        var anioDesde = (short)desde.Year;
        var cargados = await db.LoanArrearsHistory.AsNoTracking().Where(h => h.LoanId == loanId && !h.IsDeleted && h.PeriodYear >= anioDesde)
            .Select(h => new { h.PeriodYear, h.PeriodMonth, h.MaxDaysPastDue }).ToListAsync(ct);
        var posiciones = await db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == loanId && p.PositionDate >= desde && p.PositionDate <= fecha)
            .Select(p => new { p.PositionDate, p.DaysPastDue }).ToListAsync(ct);
        return cargados.Select(h => new MoraDelMes(h.PeriodYear, h.PeriodMonth, h.MaxDaysPastDue))
            .Concat(posiciones.GroupBy(p => (p.PositionDate.Year, p.PositionDate.Month)).Select(g => new MoraDelMes(g.Key.Year, g.Key.Month, g.Max(p => p.DaysPastDue))))
            .GroupBy(m => (m.Anio, m.Mes)).Select(g => new MoraDelMes(g.Key.Anio, g.Key.Mes, g.Max(m => m.MaxDiasDeMora)))
            .OrderBy(m => m.Anio).ThenBy(m => m.Mes).ToList();
    }

    /// <summary>Los créditos de una novedad guardada (el principal y los que recoge), sin seguimiento.</summary>
    public async Task<IReadOnlyList<(Loan Credito, NoveltyLoanRole Papel)>> CreditosAsync(LoanNovelty n, CancellationToken ct)
    {
        var principal = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == n.LoanId, ct);
        var recogidos = await (from x in db.LoanNoveltyLoans.AsNoTracking()
                               join l in db.Loans.AsNoTracking() on x.LoanId equals l.Id
                               where x.LoanNoveltyId == n.Id && x.Role == NoveltyLoanRole.Collected && !x.IsDeleted
                               orderby l.Number
                               select l).ToListAsync(ct);
        return [(principal, NoveltyLoanRole.Modified), .. recogidos.Select(r => (r, NoveltyLoanRole.Collected))];
    }

    /// <summary>
    /// Calcula la novedad a <paramref name="fecha"/>: lo capitalizado (todo el interés que se debe, si se pide), el plan nuevo desde la fecha
    /// —prórroga, cambio de fecha o de tasa: sobre todo el capital pendiente y siguiendo la numeración; novación o refinanciación: el enlazado desde la
    /// cuota 1 sobre el saldo del principal y el de los recogidos—, la usura vigente a la fecha, el conteo de reestructuraciones y la calificación.
    /// </summary>
    public async Task<Result<NovedadCalculada>> CalcularAsync(LoanNovelty n, DateOnly fecha, CancellationToken ct, DateOnly? seriesHasta = null)
    {
        var cargados = await CargarAsync(await CreditosAsync(n, ct), fecha, ct, seriesHasta);
        if (cargados.IsFailure) return Result.Failure<NovedadCalculada>(cargados.Error);
        return await CalcularAsync(n, fecha, cargados.Value, ct);
    }

    public async Task<Result<NovedadCalculada>> CalcularAsync(LoanNovelty n, DateOnly fecha, IReadOnlyList<CreditoDeLaNovedad> creditos, CancellationToken ct)
    {
        var terminos = NewTermsInput.DeJson(n.ProposedTermsJson);
        var principal = creditos[0];
        var plan = principal.Estado.Plan;
        if (plan is null) return Result.Failure<NovedadCalculada>(ErroresDeNovedades.CreditoNoActivo(principal.Credito.Number, principal.Credito.Status.ToString()));

        if (n.Mechanism == NoveltyMechanism.BranchChange)
        {
            // T0883: el cambio de oficina no regenera el plan ni capitaliza; lo que cambia es la sucursal contable de los saldos.
            var sinCambio = new EntradaDeNovedadDePlan(n.Mechanism, fecha, plan.Entrada, principal.Estado.Saldo(LoanConcept.Principal), plan.Cuotas.Count)
            {
                PrimeraCuota = plan.PrimeraCuotaRegenerada,
            };
            var calificacionDeHoy = await CalificacionOficialAsync(principal.Credito.Id, fecha, ct);
            return Result.Success(new NovedadCalculada(n, fecha, creditos, terminos, [], sinCambio.SaldoDeCapital, 0m, sinCambio, plan,
                ControlDeUsura.Evaluar(plan, null), principal.Cargado.Vigentes.RestructuringCount, calificacionDeHoy, calificacionDeHoy, []));
        }
        var capitalizaciones = terminos.CapitalizeInterest
            ? principal.Estado.Saldos.Where(s => InteresesCapitalizables.Contains(s.Concepto) && s.Valor > 0m)
                .OrderBy(s => s.Cuota ?? 0).ThenBy(s => (int)s.Concepto)
                .Select(s => new PartidaDeNovedad(s.Concepto, s.Cuota, s.Valor)).ToList()
            : [];
        var saldo = principal.Estado.Saldo(LoanConcept.Principal);
        var recogidos = creditos.Skip(1).Select(c => c.Estado.Saldo(LoanConcept.Principal)).Where(s => s > 0m).ToList();
        var vencidas = plan.Cuotas.Where(c => c.Vencimiento <= fecha).ToList();
        var enlazado = n.CreaCreditoEnlazado;
        var primera = enlazado ? 1 : vencidas.Count == 0 ? plan.Cuotas[0].Numero : vencidas[^1].Numero + 1;
        var cuotas = terminos.Installments ?? Math.Max(1, plan.Cuotas.Count(c => c.Vencimiento > fecha));

        var entrada = new EntradaDeNovedadDePlan(n.Mechanism, fecha, plan.Entrada, saldo, cuotas)
        {
            InteresCapitalizado = capitalizaciones.Sum(c => c.Monto),
            SaldosRecogidos = recogidos,
            TasaEfectivaAnual = terminos.Rate,
            Periodicidad = terminos.Periodicity,
            DiaDePago = terminos.PaymentDay,
            PrimerVencimiento = terminos.FirstDueDate,
            PrimeraCuota = primera,
        };
        PlanDePagos nuevo;
        ResultadoDeUsura usura;
        try
        {
            (nuevo, usura) = MotorDeCartera.SimularNovedad(entrada, Usura(principal, fecha));
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<NovedadCalculada>(ErroresDeGobierno.De(ex.Problema));
        }

        var antes = principal.Cargado.Vigentes.RestructuringCount;
        var despues = n.Cause == NoveltyCause.Restructuring ? antes + 1 : antes;
        var hoy = await CalificacionOficialAsync(principal.Credito.Id, fecha, ct);
        var luego = await CalificacionResultanteAsync(n, hoy, fecha, ct);
        var avisos = new List<string>();
        if (usura.Veredicto == VeredictoDeUsura.SinUsura)
            avisos.Add($"No hay usura certificada de la modalidad el {fecha:yyyy-MM-dd}: el plan nuevo no se pudo controlar contra la usura.");
        if (despues >= ReglaDeSuspensionDeCartera.ReestructuracionesQueSuspenden)
            avisos.Add("Con esta reestructuración el crédito queda reestructurado más de una vez: su interés se reconoce sólo cuando se cobre.");
        if (n.Cause == NoveltyCause.Modification)
            avisos.Add("Es una modificación: si bajo las condiciones nuevas el crédito vuelve a la mora que fija la norma, pasa a ser reestructuración.");
        return Result.Success(new NovedadCalculada(n, fecha, creditos, terminos, capitalizaciones, saldo, recogidos.Sum(), entrada, nuevo, usura, despues, hoy, luego,
            avisos));
    }

    /// <summary>
    /// La calificación resultante (FR-093): una modificación no la cambia; una reestructuración nunca queda mejor que la que tenía al pedirla
    /// (<c>RatingAtRequest</c>, sellada al pedir), que la de hoy ni que el mínimo de la norma para un reestructurado (<c>Anexo1.Reestructurados</c>),
    /// lo mismo que aplica <c>MotorDeCalificacion</c> en el cierre siguiente. Las mejoras por tramos de cuotas las da ese motor después.
    /// </summary>
    public async Task<RatingCategory> CalificacionResultanteAsync(LoanNovelty n, RatingCategory hoy, DateOnly fecha, CancellationToken ct)
    {
        if (n.Cause != NoveltyCause.Restructuring) return hoy;
        var piso = Enum.IsDefined(n.RatingAtRequest) && n.RatingAtRequest > hoy ? n.RatingAtRequest : hoy;
        var anexo = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo1, fecha, Anexo1.Leer, ct: ct);
        if (anexo.IsSuccess && anexo.Value.Documento.Reestructurados is { } regla && regla.CategoriaMinima > piso) piso = regla.CategoriaMinima;
        return piso;
    }

    /// <summary>La usura certificada de la modalidad del principal a la fecha (de sus series); nula si no hay o no la exige.</summary>
    public static UsuraCertificada? Usura(CreditoDeLaNovedad principal, DateOnly fecha)
    {
        if (principal.Cargado.Series is not { ExigeUsura: true } series) return null;
        var vigente = VigenciaBitemporal.Vigente(series.Usura, fecha, DateTime.MaxValue);
        return vigente is null ? null : new UsuraCertificada(vigente.Ea, vigente.PublicId, series.ClaveDeUsura!, vigente.ValidFrom);
    }

    /// <summary>La calificación oficial del crédito (la del último cierre vigente; sin cierre, la de crédito nuevo).</summary>
    public async Task<RatingCategory> CalificacionOficialAsync(int loanId, DateOnly fecha, CancellationToken ct)
    {
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(loanId, fecha, ct);
        return Enum.TryParse<RatingCategory>(categoria.Category, out var c) ? c : RatingCategory.A;
    }

    /// <summary>Los meses que cubre un plan de <paramref name="cuotas"/> de <paramref name="periodicidad"/> (para las reglas de plazo de la matriz).</summary>
    public static int Meses(int cuotas, RepaymentPeriodicity periodicidad) =>
        (int)Math.Ceiling(cuotas * Math.Max(1, (int)periodicidad) / 30m);

    public static string Texto(decimal valor) => valor.ToString("#,0.##", CultureInfo.GetCultureInfo("es-CO"));
}

/// <summary>El umbral de reestructuraciones que suspende, leído del dominio para los avisos. (nuevo)</summary>
internal static class ReglaDeSuspensionDeCartera
{
    public const int ReestructuracionesQueSuspenden = Domain.Lending.Accrual.ReglaDeSuspension.ReestructuracionesQueSuspenden;
}
