using ERP.Core.CarteraFinanciera.Services.Creditos;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class cop_deduciones : Form
    {
        bool ok;
        OdbcConnection mycon = new OdbcConnection();
        ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        ClsLiqcreditos msgliqcre = new ClsLiqcreditos();
        ERP.Core.CarteraFinanciera.Models.ParamCop parcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        public DataSet dsdatos = new DataSet();
        public string StNumSolicitud;

        public cop_deduciones(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void frmbase01_Load(object sender, System.EventArgs e)
        {
            this.CenterToScreen();

            msgparsys.ConfiguraForma(this, this.LblNomEmpresa.Text, this.txtServer.Text, this.TxtBd.Text, this.Name, msgparsys.varini.pstUsuario, this.TxtfechaSys.Text);
            if (Convert.ToDouble(this.StNumSolicitud) < 0)
            {
                CargaDatos();
            }
            else
            {
                CargaDatos1();
            }
        }

        private void opcion_ClickEvent(object sender, System.EventArgs e)
        {
            switch (opcion.ButtonPressed)
            {
                case 2:
                    this.Close();
                    this.Dispose();
                    break;
            }
        }

        private void opcion_Load(object sender, System.EventArgs e)
        {
        }

        private void CargaDatos()
        {
            DataRow row = dsdatos.Tables["TbldatosCredito"].Rows[0];
            this.lblCodigoter.Text = row["cedula"].ToString();
            this.LblNombre.Text = row["Nomasociado"].ToString();
            this.LblvalorCredito.Text = Strings.FormatNumber(row["valorCredito"], 2);
            this.LblDescuentos.Text = Strings.FormatNumber(row["valmenos"], 2);
            this.LblVlrGirar.Text = Strings.FormatNumber(Convert.ToDouble(row["valorCredito"]) - Convert.ToDouble(row["valmenos"]), 2);
        }

        private void CargaDatos1()
        {
            double ValorDes = ValorDeducciones();
            DataRow row = dsdatos.Tables["TbldatosCredito"].Rows[0];
            this.lblCodigoter.Text = row["codigoter"].ToString();
            string _nomAsociado = this.LblNombre.Text;
            string _cod = this.lblCodigoter.Text;
            System.Data.DataSet _dsBusca = new System.Data.DataSet();
            parcop.BuscaAsociado(ref _cod, ref _dsBusca, mycon);
            if (_dsBusca.Tables["tblasociados"] != null && _dsBusca.Tables["tblasociados"].Rows.Count > 0)
                this.LblNombre.Text = _dsBusca.Tables["tblasociados"].Rows[0]["apellido"].ToString() + " " + _dsBusca.Tables["tblasociados"].Rows[0]["nombre"].ToString();
            else
                this.LblNombre.Text = _nomAsociado;
            this.LblvalorCredito.Text = Strings.FormatNumber(row["vlr_solicitud"], 2);
            this.LblDescuentos.Text = Strings.FormatNumber(ValorDes, 2);
            this.LblVlrGirar.Text = Strings.FormatNumber(Convert.ToDouble(row["vlr_solicitud"]) - ValorDes, 2);
        }

        double ValorDeducciones()
        {
            double ValorDes = 0;
            for (int i = 0; i <= dsdatos.Tables["tbldeducciones"].Rows.Count - 1; i++)
            {
                DataRow row = dsdatos.Tables["tbldeducciones"].Rows[i];
                ValorDes += (Convert.ToDouble(row[4]) + Convert.ToDouble(row[5]));
            }
            return ValorDes;
        }
    }
}
