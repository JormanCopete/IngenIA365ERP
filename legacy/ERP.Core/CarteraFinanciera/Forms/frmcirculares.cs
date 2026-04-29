using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmcirculares : Form
    {
        public OdbcConnection connect;
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera clscartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Services.clscircular clscircular = new ERP.Core.CarteraFinanciera.Services.clscircular();

        public frmcirculares()
        {
            InitializeComponent();
        }

        private void frmcirculares_Load(object sender, EventArgs e)
        {
            this.LblNomAsociado.Text = " ";
            string lblNomText = this.LblNomAsociado.Text;
            //this.clscartera.BuscaAsociado(this.TxtCodigoter.Text, connect, ref lblNomText);
            this.LblNomAsociado.Text = lblNomText;
            CargarGrillaMaestro();
        }

        private void CargarGrillaMaestro()
        {
            DataSet dsdata = new DataSet();
            switch (Valida())
            {
                case true:
                    //dsdata = this.clscartera.CargarGrillaMaestroCirculares(this.TxtCodigoter.Text, this.TxtPeriodoIni.Text, this.TxtPeriodoFin.Text, this.connect);
                    this.DgwMaestro.AutoGenerateColumns = false;
                    this.DgwMaestro.DataSource = dsdata.Tables["tblmaecircular"];
                    break;
            }
        }

        private void BtnActualizar_Click(object sender, EventArgs e)
        {
            CargarGrillaMaestro();
        }

        private bool Valida()
        {
            switch (this.TxtCodigoter.Text.Trim())
            {
                case "":
                    MessageBox.Show("El campo codigo de asociado no puede estar vacio", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }
            switch (Information.IsNumeric(this.TxtPeriodoIni.Text))
            {
                case false:
                    MessageBox.Show("El periodo inicial debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtPeriodoIni.Text = "";
                    this.TxtPeriodoIni.Focus();
                    return false;
                case true:
                    switch (this.TxtPeriodoIni.Text.Length != 6)
                    {
                        case true:
                            MessageBox.Show("Error en el formato del periodo, debe ser aaaaMM", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            this.TxtPeriodoIni.Text = "";
                            this.TxtPeriodoIni.Focus();
                            return false;
                    }
                    break;
            }
            switch (Information.IsNumeric(this.TxtPeriodoFin.Text))
            {
                case false:
                    MessageBox.Show("El periodo final debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtPeriodoFin.Text = "";
                    this.TxtPeriodoFin.Focus();
                    return false;
                case true:
                    switch (this.TxtPeriodoFin.Text.Length != 6)
                    {
                        case true:
                            MessageBox.Show("Error en el formato del periodo, debe ser aaaaMM", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            this.TxtPeriodoFin.Text = "";
                            this.TxtPeriodoFin.Focus();
                            return false;
                    }
                    break;
            }
            return true;
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        public void ImprimirCircular(string numaviso, int periodo, string clasecpto, string leyarrastre, string codeudores)
        {
            //clscircular.ImprimirCirculares(numaviso, periodo, clasecpto, "T", leyarrastre, "T", "Todas", "Todas", "Todos", "", codeudores, this.TxtCodigoter.Text, this.TxtCodigoter.Text, this, this.connect);
        }

        private void VerCircularDeudorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            switch (this.DgwMaestro.CurrentRow.Selected)
            {
                case true:
                    this.ImprimirCircular(
                        Convert.ToString(this.DgwMaestro.CurrentRow.Cells["clmaviso"].Value),
                        Convert.ToInt32(this.DgwMaestro.CurrentRow.Cells["clmperiodo"].Value),
                        Convert.ToString(this.DgwMaestro.CurrentRow.Cells["clmclasecpto"].Value),
                        Convert.ToString(this.DgwMaestro.CurrentRow.Cells["clmleyarrastre"].Value),
                        "N");
                    break;
            }
        }

        private void VerCircularDeudorYCodeudoresToolStripMenuItem_Click(object sender, EventArgs e)
        {
            switch (this.DgwMaestro.CurrentRow.Selected)
            {
                case true:
                    this.ImprimirCircular(
                        Convert.ToString(this.DgwMaestro.CurrentRow.Cells["clmaviso"].Value),
                        Convert.ToInt32(this.DgwMaestro.CurrentRow.Cells["clmperiodo"].Value),
                        Convert.ToString(this.DgwMaestro.CurrentRow.Cells["clmclasecpto"].Value),
                        Convert.ToString(this.DgwMaestro.CurrentRow.Cells["clmleyarrastre"].Value),
                        "S");
                    break;
            }
        }

        private void DgwMaestro_MouseUp(object sender, MouseEventArgs e)
        {
            MenuGrilla.Visible = false;
            if (this.DgwMaestro.RowCount != 0)
            {
                switch (e.Button)
                {
                    case MouseButtons.Right:
                        if (this.DgwMaestro.CurrentRow != null)
                        {
                            MenuGrilla.Visible = true;
                            MenuGrilla.Show(DgwMaestro, new Point(e.X, e.Y));
                        }
                        break;
                }
            }
        }
    }
}
