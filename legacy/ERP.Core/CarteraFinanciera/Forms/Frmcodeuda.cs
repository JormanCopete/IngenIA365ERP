using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class Frmcodeuda : Form
    {
        public string Codigoter;
        public string Lincred;
        public string Numero;
        private bool ok;
        private System.Data.Odbc.OdbcConnection mycon = new System.Data.Odbc.OdbcConnection();
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private DataSet DsDataSet = new DataSet();

        public Frmcodeuda(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void frmbase01_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            msgparsys.ConfiguraForma(this, this.LblNomEmpresa.Text, this.txtServer.Text, this.TxtBd.Text, this.TxtNomForma.Text, "", this.TxtfechaSys.Text);
            //msgcop.BuscaAsociado(this.TxtCodigoter.Text, this.mycon, this.LblNombre.Text);
            CargaDatosGrilla();
        }

        private void CargaDatosGrilla()
        {
            DsDataSet = msgcop.BuscaCredritosCoodeudor(this.TxtCodigoter.Text, this.Lblperiodo.Text, this.mycon);
            this.DtgDatos.AutoGenerateColumns = false;
            this.DtgDatos.DataSource = DsDataSet.Tables["TblCreCodeudor"];

            if (DsDataSet.Tables["TblCreCodeudor"].Rows.Count > 0)
            {
                CargaTotales(DsDataSet);
            }
            else
            {
                this.Close();
            }
        }

        private void CargaTotales(DataSet DsDataSet)
        {
            int I = 0;
            double TotalCap = 0, Total = 0, TotalOtros = 0, TotalInt = 0;
            double TotalMora = 0, TotalSaldo = 0;

            DataTable tbl = DsDataSet.Tables["TblCreCodeudor"];
            for (I = 0; I <= tbl.Rows.Count - 1; I++)
            {
                if (tbl.Rows[I]["Capital"].ToString() != "")
                    TotalCap = TotalCap + Convert.ToDouble(tbl.Rows[I]["Capital"].ToString());
                if (tbl.Rows[I]["Interes"].ToString() != "")
                    TotalInt = TotalInt + Convert.ToDouble(tbl.Rows[I]["Interes"].ToString());
                if (tbl.Rows[I]["Mora"].ToString() != "")
                    TotalMora = TotalMora + Convert.ToDouble(tbl.Rows[I]["Mora"].ToString());
                if (tbl.Rows[I]["otros"].ToString() != "")
                    TotalOtros = TotalOtros + Convert.ToDouble(tbl.Rows[I]["otros"].ToString());
                if (tbl.Rows[I]["total"].ToString() != "")
                    Total = Total + Convert.ToDouble(tbl.Rows[I]["total"].ToString());
                if (tbl.Rows[I]["saldo"].ToString() != "")
                    TotalSaldo = TotalSaldo + Convert.ToDouble(tbl.Rows[I]["saldo"].ToString());
            }

            this.LblCapital.Text = TotalCap.ToString("###,###,###");
            this.LblInteres.Text = TotalInt.ToString("###,###,###");
            this.LblMora.Text = TotalMora.ToString("###,###,###");
            this.LblOtros.Text = TotalOtros.ToString("###,###,###");
            this.LblSaldo.Text = TotalSaldo.ToString("###,###,###");
            this.LblTotalPago.Text = Total.ToString("###,###,###");
        }

        private void DtgDatos_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            switch (this.opcion.ButtonPressed)
            {
                case 1:
                    Salir();
                    break;
                case 2:
                    Salir();
                    break;
            }
        }

        private void opcion_Load(object sender, EventArgs e)
        {
        }

        private void cmbsalir_Click(object sender, EventArgs e)
        {
            Salir();
        }

        private void Salir()
        {
            this.Codigoter = null;
            this.Lincred = "0";
            this.Numero = "0";
            this.Close();
        }

        private void CmbEstCuenta_Click(object sender, EventArgs e)
        {
        }

        private void DtgDatos_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            this.Codigoter = this.DtgDatos.SelectedCells[0].Value.ToString();
            this.Lincred = this.DtgDatos.SelectedCells[2].Value.ToString();
            this.Numero = this.DtgDatos.SelectedCells[3].Value.ToString();
            this.Close();
        }

        private void SalirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Salir();
        }

        private void CmdImprimir_Click(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            ImprimirReporte();
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }

        private void ImprimirReporte()
        {
            ERP.Core.Compartido.Reportes.reporte ReporteCodeudas = new ERP.Core.Compartido.Reportes.reporte("cop_rcodeudas", false);
            ERP.Core.Compartido.Forms.imprimir confi_report = new ERP.Core.Compartido.Forms.imprimir();
            string Nit = " ", Direccion = " ", Nombre = " ", Telefono = " ";
            //msgparsys.BuscarCompania("0001", this.mycon, "", "", "", "", "", "", "", "", "", "", "", "", ref Nit, ref Direccion, "", "", "", "", "", "", "", "", "", ref Nombre, ref Telefono, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "");
            ReporteCodeudas.SetDataSource(this.DsDataSet.Tables["TblCreCodeudor"]);
            ReporteCodeudas.SetParameterValue("Empresa", Nombre);
            ReporteCodeudas.SetParameterValue("nit", Nit);
            ReporteCodeudas.SetParameterValue("direccion", Direccion);
            ReporteCodeudas.SetParameterValue("telefono", Telefono);
            ReporteCodeudas.SetParameterValue("Codeudor", this.TxtCodigoter.Text);
            ReporteCodeudas.SetParameterValue("NombreCodeudor", this.LblNombre.Text);
            //confi_report.CrystalReportViewer1.ReportSource = ReporteCodeudas;
            confi_report.ShowDialog(this);
        }

        private void ImprimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            ImprimirReporte();
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }
    }
}
