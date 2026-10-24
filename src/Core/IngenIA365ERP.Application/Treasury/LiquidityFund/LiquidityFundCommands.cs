using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.LiquidityFund;

// Feature 015, US10 (T0139; FR-207; SC-028; contracts/api.md §9; data-model §9.4): el fondo de liquidez restringido. Ningún lote, remesa ni traslado sale de una
// cuenta del fondo sin un uso aprobado por el monto (GuardaDelFondoDeLiquidez); el uso lleva causal de la norma, soporte, aprobación de otra persona (aprobación
// diferida, sujeto LiquidityFundUse) y la alerta del aviso previo a la Supersolidaria. Los abonos al fondo no se restringen. (nuevo)

public sealed record LiquidityFundUseDto(
    Guid PublicId,
    string Number,
    Guid BankAccountPublicId,
    string BankAccountCode,
    LiquidityFundCause Cause,
    decimal Amount,
    string Reason,
    Guid SupportAttachmentPublicId,
    Guid? SupervisorNoticeAttachmentPublicId,
    LiquidityFundUseState Status,
    Guid? ApprovalRequestPublicId,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    string? DecisionReason,
    Guid? UsedByDocumentPublicId);

/// <summary>Lo común de los usos del fondo: el DTO y la huella que el aprobador confirma. (nuevo)</summary>
public static class UsosDelFondo
{
    public const string Codigo = "UF";

    public static string Huella(LiquidityFundUse u) => HuellaDeOperacion.Calcular("Tesoreria.UsoDelFondo",
        new { u.PublicId, u.BankAccountId, u.Cause, u.Amount, u.Reason, u.SupportAttachmentPublicId });

    public static async Task<LiquidityFundUseDto> DtoAsync(IApplicationDbContext db, LiquidityFundUse u, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == u.BankAccountId).Select(c => new { c.PublicId, c.Code }).FirstAsync(ct);
        return new LiquidityFundUseDto(u.PublicId, u.Number, cuenta.PublicId, cuenta.Code, u.Cause, u.Amount, u.Reason, u.SupportAttachmentPublicId,
            u.SupervisorNoticeAttachmentPublicId, u.Status, u.ApprovalRequestPublicId, u.RequestedAt, u.DecidedAt, u.DecisionReason, u.UsedByDocumentPublicId);
    }
}

/// <summary>
/// La guarda del fondo de liquidez (feature 015, US10, T0139; FR-207; SC-028): la consultan los lotes (fase 7), las remesas (fase 14) y los traslados (T1) antes
/// de sacar dinero de una cuenta. Una cuenta que no es del fondo pasa sin más. Desde una del fondo exige un uso <see cref="LiquidityFundUseState.Approved"/> de esa
/// cuenta por al menos el monto (el pedido o el más antiguo que alcance); si no, <c>Treasury.LiquidityFund.UseRequired</c>. <see cref="MarcarUsadoAsync"/> lo
/// deja <see cref="LiquidityFundUseState.Used"/> con el documento, en la unidad de trabajo de quien llama. (nuevo)
/// </summary>
public sealed class GuardaDelFondoDeLiquidez(IApplicationDbContext db)
{
    public async Task<Result<LiquidityFundUse?>> ExigirAsync(OwnBankAccount cuenta, decimal monto, Guid? usoPublicId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        if (!cuenta.EsDelFondoDeLiquidez) return Result.Success<LiquidityFundUse?>(null);
        var consulta = db.LiquidityFundUses.Where(u => u.BankAccountId == cuenta.Id && u.Status == LiquidityFundUseState.Approved && u.Amount >= monto);
        if (usoPublicId is { } uso) consulta = consulta.Where(u => u.PublicId == uso);
        var aprobado = await consulta.OrderBy(u => u.Id).FirstOrDefaultAsync(ct);
        return aprobado is null ? Result.Failure<LiquidityFundUse?>(ErroresDeTesoreria.UsoDelFondoRequerido(monto)) : Result.Success<LiquidityFundUse?>(aprobado);
    }

    public static void MarcarUsado(LiquidityFundUse uso, Guid documento)
    {
        ArgumentNullException.ThrowIfNull(uso);
        uso.Status = LiquidityFundUseState.Used;
        uso.UsedByDocumentPublicId = documento;
    }
}

