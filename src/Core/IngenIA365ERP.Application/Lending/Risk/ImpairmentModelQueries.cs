using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Core.RegulatoryProfile;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>El modelo de deterioro de un segmento de pérdida esperada a una fecha (api.md §12.4). (nuevo)</summary>
public sealed record ImpairmentModelBySegmentDto(string Segment, string Model, string Basis, string? Mark, DateOnly? Since, string? Election);

/// <summary>Un crédito sin historial de mora completo (api.md §12.5). (nuevo)</summary>
public sealed record IncompleteArrearsHistoryDto(Guid LoanPublicId, string LoanNumber, string Segment, int MonthsAvailable, IReadOnlyList<string> MissingMonths);

/// <summary>La completitud del historial de mora a una fecha (api.md §12.5). (nuevo)</summary>
public sealed record ArrearsHistoryCompletenessDto(DateOnly Date, int MonthsRequired, Guid? RuleDocumentPublicId, string? RuleDocumentCode,
    IReadOnlyList<IncompleteArrearsHistoryDto> Incomplete);

/// <summary>
/// <c>GET /api/lending/risk/impairment-model?date=</c> (feature 013, US9, T0710; api.md §12.4, <c>Lending.Ratings.Read</c>; T50, D28): el modelo de
/// cada segmento de pérdida esperada con su fundamento y la marca «modelo obligatorio no disponible», de <see cref="SelectorDeModeloDeDeterioro"/>
/// con la regla de pérdida esperada y el perfil regulatorio vigentes y la entrega desplegada. Sin elecciones voluntarias hasta K6a. Sin datos de
/// deudores. (nuevo)
/// </summary>
public sealed record GetImpairmentModelBySegmentQuery(DateOnly? Date = null) : IRequest<Result<IReadOnlyList<ImpairmentModelBySegmentDto>>>;

public sealed class GetImpairmentModelBySegmentQueryHandler(INormativaVigente normativa, IPerfilRegulatorio perfiles, IDateTimeService reloj,
    IApplicationDbContext? db = null)
    : IRequestHandler<GetImpairmentModelBySegmentQuery, Result<IReadOnlyList<ImpairmentModelBySegmentDto>>>
{
    public async Task<Result<IReadOnlyList<ImpairmentModelBySegmentDto>>> Handle(GetImpairmentModelBySegmentQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, fecha, ReglaDePerdidaEsperada.Leer, ct: ct);
        var perfil = await perfiles.VigenteAsync(fecha, null, ct);
        // K6a (T1034): con la última elección voluntaria que ya rige a la fecha.
        var elecciones = db is null ? [] : await LectorDelContextoDeRiesgo.EleccionesAsync(db, fecha, null, ct);
        var eleccion = elecciones.LastOrDefault();
        var seleccion = SelectorDeModeloDeDeterioro.SeleccionarAl(fecha, regla.IsSuccess ? regla.Value.Documento : null,
            perfil is null ? [] : [new VigenciaDePerfil(perfil.ValidFrom, new PerfilParaModelo(perfil.OrganizationTypeCode, perfil.EmployeeFundCategory))],
            elecciones, CatalogoDeCartera.EntregaVigente);
        DateOnly? desde = !seleccion.Obligada ? null
            : new[] { regla.IsSuccess ? regla.Value.ValidFrom : (DateOnly?)null, perfil?.ValidFrom }.Where(d => d is not null).Max();
        return Result.Success<IReadOnlyList<ImpairmentModelBySegmentDto>>(seleccion.Segmentos.Select(s => new ImpairmentModelBySegmentDto(s.Segmento,
            s.Modelo.ToString(), s.Fundamento.ToString(), s.ModeloObligatorioNoDisponible ? nameof(Domain.Entities.Lending.Risk.MonthlyClose.ObligatoryModelUnavailable) : null,
            s.Fundamento switch
            {
                ModelBasis.Obligated => desde,
                ModelBasis.Voluntary => eleccion?.Desde,
                _ => null,
            },
            eleccion is not null && eleccion.Segmentos.Contains(s.Segmento, StringComparer.Ordinal) ? eleccion.Clase.ToString() : null)).ToList());
    }
}

/// <summary>
/// <c>GET /api/lending/risk/arrears-history/completeness?date=&amp;segment=</c> (feature 013, US9, T0710; api.md §12.5, <c>Lending.Ratings.Read</c>;
/// FR-105, FR-139, FR-140): los créditos vivos del alcance de los segmentos cubiertos por la regla de pérdida esperada cuyo historial de mora no
/// llega a los meses que ella pide (<see cref="HistorialDeMora.Construir"/>). En K1 informa; la regla de meses faltantes se aplica en K6a. (nuevo)
/// </summary>
public sealed record GetArrearsHistoryCompletenessQuery(DateOnly? Date = null, string? Segment = null) : IRequest<Result<ArrearsHistoryCompletenessDto>>;

