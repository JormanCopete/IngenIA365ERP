using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class cop_cuadredoc : Form
    {
        private Clscartera msgcop = new Clscartera();
        private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        private ERP.Core.Contabilidad.Models.ParamCnt Parcon = new ERP.Core.Contabilidad.Models.ParamCnt();
        private ERP.Core.Compartido.Utilidades.Ayuda msgsas;
        private ERP.Core.Tesoreria.Services.clstesoreria msgtes;

        private System.Data.Odbc.OdbcConnection myConec01 = new System.Data.Odbc.OdbcConnection();
        private double tasa = 0;
        private string BancoConciliacion = "9999", TipoDConciliacion = "";
        private double NumDConciliacion = 0, ValorConci = 0;
        private char DebCred;
        private string EmpresaConciliaBanca = "N", ConsiBanca = "N";
        private bool GrabaConsiBanca = false;
        private double SecuenciaMovto = 0;
        private string _DetalleDocumento = "";
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();

        public cop_cuadredoc(System.Data.Odbc.OdbcConnection conexion)
        {
            msgsas = new ERP.Core.Compartido.Utilidades.Ayuda(this.Tag == null ? "" : this.Tag.ToString());
            msgtes = new ERP.Core.Tesoreria.Services.clstesoreria(this.Tag == null ? "" : this.Tag.ToString());
            InitializeComponent();
            this.myConec01 = conexion;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string DetalleDocumento
        {
            get { return _DetalleDocumento; }
            set { _DetalleDocumento = value; }
        }

        private void cmbGrabar_Click(object sender, EventArgs e)
        {
            bool ok;
            string stNit = "N", stcencos = "N";

            if (txtCredito.Text.Trim() == "" || !Information.IsNumeric(txtCredito.Text))
            {
                txtCredito.Text = "0";
            }
            if (txtDebito.Text.Trim() == "" || !Information.IsNumeric(txtDebito.Text))
            {
                txtDebito.Text = "0";
            }

            ok = ValidaCampos();

            if (ok == true)
            {
                return;
            }

            string _cuenta = txtCuentaCierre.Text;
            string _u = " ", _natura = "N", _nivel = "0", _aplicart = "N", _aplites = "Y";
            decimal _tasa = 0; string _tipoAux = "0"; int _estCta = 0; string _apliCnt = "N", _consiBanca = "N", _bancoConcilia = "9999";
            ok = msgcnt.BuscarCuenta(ref _cuenta, this.myConec01, ref stNit, ref stcencos,
                ref _natura, ref _u, ref _nivel, ref _aplicart, ref _aplites,
                ref _u, ref _tasa, ref _tipoAux, ref _estCta, ref _apliCnt, ref _consiBanca, ref _bancoConcilia);
            txtCuentaCierre.Text = _cuenta;

            if (stNit == "Y")
            {
                string nit = this.TxtCodigoter.Text;
                ok = Parcon.BuscarTercero(ref nit, this.myConec01);
                if (ok == false)
                {
                    MessageBox.Show("Nit no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtCodigoter.Text = "";
                    this.TxtCodigoter.Focus();
                    return;
                }
            }

            ok = ActulizaDocumento();
            initializeCampos();
            if (ok == true)
            {
                this.Dispose();
                this.Close();
            }
            else
            {
                this.txtCuentaCierre.Focus();
                return;
            }
        }

        private void initializeCampos()
        {
            this.txtCuentaCierre.Text = " ";
            this.LblCuenta.Text = " ";
            this.LblNombreNit.Text = " ";
            this.LblNomCencos.Text = " ";
        }

        private bool ValidaCampos()
        {
            if (this.txtCuentaCierre.Text.Trim() == "")
            {
                this.txtCuentaCierre.Focus();
                return true;
            }
            if (this.TxtCodigoter.Text.Trim() == "")
            {
                this.TxtCodigoter.Focus();
                return true;
            }
            if (this.Txtcencos.Text.Trim() == "")
            {
                this.Txtcencos.Focus();
                return true;
            }
            if (this.txtDebito.Text == "0" && this.txtCredito.Text == "0")
            {
                return true;
            }

            if (Convert.ToDouble(this.txtDebito.Text) > 0 && Convert.ToDouble(this.txtCredito.Text) > 0)
            {
                return true;
            }

            if (Convert.ToDouble(this.txtDebito.Text) > 0 || Convert.ToDouble(this.txtCredito.Text) > 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool ActulizaDocumento()
        {
            bool ok;
            string ApliTes = "N", factura = " ";
            double dif = 0;
            int TipoAux = 0;
            string ClaseDocAux = " ", NumDocAux = " ";
            DateTime FecVence = DateTime.Parse("1/1/1950");
            string Detallefact = " ";

            // BuscarCuenta with many ByRef params: p1=Cuenta, p2=mycocnect, p3=Tercero, p4=mane_cencos, p5=natura, p6=cencos,
            // p7=nivel, p8=Aplicart, p9=ApliTes, p10=nombre, p11=Tasa, p12=TipoAuxiliar, p13=estado, p14=ApliCnt, p15=ConsiBaca, p16=Banco_consibanca
            {
                string _cuenta = txtCuentaCierre.Text;
                string _p3 = "N", _p4 = "N", _p5 = "N", _p6 = "99999999";
                string _p7 = "0", _p8 = "N";
                string _p9 = ApliTes;
                string _p10 = " ";
                decimal _p11 = 0;
                string _p12 = " ";
                int _p13 = 0;
                string _p14 = "N";
                string _p15 = ConsiBanca;
                string _p16 = BancoConciliacion;
                ok = msgcnt.BuscarCuenta(ref _cuenta, this.myConec01, ref _p3, ref _p4, ref _p5, ref _p6,
                    ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16);
                ApliTes = _p9;
                TipoAux = Convert.ToInt32(_p12.Trim() == "" ? "0" : _p12.Trim());
                ConsiBanca = _p15;
                BancoConciliacion = _p16;
            }

            if (TipoAux > 0 || ApliTes == "Y")
            {
                // CargaDocAuxliliar: p1=nit, p2=cuenta, p3=Periodo, p4=myforma, p5=Debito, p6=Credito, p7=myconnect,
                // p8=ClaseAux(ByRef), p9=NumAuxiliar(ByRef), p10=FecVence(ByRef), p11=Detalle(ByRef), p12=CentroCosto(ByRef), p13=Agencia(ByRef)
                string _cencos = this.Txtcencos.Text;
                string _agencia = this.txtagencia.Text;
                msgcnt.CargaDocAuxliliar(this.TxtCodigoter.Text, this.txtCuentaCierre.Text,
                    this.DtpFecha.Value.ToString("yyyyMM"), this,
                    Convert.ToDouble(this.txtDebito.Text), Convert.ToDouble(this.txtCredito.Text), this.myConec01,
                    ref ClaseDocAux, ref NumDocAux, ref FecVence, ref Detallefact,
                    ref _cencos, ref _agencia);

                if (NumDocAux == "")
                {
                    return false;
                }

                ok = msgcnt.BuscaDocAuxiliar(this.DtpFecha.Value.ToString("yyyyMM"),
                    TipoAux.ToString(), this.txtCuentaCierre.Text, this.TxtCodigoter.Text,
                    this.txtagencia.Text, this.Txtcencos.Text, ClaseDocAux, NumDocAux, this.myConec01);

                if (ok == false)
                {
                    switch (TipoAux)
                    {
                        case 1:
                            if (Convert.ToInt64(this.txtCredito.Text) > 0)
                            {
                                MessageBox.Show("Documento auxiliar no se puede crear,  movimiento invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return false;
                            }
                            break;
                        case 2:
                            if (Convert.ToInt64(this.txtDebito.Text) > 0)
                            {
                                MessageBox.Show("Documento auxiliar no se puede crear,  movimiento invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return false;
                            }
                            break;
                    }
                }
            }

            if (ConsiBanca == "Y")
            {
                if (msgcnt.CargaTipodocConciliacion(this, ref TipoDConciliacion, ref NumDConciliacion) == true)
                {
                    GrabaConsiBanca = true;
                }
                else
                {
                    GrabaConsiBanca = false;
                    return false;
                }
            }

            if (Detallefact == null || Detallefact.Trim() == "")
            {
                Detallefact = DetalleDocumento;
            }

            double _credito = Convert.ToDouble(this.txtCredito.Text);
            msgcop.GrabaMovimiento(this.txtComprobante.Text, Convert.ToDouble(this.TxtConseCpte.Text),
                "99999999999999", 9999, 0, Convert.ToInt32(this.DtpFecha.Value.ToString("yyyyMM")),
                "2", this.DtpFecha.Value, Convert.ToDouble(this.txtDebito.Text), ref _credito,
                Detallefact, this.Tag.ToString().Trim(), this.myConec01, 9999,
                this.txtCuentaCierre.Text, this.TxtCodigoter.Text,
                ClaseDocAux + "-" + NumDocAux, "99999999999999", Clscartera.Cladesto.Todo, 0, true,
                ClaseDocAux, NumDocAux, Detallefact, FecVence, "9999", " ",
                "99999999999999", 999999, this.Txtcencos.Text, " ", 0, "9999",
                false, Convert.ToDouble(this.TxtBase.Text));

            if (GrabaConsiBanca == true)
            {
                if (NumDocAux == null || NumDocAux.Trim() == "")
                {
                    NumDocAux = "0";
                }

                if (msgcop.BuscarSecuenciaMovto(this.txtComprobante.Text.Trim(), Convert.ToDouble(this.TxtConseCpte.Text.Trim()),
                    this.txtCuentaCierre.Text.Trim(), "9999",
                    this.DtpFecha.Value.ToString("yyyyMM"), this.TxtCodigoter.Text.Trim(),
                    Txtcencos.Text, this.DtpFecha.Value, Detallefact,
                    NumDocAux, Convert.ToDouble(this.txtDebito.Text.Trim()),
                    Convert.ToDouble(this.txtCredito.Text.Trim()), this.Tag.ToString().Trim(),
                    ClaseDocAux, myConec01, ref SecuenciaMovto) == true)
                {
                    if (Convert.ToDouble(this.txtCredito.Text.Trim()) > 0)
                    {
                        ValorConci = Convert.ToDouble(this.txtCredito.Text.Trim());
                        DebCred = 'C';
                    }
                    else if (Convert.ToDouble(this.txtDebito.Text.Trim()) > 0)
                    {
                        ValorConci = Convert.ToDouble(this.txtDebito.Text.Trim());
                        DebCred = 'D';
                    }

                    msgcnt.GrabaDocumentoConciliacion(this.txtCuentaCierre.Text.Trim(), BancoConciliacion,
                        Convert.ToInt32(this.DtpFecha.Value.ToString("yyyyMM")), TipoDConciliacion,
                        NumDConciliacion, Detallefact,
                        ValorConci, DebCred, SecuenciaMovto, DtpFecha.Value, myConec01,
                        'N', 'N', "cop");
                }
            }

            string _comprobante = this.txtComprobante.Text;
            double _consecutivo = Convert.ToDouble(this.TxtConseCpte.Text);
            double _debitos = Convert.ToDouble(this.txtDebitos.Text);
            double _creditos = Convert.ToDouble(this.TxtCreditos.Text);
            double _dif = dif;
            string _bc5 = " ", _bc6 = " ", _bc9 = "N", _bc10 = "N";
            msgcop.BuscaComprobante(ref _comprobante, ref _consecutivo, false, myConec01,
                ref _bc5, ref _bc6, ref _debitos, ref _creditos, ref _bc9, ref _bc10, ref _dif);
            this.txtDebitos.Text = _debitos.ToString();
            this.TxtCreditos.Text = _creditos.ToString();
            dif = _dif;

            this.txtDebito.Text = "0";
            this.txtCredito.Text = "0";
            if (dif > 0)
            {
                this.txtDebito.Text = dif.ToString();
            }
            else
            {
                this.txtCredito.Text = (dif * -1).ToString();
            }

            if (dif == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void cop_cuadredoc_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                this.Dispose();
            }
        }

        private void cop_cuadredoc_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            this.TxtDiferencia.Text = (Convert.ToDouble(this.txtDebitos.Text) - Convert.ToDouble(this.TxtCreditos.Text)).ToString();
            this.txtDebito.Text = "0";
            this.txtCredito.Text = "0";
            this.LblCuenta.Text = " ";
            this.LblNombreNit.Text = " ";
            if (Convert.ToDouble(this.TxtDiferencia.Text) < 0)
            {
                this.txtCredito.Text = "0";
                this.txtDebito.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtDiferencia.Text) * -1, 2);
            }
            else
            {
                this.txtDebito.Text = "0";
                this.txtCredito.Text = Strings.FormatNumber(this.TxtDiferencia.Text, 2);
            }
        }

        private void HelpTerceros_Click(object sender, EventArgs e)
        {
            this.TxtCodigoter.Text = Parcon.HelpNits(this.myConec01, this);
            this.TxtCodigoter.Focus();
        }

        private void txtCuentaCierre_LostFocus(object sender, EventArgs e)
        {
            bool ok, CampoError;
            string stCar = "N", stTes = "N", stNit = "N", stcencos = "N";
            string cencos = "99999999";
            string nivel = "0";
            int EstCta = 0;
            tasa = 0;

            if (this.txtCuentaCierre.Text.Trim() != "")
            {
                this.LblCuenta.Text = " ";
                string _cuenta = txtCuentaCierre.Text;
                string _nombre = " ";
                decimal _tasa = 0;
                string _tipoAux = " ";
                string _apliCnt = "N", _consiBanca = "N", _bancoCon = "9999";
                string _natura = " ";
                ok = msgcnt.BuscarCuenta(ref _cuenta, this.myConec01, ref stNit, ref stcencos,
                    ref _natura, ref cencos, ref nivel, ref stCar, ref stTes, ref _nombre, ref _tasa,
                    ref _tipoAux, ref EstCta, ref _apliCnt, ref _consiBanca, ref _bancoCon);
                this.LblCuenta.Text = _nombre;
                tasa = (double)_tasa;

                if (ok == true)
                {
                    CampoError = ValidaCamposCuenta(stNit, stcencos, stCar, Convert.ToInt32(nivel));
                    if (EstCta == 1)
                    {
                        MessageBox.Show("Cuenta esta inactiva !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.txtCuentaCierre.Text = "";
                        txtCuentaCierre.Focus();
                        return;
                    }

                    if (CampoError == true)
                    {
                        return;
                    }

                    this.TxtSaldoCuenta.Text = msgcnt.BuscarSaldoCuenta(txtCuentaCierre.Text,
                        this.DtpFecha.Value.ToString("yyyyMM"), this.txtagencia.Text,
                        this.Txtcencos.Text, this.myConec01).ToString();

                    if (tasa != 0)
                    {
                        if (Convert.ToDouble(this.txtDebito.Text) > 0)
                        {
                            this.TxtBase.Text = (Convert.ToDouble(Strings.FormatNumber(Convert.ToDouble(this.txtDebito.Text) / (tasa / 100), 2)) * -1).ToString();
                        }
                        else
                        {
                            this.TxtBase.Text = Strings.FormatNumber(Convert.ToDouble(this.txtCredito.Text) / (tasa / 100), 2);
                        }
                        this.TxtBase.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtBase.Text), 2);
                    }
                    else
                    {
                        this.TxtBase.Text = "0";
                    }
                }
                else
                {
                    MessageBox.Show("Cuenta contable no existe o no es de movimiento", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.txtCuentaCierre.Text = "";
                    txtCuentaCierre.Focus();
                    return;
                }
            }
        }

        private bool ValidaCamposCuenta(string stnit, string stcencos, string stCar, int Nivel)
        {
            if (stnit == "Y")
            {
                this.TxtCodigoter.Enabled = true;
                this.TxtCodigoter.Text = "0";
            }
            else
            {
                this.TxtCodigoter.Enabled = false;
                this.TxtCodigoter.Text = "99999999999999";
            }

            if (stcencos == "Y")
            {
                this.Txtcencos.Enabled = true;
                this.Txtcencos.Text = "";
            }
            else
            {
                this.Txtcencos.Enabled = false;
                this.Txtcencos.Text = "99999999";
            }

            if (stCar != "Y")
            {
                MessageBox.Show("Cuenta no es de cartera, modificar catalogo", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.txtCuentaCierre.Text = "";
                txtCuentaCierre.Focus();
                return false;
            }

            if (Nivel != 6)
            {
                MessageBox.Show("Nivel de la Cuenta no permitido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.txtCuentaCierre.Text = "";
                txtCuentaCierre.Focus();
                return false;
            }

            return false;
        }

        private void TxtCodigoter_LostFocus(object sender, EventArgs e)
        {
            bool ok;
            int EstTer = 0;
            if (this.TxtCodigoter.Text != "" && this.TxtCodigoter.Text != "0")
            {
                this.LblNombreNit.Text = " ";
                string nit = this.TxtCodigoter.Text;
                string _nombre = this.LblNombreNit.Text;
                // BuscarTercero: p1=nit(ref), p2=myconect, p3=nombre(ref), p4=Navegar, p5=Tipo,
                // p6=RazonSocial(ref), p7=TipoNit(ref), p8=DigiCheq(ref), p9=Expedida(ref),
                // p10=Direccion(ref), p11=Telefono(ref), p12=Ciudad(ref), p13=OrigenDatos(ref),
                // p14=PagaGrabamen(ref), p15=TipoPersona(ref), p16=Estado(ref)
                string _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "", _p11 = "", _p12 = "", _p13 = "CNT";
                bool _p14 = true;
                string _p15 = "";
                decimal _tasaIca = 0;
                ok = Parcon.BuscarTercero(ref nit, myConec01, ref _nombre,
                    ERP.Core.Contabilidad.Models.ParamCnt.Navega.Ninguno, ERP.Core.Contabilidad.Models.ParamCnt.TipoTercero.Tercero,
                    ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13,
                    ref _p14, ref _p15, ref EstTer, ref _tasaIca);
                this.LblNombreNit.Text = _nombre;

                if (ok == true)
                {
                    if (EstTer == 1)
                    {
                        MessageBox.Show("Tercero esta inactivo !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.TxtCodigoter.Text = "";
                        this.TxtCodigoter.Focus();
                        return;
                    }

                    this.TxtSaldoTercero.Text = msgcnt.BuscarSaldoTecero(this.txtCuentaCierre.Text, nit,
                        this.DtpFecha.Value.ToString("yyyyMM"), myConec01).ToString();
                }
                else
                {
                    MessageBox.Show("Nit no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtCodigoter.Text = "";
                    this.TxtCodigoter.Focus();
                }
            }
        }

        private void help_cuentacon_Click(object sender, EventArgs e)
        {
            this.txtCuentaCierre.Text = msgsas.CargaAyuda("cnt_maecuen", "cuenta", "nombre", "", myConec01, this, "", "", "", " nivel='6' ");
            this.txtCuentaCierre.Focus();
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
                this.Txtcencos.Text = ("00000000" + Txtcencos.Text).Substring(("00000000" + Txtcencos.Text).Length - 8);
                if (this.Txtcencos.Text != "")
                {
                    ok = msgcnt.BuscarCencos(this.Txtcencos.Text, this.myConec01);
                    if (ok == true)
                    {
                        string _nomCencos = this.LblNomCencos.Text;
                        string _nomres = " ";
                        string _cencos = this.Txtcencos.Text;
                        msgcnt.BuscarCencos(ref _cencos, myConec01, ref _nomCencos, ref _nomres);
                        this.LblNomCencos.Text = _nomCencos;
                    }
                    else
                    {
                        MessageBox.Show("Cencosto no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Txtcencos.Text = "";
                        this.Txtcencos.Focus();
                    }
                }
            }
        }

        private void helpcencos_Click(object sender, EventArgs e)
        {
            this.Txtcencos.Text = Parcon.HelpCencos(this.myConec01, this);
            this.Txtcencos.Focus();
        }

        private void HelpAgencias_Click(object sender, EventArgs e)
        {
            this.txtagencia.Text = paramCop.HelpAgencias(this.myConec01, this);
            this.txtagencia.Focus();
        }

        private void txtCredito_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.txtCredito.Text) == false)
            {
                this.txtCredito.Text = "0";
            }
            else if (tasa > 0 && Convert.ToDouble(this.txtCredito.Text) != 0)
            {
                this.TxtBase.Text = Strings.FormatNumber(Convert.ToDouble(this.txtCredito.Text) / (tasa / 100), 2);
                this.TxtBase.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtBase.Text), 2);
                this.txtCredito.Text = Strings.FormatNumber(Convert.ToDouble(this.txtCredito.Text), 2);
            }
        }

        private void txtDebito_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.txtDebito.Text) == false)
            {
                this.txtDebito.Text = "0";
            }
            else if (tasa > 0 && Convert.ToDouble(this.txtDebito.Text) != 0)
            {
                this.TxtBase.Text = (Convert.ToDouble(Strings.FormatNumber(Convert.ToDouble(this.txtDebito.Text) / (tasa / 100), 2)) * -1).ToString();
                this.TxtBase.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtBase.Text), 2);
                this.txtDebito.Text = Strings.FormatNumber(Convert.ToDouble(this.txtDebito.Text), 2);
            }
        }
    }
}
