using ERP.Core.Contabilidad.Services;
// Traducción de: frmdocaux.vb (msgcnt)
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Contabilidad.Forms
{
    public partial class frmdocaux : Form
    {
        private OdbcConnection MyCon = new OdbcConnection();
        private ERP.Core.Compartido.Utilidades.Ayuda msgsas;
        private ClsContabilidad msgcnt = new ClsContabilidad();
        private bool ok;
        private double saldoCuentaContable_;
        public DataTable Dsdatos = new DataTable();
        public double Totalfact = 0;
        public bool Graba = false;
        public string CentroCos = "99999999";
        public string Agencia = "99999999";

        private double saldoCuentaContable
        {
            get { return saldoCuentaContable_; }
            set { saldoCuentaContable_ = value; }
        }

        public frmdocaux(OdbcConnection conexion)
        {
            InitializeComponent();
            this.MyCon = conexion;
        }

        private void frmfacturas_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Graba = false;
                this.Dispose();
                this.Close();
            }
        }

        private void frmfacturas_Load(object sender, EventArgs e)
        {
            DataSet DtDatos = new DataSet();
            DtDatos = msgcnt.LeeDocAuxiliares(this.txtnit.Text, this.txtCuenta.Text, this.TxtPeriodo.Text, this, MyCon);
            this.DtgDocs.Columns.Clear();
            this.DtgDocs.Rows.Clear();
            this.DtgDocs.DataSource = DtDatos.Tables[0];
            this.DtgDocs.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            this.CenterToScreen();
            this.TxtClaseAux.Focus();
        }

        private void CmbGrabar_Click(object sender, EventArgs e)
        {
            double Saldofact = 0;
            string TipoAux = " ";
            this.Graba = false;
            if (this.TxtClaseAux.Text.Trim() == "")
            {
                Dsdatos = BuscaFacturasSelecionadas();
                if (Dsdatos.Rows.Count > 0)
                    this.Graba = true;
            }
            else
            {
                string _tipoAuxStr = " ";
                // msgcnt.BuscarCuenta(this.txtCuenta.Text, this.MyCon, ref _tipoAuxStr); // ERROR: CS1615, CS1620
                // Extract TipoAux from the 12th ref param (aux_domto)
                // BuscarCuenta overload without DataTable: call with aux_domto at param 12
                TipoAux = _tipoAuxStr;
                // Saldofact = msgcnt.BuscarSaldoDocAuxiliar(this.txtCuenta.Text, TipoAux, this.txtnit.Text, this.TxtPeriodo.Text, TxtClaseAux.Text + "-" + this.TxtDocAux.Text, MyCon); // ERROR: CS1503
                Dsdatos = CreaTabla();
                Dsdatos.Rows.Add(this.TxtClaseAux.Text, this.TxtDocAux.Text, this.DtFecVence.Value, Saldofact);
                Totalfact = Saldofact;
                if (this.TxtClaseAux.Text.Trim() != "" && this.TxtDocAux.Text.Trim() != "")
                    this.Graba = true;
            }
            this.Close();
        }

        private void TxtDocAux_LostFocus(object sender, EventArgs e)
        {
            string TipoAuxStr = " ", cencos = "99999999";
            if (this.TxtDocAux.Text != "")
            {
                string _n1 = "N", _n2 = "N", _n3 = "N";
                // msgcnt.BuscarCuenta(this.txtCuenta.Text, this.MyCon, // ERROR: CS7036
                    // ref _n1, ref _n2, ref _n3, ref cencos, // ERROR: CS7036
                    // ref _n1, ref _n2, ref _n3, ref _n1, ref _n1, ref TipoAuxStr); // ERROR: CS7036

                DateTime _fecVence = this.DtFecVence.Value;
                // ok = msgcnt.BuscaDocAuxiliar(this.TxtPeriodo.Text, TipoAuxStr, // ERROR: CS1503
                    // this.txtCuenta.Text, this.txtnit.Text, Agencia, CentroCos, // ERROR: CS1503
                    // this.TxtClaseAux.Text, this.TxtDocAux.Text, this.MyCon, // ERROR: CS1503
                    // ref _n1, ref _n2, ref _fecVence); // ERROR: CS1503
                if (ok)
                {
                    // saldoCuentaContable = msgcnt.BuscarSaldoDocAuxiliar(this.txtCuenta.Text, TipoAuxStr, // ERROR: CS1503
                        // this.txtnit.Text, this.TxtPeriodo.Text, // ERROR: CS1503
                        // this.TxtClaseAux.Text + "-" + this.TxtDocAux.Text, this.MyCon); // ERROR: CS1503
                    if (saldoCuentaContable == 0)
                    {
                        if (MessageBox.Show("Este documento ya existe y presenta saldo cero, desea continuar?",
                            "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
                        {
                            this.TxtDocAux.Text = "";
                            this.TxtDocAux.Focus();
                            return;
                        }
                    }
                    this.DtFecVence.Enabled = false;
                }
                else
                {
                    MessageBox.Show("Documento " + this.TxtClaseAux.Text + " " + this.TxtDocAux.Text +
                        " no encontrado." + System.Environment.NewLine +
                        "Centro de costo " + CentroCos + ", Agencia " + Agencia,
                        "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DtFecVence.Value = DateTime.Now;
                    this.DtFecVence.Enabled = true;
                }
            }
        }

        private void DtgDocs_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            this.TxtClaseAux.Text = this.DtgDocs.SelectedCells[0].Value.ToString();
            this.TxtDocAux.Text = this.DtgDocs.SelectedCells[1].Value.ToString();
            this.DtFecVence.Value = Convert.ToDateTime(this.DtgDocs.SelectedCells[2].Value);
            this.TxtClaseAux.Focus();
        }

        private DataTable BuscaFacturasSelecionadas()
        {
            int fila = 0;
            DataTable dsDatafact = new DataTable();
            double TotFact = 0;
            dsDatafact = CreaTabla();
            while (fila < this.DtgDocs.SelectedRows.Count)
            {
                dsDatafact.Rows.Add(
                    this.DtgDocs.SelectedRows[fila].Cells[0].Value,
                    this.DtgDocs.SelectedRows[fila].Cells[1].Value,
                    this.DtgDocs.SelectedRows[fila].Cells[2].Value,
                    this.DtgDocs.SelectedRows[fila].Cells[4].Value);
                TotFact += Convert.ToDouble(this.DtgDocs.SelectedRows[fila].Cells[4].Value);
                fila += 1;
            }
            Totalfact = TotFact;
            return dsDatafact;
        }

        private DataTable CreaTabla()
        {
            DataTable dsDatafact = new DataTable();
            dsDatafact.TableName = "tblpagfact";
            dsDatafact.Columns.Add("Clasefact", typeof(string));
            dsDatafact.Columns.Add("Consecutivo", typeof(string));
            dsDatafact.Columns.Add("FechaVence", typeof(DateTime));
            dsDatafact.Columns.Add("saldo", typeof(double));
            return dsDatafact;
        }

        private void TxtDocAux_TextChanged(object sender, EventArgs e)
        {
        }

        private void DtgDocs_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void DtgDocs_MultiSelectChanged(object sender, EventArgs e)
        {
        }

        void SumaFacturasSelecionadas()
        {
            int fila = 0;
            double TotFactSel = 0;
            while (fila < this.DtgDocs.SelectedRows.Count)
            {
                TotFactSel += Convert.ToDouble(this.DtgDocs.SelectedRows[fila].Cells[4].Value);
                fila += 1;
            }
            this.LblTotal.Text = Strings.FormatNumber(TotFactSel, 2);
        }

        private void DtgDocs_CellStateChanged(object sender, DataGridViewCellStateChangedEventArgs e)
        {
        }

        private void DtgDocs_MouseCaptureChanged(object sender, EventArgs e)
        {
            SumaFacturasSelecionadas();
        }

        private void DtgDocs_LocationChanged(object sender, EventArgs e)
        {
        }

        private void DtgDocs_DockChanged(object sender, EventArgs e)
        {
        }

        private void DtgDocs_CurrentCellChanged(object sender, EventArgs e)
        {
            SumaFacturasSelecionadas();
        }

        private void DtgDocs_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
        }
    }
}
