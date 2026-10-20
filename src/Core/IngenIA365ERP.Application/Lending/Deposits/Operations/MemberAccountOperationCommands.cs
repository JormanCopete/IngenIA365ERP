using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

// Feature 014, US4 (T111, T123; api.md §4, §10): lo que la pantalla de la Cartera hace con los movimientos de una cuenta —consultar operaciones y libro,
// simular, retirar por orden de giro, trasladar entre cuentas y reversar— y el uso del tope del GMF de una persona. El dinero en efectivo entra y sale por el
// contrato de cuentas (Recaudo); la pantalla no consigna. Todo dentro del alcance de la Cartera (oficina y línea de la cuenta). (nuevo)

/// <summary>El recaudador de una operación, si la trajo uno. (nuevo)</summary>
public sealed record MemberAccountCollectorDto(string Code, string Class);

/// <summary>Una operación de la cuenta (api.md §4 <c>MemberAccountOperationDto</c>). (nuevo)</summary>
public sealed record MemberAccountOperationDto(
    Guid PublicId,
    string Number,
    MemberAccountOperationKind Kind,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    MemberAccountCollectorDto? Collector,
    string? MeansClass,
    string? ReceiptReference,
    Guid? ReversedBy,
    Guid? Reverses,
    string Origin,
    string? Reason,
    DateOnly? ClearingUntil,
    bool Cleared);

/// <summary>Una fila del libro (api.md §4, <c>/transactions</c>). (nuevo)</summary>
public sealed record MemberAccountTransactionDto(
    long Id,
    MemberAccountTransactionKind Kind,
    MemberAccountConcept Concept,
    decimal Amount,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    Guid? OperationPublicId,
    long? ReversesTransaction,
    bool CountsAsHolderActivity,
    Guid? MessagePublicId);

/// <summary>Un hecho de una operación con su estado en la bandeja. (nuevo)</summary>
public sealed record MemberAccountOperationMessageDto(string Type, Guid MessagePublicId, string Status);

/// <summary>El comprobante de una operación (api.md §4, <c>GET /{id}/operations/{operationId}</c>). (nuevo)</summary>
public sealed record MemberAccountOperationDetailDto(
    MemberAccountOperationDto Operation,
    IReadOnlyList<MemberAccountTransactionDto> Transactions,
    IReadOnlyList<MemberAccountOperationMessageDto> Messages,
    GmfCalculadoDto? Gmf,
    IReadOnlyList<CargoCalculadoDto> Charges);

/// <summary>La orden de giro de un retiro por giro. (nuevo)</summary>
public sealed record MemberAccountOrderDto(Guid PublicId, string Number, string Status);

/// <summary>
/// El resultado de un retiro o un traslado de la pantalla (api.md §4, §1.6): aplicado con su comprobante (y la orden de giro), o en espera de un aprobador
/// presente (<c>PendingApproval</c>, 202) sin mover saldo. (nuevo)
/// </summary>
public sealed record MemberAccountOperationResultDto(
    string Status,
    ComprobanteDeCuentaDto? Operation,
    MemberAccountOrderDto? Order,
    Guid? ApprovalPublicId,
    AtribucionRequeridaDto? Required)
{
    public const string Aplicada = "Applied";
    public const string PendienteDeAprobacion = "PendingApproval";
}

/// <summary>Las firmas que pide una simulación. (nuevo)</summary>
public sealed record MemberAccountSignaturesDto(IReadOnlyList<Guid> Required, IReadOnlyList<Guid> Missing);

/// <summary>El GMF de una simulación con lo que queda del tope (api.md §4, <c>simulate</c>). (nuevo)</summary>
public sealed record MemberAccountSimulationGmfDto(decimal Base, decimal ExemptUsed, decimal ExemptRemaining, decimal Amount, string Explanation);

/// <summary>La atribución que pediría la operación. (nuevo)</summary>
public sealed record MemberAccountAttributionDto(bool RequiresApproval, string? PermissionCode, decimal? MaxAmount);

/// <summary>La simulación de la pantalla (api.md §4, <c>POST /{id}/operations/simulate</c>, sin clave). (nuevo)</summary>
public sealed record MemberAccountSimulationDto(
    bool Allowed,
    DisponibleDto Available,
    DisponibleDto AvailableAfter,
    MemberAccountSimulationGmfDto? Gmf,
    IReadOnlyList<CargoCalculadoDto> Charges,
    MemberAccountSignaturesDto Signatures,
    MemberAccountAttributionDto Attribution,
    IReadOnlyList<string> Warnings);

/// <summary>El uso del tope exento del GMF de una persona en un mes (api.md §10, <c>GET /gmf-exemptions/usage</c>). (nuevo)</summary>
public sealed record GmfMonthlyUsageDto(
    GmfExemptionPolicy Policy,
    decimal CapUvt,
    decimal CapAmount,
    decimal UsedExempt,
    decimal Remaining,
    decimal Taxed,
    decimal GmfCharged,
    IReadOnlyList<GmfUsageLineDto> Lines);

/// <summary>Una disposición del mes. (nuevo)</summary>
public sealed record GmfUsageLineDto(Guid OperationPublicId, string OperationNumber, string AccountNumber, string TaxableEvent, decimal Disposed, decimal Exempt,
    decimal TaxedBase, decimal Gmf, bool IsReversal);

