using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Tests.Core;

public class NombreDePersonaTests
{
    [Fact]
    public void Junta_las_cuatro_partes_nombres_primero()
    {
        NombreDePersona.Completo("WILLIAN", "ANDRÉS", "LAGOS", "PÉREZ").Should().Be("WILLIAN ANDRÉS LAGOS PÉREZ");
        NombreDePersona.ApellidosYNombres("WILLIAN", "ANDRÉS", "LAGOS", "PÉREZ").Should().Be("LAGOS PÉREZ WILLIAN ANDRÉS");
    }

    [Fact]
    public void Omite_las_partes_vacias_y_recorta()
    {
        NombreDePersona.Completo("WILLIAN", null, " LAGOS ", "  ").Should().Be("WILLIAN LAGOS");
        NombreDePersona.Completo(null, null, null, null).Should().BeEmpty();
    }
}
