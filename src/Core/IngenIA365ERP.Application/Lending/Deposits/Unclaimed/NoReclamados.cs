using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Unclaimed;

/// <summary>Los errores de los saldos no reclamados (api.md §14). (nuevo)</summary>
public static class ErroresDeNoReclamados
{
    public const string CodigoInexistente = "Lending.Unclaimed.NotFound";
    public const string CodigoNoElegible = "Lending.Unclaimed.NotEligible";
    public const string CodigoReglaNoHabilitada = "Lending.Unclaimed.RuleNotEnabled";
    public const string CodigoYaPagado = "Lending.Unclaimed.AlreadyPaid";
    public const string CodigoInvalido = "Lending.Unclaimed.Invalid";

    public static Error Inexistente() => new(CodigoInexistente, "El saldo no reclamado no existe.");

    public static Error NoElegibles(IReadOnlyList<NoElegible> items) => new ErrorConDatos(CodigoNoElegible,
        items.Count == 1 ? items[0].Mensaje : $"{items.Count} de los pedidos no pueden pasar a no reclamados: nada se trasladó.", new { items });

    public static Error ReglaNoHabilitada() => new(CodigoReglaNoHabilitada,
        "El traslado del saldo de cuentas inactivas no está habilitado (CuentasAbandonadas.TrasladoHabilitado): lo confirma la contadora según el reglamento.");

    public static Error YaPagado(DateOnly? dia) => new ErrorConDatos(CodigoYaPagado, $"El saldo no reclamado ya se pagó el {dia:yyyy-MM-dd}.", new { claimedOn = dia });

    public static Error Invalido(string campo, string mensaje) => new ErrorConDatos(CodigoInvalido, mensaje, new { field = campo });
}

/// <summary>Por qué un pedido no puede pasar a no reclamados (en <c>data.items</c> de <c>Lending.Unclaimed.NotEligible</c>). (nuevo)</summary>
public sealed record NoElegible(Guid? AccountPublicId, string? AccountNumber, Guid? RefundPublicId, string Reason, string Mensaje);

/// <summary>Una cuenta que puede pasar a no reclamados: de dónde viene su saldo, la orden que volvió (si es lo devuelto) y desde cuándo cuenta. (nuevo)</summary>
public sealed record CandidataANoReclamados(int CuentaId, UnclaimedSource Fuente, int? OrdenId, DateOnly Desde, DateOnly? Cumple);

/// <summary>Cómo se paga un saldo no reclamado: por traslado a una cuenta del titular o por orden de giro. (nuevo)</summary>
public enum FormaDePagoDeNoReclamado
{
    TransferToAccount = 1,
    DisbursementOrder = 2,
}

