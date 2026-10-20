using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>
/// Una liquidación calculada sin escribir (feature 014, US5, T132, T133; FR-061, FR-063, FR-064): la ventana (desde, hasta], lo causado, lo que se abona
/// redondeado y su diferencia, la retención con su política y el perfil del titular, el interés de cada día que midió el umbral y, en la simulación, la
/// explicación día por día del motor. (nuevo)
/// </summary>
public sealed record LiquidacionCalculada(
    MemberAccount Cuenta,
    DepositLineVersion Version,
    DateOnly Desde,
    DateOnly Hasta,
    decimal Causado,
    decimal Bruto,
    decimal Redondeo,
    RetencionCalculada Retencion,
    PoliticaDeRetencion Politica,
    PerfilDeRetencion Perfil,
    IReadOnlyList<InteresDiarioDeRetencion> Diarios,
    InteresDelPeriodo? Explicacion,
    bool PersonaNatural)
{
    public decimal Neto => Bruto - Retencion.Valor;
}

/// <summary>Lo que dejó una liquidación: la liquidación (nula si no había nada que liquidar), su operación y si ya existía (idempotencia, SC-003). (nuevo)</summary>
public sealed record LiquidacionAplicada(InterestSettlement? Liquidacion, MemberAccountOperation? Operacion, bool YaExistia, LiquidacionCalculada? Calculo);