/// <summary>El beneficiario bancario de un retiro por giro. (nuevo)</summary>
public sealed record WithdrawalPayeeInput(Guid PersonPublicId, Guid? BankPublicId, byte? AccountType, string? AccountNumber);

// ------------------------------------------------------------------------------------------------------------- consultas --

/// <summary>Las operaciones de una cuenta (api.md §4, <c>GET /{id}/operations</c>, <c>Lending.Accounts.Read</c>). (nuevo)</summary>
public sealed record ListMemberAccountOperationsQuery(Guid AccountPublicId, MemberAccountOperationKind? Kind = null, DateOnly? From = null, DateOnly? To = null,
    int Page = 1, int PageSize = 50) : IRequest<Result<PagedResult<MemberAccountOperationDto>>>;

public sealed class ListMemberAccountOperationsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListMemberAccountOperationsQuery, Result<PagedResult<MemberAccountOperationDto>>>
{
    public async Task<Result<PagedResult<MemberAccountOperationDto>>> Handle(ListMemberAccountOperationsQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<PagedResult<MemberAccountOperationDto>>(leida.Error);
        var cuenta = leida.Value;
        var consulta = db.MemberAccountOperations.AsNoTracking()
            .Where(o => (o.MemberAccountId == cuenta.Id || o.CounterpartMemberAccountId == cuenta.Id) && !o.IsDeleted);
        if (request.Kind is { } clase) consulta = consulta.Where(o => o.Kind == clase);
        if (request.From is { } desde) consulta = consulta.Where(o => o.ValueDate >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(o => o.ValueDate <= hasta);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(o => o.RegisteredAt).ThenByDescending(o => o.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        await auditoria.EmitirMovimientosDeCuentaVistosAsync(cuenta.PublicId, new { request.Kind, request.From, request.To, page = pagina.SafePage }, filas.Count, ct);
        return Result.Success(new PagedResult<MemberAccountOperationDto>(await OperacionesDeCuenta.DtosAsync(db, filas, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

/// <summary>El comprobante de una operación (api.md §4, <c>GET /{id}/operations/{operationId}</c>). (nuevo)</summary>
public sealed record GetMemberAccountOperationQuery(Guid AccountPublicId, Guid OperationPublicId) : IRequest<Result<MemberAccountOperationDetailDto>>;

public sealed class GetMemberAccountOperationQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetMemberAccountOperationQuery, Result<MemberAccountOperationDetailDto>>
{
    public async Task<Result<MemberAccountOperationDetailDto>> Handle(GetMemberAccountOperationQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountOperationDetailDto>(leida.Error);
        var cuenta = leida.Value;
        var operacion = await db.MemberAccountOperations.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.OperationPublicId && !o.IsDeleted
            && (o.MemberAccountId == cuenta.Id || o.CounterpartMemberAccountId == cuenta.Id), ct);
        if (operacion is null) return Result.Failure<MemberAccountOperationDetailDto>(ErroresDeAportes.OperacionInexistente());
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == operacion.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var mensajes = filas.Where(f => f.MessagePublicId is not null).Select(f => f.MessagePublicId!.Value).Distinct().ToList();
        var hechos = await db.IntegrationMessages.AsNoTracking().Where(m => mensajes.Contains(m.PublicId) || m.OriginPublicId == operacion.PublicId)
            .Select(m => new { m.Type, m.PublicId, m.Id }).ToListAsync(ct);
        var ids = hechos.Select(h => h.Id).ToList();
        var entregas = await db.IntegrationMessageDeliveries.AsNoTracking().Where(d => ids.Contains(d.MessageId))
            .Select(d => new { d.MessageId, d.Status }).ToListAsync(ct);
        var uso = await db.GmfMonthlyUsages.AsNoTracking().FirstOrDefaultAsync(u => u.OperationId == operacion.Id, ct);
        var cargos = await db.AccountCharges.AsNoTracking().Where(c => c.TriggerOperationId == operacion.Id || c.CollectedOperationId == operacion.Id
                || c.AppliedOperationId == operacion.Id)
            .Select(c => new CargoCalculadoDto(c.ChargeCode, c.Amount, c.IsVatTaxed, c.Status.ToString())).ToListAsync(ct);
        await auditoria.EmitirMovimientosDeCuentaVistosAsync(cuenta.PublicId, new { operation = operacion.Number }, filas.Count, ct);
        return Result.Success(new MemberAccountOperationDetailDto((await OperacionesDeCuenta.DtosAsync(db, [operacion], ct))[0],
            filas.Select(OperacionesDeCuenta.Fila).ToList(),
            hechos.Select(h => new MemberAccountOperationMessageDto(h.Type, h.PublicId,
                string.Join(", ", entregas.Where(e => e.MessageId == h.Id).Select(e => e.Status.ToString()).Distinct()))).ToList(),
            uso is null ? null : new GmfCalculadoDto(uso.DisposedAmount, 0m, uso.ExemptAmount, uso.TaxableBase, uso.Rate, uso.GmfAmount, uso.Policy.ToString(),
                uso.TaxableEventCode),
            cargos));
    }
}

/// <summary>El libro de la cuenta (api.md §4, <c>GET /{id}/transactions</c>, sólo lectura; lectura sensible). (nuevo)</summary>
public sealed record ListMemberAccountTransactionsQuery(Guid AccountPublicId, DateOnly? From = null, DateOnly? To = null, MemberAccountConcept? Concept = null,
    string DateKind = "value", int Page = 1, int PageSize = 100) : IRequest<Result<PagedResult<MemberAccountTransactionDto>>>;

public sealed class ListMemberAccountTransactionsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListMemberAccountTransactionsQuery, Result<PagedResult<MemberAccountTransactionDto>>>
{
    public async Task<Result<PagedResult<MemberAccountTransactionDto>>> Handle(ListMemberAccountTransactionsQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<PagedResult<MemberAccountTransactionDto>>(leida.Error);
        var cuenta = leida.Value;
        var consulta = db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == cuenta.Id && !t.IsDeleted);
        if (request.Concept is { } concepto) consulta = consulta.Where(t => t.Concept == concepto);
        var porContable = string.Equals(request.DateKind, "accounting", StringComparison.OrdinalIgnoreCase);
        if (request.From is { } desde) consulta = porContable ? consulta.Where(t => t.AccountingDate >= desde) : consulta.Where(t => t.ValueDate >= desde);
        if (request.To is { } hasta) consulta = porContable ? consulta.Where(t => t.AccountingDate <= hasta) : consulta.Where(t => t.ValueDate <= hasta);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var ordenada = string.Equals(request.DateKind, "registered", StringComparison.OrdinalIgnoreCase)
            ? consulta.OrderByDescending(t => t.RegisteredAt).ThenByDescending(t => t.Id)
            : porContable ? consulta.OrderByDescending(t => t.AccountingDate).ThenByDescending(t => t.Id) : consulta.OrderByDescending(t => t.ValueDate).ThenByDescending(t => t.Id);
        var filas = await ordenada.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var idsDeOperacion = filas.Where(f => f.OperationId is not null).Select(f => f.OperationId!.Value).Distinct().ToList();
        var operaciones = await db.MemberAccountOperations.AsNoTracking().Where(o => idsDeOperacion.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.PublicId, ct);
        await auditoria.EmitirMovimientosDeCuentaVistosAsync(cuenta.PublicId, new { request.From, request.To, request.Concept, request.DateKind }, filas.Count, ct);
        var items = filas.Select(f => OperacionesDeCuenta.Fila(f) with { OperationPublicId = f.OperationId is { } o ? operaciones.GetValueOrDefault(o) : null }).ToList();
        return Result.Success(new PagedResult<MemberAccountTransactionDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Arma los DTO de las operaciones y del libro. (nuevo)</summary>
public static class OperacionesDeCuenta
{
    public static async Task<IReadOnlyList<MemberAccountOperationDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<MemberAccountOperation> filas, CancellationToken ct)
    {
        var ids = filas.Select(o => o.ReversedByOperationId).Concat(filas.Select(o => o.ReversesOperationId)).OfType<long>().Distinct().ToList();
        var publicos = await db.MemberAccountOperations.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.PublicId, ct);
        return filas.Select(o => new MemberAccountOperationDto(o.PublicId, o.Number, o.Kind, o.ValueDate, o.AccountingDate, o.RegisteredAt, o.Amount,
            o.CollectorCode is { } codigo ? new MemberAccountCollectorDto(codigo, o.CollectorClass ?? string.Empty) : null, o.PaymentMeansClass,
            o.ReceiptNumber, o.ReversedByOperationId is { } r ? publicos.GetValueOrDefault(r) : null,
            o.ReversesOperationId is { } v ? publicos.GetValueOrDefault(v) : null, o.ActorKind == ActorKind.Process ? "Process" : "Person", o.Reason,
            o.ClearingUntil, o.ClearedAt is not null)).ToList();
    }

    public static MemberAccountTransactionDto Fila(MemberAccountTransaction t) => new(t.Id, t.Kind, t.Concept, t.Amount, t.ValueDate, t.AccountingDate,
        t.RegisteredAt, null, t.ReversesTransactionId, RegistroDeCuentas.EsMovimientoDelTitular(t.Kind), t.MessagePublicId);
}

// --------------------------------------------------------------------------------------------------------------- simular --

/// <summary>
/// La simulación de la pantalla (api.md §4, <c>POST /{id}/operations/simulate</c>, <c>Lending.Accounts.Read</c>, sin clave): retiro o traslado con las
/// reglas del contrato —firmas, disponible con el GMF, topes, atribución, cargos— sin escribir. (nuevo)
/// </summary>
public sealed record SimulateMemberAccountOperationQuery(
    Guid AccountPublicId,
    string Kind,
    decimal Amount,
    Guid? TargetAccountPublicId,
    ClaseDeMedioDeRecaudo? MeansClass,
    IReadOnlyList<Guid> Signatures) : IRequest<Result<MemberAccountSimulationDto>>;

public sealed class SimulateMemberAccountOperationQueryValidator : AbstractValidator<SimulateMemberAccountOperationQuery>
{
    public SimulateMemberAccountOperationQueryValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Kind).Must(k => k is "Withdrawal" or "Transfer").WithMessage("La simulación es de un retiro (Withdrawal) o un traslado (Transfer).");
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.TargetAccountPublicId).NotEmpty().When(x => x.Kind == "Transfer").WithMessage("Diga la cuenta destino del traslado.");
    }
}

public sealed class SimulateMemberAccountOperationQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    TrasladoDeCuenta traslados,
    IDateTimeService reloj) : IRequestHandler<SimulateMemberAccountOperationQuery, Result<MemberAccountSimulationDto>>
{
    public async Task<Result<MemberAccountSimulationDto>> Handle(SimulateMemberAccountOperationQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountSimulationDto>(leida.Error);
        var cuenta = leida.Value;
        var hoy = reloj.HoyLocal;
        RetiroRevisado? revisado = null;
        Error? rechazo;
        if (request.Kind == "Transfer")
        {
            // US16 (T162): el destino también en el alcance (fuera, el mismo 404 que si no existiera).
            var otra = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.TargetAccountPublicId ?? Guid.Empty, seguir: false, ct);
            if (otra.IsFailure) return Result.Failure<MemberAccountSimulationDto>(otra.Error);
            var destino = otra.Value;
            var r = await traslados.RevisarAsync(cuenta, destino, Pantalla.Traslado(cuenta.PublicId, destino.PublicId, request.Amount, hoy, request.Signatures, null, null),
                pedirAprobacion: false, ct);
            revisado = r.IsSuccess ? r.Value.Origen : null;
            rechazo = r.IsFailure ? r.Error : null;
        }
        else
        {
            var r = await retiros.RevisarAsync(cuenta, Pantalla.Retiro(cuenta.PublicId, request.Amount, hoy, request.MeansClass ?? ClaseDeMedioDeRecaudo.Transfer,
                request.Signatures, null, null, null), pedirAprobacion: false, ct);
            revisado = r.IsSuccess ? r.Value : null;
            rechazo = r.IsFailure ? r.Error : null;
        }

        var c = revisado?.Cuenta ?? await movimientos.LeerAsync(cuenta, hoy, ct);
        var (firmas, personas) = revisado is { Firmas: { } f } ? (f, revisado.Personas) : await movimientos.FirmasAsync(c, request.Signatures, request.Amount, hoy, ct);
        var atribucion = await movimientos.AtribucionRequeridaAsync(ReglasDeMovimientos.PermisoDeRetiro, request.Amount, hoy, ct);
        var gmf = revisado?.Gmf;
        var avisos = new List<string>();
        if (rechazo is not null) avisos.Add(rechazo.Message);
        if (atribucion is not null) avisos.Add($"La operación supera su atribución ({atribucion.MaxAmount:N2}): necesitará un aprobador presente con segundo factor.");
        return Result.Success(new MemberAccountSimulationDto(rechazo is null, ReglasDeMovimientos.Dto(c.Disponible),
            ReglasDeMovimientos.Dto(revisado?.Despues ?? c.Disponible),
            gmf is null ? null : new MemberAccountSimulationGmfDto(gmf.Base, gmf.ExentoUsadoAntes, Math.Max(0m, gmf.Tope - gmf.ExentoUsadoDespues), gmf.Valor,
                gmf.Explicacion),
            (revisado?.Cargos ?? []).Where(x => x.Calculo.SeCausa).Select(x => new CargoCalculadoDto(x.Definicion.Code, x.Calculo.Valor, x.Definicion.IsVatTaxed,
                x.Calculo.Resultado == ResultadoDeCargo.Cobrado ? nameof(AccountChargeStatus.Collected) : nameof(AccountChargeStatus.Pending))).ToList(),
            new MemberAccountSignaturesDto(firmas.Requeridas.Select(p => personas.GetValueOrDefault(p)).ToList(), firmas.Faltantes.Select(p => personas.GetValueOrDefault(p)).ToList()),
            new MemberAccountAttributionDto(atribucion is not null, atribucion?.PermissionCode, atribucion?.MaxAmount), avisos));
    }
}

/// <summary>Los pedidos de la pantalla (sin recaudador ni clave del contrato; la idempotencia la da la clave de la ruta). (nuevo)</summary>
internal static class Pantalla
{
    public static RetiroPedido Retiro(Guid cuenta, decimal monto, DateOnly fecha, ClaseDeMedioDeRecaudo medio, IReadOnlyList<Guid> firmas, Guid? aprobacion, string? motivo,
        OrdenDeRetiro? orden) =>
        new(null, null, null, cuenta, monto, fecha, medio, firmas, null, null, aprobacion, PrevalidationOutcome.NotApplicable, [], motivo) { Orden = orden };

