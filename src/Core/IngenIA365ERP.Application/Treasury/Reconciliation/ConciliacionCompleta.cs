using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using IngenIA365ERP.Domain.Treasury.Conciliacion;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Reconciliation;

// Feature 015, T2 (fase 41, T0448, T0449; FR-100, FR-128 a FR-130, FR-218; US17): lo que la conciliación completa suma a la mínima: las reglas configurables de
// emparejamiento (el cruce automático las usa en su orden, con su holgura) y las notas bancarias —GMF real del extracto, comisión con IVA, intereses en bruto con su
// retención, impuestos—, que registra una persona y aprueba otra (NotasBancarias.ExigenAprobacion); al aprobarse nace NotaBancariaRegistrada y la nota queda como
// registro de los libros para cruzar con su línea del extracto. Las dispersiones de Nómina informadas entran también como registros.

/// <summary>Una regla de emparejamiento. (nuevo)</summary>
public sealed record ReconciliationRuleDto(Guid PublicId, string Code, string Name, ReconciliationRuleCriterion Criterion, int ToleranceDays, int Priority,
    Guid? BankAccountPublicId, bool IsActive);

/// <summary>Una nota bancaria. (nuevo)</summary>
public sealed record BankNoteDto(Guid PublicId, string Number, Guid BankAccountPublicId, string BankAccountCode, BankNoteKind Kind, DateOnly NoteDate,
    StatementLineDirection Direction, decimal Amount, decimal BaseAmount, decimal VatAmount, decimal WithholdingAmount, Guid? StatementLinePublicId,
    Guid? ThirdPartyPersonPublicId, string? Description, BankNoteState Status, DateTime RegisteredAt, DateTime? DecidedAt, string? RejectionReason);

/// <summary>Una línea del extracto que parece una nota bancaria (el GMF real, una comisión, unos intereses). (nuevo)</summary>
public sealed record BankNoteSuggestionDto(Guid StatementLinePublicId, DateOnly MovementDate, StatementLineDirection Direction, decimal Amount, string? Description,
    BankNoteKind SuggestedKind);

/// <summary>Lo común de la conciliación completa. (nuevo)</summary>
public static class ConciliacionCompletaDeTesoreria
{
    public const string ClaseDeNumeracion = "NB";

    /// <summary>Las palabras con que el extracto suele nombrar cada clase de nota (sin tildes, en mayúsculas).</summary>
    private static readonly (BankNoteKind Clase, string[] Palabras)[] Pistas =
    [
        (BankNoteKind.Gmf, ["GMF", "4X1000", "4 X 1000", "GRAVAMEN"]),
        (BankNoteKind.Commission, ["COMISION", "CUOTA DE MANEJO", "CUOTA MANEJO"]),
        (BankNoteKind.Interest, ["INTERES", "RENDIMIENTO"]),
        (BankNoteKind.Tax, ["IMPUESTO", "IVA", "RETENCION"]),
    ];

    /// <summary>La clase de nota que sugiere la descripción de una línea, o nula.</summary>
    public static BankNoteKind? Sugerir(string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion)) return null;
        var d = descripcion.ToUpperInvariant().Replace("Ó", "O").Replace("É", "E").Replace("Í", "I");
        foreach (var (clase, palabras) in Pistas)
            if (palabras.Any(p => d.Contains(p, StringComparison.Ordinal))) return clase;
        return null;
    }

    /// <summary>Las reglas activas que valen para una cuenta (las suyas y las de todas), como las ve el motor.</summary>
    public static async Task<IReadOnlyList<ReglaDeCruce>> ReglasAsync(IApplicationDbContext db, int cuenta, CancellationToken ct) =>
        (await db.TreasuryReconciliationRules.AsNoTracking().Where(r => r.IsActive && (r.BankAccountId == null || r.BankAccountId == cuenta)).ToListAsync(ct))
        .Select(r => new ReglaDeCruce(r.Code, r.Criterion, r.ToleranceDays, r.Priority)).ToList();

    public static async Task<BankNoteDto> DtoAsync(IApplicationDbContext db, BankNote n, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == n.BankAccountId).Select(c => new { c.PublicId, c.Code }).FirstAsync(ct);
        var tercero = n.ThirdPartyPersonId is { } t
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == t).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return new BankNoteDto(n.PublicId, n.Number, cuenta.PublicId, cuenta.Code, n.Kind, n.NoteDate, n.Direction, n.Amount, n.BaseAmount, n.VatAmount, n.WithholdingAmount,
            n.StatementLinePublicId, tercero, n.Description, n.Status, n.RegisteredAt, n.DecidedAt, n.RejectionReason);
    }
}

