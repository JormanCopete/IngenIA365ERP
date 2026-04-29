using System.Data;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Interfaces
{
    /// <summary>
    /// Interfaz para servicios de exportacion a Excel. Abstrae la dependencia de COM Interop
    /// para permitir implementaciones alternativas (OpenXML, ClosedXML, EPPlus, etc.)
    /// </summary>
    public interface IExcelExportService
    {
        /// <summary>
        /// Exporta el contenido de un DataGridView a un archivo Excel.
        /// </summary>
        /// <param name="dataGridView">Grid con los datos a exportar.</param>
        /// <param name="soloVisibles">Si true, exporta solo columnas visibles.</param>
        void ExportarDataGridView(DataGridView dataGridView, bool soloVisibles = false);

        /// <summary>
        /// Exporta un DataTable a un archivo Excel.
        /// </summary>
        /// <param name="dataTable">Tabla con los datos.</param>
        /// <param name="activarExcel">Si true, abre el archivo en Excel al terminar.</param>
        /// <returns>Ruta del archivo generado.</returns>
        string ExportarDataTable(DataTable dataTable, bool activarExcel = true);

        /// <summary>
        /// Exporta un DataTable a Excel con titulos personalizados por columna.
        /// </summary>
        /// <param name="dataTable">Tabla con los datos.</param>
        /// <param name="titulos">Array de titulos para las columnas.</param>
        /// <param name="activarExcel">Si true, abre el archivo en Excel al terminar.</param>
        /// <returns>Ruta del archivo generado.</returns>
        string ExportarDataTable(DataTable dataTable, string[] titulos, bool activarExcel = true);

        /// <summary>
        /// Abre un archivo de texto delimitado en Excel.
        /// </summary>
        /// <param name="rutaArchivo">Ruta del archivo a abrir.</param>
        void AbrirEnExcel(ref string rutaArchivo);
    }
}
