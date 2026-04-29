using System;
using ERP.Core.Inventario.Services;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Forms
{
    public partial class frmcantbodega : Form
    {
        private ERP.Core.Compartido.Configuracion.ParamSys MsgSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private msginv msginv = new msginv();
        public DataTable dsconsolidado = new DataTable();
        public DataTable dsdatos = new DataTable();

        private void frmcantbodega_Load(object sender, EventArgs e)
        {
            this.CenterToParent();
            // MsgSys.ConfiguraForma(this, this.empresappl.Text); // ERROR: CS1501
            this.DgvCantidades.AutoGenerateColumns = false;
            CargaInformacion();
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }

        private void CargaInformacion()
        {
            if (Information.IsNumeric(this.LblIdProducto.Text))
            {
                if (dsconsolidado.Rows.Count != 0)
                {
                    DataRow row = dsconsolidado.Rows[0];
                    this.LblNomProducto.Text = row["descripcion"].ToString();
                    this.LblCostoConsolidado.Text = Strings.FormatNumber(Convert.ToDouble(row["costoprom"]), 4);
                    this.LblCantinicial.Text = Strings.FormatNumber(Convert.ToDouble(row["CantInicial"]), 2);
                    this.LblCantCompra.Text = Strings.FormatNumber(Convert.ToDouble(row["CantCompra"]), 2);
                    this.LblCantVentas.Text = Strings.FormatNumber(Convert.ToDouble(row["Cantvendida"]), 2);
                    this.LblDisponible.Text = Strings.FormatNumber(Convert.ToDouble(row["CantFinal"]), 2);
                }

                if (dsdatos.Rows.Count != 0)
                {
                    this.DgvCantidades.DataSource = dsdatos;
                }
            }
        }
    }
}
