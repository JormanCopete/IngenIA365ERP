using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Common.Taxation;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Egresos;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Disbursements;

// Feature 015, R1, US4 (T0372; FR-051 a FR-056, FR-061, FR-099, FR-101, FR-189, FR-191, FR-205; contracts/api.md §4; data-model §8.1): el dinero que entrega la
// caja. Una sola operación con la autorización del módulo: el retiro y la cancelación con entrega van por el contrato de cuentas (Captaciones liquida su GMF), el saldo
// a favor de Cartera por ResolverSaldoAFavorAsync con RefundThroughCollector, el saldo a favor de un pagador de Recaudo se baja en Recaudo y el egreso vario por
// concepto, apagado por defecto, con aprobación y el tope del concepto. El efectivo de la sesión y el tope diario de la caja se miden antes de pedir la autorización.

/// <summary>Un egreso de caja. (nuevo)</summary>
public sealed record CashDisbursementDto(
    Guid PublicId,
    string Number,
    CashDisbursementKind Kind,
    Guid SessionPublicId,
    Guid BeneficiaryPersonPublicId,
    string BeneficiaryName,
    Guid? ReceivedByPersonPublicId,
    string? ReceivedByName,
    Guid? ProductPublicId,
    string? ProductNumber,
    string? ModuleVoucherNumber,
    decimal Amount,
    decimal? GmfAmount,
    bool GmfByOrigin,
    DateOnly OperationDate,
    DateTime RegisteredAt,
    CashDisbursementStatus Status,
    string VerificationCode,
    string? Reason,
    string? VoidingNumber);

/// <summary>Los errores del egreso de caja. (nuevo)</summary>
public static class ErroresDeEgreso
{
    public const string CodigoEfectivoInsuficiente = "Receipts.Disbursement.InsufficientCash";
    public const string CodigoTopeDiario = "Receipts.Disbursement.DailyCashOutCapReached";
    public const string CodigoEgresosVariosApagados = "Receipts.Disbursement.MiscExpensesDisabled";
    public const string CodigoConceptoNoEsEgreso = "Receipts.Disbursement.ConceptNotExpense";
    public const string CodigoSobreElTopeDelConcepto = "Receipts.Disbursement.OverConceptMax";
    public const string CodigoClaseNoAdmitida = "Receipts.Disbursement.KindNotSupported";
    public const string CodigoRechazadoPorElModulo = "Receipts.Disbursement.ModuleRejected";
    public const string CodigoValorDeCancelacion = "Receipts.Disbursement.CloseAmountMismatch";
    public const string CodigoYaAnulado = "Receipts.Disbursement.AlreadyVoided";
    public const string CodigoNoEsDeLaCaja = "Receipts.Disbursement.NotSameCashBox";

    public static Error EfectivoInsuficiente(decimal disponible, decimal valor) => new ErrorConDatos(CodigoEfectivoInsuficiente,
        string.Create(CultureInfo.InvariantCulture, $"La caja tiene {disponible:N0} en efectivo esperado y el egreso es de {valor:N0}: abastézcala desde la bóveda antes de entregar."),
        new { available = disponible, amount = valor });

    public static Error TopeDiario(decimal tope, decimal entregado) => new ErrorConDatos(CodigoTopeDiario,
        string.Create(CultureInfo.InvariantCulture, $"La caja ya entregó {entregado:N0} hoy y su tope diario de egresos es {tope:N0}."),
        new { cap = tope, deliveredToday = entregado });

    public static Error EgresosVariosApagados() => new(CodigoEgresosVariosApagados,
        "La cooperativa no entrega egresos varios por la caja (parámetro Caja.EgresosVarios).");

    public static Error ConceptoNoEsEgreso() => new(CodigoConceptoNoEsEgreso, "El concepto no es de egreso o no está activo.");

    public static Error SobreElTopeDelConcepto(decimal tope) => new ErrorConDatos(CodigoSobreElTopeDelConcepto,
        string.Create(CultureInfo.InvariantCulture, $"El egreso supera el tope del concepto ({tope:N0})."), new { maxAmount = tope });

    public static Error ClaseNoAdmitida(CashDisbursementKind clase) => new ErrorConDatos(CodigoClaseNoAdmitida,
        clase == CashDisbursementKind.CustodyRelease
            ? "El dinero retenido en custodia se devuelve desde la custodia (liberación por el oficial de cumplimiento), no como egreso de caja."
            : $"La clase de egreso {clase} no existe.",
        new { kind = clase.ToString() });

    public static Error RechazadoPorElModulo(string codigo, string mensaje) => new ErrorConDatos(CodigoRechazadoPorElModulo,
        $"El módulo no autorizó la entrega: {mensaje}", new { moduleCode = codigo });

