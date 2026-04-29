using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmrecaudo : Form
    {
        private ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private string stmysql;
        private int DiasMora;
        private DateTime Fecini;

        public frmrecaudo()
        {
            InitializeComponent();
        }

        private void frmbase_Load(object sender, EventArgs e)
        {
            int Alto = 0, ancho = 0;
            Alto = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height;
            ancho = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width;

            this.Top = (int)Math.Round(Alto * (49.0 / 100), 0);
            this.Left = (int)Math.Round(ancho * (55.7 / 100), 0);
            initializeCampos();
        }

        private void initializeCampos()
        {
            this.TxtCodigoBarras.Text = null;
            this.TxtPagar.Text = "0";
            this.Txtvalor.Text = "0";
            this.TxtFactura.Text = "0";
        }

        private void TxtCodigoBarras_LostFocus(object sender, EventArgs e)
        {
            if (this.TxtCodigoBarras.Text.Trim() != "")
            {
                string txtFacturaText = this.TxtFactura.Text;
                string txtCedulaText = this.TxtCedula.Text;
                string txtvalorText = this.Txtvalor.Text;
                string txtFechaText = this.TxtFecha.Text;
                //this.msgcop.LeeCodigobarras(this.TxtCodigoBarras.Text, ref txtFacturaText, ref txtCedulaText, ref txtvalorText, ref txtFechaText);
                this.TxtFactura.Text = txtFacturaText;
                this.TxtCedula.Text = txtCedulaText;
                this.Txtvalor.Text = txtvalorText;
                this.TxtFecha.Text = txtFechaText;
                if (this.TxtCedula.Text != "")
                {
                    this.TxtPagar.Text = this.Txtvalor.Text;
                    DateTime FechaFactura = new DateTime(Convert.ToInt32(Strings.Mid(this.TxtFecha.Text, 1, 4)), Convert.ToInt32(Strings.Mid(this.TxtFecha.Text, 5, 2)), Convert.ToInt32(Strings.Mid(this.TxtFecha.Text, 7, 2)));

                    Fecini = DateTime.Parse(DateTime.Now.ToString("yyyy/MM/dd"));
                    DiasMora = (int)DateAndTime.DateDiff(DateInterval.Day, Fecini, FechaFactura);
                    if (DiasMora < 0)
                    {
                        this.LblMora.Text = (DiasMora * -1).ToString();
                    }
                    else
                    {
                        this.LblMora.Text = "0";
                    }
                    string txtPagarText = this.TxtPagar.Text;
                    this.FormateaNumero(ref txtPagarText);
                    this.TxtPagar.Text = txtPagarText;
                    string txtvalorText2 = this.Txtvalor.Text;
                    this.FormateaNumero(ref txtvalorText2);
                    this.Txtvalor.Text = txtvalorText2;
                }
            }
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
            Salir();
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
    }
}
