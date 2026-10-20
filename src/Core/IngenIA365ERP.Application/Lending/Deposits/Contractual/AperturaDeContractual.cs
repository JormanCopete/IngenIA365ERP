using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

/// <summary>
/// La constitución del contrato al abrir un ahorro contractual (feature 014, US7, T182; FR-007, FR-024, FR-074; research §19): dentro de la transacción de la
/// apertura (<see cref="AperturaDeCuentas"/>), con la cuenta ya numerada:
/// <list type="number">
/// <item>el contrato pedido cabe en la versión vigente de la línea —la cuota no baja de la mínima, el plazo en meses está en su rango y alcanza para una cuota
/// de la periodicidad— (<see cref="AhorroContractual.ValidarContrato"/>);</item>
/// <item>la tasa base es la de la línea para lo pactado (la variable del día, el tramo por saldo de lo pactado o la de la versión) y se sella con la bonificada,
/// la de castigo, la tolerancia, el efecto del incumplimiento y el reconocimiento de la bonificación de la línea (FR-007: el contractual sella sus condiciones
/// al constituirse; un cambio de la línea no lo toca);</item>
/// <item>la instrucción al vencimiento, si se da: una cuenta de ahorro a la vista viva del titular en el alcance, o un giro (el titular con su cuenta bancaria
/// registrada o el beneficiario nombrado);</item>
/// <item>la operación <c>ContractualSetup</c> (<c>ACT-</c>, valor cero) con la versión 1 de condiciones (causa <c>ContractualSetup</c>) por
/// <see cref="RegistroDeCuentas.SellarCondiciones"/> y la primera cuota exigible del plan (<c>GeneratedByOperationId</c>), para que se pueda pagar desde el
/// primer día; las siguientes las genera el paso de captaciones.</item>
/// </list>
/// No emite hechos: abrir un contrato no mueve dinero. Guarda. (nuevo)
/// </summary>
public sealed class AperturaDeContractual(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    LectorDeContractual lector,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    OrdenesDeGiroDeCuentas ordenes,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El prefijo del comprobante de la constitución de un contractual.</summary>
    public const string PrefijoConstitucion = "ACT-";

    /// <summary>
    /// Lo que la apertura le pide al contrato: nada si la cuenta no es contractual ni trae contrato; el error si falta o sobra. Llama a
    /// <see cref="SellarAsync"/> con la cuenta recién numerada.
    /// </summary>
    public Func<MemberAccount, Task<Result>> DentroDeLaApertura(ContractualTermsInput? pedido) => async cuenta =>
    {
        if (cuenta.DepositClass != DepositClass.Contractual)
            return pedido is null ? Result.Success() : Result.Failure(ErroresDeContractual.CondicionesNoAdmitidas());
        if (pedido is null) return Result.Failure(ErroresDeContractual.CondicionesRequeridas());
        var sellada = await SellarAsync(cuenta, pedido, reloj.HoyLocal, CancellationToken.None);
        return sellada.IsFailure ? Result.Failure(sellada.Error) : Result.Success();
    };

    /// <summary>
    /// La revisión previa del contrato pedido, antes de abrir la cuenta (para no abrirla si el contrato no cabe): la línea contractual o no, el contrato en la
    /// versión vigente, la tasa base y el destino de la instrucción. Nulo si se puede abrir.
    /// </summary>
    public async Task<Error?> RevisarAsync(OpenAccountInput e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var hoy = reloj.HoyLocal;
        var lineaId = await db.CreditLines.AsNoTracking().Where(l => l.PublicId == e.LinePublicId && !l.IsDeleted).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        var version = lineaId is { } id ? await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, id, hoy, null, ct) : null;
        if (version?.DepositClass != DepositClass.Contractual) return e.Contractual is null ? null : ErroresDeContractual.CondicionesNoAdmitidas();
        if (e.Contractual is not { } pedido) return ErroresDeContractual.CondicionesRequeridas();
        var revisado = await ContratoAsync(version, pedido, hoy, ct);
        if (revisado.IsFailure) return revisado.Error;
        if (pedido.MaturityInstruction is { } instruccion)
        {
            var titular = await db.People.AsNoTracking().Where(p => p.PublicId == e.HolderPersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);
            if (titular is { } t && (await DestinoAsync(0, t, instruccion, version, ct)) is { IsFailure: true } malo) return malo.Error;
        }
        return null;
    }

    /// <summary>El plan del contrato pedido con la tasa base de la línea, o el error si no cabe en ella.</summary>
    private async Task<Result<PlanDeContractualCalculado>> ContratoAsync(DepositLineVersion version, ContractualTermsInput pedido, DateOnly hoy, CancellationToken ct)
    {
        if (AhorroContractual.ValidarContrato(pedido.Installment, pedido.TermMonths, version.InstallmentPeriodicity ?? RepaymentPeriodicity.SinglePayment,
                version.MinInstallmentAmount, version.MinTermMonths, version.MaxTermMonths) is { } problema)
            return Result.Failure<PlanDeContractualCalculado>(ErroresDeContractual.DelContrato(problema));
        if (RegistroDeCuentas.Decimales(pedido.Installment) > MemberAccountTransaction.DecimalesDeOperacion)
            return Result.Failure<PlanDeContractualCalculado>(ErroresDeContractual.DelContrato(ProblemaDeCartera.Con(AhorroContractual.CodigoCuotaBajoElMinimo,
                "La cuota lleva a lo sumo dos decimales.", "installment", pedido.Installment)));
        // La tasa base de lo pactado (cuota x cuotas del plan) y, con ella, el plan sellado.
        var periodicidad = version.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly;
        var cuotas = AhorroContractual.Cuotas(hoy, periodicidad, AhorroContractual.Vencimiento(hoy, pedido.TermMonths));
        if (cuotas.Count == 0)
            return Result.Failure<PlanDeContractualCalculado>(ErroresDeContractual.DelContrato(ProblemaDeCartera.Con(AhorroContractual.CodigoSinCuotas,
                $"Un plazo de {pedido.TermMonths} meses no alcanza para una cuota «{periodicidad}».", "termMonths", pedido.TermMonths)));
        var tasa = await lector.TasaBaseAsync(version, pedido.Installment * cuotas.Count, hoy, ct);
        if (tasa.IsFailure) return Result.Failure<PlanDeContractualCalculado>(tasa.Error);
        return Result.Success(AhorroContractual.Plan(Condiciones(version, pedido, hoy, tasa.Value)));
    }

    /// <summary>Sella el contrato de una cuenta contractual recién abierta (ver la clase). Guarda.</summary>
    public async Task<Result<MemberAccountTermsVersion>> SellarAsync(MemberAccount cuenta, ContractualTermsInput pedido, DateOnly hoy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(pedido);
        var version = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, hoy, null, ct);
        if (version is null || version.DepositClass != DepositClass.Contractual) return Falla(ErroresDeContractual.CondicionesNoAdmitidas());
        var contrato = await ContratoAsync(version, pedido, hoy, ct);
        if (contrato.IsFailure) return Falla(contrato.Error);
        var plan = contrato.Value;
        var tasa = plan.Condiciones.TasaBase;

        DestinoDeContractual? destino = null;
        if (pedido.MaturityInstruction is { } instruccion)
        {
            var resuelto = await DestinoAsync(cuenta.Id, cuenta.HolderPersonId, instruccion, version, ct);
            if (resuelto.IsFailure) return Falla(resuelto.Error);
            destino = resuelto.Value;
        }

        var control = db.MemberAccountControls.Local.FirstOrDefault(c => c.MemberAccountId == cuenta.Id)
                      ?? await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.ContractualSetup, PrefijoConstitucion, ct);
        var huella = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Decimal("installment", pedido.Installment, 2)
            .Entero("termMonths", pedido.TermMonths).Texto("periodicity", plan.Condiciones.Periodicidad.ToString()).Decimal("rate", tasa, 6)
            .Decimal("bonusRate", plan.Condiciones.TasaBonificada, 6).Decimal("breachRate", plan.Condiciones.TasaCastigo, 6).Fecha("start", hoy)
            .Texto("version", version.ContentSha256).Calcular();
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.ContractualSetup,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy,
            AccountingDate = hoy,
            RegisteredAt = sello.RegisteredAt,
            Amount = 0m,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            Payload = JsonSerializer.Serialize(new
            {
                installment = pedido.Installment, termMonths = pedido.TermMonths, periodicity = plan.Condiciones.Periodicidad.ToString(), rate = tasa,
                bonusRate = plan.Condiciones.TasaBonificada, breachRate = plan.Condiciones.TasaCastigo, installments = plan.Cuotas.Count,
                maturity = plan.Vencimiento, committed = plan.TotalPactado, projected = plan.ValorFinalCumpliendo,
            }, Json),
            InputsSha256 = huella,
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        var terminos = LectorDeContractual.NuevaVersion(cuenta, 1, operacion, AccountTermsCause.ContractualSetup, version, plan, tasa, null, destino, null,
            ahora, huella);
        registro.SellarCondiciones(control, terminos, [], 0);
        // La primera cuota exigible desde el primer día (la pagan la ventanilla, la nómina o la pagaduría); las siguientes las genera el paso diario.
        var primera = plan.Cuotas[0];
        registro.RegistrarCuotas([GeneradorDeCuotasDeContractual.Cuota(cuenta, plan, primera, null, operacion.Id, huella)]);
        RegistroDeCuentas.MarcarCuotaGenerada(control, GeneradorDeCuotasDeContractual.Clave(plan, primera));
        await db.SaveChangesAsync(ct);
        return Result.Success(terminos);
    }

    /// <summary>Las condiciones del motor de lo pedido con la versión de la línea, desde <paramref name="inicio"/> y con la tasa base dada.</summary>
    public static CondicionesDeContractual Condiciones(DepositLineVersion version, ContractualTermsInput pedido, DateOnly inicio, decimal tasaBase) => new(
        pedido.Installment, version.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly, pedido.TermMonths, inicio, tasaBase, version.BonusAnnualRate,
        version.BreachAnnualRate, version.DayCount, version.Redondeo());

    /// <summary>
    /// El destino de la instrucción al vencimiento (research §19): una cuenta de ahorro a la vista viva del titular en el alcance de quien opera, o el giro al
    /// beneficiario (el titular, si no se nombra otro) con la cuenta bancaria pedida o la registrada del asociado.
    /// </summary>
    public async Task<Result<DestinoDeContractual>> DestinoAsync(MemberAccount cuenta, ContractualMaturityInstructionInput instruccion, CancellationToken ct)
    {
        // E248: lo que admite es la versión de la línea con la que se selló el contrato (un cambio posterior de la línea no lo toca).
        var leido = await lector.LeerAsync(cuenta, ct);
        var versionId = leido.Inicial?.DepositLineVersionId;
        var version = versionId is { } vid ? await db.DepositLineVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid, ct) : null;
        return await DestinoAsync(cuenta.Id, cuenta.HolderPersonId, instruccion, version, ct);
    }

    /// <summary>
    /// ¿La línea admite este destino al vencer (E248)? Nulo = la versión no declara destinos (anterior a la decisión del 2026-10-21): admite ambos.
    /// </summary>
    public static ContractualMaturityDestinations DestinosQueAdmite(DepositLineVersion? version) =>
        version?.MaturityDestinations ?? ContractualMaturityDestinations.All;

    public static bool Admite(DepositLineVersion? version, CloseDeliveryKind kind) => kind switch
    {
        CloseDeliveryKind.TransferToAccount => DestinosQueAdmite(version).HasFlag(ContractualMaturityDestinations.TransferToDemand),
        CloseDeliveryKind.DisbursementOrder => DestinosQueAdmite(version).HasFlag(ContractualMaturityDestinations.DisbursementOrder),
        _ => false,
    };

    /// <summary>El destino de la instrucción para la cuenta <paramref name="cuentaId"/> (cero antes de abrirla) de <paramref name="titularId"/>, dentro de lo que admite la línea.</summary>
    public async Task<Result<DestinoDeContractual>> DestinoAsync(int cuentaId, int titularId, ContractualMaturityInstructionInput instruccion,
        DepositLineVersion? version, CancellationToken ct)
    {
        if (Enum.IsDefined(instruccion.Kind) && !Admite(version, instruccion.Kind))
            return Result.Failure<DestinoDeContractual>(ErroresDeContractual.DestinoNoAdmitidoPorLaLinea(instruccion.Kind, DestinosQueAdmite(version)));
        if (instruccion.Kind == CloseDeliveryKind.TransferToAccount)
        {
            if (instruccion.TargetAccountPublicId is not { } id)
                return Result.Failure<DestinoDeContractual>(ErroresDeContractual.DestinoInvalido("Elija la cuenta de ahorro a la vista a la que se traslada al vencer."));
            var otra = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, id, seguir: false, ct);
            if (otra.IsFailure) return Result.Failure<DestinoDeContractual>(otra.Error);
            var o = otra.Value;
            if (o.Id == cuentaId || o.Nature != LineNature.Deposit || o.DepositClass != DepositClass.Demand || !o.Viva || o.HolderPersonId != titularId)
                return Result.Failure<DestinoDeContractual>(ErroresDeContractual.DestinoInvalido(
                    "Al vencer, el contractual se traslada a una cuenta de ahorro a la vista viva del mismo titular."));
            return Result.Success(new DestinoDeContractual(o.Id, null, null, null, null));
        }
        if (instruccion.Kind != CloseDeliveryKind.DisbursementOrder)
            return Result.Failure<DestinoDeContractual>(ErroresDeContractual.DestinoInvalido("La instrucción al vencimiento es un traslado o un giro."));
        var persona = titularId;
        if (instruccion.Payee?.PersonPublicId is { } beneficiario && beneficiario != Guid.Empty)
            persona = await db.People.AsNoTracking().Where(p => p.PublicId == beneficiario && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
                      ?? 0;
        if (persona == 0) return Result.Failure<DestinoDeContractual>(ErroresDeContractual.DestinoInvalido("El beneficiario del giro no existe."));
        var banco = await ordenes.BeneficiarioAsync(persona, instruccion.Payee?.BankPublicId, instruccion.Payee?.AccountType, instruccion.Payee?.AccountNumber, ct);
        if (banco.IsFailure) return Result.Failure<DestinoDeContractual>(banco.Error);
        return Result.Success(new DestinoDeContractual(null, persona, banco.Value.BancoId, banco.Value.TipoDeCuenta, banco.Value.NumeroDeCuenta));
    }

    private static Result<MemberAccountTermsVersion> Falla(Error e) => Result.Failure<MemberAccountTermsVersion>(e);
}
