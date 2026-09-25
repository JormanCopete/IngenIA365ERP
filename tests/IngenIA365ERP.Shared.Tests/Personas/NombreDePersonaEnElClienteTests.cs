using FluentAssertions;
using IngenIA365ERP.Shared.Components.Shared;
using IngenIA365ERP.Shared.Models.Personas;
using IngenIA365ERP.Shared.Services.Core;
using IngenIA365ERP.Shared.Services.Nomina;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Personas;

/// <summary>
/// Desde la feature 010 (D-06) el nombre tiene cuatro partes. Hasta el 2026-09-25 el cliente lo
/// armaba con «primer nombre + primer apellido», así que «WILLIAN ANDRÉS LAGOS PÉREZ» salía en
/// buscadores, títulos y la ficha del empleado como «WILLIAN LAGOS».
/// </summary>
public class NombreDePersonaEnElClienteTests
{
    [Fact]
    public void El_nombre_completo_lleva_las_cuatro_partes_nombres_primero()
    {
        NombreDePersona.Completo("WILLIAN", "ANDRÉS", "LAGOS", "PÉREZ").Should().Be("WILLIAN ANDRÉS LAGOS PÉREZ");
        NombreDePersona.ApellidosYNombres("WILLIAN", "ANDRÉS", "LAGOS", "PÉREZ").Should().Be("LAGOS PÉREZ WILLIAN ANDRÉS");
    }

    [Fact]
    public void Las_partes_vacias_no_dejan_espacios_de_mas()
    {
        NombreDePersona.Completo(" ANA ", null, "PÉREZ", "  ").Should().Be("ANA PÉREZ");
        NombreDePersona.Completo(null, null, null, null).Should().BeEmpty();
    }

    [Fact]
    public void La_persona_natural_se_ve_con_su_nombre_completo_y_la_juridica_por_su_razon_social()
    {
        var natural = new PersonaDto { FirstName = "WILLIAN", OtherNames = "ANDRÉS", LastName = "LAGOS", SecondLastName = "PÉREZ" };
        var juridica = new PersonaDto { FirstName = "", LastName = "", OtherNames = "X", BusinessName = "Cooperativa El Roble" };

        natural.NombreVisible.Should().Be("WILLIAN ANDRÉS LAGOS PÉREZ");
        PersonSearchPicker.PersonSearchItem.Desde(natural).FullName.Should().Be("WILLIAN ANDRÉS LAGOS PÉREZ");
        juridica.NombreVisible.Should().Be("Cooperativa El Roble");
    }

    [Fact]
    public void El_formulario_de_persona_titula_con_el_nombre_completo()
    {
        var modelo = new PersonaFormularioModelo { FirstName = "WILLIAN", OtherNames = "ANDRÉS", LastName = "LAGOS", SecondLastName = "PÉREZ" };

        modelo.NombreVisible.Should().Be("WILLIAN ANDRÉS LAGOS PÉREZ");
    }

    [Fact]
    public void La_ficha_del_empleado_prefiere_el_nombre_que_compone_el_servidor_y_si_no_lo_arma()
    {
        var sinCompuesto = new FichaEmpleadoDto { FirstName = "WILLIAN", OtherNames = "ANDRÉS", LastName = "LAGOS", SecondLastName = "PÉREZ" };
        var conCompuesto = new FichaEmpleadoDto { FirstName = "WILLIAN", LastName = "LAGOS", FullName = "WILLIAN ANDRÉS LAGOS PÉREZ" };

        sinCompuesto.NombreCompleto.Should().Be("WILLIAN ANDRÉS LAGOS PÉREZ");
        sinCompuesto.Nombres.Should().Be("WILLIAN ANDRÉS");
        sinCompuesto.Apellidos.Should().Be("LAGOS PÉREZ");
        conCompuesto.NombreCompleto.Should().Be("WILLIAN ANDRÉS LAGOS PÉREZ");
    }
}
