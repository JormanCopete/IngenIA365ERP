using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Disbursements.Tareas;

/// <summary>
/// Alerta las órdenes de giro sin resultado de Tesorería (feature 013, US3, T0455; data-model §7.4; FR-045, FR-016): cada orden emitida
/// (<c>Issued</c>, sin resultado que la mueva) cuyo <c>ResultDueOn</c> —la emisión más <c>Giros.DiasSinResultado</c> días hábiles, calculado al
/// emitirla— ya pasó, levanta <c>Cartera.GiroSinResultado</c> para quien desembolsa y quien anula órdenes, con el alcance de la oficina y la línea
/// del crédito. Idempotente por el <c>DedupKey</c> de la orden: repetir suma la ocurrencia, no otra alerta. Sin Cartera activada no hace nada.
/// Devuelve cuántas órdenes alertó. Sin ruta: lo corre la tarea <c>cartera.giros-sin-resultado</c>. (nuevo)
/// </summary>
public sealed record AlertDisbursementOrdersWithoutResultCommand : IRequest<Result<int>>;

public sealed class AlertDisbursementOrdersWithoutResultCommandValidator : AbstractValidator<AlertDisbursementOrdersWithoutResultCommand>;

public sealed class AlertDisbursementOrdersWithoutResultCommandHandler(IApplicationDbContext db, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<AlertDisbursementOrdersWithoutResultCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AlertDisbursementOrdersWithoutResultCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(0);
        var hoy = reloj.HoyLocal;
        var vencidas = await (
            from o in db.DisbursementOrders.AsNoTracking()
            join l in db.Loans.AsNoTracking() on o.LoanId equals l.Id
            join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on l.BranchId equals b.Id into oficinas
            from b in oficinas.DefaultIfEmpty()
            join c in db.CreditLines.AsNoTracking().IgnoreQueryFilters() on l.CreditLineId equals c.Id into lineas
            from c in lineas.DefaultIfEmpty()
            where !o.IsDeleted && o.Status == DisbursementOrderStatus.Issued && o.ResultDueOn < hoy
            orderby o.ResultDueOn, o.Id
            select new { o.PublicId, o.Number, o.NetAmount, o.ResultDueOn, o.VoidRequestedAt, Credito = l.Number, Oficina = (Guid?)b.PublicId, Linea = (Guid?)c.PublicId })
            .ToListAsync(ct);

        var alertadas = 0;
        foreach (var o in vencidas)
        {
            var cuerpo = $"La orden de giro {o.Number} del crédito {o.Credito} ({ErroresDeDesembolso.Pesos(o.NetAmount)}) esperaba el resultado de Tesorería "
                         + $"hasta el {o.ResultDueOn:yyyy-MM-dd}"
                         + (o.VoidRequestedAt is null ? "." : " y tiene la anulación pedida.")
                         + " Confirme con Tesorería si se giró, se rechazó o se anuló.";
            var r = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.GiroSinResultado, "Orden de giro sin resultado", cuerpo,
                EntityType: "DisbursementOrder", EntityPublicId: o.PublicId, DedupKey: $"{TiposDeAlerta.GiroSinResultado}:{o.PublicId:D}",
                ModuloDeOrigen: "Lending", ScopeBranchPublicId: o.Oficina, ScopeCreditLinePublicId: o.Linea), ct);
            if (r.IsSuccess && r.Value.Desenlace != DesenlaceDeAlerta.TipoInactivo) alertadas++;
        }
        return Result.Success(alertadas);
    }
}

/// <summary>
/// La tarea programada <c>cartera.giros-sin-resultado</c> (feature 013, US3, T0455; DT §2.13): una vez al día desde las 6:00, en cada cooperativa,
/// por <see cref="IEjecutorEnCooperativa"/> (el <c>ProgramadorDeTareas</c> la corre con el actor «Proceso de integración» y el arrendamiento
/// <c>scheduled.tasks</c>). Escribe sólo por <see cref="AlertDisbursementOrdersWithoutResultCommand"/>. Singleton. (nuevo)
/// </summary>
public sealed class TareaDeGirosSinResultado : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.giros-sin-resultado";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new AlertDisbursementOrdersWithoutResultCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"La revisión de giros sin resultado falló: {r.Error.Code} {r.Error.Message}");
    }
}

/// <summary>Lo que comparten las tareas del desembolso (feature 013, US3, T0455). (nuevo)</summary>
public static class TareasDeCartera
{
    /// <summary>A primera hora, antes de la jornada.</summary>
    public static readonly TimeOnly HoraDeInicio = new(6, 0);

    /// <summary>Una vez al día desde <see cref="HoraDeInicio"/>.</summary>
    public static bool UnaVezAlDia(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        TimeOnly.FromTimeSpan(ahoraLocal.TimeOfDay) >= HoraDeInicio
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    /// <summary>¿Cartera está activada en la cooperativa? Sin activar, ninguna tarea del desembolso hace nada.</summary>
    public static Task<bool> CarteraActivadaAsync(IApplicationDbContext db, CancellationToken ct) =>
        db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);
}
