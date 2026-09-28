using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Reports.Vistas;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

/// <summary>
/// Feature 012, I4, T751 (api.md §27, decisiones-transversales §2.12): la vista <c>dian-documents</c> del centro de informes de Inventario.
/// Columnas en el orden del contrato —Tipo, Prefijo, Número, Fecha, Contraparte, Total, Estado, Contingencia, Código único, Intentos, Plazo,
/// Canal, Último mensaje— y la oculta <c>_documento</c>; filtros comunes <c>from</c>/<c>to</c> sobre la fecha de expedición y los propios
/// <c>status</c>, <c>kind</c> y <c>contingency</c>. Trae la contraparte, así que se declara con columna de datos personales.
/// </summary>
public sealed class InformeDeDocumentosDianTests
{
    private readonly EscenarioDeEmision _s = new();

    private DianDocumentsReportQueryHandler Handler() => new(_s.E.Db, [], _s.E.Reloj);

    private static FiltrosDeInformeDeInventario Diciembre() => new() { From = new DateOnly(2026, 12, 1), To = new DateOnly(2026, 12, 31) };

    [Fact]
    public void La_vista_declara_sus_filtros_y_la_columna_de_datos_personales()
    {
        var vista = DianDocumentsReportQueryHandler.Vista;

        vista.Key.Should().Be("dian-documents");
        vista.OwnFilters.Should().BeEquivalentTo("status", "kind", "contingency");
        vista.PersonalDataColumn.Should().Be("Contraparte");
        DianDocumentsReportQueryHandler.Columnas.Select(c => c.Clave ?? c.Nombre).Should().Equal(
            "Tipo", "Prefijo", "Número", "Fecha", "Contraparte", "Total", "Estado", "Contingencia", "Código único", "Intentos", "Plazo", "Canal",
            "Último mensaje", "_documento");
    }

    [Fact]
    public async Task Una_fila_por_documento_con_su_estado_y_la_clave_oculta()
    {
        var d = await _s.FacturaAsync(990000401);
        d.Status = ElectronicDocumentStatus.Validated;
        d.UniqueCode = "cufe-123";
        d.AttemptCount = 2;
        await _s.E.Db.SaveChangesAsync();

        var r = await Handler().Handle(new DianDocumentsReportQuery(Diciembre()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        var fila = r.Value.Filas.Should().ContainSingle().Subject.Valores;
        fila[0].Should().Be("Factura electrónica de venta");
        fila[1].Should().Be("SETP");
        fila[2].Should().Be("SETP990000401");
        fila[3].Should().Be(new DateOnly(2026, 12, 5));
        fila[5].Should().Be(d.TotalAmount);
        fila[6].Should().Be("Validado");
        fila[7].Should().BeNull();
        fila[8].Should().Be("cufe-123");
        fila[9].Should().Be(2);
        fila[11].Should().Be("SIMULADO");
        fila[13].Should().Be(d.PublicId.ToString());
    }

    [Fact]
    public async Task Filtra_por_estado_tipo_contingencia_y_fechas()
    {
        var validado = await _s.FacturaAsync(990000411);
        validado.Status = ElectronicDocumentStatus.Validated;
        var enContingencia = await _s.FacturaAsync(990000412);
        enContingencia.ContingencyType = ContingencyType.Dian04;
        enContingencia.Status = ElectronicDocumentStatus.DianContingency;
        var deNoviembre = await _s.FacturaAsync(990000413);
        deNoviembre.IssueDate = new DateOnly(2026, 11, 3);
        await _s.E.Db.SaveChangesAsync();

        (await Handler().Handle(new DianDocumentsReportQuery(Diciembre(), Status: ElectronicDocumentStatus.Validated), default))
            .Value.Filas.Should().ContainSingle().Which.Valores[13].Should().Be(validado.PublicId.ToString());
        (await Handler().Handle(new DianDocumentsReportQuery(Diciembre(), Contingency: true), default))
            .Value.Filas.Should().ContainSingle().Which.Valores[7].Should().Be("Tipo 04 (DIAN)");
        (await Handler().Handle(new DianDocumentsReportQuery(Diciembre(), Kind: ElectronicDocumentKind.CreditNote), default))
            .Value.Filas.Should().BeEmpty();
        (await Handler().Handle(new DianDocumentsReportQuery(Diciembre()), default)).Value.Filas.Should().HaveCount(2);
    }

    [Fact]
    public async Task Un_rango_invertido_es_un_error_de_rango()
    {
        var r = await Handler().Handle(
            new DianDocumentsReportQuery(new FiltrosDeInformeDeInventario { From = new DateOnly(2026, 12, 31), To = new DateOnly(2026, 12, 1) }), default);

        r.Error.Code.Should().Be(FiltrosDeInformeDeInventario.RangoInvalidoCodigo);
    }
}
