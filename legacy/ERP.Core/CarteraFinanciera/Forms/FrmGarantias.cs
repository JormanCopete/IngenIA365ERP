using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class FrmGarantias : Form
    {
        private ERP.Core.CarteraFinanciera.Models.ParamCop parancop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Utilidades.Ayuda Ayu = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        public string codigoter, usuario;
        public int lincred, numero, plazo, numcdat, periodo;
        public double avacatas, avalcomer, porcentaje, saldo;
        private string tipogara = " ", tieneseguro = " ", estadogaran = " ";
        private bool ok;
        internal System.Windows.Forms.DataGridView DatGriCuopen;
        internal System.Windows.Forms.DataGridView DgvTotales;
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private System.Data.Odbc.OdbcConnection MyConnect = new System.Data.Odbc.OdbcConnection();

        public FrmGarantias(System.Data.Odbc.OdbcConnection Conect)
        {
            InitializeComponent();
            this.MyConnect = Conect;
        }

        private void FrmGarantias_Load(object sender, EventArgs e)
        {
            limpiar();
            msgcop.OdbcConnect.LlenarVarini(ref msgcop.varini);
            FechaHora.Start();
            this.TxtUsuario.Text = usuario;
            //Ayu.ConfiguraForma(this.Owner, this.LblEmpresa.Text, this.TxtServidor.Text, this.TxtDb.Text, this.TxtNameform.Text);
            //ok = msgcop.BuscarGarantia(codigoter, lincred, numero, MyConnect, this.TxtMatricula.Text, tipogara, this.TxtDescripcion.Text, avacatas, avalcomer,
            //    tieneseguro, this.TxtPoliza.Text, this.DtpFechainicial.Value, this.DtpFechafinal.Value, this.DtpFechavencimiento.Value, this.TxtNitaseguradora.Text, this.TxtRazonsocial.Text,
            //    estadogaran, "", porcentaje, "", "", "", "", plazo, saldo, numcdat);
            if (ok == false)
            {
                MessageBox.Show("Esta obligacion no tiene Garantia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }
            TieneSeguroEstado(tieneseguro);
            TipoDeGerantia(tipogara);
            FormateaValores();
        }

        private void limpiar()
        {
            this.TxtAvaluocatastral.Clear();
            this.TxtAvaluocomercial.Clear();
            this.TxtClaseGarantia.Clear();
            this.TxtDescripcion.Text = " ";
            this.TxtMatricula.Text = " ";
            this.TxtNitaseguradora.Text = " ";
            this.TxtNumerocdat.Clear();
            this.TxtPoliza.Text = " ";
            this.TxtPorcentaje.Clear();
            this.TxtRazonsocial.Text = " ";
            this.TxtNumerocdat.Visible = false;
            this.Label16.Visible = false;
        }

        private void TieneSeguroEstado(string tiene)
        {
            switch (tiene)
            {
                case "N":
                    this.GbxAsegurado.Enabled = false;
                    this.GbxAsegurado.Text = "No Esta Asegurado";
                    break;
                case "Y":
                    this.GbxAsegurado.Enabled = true;
                    break;
            }
        }

        private void FormateaValores()
        {
            this.TxtAvaluocatastral.Text = Strings.FormatNumber(avacatas, 2);
            this.TxtAvaluocomercial.Text = Strings.FormatNumber(avalcomer, 2);
            this.TxtPorcentaje.Text = Strings.FormatNumber(porcentaje, 2);
            this.TxtNumerocdat.Text = numcdat.ToString();
        }

        private void TipoDeGerantia(string tipo)
        {
            int tipoInt = 0;
            int.TryParse(tipo, out tipoInt);
            switch (tipoInt)
            {
                case 1: this.TxtClaseGarantia.Text = "Personal"; break;
                case 2: this.TxtClaseGarantia.Text = "Hipoteca"; break;
                case 3: this.TxtClaseGarantia.Text = "Prendaria"; break;
                case 4: this.TxtClaseGarantia.Text = "Aportes"; break;
                case 5: this.TxtClaseGarantia.Text = "Avales"; break;
                case 6: this.TxtClaseGarantia.Text = "Fiduciaria"; break;
                case 7: this.TxtClaseGarantia.Text = "Pignoracion"; break;
                case 8: this.TxtClaseGarantia.Text = "Rentas en dacion"; break;
                case 9: this.TxtClaseGarantia.Text = "Otras admidbles"; break;
                case 10:
                    this.TxtClaseGarantia.Text = "Cdats";
                    this.TxtNumerocdat.Visible = true;
                    this.Label16.Visible = true;
                    break;
                case 11: this.TxtClaseGarantia.Text = "Sin garantia"; break;
                case 12: this.TxtClaseGarantia.Text = "Carta Tripartita"; break;
            }
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void FechaHora_Tick(object sender, EventArgs e)
        {
            if (this.TxtFechahora.Tag != null && this.TxtFechahora.Tag.ToString() == "200")
            {
                this.TxtFechahora.Text = DateTime.Now.ToString();
            }
        }

        private void BtnImpimir_Click(object sender, EventArgs e)
        {
            ERP.Core.Compartido.Reportes.reporte rep = new ERP.Core.Compartido.Reportes.reporte("cop_rinfgarantia");
            ERP.Core.Compartido.Reportes.config_report mSAS = new ERP.Core.Compartido.Reportes.config_report();
            string codAsoIni, codasofin;
            int LineaIni, LineaFin, TipoIni, TipoFin;
            string tipovecmto = "";
            string Todas = "Todas";
            this.BtnImpimir.Enabled = false;

            codAsoIni = codigoter;
            codasofin = codigoter;
            if (Todas == "Todas")
            {
                LineaIni = 1000;
                LineaFin = 9998;
            }
            else
            {
                LineaIni = lincred;
                LineaFin = lincred;
            }
            if (Todas == "Todas")
            {
                TipoIni = 1;
                TipoFin = 12;
            }
            else
            {
                int.TryParse(tipogara, out TipoIni);
                TipoFin = TipoIni;
            }

            tipovecmto = "N";
            rep.SetParameterValue("empresa", msgcop.varini.pstEmpresa);
            rep.SetParameterValue("nit", msgcop.varini.stnit);
            rep.SetParameterValue("direccion", msgcop.varini.stdircompania);
            rep.SetParameterValue("telefono", msgcop.varini.sttelcompania);
            rep.SetParameterValue("codAsoIni", codAsoIni);
            rep.SetParameterValue("codAsoFin", codasofin);
            rep.SetParameterValue("LineaIni", LineaIni);
            rep.SetParameterValue("LineaFin", LineaFin);
            rep.SetParameterValue("TipoIni", TipoIni);
            rep.SetParameterValue("TipoFin", TipoFin);
            rep.SetParameterValue("TipoVemcto", tipovecmto);
            rep.SetParameterValue("FecIni", this.DtpFechainicial.Value);
            rep.SetParameterValue("FecFinal", this.DtpFechafinal.Value);
            rep.SetParameterValue("periodo", periodo);
            mSAS.confi_reportes(this, rep);
            this.BtnImpimir.Enabled = true;
        }
    }
}
