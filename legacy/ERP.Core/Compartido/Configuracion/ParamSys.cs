using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using ERP.Core.Compartido.Utilidades;

namespace ERP.Core.Compartido.Configuracion
{
    /// <summary>
    /// Clase para parametros del sistema
    /// </summary>
    public class ParamSys
    {
        #region Campos privados

#if EXCEL_LEGACY
        private Excel.Application _mExcel;
#endif
        private OdbcCommand _mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect _odbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private bool _ok;
        private string _stmysql;
        private ArrayList _lista = new ArrayList();
        private Ayuda _msgsas = new Ayuda("admin");
        private StreamWriter _strStreamWriter;
        private StreamReader _strStreamReader;

        #endregion

        #region Enumeraciones

        public enum Acciones
        {
            Borrar = 1,
            InsertActualiza = 2
        }

        public enum Navega
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }

        public enum EncripDescrip
        {
            Encriptar = 1,
            Descriptar = 2
        }

        public enum FormaliquidarFinanciera
        {
            TasaNominalAnual = 1,
            TasaEfectivaAnual = 2,
            TasaNominalAnual_Efectiva = 3
        }

        #endregion

        #region Constructor y Destructor

        public ParamSys()
        {
            try
            {
                _odbcConnect.MyOdbcConect(ref varini);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        ~ParamSys()
        {
        }

        #endregion

        #region Metodos de Compania

        public virtual bool BuscarCompania(string Codigo, OdbcConnection myconect,
            ref string CuentaUtilidad, ref string CptoCap, ref string CalculaSaldo, ref string CptoAfavor,
            ref int TipoLiq, ref decimal TasaMora, ref int DiasGracia, ref decimal TasaUsura,
            ref int BaseLiq, ref int CtrlConse, ref double ConseCreditos, ref string CptoRevapo,
            ref string nit, ref string Direccion, ref string nomres, ref string CptoServicios,
            ref string CptoExt, ref string CptoAho, ref string CptoApo, ref string CptoRetFte,
            ref string Undred, ref string CptoCdats, ref string CptoIntCdats, ref string nombre,
            ref string telefono, ref string Cpto4Mil, ref string CptoIntAhorro, ref string CptoAnticipo,
            ref double ConseCdat, ref double Consedep, ref int OpRecDeuda, ref string CpteAnticipo,
            ref string Ciudad, ref string GenCobroConsulta, ref double Vlr_Consulta, ref string Cpte_Consulta,
            ref string NombreResumido, ref string CptoIntAnt, ref string Cptoint, ref string CptoMor,
            ref double LimDiario_LavaActi, ref double LimMes_LavaActi, ref string ManEstudio, ref string cobracodeudor,
            ref string PorcEndeudamiento, ref string ManCaptaciones, ref string ServerSmtp, ref string PasswordEnvio,
            ref string CorreoEnvio, ref string TipoNomina, ref string CptoIntAnticipados, ref string Depto,
            ref string JefeCartera, ref string Cpto4MilCheque, ref string CpteFavor, ref char retenaux,
            ref double valor_retenaux, ref double porcen_retenaux, ref string cuenta_retenaux, ref string consecut_factura,
            ref string numcodecredit, ref string Modif_cuota, ref char conciliabanca, ref int NumPagare,
            ref string PagareNotas, ref string FormaPagare, ref string ControlaConseDep, ref string paramcausacion,
            ref string DisableTasaICdat, ref string FORAPL_CAJA, ref int ConvEnpacto, ref string feec)
        {
            bool ok = false;
            DataSet datasetBuscarCompania = new DataSet();
            ok = BuscarCompania(ref Codigo, ref datasetBuscarCompania, myconect);

            if (datasetBuscarCompania.Tables["tblcompania"].Rows.Count > 0)
            {
                DataRow row = datasetBuscarCompania.Tables["tblcompania"].Rows[0];
                CuentaUtilidad = row["CUENTACON2"].ToString();
                CptoCap = row["CPTO_CAPITAL"].ToString();
                CalculaSaldo = row["CALCULA_SALDO"].ToString();
                CptoAfavor = row["sobra"].ToString();
                TipoLiq = Convert.ToInt32(row["tipo_liquidacion"]);
                TasaMora = Convert.ToDecimal(row["Tasa_mora"]);
                DiasGracia = Convert.ToInt32(row["Dias_gracia"]);
                TasaUsura = Convert.ToDecimal(row["Tasa_usura"]);
                BaseLiq = Convert.ToInt32(row["Base_liquidacion"]);
                CtrlConse = Convert.ToInt32(row["CtrlConse"]);
                ConseCreditos = Convert.ToDouble(row["ConseCreditos"]);
                CptoRevapo = row["cptorevapo"].ToString();
                nit = row["NIT"].ToString();
                Direccion = row["Direccion"].ToString();
                nomres = row["nomres"].ToString();
                CptoServicios = row["cpto_servi"].ToString();
                CptoExt = row["Cpto_extra"].ToString();
                CptoAho = row["cpto_ahorros"].ToString();
                CptoApo = row["cpto_aportes"].ToString();
                CptoRetFte = row["cptoretfte"].ToString();
                Undred = row["UNI_RED"].ToString();
                CptoCdats = row["CptoCdats"].ToString();
                CptoIntCdats = row["Cptointcdats"].ToString();
                nombre = row["nombre"].ToString();
                telefono = row["TELEFONO"].ToString();
                Cpto4Mil = row["Cpto4Mil"].ToString();
                CptoIntAhorro = row["cpto_aporatr"].ToString();
                CptoAnticipo = row["CptoAnticipo"].ToString();
                ConseCdat = Convert.ToDouble(row["ConseCdat"]);
                Consedep = Convert.ToDouble(row["Consedep"]);
                OpRecDeuda = Convert.ToInt32(row["OpRecDeuda"]);
                CpteAnticipo = row["CpteAnticipo"].ToString();
                Ciudad = row["Ciudad"].ToString();
                GenCobroConsulta = row["GenCobroConsulta"].ToString();
                Vlr_Consulta = Convert.ToDouble(row["Vlr_Consulta"]);
                Cpte_Consulta = row["cpte_Consulta"].ToString();
                NombreResumido = row["nomres"].ToString();
                CptoIntAnt = row["cpto_inteatra"].ToString();
                Cptoint = row["CPTO_INTERES"].ToString();
                CptoMor = row["CPTO_INMO"].ToString();
                LimMes_LavaActi = Convert.ToDouble(row["lim_mes_lava_acti"]);
                LimDiario_LavaActi = Convert.ToDouble(row["lim_diario_lava_acti"]);
                ManEstudio = row["ManEstudio"].ToString();
                cobracodeudor = row["cobracodeudor"].ToString();
                PorcEndeudamiento = row["cupo"].ToString();
                ManCaptaciones = row["modulo_firmas"].ToString();
                CptoIntAnticipados = row["cpto_capiatra"].ToString();
                Depto = row["DPTO"].ToString();
                ServerSmtp = row["ServerSmtp"].ToString();
                PasswordEnvio = row["PasswordEnvio"].ToString();
                CorreoEnvio = row["CorreoEnvio"].ToString();
                TipoNomina = row["CLASE_NOMINA"].ToString();
                JefeCartera = row["JefeCartera"].ToString();
                Cpto4MilCheque = row["cpto_admatra"].ToString();
                CpteFavor = row["CpteFavor"].ToString();
                retenaux = Convert.ToChar(row["retenaux"]);
                valor_retenaux = Convert.ToDouble(row["valor_retenaux"]);
                porcen_retenaux = Convert.ToDouble(row["porcen_retenaux"]);
                cuenta_retenaux = row["cuenta_retenaux"].ToString();
                consecut_factura = row["consecut_factura"].ToString();
                numcodecredit = row["numcodecredit"].ToString();
                Modif_cuota = row["Modif_Cuota_Cred"].ToString();
                conciliabanca = Convert.ToChar(row["conciliabanca"]);
                NumPagare = Convert.ToInt32(row["numpagare"]);
                PagareNotas = row["pagarenotas"].ToString();
                FormaPagare = row["formapagare"].ToString();
                ControlaConseDep = row["controlaconsedep"].ToString();
                paramcausacion = row["paramcausacion"].ToString();
                DisableTasaICdat = row["DisableTasaICdat"].ToString();
                FORAPL_CAJA = row["FORAPL_CAJA"].ToString();
                ConvEnpacto = Convert.ToInt32(row["tipconv"]);
                feec = row["feec"].ToString();
            }

            return ok;
        }

        public virtual bool BuscarCompania(ref string Codigo, ref DataSet DsDataset, OdbcConnection myconect)
        {
            StringBuilder stbuilder = new StringBuilder();

            try
            {
                if (DsDataset.Tables.Contains("tblcompania"))
                {
                    DsDataset.Tables["tblcompania"].Rows.Clear();
                }
            }
            catch { }

            stbuilder.Append("select CUENTACON2 , CPTO_CAPITAL ,CALCULA_SALDO , sobra,tipo_liquidacion , Tasa_mora ,Dias_gracia , Tasa_usura ,   ");
            stbuilder.Append("Base_liquidacion , CtrlConse , ConseCreditos ,cptorevapo ,NIT , Direccion , nomres , cpto_servi ,Cpto_extra , cpto_ahorros ,  cpto_aportes , cptoretfte, ");
            stbuilder.Append("UNI_RED , CptoCdats ,  Cptointcdats ,nombre ,TELEFONO , Cpto4Mil  , cpto_aporatr , CptoAnticipo ,ConseCdat ,Consedep ,OpRecDeuda ,CpteAnticipo,num_solcred,  ");
            stbuilder.Append("Ciudad ,GenCobroConsulta ,Vlr_Consulta ,cpte_Consulta ,nomres , cpto_inteatra , CPTO_INTERES ,CPTO_INMO ,lim_mes_lava_acti , lim_diario_lava_acti , ManEstudio ,cobracodeudor, ");
            stbuilder.Append("cpto_seguro, cpto_admon,r_extra,r_cupo,r_plazo,r_tasa, cpto_capiatra,VEMTO_INI_01,VEMTO_FIN_01,VEMTO_INI_02,VEMTO_FIN_02,VEMTO_INI_03,VEMTO_FIN_03,VEMTO_INI_04,VEMTO_FIN_04, ");
            stbuilder.Append("VEMTO_INI_05,VEMTO_FIN_05,ajuscaapor,ajuscacapi,ajuscainte,ajuscaintemor,ajuscasegcre,ajuscaserv,ajuscaahor,ajuscaext,ajuscaadm,paquetedatos,");
            stbuilder.Append("aplisuspmora,diassuspmora,consec_lavado,retenaux,valor_retenaux,porcen_retenaux,cuenta_retenaux,consecut_factura,numcodecredit,restri_fecha_rc, Modif_Cuota_Cred,conciliabanca,");
            stbuilder.Append("calcprovaportes,numpagare,pagarenotas,formapagare,cupo,modulo_firmas,DPTO,ServerSmtp,PasswordEnvio,CorreoEnvio,CLASE_NOMINA,JefeCartera,cpto_admatra,CpteFavor,controlaconsedep,");
            stbuilder.Append("salario_minimo,UploadAsocWeb,DownloadWeb,UploadMovimtoWeb,Webservice,puertoSMTP,EnabledSSL,paramcausacion,DisableTasaICdat,FORAPL_CAJA,tipconv,REPRE,REV_FIS,MAT_REV,CONTA,MAT_CON,feec from sys_compania where CODIGO = '" + Codigo + "'");

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconect, "BuscarCompania", ref DsDataset, "tblcompania", true);
            return DsDataset.Tables["tblcompania"].Rows.Count > 0;
        }

        public bool GrabaCompania(string Codigo, OdbcConnection myconect, double ConseCreditos = 0, double ConseCdat = 0)
        {
            double Consecutivo = 0, ConsecutivoCdat = 0;
            DataSet dsCompania = new DataSet();

            _ok = BuscarCompania(ref Codigo, ref dsCompania, myconect);

            if (_ok && dsCompania.Tables["tblcompania"].Rows.Count > 0)
            {
                DataRow row = dsCompania.Tables["tblcompania"].Rows[0];
                Consecutivo = Convert.ToDouble(row["ConseCreditos"]);
                ConsecutivoCdat = Convert.ToDouble(row["ConseCdat"]);
            }

            if (_ok)
            {
                if (ConseCreditos == 0) ConseCreditos = Consecutivo;
                if (ConseCdat == 0) ConseCdat = ConsecutivoCdat;

                _stmysql = "update sys_compania set ConseCreditos = " + ConseCreditos + ", ConseCdat = " + ConseCdat + " where CODIGO = '" + Codigo + "'";
                _odbcConnect.ExecuteQueryconec(_stmysql, myconect, "GrabaCompania");
            }
            return _ok;
        }

        #endregion

        #region Metodos de Usuario

        public bool BuscaUsuario(ref string Login, OdbcConnection Conect,
            ref string Nombre, ref object CreditoMinimo, ref object CreditoMaximo,
            ref string Password, ref string Grupo, ref bool CambiaPass, ref DateTime FechaCrea,
            ref DateTime FechaVence, ref bool ValidaComprobantes, ref bool SobreGiro,
            ref object CreditoGraMinimo, ref object CreditoGraMaximo, ref string Cedula, ref bool GrabaRetirados)
        {
            return BuscaUsuario(ref Login, Conect, Navega.Ninguno,
                ref Nombre, ref CreditoMinimo, ref CreditoMaximo,
                ref Password, ref Grupo, ref CambiaPass, ref FechaCrea,
                ref FechaVence, ref ValidaComprobantes, ref SobreGiro,
                ref CreditoGraMinimo, ref CreditoGraMaximo, ref Cedula, ref GrabaRetirados);
        }

        public bool BuscaUsuario(ref string Login, OdbcConnection Conect, Navega Navegar,
            ref string Nombre, ref object CreditoMinimo, ref object CreditoMaximo,
            ref string Password, ref string Grupo, ref bool CambiaPass, ref DateTime FechaCrea,
            ref DateTime FechaVence, ref bool ValidaComprobantes, ref bool SobreGiro,
            ref object CreditoGraMinimo, ref object CreditoGraMaximo, ref string Cedula, ref bool GrabaRetirados)
        {
            string where = "";
            bool ok = false;
            string estatus = " ";
            string validaCompro = "N";
            string SobGiro = "N";
            string Retiros = "N";

            if (Navegar == Navega.Ninguno)
            {
                if (string.IsNullOrEmpty(Login) || Login == "0")
                {
                    Login = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = "from sys_sasusu where login = '" + Login.Trim().ToLower() + "'";
                    break;
                case Navega.Primero:
                    where = "from sys_sasusu where login > ' ' order by login " + varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = "from sys_sasusu where login < '" + Login.Trim().ToLower() + "' order by login desc " + varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = "from sys_sasusu where login > '" + Login.Trim().ToLower() + "' order by login " + varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = "from sys_sasusu where login <= 'ZZZZZZZZZZZZZ' order by login desc " + varini.Pstlimit;
                    break;
            }

            string sFechaVence = "", sFechaCrea = "";
            string sCreditoMin = "0", sCreditoMax = "0", sCreditoGraMin = "0", sCreditoGraMax = "0";

            _stmysql = "select " + varini.Psttop + " Password as campo1,nombre as campo2, grupo as campo3 , estatus as campo4 ";
            ok = ExecuteQueryconec(_stmysql + where, Conect, "BuscaUsuario", ref Password, ref Nombre, ref Grupo, ref estatus);
            Password = EncripDescripPass(Password, EncripDescrip.Descriptar);

            _stmysql = "select " + varini.Psttop + " fecha_vmto as campo1,fecha_crea as campo2,ValidaCompro as campo3,SobreGiro as campo4 ";
            ok = ExecuteQueryconec(_stmysql + where, Conect, "BuscaUsuario", ref sFechaVence, ref sFechaCrea, ref validaCompro, ref SobGiro);

            ValidaComprobantes = validaCompro == "Y";
            CambiaPass = estatus.ToUpper() == "I";
            SobreGiro = SobGiro == "Y";

            _stmysql = "select " + varini.Psttop + " cedula as campo1,GrabaRetirados as campo2, aprocreini as campo3, aprocrefin as campo4 ";
            ok = ExecuteQueryconec(_stmysql + where, Conect, "BuscaUsuario", ref Cedula, ref Retiros, ref sCreditoMin, ref sCreditoMax);

            _stmysql = "select " + varini.Psttop + " gracreini as campo1, gracrefin as campo2 ";
            string dummy1 = "", dummy2 = "";
            ok = ExecuteQueryconec(_stmysql + where, Conect, "BuscaUsuario", ref sCreditoGraMin, ref sCreditoGraMax, ref dummy1, ref dummy2);

            GrabaRetirados = Retiros == "Y";
            double.TryParse(sCreditoMin, out double cMin);
            double.TryParse(sCreditoMax, out double cMax);
            double.TryParse(sCreditoGraMin, out double cgMin);
            double.TryParse(sCreditoGraMax, out double cgMax);
            CreditoMinimo = cMin;
            CreditoMaximo = cMax;
            CreditoGraMinimo = cgMin;
            CreditoGraMaximo = cgMax;

            DateTime.TryParse(sFechaVence, out FechaVence);
            DateTime.TryParse(sFechaCrea, out FechaCrea);

            return ok;
        }

        public virtual bool BuscaUsuario(string login, ref DataSet dsusuario, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            try
            {
                if (dsusuario.Tables.Contains("tblusuarios"))
                {
                    dsusuario.Tables["tblusuarios"].Rows.Clear();
                }
            }
            catch { }

            StBuilder.Append("select nombre,aprocreini,aprocrefin,fecha_vmto,tip_acceso,password,fecha_crea,grupo,autocreini,autocrefin, ");
            StBuilder.Append("estatus,fec_cambio,login,grabacancelacdats,gracreini,gracrefin,SobreGiro,ValidaCompro,cedula,GrabaRetirados,modifFechaIngCoopera,grabacancelaPAP,grabafacvencidas  ");
            StBuilder.Append(" from sys_sasusu where login = '" + login + "'");

            _ok = _odbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaAsociado", ref dsusuario, "tblusuarios");
            return _ok;
        }

        public bool GrabaUsuario(string Login, OdbcConnection Connection, Acciones Accion,
            ref string Nombre, ref DateTime FechaVence, ref string TipoAcceso, ref string PassWord,
            ref string Grupo, ref string Estatus, ref DateTime FechaCrea, ref DateTime FechaCambio)
        {
            object CreditoMinimo = 0, CreditoMaximo = 0, CreditoGraMinimo = 0, CreditoGraMaximo = 0;
            string mysql = "";
            bool dummyBool = false;
            string dummyCedula = "";

            if (string.IsNullOrEmpty(Login)) return false;

            if (Accion == Acciones.Borrar)
            {
                mysql = "delete from sys_sasusu where login ='" + Login + "'";
            }
            else
            {
                if (BuscaUsuario(ref Login, Connection, Navega.Ninguno, ref Nombre, ref CreditoMinimo, ref CreditoMaximo,
                    ref PassWord, ref Grupo, ref dummyBool, ref FechaCrea, ref FechaVence, ref dummyBool, ref dummyBool,
                    ref CreditoGraMinimo, ref CreditoGraMaximo, ref dummyCedula, ref dummyBool))
                {
                    mysql = "update sys_sasusu set " +
                        (!string.IsNullOrEmpty(Nombre.Trim()) ? " nombre = '" + Nombre.Trim() + "'," : "") +
                        " aprocreini = '" + CreditoMinimo + "', aprocrefin  = '" + CreditoMaximo + "'," +
                        " gracreini = '" + CreditoGraMinimo + "', gracrefin = '" + CreditoGraMaximo + "'," +
                        (FechaVence != new DateTime(1950, 1, 1) ? " fecha_vmto = '" + Strings.Format(FechaVence, varini.PstForFec) + "'," : "") +
                        (FechaCambio != new DateTime(1950, 1, 1) ? " fec_cambio = '" + Strings.Format(FechaCambio, varini.PstForFec) + "'," : "") +
                        (!string.IsNullOrEmpty(PassWord.Trim()) ? " password = '" + PassWord + "', " : "") +
                        (FechaCrea != new DateTime(1950, 1, 1) ? " fecha_crea = '" + Strings.Format(FechaCrea, varini.PstForFec) + "', " : "") +
                        (!string.IsNullOrEmpty(Grupo.Trim()) ? " grupo = '" + Grupo + "', " : "") +
                        (!string.IsNullOrEmpty(Estatus.Trim()) ? " estatus = '" + Estatus + "', " : "") +
                        " tip_acceso = '" + TipoAcceso + "' " +
                        " where login = '" + Login + "'";
                }
                else
                {
                    mysql = "insert into sys_sasusu (login,nombre, aprocreini,aprocrefin ,gracreini, gracrefin,fecha_vmto, " +
                        "tip_acceso,password,fecha_crea,grupo,estatus,fec_cambio) values('" +
                        Login + "','" + Nombre + "','" + CreditoMinimo + "','" + CreditoMaximo + "','" +
                        CreditoGraMinimo + "','" + CreditoGraMaximo + "','" +
                        Strings.Format(FechaVence, varini.PstForFec) + "','" + TipoAcceso + "','" +
                        PassWord + "','" + Strings.Format(FechaCrea, varini.PstForFec) + "','" +
                        Grupo + "','" + Estatus + "','" + Strings.Format(FechaCambio, varini.PstForFec) + "')";
                }
            }

            if (!string.IsNullOrEmpty(mysql))
            {
                return _odbcConnect.ExecuteQueryconec(mysql, Connection, "GrabarUsuario");
            }
            return false;
        }

        #endregion

        #region Metodos de Encriptacion

        public string EncripDescripPass(string Password, EncripDescrip Opcion)
        {
            char[] pass = Password.ToCharArray();
            string pas = "";

            switch (Opcion)
            {
                case EncripDescrip.Encriptar:
                    for (int i = 0; i < pass.Length; i++)
                    {
                        pas += (char)(pass[i] + 54);
                        pas += (char)(pass[i] + 34 + i);
                    }
                    break;
                case EncripDescrip.Descriptar:
                    for (int i = 0; i < pass.Length; i += 2)
                    {
                        pas += (char)(pass[i] - 54);
                    }
                    break;
            }
            return pas;
        }

        #endregion

        #region Metodos de Periodo

        public void buscaPeriodo(string Modulo, OdbcConnection myconnet, ref DateTime FechaIni, ref DateTime fechaFin,
            DateTime fechaValidar, ref string estado, ref string Periodo, string ano = "9999")
        {
            string fechaInicial = "0", fechaFinal = "0", estadoFecha = "A";
            string AnoActual = ano == "9999" ? DateTime.Now.ToString("yyyy") : ano;
            int Anoperiodo = 0;
            string MesPeriodo = "0";

            _stmysql = "select anio as campo1, periodo as campo2 from sys_periodo where modulo = '" + Modulo + "' and anio = '" + AnoActual + "'";
            string sAno = "0", sPeriodo = "0", dummy1 = "", dummy2 = "";
            if (!_odbcConnect.ExecuteQueryconec(_stmysql, myconnet, "buscaPeriodo", ref sAno, ref sPeriodo, ref dummy1, ref dummy2))
            {
                MessageBox.Show("El periodo actual no esta parametrizado.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int.TryParse(sAno, out Anoperiodo);
            MesPeriodo = sPeriodo;

            if (Periodo != "999999" && !string.IsNullOrEmpty(Periodo))
            {
                DateTime.TryParse(Periodo.Substring(0, 4) + "/" + Periodo.Substring(4, 2) + "/01", out fechaValidar);
            }

            if (fechaValidar == new DateTime(1950, 1, 1))
            {
                Periodo = Anoperiodo.ToString() + Strings.Right("00" + MesPeriodo, 2);
                DateTime.TryParse(Periodo.Substring(0, 4) + "/" + Periodo.Substring(4, 2) + "/01", out fechaValidar);
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
                Periodo = fechaValidar.ToString("yyyyMM");
                _stmysql = "select " + fechaInicial + " as campo1," + fechaFinal + " as campo2," + estadoFecha + " as campo3 from sys_periodo where modulo = '" + Modulo + "' and anio = '" + AnoActual + "'";
                string sFecIni = "", sFecFin = "";
                _odbcConnect.ExecuteQueryconec(_stmysql, myconnet, "buscaPeriodo", ref sFecIni, ref sFecFin, ref estado, ref dummy1);
                DateTime.TryParse(sFecIni, out FechaIni);
                DateTime.TryParse(sFecFin, out fechaFin);
            }
            else
            {
                Periodo = Anoperiodo.ToString() + MesPeriodo;
            }
        }

        #endregion

        #region Metodos de Comprobante

        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string ControlConse, ref string TipoDoc, ref double debitos, ref double creditos, ref string CERRADO,
            ref string ANULADO, ref double Diferencia, ref string Detalle, ref string Doctipo, ref string NODOC,
            ref DateTime FechaMovto, ref string Nombre, ref string Idbenef, ref string CencCpte, ref string Cuenta,
            ref string AFECTA_3XMIL, ref string validadora, ref string FormaImp, ref string NumCopiasImp)
        {
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref ControlConse, ref TipoDoc, ref debitos, ref creditos, ref CERRADO,
                ref ANULADO, ref Diferencia, ref Detalle, ref Doctipo, ref NODOC,
                ref FechaMovto, ref Nombre, ref Idbenef, ref CencCpte, ref Cuenta,
                ref AFECTA_3XMIL, ref validadora, ref FormaImp, "Y", ref NumCopiasImp);
        }

        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string ControlConse, ref string TipoDoc, ref double debitos, ref double creditos, ref string CERRADO,
            ref string ANULADO, ref double Diferencia, ref string Detalle, ref string Doctipo, ref string NODOC,
            ref DateTime FechaMovto, ref string Nombre, ref string Idbenef, ref string CencCpte, ref string Cuenta,
            ref string AFECTA_3XMIL, ref string validadora, ref string FormaImp, string Actualizaconcep, ref string NumCopiasImp)
        {
            bool ok = false;
            int Num_consecu = 0;
            Comprobante = Strings.Right("0000" + Comprobante, 4);

            if (Consecutivo == 0 || Consecutivo == 9999)
            {
                string sNumConsecu = "0";
                _stmysql = "select control_consec as campo1, num_consecu as campo2, DOCUMENTO as campo3, nombre as campo4 from sys_compro02 where codigo = '" + Comprobante + "'";
                ok = ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref ControlConse, ref sNumConsecu, ref TipoDoc, ref Nombre);
                int.TryParse(sNumConsecu, out Num_consecu);

                string dummy1 = "", dummy2 = "", dummy3 = "";
                _stmysql = "select CUENTA_CONTABLE as campo1 from sys_compro02 where codigo = '" + Comprobante + "'";
                ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref Cuenta, ref dummy1, ref dummy2, ref dummy3);

                Num_consecu++;

                if (ActualizaConse)
                {
                    _stmysql = "Update sys_compro02 set num_consecu = " + Num_consecu + " where codigo = '" + Comprobante + "'";
                    ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante");
                }

                if (Actualizaconcep == "Y")
                {
                    Consecutivo = Num_consecu;
                }

                return ok;
            }
            else
            {
                string sNumConsecu = "0";
                _stmysql = "select control_consec as campo1, num_consecu as campo2, DOCUMENTO as campo3, nombre as campo4 from sys_compro02 where codigo = '" + Comprobante + "'";
                ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref ControlConse, ref sNumConsecu, ref TipoDoc, ref Nombre);

