using ERP.Core.Tesoreria.Services;
using System;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Tesoreria.Forms
{
    // Traducción de: Public Class FrmPagoFact (forms/FrmPagoFact.vb)
    public partial class FrmPagoFact : Form
    {
        //private ERP.Core.Compartido.Utilidades.Ayuda msgSasAyu = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        //private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        //private clstesoreria msgtes = new clstesoreria();
        //private ERP.Core.Contabilidad.Reportes.ImpreDoc msgImpCnt = new ERP.Core.Contabilidad.Reportes.ImpreDoc("admin");
        //private ERP.Core.CarteraFinanciera.Models.ParamCop msparcop1 = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        //private ERP.Core.Compartido.Utilidades.Ayuda msgsas = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        //private ERP.Core.Contabilidad.Models.ParamCnt msgparcnt = new ERP.Core.Contabilidad.Models.ParamCnt();
        //private ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        //private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        //private OdbcConnection Myconnect1 = new OdbcConnection();
        //private ERP.Core.Compartido.Datos.ClsConect conifconect = new ERP.Core.Compartido.Datos.ClsConect();

        //public string StCedula;
        //public string[] Facturas;
        //public string[] Cuentas;
        //public string[] Terceros;
        //public string Usuario;
        //private double Grabamen = 0;
        //private double vlr4x1000 = 0;
        //public bool CambiaTercero = false;
        //private bool ok;
        //private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        //private string ControlConse = "N";

        //public FrmPagoFact(OdbcConnection conexion)
        //{
        //    InitializeComponent();
        //    this.Myconnect1 = conexion;
        //}

        //private void FrmPagoFact_Load(object sender, EventArgs e)
        //{
        //    conifconect.MyOdbcConect(ref varini);
        //    if (CambiaTercero)
        //    {
        //        LblCedula.Enabled = true;
        //        HelpNit.Visible = true;
        //        LblCedula.BackColor = SystemColors.Window;
        //        LblNombre.Text = "#";
        //        AplicarBuscarTerceroNombre(LblCedula.Text);
        //        LblCedula.Focus();
        //    }
        //    else
        //    {
        //        LblCedula.Enabled = false;
        //        HelpNit.Visible = false;
        //        LblCedula.BackColor = SystemColors.Control;
        //        AplicarBuscarTerceroNombre(LblCedula.Text);
        //    }

        //    if (LblForPago.Text.Trim() == "DF" && int.Parse(LblNumFacturas.Text) > 1)
        //    {
        //        TxtValGirar.Enabled = false;
        //    }
        //    else
        //    {
        //        if (TxtValGirar.Tag?.ToString() != "Neg")
        //            TxtValGirar.Enabled = true;
        //    }
        //    CenterToScreen();
        //}

        //private void CmdSalir_Click(object sender, EventArgs e)
        //{
        //    this.Close();
        //    this.Dispose();
        //}

        //private void HelpCpte_Click(object sender, EventArgs e)
        //{
        //    TxtCpte.Text = msgsys.HelpComprobantes(Myconnect1, this);
        //    TxtCpte.Focus();
        //}

        //private void TxtCpte_LostFocus(object sender, EventArgs e)
        //{
        //    if (!string.IsNullOrEmpty(TxtCpte.Text.Trim()))
        //    {
        //        if (Myconnect1.State == System.Data.ConnectionState.Open)
        //            BuscaComprobante();
        //    }
        //}

        //private void BuscaComprobante()
        //{
        //    string TipoDoc = "NO";
        //    TxtConseCpte.Text = "0";
        //    string _u = " ";

        //    string _cpte = TxtCpte.Text;
        //    double _conse = 0;
        //    string _controlConse = ControlConse;
        //    string _tipoDoc = TipoDoc;

        //    bool OK = msgcnt.BuscaComprobante(ref _cpte, ref _conse, false, Myconnect1,
        //        ref _controlConse, ref _tipoDoc,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u, ref _u);

        //    TxtCpte.Text = _cpte;
        //    TxtConseCpte.Text = _conse.ToString();
        //    ControlConse = _controlConse;
        //    TipoDoc = _tipoDoc;

        //    if (!OK)
        //    {
        //        MessageBox.Show("Codigo comprobante no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        TxtConseCpte.ResetText();
        //        TxtCpte.ResetText();
        //        TxtCpte.Focus();
        //        return;
        //    }

        //    switch (LblForPago.Text.Trim())
        //    {
        //        case "CH":
        //            if (TipoDoc.Trim() != "CP")
        //            {
        //                MessageBox.Show("Tipo de documento no permitido, debe ser un comprobante de pago", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //                TxtConseCpte.ResetText();
        //                TxtCpte.ResetText();
        //                TxtCpte.Focus();
        //                return;
        //            }
        //            break;
        //        case "DF":
        //            if (TipoDoc.Trim() != "TR")
        //            {
        //                MessageBox.Show("Tipo de documento no permitido, debe ser un comprobante de traslado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //                TxtConseCpte.ResetText();
        //                TxtCpte.ResetText();
        //                TxtCpte.Focus();
        //                return;
        //            }
        //            break;
        //    }

        //    if (ControlConse == "Y")
        //    {
        //        TxtConseCpte.Text = "0";
        //        TxtConseCpte.Enabled = false;
        //    }
        //    else
        //    {
        //        TxtConseCpte.Enabled = true;
        //        TxtConseCpte.Focus();
        //    }
        //}

        //private void CmbAceptar_Click(object sender, EventArgs e)
        //{
        //    CmbAceptar.Enabled = false;
        //    CmdSalir.Enabled = false;

        //    ok = ValidaCampos();
        //    if (ok)
        //    {
        //        if (this.Tag?.ToString() == "1")
        //            GrabaMovtobanco();
        //        else
        //            GrabaDocumento();
        //    }
        //    else
        //    {
        //        CmbAceptar.Enabled = true;
        //        CmdSalir.Enabled = true;
        //        return;
        //    }

        //    CmbAceptar.Enabled = true;
        //    CmdSalir.Enabled = true;
        //    this.DialogResult = DialogResult.Yes;
        //    this.Close();
        //}

        //private bool ValidaCampos()
        //{
        //    string Cuentabanco = " ";
        //    string _u = " ";

        //    // Verificar que tercero existe
        //    string _nit = LblCedula.Text;
        //    ok = msgcnt.BuscarTercero(LblCedula.Text, Myconnect1,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u);
        //    if (!ok)
        //    {
        //        MessageBox.Show("El tercero no existe.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        LblCedula.Text = "";
        //        LblCedula.Focus();
        //        return false;
        //    }

        //    // Verificar que comprobante existe (consecutivo=0)
        //    string _cpte1 = TxtCpte.Text; double _c1 = 0;
        //    ok = msgcnt.BuscaComprobante(ref _cpte1, ref _c1, false, Myconnect1,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u, ref _u);
        //    if (!ok)
        //    {
        //        TxtCpte.Text = "";
        //        MessageBox.Show("El comprobante no existe.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        TxtCpte.Text = "";
        //        TxtCpte.Focus();
        //        return false;
        //    }

        //    if (!Information.IsNumeric(TxtConseCpte.Text))
        //        return false;

        //    if (ControlConse == "N")
        //    {
        //        string _cpte2 = TxtCpte.Text; double _c2 = Convert.ToDouble(TxtConseCpte.Text);
        //        ok = msgcnt.BuscaComprobante(ref _cpte2, ref _c2, false, Myconnect1,
        //            ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //            ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //            ref _u, ref _u, ref _u, ref _u, ref _u);
        //        if (ok)
        //        {
        //            MessageBox.Show("Documento ya existe.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //            return false;
        //        }
        //    }

        //    // BuscaBanco p11=Cuentabanco
        //    string _b = Txtbanco.Text;
        //    ok = msparcop1.BuscaBanco(ref _b, Myconnect1,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref Cuentabanco, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u);
        //    if (!ok)
        //    {
        //        MessageBox.Show("Banco no esta creado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        return false;
        //    }

        //    // BuscarCuenta p7=nivel
        //    string _cBan = Cuentabanco, _nivel = "0";
        //    msgcnt.BuscarCuenta(ref _cBan, Myconnect1,
        //        ref _u, ref _u, ref _u, ref _u, ref _nivel, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u);
        //    int NIvel = int.TryParse(_nivel, out int nv) ? nv : 0;

        //    if (NIvel != 6)
        //    {
        //        MessageBox.Show("Nivel de cuenta del banco invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        return false;
        //    }

        //    return true;
        //}

        //private void GrabaMovtobanco()
        //{
        //    ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcopLocal = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        //    string Cuentabanco = " ";
        //    string _u = " ";

        //    // BuscaBanco p11=Cuentabanco
        //    string _b = Txtbanco.Text;
        //    msparcop1.BuscaBanco(ref _b, Myconnect1,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref Cuentabanco, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u);

        //    // msgcop.GrabaMovimiento: calls using ClsDepositos.cs pattern
        //    // p10=credito is ByVal in DLL (based on ClsDepositos.cs usage without ref)
        //    double credito = Convert.ToDouble(TxtValGirar.Text);
        //    msgcopLocal.GrabaMovimiento(
        //        TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text), "99999999999999",
        //        9999, 0, DtpFecha.Value.ToString("yyyyMM"), "2", DtpFecha.Value, 0, credito, " ",
        //        msgcopLocal.varini.pstUsuario, Myconnect1,
        //        ref _u, Cuentabanco, LblCedula.Text,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        Txtbanco.Text, txtNumCheque.Text);

        //    msgcopLocal.GrabaDatosDocumento(
        //        TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //        Myconnect1, Txtbanco.Text, txtNumCheque.Text, LblCedula.Text);

        //    msparcop1.GrabaNumeroCheque(Txtbanco.Text, txtNumCheque.Text, Myconnect1);
        //}

        //private void GrabaDocumento()
        //{
        //    int i = 0;
        //    double ValFact = 0;
        //    string CuentaFact = "999999999999", Detalle = " ";
        //    double TotalD = 0;
        //    string Cpto = " ", ConseCpto = " ";
        //    DateTime Fecfac = DateTime.Now, FecVence = DateTime.Now;
        //    string Estado = "P", TipoDoc = "NO";
        //    double TotPagar;
        //    string ClaseDocAux = "DC", NumDocAux = " ";
        //    string Banco = "9999";
        //    double Dif = 0;
        //    string Cencosto = "99999999", AplCenc = "N";
        //    string Mov4xmil = "99", Cuenta4xmil = " ";
        //    bool CobraGrabaNit = true;
        //    int CopiasImp = 0;
        //    string NumCheque = "";
        //    string ForPag = "CH";
        //    int FormaComision = 0;
        //    double ValComision = 0;
        //    string CuentaComision = "999999999999";
        //    char CobraComision = 'N';
        //    double ValorParComision = 0;
        //    DateTime FecPro = DateTime.Now, FechaMovimto = DateTime.Now;
        //    double Debito = 0, Credito = 0;
        //    string[] VlrDispersion = new string[Facturas.GetUpperBound(0) + 1];
        //    string[] TesoComision = new string[Facturas.GetUpperBound(0) + 1];
        //    string[] TerceroComision = new string[Facturas.GetUpperBound(0) + 1];
        //    int Contador;
        //    bool AlpComision = false;
        //    string _u = " ";

        //    TotPagar = Convert.ToDouble(TxtValGirar.Text);

        //    switch (LblForPago.Text.Trim())
        //    {
        //        case "CH": NumCheque = txtNumCheque.Text; ForPag = "CH"; break;
        //        case "DF": NumCheque = "DF"; ForPag = "DF"; break;
        //    }

        //    for (i = 0; i <= Facturas.GetUpperBound(0) - 1; i++)
        //    {
        //        CuentaFact = Cuentas[i];
        //        string _valor = "0", _tipoDoc2 = "NO", _formaPago = "", _tesoComStr = "";

        //        // BuscaNumeroFactura full overload (19 params)
        //        msgtes.BuscaNumeroFactura(Facturas[i], Terceros[i], ref CuentaFact, Myconnect1,
        //            ref _valor, ref Cpto, ref ConseCpto, ref Fecfac, ref FecVence, ref FecPro,
        //            ref Estado, ref Banco, ref Detalle, ref _tipoDoc2, ref ClaseDocAux, ref NumDocAux,
        //            ref Cencosto, ref _formaPago, ref _tesoComStr);
        //        TesoComision[i] = _tesoComStr;

        //        ValFact = msgcnt.BuscarSaldoDocAuxiliar(CuentaFact, 2, Terceros[i],
        //            DtpFecha.Value.ToString("yyyyMM"), Facturas[i], Myconnect1);
        //        Credito = 0; Debito = 0;

        //        if (TxtValGirar.Tag?.ToString() == "Neg")
        //        {
        //            if (ValFact < 0) { Debito = ValFact * -1; ValFact = ValFact * -1; }
        //            else { Credito = ValFact; ValFact = ValFact * -1; }
        //        }
        //        else
        //        {
        //            if (ValFact < 0) ValFact = ValFact * -1;
        //            if (TotPagar <= ValFact) ValFact = TotPagar;
        //            if (ValFact > TotPagar) ValFact = TotPagar;
        //            Debito = ValFact; Credito = 0;
        //        }

        //        if (TotPagar <= 0) break;

        //        FechaMovimto = FecPro > DtpFecha.Value ? FecPro : DtpFecha.Value;

        //        if (TxtDetalle.Text.Trim() == "") TxtDetalle.Text = Detalle;

        //        // BuscaComprobante (ActualizaConse=true)
        //        string _cpteGD = TxtCpte.Text; double _conseGD = Convert.ToDouble(TxtConseCpte.Text);
        //        string _tdGD = TipoDoc;
        //        msgcnt.BuscaComprobante(ref _cpteGD, ref _conseGD, true, Myconnect1,
        //            ref _u, ref _tdGD, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //            ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //            ref _u, ref _u, ref _u, ref _u, ref _u);
        //        TipoDoc = _tdGD;

        //        msgcnt.GrabaMovimiento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //            CuentaFact, "9999", FechaMovimto.ToString("yyyyMM"),
        //            Terceros[i], FechaMovimto, TxtDetalle.Text.Trim(), NumDocAux,
        //            Debito, Credito, 0, Usuario, Myconnect1,
        //            Txtbanco.Text, Facturas[i], Terceros[i], Cencosto, TxtDetalle.Text.Trim(),
        //            null, "cont", NumCheque, ClaseDocAux, FecVence, TxtDetalle.Text.Trim(),
        //            null, null, ForPag);

        //        if (TxtValGirar.Tag?.ToString() != "Neg") TotPagar = TotPagar - ValFact;

        //        TotalD = TotalD + ValFact;

        //        if (ForPag == "DF")
        //            VlrDispersion[i] = ValFact < 0 ? "0" : ValFact.ToString();

        //        ValFact = msgcnt.BuscarSaldoDocAuxiliar(CuentaFact, 2, Terceros[i],
        //            DtpFecha.Value.ToString("yyyyMM"), Facturas[i], Myconnect1);

        //        if (ValFact == 0)
        //            msgtes.GrabaPagoFactura(Facturas[i], Terceros[i], Txtbanco.Text, "C",
        //                TxtCpte.Text, TxtConseCpte.Text, txtNumCheque.Text,
        //                Convert.ToDouble(TxtValGirar.Text), CuentaFact, Myconnect1);
        //        else
        //            msgtes.GrabaPagoFactura(Facturas[i], Terceros[i], Txtbanco.Text, "P",
        //                TxtCpte.Text, TxtConseCpte.Text, txtNumCheque.Text,
        //                Convert.ToDouble(TxtValGirar.Text), CuentaFact, Myconnect1);
        //    }

        //    if (TotalD > 0)
        //    {
        //        // BuscaBanco para obtener Grabamen, CopiasImp, CobraComision, CuentaComision, FormaComision, ValorParComision
        //        string _bBan = Banco, _cuentabanco2 = "999999999999";
        //        string _grabamen = Grabamen.ToString(), _copias = CopiasImp.ToString();
        //        string _cobraC = CobraComision.ToString(), _cuentaC = CuentaComision;
        //        string _formaC = FormaComision.ToString(), _valorPC = ValorParComision.ToString();
        //        msparcop1.BuscaBanco(ref _bBan, Myconnect1,
        //            ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //            ref _u, ref _cuentabanco2, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
        //            ref _u, ref _grabamen, ref _copias, ref _u, ref _cobraC, ref _cuentaC,
        //            ref _formaC, ref _valorPC, ref _u, ref _u);
        //        Grabamen = double.TryParse(_grabamen, out double gv) ? gv : 0;
        //        CopiasImp = int.TryParse(_copias, out int cov) ? cov : 0;
        //        CobraComision = string.IsNullOrEmpty(_cobraC) ? 'N' : _cobraC[0];
        //        CuentaComision = _cuentaC;
        //        FormaComision = int.TryParse(_formaC, out int fcv) ? fcv : 0;
        //        ValorParComision = double.TryParse(_valorPC, out double vpv) ? vpv : 0;

        //        if (ForPag == "CH")
        //            msparcop1.GrabaNumeroCheque(Banco, txtNumCheque.Text, Myconnect1);

        //        Mov4xmil = BuscarCompaniaCpto4Mil(varini.sptCodEmpr, Myconnect1);

        //        // BuscaTipoMovto p7=Cuenta4xmil
        //        string _mov = Mov4xmil;
        //        msgcop.BuscaTipoMovto(ref _mov, Myconnect1,
        //            ref _u, ref _u, ref _u, ref _u, ref Cuenta4xmil);

        //        switch (ForPag)
        //        {
        //            case "CH":
        //                // msgparcnt.BuscarTercero p14=CobraGrabaNit
        //                string _nitCH = LblCedula.Text;
        //                ObtenerCobraGrabamen(ref _nitCH, ref CobraGrabaNit);

        //                // BuscarCuenta p4=AplCenc (mane_cencos)
        //                string _cBanCH = _cuentabanco2;
        //                msgcnt.BuscarCuenta(ref _cBanCH, Myconnect1,
        //                    ref _u, ref AplCenc, ref _u, ref _u, ref _u, ref _u, ref _u,
        //                    ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u);

        //                if (AplCenc == "Y")
        //                {
        //                    if (Grabamen != 0 && CobraGrabaNit)
        //                        msgcnt.Graba4xmilCencos(TxtCpte.Text, TxtConseCpte.Text,
        //                            Cuenta4xmil, Myconnect1, "9999", FechaMovimto.ToString("yyyyMM"),
        //                            LblCedula.Text, FechaMovimto, TxtDetalle.Text.Trim(),
        //                            Txtbanco.Text, Usuario, Grabamen);
        //                    msgcnt.GrabaContraCencos(TxtCpte.Text, TxtConseCpte.Text,
        //                        _cBanCH, Myconnect1, "9999", FechaMovimto.ToString("yyyyMM"),
        //                        LblCedula.Text, FechaMovimto, TxtDetalle.Text.Trim(),
        //                        Txtbanco.Text, Usuario, ForPag);
        //                }
        //                else
        //                {
        //                    if (TesoComision[0] == "Y")
        //                    {
        //                        if (CobraComision == 'N')
        //                        {
        //                            MessageBox.Show("El banco no esta parametrizado para cobrar comision", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        //                            return;
        //                        }
        //                        if (FormaComision == 1)
        //                        {
        //                            if (ValorParComision <= 0 || ValorParComision > 100)
        //                            {
        //                                MessageBox.Show("El porcentaje de comision no es valido --> " + ValComision, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        //                                ValComision = 0; return;
        //                            }
        //                        }
        //                        else ValComision = ValorParComision;

        //                        if (TotalD < ValComision)
        //                        {
        //                            MessageBox.Show("El Valor a cobrar en comision es mayor al valor de la factura", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        //                            return;
        //                        }
        //                    }

        //                    if (Grabamen != 0 && CobraGrabaNit)
        //                        vlr4x1000 = Math.Round(TotalD * (Grabamen / 100));
        //                    TotalD = TotalD - vlr4x1000;

        //                    if (vlr4x1000 > 0)
        //                        msgcnt.GrabaMovimiento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //                            Cuenta4xmil, "9999", FechaMovimto.ToString("yyyyMM"),
        //                            LblCedula.Text, FechaMovimto, TxtDetalle.Text.Trim(), " ",
        //                            0, vlr4x1000, 0, Usuario, Myconnect1);

        //                    AlpComision = false;
        //                    if (TesoComision[0] == "Y" && CobraComision == 'Y')
        //                    {
        //                        if (FormaComision == 1) ValComision = TotalD * (ValorParComision / 100);
        //                        else ValComision = ValorParComision;

        //                        if (TotalD >= ValComision)
        //                        {
        //                            TotalD = TotalD - ValComision;
        //                            msgcnt.GrabaMovimiento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //                                CuentaComision, "9999", FechaMovimto.ToString("yyyyMM"),
        //                                Terceros[i], FechaMovimto, "Descuento por comision", NumDocAux,
        //                                0, ValComision, 0, Usuario, Myconnect1,
        //                                Txtbanco.Text, Facturas[i], Terceros[i], Cencosto,
        //                                TxtDetalle.Text.Trim(), null, "cont", NumCheque,
        //                                ClaseDocAux, FecVence, TxtDetalle.Text.Trim(), null, null, ForPag);
        //                            AlpComision = true;
        //                        }
        //                    }

        //                    msgcnt.GrabaMovimiento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //                        _cBanCH, "9999", FechaMovimto.ToString("yyyyMM"),
        //                        LblCedula.Text, FechaMovimto, TxtDetalle.Text.Trim(), " ",
        //                        0, TotalD, 0, Usuario, Myconnect1,
        //                        Txtbanco.Text, null, null, null, null, null, null,
        //                        txtNumCheque.Text, null, null, null, null, null, ForPag);
        //                }
        //                break;

        //            case "DF":
        //                for (i = 0; i <= Terceros.GetUpperBound(0) - 1; i++)
        //                {
        //                    string _nitDF = Terceros[i];
        //                    ObtenerCobraGrabamen(ref _nitDF, ref CobraGrabaNit);

        //                    if (TesoComision[i] == "Y")
        //                    {
        //                        if (CobraComision == 'N')
        //                        {
        //                            MessageBox.Show("El banco no esta parametrizado para cobrar comision", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        //                            return;
        //                        }
        //                        if (FormaComision == 1)
        //                        {
        //                            if (ValorParComision <= 0 || ValorParComision > 100)
        //                            {
        //                                MessageBox.Show("El porcentaje de comision no es valido --> " + ValComision, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        //                                ValComision = 0; return;
        //                            }
        //                        }
        //                        else ValComision = ValorParComision;

        //                        if (TotalD < ValComision)
        //                        {
        //                            MessageBox.Show("El Valor a cobrar en comision es mayor al valor de la factura", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        //                            return;
        //                        }
        //                    }

        //                    vlr4x1000 = 0;
        //                    if (Grabamen != 0 && CobraGrabaNit)
        //                    {
        //                        double vlrDisp = double.TryParse(VlrDispersion[i], out double vd) ? vd : 0;
        //                        vlr4x1000 = Math.Round(vlrDisp * (Grabamen / 100));
        //                    }
        //                    TotalD = TotalD - vlr4x1000;

        //                    string _cBanDF = _cuentabanco2;
        //                    msgcnt.BuscarCuenta(ref _cBanDF, Myconnect1,
        //                        ref _u, ref AplCenc, ref _u, ref _u, ref _u, ref _u, ref _u,
        //                        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u);

        //                    if (vlr4x1000 > 0)
        //                        msgcnt.GrabaMovimiento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //                            Cuenta4xmil, "9999", FechaMovimto.ToString("yyyyMM"),
        //                            Terceros[i], FechaMovimto, TxtDetalle.Text.Trim(), " ",
        //                            0, vlr4x1000, 0, Usuario, Myconnect1);

        //                    if (ForPag == "DF" && TesoComision[i] == "Y" && CobraComision == 'Y')
        //                    {
        //                        for (Contador = 0; Contador <= TerceroComision.Length - 1; Contador++)
        //                        {
        //                            if (Terceros[i] == TerceroComision[Contador]) break;
        //                            else if (TerceroComision[Contador] == null)
        //                            { TerceroComision[Contador] = Terceros[i]; break; }
        //                        }

        //                        ValComision = 0; AlpComision = false;
        //                        if (FormaComision == 1)
        //                        {
        //                            double vlrDisp2 = double.TryParse(VlrDispersion[i], out double vd2) ? vd2 : 0;
        //                            ValComision = vlrDisp2 * (ValorParComision / 100);
        //                        }
        //                        else ValComision = ValorParComision;

        //                        if (TotalD >= ValComision)
        //                        {
        //                            TotalD = TotalD - ValComision;
        //                            msgcnt.GrabaMovimiento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //                                CuentaComision, "9999", FechaMovimto.ToString("yyyyMM"),
        //                                Terceros[i], FechaMovimto, "Descuento por comision", NumDocAux,
        //                                0, ValComision, 0, Usuario, Myconnect1,
        //                                Txtbanco.Text, Facturas[i], Terceros[i], Cencosto,
        //                                TxtDetalle.Text.Trim(), null, "cont", NumCheque,
        //                                ClaseDocAux, FecVence, TxtDetalle.Text.Trim(), null, null, ForPag);
        //                            AlpComision = true;
        //                        }
        //                    }
        //                }

        //                if (AplCenc == "Y")
        //                {
        //                    msgcnt.GrabaContraCencos(TxtCpte.Text, TxtConseCpte.Text,
        //                        _cuentabanco2, Myconnect1, "9999",
        //                        FechaMovimto.ToString("yyyyMM"), Terceros[0], FechaMovimto,
        //                        TxtDetalle.Text.Trim(), Txtbanco.Text, Usuario, ForPag);
        //                }
        //                else
        //                {
        //                    if (AlpComision)
        //                        msgcnt.GrabaMovimiento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text),
        //                            _cuentabanco2, "9999", FechaMovimto.ToString("yyyyMM"),
        //                            Terceros[0], FechaMovimto, TxtDetalle.Text.Trim(), " ",
        //                            0, TotalD, 0, Usuario, Myconnect1,
        //                            Txtbanco.Text, null, null, null, null, null, null,
        //                            txtNumCheque.Text, null, null, null, null, null, ForPag);
        //                    else
        //                        msgcnt.GrabaContraDispersionFondos(TxtCpte.Text, TxtConseCpte.Text,
        //                            _cuentabanco2, Myconnect1, "9999",
        //                            FechaMovimto.ToString("yyyyMM"), Terceros[0], FechaMovimto,
        //                            TxtDetalle.Text.Trim(), Txtbanco.Text, Usuario);
        //                }
        //                break;
        //        }
        //    }

        //    // BuscaComprobante final (p11=Dif)
        //    string _cpteFin = TxtCpte.Text; double _conseFin = Convert.ToDouble(TxtConseCpte.Text);
        //    string _tdFin = TipoDoc;
        //    double _debFin = 0, _creFin = 0, _difFin = 0;
        //    DateTime _fecMovtoFin = new DateTime(1950, 1, 1);
        //    int _periodoFin = 0;
        //    msgcnt.BuscaComprobante(ref _cpteFin, ref _conseFin, true, Myconnect1,
        //        ref _u, ref _tdFin, ref _debFin, ref _creFin, ref _u, ref _u, ref _difFin, ref _u,
        //        ref _u, ref _u, ref _fecMovtoFin, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _periodoFin, ref _u, ref _u);
        //    Dif = _difFin;
        //    TipoDoc = _tdFin;

        //    if (Dif == 0)
        //    {
        //        ok = msgcnt.CierreDocumento(TxtCpte.Text, Convert.ToDouble(TxtConseCpte.Text), Myconnect1);
        //        if (ok)
        //        {
        //            switch (ForPag)
        //            {
        //                case "CH":
        //                    msgImpCnt.impre(TxtCpte.Text, TxtConseCpte.Text, false, TipoDoc,
        //                        Myconnect1, "", "0", CopiasImp.ToString());
        //                    break;
        //                case "DF":
        //                    if (int.Parse(LblNumFacturas.Text) > 1)
        //                    {
        //                        for (i = 0; i <= Facturas.GetUpperBound(0) - 1; i++)
        //                            msgtes.GrabaPagoFactura(Facturas[i], Terceros[i], Txtbanco.Text, "C",
        //                                TxtCpte.Text, TxtConseCpte.Text, txtNumCheque.Text,
        //                                Convert.ToDouble(TxtValGirar.Text), CuentaFact, Myconnect1);
        //                    }
        //                    msgImpCnt.impre(TxtCpte.Text, TxtConseCpte.Text, false, TipoDoc,
        //                        Myconnect1, "", "1", CopiasImp.ToString());
        //                    break;
        //            }
        //        }
        //    }
        //}

        //// Helper: BuscarCompania p28=Cpto4Mil — siguiendo patrón ClsDepositos.cs (_u para dummies)
        //private string BuscarCompaniaCpto4Mil(string codEmpresa, OdbcConnection myconnect)
        //{
        //    string _u = " ";
        //    string _p3 = " ", _p4 = "00", _p5 = "N", _p6 = "0";
        //    int _p7 = 0; decimal _p8 = 0; int _p9 = 0; decimal _p10 = 0; int _p11 = 0; int _p12 = 0;
        //    double _p13 = 0;
        //    string _p14 = "00", _p15 = "0", _p16 = " ", _p17 = " ";
        //    string _p18 = "9999", _p19 = "9999", _p20 = "9999", _p21 = "9999", _p22 = "9999";
        //    string _p23 = "", _p24 = "", _p25 = "9999", _p26 = " ", _p27 = "0";
        //    string _p28 = "9999"; // Cpto4Mil — lo que necesitamos
        //    double _p31 = 0, _p32 = 0; int _p33 = 0; double _p37 = 0;
        //    double _p43 = 0, _p44 = 0;
        //    char _p58 = ' '; double _p59 = 0, _p60 = 0;
        //    char _p65 = ' '; int _p66 = 0; int _p73 = 0;
        //    msgsys.BuscarCompania(codEmpresa, myconnect,
        //        ref _p3, ref _p4, ref _p5, ref _p6,
        //        ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12,
        //        ref _p13, ref _p14, ref _p15, ref _p16, ref _p17,
        //        ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, ref _p23, ref _p24,
        //        ref _p25, ref _p26, ref _p27, ref _p28,
        //        ref _u, ref _u, ref _p31, ref _p32, ref _p33, ref _u,
        //        ref _u, ref _u, ref _p37, ref _u,
        //        ref _u, ref _u, ref _u, ref _u, ref _p43, ref _p44,
        //        ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _p58, ref _p59, ref _p60, ref _u,
        //        ref _u, ref _u, ref _u, ref _p65, ref _p66,
        //        ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _p73, ref _u);
        //    return _p28;
        //}

        //// Helper: msgparcnt.BuscarTercero p14=PagaGrabamen
        //private void ObtenerCobraGrabamen(ref string nit, ref bool cobraGrabaNit)
        //{
        //    string _u = " ";
        //    int _estado = 0;
        //    decimal _tasaIca = 0;
        //    msgparcnt.BuscarTercero(ref nit, Myconnect1,
        //        ref _u, ERP.Core.Contabilidad.Models.ParamCnt.Navega.Ninguno, ERP.Core.Contabilidad.Models.ParamCnt.TipoTercero.Tercero,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref cobraGrabaNit, ref _u, ref _estado, ref _tasaIca, "", "");
        //}

        //// Helper: BuscarTercero solo para nombre
        //private void AplicarBuscarTerceroNombre(string nit)
        //{
        //    string _u = " ";
        //    string nombre = LblNombre.Text;
        //    msgcnt.BuscarTercero(nit, Myconnect1,
        //        ref nombre, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u);
        //    LblNombre.Text = nombre;
        //}

        //private void Txtbanco_LostFocus(object sender, EventArgs e)
        //{
        //    if (string.IsNullOrEmpty(Txtbanco.Text.Trim())) return;

        //    LblNombre.Text = " "; lblNomBanco.Text = " ";
        //    string _u = " ";
        //    string UltCheque = " ", Cuentabanco = " ";
        //    string CONTROL_CONSEC = "N";
        //    string _b = Txtbanco.Text, _nomBanco = lblNomBanco.Text;

        //    // BuscaBanco: p4=nombreResumen(lblNomBanco), p10=UltCheque, p11=Cuentabanco, p22=CONTROL_CONSEC
        //    ok = msparcop1.BuscaBanco(ref _b, Myconnect1,
        //        ref _u, ref _nomBanco, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref UltCheque, ref Cuentabanco, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
        //        ref _u, ref _u, ref _u, ref _u, ref _u, ref _u,
        //        ref _u, ref _u, ref _u, ref CONTROL_CONSEC);

        //    lblNomBanco.Text = _nomBanco;

        //    if (ok)
        //    {
        //        txtSaldoBanco.Text = msgcnt.BuscarSaldoCuenta(Cuentabanco,
        //            DtpFecha.Value.ToString("yyyyMM"), "9999", "99999999", Myconnect1).ToString();
        //        txtSaldoBanco.Text = Strings.FormatNumber(Convert.ToDouble(txtSaldoBanco.Text), 2);
        //        double ultCh = double.TryParse(UltCheque, out double uc) ? uc : 0;
        //        txtNumCheque.Text = (ultCh + 1).ToString();
        //        txtNumCheque.Enabled = CONTROL_CONSEC != "Y";
        //    }
        //    else
        //    {
        //        lblNomBanco.Text = "banco No existe";
        //    }
        //}

        //private void helpBanco_Click(object sender, EventArgs e)
        //{
        //    Txtbanco.Text = msgsys.HelpBancos(Myconnect1, this);
        //    Txtbanco.Focus();
        //}

        //private void HelpNit_Click(object sender, EventArgs e)
        //{
        //    LblCedula.Text = msgparcnt.HelpNits(Myconnect1, this);
        //    LblCedula.Focus();
        //}

        //private void LblCedula_LostFocus(object sender, EventArgs e)
        //{
        //    if (Myconnect1.State == System.Data.ConnectionState.Open)
        //        AplicarBuscarTerceroNombre(LblCedula.Text);
        //}

        //private void TxtValGirar_LostFocus(object sender, EventArgs e)
        //{
        //    if (!string.IsNullOrEmpty(TxtValGirar.Text.Trim()) || Information.IsNumeric(TxtValGirar.Text))
        //        TxtValGirar.Text = Strings.FormatNumber(Convert.ToDouble(TxtValGirar.Text), 2);
        //}
    }
}
