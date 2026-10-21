using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Comercial factura el cargo gravado de una cuenta de captación (feature 014, decisión del dueño del 2026-10-18, D51; cierra T117; mensajes.md §5.5;
/// FR-128): consume <c>CargoDeCaptacionCausado</c> v1 y <c>CargoDeCaptacionReversado</c> v1 de la Cartera por su flujo de ventas existente. Sin ruta
/// (<c>LosComandosDeConsumoNoTienenRuta</c>): lo manda <see cref="DestinoInventario"/>; es <see cref="IReintentableAnteConcurrencia"/>. (nuevo)
/// <list type="bullet">
/// <item><b>Causado</b>: deja <b>lista</b> la factura de venta del servicio —un borrador de la clase <c>SalesInvoice</c> por <see cref="SaveInventoryDraftCommand"/>
/// (grupo <c>Sales</c>) con una línea del ítem comercial del cargo (<c>CommercialItemCode</c> = <c>INV_Products.Code</c>), cantidad 1, precio la base
/// del IVA, el titular como adquirente y la referencia externa <c>CGO:{cargo:N}</c>—; el IVA lo calcula Comercial con su catálogo al precificar. El tipo
/// de documento y la bodega los fija la cooperativa (<c>Ventas.TipoDeFacturaDeCargosDeCartera</c> y <c>Ventas.BodegaDeCargosDeCartera</c>; vacíos, el
/// primer tipo activo de factura de venta y la primera bodega activa). Confirmarla y emitirla electrónicamente es del flujo de ventas (I3/I4); la
/// respuesta <c>FacturaDeCargoRegistrada</c> la emitirá esa emisión.</item>
/// <item><b>Reversado</b>: si la factura sigue en borrador, la descarta; si ya se confirmó, deja lista la nota crédito que la anula entera
/// (<see cref="SaveCreditNoteDraftCommand"/>, <c>TotalVoid</c>).</item>
/// </list>
/// Lo que falta en la configuración (ítem, tipo o bodega) es <c>Rejected</c> con su código: el mensaje queda en la bandeja para reprocesarlo. Repetir el mismo
/// cargo no crea otra factura (la referencia externa es la llave). (nuevo)
/// </summary>
public sealed record ConsumeDepositChargeMessageCommand(IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeDepositChargeMessageCommandValidator : AbstractValidator<ConsumeDepositChargeMessageCommand>
{
    public ConsumeDepositChargeMessageCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeDepositChargeMessageCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ISender sender,
    ILectorDeParametros parametros,
    IDateTimeService reloj) : IRequestHandler<ConsumeDepositChargeMessageCommand, Result<ResultadoDeConsumo>>
{
    public const string CodigoItemInexistente = "Inventory.ChargeInvoice.ItemNotFound";
    public const string CodigoSinTipo = "Inventory.ChargeInvoice.DocumentTypeMissing";
    public const string CodigoSinBodega = "Inventory.ChargeInvoice.WarehouseMissing";

    /// <summary>La referencia externa de la factura de un cargo: <c>CGO:{cargo:N}</c>.</summary>
    public static string Referencia(Guid cargo) => $"CGO:{cargo:N}";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeDepositChargeMessageCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Inventory, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);

        Guid? referencia = null;
        foreach (var m in leidos.Value)
        {
            var r = m.Payload switch
            {
                CargoDeCaptacionCausadoV1 causado when m.Version == 1 => await FacturarAsync(causado, ct),
                CargoDeCaptacionReversadoV1 reversado when m.Version == 1 => await AnularAsync(reversado, ct),
                _ => Result.Failure<Guid?>(new Error("Integration.VersionNotAccepted", $"Comercial no acepta {m.Type} v{m.Version}.")),
            };
            if (r.IsFailure) return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message));
            referencia ??= r.Value;
        }
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(referencia, null, null, MotivoSinComprobante.Informational));
    }

    /// <summary>El borrador de la factura del cargo (o el que ya existe con su referencia).</summary>
    private async Task<Result<Guid?>> FacturarAsync(CargoDeCaptacionCausadoV1 c, CancellationToken ct)
    {
        var llave = Referencia(c.Charge.ChargePublicId);
        var existente = await db.InventoryDocuments.AsNoTracking().Where(d => d.ExternalReference == llave && !d.IsDeleted)
            .Select(d => (Guid?)d.PublicId).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Success(existente);

        var codigo = c.Charge.CommercialItemCode?.Trim();
        var producto = string.IsNullOrEmpty(codigo)
            ? null
            : await db.Products.AsNoTracking().Where(p => p.Code == codigo && !p.IsDeleted && p.Status == ProductStatus.Active)
                .Select(p => new { p.PublicId, p.BaseUnitId }).FirstOrDefaultAsync(ct);
        if (producto is null)
            return Result.Failure<Guid?>(new ErrorConDatos(CodigoItemInexistente,
                $"El cargo {c.Charge.Code} no tiene un ítem comercial activo ({codigo ?? "sin código"}): créelo en el catálogo y reprocese el mensaje.",
                new { itemCode = codigo }));
        var unidad = await db.UnitsOfMeasure.AsNoTracking().Where(u => u.Id == producto.BaseUnitId).Select(u => u.PublicId).FirstAsync(ct);

        var hoy = reloj.HoyLocal;
        var tipo = await TipoAsync(hoy, ct);
        if (tipo is null)
            return Result.Failure<Guid?>(new Error(CodigoSinTipo,
                "No hay un tipo de documento de factura de venta para facturar los cargos de captación: fije Ventas.TipoDeFacturaDeCargosDeCartera."));
        var bodega = await BodegaAsync(hoy, ct);
        if (bodega is null)
            return Result.Failure<Guid?>(new Error(CodigoSinBodega,
                "No hay una bodega para facturar los cargos de captación: fije Ventas.BodegaDeCargosDeCartera."));

        var borrador = new SalesDraftInput(tipo.Value, bodega.Value,
            [new SalesLineInput(producto.PublicId, unidad, 1m, c.AmountBeforeTax, Notes: $"{c.Charge.Name} · cuenta {c.Account.AccountNumber}")],
            [], c.CausedOn, c.HolderPersonPublicId, ExternalReference: llave,
            Notes: $"Cargo {c.Charge.Code} de la cuenta {c.Account.AccountNumber} ({c.Charge.Kind}), causado el {c.CausedOn:yyyy-MM-dd}.");
        var guardado = await sender.Send(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, borrador.ComoBorrador())
        {
            OperationKey = c.Charge.ChargePublicId,
        }, ct);
        return guardado.IsSuccess ? Result.Success<Guid?>(guardado.Value.PublicId) : Result.Failure<Guid?>(guardado.Error);
    }

    /// <summary>Descarta el borrador o deja lista la nota crédito total de la factura confirmada.</summary>
    private async Task<Result<Guid?>> AnularAsync(CargoDeCaptacionReversadoV1 c, CancellationToken ct)
    {
        var llave = Referencia(c.Charge.ChargePublicId);
        var factura = await db.InventoryDocuments.AsNoTracking().Where(d => d.ExternalReference == llave && !d.IsDeleted)
            .Select(d => new { d.PublicId, d.Status }).FirstOrDefaultAsync(ct);
        if (factura is null) return Result.Success<Guid?>(null);
        if (factura.Status == DocumentStatus.Draft)
        {
            var descartado = await sender.Send(new DiscardInventoryDraftCommand(factura.PublicId, DocumentClassGroup.Sales,
                $"Se reversó el cargo {c.Charge.Code} de la cuenta {c.Account.AccountNumber}") { OperationKey = c.Charge.ChargePublicId }, ct);
            return descartado.IsSuccess ? Result.Success<Guid?>(factura.PublicId) : Result.Failure<Guid?>(descartado.Error);
        }
        if (factura.Status != DocumentStatus.Confirmed) return Result.Success<Guid?>(factura.PublicId);
        var nota = await sender.Send(new SaveCreditNoteDraftCommand(null, new CreditNoteDraftInput(factura.PublicId,
            $"Se reversó el cargo {c.Charge.Code} de la cuenta {c.Account.AccountNumber}", TotalVoid: true, WithReturn: false, [], []))
        {
            OperationKey = c.Charge.ChargePublicId,
        }, ct);
        return nota.IsSuccess ? Result.Success<Guid?>(nota.Value.PublicId) : Result.Failure<Guid?>(nota.Error);
    }

    private async Task<Guid?> TipoAsync(DateOnly hoy, CancellationToken ct)
    {
        var codigo = await TextoAsync(ParametrosDeInventario.VentasTipoDeFacturaDeCargosDeCartera, hoy, ct);
        var tipos = db.InventoryDocumentTypes.AsNoTracking().Where(t => t.Class == DocumentClass.SalesInvoice && t.IsActive && !t.IsDeleted);
        if (!string.IsNullOrEmpty(codigo)) tipos = tipos.Where(t => t.Code == codigo);
        return await tipos.OrderBy(t => t.Id).Select(t => (Guid?)t.PublicId).FirstOrDefaultAsync(ct);
    }

    private async Task<Guid?> BodegaAsync(DateOnly hoy, CancellationToken ct)
    {
        var codigo = await TextoAsync(ParametrosDeInventario.VentasBodegaDeCargosDeCartera, hoy, ct);
        var bodegas = db.Warehouses.AsNoTracking().Where(w => w.IsActive && !w.IsDeleted && w.Behavior == WarehouseBehavior.Operational);
        if (!string.IsNullOrEmpty(codigo)) bodegas = bodegas.Where(w => w.Code == codigo);
        return await bodegas.OrderBy(w => w.Id).Select(w => (Guid?)w.PublicId).FirstOrDefaultAsync(ct);
    }

    private async Task<string?> TextoAsync(string clave, DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, hoy, ct: ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto.Trim() : null;
    }
}
