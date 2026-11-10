using FluentAssertions;
using Xunit;
using IngenIA365ERP.Shared.Services.Tablero;

namespace IngenIA365ERP.Shared.Tests.Tablero;

/// <summary>2026-11-10: las opciones de los accesos directos se leen del menú incrustado; si el menú cambia de forma, esto avisa.</summary>
public class CatalogoDeOpcionesTests
{
    [Fact]
    public void Lee_las_opciones_del_menu_con_su_grupo()
    {
        CatalogoDeOpciones.Opciones.Should().HaveCountGreaterThan(200);
        CatalogoDeOpciones.Buscar("/recaudo/caja").Should().NotBeNull();
        CatalogoDeOpciones.Opciones.Should().OnlyHaveUniqueItems(o => o.Ruta);
        CatalogoDeOpciones.Opciones.Should().NotContain(o => o.Ruta == "/");
        CatalogoDeOpciones.Opciones.Should().OnlyContain(o => o.Nombre.Length > 0 && !o.Nombre.Contains('<') && !o.Nombre.Contains('@'));
    }

    [Fact]
    public void Las_opciones_dentro_de_una_puerta_llevan_sus_permisos()
    {
        CatalogoDeOpciones.Opciones.Should().Contain(o => o.Permisos.Count > 0);
        CatalogoDeOpciones.Opciones.Should().Contain(o => o.Grupo == "Contabilidad");
    }
}
