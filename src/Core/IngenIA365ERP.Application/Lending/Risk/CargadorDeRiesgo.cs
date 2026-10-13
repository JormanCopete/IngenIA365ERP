using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// Un crédito tal como lo mira el riesgo a un corte con lo conocido en R: el crédito, sus condiciones vigentes, la reconstrucción
/// (<see cref="EstadoDelCredito"/>), la combinación del catálogo, el deudor y sus marcas, sus codeudores, sus garantías admisibles, la
/// calificación por análisis vigente a R, su línea del cierre anterior y la insolvencia del deudor. (nuevo)
/// </summary>
public sealed record CreditoDeRiesgo(
    Loan Credito,
    LoanTermsVersion Vigentes,
    EstadoDelCredito Estado,
    CombinacionDeModalidad Combinacion,
    bool DeudorEsEmpleado,
    Guid OficinaPublicId)
{
    public IReadOnlyList<int> Codeudores { get; init; } = [];

    public IReadOnlyList<GarantiaParaDeterioro> Garantias { get; init; } = [];

    public AnalysisRating? Analisis { get; init; }

    public MonthlyCloseLoan? LineaAnterior { get; init; }

    public InsolvencyProceeding? Insolvencia { get; init; }

    /// <summary>La mayor mora del mes del corte según las posiciones diarias (o la del corte si no las hay).</summary>
    public int MaxDiasDeMoraDelMes { get; init; }

    public int DiasDeMora => Estado.AlturaDeMora.Dias;

    public bool Suspendido => Estado.Suspendida;

    /// <summary>Los meses que lleva en E <b>antes</b> de este corte (0 el primer mes en E), para las subcategorías por tiempo en E.</summary>
    public int MesesEnE => LineaAnterior is { FinalRating: RatingCategory.E } a ? (a.MonthsInE ?? 0) + 1 : 0;
}

/// <summary>Lo que cargó <see cref="CargadorDeRiesgo"/>: los créditos de la tanda, los de sus deudores (el arrastre) y los que fallaron. (nuevo)</summary>
public sealed record DatosDeRiesgo(
    IReadOnlyList<int> Tanda,
    IReadOnlyDictionary<int, CreditoDeRiesgo> Creditos,
    IReadOnlyDictionary<int, Error> Errores,
    Domain.Entities.Lending.Risk.MonthlyClose? CierreAnterior);

/// <summary>
/// La carga en bloque del riesgo (feature 013, US9, T0699; T46, T47): para una tanda de créditos y un corte con R, la reconstrucción a fin de mes
/// por <see cref="CargadorDeReconstruccion"/> y, de cada uno, las garantías vigentes con su valor admisible, los codeudores, el análisis vigente
/// a R, la línea del cierre anterior (o del base), la insolvencia del deudor y la mayor mora del mes. Para el arrastre carga <b>todos</b> los
/// créditos vivos de los deudores de la tanda (y de sus codeudores si la norma alcanza las codeudas): los que no son de la tanda se califican
/// para el grupo pero no se escriben. Un crédito que no se puede reconstruir o cuya modalidad no está en el catálogo o en la norma vigentes queda
/// en <see cref="DatosDeRiesgo.Errores"/>. No escribe. (nuevo)
/// </summary>
public sealed class CargadorDeRiesgo(IApplicationDbContext db, CargadorDeReconstruccion reconstruccion)
{
    public const string CodigoSinSegmento = "Lending.Risk.SegmentMissing";

    /// <summary>¿Un crédito está vivo al <paramref name="corte"/>? Abierto hasta el corte, no anulado y sin cancelar ni novar hasta él.</summary>
    public static IQueryable<Loan> VivosAl(IQueryable<Loan> creditos, DateOnly corte) =>
        creditos.Where(l => !l.IsDeleted && l.Status != LoanStatus.PendingDisbursement && l.Status != LoanStatus.Voided && l.OpenedOn <= corte
            && (l.PaidOffOn == null || l.PaidOffOn > corte) && (l.NovatedOn == null || l.NovatedOn > corte));

