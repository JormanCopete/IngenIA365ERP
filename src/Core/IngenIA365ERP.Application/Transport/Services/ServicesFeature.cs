using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Services;

// Feature 020 (US6; R9, R11; T104–T106; FR-028, FR-029, FR-034, FR-044, FR-056, FR-058): los servicios por vehículo (productos de Comercial),
// las suscripciones con precio pactado y las políticas de facturación con vigencia.

public sealed record TransportServiceDto(Guid PublicId, Guid ProductPublicId, string ProductCode, string Name, int Priority, string? CreditLineCode,
    string? AssociateCreditMeansCode, string? CustomerCreditMeansCode, Guid? ConnectorPublicId, string? ConnectorName, bool IsActive);

/// <summary>La configuración de un servicio, de pantalla o API.</summary>
public sealed record TransportServiceInput(Guid ProductPublicId, int Priority, string? CreditLineCode, string? AssociateCreditMeansCode,
    string? CustomerCreditMeansCode, Guid? ConnectorPublicId, bool IsActive = true);

public sealed class TransportServiceInputValidator : AbstractValidator<TransportServiceInput>
{
    public TransportServiceInputValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEqual(Guid.Empty).WithMessage("Indique el producto de Comercial.");
        RuleFor(x => x.Priority).InclusiveBetween(1, 99).WithMessage("La prioridad va de 1 (primero) a 99.");
        RuleFor(x => x.CreditLineCode).MaximumLength(10);
        RuleFor(x => x.AssociateCreditMeansCode).MaximumLength(10);
        RuleFor(x => x.CustomerCreditMeansCode).MaximumLength(10);
    }
}

/// <summary>
/// Las reglas de la configuración de un servicio (T104): producto de Comercial de clase servicio, línea de Cartera marcada de servicio (si
/// hay Cartera), medios a crédito de su clase que admitan pago parcial (una factura lleva un pago a crédito por servicio), y conector
/// habilitado. Ningún producto es dos servicios vivos.
/// </summary>
public sealed class ReglasDeServicio(IApplicationDbContext db)
{
    public async Task<Result<(string ProductCode, string Name, int? ConnectorId)>> ValidarAsync(TransportServiceInput d, Guid? propio, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Where(p => p.PublicId == d.ProductPublicId).Select(p => new { p.Code, p.Name, p.Kind }).FirstOrDefaultAsync(ct);
        if (producto is null) return Falla("TransportService.ProductNotFound", "El producto no existe en Comercial.");
        if (producto.Kind != ProductKind.Service)
            return Falla("TransportService.ProductNotService", $"El producto {producto.Code} no es de clase servicio en Comercial.");
        if (await db.TransportServices.AsNoTracking().AnyAsync(s => s.ProductPublicId == d.ProductPublicId && (propio == null || s.PublicId != propio), ct))
            return Falla("TransportService.ProductInUse", $"El producto {producto.Code} ya es un servicio de Transporte.");

        if (!string.IsNullOrWhiteSpace(d.CreditLineCode))
        {
            var codigo = d.CreditLineCode.Trim().ToUpperInvariant();
            var linea = await db.CreditLines.AsNoTracking().Where(l => l.Code == codigo).Select(l => new { l.IsService }).FirstOrDefaultAsync(ct);
            if (linea is null) return Falla("TransportService.CreditLineNotFound", $"No existe la línea de Cartera {codigo}.");
            if (!linea.IsService)
                return Falla("TransportService.CreditLineNotService", $"La línea de Cartera {codigo} no está marcada como línea de servicio (sin interés).");
        }

        foreach (var (codigo, clase, etiqueta) in new[]
                 {
                     (d.AssociateCreditMeansCode, PaymentMeansClass.AssociateCredit, "para asociados"),
                     (d.CustomerCreditMeansCode, PaymentMeansClass.CustomerCredit, "para externos"),
                 })
        {
            if (string.IsNullOrWhiteSpace(codigo)) continue;
            var c = codigo.Trim().ToUpperInvariant();
            var medio = await db.PaymentMeans.AsNoTracking().Where(m => m.Code == c).Select(m => new { m.Class, m.AllowsPartial, m.IsActive }).FirstOrDefaultAsync(ct);
            if (medio is null || !medio.IsActive) return Falla("TransportService.CreditMeansNotFound", $"No existe el medio de pago activo {c}.");
            if (medio.Class != clase) return Falla("TransportService.CreditMeansClass", $"El medio {c} no es de la clase de crédito {etiqueta}.");
            if (!medio.AllowsPartial)
                return Falla("TransportService.CreditMeansNotPartial",
                    $"El medio {c} no admite pago parcial: una factura de servicios lleva un pago a crédito por servicio y lo necesita.");
        }

        int? conector = null;
        if (d.ConnectorPublicId is { } con)
        {
            var c = await db.Connectors.AsNoTracking().Where(x => x.PublicId == con).Select(x => new { x.Id, x.IsEnabled }).FirstOrDefaultAsync(ct);
            if (c is null || !c.IsEnabled) return Falla("Connector.NotEnabled", "El conector no está habilitado para la cooperativa.");
            conector = c.Id;
        }
        return Result.Success((producto.Code, producto.Name, conector));

        static Result<(string, string, int?)> Falla(string codigo, string mensaje) => Result.Failure<(string, string, int?)>(codigo, mensaje);
    }
}

