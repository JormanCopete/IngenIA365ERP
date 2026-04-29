using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class cop_frmObligaciones : Form
    {
        public bool Aplicar;
        public int tiposeleccion = 0;
        public string periodo = "";
        public bool linea;
        private ERP.Core.Compartido.Configuracion.ParamSys MsgSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        public DataSet datos = new DataSet();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();

        public cop_frmObligaciones(System.Data.Odbc.OdbcConnection conexion)
            : base()
        {
            InitializeComponent();
            this.myconnect = conexion;
        }

        public void AbrirConexion()
        {
            if (this.myconnect.State != ConnectionState.Open)
            {
                this.myconnect.Open();
            }
        }

        private void cop_frmObligaciones_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            string _emp = "", _srv = "", _bd = "", _nom = "", _usr = "", _fec = "";
            MsgSys.ConfiguraForma(this, ref _emp, ref _srv, ref _bd, ref _nom, ref _usr, ref _fec);
            this.KeyPreview = true;

            switch (tiposeleccion)
            {
                case 1:
                    Grp_Generar.Enabled = true;
                    RdBtn_Obligacion.Checked = true;
                    break;
                default:
                    RdBtn_Lineas.Checked = true;
                    Grp_Generar.Enabled = false;
                    break;
            }

            if (Aplicar)
            {
                RdBtn_Lineas.Checked = linea;
                Grp_Generar.Enabled = false;
                this.Chk_Aplicar.Checked = true;
            }
        }

        private void cop_frmObligaciones_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.Close();
                    break;
            }
        }

        private void RadioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (RdBtn_Lineas.Checked)
            {
                Generar();
            }
        }

        private void RadioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (RdBtn_Obligacion.Checked)
            {
                Generar();
            }
        }

        private void Generar()
        {
            if (Aplicar == false)
            {
                datos.Tables.Clear();
                Dgv_Obligacion.AutoGenerateColumns = false;

                if (RdBtn_Obligacion.Checked)
                {
                    msgcop.obligacionesCausacion(LblCodigo.Text, this.tiposeleccion, periodo, false, myconnect, ref datos);
                }
                else
                {
                    msgcop.obligacionesCausacion(LblCodigo.Text, this.tiposeleccion, periodo, true, myconnect, ref datos);
                }
            }

            if (RdBtn_Obligacion.Checked)
            {
                this.Dgv_Obligacion.Columns["numero"].Visible = true;
            }
            else
            {
                this.Dgv_Obligacion.Columns["numero"].Visible = false;
            }

            Dgv_Obligacion.DataSource = datos.Tables["obligacion"];
        }

        private void SasToolBar1_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Dgv_Obligacion_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void Dgv_Obligacion_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1)
            {
                return;
            }

            if (Dgv_Obligacion.Columns[e.ColumnIndex].Name == "extras")
            {
                if ((Dgv_Obligacion.Rows[e.RowIndex].Cells["extras"].Value.ToString().Trim().ToUpper()) != "N" && Dgv_Obligacion.Rows[e.RowIndex].Cells["extras"].Value.ToString().Trim().ToUpper() != "Y")
                {
                    MessageBox.Show("EL tipo de Extra debe ser N para NO o Y para SI");
                    Dgv_Obligacion.Rows[e.RowIndex].Cells["extras"].Value = "N";
                }
                else if (Convert.ToDouble(Dgv_Obligacion.Rows[e.RowIndex].Cells["lincred"].Value.ToString().Trim()) < 1000 && Dgv_Obligacion.Rows[e.RowIndex].Cells["extras"].Value.ToString().Trim().ToUpper() == "Y")
                {
                    MessageBox.Show("Linea no maneja Cuotas Extra");
                    Dgv_Obligacion.Rows[e.RowIndex].Cells["extras"].Value = "N";
                }
            }
            else if (Dgv_Obligacion.Columns[e.ColumnIndex].Name == "Numcuotas")
            {
                if (Dgv_Obligacion.Rows[e.RowIndex].Cells["Numcuotas"].Value.ToString().Trim() != "")
                {
                    if (Information.IsNumeric(Dgv_Obligacion.Rows[e.RowIndex].Cells["Numcuotas"].Value.ToString().Trim()) == false)
                    {
                        MessageBox.Show("El Numero de Cuotas debe ser Numerico");
                        Dgv_Obligacion.Rows[e.RowIndex].Cells["Numcuotas"].Value = "";
                    }
                }
            }
        }

        private void Chk_Aplicar_CheckedChanged(object sender, EventArgs e)
        {
            if (Chk_Aplicar.Checked)
            {
                DataSet datos2 = datos.Copy();
                double cantidad = this.Dgv_Obligacion.RowCount - 1;
                datos2.Tables["obligacion"].Rows.Clear();

                for (int i = 0; i <= cantidad; i++)
                {
                    datos2.Tables["obligacion"].Rows.Add(
                        Dgv_Obligacion.Rows[i].Cells["lincred"].Value,
                        Dgv_Obligacion.Rows[i].Cells["numero"].Value,
                        Dgv_Obligacion.Rows[i].Cells["descripcion"].Value,
                        Dgv_Obligacion.Rows[i].Cells["cuota"].Value,
                        Dgv_Obligacion.Rows[i].Cells["saldo"].Value,
                        Dgv_Obligacion.Rows[i].Cells["NumCuotas"].Value,
                        Dgv_Obligacion.Rows[i].Cells["extras"].Value,
                        Dgv_Obligacion.Rows[i].Cells["periodd"].Value);
                }
                datos = datos2;
                Grp_Generar.Enabled = false;
                Aplicar = true;
                linea = RdBtn_Lineas.Checked;
            }
            else
            {
                Aplicar = false;
                linea = RdBtn_Lineas.Checked;

                switch (tiposeleccion)
                {
                    case 1:
                        Grp_Generar.Enabled = true;
                        break;
                    default:
                        RdBtn_Lineas.Checked = true;
                        break;
                }
            }
        }
    }
}
