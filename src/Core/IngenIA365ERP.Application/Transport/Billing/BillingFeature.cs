using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Billing;

// Feature 020 (US6; R10; T111–T114; FR-030 a FR-033; contracts/api.md §3, «facturación»). La corrida en lote avanza por tandas que pide la
// pantalla (POST /billing/runs/{id}/next) en la sesión de quien factura, no en una tarea de fondo: Comercial exige que el mensaje de la venta
// a crédito lo origine la persona (T11 de la 012) y el crédito exige su permiso (Inventory.Sales.SellOnCredit).

public sealed record BillingOmissionDto(Guid VehiclePublicId, string Plate, string InternalNumber, string Code, string Reason);

public sealed record BillingPreviewItemDto(Guid VehiclePublicId, string Plate, string InternalNumber, string OwnerName, IReadOnlyList<BillingPreviewLineDto> Lines, decimal Total);

public sealed record BillingPreviewLineDto(string Service, decimal Quantity, decimal? UnitPrice, bool AgreedPrice);

public sealed record BillingPreviewDto(string Period, int Vehicles, decimal Total, IReadOnlyList<BillingPreviewItemDto> Items, IReadOnlyList<BillingOmissionDto> Omitted,
    int AlreadyBilled, string? Warning);

public sealed record BillingRunDto(Guid PublicId, string Period, BillingMode Mode, BillingPaymentMode PaymentMode, BillingRunStatus Status, DateTime? StartedAt,
    DateTime? FinishedAt, string StartedBy, int InvoicesCount, decimal TotalAmount, int AlreadyBilledCount, int OmittedCount, IReadOnlyList<BillingOmissionDto> Omitted,
    int Pending);

public sealed record SingleBillingResultDto(Guid? InvoicePublicId, string? InvoiceNumber, decimal Total, bool AlreadyBilled, string? OmissionCode, string? OmissionReason,
    IReadOnlyList<Guid> Charges);

/// <summary>El aviso de la ruta sin Cartera (FR-059).</summary>
internal static class AvisoSinCartera
{
    public const string Texto = "Cartera no está activa: la deuda queda como cuenta por cobrar comercial, se cobra sólo en caja y se salda por la prioridad de cada servicio.";
}

/// <summary>Lo que se necesita para facturar: la política vigente y el período válido.</summary>
internal static class PreparacionDeFacturacion
{
    public static async Task<Result<(PeriodoDeServicio Periodo, BillingPolicy Politica)>> PrepararAsync(IApplicationDbContext db, string periodo, DateOnly hoy, CancellationToken ct)
    {
        if (!PeriodoDeServicio.TryParse(periodo, out var p))
            return Result.Failure<(PeriodoDeServicio, BillingPolicy)>("Billing.PeriodInvalid", $"«{periodo}» no es un período (aaaamm).");
        var fecha = hoy < p.Inicio ? p.Inicio : hoy;
        var politica = await db.TransportBillingPolicies.AsNoTracking()
            .Where(x => x.ValidFrom <= fecha && (x.ValidTo == null || x.ValidTo >= fecha)).OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(ct);
        return politica is null
            ? Result.Failure<(PeriodoDeServicio, BillingPolicy)>("Billing.NoPolicy", "No hay una política de facturación de servicios vigente. Configúrela en Transporte › Servicios.")
            : Result.Success((p, politica));
    }
}

/// <summary>Qué facturaría el período y qué omitiría, sin guardar nada (T111).</summary>
public sealed record PreviewServiceBillingQuery(string Period) : IRequest<Result<BillingPreviewDto>>;

