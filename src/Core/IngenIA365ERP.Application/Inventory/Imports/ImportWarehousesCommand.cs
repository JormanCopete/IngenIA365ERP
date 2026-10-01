using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using B = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeBodegas;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// La plantilla 7 (feature 012, T229; contracts/plantillas.md §7, §0.7; <c>POST /api/inventory/warehouses/import</c>; FR-032,
/// FR-034): bodegas y ubicaciones con <b>la regla única del alta</b>, <see cref="CreateWarehouseCommandHandler.AplicarReglasAsync"/>:
/// toda bodega nace <c>NotActivated</c> con su ubicación <c>GENERAL</c> y la plantilla nunca la activa; con la primera bodega
/// operativa de una sucursal nace su bodega de tránsito, cuyo código fija una fila de tipo tránsito de esa sucursal en el mismo
/// archivo (si no viene, se crea con el código propuesto y la revisión lo anuncia en <c>extra.transitWarehouses</c>); una fila de
/// tránsito que no acompaña a esa primera bodega es <c>Inventory.WarehouseType.TransitIsSystem</c>.
/// <para>
/// <c>stockNegativo</c> con valor crea una vigencia de <c>Existencias.StockNegativoPermitido</c> con ámbito bodega desde hoy
/// (permiso <c>Inventory.Parameters.Manage</c> por columna y motivo): la escribe
/// <see cref="AddParameterVersionCommandHandler.AgregarVigenciasAsync"/> con el Id ya guardado de la bodega, en la misma
/// transacción. Igual al valor vigente, no crea nada. Las ubicaciones: única por bodega, ninguna en tránsito, y al terminar cada
/// bodega tiene exactamente una por defecto. (nuevo)
/// </para>
/// </summary>
public sealed record ImportWarehousesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportWarehousesCommandValidator : AbstractValidator<ImportWarehousesCommand>
{
    public ImportWarehousesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportWarehousesCommand>.LargoMaximo);
    }
}

/// <summary>La bodega de tránsito que nace con la primera bodega de una sucursal (<c>extra.transitWarehouses</c>). (nuevo)</summary>
public sealed record BodegaDeTransitoPropuestaDto(string Branch, string Code, string Name, bool FromFile);

