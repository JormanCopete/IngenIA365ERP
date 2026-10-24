using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Imputacion;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

// Feature 015, US3 (T0113, T0114; R-07; FR-037 a FR-041, FR-188 a FR-190; contracts/api.md §4; interfaces.md §1): lo que una persona puede pagar, y la
// operación de caja en preparación —pagador, ítems simulados con su módulo, medios e imputación—. Sólo la confirmación (ConfirmacionDeRecibo) mueve dinero.
// Recaudo conoce a Cartera y Captaciones sólo por IContratoDeObligaciones e IContratoDeCuentas, como Recaudador("REC", Receipts). (nuevo)

/// <summary>
/// <c>GET /api/receipts/payables?person=&amp;document=&amp;includeCoDebtor=</c> (<c>Receipts.Receipt.Register</c>, lectura sensible auditada): las obligaciones
/// por <c>ListarAsync</c>, las cuentas por <c>ConsultarAsync</c> y lo exigible por <c>ListarExigiblesAsync</c>, y los conceptos varios de ingreso. Busca la
/// persona por su <c>PublicId</c>, su documento o su código (el lector). (nuevo)
/// </summary>
public sealed record GetPayablesQuery(Guid? PersonPublicId = null, string? Document = null, bool IncludeCoDebtor = false, string? ObligationNumber = null)
    : IRequest<Result<LoQuePuedePagarDto>>;