    public static Error ValorDeCancelacion(decimal aEntregar) => new ErrorConDatos(CodigoValorDeCancelacion,
        string.Create(CultureInfo.InvariantCulture, $"La cancelación entrega {aEntregar:N2}: confirme ese valor."), new { amountToDeliver = aEntregar });

    public static Error YaAnulado() => new(CodigoYaAnulado, "El egreso ya está anulado.");

    public static Error NoEsDeLaCaja() => new(CodigoNoEsDeLaCaja,
        "El dinero de un egreso vuelve a una sesión abierta de la misma caja que lo entregó.");
}

/// <summary>Lo común del egreso de caja. (nuevo)</summary>
public static class EgresosDeCaja
{
    /// <summary>El sujeto de aprobación del egreso vario y de la anulación de un egreso (regla fija con <c>Receipts.Disbursements.Approve</c>, con monto).</summary>
    public const string Sujeto = ApprovalSubjects.CashDisbursementApproval;

    public const string PermisoDeAprobar = "Receipts.Disbursements.Approve";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>La clave del pedido al módulo: <c>REC:{egreso:N}:{producto:N}</c> (interfaces.md §2).</summary>
    public static string Clave(Guid egreso, Guid producto) => string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:{egreso:N}:{producto:N}");

    public static string Firmantes(IReadOnlyList<Guid> firmantes) => JsonSerializer.Serialize(firmantes, Json);

    /// <summary>El efectivo que la caja ya entregó hoy (egresos vivos de cualquier sesión de la caja).</summary>
    public static async Task<decimal> EntregadoHoyAsync(IApplicationDbContext db, int caja, DateOnly hoy, CancellationToken ct) =>
        await db.CashDisbursements.AsNoTracking().Where(e => e.CashBoxId == caja && e.OperationDate == hoy && e.Status == CashDisbursementStatus.Delivered)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

    public static async Task<CashDisbursementDto> DtoAsync(IApplicationDbContext db, CashDisbursement e, CancellationToken ct)
    {
        var sesion = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.Id == e.CashSessionId).Select(s => s.PublicId).FirstAsync(ct);
        var ids = new[] { e.BeneficiaryPersonId, e.ReceivedByPersonId ?? 0 }.Where(x => x != 0).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var anulacion = await db.CashDisbursementVoidings.AsNoTracking().Where(v => v.CashDisbursementId == e.Id).Select(v => v.Number).FirstOrDefaultAsync(ct);
        var recibe = e.ReceivedByPersonId is { } r ? personas[r] : null;
        return new CashDisbursementDto(e.PublicId, e.Number, e.Kind, sesion, personas[e.BeneficiaryPersonId].PublicId, OperacionesDeCaja.Nombre(personas[e.BeneficiaryPersonId]),
            recibe?.PublicId, recibe is null ? null : OperacionesDeCaja.Nombre(recibe), e.ProductPublicId, e.ProductNumber, e.ModuleAuthorizationNumber, e.Amount, e.GmfAmount,
            e.GmfByOrigin, e.OperationDate, e.RegisteredAt, e.Status, e.VerificationCode, e.Reason, anulacion);
    }

    /// <summary>Lee el <c>approvalPublicId</c> que Captaciones devuelve con <c>Lending.Savings.PendingApproval</c>.</summary>
    public static Guid? AprobacionDelModulo(IReadOnlyDictionary<string, object?>? datos)
    {
        if (datos is null) return null;
        foreach (var (k, v) in datos)
        {
            if (!string.Equals(k, "approvalPublicId", StringComparison.OrdinalIgnoreCase)) continue;
            return v switch
            {
                Guid g => g,
                string s when Guid.TryParse(s, out var g) => g,
                JsonElement j when j.ValueKind == JsonValueKind.String && Guid.TryParse(j.GetString(), out var g) => g,
                _ => null,
            };
        }
        return null;
    }
}

/// <summary><c>GET /api/receipts/cash-disbursements?session=&amp;date=</c> (<c>Receipts.Disbursements.View</c>) (nuevo).</summary>
public sealed record ListCashDisbursementsQuery(Guid? SessionPublicId = null, DateOnly? Date = null) : IRequest<Result<IReadOnlyList<CashDisbursementDto>>>;