public sealed class PreviewServiceBillingQueryHandler(IApplicationDbContext db, CalculoDeCargosDelPeriodo calculo, ISender sender, IDateTimeService reloj,
    IServiceProvider servicios) : IRequestHandler<PreviewServiceBillingQuery, Result<BillingPreviewDto>>
{
    public async Task<Result<BillingPreviewDto>> Handle(PreviewServiceBillingQuery request, CancellationToken ct)
    {
        var preparada = await PreparacionDeFacturacion.PrepararAsync(db, request.Period, reloj.HoyLocal, ct);
        if (preparada.IsFailure) return Result.Failure<BillingPreviewDto>(preparada.Error);
        var (periodo, politica) = preparada.Value;
        var fecha = reloj.HoyLocal < periodo.Inicio ? periodo.Inicio : reloj.HoyLocal;

        var vehiculos = await db.Vehicles.Include(v => v.OwnerPerson).Where(v => v.Status != VehicleStatus.Retired).OrderBy(v => v.InternalNumber).ToListAsync(ct);
        var items = new List<BillingPreviewItemDto>();
        var omitidos = new List<BillingOmissionDto>();
        var ya = 0;
        var precios = new Dictionary<(Guid, Guid), decimal?>();
        foreach (var v in vehiculos)
        {
            var c = await calculo.CalcularAsync(v, periodo, politica, fecha, ct);
            if (c.YaFacturado) { ya++; continue; }
            if (c.Omitido) { omitidos.Add(new(v.PublicId, v.Plate, v.InternalNumber, c.CodigoOmision!, c.MotivoOmision!)); continue; }
            var lineas = new List<BillingPreviewLineDto>();
            foreach (var cargo in c.Cargos)
            {
                var precio = cargo.PrecioPactado;
                if (precio is null)
                {
                    var clave = (cargo.Servicio.ProductPublicId, v.OwnerPerson!.PublicId);
                    if (!precios.TryGetValue(clave, out precio))
                    {
                        var r = await sender.Send(new ResolvePriceQuery(cargo.Servicio.ProductPublicId, PersonPublicId: v.OwnerPerson.PublicId, Date: fecha), ct);
                        precio = r.IsSuccess ? r.Value.Price : null;
                        precios[clave] = precio;
                    }
                }
                lineas.Add(new BillingPreviewLineDto(cargo.Servicio.Name, cargo.Cantidad, precio, cargo.PrecioPactado is not null));
            }
            if (lineas.Any(l => l.UnitPrice is null))
            {
                omitidos.Add(new(v.PublicId, v.Plate, v.InternalNumber, "Inventory.Price.NotFound", "Un servicio no tiene precio vigente en Comercial."));
                continue;
            }
            items.Add(new BillingPreviewItemDto(v.PublicId, v.Plate, v.InternalNumber, NombreVisible.De(v.OwnerPerson!), lineas,
                lineas.Sum(l => Math.Round(l.Quantity * l.UnitPrice!.Value, 2, MidpointRounding.AwayFromZero))));
        }
        var aviso = await CarteraActivaAsync(servicios, fecha, ct) ? null : AvisoSinCartera.Texto;
        return Result.Success(new BillingPreviewDto(periodo.Codigo, items.Count, items.Sum(i => i.Total), items, omitidos, ya, aviso));
    }

    internal static async Task<bool> CarteraActivaAsync(IServiceProvider servicios, DateOnly fecha, CancellationToken ct)
    {
        var cartera = (Common.Integration.Lending.IConsultaDeObligaciones?)servicios.GetService(typeof(Common.Integration.Lending.IConsultaDeObligaciones));
        return cartera is not null
               && (await cartera.ObligacionesAsync(new Common.Integration.Lending.ConsultaDeObligaciones([], fecha, false), ct)).LendingStatus
               == Common.Integration.Lending.EstadoDeCartera.Active;
    }
}

/// <summary>Factura un vehículo en el período, en línea (T112; US6 escenario 3).</summary>
public sealed record RunSingleServiceBillingCommand(Guid VehiclePublicId, string Period, BillingPaymentMode PaymentMode) : IRequest<Result<SingleBillingResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RunSingleServiceBillingCommandValidator : AbstractValidator<RunSingleServiceBillingCommand>
{
    public RunSingleServiceBillingCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Period).NotEmpty().Length(6);
        RuleFor(x => x.PaymentMode).IsInEnum();
    }
}

