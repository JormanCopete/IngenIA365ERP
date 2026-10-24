using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Paymasters;

/// <summary>Una línea del giro de una pagaduría, para la pantalla. (nuevo)</summary>
public sealed record PaymasterRemittanceLineDto(Guid PublicId, int Order, Guid DeductionFileLinePublicId, string ProductNumber, string PersonName, decimal Amount,
    string IdempotencyKey, PaymasterRemittanceLineOutcome Outcome, string? ReceiptNumber, string? RejectionCode, string? RejectionMessage, string? CreditBalanceNumber);

/// <summary>El giro de una pagaduría con sus líneas y su excedente. (nuevo)</summary>
public sealed record PaymasterRemittanceDto(Guid PublicId, string Number, Guid PaymasterPersonPublicId, string PaymasterName, PaymasterRemittanceSource Source,
    Guid? BankStatementLinePublicId, decimal Amount, DateOnly RemittedOn, PaymasterRemittanceState Status, PaymasterAllocationOrder AllocationOrder, decimal Allocated,
    decimal Surplus, int Lines, int Applied, int Rejected, int Pending, Guid? LastRunPublicId, IReadOnlyList<PaymasterRemittanceLineDto> Items);

/// <summary>
/// <c>POST /api/receipts/paymaster-remittances</c> (<c>Receipts.Paymasters.RegisterRemittance</c>, con clave; FR-078, FR-079): registra el giro de una pagaduría identificado
/// en una línea del extracto y lo reparte sobre lo descontado y no girado de los archivos elegidos (respondidos y aceptados por Cartera), en el orden del archivo
/// —el de prelación con que lo armó Cartera— o en el que diga el analista. El giro menor deja pendientes las que no cubre; el mayor exige
/// <see cref="SurplusAsCreditBalance"/> y deja el excedente como saldo de la pagaduría. El hecho <c>GiroDePagaduriaRecibido</c> lleva el dinero al saldo de pagadurías;
/// aplicar las líneas es otro paso. En R0 la fuente es la línea del extracto: el recibo de caja y el cheque recibido llegan con sus historias. (nuevo)
/// </summary>
public sealed record RegisterPaymasterRemittanceCommand(
    Guid PaymasterPersonPublicId,
    Guid BankStatementLinePublicId,
    IReadOnlyList<Guid> FilePublicIds,
    decimal? Amount = null,
    PaymasterAllocationOrder AllocationOrder = PaymasterAllocationOrder.AgreementPriority,
    IReadOnlyList<Guid>? LineOrder = null,
    bool SurplusAsCreditBalance = false,
    string? Note = null) : IRequest<Result<PaymasterRemittanceDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterPaymasterRemittanceCommandValidator : AbstractValidator<RegisterPaymasterRemittanceCommand>
{
    public RegisterPaymasterRemittanceCommandValidator()
    {
        RuleFor(x => x.PaymasterPersonPublicId).NotEmpty();
        RuleFor(x => x.BankStatementLinePublicId).NotEmpty();
        RuleFor(x => x.FilePublicIds).NotEmpty().WithMessage("Elija los archivos de descuentos que cubre el giro.");
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null);
        RuleFor(x => x.LineOrder).NotEmpty().When(x => x.AllocationOrder == PaymasterAllocationOrder.AnalystOrder)
            .WithMessage("Con el orden del analista, indique el orden de las líneas.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class RegisterPaymasterRemittanceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterPaymasterRemittanceCommand, Result<PaymasterRemittanceDto>>
{
    public async Task<Result<PaymasterRemittanceDto>> Handle(RegisterPaymasterRemittanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var mio = await alcance.ObtenerAsync(ct);
        var pagaduria = await db.People.AsNoTracking().Where(p => p.PublicId == request.PaymasterPersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (pagaduria is null || !mio.IncluyePagaduria(pagaduria.Value)) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.NoEncontrado("La pagaduría"));
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, request.BankStatementLinePublicId, ct);
        if (visto.IsFailure) return Result.Failure<PaymasterRemittanceDto>(visto.Error);
        var (abono, oficina) = visto.Value;
        var pendienteDelAbono = abono.Amount - abono.Identified;
        if (pendienteDelAbono <= 0m) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.AbonoYaIdentificado());
        var valor = request.Amount ?? pendienteDelAbono;
        if (valor > pendienteDelAbono) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.AbonoSobreIdentificado(pendienteDelAbono));

        var archivos = await db.ReceiptsPayrollDeductionFiles.AsNoTracking()
            .Where(f => request.FilePublicIds.Contains(f.PublicId) && f.PaymasterPersonId == pagaduria.Value && f.Status == PayrollDeductionFileState.Responded)
            .Select(f => f.Id).ToListAsync(ct);
        if (archivos.Count != request.FilePublicIds.Distinct().Count()) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.NoEncontrado("Un archivo de descuentos respondido"));

        await cerrojo.TomarSetupAsync(ct: ct);
        var lineas = await db.ReceiptsPayrollDeductionFileLines
            .Where(l => archivos.Contains(l.PayrollDeductionFileId) && l.DeductedAmount != null && l.DeductedAmount > l.RemittedAmount)
            .OrderBy(l => l.PayrollDeductionFileId).ThenBy(l => l.Id).ToListAsync(ct);
        if (lineas.Count == 0) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.GiroSinLineas());
        await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.LineasDeDescuento, lineas.Select(l => (long)l.Id).Order().ToList(), ct: ct);
        if (request.AllocationOrder == PaymasterAllocationOrder.AnalystOrder && request.LineOrder is { Count: > 0 } orden)
        {
            var posicion = orden.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            lineas = lineas.OrderBy(l => posicion.GetValueOrDefault(l.DeductionFileLinePublicId, int.MaxValue)).ThenBy(l => l.Id).ToList();
        }
        var pendiente = lineas.Sum(l => l.DeductedAmount!.Value - l.RemittedAmount);
        if (valor > pendiente && !request.SurplusAsCreditBalance) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.GiroExcedePendiente(pendiente));

        var ahora = reloj.UtcNow;
        var giro = new PaymasterRemittance
        {
            PaymasterPersonId = pagaduria.Value, Source = PaymasterRemittanceSource.BankItem, BankStatementLinePublicId = abono.PublicId,
            OwnBankAccountPublicId = abono.OwnBankAccountPublicId, BranchId = oficina, Amount = valor, RemittedOn = abono.MovementDate,
            AllocationOrder = request.AllocationOrder, RegisteredByUserId = usuario, RegisteredAt = ahora,
            PeriodsJson = JsonSerializer.Serialize(await db.ReceiptsPayrollDeductionFiles.AsNoTracking().Where(f => archivos.Contains(f.Id))
                .Select(f => new { f.PeriodYear, f.PeriodMonth, f.SubPeriod, f.FileNumber }).ToListAsync(ct)),
        };
        var resto = valor;
        var repartidas = new List<(PayrollDeductionFileLine Linea, decimal Valor)>();
        foreach (var l in lineas)
        {
            if (resto <= 0m) break;
            var toca = Math.Min(resto, l.DeductedAmount!.Value - l.RemittedAmount);
            repartidas.Add((l, toca));
            resto -= toca;
        }
        giro.Number = await numerador.AsignarAsync(EmisionDeRecaudo.TipoGiroDePagaduria, EmisionDeRecaudo.TipoGiroDePagaduria, ct);
        db.PaymasterRemittances.Add(giro);
        await db.SaveChangesAsync(ct);
        var n = 0;
        foreach (var (l, toca) in repartidas)
        {
            db.PaymasterRemittanceLines.Add(new PaymasterRemittanceLine
            {
                PaymasterRemittanceId = giro.Id, Order = ++n, PayrollDeductionFileLineId = l.Id, DeductionFileLinePublicId = l.DeductionFileLinePublicId, Amount = toca,
                IdempotencyKey = GirosDePagaduria.Clave(giro.PublicId, l.DeductionFileLinePublicId),
            });
            l.RemittedAmount += toca;
        }
        if (resto > 0m)
        {
            var saldo = SaldosAFavor.Reconocer(db, pagaduria.Value, PayerCreditBalanceOrigin.PaymasterSurplus, giro.PublicId, giro.Number, oficina, resto, reloj.HoyLocal, usuario,
                ahora, abono.OwnBankAccountPublicId);
            saldo.Kind = PayerCreditBalanceKind.Paymaster;
            await db.SaveChangesAsync(ct);
            SaldosAFavor.Reconocido(db, saldo, usuario, ahora);
            giro.SurplusCreditBalanceId = saldo.Id;
        }
        var secuencia = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, abono.PublicId, ct);
        db.BankItemIdentifications.Add(new BankItemIdentification
        {
            BankStatementLinePublicId = abono.PublicId, Sequence = secuencia, OwnBankAccountPublicId = abono.OwnBankAccountPublicId, MovementDate = abono.MovementDate,
            Amount = valor, Outcome = BankItemOutcome.PaymasterRemittance, PaymasterRemittanceId = giro.Id, IdentifiedByUserId = usuario, IdentifiedAt = ahora, Note = request.Note,
        });
        await movimientos.MarcarIdentificacionAsync(abono.PublicId, abono.Identified + valor, ct);
        await emision.GiroDePagaduriaRecibidoAsync(giro, ct);
        await auditoria.AgregarAsync("Receipts.PaymasterRemittance.Registered", "PaymasterRemittance", giro.PublicId, null,
            new { number = giro.Number, amount = valor, lines = repartidas.Count, surplus = resto, order = request.AllocationOrder.ToString(), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await GirosDePagaduria.DtoAsync(db, giro, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/paymaster-remittances/{id}/apply</c> (<c>Receipts.Paymasters.ApplyRemittance</c>, con clave; R-16, R-17; FR-078 a FR-081, FR-171): aplica las
/// líneas pendientes del giro por el contrato que corresponda (créditos por el de obligaciones, cuotas de cuentas por el de cuentas), cada una su unidad con
/// <c>ExternalPaymasterDeduction</c>, <c>DeductionFileLinePublicId</c>, la fecha valor del descuento y la clave <c>REC:{giro:N}:{línea:N}</c>. La tanda toma todos sus
/// cerrojos antes de la primera línea. Lo que el contrato rechaza (referencia inválida, cuenta bloqueada) queda como saldo de la pagaduría con la alerta
/// <c>Recaudo.NoAplicadoDePagaduria</c> y se reprocesa con la misma clave. (nuevo)
/// </summary>
public sealed record ApplyPaymasterRemittanceCommand(Guid RemittancePublicId, int MaxLines = 200, Guid? RunPublicId = null)
    : IRequest<Result<PaymasterRemittanceDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyPaymasterRemittanceCommandValidator : AbstractValidator<ApplyPaymasterRemittanceCommand>
{
    public ApplyPaymasterRemittanceCommandValidator()
    {
        RuleFor(x => x.RemittancePublicId).NotEmpty();
        RuleFor(x => x.MaxLines).InclusiveBetween(1, 500);
    }
}

public sealed class ApplyPaymasterRemittanceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    GirosDePagaduria giros) : IRequestHandler<ApplyPaymasterRemittanceCommand, Result<PaymasterRemittanceDto>>
{
    public async Task<Result<PaymasterRemittanceDto>> Handle(ApplyPaymasterRemittanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var giro = await db.PaymasterRemittances.FirstOrDefaultAsync(g => g.PublicId == request.RemittancePublicId, ct);
        if (giro is null || !(await alcance.ObtenerAsync(ct)).IncluyePagaduria(giro.PaymasterPersonId))
            return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.NoEncontrado("El giro de la pagaduría"));
        var pendientes = await db.PaymasterRemittanceLines.Where(l => l.PaymasterRemittanceId == giro.Id && l.Outcome == PaymasterRemittanceLineOutcome.Pending)
            .OrderBy(l => l.Order).Take(request.MaxLines).ToListAsync(ct);
        if (pendientes.Count == 0) return Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.GiroYaAplicado());
        var corrida = await giros.AplicarAsync(giro, pendientes, request.RunPublicId, usuario, ct);
        var dto = await GirosDePagaduria.DtoAsync(db, giro, ct);
        return Result.Success(dto with { LastRunPublicId = corrida.PublicId });
    }
}

