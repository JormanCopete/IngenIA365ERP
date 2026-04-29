using ERP.Core.CarteraFinanciera.Forms;
using ERP.Core.Contabilidad.Models;
using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Microsoft.VisualBasic;
using ERP.Core.Compartido.Configuracion;
using ERP.Core.Compartido.Utilidades;

namespace ERP.Core.CarteraFinanciera.Models
{
    /// <summary>
    /// Clase para parametros de cooperativa
    /// </summary>
    public class ParamCop
    {
        #region Campos privados

#if EXCEL_LEGACY
        private Excel.Application _mExcel;
#endif
        private OdbcCommand _mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect _odbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private Ayuda _msgsas = new Ayuda("admin");
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect _varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ParamSys _paramsys = new ParamSys();
        private ParamCnt _paramcnt = new ParamCnt();
        private string _stmysql;
        public double porcen = 0;
        private bool _ok;

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

        public enum TipoEstado
        {
            Activo = 0,
            Retirado = 1,
            Suspendido = 2,
            RetiradoSuspendido = 3
        }

        public enum LineasCredito
        {
            Conceptos = 1,
            Cartera = 2,
            Todos = 3
        }

        public enum TiposReferencia
        {
            Familiares = 1,
            Personales = 2,
            Comerciales = 3,
            Financieras = 4,
            Todas = 5
        }

        public enum actividad
        {
            cultural = 1,
            deportiva = 2,
            curso = 3,
            recreativa = 4
        }

        #endregion

        #region Constructor y Destructor

        public ParamCop()
        {
            try
            {
                _odbcConnect.MyOdbcConect(ref _varini);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        ~ParamCop()
        {
        }

        #endregion

        #region Metodos de Nomina

        public bool BuscarCptoNomina(string empresa, string agencia, string cencosto, string lincred,
            OdbcConnection myconnect, ref string cpto_nomina, ref string cpto_interes, ref string cpto_extras)
        {
            empresa = Strings.Right("0000" + empresa, 4);
            agencia = Strings.Right("0000" + agencia, 4);
            cencosto = Strings.Right("00000000" + cencosto, 8);

            string whereLincred = lincred == "Todas" ? "" : " and lincred=" + lincred;

            _stmysql = "select copto_nomina as campo1,cpto_interes as campo2,cpto_extras as campo3 from cop_nomconce where empresa='" + empresa +
                "' and agencia='" + agencia + "' and cencosto='" + cencosto + "'" + whereLincred;

            string dummy = "";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "BuscarCptoNomina", ref cpto_nomina, ref cpto_interes, ref cpto_extras, ref dummy);
            return _ok;
        }

        public bool GrabarCptoNomina(string empresa, string agencia, string cencosto, string lincred,
            string cpto_nomina, string cpto_interes, string cpto_extras, OdbcConnection myconnect)
        {
            string whereLincred = lincred == "Todas" ? "" : " and lincred=" + lincred;
            string dummy1 = "999999", dummy2 = "999999", dummy3 = "999999";

            _ok = BuscarCptoNomina(empresa, agencia, cencosto, lincred, myconnect, ref dummy1, ref dummy2, ref dummy3);

            if (lincred == "Todas")
            {
                _ok = EliminarCptoNomina(empresa, agencia, cencosto, lincred, myconnect);
                _stmysql = "insert into cop_nomconce (empresa,agencia,cencosto,lincred,copto_nomina,cpto_interes,cpto_extras) " +
                    " select '" + empresa + "','" + agencia + "','" + cencosto + "',b.lincred,'" + cpto_nomina + "','" + cpto_interes +
                    "','" + cpto_extras + "' from cop_concar12 b";
            }
            else
            {
                if (!_ok)
                {
                    _stmysql = "insert into cop_nomconce (empresa,agencia,cencosto,lincred,copto_nomina,cpto_interes,cpto_extras) values " +
                        "('" + empresa + "','" + agencia + "','" + cencosto + "'," + lincred + ",'" + cpto_nomina + "','" +
                        cpto_interes + "','" + cpto_extras + "')";
                }
                else
                {
                    _stmysql = "update cop_nomconce set empresa = '" + empresa + "',agencia ='" + agencia + "',cencosto ='" +
                        cencosto + "',lincred =" + lincred + ", copto_nomina ='" + cpto_nomina + "', cpto_interes ='" + cpto_interes +
                        "',cpto_extras='" + cpto_extras + "' where empresa='" + empresa + "' and agencia = '" + agencia + "' and cencosto = '" + cencosto +
                        "' and lincred = " + lincred + "";
                }
            }

            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarCptoNomina");
            return _ok;
        }

        public bool EliminarCptoNomina(string empresa, string agencia, string cencosto, string lincred, OdbcConnection myconnect)
        {
            string whereLincred = lincred == "Todas" ? "" : " and lincred=" + lincred;
            _stmysql = "delete from cop_nomconce where empresa = '" + empresa + "' and agencia = '" + agencia +
                "' and cencosto = '" + cencosto + "' " + whereLincred;
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminarCptoNomina");
            return _ok;
        }

        #endregion

        #region Metodos de Actividades

        public bool ActividadesAsociado(string codigoter, OdbcConnection Connection, string opcion = "1", ListView lstactividad = null)
        {
            string nombre_activi = " ";
            string stMysql = "select codigo_actividad from cop_actiaso where codigoter = '" +
                Strings.Right("00000000000000" + codigoter, 14) + "' and tipo_actividad = '" + opcion.Trim() + "'";

            DataSet myReader = new DataSet();
            _odbcConnect.ExecuteQueryDataset(stMysql, Connection, "Actividades Asociado",ref myReader, "TblActividad");
            int canreg = myReader.Tables["TblActividad"].Rows.Count;

            for (int fila = 0; fila < canreg; fila++)
            {
                DataRow row = myReader.Tables["TblActividad"].Rows[fila];
                if (BuscaActividad(row["codigo_actividad"].ToString(), Connection, int.Parse(opcion), ref nombre_activi))
                {
                    if (lstactividad != null)
                    {
                        int coun = lstactividad.Items.Count;
                        ListViewItem item = lstactividad.Items.Add((coun + 1).ToString());
                        item.SubItems.Add(row["codigo_actividad"].ToString());
                        item.SubItems.Add(nombre_activi);
                    }
                }
            }
            myReader.Dispose();
            return true;
        }

        public virtual DataSet ActividadesAsociado(string codigoter, OdbcConnection Connection, actividad opcion, bool tercero = false)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dataset = new DataSet();

            if (!tercero)
            {
                codigoter = Strings.Right("00000000000000" + codigoter, 14);
            }

            switch (opcion)
            {
                case actividad.cultural:
                    stbuilder.Append("select a.Codigo_actividad,b.nombre,a.fecingreso, idbenef as CodParticipante, observacion from cop_actiaso a ");
                    stbuilder.Append("inner join sys_cultura54 b on a.codigo_actividad=b.codigo ");
                    stbuilder.Append("where a.codigoter='" + codigoter + "' and a.tipo_actividad='" + (int)opcion + "'");
                    _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "ActividadesAsociado", ref dataset, "TblCultural");
                    break;

                case actividad.deportiva:
                    stbuilder.Append("select a.Codigo_actividad,b.nombre,a.fecingreso, idbenef as CodParticipante,observacion from cop_actiaso a ");
                    stbuilder.Append("inner join sys_deport53 b on a.codigo_actividad=b.codigo ");
                    stbuilder.Append("where a.codigoter='" + codigoter + "' and a.tipo_actividad='" + (int)opcion + "'");
                    _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "ActividadesAsociado", ref dataset, "TblDeportiva");
                    break;

                case actividad.curso:
                    if (_varini.pstTipoBD.ToUpper() == "DB2")
                    {
                        stbuilder.Append("select a.Codigo_actividad,b.nombre,a.fecingreso, ");
                        stbuilder.Append("case idbenef when '999999999999' then a.codigoter else idbenef end as CodParticipante, ");
                        stbuilder.Append("case idbenef when '999999999999' then (case terc.nit when 'null' then (c.nombre || ' ' || c.apellido) else terc.nombre end ) else d.nombre end as NombreParticipante,");
                        stbuilder.Append("b.porcentaje,a.observacion from cop_actiaso a inner join sys_curso b on a.codigo_actividad=b.codigo ");
                        stbuilder.Append("left join sys_maenit c on a.codigoter=c.codigoter ");
                        stbuilder.Append("left join cnt_nit terc on a.codigoter = terc.nit ");
                        stbuilder.Append("left join cop_benef d on a.codigoter=d.codigoter and a.idbenef=d.cedula ");
                        stbuilder.Append("where a.codigoter='" + codigoter + "' and a.tipo_actividad='" + (int)opcion + "'");
                    }
                    else
                    {
                        stbuilder.Append("select a.Codigo_actividad,b.nombre,a.fecingreso, ");
                        stbuilder.Append("case idbenef when '999999999999' then a.codigoter else idbenef end as CodParticipante, ");
                        stbuilder.Append("case idbenef when '999999999999' then (case terc.nit when null then {fn concat(c.nombre,{fn concat(' ',c.apellido)})} else terc.nombre end ) else d.nombre end as NombreParticipante,");
                        stbuilder.Append("b.porcentaje,a.observacion from cop_actiaso a inner join sys_curso b on a.codigo_actividad=b.codigo ");
                        stbuilder.Append("left join sys_maenit c on a.codigoter=c.codigoter ");
                        stbuilder.Append("left join cnt_nit terc on a.codigoter = terc.nit ");
                        stbuilder.Append("left join cop_benef d on a.codigoter=d.codigoter and a.idbenef=d.cedula ");
                        stbuilder.Append("where a.codigoter='" + codigoter + "' and a.tipo_actividad='" + (int)opcion + "'");
                    }
                    _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "ActividadesAsociado", ref dataset, "TblCursos");
                    break;

