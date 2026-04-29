using System;
using System.Data;
using System.Windows.Forms;

namespace ERP.Core.Nomina.Forms
{
    public partial class frmfiltros : Form
    {
        public DataSet DataSetwhere = new DataSet();
        public bool cancelar = false;

        public frmfiltros()
        {
            InitializeComponent();
        }

        private void CmbAceptar_Click(object sender, EventArgs e)
        {
            string Empresa = null, Cencos = null, periodicidad, ciudad;
            string Estado = "0";
            string seccion = null;

            switch (this.CbxEmpresa.SelectedIndex)
            {
                case 1:
                    Empresa = this.CbxEmpresa.Text + "'" + this.TxtEmpresa.Text + "'";
                    break;
                case 2:
                    Empresa = this.CbxEmpresa.Text + "'" + this.TxtEmpresa.Text + "'";
                    break;
                default:
                    Empresa = " > 0";
                    break;
            }

            switch (this.CbxCencos.SelectedIndex)
            {
                case 1:
                    Cencos = this.CbxCencos.Text + "'" + ("00000000" + this.TxtCencos.Text).PadLeft(8).Substring(("00000000" + this.TxtCencos.Text).Length - 8) + "'";
                    break;
                case 2:
                    Cencos = this.CbxCencos.Text + "'" + ("00000000" + this.TxtCencos.Text).PadLeft(8).Substring(("00000000" + this.TxtCencos.Text).Length - 8) + "'";
                    break;
                default:
                    Cencos = " <> 'zz'";
                    break;
            }

            switch (this.CbxPeriodicidad.SelectedIndex)
            {
                case 1:
                    periodicidad = this.CbxPeriodicidad.Text + "'" + this.CmbPeriodicidad.SelectedIndex + "'";
                    break;
                case 2:
                    periodicidad = this.CbxPeriodicidad.Text + "'" + this.CmbPeriodicidad.SelectedIndex + "'";
                    break;
                default:
                    periodicidad = " >= '0'";
                    break;
            }

            switch (this.CbxCiudad.SelectedIndex)
            {
                case 1:
                    ciudad = this.CbxCiudad.Text + "'" + this.TxtCiudad.Text + "'";
                    break;
                case 2:
                    ciudad = this.CbxCiudad.Text + "'" + this.TxtCiudad.Text + "'";
                    break;
                default:
                    ciudad = " >= '0'";
                    break;
            }

            switch (this.CbxEstado.SelectedIndex)
            {
                case 0:
                    Estado = " >= '0'";
                    break;
                case 1:
                    Estado = " <= '1'";
                    break;
                case 2:
                    Estado = " = '2'";
                    break;
            }

            switch (this.CBxSeccion.SelectedIndex)
            {
                case 1:
                    seccion = this.CBxSeccion.Text + "'" + ("0000" + this.TxtSeccion.Text).PadLeft(4).Substring(("0000" + this.TxtSeccion.Text).Length - 4) + "'";
                    break;
                case 2:
                    seccion = this.CBxSeccion.Text + "'" + ("0000" + this.TxtSeccion.Text).PadLeft(4).Substring(("0000" + this.TxtSeccion.Text).Length - 4) + "'";
                    break;
                default:
                    seccion = " <> 'zz'";
                    break;
            }

            GrabaWhere(Empresa, Cencos, periodicidad, ciudad, Estado, seccion);
            this.Close();
        }

        private void Creatabla()
        {
            string ststring = " ";
            DataSetwhere.Tables.Add("tblwhere");
            DataSetwhere.Tables["tblwhere"].Columns.Add("empresa", ststring.GetType());
            DataSetwhere.Tables["tblwhere"].Columns.Add("cencos", ststring.GetType());
            DataSetwhere.Tables["tblwhere"].Columns.Add("periodicidad", ststring.GetType());
            DataSetwhere.Tables["tblwhere"].Columns.Add("ciudad", ststring.GetType());
            DataSetwhere.Tables["tblwhere"].Columns.Add("estado", ststring.GetType());
            DataSetwhere.Tables["tblwhere"].Columns.Add("seccion", ststring.GetType());
        }

        private void frmfiltros_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            this.CbxEstado.SelectedIndex = 0;
            Creatabla();
        }

        private void GrabaWhere(string empresa, string cencos, string periodicidad, string ciudad, string estado, string seccion)
        {
            DataSetwhere.Tables["tblwhere"].Rows.Add(empresa, cencos, periodicidad, ciudad, estado, seccion);
        }

        private void CmbSalir_Click(object sender, EventArgs e)
        {
            cancelar = true;
            this.Close();
        }
    }
}
