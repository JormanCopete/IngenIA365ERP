using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

// Feature 014, US16 (T164; api.md §4, «balance» y «statement»; FR-003, FR-011): el saldo de una cuenta a la fecha D con lo registrado hasta R y el extracto en
// pantalla de un período. Las dos son lecturas sensibles de los datos de un titular (Lending.Accounts.Read) y se auditan: Lending.Savings.BalanceViewed y
// Lending.Savings.StatementViewed. Todo dentro del alcance de la Cartera (oficina y línea de la cuenta; fuera, el mismo 404 que si no existiera). (nuevo)

/// <summary>El saldo de la cuenta a (D, R) (api.md §4, <c>GET /{id}/balance</c>). (nuevo)</summary>
public sealed record MemberAccountBalanceDto(
    Guid AccountPublicId,
    string Number,
    DateOnly Date,
    DateTime RecordedAsOf,
    string Fingerprint,
    IReadOnlyDictionary<string, decimal> ByConcept,
    decimal HolderBalance,
    DisponibleDto Available,
    decimal? AccruedNotSettled,
    IReadOnlyList<InterestExplanationDayDto> Explanation);

/// <summary>Una línea del extracto en pantalla: una operación (o lo causado de un mes sin operación) por su fecha valor. (nuevo)</summary>
public sealed record MemberAccountStatementLineDto(
    DateOnly ValueDate,
    Guid? OperationPublicId,
    string? OperationNumber,
    string Kind,
    decimal Amount,
    decimal Withholding,
    decimal Gmf,
    decimal Balance);

/// <summary>El extracto en pantalla (api.md §4, <c>GET /{id}/statement</c>; no es un documento emitido, §17). (nuevo)</summary>
public sealed record MemberAccountStatementDto(
    Guid AccountPublicId,
    string Number,
    LineNature Nature,
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    IReadOnlyList<MemberAccountStatementLineDto> Lines,
    decimal InterestCredited,
    decimal Withholding,
    decimal Gmf,
    decimal ClosingBalance,
    DisponibleDto AvailableToday);

// ----------------------------------------------------------------------------------------------------------------- saldo --

/// <summary>
/// El saldo de una cuenta a la fecha D (pasada, hoy o futura) con lo registrado hasta R (api.md §4, <c>GET /{id}/balance?date=&amp;recordedAsOf=</c>,
/// <c>Lending.Accounts.Read</c>; api.md §1.4; FR-011): los saldos por concepto de la reconstrucción del libro (<see cref="MotorDeCaptaciones"/>) con su
/// huella, el saldo del titular, el disponible (lo en canje y el mínimo de la versión a D), y en las cuentas que ganan interés lo causado sin liquidar a D
/// con la explicación día por día del motor. Auditado <c>Lending.Savings.BalanceViewed</c> con D, R y la huella. (nuevo)
/// </summary>
public sealed record GetMemberAccountBalanceQuery(Guid AccountPublicId, DateOnly? Date = null, DateTime? RecordedAsOf = null)
    : IRequest<Result<MemberAccountBalanceDto>>;

public sealed class GetMemberAccountBalanceQueryValidator : AbstractValidator<GetMemberAccountBalanceQuery>
{
    public GetMemberAccountBalanceQueryValidator() => RuleFor(x => x.AccountPublicId).NotEmpty();
}

public sealed class GetMemberAccountBalanceQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeMovimientos movimientos,
    LiquidacionDeIntereses liquidacion,
    IDateTimeService reloj) : IRequestHandler<GetMemberAccountBalanceQuery, Result<MemberAccountBalanceDto>>
{
    /// <summary>El valor nacional que falta (api.md §4: el único error propio del saldo).</summary>
    public const string CodigoSinValorNacional = "NationalValues.Missing";

    public async Task<Result<MemberAccountBalanceDto>> Handle(GetMemberAccountBalanceQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountBalanceDto>(leida.Error);
        var cuenta = leida.Value;
        var fecha = request.Date ?? reloj.HoyLocal;
        // Sin R, todo lo registrado: el sello del libro es monótono (RegistroDeCuentas) y puede ir unos instantes delante del reloj.
        var hasta = request.RecordedAsOf ?? reloj.UtcNow;
        var informacion = request.RecordedAsOf ?? DateTime.MaxValue;

        var libro = await ExtractosDeCuenta.LibroAsync(db, cuenta.Id, ct);
        var saldos = MotorDeCaptaciones.Reconstruir(libro, fecha, informacion);
        var delTitular = cuenta.Nature == LineNature.Contribution ? saldos.SaldoDeAportes : saldos.SaldoDeCaptacion;
        var enCanje = (await movimientos.EnCanjeAsync([cuenta.Id], fecha, ct)).GetValueOrDefault(cuenta.Id);
        var minimo = 0m;
        if (cuenta.Nature == LineNature.Deposit && cuenta.Status != MemberAccountStatus.Opening)
            minimo = (await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, fecha, null, ct))?.MinBalance ?? 0m;
        var disponible = DisponibleDeCuenta.Calcular(cuenta.Nature, delTitular, 0m, 0m, enCanje, minimo);

        decimal? causado = null;
        IReadOnlyList<InterestExplanationDayDto> explicacion = [];
        if (LiquidacionDeIntereses.GanaInteres(cuenta)
            && await db.MemberAccountControls.AsNoTracking().FirstOrDefaultAsync(c => c.MemberAccountId == cuenta.Id, ct) is { } control)
        {
            var calculo = await liquidacion.CalcularAsync(cuenta, control, fecha, request.RecordedAsOf, desdeElMotor: true, ct);
            // Sin un valor nacional (un índice de tasa variable, la UVT) no hay saldo honesto que dar (api.md §4); sin versión de la línea a (D, R) —una R anterior
            // a la línea— el saldo sí se sabe y lo causado no: va sin él.
            if (calculo.IsFailure && calculo.Error.Code == CodigoSinValorNacional) return Result.Failure<MemberAccountBalanceDto>(calculo.Error);
            causado = calculo.IsSuccess ? calculo.Value.Causado : null;
            explicacion = (calculo.IsSuccess ? calculo.Value.Explicacion?.Dias ?? [] : []).Select(d => new InterestExplanationDayDto(d.Dia, d.Saldo, d.Base, d.Tramo, d.TasaEA, d.TasaDiaria,
                d.Causado, d.BajoElMinimo, d.Suspendido, d.Explicacion)).ToList();
        }

        await auditoria.EmitirSaldoDeCuentaVistoAsync(cuenta.PublicId, "balance",
            new { date = fecha.ToString("yyyy-MM-dd"), recordedAsOf = hasta, sha256 = saldos.Huella }, ct);
        return Result.Success(new MemberAccountBalanceDto(cuenta.PublicId, cuenta.Number, fecha, hasta, saldos.Huella,
            ReconstruccionDeCuenta.Conceptos.ToDictionary(c => c.ToString(), saldos.Saldo), delTitular, ReglasDeMovimientos.Dto(disponible), causado,
            explicacion));
    }
}

// --------------------------------------------------------------------------------------------------------------- extracto --

/// <summary>
/// El extracto en pantalla de un período (api.md §4, <c>GET /{id}/statement?from=&amp;to=</c>, <c>Lending.Accounts.Read</c>; no es documento, §17): el saldo
/// del titular al empezar, una línea por operación por su fecha valor —lo causado sin operación va en una línea por mes— con su efecto en el saldo del
/// titular, la retención y el GMF, los intereses abonados, el saldo al terminar y el disponible de hoy. Sin <c>from</c>, desde el primer día del mes de
/// <c>to</c>; sin <c>to</c>, hasta hoy. Auditado <c>Lending.Savings.StatementViewed</c>. (nuevo)
/// </summary>
public sealed record GetMemberAccountStatementQuery(Guid AccountPublicId, DateOnly? From = null, DateOnly? To = null)
    : IRequest<Result<MemberAccountStatementDto>>;

