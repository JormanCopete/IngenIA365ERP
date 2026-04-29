using ERP.Core.CarteraFinanciera.Models;
using System;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmtasasintcred : Form
    {
        public OdbcConnection myconexion = new OdbcConnection();
        public int Linea;
        private ParamCop Paramcop = new ParamCop();

        public frmtasasintcred()
        {
            InitializeComponent();
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private bool ValidaNumericos()
        {
            for (int fila = 0; fila <= DgvTasas.RowCount - 2; fila++)
            {
                DataGridViewRow row = DgvTasas.Rows[fila];

                if (!Information.IsNumeric(row.Cells["ClmVlrIni"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["ClmVlrFinal"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["ClmPlazoIni"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["ClmPlazoFin"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["clmantinicial"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["clmantfinal"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["ClmTasa"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["PlazoMaximo"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["MontoMaximo"].Value))
                    return false;

                if (!Information.IsNumeric(row.Cells["ColumnGarantia"].Value))
                    return false;

                int garantia = Convert.ToInt32(row.Cells["ColumnGarantia"].Value);
                if (garantia < 0 || garantia > 3)
                {
                    MessageBox.Show("La Garantia debe estar en un rango entre 0 y el 3", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }
            return true;
        }

        private void BtnGrabar_Click(object sender, EventArgs e)
        {
            if (!ValidaNumericos())
            {
                MessageBox.Show("Operacion no se puede completar, existen registros no numericos, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                //Paramcop.EliminarTasasPorPlazos(Linea, myconexion);
                for (int fila = 0; fila <= DgvTasas.RowCount - 2; fila++)
                {
                    DataGridViewRow row = DgvTasas.Rows[fila];
                    string garantiaVal = row.Cells["ColumnGarantia"].Value?.ToString() ?? "";
                    string garantia = garantiaVal.Length > 0 ? garantiaVal.Substring(0, 1) : "0";

                    //Paramcop.GrabarTasasporPlazos(
                    //    Linea,
                    //    row.Cells["ClmVlrIni"].Value,
                    //    row.Cells["ClmVlrFinal"].Value,
                    //    row.Cells["ClmPlazoIni"].Value,
                    //    row.Cells["ClmPlazoFin"].Value,
                    //    row.Cells["clmantinicial"].Value,
                    //    row.Cells["clmantfinal"].Value,
                    //    row.Cells["ClmTasa"].Value,
                    //    myconexion,
                    //    row.Cells["PlazoMaximo"].Value,
                    //    row.Cells["MontoMaximo"].Value,
                    //    garantia);
                }
                MessageBox.Show("Registro se realizo exitosamente", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DgvTasas_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // Suppress data errors
        }
    }
}
