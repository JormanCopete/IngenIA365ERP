using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Counts;
using IngenIA365ERP.Domain.Inventory.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// Registra una tanda de lecturas de un contador (feature 012, US11, T394; FR-040, US11-2; contracts/api.md §12,
/// <c>POST /counts/{id}/captures</c> con <c>{ round, reads[] }</c>, permiso <c>Inventory.Counts.Capture</c>). Cada lectura suma
/// (tres lecturas del mismo código = 3); una negativa corrige; cada una se acepta o se rechaza sola. Varias personas capturan a la
/// vez: las capturas sólo se agregan. (nuevo)
/// </summary>
public sealed record CapturePhysicalCountCommand(Guid CountPublicId, byte Round, IReadOnlyList<CountReadRequest> Reads)
    : IRequest<Result<CaptureResultDto>>, IOperacionIdempotente
{
    /// <summary>Lecturas por tanda: el lector de la pantalla envía por tandas más chicas.</summary>
    public const int MaxLecturas = 500;

    public Guid OperationKey { get; init; }
}

public sealed class CapturePhysicalCountCommandValidator : AbstractValidator<CapturePhysicalCountCommand>
{
    public CapturePhysicalCountCommandValidator()
    {
        RuleFor(x => x.CountPublicId).NotEmpty();
        RuleFor(x => x.Round).InclusiveBetween(ComparacionDeConteo.PrimeraRonda, ComparacionDeConteo.RondaDeReconteo)
            .WithMessage("La ronda es 1 (conteo) o 2 (reconteo).");
        RuleFor(x => x.Reads).NotEmpty().Must(r => r is null || r.Count <= CapturePhysicalCountCommand.MaxLecturas)
            .WithMessage($"Hasta {CapturePhysicalCountCommand.MaxLecturas} lecturas por tanda.");
        RuleForEach(x => x.Reads).Must(r => !string.IsNullOrWhiteSpace(r.Barcode) || r.ProductPublicId is not null)
            .WithMessage("Cada lectura trae un código de barras o un producto.");
        RuleForEach(x => x.Reads).Must(r => r.Quantity is null || SaveInventoryDraftCommandValidator.Decimales(r.Quantity.Value) <= 4)
            .WithMessage("La cantidad admite hasta 4 decimales.");
    }
}

