using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.ColumnasDeSaldosDeAportes;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

// Feature 013, US2 (bloque 6B; T0407; D19; plantillas.md §3; api.md §7.4; data-model §2.15): el saldo de aportes y de ahorro permanente. Mientras
// Aportes y Ahorros no ofrezca su consulta (IConsultaDeAportes responde «no disponible»), Cartera usa lo cargado por la plantilla
// lending.contribution-balances. (nuevo)

/// <summary>
/// El saldo de aportes que usa Cartera (D19): de dónde salió (<see cref="Fuente"/> <c>Savings</c> o <c>Load</c>), la fecha de corte y, si salió
/// de la plantilla, la carga (<see cref="CargaId"/>) que el cupo calculado sella para que corregir una fila después no lo cambie. (nuevo)
/// </summary>
public sealed record SaldoDeAportes(decimal Aportes, decimal AhorroPermanente, DateOnly Corte, string Fuente, int? CargaId, Guid? LotePublicId)
{
    public const string DeAportes = "Savings";
    public const string DeLaPlantilla = "Load";
}

/// <summary>
/// El <b>lector único</b> del saldo de aportes de Cartera (feature 013, T0407; D19; contracts/interfaces.md §8.1): pregunta primero a
/// <see cref="IConsultaDeAportes"/> (hoy <see cref="ConsultaDeAportesNoDisponible"/>) y, si responde «no disponible», toma la última carga con
/// corte ≤ D conocida en R (<see cref="ContributionBalanceLoad.Vigente"/>). Lo usan los requisitos de la línea, el puntaje y el cupo calculado.
/// Nulo = sin saldo: no hay relación con aportes. (nuevo)
/// </summary>
public sealed class SaldoDeAportesVigente(IApplicationDbContext db, IConsultaDeAportes aportes)
{
    public async Task<SaldoDeAportes?> DeAsync(int personId, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        var publico = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == personId).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct);
        if (publico is { } persona)
        {
            var respuesta = await aportes.SaldosAsync(new SolicitudDeSaldoDeAportes([persona], fecha), ct);
            if (respuesta.Available)
                return respuesta.Balances.FirstOrDefault(b => b.PersonPublicId == persona) is { } saldo
                    ? new SaldoDeAportes(saldo.Contributions, saldo.PermanentSavings, saldo.AsOf, SaldoDeAportes.DeAportes, null, null)
                    : null;
        }

        var cargas = await db.ContributionBalanceLoads.AsNoTracking().Where(x => x.PersonId == personId && !x.IsDeleted).ToListAsync(ct);
        var carga = ContributionBalanceLoad.Vigente(cargas, fecha, informacionHasta ?? DateTime.MaxValue);
        return carga is null
            ? null
            : new SaldoDeAportes(carga.ContributionsBalance, carga.PermanentSavingsBalance, carga.AsOfDate, SaldoDeAportes.DeLaPlantilla, carga.Id,
                carga.ImportPublicId);
    }
}

/// <summary>
/// Plantilla 3 — saldos de aportes y ahorro permanente (feature 013, T0407; plantillas.md §3; <c>POST /api/lending/contribution-balances/import
/// ?mode=review|apply</c>, <c>Lending.CreditLimits.Assign</c>, C85). (nuevo)
/// </summary>
public sealed record ImportContributionBalancesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportContributionBalancesCommandValidator : AbstractValidator<ImportContributionBalancesCommand>
{
    public ImportContributionBalancesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportContributionBalancesCommand>.LargoMaximo);
    }
}

