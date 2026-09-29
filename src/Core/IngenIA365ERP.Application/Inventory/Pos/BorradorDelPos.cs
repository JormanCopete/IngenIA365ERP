using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Units;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>La sesión abierta del usuario con su caja (y los tipos por rol) y su punto. (nuevo)</summary>
public sealed record SesionDelPos(CashSession Sesion, CashRegister Caja, PointOfSale Punto);

/// <summary>
/// Lo que cambia en una línea: la cantidad, el precio digitado (en la base de la lista) o el descuento manual. <c>null</c> = no se
/// toca; un descuento con porcentaje y valor en cero lo quita; <see cref="QuitarPrecioDigitado"/> vuelve al precio de lista. (nuevo)
/// </summary>
public sealed record CambioDeLinea(decimal? Quantity = null, decimal? PrecioDigitado = null, PosDiscountInput? Descuento = null, bool QuitarPrecioDigitado = false);

/// <summary>
/// Un producto encontrado por código: la unidad que el código trae (la de empaque o la base) y su factor. <see cref="SerialId"/> (I6,
/// T929): lo leído era la serie de una unidad en existencia. (nuevo)
/// </summary>
public sealed record ProductoLeido(Product Producto, int UnitId, Guid UnitPublicId, string UnitCode, decimal Factor, int? SerialId = null);

