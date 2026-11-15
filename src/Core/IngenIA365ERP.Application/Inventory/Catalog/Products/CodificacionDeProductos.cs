using System.Globalization;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>Cómo codifica la cooperativa a la fecha: manual o automática, y los dígitos del subgrupo y del consecutivo. (nuevo)</summary>
public sealed record ReglasDeCodificacion(bool Automatica, int DigitosDelSubgrupo, int DigitosDelConsecutivo)
{
    /// <summary>La codificación de siempre: la persona digita el código.</summary>
    public static ReglasDeCodificacion Manual { get; } = new(false, 2, 3);
}

/// <summary>
/// Un código armado: el código del producto, el número de la marca en la categoría y si ese número es nuevo (la pareja no tenía
/// número y éste es el siguiente libre). (nuevo)
/// </summary>
public sealed record CodigoArmado(string Codigo, string Segmento, bool SegmentoNuevo);

/// <summary>
/// La codificación automática de productos (decisión del dueño del 2026-11-12), en un solo sitio. Con
/// <c>Productos.Codificacion = Automatica</c> el código de un producto nuevo es <b>categoría + subgrupo + consecutivo</b>:
/// <list type="bullet">
/// <item>el <b>grupo</b> es el código de la categoría del producto, tal como está (<c>01</c>);</item>
/// <item>el <b>subgrupo</b> es el número de la marca <b>dentro de esa categoría</b> (<see cref="ProductCodeSubgroup"/>): si la pareja
/// ya lo tiene, ése; si no, el que ya usan sus productos (los que se cargaron con su código, por ejemplo al recodificar); si tampoco,
/// el siguiente al mayor usado en la categoría (el primer hueco libre si se llegó al tope). Al asignar queda registrado y no cambia;</item>
/// <item>el <b>consecutivo</b> es el siguiente al mayor usado del prefijo <c>grupo + subgrupo</c>, contando los productos dados de
/// baja: un código no se reutiliza. No es «cantidad + 1»: con bajas, la cantidad se queda atrás del último número.</item>
/// </list>
/// Los dígitos del subgrupo y del consecutivo son parámetros (2 y 3 por defecto: <c>01 01 013</c>). Proponer (la pantalla) no
/// escribe ni bloquea; asignar (el alta) toma el candado de la categoría (<see cref="ClavesDeCerrojo.CodificacionDeProductos"/>)
/// antes de leer, y deja la fila del subgrupo nuevo en el contexto para que la guarde el mismo <c>SaveChanges</c> del producto. (nuevo)
/// </summary>
public sealed class CodificacionDeProductos(IApplicationDbContext db, ILectorDeParametros lector, ICerrojoPorClave cerrojo)
{
    /// <summary>Las reglas vigentes a <paramref name="fecha"/>; sin parámetro registrado, los defectos (manual, 2 y 3).</summary>
    public async Task<ReglasDeCodificacion> ReglasAsync(DateOnly fecha, CancellationToken ct)
    {
        var modo = await lector.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ProductosCodificacion, fecha, ct: ct);
        var automatica = modo.IsSuccess && string.Equals(modo.Value.Texto, ParametrosDeInventario.CodificacionAutomatica, StringComparison.Ordinal);
        if (!automatica) return ReglasDeCodificacion.Manual;