/// <summary>
/// <c>POST /api/receipts/paymaster-balances/{id}/resolve</c> (<c>Receipts.Paymasters.ApplyRemittance</c>, con clave; FR-080): resuelve un saldo de la pagaduría —el
/// excedente de un giro o una línea que el contrato rechazó— con soporte: devolverlo por orden de giro a la cuenta de la pagaduría o aplicarlo a un recibo. Sin soporte,
/// <c>Receipts.Paymaster.SupportRequired</c>; un saldo de un pagador se resuelve en «Saldos a favor». (nuevo)
/// </summary>
public sealed record ResolvePaymasterBalanceCommand(Guid BalancePublicId, string Resolution, decimal Amount, Guid SupportAttachmentPublicId,
    Guid? PersonBankAccountPublicId = null, Guid? ReceiptPublicId = null, string? Note = null) : IRequest<Result<PayerCreditBalanceDto>>, IOperacionIdempotente,
    IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ResolvePaymasterBalanceCommandValidator : AbstractValidator<ResolvePaymasterBalanceCommand>
{
    public ResolvePaymasterBalanceCommandValidator()
    {
        RuleFor(x => x.BalancePublicId).NotEmpty();
        RuleFor(x => x.Resolution).Must(r => r is "Refund" or "Apply").WithMessage("Se resuelve devolviéndolo (Refund) o aplicándolo a un recibo (Apply).");
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.ReceiptPublicId).NotNull().When(x => x.Resolution == "Apply");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class ResolvePaymasterBalanceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    AuditoriaDeRecaudo auditoria,
    ISender sender) : IRequestHandler<ResolvePaymasterBalanceCommand, Result<PayerCreditBalanceDto>>
{
    public async Task<Result<PayerCreditBalanceDto>> Handle(ResolvePaymasterBalanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var saldo = await db.PayerCreditBalances.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.BalancePublicId, ct);
        if (saldo is null || !(await alcance.ObtenerAsync(ct)).IncluyePagaduria(saldo.PersonId))
            return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.NoEncontrado("El saldo de la pagaduría"));
        if (saldo.Kind != PayerCreditBalanceKind.Paymaster) return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.SaldoNoEsDePagaduria());
        if (request.SupportAttachmentPublicId == Guid.Empty || !await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == request.SupportAttachmentPublicId, ct))
            return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.SoporteDePagaduriaRequerido());
        var clave = AplicacionNoInteractiva.ClaveDelRecibo($"resolver-saldo-de-pagaduria:{request.OperationKey:N}");
        Result<PayerCreditBalanceDto> r = request.Resolution == "Refund"
            ? await sender.Send(new RefundPayerCreditBalanceByOrderCommand(saldo.PublicId, request.Amount, request.PersonBankAccountPublicId) { OperationKey = clave }, ct)
            : await sender.Send(new ApplyPayerCreditBalanceCommand(saldo.PublicId, request.Amount, request.ReceiptPublicId!.Value) { OperationKey = clave }, ct);
        if (r.IsFailure) return r;
        await auditoria.AgregarAsync("Receipts.PaymasterBalance.Resolved", "PayerCreditBalance", saldo.PublicId, null,
            new { number = saldo.OriginDocumentNumber, resolution = request.Resolution, amount = request.Amount, support = request.SupportAttachmentPublicId, note = request.Note,
                by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return r;
    }
}

