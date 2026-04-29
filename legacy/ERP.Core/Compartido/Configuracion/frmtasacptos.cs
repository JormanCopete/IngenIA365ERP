using ERP.Core.CarteraFinanciera.Models;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Compartido.Configuracion
{
    public partial class frmtasacptos : Form
    {
        public OdbcConnection myconexion = new OdbcConnection();
        private ParamCop msgconfig = new ParamCop();
        private bool ok;

        public frmtasacptos()
        {
            InitializeComponent();
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }

        private void CargarDatos()
        {
            // DataSet dsdatos = msgconfig.BuscarTasasporPlazosCptos(LblLinea.Text, myconexion); // ERROR: CS1061
            DgvTasas.AutoGenerateColumns = false;
            // if (dsdatos.Tables.Contains("tbltasas")) // ERROR: CS0103
                // DgvTasas.DataSource = dsdatos.Tables["tbltasas"]; // ERROR: CS0103
        }

        private bool Validar(bool todos)
        {
            if (!Information.IsNumeric(LblLinea.Text))
            {
                MessageBox.Show("No se escogio una linea de credito", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!Information.IsNumeric(LblConcepto.Text))
            {
                MessageBox.Show("No se escogio un concepto para el descuento", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!Information.IsNumeric(TxtPLazoInicial.Text))
            {
                MessageBox.Show("Plazo inicial debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtPLazoInicial.Focus();
                return false;
            }

            if (!Information.IsNumeric(TxtPlazoFInal.Text))
            {
                MessageBox.Show("Plazo final debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtPlazoFInal.Focus();
                return false;
            }

            if (todos)
            {
                if (!Information.IsNumeric(TxtValor.Text))
                {
                    MessageBox.Show("Valor debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtValor.Focus();
                    return false;
                }

                if (CbxTipoDscto.Text.Trim() == "")
                {
                    MessageBox.Show("Debe escoger una opcion en tipo descuento", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    CbxTipoDscto.Focus();
                    return false;
                }

                if (Convert.ToDouble(TxtValor.Text) <= 0)
                {
                    MessageBox.Show("Valor debe ser mayor a cero", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtValor.Focus();
                    return false;
                }
            }

            return true;
        }

        private void BtnAgregar_Click(object sender, EventArgs e)
        {
            if (Validar(true))
            {
                // msgconfig.GrabarTasasporPlazosCptos(LblLinea.Text, TxtPLazoInicial.Text, TxtPlazoFInal.Text, CbxTipoDscto.SelectedIndex, TxtValor.Text, myconexion); // ERROR: CS1061
                CargarDatos();
            }
        }

        private void BtnQuitar_Click(object sender, EventArgs e)
        {
            if (Validar(false))
            {
                // msgconfig.EliminarTasasPorPlazosCptos(LblLinea.Text, TxtPLazoInicial.Text, TxtPlazoFInal.Text, myconexion); // ERROR: CS1061
                CargarDatos();
            }
        }
    }
}
