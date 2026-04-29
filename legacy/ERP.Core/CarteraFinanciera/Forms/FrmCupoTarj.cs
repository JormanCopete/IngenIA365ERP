using ERP.Core.CarteraFinanciera.Services.Debitos;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class FrmCupoTarj : Form
    {
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.CarteraFinanciera.Models.ParamCop ParamCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ClsMsgDeb msgdeb = new ClsMsgDeb();

        public string NumeroTajeta;

        public FrmCupoTarj(OdbcConnection conexion)
        {
            // Llamada necesaria para el Disenador de Windows Forms.
            InitializeComponent();
            this.mycon = conexion;
            // Agregue cualquier inicializacion despues de la llamada a InitializeComponent().
        }

        private void FrmCupoTarj_Load(object sender, EventArgs e)
        {
            this.LblAsociado.Text = " ";

            string strDummy = " ";
            //ParamCop.BuscaAsociado(this.LblCodigoter.Text, this.mycon, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref strDummy);
            this.LblAsociado.Text = strDummy;
            CargarDatos();
            this.CenterToScreen();
        }

        private void CargarDatos()
        {
            DataSet dsdata = new DataSet();
            int i = 0;

            dsdata = this.msgdeb.CargarGrillaCupoTarjetas(this.LblCodigoter.Text, Convert.ToInt32(this.LblPeriodo.Text), this.mycon);
            while (i < dsdata.Tables["TblCupoTarjetas"].Rows.Count)
            {
                DataRow row = dsdata.Tables["TblCupoTarjetas"].Rows[i];
                if (row["error"] == DBNull.Value || row["error"].ToString().Trim() == "")
                {
                    row["Error"] = "Activa";
                }
                else
                {
                    row["Error"] = "Bloqueada";
                }
                i = i + 1;
            }
            this.DgwTarjetas.AutoGenerateColumns = false;
            this.DgwTarjetas.DataSource = dsdata.Tables["TblCupoTarjetas"];
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void DgwTarjetas_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void DgwTarjetas_DoubleClick(object sender, EventArgs e)
        {
            if (DgwTarjetas.SelectedRows.Count > 0)
            {
                NumeroTajeta = DgwTarjetas.SelectedRows[0].Cells[0].Value.ToString();
                this.Close();
            }
        }
    }
}
