using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

/// <summary>Una porción de una consignación: la cuota que cubre (nula en un extraordinario), el concepto del libro y el valor. (nuevo)</summary>
public sealed record PorcionDeConsignacion(CuotaLeida? Cuota, MemberAccountConcept Concepto, decimal Monto);

/// <summary>El reparto de una consignación: las porciones, lo que excede lo exigible y adónde fue (<c>Extraordinary</c>). (nuevo)</summary>
public sealed record RepartoDeConsignacion(IReadOnlyList<PorcionDeConsignacion> Porciones, decimal Excedente, string? ExcedenteComo)
{
    /// <summary>Lo que la consignación cubre de cada cuota (el comprobante y el hecho).</summary>
    public IReadOnlyList<DistribucionEnCuotaDto> PorCuota => Porciones.Where(p => p.Cuota is not null)
        .GroupBy(p => p.Cuota!.Cuota.PublicId)
        .Select(g => new DistribucionEnCuotaDto(g.Key, g.First().Cuota!.Cuota.Periodo, g.Sum(p => p.Monto)))
        .ToList();
}

/// <summary>Una consignación del contrato en curso: el recaudador, el ítem, la validación previa sellada y sus avisos. (nuevo)</summary>
public sealed record ConsignacionEnCurso(Recaudador Recaudador, ConsignacionAAplicar Item, PrevalidationOutcome Validacion, IReadOnlyList<string> Avisos);

/// <summary>Lo que dejó una consignación: el comprobante y si ya estaba aplicada con la misma clave. (nuevo)</summary>
public sealed record ConsignacionAplicada(ComprobanteDeCuentaDto Comprobante, bool YaAplicada);

