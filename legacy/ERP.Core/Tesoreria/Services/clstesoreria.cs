using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Tesoreria.Services
{
    // Traducción de: Public Class clstesoreria (clstesoreria.vb)
    public class clstesoreria
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();

        public struct odbcConect
        {
            public string PstForFec;
            public string pstMyconec;
            public string sptCodEmpr;
            public string pstUsuario;
            public string pstPascon;
            public string pstServer;
            public string pstPort;
            public string pstTipoBD;
            public string pstBdatos;
            public string pstDNS;
            public string pstUID;
            public string pstEmpresa;
            public string pstForfecyHora;
        }

        public enum OpPago : int
        {
            PagoFactura = 0,
            PagoDirecto = 1
        }

        public enum OpConPor : int
        {
            FecFactura = 0,
            FecVencimiento = 1
        }

        public enum Estadofact : int
        {
            Pendientes = 0,
            Programados = 1,
            Todos = 2
        }

        private string stmysql;
        private bool ok;
        public odbcConect varini;
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msParCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();

        public DataSet LeeFacturas(string nit, string cuenta, OdbcConnection myconnect)
        {
            DataSet DtDatos = new DataSet();
            stmysql = "select Concepto as Cpto,consecutivo as Consecutivo,fecha,factura,vlr_factura as Valor, nit, FEC_VEMTO, DETALLE from tes_factura where NIT = '" + nit + "' AND ESTADO <> 'C' AND VLR_FACTURA <> 0";
            connect.ExecuteQueryDataset(stmysql, myconnect, "LeeFacturas", ref DtDatos, "Tblfac");
            return DtDatos;
        }

        public DataTable BuscaFacturas(string Nit, string Cpte, double ConseCpte, OpConPor ConsulPor, DateTime FecIni, DateTime FecFin, Estadofact Estado, string Periodo, OdbcConnection Myconnect)
        {
            string stEstado = null, stFecha = null, stConcepto = null;
            string NitInicial, NitFinal, CampoMes;
            int mes;
            DataSet dtFac = new DataSet();
            string anio = Strings.Mid(Periodo, 1, 4);
            mes = Convert.ToInt32(Strings.Mid(Periodo, 5, 2));

            switch (Estado)
            {
                case Estadofact.Pendientes:
                    stEstado = " and fact.estado = 'O'";
                    break;
                case Estadofact.Programados:
                    stEstado = " and fact.estado = 'P'";
                    break;
                case Estadofact.Todos:
                    stEstado = " and fact.estado in ('P','O')";
                    break;
            }

            switch (Nit)
            {
                case "Todos":
                case null:
                    NitInicial = "0";
                    NitFinal = "99999999999999";
                    break;
                default:
                    NitInicial = Nit;
                    NitFinal = Nit;
                    break;
            }

            if (Cpte != "Todos" && Cpte != "")
                stConcepto = " and concepto = '" + Cpte + "' and consecutivo = " + ConseCpte;

            switch (ConsulPor)
            {
                case OpConPor.FecFactura:
                    stFecha = " and fecha_fac between '" + FecIni.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "'";
                    break;
                case OpConPor.FecVencimiento:
                    stFecha = " and fec_vemto between '" + FecIni.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "'";
                    break;
            }

            switch (mes)
            {
                case 1:  CampoMes = "ene"; break;
                case 2:  CampoMes = "feb"; break;
                case 3:  CampoMes = "mar"; break;
                case 4:  CampoMes = "abr"; break;
                case 5:  CampoMes = "may"; break;
                case 6:  CampoMes = "jun"; break;
                case 7:  CampoMes = "jul"; break;
                case 8:  CampoMes = "ago"; break;
                case 9:  CampoMes = "sep"; break;
                case 10: CampoMes = "oct"; break;
                case 11: CampoMes = "nov"; break;
                default: CampoMes = "dic"; break;
            }

            stmysql = "select fact.estado,fact.nit, case cntnit.tipo_persona when 'N' then cntnit.nombre else cntnit.razon_social end as razon_social,concepto,consecutivo, fact.factura,fec_vemto,VLR_FACTURA, docaux." + CampoMes + " * -1 as Saldo,banco,fact.cuenta_contable,fact.forpag,fact.comision "
                    + " from tes_factura fact left join cnt_nit cntnit on fact.nit = cntnit.nit "
                    + " inner join cnt_saldocaux_vw docaux on fact.cuenta_contable = docaux.cuenta and fact.nit = docaux.nit "
                    + " and fact.factura = docaux.factura And docaux.periodo = " + anio + " and docaux.tipo_auxiliar = 2 "
                    + " where docaux." + CampoMes + " <> 0 and fact.nit between '" + NitInicial + "' and '" + NitFinal + "'"
                    + stEstado + stFecha + stConcepto;

            connect.ExecuteQueryDataset(stmysql, Myconnect, "BuscaFacturas", ref dtFac, "Tblfacturas");
            return dtFac.Tables["Tblfacturas"];
        }

        public bool BuscaFactura(string Cpto, string consecutivo, string nit, string factura, string CuentaContable, OdbcConnection myconect)
        {
            string Valor = "0";
            return BuscaFactura(Cpto, consecutivo, nit, factura, CuentaContable, myconect, ref Valor);
        }

        public bool BuscaFactura(string Cpto, string consecutivo, string nit, string factura, string CuentaContable, OdbcConnection myconect, ref string Valor)
        {
            stmysql = "select VLR_FACTURA as campo1 from TES_FACTURA where CONCEPTO =  '" + Cpto + "' and CONSECUTIVO = " + consecutivo + " and FACTURA = '" + factura + "' and NIT = '" + nit + "' and cuenta_contable = '" + CuentaContable + "'";
            string _c2 = "", _c3 = "", _c4 = "";
            return connect.ExecuteQueryconec(stmysql, myconect, "BuscaFactura", ref Valor, ref _c2, ref _c3, ref _c4);
        }

        public bool BuscaTesoreria(string Cpto, string consecutivo, OdbcConnection myconect)
        {
            string Factura = "0", Valor = "0", CuentaContable = "999999999999";
            return BuscaTesoreria(Cpto, consecutivo, myconect, ref Factura, ref Valor, ref CuentaContable);
        }

        public bool BuscaTesoreria(string Cpto, string consecutivo, OdbcConnection myconect, ref string Factura, ref string Valor, ref string CuentaContable)
        {
            stmysql = "select FACTURA as campo1, VLR_FACTURA as campo2,cuenta_contable as campo3 from TES_FACTURA where CONCEPTO =  '" + Cpto + "' and CONSECUTIVO = " + consecutivo;
            string _c4 = "";
            return connect.ExecuteQueryconec(stmysql, myconect, "BuscaTesoreria", ref Factura, ref Valor, ref CuentaContable, ref _c4);
        }

        public bool BuscaFacCp(string CptoEgre, string consecutivoEgr, OdbcConnection myconect)
        {
            string Factura = "0", Valor = "0", Compronte = "0", Consecutivo = "0", CuentaContable = "999999999999";
            return BuscaFacCp(CptoEgre, consecutivoEgr, myconect, ref Factura, ref Valor, ref Compronte, ref Consecutivo, CuentaContable);
        }

        public bool BuscaFacCp(string CptoEgre, string consecutivoEgr, OdbcConnection myconect,
            ref string Factura, ref string Valor, ref string Compronte, ref string Consecutivo, string CuentaContable)
        {
            stmysql = "select FACTURA as campo1, VLR_FACTURA as campo2,CONCEPTO as campo3,CONSECUTIVO as campo4 from TES_FACTURA where COMBTE =  '" + CptoEgre + "' and NUME_CPMPTO = " + consecutivoEgr;
            ok = connect.ExecuteQueryconec(stmysql, myconect, "BuscaTesoreria", ref Factura, ref Valor, ref Compronte, ref Consecutivo);
            stmysql = "select cuenta_contable as campo1 from TES_FACTURA where COMBTE =  '" + CptoEgre + "' and NUME_CPMPTO = " + consecutivoEgr;
            string _c2 = "", _c3 = "", _c4 = "";
            ok = connect.ExecuteQueryconec(stmysql, myconect, "BuscaTesoreria", ref CuentaContable, ref _c2, ref _c3, ref _c4);
            return ok;
        }

        // Simple overload (internal use, no optional params)
        public bool BuscaNumeroFactura(string factura, string Nit, ref string CuentaContable, OdbcConnection myconect)
        {
            string Valor = "0", Cpto = "0", Consecutivo = "0", FecFac = "00";
            string fecVen = "00", fecPro = "00", estado = "O", Banco = "9999";
            string Detalle = null, ClaseDocAux = null, NumDocAux = null, Cencosto = null;
            string ForPag = " ", Comision = " ";
            double saldo = 0;
            return BuscaNumeroFactura(factura, Nit, ref CuentaContable, myconect,
                ref Valor, ref Cpto, ref Consecutivo, ref FecFac, ref fecVen, ref fecPro,
                ref estado, ref Banco, ref Detalle, ref saldo, ref ClaseDocAux, ref NumDocAux,
                ref Cencosto, ref ForPag, ref Comision);
        }

        // Overload returning FecFac (for ProgramaFactura)
        public bool BuscaNumeroFactura(string factura, string Nit, ref string CuentaContable, OdbcConnection myconect,
            ref string Valor, ref string Cpto, ref string Consecutivo, ref string FecFac)
        {
            string fecVen = "00", fecPro = "00", estado = "O", Banco = "9999";
            string Detalle = null, ClaseDocAux = null, NumDocAux = null, Cencosto = null;
            string ForPag = " ", Comision = " ";
            double saldo = 0;
            return BuscaNumeroFactura(factura, Nit, ref CuentaContable, myconect,
                ref Valor, ref Cpto, ref Consecutivo, ref FecFac, ref fecVen, ref fecPro,
                ref estado, ref Banco, ref Detalle, ref saldo, ref ClaseDocAux, ref NumDocAux,
                ref Cencosto, ref ForPag, ref Comision);
        }

        // Full version with all optional ByRef params
        public bool BuscaNumeroFactura(string factura, string Nit, ref string CuentaContable, OdbcConnection myconect,
            ref string Valor, ref string Cpto, ref string Consecutivo, ref string FecFac,
            ref string fecVen, ref string fecPro, ref string estado, ref string Banco,
            ref string Detalle, ref double saldo, ref string ClaseDocAux, ref string NumDocAux,
            ref string Cencosto, ref string ForPag, ref string Comision)
        {
            stmysql = "select vlR_FACTURA as campo1, CONCEPTO as campo2, CONSECUTIVO as campo3, FECHA_FAC as campo4 from TES_FACTURA where FACTURA = '" + factura + "' and NIT = '" + Nit + "' and cuenta_contable = '" + CuentaContable + "'";
            ok = connect.ExecuteQueryconec(stmysql, myconect, "BuscaFactura", ref Valor, ref Cpto, ref Consecutivo, ref FecFac);

            stmysql = "select FEC_VEMTO as campo1, FEC_PROGRAMA as campo2, estado as campo3,docu_tipo as campo4 from TES_FACTURA where FACTURA = '" + factura + "' and NIT = '" + Nit + "' and cuenta_contable = '" + CuentaContable + "'";
            ok = connect.ExecuteQueryconec(stmysql, myconect, "BuscaFactura", ref fecVen, ref fecPro, ref estado, ref ClaseDocAux);

            stmysql = "select BANCO as campo1, CUENTA_CONTABLE as campo2, detalle as campo3,docu_numero as campo4  from TES_FACTURA where FACTURA = '" + factura + "' and NIT = '" + Nit + "' and cuenta_contable = '" + CuentaContable + "'";
            ok = connect.ExecuteQueryconec(stmysql, myconect, "BuscaFactura", ref Banco, ref CuentaContable, ref Detalle, ref NumDocAux);

            stmysql = "select BANCO as campo1, CUENTA_CONTABLE as campo2, detalle as campo3,docu_numero as campo4  from TES_FACTURA where FACTURA = '" + factura + "' and NIT = '" + Nit + "' and cuenta_contable = '" + CuentaContable + "'";
            ok = connect.ExecuteQueryconec(stmysql, myconect, "BuscaFactura", ref Banco, ref CuentaContable, ref Detalle, ref NumDocAux);

            stmysql = "select ccosto as campo1,forpag as campo2, comision as campo3  from TES_FACTURA where FACTURA = '" + factura + "' and NIT = '" + Nit + "' and cuenta_contable = '" + CuentaContable + "'";
            string _c4 = "";
            ok = connect.ExecuteQueryconec(stmysql, myconect, "BuscaFactura", ref Cencosto, ref ForPag, ref Comision, ref _c4);

            return ok;
        }

        public bool GrabaFactura(string factura, string nit, string cuenta, int periodo, OdbcConnection myconect,
            string Cpto = "9999", string consecutivo = "0", string codigoter = "99999999999999",
            DateTime? FechaFactura = null, DateTime? FachaVence = null, DateTime? fechaProgra = null,
            double debito = 0, double credito = 0, string Detalle = null, string estado = "O",
            string DocTipo = null, string DocNumero = null, string Cencosto = "99999999")
        {
            DateTime dtFechaFactura = FechaFactura ?? new DateTime(1950, 1, 1);
            DateTime dtFachaVence = FachaVence ?? new DateTime(1950, 1, 1);
            DateTime dtFechaProgra = fechaProgra ?? new DateTime(1950, 1, 1);

            string agencia = "9999";
            // msgcop.BuscaAsociado(codigoter, myconect, agencia at p5)
            BuscaAsociadoGetAgencia(codigoter, myconect, ref agencia);

            string cuentaRef = cuenta;
            ok = BuscaNumeroFactura(factura, nit, ref cuentaRef, myconect);
            if (!ok)
            {
                stmysql = "insert into tes_factura(CONCEPTO,CONSECUTIVO,FECHA,PERIODO,FACTURA,FECHA_FAC,NIT,FEC_VEMTO,CODIGOTER,CCOSTO,AGENCIA,CUENTA_CONTABLE,DETALLE,VLR_FACTURA,FEC_PROGRAMA,BANCO,ESTADO,USUARIO,FECHA_GRA,docu_tipo,docu_numero) "
                        + "values ('" + Cpto + "','" + consecutivo + "','" + dtFechaFactura.ToString(varini.PstForFec) + "'," + periodo + ",'" + factura + "','" + dtFechaFactura.ToString(varini.PstForFec) + "','" + nit + "','" + dtFachaVence.ToString(varini.PstForFec) + "','" + codigoter + "','" + Cencosto + "','" + agencia + "','"
                        + cuenta + "','" + Detalle + "','" + credito + "','" + dtFechaProgra.ToString(varini.PstForFec) + "','" + "9999" + "','" + estado + "','" + varini.pstUsuario + "','" + DateTime.Now.ToString(varini.pstForfecyHora) + "','" + DocTipo + "','" + DocNumero + "')";
                ExecuteQueryconec(stmysql, myconect, "GrabaFactura");
            }
            else
            {
                stmysql = "update tes_factura set estado = '" + estado
                        + "', FEC_VEMTO =  '" + dtFachaVence.ToString(varini.PstForFec)
                        + "', DETALLE =  '" + Detalle
                        + "', docu_tipo =  '" + DocTipo
                        + "', docu_numero =  '" + DocNumero + "'"
                        + " where FACTURA = '" + factura + "'  and nit = '" + nit + "' and  CUENTA_CONTABLE =  '" + cuenta + "'";
                ExecuteQueryconec(stmysql, myconect, "GrabaFactura");
            }
            return true;
        }

        private void BuscaAsociadoGetAgencia(string codigoter, OdbcConnection myconect, ref string agencia)
        {
            // msgcop.BuscaAsociado with agencia at p5 (4 dummies p3,p4 before agencia)
            string _p3 = "", _p4 = "";
            // msgcop.BuscaAsociado(codigoter, myconect, ref _p3, ref _p4, ref agencia); // ERROR: CS7036
        }

        public bool GrabaPagoFactura(string NroFactura, string Nit, string banco, string Estado, string Cpte, string ConseCpte, string Cheque, double ValorCheque, string cuenta, OdbcConnection myconnect)
        {
            string cuentaRef = cuenta;
            ok = BuscaNumeroFactura(NroFactura, Nit, ref cuentaRef, myconnect);
            if (ok)
            {
                stmysql = "update tes_factura set banco = '" + banco + "', estado ='" + Estado + "', COMBTE ='" + Cpte + "',NUME_CPMPTO = " + ConseCpte
                        + ",NUMERO_CHEQUE  = '" + Cheque + "', VLR_CHEQUE = " + ValorCheque
                        + " where FACTURA = '" + NroFactura + "' and nit = '" + Nit + "' and cuenta_contable = '" + cuenta + "'";
                ExecuteQueryconec(stmysql, myconnect, "ProgramaFactura");
                return true;
            }
            return false;
        }

        public bool CargaPagoFactura(ref string[] nit, string[] facturas, ref string Banco, string[] Cuentas,
            string formapago, Form Myforma, string Usuario, OdbcConnection myconnect,
            double ValGirar = 0, OpPago opPago = 0, string Comprobante = "9999",
            string ConseComprobante = null)
        {
            string _num = null; bool _neg = false; string _det = null;
            return CargaPagoFactura(ref nit, facturas, ref Banco, Cuentas, formapago, Myforma, Usuario, myconnect,
                ValGirar, opPago, Comprobante, ConseComprobante, ref _num, ref _neg, ref _det);
        }

        public bool CargaPagoFactura(ref string[] nit, string[] facturas, ref string Banco, string[] Cuentas,
            string formapago, Form Myforma, string Usuario, OdbcConnection myconnect,
            double ValGirar, OpPago opPago, string Comprobante,
            string ConseComprobante, ref string NumCheque, ref bool TieneNegativos, ref string Detallefac)
        {
            // FrmPagoFact frmPagoFactura = new FrmPagoFact(myconnect); // ERROR: CS0246
            string Cuentabanco = "999999999999";
            double UltCheque = 0;
            bool Ok = false;
            string CONTROL_CONSEC = "N";
            string _nomBanco = "";

            // msParCop.BuscaBanco: p1=Banco, p4=_nomBanco(nombreResumen), p5=Cuentabanco(CodCuenta), p10=UltCheque, p22=CONTROL_CONSEC
            BuscaBancoCargaPago(Banco, myconnect, ref _nomBanco,
                ref Cuentabanco, ref UltCheque, ref CONTROL_CONSEC);

            // frmPagoFactura.LblNombre.Text = " "; // ERROR: CS0103
            // frmPagoFactura.lblNomBanco.Text = _nomBanco; // ERROR: CS0103
            // frmPagoFactura.LblCedula.Text = nit[0]; // ERROR: CS0103

            // msgcnt.BuscarTercero: p3=LblNombre
            string nombreTercero = " ";
            string _u3 = " ";
            // msgcnt.BuscarTercero(frmPagoFactura.LblCedula.Text, myconnect, ref nombreTercero, // ERROR: CS0103
                // ref _u3, ref _u3, ref _u3, ref _u3, ref _u3, ref _u3, ref _u3, ref _u3); // ERROR: CS0103
            // frmPagoFactura.LblNombre.Text = nombreTercero; // ERROR: CS0103

            // frmPagoFactura.txtSaldoBanco.Text = msgcnt.BuscarSaldoCuenta(Cuentabanco, // ERROR: CS0103
                // frmPagoFactura.DtpFecha.Value.ToString("yyyyMM"), "9999", "99999999", myconnect).ToString(); // ERROR: CS0103
            // frmPagoFactura.txtSaldoBanco.Text = Strings.FormatNumber( // ERROR: CS0103
                // Convert.ToDouble(frmPagoFactura.txtSaldoBanco.Text), 2); // ERROR: CS0103

            // frmPagoFactura.Usuario = Usuario; // ERROR: CS0103
            // frmPagoFactura.Tag = opPago; // ERROR: CS0103
            // frmPagoFactura.Facturas = facturas; // ERROR: CS0103
            // frmPagoFactura.Cuentas = Cuentas; // ERROR: CS0103
            // frmPagoFactura.Terceros = nit; // ERROR: CS0103
            // frmPagoFactura.TxtDetalle.Text = Detallefac; // ERROR: CS0103
            if (opPago == clstesoreria.OpPago.PagoDirecto)
                // frmPagoFactura.CambiaTercero = true; // ERROR: CS0103

            // frmPagoFactura.LblForPago.Text = formapago; // ERROR: CS0103
            switch (formapago)
            {
                case "CH":
                    // frmPagoFactura.txtNumCheque.Text = (UltCheque + 1).ToString(); // ERROR: CS0103
                    break;
                case "DF":
                    // frmPagoFactura.txtNumCheque.Text = "0"; // ERROR: CS0103
                    break;
            }

            // frmPagoFactura.LblNumFacturas.Text = facturas.GetUpperBound(0).ToString(); // ERROR: CS0103
            // frmPagoFactura.TxtValGirar.Text = Strings.Format(ValGirar, "####,###,##0.00"); // ERROR: CS0103
            // frmPagoFactura.Txtbanco.Text = Banco; // ERROR: CS0103
            // frmPagoFactura.TxtValGirar.Tag = ""; // ERROR: CS0103
            if (TieneNegativos)
            {
                // frmPagoFactura.TxtValGirar.Enabled = false; // ERROR: CS0103
                // frmPagoFactura.TxtValGirar.Tag = "Neg"; // ERROR: CS0103
            }

            // frmPagoFactura.Owner = Myforma; // ERROR: CS0103

            if (opPago == clstesoreria.OpPago.PagoDirecto)
            {
                // frmPagoFactura.Txtbanco.Enabled = true; // ERROR: CS0103
                // frmPagoFactura.TxtCpte.Text = Comprobante; // ERROR: CS0103
                // frmPagoFactura.TxtConseCpte.Text = ConseComprobante; // ERROR: CS0103
                // frmPagoFactura.TxtCpte.Enabled = false; // ERROR: CS0103
                // frmPagoFactura.TxtConseCpte.Enabled = false; // ERROR: CS0103
                // frmPagoFactura.TxtValGirar.Enabled = false; // ERROR: CS0103
                // frmPagoFactura.helpBanco.Enabled = true; // ERROR: CS0103
            }

            // if (CONTROL_CONSEC == "N") // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                // frmPagoFactura.txtNumCheque.Enabled = true; // ERROR: CS0103
            // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                // frmPagoFactura.txtNumCheque.Enabled = false; // ERROR: CS0103

            // if (frmPagoFactura.ShowDialog() == DialogResult.Yes) // ERROR: CS0103
                // Ok = true; // ERROR: CS0103
            else
                Ok = false;

            // Banco = frmPagoFactura.Txtbanco.Text; // ERROR: CS0103
            // NumCheque = frmPagoFactura.txtNumCheque.Text; // ERROR: CS0103
            // nit[0] = frmPagoFactura.LblCedula.Text; // ERROR: CS0103
            // frmPagoFactura.Dispose(); // ERROR: CS0103
            return Ok;
        }

        private void BuscaBancoCargaPago(string Banco, OdbcConnection myconnect,
            ref string nombreBanco, ref string Cuentabanco, ref double UltCheque, ref string CONTROL_CONSEC)
        {
            // BuscaBanco: p1=Banco, p2=myconnect, p3=skip(Nombre), p4=nombreBanco(nombreResumen),
            // p5=Cuentabanco(CodCuenta), p6-9=skip, p10=UltCheque, p11-21=skip, p22=CONTROL_CONSEC
            string _p3 = "", _p5 = Cuentabanco;
            string _p6 = "", _p7 = "", _p8 = "", _p9 = "";
            string _p11 = "", _p13 = "0", _p14 = "0", _p15 = "0";
            string _p16 = "0", _p17 = "N", _p18 = "0", _p19 = "0", _p20 = "0";
            string _p21 = "N";
            string UltChequeStr = UltCheque.ToString();
            msParCop.BuscaBanco(ref Banco, myconnect,
                ref _p3, ref nombreBanco, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9,
                ref UltChequeStr, ref _p11, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
                ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20,
                ref _p21, ref CONTROL_CONSEC);
            if (double.TryParse(UltChequeStr, out double ult))
                UltCheque = ult;
            Cuentabanco = _p5;
        }

        public bool ProgramaFactura(string NroFactura, string Nit, string banco, string Estado, DateTime FecProgra, string CuentaContable, string FormaPago, string Comision, OdbcConnection myconnect)
        {
            string _valor = "0", _cpto = "0", _consec = "0", FecFac = "00";
            ok = BuscaNumeroFactura(NroFactura, Nit, ref CuentaContable, myconnect,
                ref _valor, ref _cpto, ref _consec, ref FecFac);

            DateTime dtFecFac;
            if (!DateTime.TryParse(FecFac, out dtFecFac))
                dtFecFac = new DateTime(1950, 1, 1);

            if (FecProgra < dtFecFac)
            {
                MessageBox.Show("Fecha de programación es invalida, inferior a la fecha de la factura", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (ok)
            {
                stmysql = "update tes_factura set banco = '" + banco + "', estado ='" + Estado + "', FEC_PROGRAMA ='" + FecProgra.ToString(varini.PstForFec) + "'"
                        + ",forpag='" + FormaPago + "',comision='" + Comision + "' where FACTURA = '" + NroFactura + "'  and nit = '" + Nit + "' and cuenta_contable = '" + CuentaContable + "'";
                ExecuteQueryconec(stmysql, myconnect, "ProgramaFactura");
                return true;
            }
            return false;
        }

        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                while (Myread.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myread["campo1"] == DBNull.Value) Campo1 = "0";
                        else Campo1 = Myread["campo1"].ToString().Trim();
                    }
                    if (Campo2 != "")
                    {
                        if (Myread["campo2"] == DBNull.Value) Campo2 = "0";
                        else Campo2 = Myread["campo2"].ToString().Trim();
                    }
                    if (Campo3 != "")
                    {
                        if (Myread["campo3"] == DBNull.Value) Campo3 = "0";
                        else Campo3 = Myread["campo3"].ToString().Trim();
                    }
                    if (Campo4 != "")
                    {
                        if (Myread["campo4"] == DBNull.Value) Campo4 = "0";
                        else Campo4 = Myread["campo4"].ToString().Trim();
                    }
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento)
        {
            string _c1 = "", _c2 = "", _c3 = "", _c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref _c1, ref _c2, ref _c3, ref _c4);
        }

        public clstesoreria(string Usuario = "admin")
        {
            Microsoft.Win32.RegistryKey Rk;
            string pas;

            Rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"Applications\ConsfiaS\Shell\Open\config");

            string pasStr = (string)Rk.OpenSubKey("pass").GetValue("");
            char[] pass = pasStr.ToCharArray();
            pas = "";
            for (int i = 0; i <= pass.Length - 1; i += 2)
                pas += (char)(pass[i] - 54);

            varini.pstPascon = pas;
            varini.pstServer = (string)Rk.OpenSubKey("server").GetValue("");
            varini.pstPort = (string)Rk.OpenSubKey("port").GetValue("");
            varini.pstTipoBD = (string)Rk.OpenSubKey("tipo").GetValue("");
            varini.pstBdatos = (string)Rk.OpenSubKey("name").GetValue("");
            varini.pstDNS = (string)Rk.OpenSubKey("driver").GetValue("");
            varini.PstForFec = (string)Rk.OpenSubKey("format").GetValue("");
            varini.pstUID = (string)Rk.OpenSubKey("user").GetValue("");
            varini.pstForfecyHora = (string)Rk.OpenSubKey("fechayhora").GetValue("");
            varini.pstEmpresa = "0001-EMPRESA DE PRUEBAS 999";
            varini.sptCodEmpr = Strings.Mid(varini.pstEmpresa, 1, 4);
            varini.pstUsuario = Usuario;

            switch (varini.pstTipoBD.ToUpper())
            {
                case "MYSQL":
                    varini.pstMyconec = "Driver=" + varini.pstDNS + ";UID=" + varini.pstUID + ";DATABASE=" + varini.pstBdatos
                                      + ";PASSWORD=" + varini.pstPascon + ";PORT=" + varini.pstPort + ";SERVER=" + varini.pstServer;
                    break;
                case "SQL":
                    varini.pstMyconec = "Driver=" + varini.pstDNS + ";Server=" + varini.pstServer
                                + ";Database=" + varini.pstBdatos + ";Uid=" + varini.pstUID + ";Pwd=" + varini.pstPascon + ";";
                    break;
                case "ORACLE":
                    varini.pstMyconec = "DRIVER=" + varini.pstDNS
                                  + ";SERVER=" + varini.pstServer
                                  + ";uid=" + varini.pstUID + ";Pwd=" + varini.pstPascon + ";dbq=" + varini.pstBdatos;
                    break;
                case "POSTGRES":
                    varini.pstMyconec = "Driver=" + varini.pstDNS + ";Server=" + varini.pstServer
                                    + ";Database=" + varini.pstBdatos + ";Uid=" + varini.pstUID + ";Pwd=" + varini.pstPascon;
                    break;
            }
        }

        public DataSet DispersionFondos(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            string estructura = "0";
            DataSet dsdata = new DataSet();

            ok = controlDispersionFondo(banco, cpteini, numinicial, cptefin, numfinal, CuentaDispersora, TipoCuenta, fechapago, StArchivo, MyForma, myconnect);
            if (ok)
            {
                if (MessageBox.Show("Desea Volver a  Generar \r\nla Dispersion de Fondo", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    DataSet DsDataset = new DataSet();
                    string IdCliente = "";
                    double fila = 0;
                    DsDataset.Tables.Add("tblinforme");
                    DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
                    DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());
                    return dsdata;
                }
            }

            // BuscaBanco p1=banco, p16=estructura (EstruDispersion), rest skip
            BuscaBancoGetEstruDispersion(banco, myconnect, ref estructura);

            if (ok)
            {
                switch (estructura.Trim())
                {
                    case "1": dsdata = DispersionBancoBogota(banco, cpteini, numinicial, cptefin, numfinal, CuentaDispersora, TipoCuenta, fechapago, StArchivo, MyForma, myconnect); break;
                    case "2": break; // Estructura Banco de Occidente (not implemented)
                    case "3": dsdata = DispersionBancoCredito(banco, cpteini, numinicial, cptefin, numfinal, CuentaDispersora, TipoCuenta, fechapago, StArchivo, MyForma, myconnect); break;
                    case "4": dsdata = DispersionBancodeColombia(banco, cpteini, numinicial, cptefin, numfinal, CuentaDispersora, TipoCuenta, fechapago, StArchivo, MyForma, myconnect); break;
                    case "5": dsdata = DispersionBancoAVvillas(banco, cpteini, numinicial, cptefin, numfinal, CuentaDispersora, TipoCuenta, fechapago, StArchivo, MyForma, myconnect); break;
                    case "6": dsdata = DispersionBancoCOLMENA(banco, cpteini, numinicial, cptefin, numfinal, CuentaDispersora, TipoCuenta, fechapago, StArchivo, MyForma, myconnect); break;
                    case "7": dsdata = DispersionBancoDavidienda(banco, cpteini, numinicial, cptefin, numfinal, CuentaDispersora, TipoCuenta, fechapago, StArchivo, MyForma, myconnect); break;
                }
            }
            StArchivo.Close();
            return dsdata;
        }

        private void BuscaBancoGetEstruDispersion(string banco, OdbcConnection myconnect, ref string estructura)
        {
            // BuscaBanco p1=banco, p2=myconnect, p3-p15=skip(13), p16=estructura
            string _p1 = banco;
            string _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "", _p11 = "";
            string _p13 = "0", _p14 = "0", _p15 = "0";
            string _p17 = "N", _p18 = "0", _p19 = "0", _p20 = "0";
            string _p21 = "N", _p22 = "N";
            msParCop.BuscaBanco(ref _p1, myconnect,
                ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10,
                ref _p11, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref _p13, ref _p14, ref _p15,
                ref estructura, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22);
        }

        public DataSet InformeDispersionFondos(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Informe de Dispersion de Fondos - Banco: " + banco, MyForma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            int Codbanco = 0;
            double fila = 0;
            string IdCliente = "", Compronte = "";

            DsDataset.Tables.Add("tblinforme");
            DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());

            Codbanco = Convert.ToInt32(banco);
            cpteini = Strings.Right("0000" + cpteini, 4);
            cptefin = Strings.Right("0000" + cptefin, 4);

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre,cntnit.tipo_persona ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Codbanco + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "') ");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre,cntnit.tipo_persona ");

            ok = connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "InformeDispersionFondos", ref DsDataset, "tblinformedispersion");

            if (ok)
            {
                msBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblinformedispersion"].Rows.Count);
                msBarra.Show();
                string nombre;
                for (fila = 0; fila <= DsDataset.Tables["tblinformedispersion"].Rows.Count - 1; fila++)
                {
                    DataRow row = DsDataset.Tables["tblinformedispersion"].Rows[(int)fila];
                    nombre = (string)row["tipo_persona"] == "N" ? (string)row["nombre"] : (string)row["razon_social"];
                    DsDataset.Tables["tblinforme"].Rows.Add(row["nit"], nombre, row["numero_cuenta_ban"], row["valor"],
                        row["detalle"], row["compronte"], row["numero"], row["codigo_banco"], row["tipo_cuenta_ban"], row["bannombre"]);
                    msBarra.PerformStep();
                }
                msBarra.Close();
                msBarra.Dispose();
            }
            return DsDataset;
        }

        private DataSet DispersionBancoBogota(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Dispersion de Fondos - Banco: " + banco, MyForma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            int Codbanco = 0;
            double fila = 0;
            string TipoMovto = "", IdCliente = "", Compronte = "", nombre;

            DsDataset.Tables.Add("tblinforme");
            DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());

            Codbanco = Convert.ToInt32(banco);
            TipoMovto = Interaction.InputBox("Tipo movimiento: 001-Nomina  002-Proveedores  003-Transferencias  995-Otros", "SOLIDO", "002");
            if (TipoMovto.Trim() == "")
            {
                MessageBox.Show("Debe escoger un tipo de movimiento.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            cpteini = Strings.Right("0000" + cpteini, 4);
            cptefin = Strings.Right("0000" + cptefin, 4);

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre,doc.IDBENEF,cntnit.tipo_persona ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and movto.cuenta not like '1120%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Codbanco + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "') ");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre,doc.IDBENEF,cntnit.tipo_persona");

            ok = connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "DispersionBancoBogota", ref DsDataset, "tblbancobogota");

            if (ok)
            {
                msBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblbancobogota"].Rows.Count);
                msBarra.Show();

                DataRow row0 = DsDataset.Tables["tblbancobogota"].Rows[0];
                DispersionCabeceraBancoBogota(StArchivo, fechapago, CuentaDispersora, TipoCuenta,
                    (string)row0["nomempresa"], (string)row0["nitempresa"], TipoMovto);

                for (fila = 0; fila <= DsDataset.Tables["tblbancobogota"].Rows.Count - 1; fila++)
                {
                    DataRow row = DsDataset.Tables["tblbancobogota"].Rows[(int)fila];
                    IdCliente = (string)row["tipo_nit"] == "N"
                        ? (string)row["nit"] + (string)row["NIT_CHEQUEO"]
                        : (string)row["nit"];
                    Compronte = (string)row["compronte"] + row["numero"].ToString();
                    nombre = (string)row["tipo_persona"] == "N" ? (string)row["nombre"] : (string)row["razon_social"];
                    DispersionDetalleBancoBogota(StArchivo, (string)row["tipo_nit"], IdCliente, nombre,
                        (string)row["tipo_cuenta_ban"], (string)row["numero_cuenta_ban"],
                        row["valor"].ToString(), (string)row["codigo_banco"], Compronte, (string)row["detalle"]);
                    DsDataset.Tables["tblinforme"].Rows.Add(row["nit"], nombre, row["numero_cuenta_ban"], row["valor"],
                        row["detalle"], row["compronte"], row["numero"], row["codigo_banco"], row["tipo_cuenta_ban"], row["bannombre"]);
                    actualizarControlaDpf((string)row["compronte"], row["numero"].ToString(), (string)row["IDBENEF"], myconnect);
                    msBarra.PerformStep();
                }
                msBarra.Close();
                msBarra.Dispose();
            }
            return DsDataset;
        }

        private void DispersionCabeceraBancoBogota(StreamWriter StArchivo, DateTime Fecpago, string CuentaOrigen,
            string TipoCuenta, string Nomempresa, string IdEmpresa, string TipoMovto)
        {
            IdEmpresa = IdEmpresa.Replace(".", "").Replace("-", "");
            if (TipoCuenta == "3") TipoCuenta = "5";

            StArchivo.Write("1");
            StArchivo.Write(Fecpago.ToString("yyyyMMdd"));
            StArchivo.Write(Strings.Replace(Strings.Space(24), " ", "0"));
            StArchivo.Write(TipoCuenta);
            StArchivo.Write(Strings.Replace(Strings.Space(6), " ", "0"));
            StArchivo.Write(Strings.Right("00000000000" + CuentaOrigen, 11));
            StArchivo.Write(Strings.Left(Nomempresa + Strings.Space(40), 40));
            StArchivo.Write(Strings.Right("00000000000" + IdEmpresa, 11));
            StArchivo.Write(Strings.Right("000" + TipoMovto, 3));
            StArchivo.Write("0001");
            StArchivo.Write(Fecpago.ToString("yyyyMMdd"));
            StArchivo.Write(Strings.Mid(CuentaOrigen, 1, 3));
            StArchivo.Write("N");
            StArchivo.Write(Strings.Space(48));
            StArchivo.Write(" ");
            StArchivo.WriteLine(Strings.Space(80));
        }

        private void DispersionDetalleBancoBogota(StreamWriter StArchivo, string TipoId, string Idcliente,
            string NomCliente, string TipoCuenta, string NumCuenta, string ValorAbono,
            string CodBancoCuenta, string Comprobante, string Detalle)
        {
            ValorAbono = Strings.FormatNumber(Convert.ToDouble(ValorAbono), 2, TriState.False, TriState.UseDefault, TriState.False);
            ValorAbono = ValorAbono.Replace(".", "");

            StArchivo.Write("2");
            StArchivo.Write(TipoId);
            StArchivo.Write(Strings.Right("00000000000" + Idcliente, 11));
            StArchivo.Write(Strings.Left(NomCliente + Strings.Space(40), 40));
            StArchivo.Write("0");
            StArchivo.Write(TipoCuenta == "A" ? "2" : "1");
            StArchivo.Write(Strings.Left(NumCuenta + Strings.Space(17), 17));
            StArchivo.Write(Strings.Right("000000000000000000" + ValorAbono, 18));
            StArchivo.Write("A");
            StArchivo.Write("000");
            StArchivo.Write(Strings.Right("000" + CodBancoCuenta, 3));
            StArchivo.Write("0001");
            StArchivo.Write(Strings.Space(9));
            StArchivo.Write(" ");
            StArchivo.Write(Strings.Left(Detalle + Strings.Space(70), 70));
            StArchivo.Write("0");
            StArchivo.Write(Strings.Right("0000000000" + Comprobante, 10));
            StArchivo.Write("N");
            StArchivo.Write(Strings.Space(8));
            StArchivo.Write(Strings.Space(18));
            StArchivo.Write(Strings.Space(11));
            StArchivo.Write(Strings.Space(11));
            StArchivo.Write("N");
            StArchivo.WriteLine(Strings.Space(8));
        }

        private DataSet DispersionBancodeColombia(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Dispersion de Fondos - Banco: " + banco, MyForma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            int Codbanco = 0;
            double fila = 0;
            string TipoMovto = "", SecuenciaLote = "", IdCliente = "", Compronte = "", nombre;
            int numeroregistro = 0;
            decimal SumatoriaCredito = 0;

            DsDataset.Tables.Add("tblinforme");
            DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());

            Codbanco = Convert.ToInt32(banco);
            TipoMovto = Interaction.InputBox("Tipo movimiento: 220-PAGO A PROVEEDORES 225-PAGO DE NOMINA  238-PAGOS TERCEROS ", "SOLIDO", "220");
            if (TipoMovto.Trim() == "") { MessageBox.Show("Debe escoger un tipo de movimiento.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }

            SecuenciaLote = Interaction.InputBox("Secuencia Envio de lotes  AA BB CC.....", "SOLIDO", "AA");
            if (SecuenciaLote.Trim() == "") { MessageBox.Show("Debe escoger un Secuencia.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
            if (SecuenciaLote.Length != 2) { MessageBox.Show("La Secuencia debe ser de dos letras.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }

            cpteini = Strings.Right("0000" + cpteini, 4);
            cptefin = Strings.Right("0000" + cptefin, 4);

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre,doc.IDBENEF,cntnit.tipo_persona ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and movto.cuenta not like '1120%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Codbanco + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "') ");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre,doc.IDBENEF,cntnit.tipo_persona");

            ok = connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "DispersionBancodeColombia", ref DsDataset, "tblBancodeColombia");

            if (ok)
            {
                for (fila = 0; fila <= DsDataset.Tables["tblBancodeColombia"].Rows.Count - 1; fila++)
                {
                    numeroregistro++;
                    SumatoriaCredito += Convert.ToDecimal(DsDataset.Tables["tblBancodeColombia"].Rows[(int)fila]["valor"]);
                }

                msBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblBancodeColombia"].Rows.Count);
                msBarra.Show();

                DataRow row0 = DsDataset.Tables["tblBancodeColombia"].Rows[0];
                DispersionCabeceraBancodecolombia(StArchivo, fechapago, CuentaDispersora, TipoCuenta,
                    (string)row0["nomempresa"], (string)row0["nitempresa"], TipoMovto, SecuenciaLote, numeroregistro, SumatoriaCredito.ToString());

                for (fila = 0; fila <= DsDataset.Tables["tblBancodeColombia"].Rows.Count - 1; fila++)
                {
                    DataRow row = DsDataset.Tables["tblBancodeColombia"].Rows[(int)fila];
                    IdCliente = (string)row["tipo_nit"] == "N"
                        ? (string)row["nit"] + (string)row["NIT_CHEQUEO"]
                        : (string)row["nit"];
                    Compronte = (string)row["compronte"] + row["numero"].ToString();
                    DispersionDetalleBancodeColombia(StArchivo, (string)row["tipo_nit"], IdCliente, (string)row["nombre"],
                        (string)row["tipo_cuenta_ban"], (string)row["numero_cuenta_ban"],
                        row["valor"].ToString(), (string)row["codigo_banco"], Compronte, (string)row["detalle"], 0);
                    nombre = (string)row["tipo_persona"] == "N" ? (string)row["nombre"] : (string)row["razon_social"];
                    DsDataset.Tables["tblinforme"].Rows.Add(row["nit"], row["nombre"], row["numero_cuenta_ban"], row["valor"],
                        row["detalle"], row["compronte"], row["numero"], row["codigo_banco"], row["tipo_cuenta_ban"], row["bannombre"]);
                    actualizarControlaDpf((string)row["compronte"], row["numero"].ToString(), (string)row["IDBENEF"], myconnect);
                    msBarra.PerformStep();
                }
                msBarra.Close();
                msBarra.Dispose();
            }
            return DsDataset;
        }

        private void DispersionCabeceraBancodecolombia(StreamWriter StArchivo, DateTime Fecpago, string CuentaOrigen,
            string TipoCuenta, string Nomempresa, string IdEmpresa, string TipoMovto,
            string SecuenciaLote, int numeroregistro, string SumatoriaCreditos)
        {
            IdEmpresa = IdEmpresa.Replace(".", "").Replace("-", "");
            SumatoriaCreditos = Strings.FormatNumber(SumatoriaCreditos, 2, TriState.False, TriState.UseDefault, TriState.False);
            SumatoriaCreditos = SumatoriaCreditos.Replace(".", "");

            StArchivo.Write("1");
            StArchivo.Write(Strings.Right("000000000000000" + IdEmpresa, 15));
            StArchivo.Write("I");
            StArchivo.Write(Strings.Space(15));
            StArchivo.Write(Strings.Right("000" + TipoMovto, 3));
            StArchivo.Write(Strings.Space(10));
            StArchivo.Write(Fecpago.ToString("yyyyMMdd"));
            StArchivo.Write(SecuenciaLote);
            StArchivo.Write(DateTime.Now.ToString("yyyyMMdd"));
            StArchivo.Write(Strings.Right("000000" + numeroregistro, 6));
            StArchivo.Write(Strings.Replace(Strings.Space(17), " ", "0"));
            StArchivo.Write(Strings.Right("00000000000000000" + SumatoriaCreditos, 17));
            StArchivo.Write(Strings.Right("00000000000" + CuentaOrigen, 11));
            StArchivo.Write(TipoCuenta == "1" ? "S" : "D");
            StArchivo.WriteLine(Strings.Space(149));
        }

        private void DispersionDetalleBancodeColombia(StreamWriter StArchivo, string TipoId, string Idcliente,
            string NomCliente, string TipoCuenta, string NumCuenta, string ValorAbono,
            string CodBancoCuenta, string Comprobante, string Detalle, int tipoTrasaccion)
        {
            string tipo_nit_str = "";
            ValorAbono = Strings.FormatNumber(Convert.ToDouble(ValorAbono), 2, TriState.False, TriState.UseDefault, TriState.False);
            ValorAbono = ValorAbono.Replace(".", "");

            switch (TipoId.Trim())
            {
                case "N": tipo_nit_str = "3"; break;
                case "C": tipo_nit_str = "1"; break;
                case "T": tipo_nit_str = "4"; break;
                case "E": tipo_nit_str = "2"; break;
            }

            StArchivo.Write("6");
            StArchivo.Write(Strings.Left(Idcliente + Strings.Space(15), 15));
            StArchivo.Write(Strings.Left(NomCliente + Strings.Space(30), 30));
            StArchivo.Write(Strings.Right("000000000" + CodBancoCuenta, 9));
            StArchivo.Write(Strings.Right(Strings.Space(17) + NumCuenta, 17));
            StArchivo.Write(" ");
            StArchivo.Write(TipoCuenta == "A" ? "37" : "27");
            StArchivo.Write(Strings.Right("00000000000000000" + ValorAbono, 17));
            StArchivo.Write(Strings.Space(8));
            StArchivo.Write(Strings.Space(21));
            StArchivo.Write(tipo_nit_str);
            StArchivo.Write("00000");
            StArchivo.Write(Strings.Space(15));
            StArchivo.Write(Strings.Space(80));
            StArchivo.Write(Strings.Space(15));
            StArchivo.WriteLine(Strings.Space(27));
        }

        private DataSet DispersionBancoCredito(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Dispersion de Fondos - Banco: " + banco, MyForma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            int Codbanco = 0;
            double fila = 0, ValorAbono;
            int EscribesecProvee = 0, SecuenciaProvee = 1;
            bool MismoProvee = false;
            string IdCliente = "", Compronte = "", NombreCliente, CuentaCliente, NitEmpresa, Detalle, nitrepite1, nitrepite2, valorabonocadena, nombre;

            DsDataset.Tables.Add("tblinforme");
            DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());

            Codbanco = Convert.ToInt32(banco);
            CuentaDispersora = CuentaDispersora.Replace(".", "").Replace("-", "");
            cpteini = Strings.Right("0000" + cpteini, 4);
            cptefin = Strings.Right("0000" + cptefin, 4);

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre,doc.IDBENEF,cntnit.tipo_persona    ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and movto.cuenta not like '1120%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Codbanco + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "') ");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre,doc.IDBENEF,cntnit.tipo_persona  order by movto.nit asc");

            ok = connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "DispersionBancoCredito", ref DsDataset, "tblbancocredito");

            if (ok)
            {
                msBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblbancocredito"].Rows.Count);
                msBarra.Show();

                for (fila = 0; fila <= DsDataset.Tables["tblbancocredito"].Rows.Count - 1; fila++)
                {
                    DataRow row = DsDataset.Tables["tblbancocredito"].Rows[(int)fila];
                    IdCliente = (string)row["nit"];
                    nitrepite1 = IdCliente;
                    if (fila != DsDataset.Tables["tblbancocredito"].Rows.Count - 1)
                    {
                        nitrepite2 = (string)DsDataset.Tables["tblbancocredito"].Rows[(int)fila + 1]["nit"];
                        if (nitrepite1 == nitrepite2)
                        {
                            EscribesecProvee = SecuenciaProvee;
                            SecuenciaProvee++;
                            MismoProvee = true;
                        }
                        else
                        {
                            if (MismoProvee)
                            {
                                EscribesecProvee = SecuenciaProvee;
                                SecuenciaProvee = 1;
                                MismoProvee = false;
                            }
                            else
                            {
                                EscribesecProvee = SecuenciaProvee;
                                SecuenciaProvee = 1;
                            }
                        }
                    }
                    else
                    {
                        if (!MismoProvee) SecuenciaProvee = 1;
                        EscribesecProvee = SecuenciaProvee;
                    }

                    nombre = (string)row["tipo_persona"] == "N" ? (string)row["nombre"] : (string)row["razon_social"];
                    NombreCliente = nombre;
                    CuentaCliente = (string)row["numero_cuenta_ban"];
                    NitEmpresa = row["nitempresa"].ToString().Replace(".", "").Replace("-", "");
                    Detalle = row["detalle"].ToString().Replace("-", "").Replace(".", "");
                    ValorAbono = Convert.ToInt32(row["valor"].ToString());
                    ValorAbono = Convert.ToDouble(ValorAbono.ToString().Replace(".", ""));
                    valorabonocadena = ValorAbono.ToString();

                    StArchivo.Write(Strings.Right("00000" + (fila + 1), 5));
                    StArchivo.Write(Strings.Right("00000" + EscribesecProvee, 5));
                    StArchivo.Write(Strings.Right("00" + fechapago.ToString("MM"), 2) + Strings.Right("00" + fechapago.ToString("dd"), 2) + Strings.Right("00" + fechapago.ToString("yy"), 2));
                    StArchivo.Write(Strings.Right("000000000000000" + IdCliente, 15));
                    StArchivo.Write(Strings.Mid(Strings.Left(NombreCliente + "                      ", 22), 1, 22));
                    StArchivo.Write(Strings.Right("00" + Codbanco, 2));
                    StArchivo.Write("CA");
                    StArchivo.Write(Strings.Mid(Strings.Left(CuentaCliente + "                 ", 17), 1, 17));
                    StArchivo.Write("CR");
                    StArchivo.Write(Strings.Right("0000000000" + valorabonocadena, 10));
                    StArchivo.Write(Strings.Mid(Strings.Left(Detalle + "                                                                 ", 65), 1, 65));
                    StArchivo.Write("01");
                    StArchivo.Write(Strings.Right("000000000000000" + NitEmpresa, 15));
                    StArchivo.Write(Strings.Left(CuentaDispersora + "                 ", 17));
                    StArchivo.Write("CCDB");
                    StArchivo.WriteLine();

                    DsDataset.Tables["tblinforme"].Rows.Add(row["nit"], nombre, row["numero_cuenta_ban"], row["valor"],
                        row["detalle"], row["compronte"], row["numero"], row["codigo_banco"], row["tipo_cuenta_ban"], row["bannombre"]);
                    actualizarControlaDpf((string)row["compronte"], row["numero"].ToString(), (string)row["IDBENEF"], myconnect);
                    msBarra.PerformStep();
                }
                msBarra.Close();
                msBarra.Dispose();
            }
            return DsDataset;
        }

        private DataSet DispersionBancoAVvillas(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Dispersion de Fondos - Banco: " + banco, MyForma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            int Codbanco = 0;
            double fila = 0, TotalTransferencia = 0;
            string TipoMovto = "", IdCliente = "", Compronte = "", nombre;

            DsDataset.Tables.Add("tblinforme");
            DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());

            Codbanco = Convert.ToInt32(banco);
            TipoMovto = Interaction.InputBox("Tipo movimiento: 000013-Transferencias  000023-Pago nómina  000024-Pago Proveedores", "SOLIDO", "000024");
            if (TipoMovto.Trim() == "") { MessageBox.Show("Debe escoger un tipo de movimiento.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
            if (TipoMovto.Trim().Length != 6) { MessageBox.Show("Tipo de movimiento no tiene el tamaño requerido.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }

            cpteini = Strings.Right("0000" + cpteini, 4);
            cptefin = Strings.Right("0000" + cptefin, 4);

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nomres as nomresempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre,doc.IDBENEF,cntnit.tipo_persona ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and movto.cuenta not like '1120%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Codbanco + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "') ");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nomres,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre,doc.IDBENEF,cntnit.tipo_persona");

            ok = connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "DispersionBancoAVvillas", ref DsDataset, "tblbancoavvillas");

            if (ok)
            {
                msBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblbancoavvillas"].Rows.Count);
                msBarra.Show();

                DataRow row0 = DsDataset.Tables["tblbancoavvillas"].Rows[0];
                DispersionCabeceraBancoAvvillas(StArchivo, fechapago, TipoMovto.Trim(), CuentaDispersora, TipoCuenta,
                    (string)row0["nitempresa"], (string)row0["nomresempresa"]);

                for (fila = 0; fila <= DsDataset.Tables["tblbancoavvillas"].Rows.Count - 1; fila++)
                {
                    DataRow row = DsDataset.Tables["tblbancoavvillas"].Rows[(int)fila];
                    IdCliente = (string)row["tipo_nit"] == "N"
                        ? (string)row["nit"] + (string)row["NIT_CHEQUEO"]
                        : (string)row["nit"];
                    Compronte = TipoMovto != "000023"
                        ? (string)row["compronte"] + row["numero"].ToString()
                        : "0000";
                    nombre = (string)row["tipo_persona"] == "N" ? (string)row["nombre"] : (string)row["razon_social"];
                    DispersionDetalleBancoAvvillas(StArchivo, TipoMovto, TipoCuenta, CuentaDispersora, "052",
                        (string)row["tipo_cuenta_ban"], (string)row["numero_cuenta_ban"],
                        (fila + 1).ToString(), row["valor"].ToString(), Compronte, nombre, IdCliente, (string)row["tipo_nit"]);
                    DsDataset.Tables["tblinforme"].Rows.Add(row["nit"], nombre, row["numero_cuenta_ban"], row["valor"],
                        row["detalle"], row["compronte"], row["numero"], row["codigo_banco"], row["tipo_cuenta_ban"], row["bannombre"]);
                    actualizarControlaDpf((string)row["compronte"], row["numero"].ToString(), (string)row["IDBENEF"], myconnect);
                    msBarra.PerformStep();
                }

                TotalTransferencia = Convert.ToDouble(DsDataset.Tables["tblbancoavvillas"].Compute("sum(valor)", "").ToString());
                TotalTransferencia = Convert.ToDouble(Strings.FormatNumber(TotalTransferencia, 0));
                string totStr = TotalTransferencia.ToString().Replace(",", "").Replace("-", "");

                if (TipoMovto.Trim() == "000023")
                {
                    string totFmt = Strings.Right("000000000000000000" + totStr, 18) + Strings.Right("00", 2);
                    StArchivo.Write("03");
                    StArchivo.Write(Strings.Right("000000000" + DsDataset.Tables["tblbancoavvillas"].Rows.Count, 9));
                    StArchivo.WriteLine(Strings.Right("00000000000000000000" + totFmt, 20));
                }
                else
                {
                    string totFmt = Strings.Right("0000000000000000" + totStr, 16) + Strings.Right("00", 2);
                    StArchivo.Write("4");
                    StArchivo.Write(Strings.Right("00000000" + DsDataset.Tables["tblbancoavvillas"].Rows.Count, 8));
                    StArchivo.WriteLine(Strings.Right("000000000000000000" + totFmt, 18));
                }

                msBarra.Close();
                msBarra.Dispose();
            }
            return DsDataset;
        }

        private void DispersionCabeceraBancoAvvillas(StreamWriter StArchivo, DateTime Fecpago, string TipoMovto,
            string CuentaDispersora, string TipoCuenta, string Idcliente, string NomCliente)
        {
            Idcliente = Strings.Mid(Idcliente, 1, Idcliente.IndexOf("-"));
            Idcliente = Idcliente.Replace(",", "").Replace(".", "");

            if (TipoMovto == "000023")
            {
                StArchivo.Write("01");
                StArchivo.Write(Fecpago.ToString("yyyyMMdd"));
                StArchivo.Write(Strings.Replace(DateTime.Now.ToString("HH:mm:ss"), ":", ""));
                StArchivo.Write("088");
                StArchivo.Write("02");
                StArchivo.Write(Strings.Space(50));
                StArchivo.WriteLine(Strings.Space(120));
            }
            else
            {
                StArchivo.Write("1");
                StArchivo.Write(Strings.Left(CuentaDispersora + Strings.Space(17), 17));
                StArchivo.Write(TipoCuenta == "A" ? "1" : "0");
                StArchivo.Write("PP");
                StArchivo.Write(Fecpago.ToString("yyyyMMdd"));
                StArchivo.Write(Strings.Right("000000000000000" + Idcliente, 15));
                StArchivo.Write("03");
                StArchivo.Write(Strings.Left(NomCliente + Strings.Space(16), 16));
                StArchivo.Write("0003");
                StArchivo.Write("PPD");
                StArchivo.Write("000000");
                StArchivo.WriteLine("4");
            }
        }

        private void DispersionDetalleBancoAvvillas(StreamWriter StArchivo, string TipoMovto, string TipoCtaDispersora,
            string CuentaDispersora, string CodBancoCuenta, string TipoCuenta, string NumCuenta,
            string Secuencia, string ValorAbono, string Comprobante, string NomCliente, string Idcliente, string TipoNit)
        {
            ValorAbono = Strings.FormatNumber(Convert.ToDouble(ValorAbono), 2, TriState.False, TriState.UseDefault, TriState.False);
            ValorAbono = ValorAbono.Replace(".", "");

            if (TipoMovto == "000023")
            {
                StArchivo.Write("02");
                StArchivo.Write(Strings.Right("000000" + TipoMovto, 6));
                StArchivo.Write(Strings.Right("00" + (TipoCtaDispersora == "1" ? "06" : "01"), 2));
                StArchivo.Write(Strings.Right("0000000000000000" + CuentaDispersora, 16));
                StArchivo.Write(Strings.Right("000" + CodBancoCuenta, 3));
                StArchivo.Write(TipoCuenta == "A" ? "01" : "06");
                StArchivo.Write(Strings.Right("0000000000000000" + NumCuenta, 16));
                StArchivo.Write(Strings.Right("000000000" + Secuencia, 9));
                StArchivo.Write(Strings.Right("000000000000000000" + ValorAbono, 18));
                StArchivo.Write(Strings.Right("0000000000000000" + Comprobante, 16));
                StArchivo.Write(Strings.Replace(Strings.Space(16), " ", "0"));
                StArchivo.Write(Strings.Replace(Strings.Space(16), " ", "0"));
                StArchivo.Write(Strings.Right(NomCliente + Strings.Space(30), 30));
                StArchivo.Write(Strings.Right("00000000000" + Idcliente, 11));
                StArchivo.Write(Strings.Replace(Strings.Space(6), " ", "0"));
                StArchivo.Write(Strings.Replace(Strings.Space(2), " ", "0"));
                StArchivo.WriteLine(Strings.Replace(Strings.Space(20), " ", "0"));
            }
            else
            {
                StArchivo.Write("2");
                StArchivo.Write("37");
                StArchivo.Write(Strings.Right("0000" + CodBancoCuenta, 4));
                StArchivo.Write("0003");
                StArchivo.Write(Strings.Right(Strings.Space(15) + Idcliente, 15));
                StArchivo.Write(TipoNit == "C" ? "01" : "03");
                StArchivo.Write(Strings.Left(NumCuenta + Strings.Space(17), 17));
                StArchivo.Write(TipoCuenta == "A" ? "1" : "0");
                StArchivo.Write(Strings.Left(NomCliente + Strings.Space(22), 22));
                StArchivo.Write("0");
                StArchivo.Write(Strings.Right("000000000000000000" + ValorAbono, 18));
                StArchivo.Write("0");
                StArchivo.WriteLine(Strings.Left(NumCuenta + Strings.Space(16), 16));

                StArchivo.Write("3");
                StArchivo.Write(Strings.Right(Strings.Replace(Strings.Space(15), " ", "0") + Idcliente, 15));
                StArchivo.Write(Strings.Right("0000000000000000" + NumCuenta, 16));
                StArchivo.WriteLine(Strings.Right(Strings.Space(29) + Idcliente, 29));
            }
        }

        private void actualizarControlaDpf(string comprobante, string numero, string idbeneficiario, OdbcConnection myconnect)
        {
            stmysql = "update cnt_docmto set controlardpf ='Y'  where compronte ='" + comprobante + "'   and  numero =" + numero + "   and  IDBENEF = '" + idbeneficiario + "'";
            ExecuteQueryconec(stmysql, myconnect, "actualizarControlaDpf");
        }

        private bool controlDispersionFondo(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Buscando  Dispersion de Fondos del Banco: " + banco + " Ya Generedos ", MyForma);
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Convert.ToInt32(banco) + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "')  and doc.controlardpf='Y'");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre");

            return ExecuteQueryconec(StBuilder.ToString(), myconnect, "DispersionBancoBogota");
        }

        // Dispersion Banco COLMENA BCSC
        private DataSet DispersionBancoCOLMENA(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Dispersion de Fondos - Banco: " + banco, MyForma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            int Codbanco = 0;
            double fila = 0;
            string TipoMovto = "", IdCliente = "", nombre;

            DsDataset.Tables.Add("tblinforme");
            DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());

            Codbanco = Convert.ToInt32(banco);
            TipoMovto = Interaction.InputBox("Tipo movimiento: \r\n22-Crédito Cuenta Corriente \r\n23-Prenotificación Crédito Cuenta Corriente\r\n32-Crédito Cuenta de Ahorros\r\n33-Prenotificación Crédito Cuenta Ahorros", "SOLIDO", "32");
            if (TipoMovto.Trim() == "") { MessageBox.Show("Debe escoger un tipo de movimiento.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
            if (TipoMovto.Trim().Length != 2) { MessageBox.Show("Tipo de movimiento no tiene el tamaño requerido.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }

            cpteini = Strings.Right("0000" + cpteini, 4);
            cptefin = Strings.Right("0000" + cptefin, 4);

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre,doc.IDBENEF,ban03.codtras,cntnit.tipo_persona ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and movto.cuenta not like '1120%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Codbanco + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "') ");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre,doc.IDBENEF,ban03.codtras,cntnit.tipo_persona");

            ok = connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "DispersionBancoCOLMENA", ref DsDataset, "tblbancocolmena");

            if (ok)
            {
                msBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblbancocolmena"].Rows.Count);
                msBarra.Show();

                for (fila = 0; fila <= DsDataset.Tables["tblbancocolmena"].Rows.Count - 1; fila++)
                {
                    DataRow row = DsDataset.Tables["tblbancocolmena"].Rows[(int)fila];
                    IdCliente = (string)row["tipo_nit"] == "N"
                        ? (string)row["nit"] + (string)row["NIT_CHEQUEO"]
                        : (string)row["nit"];
                    nombre = (string)row["tipo_persona"] == "N" ? (string)row["nombre"] : (string)row["razon_social"];
                    DispersionDetalleCOlMENA(StArchivo, TipoMovto, row["valor"].ToString(), (string)row["numero_cuenta_ban"],
                        IdCliente, nombre, (string)row["detalle"], (string)row["codtras"]);
                    DsDataset.Tables["tblinforme"].Rows.Add(row["nit"], nombre, row["numero_cuenta_ban"], row["valor"],
                        row["detalle"], row["compronte"], row["numero"], row["codigo_banco"], row["tipo_cuenta_ban"], row["bannombre"]);
                    actualizarControlaDpf((string)row["compronte"], row["numero"].ToString(), (string)row["IDBENEF"], myconnect);
                    msBarra.PerformStep();
                }
                msBarra.Close();
                msBarra.Dispose();
            }
            return DsDataset;
        }

        private void DispersionDetalleCOlMENA(StreamWriter StArchivo, string TipoMovto, string ValorAbono,
            string NumCuenta, string Idcliente, string NomCliente, string detalle, string banco)
        {
            ValorAbono = Strings.FormatNumber(Convert.ToDouble(ValorAbono), 2, TriState.False, TriState.UseDefault, TriState.False);
            ValorAbono = ValorAbono.Replace(".", "");

            StArchivo.Write("6");
            StArchivo.Write(TipoMovto);
            StArchivo.Write(Strings.Right("000000000000" + ValorAbono, 12));
            StArchivo.Write(Strings.Left(NumCuenta + Strings.Space(17), 17));
            StArchivo.Write(Strings.Right("000000000" + banco, 9));
            StArchivo.Write(Strings.Left(Idcliente + Strings.Space(15), 15));
            StArchivo.Write(Strings.Left(NomCliente + Strings.Space(22), 22));
            StArchivo.Write("V ");
            StArchivo.Write(Strings.Space(13));
            StArchivo.Write(Strings.Left(detalle + Strings.Space(10), 10));
            StArchivo.Write(Strings.Left(detalle + Strings.Space(30), 30));
            StArchivo.WriteLine(Strings.Space(27));
        }

        // Dispersion Banco DAVIDIENDA
        private DataSet DispersionBancoDavidienda(string banco, string cpteini, double numinicial, string cptefin, double numfinal,
            string CuentaDispersora, string TipoCuenta, DateTime fechapago, StreamWriter StArchivo, Form MyForma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Dispersion de Fondos - Banco: " + banco, MyForma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            int Codbanco = 0;
            double fila = 0;
            string TotalTransferencia = "0", IdCliente = "", Compronte = "", NumeroRegistro = "", nombre;

            DsDataset.Tables.Add("tblinforme");
            DsDataset.Tables["tblinforme"].Columns.Add("nit", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("nombre", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("cuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("valor", fila.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("concepto", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("Comprobante", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("consecompro", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("banco", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("tipoCuenta", IdCliente.GetType());
            DsDataset.Tables["tblinforme"].Columns.Add("descbanco", IdCliente.GetType());

            Codbanco = Convert.ToInt32(banco);
            cpteini = Strings.Right("0000" + cpteini, 4);
            cptefin = Strings.Right("0000" + cptefin, 4);

            StBuilder.Append("select doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre as nomempresa,cia.nit as nitempresa,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,sum(vlr_debito-vlr_credito) as Valor,doc.detalle,ban03.nombre as bannombre,doc.IDBENEF,cntnit.tipo_persona ");
            StBuilder.Append("from cnt_docmto doc ");
            StBuilder.Append("inner join cnt_movimto movto on doc.compronte=movto.compronte and doc.numero=movto.numero ");
            StBuilder.Append("left join cnt_nit cntnit on movto.nit=cntnit.nit ");
            StBuilder.Append("left join sys_banco03 ban03 on cntnit.codigo_banco=ban03.codigo_banco ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where doc.forpag='DF' and movto.cuenta not like '1110%' and movto.cuenta not like '1120%' and doc.cerrado='Y' and doc.anulado<>'Y' and doc.banco='" + Codbanco + "' ");
            StBuilder.Append("and (doc.compronte>='" + cpteini + "' and doc.numero>='" + numinicial + "') and (doc.compronte<='" + cptefin + "' and doc.numero<='" + numfinal + "') ");
            StBuilder.Append("group by doc.compronte,doc.numero,movto.nit,cntnit.tipo_nit,cntnit.razon_social,cntnit.nombre,cntnit.NIT_CHEQUEO,cia.nombre,cia.nit,");
            StBuilder.Append("cntnit.tipo_cuenta_ban,cntnit.numero_cuenta_ban,cntnit.codigo_banco,doc.detalle,ban03.nombre,doc.IDBENEF,cntnit.tipo_persona");

            ok = connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "DispersionBancoAVvillas", ref DsDataset, "tblbancoDavidienda");

            if (ok)
            {
                msBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblbancoDavidienda"].Rows.Count);
                msBarra.Show();

                NumeroRegistro = DsDataset.Tables["tblbancoDavidienda"].Rows.Count.ToString();
                string totStr = DsDataset.Tables["tblbancoDavidienda"].Compute("sum(valor)", "").ToString();
                double totDbl = Convert.ToDouble(Strings.FormatNumber(Convert.ToDouble(totStr), 0));
                totStr = totDbl.ToString().Replace(",", "").Replace("-", "");
                TotalTransferencia = Strings.Right("0000000000000000" + totStr, 16) + Strings.Right("00", 2);

                DataRow row0 = DsDataset.Tables["tblbancoDavidienda"].Rows[0];
                DispersionCabeceraBancoDavidienda(StArchivo, fechapago, CuentaDispersora, TipoCuenta,
                    TotalTransferencia, NumeroRegistro, (string)row0["nitempresa"]);

                for (fila = 0; fila <= DsDataset.Tables["tblbancoDavidienda"].Rows.Count - 1; fila++)
                {
                    DataRow row = DsDataset.Tables["tblbancoDavidienda"].Rows[(int)fila];
                    IdCliente = (string)row["tipo_nit"] == "N"
                        ? (string)row["nit"] + (string)row["NIT_CHEQUEO"]
                        : (string)row["nit"];
                    nombre = (string)row["tipo_persona"] == "N" ? (string)row["nombre"] : (string)row["razon_social"];
                    DispersionDetalleBancoDavidienda(StArchivo, "", TipoCuenta, CuentaDispersora,
                        (string)row["codigo_banco"], (string)row["tipo_cuenta_ban"], (string)row["numero_cuenta_ban"],
                        (fila + 1).ToString(), row["valor"].ToString(), Compronte, nombre, IdCliente, (string)row["tipo_nit"], (string)row["detalle"]);
                    DsDataset.Tables["tblinforme"].Rows.Add(row["nit"], nombre, row["numero_cuenta_ban"], row["valor"],
                        row["detalle"], row["compronte"], row["numero"], row["codigo_banco"], row["tipo_cuenta_ban"], row["bannombre"]);
                    actualizarControlaDpf((string)row["compronte"], row["numero"].ToString(), (string)row["IDBENEF"], myconnect);
                    msBarra.PerformStep();
                }
                msBarra.Close();
                msBarra.Dispose();
            }
            return DsDataset;
        }

        private void DispersionCabeceraBancoDavidienda(StreamWriter StArchivo, DateTime Fecpago, string CuentaDispersora,
            string TipoCuenta, string TotalTransferencia, string NumeroRegistro, string IdEmpresa)
        {
            IdEmpresa = IdEmpresa.Replace(".", "").Replace("-", "");

            StArchivo.Write("RC");
            StArchivo.Write(Strings.Right("0000000000000000" + IdEmpresa, 16));
            StArchivo.Write("0000");
            StArchivo.Write("0000");
            StArchivo.Write(Strings.Right("0000000000000000" + CuentaDispersora, 16));
            StArchivo.Write(TipoCuenta == "1" ? "CC" : "CA");
            StArchivo.Write("000051");
            StArchivo.Write(TotalTransferencia);
            StArchivo.Write(Strings.Right("000000" + NumeroRegistro, 6));
            StArchivo.Write(Fecpago.ToString("yyyyMMdd"));
            StArchivo.Write(Strings.Replace(DateTime.Now.ToString("HH:mm:ss"), ":", ""));
            StArchivo.Write("0000");
            StArchivo.Write("9999");
            StArchivo.Write("00000000");
            StArchivo.Write("000000");
            StArchivo.Write("00");
            StArchivo.Write("01");
            StArchivo.Write("000000000000");
            StArchivo.Write("0000");
            StArchivo.Write("0000000000000000000000000000000000000000");
            StArchivo.WriteLine();
        }

        private void DispersionDetalleBancoDavidienda(StreamWriter StArchivo, string TipoMovto, string TipoCtaDispersora,
            string CuentaDispersora, string CodBancoCuenta, string TipoCuenta, string NumCuenta,
            string Secuencia, string ValorAbono, string Comprobante, string NomCliente, string Idcliente,
            string tipo_nit, string DETALLECOMPRO)
        {
            string tipo_nit_str = "";
            ValorAbono = Strings.FormatNumber(Convert.ToDouble(ValorAbono), 2, TriState.False, TriState.UseDefault, TriState.False);
            ValorAbono = ValorAbono.Replace(".", "");

            switch (tipo_nit.Trim())
            {
                case "N": tipo_nit_str = "01"; break;
                case "C": tipo_nit_str = "02"; break;
                case "T": tipo_nit_str = "03"; break;
                case "E": tipo_nit_str = "04"; break;
            }

            StArchivo.Write("TR");
            StArchivo.Write(Strings.Right("0000000000000000" + Idcliente, 16));
            StArchivo.Write("0000000000000000");
            StArchivo.Write(Strings.Right("0000000000000000" + NumCuenta.Trim(), 16));
            StArchivo.Write(TipoCuenta == "A" ? "CA" : (TipoCuenta == "C" ? "CC" : "OP"));
            StArchivo.Write(Strings.Right("000000" + CodBancoCuenta, 6));
            StArchivo.Write(Strings.Right("000000000000000000" + ValorAbono, 18));
            StArchivo.Write("000000");
            StArchivo.Write(tipo_nit_str);
            StArchivo.Write("1");
            StArchivo.Write("9999");
            StArchivo.Write("000000000000000000000000000000000000000000000000000000000000000000000000000000000");
            StArchivo.WriteLine();
            StArchivo.Write("DE");
            StArchivo.Write(Strings.Left(DETALLECOMPRO + Strings.Space(128), 128));
            StArchivo.Write("0000000000000000000000000000000000000000");
            StArchivo.WriteLine();
        }
    }
}