public sealed record ListTransportServicesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<TransportServiceDto>>>;

public sealed class ListTransportServicesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListTransportServicesQuery, Result<IReadOnlyList<TransportServiceDto>>>
{
    public async Task<Result<IReadOnlyList<TransportServiceDto>>> Handle(ListTransportServicesQuery request, CancellationToken ct)
    {
        var q = db.TransportServices.AsNoTracking().Include(s => s.Connector).AsQueryable();
        if (!request.IncludeInactive) q = q.Where(s => s.IsActive);
        var filas = await q.OrderBy(s => s.Priority).ThenBy(s => s.Name).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TransportServiceDto>>(filas.Select(Dto).ToList());
    }

    public static TransportServiceDto Dto(TransportService s) => new(s.PublicId, s.ProductPublicId, s.ProductCode, s.Name, s.Priority, s.CreditLineCode,
        s.AssociateCreditMeansCode, s.CustomerCreditMeansCode, s.Connector?.PublicId, s.Connector?.Name, s.IsActive);
}

public sealed record CreateTransportServiceCommand(TransportServiceInput Service) : IRequest<Result<TransportServiceDto>>;

public sealed class CreateTransportServiceCommandValidator : AbstractValidator<CreateTransportServiceCommand>
{
    public CreateTransportServiceCommandValidator() => RuleFor(x => x.Service).NotNull().SetValidator(new TransportServiceInputValidator());
}

public sealed class CreateTransportServiceCommandHandler(IApplicationDbContext db, ReglasDeServicio reglas)
    : IRequestHandler<CreateTransportServiceCommand, Result<TransportServiceDto>>
{
    public async Task<Result<TransportServiceDto>> Handle(CreateTransportServiceCommand request, CancellationToken ct)
    {
        var d = request.Service;
        var v = await reglas.ValidarAsync(d, null, ct);
        if (v.IsFailure) return Result.Failure<TransportServiceDto>(v.Error);
        var s = new TransportService
        {
            ProductPublicId = d.ProductPublicId, ProductCode = v.Value.ProductCode, Name = v.Value.Name, Priority = d.Priority,
            CreditLineCode = Codigo(d.CreditLineCode), AssociateCreditMeansCode = Codigo(d.AssociateCreditMeansCode),
            CustomerCreditMeansCode = Codigo(d.CustomerCreditMeansCode), ConnectorId = v.Value.ConnectorId, IsActive = d.IsActive,
        };
        db.TransportServices.Add(s);
        await db.SaveChangesAsync(ct);
        return Result.Success(ListTransportServicesQueryHandler.Dto(await db.TransportServices.Include(x => x.Connector).FirstAsync(x => x.Id == s.Id, ct)));
    }

    internal static string? Codigo(string? c) => string.IsNullOrWhiteSpace(c) ? null : c.Trim().ToUpperInvariant();
}

public sealed record UpdateTransportServiceCommand(Guid PublicId, TransportServiceInput Service) : IRequest<Result<TransportServiceDto>>;

public sealed class UpdateTransportServiceCommandValidator : AbstractValidator<UpdateTransportServiceCommand>
{
    public UpdateTransportServiceCommandValidator() => RuleFor(x => x.Service).NotNull().SetValidator(new TransportServiceInputValidator());
}

