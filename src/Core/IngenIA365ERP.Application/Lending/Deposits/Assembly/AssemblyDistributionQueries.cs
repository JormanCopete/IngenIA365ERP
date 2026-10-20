using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Withdrawals;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Assembly;

// Feature 014, US12 (T203, T205; contracts/api.md §12): la lista, el detalle y la simulación de las decisiones de asamblea, y su asunto de acta. (nuevo)

/// <summary><c>GET /api/lending/assembly-distributions?year=&amp;kind=&amp;status=</c>: las decisiones, las más recientes primero (sin líneas). (nuevo)</summary>
public sealed record ListAssemblyDistributionsQuery(int? Year = null, AssemblyDistributionKind? Kind = null, AssemblyDistributionStatus? Status = null)
    : IRequest<Result<IReadOnlyList<AssemblyDistributionDto>>>;

public sealed class ListAssemblyDistributionsQueryHandler(IApplicationDbContext db, DistribucionesDeAsamblea distribuciones, IAlcanceDeCartera alcance)
    : IRequestHandler<ListAssemblyDistributionsQuery, Result<IReadOnlyList<AssemblyDistributionDto>>>
{
    public async Task<Result<IReadOnlyList<AssemblyDistributionDto>>> Handle(ListAssemblyDistributionsQuery request, CancellationToken ct)
    {
        // Sin alcance total no hay decisiones que ver (son de toda la cooperativa): la lista sale vacía, sin evento (no se pidió una decisión).
        var vigente = await alcance.ObtenerAsync(ct);
        if (!(vigente.TodasLasOficinas && vigente.TodasLasLineas)) return Result.Success<IReadOnlyList<AssemblyDistributionDto>>([]);
        var filas = await db.AssemblyDistributions.AsNoTracking()
            .Where(d => !d.IsDeleted && (request.Year == null || d.FiscalYear == request.Year) && (request.Kind == null || d.Kind == request.Kind)
                        && (request.Status == null || d.Status == request.Status))
            .OrderByDescending(d => d.Id).Take(200).ToListAsync(ct);
        var items = new List<AssemblyDistributionDto>();
        foreach (var d in filas) items.Add(await distribuciones.DtoAsync(d, conLineas: false, ct));
        return Result.Success<IReadOnlyList<AssemblyDistributionDto>>(items);
    }
}

/// <summary><c>GET /api/lending/assembly-distributions/{id}</c>: la decisión con sus líneas aplicadas. (nuevo)</summary>
public sealed record GetAssemblyDistributionQuery(Guid DistributionPublicId) : IRequest<Result<AssemblyDistributionDto>>;

public sealed class GetAssemblyDistributionQueryHandler(DistribucionesDeAsamblea distribuciones)
    : IRequestHandler<GetAssemblyDistributionQuery, Result<AssemblyDistributionDto>>
{
    public async Task<Result<AssemblyDistributionDto>> Handle(GetAssemblyDistributionQuery request, CancellationToken ct)
    {
        var leida = await distribuciones.LeerAsync(request.DistributionPublicId, seguir: false, ct);
        return leida.IsFailure
            ? Result.Failure<AssemblyDistributionDto>(leida.Error)
            : Result.Success(await distribuciones.DtoAsync(leida.Value, conLineas: true, ct));
    }
}

/// <summary>
/// <c>POST /api/lending/assembly-distributions/{id}/simulate</c> (sin clave, US12-1): la simulación de un borrador a hoy —líneas, límite del IPC,
/// prorrateo al fondo, totales, avisos y la huella que la aplicación compara—. Sin IPC del ejercicio en una revalorización, <c>NationalValues.Missing</c>. (nuevo)
/// </summary>
public sealed record SimulateAssemblyDistributionQuery(Guid DistributionPublicId) : IRequest<Result<AssemblyDistributionSimulationDto>>;

