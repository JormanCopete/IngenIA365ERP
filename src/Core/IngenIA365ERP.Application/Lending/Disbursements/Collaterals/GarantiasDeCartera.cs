using System.Globalization;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Collaterals;

// Feature 013, US3 (bloque 7B; T0450; FR-026, FR-043, FR-082; api.md §6; data-model §2.8, §2.9): lo que comparten los comandos y las consultas de
// garantías y del pagaré: el dueño (solicitud o crédito) con su alcance, los errores y los DTO. (nuevo)

/// <summary>Las dos clases de dueño de una garantía o de un pagaré (api.md §6: <c>ownerKind</c>). (nuevo)</summary>
public static class DuenosDeGarantia
{
    public const string Solicitud = "Application";
    public const string Credito = "Loan";

    public static bool EsValido(string? clase) => clase is Solicitud or Credito;
}

/// <summary>
/// El dueño de una garantía o de un pagaré, leído y ya dentro del alcance (oficina y línea): la solicitud o el crédito, con lo que las reglas y la
/// emisión necesitan. <see cref="Decidido"/> es la solicitud ya decidida (o terminada) o cualquier crédito. (nuevo)
/// </summary>
public sealed record DuenoDeGarantia(
    string Clase,
    Guid PublicId,
    string? Numero,
    int? ApplicationId,
    int? LoanId,
    int BranchId,
    int CreditLineId,
    int? CreditLineVersionId,
    int DebtorPersonId,
    bool Decidido,
    decimal Monto,
    int? PlazoEnMeses,
    CreditApplicationStatus? EstadoDeLaSolicitud,
    LoanStatus? EstadoDelCredito);

/// <summary>Los errores de garantías (data-model §2.8 y §10.1; api.md §6.1). (nuevo)</summary>
public static class ErroresDeGarantias
{
    public const string CodigoInexistente = "Lending.Collateral.NotFound";
    public const string CodigoClaseNoAdmitida = "Lending.Collateral.KindNotAdmitted";
    public const string CodigoDuenoDecidido = "Lending.Collateral.OwnerLocked";
    public const string CodigoCreditoNoCancelado = "Lending.Collateral.LoanNotPaidOff";
    public const string CodigoSinFirmeza = "Lending.Collateral.NotFinal";

    public static Error Inexistente() => new(CodigoInexistente, "La garantía no existe.");

    public static Error ClaseNoAdmitida(CollateralKind clase, IReadOnlyCollection<CollateralKind> admitidas) => new ErrorConDatos(CodigoClaseNoAdmitida,
        admitidas.Count == 0
            ? $"La línea no admite garantías de clase «{clase}» para este monto y plazo (la regla que casa no admite garantías reales)."
            : $"La línea no admite garantías de clase «{clase}» para este monto y plazo. Admite: {string.Join(", ", admitidas)}.",
        new { kind = clase.ToString(), admitted = admitidas.Select(k => k.ToString()).ToList() });

    public static Error DuenoDecidido(string clase) => new ErrorConDatos(CodigoDuenoDecidido,
        clase == DuenosDeGarantia.Credito
            ? "El crédito ya está decidido y desembolsado: sus garantías no se cambian aquí (una garantía nueva sobre un crédito es una novedad)."
            : "La solicitud ya está decidida: sus garantías se registran y se editan antes de decidirla.",
        new { ownerKind = clase });

    public static Error CreditoNoCancelado(string? estado) => new ErrorConDatos(CodigoCreditoNoCancelado,
        "Sólo se libera cuando el crédito que respalda está cancelado.", new { loanStatus = estado });

    public static Error SinFirmeza(DateOnly hasta, Guid pago) => new ErrorConDatos(CodigoSinFirmeza,
        $"El pago que canceló el crédito queda en firme el {hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}: libere después de esa fecha.",
        new { finalityUntil = hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), paymentOperationPublicId = pago });
}

/// <summary>Lee el dueño, la garantía y el pagaré dentro del alcance, arma sus DTO y verifica la firmeza de la cancelación (FR-082). (nuevo)</summary>
public static class GarantiasDeCartera
{
    /// <summary>Las clases que guarda <c>LND_Collaterals</c> (<c>CK_LND_Collaterals_Kind</c>): el pagaré y el codeudor van por su lado.</summary>
    public static readonly IReadOnlySet<CollateralKind> Reales = new HashSet<CollateralKind>
    {
        CollateralKind.Pledge, CollateralKind.Mortgage, CollateralKind.GuaranteeFund, CollateralKind.Contributions,
    };

