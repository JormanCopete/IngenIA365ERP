using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Nomina.Forms
{
    public partial class nom_frmayuausen : Form
    {
        public double NumConsec = 0;

        private void CmbSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void DgwAusentismo_DoubleClick(object sender, EventArgs e)
        {
            if (this.DgwAusentismo.RowCount > 0)
            {
                if (!Information.IsNumeric(this.DgwAusentismo.CurrentRow.Cells["clmconsecutivo"].Value))
                {
                    NumConsec = 0;
                }
                else
                {
                    NumConsec = Convert.ToDouble(this.DgwAusentismo.CurrentRow.Cells["clmconsecutivo"].Value);
                    this.Close();
                }
            }
        }
    }
}
