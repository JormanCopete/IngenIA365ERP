using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class Frmparviv : Form
    {
        OdbcConnection mycon = new OdbcConnection();
        ERP.Core.CarteraFinanciera.Models.ParamCop MsgParcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        public DataSet dsdata = new DataSet();
        bool ok;

        public Frmparviv(OdbcConnection Conexion)
        {
            InitializeComponent();
            this.mycon = Conexion;
        }

        private void Frmparviv_Load(object sender, System.EventArgs e)
        {
            string ststring = "";
            int stinteger = 0;
            double stdouble = 0;
            this.CenterToScreen();
            InicializaDatos();
            if (dsdata.Tables[0].TableName != "tblsolparviv")
            {
                dsdata.Tables.Clear();
                dsdata.Tables.Add("tblsolparviv");
                DataColumnCollection cols = dsdata.Tables["tblsolparviv"].Columns;
                cols.Add("ClaViv", stinteger.GetType());
                cols.Add("TipViv", stinteger.GetType());
                cols.Add("IntSocial", ststring.GetType());
                cols.Add("Subsidio", ststring.GetType());
                cols.Add("EntRedes", stinteger.GetType());
                cols.Add("ValRedes", stdouble.GetType());
                cols.Add("Desembolso", stinteger.GetType());
                cols.Add("moneda", stinteger.GetType());
            }
            else
            {
                CargarDatos();
            }
        }

        void InicializaDatos()
        {
            CbxClaseVivienda.SelectedIndex = 0;
            CbxTipoVivienda.SelectedIndex = 0;
            CbxEntidadRedescuento.SelectedIndex = 0;
            CbxDesembolso.SelectedIndex = 0;
            Txtvalor.Text = "0";
            this.ChkSubsidio.Checked = false;
            this.ChkIntSocial.Checked = false;
        }

        void CargarDatos()
        {
            if (dsdata.Tables["tblsolparviv"].Rows.Count != 0)
            {
                DataRow row = dsdata.Tables["tblsolparviv"].Rows[0];
                CbxClaseVivienda.SelectedIndex = Convert.ToInt32(row["ClaViv"]);
                CbxTipoVivienda.SelectedIndex = Convert.ToInt32(row["TipViv"]);
                CbxEntidadRedescuento.SelectedIndex = Convert.ToInt32(row["EntRedes"]);
                CbxDesembolso.SelectedIndex = Convert.ToInt32(row["Desembolso"]);
                this.CbxMoneda.SelectedIndex = Convert.ToInt32(row["moneda"]);
                double valRedes = Microsoft.VisualBasic.Information.IsNumeric(row["ValRedes"]) ? Convert.ToDouble(row["ValRedes"]) : 0;
                Txtvalor.Text = Strings.FormatNumber(valRedes, 2);
                if (row["Subsidio"].ToString() == "Y")
                    this.ChkSubsidio.Checked = true;
                if (row["IntSocial"].ToString() == "Y")
                    this.ChkIntSocial.Checked = true;
            }
        }

        void GrabarDatos()
        {
            string IntSocial, SubSidio;
            if (this.CbxClaseVivienda.SelectedIndex != 0)
            {
                IntSocial = this.ChkIntSocial.Checked ? "Y" : "N";
                SubSidio = this.ChkSubsidio.Checked ? "Y" : "N";
                if (dsdata.Tables["tblsolparviv"].Rows.Count != 0)
                {
                    dsdata.Tables["tblsolparviv"].Rows.Clear();
                }
                dsdata.Tables["tblsolparviv"].Rows.Add(
                    CbxClaseVivienda.SelectedIndex, CbxTipoVivienda.SelectedIndex, IntSocial,
                    SubSidio, CbxEntidadRedescuento.SelectedIndex, Convert.ToDouble(Txtvalor.Text), CbxDesembolso.SelectedIndex, this.CbxMoneda.SelectedIndex);
                this.InicializaDatos();
            }
            else
            {
                MessageBox.Show("Debe escoger una clase de vivienda.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxClaseVivienda.Focus();
            }
        }

        private void BtnGrabar_Click(object sender, System.EventArgs e)
        {
            GrabarDatos();
        }

        private void Txtvalor_LostFocus(object sender, System.EventArgs e)
        {
            if (Microsoft.VisualBasic.Information.IsNumeric(this.Txtvalor.Text))
            {
                this.Txtvalor.Text = Strings.FormatNumber(Convert.ToDouble(this.Txtvalor.Text), 2);
            }
            else
            {
                this.Txtvalor.Text = "0";
            }
        }

        private void BtnEliminar_Click(object sender, System.EventArgs e)
        {
            if (dsdata.Tables["tblsolparviv"].Rows.Count != 0)
            {
                dsdata.Tables["tblsolparviv"].Rows.Clear();
                this.InicializaDatos();
            }
        }

        private void BtnSalir_Click(object sender, System.EventArgs e)
        {
            this.Close();
        }
    }
}
