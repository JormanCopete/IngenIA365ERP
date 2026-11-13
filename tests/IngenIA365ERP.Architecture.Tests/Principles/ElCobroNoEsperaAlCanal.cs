using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 023 (FR-005; research R3, R7): ninguna petición de venta habla con el canal electrónico. El intento lo hacen la cola, el barrido de
/// respaldo y las acciones explícitas de una persona sobre un documento. Fija quién puede enviar <c>EmitElectronicDocumentCommand</c> y que la
/// espera del POS y la confirmación fiscal no lo hagan. (nuevo)
/// </summary>
public class ElCobroNoEsperaAlCanal
{
    /// <summary>
    /// Quienes pueden pedir un intento: el manejador del aviso <c>fe.emitir</c>, el barrido de respaldo, «Reintentar ahora» de la bandeja y los
    /// eventos RADIAN de Compras, que se intentan en orden tras su commit (el 032 espera al 030) y no son parte del cobro (fuera del alcance D4).
    /// </summary>
    private static readonly string[] Permitidos =
    [
        "src/Core/IngenIA365ERP.Application/ElectronicInvoicing/Documents/EmitElectronicDocumentCommand.cs",
        "src/Core/IngenIA365ERP.Application/ElectronicInvoicing/Documents/ManejadoresDeAvisoFe.cs",
        "src/Core/IngenIA365ERP.Application/ElectronicInvoicing/Documents/EmitRadianEventCommand.cs",
        "src/Infrastructure/IngenIA365ERP.ElectronicInvoicing/Processor/ProcesadorDeDocumentosElectronicos.cs",
        "src/Presentation/IngenIA365ERP.API/Endpoints/ElectronicInvoicing/DocumentsEndpoints.cs",
    ];

    [Fact]
    public void Solo_la_cola_el_barrido_y_las_acciones_explicitas_piden_un_intento()
    {
        var root = RepoPath.FindRepoRoot();
        var pide = new Regex(@"new\s+EmitElectronicDocumentCommand\s*\(", RegexOptions.Compiled);
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => pide.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(r => !Permitidos.Contains(r))
            .ToList();

        Assert.True(infractores.Count == 0, "Una petición de venta no intenta la emisión (feature 023, FR-005):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void La_espera_del_POS_y_la_confirmacion_fiscal_no_hablan_con_el_canal()
    {
        var root = RepoPath.FindRepoRoot();
        var archivos = new[]
        {
            "src/Core/IngenIA365ERP.Application/Inventory/Pos/EsperaEnLineaDelPos.cs",
            "src/Core/IngenIA365ERP.Application/Inventory/Pos/CheckoutPosDraftCommand.cs",
            "src/Core/IngenIA365ERP.Application/Inventory/Integration/EmisionFiscalDeLaConfirmacion.cs",
        };
        var prohibido = new Regex(@"\b(ISender|ICanalesDeEmision|ICanalDeEmisionElectronica|QueryElectronicDocumentStatusCommand|GeneracionDeRepresentacion|EntregaAlComprador\s*\.\s*EntregarAsync|GuardadoDeArtefactos)\b",
            RegexOptions.Compiled);
        var infractores = archivos
            .Select(a => (a, Fuente: FuenteSinComentarios.Leer(Path.Combine(root, a))))
            .Where(x => prohibido.IsMatch(x.Fuente))
            .Select(x => $"{x.a}: {prohibido.Match(x.Fuente).Value}")
            .ToList();

        Assert.True(infractores.Count == 0, "El cobro sólo publica y lee la fila (feature 023, D7):\n  " + string.Join("\n  ", infractores));
    }
}
