using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Rates;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>Una versión de captación con sus tramos, para resolver la tasa de cada día. (nuevo)</summary>
public sealed record VersionConTramos(DepositLineVersion Version, IReadOnlyList<DepositLineRateTier> Tramos);

/// <summary>
/// La ventana de interés de una cuenta (feature 014, US5, T131–T133): de <see cref="InicioExcluido"/> —el fin del período anterior o la última liquidación, lo
/// que sea más tarde— a <see cref="Hasta"/>, con el interés que da el motor día por día, la versión que rige al final y el período de liquidación. (nuevo)
/// </summary>
public sealed record VentanaDeInteres(
    MemberAccount Cuenta,
    DateOnly InicioExcluido,
    DateOnly Hasta,
    (DateOnly Desde, DateOnly Hasta)? Periodo,
    InteresDelPeriodo Interes,
    DepositLineVersion Version);

/// <summary>
/// Lo que lee el interés de una cuenta de captación (feature 014, US5, T131–T133; research R04, R05): las versiones de la línea con sus tramos (la que
/// sella la cuenta si la versión declara tasa pactada, FR-007; si no, la vigente cada día, bitemporal), la tasa variable resuelta con el último índice
/// publicado a la fecha de cada reajuste (<c>TasaDelDia.TasaVariable</c>, a seis decimales como el reajuste de la 013), los saldos de capital al cierre de
/// cada día por la reconstrucción de la cuenta a (D, R) y la suspensión de la causación (FR-065). Arma la entrada de <see cref="InteresDeCaptacion"/> y
/// devuelve la ventana; también suma lo ya causado en el libro de una ventana. No escribe. (nuevo)
/// </summary>
public sealed class LectorDeInteres(IApplicationDbContext db, IValoresNacionales valores)
{
    private readonly Dictionary<int, IReadOnlyList<VersionConTramos>> _versiones = [];
    private readonly Dictionary<(string Codigo, string Clave, DateOnly Fecha), Result<ValorNacionalVigente>> _indices = [];

    /// <summary>
    /// Las clases de fila que forman lo causado de una ventana: la causación y sus ajustes, en el interés por pagar. US9 (T154, research E121): también el
    /// saldo inicial del interés causado no liquidado de una cuenta migrada, fechado al corte —la primera liquidación lo abona sin volver a causarlo—; la
    /// ventana de causación de una cuenta migrada empieza en el corte (<see cref="CorteDeMigracion"/>), así que el motor nunca lo recalcula.
    /// </summary>
    public static IReadOnlyList<MemberAccountTransactionKind> ClasesDeLoCausado { get; } =
        [MemberAccountTransactionKind.InterestAccrued, MemberAccountTransactionKind.AccrualAdjustment, MemberAccountTransactionKind.OpeningBalance];

    /// <summary>
    /// US9 (T154, research E121): el corte de una cuenta migrada —la víspera del día en que se activó—; nulo en una cuenta abierta en la Cartera. Antes del
    /// corte la Cartera no conoce los saldos diarios (la cuenta nace con su saldo al corte), así que ni la causación ni su ajuste recalculan días anteriores.
    /// </summary>
    public static DateOnly? CorteDeMigracion(MemberAccount cuenta) =>
        cuenta.Origin == MemberAccountOrigin.Migration && cuenta.ActivatedOn is { } activada ? activada.AddDays(-1) : null;