    public static TrasladoPedido Traslado(Guid origen, Guid destino, decimal monto, DateOnly fecha, IReadOnlyList<Guid> firmas, Guid? aprobacion, string? motivo,
        ModalidadDeConsignacion modalidad = ModalidadDeConsignacion.Free) =>
        new(null, null, null, origen, destino, monto, fecha, firmas, modalidad, null, aprobacion, PrevalidationOutcome.NotApplicable, [], motivo);
}

// -------------------------------------------------------------------------------------------------------- retiro por giro --

/// <summary>
/// El retiro por orden de giro desde la pantalla (api.md §4, <c>POST /{id}/operations/withdrawals</c>, <c>Lending.Accounts.Withdraw</c>, con clave): las
/// reglas del retiro del contrato, la validación previa contable que <b>bloquea</b> en pantalla (D23) y la orden de giro v2 a Tesorería con el beneficiario
/// (el titular u otra persona; la cuenta bancaria pedida o la registrada del asociado). Sobre la atribución responde 202 <c>PendingApproval</c>. (nuevo)
/// </summary>
public sealed record WithdrawToDisbursementOrderCommand(
    Guid AccountPublicId,
    decimal Amount,
    DateOnly? ValueDate,
    WithdrawalPayeeInput? Payee,
    IReadOnlyList<Guid> Signatures,
    string? Reason,
    Guid? ApprovalPublicId = null) : IRequest<Result<MemberAccountOperationResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class WithdrawToDisbursementOrderCommandValidator : AbstractValidator<WithdrawToDisbursementOrderCommand>
{
    public WithdrawToDisbursementOrderCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).Must(m => RegistroDeCuentas.Decimales(m) <= MemberAccountTransaction.DecimalesDeOperacion)
            .WithMessage("El valor lleva a lo sumo dos decimales.");
        RuleFor(x => x.Signatures).NotNull();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class WithdrawToDisbursementOrderCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    RetiroDeCuenta retiros,
    OrdenesDeGiroDeCuentas ordenes,
    ValidacionPreviaDeCaptaciones validacionPrevia,
    EmisionDeCartera emision,
    IDateTimeService reloj) : IRequestHandler<WithdrawToDisbursementOrderCommand, Result<MemberAccountOperationResultDto>>
{
    public async Task<Result<MemberAccountOperationResultDto>> Handle(WithdrawToDisbursementOrderCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var fecha = request.ValueDate ?? hoy;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.Nature == LineNature.Contribution) return Falla(ErroresDeAportes.NoRetirable());
        if (!(await Activation.ActivacionesDeCaptacion.LeerAsync(db, ct)).Activada(cuenta.Nature)) return Falla(ErroresDeCuentas.SinActivar(cuenta.Nature));
        if (fecha > hoy) return Falla(ErroresDeAportes.FechaValor(fecha, hoy, hoy));

        var personaId = cuenta.HolderPersonId;
        if (request.Payee?.PersonPublicId is { } beneficiario && beneficiario != Guid.Empty)
            personaId = await db.People.AsNoTracking().Where(p => p.PublicId == beneficiario).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
                        ?? personaId;
        var banco = await ordenes.BeneficiarioAsync(personaId, request.Payee?.BankPublicId, request.Payee?.AccountType, request.Payee?.AccountNumber, ct);
        if (banco.IsFailure) return Falla(banco.Error);

        // D23 (US8, T145): la pantalla bloquea si el retiro por giro y su GMF no se contabilizarían, con las dimensiones vigentes de la cuenta.
        var sucursal = await emision.SucursalContableAsync(cuenta.BranchId, ct);
        if (sucursal.IsFailure) return Falla(sucursal.Error);
        var d = (await validacionPrevia.DimensionesAsync([cuenta.Id], fecha, ct))[cuenta.Id];
        var decision = await validacionPrevia.EnPantallaAsync(HechosRepresentativosDeCaptaciones.Unidad(fecha, d,
            HechosRepresentativosDeCaptaciones.Retiro(d, null), HechosRepresentativosDeCaptaciones.Gmf(d)), $"el retiro de la cuenta {cuenta.Number}", ct);
        if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cuenta.HolderPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            var r = await retiros.AplicarAsync(Pantalla.Retiro(cuenta.PublicId, request.Amount, fecha, ClaseDeMedioDeRecaudo.Transfer, request.Signatures,
                request.ApprovalPublicId, request.Reason, new OrdenDeRetiro(personaId, banco.Value)) with
            {
                Validacion = decision.Outcome, Avisos = decision.Avisos,
            }, ct);
            if (r.IsFailure)
            {
                if (r.Error.Code != ErroresDeMovimientos.CodigoPendienteDeAprobacion) return Falla(r.Error);
                await db.SaveChangesAsync(ct);
                return Result.Success(Pendiente(r.Error));
            }
            var orden = r.Value.Orden;
            return Result.Success(new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, r.Value.Comprobante,
                orden is null ? null : new MemberAccountOrderDto(orden.PublicId, orden.Number, orden.Status.ToString()), null, null));
        }, ct);
    }

    /// <summary>El recaudador con que la pantalla se presenta a la validación previa (la Cartera misma).</summary>
    private static Recaudador RecogidaDeLaPantalla => Disbursements.RecogidaEnElDesembolso.Recaudador;

    internal static MemberAccountOperationResultDto Pendiente(Error e)
    {
        Guid? aprobacion = null;
        AtribucionRequeridaDto? requerida = null;
        if (e is ErrorConDatos d && System.Text.Json.JsonSerializer.SerializeToElement(d.Data) is var json && json.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (json.TryGetProperty("approvalPublicId", out var a) && a.TryGetGuid(out var g)) aprobacion = g;
            if (json.TryGetProperty("required", out var r))
                requerida = new AtribucionRequeridaDto(r.GetProperty("permissionCode").GetString() ?? string.Empty, r.GetProperty("amount").GetDecimal(),
                    r.GetProperty("maxAmount").GetDecimal());
        }
        return new MemberAccountOperationResultDto(MemberAccountOperationResultDto.PendienteDeAprobacion, null, null, aprobacion, requerida);
    }

    private static Result<MemberAccountOperationResultDto> Falla(Error e) => Result.Failure<MemberAccountOperationResultDto>(e);
}

