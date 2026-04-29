using System;
using System.Data;
using System.Data.Odbc;
using System.Threading;
using Microsoft.Win32;

namespace ERP.Core.Compartido.Datos
{
    /// <summary>
    /// Clase principal para manejo de conexiones a base de datos via ODBC
    /// </summary>
    public class ClsConect
    {
        #region Estructuras y Enumeraciones

        /// <summary>
        /// Estructura que contiene los parametros de conexion ODBC
        /// </summary>
        public struct odbcConect
        {
            public string PstForFec;
            public string pstMyconec;
            public string pstMyconec1;
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
            public string pstUndred;
            public string pstCptoretfte;
            public string pstCptocap;
            public string pstCptoCdats;
            public string stnit;
            public string sttelcompania;
            public string stdircompania;
            public string Psttop;
            public string Pstlimit;
            public string PstRowNum;
            public string pstForHora;
            public string pstForfecyHora;

            public static string InOIgual = "in";
            public static bool ordenatransaccion;
            public static bool ocultalogo;
            public static bool reporteinmediato;
            public static bool ciclofecha;
            public static bool openoficce;
            public static bool ImprimeQuery;
            public static bool ImprimeQueryError;
            public static string CadenaConexion = "";
        }

        /// <summary>
        /// Tipos de datos de base de datos
        /// </summary>
        public enum tipoDatosDb
        {
            System_String = 1,
            System_Decimal = 2,
            System_Double = 3,
            System_Int32 = 4,
            System_Date = 5,
            System_DateTime = 6,
            System_Byte = 7,
            System_Single = 8
        }

        /// <summary>
        /// Tipo de proceso a ejecutar
        /// </summary>
        public enum Proceso
        {
            Proceso = 1,
            Consulta = 2
        }

        #endregion

        #region Campos privados

        private OdbcConnection myconnect = new OdbcConnection();
        private OdbcCommand mycomqueryconec = new OdbcCommand();

        #endregion

        #region Metodos de Conexion

        /// <summary>
        /// Inicializa la conexion ODBC leyendo configuracion del registro de Windows
        /// </summary>
        /// <param name="varini">Estructura de conexion a inicializar</param>
        /// <summary>
        /// Overload sin ref para compatibilidad con codigo migrado de VB.NET 3.5
        /// </summary>
        public void MyOdbcConect(odbcConect varini)
        {
            MyOdbcConect(ref varini);
        }

