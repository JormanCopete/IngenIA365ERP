using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>Una cifra de referencia que entrega la cooperativa (api.md §16: <c>{ lineCode, concept, amount, source }</c>). (nuevo)</summary>
public sealed record CifraDeReferenciaDto(string LineCode, string Concept, decimal Amount, string? Source);

/// <summary>
/// El cotejo de un total cargado (línea y concepto) con su cifra de referencia: lo cargado, cuántas cuentas lo traen, la cifra, la diferencia (cargado −
/// referencia) y su fuente. Una cifra sin total cargado sale con lo cargado en cero; un total sin cifra, sin referencia. (nuevo)
/// </summary>
public sealed record CotejoDeCifraDto(string LineCode, string Concept, decimal Loaded, int Accounts, decimal? Reference, decimal? Difference, string? Source);

/// <summary>Las cifras de referencia de una carga y su cotejo con lo cargado (FR-118). (nuevo)</summary>
public sealed record DepositReferenceFiguresDto(Guid LoadPublicId, LineNature Nature, DateOnly CutoffDate, IReadOnlyList<CifraDeReferenciaDto> Figures,
    IReadOnlyList<CotejoDeCifraDto> Comparison, decimal TotalLoaded, decimal? TotalReference);

/// <summary>
/// Las <b>cifras de referencia</b> de la migración de aportes y captaciones (feature 014, US9, T156; FR-118; api.md §16): los totales por línea y concepto que
/// la cooperativa entrega de lo que lleva hoy, para cotejarlos con lo cargado antes de aplicar y de activar. Se guardan en la carga
/// (<c>LND_MigrationLoads.ReferenceFiguresJson</c>) y se reemplazan enteras. <b>Ninguna pantalla, mensaje ni plantilla nombra al sistema de origen</b>: se
/// llaman «cifras de referencia» y la columna <c>source</c> es libre («Balance de prueba de septiembre», «Reporte de saldos»). Puro: no lee. (nuevo)
/// </summary>
public static class CifrasDeReferencia
{
    public const string CodigoInvalidas = "Lending.Migration.ReferenceFiguresInvalid";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Los conceptos que admite cada naturaleza (los saldos que trae una cuenta migrada).</summary>
    public static IReadOnlyList<MemberAccountConcept> ConceptosDe(LineNature naturaleza) => naturaleza == LineNature.Contribution
        ? [MemberAccountConcept.ContributionMinimum, MemberAccountConcept.ContributionOrdinary, MemberAccountConcept.ContributionExtraordinary,
            MemberAccountConcept.ContributionRevaluation]
        : [MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable, MemberAccountConcept.ChargeReceivable];

    public static IReadOnlyList<CifraDeReferenciaDto> Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CifraDeReferenciaDto>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Escribir(IReadOnlyList<CifraDeReferenciaDto> cifras) => JsonSerializer.Serialize(cifras, Json);

