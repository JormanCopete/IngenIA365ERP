using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Checkbooks;

// Feature 015, T1 (fase 33, T0411; FR-220; contracts/contabilidad.md §4; US19-3): el cierre del mes de los cheques girados de una cuenta propia. Lo corre una persona, una
// vez por cuenta y mes, después de terminado el mes: los emitidos y no entregados al corte se reclasifican al efectivo de la cuenta (con su reverso el primer día del mes
// siguiente) y los entregados sin cobrar más allá de Cheques.DiasACuentaPorPagar pasan a cuenta por pagar al beneficiario, nunca a ingreso. Ninguna tarea lo corre.

/// <summary>Un cheque que trató el cierre del mes. Tratamiento: <c>Reclassified</c> o <c>MovedToPayable</c>. (nuevo)</summary>
public sealed record MonthEndCheckLineDto(Guid IssuedCheckPublicId, long CheckNumber, decimal Amount, string Treatment, DateOnly IssuedOn, DateOnly? DeliveredOn);

/// <summary>Un cierre del mes de los cheques de una cuenta. (nuevo)</summary>
public sealed record MonthEndCheckRunDto(
    Guid PublicId,
    string Number,
    Guid BankAccountPublicId,
    string BankAccountCode,
    int Year,
    int Month,
    DateOnly CutoffDate,
    int DaysToPayable,
    int ReclassifiedCount,
    decimal ReclassifiedAmount,
    int MovedToPayableCount,
    decimal MovedToPayableAmount,
    DateTime RunAt,
    string RunBy,
    IReadOnlyList<MonthEndCheckLineDto> Lines);

/// <summary>Lo común del cierre del mes de los cheques: qué cheques trata y el DTO. (nuevo)</summary>
public static class CierreDelMesDeCheques
{
    public const string Codigo = "CM";
    public const string Reclasificado = "Reclassified";
    public const string ACuentaPorPagar = "MovedToPayable";

    /// <summary>Emitido y todavía no entregado al corte: el cheque de una orden emitido hasta el corte y sin entrega hasta esa fecha.</summary>
    public static bool PorEntregarAlCorte(IssuedCheck c, DateOnly corte) =>
        c.PaymentOrderId is not null && c.IssuedOn <= corte && c.Amount > 0m
        && (c.Status == IssuedCheckState.Issued
            || (c.Status is IssuedCheckState.Delivered or IssuedCheckState.Cashed or IssuedCheckState.NotCashed && c.DeliveredOn is { } d && d > corte));

    /// <summary>Entregado y sin cobrar más allá del plazo al corte, y no pasado antes a cuenta por pagar.</summary>
    public static bool ParaCuentaPorPagar(IssuedCheck c, DateOnly corte, int dias) =>
        c.Status == IssuedCheckState.Delivered && c.MovedToPayableRunId is null && c.DeliveredOn is { } d && d <= corte && corte.DayNumber - d.DayNumber > dias;

    public static async Task<MonthEndCheckRunDto> DtoAsync(IApplicationDbContext db, MonthEndCheckRun r, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == r.BankAccountId).Select(c => new { c.PublicId, c.Code }).FirstAsync(ct);
        var quien = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == r.RunByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct) ?? string.Empty;
        var lineas = JsonSerializer.Deserialize<List<MonthEndCheckLineDto>>(r.DetailJson) ?? [];
        return new MonthEndCheckRunDto(r.PublicId, r.Number, cuenta.PublicId, cuenta.Code, r.PeriodYear, r.PeriodMonth, r.CutoffDate, r.DaysToPayable, r.ReclassifiedCount,
            r.ReclassifiedAmount, r.MovedToPayableCount, r.MovedToPayableAmount, r.RunAt, quien, lineas);
    }
}