public sealed class ImportWarehousesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    IDateTimeService reloj,
    ILectorDeParametros parametros,
    IExistenciasParaElCatalogo existencias)
    : IRequestHandler<ImportWarehousesCommand, Result<ImportResultDto>>
{
    /// <summary>La clave de <see cref="ImportResultDto.Extra"/> con las bodegas de tránsito que nacen.</summary>
    public const string ExtraDeTransito = "transitWarehouses";

    private readonly List<(FilaDeImportacion Fila, Warehouse Bodega, bool Valor)> _vigencias = [];

    public Task<Result<ImportResultDto>> Handle(ImportWarehousesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(B.Definicion, request, ProcesarAsync, ct, EscribirVigenciasAsync);

    private sealed record FilaDeBodega(FilaDeImportacion Fila, string Codigo, string Nombre, Branch Sucursal, WarehouseType Tipo, bool? StockNegativo, bool Activa);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var sucursales = CatalogoCitado<Branch>.Desde(await db.Branches.ToListAsync(ct), b => b, b => b.LegacyCode, b => b.Name);
        var tipos = CatalogoCitado<WarehouseType>.Desde(await db.WarehouseTypes.ToListAsync(ct), t => t, t => t.Code, t => t.Name);
        var existentes = await db.Warehouses.Include(w => w.Locations).ToListAsync(ct);
        var porCodigo = existentes.ToDictionary(w => w.Code, StringComparer.OrdinalIgnoreCase);
        var definicion = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, ParametrosDeInventario.ExistenciasStockNegativoPermitido)!;

        // ------------------------------------------------------------------------------------ lectura de Bodegas --
        var hoja = ctx.Hoja(B.HojaBodegas);
        var leidas = new List<FilaDeBodega>();
        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(B.Codigo);
            var nombre = fila.Texto(B.Nombre);
            var sucursal = fila.Referencia(B.Sucursal, sucursales, "una sucursal", "en Maestros → Agencias");
            var tipo = fila.Referencia(B.Tipo, tipos, "un tipo de bodega", "en Inventario → Bodegas → Tipos de bodega");
            var negativo = fila.SiNoIndiferente(B.StockNegativo);
            var activa = fila.SiNo(B.Activa, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, B.Codigo) || codigo is null || nombre is null || sucursal is null || tipo is null || fila.TieneErrores) continue;
            leidas.Add(new FilaDeBodega(fila, codigo, nombre, sucursal, tipo, negativo, activa));
        }

        // Las filas de tránsito nuevas se consumen con la primera bodega operativa nueva de su sucursal.
        var transitoPorSucursal = new Dictionary<int, FilaDeBodega>();
        foreach (var t in leidas.Where(l => l.Tipo.Behavior == WarehouseBehavior.Transit && !porCodigo.ContainsKey(l.Codigo)))
            if (!transitoPorSucursal.TryAdd(t.Sucursal.Id, t))
                FilasDeCatalogo.Error(t.Fila, B.Tipo, WarehouseErrors.WarehouseTypeTransitIsSystem());

        var anunciadas = new List<BodegaDeTransitoPropuestaDto>();
        var consumidas = new HashSet<FilaDeBodega>();
        foreach (var l in leidas.Where(l => l.Tipo.Behavior != WarehouseBehavior.Transit || porCodigo.ContainsKey(l.Codigo)))
        {
            if (porCodigo.TryGetValue(l.Codigo, out var existente))
            {
                await ActualizarAsync(ctx, l, existente, definicion, hoy, ct);
                continue;
            }

            // Nueva: la regla única del alta. Si es la primera de su sucursal, lleva el tránsito de la fila de tránsito.
            var primera = !db.Warehouses.Local.Any(w => w.BranchId == l.Sucursal.Id && !w.IsDeleted) && existentes.All(w => w.BranchId != l.Sucursal.Id);
            var filaDeTransito = primera && transitoPorSucursal.TryGetValue(l.Sucursal.Id, out var tr) ? tr : null;
            var alta = await CreateWarehouseCommandHandler.AplicarReglasAsync(db,
                new DatosDeBodega(l.Codigo, l.Nombre, l.Sucursal.PublicId, l.Tipo.PublicId, null,
                    filaDeTransito is null ? null : new BodegaDeTransitoPedida(filaDeTransito.Codigo, filaDeTransito.Nombre)), ct);
            if (alta.IsFailure)
            {
                FilasDeCatalogo.Error(l.Fila, ColumnaDe(alta.Error), alta.Error);
                continue;
            }

            var bodega = alta.Value.Bodega;
            bodega.IsActive = l.Activa;
            foreach (var aviso in alta.Value.Avisos) l.Fila.Aviso(B.Sucursal, aviso.Code, aviso.Message);
            ctx.Registrar(l.Fila, l.Codigo, AccionDeImportacion.Create,
                [new(B.Nombre, null, l.Nombre), new(B.Sucursal, null, l.Sucursal.LegacyCode ?? l.Sucursal.Name), new(B.Tipo, null, l.Tipo.Code)]);
            Negativo(ctx, l, bodega);

            if (alta.Value.Transito is { } transito)
            {
                anunciadas.Add(new BodegaDeTransitoPropuestaDto(l.Sucursal.LegacyCode ?? l.Sucursal.Name, transito.Code, transito.Name, filaDeTransito is not null));
                if (filaDeTransito is not null)
                {
                    consumidas.Add(filaDeTransito);
                    transito.IsActive = filaDeTransito.Activa;
                    ctx.Registrar(filaDeTransito.Fila, transito.Code, AccionDeImportacion.Create,
                        [new(B.Nombre, null, transito.Name), new(B.Tipo, null, filaDeTransito.Tipo.Code)]);
                    Negativo(ctx, filaDeTransito, transito);
                }
            }
        }

        // Una fila de tránsito que no acompañó a la primera bodega operativa nueva de su sucursal: el sistema la crea sola.
        foreach (var t in transitoPorSucursal.Values.Where(t => !consumidas.Contains(t) && !t.Fila.TieneErrores))
            FilasDeCatalogo.Error(t.Fila, B.Tipo, WarehouseErrors.WarehouseTypeTransitIsSystem());

        if (anunciadas.Count > 0) ctx.Extra[ExtraDeTransito] = anunciadas;

        await UbicacionesAsync(ctx, ct);
    }

    /// <summary>Una bodega que ya existe: nombre, tipo del mismo comportamiento, activa; la sucursal y el código no cambian.</summary>
    private async Task ActualizarAsync(ContextoDeImportacion ctx, FilaDeBodega l, Warehouse bodega, DefinicionDeParametro definicion, DateOnly hoy, CancellationToken ct)
    {
        if (bodega.BranchId != l.Sucursal.Id)
        {
            l.Fila.Error(B.Sucursal, ImportErrors.CellFormat, $"La bodega {bodega.Code} es de otra sucursal: la sucursal de una bodega no cambia.");
            return;
        }
        if (l.Tipo.Behavior != bodega.Behavior)
        {
            FilasDeCatalogo.Error(l.Fila, B.Tipo, WarehouseErrors.WarehouseBehaviorLocked());
            return;
        }
        var campos = new List<CampoCambiadoDto>();
        var tipoActual = bodega.WarehouseType?.Code ?? (await db.WarehouseTypes.Where(t => t.Id == bodega.WarehouseTypeId).Select(t => t.Code).FirstOrDefaultAsync(ct));
        FilasDeCatalogo.Diferencia(campos, B.Nombre, bodega.Name, l.Nombre);
        FilasDeCatalogo.Diferencia(campos, B.Tipo, tipoActual, l.Tipo.Code);
        if (bodega.IsActive != l.Activa)
        {
            if (!l.Activa)
            {
                var hay = await existencias.DeBodegaAsync(bodega.Id, ct);
                if (hay.HayExistencia)
                {
                    FilasDeCatalogo.Error(l.Fila, B.Activa, bodega.EsTransito
                        ? WarehouseErrors.WarehouseTransitHasStock(hay.Products, hay.Quantity)
                        : WarehouseErrors.WarehouseHasStock(hay.Products, hay.Quantity));
                    return;
                }
            }
            FilasDeCatalogo.Diferencia(campos, B.Activa, FilasDeCatalogo.SiNo(bodega.IsActive), FilasDeCatalogo.SiNo(l.Activa));
            ctx.PedirMotivo();
        }

        if (l.StockNegativo is { } negativo)
        {
            var vigente = await parametros.LeerComoAsync<bool>(ParametrosDeInventario.Modulo, ParametrosDeInventario.ExistenciasStockNegativoPermitido,
                hoy, ParameterScopeKind.Warehouse, bodega.Id, ct);
            if (vigente.IsFailure || vigente.Value != negativo)
            {
                if (await AddParameterVersionCommandHandler.CruceAsync(db, definicion, ParameterScopeKind.Warehouse, bodega.Id, hoy, ct) is { } desde)
                {
                    FilasDeCatalogo.Error(l.Fila, B.StockNegativo, ErroresDeParametros.SeCruza(desde));
                    return;
                }
                FilasDeCatalogo.Diferencia(campos, B.StockNegativo, vigente.IsSuccess ? FilasDeCatalogo.SiNo(vigente.Value) : null, FilasDeCatalogo.SiNo(negativo));
                Negativo(ctx, l, bodega);
            }
        }

        bodega.Name = l.Nombre;
        bodega.WarehouseTypeId = l.Tipo.Id;
        bodega.WarehouseType = l.Tipo;
        bodega.IsActive = l.Activa;
        ctx.Registrar(l.Fila, l.Codigo, FilasDeCatalogo.Accion(campos), campos);
    }

    /// <summary>Anota la vigencia de <c>stockNegativo</c> para escribirla con el Id de la bodega; pide motivo.</summary>
    private void Negativo(ContextoDeImportacion ctx, FilaDeBodega l, Warehouse bodega)
    {
        if (l.StockNegativo is not { } valor) return;
        ctx.PedirMotivo();
        _vigencias.Add((l.Fila, bodega, valor));
    }

    /// <summary>Tras el primer guardado: las vigencias de <c>stockNegativo</c>, desde hoy, con el motivo de la importación.</summary>
    private async Task EscribirVigenciasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var definicion = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, ParametrosDeInventario.ExistenciasStockNegativoPermitido)!;
        foreach (var (fila, bodega, valor) in _vigencias)
        {
            var r = await AddParameterVersionCommandHandler.AgregarVigenciasAsync(db, definicion, ParameterScopeKind.Warehouse, [bodega.Id],
                valor ? "true" : "false", reloj.HoyLocal, ctx.Motivo ?? string.Empty, null, ct);
            if (r.IsFailure) FilasDeCatalogo.Error(fila, B.StockNegativo, r.Error);
        }
    }

    private static string ColumnaDe(Error error) => error.Code switch
    {
        "Catalogo.CodigoDuplicado" => B.Codigo,
        "Inventory.Branch.NotFound" => B.Sucursal,
        _ => B.Tipo,
    };

    // ---------------------------------------------------------------------------------------------- Ubicaciones --

    private async Task UbicacionesAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoja = ctx.Hoja(B.HojaUbicaciones);
        if (hoja.Filas.Count == 0) return;

        // Las bodegas: las guardadas y las que dejó esta misma importación en el contexto.
        var bodegas = db.Warehouses.Local.Where(w => !w.IsDeleted).GroupBy(w => w.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var porDefectoDelArchivo = new Dictionary<Warehouse, FilaDeImportacion>(ReferenceEqualityComparer.Instance);

        foreach (var fila in hoja.Filas)
        {
            var codigoBodega = fila.Codigo(B.Bodega);
            var codigo = fila.Codigo(B.Codigo);
            var nombre = fila.Texto(B.Nombre);
            var porDefecto = fila.SiNo(B.PorDefecto);
            var activa = fila.SiNo(B.Activa, porDefecto: true);
            if (codigoBodega is null || codigo is null || nombre is null || fila.TieneErrores) continue;
            if (!bodegas.TryGetValue(codigoBodega, out var bodega))
            {
                fila.Error(B.Bodega, ImportErrors.CellNotFound, $"No hay una bodega «{codigoBodega}». Créela en la hoja Bodegas de este archivo o en Inventario → Bodegas.");
                continue;
            }
            if (!hoja.LlaveUnica(fila, $"{bodega.Code}|{codigo}", B.Codigo)) continue;
            if (bodega.EsTransito)
            {
                FilasDeCatalogo.Error(fila, B.Bodega, WarehouseErrors.LocationTransitHasOnlyDefault());
                continue;
            }
            if (porDefecto && !porDefectoDelArchivo.TryAdd(bodega, fila))
            {
                fila.Error(B.PorDefecto, ImportErrors.CellFormat,
                    $"La bodega {bodega.Code} ya tiene otra ubicación por defecto en la fila {porDefectoDelArchivo[bodega].Numero}: cada bodega tiene exactamente una.");
                continue;
            }

            var llave = $"{bodega.Code} {codigo}";
            var ubicacion = bodega.Locations.FirstOrDefault(u => !u.IsDeleted && string.Equals(u.Code, codigo, StringComparison.OrdinalIgnoreCase));
            if (ubicacion is null)
            {
                if (porDefecto && !activa)
                {
                    FilasDeCatalogo.Error(fila, B.Activa, WarehouseErrors.LocationIsDefault());
                    continue;
                }
                bodega.Locations.Add(new WarehouseLocation { Warehouse = bodega, Code = codigo, Name = nombre, IsDefault = false, IsActive = activa });
                ctx.Registrar(fila, llave, AccionDeImportacion.Create, [new(B.Nombre, null, nombre), new(B.PorDefecto, null, FilasDeCatalogo.SiNo(porDefecto))]);
                continue;
            }

            var campos = new List<CampoCambiadoDto>();
            FilasDeCatalogo.Diferencia(campos, B.Nombre, ubicacion.Name, nombre);
            if (ubicacion.IsActive != activa)
            {
                if (!activa)
                {
                    if (ubicacion.IsDefault && !porDefectoDelArchivo.ContainsKey(bodega) || porDefecto)
                    {
                        FilasDeCatalogo.Error(fila, B.Activa, WarehouseErrors.LocationIsDefault());
                        continue;
                    }
                    var hay = ubicacion.Id == 0 ? ExistenciaAgregada.Ninguna : await existencias.DeUbicacionAsync(ubicacion.Id, ct);
                    if (hay.HayExistencia)
                    {
                        FilasDeCatalogo.Error(fila, B.Activa, WarehouseErrors.LocationHasStock(hay.Products, hay.Quantity));
                        continue;
                    }
                }
                FilasDeCatalogo.Diferencia(campos, B.Activa, FilasDeCatalogo.SiNo(ubicacion.IsActive), FilasDeCatalogo.SiNo(activa));
                ctx.PedirMotivo();
            }
            if (porDefecto && !ubicacion.IsDefault) FilasDeCatalogo.Diferencia(campos, B.PorDefecto, "no", "sí");
            ubicacion.Name = nombre;
            ubicacion.IsActive = activa;
            ctx.Registrar(fila, llave, FilasDeCatalogo.Accion(campos), campos);
        }

        // Al terminar, cada bodega tiene exactamente una por defecto: la que marcó el archivo desmarca la anterior.
        foreach (var (bodega, fila) in porDefectoDelArchivo)
        {
            var codigo = fila.Codigo(B.Codigo);
            var elegida = bodega.Locations.First(u => !u.IsDeleted && string.Equals(u.Code, codigo, StringComparison.OrdinalIgnoreCase));
            foreach (var u in bodega.Locations.Where(u => !u.IsDeleted && u.IsDefault && !ReferenceEquals(u, elegida))) u.IsDefault = false;
            elegida.IsDefault = true;
            elegida.IsActive = true;
        }
    }
}