/// <summary>
/// Los saldos no reclamados (feature 014, US14, T209; data-model §6.6; FR-081, FR-084; research §23). Dos fuentes:
/// <list type="bullet">
/// <item><b>lo devuelto y no cobrado</b>: la cuenta que debía quedar vacía —la devolución de aportes o del retiro del asociado, la cancelación, la salida del
/// CDAT— cuya última orden de giro Tesorería rechazó, anuló o informó como cheque no cobrado (el reverso le reintegró el saldo) y que nadie volvió a ordenar ni
/// movió el titular desde entonces; pasa cumplidos <c>Captaciones.DiasParaNoReclamados</c> desde que volvió (vacío: sólo a mano);</item>
/// <item><b>el saldo de una cuenta inactiva</b>, sólo con <c>CuentasAbandonadas.TrasladoHabilitado</c> (la contadora confirma que el reglamento lo ordena,
/// P7) y cumplidos <c>CuentasAbandonadas.DiasSinMovimiento</c> sin movimientos del titular.</item>
/// </list>
/// Trasladar: la liquidación de intereses a la fecha si la cuenta gana interés, la operación <c>ToUnclaimed</c> (prefijo <c>NRC-</c>) con una fila por concepto
/// que sale y la del concepto <c>Unclaimed</c> que entra, <c>SaldoTrasladadoANoReclamados</c>, el <see cref="UnclaimedBalance"/>, el aviso al titular
/// (<c>NORECLAMA</c>) y la cuenta cancelada. No pasan las cuentas con bloqueos o embargos vigentes, de un titular con caso de fallecimiento vivo, con cargos
/// por cobrar o sin saldo. Pagar: la operación <c>UnclaimedPayment</c> (prefijo <c>PNR-</c>) sobre la cuenta de origen aunque esté cancelada, por orden de
/// giro (<c>DevolucionOrdenada</c> y <c>OrdenDeGiroEmitida</c> v2, motivo <c>UnclaimedPayment</c>) o por traslado a una cuenta de ahorro viva del titular
/// (<c>TrasladoEntreCuentasAplicado</c>). Si Tesorería rechaza el giro, el reverso devuelve el saldo a <c>Unclaimed</c> y el saldo no reclamado vuelve a
/// retenido. En la transacción y bajo el cerrojo de quien llama. (nuevo)
/// </summary>
public sealed class NoReclamados(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    LiquidacionALaFecha liquidacion,
    OrdenesDeGiroDeCuentas ordenes,
    ReglasDeParametrosDeCartera parametros,
    IActorActual actorActual,
    IDateTimeService reloj,
    ColaDeMensajesAlDeudor? cola = null,
    ReactivacionDeCuenta? reactivacion = null,
    ILogger<NoReclamados>? logger = null)
{
    public const string PrefijoTraslado = "NRC-";
    public const string PrefijoPago = "PNR-";
    public const string PlantillaDeAviso = "NORECLAMA";

    /// <summary>Las órdenes que debían dejar la cuenta vacía: si vuelven y nadie las cobra, el saldo es «lo devuelto y no cobrado».</summary>
    public static readonly PayoutPurpose[] PropositosQueVacian =
        [PayoutPurpose.ContributionRefund, PayoutPurpose.Cancellation, PayoutPurpose.TermDepositRedemption];

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    // -------------------------------------------------------------------------------------------------- elegibilidad --

    /// <summary>
    /// ¿Puede la cuenta pasar a no reclamados el <paramref name="hoy"/>? En el ciclo (<paramref name="automatico"/>) exige el plazo con valor; a mano, el
    /// plazo sólo si la cooperativa lo fijó. Devuelve la candidata o por qué no.
    /// </summary>
    public async Task<(CandidataANoReclamados? Candidata, NoElegible? Motivo, bool ReglaNoHabilitada)> EvaluarAsync(MemberAccount cuenta, DateOnly hoy,
        bool automatico, CancellationToken ct)
    {
        NoElegible No(string razon, string mensaje) => new(cuenta.PublicId, cuenta.Number, null, razon, mensaje);
        if (cuenta.Status is MemberAccountStatus.Closed or MemberAccountStatus.Opening)
            return (null, No("AccountNotOperable", $"La cuenta {cuenta.Number} está «{cuenta.Status}»: no tiene saldo que trasladar."), false);
        if (await db.AccountHolds.AsNoTracking().AnyAsync(h => h.MemberAccountId == cuenta.Id && h.ReleasedAt == null && !h.IsDeleted
                                                               && h.ValidFrom <= hoy && (h.ValidTo == null || h.ValidTo >= hoy), ct))
            return (null, No("HasHolds", $"La cuenta {cuenta.Number} tiene bloqueos o embargos vigentes: su saldo no pasa a no reclamados."), false);
        if (await db.DeceasedHolderCases.AsNoTracking().AnyAsync(c => c.PersonId == cuenta.HolderPersonId && !c.IsDeleted
                && (c.Status == DeceasedCaseStatus.Registered || c.Status == DeceasedCaseStatus.DeliveryPending || c.Status == DeceasedCaseStatus.Approved), ct))
            return (null, No("DeceasedHolder", $"El titular de la cuenta {cuenta.Number} tiene un caso de fallecimiento: se entrega por ese trámite."), false);
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        if (saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable) > 0m)
            return (null, No("ChargesPending", $"La cuenta {cuenta.Number} tiene cargos por cobrar: se cobran o se condonan antes del traslado."), false);
        if (LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos) <= 0m)
            return (null, No("NoBalance", $"La cuenta {cuenta.Number} no tiene saldo del titular."), false);

        // Lo devuelto y no cobrado: la última orden de la cuenta volvió y nadie la volvió a ordenar ni movió la cuenta.
        var orden = await db.AccountPayoutOrders.AsNoTracking().Where(o => o.MemberAccountId == cuenta.Id && !o.IsDeleted).OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(ct);
        var ultimo = await db.MemberAccountControls.AsNoTracking().Where(c => c.MemberAccountId == cuenta.Id).Select(c => c.LastHolderActivityDate).FirstAsync(ct);
        if (orden is not null && PropositosQueVacian.Contains(orden.Purpose) && AccountPayoutOrder.Reintegra(orden.Status))
        {
            var volvio = DateOnly.FromDateTime(orden.StatusChangedAt ?? orden.IssuedAt);
            if (ultimo is { } mov && mov > volvio)
                return (null, No("HolderMovedAfter", $"El titular movió la cuenta {cuenta.Number} después de que la orden {orden.Number} volvió."), false);
            var dias = await Inactividad.EnteroAsync(parametros, ParametrosDeCartera.CaptacionesDiasParaNoReclamados, hoy, ct);
            if (dias is null && automatico) return (null, No("TermNotSet", "Sin Captaciones.DiasParaNoReclamados el ciclo no traslada."), false);
            var cumple = dias is { } n ? volvio.AddDays(n) : (DateOnly?)null;
            if (cumple is { } c && hoy < c)
                return (null, No("NotYet", $"Lo devuelto de la cuenta {cuenta.Number} pasa a no reclamados desde el {c:yyyy-MM-dd}."), false);
            var fuente = orden.Purpose == PayoutPurpose.ContributionRefund ? UnclaimedSource.UncashedRefund : UnclaimedSource.UncashedPayout;
            return (new CandidataANoReclamados(cuenta.Id, fuente, orden.Id, volvio, cumple), null, false);
        }

        // El saldo de una cuenta inactiva, cuando el reglamento lo ordena (P7).
        if (cuenta.Status == MemberAccountStatus.Inactive && cuenta.Nature == LineNature.Deposit)
        {
            var habilitado = await HabilitadoAsync(hoy, ct);
            if (!habilitado) return (null, No("RuleNotEnabled", "El traslado de cuentas inactivas no está habilitado."), true);
            var desde = Inactividad.DesdeCuando(cuenta, ultimo);
            var dias = await Inactividad.EnteroAsync(parametros, ParametrosDeCartera.CuentasAbandonadasDiasSinMovimiento, hoy, ct) ?? 0;
            var cumple = desde.AddDays(dias);
            if (hoy < cumple)
                return (null, No("NotYet", $"La cuenta {cuenta.Number} cumple {dias} días sin movimiento del titular el {cumple:yyyy-MM-dd}."), false);
            return (new CandidataANoReclamados(cuenta.Id, UnclaimedSource.InactiveBalance, null, desde, cumple), null, false);
        }
        return (null, No("NotEligible",
            $"La cuenta {cuenta.Number} no tiene lo devuelto sin cobrar ni está inactiva: su saldo no pasa a no reclamados."), false);
    }

    /// <summary>¿Está habilitado el traslado de cuentas inactivas? (<c>CuentasAbandonadas.TrasladoHabilitado</c>; sin lectura, el defecto: no).</summary>
    public async Task<bool> HabilitadoAsync(DateOnly dia, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CuentasAbandonadasTrasladoHabilitado, dia, ct: ct);
        var texto = leido.IsSuccess ? leido.Value.Texto : ParametrosDeCartera.Definiciones.First(d => d.Clave == ParametrosDeCartera.CuentasAbandonadasTrasladoHabilitado).DefectoSeguro;
        return string.Equals(texto, "true", StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------------------------------------- trasladar --

    /// <summary>Traslada el saldo de la cuenta a no reclamados (ver la clase). La cuenta ya está bloqueada; guarda.</summary>
    public async Task<Result<UnclaimedBalance>> TrasladarAsync(MemberAccount cuenta, CandidataANoReclamados candidata, DateOnly hoy, string motivo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var texto = UnclaimedBalance.Recortar(string.IsNullOrWhiteSpace(motivo) ? "Traslado a no reclamados" : motivo);
        // Los intereses a la fecha se liquidan antes (queda en el capital lo que se le debe, con su retención).
        var liquidada = await liquidacion.LiquidarAsync(cuenta, hoy, texto, ct);
        if (liquidada.IsFailure) return Result.Failure<UnclaimedBalance>(liquidada.Error);

        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var conceptos = cuenta.Nature == LineNature.Contribution
            ? MemberAccountTransaction.ConceptosDeAportes.ToArray()
            : [MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable, MemberAccountConcept.Bonus];
        var salen = conceptos.Select(c => (Concepto: c, Valor: saldos.GetValueOrDefault(c))).Where(x => x.Valor != 0m).ToList();
        var total = salen.Sum(x => x.Valor);
        if (total <= 0m) return Result.Failure<UnclaimedBalance>(ErroresDeNoReclamados.NoElegibles([new(cuenta.PublicId, cuenta.Number, null, "NoBalance",
            $"La cuenta {cuenta.Number} no tiene saldo del titular.")]));
        if (salen.Any(x => RegistroDeCuentas.Decimales(x.Valor) > 2))
            return Result.Failure<UnclaimedBalance>(ErroresDeNoReclamados.NoElegibles([new(cuenta.PublicId, cuenta.Number, null, "AccrualPending",
                $"La cuenta {cuenta.Number} tiene causación sin liquidar: se liquida antes del traslado.")]));

        var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
        if (hecho.IsFailure) return Result.Failure<UnclaimedBalance>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.ToUnclaimed, PrefijoTraslado, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var noReclamadoId = Guid.NewGuid();
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.ToUnclaimed,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = total,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            Reason = texto,
            Payload = $"{{\"source\":\"{candidata.Fuente}\",\"unclaimedPublicId\":\"{noReclamadoId:N}\"}}",
            InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Texto("source", candidata.Fuente.ToString())
                .Decimal("amount", total, 2).Fecha("date", hoy).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        var valores = salen.Select(x => new DepositConceptAmountV1
            {
                Concept = x.Concepto.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(x.Concepto)?.ToString(), Amount = -x.Valor,
                ReasonCode = "UnclaimedTransfer",
            })
            .Append(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Unclaimed), Amount = total, ReasonCode = "UnclaimedTransfer" })
            .ToList();
        var unidad = await emision.SaldoTrasladadoANoReclamadosAsync(new TrasladoANoReclamadosAEmitir(hecho.Value, operacion, valores, candidata.Fuente.ToString(),
            noReclamadoId), ct);
        if (unidad.IsFailure) return Result.Failure<UnclaimedBalance>(unidad.Error);
        var mensaje = unidad.Value.Mensajes.FirstOrDefault()?.PublicId;
        var filas = salen.Select(x => Fila(cuenta, operacion, MemberAccountTransactionKind.ToUnclaimed, x.Concepto, -x.Valor, hoy, hecho.Value.FechaContable, mensaje))
            .Append(Fila(cuenta, operacion, MemberAccountTransactionKind.ToUnclaimed, MemberAccountConcept.Unclaimed, total, hoy, hecho.Value.FechaContable, mensaje))
            .ToList();
        registro.RegistrarTransacciones(filas);

        var saldo = new UnclaimedBalance
        {
            PublicId = noReclamadoId,
            PersonId = cuenta.HolderPersonId,
            SourceMemberAccountId = cuenta.Id,
            Source = candidata.Fuente,
            SourcePayoutOrderId = candidata.OrdenId,
            Amount = total,
            TransferredOn = hoy,
            TransferOperationId = operacion.Id,
            Reason = texto,
            TransferredByUserId = actor.UserId,
        };
        db.UnclaimedBalances.Add(saldo);
        await AvisarAsync(cuenta, total, hoy, ct);
        if (cuenta.Status != MemberAccountStatus.Closed)
            cuenta.Cerrar(hoy, actor.UserId, $"Saldo trasladado a no reclamados ({operacion.Number})", ahora);
        await db.SaveChangesAsync(ct);

        // El retiro del asociado cuya devolución no se cobró: sin nada más por devolver queda pagado y sus cuentas canceladas.
        if (candidata.OrdenId is { } ordenId
            && await db.AccountPayoutOrders.AsNoTracking().Where(o => o.Id == ordenId).Select(o => o.MembershipWithdrawalId).FirstOrDefaultAsync(ct) is { } retiroId)
        {
            var retiro = await db.MembershipWithdrawals.FirstAsync(w => w.Id == retiroId, ct);
            var vivas = await db.AccountPayoutOrders.AsNoTracking()
                .AnyAsync(o => o.MembershipWithdrawalId == retiroId && !o.IsDeleted && o.Status == DisbursementOrderStatus.Issued, ct);
            if (!vivas && retiro.Status == MembershipWithdrawalStatus.Settled && !retiro.RefundHeldByInstitutionalMinimum
                && !await ordenes.QuedaPorDevolverAsync(retiro, ct))
            {
                retiro.Pagar(ahora);
                await ordenes.CerrarCuentasAsync(retiro, hoy, ct);
                await db.SaveChangesAsync(ct);
            }
        }
        return Result.Success(saldo);
    }

    // ---------------------------------------------------------------------------------------------------------- pagar --

    /// <summary>
    /// Paga el saldo no reclamado al titular (ver la clase): por orden de giro al banco pedido o a la cuenta de depósito registrada, o por traslado a
    /// <paramref name="destino"/> (una cuenta de ahorro viva del titular, ya bloqueada y en el alcance). La cuenta de origen ya está bloqueada; guarda.
    /// </summary>
    public async Task<Result<MemberAccountOperation>> PagarAsync(UnclaimedBalance saldo, FormaDePagoDeNoReclamado forma, MemberAccount? destino, Guid? banco,
        byte? tipoDeCuenta, string? numeroDeCuenta, string motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(saldo);
        if (saldo.Status != UnclaimedStatus.Held) return Result.Failure<MemberAccountOperation>(ErroresDeNoReclamados.YaPagado(saldo.ClaimedOn));
        var hoy = reloj.HoyLocal;
        var texto = UnclaimedBalance.Recortar(string.IsNullOrWhiteSpace(motivo) ? "Pago de un saldo no reclamado" : motivo);
        var origen = await db.MemberAccounts.FirstAsync(a => a.Id == saldo.SourceMemberAccountId, ct);
        var enLaCuenta = (await LectorDeCuentas.SaldosAsync(db, [origen.Id], ct)).GetValueOrDefault(origen.Id)?.GetValueOrDefault(MemberAccountConcept.Unclaimed) ?? 0m;
        if (enLaCuenta < saldo.Amount)
            return Result.Failure<MemberAccountOperation>(ErroresDeNoReclamados.Invalido("unclaimed",
                $"La cuenta {origen.Number} guarda {enLaCuenta:N2} no reclamados y el saldo es de {saldo.Amount:N2}: el libro no lo respalda."));
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == saldo.PersonId, ct);
        var hecho = await emision.HechoDeCuentaAsync(origen, hoy, ct);
        if (hecho.IsFailure) return Result.Failure<MemberAccountOperation>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == origen.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.UnclaimedPayment, PrefijoPago, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = origen.Id,
            CounterpartMemberAccountId = destino?.Id,
            Kind = MemberAccountOperationKind.UnclaimedPayment,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = saldo.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            Reason = texto,
            Payload = $"{{\"unclaimedPublicId\":\"{saldo.PublicId:N}\",\"form\":\"{forma}\"}}",
            InputsSha256 = new HuellaDeCartera().Identificador("unclaimed", saldo.PublicId).Texto("form", forma.ToString()).Decimal("amount", saldo.Amount, 2)
                .Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        int? ordenId = null;
        if (forma == FormaDePagoDeNoReclamado.DisbursementOrder)
        {
            var beneficiario = await ordenes.BeneficiarioAsync(titular.Id, banco, tipoDeCuenta, numeroDeCuenta, ct);
            if (beneficiario.IsFailure) return Result.Failure<MemberAccountOperation>(beneficiario.Error);
            var b = beneficiario.Value;
            var orden = AccountPayoutOrder.Emitir(origen.Id, operacion.Id, PayoutPurpose.UnclaimedPayment, titular.Id, titular.IdType ?? string.Empty,
                titular.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(titular), b.BancoId, b.TipoDeCuenta, b.NumeroDeCuenta, saldo.Amount, hoy.AddDays(1),
                ahora, actor.UserId);
            await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
            db.AccountPayoutOrders.Add(orden);
            await db.SaveChangesAsync(ct);
            ordenId = orden.Id;
            var payee = new PayeeV1
            {
                PersonPublicId = titular.PublicId, TaxIdType = titular.IdType ?? string.Empty, TaxId = titular.TaxId ?? string.Empty,
                Name = ReferenciasDeGobierno.Nombre(titular), BankCode = b.CodigoDeTransferencia, BankAccountNumber = b.NumeroDeCuenta,
                BankAccountType = b.TipoDeCuenta?.ToString(CultureInfo.InvariantCulture),
            };
            var valores = new List<DepositConceptAmountV1>
            {
                new() { Concept = nameof(MemberAccountConcept.Unclaimed), Amount = -saldo.Amount, ReasonCode = nameof(PayoutPurpose.UnclaimedPayment) },
            };
            var unidad = await emision.DevolucionOrdenadaAsync(new DevolucionAEmitir(hecho.Value, operacion, valores, nameof(PayoutPurpose.UnclaimedPayment), orden,
                payee, titular.PublicId, nameof(PayoutPurpose.UnclaimedPayment), $"Saldo no reclamado de la cuenta {origen.Number}")
            {
                Clase = EmisionDeCartera.ClasePagoDeNoReclamados,
            }, ct);
            if (unidad.IsFailure) return Result.Failure<MemberAccountOperation>(unidad.Error);
            registro.RegistrarTransacciones([Fila(origen, operacion, MemberAccountTransactionKind.Refund, MemberAccountConcept.Unclaimed, -saldo.Amount, hoy,
                hecho.Value.FechaContable, unidad.Value.De(DevolucionOrdenadaV1.Type))]);
        }
        else
        {
            if (destino is null) return Result.Failure<MemberAccountOperation>(ErroresDeNoReclamados.Invalido("targetAccountPublicId",
                "El traslado nombra la cuenta de ahorro del titular que recibe."));
            var hechoDestino = await emision.HechoDeCuentaAsync(destino, hoy, ct);
            if (hechoDestino.IsFailure) return Result.Failure<MemberAccountOperation>(hechoDestino.Error);
            var deOrigen = new List<DepositConceptAmountV1> { new() { Concept = nameof(MemberAccountConcept.Unclaimed), Amount = -saldo.Amount } };
            var aDestino = new List<DepositConceptAmountV1> { new() { Concept = nameof(MemberAccountConcept.Principal), Amount = saldo.Amount } };
            var unidad = await emision.TrasladoEntreCuentasAsync(new TrasladoAEmitir(hecho.Value, hechoDestino.Value, operacion, deOrigen, aDestino), ct);
            if (unidad.IsFailure) return Result.Failure<MemberAccountOperation>(unidad.Error);
            var mensaje = unidad.Value.De(TrasladoEntreCuentasAplicadoV1.Type);
            registro.RegistrarTransacciones([
                Fila(origen, operacion, MemberAccountTransactionKind.TransferOut, MemberAccountConcept.Unclaimed, -saldo.Amount, hoy, hecho.Value.FechaContable, mensaje),
                Fila(destino, operacion, MemberAccountTransactionKind.TransferIn, MemberAccountConcept.Principal, saldo.Amount, hoy, hecho.Value.FechaContable, mensaje),
            ]);
            var controlDestino = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == destino.Id, ct);
            RegistroDeCuentas.MarcarActividadDelTitular(controlDestino, hoy);
            await db.SaveChangesAsync(ct);
            if (await ReactivacionDeCuenta.ReactivarAsync(reactivacion, destino, hoy, ahora, ct) is { IsFailure: true } noReactivada)
                return Result.Failure<MemberAccountOperation>(noReactivada.Error);
        }
        saldo.Reclamar(hoy, operacion.Id, ordenId, actor.UserId);
        await db.SaveChangesAsync(ct);
        return Result.Success(operacion);
    }

    /// <summary>Tesorería rechazó o anuló el giro del pago (el reverso ya devolvió el saldo a <c>Unclaimed</c>): el saldo vuelve a retenido. No guarda.</summary>
    public static async Task RetenerPorGiroDevueltoAsync(IApplicationDbContext db, AccountPayoutOrder orden, CancellationToken ct)
    {
        if (orden.Purpose != PayoutPurpose.UnclaimedPayment) return;
        var saldo = await db.UnclaimedBalances.FirstOrDefaultAsync(u => u.ClaimPayoutOrderId == orden.Id && !u.IsDeleted, ct);
        if (saldo is { Status: UnclaimedStatus.Claimed }) saldo.Retener();
    }

    private static MemberAccountTransaction Fila(MemberAccount cuenta, MemberAccountOperation op, MemberAccountTransactionKind clase, MemberAccountConcept concepto,
        decimal valor, DateOnly fecha, DateOnly contable, Guid? mensaje) => new()
    {
        MemberAccountId = cuenta.Id, Operation = op, OperationId = op.Id, Kind = clase, Concept = concepto, Amount = valor, ValueDate = fecha,
        AccountingDate = contable, RegisteredAt = op.RegisteredAt, MessagePublicId = mensaje,
    };

    private async Task AvisarAsync(MemberAccount cuenta, decimal valor, DateOnly hoy, CancellationToken ct)
    {
        if (cola is null) return;
        try
        {
            var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == cuenta.CreditLineId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
            var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(cuenta.HolderPersonId, null, DebtorMessagePurpose.Informative, MessageChannel.Email,
                PlantillaDeAviso, DebtorMessage.OrigenInactividadDeCuenta, $"{cuenta.PublicId:N}:noreclamados:{hoy:yyyyMMdd}",
                (await actorActual.ObtenerAsync(ct)).Kind, (await actorActual.ObtenerAsync(ct)).UserId)
            {
                OriginPublicId = cuenta.PublicId,
                Valores = new Dictionary<string, string?>
                {
                    ["NumeroDeCuenta"] = cuenta.Number,
                    ["NombreDeLaLinea"] = linea,
                    ["ValorNoReclamado"] = valor.ToString("N2", CultureInfo.GetCultureInfo("es-CO")),
                    ["FechaDelTraslado"] = hoy.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                },
            }, ct);
            if (r.IsFailure)
                _log.LogInformation("[Cartera.AvisoDeNoReclamados] No se encoló el aviso de la cuenta {Cuenta}: {Codigo} {Mensaje}", cuenta.Number, r.Error.Code,
                    r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.AvisoDeNoReclamados] El aviso de la cuenta {Cuenta} falló.", cuenta.Number);
        }
    }
}

