using System.Windows.Forms;

namespace ERP.Core.Compartido.Interfaces
{
    /// <summary>
    /// Interfaz para servicios de reportes. Abstrae la dependencia de Crystal Reports
    /// para permitir implementaciones alternativas (RDLC, FastReport, etc.)
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Configura y muestra el dialogo de opciones del reporte (vista previa, exportar, imprimir).
        /// </summary>
        /// <param name="owner">Formulario padre que origina el reporte.</param>
        /// <param name="reportSource">Objeto del reporte (ReportDocument en Crystal, u otro en implementaciones futuras).</param>
        /// <param name="dialog">Si true, muestra como dialogo modal.</param>
        /// <param name="inmediato">Si true, imprime directamente sin mostrar opciones.</param>
        void MostrarReporte(Form owner, object reportSource, bool dialog = false, bool inmediato = false);

        /// <summary>
        /// Exporta el reporte a un archivo en disco.
        /// </summary>
        /// <param name="reportSource">Objeto del reporte.</param>
        /// <param name="rutaArchivo">Ruta completa del archivo destino.</param>
        /// <param name="formato">Formato de exportacion: "PDF", "Excel", "Word", "Rtf".</param>
        void ExportarADisco(object reportSource, string rutaArchivo, string formato);

        /// <summary>
        /// Muestra la vista previa del reporte en un formulario visor.
        /// </summary>
        /// <param name="owner">Formulario padre.</param>
        /// <param name="reportSource">Objeto del reporte.</param>
        /// <param name="titulo">Titulo de la ventana de vista previa.</param>
        void VistaPrevia(Form owner, object reportSource, string titulo);

        /// <summary>
        /// Carga un reporte desde su nombre (busca el .rpt en la ruta de red configurada).
        /// </summary>
        /// <param name="nombreReporte">Nombre del reporte sin extension.</param>
        /// <param name="reemplazaBD">Si true, reemplaza la conexion BD del reporte con la actual.</param>
        /// <returns>Objeto del reporte cargado.</returns>
        object CargarReporte(string nombreReporte, bool reemplazaBD = true);
    }
}
