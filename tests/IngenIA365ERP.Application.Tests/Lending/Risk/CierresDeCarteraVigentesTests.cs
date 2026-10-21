using FluentAssertions;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Risk;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, bloque 12C (FR-012, D16, US9-2): hasta qué día hay cierre de Cartera vigente para la regla de la fecha valor de los pagos. Es el
/// último día del cierre mensual vigente más reciente; el base no cuenta (lo anterior a su corte lo rechaza la regla del corte) y el reabierto
/// tampoco. Defecto que destapó la e2e del cierre: el esqueleto <see cref="SinCierresDeCartera"/> de US5 seguía registrado, así que un pago con
/// fecha valor de un mes cerrado se aplicaba sin la atribución <c>Lending.Payments.BackdateBeforeClose</c>.
/// </summary>
public class CierresDeCarteraVigentesTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync() => _c = await CierreDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Con_solo_el_base_no_hay_mes_cerrado()
    {
        _c.Base.Should().NotBeNull();
        (await new CierresDeCarteraVigentes(_c.Db).CerradoHastaAsync(default)).Should().BeNull("el base no es un cierre mensual");
    }

    [Fact]
    public async Task El_cierre_mensual_vigente_cierra_hasta_su_ultimo_dia_y_el_reabierto_deja_de_contar()
    {
        await _c.CausarDiciembreAsync();
        var diciembre = await _c.CerrarAsync(2026, 12);

        (await new CierresDeCarteraVigentes(_c.Db).CerradoHastaAsync(default)).Should().Be(CierreDePrueba.FinDeDiciembre);

        await _c.ReabrirAsync(diciembre);

        (await new CierresDeCarteraVigentes(_c.Db).CerradoHastaAsync(default)).Should().BeNull("un cierre reabierto no cierra el mes");
    }

    [Fact]
    public void La_aplicacion_registra_los_cierres_de_verdad_y_no_el_esqueleto()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();

        services.Single(d => d.ServiceType == typeof(ICierresDeCartera)).ImplementationType.Should().Be(typeof(CierresDeCarteraVigentes));
    }
}