public sealed class SimulateAssemblyDistributionQueryHandler(DistribucionesDeAsamblea distribuciones, IDateTimeService reloj)
    : IRequestHandler<SimulateAssemblyDistributionQuery, Result<AssemblyDistributionSimulationDto>>
{
    public async Task<Result<AssemblyDistributionSimulationDto>> Handle(SimulateAssemblyDistributionQuery request, CancellationToken ct)
    {
        var leida = await distribuciones.LeerAsync(request.DistributionPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<AssemblyDistributionSimulationDto>(leida.Error);
        if (leida.Value.Status != AssemblyDistributionStatus.Simulated)
            return Result.Failure<AssemblyDistributionSimulationDto>(ErroresDeAsamblea.NoEsBorrador(leida.Value.Status));
        var s = await distribuciones.SimularAsync(leida.Value, reloj.HoyLocal, ct);
        return s.IsFailure ? Result.Failure<AssemblyDistributionSimulationDto>(s.Error) : Result.Success(s.Value.Dto);
    }
}

/// <summary>
/// El asunto de acta <c>AssemblyDistribution</c> (feature 014, US12, T203; la 013 T0295): la decisión en borrador se lleva al orden del día de la asamblea
/// —el órgano que la cooperativa registre para su asamblea general— con su título y su resumen; quien la registró no vota. Cerrar el acta no la aplica: la
/// aplica otra persona con el punto cerrado (<c>POST /{id}/apply</c>), como el retiro del asociado. (nuevo)
/// </summary>
public sealed class AsuntoDeDecisionDeAsamblea(IApplicationDbContext db) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.AssemblyDistribution;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var d = await db.AssemblyDistributions.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == subjectPublicId && !x.IsDeleted, ct);
        if (d is null) return Result.Failure<AsuntoDeActa>(ErroresDeAsamblea.Inexistente());
        var huella = new HuellaDeCartera()
            .Identificador("distribution", d.PublicId)
            .Texto("number", d.Number)
            .Enumeracion("kind", (AssemblyDistributionKind?)d.Kind)
            .Entero("year", d.FiscalYear)
            .Enumeracion("base", (RevaluationBase?)d.BaseMethod)
            .Decimal("percent", d.DecidedFraction, 6)
            .Decimal("value", d.DecidedValue, 2)
            .Decimal("fund", d.FundAmount, 2)
            .Enumeracion("destination", d.SurplusDestination)
            .Calcular();
        var clase = d.Kind switch
        {
            AssemblyDistributionKind.Revaluation => "Revalorización de aportes",
            AssemblyDistributionKind.SurplusDistribution => "Distribución de excedentes",
            _ => "Amortización de aportes",
        };
        var decidido = d.DecidedFraction is { } p
            ? string.Create(CultureInfo.InvariantCulture, $"{p * 100m:0.######} %")
            : string.Create(CultureInfo.InvariantCulture, $"{d.DecidedValue:N2}");
        var titulo = string.Create(CultureInfo.InvariantCulture, $"{clase} del ejercicio {d.FiscalYear} ({d.Number})");
        var resumen = string.Create(CultureInfo.InvariantCulture,
            $"{decidido} sobre el {(d.BaseMethod switch { RevaluationBase.YearEndBalance => "saldo al 31 de diciembre", RevaluationBase.UploadedBase => "base que definió la asamblea (subida por plantilla)", _ => "saldo promedio diario" })}, con cargo a un fondo de {d.FundAmount:N2}.");
        var personas = new List<PersonaDelAsunto>();
        if (await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == d.RegisteredByUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct) is { } radico)
            personas.Add(new PersonaDelAsunto(radico, MotivosDeExclusion.Radicador));
        return Result.Success(new AsuntoDeActa(titulo, resumen, d.Status == AssemblyDistributionStatus.Simulated, d.Status.ToString(),
            new ExpedienteDeCartera(SegregacionDeCaptaciones.DecisionDeAsamblea, d.PublicId), huella)
        {
            Personas = personas,
        });
    }

    public Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct) => Task.FromResult(Result.Success());
}
