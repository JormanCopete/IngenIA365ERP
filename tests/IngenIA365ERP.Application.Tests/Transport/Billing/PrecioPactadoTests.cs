using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;

namespace IngenIA365ERP.Application.Tests.Transport.Billing;

/// <summary>
/// Feature 020 (T097; R9): el precio pactado de una suscripción no se puede mandar desde una pantalla ni una ruta: no se lee del JSON. Sólo
/// lo pone la facturación de servicios en el servidor, y entonces la línea lo toma como su precio de lista (sin descuento ni aprobación).
/// </summary>
public class PrecioPactadoTests
{
    [Fact]
    public void El_precio_pactado_no_entra_por_el_JSON()
    {
        const string json = """{"productPublicId":"7f2c1a10-0000-0000-0000-000000000001","unitPublicId":"7f2c1a10-0000-0000-0000-000000000002","quantity":1,"agreedPrice":1}""";

        var linea = JsonSerializer.Deserialize<SaveInventoryDraftLine>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        linea!.AgreedPrice.Should().BeNull();
        linea.Quantity.Should().Be(1m);
    }

    [Fact]
    public void El_proceso_si_lo_puede_poner()
    {
        var linea = new SaveInventoryDraftLine(null, Guid.NewGuid(), Guid.NewGuid(), 1m) { AgreedPrice = 45_000m };

        linea.AgreedPrice.Should().Be(45_000m);
        JsonSerializer.Serialize(linea).Should().NotContain("AgreedPrice");
    }
}
