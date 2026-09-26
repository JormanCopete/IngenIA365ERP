using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.GoLive;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using F = IngenIA365ERP.Application.Inventory.GoLive.PlantillaDeCifrasDeSolido;

namespace IngenIA365ERP.Application.Inventory.GoLive;

/// <summary>
/// La plantilla 15 — cifras de SOLIDO (feature 012, T311; contracts/plantillas.md §15; api.md §13.2; FR-091, US4-6;
/// <c>POST /api/inventory/legacy-figures/import?mode=</c>, permiso <c>Inventory.LegacyFigures.Import</c>). Escribe
/// <c>INV_LegacyFigures</c> y <b>nunca</b> el kardex ni sus proyecciones (FR-001): son dato para comparar.
/// <list type="bullet">
/// <item>llave <c>fecha</c> + <c>bodega</c> + <c>producto</c>; la bodega debe existir en el ERP (activa o no) y estar al alcance;</item>
/// <item>un código de producto que no está en el catálogo nuevo <b>no es error</b>: la fila exige <c>grupoContable</c>, queda
/// sin <c>ProductId</c> y avisa <c>Inventory.LegacyFigures.CodeUnresolved</c>; con producto, un grupo distinto del suyo a la
/// fecha avisa <c>Inventory.LegacyFigures.GroupMismatch</c>; la cantidad puede ser negativa;</item>
/// <item>cada importación es un lote nuevo (<c>ImportBatchPublicId</c>); importar otra vez un par (fecha, bodega) da de baja
/// lógica a lo anterior de ese par y lo devuelve en <c>extra.supersededBatchPublicIds</c>;</item>
/// <item><c>extra { batchPublicId, supersededBatchPublicIds[], byDateWarehouseGroup[] }</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record ImportLegacyFiguresCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportLegacyFiguresCommandValidator : AbstractValidator<ImportLegacyFiguresCommand>
{
    public ImportLegacyFiguresCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportLegacyFiguresCommand>.LargoMaximo);
    }
}

/// <summary>Cantidad y valor por (fecha, bodega, grupo) de la revisión (§15). (nuevo)</summary>
public sealed record CifraPorFechaBodegaGrupoDto(DateOnly Fecha, string Bodega, string GrupoContable, decimal Cantidad, decimal Valor, int Filas);

