using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using S = IngenIA365ERP.Application.Inventory.GoLive.PlantillaDeSaldoInicial;

namespace IngenIA365ERP.Application.Inventory.GoLive;

/// <summary>
/// La plantilla 14 — saldo inicial (feature 012, T308; contracts/plantillas.md §14; api.md §13.1; FR-089, US4-1;
/// <c>POST /api/inventory/opening-balances/import?mode=</c>, permiso <c>Inventory.OpeningBalance.Load</c>). Todo o nada sobre la
/// mecánica común (<see cref="EjecutorDeImportacion"/>: encabezados en la fila 1, errores con hoja, fila y columna, una
/// transacción, un guardado):
/// <list type="bullet">
/// <item>resuelve bodega, producto y ubicación por código dentro de <see cref="IAlcanceDeInventario"/> (fuera del alcance =
/// <c>Import.Cell.NotFound</c>, como lo inexistente);</item>
/// <item>la bodega, operativa y no activa, sin saldo confirmado vigente; su <c>fechaDeCorte</c> una sola y, si ya estaba fijada,
/// la misma; nunca futura, en período cerrado ni antes del inicio del módulo;</item>
/// <item>sin <c>INV_Setup</c>, el primer <c>apply</c> la crea con <c>StartDate</c> = primer día del mes del corte más antiguo
/// (data-model §6.1);</item>
/// <item>agrupa por bodega y parte en borradores <c>OpeningBalance</c> de hasta <see cref="InventoryDocument.MaxLineas"/> líneas
/// (T15) con el tipo de saldo inicial activo; volver a importar <b>reemplaza las líneas</b> de los borradores de esa bodega
/// conservando el mismo documento (<c>replaced: true</c>; las viejas quedan de baja lógica, Principio XI);</item>
/// <item><c>extra.byWarehouse[]</c> (revisión y aplicación) con el valor por grupo contable redondeado según
/// <c>Redondeo.Montos</c>; <c>extra.documents[]</c> al aplicar.</item>
/// </list>
/// La plantilla nunca confirma: el borrador se confirma con aprobación (<c>Inventory.OpeningBalance.Approve</c>). (nuevo)
/// </summary>
public sealed record ImportOpeningBalanceCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportOpeningBalanceCommandValidator : AbstractValidator<ImportOpeningBalanceCommand>
{
    public ImportOpeningBalanceCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportOpeningBalanceCommand>.LargoMaximo);
    }
}

/// <summary>El resumen de una bodega en la revisión (§14): el insumo de la comparación contra Contabilidad (FR-090). (nuevo)</summary>
public sealed record ResumenDeSaldoInicialDto(
    string Bodega,
    DateOnly FechaDeCorte,
    int Lineas,
    int Documentos,
    decimal CantidadTotal,
    decimal ValorTotal,
    IReadOnlyList<ValorPorGrupoDto> ValorPorGrupo);

public sealed record ValorPorGrupoDto(string GrupoContable, decimal Valor);

/// <summary>Un borrador que dejó la aplicación (§13.1 <c>extra.documents</c>). (nuevo)</summary>
public sealed record DocumentoDeSaldoInicialDto(
    Guid DocumentPublicId,
    BodegaDeSaldoInicialDto Warehouse,
    int Lines,
    decimal Value,
    DocumentStatus Status,
    bool Replaced);

public sealed record BodegaDeSaldoInicialDto(Guid PublicId, string Code);

