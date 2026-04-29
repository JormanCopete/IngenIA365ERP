using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmrectarj01 : Form
    {
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Services.Debitos.ClsMsgDeb msgdeb = new ERP.Core.CarteraFinanciera.Services.Debitos.ClsMsgDeb();
        private ERP.Core.CarteraFinanciera.Services.TarjetaCred.Clstarjcredito msgcre = new ERP.Core.CarteraFinanciera.Services.TarjetaCred.Clstarjcredito();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private DataSet dsDatosTarjeta = new DataSet();
        private string stmysql;
        private bool ok;
        private string Debcre = "D";
        private string agencia = "9999";

        public frmrectarj01(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void frmbase_Load(object sender, EventArgs e)
        {
            int Alto = 0, ancho = 0;

            Alto = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height;
            ancho = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width;

            this.Top = (int)Math.Round(Alto * (49.0 / 100), 0);
            this.Left = (int)Math.Round(ancho * (55.7 / 100), 0);
            //this.msgdeb.BuscaConvenio(36, dsDatosTarjeta, this.mycon);

            initializeCampos();
        }

        private void initializeCampos()
        {
            this.TxtPagar.Text = "0";
            this.TxtvalPendiente.Text = "0";
            this.TxtpendAvances.Text = "0";
        }

        private void TxtCodigoBarras_LostFocus(object sender, EventArgs e)
        {
        }

        private void TxtCodigoBarras_TextChanged(object sender, EventArgs e)
        {
        }

        private void frmrecaudo_LostFocus(object sender, EventArgs e)
        {
        }

        private void CmbSalir_Click(object sender, EventArgs e)
        {
            Salir();
        }

        private void Salir()
        {
            this.Close();
        }

        private void CmbGuardar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FormateaNumero(ref string valor)
        {
            valor = Strings.FormatNumber(valor, 0);
        }

        private void TxtPagar_LostFocus(object sender, EventArgs e)
        {
            string txtPagarText = this.TxtPagar.Text;
            FormateaNumero(ref txtPagarText);
            this.TxtPagar.Text = txtPagarText;
        }

        private void TxtPagar_TextChanged(object sender, EventArgs e)
        {
        }

        private void TxtNumtarjeta_TextChanged(object sender, EventArgs e)
        {
        }

        private void Txtperiodo_LostFocus(object sender, EventArgs e)
        {
        }

        private void FormateaNumero(ref TextBox txtNumero)
        {
            txtNumero.Text = Strings.FormatNumber(txtNumero.Text, 0);
        }

        private void Txtperiodo_TextChanged(object sender, EventArgs e)
        {
        }

        private void HelpAsociado_Click(object sender, EventArgs e)
        {
            //this.TxtCodigoter.Text = this.msgparcop.HelpAsociado(this.mycon, this);
            this.TxtCodigoter.Focus();
        }

        private void BtnTarjetas_Click(object sender, EventArgs e)
        {
            this.msgdeb.Helptarjetas(this.TxtCodigoter.Text, " ", this);
        }

        private void TxtCiclo_LostFocus(object sender, EventArgs e)
        {
            if (this.Txtperiodo.Text != "")
            {
                //this.msgparcop.BuscaAsociado(this.TxtCodigoter.Text, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref agencia);
                this.msgcre.BuscaParametros(agencia, "0036", this.dsDatosTarjeta, this.mycon);

                string txtvalPendienteText = this.TxtvalPendiente.Text;
                //this.msgcop.BuscaSaldosCuotasPendientes(this.TxtCodigoter.Text,
                //    Convert.ToString(this.dsDatosTarjeta.Tables["tblparcre"].Rows[0]["lincred"]),
                //    0,
                //    Convert.ToString(this.dsDatosTarjeta.Tables["tblparcre"].Rows[0]["lincred"]),
                //    999999999,
                //    this.Txtperiodo.Text,
                    //this.TxtCiclo.Text,
                    //this.mycon,
                    //ref txtvalPendienteText);
                this.TxtvalPendiente.Text = txtvalPendienteText;

                string txtpendAvancesText = this.TxtpendAvances.Text;
                //this.msgcop.BuscaSaldosCuotasPendientes(this.TxtCodigoter.Text,
                //    Convert.ToString(this.dsDatosTarjeta.Tables["tblparcre"].Rows[0]["LincredAvance"]),
                //    0,
                //    Convert.ToString(this.dsDatosTarjeta.Tables["tblparcre"].Rows[0]["LincredAvance"]),
                //    999999999,
                //    this.Txtperiodo.Text,
                //    this.TxtCiclo.Text,
                //    this.mycon,
                //    ref txtpendAvancesText);
                this.TxtpendAvances.Text = txtpendAvancesText;

                this.TxtPagar.Text = (Convert.ToDouble(this.TxtpendAvances.Text) + Convert.ToDouble(this.TxtvalPendiente.Text)).ToString();

                string valPend = this.TxtvalPendiente.Text;
                FormateaNumero(ref valPend);
                this.TxtvalPendiente.Text = valPend;

                string pendAv = this.TxtpendAvances.Text;
                FormateaNumero(ref pendAv);
                this.TxtpendAvances.Text = pendAv;

                string pagar = this.TxtPagar.Text;
                FormateaNumero(ref pagar);
                this.TxtPagar.Text = pagar;
            }
        }

        private void TxtCiclo_TextChanged(object sender, EventArgs e)
        {
        }
    }
}