/// <summary>Lo que hace el uso del fondo cuando su aprobación se completa o se devuelve (<see cref="IAprobableDeTesoreria"/>). (nuevo)</summary>
public sealed class AprobableUsoDelFondo(IApplicationDbContext db, IDateTimeService reloj) : IAprobableDeTesoreria
{
    public string Sujeto => ApprovalSubjects.LiquidityFundUse;

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct) =>
        await DecidirAsync(solicitud, LiquidityFundUseState.Approved, null, ct);

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        await DecidirAsync(solicitud, LiquidityFundUseState.Rejected, motivo, ct);

    private async Task<Result<EstadoDeFuenteDto>> DecidirAsync(ApprovalRequest solicitud, LiquidityFundUseState estado, string? motivo, CancellationToken ct)
    {
        var uso = await db.LiquidityFundUses.FirstOrDefaultAsync(u => u.PublicId == solicitud.SourcePublicId, ct);
        if (uso is null) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.NoEncontrado("El uso del fondo"));
        if (uso.Status != LiquidityFundUseState.PendingApproval) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.UsoNoPendiente());
        uso.Status = estado;
        uso.DecidedAt = reloj.UtcNow;
        uso.DecisionReason = motivo;
        return Result.Success(new EstadoDeFuenteDto(uso.PublicId, ApprovalSourceTypes.TreasuryDocument, estado.ToString(), solicitud.SourceLabel));
    }
}

/// <summary><c>GET /api/treasury/liquidity-fund-uses?bankAccount=&amp;state=</c> (<c>Treasury.LiquidityFund.Request</c>). (nuevo)</summary>
public sealed record ListLiquidityFundUsesQuery(Guid? BankAccountPublicId = null, LiquidityFundUseState? State = null)
    : IRequest<Result<IReadOnlyList<LiquidityFundUseDto>>>;

public sealed class ListLiquidityFundUsesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListLiquidityFundUsesQuery, Result<IReadOnlyList<LiquidityFundUseDto>>>
{
    public async Task<Result<IReadOnlyList<LiquidityFundUseDto>>> Handle(ListLiquidityFundUsesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.LiquidityFundUses.AsNoTracking();
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(u => u.BankAccountId == (id ?? -1));
        }
        if (request.State is { } e) consulta = consulta.Where(u => u.Status == e);
        var usos = (await consulta.OrderByDescending(u => u.Id).Take(200).ToListAsync(ct)).Where(u => mio.IncluyeCuenta(u.BankAccountId)).ToList();
        var lista = new List<LiquidityFundUseDto>(usos.Count);
        foreach (var u in usos) lista.Add(await UsosDelFondo.DtoAsync(db, u, ct));
        return Result.Success<IReadOnlyList<LiquidityFundUseDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/treasury/liquidity-fund-uses</c> (<c>Treasury.LiquidityFund.Request</c>): pide un uso del fondo con su causal, monto, motivo y soporte; pide la
/// aprobación (sujeto <c>LiquidityFundUse</c>, permiso con monto <c>Treasury.LiquidityFund.Approve</c>) y alerta el aviso previo a la Supersolidaria
/// (<c>Tesoreria.UsoDelFondoDeLiquidez</c>). Sólo para una cuenta del fondo (<c>Treasury.LiquidityFund.NotLiquidityFundAccount</c>). (nuevo)
/// </summary>
public sealed record RequestLiquidityFundUseCommand(
    Guid BankAccountPublicId,
    LiquidityFundCause Cause,
    decimal Amount,
    string Reason,
    Guid SupportAttachmentPublicId,
    Guid? SupervisorNoticeAttachmentPublicId = null) : IRequest<Result<LiquidityFundUseDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestLiquidityFundUseCommandValidator : AbstractValidator<RequestLiquidityFundUseCommand>
{
    public RequestLiquidityFundUseCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta del fondo.");
        RuleFor(x => x.Cause).IsInEnum().WithMessage("La causal es atender a depositantes o reducción de depósitos.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto del uso tiene que ser positivo.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del uso.").MaximumLength(500);
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte del uso del fondo.");
    }
}

public sealed class RequestLiquidityFundUseCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    AprobacionDiferidaEnTesoreria aprobacion,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<RequestLiquidityFundUseCommand, Result<LiquidityFundUseDto>>
{
    public const string PermisoDeAprobar = "Treasury.LiquidityFund.Approve";

