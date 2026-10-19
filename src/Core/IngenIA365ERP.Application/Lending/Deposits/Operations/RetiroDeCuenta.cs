using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>El beneficiario de un retiro por giro: la persona y su cuenta bancaria (data-model §3.9). (nuevo)</summary>
public sealed record OrdenDeRetiro(int PersonaId, BeneficiarioDeOrden Banco);

/// <summary>
/// Un retiro pedido (feature 014, US4, T111; contrato-de-cuentas §3.9): por el contrato (recaudador, clave, medio efectivo o cheque, firmas verificadas en
/// ventanilla), por la pantalla (orden de giro) o por el débito automático (operación interna). (nuevo)
/// </summary>
public sealed record RetiroPedido(
    Recaudador? Recaudador,
    string? Clave,
    string? HuellaDeLaSolicitud,
    Guid AccountPublicId,
    decimal Monto,
    DateOnly FechaValor,
    ClaseDeMedioDeRecaudo Medio,
    IReadOnlyList<Guid> Firmantes,
    Guid? Receptor,
    ReferenciaDeRecibo? Recibo,
    Guid? AprobacionPublicId,
    PrevalidationOutcome Validacion,
    IReadOnlyList<string> Avisos,
    string? Motivo)
{
    /// <summary>La clase de la operación: <c>Withdrawal</c> o, en el débito automático, <c>DirectDebit</c>.</summary>
    public MemberAccountOperationKind Clase { get; init; } = MemberAccountOperationKind.Withdrawal;

    /// <summary>Falso en las operaciones internas (el débito automático lo autorizó el titular en su autorización).</summary>
    public bool ExigeFirmas { get; init; } = true;

    public bool ExigeAtribucion { get; init; } = true;

    /// <summary>El pago del crédito que paga el débito automático.</summary>
    public long? OperacionDelCredito { get; init; }

    public InternalWithdrawalV1? Interno { get; init; }

    /// <summary>El retiro por giro desde la pantalla (con su beneficiario).</summary>
    public OrdenDeRetiro? Orden { get; init; }

    public string Prefijo { get; init; } = RetiroDeCuenta.PrefijoRetiro;

    /// <summary>El hecho generador del GMF (<c>HechosGeneradoresDeGmf</c>): el retiro, o el del traslado que lo revisa.</summary>
    public string HechoGenerador { get; init; } = HechosGeneradoresDeGmf.Retiro;

    /// <summary>
    /// US5 (T135; FR-039): la entrega del saldo de una cancelación: se lleva todo —el mínimo de la línea no se respeta, aunque la línea no admita retiros
    /// parciales— y no aplican los topes por operación y por día ni el cargo por retiro sobre los gratuitos.
    /// </summary>
    public bool EsCancelacion { get; init; }
}

/// <summary>Lo que revisó un retiro antes de escribir: la cuenta, su GMF, sus cargos, las firmas, la atribución y el disponible que deja. (nuevo)</summary>
public sealed record RetiroRevisado(
    CuentaParaMovimiento Cuenta,
    GmfCalculado? Gmf,
    IReadOnlyList<CargoPropuesto> Cargos,
    FirmasRevisadas? Firmas,
    IReadOnlyDictionary<int, Guid> Personas,
    AtribucionEvaluada Atribucion,
    DisponibleCalculado Despues);

/// <summary>Lo que dejó un retiro: el comprobante, la operación, la orden de giro si la hubo y si ya estaba aplicado con la misma clave. (nuevo)</summary>
public sealed record RetiroAplicado(ComprobanteDeCuentaDto Comprobante, MemberAccountOperation Operacion, AccountPayoutOrder? Orden, bool YaAplicado);

