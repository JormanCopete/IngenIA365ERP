using FluentAssertions;
using IngenIA365ERP.Shared.Services.Core;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Core;

/// <summary>
/// Feature 009 (FR-035, R7): la pantalla de Agencias vincula cada agencia con su oficina. Una oficina ya vinculada a otra
/// agencia aparece con el nombre de esa agencia y deshabilitada, la propia no; un vínculo a una oficina inactiva no se pierde
/// al editar; y las agencias sin oficina se nombran. (nuevo)
/// </summary>
public class VinculoAgenciaOficinaTests
{
    private static readonly Guid Centro = Guid.NewGuid(), Norte = Guid.NewGuid(), Vieja = Guid.NewGuid();
    private static readonly Guid AgPrincipal = Guid.NewGuid(), AgNorte = Guid.NewGuid(), AgSur = Guid.NewGuid();

    private static readonly OficinaDeLaCooperativa[] Oficinas =
    [
        new(Centro, "001", "Centro"),
        new(Norte, "002", "Norte"),
        new(Vieja, "009", "Vieja", IsActive: false),
    ];

    private static AgenciaConOficina[] Agencias(Guid? sur = null) =>
    [
        new(AgPrincipal, "Principal", Centro),
        new(AgNorte, "Agencia Norte", null),
        new(AgSur, "Agencia Sur", sur),
    ];

    [Fact]
    public void Al_crear_la_oficina_de_otra_agencia_sale_con_su_nombre_y_deshabilitada()
    {
        var opciones = VinculoAgenciaOficina.Opciones(Oficinas, Agencias(), editando: null);

        opciones.Should().HaveCount(2, "las inactivas sólo aparecen si la agencia ya está vinculada a una");
        var centro = opciones.Single(o => o.PublicId == Centro);
        centro.Ocupada.Should().BeTrue();
        centro.Etiqueta.Should().Be("001 · Centro — vinculada a «Principal»");
        opciones.Single(o => o.PublicId == Norte).Ocupada.Should().BeFalse();
        opciones[0].PublicId.Should().Be(Norte, "las libres van primero");
    }

    [Fact]
    public void Al_editar_la_oficina_propia_no_cuenta_como_ocupada()
    {
        var opciones = VinculoAgenciaOficina.Opciones(Oficinas, Agencias(), editando: AgPrincipal);

        opciones.Single(o => o.PublicId == Centro).Should().Be(new OpcionDeOficina(Centro, "001 · Centro", false));
    }

    [Fact]
    public void Un_vinculo_a_una_oficina_inactiva_sigue_en_la_lista_al_editar()
    {
        var opciones = VinculoAgenciaOficina.Opciones(Oficinas, Agencias(sur: Vieja), editando: AgSur);

        opciones.Should().Contain(new OpcionDeOficina(Vieja, "009 · Vieja (inactiva)", false));
        VinculoAgenciaOficina.Opciones(Oficinas, Agencias(sur: Vieja), editando: AgNorte)
            .Should().NotContain(o => o.PublicId == Vieja, "a otra agencia no se le ofrece una oficina inactiva");
    }

    [Fact]
    public void La_columna_dice_si_hay_vinculo_aunque_no_se_vean_las_oficinas()
    {
        VinculoAgenciaOficina.Texto(null, Oficinas, oficinasVisibles: true).Should().Be("Sin oficina");
        VinculoAgenciaOficina.Texto(Centro, Oficinas, oficinasVisibles: true).Should().Be("001 · Centro");
        VinculoAgenciaOficina.Texto(Vieja, Oficinas, oficinasVisibles: true).Should().Be("009 · Vieja (inactiva)");
        VinculoAgenciaOficina.Texto(Guid.NewGuid(), Oficinas, oficinasVisibles: true).Should().Be("Oficina no encontrada");
        VinculoAgenciaOficina.Texto(Centro, [], oficinasVisibles: false).Should().Be("Vinculada");
    }

    [Fact]
    public void Las_agencias_sin_oficina_se_nombran()
    {
        VinculoAgenciaOficina.SinOficina(Agencias()).Should().Equal("Agencia Norte", "Agencia Sur");
        VinculoAgenciaOficina.SinOficina(Agencias(sur: Norte)).Should().Equal("Agencia Norte");
    }
}
