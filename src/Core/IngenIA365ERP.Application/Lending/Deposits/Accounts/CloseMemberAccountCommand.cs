using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

// Feature 014, US5 (T135; api.md §4, «close-preview» y «close»; FR-039): la cancelación de una cuenta de ahorro a la vista —liquida a la fecha, retiene,
// cobra el GMF de la entrega y entrega o traslada el saldo; no con bloqueos, embargos, canje, cargos por cobrar ni sobregiro—. (nuevo)

/// <summary>Lo que impide cancelar (api.md §4, <c>blockers</c>). (nuevo)</summary>
public sealed record CloseBlockerDto(string Kind, string Detail);

/// <summary>La vista previa de la cancelación (api.md §4, <c>GET /{id}/close-preview</c>). (nuevo)</summary>
public sealed record MemberAccountClosingPreviewDto(
    DateOnly Date,
    InterestSimulationDto? Settlement,
    decimal BalanceAfterSettlement,
    decimal Gmf,
    decimal PendingCharges,
    decimal ToDeliver,
    IReadOnlyList<CloseBlockerDto> Blockers,
    decimal? ContractualPenalty = null,
    string? ContractualExplanation = null)
{
    public bool CanClose => Blockers.Count == 0;
}

/// <summary>Cómo se entrega el saldo de una cancelación (api.md §4). (nuevo)</summary>
public enum CloseDeliveryKind
{
    TransferToAccount = 1,
    DisbursementOrder = 2,
}

public sealed record CloseDeliveryInput(CloseDeliveryKind Kind, Guid? TargetAccountPublicId, WithdrawalPayeeInput? Payee);

/// <summary>Lo que dejó la cancelación: la operación de cancelación, la liquidación, la entrega y el estado de la cuenta. (nuevo)</summary>
public sealed record MemberAccountClosingResultDto(
    Guid OperationPublicId,
    string OperationNumber,
    InterestSettlementDto? Settlement,
    MemberAccountOperationResultDto? Delivery,
    decimal Delivered,
    MemberAccountStatus Status);