public sealed class ListCashDisbursementsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListCashDisbursementsQuery, Result<IReadOnlyList<CashDisbursementDto>>>
{
    public async Task<Result<IReadOnlyList<CashDisbursementDto>>> Handle(ListCashDisbursementsQuery request, CancellationToken ct)
    {
        var consulta = db.CashDisbursements.AsNoTracking();
        if (request.SessionPublicId is { } s)
        {
            var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, s, seguir: false, ct);
            if (sesion is null) return Result.Failure<IReadOnlyList<CashDisbursementDto>>(ErroresDeRecaudo.NoEncontrado("La sesión"));
            consulta = consulta.Where(e => e.CashSessionId == sesion.Id);
        }
        if (request.Date is { } d) consulta = consulta.Where(e => e.OperationDate == d);
        var mio = await alcance.ObtenerAsync(ct);
        var filas = (await consulta.OrderByDescending(e => e.Id).Take(500).ToListAsync(ct))
            .Where(e => mio.IncluyeCaja(e.CashBoxId, e.CollectionPointId) || mio.IncluyeOficina(e.BranchId)).ToList();
        var lista = new List<CashDisbursementDto>(filas.Count);
        foreach (var e in filas) lista.Add(await EgresosDeCaja.DtoAsync(db, e, ct));
        return Result.Success<IReadOnlyList<CashDisbursementDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-disbursements</c> (<c>Receipts.Disbursements.Register</c>) (nuevo): entrega dinero por la caja en una sola operación —autorización del
/// módulo, egreso numerado, registro de efectivo <c>Out</c>, hechos— con la misma <c>Idempotency-Key</c> (repetir no entrega dos veces). Lo que pide aprobador presente
/// (el retiro sobre la atribución, el egreso vario) responde 428 y se repite con <c>X-Approval-Request</c>.
/// </summary>
public sealed record RegisterCashDisbursementCommand(
    Guid SessionPublicId,
    CashDisbursementKind Kind,
    Guid BeneficiaryPersonPublicId,
    decimal Amount,
    Guid? ProductPublicId = null,
    Guid? PayerCreditBalancePublicId = null,
    Guid? MiscConceptPublicId = null,
    Guid? ReceivedByPersonPublicId = null,
    IReadOnlyList<Guid>? SignerPersonPublicIds = null,
    Guid? SignatureAttachmentPublicId = null,
    string? Reason = null) : IRequest<Result<CashDisbursementDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class RegisterCashDisbursementCommandValidator : AbstractValidator<RegisterCashDisbursementCommand>
{
    public RegisterCashDisbursementCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.BeneficiaryPersonPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.ProductPublicId).NotEmpty()
            .When(x => x.Kind is CashDisbursementKind.SavingsWithdrawal or CashDisbursementKind.AccountClosingDelivery or CashDisbursementKind.LendingCreditBalanceRefund)
            .WithMessage("Indique la cuenta o el crédito de donde sale el dinero.");
        RuleFor(x => x.PayerCreditBalancePublicId).NotEmpty().When(x => x.Kind == CashDisbursementKind.PayerCreditBalanceRefund)
            .WithMessage("Indique el saldo a favor que se devuelve.");
        RuleFor(x => x.MiscConceptPublicId).NotEmpty().When(x => x.Kind == CashDisbursementKind.MiscExpense).WithMessage("Indique el concepto del egreso.");
        RuleFor(x => x.Reason).NotEmpty().When(x => x.Kind == CashDisbursementKind.MiscExpense).WithMessage("El egreso vario lleva su motivo.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RegisterCashDisbursementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    ParametrosDeCaja parametros,
    AprobacionPresenteEnCaja aprobacion,
    IContratoDeCuentas cuentas,
    IContratoDeObligaciones obligaciones,
    IListasDeRecaudo listas,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    Compliance.RegistroDeEfectivo? registroDeEfectivo = null) : IRequestHandler<RegisterCashDisbursementCommand, Result<CashDisbursementDto>>
{
    public async Task<Result<CashDisbursementDto>> Handle(RegisterCashDisbursementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } cajero) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var clave = request.OperationKey == Guid.Empty ? Guid.NewGuid() : request.OperationKey;
        if (await db.CashDisbursements.AsNoTracking().FirstOrDefaultAsync(e => e.OperationKey == clave, ct) is { } ya)
            return Result.Success(await EgresosDeCaja.DtoAsync(db, ya, ct));
        if (request.Kind == CashDisbursementKind.CustodyRelease) return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.ClaseNoAdmitida(request.Kind));

        if (await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct) is not { } sesion)
            return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var operable = SesionesDeCaja.Operable(sesion);
        if (operable.IsFailure) return Result.Failure<CashDisbursementDto>(operable.Error);
        if (sesion.CashierUserId != cajero) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEsCajeroDeLaCaja());
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var hoy = reloj.HoyLocal;

        var beneficiario = await db.People.AsNoTracking().Where(p => p.PublicId == request.BeneficiaryPersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (beneficiario is null) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("El beneficiario"));
        int? recibe = null;
        if (request.ReceivedByPersonPublicId is { } r && r != request.BeneficiaryPersonPublicId)
        {
            recibe = await db.People.AsNoTracking().Where(p => p.PublicId == r).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (recibe is null) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("Quien recibe"));
        }
        var personas = new[] { beneficiario.Value, recibe ?? 0 }.Where(x => x != 0).Distinct().OrderBy(x => x).ToList();
        var firmantes = request.SignerPersonPublicIds ?? [];

        // 1. Listas: una coincidencia vinculante no entrega dinero (FR-189); quien recibe también se cruza (FR-191).
        if (await listas.EvaluarAsync(personas, clave, ct) == ScreeningOutcomeR.BindingMatch)
            return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.ListaVinculante());

        // 2. Lo propio de cada clase que no escribe: el concepto, el saldo a favor de pagador, el valor de la cancelación (simulación).
        PayerCreditBalance? saldoDePagador = null;
        int? concepto = null;
        Guid? aprobacionVaria = null;
        int? aprobadorVario = null;
        var valor = request.Amount;
        SimulacionDeCuentaDto? simulacion = null;
        switch (request.Kind)
        {
            case CashDisbursementKind.MiscExpense:
            {
                if (!await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaEgresosVarios, hoy, ct))
                    return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.EgresosVariosApagados());
                var c = await db.MiscConcepts.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.MiscConceptPublicId, ct);
                if (c is null || c.Direction != MiscConceptDirection.Expense || c.Status != ReceiptsCatalogStatus.Active)
                    return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.ConceptoNoEsEgreso());
                if (c.MaxAmount is { } tope && valor > tope) return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.SobreElTopeDelConcepto(tope));
                concepto = c.Id;
                break;
            }
            case CashDisbursementKind.PayerCreditBalanceRefund:
            {
                saldoDePagador = await db.PayerCreditBalances.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.PayerCreditBalancePublicId, ct);
                if (saldoDePagador is null || saldoDePagador.PersonId != beneficiario || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(saldoDePagador.BranchId))
                    return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("El saldo a favor"));
                if (saldoDePagador.Status == PayerCreditBalanceState.Resolved) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.SaldoResuelto());
                if (valor > saldoDePagador.Balance) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.SaldoInsuficiente(saldoDePagador.Balance));
                break;
            }
            case CashDisbursementKind.SavingsWithdrawal or CashDisbursementKind.AccountClosingDelivery:
            {
                var operacion = request.Kind == CashDisbursementKind.AccountClosingDelivery ? "Close" : "Withdrawal";
                var sim = await cuentas.SimularAsync(new SolicitudDeSimulacionDeCuenta(ClavesDeRecaudo.Recaudador, operacion, request.ProductPublicId!.Value, valor, hoy,
                    ClaseDeMedioDeRecaudo.Cash, firmantes), ct);
                if (sim.Result == ResultadoDelContrato.Rejected || sim.Value is null)
                    return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.RechazadoPorElModulo(sim.Code, sim.Message));
                simulacion = sim.Value;
                if (request.Kind == CashDisbursementKind.AccountClosingDelivery)
                {
                    var aEntregar = sim.Value.AmountToDeliver ?? 0m;
                    if (aEntregar != valor) return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.ValorDeCancelacion(aEntregar));
                }
                break;
            }
        }

        // 3. El efectivo de la sesión y el tope diario, antes de pedir la autorización (FR-101).
        var efectivo = await SesionesDeCaja.EfectivoAsync(db, sesion, ct);
        if (!ReglasDeEgresoDeCaja.AlcanzaElEfectivo(efectivo, valor)) return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.EfectivoInsuficiente(efectivo, valor));
        var entregadoHoy = await EgresosDeCaja.EntregadoHoyAsync(db, sesion.CashBoxId, hoy, ct);
        if (ReglasDeEgresoDeCaja.SuperaElTopeDiario(sellados.DailyCashOutCap, entregadoHoy, valor))
            return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.TopeDiario(sellados.DailyCashOutCap!.Value, entregadoHoy));

        // 4. El egreso vario pide siempre aprobador presente (otra persona, con monto).
        if (request.Kind == CashDisbursementKind.MiscExpense)
        {
            var huella = HuellaDeRecibo.Calcular(new { request.Kind, request.BeneficiaryPersonPublicId, request.MiscConceptPublicId, valor, request.Reason });
            var obtenida = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(EgresosDeCaja.Sujeto, clave, $"Egreso vario de caja: {request.Reason}", valor, hoy,
                huella, EgresosDeCaja.PermisoDeAprobar, [cajero], request.ApprovalRequestPublicId), ct);
            if (obtenida.IsFailure) return Result.Failure<CashDisbursementDto>(obtenida.Error);
            (aprobacionVaria, aprobadorVario) = (obtenida.Value.ApprovalRequestPublicId, obtenida.Value.AprobadorUserId);
        }

        // 5. Cerrojos propios en su orden.
        var (oficina, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        await cerrojo.BloquearPagadoresAsync(personas, ct);
        // Bajo el cerrojo de la sesión, el efectivo otra vez (otro egreso pudo salir entre los dos pasos).
        efectivo = await SesionesDeCaja.EfectivoAsync(db, sesion, ct);
        if (!ReglasDeEgresoDeCaja.AlcanzaElEfectivo(efectivo, valor)) return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.EfectivoInsuficiente(efectivo, valor));

        // 6. Numeración, antes de los contratos.
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashDisbursement, punto, ct);
        var numero = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
        var oficinaPublica = await db.Branches.AsNoTracking().Where(b => b.Id == oficina).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        var referencia = new ReferenciaDeRecibo(numero, hoy, oficinaPublica, null, nameof(ReceiptDocumentKind.CashDisbursement));
        var egreso = new CashDisbursement
        {
            Number = numero, DocumentTypeId = tipo.Id, Kind = request.Kind, CashSessionId = sesion.Id, CashBoxId = sesion.CashBoxId, CollectionPointId = punto,
            BranchId = oficina, CashierUserId = cajero, BeneficiaryPersonId = beneficiario.Value, ReceivedByPersonId = recibe, Amount = valor,
            SignersJson = firmantes.Count > 0 ? EgresosDeCaja.Firmantes(firmantes) : null, SignatureAttachmentPublicId = request.SignatureAttachmentPublicId,
            IdempotencyKey = string.Empty, OperationKey = clave, OperationDate = hoy, RegisteredAt = reloj.UtcNow, Reason = request.Reason,
            VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(), MiscConceptId = concepto, PayerCreditBalanceId = saldoDePagador?.Id,
            ApprovalRequestPublicId = aprobacionVaria,
        };

        // 7. La autorización del módulo.
        string? naturaleza = null;
        switch (request.Kind)
        {
            case CashDisbursementKind.SavingsWithdrawal or CashDisbursementKind.AccountClosingDelivery:
            {
                var producto = request.ProductPublicId!.Value;
                egreso.IdempotencyKey = EgresosDeCaja.Clave(clave, producto);
                var retiro = await cuentas.RetirarAsync(new SolicitudDeRetiro(ClavesDeRecaudo.Recaudador, egreso.IdempotencyKey, producto, valor, hoy, ClaseDeMedioDeRecaudo.Cash,
                    firmantes, request.ReceivedByPersonPublicId ?? request.BeneficiaryPersonPublicId, referencia,
                    CloseAccount: request.Kind == CashDisbursementKind.AccountClosingDelivery, SimulationId: simulacion?.SimulationId,
                    ApprovalPublicId: request.ApprovalRequestPublicId), ct);
                if (retiro.Result == ResultadoDelContrato.Rejected || retiro.Value is null)
                {
                    // Sobre la atribución, Captaciones deja la solicitud de aprobación: la caja la pide presente y repite con X-Approval-Request.
                    if (retiro.Code == CodigosDelContratoDeCuentas.PendienteDeAprobacion
                        && EgresosDeCaja.AprobacionDelModulo(retiro.Data) is { } pedida)
                        return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.PresenciaRequerida(pedida, ApprovalSubjects.AccountWithdrawal, valor,
                            "Lending.Accounts.Withdraw"));
                    return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.RechazadoPorElModulo(retiro.Code, retiro.Message));
                }
                var v = retiro.Value;
                (egreso.ProductPublicId, egreso.ProductNumber, egreso.ModuleAuthorizationPublicId, egreso.ModuleAuthorizationNumber) =
                    (v.AccountPublicId, v.AccountNumber, v.OperationPublicId, v.CashAuthorization?.AuthorizationCode ?? v.Number);
                (egreso.GmfAmount, egreso.GmfByOrigin) = (v.Gmf?.Amount ?? simulacion?.Gmf?.Amount, true);
                egreso.ApprovalRequestPublicId = request.ApprovalRequestPublicId;
                naturaleza = await NaturalezaAsync(request.BeneficiaryPersonPublicId, v.AccountPublicId, hoy, ct);
                break;
            }
            case CashDisbursementKind.LendingCreditBalanceRefund:
            {
                var producto = request.ProductPublicId!.Value;
                egreso.IdempotencyKey = EgresosDeCaja.Clave(clave, producto);
                var resolucion = await obligaciones.ResolverSaldoAFavorAsync(new SolicitudDeResolucion(ClavesDeRecaudo.Recaudador, egreso.IdempotencyKey, producto,
                    ConceptoDeObligacion.CreditBalance, valor, FormaDeResolucion.RefundThroughCollector, referencia, Reason: request.Reason), ct);
                if (resolucion.Result == ResultadoDelContrato.Rejected || resolucion.Value is null)
                    return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.RechazadoPorElModulo(resolucion.Code, resolucion.Message));
                (egreso.ProductPublicId, egreso.ModuleAuthorizationPublicId, egreso.ModuleAuthorizationNumber) =
                    (resolucion.Value.ObligationPublicId, resolucion.Value.OperationPublicId, resolucion.Value.Number);
                naturaleza = "Loan";
                break;
            }
            case CashDisbursementKind.PayerCreditBalanceRefund:
                egreso.IdempotencyKey = string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:{clave:N}:{saldoDePagador!.PublicId:N}");
                egreso.ProductPublicId = saldoDePagador.PublicId;
                egreso.ProductNumber = saldoDePagador.OriginDocumentNumber;
                break;
            default:
                egreso.IdempotencyKey = string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:{clave:N}");
                break;
        }

        // 8. El GMF que causa Recaudo (FR-205): nunca el que el origen ya liquidó.
        var gmf = GmfDeEgreso.No("OrigenLoLiquido");
        decimal tarifa = 0m;
        if (!egreso.GmfByOrigin)
        {
            tarifa = await parametros.DecimalAsync(ParametrosDeRecaudoYTesoreria.GmfTarifa, hoy, ct) ?? 0m;
            gmf = ReglasDeEgresoDeCaja.Gmf(request.Kind, valor, tarifa,
                await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.GmfHechosGeneradoresDeEgresos, hoy, ct),
                CalculoDeGmf.Redondeo(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.GmfRedondeo, hoy, ct)));
            if (gmf.Causado) egreso.GmfAmount = gmf.Valor;
        }

        egreso.ProductNature = naturaleza;
        db.CashDisbursements.Add(egreso);
        await db.SaveChangesAsync(ct);
        if (saldoDePagador is not null)
        {
            var saldo = await db.PayerCreditBalances.FirstAsync(s => s.Id == saldoDePagador.Id, ct);
            SaldosAFavor.Bajar(db, saldo, PayerCreditBalanceMovementKind.RefundedByCash, valor, egreso.PublicId, egreso.Number, cajero, egreso.RegisteredAt, null);
        }
        await SesionesDeCaja.ProyectarAsync(db, await db.ReceiptsCashSessions.FirstAsync(s => s.Id == sesion.Id, ct), ct);

        // 9. Hechos, registro de efectivo y auditoría, en la misma transacción.
        await emision.EgresoDeCajaEntregadoAsync(egreso, tipo.Code, ct);
        if (gmf.Causado) await emision.GmfDeEgresoCausadoAsync(egreso, gmf, tarifa, tipo.Code, ct);
        if (registroDeEfectivo is not null) await registroDeEfectivo.AlEgresarAsync(egreso, ct);
        await auditoria.AgregarAsync("Receipts.Disbursement.Delivered", "CashDisbursement", egreso.PublicId, null,
            new { number = egreso.Number, kind = egreso.Kind.ToString(), amount = valor, product = egreso.ProductNumber, module = egreso.ModuleAuthorizationNumber,
                gmf = egreso.GmfAmount, gmfByOrigin = egreso.GmfByOrigin, approvedBy = aprobadorVario, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await EgresosDeCaja.DtoAsync(db, egreso, ct));
    }

    private async Task<string?> NaturalezaAsync(Guid persona, Guid cuenta, DateOnly hoy, CancellationToken ct)
    {
        var r = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(ClavesDeRecaudo.Recaudador, persona, hoy, IncludeAsSignatory: true), ct);
        return r.Value?.Accounts.FirstOrDefault(c => c.AccountPublicId == cuenta)?.Nature.ToString();
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-disbursements/{id}/void</c> (<c>Receipts.Disbursements.Register</c>; aprueba otra persona presente con
/// <c>Receipts.Disbursements.Approve</c>) (nuevo; FR-061): sólo si el dinero vuelve a la caja —una sesión abierta de la misma caja, de quien anula—. Reversa la
/// operación del módulo por su contrato (el retiro, la cancelación, la resolución del saldo de Cartera), reabre el saldo a favor del pagador y emite el espejo.
/// </summary>
public sealed record VoidCashDisbursementCommand(Guid DisbursementPublicId, Guid SessionPublicId, string Reason) : IRequest<Result<CashDisbursementDto>>, IOperacionIdempotente,
    IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class VoidCashDisbursementCommandValidator : AbstractValidator<VoidCashDisbursementCommand>
{
    public VoidCashDisbursementCommandValidator()
    {
        RuleFor(x => x.DisbursementPublicId).NotEmpty();
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("La anulación lleva su motivo.");
    }
}

public sealed class VoidCashDisbursementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    AprobacionPresenteEnCaja aprobacion,
    IContratoDeCuentas cuentas,
    IContratoDeObligaciones obligaciones,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    Compliance.RegistroDeEfectivo? registroDeEfectivo = null) : IRequestHandler<VoidCashDisbursementCommand, Result<CashDisbursementDto>>
{
    public async Task<Result<CashDisbursementDto>> Handle(VoidCashDisbursementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await db.CashDisbursements.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == request.DisbursementPublicId, ct);
        var mio = await alcance.ObtenerAsync(ct);
        if (leido is null || !(mio.IncluyeCaja(leido.CashBoxId, leido.CollectionPointId) || mio.IncluyeOficina(leido.BranchId)))
            return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("El egreso"));
        if (leido.Status == CashDisbursementStatus.Voided) return Result.Success(await EgresosDeCaja.DtoAsync(db, leido, ct));
        if (await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct) is not { } sesion)
            return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var operable = SesionesDeCaja.Operable(sesion);
        if (operable.IsFailure) return Result.Failure<CashDisbursementDto>(operable.Error);
        if (sesion.CashBoxId != leido.CashBoxId) return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.NoEsDeLaCaja());
        if (sesion.CashierUserId != usuario) return Result.Failure<CashDisbursementDto>(ErroresDeRecaudo.NoEsCajeroDeLaCaja());
        var hoy = reloj.HoyLocal;

        var huella = HuellaDeRecibo.Calcular(new { leido.PublicId, leido.Amount, request.Reason });
        var obtenida = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(EgresosDeCaja.Sujeto, leido.PublicId, $"Anulación del egreso {leido.Number}", leido.Amount,
            hoy, huella, EgresosDeCaja.PermisoDeAprobar, [usuario, leido.CashierUserId], request.ApprovalRequestPublicId), ct);
        if (obtenida.IsFailure) return Result.Failure<CashDisbursementDto>(obtenida.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        await cerrojo.BloquearPagadoresAsync(new[] { leido.BeneficiaryPersonId, leido.ReceivedByPersonId ?? 0 }.Where(x => x != 0).Distinct().OrderBy(x => x).ToList(), ct);
        var egreso = await db.CashDisbursements.FirstAsync(e => e.Id == leido.Id, ct);
        if (egreso.Status == CashDisbursementStatus.Voided) return Result.Success(await EgresosDeCaja.DtoAsync(db, egreso, ct));

        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.Voiding, egreso.CollectionPointId, ct);
        var numero = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == egreso.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        var referencia = new ReferenciaDeRecibo(numero, hoy, oficina, null, nameof(ReceiptDocumentKind.Voiding));

        Guid? reverso = null;
        string? numeroDelReverso = null;
        switch (egreso.Kind)
        {
            case CashDisbursementKind.SavingsWithdrawal or CashDisbursementKind.AccountClosingDelivery:
            {
                var r = await cuentas.ReversarAsync(new SolicitudDeReversoDeCuenta(ClavesDeRecaudo.Recaudador, egreso.IdempotencyKey, request.Reason, referencia), ct);
                if (r.Result == ResultadoDelContrato.Rejected || r.Value is null)
                    return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.RechazadoPorElModulo(r.Code, r.Message));
                (reverso, numeroDelReverso) = (r.Value.OperationPublicId, r.Value.Number);
                break;
            }
            case CashDisbursementKind.LendingCreditBalanceRefund:
            {
                var r = await obligaciones.ReversarAsync(new SolicitudDeReverso(ClavesDeRecaudo.Recaudador, egreso.IdempotencyKey, request.Reason, referencia), ct);
                if (r.Result == ResultadoDelContrato.Rejected || r.Value is null)
                    return Result.Failure<CashDisbursementDto>(ErroresDeEgreso.RechazadoPorElModulo(r.Code, r.Message));
                (reverso, numeroDelReverso) = (r.Value.OperationPublicId, r.Value.Number);
                break;
            }
            case CashDisbursementKind.PayerCreditBalanceRefund when egreso.PayerCreditBalanceId is { } s:
            {
                var saldo = await db.PayerCreditBalances.FirstAsync(x => x.Id == s, ct);
                saldo.Balance += egreso.Amount;
                saldo.Status = PayerCreditBalanceState.Open;
                db.PayerCreditBalanceMovements.Add(new PayerCreditBalanceMovement
                {
                    PayerCreditBalanceId = saldo.Id, Kind = PayerCreditBalanceMovementKind.CashRefundVoided, Amount = egreso.Amount, DocumentPublicId = egreso.PublicId,
                    DocumentNumber = egreso.Number, RecordedAt = reloj.UtcNow, Note = $"Egreso {egreso.Number} anulado: {request.Reason}",
                });
                break;
            }
        }

        var anulacion = new CashDisbursementVoiding
        {
            CashDisbursementId = egreso.Id, Number = numero, Reason = request.Reason, CashSessionId = sesion.Id, RequestedByUserId = usuario,
            ApprovedByUserId = obtenida.Value.AprobadorUserId, ApprovalRequestPublicId = obtenida.Value.ApprovalRequestPublicId, ModuleReversalPublicId = reverso,
            ModuleReversalNumber = numeroDelReverso, VoidedAt = reloj.UtcNow, VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(),
        };
        db.CashDisbursementVoidings.Add(anulacion);
        egreso.Status = CashDisbursementStatus.Voided;
        await db.SaveChangesAsync(ct);
        // El dinero vuelve como entrada de la sesión abierta; la que lo entregó no cambia.
        await SesionesDeCaja.ProyectarAsync(db, await db.ReceiptsCashSessions.FirstAsync(s => s.Id == sesion.Id, ct), ct);
        await emision.EgresoDeCajaAnuladoAsync(egreso, anulacion, tipo.Code, ct);
        if (registroDeEfectivo is not null) await registroDeEfectivo.AlAnularEgresoAsync(egreso, anulacion, ct);
        await auditoria.AgregarAsync("Receipts.Disbursement.Voided", "CashDisbursement", egreso.PublicId, null,
            new { number = egreso.Number, voiding = numero, reason = request.Reason, module = numeroDelReverso, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await EgresosDeCaja.DtoAsync(db, egreso, ct));
    }
}

