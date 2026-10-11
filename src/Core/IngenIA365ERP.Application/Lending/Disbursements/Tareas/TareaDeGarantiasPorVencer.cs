using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Disbursements.Tareas;

/// <summary>
/// Alerta las garantías por vencer (feature 013, US3, T0455; data-model §2.8; FR-043): cada garantía constituida o vigente cuya vigencia
/// (<c>ValidUntil</c>) o cuya póliza (<c>PolicyExpiresOn</c>) vence dentro de la anticipación del tipo de alerta
/// (<c>Cartera.GarantiaPorVencer.diasDeAnticipacion</c>, 30 por defecto) —o ya venció— levanta <c>Cartera.GarantiaPorVencer</c> para quien administra
/// garantías, con el alcance de la oficina y la línea de su dueño. Idempotente por el <c>DedupKey</c> de la garantía. Sin Cartera activada no hace
/// nada. Devuelve cuántas alertó. Sin ruta: lo corre la tarea <c>cartera.garantias-por-vencer</c>. (nuevo)
/// </summary>
public sealed record AlertExpiringCollateralsCommand : IRequest<Result<int>>;

public sealed class AlertExpiringCollateralsCommandValidator : AbstractValidator<AlertExpiringCollateralsCommand>;

public sealed class AlertExpiringCollateralsCommandHandler(IApplicationDbContext db, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<AlertExpiringCollateralsCommand, Result<int>>
{
    /// <summary>La anticipación si el tipo de alerta no trae su umbral (la del catálogo, data-model §11).</summary>
    public const int DiasDeAnticipacionPorDefecto = 30;

    public async Task<Result<int>> Handle(AlertExpiringCollateralsCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(0);
        var hoy = reloj.HoyLocal;
        var hasta = hoy.AddDays(await AnticipacionAsync(hoy, ct));

        var garantias = await db.Collaterals.AsNoTracking()
            .Where(c => !c.IsDeleted && (c.Status == CollateralStatus.Constituted || c.Status == CollateralStatus.Valid)
                && ((c.ValidUntil != null && c.ValidUntil <= hasta) || (c.PolicyExpiresOn != null && c.PolicyExpiresOn <= hasta)))
            .OrderBy(c => c.Id).ToListAsync(ct);
        var creditos = garantias.Where(g => g.LoanId is not null).Select(g => g.LoanId!.Value).Distinct().ToList();
        var deCredito = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => (l.BranchId, l.CreditLineId), ct);
        var solicitudes = garantias.Where(g => g.LoanId is null && g.ApplicationId is not null).Select(g => g.ApplicationId!.Value).Distinct().ToList();
        var deSolicitud = await db.CreditApplications.AsNoTracking().Where(a => solicitudes.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => (a.BranchId, a.CreditLineId), ct);
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(l => l.Id, l => l.PublicId, ct);

        var alertadas = 0;
        foreach (var g in garantias)
        {
            (int Oficina, int Linea)? dueno = g.LoanId is { } l && deCredito.TryGetValue(l, out var c) ? c
                : g.ApplicationId is { } a && deSolicitud.TryGetValue(a, out var s) ? s : null;
            var vence = new[] { g.ValidUntil, g.PolicyExpiresOn }.Where(f => f is not null && f <= hasta).Min()!.Value;
            var que = g.PolicyExpiresOn is { } poliza && poliza == vence ? $"la póliza {g.PolicyNumber}" : "la garantía";
            var cuerpo = $"{char.ToUpper(que[0], CultureInfo.InvariantCulture)}{que[1..]} de «{g.Description}» ({g.Kind}) "
                         + (vence < hoy ? $"venció el {vence:yyyy-MM-dd}" : $"vence el {vence:yyyy-MM-dd}")
                         + ". Gestione la renovación con el constituyente y registre la nueva vigencia.";
            var r = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.GarantiaPorVencer, "Garantía por vencer", cuerpo,
                EntityType: "Collateral", EntityPublicId: g.PublicId, DedupKey: $"{TiposDeAlerta.GarantiaPorVencer}:{g.PublicId:D}", ModuloDeOrigen: "Lending",
                ScopeBranchPublicId: dueno is { } d1 && oficinas.TryGetValue(d1.Oficina, out var o) ? o : null,
                ScopeCreditLinePublicId: dueno is { } d2 && lineas.TryGetValue(d2.Linea, out var li) ? li : null), ct);
            if (r.IsSuccess && r.Value.Desenlace != DesenlaceDeAlerta.TipoInactivo) alertadas++;
        }
        return Result.Success(alertadas);
    }

    /// <summary>La anticipación del tipo de alerta vigente (<c>ThresholdsJson.diasDeAnticipacion</c>), o la del catálogo.</summary>
    private async Task<int> AnticipacionAsync(DateOnly hoy, CancellationToken ct)
    {
        var umbrales = await db.AlertTypes.AsNoTracking()
            .Where(t => t.TypeCode == TiposDeAlerta.GarantiaPorVencer && t.ValidFrom <= hoy && (t.ValidTo == null || t.ValidTo >= hoy))
            .OrderByDescending(t => t.ValidFrom).Select(t => t.ThresholdsJson).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(umbrales)) return DiasDeAnticipacionPorDefecto;
        try
        {
            using var json = JsonDocument.Parse(umbrales);
            return json.RootElement.TryGetProperty(TiposDeAlerta.DiasDeAnticipacion, out var dias) && dias.TryGetDecimal(out var valor) && valor >= 0
                ? (int)valor
                : DiasDeAnticipacionPorDefecto;
        }
        catch (JsonException)
        {
            return DiasDeAnticipacionPorDefecto;
        }
    }
}

/// <summary>
/// La tarea programada <c>cartera.garantias-por-vencer</c> (feature 013, US3, T0455; DT §2.13): una vez al día desde las 6:00, en cada cooperativa,
/// por <see cref="IEjecutorEnCooperativa"/> (el <c>ProgramadorDeTareas</c> la corre con el actor «Proceso de integración» y el arrendamiento
/// <c>scheduled.tasks</c>). Escribe sólo por <see cref="AlertExpiringCollateralsCommand"/>. Singleton. (nuevo)
/// </summary>
public sealed class TareaDeGarantiasPorVencer : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.garantias-por-vencer";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new AlertExpiringCollateralsCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"La revisión de garantías por vencer falló: {r.Error.Code} {r.Error.Message}");
        // Feature 013, US4 (T0502, SC-020): la marca de la última ejecución, que vigila cartera.vigilancia.
        await servicios.GetRequiredService<ISender>().Send(new Processes.Tareas.RegisterDailyLendingTaskRunCommand(Nombre), ct);
    }
}
