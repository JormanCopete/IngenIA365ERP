using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Crea (<see cref="DocumentPublicId"/> nulo, <c>POST /</c>) o reemplaza (<c>PUT /{id}</c>) cabecera y líneas de un
/// borrador de la ruta de <see cref="ExpectedGroup"/> (feature 012, T144; contracts/api.md §9.3; FR-005, FR-017,
/// FR-018). No consume número. (nuevo)
/// </summary>
public sealed record SaveInventoryDraftCommand(Guid? DocumentPublicId, DocumentClassGroup ExpectedGroup, SaveInventoryDraftRequest Draft)
    : IRequest<Result<InventoryDocumentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>La forma del borrador: tipo, líneas con producto, unidad y cantidad positiva, decimales admitidos (§2.6).</summary>
public sealed class SaveInventoryDraftCommandValidator : AbstractValidator<SaveInventoryDraftCommand>
{
    public SaveInventoryDraftCommandValidator()
    {
        RuleFor(x => x.Draft).NotNull();
        RuleFor(x => x.Draft.DocumentTypePublicId).NotEmpty().WithMessage("Indicá el tipo de documento.");
        RuleFor(x => x.Draft.Lines).NotNull().WithMessage("Indicá las líneas (una lista vacía guarda el borrador sin líneas).");
        RuleFor(x => x.Draft.ExternalReference).MaximumLength(60);
        RuleFor(x => x.Draft.Reason).MaximumLength(500);
        RuleFor(x => x.Draft.Notes).MaximumLength(1000);
        RuleFor(x => x.Draft.ExchangeRate).GreaterThan(0).When(x => x.Draft.ExchangeRate is not null);
        RuleForEach(x => x.Draft.Lines).ChildRules(l =>
        {
            // Una línea con origen (recepción o factura, compras US9) toma de él producto, unidad y, en una nota, la cantidad.
            l.RuleFor(x => x.ProductPublicId).NotEmpty().When(x => x.Origen is null).WithMessage("Cada línea lleva su producto.");
            l.RuleFor(x => x.UnitPublicId).NotEmpty().When(x => x.Origen is null).WithMessage("Cada línea lleva su unidad.");
            l.RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.InvoiceLinePublicId is null || x.Quantity != 0)
                .WithMessage("La cantidad va positiva: el signo lo pone la clase.");
            l.RuleFor(x => x.Quantity).Must(q => Decimales(q) <= 4).WithMessage("La cantidad admite hasta 4 decimales.");
            l.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).Must(a => a is null || Decimales(a.Value) <= 2)
                .WithMessage("El valor de la línea admite hasta 2 decimales.");
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).Must(p => p is null || Decimales(p.Value) <= 6)
                .WithMessage("El precio unitario admite hasta 6 decimales.");
            l.RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).Must(c => c is null || Decimales(c.Value) <= 6)
                .WithMessage("El costo unitario admite hasta 6 decimales.");
            l.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).When(x => x.DiscountPercent is not null);
            l.RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).Must(d => d is null || Decimales(d.Value) <= 2)
                .WithMessage("El descuento en pesos admite hasta 2 decimales.");
            l.RuleFor(x => x.Notes).MaximumLength(200);
        });
    }

    /// <summary>Cuántos decimales significativos tiene <paramref name="valor"/>.</summary>
    public static int Decimales(decimal valor)
    {
        valor = Math.Abs(valor);
        var escala = 0;
        while (valor != Math.Truncate(valor) && escala < 28)
        {
            valor *= 10;
            escala++;
        }
        return escala;
    }
}

