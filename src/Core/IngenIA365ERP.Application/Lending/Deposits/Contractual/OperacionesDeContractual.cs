using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

/// <summary>Por qué un contractual queda incumplido (research §19): más atrasos que los tolerados, el vencimiento con cuotas sin pagar, o la cancelación anticipada. (nuevo)</summary>
public static class CausasDeIncumplimiento
{
    public const string Atrasos = "Arrears";
    public const string VencimientoConCuotasPendientes = "MaturityWithPendingInstallments";
    public const string CancelacionAnticipada = "EarlyTermination";
}

/// <summary>Lo que dejó el incumplimiento: la operación, la penalidad (la reliquidación a la tasa de castigo), la provisión reversada y la versión de castigo. (nuevo)</summary>
public sealed record IncumplimientoAplicado(MemberAccountOperation Operacion, decimal Penalidad, decimal ProvisionReversada, MemberAccountTermsVersion Version,
    DiferenciaDeTasasDelContractual? Reliquidacion);

/// <summary>
/// El incumplimiento de un ahorro contractual (feature 014, US7, T182, T183; FR-024, FR-074; research §19): el contrato vigente pasa a «incumplido» con el efecto
/// de su línea. En una operación <c>ContractualBreach</c> (<c>ICT-</c>):
/// <list type="number">
/// <item>la <b>reliquidación a la tasa de castigo</b> de los aportes hasta la fecha (<see cref="AhorroContractual.Reliquidar"/>): lo que la base les dio de más
/// sale primero del interés por pagar y el resto del capital (<c>PenaltyApplied</c>, hecho <c>PenalidadAplicada</c> con la clase <c>ContractualBreach</c>,
/// contra el ingreso por penalidad, como la redención anticipada del CDAT; pendiente de validar por la contadora);</item>
/// <item>la reversión de la bonificación provisionada, si la línea la provisiona (P26; <c>BonificacionReconocida</c> negativa);</item>
/// <item>la versión de condiciones de castigo (causa <c>ContractualBreach</c>): desde el día siguiente la cuenta causa a la tasa de castigo (o a la base si la
/// línea no la tiene);</item>
/// <item>el aviso al titular (<c>CONTRATINC</c>, informativo por la mensajería de la Cartera).</item>
/// </list>
/// El efecto (seguir a la tasa de castigo, pasar a la vista, cancelar y devolver) lo aplica quien llama con <see cref="CierreDeContractual"/>. La cuenta ya está
/// bloqueada; guarda. (nuevo)
/// </summary>
public sealed class IncumplimientoDeContractual(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    LectorDeContractual lector,
    IActorActual actorActual,
    IDateTimeService reloj,
    ColaDeMensajesAlDeudor? cola = null,
    ILogger<IncumplimientoDeContractual>? logger = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public const string PrefijoIncumplimiento = "ICT-";

    /// <summary>La plantilla del aviso al titular.</summary>
    public const string CodigoDePlantilla = "CONTRATINC";

    public async Task<Result<IncumplimientoAplicado>> AplicarAsync(MemberAccount cuenta, MemberAccountControl control, ContractualLeido leido, DateOnly fecha,
        string causa, int cuotasAtrasadas, bool porElProceso, PrevalidationOutcome validacion, string? motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        if (leido.Inicial is not { } inicial || leido.Vigente is not { } vigente) return Falla(ErroresDeContractual.SinCondiciones(cuenta.Number));
        if (cuenta.ContractualStatus != ContractualStatus.Current)
            return Falla(ErroresDeContractual.NoVigente(cuenta.Number, cuenta.Status, cuenta.ContractualStatus));
        var c = LectorDeContractual.Condiciones(inicial);
        // La reliquidación llega hasta la fecha o, si ya venció, hasta el vencimiento (después no causa).
        var vence = AhorroContractual.Vencimiento(c.Inicio, c.PlazoMeses);
        var corte = fecha < vence ? fecha : vence;
        var aportes = await lector.AportesAsync(leido, corte, ct);
        DiferenciaDeTasasDelContractual? reliquidacion = inicial.BreachAnnualRate is { } castigo && castigo < c.TasaBase
            ? AhorroContractual.Reliquidar(aportes, c.TasaBase, castigo, c.Convencion, corte, c.Redondeo)
            : null;
        var penalidad = reliquidacion?.Diferencia ?? 0m;
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var capital = Math.Max(0m, saldos.GetValueOrDefault(MemberAccountConcept.Principal));
        // Lo que la base dio de más sale del capital: el interés causado y todavía no liquidado lo abona entero la liquidación siguiente (que se cuenta
        // sobre lo causado, no sobre el saldo del concepto), así que descontarlo de ahí lo dejaría negativo y la entrega no vaciaría la cuenta.
        var delCapital = Math.Min(penalidad, capital);
        var provisionada = await lector.BonificacionProvisionadaAsync(cuenta.Id, ct);

        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Falla(hecho.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.ContractualBreach, PrefijoIncumplimiento, ct);
        var huella = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Fecha("date", fecha).Texto("cause", causa)
            .Decimal("penalty", penalidad, 2).Decimal("provisioned", provisionada, 2).Calcular();
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.ContractualBreach,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = delCapital,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            Reason = string.IsNullOrWhiteSpace(motivo) ? null : Recortar(motivo, 500),
            Payload = JsonSerializer.Serialize(new
            {
                cause = causa, lateInstallments = cuotasAtrasadas, baseRate = c.TasaBase, breachRate = inicial.BreachAnnualRate,
                interestAtBase = reliquidacion?.InteresBase, interestAtBreachRate = reliquidacion?.InteresOtraTasa, penalty = penalidad,
                fromPrincipal = delCapital, provisionReversed = provisionada,
            }, Json),
            InputsSha256 = huella,
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = validacion.ToString(),
        });
        await db.SaveChangesAsync(ct);

        var version = LectorDeContractual.VersionDeCastigo(cuenta, inicial, vigente, operacion, ahora, huella);
        registro.SellarCondiciones(control, version, [], vigente.VersionNumber);
        await db.SaveChangesAsync(ct);

        var penalidadValores = new List<DepositConceptAmountV1>();
        if (delCapital > 0m) penalidadValores.Add(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -delCapital });
        var unidad = await emision.HechosDeContractualAsync(new HechoDeContractualAEmitir(hecho.Value, operacion, validacion)
        {
            PorElProceso = porElProceso,
            Bonificacion = provisionada > 0m
                ? ([new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Bonus), Amount = -provisionada }], Contrato(inicial, cuotasAtrasadas, leido))
                : null,
            Penalidad = penalidadValores.Count > 0 ? (penalidadValores, reliquidacion?.InteresBase ?? 0m) : null,
        }, EmisionDeCartera.ClaseIncumplimientoDeContractual, ct);
        if (unidad.IsFailure) return Falla(unidad.Error);
        var mPen = unidad.Value.De(PenalidadAplicadaV1.Type);
        var mBon = unidad.Value.De(BonificacionReconocidaV1.Type);
        var contable = hecho.Value.FechaContable;
        var filas = new List<MemberAccountTransaction>();
        if (delCapital > 0m) filas.Add(Fila(cuenta.Id, operacion, MemberAccountTransactionKind.PenaltyApplied, MemberAccountConcept.Principal, -delCapital, fecha, contable, mPen, version.Id));
        if (provisionada > 0m)
            filas.Add(Fila(cuenta.Id, operacion, MemberAccountTransactionKind.BonusRecognized, MemberAccountConcept.Bonus, -provisionada, fecha, contable, mBon, version.Id));
        if (filas.Count > 0) registro.RegistrarTransacciones(filas);
        var seguida = db.MemberAccounts.Local.FirstOrDefault(a => a.Id == cuenta.Id) ?? cuenta;
        seguida.Incumplir();
        if (!ReferenceEquals(seguida, cuenta)) cuenta.ContractualStatus = seguida.ContractualStatus;
        await AvisarAsync(cuenta, fecha, cuotasAtrasadas, inicial.BreachEffect ?? ContractualBreachEffect.BreachRate, causa, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new IncumplimientoAplicado(operacion, delCapital, provisionada, version, reliquidacion));
    }

    /// <summary>El cumplimiento como viaja en <c>BonificacionReconocida</c> (mensajes.md §4.1).</summary>
    public static ContractualBonusV1 Contrato(MemberAccountTermsVersion inicial, int pagadas, ContractualLeido leido) => new()
    {
        Installments = inicial.InstallmentCount ?? 0, Paid = pagadas, BonusRateEa = inicial.BonusAnnualRate ?? 0m, BaseRateEa = inicial.AppliedAnnualRate,
    };

    /// <summary>El texto del efecto del incumplimiento para el aviso al titular.</summary>
    public static string TextoDelEfecto(ContractualBreachEffect efecto, string causa) => causa switch
    {
        CausasDeIncumplimiento.CancelacionAnticipada => "El contrato se canceló antes de su vencimiento: se reliquidó a la tasa de incumplimiento y se le entrega lo ahorrado.",
        CausasDeIncumplimiento.VencimientoConCuotasPendientes => "Venció con cuotas sin pagar: se liquidó a la tasa de incumplimiento, sin la bonificación.",
        _ => efecto switch
        {
            ContractualBreachEffect.MoveToDemand => "Según su línea, lo ahorrado pasa a su cuenta de ahorro a la vista, liquidado a la tasa de incumplimiento.",
            ContractualBreachEffect.Cancel => "Según su línea, el contrato se cancela y se le devuelve lo ahorrado, liquidado a la tasa de incumplimiento.",
            _ => "Según su línea, el contrato sigue hasta su vencimiento a la tasa de incumplimiento y pierde la bonificación.",
        },
    };

    /// <summary>Encola el aviso al titular sin guardar; lo que no se encola queda en el log (nunca tumba el incumplimiento).</summary>
    private async Task AvisarAsync(MemberAccount cuenta, DateOnly fecha, int atrasadas, ContractualBreachEffect efecto, string causa, CancellationToken ct)
    {
        if (cola is null) return;
        try
        {
            var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == cuenta.CreditLineId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
            var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(cuenta.HolderPersonId, null, DebtorMessagePurpose.Informative, MessageChannel.Email,
                CodigoDePlantilla, DebtorMessage.OrigenAhorroContractual, $"{cuenta.PublicId:N}:incumplimiento", ActorKind.Process, null)
            {
                OriginPublicId = cuenta.PublicId,
                Valores = new Dictionary<string, string?>
                {
                    ["NumeroDeCuenta"] = cuenta.Number,
                    ["NombreDeLaLinea"] = linea,
                    ["FechaDeIncumplimiento"] = fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    ["CuotasAtrasadasDelContrato"] = atrasadas.ToString(CultureInfo.InvariantCulture),
                    ["EfectoDelIncumplimiento"] = TextoDelEfecto(efecto, causa),
                },
            }, ct);
            if (r.IsFailure)
                _log.LogInformation("[Cartera.AvisoDeIncumplimientoDeContractual] No se encoló el aviso del contractual {Cuenta}: {Codigo} {Mensaje}", cuenta.Number,
                    r.Error.Code, r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.AvisoDeIncumplimientoDeContractual] El aviso del contractual {Cuenta} falló.", cuenta.Number);
        }
    }

    internal static MemberAccountTransaction Fila(int cuenta, MemberAccountOperation op, MemberAccountTransactionKind clase, MemberAccountConcept concepto, decimal monto,
        DateOnly valor, DateOnly contable, Guid? mensaje, int? terminos) => new()
    {
        MemberAccountId = cuenta, Operation = op, OperationId = op.Id == 0 ? null : op.Id, Kind = clase, Concept = concepto, Amount = monto, ValueDate = valor,
        AccountingDate = contable, RegisteredAt = op.RegisteredAt, MessagePublicId = mensaje, TermsVersionId = terminos,
    };

    internal static string Recortar(string texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }

    private static Result<IncumplimientoAplicado> Falla(Error e) => Result.Failure<IncumplimientoAplicado>(e);
}

