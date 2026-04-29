using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Reportes.Services
{
    /// <summary>
    /// Clase para impresion de extractos de cuenta
    /// </summary>
    public class ClsMscext01
    {
        #region Campos privados

        private ERP.Core.Compartido.Reportes.config_report _msgsas = new ERP.Core.Compartido.Reportes.config_report();
        private ERP.Core.Compartido.Utilidades.Clsmsgsas _msgsasview = new ERP.Core.Compartido.Utilidades.Clsmsgsas();

        #endregion

        #region Metodos publicos

        /// <summary>
        /// Imprime el estado de cuenta de un asociado
        /// </summary>
        /// <param name="codigoter">Codigo del tercero</param>
        /// <param name="periodo">Periodo a imprimir</param>
        /// <param name="detalle">Detalle del extracto</param>
        /// <param name="myforma">Formulario padre</param>
        /// <param name="usuario">Usuario del sistema (opcional)</param>
        public void ImprimeEstadoCta(string codigoter, string periodo, string detalle, Form myforma, string usuario = " ")
        {
            ERP.Core.Compartido.Reportes.reporte rep = new ERP.Core.Compartido.Reportes.reporte("extracto01");
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            rep.SetParameterValue("codigo_asociado", codigoter);
            rep.SetParameterValue("periodo", periodo);
            rep.SetParameterValue("detalle", detalle);

            try
            {
                rep.SetParameterValue("usuario", usuario);
            }
            catch
            {
                // Ignorar error si el parametro usuario no existe
            }

            _msgsas.confi_reportes(myforma, rep);
        }

        #endregion
    }
}