    /// <summary>El dueño por su clase y <c>PublicId</c>; fuera del alcance o inexistente, el 404 de la solicitud o del crédito.</summary>
    public static async Task<Result<DuenoDeGarantia>> DuenoAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        string clase, Guid publicId, CancellationToken ct)
    {
        if (clase == DuenosDeGarantia.Credito)
        {
            var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == publicId && !l.IsDeleted, ct);
            if (credito is null) return Result.Failure<DuenoDeGarantia>(ErroresDeDesembolso.CreditoInexistente());
            var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeDesembolso.CreditoInexistente(), auditoria,
                "Loan", credito.PublicId, ct);
            if (dentro.IsFailure) return Result.Failure<DuenoDeGarantia>(dentro.Error);
            return Result.Success(DeCredito(credito));
        }

        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, publicId, seguir: false, ct);
        return leida.IsFailure ? Result.Failure<DuenoDeGarantia>(leida.Error) : Result.Success(DeSolicitud(leida.Value));
    }

    /// <summary>El dueño de una garantía o un pagaré ya registrados: el crédito si lo tiene, si no la solicitud.</summary>
    public static async Task<Result<DuenoDeGarantia>> DuenoDeAsync(IApplicationDbContext db, int? loanId, int? applicationId, CancellationToken ct)
    {
        if (loanId is { } l && await db.Loans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == l, ct) is { } credito)
            return Result.Success(DeCredito(credito));
        if (applicationId is { } a && await db.CreditApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a, ct) is { } solicitud)
            return Result.Success(DeSolicitud(solicitud));
        return Result.Failure<DuenoDeGarantia>(ErroresDeGarantias.Inexistente());
    }

    private static DuenoDeGarantia DeCredito(Domain.Entities.Lending.Loans.Loan c) =>
        new(DuenosDeGarantia.Credito, c.PublicId, c.Number, c.ApplicationId, c.Id, c.BranchId, c.CreditLineId, null, c.DebtorPersonId, true, c.ApprovedAmount,
            null, null, c.Status);

    private static DuenoDeGarantia DeSolicitud(CreditApplication s) =>
        new(DuenosDeGarantia.Solicitud, s.PublicId, s.Number, s.Id, s.LoanId, s.BranchId, s.CreditLineId, s.CreditLineVersionId, s.ApplicantPersonId,
            AdjuntosDeModulo.SolicitudDecidida(s.Status), s.ApprovedAmount ?? s.RequestedAmount, s.TermMonths, s.Status, null);

    /// <summary>La garantía viva por su <c>PublicId</c>, con su dueño en el alcance; fuera, el 404 de la garantía.</summary>
    public static async Task<Result<(Collateral Garantia, DuenoDeGarantia Dueno)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance,
        LendingAuditEmitter auditoria, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.Collaterals : db.Collaterals.AsNoTracking();
        var g = await consulta.FirstOrDefaultAsync(c => c.PublicId == publicId && !c.IsDeleted, ct);
        if (g is null) return Result.Failure<(Collateral, DuenoDeGarantia)>(ErroresDeGarantias.Inexistente());
        var dueno = await DuenoDeAsync(db, g.LoanId, g.ApplicationId, ct);
        if (dueno.IsFailure) return Result.Failure<(Collateral, DuenoDeGarantia)>(dueno.Error);
        var dentro = await alcance.AsegurarAsync(dueno.Value.BranchId, dueno.Value.CreditLineId, ErroresDeGarantias.Inexistente(), auditoria,
            nameof(Collateral), g.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<(Collateral, DuenoDeGarantia)>(dentro.Error) : Result.Success((g, dueno.Value));
    }

    /// <summary>
    /// Nulo si la cancelación del crédito ya está en firme (FR-082): el crédito cancelado y el último pago sin reversar con su
    /// <c>FinalityDate</c> cumplida a <paramref name="hoy"/>; si no, <c>Lending.Collateral.LoanNotPaidOff</c> o <c>.NotFinal</c>. Devuelve también el
    /// pago que canceló (nulo si el crédito cancelado no tiene pagos registrados, p. ej. migrado ya cancelado).
    /// </summary>
    public static async Task<(Error? Error, long? Pago)> FirmezaDeLaCancelacionAsync(IApplicationDbContext db, int? loanId, DateOnly hoy, CancellationToken ct)
    {
        var credito = loanId is { } id ? await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct) : null;
        if (credito is null || credito.Status != LoanStatus.PaidOff) return (ErroresDeGarantias.CreditoNoCancelado(credito?.Status.ToString()), null);
        var pago = await db.LoanOperations.AsNoTracking()
            .Where(o => o.LoanId == credito.Id && o.Kind == LoanOperationKind.Payment && o.ReversedByOperationId == null)
            .OrderByDescending(o => o.Sequence).ThenByDescending(o => o.Id)
            .Select(o => new { o.Id, o.PublicId, o.FinalityDate, o.ValueDate })
            .FirstOrDefaultAsync(ct);
        if (pago is null) return (null, null);
        var firme = pago.FinalityDate ?? pago.ValueDate;
        return firme > hoy ? (ErroresDeGarantias.SinFirmeza(firme, pago.PublicId), pago.Id) : (null, pago.Id);
    }

    /// <summary>
    /// La emisión de <c>GarantiaConstituida</c> de un movimiento (A3; mensajes.md §10.24): sobre con el deudor del dueño, la sucursal contable de su
    /// oficina, el código de la línea, el crédito si existe y la fecha contable de hoy. Nada que emitir (sin A3) no es error. Sin guardar.
    /// </summary>
    public static async Task<Result> EmitirAsync(IApplicationDbContext db, EmisionDeCartera emision, FechaContable fechaContable, DateOnly hoy, DateTime ahora,
        Collateral garantia, DuenoDeGarantia dueno, string movimiento, decimal variacion, decimal valorDespues, CancellationToken ct)
    {
        if (variacion == 0m) return Result.Success();
        var sucursal = await emision.SucursalContableAsync(dueno.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure(sucursal.Error);
        var fecha = await fechaContable.CalcularAsync(hoy, ct);
        if (fecha.IsFailure) return Result.Failure(fecha.Error);
        var deudor = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == dueno.DebtorPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == dueno.CreditLineId).Select(l => l.Code).FirstOrDefaultAsync(ct);
        Guid? credito = null;
        string? numero = null;
        if (dueno.LoanId is { } loanId)
        {
            var c = await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => new { l.PublicId, l.Number }).FirstOrDefaultAsync(ct);
            credito = c?.PublicId;
            numero = c?.Number;
        }
        await emision.GarantiaConstituidaAsync(new GarantiaAEmitir(garantia, movimiento, variacion, valorDespues, deudor, sucursal.Value, linea ?? string.Empty,
            credito, numero, fecha.Value, ahora), ct);
        return Result.Success();
    }

    /// <summary>Los DTO de garantías: dueño, constituyente y documentos (<c>LendingGuaranteeDocument</c>).</summary>
    public static async Task<IReadOnlyList<CollateralDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<Collateral> filas, CancellationToken ct)
    {
        var personas = filas.Select(f => f.OwnerPersonId).Distinct().ToList();
        var publicas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var solicitudes = filas.Where(f => f.ApplicationId is not null).Select(f => f.ApplicationId!.Value).Distinct().ToList();
        var deSolicitud = await db.CreditApplications.AsNoTracking().Where(a => solicitudes.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => (a.PublicId, a.Number), ct);
        var creditos = filas.Where(f => f.LoanId is not null).Select(f => f.LoanId!.Value).Distinct().ToList();
        var deCredito = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => (l.PublicId, (string?)l.Number), ct);
        var ids = filas.Select(f => f.PublicId).ToList();
        var documentos = (await db.Attachments.AsNoTracking()
                .Where(a => a.OwnerEntityType == AdjuntosDeModulo.DocumentoDeGarantia && ids.Contains(a.OwnerEntityPublicId))
                .Select(a => new { a.OwnerEntityPublicId, a.PublicId }).ToListAsync(ct))
            .ToLookup(a => a.OwnerEntityPublicId, a => a.PublicId);

        return filas.Select(f =>
        {
            var dueno = f.LoanId is { } l && deCredito.TryGetValue(l, out var c)
                ? new CollateralOwnerDto(DuenosDeGarantia.Credito, c.PublicId, c.Item2)
                : f.ApplicationId is { } a && deSolicitud.TryGetValue(a, out var s)
                    ? new CollateralOwnerDto(DuenosDeGarantia.Solicitud, s.PublicId, s.Number)
                    : new CollateralOwnerDto(DuenosDeGarantia.Solicitud, Guid.Empty, null);
            return new CollateralDto(f.PublicId, f.Kind.ToString(), f.Status.ToString(), dueno, publicas.GetValueOrDefault(f.OwnerPersonId), f.Description,
                f.Value, f.AdmissibleValue, f.ConstitutedOn, f.ValidUntil, f.PolicyNumber, f.PolicyExpiresOn, f.FundCoverage)
            {
                ValuationDate = f.ValuationDate,
                RegistrationReference = f.RegistrationReference,
                MortgageRank = f.MortgageRank,
                FundFee = f.FundFee,
                ReleasedOn = f.ReleasedOn,
                ExtinguishedOn = f.ExtinguishedOn,
                ExtinguishReason = f.ExtinguishReason,
                ExecutedOn = f.ExecutedOn,
                Documents = documentos[f.PublicId].ToList(),
            };
        }).ToList();
    }
}
