using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data.Odbc;
using System.Windows.Forms;
using System.ComponentModel;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmsiplainusuales : Form
    {
        private string _Codigoter;
        private ERP.Core.Compartido.Configuracion.ParamSys msgparasys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        public bool ok;
        public string obervaciones;
        private string _linea;
        private string _numero;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Codigoter
        {
            get { return _Codigoter; }
            set { _Codigoter = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string linea
        {
            get { return _linea; }
            set { _linea = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string numero
        {
            get { return _numero; }
            set { _numero = value; }
        }

        public void AbrirConexion()
        {
            if (this.myconnect.State != System.Data.ConnectionState.Open)
            {
                this.myconnect.Open();
            }
        }

        private void frmsiplainusuales_Load(object sender, EventArgs e)
        {
            this.KeyPreview = true;
            string nomaso = "  ";
            string apeaso = "  ";
            this.lblLinea.Text = this.linea;
            this.lblNumero.Text = this.numero;
            //msgparasys.DesbloquearContenidoObjeto(this.GroupBox1);
            //ok = msgparcop.BuscaAsociado(Codigoter, myconnect, ref nomaso, ref apeaso);
            switch (ok)
            {
                case true:
                    this.lblCodigoter.Text = nomaso + "  " + apeaso;
                    break;
            }
        }

        private void frmsiplainusuales_KeyDown(object sender, KeyEventArgs e)
        {
            //msgparasys.ejecutarFocusFormulario(sender, e, this);
        }

        private void btnAceptar_Click(object sender, EventArgs e)
        {
            ok = true;
            if (this.txtObservaciones.Text.Trim() == "")
            {
                MessageBox.Show("Por  Favor Ingrese Una Observacion");
                return;
            }
            obervaciones = this.txtObservaciones.Text;
            this.Close();
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            ok = false;
            this.Close();
        }
    }
}