                case actividad.recreativa:
                    if (_varini.pstTipoBD.ToUpper() == "DB2")
                    {
                        stbuilder.Append("select a.Codigo_actividad,b.detalle,a.fecingreso, ");
                        stbuilder.Append("case idbenef when '999999999999' then a.codigoter else idbenef end as CodParticipante, ");
                        stbuilder.Append("case idbenef when '999999999999' then (c.nombre || ' ' || c.apellido) else d.nombre end as NombreParticipante,");
                        stbuilder.Append("case TipoActividad when '1' then 'AutoFinanciada' when '2' then 'Financiada' end as TipoActividad,");
                        stbuilder.Append("b.fecha_inicio,b.fecha_termino,b.porcentaje ");
                        stbuilder.Append(" from cop_actiaso a inner join sys_recreacion b on a.codigo_actividad=b.codigo ");
                        stbuilder.Append("inner join sys_maenit c on a.codigoter=c.codigoter ");
                        stbuilder.Append("left join cop_benef d on a.codigoter=d.codigoter and a.idbenef=d.cedula ");
                        stbuilder.Append("where a.codigoter='" + codigoter + "' and a.tipo_actividad='" + (int)opcion + "'");
                    }
                    else
                    {
                        stbuilder.Append("select a.Codigo_actividad,b.detalle,a.fecingreso, ");
                        stbuilder.Append("case idbenef when '999999999999' then a.codigoter else idbenef end as CodParticipante, ");
                        stbuilder.Append("case idbenef when '999999999999' then {fn concat(c.nombre,{fn concat(' ',c.apellido)})} else d.nombre end as NombreParticipante,");
                        stbuilder.Append("case TipoActividad when '1' then 'AutoFinanciada' when '2' then 'Financiada' end as TipoActividad,");
                        stbuilder.Append("b.fecha_inicio,b.fecha_termino,b.porcentaje ");
                        stbuilder.Append(" from cop_actiaso a inner join sys_recreacion b on a.codigo_actividad=b.codigo ");
                        stbuilder.Append("inner join sys_maenit c on a.codigoter=c.codigoter ");
                        stbuilder.Append("left join cop_benef d on a.codigoter=d.codigoter and a.idbenef=d.cedula ");
                        stbuilder.Append("where a.codigoter='" + codigoter + "' and a.tipo_actividad='" + (int)opcion + "'");
                    }
                    _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "ActividadesAsociado", ref dataset, "TblRecreacion");
                    break;
            }

            return dataset;
        }

        public virtual DataSet ActividadesRecreativasAsociado(string codigoter, OdbcConnection Connection, bool tercero = false)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dataset = new DataSet();

            if (!tercero)
            {
                codigoter = Strings.Right("00000000000000" + codigoter, 14);
            }

            if (_varini.pstTipoBD.ToUpper() == "DB2")
            {
                stbuilder.Append("select a.codigo,b.detalle,tipoActi_Recre as TipoActividad,a.fecha_registro,a.idbenef as CodParticipante, ");
                stbuilder.Append("case idbenef when '999999999999' then (case terc.nit when 'null' then (c.nombre || ' ' || c.apellido) else terc.nombre end ) else d.nombre end as NombreParticipante, ");
                stbuilder.Append("case TipoActividad when '1' then 'AutoFinanciada' when '2' then 'Financiada' end as ClaseActividad, ");
                stbuilder.Append("a.FechaInicioActividad as fecha_inicio  ,  a.FechaFinActividad  as fecha_termino,b.porcentaje,observacion ");
                stbuilder.Append(" from cop_actirecrea a inner join sys_recreacion b on a.codigo=b.codigo ");
                stbuilder.Append("left join sys_maenit c on a.codigoter=c.codigoter ");
                stbuilder.Append("left join cnt_nit terc on a.codigoter = terc.nit ");
                stbuilder.Append("left join cop_benef d on a.codigoter=d.codigoter and a.idbenef = d.codigobenef ");
                stbuilder.Append("where a.codigoter='" + codigoter + "'");
            }
            else
            {
                stbuilder.Append("select a.codigo,b.detalle,tipoActi_Recre as TipoActividad,a.fecha_registro,a.idbenef as CodParticipante, ");
                stbuilder.Append("case idbenef when '999999999999' then (case terc.nit when null then {fn concat(c.nombre,{fn concat(' ',c.apellido)})} else terc.nombre end ) else d.nombre end as NombreParticipante, ");
                stbuilder.Append("case TipoActividad when '1' then 'AutoFinanciada' when '2' then 'Financiada' end as ClaseActividad, ");
                stbuilder.Append("a.FechaInicioActividad as fecha_inicio  , a.FechaFinActividad  as fecha_termino ,b.porcentaje,observacion ");
                stbuilder.Append(" from cop_actirecrea a inner join sys_recreacion b on a.codigo=b.codigo ");
                stbuilder.Append("left join sys_maenit c on a.codigoter=c.codigoter ");
                stbuilder.Append("left join cnt_nit terc on a.codigoter = terc.nit ");
                stbuilder.Append("left join cop_benef d on a.codigoter=d.codigoter and a.idbenef = d.codigobenef ");
                stbuilder.Append("where a.codigoter='" + codigoter + "'");
            }

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "ActividadesRecreativasAsociado", ref dataset, "TblRecreacion");
            return dataset;
        }

        public bool BuscaActividad(string codigo, OdbcConnection Connection, actividad tipoActividad)
        {
            string nombre = string.Empty;
            return BuscaActividad(codigo, Connection, (int)tipoActividad, ref nombre);
        }

        public bool BuscaActividad(string codigo, OdbcConnection Connection, actividad tipoActividad, ref string nombre)
        {
            return BuscaActividad(codigo, Connection, (int)tipoActividad, ref nombre);
        }

        public bool BuscaActividad(string codigo, OdbcConnection Connection, int tipoActividad, ref string nombre)
        {
            string tabla = "";
            switch (tipoActividad)
            {
                case 1: tabla = "sys_cultur53"; break;
                case 2: tabla = "sys_deport53"; break;
                case 3: tabla = "sys_cursos"; break;
                case 4: tabla = "sys_recreacion"; break;
            }

            string dummy1 = "", dummy2 = "", dummy3 = "";
            _stmysql = "select nombre as campo1 from " + tabla + " where codigo = '" + codigo + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, Connection, "BuscaActividad", ref nombre, ref dummy1, ref dummy2, ref dummy3);
            return _ok;
        }

        public bool BuscaBeneficiarioAsociado(string Codigoter, string Cedula, OdbcConnection Conect)
        {
            string nombre = "";
            return BuscaBeneficiarioAsociado(Codigoter, ref Cedula, Conect, ref nombre);
        }

        public bool BuscaBeneficiarioAsociado(string Codigoter, string Cedula, OdbcConnection Conect, ref string nombre)
        {
            return BuscaBeneficiarioAsociado(Codigoter, ref Cedula, Conect, ref nombre);
        }

        public bool BuscaBeneficiarioAsociado(string Codigoter, ref string Cedula, OdbcConnection Conect,
            ref string Nombre, ref string tipodocumento, ref string Parentesco,
            ref DateTime FechaNace, ref int NivelAcademico, ref string Discapacidad, ref string Trabaja,
            ref double Porcentaje, ref int Sexo, ref string Telefono, ref int Ciudad, ref string Direccion)
        {
            string Estado = "A", IdBenefNuevo = "9999";
            string where = " where codigoter ='" +
                Strings.Right("00000000000000" + Codigoter.Trim(), 14) + "' and Cedula = '" +
                Cedula.Trim() + "' ";

            string dummy1 = "", dummy2 = "";
            _stmysql = "select estado as campo1,IdBenefNuevo as campo2 from cop_benef ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, Conect, "BuscaBeneficiarioAsociado", ref Estado, ref IdBenefNuevo, ref dummy1, ref dummy2);

            if (_ok)
            {
                if (Estado == "T")
                {
                    MessageBox.Show("Beneficiario fue trasladado al documento No." + IdBenefNuevo, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Cedula = IdBenefNuevo;
                    where = "where codigoter ='" + Strings.Right("00000000000000" + Codigoter.Trim(), 14) + "' and Cedula = '" + Cedula.Trim() + "' ";
                }
            }

            string sFechaNace = "", sNivelAcademico = "0";
            _stmysql = "select nombre as campo1,fechanacimiento as campo2,codParentesco as campo3,NivelAcademico as campo4 from cop_benef ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, Conect, "BuscaBeneficiarioAsociado", ref Nombre, ref sFechaNace, ref Parentesco, ref sNivelAcademico);
            DateTime.TryParse(sFechaNace, out FechaNace);
            int.TryParse(sNivelAcademico, out NivelAcademico);

            string sPorcentaje = "0", tipodoc = "";
            _stmysql = "select porcentaje as campo1,TipoDocumento as campo2,Discapacidad as campo3,Trabaja as campo4 from cop_benef ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, Conect, "BuscaBeneficiarioAsociado", ref sPorcentaje, ref tipodoc, ref Discapacidad, ref Trabaja);
            double.TryParse(sPorcentaje, out Porcentaje);

            string sSexo = "0", sCiudad = "999999";
            _stmysql = "select sexo as campo1,telefono as campo2, direccion as campo3, ciudad as campo4 from cop_benef ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, Conect, "BuscaBeneficiarioAsociado", ref sSexo, ref Telefono, ref Direccion, ref sCiudad);
            int.TryParse(sSexo, out Sexo);
            int.TryParse(sCiudad, out Ciudad);

            switch (tipodoc)
            {
                case "CC": tipodocumento = "Cedula"; break;
                case "NT": tipodocumento = "Nit"; break;
                case "TI": tipodocumento = "Tarjeta Identidad"; break;
                case "RC": tipodocumento = "Registro Civil"; break;
            }

            return _ok;
        }

        public bool BuscaBeneficiarioAsociado(string Codigoter, ref string Cedula, OdbcConnection Conect, ref string Nombre)
        {
            string tipodocumento = "";
            string Parentesco = "";
            DateTime FechaNace = new DateTime(1950, 1, 1);
            int NivelAcademico = 0;
            string Discapacidad = " ";
            string Trabaja = " ";
            double Porcentaje = 0;
            int Sexo = 0;
            string Telefono = "";
            int Ciudad = 999999;
            string Direccion = "";

            return BuscaBeneficiarioAsociado(Codigoter, ref Cedula, Conect,
                ref Nombre, ref tipodocumento, ref Parentesco,
                ref FechaNace, ref NivelAcademico, ref Discapacidad, ref Trabaja,
                ref Porcentaje, ref Sexo, ref Telefono, ref Ciudad, ref Direccion);
        }

        public bool BuscarCursos(ref string codigo, OdbcConnection myconnect)
        {
            string nombre = "", nomres = "";
            return BuscarCursos(ref codigo, myconnect, ref nombre, ref nomres);
        }

        public bool BuscarCursos(string codigo, OdbcConnection myconnect)
        {
            string nombre = "", nomres = "";
            return BuscarCursos(ref codigo, myconnect, ref nombre, ref nomres);
        }

        public bool BuscarCursos(string codigo, OdbcConnection myconnect, ref string nombre, ref double porcentaje)
        {
            string nomres = "", CodEntidad = "";
            int intensidad = 0, tipoeducacion = 0;
            double dPorcentaje = 0, ValorCurso = 0;
            string CodComite = "9999", CodProgAct = "9999";

            bool result = BuscarCursos(ref codigo, myconnect, ref nombre, ref nomres, ref CodEntidad,
                ref intensidad, Navega.Ninguno, ref tipoeducacion, ref dPorcentaje, ref ValorCurso,
                ref CodComite, ref CodProgAct);

            porcentaje = dPorcentaje;
            return result;
        }

        public bool BuscarCursos(ref string codigo, OdbcConnection myconnect, ref string nombre, ref string nomres)
        {
            string CodEntidad = "";
            int intensidad = 0, tipoeducacion = 0;
            double Porcentaje = 0, ValorCurso = 0;
            string CodComite = "9999", CodProgAct = "9999";

            return BuscarCursos(ref codigo, myconnect, ref nombre, ref nomres, ref CodEntidad,
                ref intensidad, Navega.Ninguno, ref tipoeducacion, ref Porcentaje, ref ValorCurso,
                ref CodComite, ref CodProgAct);
        }

        public bool BuscarCursos(ref string codigo, OdbcConnection myconnect,
            ref string nombre, ref string nomres, ref string CodEntidad, ref int intensidad,
            Navega navegar, ref int tipoeducacion, ref double Porcentaje,
            ref double ValorCurso, ref string CodComite, ref string CodProgAct)
        {
            string where = "";
            codigo = Strings.Right("0000" + codigo, 4);

            switch (navegar)
            {
                case Navega.Primero:
                    where = "where codigo >'0' order by codigo";
                    break;
                case Navega.Siguiente:
                    where = "where codigo >'" + codigo + "' order by codigo";
                    break;
                case Navega.Anterior:
                    where = "where codigo <'" + codigo + "' order by codigo desc";
                    break;
                case Navega.Ultimo:
                    where = "where codigo <= '9999' order by codigo desc";
                    break;
                case Navega.Ninguno:
                    where = "where codigo='" + codigo + "'";
                    break;
            }

            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdataset = new DataSet();

            stbuilder.Append("select codigo, nombre, nomres, entidad_dicta, intensidad, tipoeducacion, porcentaje, ");
            stbuilder.Append("valor, comite,programaAct from sys_curso " + where);

            _ok = _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarCursos", ref dsdataset, "TblCursos");

            if (_ok && dsdataset.Tables["TblCursos"].Rows.Count > 0)
            {
                DataRow row = dsdataset.Tables["TblCursos"].Rows[0];
                codigo = row["codigo"].ToString();
                nombre = row["nombre"].ToString();
                nomres = row["nomres"].ToString();
                CodEntidad = row["entidad_dicta"].ToString();
                int.TryParse(row["intensidad"].ToString(), out intensidad);
                int.TryParse(row["tipoeducacion"].ToString(), out tipoeducacion);
                double.TryParse(row["Porcentaje"].ToString(), out Porcentaje);
                double.TryParse(row["Valor"].ToString(), out ValorCurso);
                CodComite = row["Comite"].ToString();
                CodProgAct = row["programaAct"].ToString();
            }

            return _ok;
        }

        public bool BuscarRecreacion(string codigo, OdbcConnection myconnect)
        {
            string Detalle = "", TipoActividad = "", NomTipoActividad = "", Comite = "", TipoRecreacion = "", cantCupos = "", ctrlnovedad = "N", programaact = "";
            DateTime FechaInicio = new DateTime(1950, 1, 1), FechaTermino = new DateTime(1950, 1, 1);
            double Porcentaje = 0, valor = 0;

            return BuscarRecreacion(ref codigo, myconnect, ref Detalle, ref TipoActividad, ref FechaInicio, ref FechaTermino,
                ref Porcentaje, Navega.Ninguno, ref NomTipoActividad, ref Comite, ref valor, ref TipoRecreacion,
                ref cantCupos, ref ctrlnovedad, ref programaact);
        }

        public bool BuscarRecreacion(string codigo, OdbcConnection myconnect, ref string nombre,
            ref DateTime FechaInicio, ref DateTime FechaTermino, ref double porcentaje, ref string NomTipoActividad)
        {
            string TipoActividad = "", Comite = "", TipoRecreacion = "", cantCupos = "", ctrlnovedad = "N", programaact = "";
            double dPorcentaje = 0, valor = 0;

            bool result = BuscarRecreacion(ref codigo, myconnect, ref nombre, ref TipoActividad, ref FechaInicio, ref FechaTermino,
                ref dPorcentaje, Navega.Ninguno, ref NomTipoActividad, ref Comite, ref valor, ref TipoRecreacion,
                ref cantCupos, ref ctrlnovedad, ref programaact);

            porcentaje = dPorcentaje;
            return result;
        }

        public bool BuscarRecreacion(ref string codigo, OdbcConnection myconnect,
            ref string Detalle, ref string TipoActividad, ref DateTime FechaInicio, ref DateTime FechaTermino,
            ref double Porcentaje, Navega navegar, ref string NomTipoActividad,
            ref string Comite, ref double valor, ref string TipoRecreacion, ref string cantCupos,
            ref string ctrlnovedad, ref string programaact)
        {
            string where = "";

            switch (navegar)
            {
                case Navega.Primero:
                    where = "where codigo >' ' order by codigo";
                    break;
                case Navega.Siguiente:
                    where = "where codigo >'" + codigo + "' order by codigo";
                    break;
                case Navega.Anterior:
                    where = "where codigo <'" + codigo + "' order by codigo desc";
                    break;
                case Navega.Ultimo:
                    where = "where codigo <= 'ZZZZZZZZZZZZZZZ' order by codigo desc";
                    break;
                case Navega.Ninguno:
                    where = "where codigo='" + codigo + "'";
                    break;
            }

            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdataset = new DataSet();

            stbuilder.Append("select codigo,Detalle,TipoActividad,Fecha_Inicio,Fecha_Termino,Porcentaje,comite,valor,tipoActi_Recre,cantcupos,ctrlnovedad, ");
            stbuilder.Append("case TipoActividad when '1' then 'AutoFinanciada' when '2' then 'Financiada' end as NomTipoActividad,programaact   ");
            stbuilder.Append(" from sys_recreacion " + where);

            _ok = _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarRecreacion", ref dsdataset, "TblRecreacion");

            if (_ok && dsdataset.Tables["TblRecreacion"].Rows.Count > 0)
            {
                DataRow row = dsdataset.Tables["TblRecreacion"].Rows[0];
                codigo = row["codigo"].ToString();
                string tipoRecre = row["tipoActi_Recre"].ToString();
                TipoRecreacion = (tipoRecre == "NO DEFINIDO" || tipoRecre.Trim() == "") ? "" : tipoRecre;
                Detalle = row["Detalle"].ToString();
                TipoActividad = row["TipoActividad"].ToString();
                FechaInicio = Convert.ToDateTime(row["Fecha_Inicio"]);
                FechaTermino = Convert.ToDateTime(row["Fecha_Termino"]);
                double.TryParse(row["Porcentaje"].ToString(), out Porcentaje);
                NomTipoActividad = row["NomTipoActividad"].ToString();
                double.TryParse(row["Valor"].ToString(), out valor);
                Comite = row["Comite"].ToString();
                cantCupos = row["cantcupos"].ToString();
                ctrlnovedad = row["ctrlnovedad"].ToString();
                programaact = row["programaact"].ToString();
            }

            return _ok;
        }

        public bool GrabarAficionesAsociado(string codigoter, string tipo, string codigo,
            DateTime fecha, string beneficiario, string observacion, OdbcConnection myconnect)
        {
            string idBenef = (beneficiario == codigoter) ? "999999999999" : beneficiario;
            string fechaStr = Strings.Format(fecha, _varini.PstForFec);

            switch (tipo)
            {
                case "1":
                    _stmysql = "select codigo_actividad from cop_actiaso where codigoter='" + codigoter +
                        "' and tipo_actividad='1' and codigo_actividad='" + codigo + "'" +
                        " and idbenef='" + idBenef + "'";
                    _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarAficionesAsociado");
                    if (!_ok)
                    {
                        _stmysql = "insert into cop_actiaso (tipo_actividad, codigo_actividad, codigoter, fecingreso,idbenef,observacion) values ('1'," +
                            "'" + codigo + "','" + codigoter + "','" + fechaStr + "','" + idBenef + "','" + observacion + "')";
                        _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarAficionesAsociado");
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                case "2":
                    _stmysql = "select codigo_actividad from cop_actiaso where codigoter='" + codigoter +
                        "' and tipo_actividad='2' and codigo_actividad='" + codigo + "'" +
                        " and idbenef='" + idBenef + "'";
                    _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarAficionesAsociado");
                    if (!_ok)
                    {
                        _stmysql = "insert into cop_actiaso (tipo_actividad, codigo_actividad, codigoter, fecingreso,idbenef,observacion) values ('2'," +
                            "'" + codigo + "','" + codigoter + "','" + fechaStr + "','" + idBenef + "','" + observacion + "')";
                        _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarAficionesAsociado");
                        return true;
                    }
                    else
                    {
                        return false;
                    }
            }

            return false;
        }

        public bool GrabarCursoAsociado(string codigoter, string tipo, string codigo,
            DateTime fecha, string beneficiario, string observacion, OdbcConnection myconnect)
        {
            string idBenef = (beneficiario == codigoter) ? "999999999999" : beneficiario;
            string fechaStr = Strings.Format(fecha, _varini.PstForFec);

            _stmysql = "select codigo_actividad from cop_actiaso where codigoter='" + codigoter +
                "' and tipo_actividad='3' and codigo_actividad='" + codigo + "'" +
                " and idbenef='" + idBenef + "'  and  fecingreso='" + fechaStr + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarCursoAsociado");

            if (!_ok)
            {
                _stmysql = "insert into cop_actiaso (tipo_actividad, codigo_actividad, codigoter, fecingreso,idbenef,observacion) values ('3'," +
                    "'" + codigo + "','" + codigoter + "','" + fechaStr + "','" + idBenef + "','" + observacion + "')";
                _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarCursoAsociado");
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool EliminarAficionAsociado(string codigoter, int indice, string tipo, DataSet dsdata, OdbcConnection myconnect)
        {
            string tableName = (tipo == "1") ? "TblCultural" : "TblDeportiva";
            DataRow row = dsdata.Tables[tableName].Rows[indice];
            string codibenef = row["CodParticipante"].ToString();
            string codiact = row["Codigo_actividad"].ToString();

            _stmysql = "delete from cop_actiaso  where tipo_actividad='" + tipo + "' and codigoter='" + codigoter +
                "' and codigo_actividad='" + codiact + "' and idbenef='" + codibenef + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminarAficionAsociado");
            return _ok;
        }

        public bool EliminarCursoIncrito(string codigoter, int indice, DataSet dsdata, OdbcConnection myconnect)
        {
            DataRow row = dsdata.Tables["TblCursos"].Rows[indice];
            string codibenef = (row["CodParticipante"].ToString() == codigoter) ? "999999999999" : row["CodParticipante"].ToString();
            string codiact = row["Codigo_actividad"].ToString();
            DateTime fecharegistro = Convert.ToDateTime(row["fecingreso"]);

            _stmysql = "delete from cop_actiaso  where tipo_actividad='3' and codigoter='" + codigoter +
                "' and codigo_actividad='" + codiact + "' and idbenef='" + codibenef +
                "' and fecingreso='" + Strings.Format(fecharegistro, _varini.PstForFec) + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminarCursoIncrito");
            return _ok;
        }

        #endregion

        #region Metodos auxiliares

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
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
                        Campo1 = myread["campo1"] == DBNull.Value ? "0" : myread["campo1"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo2"))
                    {
                        Campo2 = myread["campo2"] == DBNull.Value ? "0" : myread["campo2"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo3"))
                    {
                        Campo3 = myread["campo3"] == DBNull.Value ? "0" : myread["campo3"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo4"))
                    {
                        Campo4 = myread["campo4"] == DBNull.Value ? "0" : myread["campo4"].ToString().Trim();
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
                throw new Exception(er + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql);
            }
            return result;
        }

        #endregion

        #region Metodos de Asociados

        public virtual bool BuscarAsociado(string codigoter, OdbcConnection myconnect, ref string nombre)
        {
            return BuscarAsociado(codigoter, myconnect, null, ref nombre);
        }

        public virtual bool BuscarAsociado(string codigoter, OdbcConnection myconnect, object navegar, ref string nombre)
        {
            string codigo = codigoter;
            string apellido = string.Empty;
            string direccion = string.Empty;
            string telefono1 = string.Empty;
            string telefono2 = string.Empty;
            string celular = string.Empty;
            string email = string.Empty;
            DateTime fechaIngreso = default(DateTime);
            DateTime fechaNacimiento = default(DateTime);
            string estado = string.Empty;
            string tipoVinculo = string.Empty;
            string sexo = string.Empty;
            string estadoCivil = string.Empty;
            string profesion = string.Empty;
            string tipoVivienda = string.Empty;
            int estrato = 0;
            decimal ingresos = 0m;
            decimal egresos = 0m;
            string empresaTrabajo = string.Empty;
            string cargoEmpresa = string.Empty;
            string ciudadResidencia = string.Empty;
            string departamento = string.Empty;

            Navega navega = navegar is Navega nav ? nav : Navega.Ninguno;

            return BuscarAsociado(
                ref codigo,
                myconnect,
                navega,
                ref nombre,
                ref apellido,
                ref direccion,
                ref telefono1,
                ref telefono2,
                ref celular,
                ref email,
                ref fechaIngreso,
                ref fechaNacimiento,
                ref estado,
                ref tipoVinculo,
                ref sexo,
                ref estadoCivil,
                ref profesion,
                ref tipoVivienda,
                ref estrato,
                ref ingresos,
                ref egresos,
                ref empresaTrabajo,
                ref cargoEmpresa,
                ref ciudadResidencia,
                ref departamento,
                TipoEstado.Activo);
        }

        public virtual bool BuscarAsociado(ref string codigoter, OdbcConnection myconnect,
            ref string nombre, ref string apellido, ref string direccion, ref string telefono1, ref string telefono2,
            ref string celular, ref string email, ref DateTime fechaIngreso, ref DateTime fechaNacimiento,
            ref string estado, ref string tipoVinculo, ref string sexo, ref string estadoCivil, ref string profesion,
            ref string tipoVivienda, ref int estrato, ref decimal ingresos, ref decimal egresos,
            ref string empresaTrabajo, ref string cargoEmpresa, ref string ciudadResidencia, ref string departamento)
        {
            return BuscarAsociado(ref codigoter, myconnect, Navega.Ninguno,
                ref nombre, ref apellido, ref direccion, ref telefono1, ref telefono2,
                ref celular, ref email, ref fechaIngreso, ref fechaNacimiento,
                ref estado, ref tipoVinculo, ref sexo, ref estadoCivil, ref profesion,
                ref tipoVivienda, ref estrato, ref ingresos, ref egresos,
                ref empresaTrabajo, ref cargoEmpresa, ref ciudadResidencia, ref departamento,
                TipoEstado.Activo);
        }

        public virtual bool BuscarAsociado(ref string codigoter, OdbcConnection myconnect, Navega Navegar,
            ref string nombre, ref string apellido, ref string direccion, ref string telefono1, ref string telefono2,
            ref string celular, ref string email, ref DateTime fechaIngreso, ref DateTime fechaNacimiento,
            ref string estado, ref string tipoVinculo, ref string sexo, ref string estadoCivil, ref string profesion,
            ref string tipoVivienda, ref int estrato, ref decimal ingresos, ref decimal egresos,
            ref string empresaTrabajo, ref string cargoEmpresa, ref string ciudadResidencia, ref string departamento,
            TipoEstado tipoEstado)
        {
            string where = "";
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            string estadoWhere = "";
            switch (tipoEstado)
            {
                case TipoEstado.Activo:
                    estadoWhere = " and estado = '0'";
                    break;
                case TipoEstado.Retirado:
                    estadoWhere = " and estado = '1'";
                    break;
                case TipoEstado.Suspendido:
                    estadoWhere = " and estado = '2'";
                    break;
                case TipoEstado.RetiradoSuspendido:
                    estadoWhere = " and (estado = '1' or estado = '2')";
                    break;
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = "from sys_maenit where codigoter = '" + codigoter + "'" + estadoWhere;
                    break;
                case Navega.Primero:
                    where = "from sys_maenit where codigoter > ' ' " + estadoWhere + " order by codigoter " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = "from sys_maenit where codigoter < '" + codigoter + "' " + estadoWhere + " order by codigoter desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = "from sys_maenit where codigoter > '" + codigoter + "' " + estadoWhere + " order by codigoter " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = "from sys_maenit where codigoter <= '99999999999999' " + estadoWhere + " order by codigoter desc " + _varini.Pstlimit;
                    break;
            }

            string sFechaNac = "", sFechaIng = "";
            _stmysql = "select " + _varini.Psttop + " codigoter as campo1, nombre as campo2, apellido as campo3, direccion as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarAsociado", ref codigoter, ref nombre, ref apellido, ref direccion);

            _stmysql = "select " + _varini.Psttop + " telefono1 as campo1, telefono2 as campo2, celular as campo3, email as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarAsociado", ref telefono1, ref telefono2, ref celular, ref email);

            _stmysql = "select " + _varini.Psttop + " fecha_ingreso as campo1, fecha_nacimiento as campo2, estado as campo3, tipo_vinculo as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarAsociado", ref sFechaIng, ref sFechaNac, ref estado, ref tipoVinculo);

            DateTime.TryParse(sFechaIng, out fechaIngreso);
            DateTime.TryParse(sFechaNac, out fechaNacimiento);

            _stmysql = "select " + _varini.Psttop + " sexo as campo1, estado_civil as campo2, profesion as campo3, tipo_vivienda as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarAsociado", ref sexo, ref estadoCivil, ref profesion, ref tipoVivienda);

            string sEstrato = "0", sIngresos = "0", sEgresos = "0";
            _stmysql = "select " + _varini.Psttop + " estrato as campo1, ingresos as campo2, egresos as campo3, empresa_trabaja as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarAsociado", ref sEstrato, ref sIngresos, ref sEgresos, ref empresaTrabajo);

            int.TryParse(sEstrato, out estrato);
            decimal.TryParse(sIngresos, out ingresos);
            decimal.TryParse(sEgresos, out egresos);

            _stmysql = "select " + _varini.Psttop + " cargo_empresa as campo1, ciudad_residencia as campo2, departamento as campo3 ";
            string dummy = "";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarAsociado", ref cargoEmpresa, ref ciudadResidencia, ref departamento, ref dummy);

            return _ok;
        }

        public virtual bool BuscaAsociado(ref string codigoter, ref DataSet DsDataset, OdbcConnection myconect, Navega Navegar = Navega.Ninguno)
        {
            StringBuilder stbuilder = new StringBuilder();
            string where = "";
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from sys_maenit where codigoter = '" + codigoter + "'";
                    break;
                case Navega.Primero:
                    where = " from sys_maenit where codigoter > ' ' order by codigoter " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from sys_maenit where codigoter < '" + codigoter + "' order by codigoter desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from sys_maenit where codigoter > '" + codigoter + "' order by codigoter " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from sys_maenit where codigoter <= '99999999999999' order by codigoter desc " + _varini.Pstlimit;
                    break;
            }

            stbuilder.Append("select codigoter, apellido, nombre, nit,empresa,telefono1,telefono2,periodo_desto,direccion,fechanac as fecnacem,");
            stbuilder.Append("estado_civil,fecnacem,dpto_ciudad,empresa_labora,contracto,fecha_ingreso,salario,cargo,agencia,cencosto,estado,SeguroRiesgo,idtipozona,idzona,");
            stbuilder.Append("CONYUGE,CONYTELEF,CONYCIUD,CONYEMPR,CONYDIREC,CONYDIREM,CONYSALAR,otro_ingreso,MOVIL,engestion,tipo_nit,Clase,empresa,CLASE_DESTO,CESANTIAS,comite,");
            stbuilder.Append("pagareunico,Acierta,saldodeudaexterna,cuotadeudaexterna,ScoreCifin,califidatacredito,CALMAN,SalMorDatacredito,clasecupoPos,valorClaseCupo,");
            stbuilder.Append("FECHA_REINGRESO, nit_chequeo, fax, email, cuenta_banco, codigo_banco, tipo_cuenta, Sexo, tipo_salario, ");
            stbuilder.Append("tasa_aporte,seccion_empresa,codigo_empresa,direccion_envio,envio_dir,tipo_vivienda,vehiculo,ciudad_envio,sector,dia_corte,profesion,   ");
            stbuilder.Append("capa_deuda,fecha_retiro,saldo_aporte,saldo_deuda,dias_mora,saldo_mora,fecult_cierre,fecult_pago,califi_categ,fecult_credito,fecult_mora,conycedu, ");
            stbuilder.Append("conycargo,NIV_ACADE,DesOtroIng,referido,asesor,expedida,natjur,ESTRATO,Tipo_Pago,escalafon,vencontracto,activos,Fechasys,CabezaFamilia,fecexpedicion,");
            stbuilder.Append("tipo_correo,TipoVehiculo,CodCIU,JornadaLaboral,autoricentralriesgo,Empleado,CONYFECNACEM ,CONYTIPONIT,CONYEXPEDIDA,CONYFECEXPEDICION,CONYSEXO,CONYFAX ,");
            stbuilder.Append("CONYENVIODIR,CONYENDIRCOR,CONYEMPRESA,CONYAGENCIA,CONYSECCION,CONYFECINGREEMP,CONYNIVACADE,CONYTIPOSALARIO,CONYCESANTIAS,CONYOTROING, ");
            stbuilder.Append("CONYDESOTROING,CONYPROFE,admrecuspub,CpAdmon,CpAptos,CpLocal,CpComision,cenutilidad,pignora_aport,exoneradoSipa,fechaexonerado,usuariosipla,");
            stbuilder.Append("IngVariables,IngArriendos,IngPension,DeudasTerceros,GASTO_FIJO_MES,DstoPension,cappagoPorcentaje,dstoGastosPerso   " + where);

            _ok = _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconect, "BuscaAsociado", ref DsDataset, "tblasociados", true);
            return _ok;
        }

        public virtual bool BuscaAsociado(ref string codigoter, OdbcConnection myconect,
            Navega Navegar, ref string Nombre, ref string Apellido,
            ref string Nit, ref string ApellidoNombre,
            ref string agencia, ref string empresa,
            ref string direccion, ref string digche,
            ref string TipoDoc, ref string Tel1,
            ref string Tel2, ref string Fax,
            ref string Movil, ref string Email,
            ref string Ciudad, ref string Cencosto,
            ref string Clase, ref string SeguroRiesgo,
            ref string CuentaBanco, ref string CodBanco,
            ref string TipoCuenta, ref string Sexo,
            ref string EstadoCivil, ref string FechaIngreso,
            ref string FecIngEmp, ref string EmpreLabora,
            ref string TipoSalario, ref string SalarioBasico,
            ref string CesantiasAcu, ref string TasaAportes,
            ref string Seccion, ref string CodigoInterno,
            ref string DirEnvio, ref string EnvioCorre,
            ref string TipoVivienda, ref bool TieneVehiculo,
            ref string CiudadEnvio, ref string Zona,
            ref string DiaCorte, ref string Estado,
            ref string profesion, ref string cargo,
            ref string capadeuda, ref string otroingreso,
            ref string FechaRetiro, ref string SaldoAportes,
            ref string SaldoDeuda, ref string DiasMora,
            ref string SaldoMora, ref string FecUltCierre,
            ref string FecUltPago, ref string CalifiCategoria,
            ref string fecultcredito, ref string FecUltmora,
            ref string peridoDescto, ref string clades,
            ref string conyuge, ref string conycedu,
            ref string conydirec, ref string conyempr,
            ref string conytelef, ref string conyciud,
            ref string conycargo, ref string conysalar,
            ref string Referido, ref string Asesor,
            ref string FechaNace, ref string NivelAcademico,
            ref string Expedida, ref string NaturalJuridico,
            ref string Escalafon, ref string TipoContrato,
            ref string VenceContrato, ref string Activos,
            ref string CiudadCuenta, ref string MotivoRetiro,
            ref string EstadoAnterior, ref string FechaReingreso,
            ref string calman, ref string FecExpedicion,
            ref int TipoCorreo, ref int TipoVehiculo,
            ref string CodCIU, ref string DesOtroIng,
            ref string Estrato, ref string FondoCesantia,
            ref string RecibeFactura, ref string TipoPago,
            ref string IdtipoZona, ref string IdZona,
            ref string IdComite, ref string FechaModificacion,
            ref string CabezaFamilia, ref string JornadaLaboral,
            ref string Autorizacion, ref string CONYFECNACEM,
            ref string CONYTIPONIT, ref string CONYEXPEDIDA,
            ref string CONYFECEXPEDICION, ref string CONYSEXO,
            ref string CONYFAX, ref string CONYENVIODIR,
            ref string CONYENDIRCOR, ref string CONYEMPRESA,
            ref string CONYAGENCIA, ref string CONYSECCION,
            ref string CONYFECINGREEMP, ref string CONYNIVACADE,
            ref string CONYTIPOSALARIO, ref string CONYCESANTIAS,
            ref string CONYOTROING, ref string CONYDESOTROING,
            ref string CONYPROFE, ref string Empleado,
            ref string admrecupub, ref string pagareunico,
            ref string Acierta, ref string saldodeudaexterna,
            ref string cuotadeudaexterna, ref string ScoreCifin,
            ref string califidatacredito, ref string SalMorDatacredito,
            ref string clasecupoPos, ref double valorClaseCupo,
            ref string CpAdmon, ref string CpAptos,
            ref string CpLocal, ref string CpComision,
            ref string cenutilidad, ref string pignora_aport,
            ref string exoneradoSipa, ref DateTime fechaexonerado,
            ref string usuariosipla,
            ref string IngVariables, ref string IngArriendos, ref string IngPension,
            ref string DeudasTerceros, ref string GASTO_FIJO_MES, ref string DstoPension,
            ref string cappagoPorcentaje, ref string dstoGastosPerso)
        {
            if (fechaexonerado == default(DateTime))
                fechaexonerado = new DateTime(1950, 1, 1);

            bool ok = false;
            string where = "";
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            if (Navegar == Navega.Ninguno)
            {
                if (codigoter == "00000000000000")
                {
                    codigoter = "";
                    return false;
                }
            }

            if (Navegar == Navega.Ninguno)
                where = " from sys_maenit where codigoter = '" + codigoter + "'";
            else if (Navegar == Navega.Primero)
                where = " from sys_maenit where codigoter > ' ' order by codigoter " + _varini.Pstlimit;
            else if (Navegar == Navega.Anterior)
                where = " from sys_maenit where codigoter < '" + codigoter + "' order by codigoter desc " + _varini.Pstlimit;
            else if (Navegar == Navega.Siguiente)
                where = " from sys_maenit where codigoter > '" + codigoter + "' order by codigoter " + _varini.Pstlimit;
            else if (Navegar == Navega.Ultimo)
                where = " from sys_maenit where codigoter <= '99999999999999' order by codigoter desc " + _varini.Pstlimit;

            DataSet datasetBuscaAsociado = new DataSet();
            BuscaAsociado(ref codigoter, ref datasetBuscaAsociado, myconect, Navegar);

            if (datasetBuscaAsociado.Tables["tblasociados"].Rows.Count > 0)
            {
                DataRow r = datasetBuscaAsociado.Tables["tblasociados"].Rows[0];
                ok = true;

                Apellido = r["apellido"].ToString(); Nombre = r["nombre"].ToString();
                Nit = r["nit"].ToString(); agencia = r["agencia"].ToString();
                ApellidoNombre = Apellido + " " + Nombre;

                digche = r["nit_chequeo"].ToString(); TipoDoc = r["Tipo_nit"].ToString();
                direccion = r["direccion"].ToString(); Tel1 = r["telefono1"].ToString();

                TipoDoc = (Strings.InStr("CNETUR", TipoDoc.Trim(), CompareMethod.Text) - 1).ToString();
                if (int.Parse(TipoDoc) < 0) TipoDoc = "0";

                Tel2 = r["telefono2"].ToString(); Fax = r["fax"].ToString();
                Movil = r["movil"].ToString(); Email = r["email"].ToString();

                Ciudad = r["dpto_ciudad"].ToString(); empresa = r["empresa"].ToString();
                Cencosto = r["cencosto"].ToString();

                Clase = r["clase"].ToString(); SeguroRiesgo = r["SeguroRiesgo"].ToString();
                CuentaBanco = r["cuenta_banco"].ToString(); CodBanco = r["codigo_banco"].ToString();

                Clase = (Strings.InStr("56", Clase.Trim(), CompareMethod.Text) - 1).ToString();
                if (int.Parse(Clase) < 0) Clase = "0";

                TipoCuenta = r["tipo_cuenta"].ToString(); Sexo = r["Sexo"].ToString();
                EstadoCivil = r["estado_civil"].ToString(); FechaIngreso = r["fecha_ingreso"].ToString();

                TipoCuenta = (Strings.InStr("AC", TipoCuenta.Trim(), CompareMethod.Text) - 1).ToString();
                if (int.Parse(TipoCuenta) < 0) TipoCuenta = "0";

                Sexo = (Strings.InStr("FM", Sexo.Trim(), CompareMethod.Text) - 1).ToString();
                if (int.Parse(Sexo) < 0) Sexo = "0";

                if (!string.IsNullOrEmpty(EstadoCivil.Trim()))
                {
                    int ec = 0;
                    if (int.TryParse(EstadoCivil, out ec)) { ec -= 1; if (ec < 0) ec = 0; EstadoCivil = ec.ToString(); }
                    else EstadoCivil = "0";
                }
                else EstadoCivil = "0";

                if (!Information.IsDate(FechaIngreso))
                    FechaIngreso = new DateTime(1950, 1, 1).ToShortDateString();

                EmpreLabora = r["empresa_labora"].ToString(); FecIngEmp = r["feing_empresa"].ToString();
                TipoSalario = r["tipo_salario"].ToString(); SalarioBasico = r["salario"].ToString();

                if (!Information.IsDate(FecIngEmp))
                    FecIngEmp = new DateTime(1950, 1, 1).ToShortDateString();

                if (!string.IsNullOrEmpty(TipoSalario.Trim()))
                {
                    int ts = 0;
                    if (int.TryParse(TipoSalario, out ts)) { ts -= 1; if (ts < 0) ts = 0; TipoSalario = ts.ToString(); }
                    else TipoSalario = "0";
                }
                else TipoSalario = "0";

                CesantiasAcu = r["cesantias"].ToString(); TasaAportes = r["tasa_aporte"].ToString();
                Seccion = r["seccion_empresa"].ToString(); CodigoInterno = r["codigo_empresa"].ToString();
                string vehiculo = " ";

                DirEnvio = r["direccion_envio"].ToString(); EnvioCorre = r["envio_dir"].ToString();
                TipoVivienda = r["tipo_vivienda"].ToString(); vehiculo = r["vehiculo"].ToString();

                if (!string.IsNullOrEmpty(EnvioCorre.Trim()))
                {
                    int ev = 0;
                    if (int.TryParse(EnvioCorre, out ev)) { ev -= 1; if (ev < 0) ev = 0; EnvioCorre = ev.ToString(); }
                    else EnvioCorre = "0";
                }
                else EnvioCorre = "0";

                if (!string.IsNullOrEmpty(TipoVivienda.Trim()))
                {
                    int tv = 0;
                    if (int.TryParse(TipoVivienda, out tv)) { tv -= 1; if (tv < 0) tv = 0; TipoVivienda = tv.ToString(); }
                    else TipoVivienda = "0";
                }
                else TipoVivienda = "0";

                TieneVehiculo = vehiculo.Trim() == "Y";

                CiudadEnvio = r["ciudad_envio"].ToString(); Zona = r["sector"].ToString();
                DiaCorte = r["dia_corte"].ToString(); Estado = r["estado"].ToString();

                Estado = (Strings.InStr("ARS", Estado.Trim(), CompareMethod.Text) - 1).ToString();
                if (int.Parse(Estado) < 0) Estado = "0";

                profesion = r["profesion"].ToString(); cargo = r["cargo"].ToString();
                capadeuda = r["capa_deuda"].ToString(); otroingreso = r["otro_ingreso"].ToString();

                FechaRetiro = r["fecha_retiro"].ToString(); SaldoAportes = r["saldo_aporte"].ToString();
                SaldoDeuda = r["saldo_deuda"].ToString(); DiasMora = r["dias_mora"].ToString();

                if (!Information.IsDate(FechaRetiro))
                    FechaRetiro = new DateTime(1950, 1, 1).ToShortDateString();

                SaldoMora = r["saldo_mora"].ToString(); FecUltCierre = r["fecult_cierre"].ToString();
                FecUltPago = r["fecult_pago"].ToString(); CalifiCategoria = r["califi_categ"].ToString();

                if (!Information.IsDate(FecUltCierre))
                    FecUltCierre = new DateTime(1950, 1, 1).ToShortDateString();
                if (!Information.IsDate(FecUltPago))
                    FecUltPago = new DateTime(1950, 1, 1).ToShortDateString();

                fecultcredito = r["fecult_credito"].ToString(); FecUltmora = r["fecult_mora"].ToString();
                peridoDescto = r["periodo_desto"].ToString(); clades = r["clase_desto"].ToString();

                if (!Information.IsDate(fecultcredito))
                    fecultcredito = new DateTime(1950, 1, 1).ToShortDateString();
                if (!Information.IsDate(FecUltmora))
                    FecUltmora = new DateTime(1950, 1, 1).ToShortDateString();

                if (!string.IsNullOrEmpty(peridoDescto.Trim()))
                {
                    int pd = 0;
                    if (int.TryParse(peridoDescto, out pd)) { pd -= 1; if (pd < 0) pd = 0; peridoDescto = pd.ToString(); }
                    else peridoDescto = "0";
                }
                else peridoDescto = "0";

                if (!string.IsNullOrEmpty(clades.Trim()))
                {
                    int cd = 0;
                    if (int.TryParse(clades, out cd)) { cd -= 1; if (cd < 0) cd = 0; clades = cd.ToString(); }
                    else clades = "0";
                }
                else clades = "0";

                conyuge = r["conyuge"].ToString(); conycedu = r["conycedu"].ToString();
                conydirec = r["conydirec"].ToString(); conyempr = r["conyempr"].ToString();

                conytelef = r["conytelef"].ToString(); conyciud = r["conyciud"].ToString();
                conycargo = r["conycargo"].ToString(); conysalar = r["conysalar"].ToString();

                FechaNace = r["fecnacem"].ToString(); NivelAcademico = r["NIV_ACADE"].ToString();
                codigoter = r["codigoter"].ToString(); DesOtroIng = r["DesOtroIng"].ToString();

                if (!Information.IsDate(FechaNace))
                    FechaNace = new DateTime(1950, 1, 1).ToShortDateString();

                Referido = r["referido"].ToString(); Asesor = r["asesor"].ToString();
                Expedida = r["expedida"].ToString(); NaturalJuridico = r["natjur"].ToString();

                if (!string.IsNullOrEmpty(NaturalJuridico.Trim()))
                {
                    int nj = 0;
                    if (int.TryParse(NaturalJuridico, out nj)) { nj -= 1; if (nj < 0) nj = 0; NaturalJuridico = nj.ToString(); }
                    else NaturalJuridico = "0";
                }
                else NaturalJuridico = "0";

                FechaReingreso = r["fecha_reingreso"].ToString(); Estrato = r["ESTRATO"].ToString();
                TipoPago = r["Tipo_Pago"].ToString(); IdComite = r["Comite"].ToString();

                Escalafon = r["escalafon"].ToString(); TipoContrato = r["contracto"].ToString();
                VenceContrato = r["vencontracto"].ToString(); Activos = r["activos"].ToString();

                IdtipoZona = r["idtipozona"].ToString(); IdZona = r["idzona"].ToString();
                FechaModificacion = r["Fechasys"].ToString(); CabezaFamilia = r["CabezaFamilia"].ToString();

                FecExpedicion = r["fecexpedicion"].ToString();
                int.TryParse(r["tipo_correo"].ToString(), out TipoCorreo);
                int.TryParse(r["TipoVehiculo"].ToString(), out TipoVehiculo);
                CodCIU = r["CodCIU"].ToString();

                JornadaLaboral = r["JornadaLaboral"].ToString(); Autorizacion = r["autoricentralriesgo"].ToString();
                Empleado = r["Empleado"].ToString(); pagareunico = r["pagareunico"].ToString();

                CONYFECNACEM = r["CONYFECNACEM"].ToString(); CONYTIPONIT = r["CONYTIPONIT"].ToString();
                CONYEXPEDIDA = r["CONYEXPEDIDA"].ToString(); CONYFECEXPEDICION = r["CONYFECEXPEDICION"].ToString();

                CONYSEXO = r["CONYSEXO"].ToString(); CONYFAX = r["CONYFAX"].ToString();
                CONYENVIODIR = r["CONYENVIODIR"].ToString(); CONYENDIRCOR = r["CONYENDIRCOR"].ToString();

                CONYEMPRESA = r["CONYEMPRESA"].ToString(); CONYAGENCIA = r["CONYAGENCIA"].ToString();
                CONYSECCION = r["CONYSECCION"].ToString(); CONYFECINGREEMP = r["CONYFECINGREEMP"].ToString();

                CONYNIVACADE = r["CONYNIVACADE"].ToString(); CONYTIPOSALARIO = r["CONYTIPOSALARIO"].ToString();
                CONYCESANTIAS = r["CONYCESANTIAS"].ToString(); CONYOTROING = r["CONYOTROING"].ToString();

                CONYDESOTROING = r["CONYDESOTROING"].ToString(); CONYPROFE = r["CONYPROFE"].ToString();
                admrecupub = r["admrecuspub"].ToString();

                Acierta = r["Acierta"].ToString(); saldodeudaexterna = r["saldodeudaexterna"].ToString();
                cuotadeudaexterna = r["cuotadeudaexterna"].ToString(); ScoreCifin = r["ScoreCifin"].ToString();

                CpAdmon = r["CpAdmon"].ToString(); CpAptos = r["CpAptos"].ToString();
                CpLocal = r["CpLocal"].ToString(); CpComision = r["CpComision"].ToString();

                califidatacredito = r["califidatacredito"].ToString(); SalMorDatacredito = r["SalMorDatacredito"].ToString();
                clasecupoPos = r["clasecupoPos"].ToString(); double.TryParse(r["valorClaseCupo"].ToString(), out valorClaseCupo);

                exoneradoSipa = r["exoneradoSipa"].ToString();
                DateTime.TryParse(r["fechaexonerado"].ToString(), out fechaexonerado);
                usuariosipla = r["usuariosipla"].ToString();
                IngVariables = r["IngVariables"].ToString();

                if (!string.IsNullOrEmpty(CONYTIPOSALARIO.Trim()))
                {
                    int cts = 0;
                    if (int.TryParse(CONYTIPOSALARIO, out cts)) { cts -= 1; if (cts < 0) cts = 0; CONYTIPOSALARIO = cts.ToString(); }
                    else CONYTIPOSALARIO = "0";
                }
                else CONYTIPOSALARIO = "0";

                if (!string.IsNullOrEmpty(CONYNIVACADE.Trim()))
                {
                    int cna = 0;
                    if (int.TryParse(CONYNIVACADE, out cna)) { if (cna < 0) cna = 0; CONYNIVACADE = cna.ToString(); }
                    else CONYNIVACADE = "0";
                }
                else CONYNIVACADE = "0";

                CONYTIPONIT = (Strings.InStr("CNETUR", CONYTIPONIT.Trim(), CompareMethod.Text) - 1).ToString();
                if (int.Parse(CONYTIPONIT) < 0) CONYTIPONIT = "0";

                CONYSEXO = (Strings.InStr("FM", CONYSEXO.Trim(), CompareMethod.Text) - 1).ToString();
                if (int.Parse(CONYSEXO) < 0) CONYSEXO = "0";

                if (!Information.IsDate(CONYFECNACEM))
                    CONYFECNACEM = new DateTime(1950, 1, 1).ToShortDateString();
                if (!Information.IsDate(CONYFECEXPEDICION))
                    CONYFECEXPEDICION = new DateTime(1950, 1, 1).ToShortDateString();
                if (!Information.IsDate(CONYFECINGREEMP))
                    CONYFECINGREEMP = new DateTime(1950, 1, 1).ToShortDateString();

                if (!Information.IsDate(VenceContrato))
                    VenceContrato = new DateTime(1950, 1, 1).ToShortDateString();
                if (!Information.IsDate(FecExpedicion))
                    FecExpedicion = new DateTime(1950, 1, 1).ToShortDateString();

                cenutilidad = r["cenutilidad"].ToString();
                pignora_aport = r["pignora_aport"].ToString();

                IngArriendos = r["IngArriendos"] == DBNull.Value ? "0" : r["IngArriendos"].ToString();
                IngPension = r["IngPension"] == DBNull.Value ? "0" : r["IngPension"].ToString();
                DeudasTerceros = r["DeudasTerceros"] == DBNull.Value ? "0" : r["DeudasTerceros"].ToString();
                GASTO_FIJO_MES = r["GASTO_FIJO_MES"] == DBNull.Value ? "0" : r["GASTO_FIJO_MES"].ToString();
                DstoPension = r["DstoPension"] == DBNull.Value ? "0" : r["DstoPension"].ToString();
                dstoGastosPerso = r["dstoGastosPerso"] == DBNull.Value ? "0" : r["dstoGastosPerso"].ToString();
                cappagoPorcentaje = r["cappagoPorcentaje"] == DBNull.Value ? "0" : r["cappagoPorcentaje"].ToString();
            }

            // Segunda parte: consulta con left join cop_retiros
            string stmysql;
            if (Navegar == Navega.Ninguno)
                where = " from sys_maenit a left join cop_retiros b on b.codigoter = a.codigoter where a.codigoter = '" + codigoter + "'";
            else if (Navegar == Navega.Primero)
                where = " from sys_maenit a left join cop_retiros b on b.codigoter = a.codigoter where a.codigoter > ' ' order by a.codigoter " + _varini.Pstlimit;
            else if (Navegar == Navega.Anterior)
                where = " from sys_maenit a left join cop_retiros b on b.codigoter = a.codigoter where a.codigoter = '" + codigoter + "' order by a.codigoter desc " + _varini.Pstlimit;
            else if (Navegar == Navega.Siguiente)
                where = " from sys_maenit a left join cop_retiros b on b.codigoter = a.codigoter where a.codigoter = '" + codigoter + "' order by a.codigoter " + _varini.Pstlimit;
            else if (Navegar == Navega.Ultimo)
                where = " from sys_maenit a left join cop_retiros b on b.codigoter = a.codigoter where a.codigoter <= '99999999999999' order by a.codigoter desc " + _varini.Pstlimit;

            stmysql = "select " + _varini.Psttop + " a.ciudad_cuenta as campo1,a.codigoter as campo2,a.motivo_retiro as campo3,b.EstadoAnt as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, myconect, "BuscaAsociado", ref CiudadCuenta, ref codigoter, ref MotivoRetiro, ref EstadoAnterior);

            string fechaing = " ";
            stmysql = "select " + _varini.Psttop + " a.fecha_reingreso as campo1, a.calman as campo2, FondoCesantia as campo3,RecibeFactura as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, myconect, "BuscaAsociado", ref fechaing, ref calman, ref FondoCesantia, ref RecibeFactura);

            return ok;
        }

        public bool BuscaEmpresa(ref string Codigo, OdbcConnection Connection,
            ref string nombre, ref string nombreResumen,
            ref string Direccion, ref string Nit,
            ref string Pagador, ref string Ciudad,
            ref string Telefono, ref string Fax,
            ref string Email, ref string FechaVence,
            ref string Plazo, ref string CptoNomina,
            Navega Navegar,
            ref string FechaCorte1, ref string FechaCorte2,
            ref string FechaCorte3, ref string Diacorte1,
            ref string Diacorte2, ref string Diacorte3,
            ref string FormatoEnvio, ref double PorDescuento,
            ref string TipoDescuento, ref string bloquearEmp, ref double PorcenViveres)
        {
            string where = "";
            bool ok = false;
            Codigo = Strings.Right("0000" + Codigo, 4);

            if (Navegar == Navega.Ninguno)
            {
                if (Codigo == "0000")
                {
                    Codigo = "";
                    return false;
                }
            }

            if (Navegar == Navega.Ninguno)
                where = " from cop_empresa13 where codigo_empresa = '" + Codigo + "'";
            else if (Navegar == Navega.Primero)
                where = " from cop_empresa13 where codigo_empresa > ' ' order by codigo_empresa " + _varini.Pstlimit;
            else if (Navegar == Navega.Anterior)
                where = " from cop_empresa13 where codigo_empresa < '" + Codigo + "' order by codigo_empresa desc " + _varini.Pstlimit;
            else if (Navegar == Navega.Siguiente)
                where = " from cop_empresa13 where codigo_empresa > '" + Codigo + "' order by codigo_empresa " + _varini.Pstlimit;
            else if (Navegar == Navega.Ultimo)
                where = " from cop_empresa13 where codigo_empresa <= '9999' order by codigo_empresa desc " + _varini.Pstlimit;

            string mysql = "";

            mysql = "select " + _varini.Psttop + " nombre as campo1 , nombre_resum as campo2 , nit as campo3 , direccion as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaEmpresa", ref nombre, ref nombreResumen, ref Nit, ref Direccion);

            mysql = "select " + _varini.Psttop + " pagador as campo1 , Ciudad as campo2 , telefono as campo3 , fax as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaEmpresa", ref Pagador, ref Ciudad, ref Telefono, ref Fax);

            mysql = "select " + _varini.Psttop + " email as campo1 , fechavence as campo2 , Plazo as campo3 , CptoNomina as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaEmpresa", ref Email, ref FechaVence, ref Plazo, ref CptoNomina);
            if (!Information.IsDate(FechaVence))
                FechaVence = new DateTime(1950, 1, 1).ToShortDateString();

            mysql = "select " + _varini.Psttop + " codigo_empresa as campo1,dia_corte1 as campo2,dia_corte2 as campo3,dia_corte3 as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaEmpresa", ref Codigo, ref Diacorte1, ref Diacorte2, ref Diacorte3);

            mysql = "select " + _varini.Psttop + " fecha_corte1 as campo1,fecha_corte2 as campo2,fecha_corte3 as campo3,FormatoEnvio as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaEmpresa", ref FechaCorte1, ref FechaCorte2, ref FechaCorte3, ref FormatoEnvio);

            string sPorDescuento = PorDescuento.ToString(), sPorcenViveres = PorcenViveres.ToString();
            mysql = "select " + _varini.Psttop + " PorDescuento as campo1,tipodsto as campo2, bloquearEmp as campo3, porviveres as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaEmpresa", ref sPorDescuento, ref TipoDescuento, ref bloquearEmp, ref sPorcenViveres);
            double.TryParse(sPorDescuento, out PorDescuento);
            double.TryParse(sPorcenViveres, out PorcenViveres);

            if (FechaVence == "0") FechaVence = "1/1/1950";
            if (FechaCorte1 == "0") FechaCorte1 = "1/1/1950";
            if (FechaCorte2 == "0") FechaCorte2 = "1/1/1950";
            if (FechaCorte3 == "0") FechaCorte3 = "1/1/1950";

            return ok;
        }

        /// <summary>
        /// Sobrecarga simplificada de BuscaEmpresa que solo retorna nombre
        /// </summary>
        public bool BuscaEmpresa(ref string Codigo, OdbcConnection Connection, ref string nombre)
        {
            string nombreResumen = "", Direccion = "", Nit = "", Pagador = "", Ciudad = "";
            string Telefono = "", Fax = "", Email = "", FechaVence = "1/1/1950";
            string Plazo = "", CptoNomina = "";
            string FechaCorte1 = "1/1/1950", FechaCorte2 = "1/1/1950", FechaCorte3 = "1/1/1950";
            string Diacorte1 = "0", Diacorte2 = "0", Diacorte3 = "0";
            string FormatoEnvio = "";
            double PorDescuento = 0, PorcenViveres = 0;
            string TipoDescuento = "0", bloquearEmp = "0";

            return BuscaEmpresa(ref Codigo, Connection,
                ref nombre, ref nombreResumen, ref Direccion, ref Nit,
                ref Pagador, ref Ciudad, ref Telefono, ref Fax,
                ref Email, ref FechaVence, ref Plazo, ref CptoNomina,
                Navega.Ninguno,
                ref FechaCorte1, ref FechaCorte2, ref FechaCorte3,
                ref Diacorte1, ref Diacorte2, ref Diacorte3,
                ref FormatoEnvio, ref PorDescuento,
                ref TipoDescuento, ref bloquearEmp, ref PorcenViveres);
        }

        #endregion

        #region Metodos de Ciudades y Parentesco

        public bool BuscaCiudad(string Codigo, OdbcConnection Connection, Navega Navegar, ref string Nombre)
        {
            string dummy = "";
            return BuscaCiudad(ref Codigo, Connection, Navegar, ref Nombre, ref dummy);
        }

        public bool BuscaCiudad(ref string Codigo, OdbcConnection Connection,
            ref string Nombre, ref string NombreDepartamento)
        {
            return BuscaCiudad(ref Codigo, Connection, Navega.Ninguno, ref Nombre, ref NombreDepartamento);
        }

        public bool BuscaCiudad(ref string Codigo, OdbcConnection Connection, Navega Navegar,
            ref string Nombre, ref string NombreDepartamento)
        {
            string where = "";

            if (Navegar == Navega.Ninguno)
            {
                if (string.IsNullOrEmpty(Codigo == null ? "" : Codigo.Trim()))
                {
                    Codigo = "";
                    return false;
                }
                if (Information.IsNumeric(Codigo))
                {
                    if (Codigo.Trim() == "0")
                    {
                        Codigo = "";
                        return false;
                    }
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from sys_ciudad57 where ciudad = '" + Codigo + "'";
                    break;
                case Navega.Primero:
                    where = " from sys_ciudad57 where ciudad > ' ' order by ciudad " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from sys_ciudad57 where ciudad < '" + Codigo + "' order by ciudad desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from sys_ciudad57 where ciudad > '" + Codigo + "' order by ciudad " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from sys_ciudad57 where ciudad <= '999999' order by ciudad desc " + _varini.Pstlimit;
                    break;
            }

            string dummy = "";
            string mysql = "select " + _varini.Psttop + "  NOMBRE_CIUDAD as campo1,dpto as campo2, ciudad as campo3 ";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaCiudad", ref Nombre, ref NombreDepartamento, ref Codigo, ref dummy);
            return _ok;
        }

        public bool BuscaParentesco(ref string codigo, OdbcConnection myconect, ref string nombre)
        {
            codigo = Strings.Right("0000" + codigo, 4);
            string dummy1 = "", dummy2 = "", dummy3 = "";
            _stmysql = "select nombre as campo1 from sys_parent51 where codigo  = '" + codigo + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconect, "BuscaParentesco", ref nombre, ref dummy1, ref dummy2, ref dummy3);
            return _ok;
        }

        #endregion

        #region Metodos de Imagen Circular

        public System.Drawing.Bitmap CargarImagen(OdbcConnection Conect, string codigo)
        {
            try
            {
                string cmdFoto = "select firma from cop_paracircular where codigo = '" + codigo + "'";
                DataSet dsFoto = new DataSet();
                _odbcConnect.ExecuteQueryDataset(cmdFoto, Conect, "cargarImagen", ref dsFoto, "circular");

                byte[] bits = (byte[])dsFoto.Tables["circular"].Rows[0][0];

                MemoryStream memorybits = new MemoryStream(bits);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(memorybits);
                dsFoto.Dispose();
                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void GrabarImagenFirma(string codigo, string photoFilePath, OdbcConnection Conect)
        {
            if (photoFilePath != null)
            {
                photoFilePath = photoFilePath.Trim();
                if (System.IO.File.Exists(photoFilePath))
                {
                    byte[] firma = _paramsys.GetPhoto(photoFilePath);
                    OdbcCommand addEmp = new OdbcCommand("update cop_paracircular set firma = " +
                        "? where codigo='" + codigo + "'", Conect);
                    addEmp.Parameters.Add("@firma", OdbcType.Image, firma.Length).Value = firma;
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

        public bool EliminarImagenFirma(string codigo, OdbcConnection Conect)
        {
            _stmysql = "update cop_paracircular set firma =  null where codigo = '" + codigo + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, Conect, "EliminarImagenFirma");
            return _ok;
        }

        #endregion

        #region Metodos de Lineas de Credito

        public bool BuscarLineaCredito(ref int lincred, OdbcConnection myconnect,
            ref string descripcion, ref decimal tasaInteres, ref int plazoMaximo, ref decimal montoMaximo,
            ref string requiereCodeudor, ref string estado, ref int diasGracia, ref decimal tasaMora)
        {
            return BuscarLineaCredito(ref lincred, myconnect, Navega.Ninguno,
                ref descripcion, ref tasaInteres, ref plazoMaximo, ref montoMaximo,
                ref requiereCodeudor, ref estado, ref diasGracia, ref tasaMora);
        }

        public bool BuscarLineaCredito(ref int lincred, OdbcConnection myconnect, Navega Navegar,
            ref string descripcion, ref decimal tasaInteres, ref int plazoMaximo, ref decimal montoMaximo,
            ref string requiereCodeudor, ref string estado, ref int diasGracia, ref decimal tasaMora)
        {
            string where = "";

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = "from cop_concar12 where lincred = " + lincred;
                    break;
                case Navega.Primero:
                    where = "from cop_concar12 where lincred > 0 order by lincred " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = "from cop_concar12 where lincred < " + lincred + " order by lincred desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = "from cop_concar12 where lincred > " + lincred + " order by lincred " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = "from cop_concar12 where lincred <= 9999 order by lincred desc " + _varini.Pstlimit;
                    break;
            }

            string sLincred = lincred.ToString(), sTasa = "0", sPlazo = "0", sMonto = "0";
            _stmysql = "select " + _varini.Psttop + " lincred as campo1, descripcion as campo2, tasa_interes as campo3, plazo_maximo as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarLineaCredito", ref sLincred, ref descripcion, ref sTasa, ref sPlazo);

            int.TryParse(sLincred, out lincred);
            decimal.TryParse(sTasa, out tasaInteres);
            int.TryParse(sPlazo, out plazoMaximo);

            string sDiasGracia = "0", sTasaMora = "0";
            _stmysql = "select " + _varini.Psttop + " monto_maximo as campo1, requiere_codeudor as campo2, estado as campo3, dias_gracia as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarLineaCredito", ref sMonto, ref requiereCodeudor, ref estado, ref sDiasGracia);

            decimal.TryParse(sMonto, out montoMaximo);
            int.TryParse(sDiasGracia, out diasGracia);

            string dummy1 = "", dummy2 = "", dummy3 = "";
            _stmysql = "select " + _varini.Psttop + " tasa_mora as campo1 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscarLineaCredito", ref sTasaMora, ref dummy1, ref dummy2, ref dummy3);

            decimal.TryParse(sTasaMora, out tasaMora);

            return _ok;
        }

        public DataSet BuscarLineasCredito(OdbcConnection myconnect, LineasCredito tipo = LineasCredito.Todos)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dataset = new DataSet();

            stbuilder.Append("select lincred, descripcion, tasa_interes, plazo_maximo, monto_maximo, ");
            stbuilder.Append("requiere_codeudor, estado, dias_gracia, tasa_mora ");
            stbuilder.Append("from cop_concar12 ");

            switch (tipo)
            {
                case LineasCredito.Conceptos:
                    stbuilder.Append("where tipo = '1' ");
                    break;
                case LineasCredito.Cartera:
                    stbuilder.Append("where tipo = '2' ");
                    break;
            }

            stbuilder.Append("order by lincred");

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarLineasCredito", ref dataset, "tbllineas");
            return dataset;
        }

        #endregion

        #region Metodos de Referencias

        public DataSet BuscarReferencias(string codigoter, OdbcConnection myconnect, TiposReferencia tipo = TiposReferencia.Todas)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dataset = new DataSet();

            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            stbuilder.Append("select codigoter, tipo_referencia, nombre, direccion, telefono, parentesco, ");
            stbuilder.Append("ciudad, empresa, cargo ");
            stbuilder.Append("from cop_referencia ");
            stbuilder.Append("where codigoter = '" + codigoter + "' ");

            switch (tipo)
            {
                case TiposReferencia.Familiares:
                    stbuilder.Append("and tipo_referencia = '1' ");
                    break;
                case TiposReferencia.Personales:
                    stbuilder.Append("and tipo_referencia = '2' ");
                    break;
                case TiposReferencia.Comerciales:
                    stbuilder.Append("and tipo_referencia = '3' ");
                    break;
                case TiposReferencia.Financieras:
                    stbuilder.Append("and tipo_referencia = '4' ");
                    break;
            }

            stbuilder.Append("order by tipo_referencia, nombre");

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarReferencias", ref dataset, "tblreferencias");
            return dataset;
        }

        #endregion

        #region Metodos de Solicitudes

        public DataSet BuscarSolicitudes(string codigo_ter, OdbcConnection myconect, DateTime fecha_inicial,
            DateTime fecha_final, bool SolicitudCredito)
        {
            DateTime fecrec = new DateTime(1950, 1, 1);
            codigo_ter = Strings.Right("00000000000000" + codigo_ter, 14);

            StringBuilder stbuilder = new StringBuilder();

            if (SolicitudCredito)
            {
                if (_varini.pstTipoBD.ToUpper() == "DB2")
                {
                    stbuilder.Append("select numero, estado, fecha_soli, vlr_solicitud, valor_aprobado, cop_solcre.lincred, cop_concar12.descripcion, cop_solcre.plazo, fecha_graba, ");
                    stbuilder.Append("case envpagador when 'Y' then case FecRecPagador when '" + Strings.Format(fecrec, _varini.PstForFec) + "' then 'Y' else 'N' end else 'N' end as envpagador,' ' as aprobado,' ' as nombenef ");
                    stbuilder.Append("from cop_solcre inner join cop_concar12 on cop_solcre.lincred=cop_concar12.lincred where codigoter='" + codigo_ter + "' ");
                    stbuilder.Append("and fecha_soli>='" + Strings.Format(fecha_inicial, _varini.PstForFec) + "' and fecha_soli<='" + Strings.Format(fecha_final, _varini.PstForFec) + "'");
                }
                else
                {
                    stbuilder.Append("select numero, estado, fecha_soli, vlr_solicitud, valor_aprobado, cop_solcre.lincred, cop_concar12.descripcion, cop_solcre.plazo, fecha_graba, ");
                    stbuilder.Append("case envpagador when 'Y' then case rtrim(FecRecPagador) when '" + Strings.Format(fecrec, _varini.PstForFec) + "' then 'Y' else 'N' end else 'N' end as envpagador,' ' as aprobado,' ' as nombenef ");
                    stbuilder.Append("from cop_solcre inner join cop_concar12 on cop_solcre.lincred=cop_concar12.lincred where codigoter='" + codigo_ter + "' ");
                    stbuilder.Append("and fecha_soli>='" + Strings.Format(fecha_inicial, _varini.PstForFec) + "' and fecha_soli<='" + Strings.Format(fecha_final, _varini.PstForFec) + "'");
                }
            }
            else
            {
                stbuilder.Append("select solaux.idsolaux as numero, case solaux.cerrado when 'Y' then (case solaux.estado when 'A' then 'G' else solaux.estado end) else solaux.estado end as estado, fecha_sol as fecha_soli, vlr_solicitado as vlr_solicitud, ");
                stbuilder.Append("vlr_aprobado as valor_aprobado, linea as lincred, cop_auxilio.nombre as descripcion, ");
                stbuilder.Append("case idbenef when '99999999999999' then solaux.codigoter else idbenef end as plazo, fecha_aprobado as fecha_graba,' ' as envpagador, aprobado, benef.nombre as nombenef ");
                stbuilder.Append("from cop_solaux solaux inner join cop_auxilio on solaux.linea=cop_auxilio.codigo ");
                stbuilder.Append("left join cop_benef benef on solaux.codigoter=benef.codigoter and solaux.idbenef=benef.cedula where solaux.codigoter='" + codigo_ter + "' ");
                stbuilder.Append("and fecha_sol between '" + Strings.Format(fecha_inicial, _varini.PstForFec) + "' and '" + Strings.Format(fecha_final, _varini.PstForFec) + "'");
            }

            DataSet MyRead = new DataSet();
            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconect, "TblSolicitudes", ref MyRead, "TblSolicitudes");
            return MyRead;
        }

        #endregion

        #region Metodos de Beneficiarios Seguro

        public DataSet CargaGrillaBenefSeguro(string Codigoter, int lincred, int numero, OdbcConnection myconect)
        {
            int canreg = 0, fila = 0;
            porcen = 0;
            Codigoter = Strings.Right("00000000000000" + Codigoter, 14);

            _stmysql = "select a.Idcedula as Identificacion, a.Nombre as Beneficiario, b.nombre as Parentesco, a.PorcSeg as Porcentaje " +
                "from cop_benefseg a inner join sys_parent51 b on b.codigo=a.Idparentesco where a.codigoter='" +
                Codigoter + "' and a.lincred='" + lincred + "' and a.numero='" + numero + "'";

            DataSet MyRead = new DataSet();
            _odbcConnect.ExecuteQueryDataset(_stmysql, myconect, "CargaGrillaBenefSeguro", ref MyRead, "TblBenefSeg");
            canreg = MyRead.Tables["TblBenefSeg"].Rows.Count;

            while (fila < canreg)
            {
                if (Information.IsNumeric(MyRead.Tables["TblBenefSeg"].Rows[fila]["porcentaje"]))
                {
                    porcen += Convert.ToDouble(MyRead.Tables["TblBenefSeg"].Rows[fila]["porcentaje"]);
                }
                fila++;
            }

            return MyRead;
        }

        #endregion

        #region Metodos de Asesores

        public bool BuscaAsesor(ref string Cedula, OdbcConnection Connection, Navega Navegar,
            ref string Nombre, ref string Direccion, ref string Telefono,
            ref string Ciudad, ref string Movil, ref string email)
        {
            string where = "";
            Cedula = Strings.Right("000000000000" + Cedula, 12);

            if (Navegar == Navega.Ninguno)
            {
                if (Cedula == "000000000000")
                {
                    Cedula = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from cop_asesores where idcedula = '" + Cedula + "'";
                    break;
                case Navega.Primero:
                    where = " from cop_asesores where idcedula > ' ' order by idcedula " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from cop_asesores where idcedula < '" + Cedula + "' order by idcedula desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from cop_asesores where idcedula > '" + Cedula + "' order by idcedula " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from cop_asesores where idcedula <= '99999999999999' order by idcedula desc " + _varini.Pstlimit;
                    break;
            }

            string mysql = "select " + _varini.Psttop + " nombre as campo1, direccion as campo2, idcedula as campo3, telefono as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaAsesor", ref Nombre, ref Direccion, ref Cedula, ref Telefono);

            string dummy = "";
            mysql = "select " + _varini.Psttop + " ciudad as campo1, movil as campo2, email as campo3 ";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaAsesor", ref Ciudad, ref Movil, ref email, ref dummy);

            return _ok;
        }

        public bool GrabarAsesor(string Cedula, OdbcConnection Connection, Acciones Accion,
            ref string Nombre, ref string Direccion, ref string Telefono,
            ref string Ciudad, ref string Movil, ref string email)
        {
            string mysql = "";
            Cedula = Strings.Right("000000000000" + Cedula, 12);

            if (Cedula == "000000000000")
            {
                Cedula = "";
                return false;
            }

            if (Accion == Acciones.Borrar)
            {
                mysql = "delete from cop_asesores where idcedula ='" + Cedula + "'";
            }
            else
            {
                string dummyNombre = "", dummyDir = "", dummyTel = "", dummyCiu = "", dummyMov = "", dummyEmail = "";
                if (BuscaAsesor(ref Cedula, Connection, Navega.Ninguno, ref dummyNombre, ref dummyDir, ref dummyTel, ref dummyCiu, ref dummyMov, ref dummyEmail))
                {
                    mysql = "update cop_asesores set nombre = '" + Nombre + "', direccion = '" + Direccion +
                        "', telefono= '" + Telefono + "', ciudad = '" + Ciudad +
                        "', movil = '" + Movil + "', email = '" + email + "' where idcedula ='" + Cedula + "'";
                }
                else
                {
                    mysql = "insert into cop_asesores (idcedula, nombre, direccion, telefono, ciudad, movil, email) values ('" +
                        Cedula + "','" + Nombre.Trim() + "','" + Direccion + "','" + Telefono + "','" +
                        Ciudad + "','" + Movil + "','" + email + "')";
                }
            }

            if (!string.IsNullOrEmpty(mysql))
            {
                return _odbcConnect.ExecuteQueryconec(mysql, Connection, "GrabarAsesor");
            }
            return false;
        }

        #endregion

        #region Metodos de Agencias

        public bool BuscaAgencia(ref string Codigo, OdbcConnection Connection, Navega Navegar,
            ref string Nombre, ref string NombreResumen)
        {
            string where = "";
            Codigo = Strings.Right("0000" + Codigo, 4);

            if (Navegar == Navega.Ninguno)
            {
                if (Codigo == "0000")
                {
                    Codigo = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from sys_agencia where codigo = '" + Codigo + "'";
                    break;
                case Navega.Primero:
                    where = " from sys_agencia where codigo > ' ' order by codigo " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from sys_agencia where codigo < '" + Codigo + "' order by codigo desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from sys_agencia where codigo > '" + Codigo + "' order by codigo " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from sys_agencia where codigo <= '9999' order by codigo desc " + _varini.Pstlimit;
                    break;
            }

            string dummy = "";
            string mysql = "select " + _varini.Psttop + " nombre as campo1, nomres as campo2, codigo as campo3 ";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaAgencia", ref Nombre, ref NombreResumen, ref Codigo, ref dummy);
            return _ok;
        }

        public bool BuscaAgencia(ref string Codigo, OdbcConnection Connection, ref DataSet DsDataset, Navega Navegar = Navega.Ninguno)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsData = new DataSet();

            string where = "";
            Codigo = Strings.Right("0000" + Codigo, 4);

            if (Navegar == Navega.Ninguno)
            {
                if (Codigo == "0000")
                {
                    Codigo = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from sys_agencia where codigo = '" + Codigo + "'";
                    break;
                case Navega.Primero:
                    where = " from sys_agencia where codigo > ' ' order by codigo";
                    break;
                case Navega.Anterior:
                    where = " from sys_agencia where codigo < '" + Codigo + "' order by codigo desc";
                    break;
                case Navega.Siguiente:
                    where = " from sys_agencia where codigo > '" + Codigo + "' order by codigo";
                    break;
                case Navega.Ultimo:
                    where = " from sys_agencia where codigo <= '9999' order by codigo desc";
                    break;
            }

            stbuilder.Append("select codigo, nombre, nomres " + where);

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Connection, "BuscaAgencia", ref DsData, "tblAgencia");

            if (DsData.Tables["tblAgencia"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsData.Tables["tblAgencia"].Copy());
                }
                catch { }
                return true;
            }
            return false;
        }

        public bool GrabarAgencia(string Codigo, OdbcConnection Connection, string Nombre = "", string NombreResumido = "")
        {
            string mysql = "";
            Codigo = Strings.Right("0000" + Codigo, 4);

            if (Codigo == "0000")
            {
                Codigo = "";
                return false;
            }

            string dummyNom = "", dummyNomRes = "";
            _ok = BuscaAgencia(ref Codigo, Connection, Navega.Ninguno, ref dummyNom, ref dummyNomRes);

            if (!_ok)
            {
                mysql = "insert into sys_agencia (codigo, nombre, nomres) values('" + Codigo + "','" + Nombre + "','" + NombreResumido + "')";
            }
            else
            {
                mysql = "update sys_agencia set nombre = '" + Nombre + "', nomres = '" + NombreResumido + "' where codigo = '" + Codigo + "'";
            }

            if (!string.IsNullOrEmpty(mysql))
            {
                return _odbcConnect.ExecuteQueryconec(mysql, Connection, "GrabarAgencia");
            }
            return false;
        }

        public bool EliminarAgencia(string codigo, OdbcConnection myconnect)
        {
            codigo = Strings.Right("0000" + codigo, 4);
            _stmysql = "delete from sys_agencia where codigo ='" + codigo + "'";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminarAgencia");
            return _ok;
        }

        #endregion

        #region Metodos de Cuentas

        public bool BuscarCuenta(ref string Cuenta, OdbcConnection mycocnect, Navega Navegar,
            ref string Tercero, ref string mane_cencos, ref string natura, ref string cencos,
            ref int nivel, ref string Aplicart, ref string Nombre)
        {
            string where = "";
            Cuenta = Strings.Left(Cuenta + "000000000000", 12);

            if (Navegar == Navega.Ninguno)
            {
                if (Cuenta == "000000000000")
                {
                    Cuenta = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from cnt_maecuen where cuenta = '" + Cuenta + "'";
                    break;
                case Navega.Primero:
                    where = " from cnt_maecuen where cuenta > ' ' order by codigo " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from cnt_maecuen where cuenta < '" + Cuenta + "' order by codigo desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from cnt_maecuen where cuenta > '" + Cuenta + "' order by codigo " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from cnt_maecuen where cuenta <= '9999' order by codigo desc " + _varini.Pstlimit;
                    break;
            }

            _stmysql = "select " + _varini.Psttop + " tercero as campo1, mane_cencos as campo2, natura as campo3, Cencos as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, mycocnect, "BuscarCuenta", ref Tercero, ref mane_cencos, ref natura, ref cencos);

            string sNivel = "0";
            _stmysql = "select " + _varini.Psttop + " nivel as campo1, APLI_CARTCOOPE as campo2, CENCOS AS campo3, nombre as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, mycocnect, "BuscarCuenta", ref sNivel, ref Aplicart, ref cencos, ref Nombre);
            int.TryParse(sNivel, out nivel);

            string dummy1 = "", dummy2 = "", dummy3 = "";
            _stmysql = "select " + _varini.Psttop + " cuenta as campo1 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, mycocnect, "BuscarCuenta", ref Cuenta, ref dummy1, ref dummy2, ref dummy3);

            return _ok;
        }

        #endregion

        #region Metodos de Cargos

        public bool BuscaCargos(ref string Codigo, OdbcConnection Connection, Navega Navegar,
            ref string Nombre)
        {
            string where = "";
            Codigo = Strings.Right("0000" + Codigo, 4);

            if (Navegar == Navega.Ninguno)
            {
                if (Codigo == "0000")
                {
                    Codigo = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from sys_cargo55 where codigo_cargo = '" + Codigo + "'";
                    break;
                case Navega.Primero:
                    where = " from sys_cargo55 where codigo_cargo > ' ' order by codigo_cargo " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from sys_cargo55 where codigo_cargo < '" + Codigo + "' order by codigo_cargo desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from sys_cargo55 where codigo_cargo > '" + Codigo + "' order by codigo_cargo " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from sys_cargo55 where codigo_cargo <= '9999' order by codigo_cargo desc " + _varini.Pstlimit;
                    break;
            }

            string mysql = "select " + _varini.Psttop + " nombre as campo1, codigo_cargo as campo2 ";
            string dummy1 = "", dummy2 = "";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaCargos", ref Nombre, ref Codigo, ref dummy1, ref dummy2);
            return _ok;
        }

        public bool GrabarCargos(string Codigo, Acciones Accion, OdbcConnection Connection,
            string Nombre = "", string Nomres = "")
        {
            string mysql = "";
            Codigo = Strings.Right("0000" + Codigo, 4);

            if (Codigo == "0000")
            {
                Codigo = "";
                return false;
            }

            if (Accion == Acciones.Borrar)
            {
                mysql = "delete from sys_cargo55 where codigo_cargo ='" + Codigo + "'";
            }
            else
            {
                string dummyNombre = "";
                if (BuscaCargos(ref Codigo, Connection, Navega.Ninguno, ref dummyNombre))
                {
                    mysql = "update sys_cargo55 set nombre = '" + Nombre + "', nomres = '" + Nomres + "' where codigo_cargo = '" + Codigo + "'";
                }
                else
                {
                    mysql = "insert into sys_cargo55 (codigo_cargo, nombre, nomres) values('" + Codigo + "','" + Nombre + "','" + Nomres + "')";
                }
            }

            if (!string.IsNullOrEmpty(mysql))
            {
                return _odbcConnect.ExecuteQueryconec(mysql, Connection, "GrabarCargos");
            }
            return false;
        }

        public string HelpCargos(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_cargo55", "codigo_cargo", "nombre", "nomres", Myconnect, Myforma, "Nombre", "Nombre resumido", null, null, ref respCampo);
        }

        #endregion

        #region Metodos de Lineas

        public virtual bool BuscaLinea(ref int Lincred, OdbcConnection Connection,
            Navega Navegar, ref string Descripcion, ref decimal TasaInt,
            ref decimal CupoAportes, ref decimal Cuantia, ref string DebCred,
            ref bool MueEstCuenta, ref bool HaceCausa, ref bool ConsulSaldo,
            ref decimal AporMinimo, ref string Cuenta, ref bool MuestraSaldo,
            ref bool TieneVemto, ref bool ManejaAtraso, ref int plazo,
            ref int ClaCuo, ref int clasei, ref decimal tasaex, ref string centroco,
            ref int TipoLinea, ref string ClaAdmon, ref string TipSeguro,
            ref double VlrAdmon, ref double VlrSeguro, ref int Equisuper,
            ref int MuestraInternet, ref bool LiqDiasMora, ref int TiempoMinimo,
            ref decimal TasaContribuccion, ref bool deducion, ref string EsAhProgViv,
            ref string ApExtra, ref string CONSE, ref string CapitalRiesgo, ref int idcptoDscto)
        {
            string internet = " ";
            string where = "";
            bool ok = false;
            string deduciones = "";

            if (Navegar == Navega.Ninguno)
            {
                if (Lincred == 0)
                {
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from cop_concar12 where lincred = '" + Lincred + "'";
                    break;
                case Navega.Primero:
                    where = " from cop_concar12 where lincred > '0' order by lincred " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from cop_concar12 where lincred < " + Lincred + " order by lincred desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from cop_concar12 where lincred > " + Lincred + " order by lincred " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from cop_concar12 where lincred <= 9999 order by lincred desc " + _varini.Pstlimit;
                    break;
            }

            string sClaCuo = "0", sClasei = "0", sTasaex = "0", sCentroco = centroco;
            string stmysql = "Select " + _varini.Psttop + " clacuo as campo1, clasei as campo2,tasaex as campo3,centroco as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref sClaCuo, ref sClasei, ref sTasaex, ref sCentroco);
            int.TryParse(sClaCuo, out ClaCuo);
            int.TryParse(sClasei, out clasei);
            decimal.TryParse(sTasaex, out tasaex);
            centroco = sCentroco;

            string tipolin = "0", sLincred = Lincred.ToString(), sTasaInt = "0";
            stmysql = "Select " + _varini.Psttop + " codahor as campo1,lincred as campo2,descripcion as campo3,tasai as campo4  ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref tipolin, ref sLincred, ref Descripcion, ref sTasaInt);
            if (!Information.IsNumeric(tipolin))
                tipolin = "0";
            TipoLinea = int.Parse(tipolin);
            int.TryParse(sLincred, out Lincred);
            decimal.TryParse(sTasaInt, out TasaInt);

            string MuesEsta = "", sCupoAp = "0", sCuantia = "0";
            stmysql = "Select " + _varini.Psttop + " cupoap as campo1, cuantia as campo2,debcre as campo3,estcta as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref sCupoAp, ref sCuantia, ref DebCred, ref MuesEsta);
            decimal.TryParse(sCupoAp, out CupoAportes);
            decimal.TryParse(sCuantia, out Cuantia);
            int debCredVal = Strings.InStr("DC", DebCred, CompareMethod.Text);
            if (debCredVal < 0) debCredVal = 0;
            DebCred = debCredVal.ToString();
            MueEstCuenta = MuesEsta == "Y";

            string hacecau = "", consal = "", sApoMin = "0";
            stmysql = "Select " + _varini.Psttop + " compri as campo1, consal as campo2,apomin as campo3,cuenta as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref hacecau, ref consal, ref sApoMin, ref Cuenta);
            HaceCausa = hacecau == "Y";
            ConsulSaldo = consal == "Y";
            decimal.TryParse(sApoMin, out AporMinimo);

            string muessal = "", tienevem = "", maneatra = "", DiasMora = "N";
            stmysql = "Select " + _varini.Psttop + " versaldo as campo1, venmento as campo2,intmora as campo3,claadmon as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref muessal, ref tienevem, ref maneatra, ref ClaAdmon);
            MuestraSaldo = muessal == "Y";
            TieneVemto = tienevem == "Y";
            ManejaAtraso = maneatra == "Y";

            string sTipSeguro = TipSeguro, sVlrAdmon = "0", sVlrSeguro = "0", sPlazo = "0";
            stmysql = "Select " + _varini.Psttop + " POAPEN as campo1, TASADM as campo2,seguro as campo3,plazo as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref sTipSeguro, ref sVlrAdmon, ref sVlrSeguro, ref sPlazo);
            TipSeguro = sTipSeguro;
            double.TryParse(sVlrAdmon, out VlrAdmon);
            double.TryParse(sVlrSeguro, out VlrSeguro);
            int.TryParse(sPlazo, out plazo);

            string sEquisuper = "0", sDiasMora = "N", sTiempoMin = "0";
            stmysql = "Select " + _varini.Psttop + " Equisuper as campo1, internet as campo2,liqDiasMora as campo3,mesesa as campo4 ";
            ok = ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref sEquisuper, ref internet, ref sDiasMora, ref sTiempoMin);
            int.TryParse(sEquisuper, out Equisuper);
            if (Information.IsNumeric(internet))
                int.TryParse(internet, out MuestraInternet);
            else
                MuestraInternet = 0;
            LiqDiasMora = sDiasMora == "Y";
            int.TryParse(sTiempoMin, out TiempoMinimo);

            string sTasaContrib = "0", sEsAhProgViv = "0";
            stmysql = "Select gravamen as campo1, codnom as campo2, INTCDAT as campo3, ApExtra as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref sTasaContrib, ref deduciones, ref sEsAhProgViv, ref ApExtra);
            decimal.TryParse(sTasaContrib, out TasaContribuccion);
            EsAhProgViv = sEsAhProgViv;
            deducion = deduciones != "1";

            string sCONSE = "", sCapRiesgo = "N", sIdcptoDscto = "0";
            string dummy = "";
            stmysql = "Select " + _varini.Psttop + " CONSE as campo1,CapitalRiesgo as campo2,idcptoDscto as campo3 ";
            ok = _odbcConnect.ExecuteQueryconec(stmysql + where, Connection, "BuscaLinea", ref sCONSE, ref sCapRiesgo, ref sIdcptoDscto, ref dummy);
            CONSE = sCONSE;
            CapitalRiesgo = sCapRiesgo;
            int.TryParse(sIdcptoDscto, out idcptoDscto);

            return ok;
        }

        /// <summary>
        /// Sobrecarga simplificada de BuscaLinea que solo retorna Descripcion
        /// </summary>
        public virtual bool BuscaLinea(ref int Lincred, OdbcConnection Connection, ref string Descripcion)
        {
            decimal TasaInt = 0, CupoAportes = 0, Cuantia = 0, AporMinimo = 0, tasaex = 0, TasaContribuccion = 0;
            string DebCred = "0", Cuenta = "", centroco = "99999999", ClaAdmon = "0", TipSeguro = "0";
            string EsAhProgViv = "0", ApExtra = "N", CONSE = "", CapitalRiesgo = "N";
            bool MueEstCuenta = false, HaceCausa = false, ConsulSaldo = false;
            bool MuestraSaldo = false, TieneVemto = false, ManejaAtraso = false, LiqDiasMora = false, deducion = true;
            int plazo = 0, ClaCuo = 0, clasei = 0, TipoLinea = 0, Equisuper = 0, MuestraInternet = 0, TiempoMinimo = 0, idcptoDscto = 0;
            double VlrAdmon = 0, VlrSeguro = 0;

            return BuscaLinea(ref Lincred, Connection, Navega.Ninguno,
                ref Descripcion, ref TasaInt, ref CupoAportes, ref Cuantia, ref DebCred,
                ref MueEstCuenta, ref HaceCausa, ref ConsulSaldo,
                ref AporMinimo, ref Cuenta, ref MuestraSaldo, ref TieneVemto,
                ref ManejaAtraso, ref plazo,
                ref ClaCuo, ref clasei, ref tasaex, ref centroco,
                ref TipoLinea, ref ClaAdmon, ref TipSeguro,
                ref VlrAdmon, ref VlrSeguro, ref Equisuper,
                ref MuestraInternet, ref LiqDiasMora, ref TiempoMinimo,
                ref TasaContribuccion, ref deducion, ref EsAhProgViv,
                ref ApExtra, ref CONSE, ref CapitalRiesgo, ref idcptoDscto);
        }

        public virtual bool BuscaLinea(ref int Lincred, ref DataSet DsDataSet, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            try
            {
                DsDataSet.Tables["tbllineas"].Rows.Clear();
            }
            catch { }

            stbuilder.Append("select clacuo , clasei ,tasaex ,centroco,codahor,lincred ,descripcion, tasai,cupoap , cuantia ,debcre ,estcta,catea,cateb,catec,cated,catee,");
            stbuilder.Append(" compri , consal ,apomin ,cuenta , versaldo , venmento ,foradmon ,claadmon , POAPEN , TASADM ,seguro ,plazo,intcie,totintant,pergracia,mesgracia, ");
            stbuilder.Append("Previv,forcap,sumaga,codseg,codadm,cpto_caradi,Tasaca,sumaga,CLASEI,codseg,codadm,cpto_caradi,CptoCapitalizacion, apomin,valsegmin,valsegmax,valadmin,valadmax, ");
            stbuilder.Append("mesesa,FOGACLA, ctaintin,intfin,modiclacuo,tipointeres,dtf,CTAINTOD,CTAINTOC,gravamen,EquiSuper,bloqFechaDstoProyec,INTCDAT,ApExtra,tasaeq,CONSE,CapitalRiesgo,idcptoDscto   from cop_concar12 where lincred = '" + Lincred + "'");

            _ok = _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaLinea", ref DsDataSet, "tbllineas");
            return _ok;
        }

        #endregion

        #region Metodos de Lineas de Auxilio

        public bool BuscaLineaAuxilio(ref string Codigo, OdbcConnection Connection, Navega Navegar,
            ref string Nombre, ref string NombreResumen, ref string Cuenta, ref string Observa,
            ref double ValorAuxilio, ref int claseAux, ref string Comite,
            ref double Gravamen, ref string CuentaGravamen, ref string CuentaGasto)
        {
            string where = "";
            Codigo = Strings.Right("0000" + Codigo, 4);

            if (Navegar == Navega.Ninguno)
            {
                if (Codigo == "0000")
                {
                    Codigo = "";
                    return false;
                }
            }

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = " from cop_auxilio where codigo = '" + Codigo + "'";
                    break;
                case Navega.Primero:
                    where = " from cop_auxilio where codigo > ' ' order by codigo " + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = " from cop_auxilio where codigo < '" + Codigo + "' order by codigo desc " + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = " from cop_auxilio where codigo > '" + Codigo + "' order by codigo " + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = " from cop_auxilio where codigo <= '9999' order by codigo desc " + _varini.Pstlimit;
                    break;
            }

            string mysql = "select nombre as campo1, nomres as campo2, cuenta as campo3, observaciones as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaLineaAuxilio", ref Nombre, ref NombreResumen, ref Cuenta, ref Observa);

            string sValor = "0", sClase = "0";
            mysql = "select codigo as campo1, Valor as campo2, ClaAux as campo3, Comite as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaLineaAuxilio", ref Codigo, ref sValor, ref sClase, ref Comite);
            double.TryParse(sValor, out ValorAuxilio);
            int.TryParse(sClase, out claseAux);

            string sGravamen = "0", dummy = "";
            mysql = "select gravamen as campo1, CuentaGravamen as campo2, CuentaGastoGravamen as campo3 ";
            _ok = _odbcConnect.ExecuteQueryconec(mysql + where, Connection, "BuscaLineaAuxilio", ref sGravamen, ref CuentaGravamen, ref CuentaGasto, ref dummy);
            double.TryParse(sGravamen, out Gravamen);

            return _ok;
        }

        public bool GrabarAuxilio(string Codigo, OdbcConnection Connection, string Nombre = "", string NombreResumido = "",
            string Cuenta = "", string Observa = "", double ValorAuxilio = 0, int claseAux = 0,
            string Comite = "9999", double Gravamen = 0, string CuentaGravamen = "999999999999", string CuentaGasto = "999999999999")
        {
            string mysql = "";
            Codigo = Strings.Right("0000" + Codigo, 4);
            Comite = Strings.Right("0000" + Comite, 4);

            if (Codigo == "0000")
            {
                Codigo = "";
                return false;
            }

            string dummyNom = "", dummyNomRes = "", dummyCta = "", dummyObs = "", dummyCom = "", dummyCGrav = "", dummyCGasto = "";
            double dummyVal = 0, dummyGrav = 0;
            int dummyCla = 0;

            _ok = BuscaLineaAuxilio(ref Codigo, Connection, Navega.Ninguno, ref dummyNom, ref dummyNomRes, ref dummyCta, ref dummyObs,
                ref dummyVal, ref dummyCla, ref dummyCom, ref dummyGrav, ref dummyCGrav, ref dummyCGasto);

            if (!_ok)
            {
                mysql = "insert into cop_auxilio (codigo, nombre, nomres, cuenta, observaciones, Valor, ClaAux, Comite, gravamen, CuentaGravamen, CuentaGastoGravamen) values('" +
                    Codigo + "','" + Nombre + "','" + NombreResumido + "','" + Cuenta + "','" + Observa + "','" +
                    ValorAuxilio + "','" + claseAux + "','" + Comite + "','" + Gravamen + "','" + CuentaGravamen + "','" + CuentaGasto + "')";
            }
            else
            {
                mysql = "update cop_auxilio set nombre = '" + Nombre + "', nomres = '" + NombreResumido + "', cuenta = '" + Cuenta +
                    "', observaciones = '" + Observa + "', Valor = '" + ValorAuxilio + "', ClaAux = '" + claseAux +
                    "', Comite='" + Comite + "', gravamen='" + Gravamen + "', CuentaGravamen='" + CuentaGravamen +
                    "', CuentaGastoGravamen='" + CuentaGasto + "' where codigo = '" + Codigo + "'";
            }

            _ok = _odbcConnect.ExecuteQueryconec(mysql, Connection, "GrabarAuxilio");
            return _ok;
        }

        public void EliminaLineaAuxilio(string Codigo, OdbcConnection Myconect)
        {
            _stmysql = "delete from cop_auxilio where codigo ='" + Codigo + "'";
            _odbcConnect.ExecuteQueryconec(_stmysql, Myconect, "EliminaLineaAuxilio");
        }


        #endregion

        #region Metodos Help

        public string HelpPersonaCargo(OdbcConnection Myconnect, Form Myforma, string codigoter)
        {
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            string respCampo = null;
            return _msgsas.CargaAyuda("sys_cargo55", "codigo_cargo", "nombre", "", Myconnect, Myforma,
                "Nombre", "           ", null, " codigoter='" + codigoter + "' and estado<>'T'", ref respCampo);
        }

        public string HelpLineasCredito(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("cop_concar12", "lincred", "descripcion", "descripcion", Myconnect, Myforma, "Nombre", "Nombre resumido", null, null, ref respCampo);
        }

        public string HelpAsociados(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_maenit", "codigoter", "nombre", "apellido", Myconnect, Myforma, "Nombre", "Nombre resumido", null, null, ref respCampo);
        }

        public string HelpAgencias(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("sys_agencia", "codigo", "nombre", "nomres", Myconnect, Myforma, "Nombre", "Nombre resumido", null, null, ref respCampo);
        }

        public string HelpAuxilios(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            return _msgsas.CargaAyuda("cop_auxilio", "codigo", "nombre", "nomres", Myconnect, Myforma, "Nombre", "Nombre resumido", null, null, ref respCampo);
        }

        public DataSet CargarReferencias(string codigoter, OdbcConnection myconnect, TiposReferencia tiporefencia = TiposReferencia.Todas)
        {
            DataSet dsdataset = new DataSet();
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            _stmysql = "select case TipoReferencia when '1' then '1-Familiares' when '2' then '2-Personales' when '3' then '3-Comerciales' when '4' then '4-Financieras' end as Referencia," +
                       "p.nombre as Nomparentesco, a.Nombre, Direccion, Telefono,Celular, Nombre_ciudad, Contacto, " +
                       "case Tipo_Producto when '0' then ' ' when '1' then '1-Tarj. Credito' when '2' then '2-Cuenta Ahorro' when '3' then '3-Prestamo' when '4' then '4-Cdat' when '5' then '5-Otros' end as Tipo_Producto ," +
                       "NroPrducto as Nro_Producto,a.Ciudad,a.parentesco from sys_referencia a inner join sys_ciudad57 b on a.ciudad=b.ciudad left join sys_parent51 p on a.parentesco=p.codigo where codigoter='" + codigoter + "'" +
                       (tiporefencia == TiposReferencia.Todas ? "" : " and tiporeferencia='" + (int)tiporefencia + "'") + " order by TipoReferencia";

            _odbcConnect.ExecuteQueryDataset(_stmysql, myconnect, "CargaCredritosCoodeudor", ref dsdataset, "TblReferencia");
            return dsdataset;
        }

        public DataSet CargarVentanaReferencias(string codigoter, string NomEmpresa, Form forma,
            DataSet dsrefe, OdbcConnection myconnect, TiposReferencia tiporeferencia = TiposReferencia.Todas)
        {
            frmreferencias frmRefe = new frmreferencias(myconnect);
            DataSet dsdata = CargarReferencias(codigoter, myconnect, tiporeferencia);

            frmRefe.codigoter = codigoter;
            frmRefe.LblNomEmpresa.Text = NomEmpresa;
            frmRefe.DgwReferencias.AutoGenerateColumns = false;
            frmRefe.DgwReferencias.DataSource = dsdata.Tables["TblReferencia"];
            frmRefe.dsreferencia = dsrefe;
            frmRefe.ShowDialog(forma);

            return frmRefe.dsreferencia;
        }

        public virtual bool GrabarReferencias(string codigoter, DataGridView grilla, OdbcConnection myconnect)
        {
            int i = 0;
            StringBuilder stbuilder = new StringBuilder();
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            _stmysql = "delete from sys_referencia where codigoter='" + codigoter + "'";
            _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "Grabar/ElimiarReferencias");

            while (i < grilla.RowCount)
            {
                stbuilder.Append("insert into sys_referencia(");
                stbuilder.Append("TipoReferencia, codigoter, nombre, direccion, Telefono, ciudad, contacto, Tipo_Producto, NroPrducto,celular,parentesco) ");
                stbuilder.Append("values ('");
                stbuilder.Append(Strings.Left(grilla[0, i].Value.ToString(), 1) + "','");
                stbuilder.Append(codigoter + "','");
                stbuilder.Append(grilla[2, i].Value + "','");
                stbuilder.Append(grilla[3, i].Value + "','");
                stbuilder.Append(grilla[4, i].Value + "',");
                stbuilder.Append(grilla[10, i].Value + ",'");
                stbuilder.Append(grilla[7, i].Value + "','");
                stbuilder.Append((!Information.IsNumeric(Strings.Left(grilla[8, i].Value.ToString(), 1)) ? "0" : Strings.Left(grilla[8, i].Value.ToString(), 1)) + "',");
                stbuilder.Append((!Information.IsNumeric(grilla[9, i].Value) ? "0" : grilla[9, i].Value.ToString()) + ",'");
                stbuilder.Append((!Information.IsNumeric(grilla[5, i].Value) ? "0" : grilla[5, i].Value.ToString()) + "','");
                stbuilder.Append(grilla[11, i].Value + "')");

                string dummy1 = "", dummy2 = "", dummy3 = "", dummy4 = "";
                _ok = ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabarReferencias", ref dummy1, ref dummy2, ref dummy3, ref dummy4);
                //stbuilder.Clear();

                i++;
            }
            return _ok;
        }

        public virtual bool GrabarReferencias(string codigoter, string TipoRef, string Parentesco, string nombre,
            string direccion, string telefono, int ciudad, string contacto, string tipproducto,
            string numproducto, string celular, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            stbuilder.Append("insert into sys_referencia(");
            stbuilder.Append("TipoReferencia, codigoter, nombre, direccion, Telefono, ciudad, contacto, Tipo_Producto, NroPrducto,celular,parentesco) ");
            stbuilder.Append("values ('");
            stbuilder.Append(TipoRef + "','");
            stbuilder.Append(codigoter + "','");
            stbuilder.Append(nombre + "','");
            stbuilder.Append(direccion + "','");
            stbuilder.Append(telefono + "',");
            stbuilder.Append(ciudad + ",'");
            stbuilder.Append(contacto + "',");
            stbuilder.Append(!Information.IsNumeric(tipproducto) ? "'0'," : "'" + tipproducto + "',");
            stbuilder.Append((!Information.IsNumeric(numproducto) ? "0" : numproducto) + ",'");
            stbuilder.Append(celular + "','");
            stbuilder.Append(Parentesco + "')");

            _ok = _odbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabarReferencias");
            return _ok;
        }

        public bool BuscaBanco(ref string Banco, OdbcConnection myconnect,
            ref string Nombre, ref string NombreResumen, ref string CodCuenta,
            ref string Nit, ref string Ciudad, ref string Direccion, ref string Telefono,
            ref string UltCheque, ref string Cheque4x1000, Navega Navegar,
            ref string Copias, ref string Tipo, ref string Cuenta4xmil,
            ref string EstruDispersion, ref string Control_Consec, ref string CobraCanje,
            ref string ValorCanje, ref string TipoIdentificacion, ref string CobrarGmf,
            ref string FormaImpresion)
        {
            string where = "";
            switch (Navegar)
            {
                case Navega.Ninguno: where = " where codigo = '" + Banco + "'"; break;
                case Navega.Primero: where = " order by codigo " + _varini.Pstlimit; break;
                case Navega.Anterior: where = " where codigo < '" + Banco + "' order by codigo desc " + _varini.Pstlimit; break;
                case Navega.Siguiente: where = " where codigo > '" + Banco + "' order by codigo " + _varini.Pstlimit; break;
                case Navega.Ultimo: where = " order by codigo desc " + _varini.Pstlimit; break;
            }
            string stmysql = "select " + _varini.Psttop + " codigo as campo1, nombre as campo2, nombreresumen as campo3, codcuenta as campo4 from sys_bancos " + where;
            _ok = _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaBanco", ref Banco, ref Nombre, ref NombreResumen, ref CodCuenta);
            if (!_ok) return false;
            stmysql = "select " + _varini.Psttop + " nit as campo1, ciudad as campo2, direccion as campo3, telefono as campo4 from sys_bancos " + where;
            _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaBanco", ref Nit, ref Ciudad, ref Direccion, ref Telefono);
            stmysql = "select " + _varini.Psttop + " ultcheque as campo1, cheque4x1000 as campo2, copias as campo3, tipo as campo4 from sys_bancos " + where;
            _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaBanco", ref UltCheque, ref Cheque4x1000, ref Copias, ref Tipo);
            stmysql = "select " + _varini.Psttop + " cuenta4xmil as campo1, EstruDisper as campo2, Control_Consec as campo3, CobraCanje as campo4 from sys_bancos " + where;
            _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaBanco", ref Cuenta4xmil, ref EstruDispersion, ref Control_Consec, ref CobraCanje);
            stmysql = "select " + _varini.Psttop + " ValorCanje as campo1, TipoIdentificacion as campo2, CobrarGmf as campo3, FormaImpresion as campo4 from sys_bancos " + where;
            _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaBanco", ref ValorCanje, ref TipoIdentificacion, ref CobrarGmf, ref FormaImpresion);
            return _ok;
        }

        public bool BuscaBanco(string Banco, OdbcConnection myconnect)
        {
            string _d = ""; string _ult = "0";
            string _b = Banco;
            return BuscaBanco(ref _b, myconnect,
                ref _d, ref _d, ref _d, ref _d, ref _d, ref _d, ref _d,
                ref _ult, ref _d, Navega.Ninguno,
                ref _d, ref _d, ref _d, ref _d, ref _d, ref _d, ref _d, ref _d, ref _d, ref _d);
        }

        #endregion
    }
}