/// <summary>
/// La consignación en una cuenta por el contrato (feature 014, US3, T083, T085; contrato-de-cuentas §3.8; FR-040, FR-041, FR-043, FR-087, FR-089): en
/// la transacción del recaudador y bajo el cerrojo que ya tomó el contrato:
/// <list type="number">
/// <item>la idempotencia por <c>(recaudador, clave)</c> con la huella de la solicitud (misma huella: el comprobante original; otra:
/// <c>Lending.Contract.KeyReused</c>);</item>
/// <item>la cuenta operable (activa o inactiva —una consignación la reactiva—, titular vivo), la clase de medio del recaudador, la fecha valor
/// (después de la activación de la naturaleza, no futura y, salvo Nómina, no más atrás que <c>Captaciones.DiasMaximosFechaValor</c>) y el origen de
/// fondos del efectivo sobre <c>Efectivo.UmbralDeclaracion</c>;</item>
/// <item>el <b>reparto</b> (<see cref="RepartirAsync"/>): las cuotas en orden de vencimiento (o las nombradas), completando el mínimo no reducible
/// primero si la línea lo dice; lo que excede lo exigible va como aporte extraordinario si la línea lo admite, y si no se rechaza; sin cuotas
/// pendientes, todo es extraordinario. El límite individual de participación (FR-043) rechaza un aporte que lo supera si la línea rechaza el
/// exceso;</item>
/// <item>la operación y sus filas por <see cref="RegistroDeCuentas"/> (el único escritor del libro), el número por <see cref="NumeradorDeCartera"/> y
/// el hecho —<c>AporteRecibido</c> en aportes, <c>ConsignacionEnCuentaAplicada</c> en la cuota del ahorro permanente— por
/// <see cref="EmisionDeCartera"/> en la misma unidad (T5).</item>
/// </list>
/// No guarda: el contrato guarda una vez al final del lote. Las consignaciones libres en cuentas de captación (con canje, cargos y GMF) llegan con K8
/// (US4): aquí sólo la cuota del ahorro permanente (research E56). (nuevo)
/// </summary>
public sealed class ConsignacionEnCuentas(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeAportes reglas,
    ReglasDeParametrosDeCartera parametros,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El nombre de la huella de una consignación (contrato-de-cuentas §3.4 paso 8).</summary>
    public const string Metodo = "Lending.AccountContract.Deposit";

    /// <summary>El prefijo del comprobante de una consignación de cuenta.</summary>
    public const string PrefijoConsignacion = "CNC-";

    /// <summary>Lo que va como aporte extraordinario (<c>overpaymentAs</c>).</summary>
    public const string ComoExtraordinario = "Extraordinary";

    /// <summary>Los días hacia atrás de la fecha valor si el parámetro no responde (D11; el defecto del catálogo).</summary>
    private const int DiasPorDefecto = 5;

    public static string Huella(ConsignacionAAplicar item) => HuellaDeOperacion.Calcular(Metodo, item);

    // --------------------------------------------------------------------------------------------------------- aplicar --

    public async Task<Result<ConsignacionAplicada>> AplicarAsync(ConsignacionEnCurso c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var item = c.Item;
        var huella = Huella(item);
        var previa = await db.MemberAccountOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.CollectorCode == c.Recaudador.Code && o.IdempotencyKey == item.IdempotencyKey, ct);
        if (previa is not null)
            return string.Equals(previa.RequestSha256, huella, StringComparison.Ordinal)
                ? Result.Success(new ConsignacionAplicada(await ComprobanteAsync(previa, ct), true))
                : Result.Failure<ConsignacionAplicada>(ErroresDePagos.ClaveReusada(item.IdempotencyKey));

        var cuenta = await db.MemberAccounts.FirstOrDefaultAsync(a => a.PublicId == item.AccountPublicId && !a.IsDeleted, ct);
        if (cuenta is null) return Falla(new Error(ErroresDePagos.CodigoNoEncontrado, "La cuenta no existe."));
        if (await OperableAsync(cuenta, ct) is { } noOperable) return Falla(noOperable);
        if (cuenta.Nature == LineNature.Deposit && cuenta.DepositClass != DepositClass.Permanent)
            return Falla(ErroresDeAportes.EntregaNoDisponible("La consignación en una cuenta de captación", nameof(EntregaDeCartera.K8)));
        if (ClaseDeMedio(c.Recaudador.Class, item.MeansClass) is { } medio) return Falla(medio);
        if (await FechaValorAsync(c.Recaudador.Class, cuenta.Nature, item.ValueDate, ct) is { } fecha) return Falla(fecha);
        if (item.MeansClass == ClaseDeMedioDeRecaudo.Cash && item.SourceOfFundsAttachmentPublicId is null
            && await LectorDeCuentas.UmbralDeOrigenDeFondosAsync(parametros, item.ValueDate, ct) is { } umbral && item.Amount >= umbral)
            return Falla(ErroresDeAportes.OrigenDeFondos(umbral, item.Amount));

        var reparto = await RepartirAsync(cuenta, item.Modality, item.Amount, item.InstallmentPublicIds, item.ValueDate, ct);
        if (reparto.IsFailure) return Falla(reparto.Error);

        var hecho = await emision.HechoDeCuentaAsync(cuenta, item.ValueDate, ct);
        if (hecho.IsFailure) return Falla(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cuenta.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Deposit, PrefijoConsignacion, ct);
        var ahora = reloj.UtcNow;
        var actor = await actorActual.ObtenerAsync(ct);
        var r = reparto.Value;
        var entradas = new HuellaDeCartera()
            .Texto("account", cuenta.PublicId.ToString("N"))
            .Decimal("amount", item.Amount, 2)
            .Fecha("valueDate", item.ValueDate)
            .Textos("portions", r.Porciones.Select(p => $"{p.Cuota?.Cuota.PublicId:N}|{p.Concepto}|{p.Monto.ToString(CultureInfo.InvariantCulture)}"))
            .Calcular();
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.Deposit,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = item.ValueDate,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = item.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = c.Recaudador.Class.ToString(),
            CollectorCode = c.Recaudador.Code,
            IdempotencyKey = item.IdempotencyKey,
            RequestSha256 = huella,
            ContractVersion = IContratoDeCuentas.VersionDelContrato,
            PaymentMeansClass = item.MeansClass.ToString(),
            ReceiptNumber = item.Receipt.Number,
            ReceiptDate = item.Receipt.Date,
            ReceiptBranchPublicId = item.Receipt.OfficePublicId,
            ReceiptMeansReference = item.Receipt.MeansReference,
            CashOriginDeclarationAttachmentPublicId = item.SourceOfFundsAttachmentPublicId,
            Payload = JsonSerializer.Serialize(item, Json),
            InputsSha256 = entradas,
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = c.Validacion.ToString(),
        });

        var reactivada = false;
        if (cuenta.Status == MemberAccountStatus.Inactive)
        {
            cuenta.Reactivar(ahora);
            reactivada = true;
        }
        RegistroDeCuentas.MarcarActividadDelTitular(control, item.ValueDate);

        var valores = r.Porciones.GroupBy(p => p.Concepto)
            .Select(g => new DepositConceptAmountV1
            {
                Concept = g.Key.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(g.Key)?.ToString(), Amount = g.Sum(p => p.Monto),
            })
            .OrderBy(v => v.Concept, StringComparer.Ordinal).ToList();
        var emitida = cuenta.Nature == LineNature.Contribution
            ? await emision.AporteRecibidoAsync(new AporteAEmitir(hecho.Value, operacion, c.Recaudador, item.IdempotencyKey, item.MeansClass,
                item.DepositorPublicId, item.Receipt, valores,
                r.PorCuota.Select(q => new CoveredInstallmentV1 { InstallmentPublicId = q.InstallmentPublicId, Period = q.Period, Amount = q.Amount }).ToList(),
                r.ExcedenteComo, c.Validacion), ct)
            : await emision.ConsignacionEnCuentaAsync(new ConsignacionAEmitir(hecho.Value, operacion, c.Recaudador, item.IdempotencyKey, item.MeansClass,
                item.DepositorPublicId, item.Receipt, item.Modality.ToString(), valores, reactivada, c.Validacion), ct);
        // Todo lo que podía rechazar ya pasó: si el hecho no se emite es un fallo técnico, se propaga y el recaudador reintenta su transacción entera.
        if (emitida.IsFailure) throw new InvalidOperationException($"{emitida.Error.Code}: {emitida.Error.Message}");
        var mensaje = emitida.Value.Mensajes.FirstOrDefault()?.PublicId;

        var filas = r.Porciones.Select(p => new MemberAccountTransaction
        {
            MemberAccountId = cuenta.Id,
            Operation = operacion,
            Kind = MemberAccountTransactionKind.Deposit,
            Concept = p.Concepto,
            InstallmentId = p.Cuota?.Cuota.Id,
            Amount = p.Monto,
            ValueDate = item.ValueDate,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = operacion.RegisteredAt,
            MessagePublicId = mensaje,
        }).ToList();
        registro.RegistrarTransacciones(filas);

        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var despues = LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos) + item.Amount;
        var comprobante = new ComprobanteDeCuentaDto(operacion.PublicId, operacion.Number, cuenta.PublicId, cuenta.Number, item.IdempotencyKey,
            nameof(MemberAccountOperationKind.Deposit), operacion.ValueDate, operacion.AccountingDate, operacion.RegisteredAt, item.Amount,
            filas.Select(f => new LineaDeCuentaDto((ConceptoDeCuenta)(int)f.Concept, f.Kind.ToString(), f.Amount)).ToList(), null, [], r.PorCuota,
            Disponible(cuenta.Nature, despues), null, null, reactivada, entradas, operacion.RulesVersion, c.Avisos);
        return Result.Success(new ConsignacionAplicada(comprobante, false));
    }

    // --------------------------------------------------------------------------------------------------------- repartir --

    /// <summary>
    /// El reparto de una consignación en la cuenta (también para simular): las cuotas pendientes en orden de vencimiento (<c>Installments</c>) o las
    /// nombradas (<c>SpecificInstallments</c>), el mínimo no reducible primero si la línea lo dice, y el excedente como extraordinario si la línea lo
    /// admite; <c>Free</c> en aportes es un extraordinario. En el ahorro permanente sólo cuotas: el excedente no se admite. No escribe.
    /// </summary>
    public async Task<Result<RepartoDeConsignacion>> RepartirAsync(MemberAccount cuenta, ModalidadDeConsignacion modalidad, decimal monto,
        IReadOnlyList<Guid>? nombradas, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        if (modalidad == ModalidadDeConsignacion.TermDepositFunding) return FallaReparto(ErroresDeAportes.ModalidadNoAdmitida(modalidad));
        if (cuenta.Nature == LineNature.Deposit && modalidad == ModalidadDeConsignacion.Free)
            return FallaReparto(ErroresDeAportes.EntregaNoDisponible("La consignación libre en una cuenta de captación", nameof(EntregaDeCartera.K8)));

        ContributionLineVersion? version = null;
        if (cuenta.Nature == LineNature.Contribution)
        {
            version = await reglas.VersionAsync(cuenta, fecha, ct);
            if (version is null)
                return FallaReparto(new ErrorConDatos(ErroresDeCuentas.CodigoLineaSinVersion,
                    $"La línea de la cuenta {cuenta.Number} no tiene condiciones vigentes el {ErroresDeAportes.Fecha(fecha)}.",
                    new { date = ErroresDeAportes.Fecha(fecha) }));
        }

        // Las cuotas que recibe, en su orden.
        var cuotas = new List<CuotaLeida>();
        if (modalidad == ModalidadDeConsignacion.SpecificInstallments)
        {
            var leidas = await LectorDeCuotas.PorPublicIdAsync(db, nombradas ?? [], ct);
            foreach (var publico in nombradas ?? [])
            {
                var cuota = leidas.FirstOrDefault(q => q.Cuota.PublicId == publico);
                if (cuota is null || cuota.Cuota.MemberAccountId != cuenta.Id) return FallaReparto(ErroresDeAportes.CuotaAjena(publico));
                if (cuota.Pendiente <= 0m) return FallaReparto(ErroresDeAportes.CuotaPagada(cuota.Cuota.Periodo));
                cuotas.Add(cuota);
            }
        }
        else if (modalidad == ModalidadDeConsignacion.Installments)
        {
            cuotas.AddRange((await LectorDeCuotas.DeCuentasAsync(db, [cuenta.Id], ct)).Where(q => q.Pendiente > 0m));
        }

        // El mínimo no reducible que falta, si la línea lo completa primero (MinimumFirst).
        var faltaDelMinimo = 0m;
        if (version is { MinimumFirst: true })
        {
            var minimo = await reglas.MinimoAsync(version, cuenta.HolderPersonId, fecha, ct);
            var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
            faltaDelMinimo = Math.Max(0m, minimo - saldos.GetValueOrDefault(MemberAccountConcept.ContributionMinimum));
        }

        var porciones = new List<PorcionDeConsignacion>();
        var resto = monto;
        foreach (var cuota in cuotas)
        {
            if (resto <= 0m) break;
            var aplica = Math.Min(resto, cuota.Pendiente);
            resto -= aplica;
            if (cuenta.Nature == LineNature.Deposit)
            {
                porciones.Add(new PorcionDeConsignacion(cuota, MemberAccountConcept.Principal, aplica));
                continue;
            }
            var alMinimo = Math.Min(aplica, faltaDelMinimo);
            faltaDelMinimo -= alMinimo;
            if (alMinimo > 0m) porciones.Add(new PorcionDeConsignacion(cuota, MemberAccountConcept.ContributionMinimum, alMinimo));
            if (aplica - alMinimo > 0m) porciones.Add(new PorcionDeConsignacion(cuota, MemberAccountConcept.ContributionOrdinary, aplica - alMinimo));
        }

        string? como = null;
        if (resto > 0m)
        {
            if (cuenta.Nature == LineNature.Deposit || version is not { AllowsExtraordinary: true })
                return FallaReparto(ErroresDeAportes.ExcedenteNoAdmitido(resto));
            porciones.Add(new PorcionDeConsignacion(null, MemberAccountConcept.ContributionExtraordinary, resto));
            como = ComoExtraordinario;
        }

        // El límite individual de participación (FR-043): lo que entra a aportes no puede dejar a la persona por encima.
        if (version is not null && version.ExcessAction == ExcessContributionAction.Reject
            && await reglas.LimiteAsync(version, cuenta.HolderPersonId, fecha, ct) is { } limite)
        {
            var actual = (await reglas.AportesDeAsync(cuenta.HolderPersonId, ct)).Values.Sum();
            var capital = await reglas.CapitalSocialAsync(ct);
            // Sin capital social todavía (la primera consignación de la cooperativa) no hay contra qué medir la participación.
            if (capital > 0m && (actual + monto) / (capital + monto) > limite.Fraccion)
                return FallaReparto(ErroresDeAportes.LimiteDeParticipacion(limite.Fraccion, actual, monto, limite.Fuente));
        }
        return Result.Success(new RepartoDeConsignacion(porciones, resto, como));
    }

    // ----------------------------------------------------------------------------------------------------------- reglas --

    /// <summary>Nulo si la cuenta recibe la consignación: activa o inactiva (una consignación la reactiva) y con su titular vivo.</summary>
    public async Task<Error?> OperableAsync(MemberAccount cuenta, CancellationToken ct)
    {
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive))
            return ErroresDeAportes.NoOperable(cuenta.Number, cuenta.Status);
        var fallecido = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == cuenta.HolderPersonId).Select(p => p.IsDeceased)
            .FirstOrDefaultAsync(ct);
        return fallecido
            ? ErroresDeAportes.NoOperable(cuenta.Number, cuenta.Status, $"El titular de la cuenta {cuenta.Number} figura como fallecido: la cuenta no recibe operaciones.")
            : null;
    }

    /// <summary>
    /// Las clases de medio de cada recaudador (contrato-de-cuentas §3.3): Nómina sólo su descuento; Tesorería transferencia y consignación bancaria; Recaudo
    /// y el doble de prueba, las de la 012 salvo el crédito y la operación interna (Recaudo, además, nunca el descuento de la nómina propia).
    /// </summary>
    public static Error? ClaseDeMedio(ClaseDeRecaudador recaudador, ClaseDeMedioDeRecaudo medio)
    {
        var admite = recaudador switch
        {
            ClaseDeRecaudador.Payroll => medio == ClaseDeMedioDeRecaudo.OwnPayrollDeduction,
            ClaseDeRecaudador.Treasury => medio is ClaseDeMedioDeRecaudo.Transfer or ClaseDeMedioDeRecaudo.BankDeposit,
            ClaseDeRecaudador.Receipts => Enum.IsDefined(medio) && medio is not (ClaseDeMedioDeRecaudo.AssociateCredit or ClaseDeMedioDeRecaudo.CustomerCredit
                or ClaseDeMedioDeRecaudo.InternalOffset or ClaseDeMedioDeRecaudo.OwnPayrollDeduction),
            ClaseDeRecaudador.Test => Enum.IsDefined(medio) && medio is not (ClaseDeMedioDeRecaudo.AssociateCredit or ClaseDeMedioDeRecaudo.CustomerCredit
                or ClaseDeMedioDeRecaudo.InternalOffset),
            _ => false,
        };
        return admite ? null : ErroresDeAportes.ClaseDeMedio(medio, recaudador);
    }

    /// <summary>
    /// La fecha valor (D11, FR-050): nunca antes de la activación de la naturaleza (lo de antes va en la carga) y, salvo Nómina —que fecha al fin de su
    /// período, aunque apruebe antes—, nunca futura ni más atrás que <c>Captaciones.DiasMaximosFechaValor</c>.
    /// </summary>
    public async Task<Error?> FechaValorAsync(ClaseDeRecaudador recaudador, LineNature naturaleza, DateOnly fecha, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var activacion = (await ActivacionesDeCaptacion.LeerAsync(db, ct)).De(naturaleza);
        if (activacion is { } desde && fecha < desde) return ErroresDeAportes.AntesDelCorte(fecha, desde);
        // Nómina fecha al fin de su período, aunque apruebe antes de que termine (la fecha la fija la spec, como en el contrato de obligaciones).
        if (recaudador == ClaseDeRecaudador.Payroll) return null;
        var minima = hoy.AddDays(-await DiasHaciaAtrasAsync(hoy, ct));
        if (activacion is { } a && minima < a) minima = a;
        return fecha > hoy || fecha < minima ? ErroresDeAportes.FechaValor(fecha, minima, hoy) : null;
    }

    private async Task<int> DiasHaciaAtrasAsync(DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CaptacionesDiasMaximosFechaValor, hoy, ct: ct);
        if (leido.IsFailure) return DiasPorDefecto;
        return leido.Value.Valor switch
        {
            int i => i,
            long l => (int)l,
            decimal d => (int)d,
            _ => int.TryParse(leido.Value.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : DiasPorDefecto,
        };
    }

    // ------------------------------------------------------------------------------------------------------ comprobante --

    /// <summary>El comprobante de una operación ya registrada (el reintento con la misma clave devuelve el original).</summary>
    public async Task<ComprobanteDeCuentaDto> ComprobanteAsync(MemberAccountOperation operacion, CancellationToken ct)
    {
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == operacion.MemberAccountId, ct);
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == operacion.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var ids = filas.Where(f => f.InstallmentId is not null).Select(f => f.InstallmentId!.Value).Distinct().ToList();
        var cuotas = await db.AccountInstallments.AsNoTracking().Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var porCuota = filas.Where(f => f.InstallmentId is not null).GroupBy(f => f.InstallmentId!.Value)
            .Select(g => new DistribucionEnCuotaDto(cuotas[g.Key].PublicId, cuotas[g.Key].Periodo, g.Sum(f => f.Amount))).ToList();
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        return new ComprobanteDeCuentaDto(operacion.PublicId, operacion.Number, cuenta.PublicId, cuenta.Number, operacion.IdempotencyKey ?? string.Empty,
            operacion.Kind.ToString(), operacion.ValueDate, operacion.AccountingDate, operacion.RegisteredAt, operacion.Amount,
            filas.Select(f => new LineaDeCuentaDto((ConceptoDeCuenta)(int)f.Concept, f.Kind.ToString(), f.Amount)).ToList(), null, [], porCuota,
            Disponible(cuenta.Nature, LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos)), null, null, false, operacion.InputsSha256,
            operacion.RulesVersion, []);
    }

    /// <summary>El disponible de una cuenta sin restricciones (K7): en aportes nada es disponible para retirar; en el ahorro, el saldo.</summary>
    public static DisponibleDto Disponible(LineNature naturaleza, decimal saldo) =>
        new(saldo, 0m, 0m, 0m, 0m, naturaleza == LineNature.Contribution ? 0m : saldo);

    private static Result<ConsignacionAplicada> Falla(Error e) => Result.Failure<ConsignacionAplicada>(e);

    private static Result<RepartoDeConsignacion> FallaReparto(Error e) => Result.Failure<RepartoDeConsignacion>(e);
}
