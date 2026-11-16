using FluentAssertions;
using IngenIA365ERP.Shared.Components.Contabilidad;
using IngenIA365ERP.Shared.Services.Contabilidad;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Contabilidad;

/// <summary>
/// 2026-11-16 (pedido del dueño): el filtro por columna de un comprobante consultado. Texto: contiene, sin mayúsculas ni tildes.
/// Importes: un número o «>», «<», «>=», «<=», «=» con el número, en formato colombiano o con punto decimal.
/// </summary>
public class ConsultaDeLineasTests
{
    private static readonly LineaDeComprobanteDto L = new(3, Guid.NewGuid(), "27100501", "Salarios por pagar", Guid.NewGuid(), "PRINCIPAL",
        null, "Almacén", Guid.NewGuid(), "José Pérez", "1061", null, null, 0m, 1234.5m, "Quincena", null);

    private static ConsultaDeLineas.Columna Col(string clave) => ConsultaDeLineas.Columnas.Single(c => c.Clave == clave);

    [Theory]
    [InlineData("cuenta", "2710")]
    [InlineData("cuenta", "SALARIOS")]
    [InlineData("tercero", "jose perez")]
    [InlineData("centro", "almacen")]
    [InlineData("credito", ">1000")]
    [InlineData("credito", ">=1.234,5")]
    [InlineData("credito", "=1234.5")]
    [InlineData("credito", "1.234")]
    [InlineData("debito", "=0")]
    public void Cumple(string columna, string filtro) => ConsultaDeLineas.Cumple(Col(columna), L, filtro).Should().BeTrue();

    [Theory]
    [InlineData("cuenta", "5105")]
    [InlineData("credito", "<1000")]
    [InlineData("debito", ">0")]
    [InlineData("tercero", "maria")]
    public void No_cumple(string columna, string filtro) => ConsultaDeLineas.Cumple(Col(columna), L, filtro).Should().BeFalse();
}
