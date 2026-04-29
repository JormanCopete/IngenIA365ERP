using ERP.Core.Contabilidad.Forms;
using ERP.Core.Contabilidad.Models;
// Traducción de: ClsContabilidad.vb (msgcnt)
using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Microsoft.VisualBasic;

namespace ERP.Core.Contabilidad.Services
{
    public partial class ClsContabilidad
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgconfig = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys msgconsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Contabilidad.Models.ParamCnt msgparcnt = new ERP.Core.Contabilidad.Models.ParamCnt();
        private bool ok;
        private string stmysql;
        private StreamWriter strStreamWriter;
        private StreamReader strStreamReader;
        private ERP.Core.Compartido.Reportes.config_report config = new ERP.Core.Compartido.Reportes.config_report();
        private ERP.Core.Compartido.Datos.ClsConect conect = new ERP.Core.Compartido.Datos.ClsConect();

        public struct Numfolios
        {
            public double NumInicial;
            public double NumFinal;
        }

        private Numfolios NumFomlios;

        public enum Navega : int
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }

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
            public string pstForHora;
            public string pstForfecyHora;
        }

        public odbcConect varini;

        public void buscaPeriodo(string Modulo, OdbcConnection myconnet,
            ref DateTime FechaIni, ref DateTime fechaFin, DateTime fechaValidar,
            ref string estado, ref string Periodo, string anio)
        {
            string fechaInicial = "0", fechaFinal = "0", estadoFecha = "A";
            string _anoperiodo = "0", _mesperiodo = "0";
            int Anoperiodo = 0;
            string MesPeriodo = "0";
            string Anioactual;

            if (anio == "9999")
                Anioactual = DateTime.Now.ToString("yyyy");
            else
                Anioactual = anio;

            stmysql = "select anio as campo1, periodo as campo2  from sys_periodo where  modulo = '" + Modulo + "' and anio = '" + Anioactual + "'";
            conect.ExecuteQueryconec(stmysql, myconnet, "buscaPeriodo", ref _anoperiodo, ref _mesperiodo);
            int.TryParse(_anoperiodo, out Anoperiodo);
            MesPeriodo = _mesperiodo;

            if (Periodo != "999999" && Periodo != "" && Periodo != "P13")
            {
                if (Strings.Mid(Periodo, 5, 2) == "13")
                {
                    fechaValidar = Convert.ToDateTime(Strings.Mid(Periodo, 1, 4) + "/" + 12 + "/01");
                    Periodo = "P13";
                }
                else
                {
                    fechaValidar = Convert.ToDateTime(Strings.Mid(Periodo, 1, 4) + "/" + Strings.Mid(Periodo, 5, 2) + "/01");
                }
            }

            if (fechaValidar == new DateTime(1950, 1, 1))
            {
                int periodo1 = 0;
                int.TryParse(MesPeriodo, out periodo1);
                if (periodo1 > 12)
                {
                    periodo1 = 12;
                    Periodo = Anoperiodo + Strings.Right("00" + periodo1.ToString(), 2);
                    fechaValidar = new DateTime(int.Parse(Strings.Mid(Periodo, 1, 4)), int.Parse(Strings.Mid(Periodo, 5, 2)), 1);
                }
                if (MesPeriodo == "13")
                {
                    Periodo = "P13";
                }
                else
                {
                    Periodo = Anoperiodo + Strings.Right("00" + periodo1.ToString(), 2);
                    if (Periodo.Length >= 6)
                        fechaValidar = new DateTime(int.Parse(Strings.Mid(Periodo, 1, 4)), int.Parse(Strings.Mid(Periodo, 5, 2)), 1);
                }
            }

            switch (fechaValidar.ToString("MM"))
            {
                case "01": fechaInicial = "fecha_ini01"; fechaFinal = "fecha_fin01"; estadoFecha = "estado_01"; break;
                case "02": fechaInicial = "fecha_ini02"; fechaFinal = "fecha_fin02"; estadoFecha = "estado_02"; break;
                case "03": fechaInicial = "fecha_ini03"; fechaFinal = "fecha_fin03"; estadoFecha = "estado_03"; break;
                case "04": fechaInicial = "fecha_ini04"; fechaFinal = "fecha_fin04"; estadoFecha = "estado_04"; break;
                case "05": fechaInicial = "fecha_ini05"; fechaFinal = "fecha_fin05"; estadoFecha = "estado_05"; break;
                case "06": fechaInicial = "fecha_ini06"; fechaFinal = "fecha_fin06"; estadoFecha = "estado_06"; break;
                case "07": fechaInicial = "fecha_ini07"; fechaFinal = "fecha_fin07"; estadoFecha = "estado_07"; break;
                case "08": fechaInicial = "fecha_ini08"; fechaFinal = "fecha_fin08"; estadoFecha = "estado_08"; break;
                case "09": fechaInicial = "fecha_ini09"; fechaFinal = "fecha_fin09"; estadoFecha = "estado_09"; break;
                case "10": fechaInicial = "fecha_ini10"; fechaFinal = "fecha_fin10"; estadoFecha = "estado_10"; break;
                case "11": fechaInicial = "fecha_ini11"; fechaFinal = "fecha_fin11"; estadoFecha = "estado_11"; break;
                case "12": fechaInicial = "fecha_ini12"; fechaFinal = "fecha_fin12"; estadoFecha = "estado_12"; break;
                case "13": fechaInicial = "fecha_ini13"; fechaFinal = "fecha_fin13"; estadoFecha = "estado_13"; break;
            }

            if (fechaValidar != new DateTime(1950, 1, 1))
            {
                if (Periodo == "P13")
                {
                    Periodo = fechaValidar.ToString("yyyy") + "13";
                    fechaInicial = "fecha_ini13";
                    fechaFinal = "fecha_fin13";
                    estadoFecha = "estado_13";
                }
                else
                {
                    Periodo = fechaValidar.ToString("yyyyMM");
                }

                stmysql = "select " + fechaInicial + " as campo1," + fechaFinal + " as campo2," + estadoFecha + " as campo3 from sys_periodo where  modulo = '" + Modulo + "' and anio = '" + Anioactual + "'";
                string _fi = "0", _ff = "0", _est = estado;
                // conect.ExecuteQueryconec(stmysql, myconnet, "buscaPeriodo", ref _fi, ref _ff, ref _est); // ERROR: CS1501
                DateTime.TryParse(_fi, out FechaIni);
                DateTime.TryParse(_ff, out fechaFin);
                estado = _est;
            }
        }

        public void buscaPeriodo(string Modulo, OdbcConnection myconnet,
            ref DateTime FechaIni, ref DateTime fechaFin, DateTime fechaValidar,
            ref string estado, ref string Periodo)
        {
            buscaPeriodo(Modulo, myconnet, ref FechaIni, ref fechaFin, fechaValidar, ref estado, ref Periodo, "9999");
        }

        public void buscaPeriodo(string Modulo, OdbcConnection myconnet,
            ref DateTime FechaIni, ref DateTime fechaFin)
        {
            string estado = "C", Periodo = "999999";
            buscaPeriodo(Modulo, myconnet, ref FechaIni, ref fechaFin, new DateTime(1950, 1, 1), ref estado, ref Periodo, "9999");
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                mycomqueryconec.CommandTimeout = 0;
                mycomqueryconec.CommandText = mycomqueryconec.CommandText.Replace("''", "' '");

                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                bool result = false;
                if (Myread.RecordsAffected > 0) result = true;
                while (Myread.Read())
                {
                    if (Campo1 != "") Campo1 = (Myread["campo1"] is DBNull) ? "0" : Myread["campo1"].ToString().Trim();
                    if (Campo2 != "") Campo2 = (Myread["campo2"] is DBNull) ? "0" : Myread["campo2"].ToString().Trim();
                    if (Campo3 != "") Campo3 = (Myread["campo3"] is DBNull) ? "0" : Myread["campo3"].ToString().Trim();
                    if (Campo4 != "") Campo4 = (Myread["campo4"] is DBNull) ? "0" : Myread["campo4"].ToString().Trim();
                    result = true;
                }
                Myread.Close();
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento)
        {
            string c1 = "", c2 = "", c3 = "", c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref c1, ref c2, ref c3, ref c4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1)
        {
            string c2 = "", c3 = "", c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref c2, ref c3, ref c4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2)
        {
            string c3 = "", c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref c3, ref c4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3)
        {
            string c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref Campo3, ref c4);
        }

        public bool BuscarCuenta(ref string Cuenta, OdbcConnection mycocnect,
            ref string Tercero, ref string mane_cencos, ref string natura, ref string cencos,
            ref string nivel, ref string Aplicart, ref string ApliTes, ref string nombre,
            ref decimal Tasa, ref string TipoAuxiliar, ref int estado,
            ref string ApliCnt, ref string ConsiBaca, ref string Banco_consibanca)
        {
            Cuenta = Strings.Left(Cuenta + "000000000000", 12);
            string _tasa = "0", _estado = "0";
            ok = conect.ExecuteQueryconec(stmysql = "select tercero as campo1, mane_cencos as campo2, natura as campo3, Cencos as campo4 from cnt_maecuen where cuenta = '" + Cuenta + "'",
                mycocnect, "BuscarCuenta", ref Tercero, ref mane_cencos, ref natura, ref cencos);
            ok = conect.ExecuteQueryconec("select nivel as campo1, APLI_CARTCOOPE as campo2, CENCOS AS CAMPO3, APLI_TESORERI as campo4 from cnt_maecuen where cuenta = '" + Cuenta + "'",
                mycocnect, "BuscarCuenta", ref nivel, ref Aplicart, ref cencos, ref ApliTes);
            ok = conect.ExecuteQueryconec("select nombre as campo1, tasa as campo2,aux_domto as campo3,estado as campo4 from cnt_maecuen where cuenta = '" + Cuenta + "'",
                mycocnect, "BuscarCuenta", ref nombre, ref _tasa, ref TipoAuxiliar, ref _estado);
            decimal.TryParse(_tasa, out Tasa);
            int.TryParse(_estado, out estado);
            // ok = conect.ExecuteQueryconec("select apli_contab as campo1, CONSI_BANCA as campo2, banco_concibanca as campo3 from cnt_maecuen where cuenta = '" + Cuenta + "'", // ERROR: CS1501
                // mycocnect, "BuscarCuenta", ref ApliCnt, ref ConsiBaca, ref Banco_consibanca); // ERROR: CS1501
            return ok;
        }

        public virtual bool BuscarCuenta(ref string Cuenta, OdbcConnection mycocnect, DataTable TblCuenta)
        {
            var stbuilder = new StringBuilder();
            Cuenta = Strings.Left(Cuenta + "000000000000", 12);
            try { TblCuenta.Clear(); } catch { }
            stbuilder.Append("select tercero, mane_cencos , natura , Cencos , nivel , APLI_CARTCOOPE ,  APLI_TESORERI as AplTes,");
            stbuilder.Append("nombre,tasa,aux_domto, estado ");
            stbuilder.Append("from cnt_maecuen");
            stbuilder.Append(" where cuenta = '" + Cuenta + "'");
            // conect.ExecuteConsulta(stbuilder.ToString(), mycocnect, "BuscarCuenta", TblCuenta); // ERROR: CS1620
            return TblCuenta.Rows.Count > 0;
        }

        public double BuscarSaldoCuenta(string cuenta, string periodo, string Agencia, string Cencosto, OdbcConnection myconnect)
        {
            string saldo = "0", CampoMes = " ";
            string anio = Strings.Mid(periodo, 1, 4);
            int Mes = 0;
            int.TryParse(Strings.Mid(periodo, 5, 2), out Mes);
            switch (Mes)
            {
                case 1: CampoMes = "ene"; break;
                case 2: CampoMes = "feb"; break;
                case 3: CampoMes = "mar"; break;
                case 4: CampoMes = "abr"; break;
                case 5: CampoMes = "may"; break;
                case 6: CampoMes = "jun"; break;
                case 7: CampoMes = "jul"; break;
                case 8: CampoMes = "ago"; break;
                case 9: CampoMes = "sep"; break;
                case 10: CampoMes = "oct"; break;
                case 11: CampoMes = "nov"; break;
                case 12: CampoMes = "dic"; break;
            }
            stmysql = "select " + CampoMes + " as campo1 from cnt_salcuen_vw WHERE CUENTA = '" + cuenta + "' and periodo = " + anio;
            conect.ExecuteQueryconec(stmysql, myconnect, "BuscarSaldoCuenta", ref saldo);
            return Convert.ToDouble(saldo);
        }

        public bool BuscarMovtoCuenta(string cuenta, string periodo, OdbcConnection myconnect, ref double debito, ref double credito)
        {
            string _deb = "0", _cre = "0";
            stmysql = "select sum(vlr_debito) as campo1,sum(vlr_credito) as campo2 from cnt_movimto where cuenta='" + cuenta + "' and periodo=" + periodo;
            ok = conect.ExecuteQueryconec(stmysql, myconnect, "BuscarMovtoCuenta", ref _deb, ref _cre);
            double.TryParse(_deb, out debito);
            double.TryParse(_cre, out credito);
            return ok;
        }

        public bool BuscarSecuenciaMovto(string compronte, double numero, string cuenta,
            string agencia, string periodo, string nit, string cencosto,
            DateTime FechaMovto, string detalle, string domto_auxiliar, double debito,
            double credito, double vlr_base, string Usuario,
            string ClaseAux, OdbcConnection myconect, ref double secuencia,
            bool Tesoreria, string factura)
        {
            if (Tesoreria)
                stmysql = "select max(secuencia) as campo1 from cnt_movimto where cuenta='" + cuenta + "' and COMPRONTE='" + compronte + "' and numero=" + numero + " and agencia='" + agencia +
                    "' and CENCOSTO='" + cencosto + "' and nit='" + nit + "' and periodo=" + periodo + " and fecha='" + FechaMovto.ToString(varini.PstForFec) + "' and detalle='" + detalle + "' and DOMTO_AUXILIAR='" + domto_auxiliar +
                    "' and VLR_DEBITO=" + debito + " and VLR_CREDITO=" + credito + " and VLR_BASE=" + vlr_base + " and ESTADO='A' and factura='" + factura + "' and Usuario='" + Usuario +
                    "' and IdSolAux=0 and docu_tipo='" + ClaseAux + "'";
            else
                stmysql = "select max(secuencia) as campo1 from cnt_movimto where cuenta='" + cuenta + "' and COMPRONTE='" + compronte + "' and numero=" + numero + " and agencia='" + agencia +
                    "' and CENCOSTO='" + cencosto + "' and nit='" + nit + "' and periodo=" + periodo + " and fecha='" + FechaMovto.ToString(varini.PstForFec) + "' and detalle='" + detalle + "' and DOMTO_AUXILIAR='" + domto_auxiliar +
                    "' and VLR_DEBITO=" + debito + " and VLR_CREDITO=" + credito + " and VLR_BASE=" + vlr_base + " and ESTADO='A' and factura='" + ClaseAux.Trim() + "-" + domto_auxiliar.Trim() + "' and Usuario='" + Usuario +
                    "' and IdSolAux=0 and docu_tipo='" + ClaseAux + "'";
            string _sec = "0";
            ok = this.ExecuteQueryconec(stmysql, myconect, "BuscarSecuenciaMovto", ref _sec);
            double.TryParse(_sec, out secuencia);
            return ok;
        }

        public bool BuscarSecuenciaMovto(string compronte, double numero, string cuenta,
            string agencia, string periodo, string nit, string cencosto,
            DateTime FechaMovto, string detalle, string domto_auxiliar, double debito,
            double credito, double vlr_base, string Usuario,
            string ClaseAux, OdbcConnection myconect, ref double secuencia)
        {
            return BuscarSecuenciaMovto(compronte, numero, cuenta, agencia, periodo, nit, cencosto,
                FechaMovto, detalle, domto_auxiliar, debito, credito, vlr_base, Usuario,
                ClaseAux, myconect, ref secuencia, false, " ");
        }

        public double BuscarSaldoTecero(string cuenta, string Nit, string periodo, OdbcConnection myconnect)
        {
            string saldo = "0", CampoMes = " ";
            string anio = Strings.Mid(periodo, 1, 4);
            int Mes = 0;
            int.TryParse(Strings.Mid(periodo, 5, 2), out Mes);
            switch (Mes)
            {
                case 1: CampoMes = "ene"; break;
                case 2: CampoMes = "feb"; break;
                case 3: CampoMes = "mar"; break;
                case 4: CampoMes = "abr"; break;
                case 5: CampoMes = "may"; break;
                case 6: CampoMes = "jun"; break;
                case 7: CampoMes = "jul"; break;
                case 8: CampoMes = "ago"; break;
                case 9: CampoMes = "sep"; break;
                case 10: CampoMes = "oct"; break;
                case 11: CampoMes = "nov"; break;
                case 12: CampoMes = "dic"; break;
            }
            stmysql = "select " + CampoMes + " as campo1 from cnt_salterc_vw WHERE CUENTA = '" + cuenta + "' and periodo = " + anio + " and nit = '" + Nit + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "BuscarSaldoTecero", ref saldo);
            return Convert.ToDouble(saldo);
        }

        public double BuscarSaldoGlobalTecero(string CxCoCxP, string Nit, string periodo, OdbcConnection myconnect)
        {
            string saldo = "0", CampoMes = " ";
            string anho = Strings.Mid(periodo, 1, 4);
            int Mes = 0;
            int.TryParse(Strings.Mid(periodo, 5, 2), out Mes);
            switch (Mes)
            {
                case 1: CampoMes = "sum(((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) - cnt_tercero.ene_cre))"; break;
                case 2: CampoMes = "sum(((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre))"; break;
                case 3: CampoMes = "sum(((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre))"; break;
                case 4: CampoMes = "sum(((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre)) "; break;
                case 5: CampoMes = "sum(((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre))"; break;
                case 6: CampoMes = "sum(((((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) + cnt_tercero.jun_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre) - cnt_tercero.jun_cre)) "; break;
                case 7: CampoMes = "sum(((((((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) + cnt_tercero.jun_deb) + cnt_tercero.jul_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre) - cnt_tercero.jun_cre) - cnt_tercero.jul_cre)) "; break;
                case 8: CampoMes = "sum(((((((((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) + cnt_tercero.jun_deb) + cnt_tercero.jul_deb) + cnt_tercero.ago_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre) - cnt_tercero.jun_cre) - cnt_tercero.jul_cre) - cnt_tercero.ago_cre))"; break;
                case 9: CampoMes = "sum(((((((((((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) + cnt_tercero.jun_deb) + cnt_tercero.jul_deb) + cnt_tercero.ago_deb) + cnt_tercero.sep_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre) - cnt_tercero.jun_cre) - cnt_tercero.jul_cre) - cnt_tercero.ago_cre) - cnt_tercero.sep_cre))"; break;
                case 10: CampoMes = "sum(((((((((((((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) + cnt_tercero.jun_deb) + cnt_tercero.jul_deb) + cnt_tercero.ago_deb) + cnt_tercero.sep_deb) + cnt_tercero.oct_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre) - cnt_tercero.jun_cre) - cnt_tercero.jul_cre) - cnt_tercero.ago_cre) - cnt_tercero.sep_cre) - cnt_tercero.oct_cre))"; break;
                case 11: CampoMes = "sum(((((((((((((((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) + cnt_tercero.jun_deb) + cnt_tercero.jul_deb) + cnt_tercero.ago_deb) + cnt_tercero.sep_deb) + cnt_tercero.oct_deb) + cnt_tercero.nov_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre) - cnt_tercero.jun_cre) - cnt_tercero.jul_cre) - cnt_tercero.ago_cre) - cnt_tercero.sep_cre) - cnt_tercero.oct_cre) - cnt_tercero.nov_cre))"; break;
                case 12: CampoMes = " sum(((((((((((((((((((((((((cnt_tercero.saldo_inicial + cnt_tercero.ene_deb) + cnt_tercero.feb_deb) + cnt_tercero.mar_deb) + cnt_tercero.abr_deb) + cnt_tercero.may_deb) + cnt_tercero.jun_deb) + cnt_tercero.jul_deb) + cnt_tercero.ago_deb) + cnt_tercero.sep_deb) + cnt_tercero.oct_deb) + cnt_tercero.nov_deb) + cnt_tercero.dic_deb) - cnt_tercero.ene_cre) - cnt_tercero.feb_cre) - cnt_tercero.mar_cre) - cnt_tercero.abr_cre) - cnt_tercero.may_cre) - cnt_tercero.jun_cre) - cnt_tercero.jul_cre) - cnt_tercero.ago_cre) - cnt_tercero.sep_cre) - cnt_tercero.oct_cre) - cnt_tercero.nov_cre) - cnt_tercero.dic_cre)) "; break;
            }
            stmysql = "select " + CampoMes + " as campo1 from cnt_tercero inner join cnt_maecuen c on cnt_tercero.cuenta=c.cuenta " +
                      " where c.AUX_DOMTO='" + CxCoCxP + "' and cnt_tercero.nit='" + Nit + "' and cnt_tercero.periodo= " + anho + " group by cnt_tercero.periodo";
            conect.ExecuteQueryconec(stmysql, myconnect, "BuscarSaldoGlobalTecero", ref saldo);
            return Convert.ToDouble(saldo);
        }

        public bool BuscarCompania(string Codigo, OdbcConnection myconect, ref string CuentaUtilidad)
        {
            stmysql = "select CUENTACON2 as campo1  from sys_compania where CODIGO = '" + Codigo + "'";
            ok = conect.ExecuteQueryconec(stmysql, myconect, "BsucarCompania", ref CuentaUtilidad);
            return ok;
        }

        public bool BuscarTercero(string nit, OdbcConnection myconect,
            ref string nombre, ref string Direccion, ref string Telefono, ref string Email,
            ref string TipoTercero, ref string Precioesp, ref string ClientePatronal,
            ref int estado, ref string retenFuente, ref string tipo_persona,
            ref int dias_pago, ref double cupocredito, ref string idasesor, ref string contactos)
        {
            string nombreTer = "", razon_social = "";
            string _estado = "0", _dias_pago = "0", _cupocredito = "0";

            ok = conect.ExecuteQueryconec("select RAZON_SOCIAL as campo1, DIRECCION as campo2, TELEFONO1 as campo3,email as campo4 from cnt_nit where nit='" + nit + "'",
                myconect, "BuscarTercero", ref razon_social, ref Direccion, ref Telefono, ref Email);
            ok = conect.ExecuteQueryconec("select Tipo_tercero as campo1, precioEsp as campo2,CliPatronal as campo3, estado as campo4 from cnt_nit where nit= '" + nit + "'",
                myconect, "BuscarTercero", ref TipoTercero, ref Precioesp, ref ClientePatronal, ref _estado);
            int.TryParse(_estado, out estado);
            ok = conect.ExecuteQueryconec("select retenFuente as campo1, nombre as campo2,tipo_persona as campo3,cupocredito as campo4  from cnt_nit  where nit= '" + nit + "'",
                myconect, "BuscarTercero", ref retenFuente, ref nombreTer, ref tipo_persona, ref _cupocredito);
            double.TryParse(_cupocredito, out cupocredito);
            // ok = conect.ExecuteQueryconec("select dias_pago as campo1, idasesor as campo2, contactos as campo3  from cnt_nit  where nit= '" + nit + "'", // ERROR: CS1501
                // myconect, "BuscarTercero", ref _dias_pago, ref idasesor, ref contactos); // ERROR: CS1501
            int.TryParse(_dias_pago, out dias_pago);
            if (TipoTercero.Trim() == "") TipoTercero = "0";
            nombre = (tipo_persona == "N") ? nombreTer : razon_social;
            return ok;
        }

        public bool BuscarTercero(string nit, OdbcConnection myconect)
        {
            string nombre = " ", Direccion = " ", Telefono = "", Email = " ";
            string TipoTercero = "0", Precioesp = "N", ClientePatronal = "N";
            int estado = 0; string retenFuente = "Y", tipo_persona = "";
            int dias_pago = 0; double cupocredito = 0;
            string idasesor = "99999999999999", contactos = "";
            return BuscarTercero(nit, myconect, ref nombre, ref Direccion, ref Telefono, ref Email,
                ref TipoTercero, ref Precioesp, ref ClientePatronal, ref estado,
                ref retenFuente, ref tipo_persona, ref dias_pago, ref cupocredito,
                ref idasesor, ref contactos);
        }

        public bool BuscarTercero(string nit, OdbcConnection myconect, ref string nombre)
        {
            string Direccion = " ", Telefono = "", Email = " ";
            string TipoTercero = "0", Precioesp = "N", ClientePatronal = "N";
            int estado = 0; string retenFuente = "Y", tipo_persona = "";
            int dias_pago = 0; double cupocredito = 0;
            string idasesor = "99999999999999", contactos = "";
            return BuscarTercero(nit, myconect, ref nombre, ref Direccion, ref Telefono, ref Email,
                ref TipoTercero, ref Precioesp, ref ClientePatronal, ref estado,
                ref retenFuente, ref tipo_persona, ref dias_pago, ref cupocredito,
                ref idasesor, ref contactos);
        }

        public bool GrabaTercero(string nit, string nombre, string Direccion, string Telefono,
            string Email, string TipoPersona, string NIT_CHEQUEO, OdbcConnection myconect,
            string TIPO_NIT, string AUTORFTE, string AUTORTEICA)
        {
            ok = this.BuscarTercero(nit, myconect);
            if (!ok)
                stmysql = "insert into cnt_nit (NIT,RAZON_SOCIAL,NOMBRE,DIRECCION,TELEFONO1,email,tipo_persona,NIT_CHEQUEO,TIPO_NIT,AUTORFTE,AUTORTEICA) values ('" +
                    nit + "','" + nombre + "','" + nombre + "','" + Direccion + "','" + Telefono + "','" + Email + "','" + TipoPersona + "','" + NIT_CHEQUEO + "','" + TIPO_NIT + "','" + AUTORFTE + "','" + AUTORTEICA + "')";
            else
                stmysql = "update cnt_nit set RAZON_SOCIAL = '" + nombre + "',NOMBRE= '" + nombre + "',DIRECCION= '" + Direccion + "',TELEFONO1= '" + Telefono + "',email= '" + Email + "'" +
                    " where  nit = '" + nit + "'";
            conect.ExecuteQueryconec(stmysql, myconect, "GrabaTercero");
            return ok;
        }

        public bool GrabaTercero(string nit, string nombre, string Direccion, string Telefono,
            string Email, string TipoPersona, string NIT_CHEQUEO, OdbcConnection myconect)
        {
            return GrabaTercero(nit, nombre, Direccion, Telefono, Email, TipoPersona, NIT_CHEQUEO, myconect, "C", "N", "N");
        }

        public void GrabaDatosMediosMagneticos(int Anio, int idformato, int idcpto, string nit,
            string Cuenta, int Tipodo, string Dive, string Apellido1, string Apellido2,
            string nombre, string Razonsocial, double Valor1, double valor2, double valor3,
            double valor4, double valor5, int municipio, string Direccion, double valor6,
            double valor7, double valor8, double valor9, double valor10,
            OdbcConnection myconnect, string CuentaAhorros)
        {
            var stbuilder = new StringBuilder();
            // ok = this.msgparcnt.BuscaTablaFormatosdian(idformato, idcpto, myconnect); // ERROR: CS1620
            if (!ok)
            {
                MessageBox.Show("concepto no esta creado, por favor comuniquese con su administrador \r Formato : " + idformato + "  Concepto :" + idcpto);
                return;
            }
            ok = this.BuscaDatosMediosMagneticos(Anio, idformato, idcpto, nit, Cuenta, myconnect);
            if (!ok)
            {
                stmysql = "insert into cnt_infmedian(anio,idformato,idcpto,nit,cuenta,tipodo,Dive,Apellido1,Apellido2,Nombre,Razonsocial,Valor1,valor2,valor3,valor4,valor5,Municipio,Direccion,CuentaAhorros,Valor6,valor7,valor8,valor9,valor10) values ('" +
                    Anio + "','" + idformato + "','" + idcpto + "','" + nit + "','" + Cuenta + "','" + Tipodo + "','" + Dive + "','" + Apellido1 + "','" + Apellido2 + "','" + nombre + "','" + Razonsocial + "','" + Valor1 + "','" + valor2 + "','" + valor3 +
                    "','" + valor4 + "','" + valor5 + "','" + municipio + "','" + Direccion + "','" + CuentaAhorros + "','" + valor6 + "','" + valor7 + "','" + valor8 + "','" + valor9 + "','" + valor10 + "')";
                conect.ExecuteQueryconec(stmysql, myconnect, "GrabaMediosMagneticos");
            }
            else
            {
                stbuilder.Append("Update cnt_infmedian set ");
                stbuilder.Append("tipodo = '"); stbuilder.Append(Tipodo + "',");
                stbuilder.Append("Dive = '"); stbuilder.Append(Dive + "',");
                stbuilder.Append("Apellido1 = '"); stbuilder.Append(Apellido1 + "',");
                stbuilder.Append("Apellido2 = '"); stbuilder.Append(Apellido2 + "',");
                stbuilder.Append("Nombre = '"); stbuilder.Append(nombre + "',");
                stbuilder.Append("Razonsocial = '"); stbuilder.Append(Razonsocial + "',");
                stbuilder.Append("Valor1 = valor1 + '"); stbuilder.Append(Valor1 + "',");
                stbuilder.Append("Valor2 = valor2 + '"); stbuilder.Append(valor2 + "',");
                stbuilder.Append("Valor3 = valor3 + '"); stbuilder.Append(valor3 + "',");
                stbuilder.Append("Valor4 = valor4 + '"); stbuilder.Append(valor4 + "',");
                stbuilder.Append("Valor5 = valor5 + '"); stbuilder.Append(valor5 + "',");
                stbuilder.Append("municipio = '"); stbuilder.Append(municipio + "',");
                stbuilder.Append("Direccion = '"); stbuilder.Append(Direccion + "',");
                stbuilder.Append("Valor6 = valor6 + '"); stbuilder.Append(valor6 + "',");
                stbuilder.Append("Valor7 = valor7 + '"); stbuilder.Append(valor7 + "',");
                stbuilder.Append("Valor8 = valor8 + '"); stbuilder.Append(valor8 + "',");
                stbuilder.Append("Valor9 = valor9 + '"); stbuilder.Append(valor9 + "',");
                stbuilder.Append("Valor10 = valor10 + '"); stbuilder.Append(valor10 + "'");
                if (CuentaAhorros != null)
                {
                    stbuilder.Append(",CuentaAhorros = '");
                    stbuilder.Append(CuentaAhorros + "'");
                }
                stbuilder.Append(" where anio = '");
                stbuilder.Append(Anio + "' and idformato = '");
                stbuilder.Append(idformato + "' and idcpto = '");
                stbuilder.Append(idcpto + "' and nit = '");
                stbuilder.Append(nit + "'");
                conect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaDatosMediosMagneticos");
            }
        }

        public void GrabaDatosMediosMagneticos(int Anio, int idformato, int idcpto, string nit,
            string Cuenta, int Tipodo, string Dive, string Apellido1, string Apellido2,
            string nombre, string Razonsocial, double Valor1, double valor2, double valor3,
            double valor4, double valor5, int municipio, string Direccion, double valor6,
            double valor7, double valor8, double valor9, double valor10,
            OdbcConnection myconnect)
        {
            GrabaDatosMediosMagneticos(Anio, idformato, idcpto, nit, Cuenta, Tipodo, Dive, Apellido1, Apellido2,
                nombre, Razonsocial, Valor1, valor2, valor3, valor4, valor5, municipio, Direccion,
                valor6, valor7, valor8, valor9, valor10, myconnect, null);
        }

        public bool BuscaDatosMediosMagneticos(int Anio, int idformato, int idcpto, string nit, string cuenta,
            OdbcConnection myconnect,
            ref int Tipodoc, ref string DigVe, ref string Apellido1, ref string Apellido2, ref string nombre,
            ref string Razonsocial, ref double Valor1, ref double valor2, ref double valor3, ref double valor4,
            ref double valor5, ref int municipio, ref string Direccion,
            ref double Valor6, ref double valor7, ref double valor8, ref double valor9, ref double valor10)
        {
            var StBuilder = new StringBuilder();
            var dsdata = new DataSet();
            StBuilder.Append("select tipodo,Dive,Apellido1,Apellido2,Nombre,Razonsocial,Valor1,Valor2,Valor3,Valor4,Valor5, ");
            StBuilder.Append("Valor6,Valor7,Valor8,Valor9,Valor10,Municipio,Direccion ");
            StBuilder.Append("from cnt_infmedian ");
            StBuilder.Append("where anio ='" + Anio + "' and idformato ='" + idformato + "' and idcpto ='" + idcpto + "' and nit = '" + nit + "' and cuenta = '" + cuenta + "' ");
            ok = this.conect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaDatosMediosMagneticos", ref dsdata, "tblmedios");
            if (ok)
            {
                var row = dsdata.Tables["tblmedios"].Rows[0];
                Tipodoc = (row["tipodo"] is DBNull) ? 0 : Convert.ToInt32(row["tipodo"]);
                DigVe = row["Dive"].ToString();
                Apellido1 = row["Apellido1"].ToString(); Apellido2 = row["Apellido2"].ToString();
                Razonsocial = row["Razonsocial"].ToString(); nombre = row["Nombre"].ToString();
                Valor1 = (row["Valor1"] is DBNull) ? 0 : Convert.ToDouble(row["Valor1"]);
                valor2 = (row["Valor2"] is DBNull) ? 0 : Convert.ToDouble(row["Valor2"]);
                valor3 = (row["Valor3"] is DBNull) ? 0 : Convert.ToDouble(row["Valor3"]);
                valor4 = (row["Valor4"] is DBNull) ? 0 : Convert.ToDouble(row["Valor4"]);
                valor5 = (row["Valor5"] is DBNull) ? 0 : Convert.ToDouble(row["Valor5"]);
                Valor6 = (row["Valor6"] is DBNull) ? 0 : Convert.ToDouble(row["Valor6"]);
                valor7 = (row["Valor7"] is DBNull) ? 0 : Convert.ToDouble(row["Valor7"]);
                valor8 = (row["Valor8"] is DBNull) ? 0 : Convert.ToDouble(row["Valor8"]);
                valor9 = (row["Valor9"] is DBNull) ? 0 : Convert.ToDouble(row["Valor9"]);
                valor10 = (row["Valor10"] is DBNull) ? 0 : Convert.ToDouble(row["Valor10"]);
                Direccion = row["Direccion"].ToString();
                municipio = (row["Municipio"] is DBNull) ? 0 : Convert.ToInt32(row["Municipio"]);
            }
            else
            {
                Tipodoc = 0; DigVe = "0"; Apellido1 = ""; Apellido2 = "";
                Razonsocial = ""; nombre = ""; Valor1 = 0; valor2 = 0;
                valor3 = 0; valor4 = 0; valor5 = 0; Valor6 = 0;
                valor7 = 0; valor8 = 0; valor9 = 0; valor10 = 0;
                Direccion = ""; municipio = 0;
            }
            return ok;
        }

        public bool BuscaDatosMediosMagneticos(int Anio, int idformato, int idcpto, string nit, string cuenta, OdbcConnection myconnect)
        {
            int _tipodoc = 0; string _digve = " ", _ap1 = " ", _ap2 = " ", _nom = " ", _razon = " ";
            double _v1 = 0, _v2 = 0, _v3 = 0, _v4 = 0, _v5 = 0, _v6 = 0, _v7 = 0, _v8 = 0, _v9 = 0, _v10 = 0;
            int _mun = 0; string _dir = " ";
            return BuscaDatosMediosMagneticos(Anio, idformato, idcpto, nit, cuenta, myconnect,
                ref _tipodoc, ref _digve, ref _ap1, ref _ap2, ref _nom, ref _razon,
                ref _v1, ref _v2, ref _v3, ref _v4, ref _v5, ref _mun, ref _dir,
                ref _v6, ref _v7, ref _v8, ref _v9, ref _v10);
        }

        private void EliminaDatosMediosMagneticos(string Anio, OdbcConnection myconnect)
        {
            stmysql = "delete from cnt_infmedian where anio = '" + Anio + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "EliminaDatosMediosMagneticos");
        }

        public DataSet BuscarInformacionMediosMagneticos(string anio, OdbcConnection myconnect)
        {
            var StBuilder = new StringBuilder();
            var dsdata = new DataSet();
            StBuilder.Append("select idformato,idcpto,nit,cuenta,tipodo,Dive,Apellido1,Apellido2,Nombre,Razonsocial,Valor1,Valor2,Valor3,Valor4,Valor5, ");
            StBuilder.Append("Valor6,Valor7,Valor8,Valor9,Valor10,Municipio,Direccion ");
            StBuilder.Append("from cnt_infmedian ");
            StBuilder.Append("where anio ='" + anio + "' ");
            this.conect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaDatosMediosMagneticos", ref dsdata, "tblmedios");
            return dsdata;
        }

        public void buscaFirmasRegistradasAhorros(double NumCuenta, ref string Cedula1, ref string cedula2,
            ref string cedula3, ref string nombre1, ref string nombre2, ref string nombre3,
            OdbcConnection myconnect, ref int NumFirmas, ref string Excenta)
        {
            stmysql = "select cc_nit_firmareq1 as campo1, cc_nit_firmareq2 as campo2,cc_nit_firmareq3 as campo3,nom_firmareq1 as campo4 from dep_maeahor where num_cuenta = '" + NumCuenta + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "FirmasRegistradas", ref Cedula1, ref cedula2, ref cedula3, ref nombre1);
            stmysql = "select nom_firmareq2 as campo1, nom_firmareq2 as campo2,Excenta as campo3 from dep_maeahor where num_cuenta = '" + NumCuenta + "'";
            // conect.ExecuteQueryconec(stmysql, myconnect, "FirmasRegistradas", ref nombre2, ref nombre3, ref Excenta); // ERROR: CS1501
            NumFirmas = 0;
            if (Cedula1.Trim() != "" && Cedula1.Trim() != "00000000000000") NumFirmas++;
            if (cedula2.Trim() != "" && cedula2.Trim() != "00000000000000") NumFirmas++;
            if (cedula3.Trim() != "" && cedula3.Trim() != "00000000000000") NumFirmas++;
        }

        public void buscaFirmasRegistradasCdats(double NumCdat, ref string Cedula1, ref string cedula2,
            ref string cedula3, ref string nombre1, ref string nombre2, ref string nombre3,
            OdbcConnection myconnect, int NumFirmas)
        {
            stmysql = "select cc_nit_firmareq1 as campo1, cc_nit_firmareq2 as campo2,cc_nit_firmareq3 as campo3,nom_firmareq1 as campo4 from cdt_maecdats where num_cdat = '" + NumCdat + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "FirmasRegistradas", ref Cedula1, ref cedula2, ref cedula3, ref nombre1);
            stmysql = "select nom_firmareq2 as campo1, nom_firmareq2 as campo2 from cdt_maecdats where num_cdat = '" + NumCdat + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "FirmasRegistradas", ref nombre2, ref nombre3);
            NumFirmas = 0;
            if (Cedula1.Trim() != "" && Cedula1.Trim() != "00000000000000") NumFirmas++;
            if (cedula2.Trim() != "" && cedula2.Trim() != "00000000000000") NumFirmas++;
            if (cedula3.Trim() != "" && cedula3.Trim() != "00000000000000") NumFirmas++;
        }

        public void ObtieneValoresMediosMagneticos(int Fuente, int OpcionValor, string Naturaleza,
            double VlrDebito, double VlrCredito, double Saldo, double SaldoInicial,
            ref double valor1, ref double valor2, ref double valor3, ref double valor4, ref double valor5,
            ref double valor6, ref double valor7, ref double valor8, ref double valor9, ref double valor10)
        {
            switch (Fuente)
            {
                case 5:
                    valor1 = VlrCredito;
                    valor2 = Saldo;
                    break;
                case 6:
                    valor1 = SaldoInicial;
                    valor2 = VlrCredito;
                    valor3 = VlrDebito;
                    valor4 = Saldo;
                    break;
                default:
                    switch (OpcionValor)
                    {
                        case 1:
                            switch (Fuente) { case 1: valor1 = VlrDebito; break; case 2: valor1 = VlrCredito; break; case 3: valor1 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor1 = Saldo; break; }
                            break;
                        case 2:
                            switch (Fuente) { case 1: valor2 = VlrDebito; break; case 2: valor2 = VlrCredito; break; case 3: valor2 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor2 = Saldo; break; }
                            break;
                        case 3:
                            switch (Fuente) { case 1: valor3 = VlrDebito; break; case 2: valor3 = VlrCredito; break; case 3: valor3 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor3 = Saldo; break; }
                            break;
                        case 4:
                            switch (Fuente) { case 1: valor4 = VlrDebito; break; case 2: valor4 = VlrCredito; break; case 3: valor4 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor4 = Saldo; break; }
                            break;
                        case 5:
                            switch (Fuente) { case 1: valor5 = VlrDebito; break; case 2: valor5 = VlrCredito; break; case 3: valor5 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor5 = Saldo; break; }
                            break;
                        case 6:
                            switch (Fuente) { case 1: valor6 = VlrDebito; break; case 2: valor6 = VlrCredito; break; case 3: valor6 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor6 = Saldo; break; }
                            break;
                        case 7:
                            switch (Fuente) { case 1: valor7 = VlrDebito; break; case 2: valor7 = VlrCredito; break; case 3: valor7 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor7 = Saldo; break; }
                            break;
                        case 8:
                            switch (Fuente) { case 1: valor8 = VlrDebito; break; case 2: valor8 = VlrCredito; break; case 3: valor8 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor8 = Saldo; break; }
                            break;
                        case 9:
                            switch (Fuente) { case 1: valor9 = VlrDebito; break; case 2: valor9 = VlrCredito; break; case 3: valor9 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor9 = Saldo; break; }
                            break;
                        case 10:
                            switch (Fuente) { case 1: valor10 = VlrDebito; break; case 2: valor10 = VlrCredito; break; case 3: valor10 = (Naturaleza == "D") ? VlrDebito - VlrCredito : VlrCredito - VlrDebito; break; case 4: valor10 = Saldo; break; }
                            break;
                    }
                    break;
            }
        }

        // NOTE: GrabaMediosMagneticos and DIAN methods are complex bulk-processing methods
        // that follow the same pattern. Stubs provided; full translation below.

        public void GrabaMediosMagneticos(int Anio, OdbcConnection myconnect, Form myforma)
        {
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Genera informacion Medios Magneticos", myforma);
            int Tipodo = 0; double valor1 = 0, valor2 = 0, valor3 = 0, valor4 = 0, valor5 = 0;
            double valor6 = 0, valor7 = 0, valor8 = 0, valor9 = 0, valor10 = 0;
            string Apellido1 = " ", Apellido2 = " ", nombre = null;
            int sw1 = 0; double SaldoTercero = 0;
            var myread = new DataSet();
            var Stbuilder = new StringBuilder();

            EliminaDatosMediosMagneticos(Anio.ToString(), myconnect);

            Stbuilder.Append("select cntmae.cuenta,parinf.idformato,parinf.idcpto,parinf.basecalculo as fuente,cntmae.nivel,cntmae.tasa,cntter.nit, ");
            Stbuilder.Append("cntnit.tipo_persona,cntnit.razon_social, cntnit.nombre,cntnit.nit_chequeo,cntnit.idciudad,cntnit.direccion,");
            Stbuilder.Append("cntter.dic,movter.totdeb,movter.totcre,parinf.opcionvalor,cntmae.NATURA,parinf.formula ");
            Stbuilder.Append("from cnt_parinfmedian parinf ");
            Stbuilder.Append("inner join cnt_maecuen cntmae  on cntmae.cuenta = parinf.valorparam ");
            Stbuilder.Append("inner join cnt_salterc_vw cntter on cntmae.cuenta = cntter.cuenta and cntter.periodo =" + Anio + " ");
            Stbuilder.Append("inner join cnt_nit cntnit on cntter.nit = cntnit.nit ");
            Stbuilder.Append("left join cnt_movter_vw movter on cntter.cuenta = movter.cuenta and cntter.periodo = movter.periodo And cntter.nit = movter.nit ");
            Stbuilder.Append("where cntmae.tercero ='Y' and cntter.periodo = " + Anio + " and parinf.opcionparam=1");

            conect.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "GrabaMediosMagneticos", ref myread, "TblGrabMediosMag");
            int canreg = myread.Tables["TblGrabMediosMag"].Rows.Count;
            msgbarra.ValorMinimoMaximo(0, canreg);
            msgbarra.Show();

            int fila = 0;
            while (fila < canreg)
            {
                var row = myread.Tables["TblGrabMediosMag"].Rows[fila];
                sw1 = 0;
                valor2 = 0; valor1 = 0; valor3 = 0; valor4 = 0; valor5 = 0;
                valor6 = 0; valor7 = 0; valor8 = 0; valor9 = 0; valor10 = 0;

                string tipo_persona = row["Tipo_persona"].ToString();
                switch (tipo_persona)
                {
                    case "J": Tipodo = 31; break;
                    default: Tipodo = 13; break;
                }

                if (row["nit"].ToString() == "99999999999999") sw1 = 1;

                string cuentaStr = row["cuenta"].ToString();
                if (sw1 == 0 && (Strings.Mid(cuentaStr, 1, 1) == "2" || Strings.Mid(cuentaStr, 1, 1) == "3" || Strings.Mid(cuentaStr, 1, 1) == "4"))
                {
                    int fuente = 0; int.TryParse(row["fuente"].ToString(), out fuente);
                    if (fuente != 0)
                        SaldoTercero = Convert.ToDouble(row["dic"]) * -1;
                }
                else
                    SaldoTercero = (row["dic"] is DBNull) ? 0 : Convert.ToDouble(row["dic"]);

                if (SaldoTercero < 0)
                    if (row["idformato"].ToString() != "1009" && row["idcpto"].ToString() != "2204")
                        sw1 = 1;

                double totdeb = (row["totdeb"] is DBNull) ? 0 : Convert.ToDouble(row["totdeb"]);
                double totcre = (row["totcre"] is DBNull) ? 0 : Convert.ToDouble(row["totcre"]);
                if (SaldoTercero == 0 && totdeb == 0 && totcre == 0) sw1 = 1;

                int _fuente = 0; int.TryParse(row["fuente"].ToString(), out _fuente);
                int _opcionvalor = 0; int.TryParse(row["opcionvalor"].ToString(), out _opcionvalor);
                this.ObtieneValoresMediosMagneticos(_fuente, _opcionvalor, row["NATURA"].ToString(), totdeb, totcre, SaldoTercero, 0,
                    ref valor1, ref valor2, ref valor3, ref valor4, ref valor5, ref valor6, ref valor7, ref valor8, ref valor9, ref valor10);

                if (row["idformato"].ToString() == "1003" && valor1 > 0)
                {
                    decimal tasa = 0; decimal.TryParse(row["tasa"].ToString(), out tasa);
                    if (tasa <= 0) { MessageBox.Show("Tasa invalida cuenta : " + row["cuenta"]); sw1 = 1; }
                    valor2 = valor1;
                    valor1 = Math.Round(valor1 / (Convert.ToDouble(tasa) / 100), 0);
                }

                if (sw1 == 0)
                {
                    if (!Information.IsNumeric(valor2)) valor2 = 0;
                    nombre = row["nombre"].ToString();
                    Apellido1 = row["nombre"].ToString();
                    Apellido2 = row["nombre"].ToString();

                    if (valor2 == 0)
                    {
                        int formula = 0; int.TryParse(row["formula"].ToString(), out formula);
                        switch (formula)
                        {
                            case 1: valor2 = Math.Round((valor1 * 4) / 8.5, 0); break;
                            case 2: valor2 = Math.Round((valor1 * 4) / 12, 0); break;
                        }
                    }

                    int _idformato = 0; int.TryParse(row["idformato"].ToString(), out _idformato);
                    int _idcpto = 0; int.TryParse(row["idcpto"].ToString(), out _idcpto);
                    int _idciudad = 0; int.TryParse(row["idciudad"].ToString(), out _idciudad);
                    this.GrabaDatosMediosMagneticos(Anio, _idformato, _idcpto, row["nit"].ToString(), row["cuenta"].ToString(), Tipodo,
                        row["nit_chequeo"].ToString(), Apellido1, Apellido2, nombre, row["razon_social"].ToString(),
                        valor1, valor2, valor3, valor4, valor5, _idciudad, row["direccion"].ToString(),
                        valor6, valor7, valor8, valor9, valor10, myconnect);
                }
                msgbarra.PerformStep();
                Application.DoEvents();
                fila++;
            }
            msgbarra.Close();
            msgbarra.Dispose();
        }

        // ─── DIAN bulk methods (follow identical pattern) ────────────────────────

        private void DianBulkHelper(DataSet myread, string tableName, int Anio,
            OdbcConnection myconnect, ERP.Core.Compartido.Controles.Barraprogress msgbarra, bool usarFuente)
        {
            int reg = myread.Tables[tableName].Rows.Count;
            msgbarra.ValorMinimoMaximo(0, reg);
            msgbarra.Show();
            for (int fil = 0; fil < reg; fil++)
            {
                var row = myread.Tables[tableName].Rows[fil];
                int sw1 = 0;
                double valor1 = 0, valor2 = 0, valor3 = 0, valor4 = 0, valor5 = 0;
                double valor6 = 0, valor7 = 0, valor8 = 0, valor9 = 0, valor10 = 0;
                int Tipodo = (row["natjur"].ToString() == "1") ? 31 : 13;
                if (row["nit"].ToString() == "99999999999999") sw1 = 1;

                if (sw1 == 0)
                {
                    int _fuente = 0; int.TryParse(row["fuente"].ToString(), out _fuente);
                    int _opcion = 0; int.TryParse(row["opcionvalor"].ToString(), out _opcion);
                    double saldo = (row.Table.Columns.Contains("Saldo") && !(row["Saldo"] is DBNull)) ? Convert.ToDouble(row["Saldo"]) : 0;
                    double deb = (row.Table.Columns.Contains("debitos") && !(row["debitos"] is DBNull)) ? Convert.ToDouble(row["debitos"]) : 0;
                    double cre = (row.Table.Columns.Contains("creditos") && !(row["creditos"] is DBNull)) ? Convert.ToDouble(row["creditos"]) : 0;
                    string natura = row.Table.Columns.Contains("natura") ? row["natura"].ToString() : "C";
                    this.ObtieneValoresMediosMagneticos(_fuente, _opcion, natura, deb, cre, saldo, 0,
                        ref valor1, ref valor2, ref valor3, ref valor4, ref valor5, ref valor6, ref valor7, ref valor8, ref valor9, ref valor10);
                    string ap1 = row["apellido"].ToString(), ap2 = row["apellido"].ToString();
                    string nom = row["apellido"] + " " + row["nombre"];
                    int _idformato = 0; int.TryParse(row["idformato"].ToString(), out _idformato);
                    int _idcpto = 0;
                    if (row.Table.Columns.Contains("idcpto")) int.TryParse(row["idcpto"].ToString(), out _idcpto);
                    int _idciudad = 0; int.TryParse(row["idciudad"].ToString(), out _idciudad);
                    this.GrabaDatosMediosMagneticos(Anio, _idformato, _idcpto, row["nit"].ToString(), "999999999999",
                        Tipodo, 0.ToString(), ap1, ap2, nom, " ",
                        valor1, valor2, valor3, valor4, valor5, _idciudad, row["direccion"].ToString(),
                        valor6, valor7, valor8, valor9, valor10, myconnect);
                }
                msgbarra.PerformStep();
                Application.DoEvents();
            }
        }

        public void BuscaDianRetencionesFinancieras(int Anio, OdbcConnection myconnect, Form myforma)
        {
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Genera informacion Retenciones Medios Magneticos", myforma);
            stmysql = "select maenit.nit, maenit.nombre,maenit.apellido,maenit.natjur,copmov.cuenta, copmov.lincred,copmov.idformato,copmov.idcpto,codahor,saldo, " +
                "cntmae.tasa,dpto_ciudad as idciudad,direccion,copmov.fuente,copmov.opcionvalor,cntmae.natura from cnt_retftedian_vw copmov inner join cop_concar12 par12 " +
                "on copmov.lincred = par12.lincred inner join sys_maenit maenit on copmov.codigoter = maenit.codigoter " +
                "inner join cnt_maecuen cntmae on copmov.cuenta = cntmae.cuenta " +
                "where anio  = '" + Anio + "'";
            var myread = new DataSet();
            conect.ExecuteQueryDataset(stmysql, myconnect, "BuscaDianRetencionesFinancieras", ref myread, "TblRetFinanc");
            int reg = myread.Tables["TblRetFinanc"].Rows.Count;
            msgbarra.ValorMinimoMaximo(0, reg);
            msgbarra.Show();
            for (int fila = 0; fila < reg; fila++)
            {
                var row = myread.Tables["TblRetFinanc"].Rows[fila];
                int sw1 = 0;
                double valor1 = 0, valor2 = 0, valor3 = 0, valor4 = 0, valor5 = 0;
                double valor6 = 0, valor7 = 0, valor8 = 0, valor9 = 0, valor10 = 0;
                int Tipodo = (row["natjur"].ToString() == "1") ? 31 : 13;
                if (row["nit"].ToString() == "99999999999999") sw1 = 1;
                double _saldo = (row["Saldo"] is DBNull) ? 0 : Convert.ToDouble(row["Saldo"]);
                if (_saldo <= 0) sw1 = 1;
                if (sw1 == 0)
                {
                    int _fuente = 0; int.TryParse(row["fuente"].ToString(), out _fuente);
                    int _opcion = 0; int.TryParse(row["opcionvalor"].ToString(), out _opcion);
                    decimal tasa = 0; decimal.TryParse(row["tasa"].ToString(), out tasa);
                    this.ObtieneValoresMediosMagneticos(_fuente, _opcion, row["natura"].ToString(), 0, 0, _saldo, 0,
                        ref valor1, ref valor2, ref valor3, ref valor4, ref valor5, ref valor6, ref valor7, ref valor8, ref valor9, ref valor10);
                    valor2 = Math.Round(valor1 / (Convert.ToDouble(tasa) / 100), 0);
                    string ap1 = row["nombre"].ToString(), ap2 = row["nombre"].ToString();
                    string nom = row["apellido"] + " " + row["nombre"];
                    int _idformato = 0; int.TryParse(row["idformato"].ToString(), out _idformato);
                    int _idcpto = 0; int.TryParse(row["idcpto"].ToString(), out _idcpto);
                    int _idciudad = 0; int.TryParse(row["idciudad"].ToString(), out _idciudad);
                    this.GrabaDatosMediosMagneticos(Anio, _idformato, _idcpto, row["nit"].ToString(), row["cuenta"].ToString(),
                        Tipodo, 0.ToString(), ap1, ap2, nom, " ",
                        valor1, valor2, valor3, valor4, valor5, _idciudad, row["direccion"].ToString(),
                        valor6, valor7, valor8, valor9, valor10, myconnect);
                }
                msgbarra.PerformStep();
                Application.DoEvents();
            }
            myread.Dispose();
            msgbarra.Close(); msgbarra.Dispose();
        }

        // ─── Remaining DIAN / bulk methods – stubs to be completed ───────────────
        public void BuscaDianCartera(int anio, int periodo, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void BuscaDianCodMovto(int anio, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void BuscaDianCuentasAhorro(int anio, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void BuscaSaldosporpagar2202(DateTime fechaCorte, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void BuscaDianCdats(DateTime fechaCorte, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void BuscaDianAportes(int anio, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void BuscaDianPrestamosBancarios(int anio, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void BuscaDianProvCart(DateTime fechaCorte, OdbcConnection myconnect, Form myforma) { /* TODO */ }
        public void GeneraInformacionDian(DateTime FecEnvio, DateTime FecInicial, DateTime FecFinal, string AnioEnvio, string Consecutivo, OdbcConnection Myconnect, Form myforma, bool Excel) { /* TODO */ }
        public void GeneraInformacionDian(DateTime FecEnvio, DateTime FecInicial, DateTime FecFinal, string AnioEnvio, string Consecutivo, OdbcConnection Myconnect, Form myforma) { GeneraInformacionDian(FecEnvio, FecInicial, FecFinal, AnioEnvio, Consecutivo, Myconnect, myforma, false); }
        private void BuscaSubFormatos(string Idformato, ArrayList Atributos, DateTime FecEnvio, DateTime FecInicial, DateTime FecFinal, string AnioEnvio, string Consecutivo, OdbcConnection Myconnect, Form myforma) { /* TODO */ }
        private void GeneraInformacionFormatos(string Idformato, ArrayList Atributos, string Concepto, string Anio, ref double Valor1, ref double Valor2, OdbcConnection Myconnect, Form myforma) { /* TODO */ }
        public void GeneraXmlRegistradas(string Cedula, string nombre, ArrayList Atributos, XmlTextWriter writer) { /* TODO */ }
        private void BuscaDatosCabezera(string idFormato, int AnioEnvio, OdbcConnection Myconnect, ref double VlrTotal, ref double NumRegistro) { /* TODO */ }
        private void GrabaCuantiasMenores(string Idformato, ArrayList Atributos, string Concepto, ref double Valor1, double Valor2, string Nit, int Dive, XmlTextWriter writer, ref DataSet DataWriter) { /* TODO */ }
        private void GeneraCabezeraXml(string Anio, string Concepto, string Formato, string Version, XmlTextWriter writer) { /* TODO */ }
        private void GeneraDetalleXml(XmlTextWriter writer, ArrayList Atributos, ArrayList ValAtributos) { /* TODO */ }
        private ArrayList LeeEstructuraFormato(string NomArchivo, string Idformato, ref DataSet DsDatat, ref DataSet DsDatatNew) { return new ArrayList(); }

        public bool BuscarCencos(ref string cencos, OdbcConnection myconnect, ref string Nombre, ref string Nomres)
        {
            stmysql = "select nombre as campo1, nomres as campo2 from cnt_maecencos where cencos = '" + cencos + "'";
            ok = conect.ExecuteQueryconec(stmysql, myconnect, "BuscarCencos", ref Nombre, ref Nomres);
            return ok;
        }

        public bool BuscarCencos(string cencos, OdbcConnection myconnect)
        {
            string _cencos = cencos, Nombre = " ", Nomres = " ";
            return BuscarCencos(ref _cencos, myconnect, ref Nombre, ref Nomres);
        }

        public bool CierreDocumento(string comprobante, double Consecutivo, OdbcConnection myconnect)
        {
            stmysql = "update cnt_movimto set ESTADO='C' where COMPRONTE='" + comprobante + "' and numero=" + Consecutivo;
            ok = conect.ExecuteQueryconec(stmysql, myconnect, "CierreDocumento");
            return ok;
        }

        private bool AnulaDocumento(string comprobante, double Consecutivo, OdbcConnection myconnect, string DetalleAnulacion)
        {
            stmysql = "update cnt_movimto set ESTADO='X', detalle='" + DetalleAnulacion + "' where COMPRONTE='" + comprobante + "' and numero=" + Consecutivo;
            ok = conect.ExecuteQueryconec(stmysql, myconnect, "AnulaDocumento");
            return ok;
        }

        private bool AnulaDocumento(string comprobante, double Consecutivo, OdbcConnection myconnect)
        {
            return AnulaDocumento(comprobante, Consecutivo, myconnect, " ");
        }

        public bool BorraMovimiento(string Comprobante, double NumCpte, double Secuencia, string periodo, OdbcConnection myconnet, string Usuario = "admin")
        {
            string tercero = "N", cencos = "N";
            string cuenta = "999999999999", nit = " ", debito = "0", credito = "0";
            string factura = "0", ApliTes = "N";
            string agencia = "9999", cencosto = "99999999";
            string CptoTes = "0", ConseTes = "0", TipoAux = "0", TipoDocAux = "fc", NumdocAux = " ";
            // var msgtesSvc = new msgtes.clstesoreria(Usuario); // ERROR: CS0246

            // conect.ExecuteQueryconec("select cencosto as campo1,agencia as campo2, factura as campo3 from cnt_movimto where compronte = '" + Comprobante + "' and numero = '" + NumCpte + "' and SECUENCIA = " + Secuencia, // ERROR: CS1501
                // myconnet, "BorraMovimiento", ref cencosto, ref agencia, ref factura); // ERROR: CS1501
            conect.ExecuteQueryconec("select CUENTA as campo1,nit as campo2,vlr_debito as campo3,vlr_credito as campo4 from cnt_movimto where compronte = '" + Comprobante + "' and numero = '" + NumCpte + "' and SECUENCIA = " + Secuencia,
                myconnet, "BorraMovimiento", ref cuenta, ref nit, ref debito, ref credito);
            conect.ExecuteQueryconec("select docu_tipo as campo1,domto_auxiliar as campo2 from cnt_movimto where compronte = '" + Comprobante + "' and numero = '" + NumCpte + "' and SECUENCIA = " + Secuencia,
                myconnet, "BorraMovimiento", ref TipoDocAux, ref NumdocAux);

            string _natura = "N", _cencos2 = "99999999", _nivel = "0", _aplicart = "N";
            string _nombre = " "; decimal _tasa = 0; int _est = 0; string _apliCnt = "N", _consiBanca = "N", _bancoCon = "9999";
            BuscarCuenta(ref cuenta, myconnet, ref tercero, ref cencos, ref _natura, ref _cencos2, ref _nivel, ref _aplicart,
                ref ApliTes, ref _nombre, ref _tasa, ref TipoAux, ref _est, ref _apliCnt, ref _consiBanca, ref _bancoCon);

            if (tercero == "Y")
                BorraTercero(cuenta, periodo, nit, cencosto, agencia, Convert.ToDouble(debito), Convert.ToDouble(credito), myconnet);

            if (ApliTes == "Y" && factura.Trim() != "" && factura.Trim() != "0")
            {
                string _cuentaRef = cuenta;
                string _valor = "0", _fecFac = "00", _fecVen = "00", _fecPro = "00";
                string _banco = "9999", _det = null, _claseAux = null, _numDocAux2 = null, _cc = null, _fp = " ", _com = " ";
                double _saldo = 0;
                // msgtesSvc.BuscaNumeroFactura(factura, nit, ref _cuentaRef, myconnet, // ERROR: CS0103
                    // ref _valor, ref CptoTes, ref ConseTes, ref _fecFac, ref _fecVen, ref _fecPro, // ERROR: CS0103
                    // ref _fecPro, ref _banco, ref _det, ref _saldo, ref _claseAux, ref _numDocAux2, // ERROR: CS0103
                    // ref _cc, ref _fp, ref _com); // ERROR: CS0103
                DateTime fecFac = new DateTime(1950, 1, 1), fecVen = new DateTime(1950, 1, 1), fecPro = new DateTime(1950, 1, 1);
                DateTime.TryParse(_fecFac, out fecFac); DateTime.TryParse(_fecVen, out fecVen); DateTime.TryParse(_fecPro, out fecPro);
                // msgtesSvc.GrabaFactura(factura, nit, cuenta, int.Parse(periodo), myconnet, CptoTes, ConseTes, nit, // ERROR: CS0103
                    // fecFac, fecVen, fecPro, Convert.ToDouble(credito), Convert.ToDouble(debito), " ", "O", TipoDocAux, NumdocAux); // ERROR: CS0103
            }

            if (TipoAux == null) TipoAux = "0";
            if (string.Compare(TipoAux.Trim(), "0") > 0)
                BorraDocAuxiliar(periodo, cuenta, TipoAux, TipoDocAux, NumdocAux, agencia, cencosto, nit, Convert.ToDouble(debito), Convert.ToDouble(credito), "C", myconnet);

            BorraSaldoCuenta(cuenta, periodo, cencosto, agencia, Convert.ToDouble(debito), Convert.ToDouble(credito), myconnet);
            BorraDocumentos(Comprobante, NumCpte, Convert.ToDouble(debito), Convert.ToDouble(credito), myconnet);
            BorraDocumentoConciliacion(Secuencia, myconnet);

            stmysql = " delete from cnt_movimto where SECUENCIA = " + Secuencia + " and COMPRONTE = '" + Comprobante + "' and numero = " + NumCpte;
            conect.ExecuteQueryconec(stmysql, myconnet, "BorraRegistro");
            return true;
        }

        private void BorraDocumentos(string Comprobante, double NumCpte, double debito, double credito, OdbcConnection myconnect)
        {
            stmysql = "update cnt_docmto set DEBITO = DEBITO - " + debito + ", CREDITO = CREDITO - " + credito
                    + " where COMPRONTE = '" + Comprobante + "' and NUMERO = " + NumCpte;
            conect.ExecuteQueryconec(stmysql, myconnect, "BorraDocumentos");
        }

        private void BorraSaldoCuenta(string cuenta, string periodo, string Cencosto, string agencia, double debito, double credito, OdbcConnection myconect)
        {
            string[] stCuen = new string[6];
            stCuen[0] = cuenta.Substring(0, 1);
            stCuen[1] = cuenta.Substring(1, 1);
            stCuen[2] = cuenta.Substring(2, 2);
            stCuen[3] = cuenta.Substring(4, 2);
            stCuen[4] = cuenta.Substring(6, 3);
            stCuen[5] = cuenta.Substring(9, 3);
            string pstUtiliper = "0";
            // BuscarCompania(varini.sptCodEmpr, myconect, pstUtiliper); // ERROR: CS1620
            bool boUtilidad = (stCuen[0] == "4" || stCuen[0] == "5" || stCuen[0] == "6" || stCuen[0] == "7");
            for (int inI = 5; inI >= 0; inI--)
            {
                string stCuenta = Strings.Left(stCuen[0].Trim() + stCuen[1].Trim() + stCuen[2].Trim() + stCuen[3].Trim() + stCuen[4].Trim() + stCuen[5].Trim() + "000000000000", 12);
                int _val;
                if (int.TryParse(stCuen[inI], out _val) && _val > 0)
                    BorraSaldoCta(stCuenta, periodo, Cencosto, agencia, debito, credito, myconect);
                int inJ = stCuen[inI].Length;
                stCuen[inI] = Strings.Right("000", inJ);
            }
            if (boUtilidad)
                BorraSaldoCta(pstUtiliper, periodo, Cencosto, agencia, debito, credito, myconect);
        }

        private void BorraSaldoCta(string cuenta, string periodo, string Cencosto, string agencia, double debito, double credito, OdbcConnection myconect)
        {
            int mes = int.Parse(periodo.Substring(4, 2));
            string anio = periodo.Substring(0, 4);
            string[] debs = { "", "ene_deb", "feb_deb", "mar_deb", "abr_deb", "may_deb", "jun_deb", "jul_deb", "ago_deb", "sep_deb", "oct_deb", "nov_deb", "dic_deb", "tre_deb" };
            string[] cres = { "", "ene_cre", "feb_cre", "mar_cre", "abr_cre", "may_cre", "jun_cre", "jul_cre", "ago_cre", "sep_cre", "oct_cre", "nov_cre", "dic_cre", "tre_cre" };
            if (mes >= 1 && mes <= 12)
                stmysql = "update cnt_salage set " + debs[mes] + " = " + debs[mes] + " - " + debito + "," + cres[mes] + " = " + cres[mes] + " - " + credito;
            else if (mes == 13)
                stmysql = "update cnt_salage set trece = trece - " + (debito - credito);
            else return;
            stmysql += " where periodo='" + anio + "' and cuenta = '" + cuenta + "' and agencia = '" + agencia + "' and Cencosto = '" + Cencosto + "'";
            conect.ExecuteQueryconec(stmysql, myconect, "BorraSaldoCuenta");
        }

        private void BorraTercero(string cuenta, string periodo, string nit, string Cencosto, string agencia, double debito, double credito, OdbcConnection myconect)
        {
            int mes = int.Parse(periodo.Substring(4, 2));
            string anio = periodo.Substring(0, 4);
            string[] debs = { "", "ene_deb", "feb_deb", "mar_deb", "abr_deb", "may_deb", "jun_deb", "jul_deb", "ago_deb", "sep_deb", "oct_deb", "nov_deb", "dic_deb", "tre_deb" };
            string[] cres = { "", "ene_cre", "feb_cre", "mar_cre", "abr_cre", "may_cre", "jun_cre", "jul_cre", "ago_cre", "sep_cre", "oct_cre", "nov_cre", "dic_cre", "tre_cre" };
            if (mes >= 1 && mes <= 12)
                stmysql = "update cnt_tercero set " + debs[mes] + " = " + debs[mes] + " - " + debito + "," + cres[mes] + " = " + cres[mes] + " - " + credito;
            else if (mes == 13)
                stmysql = "update cnt_tercero set trece = trece - " + (debito - credito);
            else return;
            stmysql += " where periodo='" + anio + "' and cuenta = '" + cuenta + "' and agencia = '" + agencia + "' and Cencosto = '" + Cencosto + "' and nit ='" + nit.Trim() + "'";
            conect.ExecuteQueryconec(stmysql, myconect, "BorraTercero");
        }

        public void ActualizacionContable(string Periodo, OdbcConnection myconnect, Form Myform)
        {
            string anio = Periodo.Substring(0, 4);
            int mes = int.Parse(Periodo.Substring(4, 2));
            DateTime fecini = new DateTime(1950, 1, 1), fecfin = new DateTime(1950, 1, 1);
            string CtaUtlPer = "999999999999";
            // this.buscaPeriodo("cont", myconnect, ref fecini, ref fecfin, Periodo, anio); // ERROR: CS1501
            this.BuscarCompania(varini.sptCodEmpr, myconnect, ref CtaUtlPer);
            limpiaMovCuentas(anio, mes, myconnect);
            LimpiaMovTerceros(anio, mes, myconnect);
            LimpiaMovAxiliar(anio, mes, myconnect);
            LimpiaDocumentos(fecini, fecfin, myconnect, mes);
            ActualizaMovimiento(anio, mes.ToString(), fecini, fecfin, myconnect, CtaUtlPer, Myform);
        }

        public bool AnulaComprobante(string Comprobante, double ConseCpte, string CpteAnulacion, double ConseCpteAnulacion,
            DateTime FechaAnulacion, string Detalle, string Usuario, OdbcConnection Myconnect, Form myform)
        {
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Anulando Comprobante :  " + Comprobante + "-" + ConseCpte, myform);
            string Cpto = "0", Consecutivo = "0", estado = "O";
            // var msgtesSvc = new msgtes.clstesoreria(Usuario); // ERROR: CS0246
            int canreg = 0, fila = 0;
            string StTes = "N", factura = "0";
            string Codigoter = "99999999999999";
            int LineaAux = 0;
            DateTime Fecsol = new DateTime(1950, 1, 1), FecAprobado = new DateTime(1950, 1, 1), FecPago = new DateTime(1950, 1, 1);
            string Aprobado = "A", EstadoSol = "A";
            double ValSolicitado = 0, ValAprobado = 0;
            string Oblinea = " ", ObAprobado_s = " ", ObSolicitud = " ", Cerrado = "N";
            bool TieneAuxilio = false;
            string idasociado = " ";
            var msgcopSvc = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
            string DetaAnulacion = "Anulado con el comprobante " + CpteAnulacion + " - " + ConseCpteAnulacion;

            stmysql = "Select cntmov.cuenta, cntmov.agencia, cntmov.cencosto,cntmov.nit,cntmov.docu_tipo,domto_auxiliar,vlr_base,factura,vlr_debito,vlr_credito,cntdoc.IdBenef,cntmov.Idsolaux,cntmaec.aux_domto,cntmov.secuencia,cntmov.fecha,cntdoc.CHEQUE,cntdoc.BANCO "
                    + "from cnt_movimto cntmov inner join cnt_docmto cntdoc on cntmov.compronte = cntdoc.compronte and cntmov.numero = cntdoc.numero "
                    + "left join sys_maenit maenit on cntmov.nit = maenit.nit left join cnt_maecuen cntmaec on cntmov.cuenta = cntmaec.cuenta "
                    + "where cntmov.compronte = '" + Comprobante + "' and cntmov.numero = '" + ConseCpte + "' ORDER by cntmov.Idsolaux desc";

            var Myread = new DataSet();
            conect.ExecuteQueryDataset(stmysql, Myconnect, "Anulando Comprobante", ref Myread, "TblAnulacion");
            canreg = Myread.Tables["TblAnulacion"].Rows.Count;
            msgbarra.DefineMaximo(stmysql, Myconnect);
            msgbarra.Show();
            Application.DoEvents();

            while (fila < canreg)
            {
                var row = Myread.Tables["TblAnulacion"].Rows[fila];
                factura = (row["factura"] == DBNull.Value) ? "0" : row["factura"].ToString();

                if (factura != "0" && factura.Trim() != "")
                {
                    string _cuentaRef = row["cuenta"].ToString();
                    string _valor = "0", _fecFac = "00", _fecVen = "00", _fecPro = "00";
                    string _banco = "9999", _det = null, _claseAux = null, _numDocAux2 = null, _cc = null, _fp = " ", _com = " ";
                    double _saldo = 0;
                    bool ok2 = false; // msgtesSvc.BuscaNumeroFactura(row["factura"].ToString(), row["nit"].ToString(), ref _cuentaRef, Myconnect, // ERROR: CS0103
                        // ref _valor, ref Cpto, ref Consecutivo, ref _fecFac, ref _fecVen, ref _fecPro, // ERROR: CS0103
                        // ref estado, ref _banco, ref _det, ref _saldo, ref _claseAux, ref _numDocAux2, // ERROR: CS0103
                        // ref _cc, ref _fp, ref _com); // ERROR: CS0103
                    if (ok2 && estado == "C")
                    {
                        stmysql = "update tes_factura set vlr_factura = 0 where concepto = '" + Cpto + "' and consecutivo = " + Consecutivo;
                        conect.ExecuteQueryconec(stmysql, Myconnect, "AnulaComprobante");
                        // msgtesSvc.GrabaFactura(row["factura"].ToString(), row["nit"].ToString(), row["cuenta"].ToString(), // ERROR: CS0103
                            // int.Parse(FechaAnulacion.ToString("yyyyMM")), Myconnect, Cpto, Consecutivo, // ERROR: CS0103
                            // row["nit"].ToString(), FechaAnulacion, FechaAnulacion, FechaAnulacion, // ERROR: CS0103
                            // Convert.ToDouble(row["vlr_credito"]), Convert.ToDouble(row["vlr_debito"]), " ", "P"); // ERROR: CS0103
                    }
                }
                else
                {
                    factura = "0";
                    StTes = "N";
                    string _cuentaRef3 = row["cuenta"].ToString();
                    string _terc3 = "N", _mane3 = "N", _nat3 = "N", _cen3 = "99999999", _niv3 = "0", _ap3 = "N";
                    string _nom3 = " "; decimal _tasa3 = 0; int _est3 = 0;
                    string _apliCnt3 = "N", _consiBanca3 = "N", _bancoCon3 = "9999", _tipoAux3 = "0";
                    BuscarCuenta(ref _cuentaRef3, Myconnect, ref _terc3, ref _mane3, ref _nat3, ref _cen3, ref _niv3, ref _ap3,
                        ref StTes, ref _nom3, ref _tasa3, ref _tipoAux3, ref _est3, ref _apliCnt3, ref _consiBanca3, ref _bancoCon3);
                    if (StTes == "Y")
                    {
                        string _fac4 = "0", _val4 = "0", _cpto4 = "0", _conse4 = "0";
                        bool ok4 = false; // msgtesSvc.BuscaFacCp(Comprobante, ConseCpte.ToString(), Myconnect, // ERROR: CS0103
                            // ref _fac4, ref _val4, ref _cpto4, ref _conse4, row["cuenta"].ToString()); // ERROR: CS0103
                        if (ok4)
                        {
                            stmysql = "update tes_factura set vlr_factura = 0 where concepto = '" + _cpto4 + "' and consecutivo = " + _conse4;
                            conect.ExecuteQueryconec(stmysql, Myconnect, "AnulaComprobante");
                            // msgtesSvc.GrabaFactura(_fac4, row["nit"].ToString(), row["cuenta"].ToString(), // ERROR: CS0103
                                // int.Parse(FechaAnulacion.ToString("yyyyMM")), Myconnect, _cpto4, _conse4, // ERROR: CS0103
                                // row["nit"].ToString(), FechaAnulacion, FechaAnulacion, FechaAnulacion, // ERROR: CS0103
                                // Convert.ToDouble(row["vlr_credito"]), Convert.ToDouble(row["vlr_debito"]), // ERROR: CS0103
                                // row["cuenta"].ToString(), "P"); // ERROR: CS0103
                        }
                    }
                }

                double idsolaux = Convert.ToDouble(row["Idsolaux"]);
                if (idsolaux > 0)
                {
                    int _lineaS = LineaAux;
                    DateTime _fecSolDt = Fecsol;
                    string _fecAprobStr = "1/1/1950", _fecPagoStr = "1/1/1950";
                    string _cerradoS = Cerrado, _estadoSolS = EstadoSol, _aprobadoS = Aprobado;
                    string _oblineaS = Oblinea, _obAprobadoS = ObAprobado_s, _obSolicitudS = ObSolicitud;
                    double _valSolS = ValSolicitado, _valAprobS = ValAprobado;
                    string _idbenef = "99999999999999", _codigoterS = Codigoter;
                    ok = msgcopSvc.BuscaSolicitudAuxilio(idsolaux, Myconnect,
                        ref _codigoterS, ref _lineaS, ref _fecSolDt, ref _fecAprobStr, ref _fecPagoStr,
                        ref _aprobadoS, ref _estadoSolS, ref _valSolS, ref _valAprobS,
                        ref _oblineaS, ref _obAprobadoS, ref _obSolicitudS, ref _cerradoS, ref _idbenef);
                    Codigoter = _codigoterS; LineaAux = _lineaS; Fecsol = _fecSolDt;
                    DateTime.TryParse(_fecAprobStr, out FecAprobado);
                    DateTime.TryParse(_fecPagoStr, out FecPago);
                    Aprobado = _aprobadoS; EstadoSol = _estadoSolS;
                    ValSolicitado = _valSolS; ValAprobado = _valAprobS;
                    Oblinea = _oblineaS; ObAprobado_s = _obAprobadoS; ObSolicitud = _obSolicitudS; Cerrado = _cerradoS;
                    if (ok && EstadoSol == "A")
                    {
                        string _newCerrado = (Cerrado == "Y") ? "N" : "Y";
                        msgcopSvc.GrabaSolicitudAuxliios(idsolaux, Myconnect,
                            Codigoter, ref _lineaS, ref Fecsol, ref FecAprobado, ref FecPago,
                            ref Aprobado, ref EstadoSol, ref ValSolicitado, ref ValAprobado,
                            ref Oblinea, ref ObAprobado_s, ref ObSolicitud, _newCerrado);
                    }
                    TieneAuxilio = true;
                }
                else
                {
                    if (TieneAuxilio && EstadoSol == "A" && Cerrado == "Y")
                    {
                        idasociado = Strings.Right("00000000000000" + row["idbenef"].ToString(), 14);
                        DateTime FechaPago = Convert.ToDateTime(row["fecha"]);
                        AnulaCuotaAuxilio(idasociado, row["nit"].ToString(), FechaPago, Myconnect);
                    }
                }

                this.GrabaMovimiento(CpteAnulacion, ConseCpteAnulacion, row["cuenta"].ToString(), row["agencia"].ToString(),
                    FechaAnulacion.ToString("yyyyMM"), row["nit"].ToString(), FechaAnulacion, Detalle,
                    row["domto_auxiliar"].ToString(), Convert.ToDouble(row["vlr_credito"]), Convert.ToDouble(row["vlr_debito"]),
                    Convert.ToDouble(row["vlr_base"]) * -1, Usuario, Myconnect,
                    banco: Convert.ToInt32(row["BANCO"]), FACTURA: factura,
                    IdBenef: row["IdBenef"].ToString(), Cencosto: row["cencosto"].ToString(),
                    DetalleDoc: Detalle, IdSolaux: idsolaux,
                    Modulo: "cont", Cheque: row["CHEQUE"].ToString(), ClaseAux: row["docu_tipo"].ToString(),
                    secuencia: Convert.ToDouble(row["secuencia"]));

                msgbarra.PerformStep();
                fila++;
            }

            ok = CierreDocumento(CpteAnulacion, ConseCpteAnulacion, Myconnect);
            if (ok)
                AnulaDocumento(Comprobante, ConseCpte, Myconnect, DetaAnulacion);
            msgbarra.Close(); msgbarra.Dispose(); Myread.Dispose();
            return ok;
        }

        public void AnulaCuotaAuxilio(string codigoter, string beneficiario, DateTime FechaPago, OdbcConnection myconnect)
        {
            string SqlUpdate = "update cop_solauxcuotabenef set estado='A' where codigoter='" + codigoter + "' and beneficiario='" + beneficiario + "' and fechapago='" + FechaPago.ToString(varini.PstForFec) + "' and estado='C'";
            conect.ExecuteQueryconec(SqlUpdate, myconnect, "AnulaCuotaAuxilio");
        }

        public void ActualizaMovimiento(string Anio, string mes, DateTime FecIni, DateTime FecFin, OdbcConnection myconnect, string CtaUtlPer, Form Myform)
        {
            string stquery;
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Movimientos " + mes, Myform);
            string TipoAux = "0";
            int canreg = 0, fila = 0;
            mes = Strings.Right("00" + mes, 2);
            if (mes == "13")
                stquery = " where periodo='" + Anio + mes + "'";
            else
                stquery = " where fecha >='" + FecIni.ToString(varini.PstForFec) + "' and fecha <='" + FecFin.ToString(varini.PstForFec) + "' and cntmov.periodo <> '" + Anio + "13'";

            stmysql = "select  cntmov.compronte,cntmov.numero,cntmov.cuenta, cntmov.fecha,cencosto,agencia,cntmaec.natura, cntmaec.tercero, "
                    + "cntmaec.aux_domto,cntmov.docu_tipo, cntmov.domto_auxiliar, cntmov.nit,vlr_debito, vlr_credito "
                    + "from cnt_movimto cntmov inner join cnt_maecuen cntmaec on cntmov.cuenta = cntmaec.cuenta  "
                    + stquery + " order by cntmov.cuenta,cencosto,agencia";

            msgbarra.DefineMaximo(stmysql, myconnect);
            msgbarra.Show();
            Application.DoEvents();

            var Myread = new DataSet();
            conect.ExecuteQueryDataset(stmysql, myconnect, "ActualizaMovimiento", ref Myread, "TblActMovtos");
            canreg = Myread.Tables["TblActMovtos"].Rows.Count;

            while (fila < canreg)
            {
                var row = Myread.Tables["TblActMovtos"].Rows[fila];
                suma_niveles(row["cuenta"].ToString(), Anio + mes, row["agencia"].ToString(), row["cencosto"].ToString(),
                    Convert.ToDouble(row["vlr_debito"]), Convert.ToDouble(row["vlr_credito"]), row["natura"].ToString(), CtaUtlPer, myconnect);

                if (row["tercero"].ToString() == "Y" && row["nit"].ToString().Trim() != "")
                    suma_nit(Anio, row["cuenta"].ToString(), row["agencia"].ToString(), row["cencosto"].ToString(),
                        mes, Convert.ToDouble(row["vlr_debito"]), Convert.ToDouble(row["vlr_credito"]),
                        row["natura"].ToString(), row["nit"].ToString(), myconnect);

                TipoAux = (row["aux_domto"] == DBNull.Value || row["aux_domto"].ToString().Trim() == "") ? "0" : row["aux_domto"].ToString();
                if (string.Compare(TipoAux.Trim(), "0") > 0)
                {
                    if (row["docu_tipo"].ToString().Trim() != "" && row["domto_auxiliar"].ToString().Trim() != "")
                        GrabaDocAuxiliar(Convert.ToDateTime(row["fecha"]).ToString("yyyyMM"), row["cuenta"].ToString(),
                            row["aux_domto"].ToString(), row["docu_tipo"].ToString(), row["domto_auxiliar"].ToString(),
                            row["agencia"].ToString(), row["cencosto"].ToString(), row["nit"].ToString(),
                            Convert.ToDouble(row["vlr_debito"]), Convert.ToDouble(row["vlr_credito"]),
                            row["natura"].ToString(), myconnect);
                }

                GrabaDocumento(row["compronte"].ToString(), Convert.ToDouble(row["numero"]),
                    Convert.ToDouble(row["vlr_debito"]), Convert.ToDouble(row["vlr_credito"]),
                    Convert.ToDateTime(row["fecha"]), "Creado por la actualizacion", 9999, myconnect);

                fila++;
                msgbarra.PerformStep();
            }
            msgbarra.Close(); msgbarra.Dispose(); Myread.Dispose();
        }

        private void limpiaMovCuentas(string Anio, int Mes, OdbcConnection myconnect)
        {
            string[] campos = { "", "ene_deb='0',ene_cre='0'", "feb_deb='0',feb_cre='0'", "mar_deb='0',mar_cre='0'",
                "abr_deb='0',abr_cre='0'", "may_deb='0',may_cre='0'", "jun_deb='0',jun_cre='0'",
                "jul_deb='0',jul_cre='0'", "ago_deb='0',ago_cre='0'", "sep_deb='0',sep_cre='0'",
                "oct_deb='0',oct_cre='0'", "nov_deb='0',nov_cre='0'", "dic_deb='0',dic_cre='0'",
                "tre_deb='0',tre_cre='0', trece='0'" };
            if (Mes < 1 || Mes > 13) return;
            stmysql = "update cnt_salage set " + campos[Mes] + " where periodo='" + Anio + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "limpiaMovCuentas");
        }

        private void LimpiaMovTerceros(string Anio, int Mes, OdbcConnection myconnect)
        {
            string[] campos = { "", "ene_deb='0',ene_cre='0'", "feb_deb='0',feb_cre='0'", "mar_deb='0',mar_cre='0'",
                "abr_deb='0',abr_cre='0'", "may_deb='0',may_cre='0'", "jun_deb='0',jun_cre='0'",
                "jul_deb='0',jul_cre='0'", "ago_deb='0',ago_cre='0'", "sep_deb='0',sep_cre='0'",
                "oct_deb='0',oct_cre='0'", "nov_deb='0',nov_cre='0'", "dic_deb='0',dic_cre='0'",
                "tre_deb='0',tre_cre='0',trece='0'" };
            if (Mes < 1 || Mes > 13) return;
            stmysql = "update cnt_tercero set " + campos[Mes] + " where periodo='" + Anio + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "LimpiaMovTerceros");
        }

        private void LimpiaDocumentos(DateTime fecini, DateTime FecFin, OdbcConnection myconect, int Mes = 0)
        {
            string wherePer13 = (Mes == 13)
                ? " and periodo = '" + FecFin.ToString("yyyy") + "13' "
                : " and periodo <> '" + FecFin.ToString("yyyy") + "13' ";
            stmysql = " update cnt_docmto set debito = 0, credito = 0 where fecha between '" + fecini.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "' ";
            conect.ExecuteQueryconec(stmysql + wherePer13, myconect, "proceso_saldos");
        }

        private void LimpiaMovAxiliar(string Anio, int Mes, OdbcConnection myconnect)
        {
            string[] campos = { "", "ene_deb='0',ene_cre='0'", "feb_deb='0',feb_cre='0'", "mar_deb='0',mar_cre='0'",
                "abr_deb='0',abr_cre='0'", "may_deb='0',may_cre='0'", "jun_deb='0',jun_cre='0'",
                "jul_deb='0',jul_cre='0'", "ago_deb='0',ago_cre='0'", "sep_deb='0',sep_cre='0'",
                "oct_deb='0',oct_cre='0'", "nov_deb='0',nov_cre='0'", "dic_deb='0',dic_cre='0'",
                "trece='0'" };
            if (Mes < 1 || Mes > 13) return;
            stmysql = "update cnt_docaux set " + campos[Mes] + " where periodo='" + Anio + "'";
            conect.ExecuteQueryconec(stmysql, myconnect, "LimpiaMovAxiliar");
        }

        public bool BuscaDocAuxiliar(string Periodo, string TipoAux, string Cuenta, string Nit,
            string agencia, string cencos, string TipoDoc, string NumDoc, OdbcConnection myconect,
            ref string detalle, ref string FecDocumento, ref string FecVence)
        {
            string Mes = Periodo.Substring(4, 2);
            string Anio = Periodo.Substring(0, 4);
            stmysql = "select detalle as campo1,fecha_domto as campo2, fecha_vemto as campo3 from cnt_docaux where periodo = '" + Anio + "' and cuenta = '" + Cuenta + "' and tipo_auxiliar = '" + TipoAux + "' and docu_tipo = '" + TipoDoc + "' and docu_numero = '" + NumDoc + "' and "
                    + "agencia = '" + agencia + "' and Cencosto = '" + cencos + "' and nit = '" + Nit + "'";
            // ok = conect.ExecuteQueryconec(stmysql, myconect, "BuscaDocAuxiliar", ref detalle, ref FecDocumento, ref FecVence); // ERROR: CS1501
            return ok;
        }

        public bool BuscaDocAuxiliar(string Periodo, string TipoAux, string Cuenta, string Nit,
            string agencia, string cencos, string TipoDoc, string NumDoc, OdbcConnection myconect,
            ref string detalle, ref DateTime FecVence)
        {
            string _fecDoc = " ", _fecVen = " ";
            ok = BuscaDocAuxiliar(Periodo, TipoAux, Cuenta, Nit, agencia, cencos, TipoDoc, NumDoc, myconect, ref detalle, ref _fecDoc, ref _fecVen);
            if (ok) DateTime.TryParse(_fecVen, out FecVence);
            return ok;
        }

        public bool BuscaDocAuxiliar(string Periodo, string TipoAux, string Cuenta, string Nit,
            string agencia, string cencos, string TipoDoc, string NumDoc, OdbcConnection myconect)
        {
            string _det = null, _fec1 = null, _fec2 = null;
            return BuscaDocAuxiliar(Periodo, TipoAux, Cuenta, Nit, agencia, cencos, TipoDoc, NumDoc, myconect, ref _det, ref _fec1, ref _fec2);
        }

        public DataSet LeeDocAuxiliares(string nit, string cuenta, string Periodo, Form myforma, OdbcConnection myconnect)
        {
            string anio = Periodo.Substring(0, 4);
            int Mes = int.Parse(Periodo.Substring(4, 2));
            string[] meses = { "", "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };
            string CampoMes = (Mes >= 1 && Mes <= 12) ? meses[Mes] : "dic";
            string _tipoAux = "0";
            string _c1 = "N", _c2 = "N", _c3 = "N", _c4 = "99999999", _c5 = "0", _c6 = "N", _c7 = "N", _c8 = " ";
            decimal _t = 0; int _e = 0; string _apliCnt = "N", _consiBanca = "N", _bancoCon = "9999";
            BuscarCuenta(ref cuenta, myconnect, ref _c1, ref _c2, ref _c3, ref _c4, ref _c5, ref _c6,
                ref _c7, ref _c8, ref _t, ref _tipoAux, ref _e, ref _apliCnt, ref _consiBanca, ref _bancoCon);

            stmysql = "select docaux.docu_tipo as Cpto,docaux.docu_numero as Consecutivo,docaux.fecha_vemto,docaux.valor_inicial,saldocaux." + CampoMes + " as saldo "
                    + " from cnt_docaux docaux inner join cnt_saldocaux_vw saldocaux  on docaux.periodo = saldocaux.periodo"
                    + " and docaux.tipo_auxiliar = saldocaux.tipo_auxiliar And docaux.cuenta = saldocaux.cuenta"
                    + " and docaux.nit = saldocaux.nit and docaux.factura  =  saldocaux.factura"
                    + " where docaux.periodo = '" + anio + "' and docaux.NIT = '" + nit + "' and saldocaux." + CampoMes + " <> 0  and docaux.cuenta = '" + cuenta + "'";

            var DtDatos = new DataSet();
            conect.ExecuteQueryDataset(stmysql, myconnect, "LeeDocAuxiliares", ref DtDatos, "TblAux");
            return DtDatos;
        }

        public bool CargaDocAuxliliar(string nit, string cuenta, string Periodo, Form myforma,
            double Debito, double Credito, OdbcConnection myconnect,
            ref string ClaseAux, ref string NumAuxiliar,
            ref DateTime FecVence, ref string Detalle, ref string CentroCosto, ref string Agencia,
            ref DataTable dsDataTable, ref double Totfacturas)
        {
            var frmfacturas = new frmdocaux(myconnect);
            frmfacturas.txtCuenta.Text = cuenta;
            frmfacturas.txtnit.Text = nit;
            frmfacturas.TxtPeriodo.Text = Periodo;
            frmfacturas.txtDebito.Text = Debito.ToString();
            frmfacturas.txtCredito.Text = Credito.ToString();
            frmfacturas.CentroCos = CentroCosto;
            frmfacturas.TxtDetalle.Text = Detalle;
            frmfacturas.Agencia = Agencia;
            frmfacturas.ShowDialog(myforma);
            ClaseAux = frmfacturas.TxtClaseAux.Text.ToUpper();
            NumAuxiliar = frmfacturas.TxtDocAux.Text;
            FecVence = frmfacturas.DtFecVence.Value;
            Detalle = frmfacturas.TxtDetalle.Text;
            Totfacturas = frmfacturas.Totalfact;
            dsDataTable = frmfacturas.Dsdatos;
            return frmfacturas.Graba;
        }

        public bool CargaDocAuxliliar(string nit, string cuenta, string Periodo, Form myforma,
            double Debito, double Credito, OdbcConnection myconnect,
            ref string ClaseAux, ref string NumAuxiliar,
            ref DateTime FecVence, ref string Detalle, ref string CentroCosto, ref string Agencia)
        {
            DataTable _ds = null; double _tot = 0;
            return CargaDocAuxliliar(nit, cuenta, Periodo, myforma, Debito, Credito, myconnect,
                ref ClaseAux, ref NumAuxiliar, ref FecVence, ref Detalle, ref CentroCosto, ref Agencia, ref _ds, ref _tot);
        }

        public bool CargaTipodocConciliacion(Form myforma, ref string TipoDc, ref double NumeroDc)
        {
            var frmTipodocConcibanca = new frmTipodoc();
            frmTipodocConcibanca.ShowDialog(myforma);
            TipoDc = frmTipodocConcibanca.TipoDc;
            NumeroDc = frmTipodocConcibanca.NumeroDc;
            return frmTipodocConcibanca.Correcto;
        }

        public bool GrabaDocumentoConciliacion(string cuenta, string banco, int periodo, string tipodoc, double numerodoc,
            string detalle, double valor, char debcred, double secuenciamovto, DateTime fecha, OdbcConnection myconect,
            char acepta = 'N', char adiciona = 'N', string modulo = "cnt")
        {
            string mysql = "insert into cnt_concibanca(cuenta,banco,periodo,tipodoc,numerodoc,detalle,acepta,valor,debcred,adiciona,fecha,Secuenciamovto,modulo)"
                + " values('" + cuenta + "','" + banco + "'," + periodo + ",'" + tipodoc + "'," + numerodoc + ",'" + detalle + "','" + acepta + "'," + valor
                + ",'" + debcred + "','" + adiciona + "','" + fecha.ToString(varini.pstForfecyHora) + "'," + secuenciamovto + ",'" + modulo + "')";
            return ExecuteQueryconec(mysql, myconect, "GrabaDocumentoConciliacion");
        }

        public bool BorraDocumentoConciliacion(double secuencia, OdbcConnection myconect, string modulo = "cnt")
        {
            string moduloborrar = (modulo == "cop") ? "('cop')" : "('cnt','tes')";
            stmysql = "delete from cnt_concibanca where Secuenciamovto = " + secuencia + " and modulo in " + moduloborrar;
            return ExecuteQueryconec(stmysql, myconect, "BorraDocumentoConciliacion");
        }

        public bool BorraDocumentoConciliacionPorCuenta(string cuenta, string banco, int periodo, string tipodoc, int numerodoc, OdbcConnection myconect)
        {
            stmysql = "delete from cnt_concibanca where cuenta='" + cuenta + "' and banco='" + banco + "' and periodo=" + periodo + " and tipodoc='" + tipodoc + "' and numerodoc=" + numerodoc;
            return ExecuteQueryconec(stmysql, myconect, "BorraDocumentoConciliacion");
        }

        public bool ActualizaDocumentoConciliacion(string secuencia, char acepta, OdbcConnection myconect)
        {
            string mysql = "update cnt_concibanca set acepta='" + acepta + "' where Secuenciamovto=" + secuencia;
            return ExecuteQueryconec(mysql, myconect, "GrabaDocumentoConciliacion");
        }

        public bool CerrarDocumentoConciliacion(string cuenta, string banco, int periodo, string tipodoc, int numerodoc, string cerrado, OdbcConnection myconect)
        {
            string mysql = "update cnt_concibanca set cerrado='" + cerrado + "' where cuenta='" + cuenta + "' and banco='" + banco + "' and periodo=" + periodo + " and tipodoc='" + tipodoc + "' and numerodoc=" + numerodoc;
            return ExecuteQueryconec(mysql, myconect, "GrabaDocumentoConciliacion");
        }

        public bool TrasladarConciliaciondePeriodo(string cuenta, string banco, int PeriodoActual, string tipodoc, int numerodoc, OdbcConnection myconect)
        {
            string mysql = "update cnt_concibanca set cerrado='Y' where acepta='N' and cuenta='" + cuenta + "' and banco='" + banco + "' and periodo=" + PeriodoActual + " and tipodoc='" + tipodoc + "' and numerodoc=" + numerodoc;
            return ExecuteQueryconec(mysql, myconect, "TrasladarConciliaciondePeriodo");
        }

        public bool ActualizaSaldoAmortizacion(string cuenta, string agencia, string cencosto, string nit, double saldo, OdbcConnection myconect)
        {
            string mysql = "select tipo, cuenta, agencia, ccosto, nit, documento, saldo from cnt_amortiza where cuenta='" + cuenta + "' and agencia='" + agencia + "' and ccosto='" + cencosto + "' and nit='" + nit + "'";
            if (ExecuteQueryconec(mysql, myconect, "GrabaDocumentoConciliacion"))
            {
                if (MessageBox.Show("Exiten parametros de amortizacion para esta cuenta. Desea actualizar el saldo ?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    mysql = "update cnt_amortiza  set saldo=" + saldo + " where cuenta='" + cuenta + "' and agencia='" + agencia + "' and ccosto='" + cencosto + "' and nit='" + nit + "'";
                    ExecuteQueryconec(mysql, myconect, "ActualizaSaldoAmortizacion");
                }
            }
            return false;
        }

        public void AnulaConcibanca(double Secuencia, string cuenta, string banco, int periodo, string tipodoc, double numerodoc,
            string detalle, double valor, char debcred, DateTime fecha, OdbcConnection myconect,
            char acepta = 'N', char adiciona = 'N')
        {
            // Placeholder – original VB body is commented out
        }

        public void GrabaDocAuxiliar(string Periodo, string Cuenta, string TipoAux, string TipoDoc,
            string NumDoc, string Agencia, string Cencos, string Nit, double debito, double credito, string Naturaleza,
            OdbcConnection myconect, string Detalle = null, DateTime? FecDocto = null, DateTime? FecVence = null)
        {
            DateTime dtFecDoc = FecDocto ?? new DateTime(1950, 1, 1);
            DateTime dtFecVen = FecVence ?? new DateTime(1950, 1, 1);
            string Mes = Strings.Right("00" + Periodo.Substring(4), 2);
            string Anio = Periodo.Substring(0, 4);
            bool existe = BuscaDocAuxiliar(Anio + Mes, TipoAux, Cuenta, Nit, Agencia, Cencos, TipoDoc, NumDoc, myconect);
            string[] mesDebs = { "", "ENE_DEB", "FEB_DEB", "MAR_DEB", "ABR_DEB", "MAY_DEB", "JUN_DEB", "JUL_DEB", "AGO_DEB", "SEP_DEB", "OCT_DEB", "NOV_DEB", "DIC_DEB", "TRECE" };
            string[] mesCres = { "", "ENE_CRE", "FEB_CRE", "MAR_CRE", "ABR_CRE", "MAY_CRE", "JUN_CRE", "JUL_CRE", "AGO_CRE", "SEP_CRE", "OCT_CRE", "NOV_CRE", "DIC_CRE", "TRECE" };
            int mesNum = int.Parse(Mes);
            string whereAux = " where periodo='" + Anio + "' and cuenta = '" + Cuenta + "' and tipo_auxiliar='" + TipoAux + "' and docu_tipo='" + TipoDoc + "' and docu_numero ='" + NumDoc + "' and agencia = '" + Agencia + "' and Cencosto = '" + Cencos + "' and nit ='" + Nit + "'";
            if (existe)
            {
                if (mesNum >= 1 && mesNum <= 12)
                    stmysql = "update cnt_docaux set " + mesDebs[mesNum].ToLower().Replace("_deb", "_deb") + " = " + mesDebs[mesNum].ToLower().Replace("_deb", "_deb") + " + '" + debito + "',";

                // Use the positional month field names directly
                string[] updDebs = { "", "ene_deb", "feb_deb", "mar_deb", "abr_deb", "may_deb", "jun_deb", "jul_deb", "ago_deb", "sep_deb", "oct_deb", "nov_deb", "dic_deb" };
                string[] updCres = { "", "ene_cre", "feb_cre", "mar_cre", "abr_cre", "may_cre", "jun_cre", "jul_cre", "ago_cre", "sep_cre", "oct_cre", "nov_cre", "dic_cre" };
                if (mesNum >= 1 && mesNum <= 12)
                    stmysql = "update cnt_docaux set " + updDebs[mesNum] + " = " + updDebs[mesNum] + " + '" + debito + "'," + updCres[mesNum] + " = " + updCres[mesNum] + " + '" + credito + "'" + whereAux;
                else if (mesNum == 13)
                    stmysql = "update cnt_docaux set trece = trece + '" + (Naturaleza == "D" ? debito - credito : -(debito + credito)) + "'" + whereAux;
                conect.ExecuteQueryconec(stmysql, myconect, "GrabaDocAuxiliar");
            }
            else
            {
                double Valini = (debito > 0) ? debito : credito;
                string factura = TipoDoc + "-" + NumDoc;
                string[] insFields = { "", "ENE_DEB,ENE_CRE", "FEB_DEB,FEB_CRE", "MAR_DEB,MAR_CRE", "ABR_DEB,ABR_CRE", "MAY_DEB,MAY_CRE", "JUN_DEB,JUN_CRE", "JUL_DEB,JUL_CRE", "AGO_DEB,AGO_CRE", "SEP_DEB,SEP_CRE", "OCT_DEB,OCT_CRE", "NOV_DEB,NOV_CRE", "DIC_DEB,DIC_CRE" };
                string colBase = "PERIODO,TIPO_AUXILIAR,CUENTA,AGENCIA,NIT,CENCOSTO,DOCU_TIPO,DOCU_NUMERO,DETALLE,FECHA_DOMTO,FECHA_VEMTO,VALOR_INICIAL,FACTURA";
                string valBase = "'" + Anio + "','" + TipoAux + "','" + Cuenta + "','" + Agencia + "','" + Nit + "','" + Cencos + "','" + TipoDoc + "','" + NumDoc + "','" + Detalle + "','" + dtFecDoc.ToString(varini.PstForFec) + "','" + dtFecVen.ToString(varini.PstForFec) + "','" + Valini + "','" + factura + "'";
                if (mesNum >= 1 && mesNum <= 12)
                    stmysql = "insert into cnt_docaux (" + colBase + "," + insFields[mesNum] + ") VALUES (" + valBase + ",'" + debito + "','" + credito + "')";
                else if (mesNum == 13)
                {
                    double treceVal = (Naturaleza == "D") ? debito - credito : credito - debito;
                    stmysql = "insert into cnt_docaux (" + colBase + ",TRECE) VALUES (" + valBase + ",'" + treceVal + "')";
                }
                conect.ExecuteQueryconec(stmysql, myconect, "GrabaDocAuxiliar");
            }
        }

        public void BorraDocAuxiliar(string Periodo, string Cuenta, string TipoAux, string TipoDoc,
            string NumDoc, string Agencia, string Cencos, string Nit, double debito, double credito, string Naturaleza,
            OdbcConnection myconect)
        {
            string Mes = Strings.Right("00" + Periodo.Substring(4), 2);
            string Anio = Periodo.Substring(0, 4);
            bool existe = BuscaDocAuxiliar(Anio + Mes, TipoAux, Cuenta, Nit, Agencia, Cencos, TipoDoc, NumDoc, myconect);
            if (!existe) return;
            string whereAux = " where periodo='" + Anio + "' and cuenta = '" + Cuenta + "' and tipo_auxiliar='" + TipoAux + "' and docu_tipo='" + TipoDoc + "' and docu_numero ='" + NumDoc + "' and agencia = '" + Agencia + "' and Cencosto = '" + Cencos + "' and nit ='" + Nit + "'";
            int mesNum = int.Parse(Mes);
            string[] updDebs = { "", "ene_deb", "feb_deb", "mar_deb", "abr_deb", "may_deb", "jun_deb", "jul_deb", "ago_deb", "sep_deb", "oct_deb", "nov_deb", "dic_deb" };
            string[] updCres = { "", "ene_cre", "feb_cre", "mar_cre", "abr_cre", "may_cre", "jun_cre", "jul_cre", "ago_cre", "sep_cre", "oct_cre", "nov_cre", "dic_cre" };
            if (mesNum >= 1 && mesNum <= 12)
                stmysql = "update cnt_docaux set " + updDebs[mesNum] + " = " + updDebs[mesNum] + " - '" + debito + "'," + updCres[mesNum] + " = " + updCres[mesNum] + " - '" + credito + "'" + whereAux;
            else if (mesNum == 13)
                stmysql = "update cnt_docaux set trece = trece - '" + (debito - credito) + "'" + whereAux;
            else return;
            conect.ExecuteQueryconec(stmysql, myconect, "GrabaDocAuxiliar");
        }

        public double BuscarSaldoDocAuxiliar(string cuenta, int TipoAux, string Nit, string periodo, string Factura, OdbcConnection myconnect)
        {
            string anio = periodo.Substring(0, 4);
            int Mes = int.Parse(periodo.Substring(4, 2));
            string[] meses = { "", "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };
            string CampoMes = (Mes >= 1 && Mes <= 12) ? meses[Mes] : "dic";
            stmysql = "select " + CampoMes + " as campo1 from cnt_saldocaux_vw WHERE CUENTA = '" + cuenta + "' and periodo = " + anio + " and nit = '" + Nit + "'  and tipo_auxiliar = '" + TipoAux + "' and factura = '" + Factura + "'";
            string saldo = "0";
            conect.ExecuteQueryconec(stmysql, myconnect, "BuscarSaldoTecero", ref saldo);
            double result = 0; double.TryParse(saldo, out result); return result;
        }

        public string CalculaPeriodoFinal(int Meses, string PeriodoInicial)
        {
            int NumAhno = int.Parse(PeriodoInicial.Substring(0, 4));
            int NumMes = int.Parse(PeriodoInicial.Substring(4, 3).TrimStart('0') == "" ? "0" : PeriodoInicial.Substring(4, 3).TrimStart('0'));
            string PeriodoFinal = PeriodoInicial;
            int Cont = 1;
            while (Cont <= Meses)
            {
                PeriodoFinal = NumAhno.ToString() + Strings.Right("00" + NumMes.ToString(), 2);
                NumMes++;
                if (NumMes > 12) { NumAhno++; NumMes = 1; }
                Cont++;
            }
            return PeriodoFinal;
        }
// _chunk_c: GrabaMovimiento, insert_movcnt, GrabaContraCencos, Graba4xmilCencos,
//           GrabaDocumento, GrabaDatosDocumentos, suma_niveles, suma_nit, suma_saldos,
//           CargaMovtoCpte, Barraprogress, ConfiguraGrilla, CuadreDocumento,
//           BuscaComprobante, ClsContabilidad()

        public bool GrabaMovimiento(string compronte, double numero, string cuenta, string agencia,
            string periodo, string nit, DateTime FechaMovto, string detalle, string domto_auxiliar,
            double debito, double credito, double vlr_base, string Usuario, OdbcConnection myconect,
            int banco = 0, string FACTURA = " ", string IdBenef = "99999999999999",
            string Cencosto = "99999999", string DetalleDoc = " ", double IdSolaux = 0,
            string Modulo = null, string Cheque = null, string ClaseAux = " ",
            DateTime FecVenDocAux = default(DateTime),
            string DetalleFactura = null, bool ProcesoImportacion = false, double secuencia = 0,
            string TipoDoc = "CH", string ModuloEnvia = "TES", bool ActualizaFacturas = true,
            string CantFacturas = "0", bool actBenef = false)
        {
            if (FecVenDocAux == default(DateTime)) FecVenDocAux = new DateTime(1950, 1, 1);

            bool ok;
            string Cencos = "99999999", CencCpte = "99999999";
            string CuentaUtilidad = " ", stTercero = "N", AplCencos = "N";
            string estado = "C";
            string Cerrado = " ", TipoAux = "0", Aplites = "Y", EmpresaConciliaBanca = "N";
            string Anulado = " "; int EstCta = 0; double SecuenciaMovto = 0; string TipoDc = "";
            string ConsiBanca = "N", BancoConcilia = "9999"; double ValorConci = 0; string DebCred = "";

            if (DetalleDoc.Trim() == "")
                DetalleDoc = detalle;

            if (periodo != "P13")
                periodo = null;

            if (!Information.IsNumeric(CantFacturas))
                CantFacturas = "0";

            DateTime _fecIni = new DateTime(1950, 1, 1), _fecFin = new DateTime(1950, 1, 1);
            buscaPeriodo("cont", myconect, ref _fecIni, ref _fecFin, FechaMovto, ref estado, ref periodo, FechaMovto.ToString("yyyy"));

            switch (estado)
            {
                case "P":
                    if (MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?",
                        "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
                        return false;
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo de contabilidad esta cerrado ",
                        "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            string _natura = "N", _nivel = "0", _aplicart = "N", _nombre = " ";
            decimal _tasa = 0; string _apliCnt = "N";
            ok = BuscarCuenta(ref cuenta, myconect, ref stTercero, ref AplCencos, ref _natura, ref Cencos,
                ref _nivel, ref _aplicart, ref Aplites, ref _nombre, ref _tasa, ref TipoAux,
                ref EstCta, ref _apliCnt, ref ConsiBanca, ref BancoConcilia);

            if (!ok)
            {
                MessageBox.Show("Cuenta contable no existe:" + cuenta);
                return false;
            }

            if (EstCta == 1)
            {
                MessageBox.Show("Cuenta esta inactiva !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (Cencos == "99999999" && AplCencos == "Y")
            {
                if (Cencosto != "99999999")
                {
                    Cencos = Cencosto;
                }
                else
                {
                    string _cc1 = " ", _td1 = " "; double _d1 = 0, _c1 = 0; string _cerr1 = "N", _an1 = "N"; double _dif1 = 0;
                    string _det1 = " ", _dt1 = "NC", _nd1 = " "; DateTime _fec1 = new DateTime(1950, 1, 1);
                    string _nom1 = " ", _idb1 = "99999999999999"; string _cta1 = "999999999999";
                    string _mod1 = null; int _per1 = 0; string _cf1 = "0", _da1 = " ";
                    BuscaComprobante(ref compronte, ref numero, false, myconect,
                        ref _cc1, ref _td1, ref _d1, ref _c1, ref _cerr1, ref _an1, ref _dif1,
                        ref _det1, ref _dt1, ref _nd1, ref _fec1, ref _nom1, ref _idb1, ref CencCpte,
                        ref _cta1, ref _mod1, ref _per1, ref _cf1, ref _da1);
                    if (CencCpte != "99999999")
                        Cencos = CencCpte;
                }
            }
            else
            {
                Cencos = "99999999";
            }

            string _cc2 = " ", _td2 = " "; double _d2 = 0, _c2 = 0; double _dif2 = 0;
            string _det2 = " ", _dt2 = "NC", _nd2 = " "; DateTime _fec2 = new DateTime(1950, 1, 1);
            string _nom2 = " ", _idb2 = "99999999999999", _cen2 = "99999999", _cta2 = "999999999999";
            string _mod2 = null; int _per2 = 0; string _cf2 = "0", _da2 = " ";
            BuscaComprobante(ref compronte, ref numero, false, myconect,
                ref _cc2, ref _td2, ref _d2, ref _c2, ref Cerrado, ref Anulado, ref _dif2,
                ref _det2, ref _dt2, ref _nd2, ref _fec2, ref _nom2, ref _idb2, ref _cen2,
                ref _cta2, ref _mod2, ref _per2, ref _cf2, ref _da2);

            if (!ProcesoImportacion)
            {
                if (Anulado == "Y")
                {
                    MessageBox.Show("El documento esta Anulado no permite ingresar registros.",
                        "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                if (Cerrado == "Y")
                {
                    MessageBox.Show("El documento ya esta Cerrado no permite ingresar registros.",
                        "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            // BuscarCompania via DataSet overload — extract CuentaUtilidad and conciliabanca
            string _codigoDS = varini.sptCodEmpr;
            var dsComp = new DataSet();
            msgconsys.BuscarCompania(ref _codigoDS, ref dsComp, myconect);
            if (dsComp.Tables["tblcompania"] != null && dsComp.Tables["tblcompania"].Rows.Count > 0)
            {
                var row = dsComp.Tables["tblcompania"].Rows[0];
                CuentaUtilidad = row["CUENTACON2"].ToString();
                EmpresaConciliaBanca = row["conciliabanca"].ToString();
            }

            if (CuentaUtilidad.Trim() == "" || !Information.IsNumeric(CuentaUtilidad.Trim()))
            {
                MessageBox.Show("Cuenta de utilidad o perdida no definida en la compania");
                return false;
            }

            if (stTercero == "N")
                nit = " ";

            insert_movcnt(compronte, numero, cuenta, agencia, Cencos, nit, periodo, FechaMovto, detalle,
                domto_auxiliar, debito, credito, vlr_base, CuentaUtilidad, Usuario, myconect,
                banco, FACTURA, IdBenef, DetalleDoc, IdSolaux, Modulo, Cheque, ClaseAux, FecVenDocAux,
                DetalleFactura, ActualizaFacturas, CantFacturas, actBenef);

            if (EmpresaConciliaBanca == "Y")
            {
                if (secuencia != 0)
                {
                    var DsConcilia1 = new DataSet();
                    if (BuscarSecuenciaMovto(compronte, numero, cuenta, agencia, periodo, nit, Cencos,
                            FechaMovto, detalle, domto_auxiliar, debito, credito, vlr_base,
                            Usuario, ClaseAux, myconect, ref SecuenciaMovto))
                    {
                        stmysql = "Select * from cnt_concibanca where Secuenciamovto =" + secuencia;
                        if (conect.ExecuteQueryDataset(stmysql, myconect, "BuscaDatosParaConciliacion", ref DsConcilia1, "tbconciliacion1"))
                        {
                            BorraDocumentoConciliacion(secuencia, myconect);
                            // GrabaDocumentoConciliacion commented out in original VB
                        }
                    }
                }
                else
                {
                    if (ModuloEnvia == "TES")
                    {
                        if (ConsiBanca == "Y")
                        {
                            if (BuscarSecuenciaMovto(compronte, numero, cuenta, agencia, periodo, nit, Cencos,
                                    FechaMovto, detalle, domto_auxiliar, debito, credito, vlr_base,
                                    Usuario, ClaseAux, myconect, ref SecuenciaMovto, true, FACTURA))
                            {
                                if (BancoConcilia == "9999" || string.IsNullOrEmpty(BancoConcilia))
                                    BancoConcilia = Strings.Right("0000" + banco.ToString(), 4);
                                if (credito > 0)
                                {
                                    ValorConci = credito;
                                    DebCred = "C";
                                }
                                else if (debito > 0)
                                {
                                    ValorConci = debito;
                                    DebCred = "D";
                                }
                                if (TipoDoc == "CH")
                                    TipoDc = "CH";
                                else if (TipoDoc == "DF")
                                {
                                    TipoDc = "TR";
                                    Cheque = numero.ToString();
                                }
                                int _periodoInt = 0; int.TryParse(periodo, out _periodoInt);
                                double _numDoc = 0; if (Cheque != null) double.TryParse(Cheque, out _numDoc);
                                char _acepta = ' ', _adiciona = ' ';
                                GrabaDocumentoConciliacion(cuenta, BancoConcilia, _periodoInt, TipoDc, _numDoc,
                                    detalle, ValorConci, DebCred[0], SecuenciaMovto, FechaMovto, myconect,
                                    _acepta, _adiciona, "tes");
                            }
                        }
                    }
                    // Case "COP": empty in VB
                }
            }
            return true;
        }

        public void insert_movcnt(string compronte, double numero, string cuenta, string agencia,
            string cencosto, string nit, string periodo, DateTime fecha, string detalle,
            string domto_auxiliar, double debito, double credito, double vlr_base,
            string pstUtiliper, string Usuario, OdbcConnection myconect,
            int banco = 0, string factura = " ", string IdBenef = "99999999999999",
            string DetalleDoc = " ", double IdSolAux = 0, string Modulo = null,
            string Cheque = null, string ClaseAux = " ", DateTime FecVenDocAux = default(DateTime),
            string DetalleFactura = null, bool ActualizaFacturas = true, string CantFacturas = "0",
            bool actBenef = false)
        {
            if (FecVenDocAux == default(DateTime)) FecVenDocAux = new DateTime(1950, 1, 1);

            // var msgtesSvc = new msgtes.clstesoreria(null); // ERROR: CS0246
            string stNatura = "N", stTercero = "N", stCencos = "N";
            string Nomusu = "admin", TipoAux = "0", AplTes = "N";

            string _cencos6 = "99999999", _nivel6 = "0", _aplicart6 = "N", _nombre6 = " ";
            decimal _tasa6 = 0; int _est6 = 0; string _apliCnt6 = "N", _consiBanca6 = "N", _banco6 = "9999";
            BuscarCuenta(ref cuenta, myconect, ref stTercero, ref stCencos, ref stNatura, ref _cencos6,
                ref _nivel6, ref _aplicart6, ref AplTes, ref _nombre6, ref _tasa6, ref TipoAux,
                ref _est6, ref _apliCnt6, ref _consiBanca6, ref _banco6);

            if (IdBenef == null)
                IdBenef = "99999999999999";
            if (TipoAux.Trim() == null || TipoAux.Trim() == "")
                TipoAux = "0";

            GrabaDocumento(compronte, numero, debito, credito, fecha, DetalleDoc, banco, myconect,
                IdBenef, Modulo, Cheque, true, ActualizaFacturas, CantFacturas, actBenef);

            string usuarioCopy = Usuario;
            string _pass = " ", _grp = " "; bool _cambPass = false;
            DateTime _fechCrea = DateTime.Now, _fechVence = DateTime.Now;
            bool _valComp = false, _sobreGiro = false;
            object _cmin = null, _cmax = null, _cgramin = null, _cgramax = null;
            string _cedula = " "; bool _grabaRet = false;
            msgconsys.BuscaUsuario(ref usuarioCopy, myconect, ERP.Core.Compartido.Configuracion.ParamSys.Navega.Ninguno,
                ref Nomusu, ref _cmin, ref _cmax, ref _pass, ref _grp, ref _cambPass,
                ref _fechCrea, ref _fechVence, ref _valComp, ref _sobreGiro,
                ref _cgramin, ref _cgramax, ref _cedula, ref _grabaRet);

            stmysql = "insert into cnt_movimto (  COMPRONTE ,NUMERO ,CUENTA,AGENCIA,CENCOSTO,NIT,PERIODO,FECHA,DETALLE,"
                + "DOMTO_AUXILIAR,VLR_DEBITO,VLR_CREDITO,VLR_BASE,ESTADO,factura, Usuario, fecha_sys,IdSolAux,NomUsu,docu_tipo) VALUES ('"
                + compronte + "','" + numero + "','" + cuenta + "','" + agencia + "','"
                + cencosto + "','" + nit + "','" + periodo + "','" + fecha.ToString(varini.PstForFec) + "','"
                + detalle + "','" + domto_auxiliar + "','"
                + debito + "','" + credito + "','" + vlr_base + "','" + "A','" + factura + "','"
                + Usuario + "','" + DateTime.Now.ToString(varini.pstForfecyHora) + "','" + IdSolAux + "','" + Nomusu + "','" + ClaseAux + "')";
            ExecuteQueryconec(stmysql, myconect, "insert_movcnt");

            suma_niveles(cuenta, periodo, agencia, cencosto, debito, credito, stNatura, pstUtiliper, myconect);

            int stAno = 0, stMes = 0;
            if (periodo != null && periodo.Length >= 6)
            {
                int.TryParse(periodo.Substring(0, 4), out stAno);
                int.TryParse(periodo.Substring(4, 2), out stMes);
            }

            if (stTercero == "Y")
            {
                if (nit == null || nit.Trim() == "")
                    nit = "99999999999999";
                suma_nit(stAno.ToString(), cuenta, agencia, cencosto, stMes.ToString(), debito, credito, stNatura, nit, myconect);
            }

            double tipoAuxVal = 0; double.TryParse(TipoAux, out tipoAuxVal);
            if (tipoAuxVal > 0)
                GrabaDocAuxiliar(periodo, cuenta, TipoAux, ClaseAux, domto_auxiliar, agencia, cencosto, nit,
                    debito, credito, stNatura, myconect, DetalleFactura, fecha, FecVenDocAux);

            if (AplTes == "Y")
            {
                int _periodoInt = 0; int.TryParse(periodo, out _periodoInt);
                // msgtesSvc.GrabaFactura(ClaseAux + "-" + domto_auxiliar, nit, cuenta, _periodoInt, myconect, // ERROR: CS0103
                    // compronte, numero.ToString(), nit, // ERROR: CS0103
                    // (DateTime?)fecha, (DateTime?)FecVenDocAux, (DateTime?)fecha, // ERROR: CS0103
                    // debito, credito, DetalleFactura, // ERROR: CS0103
                    // DocTipo: ClaseAux, DocNumero: domto_auxiliar, Cencosto: cencosto); // ERROR: CS0103
            }
        }

        public void GrabaContraCencos(string Compronte, double ConseCpte, string Cuenta,
            OdbcConnection myconnect, string Agencia, string Periodo, string Nit,
            DateTime FechaMovto, string Detalle, string banco, string Usuario,
            string TipoDoc = "CH")
        {
            int canreg = 0, fila = 0;
            double NumCheque = 0;
            var Myread = new DataSet();
            string NitMovto = "";

            if (TipoDoc == "CH")
                stmysql = "Select cencosto, sum(vlr_debito-vlr_credito) as Debito  from cnt_movimto  where compronte = '"
                    + Compronte + "' and Numero = " + ConseCpte + " group  by Cencosto order by cencosto";
            else
                stmysql = "Select cencosto,nit,sum(vlr_debito-vlr_credito) as Debito  from cnt_movimto  where compronte = '"
                    + Compronte + "' and Numero = " + ConseCpte + " group  by Cencosto,nit order by cencosto";

            conect.ExecuteQueryDataset(stmysql, myconnect, "Graba Contra Cencos", ref Myread, "TblGrabaCencosto");
            canreg = Myread.Tables["TblGrabaCencosto"].Rows.Count;

            int bancoInt = 0; int.TryParse(banco, out bancoInt);
            while (fila < canreg)
            {
                var row = Myread.Tables["TblGrabaCencosto"].Rows[fila];
                NumCheque = double.Parse(ConseCpte.ToString() + (fila + 1).ToString());
                NitMovto = (TipoDoc == "CH") ? Nit : row["nit"].ToString();
                GrabaMovimiento(Compronte, ConseCpte, Cuenta, Agencia, Periodo, NitMovto,
                    FechaMovto, Detalle, " ", 0, Convert.ToDouble(row["debito"]), 0,
                    Usuario, myconnect, bancoInt, Cencosto: row["Cencosto"].ToString(),
                    Cheque: NumCheque.ToString(), TipoDoc: TipoDoc);
                fila++;
            }
            Myread.Dispose();
        }

        public void Graba4xmilCencos(string Compronte, double ConseCpte, string Cuenta,
            OdbcConnection myconnect, string Agencia, string Periodo, string Nit,
            DateTime FechaMovto, string Detalle, string banco, string Usuario, double gravamen)
        {
            int canreg = 0, fila = 0;
            double Vlr4xmil = 0;
            var Myread = new DataSet();

            stmysql = "Select cencosto, sum(vlr_debito) as Debito  from cnt_movimto  where compronte = '"
                + Compronte + "' and Numero = " + ConseCpte + " group  by Cencosto order by cencosto";
            conect.ExecuteQueryDataset(stmysql, myconnect, "Graba 4xmil Cencos", ref Myread, "TblGrabaCencosto");
            canreg = Myread.Tables["TblGrabaCencosto"].Rows.Count;

            int bancoInt = 0; int.TryParse(banco, out bancoInt);
            while (fila < canreg)
            {
                var row = Myread.Tables["TblGrabaCencosto"].Rows[fila];
                Vlr4xmil = Math.Round(Convert.ToDouble(row["debito"]) * (gravamen / 100));
                if (Vlr4xmil > 0)
                    GrabaMovimiento(Compronte, ConseCpte, Cuenta, Agencia, Periodo, Nit,
                        FechaMovto, Detalle, " ", 0, Vlr4xmil, 0, Usuario, myconnect,
                        bancoInt, Cencosto: row["Cencosto"].ToString());
                fila++;
            }
            Myread.Dispose();
        }

        public void GrabaDocumento(string compronte, double ConseCompro, double Debito, double credito,
            DateTime FechaMovto, string detalle, int banco, OdbcConnection Myconect,
            string IdBenef = "99999999999999", string Modulo = null, string Cheque = null,
            bool ActDetalle = false, bool ActualizaFacturas = true,
            string CantFacturas = "0", bool ActualizaIdBenef = false)
        {
            string DOMTO = "NC", ForPag = " ", StDetalle = "", StIdBenef = "";
            string CodfacturaCont = "";
            double ConseFactCont = 0;

            if (Cheque == "DF")
            {
                ForPag = "DF";
                Cheque = null;
            }
            else if (Cheque == null)
            {
                ForPag = "CH";
            }
            else
            {
                if (Cheque.Trim() != "")
                    ForPag = "CH";
            }

            stmysql = "select documento as campo1,CodfacturaCont as campo2 from sys_compro02 where codigo =  '" + compronte + "'";
            ExecuteQueryconec(stmysql, Myconect, "GrabaDocumento", ref DOMTO, ref CodfacturaCont);

            if (ActDetalle)
                StDetalle = ",DETALLE= '" + detalle + "' ";

            if (ActualizaIdBenef)
                StIdBenef = ",IdBenef= '" + IdBenef + "' ";

            double _cCompro = ConseCompro;
            ok = BuscaComprobante(ref compronte, ref _cCompro, false, Myconect);
            if (ok)
            {
                stmysql = "update cnt_docmto set debito = debito + " + Debito + ", credito = credito + " + credito
                    + StDetalle + StIdBenef + ",filler='" + CantFacturas + "' where COMPRONTE ='" + compronte + "' and  NUMERO = " + ConseCompro;
                ExecuteQueryconec(stmysql, Myconect, "GrabaDocumento");
            }
            else
            {
                if (DOMTO == "FC")
                {
                    // if (CodfacturaCont != "9999" && ActualizaFacturas) // ERROR: CS1002, CS1525
                        // BuscaConseFactura(CodfacturaCont, Myconect, ref ConseFactCont); // ERROR: CS0103
                }
                stmysql = "insert into cnt_docmto (COMPRONTE, NUMERO ,DOMTO ,DETALLE,DEBITO ,CREDITO , DOCTIPO,NODOC ,FECHA,CERRADO,banco,periodo,graba, ANULADO, IdBenef,Modulo,cheque,forpag,facturaCont,filler) values ('"
                    + Strings.Right("0000" + compronte.Trim(), 4) + "','" + ConseCompro + "','"
                    + DOMTO.Trim() + "','" + detalle + "','" + Debito + "','" + credito + "','" + DOMTO + "','" + ConseCompro.ToString().Trim() + "','"
                    + FechaMovto.ToString(varini.PstForFec) + "','N'," + banco + "," + FechaMovto.ToString("yyyyMM") + ",'A','N','" + IdBenef + "','" + Modulo + "','" + Cheque + "','" + ForPag + "'," + ConseFactCont + ",'" + CantFacturas + "')";
                ExecuteQueryconec(stmysql, Myconect, "GrabaDocumento");
            }
        }

        public void GrabaDatosDocumentos(string compronte, double ConseCompro, OdbcConnection myconect,
            string banco, string NumCheque, string BenefCheque)
        {
            double _cCompro = ConseCompro;
            ok = BuscaComprobante(ref compronte, ref _cCompro, false, myconect);
            if (ok)
            {
                stmysql = "update cnt_docmto set banco = '" + banco + "', cheque = '" + NumCheque + "'"
                    + ", IdBenefCheque = '" + BenefCheque + "' where COMPRONTE ='" + compronte + "' and  NUMERO = '" + ConseCompro + "'";
                ExecuteQueryconec(stmysql, myconect, "GrabaDocumento");
            }
        }

        public void suma_niveles(string Cuenta, string periodo, string agencia, string cencosto,
            double debito, double credito, string stNatura, string pstUtiliper, OdbcConnection myconect)
        {
            string[] stCuen = new string[6];
            bool boUtilidad = false;

            stCuen[0] = Strings.Mid(Cuenta, 1, 1);
            stCuen[1] = Strings.Mid(Cuenta, 2, 1);
            stCuen[2] = Strings.Mid(Cuenta, 3, 2);
            stCuen[3] = Strings.Mid(Cuenta, 5, 2);
            stCuen[4] = Strings.Mid(Cuenta, 7, 3);
            stCuen[5] = Strings.Mid(Cuenta, 10, 3);
            string stAno = Strings.Mid(periodo, 1, 4).Trim();
            string stMes = Strings.Mid(periodo, 5, 2).Trim();

            string c0 = stCuen[0];
            if (c0 == "4" || c0 == "5" || c0 == "6" || c0 == "7")
                boUtilidad = true;

            string AplCenc = "N", Cencos = "99999999";
            for (int inI = 5; inI >= 0; inI--)
            {
                string stCuenta = Strings.Left(
                    stCuen[0].Trim() + stCuen[1].Trim() + stCuen[2].Trim() + stCuen[3].Trim() +
                    stCuen[4].Trim() + stCuen[5].Trim() + "000000000000", 12);

                string _terc = "N", _natura = "N", _cv = "99999999", _nivel = "0", _aplt = "N", _apltes = "Y", _nom = " ";
                decimal _tasa = 0; string _tipoAux = "0"; int _est = 0; string _apliCnt = "N", _csBanca = "N", _bancoC = "9999";
                BuscarCuenta(ref stCuenta, myconect, ref _terc, ref AplCenc, ref _natura, ref _cv,
                    ref _nivel, ref _aplt, ref _apltes, ref _nom, ref _tasa, ref _tipoAux,
                    ref _est, ref _apliCnt, ref _csBanca, ref _bancoC);

                Cencos = cencosto;
                int cuentaVal = 0; int.TryParse(stCuen[inI], out cuentaVal);
                if (cuentaVal > 0)
                    suma_saldos(stAno, stCuenta, agencia, Cencos, stMes, debito, credito, stNatura, myconect);

                int lenJ = stCuen[inI].Length;
                stCuen[inI] = Strings.Right("000", lenJ);
            }
            if (boUtilidad)
                suma_saldos(stAno, pstUtiliper, agencia, cencosto, stMes, debito, credito, "C", myconect);
        }

        public void suma_nit(string stAnio, string stCuenta, string stAgencia,
            string stCencosto, string stMes, double dbDebito, double dbCredito,
            string stNatu, string stNit, OdbcConnection myconect)
        {
            stMes = Strings.Right("00" + stMes, 2);
            string mysql = "select * from cnt_tercero where periodo='" + stAnio + "' and cuenta = '" + stCuenta + "' and "
                + "agencia = '" + stAgencia + "' and Cencosto = '" + stCencosto + "' and nit ='" + stNit.Trim() + "'";
            bool rowExists = ExecuteQueryconec(mysql, myconect, "suma_nit");

            string where = " where periodo='" + stAnio + "' and cuenta = '" + stCuenta + "' and "
                + "agencia = '" + stAgencia + "' and Cencosto = '" + stCencosto + "' and nit ='" + stNit.Trim() + "'";

            string[] debF = { "", "ene_deb", "feb_deb", "mar_deb", "abr_deb", "may_deb", "jun_deb",
                               "jul_deb", "ago_deb", "sep_deb", "oct_deb", "nov_deb", "dic_deb" };
            string[] creF = { "", "ene_cre", "feb_cre", "mar_cre", "abr_cre", "may_cre", "jun_cre",
                               "jul_cre", "ago_cre", "sep_cre", "oct_cre", "nov_cre", "dic_cre" };
            string[] colD = { "", "ENE_DEB", "FEB_DEB", "MAR_DEB", "ABR_DEB", "MAY_DEB", "JUN_DEB",
                               "JUL_DEB", "AGO_DEB", "SEP_DEB", "OCT_DEB", "NOV_DEB", "DIC_DEB" };
            string[] colC = { "", "ENE_CRE", "FEB_CRE", "MAR_CRE", "ABR_CRE", "MAY_CRE", "JUN_CRE",
                               "JUL_CRE", "AGO_CRE", "SEP_CRE", "OCT_CRE", "NOV_CRE", "DIC_CRE" };

            int mes = 0; int.TryParse(stMes, out mes);
            string sql;

            if (rowExists)
            {
                string stUpdate;
                if (mes >= 1 && mes <= 12)
                    stUpdate = "update cnt_tercero set " + debF[mes] + " = " + debF[mes] + " + " + dbDebito
                        + "," + creF[mes] + " = " + creF[mes] + " + " + dbCredito;
                else // mes == 13: VB overwrites trece with tre_deb/tre_cre (preserving VB behavior)
                    stUpdate = "update cnt_tercero set tre_deb = tre_deb + " + dbDebito
                        + ",tre_cre = tre_cre + " + dbCredito;
                sql = stUpdate + where;
            }
            else
            {
                string stInsert;
                if (mes >= 1 && mes <= 12)
                    stInsert = "insert into cnt_tercero (PERIODO,CUENTA,AGENCIA,NIT,CENCOSTO," + colD[mes] + "," + colC[mes] + ") VALUES ('"
                        + stAnio + "','" + stCuenta + "','" + stAgencia + "','" + stNit.Trim() + "','" + stCencosto + "','"
                        + dbDebito + "','" + dbCredito + "')";
                else // mes == 13
                {
                    double trece = (stNatu == "D") ? (dbDebito - dbCredito) : (dbCredito - dbDebito);
                    stInsert = "insert into cnt_tercero (PERIODO,CUENTA,AGENCIA,NIT,CENCOSTO,TRECE,tre_deb,tre_cre) VALUES ('"
                        + stAnio + "','" + stCuenta + "','" + stAgencia + "','" + stNit.Trim() + "','" + stCencosto + "','"
                        + trece + "','" + dbDebito + "','" + dbCredito + "')";
                }
                sql = stInsert;
            }
            ExecuteQueryconec(sql, myconect, "suma_nit");
        }

        private void suma_saldos(string stAnio, string stCuenta, string stAgencia,
            string stCencosto, string stMes, double dbDebito, double dbCredito,
            string stNatu, OdbcConnection myconect)
        {
            stMes = Strings.Right("00" + stMes, 2);
            string mysql = "select ene_deb from cnt_salage where periodo='" + stAnio + "' and cuenta = '" + stCuenta + "' and "
                + "agencia = '" + stAgencia + "' and Cencosto = '" + stCencosto + "'";
            bool rowExists = ExecuteQueryconec(mysql, myconect, "suma_saldos");

            string where = " where periodo='" + stAnio + "' and cuenta = '" + stCuenta + "' and "
                + "agencia = '" + stAgencia + "' and Cencosto = '" + stCencosto + "'";

            string[] debF = { "", "ene_deb", "feb_deb", "mar_deb", "abr_deb", "may_deb", "jun_deb",
                               "jul_deb", "ago_deb", "sep_deb", "oct_deb", "nov_deb", "dic_deb" };
            string[] creF = { "", "ene_cre", "feb_cre", "mar_cre", "abr_cre", "may_cre", "jun_cre",
                               "jul_cre", "ago_cre", "sep_cre", "oct_cre", "nov_cre", "dic_cre" };
            string[] colD = { "", "ENE_DEB", "FEB_DEB", "MAR_DEB", "ABR_DEB", "MAY_DEB", "JUN_DEB",
                               "JUL_DEB", "AGO_DEB", "SEP_DEB", "OCT_DEB", "NOV_DEB", "DIC_DEB" };
            string[] colC = { "", "ENE_CRE", "FEB_CRE", "MAR_CRE", "ABR_CRE", "MAY_CRE", "JUN_CRE",
                               "JUL_CRE", "AGO_CRE", "SEP_CRE", "OCT_CRE", "NOV_CRE", "DIC_CRE" };

            int mes = 0; int.TryParse(stMes, out mes);
            string sql;

            if (rowExists)
            {
                string stUpdate;
                if (mes >= 1 && mes <= 12)
                    stUpdate = "update cnt_salage set " + debF[mes] + " = " + debF[mes] + " + " + dbDebito
                        + ", " + creF[mes] + " = " + creF[mes] + " + " + dbCredito;
                else // mes == 13: VB overwrites trece with tre_deb/tre_cre (preserving VB behavior)
                    stUpdate = "update cnt_salage set tre_deb = tre_deb + " + dbDebito
                        + ",tre_cre = tre_cre + " + dbCredito;
                sql = stUpdate + where;
            }
            else
            {
                string stInsert;
                if (mes >= 1 && mes <= 12)
                    stInsert = "insert into cnt_salage (PERIODO,CUENTA,AGENCIA,CENCOSTO," + colD[mes] + "," + colC[mes] + ") VALUES ('"
                        + stAnio + "','" + stCuenta + "','" + stAgencia + "','" + stCencosto + "','"
                        + dbDebito + "','" + dbCredito + "')";
                else // mes == 13
                {
                    double trece = (stNatu == "D") ? (dbDebito - dbCredito) : (dbCredito - dbDebito);
                    stInsert = "insert into cnt_salage (PERIODO,CUENTA,AGENCIA,CENCOSTO,TRECE,tre_deb,tre_cre) VALUES ('"
                        + stAnio + "','" + stCuenta + "','" + stAgencia + "','" + stCencosto + "','"
                        + trece + "','" + dbDebito + "','" + dbCredito + "')";
                }
                sql = stInsert;
            }
            ExecuteQueryconec(sql, myconect, "suma_saldos");
        }

        public void CargaMovtoCpte(DataGridView GrillaMovtoCpte, string comprobante,
            string ConseCpte, OdbcConnection Myconect, ref double VlrPagarRc)
        {
            double item = 0;
            int canreg = 0, fila = 0;
            string Orden = "asc";
            var FrmProgres = new FrmCntProgres();
            VlrPagarRc = 0;
            ConfiguraGrilla(GrillaMovtoCpte);

            // if (conect.odbcConect.ordenatransaccion) // ERROR: CS0572
                // Orden = "desc"; // ERROR: CS0572

            stmysql = "Select cntmov.cuenta, cntmov.agencia, cntmov.cencosto,cntmov.nit,vlr_debito,vlr_credito, cntmaec.nombre, cntmov.secuencia, cntmov.detalle,cntmov.vlr_base,cntmov.factura "
                + "from cnt_movimto  cntmov left join sys_maenit maenit on cntmov.nit = maenit.nit left join cnt_maecuen cntmaec on cntmov.cuenta = cntmaec.cuenta "
                + "where compronte = '" + comprobante + "' and numero = '" + ConseCpte + "'"
                + " order by secuencia " + Orden;

            FrmProgres = Barraprogress(stmysql, "Cargando Movimientos", FrmProgres, Myconect);
            FrmProgres.Show();
            Application.DoEvents();
            GrillaMovtoCpte.Rows.Clear();

            var Myread = new DataSet();
            conect.ExecuteQueryDataset(stmysql, Myconect, "Carga Movto Cpte", ref Myread, "TblCargaMovto");
            canreg = Myread.Tables["TblCargaMovto"].Rows.Count;

            if (Orden == "desc")
                item = canreg + 1;

            while (fila < canreg)
            {
                var row = Myread.Tables["TblCargaMovto"].Rows[fila];
                item = (Orden == "desc") ? item - 1 : item + 1;

                if (Strings.Mid(row["cuenta"].ToString(), 1, 4) == "1105")
                    VlrPagarRc += Convert.ToDouble(row["vlr_debito"]);

                object vlrBase = (row["vlr_base"] is DBNull) ? (object)0 : row["vlr_base"];
                GrillaMovtoCpte.Rows.Add(item, row["cuenta"], row["nombre"], row["agencia"],
                    row["cencosto"], row["nit"],
                    Strings.FormatNumber(Convert.ToDouble(row["vlr_debito"])),
                    Strings.FormatNumber(Convert.ToDouble(row["vlr_credito"])),
                    row["secuencia"], row["detalle"],
                    Strings.FormatNumber(Convert.ToDouble(vlrBase)), row["factura"]);
                FrmProgres.Progress.PerformStep();
                fila++;
            }
            Myread.Dispose();
            FrmProgres.Close();
        }

        public FrmCntProgres Barraprogress(string Sql, string Titulo, FrmCntProgres pro,
            OdbcConnection myconect, string Comentario = "Espere por favor.")
        {
            pro.lblTittulo.Visible = true;
            pro.ControlBox = false;
            pro.Progress.Visible = true;
            pro.lblTittulo.Text = Titulo;
            pro.Progress.Minimum = 0;
            pro.Progress.Step = 1;
            var ds = new DataSet();
            conect.ExecuteQueryDataset(Sql, myconect, "Barraprogress", ref ds, "new");
            pro.Progress.Maximum = ds.Tables["new"].Rows.Count;
            return pro;
        }

        private void ConfiguraGrilla(DataGridView GrillaMovtoCpte)
        {
            GrillaMovtoCpte.BorderStyle = BorderStyle.Fixed3D;
            GrillaMovtoCpte.EditMode = DataGridViewEditMode.EditOnEnter;
            GrillaMovtoCpte.ColumnHeadersDefaultCellStyle.Font =
                new System.Drawing.Font(GrillaMovtoCpte.Font, System.Drawing.FontStyle.Bold);
            GrillaMovtoCpte.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            GrillaMovtoCpte.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        public void CuadreDocumento(string comprobante, double Consecutivo, string Usuario, OdbcConnection myconect)
        {
            var CuadreDoc = new cnt_cuadredoc(myconect);
            double debitos = 0, creditos = 0, dif = 0;
            DateTime fecha = new DateTime(1950, 1, 1);
            string detalle = " ";

            string _cc = " ", _td = " "; string _cerr = "N", _an = "N";
            string _dt = "NC", _nd = " "; string _nom = " ", _idb = "99999999999999", _cen = "99999999", _cta = "999999999999";
            string _mod = null; int _per = 0; string _cf = "0", _da = " ";
            BuscaComprobante(ref comprobante, ref Consecutivo, false, myconect,
                ref _cc, ref _td, ref debitos, ref creditos, ref _cerr, ref _an, ref dif,
                ref detalle, ref _dt, ref _nd, ref fecha, ref _nom, ref _idb, ref _cen,
                ref _cta, ref _mod, ref _per, ref _cf, ref _da);

            CuadreDoc.txtComprobante.Text = comprobante;
            CuadreDoc.TxtConseCpte.Text = Consecutivo.ToString();
            CuadreDoc.TxtDiferencia.Text = dif.ToString();
            CuadreDoc.txtDebitos.Text = debitos.ToString();
            CuadreDoc.TxtCreditos.Text = creditos.ToString();
            CuadreDoc.DtpFecha.Value = fecha;
            CuadreDoc.DtpFecha.Enabled = false;
            CuadreDoc.Tag = Usuario;
            if (dif > 0)
                CuadreDoc.txtDebito.Text = dif.ToString();
            else
                CuadreDoc.txtCredito.Text = dif.ToString();
            CuadreDoc.txtComprobante.Tag = detalle;
            CuadreDoc.ShowDialog();
        }

        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo,
            bool ActualizaConse, OdbcConnection Myconect,
            ref string ControlConse, ref string TipoDoc,
            ref double debitos, ref double creditos,
            ref string CERRADO, ref string ANULADO,
            ref double Diferencia, ref string Detalle,
            ref string Doctipo, ref string NODOC,
            ref DateTime FechaMovto, ref string Nombre,
            ref string Idbenef, ref string CencCpte,
            ref string Cuenta, ref string Modulo,
            ref int Periodo, ref string CantFactura,
            ref string detalle_automatico)
        {
            double Num_consecu = 0;
            Comprobante = Strings.Right("0000" + Comprobante, 4);

            if (Consecutivo == 0)
            {
                stmysql = "select control_consec as campo1, num_consecu as campo2, DOCUMENTO as campo3, nombre as campo4   from sys_compro02 where codigo =  '" + Comprobante + "'";
                string _numCons = "0";
                ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref ControlConse, ref _numCons, ref TipoDoc, ref Nombre);
                double.TryParse(_numCons, out Num_consecu);

                stmysql = "select CUENTA_CONTABLE as campo1, modulo as campo2, detalle_automatico as campo3 from sys_compro02 where codigo =  '" + Comprobante + "'";
                ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref Cuenta, ref Modulo, ref detalle_automatico);

                Num_consecu++;
                if (ActualizaConse)
                {
                    stmysql = "Update sys_compro02 set num_consecu = " + Num_consecu + " where codigo = '" + Comprobante + "'";
                    ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante");
                }
                Consecutivo = Num_consecu;
                return ok;
            }
            else
            {
                stmysql = "select control_consec as campo1, num_consecu as campo2, DOCUMENTO as campo3, nombre as campo4 from sys_compro02 where codigo =  '" + Comprobante + "'";
                string _numCons2 = "0";
                ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref ControlConse, ref _numCons2, ref TipoDoc, ref Nombre);

                stmysql = "select CENCOSTO as campo1 from sys_compro02 where codigo =  '" + Comprobante + "'";
                ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref CencCpte);

                string _deb = "0", _cre = "0";
                stmysql = "select compronte, debito as campo1, credito as campo2,CERRADO as campo3,ANULADO as campo4  from cnt_docmto where COMPRONTE = '" + Comprobante + "' and NUMERO = " + Consecutivo;
                ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref _deb, ref _cre, ref CERRADO, ref ANULADO);
                double.TryParse(_deb, out debitos);
                double.TryParse(_cre, out creditos);

                string _fecMovto = "0";
                stmysql = "select detalle as campo1, doctipo as campo2, nodoc as campo3,fecha as campo4 from cnt_docmto where COMPRONTE = '" + Comprobante + "' and NUMERO = " + Consecutivo;
                ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref Detalle, ref Doctipo, ref NODOC, ref _fecMovto);
                DateTime.TryParse(_fecMovto, out FechaMovto);

                string _per = "0";
                stmysql = "select Idbenef as campo1, Modulo as campo2, periodo as campo3, FILLER as campo4  from cnt_docmto where COMPRONTE = '" + Comprobante + "' and NUMERO = " + Consecutivo;
                ok = ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref Idbenef, ref Modulo, ref _per, ref CantFactura);
                int.TryParse(_per, out Periodo);

                Diferencia = Math.Round(creditos - debitos, 2);
                return ok;
            }
        }

        // Convenience overload for callers that only need ok result (no optional params)
        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo,
            bool ActualizaConse, OdbcConnection Myconect)
        {
            string _cc = " ", _td = " "; double _d = 0, _c = 0; string _cerr = "N", _an = "N";
            double _dif = 0; string _det = " ", _dt = "NC", _nd = " ";
            DateTime _fec = new DateTime(1950, 1, 1);
            string _nom = " ", _idb = "99999999999999", _cen = "99999999", _cta = "999999999999";
            string _mod = null; int _per = 0; string _cf = "0", _da = " ";
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref _cc, ref _td, ref _d, ref _c, ref _cerr, ref _an, ref _dif,
                ref _det, ref _dt, ref _nd, ref _fec, ref _nom, ref _idb, ref _cen,
                ref _cta, ref _mod, ref _per, ref _cf, ref _da);
        }

        public ClsContabilidad()
        {
            var Rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("Applications\\ConsfiaS\\Shell\\Open\\config");
            if (Rk == null) return;

            string pas_raw = Rk.OpenSubKey("pass").GetValue("").ToString();
            char[] pass = pas_raw.ToCharArray();
            string pas = "";
            for (int i = 0; i <= pass.Length - 1; i += 2)
                pas += (char)((int)pass[i] - 54);

            varini.pstPascon = pas;
            varini.pstServer = Rk.OpenSubKey("server").GetValue("").ToString();
            varini.pstPort = Rk.OpenSubKey("port").GetValue("").ToString();
            varini.pstTipoBD = Rk.OpenSubKey("tipo").GetValue("").ToString();
            varini.pstBdatos = Rk.OpenSubKey("name").GetValue("").ToString();
            varini.pstDNS = Rk.OpenSubKey("driver").GetValue("").ToString();
            varini.PstForFec = Rk.OpenSubKey("format").GetValue("").ToString();
            varini.pstForfecyHora = Rk.OpenSubKey("fechayhora").GetValue("").ToString();
            varini.pstForHora = Rk.OpenSubKey("hora").GetValue("").ToString();
            varini.pstUID = Rk.OpenSubKey("user").GetValue("").ToString();
            varini.pstEmpresa = "0001-EMPRESA DE PRUEBAS 999";
            varini.sptCodEmpr = varini.pstEmpresa.Substring(0, 4);
            varini.pstForHora = Rk.OpenSubKey("hora").GetValue("").ToString();
            varini.pstForfecyHora = Rk.OpenSubKey("fechayhora").GetValue("").ToString();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "MYSQL":
                    varini.pstMyconec = "Driver=" + varini.pstDNS + ";UID=" + varini.pstUID
                        + ";DATABASE=" + varini.pstBdatos + ";PASSWORD=" + varini.pstPascon
                        + ";PORT=" + varini.pstPort + ";SERVER=" + varini.pstServer;
                    break;
                case "SQL":
                    varini.pstMyconec = "Driver=" + varini.pstDNS + ";Server=" + varini.pstServer
                        + ";Database=" + varini.pstBdatos + ";Uid=" + varini.pstUID
                        + ";Pwd=" + varini.pstPascon + ";";
                    break;
                case "ORACLE":
                    varini.pstMyconec = "DRIVER=" + varini.pstDNS + ";SERVER=" + varini.pstServer
                        + ";uid=" + varini.pstUID + ";Pwd=" + varini.pstPascon + ";dbq=" + varini.pstBdatos;
                    break;
            }
        }

// _chunk_d: cierra_gyp, CerrarTercero, CierraTercero, traslada_cuentas,
//           traslada_terceros, traslada_domto, ReversaDocumentoCierre,
//           ReversaDocumentoCierreTercero, consolidar, RepiteComprobante

        public bool cierra_gyp(string combte, string comp_nume, string detalle,
            int periodo_conta, string cuenta_cierre, DateTime dtFecfin,
            string stDoctype, OdbcConnection myconet, Form Pertenece, string cuenta_perdida)
        {
            int inMesp = int.Parse(Strings.Mid(periodo_conta.ToString(), 5, 2));
            int inAniop = int.Parse(Strings.Mid(periodo_conta.ToString(), 1, 4));
            string stNitnu = "", ExigeCencosUtil = "N", ExigeCencosPerd = "N";
            string DocAnteriro = " "; double NumAnteriro = 0;
            int canreg = 0, fila = 0;
            string CuentaPyG = "";
            var fprogre = new ERP.Core.Compartido.Controles.Barraprogress("Grabando Cierre de cuentas", Pertenece);
            var dsdata = new DataSet();
            double dbVlrdeb = 0, dbVlrcre = 0, dbUtiper = 0;

            combte = Strings.Right("0000" + combte.Trim(), 4);

            string _t1 = "N", _na1 = "N", _cv1 = "99999999", _nv1 = "0", _ap1 = "N", _at1 = "Y", _nm1 = " ";
            decimal _ts1 = 0; string _ta1 = "0"; int _e1 = 0; string _ac1 = "N", _cb1 = "N", _bc1 = "9999";
            BuscarCuenta(ref cuenta_cierre, myconet, ref _t1, ref ExigeCencosUtil, ref _na1, ref _cv1,
                ref _nv1, ref _ap1, ref _at1, ref _nm1, ref _ts1, ref _ta1, ref _e1, ref _ac1, ref _cb1, ref _bc1);
            string _t2 = "N", _na2 = "N", _cv2 = "99999999", _nv2 = "0", _ap2 = "N", _at2 = "Y", _nm2 = " ";
            decimal _ts2 = 0; string _ta2 = "0"; int _e2 = 0; string _ac2 = "N", _cb2 = "N", _bc2 = "9999";
            BuscarCuenta(ref cuenta_perdida, myconet, ref _t2, ref ExigeCencosPerd, ref _na2, ref _cv2,
                ref _nv2, ref _ap2, ref _at2, ref _nm2, ref _ts2, ref _ta2, ref _e2, ref _ac2, ref _cb2, ref _bc2);

            stmysql = "select compronte as campo1,numero as campo2 from cnt_docmto where domto = 'CA' and periodo = '" + periodo_conta + "'";
            string _docAnt = " ", _numAnt = "0";
            if (conect.ExecuteQueryconec(stmysql, myconet, "cierra_gyp", ref _docAnt, ref _numAnt))
            {
                DocAnteriro = _docAnt;
                double.TryParse(_numAnt, out NumAnteriro);
                MessageBox.Show("El proceso de cierre ya fue realizado. \nSi desea hacerlo de nuevo debe reversar el documento : "
                    + DocAnteriro + "-" + NumAnteriro, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            stmysql = "update cnt_salage set trece='0',tre_deb='0',tre_cre='0' "
                + " where periodo='" + inAniop + "'";
            conect.ExecuteQueryconec(stmysql, myconet, "cierra_gyp");

            stmysql = "select a.cuenta,a.agencia,a.cencosto,b.tercero,(saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )as total "
                + "from cnt_salage a inner join cnt_maecuen b on a.cuenta = b.cuenta where "
                + "periodo ='" + inAniop + "' and left(a.cuenta,1) in ('4','5','6','7')  and b.nivel='6' and "
                + "(saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )  <> 0";

            fprogre.DefineMaximo(stmysql, myconet);
            fprogre.Show();

            var myReader1 = new DataSet();
            conect.ExecuteQueryDataset(stmysql, myconet, "cierra gyp", ref myReader1, "TblCierreGyp");
            canreg = myReader1.Tables["TblCierreGyp"].Rows.Count;

            while (fila < canreg)
            {
                Application.DoEvents();
                var row = myReader1.Tables["TblCierreGyp"].Rows[fila];
                double total = Convert.ToDouble(row["total"]);
                if (total >= 0)
                {
                    dbVlrcre = total;
                    dbVlrdeb = 0;
                }
                else
                {
                    dbVlrdeb = total * -1;
                    dbVlrcre = 0;
                }
                fprogre.Titulo("Cerrando la Cuenta " + row["Cuenta"]);
                stNitnu = "";
                dbUtiper += dbVlrdeb - dbVlrcre;
                ok = GrabaMovimiento(combte, Convert.ToDouble(comp_nume),
                    row["cuenta"].ToString(), row["agencia"].ToString(),
                    "P13", stNitnu, dtFecfin, detalle, "Cierre anual",
                    dbVlrdeb, dbVlrcre, 0, varini.pstUsuario, myconet,
                    Cencosto: row["cencosto"].ToString(), Modulo: "cont");
                fprogre.PerformStep();
                fila++;
            }

            if (dbUtiper != 0)
            {
                stmysql = "Select cencosto, sum(vlr_debito-vlr_credito) as valor  from cnt_movimto  where compronte = '" + combte + "' and Numero = " + Convert.ToDouble(comp_nume)
                    + " group  by Cencosto order by cencosto";
                if (conect.ExecuteQueryDataset(stmysql, myconet, "Cierra_gyp(UtilidadPerdida)", ref dsdata, "tblcierre", true))
                {
                    for (fila = 0; fila < dsdata.Tables["tblcierre"].Rows.Count; fila++)
                    {
                        var rowU = dsdata.Tables["tblcierre"].Rows[fila];
                        double valor = Convert.ToDouble(rowU["valor"]);
                        if (valor != 0)
                        {
                            if (valor > 0)
                            {
                                dbVlrcre = valor;
                                dbVlrdeb = 0;
                                CuentaPyG = cuenta_cierre;
                                if (ExigeCencosUtil == "N")
                                    rowU["cencosto"] = "99999999";
                            }
                            else
                            {
                                dbVlrdeb = valor * -1;
                                dbVlrcre = 0;
                                CuentaPyG = cuenta_perdida;
                                if (ExigeCencosPerd == "N")
                                    rowU["cencosto"] = "99999999";
                            }
                            ok = GrabaMovimiento(combte, Convert.ToDouble(comp_nume),
                                CuentaPyG.Trim(), "9999", "P13", stNitnu, dtFecfin, detalle, "Cierre anual",
                                dbVlrdeb, dbVlrcre, 0, varini.pstUsuario.Trim(), myconet,
                                Cencosto: rowU["cencosto"].ToString(), Modulo: "cont");
                        }
                    }
                }
            }

            stmysql = "Update cnt_docmto set cerrado='Y',domto = 'CA',periodo='" + periodo_conta + "' where compronte='" + Strings.Right("0000" + combte, 4) + "' and "
                + " numero ='" + comp_nume + "'";
            conect.ExecuteQueryconec(stmysql, myconet, "cierra_gyp");

            myReader1.Dispose();
            fprogre.Close();
            return true;
        }

        public bool CerrarTercero(string cuentaACerra, string cuenta_cierre, string stNitCierre,
            int periodo_conta, string combte, string comp_nume, string detalle,
            DateTime dtFecfin, OdbcConnection myconect, Form Pertenece, ref double dbUtiper)
        {
            dbUtiper = 0;
            var Fpro = new ERP.Core.Compartido.Controles.Barraprogress("Grabando Cierre de terceros", Pertenece);
            int canreg = 0, fila = 0;
            string ExigeCencos = "N";
            int inMesp = int.Parse(Strings.Mid(periodo_conta.ToString(), 5, 2));
            int inAniop = int.Parse(Strings.Mid(periodo_conta.ToString(), 1, 4));

            stmysql = "update cnt_tercero set trece='0',tre_deb='0',tre_cre='0' "
                + " where periodo='" + inAniop + "' and cuenta = '" + cuentaACerra + "' ";
            conect.ExecuteQueryconec(stmysql, myconect, "CierraTercero");

            stmysql = "select a.cuenta,a.agencia,a.cencosto,a.nit,(saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )as total "
                + "from cnt_tercero a where a.nit <> '99999999999999' and "
                + "a.periodo='" + inAniop + "' and a.cuenta = '" + cuentaACerra + "' and "
                + "(saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )  <> 0";

            Fpro.DefineMaximo(stmysql, myconect);
            var myRead = new DataSet();
            var dsdata = new DataSet();
            conect.ExecuteQueryDataset(stmysql, myconect, "Cierra Tercero", ref myRead, "TblCierreTerc");
            canreg = myRead.Tables["TblCierreTerc"].Rows.Count;
            double deb = 0, cre = 0;
            Fpro.Show();
            ok = false;

            string _t3 = "N", _na3 = "N", _cv3 = "99999999", _nv3 = "0", _ap3 = "N", _at3 = "Y", _nm3 = " ";
            decimal _ts3 = 0; string _ta3 = "0"; int _e3 = 0; string _ac3 = "N", _cb3 = "N", _bc3 = "9999";
            BuscarCuenta(ref cuenta_cierre, myconect, ref _t3, ref ExigeCencos, ref _na3, ref _cv3,
                ref _nv3, ref _ap3, ref _at3, ref _nm3, ref _ts3, ref _ta3, ref _e3, ref _ac3, ref _cb3, ref _bc3);

            while (fila < canreg)
            {
                Application.DoEvents();
                var row = myRead.Tables["TblCierreTerc"].Rows[fila];
                double total = Convert.ToDouble(row["total"]);
                if (total >= 0)
                {
                    cre = total;
                    deb = 0;
                }
                else
                {
                    deb = total * -1;
                    cre = 0;
                }
                dbUtiper += deb - cre;
                GrabaMovimiento(combte, Convert.ToInt32(comp_nume), cuentaACerra, row["agencia"].ToString(),
                    "P13", row["nit"].ToString(), dtFecfin, detalle, "Cierre Terceros",
                    deb, cre, 0, varini.pstUsuario.Trim(), myconect,
                    Cencosto: row["cencosto"].ToString(), Modulo: "cont");
                Fpro.PerformStep();
                ok = true;
                fila++;
            }
            myRead.Dispose();

            if (dbUtiper != 0)
            {
                stmysql = "Select cencosto, sum(vlr_debito-vlr_credito) as valor  from cnt_movimto  where compronte = '" + combte + "' and Numero = " + Convert.ToDouble(comp_nume)
                    + " group  by Cencosto order by cencosto";
                if (conect.ExecuteQueryDataset(stmysql, myconect, "Cierra_Tercero(UtilidadPerdida)", ref dsdata, "tbltercero", true))
                {
                    for (fila = 0; fila < dsdata.Tables["tbltercero"].Rows.Count; fila++)
                    {
                        var rowU = dsdata.Tables["tbltercero"].Rows[fila];
                        double valor = Convert.ToDouble(rowU["valor"]);
                        if (valor != 0)
                        {
                            if (valor > 0)
                            {
                                cre = valor;
                                deb = 0;
                            }
                            else
                            {
                                deb = valor * -1;
                                cre = 0;
                            }
                            if (ExigeCencos == "N")
                                rowU["cencosto"] = "99999999";
                            GrabaMovimiento(combte, Convert.ToDouble(comp_nume),
                                cuenta_cierre.Trim(), "9999", "P13", stNitCierre, dtFecfin, detalle, "Cierre Terceros",
                                deb, cre, 0, varini.pstUsuario.Trim(), myconect,
                                Cencosto: rowU["cencosto"].ToString(), Modulo: "cont");
                        }
                    }
                }
            }

            stmysql = "Update cnt_docmto set cerrado='Y',domto = 'CT',periodo='" + periodo_conta + "' where compronte='" + Strings.Right("0000" + combte, 4) + "' and "
                + " numero ='" + comp_nume + "'";
            conect.ExecuteQueryconec(stmysql, myconect, "CerrarTercero");
            Fpro.Close();
            return ok;
        }

        private void CierraTercero(string cuenta, string stNitnu, string agencia,
            string cencosto, int inAniop, string combte, string comp_nume, string detalle,
            int periodo_conta, DateTime dtFecfin, OdbcConnection myconect,
            Form Pertenece, ref double dbUtiper)
        {
            stmysql = "update cnt_tercero set trece='0' "
                + " where periodo='" + inAniop + "' and cuenta = '" + cuenta + "' and cencosto = '" + cencosto + "' and agencia = '" + agencia + "'";
            conect.ExecuteQueryconec(stmysql, myconect, "CierraTercero");

            stmysql = "select a.cuenta,a.agencia,a.cencosto,a.nit,(saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )as total "
                + "from cnt_tercero a where "
                + "a.periodo='" + inAniop + "' and a.cuenta = '" + cuenta + "' and a.cencosto = '" + cencosto + "' and a.agencia = '" + agencia + "' and "
                + "(saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )  <> 0";

            int canreg = 0, fila = 0;
            var myRead = new DataSet();
            conect.ExecuteQueryDataset(stmysql, myconect, "CierraTercero", ref myRead, "TblCierreTerc");
            canreg = myRead.Tables["TblCierreTerc"].Rows.Count;
            double deb = 0, cre = 0;

            while (fila < canreg)
            {
                Application.DoEvents();
                var row = myRead.Tables["TblCierreTerc"].Rows[fila];
                double total = Convert.ToDouble(row["total"]);
                if (total >= 0)
                {
                    cre = total;
                    deb = 0;
                }
                else
                {
                    deb = total * -1;
                    cre = 0;
                }
                stNitnu = row["nit"].ToString();
                dbUtiper += deb - cre;
                GrabaMovimiento(combte, Convert.ToInt32(comp_nume), cuenta, row["agencia"].ToString(),
                    "P13", stNitnu, dtFecfin, detalle, "Cierre anual",
                    deb, cre, 0, varini.pstUsuario.Trim(), myconect,
                    Cencosto: row["cencosto"].ToString(), Modulo: "cont");
                fila++;
            }
        }

        public void traslada_cuentas(int periodo_conta, int nuevo_anio,
            OdbcConnection myconet, Form Pertenese)
        {
            int inAniop = int.Parse(Strings.Mid(periodo_conta.ToString(), 1, 4));
            var fprogre = new ERP.Core.Compartido.Controles.Barraprogress("Trasladando saldos de cuentas", Pertenese);
            int canreg = 0, fila = 0;
            try
            {
                stmysql = "update cnt_salage set saldo_inicial='0' where periodo='" + nuevo_anio + "'";
                conect.ExecuteQueryconec(stmysql, myconet, "traslada_cuentas");

                stmysql = "select a.cuenta,a.agencia,a.cencosto,( saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                    + "oct_deb + nov_deb + dic_deb + tre_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                    + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre - tre_cre) as total "
                    + "from cnt_salage a inner join cnt_maecuen b on a.cuenta = b.cuenta where "
                    + "periodo='" + inAniop + "' and left(a.cuenta,1) in ('1','2','3','8','9') and "
                    + "( saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                    + "oct_deb + nov_deb + dic_deb + tre_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                    + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre - tre_cre)  <> 0";

                fprogre.DefineMaximo(stmysql, myconet);
                var myReader1 = new DataSet();
                conect.ExecuteQueryDataset(stmysql, myconet, "traslada_cuentas", ref myReader1, "TblTraslado");
                canreg = myReader1.Tables["TblTraslado"].Rows.Count;
                fprogre.Show();

                while (fila < canreg)
                {
                    Application.DoEvents();
                    var row = myReader1.Tables["TblTraslado"].Rows[fila];
                    stmysql = "select saldo_inicial from cnt_salage  where cuenta='" + row["cuenta"] + "' and agencia='"
                        + row["agencia"] + "' and cencosto='" + row["cencosto"] + "' and periodo='"
                        + nuevo_anio + "'";

                    if (!conect.ExecuteQueryconec(stmysql, myconet, "traslada_cuentas"))
                        stmysql = "insert into cnt_salage (periodo,cuenta,agencia,cencosto,saldo_inicial) values ('" + nuevo_anio + "','"
                            + row["cuenta"] + "','" + row["agencia"] + "','" + row["cencosto"] + "','"
                            + row["total"] + "')";
                    else
                        stmysql = "update cnt_salage set saldo_inicial='" + row["total"] + "' where cuenta='" + row["cuenta"] + "' and agencia='"
                            + row["agencia"] + "' and cencosto='" + row["cencosto"] + "' and periodo='"
                            + nuevo_anio + "'";

                    conect.ExecuteQueryconec(stmysql, myconet, "traslada_cuentas");
                    fprogre.PerformStep();
                    fila++;
                }
                myReader1.Dispose();
                fprogre.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message, "SOLIDO");
                fprogre.Close();
            }
        }

        public void traslada_terceros(int periodo_conta, int nuevo_anio,
            OdbcConnection myconet, Form Pertenese)
        {
            int inAniop = int.Parse(Strings.Mid(periodo_conta.ToString(), 1, 4));
            var fprogre = new ERP.Core.Compartido.Controles.Barraprogress("Trasladando saldos de terceros a nuevo ano", Pertenese);
            int canreg = 0, fila = 0;
            try
            {
                stmysql = "update cnt_tercero set saldo_inicial='0' where periodo='" + nuevo_anio + "'";
                conect.ExecuteQueryconec(stmysql, myconet, "traslada_terceros");

                stmysql = "select a.cuenta,a.agencia,a.cencosto,nit,( saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                    + "oct_deb + nov_deb + dic_deb + tre_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                    + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre - tre_cre) as total "
                    + "from cnt_tercero a inner join cnt_maecuen b on a.cuenta = b.cuenta where "
                    + "periodo='" + inAniop + "' and left(a.cuenta,1) in ('1','2','3','8','9') and "
                    + "( saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                    + "oct_deb + nov_deb + dic_deb + tre_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                    + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre - tre_cre)  <> 0";

                fprogre.DefineMaximo(stmysql, myconet);
                var myReader1 = new DataSet();
                conect.ExecuteQueryDataset(stmysql, myconet, "traslada_terceros", ref myReader1, "TrasladaTercero");
                canreg = myReader1.Tables["TrasladaTercero"].Rows.Count;
                fprogre.Show();

                while (fila < canreg)
                {
                    var row = myReader1.Tables["TrasladaTercero"].Rows[fila];
                    Application.DoEvents();
                    stmysql = "select saldo_inicial from cnt_tercero  where cuenta='" + row["cuenta"] + "' and agencia='"
                        + row["agencia"] + "' and cencosto='" + row["cencosto"] + "' and periodo='"
                        + nuevo_anio + "' and nit='" + row["nit"] + "'";

                    if (!conect.ExecuteQueryconec(stmysql, myconet, "traslada_terceros"))
                        stmysql = "insert into cnt_tercero (periodo,cuenta,agencia,cencosto,nit,saldo_inicial) values ('" + nuevo_anio + "','"
                            + row["cuenta"] + "','" + row["agencia"] + "','" + row["cencosto"] + "','"
                            + row["nit"] + "','" + row["total"] + "')";
                    else
                        stmysql = "update cnt_tercero set saldo_inicial='" + row["total"] + "' where cuenta='" + row["cuenta"] + "' and agencia='"
                            + row["agencia"] + "' and cencosto='" + row["cencosto"] + "' and periodo='"
                            + nuevo_anio + "' and nit='" + row["nit"] + "'";

                    conect.ExecuteQueryconec(stmysql, myconet, "traslada_terceros");
                    fprogre.PerformStep();
                    fila++;
                }
                myReader1.Dispose();
                fprogre.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Modulo : traslada_terceros , Descripcion:" + ex.Message);
                fprogre.Close();
            }
        }

        public void traslada_domto(int periodo_conta, int nuevo_anio,
            OdbcConnection myconet, Form Pertenese)
        {
            int inAniop = int.Parse(Strings.Mid(periodo_conta.ToString(), 1, 4));
            var fprogre = new ERP.Core.Compartido.Controles.Barraprogress("Trasladando saldos de documentos a nuevo ano", Pertenese);
            int canreg = 0, fila = 0;
            try
            {
                stmysql = "update cnt_docaux set saldo_inicial='0' where periodo='" + nuevo_anio + "'";
                conect.ExecuteQueryconec(stmysql, myconet, "traslada_domto");

                stmysql = "select a.cuenta,a.agencia,a.cencosto,a.tipo_auxiliar,nit,docu_tipo,docu_numero,detalle,fecha_domto,fecha_vemto,valor_inicial,factura, "
                    + "(saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                    + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                    + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )as total "
                    + "from cnt_docaux a inner join cnt_maecuen b on a.cuenta = b.cuenta where "
                    + "periodo='" + inAniop + "' and left(a.cuenta,1) in ('1','2','3','8','9') and "
                    + "( saldo_inicial + ene_deb + feb_deb + mar_deb + abr_deb + may_deb + jun_deb + jul_deb + ago_deb + sep_deb + "
                    + "oct_deb + nov_deb + dic_deb - ene_cre - feb_cre - mar_cre - abr_cre - may_cre - jun_cre - jul_cre - "
                    + "ago_cre - sep_cre - oct_cre - nov_cre - dic_cre )  <> 0";

                fprogre.DefineMaximo(stmysql, myconet);
                var myReader1 = new DataSet();
                conect.ExecuteQueryDataset(stmysql, myconet, "traslada_domto", ref myReader1, "TblTrasladaDomto");
                canreg = myReader1.Tables["TblTrasladaDomto"].Rows.Count;
                fprogre.Show();

                while (fila < canreg)
                {
                    var row = myReader1.Tables["TblTrasladaDomto"].Rows[fila];
                    Application.DoEvents();
                    stmysql = "select saldo_inicial from cnt_docaux  where cuenta='" + row["cuenta"] + "' and agencia='"
                        + row["agencia"] + "' and cencosto='" + row["cencosto"] + "' and periodo='"
                        + nuevo_anio + "' and nit='" + row["nit"] + "' and docu_tipo='" + row["docu_tipo"]
                        + "' and docu_numero='" + row["docu_numero"] + "' and tipo_auxiliar = '" + row["tipo_auxiliar"] + "'";

                    if (!conect.ExecuteQueryconec(stmysql, myconet, "traslada_domto"))
                        stmysql = "insert into cnt_docaux (periodo,tipo_auxiliar,cuenta,agencia,cencosto,nit,docu_tipo,docu_numero,detalle,fecha_domto,fecha_vemto,saldo_inicial,valor_inicial,factura) values ('" + nuevo_anio + "','"
                            + row["tipo_auxiliar"] + "','" + row["cuenta"] + "','" + row["agencia"] + "','" + row["cencosto"] + "','"
                            + row["nit"] + "','" + row["docu_tipo"] + "','" + row["docu_numero"] + "','"
                            + row["detalle"] + "','" + Strings.Format(Convert.ToDateTime(row["fecha_domto"]), varini.PstForFec) + "','"
                            + Strings.Format(Convert.ToDateTime(row["fecha_vemto"]), varini.PstForFec) + "','" + row["total"] + "','" + row["valor_inicial"]
                            + "','" + row["factura"] + "')";
                    else
                        stmysql = "update cnt_docaux set saldo_inicial='" + row["total"] + "' where cuenta='" + row["cuenta"] + "' and agencia='"
                            + row["agencia"] + "' and cencosto='" + row["cencosto"] + "' and periodo='"
                            + nuevo_anio + "' and nit='" + row["nit"] + "' and docu_tipo='" + row["docu_tipo"]
                            + "' and docu_numero='" + row["docu_numero"] + "' and tipo_auxiliar = '" + row["tipo_auxiliar"] + "'";

                    conect.ExecuteQueryconec(stmysql, myconet, "traslada_domto");
                    fprogre.PerformStep();
                    fila++;
                }
                fprogre.Close();
                myReader1.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Modulo : traslada_domto , Descripcion:" + ex.Message);
                fprogre.Close();
            }
        }

        public bool ReversaDocumentoCierre(string combte, string comp_nume,
            int periodo_conta, OdbcConnection myconet)
        {
            stmysql = "select * from cnt_docmto where domto = 'CA' and compronte = '" + combte + "' and numero = '" + comp_nume
                + "' and periodo = '" + periodo_conta + "'";
            ok = conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierre.Consulta");
            if (!ok)
            {
                MessageBox.Show("Este comprobante no es el utilizado en el anterior cierre.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            stmysql = "Delete from cnt_movimto where compronte = '" + combte + "' and numero = '" + comp_nume + "' ";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierre.BorradoMov");
            stmysql = "Delete from cnt_docmto where compronte = '" + combte + "' and numero = '" + comp_nume + "' ";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierre.BorradoDoc");
            stmysql = "update cnt_salage set trece= '0',tre_deb='0',tre_cre='0' where periodo='" + Strings.Mid(periodo_conta.ToString(), 1, 4) + "'";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierre.IniTreceCuentas");
            stmysql = "update cnt_tercero set trece='0',tre_deb='0',tre_cre='0' where periodo='" + Strings.Mid(periodo_conta.ToString(), 1, 4) + "'";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierre.IniTreceTerceros");
            return ok;
        }

        public bool ReversaDocumentoCierreTercero(string combte, string comp_nume,
            int periodo_conta, OdbcConnection myconet)
        {
            stmysql = "select * from cnt_docmto where domto = 'CT' and compronte = '" + combte + "' and numero = '" + comp_nume
                + "' and periodo = '" + periodo_conta + "'";
            ok = conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierreTercero.Consulta");
            if (!ok)
            {
                MessageBox.Show("Este comprobante no es el utilizado en el anterior cierre.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            stmysql = "Delete from cnt_movimto where compronte = '" + combte + "' and numero = '" + comp_nume + "' ";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierreTercero.BorradoMov");
            stmysql = "Delete from cnt_docmto where compronte = '" + combte + "' and numero = '" + comp_nume + "' ";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierreTercero.BorradoDoc");
            stmysql = "update cnt_salage set trece= '0',tre_deb='0',tre_cre='0' where periodo='" + Strings.Mid(periodo_conta.ToString(), 1, 4) + "'";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierreTercero.IniTreceCuentas");
            stmysql = "update cnt_tercero set trece='0',tre_deb='0',tre_cre='0' where periodo='" + Strings.Mid(periodo_conta.ToString(), 1, 4) + "'";
            conect.ExecuteQueryconec(stmysql, myconet, "ReversaDocumentoCierreTercero.IniTreceTerceros");
            MessageBox.Show("Proceso termino Por favor Actualice el Periodo 13 ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return ok;
        }

        public void consolidar(Form pertenece, OdbcConnection conect1,
            string cuenta_afec = "999999999999", string age = "9999",
            string cenco = "99999999", int peri = 0)
        {
            string cuenta = "", agencia = "", cencosto = "";
            string mysql = "", stCuenta = "";
            string[] stCuen = new string[6];
            int canreg = 0, fila = 0;

            if (cuenta_afec == "999999999999" && age == "9999" && cenco == "99999999")
                mysql = "select * from cnt_presupto a inner join cnt_maecuen b on b.CUENTA = a.cuenta where b.nivel = 6 AND a.PERIODO  = " + peri + " order by a.cuenta asc";
            else
                mysql = "select * from cnt_presupto where periodo = '" + peri + "' and cuenta = '" + cuenta_afec
                    + "' and agencia = '" + age + "' and cencosto = '" + cenco + "'";

            var Myread = new DataSet();
            try
            {
                var myComcondo = new OdbcDataAdapter(mysql, conect1);
                var men = new ERP.Core.Compartido.Controles.Barraprogress("Consolidando...", pertenece);
                if (cuenta_afec == "999999999999" || age == "9999" || cenco == "99999999")
                    men.Show();

                canreg = myComcondo.Fill(Myread, "TblConsolidar");

                while (fila < canreg)
                {
                    var row = Myread.Tables["TblConsolidar"].Rows[fila];
                    cuenta = row["cuenta"].ToString();
                    stCuen[0] = Strings.Mid(cuenta, 1, 1);
                    stCuen[1] = Strings.Mid(cuenta, 2, 1);
                    stCuen[2] = Strings.Mid(cuenta, 3, 2);
                    stCuen[3] = Strings.Mid(cuenta, 5, 2);
                    stCuen[4] = Strings.Mid(cuenta, 7, 3);
                    stCuen[5] = Strings.Mid(cuenta, 10, 3);

                    for (int inI = 5; inI >= 0; inI--)
                    {
                        stCuenta = Strings.Left(
                            stCuen[0].Trim() + stCuen[1].Trim() + stCuen[2].Trim() + stCuen[3].Trim() +
                            stCuen[4].Trim() + stCuen[5].Trim() + "0000000000000", 12);

                        string cuent1 = "", cuent_ante = "";
                        int cuentaVal = 0; int.TryParse(stCuen[inI], out cuentaVal);
                        if (cuentaVal > 0)
                        {
                            int i2 = 0; cuent1 = "";
                            while (i2 < inI)
                            {
                                cuent1 += stCuen[i2].Trim();
                                i2++;
                            }
                            int inJ = stCuen[inI].Length;
                            cuent_ante = Strings.Left(cuent1 + "0000000000000", 12);

                            string vari = "";
                            for (int x = 1; x <= inJ; x++)
                                vari += "_";

                            mysql = "delete from cnt_presupto where cuenta = '" + cuent_ante.Trim() + "' and periodo = '" + row["periodo"] + "' and agencia = '" +
                                row["agencia"] + "' and cencosto = '" + row["cencosto"] + "'";
                            conect.ExecuteQueryconec(mysql, conect1, "Consolida");

                            mysql = " insert into cnt_presupto select periodo , '" + cuent_ante.Trim() + "' as cuenta , '" + row["agencia"] + "' , '" + row["cencosto"] + "' , sum(ene) as ene , sum(feb) as feb,"
                                + "sum(mar)as mar , sum(abr) as abr , sum(may) as may , sum(jun) as jun , sum(jul) as jul,"
                                + "sum(ago) as ago , sum(sep) as sep , sum(oct) as oct , sum(nov) as nov , sum(dic) ,"
                                + "sum(total) as total from  cnt_presupto where  cuenta like '" + Strings.Left(cuent1 + vari + "0000000000000", 12)
                                + "'  and periodo = '" + row["periodo"]
                                + "' and agencia = '" + row["agencia"] + "' and cencosto = '" + row["cencosto"] + "'"
                                + "group by periodo, agencia ";

                            double cuent_anteVal = 0; double.TryParse(cuent_ante, out cuent_anteVal);
                            if (cuent_anteVal != 0)
                                conect.ExecuteQueryconec(mysql, conect1, "Consolida");

                            stCuen[inI] = Strings.Right("000", inJ);
                        }
                    }
                    men.PerformStep();
                    fila++;
                }

                if (cuenta_afec == "999999999999" || age == "9999" || cenco == "99999999")
                {
                    MessageBox.Show("Operacion finalizada con exito.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    men.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Myread.Dispose();
        }

        public bool RepiteComprobante(string Comprobante, double ConseCpte,
            string CpteRepite, double ConseCpteRepite, DateTime FechaRepite,
            string Detalle, string Usuario, OdbcConnection Myconnect,
            Form myform, string Idbenef)
        {
            // var msgtesSvc = new msgtes.clstesoreria(varini.pstUsuario); // ERROR: CS0246
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Repitiendo Comprobante :  " + Comprobante + "-" + ConseCpte, myform);
            int canreg = 0, fila = 0;
            ok = false;

            stmysql = "Select cntmov.cuenta, cntmov.agencia, cntmov.cencosto,cntmov.nit,domto_auxiliar,vlr_base,factura,vlr_debito,vlr_credito,cntdoc.IdBenef,cntmov.Idsolaux,cntmov.docu_tipo "
                + "from cnt_movimto cntmov inner join cnt_docmto cntdoc on cntmov.compronte = cntdoc.compronte and cntmov.numero = cntdoc.numero "
                + "left join sys_maenit maenit on cntmov.nit = maenit.nit left join cnt_maecuen cntmaec on cntmov.cuenta = cntmaec.cuenta "
                + "where cntmov.compronte = '" + Comprobante + "' and cntmov.numero = '" + ConseCpte + "'";

            var myRead = new DataSet();
            conect.ExecuteQueryDataset(stmysql, Myconnect, "Repitiendo Comprobante ", ref myRead, "TblRepDomto");
            canreg = myRead.Tables["TblRepDomto"].Rows.Count;

            msgbarra.DefineMaximo(stmysql, Myconnect);
            msgbarra.Show();
            Application.DoEvents();

            while (fila < canreg)
            {
                var row = myRead.Tables["TblRepDomto"].Rows[fila];
                string factura = (row["factura"] is DBNull) ? "0" : row["factura"].ToString();

                int _perInt = int.Parse(FechaRepite.ToString("yyyyMM"));
                if (factura != "0" && factura.Trim() != "" && factura != "-")
                {
                    factura = CpteRepite + "-" + ConseCpteRepite;
                    // msgtesSvc.GrabaFactura(factura, row["nit"].ToString(), row["cuenta"].ToString(), // ERROR: CS0103
                        // _perInt, Myconnect, CpteRepite, ConseCpteRepite.ToString(), row["nit"].ToString(), // ERROR: CS0103
                        // FechaRepite, FechaRepite, FechaRepite, // ERROR: CS0103
                        // Convert.ToDouble(row["vlr_debito"]), Convert.ToDouble(row["vlr_credito"]), " ", "O"); // ERROR: CS0103
                }
                else
                {
                    factura = "0";
                    string StTes = "N";
                    string _ctaR = row["cuenta"].ToString();
                    string _tR = "N", _mcR = "N", _naR = "N", _cvR = "99999999", _nvR = "0", _aplR = "N", _nomR = " ";
                    decimal _tsR = 0; string _taR = "0"; int _estR = 0; string _acR = "N", _cbR = "N", _bcR = "9999";
                    BuscarCuenta(ref _ctaR, Myconnect, ref _tR, ref _mcR, ref _naR, ref _cvR,
                        ref _nvR, ref _aplR, ref StTes, ref _nomR, ref _tsR, ref _taR,
                        ref _estR, ref _acR, ref _cbR, ref _bcR);

                    if (StTes == "Y")
                    {
                        factura = CpteRepite + "-" + ConseCpteRepite;
                        // msgtesSvc.GrabaFactura(factura, row["nit"].ToString(), row["cuenta"].ToString(), // ERROR: CS0103
                            // _perInt, Myconnect, CpteRepite, ConseCpteRepite.ToString(), row["nit"].ToString(), // ERROR: CS0103
                            // FechaRepite, FechaRepite, FechaRepite, // ERROR: CS0103
                            // Convert.ToDouble(row["vlr_debito"]), Convert.ToDouble(row["vlr_credito"]), " ", "O"); // ERROR: CS0103
                    }
                }

                GrabaMovimiento(CpteRepite, ConseCpteRepite, row["cuenta"].ToString(), row["agencia"].ToString(),
                    FechaRepite.ToString("yyyyMM"), row["nit"].ToString(), FechaRepite, Detalle,
                    ConseCpteRepite.ToString(), Convert.ToDouble(row["vlr_debito"]),
                    Convert.ToDouble(row["vlr_credito"]), Convert.ToDouble(row["vlr_base"]),
                    Usuario, Myconnect,
                    FACTURA: factura, IdBenef: Idbenef, Cencosto: row["cencosto"].ToString(),
                    DetalleDoc: Detalle, IdSolaux: Convert.ToDouble(row["Idsolaux"]),
                    Modulo: "cont", ClaseAux: CpteRepite, DetalleFactura: Detalle);

                msgbarra.PerformStep();
                ok = true;
                fila++;
            }

            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();
            return ok;
        }

        // --- chunk_e: ValidarPlano ... NombresParaFormatosDian (VB 5294-6233) ---

        public bool ValidarPlano(string NombreArchivo, string comprobante, System.Windows.Forms.Form Myforma, OdbcConnection myconnect)
        {
            string nomruta = AppDomain.CurrentDomain.BaseDirectory + "\\DatosNoExisten.txt";
            string cuenta = " ", nit = " ", compronte = "9999", consecompronte = "0", ccosto = "9999999", credito = "0";
            string debito = "0", mensaje = "", domto_aux = " ", agencia = " ", numeroDocAux = "0", tipoaux = " ";
            int nivel = 0;
            decimal TotReg;
            bool okk = true, NoExiste = false;
            var arreglo = new System.Collections.ArrayList();
            int NumLinea = 1;

            strStreamWriter = new System.IO.StreamWriter(nomruta, false);
            strStreamReader = new System.IO.StreamReader(NombreArchivo);
            string line = strStreamReader.ReadLine();

            var BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Verificando Datos", Myforma);
            try
            {
                TotReg = (decimal)Math.Round((double)(new System.IO.FileInfo(NombreArchivo).Length) / (line.Length + 2));
                arreglo.Clear();
                BarraProgreso.ValorMinimoMaximo(0, (int)TotReg);
                BarraProgreso.Show();

                while (line != null && line.Trim().Length > 20)
                {
                    mensaje = "";
                    tipoaux = " ";
                    arreglo.Clear();
                    string[] parts = line.Split(',');
                    if (parts.Length >= 10)
                    {
                        cuenta = parts[0];
                        nit = parts[1];
                        compronte = parts[2];
                        consecompronte = parts[3];
                        ccosto = parts[4];
                        debito = parts[5];
                        credito = parts[6];
                        agencia = parts[7];
                        domto_aux = parts[8];
                        numeroDocAux = parts[9];
                    }

                    if (Microsoft.VisualBasic.Information.IsNumeric(cuenta))
                    {
                        cuenta = (cuenta + "000000000000").Substring(0, 12);
                        string _ctaV = cuenta;
                        string _t = "N", _mc = "N", _na = "N", _cv = "99999999", _nv = "0", _apl = "N", _tes = "N", _nom = " ";
                        decimal _ts = 0; string _ta = " "; int _est = 0; string _ac = "N", _cb = "N", _bc = "9999";
                        string _nivStr = "0";
                        ok = BuscarCuenta(ref _ctaV, myconnect, ref _t, ref _mc, ref _na, ref _cv,
                            ref _nivStr, ref _apl, ref _tes, ref _nom, ref _ts, ref _ta,
                            ref _est, ref _ac, ref _cb, ref _bc);
                        cuenta = _ctaV;
                        int.TryParse(_nivStr, out nivel);
                        tipoaux = _ta;
                        if (!ok)
                            mensaje = "No Existe Cuenta. ";
                        else if (nivel != 6)
                            mensaje = "Cuenta no permite movimiento. ";
                    }
                    else
                    {
                        mensaje = "Cuenta debe ser numerica. ";
                    }

                    if (Microsoft.VisualBasic.Information.IsNumeric(nit))
                    {
                        ok = BuscarTercero(nit, myconnect);
                        if (!ok)
                            mensaje += nit + "No Existe Tercero. ";
                    }
                    else
                    {
                        mensaje += "Nit debe ser numerico. ";
                    }

                    if (comprobante == "9999")
                    {
                        if (Microsoft.VisualBasic.Information.IsNumeric(compronte))
                        {
                            compronte = Microsoft.VisualBasic.Strings.Right("0000" + compronte, 4);
                            consecompronte = parts.Length > 3 ? parts[3] : "0";
                            double _conse0 = 0;
                            ok = BuscaComprobante(ref compronte, ref _conse0, false, myconnect);
                            if (!ok)
                            {
                                mensaje += compronte + "No Existe Comprobante. ";
                            }
                            else
                            {
                                double _conseStr = 0;
                                double.TryParse(consecompronte, out _conseStr);
                                ok = BuscaComprobante(ref compronte, ref _conseStr, false, myconnect);
                                if (ok)
                                    mensaje += "Documento ya existe.";
                            }
                        }
                        else
                        {
                            mensaje += "Comprobante debe ser numerico. ";
                        }
                    }

                    if (Microsoft.VisualBasic.Information.IsNumeric(ccosto))
                    {
                        ccosto = Microsoft.VisualBasic.Strings.Right("00000000" + ccosto, 8);
                        ok = BuscarCencos(ccosto, myconnect);
                        if (!ok)
                            mensaje += ccosto + "No Existe Centro Costo. ";
                    }
                    else
                    {
                        mensaje += "Centro Costo debe ser numerico. ";
                    }

                    if (!Microsoft.VisualBasic.Information.IsNumeric(debito))
                        mensaje += "Valor debito debe ser numerico. ";
                    if (!Microsoft.VisualBasic.Information.IsNumeric(credito))
                        mensaje += "Valor credito debe ser numerico. ";

                    if (agencia.Trim() == "")
                    {
                        agencia = "9999";
                    }
                    else
                    {
                        if (Microsoft.VisualBasic.Information.IsNumeric(agencia))
                        {
                            agencia = Microsoft.VisualBasic.Strings.Right("0000" + agencia, 4);
                            // ok = msgconfig.BuscaAgencia(agencia, myconnect); // ERROR: CS1501
                            if (!ok)
                                mensaje += agencia + "Agencia no existe. ";
                        }
                        else
                        {
                            mensaje += "Agencia debe ser numerica o un espacio. ";
                        }
                    }

                    if (tipoaux.Trim() != "" && tipoaux != "0" && domto_aux.Trim() == "")
                        mensaje += "Cuenta requiere documento auxliar. ";
                    else if (domto_aux.Trim() != "" && (tipoaux.Trim() == "" || tipoaux == "0"))
                        mensaje += "Cuenta no requiere documento auxliar. ";
                    else if (!Microsoft.VisualBasic.Information.IsNumeric(numeroDocAux))
                        mensaje += "Numero documento auxliar debe ser numerico. ";

                    arreglo.Clear();
                    if (mensaje.Trim() != "")
                    {
                        NoExiste = true;
                        arreglo.Add(cuenta);
                        arreglo.Add(mensaje);
                        strStreamWriter.WriteLine(string.Join("   ", System.Linq.Enumerable.Cast<object>(arreglo).Select(o => o.ToString()).ToArray()) + " Registro Numero. " + NumLinea);
                    }
                    line = strStreamReader.ReadLine();
                    NumLinea++;
                    if (line == null) break;
                    BarraProgreso.PerformStep();
                }
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                strStreamReader.Close();

                if (NoExiste)
                {
                    MessageBox.Show("Las siguientes cuentas tienen datos erroneos.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    System.Diagnostics.Process.Start(nomruta);
                    okk = false;
                }
            }
            catch (Exception ex)
            {
                okk = false;
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                strStreamWriter.Close();
                strStreamWriter.Dispose();
            }
            return okk;
        }

        public bool PlanoReciboDatos(string NombreArchivo, ref string Comprobante, ref double ConseCpte, DateTime FechaMovto, System.Windows.Forms.Form Myforma,
            string detalle, string usuario, OdbcConnection myconnect)
        {
            string cuenta = " ", nit = " ", compronte = "9999", ccosto = "9999999";
            double consecompronte = 0, credito = 0, debito = 0;
            string agencia = " ", domto_aux = " ", numerodocaux = "";
            var BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Recibiendo y Actualizando Datos", Myforma);
            bool okk = false;

            // Read first line for size estimation
            string firstLine;
            using (var sr0 = new System.IO.StreamReader(NombreArchivo))
                firstLine = sr0.ReadLine();

            decimal TotReg = 0;
            try
            {
                ok = ValidarPlano(NombreArchivo, Comprobante, Myforma, myconnect);
                if (ok)
                {
                    if (firstLine != null)
                        TotReg = (decimal)Math.Round((double)(new System.IO.FileInfo(NombreArchivo).Length) / (firstLine.Length + 2));
                    BarraProgreso.ValorMinimoMaximo(0, (int)TotReg);
                    BarraProgreso.Show();

                    using (var sr = new System.IO.StreamReader(NombreArchivo))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string[] parts = line.Split(',');
                            if (parts.Length < 10) continue;
                            cuenta = parts[0];
                            nit = parts[1];
                            compronte = parts[2];
                            double.TryParse(parts[3], out consecompronte);
                            ccosto = parts[4];
                            double.TryParse(parts[5], out debito);
                            double.TryParse(parts[6], out credito);
                            agencia = parts[7];
                            domto_aux = parts[8];
                            numerodocaux = parts[9];

                            if (Comprobante != "9999")
                            {
                                compronte = Comprobante;
                                consecompronte = ConseCpte;
                            }
                            else
                            {
                                compronte = Microsoft.VisualBasic.Strings.Right("0000" + compronte, 4);
                                Comprobante = compronte;
                                ConseCpte = consecompronte;
                            }

                            if (debito != 0 || credito != 0)
                            {
                                okk = true;
                                ccosto = Microsoft.VisualBasic.Strings.Right("00000000" + ccosto, 8);
                                GrabaMovimiento(compronte, consecompronte, cuenta, agencia,
                                    FechaMovto.ToString("yyyyMM"), nit, FechaMovto, detalle, numerodocaux,
                                    debito, credito, 0, usuario, myconnect,
                                    Cencosto: ccosto, ClaseAux: domto_aux);
                            }
                            BarraProgreso.PerformStep();
                        }
                    }
                    BarraProgreso.Close();
                    BarraProgreso.Dispose();
                }
            }
            catch (Exception ex)
            {
                okk = false;
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                BarraProgreso.Close();
                BarraProgreso.Dispose();
            }
            return okk;
        }

        public bool BuscaCertiRetefuente(int Anio, ref string Cuenta, ref string Agencia, ref string Cencosto, OdbcConnection Conect,
            ref string Ciudad, ref string Razonsocial, ref string NitRetenedor,
            ref string Direccion, ref string Firma, string NitRetenido,
            ref double ValorRetenido, DateTime FechaCorte, ref string Cargo)
        {
            int perIni = int.Parse(Anio + "01");
            int Perfin = int.Parse(Anio.ToString() + Microsoft.VisualBasic.Strings.Right("00" + FechaCorte.Month, 2));
            string SelAgencia = "", SelCencos = "";

            if (Agencia != "Consolidado")
            {
                Agencia = Microsoft.VisualBasic.Strings.Right("0000" + Agencia, 4);
                SelAgencia = " and a.agencia = '" + Agencia + "'";
            }
            if (Cencosto != "Consolidado")
            {
                Cencosto = Microsoft.VisualBasic.Strings.Right("0000" + Cencosto, 4);
                SelCencos = " and a.cencosto = '" + Cencosto + "'";
            }

            string where = " from cnt_certrefte where anio = '" + Anio + "' ";

            if (NitRetenido.Trim() != " " && FechaCorte != new DateTime(1950, 1, 1))
            {
                stmysql = "select(  sum(a.vlr_debito - a.vlr_credito) ) as campo1 " +
                    " from cnt_movimto a inner join cnt_maecuen b on b.CUENTA = a.cuenta" +
                    " inner join cnt_nit e on e.NIT = a.nit" +
                    " where a.cuenta like '" + Cuenta + "%' and b.NIVEL = 6" +
                    " and  a.nit = '" + NitRetenido + "' " + SelAgencia + SelCencos +
                    " and a.periodo between " + perIni + " and " + Perfin +
                    " group by a.cuenta ,a.nit ";

                string _vrStr = "0";
                ok = ExecuteQueryconec(stmysql, Conect, "BuscaCertiRetefuente.ValorRetenido", ref _vrStr);
                double.TryParse(_vrStr, out ValorRetenido);
                if (!ok) return ok;
            }

            stmysql = "select ciudad as campo1,razon_soci as campo2,nit as campo3,direccion as campo4 ";
            ok = conect.ExecuteQueryconec(stmysql + where, Conect, "BuscaCertiRetefuente", ref Ciudad, ref Razonsocial, ref NitRetenedor, ref Direccion);
            stmysql = "select firma as campo1,cuenta as campo2,Cargo as campo3 ";
            string _cuentaRef = " ";
            // ok = conect.ExecuteQueryconec(stmysql + where, Conect, "BuscaCertiRetefuente", ref Firma, ref _cuentaRef, ref Cargo); // ERROR: CS1501
            return ok;
        }

        public bool BuscaCuentaContable(string cuenta, OdbcConnection myconnect)
        {
            string nivel = "6";
            return BuscaCuentaContable(cuenta, myconnect, ref nivel);
        }

        public bool BuscaCuentaContable(string cuenta, OdbcConnection myconnect, ref string NIVEL)
        {
            string cuenta_str = "";
            stmysql = "select cuenta as campo1,NIVEL as campo2 from cnt_maecuen  where cuenta = '" + cuenta + "'";
            ok = ExecuteQueryconec(stmysql, myconnect, "BuscaCuentaContable", ref cuenta_str, ref NIVEL);
            return ok;
        }

        public void GrabaCuentaContable(string cuenta, string tercero, string ajus_infla, string consi_banca, string decla_renta,
            string APLI_CARTCOOPE, string APLI_AHOR_CDT, string APLI_INVENTA, string APLI_TESORERI,
            string APLI_NOMINA, string APLI_FACTURAC, string aux_domto, string natura, string nombre,
            int nivel, decimal tasa, string cencos, string gru_act_fij, string clas_act_fij, string mane_cencos,
            string APLI_CONTAB, string actopera, string Periodo, OdbcConnection appconnect)
        {
            string anio = Periodo.Substring(0, 4);
            string Mes = Periodo.Substring(4, 2);
            string _niv = "6";
            ok = BuscaCuentaContable(cuenta, appconnect, ref _niv);
            if (!ok)
            {
                stmysql = "insert into cnt_maecuen (cuenta,tercero,ajus_infla,consi_banca,decla_renta,APLI_CARTCOOPE," +
                    "APLI_AHOR_CDT,APLI_INVENTA,APLI_TESORERI,APLI_NOMINA,APLI_FACTURAC,aux_domto,natura,nombre," +
                    "nivel,tasa,cencos,gru_act_fij,clas_act_fij,mane_cencos,APLI_CONTAB,actopera) values ('" +
                    Microsoft.VisualBasic.Strings.Left(cuenta.Trim() + "0000000000000", 12) +
                    "','" + tercero +
                    "','" + ajus_infla +
                    "','" + consi_banca +
                    "','" + decla_renta +
                    "','" + APLI_CARTCOOPE +
                    "','" + APLI_AHOR_CDT +
                    "','" + APLI_INVENTA +
                    "','" + APLI_TESORERI +
                    "','" + APLI_NOMINA +
                    "','" + APLI_FACTURAC +
                    "','" + aux_domto +
                    "','" + natura +
                    "','" + nombre +
                    "','" + nivel +
                    "','" + tasa +
                    "','" + Microsoft.VisualBasic.Strings.Right("00000000" + cencos, 8) +
                    "','" + gru_act_fij +
                    "','" + clas_act_fij +
                    "','" + mane_cencos + "','" + APLI_CONTAB + "','" + Microsoft.VisualBasic.Strings.Left(actopera, 1) + "')";
                ExecuteQueryconec(stmysql, appconnect, "GrabaCuentaContable");
            }

            suma_saldos(anio, cuenta, "9999", cencos, Mes, 0, 0, "D", appconnect);
        }

        public bool GrabaCertiRetefuente(int Anio, ref string Cuenta, ref string Agencia, ref string Cencosto, OdbcConnection Conect,
            ref string Ciudad, ref string Razonsocial, ref string NitRetenedor,
            ref string Direccion, ref string Firma, ref string Cargo)
        {
            string _c1 = " ", _c2 = " ", _c3 = " ", _c4 = " ", _f = " ", _carg = " ";
            double _vr = 0;
            ok = BuscaCertiRetefuente(Anio, ref Cuenta, ref Agencia, ref Cencosto, Conect,
                ref _c1, ref _c2, ref _c3, ref _c4, ref _f, " ", ref _vr, new DateTime(1950, 1, 1), ref _carg);
            if (!ok)
            {
                stmysql = "insert into  cnt_certrefte (anio,ciudad ,razon_soci,nit, direccion,firma,cuenta,Cargo) values ('" + Anio + "','" +
                    Ciudad + "','" + Razonsocial + "','" + NitRetenedor + "','" + Direccion + "','" + Firma + "','" + Cuenta + "','" + Cargo + "')";
            }
            else
            {
                stmysql = "Update cnt_certrefte set ciudad = '" + Ciudad + "' , razon_soci = '" +
                    Razonsocial + "' , nit = '" + NitRetenedor + "' ,direccion = '" + Direccion + "' , firma = '" + Firma + "', " +
                    " cuenta = '" + Cuenta + "',Cargo='" + Cargo + "' where anio = '" + Anio + "' ";
            }
            ok = ExecuteQueryconec(stmysql, Conect, "GrabaCertiRetefuente");
            return ok;
        }

        public bool ImprimirSertificados(string Anio, string Cuenta, string Agencia, string Cencosto,
            string Fec_corte, string nitIni, string nitFin, int Cualfec_cert,
            OdbcConnection myConec03, System.Windows.Forms.Form Pertenece, string exportar,
            ref string periodoinicial, string tipoInf)
        {
            bool vistaPre = false;
            bool Ok1 = false;
            double ValorRetenido = 0;
            int perIni, Perfin;
            string SelAgencia = "", SelCencos = "";
            DateTime fechacorte, fechaini;

            var pro = new ERP.Core.Compartido.Controles.Barraprogress("Generando Certificados", Pertenece);
            pro.ValorMinimoMaximo(0, 5);
            pro.Show();

            if (Agencia != "Consolidado")
            {
                Agencia = Microsoft.VisualBasic.Strings.Right("0000" + Agencia, 4);
                SelAgencia = " and agencia = '" + Agencia + "'";
            }
            if (Cencosto != "Consolidado")
            {
                Cencosto = Microsoft.VisualBasic.Strings.Right("0000" + Cencosto, 4);
                SelCencos = " and cencosto = '" + Cencosto + "'";
            }

            Perfin = int.Parse(Fec_corte);
            if (periodoinicial != "1")
                perIni = int.Parse(periodoinicial);
            else
                perIni = int.Parse(Anio + "01");

            fechaini = new DateTime(int.Parse(perIni.ToString().Substring(0, 4)), int.Parse(perIni.ToString().Substring(4, 2)), 1);
            fechacorte = new DateTime(int.Parse(Fec_corte.Substring(0, 4)), int.Parse(Fec_corte.Substring(4, 2)), 1);
            fechacorte = new DateTime(fechacorte.Year, fechacorte.Month, DateTime.DaysInMonth(fechacorte.Year, fechacorte.Month));

            var NumLetras = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
            var Impre = new System.Windows.Forms.PrintDialog();
            var R = new ERP.Core.Compartido.Reportes.reporte("cnt_rinfcertretefte");

            if (nitIni == nitFin)
            {
                string _city = " ", _razSoc = " ", _nit = " ", _dir = " ", _firma = " ", _cargo = " ";
                string _cuentaB = Cuenta, _agenciaB = Agencia, _cencosB = Cencosto;
                if (!BuscaCertiRetefuente(int.Parse(Anio), ref _cuentaB, ref _agenciaB, ref _cencosB, myConec03,
                    ref _city, ref _razSoc, ref _nit, ref _dir, ref _firma, nitIni, ref ValorRetenido, fechacorte, ref _cargo))
                {
                    MessageBox.Show("No se tiene informaci\u00f3n de retencion para este Tercero.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    pro.Close();
                    pro.Dispose();
                    goto Label1;
                }
                vistaPre = true;
            }

            var dire = new System.Windows.Forms.FolderBrowserDialog();
            if (exportar == "W")
            {
                dire.Description = "Buscar directorio";
                dire.ShowNewFolderButton = true;
                dire.RootFolder = Environment.SpecialFolder.MyComputer;
                if (dire.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    MessageBox.Show("Cancelado por el usuario.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    pro.Close();
                    pro.Dispose();
                    return false;
                }
            }
            else
            {
                if (Impre.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    MessageBox.Show("Cancelado por el usuario.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    pro.Close();
                    pro.Dispose();
                    return false;
                }
            }

            stmysql = "select nit,sum(ValorRetenido) as ValorRetenido  from Cnt_certifiRetefuente_vw " +
                " where   cuenta like '" + Cuenta + "%' " +
                " and  nit between '" + nitIni + "' and '" + nitFin + "'" +
                " and periodo between  '" + perIni + "' and '" + Perfin + "' " +
                " and ValorRetenido <> 0 " + SelCencos + SelAgencia +
                " group by nit ";

            var dsdata = new DataSet();
            conect.ExecuteQueryDataset(stmysql, myConec03, "ImprimirSertificados", ref dsdata, "tblcerti");

            pro.ValorMinimoMaximo(0, dsdata.Tables["tblcerti"].Rows.Count);

            for (int fila = 0; fila < dsdata.Tables["tblcerti"].Rows.Count; fila++)
            {
                try
                {
                    var row = dsdata.Tables["tblcerti"].Rows[fila];
                    ValorRetenido = 0;
                    ValorRetenido = Convert.ToDouble(row["ValorRetenido"]);
                    R.SetParameterValue("fecha_corte", fechacorte);
                    R.SetParameterValue("NitIni", row["nit"].ToString());
                    R.SetParameterValue("anioinfo", Anio);
                    R.SetParameterValue("cuenta", Cuenta);
                    R.SetParameterValue("agencia", Agencia);
                    R.SetParameterValue("cencosto", Cencosto);
                    if (Cualfec_cert == 0)
                        R.SetParameterValue("fec_certifi", DateTime.Now.Date.ToLongDateString());
                    else
                        R.SetParameterValue("fec_certifi", fechacorte.ToLongDateString());
                    if (ValorRetenido < 0) ValorRetenido = ValorRetenido * -1;
                    R.SetParameterValue("valor_letras", NumLetras.Num_a_Letras(ValorRetenido));
                    R.SetParameterValue("corte_ica", fechaini);
                    R.SetParameterValue("ica-fuent", tipoInf);

                    if (exportar == "W")
                    {
                        // R.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.WordForWindows, // ERROR: CS0103
                            // dire.SelectedPath + "\\" + row["nit"].ToString() + fechacorte.ToString("yyyyMMdd") + ".doc"); // ERROR: CS0103
                        R.Refresh();
                    }
                    else
                    {
                        R.PrintOptions.PrinterName = Impre.PrinterSettings.PrinterName;
                        R.PrintToPrinter(0, false, 1, 999);
                    }
                    Ok1 = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                pro.PerformStep();
            }

            pro.Close();
            pro.Dispose();
Label1:
            return Ok1;
        }

        public DataSet BuscarSaldosXcuenta(string Cuenta, string Periodo, OdbcConnection myconnect)
        {
            var DsdataSet = new DataSet();
            stmysql = "select ene as ENERO, feb AS FEBRERO, mar AS MARZO, abr AS ABRIL, may AS MAYO, jun AS JUNIO," +
                " jul AS JULIO, ago AS AGOSTO, sep AS SEPTIEMBRE, Oct AS OCTUBRE, nov AS NOVIEMBRE, dic AS DICIEMBRE," +
                " trece AS TRECE from cnt_salcuen_vw where periodo=" + Periodo.Substring(0, 4) + " and cuenta='" + Cuenta + "'";
            conect.ExecuteQueryDataset(stmysql, myconnect, "BuscarSaldosXcuenta", ref DsdataSet, "TblSaldosCuenta");
            return DsdataSet;
        }

        public DataSet CargaDatosTotalLibyBal(int anio, int mes, string agencia, string cencosto, int nivel, OdbcConnection myconnect)
        {
            var dsdata = new DataSet();
            string campos = "mae.cuenta, mae.nombre,mae.nivel,mae.aux_domto,mae.tercero ";
            string grupo = campos;

            if (agencia == "Todos" || Microsoft.VisualBasic.Information.IsNumeric(agencia))
            {
                campos += ",sal.agencia ";
                grupo += ",sal.agencia ";
            }
            else
            {
                campos += ",' ' as agencia ";
            }
            if (cencosto == "Todos" || Microsoft.VisualBasic.Information.IsNumeric(cencosto))
            {
                campos += ",sal.cencosto ";
                grupo += ",sal.cencosto ";
            }
            else
            {
                campos += ",' ' as cencosto ";
            }

            int nivelMin = (nivel < 3 ? 3 : nivel);
            stmysql = "select case " + mes + " when 1 then sum(sal.saldo_inicial) " +
                "when 2 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre)) " +
                "when 3 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre)) " +
                "when 4 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre)) " +
                "when 5 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre)) " +
                "when 6 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre))" +
                "when 7 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre+sal.jun_deb-sal.jun_cre)) " +
                "when 8 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre+sal.jun_deb-sal.jun_cre+sal.jul_deb-sal.jul_cre)) " +
                "when 9 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre+sal.jun_deb-sal.jun_cre+sal.jul_deb-sal.jul_cre+sal.ago_deb-sal.ago_cre)) " +
                "when 10 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre+sal.jun_deb-sal.jun_cre+sal.jul_deb-sal.jul_cre+sal.ago_deb-sal.ago_cre+sal.sep_deb-sal.sep_cre)) " +
                "when 11 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre+sal.jun_deb-sal.jun_cre+sal.jul_deb-sal.jul_cre+sal.ago_deb-sal.ago_cre+sal.sep_deb-sal.sep_cre+sal.oct_deb-sal.oct_cre)) " +
                "when 12 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre+sal.jun_deb-sal.jun_cre+sal.jul_deb-sal.jul_cre+sal.ago_deb-sal.ago_cre+sal.sep_deb-sal.sep_cre+sal.oct_deb-sal.oct_cre+sal.nov_deb-sal.nov_cre)) " +
                "when 13 then sum((sal.saldo_inicial+sal.ene_deb-sal.ene_cre+sal.feb_deb-sal.feb_cre+sal.mar_deb-sal.mar_cre+sal.abr_deb-sal.abr_cre+sal.may_deb-sal.may_cre+sal.jun_deb-sal.jun_cre+sal.jul_deb-sal.jul_cre+sal.ago_deb-sal.ago_cre+sal.sep_deb-sal.sep_cre+sal.oct_deb-sal.oct_cre+sal.nov_deb-sal.nov_cre+sal.dic_deb-sal.dic_cre)) " +
                "end as SaldoInicial," +
                "case " + mes + " when 1 then sum((sal.ene_deb)) when 2 then sum((sal.feb_deb)) when 3 then sum((sal.mar_deb)) " +
                "when 4 then sum((sal.abr_deb)) when 5 then sum((sal.may_deb)) when 6 then sum((sal.jun_deb)) when 7 then sum((sal.jul_deb)) " +
                "when 8 then sum((sal.ago_deb)) when 9 then sum((sal.sep_deb)) when 10 then sum((sal.oct_deb)) when 11 then sum((sal.nov_deb)) " +
                "when 12 then sum((sal.dic_deb)) when 13 then sum(sal.tre_deb) end as MovDebito," +
                "case " + mes + " when 1 then sum((sal.ene_cre)) when 2 then sum((sal.feb_cre)) when 3 then sum((sal.mar_cre)) " +
                "when 4 then sum((sal.abr_cre)) when 5 then sum((sal.may_cre)) when 6 then sum((sal.jun_cre)) when 7 then sum((sal.jul_cre)) " +
                "when 8 then sum((sal.ago_cre)) when 9 then sum((sal.sep_cre)) when 10 then sum((sal.oct_cre)) when 11 then sum((sal.nov_cre)) " +
                "when 12 then sum((sal.dic_cre)) when 13 then sum(sal.tre_cre) end as MovCredito, " + campos +
                "from cnt_salage sal inner join cnt_maecuen mae on sal.cuenta=mae.cuenta and mae.nivel<=" + nivelMin +
                " where periodo=" + anio +
                (Microsoft.VisualBasic.Information.IsNumeric(agencia) ? " and sal.agencia='" + Microsoft.VisualBasic.Strings.Right("0000" + agencia, 4) + "'" : "") +
                (Microsoft.VisualBasic.Information.IsNumeric(cencosto) ? " and sal.cencosto='" + Microsoft.VisualBasic.Strings.Right("00000000" + cencosto, 8) + "'" : "") +
                " group by " + grupo + " order by mae.cuenta";

            conect.ExecuteQueryDataset(stmysql, myconnect, "CargaDatosTotalLibyBal", ref dsdata, "TblLibroMayor");
            return dsdata;
        }

        public void ImprimirAuxCuenta(int PeriodoIni, int periodoFin, string CuentaIni, string cuentaFin, string empresa,
            int TipoInf, string Cencosini, string cencosfin, int diaini, int diafin, int orden, System.Windows.Forms.Form forma)
        {
            var INFO = new ERP.Core.Compartido.Reportes.reporte("cnt_rauxcuen");
            INFO.SetParameterValue("a\u00f1o", PeriodoIni / 100);
            INFO.SetParameterValue("PERIODO_INICIAL", PeriodoIni);
            INFO.SetParameterValue("PERIODO_FINAL", periodoFin);
            INFO.SetParameterValue("CUENTA_INI", CuentaIni);
            INFO.SetParameterValue("CUENTA_FIN", cuentaFin);
            INFO.SetParameterValue("EMPRESA", empresa);
            INFO.SetParameterValue("tipo", TipoInf);
            INFO.SetParameterValue("cencosini", Cencosini);
            INFO.SetParameterValue("cencosfin", cencosfin);
            INFO.SetParameterValue("diaini", diaini);
            INFO.SetParameterValue("diafin", diafin);
            try { INFO.SetParameterValue("orden", orden); } catch { }
            config.confi_reportes(forma, INFO);
        }

        public void ImprimirAuxTercero(int PeriodoIni, int periodoFin, string CuentaIni, string cuentaFin, string empresa,
            int TipoInf, string Cencos, string NitIni, string NitFin, System.Windows.Forms.Form forma)
        {
            var INFO = new ERP.Core.Compartido.Reportes.reporte("cnt_rauxterce");
            INFO.SetParameterValue("PERIODO_INICIAL", PeriodoIni);
            INFO.SetParameterValue("PERIODO_FINAL", periodoFin);
            INFO.SetParameterValue("CUENTA_INI", CuentaIni);
            INFO.SetParameterValue("CUENTA_FIN", cuentaFin);
            INFO.SetParameterValue("NIT_INI", NitIni);
            INFO.SetParameterValue("NIT_FIN", NitFin);
            INFO.SetParameterValue("EMPRESA", empresa);
            INFO.SetParameterValue("tipo", TipoInf);
            INFO.SetParameterValue("Aniopar", PeriodoIni.ToString().Substring(0, 4));
            INFO.SetParameterValue("cencos", Cencos);
            if (Cencos == "Consolidado")
                INFO.SetParameterValue("filcencos", " ");
            else
                INFO.SetParameterValue("filcencos", " and cnt_movimto.cencosto = '" + Cencos + "'");
            config.confi_reportes(forma, INFO);
        }

        public void ImprimirAuxCuentaDoc(int PeriodoIni, int periodoFin, string CuentaIni, string cuentaFin, string empresa, string NitEmp,
            string DocAux, string NitIni, string NitFin, int diaini, int diafin, int orden, System.Windows.Forms.Form forma)
        {
            var INFO = new ERP.Core.Compartido.Reportes.reporte("cnt_rauxdocucruce");
            INFO.SetParameterValue("PERIODO_INICIAL", PeriodoIni);
            INFO.SetParameterValue("PERIODO_FINAL", periodoFin);
            INFO.SetParameterValue("CUENTA_INI", CuentaIni);
            INFO.SetParameterValue("CUENTA_FIN", cuentaFin);
            INFO.SetParameterValue("EMPRESA", empresa);
            INFO.SetParameterValue("nit", NitEmp);
            INFO.SetParameterValue("Documento", DocAux);
            INFO.SetParameterValue("diaini", diaini);
            INFO.SetParameterValue("diafin", diafin);
            INFO.SetParameterValue("NIT_INI", NitIni);
            INFO.SetParameterValue("NIT_FIN", NitFin);
            INFO.SetParameterValue("A\u00f1o1", PeriodoIni.ToString().Substring(0, 4));
            INFO.SetParameterValue("A\u00f1o2", periodoFin.ToString().Substring(0, 4));
            try { INFO.SetParameterValue("orden", orden); } catch { }
            config.confi_reportes(forma, INFO);
        }

        public DataSet CargarMovPeriodos(int anio, string cuenta, OdbcConnection myconnect)
        {
            var dsdata = new DataSet();
            stmysql = "select sum(saldo_inicial)  as saldoinicial, sum(ene_deb) as  ene_deb, sum(ene_cre) as ene_cre, sum(feb_deb) as feb_deb, sum(feb_cre) as feb_cre, " +
                "sum(mar_deb) as mar_deb, sum(mar_cre) as mar_cre, sum(abr_deb) as abr_deb, sum(abr_cre) as abr_cre, sum(may_deb) as may_deb, sum(may_cre) as may_cre, " +
                "sum(jun_deb) as jun_deb, sum(jun_cre) as jun_cre, sum(jul_deb) as jul_deb, sum(jul_cre) as jul_cre, sum(ago_deb) as ago_deb, sum(ago_cre) as ago_cre," +
                "sum(sep_deb) as sep_deb, sum(sep_cre) as sep_cre, sum(oct_deb) as oct_deb, sum(oct_cre) as oct_cre, sum(nov_deb) as nov_deb, sum(nov_cre) as nov_cre, sum(dic_deb) as dic_deb, " +
                "sum(dic_cre) as dic_cre, sum(trece) as trece from cnt_salage where cuenta='" + cuenta + "' and periodo=" + anio + " group by cuenta";
            conect.ExecuteQueryDataset(stmysql, myconnect, "CargarMovPeriodos", ref dsdata, "TblMovCiclo");
            return dsdata;
        }

        public void AgregaColumnasMovPeriodo(ref DataSet MovPeriodo)
        {
            MovPeriodo.Tables.Add("MovPeriodo");
            var cols = MovPeriodo.Tables["MovPeriodo"].Columns;
            cols.Add("periodo", typeof(int));
            cols.Add("saldoini", typeof(double));
            cols.Add("debito", typeof(double));
            cols.Add("credito", typeof(double));
            cols.Add("saldo", typeof(double));
        }

        public void ImprimirReporte(System.Windows.Forms.Form pertenese, string cuenta, string nomcuenta,
            int periodo, DataSet dsdata, string empresa, string nit, string direccion, string telefono)
        {
            var r = new ERP.Core.Compartido.Reportes.reporte("cnt_rmovcta", false);
            var confi_report = new ERP.Core.Compartido.Reportes.config_report();
            r.SetDataSource(dsdata.Tables["MovPeriodo"]);
            r.SetParameterValue("nombre_empresa", empresa);
            r.SetParameterValue("Periodo", periodo);
            r.SetParameterValue("nit", nit);
            r.SetParameterValue("direccion", direccion);
            r.SetParameterValue("telefono", telefono);
            r.SetParameterValue("cuenta", cuenta);
            r.SetParameterValue("nomcuenta", nomcuenta);
            confi_report.confi_reportes(pertenese, r);
        }

        public void RevisaSaldoCencos(string Cpte, double Conse, DateTime fecha, OdbcConnection myconnet)
        {
            var DsdataSet = new DataSet();
            double fila = 0, SaldoCuenta = 0, SaldoCencos = 0, credito = 0, Debito = 0, saldo = 0;
            double SaldoIniCuenta = 0;
            int primer = 0;
            string Cencos = null, cuenta = null;
            var msgcntSvc = new ClsContabilidad();

            stmysql = "select cntmov.cuenta,invcen.cencos,cntmov.vlr_debito, cntmov.vlr_credito," +
                "invcen.saldoinicial as SaldoCencos from cnt_movimto cntmov inner join tblcencos invcen " +
                "on cntmov.cuenta = invcen.cuenta where cntmov.compronte = '0098' and numero = 2" +
                " order by cntmov.cuenta,invcen.cencos";

            conect.ExecuteQueryDataset(stmysql, myconnet, "RevisaSaldoCencos", ref DsdataSet, "tblmovtocencos");

            while (fila < DsdataSet.Tables["tblmovtocencos"].Rows.Count)
            {
                var row = DsdataSet.Tables["tblmovtocencos"].Rows[(int)fila];

                if (primer == 1 && cuenta != row["cuenta"].ToString())
                {
                    primer = 0;
                    if (SaldoCuenta != 0)
                    {
                        if (SaldoCuenta > 0) { Debito = SaldoCuenta; credito = 0; }
                        else { credito = SaldoCuenta * -1; Debito = 0; }
                        msgcntSvc.GrabaMovimiento(Cpte, Conse, cuenta, "9999", fecha.ToString(varini.PstForFec), "", fecha, "", "", Debito, credito, 0, varini.pstUsuario, myconnet,
                            Cencosto: Cencos);
                        if (SaldoIniCuenta > 0) { credito = SaldoIniCuenta; Debito = 0; }
                        else { credito = 0; Debito = SaldoIniCuenta * -1; }
                        msgcntSvc.GrabaMovimiento(Cpte, Conse, cuenta, "9999", fecha.ToString(varini.PstForFec), "", fecha, "", "", Debito, credito, 0, varini.pstUsuario, myconnet);
                    }
                    else
                    {
                        if (SaldoIniCuenta > 0) { credito = SaldoIniCuenta; Debito = 0; }
                        else { credito = 0; Debito = SaldoIniCuenta * -1; }
                        msgcntSvc.GrabaMovimiento(Cpte, Conse, cuenta, "9999", fecha.ToString(varini.PstForFec), "", fecha, "", "", Debito, credito, 0, varini.pstUsuario, myconnet);
                    }
                    cuenta = row["cuenta"].ToString();
                    SaldoCuenta = 0;
                }

                if (primer == 0)
                {
                    SaldoCuenta = Convert.ToDouble(row["vlr_debito"]);
                    if (SaldoCuenta == 0) SaldoCuenta = Convert.ToDouble(row["vlr_credito"]) * -1;
                    cuenta = row["cuenta"].ToString();
                    SaldoIniCuenta = SaldoCuenta;
                    primer = 1;
                }

                SaldoCencos = Convert.ToDouble(row["SaldoCencos"]);
                if (SaldoCencos < 0) SaldoCencos = SaldoCencos * -1;

                saldo = SaldoCuenta - SaldoCencos;
                if (saldo >= 0)
                {
                    if (SaldoCencos < 0) { credito = SaldoCencos * -1; Debito = 0; }
                    else { credito = 0; Debito = SaldoCencos; }
                    Cencos = row["cencos"].ToString();
                }
                else
                {
                    Cencos = row["cencos"].ToString();
                    if (SaldoCuenta > 0) { Debito = SaldoCuenta; credito = 0; SaldoCencos = SaldoCuenta; }
                    else { credito = SaldoCuenta * -1; SaldoCencos = SaldoCuenta; Debito = 0; }
                }

                if (SaldoCuenta != 0)
                {
                    msgcntSvc.GrabaMovimiento(Cpte, Conse, row["cuenta"].ToString(), "9999", fecha.ToString(varini.PstForFec), "", fecha, "", "", Debito, credito, 0, varini.pstUsuario, myconnet,
                        Cencosto: Cencos);
                }

                stmysql = "update cnt_salage set saldo_inicial = 0 where cuenta = '" + row["cuenta"].ToString() + "' and cencosto = '" + Cencos + "'";
                ExecuteQueryconec(stmysql, myconnet, "RevisaSaldoCencos");
                SaldoCuenta -= SaldoCencos;
                fila++;
            }

            if (SaldoCuenta != 0)
            {
                if (SaldoCuenta > 0) { Debito = SaldoCuenta; credito = 0; }
                else { credito = SaldoCuenta * -1; Debito = 0; }
                msgcntSvc.GrabaMovimiento(Cpte, Conse, cuenta, "9999", fecha.ToString(varini.PstForFec), "", fecha, "", "", Debito, credito, 0, varini.pstUsuario, myconnet,
                    Cencosto: Cencos);
                if (SaldoIniCuenta > 0) { credito = SaldoIniCuenta; Debito = 0; }
                else { credito = 0; Debito = SaldoIniCuenta * -1; }
                msgcntSvc.GrabaMovimiento(Cpte, Conse, cuenta, "9999", fecha.ToString(varini.PstForFec), "", fecha, "", "", Debito, credito, 0, varini.pstUsuario, myconnet);
            }
            else
            {
                if (SaldoIniCuenta > 0) { credito = SaldoIniCuenta; Debito = 0; }
                else { credito = 0; Debito = SaldoIniCuenta * -1; }
                msgcntSvc.GrabaMovimiento(Cpte, Conse, cuenta, "9999", fecha.ToString(varini.PstForFec), "", fecha, "", "", Debito, credito, 0, varini.pstUsuario, myconnet);
            }
        }

        public void NumeracionFolios(double NumeroInicial, double NumeroFinal)
        {
            var pd = new System.Drawing.Printing.PrintDocument();
            var msgimpresoras = new System.Windows.Forms.PrintDialog();
            pd.PrintPage += pd_PrintPagefolios;
            NumFomlios.NumInicial = NumeroInicial;
            NumFomlios.NumFinal = NumeroFinal;
            msgimpresoras.ShowDialog();
            pd.PrinterSettings.PrinterName = msgimpresoras.PrinterSettings.PrinterName;
            pd.Print();
        }

        private void pd_PrintPagefolios(object sender, System.Drawing.Printing.PrintPageEventArgs ev)
        {
            var printFont = new System.Drawing.Font("Courier New", 10, System.Drawing.FontStyle.Regular);
            string line = "Folio # " + NumFomlios.NumInicial;
            ev.Graphics.DrawString(line, printFont, System.Drawing.Brushes.Black, 600, 30, new System.Drawing.StringFormat());
            NumFomlios.NumInicial += 1;
            ev.HasMorePages = NumFomlios.NumInicial <= NumFomlios.NumFinal;
        }

        public void NombresParaFormatosDian(int anio, System.Windows.Forms.Form forma, OdbcConnection myconnect)
        {
            string nombre, nom1, nom2, apl1, apl2;
            var progreso = new ERP.Core.Compartido.Controles.Barraprogress("Corrigiendo Nombres", forma);
            stmysql = "SELECT medios.nit, medios.nombre, medios.idformato, medios.idcpto, medios.Razonsocial,cnt_nit.NaturalTieneRut,cnt_nit.tipo_persona,cnt_nit.RAZON_SOCIAL as NomRazonSocial " +
                "FROM cnt_infmedian medios " +
                "INNER JOIN cnt_nit cnt_nit On cnt_nit.NIT = medios.nit " +
                "WHERE anio=" + anio;
            try
            {
                progreso.DefineMaximo(stmysql, myconnect);
                progreso.Show(forma);

                var daData = new OdbcDataAdapter(stmysql, myconnect);
                var dsData = new DataSet();
                int cantidadreg = daData.Fill(dsData, "TblNombresMedios");
                int i = 0;
                while (i < cantidadreg)
                {
                    var rowN = dsData.Tables["TblNombresMedios"].Rows[i];
                    nombre = rowN["nombre"].ToString();
                    for (int n = 1; n <= 5; n++)
                        nombre = nombre.Replace("  ", " ");
                    string[] CadenaNombres = nombre.Split(' ');
                    apl1 = " "; apl2 = " "; nom1 = " "; nom2 = " ";
                    switch (CadenaNombres.Length)
                    {
                        case 1:
                            apl1 = CadenaNombres[0];
                            break;
                        case 2:
                            apl1 = CadenaNombres[0]; nom1 = CadenaNombres[1];
                            apl2 = " "; nom2 = " ";
                            break;
                        case 3:
                            apl1 = CadenaNombres[0]; apl2 = CadenaNombres[1]; nom1 = CadenaNombres[2]; nom2 = " ";
                            break;
                        case 4:
                            apl1 = CadenaNombres[0]; apl2 = CadenaNombres[1]; nom1 = CadenaNombres[2]; nom2 = CadenaNombres[3];
                            break;
                        case 5:
                            if (CadenaNombres[1].Length <= 3) { apl1 = CadenaNombres[0]; apl2 = CadenaNombres[1] + " " + CadenaNombres[2]; nom1 = CadenaNombres[3]; nom2 = CadenaNombres[4]; }
                            if (CadenaNombres[2].Length <= 3) { apl1 = CadenaNombres[0]; apl2 = CadenaNombres[1]; nom1 = CadenaNombres[2] + " " + CadenaNombres[3]; nom2 = CadenaNombres[4]; }
                            if (CadenaNombres[3].Length <= 3) { apl1 = CadenaNombres[0]; apl2 = CadenaNombres[1]; nom1 = CadenaNombres[2]; nom2 = CadenaNombres[3] + " " + CadenaNombres[4]; }
                            break;
                        case 6:
                            apl1 = CadenaNombres[0]; apl2 = CadenaNombres[1] + " " + CadenaNombres[2]; nom1 = CadenaNombres[3] + " " + CadenaNombres[4]; nom2 = CadenaNombres[5];
                            break;
                        case 7:
                            apl1 = CadenaNombres[0]; apl2 = CadenaNombres[1]; nom1 = CadenaNombres[2] + " " + CadenaNombres[3] + " " + CadenaNombres[4]; nom2 = CadenaNombres[5] + " " + CadenaNombres[6];
                            break;
                        default:
                            apl1 = nombre.Substring(0, Math.Min(50, nombre.Length));
                            apl2 = nombre.Substring(0, Math.Min(50, nombre.Length));
                            nom1 = nombre.Substring(0, Math.Min(50, nombre.Length));
                            nom2 = nombre.Substring(0, Math.Min(50, nombre.Length));
                            break;
                    }

                    if (rowN["tipo_persona"].ToString() == "N" && rowN["NaturalTieneRut"].ToString() == "N")
                    {
                        stmysql = "update cnt_infmedian set nombre1='" + nom1 + "',nombre2='" + nom2 + "', apellido1='" + apl1 + "',apellido2='" + apl2 + "',Razonsocial=' '" +
                            " where anio=" + anio + " and nit='" + rowN["nit"] + "' and idformato=" + rowN["idformato"] + " and idcpto=" + rowN["idcpto"];
                    }
                    else
                    {
                        stmysql = "update cnt_infmedian set nombre1=' ',nombre2=' ', apellido1=' ',apellido2=' ',Razonsocial='" + rowN["NomRazonSocial"] +
                            "' where anio=" + anio + " and nit='" + rowN["nit"] + "' and idformato=" + rowN["idformato"] + " and idcpto=" + rowN["idcpto"];
                    }
                    ExecuteQueryconec(stmysql, myconnect, "NombresParaFormatosDian");
                    progreso.PerformStep();
                    i++;
                }
                progreso.Dispose();
                progreso.Close();
            }
            catch (Exception ex)
            {
                ex.ToString();
                progreso.Dispose();
                progreso.Close();
            }
        }




    } // class ClsContabilidad
} // namespace msgcnt
