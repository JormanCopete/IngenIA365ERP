using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmcptoadicionales : Form
    {
        ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        OdbcConnection myconexion;
        ERP.Core.Compartido.Datos.ClsConect myodbcconect = new ERP.Core.Compartido.Datos.ClsConect();
        ERP.Core.Compartido.Datos.ClsConect.odbcConect varinicial = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();

        public string codigoter;
        public int periodo;
        bool ok;

        public frmcptoadicionales(OdbcConnection myconnect)
        {
            InitializeComponent();
            this.myconexion = myconnect;
            this.myodbcconect.MyOdbcConect(ref this.varinicial);
        }

        private void opcion_ClickEvent(object sender, System.EventArgs e)
        {
            switch (this.opcion.ButtonPressed)
            {
                case 1:
                case 2:
                    this.Close();
                    break;
                case 3:
                    GrabarCptoAdicional();
                    break;
            }
        }

        private bool Validar()
        {
            //ok = this.msgparcop.BuscaAsociado(this.codigoter, this.myconexion);
            if (!ok)
            {
                MessageBox.Show("Codigo de asociado no existe, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtLinea.Focus();
                return false;
            }

            if (!Information.IsNumeric(this.TxtLinea.Text))
            {
                MessageBox.Show("Linea debe ser numerica", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtLinea.Focus();
                return false;
            }
            else
            {
                if (Convert.ToDouble(this.TxtLinea.Text) >= 1000)
                {
                    MessageBox.Show("La linea debe ser menor a 1000", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtLinea.Focus();
                    return false;
                }
                //ok = this.msgparcop.BuscaLinea(this.TxtLinea.Text, this.myconexion);
                if (!ok)
                {
                    MessageBox.Show("Linea no existe, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtLinea.Focus();
                    return false;
                }
            }

            if (!Information.IsNumeric(this.TxtNumero.Text))
            {
                MessageBox.Show("El numero de la obligacion debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtNumero.Focus();
                return false;
            }

            if (!Information.IsNumeric(this.TxtValor.Text))
            {
                MessageBox.Show("El valor de la obligacion debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtValor.Focus();
                return false;
            }

            return true;
        }

        void GrabarCptoAdicional()
        {
            DataSet dsasociado = new DataSet();
            if (Validar())
            {
                //ok = this.msgparcop.BuscaAsociado(this.codigoter, ref dsasociado, this.myconexion);
                if (ok)
                {
                    DataRow row = dsasociado.Tables["tblasociados"].Rows[0];
                    //ok = this.msgcop.GrabaNuevoCredito(this.codigoter, this.TxtLinea.Text, this.TxtNumero.Text, DateTime.Now, DateTime.Now, DateTime.Now, DateTime.Now, row["nit"].ToString(), 1, this.TxtValor.Text, this.TxtValor.Text, 0, this.TxtValor.Text, 0, 5, row["periodo_desto"].ToString(), 1, 1, this.varinicial.pstUsuario, DateTime.Now, row["agencia"].ToString(), row["cencosto"].ToString(), this.periodo, this.myconexion, "", row["CLASE_DESTO"].ToString());
                    if (ok)
                    {
                        this.DialogResult = DialogResult.Yes;
                        this.Close();
                    }
                }
            }
        }

        private void HelpLincred_Click(object sender, System.EventArgs e)
        {
            //this.TxtLinea.Text = this.msgparcop.HelpLIneas(myconexion, this, ERP.Core.CarteraFinanciera.Models.ParamCop.LineasCredito.Conceptos);
            this.TxtLinea.Focus();
        }

        private void TxtLinea_LostFocus(object sender, System.EventArgs e)
        {
            DataSet dslinea = new DataSet();
            if (Information.IsNumeric(this.TxtLinea.Text))
            {
                if (Convert.ToDouble(this.TxtLinea.Text) >= 1000)
                {
                    MessageBox.Show("La linea debe ser menor a 1000", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtLinea.Text = "";
                    this.TxtLinea.Focus();
                }
                else
                {
                    //this.msgparcop.BuscaLinea(this.TxtLinea.Text, ref dslinea, this.myconexion);
                    if (dslinea.Tables["tbllineas"].Rows.Count > 0)
                        this.LblNomLinea.Text = dslinea.Tables["tbllineas"].Rows[0]["descripcion"].ToString();
                    else
                        this.LblNomLinea.Text = "Linea no existe";
                }
            }
        }

        private void TxtValor_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtValor.Text))
                this.TxtValor.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtValor.Text), 2);
            else
                this.TxtValor.Text = "0";
        }

        private void TxtNumero_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtNumero.Text))
            {
                if (Information.IsNumeric(this.TxtLinea.Text))
                {
                    //ok = this.msgcop.BuscaObligacion(this.codigoter, this.TxtLinea.Text, this.TxtNumero.Text, this.myconexion);
                    if (!ok)
                        this.LblExisteCpto.Text = "Obligacion no existe!!";
                }
            }
        }
    }
}