/// <summary>
/// La liquidación de intereses de una cuenta y un período (feature 014, US5, T132; research R05, R06, D9, D10; FR-061, FR-063, SC-003, SC-016): <b>idempotente
/// por cuenta y período</b> —volver a liquidar responde con la existente—, exige la causación al día hasta el fin del período (<c>Lending.Savings.AccrualBehind</c>)
/// y en una sola operación <c>InterestSettlement</c> (o dentro de la cancelación):
/// <list type="number">
/// <item>ajusta la diferencia de redondeo entre lo causado (seis decimales) y lo que se abona (la unidad de la versión, D9): <c>AccrualAdjustment</c>;</item>
/// <item>abona el bruto al capital: <c>InterestCredited</c> (−interés por pagar, +capital);</item>
/// <item>retiene <b>antes</b> de capitalizar (FR-063) por <see cref="RetencionSobreRendimientos"/>, con el interés de cada día del período para el umbral:
/// <c>WithholdingApplied</c> en pareja (−capital, +retención);</item>
/// <item>reparte el bruto, el componente inflacionario, la base y la retención entre el titular y los cotitulares por su proporción
/// (<c>LND_WithholdingAccumulations</c>, para el certificado);</item>
/// <item>emite <c>InteresDeCaptacionLiquidado</c> (+ <c>RetencionPracticada</c>) y marca el período liquidado.</item>
/// </list>
/// Escribe por <see cref="RegistroDeCuentas"/>; corre en la transacción y bajo el cerrojo de quien llama y guarda. (nuevo)
/// </summary>
public sealed class LiquidacionDeIntereses(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    LectorDeInteres lector,
    ReglasDeRetencion retencion,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El prefijo del comprobante de una liquidación de intereses.</summary>
    public const string PrefijoLiquidacion = "LQI-";

    public const string CodigoCausacionAtrasada = "Lending.Savings.AccrualBehind";
    public const string CodigoPeriodoAbierto = "Lending.Savings.SettlementPeriodOpen";
    public const string CodigoNoCausa = "Lending.Savings.AccountDoesNotAccrue";

    /// <summary>¿Liquida intereses por períodos? Cuentas de captación a la vista, permanentes y contractuales (el CDAT los paga por su plan).</summary>
    public static bool GanaInteres(MemberAccount cuenta) =>
        cuenta.Nature == LineNature.Deposit && cuenta.DepositClass is { } c && CausacionDeCaptaciones.ClasesQueLiquidan.Contains(c);

    /// <summary>El inicio (excluido) de la próxima liquidación: la última liquidada o la apertura.</summary>
    public static DateOnly InicioDeLiquidacion(MemberAccount cuenta, MemberAccountControl control) =>
        control.LastSettledThrough is { } l && l > cuenta.OpenedOn ? l : cuenta.OpenedOn;

    /// <summary>
    /// Calcula sin escribir la liquidación de (inicio, <paramref name="hasta"/>]. Con <paramref name="desdeElMotor"/> (la simulación, FR-064) lo causado sale
    /// del motor con lo sabido hasta <paramref name="informacionHasta"/> —a cualquier fecha, pasada o futura—; sin él, del libro (lo que de verdad se causó).
    /// </summary>
    public async Task<Result<LiquidacionCalculada>> CalcularAsync(MemberAccount cuenta, MemberAccountControl control, DateOnly hasta, DateTime? informacionHasta,
        bool desdeElMotor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(control);
        if (!GanaInteres(cuenta))
            return Result.Failure<LiquidacionCalculada>(new ErrorConDatos(CodigoNoCausa,
                $"La cuenta {cuenta.Number} no liquida intereses por este camino (aportes o CDAT).", new { nature = cuenta.Nature.ToString(), depositClass = cuenta.DepositClass?.ToString() }));
        var versiones = await lector.VersionesAsync(cuenta.CreditLineId, ct);
        var version = LectorDeInteres.VersionDelDia(versiones, cuenta, hasta, informacionHasta ?? DateTime.MaxValue);
        if (version is null)
            return Result.Failure<LiquidacionCalculada>(new ErrorConDatos("Lending.Savings.LineWithoutVersion",
                $"La línea de la cuenta {cuenta.Number} no tiene versión vigente el {ErroresDeCuentas.Fecha(hasta)}.", new { date = hasta }));
        var inicio = InicioDeLiquidacion(cuenta, control);
        if (hasta < inicio) hasta = inicio;

        decimal causado;
        IReadOnlyList<InteresDiarioDeRetencion> diarios;
        InteresDelPeriodo? explicacion = null;
        if (desdeElMotor)
        {
            var titularFallecido = await Deceased.TitularesFallecidos.EsAsync(db, cuenta.HolderPersonId, ct);
            // La simulación respeta las ventanas: cada período de liquidación vencido y no liquidado causa por su método, y el último hasta la fecha.
            var tramos = new List<InteresDelDia>();
            var desde = inicio;
            // US9 (research E121): en una cuenta migrada lo de antes del corte es el interés causado no liquidado que trajo la carga (el motor no conoce los
            // saldos diarios anteriores): se suma tal cual y el motor arranca en el corte.
            var migrado = 0m;
            IReadOnlyList<InteresDiarioDeRetencion> diariosMigrados = [];
            if (LectorDeInteres.CorteDeMigracion(cuenta) is { } corte && corte > desde)
            {
                migrado = await InteresMigradoAsync(cuenta.Id, desde, corte, ct);
                diariosMigrados = RepartirInteresMigrado(migrado, desde, corte < hasta ? corte : hasta);
                desde = corte < hasta ? corte : hasta;
            }
            while (desde < hasta)
            {
                var periodo = InteresDeCaptacion.PeriodoDeLiquidacion(version.Version.SettlementPeriodicity, version.Version.SettlementDay, desde.AddDays(1));
                var fin = periodo is { } p && p.Hasta < hasta ? p.Hasta : hasta;
                var ventana = await lector.VentanaAsync(cuenta, desde, fin, informacionHasta ?? DateTime.MaxValue, titularFallecido, ct);
                if (ventana.IsFailure) return Result.Failure<LiquidacionCalculada>(ventana.Error);
                tramos.AddRange(ventana.Value.Interes.Dias);
                desde = fin;
            }
            explicacion = new InteresDelPeriodo(version.Version.BalanceMethod, tramos);
            causado = explicacion.Causado + migrado;
            diarios = diariosMigrados.Concat(tramos.Select(t => new InteresDiarioDeRetencion(t.Dia, t.Causado))).ToList();
        }
        else
        {
            // Respuesta 64 B (E256): también lo que el período abierto trae de movimientos fechados en períodos ya liquidados.
            var clases = LectorDeInteres.ClasesDeLoLiquidable.ToArray();
            var filas = await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind)
                            && t.ValueDate > inicio && t.ValueDate <= hasta && !t.IsDeleted)
                .Select(t => new { t.ValueDate, t.Amount, t.Kind }).ToListAsync(ct);
            causado = filas.Sum(f => f.Amount);
            // US9 (research E121): el saldo inicial del interés de una cuenta migrada se reparte por igual entre los días que cubre para medir el umbral
            // diario de la retención (no es un día de causación).
            var migrado = filas.Where(f => f.Kind == MemberAccountTransactionKind.OpeningBalance).Sum(f => f.Amount);
            var corteMigrado = LectorDeInteres.CorteDeMigracion(cuenta);
            var repartidos = migrado != 0m && corteMigrado is { } c0 && c0 > inicio ? RepartirInteresMigrado(migrado, inicio, c0) : [];
            diarios = repartidos.Concat(filas.Where(f => f.Kind != MemberAccountTransactionKind.OpeningBalance || repartidos.Count == 0)
                    .GroupBy(f => f.ValueDate).OrderBy(g => g.Key).Select(g => new InteresDiarioDeRetencion(g.Key, g.Sum(f => f.Amount))))
                .GroupBy(d => d.Dia).OrderBy(g => g.Key).Select(g => new InteresDiarioDeRetencion(g.Key, g.Sum(d => d.Interes))).ToList();
        }

        var bruto = InteresDeCaptacion.Liquidable(causado, version.Version.Redondeo());
        var politica = await retencion.PoliticaAsync(version.Version, hasta, ct);
        if (politica.IsFailure) return Result.Failure<LiquidacionCalculada>(politica.Error);
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        var (perfil, natural) = ReglasDeRetencion.PerfilDe(titular);
        var retenido = RetencionSobreRendimientos.Calcular(politica.Value,
            new EntradaDeRetencion(perfil, natural, version.Version.DepositClass, bruto, diarios));
        return Result.Success(new LiquidacionCalculada(cuenta, version.Version, inicio, hasta, causado, bruto, bruto - causado, retenido, politica.Value, perfil,
            diarios, explicacion, natural));
    }

    /// <summary>
    /// Liquida (inicio, <paramref name="hasta"/>] (ver la clase). <paramref name="enLaOperacion"/> es la operación de la cancelación, si liquida dentro de ella;
    /// si no, nace la operación <c>InterestSettlement</c>. La cuenta ya está bloqueada.
    /// </summary>
    public async Task<Result<LiquidacionAplicada>> LiquidarAsync(MemberAccount cuenta, MemberAccountControl control, DateOnly hasta, LendingProcessRun? corrida,
        MemberAccountOperation? enLaOperacion, string? motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(control);
        var existente = await db.InterestSettlements.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemberAccountId == cuenta.Id && s.PeriodTo == hasta && s.ReversedByOperationId == null && !s.IsDeleted, ct);
        if (existente is not null)
            return Result.Success(new LiquidacionAplicada(existente,
                await db.MemberAccountOperations.AsNoTracking().FirstAsync(o => o.Id == existente.OperationId, ct), true, null));
        if (control.LastAccruedDate < hasta)
            return Result.Failure<LiquidacionAplicada>(new ErrorConDatos(CodigoCausacionAtrasada,
                $"La cuenta {cuenta.Number} está causada hasta el {ErroresDeCuentas.Fecha(control.LastAccruedDate)}: no se liquida hasta el {ErroresDeCuentas.Fecha(hasta)}.",
                new { lastAccruedDate = control.LastAccruedDate }));

        var calculo = await CalcularAsync(cuenta, control, hasta, null, desdeElMotor: false, ct);
        if (calculo.IsFailure) return Result.Failure<LiquidacionAplicada>(calculo.Error);
        var c = calculo.Value;
        if (c.Causado == 0m && c.Bruto == 0m)
        {
            // Nada que liquidar: sólo avanza la marca del período (no hay operación ni hecho).
            if (control.LastSettledThrough is not { } previo || hasta > previo) RegistroDeCuentas.MarcarLiquidacion(control, hasta);
            return Result.Success(new LiquidacionAplicada(null, enLaOperacion, false, c));
        }

        var hecho = await emision.HechoDeCuentaAsync(cuenta, hasta, ct);
        if (hecho.IsFailure) return Result.Failure<LiquidacionAplicada>(hecho.Error);
        var exenta = await db.GmfExemptAccounts.AsNoTracking()
            .AnyAsync(g => g.MemberAccountId == cuenta.Id && !g.IsDeleted && g.ValidFrom <= hasta && (g.ValidTo == null || g.ValidTo >= hasta), ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var numero = enLaOperacion is null
            ? await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.InterestSettlement, PrefijoLiquidacion, ct)
            : string.Empty;
        var operacion = enLaOperacion ?? registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.InterestSettlement,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hasta,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = c.Bruto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = RecaudadorPropio,
            Reason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim().Length <= 500 ? motivo.Trim() : motivo.Trim()[..500],
            Payload = JsonSerializer.Serialize(new { period = new { from = c.Desde, to = c.Hasta }, accrued = c.Causado, gross = c.Bruto, withholding = c.Retencion.Valor }, Json),
            InputsSha256 = Huella(c),
            RulesVersion = VersionesDeReglas.Vigente,
        });

        var liquidacion = new InterestSettlement
        {
            MemberAccountId = cuenta.Id,
            Operation = operacion,
            OperationId = operacion.Id,
            PeriodFrom = c.Desde.AddDays(1),
            PeriodTo = c.Hasta,
            AccruedInterest = c.Causado,
            GrossInterest = c.Bruto,
            RoundingDifference = c.Redondeo,
            ThresholdReached = c.Retencion.UmbralAlcanzado,
            ThresholdDailyAmount = c.Retencion.UmbralDiario,
            InflationaryComponentFraction = c.Retencion.ComponenteInflacionario,
            InflationaryComponentAmount = c.Retencion.ComponenteInflacionario is null ? null : c.Retencion.ComponenteInflacionarioValor,
            WithholdingBase = c.Retencion.SeRetiene ? c.Retencion.BaseGravable : 0m,
            WithholdingRate = c.Retencion.Tarifa,
            WithholdingAmount = c.Retencion.Valor,
            WithholdingExemptionReason = c.Retencion.MotivoDeExencion,
            NetInterest = c.Neto,
            Destination = InterestDestination.Capitalize,
            TaxProfileSnapshotJson = JsonSerializer.Serialize(new
            {
                profile = c.Perfil.ToString(), naturalPerson = c.PersonaNatural, policy = c.Politica, explanation = c.Retencion.Explicacion,
            }, Json),
            InputsSha256 = Huella(c),
        };

        var unidad = await emision.InteresLiquidadoAsync(new LiquidacionAEmitir(new HechoDeCuentaMarcada(hecho.Value, exenta), operacion, liquidacion, c.Retencion,
            c.Politica, c.Perfil, corrida is not null,
            operacion.Kind == MemberAccountOperationKind.Cancellation ? EmisionDeCartera.ClaseCancelacionDeCuenta : EmisionDeCartera.ClaseLiquidacionDeIntereses,
            PrevalidationOutcome.NotApplicable)
        {
            CorridaPublicId = corrida?.PublicId,
        }, ct);
        if (unidad.IsFailure) return Result.Failure<LiquidacionAplicada>(unidad.Error);
        var delInteres = unidad.Value.De(InteresDeCaptacionLiquidadoV1.Type);
        var deLaRetencion = unidad.Value.De(RetencionPracticadaV1.Type);

        var filas = new List<MemberAccountTransaction>();
        if (c.Redondeo != 0m)
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.AccrualAdjustment, MemberAccountConcept.InterestPayable, c.Redondeo, c.Hasta,
                hecho.Value.FechaContable, delInteres));
        if (c.Bruto != 0m)
        {
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.InterestCredited, MemberAccountConcept.InterestPayable, -c.Bruto, c.Hasta,
                hecho.Value.FechaContable, delInteres));
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.InterestCredited, MemberAccountConcept.Principal, c.Bruto, c.Hasta,
                hecho.Value.FechaContable, delInteres));
        }
        // FR-063: la retención de RetencionSobreRendimientos se practica antes de capitalizar, en pareja (−capital, +retención).
        if (c.Retencion.SeRetiene)
        {
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Principal, -c.Retencion.Valor, c.Hasta,
                hecho.Value.FechaContable, deLaRetencion));
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Withholding, c.Retencion.Valor, c.Hasta,
                hecho.Value.FechaContable, deLaRetencion));
        }
        registro.RegistrarTransacciones(filas);
        registro.RegistrarLiquidacion(control, liquidacion, await AcumuladosAsync(db, cuenta, operacion, liquidacion, ct));
        await db.SaveChangesAsync(ct);
        return Result.Success(new LiquidacionAplicada(liquidacion, operacion, false, c));
    }

    /// <summary>El saldo inicial del interés causado no liquidado de una cuenta migrada en (<paramref name="desde"/>, <paramref name="corte"/>] (research E121).</summary>
    private async Task<decimal> InteresMigradoAsync(int cuentaId, DateOnly desde, DateOnly corte, CancellationToken ct) =>
        await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.InterestPayable && t.Kind == MemberAccountTransactionKind.OpeningBalance
                        && t.ValueDate > desde && t.ValueDate <= corte && !t.IsDeleted)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

    /// <summary>
    /// US9 (research E121, pendiente de validar por la contadora): el interés migrado repartido por igual entre los días (<paramref name="desde"/>,
    /// <paramref name="hasta"/>] a seis decimales, con el residuo en el último; así el umbral diario de la retención se mide por día y no sobre el total.
    /// </summary>
    public static IReadOnlyList<InteresDiarioDeRetencion> RepartirInteresMigrado(decimal monto, DateOnly desde, DateOnly hasta)
    {
        var dias = hasta.DayNumber - desde.DayNumber;
        if (monto == 0m || dias <= 0) return [];
        var porDia = Math.Round(monto / dias, MemberAccountTransaction.DecimalesDeCausacion, MidpointRounding.ToZero);
        var lista = new List<InteresDiarioDeRetencion>(dias);
        for (var i = 1; i <= dias; i++)
            lista.Add(new InteresDiarioDeRetencion(desde.AddDays(i), i == dias ? monto - porDia * (dias - 1) : porDia));
        return lista;
    }

    /// <summary>
    /// Los períodos vencidos sin liquidar de la cuenta a <paramref name="d"/> (FR-061): desde el siguiente a la última liquidación, cada período de la
    /// periodicidad de la versión que terminó a más tardar el día <paramref name="d"/> y la causación ya alcanzó.
    /// </summary>
    public async Task<IReadOnlyList<DateOnly>> PeriodosVencidosAsync(MemberAccount cuenta, MemberAccountControl control, DateOnly d, CancellationToken ct)
    {
        var versiones = await lector.VersionesAsync(cuenta.CreditLineId, ct);
        var fines = new List<DateOnly>();
        var inicio = InicioDeLiquidacion(cuenta, control);
        var tope = control.LastAccruedDate < d ? control.LastAccruedDate : d;
        while (true)
        {
            var version = LectorDeInteres.VersionDelDia(versiones, cuenta, inicio.AddDays(1), DateTime.MaxValue);
            if (version is null) break;
            var periodo = InteresDeCaptacion.PeriodoDeLiquidacion(version.Version.SettlementPeriodicity, version.Version.SettlementDay, inicio.AddDays(1));
            if (periodo is not { } p || p.Hasta > tope) break;
            fines.Add(p.Hasta);
            inicio = p.Hasta;
        }
        return fines;
    }

    /// <summary>El recaudador de las operaciones internas de la Cartera (la liquidación la hace la Cartera, no un recaudador).</summary>
    public const string RecaudadorPropio = "CAR";

    /// <summary>
    /// El reparto de la liquidación entre el titular y los cotitulares vigentes al fin del período por su proporción (data-model §4.6): al centavo, y el
    /// titular absorbe el residuo. Sin cotitulares, todo al titular.
    /// </summary>
    internal static Task<IReadOnlyList<WithholdingAccumulation>> AcumuladosAsync(IApplicationDbContext db, MemberAccount cuenta, MemberAccountOperation operacion, InterestSettlement l,
        CancellationToken ct) =>
        RepartoAsync(db, cuenta, operacion, l.PeriodTo, l.GrossInterest, l.InflationaryComponentAmount ?? 0m, l.WithholdingBase, l.WithholdingAmount, l,
            WithholdingAccumulation.OrigenLiquidacion, ct);

    /// <summary>
    /// El reparto de un rendimiento abonado (una liquidación o, US7, la bonificación del contractual) entre el titular y los cotitulares vigentes a
    /// <paramref name="hasta"/> por su proporción (data-model §4.6): al centavo, y el titular absorbe el residuo.
    /// </summary>
    internal static async Task<IReadOnlyList<WithholdingAccumulation>> RepartoAsync(IApplicationDbContext db, MemberAccount cuenta, MemberAccountOperation operacion,
        DateOnly hasta, decimal bruto, decimal componente, decimal baseDeRetencion, decimal retencion, InterestSettlement? l, string origen, CancellationToken ct)
    {
        var partes = await db.MemberAccountParties.AsNoTracking()
            .Where(p => p.MemberAccountId == cuenta.Id && !p.IsDeleted && p.Role == MemberAccountPartyRole.CoHolder && p.ValidFrom <= hasta
                        && (p.ValidTo == null || p.ValidTo >= hasta))
            .Select(p => new { p.PersonId, p.Share }).ToListAsync(ct);
        var proporciones = new List<(int Persona, decimal Proporcion)>();
        if (partes.Count > 0)
        {
            var conProporcion = partes.All(p => p.Share is not null);
            var cada = conProporcion ? 0m : decimal.Round(1m / (partes.Count + 1), 6, MidpointRounding.ToEven);
            foreach (var p in partes) proporciones.Add((p.PersonId, conProporcion ? p.Share!.Value : cada));
            proporciones.Insert(0, (cuenta.HolderPersonId, 1m - proporciones.Sum(x => x.Proporcion)));
        }
        else proporciones.Add((cuenta.HolderPersonId, 1m));

        var anio = (short)hasta.Year;
        var acumulados = new List<WithholdingAccumulation>();
        decimal restoBruto = bruto, restoComponente = componente, restoBase = baseDeRetencion, restoRetencion = retencion;
        for (var i = proporciones.Count - 1; i >= 0; i--)
        {
            var (persona, proporcion) = proporciones[i];
            var esElTitular = i == 0;
            decimal Parte(decimal total, ref decimal resto)
            {
                var valor = esElTitular ? resto : decimal.Round(total * proporcion, 2, MidpointRounding.AwayFromZero);
                resto -= valor;
                return valor;
            }
            acumulados.Add(new WithholdingAccumulation
            {
                PersonId = persona,
                TaxYear = anio,
                MemberAccountId = cuenta.Id,
                DepositClass = cuenta.DepositClass,
                InterestSettlement = l,
                Operation = operacion,
                OperationId = operacion.Id,
                Share = proporcion,
                GrossInterest = Parte(bruto, ref restoBruto),
                InflationaryComponentAmount = Parte(componente, ref restoComponente),
                WithholdingBase = Parte(baseDeRetencion, ref restoBase),
                WithholdingAmount = Parte(retencion, ref restoRetencion),
                Origin = origen,
            });
        }
        acumulados.Reverse();
        return acumulados;
    }

    private static string Huella(LiquidacionCalculada c) => new HuellaDeCartera()
        .Identificador("account", c.Cuenta.PublicId).Fecha("from", c.Desde).Fecha("to", c.Hasta).Decimal("accrued", c.Causado, 6).Decimal("gross", c.Bruto, 2)
        .Decimal("withholding", c.Retencion.Valor, 2).Texto("profile", c.Perfil.ToString()).Texto("rules", VersionesDeReglas.Vigente).Calcular();

    private static MemberAccountTransaction Fila(MemberAccount cuenta, MemberAccountOperation operacion, MemberAccountTransactionKind clase,
        MemberAccountConcept concepto, decimal monto, DateOnly fecha, DateOnly contable, Guid? mensaje) => new()
    {
        MemberAccountId = cuenta.Id,
        Operation = operacion,
        OperationId = operacion.Id == 0 ? null : operacion.Id,
        Kind = clase,
        Concept = concepto,
        Amount = monto,
        ValueDate = fecha,
        AccountingDate = contable,
        RegisteredAt = operacion.RegisteredAt,
        MessagePublicId = mensaje,
    };
}

