using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Agreements;

// Feature 015, R2 (fase 38, T0434; FR-093, FR-218; US16-3): la conciliación neta del archivo de un convenio o de una pasarela con su abono en la cuenta recaudadora. La
// diferencia entre lo informado y lo abonado se explica con la comisión, su IVA y cada retención con su tercero; lo que queda sin explicar por encima de
// Convenios.ToleranciaDeConciliacion exige la aprobación de otra persona. El abono queda identificado en Recaudo (AgreementSettlement), así la conciliación de Tesorería
// lo ve como la liquidación del convenio y no vuelve a identificarse; el hecho ComisionDeConvenioRegistrada nace al quedar explicada o aprobada.

/// <summary>Una retención pedida: su clase, su valor y su tercero (nulo, el aliado del convenio). (nuevo)</summary>
public sealed record RetencionPedida(string Kind, decimal Amount, Guid? ThirdPartyPersonPublicId = null);

/// <summary>Una conciliación de convenio. (nuevo)</summary>
public sealed record AgreementReconciliationDto(Guid PublicId, string Number, Guid AgreementPublicId, string AgreementCode, Guid? FilePublicId, string? FileNumber,
    Guid StatementLinePublicId, DateOnly CreditedOn, decimal InformedAmount, decimal CreditedAmount, decimal CommissionAmount, decimal VatAmount, decimal WithholdingsAmount,
    IReadOnlyList<RetencionPedida> Withholdings, decimal UnexplainedAmount, decimal Tolerance, AgreementReconciliationState Status, DateTime RegisteredAt,
    DateTime? ApprovedAt, string? Note);

/// <summary>Lo común de la conciliación del convenio. (nuevo)</summary>
public static class ConciliacionesDeConvenio
{
    public const string ClaseDeNumeracion = "AC";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Serializar(IReadOnlyList<RetencionPracticada> retenciones) => JsonSerializer.Serialize(retenciones, Json);

    public static async Task<AgreementReconciliationDto> DtoAsync(IApplicationDbContext db, AgreementReconciliation r, CancellationToken ct)
    {
        var convenio = await db.CollectionAgreements.AsNoTracking().IgnoreQueryFilters().FirstAsync(c => c.Id == r.CollectionAgreementId, ct);
        var archivo = r.AgreementFileId is { } a
            ? await db.AgreementFiles.AsNoTracking().Where(f => f.Id == a).Select(f => new { f.PublicId, f.Number }).FirstOrDefaultAsync(ct)
            : r.GatewayFileId is { } g
                ? await db.GatewayFiles.AsNoTracking().Where(f => f.Id == g).Select(f => new { f.PublicId, f.Number }).FirstOrDefaultAsync(ct)
                : null;
        var retenciones = EmisionDeRecaudo.Retenciones(r).Select(x => new RetencionPedida(x.Clase, x.Valor, x.TerceroPublicId)).ToList();
        return new AgreementReconciliationDto(r.PublicId, r.Number, convenio.PublicId, convenio.Code, archivo?.PublicId, archivo?.Number, r.StatementLinePublicId, r.CreditedOn,
            r.InformedAmount, r.CreditedAmount, r.CommissionAmount, r.VatAmount, r.WithholdingsAmount, retenciones, r.UnexplainedAmount, r.Tolerance, r.Status, r.RegisteredAt,
            r.ApprovedAt, r.Note);
    }
}

