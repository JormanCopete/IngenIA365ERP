using FluentAssertions;
using IngenIA365ERP.Shared.Services.Security;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Security;

/// <summary>2026-11-19: el mapa del menú que usan la pantalla de Roles y el cierre de un clic de los módulos.</summary>
public class MapaDelMenuTests
{
    [Theory]
    [InlineData("Accounting.Vouchers.View", MapaDelMenu.Peso.Lectura)]
    [InlineData("Accounting.Vouchers.Create", MapaDelMenu.Peso.Operacion)]
    [InlineData("Accounting.Periods.Reopen", MapaDelMenu.Peso.Sensible)]
    [InlineData("Receipts.Voiding.Approve", MapaDelMenu.Peso.Sensible)]
    [InlineData("Security.Users.AssignRole", MapaDelMenu.Peso.Sensible)]
    [InlineData("Inventory.Warehouses.AcceptActivationDifference", MapaDelMenu.Peso.Sensible)]
    public void El_peso_sale_de_la_accion_y_del_modulo(string codigo, MapaDelMenu.Peso esperado) =>
        MapaDelMenu.PesoDe(codigo).Should().Be(esperado);

    [Fact]
    public void Roles_dice_en_que_opcion_del_menu_se_usa()
    {
        var usos = MapaDelMenu.DondeSeUsa("Security.Roles.View");

        usos.Should().Contain(u => u.Opcion.Ruta == "/security/roles" && u.Uso == MapaDelMenu.Uso.Abre);
        usos.First(u => u.Opcion.Ruta == "/security/roles").Opcion.Camino.Should().Be("Administracion › Roles y Permisos");
    }

    [Fact]
    public void Un_permiso_que_no_pide_ninguna_pantalla_no_tiene_opcion() =>
        MapaDelMenu.DondeSeUsa("No.Existe.Nunca").Should().BeEmpty();

    [Theory]
    [InlineData("Administracion", "/security/roles", true)]
    [InlineData("Administracion", "/security/roles/5b4c0a6e-1d1f-4c55-9d7e-0d5f7d1b8a10", true)]
    [InlineData("Administracion", "/security/roles?x=1", true)]
    [InlineData("Contabilidad", "/security/roles", false)]
    [InlineData("Contabilidad", "/contabilidad/comprobantes", true)]
    [InlineData("Sistema", "/contabilidad/comprobantes", false)]
    [InlineData("Sistema", "/", true)]
    public void Sabe_si_el_modulo_contiene_la_pantalla(string modulo, string ruta, bool esperado) =>
        MapaDelMenu.ElModuloContiene(modulo, ruta).Should().Be(esperado);

    [Fact]
    public void Todo_modulo_de_permisos_tiene_nombre_en_espanol()
    {
        foreach (var codigo in new[] { "Accounting", "Inventory", "Lending", "Payroll", "Receipts", "Treasury", "Security", "Core" })
        {
            MapaDelMenu.NombreDelModulo(codigo).Should().NotBe(codigo);
        }
    }
}
