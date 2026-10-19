using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Withdrawals;

/// <summary>
/// Los errores del retiro del asociado (feature 014, US10; api.md §11 y §22; research E66). En las rutas, 422 con <c>data</c> (404 el inexistente). Mensajes
/// en español que dicen qué hacer (FR-013). (nuevo)
/// </summary>
public static class ErroresDeRetiro
{
    public const string CodigoInexistente = "Lending.MembershipWithdrawal.NotFound";
    public const string CodigoEnCurso = "Lending.MembershipWithdrawal.AlreadyInProgress";
    public const string CodigoNoAsociado = "Lending.MembershipWithdrawal.NotAssociate";
    public const string CodigoNoRadicado = "Lending.MembershipWithdrawal.NotFiled";
    public const string CodigoNoAprobado = "Lending.MembershipWithdrawal.NotApproved";
    public const string CodigoNoLiquidado = "Lending.MembershipWithdrawal.NotSettled";
    public const string CodigoFechaEfectiva = "Lending.MembershipWithdrawal.EffectiveDateInvalid";
    public const string CodigoFechaDeLiquidacion = "Lending.MembershipWithdrawal.SettlementDateInvalid";
    public const string CodigoPerdidasNoAdmitidas = "Lending.MembershipWithdrawal.LossDeductionNotEnabled";
    public const string CodigoCruceRechazado = "Lending.MembershipWithdrawal.OffsetRejected";
    public const string CodigoNadaQueDevolver = "Lending.MembershipWithdrawal.NothingToRefund";
    public const string CodigoCuentasCerradas = "Lending.MembershipWithdrawal.AccountsClosed";
    public const string CodigoReaperturaConCuentaViva = "Lending.MembershipWithdrawal.ReopenConflict";
    public const string CodigoVistaPreviaCambio = "Lending.Savings.PreviewChanged";
    public const string CodigoPuntoNoCorresponde = ErroresDeGobierno.CodigoPuntoNoCorresponde;

    public static Error Inexistente() => new(CodigoInexistente, "El retiro de asociado no existe.");

    public static Error EnCurso(string numero, MembershipWithdrawalStatus estado) => new ErrorConDatos(CodigoEnCurso,
        $"La persona ya tiene el retiro {numero} en trámite («{estado}»): desista de ése o termínelo antes de radicar otro.",
        new { number = numero, status = estado.ToString() });

    public static Error NoAsociado(string motivo) => new ErrorConDatos(CodigoNoAsociado,
        $"No se radica el retiro: {motivo}.", new { reason = motivo });

    public static Error NoRadicado(MembershipWithdrawalStatus estado) => new ErrorConDatos(CodigoNoRadicado,
        $"El retiro está «{estado}»: sólo se desiste o se decide uno radicado.", new { status = estado.ToString() });

    public static Error NoAprobado(MembershipWithdrawalStatus estado) => new ErrorConDatos(CodigoNoAprobado,
        $"El retiro está «{estado}»: sólo se liquida uno aprobado o por rehacer.", new { status = estado.ToString() });

    public static Error NoLiquidado(MembershipWithdrawalStatus estado) => new ErrorConDatos(CodigoNoLiquidado,
        $"El retiro está «{estado}»: la devolución se aprueba en un retiro liquidado.", new { status = estado.ToString() });

    public static Error FechaEfectiva(DateOnly fecha, DateOnly radicado) => new ErrorConDatos(CodigoFechaEfectiva,
        $"La fecha del retiro ({Fecha(fecha)}) no puede ser anterior a la radicación ({Fecha(radicado)}).",
        new { effectiveDate = Fecha(fecha), filedOn = Fecha(radicado) });

    public static Error FechaDeLiquidacion(DateOnly fecha, DateOnly desde, DateOnly hasta) => new ErrorConDatos(CodigoFechaDeLiquidacion,
        $"La liquidación va con fecha entre {Fecha(desde)} (la fecha del retiro) y {Fecha(hasta)} (hoy): {Fecha(fecha)} no.",
        new { date = Fecha(fecha), min = Fecha(desde), max = Fecha(hasta) });

