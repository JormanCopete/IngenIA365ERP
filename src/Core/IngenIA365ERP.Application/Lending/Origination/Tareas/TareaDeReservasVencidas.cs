using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Origination.Tareas;

/// <summary>
/// Vence las reservas de número que nadie usó ni liberó (feature 013, US6, T0585; FR-057, T22, C21; interfaces.md §5.2): cada
/// <c>LND_LoanNumberReservations</c> <c>Reserved</c> con <c>ExpiresOn</c> anterior a hoy pasa a <c>Expired</c> (deja de descontar cupo) y levanta
/// <c>Cartera.ReservaVencida</c> con su <c>DedupKey</c>. El número <b>nunca se reasigna</b>: si después llega la venta con ese número, lo toma
/// (<c>Expired → Used</c>). Sin Cartera activada no hace nada; repetir no vuelve a vencer ni duplica la alerta. Devuelve cuántas venció. Sin ruta: lo
/// corre la tarea <c>cartera.reservas-vencidas</c>. (nuevo)
/// </summary>
public sealed record ExpireLoanNumberReservationsCommand : IRequest<Result<int>>;

public sealed class ExpireLoanNumberReservationsCommandValidator : AbstractValidator<ExpireLoanNumberReservationsCommand>;

public sealed class ExpireLoanNumberReservationsCommandHandler(IApplicationDbContext db, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<ExpireLoanNumberReservationsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ExpireLoanNumberReservationsCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(0);
        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var vencidas = await db.LoanNumberReservations
            .Where(r => !r.IsDeleted && r.Status == LoanNumberReservationStatus.Reserved && r.ExpiresOn < hoy)
            .OrderBy(r => r.ExpiresOn).ThenBy(r => r.Id)
            .ToListAsync(ct);
        foreach (var r in vencidas)
        {
            r.Status = LoanNumberReservationStatus.Expired;
            r.ExpiredAt = ahora;
        }
        await db.SaveChangesAsync(ct);

        foreach (var r in vencidas)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.ReservaVencida, "Reserva de número vencida",
                $"La reserva del número {r.Number} por {Disbursements.ErroresDeDesembolso.Pesos(r.Amount)} venció el {r.ExpiresOn:yyyy-MM-dd} sin que la venta "
                + "se confirmara ni se liberara. El número no se reasigna: si la venta llega después, lo toma.",
                EntityType: "LoanNumberReservation", EntityPublicId: r.PublicId, DedupKey: $"{TiposDeAlerta.ReservaVencida}:{r.PublicId:D}",
                ModuloDeOrigen: "Lending"), ct);
        return Result.Success(vencidas.Count);
    }
}

/// <summary>
/// La tarea programada <c>cartera.reservas-vencidas</c> (feature 013, US6, T0585; DT §2.13): una vez al día desde las 6:00, en cada cooperativa, por
/// <see cref="IEjecutorEnCooperativa"/> (el <c>ProgramadorDeTareas</c> la corre con el actor «Proceso de integración»). Escribe sólo por
/// <see cref="ExpireLoanNumberReservationsCommand"/>; sin Cartera activada sale de inmediato. Singleton. (nuevo)
/// </summary>
public sealed class TareaDeReservasVencidas : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.reservas-vencidas";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new ExpireLoanNumberReservationsCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"El vencimiento de reservas de número falló: {r.Error.Code} {r.Error.Message}");
        await servicios.GetRequiredService<ISender>().Send(new Processes.Tareas.RegisterDailyLendingTaskRunCommand(Nombre), ct);
    }
}