public sealed class ImportLegacyFiguresCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj)
    : IRequestHandler<ImportLegacyFiguresCommand, Result<ImportResultDto>>
{
    public const string ExtraLote = "batchPublicId";
    public const string ExtraReemplazados = "supersededBatchPublicIds";
    public const string ExtraPorFechaBodegaGrupo = "byDateWarehouseGroup";

    public Task<Result<ImportResultDto>> Handle(ImportLegacyFiguresCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(F.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request.File.NombreArchivo, c), ct);

    private sealed record FilaDeCifra(FilaDeImportacion Fila, DateOnly Fecha, Warehouse Bodega, string CodigoDeProducto, int? ProductId,
        int? GrupoId, string Grupo, decimal Cantidad, decimal Valor);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, string archivo, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var hoja = ctx.Datos;

        var bodegas = (await db.Warehouses.AsNoTracking().ToListAsync(ct))
            .Where(w => alcance.IncluyeBodega(w.Id))
            .ToDictionary(w => w.Code, StringComparer.OrdinalIgnoreCase);
        var codigos = hoja.Filas.Select(f => f.Crudo(F.Producto)).OfType<string>().Select(c => c.ToUpperInvariant()).Distinct().ToList();
        var productos = (await db.Products.AsNoTracking().Where(p => codigos.Contains(p.Code)).Select(p => new { p.Id, p.Code }).ToListAsync(ct))
            .ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);
        var grupos = await db.AccountingGroups.AsNoTracking().Select(g => new { g.Id, g.Code }).ToListAsync(ct);
        var grupoPorCodigo = grupos.ToDictionary(g => g.Code, g => g.Id, StringComparer.OrdinalIgnoreCase);
        var codigoDeGrupo = grupos.ToDictionary(g => g.Id, g => g.Code);

        var leidas = new List<(FilaDeImportacion Fila, DateOnly Fecha, Warehouse Bodega, string Producto, int? ProductId, int? GrupoArchivo, decimal Cantidad, decimal Valor)>();
        foreach (var fila in hoja.Filas)
        {
            var fecha = fila.Fecha(F.Fecha);
            var codigoDeBodega = fila.Texto(F.Bodega);
            var codigoDeProducto = fila.Texto(F.Producto);
            var cantidad = fila.Cantidad(F.Cantidad);
            var valor = fila.Monto(F.Valor);
            var codigoDeGrupo2 = fila.Codigo(F.GrupoContable);

            Warehouse? bodega = null;
            if (codigoDeBodega is not null && !bodegas.TryGetValue(codigoDeBodega, out bodega))
                fila.Error(F.Bodega, ImportErrors.CellNotFound, $"No hay una bodega «{codigoDeBodega}» en el ERP. Créela en la plantilla de bodegas o en Inventario → Bodegas.");

            int? grupoArchivo = null;
            if (codigoDeGrupo2 is not null)
            {
                if (grupoPorCodigo.TryGetValue(codigoDeGrupo2, out var g)) grupoArchivo = g;
                else fila.Error(F.GrupoContable, ImportErrors.CellNotFound, $"No hay un grupo contable «{codigoDeGrupo2}». Créelo en la plantilla de grupos o en Inventario → Grupos contables.");
            }

            int? productId = null;
            if (codigoDeProducto is not null)
            {
                if (productos.TryGetValue(codigoDeProducto, out var p)) productId = p;
                else if (codigoDeGrupo2 is null)
                    fila.Error(F.GrupoContable, ImportErrors.CellRequired,
                        $"El producto «{codigoDeProducto}» no está en el catálogo nuevo: la fila exige su grupo contable.");
                else
                    fila.Aviso(F.Producto, GoLiveErrors.LegacyFiguresCodeUnresolvedCode,
                        $"El producto «{codigoDeProducto}» no está en el catálogo nuevo: la cifra cuenta en su grupo, no en el comparativo por producto.");
            }

            if (fecha is not null && codigoDeBodega is not null && codigoDeProducto is not null)
                hoja.LlaveUnica(fila, $"{fecha:yyyy-MM-dd}|{codigoDeBodega}|{codigoDeProducto}", F.Producto);

            if (fila.TieneErrores || fecha is null || bodega is null || codigoDeProducto is null || cantidad is null || valor is null) continue;
            leidas.Add((fila, fecha.Value, bodega, codigoDeProducto, productId, grupoArchivo, cantidad.Value, valor.Value));
        }

        // El grupo de cada producto resuelto a la fecha de su cifra (US3, T287): el que va a la fila y el que se compara.
        var cifras = new List<FilaDeCifra>(leidas.Count);
        foreach (var porFecha in leidas.GroupBy(l => l.Fecha))
        {
            var ids = porFecha.Select(l => l.ProductId).OfType<int>().Distinct().ToList();
            var alDia = ids.Count == 0 ? new Dictionary<int, int?>() : await GrupoContableALaFecha.DeAsync(db, ids, porFecha.Key, ct);
            foreach (var l in porFecha)
            {
                var delProducto = l.ProductId is int pid ? alDia.GetValueOrDefault(pid) : null;
                if (l.ProductId is not null && l.GrupoArchivo is int g && delProducto is int gp && g != gp)
                    l.Fila.Aviso(F.GrupoContable, GoLiveErrors.LegacyFiguresGroupMismatchCode,
                        $"El producto «{l.Producto}» es del grupo {codigoDeGrupo.GetValueOrDefault(gp)} al {l.Fecha:yyyy-MM-dd}, no de {codigoDeGrupo.GetValueOrDefault(g)}.");
                var grupo = l.GrupoArchivo ?? delProducto;
                cifras.Add(new FilaDeCifra(l.Fila, l.Fecha, l.Bodega, l.Producto, l.ProductId, grupo,
                    grupo is int x ? codigoDeGrupo.GetValueOrDefault(x) ?? string.Empty : string.Empty, l.Cantidad, l.Valor));
            }
        }

        ctx.Extra[ExtraPorFechaBodegaGrupo] = cifras
            .GroupBy(c => (c.Fecha, c.Bodega.Code, c.Grupo))
            .OrderBy(g => g.Key.Fecha).ThenBy(g => g.Key.Code, StringComparer.Ordinal).ThenBy(g => g.Key.Grupo, StringComparer.Ordinal)
            .Select(g => new CifraPorFechaBodegaGrupoDto(g.Key.Fecha, g.Key.Code, g.Key.Grupo, g.Sum(x => x.Cantidad), g.Sum(x => x.Valor), g.Count()))
            .ToList();
        if (ctx.HayErrores) return;

        // Un lote nuevo; lo anterior de cada par (fecha, bodega) del archivo queda de baja lógica (historia, no se borra).
        var lote = Guid.NewGuid();
        var pares = cifras.Select(c => (c.Fecha, BodegaId: c.Bodega.Id)).Distinct().ToList();
        var fechas = pares.Select(p => p.Fecha).Distinct().ToList();
        var idsDeBodega = pares.Select(p => p.BodegaId).Distinct().ToList();
        var anteriores = (await db.LegacyFigures
                .Where(x => fechas.Contains(x.AsOfDate) && x.WarehouseId != null && idsDeBodega.Contains(x.WarehouseId.Value))
                .ToListAsync(ct))
            .Where(x => pares.Contains((x.AsOfDate, x.WarehouseId!.Value)))
            .ToList();
        foreach (var anterior in anteriores)
        {
            anterior.IsDeleted = true;
            anterior.DeletedAt = reloj.UtcNow;
            anterior.DeletedBy = actor.Name;
        }

        var nombre = archivo.Length > LegacyFigure.LargoDelArchivo ? archivo[..LegacyFigure.LargoDelArchivo] : archivo;
        foreach (var c in cifras)
        {
            db.LegacyFigures.Add(new LegacyFigure
            {
                ImportBatchPublicId = lote,
                AsOfDate = c.Fecha,
                ProductCodeRaw = c.CodigoDeProducto,
                WarehouseCodeRaw = c.Bodega.Code,
                ProductId = c.ProductId,
                WarehouseId = c.Bodega.Id,
                AccountingGroupId = c.GrupoId,
                Quantity = c.Cantidad,
                Value = c.Valor,
                SourceFileName = nombre,
            });
            ctx.Registrar(c.Fila, $"{c.Fecha:yyyy-MM-dd}|{c.Bodega.Code}|{c.CodigoDeProducto}", AccionDeImportacion.Create);
        }

        ctx.Extra[ExtraLote] = lote;
        ctx.Extra[ExtraReemplazados] = anteriores.Select(a => a.ImportBatchPublicId).Distinct().ToList();
    }
}