public sealed class GetMemberAccountStatementQueryValidator : AbstractValidator<GetMemberAccountStatementQuery>
{
    public GetMemberAccountStatementQueryValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithName("from")
            .WithMessage("El extracto no empieza después de terminar.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= ExtractosDeCuenta.DiasMaximos)
            .WithName("to").WithMessage($"El extracto en pantalla cubre a lo sumo {ExtractosDeCuenta.DiasMaximos} días; para más, emita el documento.");
    }
}

public sealed class GetMemberAccountStatementQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeMovimientos movimientos,
    IDateTimeService reloj) : IRequestHandler<GetMemberAccountStatementQuery, Result<MemberAccountStatementDto>>
{
    public async Task<Result<MemberAccountStatementDto>> Handle(GetMemberAccountStatementQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountStatementDto>(leida.Error);
        var cuenta = leida.Value;
        var hoy = reloj.HoyLocal;
        var hasta = request.To ?? hoy;
        var desde = request.From ?? new DateOnly(hasta.Year, hasta.Month, 1);
        if (desde > hasta)
            return Result.Failure<MemberAccountStatementDto>(new ErrorConDatos("Validation.Invalid", "El extracto no empieza después de terminar.",
                new { from = desde, to = hasta }));

        var filas = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuenta.Id && !t.IsDeleted && t.ValueDate <= hasta)
            .Select(t => new FilaDeExtracto(t.Id, t.Kind, t.Concept, t.Amount, t.ValueDate, t.OperationId))
            .ToListAsync(ct);
        var idsDeOperacion = filas.Where(f => f.OperacionId is not null && f.FechaValor >= desde).Select(f => f.OperacionId!.Value).Distinct().ToList();
        var operaciones = await db.MemberAccountOperations.AsNoTracking().Where(o => idsDeOperacion.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => new OperacionDeExtracto(o.PublicId, o.Number, o.Kind.ToString()), ct);

        var extracto = ExtractosDeCuenta.Armar(cuenta.Nature, desde, hasta, filas, operaciones);
        var disponible = (await movimientos.LeerAsync(cuenta, hoy, ct)).Disponible;

        await auditoria.EmitirExtractoDeCuentaVistoAsync(cuenta.PublicId, desde, hasta, extracto.Lineas.Count, ct);
        return Result.Success(new MemberAccountStatementDto(cuenta.PublicId, cuenta.Number, cuenta.Nature, desde, hasta, extracto.SaldoInicial, extracto.Lineas,
            extracto.InteresesAbonados, extracto.Retencion, extracto.Gmf, extracto.SaldoFinal, ReglasDeMovimientos.Dto(disponible)));
    }
}

/// <summary>Una fila del libro tal como la lee el extracto. (nuevo)</summary>
public sealed record FilaDeExtracto(long Id, MemberAccountTransactionKind Clase, MemberAccountConcept Concepto, decimal Monto, DateOnly FechaValor, long? OperacionId);

/// <summary>La operación de una línea del extracto. (nuevo)</summary>
public sealed record OperacionDeExtracto(Guid PublicId, string Numero, string Clase);

/// <summary>El extracto armado (sin IO). (nuevo)</summary>
public sealed record ExtractoArmado(decimal SaldoInicial, IReadOnlyList<MemberAccountStatementLineDto> Lineas, decimal InteresesAbonados, decimal Retencion,
    decimal Gmf, decimal SaldoFinal);

/// <summary>
/// Lo que comparten el saldo y el extracto (feature 014, US16, T164): el libro de la cuenta como lo ve la reconstrucción y el armado del extracto, que es
/// puro: el saldo inicial es el del titular con lo de fecha valor anterior a <c>from</c>, cada línea suma el efecto de sus filas en el saldo del titular
/// (<see cref="LectorDeCuentas.SaldoDelTitular"/>, data-model §0.2) y el saldo final es el inicial más todas las líneas: cuadra con el libro siempre. (nuevo)
/// </summary>
public static class ExtractosDeCuenta
{
    /// <summary>El período más largo del extracto en pantalla (un año y un día, para cubrir un año bisiesto completo).</summary>
    public const int DiasMaximos = 366;