/// <summary>
/// <c>POST /api/treasury/month-end/checks</c> (<c>Treasury.Checkbooks.Manage</c>, con clave): corre el cierre del mes de los cheques de una cuenta propia. El mes tiene que haber
/// terminado (<c>Treasury.MonthEndChecks.MonthNotEnded</c>) y corre una sola vez por cuenta y mes (<c>Treasury.MonthEndChecks.AlreadyRun</c>). Emite
/// <c>ChequesReclasificadosAlCierre</c> y su reverso y un <c>ChequeNoCobradoACuentaPorPagar</c> por cheque; sin cheques que tratar, deja la corrida con cero. (nuevo)
/// </summary>
public sealed record RunMonthEndChecksCommand(Guid BankAccountPublicId, int Year, int Month) : IRequest<Result<MonthEndCheckRunDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RunMonthEndChecksCommandValidator : AbstractValidator<RunMonthEndChecksCommand>
{
    public RunMonthEndChecksCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class RunMonthEndChecksCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ParametrosDeTesoreria parametros,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RunMonthEndChecksCommand, Result<MonthEndCheckRunDto>>
{
    public Task<Result<MonthEndCheckRunDto>> Handle(RunMonthEndChecksCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var r = await CorrerAsync(request, ct);
            if (r.IsFailure) db.DescartarCambios();
            return r;
        }, ct);

    private async Task<Result<MonthEndCheckRunDto>> CorrerAsync(RunMonthEndChecksCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<MonthEndCheckRunDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await BankAccounts.CuentasPropias.VisibleAsync(db, alcance, request.BankAccountPublicId, seguir: false, ct);
        if (cuenta is null) return Result.Failure<MonthEndCheckRunDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        var corte = new DateOnly(request.Year, request.Month, 1).AddMonths(1).AddDays(-1);
        if (reloj.HoyLocal <= corte) return Result.Failure<MonthEndCheckRunDto>(ErroresDeTesoreria.CierreDeChequesMesAbierto(request.Year, request.Month));
        if (await db.MonthEndCheckRuns.AsNoTracking().AnyAsync(r => r.BankAccountId == cuenta.Id && r.PeriodYear == request.Year && r.PeriodMonth == request.Month, ct))
            return Result.Failure<MonthEndCheckRunDto>(ErroresDeTesoreria.CierreDeChequesRepetido(cuenta.Code, request.Year, request.Month));

        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ChequesDiasACuentaPorPagar, corte, ct);
        var chequeras = await db.Checkbooks.AsNoTracking().Where(k => k.BankAccountId == cuenta.Id).Select(k => k.Id).ToListAsync(ct);
        var candidatos = await db.IssuedChecks.AsNoTracking().Where(c => chequeras.Contains(c.CheckbookId) && c.IssuedOn <= corte).ToListAsync(ct);
        var porEntregar = candidatos.Where(c => CierreDelMesDeCheques.PorEntregarAlCorte(c, corte)).OrderBy(c => c.CheckNumber).ToList();
        var aPagar = dias is { } d ? candidatos.Where(c => CierreDelMesDeCheques.ParaCuentaPorPagar(c, corte, d)).OrderBy(c => c.CheckNumber).ToList() : [];