                _stmysql = "select CENCOSTO as campo1,AFECTA_3XMIL as campo2 ,validadora as campo3,forma_imprimir as campo4 from sys_compro02 where codigo = '" + Comprobante + "'";
                ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref CencCpte, ref AFECTA_3XMIL, ref validadora, ref FormaImp);

                string sDebitos = "0", sCreditos = "0";
                _stmysql = "select compronte, debito as campo1, credito as campo2,CERRADO as campo3,ANULADO as campo4 from cnt_docmto where COMPRONTE = '" + Comprobante + "' and NUMERO = " + Consecutivo;
                ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref sDebitos, ref sCreditos, ref CERRADO, ref ANULADO);
                double.TryParse(sDebitos, out debitos);
                double.TryParse(sCreditos, out creditos);

                string sFechaMovto = "";
                _stmysql = "select detalle as campo1, doctipo as campo2, nodoc as campo3,fecha as campo4 from cnt_docmto where COMPRONTE = '" + Comprobante + "' and NUMERO = " + Consecutivo;
                ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref Detalle, ref Doctipo, ref NODOC, ref sFechaMovto);
                DateTime.TryParse(sFechaMovto, out FechaMovto);

                string dummy1 = "", dummy2 = "", dummy3 = "";
                _stmysql = "select PUERT_VALIDADORA as campo1 from sys_compro02 where codigo = '" + Comprobante + "'";
                ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref NumCopiasImp, ref dummy1, ref dummy2, ref dummy3);

                _stmysql = "select Idbenef as campo1 from cnt_docmto where COMPRONTE = '" + Comprobante + "' and NUMERO = " + Consecutivo;
                ok = _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "BuscaComprobante", ref Idbenef, ref dummy1, ref dummy2, ref dummy3);

                Diferencia = Math.Round(creditos - debitos, 2);
                return ok;
            }
        }

        #endregion

        #region Metodos de Excel

        public void DataGridViewToExcel(DataGridView pDataGridView, bool SoloVisibles = false)
        {
            string vFileName = Path.GetTempFileName();
            FileSystem.FileOpen(1, vFileName, OpenMode.Output);
            string sb = "";

            foreach (DataGridViewColumn dc in pDataGridView.Columns)
            {
                if (SoloVisibles)
                {
                    if (dc.Visible) sb += dc.HeaderText + "\t";
                }
                else
                {
                    sb += dc.HeaderText + "\t";
                }
            }
            FileSystem.PrintLine(1, sb);

            foreach (DataGridViewRow dr in pDataGridView.Rows)
            {
                int i = 0;
                sb = "";
                foreach (DataGridViewColumn dc in pDataGridView.Columns)
                {
                    if (!Information.IsDBNull(dr.Cells[i].Value))
                    {
                        if (SoloVisibles)
                        {
                            if (dc.Visible) sb += dr.Cells[i].Value.ToString() + "\t";
                        }
                        else
                        {
                            sb += dr.Cells[i].Value.ToString() + "\t";
                        }
                    }
                    else
                    {
                        sb += "\t";
                    }
                    i++;
                }
                FileSystem.PrintLine(1, sb);
            }
            FileSystem.FileClose(1);
            TextToExcel(ref vFileName);
        }

        public string DataTableToExcel(DataTable pDataTable, bool ActivaExcel = true)
        {
            string vFileName = Path.GetTempFileName();
            FileSystem.FileOpen(1, vFileName, OpenMode.Output);
            string sb = "";

            foreach (DataColumn dc in pDataTable.Columns)
            {
                sb += dc.Caption.ToLower() + "\t";
            }
            FileSystem.PrintLine(1, sb);

            foreach (DataRow dr in pDataTable.Rows)
            {
                int i = 0;
                sb = "";
                foreach (DataColumn dc in pDataTable.Columns)
                {
                    if (!Information.IsDBNull(dr[i]))
                    {
                        sb += dr[i].ToString() + "\t";
                    }
                    else
                    {
                        sb += "\t";
                    }
                    i++;
                }
                FileSystem.PrintLine(1, sb);
            }
            FileSystem.FileClose(1);

            if (ActivaExcel) TextToExcel(ref vFileName);
            return vFileName;
        }

        public void TextToExcel(ref string pFileName)
        {
#if EXCEL_LEGACY
            try
            {
                CultureInfo vCultura = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("ES-ES");

                Excel.Application Exc = new Excel.Application();
                Exc.Workbooks.OpenText(pFileName, Type.Missing, Type.Missing, Type.Missing,
                    Excel.XlTextQualifier.xlTextQualifierNone, Type.Missing, true);

                Excel.Workbook Wb = Exc.ActiveWorkbook;
                Excel.Worksheet Ws = (Excel.Worksheet)Wb.ActiveSheet;

                Excel.XlRangeAutoFormat vFormato = Excel.XlRangeAutoFormat.xlRangeAutoFormatSimple;
                Ws.Range[Ws.Cells[1, 1], Ws.Cells[Ws.UsedRange.Rows.Count, Ws.UsedRange.Columns.Count]].AutoFormat(vFormato);

                pFileName = Path.GetTempFileName().Replace("tmp", "xls");
                File.Delete(pFileName);
                Exc.ActiveWorkbook.SaveAs(pFileName, (int)Excel.XlTextQualifier.xlTextQualifierNone - 1);

                Exc.Quit();
                Ws = null;
                Wb = null;
                Exc = null;
                GC.Collect();

                pFileName = "\"" + pFileName + "\"";
                Process p = new Process();
                p.EnableRaisingEvents = false;

                if (ERP.Core.Compartido.Datos.ClsConect.odbcConect.openoficce)
                {
                    Process.Start("scalc.exe", pFileName);
                }
                else
                {
                    Process.Start(pFileName);
                }

                Thread.CurrentThread.CurrentCulture = vCultura;
            }
            catch
            {
                // Error handling
            }
#else
            throw new NotSupportedException("Excel COM Interop no disponible. Use IExcelExportService.");
#endif
        }

        #endregion

        #region Metodos auxiliares

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            return _odbcConnect.ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref Campo3, ref Campo4);
        }

        public void ConfiguraForma(Form Forma, ref string Empresa, ref string Servidor, ref string Bd,
            ref string Nomforma, ref string Usuario, ref string Fecha)
        {
            _odbcConnect.LlenarVarini(ref varini);
            Empresa = varini.pstEmpresa;
            Servidor = varini.pstServer;
            Bd = varini.pstBdatos;
            Nomforma = Forma.Name;
            Usuario = varini.pstUsuario;
            Fecha = Strings.Format(DateTime.Now, varini.PstForFec);
        }

        public decimal BuscaIntAhorros(string Cuenta, OdbcConnection myconnect)
        {
            decimal tasa = 0;
            string sTasa = "0", dummy1 = "", dummy2 = "", dummy3 = "";
            _stmysql = "select tasa_interes as campo1 from dep_ahor29 where cuenta = '" + Cuenta + "'";
            _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "BuscaIntAhorros", ref sTasa, ref dummy1, ref dummy2, ref dummy3);
            decimal.TryParse(sTasa, out tasa);
            return tasa;
        }

        #endregion

        #region Metodos de validacion de email

        public bool validaEmails(string email)
        {
            string[] Destinatarios = email.Split(';');
            bool Validar = false;

            if (Destinatarios.Length > 0)
            {
                for (int i = 0; i < Destinatarios.Length; i++)
                {
                    Validar = validar_Mail(Destinatarios[i]);
                }
            }
            return Validar;
        }

        private bool validar_Mail(string sMail)
        {
            return Regex.IsMatch(sMail,
                @"^[a-zA-Z][\w\.-]*[a-zA-Z0-9]@[a-zA-Z][\w\.-]*[a-zA-Z0-9]\.[a-zA-Z][a-zA-Z\.]*[a-zA-Z]$");
        }

        #endregion

        #region Metodos de conversion financiera

        public double Conversion_TasaFinanciera(FormaliquidarFinanciera formaLiquidar, double tasa_interes, int diasLiquidar = 1, string modalidadInt = "1")
        {
            double TasaConvertida = 0;
            double tasa;

            switch (formaLiquidar)
            {
                case FormaliquidarFinanciera.TasaNominalAnual:
                    TasaConvertida = Math.Round((tasa_interes / 360) / 100, 9);
                    break;

                case FormaliquidarFinanciera.TasaEfectivaAnual:
                    if (diasLiquidar > 0)
                    {
                        tasa = Math.Round(Math.Pow(1 + (tasa_interes / 100), 1.0 / (360.0 / diasLiquidar)) - 1, 9);
                        TasaConvertida = tasa;
                        switch (modalidadInt)
                        {
                            case "2":
                                TasaConvertida = tasa / (1 + tasa);
                                break;
                        }
                    }
                    else
                    {
                        TasaConvertida = 0;
                    }
                    break;

                case FormaliquidarFinanciera.TasaNominalAnual_Efectiva:
                    if (diasLiquidar > 0)
                    {
                        TasaConvertida = Math.Round(Math.Pow(1 + (tasa_interes / 100), 360.0 / diasLiquidar) - 1, 9);
                    }
                    else
                    {
                        TasaConvertida = 0;
                    }
                    break;
            }

            return TasaConvertida;
        }

        #endregion

        #region Metodos de carga XML y ComboBox

        public DataSet CargarXmlaDataset(string NombreArchivo)
        {
            DataSet dsdataset = new DataSet();
            string path = Path.Combine(Path.Combine(Application.StartupPath, "plugins"), NombreArchivo + ".xml");

            if (File.Exists(path))
            {
                dsdataset.ReadXml(path);
            }

            return dsdataset;
        }

        public double ObtieneValorComboBox(ComboBox CuadroLista)
        {
            object value = CuadroLista.SelectedValue;

            if (value is DataRowView)
                return 0;

            return Convert.ToDouble(value);
        }

        public void CargaDatasetComboBox(DataTable dsdatatable, string ValueMiembro, string DisplayMiembro, ref ComboBox ObjCombo)
        {
            ObjCombo.DataSource = dsdatatable;
            ObjCombo.DisplayMember = DisplayMiembro;
            ObjCombo.ValueMember = ValueMiembro;
        }

        #endregion

        #region Metodos LlenaAutocomplete

        public bool LlenaAutocompleteCuentas(OdbcConnection myconnect, ref DataTable TblCuentas, string Filtro = null)
        {
            if (!string.IsNullOrEmpty(Filtro))
            {
                Filtro = " where " + Filtro;
            }
            if (!_odbcConnect.ExecuteConsulta("SELECT cuenta, nombre FROM CNT_MAECUEN" + Filtro, myconnect, "LlenaAutocompleteCuentas", ref TblCuentas))
            {
                TblCuentas = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteComprobantes(OdbcConnection myconnect, ref DataTable TblComprobantes, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("select codigo, nombre from sys_compro02" + Filtro, myconnect, "LlenaAutocompleteComprobantes", ref TblComprobantes))
            {
                TblComprobantes = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteBancos(OdbcConnection myconnect, ref DataTable TblBancos, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("select codigo_banco, nombre from sys_banco03" + Filtro, myconnect, "LlenaAutocompleteBancos", ref TblBancos))
            {
                TblBancos = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteAgencias(OdbcConnection myconnect, ref DataTable TblAgencias, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("select codigo, nombre from sys_agencia" + Filtro, myconnect, "LlenaAutocompleteAgencias", ref TblAgencias))
            {
                TblAgencias = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteCencosCon(OdbcConnection myconnect, ref DataTable TblCencos, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("select ccosto, nombre from cnt_cencos" + Filtro, myconnect, "LlenaAutocompleteCencosCon", ref TblCencos))
            {
                TblCencos = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteCiudad(OdbcConnection myconnect, ref DataTable TblCiudad, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("select ciudad, nombre_ciudad, dpto from sys_ciudad57" + Filtro, myconnect, "LlenaAutocompleteCiudad", ref TblCiudad))
            {
                TblCiudad = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteUsuarios(OdbcConnection myconnect, ref DataTable TblUsuarios, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("select login, nombre from sys_sasusu" + Filtro, myconnect, "LlenaAutocompleteUsuarios", ref TblUsuarios))
            {
                TblUsuarios = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteTerceros(OdbcConnection myconnect, ref DataTable TblTerceros, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("SELECT nit, nombre FROM CNT_NIT" + Filtro, myconnect, "LlenaAutocompleteTerceros", ref TblTerceros))
            {
                TblTerceros = null;
                return false;
            }
            return true;
        }

        public bool LlenaAutocompleteMaenit(OdbcConnection myconnect, ref DataTable TblMaenit, string Filtro = null)
        {
            if (!_odbcConnect.ExecuteConsulta("SELECT codigoter, nombre, apellido, nit FROM sys_maenit" + Filtro, myconnect, "LlenaAutocompleteMaenit", ref TblMaenit))
            {
                TblMaenit = null;
                return false;
            }
            return true;
        }

        #endregion

        #region Metodos de Profesiones

        public void GrabaProfesiones(int idProfesion, string nombre, string nomres, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            _ok = BuscaProfesiones(idProfesion, myconnect);

            if (!_ok)
            {
                stbuilder.Append("insert into sys_profes52 (codigo, nombre, nomres) values('" + idProfesion + "','" + nombre + "','" + nomres + "')");
            }
            else
            {
                stbuilder.Append("update sys_profes52 set nombre = '" + nombre + "', nomres = '" + nomres + "' where codigo = '" + idProfesion + "'");
            }

            _odbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaProfesiones");
        }

        public bool BuscaProfesiones(int idProfesion, OdbcConnection myconnect, ref string nombre)
        {
            string dummy1 = "", dummy2 = "", dummy3 = "";
            _stmysql = "select nombre as campo1 from sys_profes52 where codigo = '" + idProfesion + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "BuscaProfesiones", ref nombre, ref dummy1, ref dummy2, ref dummy3);
            return _ok;
        }

        public bool BuscaProfesiones(int idProfesion, OdbcConnection myconnect)
        {
            string nombre = "";
            return BuscaProfesiones(idProfesion, myconnect, ref nombre);
        }

        public bool EliminaProfesiones(int idProfesion, OdbcConnection myconnect)
        {
            _stmysql = "delete from sys_profes52 where codigo = '" + idProfesion + "'";
            return _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminaProfesiones");
        }

        public string HelpProfesiones(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_profes52", "codigo", "nombre", "nomres", Myconnect, Myforma, "Nombre", "Nombre Resumido", null, null, ref respCampo);
        }

        #endregion

        #region Metodos de Ciudades

        public bool BuscaCiudades(ref string codigo, OdbcConnection myconnect,
            ref string nombre, ref string departamento)
        {
            return BuscaCiudades(ref codigo, myconnect, Navega.Ninguno, ref nombre, ref departamento);
        }

        public bool BuscaCiudades(ref string codigo, OdbcConnection myconnect, Navega Navegar,
            ref string nombre, ref string departamento)
        {
            string where = "";
            codigo = Strings.Right("0000" + codigo, 4);

            if (Navegar == Navega.Ninguno)
            {
                if (codigo == "0000")
                {
                    codigo = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from sys_ciudad57 where ciudad = '" + codigo + "'";
                    break;
                case Navega.Primero:
                    where = " from sys_ciudad57 where ciudad > ' ' order by ciudad " + varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from sys_ciudad57 where ciudad < '" + codigo + "' order by ciudad desc " + varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from sys_ciudad57 where ciudad > '" + codigo + "' order by ciudad " + varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from sys_ciudad57 where ciudad <= '9999' order by ciudad desc " + varini.Pstlimit;
                    break;
            }

            string dummy = "";
            _stmysql = "select " + varini.Psttop + " nombre_ciudad as campo1, dpto as campo2, ciudad as campo3 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscaCiudades", ref nombre, ref departamento, ref codigo, ref dummy);
            return _ok;
        }

        public bool GrabarCiudad(string codigo, string nombre, string departamento, OdbcConnection myconnect)
        {
            codigo = Strings.Right("0000" + codigo, 4);
            string nombreDummy = "", dptoDummy = "";

            if (BuscaCiudades(ref codigo, myconnect, Navega.Ninguno, ref nombreDummy, ref dptoDummy))
            {
                _stmysql = "update sys_ciudad57 set nombre_ciudad = '" + nombre + "', dpto = '" + departamento + "' where ciudad = '" + codigo + "'";
            }
            else
            {
                _stmysql = "insert into sys_ciudad57 (ciudad, nombre_ciudad, dpto) values('" + codigo + "','" + nombre + "','" + departamento + "')";
            }

            return _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarCiudad");
        }

        public bool EliminarCiudad(string codigo, OdbcConnection myconnect)
        {
            _stmysql = "delete from sys_ciudad57 where ciudad = '" + codigo + "'";
            return _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminarCiudad");
        }

        public string Helpciudades(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_ciudad57", "ciudad", "nombre_ciudad", "dpto", Myconnect, Myforma, "Nombre", "Nombre Resumido", null, null, ref respCampo);
        }

        #endregion

        #region Metodos Help

        public string HelpSolicitudes(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("cop_solcre", "numero", "codigoter", "fecha_soli", Myconnect, Myforma, "Nombre", "Nombre Resumido", null, null, ref respCampo);
        }

        public string HelpSecciones(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_seccion54", "codigo", "nombre", "nomres", Myconnect, Myforma, "Nombre", "Nombre Resumido", null, null, ref respCampo);
        }

        public string HelpComprobantes(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_compro02", "codigo", "nombre", "nomres", Myconnect, Myforma, "Nombre", "Nombre Resumido", null, null, ref respCampo);
        }

        public string HelpBancos(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_banco03", "codigo_banco", "nombre", "nomres", Myconnect, Myforma, "Nombre", "Nombre Resumido", null, null, ref respCampo);
        }

        public string HelpCuentaContable(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("cnt_maecuen", "cuenta", "nombre", "nombre", Myconnect, Myforma, "Nombre", "Nombre Resumido", null, null, ref respCampo);
        }

        #endregion

        #region Metodos de DataTable a Excel con titulos

        public string DataTableToExcel(DataTable pDataTable, string[] Titulos, bool ActivaExcel = true)
        {
            string vFileName = Path.GetTempFileName();
            FileSystem.FileOpen(1, vFileName, OpenMode.Output);

            // Escribir titulos
            string sb = "";
            foreach (string titulo in Titulos)
            {
                sb += titulo + "\t";
            }
            FileSystem.PrintLine(1, sb);

            // Escribir encabezados
            sb = "";
            foreach (DataColumn dc in pDataTable.Columns)
            {
                sb += dc.Caption.ToLower() + "\t";
            }
            FileSystem.PrintLine(1, sb);

            // Escribir datos
            foreach (DataRow dr in pDataTable.Rows)
            {
                int i = 0;
                sb = "";
                foreach (DataColumn dc in pDataTable.Columns)
                {
                    if (!Information.IsDBNull(dr[i]))
                    {
                        sb += dr[i].ToString() + "\t";
                    }
                    else
                    {
                        sb += "\t";
                    }
                    i++;
                }
                FileSystem.PrintLine(1, sb);
            }
            FileSystem.FileClose(1);

            if (ActivaExcel) TextToExcel(ref vFileName);
            return vFileName;
        }

        #endregion

        #region Metodos de envio de correo

        public bool EnviarCorreoSolido(string ServerSmtp, string CorreoEnvio, string PasswordEnvio,
            string Destinatario, string Asunto, string Cuerpo, int Puerto = 587, bool EnabledSSL = true,
            string ArchivoAdjunto = "")
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(CorreoEnvio);
                mail.To.Add(Destinatario);
                mail.Subject = Asunto;
                mail.Body = Cuerpo;
                mail.IsBodyHtml = true;

                if (!string.IsNullOrEmpty(ArchivoAdjunto) && File.Exists(ArchivoAdjunto))
                {
                    mail.Attachments.Add(new Attachment(ArchivoAdjunto));
                }

                SmtpClient smtp = new SmtpClient(ServerSmtp, Puerto);
                smtp.Credentials = new NetworkCredential(CorreoEnvio, PasswordEnvio);
                smtp.EnableSsl = EnabledSSL;
                smtp.Send(mail);

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Metodos de calculo de dias

        public int CalculaDias(DateTime FechaInicial, DateTime FechaFinal)
        {
            TimeSpan diferencia = FechaFinal.Subtract(FechaInicial);
            return diferencia.Days;
        }

        #endregion

        #region Metodos de separar nombres

        public void SepararNombres(string NombreCompleto, ref string PrimerNombre, ref string SegundoNombre,
            ref string PrimerApellido, ref string SegundoApellido)
        {
            string[] partes = NombreCompleto.Trim().Split(' ');

            PrimerNombre = "";
            SegundoNombre = "";
            PrimerApellido = "";
            SegundoApellido = "";

            if (partes.Length >= 1) PrimerApellido = partes[0];
            if (partes.Length >= 2) SegundoApellido = partes[1];
            if (partes.Length >= 3) PrimerNombre = partes[2];
            if (partes.Length >= 4) SegundoNombre = partes[3];
        }

        #endregion

        #region Metodos de Archivos e Imagenes

        public string BuscaArchivo(string Filter, string title = " ")
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            openDlg.Title = title;
            if (openDlg.ShowDialog() == DialogResult.OK)
            {
                return openDlg.FileName;
            }
            else
            {
                return null;
            }
        }

        public Bitmap CargarImagen(int Num_cargo, OdbcConnection Conect, string empresa,
            ref string nombre, ref string Nom_cargo, ref string firma)
        {
            string cargo = "";

            switch (Num_cargo)
            {
                case 1:
                    firma = "firma_repre";
                    cargo = firma + ", REPRE";
                    break;
                case 2:
                    firma = "firma_conta";
                    cargo = firma + ", CONTA";
                    break;
                case 3:
                    firma = "firma_revi";
                    cargo = firma + ", REV_FIS";
                    break;
                case 4:
                    firma = "firma_cart";
                    cargo = firma + ", JefeCartera";
                    break;
                case 5:
                    firma = "firma_otros";
                    cargo = firma + ", nombre_otros, cargo_otros";
                    break;
            }

            try
            {
                OdbcCommand cmdFoto = new OdbcCommand("select " + cargo + " from sys_compania where codigo = '" + empresa + "'");
                cmdFoto.Connection = Conect;
                cmdFoto.CommandType = CommandType.Text;
                OdbcDataAdapter daFoto = new OdbcDataAdapter(cmdFoto);
                DataSet dsFoto = new DataSet();
                daFoto.Fill(dsFoto);

                nombre = dsFoto.Tables[0].Rows[0][1].ToString();
                if (Num_cargo == 5)
                {
                    Nom_cargo = dsFoto.Tables[0].Rows[0][2].ToString();
                }
                byte[] bits = (byte[])dsFoto.Tables[0].Rows[0][0];

                MemoryStream memorybits = new MemoryStream(bits);
                Bitmap bitmap = new Bitmap(memorybits);
                dsFoto.Dispose();
                cmdFoto.Dispose();
                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void GrabarImagenFirma(string empresa, string coluncargo, string photoFilePath, OdbcConnection Conect)
        {
            if (photoFilePath != null)
            {
                photoFilePath = photoFilePath.Trim();
                if (File.Exists(photoFilePath))
                {
                    byte[] firma = GetPhoto(photoFilePath);
                    OdbcCommand addEmp = new OdbcCommand("update sys_compania set " + coluncargo + " = " +
                        "? where codigo = '" + empresa + "'", Conect);
                    addEmp.Parameters.Add("@" + coluncargo, OdbcType.Image, firma.Length).Value = firma;
                    try
                    {
                        addEmp.ExecuteNonQuery();
                        MessageBox.Show("Carge de firma se realizo correctamente", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("El nombre de archivo es invalido.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        public bool EliminarImagenFirma(string empresa, string coluncargo, OdbcConnection Conect)
        {
            _stmysql = "update sys_compania set " + coluncargo + " =  null where codigo = '" + empresa + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, Conect, "EliminarImagenFirma");
            return _ok;
        }

        public byte[] GetPhoto(string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            byte[] photo = br.ReadBytes((int)fs.Length);
            br.Close();
            fs.Close();
            return photo;
        }

        #endregion

        #region Metodos de llamar calculadora

        public void LlamarCalculadora()
        {
            Process.Start("calc.exe");
        }

        #endregion

        #region Overloads sin ref para compatibilidad VB.NET 3.5 → C# 13

        public virtual bool BuscarCompania(string Codigo, DataSet DsDataset, OdbcConnection myconect)
        {
            return BuscarCompania(ref Codigo, ref DsDataset, myconect);
        }

        public bool BuscaUsuario(string Login, OdbcConnection Conect,
            ref string Nombre, ref object CreditoMinimo, ref object CreditoMaximo,
            ref string Password, ref string Grupo, ref bool CambiaPass, ref DateTime FechaCrea,
            ref DateTime FechaVence, ref bool ValidaComprobantes, ref bool SobreGiro,
            ref object CreditoGraMinimo, ref object CreditoGraMaximo, ref string Cedula, ref bool GrabaRetirados)
        {
            return BuscaUsuario(ref Login, Conect,
                ref Nombre, ref CreditoMinimo, ref CreditoMaximo,
                ref Password, ref Grupo, ref CambiaPass, ref FechaCrea,
                ref FechaVence, ref ValidaComprobantes, ref SobreGiro,
                ref CreditoGraMinimo, ref CreditoGraMaximo, ref Cedula, ref GrabaRetirados);
        }

        public bool BuscaUsuario(string Login, OdbcConnection Conect, Navega Navegar,
            ref string Nombre, ref object CreditoMinimo, ref object CreditoMaximo,
            ref string Password, ref string Grupo, ref bool CambiaPass, ref DateTime FechaCrea,
            ref DateTime FechaVence, ref bool ValidaComprobantes, ref bool SobreGiro,
            ref object CreditoGraMinimo, ref object CreditoGraMaximo, ref string Cedula, ref bool GrabaRetirados)
        {
            return BuscaUsuario(ref Login, Conect, Navegar,
                ref Nombre, ref CreditoMinimo, ref CreditoMaximo,
                ref Password, ref Grupo, ref CambiaPass, ref FechaCrea,
                ref FechaVence, ref ValidaComprobantes, ref SobreGiro,
                ref CreditoGraMinimo, ref CreditoGraMaximo, ref Cedula, ref GrabaRetirados);
        }

        public virtual bool BuscaUsuario(string login, DataSet dsusuario, OdbcConnection myconnect)
        {
            return BuscaUsuario(login, ref dsusuario, myconnect);
        }

        public bool BuscaComprobante(string Comprobante, double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string ControlConse, ref string TipoDoc, ref double debitos, ref double creditos, ref string CERRADO,
            ref string ANULADO, ref double Diferencia, ref string Detalle, ref string Doctipo, ref string NODOC,
            ref DateTime FechaMovto, ref string Nombre, ref string Idbenef, ref string CencCpte, ref string Cuenta,
            ref string AFECTA_3XMIL, ref string validadora, ref string FormaImp, ref string NumCopiasImp)
        {
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref ControlConse, ref TipoDoc, ref debitos, ref creditos, ref CERRADO,
                ref ANULADO, ref Diferencia, ref Detalle, ref Doctipo, ref NODOC,
                ref FechaMovto, ref Nombre, ref Idbenef, ref CencCpte, ref Cuenta,
                ref AFECTA_3XMIL, ref validadora, ref FormaImp, ref NumCopiasImp);
        }

        public void buscaPeriodo(string Modulo, OdbcConnection myconnet, ref DateTime FechaIni, ref DateTime fechaFin,
            DateTime fechaValidar, string estado, string Periodo, string ano = "9999")
        {
            buscaPeriodo(Modulo, myconnet, ref FechaIni, ref fechaFin, fechaValidar, ref estado, ref Periodo, ano);
        }

        public void CargaDatasetComboBox(DataTable dsdatatable, string ValueMiembro, string DisplayMiembro, ComboBox ObjCombo)
        {
            CargaDatasetComboBox(dsdatatable, ValueMiembro, DisplayMiembro, ref ObjCombo);
        }

        public bool LlenaAutocompleteCuentas(OdbcConnection myconnect, DataTable TblCuentas, string Filtro = null)
        { return LlenaAutocompleteCuentas(myconnect, ref TblCuentas, Filtro); }

        public bool LlenaAutocompleteComprobantes(OdbcConnection myconnect, DataTable TblComprobantes, string Filtro = null)
        { return LlenaAutocompleteComprobantes(myconnect, ref TblComprobantes, Filtro); }

        public bool LlenaAutocompleteBancos(OdbcConnection myconnect, DataTable TblBancos, string Filtro = null)
        { return LlenaAutocompleteBancos(myconnect, ref TblBancos, Filtro); }

        public bool LlenaAutocompleteAgencias(OdbcConnection myconnect, DataTable TblAgencias, string Filtro = null)
        { return LlenaAutocompleteAgencias(myconnect, ref TblAgencias, Filtro); }

        public bool LlenaAutocompleteCencosCon(OdbcConnection myconnect, DataTable TblCencos, string Filtro = null)
        { return LlenaAutocompleteCencosCon(myconnect, ref TblCencos, Filtro); }

        public bool LlenaAutocompleteCiudad(OdbcConnection myconnect, DataTable TblCiudad, string Filtro = null)
        { return LlenaAutocompleteCiudad(myconnect, ref TblCiudad, Filtro); }

        public bool LlenaAutocompleteUsuarios(OdbcConnection myconnect, DataTable TblUsuarios, string Filtro = null)
        { return LlenaAutocompleteUsuarios(myconnect, ref TblUsuarios, Filtro); }

        public bool LlenaAutocompleteTerceros(OdbcConnection myconnect, DataTable TblTerceros, string Filtro = null)
        { return LlenaAutocompleteTerceros(myconnect, ref TblTerceros, Filtro); }

        public bool LlenaAutocompleteMaenit(OdbcConnection myconnect, DataTable TblMaenit, string Filtro = null)
        { return LlenaAutocompleteMaenit(myconnect, ref TblMaenit, Filtro); }

        public void ConfiguraForma(Form Forma, string Empresa, string Servidor, string Bd,
            string Nomforma, string Usuario, string Fecha)
        {
            ConfiguraForma(Forma, ref Empresa, ref Servidor, ref Bd, ref Nomforma, ref Usuario, ref Fecha);
        }

        #endregion
    }
}

