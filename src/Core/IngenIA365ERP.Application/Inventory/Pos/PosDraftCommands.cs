using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

// La venta del POS mientras es borrador (feature 012, I3, T603 y T605; contracts/api.md §20.2; FR-057, FR-058, FR-059; T50, F13).
// Todos los comandos llevan clave de operación (IOperacionIdempotente) y canal pos (IOperacionDePuntoDeVenta). Las acciones de riesgo
// —quitar, descuento, precio manual, suspender, recuperar, descartar— dejan además su evento con nombre (IAuditoriaDelPuntoDeVenta);
// leer un producto no.

/// <summary>La primera lectura que abre la venta: código de barras o de producto, con «3*» o cantidad. (nuevo)</summary>
public sealed record PosFirstLineInput(string Code, decimal? Quantity = null);

/// <summary>
/// Abre la venta en la sesión abierta del usuario (<c>POST /pos/drafts</c>): tipo del rol <c>PosSale</c> de la caja, canal y sucursal
/// del punto, bodega de la caja, fecha operativa de la sesión y el consumidor final por defecto. Punto sin POS →
/// <c>Inventory.Pos.NotEnabled</c> (T605). (nuevo)
/// </summary>
public sealed record CreatePosDraftCommand(Guid CashSessionPublicId, PosFirstLineInput? FirstLine = null)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePosDraftCommandValidator : AbstractValidator<CreatePosDraftCommand>
{
    public CreatePosDraftCommandValidator() => RuleFor(x => x.CashSessionPublicId).NotEmpty();
}

public sealed class CreatePosDraftCommandHandler(IApplicationDbContext db, BorradorDelPos pos)
    : IRequestHandler<CreatePosDraftCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(CreatePosDraftCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var sesion = await pos.SesionAbiertaAsync(request.CashSessionPublicId, exigirPos: true, ct);
            if (sesion.IsFailure) return Result.Failure<PosDraftDto>(sesion.Error);
            var (s, caja, punto) = sesion.Value;
            var tipo = BorradorDelPos.TipoDelRol(caja, CashRegisterDocumentRole.PosSale);
            if (tipo.IsFailure) return Result.Failure<PosDraftDto>(tipo.Error);
            var usuario = await pos.UsuarioAsync(ct);

            var venta = new InventoryDocument
            {
                Class = tipo.Value.Class,
                DocumentTypeId = tipo.Value.Id,
                DocumentType = tipo.Value,
                OperationDate = s.OperatingDate,
                CreatedByUserId = usuario.Value.UserId,
                WarehouseId = caja.WarehouseId,
                BranchId = punto.BranchId,
                SalesChannelId = punto.SalesChannelId,
                PointOfSaleId = punto.Id,
                CashRegisterId = caja.Id,
                CashSessionId = s.Id,
                CounterpartyPersonId = await pos.ConsumidorFinalAsync(s.OperatingDate, ct),
            };
            db.InventoryDocuments.Add(venta);
            await db.SaveChangesAsync(ct);

            int? ultima = null;
            VentaPrecificada? precificada = null;
            if (request.FirstLine is { } primera && !string.IsNullOrWhiteSpace(primera.Code))
            {
                var agregada = await PosDraftComun.AgregarPorCodigoAsync(pos, venta, primera.Code, primera.Quantity, ct);
                if (agregada.IsFailure) return Result.Failure<PosDraftDto>(agregada.Error);
                ultima = agregada.Value.LineNumber;
                var r = await pos.PrecificarAsync(venta, null, resolverListas: false, null, aplicar: true, ct);
                if (r.IsFailure) return Result.Failure<PosDraftDto>(r.Error);
                precificada = r.Value;
                await db.SaveChangesAsync(ct);
                await pos.EnlazarAprobacionesAsync(venta, ct);
            }
            return Result.Success(await pos.DtoAsync(venta, precificada, ultima, null, ct));
        }, ct);
}

/// <summary>
/// Cambia la cabecera (<c>PATCH /pos/drafts/{id}</c>): cliente (re-precifica), vendedor validado como persona con rol vivo en
/// <c>INV_Salespeople</c> (FR-057, <c>Inventory.Sales.SalespersonInvalid</c>), rol del documento (<c>InvoiceOnRequest</c> exige un cliente
/// identificado: <c>Inventory.Pos.InvoiceRequiresCustomer</c>), descuento por total y notas. (nuevo)
/// </summary>
public sealed record UpdatePosDraftCommand(
    Guid DraftPublicId,
    Guid? CustomerPersonPublicId = null,
    bool ClearCustomer = false,
    Guid? SalespersonPublicId = null,
    bool ClearSalesperson = false,
    CashRegisterDocumentRole? Role = null,
    PosDiscountInput? DocumentDiscount = null,
    string? Notes = null)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }

    public Guid CashSessionPublicId { get; init; }
}