/// <summary>El documento del egreso de caja firmado (FR-051, FR-191): número, código de verificación, lo que se entrega y de dónde, el GMF y las firmas. (nuevo)</summary>
public sealed record EgresoModel(
    IngenIA365ERP.Application.Receipts.Sessions.EmpresaDelDocumentoDto Company,
    string Number,
    string VerificationCode,
    string Kind,
    string CashBox,
    string Cashier,
    DateOnly OperationDate,
    DateTime RegisteredAt,
    string Beneficiary,
    string? ReceivedBy,
    string? Product,
    string? ModuleVoucher,
    decimal Amount,
    decimal? Gmf,
    bool GmfByOrigin,
    string? Reason,
    bool Voided,
    string? VoidingNumber);

/// <summary><c>GET /api/receipts/cash-disbursements/{id}/document</c> (<c>Receipts.Disbursements.View</c>) (nuevo).</summary>
public sealed record GetCashDisbursementDocumentQuery(Guid DisbursementPublicId) : IRequest<Result<EgresoModel>>;

public sealed class GetCashDisbursementDocumentQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetCashDisbursementDocumentQuery, Result<EgresoModel>>
{
    public static string Clase(CashDisbursementKind k) => k switch
    {
        CashDisbursementKind.SavingsWithdrawal => "Retiro de ahorro",
        CashDisbursementKind.AccountClosingDelivery => "Cancelación de cuenta con entrega",
        CashDisbursementKind.LendingCreditBalanceRefund => "Devolución de saldo a favor de crédito",
        CashDisbursementKind.PayerCreditBalanceRefund => "Devolución de saldo a favor",
        CashDisbursementKind.MiscExpense => "Egreso vario",
        _ => k.ToString(),
    };