public sealed class GetArrearsHistoryCompletenessQueryHandler(
    IApplicationDbContext db,
    INormativaVigente normativa,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj) : IRequestHandler<GetArrearsHistoryCompletenessQuery, Result<ArrearsHistoryCompletenessDto>>
{
    public async Task<Result<ArrearsHistoryCompletenessDto>> Handle(GetArrearsHistoryCompletenessQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, fecha, ReglaDePerdidaEsperada.Leer, ct: ct);
        if (regla.IsFailure) return Result.Success(new ArrearsHistoryCompletenessDto(fecha, 0, null, null, []));
        var catalogo = await normativa.CatalogoDeModalidadesAsync(fecha, null, ct);
        if (catalogo.IsFailure) return Result.Failure<ArrearsHistoryCompletenessDto>(catalogo.Error);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), fecha).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).ToListAsync(ct);
        var incompletos = await CompletitudDelHistorial.IncompletosAsync(db, creditos, regla.Value.Documento, catalogo.Value.Documento, fecha, request.Segment, ct);
        return Result.Success(new ArrearsHistoryCompletenessDto(fecha, regla.Value.Documento.MesesDeHistorial, regla.Value.PublicId, regla.Value.Codigo, incompletos));
    }
}

/// <summary>
/// La completitud del historial de mora de unos créditos (feature 013, US9, T0710; T49): por cada crédito vivo de un segmento cubierto, su cadena
/// (novaciones y refinanciaciones), sus filas de <c>LND_LoanArrearsHistory</c> —las de un cierre sólo si ese cierre está vigente— y el corte de la
/// migración con que nació, y <see cref="HistorialDeMora.Construir"/> con los meses que pide la regla. (nuevo)
/// </summary>
public static class CompletitudDelHistorial
{
    /// <summary>
    /// El corte que mira <see cref="HistorialDeMora.Construir"/> para un crédito migrado: el último día del mes <b>anterior</b> al del corte de la
    /// carga. El historial migrado llega hasta el mes anterior al del corte (plantillas.md §5.3.17) y el mes del corte lo cubren la mora migrada al
    /// corte (<c>risk-data</c>, la línea del cierre base) y las transacciones desde el día siguiente: así la completitud dice lo mismo que la
    /// validación de la carga al aplicarla (K6a, bloque 2; antes el mes del corte quedaba siempre «faltante»).
    /// </summary>
    public static DateOnly? CorteParaHistorial(DateOnly? corteDeLaCarga) =>
        corteDeLaCarga is { } c ? new DateOnly(c.Year, c.Month, 1).AddDays(-1) : null;

    /// <summary>Las cadenas de los créditos: los antecesores por novación o refinanciación y el inicio más antiguo.</summary>
    public static async Task<Dictionary<int, CreditoParaHistorial>> CadenasAsync(IApplicationDbContext db, IReadOnlyCollection<Loan> creditos, CancellationToken ct)
    {
        var ids = creditos.Select(c => c.Id).ToList();
        var todos = await db.Loans.AsNoTracking().IgnoreQueryFilters()
            .Where(l => ids.Contains(l.Id) || l.OriginLoanId != null || db.Loans.Any(x => x.OriginLoanId == l.Id))
            .Select(l => new { l.Id, l.OriginLoanId, l.OriginLinkKind, l.OpenedOn }).ToDictionaryAsync(l => l.Id, ct);
        return creditos.ToDictionary(c => c.Id, c =>
        {
            var antecesores = new List<int>();
            var inicio = c.OpenedOn;
            if (!todos.TryGetValue(c.Id, out var actual)) return new CreditoParaHistorial(c.Id, inicio);
            while (actual.OriginLoanId is { } previo && actual.OriginLinkKind is LoanLinkKind.Novation or LoanLinkKind.Refinancing
                   && todos.TryGetValue(previo, out var anterior) && !antecesores.Contains(previo))
            {
                antecesores.Add(previo);
                if (anterior.OpenedOn < inicio) inicio = anterior.OpenedOn;
                actual = anterior;
            }
            return new CreditoParaHistorial(c.Id, inicio) { Antecesores = antecesores };
        });
    }