    public async Task<DatosDeRiesgo> CargarAsync(IReadOnlyCollection<int> tanda, DateOnly corte, DateTime? informacionHasta, ContextoDeRiesgo contexto,
        Domain.Entities.Lending.Risk.MonthlyClose? cierreAnterior, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var ids = tanda.Distinct().ToList();
        var deLaTanda = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        var deudores = deLaTanda.Select(l => l.DebtorPersonId).Distinct().ToList();
        var codeudas = await db.LoanParties.AsNoTracking()
            .Where(p => ids.Contains(p.LoanId) && p.Role == CreditPartyRole.CoDebtor && !p.IsDeleted && p.JoinedOn <= corte
                && (p.ReleasedOn == null || p.ReleasedOn > corte) && (p.ExtinguishedOn == null || p.ExtinguishedOn > corte))
            .Select(p => new { p.LoanId, p.PersonId }).ToListAsync(ct);
        var personasDelGrupo = deudores.ToHashSet();
        if (contexto.Norma.Arrastre.IncluyeCodeudas) personasDelGrupo.UnionWith(codeudas.Select(c => c.PersonId));
        var personas = personasDelGrupo.ToList();

        var delGrupo = await VivosAl(db.Loans.AsNoTracking(), corte).Where(l => personas.Contains(l.DebtorPersonId)).Select(l => l.Id).ToListAsync(ct);
        var todos = ids.Union(delGrupo).ToList();
        var codeudasDeTodos = await db.LoanParties.AsNoTracking()
            .Where(p => todos.Contains(p.LoanId) && p.Role == CreditPartyRole.CoDebtor && !p.IsDeleted && p.JoinedOn <= corte
                && (p.ReleasedOn == null || p.ReleasedOn > corte) && (p.ExtinguishedOn == null || p.ExtinguishedOn > corte))
            .Select(p => new { p.LoanId, p.PersonId }).ToListAsync(ct);

        var cargados = await reconstruccion.CargarAsync(todos, informacionHasta, corte, ct);
        var deudoresDeTodos = cargados.Values.Select(c => c.Credito.DebtorPersonId).Distinct().ToList();
        var empleados = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => deudoresDeTodos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.IsEmployee, ct);
        var oficinaIds = cargados.Values.Select(c => c.Credito.BranchId).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinaIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.TenantBranchPublicId ?? b.PublicId, ct);

        var r = informacionHasta ?? DateTime.MaxValue;
        var analisis = (await db.AnalysisRatings.AsNoTracking().Where(a => todos.Contains(a.LoanId) && !a.IsDeleted && a.SetAt <= r).ToListAsync(ct))
            .Where(a => a.VigenteA(r)).GroupBy(a => a.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.SetAt).First());

        var anteriores = cierreAnterior is null
            ? new Dictionary<int, MonthlyCloseLoan>()
            : await CategoriaContableDeCartera.LineasDe(db, cierreAnterior).Where(l => todos.Contains(l.LoanId)).ToDictionaryAsync(l => l.LoanId, ct);

        var insolvencias = (await db.InsolvencyProceedings.AsNoTracking()
                .Where(i => deudoresDeTodos.Contains(i.PersonId) && !i.IsDeleted && i.StartDate <= corte).ToListAsync(ct))
            .Where(i => i.ClosedAt is not { } cerrada || cerrada > r)
            .GroupBy(i => i.PersonId).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.StartDate).ThenByDescending(i => i.Id).First());

        var garantias = await db.Collaterals.AsNoTracking()
            .Where(c => c.LoanId != null && ids.Contains(c.LoanId.Value) && !c.IsDeleted && c.ConstitutedOn <= corte
                && (c.Kind == CollateralKind.Pledge || c.Kind == CollateralKind.Mortgage || c.Kind == CollateralKind.GuaranteeFund))
            .ToListAsync(ct);

        var inicioDelMes = new DateOnly(corte.Year, corte.Month, 1);
        var moraDelMes = await db.LoanDailyPositions.AsNoTracking()
            .Where(p => ids.Contains(p.LoanId) && p.PositionDate >= inicioDelMes && p.PositionDate <= corte && p.RegisteredAt <= r)
            .GroupBy(p => p.LoanId).Select(g => new { LoanId = g.Key, Max = g.Max(p => p.DaysPastDue) })
            .ToDictionaryAsync(x => x.LoanId, x => x.Max, ct);

        var creditos = new Dictionary<int, CreditoDeRiesgo>();
        var errores = new Dictionary<int, Error>();
        foreach (var id in todos)
        {
            if (!cargados.TryGetValue(id, out var cargado) || !cargado.Listo)
            {
                errores[id] = cargado?.Error ?? new Error("Lending.Replay.InvalidOrigin", "El crédito no tiene condiciones selladas para reconstruirse.");
                continue;
            }
            var combinacion = contexto.Combinacion(cargado.Vigentes.ModalityCode, cargado.Vigentes.SubsegmentCode);
            if (combinacion is null || contexto.Norma.Segmento(combinacion.SarcSegment) is null)
            {
                errores[id] = new ErrorConDatos(CodigoSinSegmento,
                    $"La modalidad {cargado.Vigentes.ModalityCode} del crédito {cargado.Credito.Number} no tiene segmento SARC en el catálogo o en el Anexo 1 vigentes.",
                    new { modalityCode = cargado.Vigentes.ModalityCode, subsegmentCode = cargado.Vigentes.SubsegmentCode });
                continue;
            }
            EstadoDelCredito estado;
            try
            {
                estado = MotorDeCartera.Reconstruir(cargado.Entrada!, corte);
            }
            catch (ReglaDeCarteraException ex)
            {
                errores[id] = new Error(ex.Codigo, ex.Message);
                continue;
            }

            creditos[id] = new CreditoDeRiesgo(cargado.Credito, cargado.Vigentes, estado, combinacion,
                empleados.GetValueOrDefault(cargado.Credito.DebtorPersonId), oficinas.GetValueOrDefault(cargado.Credito.BranchId))
            {
                Codeudores = codeudasDeTodos.Where(c => c.LoanId == id).Select(c => c.PersonId).Distinct().ToList(),
                Garantias = garantias.Where(g => g.LoanId == id && g.PerfeccionadaA(corte)).Select(Garantia).ToList(),
                Analisis = analisis.GetValueOrDefault(id),
                LineaAnterior = anteriores.GetValueOrDefault(id),
                Insolvencia = insolvencias.GetValueOrDefault(cargado.Credito.DebtorPersonId),
                MaxDiasDeMoraDelMes = Math.Max(moraDelMes.GetValueOrDefault(id), estado.AlturaDeMora.Dias),
            };
        }
        return new DatosDeRiesgo(ids, creditos, errores, cierreAnterior);
    }

    /// <summary>
    /// Una garantía para el deterioro: la hipoteca es <c>Mortgage</c>; la prenda y el fondo de garantías, <c>NonMortgage</c>. El valor es el
    /// admisible registrado al constituirla (o el comercial si no lo trae); los aportes no cuentan mientras rija D19.
    /// </summary>
    public static GarantiaParaDeterioro Garantia(Domain.Entities.Lending.Origination.Collateral g) =>
        new(g.Kind == CollateralKind.Mortgage ? NaturalezaDeGarantia.Mortgage : NaturalezaDeGarantia.NonMortgage,
            g.AdmissibleValue > 0m ? g.AdmissibleValue : g.Value);
}