/// <summary>
/// La cancelación de una cuenta (feature 014, US5, T135; FR-039): lo que la impide, lo que se liquidaría a la fecha y lo que se entregaría. Compartida por la
/// vista previa y el comando. El ahorro a la vista y, desde US7 (T182), el contractual se cancelan por aquí: los aportes y el ahorro permanente se cancelan con
/// el retiro del asociado (<c>Lending.Contributions.ClosureOnlyByWithdrawal</c>) y el CDAT por su ruta. El contractual vigente que se cancela antes de su
/// vencimiento se reliquida a la tasa de castigo (<c>Contractual.IncumplimientoDeContractual</c>); el vencido, a disposición, sólo se entrega. (nuevo)
/// </summary>
public sealed class CancelacionDeCuenta(IApplicationDbContext db, ReglasDeMovimientos movimientos, LiquidacionDeIntereses liquidacion, IDateTimeService reloj,
    Contractual.LectorDeContractual? contractual = null, CargosDeCuentas? cargos = null)
{
    /// <summary>US7 (T182): un contractual vigente que ya llegó a su vencimiento lo liquida el proceso diario (con la bonificación si se cumplió).</summary>
    public const string CodigoVencimientoPendiente = "Lending.Contractual.MaturityPending";
    public const string CodigoSoloPorRetiro = "Lending.Contributions.ClosureOnlyByWithdrawal";
    public const string CodigoFechaDeCancelacion = "Lending.Savings.CloseDateInvalid";

    /// <summary>Nulo si la cuenta se cancela por aquí; si no, el error.</summary>
    public static Error? Admite(MemberAccount cuenta)
    {
        if (cuenta.Nature == LineNature.Contribution || cuenta.DepositClass == DepositClass.Permanent)
            return new ErrorConDatos(CodigoSoloPorRetiro,
                $"La cuenta {cuenta.Number} se cancela sólo con el retiro del asociado (aportes y ahorro permanente).", new { nature = cuenta.Nature.ToString() });
        // US6 (T173): un CDAT se cancela o se redime por su ruta (con su plan, su penalidad y su destino).
        if (cuenta.DepositClass == DepositClass.TermDeposit)
            return new ErrorConDatos(ErroresDeCuentas.CodigoUsarRutaDeCdat, $"El CDAT {cuenta.Number} se cancela o se redime en «CDAT», con su plan y su destino.",
                new { route = "/api/lending/term-deposits/{id}/redeem" });
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive or MemberAccountStatus.Matured))
            return ErroresDeAportes.NoOperable(cuenta.Number, cuenta.Status);
        return null;
    }

    /// <summary>Lo que impide cancelar a la fecha (FR-039): embargo, canje, cargos por cobrar, sobregiro técnico, titular fallecido.</summary>
    public static IReadOnlyList<CloseBlockerDto> Bloqueos(MemberAccount cuenta, CuentaParaMovimiento leida)
    {
        var bloqueos = new List<CloseBlockerDto>();
        if (cuenta.IsGarnished) bloqueos.Add(new("Garnishment", "La cuenta está embargada."));
        if (leida.Disponible.Retenido > 0m) bloqueos.Add(new("Hold", $"Tiene {leida.Disponible.Retenido:N2} bloqueados."));
        if (leida.Disponible.EnCanje > 0m) bloqueos.Add(new("PendingClearance", $"Tiene {leida.Disponible.EnCanje:N2} en canje."));
        if (cuenta.TechnicalNegativeSince is not null) bloqueos.Add(new("TechnicalOverdraft", "La cuenta tiene un sobregiro técnico."));
        if (leida.Titular.IsDeceased) bloqueos.Add(new("Deceased", "El titular figura como fallecido: la entrega es por el trámite de fallecimiento (K10)."));
        return bloqueos;
    }

    /// <summary>
    /// Respuesta 65 B del dueño (E255): los cargos por cobrar ya no impiden cancelar: se cobran del saldo dentro de la misma cancelación y lo restante se entrega.
    /// Sólo impide la parte que el capital no alcanza a cubrir (los cargos se cobran enteros): ésa se condona antes.
    /// </summary>
    public static CloseBlockerDto? CargosSinCubrir(decimal sinCubrir) => sinCubrir <= 0m ? null
        : new("PendingCharges", $"Tiene {sinCubrir:N2} en cargos por cobrar que el saldo no alcanza a cubrir: condónelos antes de cancelar.");

    /// <summary>Los cargos pendientes que la cancelación cobra de <paramref name="capital"/> y lo que éste no alcanza a cubrir.</summary>
    public async Task<(IReadOnlyList<(AccountCharge Cargo, decimal Monto)> Cobrables, decimal SinCubrir)> CargosDeLaCancelacionAsync(MemberAccount cuenta, decimal capital,
        CancellationToken ct)
    {
        if (cargos is null) return ([], 0m);
        return await cargos.PendientesDeLaCancelacionAsync(cuenta, capital, ct);
    }

    /// <summary>
    /// Lo que se entrega de <paramref name="saldo"/> con el GMF de la cancelación: el mayor valor cuya suma con su GMF no pasa del saldo (el GMF se redondea,
    /// así que se busca hacia abajo unos pocos pesos). Devuelve la entrega y el GMF.
    /// </summary>
    public async Task<(decimal Entrega, decimal Gmf)> EntregaAsync(CuentaParaMovimiento leida, decimal saldo, DateOnly fecha, CancellationToken ct,
        string? hechoGenerador = null)
    {
        if (saldo <= 0m) return (0m, 0m);
        var monto = saldo;
        for (var intento = 0; intento < 6; intento++)
        {
            // US7 (T182, research §19): la entrega por traslado se mide con el hecho generador del traslado (entre ahorros del mismo titular está exento): con el
            // de la cancelación quedaba en la cuenta el GMF que el traslado no cobra y la cuenta no se cancelaba.
            var gmf = await movimientos.GmfAsync(leida, hechoGenerador ?? HechosGeneradoresDeGmf.Cancelacion, monto, fecha, ct);
            var valor = gmf.IsSuccess ? gmf.Value?.Valor ?? 0m : 0m;
            if (monto + valor <= saldo) return await HastaElSaldoAsync(leida, hechoGenerador ?? HechosGeneradoresDeGmf.Cancelacion, monto, valor, saldo, fecha, ct);
            monto -= Math.Max(RedondeoDeCartera.Centavo, monto + valor - saldo);
            if (monto <= 0m) return (0m, 0m);
        }
        return (0m, 0m);
    }

    /// <summary>
    /// Corrida del 2026-10-26 (feature 015, la cancelación con entrega por el contrato): bajar el monto por el exceso entero puede quedarse corto, porque el GMF se
    /// redondea al peso —400.000 con el 4 × 1.000 daba 398.400 + 1.594 y dejaba 6 pesos, y la cuenta no se cancelaba—. Se sube hacia el saldo mientras el monto
    /// más su GMF quepan en él.
    /// </summary>
    private async Task<(decimal Entrega, decimal Gmf)> HastaElSaldoAsync(CuentaParaMovimiento leida, string hecho, decimal monto, decimal valor, decimal saldo,
        DateOnly fecha, CancellationToken ct)
    {
        for (var intento = 0; intento < 6 && monto + valor < saldo; intento++)
        {
            var candidato = saldo - valor;
            if (candidato <= monto) break;
            var gmf = await movimientos.GmfAsync(leida, hecho, candidato, fecha, ct);
            var suyo = gmf.IsSuccess ? gmf.Value?.Valor ?? 0m : 0m;
            if (candidato + suyo > saldo) break;
            (monto, valor) = (candidato, suyo);
        }
        return (monto, valor);
    }

    public async Task<Result<MemberAccountClosingPreviewDto>> PrevisualizarAsync(MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        if (Admite(cuenta) is { } no) return Result.Failure<MemberAccountClosingPreviewDto>(no);
        var leida = await movimientos.LeerAsync(cuenta, fecha, ct);
        var bloqueos = Bloqueos(cuenta, leida).ToList();
        var control = await db.MemberAccountControls.AsNoTracking().FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        InterestSimulationDto? simulada = null;
        var neto = 0m;
        if (LiquidacionDeIntereses.GanaInteres(cuenta))
        {
            var r = await liquidacion.CalcularAsync(cuenta, control, fecha, reloj.UtcNow, desdeElMotor: true, ct);
            if (r.IsFailure) return Result.Failure<MemberAccountClosingPreviewDto>(r.Error);
            var c = r.Value;
            neto = c.Neto;
            simulada = new InterestSimulationDto(fecha, reloj.UtcNow, c.Desde.AddDays(1), c.Hasta, c.Causado, c.Bruto, c.Redondeo,
                new InterestWithholdingDto(c.Retencion.Base, c.Retencion.ComponenteInflacionario, c.Retencion.ComponenteInflacionarioValor, c.Retencion.BaseGravable,
                    c.Retencion.Tarifa, c.Retencion.UmbralDiario, c.Retencion.UmbralAplica, c.Retencion.DiasQueAlcanzan, c.Retencion.Valor, c.Retencion.MotivoDeExencion,
                    c.Perfil.ToString(), c.Retencion.Explicacion), c.Neto, []);
        }
        // Lo causado sin liquidar ya está en el saldo del titular (interés por pagar): después de liquidar, el saldo es el capital más el neto.
        var capital = leida.Saldos.GetValueOrDefault(MemberAccountConcept.Principal);
        var despues = capital + neto - leida.Saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable);
        // Respuesta 65 B (E255): los cargos por cobrar se cobran del saldo al cancelar; sólo la parte que no alcanza a cubrirse impide.
        var (_, sinCubrir) = await CargosDeLaCancelacionAsync(cuenta, cuenta.DepositClass == DepositClass.Contractual ? capital : capital + neto, ct);
        if (cargos is null) sinCubrir = leida.Saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable);
        if (CargosSinCubrir(sinCubrir) is { } sinCobertura) bloqueos.Add(sinCobertura);
        // US7 (T182): el contractual vigente que se cancela antes de vencer pierde lo que la base le dio de más a sus aportes (la tasa de castigo).
        decimal? penalidad = null;
        string? explicacion = null;
        if (cuenta.DepositClass == DepositClass.Contractual && contractual is not null)
        {
            var anticipada = await PenalidadDeContractualAsync(cuenta, fecha, ct);
            if (anticipada.IsFailure) return Result.Failure<MemberAccountClosingPreviewDto>(anticipada.Error);
            (penalidad, explicacion) = anticipada.Value;
            despues -= penalidad ?? 0m;
        }
        var (entrega, gmf) = await EntregaAsync(leida, despues, fecha, ct);
        return Result.Success(new MemberAccountClosingPreviewDto(fecha, simulada, despues, gmf, leida.Saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable),
            entrega, bloqueos, penalidad, explicacion));
    }

    /// <summary>
    /// US7 (T182): lo que cuesta cancelar el contractual a <paramref name="fecha"/>: si el contrato está vigente y no ha vencido, la reliquidación de sus aportes
    /// a la tasa de castigo (cero sin tasa de castigo); incumplido o vencido, nada. Un contrato vencido que el proceso todavía no liquidó no se cancela por aquí.
    /// </summary>
    public async Task<Result<(decimal? Penalidad, string? Explicacion)>> PenalidadDeContractualAsync(MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        if (contractual is null || cuenta.DepositClass != DepositClass.Contractual || cuenta.Status == MemberAccountStatus.Matured
            || cuenta.ContractualStatus != ContractualStatus.Current)
            return Result.Success<(decimal?, string?)>((null, null));
        var leido = await contractual.LeerAsync(cuenta, ct);
        if (leido.Inicial is not { } inicial) return Result.Success<(decimal?, string?)>((null, null));
        var plan = Contractual.LectorDeContractual.Plan(inicial);
        if (fecha >= plan.Vencimiento)
            return Result.Failure<(decimal?, string?)>(new ErrorConDatos(CodigoVencimientoPendiente,
                $"El contrato de la cuenta {cuenta.Number} venció el {ErroresDeCuentas.Fecha(plan.Vencimiento)}: el proceso diario lo liquida (con su bonificación si se "
                + "cumplió) y lo entrega; sin instrucción queda a disposición y entonces se cancela.", new { maturityDate = plan.Vencimiento }));
        if (inicial.BreachAnnualRate is not { } castigo || castigo >= inicial.AppliedAnnualRate)
            return Result.Success<(decimal?, string?)>((0m, "La línea no tiene tasa de castigo menor que la base: cancelar antes no cuesta interés."));
        var c = Contractual.LectorDeContractual.Condiciones(inicial);
        var r = AhorroContractual.Reliquidar(await contractual.AportesAsync(leido, fecha, ct), c.TasaBase, castigo, c.Convencion, fecha, c.Redondeo);
        return Result.Success<(decimal?, string?)>((r.Diferencia, r.Explicacion));
    }
}