    /// <summary>La línea de lo causado sin operación (la causación diaria del ciclo) de un mes.</summary>
    public const string CausadoDelMes = "InterestAccrued";

    /// <summary>Todas las filas del libro de la cuenta como las lee la reconstrucción.</summary>
    public static async Task<IReadOnlyList<MovimientoDeCuenta>> LibroAsync(IApplicationDbContext db, int cuentaId, CancellationToken ct)
    {
        var filas = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuentaId && !t.IsDeleted)
            .Select(t => new { t.Id, t.Kind, t.Concept, t.Amount, t.ValueDate, t.RegisteredAt, t.ReversesTransactionId })
            .ToListAsync(ct);
        return filas.Select(f => new MovimientoDeCuenta(f.Id, f.Kind, f.Concept, f.Amount, f.ValueDate, f.RegisteredAt, f.ReversesTransactionId)).ToList();
    }

    /// <summary>El efecto de una fila en el saldo del titular.</summary>
    public static decimal Efecto(LineNature naturaleza, MemberAccountConcept concepto, decimal monto) =>
        LectorDeCuentas.SaldoDelTitular(naturaleza, new Dictionary<MemberAccountConcept, decimal> { [concepto] = monto });

    /// <summary>Arma el extracto de (<paramref name="desde"/>, <paramref name="hasta"/>) con las filas de fecha valor hasta <paramref name="hasta"/>.</summary>
    public static ExtractoArmado Armar(LineNature naturaleza, DateOnly desde, DateOnly hasta, IReadOnlyList<FilaDeExtracto> filas,
        IReadOnlyDictionary<long, OperacionDeExtracto> operaciones)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(operaciones);
        var inicial = filas.Where(f => f.FechaValor < desde).Sum(f => Efecto(naturaleza, f.Concepto, f.Monto));
        var delPeriodo = filas.Where(f => f.FechaValor >= desde && f.FechaValor <= hasta).ToList();

        var grupos = delPeriodo
            .GroupBy(f => f.OperacionId is { } o
                ? (Fecha: f.FechaValor, Operacion: (long?)o, Clase: (string?)null)
                : (Fecha: FinDeMes(f.FechaValor, hasta), Operacion: (long?)null, Clase: f.Clase == MemberAccountTransactionKind.InterestAccrued
                    ? CausadoDelMes
                    : f.Clase.ToString()))
            .OrderBy(g => g.Key.Fecha).ThenBy(g => g.Min(f => f.Id))
            .ToList();

        var saldo = inicial;
        var lineas = new List<MemberAccountStatementLineDto>(grupos.Count);
        foreach (var g in grupos)
        {
            var efecto = g.Sum(f => Efecto(naturaleza, f.Concepto, f.Monto));
            saldo += efecto;
            var op = g.Key.Operacion is { } id ? operaciones.GetValueOrDefault(id) : null;
            lineas.Add(new MemberAccountStatementLineDto(g.Key.Fecha, op?.PublicId, op?.Numero, op?.Clase ?? g.Key.Clase ?? string.Empty, efecto,
                g.Where(f => f.Concepto == MemberAccountConcept.Withholding).Sum(f => f.Monto),
                g.Where(f => f.Concepto == MemberAccountConcept.Gmf).Sum(f => f.Monto), saldo));
        }

        return new ExtractoArmado(inicial, lineas,
            delPeriodo.Where(f => f.Clase == MemberAccountTransactionKind.InterestCredited && f.Concepto == MemberAccountConcept.Principal).Sum(f => f.Monto),
            delPeriodo.Where(f => f.Concepto == MemberAccountConcept.Withholding).Sum(f => f.Monto),
            delPeriodo.Where(f => f.Concepto == MemberAccountConcept.Gmf).Sum(f => f.Monto),
            saldo);
    }

    private static DateOnly FinDeMes(DateOnly dia, DateOnly tope)
    {
        var fin = new DateOnly(dia.Year, dia.Month, DateTime.DaysInMonth(dia.Year, dia.Month));
        return fin < tope ? fin : tope;
    }
}