    /// <summary>Las versiones de la línea con sus tramos (una lectura por línea y por lector).</summary>
    public async Task<IReadOnlyList<VersionConTramos>> VersionesAsync(int lineaId, CancellationToken ct)
    {
        if (_versiones.TryGetValue(lineaId, out var leidas)) return leidas;
        var versiones = await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == lineaId && !v.IsDeleted).ToListAsync(ct);
        var ids = versiones.Select(v => v.Id).ToList();
        var tramos = await db.DepositLineRateTiers.AsNoTracking().Where(t => ids.Contains(t.DepositLineVersionId) && !t.IsDeleted).ToListAsync(ct);
        var lista = versiones.Select(v => new VersionConTramos(v, tramos.Where(t => t.DepositLineVersionId == v.Id).OrderBy(t => t.Order).ToList())).ToList();
        _versiones[lineaId] = lista;
        return lista;
    }

    /// <summary>
    /// La versión que rige el día para la cuenta: la sellada al abrir si declara tasa pactada —y siempre en el ahorro contractual, que sella sus condiciones al
    /// constituirse (FR-007; US7, T182)—; si no, la vigente ese día con lo sabido hasta R.
    /// </summary>
    public static VersionConTramos? VersionDelDia(IReadOnlyList<VersionConTramos> versiones, MemberAccount cuenta, DateOnly dia, DateTime informacionHasta)
    {
        var deApertura = VigenciaBitemporal.VigenteDeCartera(versiones.Select(v => v.Version), cuenta.OpenedOn, informacionHasta);
        var elegida = deApertura is { DeclaresAgreedRate: true } || (deApertura is not null && cuenta.DepositClass == DepositClass.Contractual)
            ? deApertura
            : VigenciaBitemporal.VigenteDeCartera(versiones.Select(v => v.Version), dia, informacionHasta) ?? deApertura;
        return elegida is null ? null : versiones.First(v => v.Version.Id == elegida.Id);
    }

    /// <summary>
    /// El inicio (excluido) de la ventana del día <paramref name="dia"/>: el fin del período de liquidación anterior, la última liquidación, la apertura o, en
    /// una cuenta migrada, el corte, lo que sea más tarde. Con pago único (sin períodos), la última liquidación, la apertura o el corte.
    /// </summary>
    public static DateOnly InicioDeLaVentana(MemberAccount cuenta, MemberAccountControl control, DepositLineVersion version, DateOnly dia)
    {
        var periodo = InteresDeCaptacion.PeriodoDeLiquidacion(version.SettlementPeriodicity, version.SettlementDay, dia);
        var inicio = cuenta.OpenedOn;
        if (control.LastSettledThrough is { } liquidada && liquidada > inicio) inicio = liquidada;
        if (periodo is { } p && p.Desde.AddDays(-1) > inicio) inicio = p.Desde.AddDays(-1);
        if (CorteDeMigracion(cuenta) is { } corte && corte > inicio) inicio = corte;
        return inicio;
    }

    /// <summary>
    /// El interés de la ventana (<paramref name="inicioExcluido"/>, <paramref name="hasta"/>] con lo sabido hasta <paramref name="informacionHasta"/>. Falla
    /// con <c>NationalValues.Missing</c> si una tasa variable no tiene índice publicado, y con <c>Lending.Savings.LineWithoutVersion</c> si la línea no tiene
    /// versión a una fecha.
    /// </summary>
    public async Task<Result<VentanaDeInteres>> VentanaAsync(MemberAccount cuenta, DateOnly inicioExcluido, DateOnly hasta, DateTime informacionHasta,
        bool titularFallecido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var versiones = await VersionesAsync(cuenta.CreditLineId, ct);
        var libro = await LibroDeCapitalAsync(cuenta.Id, ct);
        var saldos = hasta <= inicioExcluido
            ? []
            : ReconstruccionDeCuenta.SaldosDiarios(libro, inicioExcluido, hasta, informacionHasta);
        var dias = new List<DiaDeCaptacion>(saldos.Count);
        VersionConTramos? ultima = VersionDelDia(versiones, cuenta, hasta, informacionHasta);
        // US7 (T182): el contractual causa a la tasa de sus condiciones selladas (la base y, desde el incumplimiento, la de castigo) y no causa después de
        // su vencimiento (FR-074, FR-075).
        var sellado = cuenta.DepositClass == DepositClass.Contractual ? await CondicionesSelladasAsync(cuenta.Id, ct) : null;
        foreach (var s in saldos)
        {
            var v = VersionDelDia(versiones, cuenta, s.Dia, informacionHasta);
            if (v is null)
                return Result.Failure<VentanaDeInteres>(new ErrorConDatos("Lending.Savings.LineWithoutVersion",
                    $"La línea de la cuenta {cuenta.Number} no tiene versión vigente el {s.Dia:yyyy-MM-dd}: no se puede causar.", new { date = s.Dia }));
            Result<TasaDeCaptacionDelDia> tasa;
            var vencido = false;
            if (sellado is { Count: > 0 })
            {
                var t = sellado.Where(x => x.EffectiveValueDate < s.Dia).MaxBy(x => x.VersionNumber) ?? sellado.MinBy(x => x.VersionNumber)!;
                tasa = Result.Success(new TasaDeCaptacionDelDia(t.AppliedAnnualRate, [], RateTierMode.WholeBalance, v.Version.MinBalanceForInterest, t.DayCount,
                    t.RateEquivalence, v.Version.SettlementPeriodicity));
                vencido = t.MaturityDate is { } vence && s.Dia > vence;
            }
            else
            {
                tasa = await TasaAsync(v, s.Dia, ct);
            }
            if (tasa.IsFailure) return Result.Failure<VentanaDeInteres>(tasa.Error);
            dias.Add(new DiaDeCaptacion(s.Dia, s.Saldo(MemberAccountConcept.Principal), tasa.Value,
                vencido || Suspendido(cuenta, v.Version, s.Dia, titularFallecido)));
            ultima = v;
        }
        if (ultima is null)
            return Result.Failure<VentanaDeInteres>(new ErrorConDatos("Lending.Savings.LineWithoutVersion",
                $"La línea de la cuenta {cuenta.Number} no tiene versión vigente: no se puede causar.", new { date = hasta }));
        var interes = InteresDeCaptacion.Calcular(new EntradaDeInteres(ultima.Version.BalanceMethod, inicioExcluido, dias));
        var periodo = InteresDeCaptacion.PeriodoDeLiquidacion(ultima.Version.SettlementPeriodicity, ultima.Version.SettlementDay, hasta);
        return Result.Success(new VentanaDeInteres(cuenta, inicioExcluido, hasta, periodo, interes, ultima.Version));
    }

    /// <summary>
    /// ¿Está suspendida la causación ese día? (FR-065): con la marca de la cuenta (<c>AccrualSuspendedSince</c>), con el titular fallecido si la línea lo dice,
    /// embargada totalmente si la línea lo dice (US11: la marca la pone <c>RetencionDeEmbargos</c> con su fecha de reanudación), o inactiva si la línea no causa en la inactividad.
    /// </summary>
    public static bool Suspendido(MemberAccount cuenta, DepositLineVersion version, DateOnly dia, bool titularFallecido) =>
        cuenta.SuspendidaEl(dia)
        || (titularFallecido && version.SuspendsAccrualOnDeceased)
        || (!version.AccruesWhileInactive && cuenta.InactiveSince is { } inactiva && inactiva <= dia);

    /// <summary>US7 (T182): las versiones de condiciones selladas de un contractual (también las de esta unidad de trabajo sin guardar).</summary>
    public async Task<IReadOnlyList<MemberAccountTermsVersion>> CondicionesSelladasAsync(int cuentaId, CancellationToken ct)
    {
        var guardadas = cuentaId == 0
            ? []
            : await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == cuentaId && !v.IsDeleted).ToListAsync(ct);
        return guardadas.Concat(db.MemberAccountTermsVersions.Local.Where(v => v.Id == 0 && v.MemberAccountId == cuentaId)).ToList();
    }

    /// <summary>Lo ya causado en el libro en la ventana (<paramref name="inicioExcluido"/>, <paramref name="hasta"/>]: la causación y sus ajustes.</summary>
    public async Task<decimal> CausadoEnElLibroAsync(int cuentaId, DateOnly inicioExcluido, DateOnly hasta, CancellationToken ct)
    {
        var clases = ClasesDeLoCausado.ToArray();
        var guardado = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind)
                        && t.ValueDate > inicioExcluido && t.ValueDate <= hasta && !t.IsDeleted)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        // Lo agregado en esta misma unidad de trabajo y todavía sin guardar (el ajuste de la operación que se está escribiendo).
        var pendiente = db.MemberAccountTransactions.Local
            .Where(t => t.Id == 0 && t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind)
                        && t.ValueDate > inicioExcluido && t.ValueDate <= hasta)
            .Sum(t => t.Amount);
        return guardado + pendiente;
    }

    /// <summary>Las filas de capital de la cuenta tal como las ve la reconstrucción (también las de esta unidad de trabajo sin guardar).</summary>
    public async Task<IReadOnlyList<MovimientoDeCuenta>> LibroDeCapitalAsync(int cuentaId, CancellationToken ct)
    {
        var filas = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.Principal && !t.IsDeleted)
            .Select(t => new { t.Id, t.Kind, t.Concept, t.Amount, t.ValueDate, t.RegisteredAt, t.ReversesTransactionId })
            .ToListAsync(ct);
        var libro = filas.Select(f => new MovimientoDeCuenta(f.Id, f.Kind, f.Concept, f.Amount, f.ValueDate, f.RegisteredAt, f.ReversesTransactionId)).ToList();
        // Las de esta unidad sin Id: con un Id negativo propio, para que la reconstrucción las cuente (sin espejos: un espejo nuevo nombra a su original).
        var siguiente = -1L;
        foreach (var t in db.MemberAccountTransactions.Local.Where(t => t.Id == 0 && t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.Principal))
            libro.Add(new MovimientoDeCuenta(siguiente--, t.Kind, t.Concept, t.Amount, t.ValueDate, t.RegisteredAt, t.ReversesTransactionId));
        return libro;
    }

    /// <summary>
    /// La tasa del día de la versión: la fija (o sus tramos) o la variable con el último índice publicado a la fecha de su reajuste (el inicio del período de
    /// <c>ResetPeriodicity</c>, mensual si no lo dice), con el margen de la versión o de cada tramo, a seis decimales.
    /// </summary>
    public async Task<Result<TasaDeCaptacionDelDia>> TasaAsync(VersionConTramos vt, DateOnly dia, CancellationToken ct)
    {
        var v = vt.Version;
        var modo = v.RateTierMode ?? RateTierMode.WholeBalance;
        var porSaldo = vt.Tramos.Where(t => t.MinTermDays is null && t.MaxTermDays is null && t.MinAmount is null && t.MaxAmount is null).ToList();
        if (v.RateKind != RateKind.Variable)
            return Result.Success(new TasaDeCaptacionDelDia(v.AnnualRate ?? 0m,
                porSaldo.Select(t => new TramoDeInteres(t.MinBalance ?? 0m, t.MaxBalance, t.AnnualRate ?? 0m)).ToList(), modo, v.MinBalanceForInterest, v.DayCount,
                v.RateEquivalence, v.SettlementPeriodicity));

        if (string.IsNullOrWhiteSpace(v.IndexCode))
            return Result.Failure<TasaDeCaptacionDelDia>(new ErrorConDatos("Lending.Savings.LineWithoutVersion",
                $"La versión {v.VersionNumber} es de tasa variable y no nombra su índice.", new { version = v.VersionNumber }));
        var reajuste = CargosDeCuenta.PeriodoDe(v.ResetPeriodicity is { } r && r != RepaymentPeriodicity.SinglePayment ? r : RepaymentPeriodicity.Monthly, dia).Desde;
        var clave = (v.IndexCode.Trim().ToUpperInvariant(), v.IndexKey?.Trim() ?? string.Empty, reajuste);
        if (!_indices.TryGetValue(clave, out var indice))
        {
            indice = await valores.LeerUltimoPublicadoAsync(clave.Item1, clave.Item2, reajuste, null, ct);
            _indices[clave] = indice;
        }
        if (indice.IsFailure) return Result.Failure<TasaDeCaptacionDelDia>(indice.Error);
        var referencia = ReglasDeLineaDeCredito.Indice(indice.Value);
        decimal Variable(decimal margen) => decimal.Round(TasaDelDia.TasaVariable(referencia, margen, v.RateFloor, v.RateCeiling),
            EquivalenciaDeTasas.DecimalesDeTasaPactada, MidpointRounding.AwayFromZero);
        return Result.Success(new TasaDeCaptacionDelDia(Variable(v.Margin ?? 0m),
            porSaldo.Select(t => new TramoDeInteres(t.MinBalance ?? 0m, t.MaxBalance, Variable(t.Margin ?? v.Margin ?? 0m))).ToList(), modo, v.MinBalanceForInterest,
            v.DayCount, v.RateEquivalence, v.SettlementPeriodicity));
    }
}