    public static Error PerdidasNoAdmitidas() => new(CodigoPerdidasNoAdmitidas,
        "La línea de aportes no admite descontar pérdidas al retiro (D15): actívelo en la versión de la línea antes de liquidar con descuento.");

    public static Error CruceRechazado(string credito, string codigo, string mensaje) => new ErrorConDatos(CodigoCruceRechazado,
        $"El cruce contra el crédito {credito} no se aplicó ({codigo}: {mensaje}); no se aplicó nada.",
        new { loan = credito, code = codigo, reason = mensaje });

    public static Error NadaQueDevolver() => new(CodigoNadaQueDevolver, "El retiro no tiene una devolución pendiente que aprobar.");

    public static Error CuentasCerradas(IReadOnlyList<string> cuentas) => new ErrorConDatos(CodigoCuentasCerradas,
        $"El pago es del cruce de un retiro ya pagado y sus cuentas ({string.Join(", ", cuentas)}) están canceladas: reversarlo reabre las cuentas con el "
        + "saldo a favor del exasociado y exige el permiso Lending.MembershipWithdrawals.ReverseAfterPayment (D44).",
        new { accounts = cuentas, requiredPermission = "Lending.MembershipWithdrawals.ReverseAfterPayment" });

    public static Error ReaperturaConCuentaViva(string cuenta) => new ErrorConDatos(CodigoReaperturaConCuentaViva,
        $"La cuenta de aportes {cuenta} no se puede reabrir: el exasociado ya tiene otra cuenta de aportes viva (reingresó). Resuelva la devolución con soporte.",
        new { account = cuenta });

    public static Error VistaPreviaCambio(string esperada, string actual) => new ErrorConDatos(CodigoVistaPreviaCambio,
        "La liquidación cambió desde la vista previa (un pago, una consignación o un saldo nuevo): vuelva a verla antes de liquidar.",
        new { expectedPreviewSha256 = esperada, previewSha256 = actual });

    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

// ------------------------------------------------------------------------------------------------------------- DTO --

/// <summary>La aprobación (o el rechazo) del retiro con su punto de acta. (nuevo)</summary>
public sealed record MembershipWithdrawalApprovalDto(Guid SessionItemPublicId, string? SessionNumber, DateTime On, DateOnly? EffectiveDate, string? Reason);

/// <summary>Los totales de la liquidación vigente. (nuevo)</summary>
public sealed record MembershipWithdrawalSettlementDto(
    int Version,
    DateTime SettledAt,
    decimal Contributions,
    decimal Savings,
    decimal Crossed,
    decimal LossDeduction,
    decimal Refund,
    DateOnly? RefundDueOn,
    bool RefundHeldByInstitutionalMinimum,
    Guid? DocumentPublicId,
    string? DocumentNumber);

/// <summary>Una orden de devolución del retiro. (nuevo)</summary>
public sealed record MembershipWithdrawalRefundOrderDto(Guid PublicId, string Number, string AccountNumber, decimal Amount, DisbursementOrderStatus Status,
    DateOnly ResultDueOn);

/// <summary>Una línea de la liquidación (data-model §6.2). (nuevo)</summary>
public sealed record MembershipWithdrawalLineDto(
    int Version,
    int LineNumber,
    MembershipWithdrawalLineKind Kind,
    Guid? AccountPublicId,
    string? AccountNumber,
    Guid? LoanPublicId,
    string? LoanNumber,
    string? Concept,
    decimal Amount,
    string? AccountOperationNumber,
    string? LoanOperationNumber,
    string? Notes);

/// <summary>El trámite del retiro (api.md §11). Las líneas sólo en el detalle. (nuevo)</summary>
public sealed record MembershipWithdrawalDto(
    Guid PublicId,
    string Number,
    MemberAccountPersonDto Person,
    Guid ContributionAccountPublicId,
    string ContributionAccountNumber,
    MembershipWithdrawalCause Cause,
    string? CauseDetail,
    DateOnly FiledOn,
    MembershipWithdrawalStatus Status,
    IReadOnlyList<Guid> SupportAttachmentPublicIds,
    MembershipWithdrawalApprovalDto? Approval,
    DateTime? DesistedAt,
    string? DesistReason,
    MembershipWithdrawalSettlementDto? Settlement,
    string? ToRedoReason,
    DateTime? PaidAt,
    IReadOnlyList<MembershipWithdrawalRefundOrderDto> RefundOrders,
    IReadOnlyList<MembershipWithdrawalLineDto> Lines);

// --------------------------------------------------------------------------------------------------------- comunes --

/// <summary>
/// Lo común del trámite del retiro (feature 014, US10, T097): el expediente de la segregación, el de la devolución, el trámite en alcance (por la oficina y
/// la línea de la cuenta de aportes; fuera, el 404 genérico) y el DTO. (nuevo)
/// </summary>
public sealed class RetirosDeAsociado(IApplicationDbContext db, LendingAuditEmitter auditoria)
{
    /// <summary>El tipo de expediente de la segregación del trámite (radicar × decidir).</summary>
    public const string Expediente = nameof(MembershipWithdrawal);