public sealed class UpdatePosDraftCommandValidator : AbstractValidator<UpdatePosDraftCommand>
{
    public UpdatePosDraftCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x.Role).Must(r => r is null or CashRegisterDocumentRole.PosSale or CashRegisterDocumentRole.InvoiceOnRequest)
            .WithMessage("El rol de la venta es PosSale o InvoiceOnRequest.");
        RuleFor(x => x.DocumentDiscount!.Percent).InclusiveBetween(0m, 1m).When(x => x.DocumentDiscount?.Percent is not null);
        RuleFor(x => x.DocumentDiscount!.Amount).GreaterThanOrEqualTo(0m).When(x => x.DocumentDiscount?.Amount is not null);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class UpdatePosDraftCommandHandler(IApplicationDbContext db, BorradorDelPos pos, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<UpdatePosDraftCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(UpdatePosDraftCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cargada = await PosDraftComun.CargarDeLaSesionAsync(pos, request.DraftPublicId, ct);
            if (cargada.IsFailure) return Result.Failure<PosDraftDto>(cargada.Error);
            var (venta, sesion) = cargada.Value;
            var resolver = false;

            if (request.ClearCustomer)
            {
                venta.CounterpartyPersonId = await pos.ConsumidorFinalAsync(venta.OperationDate, ct);
                resolver = true;
            }
            else if (request.CustomerPersonPublicId is { } cliente)
            {
                var id = await db.People.AsNoTracking().Where(p => p.PublicId == cliente).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
                if (id is null) return Result.Failure<PosDraftDto>(ErroresDelPos.CustomerNotFound());
                resolver = venta.CounterpartyPersonId != id;
                venta.CounterpartyPersonId = id;
            }

            if (request.ClearSalesperson) venta.SalespersonId = null;
            else if (request.SalespersonPublicId is { } vendedor)
            {
                var vivo = await PosDraftComun.VendedorVivoAsync(db, vendedor, ct);
                if (vivo is null) return Result.Failure<PosDraftDto>(ErroresDelPos.SalespersonInvalid());
                venta.SalespersonId = vivo;
            }

            var rol = request.Role;
            var rolActual = sesion.Caja.DocumentTypes.FirstOrDefault(t => t.DocumentTypeId == venta.DocumentTypeId)?.Role ?? CashRegisterDocumentRole.PosSale;
            if ((rol ?? rolActual) == CashRegisterDocumentRole.InvoiceOnRequest && await pos.EsConsumidorFinalAsync(venta, ct))
                return Result.Failure<PosDraftDto>(ErroresDelPos.InvoiceRequiresCustomer());
            if (rol is { } nuevo && nuevo != rolActual)
            {
                var tipo = BorradorDelPos.TipoDelRol(sesion.Caja, nuevo);
                if (tipo.IsFailure) return Result.Failure<PosDraftDto>(tipo.Error);
                venta.DocumentTypeId = tipo.Value.Id;
                venta.DocumentType = tipo.Value;
                venta.Class = tipo.Value.Class;
            }
            if (request.Notes is not null) venta.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            decimal? porTotal = null;
            if (request.DocumentDiscount is { } d)
            {
                var base_ = venta.Lines.Where(l => !l.IsDeleted).Sum(l => l.GrossAmount)
                    - await db.DocumentLineDiscounts.AsNoTracking().Where(x => x.DocumentId == venta.Id && !x.IsDeleted && !x.FromDocumentDiscount).SumAsync(x => x.Amount, ct);
                porTotal = d.Amount is { } monto && monto > 0m ? monto
                    : d.Percent is { } pct && pct > 0m ? Math.Round(base_ * pct, 2, MidpointRounding.AwayFromZero) : 0m;
            }

            var r = await pos.PrecificarAsync(venta, null, resolver, porTotal, aplicar: true, ct);
            if (r.IsFailure) return Result.Failure<PosDraftDto>(r.Error);
            if (request.DocumentDiscount is not null)
                await auditoria.AnotarAsync(AuditEventTypes.InventoryPosDiscountApplied, request, venta.PublicId,
                    new { documentDiscount = request.DocumentDiscount, amount = porTotal }, ct);
            await db.SaveChangesAsync(ct);
            await pos.EnlazarAprobacionesAsync(venta, ct);
            return Result.Success(await pos.DtoAsync(venta, r.Value, null, null, ct));
        }, ct);
}

