using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 012, T469 (T27, T31; contracts/contabilidad.md §1, §7.1): lo único de Inventario que Contabilidad conoce además de la
/// bandeja. <see cref="DimensionesDeInventario"/> devuelve los códigos vivos de grupos contables, bodegas (con su sucursal y su
/// comportamiento), causas de ajuste y tipos de documento, y las combinaciones operación × grupo × bodega en uso de los tipos cuyo
/// <c>Contabilidad.ModoDePaso</c> vigente no es <c>NoPasa</c>.
/// </summary>
public class DimensionesDeInventarioTests
{
    private static DimensionesDeInventario Dimensiones(KardexDePrueba k) => new(k.C.Db, k.Lector());

    [Fact]
    public async Task El_catalogo_trae_los_codigos_vivos_de_cada_dimension()
    {
        var k = await KardexDePrueba.CrearAsync();
        var inactivo = k.Tipo("ENS");
        inactivo.IsActive = false;
        await k.C.Db.SaveChangesAsync();

        var catalogo = await Dimensiones(k).CatalogoAsync(default);

        catalogo.AccountingGroups.Select(g => g.Code).Should().Contain("ABARR");
        catalogo.Warehouses.Select(w => w.Code).Should().BeEquivalentTo(["PRIN", "B2", "TR01"]);
        var transito = catalogo.Warehouses.Single(w => w.Code == "TR01");
        transito.Behavior.Should().Be(WarehouseBehavior.Transit);
        transito.BranchPublicId.Should().Be(k.Sucursal.PublicId);
        catalogo.AdjustmentCauses.Select(c => c.Code).Should().Contain("MERMA");
        catalogo.DocumentTypes.Select(t => t.Code).Should().Contain(["AJP", "AJN", "CI", "BAJ"]).And.NotContain("ENS");
        catalogo.PointsOfSale.Should().BeEmpty("los puntos de venta llegan con I3 (T622)");
        catalogo.PaymentMeans.Should().BeNull("el catálogo de medios de pago no existe todavía");
    }

    [Fact]
    public async Task Las_combinaciones_en_uso_son_las_de_los_tipos_que_pasan()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.EntradaAsync(k.P2, 5m, 2000m, k.Segunda);
        var (_, salida) = await k.AjusteAsync(k.Borrador("AJN", k.Segunda, k.Causa(), lineas: [k.Linea(k.P2, 1m)]));
        salida.IsSuccess.Should().BeTrue(salida.IsFailure ? salida.Error.Message : string.Empty);
        k.Parametro(ParametrosDeInventario.ContabilidadModoDePaso, "NoPasa", ParameterScopeKind.DocumentType, k.Tipo("AJN").Id);

        var combinaciones = await Dimensiones(k).CombinacionesEnUsoAsync(CatalogoDePruebaHoy, default);

        combinaciones.Select(c => (c.Operation, c.AccountingGroupCode, c.WarehouseCode)).Should().BeEquivalentTo(
        [
            ("AjustePositivo", "ABARR", "PRIN"),
            ("AjustePositivo", "ABARR", "B2"),
        ], "el ajuste negativo no pasa a contabilidad");
        combinaciones.Should().OnlyContain(c => c.DocumentTypeCodes.SequenceEqual(new[] { "AJP" }));
        combinaciones.Should().OnlyContain(c => c.LastUsedAt == CatalogoDePruebaHoy);
    }

    [Fact]
    public async Task Una_fecha_anterior_al_uso_no_trae_combinaciones()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);

        var combinaciones = await Dimensiones(k).CombinacionesEnUsoAsync(CatalogoDePruebaHoy.AddDays(-1), default);

        combinaciones.Should().BeEmpty();
    }

    private static DateOnly CatalogoDePruebaHoy => Catalog.CatalogoDePrueba.Hoy;
}
