using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.UnitsOfMeasure;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using G = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeGruposContables;
using U = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeUnidades;
using M = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeMarcas;
using K = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeCategorias;

namespace IngenIA365ERP.Application.Inventory.Imports;

// Las plantillas 2 a 5 (feature 012, T227; contracts/plantillas.md §2–§5): grupos contables, unidades, marcas y categorías
// sobre EjecutorDeImportacion y con las mismas reglas que el alta unitaria (código por CodigoDeCatalogo, que no se renombra;
// la plantilla nunca borra; activar o inactivar pide motivo y no se inactiva lo que está en uso). Cada una trae su consulta
// de datos para la descarga con datos (§0.6). (nuevo)

/// <summary>Lo que comparten las plantillas del catálogo: los cambios campo a campo y el error de una regla. (nuevo)</summary>
internal static class FilasDeCatalogo
{
    public static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        if (!string.Equals(antes ?? string.Empty, despues ?? string.Empty, StringComparison.Ordinal)) campos.Add(new(columna, antes, despues));
    }

    public static string SiNo(bool valor) => valor ? "sí" : "no";

    public static string Numero(decimal? valor) => valor?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public static void Error(FilaDeImportacion fila, string columna, Error error) => fila.Error(columna, error.Code, error.Message);

    /// <summary>La acción de una fila existente según sus cambios.</summary>
    public static AccionDeImportacion Accion(List<CampoCambiadoDto> campos) =>
        campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update;
}

// ================================================================================================== grupos contables --

/// <summary>Plantilla 2 (§2; <c>POST /api/inventory/accounting-groups/import</c>). (nuevo)</summary>
public sealed record ImportAccountingGroupsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportAccountingGroupsCommandValidator : AbstractValidator<ImportAccountingGroupsCommand>
{
    public ImportAccountingGroupsCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportAccountingGroupsCommand>.LargoMaximo);
    }
}

public sealed class ImportAccountingGroupsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor)
    : IRequestHandler<ImportAccountingGroupsCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportAccountingGroupsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(G.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var grupos = await db.AccountingGroups.ToListAsync(ct);
        var porCodigo = grupos.ToDictionary(g => g.Code, StringComparer.OrdinalIgnoreCase);
        var enUso = await db.Products
            .Where(p => p.AccountingGroupId != null && p.Status != ProductStatus.Inactive && (p.Kind == ProductKind.Inventoriable || p.Kind == ProductKind.Variant))
            .GroupBy(p => p.AccountingGroupId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);

        foreach (var fila in ctx.Datos.Filas)
        {
            var codigo = fila.Codigo(G.Codigo);
            var nombre = fila.Texto(G.Nombre);
            var descripcion = fila.Texto(G.Descripcion);
            var activo = fila.SiNo(G.Activo, porDefecto: true);
            if (!ctx.Datos.LlaveUnica(fila, codigo, G.Codigo) || codigo is null || nombre is null || fila.TieneErrores) continue;

            if (!porCodigo.TryGetValue(codigo, out var grupo))
            {
                grupo = new AccountingGroup { Code = codigo, Name = nombre, Description = descripcion, IsActive = activo };
                db.AccountingGroups.Add(grupo);
                porCodigo[codigo] = grupo;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, [new(G.Nombre, null, nombre)]);
                continue;
            }

            var campos = new List<CampoCambiadoDto>();
            FilasDeCatalogo.Diferencia(campos, G.Nombre, grupo.Name, nombre);
            FilasDeCatalogo.Diferencia(campos, G.Descripcion, grupo.Description, descripcion);
            if (grupo.IsActive != activo)
            {
                if (!activo && enUso.GetValueOrDefault(grupo.Id) is > 0 and var n)
                {
                    FilasDeCatalogo.Error(fila, G.Activo, CatalogErrors.AccountingGroupInUse(n));
                    continue;
                }
                FilasDeCatalogo.Diferencia(campos, G.Activo, FilasDeCatalogo.SiNo(grupo.IsActive), FilasDeCatalogo.SiNo(activo));
                ctx.PedirMotivo();
            }
            grupo.Name = nombre;
            grupo.Description = descripcion;
            grupo.IsActive = activo;
            ctx.Registrar(fila, codigo, FilasDeCatalogo.Accion(campos), campos);
        }
    }
}