        return new ReglasDeCodificacion(true,
            await EnteroAsync(ParametrosDeInventario.ProductosDigitosDelSubgrupo, fecha, ReglasDeCodificacion.Manual.DigitosDelSubgrupo, ct),
            await EnteroAsync(ParametrosDeInventario.ProductosDigitosDelConsecutivo, fecha, ReglasDeCodificacion.Manual.DigitosDelConsecutivo, ct));
    }

    private async Task<int> EnteroAsync(string clave, DateOnly fecha, int porDefecto, CancellationToken ct)
    {
        var leido = await lector.LeerComoAsync<int>(ParametrosDeInventario.Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess && leido.Value > 0 ? leido.Value : porDefecto;
    }

    /// <summary>El código que tendría hoy un producto de la pareja, sin bloquear ni escribir (la pantalla lo muestra).</summary>
    public Task<Result<CodigoArmado>> ProponerAsync(ProductCategory categoria, Brand? marca, ReglasDeCodificacion reglas, CancellationToken ct) =>
        ArmarAsync(categoria, marca, reglas, asignar: false, ct);

    /// <summary>
    /// El código del producto que se está creando: bloquea la categoría hasta el fin de la transacción y, si la pareja no tenía
    /// número, agrega su fila al contexto. Quien llama guarda.
    /// </summary>
    public async Task<Result<CodigoArmado>> AsignarAsync(ProductCategory categoria, Brand? marca, ReglasDeCodificacion reglas, CancellationToken ct)
    {
        await cerrojo.BloquearAsync(ClavesDeCerrojo.CodificacionDeProductos(categoria.Id), ct);
        return await ArmarAsync(categoria, marca, reglas, asignar: true, ct);
    }

    private async Task<Result<CodigoArmado>> ArmarAsync(ProductCategory categoria, Brand? marca, ReglasDeCodificacion reglas, bool asignar,
        CancellationToken ct)
    {
        if (marca is null) return Result.Failure<CodigoArmado>(CatalogErrors.ProductCodeNeedsBrand());

        var grupo = categoria.Code;
        var largo = grupo.Length + reglas.DigitosDelSubgrupo + reglas.DigitosDelConsecutivo;
        if (largo > CodigoDeCatalogo.LargoLargo)
            return Result.Failure<CodigoArmado>(CatalogErrors.ProductCodeTooLong(grupo, largo, CodigoDeCatalogo.LargoLargo));

        var segmento = await SegmentoAsync(categoria, marca, reglas, ct);
        if (segmento.IsFailure) return Result.Failure<CodigoArmado>(segmento.Error);
        var (numero, nuevo) = segmento.Value;

        var prefijo = grupo + numero;
        var usados = await SufijosAsync(prefijo, reglas.DigitosDelConsecutivo, ct);
        var tope = Tope(reglas.DigitosDelConsecutivo);
        var siguiente = (usados.Count == 0 ? 0 : usados.Max()) + 1;
        if (siguiente > tope) return Result.Failure<CodigoArmado>(CatalogErrors.ProductCodeSequenceFull(prefijo, tope));

        if (asignar && nuevo)
            db.ProductCodeSubgroups.Add(new ProductCodeSubgroup { CategoryId = categoria.Id, Category = categoria, BrandId = marca.Id, Brand = marca, Segment = numero });

        return Result.Success(new CodigoArmado(prefijo + Relleno(siguiente, reglas.DigitosDelConsecutivo), numero, nuevo));
    }

    /// <summary>El número de la marca en la categoría: el registrado, el que ya usan sus productos o el siguiente libre.</summary>
    private async Task<Result<(string Numero, bool Nuevo)>> SegmentoAsync(ProductCategory categoria, Brand marca, ReglasDeCodificacion reglas,
        CancellationToken ct)
    {
        var registrados = await db.ProductCodeSubgroups.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.CategoryId == categoria.Id)
            .Select(s => new { s.BrandId, s.Segment, s.IsDeleted })
            .ToListAsync(ct);
        if (registrados.FirstOrDefault(s => s.BrandId == marca.Id && !s.IsDeleted) is { } propio)
            return Result.Success((propio.Segment, false));

        // Los productos con código del esquema en esta categoría (vivos y de baja): de ahí salen los números ya usados y, para la
        // pareja, el que ya trae (lo que se cargó con su código antes de que existiera la fila).
        var digitos = reglas.DigitosDelSubgrupo;
        var conCodigo = await ProductosDelEsquemaAsync(categoria.Code, digitos + reglas.DigitosDelConsecutivo, ct);
        var ocupados = registrados.Select(s => s.Segment)
            .Concat(conCodigo.Select(p => p.Codigo.Substring(categoria.Code.Length, digitos)))
            .Where(s => s.Length == digitos && s.All(char.IsAsciiDigit))
            .ToHashSet(StringComparer.Ordinal);
        var deOtraMarca = registrados.Where(s => !s.IsDeleted && s.BrandId != marca.Id).Select(s => s.Segment).ToHashSet(StringComparer.Ordinal);

        var queTrae = conCodigo
            .Where(p => p.CategoryId == categoria.Id && p.BrandId == marca.Id)
            .Select(p => p.Codigo.Substring(categoria.Code.Length, digitos))
            .Where(s => !deOtraMarca.Contains(s))
            .GroupBy(s => s, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();
        if (queTrae is not null) return Result.Success((queTrae, true));

        var tope = Tope(digitos);
        var mayor = ocupados.Count == 0 ? 0 : ocupados.Max(s => int.Parse(s, CultureInfo.InvariantCulture));
        var siguiente = mayor + 1 <= tope
            ? mayor + 1
            : Enumerable.Range(1, tope).FirstOrDefault(n => !ocupados.Contains(Relleno(n, digitos)));
        return siguiente == 0
            ? Result.Failure<(string, bool)>(CatalogErrors.ProductCodeSubgroupsFull(categoria.Code, tope))
            : Result.Success((Relleno(siguiente, digitos), true));
    }

    /// <summary>
    /// Los productos (vivos y de baja) cuyo código es <paramref name="grupo"/> seguido de <paramref name="digitos"/> dígitos: los
    /// del esquema de esa categoría, aunque después hayan cambiado de categoría o de marca (el código no cambia).
    /// </summary>
    private async Task<List<(string Codigo, int CategoryId, int? BrandId)>> ProductosDelEsquemaAsync(string grupo, int digitos, CancellationToken ct)
    {
        var largo = grupo.Length + digitos;
        var filas = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.Code.StartsWith(grupo) && p.Code.Length == largo)
            .Select(p => new { p.Code, p.CategoryId, p.BrandId })
            .ToListAsync(ct);
        return filas
            .Where(p => p.Code.StartsWith(grupo, StringComparison.Ordinal) && p.Code.Skip(grupo.Length).All(char.IsAsciiDigit))
            .Select(p => (p.Code, p.CategoryId, p.BrandId))
            .ToList();
    }

    /// <summary>Los consecutivos ya usados del prefijo, contando los productos dados de baja.</summary>
    private async Task<List<int>> SufijosAsync(string prefijo, int digitos, CancellationToken ct)
    {
        var largo = prefijo.Length + digitos;
        var codigos = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.Code.StartsWith(prefijo) && p.Code.Length == largo)
            .Select(p => p.Code)
            .ToListAsync(ct);
        return codigos
            .Where(c => c.StartsWith(prefijo, StringComparison.Ordinal))
            .Select(c => c[prefijo.Length..])
            .Where(s => s.All(char.IsAsciiDigit))
            .Select(s => int.Parse(s, CultureInfo.InvariantCulture))
            .ToList();
    }

    private static int Tope(int digitos) => (int)Math.Pow(10, digitos) - 1;

    private static string Relleno(int numero, int digitos) => numero.ToString(CultureInfo.InvariantCulture).PadLeft(digitos, '0');
}