public sealed class RunSingleServiceBillingCommandHandler(IApplicationDbContext db, FacturadorDeVehiculo facturador, IActorActual actor, IDateTimeService reloj)
    : IRequestHandler<RunSingleServiceBillingCommand, Result<SingleBillingResultDto>>
{
    public async Task<Result<SingleBillingResultDto>> Handle(RunSingleServiceBillingCommand request, CancellationToken ct)
    {
        var preparada = await PreparacionDeFacturacion.PrepararAsync(db, request.Period, reloj.HoyLocal, ct);
        if (preparada.IsFailure) return Result.Failure<SingleBillingResultDto>(preparada.Error);
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure<SingleBillingResultDto>(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));

        var quien = await actor.ObtenerAsync(ct);
        var corrida = new BillingRun
        {
            Period = preparada.Value.Periodo.Codigo, Mode = BillingMode.Single, PaymentMode = request.PaymentMode, Status = BillingRunStatus.Running,
            StartedAt = reloj.UtcNow, StartedBy = quien.Name.Length > 150 ? quien.Name[..150] : quien.Name, StartedByCentralUserId = quien.CentralUserId,
            OperationKey = request.OperationKey, VehicleId = v.Id,
        };
        db.TransportBillingRuns.Add(corrida);
        await db.SaveChangesAsync(ct);

        var r = await facturador.FacturarAsync(v, preparada.Value.Periodo, request.PaymentMode, corrida, preparada.Value.Politica, ct);
        corrida.FinishedAt = reloj.UtcNow;
        corrida.InvoicesCount = r.Facturado ? 1 : 0;
        corrida.TotalAmount = r.Total;
        corrida.AlreadyBilledCount = r.YaEstaba ? 1 : 0;
        corrida.OmittedCount = r.CodigoOmision is null ? 0 : 1;
        corrida.Status = r.CodigoOmision is null ? BillingRunStatus.Finished : BillingRunStatus.FinishedWithOmissions;
        corrida.SummaryJson = r.CodigoOmision is null ? null
            : JsonSerializer.Serialize(new[] { new BillingOmissionDto(v.PublicId, v.Plate, v.InternalNumber, r.CodigoOmision, r.MotivoOmision!) });
        await db.SaveChangesAsync(ct);

        if (r.YaEstaba) return Result.Failure<SingleBillingResultDto>("Billing.PeriodAlreadyBilled", $"El vehículo {v.Plate} ya tiene su factura de servicios del período.");
        return Result.Success(new SingleBillingResultDto(r.InvoicePublicId, r.InvoiceNumber, r.Total, r.YaEstaba, r.CodigoOmision, r.MotivoOmision,
            r.Cargos.Select(c => c.PublicId).ToList()));
    }
}

/// <summary>Abre una corrida en lote del período (T113): la pantalla la avanza por tandas con <see cref="ProcessServiceBillingRunCommand"/>.</summary>
public sealed record StartServiceBillingRunCommand(string Period, BillingPaymentMode PaymentMode) : IRequest<Result<BillingRunDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class StartServiceBillingRunCommandValidator : AbstractValidator<StartServiceBillingRunCommand>
{
    public StartServiceBillingRunCommandValidator()
    {
        RuleFor(x => x.Period).NotEmpty().Length(6);
        RuleFor(x => x.PaymentMode).IsInEnum();
    }
}

public sealed class StartServiceBillingRunCommandHandler(IApplicationDbContext db, IActorActual actor, IDateTimeService reloj)
    : IRequestHandler<StartServiceBillingRunCommand, Result<BillingRunDto>>
{
    public async Task<Result<BillingRunDto>> Handle(StartServiceBillingRunCommand request, CancellationToken ct)
    {
        var preparada = await PreparacionDeFacturacion.PrepararAsync(db, request.Period, reloj.HoyLocal, ct);
        if (preparada.IsFailure) return Result.Failure<BillingRunDto>(preparada.Error);
        var codigo = preparada.Value.Periodo.Codigo;
        if (await db.TransportBillingRuns.AsNoTracking().AnyAsync(r => r.Period == codigo && r.Mode == BillingMode.Batch
                                                                       && (r.Status == BillingRunStatus.Queued || r.Status == BillingRunStatus.Running), ct))
            return Result.Failure<BillingRunDto>("Billing.RunInProgress", $"Ya hay una facturación en curso del período {codigo}: continúela.");

        var quien = await actor.ObtenerAsync(ct);
        var corrida = new BillingRun
        {
            Period = codigo, Mode = BillingMode.Batch, PaymentMode = request.PaymentMode, Status = BillingRunStatus.Queued, StartedAt = reloj.UtcNow,
            StartedBy = quien.Name.Length > 150 ? quien.Name[..150] : quien.Name, StartedByCentralUserId = quien.CentralUserId, OperationKey = request.OperationKey,
        };
        db.TransportBillingRuns.Add(corrida);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDeFacturacion.DtoAsync(db, corrida, ct));
    }
}