/// <summary>
/// Una lectura del lector (<c>POST /pos/drafts/{id}/lines</c>): por código (el de empaque trae su unidad; «3*» multiplica) o por
/// producto y unidad. Si la venta ya tiene el producto en la misma unidad, sin precio digitado ni descuento, suma cantidad. No emite
/// evento propio: las lecturas no se auditan una a una (F13). (nuevo)
/// </summary>
public sealed record AddPosLineCommand(Guid DraftPublicId, string? Code = null, Guid? ProductPublicId = null, Guid? UnitPublicId = null, decimal? Quantity = null)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }

    public Guid CashSessionPublicId { get; init; }
}

public sealed class AddPosLineCommandValidator : AbstractValidator<AddPosLineCommand>
{
    public AddPosLineCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Code) || x.ProductPublicId is not null)
            .WithMessage("Lea un código o elija el producto.");
        RuleFor(x => x.Quantity).GreaterThan(0m).When(x => x.Quantity is not null);
        RuleFor(x => x.Code).MaximumLength(60);
    }
}

public sealed class AddPosLineCommandHandler(IApplicationDbContext db, BorradorDelPos pos)
    : IRequestHandler<AddPosLineCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(AddPosLineCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cargada = await PosDraftComun.CargarDeLaSesionAsync(pos, request.DraftPublicId, ct);
            if (cargada.IsFailure) return Result.Failure<PosDraftDto>(cargada.Error);
            var venta = cargada.Value.Venta;

            Result<(int LineNumber, bool Sumada)> agregada;
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                agregada = await PosDraftComun.AgregarPorCodigoAsync(pos, venta, request.Code, request.Quantity, ct);
            }
            else
            {
                var producto = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
                if (producto is null) return Result.Failure<PosDraftDto>(ErroresDelPos.ProductNotFound(request.ProductPublicId.ToString()!));
                var unidad = request.UnitPublicId is { } u
                    ? await db.UnitsOfMeasure.AsNoTracking().Where(x => x.PublicId == u).Select(x => new { x.Id, x.PublicId }).FirstOrDefaultAsync(ct)
                    : await db.UnitsOfMeasure.AsNoTracking().Where(x => x.Id == producto.BaseUnitId).Select(x => new { x.Id, x.PublicId }).FirstOrDefaultAsync(ct);
                if (unidad is null) return Result.Failure<PosDraftDto>(InventoryErrors.UnitNotForProduct(0, producto.Code, string.Empty));
                agregada = await pos.AgregarAsync(venta, producto.Id, unidad.Id, unidad.PublicId, request.Quantity ?? 1m, ct);
            }
            if (agregada.IsFailure) return Result.Failure<PosDraftDto>(agregada.Error);

            var cambios = agregada.Value.Sumada
                ? new Dictionary<int, CambioDeLinea> { [agregada.Value.LineNumber] = new CambioDeLinea(Quantity: 0m) }
                : null;
            var r = await pos.PrecificarAsync(venta, cambios, resolverListas: false, null, aplicar: true, ct);
            if (r.IsFailure) return Result.Failure<PosDraftDto>(r.Error);
            await db.SaveChangesAsync(ct);
            await pos.EnlazarAprobacionesAsync(venta, ct);
            return Result.Success(await pos.DtoAsync(venta, r.Value, agregada.Value.LineNumber, null, ct));
        }, ct);
}

/// <summary>
/// Cambia una línea (<c>PATCH /pos/drafts/{id}/lines/{lineId}</c>): cantidad, precio manual (en la base de la lista) o descuento
/// (<c>percent</c> como fracción o <c>amount</c> en pesos). El precio manual y el descuento se auditan uno a uno; sobre el tope, piden
/// aprobación (§19.3) y cambiar la línea deja sin efecto la que había. (nuevo)
/// </summary>
public sealed record UpdatePosLineCommand(Guid DraftPublicId, Guid LinePublicId, decimal? Quantity = null, decimal? UnitPrice = null, PosDiscountInput? Discount = null,
    bool ClearUnitPrice = false)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }

    public Guid CashSessionPublicId { get; init; }
}