    /// <summary>El cotejo por línea y concepto, ordenado; sin cifras, sólo lo cargado.</summary>
    public static IReadOnlyList<CotejoDeCifraDto> Cotejar(IReadOnlyList<TotalDeCargaDto> totales, IReadOnlyList<CifraDeReferenciaDto> cifras)
    {
        ArgumentNullException.ThrowIfNull(totales);
        ArgumentNullException.ThrowIfNull(cifras);
        static (string, string) Llave(string linea, string concepto) => (linea.ToUpperInvariant(), concepto);
        var porLlave = cifras.GroupBy(c => Llave(c.LineCode, c.Concept)).ToDictionary(g => g.Key, g => g.First());
        var resultado = new List<CotejoDeCifraDto>();
        foreach (var t in totales)
        {
            var llave = Llave(t.LineCode, t.Concept);
            porLlave.TryGetValue(llave, out var cifra);
            resultado.Add(new CotejoDeCifraDto(t.LineCode, t.Concept, t.Amount, t.Accounts, cifra?.Amount, cifra is null ? null : t.Amount - cifra.Amount,
                cifra?.Source));
            porLlave.Remove(llave);
        }
        foreach (var c in porLlave.Values)
            resultado.Add(new CotejoDeCifraDto(c.LineCode.ToUpperInvariant(), c.Concept, 0m, 0, c.Amount, -c.Amount, c.Source));
        return resultado.OrderBy(r => r.LineCode, StringComparer.Ordinal).ThenBy(r => r.Concept, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Los errores de unas cifras: línea de la naturaleza, concepto de la naturaleza, valor mayor o igual a cero con dos decimales, una por línea y concepto, y
    /// fuente de hasta 120. Vacía = válidas.
    /// </summary>
    public static IReadOnlyList<string> Validar(IReadOnlyList<CifraDeReferenciaDto> cifras, LineNature naturaleza, IReadOnlySet<string> lineas)
    {
        var errores = new List<string>();
        var conceptos = ConceptosDe(naturaleza).Select(c => c.ToString()).ToHashSet(StringComparer.Ordinal);
        var vistas = new HashSet<(string, string)>();
        for (var i = 0; i < cifras.Count; i++)
        {
            var c = cifras[i];
            var n = i + 1;
            if (string.IsNullOrWhiteSpace(c.LineCode) || !lineas.Contains(c.LineCode.Trim()))
                errores.Add(string.Create(CultureInfo.InvariantCulture, $"Cifra {n}: «{c.LineCode}» no es una línea de {ErroresDeMigracionDeCaptaciones.Nombre(naturaleza)}."));
            if (!conceptos.Contains(c.Concept ?? string.Empty))
                errores.Add(string.Create(CultureInfo.InvariantCulture, $"Cifra {n}: el concepto es uno de {string.Join(", ", conceptos)}."));
            if (c.Amount < 0m || decimal.Round(c.Amount, 2) != c.Amount)
                errores.Add(string.Create(CultureInfo.InvariantCulture, $"Cifra {n}: el valor es mayor o igual a cero, con a lo sumo dos decimales."));
            if (c.Source is { Length: > 120 })
                errores.Add(string.Create(CultureInfo.InvariantCulture, $"Cifra {n}: la fuente admite hasta 120 caracteres."));
            if (!vistas.Add(((c.LineCode ?? string.Empty).Trim().ToUpperInvariant(), c.Concept ?? string.Empty)))
                errores.Add(string.Create(CultureInfo.InvariantCulture, $"Cifra {n}: la línea {c.LineCode} y el concepto {c.Concept} ya tienen su cifra."));
        }
        return errores;
    }
}

/// <summary>
/// Las cifras de referencia de una carga con su cotejo (feature 014, US9, T156; api.md §16, <c>GET …/reference-figures</c>,
/// <c>Lending.Deposits.Migration</c>). Lo cargado se suma de las tandas vivas (sirve en borrador y aplicada). La carga es de toda la cooperativa, sin
/// alcance de oficina ni de línea. (nuevo)
/// </summary>
public sealed record GetDepositReferenceFiguresQuery(Guid MigrationLoadPublicId) : IRequest<Result<DepositReferenceFiguresDto>>;

public sealed class GetDepositReferenceFiguresQueryValidator : AbstractValidator<GetDepositReferenceFiguresQuery>
{
    public GetDepositReferenceFiguresQueryValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

public sealed class GetDepositReferenceFiguresQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDepositReferenceFiguresQuery, Result<DepositReferenceFiguresDto>>
{
    public async Task<Result<DepositReferenceFiguresDto>> Handle(GetDepositReferenceFiguresQuery request, CancellationToken ct)
    {
        var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, request.MigrationLoadPublicId, seguimiento: false, ct);
        if (carga is null) return Result.Failure<DepositReferenceFiguresDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
        return Result.Success(await ArmarAsync(db, carga, ct));
    }

    public static async Task<DepositReferenceFiguresDto> ArmarAsync(IApplicationDbContext db, Domain.Entities.Lending.Migration.MigrationLoad carga,
        CancellationToken ct)
    {
        var filas = await FilasDeLaCarga.LeerAsync(db, carga, ct);
        var totales = filas.Totales();
        var cifras = CifrasDeReferencia.Leer(carga.ReferenceFiguresJson);
        var cotejo = CifrasDeReferencia.Cotejar(totales, cifras);
        return new DepositReferenceFiguresDto(carga.PublicId, carga.Nature, carga.CutoffDate, cifras, cotejo, totales.Sum(t => t.Amount),
            cifras.Count == 0 ? null : cifras.Sum(c => c.Amount));
    }
}

/// <summary>
/// Registra (reemplaza enteras) las cifras de referencia de una carga en borrador o aplicada (feature 014, US9, T156; api.md §16, <c>PUT
/// …/reference-figures</c>, <c>Lending.Deposits.Migration</c>). Con errores, 422 <c>Lending.Migration.ReferenceFiguresInvalid</c> con
/// <c>data.errors</c> y nada se guarda. Una carga activada o reemplazada ya no cambia (<c>Lending.Migration.LoadNotDraft</c>). Toda la cooperativa, sin
/// alcance. (nuevo)
/// </summary>
public sealed record SetDepositReferenceFiguresCommand(Guid MigrationLoadPublicId, IReadOnlyList<CifraDeReferenciaDto> Figures)
    : IRequest<Result<DepositReferenceFiguresDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetDepositReferenceFiguresCommandValidator : AbstractValidator<SetDepositReferenceFiguresCommand>
{
    public SetDepositReferenceFiguresCommandValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.Figures).NotNull();
        RuleFor(x => x.Figures.Count).LessThanOrEqualTo(2000).When(x => x.Figures is not null).WithMessage("Hasta 2.000 cifras por carga.");
    }
}

public sealed class SetDepositReferenceFiguresCommandHandler(IApplicationDbContext db) : IRequestHandler<SetDepositReferenceFiguresCommand, Result<DepositReferenceFiguresDto>>
{
    public async Task<Result<DepositReferenceFiguresDto>> Handle(SetDepositReferenceFiguresCommand request, CancellationToken ct)
    {
        var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, request.MigrationLoadPublicId, seguimiento: true, ct);
        if (carga is null) return Result.Failure<DepositReferenceFiguresDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
        if (carga.Status is not (MigrationLoadStatus.Draft or MigrationLoadStatus.Applied))
            return Result.Failure<DepositReferenceFiguresDto>(ErroresDeActivacion.CargaNoBorrador());

        var lineas = (await db.CreditLines.AsNoTracking().Where(l => l.Nature == carga.Nature && !l.IsDeleted).Select(l => l.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errores = CifrasDeReferencia.Validar(request.Figures, carga.Nature, lineas);
        if (errores.Count > 0)
            return Result.Failure<DepositReferenceFiguresDto>(new ErrorConDatos(CifrasDeReferencia.CodigoInvalidas,
                "Las cifras de referencia tienen errores: corríjalas y vuelva a registrarlas.", new { errors = errores }));

        var normalizadas = request.Figures
            .Select(c => new CifraDeReferenciaDto(c.LineCode.Trim().ToUpperInvariant(), c.Concept, c.Amount, string.IsNullOrWhiteSpace(c.Source) ? null : c.Source.Trim()))
            .ToList();
        carga.ReferenceFiguresJson = CifrasDeReferencia.Escribir(normalizadas);
        await db.SaveChangesAsync(ct);
        return Result.Success(await GetDepositReferenceFiguresQueryHandler.ArmarAsync(db, carga, ct));
    }
}