    /// <summary>
    /// Las filas de <c>LND_LoanArrearsHistory</c> de los créditos (conocidas hasta R si se da): las de un cierre sólo cuentan si ese cierre existe y
    /// está vigente, y el <c>PredecessorLoanNumber</c> del historial migrado de un antecesor que no nació en Cartera viaja como
    /// <see cref="EntradaDeHistorialDeMora.NumeroDelAntecesor"/> (otro eslabón de la cadena, T1015).
    /// </summary>
    public static async Task<List<EntradaDeHistorialDeMora>> EntradasAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos, DateTime? informacionHasta,
        CancellationToken ct)
    {
        var r = informacionHasta ?? DateTime.MaxValue;
        var filas = await db.LoanArrearsHistory.AsNoTracking().Where(h => creditos.Contains(h.LoanId) && !h.IsDeleted && h.CreatedAt <= r).ToListAsync(ct);
        var corridasVigentes = (await db.MonthlyCloses.AsNoTracking()
                .Where(c => c.ProcessRunId != null && !c.IsDeleted && !db.MonthlyCloseReopenings.Any(x => x.MonthlyCloseId == c.Id && !x.IsDeleted))
                .Select(c => c.ProcessRunId!.Value).ToListAsync(ct))
            .ToHashSet();
        return filas.Select(h => new EntradaDeHistorialDeMora(h.LoanId, h.PeriodYear, h.PeriodMonth, h.MaxDaysPastDue, h.Origin, h.Id)
        {
            Vigente = h.Origin != ArrearsHistoryOrigin.MonthlyClose || (h.ProcessRunId is { } run && corridasVigentes.Contains(run)),
            NumeroDelAntecesor = h.PredecessorLoanNumber,
        }).ToList();
    }

    public static async Task<IReadOnlyList<IncompleteArrearsHistoryDto>> IncompletosAsync(IApplicationDbContext db, IReadOnlyList<Loan> creditos,
        ReglaDePerdidaEsperada regla, CatalogoDeModalidades catalogo, DateOnly fecha, string? soloSegmento, CancellationToken ct)
    {
        var ids = creditos.Select(c => c.Id).ToList();
        var condiciones = (await db.LoanTermsVersions.AsNoTracking().Where(t => ids.Contains(t.LoanId)).ToListAsync(ct))
            .GroupBy(t => t.LoanId).ToDictionary(g => g.Key, g => g.OrderBy(t => t.VersionNumber).Last());
        var cubiertos = new List<(Loan Credito, string Segmento)>();
        foreach (var c in creditos)
        {
            if (!condiciones.TryGetValue(c.Id, out var t)) continue;
            var segmento = catalogo.Buscar(t.ModalityCode, t.SubsegmentCode)?.ExpectedLossSegment;
            if (segmento is null || !regla.SegmentosCubiertos.Contains(segmento, StringComparer.Ordinal)) continue;
            if (soloSegmento is not null && !string.Equals(soloSegmento, segmento, StringComparison.Ordinal)) continue;
            cubiertos.Add((c, segmento));
        }
        if (cubiertos.Count == 0) return [];

        var cadenas = await CadenasAsync(db, cubiertos.Select(c => c.Credito).ToList(), ct);
        var cargas = await db.MigrationLoads.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(l => l.Id, l => l.CutoffDate, ct);
        var deLasCadenas = cadenas.Values.SelectMany(c => c.Antecesores.Append(c.LoanId)).Distinct().ToList();
        var entradas = await EntradasAsync(db, deLasCadenas, null, ct);

        var resultado = new List<IncompleteArrearsHistoryDto>();
        foreach (var (credito, segmento) in cubiertos.OrderBy(c => c.Credito.Number, StringComparer.Ordinal))
        {
            var corte = CorteParaHistorial(credito.MigrationLoadId is { } carga && cargas.TryGetValue(carga, out var c) ? c : null);
            var construido = HistorialDeMora.Construir(cadenas[credito.Id], entradas, corte, fecha, regla.MesesDeHistorial);
            if (construido.Completo) continue;
            resultado.Add(new IncompleteArrearsHistoryDto(credito.PublicId, credito.Number, segmento, construido.Meses.Count(m => m.Cubierto),
                construido.Faltantes.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Anio:0000}-{f.Mes:00}")).ToList()));
        }
        return resultado;
    }
}