public sealed class GetPayablesQueryHandler(
    IApplicationDbContext db,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IBusquedaDeObligaciones busqueda,
    IDateTimeService reloj,
    AuditoriaDeRecaudo auditoria) : IRequestHandler<GetPayablesQuery, Result<LoQuePuedePagarDto>>
{
    public async Task<Result<LoQuePuedePagarDto>> Handle(GetPayablesQuery request, CancellationToken ct)
    {
        var documento = request.Document?.Trim();
        var porNumero = request.PersonPublicId is null && string.IsNullOrEmpty(documento) && !string.IsNullOrWhiteSpace(request.ObligationNumber)
            ? await PorNumeroAsync(request.ObligationNumber!, ct)
            : null;
        if (porNumero is { IsFailure: true }) return Result.Failure<LoQuePuedePagarDto>(porNumero.Error);
        var buscada = request.PersonPublicId ?? porNumero?.Value;
        var persona = buscada is { } p
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == p, ct)
            : string.IsNullOrEmpty(documento) ? null
            : await db.People.AsNoTracking().FirstOrDefaultAsync(x => x.TaxId == documento || x.LegacyCode == documento, ct);
        if (persona is null) return Result.Failure<LoQuePuedePagarDto>(ErroresDeRecaudo.PersonaNoEncontrada());
        var hoy = reloj.HoyLocal;
        var avisos = new List<string>();

        var deObligaciones = await obligaciones.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, persona.PublicId, hoy, request.IncludeCoDebtor), ct);
        if (deObligaciones.Result == ResultadoDelContrato.Rejected) avisos.Add(deObligaciones.Message);
        var deCuentas = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(ClavesDeRecaudo.Recaudador, persona.PublicId, hoy), ct);
        if (deCuentas.Result == ResultadoDelContrato.Rejected) avisos.Add(deCuentas.Message);
        var exigibles = await cuentas.ListarExigiblesAsync(new SolicitudDeExigibles(ClavesDeRecaudo.Recaudador, [persona.PublicId], hoy), ct);
        if (exigibles.Result == ResultadoDelContrato.Rejected) avisos.Add(exigibles.Message);

        var conceptos = await db.MiscConcepts.AsNoTracking()
            .Where(c => c.Status == ReceiptsCatalogStatus.Active && c.Direction == MiscConceptDirection.Income && !c.IsSystem)
            .OrderBy(c => c.Code)
            .Select(c => new ConceptoVarioPagableDto(c.PublicId, c.Code, c.Name, c.RequiresThirdParty, c.RequiresCrossDocument, c.RequiresInvoice, c.MaxAmount))
            .ToListAsync(ct);

        await auditoria.LoQuePuedePagarAsync(persona.PublicId, deObligaciones.Value?.Obligations.Count ?? 0, deCuentas.Value?.Accounts.Count ?? 0, ct);
        return Result.Success(new LoQuePuedePagarDto(persona.PublicId, OperacionesDeCaja.Nombre(persona), persona.TaxId, persona.IsAssociate,
            deObligaciones.Value, deCuentas.Value, exigibles.Value, conceptos, avisos));
    }

    /// <summary>
    /// La persona dueña del crédito o de la cuenta con ese número, por el puerto de lectura de Cartera y Captaciones (decisión del dueño del 2026-10-24): Recaudo
    /// no lee <c>LND_</c>. Sin coincidencia, <c>Receipts.Payables.PersonNotFound</c>; con dos personas distintas, <c>Receipts.Payables.AmbiguousNumber</c>.
    /// </summary>
    private async Task<Result<Guid>> PorNumeroAsync(string numero, CancellationToken ct)
    {
        var encontradas = await busqueda.PorNumeroAsync(new BusquedaPorNumero(numero), ct);
        var personas = encontradas.Select(e => e.PersonPublicId).Distinct().ToList();
        return personas.Count switch
        {
            0 => Result.Failure<Guid>(ErroresDeRecaudo.PersonaNoEncontrada()),
            1 => Result.Success(personas[0]),
            _ => Result.Failure<Guid>(new ErrorConDatos("Receipts.Payables.AmbiguousNumber",
                $"El número {numero.Trim()} es de un crédito y de una cuenta de personas distintas: busque por el documento.", new { number = numero.Trim() })),
        };
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-operations</c> (<c>Receipts.Receipt.Register</c>): nace la operación <c>Preparing</c> con su <c>PublicId</c> estable (el del
/// recibo) en la sesión del cajero, con el pagador, el titular cuando paga un tercero y quien efectúa (FR-191). (nuevo)
/// </summary>
public sealed record CreateCashOperationCommand(
    Guid SessionPublicId,
    Guid PayerPersonPublicId,
    Guid? BeneficiaryPersonPublicId = null,
    Guid? DeliveredByPersonPublicId = null,
    DateOnly? ValueDate = null,
    string? BackdateReason = null,
    Guid? BackdateSupportAttachmentPublicId = null) : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCashOperationCommandValidator : AbstractValidator<CreateCashOperationCommand>
{
    public CreateCashOperationCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.PayerPersonPublicId).NotEmpty().WithMessage("Indique quién paga.");
        RuleFor(x => x.BackdateReason).MaximumLength(500);
    }
}

public sealed class CreateCashOperationCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj, IListasDeRecaudo? listas = null)
    : IRequestHandler<CreateCashOperationCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(CreateCashOperationCommand request, CancellationToken ct)
    {
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.SesionRequerida());
        var operable = SesionesDeCaja.Operable(sesion);
        if (operable.IsFailure) return Result.Failure<CashOperationDto>(operable.Error);

        async Task<int?> PersonaAsync(Guid? id) => id is { } g
            ? await db.People.AsNoTracking().Where(p => p.PublicId == g).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            : null;
        var pagador = await PersonaAsync(request.PayerPersonPublicId);
        if (pagador is null) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.PersonaNoEncontrada());
        var titular = await PersonaAsync(request.BeneficiaryPersonPublicId);
        if (request.BeneficiaryPersonPublicId is not null && titular is null) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.PersonaNoEncontrada());
        var efectua = await PersonaAsync(request.DeliveredByPersonPublicId);
        if (request.DeliveredByPersonPublicId is not null && efectua is null) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.PersonaNoEncontrada());

        var operacion = new CashOperation
        {
            CashSessionId = sesion.Id,
            PayerPersonId = pagador.Value,
            BeneficiaryPersonId = titular == pagador ? null : titular,
            DeliveredByPersonId = efectua,
            ValueDate = request.ValueDate ?? reloj.HoyLocal,
            BackdateReason = request.BackdateReason?.Trim(),
            BackdateSupportAttachmentPublicId = request.BackdateSupportAttachmentPublicId,
        };
        db.CashOperations.Add(operacion);
        await db.SaveChangesAsync(ct);
        // Feature 015, US14 (T0240; FR-098, FR-203): pagador, titular y quien efectúa contra las listas desde que nace la operación. La vinculante la bloquea sin medios
        // ni confirmación (la alerta al oficial es inmediata); la no vinculante queda anotada para la confirmación.
        if (listas is not null)
        {
            var personas = new[] { pagador.Value, titular ?? 0, efectua ?? 0 }.Where(x => x != 0).Distinct().OrderBy(x => x).ToList();
            operacion.ScreeningOutcome = await listas.EvaluarAsync(personas, operacion.PublicId, ct);
            if (operacion.ScreeningOutcome == Domain.Enums.Receipts.ScreeningOutcomeR.BindingMatch) operacion.Status = CashOperationState.Blocked;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}