/// <summary>
/// El subpaso «liquidación» del paso de captaciones (feature 014, US5, T132; R10; FR-061): después de la causación, liquida cada período vencido sin liquidar
/// de cada cuenta que gana interés, en orden, cada cuenta en su transacción con el cerrojo del proceso (salta la ocupada y la deja para la pasada
/// siguiente). Idempotente: un período ya liquidado no se repite. Lo que falla queda en <c>LND_ProcessRunErrors</c>. (nuevo)
/// </summary>
public sealed class LiquidacionPeriodicaDeIntereses(
    IApplicationDbContext db,
    LiquidacionDeIntereses liquidacion,
    ICerrojoDeCartera cerrojo,
    ILogger<LiquidacionPeriodicaDeIntereses>? logger = null,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "Liquidacion";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var clases = CausacionDeCaptaciones.ClasesQueLiquidan.ToArray();
        var estados = CausacionDeCaptaciones.EstadosQueCausan.ToArray();
        var candidatas = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Nature == LineNature.Deposit && a.DepositClass != null && clases.Contains(a.DepositClass.Value)
                        && estados.Contains(a.Status))
            .OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        // US8 (T142): la validación previa, sólo para las cuentas que liquidan hoy y una vez por combinación.
        var pregunta = validacionPrevia is null || candidatas.Count == 0
            ? null
            : await validacionPrevia.TandaAsync(candidatas, d, $"la liquidación de intereses del {d:yyyy-MM-dd}", ct);
        foreach (var id in candidatas)
        {
            try
            {
                await TransaccionExplicita.EjecutarAsync(db, async () =>
                {
                    await cerrojo.TomarSetupAsync(exclusivo: false, ct);
                    if ((await cerrojo.BloquearCuentasAsync([id], ModoDeEspera.Saltar, ct)).Count == 0) return 0;
                    var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == id, ct);
                    var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == id, ct);
                    var liquidadas = 0;
                    var vencidos = await liquidacion.PeriodosVencidosAsync(cuenta, control, d, ct);
                    if (vencidos.Count > 0 && pregunta is not null
                        && await pregunta.CuentaAsync(id, x => [HechosRepresentativosDeCaptaciones.Liquidacion(x), HechosRepresentativosDeCaptaciones.Retencion(x)], ct)
                            is { } bloqueada)
                        throw new ReglaDeCarteraException(new ProblemaDeCartera(bloqueada.Codigo, bloqueada.Mensaje));
                    foreach (var fin in vencidos)
                    {
                        var r = await liquidacion.LiquidarAsync(cuenta, control, fin, corrida, null, null, ct);
                        if (r.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(r.Error.Code, r.Error.Message));
                        liquidadas++;
                    }
                    await db.SaveChangesAsync(ct);
                    return liquidadas;
                }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.LiquidacionDeInteresesFallida] La cuenta {Cuenta} no liquidó sus intereses al {Dia}.", id, d);
                fallos.Add(new FalloDeCuenta(id, ex is ReglaDeCarteraException r ? r.Codigo : "Lending.Savings.SettlementFailed", ex.Message));
            }
        }
        return fallos;
    }
}

