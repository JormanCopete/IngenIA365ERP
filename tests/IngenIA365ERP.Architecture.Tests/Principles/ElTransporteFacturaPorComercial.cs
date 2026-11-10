using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 020 (T120; R10; Principio III): Transporte factura sólo por los comandos de Comercial (<c>SaveInventoryDraftCommand</c>,
/// <c>ConfirmInventoryDocumentCommand</c>) y lee sus facturas por <c>FacturasDeServicios</c>. Ningún archivo de <c>Application/Transport</c>
/// arma documentos, líneas o pagos de inventario ni toca sus tablas: si lo hiciera, se saltaría la numeración, los impuestos, la factura
/// electrónica y los mensajes a Cartera y Contabilidad.
/// </summary>
public class ElTransporteFacturaPorComercial
{
    private static readonly string[] Prohibidos =
    [
        "new InventoryDocument(", "new InventoryDocument {", "new InventoryDocumentLine", "new DocumentPayment(", "new DocumentPayment {",
        ".InventoryDocuments", ".InventoryDocumentLines", ".DocumentPayments", ".DocumentTaxLines", "EmisorDeMensajes.EmitirAsync(new SolicitudDeEmision(new OrigenDeEmision(MessageOriginKind.Document",
    ];

    private static string Carpeta => Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Transport");

    public static TheoryData<string> LosArchivos =>
        [.. (Directory.Exists(Carpeta) ? Directory.EnumerateFiles(Carpeta, "*.cs", SearchOption.AllDirectories) : [])
            .Select(f => Path.GetRelativePath(Carpeta, f)).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(LosArchivos))]
    public void Transporte_no_toca_los_documentos_de_Comercial(string archivo)
    {
        var fuente = FuenteSinComentarios.Leer(Path.Combine(Carpeta, archivo));
        var encontrados = Prohibidos.Where(p => fuente.Contains(p, StringComparison.Ordinal)).ToList();
        Assert.True(encontrados.Count == 0,
            $"{archivo} toca los documentos de Comercial ({string.Join(", ", encontrados)}): facture por SaveInventoryDraftCommand y ConfirmInventoryDocumentCommand.");
    }
}