/// <summary><c>GET /api/receipts/paymaster-remittances/{id}</c> (<c>Receipts.Paymasters.View</c>). (nuevo)</summary>
public sealed record GetPaymasterRemittanceQuery(Guid RemittancePublicId) : IRequest<Result<PaymasterRemittanceDto>>;

public sealed class GetPaymasterRemittanceQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetPaymasterRemittanceQuery, Result<PaymasterRemittanceDto>>
{
    public async Task<Result<PaymasterRemittanceDto>> Handle(GetPaymasterRemittanceQuery request, CancellationToken ct)
    {
        var giro = await db.PaymasterRemittances.AsNoTracking().FirstOrDefaultAsync(g => g.PublicId == request.RemittancePublicId, ct);
        return giro is null || !(await alcance.ObtenerAsync(ct)).IncluyePagaduria(giro.PaymasterPersonId)
            ? Result.Failure<PaymasterRemittanceDto>(ErroresDeRecaudo.NoEncontrado("El giro de la pagaduría"))
            : Result.Success(await GirosDePagaduria.DtoAsync(db, giro, ct));
    }
}

/// <summary><c>GET /api/receipts/paymaster-remittances?paymaster=</c> (<c>Receipts.Paymasters.View</c>): los giros al alcance, los más recientes primero. (nuevo)</summary>
public sealed record ListPaymasterRemittancesQuery(Guid? PaymasterPersonPublicId = null) : IRequest<Result<IReadOnlyList<PaymasterRemittanceDto>>>;

