using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

/// <summary>Lo que hizo el ciclo con un contractual el día d (para el registro y las pruebas). (nuevo)</summary>
public sealed record CicloDeUnContractual(int CuentaId, string? Resultado, decimal Penalidad = 0m, decimal Bonificacion = 0m);

/// <summary>
/// El subpaso «contractual» del paso de captaciones (feature 014, US7, T182; research §19; FR-074, FR-075; US7-1, US7-2): corre <b>después</b> de las cuotas, la
/// causación, la liquidación y el CDAT, cuenta por cuenta en su transacción con el cerrojo del proceso (salta la ocupada y la deja para la pasada siguiente).
/// Por cada ahorro contractual activo y el día d:
/// <list type="number">
/// <item><b>Vencimiento</b> (d ≥ el vencimiento del contrato y, si quedan cuotas sin pagar, pasados los días de gracia de la última —la nómina o la pagaduría
/// descuentan unos días después—): con todas las cuotas pagadas se <b>cumple</b> y se abona la bonificación (<see cref="BonificacionDeContractual"/>); con
/// cuotas sin pagar queda <b>incumplido</b> y se reliquida a la tasa de castigo. Después se liquida a la fecha y se entrega según la instrucción (traslado a la
/// cuenta a la vista o giro); sin instrucción queda vencido, a disposición (<see cref="CierreDeContractual"/>).</item>
/// <item><b>Incumplimiento</b> antes del vencimiento: más cuotas atrasadas que las toleradas (vencidas hace más de los días de gracia de
/// <c>Captaciones.DiasDeGraciaDeCuotaContractual</c>) lo dejan incumplido con el aviso al titular (<see cref="IncumplimientoDeContractual"/>) y el efecto de la
/// línea: <c>BreachRate</c> sigue hasta su vencimiento a la tasa de castigo; <c>MoveToDemand</c> pasa lo ahorrado a la cuenta a la vista del titular;
/// <c>Cancel</c> lo cancela y lo entrega según la instrucción.</item>
/// <item><b>Provisión</b> de la bonificación (P26, con la línea en <c>Provisioned</c>): el día del vencimiento de cada cuota.</item>
/// <item>Un contrato incumplido que siguió a la tasa de castigo, al vencer: se liquida y se entrega igual, sin bonificación.</item>
/// </list>
/// Con <c>Contabilidad.FaltaDeRegla = Bloquear</c> la cuenta no avanza si sus hechos no se contabilizarían (A8, E108). Lo que falla queda en
/// <c>LND_ProcessRunErrors</c>. (nuevo)
/// </summary>
public sealed class CicloDelContractual(
    IApplicationDbContext db,
    LectorDeContractual lector,
    IncumplimientoDeContractual incumplimiento,
    BonificacionDeContractual bonificacion,
    CierreDeContractual cierre,
    ICerrojoDeCartera cerrojo,
    ILogger<CicloDelContractual>? logger = null,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public const string Cumplido = "Fulfilled";
    public const string Incumplido = "Breached";
    public const string Provisionado = "BonusProvisioned";

    public string Nombre => "Contractual";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var candidatas = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Nature == LineNature.Deposit && a.DepositClass == DepositClass.Contractual && !a.IsDeleted
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive)
                        && (a.ContractualStatus == ContractualStatus.Current || a.ContractualStatus == ContractualStatus.Breached))
            .OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        var pregunta = validacionPrevia is null || candidatas.Count == 0
            ? null
            : await validacionPrevia.TandaAsync(candidatas, d, $"el vencimiento y el incumplimiento de los ahorros contractuales del {d:yyyy-MM-dd}", ct);
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
                    await ProcesarCuentaAsync(cuenta, control, d, pregunta, ct);
                    await db.SaveChangesAsync(ct);
                    return 1;
                }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.CicloDelContractualFallido] El contractual {Cuenta} no completó su ciclo del {Dia}.", id, d);
                fallos.Add(new FalloDeCuenta(id, ex is ReglaDeCarteraException r ? r.Codigo : "Lending.Contractual.CycleFailed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>El ciclo de un contractual ya bloqueado el día <paramref name="d"/> (ver la clase). Guarda lo que hace.</summary>
    public async Task<CicloDeUnContractual> ProcesarCuentaAsync(MemberAccount cuenta, MemberAccountControl control, DateOnly d, PreguntaDeTanda? pregunta,
        CancellationToken ct)
    {
        var leido = await lector.LeerAsync(cuenta, ct);
        if (leido.Inicial is not { } inicial || leido.Vigente is not { } vigente) return new CicloDeUnContractual(cuenta.Id, null);
        var plan = LectorDeContractual.Plan(inicial);
        var cuotas = await lector.CuotasAsync(cuenta, plan, ct);
        var cumplimiento = await lector.CumplimientoAsync(cuenta, inicial, plan, cuotas, d, ct);
        var gracia = await lector.DiasDeGraciaAsync(cuenta.CreditLineId, d, ct);
        var efecto = inicial.BreachEffect ?? ContractualBreachEffect.BreachRate;

        if (cuenta.ContractualStatus == ContractualStatus.Breached)
        {
            // Incumplido a tasa de castigo: al vencer se liquida y se entrega, sin bonificación.
            if (d < plan.Vencimiento) return new CicloDeUnContractual(cuenta.Id, null);
            await PreguntarAsync(pregunta, cuenta.Id, x => [HechosRepresentativosDeCaptaciones.Liquidacion(x), HechosRepresentativosDeCaptaciones.Retencion(x)], ct);
            return new CicloDeUnContractual(cuenta.Id, await TerminarAsync(cuenta, vigente, d, aLaVista: false, null, ct));
        }

        var vencido = d >= plan.Vencimiento;
        if (vencido && (cumplimiento.Completo || d > plan.Vencimiento.AddDays(gracia)))
        {
            if (cumplimiento.Completo)
            {
                await PreguntarAsync(pregunta, cuenta.Id, x => [HechosRepresentativosDeCaptaciones.Bonificacion(x), HechosRepresentativosDeCaptaciones.RetencionDeBonificacion(x)],
                    ct);
                cuenta.Cumplir();
                var abonada = await bonificacion.AbonarAsync(cuenta, control, leido, d, cumplimiento.Pagadas, porElProceso: true, PrevalidationOutcome.NotApplicable, ct);
                if (abonada.IsFailure) throw Excepcion(abonada.Error);
                var resultado = await TerminarAsync(cuenta, vigente, d, aLaVista: false, abonada.Value.Operacion?.Id, ct);
                return new CicloDeUnContractual(cuenta.Id, $"{Cumplido}:{resultado}", 0m, abonada.Value.Abonada);
            }
            // Venció con cuotas sin pagar: incumplido, reliquidado a la tasa de castigo y entregado.
            await PreguntarAsync(pregunta, cuenta.Id, x => [HechosRepresentativosDeCaptaciones.PenalidadDeContractual(x)], ct);
            var aplicado = await incumplimiento.AplicarAsync(cuenta, control, leido, d, CausasDeIncumplimiento.VencimientoConCuotasPendientes,
                cumplimiento.Generadas - cumplimiento.Pagadas, porElProceso: true, PrevalidationOutcome.NotApplicable, null, ct);
            if (aplicado.IsFailure) throw Excepcion(aplicado.Error);
            var leidoDespues = await lector.LeerAsync(cuenta, ct);
            var fin = await TerminarAsync(cuenta, leidoDespues.Vigente!, d, aLaVista: false, aplicado.Value.Operacion.Id, ct);
            return new CicloDeUnContractual(cuenta.Id, $"{Incumplido}:{fin}", aplicado.Value.Penalidad);
        }
        if (vencido) return new CicloDeUnContractual(cuenta.Id, null);

        if (cumplimiento.SuperaLaTolerancia)
        {
            await PreguntarAsync(pregunta, cuenta.Id, x => [HechosRepresentativosDeCaptaciones.PenalidadDeContractual(x)], ct);
            var aplicado = await incumplimiento.AplicarAsync(cuenta, control, leido, d, CausasDeIncumplimiento.Atrasos, cumplimiento.Atrasadas, porElProceso: true,
                PrevalidationOutcome.NotApplicable, null, ct);
            if (aplicado.IsFailure) throw Excepcion(aplicado.Error);
            if (efecto == ContractualBreachEffect.BreachRate) return new CicloDeUnContractual(cuenta.Id, Incumplido, aplicado.Value.Penalidad);
            var leidoDespues = await lector.LeerAsync(cuenta, ct);
            var fin = await TerminarAsync(cuenta, leidoDespues.Vigente!, d, aLaVista: efecto == ContractualBreachEffect.MoveToDemand, aplicado.Value.Operacion.Id, ct);
            return new CicloDeUnContractual(cuenta.Id, $"{Incumplido}:{fin}", aplicado.Value.Penalidad);
        }

        // P26: la provisión de la bonificación el día del vencimiento de cada cuota.
        if (inicial.BonusRecognition == BonusRecognition.Provisioned && inicial.BonusAnnualRate is not null && plan.CuotaQueVenceEl(d) is not null)
        {
            await PreguntarAsync(pregunta, cuenta.Id, x => [HechosRepresentativosDeCaptaciones.ProvisionDeBonificacion(x)], ct);
            var provision = await bonificacion.ProvisionarAsync(cuenta, control, leido, d, porElProceso: true, ct);
            if (provision.IsFailure) throw Excepcion(provision.Error);
            return new CicloDeUnContractual(cuenta.Id, provision.Value.Operacion is null ? null : Provisionado, 0m, provision.Value.Reconocida);
        }
        return new CicloDeUnContractual(cuenta.Id, null);
    }

    /// <summary>Liquida, anula lo pendiente y entrega a su destino (bloqueando la cuenta destino) o deja a disposición.</summary>
    private async Task<string> TerminarAsync(MemberAccount cuenta, MemberAccountTermsVersion vigente, DateOnly d, bool aLaVista, long? operacion,
        CancellationToken ct)
    {
        var entrega = await cierre.DestinoAsync(cuenta, vigente, aLaVista, ct);
        if (entrega?.Cuenta is { } otra) await cerrojo.BloquearCuentasAsync(new[] { cuenta.Id, otra.Id }.Order().ToList(), ModoDeEspera.Esperar, ct);
        var r = await cierre.TerminarAsync(cuenta, d, entrega, $"Vencimiento del ahorro contractual {cuenta.Number}", [], exigeFirmas: false, operacion, ct);
        if (r.IsFailure) throw Excepcion(r.Error);
        return r.Value.Resultado;
    }

    private static async Task PreguntarAsync(PreguntaDeTanda? pregunta, int cuenta,
        Func<HechosRepresentativosDeCaptaciones.CuentaRepresentativa, IReadOnlyList<object>> contenidos, CancellationToken ct)
    {
        if (pregunta is not null && await pregunta.CuentaAsync(cuenta, contenidos, ct) is { } bloqueada)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(bloqueada.Codigo, bloqueada.Mensaje));
    }

    private static ReglaDeCarteraException Excepcion(IngenIA365ERP.Application.Common.Models.Error e) => new(new ProblemaDeCartera(e.Code, e.Message));
}