// ------------------------------------------------------------------------------------------------------------- vista previa --

/// <summary>La vista previa de la cancelación (api.md §4, <c>GET /{id}/close-preview?date=</c>, <c>Lending.Accounts.Read</c>). Sin efectos. (nuevo)</summary>
public sealed record PreviewMemberAccountClosingQuery(Guid AccountPublicId, DateOnly? Date = null) : IRequest<Result<MemberAccountClosingPreviewDto>>;

public sealed class PreviewMemberAccountClosingQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    CancelacionDeCuenta cancelacion,
    IDateTimeService reloj) : IRequestHandler<PreviewMemberAccountClosingQuery, Result<MemberAccountClosingPreviewDto>>
{
    public async Task<Result<MemberAccountClosingPreviewDto>> Handle(PreviewMemberAccountClosingQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountClosingPreviewDto>(leida.Error);
        var fecha = request.Date ?? reloj.HoyLocal;
        var vista = await cancelacion.PrevisualizarAsync(leida.Value, fecha, ct);
        // US16 (T164): la vista previa muestra el saldo, la liquidación y lo que se entregaría: lectura sensible.
        if (vista.IsSuccess) await auditoria.EmitirSaldoDeCuentaVistoAsync(leida.Value.PublicId, "close-preview", new { date = fecha }, ct);
        return vista;
    }
}

