using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Disbursements.Tareas;

/// <summary>
/// Pone al día los pagarés desmaterializados con el depósito (feature 013, US3, T0455; adaptadores §3.8; data-model §2.9): consulta cada pagaré
/// <c>Created</c> o <c>PendingSignature</c> por su canal <b>sellado</b> (<see cref="DepositoDelPagare.SincronizarAsync"/>: firma, custodia, sin
/// retroceder) y anota la sincronización; uno que siga sin firma pasados los días de vigencia de la aprobación
/// (<c>Originacion.DiasVigenciaDeAprobacion</c>) desde que se creó levanta <c>Cartera.PagarePendienteDeFirma</c> (idempotente por el
/// <c>DedupKey</c> del pagaré). Un pagaré cuyo canal no responde se deja para la pasada siguiente sin detener a los demás. Sin Cartera activada no
/// hace nada. Devuelve cuántos consultó. Sin ruta: lo corre la tarea <c>cartera.pagares</c>. (nuevo)
/// </summary>
public sealed record SyncPromissoryNotesCommand : IRequest<Result<int>>;

public sealed class SyncPromissoryNotesCommandValidator : AbstractValidator<SyncPromissoryNotesCommand>;

public sealed class SyncPromissoryNotesCommandHandler(
    IApplicationDbContext db,
    DepositoDelPagare deposito,
    ReglasDeParametrosDeCartera parametros,
    IAlertas alertas,
    IDateTimeService reloj,
    ILogger<SyncPromissoryNotesCommandHandler>? logger = null) : IRequestHandler<SyncPromissoryNotesCommand, Result<int>>
{
    /// <summary>Cuántos pagarés toma cada pasada.</summary>
    public const int TamanoDeTanda = 200;

    /// <summary>Los días de vigencia de una aprobación si el parámetro no se puede leer (es el valor por defecto del catálogo).</summary>
    public const int DiasDeVigenciaPorDefecto = 30;

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public async Task<Result<int>> Handle(SyncPromissoryNotesCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(0);
        var hoy = reloj.HoyLocal;
        var leido = await parametros.LeerAsync(ParametrosDeCartera.OriginacionDiasVigenciaDeAprobacion, hoy, ct: ct);
        var vigencia = leido.IsSuccess && leido.Value.Valor is int d && d > 0 ? d : DiasDeVigenciaPorDefecto;

        var ids = await db.PromissoryNotes.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Kind == PromissoryNoteKind.Dematerialized
                && (p.Status == PromissoryNoteStatus.Created || p.Status == PromissoryNoteStatus.PendingSignature))
            .OrderBy(p => p.LastSyncedAt).ThenBy(p => p.Id).Select(p => p.Id).Take(TamanoDeTanda).ToListAsync(ct);

        var consultados = 0;
        foreach (var id in ids)
        {
            var p = await db.PromissoryNotes.FirstAsync(x => x.Id == id, ct);
            var r = await deposito.SincronizarAsync(p, reloj.UtcNow, ct);
            if (r.IsFailure)
            {
                _log.LogWarning("[Cartera.Pagares] El pagaré {Pagare} no se pudo consultar: {Codigo}. Queda para la pasada siguiente.", p.PublicId, r.Error.Code);
                continue;
            }
            await db.SaveChangesAsync(ct);
            consultados++;

            if (p.Status is PromissoryNoteStatus.Created or PromissoryNoteStatus.PendingSignature && p.IssuedOn.AddDays(vigencia) < hoy)
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.PagarePendienteDeFirma, "Pagaré pendiente de firma",
                    $"El pagaré {p.Number ?? p.ExternalNoteId} creado el {p.IssuedOn:yyyy-MM-dd} sigue sin firma en el depósito después de {vigencia} días"
                    + (r.Value.Valor is { FirmaRechazada: true } ? " y el depósito informa la firma rechazada" : string.Empty)
                    + ". Contacte a los firmantes o anule el pagaré y cree otro.",
                    EntityType: "PromissoryNote", EntityPublicId: p.PublicId, DedupKey: $"{TiposDeAlerta.PagarePendienteDeFirma}:{p.PublicId:D}",
                    ModuloDeOrigen: "Lending"), ct);
        }
        return Result.Success(consultados);
    }
}

/// <summary>
/// La tarea programada <c>cartera.pagares</c> (feature 013, US3, T0455; DT §2.13): cada hora, en cada cooperativa, por
/// <see cref="IEjecutorEnCooperativa"/> (el <c>ProgramadorDeTareas</c> la corre con el actor «Proceso de integración» y el arrendamiento
/// <c>scheduled.tasks</c>). Escribe sólo por <see cref="SyncPromissoryNotesCommand"/>; reintentar lo que no llegó al depósito es esto mismo
/// (adaptadores §3.1, regla 4). Singleton. (nuevo)
/// </summary>
public sealed class TareaDePagares : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.pagares";

    /// <summary>Cada hora.</summary>
    public static readonly TimeSpan Cadencia = TimeSpan.FromHours(1);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Cadencia;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new SyncPromissoryNotesCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"La consulta de pagarés falló: {r.Error.Code} {r.Error.Message}");
    }
}