        public void MyOdbcConect(ref odbcConect varini)
        {
            RegistryKey rk = Registry.ClassesRoot.OpenSubKey(@"Applications\ConsfiaS\Shell\Open\config");

            if (rk == null)
            {
                throw new Exception("No se encontro la configuracion de conexion en el registro de Windows");
            }

            // Desencriptar password
            string pas = rk.OpenSubKey("pass")?.GetValue("")?.ToString() ?? "";
            char[] passChars = pas.ToCharArray();
            pas = "";
            for (int i = 0; i < passChars.Length; i += 2)
            {
                pas += (char)(passChars[i] - 54);
            }

            varini.pstPascon = pas;
            varini.pstServer = rk.OpenSubKey("server")?.GetValue("")?.ToString() ?? "";
            varini.pstPort = rk.OpenSubKey("port")?.GetValue("")?.ToString() ?? "";
            varini.pstTipoBD = rk.OpenSubKey("tipo")?.GetValue("")?.ToString() ?? "";
            varini.pstBdatos = rk.OpenSubKey("name")?.GetValue("")?.ToString() ?? "";
            varini.pstDNS = rk.OpenSubKey("driver")?.GetValue("")?.ToString() ?? "";
            varini.PstForFec = rk.OpenSubKey("format")?.GetValue("")?.ToString() ?? "";
            varini.pstForfecyHora = rk.OpenSubKey("fechayhora")?.GetValue("")?.ToString() ?? "";
            varini.pstForHora = rk.OpenSubKey("hora")?.GetValue("")?.ToString() ?? "";
            varini.pstUID = rk.OpenSubKey("user")?.GetValue("")?.ToString() ?? "";
            varini.pstEmpresa = "0001-EMPRESA DE PRUEBAS 999";
            varini.sptCodEmpr = varini.pstEmpresa.Substring(0, 4);
            varini.pstUndred = " ";

            switch (varini.pstTipoBD.ToUpper())
            {
                case "MYSQL":
                    varini.pstMyconec = $"Driver={varini.pstDNS};UID={varini.pstUID};DATABASE={varini.pstBdatos}" +
                                        $";PASSWORD={varini.pstPascon};PORT={varini.pstPort};SERVER={varini.pstServer}";    
                    varini.pstMyconec1 = $"ODBC;DATABASE={varini.pstBdatos};DSN=MySql" +
                                         $";OPTION=0;UID=sas;PWR=Q Sa Reader001{varini.pstUID};PORT={varini.pstPort};SERVER={varini.pstServer}";
                    break;

                case "SQL":
                    varini.pstMyconec = $"Driver={varini.pstDNS};Server={varini.pstServer}" +
                                        $";Database={varini.pstBdatos};Uid={varini.pstUID};Pwd={varini.pstPascon};";

                    varini.pstMyconec1 = $"ODBC;Description=Sql Para;DRIVER={varini.pstDNS}" +
                                         $";SERVER={varini.pstServer}" +
                                         $";APP=Microsoft Data Access Components;WSID={Environment.MachineName}" +
                                         $";DATABASE={varini.pstBdatos};UID=sas;Pwd=Q Sa Reader001";
                    break;

                case "ORACLE":
                    varini.pstMyconec = $"DRIVER={varini.pstDNS}" +
                                        $";SERVER={varini.pstServer}" +
                                        $";uid={varini.pstUID};Pwd={varini.pstPascon};dbq={varini.pstBdatos}";

                    varini.pstMyconec1 = $"ODBC;DSN=Mysql;UID=sas;;DBQ={varini.pstBdatos};DBA=W;APA=T;EXC=F;FEN=T;QTO=T;FRC=10;FDL=10;LOB=T;RST=T;BTD=F;BNF=F;BAM=IfAllSuccessful;NUM=NLS";
                    break;

                case "POSTGRES":
                    varini.pstMyconec = $"Driver={varini.pstDNS};Server={varini.pstServer}" +
                                        $";Database={varini.pstBdatos};Uid={varini.pstUID};Pwd={varini.pstPascon}";

                    varini.pstMyconec1 = $"ODBC;DATABASE={varini.pstBdatos};DSN=Mysql" +
                                         $";OPTION=0;UID=sas;PWR=Q Sa Reader001;PORT={varini.pstPort};SERVER={varini.pstServer}";
                    break;

                case "DB2":
                    varini.pstMyconec = $"DSN={varini.pstDNS};Pwd={varini.pstPascon}";
                    odbcConect.InOIgual = "=";
                    break;
            }

            odbcConect.CadenaConexion = varini.pstMyconec;
        }

        #endregion

        #region Metodos de Busqueda

