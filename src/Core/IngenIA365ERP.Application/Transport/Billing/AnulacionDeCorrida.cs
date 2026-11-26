using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Transport.Integration;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Billing;

// 2026-11-25 (pedido del dueño: «poder anularla y facturar sin restricción de forma masiva varias veces»): anular una corrida de facturación
// de servicios terminada, por tandas que pide la pantalla, para volver a facturar el período.

/// <summary>Una factura de la corrida que no se anuló, con el motivo.</summary>
public sealed record BillingRunVoidOmissionDto(Guid InvoicePublicId, string InvoiceNumber, string Plate, string? InternalNumber, string Code, string Reason);

/// <summary>Lo que hizo una tanda de la anulación: facturas y cargos anulados en ella, las que no se anulan y por qué, y cuántas faltan.</summary>
public sealed record BillingRunVoidDto(Guid RunPublicId, int VoidedInvoices, int VoidedCharges, IReadOnlyList<BillingRunVoidOmissionDto> Omitted, int Pending);

/// <summary>
/// Anula una tanda de las facturas de una corrida terminada (2026-11-25). Cada factura <b>abierta y sin pagos</b> —todos sus cargos abiertos,
/// sin nada asignado y sin aviso al sistema externo— se anula por Comercial: un comprobante no electrónico con su documento contrario
/// (<see cref="VoidInventoryDocumentCommand"/>, emite <c>DocumentoAnulado</c>) y una factura electrónica con su nota de anulación total
/// (<see cref="SaveCreditNoteDraftCommand"/> con <c>TotalVoid</c> y <see cref="ConfirmInventoryDocumentCommand"/>, emite
/// <c>NotaCreditoEmitida</c> y, si fue a crédito, el ajuste a Cartera). Sus cargos quedan anulados en la misma petición, así el vehículo se
/// puede volver a facturar en el período sin esperar a la bandeja; cuando llegue el mensaje, ya no hay nada que hacer. Lo pagado, abonado o
/// avisado no se toca y se informa. <c>Skip</c> son las facturas que fallaron en tandas anteriores: la pantalla no las vuelve a intentar.
/// La auditoría guarda el motivo (<see cref="IConMotivo"/>).
/// </summary>
public sealed record VoidServiceBillingRunCommand(Guid RunPublicId, string Reason, int BatchSize = 10, IReadOnlyList<Guid>? Skip = null)
    : IRequest<Result<BillingRunVoidDto>>, IConMotivo;

public sealed class VoidServiceBillingRunCommandValidator : ValidadorConMotivo<VoidServiceBillingRunCommand>
{
    public VoidServiceBillingRunCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.BatchSize).InclusiveBetween(1, 50);
    }
}