// --------------------------------------------------------------------------------------------------------------- reglas --

/// <summary>
/// <c>POST /api/treasury/reconciliation-rules</c> (crear) y <c>PUT /{id}</c> (<c>Treasury.Reconciliation.Work</c>, con clave; FR-129): una regla de emparejamiento con su
/// criterio, holgura y prioridad, para todas las cuentas o para una. Es configuración: no exige Tesorería activa. (nuevo)
/// </summary>
public sealed record SaveReconciliationRuleCommand(Guid? PublicId, string Code, string Name, ReconciliationRuleCriterion Criterion, int ToleranceDays, int Priority,
    Guid? BankAccountPublicId = null, bool IsActive = true) : IRequest<Result<ReconciliationRuleDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveReconciliationRuleCommandValidator : AbstractValidator<SaveReconciliationRuleCommand>
{
    public SaveReconciliationRuleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Criterion).IsInEnum();
        RuleFor(x => x.ToleranceDays).InclusiveBetween(0, 31).WithMessage("La holgura de una regla va de 0 a 31 días.");
        RuleFor(x => x.Priority).GreaterThan(0);
    }
}

public sealed class SaveReconciliationRuleCommandHandler(IApplicationDbContext db, AuditoriaDeTesoreria auditoria)
    : IRequestHandler<SaveReconciliationRuleCommand, Result<ReconciliationRuleDto>>
{
    public async Task<Result<ReconciliationRuleDto>> Handle(SaveReconciliationRuleCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        int? cuenta = null;
        if (request.BankAccountPublicId is { } c)
        {
            cuenta = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (cuenta is null) return Result.Failure<ReconciliationRuleDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        }
        if (await db.TreasuryReconciliationRules.AsNoTracking().FirstOrDefaultAsync(r => r.Code == codigo && r.PublicId != request.PublicId, ct) is { } otra)
            return Result.Failure<ReconciliationRuleDto>(CodigoDeCatalogo.Duplicado("una regla de conciliación", codigo, otra.Name, otra.PublicId));
        ReconciliationRule regla;
        if (request.PublicId is { } id)
        {
            regla = await db.TreasuryReconciliationRules.FirstOrDefaultAsync(r => r.PublicId == id, ct)
                    ?? throw new InvalidOperationException("unreachable");
        }
        else
        {
            regla = new ReconciliationRule();
            db.TreasuryReconciliationRules.Add(regla);
        }
        (regla.Code, regla.Name, regla.Criterion, regla.ToleranceDays, regla.Priority, regla.BankAccountId, regla.IsActive) =
            (codigo, request.Name.Trim(), request.Criterion, request.ToleranceDays, request.Priority, cuenta, request.IsActive);
        await auditoria.AgregarAsync("Treasury.ReconciliationRule.Saved", "ReconciliationRule", regla.PublicId, null,
            new { code = codigo, criterion = request.Criterion.ToString(), request.ToleranceDays, request.Priority, account = request.BankAccountPublicId, request.IsActive }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReconciliationRuleDto(regla.PublicId, regla.Code, regla.Name, regla.Criterion, regla.ToleranceDays, regla.Priority, request.BankAccountPublicId,
            regla.IsActive));
    }
}

/// <summary><c>GET /api/treasury/reconciliation-rules</c> (<c>Treasury.Reconciliation.View</c>). (nuevo)</summary>
public sealed record ListReconciliationRulesQuery : IRequest<Result<IReadOnlyList<ReconciliationRuleDto>>>;

public sealed class ListReconciliationRulesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListReconciliationRulesQuery, Result<IReadOnlyList<ReconciliationRuleDto>>>
{
    public async Task<Result<IReadOnlyList<ReconciliationRuleDto>>> Handle(ListReconciliationRulesQuery request, CancellationToken ct)
    {
        var filas = await (from r in db.TreasuryReconciliationRules.AsNoTracking()
                           join c in db.OwnBankAccounts.AsNoTracking() on r.BankAccountId equals c.Id into cs
                           from c in cs.DefaultIfEmpty()
                           orderby r.Priority, r.Code
                           select new ReconciliationRuleDto(r.PublicId, r.Code, r.Name, r.Criterion, r.ToleranceDays, r.Priority, c == null ? null : c.PublicId, r.IsActive))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<ReconciliationRuleDto>>(filas);
    }
}

