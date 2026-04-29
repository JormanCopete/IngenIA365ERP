using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmreferencias : Form
    {
        private OdbcConnection mycon = new OdbcConnection();
        public DataSet dsreferencia = new DataSet();
        public string codigoter;
        private int fila = -1;
        private DataGridViewCellStyle style = new DataGridViewCellStyle();
        private DataGridViewCellStyle style2 = new DataGridViewCellStyle();

        public frmreferencias(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void frmreferencias_Load(object sender, EventArgs e)
        {
            string ststring = "";
            int stinteger = 0;
            int i = 0;
            style.BackColor = Color.Cyan;
            style2.BackColor = Color.White;
            IncluirReferenciaToolStripMenuItem.Enabled = false;
            QuitarRefereToolStripMenuItem.Enabled = false;

            if (dsreferencia.Tables.Count == 0)
            {
                dsreferencia.Tables.Add("TblReferencia");
                dsreferencia.Tables["TblReferencia"].Columns.Add("Referencia", ststring.GetType());
                dsreferencia.Tables["TblReferencia"].Columns.Add("Nombre", ststring.GetType());
                dsreferencia.Tables["TblReferencia"].Columns.Add("Direccion", ststring.GetType());
                dsreferencia.Tables["TblReferencia"].Columns.Add("Telefono", ststring.GetType());
                dsreferencia.Tables["TblReferencia"].Columns.Add("Ciudad", stinteger.GetType());
            }
            else
            {
                DataTable tbl = dsreferencia.Tables["TblReferencia"];
                while (stinteger < tbl.Rows.Count)
                {
                    while (i < DgwReferencias.RowCount)
                    {
                        if (tbl.Rows[stinteger]["Referencia"].ToString() == DgwReferencias["ClmReferencia", i].Value?.ToString()
                            && tbl.Rows[stinteger]["Nombre"].ToString() == DgwReferencias["ClmNombre", i].Value?.ToString())
                        {
                            DgwReferencias.Rows[i].DefaultCellStyle = style;
                            DgwReferencias["ClmMarca", i].Value = "Y";
                            break;
                        }
                        i++;
                    }
                    i = 0;
                    stinteger++;
                }
            }
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }

        private void DgwReferencias_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (DgwReferencias.RowCount > 0)
                {
                    fila = DgwReferencias.CurrentRow.Index;
                    if (DgwReferencias["ClmMarca", fila].Value == null)
                    {
                        IncluirReferenciaToolStripMenuItem.Enabled = true;
                        QuitarRefereToolStripMenuItem.Enabled = false;
                    }
                    else
                    {
                        IncluirReferenciaToolStripMenuItem.Enabled = false;
                        QuitarRefereToolStripMenuItem.Enabled = true;
                    }
                    Menu.Show(DgwReferencias, new Point(e.X, e.Y));
                }
            }
        }

        private void IncluirReferenciaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fila = DgwReferencias.CurrentRow.Index;
            DgwReferencias["ClmMarca", fila].Value = "Y";
            DgwReferencias.Rows[fila].DefaultCellStyle = style;

            DataGridViewRow row = DgwReferencias.Rows[fila];
            object ciudad = row.Cells["ClmcodCiudad"].Value;
            dsreferencia.Tables["TblReferencia"].Rows.Add(
                row.Cells["ClmReferencia"].Value,
                row.Cells["ClmNombre"].Value,
                row.Cells["ClmDireccion"].Value,
                row.Cells["ClmTelefono"].Value,
                ciudad == null ? 999999 : ciudad);
        }

        private void QuitarRefereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int indice = 0;
            fila = DgwReferencias.CurrentRow.Index;
            DgwReferencias["ClmMarca", fila].Value = null;
            DgwReferencias.Rows[fila].DefaultCellStyle = style2;

            DataTable tbl = dsreferencia.Tables["TblReferencia"];
            if (tbl.Rows.Count > 0)
            {
                string refVal = DgwReferencias.Rows[fila].Cells["ClmReferencia"].Value?.ToString();
                string nomVal = DgwReferencias.Rows[fila].Cells["ClmNombre"].Value?.ToString();
                DataRow[] found = tbl.Select("Referencia='" + refVal + "' AND Nombre='" + nomVal + "'");
                if (found.Length > 0)
                {
                    while (indice < tbl.Rows.Count)
                    {
                        if (tbl.Rows[indice]["Referencia"].ToString() == refVal
                            && tbl.Rows[indice]["Nombre"].ToString() == nomVal)
                        {
                            tbl.Rows.RemoveAt(indice);
                            break;
                        }
                        indice++;
                    }
                }
            }
        }

        private void BtnAgregar_Click(object sender, EventArgs e)
        {
            FrmAgregarReferencia frAgreRefe = new FrmAgregarReferencia(mycon);
            frAgreRefe.idcodigoter = codigoter;
            frAgreRefe.formulario = this;
            frAgreRefe.nomempresa = LblNomEmpresa.Text;
            frAgreRefe.datase = dsreferencia;
            this.Hide();
            frAgreRefe.ShowDialog(this);
            this.Close();
        }
    }
}