public sealed class UpdatePosLineCommandValidator : AbstractValidator<UpdatePosLineCommand>
{
    public UpdatePosLineCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0m).When(x => x.Quantity is not null);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0m).When(x => x.UnitPrice is not null);
        RuleFor(x => x.Discount!.Percent).InclusiveBetween(0m, 1m).When(x => x.Discount?.Percent is not null);
        RuleFor(x => x.Discount!.Amount).GreaterThanOrEqualTo(0m).When(x => x.Discount?.Amount is not null);
    }
}

public sealed class UpdatePosLineCommandHandler(IApplicationDbContext db, BorradorDelPos pos, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<UpdatePosLineCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(UpdatePosLineCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cargada = await PosDraftComun.CargarDeLaSesionAsync(pos, request.DraftPublicId, ct);
            if (cargada.IsFailure) return Result.Failure<PosDraftDto>(cargada.Error);
            var venta = cargada.Value.Venta;
            var linea = venta.Lines.FirstOrDefault(l => l.PublicId == request.LinePublicId && !l.IsDeleted);
            if (linea is null) return Result.Failure<PosDraftDto>(ErroresDelPos.LineNotFound());

            if (request.Quantity is { } cantidad && cantidad != linea.Quantity)
            {
                var cambiada = await pos.CambiarCantidadAsync(linea, cantidad, ct);
                if (cambiada.IsFailure) return Result.Failure<PosDraftDto>(cambiada.Error);
            }
            var cambio = new CambioDeLinea(request.Quantity, request.UnitPrice, request.Discount, request.ClearUnitPrice);
            var r = await pos.PrecificarAsync(venta, new Dictionary<int, CambioDeLinea> { [linea.LineNumber] = cambio }, resolverListas: false, null, aplicar: true, ct);
            if (r.IsFailure) return Result.Failure<PosDraftDto>(r.Error);

            var datos = new { lineNumber = linea.LineNumber, linePublicId = linea.PublicId, productId = linea.ProductId, linea.Quantity, linea.ListPrice };
            if (request.UnitPrice is not null || request.ClearUnitPrice)
                await auditoria.AnotarAsync(AuditEventTypes.InventoryPosPriceOverridden, request, venta.PublicId, new { datos, unitPrice = request.UnitPrice }, ct);
            if (request.Discount is not null)
                await auditoria.AnotarAsync(AuditEventTypes.InventoryPosDiscountApplied, request, venta.PublicId, new { datos, discount = request.Discount }, ct);
            await db.SaveChangesAsync(ct);
            await pos.EnlazarAprobacionesAsync(venta, ct);
            return Result.Success(await pos.DtoAsync(venta, r.Value, linea.LineNumber, null, ct));
        }, ct);
}

/// <summary>Quita una línea (<c>DELETE /pos/drafts/{id}/lines/{lineId}</c>): baja lógica, auditada (FR-059). (nuevo)</summary>
public sealed record RemovePosLineCommand(Guid DraftPublicId, Guid LinePublicId)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }

    public Guid CashSessionPublicId { get; init; }
}

public sealed class RemovePosLineCommandValidator : AbstractValidator<RemovePosLineCommand>
{
    public RemovePosLineCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x.LinePublicId).NotEmpty();
    }
}

public sealed class RemovePosLineCommandHandler(IApplicationDbContext db, BorradorDelPos pos, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<RemovePosLineCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(RemovePosLineCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cargada = await PosDraftComun.CargarDeLaSesionAsync(pos, request.DraftPublicId, ct);
            if (cargada.IsFailure) return Result.Failure<PosDraftDto>(cargada.Error);
            var venta = cargada.Value.Venta;
            var linea = venta.Lines.FirstOrDefault(l => l.PublicId == request.LinePublicId && !l.IsDeleted);
            if (linea is null) return Result.Failure<PosDraftDto>(ErroresDelPos.LineNotFound());

            await pos.QuitarLineaAsync(venta, linea, ct);
            var r = await pos.PrecificarAsync(venta, null, resolverListas: false, null, aplicar: true, ct);
            if (r.IsFailure) return Result.Failure<PosDraftDto>(r.Error);
            await auditoria.AnotarAsync(AuditEventTypes.InventoryPosLineRemoved, request, venta.PublicId,
                new { lineNumber = linea.LineNumber, linePublicId = linea.PublicId, productId = linea.ProductId, linea.Quantity, linea.NetAmount }, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await pos.DtoAsync(venta, r.Value, null, null, ct));
        }, ct);
}