public sealed class UpdateTransportServiceCommandHandler(IApplicationDbContext db, ReglasDeServicio reglas)
    : IRequestHandler<UpdateTransportServiceCommand, Result<TransportServiceDto>>
{
    public async Task<Result<TransportServiceDto>> Handle(UpdateTransportServiceCommand request, CancellationToken ct)
    {
        var s = await db.TransportServices.FirstOrDefaultAsync(x => x.PublicId == request.PublicId, ct);
        if (s is null) return Result.Failure<TransportServiceDto>("TransportService.NotFound", "No existe ese servicio.");
        var d = request.Service with { ProductPublicId = s.ProductPublicId };
        var v = await reglas.ValidarAsync(d, s.PublicId, ct);
        if (v.IsFailure) return Result.Failure<TransportServiceDto>(v.Error);
        s.Name = v.Value.Name;
        s.ProductCode = v.Value.ProductCode;
        s.Priority = d.Priority;
        s.CreditLineCode = CreateTransportServiceCommandHandler.Codigo(d.CreditLineCode);
        s.AssociateCreditMeansCode = CreateTransportServiceCommandHandler.Codigo(d.AssociateCreditMeansCode);
        s.CustomerCreditMeansCode = CreateTransportServiceCommandHandler.Codigo(d.CustomerCreditMeansCode);
        s.ConnectorId = v.Value.ConnectorId;
        s.IsActive = d.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Success(ListTransportServicesQueryHandler.Dto(await db.TransportServices.Include(x => x.Connector).FirstAsync(x => x.Id == s.Id, ct)));
    }
}

/// <summary>
/// Suscribir un vehículo a un servicio, terminar la suscripción y pactar su precio con vigencia (R9, T105; FR-029). Lo usan la pantalla, la API
/// y la plantilla <c>transport.subscriptions</c>. <b>No guarda.</b>
/// </summary>
public sealed class SuscripcionDeServicio(IApplicationDbContext db, IDateTimeService reloj)
{
    public async Task<Result<VehicleSubscription>> SuscribirAsync(Vehicle v, TransportService s, DateOnly desde, DateOnly? hasta, decimal? precio, string? motivo, CancellationToken ct)
    {
        if (v.EstaRetirado) return Result.Failure<VehicleSubscription>(ErroresDeTransporte.Retirado(v.Plate));
        if (!s.IsActive) return Result.Failure<VehicleSubscription>("TransportService.Inactive", $"El servicio {s.Name} está inactivo.");
        if (hasta < desde) return Result.Failure<VehicleSubscription>("Subscription.Dates", "La suscripción no puede terminar antes de empezar.");
        var existentes = v.Id == 0 ? [] : await db.VehicleSubscriptions.Where(x => x.VehicleId == v.Id && x.ServiceId == s.Id).ToListAsync(ct);
        existentes.AddRange(db.VehicleSubscriptions.Local.Where(x => (ReferenceEquals(x.Vehicle, v) || (v.Id != 0 && x.VehicleId == v.Id)) && x.ServiceId == s.Id
                                                                    && !existentes.Contains(x)));
        if (existentes.Any(x => !x.IsDeleted && x.SeCruzaCon(desde, hasta)))
            return Result.Failure<VehicleSubscription>("Subscription.Overlaps", $"El vehículo {v.Plate} ya está suscrito a {s.Name} en esas fechas.");

        var suscripcion = new VehicleSubscription
        {
            VehicleId = v.Id, Vehicle = v.Id == 0 ? v : null, ServiceId = s.Id, From = desde, To = hasta,
            Reason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(), CreatedAt = reloj.UtcNow,
        };
        db.VehicleSubscriptions.Add(suscripcion);
        if (precio is { } p)
        {
            var r = PonerPrecio(suscripcion, p, desde);
            if (r.IsFailure) return Result.Failure<VehicleSubscription>(r.Error);
        }
        return Result.Success(suscripcion);
    }

    public Result Terminar(VehicleSubscription s, DateOnly hasta, string? motivo)
    {
        if (hasta < s.From) return Result.Failure("Subscription.Dates", "La suscripción no puede terminar antes de empezar.");
        s.To = hasta;
        if (!string.IsNullOrWhiteSpace(motivo)) s.Reason = motivo.Trim();
        foreach (var p in s.Prices.Where(x => !x.IsDeleted && (x.To is null || x.To > hasta)))
            p.To = hasta < p.From ? p.From : hasta;
        return Result.Success();
    }

    /// <summary>Un precio pactado desde una fecha: cierra el vigente la víspera; no admite vigencias cruzadas (FR-029).</summary>
    public Result PonerPrecio(VehicleSubscription s, decimal precio, DateOnly desde)
    {
        if (precio <= 0m) return Result.Failure("SubscriptionPrice.Invalid", "El precio pactado es mayor que cero.");
        var vivos = s.Prices.Where(x => !x.IsDeleted).ToList();
        if (vivos.Any(x => x.From >= desde))
            return Result.Failure("SubscriptionPrice.Overlaps", "Ya hay un precio pactado desde esa fecha o después: corrija el último.");
        foreach (var abierto in vivos.Where(x => x.To is null || x.To >= desde)) abierto.To = desde.AddDays(-1);
        var nuevo = new SubscriptionPrice { Subscription = s, SubscriptionId = s.Id, Price = precio, From = desde, To = s.To, CreatedAt = reloj.UtcNow };
        s.Prices.Add(nuevo);
        db.SubscriptionPrices.Add(nuevo);
        return Result.Success();
    }
}

