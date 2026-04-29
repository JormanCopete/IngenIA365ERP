using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Produccion.Services
{
    /// <summary>
    /// Clase para manejo de procesos productivos
    /// </summary>
    public class ProcesoProd
    {
        #region Campos privados

        private OdbcCommand _mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect _odbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private OdbcConnection _myconnect = new OdbcConnection();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private bool _ok;
        private ERP.Core.Compartido.Utilidades.Ayuda _msgSas = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private string _stmysql;
        private string _usuarioSistema;
        private string _accionsql;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor de la clase ProcesoProd
        /// </summary>
        /// <param name="conexion">Conexion ODBC</param>
        public ProcesoProd(OdbcConnection conexion)
        {
            _myconnect = conexion;

            if (_myconnect.State != ConnectionState.Open)
            {
                _myconnect.Open();
            }

            try
            {
                _odbcConnect.MyOdbcConect(ref varini);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #endregion

        #region Destructor

        ~ProcesoProd()
        {
        }

        #endregion

        #region Propiedades

        /// <summary>
        /// Usuario del sistema
        /// </summary>
        public string UsuarioSistema
        {
            get { return _usuarioSistema; }
            set { _usuarioSistema = value; }
        }

        #endregion

        #region Enumeraciones

        /// <summary>
        /// Enumeracion para navegacion de registros
        /// </summary>
        public enum Navega
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }

        #endregion

        #region Metodos privados

        /// <summary>
        /// Ejecuta una consulta SQL y llena un DataSet
        /// </summary>
        private bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string nombreProcedimiento, ref DataSet dsDataset, string nombreTabla)
        {
            OdbcDataAdapter myread = new OdbcDataAdapter();
            _mycomqueryconec.CommandText = stMysql;
            _mycomqueryconec.Connection = appadoConect;
            _mycomqueryconec.CommandText = Strings.Replace(_mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
            _mycomqueryconec.ExecuteNonQuery();

            myread.SelectCommand = _mycomqueryconec;
            myread.Fill(dsDataset, nombreTabla);

            return dsDataset.Tables[nombreTabla].Rows.Count > 0;
        }

        #endregion

        #region ExecuteQueryconec

        /// <summary>
        /// Ejecuta una consulta SQL con parametros opcionales
        /// </summary>
        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string nombreProcedimiento,
            ref string accionSql, ref string campo1, ref string campo2, ref string campo3, ref string campo4)
        {
            string er = "";
            bool result = false;
            try
            {
                _mycomqueryconec.CommandText = stMysql;
                _mycomqueryconec.Connection = appadoConect;
                _mycomqueryconec.CommandText = Strings.Replace(_mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);

                OdbcDataReader myread = _mycomqueryconec.ExecuteReader();
                if (myread.RecordsAffected > 0)
                {
                    result = true;
                }
                if (myread.Read())
                {
                    if (stMysql.Contains("campo1"))
                    {
                        campo1 = myread["campo1"] == DBNull.Value ? "0" : myread["campo1"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo2"))
                    {
                        campo2 = myread["campo2"] == DBNull.Value ? "0" : myread["campo2"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo3"))
                    {
                        campo3 = myread["campo3"] == DBNull.Value ? "0" : myread["campo3"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo4"))
                    {
                        campo4 = myread["campo4"] == DBNull.Value ? "0" : myread["campo4"].ToString().Trim();
                    }
                    result = true;
                }
                myread.Close();
            }
            catch
            {
                er = Information.Err().Description;
                result = false;
            }

            if (!string.IsNullOrEmpty(er))
            {
                if (accionSql == "delete")
                {
                    MessageBox.Show("No se puede borrar el dato por que esta relacionado con otra tabla");
                }
                else
                {
                    throw new Exception(er + "\n Procedimiento Origen : " + nombreProcedimiento + "\nquery :" + stMysql);
                }
            }

            return result;
        }

        #endregion

        #region Unidad Productiva

        /// <summary>
        /// Busca una unidad productiva
        /// </summary>
        public bool BuscarUnidadProductiva(string codUniProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            codUniProd = Strings.Right("00000000" + codUniProd, 8);
            string where = "";

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_UnidadProd where cod_uni_prod = '" + codUniProd + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_UnidadProd where cod_uni_prod > ' ' order by cod_uni_prod";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_UnidadProd where cod_uni_prod < '" + codUniProd + "' order by cod_uni_prod desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_UnidadProd where cod_uni_prod > '" + codUniProd + "' order by cod_uni_prod";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_UnidadProd where cod_uni_prod <= '9999' order by cod_uni_prod desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblProcesoProd");
            }
            catch (Exception) { }

            stbuilder.Append("select cod_uni_prod,nombre,nomres  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarUnidadProductiva", ref dsData, "tblProcesoProd");

            if (dsData.Tables["tblProcesoProd"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblProcesoProd"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba una unidad productiva
        /// </summary>
        public bool GrabarUnidadProductivo(string codUniProd, string nombre, string nomres)
        {
            DataSet ds = null;
            _ok = BuscarUnidadProductiva(codUniProd, ref ds, Navega.Ninguno);
            codUniProd = Strings.Right("00000000" + codUniProd, 8);

            if (!_ok)
            {
                _stmysql = "insert into gp_UnidadProd(cod_uni_prod,nombre,nomres,usuarioSistema) values ('" + codUniProd + "','" + nombre + "','" + nomres + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_UnidadProd set nombre='" + nombre + "',nomres='" + nomres + "' where cod_uni_prod='" + codUniProd + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarUnidadProductivo", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina una unidad productiva
        /// </summary>
        public bool EliminarUnidadProductivo(string codUniProd)
        {
            codUniProd = Strings.Right("00000000" + codUniProd, 8);
            _stmysql = "delete from gp_UnidadProd where cod_uni_prod='" + codUniProd + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarUnidadProductivo", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de unidad productiva
        /// </summary>
        public string HelpUnidadProductivo(Form myforma)
        {
            return _msgSas.CargaAyuda("gp_UnidadProd", "cod_uni_prod", "nombre", "nomres", _myconnect, myforma, "Codigo Unidad");
        }

        #endregion

        #region Sub Unidad Productiva

        /// <summary>
        /// Busca una sub unidad productiva
        /// </summary>
        public bool BuscarSubUnidadProductiva(string codSubUniProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";
            codSubUniProd = Strings.Right("00000000" + codSubUniProd, 8);

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_subUnidadProd where codigo_SubUnidadProd= '" + codSubUniProd + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd > ' '  order by codigo_SubUnidadProd Asc";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd < '" + codSubUniProd + "' order by codigo_SubUnidadProd desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd > '" + codSubUniProd + "' order by codigo_SubUnidadProd";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd <= '9999' order by codigo_SubUnidadProd desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblSubUniProd");
            }
            catch (Exception) { }

            stbuilder.Append("select codigo_SubUnidadProd,nombre,nomres,cod_uni_prod  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarSubUnidadProductiva", ref dsData, "tblSubUniProd");

            if (dsData.Tables["tblSubUniProd"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblSubUniProd"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Busca una sub unidad productiva por codigo de unidad
        /// </summary>
        public virtual bool BuscarSubUnidadProductiva(string codSubUniProd, string cod_uni_prod, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";

            codSubUniProd = Strings.Right("00000000" + codSubUniProd, 8);
            cod_uni_prod = Strings.Right("00000000" + cod_uni_prod, 8);

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_subUnidadProd where codigo_SubUnidadProd= '" + codSubUniProd + "' and cod_uni_prod='" + cod_uni_prod + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd > ' '   and cod_uni_prod='" + cod_uni_prod + "' order by codigo_SubUnidadProd";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd < '" + codSubUniProd + "' and cod_uni_prod='" + cod_uni_prod + "' order by codigo_SubUnidadProd desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd > '" + codSubUniProd + "' and cod_uni_prod='" + cod_uni_prod + "' order by codigo_SubUnidadProd";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd <= '9999' and cod_uni_prod='" + cod_uni_prod + "' order by codigo_SubUnidadProd desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblSubUniProd");
            }
            catch (Exception) { }

            stbuilder.Append("select codigo_SubUnidadProd,nombre,nomres,cod_uni_prod  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarSubUnidadProductiva", ref dsData, "tblSubUniProd");

            if (dsData.Tables["tblSubUniProd"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblSubUniProd"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Busca sub unidad productiva por empresa
        /// </summary>
        public bool BuscarSubUnidadProductivaXEmpresa(string cod_uni_prod, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";
            cod_uni_prod = Strings.Right("00000000" + cod_uni_prod, 8);

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_subUnidadProd where  cod_uni_prod='" + cod_uni_prod + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_SubUnidadProd where cod_uni_prod  > ' '    order by codigo_SubUnidadProd";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_SubUnidadProd where cod_uni_prod < '" + cod_uni_prod + "' order by codigo_SubUnidadProd desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_SubUnidadProd where cod_uni_prod > '" + cod_uni_prod + "' order by codigo_SubUnidadProd";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_SubUnidadProd where codigo_SubUnidadProd <= '9999' and cod_uni_prod='" + cod_uni_prod + "' order by codigo_SubUnidadProd desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblSubUniProd");
            }
            catch (Exception) { }

            stbuilder.Append("select codigo_SubUnidadProd,nombre,nomres,cod_uni_prod  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarSubUnidadProductivaXEmpresa", ref dsData, "tblSubUniProd");

            if (dsData.Tables["tblSubUniProd"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblSubUniProd"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba una sub unidad productiva
        /// </summary>
        public bool GrabarSubUnidadProductivo(string codSubUniProd, string nombre, string nomres, string codUniProd)
        {
            DataSet ds = null;
            _ok = BuscarSubUnidadProductiva(codSubUniProd, ref ds, Navega.Ninguno);
            codSubUniProd = Strings.Right("00000000" + codSubUniProd, 8);

            if (!_ok)
            {
                _stmysql = "insert into gp_SubUnidadProd(codigo_SubUnidadProd,nombre,nomres,cod_uni_prod,usuarioSistema) values ('" + codSubUniProd + "','" + nombre + "','" + nomres + "','" + codUniProd + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_SubUnidadProd set nombre='" + nombre + "', nomres='" + nomres + "',  cod_uni_prod='" + codUniProd + "'  where codigo_SubUnidadProd='" + codSubUniProd + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarSubUnidadProductivo", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina una sub unidad productiva
        /// </summary>
        public bool EliminarSubUnidadProductivo(string codSubUniProd)
        {
            codSubUniProd = Strings.Right("00000000" + codSubUniProd, 8);
            _stmysql = "delete from gp_SubUnidadProd where codigo_SubUnidadProd='" + codSubUniProd + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarSubUnidadProductivo", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de sub unidad productiva
        /// </summary>
        public string HelpSubUnidadProductivo(Form myforma, string filtro = "")
        {
            //return _msgSas.CargaAyuda("gp_SubUnidadProd", "codigo_SubUnidadProd", "nombre", "nomres", _myconnect, myforma, "codigo Sub Unidad", "Nombre", "", filtro, "", "", "Nombre Resumido");
            return string.Empty;
        }

        #endregion

        #region Tipo Evolucion Empresa

        /// <summary>
        /// Busca un tipo de evolucion de empresa
        /// </summary>
        public bool BuscarTipoEvolucionEmp(string codUniProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_TipoevolucionEmpresa where codEvolucion = '" + codUniProd + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_TipoevolucionEmpresa where codEvolucion > ' ' order by codEvolucion";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_TipoevolucionEmpresa where codEvolucion < '" + codUniProd + "' order by codEvolucion desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_TipoevolucionEmpresa where codEvolucion > '" + codUniProd + "' order by codEvolucion";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_TipoevolucionEmpresa where codEvolucion <= '9999' order by codEvolucion desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblTipoEvolucion");
            }
            catch (Exception) { }

            stbuilder.Append("select codEvolucion,nombre,nomres  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarUnidadProductiva", ref dsData, "tblTipoEvolucion");

            if (dsData.Tables["tblTipoEvolucion"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblTipoEvolucion"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Busca todos los tipos de evolucion de empresa
        /// </summary>
        public virtual bool BuscarTipoEvolucionEmp(ref DataSet dsDataset)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = " from gp_TipoevolucionEmpresa";

            try
            {
                dsDataset?.Tables.Remove("tblTipoEvolucion");
            }
            catch (Exception) { }

            stbuilder.Append("select codEvolucion,nombre  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarTipoEvolucionEmp", ref dsData, "tblTipoEvolucion");

            if (dsData.Tables["tblTipoEvolucion"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblTipoEvolucion"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba un tipo de evolucion de empresa
        /// </summary>
        public bool GrabarTipoEvolucionEmp(string codUniProd, string nombre, string nomres)
        {
            DataSet ds = null;
            _ok = BuscarTipoEvolucionEmp(codUniProd, ref ds, Navega.Ninguno);

            if (!_ok)
            {
                _stmysql = "insert into gp_TipoevolucionEmpresa(codEvolucion,nombre,nomres,usuarioSistema) values ('" + codUniProd + "','" + nombre + "','" + nomres + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_TipoevolucionEmpresa set nombre='" + nombre + "',nomres='" + nomres + "' where codEvolucion='" + codUniProd + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarTipoEvolucionEmp", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina un tipo de evolucion de empresa
        /// </summary>
        public bool EliminarTipoEvolucionEmp(string codUniProd)
        {
            _stmysql = "delete from gp_TipoevolucionEmpresa where codEvolucion='" + codUniProd + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarTipoEvolucionEmp", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de tipo de evolucion de empresa
        /// </summary>
        public string HelpTipoEvolucionEmp(Form myforma)
        {
            return _msgSas.CargaAyuda("gp_TipoevolucionEmpresa", "codEvolucion", "nombre", "nomres", _myconnect, myforma, "Codigo Tipo Evolucion");
        }

        #endregion

        #region Estado Socio Empresa

        /// <summary>
        /// Busca un estado de socio de empresa
        /// </summary>
        public bool BuscarEstadoSocioEmp(string codUniProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            codUniProd = Strings.Right("0000" + codUniProd, 4);
            string where = "";

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_EstadoSocionEmp where codestadoEmpre = '" + codUniProd + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_EstadoSocionEmp where codestadoEmpre > ' ' order by codestadoEmpre";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_EstadoSocionEmp where codestadoEmpre < '" + codUniProd + "' order by codestadoEmpre desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_EstadoSocionEmp where codestadoEmpre > '" + codUniProd + "' order by codestadoEmpre";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_EstadoSocionEmp where codestadoEmpre <= '9999' order by codestadoEmpre desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblTipoEstadoSocioEmp");
            }
            catch (Exception) { }

            stbuilder.Append("select codestadoEmpre,nombre,nomres  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarUnidadProductiva", ref dsData, "tblTipoEstadoSocioEmp");

            if (dsData.Tables["tblTipoEstadoSocioEmp"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblTipoEstadoSocioEmp"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Busca todos los estados de socio de empresa
        /// </summary>
        public DataSet BuscarEstadoSocioEmpresa()
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = " from gp_estadosocionemp  order by codestadoEmpre desc ";

            stbuilder.Append("select codestadoEmpre,nombre " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarEstadoSocioEmpresa", ref dsData, "tblTipoEstadoSocioEmp");

            return dsData;
        }

        /// <summary>
        /// Graba un estado de socio de empresa
        /// </summary>
        public bool GrabarEstadoSocioEmp(string codUniProd, string nombre, string nomres)
        {
            DataSet ds = null;
            _ok = BuscarEstadoSocioEmp(codUniProd, ref ds, Navega.Ninguno);
            codUniProd = Strings.Right("0000" + codUniProd, 4);

            if (!_ok)
            {
                _stmysql = "insert into gp_EstadoSocionEmp(codestadoEmpre,nombre,nomres,usuarioSistema) values ('" + codUniProd + "','" + nombre + "','" + nomres + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_EstadoSocionEmp set nombre='" + nombre + "',nomres='" + nomres + "' where codestadoEmpre='" + codUniProd + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarEstadoSocioEmp", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina un estado de socio de empresa
        /// </summary>
        public bool EliminarEstadoSocioEmp(string codUniProd)
        {
            codUniProd = Strings.Right("0000" + codUniProd, 4);
            _stmysql = "delete from gp_EstadoSocionEmp where codestadoEmpre='" + codUniProd + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarEstadoSocioEmp", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de estado de socio de empresa
        /// </summary>
        public string HelpEstadoSocioEmp(Form myforma)
        {
            return _msgSas.CargaAyuda("gp_EstadoSocionEmp", "codestadoEmpre", "nombre", "nomres", _myconnect, myforma, "Codigo Estado Evolucion");
        }

        #endregion

        #region Instructor Grupo Productivo

        /// <summary>
        /// Busca un instructor de grupo productivo
        /// </summary>
        public bool BuscarIntructorGrupProd(string codUniProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            codUniProd = Strings.Right("00000000000000" + codUniProd, 14);
            string where = "";

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_instruGruPoProduc where codInstruProd = '" + codUniProd + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_instruGruPoProduc where codInstruProd > ' ' order by codInstruProd";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_instruGruPoProduc where codInstruProd < '" + codUniProd + "' order by codInstruProd desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_instruGruPoProduc where codInstruProd > '" + codUniProd + "' order by codInstruProd";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_instruGruPoProduc where codInstruProd <= '9999' order by codInstruProd desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblIntructorGrupProd");
            }
            catch (Exception) { }

            stbuilder.Append("select codInstruProd,nombres,apellidos  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarIntructorGrupProd", ref dsData, "tblIntructorGrupProd");

            if (dsData.Tables["tblIntructorGrupProd"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblIntructorGrupProd"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba un instructor de grupo productivo
        /// </summary>
        public bool GrabarIntructorGrupProd(string codUniProd, string nombre, string apellidos)
        {
            DataSet ds = null;
            _ok = BuscarIntructorGrupProd(codUniProd, ref ds, Navega.Ninguno);
            codUniProd = Strings.Right("00000000000000" + codUniProd, 14);

            if (!_ok)
            {
                _stmysql = "insert into gp_instruGruPoProduc(codInstruProd,nombres,apellidos,usuarioSistema) values ('" + codUniProd + "','" + nombre + "','" + apellidos + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_instruGruPoProduc set nombres='" + nombre + "',apellidos='" + apellidos + "' where codInstruProd='" + codUniProd + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarIntructorGrupProd", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina un instructor de grupo productivo
        /// </summary>
        public bool EliminarIntructorGrupProdp(string codUniProd)
        {
            codUniProd = Strings.Right("00000000000000" + codUniProd, 14);
            _stmysql = "delete from gp_instruGruPoProduc where codInstruProd='" + codUniProd + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarIntructorGrupProdp", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de instructor de grupo productivo
        /// </summary>
        public string HelpIntructorGrupProd(Form myforma)
        {
            return _msgSas.CargaAyuda("gp_instruGruPoProduc", "codInstruProd", "nombres", "apellidos", _myconnect, myforma, "Codigo Instructor");
        }

        #endregion

        #region Capacitaciones

        /// <summary>
        /// Busca una capacitacion
        /// </summary>
        public bool BuscarCapaGrupProd(string codCapacitaciones, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            codCapacitaciones = Strings.Right("00000000" + codCapacitaciones, 8);
            string where = "";

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_Capacitaciones where codCapacitacion = '" + codCapacitaciones + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_Capacitaciones where codCapacitacion > ' ' order by codCapacitacion";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_Capacitaciones where codCapacitacion < '" + codCapacitaciones + "' order by codCapacitacion desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_Capacitaciones where codCapacitacion > '" + codCapacitaciones + "' order by codCapacitacion";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_Capacitaciones where codCapacitacion <= '9999' order by codCapacitacion desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblCapacitaciones");
            }
            catch (Exception) { }

            stbuilder.Append("select codCapacitacion,nombres,nomres,FechaInicio,FechaTerminacin,CuposCapacitacion,direccionCartilla " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarCapaGrupProd", ref dsData, "tblCapacitaciones");

            if (dsData.Tables["tblCapacitaciones"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblCapacitaciones"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba una capacitacion
        /// </summary>
        public bool GrabarCapaGrupProd(string codCapacitaciones, string nombre, string nomres, string direccionCartilla, string fechaInicio, string fechaTerminacin, string cuposCapacitacion)
        {
            DataSet ds = null;
            _ok = BuscarCapaGrupProd(codCapacitaciones, ref ds, Navega.Ninguno);
            codCapacitaciones = Strings.Right("00000000" + codCapacitaciones, 8);

            if (!_ok)
            {
                _stmysql = "insert into gp_Capacitaciones(codCapacitacion,nombres,nomres,direccionCartilla,FechaInicio,FechaTerminacin,CuposCapacitacion,usuarioSistema) values ('" + codCapacitaciones + "','" + nombre + "','" + nomres + "','" + direccionCartilla + "','" + fechaInicio + "','" + fechaTerminacin + "'," + cuposCapacitacion + ",'" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_Capacitaciones set nombres='" + nombre + "',nomres='" + nomres + "',direccionCartilla='" + direccionCartilla + "',FechaInicio='" + fechaInicio + "',FechaTerminacin='" + fechaTerminacin + "',CuposCapacitacion=" + cuposCapacitacion + "  where codCapacitacion='" + codCapacitaciones + "'";
            }

            return _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarCapaGrupProd", ref _accionsql);
        }

        /// <summary>
        /// Elimina una capacitacion
        /// </summary>
        public bool EliminarCapaGrupProd(string codCapacitaciones)
        {
            codCapacitaciones = Strings.Right("00000000" + codCapacitaciones, 8);
            _stmysql = "delete from gp_Capacitaciones where codCapacitacion='" + codCapacitaciones + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarCapaGrupProd", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de capacitaciones
        /// </summary>
        public string HelpCapaGrupProd(Form myforma)
        {
            return _msgSas.CargaAyuda("gp_Capacitaciones", "codCapacitacion", "nombres", "nomres", _myconnect, myforma, "Codigo_Capacitaciones");
        }

        #endregion

        #region Tipo Inversion

        /// <summary>
        /// Busca un tipo de inversion
        /// </summary>
        public bool BuscarTipoInversion(string codUniProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            codUniProd = Strings.Right("00" + codUniProd, 2);
            string where = "";

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_tipoInversion where codTipoInventario = '" + codUniProd + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_tipoInversion where codTipoInventario > ' ' order by codTipoInventario";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_tipoInversion where codTipoInventario < '" + codUniProd + "' order by codTipoInventario desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_tipoInversion where codTipoInventario > '" + codUniProd + "' order by codTipoInventario";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_tipoInversion where codTipoInventario <= '9999' order by codTipoInventario desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblTipoInversion");
            }
            catch (Exception) { }

            stbuilder.Append("select codTipoInventario,nombre,nomres  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarTipoInversion", ref dsData, "tblTipoInversion");

            if (dsData.Tables["tblTipoInversion"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblTipoInversion"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba un tipo de inversion
        /// </summary>
        public bool GrabarTipoInversion(string codUniProd, string nombre, string nomres)
        {
            DataSet ds = null;
            _ok = BuscarTipoInversion(codUniProd, ref ds, Navega.Ninguno);
            codUniProd = Strings.Right("00" + codUniProd, 2);

            if (!_ok)
            {
                _stmysql = "insert into gp_tipoInversion(codTipoInventario,nombre,nomres,usuarioSistema) values ('" + codUniProd + "','" + nombre + "','" + nomres + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_tipoInversion set nombre='" + nombre + "',nomres='" + nomres + "' where codTipoInventario='" + codUniProd + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarTipoInversion", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina un tipo de inversion
        /// </summary>
        public bool EliminarTipoInversion(string codUniProd)
        {
            codUniProd = Strings.Right("00" + codUniProd, 2);
            _stmysql = "delete from gp_tipoInversion where codTipoInventario='" + codUniProd + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarTipoInversion", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de tipo de inversion
        /// </summary>
        public string HelpTipoInversion(Form myforma)
        {
            return _msgSas.CargaAyuda("gp_tipoInversion", "codTipoInventario", "nombre", "nomres", _myconnect, myforma, "Tipo Inversion");
        }

        #endregion

        #region Tipo Seguimiento

        /// <summary>
        /// Busca un tipo de seguimiento
        /// </summary>
        public bool BuscarTipoSeguimiento(string codUniProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            codUniProd = Strings.Right("00" + codUniProd, 2);
            string where = "";

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_TipoSeguimiento where codTipoSeguimiento = '" + codUniProd + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_TipoSeguimiento where codTipoSeguimiento > ' ' order by codTipoSeguimiento";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_TipoSeguimiento where codTipoSeguimiento < '" + codUniProd + "' order by codTipoSeguimiento desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_TipoSeguimiento where codTipoSeguimiento > '" + codUniProd + "' order by codTipoSeguimiento";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_TipoSeguimiento where codTipoSeguimiento <= '9999' order by codTipoSeguimiento desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblTipoSeguimiento");
            }
            catch (Exception) { }

            stbuilder.Append("select codTipoSeguimiento,nombre,nomres  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarTipoSeguimiento", ref dsData, "tblTipoSeguimiento");

            if (dsData.Tables["tblTipoSeguimiento"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblTipoSeguimiento"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Busca todos los tipos de seguimiento
        /// </summary>
        public virtual bool BuscarTipoSeguimiento(ref DataSet dsDataset)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = " from gp_TipoSeguimiento";

            try
            {
                dsDataset?.Tables.Remove("tblTipoSeguimiento");
            }
            catch (Exception) { }

            stbuilder.Append("select codTipoSeguimiento,nombre" + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarTipoSeguimiento", ref dsData, "tblTipoSeguimiento");

            if (dsData.Tables["tblTipoSeguimiento"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblTipoSeguimiento"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba un tipo de seguimiento
        /// </summary>
        public bool GrabarTipoSeguimiento(string codUniProd, string nombre, string nomres)
        {
            DataSet ds = null;
            _ok = BuscarTipoSeguimiento(codUniProd, ref ds, Navega.Ninguno);
            codUniProd = Strings.Right("00" + codUniProd, 2);

            if (!_ok)
            {
                _stmysql = "insert into gp_TipoSeguimiento(codTipoSeguimiento,nombre,nomres,usuarioSistema) values ('" + codUniProd + "','" + nombre + "','" + nomres + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_TipoSeguimiento set nombre='" + nombre + "',nomres='" + nomres + "' where codTipoSeguimiento='" + codUniProd + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarTipoSeguimiento", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina un tipo de seguimiento
        /// </summary>
        public bool EliminarTipoSeguimiento(string codUniProd)
        {
            codUniProd = Strings.Right("00" + codUniProd, 2);
            _stmysql = "delete from gp_TipoSeguimiento where codTipoSeguimiento='" + codUniProd + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarTipoSeguimiento", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de tipo de seguimiento
        /// </summary>
        public string HelpTipoSeguimiento(Form myforma)
        {
            return _msgSas.CargaAyuda("gp_TipoSeguimiento", "codTipoSeguimiento", "nombre", "nomres", _myconnect, myforma, "Codigo Estado Evolucion");
        }

        #endregion

        #region Empresa

        /// <summary>
        /// Busca una empresa
        /// </summary>
        public bool BuscarEmpresa(string codEmpresa, ref DataSet dsDataset, Navega navegar = Navega.Ninguno)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_EmpresaProd where codEmpresa= '" + codEmpresa + "'";
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_EmpresaProd where codEmpresa > ' ' order by codEmpresa";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_EmpresaProd where codEmpresa < '" + codEmpresa + "' order by codEmpresa desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_EmpresaProd where codEmpresa > '" + codEmpresa + "' order by codEmpresa";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_EmpresaProd where codEmpresa <= '9999' order by codEmpresa desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblEmpresaProd");
            }
            catch (Exception) { }

            stbuilder.Append("select codEmpresa,nombre,codigo_SubUnidadProd,fechaCreaEmp " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarEmpresa", ref dsData, "tblEmpresaProd");

            if (dsData.Tables["tblEmpresaProd"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblEmpresaProd"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba una empresa
        /// </summary>
        public bool GrabarEmpresa(string codEmpresa, string nombre, string subUnidadProd, string fechaCreaEmp)
        {
            DataSet ds = null;
            _ok = BuscarEmpresa(codEmpresa, ref ds, Navega.Ninguno);
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);

            if (!_ok)
            {
                _stmysql = "insert into gp_EmpresaProd(codEmpresa,nombre,codigo_SubUnidadProd,fechaCreaEmp,usuarioSistema) values ('" + codEmpresa + "','" + nombre + "','" + subUnidadProd + "','" + fechaCreaEmp + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_EmpresaProd set nombre='" + nombre + "',codigo_SubUnidadProd='" + subUnidadProd + "', fechaCreaEmp='" + fechaCreaEmp + "'   where codEmpresa='" + codEmpresa + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarEmpresa", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina una empresa
        /// </summary>
        public bool EliminarEmpresa(string codEmpresa)
        {
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);
            _stmysql = "delete from gp_EmpresaProd where codEmpresa='" + codEmpresa + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarEmpresa", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de empresa
        /// </summary>
        // public string HelpEmpresa(Form myforma) // ERROR: CS0161
        // {
            // return _msgSas.CargaAyuda("gp_EmpresaProd", "codEmpresa", "nombre", "fechaCreaEmp", _myconnect, myforma, "codigo_Empresa", "Nombre", "", "", "", "", "Fecha Creacion"); // ERROR: CS1620
        // }

        #endregion

        #region Referencia Producto

        /// <summary>
        /// Busca una referencia de producto
        /// </summary>
        public bool BuscarReferenciaProd(string codReferenciaProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno, string codEmpresa = "")
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";
            codReferenciaProd = Strings.Right("000000" + codReferenciaProd, 6);
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);

            if (navegar == Navega.Ninguno && codEmpresa != "00000000")
            {
                where = " from gp_ReferenciaProd where codReferenciaProd= '" + codReferenciaProd + "' and  codEmpresa   ='" + codEmpresa + "'";
            }
            else
            {
                where = " from gp_ReferenciaProd where codReferenciaProd= '" + codReferenciaProd + "'";
            }

            if (navegar == Navega.Primero)
            {
                where = " from gp_ReferenciaProd where codReferenciaProd > ' ' and  codEmpresa='" + codEmpresa + "' order by codReferenciaProd";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_ReferenciaProd where codReferenciaProd < '" + codReferenciaProd + "'and  codEmpresa='" + codEmpresa + "' order by codReferenciaProd desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_ReferenciaProd where codReferenciaProd > '" + codReferenciaProd + "'and  codEmpresa='" + codEmpresa + "' order by codReferenciaProd";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_ReferenciaProd where codReferenciaProd <= '9999'  and  codEmpresa='" + codEmpresa + "' order by codReferenciaProd desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblReferenciaProd");
            }
            catch (Exception) { }

            stbuilder.Append("select codReferenciaProd,nombre,descripcion,codEmpresa  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarReferenciaProd", ref dsData, "tblReferenciaProd");

            if (dsData.Tables["tblReferenciaProd"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblReferenciaProd"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba una referencia de producto
        /// </summary>
        public bool GrabarReferenciaProdo(string codReferenciaProd, string nombre, string descripcion, string codEmpresa)
        {
            DataSet ds = null;
            _ok = BuscarReferenciaProd(codReferenciaProd, ref ds, Navega.Ninguno, codEmpresa);
            codReferenciaProd = Strings.Right("000000" + codReferenciaProd, 6);
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);

            if (!_ok)
            {
                _stmysql = "insert into gp_ReferenciaProd(codReferenciaProd,nombre,descripcion,codEmpresa,usuarioSistema) values ('" + codReferenciaProd + "','" + nombre + "','" + descripcion + "','" + codEmpresa + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_ReferenciaProd set nombre='" + nombre + "', descripcion='" + descripcion + "'  where codReferenciaProd='" + codReferenciaProd + "'   and  codEmpresa='" + codEmpresa + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarReferenciaProd", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina una referencia de producto
        /// </summary>
        public bool EliminarReferenciaProd(string codReferenciaProd, string codEmpresa)
        {
            codReferenciaProd = Strings.Right("000000" + codReferenciaProd, 6);
            _stmysql = "delete from gp_ReferenciaProd where  codReferenciaProd='" + codReferenciaProd + "'  and   codEmpresa='" + codEmpresa + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarReferenciaProd", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de referencia de producto
        /// </summary>
        // public string HelpReferenciaProd(Form myforma, string filtro = "") // ERROR: CS0161
        // { // ERROR: CS1519 - method body for commented signature
        //     //return _msgSas.CargaAyuda(...)
        // }

        #endregion

        #region Seguimiento

        /// <summary>
        /// Busca un seguimiento
        /// </summary>
        public bool BuscarSeguimiento(string codTipoSeguimiento, string codEmpresa, string codigoseg, ref DataSet dsDataset, Navega navegar = Navega.Ninguno, string fechaSeguimiento = "")
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";

            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);

            if (navegar == Navega.Ninguno)
            {
                where = " from gp_SeguimitoEmp where codigoseg = " + codigoseg;
            }
            else if (navegar == Navega.Primero)
            {
                where = " from gp_SeguimitoEmp where codigoseg > ' ' and  codEmpresa='" + codEmpresa + "' and  codTipoSeguimiento='" + codTipoSeguimiento + "' order by codigoseg ";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_SeguimitoEmp where codigoseg < '" + codigoseg + "'and  codEmpresa='" + codEmpresa + "' and  codTipoSeguimiento='" + codTipoSeguimiento + "' order by codigoseg desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_SeguimitoEmp where codigoseg > '" + codigoseg + "'and  codEmpresa='" + codEmpresa + "' and  codTipoSeguimiento='" + codTipoSeguimiento + "' order by codigoseg ";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_SeguimitoEmp where codigoseg <= '9999'  and  codEmpresa='" + codEmpresa + "' and  codTipoSeguimiento='" + codTipoSeguimiento + "' order by codigoseg desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblTipoSeguimiento");
            }
            catch (Exception) { }

            stbuilder.Append("select codigoseg,codTipoSeguimiento,fechaSeguimiento,Porcentaje,observaciones " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarSeguimiento", ref dsData, "tblTipoSeguimiento");

            if (dsData.Tables["tblTipoSeguimiento"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblTipoSeguimiento"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Graba un seguimiento
        /// </summary>
        public bool GrabarSeguimiento(string codigoseg, string codEmpresa, string codTipoSeguimiento, string fechaSeguimiento, string porcentaje, string observaciones)
        {
            DataSet ds = null;
            _ok = BuscarSeguimiento(codTipoSeguimiento, codEmpresa, codigoseg, ref ds, Navega.Ninguno);
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);

            if (!_ok)
            {
                _stmysql = "insert into gp_SeguimitoEmp(codEmpresa,codTipoSeguimiento,fechaSeguimiento,Porcentaje,observaciones,usuarioSistema) values ('" + codEmpresa + "','" + codTipoSeguimiento + "','" + fechaSeguimiento + "','" + porcentaje + "','" + observaciones + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_SeguimitoEmp set codTipoSeguimiento='" + codTipoSeguimiento + "', fechaSeguimiento='" + fechaSeguimiento + "', Porcentaje=" + porcentaje + ", observaciones='" + observaciones + "'    where codEmpresa='" + codEmpresa + "'   and  codigoseg=" + codigoseg;
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarSeguimiento", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina un seguimiento
        /// </summary>
        public bool EliminarSeguimiento(string codigoseg, string codEmpresa)
        {
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);
            _stmysql = "delete from gp_SeguimitoEmp where  codigoseg='" + codigoseg + "'  and   codEmpresa='" + codEmpresa + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarSeguimiento", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de seguimiento
        /// </summary>
        public string HelpSeguimiento(Form myforma, string filtro = "")
        {
            return _msgSas.CargaAyuda("gp_seguimitoemp", "codigoseg", "fechaSeguimiento", "Porcentaje", _myconnect, myforma, "fecha Seguimiento", "Porcentaje", "", filtro);
        }

        #endregion

        #region Departamento Empresa

        /// <summary>
        /// Busca un departamento de empresa
        /// </summary>
        public bool BuscarDepartaEmp(string codDistribucion, ref DataSet dsDataset, Navega navegar = Navega.Ninguno, string codEmpresa = "")
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet dsData = new DataSet();
            string where = "";
            codDistribucion = Strings.Right("0000" + codDistribucion, 4);
            codEmpresa = Strings.Right("00000000" + codEmpresa, 8);

            if (navegar == Navega.Ninguno && codEmpresa != "00000000")
            {
                where = " from gp_DistribuEmpresa where codDistribucion= '" + codDistribucion + "' and  codEmpresa            ='" + codEmpresa + "'";
            }
            else
            {
                where = " from gp_DistribuEmpresa where codDistribucion= '" + codDistribucion + "'";
            }

            if (navegar == Navega.Primero)
            {
                where = " from gp_DistribuEmpresa where codDistribucion > ' ' and  codEmpresa='" + codEmpresa + "' order by codDistribucion";
            }
            else if (navegar == Navega.Anterior)
            {
                where = " from gp_DistribuEmpresa where codDistribucion < '" + codDistribucion + "'and  codEmpresa='" + codEmpresa + "' order by codDistribucion desc ";
            }
            else if (navegar == Navega.Siguiente)
            {
                where = " from gp_DistribuEmpresa where codDistribucion > '" + codDistribucion + "'and  codEmpresa='" + codEmpresa + "' order by codDistribucion";
            }
            else if (navegar == Navega.Ultimo)
            {
                where = " from gp_DistribuEmpresa where codDistribucion <= '9999'  and  codEmpresa='" + codEmpresa + "' order by codDistribucion desc ";
            }

            try
            {
                dsDataset?.Tables.Remove("tblDepartaEmp");
            }
            catch (Exception) { }

            stbuilder.Append("select codDistribucion,nombre,descripcion,codEmpresa  " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarDepartaEmp", ref dsData, "tblDepartaEmp");

            if (dsData.Tables["tblDepartaEmp"].Rows.Count > 0)
            {
                try
                {
                    dsDataset.Tables.Add(dsData.Tables["tblDepartaEmp"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Busca todos los departamentos productivos
        /// </summary>
        public void BuscarTodolosDepartamentoProductivo(ref string numeroDepartamentoProductivo)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string where = " from gp_DistribuEmpresa order by codDistribucion desc ";
            stbuilder.Append("select codDistribucion  as campo1  " + where);
            _ok = _odbcConnect.ExecuteQueryconec(stbuilder.ToString(), _myconnect, "buscarTodolosDepartamentoProductivo", ref _accionsql, ref numeroDepartamentoProductivo);
        }

        /// <summary>
        /// Graba un departamento de empresa
        /// </summary>
        public bool GrabarDepartaEmpo(string codDistribucion, string nombre, string descripcion, string codEmpresa)
        {
            DataSet ds = null;
            _ok = BuscarDepartaEmp(codDistribucion, ref ds, Navega.Ninguno, codEmpresa);
            codDistribucion = Strings.Right("0000" + codDistribucion, 4);

            if (!_ok)
            {
                _stmysql = "insert into gp_DistribuEmpresa(codDistribucion,nombre,descripcion,codEmpresa,usuarioSistema) values ('" + codDistribucion + "','" + nombre + "','" + descripcion + "','" + codEmpresa + "','" + _usuarioSistema + "')";
                _accionsql = "insert";
            }
            else
            {
                _accionsql = "update";
                _stmysql = "update gp_DistribuEmpresa set nombre='" + nombre + "', descripcion='" + descripcion + "'  where codDistribucion='" + codDistribucion + "'   and  codEmpresa='" + codEmpresa + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarDepartaEmp", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Elimina un departamento de empresa
        /// </summary>
        public bool EliminarDepartaEmp(string codDistribucion, string codEmpresa)
        {
            codDistribucion = Strings.Right("0000" + codDistribucion, 4);
            _stmysql = "delete from gp_DistribuEmpresa where  codDistribucion='" + codDistribucion + "'  and   codEmpresa='" + codEmpresa + "'";
            _accionsql = "delete";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarDepartaEmp", ref _accionsql);
            return _ok;
        }

        /// <summary>
        /// Muestra la ayuda de departamento de empresa
        /// </summary>
        // public string HelpDepartaEmp(Form myforma, string filtro = "") // ERROR: CS0161
        // { // ERROR: CS1513 - method body for commented signature
        //     //return _msgSas.CargaAyuda(...)
        // }

        #endregion

        #region Asociado Empresa

        /// <summary>
        /// Busca asociados de una empresa
        /// </summary>
        // public bool BuscarAsociadoEmp(string codDistribucion, ref DataSet dsDataset) // ERROR: CS0106, CS8803
        // { // ERROR: CS0106 - method body for commented signature
            // System.Text.StringBuilder stbuilder = new System.Text.StringBuilder(); // ERROR: CS0106 - method body for commented signature
            // DataSet dsData = new DataSet(); // ERROR: CS0106 - method body for commented signature
            // codDistribucion = Strings.Right("0000" + codDistribucion, 4); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // string where = " from gp_SocioDistibuEmp a inner  join sys_maenit b on a.codigoter = b.codigoter where a.codDistribucion= '" + codDistribucion + "'"; // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // try // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // dsDataset?.Tables.Remove("tblSocioEmp"); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // catch (Exception) { } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("Select a.codDistribucion,b.codigoter,b.NOMBRE  as  nombreasociado,a.representante,a.desplazada,a.vulnerable,a.codestadoEmpre" + where); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarAsociadoEmp", ref dsData, "tblSocioEmp"); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // try // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // dsDataset.Tables.Add(dsData.Tables["tblSocioEmp"].Copy()); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // catch (Exception) { } // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Busca un asociado especifico de una empresa
        /// </summary>
        public virtual bool BuscarAsociadoEmp(string codDistribucion, string codigoter, string buscarActivo = "")
        {
            string nomempresa = "";
            return BuscarAsociadoEmp(codDistribucion, codigoter, buscarActivo, ref nomempresa);
        }

        public virtual bool BuscarAsociadoEmp(string codDistribucion, string codigoter, string buscarActivo, ref string nomempresa)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string where = "";
            codDistribucion = Strings.Right("0000" + codDistribucion, 4);
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            if (string.IsNullOrEmpty(buscarActivo))
            {
                where = " from gp_SocioDistibuEmp a   where a.codDistribucion= '" + codDistribucion + "' and  a.codigoter='" + codigoter + "'";
            }
            else
            {
                where = "  ,c.nombre as campo3 from gp_SocioDistibuEmp a  inner join gp_distribuempresa b  on b.codDistribucion = a.codDistribucion  inner join gp_empresaprod c  on c.codEmpresa = b.codEmpresa  where a.codigoter='" + codigoter + "'";
            }

            stbuilder.Append("Select a.codDistribucion as campo1,a.codigoter as campo2   " + where);

            string campo1 = "", campo2 = "", campo4 = "";
            _ok = _odbcConnect.ExecuteQueryconec(stbuilder.ToString(), _myconnect, "BuscarAsociadoEmp", ref _accionsql, ref campo1, ref campo2, ref nomempresa, ref campo4);
            return _ok;
        }

        /// <summary>
        /// Graba asociados de empresa
        /// </summary>
        // public bool GrabarAsociadoEmp(DataSet dsDataset, string fechaActual = "") // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // string codDistribucion, codigoter, codestadoEmpre; // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // for (int i = 0; i < dsDataset.Tables["tblSocioEmp"].Rows.Count; i++) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // string nomempresa = ""; // ERROR: CS0106 - method body for commented signature
                // _ok = BuscarAsociadoEmp(dsDataset.Tables["tblSocioEmp"].Rows[i][0].ToString(), dsDataset.Tables["tblSocioEmp"].Rows[i][1].ToString(), "", ref nomempresa); // ERROR: CS0106 - method body for commented signature
                // codDistribucion = Strings.Right("0000" + dsDataset.Tables["tblSocioEmp"].Rows[i][0].ToString(), 4); // ERROR: CS0106 - method body for commented signature
                // codigoter = Strings.Right("00000000000000" + dsDataset.Tables["tblSocioEmp"].Rows[i][1].ToString(), 14); // ERROR: CS0106 - method body for commented signature
                // codestadoEmpre = Strings.Right("0000" + dsDataset.Tables["tblSocioEmp"].Rows[i][6].ToString(), 4); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
                // if (!_ok) // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // _stmysql = "insert into gp_SocioDistibuEmp(codDistribucion,codigoter,representante,desplazada,vulnerable,codestadoEmpre,usuarioSistema) values ('" + codDistribucion + "','" + codigoter + "','" + dsDataset.Tables["tblSocioEmp"].Rows[i][3].ToString() + "','" + dsDataset.Tables["tblSocioEmp"].Rows[i][4].ToString() + "','" + dsDataset.Tables["tblSocioEmp"].Rows[i][5].ToString() + "','" + codestadoEmpre + "','" + _usuarioSistema + "')"; // ERROR: CS0106 - method body for commented signature
                    // _accionsql = "insert"; // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // else // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // _accionsql = "update"; // ERROR: CS0106 - method body for commented signature
                    // if (dsDataset.Tables["tblSocioEmp"].Rows[i][4].ToString() == "0002" || dsDataset.Tables["tblSocioEmp"].Rows[i][4].ToString() == "0003") // ERROR: CS0106 - method body for commented signature
                    // { // ERROR: CS0106 - method body for commented signature
                        // _stmysql = "update gp_SocioDistibuEmp set representante='" + dsDataset.Tables["tblSocioEmp"].Rows[i][3].ToString() + "', fechaRetiroTerm='" + fechaActual + "',desplazada='" + dsDataset.Tables["tblSocioEmp"].Rows[i][4].ToString() + "',vulnerable='" + dsDataset.Tables["tblSocioEmp"].Rows[i][5].ToString() + "', codestadoEmpre='" + codestadoEmpre + "'  where codDistribucion='" + codDistribucion + "'   and  codigoter='" + codigoter + "'"; // ERROR: CS0106 - method body for commented signature
                    // } // ERROR: CS0106 - method body for commented signature
                    // else // ERROR: CS0106 - method body for commented signature
                    // { // ERROR: CS0106 - method body for commented signature
                        // _stmysql = "update gp_SocioDistibuEmp set representante='" + dsDataset.Tables["tblSocioEmp"].Rows[i][3].ToString() + "',desplazada='" + dsDataset.Tables["tblSocioEmp"].Rows[i][4].ToString() + "',vulnerable='" + dsDataset.Tables["tblSocioEmp"].Rows[i][5].ToString() + "', codestadoEmpre='" + codestadoEmpre + "'  where codDistribucion='" + codDistribucion + "'   and  codigoter='" + codigoter + "'"; // ERROR: CS0106 - method body for commented signature
                    // } // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
                // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarAsociadoEmp", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Elimina un asociado de empresa
        /// </summary>
        // public bool EliminarAsociadoEmpresa(int indice, DataSet dsdata) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // DataRow row = dsdata.Tables["tblSocioEmp"].Rows[indice]; // ERROR: CS0106 - method body for commented signature
            // string codDistribucion = Strings.Right("0000" + row["codDistribucion"].ToString(), 4); // ERROR: CS0106 - method body for commented signature
            // string codigoter = Strings.Right("00000000000000" + row["codigoter"].ToString(), 14); // ERROR: CS0106 - method body for commented signature
            // _accionsql = "delete"; // ERROR: CS0106 - method body for commented signature
            // _stmysql = "delete from gp_SocioDistibuEmp  where codDistribucion='" + codDistribucion + "' and codigoter='" + codigoter + "'"; // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarAsociadoEmpresa", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        #endregion

        #region Proceso Productivo

        /// <summary>
        /// Busca un proceso productivo
        /// </summary>
        // public bool BuscarProcesoProductivo(string codigoProceProd, string codEmpresa, ref DataSet dsDataset, Navega navegar = Navega.Ninguno) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // System.Text.StringBuilder stbuilder = new System.Text.StringBuilder(); // ERROR: CS0106 - method body for commented signature
            // DataSet dsData = new DataSet(); // ERROR: CS0106 - method body for commented signature
            // string where = ""; // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // codEmpresa = Strings.Right("00000000" + codEmpresa, 8); // ERROR: CS0106 - method body for commented signature
            // codigoProceProd = Strings.Right("00000000" + codigoProceProd, 8); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (navegar == Navega.Ninguno) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_procesoprodu where   codEmpresa='" + codEmpresa + "' and  codigoProceProd='" + codigoProceProd + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Primero) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_procesoprodu where codigoProceProd > ' ' and  codEmpresa='" + codEmpresa + "'  order by codigoProceProd"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Anterior) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_procesoprodu where codigoProceProd < '" + codigoProceProd + "'and  codEmpresa='" + codEmpresa + "'  order by codigoProceProd desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Siguiente) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_procesoprodu where codigoProceProd > '" + codigoProceProd + "'and  codEmpresa='" + codEmpresa + "'  order by codigoProceProd"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Ultimo) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_procesoprodu where codigoProceProd <= '9999'  and  codEmpresa='" + codEmpresa + "' order by codigoProceProd desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // try // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // dsDataset?.Tables.Remove("tblProceProd"); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // catch (Exception) { } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("select codigoProceProd,fechaProducion,fechaVenta,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("fechaFacturacion,fechaProducion,codReferenciaProd,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("cantidadVendidad,Facturacion,costoProduccion,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("UtilidadNegocio,NumTrabajadores,DiasTrabajados,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("ingresosXpersonas,ingresosemilla,ingresoComercial,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("ingresoFondoSemilla,observaciones   " + where); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarProcesoProductivo", ref dsData, "tblProceProd"); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (dsData.Tables["tblProceProd"].Rows.Count > 0) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // try // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // dsDataset.Tables.Add(dsData.Tables["tblProceProd"].Copy()); // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // catch (Exception) { } // ERROR: CS0106 - method body for commented signature
                // return true; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // return false; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Graba un proceso productivo
        /// </summary>
        // public bool GrabarProcesoProductivo(DataSet dsDataset) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // DataRow row = dsDataset.Tables["tblProceProd"].Rows[0]; // ERROR: CS0106 - method body for commented signature
            // DataSet ds = null; // ERROR: CS0106 - method body for commented signature
            // _ok = BuscarProcesoProductivo(row["codigoProceProd"].ToString(), row["codEmpresa"].ToString(), ref ds, Navega.Ninguno); // ERROR: CS0106 - method body for commented signature
            // string codEmpresa = Strings.Right("00000000" + row["codEmpresa"].ToString(), 8); // ERROR: CS0106 - method body for commented signature
            // string codigoProceProd = Strings.Right("00000000" + row["codigoProceProd"].ToString(), 8); // ERROR: CS0106 - method body for commented signature
            // string codReferenciaProd = Strings.Right("000000" + row["codReferenciaProd"].ToString(), 6); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (!_ok) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _stmysql = "insert into gp_procesoprodu(codigoProceProd,codEmpresa,fechaProducion,fechaVenta," + // ERROR: CS0106 - method body for commented signature
                          // "fechaFacturacion,codReferenciaProd,cantidadVendidad," + // ERROR: CS0106 - method body for commented signature
                          // "Facturacion,costoProduccion,UtilidadNegocio,NumTrabajadores," + // ERROR: CS0106 - method body for commented signature
                          // "DiasTrabajados,ingresosXpersonas,ingresosemilla,ingresoComercial,ingresoFondoSemilla," + // ERROR: CS0106 - method body for commented signature
                          // "observaciones,usuarioSistema) values ('" + codigoProceProd + "','" + codEmpresa + "','" + // ERROR: CS0106 - method body for commented signature
                          // row["fechaProducion"].ToString() + "','" + row["fechaVenta"].ToString() + "','" + // ERROR: CS0106 - method body for commented signature
                          // row["fechaFacturacion"].ToString() + "','" + codReferenciaProd + "'," + // ERROR: CS0106 - method body for commented signature
                          // row["cantidadVendidad"].ToString() + "," + row["Facturacion"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                          // row["costoProduccion"].ToString() + "," + row["UtilidadNegocio"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                          // row["NumTrabajadores"].ToString() + "," + row["DiasTrabajados"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                          // row["ingresosXpersonas"].ToString() + "," + row["ingresosemilla"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                          // row["ingresoComercial"].ToString() + "," + row["ingresoFondoSemilla"].ToString() + ",'" + // ERROR: CS0106 - method body for commented signature
                          // row["observaciones"].ToString() + "','" + UsuarioSistema + "')"; // ERROR: CS0106 - method body for commented signature
                // _accionsql = "insert"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _accionsql = "update"; // ERROR: CS0106 - method body for commented signature
                // _stmysql = "update gp_procesoprodu set fechaProducion='" + row["fechaProducion"].ToString() + "',fechaVenta='" + row["fechaVenta"].ToString() + // ERROR: CS0106 - method body for commented signature
                          // "',fechaFacturacion='" + row["fechaFacturacion"].ToString() + "',codReferenciaProd='" + codReferenciaProd + // ERROR: CS0106 - method body for commented signature
                          // "',cantidadVendidad=" + row["cantidadVendidad"].ToString() + ",Facturacion=" + row["Facturacion"].ToString() + // ERROR: CS0106 - method body for commented signature
                          // ",costoProduccion=" + row["costoProduccion"].ToString() + ",UtilidadNegocio=" + row["UtilidadNegocio"].ToString() + // ERROR: CS0106 - method body for commented signature
                          // ",NumTrabajadores=" + row["NumTrabajadores"].ToString() + ",DiasTrabajados=" + row["DiasTrabajados"].ToString() + // ERROR: CS0106 - method body for commented signature
                          // ",ingresosXpersonas=" + row["ingresosXpersonas"].ToString() + ",ingresosemilla=" + row["ingresosemilla"].ToString() + // ERROR: CS0106 - method body for commented signature
                          // ",ingresoComercial=" + row["ingresoComercial"].ToString() + ",ingresoFondoSemilla=" + row["ingresoFondoSemilla"].ToString() + // ERROR: CS0106 - method body for commented signature
                          // ",observaciones='" + row["observaciones"].ToString() + "',usuarioSistema='" + UsuarioSistema + "'    where codigoProceProd='" + codigoProceProd + "' and codEmpresa='" + codEmpresa + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarProcesoProductivo", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Elimina un proceso productivo
        /// </summary>
        // public bool EliminarProdProductivo(string codigoProceProd, string codEmpresa) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // codEmpresa = Strings.Right("00000000" + codEmpresa, 8); // ERROR: CS0106 - method body for commented signature
            // codigoProceProd = Strings.Right("00000000" + codigoProceProd, 8); // ERROR: CS0106 - method body for commented signature
            // _stmysql = "delete from gp_procesoprodu where  codigoProceProd='" + codigoProceProd + "' and codEmpresa='" + codEmpresa + "'"; // ERROR: CS0106 - method body for commented signature
            // _accionsql = "delete"; // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, " EliminarProdProductivo", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Muestra la ayuda de proceso productivo
        /// </summary>
        // public string HelpProdProductivo(Form myforma, string filtro = "") // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // return string.Empty; // ERROR: CS0106 - method body for commented signature
            //return _msgSas.CargaAyuda("gp_procesoprodu", "codigoProceProd", "fechacreareg", "codEmpresa", _myconnect, myforma, "codigo Proceso Produc", "Fecha Del Proceso", "", filtro, "", "", "Codigo  Empresa");
        // } // ERROR: CS0106 - method body for commented signature

        #endregion

        #region Plan de Negocio

        /// <summary>
        /// Busca un plan de negocio
        /// </summary>
        // public bool BuscarPlandeNegocio(string codPlanNegocio, ref DataSet dsDataset, Navega navegar = Navega.Ninguno, string codEmpresa = "") // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // System.Text.StringBuilder stbuilder = new System.Text.StringBuilder(); // ERROR: CS0106 - method body for commented signature
            // DataSet dsData = new DataSet(); // ERROR: CS0106 - method body for commented signature
            // string where = ""; // ERROR: CS0106 - method body for commented signature
            // codPlanNegocio = Strings.Right("00000000" + codPlanNegocio, 8); // ERROR: CS0106 - method body for commented signature
            // codEmpresa = Strings.Right("00000000" + codEmpresa, 8); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (navegar == Navega.Ninguno && codEmpresa != "00000000") // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_plannegocio where codPlanNegocio= '" + codPlanNegocio + "' and  codEmpresa            ='" + codEmpresa + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_plannegocio where codPlanNegocio= '" + codPlanNegocio + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (navegar == Navega.Primero) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_plannegocio where codPlanNegocio > ' ' and  codEmpresa='" + codEmpresa + "' order by codPlanNegocio"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Anterior) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_plannegocio where codPlanNegocio < '" + codPlanNegocio + "'and  codEmpresa='" + codEmpresa + "' order by codPlanNegocio desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Siguiente) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_plannegocio where codPlanNegocio > '" + codPlanNegocio + "'and  codEmpresa='" + codEmpresa + "' order by codPlanNegocio"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Ultimo) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_plannegocio where codPlanNegocio <= '9999'  and  codEmpresa='" + codEmpresa + "' order by codPlanNegocio desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // try // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // dsDataset?.Tables.Remove("tblplandenegocio"); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // catch (Exception) { } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("select codPlanNegocio,Descripcion,Ubicacion,codEmpresa  " + where); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarPlandeNegocio", ref dsData, "tblplandenegocio"); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (dsData.Tables["tblplandenegocio"].Rows.Count > 0) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // try // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // dsDataset.Tables.Add(dsData.Tables["tblplandenegocio"].Copy()); // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // catch (Exception) { } // ERROR: CS0106 - method body for commented signature
                // return true; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // return false; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Graba un plan de negocio
        /// </summary>
        // public bool GrabarPlanNegocion(string codPlanNegocio, string codEmpresa, string descripcion, string ubicacion) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // DataSet ds = null; // ERROR: CS0106 - method body for commented signature
            // _ok = BuscarPlandeNegocio(codPlanNegocio, ref ds, Navega.Ninguno, codEmpresa); // ERROR: CS0106 - method body for commented signature
            // codPlanNegocio = Strings.Right("00000000" + codPlanNegocio, 8); // ERROR: CS0106 - method body for commented signature
            // codEmpresa = Strings.Right("00000000" + codEmpresa, 8); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (!_ok) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _stmysql = "insert into gp_plannegocio(codPlanNegocio,codEmpresa,Descripcion,Ubicacion,usuarioSistema) values ('" + codPlanNegocio + "','" + codEmpresa + "','" + descripcion + "','" + ubicacion + "','" + _usuarioSistema + "')"; // ERROR: CS0106 - method body for commented signature
                // _accionsql = "insert"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _accionsql = "update"; // ERROR: CS0106 - method body for commented signature
                // _stmysql = "update gp_plannegocio set Ubicacion='" + ubicacion + "', Descripcion='" + descripcion + "'  where codPlanNegocio='" + codPlanNegocio + "'   and  codEmpresa='" + codEmpresa + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarPlanNegocion", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Elimina un plan de negocio
        /// </summary>
        // public bool EliminarPlanNegocion(string codPlanNegocio, string codEmpresa) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // codPlanNegocio = Strings.Right("00000000" + codPlanNegocio, 8); // ERROR: CS0106 - method body for commented signature
            // codEmpresa = Strings.Right("00000000" + codEmpresa, 8); // ERROR: CS0106 - method body for commented signature
            // _stmysql = "delete from gp_plannegocio where  codPlanNegocio='" + codPlanNegocio + "'  and   codEmpresa='" + codEmpresa + "'"; // ERROR: CS0106 - method body for commented signature
            // _accionsql = "delete"; // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarPlanNegocion", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Muestra la ayuda de plan de negocio
        /// </summary>
        // public string HelpPlanNegocio(Form myforma, string filtro = "") // ERROR: CS0161
        // { // ERROR: CS0106 - method body for commented signature
            //return _msgSas.CargaAyuda("gp_plannegocio", "codPlanNegocio", "Descripcion", "codEmpresa", _myconnect, myforma, "codigo PlanNegoicion", "", "", filtro, "", "", "Codigo  Empresa");
        // } // ERROR: CS0106 - method body for commented signature

        #endregion

        #region Estado General

        /// <summary>
        /// Busca un estado general
        /// </summary>
        // public bool BuscarEstadoGeneral(string codEmpresa, string fechaestadogeneral, ref DataSet dsDataset, Navega navegar = Navega.Ninguno) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // System.Text.StringBuilder stbuilder = new System.Text.StringBuilder(); // ERROR: CS0106 - method body for commented signature
            // DataSet dsData = new DataSet(); // ERROR: CS0106 - method body for commented signature
            // string where = ""; // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // codEmpresa = Strings.Right("00000000" + codEmpresa, 8); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (navegar == Navega.Ninguno) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_estadogeneral where   codEmpresa='" + codEmpresa + "' and  fechaestadogeneral='" + fechaestadogeneral + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Primero) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_estadogeneral where fechaestadogeneral > ' ' and  codEmpresa='" + codEmpresa + "'  order by fechaestadogeneral"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Anterior) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_estadogeneral where fechaestadogeneral < '" + fechaestadogeneral + "'and  codEmpresa='" + codEmpresa + "'  order by fechaestadogeneral desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Siguiente) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_estadogeneral where fechaestadogeneral > '" + fechaestadogeneral + "'and  codEmpresa='" + codEmpresa + "'  order by fechaestadogeneral"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Ultimo) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_estadogeneral where fechaestadogeneral <= '9999'  and  codEmpresa='" + codEmpresa + "' order by fechaestadogeneral desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // try // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // dsDataset?.Tables.Remove("tblEstadoGeneral"); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // catch (Exception) { } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("select codEmpresa,fechaestadogeneral,MesesProyecta,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("ventasContac,VentaCretidos,SalaYmanObra,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("MateriaPrima,GastoFabrica,Publicidad,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("OtroCostoProduccion,AlquilerLocal,ServiciosPublicos,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("GastosFinaciero,Impuesto,SeguridadSocial,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("OtrosGasto   " + where); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarEstadoGeneral", ref dsData, "tblEstadoGeneral"); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (dsData.Tables["tblEstadoGeneral"].Rows.Count > 0) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // try // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // dsDataset.Tables.Add(dsData.Tables["tblEstadoGeneral"].Copy()); // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // catch (Exception) { } // ERROR: CS0106 - method body for commented signature
                // return true; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // return false; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Graba un estado general
        /// </summary>
        // public bool GrabarEstadoGeneral(DataSet dsDataset) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // DataRow row = dsDataset.Tables["tblEstadoGeneral"].Rows[0]; // ERROR: CS0106 - method body for commented signature
            // DataSet ds = null; // ERROR: CS0106 - method body for commented signature
            // _ok = BuscarEstadoGeneral(row["codEmpresa"].ToString(), row["fechaestadogeneral"].ToString(), ref ds, Navega.Ninguno); // ERROR: CS0106 - method body for commented signature
            // string codEmpresa = Strings.Right("00000000" + row["codEmpresa"].ToString(), 8); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (!_ok) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _stmysql = "insert into gp_estadogeneral(codEmpresa,fechaestadogeneral,MesesProyecta," + // ERROR: CS0106 - method body for commented signature
                            // "ventasContac,VentaCretidos,SalaYmanObra," + // ERROR: CS0106 - method body for commented signature
                            // "MateriaPrima,GastoFabrica,Publicidad," + // ERROR: CS0106 - method body for commented signature
                            // "OtroCostoProduccion,AlquilerLocal,ServiciosPublicos," + // ERROR: CS0106 - method body for commented signature
                            // "GastosFinaciero,Impuesto,SeguridadSocial,OtrosGasto,usuarioSistema) " + // ERROR: CS0106 - method body for commented signature
                            // " values ('" + codEmpresa + "','" + // ERROR: CS0106 - method body for commented signature
                            // row["fechaestadogeneral"].ToString() + "'," + row["MesesProyecta"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                            // row["ventasContac"].ToString() + "," + row["VentaCretidos"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                            // row["SalaYmanObra"].ToString() + "," + row["MateriaPrima"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                            // row["GastoFabrica"].ToString() + "," + row["Publicidad"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                            // row["OtroCostoProduccion"].ToString() + "," + row["AlquilerLocal"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                            // row["ServiciosPublicos"].ToString() + "," + row["GastosFinaciero"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                            // row["Impuesto"].ToString() + "," + row["SeguridadSocial"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                            // row["OtrosGasto"].ToString() + ",'" + UsuarioSistema + "')"; // ERROR: CS0106 - method body for commented signature
                // _accionsql = "insert"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _accionsql = "update"; // ERROR: CS0106 - method body for commented signature
                // _stmysql = "update gp_estadogeneral set MesesProyecta=" + row["MesesProyecta"].ToString() + // ERROR: CS0106 - method body for commented signature
                           // ",ventasContac=" + row["ventasContac"].ToString() + ",VentaCretidos=" + row["VentaCretidos"].ToString() + // ERROR: CS0106 - method body for commented signature
                           // ",SalaYmanObra=" + row["SalaYmanObra"].ToString() + ",MateriaPrima=" + row["MateriaPrima"].ToString() + // ERROR: CS0106 - method body for commented signature
                           // ",GastoFabrica=" + row["GastoFabrica"].ToString() + ",Publicidad=" + row["Publicidad"].ToString() + // ERROR: CS0106 - method body for commented signature
                           // ",OtroCostoProduccion=" + row["OtroCostoProduccion"].ToString() + ",AlquilerLocal=" + row["AlquilerLocal"].ToString() + // ERROR: CS0106 - method body for commented signature
                           // ",ServiciosPublicos=" + row["ServiciosPublicos"].ToString() + ",GastosFinaciero=" + row["GastosFinaciero"].ToString() + // ERROR: CS0106 - method body for commented signature
                           // ",Impuesto=" + row["Impuesto"].ToString() + ",SeguridadSocial=" + row["SeguridadSocial"].ToString() + // ERROR: CS0106 - method body for commented signature
                           // ",OtrosGasto=" + row["OtrosGasto"].ToString() + ",usuarioSistema='" + UsuarioSistema + "'    where codEmpresa='" + codEmpresa + "' and fechaestadogeneral='" + row["fechaestadogeneral"].ToString() + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarEstadoGeneral", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Elimina un estado general
        /// </summary>
        // public bool EliminarEstadoGeneral(string codEmpresa, string fechaestadogeneral) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // codEmpresa = Strings.Right("00000000" + codEmpresa, 8); // ERROR: CS0106 - method body for commented signature
            // _stmysql = "delete from gp_estadogeneral where codEmpresa='" + codEmpresa + "' and fechaestadogeneral='" + fechaestadogeneral + "'"; // ERROR: CS0106 - method body for commented signature
            // _accionsql = "delete"; // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarEstadoGeneral", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Muestra la ayuda de estado general
        /// </summary>
        // public string HelpEstadoGeneral(Form myforma, string filtro = "") // ERROR: CS0161
        // { // ERROR: CS0106 - method body for commented signature
            //return _msgSas.CargaAyuda("gp_estadogeneral", "fechaestadogeneral", "codEmpresa", "MesesProyecta", _myconnect, myforma, "codigoEmpresa", "FechaCreacion", "", filtro, "", "", "Meses a Proyectar");
        // } // ERROR: CS0106 - method body for commented signature

        #endregion

        #region Inversion

        /// <summary>
        /// Busca una inversion
        /// </summary>
        // public bool BuscarInversion(string codInventario, string codReferenciaProd, ref DataSet dsDataset, Navega navegar = Navega.Ninguno) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // System.Text.StringBuilder stbuilder = new System.Text.StringBuilder(); // ERROR: CS0106 - method body for commented signature
            // DataSet dsData = new DataSet(); // ERROR: CS0106 - method body for commented signature
            // string where = ""; // ERROR: CS0106 - method body for commented signature
            // codInventario = Strings.Right("000000" + codInventario, 6); // ERROR: CS0106 - method body for commented signature
            // codReferenciaProd = Strings.Right("000000" + codReferenciaProd, 6); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (navegar == Navega.Ninguno) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_inventarioemp where   codInventario ='" + codInventario + "' and  codReferenciaProd='" + codReferenciaProd + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Primero) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_inventarioemp where codInventario > ' ' and  codReferenciaProd='" + codReferenciaProd + "'  order by codInventario"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Anterior) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_inventarioemp where codInventario < '" + codInventario + "'and  codReferenciaProd='" + codReferenciaProd + "'  order by codInventario desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Siguiente) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_inventarioemp where codInventario > '" + codInventario + "'and  codReferenciaProd='" + codReferenciaProd + "'  order by codInventario"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Ultimo) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_inventarioemp where codInventario <= '9999'  and  codReferenciaProd='" + codReferenciaProd + "' order by codInventario desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // try // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // dsDataset?.Tables.Remove("tblInversion"); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // catch (Exception) { } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("select codInventario,codReferenciaProd,codTipoInventario,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("Descripcion " + where); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarInversion", ref dsData, "tblInversion"); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (dsData.Tables["tblInversion"].Rows.Count > 0) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // try // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // dsDataset.Tables.Add(dsData.Tables["tblInversion"].Copy()); // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // catch (Exception) { } // ERROR: CS0106 - method body for commented signature
                // return true; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // return false; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Graba una inversion
        /// </summary>
        // public bool GrabarInversion(string codInventario, string codReferenciaProd, string codTipoInventario, string descripcion) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // DataSet ds = null; // ERROR: CS0106 - method body for commented signature
            // _ok = BuscarInversion(codInventario, codReferenciaProd, ref ds, Navega.Ninguno); // ERROR: CS0106 - method body for commented signature
            // codInventario = Strings.Right("000000" + codInventario, 6); // ERROR: CS0106 - method body for commented signature
            // codReferenciaProd = Strings.Right("000000" + codReferenciaProd, 6); // ERROR: CS0106 - method body for commented signature
            // codTipoInventario = Strings.Right("00" + codTipoInventario, 2); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (!_ok) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _stmysql = "insert into gp_inventarioemp(codInventario,codReferenciaProd,codTipoInventario,Descripcion,usuarioSistema) values ('" + codInventario + "','" + codReferenciaProd + "','" + codTipoInventario + "','" + descripcion + "','" + _usuarioSistema + "')"; // ERROR: CS0106 - method body for commented signature
                // _accionsql = "insert"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // _accionsql = "update"; // ERROR: CS0106 - method body for commented signature
                // _stmysql = "update gp_inventarioemp set codReferenciaProd='" + codReferenciaProd + "', codTipoInventario='" + codTipoInventario + "',Descripcion='" + descripcion + "', codTipoInventario='" + codTipoInventario + "'  where codInventario='" + codInventario + "'"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarInversion", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Elimina una inversion
        /// </summary>
        // public bool EliminarInversion(string codInventario, string codReferenciaProd) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // codInventario = Strings.Right("000000" + codInventario, 6); // ERROR: CS0106 - method body for commented signature
            // codReferenciaProd = Strings.Right("000000" + codReferenciaProd, 6); // ERROR: CS0106 - method body for commented signature
            // _stmysql = "delete from gp_estadogeneral where codInventario='" + codInventario + "' and codReferenciaProd='" + codReferenciaProd + "'"; // ERROR: CS0106 - method body for commented signature
            // _accionsql = "delete"; // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarInversion", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Muestra la ayuda de inversion
        /// </summary>
        // public string HelpInversion(Form myforma, string filtro = "") // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // return string.Empty;// _msgSas.CargaAyuda("gp_inventarioemp", "codInventario", "codReferenciaProd", "Descripcion", _myconnect, myforma, "codigo inversion", "codigo_Referencia ", "", filtro, "", "", "Descripcion"); // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        #endregion

        #region Precio Inversion

        /// <summary>
        /// Busca precios de inversion
        /// </summary>
        // public bool BuscarPrecioInversion(string codInventario, ref DataSet dsDataset, Navega navegar = Navega.Ninguno, string codPrecioInversion = "9999") // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // System.Text.StringBuilder stbuilder = new System.Text.StringBuilder(); // ERROR: CS0106 - method body for commented signature
            // DataSet dsData = new DataSet(); // ERROR: CS0106 - method body for commented signature
            // string where = ""; // ERROR: CS0106 - method body for commented signature
            // codInventario = Strings.Right("000000" + codInventario, 6); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (navegar == Navega.Ninguno) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // if (codPrecioInversion != "9999" && !string.IsNullOrWhiteSpace(codPrecioInversion)) // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // where = " from gp_precioinversion where   codInventario ='" + codInventario + "' and codPrecioInversion =" + codPrecioInversion; // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // else if (codPrecioInversion == "9999") // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // where = " from gp_precioinversion where   codInventario ='" + codInventario + "'"; // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // else // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // where = " from gp_precioinversion where   codInventario ='" + codInventario + "' and codPrecioInversion = 9999"; // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Primero) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_precioinversion where codInventario > ' '   order by codInventario"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Anterior) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_precioinversion where codInventario < '" + codInventario + "'  order by codInventario desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Siguiente) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_precioinversion where codInventario > '" + codInventario + "'  order by codInventario"; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // else if (navegar == Navega.Ultimo) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // where = " from gp_precioinversion where codInventario <= '9999'order by codInventario desc "; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // try // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // dsDataset?.Tables.Remove("tblPrecioInversion"); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // catch (Exception) { } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("select codPrecioInversion,nombreProd,cantidad,"); // ERROR: CS0106 - method body for commented signature
            // stbuilder.Append("valorUnitario " + where); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), _myconnect, "BuscarPrecioInversion", ref dsData, "tblPrecioInversion"); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
            // if (dsData.Tables["tblPrecioInversion"].Rows.Count > 0) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // try // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // dsDataset.Tables.Add(dsData.Tables["tblPrecioInversion"].Copy()); // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // catch (Exception) { } // ERROR: CS0106 - method body for commented signature
                // return true; // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // return false; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Graba precios de inversion
        /// </summary>
        // public bool GrabarPrecioInversion(DataSet dsDataset) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // for (int i = 0; i < dsDataset.Tables["tblPrecioInversion"].Rows.Count; i++) // ERROR: CS0106 - method body for commented signature
            // { // ERROR: CS0106 - method body for commented signature
                // DataRow row = dsDataset.Tables["tblPrecioInversion"].Rows[i]; // ERROR: CS0106 - method body for commented signature
                // DataSet ds = null; // ERROR: CS0106 - method body for commented signature
                // _ok = BuscarPrecioInversion(row["codInventario"].ToString(), ref ds, Navega.Ninguno, row["codPrecioInversion"].ToString()); // ERROR: CS0106 - method body for commented signature
                // string codInventario = Strings.Right("000000" + row["codInventario"].ToString(), 6); // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
                // if (!_ok) // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // _stmysql = "insert into gp_precioinversion(codInventario,nombreProd," + // ERROR: CS0106 - method body for commented signature
                                // "valorUnitario,cantidad,usuarioSistema) " + // ERROR: CS0106 - method body for commented signature
                                // "values ('" + codInventario + "','" + // ERROR: CS0106 - method body for commented signature
                                // row["nombreProd"].ToString() + "'," + row["valorUnitario"].ToString() + "," + // ERROR: CS0106 - method body for commented signature
                                // row["cantidad"].ToString() + ",'" + UsuarioSistema + "')"; // ERROR: CS0106 - method body for commented signature
                    // _accionsql = "insert"; // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature
                // else // ERROR: CS0106 - method body for commented signature
                // { // ERROR: CS0106 - method body for commented signature
                    // _accionsql = "update"; // ERROR: CS0106 - method body for commented signature
                    // _stmysql = "update gp_precioinversion set nombreProd='" + row["nombreProd"].ToString() + // ERROR: CS0106 - method body for commented signature
                               // "',cantidad=" + row["cantidad"].ToString() + ",valorUnitario=" + row["valorUnitario"].ToString() + ",usuarioSistema='" + UsuarioSistema + "'    where codInventario='" + codInventario + "' and codPrecioInversion =" + row["codPrecioInversion"].ToString(); // ERROR: CS0106 - method body for commented signature
                // } // ERROR: CS0106 - method body for commented signature

//  // ERROR: CS0106 - method body for commented signature
                // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "GrabarPrecioInversion", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // } // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        /// <summary>
        /// Elimina un precio de inversion
        /// </summary>
        // public bool EliminarPrecioInversion(int indice, DataSet dsdata, string codInventario) // ERROR: CS0106
        // { // ERROR: CS0106 - method body for commented signature
            // DataRow row = dsdata.Tables["tblPrecioInversion"].Rows[indice]; // ERROR: CS0106 - method body for commented signature
            // _stmysql = "delete from gp_precioinversion where codInventario='" + codInventario + "' and codPrecioInversion= " + row["codPrecioInversion"].ToString(); // ERROR: CS0106 - method body for commented signature
            // _accionsql = "delete"; // ERROR: CS0106 - method body for commented signature
            // _ok = _odbcConnect.ExecuteQueryconec(_stmysql, _myconnect, "EliminarPrecioInversion", ref _accionsql); // ERROR: CS0106 - method body for commented signature
            // return _ok; // ERROR: CS0106 - method body for commented signature
        // } // ERROR: CS0106 - method body for commented signature

        #endregion
    }
}