// --------------------------------------------------------------------------------------------------------- notas bancarias --

/// <summary>
/// <c>POST /api/treasury/bank-notes</c> (<c>Treasury.BankNotes.Register</c>, con clave; FR-130, FR-218): registra una nota bancaria de una cuenta propia, casi siempre desde
/// su línea del extracto. La comisión es base + IVA; los intereses, bruto − retención; el banco de la cuenta es el tercero si no se da otro. Con
/// <c>NotasBancarias.ExigenAprobacion</c> queda por aprobar; si no, nace su hecho al registrarla. (nuevo)
/// </summary>
public sealed record RegisterBankNoteCommand(
    Guid BankAccountPublicId,
    BankNoteKind Kind,
    decimal Amount,
    DateOnly? NoteDate = null,
    decimal? BaseAmount = null,
    decimal VatAmount = 0m,
    decimal WithholdingAmount = 0m,
    Guid? StatementLinePublicId = null,
    Guid? ThirdPartyPersonPublicId = null,
    string? Description = null) : IRequest<Result<BankNoteDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterBankNoteCommandValidator : AbstractValidator<RegisterBankNoteCommand>
{
    public RegisterBankNoteCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage("La nota mueve un valor mayor que cero.");
        RuleFor(x => x.VatAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.WithholdingAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x).Must(x => x.Kind != BankNoteKind.Commission || (x.BaseAmount ?? x.Amount - x.VatAmount) + x.VatAmount == x.Amount)
            .WithMessage("En una comisión, la base más el IVA es lo que cobró el banco.");
        RuleFor(x => x).Must(x => x.Kind != BankNoteKind.Interest || (x.BaseAmount ?? x.Amount + x.WithholdingAmount) - x.WithholdingAmount == x.Amount)
            .WithMessage("En los intereses, el bruto menos la retención es lo que abonó el banco.");
        RuleFor(x => x.Description).MaximumLength(300);
    }
}