/// <summary>
/// Guardar un borrador (§9.3): el tipo existe, está activo y es del grupo de la ruta; la clase opera en este despliegue;
/// lo referenciado existe y está en el alcance (si no, 404); unidad de cada producto y sus decimales
/// (<c>Inventory.Unit.*</c>); calcula factor, cantidad base, residuo de conversión y totales. Lo que hoy impediría
/// confirmar (campos del tipo, fecha, período, bodega no activa, existencia) no bloquea: vuelve en <c>warnings[]</c> con
/// el código que daría la confirmación. Un solo guardado.
/// </summary>
public sealed class SaveInventoryDraftCommandHandler(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj,
    EfectosDeClase efectos,
    VistaDeDocumentos vista,
    IEnumerable<IBorradorDeGrupo>? borradoresDeGrupo = null)
    : IRequestHandler<SaveInventoryDraftCommand, Result<InventoryDocumentDto>>
{
    private readonly IReadOnlyList<IBorradorDeGrupo> _deGrupo = borradoresDeGrupo?.ToList() ?? [];

    public async Task<Result<InventoryDocumentDto>> Handle(SaveInventoryDraftCommand request, CancellationToken ct)
    {
        var borrador = request.Draft;
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) El documento existente: del alcance y del grupo (si no, 404), en borrador, sin cambios desde que se leyó.
        InventoryDocument? documento = null;
        if (request.DocumentPublicId is { } id)
        {
            documento = await vista.BuscarAsync(id, request.ExpectedGroup, seguir: true, ct);
            if (documento is null) return Falla(InventoryErrors.DocumentNotFound());
            if (documento.Status != DocumentStatus.Draft) return Falla(InventoryErrors.NotDraft(documento.Status));
            if (borrador.RowVersion is { Length: > 0 } leida && documento.RowVersion is { Length: > 0 } actual && !leida.AsSpan().SequenceEqual(actual))
                return Falla(Error.StaleRowVersion);
        }

        // (2) El tipo: existe, es del grupo de la ruta, su clase opera; un tipo inactivo no sirve para lo nuevo.
        var tipo = await db.InventoryDocumentTypes.Include(t => t.Warehouses)
            .FirstOrDefaultAsync(t => t.PublicId == borrador.DocumentTypePublicId, ct);
        if (tipo is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        var clase = ClasesDeDocumento.De(tipo.Class);
        if (clase.Group != request.ExpectedGroup || !clase.ManualCreation)
            return Falla(InventoryErrors.TypeNotForRoute(tipo.Class, clase.Group ?? request.ExpectedGroup));
        // US10 (T370): por la ruta de traslados sólo se arma el despacho; la recepción la crea y confirma ReceiveTransferCommand.
        if (request.ExpectedGroup == DocumentClassGroup.Transfers && tipo.Class != DocumentClass.TransferDispatch)
            return Falla(InventoryErrors.TypeNotForRoute(tipo.Class, DocumentClassGroup.Transfers));
        if (!tipo.IsActive && (documento is null || documento.DocumentTypeId != tipo.Id)) return Falla(InventoryErrors.DocumentTypeInactive(tipo.Code));
        var efecto = efectos.Para(tipo.Class);
        if (efecto.IsFailure) return Falla(efecto.Error);

        // (2b) Lo que agrega el grupo (compras, US9): sin implementación, sus campos no se admiten.
        var deGrupo = _deGrupo.FirstOrDefault(g => g.Grupo == request.ExpectedGroup);
        if (deGrupo is null && borrador.TraeCamposDeCompra)
            return Falla(new Error("Validation.Invalid", $"Los campos de compras no aplican a un documento de clase {tipo.Class}."));
        if (deGrupo is not null)
        {
            var preparado = await deGrupo.PrepararAsync(tipo, borrador, documento, ct);
            if (preparado.IsFailure) return Falla(preparado.Error);
            borrador = preparado.Value;
        }

        // (3) Moneda y tamaño.
        if ((borrador.Currency is { } moneda && !string.Equals(moneda, InventoryDocument.MonedaPorDefecto, StringComparison.OrdinalIgnoreCase))
            || (borrador.ExchangeRate is { } tasa && tasa != 1m))
        {
            return Falla(InventoryErrors.CurrencyNotSupported());
        }
        if (borrador.Lines.Count > InventoryDocument.MaxLineas) return Falla(InventoryErrors.TooManyLines());

        // (4) Lo referenciado en la cabecera: existe y está en el alcance (fuera = 404).
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var pedidas = new[] { borrador.WarehousePublicId, borrador.DestinationWarehousePublicId }.OfType<Guid>().Distinct().ToList();
        var bodegas = (await maestros.BodegasAsync(pedidas, ct)).ToDictionary(b => b.PublicId);
        BodegaDelDocumento? bodega = null, destino = null;
        if (borrador.WarehousePublicId is { } b)
        {
            if (!bodegas.TryGetValue(b, out bodega) || !alcance.IncluyeBodega(bodega.Id)) return Falla(ErroresDeAlcance.BodegaInexistente());
        }
        if (borrador.DestinationWarehousePublicId is { } d)
        {
            // US10 (api.md §11, §17.2): el destino de un traslado puede ser cualquier bodega de la cooperativa; el despacho no mueve su
            // existencia (la recepción sí exige el destino en el alcance).
            var destinoLibre = request.ExpectedGroup == DocumentClassGroup.Transfers;
            if (!bodegas.TryGetValue(d, out destino) || (!destinoLibre && !alcance.IncluyeBodega(destino.Id))) return Falla(ErroresDeAlcance.BodegaInexistente());
        }

        int? centroId = null;
        if (borrador.CostCenterPublicId is { } cc)
        {
            centroId = await db.CostCenters.Where(c => c.PublicId == cc).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (centroId is null) return Falla(ErroresDelDocumento.CentroDeCostoInexistente());
        }
        int? personaId = null;
        if (borrador.Contraparte is { } per)
        {
            personaId = await db.People.Where(p => p.PublicId == per).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (personaId is null) return Falla(ErroresDelDocumento.PersonaInexistente());
        }
        int? causaId = null;
        if (borrador.AdjustmentCausePublicId is { } ca)
        {
            var causa = await maestros.CausaDeAjusteAsync(ca, ct);
            if (causa is null) return Falla(ErroresDelDocumento.CausaInexistente());
            causaId = causa.Id;
        }

        // (5) Las líneas: producto, unidad para ese producto y decimales, ubicaciones de la bodega; cantidades y montos.
        var productos = (await maestros.ProductosAsync(borrador.Lines.Select(l => l.ProductPublicId).Distinct().ToList(), ct))
            .ToDictionary(p => p.PublicId);
        var ubicaciones = (await maestros.UbicacionesAsync(
                borrador.Lines.SelectMany(l => new[] { l.LocationPublicId, l.ToLocationPublicId }).OfType<Guid>().Distinct().ToList(), ct))
            .ToDictionary(u => u.PublicId);

        var calculadas = new List<LineaCalculada>(borrador.Lines.Count);
        for (var i = 0; i < borrador.Lines.Count; i++)
        {
            var linea = borrador.Lines[i];
            var numero = i + 1;
            if (!productos.TryGetValue(linea.ProductPublicId, out var producto)) return Falla(ErroresDelDocumento.ProductoInexistente());

            var unidad = await maestros.UnidadAsync(producto.Id, linea.UnitPublicId, ct);
            if (unidad is null) return Falla(InventoryErrors.UnitNotForProduct(numero, producto.Code, string.Empty));

            // FR-017, T205: la conversión a la unidad base la hace el motor puro, igual que el POS y los conteos.
            var conversion = ConversionDeUnidades.Convertir(new PedidoDeConversion(numero, unidad.Code, linea.Quantity, unidad.Factor,
                unidad.DecimalesPermitidos, unidad.BaseUnitCode ?? unidad.Code, unidad.DecimalesDeLaBase));
            if (conversion.Rechazo is { } rechazo)
                return Falla(InventoryErrors.UnitDecimalsNotAllowed(numero, producto.Code, rechazo.UnitCode, rechazo.AllowedDecimals, rechazo.QuantityBase));
            var baseRedondeada = conversion.QuantityBase;

            int? ubicacionId = null, haciaId = null;
            if (linea.LocationPublicId is { } lu)
            {
                if (!ubicaciones.TryGetValue(lu, out var u) || !alcance.IncluyeBodega(u.WarehouseId)) return Falla(ErroresDelDocumento.UbicacionInexistente());
                if (bodega is not null && u.WarehouseId != bodega.Id) return Falla(InventoryErrors.LocationNotInWarehouse(numero, producto.Code));
                ubicacionId = u.Id;
            }
            if (linea.ToLocationPublicId is { } th)
            {
                if (!ubicaciones.TryGetValue(th, out var u) || !alcance.IncluyeBodega(u.WarehouseId)) return Falla(ErroresDelDocumento.UbicacionInexistente());
                var bodegaDeLlegada = destino ?? bodega;
                if (bodegaDeLlegada is not null && u.WarehouseId != bodegaDeLlegada.Id) return Falla(InventoryErrors.LocationNotInWarehouse(numero, producto.Code));
                haciaId = u.Id;
            }

            var precio = linea.UnitPrice ?? 0m;
            var bruto = Math.Round(linea.Quantity * precio, 2, MidpointRounding.AwayFromZero);
            var descuento = linea.DiscountAmount
                ?? (linea.DiscountPercent is { } pct ? Math.Round(bruto * pct / 100m, 2, MidpointRounding.AwayFromZero) : 0m);
            if (descuento > bruto) descuento = bruto;
            decimal? costoTotal = linea.UnitCost is { } costo ? Math.Round(baseRedondeada * costo, 2, MidpointRounding.AwayFromZero) : null;

            calculadas.Add(new LineaCalculada(linea, numero, producto.Id, unidad.Id, unidad.Factor, baseRedondeada, conversion.RoundingQuantity,
                precio, bruto, descuento, costoTotal, ubicacionId, haciaId));
        }

        // (6) Cabecera: nueva o reemplazada.
        var hoy = reloj.HoyLocal;
        var nuevo = documento is null;
        if (documento is null)
        {
            documento = new InventoryDocument
            {
                CreatedByUserId = usuario,
                Currency = InventoryDocument.MonedaPorDefecto,
                ExchangeRate = 1m,
            };
            db.InventoryDocuments.Add(documento);
        }
        documento.Class = tipo.Class;
        documento.DocumentTypeId = tipo.Id;
        documento.DocumentType = tipo;
        documento.OperationDate = borrador.OperationDate ?? (nuevo ? hoy : documento.OperationDate);
        documento.WarehouseId = bodega?.Id;
        documento.DestinationWarehouseId = destino?.Id;
        documento.BranchId = bodega?.BranchId ?? destino?.BranchId ?? documento.BranchId;
        documento.CostCenterId = centroId;
        documento.CounterpartyPersonId = personaId;
        documento.SalesChannelId = tipo.SalesChannelId;
        documento.ExternalReference = Limpio(borrador.ExternalReference);
        documento.Reason = Limpio(borrador.Reason);
        documento.Notes = Limpio(borrador.Notes);

        // (7) Líneas: las que traen su PublicId se conservan; las nuevas se agregan; las que faltan se dan de baja.
        var existentes = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.PublicId);
        var conservadas = new HashSet<Guid>();
        foreach (var c in calculadas)
        {
            InventoryDocumentLine linea;
            if (c.Pedida.LinePublicId is { } lp && existentes.TryGetValue(lp, out var previa))
            {
                linea = previa;
                conservadas.Add(lp);
            }
            else
            {
                linea = new InventoryDocumentLine { Document = documento };
                documento.Lines.Add(linea);
            }
            linea.LineNumber = c.Numero;
            linea.ProductId = c.ProductoId;
            linea.UnitId = c.UnidadId;
            linea.Quantity = c.Pedida.Quantity;
            linea.Factor = c.Factor;
            linea.QuantityBase = c.CantidadBase;
            linea.RoundingQuantity = c.Residuo;
            linea.UnitPrice = c.Precio;
            linea.GrossAmount = c.Bruto;
            linea.DiscountAmount = c.Descuento;
            linea.NetAmount = c.Bruto - c.Descuento;
            linea.UnitCost = c.Pedida.UnitCost;
            linea.TotalCost = c.CostoTotal;
            linea.LocationId = c.UbicacionId;
            linea.ToLocationId = c.HaciaId;
            linea.AdjustmentCauseId = causaId;
            linea.Description = Limpio(c.Pedida.Notes);
            linea.AffectsCost = c.Pedida.AffectsCost == true;
        }
        foreach (var sobrante in existentes.Values.Where(l => !conservadas.Contains(l.PublicId)))
        {
            // Principio VII: baja lógica. UK_INV_DocumentLines_Document_LineNumber está filtrado a las vivas, así el número
            // se puede reusar.
            sobrante.IsDeleted = true;
            sobrante.DeletedAt = reloj.UtcNow;
            sobrante.DeletedBy = actor.Name;
        }

        // (8) Totales (T26): sin impuestos en el ciclo común; compras y ventas los calculan en su estrategia.
        var vivas = documento.Lines.Where(l => !l.IsDeleted).ToList();
        documento.Subtotal = vivas.Sum(l => l.GrossAmount);
        documento.DiscountTotal = vivas.Sum(l => l.DiscountAmount);
        documento.Total = documento.Subtotal - documento.DiscountTotal + documento.TaxTotal;
        documento.AmountDue = documento.Total - documento.WithholdingTotal;
        documento.CostTotal = vivas.Sum(l => l.TotalCost ?? 0m);

        // (8b) Lo del grupo: satélites, vínculos, impuestos y totales (compras).
        var enCurso = new BorradorEnCurso(documento, tipo, borrador, bodega);
        var delGrupo = ResultadoDelBorrador.Vacio;
        if (deGrupo is not null)
        {
            var aplicado = await deGrupo.AplicarAsync(enCurso, ct);
            if (aplicado.IsFailure)
            {
                // Nada de este intento queda en el seguimiento: otro guardado de la misma unidad de trabajo no lo arrastra.
                db.DescartarCambios();
                return Falla(aplicado.Error);
            }
            delGrupo = aplicado.Value;
            if (documento.WarehouseId is int propia && propia != bodega?.Id)
                bodega = (await maestros.BodegasPorIdAsync([propia], ct)).FirstOrDefault();
        }

        // (9) Avisos: lo que impediría confirmar hoy, comunes, del grupo y de la clase.
        var corte = await maestros.CorteAsync(ct);
        var avisos = ReglasDelDocumento.Evaluar(documento, tipo, bodega, corte, hoy).ToList();
        avisos.AddRange(delGrupo.Avisos);
        avisos.AddRange(await efecto.Value.AvisosDelBorradorAsync(new ContextoDeEfecto(documento, tipo, clase), ct));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (deGrupo is not null)
        {
            var colision = await deGrupo.TraducirColisionAsync(ex, enCurso, ct);
            if (colision is null) throw;
            return Falla(colision);
        }

        var detalle = await vista.DetalleAsync(documento, avisos.Select(ReglasDelDocumento.ComoAviso).ToList(), ct);
        return Result.Success(delGrupo.ImpuestosPrevistos.Count > 0 && detalle.TaxLines.Count == 0
            ? detalle with { TaxLines = delGrupo.ImpuestosPrevistos }
            : detalle);
    }

    private static Result<InventoryDocumentDto> Falla(Error error) => Result.Failure<InventoryDocumentDto>(error);

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private sealed record LineaCalculada(
        SaveInventoryDraftLine Pedida,
        int Numero,
        int ProductoId,
        int UnidadId,
        decimal Factor,
        decimal CantidadBase,
        decimal Residuo,
        decimal Precio,
        decimal Bruto,
        decimal Descuento,
        decimal? CostoTotal,
        int? UbicacionId,
        int? HaciaId);
}
