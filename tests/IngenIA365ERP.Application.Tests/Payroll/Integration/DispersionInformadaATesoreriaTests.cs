using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Payroll;
using IngenIA365ERP.Application.Treasury.Activation;
using IngenIA365ERP.Application.Treasury.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Tests.Payroll.Integration;

/// <summary>
/// Feature 015, T2, T0445 (E-05; FR-168): Nómina es el módulo de origen <c>NOM</c> y emite <c>DispersionDeNominaGenerada</c> v1 a Tesorería —un solo destino, por la
/// clave <c>Confirmation:{archivo:N}</c>, sin admitir proceso— al generar su archivo; Tesorería la acepta y la adopta como destino. Nómina sigue pagando con su archivo
/// (D27). Prueba escrita, sin correr. (nuevo)
/// </summary>
public class DispersionInformadaATesoreriaTests
{
    [Fact]
    public void El_mensaje_es_de_Nomina_a_Tesoreria_y_esta_en_el_catalogo()
    {
        var tipo = CatalogoDeMensajes.Buscar(DispersionDeNominaGeneradaV1.Type, 1);

        tipo.Should().NotBeNull();
        tipo!.OriginModule.Should().Be(ModulosDeOrigen.Nomina);
        tipo.Destinos.Should().BeEquivalentTo([IntegrationDestinations.Treasury]);
        tipo.AdmiteProceso.Should().BeFalse();
        ModulosDeOrigen.Todos.Should().Contain("NOM");
    }

    [Fact]
    public void Tesoreria_la_acepta_y_la_adopta()
    {
        DestinoTesoreria.Aceptados.Should().Contain((DispersionDeNominaGeneradaV1.Type, 1));
        ActivateTreasuryCommandHandler.VersionesDelDestino.Should().Contain(v => v.Contrato == DispersionDeNominaGeneradaV1.Type && v.Version == 1);
    }
}