/// <summary>Suspende la venta con un rótulo (<c>POST /pos/drafts/{id}/suspend</c>); cualquier cajero del punto la recupera. (nuevo)</summary>
public sealed record SuspendPosDraftCommand(Guid DraftPublicId, string? Label)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }

    public Guid CashSessionPublicId { get; init; }
}

public sealed class SuspendPosDraftCommandValidator : AbstractValidator<SuspendPosDraftCommand>
{
    public SuspendPosDraftCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x.Label).MaximumLength(60);
    }
}

public sealed class SuspendPosDraftCommandHandler(IApplicationDbContext db, BorradorDelPos pos, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<SuspendPosDraftCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(SuspendPosDraftCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cargada = await PosDraftComun.CargarDeLaSesionAsync(pos, request.DraftPublicId, ct);
            if (cargada.IsFailure) return Result.Failure<PosDraftDto>(cargada.Error);
            var venta = cargada.Value.Venta;
            venta.IsSuspended = true;
            venta.SuspendedAt = pos.Reloj.UtcNow;
            venta.SuspendedLabel = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
            await auditoria.AnotarAsync(AuditEventTypes.InventoryPosSuspended, request, venta.PublicId, new { label = venta.SuspendedLabel, venta.Total }, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await pos.DtoAsync(venta, null, null, null, ct));
        }, ct);
}

/// <summary>
/// Recupera una venta suspendida en la sesión abierta del usuario, en cualquier caja del mismo punto (<c>POST /pos/drafts/{id}/resume</c>).
/// Si la fecha operativa cambió, se vuelve a precificar con las listas del día y avisa <c>Inventory.Pos.Repriced</c>. No suspendida →
/// <c>Inventory.Pos.NotSuspended</c>; punto sin POS → <c>Inventory.Pos.NotEnabled</c> (T605). (nuevo)
/// </summary>
public sealed record ResumePosDraftCommand(Guid DraftPublicId, Guid CashSessionPublicId)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }
}

public sealed class ResumePosDraftCommandValidator : AbstractValidator<ResumePosDraftCommand>
{
    public ResumePosDraftCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x.CashSessionPublicId).NotEmpty();
    }
}

public sealed class ResumePosDraftCommandHandler(IApplicationDbContext db, BorradorDelPos pos, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<ResumePosDraftCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(ResumePosDraftCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var sesion = await pos.SesionAbiertaAsync(request.CashSessionPublicId, exigirPos: true, ct);
            if (sesion.IsFailure) return Result.Failure<PosDraftDto>(sesion.Error);
            var cargada = await pos.VentaAsync(request.DraftPublicId, soloBorrador: true, ct);
            if (cargada.IsFailure) return Result.Failure<PosDraftDto>(cargada.Error);
            var venta = cargada.Value;
            var (s, caja, punto) = sesion.Value;
            if (venta.PointOfSaleId != punto.Id) return Result.Failure<PosDraftDto>(ErroresDelPos.DraftNotFound());
            if (!venta.IsSuspended) return Result.Failure<PosDraftDto>(ErroresDelPos.NotSuspended());

            var otraFecha = venta.OperationDate != s.OperatingDate;
            venta.IsSuspended = false;
            venta.SuspendedAt = null;
            venta.SuspendedLabel = null;
            venta.CashSessionId = s.Id;
            venta.CashRegisterId = caja.Id;
            venta.WarehouseId = caja.WarehouseId;
            venta.OperationDate = s.OperatingDate;

            var r = await pos.PrecificarAsync(venta, null, resolverListas: otraFecha, null, aplicar: true, ct);
            if (r.IsFailure) return Result.Failure<PosDraftDto>(r.Error);
            await auditoria.AnotarAsync(AuditEventTypes.InventoryPosResumed, request, venta.PublicId,
                new { cashRegisterId = caja.Id, repriced = otraFecha, venta.Total }, ct);
            await db.SaveChangesAsync(ct);
            await pos.EnlazarAprobacionesAsync(venta, ct);
            IReadOnlyList<AvisoDto>? avisos = otraFecha ? [new AvisoDto(ErroresDelPos.RepricedCode, ErroresDelPos.MensajeRepriced, null)] : null;
            return Result.Success(await pos.DtoAsync(venta, r.Value, null, avisos, ct));
        }, ct);
}