    public async Task<Result<LiquidityFundUseDto>> Handle(RequestLiquidityFundUseCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<LiquidityFundUseDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.BankAccountPublicId, seguir: false, ct);
        if (cuenta is null) return Result.Failure<LiquidityFundUseDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        if (!cuenta.EsDelFondoDeLiquidez) return Result.Failure<LiquidityFundUseDto>(ErroresDeTesoreria.NoEsDelFondo(cuenta.Code));

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: false, ct);
        var hoy = reloj.HoyLocal;
        var uso = new LiquidityFundUse
        {
            BankAccountId = cuenta.Id, Cause = request.Cause, Amount = request.Amount, Reason = request.Reason.Trim(),
            SupportAttachmentPublicId = request.SupportAttachmentPublicId, SupervisorNoticeAttachmentPublicId = request.SupervisorNoticeAttachmentPublicId,
            RequestedByUserId = usuario, RequestedAt = reloj.UtcNow,
        };
        var pedida = await aprobacion.PedirAsync(new PedidoDeAprobacionDiferida(ApprovalSubjects.LiquidityFundUse, uso.PublicId,
            string.Create(CultureInfo.InvariantCulture, $"Uso del fondo de liquidez de {cuenta.Code}"), request.Amount, hoy, UsosDelFondo.Huella(uso),
            PermisoDeAprobar, [usuario]), ct);
        if (pedida.IsFailure) return Result.Failure<LiquidityFundUseDto>(pedida.Error);
        uso.ApprovalRequestPublicId = pedida.Value?.PublicId;
        if (pedida.Value is null) uso.Status = LiquidityFundUseState.Approved;
        uso.Number = await numerador.AsignarAsync(UsosDelFondo.Codigo, UsosDelFondo.Codigo, ct);
        db.LiquidityFundUses.Add(uso);
        await db.SaveChangesAsync(ct);

        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaUsoDelFondoDeLiquidez, "Uso del fondo de liquidez",
            string.Create(CultureInfo.InvariantCulture,
                $"Se pidió el uso {uso.Number} del fondo de liquidez en {cuenta.Code} por {request.Amount:N0} ({request.Cause}). Dé el aviso previo a la Supersolidaria."),
            "LiquidityFundUse", uso.PublicId, DedupKey: $"Tesoreria.UsoDelFondoDeLiquidez:{uso.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        return Result.Success(await UsosDelFondo.DtoAsync(db, uso, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/liquidity-fund-uses/{id}/approve</c> (<c>Treasury.LiquidityFund.Approve</c>, con monto): decide el nivel actual por el motor de
/// aprobaciones; quien pidió no aprueba (<c>Treasury.LiquidityFund.SelfApproval</c>). Aprobar no mueve dinero: deja el uso disponible para un lote. (nuevo)
/// </summary>
public sealed record ApproveLiquidityFundUseCommand(Guid UsePublicId, string? Reason = null) : IRequest<Result<LiquidityFundUseDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveLiquidityFundUseCommandValidator : AbstractValidator<ApproveLiquidityFundUseCommand>
{
    public ApproveLiquidityFundUseCommandValidator()
    {
        RuleFor(x => x.UsePublicId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class ApproveLiquidityFundUseCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IMotorDeAprobaciones motor)
    : IRequestHandler<ApproveLiquidityFundUseCommand, Result<LiquidityFundUseDto>>
{
    public async Task<Result<LiquidityFundUseDto>> Handle(ApproveLiquidityFundUseCommand request, CancellationToken ct) =>
        await DecisionDelUso.DecidirAsync(db, alcance, motor, request.UsePublicId, ApprovalDecisionKind.Approve, request.Reason, ct);
}

/// <summary><c>POST /api/treasury/liquidity-fund-uses/{id}/reject</c> (<c>Treasury.LiquidityFund.Approve</c>): devuelve el uso con motivo. (nuevo)</summary>
public sealed record RejectLiquidityFundUseCommand(Guid UsePublicId, string Reason) : IRequest<Result<LiquidityFundUseDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RejectLiquidityFundUseCommandValidator : AbstractValidator<RejectLiquidityFundUseCommand>
{
    public RejectLiquidityFundUseCommandValidator()
    {
        RuleFor(x => x.UsePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del rechazo.").MaximumLength(500);
    }
}

public sealed class RejectLiquidityFundUseCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IMotorDeAprobaciones motor)
    : IRequestHandler<RejectLiquidityFundUseCommand, Result<LiquidityFundUseDto>>
{
    public async Task<Result<LiquidityFundUseDto>> Handle(RejectLiquidityFundUseCommand request, CancellationToken ct) =>
        await DecisionDelUso.DecidirAsync(db, alcance, motor, request.UsePublicId, ApprovalDecisionKind.Reject, request.Reason, ct);
}

internal static class DecisionDelUso
{
    public static async Task<Result<LiquidityFundUseDto>> DecidirAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IMotorDeAprobaciones motor, Guid uso,
        ApprovalDecisionKind decision, string? motivo, CancellationToken ct)
    {
        var fila = await db.LiquidityFundUses.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == uso, ct);
        if (fila is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(fila.BankAccountId))
            return Result.Failure<LiquidityFundUseDto>(ErroresDeTesoreria.NoEncontrado("El uso del fondo"));
        if (fila.Status != LiquidityFundUseState.PendingApproval || fila.ApprovalRequestPublicId is not { } solicitud)
            return Result.Failure<LiquidityFundUseDto>(ErroresDeTesoreria.UsoNoPendiente());
        var decidida = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud, decision, motivo, UsosDelFondo.Huella(fila)), ct);
        if (decidida.IsFailure) return Result.Failure<LiquidityFundUseDto>(decidida.Error);
        var actual = await db.LiquidityFundUses.AsNoTracking().FirstAsync(u => u.Id == fila.Id, ct);
        return Result.Success(await UsosDelFondo.DtoAsync(db, actual, ct));
    }
}