/// <summary>Lo que dejó la bonificación: la operación (nula si no había nada que reconocer), lo reconocido, lo abonado y la retención. (nuevo)</summary>
public sealed record BonificacionAplicada(MemberAccountOperation? Operacion, decimal Reconocida, decimal Abonada, decimal Retencion,
    DiferenciaDeTasasDelContractual? Calculo);

/// <summary>
/// La bonificación del ahorro contractual (feature 014, US7, T183; FR-024, FR-075; P26; research §19):
/// <list type="bullet">
/// <item><b>Provisión</b> (<see cref="ProvisionarAsync"/>, con la línea en <c>Provisioned</c>): en cada vencimiento de cuota del contrato vigente, la bonificación
/// de los aportes hasta ese día (<see cref="AhorroContractual.Bonificacion"/>) menos lo ya provisionado va al concepto <c>Bonus</c> (interés por pagar) contra el
/// gasto por bonificación, en una operación <c>ContractualBonus</c> (<c>BCT-</c>, hecho <c>BonificacionReconocida</c>). Si incumple, la reversa el
/// incumplimiento.</item>
/// <item><b>Abono al cumplirse</b> (<see cref="AbonarAsync"/>): la bonificación de los aportes al vencimiento —lo provisionado y la diferencia— se abona al
/// capital en una operación <c>ContractualMaturity</c> (<c>VCC-</c>), con la retención en la fuente sobre ella (FR-063: es rendimiento; el umbral diario se
/// mide con la bonificación repartida por igual entre los días del contrato, pendiente de validar por la contadora) y sus acumulados por titular para el
/// certificado.</item>
/// </list>
/// La cuenta ya está bloqueada; guarda. (nuevo)
/// </summary>
public sealed class BonificacionDeContractual(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    LectorDeContractual lector,
    ReglasDeRetencion retencion,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string PrefijoProvision = "BCT-";
    public const string PrefijoVencimiento = "VCC-";

    /// <summary>Provisiona la bonificación hasta <paramref name="fecha"/> (ver la clase). Sin bonificación en el contrato o sin diferencia, no hace nada.</summary>
    public async Task<Result<BonificacionAplicada>> ProvisionarAsync(MemberAccount cuenta, MemberAccountControl control, ContractualLeido leido, DateOnly fecha,
        bool porElProceso, CancellationToken ct)
    {
        if (leido.Inicial is not { BonusAnnualRate: { } bonificada } inicial) return Result.Success(new BonificacionAplicada(null, 0m, 0m, 0m, null));
        var c = LectorDeContractual.Condiciones(inicial);
        var calculo = AhorroContractual.Bonificacion(await lector.AportesAsync(leido, fecha, ct), c.TasaBase, bonificada, c.Convencion, fecha, c.Redondeo);
        var provisionada = await lector.BonificacionProvisionadaAsync(cuenta.Id, ct);
        var ajuste = calculo.Diferencia - provisionada;
        if (ajuste == 0m) return Result.Success(new BonificacionAplicada(null, 0m, 0m, 0m, calculo));

        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<BonificacionAplicada>(hecho.Error);
        var operacion = await OperacionAsync(cuenta, control, MemberAccountOperationKind.ContractualBonus, PrefijoProvision, fecha, hecho.Value.FechaContable,
            ajuste, new { provisionedBefore = provisionada, bonusToDate = calculo.Diferencia, adjustment = ajuste }, ct);
        var unidad = await emision.HechosDeContractualAsync(new HechoDeContractualAEmitir(hecho.Value, operacion, PrevalidationOutcome.NotApplicable)
        {
            PorElProceso = porElProceso,
            Bonificacion = ([new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Bonus), Amount = ajuste }],
                IncumplimientoDeContractual.Contrato(inicial, 0, leido)),
        }, EmisionDeCartera.ClaseBonificacionDeContractual, ct);
        if (unidad.IsFailure) return Result.Failure<BonificacionAplicada>(unidad.Error);
        registro.RegistrarTransacciones([IncumplimientoDeContractual.Fila(cuenta.Id, operacion, MemberAccountTransactionKind.BonusRecognized, MemberAccountConcept.Bonus,
            ajuste, fecha, hecho.Value.FechaContable, unidad.Value.De(BonificacionReconocidaV1.Type), leido.Vigente?.Id)]);
        await db.SaveChangesAsync(ct);
        return Result.Success(new BonificacionAplicada(operacion, ajuste, 0m, 0m, calculo));
    }

    /// <summary>
    /// Abona la bonificación del contrato cumplido a <paramref name="fecha"/> (ver la clase): la de los aportes al vencimiento, menos lo provisionado que se
    /// abona, con su retención. Sin tasa bonificada o sin diferencia, sólo abona lo provisionado (si lo hay).
    /// </summary>
    public async Task<Result<BonificacionAplicada>> AbonarAsync(MemberAccount cuenta, MemberAccountControl control, ContractualLeido leido, DateOnly fecha,
        int pagadas, bool porElProceso, PrevalidationOutcome validacion, CancellationToken ct)
    {
        if (leido.Inicial is not { } inicial) return Result.Failure<BonificacionAplicada>(ErroresDeContractual.SinCondiciones(cuenta.Number));
        var c = LectorDeContractual.Condiciones(inicial);
        var plan = LectorDeContractual.Plan(inicial);
        DiferenciaDeTasasDelContractual? calculo = inicial.BonusAnnualRate is { } bonificada
            ? AhorroContractual.Bonificacion(await lector.AportesAsync(leido, plan.Vencimiento, ct), c.TasaBase, bonificada, c.Convencion, plan.Vencimiento,
                c.Redondeo)
            : null;
        var bonificacion = calculo?.Diferencia ?? 0m;
        var provisionada = await lector.BonificacionProvisionadaAsync(cuenta.Id, ct);
        if (bonificacion == 0m && provisionada == 0m) return Result.Success(new BonificacionAplicada(null, 0m, 0m, 0m, calculo));
        // Lo provisionado de más (una provisión hecha con aportes que después se reversaron) se devuelve al gasto: lo que se abona es la bonificación final.
        var ajuste = bonificacion - provisionada;

        var linea = await db.DepositLineVersions.AsNoTracking().FirstAsync(v => v.Id == inicial.DepositLineVersionId, ct);
        var politica = await retencion.PoliticaAsync(linea, fecha, ct);
        if (politica.IsFailure) return Result.Failure<BonificacionAplicada>(politica.Error);
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        var (perfil, natural) = ReglasDeRetencion.PerfilDe(titular);
        var diarios = await DiariosAsync(cuenta.Id, c.Inicio, plan.Vencimiento, bonificacion, ct);
        var r = RetencionSobreRendimientos.Calcular(politica.Value, new EntradaDeRetencion(perfil, natural, DepositClass.Contractual, bonificacion, diarios));

        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<BonificacionAplicada>(hecho.Error);
        var operacion = await OperacionAsync(cuenta, control, MemberAccountOperationKind.ContractualMaturity, PrefijoVencimiento, fecha, hecho.Value.FechaContable,
            bonificacion, new
            {
                contributions = calculo?.Aportes, interestAtBase = calculo?.InteresBase, interestAtBonusRate = calculo?.InteresOtraTasa, bonus = bonificacion,
                provisioned = provisionada, withholding = r.Valor,
            }, ct, validacion);

        var valores = new List<DepositConceptAmountV1>();
        if (provisionada != 0m) valores.Add(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Bonus), Amount = -provisionada });
        if (bonificacion != 0m) valores.Add(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = bonificacion });
        var unidad = await emision.HechosDeContractualAsync(new HechoDeContractualAEmitir(hecho.Value, operacion, validacion)
        {
            PorElProceso = porElProceso,
            Bonificacion = (valores, IncumplimientoDeContractual.Contrato(inicial, pagadas, leido)),
            Retencion = bonificacion > 0m ? new RetencionDeCdat(r, politica.Value, perfil, fecha.Year) : null,
        }, EmisionDeCartera.ClaseVencimientoDeContractual, ct);
        if (unidad.IsFailure) return Result.Failure<BonificacionAplicada>(unidad.Error);
        var mBon = unidad.Value.De(BonificacionReconocidaV1.Type);
        var mRet = unidad.Value.De(RetencionPracticadaV1.Type);
        var contable = hecho.Value.FechaContable;
        var v = leido.Vigente?.Id;
        var filas = new List<MemberAccountTransaction>();
        if (ajuste != 0m)
            filas.Add(IncumplimientoDeContractual.Fila(cuenta.Id, operacion, MemberAccountTransactionKind.BonusRecognized, MemberAccountConcept.Bonus, ajuste, fecha,
                contable, mBon, v));
        if (bonificacion != 0m)
        {
            filas.Add(IncumplimientoDeContractual.Fila(cuenta.Id, operacion, MemberAccountTransactionKind.BonusRecognized, MemberAccountConcept.Bonus, -bonificacion,
                fecha, contable, mBon, v));
            filas.Add(IncumplimientoDeContractual.Fila(cuenta.Id, operacion, MemberAccountTransactionKind.BonusRecognized, MemberAccountConcept.Principal, bonificacion,
                fecha, contable, mBon, v));
        }
        // FR-063: la retención sobre la bonificación que se abona, de su motor y en pareja (−capital, +retención).
        if (r.SeRetiene && bonificacion > 0m)
        {
            filas.Add(IncumplimientoDeContractual.Fila(cuenta.Id, operacion, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Principal, -r.Valor,
                fecha, contable, mRet, v));
            filas.Add(IncumplimientoDeContractual.Fila(cuenta.Id, operacion, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Withholding, r.Valor,
                fecha, contable, mRet, v));
        }
        if (filas.Count > 0) registro.RegistrarTransacciones(filas);
        if (bonificacion > 0m)
            registro.RegistrarAcumuladosDeBonificacion(await LiquidacionDeIntereses.RepartoAsync(db, cuenta, operacion, fecha, bonificacion,
                r.ComponenteInflacionario is null ? 0m : r.ComponenteInflacionarioValor, r.SeRetiene ? r.BaseGravable : 0m, r.Valor, null,
                WithholdingAccumulation.OrigenBonificacion, ct));
        await db.SaveChangesAsync(ct);
        return Result.Success(new BonificacionAplicada(operacion, ajuste, bonificacion, r.Valor, calculo));
    }

    /// <summary>
    /// El interés de cada día del contrato para medir el umbral diario de la retención: lo causado en el libro más la bonificación repartida por igual entre
    /// los días del contrato (research §19; pendiente de validar por la contadora).
    /// </summary>
    private async Task<IReadOnlyList<InteresDiarioDeRetencion>> DiariosAsync(int cuentaId, DateOnly inicio, DateOnly vencimiento, decimal bonificacion,
        CancellationToken ct)
    {
        var clases = LectorDeInteres.ClasesDeLoCausado.ToArray();
        var delLibro = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind) && t.ValueDate > inicio
                        && t.ValueDate <= vencimiento && !t.IsDeleted)
            .GroupBy(t => t.ValueDate).Select(g => new { Dia = g.Key, Monto = g.Sum(t => t.Amount) }).ToListAsync(ct);
        return delLibro.Select(x => new InteresDiarioDeRetencion(x.Dia, x.Monto))
            .Concat(LiquidacionDeIntereses.RepartirInteresMigrado(bonificacion, inicio, vencimiento))
            .GroupBy(d => d.Dia).OrderBy(g => g.Key).Select(g => new InteresDiarioDeRetencion(g.Key, g.Sum(d => d.Interes))).ToList();
    }

    private async Task<MemberAccountOperation> OperacionAsync(MemberAccount cuenta, MemberAccountControl control, MemberAccountOperationKind clase, string prefijo,
        DateOnly fecha, DateOnly contable, decimal monto, object payload, CancellationToken ct, PrevalidationOutcome validacion = PrevalidationOutcome.NotApplicable)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(clase, prefijo, ct);
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = clase,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = contable,
            RegisteredAt = sello.RegisteredAt,
            Amount = Math.Abs(monto),
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            Payload = JsonSerializer.Serialize(payload, Json),
            InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Texto("kind", clase.ToString()).Fecha("date", fecha)
                .Decimal("amount", monto, 2).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = validacion.ToString(),
        });
        await db.SaveChangesAsync(ct);
        return operacion;
    }
}

