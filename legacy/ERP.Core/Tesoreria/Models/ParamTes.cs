using System;
using System.Windows.Forms;

namespace ERP.Core.Tesoreria.Models
{
    /// <summary>
    /// Clase para parametros de tesoreria
    /// </summary>
    public class ParamTes
    {
        private ERP.Core.Compartido.Datos.ClsConect _odbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect _varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();

        public ParamTes()
        {
            try
            {
                _odbcConnect.MyOdbcConect(_varini);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        ~ParamTes()
        {
        }
    }
}
