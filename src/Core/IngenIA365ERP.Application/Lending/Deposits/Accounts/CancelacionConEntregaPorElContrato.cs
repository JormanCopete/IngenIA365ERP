using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// La cancelación de una cuenta de ahorro <b>con entrega por el contrato</b> (feature 015, R1, T0350; E-02 d; contratos-v2.md §3.4; FR-052): cuando
/// <c>RetirarAsync</c> llega con <c>CloseAccount = true</c> de un recaudador <c>Receipts</c> real, Captaciones hace lo mismo que la cancelación desde su
/// pantalla —liquida a la fecha con su retención, cobra los cargos pendientes del saldo y el GMF de la cancelación— y, en lugar de girar o trasladar, deja el
/// retiro de <b>todo el disponible</b> en efectivo o cheque como autorización que el recaudador entrega con el mismo egreso. Corre dentro de la transacción y
/// del cerrojo que ya tomó el contrato.
/// <list type="bullet">
/// <item>sólo el ahorro que se cancela por la pantalla (<see cref="CancelacionDeCuenta.Admite"/>); el contractual se sigue cancelando desde Cartera, con su
/// penalidad (<c>Lending.Savings.DeliveryNotAvailable</c>);</item>
/// <item>la fecha es la de hoy (<c>Lending.Savings.CloseDateInvalid</c>) y no hay bloqueos (<c>Lending.Savings.CloseBlocked</c>);</item>
/// <item>el valor pedido es el que entrega la cancelación a hoy (el de la simulación <c>Close</c>); si cambió, nada se escribe y responde
/// <c>Lending.Savings.CloseAmountChanged</c> <b>(nuevo)</b> con <c>data.toDeliver</c>, para simular otra vez;</item>
/// <item>repetir la clave devuelve la autorización original sin cancelar dos veces.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class CancelacionConEntregaPorElContrato(
    IApplicationDbContext db,
    CancelacionDeCuenta cancelacion,
    CausacionDeCaptaciones causacion,
    LiquidacionDeIntereses liquidacion,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj,
    CargosDeCuentas? cargos = null)
{
    /// <summary>El valor pedido ya no es el que entrega la cancelación a hoy. (nuevo)</summary>
    public const string CodigoValorCambiado = "Lending.Savings.CloseAmountChanged";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>¿El recaudador puede pedir la cancelación con entrega? Sólo Recaudo real (E-02 d).</summary>
    public static bool Admitido(Recaudador recaudador) => recaudador.Class == ClaseDeRecaudador.Receipts;

    /// <summary>Lo que entregaría la cancelación de la cuenta hoy (la simulación <c>Close</c> del contrato).</summary>
    public async Task<Result<MemberAccountClosingPreviewDto>> PrevisualizarAsync(MemberAccount cuenta, CancellationToken ct)
    {
        if (cuenta.DepositClass == DepositClass.Contractual) return Result.Failure<MemberAccountClosingPreviewDto>(ErroresDeAportes.CancelacionPorElContratoNoDisponible());
        return await cancelacion.PrevisualizarAsync(cuenta, reloj.HoyLocal, ct);
    }

    /// <summary>Cancela y autoriza el retiro de todo el disponible; el recaudador lo entrega (ver la clase).</summary>
    public async Task<Result<RetiroAplicado>> CancelarAsync(RetiroPedido pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        // Repetir la clave: la autorización original (RetiroDeCuenta compara la huella), sin volver a cancelar.
        if (pedido.Recaudador is { } recaudador && pedido.Clave is { } clave
            && await db.MemberAccountOperations.AsNoTracking().AnyAsync(o => o.CollectorCode == recaudador.Code && o.IdempotencyKey == clave, ct))
            return await retiros.AplicarAsync(pedido with { EsCancelacion = true }, ct);

        var hoy = reloj.HoyLocal;
        if (pedido.FechaValor != hoy)
            return Result.Failure<RetiroAplicado>(new ErrorConDatos(CancelacionDeCuenta.CodigoFechaDeCancelacion,
                $"La cuenta se cancela con la fecha de hoy ({ErroresDeCuentas.Fecha(hoy)}).", new { date = pedido.FechaValor, today = hoy }));
        var cuenta = await db.MemberAccounts.FirstOrDefaultAsync(a => a.PublicId == pedido.AccountPublicId && !a.IsDeleted, ct);
        if (cuenta is null) return Result.Failure<RetiroAplicado>(new Error(ErroresDePagos.CodigoNoEncontrado, "La cuenta no existe."));

        // Antes de escribir: lo que se entregaría y lo que lo impide.
        var vista = await PrevisualizarAsync(cuenta, ct);
        if (vista.IsFailure) return Result.Failure<RetiroAplicado>(vista.Error);
        if (vista.Value.Blockers.Count > 0) return Result.Failure<RetiroAplicado>(ErroresDeMovimientos.CierreBloqueado(vista.Value.Blockers.Cast<object>().ToList()));
        if (vista.Value.ToDeliver != pedido.Monto)
            return Result.Failure<RetiroAplicado>(new ErrorConDatos(CodigoValorCambiado,
                $"La cancelación entrega hoy {vista.Value.ToDeliver:N2}, no {pedido.Monto:N2}: simule la cancelación otra vez y confirme ese valor.",
                new { toDeliver = vista.Value.ToDeliver, requested = pedido.Monto, gmf = vista.Value.Gmf }));

        // 1. La operación de la cancelación con la puesta al día y la liquidación a la fecha (como la cancelación desde la pantalla).
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
        if (hecho.IsFailure) return Result.Failure<RetiroAplicado>(hecho.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Cancellation, CloseMemberAccountCommandHandler.PrefijoCancelacion, ct);
        var motivo = string.IsNullOrWhiteSpace(pedido.Motivo) ? $"Cancelación con entrega en {pedido.Recaudador?.Code ?? "el recaudador"}" : pedido.Motivo.Trim();
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.Cancellation,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = 0m,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            Reason = motivo.Length <= 500 ? motivo : motivo[..500],
            Payload = JsonSerializer.Serialize(new { account = cuenta.PublicId, date = hoy, delivery = "Collector", collector = pedido.Recaudador?.Code, key = pedido.Clave },
                Json),
            InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Fecha("date", hoy).Texto("delivery", "Collector").Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        if (LiquidacionDeIntereses.GanaInteres(cuenta))
        {
            var alDia = await causacion.PonerAlDiaAsync(cuenta, hoy, operacion, ct);
            if (alDia.IsFailure) return Result.Failure<RetiroAplicado>(alDia.Error);
            await db.SaveChangesAsync(ct);
            foreach (var fin in (await liquidacion.PeriodosVencidosAsync(cuenta, control, hoy, ct)).Where(f => f < hoy))
            {
                var vencida = await liquidacion.LiquidarAsync(cuenta, control, fin, null, null, motivo, ct);
                if (vencida.IsFailure) return Result.Failure<RetiroAplicado>(vencida.Error);
            }
            var ultima = await liquidacion.LiquidarAsync(cuenta, control, hoy, null, operacion, motivo, ct);
            if (ultima.IsFailure) return Result.Failure<RetiroAplicado>(ultima.Error);
        }

        // 1b. Los cargos por cobrar, del capital, antes de entregar (respuesta 65 B de la 014).
        if (cargos is not null)
        {
            var actual = await movimientos.LeerAsync(cuenta, hoy, ct);
            var (cobrables, sinCubrir) = await cargos.PendientesDeLaCancelacionAsync(cuenta, actual.Saldos.GetValueOrDefault(MemberAccountConcept.Principal), ct);
            if (CancelacionDeCuenta.CargosSinCubrir(sinCubrir) is { } sinCobertura)
                return Result.Failure<RetiroAplicado>(ErroresDeMovimientos.CierreBloqueado([sinCobertura]));
            var cobro = await cargos.CobrarEnCancelacionAsync(cuenta, cobrables, hoy, motivo, ct);
            if (cobro.IsFailure) return Result.Failure<RetiroAplicado>(cobro.Error);
        }

        // 2. La entrega: el retiro de todo el disponible con el GMF de la cancelación, que el recaudador entrega.
        var despues = await movimientos.LeerAsync(cuenta, hoy, ct);
        var saldo = despues.Saldo - despues.Disponible.Retenido - despues.Disponible.Embargado - despues.Disponible.EnCanje;
        var (entrega, _) = await cancelacion.EntregaAsync(despues, saldo, hoy, ct);
        RetiroAplicado? retiro = null;
        if (entrega > 0m)
        {
            var r = await retiros.AplicarAsync(pedido with { Monto = entrega, EsCancelacion = true, HechoGenerador = HechosGeneradoresDeGmf.Cancelacion, Motivo = motivo }, ct);
            if (r.IsFailure) return r;
            retiro = r.Value;
        }

        // 3. Cancelada: ya no queda saldo.
        var final = await movimientos.LeerAsync(cuenta, hoy, ct);
        var ahora = reloj.UtcNow;
        if (cuenta.Status != MemberAccountStatus.Closing) cuenta.PedirCierre(ahora);
        if (final.Saldo == 0m) cuenta.Cerrar(hoy, actor.UserId, motivo, ahora);
        await db.SaveChangesAsync(ct);
        return retiro is not null
            ? Result.Success(retiro)
            : Result.Failure<RetiroAplicado>(new ErrorConDatos(CodigoValorCambiado, "La cancelación no deja nada que entregar: cancele la cuenta desde Cartera.",
                new { toDeliver = 0m }));
    }
}