        /// <summary>
        /// Busca informacion de una compania por su codigo
        /// </summary>
        public bool BuscarCompania(
            string codigo,
            OdbcConnection myconect,
            ref string cuentaUtilidad,
            ref string cptoCap,
            ref string calculaSaldo,
            ref string cptoAfavor,
            ref int tipoLiq,
            ref decimal tasaMora,
            ref int diasGracia,
            ref decimal tasaUsura,
            ref int baseLiq,
            ref int ctrlConse,
            ref int conseCreditos,
            ref string cptoRevapo,
            ref string nit,
            ref string direccion,
            ref string nomres,
            ref string cptoServicios,
            ref string cptoExt,
            ref string cptoAho,
            ref string cptoApo,
            ref string cptoRetFte,
            ref string undred,
            ref string cptoCdats,
            ref string cptoIntCdats,
            ref string nombre,
            ref string telefono,
            ref string cpto4Mil,
            ref string cptoIntAhorro,
            ref string cptoAnticipo,
            ref double conseCdat,
            ref double consedep,
            ref int opRecDeuda,
            ref string cpteAnticipo,
            ref string ciudad,
            ref string genCobroConsulta,
            ref double vlrConsulta,
            ref string cpteConsulta,
            ref string nombreResumido,
            ref string cptoIntAnt,
            ref string cptoint)
        {
            string stmysql;
            bool ok;
            DataSet dsdata = new DataSet();
            odbcConect varini = new odbcConect();

            MyOdbcConect(ref varini);

            stmysql = $"select CPTO_CAPITAL,nit,direccion,cptoretfte,UNI_RED,CptoCdats,nombre,TELEFONO from sys_compania where CODIGO = '{codigo}'";
            ok = ExecuteQueryDataset(stmysql, myconnect, "BuscarCompania", ref dsdata, "TblCompania");

            if (ok)
            {
                DataRow row = dsdata.Tables["TblCompania"].Rows[0];
                cptoCap = row["CPTO_CAPITAL"]?.ToString() ?? "";
                nit = row["nit"]?.ToString() ?? "";
                direccion = row["direccion"]?.ToString() ?? "";
                cptoRetFte = row["cptoretfte"]?.ToString() ?? "";
                undred = row["UNI_RED"]?.ToString() ?? "";
                cptoCdats = row["CptoCdats"]?.ToString() ?? "";
                nombre = row["nombre"]?.ToString() ?? "";
                telefono = row["TELEFONO"]?.ToString() ?? "";
            }

            return ok;
        }

        /// <summary>
        /// Sobrecarga simplificada de BuscarCompania
        /// </summary>
        public bool BuscarCompania(
            string codigo,
            OdbcConnection myconect,
            ref string cptoCap,
            ref string nit,
            ref string direccion,
            ref string cptoRetFte,
            ref string undred,
            ref string cptoCdats,
            ref string nombre,
            ref string telefono)
        {
            string stmysql;
            bool ok;
            DataSet dsdata = new DataSet();
            odbcConect varini = new odbcConect();

            MyOdbcConect(ref varini);

            stmysql = $"select CPTO_CAPITAL,nit,direccion,cptoretfte,UNI_RED,CptoCdats,nombre,TELEFONO from sys_compania where CODIGO = '{codigo}'";
            ok = ExecuteQueryDataset(stmysql, myconnect, "BuscarCompania", ref dsdata, "TblCompania");

            if (ok)
            {
                DataRow row = dsdata.Tables["TblCompania"].Rows[0];
                cptoCap = row["CPTO_CAPITAL"]?.ToString() ?? "";
                nit = row["nit"]?.ToString() ?? "";
                direccion = row["direccion"]?.ToString() ?? "";
                cptoRetFte = row["cptoretfte"]?.ToString() ?? "";
                undred = row["UNI_RED"]?.ToString() ?? "";
                cptoCdats = row["CptoCdats"]?.ToString() ?? "";
                nombre = row["nombre"]?.ToString() ?? "";
                telefono = row["TELEFONO"]?.ToString() ?? "";
            }

            return ok;
        }

        #endregion