        // Validación previa contable (FR-221) de lo que se va a emitir.
        var tipos = new List<string>();
        if (porEntregar.Count > 0) tipos.Add(ChequesReclasificadosAlCierreV1.Type);
        if (aPagar.Count > 0) tipos.Add(ChequeNoCobradoACuentaPorPagarV1.Type);
        var previa = await emision.ValidacionPreviaAsync(tipos, cuenta, corte, ct);
        if (previa.IsFailure) return Result.Failure<MonthEndCheckRunDto>(previa.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], ct: ct);
        if (chequeras.Count > 0) await cerrojo.BloquearAsync(NivelDeTesoreria.Chequera, chequeras.Select(i => (long)i).Order().ToList(), ct: ct);

        var lineas = porEntregar.Select(c => new MonthEndCheckLineDto(c.PublicId, c.CheckNumber, c.Amount, CierreDelMesDeCheques.Reclasificado, c.IssuedOn, c.DeliveredOn))
            .Concat(aPagar.Select(c => new MonthEndCheckLineDto(c.PublicId, c.CheckNumber, c.Amount, CierreDelMesDeCheques.ACuentaPorPagar, c.IssuedOn, c.DeliveredOn)))
            .ToList();
        var corrida = new MonthEndCheckRun
        {
            Number = await numerador.AsignarAsync(CierreDelMesDeCheques.Codigo, CierreDelMesDeCheques.Codigo, ct), BankAccountId = cuenta.Id, PeriodYear = request.Year,
            PeriodMonth = request.Month, CutoffDate = corte, DaysToPayable = dias ?? 0, ReclassifiedCount = porEntregar.Count,
            ReclassifiedAmount = porEntregar.Sum(c => c.Amount), MovedToPayableCount = aPagar.Count, MovedToPayableAmount = aPagar.Sum(c => c.Amount),
            DetailJson = JsonSerializer.Serialize(lineas), RunByUserId = usuario, RunAt = reloj.UtcNow,
        };
        db.MonthEndCheckRuns.Add(corrida);
        await db.SaveChangesAsync(ct);

        if (porEntregar.Count > 0)
        {
            await emision.ChequesReclasificadosAsync(cuenta, corrida, porEntregar, reverso: false, ct);
            await emision.ChequesReclasificadosAsync(cuenta, corrida, porEntregar, reverso: true, ct);
        }
        foreach (var leido in aPagar)
        {
            var cheque = await db.IssuedChecks.FirstAsync(c => c.Id == leido.Id, ct);
            cheque.MovedToPayableRunId = corrida.Id;
            cheque.MovedToPayableOn = corte;
            await emision.ChequeNoCobradoACuentaPorPagarAsync(cuenta, corrida, cheque, ct);
        }
        await auditoria.AgregarAsync("Treasury.MonthEndChecks.Run", "MonthEndCheckRun", corrida.PublicId, null,
            new
            {
                number = corrida.Number, account = cuenta.Code, period = string.Create(CultureInfo.InvariantCulture, $"{request.Year}-{request.Month:00}"),
                reclassified = corrida.ReclassifiedCount, toPayable = corrida.MovedToPayableCount, days = corrida.DaysToPayable, by = actor.Name,
            }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await CierreDelMesDeCheques.DtoAsync(db, corrida, ct));
    }
}

/// <summary><c>GET /api/treasury/month-end/checks?account=&amp;year=</c> (<c>Treasury.Checkbooks.View</c>): los cierres del mes de los cheques de las cuentas al alcance. (nuevo)</summary>
public sealed record ListMonthEndCheckRunsQuery(Guid? BankAccountPublicId = null, int? Year = null) : IRequest<Result<IReadOnlyList<MonthEndCheckRunDto>>>;

public sealed class ListMonthEndCheckRunsQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListMonthEndCheckRunsQuery, Result<IReadOnlyList<MonthEndCheckRunDto>>>
{
    public async Task<Result<IReadOnlyList<MonthEndCheckRunDto>>> Handle(ListMonthEndCheckRunsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.MonthEndCheckRuns.AsNoTracking();
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(r => r.BankAccountId == id);
        }
        if (request.Year is { } y) consulta = consulta.Where(r => r.PeriodYear == y);
        var filas = (await consulta.OrderByDescending(r => r.Id).Take(200).ToListAsync(ct)).Where(r => mio.IncluyeCuenta(r.BankAccountId)).ToList();
        var lista = new List<MonthEndCheckRunDto>(filas.Count);
        foreach (var r in filas) lista.Add(await CierreDelMesDeCheques.DtoAsync(db, r, ct));
        return Result.Success<IReadOnlyList<MonthEndCheckRunDto>>(lista);
    }
}