/// <summary><c>GET /api/receipts/cash-operations/{id}</c> (<c>Receipts.Receipt.Register</c>). (nuevo)</summary>
public sealed record GetCashOperationQuery(Guid PublicId) : IRequest<Result<CashOperationDto>>;

public sealed class GetCashOperationQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetCashOperationQuery, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(GetCashOperationQuery request, CancellationToken ct) =>
        await OperacionesDeCaja.VisibleAsync(db, alcance, request.PublicId, seguir: false, ct) is { } v
            ? Result.Success(await OperacionesDeCaja.DtoAsync(db, v.Operacion, ct))
            : Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
}

/// <summary>
/// <c>PUT /api/receipts/cash-operations/{id}/items</c> (<c>Receipts.Receipt.Register</c>): las aplicaciones elegidas, cada una simulada con su módulo —el crédito
/// por <c>SimularAsync</c> del contrato de obligaciones (el abono extraordinario trae sus dos alternativas), la cuenta por el de cuentas—, y los conceptos
/// varios con sus reglas. Un rechazo de un módulo responde <c>Receipts.Receipt.ItemRejected</c> con <c>data.items[]</c> y nada cambia. (nuevo)
/// </summary>
public sealed record UpdateCashOperationItemsCommand(Guid OperationPublicId, IReadOnlyList<ItemPedidoDto> Items)
    : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCashOperationItemsCommandValidator : AbstractValidator<UpdateCashOperationItemsCommand>
{
    public UpdateCashOperationItemsCommandValidator()
    {
        RuleFor(x => x.OperationPublicId).NotEmpty();
        RuleFor(x => x.Items).NotNull();
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Amount).GreaterThan(0m);
            i.RuleFor(x => x.Target).Must(t => t is ApplicationTarget.Loan or ApplicationTarget.Account or ApplicationTarget.MiscConcept
                    or ApplicationTarget.PendingApplication)
                .WithMessage("La caja cobra créditos, cuentas, conceptos varios y recaudo pendiente de aplicar.");
            // Feature 015, R1 (T0363): el pendiente dice a qué módulo irá.
            i.RuleFor(x => x.PendingTarget).Must(t => t is ApplicationTarget.Loan or ApplicationTarget.Account)
                .When(x => x.Target == ApplicationTarget.PendingApplication).WithMessage("Diga si el pendiente es de un crédito o de una cuenta.");
            i.RuleFor(x => x.ProductPublicId).NotEmpty().When(x => x.Target is ApplicationTarget.Loan or ApplicationTarget.Account);
            i.RuleFor(x => x.LoanModality).NotNull().When(x => x.Target == ApplicationTarget.Loan);
            i.RuleFor(x => x.AccountModality).NotNull().When(x => x.Target == ApplicationTarget.Account);
            i.RuleFor(x => x.MiscConceptPublicId).NotEmpty().When(x => x.Target == ApplicationTarget.MiscConcept);
            i.RuleFor(x => x.CrossDocument).MaximumLength(60);
        });
    }
}

public sealed class UpdateCashOperationItemsCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IConsultaDeCargaSinActivar? carga = null) : IRequestHandler<UpdateCashOperationItemsCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(UpdateCashOperationItemsCommand request, CancellationToken ct)
    {
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var (operacion, _) = v;
        if (operacion.Status != CashOperationState.Preparing) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        var titularPorDefecto = await db.People.AsNoTracking().Where(p => p.Id == (operacion.BeneficiaryPersonId ?? operacion.PayerPersonId))
            .Select(p => p.PublicId).FirstAsync(ct);

        var items = new List<ItemDeCajaDto>();
        var rechazos = new List<object>();
        foreach (var pedido in request.Items)
        {
            var id = pedido.ItemId ?? Guid.NewGuid();
            var titular = pedido.BeneficiaryPersonPublicId ?? titularPorDefecto;
            switch (pedido.Target)
            {
                case ApplicationTarget.Loan:
                {
                    var r = await obligaciones.SimularAsync(new SolicitudDeSimulacion(ClavesDeRecaudo.Recaudador, pedido.ProductPublicId!.Value, pedido.LoanModality!.Value,
                        pedido.Amount, operacion.ValueDate, pedido.Choice, pedido.InstallmentNumber), ct);
                    if (r.Result == ResultadoDelContrato.Rejected || r.Value is null) { rechazos.Add(new { itemId = id, code = r.Code, message = r.Message }); continue; }
                    var s = r.Value;
                    var alternativas = s.Alternatives.Select(a => new AlternativaDeAbonoDto(a.Choice, a.AppliedAmount, a.NewPlan)).ToList();
                    var anticipadoSinCapital = pedido.LoanModality == ModalidadDePago.Advance && s.Distribution.All(d => d.Concept != ConceptoDeObligacion.Principal);
                    items.Add(new ItemDeCajaDto(id, pedido.Target, pedido.Amount, pedido.ProductPublicId, null, titular, pedido.LoanModality, null, pedido.Choice,
                        pedido.InstallmentNumber, null, pedido.AdvanceNoticeAccepted, null, null, null, s.SimulationId, s.AppliedAmount, s.Surplus,
                        anticipadoSinCapital, false, alternativas, s.Notices));
                    break;
                }
                case ApplicationTarget.Account:
                {
                    var r = await cuentas.SimularAsync(new SolicitudDeSimulacionDeCuenta(ClavesDeRecaudo.Recaudador, "Deposit", pedido.ProductPublicId!.Value,
                        pedido.Amount, operacion.ValueDate, ClaseDeMedioDeRecaudo.Cash, [], null, pedido.AccountModality), ct);
                    if (r.Result == ResultadoDelContrato.Rejected || r.Value is null || !r.Value.Allowed)
                    {
                        rechazos.Add(new { itemId = id, code = r.Code, message = r.Message });
                        continue;
                    }
                    items.Add(new ItemDeCajaDto(id, pedido.Target, pedido.Amount, pedido.ProductPublicId, null, titular, null, pedido.AccountModality, null, null,
                        pedido.InstallmentPublicIds, false, null, null, null, r.Value.SimulationId, pedido.Amount, 0m, false, r.Value.RequiresSourceOfFunds, [],
                        r.Value.Notices));
                    break;
                }
                case ApplicationTarget.PendingApplication:
                {
                    // Feature 015, R1 (T0363; FR-046, D14): sólo para una persona de una carga aplicada sin activar; si no, el ítem se rechaza con el motivo.
                    var error = await new ReglasDelPendiente(obligaciones, cuentas, carga).ElegibleAsync(titular, pedido.PendingTarget!.Value, operacion.ValueDate, ct);
                    if (error is not null) { rechazos.Add(new { itemId = id, code = error.Code, message = error.Message }); continue; }
                    items.Add(new ItemDeCajaDto(id, pedido.Target, pedido.Amount, null, pedido.ProductReference, titular, null, null, null, null, null, false, null, null,
                        null, null, pedido.Amount, 0m, false, false, [], ["Recaudo pendiente de aplicar: se aplica el día de la activación del módulo."],
                        pedido.PendingTarget, pedido.LoadObligationPublicId, pedido.ProductReference?.Trim()));
                    break;
                }
                default:
                {
                    var concepto = await db.MiscConcepts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == pedido.MiscConceptPublicId, ct);
                    var error = ReglasDeConcepto.Validar(concepto, pedido, operacion.BeneficiaryPersonId is not null || pedido.BeneficiaryPersonPublicId is not null);
                    if (error is not null) { rechazos.Add(new { itemId = id, code = error.Code, message = error.Message }); continue; }
                    items.Add(new ItemDeCajaDto(id, pedido.Target, pedido.Amount, null, concepto!.Code, titular, null, null, null, null, null, false,
                        pedido.MiscConceptPublicId, pedido.CrossDocument?.Trim(), pedido.InvoicePublicId, null, pedido.Amount, 0m, false, false, [], []));
                    break;
                }
            }
        }
        if (rechazos.Count > 0) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.ItemRechazado(rechazos));

        operacion.ItemsJson = OperacionesDeCaja.Json(items);
        // Los ítems cambiaron: la imputación anterior ya no vale.
        operacion.ImputationJson = "[]";
        operacion.ImputationFixed = false;
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}

