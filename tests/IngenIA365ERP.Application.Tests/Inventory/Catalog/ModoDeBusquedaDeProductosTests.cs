using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// 2026-11-12 (decisión del dueño): la cooperativa decide cómo se busca un producto en las pantallas que lo registran
/// (<c>Comercial.BusquedaDeProductos</c>): «VentanaF2» por defecto o «EnElCampo», con vigencia, por el único lector.
/// </summary>
public class ModoDeBusquedaDeProductosTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 12);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private async Task<string> ModoAsync()
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(Hoy);
        var r = await new GetProductSearchModeQueryHandler(new LectorDeParametros(_db), reloj).Handle(new GetProductSearchModeQuery(), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value.Mode;
    }

    [Fact]
    public async Task Sin_vigencia_es_la_ventana_con_F2()
    {
        (await ModoAsync()).Should().Be(ParametrosDeInventario.BusquedaEnVentanaF2);
    }

    [Fact]
    public async Task Con_una_vigencia_rige_la_de_la_cooperativa_y_una_programada_todavia_no()
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeInventario.Modulo, Key = ParametrosDeInventario.ComercialBusquedaDeProductos, Value = ParametrosDeInventario.BusquedaEnElCampo,
            ValidFrom = Hoy.AddDays(-1), ValidTo = Hoy.AddDays(9), Reason = "Prefieren ver la lista",
        });
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeInventario.Modulo, Key = ParametrosDeInventario.ComercialBusquedaDeProductos, Value = ParametrosDeInventario.BusquedaEnVentanaF2,
            ValidFrom = Hoy.AddDays(10), Reason = "Vuelven a la ventana",
        });
        await _db.SaveChangesAsync();

        (await ModoAsync()).Should().Be(ParametrosDeInventario.BusquedaEnElCampo);
    }

    [Fact]
    public void La_clave_es_una_eleccion_general_entre_los_dos_modos_con_la_ventana_por_defecto()
    {
        var definicion = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComercialBusquedaDeProductos)!;

        definicion.DefectoSeguro.Should().Be(ParametrosDeInventario.BusquedaEnVentanaF2);
        definicion.Interpretar("EnElCampo", CatalogoDeParametros.EntregaVigente).Admitido.Should().BeTrue();
        definicion.Interpretar("Lista", CatalogoDeParametros.EntregaVigente).Admitido.Should().BeFalse();
    }
}
