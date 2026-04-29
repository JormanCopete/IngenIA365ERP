using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Cartera
{
    public partial class Clscartera
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgconfig = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys msgcofsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        public ERP.Core.Compartido.Datos.ClsConect OdbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private bool ok;
        private bool tienemora = false;
        private string stmysql, stmysql2, stmysqlcuopen, stmysqlmora;
        private DataSet dsdataset = new DataSet();
        public StreamReader streamToPrint;
        private DataSet DatLiquidacion = new DataSet();
        private ArrayList lista = new ArrayList();
        private StreamWriter strStreamWriter;
        private StreamReader strStreamReader;
        private DataSet myreadmor = new DataSet("Mora");
        private DataSet myreadpen = new DataSet("Cuopen");
        private int canmora, canpen;
        private ERP.Core.Compartido.Utilidades.Ayuda MsgSas = new ERP.Core.Compartido.Utilidades.Ayuda("admin");

        public Clscartera()
        {
            OdbcConnect.MyOdbcConect(this.varini);
        }

        public enum TipoSelecion : int
        {
            Asociado = 0,
            CentroCosto = 1,
            Agencia = 2,
            Empresa = 3
        }

        public enum Cladesto : int
        {
            Todo = 0,
            Nomina = 1,
            Caja = 2
        }

        public enum MotivoNovedad : int
        {
            Incapacidad = 1,
            Vacaciones = 2,
            Licencias = 3,
            Otros = 9
        }

        public enum TipoDescuento
        {
            Todos = 0,
            Vacaciones = 1,
            Sin_Vacaciones = 2
        }

        public enum opciones : int
        {
            Impresora = 0,
            Correo_electronico = 1,
            A_disco_formato_pdf = 2,
            A_disco_Un_Solo_Archivo = 3
        }

        public enum ClaseModificacion : int
        {
            cuota = 1,
            fechas = 2,
            codeudores = 3
        }

        public enum Navega : int
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }

        public enum TipoBien : int
        {
            Raiz = 1,
            Vehiculo = 2,
            Todos = 3
        }

        public enum SqlDml : int
        {
            Consulta = 1,
            Inserta = 2,
            Actualiza = 3,
            Borra = 4
        }

        public enum TipoActualizacion : int
        {
            Todos = 1,
            Cuota = 2
        }

        public enum PrioridadesAtomar : int
        {
            Todos = 1,
            SoloCreditos = 2,
            SoloAportes = 3,
            SoloAhorros = 4,
            SoloServicios = 5
        }

        // BuscaObligacion with all ref params
        public bool BuscaObligacion(string codigoter, int Lincred, double Numero, OdbcConnection Myconnect,
            ref double cargosad, ref decimal tasaint, ref string CLASEI, ref int CLACUO, ref double cuota,
            ref decimal tasaseg, ref decimal tasaadm, ref int periodd, ref int plazo, ref DateTime fecPriDesc,
            ref int clades, ref int ciclod, ref decimal VALOROB, ref double NumSolicitud,
            ref double SaldoTotal, ref double CapAtra, ref double IntAtra, ref double SegAtra,
            ref double AdmonAtra, ref string codeudor1, ref string codeudor2, ref string codeudor3,
            ref DateTime FECFACT, ref string codeudor4, ref DateTime fecvemto,
            ref string FecUltPago, ref string EmpDsto, ref string IncluyeDebAuto, ref string Autorizacion,
            ref double CuotaAdmon, ref double CuotaSeguro, ref string FormaAdmon, ref string castigo,
            ref string forseg, ref decimal puntosdtf, ref string ReEst, ref DateTime fecrest,
            ref string Calrest, ref string trasladacpto, ref DateTime fecintprop, string PeriodoSalmaecar)
        {
            DataSet DsDataset = new DataSet();
            bool ok;
            string stcargosad = "0", sttasaint = "0", stclades = "0", stciclod = "0", stCLACUO = "0",
                   stcuota = "0", sttasaseg = "0", sttasaadm = "0", stperiodd = "0";
            string fechavemto = "01/01/1950", Stfechaintprop = "01/01/1950";
            StringBuilder Stbuilder = new StringBuilder();

            Stbuilder.Append("Select mae.codigoter,mae.lincred,mae.numero,mae.cargosad, mae.tasaint, mae.CLASEI, mae.CLACUO, mae.cuota, mae.tasaseg, mae.tasaadm, mae.periodd ,");
            Stbuilder.Append("mae.plazo,mae.fecdesc, mae.clades,mae.ciclod, mae.VALOROB, mae.numero_soli,mae.saldot,mae.capatr ,");
            Stbuilder.Append("mae.intatr, mae.segatr,mae.admatr,mae.codeudor1, mae.codeudor2, mae.codeudor3,mae.FECFACT, mae.codeudor4,");
            Stbuilder.Append("mae.fecvemto, mae.fecultpago,mae.EmpDsto,mae.IncluyeDebAuto, mae.autoricentralriesgo,mae.CUOTA_ADM,mae.CUOTA_SEG,sol.for_adm ,");
            Stbuilder.Append("mae.castigo, mae.forseg, mae.puntos,mae.fecintprop, mae.ReEst, mae.fecrest, mae.Calrest, mae.trasladacpto ");
            Stbuilder.Append("from cop_maecar mae ");
            Stbuilder.Append("left join cop_solcre sol on mae.NUMERO_SOLI = sol.numero ");
            Stbuilder.Append("where mae.Codigoter = '" + codigoter + "' and mae.lincred = " + Lincred + " and mae.numero = " + Numero);

            ok = this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), Myconnect, "BuscaObligacion", ref DsDataset, "tblcopmae", true);

            if (!ok)
            {
                return false;
            }
            else
            {
                DataRow row = DsDataset.Tables["tblcopmae"].Rows[0];
                stcargosad = row["cargosad"].ToString();
                sttasaint = row["tasaint"].ToString();
                CLASEI = row["CLASEI"].ToString();
                stCLACUO = row["CLACUO"].ToString();
                stcuota = row["cuota"].ToString();
                sttasaseg = row["tasaseg"].ToString();
                sttasaadm = row["tasaadm"].ToString();
                stperiodd = row["periodd"].ToString();
                plazo = Convert.ToInt32(row["plazo"]);
                fecPriDesc = Convert.ToDateTime(row["fecdesc"]);
                stclades = row["clades"].ToString();
                stciclod = row["ciclod"].ToString();
                VALOROB = Convert.ToDecimal(row["VALOROB"]);
                NumSolicitud = Convert.ToDouble(row["numero_soli"]);
                SaldoTotal = Convert.ToDouble(row["saldot"]);
                CapAtra = Convert.ToDouble(row["capatr"]);
                IntAtra = Convert.ToDouble(row["intatr"]);
                SegAtra = Convert.ToDouble(row["segatr"]);
                AdmonAtra = Convert.ToDouble(row["admatr"]);
                codeudor1 = row["codeudor1"].ToString();
                codeudor2 = row["codeudor2"].ToString();
                codeudor3 = row["codeudor3"].ToString();
                FECFACT = Convert.ToDateTime(row["FECFACT"]);
                codeudor4 = row["codeudor4"].ToString();
                fechavemto = row["fecvemto"].ToString();

                if (row["fecultpago"] is DBNull)
                {
                    FecUltPago = new DateTime(1950, 1, 1).ToString();
                }
                else
                {
                    FecUltPago = row["fecultpago"].ToString();
                }
                // empresa
                EmpDsto = row["EmpDsto"].ToString();
                IncluyeDebAuto = row["IncluyeDebAuto"].ToString();
                Autorizacion = row["autoricentralriesgo"].ToString();
                CuotaAdmon = Convert.ToDouble(row["CUOTA_ADM"]);
                CuotaSeguro = Convert.ToDouble(row["CUOTA_SEG"]);

                if (row["for_adm"] is DBNull)
                {
                    FormaAdmon = "0";
                }
                else
                {
                    FormaAdmon = row["for_adm"].ToString();
                }

                castigo = row["castigo"].ToString();
                forseg = row["forseg"].ToString();
                puntosdtf = Convert.ToDecimal(row["puntos"]);

                if (row["fecintprop"] is DBNull)
                {
                    Stfechaintprop = new DateTime(1950, 1, 1).ToString();
                }
                else
                {
                    Stfechaintprop = row["fecintprop"].ToString();
                }
                ReEst = row["ReEst"].ToString();
                fecrest = Convert.ToDateTime(row["fecrest"]);
                Calrest = row["Calrest"].ToString();
                trasladacpto = row["trasladacpto"].ToString();

                if (PeriodoSalmaecar != "999999")
                {
                    //BuscaCuaotaSalmaecar(codigoter, Lincred, Numero, PeriodoSalmaecar, Myconnect, ref stcuota, ref sttasaint, ref stciclod, ref stclades, ref stperiodd);
                }

                if (Information.IsNumeric(stclades))
                    clades = Convert.ToInt32(stclades);
                else
                    clades = 0;

                if (Information.IsNumeric(stciclod))
                    ciclod = Convert.ToInt32(stciclod);
                else
                    ciclod = 0;

                if (Information.IsNumeric(stcargosad))
                    cargosad = Convert.ToDouble(stcargosad);
                else
                    cargosad = 0;

                if (Information.IsNumeric(sttasaint))
                    tasaint = Convert.ToDecimal(sttasaint);
                else
                    tasaint = 0;

                if (Information.IsNumeric(stCLACUO))
                    CLACUO = Convert.ToInt32(stCLACUO);
                else
                    CLACUO = 0;

                if (Information.IsNumeric(stcuota))
                    cuota = Convert.ToDouble(stcuota);
                else
                    cuota = 0;

                if (Information.IsNumeric(sttasaseg))
                    tasaseg = Convert.ToDecimal(sttasaseg);
                else
                    tasaseg = 0;

                if (Information.IsNumeric(sttasaadm))
                    tasaadm = Convert.ToDecimal(sttasaadm);
                else
                    tasaadm = 0;

                if (Information.IsNumeric(stperiodd))
                    periodd = Convert.ToInt32(stperiodd);
                else
                    stperiodd = "0";

                if (Information.IsDate(fechavemto) == false)
                    fechavemto = new DateTime(1950, 1, 1).ToShortDateString();
                fecvemto = Convert.ToDateTime(fechavemto);

                if (Information.IsDate(FecUltPago) == false)
                    FecUltPago = new DateTime(1950, 1, 1).ToShortDateString();

                if (Information.IsDate(Stfechaintprop) == false)
                    Stfechaintprop = new DateTime(1950, 1, 1).ToShortDateString();
                fecintprop = Convert.ToDateTime(Stfechaintprop);

                return true;
            }
        }

        // Overload without optional params
        public bool BuscaObligacion(string codigoter, int Lincred, double Numero, OdbcConnection Myconnect)
        {
            double cargosad = 0; decimal tasaint = 0; string CLASEI = "0"; int CLACUO = 0; double cuota = 0;
            decimal tasaseg = 0; decimal tasaadm = 0; int periodd = 0; int plazo = 0;
            DateTime fecPriDesc = new DateTime(1950, 1, 1); int clades = 0; int ciclod = 0;
            decimal VALOROB = 0; double NumSolicitud = 0; double SaldoTotal = 0; double CapAtra = 0;
            double IntAtra = 0; double SegAtra = 0; double AdmonAtra = 0;
            string codeudor1 = " ", codeudor2 = " ", codeudor3 = " ";
            DateTime FECFACT = new DateTime(1950, 1, 1); string codeudor4 = " ";
            DateTime fecvemto = new DateTime(1950, 1, 1);
            string FecUltPago = "1/1/1950", EmpDsto = "9999", IncluyeDebAuto = "N", Autorizacion = "Y";
            double CuotaAdmon = 0, CuotaSeguro = 0; string FormaAdmon = "0", castigo = "N", forseg = "10";
            decimal puntosdtf = 0; string ReEst = "N"; DateTime fecrest = new DateTime(1950, 1, 1);
            string Calrest = "A", trasladacpto = "N"; DateTime fecintprop = new DateTime(1950, 1, 1);
            return BuscaObligacion(codigoter, Lincred, Numero, Myconnect,
                ref cargosad, ref tasaint, ref CLASEI, ref CLACUO, ref cuota,
                ref tasaseg, ref tasaadm, ref periodd, ref plazo, ref fecPriDesc,
                ref clades, ref ciclod, ref VALOROB, ref NumSolicitud,
                ref SaldoTotal, ref CapAtra, ref IntAtra, ref SegAtra,
                ref AdmonAtra, ref codeudor1, ref codeudor2, ref codeudor3,
                ref FECFACT, ref codeudor4, ref fecvemto,
                ref FecUltPago, ref EmpDsto, ref IncluyeDebAuto, ref Autorizacion,
                ref CuotaAdmon, ref CuotaSeguro, ref FormaAdmon, ref castigo,
                ref forseg, ref puntosdtf, ref ReEst, ref fecrest,
                ref Calrest, ref trasladacpto, ref fecintprop, "999999");
        }

        public bool BuscaSaldoObligacion(string codigoter, int Lincred, double Numero, int Periodo,
            OdbcConnection Connection, ref double Saldo, ref double CUOTA, ref decimal TASAINT,
            ref string CICLOD, ref string PERIODD, ref string CLADES)
        {
            string stmysql;
            bool ok = true;
            stmysql = "Select SALDO as campo1,CUOTA as campo2,TASAINT as campo3,CICLOD as campo4 from cop_salmaecar where Codigoter = '" + codigoter + "' and lincred = " + Lincred
                     + " and numero = " + Numero + " and periodo = " + Periodo;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaSaldoObligacion", ref Saldo, ref CUOTA, ref TASAINT, ref CICLOD);

            stmysql = "Select PERIODD as campo1,CLADES as campo2  from cop_salmaecar where Codigoter = '" + codigoter + "' and lincred = " + Lincred
                     + " and numero = " + Numero + " and periodo = " + Periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaSaldoObligacion", ref PERIODD, ref CLADES);

            return ok;
        }

        // Overload with fewer params
        public bool BuscaSaldoObligacion(string codigoter, int Lincred, double Numero, int Periodo,
            OdbcConnection Connection, ref double Saldo)
        {
            double CUOTA = 0; decimal TASAINT = 0; string CICLOD = "0", PERIODD = "0", CLADES = "0";
            return BuscaSaldoObligacion(codigoter, Lincred, Numero, Periodo, Connection,
                ref Saldo, ref CUOTA, ref TASAINT, ref CICLOD, ref PERIODD, ref CLADES);
        }

        // Overload: BuscaSaldoObligacion accepting string Periodo
        public bool BuscaSaldoObligacion(string codigoter, int Lincred, double Numero, string Periodo,
            OdbcConnection Connection, ref double Saldo)
        {
            double CUOTA = 0; decimal TASAINT = 0; string CICLOD = "0", PERIODD = "0", CLADES = "0";
            return BuscaSaldoObligacion(codigoter, Lincred, Numero, Convert.ToInt32(Periodo), Connection,
                ref Saldo, ref CUOTA, ref TASAINT, ref CICLOD, ref PERIODD, ref CLADES);
        }

        public bool BuscaCuotaExtra(string codigoter, int lincred, double ConseCredito, int NumExtra,
            int Periodo, OdbcConnection myconect, ref double saldo, ref double SaldoInicial, ref double NumExtraCausada)
        {
            bool ok=true;
            string stmysql = "select saldo as campo1, SALDO_INICIAL as campo2 from cop_salextras where codigoter = '" + codigoter + "' and lincred =" + lincred + " and numero =" + ConseCredito + " and num_extra = " + NumExtra + " and periodo = " + Periodo;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaCuotaExtra", ref saldo, ref SaldoInicial);

            stmysql = "select cuopen.NUME_EXTRA as campo1 from cop_cuopen cuopen "
                    + "inner join cop_copmora copmora on copmora.codigoter = cuopen.codigoter and copmora.lincred =cuopen.lincred "
                    + "and copmora.numero =cuopen.numero and copmora.periodo_contable = cuopen.periodo_contable and copmora.periodo_causa = cuopen.periodo_causa "
                    + "where cuopen.codigoter ='" + codigoter + "' and cuopen.lincred =" + lincred + " and cuopen.numero =" + ConseCredito + " "
                    + "and cuopen.periodo_contable =" + Periodo + " and copmora.saldoextra<>0 and cuopen.NUME_EXTRA=" + NumExtra;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaCuotaExtra", ref NumExtraCausada);

            return ok;
        }

        public bool BuscaLinea(int Lincred, OdbcConnection Connection, ref int clacuo, ref string clasei,
            ref decimal tasaex, ref string centroco, ref int TipoLinea, ref decimal TasaInt,
            ref string totintant, ref string poapen, ref string foradmon, ref string plazo,
            ref string CheTer, ref string Descripcion, ref string IntCuoExt, ref string CuentaCierre)
        {
            string stmysql;
            bool ok;
            stmysql = "Select clacuo as campo1, clasei as campo2,tasaex as campo3,centroco as campo4 from cop_concar12 where lincred = " + Lincred;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaLinea", ref clacuo, ref clasei, ref tasaex, ref centroco);

            stmysql = "Select codahor as campo1, TASAI as campo2, totintant as campo3, poapen as campo4 from cop_concar12 where lincred = " + Lincred;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaLinea", ref TipoLinea, ref TasaInt, ref totintant, ref poapen);
            if (poapen.Trim() == "")
                poapen = "0";

            stmysql = "Select foradmon as campo1, plazo as campo2, CODNOMI  as campo3, descripcion as campo4  from cop_concar12 where lincred = " + Lincred;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaLinea", ref foradmon, ref plazo, ref CheTer, ref Descripcion);

            stmysql = "Select previv as campo1, cuentaiva as campo2  from cop_concar12 where lincred = " + Lincred;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaLinea", ref IntCuoExt, ref CuentaCierre);
            if (Information.IsNumeric(foradmon) == false)
                foradmon = "0";
            return ok;
        }

        // Overload with fewer params (used by BuscaLineaTipoMovto etc.)
        public bool BuscaLinea(int Lincred, OdbcConnection Connection)
        {
            int clacuo = 0; string clasei = "0"; decimal tasaex = 0; string centroco = "999999999";
            int TipoLinea = 0; decimal TasaInt = 0; string totintant = "N"; string poapen = "0";
            string foradmon = "0"; string plazo = "0"; string CheTer = "N"; string Descripcion = " ";
            string IntCuoExt = "N"; string CuentaCierre = "999999999999";
            return BuscaLinea(Lincred, Connection, ref clacuo, ref clasei, ref tasaex, ref centroco,
                ref TipoLinea, ref TasaInt, ref totintant, ref poapen, ref foradmon, ref plazo,
                ref CheTer, ref Descripcion, ref IntCuoExt, ref CuentaCierre);
        }

        // Overload getting only TipoLinea
        public bool BuscaLinea(int Lincred, OdbcConnection Connection, ref int TipoLinea)
        {
            int clacuo = 0; string clasei = "0"; decimal tasaex = 0; string centroco = "999999999";
            decimal TasaInt = 0; string totintant = "N"; string poapen = "0";
            string foradmon = "0"; string plazo = "0"; string CheTer = "N"; string Descripcion = " ";
            string IntCuoExt = "N"; string CuentaCierre = "999999999999";
            return BuscaLinea(Lincred, Connection, ref clacuo, ref clasei, ref tasaex, ref centroco,
                ref TipoLinea, ref TasaInt, ref totintant, ref poapen, ref foradmon, ref plazo,
                ref CheTer, ref Descripcion, ref IntCuoExt, ref CuentaCierre);
        }

        public void BuscaLineaTipoMovto(int Lincred, OdbcConnection Myconnect, ref string CodMovto)
        {
            int Tipolinea = 0;
            ok = BuscaLinea(Lincred, Myconnect, ref Tipolinea);
            string _p3 = "0", _p4 = "0", _p5 = "0", _p6 = "0", _p7 = "0", _p8 = "0", _p9 = "0";
            string _p10 = "0", _p11 = "0", _p12 = "0", _p13 = "0", _p14 = "0", _p15 = "0", _p16 = "0";
            string _p17 = "0", _p18 = "0", _p19 = "0", _p20 = "0", _p21 = "0", _p22 = "0", _p23 = "0", _p24 = "0";
            switch (Tipolinea)
            {
                case 1:
                    //msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref CodMovto);
                    break;
                case 2:
                   // msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref CodMovto);
                    break;
                case 3:
                   // msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref CodMovto);
                    break;
                case 4:
                case 5:
                  //  msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _p3, ref CodMovto);
                    break;
                case 6:
                  //  msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, ref _p23, ref CodMovto);
                    break;
            }
        }

        public void LiquidaMora(string fecLiqui, int ClaseDsto, decimal Tasa, int DiasGracia,
            decimal TasaUsura, int BaseLiquidacion, int FormaLiq, OdbcConnection pmyConeConect,
            ProgressBar Myprogress, string EmpIni, string EmpFin, string Usuario, bool moraRetirado)
        {
            long dias; DateTime Fechavence = DateTime.MinValue; long vrbase; long VrMora; decimal I;
            int SW1; string mysql; string CODIGOTER; int Cuopen = 0;
            double saldot; string PeriodoCausa = " "; int NUmreg = 0; double Total = 0;
            int fila = 0; string Empresa = "";
           // cop_cuadredoc mYform = new cop_cuadredoc(pmyConeConect);
           // ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidacion de Mora", mYform);
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            bool YaPago = false;

            string mysqlcopmora = "SELECT copmora.codigoter, copmora.lincred, copmora.numero,copmora.periodo_causa, copmora.periodo_contable, saldocapital,saldoextra,saldoInteres, saldoSeguro, saldoAdmon,SaldoOtros, copmora.Diasmora, fecUltliq, fecha_movto, saldos.clades, parame12.intmora, parame12.catec "
                                + ", cuoint,intatr, saldos.periodd,saldos.ciclod, copmae.clacuo,saldos.cuota,parame12.liqDiasMora  "
                                + ", saldoMora, Mora_causado, saldo_antMora,Mora_abono, saldoInteres,cuopen.intmor_causa, cuopen.salint_mora,cuopen.inte_mora,cuopen.intmor_causa,cuopen.intmor_pagado,cuopen.dias_Vemto,cuopen.dias_mora,cuopen.fecult_mora,saldos.tasaint, saldos.saldo,parame12.tipointeres,parame12.dtf,copmae.puntos, "
                                + " copmae.tasaseg,copmae.tasaadm,copmae.forseg,parame12.claAdmon,parame12.foradmon,copmae.CUOTA_SEG,copmae.CUOTA_ADM,parame12.valsegMin,parame12.valsegMax ,copmae.valorob   "
                                + " FROM cop_copmora copmora inner join cop_cuopen cuopen on copmora.codigoter = cuopen.codigoter and copmora.lincred = cuopen.lincred and copmora.numero = cuopen.numero and copmora.periodo_causa = cuopen.periodo_causa "
                                + " inner join sys_maenit maenit on copmora.codigoter = maenit.codigoter "
                                + " inner join cop_maecar copmae on copmora.codigoter = copmae.codigoter and copmora.lincred = copmae.lincred and copmora.numero = copmae.numero inner join cop_salmaecar saldos on saldos.codigoter = copmora.codigoter and saldos.lincred = copmora.lincred and saldos.numero = copmora.numero and saldos.periodo = " + Convert.ToDateTime(fecLiqui).ToString("yyyyMM")
                                + " inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred"
                                + " where copmora.periodo_contable = " + Convert.ToDateTime(fecLiqui).ToString("yyyyMM")
                                + " and maenit.empresa between '" + EmpIni + "' and '" + EmpFin + "'"
                                + " AND cuopen.fecha_movto <= '" + Convert.ToDateTime(fecLiqui).ToString(varini.PstForFec) + "' and copmora.lincred<>9999 "
                                + " and maenit.estado <> 'T'"
                                + (moraRetirado ? " and maenit.estado <> 'R'" : "");

            DataSet Mycopmora = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(mysqlcopmora, pmyConeConect, "LiquidaMora", ref Mycopmora, "LiqMora");
            NUmreg = Mycopmora.Tables["LiqMora"].Rows.Count;

            switch (FormaLiq)
            {
                case 0:
                    //msgbarra.ValorMinimoMaximo(0, NUmreg, "Liquidacion de mora");
                    //msgbarra.Show();
                    break;
                case 1:
                    Myprogress.Maximum = NUmreg;
                    Myprogress.Minimum = 0;
                    Myprogress.Value = 1;
                    Myprogress.Step = 1;
                    break;
            }

            if (Convert.ToDateTime(fecLiqui).ToString("dd") == "31")
            {
                fecLiqui = Convert.ToDateTime(fecLiqui).ToString("yyyy/MM/30");
            }

            try
            {
                while (fila < NUmreg)
                {
                    DataRow row = Mycopmora.Tables[0].Rows[fila];
                    Application.DoEvents();
                    SW1 = 0;
                    YaPago = false;
                    if (row["intmora"].ToString() != "Y" && row["liqdiasmora"].ToString() != "Y")
                    {
                        SW1 = 1;
                    }

                    switch (SW1)
                    {
                        case 0:
                            if (row["fecultliq"] is DBNull)
                            {
                                if (row["fecha_movto"] is DBNull)
                                    SW1 = 1;
                                else
                                    Fechavence = Convert.ToDateTime(row["fecha_movto"]);
                            }
                            else if (row["fecultliq"].ToString() == "1950-01-01")
                            {
                                if (row["fecha_movto"] is DBNull)
                                    SW1 = 1;
                                else
                                    Fechavence = Convert.ToDateTime(row["fecha_movto"]);
                            }
                            else
                            {
                                Fechavence = Convert.ToDateTime(row["fecultliq"]);
                            }

                            if (Fechavence.ToString("dd") == "31")
                            {
                                Fechavence = Convert.ToDateTime(Fechavence.ToString("yyyy-MM-30"));
                            }

                            if (ClaseDsto > 0)
                            {
                                if (ClaseDsto != Convert.ToInt32(row["clades"]))
                                    SW1 = 1;
                            }

                            Total = Convert.ToDouble(row["saldoCapital"]) + Convert.ToDouble(row["saldoExtra"]) + Convert.ToDouble(row["saldoInteres"]);

                            if (Total == 0)
                            {
                                SW1 = 1;
                                if (Convert.ToDouble(row["saldoMora"]) == 0)
                                    YaPago = true;
                            }

                            if (Convert.ToDouble(row["saldo"]) == 0)
                            {
                                SW1 = 1;
                                YaPago = true;
                            }

                            if (Convert.ToDouble(row["saldoCapital"]) != 0 && Convert.ToInt32(row["lincred"]) < 1000 && Convert.ToDouble(row["saldo"]) == 0)
                            {
                                SW1 = 0;
                            }

                            switch (FormaLiq)
                            {
                                case 0:
                                    //msgbarra.PerformStep();
                                    break;
                                case 1:
                                    Myprogress.PerformStep();
                                    break;
                            }

                            if (SW1 == 0)
                            {
                                dias = CalculaDias(Convert.ToDateTime(fecLiqui), Fechavence);
                                switch (BaseLiquidacion)
                                {
                                    case 1:
                                        vrbase = Convert.ToInt64(row["saldoCapital"]) + Convert.ToInt64(row["saldoExtra"]);
                                        break;
                                    case 2:
                                        vrbase = Convert.ToInt64(row["saldoCapital"]) + Convert.ToInt64(row["saldoInteres"]) + Convert.ToInt64(row["saldoSeguro"])
                                                 + Convert.ToInt64(row["saldoAdmon"]) + Convert.ToInt64(row["saldoOtros"]);
                                        break;
                                    case 3:
                                    default:
                                        if (row["saldo"] is DBNull)
                                            saldot = 0;
                                        else
                                            saldot = Convert.ToDouble(row["saldo"]);
                                        vrbase = (long)(saldot - Convert.ToDouble(row["saldoCapital"]) - Convert.ToDouble(row["saldoExtra"]));
                                        break;
                                }

                                if (DiasGracia > 0)
                                {
                                    if (Convert.ToInt32(row["Diasmora"]) + dias <= DiasGracia)
                                    {
                                        vrbase = 0;
                                        dias = 0;
                                    }
                                }

                                if (row["intmora"].ToString() == "N")
                                {
                                    vrbase = 0;
                                }

                                if (TasaUsura > 0)
                                {
                                    switch (row["tipointeres"].ToString())
                                    {
                                        case "1":
                                            row["tasaint"] = clsliqcredito.ConversionDTFaNMV(Convert.ToDecimal(row["dtf"]), Convert.ToDecimal(row["puntos"]));
                                            Tasa = (Convert.ToDecimal(row["tasaint"]) - TasaUsura) / 100;
                                            break;
                                        default:
                                            Tasa = (Convert.ToDecimal(row["tasaint"]) - TasaUsura) / 100;
                                            break;
                                    }
                                    if (Tasa < 0)
                                        I = Tasa * -1;
                                    else
                                        I = 0;
                                }
                                else
                                {
                                    I = (Tasa / 100);
                                }

                                VrMora = (long)((vrbase * I) / 30 * dias);

                                if (I != 0)
                                {
                                    if (row["intmora"].ToString() == "Y" && vrbase <= 0)
                                        dias = 0;
                                }

                                if (dias > 0)
                                {
                                    //this.GrabaCopmora(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), VrMora, 0, "M", row["periodo_causa"].ToString(), row["periodo_contable"].ToString(), pmyConeConect, dias, fecLiqui);
                                    //this.GrabaLiqMora(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToDateTime(fecLiqui), row["periodo_causa"].ToString(), (int)vrbase, (int)dias, (int)VrMora, Fechavence, EmpFin, Tasa, DiasGracia, TasaUsura, ClaseDsto, BaseLiquidacion, pmyConeConect, DateTime.Now, Usuario);
                                }
                            }
                            else
                            {
                                if (YaPago)
                                {
                                    if (Convert.ToDouble(row["saldoMora"]) > 0)
                                    {
                                        double DifMora = 0;
                                        DifMora = Convert.ToDouble(row["Mora_causado"]) - Convert.ToDouble(row["Mora_abono"]);
                                        if (DifMora >= 0)
                                        {
                                            stmysql = "update cop_copmora  set SaldoMora = 0, Mora_causado = " + row["Mora_abono"]
                                                   + " where codigoter ='" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"] + " and periodo_causa = " + row["periodo_causa"] + " and Periodo_contable = " + row["periodo_contable"];
                                            this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "LiquidaMora");

                                            stmysql = "update cop_liqmor set valliq=0 where codigoter ='" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"] + " and FechaLiq = '" + Convert.ToDateTime(fecLiqui).ToString(varini.PstForFec) + "' and Periodo_causa = " + row["periodo_causa"];
                                            this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "LiquidaMora");
                                        }
                                    }
                                }
                            }

                            Cuopen = 0;
                            //Cuopen = this.CalculaCuotasPagar(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(row["periodd"]), Convert.ToInt32(row["ciclod"]), Convert.ToInt32(row["clacuo"]), Convert.ToDecimal(row["tasaint"]), Convert.ToDouble(row["saldo"]), Convert.ToDouble(row["cuota"]), Convert.ToDateTime(fecLiqui).ToString("yyyyMM"), row["forseg"].ToString(), Convert.ToDecimal(row["tasaseg"]), Convert.ToDouble(row["CUOTA_SEG"]), row["claAdmon"].ToString(), row["foradmon"].ToString(), Convert.ToDecimal(row["tasaadm"]), Convert.ToDouble(row["CUOTA_ADM"]), pmyConeConect, Convert.ToDouble(row["valorob"]), Convert.ToDouble(row["valsegMin"]), Convert.ToDouble(row["valsegMax"]));
                            this.GrabaNumeroCuotas(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToDateTime(fecLiqui).ToString("yyyyMM"), Cuopen, Cuopen, pmyConeConect);
                            break;
                    }
                    fila += 1;
                }

                if (EmpIni == EmpFin)
                    Empresa = EmpIni;
                else
                    Empresa = "Todas";
                //this.ProcesoAsociadosMorosos(fecLiqui, Empresa, Usuario, mYform, pmyConeConect);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //  msgbarra.Close();
           // msgbarra.Dispose();
            Mycopmora.Dispose();
        }

        // Overload with defaults
        public void LiquidaMora(string fecLiqui, int ClaseDsto, decimal Tasa, int DiasGracia,
            decimal TasaUsura, int BaseLiquidacion, int FormaLiq, OdbcConnection pmyConeConect,
            ProgressBar Myprogress)
        {
            LiquidaMora(fecLiqui, ClaseDsto, Tasa, DiasGracia, TasaUsura, BaseLiquidacion, FormaLiq, pmyConeConect, Myprogress, "0000", "9999", " ", false);
        }

        public void LiquidaCuotasporPagar(string fecLiqui, int ClaseDsto, decimal Tasa, int DiasGracia,
            decimal TasaUsura, int BaseLiquidacion, int FormaLiq, OdbcConnection pmyConeConect,
            ProgressBar Myprogress, string EmpIni, string EmpFin, string Usuario, bool moraRetirado)
        {
            long dias; DateTime Fechavence = DateTime.MinValue; long vrbase; long VrMora; decimal I;
            int SW1; string mysql; string CODIGOTER; int Cuopen = 0;
            double saldot; string PeriodoCausa = " "; int NUmreg = 0; double Total = 0;
            int fila = 0; string Empresa = "";
            //cop_cuadredoc mYform = new cop_cuadredoc(pmyConeConect);
            //ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidacion de cuotas por pagar", mYform);
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();

            string mysqlcopmora = " SELECT saldos.codigoter, saldos.lincred, saldos.numero, saldos.periodo, saldos.clades, parame12.intmora, "
                                + "parame12.catec , cuoint,intatr, saldos.periodd,saldos.ciclod, copmae.clacuo,saldos.cuota,parame12.liqDiasMora  ,   "
                                + "saldos.tasaint, saldos.saldo, parame12.tipointeres, parame12.dtf, "
                                + "copmae.puntos, copmae.tasaseg, copmae.tasaadm, copmae.forseg, parame12.claAdmon, parame12.foradmon, copmae.CUOTA_SEG, copmae.CUOTA_ADM, "
                                + "parame12.valsegMin,parame12.valsegMax ,copmae.valorob FROM cop_salmaecar saldos  inner join  "
                                + "cop_maecar copmae on saldos.codigoter = copmae.codigoter and saldos.lincred = copmae.lincred and saldos.numero = copmae.numero and saldos.periodo = " + Convert.ToDateTime(fecLiqui).ToString("yyyyMM")
                                + " inner join sys_maenit maenit on saldos.codigoter = maenit.codigoter  inner join cop_concar12 parame12 on saldos.lincred = parame12.lincred  "
                                + "where maenit.empresa between '" + EmpIni + "' and '" + EmpFin + "' AND saldos.lincred<>9999  and maenit.estado <> 'T' "
                                + "AND (  (saldos.LINCRED < 1000 AND ( saldos.SALDO <> 0 OR saldos.CUOTA <> 0)) or (saldos.LINCRED >= 1000 AND  saldos.SALDO <> 0)) ";

            Interaction.InputBox("DE", "", mysqlcopmora);
            DataSet Mycopmora = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(mysqlcopmora, pmyConeConect, "LiquidaCuotasporPagar", ref Mycopmora, "LiqMora");
            NUmreg = Mycopmora.Tables["LiqMora"].Rows.Count;

            switch (FormaLiq)
            {
                case 0:
                    //msgbarra.ValorMinimoMaximo(0, NUmreg, "Liquidacion de cuotas por pagar");
                    //msgbarra.Show();
                    break;
                case 1:
                    Myprogress.Maximum = NUmreg;
                    Myprogress.Minimum = 0;
                    Myprogress.Value = 1;
                    Myprogress.Step = 1;
                    break;
            }

            if (Convert.ToDateTime(fecLiqui).ToString("dd") == "31")
            {
                fecLiqui = Convert.ToDateTime(fecLiqui).ToString("yyyy/MM/30");
            }

            try
            {
                while (fila < NUmreg)
                {
                    DataRow row = Mycopmora.Tables[0].Rows[fila];
                    Application.DoEvents();

                    switch (FormaLiq)
                    {
                        case 0:
                            //msgbarra.PerformStep();
                            break;
                        case 1:
                            Myprogress.PerformStep();
                            break;
                    }

                    Cuopen = 0;
                    //Cuopen = this.CalculaCuotasPagar(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(row["periodd"]), Convert.ToInt32(row["ciclod"]), Convert.ToInt32(row["clacuo"]), Convert.ToDecimal(row["tasaint"]), Convert.ToDouble(row["saldo"]), Convert.ToDouble(row["cuota"]), Convert.ToDateTime(fecLiqui).ToString("yyyyMM"), row["forseg"].ToString(), Convert.ToDecimal(row["tasaseg"]), Convert.ToDouble(row["CUOTA_SEG"]), row["claAdmon"].ToString(), row["foradmon"].ToString(), Convert.ToDecimal(row["tasaadm"]), Convert.ToDouble(row["CUOTA_ADM"]), pmyConeConect, Convert.ToDouble(row["valorob"]), Convert.ToDouble(row["valsegMin"]), Convert.ToDouble(row["valsegMax"]));
                    this.GrabaNumeroCuotas(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToDateTime(fecLiqui).ToString("yyyyMM"), Cuopen, Cuopen, pmyConeConect);

                    fila += 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //msgbarra.Close();
            //msgbarra.Dispose();
            Mycopmora.Dispose();
        }

        // Overload with defaults
        public void LiquidaCuotasporPagar(string fecLiqui, int ClaseDsto, decimal Tasa, int DiasGracia,
            decimal TasaUsura, int BaseLiquidacion, int FormaLiq, OdbcConnection pmyConeConect,
            ProgressBar Myprogress)
        {
            LiquidaCuotasporPagar(fecLiqui, ClaseDsto, Tasa, DiasGracia, TasaUsura, BaseLiquidacion, FormaLiq, pmyConeConect, Myprogress, "0000", "9999", " ", false);
        }

        public void ProyectaLiquidaMora(string fecLiqui, int ClaseDsto, decimal Tasa, int DiasGracia,
            decimal TasaUsura, int BaseLiquidacion, int FormaLiq, OdbcConnection pmyConeConect,
            ProgressBar Myprogress, string Empresa, string Agencia, string Cencosto, string Usuario)
        {
            string EmpIni, EmpFin, AgenIni, AgenFin, CencosIni, CencosFin;
            EmpIni = ""; EmpFin = ""; AgenIni = ""; AgenFin = ""; CencosIni = ""; CencosFin = "";

            long dias; DateTime Fechavence = DateTime.MinValue; long vrbase; long VrMora; decimal I;
            int SW1; string mysql; string CODIGOTER; int Cuopen = 0;
            double saldot; string PeriodoCausa = " "; int NUmreg = 0; double Total = 0;
            int fila = 0;
            //cop_cuadredoc mYform = new cop_cuadredoc(pmyConeConect);
            //ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidacion de Mora", mYform);

            switch (Empresa)
            {
                case "Todos":
                    EmpIni = "0000";
                    EmpFin = "9999";
                    break;
                default:
                    EmpIni = Empresa;
                    EmpFin = Empresa;
                    break;
            }

            switch (Agencia)
            {
                case "Todos":
                    AgenIni = "0000";
                    AgenIni = "9999"; // VB bug preserved
                    break;
                default:
                    AgenIni = Agencia;
                    AgenIni = Agencia; // VB bug preserved
                    break;
            }

            switch (Cencosto)
            {
                case "Todos":
                    CencosIni = "00000000";
                    EmpFin = "99999999"; // VB bug preserved
                    break;
                default:
                    CencosIni = Cencosto;
                    CencosFin = Cencosto;
                    break;
            }

            string mysqlcopmora = "SELECT copmora.codigoter, copmora.lincred, copmora.numero,copmora.periodo_causa, copmora.periodo_contable, saldocapital,saldoextra,saldoInteres, saldoSeguro, saldoAdmon,SaldoOtros, copmora.Diasmora, fecUltliq, fecha_movto, copmae.clades, parame12.intmora, parame12.catec "
                                + ", cuoint,intatr, copmae.periodd,copmae.ciclod, copmae.clacuo,copmae.cuota,parame12.liqDiasMora  "
                                + ", saldoMora, Mora_causado, saldo_antMora,Mora_abono, saldoInteres,cuopen.intmor_causa, cuopen.salint_mora,cuopen.inte_mora,cuopen.intmor_causa,cuopen.intmor_pagado,cuopen.dias_Vemto,cuopen.dias_mora,cuopen.fecult_mora,copmae.tasaint, saldos.saldo"
                                + " FROM cop_copmora copmora inner join cop_cuopen cuopen on copmora.codigoter = cuopen.codigoter and copmora.lincred = cuopen.lincred and copmora.numero = cuopen.numero and copmora.periodo_causa = cuopen.periodo_causa "
                                + " inner join sys_maenit maenit on copmora.codigoter = maenit.codigoter "
                                + " inner join cop_maecar copmae on copmora.codigoter = copmae.codigoter and copmora.lincred = copmae.lincred and copmora.numero = copmae.numero inner join cop_saldos_vw saldos on saldos.codigoter = copmora.codigoter and saldos.lincred = copmora.lincred and saldos.numero = copmora.numero and saldos.periodo = " + Convert.ToDateTime(fecLiqui).ToString("yyyyMM")
                                + " inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred"
                                + " where copmora.periodo_contable = " + Convert.ToDateTime(fecLiqui).ToString("yyyyMM")
                                + " and maenit.empresa between '" + EmpIni + "' and '" + EmpFin + "'"
                                + " AND cuopen.fecha_movto <= '" + Convert.ToDateTime(fecLiqui).ToString(varini.PstForFec) + "' and copmora.lincred<>9999 ";

            DataSet Mycopmora = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(mysqlcopmora, pmyConeConect, "ProyectaLiquidaMora", ref Mycopmora, "LiqMora");
            NUmreg = Mycopmora.Tables["LiqMora"].Rows.Count;

            switch (FormaLiq)
            {
                case 0:
                    //msgbarra.ValorMinimoMaximo(0, NUmreg, "Liquidacion de mora");
                    //msgbarra.Show();
                    break;
                case 1:
                    Myprogress.Maximum = NUmreg;
                    Myprogress.Minimum = 0;
                    Myprogress.Value = 1;
                    Myprogress.Step = 1;
                    break;
            }

            if (Convert.ToDateTime(fecLiqui).ToString("dd") == "31")
            {
                fecLiqui = Convert.ToDateTime(fecLiqui).ToString("yyyy/MM/30");
            }

            try
            {
                while (fila < NUmreg)
                {
                    DataRow row = Mycopmora.Tables[0].Rows[fila];
                    Application.DoEvents();
                    SW1 = 0;

                    if (row["intmora"].ToString() == "N" && row["liqdiasmora"].ToString() == "N")
                    {
                        SW1 = 1;
                    }

                    switch (SW1)
                    {
                        case 0:
                            if (row["fecultliq"] is DBNull)
                            {
                                if (row["fecha_movto"] is DBNull)
                                    SW1 = 1;
                                else
                                    Fechavence = Convert.ToDateTime(row["fecha_movto"]);
                            }
                            else if (row["fecultliq"].ToString() == "1950-01-01")
                            {
                                if (row["fecha_movto"] is DBNull)
                                    SW1 = 1;
                                else
                                    Fechavence = Convert.ToDateTime(row["fecha_movto"]);
                            }
                            else
                            {
                                Fechavence = Convert.ToDateTime(row["fecultliq"]);
                            }

                            if (Fechavence.ToString("dd") == "31")
                            {
                                Fechavence = Convert.ToDateTime(Fechavence.ToString("yyyy-MM-30"));
                            }

                            if (ClaseDsto > 0)
                            {
                                if (ClaseDsto != Convert.ToInt32(row["clades"]))
                                    SW1 = 1;
                            }

                            Total = Convert.ToDouble(row["saldoCapital"]) + Convert.ToDouble(row["saldoExtra"]) + Convert.ToDouble(row["saldoInteres"]);

                            if (Total == 0)
                                SW1 = 1;

                            if (Convert.ToDouble(row["saldo"]) == 0)
                                SW1 = 1;

                            if (Convert.ToDouble(row["saldoCapital"]) != 0 && Convert.ToInt32(row["lincred"]) < 1000 && Convert.ToDouble(row["saldo"]) == 0)
                                SW1 = 0;

                            switch (FormaLiq)
                            {
                                case 0:
                                    //msgbarra.PerformStep();
                                    break;
                                case 1:
                                    Myprogress.PerformStep();
                                    break;
                            }

                            if (SW1 == 0)
                            {
                                dias = CalculaDias(Convert.ToDateTime(fecLiqui), Fechavence);
                                switch (BaseLiquidacion)
                                {
                                    case 1:
                                        vrbase = Convert.ToInt64(row["saldoCapital"]) + Convert.ToInt64(row["saldoExtra"]);
                                        break;
                                    case 2:
                                        vrbase = Convert.ToInt64(row["saldoCapital"]) + Convert.ToInt64(row["saldoInteres"]) + Convert.ToInt64(row["saldoSeguro"])
                                                 + Convert.ToInt64(row["saldoAdmon"]) + Convert.ToInt64(row["saldoOtros"]);
                                        break;
                                    case 3:
                                    default:
                                        if (row["saldo"] is DBNull)
                                            saldot = 0;
                                        else
                                            saldot = Convert.ToDouble(row["saldo"]);
                                        vrbase = (long)(saldot - Convert.ToDouble(row["saldoCapital"]) - Convert.ToDouble(row["saldoExtra"]));
                                        break;
                                }

                                if (DiasGracia > 0)
                                {
                                    if (Convert.ToInt32(row["Diasmora"]) + dias <= DiasGracia)
                                    {
                                        vrbase = 0;
                                        dias = 0;
                                    }
                                }

                                if (row["intmora"].ToString() == "N")
                                    vrbase = 0;

                                if (TasaUsura > 0)
                                {
                                    Tasa = (Convert.ToDecimal(row["tasaint"]) - TasaUsura) / 100;
                                    if (Tasa < 0)
                                        I = Tasa * -1;
                                    else
                                        I = 0;
                                }
                                else
                                {
                                    I = (Tasa / 100);
                                }

                                VrMora = (long)((vrbase * I) / 30 * dias);

                                if (dias > 0)
                                {
                                    //this.GrabaMoraEnDescuentos(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), VrMora, 0, "M", row["periodo_causa"].ToString(), row["periodo_contable"].ToString(), pmyConeConect, dias, fecLiqui);
                                }
                            }
                            break;
                    }
                    fila += 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //msgbarra.Close();
            //msgbarra.Dispose();
            Mycopmora.Dispose();
        }

        // Overload with defaults
        public void ProyectaLiquidaMora(string fecLiqui, int ClaseDsto, decimal Tasa, int DiasGracia,
            decimal TasaUsura, int BaseLiquidacion, int FormaLiq, OdbcConnection pmyConeConect,
            ProgressBar Myprogress)
        {
            ProyectaLiquidaMora(fecLiqui, ClaseDsto, Tasa, DiasGracia, TasaUsura, BaseLiquidacion, FormaLiq, pmyConeConect, Myprogress, "Todos", "Todos", "Todos", " ");
        }

        private bool GrabaLiqMora(string codigoter, int lincred, double numero, DateTime fecliq,
            string periodo_causa, int baseliq, int diasliq, int valLiq, DateTime fecUltLiq,
            string empresa, decimal tasaliq, int diasgracia, decimal tasausu, int cladsto,
            int tipoliq, OdbcConnection myconnect, DateTime fechasys, string Usuario)
        {
            string stmysql; bool ok; string nombreusu = " ";
            //msgcofsys.BuscaUsuario(Usuario, myconnect, ref nombreusu);

            ok = this.BuscaLiqmora(codigoter, lincred, numero, fecliq, periodo_causa, myconnect);
            switch (ok)
            {
                case false:
                    stmysql = "insert into cop_liqmor (Codigoter,Lincred,Numero,FechaLiq,BaseLiq,DiasLiq,ValLiq,FecUltLiq,empresa,tasaliq,clades,diasgracia,tasausu,tipoliq,fechasystem,Periodo_causa,usuario,nomusuario) values ('"
                            + codigoter + "','" + lincred + "','" + numero + "','" + fecliq.ToString(varini.pstForfecyHora) + "','" + baseliq + "','" + diasliq + "','" + valLiq + "','" + fecUltLiq.ToString(varini.pstForfecyHora) + "','" + empresa + "','" + tasaliq + "','"
                            + cladsto + "','" + diasgracia + "','" + tasausu + "','" + tipoliq + "','" + DateTime.Now.ToString(varini.pstForfecyHora) + "','" + periodo_causa + "','" + Usuario + "','" + nombreusu + "')";
                    ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaLiqMora");
                    break;
                case true:
                    MessageBox.Show("Aqui es el problema " + codigoter + " -> " + lincred + " -> " + numero + " -> " + fecliq + " -> " + periodo_causa, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }

            return ok;
        }

        public bool BuscaLiqmora(string codigoter, int lincred, double numero, DateTime fecliq,
            string periodo_causa, OdbcConnection myconnect)
        {
            switch (varini.pstTipoBD.ToUpper())
            {
                case "DB2":
                    stmysql = "select diasliq as campo1, valliq as campo2 from cop_liqmor where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + numero + " and FechaLiq = '" + fecliq.ToString(varini.pstForfecyHora) + "' and Periodo_causa = " + periodo_causa;
                    break;
                default:
                    stmysql = "select diasliq as campo1, valliq as campo2 from cop_liqmor where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + numero + " and FechaLiq = '" + fecliq.ToString(varini.PstForFec) + "' and Periodo_causa = " + periodo_causa;
                    break;
            }

            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaLiqmora");

            return ok;
        }

        private int CalculaDias(DateTime fechaInicial, DateTime FechaFinal)
        {
            long DiasVencido;

            int anioIni, MesIni, DiaIni;
            int anioFin, MesFin, DiaFin;

            anioIni = Convert.ToInt32(FechaFinal.ToString("yyyy"));
            MesIni = Convert.ToInt32(FechaFinal.ToString("MM"));
            DiaIni = Convert.ToInt32(FechaFinal.ToString("dd"));

            anioFin = Convert.ToInt32(fechaInicial.ToString("yyyy"));
            MesFin = Convert.ToInt32(fechaInicial.ToString("MM"));
            DiaFin = Convert.ToInt32(fechaInicial.ToString("dd"));

            if (DiaFin > 30)
                DiaFin = 30;
            if (DiaIni > 30)
                DiaIni = 30;

            if (MesFin == 2)
            {
                if (DiaFin >= 28)
                    DiaFin = 30;
            }

            if (MesIni == 2)
            {
                if (DiaIni >= 28)
                    DiaIni = 30;
            }

            DiasVencido = (((anioFin - anioIni) * 360) + ((MesFin - MesIni) * 30) + (DiaFin - DiaIni));

            return (int)DiasVencido;
        }

        public bool BuscaTipoMovto(ref string Codmovto, OdbcConnection myconect, ref string Tipo_movto,
            ref string debcre, ref string ajucau, ref string nomres, ref string Cuenta)
        {
            string stmysql; bool ok;
            Codmovto = Strings.Right("00" + Codmovto, 2);
            stmysql = "select tipo_movto as campo1,debcre as campo2,ajucau as campo3,nomres as campo4 from cop_codmov where cod_movto = '" + Codmovto + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaTipoMovto", ref Tipo_movto, ref debcre, ref ajucau, ref nomres);
            stmysql = "select cuenta as campo1 from cop_codmov where cod_movto = '" + Codmovto + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaTipoMovto", ref Cuenta);
            return ok;
        }

        public DataSet CargaSolicitudAuxilios(string CodigoterInicial, string CodigoterFinal,
            double SolicitudInicial, double SolicitudFinal, DateTime Fecini, DateTime fecfin,
            string estado, OdbcConnection myconect)
        {
            string stmysql = null;
            DataSet DatSoli = new DataSet();
            string fechainistr = Fecini.ToString(varini.PstForFec);
            string fechafinstr = fecfin.ToString(varini.PstForFec);

            DateTime fechainiHora = Convert.ToDateTime(fechainistr + " 01:00:00");
            DateTime fechafinhora = Convert.ToDateTime(fechafinstr + " 23:59:59");

            if (varini.pstTipoBD.ToUpper() != "DB2")
            {
                stmysql = "select  Idsolaux as Solicitud,case cerrado when 'Y' then (case estado when 'A' then 'G' else estado end) else estado end as estado,fecha_sol as Fecha ,vlr_solicitado as Solicitado,nomres as Auxilio,aprobado from cop_solaux solaux inner join cop_auxilio aux on solaux.linea = aux.codigo "
                    + "where codigoter between '" + CodigoterInicial + "' and '" + CodigoterFinal + "'"
                    + " and Idsolaux between " + SolicitudInicial + " and " + SolicitudFinal + " and fecha_sol between '" + fechainiHora.ToString(varini.pstForfecyHora) + "' and '" + fechafinhora.ToString(varini.pstForfecyHora) + "'"
                    + " and estado in ('" + estado + "')";
            }
            else
            {
                stmysql = "select  Idsolaux as Solicitud,case cerrado when 'Y' then (case estado when 'A' then 'G' else estado end) else estado end as estado,fecha_sol as Fecha ,vlr_solicitado as Solicitado,nomres as Auxilio,aprobado from cop_solaux solaux inner join cop_auxilio aux on solaux.linea = aux.codigo "
                    + "where codigoter between '" + CodigoterInicial + "' and '" + CodigoterFinal + "'"
                    + " and Idsolaux between " + SolicitudInicial + " and " + SolicitudFinal + " and fecha_sol between '" + Fecini.ToString(varini.PstForFec) + "' and '" + fecfin.ToString(varini.PstForFec) + "'"
                    + " and estado in ('" + estado + "')";
            }

            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconect, "CargaSolicitudAuxilios", ref DatSoli, "TblAuxilios");
            return DatSoli;
        }

        public void GrabaSolicitudAuxliios(double idsolaux, OdbcConnection myconect, string codigoter,
            ref int linea, ref DateTime FecSol, ref DateTime FecAprobado, ref DateTime FecPago,
            ref string Aprobado, ref string Estado, ref double ValSolicitado, ref double ValAprobado,
            ref string ObLinea, ref string ObAprobado, ref string ObSolicitud, string cerrado)
        {
            ok = BuscaSolicitudAuxilio(idsolaux, myconect);
            if (ok)
            {
                stmysql = "update cop_solaux set observaciones_aprobado = '" + ObAprobado + "', estado = '" + Estado + "'"
                        + ", aprobado = '" + Aprobado + "',vlr_aprobado = " + ValAprobado + ", cerrado = '" + cerrado + "'"
                        + ", fecha_aprobado = '" + Strings.Format(FecAprobado, varini.PstForFec) + "', fecha_pago = '" + Strings.Format(FecPago, varini.PstForFec)
                        + "' where idsolaux = " + idsolaux;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaSolicitudAuxliios");
            }
        }

        // Overload with default cerrado
        public void GrabaSolicitudAuxliios(double idsolaux, OdbcConnection myconect, string codigoter,
            ref int linea, ref DateTime FecSol, ref DateTime FecAprobado, ref DateTime FecPago,
            ref string Aprobado, ref string Estado, ref double ValSolicitado, ref double ValAprobado,
            ref string ObLinea, ref string ObAprobado, ref string ObSolicitud)
        {
            GrabaSolicitudAuxliios(idsolaux, myconect, codigoter, ref linea, ref FecSol, ref FecAprobado,
                ref FecPago, ref Aprobado, ref Estado, ref ValSolicitado, ref ValAprobado,
                ref ObLinea, ref ObAprobado, ref ObSolicitud, "N");
        }

        public bool BuscaSolicitudAuxilio(double idsolaux, OdbcConnection myconect, ref string codigoter,
            ref int linea, ref DateTime FecSol, ref string FecAprobado, ref string FecPago,
            ref string Aprobado, ref string Estado, ref double ValSolicitado, ref double ValAprobado,
            ref string ObLinea, ref string ObAprobado, ref string ObSolicitud, ref string cerrado,
            ref string IdBenef)
        {
            string stmysql = null; bool ok;
            stmysql = "select sol.codigoter as campo1, linea as campo2 ,fecha_sol as campo3,fecha_aprobado as campo4 "
                    + " from cop_solaux sol left join sys_maenit maenit on sol.codigoter = maenit.codigoter inner join cop_auxilio parame12 on parame12.codigo = sol.linea where  sol.codigoter = maenit.codigoter and idsolaux = " + idsolaux;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaSolicitud", ref codigoter, ref linea, ref FecSol, ref FecAprobado);

            stmysql = "select fecha_pago as campo1,aprobado as campo2,sol.estado as campo3,vlr_solicitado as campo4 "
                    + "from cop_solaux sol left join sys_maenit maenit on sol.codigoter = maenit.codigoter inner join cop_auxilio parame12 on parame12.codigo = sol.linea where  sol.codigoter = maenit.codigoter and idsolaux = " + idsolaux;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaSolicitud", ref FecPago, ref Aprobado, ref Estado, ref ValSolicitado);

            stmysql = "select vlr_aprobado as campo1,observaciones_linea as campo2,observaciones_aprobado as campo3,observaciones_sol as campo4 "
                    + "from cop_solaux sol left join sys_maenit maenit on sol.codigoter = maenit.codigoter inner join cop_auxilio parame12 on parame12.codigo = sol.linea where  sol.codigoter = maenit.codigoter and idsolaux = " + idsolaux;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaSolicitud", ref ValAprobado, ref ObLinea, ref ObAprobado, ref ObSolicitud);  
            stmysql = "select cerrado as campo1,IdBenef as campo2 "
                    + "from cop_solaux sol left join sys_maenit maenit on sol.codigoter = maenit.codigoter inner join cop_auxilio parame12 on parame12.codigo = sol.linea where  sol.codigoter = maenit.codigoter and idsolaux = " + idsolaux;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaSolicitud", ref cerrado, ref IdBenef);

            return ok;
        }

        // Overload with no optional params
        public bool BuscaSolicitudAuxilio(double idsolaux, OdbcConnection myconect)
        {
            string codigoter = "99999999999999"; int linea = 0; DateTime FecSol = new DateTime(1950, 1, 1);
            string FecAprobado = "1/1/1950", FecPago = "1/1/1950", Aprobado = "O", Estado = "P";
            double ValSolicitado = 0, ValAprobado = 0;
            string ObLinea = " ", ObAprobado = " ", ObSolicitud = " ", cerrado = "N", IdBenef = "99999999999999";
            return BuscaSolicitudAuxilio(idsolaux, myconect, ref codigoter, ref linea, ref FecSol,
                ref FecAprobado, ref FecPago, ref Aprobado, ref Estado, ref ValSolicitado, ref ValAprobado,
                ref ObLinea, ref ObAprobado, ref ObSolicitud, ref cerrado, ref IdBenef);
        }

        public bool BuscaLineaAuxilio(int IdLinea, OdbcConnection myconnect, ref string Cuenta,
            ref string Descripcion, ref string Nomres, ref string ClaAux)
        {
            string stmysql; bool ok;
            stmysql = "select cuenta as campo1, nombre as campo2,nomres as campo3,claaux as campo4 "
                    + "from cop_auxilio where codigo = " + IdLinea;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaLineaAuxilio", ref Cuenta, ref Descripcion, ref Nomres, ref ClaAux);
            return ok;
        }

        public void buscaPeriodo(string Modulo, OdbcConnection myconnet, ref DateTime FechaIni,
            ref DateTime fechaFin, DateTime fechaValidar, ref string estado, ref string Periodo, string anio)
        {
            string fechaInicial = "0", fechaFinal = "0", estadoFecha = "A";
            string anioactual;
            int Anoperiodo = 0; string MesPeriodo = "0";
            string stmysql;

            if (anio == "9999")
                anioactual = DateTime.Now.ToString("yyyy");
            else
                anioactual = anio;

            stmysql = "select anio as campo1, periodo as campo2  from sys_periodo where  modulo = '" + Modulo + "' and anio = '" + anioactual + "'";
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnet, "buscaPeriodo", ref Anoperiodo, ref MesPeriodo);

            if (Periodo != "999999" && Periodo != "")
            {
                fechaValidar = Convert.ToDateTime(Periodo.Substring(0, 4) + "/" + Periodo.Substring(4, 2) + "/" + "01");
            }

            if (fechaValidar.ToString("M/d/yyyy") == "1/1/1950")
            {
                Periodo = Anoperiodo.ToString() + Strings.Right("00" + ((MesPeriodo == "13") ? "12" : MesPeriodo), 2);
                fechaValidar = Convert.ToDateTime(Periodo.Substring(0, 4) + "/" + Periodo.Substring(4, 2) + "/" + "01");
            }

            switch (fechaValidar.ToString("MM"))
            {
                case "01":
                    fechaInicial = "fecha_ini01"; fechaFinal = "fecha_fin01"; estadoFecha = "estado_01"; break;
                case "02":
                    fechaInicial = "fecha_ini02"; fechaFinal = "fecha_fin02"; estadoFecha = "estado_02"; break;
                case "03":
                    fechaInicial = "fecha_ini03"; fechaFinal = "fecha_fin03"; estadoFecha = "estado_03"; break;
                case "04":
                    fechaInicial = "fecha_ini04"; fechaFinal = "fecha_fin04"; estadoFecha = "estado_04"; break;
                case "05":
                    fechaInicial = "fecha_ini05"; fechaFinal = "fecha_fin05"; estadoFecha = "estado_05"; break;
                case "06":
                    fechaInicial = "fecha_ini06"; fechaFinal = "fecha_fin06"; estadoFecha = "estado_06"; break;
                case "07":
                    fechaInicial = "fecha_ini07"; fechaFinal = "fecha_fin07"; estadoFecha = "estado_07"; break;
                case "08":
                    fechaInicial = "fecha_ini08"; fechaFinal = "fecha_fin08"; estadoFecha = "estado_08"; break;
                case "09":
                    fechaInicial = "fecha_ini09"; fechaFinal = "fecha_fin09"; estadoFecha = "estado_09"; break;
                case "10":
                    fechaInicial = "fecha_ini10"; fechaFinal = "fecha_fin10"; estadoFecha = "estado_10"; break;
                case "11":
                    fechaInicial = "fecha_ini11"; fechaFinal = "fecha_fin11"; estadoFecha = "estado_11"; break;
                case "12":
                    fechaInicial = "fecha_ini12"; fechaFinal = "fecha_fin12"; estadoFecha = "estado_12"; break;
                case "13":
                    fechaInicial = "fecha_ini13"; fechaFinal = "fecha_fin13"; estadoFecha = "estado_13"; break;
            }

            if (fechaValidar.ToString("M/d/yyyy") != "1/1/1950")
            {
                Periodo = fechaValidar.ToString("yyyyMM");
                stmysql = "select " + fechaInicial + " as campo1," + fechaFinal + " as campo2," + estadoFecha + " as campo3 from sys_periodo where  modulo = '" + Modulo + "' and anio = '" + anioactual + "'";
               // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnet, "buscaPeriodo", ref FechaIni, ref fechaFin, ref estado);
            }
            else
            {
                Periodo = Anoperiodo.ToString() + MesPeriodo;
            }
        }

        // Overload with defaults
        public void buscaPeriodo(string Modulo, OdbcConnection myconnet)
        {
            DateTime FechaIni = new DateTime(1950, 1, 1); DateTime fechaFin = new DateTime(1950, 1, 1);
            DateTime fechaValidar = new DateTime(1950, 1, 1); string estado = "C"; string Periodo = "999999";
            buscaPeriodo(Modulo, myconnet, ref FechaIni, ref fechaFin, fechaValidar, ref estado, ref Periodo, "9999");
        }

        // Overload with fecfin + fechaValidar
        public void buscaPeriodo(string Modulo, OdbcConnection myconnet, ref DateTime FechaIni,
            ref DateTime fechaFin, DateTime fechaValidar)
        {
            string estado = "C"; string Periodo = "999999";
            buscaPeriodo(Modulo, myconnet, ref FechaIni, ref fechaFin, fechaValidar, ref estado, ref Periodo, "9999");
        }

        // Overload with estado
        public void buscaPeriodo(string Modulo, OdbcConnection myconnet, ref DateTime FechaIni,
            ref DateTime fechaFin, DateTime fechaValidar, ref string estado)
        {
            string Periodo = "999999";
            buscaPeriodo(Modulo, myconnet, ref FechaIni, ref fechaFin, fechaValidar, ref estado, ref Periodo, "9999");
        }

        // Overload with Periodo + anio
        public void buscaPeriodo(string Modulo, OdbcConnection myconnet, ref DateTime FechaIni,
            ref DateTime fechaFin, DateTime fechaValidar, ref string estado, ref string Periodo)
        {
            buscaPeriodo(Modulo, myconnet, ref FechaIni, ref fechaFin, fechaValidar, ref estado, ref Periodo, "9999");
        }

        public bool ValidaPrioridad(string codigoter, int lincred, double Numconse, int periodo,
            string TipoMovto, OdbcConnection myconnect, ref int ItemPrioridad)
        {
            double SaldoCapital = 0; Array Prioridades; double valor = 0; int item;
            double SaldoExtra = 0, SaldoInteres = 0, SaldoMora = 0;
            double SaldoSeguro = 0, SaldoAdmon = 0, SaldoOtros = 0;
            string SaldoAportes = "0"; double SaldoAhorros = 0, SaldoServicios = 0;

            this.BuscaSaldosCuotasPendientes(codigoter, lincred, Numconse, lincred, Numconse, periodo, myconnect,
                ref SaldoCapital, ref SaldoExtra, ref SaldoInteres, ref SaldoMora, ref SaldoSeguro,
                ref SaldoAdmon, ref SaldoOtros, ref SaldoAportes, ref SaldoAhorros, ref SaldoServicios);

            Prioridades = this.BuscaPrioridad(myconnect);

            for (item = Prioridades.GetLowerBound(0); item <= Prioridades.GetUpperBound(0); item++)
            {
                string prioridadItem = Prioridades.GetValue(item).ToString();
                switch (prioridadItem)
                {
                    case "Capital":
                    case "Extras":
                        if (TipoMovto == prioridadItem)
                        {
                            if (valor == 0)
                            { ItemPrioridad = item; return true; }
                            else
                            { ItemPrioridad = item; return false; }
                        }
                        valor = valor + SaldoCapital + SaldoExtra;
                        break;
                    case "Interes":
                        if (TipoMovto == prioridadItem)
                        {
                            if (valor == 0)
                            { ItemPrioridad = item; return true; }
                            else
                            { ItemPrioridad = item; return false; }
                        }
                        valor = valor + SaldoInteres;
                        break;
                    case "Mora":
                        if (TipoMovto == prioridadItem)
                        {
                            if (valor == 0)
                            { ItemPrioridad = item; return true; }
                            else
                            { ItemPrioridad = item; return false; }
                        }
                        valor = valor + SaldoMora;
                        break;
                    case "Admon":
                        if (TipoMovto == prioridadItem)
                        {
                            if (valor == 0)
                            { ItemPrioridad = item; return true; }
                            else
                            { ItemPrioridad = item; return false; }
                        }
                        valor = valor + SaldoAdmon;
                        break;
                    case "Seguro":
                        if (TipoMovto == prioridadItem)
                        {
                            if (valor == 0)
                            { ItemPrioridad = item; return true; }
                            else
                            { ItemPrioridad = item; return false; }
                        }
                        valor = valor + SaldoSeguro;
                        break;
                }
            }
            return false;
        }

        public void BuscaFecMaxNovedad(ref string codigoter, OdbcConnection myconect, ref string FecMaxNov)
        {
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "select max(FecCaduca) as campo1 from cop_caunov where codigoter = '" + codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaFecMaxNovedad", ref FecMaxNov);
            if (Information.IsDate(FecMaxNov) == false)
                FecMaxNov = "01/01/1950";
        }

        public bool BuscaAsociado(ref string codigoter, OdbcConnection myconect, ref string Nombre,
            ref string Nit, ref string agencia, ref string Telefono, ref string Direccion,
            ref string email, ref string celular, ref int natjur, ref string banco,
            ref string Cuentabanco, ref string password, ref string ConsultaEnLinea,
            ref string EstatusConsulta, ref string Periodd, ref string Empresa,
            ref string Clase, ref string Estado)
        {
            string stmysql; bool ok; string apellido = " ";

            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "select apellido as campo1, nombre as campo2, nit as campo3, agencia as campo4 from sys_maenit where codigoter = '" + codigoter + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaAsociado", ref apellido, ref Nombre, ref Nit, ref agencia);
            Nombre = apellido + " " + Nombre;

            stmysql = "select telefono1 as campo1, direccion as campo2, email as campo3, movil as campo4 from sys_maenit where codigoter = '" + codigoter + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaAsociado", ref Telefono, ref Direccion, ref email, ref celular);

            stmysql = "select  natjur as campo1,codigo_banco as campo2, cuenta_banco as campo3, password as campo4 from sys_maenit where codigoter = '" + codigoter + "'";
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaAsociado", ref natjur, ref banco, ref Cuentabanco, ref password);

            stmysql = "select ConsultaEnLinea as campo1,EstatusConsulta as campo2, PERIODO_DESTO as campo3,empresa as campo4 from sys_maenit where codigoter = '" + codigoter + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaAsociado", ref ConsultaEnLinea, ref EstatusConsulta, ref Periodd, ref Empresa);

            stmysql = "select clase as campo1,estado as campo2 from sys_maenit where codigoter = '" + codigoter + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaAsociado", ref Clase, ref Estado);

            return ok;
        }

        // Overload with fewer params
        public bool BuscaAsociado(ref string codigoter, OdbcConnection myconect)
        {
            string Nombre = " ", Nit = " ", agencia = "9999", Telefono = " ", Direccion = " ";
            string email = " ", celular = " "; int natjur = 0; string banco = "9999", Cuentabanco = " ";
            string password = " ", ConsultaEnLinea = " ", EstatusConsulta = " ", Periodd = " ";
            string Empresa = "9999", Clase = "5", Estado = "R";
            return BuscaAsociado(ref codigoter, myconect, ref Nombre, ref Nit, ref agencia,
                ref Telefono, ref Direccion, ref email, ref celular, ref natjur, ref banco,
                ref Cuentabanco, ref password, ref ConsultaEnLinea, ref EstatusConsulta,
                ref Periodd, ref Empresa, ref Clase, ref Estado);
        }

        public bool BuscarGarantia(string codigoter, int lincred, double numero, OdbcConnection myconnect,
            ref string matricula, ref string TipoGarantia, ref string Descripcion,
            ref double AvaluoCatastral, ref double AvaluoComercial, ref string TieneSeguro,
            ref string NumeroPoliza, ref DateTime FecIniGarantia, ref DateTime FecCancGarantia,
            ref DateTime FecVemtoGarantia, ref string NitAseguradora, ref string NombreAseguradora,
            ref string EstadoGarantia, ref string usuario, ref double Porcentaje,
            ref string CodFiador1, ref string CodFiador2, ref string CodFiador3, ref string CodFiador4,
            ref int Plazo, ref double Saldo, ref double NumCdat)
        {
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            //stmysql = "select matricula as campo1,tipo_garantia as campo2,descripcion_garantia as campo3,avaluo_catastral as campo4 from cop_garantia where codigoter ='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGarantia", ref matricula, ref TipoGarantia, ref Descripcion, ref AvaluoCatastral);

            //stmysql = "select avaluo_ccial as campo1,tiene_seguro as campo2,NRO_POLIZA as campo3,FECINI_GARANTIA as campo4 from cop_garantia where codigoter ='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGarantia", ref AvaluoComercial, ref TieneSeguro, ref NumeroPoliza, ref FecIniGarantia);

            //stmysql = "select FEC_CANCEL_GARANTIA as campo1,FECHA_VEMTO as campo2,NIT_ASEGURADORA as campo3,NOMBRE_ASEGU as campo4 from cop_garantia where codigoter ='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGarantia", ref FecCancGarantia, ref FecVemtoGarantia, ref NitAseguradora, ref NombreAseguradora);

            //stmysql = "select ESTADO_GARANTIA as campo1,USUARIO as campo2,POR_SEGURADO as campo3,FIADOR1 as campo4 from cop_garantia where codigoter ='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGarantia", ref EstadoGarantia, ref usuario, ref Porcentaje, ref CodFiador1);

            //stmysql = "select FIADOR2 as campo1,FIADOR3 as campo2,FIADOR4 as campo3,PLAZO as campo4 from cop_garantia where codigoter ='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGarantia", ref CodFiador2, ref CodFiador3, ref CodFiador4, ref Plazo);

            //stmysql = "select SALDO as campo1,NumCdat as campo2 from cop_garantia where codigoter ='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGarantia", ref Saldo, ref NumCdat);

            return ok;
        }

        public bool BuscarAsociadoPorFicha(string ficha, OdbcConnection myconnetc, ref string codigoter,
            ref string Nombre, ref string estado)
        {
            string apellido = " ", nom = " ";
            stmysql = "select codigoter as campo1, apellido as campo2, nombre as campo3, estado as campo4 from sys_maenit where codigo_empresa='" + ficha + "' and estado<>'T'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnetc, "BuscarAsociadoPorFicha", ref codigoter, ref apellido, ref nom, ref estado);
            Nombre = apellido + " " + nom;
            return ok;
        }

        public bool BsucarCompania(string Codigo, OdbcConnection myconect, ref string CuentaUtilidad,
            ref string CptoCap, ref string CalculaSaldo, ref string CptoAfavor, ref int TipoLiq,
            ref decimal TasaMora, ref int DiasGracia, ref decimal TasaUsura, ref int BaseLiq,
            ref int CtrlConse, ref int ConseCreditos, ref string CptoRevapo, ref string nit,
            ref string Direccion, ref string nomres, ref string CptoServicios, ref string mora_retirados)
        {
            string stmysql; bool ok;
            stmysql = "select CUENTACON2 as campo1, CPTO_CAPITAL as campo2,CALCULA_SALDO as campo3, sobra as campo4 from sys_compania where CODIGO = '" + Codigo + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BsucarCompania", ref CuentaUtilidad, ref CptoCap, ref CalculaSaldo, ref CptoAfavor);

            stmysql = "select tipo_liquidacion as campo1, Tasa_mora as campo2,Dias_gracia as campo3, Tasa_usura as campo4 from sys_compania where CODIGO = '" + Codigo + "'";
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BsucarCompania", ref TipoLiq, ref TasaMora, ref DiasGracia, ref TasaUsura);

            stmysql = "select Base_liquidacion as campo1, CtrlConse as campo2, ConseCreditos as campo3,cptorevapo as campo4 from sys_compania where CODIGO = '" + Codigo + "'";
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BsucarCompania", ref BaseLiq, ref CtrlConse, ref ConseCreditos, ref CptoRevapo);

            stmysql = "select NIT as campo1, Direccion as campo2, nomres as campo3, cpto_servi as campo4 from sys_compania where CODIGO = '" + Codigo + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BsucarCompania", ref nit, ref Direccion, ref nomres, ref CptoServicios);

            stmysql = "select mora_retirados as campo1 from sys_compania where CODIGO = '" + Codigo + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BsucarCompania", ref mora_retirados);
            return ok;
        }

        public bool BuscaCuotaPendiente(string codigoter, int Lincred, double ConseLinea,
            int PeriodoCausa, int PeriodoContable, OdbcConnection MyConnet,
            ref double CapitalCausado, ref double ExtraCausado, ref double InteresCausado,
            ref double MoraCausado, ref double SeguroCausado, ref double AdmonCausado)
        {
            string stmysql;
            stmysql = "select Capital_causado as campo1,Extra_causado as campo2,Interes_causado as campo3,Mora_causado as campo4  from cop_copmora  "
                    + " where codigoter = '" + codigoter + "' and lincred = " + Lincred + " and numero  = " + ConseLinea + " and periodo_causa = " + PeriodoCausa + " and periodo_contable = " + PeriodoContable;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, MyConnet, "BuscaCuotaPendiente", ref CapitalCausado, ref ExtraCausado, ref InteresCausado, ref MoraCausado);

            stmysql = "select Seguro_causado as campo1,Admon_causado as campo2  from cop_copmora  "
                    + " where codigoter = '" + codigoter + "' and lincred = " + Lincred + " and numero  = " + ConseLinea + " and periodo_causa = " + PeriodoCausa + " and periodo_contable = " + PeriodoContable;
            //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, MyConnet, "BuscaCuotaPendiente", ref SeguroCausado, ref AdmonCausado);
            return ok;
        }

        public virtual void BuscaSaldosCuotasPendientes(string codigoter, int LincredInicial,
            double NumeroInicial, int LincredFinal, double NumeroFinal, int Periodo_contable,
            OdbcConnection Connection, ref double SaldoCapital, ref double SaldoExtra,
            ref double SaldoInteres, ref double SaldoMora, ref double SaldoSeguro,
            ref double saldoAdmon, ref double SaldoOtros, ref string SaldoAportes,
            ref double saldoAhorros, ref double SaldoServicios, ref double Saldo,
            ref double TotalPend, ref string Clades)
        {
            string stmysql; int i = 0;
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            stbuilder.Append("SELECT sum(SaldoCapital) as SaldoCapital, sum(SaldoExtra) as SaldoExtra,sum(SaldoInteres) as SaldoInteres,");
            stbuilder.Append("sum(SaldoMora) as SaldoMora,sum(SaldoSeguro) as SaldoSeguro,sum(SaldoAdmon) as SaldoAdmon,sum(SaldoOtros) as SaldoOtros ");
            stbuilder.Append("from cop_copmora where codigoter = '" + codigoter + "' and lincred between " + LincredInicial + " and " + LincredFinal);
            stbuilder.Append(" and numero between " + NumeroInicial + " and " + NumeroFinal + " and Periodo_contable = " + Periodo_contable);
            stbuilder.Append(" group by codigoter,lincred,numero");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "BuscaSaldosCuotasPendientes", ref dsdata, "tblcuopen");
            while (i < dsdata.Tables["tblcuopen"].Rows.Count)
            {
                DataRow row = dsdata.Tables["tblcuopen"].Rows[i];
                SaldoCapital += (row["SaldoCapital"] is DBNull) ? 0 : Convert.ToDouble(row["SaldoCapital"]);
                SaldoExtra += (row["SaldoExtra"] is DBNull) ? 0 : Convert.ToDouble(row["SaldoExtra"]);
                SaldoInteres += (row["SaldoInteres"] is DBNull) ? 0 : Convert.ToDouble(row["SaldoInteres"]);
                SaldoMora += (row["SaldoMora"] is DBNull) ? 0 : Convert.ToDouble(row["SaldoMora"]);
                SaldoSeguro += (row["SaldoSeguro"] is DBNull) ? 0 : Convert.ToDouble(row["SaldoSeguro"]);
                saldoAdmon += (row["SaldoAdmon"] is DBNull) ? 0 : Convert.ToDouble(row["SaldoAdmon"]);
                SaldoOtros += (row["SaldoOtros"] is DBNull) ? 0 : Convert.ToDouble(row["SaldoOtros"]);
                i = i + 1;
            }

            stmysql = "SELECT sum(mor.SaldoCapital) as campo1 "
                     + " from cop_copmora mor inner join cop_concar12 car12 on mor.lincred = car12.lincred "
                     + "where mor.codigoter = '" + codigoter + "' and car12.codahor = '1' and mor.Periodo_contable = " + Periodo_contable;
            this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaSaldosCuotasPendientes", ref SaldoAportes);

            stmysql = "SELECT sum(mor.SaldoCapital) as campo1 "
                     + " from cop_copmora mor inner join cop_concar12 car12 on mor.lincred = car12.lincred "
                     + "where mor.codigoter = '" + codigoter + "' and car12.codahor = '2' and mor.Periodo_contable = " + Periodo_contable;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaSaldosCuotasPendientes", ref saldoAhorros);

            stmysql = "SELECT sum(mor.SaldoCapital) as campo1 "
                     + " from cop_copmora mor inner join cop_concar12 car12 on mor.lincred = car12.lincred "
                     + "where mor.codigoter = '" + codigoter + "' and car12.codahor = '3' and mor.Periodo_contable = " + Periodo_contable;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, Connection, "BuscaSaldosCuotasPendientes", ref SaldoServicios);

            stbuilder.Remove(0, stbuilder.Length);
            stbuilder.Append("SELECT salmae.Saldo,salmae.clades from cop_salmaecar salmae ");
            stbuilder.Append("inner join cop_maecar copmae on salmae.codigoter = copmae.codigoter and salmae.lincred = copmae.lincred and salmae.numero = copmae.numero ");
            stbuilder.Append("where salmae.codigoter = '" + codigoter + "' and salmae.lincred between " + LincredInicial + " and " + LincredFinal);
            stbuilder.Append(" and salmae.numero between " + NumeroInicial + " and " + NumeroFinal + " and salmae.PERIODO = " + Periodo_contable);

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "BuscaSaldosCuotasPendientes", ref dsdata, "tblsaldos");
            i = 0;
            while (i < dsdata.Tables["tblsaldos"].Rows.Count)
            {
                DataRow row = dsdata.Tables["tblsaldos"].Rows[i];
                if (row["Saldo"] is DBNull)
                    Saldo += 0;
                else
                    Saldo += (Convert.ToDouble(row["Saldo"]) < 0) ? Convert.ToDouble(row["Saldo"]) * -1 : Convert.ToDouble(row["Saldo"]);

                if (row["clades"] is DBNull)
                    Clades = "0";
                else
                    Clades = row["clades"].ToString();
                i = i + 1;
            }

            TotalPend = SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + saldoAdmon;
        }

        // Overload with fewer params (no Saldo, TotalPend, Clades)
        public virtual void BuscaSaldosCuotasPendientes(string codigoter, int LincredInicial,
            double NumeroInicial, int LincredFinal, double NumeroFinal, int Periodo_contable,
            OdbcConnection Connection, ref double SaldoCapital, ref double SaldoExtra,
            ref double SaldoInteres, ref double SaldoMora, ref double SaldoSeguro,
            ref double saldoAdmon, ref double SaldoOtros, ref string SaldoAportes,
            ref double saldoAhorros, ref double SaldoServicios)
        {
            double Saldo = 0; double TotalPend = 0; string Clades = "0";
            BuscaSaldosCuotasPendientes(codigoter, LincredInicial, NumeroInicial, LincredFinal,
                NumeroFinal, Periodo_contable, Connection, ref SaldoCapital, ref SaldoExtra,
                ref SaldoInteres, ref SaldoMora, ref SaldoSeguro, ref saldoAdmon, ref SaldoOtros,
                ref SaldoAportes, ref saldoAhorros, ref SaldoServicios, ref Saldo, ref TotalPend, ref Clades);
        }

        // Overload with PeriodoCausado (second VB overload)
        public virtual void BuscaSaldosCuotasPendientes(string codigoter, int LincredInicial,
            double NumeroInicial, int LincredFinal, double NumeroFinal, int Periodo_contable,
            string PeriodoCausado, OdbcConnection Connection, ref double SaldoCapital,
            ref double SaldoExtra, ref double SaldoInteres, ref double SaldoMora,
            ref double SaldoSeguro, ref double saldoAdmon, ref double SaldoOtros,
            ref string SaldoAportes, ref double saldoAhorros, ref double SaldoServicios,
            ref double Saldo, ref double TotalPend)
        {
            string stmysql;
            DataSet dsdata = new DataSet();

            stmysql = "SELECT sum(SaldoCapital) as SaldoCapital, sum(SaldoExtra) as SaldoExtra,sum(SaldoInteres) as SaldoInteres, sum(SaldoMora) as SaldoMora,"
                      + " sum(SaldoSeguro) as SaldoSeguro,sum(SaldoAdmon) as SaldoAdmon,sum(SaldoOtros) as SaldoOtros "
                      + " from cop_copmora mor inner join cop_salmaecar sal on mor.codigoter=sal.codigoter and mor.lincred=sal.lincred and mor.numero=sal.numero and sal.periodo=mor.Periodo_contable "
                      + "where mor.codigoter = '" + codigoter + "' and mor.lincred between " + LincredInicial + " and " + LincredFinal + " and mor.numero between " + NumeroInicial + " and " + NumeroFinal
                      + " and periodo_causa <= '" + PeriodoCausado + "' And Periodo_contable = " + Periodo_contable + " and ((mor.lincred>=1000 and sal.saldo>0) or mor.lincred<1000)";

            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, Connection, "BuscaSaldosCuotasPendientes", ref dsdata, "tblcuopen", true);
            if (ok)
            {
                DataRow row = dsdata.Tables["tblcuopen"].Rows[0];
                SaldoCapital = Convert.ToDouble(row["SaldoCapital"]);
                SaldoExtra = Convert.ToDouble(row["SaldoExtra"]);
                SaldoInteres = Convert.ToDouble(row["SaldoInteres"]);
                SaldoMora = Convert.ToDouble(row["SaldoMora"]);
                SaldoSeguro = Convert.ToDouble(row["SaldoSeguro"]);
                saldoAdmon = Convert.ToDouble(row["SaldoAdmon"]);
                SaldoOtros = Convert.ToDouble(row["SaldoOtros"]);
            }
            else
            {
                SaldoCapital = 0;
                SaldoExtra = 0;
                SaldoInteres = 0;
                SaldoMora = 0;
                SaldoSeguro = 0;
                saldoAdmon = 0;
                SaldoOtros = 0;
            }

            TotalPend = SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + saldoAdmon + SaldoOtros;
        }

        public void CargaCuotasPendientes(string codigoter, int Periodo, ref DataGridView lstView,
            OdbcConnection myconnect, ref double VlrTotal, DateTime FechaCorte)
        {
            int Items = 0; Array Prioridades; double Total = 0; double saldo;
            string[] Fila = new string[11]; int sw1 = 0; DateTime fecfin = DateTime.MinValue; int canreg = 0; int tfila = 0;

            if (FechaCorte.ToString("M/d/yyyy") == "1/1/1950")
                FechaCorte = DateTime.Now;

            this.buscaPeriodo("copc", myconnect, ref FechaCorte, ref fecfin, FechaCorte);

            StringBuilder StBuilder = new StringBuilder();
            StBuilder.Append("select copmae.codigoter, copmae.lincred, copmae.numero,salmaecar.cuota,salmaecar.clades,saldo,");
            StBuilder.Append("sum(saldoCapital) as saldoCapital,sum(Saldoextra) as Saldoextra,sum(saldoInteres) as saldoInteres,sum(SaldoMora) as SaldoMora,");
            StBuilder.Append("sum(SaldoSeguro) as SaldoSeguro,sum(Saldoadmon) as Saldoadmon,sum(SaldoOtros) as SaldoOtros,");
            StBuilder.Append("sum(saldoCapital + Saldoextra + saldoInteres + SaldoMora + SaldoSeguro + Saldoadmon + SaldoOtros) as Total ");
            StBuilder.Append("from cop_maecar copmae ");
            StBuilder.Append("left join cop_salmaecar salmaecar on copmae.codigoter = salmaecar.codigoter and copmae.lincred = salmaecar.lincred and ");
            StBuilder.Append("copmae.numero = salmaecar.numero and salmaecar.periodo = " + Periodo);
            StBuilder.Append(" left join cop_copmora copmora on copmae.codigoter = copmora.codigoter and copmae.lincred = copmora.lincred and ");
            StBuilder.Append("copmae.numero = copmora.numero and periodo_contable =" + Periodo);
            StBuilder.Append(" where copmae.codigoter = '" + codigoter + "' and copmae.fecfact <= '" + fecfin.ToString(varini.PstForFec) + "' ");
            StBuilder.Append("group by copmae.codigoter, copmae.lincred, copmae.numero,salmaecar.cuota,salmaecar.clades,saldo ");
            StBuilder.Append("order by copmae.codigoter, copmae.lincred, copmae.numero");

            Prioridades = BuscaPrioridad(myconnect);
            lstView.Rows.Clear();

            CargaPrioridades(lstView, ref Prioridades, myconnect, true);

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "CargaCuotasPendientes", ref myRead, "TblCuoPend");
            canreg = myRead.Tables["TblCuoPend"].Rows.Count;

            while (tfila < canreg)
            {
                DataRow row = myRead.Tables["TblCuoPend"].Rows[tfila];
                sw1 = 0;
                Fila[0] = row["lincred"].ToString();
                Fila[1] = row["numero"].ToString();

                if (!(row["clades"] is DBNull))
                {
                    switch (row["clades"].ToString())
                    {
                        case "1": Fila[2] = "N"; break;
                        case "2": Fila[2] = "C"; break;
                        default: Fila[2] = row["clades"].ToString(); break;
                    }
                }
                else
                {
                    Fila[2] = "";
                }

                if (row["saldo"] is DBNull)
                    saldo = 0;
                else
                    saldo = Convert.ToDouble(row["saldo"]);

                if (row["cuota"] is DBNull)
                    row["cuota"] = 0;

                Fila[3] = saldo.ToString("###,###,###.00");

                if (row["SaldoCapital"] is DBNull)
                    Total = 0;
                else
                    Total = Convert.ToDouble(row["Total"]);

                Fila[4] = Total.ToString("###,###,###.00");

                if (Convert.ToInt32(row["lincred"]) < 1000 && Convert.ToDouble(row["cuota"]) == 0 && saldo == 0)
                    sw1 = 1;
                if (Convert.ToInt32(row["lincred"]) >= 1000 && saldo == 0)
                    sw1 = 1;

                if (sw1 == 0)
                {
                    for (Items = Prioridades.GetLowerBound(0); Items <= Prioridades.GetUpperBound(0); Items++)
                    {
                        switch (Prioridades.GetValue(Items).ToString())
                        {
                            case "Capital":
                                Fila[Items + 5] = (row["SaldoCapital"] is DBNull) ? "0" : (Convert.ToDouble(row["SaldoCapital"]) + Convert.ToDouble(row["Saldoextra"])).ToString();
                                break;
                            case "Interes":
                                Fila[Items + 5] = (row["saldoInteres"] is DBNull) ? "0" : row["saldoInteres"].ToString();
                                break;
                            case "Mora":
                                Fila[Items + 5] = (row["SaldoMora"] is DBNull) ? "0" : row["SaldoMora"].ToString();
                                break;
                            case "Admon":
                                Fila[Items + 5] = (row["Saldoadmon"] is DBNull) ? "0" : row["Saldoadmon"].ToString();
                                break;
                            case "Seguro":
                                Fila[Items + 5] = (row["SaldoSeguro"] is DBNull) ? "0" : row["SaldoSeguro"].ToString();
                                break;
                        }
                    }
                    VlrTotal = VlrTotal + Total;
                    lstView.Rows.Add(Fila);
                }
                tfila += 1;
            }
            myRead.Dispose();
        }

        // Overload with defaults
        public void CargaCuotasPendientes(string codigoter, int Periodo, ref DataGridView lstView,
            OdbcConnection myconnect)
        {
            double VlrTotal = 0;
            CargaCuotasPendientes(codigoter, Periodo, ref lstView, myconnect, ref VlrTotal, new DateTime(1950, 1, 1));
        }

        public void CargaResumenCuotas(string codigoter, int lincred, double Numcredito, string Periodo,
            ref DataGridView lstView, OdbcConnection myconnect, ref double VlrTotal)
        {
            int Items = 0; Array Prioridades; double Total = 0; string Clades = "0";
            string[] Fila = new string[14];

            double SaldoCapital = 0, Saldo = 0;
            double SaldoExtra = 0, SaldoInteres = 0, SaldoMora = 0;
            double SaldoSeguro = 0, SaldoAdmon = 0, SaldoOtros = 0;
            string SaldoAportes = "0"; double SaldoAhorros = 0, SaldoServicios = 0;
            double TotalPend = 0;

            BuscaSaldosCuotasPendientes(codigoter, lincred, Numcredito, lincred, Numcredito,
                Convert.ToInt32(Periodo), myconnect,
                ref SaldoCapital, ref SaldoExtra, ref SaldoInteres, ref SaldoMora, ref SaldoSeguro,
                ref SaldoAdmon, ref SaldoOtros, ref SaldoAportes, ref SaldoAhorros, ref SaldoServicios,
                ref Saldo, ref TotalPend, ref Clades);

            Prioridades = BuscaPrioridad(myconnect);
            lstView.Rows.Clear();
            CargaPrioridades(lstView, ref Prioridades, myconnect, false);

            Fila[0] = lincred.ToString();
            Fila[1] = Numcredito.ToString();
            switch (Clades)
            {
                case "1": Fila[2] = "N"; break;
                case "2": Fila[2] = "C"; break;
                default: Fila[2] = Clades; break;
            }

            Fila[3] = Saldo.ToString("###,###,###.00");
            Fila[4] = (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros).ToString("###,###,###.00");

            for (Items = Prioridades.GetLowerBound(0); Items <= Prioridades.GetUpperBound(0); Items++)
            {
                switch (Prioridades.GetValue(Items).ToString())
                {
                    case "Capital": Fila[Items + 5] = (SaldoCapital + SaldoExtra).ToString(); break;
                    case "Interes": Fila[Items + 5] = SaldoInteres.ToString(); break;
                    case "Mora": Fila[Items + 5] = SaldoMora.ToString(); break;
                    case "Admon": Fila[Items + 5] = SaldoAdmon.ToString(); break;
                    case "Seguro": Fila[Items + 5] = SaldoSeguro.ToString(); break;
                    case "Aportes": Fila[Items + 5] = SaldoAportes.ToString(); break;
                    case "Ahorros": Fila[Items + 5] = SaldoAhorros.ToString(); break;
                    case "Servicio": Fila[Items + 5] = SaldoServicios.ToString(); break;
                }
            }
            VlrTotal = Convert.ToDouble(Fila[4]);
            lstView.Rows.Add(Fila);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandTimeout = 0;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);

            try
            {
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();

                if (Myread.RecordsAffected > 0)
                    result = true;

                if (Myread.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myread["campo1"] is DBNull)
                            Campo1 = "0";
                        else
                            Campo1 = Myread["campo1"].ToString().Trim();
                    }
                    if (Campo2 != "")
                    {
                        if (Myread["campo2"] is DBNull)
                            Campo2 = "0";
                        else
                            Campo2 = Myread["campo2"].ToString().Trim();
                    }
                    if (Campo3 != "")
                    {
                        if (Myread["campo3"] is DBNull)
                            Campo3 = "0";
                        else
                            Campo3 = Myread["campo3"].ToString().Trim();
                    }
                    if (Campo4 != "")
                    {
                        if (Myread["campo4"] is DBNull)
                            Campo4 = "0";
                        else
                            Campo4 = Myread["campo4"].ToString().Trim();
                    }
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(Information.Err().Description + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return result;
        }

        // Overload without campo params
        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento)
        {
            string c1 = "", c2 = "", c3 = "", c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref c1, ref c2, ref c3, ref c4);
        }

        public bool BuscaCuotaAnticipada(string codigoter, string lincred, double ConseCredito,
            int PeriodoCausa, OdbcConnection Myconnect, int PeriodoConatble)
        {
            string stmysql; bool ok;
            string whereCausa, wherePErio, whereLincred;

            if (PeriodoConatble == 0)
                wherePErio = " ";
            else
                wherePErio = " and periodo_contable = " + PeriodoConatble;

            if (PeriodoCausa == 0)
                whereCausa = " ";
            else
                whereCausa = " and periodo_causa = " + PeriodoCausa;

            if (lincred == "999999")
                whereLincred = " ";
            else
                whereLincred = " and lincred ='" + lincred + "' and "
                   + "numero ='" + ConseCredito + "' ";

            stmysql = " select VLR_CUOTA from cop_cuoant where estado = 'O' and codigoter ='" + codigoter + "' " + whereLincred + wherePErio + whereCausa;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaCuotaAnticipada");
            return ok;
        }

        // Overload with default PeriodoConatble
        public bool BuscaCuotaAnticipada(string codigoter, string lincred, double ConseCredito,
            int PeriodoCausa, OdbcConnection Myconnect)
        {
            return BuscaCuotaAnticipada(codigoter, lincred, ConseCredito, PeriodoCausa, Myconnect, 0);
        }

        public void EliminaCuotaAnticipadaSecuencia(string secuencia, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            Stbuilder.Append("delete from cop_cuoant where secuencia = '" + secuencia + "'");
            this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "EliminaCuotaAnticipadaSecuencia");
        }

        public void GrabaCuotasAnticipadas(string codigoter, string lincred, double ConseCredito,
            int PeriodoCausa, int periodoContable, double Capital, double Interes, double Seguro,
            double Admon, double Cuota, DateTime fechaMovimiento, string Secuencia, OdbcConnection Myconnect)
        {
            bool ok;
            ok = this.BuscaCuotaAnticipada(codigoter, lincred, ConseCredito, PeriodoCausa, Myconnect, periodoContable);
            switch (ok)
            {
                case true:
                    // ActualizaCuotaAnticipada()
                    break;
                case false:
                    IngresaCuotaAnticipada(codigoter, lincred, ConseCredito, PeriodoCausa, periodoContable, Capital, Interes, Seguro, Admon, Cuota, fechaMovimiento, Secuencia, Myconnect);
                    break;
            }
        }

        private void IngresaCuotaAnticipada(string codigoter, string lincred, double ConseCredito,
            int PeriodoCausa, int periodoContable, double Capital, double Interes, double Seguro,
            double Admon, double Cuota, DateTime fechaMovimiento, string Secuencia, OdbcConnection Myconnect)
        {
            string stmysql;
            stmysql = "insert into cop_cuoant (codigoter,lincred,numero,periodo_causa,periodo_contable,vlrant_capi,"
                    + "vlrant_inte,vlrant_segu,vlrant_admi,vlr_cuota,vlrant_otro,vlrant_extr,vlr_extra,fecha_anticipo,Secuencia) values ('"
                    + codigoter + "','" + lincred + "','" + ConseCredito + "','" + PeriodoCausa + "','"
                    + periodoContable + "','" + Capital + "','" + Interes + "','" + Seguro + "','"
                    + Admon + "','" + Cuota + "','0','0','0','" + fechaMovimiento.ToString(varini.PstForFec) + "','" + Secuencia + "')";
            this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "IngresaCuotaAnticipada");
        }

        public bool AplicaCptoAnticipo(DateTime FechaMovto, string PeriodoCausa, string Usuario,
            Form Myforma, string Empresa, OdbcConnection Myconnet,
            ref string CpteAnticipos, ref double ConseCpte)
        {
            int primer = 1; string CuentaCpte = "999999999999"; double Saldo = 0;
            double Credito = 0, Debito = 0; string CptoAnticipos = "9999";
            int sw1 = 0; int Tipolinea = 0; string CptoMovCap = "99"; double Dif = 0;
            ERP.Core.Compartido.Controles.Barraprogress Progress = new ERP.Core.Compartido.Controles.Barraprogress("Aplicando Cuotas Anticipadas", Myforma);
            int canreg = 0, fila = 0; double VlrCuotaAnt = 0; DateTime fecha; DataSet dscompania = new DataSet();
            DataSet myRead = new DataSet(); string ParamValidaCiclo = "Y"; string StestadoPeriodo = "A";
            string estado = "A";

            stmysql = "select a.codigoter, a.lincred,a.numero,a.VLR_CUOTA  from cop_cuoant a "
                    + "inner join sys_maenit b on a.codigoter=b.codigoter "
                    + "where a.periodo_causa  = '" + PeriodoCausa + "' and a.estado = 'O' "
                    + (Empresa == "Todos" ? "" : "and b.empresa='" + Strings.Right("0000" + Empresa, 4) + "'") + " order by b.empresa,a.codigoter";

            string _p3 = "0", _p4 = "0", _p5 = "0", _p6 = "0", _p7 = "0", _p8 = "0", _p9 = "0", _p10 = "0";
            string _p11 = "0", _p12 = "0", _p13 = "0", _p14 = "0", _p15 = "0", _p16 = "0", _p17 = "0", _p18 = "0";
            string _p19 = "0", _p20 = "0", _p21 = "0", _p22 = "0", _p23 = "0", _p24 = "0", _p25 = "0", _p26 = "0";
            string _p27 = "0", _p28 = "0", _p29 = "0", _p30 = "0", _p31 = "0", _p32 = "0", _p33 = "0";
            //this.msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnet, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27, ref _p28, ref _p29, ref CptoAnticipos, ref _p31, ref _p32, ref _p33, ref CpteAnticipos);

            //this.msgcofsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, Myconnet);

            CptoAnticipos = dscompania.Tables["tblcompania"].Rows[0]["CptoAnticipo"].ToString();
            CpteAnticipos = dscompania.Tables["tblcompania"].Rows[0]["CpteAnticipo"].ToString();
            ParamValidaCiclo = dscompania.Tables["tblcompania"].Rows[0]["paramcausacion"].ToString();

            ok = this.BuscaLinea(Convert.ToInt32(CptoAnticipos), Myconnet, ref Tipolinea);
            switch (Tipolinea)
            {
                case 3:
                    CptoMovCap = dscompania.Tables["tblcompania"].Rows[0]["cpto_servi"].ToString();
                    break;
                case 4:
                    CptoMovCap = dscompania.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"].ToString();
                    break;
                default:
                    MessageBox.Show("Revise el tipo de linea del concepto Cuotas Anticipadas incorrecto", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            switch (ParamValidaCiclo)
            {
                case "N":
                    if (Convert.ToDouble(FechaMovto.ToString("yyyyMM")) < Convert.ToDouble(DateTime.Now.ToString("yyyyMM")))
                    {
                        DateTime _fecIni = new DateTime(1950, 1, 1); DateTime _fecFin = new DateTime(1950, 1, 1);
                        this.buscaPeriodo("copc", Myconnet, ref _fecIni, ref _fecFin, DateTime.Now, ref StestadoPeriodo, ref PeriodoCausa, DateTime.Now.ToString("yyyy"));
                        if (StestadoPeriodo != "C")
                            FechaMovto = DateTime.Now;
                    }
                    break;
                default:
                    DateTime _fi2 = new DateTime(1950, 1, 1); DateTime _ff2 = new DateTime(1950, 1, 1);
                    //msgcofsys.buscaPeriodo("copc", Myconnet, ref _fi2, ref _ff2, FechaMovto, ref estado);
                    if (estado == "C")
                    {
                        DateTime _fi3 = new DateTime(1950, 1, 1); DateTime _ff3 = new DateTime(1950, 1, 1);
                        this.buscaPeriodo("copc", Myconnet, ref _fi3, ref _ff3, DateTime.Now, ref StestadoPeriodo, ref PeriodoCausa, DateTime.Now.ToString("yyyy"));
                        if (StestadoPeriodo != "C")
                            FechaMovto = DateTime.Now;
                    }
                    break;
            }

            if (FechaMovto.ToString("yyyyMM") != DateTime.Now.ToString("yyyyMM"))
                fecha = FechaMovto;
            else
                fecha = DateTime.Now;

            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconnet, "AplicaCptoAnticipo", ref myRead, "TblCptoAnticipo");
            canreg = myRead.Tables["TblCptoAnticipo"].Rows.Count;

            Progress.ValorMinimoMaximo(0, canreg);
            Progress.Show();

            while (fila < canreg)
            {
                Application.DoEvents();
                DataRow row = myRead.Tables["TblCptoAnticipo"].Rows[fila];
                sw1 = 0;
                VlrCuotaAnt = 0;
                this.BuscaSaldoObligacion(row["codigoter"].ToString(), Convert.ToInt32(CptoAnticipos), 0, fecha.ToString("yyyyMM"), Myconnet, ref Saldo);

                if (Saldo >= 0)
                    sw1 = 1;

                Credito = Convert.ToDouble(row["VLR_CUOTA"]);

                if (Credito > (Saldo * -1))
                    Credito = (Saldo * -1);
                VlrCuotaAnt = Credito;

                if (sw1 == 0)
                {
                    if (primer == 1)
                    {
                        //this.BuscaComprobante(CpteAnticipos, ref ConseCpte, true, Myconnet, ref CuentaCpte);
                    }
                    primer = 0;

                    //this.GrabaMovimiento(CpteAnticipos, ConseCpte, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), fecha.ToString("yyyyMM"), "99", fecha, 0, Credito, "Aplicacion de cuotas anticipadas -- Proceso automatico " + DateTime.Now.ToString("ddMMyyhhmmss") + " !!", Usuario, Myconnet, row["codigoter"].ToString());
                    Debito = Convert.ToDouble(row["VLR_CUOTA"]) - Credito;
                    if (VlrCuotaAnt != Credito)
                    {
                        ActuaEstaAntici(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(PeriodoCausa), Myconnet, "C");
                        //this.GrabaMovimiento(CpteAnticipos, ConseCpte, row["codigoter"].ToString(), Convert.ToInt32(CptoAnticipos), 0, fecha.ToString("yyyyMM"), CptoMovCap, fecha, Debito, 0, "Aplicacion de cuotas anticipadas -- Proceso automatico " + DateTime.Now.ToString("ddMMyyhhmmss") + " !!", Usuario, Myconnet, row["codigoter"].ToString());
                    }
                }
                Progress.PerformStep();
                fila += 1;
            }

            if (ConseCpte > 0)
            {
                ok = false;
                //this.BuscaComprobante(CpteAnticipos, ref ConseCpte, false, Myconnet, ref Dif);
                if (Dif == 0)
                {
                    ok = TrasladaContabilidad(CpteAnticipos, ConseCpte, Myconnet, varini.pstUsuario);
                }
            }

            Progress.Close();
            Progress.Dispose();
            myRead.Dispose();
            return ok;
        }

        // Overload with defaults
        public bool AplicaCptoAnticipo(DateTime FechaMovto, string PeriodoCausa, string Usuario,
            Form Myforma, string Empresa, OdbcConnection Myconnet)
        {
            string CpteAnticipos = "9999"; double ConseCpte = 0;
            return AplicaCptoAnticipo(FechaMovto, PeriodoCausa, Usuario, Myforma, Empresa, Myconnet, ref CpteAnticipos, ref ConseCpte);
        }

        public bool ActuaEstaAntici(string Codigoter, int Lincred, double numero, int PeriodoCausa,
            OdbcConnection Myconect, string Estado)
        {
            stmysql = "update cop_cuoant set estado = '" + Estado + "' where  codigoter = '" + Codigoter + "'"
                    + " and lincred = " + Lincred + " and numero = " + numero + " and periodo_causa  = '" + PeriodoCausa + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "ActuaEstaAntici");
            return ok;
        }

        // Overload with default Estado
        public bool ActuaEstaAntici(string Codigoter, int Lincred, double numero, int PeriodoCausa,
            OdbcConnection Myconect)
        {
            return ActuaEstaAntici(Codigoter, Lincred, numero, PeriodoCausa, Myconect, "O");
        }

    } // end partial class Clscartera
} // end namespace msgcop