/// <summary>
/// Cada fila es un saldo de una persona a una fecha de corte, en un lote (<c>ImportPublicId</c>) por archivo aplicado. La persona se cita por
/// documento y debe existir viva (si no, <c>Import.Cell.NotFound</c>: la plantilla no crea personas); la fecha de corte no es futura; los saldos
/// no son negativos. Una fila igual a la última carga de esa persona y corte queda «sin cambio»; si cambia, es una carga nueva (hecho: la anterior
/// queda, y los cupos ya otorgados conservan la que sellaron). Todo o nada (<see cref="EjecutorDeImportacion"/>). (nuevo)
/// </summary>
public sealed class ImportContributionBalancesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ImportContributionBalancesCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportContributionBalancesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(CatalogoDePlantillasDeCartera.SaldosDeAportes, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var usuario = actor.UserId ?? 0;
        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var lote = Guid.NewGuid();

        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(C.Persona)).OfType<string>().Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var personas = (await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted).ToListAsync(ct))
            .GroupBy(p => p.TaxId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var ids = personas.Values.ToList();
        var anteriores = await db.ContributionBalanceLoads.AsNoTracking().Where(x => ids.Contains(x.PersonId) && !x.IsDeleted).ToListAsync(ct);

        foreach (var fila in ctx.Datos.Filas)
        {
            var documento = fila.Texto(C.Persona, 20);
            var corte = fila.Fecha(C.FechaDeCorte);
            var aportes = fila.Monto(C.SaldoDeAportes);
            var ahorro = fila.Monto(C.SaldoDeAhorroPermanente);
            var fuente = fila.Texto(C.Fuente, 120);
            fila.Requerido(C.Fuente);

            var llave = string.Join('|', documento, corte?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (!ctx.Datos.LlaveUnica(fila, llave, C.Persona) || fila.TieneErrores || documento is null || corte is null || aportes is null) continue;

            if (corte > hoy)
            {
                fila.Error(C.FechaDeCorte, ImportErrors.CellFormat,
                    $"La fecha de corte ({ErroresDeOriginacion.Fecha(corte.Value)}) es futura: el saldo es el de un corte ya pasado o de hoy.");
                continue;
            }
            if (aportes < 0m || ahorro < 0m)
            {
                fila.Error(aportes < 0m ? C.SaldoDeAportes : C.SaldoDeAhorroPermanente, ImportErrors.CellFormat, "Los saldos no son negativos.");
                continue;
            }
            if (!personas.TryGetValue(documento, out var persona))
            {
                fila.Error(C.Persona, ImportErrors.CellNotFound,
                    $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                continue;
            }

            var ultima = anteriores.Where(x => x.PersonId == persona && x.AsOfDate == corte).OrderByDescending(x => x.RegisteredAt).FirstOrDefault();
            if (ultima is not null && ultima.ContributionsBalance == aportes && ultima.PermanentSavingsBalance == (ahorro ?? 0m)
                && string.Equals(ultima.Source, fuente, StringComparison.Ordinal))
            {
                ctx.Registrar(fila, llave, AccionDeImportacion.Unchanged);
                continue;
            }

            db.ContributionBalanceLoads.Add(ContributionBalanceLoad.Registrar(persona, corte.Value, aportes.Value, ahorro ?? 0m, lote, ahora, usuario, fuente));
            ctx.Registrar(fila, llave, ultima is null ? AccionDeImportacion.Create : AccionDeImportacion.Update,
            [
                new(C.SaldoDeAportes, ultima?.ContributionsBalance.ToString(CultureInfo.InvariantCulture), aportes.Value.ToString(CultureInfo.InvariantCulture)),
            ]);
        }
    }
}

/// <summary>Un saldo cargado (api.md §7.4). (nuevo)</summary>
public sealed record ContributionBalanceDto(
    PersonaRefDto Person,
    decimal Contributions,
    decimal PermanentSavings,
    DateOnly CutoffDate,
    Guid LoadPublicId,
    DateTime LoadedAt,
    string? Source);

/// <summary>
/// Los saldos cargados (api.md §7.4, <c>GET /api/lending/contribution-balances</c>, <c>Lending.CreditLimits.Read</c>): por persona, la última
/// carga con corte ≤ <c>asOf</c> (hoy si no se dice). El saldo es de la persona, no de una oficina: no filtra por el alcance (exceptuado con
/// motivo). (nuevo)
/// </summary>
public sealed record ListContributionBalancesQuery(Guid? PersonPublicId = null, DateOnly? AsOf = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<ContributionBalanceDto>>>;

public sealed class ListContributionBalancesQueryValidator : AbstractValidator<ListContributionBalancesQuery>;

public sealed class ListContributionBalancesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListContributionBalancesQuery, Result<PagedResult<ContributionBalanceDto>>>
{
    public async Task<Result<PagedResult<ContributionBalanceDto>>> Handle(ListContributionBalancesQuery request, CancellationToken ct)
    {
        var fecha = request.AsOf ?? reloj.HoyLocal;
        var consulta = db.ContributionBalanceLoads.AsNoTracking().Where(x => !x.IsDeleted && x.AsOfDate <= fecha);
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(x => db.People.Any(p => p.Id == x.PersonId && p.PublicId == persona));

        var ultimas = (await consulta.ToListAsync(ct))
            .GroupBy(x => x.PersonId)
            .Select(g => ContributionBalanceLoad.Vigente(g, fecha, DateTime.MaxValue)!)
            .OrderBy(x => x.PersonId)
            .ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        var filas = ultimas.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.Select(x => x.PersonId), ct);
        var items = filas.Select(x => new ContributionBalanceDto(personas[x.PersonId], x.ContributionsBalance, x.PermanentSavingsBalance, x.AsOfDate,
            x.ImportPublicId, x.RegisteredAt, x.Source)).ToList();
        return Result.Success(new PagedResult<ContributionBalanceDto>(items, pagina.SafePage, pagina.SafePageSize, ultimas.Count));
    }
}
