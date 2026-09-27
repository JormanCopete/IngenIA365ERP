using FluentAssertions;
using IngenIA365ERP.Shared.Services.Contabilidad;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// T537, T533 (decisiones-transversales §2.1; contracts/contabilidad.md §2.8): los tipos de mensaje se guardan sin tildes y la
/// pantalla los muestra con ellas; la matriz reparte sus roles en las siete familias de pestañas sin dejar uno afuera ni repetirlo.
/// </summary>
public class TextosDeIntegracionTests
{
    [Theory]
    [InlineData("DevolucionRegistrada", "DevoluciónRegistrada")]
    [InlineData("NotaCreditoEmitida", "NotaCréditoEmitida")]
    [InlineData("NotaDebitoEmitida", "NotaDébitoEmitida")]
    [InlineData("CompraRecibida", "CompraRecibida")]
    [InlineData("PeriodoInventarioCerrado", "PeriodoInventarioCerrado")]
    public void El_tipo_de_mensaje_se_muestra_con_tilde(string guardado, string enPantalla) =>
        TextosDeInventario.TipoDeMensaje(guardado).Should().Be(enPantalla);

    [Fact]
    public void Cada_rol_de_la_matriz_cae_en_una_sola_familia()
    {
        var roles = FamiliasDeRolDeInventario.Todas.SelectMany(f => f.Roles).ToList();

        roles.Should().OnlyHaveUniqueItems();
        FamiliasDeRolDeInventario.Todas.Should().HaveCount(7);
        FamiliasDeRolDeInventario.FamiliaDe("MedioDePago")!.Nombre.Should().Be("Medios de pago");
        FamiliasDeRolDeInventario.FamiliaDe("Transito")!.Nombre.Should().Be("Traslados y puentes");
        FamiliasDeRolDeInventario.FamiliaDe("NoExiste").Should().BeNull();
    }
}
