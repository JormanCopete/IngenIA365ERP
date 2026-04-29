using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Forms
{
    public partial class helptable : Form
    {
        private ERP.Core.Compartido.Datos.ClsConect Inicial = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect CadenaCone = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private OdbcConnection Conection = new OdbcConnection();
        private string SqlConsulta, TodosCampos, FiltroWhere;
        private DataTable ResConsulta = new DataTable();
        private int Contador, AnchoVentana;
        public bool Selecciono = false, DaleBuscar = false;
        public string Resultado = "", Resultado2 = "";
        public string NameTable, NameCampo1, NameCampo2, NameCampo3, NameCampo4, NameCampo5, Filtro;
        public string NameColumna1, NameColumna2, NameColumna3, NameColumna4, NameColumna5;

        public helptable()
        {
            InitializeComponent();
        }

        private bool Consultar()
        {
            ArmaCamposConsulta();
            MuestraGrilla();
            return true;
        }

        private bool MuestraGrilla()
        {
            bool result = false;
            // Conection.ConnectionString = CadenaCone.CadenaConexion; // ERROR: CS0176
            SqlConsulta = "select " + TodosCampos + " from " + NameTable + " " + FiltroWhere;
            // Inicial.ExecuteConsulta(SqlConsulta, Conection, "DS", ResConsulta); // ERROR: CS1620
            if (ResConsulta.Rows.Count > 0)
            {
                AnchoVentana = 0;
                result = true;
                this.DgvResultado.DataSource = ResConsulta;
                for (Contador = 0; Contador <= DgvResultado.Columns.Count - 1; Contador++)
                {
                    switch (Contador)
                    {
                        case 0:
                            DgvResultado.Columns[Contador].HeaderText = (NameColumna1.Trim() == "") ? NameCampo1 : NameColumna1;
                            break;
                        case 1:
                            DgvResultado.Columns[Contador].HeaderText = (NameColumna2.Trim() == "") ? NameCampo2 : NameColumna2;
                            break;
                        case 2:
                            DgvResultado.Columns[Contador].HeaderText = (NameColumna3.Trim() == "") ? NameCampo3 : NameColumna3;
                            break;
                        case 3:
                            DgvResultado.Columns[Contador].HeaderText = (NameColumna4.Trim() == "") ? NameCampo4 : NameColumna4;
                            break;
                        case 4:
                            DgvResultado.Columns[Contador].HeaderText = (NameColumna5.Trim() == "") ? NameCampo5 : NameColumna5;
                            break;
                    }
                    AnchoVentana += DgvResultado.Columns[Contador].Width;
                }
            }
            else
            {
                result = false;
            }
            return result;
        }

        private void ArmaCamposConsulta()
        {
            FiltroWhere = "";
            TodosCampos = NameCampo1;
            this.LblCodigo.Text = NameColumna1;

            switch (this.TxtNombre.Text.Trim())
            {
                case "":
                    if (NameCampo2.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo2;
                        this.LblNombre.Text = NameColumna2;
                    }
                    if (NameCampo3.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo3;
                        this.LblNombre.Text += ", " + NameColumna3;
                    }
                    if (NameCampo4.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo4;
                        this.LblNombre.Text += ", " + NameColumna4;
                    }
                    if (NameCampo5.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo5;
                        this.LblNombre.Text += ", " + NameColumna5;
                    }
                    if (Filtro.Trim() != "")
                    {
                        FiltroWhere = " where " + Filtro;
                    }
                    break;

                default:
                    FiltroWhere = " where " + NameCampo1 + " like '%" + this.TxtNombre.Text.Trim() + "%'";
                    if (NameCampo2.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo2;
                        FiltroWhere += " or " + NameCampo2 + " like '%" + this.TxtNombre.Text.Trim() + "%'";
                    }
                    if (NameCampo3.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo3;
                        FiltroWhere += " or " + NameCampo3 + " like '%" + this.TxtNombre.Text.Trim() + "%'";
                    }
                    if (NameCampo4.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo4;
                        FiltroWhere += " or " + NameCampo4 + " like '%" + this.TxtNombre.Text.Trim() + "%'";
                    }
                    if (NameCampo5.Trim() != "")
                    {
                        TodosCampos += "," + NameCampo5;
                        FiltroWhere += " or " + NameCampo5 + " like '%" + this.TxtNombre.Text.Trim() + "%'";
                    }
                    if (Filtro.Trim() != "")
                    {
                        FiltroWhere += " and " + Filtro;
                    }
                    break;
            }
        }

        private bool Validar()
        {
            if (ResConsulta.Rows.Count > 0)
            {
                ResConsulta.Clear();
            }
            return true;
        }

        private void helptable_KeyDown(object sender, KeyEventArgs e)
        {
        }

        private void helptable_Load(object sender, EventArgs e)
        {
            Button1_Click(null, null);
        }

        private void CargaProductos()
        {
        }

        private void DgvProductos_Click(object sender, EventArgs e)
        {
        }

        private void TxtFiltroProducto_KeyPress(object sender, KeyPressEventArgs e)
        {
        }

        private void TxtFiltroReferencia_TextChanged(object sender, EventArgs e)
        {
            ResConsulta.DefaultView.RowFilter = (NameCampo1 + " like '%" + this.TxtCodigo.Text + "%'");
            this.DgvResultado.DataSource = ResConsulta.DefaultView;
        }

        private void DgvDatos_DoubleClick(object sender, EventArgs e)
        {
            if (DgvResultado.SelectedRows.Count != 0)
            {
                Resultado = DgvResultado.SelectedRows[0].Cells[0].Value.ToString();
                Resultado2 = DgvResultado.SelectedRows[0].Cells[1].Value.ToString();
                this.Selecciono = true;
                this.Close();
            }
        }

        private void DgvDatos_MouseDoubleClick(object sender, MouseEventArgs e)
        {
        }

        private void DgvDatos_SelectionChanged(object sender, EventArgs e)
        {
        }

        private void TxtNombre_TextChanged(object sender, EventArgs e)
        {
            ResConsulta.DefaultView.RowFilter = (NameCampo2 + " like '%" + this.TxtNombre.Text + "%' or " + NameCampo3 + " like '%" + this.TxtNombre.Text + "%'");
            this.DgvResultado.DataSource = ResConsulta.DefaultView;
        }

        private void DgvResultado_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            this.Consultar();
        }
    }
}