    /// <summary>El tipo de expediente de la devolución (quien liquida no aprueba la devolución retenida).</summary>
    public const string ExpedienteDeDevolucion = "MembershipWithdrawalRefund";

    /// <summary>El prefijo del radicado (<c>LendingNumberKind.MembershipWithdrawal</c>).</summary>
    public const string Prefijo = "RTA-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// La raíz del expediente de la devolución: un <c>Guid</c> fijo derivado del trámite (la segregación compara por raíz; la del trámite ya tiene a quien
    /// aprobó el retiro, que sí puede liquidarlo).
    /// </summary>
    public static Guid RaizDeLaDevolucion(Guid retiro)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{ExpedienteDeDevolucion}:{retiro:N}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    /// <summary>
    /// El trámite en el alcance de quien pregunta (sin seguimiento salvo que se pida): la oficina y la línea de su cuenta de aportes; fuera o inexistente,
    /// <c>Lending.MembershipWithdrawal.NotFound</c> (con el evento de fuera de alcance).
    /// </summary>
    public async Task<Result<MembershipWithdrawal>> EnAlcanceAsync(IAlcanceDeCartera alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.MembershipWithdrawals : db.MembershipWithdrawals.AsNoTracking();
        var retiro = await consulta.FirstOrDefaultAsync(w => w.PublicId == publicId && !w.IsDeleted, ct);
        if (retiro is null) return Result.Failure<MembershipWithdrawal>(ErroresDeRetiro.Inexistente());
        var cuenta = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().FirstAsync(a => a.Id == retiro.ContributionAccountId, ct);
        ArgumentNullException.ThrowIfNull(alcance);
        var dentro = await alcance.AsegurarAsync(cuenta.BranchId, cuenta.CreditLineId, ErroresDeRetiro.Inexistente(), auditoria, Expediente, publicId, ct);
        return dentro.IsFailure ? Result.Failure<MembershipWithdrawal>(dentro.Error) : Result.Success(retiro);
    }

    public async Task<MembershipWithdrawalDto> DtoAsync(MembershipWithdrawal r, bool conLineas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var personas = await LectorDeCuentas.PersonasAsync(db, [r.PersonId], ct);
        var cuenta = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => a.Id == r.ContributionAccountId)
            .Select(a => new { a.PublicId, a.Number }).FirstAsync(ct);

        MembershipWithdrawalApprovalDto? aprobacion = null;
        if (r.ApprovalSessionItemId is { } punto)
        {
            var p = await (from i in db.SessionItems.AsNoTracking()
                           join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                           where i.Id == punto
                           select new { i.PublicId, s.Number }).FirstOrDefaultAsync(ct);
            if (p is not null) aprobacion = new MembershipWithdrawalApprovalDto(p.PublicId, p.Number, r.DecidedAt ?? r.CreatedAt, r.EffectiveDate, r.DecisionReason);
        }

