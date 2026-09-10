using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// <c>AllowedMethodsMask</c> tiene default de base («todos») porque las filas que ya
/// existían se rellenan con él al migrar. Pero un default de base tiene un precio en
/// EF: al insertar, si la propiedad vale lo que EF considera «no asignado», se omite
/// del INSERT y manda la base. Y «no asignado» para un enum es el cero, que aquí es
/// <see cref="MetodosMfa.Ninguno"/>: un valor legítimo cuando la cooperativa no exige
/// segundo factor. Sin sentinel, «no acepta ninguno» se guardaba como «acepta todo»,
/// y Serilog lo avisaba en cada arranque de producción.
///
/// <para>
/// Estas pruebas fijan el modelo; la de ida y vuelta contra PostgreSQL real está en
/// <c>API.IntegrationTests/Identity/MascaraDeMetodosSeGuardaTalCual</c>.
/// </para>
/// </summary>
public class MascaraDeMetodosMfaSinAsignarTests
{
    private const string Cadena = "Host=localhost;Database=x;Username=y;Password=z";

    private static IModel Modelo() =>
        new AdminDbContext(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql(Cadena).Options).Model;

    [Theory]
    [InlineData(typeof(TenantMfaPolicy))]
    [InlineData(typeof(PlatformMfaPolicy))]
    public void La_mascara_tiene_default_de_base_y_un_sentinel_que_no_es_el_cero(Type entidad)
    {
        var propiedad = Modelo().FindEntityType(entidad)!.FindProperty("AllowedMethodsMask")!;

        propiedad.GetDefaultValue().Should().Be(ConversionDeMetodosMfa.Todos,
            "el default de la COLUMNA es lo que deja arrancar a la imagen anterior contra el esquema nuevo");
        propiedad.Sentinel.Should().Be(ConversionDeMetodosMfa.SinAsignar,
            "el cero es Ninguno, un valor legítimo, y no puede ser la marca de «no asignado»");
    }

    [Fact]
    public void El_sentinel_no_coincide_con_ninguna_mascara_posible()
    {
        var valores = Enum.GetValues<MetodosMfa>().Cast<int>().ToList();
        var todasLasCombinaciones = Enumerable.Range(0, 1 << valores.Count(v => v > 0))
            .Select(bits => valores.Where(v => v > 0).Where((v, i) => (bits & (1 << i)) != 0).Aggregate(0, (a, b) => a | b));

        todasLasCombinaciones.Should().NotContain((int)ConversionDeMetodosMfa.SinAsignar);
    }
}