/// <summary>
/// La obligación sobrevenida de aplicar la pérdida esperada (feature 013, US9, T0710; T50, D28; FR-105): con Cartera activada, una vigencia del
/// perfil regulatorio o una <c>ReglaDePerdidaEsperada</c> que llegan <b>después</b> de la activación y obligan a la cooperativa levantan
/// <c>Cartera.ObligacionSobrevenida</c> —con los créditos sin historial completo— y, sin K6a desplegada, <c>Cartera.ModeloObligatorioNoDisponible</c>
/// para el contador y el jefe de cartera. Antes de activar no alerta. El selector la refleja desde el primer cierre dentro de la vigencia. La usan el
/// alta del perfil (<see cref="IEvaluadorDeObligacionDePerdidaEsperada"/>) y la réplica (<see cref="EfectoDeReplicacionObligacionSobrevenida"/>).
/// (nuevo)
/// </summary>
public static class ObligacionSobrevenida
{
    public static async Task<(ExpectedLossEvaluationDto Evaluacion, IReadOnlyList<AlertaALevantar> Alertas)> EvaluarAsync(IApplicationDbContext db,
        ReglaDePerdidaEsperada? regla, PerfilRegulatorioDto? perfil, DateOnly desde, string fundamento, CancellationToken ct)
    {
        if (regla is null || perfil is null)
            return (new ExpectedLossEvaluationDto(false, [], null, "Sin regla de pérdida esperada o sin perfil regulatorio vigente: no hay obligación."), []);
        var obligada = regla.Obliga(perfil.OrganizationTypeCode, perfil.EmployeeFundCategory);
        if (!obligada)
            return (new ExpectedLossEvaluationDto(false, [], null, $"El tipo {perfil.OrganizationTypeCode} no está obligado por la regla vigente."), []);

        var evaluacion = new ExpectedLossEvaluationDto(true, regla.SegmentosCubiertos, desde, fundamento);
        var activacion = await db.LendingSetups.AsNoTracking().Where(s => s.Id == LendingSetup.IdDeLaFilaUnica).Select(s => s.ActivationDate).FirstOrDefaultAsync(ct);
        if (activacion is null) return (evaluacion, []);

        // K6a (T1037): el historial se mira a la fecha del primer cierre en pérdida esperada, el fin del mes en que empieza la obligación.
        var primerCierre = Domain.Entities.Lending.Risk.MonthlyClose.UltimoDiaDe(desde.Year, desde.Month);
        var catalogo = await new LectorDeDocumentoNormativo(db).CatalogoDeModalidadesAsync(desde, null, ct);
        var incompletos = catalogo.IsFailure ? [] : await CompletitudDelHistorial.IncompletosAsync(db,
            await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), desde).ToListAsync(ct), regla, catalogo.Value.Documento, primerCierre, null, ct);
        var mes = desde.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var alertas = new List<AlertaALevantar>
        {
            new(TiposDeAlerta.ObligacionSobrevenida, "La cooperativa quedó obligada al modelo de pérdida esperada",
                $"Desde el {desde:yyyy-MM-dd} la cooperativa ({perfil.OrganizationTypeCode}) está obligada al modelo de pérdida esperada en "
                + $"{string.Join(", ", regla.SegmentosCubiertos)} ({fundamento}). {incompletos.Count.ToString(CultureInfo.InvariantCulture)} créditos no tienen el "
                + $"historial de mora de {regla.MesesDeHistorial.ToString(CultureInfo.InvariantCulture)} meses que pide la regla"
                + (incompletos.Count == 0 ? "." : $": {string.Join(", ", incompletos.Take(20).Select(i => i.LoanNumber))}{(incompletos.Count > 20 ? "…" : ".")}"),
                DedupKey: $"{TiposDeAlerta.ObligacionSobrevenida}:{mes}", ModuloDeOrigen: "Lending"),
        };
        // K6a (T1037, US9-9): la lista de créditos sin historial completo va también como su propia alerta al contador y al jefe de cartera; el historial
        // se completa con la carga complementaria hasta el primer cierre en pérdida esperada, y si entonces falta, el cierre aplica la regla de meses
        // faltantes y los lista.
        if (incompletos.Count > 0)
            alertas.Add(new(TiposDeAlerta.HistorialDeMoraIncompleto, "Hay créditos de segmentos cubiertos sin historial de mora completo",
                $"{incompletos.Count.ToString(CultureInfo.InvariantCulture)} créditos de los segmentos cubiertos no tienen el historial de mora completo al "
                + $"{primerCierre:yyyy-MM-dd}, el primer cierre en pérdida esperada: {string.Join(", ", incompletos.Take(20).Select(i => i.LoanNumber))}"
                + (incompletos.Count > 20 ? "…" : ".") + " Complételo con la carga complementaria antes de ese cierre; si no, el cierre aplica la regla de meses faltantes.",
                DedupKey: $"{TiposDeAlerta.HistorialDeMoraIncompleto}:obligacion-{mes}", ModuloDeOrigen: "Lending"));
        if (!SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(CatalogoDeCartera.EntregaVigente))
            alertas.Add(new(TiposDeAlerta.ModeloObligatorioNoDisponible, "El modelo de pérdida esperada todavía no está disponible",
                $"La cooperativa está obligada al modelo de pérdida esperada desde el {desde:yyyy-MM-dd}, pero la entrega desplegada ({CatalogoDeCartera.EntregaVigente}) "
                + "no lo trae: los cierres siguen con el Anexo 1 y quedan marcados «modelo obligatorio no disponible» (D28).",
                DedupKey: $"{TiposDeAlerta.ModeloObligatorioNoDisponible}:{mes}", ModuloDeOrigen: "Lending"));
        return (evaluacion, alertas);
    }
}

