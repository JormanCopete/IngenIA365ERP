using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Charges;

/// <summary>Un cargo calculado de un catálogo, antes de causarlo. (nuevo)</summary>
public sealed record CargoPropuesto(DepositLineCharge Definicion, CargoCalculadoDeCuenta Calculo, DateOnly? PeriodoDesde = null, DateOnly? PeriodoHasta = null);

/// <summary>Un cargo causado: la fila del cargo, la operación que lo causó y el cálculo. (nuevo)</summary>
public sealed record CargoCausado(AccountCharge Cargo, MemberAccountOperation Operacion, CargoCalculadoDeCuenta Calculo);

/// <summary>
/// Los cargos de las cuentas de captación (feature 014, US4, T116; research R14; data-model §2.3, §4.8, §4.9; FR-128, D35): el único que los causa, los
/// cobra, los condona y los reversa. Cada cargo lo calcula <see cref="CargosDeCuenta"/> con el catálogo de la versión vigente de la línea (apagado por
/// defecto) y el contexto de la cuenta; se causa en su propia operación <c>Charge</c> —debita el capital si alcanza sin bajar del mínimo, o queda por cobrar
/// (<c>ChargeReceivable</c>) y se cobra con la siguiente consignación (<c>CargoPendienteCobrado</c>)—, con <c>CargoDeCuentaCausado</c> a Contabilidad y, si
/// está gravado, <c>CargoDeCaptacionCausado</c> a Comercial, que factura. Corre en la transacción y bajo el cerrojo de quien llama (el contrato, la
/// pantalla o el paso de captaciones). (nuevo)
/// </summary>
public sealed class CargosDeCuentas(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public const string PrefijoCargo = "CGO-";
    public const string PrefijoCondonacion = "CGN-";
    public const string PrefijoCobroEnCancelacion = "CGC-";

    /// <summary>Los cargos habilitados del catálogo de la versión con ese disparador (la versión vigente a la fecha; vacío sin versión).</summary>
    public async Task<IReadOnlyList<DepositLineCharge>> CatalogoAsync(DepositLineVersion? version, IReadOnlyCollection<ChargeTrigger> disparadores, CancellationToken ct)
    {
        if (version is null) return [];
        var lista = disparadores.ToArray();
        return await db.DepositLineCharges.AsNoTracking()
            .Where(c => c.DepositLineVersionId == version.Id && c.IsEnabled && !c.IsDeleted && lista.Contains(c.Trigger))
            .OrderBy(c => c.Code).ToListAsync(ct);
    }

    /// <summary>
    /// Los cargos por evento de una operación (FR-128): los de su disparador sobre los gratuitos del período —contando las operaciones del mismo tipo en el
    /// período del cargo, más ésta— calculados con el disponible que deja la operación. No escribe.
    /// </summary>
    public async Task<IReadOnlyList<CargoPropuesto>> PorEventoAsync(CuentaParaMovimiento c, ChargeTrigger disparador, decimal montoDeLaOperacion, DateOnly fecha,
        decimal cobrable, CancellationToken ct)
    {
        var catalogo = await CatalogoAsync(c.Version, [disparador], ct);
        var propuestos = new List<CargoPropuesto>();
        foreach (var def in catalogo)
        {
            var periodo = CargosDeCuenta.PeriodoDe(def.FreePeriod ?? RepaymentPeriodicity.Monthly, fecha);
            var clases = disparador == ChargeTrigger.WithdrawalOverFree
                ? new[] { MemberAccountOperationKind.Withdrawal, MemberAccountOperationKind.Transfer }
                : [MemberAccountOperationKind.Deposit];
            var hechas = await db.MemberAccountOperations.AsNoTracking()
                .CountAsync(o => o.MemberAccountId == c.Cuenta.Id && clases.Contains(o.Kind) && o.ValueDate >= periodo.Desde && o.ValueDate <= periodo.Hasta
                                 && o.ReversedByOperationId == null && !o.IsDeleted, ct);
            var contexto = await ContextoAsync(c, def, montoDeLaOperacion, periodo.Desde, fecha, hechas + 1, cobrable, ct);
            propuestos.Add(new CargoPropuesto(def, CargosDeCuenta.Calcular(def.Definicion(), contexto)));
        }
        return propuestos;
    }

    /// <summary>El contexto del cargo (FR-128): saldos promedio y mínimo del período, antigüedad, empleado, exenta, menor, oficina y exoneración individual.</summary>
    public async Task<ContextoDeCargo> ContextoAsync(CuentaParaMovimiento c, DepositLineCharge def, decimal montoDeLaOperacion, DateOnly desde, DateOnly hasta,
        int operaciones, decimal cobrable, CancellationToken ct)
    {
        var (promedio, minimo) = await SaldosDelPeriodoAsync(c.Cuenta, desde, hasta, ct);
        var afiliacion = await PersonaDeOriginacion.AfiliacionAsync(db, c.Titular, hasta, ct);
        int? meses = afiliacion.Activo && afiliacion.Desde is { } ingreso ? (hasta.Year - ingreso.Year) * 12 + hasta.Month - ingreso.Month : null;
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == c.Cuenta.BranchId).Select(b => b.PublicId).FirstOrDefaultAsync(ct);
        var exonerada = await db.AccountChargeExemptions.AsNoTracking()
            .AnyAsync(e => e.MemberAccountId == c.Cuenta.Id && e.ChargeCode == def.Code && !e.IsDeleted && e.ValidFrom <= hasta && (e.ValidTo == null || e.ValidTo >= hasta), ct);
        return new ContextoDeCargo(montoDeLaOperacion, promedio, minimo, meses, c.Titular.IsEmployee, c.ExentaDeGmf, c.TitularMenor, oficina, exonerada, operaciones,
            cobrable);
    }

    /// <summary>El saldo promedio y el mínimo del titular en (desde, hasta), día por día, con lo registrado hasta ahora.</summary>
    public async Task<(decimal Promedio, decimal Minimo)> SaldosDelPeriodoAsync(MemberAccount cuenta, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) return (0m, 0m);
        var filas = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuenta.Id && t.ValueDate <= hasta && !t.IsDeleted)
            .Select(t => new { t.ValueDate, t.Concept, t.Amount }).ToListAsync(ct);
        decimal Del(MemberAccountConcept concepto, decimal monto) => concepto switch
        {
            MemberAccountConcept.Principal or MemberAccountConcept.InterestPayable or MemberAccountConcept.Bonus => monto,
            MemberAccountConcept.ChargeReceivable => -monto,
            _ => 0m,
        };
        var saldo = filas.Where(f => f.ValueDate < desde).Sum(f => Del(f.Concept, f.Amount));
        var porDia = filas.Where(f => f.ValueDate >= desde).GroupBy(f => f.ValueDate).ToDictionary(g => g.Key, g => g.Sum(f => Del(f.Concept, f.Amount)));
        decimal suma = 0m, minimo = decimal.MaxValue;
        var dias = 0;
        for (var d = desde; d <= hasta; d = d.AddDays(1))
        {
            saldo += porDia.GetValueOrDefault(d);
            suma += saldo;
            minimo = Math.Min(minimo, saldo);
            dias++;
        }
        return (Math.Round(suma / dias, 2, MidpointRounding.AwayFromZero), minimo == decimal.MaxValue ? 0m : minimo);
    }

    /// <summary>
    /// Causa un cargo calculado en su propia operación <c>Charge</c> (data-model §4.8): cobrado (<c>ChargeApplied</c> −capital) o pendiente
    /// (<c>ChargeApplied</c> +por cobrar); la fila del cargo con su disparador o su período; <c>CargoDeCuentaCausado</c> y, si está gravado,
    /// <c>CargoDeCaptacionCausado</c> a Comercial. La cuenta ya está bloqueada. Guarda dentro de la transacción de quien llama (la fila del cargo nombra su
    /// operación).
    /// </summary>
    public async Task<Result<CargoCausado>> CausarAsync(CuentaParaMovimiento c, CargoPropuesto propuesto, DateOnly fecha, long? disparadora, string? evento,
        string? motivo, CancellationToken ct)
    {
        var calculo = propuesto.Calculo;
        if (!calculo.SeCausa) throw new InvalidOperationException("Un cargo que no se causa no llega aquí.");
        var cuenta = c.Cuenta;
        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<CargoCausado>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cuenta.Id, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Charge, PrefijoCargo, ct);
        var def = propuesto.Definicion;
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.Charge,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = calculo.Valor,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            Reason = Recortar(motivo ?? $"Cargo {def.Code}: {def.Name}"),
            Payload = string.Create(CultureInfo.InvariantCulture,
                $"{{\"chargeCode\":\"{def.Code}\",\"trigger\":\"{def.Trigger}\",\"base\":{calculo.Base},\"amount\":{calculo.Valor},\"status\":\"{calculo.Resultado}\"}}"),
            InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Texto("charge", def.Code).Decimal("base", calculo.Base, 2)
                .Decimal("amount", calculo.Valor, 2).Fecha("periodFrom", propuesto.PeriodoDesde).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        var cargo = new AccountCharge
        {
            MemberAccountId = cuenta.Id,
            DepositLineChargeId = def.Id,
            ChargeCode = def.Code,
            Kind = def.Kind,
            PeriodFrom = propuesto.PeriodoDesde,
            PeriodTo = propuesto.PeriodoHasta,
            TriggerOperationId = disparadora,
            BaseAmount = calculo.Base,
            Amount = calculo.Valor,
            Status = calculo.Resultado == ResultadoDeCargo.Cobrado ? AccountChargeStatus.Collected : AccountChargeStatus.Pending,
            AppliedOperationId = operacion.Id,
            IsVatTaxed = def.IsVatTaxed,
        };
        db.AccountCharges.Add(cargo);
        await db.SaveChangesAsync(ct);

        var cobrado = cargo.Status == AccountChargeStatus.Collected;
        var valores = new List<DepositConceptAmountV1>
        {
            cobrado
                ? new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -calculo.Valor, ReasonCode = def.Kind.ToString() }
                : new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.ChargeReceivable), Amount = calculo.Valor, ReasonCode = def.Kind.ToString() },
        };
        var unidad = await emision.CargoDeCuentaAsync(new CargoAEmitir(hecho.Value, operacion, cargo, def.Name, def.CommercialItemCode, evento, valores), ct);
        if (unidad.IsFailure) return Result.Failure<CargoCausado>(unidad.Error);
        cargo.ChargeMessagePublicId = unidad.Value.De(CargoDeCaptacionCausadoV1.Type);
        registro.RegistrarTransacciones(
        [
            new MemberAccountTransaction
            {
                MemberAccountId = cuenta.Id,
                Operation = operacion,
                OperationId = operacion.Id,
                Kind = MemberAccountTransactionKind.ChargeApplied,
                Concept = cobrado ? MemberAccountConcept.Principal : MemberAccountConcept.ChargeReceivable,
                Amount = cobrado ? -calculo.Valor : calculo.Valor,
                ValueDate = fecha,
                AccountingDate = hecho.Value.FechaContable,
                RegisteredAt = operacion.RegisteredAt,
                MessagePublicId = unidad.Value.De(CargoDeCuentaCausadoV1.Type),
            },
        ]);
        await db.SaveChangesAsync(ct);
        return Result.Success(new CargoCausado(cargo, operacion, calculo));
    }

    /// <summary>
    /// Los cargos pendientes que una consignación cobra (FR-128): en orden de causación, enteros, mientras el <b>capital</b> después de la consignación no
    /// baje del mínimo (cobrar baja el capital y lo por cobrar por el mismo valor: el saldo del titular no cambia). Devuelve los cargos y lo que cobra de
    /// cada uno; no escribe (las filas van en la operación de la consignación).
    /// </summary>
    public async Task<IReadOnlyList<(AccountCharge Cargo, decimal Monto)>> PendientesACobrarAsync(MemberAccount cuenta, decimal capitalDespues, decimal minimo,
        CancellationToken ct)
    {
        var pendientes = await db.AccountCharges.Where(c => c.MemberAccountId == cuenta.Id && c.Status == AccountChargeStatus.Pending && !c.IsDeleted)
            .OrderBy(c => c.Id).ToListAsync(ct);
        var cobrables = new List<(AccountCharge, decimal)>();
        var resto = capitalDespues - minimo;
        foreach (var p in pendientes)
        {
            if (p.Amount > resto) break;
            cobrables.Add((p, p.Amount));
            resto -= p.Amount;
        }
        return cobrables;
    }

    /// <summary>
    /// Respuesta 65 B del dueño (E255): los cargos pendientes que la <b>cancelación</b> de la cuenta cobra del capital (enteros, en orden de causación, mientras el
    /// capital alcance; sin mínimo, porque la cuenta se cierra). Devuelve lo que no alcanzó a cubrir. No escribe.
    /// </summary>
    public async Task<(IReadOnlyList<(AccountCharge Cargo, decimal Monto)> Cobrables, decimal SinCubrir)> PendientesDeLaCancelacionAsync(MemberAccount cuenta,
        decimal capital, CancellationToken ct)
    {
        var cobrables = await PendientesACobrarAsync(cuenta, capital, 0m, ct);
        var pendientes = await db.AccountCharges.AsNoTracking()
            .Where(c => c.MemberAccountId == cuenta.Id && c.Status == AccountChargeStatus.Pending && !c.IsDeleted).Select(c => c.Amount).ToListAsync(ct);
        return (cobrables, pendientes.Sum() - cobrables.Sum(c => c.Monto));
    }

    /// <summary>
    /// Respuesta 65 B del dueño (E255): cobra del saldo, dentro de la cancelación, los cargos pendientes: su propia operación <c>ChargeCollection</c> (<c>CGC-</c>)
    /// con las filas <c>ChargeApplied</c> (−por cobrar, −capital) y el hecho <c>CargoPendienteCobrado</c> en la misma transacción. La cuenta ya está bloqueada.
    /// Sin cargos cobrables no hace nada.
    /// </summary>
    public async Task<Result<MemberAccountOperation?>> CobrarEnCancelacionAsync(MemberAccount cuenta, IReadOnlyList<(AccountCharge Cargo, decimal Monto)> cobrados,
        DateOnly fecha, string motivo, CancellationToken ct)
    {
        if (cobrados.Count == 0) return Result.Success<MemberAccountOperation?>(null);
        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<MemberAccountOperation?>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cuenta.Id, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.ChargeCollection, PrefijoCobroEnCancelacion, ct);
        var total = cobrados.Sum(c => c.Monto);
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.ChargeCollection,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = total,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = Accrual.LiquidacionDeIntereses.RecaudadorPropio,
            Reason = Recortar($"Cobro de cargos pendientes al cancelar la cuenta. {motivo}"),
            Payload = string.Create(CultureInfo.InvariantCulture,
                $"{{\"charges\":[{string.Join(",", cobrados.Select(c => $"\"{c.Cargo.PublicId:N}\""))}],\"amount\":{total}}}"),
            InputsSha256 = cobrados.Aggregate(new HuellaDeCartera().Identificador("account", cuenta.PublicId).Fecha("date", fecha),
                (h, c) => h.Identificador("charge", c.Cargo.PublicId)).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);
        var unidad = await emision.CargosCobradosEnCancelacionAsync(hecho.Value, operacion, cobrados, ct);
        if (unidad.IsFailure) return Result.Failure<MemberAccountOperation?>(unidad.Error);
        registro.RegistrarTransacciones(FilasDeCobro(cuenta, operacion, cobrados, fecha, hecho.Value.FechaContable, unidad.Value.De(CargoPendienteCobradoV1.Type)));
        foreach (var (cargo, _) in cobrados) cargo.Cobrar(operacion.Id);
        await db.SaveChangesAsync(ct);
        return Result.Success<MemberAccountOperation?>(operacion);
    }

    /// <summary>Las filas del cobro de unos cargos pendientes dentro de la operación de una consignación (<c>ChargeApplied</c> −por cobrar y −capital).</summary>
    public static IEnumerable<MemberAccountTransaction> FilasDeCobro(MemberAccount cuenta, MemberAccountOperation operacion, IReadOnlyList<(AccountCharge Cargo, decimal Monto)> cobrados,
        DateOnly fecha, DateOnly contable, Guid? mensaje)
    {
        foreach (var (_, monto) in cobrados)
        {
            yield return new MemberAccountTransaction
            {
                MemberAccountId = cuenta.Id, Operation = operacion, Kind = MemberAccountTransactionKind.ChargeApplied, Concept = MemberAccountConcept.ChargeReceivable,
                Amount = -monto, ValueDate = fecha, AccountingDate = contable, RegisteredAt = operacion.RegisteredAt, MessagePublicId = mensaje,
            };
            yield return new MemberAccountTransaction
            {
                MemberAccountId = cuenta.Id, Operation = operacion, Kind = MemberAccountTransactionKind.ChargeApplied, Concept = MemberAccountConcept.Principal,
                Amount = -monto, ValueDate = fecha, AccountingDate = contable, RegisteredAt = operacion.RegisteredAt, MessagePublicId = mensaje,
            };
        }
    }

    /// <summary>
    /// Condona un cargo (FR-128; api.md §4, <c>Lending.Charges.Waive</c>): el pendiente deja de deberse (<c>ChargeWaived</c> −por cobrar); el cobrado se
    /// reintegra al capital (<c>ChargeWaived</c> +capital). Operación <c>ChargeWaiver</c> y <c>CargoDeCuentaCondonado</c>. La cuenta ya está bloqueada.
    /// </summary>
    public async Task<Result<MemberAccountOperation>> CondonarAsync(MemberAccount cuenta, AccountCharge cargo, string motivo, int? decision, CancellationToken ct)
    {
        if (!cargo.Condonable)
            return Result.Failure<MemberAccountOperation>(new ErrorConDatos(AccountCharge.CodigoNoCondonable,
                $"El cargo {cargo.ChargeCode} está «{cargo.Status}»: sólo se condona uno pendiente o cobrado.", new { status = cargo.Status.ToString() }));
        var hoy = reloj.HoyLocal;
        var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
        if (hecho.IsFailure) return Result.Failure<MemberAccountOperation>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cuenta.Id, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.ChargeWaiver, PrefijoCondonacion, ct);
        var previo = cargo.Status;
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.ChargeWaiver,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = cargo.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            AuthorizationDecisionId = decision,
            Reason = Recortar(motivo),
            Payload = $"{{\"chargePublicId\":\"{cargo.PublicId:N}\",\"previousStatus\":\"{previo}\"}}",
            InputsSha256 = new HuellaDeCartera().Identificador("charge", cargo.PublicId).Texto("previous", previo.ToString()).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);
        cargo.Condonar(reloj.UtcNow, actor.UserId, motivo, decision);
        var pendiente = previo == AccountChargeStatus.Pending;
        var concepto = pendiente ? MemberAccountConcept.ChargeReceivable : MemberAccountConcept.Principal;
        var monto = pendiente ? -cargo.Amount : cargo.Amount;
        var valores = new List<DepositConceptAmountV1> { new() { Concept = concepto.ToString(), Amount = monto, ReasonCode = cargo.Kind.ToString() } };
        var unidad = await emision.CargoCondonadoAsync(hecho.Value, operacion, cargo, previo, valores, ct);
        if (unidad.IsFailure) return Result.Failure<MemberAccountOperation>(unidad.Error);
        registro.RegistrarTransacciones(
        [
            new MemberAccountTransaction
            {
                MemberAccountId = cuenta.Id, Operation = operacion, OperationId = operacion.Id, Kind = MemberAccountTransactionKind.ChargeWaived, Concept = concepto,
                Amount = monto, ValueDate = hoy, AccountingDate = hecho.Value.FechaContable, RegisteredAt = operacion.RegisteredAt,
                MessagePublicId = unidad.Value.De(CargoDeCuentaCondonadoV1.Type),
            },
        ]);
        await db.SaveChangesAsync(ct);
        return Result.Success(operacion);
    }

    private static string Recortar(string texto)
    {
        var t = texto.Trim();
        return t.Length <= 500 ? t : t[..500];
    }
}