// --------------------------------------------------------------------------------------------------------------- traslado --

/// <summary>
/// El traslado entre cuentas desde la pantalla (api.md §4, <c>POST /{id}/operations/transfers</c>, <c>Lending.Accounts.Transfer</c>, con clave): una sola
/// operación con las dos cuentas en orden de <c>Id</c> (R08), el GMF por su hecho generador, la validación previa que bloquea en pantalla; sobre la
/// atribución, 202 <c>PendingApproval</c>. La cuenta destino puede ser de otro titular o de aportes (por cuotas). (nuevo)
/// </summary>
public sealed record TransferBetweenMemberAccountsCommand(
    Guid AccountPublicId,
    Guid TargetAccountPublicId,
    decimal Amount,
    DateOnly? ValueDate,
    IReadOnlyList<Guid> Signatures,
    string? Reason,
    ModalidadDeConsignacion TargetModality = ModalidadDeConsignacion.Installments,
    Guid? ApprovalPublicId = null) : IRequest<Result<MemberAccountOperationResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class TransferBetweenMemberAccountsCommandValidator : AbstractValidator<TransferBetweenMemberAccountsCommand>
{
    public TransferBetweenMemberAccountsCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.TargetAccountPublicId).NotEmpty().NotEqual(x => x.AccountPublicId).WithMessage("El traslado es entre dos cuentas distintas.");
        RuleFor(x => x.Amount).GreaterThan(0m).Must(m => RegistroDeCuentas.Decimales(m) <= MemberAccountTransaction.DecimalesDeOperacion)
            .WithMessage("El valor lleva a lo sumo dos decimales.");
        RuleFor(x => x.Signatures).NotNull();
        RuleFor(x => x.TargetModality).IsInEnum().NotEqual(ModalidadDeConsignacion.TermDepositFunding);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class TransferBetweenMemberAccountsCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    TrasladoDeCuenta traslados,
    ValidacionPreviaDeCaptaciones validacionPrevia,
    EmisionDeCartera emision,
    IDateTimeService reloj) : IRequestHandler<TransferBetweenMemberAccountsCommand, Result<MemberAccountOperationResultDto>>
{
    public async Task<Result<MemberAccountOperationResultDto>> Handle(TransferBetweenMemberAccountsCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var fecha = request.ValueDate ?? hoy;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var origen = leida.Value;
        // US16 (T162): el traslado escribe en las dos cuentas: el destino también tiene que estar en el alcance (fuera, el mismo 404 que si no existiera).
        var otra = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.TargetAccountPublicId, seguir: false, ct);
        if (otra.IsFailure) return Falla(otra.Error);
        var destino = otra.Value;
        if (origen.Nature == LineNature.Contribution) return Falla(ErroresDeAportes.NoRetirable());
        var fechas = await Activation.ActivacionesDeCaptacion.LeerAsync(db, ct);
        foreach (var n in new[] { origen.Nature, destino.Nature }.Distinct())
            if (!fechas.Activada(n)) return Falla(ErroresDeCuentas.SinActivar(n));
        if (fecha > hoy) return Falla(ErroresDeAportes.FechaValor(fecha, hoy, hoy));

        // D23 (US8, T145): el traslado con las dimensiones de las dos cuentas (la de aportes, con su clase) y el GMF de la que se debita.
        foreach (var oficina in new[] { origen.BranchId, destino.BranchId }.Distinct())
            if (await emision.SucursalContableAsync(oficina, ct) is { IsFailure: true } sinSucursal) return Falla(sinSucursal.Error);
        var dimensiones = await validacionPrevia.DimensionesAsync([origen.Id, destino.Id], fecha, ct);
        var (dOrigen, dDestino) = (dimensiones[origen.Id], dimensiones[destino.Id]);
        var decision = await validacionPrevia.EnPantallaAsync(HechosRepresentativosDeCaptaciones.Unidad(fecha, dOrigen,
            HechosRepresentativosDeCaptaciones.Traslado(dOrigen, dDestino), HechosRepresentativosDeCaptaciones.Gmf(dOrigen)),
            $"el traslado desde la cuenta {origen.Number}", ct);
        if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(new[] { origen.HolderPersonId, destino.HolderPersonId }.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(new[] { origen.Id, destino.Id }.Order().ToList(), ModoDeEspera.Esperar, ct);
            var r = await traslados.AplicarAsync(Pantalla.Traslado(origen.PublicId, destino.PublicId, request.Amount, fecha, request.Signatures, request.ApprovalPublicId,
                request.Reason, request.TargetModality) with { Validacion = decision.Outcome, Avisos = decision.Avisos }, ct);
            if (r.IsFailure)
            {
                if (r.Error.Code != ErroresDeMovimientos.CodigoPendienteDeAprobacion) return Falla(r.Error);
                await db.SaveChangesAsync(ct);
                return Result.Success(WithdrawToDisbursementOrderCommandHandler.Pendiente(r.Error));
            }
            return Result.Success(new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, r.Value.Comprobante, null, null, null));
        }, ct);
    }

    private static Result<MemberAccountOperationResultDto> Falla(Error e) => Result.Failure<MemberAccountOperationResultDto>(e);
}