public sealed record SubscribeVehicleCommand(Guid VehiclePublicId, Guid ServicePublicId, DateOnly From, DateOnly? To, decimal? Price, string? Reason = null) : IRequest<Result<Guid>>;

public sealed class SubscribeVehicleCommandValidator : AbstractValidator<SubscribeVehicleCommand>
{
    public SubscribeVehicleCommandValidator()
    {
        RuleFor(x => x.ServicePublicId).NotEqual(Guid.Empty).WithMessage("Indique el servicio.");
        RuleFor(x => x.From).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo.");
        RuleFor(x => x.Price).GreaterThan(0).When(x => x.Price is not null);
    }
}

public sealed class SubscribeVehicleCommandHandler(IApplicationDbContext db, SuscripcionDeServicio suscripciones) : IRequestHandler<SubscribeVehicleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SubscribeVehicleCommand request, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure<Guid>(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var s = await db.TransportServices.FirstOrDefaultAsync(x => x.PublicId == request.ServicePublicId, ct);
        if (s is null) return Result.Failure<Guid>("TransportService.NotFound", "No existe ese servicio.");
        var r = await suscripciones.SuscribirAsync(v, s, request.From, request.To, request.Price, request.Reason, ct);
        if (r.IsFailure) return Result.Failure<Guid>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(r.Value.PublicId);
    }
}

public sealed record EndSubscriptionCommand(Guid VehiclePublicId, Guid SubscriptionPublicId, DateOnly To, string? Reason) : IRequest<Result>;