public sealed class ListPaymasterRemittancesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListPaymasterRemittancesQuery, Result<IReadOnlyList<PaymasterRemittanceDto>>>
{
    public async Task<Result<IReadOnlyList<PaymasterRemittanceDto>>> Handle(ListPaymasterRemittancesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.PaymasterRemittances.AsNoTracking();
        if (request.PaymasterPersonPublicId is { } p)
        {
            var id = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(g => g.PaymasterPersonId == id);
        }
        var filas = (await consulta.OrderByDescending(g => g.Id).Take(200).ToListAsync(ct)).Where(g => mio.IncluyePagaduria(g.PaymasterPersonId)).ToList();
        var lista = new List<PaymasterRemittanceDto>(filas.Count);
        foreach (var g in filas) lista.Add(await GirosDePagaduria.DtoAsync(db, g, ct));
        return Result.Success<IReadOnlyList<PaymasterRemittanceDto>>(lista);
    }
}

/// <summary>
/// La aplicación de las líneas de un giro (feature 015, US7, T0208): la usan aplicar y reprocesar (<see cref="IReprocesoDeCorrida"/>). Cada línea es su unidad por
/// <see cref="AplicacionNoInteractiva"/>; la tanda toma antes todos sus cerrojos (configuración, pagadores —la pagaduría y los titulares— y las líneas del giro). (nuevo)
/// </summary>
public sealed class GirosDePagaduria(
    IApplicationDbContext db,
    ICerrojoDeRecaudo cerrojo,
    AplicacionNoInteractiva aplicacion,
    AuditoriaDeRecaudo auditoria,
    IAlertas alertas,
    IDateTimeService reloj) : IReprocesoDeCorrida
{
    public ProcessingRunKind Clase => ProcessingRunKind.PaymasterRemittance;

    /// <summary>La clave de una línea de giro: <c>REC:{giro:N}:{línea:N}</c>.</summary>
    public static string Clave(Guid giro, Guid lineaDelArchivo) => string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:{giro:N}:{lineaDelArchivo:N}");

    public async Task<ProcessingRun> AplicarAsync(PaymasterRemittance giro, IReadOnlyList<PaymasterRemittanceLine> lineas, Guid? corridaPedida, int usuario, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var corrida = await CorridasNoInteractivas.AbrirAsync(db, ProcessingRunKind.PaymasterRemittance, giro.PublicId, giro.Number, corridaPedida, usuario, ahora, ct);
        await db.SaveChangesAsync(ct);
        var ids = lineas.Select(l => l.PayrollDeductionFileLineId).ToList();
        var delArchivo = await db.ReceiptsPayrollDeductionFileLines.AsNoTracking().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var personas = delArchivo.Values.Select(l => l.PersonId).Append(giro.PaymasterPersonId).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);

        // Todos los cerrojos de la tanda antes de la primera línea (el orden fijo no vuelve a los pagadores después de numerar).
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync(personas.Order().ToList(), ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.LineasDeGiro, lineas.Select(l => (long)l.Id).Order().ToList(), ct: ct);

        var recibos = new List<Receipt>();
        var rechazadas = new List<ProcessingRunLine>();
        foreach (var l in lineas)
        {
            var linea = delArchivo[l.PayrollDeductionFileLineId];
            var deCorrida = await CorridasNoInteractivas.LineaAsync(db, corrida, $"{giro.Number}·{l.Order}", l.IdempotencyKey, l.Amount, new { remittanceLine = l.PublicId }, ct);
            await db.SaveChangesAsync(ct);
            var resultado = await aplicacion.AplicarAsync(new LineaNoInteractiva(l.IdempotencyKey, ReceiptChannel.Paymaster, publicos[giro.PaymasterPersonId],
                linea.DeductionDate ?? giro.RemittedOn, l.Amount, giro.BranchId,
                [new ParteNoInteractiva(linea.Kind == PayrollDeductionLineKind.AccountInstallment ? ApplicationTarget.Account : ApplicationTarget.Loan, linea.ProductPublicId,
                    publicos[linea.PersonId], l.Amount, InstallmentPublicIds: linea.InstallmentPublicId is { } cuota ? [cuota] : null,
                    DeductionFileLinePublicId: linea.DeductionFileLinePublicId, Key: l.IdempotencyKey)],
                ClaseDeMedioDeRecaudo.ExternalPaymasterDeduction, PaymentMeansClass.Transfer, giro.OwnBankAccountPublicId, giro.Number,
                giro.BankStatementLinePublicId, giro.Id, AllowLateValueDate: false, PaymasterReference: true, CerrojosTomados: true), ct);
            CorridasNoInteractivas.Anotar(corrida, deCorrida, resultado, ahora);
            await AnotarAsync(giro, l, linea, resultado, usuario, ahora, ct);
            if (resultado.Outcome == ProcessingLineOutcome.Applied && resultado.Receipt is { } r) recibos.Add(r);
            if (resultado.Outcome == ProcessingLineOutcome.Rejected) rechazadas.Add(deCorrida);
            await db.SaveChangesAsync(ct);
        }
        await EstadoAsync(giro, ct);
        await CorridasNoInteractivas.CerrarTandaAsync(db, auditoria, corrida, recibos, rechazadas, ahora, ct);
        await db.SaveChangesAsync(ct);
        return corrida;
    }

    public async Task<Result> ReprocesarAsync(ProcessingRun corrida, ProcessingRunLine linea, int usuario, CancellationToken ct)
    {
        var deGiro = await db.PaymasterRemittanceLines.FirstOrDefaultAsync(l => l.IdempotencyKey == linea.IdempotencyKey, ct);
        if (deGiro is null) return Result.Failure(ErroresDeRecaudo.NoEncontrado("La línea del giro"));
        var giro = await db.PaymasterRemittances.FirstAsync(g => g.Id == deGiro.PaymasterRemittanceId, ct);
        if (deGiro.Outcome == PaymasterRemittanceLineOutcome.Applied) return Result.Success();
        if (deGiro.CreditBalanceId is { } s
            && await db.PayerCreditBalances.AsNoTracking().FirstAsync(x => x.Id == s, ct) is { } saldo && saldo.Balance < deGiro.Amount)
            return Result.Failure(ErroresDeRecaudo.CorridaLineaNoReprocesable("su saldo de la pagaduría ya se resolvió"));
        deGiro.Outcome = PaymasterRemittanceLineOutcome.Pending;
        await AplicarAsync(giro, [deGiro], corrida.PublicId, usuario, ct);
        return Result.Success();
    }

    /// <summary>Lo que pasó con la línea del giro: el recibo, o el saldo de la pagaduría con su alerta (o, al reprocesar, la baja de ese saldo).</summary>
    private async Task AnotarAsync(PaymasterRemittance giro, PaymasterRemittanceLine l, PayrollDeductionFileLine linea, ResultadoNoInteractivo resultado, int usuario,
        DateTime ahora, CancellationToken ct)
    {
        if (resultado.Outcome is ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied && resultado.Receipt is { } recibo)
        {
            l.Outcome = PaymasterRemittanceLineOutcome.Applied;
            l.ReceiptId = recibo.Id;
            l.RejectionCode = null;
            l.RejectionMessage = null;
            if (l.CreditBalanceId is { } previo && await db.PayerCreditBalances.FirstOrDefaultAsync(x => x.Id == previo, ct) is { } saldoPrevio && saldoPrevio.Balance >= l.Amount)
                SaldosAFavor.Bajar(db, saldoPrevio, PayerCreditBalanceMovementKind.AppliedByReceipt, l.Amount, recibo.PublicId, recibo.Number, usuario, ahora,
                    "Aplicada al reprocesar la línea del giro.");
            return;
        }
        if (resultado.Outcome != ProcessingLineOutcome.Rejected) return;
        l.Outcome = PaymasterRemittanceLineOutcome.Rejected;
        l.RejectionCode = resultado.Code;
        l.RejectionMessage = resultado.Message is { Length: > 500 } m ? m[..500] : resultado.Message;
        if (l.CreditBalanceId is not null) return;
        var saldo = SaldosAFavor.Reconocer(db, giro.PaymasterPersonId, PayerCreditBalanceOrigin.PaymasterUnapplied, giro.PublicId, giro.Number, giro.BranchId, l.Amount,
            reloj.HoyLocal, usuario, ahora, giro.OwnBankAccountPublicId);
        saldo.Kind = PayerCreditBalanceKind.Paymaster;
        await db.SaveChangesAsync(ct);
        SaldosAFavor.Reconocido(db, saldo, usuario, ahora);
        l.CreditBalanceId = saldo.Id;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoNoAplicadoDePagaduria, "Línea del giro de una pagaduría sin aplicar",
            string.Create(CultureInfo.InvariantCulture,
                $"El contrato rechazó {l.Amount:N0} del giro {giro.Number} (crédito o cuenta {linea.ProductNumber}): quedó como saldo de la pagaduría. {resultado.Message}"),
            "PaymasterRemittanceLine", l.PublicId, DedupKey: $"Recaudo.NoAplicadoDePagaduria:{l.PublicId:N}", ModuloDeOrigen: "Receipts",
            ScopeBranchPublicId: await db.Branches.AsNoTracking().Where(b => b.Id == giro.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)), ct);
    }

    private async Task EstadoAsync(PaymasterRemittance giro, CancellationToken ct)
    {
        var estados = await db.PaymasterRemittanceLines.Where(l => l.PaymasterRemittanceId == giro.Id).Select(l => l.Outcome).ToListAsync(ct);
        giro.Status = estados.All(e => e == PaymasterRemittanceLineOutcome.Applied) ? PaymasterRemittanceState.Applied
            : estados.Any(e => e == PaymasterRemittanceLineOutcome.Pending) ? PaymasterRemittanceState.Applying
            : PaymasterRemittanceState.PartiallyApplied;
    }

    public static async Task<PaymasterRemittanceDto> DtoAsync(IApplicationDbContext db, PaymasterRemittance g, CancellationToken ct)
    {
        var pagaduria = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == g.PaymasterPersonId)
            .Select(p => new { p.PublicId, Nombre = p.BusinessName ?? (p.FirstName + " " + p.LastName) }).FirstAsync(ct);
        var lineas = await (from l in db.PaymasterRemittanceLines.AsNoTracking()
                            join d in db.ReceiptsPayrollDeductionFileLines.AsNoTracking() on l.PayrollDeductionFileLineId equals d.Id
                            join p in db.People.AsNoTracking().IgnoreQueryFilters() on d.PersonId equals p.Id
                            where l.PaymasterRemittanceId == g.Id
                            orderby l.Order
                            select new { l, d.ProductNumber, Nombre = (p.FirstName + " " + p.LastName).Trim() }).ToListAsync(ct);
        var recibos = lineas.Where(x => x.l.ReceiptId != null).Select(x => x.l.ReceiptId!.Value).ToList();
        var numeros = await db.IssuedReceipts.AsNoTracking().Where(r => recibos.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Number, ct);
        var saldos = lineas.Where(x => x.l.CreditBalanceId != null).Select(x => x.l.CreditBalanceId!.Value).ToList();
        var deSaldos = await db.PayerCreditBalances.AsNoTracking().Where(s => saldos.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.OriginDocumentNumber, ct);
        var asignado = lineas.Sum(x => x.l.Amount);
        var ultima = await db.ProcessingRuns.AsNoTracking().Where(r => r.SourcePublicId == g.PublicId && r.Kind == ProcessingRunKind.PaymasterRemittance)
            .OrderByDescending(r => r.Id).Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct);
        return new PaymasterRemittanceDto(g.PublicId, g.Number, pagaduria.PublicId, pagaduria.Nombre.Trim(), g.Source, g.BankStatementLinePublicId, g.Amount, g.RemittedOn,
            g.Status, g.AllocationOrder, asignado, g.Amount - asignado, lineas.Count, lineas.Count(x => x.l.Outcome == PaymasterRemittanceLineOutcome.Applied),
            lineas.Count(x => x.l.Outcome == PaymasterRemittanceLineOutcome.Rejected), lineas.Count(x => x.l.Outcome == PaymasterRemittanceLineOutcome.Pending), ultima,
            lineas.Select(x => new PaymasterRemittanceLineDto(x.l.PublicId, x.l.Order, x.l.DeductionFileLinePublicId, x.ProductNumber, x.Nombre, x.l.Amount, x.l.IdempotencyKey,
                x.l.Outcome, x.l.ReceiptId is { } r && numeros.TryGetValue(r, out var n) ? n : null, x.l.RejectionCode, x.l.RejectionMessage,
                x.l.CreditBalanceId is { } s && deSaldos.TryGetValue(s, out var sn) ? sn : null)).ToList());
    }
}