// ----------------------------------------------------------------------------------------------------------------- cancelar --

/// <summary>
/// La cancelación de una cuenta de ahorro a la vista (feature 014, US5, T135; api.md §4, <c>POST /{id}/close</c>, <c>Lending.Accounts.Close</c>, con clave;
/// FR-039), hoy y con las firmas del manejo, en una transacción con el cerrojo de la persona y de las cuentas:
/// <list type="number">
/// <item>sin bloqueos (<c>Lending.Savings.CloseBlocked</c> con la lista: embargo, bloqueo, canje, cargos por cobrar, sobregiro, fallecido);</item>
/// <item>nace la operación <c>Cancellation</c> (<c>CAN-</c>), pone al día la causación hasta hoy en ella (partes <c>ALD-</c>), liquida los períodos vencidos y
/// el último hasta hoy con su retención —antes de entregar— (<c>InteresDeCaptacionLiquidado</c> + <c>RetencionPracticada</c>, origen <c>AccountClosing</c>);</item>
/// <item>entrega todo el saldo con el GMF de la cancelación (hecho generador <c>Cancellation</c>): por orden de giro a Tesorería (la cuenta queda en cierre
/// y se cancela cuando el giro se confirma; si se rechaza, vuelve a activa con su saldo) o por traslado a otra cuenta (se cancela en el acto).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record CloseMemberAccountCommand(Guid AccountPublicId, DateOnly? Date, CloseDeliveryInput Delivery, IReadOnlyList<Guid> Signatures, string Reason)
    : IRequest<Result<MemberAccountClosingResultDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseMemberAccountCommandValidator : ValidadorConMotivo<CloseMemberAccountCommand>
{
    public CloseMemberAccountCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Delivery).NotNull();
        RuleFor(x => x.Delivery.Kind).IsInEnum().When(x => x.Delivery is not null);
        RuleFor(x => x.Delivery.TargetAccountPublicId).NotEmpty()
            .When(x => x.Delivery is { Kind: CloseDeliveryKind.TransferToAccount }).WithMessage("El traslado nombra la cuenta destino.");
        RuleFor(x => x.Signatures).NotNull();
    }
}