/// <summary>La plantilla 2 llena (§0.6). (nuevo)</summary>
public sealed record GetAccountingGroupsTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetAccountingGroupsTemplateDataQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountingGroupsTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetAccountingGroupsTemplateDataQuery request, CancellationToken ct)
    {
        var filas = (await db.AccountingGroups.AsNoTracking().OrderBy(g => g.Code).ToListAsync(ct))
            .Select(g => (IReadOnlyList<object?>)[g.Code, g.Name, g.Description, g.IsActive]).ToList();
        return Result.Success(DatosDeUnaHoja.De(filas));
    }
}

// ============================================================================================================ unidades --

/// <summary>Plantilla 3 (§3; <c>POST /api/inventory/units/import</c>). La semilla se actualiza por su código. (nuevo)</summary>
public sealed record ImportUnitsOfMeasureCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportUnitsOfMeasureCommandValidator : AbstractValidator<ImportUnitsOfMeasureCommand>
{
    public ImportUnitsOfMeasureCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportUnitsOfMeasureCommand>.LargoMaximo);
    }
}

public sealed class ImportUnitsOfMeasureCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IDateTimeService reloj)
    : IRequestHandler<ImportUnitsOfMeasureCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportUnitsOfMeasureCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(U.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var unidades = await db.UnitsOfMeasure.ToListAsync(ct);
        var porCodigo = unidades.ToDictionary(u => u.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var fila in ctx.Datos.Filas)
        {
            var codigo = fila.Codigo(U.Codigo);
            var nombre = fila.Texto(U.Nombre);
            var decimales = fila.Entero(U.Decimales);
            var dianCrudo = fila.Texto(U.CodigoDian);
            var activo = fila.SiNo(U.Activo, porDefecto: true);
            if (decimales is < 0 or > UnitOfMeasure.MaxDecimals)
                fila.Error(U.Decimales, ImportErrors.CellFormat, $"«{decimales}» no es válido en «{U.Decimales}». Admite de 0 a {UnitOfMeasure.MaxDecimals}.");
            string? dian = null;
            if (dianCrudo is not null)
            {
                var r = ReglasDeUnidad.CodigoDian(dianCrudo, hoy);
                if (r.IsFailure) FilasDeCatalogo.Error(fila, U.CodigoDian, r.Error);
                else dian = r.Value;
            }
            if (!ctx.Datos.LlaveUnica(fila, codigo, U.Codigo) || codigo is null || nombre is null || decimales is null || fila.TieneErrores) continue;

            if (!porCodigo.TryGetValue(codigo, out var unidad))
            {
                unidad = new UnitOfMeasure { Code = codigo, Name = nombre, AllowedDecimals = (byte)decimales.Value, DianUnitCode = dian, IsActive = activo };
                db.UnitsOfMeasure.Add(unidad);
                porCodigo[codigo] = unidad;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create,
                    [new(U.Nombre, null, nombre), new(U.Decimales, null, decimales.Value.ToString(CultureInfo.InvariantCulture)), new(U.CodigoDian, null, dian)]);
                continue;
            }

            if (decimales < unidad.AllowedDecimals)
            {
                var usados = await ReglasDeUnidad.DecimalesUsadosAsync(db, unidad.Id, ct);
                if (usados > decimales)
                {
                    FilasDeCatalogo.Error(fila, U.Decimales, CatalogErrors.UnitDecimalsInUse(usados));
                    continue;
                }
            }
            var campos = new List<CampoCambiadoDto>();
            FilasDeCatalogo.Diferencia(campos, U.Nombre, unidad.Name, nombre);
            FilasDeCatalogo.Diferencia(campos, U.Decimales, unidad.AllowedDecimals.ToString(CultureInfo.InvariantCulture), decimales.Value.ToString(CultureInfo.InvariantCulture));
            FilasDeCatalogo.Diferencia(campos, U.CodigoDian, unidad.DianUnitCode, dian);
            if (unidad.IsActive != activo)
            {
                if (!activo)
                {
                    var usan = await db.Products
                        .Where(p => p.Status != ProductStatus.Inactive
                            && (p.BaseUnitId == unidad.Id || db.ProductUnits.Any(pu => pu.ProductId == p.Id && pu.UnitId == unidad.Id)))
                        .OrderBy(p => p.Code).Select(p => p.Code).ToListAsync(ct);
                    if (usan.Count > 0)
                    {
                        FilasDeCatalogo.Error(fila, U.Activo, CatalogErrors.UnitInUse(usan.Count, usan.Take(5).ToList()));
                        continue;
                    }
                }
                FilasDeCatalogo.Diferencia(campos, U.Activo, FilasDeCatalogo.SiNo(unidad.IsActive), FilasDeCatalogo.SiNo(activo));
                ctx.PedirMotivo();
            }
            unidad.Name = nombre;
            unidad.AllowedDecimals = (byte)decimales.Value;
            unidad.DianUnitCode = dian;
            unidad.IsActive = activo;
            ctx.Registrar(fila, codigo, FilasDeCatalogo.Accion(campos), campos);
        }
    }
}

