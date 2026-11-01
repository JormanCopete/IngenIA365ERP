using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// FR-035 (feature 009): el alcance de sucursal se le dice a la persona. Sin asignaciones no hay restricción; con oficinas que
/// ninguna agencia tiene vinculadas el alcance queda vacío y la consulta lo marca, para que la pantalla explique por qué los
/// informes salen vacíos. (nuevo)
/// </summary>
public class MiAlcanceDeSucursalQueryTests
{
    private static async Task<MiAlcanceDeSucursalDto> Consultar(AlcanceDeSucursales alcance, params Branch[] sucursales)
    {
        var db = TestDbContextFactory.Create();
        db.Branches.AddRange(sucursales);
        await db.SaveChangesAsync();
        var scope = Substitute.For<IUserBranchScope>();
        scope.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(alcance);
        return await new MiAlcanceDeSucursalQueryHandler(db, scope).Handle(new MiAlcanceDeSucursalQuery(), CancellationToken.None);
    }

    [Fact]
    public async Task Sin_asignaciones_no_hay_restriccion()
    {
        var r = await Consultar(AlcanceDeSucursales.SinRestriccion, new Branch { Name = "Principal" });

        r.Restringido.Should().BeFalse();
        r.SinSucursales.Should().BeFalse();
    }

    [Fact]
    public async Task Con_oficinas_sin_agencia_vinculada_queda_sin_sucursales()
    {
        var r = await Consultar(AlcanceDeSucursales.Limitado([], null), new Branch { Name = "Principal" });

        r.Restringido.Should().BeTrue();
        r.SinSucursales.Should().BeTrue();
    }

    [Fact]
    public async Task Con_agencias_vinculadas_las_nombra()
    {
        var norte = new Branch { Id = 7, Name = "Norte" };
        var centro = new Branch { Id = 3, Name = "Centro" };
        var r = await Consultar(AlcanceDeSucursales.Limitado([7, 3], 3), norte, centro, new Branch { Id = 9, Name = "Sur" });

        r.SinSucursales.Should().BeFalse();
        r.Sucursales.Should().Equal("Centro", "Norte");
    }
}