/// <summary>
/// Dentro de una <see cref="TransaccionExplicita"/>, con la fila del conteo en exclusivo (dos tandas del mismo producto nuevo no crean
/// dos líneas): el conteo existe en el alcance (404) y está abierto (<c>Inventory.Count.NotOpen</c>); si declara contadores, quien
/// captura es uno de ellos (<c>.CounterNotAssigned</c>). Cada lectura se resuelve por código de barras (exacto, T43: el de una caja
/// propone su unidad y su factor) o por producto y unidad, se convierte a la unidad base con <see cref="ConversionDeUnidades"/> y va a
/// la línea de su producto, ubicación y lote; un producto sin línea en la foto entra con teórico 0 si el alcance lo cubre
/// (<c>AddedDuringCapture</c>), si no se rechaza (<c>Inventory.Count.ProductNotInScope</c>). La ronda 2 sólo acepta las líneas con
/// reconteo pendiente. Las aceptadas se agrupan en <c>INV_CountCaptures</c> por línea (sumas y correcciones aparte) y cada línea tocada
/// queda con su última ronda y si pide reconteo.
/// </summary>
public sealed class CapturePhysicalCountCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IMaestrosDelDocumento maestros,
    ICerrojoDeInventario cerrojo,
    VistaDeConteos conteos)
    : IRequestHandler<CapturePhysicalCountCommand, Result<CaptureResultDto>>
{
    public const string CodigoProductoInexistente = "Inventory.Product.NotFound";
    public const string CodigoUbicacionAjena = "Inventory.Location.NotInWarehouse";
    public const string CodigoDecimales = "Inventory.Unit.DecimalsNotAllowed";
    public const string CodigoRondaNoAbierta = "Inventory.Count.RoundNotOpen";

    public Task<Result<CaptureResultDto>> Handle(CapturePhysicalCountCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => CapturarAsync(request, ct), ct);

    private async Task<Result<CaptureResultDto>> CapturarAsync(CapturePhysicalCountCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: false, ct);
        if (conteo is null) return Falla(ErroresDeConteos.NotFound());
        await cerrojo.BloquearAsync(new PedidoDeCerrojo { DocumentosDeOrigen = [conteo.Id] }, ct);
        conteo = await db.InventoryDocuments.FirstAsync(d => d.Id == conteo.Id, ct);
        if (!VistaDeConteos.EstaAbierto(conteo) || conteo.WarehouseId is not int bodegaId) return Falla(ErroresDeConteos.NotOpen());

        var criterio = CriterioDelConteo.De(conteo.CountScopeJson);
        if (criterio.Contadores.Count > 0 && !criterio.Contadores.Contains(usuario)) return Falla(ErroresDeConteos.CounterNotAssigned());

        var lineas = await db.CountSnapshotLines.Where(l => l.DocumentId == conteo.Id).ToListAsync(ct);
        var porReconteo = request.Round < ComparacionDeConteo.RondaDeReconteo
            ? []
            : await LineasConReconteoAsync(conteo, lineas, ct);
        if (porReconteo is null) return Falla(ErroresDeConteos.RoundNotOpen(request.Round, conteo.CountRound ?? ComparacionDeConteo.PrimeraRonda));

        var ubicacionPorDefecto = await db.WarehouseLocations.AsNoTracking().Where(u => u.WarehouseId == bodegaId && u.IsDefault && u.IsActive)
            .Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
        var rechazadas = new List<RejectedReadDto>();
        var aceptadas = new List<(CountSnapshotLine Linea, decimal Cantidad, DateTime Hora)>();
        var nuevas = new List<CountSnapshotLine>();

        for (var i = 0; i < request.Reads.Count; i++)
        {
            var lectura = request.Reads[i];
            var resuelta = await ResolverAsync(lectura, ct);
            if (resuelta.Rechazo is { } r1)
            {
                rechazadas.Add(new RejectedReadDto(i, r1.Code, r1.Message));
                continue;
            }
            var (producto, unidad) = (resuelta.Producto!, resuelta.Unidad!);

            var cantidad = lectura.Quantity ?? 1m;
            var conversion = ConversionDeUnidades.Convertir(new PedidoDeConversion(i + 1, unidad.Code, Math.Abs(cantidad), unidad.Factor,
                unidad.DecimalesPermitidos, unidad.BaseUnitCode ?? unidad.Code, unidad.DecimalesDeLaBase));
            if (cantidad != 0m && conversion.Rechazo is { } decimales)
            {
                rechazadas.Add(new RejectedReadDto(i, CodigoDecimales,
                    $"{producto.Code}: la unidad {decimales.UnitCode} admite {decimales.AllowedDecimals} decimales."));
                continue;
            }
            var enBase = cantidad == 0m ? 0m : Math.Sign(cantidad) * conversion.QuantityBase;

            // La ubicación: la leída (de la bodega y, en un conteo por ubicación, del criterio), la de la foto o la de la bodega.
            int? ubicacionId = null;
            if (lectura.LocationPublicId is { } lu)
            {
                var u = (await maestros.UbicacionesAsync([lu], ct)).FirstOrDefault();
                if (u is null || u.WarehouseId != bodegaId)
                {
                    rechazadas.Add(new RejectedReadDto(i, CodigoUbicacionAjena, "La ubicación no es de la bodega del conteo."));
                    continue;
                }
                ubicacionId = u.Id;
            }
            var linea = lineas.Concat(nuevas).FirstOrDefault(l => l.ProductId == producto.Id && l.LotId == null
                && (ubicacionId is null ? true : l.LocationId == ubicacionId));
            if (linea is null)
            {
                ubicacionId ??= conteo.CountScope == CountScope.Location ? criterio.Ubicaciones.FirstOrDefault() : ubicacionPorDefecto;
                // Todo producto en un conteo total o por ubicación (en una de sus ubicaciones); en categorías o selección, el del criterio.
                var enAlcance = request.Round == ComparacionDeConteo.PrimeraRonda && ubicacionId is int ubic && conteo.CountScope switch
                {
                    CountScope.All => true,
                    CountScope.Location => criterio.Ubicaciones.Contains(ubic),
                    _ => (await conteos.ProductosEnElAlcanceAsync(conteo, [producto.Id], ct)).Count > 0,
                };
                if (!enAlcance)
                {
                    rechazadas.Add(new RejectedReadDto(i, ErroresDeConteos.CodigoProductoFueraDelAlcance, ErroresDeConteos.MensajeFueraDelAlcance(producto.Code)));
                    continue;
                }
                linea = new CountSnapshotLine
                {
                    DocumentId = conteo.Id,
                    ProductId = producto.Id,
                    LocationId = ubicacionId!.Value,
                    TheoreticalQuantity = 0m,
                    SnapshotUnitCost = await CostoAsync(producto.Id, ct),
                    AddedDuringCapture = true,
                };
                nuevas.Add(linea);
                db.CountSnapshotLines.Add(linea);
            }
            else if (request.Round >= ComparacionDeConteo.RondaDeReconteo && !porReconteo.Contains(linea.Id))
            {
                rechazadas.Add(new RejectedReadDto(i, CodigoRondaNoAbierta, ErroresDeConteos.MensajeSinReconteo(producto.Code)));
                continue;
            }

            aceptadas.Add((linea, enBase, lectura.ReadAt ?? reloj.UtcNow));
        }

        if (aceptadas.Count > 0)
        {
            // Una tanda por línea: las sumas y las correcciones en filas aparte, con cuántas lecturas trae cada una.
            foreach (var grupo in aceptadas.GroupBy(a => (a.Linea, Correccion: a.Cantidad < 0m)))
            {
                var captura = new CountCapture
                {
                    DocumentId = conteo.Id,
                    SnapshotLine = grupo.Key.Linea,
                    SnapshotLineId = grupo.Key.Linea.Id,
                    Round = request.Round,
                    CounterUserId = usuario,
                    Quantity = grupo.Sum(a => a.Cantidad),
                    Reads = grupo.Count(),
                    IsCorrection = grupo.Key.Correccion,
                    CapturedAt = grupo.Max(a => a.Hora),
                };
                grupo.Key.Linea.Captures.Add(captura);
                db.CountCaptures.Add(captura);
            }
            if (request.Round > (conteo.CountRound ?? 0)) conteo.CountRound = request.Round;
            await db.SaveChangesAsync(ct);
        }

        // Cada línea tocada queda con su última ronda y si pide reconteo.
        var tocadas = aceptadas.Select(a => a.Linea).Distinct().ToList();
        var comparadas = tocadas.Count == 0 ? [] : (await conteos.CompararAsync(conteo, tocadas, ct)) is { IsSuccess: true } c ? c.Value : [];
        foreach (var comparada in comparadas)
            tocadas.First(l => l.Id == comparada.Linea).MarcarRonda(comparada.RondaQueManda, comparada.RequiereReconteo);
        if (comparadas.Count > 0) await db.SaveChangesAsync(ct);

        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => tocadas.Select(l => l.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.PublicId, p.Code }, ct);
        var ubicaciones = (await maestros.UbicacionesPorIdAsync(tocadas.Select(l => l.LocationId).Distinct().ToList(), ct)).ToDictionary(u => u.Id, u => u.PublicId);
        return Result.Success(new CaptureResultDto(aceptadas.Count, rechazadas,
            comparadas.Select(x =>
            {
                var linea = tocadas.First(l => l.Id == x.Linea);
                var p = productos.GetValueOrDefault(linea.ProductId);
                return new CapturedLineDto(p?.PublicId ?? Guid.Empty, p?.Code ?? string.Empty, ubicaciones.GetValueOrDefault(linea.LocationId), x.Contado,
                    linea.AddedDuringCapture);
            }).ToList()));
    }

    /// <summary>
    /// Las líneas que admiten la ronda de reconteo: las que la primera ronda —ya capturada— dejó fuera de tolerancia y las que ya tienen
    /// reconteo. Una línea que nadie contó todavía se cuenta en la ronda 1. Nulo si ninguna lo pide (la ronda 2 no está abierta).
    /// </summary>
    private async Task<HashSet<int>?> LineasConReconteoAsync(InventoryDocument conteo, IReadOnlyList<CountSnapshotLine> lineas, CancellationToken ct)
    {
        var comparadas = await conteos.CompararAsync(conteo, lineas, ct);
        if (comparadas.IsFailure) return null;
        var admitidas = comparadas.Value
            .Where(c => (c.RequiereReconteo && c.RondaQueManda >= ComparacionDeConteo.PrimeraRonda) || c.RondaQueManda >= ComparacionDeConteo.RondaDeReconteo)
            .Select(c => c.Linea).ToHashSet();
        return admitidas.Count == 0 ? null : admitidas;
    }

    /// <summary>La lectura como producto y unidad: por código de barras exacto (su empaque) o por producto y unidad (la base si no viene).</summary>
    private async Task<(ProductoDelDocumento? Producto, UnidadDelDocumento? Unidad, Error? Rechazo)> ResolverAsync(CountReadRequest lectura, CancellationToken ct)
    {
        ProductoDelDocumento? producto;
        Guid? unidadPublica = lectura.UnitPublicId;
        if (!string.IsNullOrWhiteSpace(lectura.Barcode))
        {
            var codigo = ProductBarcode.Normalizar(lectura.Barcode);
            var barra = await db.ProductBarcodes.AsNoTracking().Include(b => b.ProductUnit).ThenInclude(u => u!.Unit)
                .FirstOrDefaultAsync(b => b.Barcode == codigo, ct);
            if (barra is null)
                return (null, null, new Error(ErroresDeConteos.CodigoCodigoDeBarrasInexistente, $"El código {codigo} no es de ningún producto."));
            producto = (await maestros.ProductosPorIdAsync([barra.ProductId], ct)).FirstOrDefault();
            unidadPublica ??= barra.ProductUnit?.Unit?.PublicId;
        }
        else
        {
            producto = (await maestros.ProductosAsync([lectura.ProductPublicId!.Value], ct)).FirstOrDefault();
        }
        if (producto is null) return (null, null, new Error(CodigoProductoInexistente, "El producto no existe."));
        if (!producto.Inventariable) return (null, null, new Error(ErroresDeConteos.CodigoProductoFueraDelAlcance, ErroresDeConteos.MensajeFueraDelAlcance(producto.Code)));

        unidadPublica ??= await db.Products.AsNoTracking().Where(p => p.Id == producto.Id)
            .Join(db.UnitsOfMeasure.AsNoTracking(), p => p.BaseUnitId, u => u.Id, (p, u) => u.PublicId).FirstAsync(ct);
        var unidad = await maestros.UnidadAsync(producto.Id, unidadPublica.Value, ct);
        return unidad is null
            ? (null, null, InventoryErrors.UnitNotForProduct(0, producto.Code, string.Empty))
            : (producto, unidad, null);
    }

    /// <summary>El costo del ámbito de la cooperativa o el último costo, informativo, para la línea que aparece en la captura.</summary>
    private async Task<decimal> CostoAsync(int productoId, CancellationToken ct)
    {
        var estado = await db.CostStates.AsNoTracking().Where(c => c.ProductId == productoId).OrderBy(c => c.ScopeWarehouseId).FirstOrDefaultAsync(ct);
        return estado is null ? 0m : estado.Quantity > 0m ? estado.AverageCost : estado.LastUnitCost;
    }

    private static Result<CaptureResultDto> Falla(Error error) => Result.Failure<CaptureResultDto>(error);
}
