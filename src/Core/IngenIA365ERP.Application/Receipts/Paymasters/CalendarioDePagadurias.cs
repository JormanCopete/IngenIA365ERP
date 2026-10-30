using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Pagadurias;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Paymasters;

// Feature 015, R1, US7 (T0383; FR-082; FR-010; Ley 1527 art. 6): el calendario legal de las pagadurías. Por pagaduría y período, lo enviado, lo descontado (respondido),
// lo girado y lo pendiente; la fecha de pago de la nómina que informa la pagaduría y la fecha legal de giro a Libranza.DiasHabilesGiro días hábiles. Es informativo: nada
// se bloquea por el plazo; la tarea de antigüedad avisa el giro tardío y el informe de morosas soporta el cobro solidario.

/// <summary>Una fila del calendario: una pagaduría en un período. (nuevo)</summary>
public sealed record PaymasterCalendarRowDto(
    Guid PaymasterPersonPublicId,
    string PaymasterName,
    int Year,
    int Month,
    int SubPeriod,
    decimal Sent,
    decimal Deducted,
    decimal Remitted,
    decimal Pending,
    DateOnly? PayrollPayDate,
    DateOnly? LegalRemittanceDate,
    bool IsLate);

/// <summary>Lo común del calendario de pagadurías. (nuevo)</summary>
public static class CalendarioDePagadurias
{
    /// <summary>Las filas del calendario (sin los archivos reemplazados), con el alcance de pagadurías si se da.</summary>
    public static async Task<IReadOnlyList<PaymasterCalendarRowDto>> FilasAsync(IApplicationDbContext db, AlcanceDeRecaudo? alcance, int? pagaduria, int? anio, DateOnly hoy,
        CancellationToken ct)
    {
        var archivos = db.ReceiptsPayrollDeductionFiles.AsNoTracking().Where(f => f.Status != PayrollDeductionFileState.Superseded);
        if (pagaduria is { } p) archivos = archivos.Where(f => f.PaymasterPersonId == p);
        if (anio is { } a) archivos = archivos.Where(f => f.PeriodYear == a);
        var filas = await archivos.Select(f => new { f.Id, f.PaymasterPersonId, f.PeriodYear, f.PeriodMonth, SubPeriod = f.SubPeriod ?? 0, f.Amount }).ToListAsync(ct);
        if (alcance is not null) filas = filas.Where(f => alcance.IncluyePagaduria(f.PaymasterPersonId)).ToList();
        var ids = filas.Select(f => f.Id).ToList();
        var lineas = await db.ReceiptsPayrollDeductionFileLines.AsNoTracking().Where(l => ids.Contains(l.PayrollDeductionFileId))
            .GroupBy(l => l.PayrollDeductionFileId)
            .Select(g => new { Archivo = g.Key, Descontado = g.Sum(l => l.DeductedAmount ?? 0m), Girado = g.Sum(l => l.RemittedAmount) }).ToListAsync(ct);
        var porArchivo = lineas.ToDictionary(l => l.Archivo);
        var pagadurias = filas.Select(f => f.PaymasterPersonId).Distinct().ToList();
        var entradas = await db.PaymasterCalendar.AsNoTracking().Where(e => pagadurias.Contains(e.PaymasterPersonId)).ToListAsync(ct);
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => pagadurias.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return filas.GroupBy(f => (f.PaymasterPersonId, f.PeriodYear, f.PeriodMonth, f.SubPeriod))
            .Select(g =>
            {
                var entrada = entradas.FirstOrDefault(e => e.PaymasterPersonId == g.Key.PaymasterPersonId && e.PeriodYear == g.Key.PeriodYear
                                                           && e.PeriodMonth == g.Key.PeriodMonth && e.SubPeriod == g.Key.SubPeriod);
                var estado = CalendarioLegalDeGiro.Estado(g.Sum(f => f.Amount), g.Sum(f => porArchivo.GetValueOrDefault(f.Id)?.Descontado ?? 0m),
                    g.Sum(f => porArchivo.GetValueOrDefault(f.Id)?.Girado ?? 0m), entrada?.LegalRemittanceDate, hoy);
                var persona = personas[g.Key.PaymasterPersonId];
                return new PaymasterCalendarRowDto(persona.PublicId, OperacionesDeCaja.Nombre(persona), g.Key.PeriodYear, g.Key.PeriodMonth, g.Key.SubPeriod, estado.Enviado,
                    estado.Descontado, estado.Girado, estado.Pendiente, entrada?.PayrollPayDate, estado.FechaLegal, estado.Tardio);
            })
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ThenBy(r => r.PaymasterName).ToList();
    }
}

/// <summary>
/// <c>GET /api/receipts/paymaster-calendar?paymaster=&amp;year=&amp;onlyLate=</c> (<c>Receipts.Paymasters.View</c>) (nuevo): el calendario por pagaduría y período; con
/// <c>onlyLate</c>, el informe de pagadurías morosas (lo pendiente pasada la fecha legal).
/// </summary>
public sealed record GetPaymasterCalendarQuery(Guid? PaymasterPersonPublicId = null, int? Year = null, bool OnlyLate = false)
    : IRequest<Result<IReadOnlyList<PaymasterCalendarRowDto>>>;

