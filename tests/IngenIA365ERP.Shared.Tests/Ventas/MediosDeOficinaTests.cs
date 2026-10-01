using FluentAssertions;
using IngenIA365ERP.Shared.Services.Core;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T642 (FR-056, FR-096): los medios del panel de cobro en una venta de oficina salen del catálogo de Core: sólo activos y vigentes a la
/// fecha, en su orden; la tarjeta lleva los datáfonos activos de su adquirente y el crédito sus valores por defecto. (nuevo)
/// </summary>
public class MediosDeOficinaTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 27);

    private static MedioDePagoDto Medio(string code, int clase, short orden = 1, bool activo = true, DateOnly? hasta = null, Guid? adquirente = null,
        CreditoPorDefectoDto? credito = null) =>
        new(Guid.NewGuid(), code, code, orden, null, clase, null, null, adquirente, null, null, null, null, false, null, null, null, clase == 1, true, false, 1, false, 0m,
            null, null, "10", credito, true, true, true, activo, new DateOnly(2026, 1, 1), hasta, null);

    [Fact]
    public void Solo_los_activos_y_vigentes_en_su_orden()
    {
        var medios = MediosDeOficina.Desde(
        [
            Medio("TRF", 7, orden: 3), Medio("EFE", 1, orden: 1), Medio("VIEJO", 1, hasta: Hoy.AddDays(-1)), Medio("INACT", 1, activo: false),
        ], [], Hoy);

        medios.Select(m => m.Code).Should().Equal("EFE", "TRF");
    }

    [Fact]
    public void La_tarjeta_lleva_los_datafonos_activos_de_su_adquirente()
    {
        var credibanco = Guid.NewGuid();
        var otro = Guid.NewGuid();
        var datafonos = new[]
        {
            new DatafonoDto(Guid.NewGuid(), "D1", credibanco, "CRB", null, null, true),
            new DatafonoDto(Guid.NewGuid(), "D2", otro, "RBM", null, null, true),
            new DatafonoDto(Guid.NewGuid(), "D3", credibanco, "CRB", null, null, false),
        };

        var visa = MediosDeOficina.Desde([Medio("VISA", TextosDeVentas.MedioTarjetaCredito, adquirente: credibanco)], datafonos, Hoy).Single();

        visa.CardTerminals.Select(d => d.Code).Should().Equal("D1");
    }

    [Fact]
    public void El_credito_propone_sus_cuotas_plazo_y_periodicidad()
    {
        var credito = MediosDeOficina.Desde([Medio("CRA", TextosDeVentas.MedioCreditoAsociado, credito: new CreditoPorDefectoDto(90, 180, 6, 3, 30, "L01"))], [], Hoy).Single();

        credito.CreditDefaults.Should().Be(new CreditoPropuestoDto(90, 3, 30, "L01"));
    }
}
