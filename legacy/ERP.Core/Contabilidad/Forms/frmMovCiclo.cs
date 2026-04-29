using ERP.Core.Contabilidad.Services;
// Traducción de: frmMovCiclo.vb (msgcnt)
using System.Data;
using System.Data.Odbc;

namespace ERP.Core.Contabilidad.Forms
{
    public partial class frmMovCiclo : System.Windows.Forms.Form
    {
        private OdbcConnection mycon;
        private ClsContabilidad msgcnt = new ClsContabilidad();
        private DataSet dsdata = new DataSet();
        private DataSet dsdatosmov = new DataSet();

        public frmMovCiclo(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void frmMovCiclo_Load(object sender, System.EventArgs e)
        {
            this.CenterToScreen();
            // this.msgcnt.AgregaColumnasMovPeriodo(dsdatosmov); // ERROR: CS1620
            cargarDatos();
        }

        private void BtnSalir_Click(object sender, System.EventArgs e)
        {
            this.Close();
        }

        private void BtnImprimir_Click(object sender, System.EventArgs e)
        {
            imprimir();
        }

        void cargarDatos()
        {
            int i = 1, n = 0;
            double saldoinicial = 0, debito = 0, credito = 0, saldo = 0;
            double TotDebito = 0, TotCredito = 0;
            int periodo;

            periodo = int.Parse(((int.Parse(this.Lblperiodo.Text.Substring(0, 4)) - 1).ToString()) + "12");

            n = (int.Parse(this.Lblperiodo.Text.Substring(4)) * 2);
            if (this.Lblperiodo.Text.Substring(4) == "13")
                n = n - 1;

            // dsdata = msgcnt.CargarMovPeriodos(this.Lblperiodo.Text.Substring(0, 4), this.LblCuenta.Text, this.mycon); // ERROR: CS1503
            if (dsdata.Tables["TblMovCiclo"].Rows.Count > 0)
            {
                saldoinicial = System.Convert.ToDouble(dsdata.Tables["TblMovCiclo"].Rows[0][0]);
                this.DgwGrilla.Rows.Add(periodo, 0, 0, 0, saldoinicial);
                CargarDataset(periodo, 0, 0, 0, saldoinicial);
                periodo = periodo + 89;
                while (i < n)
                {
                    DataRow row = dsdata.Tables["TblMovCiclo"].Rows[0];
                    debito = System.Convert.ToDouble(row[i]);
                    credito = System.Convert.ToDouble(row[i + 1]);
                    saldo = saldoinicial + (debito - credito);

                    this.DgwGrilla.Rows.Add(periodo, saldoinicial, debito, credito, saldo);
                    CargarDataset(periodo, saldoinicial, debito, credito, saldo);

                    TotDebito = TotDebito + debito;
                    TotCredito = TotCredito + credito;
                    saldoinicial = saldo;
                    periodo = periodo + 1;
                    i = i + 2;
                }
                this.LblTotDeb.Text = Microsoft.VisualBasic.Strings.FormatNumber(TotDebito, 2);
                this.LblTotCred.Text = Microsoft.VisualBasic.Strings.FormatNumber(TotCredito, 2);
            }
        }

        void imprimir()
        {
            // this.msgcnt.ImprimirReporte(this, this.LblCuenta.Text, this.LblNombre.Text, // ERROR: CS1503
                // this.Lblperiodo.Text, dsdatosmov, this.empresappl.Text, // ERROR: CS1503
                // this.LblNit.Text, this.LblDireccion.Text, this.LblTelefono.Text); // ERROR: CS1503
        }

        void CargarDataset(int periodo, double saldoini, double debito, double credito, double saldo)
        {
            this.dsdatosmov.Tables["MovPeriodo"].Rows.Add(periodo, saldoini, debito, credito, saldo);
        }
    }
}