public sealed class GetPaymasterCalendarQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<GetPaymasterCalendarQuery, Result<IReadOnlyList<PaymasterCalendarRowDto>>>
{
    public async Task<Result<IReadOnlyList<PaymasterCalendarRowDto>>> Handle(GetPaymasterCalendarQuery request, CancellationToken ct)
    {
        int? pagaduria = null;
        if (request.PaymasterPersonPublicId is { } p)
            pagaduria = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
        var filas = await CalendarioDePagadurias.FilasAsync(db, await alcance.ObtenerAsync(ct), pagaduria, request.Year, reloj.HoyLocal, ct);
        return Result.Success<IReadOnlyList<PaymasterCalendarRowDto>>(request.OnlyLate ? filas.Where(f => f.IsLate).ToList() : filas);
    }
}

/// <summary>
/// <c>POST /api/receipts/paymaster-calendar</c> (<c>Receipts.Paymasters.RegisterRemittance</c>) (nuevo): registra (o corrige) la fecha de pago de la nómina que informa la
/// pagaduría para un período y calcula la fecha legal de giro con el calendario hábil y <c>Libranza.DiasHabilesGiro</c> vigente, que queda sellado en la fila.
/// </summary>
public sealed record RegisterPaymasterPayDateCommand(Guid PaymasterPersonPublicId, int Year, int Month, DateOnly PayrollPayDate, int SubPeriod = 0, string? Note = null)
    : IRequest<Result<PaymasterCalendarRowDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterPaymasterPayDateCommandValidator : AbstractValidator<RegisterPaymasterPayDateCommand>
{
    public RegisterPaymasterPayDateCommandValidator()
    {
        RuleFor(x => x.PaymasterPersonPublicId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.SubPeriod).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class RegisterPaymasterPayDateCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICalendarioHabil calendario,
    ParametrosDeCaja parametros,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterPaymasterPayDateCommand, Result<PaymasterCalendarRowDto>>
{
    public async Task<Result<PaymasterCalendarRowDto>> Handle(RegisterPaymasterPayDateCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymasterCalendarRowDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var pagaduria = await db.People.AsNoTracking().Where(x => x.PublicId == request.PaymasterPersonPublicId).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        var mio = await alcance.ObtenerAsync(ct);
        if (pagaduria is null || !mio.IncluyePagaduria(pagaduria.Value)) return Result.Failure<PaymasterCalendarRowDto>(ErroresDeRecaudo.NoEncontrado("La pagaduría"));
        var hoy = reloj.HoyLocal;
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.LibranzaDiasHabilesGiro, request.PayrollPayDate, ct) ?? 0;
        // El calendario hábil de la pagaduría: sábados y domingos y los festivos de la central, con margen para el plazo.
        var habil = await calendario.ObtenerAsync(request.PayrollPayDate, request.PayrollPayDate.AddDays(31 + dias * 2), DiasDeDescanso.SabadoYDomingo, ct: ct);
        var legal = CalendarioLegalDeGiro.FechaLegal(habil, request.PayrollPayDate, dias);
        var entrada = await db.PaymasterCalendar.FirstOrDefaultAsync(e => e.PaymasterPersonId == pagaduria && e.PeriodYear == request.Year && e.PeriodMonth == request.Month
                                                                           && e.SubPeriod == request.SubPeriod, ct);
        var antes = entrada is null ? null : new { entrada.PayrollPayDate, entrada.LegalRemittanceDate, entrada.BusinessDays };
        if (entrada is null)
        {
            entrada = new PaymasterCalendarEntry { PaymasterPersonId = pagaduria.Value, PeriodYear = request.Year, PeriodMonth = request.Month, SubPeriod = request.SubPeriod };
            db.PaymasterCalendar.Add(entrada);
        }
        (entrada.PayrollPayDate, entrada.BusinessDays, entrada.LegalRemittanceDate, entrada.RegisteredByUserId, entrada.RegisteredAt, entrada.Note) =
            (request.PayrollPayDate, dias, legal, usuario, reloj.UtcNow, string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim());
        await auditoria.AgregarAsync("Receipts.Paymaster.PayDateRegistered", "PaymasterCalendar", entrada.PublicId, antes,
            new { year = request.Year, month = request.Month, subPeriod = request.SubPeriod, payDate = request.PayrollPayDate, legal, businessDays = dias, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        var filas = await CalendarioDePagadurias.FilasAsync(db, mio, pagaduria, request.Year, hoy, ct);
        var fila = filas.FirstOrDefault(f => f.Month == request.Month && f.SubPeriod == request.SubPeriod);
        var persona = await db.People.AsNoTracking().FirstAsync(x => x.Id == pagaduria, ct);
        return Result.Success(fila ?? new PaymasterCalendarRowDto(persona.PublicId, OperacionesDeCaja.Nombre(persona), request.Year, request.Month, request.SubPeriod, 0m, 0m,
            0m, 0m, request.PayrollPayDate, legal, false));
    }
}