/// <summary>
/// El borrador de la venta del POS (feature 012, I3, T603–T606; contracts/api.md §20.2; T50): la venta vive en el servidor desde la
/// primera lectura, ligada a la sesión y sin número. Este servicio reúne lo que comparten los comandos y las consultas:
/// <list type="bullet">
/// <item>la sesión abierta del usuario (<c>Inventory.CashSession.NotOpen</c>) y la guardia de punto sin POS (FR-058,
/// <c>Inventory.Pos.NotEnabled</c>);</item>
/// <item>la búsqueda exacta por código de barras (el de empaque trae su unidad y factor) o por código de producto;</item>
/// <item>la precificación de todas las líneas por <see cref="PrecificacionDeVenta"/> —la única que pone precio, descuentos e
/// impuestos—, y su aplicación <b>conservando</b> las filas de descuento que no cambiaron, para que su aprobación (atada al
/// <c>PublicId</c> y a la huella de la línea) no se pierda al leer otro producto; los descuentos sobre el tope piden su aprobación
/// por <see cref="AprobacionDeDescuentos"/>;</item>
/// <item>el <see cref="PosDraftDto"/> con los medios ofrecidos por <see cref="DisponibilidadDeMedio"/>, las aprobaciones pendientes
/// y los avisos.</item>
/// </list>
/// Una línea que no cambió se vuelve a medir por el <b>valor</b> de sus descuentos; la que cambió de cantidad, por su
/// <b>fracción</b> (un 10 % sigue siendo 10 %). El descuento por total se conserva en pesos. (nuevo)
/// </summary>
public sealed class BorradorDelPos(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IMaestrosDelDocumento maestros,
    PrecificacionDeVenta precificacion,
    AprobacionDeDescuentos aprobaciones,
    IMotorDeAprobaciones motor,
    IPermissionChecker permisos,
    ReglasDeSeguimiento? seguimiento = null)
{
    public const string PermisoCredito = "Inventory.Sales.SellOnCredit";

    public IDateTimeService Reloj => reloj;

    // ------------------------------------------------------------------------------------------ quién y dónde --

    /// <summary>El usuario de <c>SEC_Users</c> que vende; sin él no hay sesión posible.</summary>
    public async Task<Result<(int UserId, string Name)>> UsuarioAsync(CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        return actor.UserId is int id
            ? Result.Success((id, actor.Name))
            : Result.Failure<(int, string)>(ErroresDelPos.CashSessionNotOpen());
    }

    /// <summary>
    /// La sesión abierta del usuario, con su caja, sus tipos por rol y su punto. Cerrada, de otro cajero o inexistente →
    /// <c>Inventory.CashSession.NotOpen</c>; punto fuera del alcance → el 404 del punto; con <paramref name="exigirPos"/> y el
    /// punto con <c>PosEnabled = false</c> → <c>Inventory.Pos.NotEnabled</c> (FR-058).
    /// </summary>
    public async Task<Result<SesionDelPos>> SesionAbiertaAsync(Guid sesionPublicId, bool exigirPos, CancellationToken ct)
    {
        var usuario = await UsuarioAsync(ct);
        if (usuario.IsFailure) return Result.Failure<SesionDelPos>(usuario.Error);
        var sesion = await db.CashSessions.FirstOrDefaultAsync(s => s.PublicId == sesionPublicId, ct);
        return await SesionAsync(sesion, usuario.Value.UserId, exigirPos, ct);
    }

    /// <summary>La sesión de la venta, si sigue abierta y es del usuario.</summary>
    public async Task<Result<SesionDelPos>> SesionDeLaVentaAsync(InventoryDocument venta, bool exigirPos, CancellationToken ct)
    {
        var usuario = await UsuarioAsync(ct);
        if (usuario.IsFailure) return Result.Failure<SesionDelPos>(usuario.Error);
        var sesion = venta.CashSessionId is int id ? await db.CashSessions.FirstOrDefaultAsync(s => s.Id == id, ct) : null;
        return await SesionAsync(sesion, usuario.Value.UserId, exigirPos, ct);
    }

    private async Task<Result<SesionDelPos>> SesionAsync(CashSession? sesion, int usuario, bool exigirPos, CancellationToken ct)
    {
        if (sesion is null || !sesion.EstaAbierta || sesion.CashierUserId != usuario)
            return Result.Failure<SesionDelPos>(ErroresDelPos.CashSessionNotOpen());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!alcance.IncluyePunto(sesion.PointOfSaleId)) return Result.Failure<SesionDelPos>(ErroresDePuntoDeVenta.PointOfSaleNotFound());

        var caja = await db.CashRegisters.Include(c => c.DocumentTypes.Where(t => !t.IsDeleted)).ThenInclude(t => t.DocumentType)
            .FirstAsync(c => c.Id == sesion.CashRegisterId, ct);
        var punto = await db.PointsOfSale.FirstAsync(p => p.Id == sesion.PointOfSaleId, ct);
        if (exigirPos && !punto.PosEnabled) return Result.Failure<SesionDelPos>(ErroresDelPos.NotEnabled(punto.Code));
        return Result.Success(new SesionDelPos(sesion, caja, punto));
    }

    /// <summary>
    /// La venta del POS por su id: un documento de ventas con punto de venta, en el alcance del usuario. Con
    /// <paramref name="soloBorrador"/>, fuera de <c>Draft</c> → <c>Inventory.Document.NotDraft</c>.
    /// </summary>
    public async Task<Result<InventoryDocument>> VentaAsync(Guid ventaPublicId, bool soloBorrador, CancellationToken ct)
    {
        var venta = await db.InventoryDocuments.Include(d => d.Lines).Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.PublicId == ventaPublicId && d.PointOfSaleId != null, ct);
        if (venta is null || ClasesDeDocumento.De(venta.Class).Group != DocumentClassGroup.Sales)
            return Result.Failure<InventoryDocument>(ErroresDelPos.DraftNotFound());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!alcance.IncluyePunto(venta.PointOfSaleId!.Value)) return Result.Failure<InventoryDocument>(ErroresDelPos.DraftNotFound());
        if (soloBorrador && venta.Status != DocumentStatus.Draft) return Result.Failure<InventoryDocument>(InventoryErrors.NotDraft(venta.Status));
        return Result.Success(venta);
    }

    /// <summary>El tipo de la caja para el rol; sin él → <c>Inventory.Pos.RoleNotConfigured</c>.</summary>
    public static Result<InventoryDocumentType> TipoDelRol(CashRegister caja, CashRegisterDocumentRole rol)
    {
        var tipo = caja.DocumentTypes.FirstOrDefault(t => t.Role == rol && !t.IsDeleted)?.DocumentType;
        return tipo is null ? Result.Failure<InventoryDocumentType>(ErroresDelPos.RoleNotConfigured(caja.Code, rol)) : Result.Success(tipo);
    }

    /// <summary>El consumidor final genérico del maestro (<c>ConsumidorFinalSeeder</c>), por la identificación de la Res. 202/2025.</summary>
    public async Task<int?> ConsumidorFinalAsync(DateOnly fecha, CancellationToken ct)
    {
        var numero = CatalogoDian.Embebido.ConsumidorFinal(fecha)?.Numero;
        if (numero is null) return null;
        return await db.People.AsNoTracking().Where(p => p.TaxId == numero).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>¿El comprador es el consumidor final (sin contraparte o la genérica)?</summary>
    public async Task<bool> EsConsumidorFinalAsync(InventoryDocument venta, CancellationToken ct) =>
        venta.CounterpartyPersonId is not int persona || persona == await ConsumidorFinalAsync(venta.OperationDate, ct);

    // --------------------------------------------------------------------------------------------- el lector --

    /// <summary>
    /// La búsqueda <b>exacta</b>: primero el código de barras normalizado de <c>INV_ProductBarcodes</c> (el de empaque trae su unidad y
    /// su factor), después el código del producto (unidad base). Sin coincidencia → <c>Inventory.Product.NotFound</c>.
    /// </summary>
    public async Task<Result<ProductoLeido>> LeerAsync(string codigo, CancellationToken ct)
    {
        var normalizado = ProductBarcode.Normalizar(codigo);
        if (normalizado.Length == 0) return Result.Failure<ProductoLeido>(ErroresDelPos.ProductNotFound(codigo ?? string.Empty));

        var barra = await db.ProductBarcodes.AsNoTracking().Include(b => b.Product).Include(b => b.ProductUnit)
            .Where(b => b.Barcode == normalizado && b.Product != null && !b.Product.IsDeleted)
            .FirstOrDefaultAsync(ct);
        Product? producto;
        int unidad;
        decimal factor = 1m;
        if (barra is not null)
        {
            producto = barra.Product!;
            unidad = barra.ProductUnit?.UnitId ?? producto.BaseUnitId;
            factor = barra.ProductUnit?.Factor ?? 1m;
        }
        else
        {
            producto = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Code == normalizado, ct);
            int? serieLeida = null;
            if (producto is null)
            {
                // I6 (T929): la serie de una unidad en existencia; cada lectura de serie es una unidad.
                var numero = ReglasDeSeguimiento.Normalizar(codigo);
                var serie = await db.Serials.AsNoTracking().Include(s => s.Product)
                    .Where(s => s.SerialNumber == numero && s.InStockWarehouseId != null && s.Product != null && !s.Product.IsDeleted && s.Product.TracksSerial)
                    .OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
                if (serie is null) return Result.Failure<ProductoLeido>(ErroresDelPos.ProductNotFound(codigo!.Trim()));
                producto = serie.Product!;
                serieLeida = serie.Id;
            }
            unidad = producto.BaseUnitId;
            if (serieLeida is not null)
            {
                var deLaSerie = await db.UnitsOfMeasure.AsNoTracking().Where(x => x.Id == unidad).Select(x => new { x.PublicId, x.Code }).FirstAsync(ct);
                return Result.Success(new ProductoLeido(producto, unidad, deLaSerie.PublicId, deLaSerie.Code, 1m, serieLeida));
            }
        }
        var u = await db.UnitsOfMeasure.AsNoTracking().Where(x => x.Id == unidad).Select(x => new { x.PublicId, x.Code }).FirstAsync(ct);
        return Result.Success(new ProductoLeido(producto, unidad, u.PublicId, u.Code, factor));
    }

    /// <summary>«3*CODIGO» → (3, CODIGO); sin asterisco, la cantidad pedida (1 por defecto).</summary>
    public static (decimal Cantidad, string Codigo) Multiplicador(string codigo, decimal? cantidad)
    {
        var i = codigo.IndexOf('*', StringComparison.Ordinal);
        if (i > 0 && decimal.TryParse(codigo[..i].Trim(), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var veces) && veces > 0m)
            return (veces * (cantidad ?? 1m), codigo[(i + 1)..].Trim());
        return (cantidad ?? 1m, codigo.Trim());
    }

    // ---------------------------------------------------------------------------------------------- líneas --

    /// <summary>
    /// Agrega el producto a la venta, o suma la cantidad a la línea viva del mismo producto y unidad que no tiene precio digitado ni
    /// descuento manual (misma unidad, precio y descuento, §20.2). Devuelve el número de la línea y si ya existía.
    /// </summary>
    public async Task<Result<(int LineNumber, bool Sumada)>> AgregarAsync(InventoryDocument venta, int productoId, int unidadId, Guid unidadPublicId,
        decimal cantidad, CancellationToken ct, int? serieId = null, string? codigoDeLote = null)
    {
        if (cantidad <= 0m) return Result.Failure<(int, bool)>(new Error(Error.Validation.Code, "La cantidad debe ser mayor que cero."));
        var producto = await db.Products.AsNoTracking().FirstAsync(p => p.Id == productoId, ct);
        if (producto.Status == ProductStatus.Inactive) return Result.Failure<(int, bool)>(InventoryErrors.ProductInactive(0, producto.Code));
        if (producto.Status == ProductStatus.Blocked) return Result.Failure<(int, bool)>(InventoryErrors.ProductBlocked(0, producto.Code));
        // I6 (T927): una plantilla de variantes no se vende.
        if (producto.Kind == ProductKind.Template) return Result.Failure<(int, bool)>(InventoryErrors.ProductNotInventoriable(0, producto.Code));

        var vivas = venta.Lines.Where(l => !l.IsDeleted).ToList();

        // I6 (T929): con serie, una línea por unidad leída; sin la serie, el lector la pide.
        if (producto.TracksSerial)
        {
            if (serieId is not int serie) return Result.Failure<(int, bool)>(Catalog.CatalogErrors.SerialRequired(producto.Code));
            var registrada = await db.Serials.AsNoTracking().FirstAsync(s => s.Id == serie, ct);
            if (cantidad != 1m) return Result.Failure<(int, bool)>(Catalog.CatalogErrors.SerialQuantityNotOne(producto.Code, registrada.SerialNumber));
            if (registrada.InStockWarehouseId != venta.WarehouseId || vivas.Any(l => l.SerialId == serie))
                return Result.Failure<(int, bool)>(Catalog.CatalogErrors.SerialNotInStock(producto.Code, registrada.SerialNumber));
            var deLaSerie = new InventoryDocumentLine
            {
                Document = venta, DocumentId = venta.Id, LineNumber = vivas.Count == 0 ? 1 : vivas.Max(l => l.LineNumber) + 1,
                ProductId = productoId, UnitId = unidadId, LocationId = registrada.InStockLocationId ?? await UbicacionPorDefectoAsync(venta.WarehouseId, ct),
                SerialId = serie, LotId = registrada.LotId,
            };
            var unidadDeLaSerie = await ConvertirAsync(deLaSerie.LineNumber, productoId, producto.Code, unidadPublicId, 1m, ct);
            if (unidadDeLaSerie.IsFailure) return Result.Failure<(int, bool)>(unidadDeLaSerie.Error);
            venta.Lines.Add(deLaSerie);
            Cantidades(deLaSerie, unidadDeLaSerie.Value);
            return Result.Success((deLaSerie.LineNumber, false));
        }

        var conDescuento = await LineasConDescuentoManualAsync(venta, ct);
        var previa = vivas.FirstOrDefault(l => l.ProductId == productoId && l.UnitId == unidadId && !conDescuento.Contains(l.Id));
        var nueva = previa is null;
        var linea = previa ?? new InventoryDocumentLine { Document = venta, DocumentId = venta.Id, LineNumber = vivas.Count == 0 ? 1 : vivas.Max(l => l.LineNumber) + 1 };
        var convertida = await ConvertirAsync(linea.LineNumber, productoId, producto.Code, unidadPublicId, (previa?.Quantity ?? 0m) + cantidad, ct);
        if (convertida.IsFailure) return Result.Failure<(int, bool)>(convertida.Error);

        if (nueva)
        {
            linea.ProductId = productoId;
            linea.UnitId = unidadId;
            linea.LocationId = await UbicacionPorDefectoAsync(venta.WarehouseId, ct);
            venta.Lines.Add(linea);
        }
        Cantidades(linea, convertida.Value);

        // I6 (T929): el lote pedido o el sugerido (el que vence primero), visible y cambiable.
        if (producto.TracksLot)
        {
            var lote = await AsignarLoteAsync(linea, producto, codigoDeLote, ct);
            if (lote.IsFailure) return Result.Failure<(int, bool)>(lote.Error);
        }
        return Result.Success((linea.LineNumber, !nueva));
    }

    /// <summary>
    /// El lote de una línea del POS (I6, T929; FR-026): el que pidió el cajero —vencido y con <c>Ventas.LoteVencido = Bloquear</c>,
    /// <c>Inventory.Lot.Expired</c>— o el que sugiere <see cref="Domain.Inventory.Tracking.SelectorDeLotes"/> si uno solo alcanza para la
    /// línea; si la línea se reparte entre lotes, queda sin lote y la confirmación los reparte por el que vence primero. Los tres pasos del
    /// cobro no cambian: el lote va con la línea.
    /// </summary>
    public async Task<Result> AsignarLoteAsync(InventoryDocumentLine linea, Product producto, string? codigoDeLote, CancellationToken ct)
    {
        if (seguimiento is null || venta(linea) is not { WarehouseId: int bodega }) return Result.Success();
        var codigo = ReglasDeSeguimiento.Normalizar(codigoDeLote);
        if (codigo is not null)
        {
            var lote = await db.Lots.AsNoTracking().FirstOrDefaultAsync(l => l.ProductId == producto.Id && l.Code == codigo, ct);
            if (lote is null) return Result.Failure(Catalog.CatalogErrors.LotNotFound(producto.Code, codigo));
            if (Domain.Inventory.Tracking.SelectorDeLotes.EstaVencido(lote.ExpiryDate, reloj.HoyLocal)
                && await seguimiento.PoliticaVigenteAsync(ct) == Domain.Inventory.Tracking.PoliticaDeLoteVencido.Bloquear)
                return Result.Failure(Catalog.CatalogErrors.LotExpired(producto.Code, lote.Code, lote.ExpiryDate));
            linea.LotId = lote.Id;
            return Result.Success();
        }
        var sugerencia = await seguimiento.SugerirParaVenderAsync(producto.Id, bodega, linea.QuantityBase, ct);
        linea.LotId = sugerencia.Completo && sugerencia.Asignaciones.Count == 1 ? sugerencia.Asignaciones[0].Lote.LotId : null;
        return Result.Success();

        static InventoryDocument? venta(InventoryDocumentLine l) => l.Document;
    }

    /// <summary>Cambia la cantidad de la línea (con la conversión y los decimales de su unidad).</summary>
    public async Task<Result> CambiarCantidadAsync(InventoryDocumentLine linea, decimal cantidad, CancellationToken ct)
    {
        if (cantidad <= 0m) return Result.Failure(new Error(Error.Validation.Code, "La cantidad debe ser mayor que cero."));
        var producto = await db.Products.AsNoTracking().Where(p => p.Id == linea.ProductId).Select(p => p.Code).FirstAsync(ct);
        var unidad = await db.UnitsOfMeasure.AsNoTracking().Where(u => u.Id == linea.UnitId).Select(u => u.PublicId).FirstAsync(ct);
        var convertida = await ConvertirAsync(linea.LineNumber, linea.ProductId, producto, unidad, cantidad, ct);
        if (convertida.IsFailure) return Result.Failure(convertida.Error);
        Cantidades(linea, convertida.Value);
        return Result.Success();
    }

    private async Task<Result<(decimal Quantity, decimal Factor, decimal QuantityBase, decimal Rounding)>> ConvertirAsync(
        int numero, int productoId, string productoCodigo, Guid unidadPublicId, decimal cantidad, CancellationToken ct)
    {
        var unidad = await maestros.UnidadAsync(productoId, unidadPublicId, ct);
        if (unidad is null) return Result.Failure<(decimal, decimal, decimal, decimal)>(InventoryErrors.UnitNotForProduct(numero, productoCodigo, string.Empty));
        var conversion = ConversionDeUnidades.Convertir(new PedidoDeConversion(numero, unidad.Code, cantidad, unidad.Factor,
            unidad.DecimalesPermitidos, unidad.BaseUnitCode ?? unidad.Code, unidad.DecimalesDeLaBase));
        if (conversion.Rechazo is { } rechazo)
            return Result.Failure<(decimal, decimal, decimal, decimal)>(
                InventoryErrors.UnitDecimalsNotAllowed(numero, productoCodigo, rechazo.UnitCode, rechazo.AllowedDecimals, rechazo.QuantityBase));
        return Result.Success((cantidad, unidad.Factor, conversion.QuantityBase, conversion.RoundingQuantity));
    }

    private static void Cantidades(InventoryDocumentLine linea, (decimal Quantity, decimal Factor, decimal QuantityBase, decimal Rounding) c)
    {
        linea.Quantity = c.Quantity;
        linea.Factor = c.Factor;
        linea.QuantityBase = c.QuantityBase;
        linea.RoundingQuantity = c.Rounding;
    }

    private async Task<int?> UbicacionPorDefectoAsync(int? bodega, CancellationToken ct) => bodega is int b
        ? await db.WarehouseLocations.AsNoTracking().Where(l => l.WarehouseId == b && l.IsDefault).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct)
        : null;

    private async Task<HashSet<int>> LineasConDescuentoManualAsync(InventoryDocument venta, CancellationToken ct) =>
        (await db.DocumentLineDiscounts.AsNoTracking()
            .Where(d => d.DocumentId == venta.Id && !d.IsDeleted && !d.FromDocumentDiscount)
            .Select(d => d.DocumentLineId).ToListAsync(ct)).ToHashSet();

    // ------------------------------------------------------------------------------------------ precificar --

    /// <summary>
    /// Precifica todas las líneas vivas y, con <paramref name="aplicar"/>, escribe precios, descuentos (conservando las filas que no
    /// cambiaron) y totales, y pide o retira las aprobaciones de los descuentos sobre el tope. <paramref name="cambios"/> son los
    /// cambios pedidos por número de línea; <paramref name="resolverListas"/> vuelve a resolver la lista de todas (cambio de cliente,
    /// otra fecha operativa); <paramref name="descuentoPorTotal"/> reemplaza el descuento por total (0 lo quita).
    /// </summary>
    public async Task<Result<VentaPrecificada>> PrecificarAsync(InventoryDocument venta, IReadOnlyDictionary<int, CambioDeLinea>? cambios,
        bool resolverListas, decimal? descuentoPorTotal, bool aplicar, CancellationToken ct)
    {
        cambios ??= new Dictionary<int, CambioDeLinea>();
        var vivas = venta.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var ids = vivas.Where(l => l.Id != 0).Select(l => l.Id).ToList();
        var filas = aplicar
            ? await db.DocumentLineDiscounts.Where(d => d.DocumentId == venta.Id && ids.Contains(d.DocumentLineId) && !d.IsDeleted).ToListAsync(ct)
            : await db.DocumentLineDiscounts.AsNoTracking().Where(d => d.DocumentId == venta.Id && ids.Contains(d.DocumentLineId) && !d.IsDeleted).ToListAsync(ct);
        var porLinea = filas.ToLookup(f => f.DocumentLineId);

        if (vivas.Count == 0)
        {
            foreach (var f in filas) f.IsDeleted = true;
            if (aplicar) Totales(venta, new TotalesDeVenta(0m, 0m, 0m, 0m, 0m, 0m));
            var vacia = new VentaPrecificada([], [], new TotalesDeVenta(0m, 0m, 0m, 0m, 0m, 0m), Domain.Sales.Pricing.TopeDelUsuario.Ninguno, null, [], []);
            return Result.Success(vacia);
        }

        var lineas = vivas.Select(l => Pedida(l, porLinea[l.Id].ToList(), cambios.GetValueOrDefault(l.LineNumber), resolverListas)).ToList();
        var total = descuentoPorTotal ?? filas.Where(f => f.FromDocumentDiscount).Sum(f => f.Amount);
        var usuario = (await actorActual.ObtenerAsync(ct)).UserId;
        var municipio = await db.Branches.AsNoTracking().Where(b => b.Id == venta.BranchId).Select(b => b.MunicipalityDaneCode).FirstOrDefaultAsync(ct);
        var precificada = await precificacion.PrecificarAsync(new PedidoDePrecificacion(venta.OperationDate, venta.CounterpartyPersonId,
            venta.SalesChannelId, venta.BranchId, venta.WarehouseId, usuario, lineas, total > 0m ? total : null, municipio), ct);
        if (precificada.IsFailure || !aplicar) return precificada;

        var pares = new List<(InventoryDocumentLine, DocumentLineDiscount)>();
        foreach (var l in vivas)
        {
            var p = precificada.Value.Lineas.Single(x => x.LineNumber == l.LineNumber);
            foreach (var par in AplicarConservando(venta, l, p, porLinea[l.Id].ToList())) pares.Add(par);
        }
        Totales(venta, precificada.Value.Totales);

        var punto = venta.PointOfSaleId is int pid ? await db.PointsOfSale.AsNoTracking().Where(x => x.Id == pid).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        var pedidas = await aprobaciones.SolicitarAsync(venta, pares, punto, ct);
        return pedidas.IsFailure ? Result.Failure<VentaPrecificada>(pedidas.Error) : precificada;
    }

    /// <summary>Después del guardado: anota en cada descuento la solicitud que lo cubre.</summary>
    public async Task EnlazarAprobacionesAsync(InventoryDocument venta, CancellationToken ct)
    {
        var filas = await db.DocumentLineDiscounts.Where(d => d.DocumentId == venta.Id && !d.IsDeleted && d.RequiresApproval && d.ApprovalRequestId == null).ToListAsync(ct);
        if (filas.Count == 0) return;
        await aprobaciones.EnlazarSolicitudesAsync(filas, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Retira las solicitudes pendientes de los descuentos de una venta que se descarta.</summary>
    public async Task RetirarAprobacionesAsync(InventoryDocument venta, CancellationToken ct)
    {
        var ids = await db.DocumentLineDiscounts.AsNoTracking().Where(d => d.DocumentId == venta.Id && !d.IsDeleted && d.RequiresApproval)
            .Select(d => d.PublicId).ToListAsync(ct);
        foreach (var id in ids) await motor.InvalidarAsync(ApprovalSourceTypes.DocumentLineDiscount, id, ApprovalSubjects.DiscountOverCap, ct);
    }

    private static LineaAPrecificar Pedida(InventoryDocumentLine l, IReadOnlyList<DocumentLineDiscount> filas, CambioDeLinea? cambio, bool resolverListas)
    {
        PrecioFijado? fijado = !resolverListas && l.ListPrice is { } lista ? new PrecioFijado(l.PriceListId, lista, l.ListPriceIncludesTaxes) : null;
        // Los de promoción (I6) no son pedidos de la persona: el motor los vuelve a calcular en cada precificación.
        var manuales = filas.Where(f => !f.FromDocumentDiscount && f.Source == DiscountSource.Manual).OrderBy(f => f.Sequence).ToList();
        var porPrecio = manuales.FirstOrDefault(f => f.IsPriceOverride);
        var otros = manuales.Where(f => !f.IsPriceOverride).ToList();
        var divisor = l.ListPriceIncludesTaxes && l.ListPrice is { } lp && l.UnitPrice > 0m ? lp / l.UnitPrice : 1m;

        decimal? digitado = null, porcentaje = null, valor = null;
        var cambiada = cambio?.Quantity is not null || resolverListas;
        if (porPrecio is not null && l.Quantity > 0m)
        {
            // Sin cambios, por su valor (el mismo monto); con otra cantidad o lista, por su fracción del precio de lista.
            digitado = cambiada && l.ListPrice is { } lista2
                ? lista2 * (1m - (porPrecio.Rate ?? 0m))
                : (l.UnitPrice - porPrecio.Amount / l.Quantity) * divisor;
        }
        if (otros.Count > 0)
        {
            if (cambiada) porcentaje = otros.Sum(o => o.Rate ?? 0m);
            else valor = otros.Sum(o => o.Amount) * divisor;
        }

        if (cambio is not null)
        {
            if (cambio.QuitarPrecioDigitado) digitado = null;
            if (cambio.PrecioDigitado is { } nuevo) digitado = nuevo;
            if (cambio.Descuento is { } d)
            {
                porcentaje = d.Percent is { } pct && pct > 0m ? pct : null;
                valor = d.Amount is { } monto && monto > 0m ? monto : null;
            }
        }
        return new LineaAPrecificar(l.LineNumber, l.ProductId, l.UnitId, l.Quantity, l.QuantityBase,
            digitado, porcentaje, valor, fijado);
    }

    /// <summary>
    /// Como <see cref="PrecificacionDeVenta.AplicarALinea"/>, pero conserva la fila de descuento que sigue igual (misma clase y monto):
    /// su <c>PublicId</c> es la fuente de la aprobación. Las que ya no están se dan de baja; las nuevas se agregan. Devuelve todas (vivas y
    /// retiradas) para pedir o retirar su aprobación.
    /// </summary>
    private IEnumerable<(InventoryDocumentLine, DocumentLineDiscount)> AplicarConservando(InventoryDocument venta, InventoryDocumentLine linea,
        LineaPrecificada p, List<DocumentLineDiscount> filas)
    {
        linea.PriceListId = p.PriceListId;
        linea.ListPrice = p.ListPrice;
        linea.ListPriceIncludesTaxes = p.ListPriceIncludesTaxes;
        linea.UnitPrice = p.UnitPrice;
        linea.GrossAmount = p.GrossAmount;
        linea.DiscountAmount = p.DiscountAmount;
        linea.NetAmount = p.NetAmount;

        var libres = filas.ToList();
        var resultado = new List<(InventoryDocumentLine, DocumentLineDiscount)>();
        foreach (var d in p.Descuentos)
        {
            var igual = libres.FirstOrDefault(f => f.FromDocumentDiscount == d.FromDocumentDiscount && f.IsPriceOverride == d.IsPriceOverride && f.Amount == d.Amount
                && f.Source == d.Source && f.PromotionId == d.PromotionId);
            if (igual is not null)
            {
                libres.Remove(igual);
                igual.Sequence = d.Sequence;
                igual.Rate = d.Rate;
                igual.CapRateApplied = d.CapRateApplied;
                igual.RequiresApproval = d.RequiresApproval;
                resultado.Add((linea, igual));
                continue;
            }
            var nueva = new DocumentLineDiscount
            {
                DocumentLine = linea,
                DocumentLineId = linea.Id,
                DocumentId = venta.Id,
                Sequence = d.Sequence,
                Source = d.Source,
                PromotionId = d.PromotionId,
                FromDocumentDiscount = d.FromDocumentDiscount,
                IsPriceOverride = d.IsPriceOverride,
                Rate = d.Rate,
                Amount = d.Amount,
                CapRateApplied = d.CapRateApplied,
                RequiresApproval = d.RequiresApproval,
            };
            db.DocumentLineDiscounts.Add(nueva);
            resultado.Add((linea, nueva));
        }
        foreach (var vieja in libres)
        {
            vieja.IsDeleted = true;
            vieja.DeletedAt = reloj.UtcNow;
            resultado.Add((linea, vieja));
        }
        return resultado;
    }

    private static void Totales(InventoryDocument venta, TotalesDeVenta t)
    {
        venta.Subtotal = t.Subtotal;
        venta.DiscountTotal = t.DiscountTotal;
        venta.TaxTotal = t.TaxTotal;
        venta.WithholdingTotal = t.WithholdingTotal;
        venta.Total = t.Total;
        venta.AmountDue = t.AmountDue;
    }

    /// <summary>Da de baja la línea y sus descuentos (Principio VII) y renumera las siguientes.</summary>
    public async Task QuitarLineaAsync(InventoryDocument venta, InventoryDocumentLine linea, CancellationToken ct)
    {
        linea.IsDeleted = true;
        linea.DeletedAt = reloj.UtcNow;
        var filas = await db.DocumentLineDiscounts.Where(d => d.DocumentLineId == linea.Id && !d.IsDeleted).ToListAsync(ct);
        var pares = new List<(InventoryDocumentLine, DocumentLineDiscount)>();
        foreach (var f in filas)
        {
            f.IsDeleted = true;
            f.DeletedAt = reloj.UtcNow;
            pares.Add((linea, f));
        }
        if (pares.Count > 0) await aprobaciones.SolicitarAsync(venta, pares, null, ct);
    }

    // ------------------------------------------------------------------------------------------------ el DTO --

    /// <summary>
    /// El <see cref="PosDraftDto"/>: cabecera, comprador, vendedor, líneas con sus descuentos e impuestos (de <paramref name="precificada"/>
    /// en un borrador; de <c>INV_DocumentTaxLines</c> si ya se confirmó), totales, medios ofrecidos, aprobaciones pendientes y avisos.
    /// </summary>
    public async Task<PosDraftDto> DtoAsync(InventoryDocument venta, VentaPrecificada? precificada, int? ultimaLinea, IReadOnlyList<AvisoDto>? avisosExtra,
        CancellationToken ct)
    {
        var tipo = venta.DocumentType ?? await db.InventoryDocumentTypes.AsNoTracking().FirstAsync(t => t.Id == venta.DocumentTypeId, ct);
        var caja = venta.CashRegisterId is int cid ? await db.CashRegisters.AsNoTracking().Include(c => c.DocumentTypes).FirstOrDefaultAsync(c => c.Id == cid, ct) : null;
        var punto = await db.PointsOfSale.AsNoTracking().FirstAsync(p => p.Id == venta.PointOfSaleId, ct);
        var bodega = venta.WarehouseId is int wid ? await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wid, ct) : null;
        var sesion = venta.CashSessionId is int sid ? await db.CashSessions.AsNoTracking().Where(s => s.Id == sid).Select(s => (Guid?)s.PublicId).FirstOrDefaultAsync(ct) : null;
        var rol = caja?.DocumentTypes.FirstOrDefault(t => t.DocumentTypeId == tipo.Id && !t.IsDeleted)?.Role ?? CashRegisterDocumentRole.PosSale;

        // Comprador y vendedor.
        var consumidor = await ConsumidorFinalAsync(venta.OperationDate, ct);
        Person? persona = venta.CounterpartyPersonId is int per ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == per, ct) : null;
        var esFinal = persona is null || persona.Id == consumidor;
        var cliente = new PosCustomerDto(persona?.PublicId, persona is null ? "Consumidor final" : Nombre(persona), esFinal,
            esFinal ? null : await ReglasDeListaDePrecios.SegmentoDeAsync(db, persona!.Id, ct));
        PosSalespersonDto? vendedor = null;
        if (venta.SalespersonId is int vid)
        {
            var v = await db.Salespeople.AsNoTracking().Include(s => s.Person).FirstOrDefaultAsync(s => s.Id == vid, ct);
            if (v is not null) vendedor = new PosSalespersonDto(v.PublicId, Nombre(v.Person));
        }

        // Líneas.
        var vivas = venta.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new PosRefDto(p.PublicId, p.Code, p.Name), ct);
        // I6 (T941): el lote asignado, la serie y el seguimiento de cada producto, para que la pantalla los muestre.
        var seguimiento = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.TracksLot, p.TracksSerial), ct);
        var loteIds = vivas.Select(l => l.LotId).OfType<int>().Distinct().ToList();
        var lotesDeLinea = await db.Lots.AsNoTracking().Where(x => loteIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => (x.Code, x.ExpiryDate), ct);
        var serieIds = vivas.Select(l => l.SerialId).OfType<int>().Distinct().ToList();
        var seriesDeLinea = await db.Serials.AsNoTracking().Where(x => serieIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.SerialNumber, ct);
        var unidadIds = vivas.Select(l => l.UnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new PosRefDto(u.PublicId, u.Code, u.Name), ct);
        var listaIds = vivas.Select(l => l.PriceListId).OfType<int>().Distinct().ToList();
        var listas = await db.PriceLists.AsNoTracking().Where(p => listaIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new PosRefDto(p.PublicId, p.Code, p.Name), ct);
        var lineaIds = vivas.Select(l => l.Id).ToList();
        var filas = await db.DocumentLineDiscounts.AsNoTracking().Where(d => lineaIds.Contains(d.DocumentLineId) && !d.IsDeleted).ToListAsync(ct);
        var solicitudes = await SolicitudesAsync(filas.Where(f => f.RequiresApproval).Select(f => f.PublicId).ToList(), ct);
        var promocionIds = filas.Select(f => f.PromotionId).OfType<int>().Distinct().ToList();
        var promociones = await db.Promotions.AsNoTracking().Where(p => promocionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.PublicId, p.Name), ct);
        var impuestosGuardados = precificada is null
            ? (await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == venta.Id).ToListAsync(ct))
            : [];

        var lineas = new List<PosLineDto>(vivas.Count);
        foreach (var l in vivas)
        {
            var p = precificada?.Lineas.FirstOrDefault(x => x.LineNumber == l.LineNumber);
            var impuestos = precificada is not null
                ? precificada.Renglones.Where(r => r.Linea == l.LineNumber && !r.EsRetencion)
                    .Select(r => new PosLineTaxDto(r.TaxRateCode, r.Kind, r.Rate, r.Base, r.Amount)).ToList()
                : impuestosGuardados.Where(t => t.DocumentLineId == l.Id && t.Treatment == Domain.Enums.Core.TaxTreatment.Generated)
                    .Select(t => new PosLineTaxDto(t.TaxRateCode, t.Kind, t.Rate, t.Base, t.Amount)).ToList();
            var descuentos = filas.Where(f => f.DocumentLineId == l.Id).OrderBy(f => f.Sequence).Select(f =>
            {
                var s = solicitudes.GetValueOrDefault(f.PublicId);
                var promocion = f.PromotionId is int pid ? promociones.GetValueOrDefault(pid) : default;
                return new PosLineDiscountDto(f.Sequence, f.Source, f.FromDocumentDiscount, f.IsPriceOverride,
                    f.Rate, f.Amount, f.RequiresApproval,
                    f.RequiresApproval ? new PosLineDiscountApprovalDto(s?.PublicId, s?.Status.ToString() ?? "Pending") : null,
                    promocion.PublicId == Guid.Empty ? null : promocion.PublicId, promocion.Name);
            }).ToList();
            lineas.Add(new PosLineDto(l.PublicId, l.LineNumber, productos[l.ProductId], unidades[l.UnitId], l.Factor, l.Quantity, l.QuantityBase,
                l.RoundingQuantity, l.ListPrice ?? l.UnitPrice, l.UnitPrice, l.ListPriceIncludesTaxes,
                l.PriceListId is int li && listas.TryGetValue(li, out var lr) ? lr : null, descuentos, impuestos,
                l.NetAmount + impuestos.Sum(i => i.Amount), p?.Available, p?.BelowCost ?? false,
                l.LotId is int lid && lotesDeLinea.TryGetValue(lid, out var lote) ? lote.Code : null,
                l.LotId is int lid2 && lotesDeLinea.TryGetValue(lid2, out var lote2) ? lote2.ExpiryDate : null,
                l.LotId is int lid3 && lotesDeLinea.TryGetValue(lid3, out var lote3) && Domain.Inventory.Tracking.SelectorDeLotes.EstaVencido(lote3.ExpiryDate, reloj.HoyLocal),
                l.SerialId is int serieDeLinea ? seriesDeLinea.GetValueOrDefault(serieDeLinea) : null,
                seguimiento.GetValueOrDefault(l.ProductId).TracksLot, seguimiento.GetValueOrDefault(l.ProductId).TracksSerial));
        }

        // Descuento por total, aprobaciones pendientes y avisos.
        var porTotal = filas.Where(f => f.FromDocumentDiscount).ToList();
        var descuentoTotal = porTotal.Count == 0 ? null : new PosDocumentDiscountDto(
            porTotal[0].Rate, porTotal.Sum(f => f.Amount), porTotal.Any(f => f.RequiresApproval));
        var pendientes = filas.Where(f => f.RequiresApproval && solicitudes.GetValueOrDefault(f.PublicId) is { Status: ApprovalRequestStatus.Pending })
            .Select(f => new PosPendingApprovalDto(solicitudes[f.PublicId]!.PublicId, ApprovalSubjects.DiscountOverCap, ApprovalSourceTypes.DocumentLineDiscount,
                vivas.FirstOrDefault(l => l.Id == f.DocumentLineId)?.LineNumber, ApprovalRequestStatus.Pending.ToString()))
            .ToList();
        var delDocumento = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.InventoryDocument && r.SourcePublicId == venta.PublicId && r.Status == ApprovalRequestStatus.Pending)
            .Select(r => new { r.PublicId, r.Subject }).ToListAsync(ct);
        pendientes.AddRange(delDocumento.Select(r => new PosPendingApprovalDto(r.PublicId, r.Subject, ApprovalSourceTypes.InventoryDocument, null,
            ApprovalRequestStatus.Pending.ToString())));

        var avisos = new List<AvisoDto>();
        if (precificada is not null)
            foreach (var p in precificada.Lineas.Where(x => x.BelowCost))
                avisos.Add(new AvisoDto(ErroresDePrecios.BelowCostCode,
                    $"La línea {p.LineNumber} se vende por debajo de su costo promedio.", new { lineNumber = p.LineNumber }));
        if (avisosExtra is not null) avisos.AddRange(avisosExtra);

        var totales = new PosTotalsDto(venta.Subtotal, venta.DiscountTotal, venta.TaxTotal, venta.WithholdingTotal, venta.Total, venta.AmountDue);
        var medios = venta.Status == DocumentStatus.Draft ? await MediosAsync(venta, punto, caja, esFinal, ct) : [];
        return new PosDraftDto(
            venta.PublicId, venta.Status, venta.Class, new PosDocumentTypeDto(tipo.PublicId, tipo.Code, rol), sesion,
            new PosRefDto(punto.PublicId, punto.Code, punto.Name),
            caja is null ? new PosRefDto(Guid.Empty, string.Empty, string.Empty) : new PosRefDto(caja.PublicId, caja.Code, caja.Name),
            bodega is null ? new PosRefDto(Guid.Empty, string.Empty, string.Empty) : new PosRefDto(bodega.PublicId, bodega.Code, bodega.Name),
            venta.OperationDate, cliente, vendedor, lineas, ultimaLinea is int u ? lineas.FirstOrDefault(x => x.LineNumber == u) : null,
            descuentoTotal, totales, medios,
            venta.IsSuspended ? new PosSuspendedDto(venta.SuspendedLabel, venta.SuspendedAt, venta.UpdatedBy) : null,
            pendientes, avisos, venta.Notes, venta.RowVersion);
    }

    private async Task<Dictionary<Guid, Domain.Entities.Approvals.ApprovalRequest?>> SolicitudesAsync(IReadOnlyCollection<Guid> fuentes, CancellationToken ct)
    {
        if (fuentes.Count == 0) return [];
        var filas = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.DocumentLineDiscount && fuentes.Contains(r.SourcePublicId) && r.Status != ApprovalRequestStatus.Cancelled)
            .ToListAsync(ct);
        return fuentes.ToDictionary(f => f, f => filas.Where(r => r.SourcePublicId == f).OrderByDescending(r => r.Id).FirstOrDefault());
    }

    /// <summary>
    /// Los medios que se ofrecen en esta venta: activos y vigentes, en el punto, el canal y el tipo, y —los de crédito— sólo con cliente
    /// identificado y <c>Inventory.Sales.SellOnCredit</c> (<see cref="DisponibilidadDeMedio"/>, §22.3).
    /// </summary>
    public async Task<IReadOnlyList<PosPaymentMeansDto>> MediosAsync(InventoryDocument venta, PointOfSale punto, CashRegister? caja, bool esConsumidorFinal,
        CancellationToken ct)
    {
        var ofrecibles = await OfreciblesAsync(ct);
        var credito = await permisos.HasPermissionAsync(PermisoCredito, ct);
        var caso = new CasoDeCobro(venta.OperationDate, punto.Id, venta.SalesChannelId ?? punto.SalesChannelId, venta.DocumentTypeId, esConsumidorFinal, credito);
        var ofrecidos = DisponibilidadDeMedio.Ofrecidos(ofrecibles, caso).Select(m => m.PaymentMeansId).ToList();
        if (ofrecidos.Count == 0) return [];

        var medios = await db.PaymentMeans.AsNoTracking().Where(m => ofrecidos.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var adquirentes = medios.Values.Select(m => m.CardAcquirerId).OfType<int>().Distinct().ToList();
        var datafonos = await db.CardTerminals.AsNoTracking().Where(t => adquirentes.Contains(t.CardAcquirerId) && t.IsActive)
            .Select(t => new { t.Id, t.PublicId, t.Code, t.CardAcquirerId }).ToListAsync(ct);
        return ofrecidos.Select(id =>
        {
            var m = medios[id];
            var suyos = datafonos.Where(t => t.CardAcquirerId == m.CardAcquirerId).ToList();
            var propuesto = caja?.DefaultCardTerminalId is int d ? suyos.FirstOrDefault(t => t.Id == d)?.PublicId : null;
            return new PosPaymentMeansDto(m.PublicId, m.Code, m.Name, m.Class, m.QuickKey, m.RequiresReference, m.ReferenceKind, m.AllowsChange,
                m.AllowsPartial, m.CountMethod, suyos.Select(t => new PosCardTerminalDto(t.PublicId, t.Code)).ToList(), propuesto,
                ClasesDeMedio.EsCredito(m.Class) ? new PosCreditDefaultsDto(m.DefaultTermDays, m.DefaultInstallments, m.InstallmentPeriodDays, m.SuggestedCreditLineCode) : null);
        }).ToList();
    }

    /// <summary>Los medios con sus conjuntos de disponibilidad, en la forma de la regla pura.</summary>
    public async Task<IReadOnlyList<MedioOfrecible>> OfreciblesAsync(CancellationToken ct)
    {
        var medios = await db.PaymentMeans.AsNoTracking().ToListAsync(ct);
        var puntos = (await db.PaymentMeansPointsOfSale.AsNoTracking().Select(x => new { x.PaymentMeansId, x.PointOfSaleId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.PointOfSaleId);
        var canales = (await db.PaymentMeansChannels.AsNoTracking().Select(x => new { x.PaymentMeansId, x.SalesChannelId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.SalesChannelId);
        var tipos = (await db.PaymentMeansDocumentTypes.AsNoTracking().Select(x => new { x.PaymentMeansId, x.DocumentTypeId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.DocumentTypeId);
        return medios.Select(m => new MedioOfrecible(m.Id, m.Code, m.Name, m.Class, m.DisplayOrder, m.QuickKey, m.IsActive, m.ValidFrom, m.ValidTo,
            m.OfferedAtAllPointsOfSale, puntos[m.Id].ToList(), m.OfferedInAllChannels, canales[m.Id].ToList(), m.OfferedForAllDocumentTypes,
            tipos[m.Id].ToList())).ToList();
    }

    public static string Nombre(Person persona) => !string.IsNullOrWhiteSpace(persona.BusinessName)
        ? persona.BusinessName!
        : string.Join(' ', new[] { persona.FirstName, persona.OtherNames, persona.LastName, persona.SecondLastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