/// <summary>
/// La liquidación de una cuenta <b>a una fecha</b> que no es fin de período (feature 014, US5, T136; FR-076): la usa el retiro del asociado para sumar los
/// ahorros con sus intereses liquidados a la fecha. Liquida primero los períodos vencidos sin liquidar; después nace la operación <c>InterestSettlement</c>
/// (con el bruto que el motor da a la fecha), pone al día la causación en ella y liquida hasta la fecha. En la transacción y bajo el cerrojo de quien llama;
/// guarda. (nuevo)
/// </summary>
public sealed class LiquidacionALaFecha(
    IApplicationDbContext db,
    LiquidacionDeIntereses liquidacion,
    CausacionDeCaptaciones causacion,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public async Task<Result<LiquidacionAplicada?>> LiquidarAsync(MemberAccount cuenta, DateOnly fecha, string motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        if (!LiquidacionDeIntereses.GanaInteres(cuenta) || cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive))
            return Result.Success<LiquidacionAplicada?>(null);
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        if (control.LastSettledThrough is { } previo && previo >= fecha) return Result.Success<LiquidacionAplicada?>(null);
        foreach (var fin in (await liquidacion.PeriodosVencidosAsync(cuenta, control, fecha, ct)).Where(f => f < fecha))
        {
            var vencida = await liquidacion.LiquidarAsync(cuenta, control, fin, null, null, motivo, ct);
            if (vencida.IsFailure) return Result.Failure<LiquidacionAplicada?>(vencida.Error);
        }
        var simulada = await liquidacion.CalcularAsync(cuenta, control, fecha, DateTime.MaxValue, desdeElMotor: true, ct);
        if (simulada.IsFailure) return Result.Failure<LiquidacionAplicada?>(simulada.Error);
        if (simulada.Value.Causado == 0m && simulada.Value.Bruto == 0m)
        {
            // Sin interés no hay operación: la puesta al día sólo avanza la marca (no escribe filas ni partes) y la liquidación marca el período.
            if (control.LastAccruedDate < fecha)
            {
                var marca = await causacion.PonerAlDiaAsync(cuenta, fecha, new MemberAccountOperation { MemberAccountId = cuenta.Id, Number = "ALD" }, ct);
                if (marca.IsFailure) return Result.Failure<LiquidacionAplicada?>(marca.Error);
                if (marca.Value.Causado != 0m || marca.Value.Ajuste != 0m)
                    throw new InvalidOperationException("La puesta al día sin interés escribió filas: el motor y la simulación no coinciden.");
            }
            var nada = await liquidacion.LiquidarAsync(cuenta, control, fecha, null, null, motivo, ct);
            return nada.IsFailure ? Result.Failure<LiquidacionAplicada?>(nada.Error) : Result.Success<LiquidacionAplicada?>(nada.Value);
        }

        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<LiquidacionAplicada?>(hecho.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.InterestSettlement, LiquidacionDeIntereses.PrefijoLiquidacion, ct);
        var texto = motivo.Trim();
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.InterestSettlement,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = simulada.Value.Bruto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            Reason = texto.Length <= 500 ? texto : texto[..500],
            Payload = JsonSerializer.Serialize(new { account = cuenta.PublicId, date = fecha, atDate = true }),
            InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Fecha("date", fecha).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);
        var alDia = await causacion.PonerAlDiaAsync(cuenta, fecha, operacion, ct);
        if (alDia.IsFailure) return Result.Failure<LiquidacionAplicada?>(alDia.Error);
        await db.SaveChangesAsync(ct);
        var r = await liquidacion.LiquidarAsync(cuenta, control, fecha, null, operacion, motivo, ct);
        return r.IsFailure ? Result.Failure<LiquidacionAplicada?>(r.Error) : Result.Success<LiquidacionAplicada?>(r.Value);
    }
}
