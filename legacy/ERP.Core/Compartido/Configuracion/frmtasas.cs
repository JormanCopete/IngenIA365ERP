using ERP.Core.CDT.Models;
using System;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Compartido.Configuracion
{
    public partial class frmtasas : Form
    {
        private ParamCdt msgparcdt = new ParamCdt();
        public OdbcConnection myconexion = new OdbcConnection();
        public int LineaCdat;

        public frmtasas()
        {
            InitializeComponent();
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnGrabar_Click(object sender, EventArgs e)
        {
            if (!ValidaNumericos())
            {
                MessageBox.Show("Operacion no se puede completar, existen registros no numericos, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                msgparcdt.EliminarTasasPorPlazos(LineaCdat, myconexion);
                for (int fila = 0; fila < DgvTasas.RowCount - 1; fila++)
                {
                    DataGridViewRow row = DgvTasas.Rows[fila];
                    msgparcdt.GrabarTasasporPlazos(LineaCdat,
                        Convert.ToDouble(row.Cells["ClmVlrIni"].Value),
                        Convert.ToDouble(row.Cells["ClmVlrFinal"].Value),
                        Convert.ToInt32(row.Cells["ClmPlazoIni"].Value),
                        Convert.ToInt32(row.Cells["ClmPlazoFin"].Value),
                        Convert.ToDecimal(row.Cells["ClmTasa"].Value),
                        myconexion);
                }
                MessageBox.Show("Registro se realizo exitosamente", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool ValidaNumericos()
        {
            for (int fila = 0; fila < DgvTasas.RowCount - 1; fila++)
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
                if (!Information.IsNumeric(row.Cells["ClmTasa"].Value))
                    return false;
            }
            return true;
        }

        private void BorrarRegistroToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                int indice = DgvTasas.CurrentCell.RowIndex;
                if (DgvTasas.RowCount - 1 >= 0)
                {
                    if (MessageBox.Show("Esta Seguro de Eliminar", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        if (msgparcdt.EliminarTasasPorPlazosRegistro(LineaCdat,
                            Convert.ToDouble(DgvTasas.Rows[indice].Cells[0].Value),
                            Convert.ToDouble(DgvTasas.Rows[indice].Cells[1].Value),
                            Convert.ToInt32(DgvTasas.Rows[indice].Cells[2].Value),
                            Convert.ToInt32(DgvTasas.Rows[indice].Cells[3].Value),
                            myconexion))
                        {
                            MessageBox.Show("Se ha eliminado con Exito", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            DgvTasas.Rows.RemoveAt(indice);
                        }
                    }
                }
            }
            catch
            {
                MessageBox.Show("No tiene una fila seleccionada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