/// <summary>La plantilla 7 llena (§0.6): bodegas del alcance y sus ubicaciones. (nuevo)</summary>
public sealed record GetWarehousesTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetWarehousesTemplateDataQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj, ILectorDeParametros parametros)
    : IRequestHandler<GetWarehousesTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetWarehousesTemplateDataQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var bodegas = (await db.Warehouses.AsNoTracking().Include(w => w.Locations).OrderBy(w => w.Code).ToListAsync(ct))
            .Where(w => alcance.IncluyeBodega(w.Id)).ToList();
        var sucursales = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, ct);
        var tipos = await db.WarehouseTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);

        var filasBodegas = new List<IReadOnlyList<object?>>();
        foreach (var w in bodegas)
        {
            var sucursal = sucursales.GetValueOrDefault(w.BranchId);
            var negativo = await parametros.LeerComoAsync<bool>(ParametrosDeInventario.Modulo, ParametrosDeInventario.ExistenciasStockNegativoPermitido,
                reloj.HoyLocal, ParameterScopeKind.Warehouse, w.Id, ct);
            filasBodegas.Add([w.Code, w.Name, sucursal?.LegacyCode ?? sucursal?.Name, tipos.GetValueOrDefault(w.WarehouseTypeId)?.Code,
                negativo.IsSuccess ? negativo.Value : null, w.IsActive]);
        }
        var filasUbicaciones = bodegas.Where(w => !w.EsTransito)
            .SelectMany(w => w.Locations.OrderBy(l => l.Code).Select(l => (IReadOnlyList<object?>)[w.Code, l.Code, l.Name, l.IsDefault, l.IsActive]))
            .ToList();

        return Result.Success(new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [B.HojaBodegas] = filasBodegas,
            [B.HojaUbicaciones] = filasUbicaciones,
        }));
    }
}
