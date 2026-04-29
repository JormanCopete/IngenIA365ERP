using ERP.Core.CarteraFinanciera.Services.Depositos;
using System;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class dep_fverdocu01 : Form
    {
        private ERP.Core.Compartido.Datos.ClsConect dbconect = new ERP.Core.Compartido.Datos.ClsConect();
        private ClsDepositos depositos = new ClsDepositos();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera Cartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Models.ParamCop parCar = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys ParSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private System.Data.Odbc.OdbcConnection conect = new System.Data.Odbc.OdbcConnection();
        private bool ok = false;
        public string Compronte = "";
        public double NumCompronte = 0;
        private string Cerrado = "N";
        private string Anulado = "N";

        public dep_fverdocu01(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.conect = conexion;
        }

        private void dep_fverdocu01_Load(object sender, EventArgs e)
        {
            dbconect.MyOdbcConect(ref Var.varini);
            dbconect.LlenarVarini(ref Var.varini);
            server0.Text = Var.varini.pstServer;
            bdatos.Text = Var.varini.pstBdatos;
            usuario.Text = Var.varini.pstUsuario;
            empresappl.Text = Var.varini.pstEmpresa;
            progname.Text = this.Name;
            this.Text += " " + Compronte + " - " + NumCompronte;
            LblCompronte.Text = Compronte;
            LblNumCompronte.Text = NumCompronte.ToString();
            LblFecha.Text = DateTime.Now.Date.ToShortDateString();
            this.CenterToScreen();

            Lblperiodo.Text = "999999";
            DateTime dummy1 = new DateTime(1950, 1, 1), dummy2 = new DateTime(1950, 1, 1);
            DateTime dummy3 = new DateTime(1950, 1, 1);
            string dummy4 = null;
            string periodo = Lblperiodo.Text;
            ParSys.buscaPeriodo("ahor", conect, ref dummy1, ref dummy2, dummy3, ref dummy4, ref periodo);
            Lblperiodo.Text = periodo;

            try
            {
                CargarInformacion();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CargarInformacion()
        {
            string debitos = LblDebitos.Text, creditos = LblCreditos.Text, numReg = LblNumRegistro.Text;
            string detalle = LblDetalle.Text, fecha = LblFecha.Text, descripcion = LblDescripcion.Text;
            string dummy1 = null, dummy2 = null, dummy3 = null, dummy4 = null;

            double _deb = 0, _cred = 0, _dif = 0;
            DateTime _fecMovto = new DateTime(1950, 1, 1);
            string _cenCosto = "99999999", _cuenta = "999999999999", _modulo = null;
            string _restricTes = "N", _devMora = "N", _lavActi = "N";
            int _periodo = 0;
            //ok = Cartera.BuscaComprobante(ref Compronte, ref NumCompronte, false, conect,
            //    ref detalle, ref dummy2, ref _deb, ref _cred,
            //    ref Cerrado, ref Anulado, ref _dif, ref detalle,
            //    ref dummy4, ref dummy1, ref _fecMovto, ref descripcion, ref descripcion,
            //    ref _cenCosto, ref _cuenta, ref _modulo, ref _periodo, ref numReg, ref dummy2,
            //    ref _restricTes, ref _modulo, ref _devMora, ref _lavActi);

            LblDebitos.Text = debitos;
            LblCreditos.Text = creditos;
            LblNumRegistro.Text = numReg;
            LblDetalle.Text = detalle;
            LblFecha.Text = fecha;
            LblDescripcion.Text = descripcion;

            LblEstado.Text = Cerrado == "Y" ? "Cerrado" : "Abierto";
            if (Anulado == "Y") LblEstado.Text = "Anulado";

            double deb = Microsoft.VisualBasic.Information.IsNumeric(LblDebitos.Text) ? Convert.ToDouble(LblDebitos.Text) : 0;
            double cred = Microsoft.VisualBasic.Information.IsNumeric(LblCreditos.Text) ? Convert.ToDouble(LblCreditos.Text) : 0;
            LblDebitos.Text = deb.ToString("N2");
            LblCreditos.Text = cred.ToString("N2");

            LisMov.DataSource = depositos.CargaMovimientos(Compronte, NumCompronte, conect).Tables[0];
            ConfigColumnas();
        }

        private void ConfigColumnas()
        {
            if (LisMov.Rows.Count > 0)
            {
                LisMov.Columns[0].Width = 30;
                LisMov.Columns[1].Width = 110;
                LisMov.Columns[2].Width = 50;
                LisMov.Columns[3].Width = 220;
                LisMov.Columns[4].Width = 75;
                LisMov.Columns[5].Width = 95;
                LisMov.Columns[5].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                LisMov.Columns[6].Width = 95;
                LisMov.Columns[6].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            for (int i = 0; i < LisMov.Rows.Count; i++)
                LisMov.Rows[i].Cells[0].Value = i + 1;
        }

        private void opcion1_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }

        private void LisMov_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void LisMov_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void cmbfirmas_Click(object sender, EventArgs e)
        {
            depositos.VerFirmas(txtCuenta.Text, txtFirmas.Text, this, Var.varini.pstUsuario, conect);
        }

        private void LisMov_DoubleClick(object sender, EventArgs e)
        {
            if (LisMov.SelectedRows.Count > 0)
            {
                int index = LisMov.SelectedRows[0].Index;
                txtCuenta.Text = LisMov.Rows[index].Cells[1].Value?.ToString();
                string codigoter = txtCodigoter.Text, nombreTercero = LblNombreTercero.Text;
                string firmas = txtFirmas.Text;
                bool sello = chkSello.Checked, protector = chkprotector.Checked;
                string d1=null,d2=null,d3=null,d4=null,d5=null,d6=null,d7=null,d8=null,d9=null,d10=null,d11=null,d12=null,d13=null;
                //depositos.BuscarCuentaAhorro(txtCuenta.Text, conect, ClsDepositos.Navega.Ninguno,
                //    ref codigoter, ref d1, ref d2, ref d3, ref d4, ref d5, ref d6, ref d7, ref d8,
                //    ref d9, ref d10, ref d11, ref d12, ref d13, ref sello, ref protector, ref d1, ref firmas);
                txtCodigoter.Text = codigoter;
                txtFirmas.Text = firmas;
                chkSello.Checked = sello;
                chkprotector.Checked = protector;

                string _codigoterTemp = txtCodigoter.Text;
                System.Data.DataSet _dsPar2 = new System.Data.DataSet();
                parCar.BuscaAsociado(ref _codigoterTemp, ref _dsPar2, conect);
                if (_dsPar2.Tables["tblasociados"] != null && _dsPar2.Tables["tblasociados"].Rows.Count > 0)
                    nombreTercero = _dsPar2.Tables["tblasociados"].Rows[0]["apellido"].ToString() + " " + _dsPar2.Tables["tblasociados"].Rows[0]["nombre"].ToString();
                LblNombreTercero.Text = nombreTercero;
            }
        }
    }
}
