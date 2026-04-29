using ERP.Core.Contabilidad.Services;
// Traducción de: cnt_cuadredoc.vb (msgcnt)
using System;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Contabilidad.Forms
{
    public partial class cnt_cuadredoc : Form
    {
        private ClsContabilidad msgcnt = new ClsContabilidad();
        private ERP.Core.Tesoreria.Services.clstesoreria clstesoreria;
        private ERP.Core.Contabilidad.Models.ParamCnt msgsyscnt = new ERP.Core.Contabilidad.Models.ParamCnt();
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private OdbcConnection myConec01 = new OdbcConnection();

        public cnt_cuadredoc(OdbcConnection conexion)
        {
            myConec01 = conexion;
            InitializeComponent();
            // ERP.Core.Tesoreria.Services needs pstUsuario from varini; initialize after connection is set
            clstesoreria = new ERP.Core.Tesoreria.Services.clstesoreria(null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void cmbGrabar_Click(object sender, EventArgs e)
        {
            bool ok;
            string stNit = "N", stcencos = "N";

            if (txtCredito.Text.Trim() == "" || !Information.IsNumeric(txtCredito.Text))
                txtCredito.Text = "0";
            if (txtDebito.Text.Trim() == "" || !Information.IsNumeric(txtDebito.Text))
                txtDebito.Text = "0";

            ok = ValidaCampos();
            if (ok)
                return;

            string cuentaRef = txtCuentaCierre.Text;
            // BuscarCuenta: extract stNit(p3) and stcencos(p4), dummy the rest
            string _n = "N"; decimal _tasa = 0; int _est = 0; string _s = " ";
            msgcnt.BuscarCuenta(ref cuentaRef, myConec01,
                ref stNit, ref stcencos, ref _n, ref _s, ref _s, ref _n, ref _n, ref _s,
                ref _tasa, ref _s, ref _est, ref _n, ref _n, ref _s);
            txtCuentaCierre.Text = cuentaRef;

            if (stNit == "Y")
            {
                ok = msgcnt.BuscarTercero(TxtCodigoter.Text, myConec01);
                if (!ok)
                {
                    MessageBox.Show("Nit no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtCodigoter.Text = "";
                    TxtCodigoter.Focus();
                    return;
                }
            }

            ok = ActulizaDocumento();
            if (ok)
            {
                this.Dispose();
                this.Close();
            }
        }

        bool ValidaCampos()
        {
            if (txtCuentaCierre.Text == "")
            { txtCuentaCierre.Focus(); return true; }
            if (TxtCodigoter.Text == "")
            { TxtCodigoter.Focus(); return true; }
            if (Txtcencos.Text == "")
            { Txtcencos.Focus(); return true; }
            if (txtDebito.Text == "0" && txtCredito.Text == "0")
                return true;
            if (Convert.ToInt64(txtDebito.Text) > 0 && Convert.ToInt64(txtCredito.Text) > 0)
                return true;
            if (Convert.ToInt64(txtDebito.Text) > 0 || Convert.ToInt64(txtCredito.Text) > 0)
                return false;
            else
                return true;
        }

        bool ActulizaDocumento()
        {
            bool ok;
            string ApliTes = "N", factura = " ", UsuarioMovto = "";
            string ClaseDocAux = " ", NumDocAux = " ", Detallefact = " ", Auxdomto = "0";
            DateTime FecVence = DateTime.Now;

            // BuscarCuenta: extract ApliTes(p9) and Auxdomto(p12=TipoAuxiliar)
            string cuentaRef = txtCuentaCierre.Text;
            string _s = "N"; decimal _tasa = 0; int _est = 0;
            string _cencos = "99999999", _nivel = "0"; string _apliCnt = "N", _consiBanca = "N", _bancoCon = "9999";
            string _natura = "N", _nombre = " "; string _mane = "N", _terc = "N";
            msgcnt.BuscarCuenta(ref cuentaRef, myConec01,
                ref _terc, ref _mane, ref _natura, ref _cencos, ref _nivel,
                ref _s, ref ApliTes, ref _nombre,
                ref _tasa, ref Auxdomto, ref _est, ref _apliCnt, ref _consiBanca, ref _bancoCon);
            txtCuentaCierre.Text = cuentaRef;

            if (txtComprobante.Tag != null)
                Detallefact = txtComprobante.Tag.ToString();

            if (string.Compare(Auxdomto, "0") > 0)
            {
                // msgcnt.CargaDocAuxliliar(TxtCodigoter.Text, txtCuentaCierre.Text, // ERROR: CS1503
                    // DtpFecha.Value.ToString("yyyyMM"), this, // ERROR: CS1503
                    // txtDebito.Text, txtCredito.Text, myConec01, // ERROR: CS1503
                    // ref ClaseDocAux, ref NumDocAux, ref FecVence, ref Detallefact, // ERROR: CS1503
                    // Txtcencos.Text, txtagencia.Text); // ERROR: CS1503

                if (NumDocAux == "")
                    return false;

                string _f1 = " ", _f2 = " ";
                // ok = msgcnt.BuscaDocAuxiliar(DtpFecha.Value.ToString("yyyyMM"), Auxdomto, // ERROR: CS1503
                    // txtCuentaCierre.Text, TxtCodigoter.Text, // ERROR: CS1503
                    // txtagencia.Text, Txtcencos.Text, ClaseDocAux, NumDocAux, // ERROR: CS1503
                    // myConec01, ref _f1, ref _f2, ref FecVence); // ERROR: CS1503

                // if (!ok) // ERROR: CS0165
                {
                    if (Auxdomto == "1")
                    {
                        if (Convert.ToDouble(txtCredito.Text) > 0)
                        {
                            MessageBox.Show("Documento auxiliar no se puede crear,  movimiento invalido",
                                "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        }
                    }
                    else if (Auxdomto == "2")
                    {
                        if (Convert.ToDouble(txtDebito.Text) > 0)
                        {
                            MessageBox.Show("Documento auxiliar no se puede crear,  movimiento invalido",
                                "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        }
                    }
                }

                factura = ClaseDocAux + "-" + NumDocAux;

                if (ApliTes == "Y")
                {
                    clstesoreria.GrabaFactura(
                        ClaseDocAux + "-" + NumDocAux,
                        TxtCodigoter.Text,
                        txtCuentaCierre.Text,
                        int.Parse(DtpFecha.Value.ToString("yyyyMM")),
                        myConec01,
                        txtComprobante.Text,
                        TxtConseCpte.Text,
                        TxtCodigoter.Text,
                        DtpFecha.Value,
                        FecVence,
                        FecVence,
                        Convert.ToDouble(txtDebito.Text),
                        Convert.ToDouble(txtCredito.Text),
                        Detallefact,
                        "O",
                        ClaseDocAux,
                        NumDocAux);
                }
            }

            if (this.Tag != null)
                UsuarioMovto = this.Tag.ToString();
            else
                UsuarioMovto = msgcnt.varini.pstUsuario;

            msgcnt.GrabaMovimiento(
                txtComprobante.Text,
                Convert.ToDouble(TxtConseCpte.Text),
                txtCuentaCierre.Text,
                txtagencia.Text.Trim(),
                DtpFecha.Value.ToString("yyyyMM"),
                TxtCodigoter.Text,
                DtpFecha.Value,
                Detallefact,
                NumDocAux,
                Convert.ToDouble(txtDebito.Text),
                Convert.ToDouble(txtCredito.Text),
                0.0,
                UsuarioMovto,
                myConec01,
                banco: 9999,
                FACTURA: factura,
                IdBenef: TxtCodigoter.Text,
                Cencosto: Txtcencos.Text,
                ClaseAux: ClaseDocAux);
            return true;
        }

        private void cop_cuadredoc_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            double dif = Convert.ToDouble(txtDebitos.Text) - Convert.ToDouble(TxtCreditos.Text);
            this.TxtDiferencia.Text = dif.ToString();
            txtDebito.Text = "0";
            txtCredito.Text = "0";
            if (dif < 0)
            {
                txtCredito.Text = "0";
                txtDebito.Text = Strings.FormatNumber(dif * -1, 2);
            }
            else
            {
                txtDebito.Text = "0";
                txtCredito.Text = Strings.FormatNumber(dif, 2);
            }
        }

        private void HelpTerceros_Click(object sender, EventArgs e)
        {
            TxtCodigoter.Text = msgsyscnt.HelpNits(myConec01, this);
            TxtCodigoter.Focus();
        }

        private void txtCuentaCierre_LostFocus(object sender, EventArgs e)
        {
            bool ok, CampoError;
            string stCar = "N", stTes = "N", stNit = "N", stcencos = "N";
            string cencos = "99999999", _nivel = "0";

            if (txtCuentaCierre.Text.Trim() != "")
            {
                string cuentaRef = txtCuentaCierre.Text;
                decimal _tasa = 0; int _est = 0; string _s = " ";
                string _n = "N", _natura = "N", _nombre = " "; string _apliCnt = "N", _consiBanca = "N", _bancoCon = "9999";
                msgcnt.BuscarCuenta(ref cuentaRef, myConec01,
                    ref stNit, ref stcencos, ref _natura, ref cencos, ref _nivel,
                    ref stCar, ref stTes, ref _nombre,
                    ref _tasa, ref _s, ref _est, ref _apliCnt, ref _consiBanca, ref _bancoCon);
                txtCuentaCierre.Text = cuentaRef;

                int nivel = 0;
                int.TryParse(_nivel, out nivel);

                ok = !string.IsNullOrEmpty(cuentaRef);

                if (ok)
                {
                    CampoError = ValidaCamposCuenta(stNit, stcencos, stCar, nivel);
                    if (CampoError)
                        return;
                }
                else
                {
                    MessageBox.Show("Cuenta contable no existe o no es de movimiento",
                        "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtCuentaCierre.Text = "";
                    txtCuentaCierre.Focus();
                }
            }
        }

        private bool ValidaCamposCuenta(string stnit, string stcencos, string stCar, int Nivel)
        {
            if (stnit == "Y")
            {
                TxtCodigoter.Enabled = true;
                TxtCodigoter.Text = "0";
            }
            else
            {
                TxtCodigoter.Enabled = false;
                TxtCodigoter.Text = "99999999999999";
            }

            if (stcencos == "Y")
            {
                Txtcencos.Enabled = true;
                Txtcencos.Text = "";
            }
            else
            {
                Txtcencos.Enabled = false;
                Txtcencos.Text = "99999999";
            }

            if (stCar != "Y")
            {
                MessageBox.Show("Cuenta no es de cartera, modificar catalogo",
                    "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtCuentaCierre.Text = "";
                txtCuentaCierre.Focus();
                return false;
            }

            if (Nivel != 6)
            {
                MessageBox.Show("Nivel de la cuenta no permitido",
                    "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtCuentaCierre.Text = "";
                txtCuentaCierre.Focus();
                return false;
            }

            return true;
        }

        private void TxtCodigoter_LostFocus(object sender, EventArgs e)
        {
            bool ok;
            if (TxtCodigoter.Text != "")
            {
                ok = msgcnt.BuscarTercero(TxtCodigoter.Text, myConec01);
                if (!ok)
                {
                    MessageBox.Show("Nit no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtCodigoter.Text = "";
                    TxtCodigoter.Focus();
                }
            }
        }

        private void help_cuentacon_Click(object sender, EventArgs e)
        {
            txtCuentaCierre.Text = msgsyscnt.HelpcuentaContables(myConec01, this);
            txtCuentaCierre.Focus();
        }

        private void Txtcencos_LostFocus(object sender, EventArgs e)
        {
            bool ok;
            if (Txtcencos.Text.Trim() == "")
            {
                Txtcencos.Focus();
            }
            else
            {
                Txtcencos.Text = Microsoft.VisualBasic.Strings.Right("00000000" + Txtcencos.Text, 8);
                if (Txtcencos.Text != "")
                {
                    ok = msgcnt.BuscarCencos(Txtcencos.Text, myConec01);
                    if (!ok)
                    {
                        MessageBox.Show("Cencosto no existe", "SOLIDO",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Txtcencos.Text = "";
                        Txtcencos.Focus();
                    }
                }
            }
        }

        private void helpcencos_Click(object sender, EventArgs e)
        {
            Txtcencos.Text = msgsyscnt.HelpCencos(myConec01, this);
            Txtcencos.Focus();
        }

        private void HelpAgencias_Click(object sender, EventArgs e)
        {
            // txtagencia.Text = paramCop.HelpAgencia(myConec01, this); // ERROR: CS1061
            txtagencia.Focus();
        }
    }
}