/// <summary>A dónde va lo ahorrado al terminar el contrato: una cuenta de ahorro (con seguimiento) o un giro a una persona con su cuenta bancaria. (nuevo)</summary>
public sealed record EntregaDeContractual(CloseDeliveryKind Kind, MemberAccount? Cuenta, int? Persona, BeneficiarioDeOrden? Banco);

/// <summary>Lo que dejó el cierre del contrato: lo entregado, la operación de la entrega, el estado de la cuenta y qué pasó. (nuevo)</summary>
public sealed record CierreDeContractualAplicado(decimal Entregado, MemberAccountOperationResultDto? Entrega, MemberAccountStatus Estado, string Resultado);

/// <summary>
/// El fin de un ahorro contractual (feature 014, US7, T182; FR-075; research §19): al vencer —cumplido o incumplido—, al incumplir con un efecto que lo termina
/// (pasar a la vista, cancelar) o al cancelarlo el titular antes de tiempo:
/// <list type="number">
/// <item>liquida los intereses a la fecha (<see cref="LiquidacionALaFecha"/>: los períodos vencidos y el último hasta la fecha, con su retención);</item>
/// <item>anula las cuotas exigibles que quedaron pendientes (ya no se exigen);</item>
/// <item>entrega todo el saldo con el GMF de la cancelación: por traslado a la cuenta de ahorro (se cancela en el acto) o por orden de giro (queda en cierre
/// hasta que Tesorería gira); sin destino, la cuenta queda <b>vencida, a disposición</b> del titular, que la cancela después por la pantalla.</item>
/// </list>
/// La cuenta y su destino ya están bloqueados; guarda. (nuevo)
/// </summary>
public sealed class CierreDeContractual(
    IApplicationDbContext db,
    LiquidacionALaFecha liquidacion,
    CancelacionDeCuenta cancelacion,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    TrasladoDeCuenta traslados,
    OrdenesDeGiroDeCuentas ordenes,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public const string ResultadoTrasladado = "Transferred";
    public const string ResultadoGirado = "PayoutOrdered";
    public const string ResultadoADisposicion = "AtDisposal";

    /// <summary>
    /// El destino al terminar (research §19): el que pidió el titular (<c>MaturityInstructionJson</c>) o el sellado; con el efecto «pasar a la vista», la cuenta a
    /// la vista de la instrucción o, sin ella, la primera cuenta de ahorro a la vista viva del titular. Nulo = a disposición.
    /// </summary>
    public async Task<EntregaDeContractual?> DestinoAsync(MemberAccount cuenta, MemberAccountTermsVersion vigente, bool aLaVista, CancellationToken ct)
    {
        var pedida = InstruccionDeContractual.Leer(cuenta.MaturityInstructionJson);
        MemberAccount? destino = null;
        if (pedida is { Kind: CloseDeliveryKind.TransferToAccount, TargetAccountPublicId: { } id })
            destino = await db.MemberAccounts.FirstOrDefaultAsync(a => a.PublicId == id && !a.IsDeleted, ct);
        else if (pedida is null && vigente.DestinationAccountId is { } sellada)
            destino = await db.MemberAccounts.FirstOrDefaultAsync(a => a.Id == sellada, ct);
        if (destino is { Viva: true, Status: MemberAccountStatus.Active or MemberAccountStatus.Inactive })
            return new EntregaDeContractual(CloseDeliveryKind.TransferToAccount, destino, null, null);
        if (aLaVista)
        {
            var vista = await db.MemberAccounts
                .Where(a => a.HolderPersonId == cuenta.HolderPersonId && a.Nature == LineNature.Deposit && a.DepositClass == DepositClass.Demand && !a.IsDeleted
                            && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
                .OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
            return vista is null ? null : new EntregaDeContractual(CloseDeliveryKind.TransferToAccount, vista, null, null);
        }
        if (pedida is { Kind: CloseDeliveryKind.DisbursementOrder })
        {
            var persona = pedida.PayeePersonPublicId is { } p
                ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? cuenta.HolderPersonId
                : cuenta.HolderPersonId;
            var banco = await ordenes.BeneficiarioAsync(persona, pedida.BankPublicId, pedida.AccountType, pedida.AccountNumber, ct);
            return banco.IsSuccess ? new EntregaDeContractual(CloseDeliveryKind.DisbursementOrder, null, persona, banco.Value) : null;
        }
        if (pedida is null && vigente.MaturityInstruction == MaturityInstruction.CancelAndPay && vigente.DestinationPersonId is { } giro)
        {
            var banco = vigente.DestinationBankId is { } b
                ? new BeneficiarioDeOrden(b, await db.Banks.AsNoTracking().Where(x => x.Id == b).Select(x => x.TransferCode).FirstOrDefaultAsync(ct),
                    vigente.DestinationAccountType, vigente.DestinationBankAccountNumber)
                : (await ordenes.BeneficiarioAsync(giro, null, null, null, ct)).Value;
            return new EntregaDeContractual(CloseDeliveryKind.DisbursementOrder, null, giro, banco);
        }
        return null;
    }

    /// <summary>Liquida a la fecha, anula las cuotas pendientes y entrega o deja a disposición (ver la clase).</summary>
    public async Task<Result<CierreDeContractualAplicado>> TerminarAsync(MemberAccount cuenta, DateOnly fecha, EntregaDeContractual? entrega, string motivo,
        IReadOnlyList<Guid> firmas, bool exigeFirmas, long? operacionQueTermina, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var liquidada = await liquidacion.LiquidarAsync(cuenta, fecha, motivo, ct);
        if (liquidada.IsFailure) return Falla(liquidada.Error);

        var pendientes = await db.AccountInstallments.Where(c => c.MemberAccountId == cuenta.Id && c.CancelledAt == null && !c.IsDeleted).ToListAsync(ct);
        var leidas = await Contributions.LectorDeCuotas.DeCuentasAsync(db, [cuenta.Id], ct);
        var sinPagar = pendientes.Where(c => leidas.FirstOrDefault(l => l.Cuota.Id == c.Id) is { Pendiente: > 0m }).ToList();
        RegistroDeCuentas.AnularCuotas(sinPagar, reloj.UtcNow, $"Terminó el contrato de ahorro {cuenta.Number}: {IncumplimientoDeContractual.Recortar(motivo, 200)}",
            operacionQueTermina);
        await db.SaveChangesAsync(ct);

        var ahora = reloj.UtcNow;
        var actor = await actorActual.ObtenerAsync(ct);
        if (entrega is null)
        {
            if (cuenta.Status == MemberAccountStatus.Inactive) cuenta.Reactivar(ahora);
            if (cuenta.Status == MemberAccountStatus.Active) cuenta.Vencer(fecha, ahora);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CierreDeContractualAplicado(0m, null, cuenta.Status, ResultadoADisposicion));
        }

        var leida = await movimientos.LeerAsync(cuenta, fecha, ct);
        var saldo = leida.Saldo - leida.Disponible.Retenido - leida.Disponible.Embargado - leida.Disponible.EnCanje;
        var (monto, _) = await cancelacion.EntregaAsync(leida, saldo, fecha, ct, entrega.Kind == CloseDeliveryKind.TransferToAccount && entrega.Cuenta is { } otra
            ? HechosGeneradoresDeGmf.DeTraslado(cuenta.DepositClass, otra.DepositClass, cuenta.HolderPersonId == otra.HolderPersonId)
            : null);
        MemberAccountOperationResultDto? resultado = null;
        if (monto > 0m)
        {
            if (entrega.Kind == CloseDeliveryKind.TransferToAccount)
            {
                var t = await traslados.AplicarAsync(Pantalla.Traslado(cuenta.PublicId, entrega.Cuenta!.PublicId, monto, fecha, firmas, null, motivo)
                    with { EsCancelacion = true, ExigeFirmas = exigeFirmas }, ct);
                if (t.IsFailure) return Falla(t.Error);
                resultado = new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, t.Value.Comprobante, null, null, null);
            }
            else
            {
                var r = await retiros.AplicarAsync(Pantalla.Retiro(cuenta.PublicId, monto, fecha, ClaseDeMedioDeRecaudo.Transfer, firmas, null, motivo,
                    new OrdenDeRetiro(entrega.Persona ?? cuenta.HolderPersonId, entrega.Banco ?? new BeneficiarioDeOrden(null, null, null, null))) with
                {
                    EsCancelacion = true, ExigeAtribucion = false, ExigeFirmas = exigeFirmas, HechoGenerador = HechosGeneradoresDeGmf.Cancelacion,
                }, ct);
                if (r.IsFailure) return Falla(r.Error);
                var orden = r.Value.Orden;
                resultado = new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, r.Value.Comprobante,
                    orden is null ? null : new MemberAccountOrderDto(orden.PublicId, orden.Number, orden.Status.ToString()), null, null);
            }
        }

        var final = await movimientos.LeerAsync(cuenta, fecha, ct);
        var enGiro = entrega.Kind == CloseDeliveryKind.DisbursementOrder && monto > 0m;
        if (cuenta.Status != MemberAccountStatus.Closing) cuenta.PedirCierre(ahora);
        if (!enGiro && final.Saldo == 0m) cuenta.Cerrar(fecha, actor.UserId, IncumplimientoDeContractual.Recortar(motivo, 300), ahora);
        cuenta.MaturityInstructionJson = null;
        await db.SaveChangesAsync(ct);
        return Result.Success(new CierreDeContractualAplicado(monto, resultado, cuenta.Status, enGiro ? ResultadoGirado : ResultadoTrasladado));
    }

    private static Result<CierreDeContractualAplicado> Falla(Error e) => Result.Failure<CierreDeContractualAplicado>(e);
}