// --------------------------------------------------------------------------------------------------------------- reversar --

/// <summary>
/// El reverso desde la Cartera (api.md §4, <c>POST /{id}/operations/{operationId}/reverse</c>, <c>Lending.Accounts.Reverse</c>, con clave; FR-092, US4-6):
/// arrastra a sus dependientes (GMF, cargos); una consignación de un recaudador se reversa y se le avisa con <c>ConsignacionReversadaPorCartera</c> para que
/// anule su recibo; lo que hicieron Nómina o un recaudador por su cuenta (retiros, traslados) lo reversa él (<c>Lending.Savings.ReversalOwnedByCollector</c>);
/// lo de un proceso de la Cartera (cruce del retiro, débito automático, devoluciones) se reversa desde su origen. Desde la pantalla <b>nunca</b> queda un
/// sobregiro: si la consignación ya se usó, <c>Lending.Savings.ReversalWouldOverdraw</c> con la operación que lo impide. (nuevo)
/// </summary>
public sealed record ReverseMemberAccountOperationCommand(Guid AccountPublicId, Guid OperationPublicId, string Reason)
    : IRequest<Result<ComprobanteDeReversoDeCuentaDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReverseMemberAccountOperationCommandValidator : ValidadorConMotivo<ReverseMemberAccountOperationCommand>
{
    public ReverseMemberAccountOperationCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.OperationPublicId).NotEmpty();
    }
}

public sealed class ReverseMemberAccountOperationCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReversionDeCuentas reversion,
    EmisionDeCartera emision,
    IActorActual actorActual) : IRequestHandler<ReverseMemberAccountOperationCommand, Result<ComprobanteDeReversoDeCuentaDto>>
{
    /// <summary>Quién inicia el reverso desde la pantalla.</summary>
    public const string IniciadoPorLaCartera = EmisionDeCartera.ReversoIniciadoPorLaCartera;

    public async Task<Result<ComprobanteDeReversoDeCuentaDto>> Handle(ReverseMemberAccountOperationCommand request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        var vista = await db.MemberAccountOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.PublicId == request.OperationPublicId && o.MemberAccountId == cuenta.Id && !o.IsDeleted, ct);
        if (vista is null) return Falla(ErroresDeAportes.OperacionInexistente());
        if (vista.Kind == MemberAccountOperationKind.Reversal || vista.ReversesOperationId is not null)
            return Falla(ErroresDelLibroDeCuentas.ReversoDeReverso(vista.Id));
        if (vista.CollectorClass == nameof(ClaseDeRecaudador.Payroll)
            || (vista.CollectorCode is not null && vista.Kind != MemberAccountOperationKind.Deposit && vista.CollectorCode != Disbursements.RecogidaEnElDesembolso.Recaudador.Code))
            return Falla(ErroresDeAportes.ReversoDeOtro());
        // US12 (T203): lo de la asamblea se reversa entero, desde la decisión (POST /assembly-distributions/{id}/reverse).
        if (vista.MembershipWithdrawalId is not null || vista.AssemblyDistributionId is not null || vista.Kind is MemberAccountOperationKind.AssemblyCredit or MemberAccountOperationKind.DirectDebit or MemberAccountOperationKind.InternalOffset
                or MemberAccountOperationKind.ContributionRefund or MemberAccountOperationKind.Charge or MemberAccountOperationKind.ChargeWaiver or MemberAccountOperationKind.ChargeCollection
                // US5 (T132, T135; research E98): la liquidación de intereses y la cancelación no se reversan desde la pantalla.
                or MemberAccountOperationKind.InterestSettlement or MemberAccountOperationKind.Cancellation
                // US11 (T189): la consignación de un embargo (la reversa Tesorería) y la reclasificación (sin filas) tampoco.
                or MemberAccountOperationKind.GarnishmentConsignment or MemberAccountOperationKind.Reclassification
            || await db.AccountPayoutOrders.AsNoTracking().AnyAsync(o => o.OperationId == vista.Id, ct))
            return Falla(ErroresDeMovimientos.DelProceso(vista.Number));

        var personas = new List<int> { cuenta.HolderPersonId };
        var cuentas = new List<int> { cuenta.Id };
        if (vista.CounterpartMemberAccountId is { } contraparte)
        {
            cuentas.Add(contraparte);
            personas.Add(await db.MemberAccounts.AsNoTracking().Where(a => a.Id == contraparte).Select(a => a.HolderPersonId).FirstAsync(ct));
        }
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(personas.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(cuentas.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            var original = await db.MemberAccountOperations.FirstAsync(o => o.Id == vista.Id, ct);

            // US4-6 (FR-056): desde la pantalla nunca queda un sobregiro: lo que el reverso quita no puede pasar del saldo de la cuenta.
            if (original.Kind is MemberAccountOperationKind.Deposit)
            {
                var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
                var entra = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == original.Id && t.MemberAccountId == cuenta.Id)
                    .ToListAsync(ct);
                var despues = LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos)
                              - LectorDeCuentas.SaldoDelTitular(cuenta.Nature, entra.GroupBy(t => t.Concept).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount)));
                if (despues < 0m)
                {
                    var bloquea = await db.MemberAccountOperations.AsNoTracking()
                        .Where(o => o.MemberAccountId == cuenta.Id && o.Id > original.Id && o.ReversedByOperationId == null && o.ReversesOperationId == null
                                    && (o.Kind == MemberAccountOperationKind.Withdrawal || o.Kind == MemberAccountOperationKind.Transfer
                                        || o.Kind == MemberAccountOperationKind.DirectDebit || o.Kind == MemberAccountOperationKind.Charge))
                        .OrderByDescending(o => o.Id).Select(o => new { o.PublicId, o.Number }).FirstOrDefaultAsync(ct);
                    return Falla(ErroresDeMovimientos.ReversoSobregira(bloquea?.PublicId ?? Guid.Empty, bloquea?.Number ?? string.Empty));
                }
            }

            var r = await reversion.ReversarAsync(original, request.Reason, IniciadoPorLaCartera, ct);
            if (r.IsFailure) return Falla(r.Error);
            // FR-092: la consignación de un recaudador reversada desde la Cartera se le avisa para que anule su recibo.
            if (!r.Value.YaReversada && r.Value.Reverso is { } reverso && original.CollectorCode is { } codigo && original.Kind == MemberAccountOperationKind.Deposit
                && original.IdempotencyKey is { } clave && Enum.TryParse<ClaseDeRecaudador>(original.CollectorClass, out var clase))
            {
                var hecho = await emision.HechoDeCuentaAsync(cuenta, reverso.AccountingDate, ct);
                if (hecho.IsFailure) return Falla(hecho.Error);
                var deposito = System.Text.Json.JsonSerializer.Deserialize<ConsignacionAAplicar>(original.Payload,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                var actor = await actorActual.ObtenerAsync(ct);
                var recaudador = EmisionDeCartera.Recaudador(new Recaudador(codigo, clase), clave,
                    Enum.TryParse<ClaseDeMedioDeRecaudo>(original.PaymentMeansClass, out var medio) ? medio : ClaseDeMedioDeRecaudo.Cash,
                    deposito?.DepositorPublicId ?? hecho.Value.TitularPublicId,
                    deposito?.Receipt ?? new ReferenciaDeRecibo(original.ReceiptNumber ?? original.Number, original.ReceiptDate ?? original.ValueDate,
                        original.ReceiptBranchPublicId, original.ReceiptMeansReference, "Recibo"));
                await emision.ConsignacionReversadaPorCarteraAsync(hecho.Value, reverso, original, recaudador, deposito?.DepositorPublicId ?? hecho.Value.TitularPublicId,
                    request.Reason.Trim(), new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name }, ct);
                await db.SaveChangesAsync(ct);
            }
            return Result.Success(r.Value.Comprobante);
        }, ct);
    }

    private static Result<ComprobanteDeReversoDeCuentaDto> Falla(Error e) => Result.Failure<ComprobanteDeReversoDeCuentaDto>(e);
}