    public async Task<Result<EgresoModel>> Handle(GetCashDisbursementDocumentQuery request, CancellationToken ct)
    {
        var e = await db.CashDisbursements.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.DisbursementPublicId, ct);
        var mio = await alcance.ObtenerAsync(ct);
        if (e is null || !(mio.IncluyeCaja(e.CashBoxId, e.CollectionPointId) || mio.IncluyeOficina(e.BranchId)))
            return Result.Failure<EgresoModel>(ErroresDeRecaudo.NoEncontrado("El egreso"));
        var dto = await EgresosDeCaja.DtoAsync(db, e, ct);
        var caja = await db.CashBoxes.AsNoTracking().Where(c => c.Id == e.CashBoxId).Select(c => c.Code).FirstAsync(ct);
        return Result.Success(new EgresoModel(await IngenIA365ERP.Application.Receipts.Sessions.DocumentosDeCajaDeRecaudo.EmpresaAsync(db, ct), e.Number, e.VerificationCode,
            Clase(e.Kind), caja, await SesionesDeCaja.NombreAsync(db, e.CashierUserId, ct), e.OperationDate, e.RegisteredAt, dto.BeneficiaryName, dto.ReceivedByName,
            e.ProductNumber, e.ModuleAuthorizationNumber, e.Amount, e.GmfAmount, e.GmfByOrigin, e.Reason, e.Status == CashDisbursementStatus.Voided, dto.VoidingNumber));
    }
}
