using FluentValidation;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

/// <summary>
/// «¿Es contabilizable?» para Cartera (feature 013, T0649; contracts/contabilidad.md §13.1 y §13.3; FR-127), detrás de
/// <see cref="IContabilidadParaCartera.EvaluarAsync"/>. Agrupa los hechos tal como se emitirían en unidades (origen + evento), arma cada
/// comprobante con el <b>mismo</b> <see cref="ConstructorDeLineasDeCartera"/> del consumidor y los somete a
/// <see cref="AccountingPoster.ValidarVariosAsync"/> (reglas 1 a 11, carga en bloque, sin seguimiento, sin numerar), más las comprobaciones de
/// la matriz: regla faltante, tipo de comprobante sin mapear o inactivo, hecho que no cuadra o con más de dos decimales. No deja nada
/// rastreado: corre fuera del cerrojo de la operación de Cartera.
///
/// <para>
/// Sólo evalúa lo que pasaría a Contabilidad: hechos de negocio de Cartera con destino Contabilidad. Los informativos y los mensajes a otros
/// destinos se ignoran; qué hechos pasan (el modo sellado, <c>NoPasa</c>) lo decide Cartera antes de preguntar
/// (<c>ValidacionPreviaContableDeCartera</c>). Los hallazgos se reducen a uno por hecho, rol, combinación y regla (una pregunta por
/// combinación), cada uno con quién lo corrige (<see cref="QuienCorrigeEnCartera"/>). Los avisos de la 009 no impiden. (nuevo)
/// </para>
/// </summary>
public sealed record EvaluateLendingPostingQuery(IReadOnlyList<MensajeContableDto> Hechos) : IRequest<Result<ResultadoDeEvaluacionContableDto>>;

public sealed class EvaluateLendingPostingQueryValidator : AbstractValidator<EvaluateLendingPostingQuery>
{
    public EvaluateLendingPostingQueryValidator()
    {
        RuleFor(x => x.Hechos).NotNull();
        RuleForEach(x => x.Hechos).Must(m => m?.Envelope is not null).WithMessage("Cada hecho lleva su sobre.");
    }
}

public sealed class EvaluateLendingPostingQueryHandler(
    IApplicationDbContext db, AccountingPoster poster, ResolutorDeReglasDeCartera resolutor, TiposDeComprobanteDeCartera tipos, IDateTimeService reloj)
    : IRequestHandler<EvaluateLendingPostingQuery, Result<ResultadoDeEvaluacionContableDto>>
{
    public async Task<Result<ResultadoDeEvaluacionContableDto>> Handle(EvaluateLendingPostingQuery request, CancellationToken ct)
    {
        // Feature 014, US8 (T145; research E108): un hecho sin fecha contable se evalúa con la de hoy (la matriz del año 1 no tiene reglas) y uno sin
        // origen es su propia unidad (si no, todas las preguntas sin origen de un lote serían una sola unidad).
        var hoy = reloj.TodayUtc;
        var unidades = request.Hechos
            .Where(m => m.Envelope.Kind == IntegrationMessageKind.Business && m.Envelope.OriginModule == ModulosDeOrigen.Cartera
                        && OperacionesDeCartera.Buscar(m.Envelope.Type) is { EsInformativo: false })
            .Select((m, i) => (Hecho: HechoDeCartera.De(m.Envelope.Origin.OperationDate == default
                ? m with { Envelope = m.Envelope with { Origin = m.Envelope.Origin with { OperationDate = hoy } } }
                : m), Indice: i))
            .GroupBy(x => (Origen: x.Hecho.Sobre.Origin.PublicId == Guid.Empty ? $"#{x.Indice}" : x.Hecho.Sobre.Origin.PublicId.ToString("N"),
                x.Hecho.Sobre.OriginEventKey))
            .Select(g => (IReadOnlyList<HechoDeCartera>)g.Select(x => x.Hecho).ToList())
            .ToList();
        var errores = new List<HallazgoDeCarteraDto>();
        var avisos = new List<HallazgoDeCarteraDto>();
        if (unidades.Count == 0) return Result.Success(new ResultadoDeEvaluacionContableDto(true, errores, avisos));

        // Sin contabilidad iniciada los hechos de Cartera no pasan (NoPasa por defecto, §10.1): no hay qué evaluar.
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct))
            return Result.Success(new ResultadoDeEvaluacionContableDto(true, errores, avisos));

        var catalogos = await CatalogosDeCartera.CargarAsync(db, resolutor, tipos, unidades, reloj.TodayUtc, ct);
        var construidas = new List<ConstruccionDeCartera>();
        foreach (var unidad in unidades)
        {
            foreach (var h in unidad.Where(h => h.Sobre.Currency != "COP" || h.Sobre.ExchangeRate != 1m))
            {
                var e = AccountingErrors.LendingFactCurrencyNotSupported(h.Sobre.Currency, h.Sobre.ExchangeRate);
                errores.Add(new HallazgoDeCarteraDto(h.Tipo, null, null, null, e.Code, e.Message, QuienCorrigeEnCartera.De(e.Code)));
            }
            var c = ConstructorDeLineasDeCartera.Construir(unidad, catalogos);
            if (!c.EsValida)
            {
                errores.AddRange(c.Fallos.Select(f => new HallazgoDeCarteraDto(f.FactType, f.Role, f.Combination, f.AccountCode, f.Error.Code, f.Error.Message,
                    QuienCorrigeEnCartera.De(f.Error.Code))));
                continue;
            }
            if (!c.SinLineas) construidas.Add(c);
        }

        var validaciones = await poster.ValidarVariosAsync(construidas.Select(c => c.Request!).ToList(), ct);
        for (var i = 0; i < construidas.Count; i++)
        {
            errores.AddRange(validaciones[i].Errores.Select(e => Hallazgo(e, construidas[i])));
            avisos.AddRange(validaciones[i].Avisos.Select(e => Hallazgo(e, construidas[i])));
        }

        var sinRepetir = Unicos(errores);
        return Result.Success(new ResultadoDeEvaluacionContableDto(sinRepetir.Count == 0, sinRepetir, Unicos(avisos)));
    }

    /// <summary>Un error de línea del contrato traducido al hecho y al rol de su línea, con la cuenta y quién lo corrige.</summary>
    public static HallazgoDeCarteraDto Hallazgo(ErrorDeLinea e, ConstruccionDeCartera c)
    {
        var linea = e.LineNumber >= 1 && e.LineNumber <= c.Mapa.Count ? c.Mapa[e.LineNumber - 1] : null;
        IReadOnlyDictionary<string, string>? combinacion = linea is null ? null : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["concept"] = linea.Concepto ?? string.Empty,
            ["loans"] = string.Join(",", linea.Creditos),
        };
        return new HallazgoDeCarteraDto(linea?.Hechos.FirstOrDefault() ?? c.Principal.Tipo, linea?.Rol, combinacion, e.AccountCode ?? linea?.AccountCode,
            e.Code, e.Message, QuienCorrigeEnCartera.De(e.Code));
    }

    /// <summary>Uno por hecho, rol, combinación, cuenta y regla (§13.1: una pregunta por combinación distinta).</summary>
    private static IReadOnlyList<HallazgoDeCarteraDto> Unicos(IEnumerable<HallazgoDeCarteraDto> hallazgos) =>
        hallazgos.GroupBy(h => string.Join('|', h.FactType, h.Role, h.AccountCode, h.Rule,
                h.Combination is null ? string.Empty : string.Join(';', h.Combination.Where(kv => kv.Key != "loans").OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key}={kv.Value}"))))
            .Select(g => g.First())
            .ToList();
}