// --------------------------------------------------------------------------------------------------------- uso del GMF --

/// <summary>
/// El uso del tope exento del GMF de una persona en un mes (api.md §10, <c>GET /api/lending/gmf-exemptions/usage</c>, <c>Lending.Accounts.Read</c>;
/// research E48): la política, el tope en UVT y en pesos, lo exento usado, lo que queda, lo gravado y el GMF cobrado, con sus disposiciones. (nuevo)
/// </summary>
public sealed record GetGmfMonthlyUsageQuery(Guid PersonPublicId, int? Year = null, int? Month = null) : IRequest<Result<GmfMonthlyUsageDto>>;

public sealed class GetGmfMonthlyUsageQueryHandler(IApplicationDbContext db, ReglasDeMovimientos movimientos, IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj, LendingAuditEmitter auditoria) : IRequestHandler<GetGmfMonthlyUsageQuery, Result<GmfMonthlyUsageDto>>
{
    public async Task<Result<GmfMonthlyUsageDto>> Handle(GetGmfMonthlyUsageQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var anio = request.Year ?? hoy.Year;
        var mes = request.Month ?? hoy.Month;
        if (mes is < 1 or > 12) return Result.Failure<GmfMonthlyUsageDto>(new ErrorConDatos("Validation.Invalid", "El mes va de 1 a 12.", new { month = mes }));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<GmfMonthlyUsageDto>(new Error("Person.NotFound", "La persona no existe."));
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId).Select(a => a.Id);
        var fin = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes));
        var politica = await movimientos.PoliticaDeGmfAsync(fin < hoy ? fin : hoy, ct);
        var filas = await (from u in db.GmfMonthlyUsages.AsNoTracking()
                           join o in db.MemberAccountOperations.AsNoTracking() on u.OperationId equals o.Id
                           join a in db.MemberAccounts.AsNoTracking() on u.MemberAccountId equals a.Id
                           where u.PersonId == persona && u.Year == anio && u.Month == mes && !u.IsDeleted && cuentas.Contains(u.MemberAccountId)
                           orderby u.Id
                           select new GmfUsageLineDto(o.PublicId, o.Number, a.Number, u.TaxableEventCode, u.DisposedAmount, u.ExemptAmount, u.TaxableBase, u.GmfAmount,
                               u.ReversesUsageId != null)).ToListAsync(ct);
        // US16 (T164): las disposiciones de una persona en el mes son sus movimientos: lectura sensible.
        await auditoria.EmitirMovimientosDePersonaVistosAsync(request.PersonPublicId, new { gmfUsage = true, year = anio, month = mes }, filas.Count, ct);
        var usado = Math.Max(0m, filas.Sum(f => f.Exempt));
        var tope = politica.IsSuccess ? politica.Value.TopeEnPesos : 0m;
        return Result.Success(new GmfMonthlyUsageDto(politica.IsSuccess ? politica.Value.Politica : GmfExemptionPolicy.SingleMarkedAccount,
            politica.IsSuccess ? politica.Value.TopeExencionUvt : 0m, tope, usado, Math.Max(0m, tope - usado), filas.Sum(f => f.TaxedBase), filas.Sum(f => f.Gmf), filas));
    }
}