/// <summary>
/// El gancho de Cartera en el alta del perfil regulatorio (feature 013, US9, T0710; T50): evalúa la obligación de pérdida esperada con la regla
/// vigente en la fecha del perfil y, si Cartera está activada, levanta sus alertas. Lo consulta <c>AddRegulatoryProfileVersionCommand</c> por la
/// interfaz <see cref="IEvaluadorDeObligacionDePerdidaEsperada"/> (Core no conoce Cartera). (nuevo)
/// </summary>
public sealed class EvaluadorDeObligacionDePerdidaEsperada(IApplicationDbContext db, INormativaVigente normativa, IAlertas alertas)
    : IEvaluadorDeObligacionDePerdidaEsperada
{
    public async Task<ExpectedLossEvaluationDto> EvaluarAsync(RegulatoryProfileVersion version, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, version.ValidFrom, ReglaDePerdidaEsperada.Leer, ct: ct);
        var (evaluacion, porLevantar) = await ObligacionSobrevenida.EvaluarAsync(db, regla.IsSuccess ? regla.Value.Documento : null,
            PerfilRegulatorioDto.Desde(version), version.ValidFrom, $"perfil regulatorio desde el {version.ValidFrom:yyyy-MM-dd}", ct);
        foreach (var a in porLevantar) await alertas.LevantarAsync(a, ct);
        return evaluacion;
    }
}

/// <summary>
/// El efecto de Cartera sobre la réplica que detecta la obligación sobrevenida (feature 013, US9, T0710; T50, D28): cuando llega una
/// <c>ReglaDePerdidaEsperada</c> y la cooperativa, con Cartera activada, queda obligada por su perfil vigente, devuelve las alertas
/// (<see cref="ObligacionSobrevenida"/>) para que el núcleo las levante después de guardar. No escribe. (nuevo)
/// </summary>
public sealed class EfectoDeReplicacionObligacionSobrevenida : IEfectoDeReplicacion
{
    public const string NombreDelEfecto = "cartera-obligacion-sobrevenida";

    public string Nombre => NombreDelEfecto;

    public async Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(lote);
        var reglas = lote.Documentos.Where(d => d.Kind == nameof(RegulatoryDocumentKind.ReglaDePerdidaEsperada)).OrderBy(d => d.SourceSequence).ToList();
        if (reglas.Count == 0) return EfectoAplicado.Nada;
        var activada = await db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);
        if (!activada) return EfectoAplicado.Nada;

        var alertas = new List<AlertaALevantar>();
        var notas = new List<string>();
        foreach (var d in reglas)
        {
            var leida = ReglaDePerdidaEsperada.Leer(d.Content, d.SchemaVersion);
            if (!leida.EsValido) continue;
            var desde = d.ValidFrom > lote.Hoy ? d.ValidFrom : lote.Hoy;
            var perfil = await new LectorDePerfilRegulatorio(db).VigenteAsync(desde, null, ct);
            var (evaluacion, porLevantar) = await ObligacionSobrevenida.EvaluarAsync(db, leida.Documento, perfil, desde,
                $"regla de pérdida esperada {d.Code} desde el {d.ValidFrom:yyyy-MM-dd}", ct);
            if (evaluacion.Obligated is true) notas.Add($"{d.Code}: la cooperativa queda obligada desde el {desde:yyyy-MM-dd}.");
            alertas.AddRange(porLevantar);
        }
        return alertas.Count == 0 && notas.Count == 0
            ? EfectoAplicado.Nada
            : new EfectoAplicado(new Dictionary<string, int> { ["obligaciones"] = notas.Count }, notas, alertas);
    }
}