        MembershipWithdrawalSettlementDto? liquidacion = null;
        if (r.SettlementVersion > 0 && r.SettledAt is { } liquidado)
        {
            var documento = r.SettlementIssuedDocumentId is { } d
                ? await db.IssuedLendingDocuments.AsNoTracking().Where(x => x.Id == d).Select(x => new { x.PublicId, x.Number }).FirstOrDefaultAsync(ct)
                : null;
            liquidacion = new MembershipWithdrawalSettlementDto(r.SettlementVersion, liquidado, r.ContributionsTotal ?? 0m, r.SavingsTotal ?? 0m,
                r.LoansOffsetTotal ?? 0m, r.LossDeductionAmount ?? 0m, r.RefundAmount ?? 0m, r.RefundDueOn, r.RefundHeldByInstitutionalMinimum,
                documento?.PublicId, documento?.Number);
        }

        var ordenes = await (from o in db.AccountPayoutOrders.AsNoTracking()
                             join a in db.MemberAccounts.AsNoTracking().IgnoreQueryFilters() on o.MemberAccountId equals a.Id
                             where o.MembershipWithdrawalId == r.Id && !o.IsDeleted
                             orderby o.Id
                             select new MembershipWithdrawalRefundOrderDto(o.PublicId, o.Number, a.Number, o.Amount, o.Status, o.ResultDueOn)).ToListAsync(ct);

        var lineas = conLineas ? await LineasAsync(r, ct) : [];
        var soportes = JsonSerializer.Deserialize<List<Guid>>(r.SupportAttachmentPublicIdsJson, Json) ?? [];
        return new MembershipWithdrawalDto(r.PublicId, r.Number, personas[r.PersonId], cuenta.PublicId, cuenta.Number, r.Cause, r.CauseDetail, r.FiledOn,
            r.Status, soportes, aprobacion, r.DesistedAt, r.DesistReason, liquidacion, r.ToRedoReason, r.PaidAt, ordenes, lineas);
    }

    /// <summary>Las líneas de la versión vigente de la liquidación, con sus cuentas, créditos y operaciones.</summary>
    private async Task<IReadOnlyList<MembershipWithdrawalLineDto>> LineasAsync(MembershipWithdrawal r, CancellationToken ct)
    {
        if (r.SettlementVersion == 0) return [];
        var filas = await db.MembershipWithdrawalLines.AsNoTracking()
            .Where(l => l.MembershipWithdrawalId == r.Id && l.SettlementVersion == r.SettlementVersion)
            .OrderBy(l => l.LineNumber).ToListAsync(ct);
        var cuentas = filas.Where(f => f.MemberAccountId is not null).Select(f => f.MemberAccountId!.Value).Distinct().ToList();
        var creditos = filas.Where(f => f.LoanId is not null).Select(f => f.LoanId!.Value).Distinct().ToList();
        var opsDeCuenta = filas.Where(f => f.AccountOperationId is not null).Select(f => f.AccountOperationId!.Value).Distinct().ToList();
        var opsDeCredito = filas.Where(f => f.LoanOperationId is not null).Select(f => f.LoanOperationId!.Value).Distinct().ToList();
        var deCuentas = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => cuentas.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => (a.PublicId, a.Number), ct);
        var deCreditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => creditos.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => (l.PublicId, l.Number), ct);
        var numerosDeCuenta = await db.MemberAccountOperations.AsNoTracking().Where(o => opsDeCuenta.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
        var numerosDeCredito = await db.LoanOperations.AsNoTracking().Where(o => opsDeCredito.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
        return filas.Select(f => new MembershipWithdrawalLineDto(f.SettlementVersion, f.LineNumber, f.Kind,
            f.MemberAccountId is { } a ? deCuentas[a].PublicId : null, f.MemberAccountId is { } a2 ? deCuentas[a2].Number : null,
            f.LoanId is { } l ? deCreditos[l].PublicId : null, f.LoanId is { } l2 ? deCreditos[l2].Number : null,
            f.Concept, f.Amount, f.AccountOperationId is { } o ? numerosDeCuenta.GetValueOrDefault(o) : null,
            f.LoanOperationId is { } lo ? numerosDeCredito.GetValueOrDefault(lo) : null, f.Notes)).ToList();
    }

    /// <summary>La cuenta de aportes viva de la persona (activa o inactiva), o nula.</summary>
    public async Task<MemberAccount?> CuentaDeAportesAsync(int personaId, CancellationToken ct) =>
        await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == personaId && a.Nature == LineNature.Contribution && !a.IsDeleted
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
            .OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
}