/// <summary>
/// Descarta la venta con motivo (<c>POST /pos/drafts/{id}/discard</c>), auditado. Una venta suspendida la puede descartar cualquier cajero
/// del punto; una en curso, sólo quien la tiene en su sesión. Las aprobaciones de descuento pendientes quedan sin efecto. (nuevo)
/// </summary>
public sealed record DiscardPosDraftCommand(Guid DraftPublicId, string Reason)
    : IRequest<Result<PosDraftDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta, IConMotivo
{
    public Guid OperationKey { get; init; }

    public Guid CashSessionPublicId { get; init; }
}

public sealed class DiscardPosDraftCommandValidator : AbstractValidator<DiscardPosDraftCommand>
{
    public DiscardPosDraftCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}

public sealed class DiscardPosDraftCommandHandler(IApplicationDbContext db, BorradorDelPos pos, IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<DiscardPosDraftCommand, Result<PosDraftDto>>
{
    public Task<Result<PosDraftDto>> Handle(DiscardPosDraftCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cargada = await pos.VentaAsync(request.DraftPublicId, soloBorrador: true, ct);
            if (cargada.IsFailure) return Result.Failure<PosDraftDto>(cargada.Error);
            var venta = cargada.Value;
            if (!venta.IsSuspended)
            {
                var sesion = await pos.SesionDeLaVentaAsync(venta, exigirPos: false, ct);
                if (sesion.IsFailure) return Result.Failure<PosDraftDto>(sesion.Error);
            }
            var usuario = await pos.UsuarioAsync(ct);
            if (usuario.IsFailure) return Result.Failure<PosDraftDto>(usuario.Error);

            await pos.RetirarAprobacionesAsync(venta, ct);
            venta.Descartar(usuario.Value.UserId, pos.Reloj.UtcNow, request.Reason);
            await auditoria.AnotarAsync(AuditEventTypes.InventoryPosDiscarded, request, venta.PublicId,
                new { reason = request.Reason.Trim(), venta.Total, lines = venta.Lines.Count(l => !l.IsDeleted) }, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await pos.DtoAsync(venta, null, null, null, ct));
        }, ct);
}

/// <summary>Lo que comparten los comandos del borrador. (nuevo)</summary>
internal static class PosDraftComun
{
    /// <summary>La venta en borrador, no suspendida, en la sesión abierta del usuario.</summary>
    public static async Task<Result<(InventoryDocument Venta, SesionDelPos Sesion)>> CargarDeLaSesionAsync(BorradorDelPos pos, Guid ventaPublicId, CancellationToken ct)
    {
        var cargada = await pos.VentaAsync(ventaPublicId, soloBorrador: true, ct);
        if (cargada.IsFailure) return Result.Failure<(InventoryDocument, SesionDelPos)>(cargada.Error);
        if (cargada.Value.IsSuspended) return Result.Failure<(InventoryDocument, SesionDelPos)>(ErroresDelPos.CashSessionNotOpen());
        var sesion = await pos.SesionDeLaVentaAsync(cargada.Value, exigirPos: false, ct);
        return sesion.IsFailure
            ? Result.Failure<(InventoryDocument, SesionDelPos)>(sesion.Error)
            : Result.Success((cargada.Value, sesion.Value));
    }

    /// <summary>Lee el código («3*» multiplica) y agrega o suma la línea.</summary>
    public static async Task<Result<(int LineNumber, bool Sumada)>> AgregarPorCodigoAsync(BorradorDelPos pos, InventoryDocument venta, string codigo, decimal? cantidad,
        CancellationToken ct)
    {
        var (veces, limpio) = BorradorDelPos.Multiplicador(codigo, cantidad);
        var leido = await pos.LeerAsync(limpio, ct);
        if (leido.IsFailure) return Result.Failure<(int, bool)>(leido.Error);
        return await pos.AgregarAsync(venta, leido.Value.Producto.Id, leido.Value.UnitId, leido.Value.UnitPublicId, veces, ct);
    }

    /// <summary>El vendedor: una fila viva de <c>INV_Salespeople</c> cuya persona no está eliminada (FR-057). Nulo si no lo es.</summary>
    public static Task<int?> VendedorVivoAsync(IApplicationDbContext db, Guid salespersonPublicId, CancellationToken ct) =>
        db.Salespeople.AsNoTracking()
            .Where(s => s.PublicId == salespersonPublicId && !s.IsDeleted && !s.Person.IsDeleted)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
}
