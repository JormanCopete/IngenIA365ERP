using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Registrar que la cooperativa emitió por fuera el acuse (030) o el recibo del bien (032) de una factura del proveedor a
/// crédito (feature 012, T346; FR-050, US9-4, T42; contracts/api.md §14.8, <c>POST /supplier-invoices/{id}/radian-events</c>,
/// permiso <c>Inventory.Purchases.RegisterRadianEvent</c>). Con <c>Correct</c>, corrige un registro externo ya hecho y audita el
/// antes y el después. Devuelve la lista actualizada. (nuevo)
/// </summary>
public sealed record RegisterExternalRadianEventCommand(Guid SupplierInvoicePublicId, RegistrarEventoRadianRequest Evento)
    : IRequest<Result<IReadOnlyList<RadianEventDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterExternalRadianEventCommandValidator : AbstractValidator<RegisterExternalRadianEventCommand>
{
    public RegisterExternalRadianEventCommandValidator()
    {
        RuleFor(x => x.SupplierInvoicePublicId).NotEmpty();
        RuleFor(x => x.Evento).NotNull();
        RuleFor(x => x.Evento.EventCode).IsInEnum();
        RuleFor(x => x.Evento.Source).NotEmpty().MaximumLength(SupplierInvoiceEvent.LargoDeLaFuente);
        RuleFor(x => x.Evento.Cude).MaximumLength(SupplierInvoiceEvent.LargoDelCude);
        RuleFor(x => x.Evento.Notes).MaximumLength(SupplierInvoiceEvent.LargoDeLasNotas);
    }
}

/// <summary>
/// La factura existe, es del alcance y es una factura del proveedor (404 si no); la fuente es un portal (DIAN o proveedor);
/// la transición la decide <see cref="TransicionesDeEventoRadian"/> (<c>NotApplicable</c>, <c>OutOfOrder</c>, <c>DateInvalid</c>,
/// <c>AlreadyRegistered</c>). Si ya no queda ningún evento pendiente, atiende la alerta <c>Compras.EventosRadianFaltantes</c> de
/// la factura con una nota automática (lo hace el proceso: desapareció la causa).
/// </summary>
public sealed class RegisterExternalRadianEventCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    VistaDeDocumentos vista,
    IAlertas alertas,
    InventoryAuditEmitter auditoria)
    : IRequestHandler<RegisterExternalRadianEventCommand, Result<IReadOnlyList<RadianEventDto>>>
{
    public const string AccionDeCorreccion = "Inventory.RadianEvent.Corrected";

    public async Task<Result<IReadOnlyList<RadianEventDto>>> Handle(RegisterExternalRadianEventCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        var factura = await vista.BuscarAsync(request.SupplierInvoicePublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (factura is null || factura.Class != DocumentClass.SupplierInvoice) return Falla(InventoryErrors.DocumentNotFound());

        var e = request.Evento;
        if (e.Source is not (SupplierInvoiceEvent.FuenteDian or SupplierInvoiceEvent.FuenteProveedor)) return Falla(ErroresDeCompras.RadianSourceInvalid());

        var detalle = await db.SupplierInvoiceDetails.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == factura.Id, ct);
        var eventos = await db.SupplierInvoiceEvents.Where(x => x.DocumentId == factura.Id).ToListAsync(ct);
        if (detalle is null || eventos.Count == 0 || factura.Status != DocumentStatus.Confirmed)
            return Falla(ErroresDeCompras.Radian(TransicionesDeEventoRadian.CodigoNoAplica));

        var rechazo = TransicionesDeEventoRadian.RegistrarExterno(
            eventos.Select(x => new EventoRadianActual(x.EventCode, x.Status, x.EventDate)).ToList(),
            e.EventCode, e.Date, detalle.IssueDate, reloj.HoyLocal, e.Correct);
        if (rechazo is not null) return Falla(ErroresDeCompras.Radian(rechazo));

        var evento = eventos.First(x => x.EventCode == e.EventCode);
        var antes = e.Correct ? new { evento.EventDate, evento.Source, evento.Cude, evento.Notes } : null;
        evento.Status = SupplierInvoiceEventStatus.RegisteredExternally;
        evento.EventDate = e.Date;
        evento.Source = e.Source;
        evento.Cude = string.IsNullOrWhiteSpace(e.Cude) ? null : e.Cude.Trim().ToLowerInvariant();
        evento.Notes = string.IsNullOrWhiteSpace(e.Notes) ? null : e.Notes.Trim();
        evento.RegisteredByUserId = usuario;
        evento.RegisteredAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);

        if (antes is not null)
        {
            await auditoria.EmitAsync(AccionDeCorreccion, "SupplierInvoiceEvent", evento.PublicId, antes,
                new { evento.EventDate, evento.Source, evento.Cude, evento.Notes }, ct);
        }

        // Llegaron los dos: la alerta de la factura se atiende sola.
        if (!TransicionesDeEventoRadian.QuedaPendiente(eventos.Select(x => new EventoRadianActual(x.EventCode, x.Status, x.EventDate))))
            await alertas.AtenderPorProcesoAsync(RevisionDeEventosRadian.ClaveDeLaAlerta(factura.PublicId),
                "Se registraron el acuse de recibo (030) y el recibo del bien (032).", ct);

        return Result.Success(await ConsultasDeCompras.EventosAsync(db, factura.Id, ct));
    }

    private static Result<IReadOnlyList<RadianEventDto>> Falla(Error error) => Result.Failure<IReadOnlyList<RadianEventDto>>(error);
}

/// <summary>Los eventos RADIAN de una factura del proveedor (§14.8, <c>GET /supplier-invoices/{id}/radian-events</c>). (nuevo)</summary>
public sealed record ListRadianEventsQuery(Guid SupplierInvoicePublicId) : IRequest<Result<IReadOnlyList<RadianEventDto>>>;

/// <summary>Con alcance: la factura fuera de él (o de otra clase) es 404, como la inexistente.</summary>
public sealed class ListRadianEventsQueryHandler(IApplicationDbContext db, VistaDeDocumentos vista)
    : IRequestHandler<ListRadianEventsQuery, Result<IReadOnlyList<RadianEventDto>>>
{
    public async Task<Result<IReadOnlyList<RadianEventDto>>> Handle(ListRadianEventsQuery request, CancellationToken ct)
    {
        var factura = await vista.BuscarAsync(request.SupplierInvoicePublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (factura is null || factura.Class != DocumentClass.SupplierInvoice)
            return Result.Failure<IReadOnlyList<RadianEventDto>>(InventoryErrors.DocumentNotFound());
        return Result.Success(await ConsultasDeCompras.EventosAsync(db, factura.Id, ct));
    }
}