/// <summary>
/// El retiro de una cuenta de captación (feature 014, US4, T111, T119; contrato-de-cuentas §3.9; FR-051, FR-053, FR-056, FR-057, FR-088, FR-090). Lo usan el
/// contrato (<c>RetirarAsync</c>), la pantalla (retiro por orden de giro) y el débito automático. Valida en el orden del contrato:
/// <list type="number">
/// <item>estado y naturaleza: los aportes no se retiran por el contrato; el CDAT vigente tampoco; la línea sin retiros parciales tampoco; una cuenta con
/// sobregiro técnico, en cierre, cerrada o de titular fallecido no admite retiros;</item>
/// <item>el manejo y las firmas (<see cref="FirmasDeCuenta"/>);</item>
/// <item>el disponible —saldo − bloqueos − embargos − canje − mínimo— con el GMF incluido (<see cref="DisponibleDeCuenta"/>): nunca deja la cuenta bajo el
/// mínimo ni negativa;</item>
/// <item>los topes de la línea por operación y por día, y el efectivo máximo;</item>
/// <item>la atribución con monto del usuario que actúa: superada, queda en espera de un aprobador presente y no mueve saldo;</item>
/// <item>el GMF por su motor (<see cref="GravamenMovimientosFinancieros"/>) como transacción enlazada y el uso del tope del mes en la misma operación.</item>
/// </list>
/// Escribe por <see cref="RegistroDeCuentas"/>, emite <c>RetiroDeCuentaAplicado</c> con su <c>GmfRetenido</c> (y la orden de giro v2 si es por giro) y causa
/// el cargo por retiro sobre los gratuitos en su propia operación. Corre en la transacción y bajo el cerrojo de quien llama; guarda. (nuevo)
/// </summary>
public sealed class RetiroDeCuenta(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeMovimientos movimientos,
    CargosDeCuentas cargos,
    IActorActual actorActual,
    IDateTimeService reloj,
    Accrual.AjusteDeCausacionDeCuenta? ajusteDeCausacion = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El prefijo del comprobante de un retiro de cuenta.</summary>
    public const string PrefijoRetiro = "RTC-";

    /// <summary>El nombre de la huella de un retiro del contrato (contrato-de-cuentas §3.4 paso 8).</summary>
    public const string Metodo = "Lending.AccountContract.Withdraw";

    // --------------------------------------------------------------------------------------------------------- revisar --

    /// <summary>
    /// Revisa el retiro sin escribir (también para simular): con <paramref name="pedirAprobacion"/>, una atribución superada pide la aprobación (la guarda
    /// quien llama); sin él, sólo informa. Devuelve la revisión o el rechazo del primer paso que falla.
    /// </summary>
    public async Task<Result<RetiroRevisado>> RevisarAsync(MemberAccount cuenta, RetiroPedido p, bool pedirAprobacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(p);
        if (cuenta.Nature == LineNature.Contribution) return Falla(ErroresDeAportes.NoRetirable());
        var c = await movimientos.LeerAsync(cuenta, p.FechaValor, ct);
        if (Operable(c, p) is { } noOperable) return Falla(noOperable);

        FirmasRevisadas? firmas = null;
        IReadOnlyDictionary<int, Guid> personas = new Dictionary<int, Guid>();
        if (p.ExigeFirmas)
        {
            (firmas, personas) = await movimientos.FirmasAsync(c, p.Firmantes, p.Monto, p.FechaValor, ct);
            if (!firmas.Completas) return Falla(ErroresDeMovimientos.FaltanFirmas(firmas, personas));
        }

        var gmf = await movimientos.GmfAsync(c, p.HechoGenerador, p.Monto, p.FechaValor, ct);
        if (gmf.IsFailure) return Falla(gmf.Error);
        var total = p.Monto + (gmf.Value?.Valor ?? 0m);
        var alcanza = p.EsCancelacion
            ? c.Disponible.Saldo - c.Disponible.Retenido - c.Disponible.Embargado - c.Disponible.EnCanje >= total
            : DisponibleDeCuenta.Alcanza(c.Disponible, total);
        if (!alcanza) return Falla(ErroresDeMovimientos.DisponibleInsuficiente(c.Disponible, total));
        if (!p.EsCancelacion && await movimientos.TopesAsync(c, p.Monto, p.Medio, esRetiro: true, p.FechaValor, ct) is { } tope) return Falla(tope);

        var atribucion = AtribucionEvaluada.SinAprobacion;
        if (p.ExigeAtribucion && pedirAprobacion)
        {
            var evaluada = await movimientos.AtribucionAsync(ReglasDeMovimientos.PermisoDeRetiro, cuenta, p.Monto,
                ReglasDeMovimientos.HuellaDeOperacion(p.Clase.ToString(), p.Recaudador?.Code, p.Clave, cuenta.PublicId, p.Monto),
                $"Retiro de {p.Monto:N0} de la cuenta {cuenta.Number}", p.AprobacionPublicId, p.FechaValor, ct);
            if (evaluada.IsFailure) return Falla(evaluada.Error);
            atribucion = evaluada.Value;
        }

        var despues = DisponibleDeCuenta.Calcular(cuenta.Nature, c.Disponible.Saldo - total, c.Disponible.Retenido, c.Disponible.Embargado,
            c.Disponible.EnCanje, c.Disponible.Minimo);
        IReadOnlyList<CargoPropuesto> propuestos = p.Clase == MemberAccountOperationKind.Withdrawal && !p.EsCancelacion
            ? await cargos.PorEventoAsync(c, ChargeTrigger.WithdrawalOverFree, p.Monto, p.FechaValor, despues.Disponible, ct)
            : [];
        return Result.Success(new RetiroRevisado(c, gmf.Value, propuestos, firmas, personas, atribucion, despues));
    }

    /// <summary>Nulo si la cuenta admite el retiro por su estado, su clase y su línea (contrato-de-cuentas §3.9 paso 1).</summary>
    private static Error? Operable(CuentaParaMovimiento c, RetiroPedido p)
    {
        var cuenta = c.Cuenta;
        if (cuenta.DepositClass == DepositClass.TermDeposit && cuenta.Status != MemberAccountStatus.Matured)
            return ErroresDeMovimientos.CdatVigente(cuenta.Number);
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive or MemberAccountStatus.Matured))
            return ErroresDeAportes.NoOperable(cuenta.Number, cuenta.Status);
        if (c.Titular.IsDeceased)
            return ErroresDeAportes.NoOperable(cuenta.Number, cuenta.Status, $"El titular de la cuenta {cuenta.Number} figura como fallecido: la cuenta no admite retiros.");
        if (cuenta.TechnicalNegativeSince is not null)
            return new ErrorConDatos(ErroresDeAportes.CodigoNoOperable,
                $"La cuenta {cuenta.Number} tiene un sobregiro técnico desde el {ErroresDeCuentas.Fecha(cuenta.TechnicalNegativeSince.Value)}: no admite retiros hasta cubrirlo.",
                new { status = cuenta.Status.ToString(), holds = new[] { "TechnicalOverdraft" } });
        if (p.Clase is MemberAccountOperationKind.Withdrawal or MemberAccountOperationKind.Transfer && c.Version is { AllowsPartialWithdrawals: false }
            && !p.EsCancelacion)
            return ErroresDeMovimientos.RetiroNoAdmitido(cuenta.Number);
        return null;
    }

    // --------------------------------------------------------------------------------------------------------- aplicar --

    /// <summary>
    /// Aplica el retiro (ver la clase). Con recaudador y clave, la idempotencia por <c>(recaudador, clave)</c>: la misma huella devuelve el comprobante
    /// original; otra, <c>Lending.Contract.KeyReused</c>. La cuenta ya está bloqueada.
    /// </summary>
    public async Task<Result<RetiroAplicado>> AplicarAsync(RetiroPedido p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.Recaudador is { } recaudador && p.Clave is { } clave)
        {
            var previa = await db.MemberAccountOperations.AsNoTracking().FirstOrDefaultAsync(o => o.CollectorCode == recaudador.Code && o.IdempotencyKey == clave, ct);
            if (previa is not null)
                return string.Equals(previa.RequestSha256, p.HuellaDeLaSolicitud, StringComparison.Ordinal)
                    ? Result.Success(new RetiroAplicado(await ComprobantesDeCuenta.DeAsync(db, movimientos, previa, reloj.HoyLocal, ct), previa, null, true))
                    : Result.Failure<RetiroAplicado>(ErroresDePagos.ClaveReusada(clave));
        }

        var cuenta = await db.MemberAccounts.FirstOrDefaultAsync(a => a.PublicId == p.AccountPublicId && !a.IsDeleted, ct);
        if (cuenta is null) return Result.Failure<RetiroAplicado>(new Error(ErroresDePagos.CodigoNoEncontrado, "La cuenta no existe."));
        var revisado = await RevisarAsync(cuenta, p, pedirAprobacion: true, ct);
        if (revisado.IsFailure) return Result.Failure<RetiroAplicado>(revisado.Error);
        var rv = revisado.Value;

        var hecho = await emision.HechoDeCuentaAsync(cuenta, p.FechaValor, ct);
        if (hecho.IsFailure) return Result.Failure<RetiroAplicado>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cuenta.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(p.Clase, p.Prefijo, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var gmf = rv.Gmf;
        var firmantes = rv.Firmas?.Presentadas ?? [];
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = p.Clase,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = p.FechaValor,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = p.Monto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = p.Recaudador?.Class.ToString(),
            CollectorCode = p.Recaudador?.Code,
            IdempotencyKey = p.Clave,
            RequestSha256 = p.HuellaDeLaSolicitud,
            ContractVersion = p.Recaudador is null ? null : IContratoDeCuentas.VersionDelContrato,
            PaymentMeansClass = p.Medio.ToString(),
            ReceiptNumber = p.Recibo?.Number,
            ReceiptDate = p.Recibo?.Date,
            ReceiptBranchPublicId = p.Recibo?.OfficePublicId,
            ReceiptMeansReference = p.Recibo?.MeansReference,
            AuthorizedByUserId = rv.Atribucion.AprobadoPor,
            SignedByPersonIdsJson = firmantes.Count == 0 ? null : JsonSerializer.Serialize(firmantes.Order()),
            RelatedLoanOperationId = p.OperacionDelCredito,
            Reason = string.IsNullOrWhiteSpace(p.Motivo) ? null : Recortar(p.Motivo),
            Payload = JsonSerializer.Serialize(new
            {
                account = cuenta.PublicId, amount = p.Monto, valueDate = p.FechaValor, meansClass = p.Medio.ToString(), signers = p.Firmantes,
                receiver = p.Receptor, approval = rv.Atribucion.AprobacionPublicId, gmf = gmf?.Valor,
            }, Json),
            InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Decimal("amount", p.Monto, 2).Fecha("valueDate", p.FechaValor)
                .Texto("means", p.Medio.ToString()).Decimal("gmf", gmf?.Valor, 2).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = p.Validacion.ToString(),
        });
        await db.SaveChangesAsync(ct);

        // El retiro por giro (pantalla): la orden nombra la operación (research E16) y su OrdenDeGiroEmitida v2 sale con el retiro.
        AccountPayoutOrder? orden = null;
        PayeeV1? beneficiario = null;
        if (p.Orden is { } pedida)
        {
            var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(x => x.Id == pedida.PersonaId, ct);
            orden = AccountPayoutOrder.Emitir(cuenta.Id, operacion.Id, p.EsCancelacion ? PayoutPurpose.Cancellation : PayoutPurpose.Withdrawal, persona.Id, persona.IdType ?? string.Empty, persona.TaxId ?? string.Empty,
                ReferenciasDeGobierno.Nombre(persona), pedida.Banco.BancoId, pedida.Banco.TipoDeCuenta, pedida.Banco.NumeroDeCuenta, p.Monto,
                hecho.Value.FechaContable.AddDays(1), ahora, actor.UserId);
            await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
            db.AccountPayoutOrders.Add(orden);
            await db.SaveChangesAsync(ct);
            beneficiario = new PayeeV1
            {
                PersonPublicId = persona.PublicId, TaxIdType = persona.IdType ?? string.Empty, TaxId = persona.TaxId ?? string.Empty,
                Name = ReferenciasDeGobierno.Nombre(persona), BankCode = pedida.Banco.CodigoDeTransferencia, BankAccountNumber = pedida.Banco.NumeroDeCuenta,
                BankAccountType = pedida.Banco.TipoDeCuenta?.ToString(CultureInfo.InvariantCulture),
            };
        }

        var exenta = rv.Cuenta.ExentaDeGmf ? cuenta.PublicId : (Guid?)null;
        var valores = new List<DepositConceptAmountV1>
        {
            new() { Concept = nameof(MemberAccountConcept.Principal), Amount = -p.Monto, ReasonCode = p.Clase == MemberAccountOperationKind.DirectDebit ? "DirectDebit" : "DepositWithdrawal" },
        };
        var unidad = await emision.RetiroDeCuentaAsync(new RetiroAEmitir(hecho.Value, operacion, valores, p.Medio.ToString(),
            p.Receptor ?? rv.Cuenta.Titular.PublicId, gmf is null ? null : new GmfAEmitir(gmf, exenta, cuenta.DepositClass?.ToString()), p.Validacion)
        {
            Recaudador = p.Recaudador is { } r && p.Clave is { } k
                ? EmisionDeCartera.Recaudador(r, k, p.Medio, p.Receptor ?? rv.Cuenta.Titular.PublicId, p.Recibo ?? new ReferenciaDeRecibo(numero, p.FechaValor, null, null, "Retiro"))
                : null,
            Interno = p.Interno,
            Orden = orden,
            Beneficiario = beneficiario,
        }, ct);
        if (unidad.IsFailure) return Result.Failure<RetiroAplicado>(unidad.Error);

        var kind = p.Clase == MemberAccountOperationKind.DirectDebit ? MemberAccountTransactionKind.InternalOffset : MemberAccountTransactionKind.Withdrawal;
        var filas = new List<MemberAccountTransaction>
        {
            Fila(cuenta, operacion, kind, MemberAccountConcept.Principal, -p.Monto, p.FechaValor, hecho.Value.FechaContable, unidad.Value.De(RetiroDeCuentaAplicadoV1.Type)),
        };
        if (GravamenMovimientosFinancieros.SeCobra(gmf))
        {
            var mensaje = unidad.Value.De(GmfRetenidoV1.Type);
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -gmf.Valor, p.FechaValor,
                hecho.Value.FechaContable, mensaje));
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, gmf.Valor, p.FechaValor,
                hecho.Value.FechaContable, mensaje));
        }
        registro.RegistrarTransacciones(filas);
        if (gmf is not null)
            registro.RegistrarUsoDeGmf(cuenta.HolderPersonId, operacion, cuenta.Id, p.FechaValor, gmf.HechoGenerador, gmf.Base, gmf.ExentoAplicado, gmf.BaseGravada,
                gmf.Valor, gmf.Politica, gmf.Tarifa);
        var reactivada = false;
        if (p.Clase == MemberAccountOperationKind.Withdrawal)
        {
            // FR-058: un retiro del titular es movimiento del titular (reactiva una inactiva); el débito automático lo hace el proceso.
            RegistroDeCuentas.MarcarActividadDelTitular(control, p.FechaValor);
            if (cuenta.Status == MemberAccountStatus.Inactive)
            {
                cuenta.Reactivar(ahora);
                reactivada = true;
            }
        }
        await db.SaveChangesAsync(ct);
        // US5 (T133; FR-062): con fecha valor en días ya causados, el ajuste de la causación va enlazado a esta operación.
        if (ajusteDeCausacion is not null
            && await ajusteDeCausacion.AjustarAsync(cuenta, operacion, EmisionDeCartera.ClaseRetiroDeCuenta, ct) is { IsFailure: true } ajusteFallido)
            return Result.Failure<RetiroAplicado>(ajusteFallido.Error);

        // FR-128: el cargo por retiro sobre los gratuitos, en su propia operación.
        var causados = new List<CargoCalculadoDto>();
        foreach (var propuesto in rv.Cargos.Where(x => x.Calculo.SeCausa))
        {
            var leida = await movimientos.LeerAsync(cuenta, p.FechaValor, ct);
            var causado = await cargos.CausarAsync(leida, propuesto, p.FechaValor, operacion.Id, nameof(MemberAccountOperationKind.Withdrawal), null, ct);
            if (causado.IsFailure) return Result.Failure<RetiroAplicado>(causado.Error);
            causados.Add(new CargoCalculadoDto(propuesto.Definicion.Code, propuesto.Calculo.Valor, propuesto.Definicion.IsVatTaxed, causado.Value.Cargo.Status.ToString()));
        }

        var final = await movimientos.LeerAsync(cuenta, p.FechaValor, ct);
        AutorizacionDeEfectivoDto? efectivo = p.Medio is ClaseDeMedioDeRecaudo.Cash or ClaseDeMedioDeRecaudo.Check
            ? new AutorizacionDeEfectivoDto(operacion.Number, p.Monto, ReferenciasDeGobierno.Nombre(rv.Cuenta.Titular),
                rv.Firmas?.Presentadas.Select(x => rv.Personas.GetValueOrDefault(x)).ToList() ?? [])
            : null;
        var comprobante = new ComprobanteDeCuentaDto(operacion.PublicId, operacion.Number, cuenta.PublicId, cuenta.Number, p.Clave ?? string.Empty,
            nameof(MemberAccountOperationKind.Withdrawal), operacion.ValueDate, operacion.AccountingDate, operacion.RegisteredAt, p.Monto,
            filas.Select(f => new LineaDeCuentaDto((ConceptoDeCuenta)(int)f.Concept, f.Kind.ToString(), f.Amount)).ToList(), ReglasDeMovimientos.Dto(gmf), causados, [],
            ReglasDeMovimientos.Dto(final.Disponible), efectivo, null, reactivada, operacion.InputsSha256, operacion.RulesVersion, p.Avisos);
        return Result.Success(new RetiroAplicado(comprobante, operacion, orden, false));
    }

    private static MemberAccountTransaction Fila(MemberAccount cuenta, MemberAccountOperation operacion, MemberAccountTransactionKind clase,
        MemberAccountConcept concepto, decimal monto, DateOnly fecha, DateOnly contable, Guid? mensaje) => new()
    {
        MemberAccountId = cuenta.Id,
        Operation = operacion,
        OperationId = operacion.Id,
        Kind = clase,
        Concept = concepto,
        Amount = monto,
        ValueDate = fecha,
        AccountingDate = contable,
        RegisteredAt = operacion.RegisteredAt,
        MessagePublicId = mensaje,
    };

    private static Result<RetiroRevisado> Falla(Error e) => Result.Failure<RetiroRevisado>(e);

    private static string Recortar(string texto)
    {
        var t = texto.Trim();
        return t.Length <= 500 ? t : t[..500];
    }
}
