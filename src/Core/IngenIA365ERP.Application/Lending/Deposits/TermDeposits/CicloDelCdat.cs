using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
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

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>Lo que hizo el ciclo con un CDAT el día d: los períodos que pagó y lo que pasó al vencimiento. (nuevo)</summary>
public sealed record CicloDeUnCdat(int CuentaId, int PeriodosPagados, string? AlVencimiento);

/// <summary>
/// El subpaso «CDAT» del paso de captaciones (feature 014, US6, T172; research R10, R11; FR-069, FR-070, FR-072, FR-073; D17, D19): corre <b>después</b> de la
/// causación y de la liquidación periódica, cuenta por cuenta en su transacción con el cerrojo del proceso (salta la ocupada y la deja para la pasada
/// siguiente). Por cada CDAT activo y el día d:
/// <list type="number">
/// <item>paga los períodos del plan cuya fecha de pago llegó y no se pagaron (<see cref="PagoDeInteresesDeCdat"/>); el vencido exige la causación hasta el fin
/// del período; el último no se paga si la versión capitaliza en la prórroga que viene;</item>
/// <item>al cierre del último día de gracia (D17, días hábiles), con todo pagado: un CDAT embargado o de un titular fallecido queda vencido, a disposición
/// (FR-072); la instrucción «cancelar y pagar» lo cancela al destino (<see cref="SalidaDeCdat"/>); si no, se prorroga si la versión lo admite, no se agotó
/// el máximo y hay tasa (<see cref="ProrrogaDeCdat"/>); si no, el tratamiento de la línea: deja de causar (vencido por pagar) o pasa a la cuenta a la vista del
/// titular en la línea destino (sin ella, vencido).</item>
/// </list>
/// Con <c>Contabilidad.FaltaDeRegla = Bloquear</c> la cuenta no avanza si sus hechos no se contabilizarían (A8, E108). Lo que falla queda en
/// <c>LND_ProcessRunErrors</c>. (nuevo)
/// </summary>
public sealed class CicloDelCdat(
    IApplicationDbContext db,
    LectorDeCdat lector,
    PagoDeInteresesDeCdat pagos,
    ProrrogaDeCdat prorrogas,
    SalidaDeCdat salidas,
    ICerrojoDeCartera cerrojo,
    IDateTimeService reloj,
    ILogger<CicloDelCdat>? logger = null,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public const string CausaADisposicion = "AtDisposal";

    public string Nombre => "Cdat";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var candidatas = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Nature == LineNature.Deposit && a.DepositClass == DepositClass.TermDeposit && a.Status == MemberAccountStatus.Active)
            .OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        var pregunta = validacionPrevia is null || candidatas.Count == 0
            ? null
            : await validacionPrevia.TandaAsync(candidatas, d, $"el pago y el vencimiento de los CDAT del {d:yyyy-MM-dd}", ct);
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
                    await ProcesarCuentaAsync(cuenta, control, d, corrida, pregunta, ct);
                    await db.SaveChangesAsync(ct);
                    return 1;
                }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.CicloDelCdatFallido] El CDAT {Cuenta} no completó su ciclo del {Dia}.", id, d);
                fallos.Add(new FalloDeCuenta(id, ex is ReglaDeCarteraException r ? r.Codigo : "Lending.TermDeposit.CycleFailed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>El ciclo de un CDAT ya bloqueado el día <paramref name="d"/> (ver la clase). No guarda lo último: lo guarda quien llama.</summary>
    public async Task<CicloDeUnCdat> ProcesarCuentaAsync(MemberAccount cuenta, MemberAccountControl control, DateOnly d, LendingProcessRun? corrida, PreguntaDeTanda? pregunta,
        CancellationToken ct)
    {
        var leido = await lector.LeerAsync(cuenta, ct);
        if (leido.Vigente is not { } v) return new CicloDeUnCdat(cuenta.Id, 0, null);
        var plan = await lector.PlanAsync(v, ct);
        var aDisposicion = await PiezasDeCdat.ADisposicionAsync(db, cuenta, ct);
        var instruccion = PiezasDeCdat.InstruccionVigente(cuenta, v);
        var prorrogable = v.AutoRollover == true && (v.MaxRollovers is not { } max || v.RolloverCount < max);
        var capitaliza = v.CapitalizesOnRollover == true && prorrogable && instruccion != MaturityInstruction.CancelAndPay && !aDisposicion;
        var pagados = await PiezasDeCdat.PeriodosPagadosAsync(db, cuenta, plan, ct);
        var vencido = (v.InterestTiming ?? InterestTiming.Arrears) == InterestTiming.Arrears;
        var porPagar = plan.Periodos.Where(p => p.FechaDePago <= d && !pagados.Contains(p.Numero) && !(p.EsUltimo && capitaliza)).ToList();
        if (porPagar.Count > 0 && pregunta is not null
            && await pregunta.CuentaAsync(cuenta.Id, x => [HechosRepresentativosDeCaptaciones.PagoDeIntereses(x, null), HechosRepresentativosDeCaptaciones.RetencionDeCdat(x)], ct)
                is { } bloqueada)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(bloqueada.Codigo, bloqueada.Mensaje));

        var pagadosHoy = 0;
        if (!aDisposicion)
            foreach (var p in porPagar)
            {
                if (vencido && control.LastAccruedDate < p.Hasta) break;
                var r = await pagos.PagarAsync(cuenta, control, v, plan, p, d, corrida, ct);
                if (r.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(r.Error.Code, r.Error.Message));
                pagadosHoy++;
            }

        var finDeLaGracia = await PiezasDeCdat.FinDeLaGraciaAsync(lector, v, plan, ct);
        if (d < finDeLaGracia) return new CicloDeUnCdat(cuenta.Id, pagadosHoy, null);
        if (aDisposicion)
        {
            cuenta.Vencer(d, reloj.UtcNow);
            return new CicloDeUnCdat(cuenta.Id, pagadosHoy, CausaADisposicion);
        }
        pagados = await PiezasDeCdat.PeriodosPagadosAsync(db, cuenta, plan, ct);
        var pendientes = plan.Periodos.Where(p => !pagados.Contains(p.Numero) && !(p.EsUltimo && capitaliza)).ToList();
        if (pendientes.Count > 0) return new CicloDeUnCdat(cuenta.Id, pagadosHoy, null);

        if (instruccion == MaturityInstruction.CancelAndPay)
            return new CicloDeUnCdat(cuenta.Id, pagadosHoy, await CancelarAsync(cuenta, control, v, d, EmisionDeCartera.CancelacionPorInstruccion, null, corrida, ct));

        if (prorrogable)
        {
            var prorroga = await prorrogas.ProrrogarAsync(cuenta, control, v, plan, finDeLaGracia, capitaliza, corrida, ct);
            if (prorroga.IsSuccess) return new CicloDeUnCdat(cuenta.Id, pagadosHoy, "Rollover");
            if (prorroga.Error.Code != ProrrogaDeCdat.CodigoSinTasaDeProrroga)
                throw new ReglaDeCarteraException(new ProblemaDeCartera(prorroga.Error.Code, prorroga.Error.Message));
            // Sin tasa: el último período que iba a capitalizarse se paga antes del tratamiento.
            if (capitaliza && plan.Periodos[^1] is var ultimo && !pagados.Contains(ultimo.Numero))
            {
                var pagado = await pagos.PagarAsync(cuenta, control, v, plan, ultimo, d, corrida, ct);
                if (pagado.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(pagado.Error.Code, pagado.Error.Message));
            }
            return new CicloDeUnCdat(cuenta.Id, pagadosHoy, await TratamientoAsync(cuenta, control, v, d, EmisionDeCartera.CancelacionSinTasa, corrida, ct));
        }
        var causa = v.AutoRollover == true ? EmisionDeCartera.CancelacionPorMaximoDeProrrogas : EmisionDeCartera.CancelacionPorInstruccion;
        return new CicloDeUnCdat(cuenta.Id, pagadosHoy, await TratamientoAsync(cuenta, control, v, d, causa, corrida, ct));
    }

    /// <summary>
    /// El tratamiento sin prórroga (FR-070): <c>StopAccruing</c> deja el CDAT vencido por pagar; <c>MoveToDemand</c> lo pasa a la cuenta a la vista viva del
    /// titular en la línea destino (sin ella queda vencido y se anota en el registro).
    /// </summary>
    private async Task<string> TratamientoAsync(MemberAccount cuenta, MemberAccountControl control, MemberAccountTermsVersion v, DateOnly d, string causa,
        LendingProcessRun? corrida, CancellationToken ct)
    {
        if ((v.AfterMaturityTreatment ?? AfterMaturityTreatment.StopAccruing) == AfterMaturityTreatment.MoveToDemand)
        {
            var linea = await db.DepositLineVersions.AsNoTracking().Where(x => x.Id == v.DepositLineVersionId).Select(x => x.MoveToDemandLineId).FirstOrDefaultAsync(ct);
            var destino = linea is { } lineaId
                ? await db.MemberAccounts.Where(a => a.HolderPersonId == cuenta.HolderPersonId && a.CreditLineId == lineaId && a.DepositClass == DepositClass.Demand
                                                     && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive) && !a.IsDeleted)
                    .OrderBy(a => a.Id).FirstOrDefaultAsync(ct)
                : null;
            if (destino is not null)
                return await CancelarAsync(cuenta, control, v, d, EmisionDeCartera.CancelacionALaVista,
                    new DestinoResueltoDeCdat(TermDepositDestinationKind.Account, destino, null, null, null, null, null), corrida, ct);
            _log.LogWarning("[Cartera.CdatSinCuentaALaVista] El CDAT {Cuenta} pasa a la vista y el titular no tiene cuenta viva en la línea destino: queda vencido.",
                cuenta.Number);
        }
        cuenta.Vencer(d, reloj.UtcNow);
        return causa;
    }

    private async Task<string> CancelarAsync(MemberAccount cuenta, MemberAccountControl control, MemberAccountTermsVersion v, DateOnly d, string causa,
        DestinoResueltoDeCdat? destino, LendingProcessRun? corrida, CancellationToken ct)
    {
        var resuelto = destino ?? (await PiezasDeCdat.DestinoAsync(db, cuenta, v, alVencimiento: true, ct)) switch
        {
            { IsSuccess: true } ok => ok.Value,
            var mal => throw new ReglaDeCarteraException(new ProblemaDeCartera(mal.Error.Code, mal.Error.Message)),
        };
        if (resuelto.Cuenta is { } otra) await cerrojo.BloquearCuentasAsync([cuenta.Id, otra.Id], ModoDeEspera.Esperar, ct);
        var calculo = await salidas.CalcularAsync(cuenta, d, ct);
        if (calculo.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(calculo.Error.Code, calculo.Error.Message));
        var aplicada = await salidas.AplicarAsync(calculo.Value, control, resuelto, d, causa, $"Vencimiento del CDAT {cuenta.Number}", corrida, null,
            PrevalidationOutcome.NotApplicable, ct);
        if (aplicada.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(aplicada.Error.Code, aplicada.Error.Message));
        return causa;
    }
}
