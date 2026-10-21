using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Lo que hizo un re-escaneo (nuevo).</summary>
public sealed record ReEscaneoDeListasDto(bool Corrio, string? Motivo, int Consultadas, int Hallazgos, string? VersionDeLaCarga);

/// <summary>
/// El re-escaneo de deudores y codeudores vivos contra las listas restrictivas (feature 013, US2 bloque 6B; T0404; C90, I12; adaptadores §3.8). Sin
/// ruta: lo manda la tarea <see cref="ReEscaneoDeListas"/>. Sale de inmediato si Cartera no está activada; si no hay listas cargadas o la versión
/// vigente de las cargas (<c>CARGA-PROPIA</c>) ya se escaneó —lo dice la marca <c>LND_ProcessRuns</c> de clase <c>ScheduledTask</c> con la
/// huella de las versiones—, no hace nada (idempotente por la versión de la carga). Si no, consulta a cada deudor y codeudor de un crédito vivo
/// (<see cref="IConsultaDeListasDeCartera"/> con contexto <c>ListRescan</c>): cada hallazgo deja su <c>ScreeningResult</c>, el bloqueo automático
/// <c>RestrictiveListMatch</c> (impide originar y vender; el cobro de lo vivo sigue) y la alerta <c>Cartera.CoincidenciaEnListas</c>. (nuevo)
/// </summary>
public sealed record RescanRestrictiveListsCommand : IRequest<Result<ReEscaneoDeListasDto>>;

public sealed class RescanRestrictiveListsCommandValidator : AbstractValidator<RescanRestrictiveListsCommand>;

public sealed class RescanRestrictiveListsCommandHandler(
    IApplicationDbContext db,
    IConsultaDeListasDeCartera listas,
    ICargaDeListasRestrictivas cargas,
    IDateTimeService reloj) : IRequestHandler<RescanRestrictiveListsCommand, Result<ReEscaneoDeListasDto>>
{
    private static readonly LoanStatus[] Vivos = [LoanStatus.PendingDisbursement, LoanStatus.Active];

    public async Task<Result<ReEscaneoDeListasDto>> Handle(RescanRestrictiveListsCommand request, CancellationToken ct)
    {
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return Result.Success(new ReEscaneoDeListasDto(false, "NotActivated", 0, 0, null));
        var hoy = reloj.HoyLocal;
        var carga = await cargas.LeerVigenteAsync(hoy, ct);
        if (carga.Versiones.Count == 0) return Result.Success(new ReEscaneoDeListasDto(false, "NoLists", 0, 0, null));

        var version = Huella(carga.Versiones);
        var origen = $"Tarea:{ReEscaneoDeListas.NombreDeLaTarea}";
        if (await db.LendingProcessRuns.AsNoTracking().AnyAsync(r => r.Kind == LendingProcessKind.ScheduledTask && r.ExecutionOrigin == origen
                && r.ResultSha256 == version && !r.IsDeleted, ct))
            return Result.Success(new ReEscaneoDeListasDto(false, "AlreadyScanned", 0, 0, version));

        var deudores = await db.Loans.AsNoTracking().Where(l => !l.IsDeleted && Vivos.Contains(l.Status)).Select(l => l.DebtorPersonId).ToListAsync(ct);
        var codeudores = await (from p in db.LoanParties.AsNoTracking()
                                join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                                where !p.IsDeleted && !l.IsDeleted && Vivos.Contains(l.Status) && p.Role == CreditPartyRole.CoDebtor
                                      && (p.ReleasedOn == null || p.ReleasedOn > hoy) && (p.ExtinguishedOn == null || p.ExtinguishedOn > hoy)
                                select p.PersonId).ToListAsync(ct);
        var personas = deudores.Concat(codeudores).Distinct().Order().ToList();

        var hallazgos = 0;
        foreach (var persona in personas)
        {
            var r = await listas.ConsultarAsync(new PedidoDeConsultaDeListas(persona, ScreeningContext.ListRescan) { RegistrarSinHallazgo = false }, ct);
            if (r.Bloquea) hallazgos++;
        }

        var ahora = reloj.UtcNow;
        db.LendingProcessRuns.Add(new LendingProcessRun
        {
            Kind = LendingProcessKind.ScheduledTask,
            ProcessDate = hoy,
            Status = LendingProcessRunStatus.Completed,
            RequestedByKind = ActorKind.Process,
            RequestedByName = Actor.ProcesoDeIntegracion(origen).Name,
            ExecutionOrigin = origen,
            Reason = "Re-escaneo de deudores y codeudores vivos contra la carga vigente de listas restrictivas (C90).",
            TotalLoans = personas.Count,
            ProcessedLoans = personas.Count,
            TotalsJson = JsonSerializer.Serialize(new { people = personas.Count, findings = hallazgos, lists = carga.Versiones.Select(v => new { v.ListCode, v.Version }) }),
            ResultSha256 = version,
            RequestedAt = ahora,
            StartedAt = ahora,
            FinishedAt = ahora,
        });
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReEscaneoDeListasDto(true, null, personas.Count, hallazgos, version));
    }

    /// <summary>La huella de la versión vigente de las cargas: cada lista con su versión y su vigencia.</summary>
    public static string Huella(IReadOnlyList<VersionDeListaConsultada> versiones) =>
        new HuellaDeCartera()
            .Lista("lists", versiones.OrderBy(v => v.ListCode, StringComparer.Ordinal), (h, v) => h.Texto("code", v.ListCode).Entero("version", v.Version)
                .Fecha("validFrom", v.ValidFrom))
            .Calcular();
}

/// <summary>
/// La tarea programada <c>cartera.re-escaneo-de-listas</c> (feature 013, T0404; C90, I12; DT §2.13): cada <see cref="Cadencia"/>, en cada
/// cooperativa, por <see cref="IEjecutorEnCooperativa"/> con el arrendamiento <c>scheduled.tasks</c>. Sale si Cartera no está activada; si no,
/// manda <see cref="RescanRestrictiveListsCommand"/>, que sólo trabaja cuando hay una versión de la carga de listas sin escanear (al aplicarse una
/// carga nueva). Escribe sólo por el comando. Singleton. (nuevo)
/// </summary>
public sealed class ReEscaneoDeListas : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.re-escaneo-de-listas";

    /// <summary>Cada hora: una carga nueva se escanea dentro de la hora en que empieza a regir.</summary>
    public static readonly TimeSpan Cadencia = TimeSpan.FromHours(1);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Cadencia;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var db = servicios.GetRequiredService<IApplicationDbContext>();
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return;
        var logger = servicios.GetService<ILogger<ReEscaneoDeListas>>() ?? NullLogger<ReEscaneoDeListas>.Instance;
        var r = await servicios.GetRequiredService<ISender>().Send(new RescanRestrictiveListsCommand(), ct);
        if (r.IsFailure) logger.LogWarning("[Cartera.Listas] El re-escaneo de listas no corrió: {Codigo}", r.Error.Code);
        else if (r.Value.Hallazgos > 0)
            logger.LogInformation("[Cartera.Listas] El re-escaneo de listas encontró {Hallazgos} coincidencias en {Personas} personas.", r.Value.Hallazgos, r.Value.Consultadas);
    }
}
