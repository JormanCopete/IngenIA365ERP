using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>La calificación informativa que se copia en la posición diaria (<c>DailyRating</c>, <c>DailyRatingSubcategory</c>). (nuevo)</summary>
public sealed record CalificacionDiaria(RatingCategory Categoria, RatingSubcategory Subcategoria);

/// <summary>Un día causado de un crédito de la tanda, con su reconstrucción a ese día.</summary>
public sealed record DiaParaCalificar(Loan Credito, LoanTermsVersion Vigentes, DateOnly Dia, EstadoDelCredito Estado);

/// <summary>
/// La calificación informativa que el ciclo diario copia en las posiciones (feature 013, US9, T0701; T46, pregunta E7): el mismo motor del cierre
/// sobre la norma efectiva del día, con arrastre si <c>Riesgo.CalificacionDiariaConArrastre</c> lo pide. Los créditos de la tanda llevan la mora de
/// su reconstrucción de ese día; los demás créditos vivos de sus deudores (para el arrastre), la de su última posición. Sin Anexo 1 o sin catálogo
/// vigentes no califica (la posición queda sin calificación): la causación nunca falla por esto. Nunca escribe. (nuevo)
/// </summary>
public sealed class CalificacionDelCiclo(IApplicationDbContext db, LectorDelContextoDeRiesgo contextos, ILogger<CalificacionDelCiclo>? logger = null)
{
    private readonly ILogger _logger = logger ?? (ILogger)NullLogger.Instance;

    public async Task<IReadOnlyDictionary<(int LoanId, DateOnly Dia), CalificacionDiaria>> CalificarAsync(IReadOnlyList<DiaParaCalificar> dias, CancellationToken ct)
    {
        var resultado = new Dictionary<(int, DateOnly), CalificacionDiaria>();
        if (dias.Count == 0) return resultado;
        var ultimo = dias.Max(d => d.Dia);
        var leido = await contextos.LeerAsync(ultimo, null, paraElCierre: false, ct);
        if (leido.IsFailure)
        {
            _logger.LogInformation("[Cartera.CalificacionDiariaSinNorma] Sin calificación informativa del {Dia}: {Motivo}", ultimo, leido.Error.Message);
            return resultado;
        }
        var contexto = leido.Value;
        var norma = contexto.Norma;

        var anterior = await CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, ultimo.AddDays(1), ct);
        var deLaTanda = dias.Select(d => d.Credito.Id).Distinct().ToList();
        var deudores = dias.Select(d => d.Credito.DebtorPersonId).Distinct().ToList();
        var otros = await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), ultimo)
            .Where(l => deudores.Contains(l.DebtorPersonId) && !deLaTanda.Contains(l.Id)).ToListAsync(ct);
        var otrosIds = otros.Select(o => o.Id).ToList();
        var condicionesDeOtros = (await db.LoanTermsVersions.AsNoTracking().Where(t => otrosIds.Contains(t.LoanId)).ToListAsync(ct))
            .GroupBy(t => t.LoanId).ToDictionary(g => g.Key, g => g.OrderBy(t => t.VersionNumber).Last());
        var moraDeOtros = (await db.LoanDailyPositions.AsNoTracking().Where(p => otrosIds.Contains(p.LoanId) && p.PositionDate <= ultimo)
                .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.DaysPastDue }).ToListAsync(ct))
            .GroupBy(p => p.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First().DaysPastDue);
        var todos = deLaTanda.Concat(otrosIds).ToList();
        var lineasAnteriores = anterior is null
            ? new Dictionary<int, MonthlyCloseLoan>()
            : await CategoriaContableDeCartera.LineasDe(db, anterior).Where(l => todos.Contains(l.LoanId)).ToDictionaryAsync(l => l.LoanId, ct);
        var analisis = (await db.AnalysisRatings.AsNoTracking().Where(a => deLaTanda.Contains(a.LoanId) && a.LiftedAt == null && !a.IsDeleted).ToListAsync(ct))
            .GroupBy(a => a.LoanId).ToDictionary(g => g.Key, g => g.First());

        CreditoParaCalificar? Para(Loan credito, LoanTermsVersion vigentes, int dias)
        {
            var combinacion = contexto.Combinacion(vigentes.ModalityCode, vigentes.SubsegmentCode);
            if (combinacion is null || norma.Segmento(combinacion.SarcSegment) is null) return null;
            var previa = lineasAnteriores.GetValueOrDefault(credito.Id);
            return new CreditoParaCalificar(credito.Id, credito.DebtorPersonId, combinacion.SarcSegment, dias)
            {
                MesesEnE = previa is { FinalRating: RatingCategory.E } p ? (p.MonthsInE ?? 0) + 1 : 0,
                PagoUnico = vigentes.AmortizationSystem == AmortizationSystem.Bullet,
                Nuevo = anterior is not null && previa is null,
                Analisis = analisis.TryGetValue(credito.Id, out var a) ? new CalificacionImpuesta(a.Rating, a.Subcategory) : null,
            };
        }

        var deOtros = otros.Where(o => condicionesDeOtros.ContainsKey(o.Id))
            .Select(o => Para(o, condicionesDeOtros[o.Id], moraDeOtros.GetValueOrDefault(o.Id))).Where(c => c is not null).Select(c => c!).ToList();
        foreach (var grupo in dias.GroupBy(d => d.Dia))
        {
            var propios = grupo.GroupBy(d => d.Credito.Id).Select(g => g.Last())
                .Select(d => Para(d.Credito, d.Vigentes, d.Estado.AlturaDeMora.Dias)).Where(c => c is not null).Select(c => c!).ToList();
            if (propios.Count == 0) continue;
            try
            {
                var calificadas = MotorDeCalificacion.Calificar(norma, [.. propios, .. deOtros], new OpcionesDeCalificacion(contexto.ArrastreEnLaInformativa, contexto.Aportes));
                foreach (var c in calificadas.Where(c => deLaTanda.Contains(c.LoanId)))
                    resultado[(c.LoanId, grupo.Key)] = new CalificacionDiaria(c.Categoria, c.Subcategoria);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "[Cartera.CalificacionDiariaFallida] Sin calificación informativa del {Dia}.", grupo.Key);
            }
        }
        return resultado;
    }
}