/// <summary>
/// <c>POST /api/receipts/collection-agreements/{id}/reconciliations</c> (<c>Receipts.Agreements.Manage</c>, con clave; FR-093, FR-218): concilia un archivo cargado
/// (<see cref="FilePublicId"/>, de convenio o de pasarela) con el abono del extracto de la cuenta recaudadora que lo liquidó. (nuevo)
/// </summary>
public sealed record ReconcileCollectionFileCommand(
    Guid AgreementPublicId,
    Guid FilePublicId,
    Guid StatementLinePublicId,
    decimal Commission,
    decimal Vat,
    IReadOnlyList<RetencionPedida>? Withholdings = null,
    string? Note = null) : IRequest<Result<AgreementReconciliationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReconcileCollectionFileCommandValidator : AbstractValidator<ReconcileCollectionFileCommand>
{
    public ReconcileCollectionFileCommandValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty();
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.StatementLinePublicId).NotEmpty().WithMessage("Elija el abono del extracto que liquidó el archivo.");
        RuleFor(x => x.Commission).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Vat).GreaterThanOrEqualTo(0m);
        RuleForEach(x => x.Withholdings).ChildRules(r =>
        {
            r.RuleFor(w => w.Kind).NotEmpty().MaximumLength(40);
            r.RuleFor(w => w.Amount).GreaterThan(0m);
        }).When(x => x.Withholdings is not null);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class ReconcileCollectionFileCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeRecaudo alcance,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    IMovimientosBancariosParaRecaudo movimientos,
    ParametrosDeCaja parametros,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ReconcileCollectionFileCommand, Result<AgreementReconciliationDto>>
{
    public async Task<Result<AgreementReconciliationDto>> Handle(ReconcileCollectionFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AgreementReconciliationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var convenio = await db.CollectionAgreements.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.AgreementPublicId, ct);
        if (convenio is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(convenio.BranchId))
            return Result.Failure<AgreementReconciliationDto>(ErroresDeRecaudo.NoEncontrado("El convenio"));
        var pasarela = convenio.Kind == Domain.Enums.Receipts.CollectionAgreementKind.Gateway;
        CollectionFileBase? archivo = pasarela
            ? await db.GatewayFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.FilePublicId && f.CollectionAgreementId == convenio.Id, ct)
            : await db.AgreementFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.FilePublicId && f.CollectionAgreementId == convenio.Id, ct);
        if (archivo is null) return Result.Failure<AgreementReconciliationDto>(ErroresDeRecaudo.NoEncontrado("El archivo del convenio"));
        if (await db.AgreementReconciliations.AnyAsync(r => pasarela ? r.GatewayFileId == archivo.Id : r.AgreementFileId == archivo.Id, ct))
            return Result.Failure<AgreementReconciliationDto>(ErroresDeConvenios.ArchivoYaConciliado());
        var linea = await movimientos.LineaAsync(request.StatementLinePublicId, ct);
        if (linea is null || linea.OwnBankAccountPublicId != convenio.OwnBankAccountPublicId)
            return Result.Failure<AgreementReconciliationDto>(ErroresDeConvenios.AbonoDeOtraCuenta());
        var pendiente = linea.Amount - linea.Identified;
        if (pendiente <= 0m) return Result.Failure<AgreementReconciliationDto>(ErroresDeRecaudo.AbonoYaIdentificado());

        var hoy = reloj.HoyLocal;
        var tolerancia = await parametros.DecimalAsync(ParametrosDeRecaudoYTesoreria.ConveniosToleranciaDeConciliacion, hoy, ct) ?? 0m;
        var aliado = convenio.PartnerPersonId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == p).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var retenciones = (request.Withholdings ?? []).Select(w => new RetencionPracticada(w.Kind.Trim(), w.Amount, w.ThirdPartyPersonPublicId ?? aliado)).ToList();
        // Lo informado es lo que el archivo aplicó o dejó en partidas (lo que el aliado recaudó); las no aprobadas y las omitidas no son dinero recibido.
        var informado = archivo.TotalApplied + archivo.TotalUnidentified;
        var explicacion = ConciliacionNeta.Explicar(informado, pendiente, request.Commission, request.Vat, retenciones, tolerancia);

        await cerrojo.TomarSetupAsync(ct: ct);
        var conciliacion = new AgreementReconciliation
        {
            Number = await numerador.AsignarAsync(ConciliacionesDeConvenio.ClaseDeNumeracion, ConciliacionesDeConvenio.ClaseDeNumeracion, ct),
            CollectionAgreementId = convenio.Id, AgreementFileId = pasarela ? null : archivo.Id, GatewayFileId = pasarela ? archivo.Id : null,
            StatementLinePublicId = linea.PublicId, CreditedOn = linea.MovementDate, InformedAmount = informado, CreditedAmount = pendiente,
            CommissionAmount = request.Commission, VatAmount = request.Vat, WithholdingsAmount = explicacion.Retenciones,
            WithholdingsJson = ConciliacionesDeConvenio.Serializar(retenciones), UnexplainedAmount = explicacion.SinExplicar, Tolerance = explicacion.Tolerancia,
            Status = explicacion.ExigeAprobacion ? AgreementReconciliationState.PendingApproval : AgreementReconciliationState.Explained,
            RegisteredByUserId = usuario, RegisteredAt = reloj.UtcNow, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        db.AgreementReconciliations.Add(conciliacion);
        db.BankItemIdentifications.Add(new BankItemIdentification
        {
            BankStatementLinePublicId = linea.PublicId, Sequence = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct),
            OwnBankAccountPublicId = linea.OwnBankAccountPublicId, MovementDate = linea.MovementDate, Amount = pendiente, Outcome = BankItemOutcome.AgreementSettlement,
            IdentifiedByUserId = usuario, IdentifiedAt = reloj.UtcNow, Note = string.Create(CultureInfo.InvariantCulture, $"Convenio {convenio.Code}, archivo {archivo.Number}"),
        });
        await movimientos.MarcarIdentificacionAsync(linea.PublicId, linea.Identified + pendiente, ct);
        await db.SaveChangesAsync(ct);
        if (conciliacion.Status == AgreementReconciliationState.Explained)
            await emision.ComisionDeConvenioAsync(conciliacion, convenio, archivo.Number, archivo.PublicId, retenciones, null, ct);
        await auditoria.AgregarAsync("Receipts.AgreementReconciliation.Registered", "AgreementReconciliation", conciliacion.PublicId, null,
            new { number = conciliacion.Number, agreement = convenio.Code, file = archivo.Number, informed = informado, credited = pendiente, commission = request.Commission,
                vat = request.Vat, withholdings = explicacion.Retenciones, unexplained = explicacion.SinExplicar, status = conciliacion.Status.ToString(), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionesDeConvenio.DtoAsync(db, conciliacion, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/collection-agreements/reconciliations/{id}/approve</c> (<c>Receipts.Agreements.Manage</c>, con clave; FR-093): otra persona aprueba la diferencia
/// sin explicar de una conciliación sobre la tolerancia; nace su hecho. (nuevo)
/// </summary>
public sealed record ApproveAgreementReconciliationCommand(Guid ReconciliationPublicId, string? Note = null)
    : IRequest<Result<AgreementReconciliationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveAgreementReconciliationCommandValidator : AbstractValidator<ApproveAgreementReconciliationCommand>
{
    public ApproveAgreementReconciliationCommandValidator()
    {
        RuleFor(x => x.ReconciliationPublicId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class ApproveAgreementReconciliationCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeRecaudo alcance,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ApproveAgreementReconciliationCommand, Result<AgreementReconciliationDto>>
{
    public async Task<Result<AgreementReconciliationDto>> Handle(ApproveAgreementReconciliationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AgreementReconciliationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var conciliacion = await db.AgreementReconciliations.FirstOrDefaultAsync(r => r.PublicId == request.ReconciliationPublicId, ct);
        if (conciliacion is null) return Result.Failure<AgreementReconciliationDto>(ErroresDeRecaudo.NoEncontrado("La conciliación"));
        var convenio = await db.CollectionAgreements.AsNoTracking().FirstAsync(c => c.Id == conciliacion.CollectionAgreementId, ct);
        if (!(await alcance.ObtenerAsync(ct)).IncluyeOficina(convenio.BranchId)) return Result.Failure<AgreementReconciliationDto>(ErroresDeRecaudo.NoEncontrado("La conciliación"));
        if (conciliacion.Status == AgreementReconciliationState.Approved && conciliacion.ApprovedByUserId == usuario)
            return Result.Success(await ConciliacionesDeConvenio.DtoAsync(db, conciliacion, ct));
        if (conciliacion.Status != AgreementReconciliationState.PendingApproval) return Result.Failure<AgreementReconciliationDto>(ErroresDeConvenios.NoPorAprobar());
        if (conciliacion.RegisteredByUserId == usuario) return Result.Failure<AgreementReconciliationDto>(ErroresDeConvenios.AutoAprobacion());
        (conciliacion.Status, conciliacion.ApprovedByUserId, conciliacion.ApprovedAt) = (AgreementReconciliationState.Approved, usuario, reloj.UtcNow);
        if (!string.IsNullOrWhiteSpace(request.Note)) conciliacion.Note = string.Join(" · ", new[] { conciliacion.Note, request.Note.Trim() }.Where(s => !string.IsNullOrEmpty(s)));
        var (numero, archivo) = conciliacion.AgreementFileId is { } a
            ? await db.AgreementFiles.AsNoTracking().Where(f => f.Id == a).Select(f => new ValueTuple<string?, Guid?>(f.Number, f.PublicId)).FirstOrDefaultAsync(ct)
            : await db.GatewayFiles.AsNoTracking().Where(f => f.Id == conciliacion.GatewayFileId).Select(f => new ValueTuple<string?, Guid?>(f.Number, f.PublicId))
                .FirstOrDefaultAsync(ct);
        await emision.ComisionDeConvenioAsync(conciliacion, convenio, numero, archivo, EmisionDeRecaudo.Retenciones(conciliacion), usuario, ct);
        await auditoria.AgregarAsync("Receipts.AgreementReconciliation.Approved", "AgreementReconciliation", conciliacion.PublicId, null,
            new { number = conciliacion.Number, unexplained = conciliacion.UnexplainedAmount, tolerance = conciliacion.Tolerance, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionesDeConvenio.DtoAsync(db, conciliacion, ct));
    }
}

/// <summary><c>GET /api/receipts/collection-agreements/reconciliations?agreement=&amp;state=</c> (<c>Receipts.Agreements.View</c>). (nuevo)</summary>
public sealed record ListAgreementReconciliationsQuery(Guid? AgreementPublicId = null, AgreementReconciliationState? State = null)
    : IRequest<Result<IReadOnlyList<AgreementReconciliationDto>>>;

public sealed class ListAgreementReconciliationsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListAgreementReconciliationsQuery, Result<IReadOnlyList<AgreementReconciliationDto>>>
{
    public async Task<Result<IReadOnlyList<AgreementReconciliationDto>>> Handle(ListAgreementReconciliationsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var convenios = (await db.CollectionAgreements.AsNoTracking().ToListAsync(ct))
            .Where(c => mio.IncluyeOficina(c.BranchId) && (request.AgreementPublicId is null || c.PublicId == request.AgreementPublicId)).Select(c => c.Id).ToList();
        var consulta = db.AgreementReconciliations.AsNoTracking().Where(r => convenios.Contains(r.CollectionAgreementId));
        if (request.State is { } s) consulta = consulta.Where(r => r.Status == s);
        var filas = await consulta.OrderByDescending(r => r.Id).Take(200).ToListAsync(ct);
        var lista = new List<AgreementReconciliationDto>(filas.Count);
        foreach (var r in filas) lista.Add(await ConciliacionesDeConvenio.DtoAsync(db, r, ct));
        return Result.Success<IReadOnlyList<AgreementReconciliationDto>>(lista);
    }
}