/// <summary>La plantilla 3 llena (§0.6). (nuevo)</summary>
public sealed record GetUnitsOfMeasureTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetUnitsOfMeasureTemplateDataQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetUnitsOfMeasureTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetUnitsOfMeasureTemplateDataQuery request, CancellationToken ct)
    {
        var filas = (await db.UnitsOfMeasure.AsNoTracking().OrderBy(u => u.Code).ToListAsync(ct))
            .Select(u => (IReadOnlyList<object?>)[u.Code, u.Name, (int)u.AllowedDecimals, u.DianUnitCode, u.IsActive]).ToList();
        return Result.Success(DatosDeUnaHoja.De(filas));
    }
}

// ============================================================================================================== marcas --

/// <summary>Plantilla 4 (§4; <c>POST /api/inventory/brands/import</c>). Renombrar una marca recalcula la búsqueda de sus productos. (nuevo)</summary>
public sealed record ImportBrandsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportBrandsCommandValidator : AbstractValidator<ImportBrandsCommand>
{
    public ImportBrandsCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportBrandsCommand>.LargoMaximo);
    }
}

public sealed class ImportBrandsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor)
    : IRequestHandler<ImportBrandsCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportBrandsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(M.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var marcas = await db.Brands.ToListAsync(ct);
        var porCodigo = marcas.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
        var renombradas = new List<Brand>();

        foreach (var fila in ctx.Datos.Filas)
        {
            var codigo = fila.Codigo(M.Codigo);
            var nombre = fila.Texto(M.Nombre);
            var activo = fila.SiNo(M.Activo, porDefecto: true);
            if (!ctx.Datos.LlaveUnica(fila, codigo, M.Codigo) || codigo is null || nombre is null || fila.TieneErrores) continue;

            if (!porCodigo.TryGetValue(codigo, out var marca))
            {
                marca = new Brand { Code = codigo, Name = nombre, IsActive = activo };
                db.Brands.Add(marca);
                porCodigo[codigo] = marca;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, [new(M.Nombre, null, nombre)]);
                continue;
            }

            var campos = new List<CampoCambiadoDto>();
            FilasDeCatalogo.Diferencia(campos, M.Nombre, marca.Name, nombre);
            if (marca.Name != nombre) renombradas.Add(marca);
            // Una marca con productos activos se puede inactivar: los productos la conservan (§3.3).
            if (marca.IsActive != activo)
            {
                FilasDeCatalogo.Diferencia(campos, M.Activo, FilasDeCatalogo.SiNo(marca.IsActive), FilasDeCatalogo.SiNo(activo));
                ctx.PedirMotivo();
            }
            marca.Name = nombre;
            marca.IsActive = activo;
            ctx.Registrar(fila, codigo, FilasDeCatalogo.Accion(campos), campos);
        }

        // Como BrandCommands: la marca entra al texto de búsqueda de sus productos, en el mismo guardado.
        if (renombradas.Count > 0)
        {
            var ids = renombradas.Select(b => b.Id).ToList();
            var productos = await db.Products.Where(p => p.BrandId != null && ids.Contains(p.BrandId.Value)).ToListAsync(ct);
            var codigos = await CodigosVivosAsync(db, productos.Select(p => p.Id).ToList(), ct);
            foreach (var p in productos)
                p.SearchText = Catalog.Products.ReglasDeProducto.TextoDeBusqueda(p, renombradas.First(b => b.Id == p.BrandId).Name, codigos[p.Id]);
        }
    }

    internal static async Task<ILookup<int, string>> CodigosVivosAsync(IApplicationDbContext db, List<int> productos, CancellationToken ct) =>
        (await db.ProductBarcodes.AsNoTracking().Where(b => productos.Contains(b.ProductId)).Select(b => new { b.ProductId, b.Barcode }).ToListAsync(ct))
        .ToLookup(b => b.ProductId, b => b.Barcode);
}