/// <summary>
/// El subpaso «no reclamados» del paso de captaciones (feature 014, US14, T209; FR-081, FR-084): cada día traslada solo lo que cumple —lo devuelto y no
/// cobrado con <c>Captaciones.DiasParaNoReclamados</c> fijado, y el saldo de las inactivas con el traslado habilitado y el plazo sin movimiento cumplido—.
/// Cada cuenta en su transacción con el cerrojo del proceso (salta la ocupada). Corre desde K10, después de la inactividad. (nuevo)
/// </summary>
public sealed class NoReclamadosDelCiclo(
    IApplicationDbContext db,
    NoReclamados noReclamados,
    ICerrojoDeCartera cerrojo,
    IContextoDeLineasDeCaptacion contexto,
    ILogger<NoReclamadosDelCiclo>? logger = null,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "NoReclamados";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0 || contexto.Entrega < EntregaDeCartera.K10) return [];
        var ids = cuentas.ToArray();
        var vacian = NoReclamados.PropositosQueVacian;
        var conOrdenDevuelta = await db.AccountPayoutOrders.AsNoTracking()
            .Where(o => ids.Contains(o.MemberAccountId) && !o.IsDeleted && vacian.Contains(o.Purpose)
                        && (o.Status == DisbursementOrderStatus.Rejected || o.Status == DisbursementOrderStatus.Voided || o.Status == DisbursementOrderStatus.CheckNotCashed))
            .Select(o => o.MemberAccountId).Distinct().ToListAsync(ct);
        var habilitado = await noReclamados.HabilitadoAsync(d, ct);
        var inactivas = habilitado
            ? await db.MemberAccounts.AsNoTracking().Where(a => ids.Contains(a.Id) && a.Status == MemberAccountStatus.Inactive && !a.IsDeleted).Select(a => a.Id)
                .ToListAsync(ct)
            : [];
        var fallos = new List<FalloDeCuenta>();
        var aTrasladar = conOrdenDevuelta.Concat(inactivas).Distinct().Order().ToList();
        // Pendiente de la fase 17 (research E206): el proceso pregunta a Contabilidad por la tanda, una vez por combinación; con «Bloquear» y una regla
        // faltante la cuenta no se traslada, queda en los errores de la corrida y sale Cartera.HechoNoContabilizable.
        var pregunta = validacionPrevia is null || aTrasladar.Count == 0
            ? null
            : await validacionPrevia.TandaAsync(aTrasladar, d, $"el traslado a no reclamados del {d:yyyy-MM-dd}", ct);
        foreach (var id in aTrasladar)
        {
            if (pregunta is not null && await pregunta.CuentaAsync(id, c => [HechosRepresentativosDeCaptaciones.TrasladoANoReclamados(c, "Process")], ct) is { } bloqueo)
            {
                fallos.Add(bloqueo);
                continue;
            }
            try
            {
                var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
                {
                    await cerrojo.TomarSetupAsync(exclusivo: false, ct);
                    if ((await cerrojo.BloquearCuentasAsync([id], ModoDeEspera.Saltar, ct)).Count == 0) return Result.Success();
                    var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == id, ct);
                    var (candidata, _, _) = await noReclamados.EvaluarAsync(cuenta, d, automatico: true, ct);
                    if (candidata is null) return Result.Success();
                    var hecho = await noReclamados.TrasladarAsync(cuenta, candidata, d, "Traslado a no reclamados del ciclo diario", ct);
                    return hecho.IsFailure ? Result.Failure(hecho.Error) : Result.Success();
                }, ct);
                if (r.IsFailure) fallos.Add(new FalloDeCuenta(id, r.Error.Code, r.Error.Message));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.NoReclamadosFallido] La cuenta {Cuenta} no pasó a no reclamados el {Dia}.", id, d);
                fallos.Add(new FalloDeCuenta(id, ex is ReglaDeCarteraException e ? e.Codigo : "Lending.Unclaimed.TransferFailed", ex.Message));
            }
        }
        return fallos;
    }
}