public sealed class ImportOpeningBalanceCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj,
    ILectorDeParametros parametros)
    : IRequestHandler<ImportOpeningBalanceCommand, Result<ImportResultDto>>
{
    public const string ExtraPorBodega = "byWarehouse";
    public const string ExtraDocumentos = "documents";

    /// <summary>El motivo con que se descarta un borrador que sobra al volver a importar con menos líneas.</summary>
    public const string MotivoDeDescarte = "Reemplazado al volver a importar el saldo inicial.";

    public Task<Result<ImportResultDto>> Handle(ImportOpeningBalanceCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(S.Definicion, request, ProcesarAsync, ct);

    private sealed record FilaDeSaldo(FilaDeImportacion Fila, Warehouse Bodega, DateOnly Corte, ProductoLeido Producto, int? UbicacionId,
        string? CodigoDeUbicacion, decimal Cantidad, decimal Costo);

    private sealed record ProductoLeido(int Id, string Code, int BaseUnitId, int Decimales, string? Grupo, bool Inventariable, ProductStatus Status);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var hoja = ctx.Datos;

        // Catálogos citados, en bloque (§0.3).
        var bodegas = (await db.Warehouses.Include(w => w.Locations).ToListAsync(ct))
            .Where(w => alcance.IncluyeBodega(w.Id))
            .ToDictionary(w => w.Code, StringComparer.OrdinalIgnoreCase);
        var codigos = hoja.Filas.Select(f => f.Crudo(S.Producto)).OfType<string>().Select(c => c.ToUpperInvariant()).Distinct().ToList();
        var productos = (await db.Products.AsNoTracking()
                .Where(p => codigos.Contains(p.Code))
                .Select(p => new
                {
                    p.Id, p.Code, p.BaseUnitId, p.Kind, p.Status,
                    Decimales = p.BaseUnit != null ? (int)p.BaseUnit.AllowedDecimals : 0,
                    Grupo = p.AccountingGroup != null ? p.AccountingGroup.Code : null,
                })
                .ToListAsync(ct))
            .ToDictionary(p => p.Code, p => new ProductoLeido(p.Id, p.Code, p.BaseUnitId, p.Decimales, p.Grupo,
                p.Kind is ProductKind.Inventoriable or ProductKind.Variant, p.Status), StringComparer.OrdinalIgnoreCase);
        var setup = await db.InventorySetups.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

        var idsDeBodega = bodegas.Values.Select(b => b.Id).ToList();
        var saldos = await db.InventoryDocuments.Include(d => d.Lines)
            .Where(d => d.Class == DocumentClass.OpeningBalance && d.WarehouseId != null && idsDeBodega.Contains(d.WarehouseId.Value)
                && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingApproval || d.Status == DocumentStatus.Confirmed))
            .OrderBy(d => d.Id)
            .ToListAsync(ct);
        var porBodega = saldos.GroupBy(d => d.WarehouseId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        // ------------------------------------------------------------------------------------------ las filas --
        var leidas = new List<FilaDeSaldo>();
        var cortePorBodega = new Dictionary<int, (DateOnly Corte, int Fila)>();
        foreach (var fila in hoja.Filas)
        {
            var codigoDeBodega = fila.Codigo(S.Bodega);
            var corte = fila.Fecha(S.FechaDeCorte);
            var codigoDeProducto = fila.Codigo(S.Producto, S.LargoDeProducto);
            var codigoDeUbicacion = fila.Codigo(S.Ubicacion);
            var cantidad = fila.Cantidad(S.Cantidad);
            var costo = fila.Costo(S.CostoUnitario);
            foreach (var columna in new[] { S.Lote, S.Vencimiento, S.Serie })
                if (!fila.EstaVacia(columna)) fila.TodaviaNoDisponible(columna, "I6");

            // Bodega: del alcance (si no, como inexistente), operativa, no activa, sin saldo confirmado.
            Warehouse? bodega = null;
            if (codigoDeBodega is not null)
            {
                if (!bodegas.TryGetValue(codigoDeBodega, out bodega))
                    fila.Error(S.Bodega, ImportErrors.CellNotFound, $"No hay una bodega «{codigoDeBodega}» a su alcance. Créela en la plantilla de bodegas o en Inventario → Bodegas.");
                else if (!ReglasDeBodega(fila, bodega, porBodega.GetValueOrDefault(bodega.Id)))
                    bodega = null;
            }

            // Fecha de corte: nunca futura, ni en período cerrado, ni antes del inicio del módulo; una por bodega.
            if (corte is { } c)
            {
                if (c > hoy) Error(fila, S.FechaDeCorte, InventoryErrors.DateInFuture(c, hoy));
                else if (setup?.LastClosedDate is { } cerrado && c <= cerrado) Error(fila, S.FechaDeCorte, InventoryErrors.PeriodClosed(c.Year, c.Month, cerrado));
                else if (setup is not null && c < setup.StartDate) Error(fila, S.FechaDeCorte, InventoryErrors.DateBeforeCutoff(setup.StartDate));
                else if (bodega is not null)
                {
                    if (bodega.CutoffDate is { } fijada && fijada != c)
                        fila.Error(S.FechaDeCorte, ImportErrors.CellFormat,
                            $"La bodega {bodega.Code} ya tiene fecha de corte {fijada:yyyy-MM-dd}: todas sus filas deben traer esa fecha.");
                    else if (cortePorBodega.TryGetValue(bodega.Id, out var primera) && primera.Corte != c)
                        fila.Error(S.FechaDeCorte, ImportErrors.CellFormat,
                            $"La bodega {bodega.Code} trae la fecha de corte {primera.Corte:yyyy-MM-dd} en la fila {primera.Fila}: una bodega tiene una sola.");
                    else cortePorBodega.TryAdd(bodega.Id, (c, fila.Numero));
                }
            }

            // Producto: inventariable, con grupo contable, activo y no bloqueado (los mismos códigos del alta unitaria).
            ProductoLeido? producto = null;
            if (codigoDeProducto is not null)
            {
                if (!productos.TryGetValue(codigoDeProducto, out producto))
                    fila.Error(S.Producto, ImportErrors.CellNotFound, $"No hay un producto «{codigoDeProducto}». Créelo en la plantilla de productos o en Inventario → Productos.");
                else if (!producto.Inventariable) Error(fila, S.Producto, InventoryErrors.ProductNotInventoriable(fila.Numero, producto.Code));
                else if (producto.Status == ProductStatus.Blocked) Error(fila, S.Producto, InventoryErrors.ProductBlocked(fila.Numero, producto.Code));
                else if (producto.Status == ProductStatus.Inactive) Error(fila, S.Producto, InventoryErrors.ProductInactive(fila.Numero, producto.Code));
                else if (producto.Grupo is null) Error(fila, S.Producto, CatalogErrors.ProductAccountingGroupRequired());
            }

            // Ubicación: de la bodega; vacía = la por defecto.
            int? ubicacionId = null;
            if (codigoDeUbicacion is not null && bodega is not null)
            {
                var ubicacion = bodega.Locations.FirstOrDefault(l => !l.IsDeleted && l.IsActive
                    && string.Equals(l.Code, codigoDeUbicacion, StringComparison.OrdinalIgnoreCase));
                if (ubicacion is null)
                    fila.Error(S.Ubicacion, ImportErrors.CellNotFound, $"La bodega {bodega.Code} no tiene la ubicación «{codigoDeUbicacion}». Créela en Inventario → Bodegas.");
                else ubicacionId = ubicacion.Id;
            }

            // Cantidad positiva, en la base, con los decimales de la unidad; costo cero o más (cero con aviso).
            if (cantidad is { } q)
            {
                if (q <= 0m) fila.Error(S.Cantidad, ImportErrors.CellFormat, $"«{q}» no es válido en «{S.Cantidad}». Debe ser mayor que cero.");
                else if (producto is not null && Decimales(q) > producto.Decimales)
                    Error(fila, S.Cantidad, InventoryErrors.UnitDecimalsNotAllowed(fila.Numero, producto.Code, string.Empty, producto.Decimales, q));
            }
            if (costo is { } k)
            {
                if (k < 0m) fila.Error(S.CostoUnitario, ImportErrors.CellFormat, $"«{k}» no es válido en «{S.CostoUnitario}». No puede ser negativo.");
                else if (k == 0m) fila.Aviso(S.CostoUnitario, GoLiveErrors.OpeningBalanceZeroCostCode, "El costo unitario es cero: la existencia entra sin valor.");
            }

            // Llave: bodega + producto + ubicación (repetida es error, no se suma).
            if (codigoDeBodega is not null && codigoDeProducto is not null)
                hoja.LlaveUnica(fila, $"{codigoDeBodega}|{codigoDeProducto}|{codigoDeUbicacion}", S.Producto);

            if (fila.TieneErrores || bodega is null || corte is null || producto is null || cantidad is null || costo is null)
                continue;
            leidas.Add(new FilaDeSaldo(fila, bodega, corte.Value, producto, ubicacionId, codigoDeUbicacion, cantidad.Value, costo.Value));
        }

        var montos = await MontosAsync(hoy, ct);
        ctx.Extra[ExtraPorBodega] = Resumen(leidas, montos);
        if (ctx.HayErrores) return;

        // --------------------------------------------------------------------------------------- los borradores --
        var tipo = await db.InventoryDocumentTypes
            .Where(t => t.Class == DocumentClass.OpeningBalance && t.IsActive).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
        if (tipo is null)
        {
            var error = InventoryErrors.DocumentClassNotAvailable(DocumentClass.OpeningBalance);
            ctx.ErrorSinFila(null, S.Bodega, error.Code, "No hay un tipo de documento de saldo inicial activo. Créelo en Inventario → Tipos de documento.");
            return;
        }
        if (actor.UserId is not { } usuario)
        {
            var sinUsuario = Documents.ErroresDelDocumento.SinUsuario();
            ctx.ErrorSinFila(null, S.Bodega, sinUsuario.Code, sinUsuario.Message);
            return;
        }

        if (setup is null && leidas.Count > 0)
        {
            var primero = leidas.Min(l => l.Corte);
            db.InventorySetups.Add(new InventorySetup
            {
                StartDate = new DateOnly(primero.Year, primero.Month, 1),
                StartedAt = reloj.UtcNow,
                StartedByUserId = usuario,
            });
        }

        var documentos = new List<DocumentoDeSaldoInicialDto>();
        foreach (var grupo in leidas.GroupBy(l => l.Bodega).OrderBy(g => g.Key.Code, StringComparer.Ordinal))
        {
            var bodega = grupo.Key;
            var lineas = grupo.ToList();
            var corte = lineas[0].Corte;
            bodega.FijarFechaDeCorte(corte);

            var borradores = porBodega.GetValueOrDefault(bodega.Id)?.Where(d => d.Status == DocumentStatus.Draft).ToList() ?? [];
            var tramos = lineas.Chunk(InventoryDocument.MaxLineas).ToList();
            for (var i = 0; i < tramos.Count; i++)
            {
                var reemplaza = i < borradores.Count;
                var documento = reemplaza ? borradores[i] : NuevoBorrador(tipo, bodega, usuario);
                documento.OperationDate = corte;
                documento.DocumentTypeId = tipo.Id;
                documento.DocumentType = tipo;
                foreach (var vieja in documento.Lines.Where(l => !l.IsDeleted))
                {
                    vieja.IsDeleted = true;
                    vieja.DeletedAt = reloj.UtcNow;
                    vieja.DeletedBy = actor.Name;
                }

                var numero = 0;
                foreach (var l in tramos[i])
                {
                    documento.Lines.Add(new InventoryDocumentLine
                    {
                        Document = documento,
                        LineNumber = ++numero,
                        ProductId = l.Producto.Id,
                        UnitId = l.Producto.BaseUnitId,
                        Quantity = l.Cantidad,
                        Factor = 1m,
                        QuantityBase = l.Cantidad,
                        UnitCost = l.Costo,
                        TotalCost = Redondeo.Monto(l.Cantidad * l.Costo, montos),
                        LocationId = l.UbicacionId,
                    });
                    ctx.Registrar(l.Fila, $"{bodega.Code}|{l.Producto.Code}|{l.CodigoDeUbicacion}", reemplaza ? AccionDeImportacion.Update : AccionDeImportacion.Create);
                }
                documento.CostTotal = documento.Lines.Where(x => !x.IsDeleted).Sum(x => x.TotalCost ?? 0m);
                documentos.Add(new DocumentoDeSaldoInicialDto(documento.PublicId, new BodegaDeSaldoInicialDto(bodega.PublicId, bodega.Code),
                    numero, documento.CostTotal, DocumentStatus.Draft, reemplaza));
            }

            // Borradores que sobran (el archivo trae menos tramos que antes): se descartan con su motivo.
            foreach (var sobrante in borradores.Skip(tramos.Count))
                sobrante.Descartar(usuario, reloj.UtcNow, MotivoDeDescarte);
        }
        ctx.Extra[ExtraDocumentos] = documentos;
    }

    /// <summary>La bodega admite saldo inicial: operativa, no activa, sin un saldo confirmado vigente ni uno en aprobación.</summary>
    private static bool ReglasDeBodega(FilaDeImportacion fila, Warehouse bodega, List<InventoryDocument>? saldos)
    {
        if (bodega.EsTransito)
        {
            Error(fila, S.Bodega, GoLiveErrors.OpeningBalanceTransitNotAllowed(bodega.Code));
            return false;
        }
        if (bodega.EstaActiva)
        {
            Error(fila, S.Bodega, GoLiveErrors.OpeningBalanceWarehouseActive(bodega.PublicId, bodega.Code));
            return false;
        }
        var confirmados = saldos?.Where(d => d.Status == DocumentStatus.Confirmed).ToList() ?? [];
        if (confirmados.Count > 0)
        {
            Error(fila, S.Bodega, GoLiveErrors.OpeningBalanceAlreadyConfirmed(bodega.Code, confirmados
                .Select(d => new GoLiveErrors.SaldoConfirmado(d.PublicId, Documents.VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), d.OperationDate))
                .ToList()));
            return false;
        }
        if (saldos?.FirstOrDefault(d => d.Status == DocumentStatus.PendingApproval) is { } enAprobacion)
        {
            Error(fila, S.Bodega, InventoryErrors.NotDraft(enAprobacion.Status));
            return false;
        }
        return true;
    }

    private InventoryDocument NuevoBorrador(InventoryDocumentType tipo, Warehouse bodega, int usuario)
    {
        var documento = new InventoryDocument
        {
            Class = DocumentClass.OpeningBalance,
            DocumentTypeId = tipo.Id,
            DocumentType = tipo,
            WarehouseId = bodega.Id,
            BranchId = bodega.BranchId,
            CreatedByUserId = usuario,
            Currency = InventoryDocument.MonedaPorDefecto,
            ExchangeRate = 1m,
        };
        db.InventoryDocuments.Add(documento);
        return documento;
    }

    /// <summary><c>extra.byWarehouse[]</c> (§14): por bodega, lo que se cargaría, con el valor por grupo contable.</summary>
    private static IReadOnlyList<ResumenDeSaldoInicialDto> Resumen(IReadOnlyList<FilaDeSaldo> leidas, RedondeoDeMontos montos) =>
        leidas.GroupBy(l => l.Bodega)
            .OrderBy(g => g.Key.Code, StringComparer.Ordinal)
            .Select(g =>
            {
                var valores = g.Select(l => (l.Producto.Grupo ?? string.Empty, Valor: Redondeo.Monto(l.Cantidad * l.Costo, montos))).ToList();
                return new ResumenDeSaldoInicialDto(
                    g.Key.Code,
                    g.First().Corte,
                    g.Count(),
                    (g.Count() + InventoryDocument.MaxLineas - 1) / InventoryDocument.MaxLineas,
                    g.Sum(l => l.Cantidad),
                    valores.Sum(v => v.Valor),
                    valores.GroupBy(v => v.Item1).OrderBy(x => x.Key, StringComparer.Ordinal)
                        .Select(x => new ValorPorGrupoDto(x.Key, x.Sum(v => v.Valor))).ToList());
            })
            .ToList();

    private async Task<RedondeoDeMontos> MontosAsync(DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, hoy, ct: ct);
        return leido.IsSuccess ? Redondeo.MontosDesde(leido.Value.Texto) : RedondeoDeMontos.Centavo;
    }

    private static void Error(FilaDeImportacion fila, string columna, Error error) => fila.Error(columna, error.Code, error.Message);

    /// <summary>Los decimales significativos de una cantidad.</summary>
    private static int Decimales(decimal valor) => Documents.SaveInventoryDraftCommandValidator.Decimales(valor);
}