/// <summary>La plantilla 4 llena (§0.6). (nuevo)</summary>
public sealed record GetBrandsTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetBrandsTemplateDataQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBrandsTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetBrandsTemplateDataQuery request, CancellationToken ct)
    {
        var filas = (await db.Brands.AsNoTracking().OrderBy(b => b.Code).ToListAsync(ct))
            .Select(b => (IReadOnlyList<object?>)[b.Code, b.Name, b.IsActive]).ToList();
        return Result.Success(DatosDeUnaHoja.De(filas));
    }
}

// ========================================================================================================== categorías --

/// <summary>
/// Plantilla 5 (§5; <c>POST /api/inventory/product-categories/import</c>): el padre puede venir en cualquier fila del mismo
/// archivo; sin ciclos (el error nombra la cadena) y ninguna categoría —tampoco una hija que ya existía, al mover su rama—
/// por debajo del nivel 5. La ruta materializada necesita los Id: se escribe después del primer guardado, en la misma
/// transacción. (nuevo)
/// </summary>
public sealed record ImportProductCategoriesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportProductCategoriesCommandValidator : AbstractValidator<ImportProductCategoriesCommand>
{
    public ImportProductCategoriesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportProductCategoriesCommand>.LargoMaximo);
    }
}

public sealed class ImportProductCategoriesCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor)
    : IRequestHandler<ImportProductCategoriesCommand, Result<ImportResultDto>>
{
    private List<ProductCategory> _todas = [];

    public Task<Result<ImportResultDto>> Handle(ImportProductCategoriesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(K.Definicion, request, ProcesarAsync, ct, EscribirRutasAsync);

    private sealed record FilaDeCategoria(FilaDeImportacion Fila, ProductCategory Categoria, string? Padre, bool Nueva, bool Activo, List<CampoCambiadoDto> Campos);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        _todas = await db.ProductCategories.ToListAsync(ct);
        var porCodigo = _todas.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var porId = _todas.ToDictionary(c => c.Id);
        var leidas = new List<FilaDeCategoria>();

        foreach (var fila in ctx.Datos.Filas)
        {
            var codigo = fila.Codigo(K.Codigo);
            var nombre = fila.Texto(K.Nombre);
            var padre = fila.Codigo(K.Padre);
            var activo = fila.SiNo(K.Activo, porDefecto: true);
            if (!ctx.Datos.LlaveUnica(fila, codigo, K.Codigo) || codigo is null || nombre is null || fila.TieneErrores) continue;

            var nueva = !porCodigo.TryGetValue(codigo, out var categoria);
            var campos = new List<CampoCambiadoDto>();
            if (nueva)
            {
                categoria = new ProductCategory { Code = codigo, Name = nombre, IsActive = activo, Level = 1 };
                porCodigo[codigo] = categoria;
                _todas.Add(categoria);
                campos.Add(new(K.Nombre, null, nombre));
                if (padre is not null) campos.Add(new(K.Padre, null, padre));
            }
            else
            {
                FilasDeCatalogo.Diferencia(campos, K.Nombre, categoria!.Name, nombre);
                var padreActual = categoria.ParentId is int pid && porId.TryGetValue(pid, out var pa) ? pa.Code : null;
                FilasDeCatalogo.Diferencia(campos, K.Padre, padreActual, padre);
                if (categoria.IsActive != activo)
                {
                    FilasDeCatalogo.Diferencia(campos, K.Activo, FilasDeCatalogo.SiNo(categoria.IsActive), FilasDeCatalogo.SiNo(activo));
                    ctx.PedirMotivo();
                }
            }
            leidas.Add(new FilaDeCategoria(fila, categoria!, padre, nueva, activo, campos));
        }

        // El padre de cada fila: de otra fila del archivo o existente.
        var padres = _todas.Where(c => c.Id != 0).ToDictionary(c => c, c => c.ParentId is int p && porId.TryGetValue(p, out var x) ? x : null, ReferenceEqualityComparer.Instance);
        var validas = new List<FilaDeCategoria>();
        foreach (var l in leidas)
        {
            ProductCategory? padre = null;
            if (l.Padre is not null && !porCodigo.TryGetValue(l.Padre, out padre))
            {
                l.Fila.Error(K.Padre, ImportErrors.CellNotFound,
                    $"No hay una categoría «{l.Padre}». Créela en otra fila de este archivo o en Inventario → Categorías.");
                continue;
            }
            if (ReferenceEquals(padre, l.Categoria))
            {
                l.Fila.Error(K.Padre, CatalogErrors.CategoryCycle().Code, $"La categoría «{l.Categoria.Code}» no puede ser su propio padre.");
                continue;
            }
            padres[l.Categoria] = padre;
            validas.Add(l);
        }

        // Sin ciclos: se sube por los padres finales; si se vuelve a la misma, la cadena se nombra.
        var conCiclo = new HashSet<ProductCategory>(ReferenceEqualityComparer.Instance);
        foreach (var l in validas)
        {
            var cadena = new List<ProductCategory> { l.Categoria };
            var actual = padres.GetValueOrDefault(l.Categoria);
            while (actual is not null && cadena.Count <= _todas.Count)
            {
                if (ReferenceEquals(actual, l.Categoria))
                {
                    cadena.Add(actual);
                    l.Fila.Error(K.Padre, CatalogErrors.CategoryCycle().Code,
                        $"El padre forma un ciclo: {string.Join(" › ", cadena.Select(c => c.Code))}. Una categoría no puede quedar debajo de sí misma.");
                    conCiclo.Add(l.Categoria);
                    break;
                }
                cadena.Add(actual);
                actual = padres.GetValueOrDefault(actual);
            }
        }

        // Niveles finales de todas (también las hijas existentes que se mueven con su rama).
        var nivel = new Dictionary<ProductCategory, int>(ReferenceEqualityComparer.Instance);
        int Nivel(ProductCategory c, int guarda = 0)
        {
            if (nivel.TryGetValue(c, out var n)) return n;
            if (guarda > _todas.Count || conCiclo.Contains(c)) return ProductCategory.MaxLevel + 1;
            var p = padres.GetValueOrDefault(c);
            n = p is null ? 1 : Nivel(p, guarda + 1) + 1;
            nivel[c] = n;
            return n;
        }
        var hijas = _todas.Where(c => padres.GetValueOrDefault(c) is not null).ToLookup(c => padres[c]!, ReferenceEqualityComparer.Instance);
        int MasProfunda(ProductCategory c, int guarda = 0) =>
            guarda > _todas.Count ? int.MaxValue : hijas[c].Select(h => MasProfunda(h, guarda + 1)).DefaultIfEmpty(Nivel(c)).Max();

        foreach (var l in validas.Where(v => !conCiclo.Contains(v.Categoria)))
        {
            if (MasProfunda(l.Categoria) > ProductCategory.MaxLevel)
            {
                FilasDeCatalogo.Error(l.Fila, K.Padre, CatalogErrors.CategoryTooDeep());
                continue;
            }

            // Inactivar: sin hijas activas ni productos que no estén inactivos.
            if (!l.Activo && l.Categoria.IsActive && !l.Nueva)
            {
                var hijasActivas = hijas[l.Categoria].Count(h => (leidas.FirstOrDefault(x => ReferenceEquals(x.Categoria, h))?.Activo ?? h.IsActive));
                var productos = await db.Products.CountAsync(p => p.CategoryId == l.Categoria.Id && p.Status != ProductStatus.Inactive, ct);
                if (hijasActivas + productos > 0)
                {
                    FilasDeCatalogo.Error(l.Fila, K.Activo, CatalogErrors.CategoryInUse(hijasActivas, productos));
                    continue;
                }
            }

            var padre = padres.GetValueOrDefault(l.Categoria);
            l.Categoria.Name = l.Fila.Texto(K.Nombre)!;
            l.Categoria.IsActive = l.Activo;
            l.Categoria.Parent = padre;
            l.Categoria.ParentId = padre?.Id is > 0 ? padre.Id : null;
            l.Categoria.Level = (byte)Nivel(l.Categoria);
            if (l.Nueva) db.ProductCategories.Add(l.Categoria);
            ctx.Registrar(l.Fila, l.Categoria.Code, l.Nueva ? AccionDeImportacion.Create : FilasDeCatalogo.Accion(l.Campos), l.Campos);
        }

        // Las hijas existentes de una rama movida cambian de nivel.
        foreach (var c in _todas.Where(c => c.Id != 0 && leidas.All(l => !ReferenceEquals(l.Categoria, c)) && !conCiclo.Contains(c)))
        {
            var n = Nivel(c);
            if (n <= ProductCategory.MaxLevel && c.Level != n) c.Level = (byte)n;
        }
    }

    /// <summary>Con los Id ya asignados, la ruta de cada categoría desde la raíz (sólo cambian las que se movieron o nacieron).</summary>
    private Task EscribirRutasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var porId = _todas.Where(c => c.Id != 0).ToDictionary(c => c.Id);
        foreach (var c in _todas.Where(c => c.Id != 0))
        {
            var ids = new List<int>();
            var actual = c;
            while (actual is not null && ids.Count <= _todas.Count)
            {
                ids.Insert(0, actual.Id);
                actual = actual.ParentId is int p && porId.TryGetValue(p, out var x) ? x : null;
            }
            var ruta = "/" + string.Join('/', ids) + "/";
            if (c.Path != ruta) c.Path = ruta;
        }
        return Task.CompletedTask;
    }
}

/// <summary>La plantilla 5 llena (§0.6), en orden de árbol. (nuevo)</summary>
public sealed record GetProductCategoriesTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetProductCategoriesTemplateDataQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductCategoriesTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetProductCategoriesTemplateDataQuery request, CancellationToken ct)
    {
        var todas = await db.ProductCategories.AsNoTracking().ToListAsync(ct);
        var porId = todas.ToDictionary(c => c.Id);
        var filas = todas.OrderBy(c => c.Level).ThenBy(c => c.Code, StringComparer.Ordinal)
            .Select(c => (IReadOnlyList<object?>)[c.Code, c.Name, c.ParentId is int p && porId.TryGetValue(p, out var x) ? x.Code : null, c.IsActive])
            .ToList();
        return Result.Success(DatosDeUnaHoja.De(filas));
    }
}

/// <summary>Los datos de una plantilla de una sola hoja (<c>Datos</c>). (nuevo)</summary>
internal static class DatosDeUnaHoja
{
    public static DatosDePlantilla De(IReadOnlyList<IReadOnlyList<object?>> filas) =>
        new(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase) { ["Datos"] = filas });
}
