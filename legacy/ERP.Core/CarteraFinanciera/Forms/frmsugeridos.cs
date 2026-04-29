using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmsugeridos : Form
    {
        ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        OdbcConnection myconexion;
        DataSet dsdata = new DataSet();
        bool ok;

        public frmsugeridos(OdbcConnection myconnect)
        {
            InitializeComponent();
            this.myconexion = myconnect;
        }

        private void RbtMonto_CheckedChanged(object sender, System.EventArgs e)
        {
            this.LblLabel.Visible = false;
            if (this.RbtMonto.Checked)
            {
                this.LblResultado.Text = "Monto Sugerido";
                this.LblSugerido.Text = "Plazo Solicitado";
            }
            else
            {
                this.LblResultado.Text = "Plazo Sugerido";
                this.LblSugerido.Text = "Monto Solicitado";
                this.LblLabel.Visible = true;
            }
        }

        bool Validar()
        {
            if (this.CbxClaCuota.Text.Trim() == "")
            {
                MessageBox.Show("Debe seleccionar una clase de cuota", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxClaCuota.Focus();
                return false;
            }
            if (this.CbxPeriodicidad.Text.Trim() == "")
            {
                MessageBox.Show("Debe seleccionar la periodicidad", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxPeriodicidad.Focus();
                return false;
            }
            return true;
        }

        void CalcularSugerido()
        {
            if (Validar())
            {
                if (this.RbtMonto.Checked)
                    CalcularMontoSugerido();
                else
                    CalcularPlazoSugerido();
            }
        }

        void CalcularMontoSugerido()
        {
            double cuota = 0;
            try
            {
                cuota = Convert.ToDouble(this.TxtCuota.Text) * Convert.ToDouble(this.CbxPeriodicidad.SelectedIndex);
                switch (this.CbxClaCuota.SelectedIndex.ToString())
                {
                    case "1":
                        this.LblValorSugerido.Text = Strings.FormatNumber(Convert.ToDouble(Math.Round(Financial.PV((Convert.ToDouble(this.TxtTasaInt.Text) / 100), Convert.ToDouble(this.TxtSugerido.Text), (cuota * -1)))), 0);
                        break;
                    case "2":
                        this.LblValorSugerido.Text = Strings.FormatNumber(cuota * Convert.ToDouble(this.TxtSugerido.Text), 0);
                        break;
                }
            }
            catch (Exception)
            {
                this.LblValorSugerido.Text = "0";
            }
        }

        void CalcularPlazoSugerido()
        {
            double cuota = 0;
            try
            {
                cuota = Convert.ToDouble(this.TxtCuota.Text) * Convert.ToDouble(this.CbxPeriodicidad.SelectedIndex);
                switch (this.CbxClaCuota.SelectedIndex.ToString())
                {
                    case "1":
                        this.LblValorSugerido.Text = Strings.FormatNumber(Convert.ToDouble(Math.Round(Financial.NPer((Convert.ToDouble(this.TxtTasaInt.Text) / 100), cuota, (Convert.ToDouble(this.TxtSugerido.Text) * -1)))), 0);
                        break;
                    case "2":
                        if (cuota > 0)
                            this.LblValorSugerido.Text = Strings.FormatNumber(Convert.ToDouble(Math.Round(Convert.ToDouble(this.TxtSugerido.Text) / cuota, 0)), 0);
                        else
                            this.LblValorSugerido.Text = "0";
                        break;
                }
            }
            catch (Exception)
            {
                this.LblValorSugerido.Text = "0";
            }
        }

        private void opcion_ClickEvent(object sender, System.EventArgs e)
        {
            this.Close();
        }

        private void TxtCuota_GotFocus(object sender, System.EventArgs e)
        {
            this.LblDetalleCampos.Text = "Valor a pagar segun la periodicidad del prestamo";
        }

        private void TxtCuota_LostFocus(object sender, System.EventArgs e)
        {
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtCuota.Text))
                this.TxtCuota.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtCuota.Text), 0);
            else
                this.TxtCuota.Text = "0";
            CalcularSugerido();
        }

        private void TxtSugerido_GotFocus(object sender, System.EventArgs e)
        {
            if (this.RbtMonto.Checked)
                this.LblDetalleCampos.Text = "Plazo solicitado en meses, para el pago del prestamo";
            else
                this.LblDetalleCampos.Text = "Monto solicitado";
        }

        private void TxtSugerido_LostFocus(object sender, System.EventArgs e)
        {
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtSugerido.Text))
                this.TxtSugerido.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtSugerido.Text), 0);
            else
                this.TxtSugerido.Text = "0";
            CalcularSugerido();
        }

        private void TxtTasaInt_GotFocus(object sender, System.EventArgs e)
        {
            this.LblDetalleCampos.Text = "Tasa de interes mensual";
        }

        private void TxtTasaInt_LostFocus(object sender, System.EventArgs e)
        {
            if (!Microsoft.VisualBasic.Information.IsNumeric(this.TxtTasaInt.Text))
                this.TxtTasaInt.Text = "0";
            CalcularSugerido();
        }

        private void CbxClaCuota_LostFocus(object sender, System.EventArgs e)
        {
            switch (this.CbxClaCuota.SelectedIndex)
            {
                case 1:
                    this.TxtTasaInt.Enabled = true;
                    break;
                case 2:
                    this.TxtTasaInt.Enabled = false;
                    break;
            }
        }

        private void CbxPeriodicidad_GotFocus(object sender, System.EventArgs e)
        {
            this.LblDetalleCampos.Text = "Periodicidad de pago de la cuota del prestamo";
        }
    }
}
