using System;
using System.Data;
using System.Windows.Forms;

namespace ERP.Core.Inventario.Forms
{
    public partial class Inv_frmotroimp : Form
    {
        public DataTable DsdatosImp = new DataTable();

        private void Inv_frmotroimp_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            Creatabla();
            InitializaCampos();
        }

        private void Creatabla()
        {
            double Stdouble = 0;
            DsdatosImp.TableName = "tblotrosImp";
            DsdatosImp.Columns.Add("Valadm", Stdouble.GetType());
            DsdatosImp.Columns.Add("IvaAdm", Stdouble.GetType());
            DsdatosImp.Columns.Add("IvaTiq", Stdouble.GetType());
            DsdatosImp.Columns.Add("OtroImp", Stdouble.GetType());
            DsdatosImp.Columns.Add("AeroPort", Stdouble.GetType());
            DsdatosImp.Columns.Add("ImpComb", Stdouble.GetType());
        }

        private void InitializaCampos()
        {
            this.TxtValAdm.Text = "0";
            this.TxtValAeroInt.Text = "0";
            this.TxtValImpCombustible.Text = "0";
            this.TxtValImpInt.Text = "0";
            this.TxtValIvaAdm.Text = "0";
            this.TxtValIvaTiq.Text = "0";
        }

        private void CmdGuardar_Click(object sender, EventArgs e)
        {
            AgregaDatos();
            this.Close();
            this.Dispose();
        }

        private void AgregaDatos()
        {
            DsdatosImp.Rows.Add(this.TxtValAdm.Text, this.TxtValIvaAdm.Text, this.TxtValIvaTiq.Text, this.TxtValImpInt.Text, this.TxtValAeroInt.Text, this.TxtValImpCombustible.Text);
        }

        private void CmbSalir_Click(object sender, EventArgs e)
        {
            this.Close();
            this.Dispose();
        }
    }
}