public sealed class EndSubscriptionCommandHandler(IApplicationDbContext db, SuscripcionDeServicio suscripciones) : IRequestHandler<EndSubscriptionCommand, Result>
{
    public async Task<Result> Handle(EndSubscriptionCommand request, CancellationToken ct)
    {
        var s = await db.VehicleSubscriptions.Include(x => x.Prices).Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.PublicId == request.SubscriptionPublicId && x.Vehicle!.PublicId == request.VehiclePublicId, ct);
        if (s is null) return Result.Failure("Subscription.NotFound", "No existe esa suscripción.");
        var r = suscripciones.Terminar(s, request.To, request.Reason);
        if (r.IsFailure) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record SetSubscriptionPriceCommand(Guid VehiclePublicId, Guid SubscriptionPublicId, decimal Price, DateOnly From) : IRequest<Result>;

public sealed class SetSubscriptionPriceCommandHandler(IApplicationDbContext db, SuscripcionDeServicio suscripciones) : IRequestHandler<SetSubscriptionPriceCommand, Result>
{
    public async Task<Result> Handle(SetSubscriptionPriceCommand request, CancellationToken ct)
    {
        var s = await db.VehicleSubscriptions.Include(x => x.Prices).Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.PublicId == request.SubscriptionPublicId && x.Vehicle!.PublicId == request.VehiclePublicId, ct);
        if (s is null) return Result.Failure("Subscription.NotFound", "No existe esa suscripción.");
        var r = suscripciones.PonerPrecio(s, request.Price, request.From);
        if (r.IsFailure) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record BillingPolicyDto(Guid PublicId, DateOnly ValidFrom, DateOnly? ValidTo, ProrationPolicy Proration, string SalesDocumentTypeCode,
    string? WarehouseCode, string? SalesPointCode, short DueDay);

public sealed record ListBillingPoliciesQuery : IRequest<Result<IReadOnlyList<BillingPolicyDto>>>;

public sealed class ListBillingPoliciesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListBillingPoliciesQuery, Result<IReadOnlyList<BillingPolicyDto>>>
{
    public async Task<Result<IReadOnlyList<BillingPolicyDto>>> Handle(ListBillingPoliciesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<BillingPolicyDto>>(await db.TransportBillingPolicies.AsNoTracking().OrderByDescending(p => p.ValidFrom)
            .Select(p => new BillingPolicyDto(p.PublicId, p.ValidFrom, p.ValidTo, p.Proration, p.SalesDocumentTypeCode, p.WarehouseCode, p.SalesPointCode, p.DueDay))
            .ToListAsync(ct));
}

/// <summary>Una política de facturación con vigencia (T106; FR-034): cierra la vigente la víspera; no hay cruces.</summary>
public sealed record SetBillingPolicyCommand(DateOnly ValidFrom, ProrationPolicy Proration, string SalesDocumentTypeCode, string? WarehouseCode,
    string? SalesPointCode, short DueDay) : IRequest<Result<Guid>>;

public sealed class SetBillingPolicyCommandValidator : AbstractValidator<SetBillingPolicyCommand>
{
    public SetBillingPolicyCommandValidator()
    {
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly));
        RuleFor(x => x.Proration).IsInEnum();
        RuleFor(x => x.SalesDocumentTypeCode).NotEmpty().WithMessage("Indique el tipo de documento de venta.").MaximumLength(10);
        RuleFor(x => x.WarehouseCode).MaximumLength(10);
        RuleFor(x => x.SalesPointCode).MaximumLength(10);
        RuleFor(x => x.DueDay).InclusiveBetween((short)0, (short)28).WithMessage("El día de vencimiento va de 1 a 28 (0 = último día del mes).");
    }
}

public sealed class SetBillingPolicyCommandHandler(IApplicationDbContext db) : IRequestHandler<SetBillingPolicyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SetBillingPolicyCommand request, CancellationToken ct)
    {
        var codigo = request.SalesDocumentTypeCode.Trim().ToUpperInvariant();
        var tipo = await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.Code == codigo).Select(t => new { t.Class, t.IsActive }).FirstOrDefaultAsync(ct);
        if (tipo is null || !tipo.IsActive) return Result.Failure<Guid>("Billing.DocumentTypeNotFound", $"No existe el tipo de documento activo {codigo}.");
        if (tipo.Class is not (DocumentClass.SalesInvoice or DocumentClass.NonElectronicSalesReceipt))
            return Result.Failure<Guid>("Billing.DocumentTypeNotSales", $"El tipo {codigo} no es una factura de venta.");
        if (!string.IsNullOrWhiteSpace(request.WarehouseCode)
            && !await db.Warehouses.AsNoTracking().AnyAsync(w => w.Code == request.WarehouseCode.Trim().ToUpperInvariant(), ct))
            return Result.Failure<Guid>("Billing.WarehouseNotFound", $"No existe la bodega {request.WarehouseCode}.");

        var vigentes = await db.TransportBillingPolicies.Where(p => p.ValidTo == null || p.ValidTo >= request.ValidFrom).ToListAsync(ct);
        if (vigentes.Any(p => p.ValidFrom >= request.ValidFrom))
            return Result.Failure<Guid>("Billing.PolicyOverlaps", "Ya hay una política desde esa fecha o después.");
        foreach (var p in vigentes) p.ValidTo = request.ValidFrom.AddDays(-1);

        var nueva = new BillingPolicy
        {
            ValidFrom = request.ValidFrom, Proration = request.Proration, SalesDocumentTypeCode = codigo,
            WarehouseCode = string.IsNullOrWhiteSpace(request.WarehouseCode) ? null : request.WarehouseCode.Trim().ToUpperInvariant(),
            SalesPointCode = string.IsNullOrWhiteSpace(request.SalesPointCode) ? null : request.SalesPointCode.Trim().ToUpperInvariant(),
            DueDay = request.DueDay,
        };
        db.TransportBillingPolicies.Add(nueva);
        await db.SaveChangesAsync(ct);
        return Result.Success(nueva.PublicId);
    }
}

/// <summary>La parte de servicios de la ficha del vehículo: suscripciones con su precio pactado vigente.</summary>
public sealed class ServiciosEnLaFicha(IApplicationDbContext db, IDateTimeService reloj) : IParteDeLaFichaDelVehiculo
{
    public async Task<VehicleDetailDto> CompletarAsync(VehicleDetailDto ficha, Vehicle vehiculo, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var subs = await db.VehicleSubscriptions.AsNoTracking().Where(s => s.VehicleId == vehiculo.Id).Include(s => s.Service).Include(s => s.Prices)
            .OrderBy(s => s.Service!.Priority).ThenByDescending(s => s.From).ToListAsync(ct);
        return ficha with
        {
            Subscriptions = subs.Select(s => new VehicleSubscriptionDto(s.PublicId, s.Service!.PublicId, s.Service.Name, s.Service.ProductCode, s.From, s.To,
                s.Prices.Where(p => !p.IsDeleted && p.VigenteEn(hoy)).Select(p => (decimal?)p.Price).FirstOrDefault(),
                s.From <= hoy && (s.To is null || s.To >= hoy))).ToList(),
        };
    }
}