public sealed class RegisterBankNoteCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ParametrosDeTesoreria parametros,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterBankNoteCommand, Result<BankNoteDto>>
{
    public async Task<Result<BankNoteDto>> Handle(RegisterBankNoteCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BankNoteDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.BankAccountPublicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id)) return Result.Failure<BankNoteDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        var hoy = reloj.HoyLocal;
        var sentido = request.Kind == BankNoteKind.Interest ? StatementLineDirection.Credit : StatementLineDirection.Debit;
        var fecha = request.NoteDate ?? hoy;
        if (request.StatementLinePublicId is { } lp)
        {
            var linea = await db.TreasuryBankStatementLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == lp, ct);
            if (linea is null || linea.BankAccountId != cuenta.Id) return Result.Failure<BankNoteDto>(ErroresDeTesoreriaT2.LineaDeOtraCuenta());
            if (linea.Amount != request.Amount) return Result.Failure<BankNoteDto>(ErroresDeTesoreriaT2.ValorDistintoDeLaLinea(linea.Amount));
            if (await db.TreasuryBankNotes.AnyAsync(n => n.StatementLinePublicId == lp && n.Status != BankNoteState.Rejected, ct))
                return Result.Failure<BankNoteDto>(ErroresDeTesoreriaT2.LineaConNota());
            sentido = linea.Direction;
            fecha = request.NoteDate ?? linea.MovementDate;
        }
        int? tercero = cuenta.BankThirdPartyPersonId;
        if (request.ThirdPartyPersonPublicId is { } tp)
        {
            tercero = await db.People.AsNoTracking().Where(p => p.PublicId == tp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (tercero is null) return Result.Failure<BankNoteDto>(ErroresDeTesoreria.NoEncontrado("El tercero"));
        }
        var baseDeLaNota = request.BaseAmount ?? request.Kind switch
        {
            BankNoteKind.Commission => request.Amount - request.VatAmount,
            BankNoteKind.Interest => request.Amount + request.WithholdingAmount,
            _ => request.Amount,
        };
        var exigeAprobacion = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.NotasBancariasExigenAprobacion, fecha, ct);
        if (!exigeAprobacion)
        {
            var previa = await emision.ValidacionPreviaAsync([IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.NotaBancariaRegistradaV1.Type], cuenta, fecha, ct);
            if (previa.IsFailure) return Result.Failure<BankNoteDto>(previa.Error);
        }

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: false, ct);
        var nota = new BankNote
        {
            Number = await numerador.AsignarAsync(ConciliacionCompletaDeTesoreria.ClaseDeNumeracion, ConciliacionCompletaDeTesoreria.ClaseDeNumeracion, ct),
            BankAccountId = cuenta.Id, StatementLinePublicId = request.StatementLinePublicId, Kind = request.Kind, NoteDate = fecha, Direction = sentido, Amount = request.Amount,
            BaseAmount = baseDeLaNota, VatAmount = request.VatAmount, WithholdingAmount = request.WithholdingAmount, ThirdPartyPersonId = tercero,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), RegisteredByUserId = usuario, RegisteredAt = reloj.UtcNow,
            Status = exigeAprobacion ? BankNoteState.PendingApproval : BankNoteState.Approved,
        };
        if (!exigeAprobacion) (nota.DecidedByUserId, nota.DecidedAt) = (usuario, reloj.UtcNow);
        db.TreasuryBankNotes.Add(nota);
        if (!exigeAprobacion) await emision.NotaBancariaAsync(cuenta, nota, ct);
        await auditoria.AgregarAsync("Treasury.BankNote.Registered", "BankNote", nota.PublicId, null,
            new { number = nota.Number, account = cuenta.Code, kind = nota.Kind.ToString(), amount = nota.Amount, status = nota.Status.ToString(), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionCompletaDeTesoreria.DtoAsync(db, nota, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/bank-notes/{id}/approve</c> y <c>/reject</c> (<c>Treasury.BankNotes.Approve</c>, con clave; FR-130): otra persona aprueba la nota —nace su hecho— o
/// la rechaza con motivo. (nuevo)
/// </summary>
public sealed record DecideBankNoteCommand(Guid BankNotePublicId, bool Approve, string? Reason = null) : IRequest<Result<BankNoteDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class DecideBankNoteCommandValidator : AbstractValidator<DecideBankNoteCommand>
{
    public DecideBankNoteCommandValidator()
    {
        RuleFor(x => x.BankNotePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Approve).WithMessage("Escriba por qué se rechaza la nota.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class DecideBankNoteCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<DecideBankNoteCommand, Result<BankNoteDto>>
{
    public async Task<Result<BankNoteDto>> Handle(DecideBankNoteCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BankNoteDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var nota = await db.TreasuryBankNotes.FirstOrDefaultAsync(n => n.PublicId == request.BankNotePublicId, ct);
        if (nota is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(nota.BankAccountId)) return Result.Failure<BankNoteDto>(ErroresDeTesoreria.NoEncontrado("La nota bancaria"));
        if (nota.Status != BankNoteState.PendingApproval) return Result.Failure<BankNoteDto>(ErroresDeTesoreriaT2.NotaNoPorAprobar());
        if (nota.RegisteredByUserId == usuario) return Result.Failure<BankNoteDto>(ErroresDeTesoreriaT2.NotaAutoaprobada());
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == nota.BankAccountId, ct);
        if (request.Approve)
        {
            var previa = await emision.ValidacionPreviaAsync([IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.NotaBancariaRegistradaV1.Type], cuenta, nota.NoteDate,
                ct);
            if (previa.IsFailure) return Result.Failure<BankNoteDto>(previa.Error);
        }
        (nota.Status, nota.DecidedByUserId, nota.DecidedAt, nota.RejectionReason) =
            (request.Approve ? BankNoteState.Approved : BankNoteState.Rejected, usuario, reloj.UtcNow, request.Approve ? null : request.Reason!.Trim());
        if (request.Approve) await emision.NotaBancariaAsync(cuenta, nota, ct);
        await auditoria.AgregarAsync(request.Approve ? "Treasury.BankNote.Approved" : "Treasury.BankNote.Rejected", "BankNote", nota.PublicId, null,
            new { number = nota.Number, reason = nota.RejectionReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionCompletaDeTesoreria.DtoAsync(db, nota, ct));
    }
}

/// <summary><c>GET /api/treasury/bank-notes?account=&amp;state=</c> (<c>Treasury.Reconciliation.View</c> o <c>Treasury.BankNotes.Register</c>). (nuevo)</summary>
public sealed record ListBankNotesQuery(Guid? BankAccountPublicId = null, BankNoteState? State = null) : IRequest<Result<IReadOnlyList<BankNoteDto>>>;

public sealed class ListBankNotesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance) : IRequestHandler<ListBankNotesQuery, Result<IReadOnlyList<BankNoteDto>>>
{
    public async Task<Result<IReadOnlyList<BankNoteDto>>> Handle(ListBankNotesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.TreasuryBankNotes.AsNoTracking();
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(n => n.BankAccountId == id);
        }
        if (request.State is { } s) consulta = consulta.Where(n => n.Status == s);
        var filas = (await consulta.OrderByDescending(n => n.Id).Take(300).ToListAsync(ct)).Where(n => mio.IncluyeCuenta(n.BankAccountId)).ToList();
        var lista = new List<BankNoteDto>(filas.Count);
        foreach (var n in filas) lista.Add(await ConciliacionCompletaDeTesoreria.DtoAsync(db, n, ct));
        return Result.Success<IReadOnlyList<BankNoteDto>>(lista);
    }
}

/// <summary>
/// <c>GET /api/treasury/bank-notes/suggestions?account=&amp;from=&amp;to=</c> (<c>Treasury.BankNotes.Register</c>; FR-130): las líneas del extracto de la cuenta, sin cruzar ni
/// con nota, cuya descripción parece GMF, comisión, intereses o impuestos —el GMF real del extracto se registra así como nota—. (nuevo)
/// </summary>
public sealed record ListBankNoteSuggestionsQuery(Guid BankAccountPublicId, DateOnly? From = null, DateOnly? To = null) : IRequest<Result<IReadOnlyList<BankNoteSuggestionDto>>>;

public sealed class ListBankNoteSuggestionsQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IDateTimeService reloj)
    : IRequestHandler<ListBankNoteSuggestionsQuery, Result<IReadOnlyList<BankNoteSuggestionDto>>>
{
    public async Task<Result<IReadOnlyList<BankNoteSuggestionDto>>> Handle(ListBankNoteSuggestionsQuery request, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.BankAccountPublicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id))
            return Result.Failure<IReadOnlyList<BankNoteSuggestionDto>>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        var hasta = request.To ?? reloj.HoyLocal;
        var desde = request.From ?? hasta.AddMonths(-1);
        var lineas = await db.TreasuryBankStatementLines.AsNoTracking()
            .Where(l => l.BankAccountId == cuenta.Id && l.MovementDate >= desde && l.MovementDate <= hasta
                        && l.ReconciliationStatus == StatementLineReconciliationStatus.Open)
            .OrderBy(l => l.MovementDate).ToListAsync(ct);
        var conNota = (await db.TreasuryBankNotes.AsNoTracking().Where(n => n.BankAccountId == cuenta.Id && n.StatementLinePublicId != null && n.Status != BankNoteState.Rejected)
            .Select(n => n.StatementLinePublicId!.Value).ToListAsync(ct)).ToHashSet();
        var lista = lineas.Where(l => !conNota.Contains(l.PublicId))
            .Select(l => (l, Clase: ConciliacionCompletaDeTesoreria.Sugerir(l.Description ?? l.Reference)))
            .Where(x => x.Clase is not null)
            .Select(x => new BankNoteSuggestionDto(x.l.PublicId, x.l.MovementDate, x.l.Direction, x.l.Amount, x.l.Description, x.Clase!.Value)).ToList();
        return Result.Success<IReadOnlyList<BankNoteSuggestionDto>>(lista);
    }
}

/// <summary>Los errores de la Tesorería ampliada (feature 015, T2; nuevos, DT §2.7). (nuevo)</summary>
public static partial class ErroresDeTesoreriaT2
{
    public static Error LineaDeOtraCuenta() => new("Treasury.BankNote.StatementLineMismatch", "La línea no es del extracto de esa cuenta: elija una línea de la cuenta de la nota.");

    public static Error ValorDistintoDeLaLinea(decimal linea) => new ErrorConDatos("Treasury.BankNote.AmountMismatch",
        string.Create(CultureInfo.GetCultureInfo("es-CO"), $"La línea del extracto es de {linea:N2}: la nota mueve ese mismo valor."), new { statementLine = linea });

    public static Error LineaConNota() => new("Treasury.BankNote.StatementLineHasNote", "La línea del extracto ya tiene su nota bancaria.");

    public static Error NotaNoPorAprobar() => new("Treasury.BankNote.NotPendingApproval", "La nota bancaria no está por aprobar.");

    public static Error NotaAutoaprobada() => new("Treasury.BankNote.SelfApproval", "La nota bancaria la aprueba una persona distinta de quien la registró.");
}
