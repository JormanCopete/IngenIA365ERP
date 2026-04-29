using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class FrmSolicitudesUsu : Form
    {
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.CarteraFinanciera.Models.ParamCop parCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private DataSet DatDatos = new DataSet();
        private DateTime fecfin;

        public FrmSolicitudesUsu(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void FrmGestionCob_Load(object sender, EventArgs e)
        {
            int diafin = 30;
            if (this.TxtCodigoter.Text.Trim() != "")
            {
                diafin = DateTime.DaysInMonth(Convert.ToInt32(Strings.Mid(this.LblPeriodo.Text, 1, 4)), Convert.ToInt32(Strings.Mid(this.LblPeriodo.Text, 5)));
                this.DtpFechaInicial.Value = new DateTime(Convert.ToInt32(Strings.Mid(this.LblPeriodo.Text, 1, 4)), Convert.ToInt32(Strings.Mid(this.LblPeriodo.Text, 5)), 1);
                this.DtpFechaFinal.Value = new DateTime(Convert.ToInt32(Strings.Mid(this.LblPeriodo.Text, 1, 4)), Convert.ToInt32(Strings.Mid(this.LblPeriodo.Text, 5)), diafin);
                fecfin = this.DtpFechaFinal.Value;
                cargagrillaAsociados();
            }
        }

        private void cargagrillaAsociados()
        {
            this.DatDatos = parCop.BuscarSolicitudes(this.TxtCodigoter.Text, mycon, this.DtpFechaInicial.Value, this.DtpFechaFinal.Value, this.RbtCredito.Checked);
            this.DgwGrilla.DataSource = DatDatos.Tables["TblSolicitudes"];
            switch (this.RbtCredito.Checked)
            {
                case false:
                    this.DgwGrilla.Columns["Plazo"].Width = 100;
                    this.DgwGrilla.Columns["Plazo"].HeaderText = "Beneficiario";
                    this.DgwGrilla.Columns["clmPagador"].Visible = false;
                    this.DgwGrilla.Columns["clmente"].Visible = true;
                    this.DgwGrilla.Columns["clmnombenef"].Visible = true;
                    break;
                case true:
                    this.DgwGrilla.Columns["Plazo"].Width = 60;
                    this.DgwGrilla.Columns["Plazo"].HeaderText = "Plazo";
                    this.DgwGrilla.Columns["clmente"].Visible = false;
                    this.DgwGrilla.Columns["clmPagador"].Visible = true;
                    this.DgwGrilla.Columns["clmnombenef"].Visible = false;
                    break;
            }
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnActualizar_Click(object sender, EventArgs e)
        {
            cargagrillaAsociados();
        }

        private void RbtCredito_CheckedChanged(object sender, EventArgs e)
        {
            if (this.TxtCodigoter.Text.Trim() != "")
            {
                cargagrillaAsociados();
            }
        }
    }
}
