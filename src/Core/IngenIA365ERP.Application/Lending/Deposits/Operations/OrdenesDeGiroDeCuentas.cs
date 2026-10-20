using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>El beneficiario bancario de una orden de giro de cuentas, ya resuelto (nulo el banco: Tesorería paga por su medio). (nuevo)</summary>
public sealed record BeneficiarioDeOrden(int? BancoId, string? CodigoDeTransferencia, byte? TipoDeCuenta, string? NumeroDeCuenta);

/// <summary>Una devolución ordenada: la operación que debitó la cuenta y su orden de giro. (nuevo)</summary>
public sealed record DevolucionDeCuentaOrdenada(int CuentaId, MemberAccountOperation Operacion, AccountPayoutOrder Orden, decimal Monto);

/// <summary>
/// Las órdenes de giro de cuentas (feature 014, US10, T101; data-model §3.9; mensajes.md §5.3; FR-079, FR-091; research C05, C11): la devolución del
/// retiro del asociado por <c>AccountPayoutOrder</c> y <c>OrdenDeGiroEmitida</c> v2 a Tesorería en el plazo de <c>Captaciones.DiasPlazoDevolucionAportes</c>
/// (o el de la versión de aportes), con <c>DevolucionOrdenada</c>; y el consumo del <c>ResultadoDelGiro</c> de cualquier orden de cuentas:
/// <list type="bullet">
/// <item><c>Disbursed</c>: la orden queda girada y se emite <c>DevolucionGirada</c> (informativo); un retiro pasa a <c>Paid</c> cuando todas sus órdenes
/// vivas están giradas y sus cuentas sin saldo se cancelan;</item>
/// <item><c>Rejected</c>, <c>Voided</c> o <c>CheckNotCashed</c>: <b>reintegra el saldo</b> con el reverso de la operación de origen
/// (<c>OperacionDeCuentaReversada</c>, iniciado por <c>TreasuryResult</c>); la devolución de un retiro vuelve a «por devolver» y se ordena otra vez con
/// <c>refund/approve</c>.</item>
/// </list>
/// Corre bajo el cerrojo y en la transacción de quien llama; no guarda salvo lo que el reverso necesita. (nuevo)
/// </summary>
public sealed class OrdenesDeGiroDeCuentas(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReversionDeCuentas reversion,
    ReglasDeParametrosDeCartera parametros,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    /// <summary>El prefijo del comprobante de una devolución del retiro.</summary>
    public const string PrefijoDevolucion = "DVR-";

    /// <summary>Quién inicia el reintegro de una orden rechazada (mensajes.md §5.3, <c>initiatedBy</c>).</summary>
    public const string ReintegroPorTesoreria = "TreasuryResult";

    /// <summary>
    /// El plazo de la devolución (FR-079): los días de devolución de la versión de aportes y, si no los fija, <c>Captaciones.DiasPlazoDevolucionAportes</c>;
    /// sin ninguno, la misma fecha efectiva.
    /// </summary>
    public async Task<DateOnly> PlazoDeDevolucionAsync(DateOnly efectiva, int? diasDeLaVersion, CancellationToken ct)
    {
        if (diasDeLaVersion is > 0) return efectiva.AddDays(diasDeLaVersion.Value);
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CaptacionesDiasPlazoDevolucionAportes, efectiva, ct: ct);
        var dias = leido.IsSuccess
            ? leido.Value.Valor switch
            {
                int i => i,
                decimal d => (int)d,
                _ => int.TryParse(leido.Value.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0,
            }
            : 0;
        return efectiva.AddDays(Math.Max(0, dias));
    }

    /// <summary>
    /// El beneficiario de la devolución: el que se pide o, sin él, la cuenta de depósito registrada del asociado (<c>COR_Associates.DepositBank*</c>) si su
    /// tipo de cuenta se lee; si no, ninguno (Tesorería paga por su medio).
    /// </summary>
    public async Task<Result<BeneficiarioDeOrden>> BeneficiarioAsync(int personaId, Guid? banco, byte? tipo, string? numero, CancellationToken ct)
    {
        if (banco is { } b)
        {
            var fila = await db.Banks.AsNoTracking().Where(x => x.PublicId == b && !x.IsDeleted).Select(x => new { x.Id, x.TransferCode }).FirstOrDefaultAsync(ct);
            if (fila is null) return Result.Failure<BeneficiarioDeOrden>(new ErrorConDatos("Generic.NotFound", "El banco del beneficiario no existe.",
                new { field = "refund.payee.bankPublicId" }));
            return Result.Success(new BeneficiarioDeOrden(fila.Id, fila.TransferCode, tipo, numero));
        }
        var asociado = await db.Associates.AsNoTracking().Where(a => a.PersonId == personaId && !a.IsDeleted).OrderByDescending(a => a.Id)
            .Select(a => new { a.DepositBankId, a.DepositBankAccountNumber, a.DepositBankAccountType }).FirstOrDefaultAsync(ct);
        if (asociado?.DepositBankId is { } id && !string.IsNullOrWhiteSpace(asociado.DepositBankAccountNumber)
            && byte.TryParse(asociado.DepositBankAccountType, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
        {
            var codigo = await db.Banks.AsNoTracking().Where(x => x.Id == id).Select(x => x.TransferCode).FirstOrDefaultAsync(ct);
            return Result.Success(new BeneficiarioDeOrden(id, codigo, t, asociado.DepositBankAccountNumber));
        }
        return Result.Success(new BeneficiarioDeOrden(null, null, null, null));
    }

    /// <summary>
    /// Ordena la devolución del retiro: por cada cuenta con algo que devolver, la operación que la debita (aportes <c>ContributionRefund</c>, ahorro
    /// <c>Withdrawal</c>, filas <c>Refund</c> por concepto), la orden de giro numerada (propósito <c>ContributionRefund</c>, con el retiro) y
    /// <c>DevolucionOrdenada</c> + <c>OrdenDeGiroEmitida</c> v2 (<c>MembershipRefund</c>). Las cuentas ya están bloqueadas. Guarda (la orden nombra su
    /// operación).
    /// </summary>
    public async Task<Result<IReadOnlyList<DevolucionDeCuentaOrdenada>>> DevolverRetiroAsync(MembershipWithdrawal retiro,
        IReadOnlyList<DebitoDeCuentaEnElRetiro> devolucion, BeneficiarioDeOrden beneficiario, DateOnly hoy, DateOnly devolverHasta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retiro);
        ArgumentNullException.ThrowIfNull(devolucion);
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == retiro.PersonId, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var hechas = new List<DevolucionDeCuentaOrdenada>();
        foreach (var grupo in devolucion.GroupBy(d => d.CuentaId))
        {
            var monto = grupo.Sum(d => d.Monto);
            if (monto <= 0m) continue;
            var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == grupo.Key, ct);
            var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
            if (hecho.IsFailure) return Result.Failure<IReadOnlyList<DevolucionDeCuentaOrdenada>>(hecho.Error);
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
            var clase = cuenta.Nature == LineNature.Contribution ? MemberAccountOperationKind.ContributionRefund : MemberAccountOperationKind.Withdrawal;
            var numero = await numerador.NumeroDeOperacionDeCuentaAsync(clase, PrefijoDevolucion, ct);
            var ahora = reloj.UtcNow;
            var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
            {
                MemberAccountId = cuenta.Id,
                Kind = clase,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = hoy,
                AccountingDate = hecho.Value.FechaContable,
                RegisteredAt = sello.RegisteredAt,
                Amount = monto,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                MembershipWithdrawalId = retiro.Id,
                Reason = $"Devolución del retiro {retiro.Number}",
                Payload = $"{{\"withdrawalPublicId\":\"{retiro.PublicId:N}\",\"settlementVersion\":{retiro.SettlementVersion.ToString(CultureInfo.InvariantCulture)}}}",
                InputsSha256 = new HuellaDeCartera().Identificador("withdrawal", retiro.PublicId).Entero("version", retiro.SettlementVersion)
                    .Decimal("amount", monto, 2).Calcular(),
                RulesVersion = VersionesDeReglas.Vigente,
            });
            await db.SaveChangesAsync(ct);

            var orden = AccountPayoutOrder.Emitir(cuenta.Id, operacion.Id, PayoutPurpose.ContributionRefund, titular.Id, titular.IdType ?? string.Empty,
                titular.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(titular), beneficiario.BancoId, beneficiario.TipoDeCuenta, beneficiario.NumeroDeCuenta,
                monto, devolverHasta < hoy.AddDays(1) ? hoy.AddDays(1) : devolverHasta, ahora, actor.UserId, retiro.Id);
            await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
            db.AccountPayoutOrders.Add(orden);
            await db.SaveChangesAsync(ct);

            var valores = grupo.Select(d => new DepositConceptAmountV1
            {
                Concept = d.Concepto.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(d.Concepto)?.ToString(), Amount = -d.Monto,
                ReasonCode = "MembershipRefund",
            }).ToList();
            var payee = new PayeeV1
            {
                PersonPublicId = titular.PublicId, TaxIdType = titular.IdType ?? string.Empty, TaxId = titular.TaxId ?? string.Empty,
                Name = ReferenciasDeGobierno.Nombre(titular), BankCode = beneficiario.CodigoDeTransferencia, BankAccountNumber = beneficiario.NumeroDeCuenta,
                BankAccountType = beneficiario.TipoDeCuenta?.ToString(CultureInfo.InvariantCulture),
            };
            var unidad = await emision.DevolucionOrdenadaAsync(new DevolucionAEmitir(hecho.Value, operacion, valores, "MembershipRefund", orden, payee,
                titular.PublicId, nameof(MembershipWithdrawal), cuenta.Number)
            {
                Clase = EmisionDeCartera.ClaseLiquidacionDeRetiro,
            }, ct);
            if (unidad.IsFailure) return Result.Failure<IReadOnlyList<DevolucionDeCuentaOrdenada>>(unidad.Error);
            var mensaje = unidad.Value.De(DevolucionOrdenadaV1.Type);
            registro.RegistrarTransacciones(grupo.Select(d => new MemberAccountTransaction
            {
                MemberAccountId = cuenta.Id,
                Operation = operacion,
                OperationId = operacion.Id,
                Kind = MemberAccountTransactionKind.Refund,
                Concept = d.Concepto,
                Amount = -d.Monto,
                ValueDate = hoy,
                AccountingDate = hecho.Value.FechaContable,
                RegisteredAt = operacion.RegisteredAt,
                MessagePublicId = mensaje,
            }).ToList());
            hechas.Add(new DevolucionDeCuentaOrdenada(cuenta.Id, operacion, orden, monto));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<DevolucionDeCuentaOrdenada>>(hechas);
    }

    /// <summary>
    /// El efecto de un <c>ResultadoDelGiro</c> sobre una orden de cuentas (mensajes.md §5.3), ya validado el valor y la transición: girada (emite
    /// <c>DevolucionGirada</c> y, si es la última viva de un retiro, lo deja pagado) o reintegrada (reversa la operación de origen). La orden y sus cuentas
    /// ya están bloqueadas. No guarda el estado de la orden: lo guarda quien llama.
    /// </summary>
    public async Task<Result> AplicarResultadoAsync(AccountPayoutOrder orden, DisbursementOrderStatus hacia, string referencia, DateOnly ocurrida, string? motivo,
        CancellationToken ct, UserRefV1? usuarioDeOrigen = null)
    {
        ArgumentNullException.ThrowIfNull(orden);
        var ahora = reloj.UtcNow;
        // US12 (T203; research §22) y respuestas 92 y 96 del dueño (E258): el pago de excedentes no salió de ninguna cuenta del asociado (va del fondo a los
        // retornos por pagar) y la entrega por fallecimiento tampoco vuelve a la cuenta del fallecido: lo decretado sigue respaldado en la cuenta por pagar a
        // favor del destinatario (la «devolución por pagar» de su tercero) y la orden queda con su resultado, pendiente de regirar (RegirarAsync). El GMF ya
        // retenido de una entrega no se devuelve ni se vuelve a cobrar al regirar: el hecho generador fue el retiro, y ocurrió.
        if (orden.SeRegira && AccountPayoutOrder.Reintegra(hacia))
        {
            orden.RegistrarResultado(hacia, ahora);
            return Result.Success();
        }
        if (AccountPayoutOrder.Reintegra(hacia))
        {
            var origen = await db.MemberAccountOperations.FirstAsync(o => o.Id == orden.OperationId, ct);
            var texto = $"Tesorería informó la orden {orden.Number} como «{hacia}»" + (string.IsNullOrWhiteSpace(motivo) ? "." : $": {motivo}");
            var reverso = await reversion.ReversarAsync(origen, texto, ReintegroPorTesoreria, ct);
            if (reverso.IsFailure) return Result.Failure(reverso.Error);
            orden.RegistrarResultado(hacia, ahora);
            // US14 (T209): el giro del pago de un saldo no reclamado que volvió: el reverso lo devolvió a «no reclamados» y el saldo vuelve a retenido.
            await Unclaimed.NoReclamados.RetenerPorGiroDevueltoAsync(db, orden, ct);
            // US11 (T189; FR-091): el giro rechazado de una consignación al juzgado devuelve el saldo y lo deja retenido para el mismo embargo.
            if (orden.Purpose == PayoutPurpose.GarnishmentConsignment && origen.GarnishmentId is { } embargo)
                Holds.RetencionDeEmbargos.Reintegrar(db, embargo, orden.MemberAccountId, orden.Amount, reloj.HoyLocal, texto);
            // US5 (T135; data-model §3.1): la orden de una cancelación rechazada devuelve la cuenta a activa con su saldo.
            if (orden.Purpose == PayoutPurpose.Cancellation
                && await db.MemberAccounts.FirstOrDefaultAsync(a => a.Id == orden.MemberAccountId && a.Status == MemberAccountStatus.Closing, ct) is { } enCierre)
                enCierre.RevertirCierre(MemberAccountStatus.Active, ahora);
            // US6 (T173): el giro rechazado de la salida de un CDAT lo devuelve a su estado (vencido si ya había vencido) con su saldo reintegrado.
            if (orden.Purpose == PayoutPurpose.TermDepositRedemption
                && await db.MemberAccounts.FirstOrDefaultAsync(a => a.Id == orden.MemberAccountId && a.Status == MemberAccountStatus.Closing, ct) is { } cdatEnCierre)
                cdatEnCierre.RevertirCierre(cdatEnCierre.MaturedOn is null ? MemberAccountStatus.Active : MemberAccountStatus.Matured, ahora);
            return Result.Success();
        }

        orden.RegistrarResultado(hacia, ahora);
        if (hacia != DisbursementOrderStatus.Disbursed) return Result.Success();
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == orden.MemberAccountId, ct);
        var sucursal = await emision.SucursalContableAsync(cuenta.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure(sucursal.Error);
        var beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == orden.BeneficiaryPersonId).Select(p => p.PublicId).FirstAsync(ct);
        await emision.DevolucionGiradaAsync(orden, EmisionDeCartera.TipoDeOrden(orden), referencia, ocurrida, sucursal.Value, beneficiario, ct, usuarioDeOrigen);

        // US14 (T210): girada una entrega por fallecimiento, la cuenta sin saldo se cancela y, con todas canceladas, el caso queda entregado.
        await Deceased.Fallecimientos.AlGirarAsync(db, orden, reloj.HoyLocal, ahora, (await actorActual.ObtenerAsync(ct)).UserId, ct);
        // US5 (T135; FR-039): girada la entrega de una cancelación, la cuenta sin saldo queda cancelada.
        if (orden.Purpose is PayoutPurpose.Cancellation or PayoutPurpose.TermDepositRedemption)
        {
            var enCierre = await db.MemberAccounts.FirstAsync(a => a.Id == orden.MemberAccountId, ct);
            var saldos = (await LectorDeCuentas.SaldosAsync(db, [enCierre.Id], ct)).GetValueOrDefault(enCierre.Id) ?? [];
            if (enCierre.Status == MemberAccountStatus.Closing && LectorDeCuentas.SaldoDelTitular(enCierre.Nature, saldos) == 0m)
                enCierre.Cerrar(reloj.HoyLocal, (await actorActual.ObtenerAsync(ct)).UserId, $"Cancelación girada por la orden {orden.Number}", ahora);
        }

        if (orden.MembershipWithdrawalId is { } retiroId)
        {
            var retiro = await db.MembershipWithdrawals.FirstAsync(w => w.Id == retiroId, ct);
            var pendientes = await db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.MembershipWithdrawalId == retiroId && o.Id != orden.Id && !o.IsDeleted && o.Status == DisbursementOrderStatus.Issued)
                .AnyAsync(ct);
            if (!pendientes && retiro.Status == MembershipWithdrawalStatus.Settled && !retiro.RefundHeldByInstitutionalMinimum
                && !await QuedaPorDevolverAsync(retiro, ct))
            {
                retiro.Pagar(ahora);
                await CerrarCuentasAsync(retiro, reloj.HoyLocal, ct);
            }
        }
        return Result.Success();
    }

    /// <summary>Lo que responde cuando una orden no espera un regiro.</summary>
    public const string CodigoNoSeRegira = "Lending.PayoutOrder.NotReissuable";

    /// <summary>
    /// Las órdenes que esperan volver a girarse (E258): de un propósito que se regira, con el resultado de Tesorería que no las pagó y sin otra orden que las
    /// reemplace. Mientras estén así, lo que deben sigue respaldado en la cuenta por pagar a favor de su destinatario.
    /// </summary>
    public static IQueryable<AccountPayoutOrder> PendientesDeRegiro(IApplicationDbContext db) =>
        db.AccountPayoutOrders.Where(o => !o.IsDeleted
            && (o.Purpose == PayoutPurpose.SurplusPayment || o.Purpose == PayoutPurpose.DeceasedDelivery)
            && (o.Status == DisbursementOrderStatus.Rejected || o.Status == DisbursementOrderStatus.Voided || o.Status == DisbursementOrderStatus.CheckNotCashed)
            && !db.AccountPayoutOrders.Any(r => r.ReissuesOrderId == o.Id && !r.IsDeleted));

    /// <summary>
    /// Vuelve a girar una orden pendiente de regiro (respuestas 92 y 96, E258): una orden nueva por el mismo valor y la misma operación —el dinero no volvió a
    /// ninguna cuenta: sigue por pagar a favor del destinatario—, al mismo beneficiario y cuenta destino, o a los que se indiquen, con su
    /// <c>OrdenDeGiroEmitida</c> v2 a Tesorería. Las cuentas y la persona ya están bloqueadas; no guarda. <paramref name="fuente"/> es el documento que la
    /// origina (la decisión de asamblea o la operación de la entrega) y <paramref name="concepto"/> el texto de la orden.
    /// </summary>
    public async Task<Result<AccountPayoutOrder>> RegirarAsync(AccountPayoutOrder original, BeneficiarioDeOrden? nuevoDestino, Guid fuente, string concepto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!original.SeRegira || !AccountPayoutOrder.Reintegra(original.Status)
            || await db.AccountPayoutOrders.AsNoTracking().AnyAsync(r => r.ReissuesOrderId == original.Id && !r.IsDeleted, ct))
            return Result.Failure<AccountPayoutOrder>(new ErrorConDatos(CodigoNoSeRegira,
                $"La orden {original.Number} no espera un regiro: sólo se vuelve a girar, y una sola vez, la orden de excedentes o de una entrega por fallecimiento que Tesorería rechazó, anuló o no se cobró.",
                new { order = original.Number, status = original.Status.ToString() }));
        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == original.MemberAccountId, ct);
        var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
        if (hecho.IsFailure) return Result.Failure<AccountPayoutOrder>(hecho.Error);
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == original.BeneficiaryPersonId, ct);
        var destino = nuevoDestino ?? new BeneficiarioDeOrden(original.DestinationBankId,
            original.DestinationBankId is { } banco ? await db.Banks.AsNoTracking().Where(b => b.Id == banco).Select(b => b.TransferCode).FirstOrDefaultAsync(ct) : null,
            original.DestinationAccountType, original.DestinationAccountNumber);
        var actor = await actorActual.ObtenerAsync(ct);
        var nueva = AccountPayoutOrder.Emitir(original.MemberAccountId, original.OperationId, original.Purpose, original.BeneficiaryPersonId,
            original.BeneficiaryTaxIdType, original.BeneficiaryTaxId, original.BeneficiaryName, destino.BancoId, destino.TipoDeCuenta, destino.NumeroDeCuenta,
            original.Amount, hoy.AddDays(1), ahora, actor.UserId);
        nueva.DeceasedHolderCaseId = original.DeceasedHolderCaseId;
        nueva.ReissuesOrderId = original.Id;
        await numerador.NumerarOrdenDeCuentaAsync(nueva, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
        db.AccountPayoutOrders.Add(nueva);
        var payee = new PayeeV1
        {
            PersonPublicId = titular.PublicId, TaxIdType = original.BeneficiaryTaxIdType, TaxId = original.BeneficiaryTaxId, Name = original.BeneficiaryName,
            BankCode = destino.CodigoDeTransferencia, BankAccountNumber = destino.NumeroDeCuenta, BankAccountType = destino.TipoDeCuenta?.ToString(CultureInfo.InvariantCulture),
        };
        await emision.OrdenReemitidaAsync(nueva, hecho.Value, payee, titular.PublicId, fuente, concepto, ct);
        return Result.Success(nueva);
    }

    /// <summary>¿Alguna cuenta del retiro conserva saldo del titular? (una orden reintegrada que todavía no se volvió a ordenar).</summary>
    public async Task<bool> QuedaPorDevolverAsync(MembershipWithdrawal retiro, CancellationToken ct)
    {
        var cuentas = await CuentasDelRetiroAsync(retiro, ct);
        var saldos = await LectorDeCuentas.SaldosAsync(db, cuentas.Select(c => c.Id).ToList(), ct);
        return cuentas.Any(c => LectorDeCuentas.SaldoDelTitular(c.Nature, saldos.GetValueOrDefault(c.Id) ?? []) > 0m);
    }

    /// <summary>Las cuentas del retiro: la de aportes y las que la liquidación vigente nombra.</summary>
    public async Task<IReadOnlyList<MemberAccount>> CuentasDelRetiroAsync(MembershipWithdrawal retiro, CancellationToken ct)
    {
        // Los Id primero: un AsNoTracking en una subconsulta vuelve sin seguimiento la consulta entera y el cierre no se guardaría.
        var nombradas = await db.MembershipWithdrawalLines.AsNoTracking()
            .Where(l => l.MembershipWithdrawalId == retiro.Id && l.SettlementVersion == retiro.SettlementVersion && l.MemberAccountId != null
                        // US6 (T175): los CDAT vigentes van informativos y siguen hasta su vencimiento: no son cuentas del retiro.
                        && l.Kind != MembershipWithdrawalLineKind.TermDepositBalance)
            .Select(l => l.MemberAccountId!.Value).Distinct().ToListAsync(ct);
        nombradas.Add(retiro.ContributionAccountId);
        return await db.MemberAccounts.Where(a => nombradas.Contains(a.Id)).OrderBy(a => a.Id).ToListAsync(ct);
    }

    /// <summary>Cancela las cuentas del retiro que quedaron sin saldo del titular (FR-076): se piden en cierre si hace falta y se cierran.</summary>
    public async Task CerrarCuentasAsync(MembershipWithdrawal retiro, DateOnly hoy, CancellationToken ct)
    {
        var cuentas = await CuentasDelRetiroAsync(retiro, ct);
        var saldos = await LectorDeCuentas.SaldosAsync(db, cuentas.Select(c => c.Id).ToList(), ct);
        var ahora = reloj.UtcNow;
        var actor = await actorActual.ObtenerAsync(ct);
        foreach (var cuenta in cuentas.Where(c => c.Status != MemberAccountStatus.Closed))
        {
            if (LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos.GetValueOrDefault(cuenta.Id) ?? []) != 0m) continue;
            if (cuenta.Status is not MemberAccountStatus.Closing && MemberAccount.PuedePasar(cuenta.Status, MemberAccountStatus.Closing)) cuenta.PedirCierre(ahora);
            if (MemberAccount.PuedePasar(cuenta.Status, MemberAccountStatus.Closed))
                cuenta.Cerrar(hoy, actor.UserId, $"Retiro del asociado {retiro.Number}", ahora);
        }
    }
}