/// <summary>
/// Avanza una corrida en lote por una tanda de vehículos activos (T113; SC-003): factura cada uno en su propia unidad de trabajo —un fallo
/// deja el vehículo omitido con su motivo y la tanda sigue— y termina cuando no quedan vehículos. Un vehículo que otra sesión ya facturó cuenta
/// como «ya estaba» (índice único).
/// </summary>
public sealed record ProcessServiceBillingRunCommand(Guid RunPublicId, int BatchSize = 20) : IRequest<Result<BillingRunDto>>;

public sealed class ProcessServiceBillingRunCommandHandler(IApplicationDbContext db, FacturadorDeVehiculo facturador, IDateTimeService reloj)
    : IRequestHandler<ProcessServiceBillingRunCommand, Result<BillingRunDto>>
{
    public async Task<Result<BillingRunDto>> Handle(ProcessServiceBillingRunCommand request, CancellationToken ct)
    {
        var corrida = await db.TransportBillingRuns.FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (corrida is null) return Result.Failure<BillingRunDto>("Billing.RunNotFound", "No existe esa corrida.");
        if (corrida.Status is not (BillingRunStatus.Queued or BillingRunStatus.Running)) return Result.Success(await ConsultasDeFacturacion.DtoAsync(db, corrida, ct));

        var preparada = await PreparacionDeFacturacion.PrepararAsync(db, corrida.Period, reloj.HoyLocal, ct);
        if (preparada.IsFailure)
        {
            corrida.Status = BillingRunStatus.Failed;
            corrida.FinishedAt = reloj.UtcNow;
            corrida.SummaryJson = JsonSerializer.Serialize(new[] { new BillingOmissionDto(Guid.Empty, "", "", preparada.Error.Code, preparada.Error.Message) });
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeFacturacion.DtoAsync(db, corrida, ct));
        }

        corrida.Status = BillingRunStatus.Running;
        var cursor = corrida.CursorVehicleId ?? 0;
        var tanda = Math.Clamp(request.BatchSize, 1, 100);
        var ids = await db.Vehicles.AsNoTracking().Where(v => v.Status == VehicleStatus.Active && v.Id > cursor).OrderBy(v => v.Id).Select(v => v.Id).Take(tanda).ToListAsync(ct);
        var omitidos = ConsultasDeFacturacion.Omitidos(corrida).ToList();

        foreach (var id in ids)
        {
            var v = await db.Vehicles.FirstAsync(x => x.Id == id, ct);
            FacturaDeVehiculo r;
            try
            {
                r = await facturador.FacturarAsync(v, preparada.Value.Periodo, corrida.PaymentMode, corrida, preparada.Value.Politica, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                r = FacturaDeVehiculo.Omitido("Billing.Unexpected", ex.Message.Length > 300 ? ex.Message[..300] : ex.Message);
            }

            if (r.Facturado) { corrida.InvoicesCount++; corrida.TotalAmount += r.Total; }
            else if (r.YaEstaba) corrida.AlreadyBilledCount++;
            else if (r.CodigoOmision is not null && r.CodigoOmision != "Billing.NoSubscription")
            {
                corrida.OmittedCount++;
                omitidos.Add(new BillingOmissionDto(v.PublicId, v.Plate, v.InternalNumber, r.CodigoOmision, r.MotivoOmision!));
            }
            corrida.CursorVehicleId = id;
            corrida.SummaryJson = JsonSerializer.Serialize(omitidos);
            await db.SaveChangesAsync(ct);
        }

        if (ids.Count < tanda)
        {
            corrida.Status = corrida.OmittedCount == 0 ? BillingRunStatus.Finished : BillingRunStatus.FinishedWithOmissions;
            corrida.FinishedAt = reloj.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDeFacturacion.DtoAsync(db, corrida, ct));
    }
}

internal static class ConsultasDeFacturacion
{
    public static IReadOnlyList<BillingOmissionDto> Omitidos(BillingRun r) =>
        string.IsNullOrWhiteSpace(r.SummaryJson) ? [] : JsonSerializer.Deserialize<List<BillingOmissionDto>>(r.SummaryJson) ?? [];

