using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.Application.Tests.Accounting.Documents;

/// <summary>
/// 2026-11-16 (pedido del dueño): todo comprobante que se imprime también se baja a Excel, con las columnas de la pantalla, la cuenta y
/// el tercero partidos en código y nombre, una fila por línea en su orden y los totales aparte (no entran al filtro de Excel).
/// </summary>
public class ExportarComprobanteTests
{
    private static LineaDeComprobanteDto Linea(int n, string cuenta, decimal debito, decimal credito, string? tercero = null) =>
        new(n, Guid.NewGuid(), cuenta, "Cuenta " + cuenta, Guid.NewGuid(), "PRINCIPAL", null, null,
            tercero is null ? null : Guid.NewGuid(), tercero, tercero is null ? null : "123", null, null, debito, credito, "detalle " + n, null);

    private static ComprobanteDto Comprobante(params LineaDeComprobanteDto[] lineas) =>
        new(Guid.NewGuid(), "NM", "Nómina", 7, new DateOnly(2026, 11, 15), "Nómina 01-15/11", "Posted", "Regular",
            lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit), "ana", new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc), "luis",
            new DateTime(2026, 11, 16, 16, 0, 0, DateTimeKind.Utc), new OrigenDeComprobanteDto("NOM", "Nómina", "PayrollRun", Guid.NewGuid(), null),
            new ReversionDto(null, null, null, null, null), lineas, [], []);

    [Fact]
    public void Una_fila_por_linea_en_su_orden_con_totales_aparte()
    {
        var t = ExportDocumentQueryHandler.ComoTabla(Comprobante(Linea(2, "27100501", 0, 900, "PEREZ"), Linea(1, "51050601", 900, 0)));

        t.Titulo.Should().Be("Comprobante NM-7");
        t.Subtitulo.Should().Contain("Contabilizado").And.Contain("15/11/2026");
        t.Columnas.Select(c => c.Nombre).Should().StartWith(["Línea", "Cuenta", "Nombre de la cuenta", "Identificación del tercero", "Tercero"]);
        t.Columnas.Single(c => c.Nombre == "Débito").Tipo.Should().Be(TipoDeColumna.Moneda);
        t.Filas.Select(f => f.Valores[0]).Should().Equal(1, 2);
        t.Filas[1].Valores[1].Should().Be("27100501");
        t.Filas[1].Valores[4].Should().Be("PEREZ");
        t.Totales!.Valores[9].Should().Be(900m);
        t.Totales.Valores[10].Should().Be(900m);
        t.Notas.Should().Contain(n => n.Contains("contabilizó luis"));
    }
}