public sealed class CloseMemberAccountCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ICerrojoDeCartera cerrojo,
    CancelacionDeCuenta cancelacion,
    CausacionDeCaptaciones causacion,
    LiquidacionDeIntereses liquidacion,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    TrasladoDeCuenta traslados,
    OrdenesDeGiroDeCuentas ordenes,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null,
    Contractual.LectorDeContractual? lectorDeContractual = null,
    Contractual.IncumplimientoDeContractual? incumplimiento = null,
    Contractual.CierreDeContractual? cierreDeContractual = null,
    CargosDeCuentas? cargosDeCuentas = null) : IRequestHandler<CloseMemberAccountCommand, Result<MemberAccountClosingResultDto>>
{
    /// <summary>El prefijo del comprobante de una cancelación.</summary>
    public const string PrefijoCancelacion = "CAN-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<MemberAccountClosingResultDto>> Handle(CloseMemberAccountCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var fecha = request.Date ?? hoy;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var vista = leida.Value;
        if (CancelacionDeCuenta.Admite(vista) is { } no) return Falla(no);
        if (fecha != hoy)
            return Falla(new ErrorConDatos(CancelacionDeCuenta.CodigoFechaDeCancelacion, $"La cuenta se cancela con la fecha de hoy ({ErroresDeCuentas.Fecha(hoy)}).",
                new { date = fecha, today = hoy }));

        MemberAccount? destino = null;
        var personaDelGiro = vista.HolderPersonId;
        BeneficiarioDeOrden? banco = null;
        if (request.Delivery.Kind == CloseDeliveryKind.TransferToAccount)
        {
            // US16 (T162): la cuenta que recibe la entrega también tiene que estar en el alcance (fuera, el mismo 404 que si no existiera).
            var otra = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.Delivery.TargetAccountPublicId ?? Guid.Empty, seguir: false, ct);
            if (otra.IsFailure) return Falla(otra.Error);
            destino = otra.Value;
            if (destino.Id == vista.Id) return Falla(ErroresDeMovimientos.MismaCuenta());
        }
        else
        {
            if (request.Delivery.Payee?.PersonPublicId is { } beneficiario && beneficiario != Guid.Empty)
                personaDelGiro = await db.People.AsNoTracking().Where(p => p.PublicId == beneficiario).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? personaDelGiro;
            var b = await ordenes.BeneficiarioAsync(personaDelGiro, request.Delivery.Payee?.BankPublicId, request.Delivery.Payee?.AccountType,
                request.Delivery.Payee?.AccountNumber, ct);
            if (b.IsFailure) return Falla(b.Error);
            banco = b.Value;
        }

        // US8 (T145; D23): la pantalla no cancela si lo que la cancelación emite no se contabilizaría: la liquidación a la fecha con su retención (si la
        // cuenta causó intereses alguna vez) y la entrega —giro o traslado— con su GMF.
        if (validacionPrevia is not null)
        {
            var dimensiones = await validacionPrevia.DimensionesAsync(destino is null ? [vista.Id] : [vista.Id, destino.Id], fecha, ct);
            if (!dimensiones.TryGetValue(vista.Id, out var d))
            {
                var sinSucursal = await emision.SucursalContableAsync(vista.BranchId, ct);
                if (sinSucursal.IsFailure) return Falla(sinSucursal.Error);
                d = (await validacionPrevia.DimensionesAsync([vista.Id], fecha, ct))[vista.Id];
            }
            var hechos = new List<IngenIA365ERP.Application.Common.Integration.Accounting.MensajeContableDto>();
            if (await db.MemberAccountTransactions.AsNoTracking().AnyAsync(t => t.MemberAccountId == vista.Id && t.Concept == MemberAccountConcept.InterestPayable, ct))
                hechos.AddRange(HechosRepresentativosDeCaptaciones.Unidad(fecha, d, HechosRepresentativosDeCaptaciones.Liquidacion(d),
                    HechosRepresentativosDeCaptaciones.Retencion(d)));
            object entrega = destino is not null && dimensiones.TryGetValue(destino.Id, out var dDestino)
                ? HechosRepresentativosDeCaptaciones.Traslado(d, dDestino)
                : HechosRepresentativosDeCaptaciones.Retiro(d, null);
            hechos.AddRange(HechosRepresentativosDeCaptaciones.Unidad(fecha, d, entrega, HechosRepresentativosDeCaptaciones.Gmf(d)));
            // Respuesta 65 B (E255): los cargos por cobrar que la cancelación cobra del saldo.
            if (cargosDeCuentas is not null && await db.AccountCharges.AsNoTracking()
                    .AnyAsync(c => c.MemberAccountId == vista.Id && c.Status == AccountChargeStatus.Pending && !c.IsDeleted, ct))
                hechos.AddRange(HechosRepresentativosDeCaptaciones.Unidad(fecha, d, HechosRepresentativosDeCaptaciones.CargoCobrado(d)));
            // US7 (T183): el contractual vigente que se cancela antes de vencer, con la penalidad del incumplimiento.
            if (vista.DepositClass == DepositClass.Contractual && vista.ContractualStatus == ContractualStatus.Current && vista.Status != MemberAccountStatus.Matured)
                hechos.AddRange(HechosRepresentativosDeCaptaciones.Unidad(fecha, d, HechosRepresentativosDeCaptaciones.PenalidadDeContractual(d)));
            var decision = await validacionPrevia.EnPantallaAsync(hechos, $"la cancelación de la cuenta {vista.Number}", ct);
            if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            var personas = new[] { vista.HolderPersonId, destino?.HolderPersonId ?? vista.HolderPersonId }.Distinct().Order().ToList();
            await cerrojo.BloquearDeudoresAsync(personas, ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(new[] { vista.Id, destino?.Id ?? vista.Id }.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == vista.Id, ct);
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
            var antes = await movimientos.LeerAsync(cuenta, fecha, ct);
            var bloqueos = CancelacionDeCuenta.Bloqueos(cuenta, antes);
            if (bloqueos.Count > 0) return Falla(ErroresDeMovimientos.CierreBloqueado(bloqueos.Cast<object>().ToList()));
            // Respuesta 65 B (E255): sin quien los cobre (construcción sin CargosDeCuentas), lo por cobrar sigue impidiendo.
            if (cargosDeCuentas is null && antes.Saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable) > 0m)
                return Falla(ErroresDeMovimientos.CierreBloqueado([new CloseBlockerDto("PendingCharges", "Tiene cargos por cobrar: cóbrelos o condónelos antes de cancelar.")]));
            // US7 (T182): el contractual se cancela con su contrato (la penalidad si es antes de vencer) y la entrega común del contractual.
            if (cuenta.DepositClass == DepositClass.Contractual && lectorDeContractual is not null && incumplimiento is not null && cierreDeContractual is not null)
                return await CancelarContractualAsync(cuenta, fecha, destino, personaDelGiro, banco, request, ct);

            // 1. La operación de la cancelación: en ella van la puesta al día de la causación y la liquidación a la fecha.
            var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
            if (hecho.IsFailure) return Falla(hecho.Error);
            var actor = await actorActual.ObtenerAsync(ct);
            var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Cancellation, PrefijoCancelacion, ct);
            var motivo = request.Reason.Trim();
            var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
            {
                MemberAccountId = cuenta.Id,
                CounterpartMemberAccountId = destino?.Id,
                Kind = MemberAccountOperationKind.Cancellation,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = fecha,
                AccountingDate = hecho.Value.FechaContable,
                RegisteredAt = sello.RegisteredAt,
                Amount = 0m,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
                Reason = motivo.Length <= 500 ? motivo : motivo[..500],
                Payload = JsonSerializer.Serialize(new { account = cuenta.PublicId, date = fecha, delivery = request.Delivery, signatures = request.Signatures }, Json),
                InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Fecha("date", fecha).Texto("delivery", request.Delivery.Kind.ToString())
                    .Identificador("target", destino?.PublicId).Calcular(),
                RulesVersion = VersionesDeReglas.Vigente,
            });
            await db.SaveChangesAsync(ct);

            InterestSettlementDto? liquidada = null;
            if (LiquidacionDeIntereses.GanaInteres(cuenta))
            {
                var alDia = await causacion.PonerAlDiaAsync(cuenta, fecha, operacion, ct);
                if (alDia.IsFailure) return Falla(alDia.Error);
                await db.SaveChangesAsync(ct);
                foreach (var fin in (await liquidacion.PeriodosVencidosAsync(cuenta, control, fecha, ct)).Where(f => f < fecha))
                {
                    var vencida = await liquidacion.LiquidarAsync(cuenta, control, fin, null, null, motivo, ct);
                    if (vencida.IsFailure) return Falla(vencida.Error);
                }
                var ultima = await liquidacion.LiquidarAsync(cuenta, control, fecha, null, operacion, motivo, ct);
                if (ultima.IsFailure) return Falla(ultima.Error);
                if (ultima.Value is { Liquidacion: { } l, Operacion: { } o }) liquidada = Liquidaciones.Dto(l, o, ultima.Value.YaExistia);
            }

            // 1b. Respuesta 65 B (E255): los cargos por cobrar se cobran del capital (ya con los intereses liquidados) en su propia operación CGC-, antes de entregar.
            var cobradosAlCancelar = await CobrarCargosAsync(cuenta, fecha, motivo, ct);
            if (cobradosAlCancelar.IsFailure) return Falla(cobradosAlCancelar.Error);

            // 2. La entrega de todo el saldo con el GMF de la cancelación.
            var despues = await movimientos.LeerAsync(cuenta, fecha, ct);
            var saldo = despues.Saldo - despues.Disponible.Retenido - despues.Disponible.Embargado - despues.Disponible.EnCanje;
            var (entrega, _) = await cancelacion.EntregaAsync(despues, saldo, fecha, ct,
                destino is null ? null : HechosGeneradoresDeGmf.DeTraslado(cuenta.DepositClass, destino.DepositClass, cuenta.HolderPersonId == destino.HolderPersonId));
            MemberAccountOperationResultDto? resultado = null;
            if (entrega > 0m)
            {
                if (destino is not null)
                {
                    var t = await traslados.AplicarAsync(Pantalla.Traslado(cuenta.PublicId, destino.PublicId, entrega, fecha, request.Signatures, null, motivo)
                        with { EsCancelacion = true }, ct);
                    if (t.IsFailure) return Falla(t.Error);
                    resultado = new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, t.Value.Comprobante, null, null, null);
                }
                else
                {
                    var r = await retiros.AplicarAsync(Pantalla.Retiro(cuenta.PublicId, entrega, fecha, ClaseDeMedioDeRecaudo.Transfer, request.Signatures, null, motivo,
                        new OrdenDeRetiro(personaDelGiro, banco!)) with
                    {
                        EsCancelacion = true, ExigeAtribucion = false, HechoGenerador = HechosGeneradoresDeGmf.Cancelacion,
                    }, ct);
                    if (r.IsFailure) return Falla(r.Error);
                    var orden = r.Value.Orden;
                    resultado = new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, r.Value.Comprobante,
                        orden is null ? null : new MemberAccountOrderDto(orden.PublicId, orden.Number, orden.Status.ToString()), null, null);
                }
            }

            // 3. El estado: cancelada si ya no queda saldo y no hay giro en curso; en cierre mientras Tesorería gira.
            var final = await movimientos.LeerAsync(cuenta, fecha, ct);
            var ahora = reloj.UtcNow;
            var enGiro = destino is null && entrega > 0m;
            if (cuenta.Status != MemberAccountStatus.Closing) cuenta.PedirCierre(ahora);
            if (!enGiro && final.Saldo == 0m) cuenta.Cerrar(fecha, actor.UserId, motivo, ahora);
            await db.SaveChangesAsync(ct);
            return Result.Success(new MemberAccountClosingResultDto(operacion.PublicId, operacion.Number, liquidada, resultado, entrega, cuenta.Status));
        }, ct);
    }

    /// <summary>
    /// Respuesta 65 B (E255): cobra del capital los cargos pendientes de la cuenta que se cancela (enteros, en orden de causación). Si el capital no alcanza a
    /// cubrirlos todos, la cancelación no sigue (<c>CloseBlocked</c> con <c>PendingCharges</c>) y la transacción se deshace.
    /// </summary>
    private async Task<Result> CobrarCargosAsync(MemberAccount cuenta, DateOnly fecha, string motivo, CancellationToken ct)
    {
        if (cargosDeCuentas is null) return Result.Success();
        var actual = await movimientos.LeerAsync(cuenta, fecha, ct);
        var (cobrables, sinCubrir) = await cargosDeCuentas.PendientesDeLaCancelacionAsync(cuenta, actual.Saldos.GetValueOrDefault(MemberAccountConcept.Principal), ct);
        if (CancelacionDeCuenta.CargosSinCubrir(sinCubrir) is { } sinCobertura)
            return Result.Failure(ErroresDeMovimientos.CierreBloqueado([sinCobertura]));
        var cobro = await cargosDeCuentas.CobrarEnCancelacionAsync(cuenta, cobrables, fecha, motivo, ct);
        return cobro.IsFailure ? Result.Failure(cobro.Error) : Result.Success();
    }

    /// <summary>
    /// US7 (T182; FR-074): la cancelación de un ahorro contractual, dentro de la transacción y el cerrojo del comando. Vigente y antes de su vencimiento, queda
    /// incumplido con la reliquidación a la tasa de castigo (<c>CancelacionAnticipada</c>); incumplido o vencido, sólo se entrega. Después liquida a la fecha,
    /// anula las cuotas pendientes y entrega al destino pedido con las firmas del manejo.
    /// </summary>
    private async Task<Result<MemberAccountClosingResultDto>> CancelarContractualAsync(MemberAccount cuenta, DateOnly fecha, MemberAccount? destino,
        int personaDelGiro, BeneficiarioDeOrden? banco, CloseMemberAccountCommand request, CancellationToken ct)
    {
        var motivo = request.Reason.Trim();
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var leido = await lectorDeContractual!.LeerAsync(cuenta, ct);
        if (leido.Inicial is null) return Falla(Contractual.ErroresDeContractual.SinCondiciones(cuenta.Number));
        var costo = await cancelacion.PenalidadDeContractualAsync(cuenta, fecha, ct);
        if (costo.IsFailure) return Falla(costo.Error);
        Guid? operacion = null;
        string? numero = null;
        long? operacionId = null;
        if (cuenta.ContractualStatus == ContractualStatus.Current && cuenta.Status != MemberAccountStatus.Matured)
        {
            var plan = Contractual.LectorDeContractual.Plan(leido.Inicial);
            var cuotas = await lectorDeContractual.CuotasAsync(cuenta, plan, ct);
            var cumplimiento = await lectorDeContractual.CumplimientoAsync(cuenta, leido.Inicial, plan, cuotas, fecha, ct);
            var aplicado = await incumplimiento!.AplicarAsync(cuenta, control, leido, fecha, Contractual.CausasDeIncumplimiento.CancelacionAnticipada,
                cumplimiento.Atrasadas, porElProceso: false, PrevalidationOutcome.NotApplicable, motivo, ct);
            if (aplicado.IsFailure) return Falla(aplicado.Error);
            (operacion, numero, operacionId) = (aplicado.Value.Operacion.PublicId, aplicado.Value.Operacion.Number, aplicado.Value.Operacion.Id);
        }
        var cobrados = await CobrarCargosAsync(cuenta, fecha, motivo, ct);
        if (cobrados.IsFailure) return Falla(cobrados.Error);
        var entrega = destino is not null
            ? new Contractual.EntregaDeContractual(CloseDeliveryKind.TransferToAccount, destino, null, null)
            : new Contractual.EntregaDeContractual(CloseDeliveryKind.DisbursementOrder, null, personaDelGiro, banco);
        var terminado = await cierreDeContractual!.TerminarAsync(cuenta, fecha, entrega, motivo, request.Signatures, exigeFirmas: true, operacionId, ct);
        if (terminado.IsFailure) return Falla(terminado.Error);
        var entregada = terminado.Value.Entrega?.Operation;
        return Result.Success(new MemberAccountClosingResultDto(operacion ?? entregada?.OperationPublicId ?? Guid.Empty, numero ?? entregada?.Number ?? string.Empty,
            null, terminado.Value.Entrega, terminado.Value.Entregado, cuenta.Status));
    }

    private static Result<MemberAccountClosingResultDto> Falla(Error e) => Result.Failure<MemberAccountClosingResultDto>(e);
}