    public static async Task<BillingRunDto> DtoAsync(IApplicationDbContext db, BillingRun r, CancellationToken ct)
    {
        var cursor = r.CursorVehicleId ?? 0;
        var pendientes = r.Mode == BillingMode.Batch && r.Status is BillingRunStatus.Queued or BillingRunStatus.Running
            ? await db.Vehicles.AsNoTracking().CountAsync(v => v.Status == VehicleStatus.Active && v.Id > cursor, ct)
            : 0;
        return new BillingRunDto(r.PublicId, r.Period, r.Mode, r.PaymentMode, r.Status, r.StartedAt, r.FinishedAt, r.StartedBy, r.InvoicesCount, r.TotalAmount,
            r.AlreadyBilledCount, r.OmittedCount, Omitidos(r), pendientes);
    }
}

public sealed record GetBillingRunsQuery(string? Period = null) : IRequest<Result<IReadOnlyList<BillingRunDto>>>;

public sealed class GetBillingRunsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBillingRunsQuery, Result<IReadOnlyList<BillingRunDto>>>
{
    public async Task<Result<IReadOnlyList<BillingRunDto>>> Handle(GetBillingRunsQuery request, CancellationToken ct)
    {
        var q = db.TransportBillingRuns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Period)) q = q.Where(r => r.Period == request.Period);
        var filas = await q.OrderByDescending(r => r.Id).Take(50).ToListAsync(ct);
        var lista = new List<BillingRunDto>();
        foreach (var r in filas) lista.Add(await ConsultasDeFacturacion.DtoAsync(db, r, ct));
        return Result.Success<IReadOnlyList<BillingRunDto>>(lista);
    }
}

public sealed record GetBillingRunQuery(Guid RunPublicId) : IRequest<Result<BillingRunDto>>;

public sealed class GetBillingRunQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBillingRunQuery, Result<BillingRunDto>>
{
    public async Task<Result<BillingRunDto>> Handle(GetBillingRunQuery request, CancellationToken ct)
    {
        var r = await db.TransportBillingRuns.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.RunPublicId, ct);
        return r is null ? Result.Failure<BillingRunDto>("Billing.RunNotFound", "No existe esa corrida.") : Result.Success(await ConsultasDeFacturacion.DtoAsync(db, r, ct));
    }
}

public sealed record ServiceChargeDto(Guid PublicId, Guid VehiclePublicId, string Plate, string InternalNumber, string Service, string Period, string InvoiceNumber,
    decimal Amount, decimal Paid, ChargeRoute Route, ChargeStatus Status, DateTime? PaidAt, ChargeReviewFlag ReviewFlag, Guid ExternalId);

public sealed record ListServiceChargesQuery(Guid? VehiclePublicId = null, string? Period = null, ChargeStatus? Status = null) : IRequest<Result<IReadOnlyList<ServiceChargeDto>>>;

public sealed class ListServiceChargesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListServiceChargesQuery, Result<IReadOnlyList<ServiceChargeDto>>>
{
    public async Task<Result<IReadOnlyList<ServiceChargeDto>>> Handle(ListServiceChargesQuery request, CancellationToken ct)
    {
        var q = db.ServiceCharges.AsNoTracking().AsQueryable();
        if (request.VehiclePublicId is { } v) q = q.Where(c => c.Vehicle!.PublicId == v);
        if (!string.IsNullOrWhiteSpace(request.Period)) q = q.Where(c => c.Period == request.Period);
        if (request.Status is { } s) q = q.Where(c => c.Status == s);
        var filas = await q.OrderByDescending(c => c.Period).ThenBy(c => c.Vehicle!.InternalNumber).Take(2000)
            .Select(c => new ServiceChargeDto(c.PublicId, c.Vehicle!.PublicId, c.Vehicle.Plate, c.Vehicle.InternalNumber, c.Service!.Name, c.Period, c.InvoiceNumber,
                c.Amount, c.Allocations.Where(a => !a.IsDeleted).Sum(a => a.Amount), c.Route, c.Status, c.PaidAt, c.ReviewFlag, c.ExternalId))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<ServiceChargeDto>>(filas);
    }
}