/// <summary>Las reglas de un concepto vario en un recibo (FR-050, FR-179). (nuevo)</summary>
internal static class ReglasDeConcepto
{
    public static Error? Validar(Domain.Entities.Receipts.Configuration.MiscConcept? concepto, ItemPedidoDto pedido, bool conTercero)
    {
        if (concepto is null || concepto.Status != ReceiptsCatalogStatus.Active || concepto.Direction != MiscConceptDirection.Income)
            return ErroresDeRecaudo.ConceptoInvalido();
        if (concepto.RequiresInvoice && pedido.InvoicePublicId is null) return ErroresDeRecaudo.FacturaRequerida(concepto.Code);
        if (concepto.RequiresCrossDocument && string.IsNullOrWhiteSpace(pedido.CrossDocument)) return ErroresDeRecaudo.CruceRequerido(concepto.Code);
        if (concepto.RequiresThirdParty && !conTercero && pedido.BeneficiaryPersonPublicId is null) return ErroresDeRecaudo.TerceroRequerido(concepto.Code);
        if (concepto.MaxAmount is { } maximo && pedido.Amount > maximo)
            return new ErrorConDatos(ErroresDeRecaudo.CodigoConceptoInvalido, $"El concepto {concepto.Code} admite hasta {maximo:N0}.", new { max = maximo });
        return null;
    }
}

/// <summary>
/// <c>PUT /api/receipts/cash-operations/{id}/means</c> (<c>Receipts.Receipt.Register</c>): los medios —sellados en la sesión—, el bono de número único que no
/// se repite, y la imputación propuesta por <c>ImputacionDeMedios</c> con el orden de <c>Recibo.OrdenDeImputacion</c> o la fijada por el cajero (sólo se
/// valida). Con efectivo, el tope de bloqueo de la caja se respeta (<c>Receipts.Session.CashBlockCapReached</c>, FR-181). (nuevo)
/// </summary>
public sealed record UpdateCashOperationMeansCommand(Guid OperationPublicId, IReadOnlyList<MedioDeCajaDto> Means, IReadOnlyList<PorcionDeCajaDto>? FixedImputation = null)
    : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCashOperationMeansCommandValidator : AbstractValidator<UpdateCashOperationMeansCommand>
{
    public UpdateCashOperationMeansCommandValidator()
    {
        RuleFor(x => x.OperationPublicId).NotEmpty();
        RuleFor(x => x.Means).NotEmpty().WithMessage("Registre al menos un medio.");
        RuleForEach(x => x.Means).ChildRules(m =>
        {
            m.RuleFor(x => x.Amount).GreaterThan(0m);
            m.RuleFor(x => x.Reference).MaximumLength(80);
            m.RuleFor(x => x.VoucherNumber).MaximumLength(40);
            m.RuleFor(x => x.ApprovalCode).MaximumLength(20);
            m.RuleFor(x => x.TerminalBatch).MaximumLength(20);
        });
    }
}

public sealed class UpdateCashOperationMeansCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, ParametrosDeCaja parametros, IDateTimeService reloj)
    : IRequestHandler<UpdateCashOperationMeansCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(UpdateCashOperationMeansCommand request, CancellationToken ct)
    {
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var (operacion, sesion) = v;
        if (operacion.Status == CashOperationState.Blocked) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.ListaVinculante());
        if (operacion.Status != CashOperationState.Preparing) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        var items = OperacionesDeCaja.Items(operacion);
        if (items.Count == 0) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionSinItems());
        var sellados = ValoresSelladosDeLaSesion.De(sesion);

        var medios = request.Means.Select(m => m with { MeansId = m.MeansId ?? Guid.NewGuid() }).ToList();
        foreach (var m in medios.Where(m => !string.IsNullOrWhiteSpace(m.VoucherNumber)))
        {
            var medioId = sellados.Means.FirstOrDefault(x => x.PublicId == m.PaymentMeansPublicId)?.PaymentMeansId;
            var bono = m.VoucherNumber!.Trim();
            if (medioId is { } id && await db.ReceiptMeans.AsNoTracking().AnyAsync(x => x.PaymentMeansId == id && x.VoucherNumber == bono, ct))
                return Result.Failure<CashOperationDto>(ErroresDeRecaudo.BonoUsado(bono));
        }
        // Feature 015, R1 (T0357; FR-024, FR-031, FR-041): tarjetas con datáfono, lote y aprobación (sin el número), bono con número único, «otro» con referencia.
        var captura = await CapturaDeMedios.ValidarAsync(db, sellados, medios, ct);
        if (captura.IsFailure) return Result.Failure<CashOperationDto>(captura.Error);
        // Feature 015, US15 (T0217; FR-083, FR-084, FR-086): un cheque trae sus datos y pasa sus reglas antes de imputarse (va al final, D35).
        foreach (var m in medios.Where(m => sellados.Means.FirstOrDefault(x => x.PublicId == m.PaymentMeansPublicId)?.Class == PaymentMeansClass.Check))
        {
            var cheque = await Checks.ChequesRecibidos.ValidarAsync(db, parametros, m.Check, m.Amount, operacion.PayerPersonId, reloj.HoyLocal, enCustodia: false, ct);
            if (cheque.IsFailure) return Result.Failure<CashOperationDto>(cheque.Error);
        }

        var orden = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.ReciboOrdenDeImputacion, reloj.HoyLocal, ct) == nameof(OrdenDeImputacion.ElegidoEnCaja)
            ? OrdenDeImputacion.ElegidoEnCaja
            : OrdenDeImputacion.InmediatosPrimeroChequeAlFinal;
        var imputacion = OperacionesDeCaja.Imputar(items, medios, sellados, orden, request.FixedImputation);
        if (imputacion.IsFailure) return Result.Failure<CashOperationDto>(imputacion.Error);

        var efectivo = OperacionesDeCaja.Efectivo(medios, sellados) - imputacion.Value.Vueltas;
        if (efectivo > 0m && sellados.CashBlockCap is { } tope && await SesionesDeCaja.EfectivoAsync(db, sesion, ct) + efectivo > tope)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.TopeDeBloqueo(tope));

        operacion.MeansJson = OperacionesDeCaja.Json(medios);
        operacion.ImputationJson = OperacionesDeCaja.Json(OperacionesDeCaja.Porciones(imputacion.Value));
        operacion.ImputationFixed = request.FixedImputation is not null;
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-operations/{id}/declaration</c> (<c>Receipts.Receipt.Register</c>): la declaración de origen de fondos con su soporte (FR-094),
/// que la confirmación exige cuando el efectivo pasa de <c>Sarlaft.UmbralDeclaracionOrigenFondos</c> o un módulo la pidió. (nuevo)
/// </summary>
public sealed record DeclareSourceOfFundsCommand(Guid OperationPublicId, string Origin, decimal CashAmount, Guid SupportAttachmentPublicId)
    : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class DeclareSourceOfFundsCommandValidator : AbstractValidator<DeclareSourceOfFundsCommand>
{
    public DeclareSourceOfFundsCommandValidator()
    {
        RuleFor(x => x.OperationPublicId).NotEmpty();
        RuleFor(x => x.Origin).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CashAmount).GreaterThan(0m);
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte de la declaración.");
    }
}

public sealed class DeclareSourceOfFundsCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<DeclareSourceOfFundsCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(DeclareSourceOfFundsCommand request, CancellationToken ct)
    {
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var operacion = v.Operacion;
        if (operacion.Status != CashOperationState.Preparing) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        var declaracion = new SourceOfFundsDeclaration
        {
            PersonId = operacion.DeliveredByPersonId ?? operacion.PayerPersonId,
            ReceiptOperationPublicId = operacion.PublicId,
            CashAmount = request.CashAmount,
            Origin = request.Origin.Trim(),
            SupportAttachmentPublicId = request.SupportAttachmentPublicId,
            DeclaredAt = reloj.UtcNow,
            RequiredBy = SourceOfFundsRequiredBy.ReceiptsThreshold,
        };
        db.SourceOfFundsDeclarations.Add(declaracion);
        await db.SaveChangesAsync(ct);
        operacion.SourceOfFundsDeclarationId = declaracion.Id;
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}

/// <summary><c>POST /api/receipts/cash-operations/{id}/abandon</c> (<c>Receipts.Receipt.Register</c>): la operación se abandona sin dejar nada. (nuevo)</summary>
public sealed record AbandonCashOperationCommand(Guid OperationPublicId) : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class AbandonCashOperationCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<AbandonCashOperationCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(AbandonCashOperationCommand request, CancellationToken ct)
    {
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        if (v.Operacion.Status is not (CashOperationState.Preparing or CashOperationState.Blocked))
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        v.Operacion.Status = CashOperationState.Abandoned;
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, v.Operacion, ct));
    }
}

/// <summary>
/// <c>PUT /api/receipts/cash-operations/{id}/delivered-by</c> (<c>Receipts.Receipt.Register</c>; decisión del dueño del 2026-10-24 sobre T0115): registra (o quita, con
/// nulo) quién efectúa el pago en la ventanilla de una operación en preparación; la confirmación lo exige con el efectivo en el umbral de la transacción individual
/// o cuando el pagador no es el titular (<c>Receipts.Compliance.DeliveredByRequired</c>). (nuevo)
/// </summary>
public sealed record SetCashOperationDeliveredByCommand(Guid OperationPublicId, Guid? DeliveredByPersonPublicId) : IRequest<Result<CashOperationDto>>,
    IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetCashOperationDeliveredByCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<SetCashOperationDeliveredByCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(SetCashOperationDeliveredByCommand request, CancellationToken ct)
    {
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        if (v.Operacion.Status != CashOperationState.Preparing) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        int? persona = null;
        if (request.DeliveredByPersonPublicId is { } p)
        {
            persona = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (persona is null) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.PersonaNoEncontrada());
        }
        v.Operacion.DeliveredByPersonId = persona;
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, v.Operacion, ct));
    }
}