        #region Metodos de Ejecucion de Queries

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, ref string accionSql)
        {
            string campo1 = null, campo2 = null, campo3 = null, campo4 = null;

            try
            {
                return ExecuteQueryconec(stMysql, appadoConect, nombreProcedimiento,
                    ref campo1, ref campo2, ref campo3, ref campo4);
            }
            catch (Exception ex)
            {
                if (accionSql == "delete")
                {
                    System.Windows.Forms.MessageBox.Show(
                        "No se puede borrar el dato por que esta relacionado con otra tabla");
                    return false;
                }

                throw new Exception(ex.Message);
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, ref string accionSql, ref string campo1)
        {
            string campo2 = null, campo3 = null, campo4 = null;

            try
            {
                return ExecuteQueryconec(stMysql, appadoConect, nombreProcedimiento,
                    ref campo1, ref campo2, ref campo3, ref campo4);
            }
            catch (Exception ex)
            {
                if (accionSql == "delete")
                {
                    System.Windows.Forms.MessageBox.Show(
                        "No se puede borrar el dato por que esta relacionado con otra tabla");
                    return false;
                }

                throw new Exception(ex.Message);
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, ref string accionSql, ref string campo1,
            ref string campo2, ref string campo3, ref string campo4)
        {
            try
            {
                return ExecuteQueryconec(stMysql, appadoConect, nombreProcedimiento,
                    ref campo1, ref campo2, ref campo3, ref campo4);
            }
            catch (Exception ex)
            {
                if (accionSql == "delete")
                {
                    System.Windows.Forms.MessageBox.Show(
                        "No se puede borrar el dato por que esta relacionado con otra tabla");
                    return false;
                }

                throw new Exception(ex.Message);
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento)
        {
            string campo1 = null, campo2 = null, campo3 = null, campo4 = null;
            return ExecuteQueryconec(stMysql, appadoConect, nombreProcedimiento,
                ref campo1, ref campo2, ref campo3, ref campo4);
        }
        /// <summary>
        /// Ejecuta una consulta SQL y retorna valores en los parametros Campo1-4
        /// </summary>
        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, ref string campo1, ref string campo2,
            ref string campo3, ref string campo4 )
        {
            string er = "";
            string querySql;
            bool consulta = false;
            bool result = false;

            if (odbcConect.ImprimeQuery)
            {
                Microsoft.VisualBasic.Interaction.InputBox("Query", nombreProcedimiento, stMysql);
            }

            try
            {
                odbcConect variniValida = new odbcConect();

                if (appadoConect.State == ConnectionState.Closed)
                {
                    MyOdbcConect(ref variniValida);
                    appadoConect.ConnectionString = variniValida.pstMyconec;
                    appadoConect.Open();
                }

                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                mycomqueryconec.CommandTimeout = 0;
                mycomqueryconec.CommandText = mycomqueryconec.CommandText.Replace("''", "' '");

                querySql = mycomqueryconec.CommandText;

                if (querySql.ToLower().Contains("select"))
                {
                    consulta = true;
                }
                else if (querySql.ToLower().Contains("insert") || querySql.ToLower().Contains("update") || querySql.ToLower().Contains("delete"))
                {
                    consulta = false;
                }
                else
                {
                    consulta = true;
                }

                if (consulta)
                {
                    using (OdbcDataReader myread = mycomqueryconec.ExecuteReader())
                    {
                        if (myread.RecordsAffected > 0)
                        {
                            result = true;
                        }

                        if (myread.Read())
                        {
                            string stMysqlLower = stMysql.ToLower();

                            if (stMysqlLower.Contains("campo1"))
                            {
                                campo1 = myread["campo1"] == DBNull.Value ? "0" : myread["campo1"].ToString().Trim();
                            }
                            if (stMysqlLower.Contains("campo2"))
                            {
                                campo2 = myread["campo2"] == DBNull.Value ? "0" : myread["campo2"].ToString().Trim();
                            }
                            if (stMysqlLower.Contains("campo3"))
                            {
                                campo3 = myread["campo3"] == DBNull.Value ? "0" : myread["campo3"].ToString().Trim();
                            }
                            if (stMysqlLower.Contains("campo4"))
                            {
                                campo4 = myread["campo4"] == DBNull.Value ? "0" : myread["campo4"].ToString().Trim();
                            }

                            result = true;
                        }
                    }
                }
                else
                {
                    if (mycomqueryconec.ExecuteNonQuery() > 0)
                    {
                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (odbcConect.ImprimeQueryError)
                {
                    Microsoft.VisualBasic.Interaction.InputBox("Query Error", nombreProcedimiento, stMysql);
                }
                er = ex.Message;
                result = false;
            }

            if (!string.IsNullOrEmpty(er))
            {
                throw new Exception($"{er}\n Procedimiento Origen : {nombreProcedimiento}\nquery :{stMysql}");
            }

            return result;
        }

        /// <summary>
        /// Ejecuta una consulta SQL y llena un DataSet
        /// </summary>
        public bool ExecuteQueryDataset(
            string stMysql,
            OdbcConnection appadoConect,
            string nombreProcedimiento,
            ref DataSet dsDataset,
            string nombreTabla,
            bool validaNulos = false)
        {
            OdbcDataAdapter myread = new OdbcDataAdapter();
            odbcConect variniValida = new odbcConect();

            if (odbcConect.ImprimeQuery)
            {
                Microsoft.VisualBasic.Interaction.InputBox("Query", nombreProcedimiento, stMysql);
            }

            if (appadoConect.State == ConnectionState.Closed)
            {
                MyOdbcConect(ref variniValida);
                appadoConect.ConnectionString = variniValida.pstMyconec;
                appadoConect.Open();
            }

            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                mycomqueryconec.CommandTimeout = 0;
                mycomqueryconec.CommandText = mycomqueryconec.CommandText.Replace("''", "' '");
                mycomqueryconec.ExecuteNonQuery();  
                myread.SelectCommand = mycomqueryconec;
                myread.Fill(dsDataset, nombreTabla);

                if (dsDataset.Tables[nombreTabla].Rows.Count > 0)
                {
                    if (validaNulos)
                    {
                        int cuentaFilas = dsDataset.Tables[nombreTabla].Rows.Count;
                        int cuentaColumnas = dsDataset.Tables[nombreTabla].Columns.Count;

                        for (int indice = 0; indice < cuentaFilas; indice++)
                        {
                            for (int indiceColum = 0; indiceColum < cuentaColumnas; indiceColum++)
                            {
                                bool estaNulo = dsDataset.Tables[nombreTabla].Rows[indice].IsNull(indiceColum);
                                string tipoDato = dsDataset.Tables[nombreTabla].Columns[indiceColum].DataType.ToString();

                                if (estaNulo)
                                {
                                    switch (tipoDato)
                                    {
                                        case "System.String":
                                            dsDataset.Tables[nombreTabla].Rows[indice][indiceColum] = "0";
                                            break;
                                        case "System.Decimal":
                                        case "System.Int32":
                                        case "System.Int16":
                                        case "System.Int64":
                                            dsDataset.Tables[nombreTabla].Rows[indice][indiceColum] = 0;
                                            break;
                                        case "System.DateTime":
                                            dsDataset.Tables[nombreTabla].Rows[indice][indiceColum] = new DateTime(1950, 1, 1);
                                            break;
                                        case "System.Byte[]":
                                            // No hacer nada para arrays de bytes
                                            break;
                                        default:
                                            dsDataset.Tables[nombreTabla].Rows[indice][indiceColum] = "0";
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (odbcConect.ImprimeQueryError)
                {
                    Microsoft.VisualBasic.Interaction.InputBox("Query Error", nombreProcedimiento, stMysql);
                }

                throw new Exception($"{ex}\n Procedimiento Origen : {nombreProcedimiento}\nquery :{stMysql}");
            }
        }

        /// <summary>
        /// Ejecuta una consulta SQL y retorna un DataTable
        /// </summary>
        /// <param name="stMysql">Consulta SQL</param>
        /// <param name="appadoConect">Conexion de la Base Datos</param>
        /// <param name="nombreProcedimiento">Nombre de la funcion que esta invocando</param>
        /// <param name="tablaDatos">La variable DataTable que se va llenar</param>
        /// <returns>Retorna un Boolean que indica que encontro Datos</returns>
        public bool ExecuteConsulta(
            string stMysql,
            OdbcConnection appadoConect,
            string nombreProcedimiento,
            ref DataTable tablaDatos)
        {
            bool result = false;
            OdbcDataAdapter myread = new OdbcDataAdapter();

            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandTimeout = 0;

            if (odbcConect.ImprimeQuery)
            {
                Microsoft.VisualBasic.Interaction.InputBox("Query", nombreProcedimiento, stMysql);
            }

            try
            {
                if (tablaDatos != null && tablaDatos.Rows.Count > 0)
                {
                    tablaDatos.Clear();
                }

                myread.SelectCommand = mycomqueryconec;
                myread.Fill(tablaDatos);

                result = tablaDatos.Rows.Count > 0;
            }
            catch (Exception)
            {
                if (odbcConect.ImprimeQueryError)
                {
                    Microsoft.VisualBasic.Interaction.InputBox("Query Error", nombreProcedimiento, stMysql);
                }
                result = false;
                System.Windows.Forms.MessageBox.Show(
                    $"{System.Runtime.InteropServices.Marshal.GetExceptionForHR(System.Runtime.InteropServices.Marshal.GetHRForException(new Exception()))?.Message}\n Procedimiento Origen : {nombreProcedimiento}\nquery :{stMysql}",
                    "SOLIDO",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
            }
            finally
            {
                myread.Dispose();
            }

            return result;
        }

        #endregion

        #region Metodos Auxiliares

        /// <summary>
        /// Llena la estructura varini con datos de la compania
        /// </summary>
        public void LlenarVarini(ref odbcConect varini)
        {
            myconnect.ConnectionString = varini.pstMyconec;
            myconnect.Open();

            string cptoCap = "", nit = "", direccion = "", cptoRetFte = "", undred = "", cptoCdats = "", nombre = "", telefono = "";

            BuscarCompania(varini.sptCodEmpr, myconnect, ref cptoCap, ref nit, ref direccion, ref cptoRetFte, ref undred, ref cptoCdats, ref nombre, ref telefono);
            varini.pstCptocap = cptoCap;
            varini.stnit = nit;
            varini.stdircompania = direccion;
            varini.pstCptoretfte = cptoRetFte;
            varini.pstUndred = undred;
            varini.pstCptoCdats = cptoCdats;
            varini.pstEmpresa = nombre;
            varini.sttelcompania = telefono;
            myconnect.Close();
            myconnect.Dispose();
        }

        #endregion

        #region Metodos de Stored Procedures

        /// <summary>
        /// Crea un DataTable para parametros de Store Procedure
        /// </summary>
        /// <param name="tipoBd">Tipo de base de datos</param>
        /// <returns>DataTable con columnas para parametros</returns>
        public DataTable SP_GetDataTable(string tipoBd)
        {
            DataTable dataTables = new DataTable();
            dataTables.Columns.Add("NameParameter");
            dataTables.Columns.Add("Valor");
            dataTables.Columns.Add("DbType");
            dataTables.Columns.Add("Direction");

            if (tipoBd.ToUpper() != "MYSQL")
            {
                SP_Armar_DataTable(ref dataTables, "ERROR", "-1", DbType.String, ParameterDirection.Output);
            }

            return dataTables;
        }

        /// <summary>
        /// Agrega un parametro al DataTable de parametros
        /// </summary>
        /// <param name="dataTable">DataTable instanciado con SP_GetDataTable()</param>
        /// <param name="nameParameter">Nombre del parametro del store procedure</param>
        /// <param name="valor">Valor del parametro</param>
        /// <param name="tipoDato">Tipo de dato del parametro</param>
        /// <param name="direction">Direccion del parametro</param>
        public void SP_Armar_DataTable(ref DataTable dataTable, string nameParameter, object valor, DbType tipoDato, ParameterDirection direction)
        {
            dataTable.Rows.Add(nameParameter, valor, tipoDato.ToString(), direction.ToString());
        }

        private DbType GetDbType(string typeColumn)
        {
            switch (typeColumn.Trim())
            {
                case "String": return DbType.String;
                case "Decimal": return DbType.Decimal;
                case "Double": return DbType.Double;
                case "Int32": return DbType.Int32;
                case "Int16": return DbType.Int16;
                case "Int64": return DbType.Int64;
                case "Date": return DbType.Date;
                case "DateTime": return DbType.DateTime;
                case "DateTime2": return DbType.DateTime2;
                case "Time": return DbType.Time;
                case "Boolean": return DbType.Boolean;
                case "Byte": return DbType.Byte;
                case "Single": return DbType.Single;
                case "Binary": return DbType.Binary;
                default: return DbType.String;
            }
        }

        private ParameterDirection GetDbDirection(string typeDireccion)
        {
            switch (typeDireccion)
            {
                case "Input": return ParameterDirection.Input;
                case "InputOutput": return ParameterDirection.InputOutput;
                case "Output": return ParameterDirection.Output;
                case "ReturnValue": return ParameterDirection.ReturnValue;
                default: return ParameterDirection.Input;
            }
        }

        private string GetNameParameter_SP(string nameParameter, string tipoBD)
        {
            switch (tipoBD.ToUpper())
            {
                case "MYSQL":
                case "ORACLE":
                case "DB2":
                    return nameParameter;
                case "SQL":
                    return "@" + nameParameter;
                default:
                    return nameParameter;
            }
        }

        private string GetStoreProcedure(string nameStoreProcedure, string tipoBD, DataTable arregoParametro)
        {
            string nameParameterMod = "";
            int cuentaFilas = arregoParametro.Rows.Count;

            switch (tipoBD.ToUpper())
            {
                case "MYSQL":
                    nameParameterMod = " call " + nameStoreProcedure;
                    if (cuentaFilas != 0)
                    {
                        nameParameterMod += " (";
                        for (int indice = 0; indice < cuentaFilas; indice++)
                        {
                            nameParameterMod += "?,";
                        }
                        nameParameterMod = nameParameterMod.Substring(0, nameParameterMod.Length - 1);
                    }
                    nameParameterMod += ")";
                    break;

                case "SQL":
                    nameParameterMod = nameStoreProcedure;
                    break;

                case "ORACLE":
                    nameParameterMod = "{call " + nameStoreProcedure;
                    if (cuentaFilas != 0)
                    {
                        nameParameterMod += "(";
                        for (int indice = 0; indice < cuentaFilas; indice++)
                        {
                            nameParameterMod += "?,";
                        }
                        nameParameterMod = nameParameterMod.Substring(0, nameParameterMod.Length - 1);
                    }
                    nameParameterMod += ")}";
                    break;

                case "DB2":
                    nameParameterMod = nameStoreProcedure;
                    break;
            }

            return nameParameterMod;
        }

        /// <summary>
        /// Ejecuta un Stored Procedure
        /// </summary>
        /// <param name="nameStoreProcedure">Nombre del procedimiento</param>
        /// <param name="arregoParametro">DataTable con los parametros</param>
        /// <param name="tipoBd">Tipo de base de datos</param>
        /// <param name="myconnect">Conexion</param>
        /// <param name="nameProcesoInvoque">Nombre del proceso que invoca SP_Execute</param>
        /// <param name="tipoProceso">Tipo de proceso (Proceso o Consulta)</param>
        /// <param name="nombreTabla">Nombre de la tabla (opcional)</param>
        /// <param name="datasetSp">DataSet para resultados (opcional)</param>
        public void SP_Execute(
            string nameStoreProcedure,
            DataTable arregoParametro,
            string tipoBd,
            OdbcConnection myconnect,
            string nameProcesoInvoque,
            Proceso tipoProceso,
            string nombreTabla = "",
            DataSet datasetSp = null)
        {
            string er = "";
            string errorContr = "";

            try
            {
                OdbcCommand odbc = new OdbcCommand(GetStoreProcedure(nameStoreProcedure, tipoBd, arregoParametro), myconnect);
                odbc.CommandType = CommandType.StoredProcedure;
                odbc.CommandTimeout = 0;

                if (arregoParametro.Rows.Count > 0)
                {
                    int cuentaFilas = arregoParametro.Rows.Count;
                    for (int indice = 0; indice < cuentaFilas; indice++)
                    {
                        DataRow row = arregoParametro.Rows[indice];
                        OdbcParameter parametros = new OdbcParameter();
                        parametros.ParameterName = GetNameParameter_SP(row["NameParameter"].ToString(), tipoBd);
                        parametros.DbType = GetDbType(row["DbType"].ToString());
                        parametros.Direction = GetDbDirection(row["Direction"].ToString());

                        if (row["Direction"].ToString() == "Input")
                        {
                            parametros.Value = row["Valor"];
                        }
                        else
                        {
                            parametros.Size = 100;
                        }

                        odbc.Parameters.Add(parametros);
                    }
                }

                odbc.ExecuteNonQuery();

                switch (tipoProceso)
                {
                    case Proceso.Consulta:
                        OdbcDataAdapter myread = new OdbcDataAdapter();
                        myread.SelectCommand = odbc;
                        myread.Fill(datasetSp, nombreTabla);
                        errorContr = "-1"; // falta consultar como en mysql arroja el error
                        break;

                    case Proceso.Proceso:
                        errorContr = odbc.Parameters[0].Value?.ToString() ?? "";
                        break;
                }

                if (errorContr != "-1") // en el sp debe asignarle a la variable ERROR = -1 para instanciarlo
                {
                    System.Windows.Forms.MessageBox.Show(errorContr, $"SOLIDO-{nameProcesoInvoque}", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                }
            }
            catch (OdbcException)
            {
                er = System.Runtime.InteropServices.Marshal.GetExceptionForHR(System.Runtime.InteropServices.Marshal.GetHRForException(new Exception()))?.Message ?? "";
            }

            if (!string.IsNullOrEmpty(er))
            {
                throw new Exception($"{er}\n Procedimiento Origen : {nameProcesoInvoque} --> Stored Procedure{nameStoreProcedure}\n");
            }
        }

        #endregion

        #region Metodos Utilitarios

        /// <summary>
        /// Pausa la ejecucion por el tiempo especificado
        /// </summary>
        /// <param name="tiempo">Tiempo en milisegundos</param>
        public void Espera(int tiempo)
        {
            Thread.Sleep(tiempo);
        }

        #endregion

        #region Overloads sin ref para compatibilidad VB.NET 3.5 → C# 13

        // ExecuteQueryconec: overloads sin ref para callers que no necesitan los valores de retorno
        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, string campo1, string campo2, string campo3, string campo4)
        {
            return ExecuteQueryconec(stMysql, appadoConect, nombreProcedimiento,
                ref campo1, ref campo2, ref campo3, ref campo4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, string accionSql, string campo1)
        {
            return ExecuteQueryconec(stMysql, appadoConect, nombreProcedimiento,
                ref accionSql, ref campo1);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, string accionSql, string campo1,
            string campo2, string campo3, string campo4)
        {
            return ExecuteQueryconec(stMysql, appadoConect, nombreProcedimiento,
                ref accionSql, ref campo1, ref campo2, ref campo3, ref campo4);
        }

        // ExecuteQueryDataset: overload sin ref para DataSet
        public bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect,
            string nombreProcedimiento, DataSet dsDataset, string nombreTabla,
            bool validaNulos = false)
        {
            return ExecuteQueryDataset(stMysql, appadoConect, nombreProcedimiento,
                ref dsDataset, nombreTabla, validaNulos);
        }

        // LlenarVarini: ya tiene overload sin ref (MyOdbcConect tambien)
        // BuscarCompania: ya existe overload sin ref en Codigo

        // SP_Armar_DataTable: overload sin ref
        public void SP_Armar_DataTable(DataTable dataTable, string nameParameter, object valor, System.Data.DbType tipoDato, System.Data.ParameterDirection direction)
        {
            SP_Armar_DataTable(ref dataTable, nameParameter, valor, tipoDato, direction);
        }

        #endregion
    }
}