public sealed class VoidServiceBillingRunCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<VoidServiceBillingRunCommand, Result<BillingRunVoidDto>>
{
    public async Task<Result<BillingRunVoidDto>> Handle(VoidServiceBillingRunCommand request, CancellationToken ct)
    {
        var corrida = await db.TransportBillingRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (corrida is null) return Result.Failure<BillingRunVoidDto>("Billing.RunNotFound", "No existe esa corrida.");
        if (corrida.Status is BillingRunStatus.Queued or BillingRunStatus.Running)
            return Result.Failure<BillingRunVoidDto>("Billing.RunNotFinished", "La corrida sigue en curso: termínela con «Continuar» antes de anularla.");

        var saltar = request.Skip?.ToHashSet() ?? [];
        var cargos = await db.ServiceCharges.AsNoTracking().Include(c => c.Allocations).Include(c => c.Vehicle)
            .Where(c => c.BillingRunId == corrida.Id && c.Origin == ChargeOrigin.Billing && c.Status != ChargeStatus.Voided)
            .ToListAsync(ct);
        var omitidas = new List<BillingRunVoidOmissionDto>();
        var elegibles = new List<(Guid Factura, ServiceCharge Primero)>();
        foreach (var g in cargos.GroupBy(c => c.InvoicePublicId).OrderBy(g => g.Min(c => c.Id)))
        {
            if (saltar.Contains(g.Key)) continue;
            var primero = g.OrderBy(c => c.Id).First();
            if (g.FirstOrDefault(c => !ApplyChargeAllocationsCommandHandler.SePuedeAnular(c)) is { } bloquea)
                omitidas.Add(Omision(g.Key, primero, "Billing.InvoiceHasPayments", PorQueNo(bloquea)));
            else
                elegibles.Add((g.Key, primero));
        }

        var tanda = elegibles.Take(request.BatchSize).ToList();
        int facturas = 0, anulados = 0;
        foreach (var (factura, primero) in tanda)
        {
            Result r;
            try
            {
                r = await AnularFacturaAsync(factura, request.Reason.Trim(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo();
                r = Result.Failure("Billing.Unexpected", ex.Message.Length > 300 ? ex.Message[..300] : ex.Message);
            }
            if (r.IsFailure)
            {
                omitidas.Add(Omision(factura, primero, r.Error.Code, r.Error.Message));
                continue;
            }

            var vivos = await db.ServiceCharges.Include(c => c.Allocations)
                .Where(c => c.InvoicePublicId == factura && c.Status != ChargeStatus.Voided).ToListAsync(ct);
            foreach (var c in vivos.Where(ApplyChargeAllocationsCommandHandler.SePuedeAnular))
            {
                c.Status = ChargeStatus.Voided;
                anulados++;
            }
            await db.SaveChangesAsync(ct);
            facturas++;
        }

        return Result.Success(new BillingRunVoidDto(corrida.PublicId, facturas, anulados, omitidas, elegibles.Count - tanda.Count));
    }

    /// <summary>La anulación por Comercial: documento contrario o nota de anulación total. Ya anulada o acreditada del todo, nada que hacer.</summary>
    private async Task<Result> AnularFacturaAsync(Guid factura, string motivo, CancellationToken ct)
    {
        var f = await new FacturasDeServicios(db).ParaAnularAsync(factura, ct);
        if (f is null) return Result.Failure("Billing.InvoiceNotFound", "No se encontró la factura.");
        if (f.Status == DocumentStatus.Voided || f.AcreditadaDelTodo) return Result.Success();
        if (f.Status != DocumentStatus.Confirmed) return Result.Failure("Billing.InvoiceNotConfirmed", "La factura no está confirmada en Comercial.");

        if (f.SeAnulaConAnulacion)
        {
            var anulada = await sender.Send(new VoidInventoryDocumentCommand(factura, DocumentClassGroup.Sales, motivo) { OperationKey = Guid.NewGuid() }, ct);
            if (anulada.IsFailure) return Result.Failure(anulada.Error);
            return anulada.Value.Status == DocumentStatus.Confirmed
                ? Result.Success()
                : Result.Failure("Billing.VoidPendingApproval", "La anulación quedó pendiente de una aprobación en Comercial.");
        }

        // Una factura electrónica no se anula: se corrige con su nota de anulación total. Una nota que un intento anterior dejó en borrador se
        // reutiliza (otra en borrador contaría como acreditada y la nueva se rechazaría por exceder lo que queda).
        var entrada = new CreditNoteDraftInput(factura, motivo, TotalVoid: true, WithReturn: false, Lines: [], Refunds: []);
        var nota = await sender.Send(new SaveCreditNoteDraftCommand(f.NotaEnBorrador, entrada) { OperationKey = Guid.NewGuid() }, ct);
        if (nota.IsFailure) return Result.Failure(nota.Error);
        var confirmada = await sender.Send(new ConfirmInventoryDocumentCommand(nota.Value.PublicId, DocumentClassGroup.Sales) { OperationKey = Guid.NewGuid() }, ct);
        if (confirmada.IsFailure) return Result.Failure(confirmada.Error);
        return confirmada.Value.Status == DocumentStatus.Confirmed
            ? Result.Success()
            : Result.Failure("Billing.CreditNotePendingApproval", "La nota crédito quedó pendiente de una aprobación en Comercial.");
    }

    private static BillingRunVoidOmissionDto Omision(Guid factura, ServiceCharge c, string codigo, string motivo) =>
        new(factura, c.InvoiceNumber, c.Vehicle?.Plate ?? c.VehicleReference ?? "", c.Vehicle?.InternalNumber, codigo, motivo);

    private static string PorQueNo(ServiceCharge c) =>
        c.Status == ChargeStatus.Paid ? "Ya está pagada."
        : c.NotifiedRequestPublicId is not null ? "Ya se avisó al sistema externo."
        : c.Allocations.Any(a => !a.IsDeleted) ? "Tiene abonos."
        : "No está abierta.";
}
