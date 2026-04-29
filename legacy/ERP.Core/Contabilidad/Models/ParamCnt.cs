using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using ERP.Core.Compartido.Utilidades;

namespace ERP.Core.Contabilidad.Models
{
    /// <summary>
    /// Clase para parametros de contabilidad
    /// </summary>
    public class ParamCnt
    {
        #region Campos privados

        private OdbcCommand _mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect _odbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect _varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private string _stmysql;
        private bool _ok;
        private Ayuda _msgSas = new Ayuda("admin");

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

        public enum TipoTercero
        {
            Referente = 1,
            Proveedor = 2,
            ClienteProveedor = 3,
            Tercero = 4,
            Asociado = 5,
            AFiliado = 6,
            Empleado = 7
        }

        public enum OpcParametroMediosDian
        {
            PUC = 1,
            TipoMovto = 2,
            Lineas = 3
        }

        #endregion

        #region Constructor y Destructor

        public ParamCnt()
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

        ~ParamCnt()
        {
        }

        #endregion

        #region Metodos de ayuda

        public string HelpcuentaContables(OdbcConnection Myconnect, Form Myforma, string Nivel = "")
        {
            string StNivel = null;

            if (!string.IsNullOrEmpty(Nivel.Trim()))
            {
                StNivel = " nivel='" + Nivel.Trim() + "' ";
            }
            string respCampo = string.Empty;
            string Cuenta = _msgSas.CargaAyuda("cnt_maecuen", "cuenta", "nombre", "", Myconnect, Myforma, "", "",null,null, ref respCampo, StNivel);
            return Cuenta;
        }

        public string HelpLineasImpuestos(OdbcConnection Myconnect, Form Myforma, string Tabla)
        {
            string respCampo = string.Empty;
            string Linea = _msgSas.CargaAyuda(Tabla, "linea", "nombre", "afecta", Myconnect, Myforma, "", "Linea Afecta",null,null, ref respCampo);
            return Linea;
        }

        public string HelpNits(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            string nit = _msgSas.CargaAyuda("cnt_nit", "NIT", "RAZON_SOCIAL", "NOMBRE", Myconnect, Myforma, "Razon social", "Nombre",null,null, ref respCampo);
            return nit;
        }

        public string HelpCencos(OdbcConnection Myconnect, Form Myforma)
        {
            string respCampo = string.Empty;
            string Cencos = _msgSas.CargaAyuda("cnt_cencos", "ccosto", "nombre", " ", Myconnect, Myforma, "", "", null, null, ref respCampo);
            return Cencos;
        }

        #endregion

        #region Metodos de Terceros

        public bool BuscarTercero(ref string nit, OdbcConnection myconect,
            ref string nombre,
            ref string RazonSocial, ref string TipoNit, ref string DigiCheq, ref string Expedida,
            ref string Direccion, ref string Telefono, ref string Ciudad, ref string OrigenDatos,
            ref bool PagaGrabamen, ref string TipoPersona, ref int Estado, ref decimal TasaIca,
            string ciiu = "", string contactos = "")
        {
            return BuscarTercero(ref nit, myconect, ref nombre, Navega.Ninguno, TipoTercero.Tercero,
                ref RazonSocial, ref TipoNit, ref DigiCheq, ref Expedida,
                ref Direccion, ref Telefono, ref Ciudad, ref OrigenDatos,
                ref PagaGrabamen, ref TipoPersona, ref Estado, ref TasaIca, ciiu, contactos);
        }

        public bool BuscarTercero(ref string nit, OdbcConnection myconect,
            ref string nombre, Navega Navegar, TipoTercero Tipo,
            ref string RazonSocial, ref string TipoNit, ref string DigiCheq, ref string Expedida,
            ref string Direccion, ref string Telefono, ref string Ciudad, ref string OrigenDatos,
            ref bool PagaGrabamen, ref string TipoPersona, ref int Estado, ref decimal TasaIca,
            string ciiu = "", string contactos = "")
        {
            string grabamen = "Y";
            string where = "";

            if (Tipo == TipoTercero.Tercero)
            {
                if (Navegar == Navega.Ninguno)
                {
                    where = " from cnt_nit where nit='" + nit + "'";
                }
                else if (Navegar == Navega.Primero)
                {
                    where = " from cnt_nit where nit > ' ' order by nit " + _varini.Pstlimit;
                }
                else if (Navegar == Navega.Anterior)
                {
                    where = " from  cnt_nit where nit < '" + nit + "' order by nit desc " + _varini.Pstlimit;
                }
                else if (Navegar == Navega.Siguiente)
                {
                    where = " from  cnt_nit where nit > '" + nit + "' order by nit " + _varini.Pstlimit;
                }
                else if (Navegar == Navega.Ultimo)
                {
                    where = " from  cnt_nit where nit <= '99999999999999' order by nit desc " + _varini.Pstlimit;
                }
            }
            else
            {
                where = " from cnt_nit where nit='" + nit + "' and asesor = '1'";
            }

            bool ok;
            string sEstado = Estado.ToString();
            string sTasaIca = TasaIca.ToString();

            _stmysql = "select" + _varini.Psttop + " nit as campo1,nombre as campo2,razon_Social as campo3,tipo_nit as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconect, "BuscarTercero", ref nit, ref nombre, ref RazonSocial, ref TipoNit);

            int tipoNitIndex = Strings.InStr("CNETUR", TipoNit, CompareMethod.Text) - 1;
            TipoNit = (tipoNitIndex < 0 ? 0 : tipoNitIndex).ToString();

            _stmysql = "select" + _varini.Psttop + " nit_chequeo as campo1,expedida as campo2,direccion as campo3,telefono1 as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconect, "BuscarTercero", ref DigiCheq, ref Expedida, ref Direccion, ref Telefono);

            _stmysql = "select" + _varini.Psttop + " ciudad as campo1,origen_datos as campo2,grabamen as campo3,tipo_persona as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconect, "BuscarTercero", ref Ciudad, ref OrigenDatos, ref grabamen, ref TipoPersona);

            _stmysql = "select" + _varini.Psttop + " estado as campo1, tasa_ica as campo2, cod_ciiu as campo3, contactos as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconect, "BuscarTercero", ref sEstado, ref sTasaIca, ref ciiu, ref contactos);

            int.TryParse(sEstado, out Estado);
            decimal.TryParse(sTasaIca, out TasaIca);

            PagaGrabamen = grabamen == "Y";

            int tipoPersonaIndex = Strings.InStr("NJ", TipoPersona, CompareMethod.Text) - 1;
            TipoPersona = (tipoPersonaIndex < 0 ? 0 : tipoPersonaIndex).ToString();

            return ok;
        }

        public bool BuscarTercero(ref string nit, OdbcConnection myconect)
        {
            string nombre = " ", RazonSocial = "", TipoNit = "", DigiCheq = "", Expedida = "";
            string Direccion = "", Telefono = "", Ciudad = "", OrigenDatos = "CNT", TipoPersona = "";
            bool PagaGrabamen = true;
            int Estado = 0;
            decimal TasaIca = 0;

            return BuscarTercero(ref nit, myconect, ref nombre, Navega.Ninguno, TipoTercero.Tercero,
                ref RazonSocial, ref TipoNit, ref DigiCheq, ref Expedida, ref Direccion, ref Telefono,
                ref Ciudad, ref OrigenDatos, ref PagaGrabamen, ref TipoPersona, ref Estado, ref TasaIca);
        }

        public bool GrabaTercero(ref string nit, Acciones Accion, OdbcConnection myconect,
            ref string nombre,
            ref string RazonSocial, ref string TipoNit, ref string DigiCheq, ref string Expedida,
            ref string Direccion, ref string Telefono, ref string Ciudad, ref string OrigenDatos,
            ref string Email, ref string fax, ref string CodigoBanco, ref string NumCuenBanco,
            ref string TipoCuenta, ref string TasaIca, ref string DiasPago, ref string PrecioEsp,
            ref string IdCiudad, string ciiu = "9999999999", string contactos = "")
        {
            return GrabaTercero(ref nit, Accion, myconect, ref nombre, TipoTercero.Tercero,
                ref RazonSocial, ref TipoNit, ref DigiCheq, ref Expedida,
                ref Direccion, ref Telefono, ref Ciudad, ref OrigenDatos,
                ref Email, ref fax, ref CodigoBanco, ref NumCuenBanco,
                ref TipoCuenta, ref TasaIca, ref DiasPago, ref PrecioEsp,
                ref IdCiudad, ciiu, contactos);
        }

        public bool GrabaTercero(ref string nit, Acciones Accion, OdbcConnection myconect,
            ref string nombre, TipoTercero Tipo,
            ref string RazonSocial, ref string TipoNit, ref string DigiCheq, ref string Expedida,
            ref string Direccion, ref string Telefono, ref string Ciudad, ref string OrigenDatos,
            ref string Email, ref string fax, ref string CodigoBanco, ref string NumCuenBanco,
            ref string TipoCuenta, ref string TasaIca, ref string DiasPago, ref string PrecioEsp,
            ref string IdCiudad, string ciiu = "9999999999", string contactos = "")
        {
            string mysql = "";
            mysql = "Select * from cnt_nit where nit = '" + nit + "'";

            if (Accion == Acciones.Borrar)
            {
                mysql = "Delete from cnt_nit where nit = '" + nit + "'";
            }
            else
            {
                if (_odbcConnect.ExecuteQueryconec(mysql, myconect, "GrabaTercero(Busqueda)"))
                {
                    mysql = " update cnt_nit set nit_chequeo ='" + DigiCheq +
                        "', tipo_nit ='" + TipoNit + "', razon_social ='" + RazonSocial +
                        "', nombre ='" + nombre +
                        "', direccion ='" + Direccion + "', telefono1 ='" + Telefono +
                        "', ciudad ='" + Strings.Mid(Ciudad.Trim(), 1, 40) + "', autorfte ='N' , autorteica ='N' , regimen = 'C',TIPO_ICA = '0" +
                        "', fax ='" + fax +
                        "', email ='" + Email +
                        "', codigo_banco ='" + CodigoBanco +
                        "', numero_cuenta_ban ='" + NumCuenBanco +
                        "', tipo_cuenta_ban ='" + TipoCuenta +
                        "', tipo_tercero = '" + (int)Tipo +
                        "', EXPEDIDA ='" + Expedida +
                        "', tasa_ica ='" + TasaIca +
                        "', Asesor ='" + (Tipo == TipoTercero.Referente ? "1" : " ") +
                        "', PrecioEsp ='" + PrecioEsp +
                        "',IdCiudad='" + IdCiudad +
                        "', origen_datos = '" + OrigenDatos + "', dias_pago = '" + DiasPago + "', cod_ciiu = '" + ciiu + "', contactos = '" + contactos + "'  where nit = '" + nit.Trim() + "'";
                }
                else
                {
                    mysql = "insert into cnt_nit ( NIT,NIT_CHEQUEO,EXPEDIDA,TIPO_NIT,RAZON_SOCIAL,NOMBRE,";
                    mysql += "DIRECCION,TELEFONO1,CIUDAD,AUTORFTE,AUTORTEICA,REGIMEN,TIPO_ICA,";
                    mysql += "TASA_ICA,ORIGEN_DATOS,DIAS_PAGO,fax,email,codigo_banco,numero_cuenta_ban,tipo_cuenta_ban,tipo_tercero,Asesor,PrecioEsp,IdCiudad,cod_ciiu,contactos)";
                    mysql += " values('" +
                        nit + "','" + DigiCheq +
                        "','" + Expedida + "','" + TipoNit + "','" +
                        RazonSocial + "','" +
                        nombre + "','" +
                        Direccion.Trim() + "','" + Telefono.Trim() + "','" +
                        Strings.Mid(Ciudad.Trim(), 1, 40) + "','N','N','C','0','" + TasaIca + "','" + OrigenDatos + "','" + DiasPago + "','" +
                        fax + "','" +
                        Email + "','" +
                        CodigoBanco + "','" +
                        NumCuenBanco + "','" +
                        TipoCuenta + "','" + (int)Tipo + "','" + (Tipo == TipoTercero.Referente ? "1" : " ") + "','" + PrecioEsp + "','" + IdCiudad + "','" + ciiu + "','" + contactos + "')";
                }
            }
            return _odbcConnect.ExecuteQueryconec(mysql, myconect, "GrabaTercero");
        }

        #endregion

        #region Metodos de Formatos DIAN

        public void GrabaTablaFormatosDian(int Idformato, string idCpto, string NombreFormato, string Nombreconcepto,
            double Tope, string Nitmenos, double TopeSaldo, string nitdian, OdbcConnection myconnect)
        {
            if (!Information.IsNumeric(idCpto))
            {
                idCpto = "0";
            }

            int idCptoInt = int.Parse(idCpto);
            _ok = BuscaTablaFormatosdian(ref Idformato, ref idCptoInt, myconnect);

            if (!_ok)
            {
                _stmysql = "insert into cnt_parfordian(Idformato,Idcpto,NomFormato,NomCpto,Tope,NitMenos,TopeSaldo,nitdian) values ('" +
                    Idformato + "','" + idCpto + "','" + NombreFormato + "','" + Nombreconcepto + "','" + Tope + "','" + Nitmenos + "','" + TopeSaldo + "','" + nitdian + "')";
                _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabaTablaFormatosDian");
            }
            else
            {
                _stmysql = "update cnt_parfordian set NomFormato = '" + NombreFormato + "',NomCpto ='" + Nombreconcepto + "',Tope ='" + Tope + "',NitMenos= '" + Nitmenos + "'," +
                    "TopeSaldo=" + TopeSaldo + ",nitdian='" + nitdian + "' where Idformato = '" + Idformato + "' and Idcpto = '" + idCpto + "'";
                _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabaTablaFormatosDian");
            }
        }

        public bool BuscaTablaFormatosdian(ref int Idformato, ref int idCpto, OdbcConnection myconnect,
            ref string NomFormato, ref string NomCpto, ref double Tope, ref string NitMenos,
            ref double TopeSaldo, ref string NitDian)
        {
            return BuscaTablaFormatosdian(ref Idformato, ref idCpto, myconnect,
                ref NomFormato, ref NomCpto, ref Tope, ref NitMenos,
                Navega.Ninguno, ref TopeSaldo, ref NitDian);
        }

        public bool BuscaTablaFormatosdian(ref int Idformato, ref int idCpto, OdbcConnection myconnect,
            ref string NomFormato, ref string NomCpto, ref double Tope, ref string NitMenos,
            Navega Navegar, ref double TopeSaldo, ref string NitDian)
        {
            string where = null;

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = "from cnt_parfordian where Idformato = " + Idformato + " and Idcpto = '" + idCpto + "'";
                    break;
                case Navega.Primero:
                    where = "from cnt_parfordian where Idformato > 0 and Idcpto > 0 order by Idformato,Idcpto" + _varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = "from cnt_parfordian where Idformato > " + Idformato + " and Idcpto > '" + idCpto + "' order by Idformato,Idcpto" + _varini.Pstlimit;
                    break;
                case Navega.Anterior:
                    where = "from cnt_parfordian where Idformato <" + Idformato + " order by Idformato,Idcpto desc" + _varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = "from cnt_parfordian where Idformato <= 999999 order by Idformato,Idcpto desc" + _varini.Pstlimit;
                    break;
            }

            string sTope = Tope.ToString();
            string sTopeSaldo = TopeSaldo.ToString();
            string sIdformato = Idformato.ToString();
            string sIdCpto = idCpto.ToString();

            _stmysql = "select NomFormato as campo1,NomCpto as campo2,Tope as campo3,NitMenos as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscaTablaFormatosdian", ref NomFormato, ref NomCpto, ref sTope, ref NitMenos);

            _stmysql = "select Idformato as campo1,idCpto as campo2,TopeSaldo as campo3,nitdian as campo4 ";
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscaTablaFormatosdian", ref sIdformato, ref sIdCpto, ref sTopeSaldo, ref NitDian);

            double.TryParse(sTope, out Tope);
            double.TryParse(sTopeSaldo, out TopeSaldo);
            int.TryParse(sIdformato, out Idformato);
            int.TryParse(sIdCpto, out idCpto);

            return _ok;
        }

        public bool BuscaTablaFormatosdian(ref int Idformato, ref int idCpto, OdbcConnection myconnect)
        {
            string NomFormato = "", NomCpto = "", NitMenos = "", NitDian = "";
            double Tope = 0, TopeSaldo = 0;
            return BuscaTablaFormatosdian(ref Idformato, ref idCpto, myconnect, ref NomFormato, ref NomCpto, ref Tope, ref NitMenos, Navega.Ninguno, ref TopeSaldo, ref NitDian);
        }

        public void ElimanaTablaFormatosdian(int Idformato, int idCpto, OdbcConnection myconnect)
        {
            _stmysql = "delete from cnt_parfordian where Idformato = '" + Idformato + "' and Idcpto = '" + idCpto + "'";
            _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "ElimanaTablaFosmatodian");
        }

        public DataSet BuscaTablaFormatosdian(OdbcConnection myconnect)
        {
            StringBuilder stBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            stBuilder.Append("select Idformato, Idcpto, NomFormato, NomCpto, Tope, NitMenos, topesaldo, nitdian ");
            stBuilder.Append("from cnt_parfordian ");
            stBuilder.Append("order by Idformato, Idcpto");

            _odbcConnect.ExecuteQueryDataset(stBuilder.ToString(), myconnect, "BuscaTablaFormatosdian", ref dsdata, "tblfordian");

            return dsdata;
        }

        #endregion

        #region Metodos de Digito de Verificacion

        /// <summary>
        /// Calcula el digito de verificacion del NIT o cedula
        /// </summary>
        public int CalcularDigito_Verificacion_Nit(string cedula)
        {
            int[] numerosprimo = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };
            int digitoVerificacion = 0;

            for (int indice = 0; indice < cedula.Trim().Length; indice++)
            {
                digitoVerificacion += Convert.ToInt32(cedula.Substring(cedula.Trim().Length - (indice + 1), 1)) * numerosprimo[indice];
            }

            int resultdigitoVerificacion = digitoVerificacion % 11;
            if (resultdigitoVerificacion == 0 || resultdigitoVerificacion == 1)
            {
                return resultdigitoVerificacion;
            }
            else
            {
                return 11 - resultdigitoVerificacion;
            }
        }

        #endregion

        #region Metodos de Tablas de Impuestos

        public bool NavegarTablasImpuestos(ref string Linea, OdbcConnection myconnect,
            ref string NomLinea, ref string LinAfecta, ref string NomLineaAfecta, ref string Tabla,
            ref string signo)
        {
            return NavegarTablasImpuestos(ref Linea, myconnect,
                ref NomLinea, ref LinAfecta, ref NomLineaAfecta, ref Tabla,
                Navega.Ninguno, false, ref signo);
        }

        public bool NavegarTablasImpuestos(ref string Linea, OdbcConnection myconnect,
            ref string NomLinea, ref string LinAfecta, ref string NomLineaAfecta, ref string Tabla,
            Navega Navegar, bool Existe, ref string signo)
        {
            string LinViene = Linea;
            string where = "";

            switch (Navegar)
            {
                case Navega.Primero:
                    where = " where  LINEA >0 order by LINEA";
                    break;
                case Navega.Siguiente:
                    where = " where  LINEA >" + Linea + " order by LINEA";
                    break;
                case Navega.Anterior:
                    where = " where  LINEA <" + Linea + " order by LINEA desc";
                    break;
                case Navega.Ultimo:
                    where = " where  LINEA <= '99999' order by LINEA desc";
                    break;
                case Navega.Ninguno:
                    where = " where  LINEA = " + Linea + "";
                    break;
            }

            _stmysql = "select LINEA as campo1,NOMBRE as campo2,AFECTA as campo3,signo as campo4  from " + Tabla + where;
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "NavegarTablasImpuestos", ref Linea, ref NomLinea, ref LinAfecta, ref signo);

            if (_ok)
            {
                _stmysql = "select NOMBRE as campo1 from " + Tabla + " where linea = '" + LinAfecta + "'";
                string dummy1 = "", dummy2 = "", dummy3 = "";
                if (!_odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "NavegarTablasImpuestos", ref NomLineaAfecta, ref dummy1, ref dummy2, ref dummy3))
                {
                    NomLineaAfecta = "LINEA NO EXISTE";
                }
            }
            else
            {
                if (Existe && LinViene == Linea)
                {
                    _ok = true;
                }
            }

            Linea = Linea.Trim();
            NomLinea = NomLinea.Trim();
            LinAfecta = LinAfecta.Trim();
            NomLineaAfecta = NomLineaAfecta.Trim();

            return _ok;
        }

        #endregion

        #region Metodos de Parametros Medios DIAN

        public bool GrabarParametroMediosDian(int idformato, int idcpto, int OpcParametro, string ValorParametro,
            int BaseFuente, int UbicacionFuente, int formula, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            DataSet ds = null;
            _ok = BuscarParametroMediosDian(idformato, idcpto, OpcParametro, ValorParametro, myconnect, ref ds);

            if (!_ok)
            {
                stbuilder.Append("insert into cnt_parinfmedian (idformato, idcpto, opcionparam, valorparam, opcionvalor, basecalculo, formula) values ('");
                stbuilder.Append(idformato + "','");
                stbuilder.Append(idcpto + "','");
                stbuilder.Append(OpcParametro + "','");
                stbuilder.Append(ValorParametro + "','");
                stbuilder.Append(UbicacionFuente + "','");
                stbuilder.Append(BaseFuente + "','");
                stbuilder.Append(formula + "')");
            }
            else
            {
                stbuilder.Append("update cnt_parinfmedian set ");
                stbuilder.Append("opcionvalor='" + UbicacionFuente + "',");
                stbuilder.Append("basecalculo='" + BaseFuente + "', ");
                stbuilder.Append("formula='" + formula + "' ");
                stbuilder.Append("where idformato='" + idformato + "' and idcpto='" + idcpto + "' and opcionparam='" + OpcParametro + "' and valorparam='" + ValorParametro + "'");
            }

            _ok = _odbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabarParametroMediosDian");
            return _ok;
        }

        public bool BuscarParametroMediosDian(int idformato, int idcpto, int OpcParametro, string ValorParametro,
            OdbcConnection myconnect, ref DataSet Dsdataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            try
            {
                if (Dsdataset != null && Dsdataset.Tables.Contains("tblparinfmedian"))
                {
                    Dsdataset.Tables.Remove("tblparinfmedian");
                }
            }
            catch { }

            stbuilder.Append("select idformato, idcpto, opcionparam, valorparam, opcionvalor, basecalculo,formula ");
            stbuilder.Append("from cnt_parinfmedian ");
            stbuilder.Append("where idformato='" + idformato + "' and idcpto='" + idcpto + "' and opcionparam='" + OpcParametro + "' and valorparam='" + ValorParametro + "'");

            _ok = _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarParametroMediosDian", ref dsdata, "tblparinfmedian");
            {
                try
                {
                    if (Dsdataset == null)
                    {
                        Dsdataset = new DataSet();
                    }
                    Dsdataset.Tables.Add(dsdata.Tables["tblparinfmedian"].Copy());
                }
                catch { }
                return true;
            }
            return false;
        }

        public DataSet BuscarParametroMediosDian(int idformato, int idcpto, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            stbuilder.Append("select parinf.idformato, parinf.idcpto, parinf.opcionparam, parinf.valorparam, parinf.opcionvalor,parinf.basecalculo,parval.descripcion as descopcionvalor,parinf.formula, ");
            stbuilder.Append("case parinf.opcionparam when 1 then 'PUC' when 2 then 'Tipo Movto' when 3 then 'Lineas' else '' end as descopcionparam,");
            stbuilder.Append("case parinf.opcionparam when 1 then maecuen.NOMBRE when 2 then codmov.NOMBRE when 3 then car12.DESCRIPCION else '' end as descripcion,");
            stbuilder.Append("case parinf.basecalculo when 1 then 'Sumatoria debito del ano' when 2 then 'Sumatoria credito del ano' when 3 then 'Sumatoria neta del ano' when 4 then 'Saldo al final del ano' when 5 then 'Inf. Ahorros (MOV,SF)' when 6 then 'Inf. Cdats (SI,REND,INV,SF)' else '' end as desbasecal, ");
            stbuilder.Append("case parinf.formula when 0 then 'No aplica' when 1 then 'Pago salud empleado' when 2 then 'Pago pension empleado' else '' end as Descformula ");
            stbuilder.Append("from cnt_parinfmedian parinf ");
            stbuilder.Append("left join cnt_maecuen maecuen on parinf.valorparam = maecuen.CUENTA ");
            stbuilder.Append("left join cop_codmov codmov on parinf.valorparam = codmov.cod_movto ");
            stbuilder.Append("left join cop_concar12 car12 on parinf.valorparam = rtrim(car12.LINCRED) ");
            stbuilder.Append("left join cnt_parvalmedian parval on parinf.idformato = parval.idformato and parinf.opcionvalor=parval.idvalor ");
            stbuilder.Append("where parinf.idformato='" + idformato + "' and parinf.idcpto='" + idcpto + "'");

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarParametroMediosDian", ref dsdata, "tblparinfmedian");

            return dsdata;
        }

        public virtual DataSet BuscarParametroMediosDian(OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            stbuilder.Append("select parinf.idformato, parinf.idcpto, parinf.opcionparam, ");
            stbuilder.Append("case parinf.opcionparam when 1 then 'PUC' when 2 then 'Tipo Movto' when 3 then 'Lineas' else '' end as descopcionparam,");
            stbuilder.Append("parinf.valorparam,case parinf.opcionparam when 1 then maecuen.NOMBRE when 2 then codmov.NOMBRE when 3 then car12.DESCRIPCION else '' end as descripcion, ");
            stbuilder.Append("parinf.opcionvalor,parval.descripcion as descopcionvalor,parinf.basecalculo,");
            stbuilder.Append("case parinf.basecalculo when 1 then 'Sumatoria debito del ano' when 2 then 'Sumatoria credito del ano' when 3 then 'Sumatoria neta del ano' when 4 then 'Saldo al final del ano' when 5 then 'Inf. Ahorros (MOV,SF)' when 6 then 'Inf. Cdats (SI,REND,INV,SF)' else '' end as desbasecal, ");
            stbuilder.Append("parinf.formula,");
            stbuilder.Append("case parinf.formula when 0 then 'No aplica' when 1 then 'Pago salud empleado' when 2 then 'Pago pension empleado' else '' end as Descformula ");
            stbuilder.Append("from cnt_parinfmedian parinf ");
            stbuilder.Append("left join cnt_maecuen maecuen on parinf.valorparam = maecuen.CUENTA ");
            stbuilder.Append("left join cop_codmov codmov on parinf.valorparam = codmov.cod_movto ");
            stbuilder.Append("left join cop_concar12 car12 on parinf.valorparam = rtrim(car12.LINCRED) ");
            stbuilder.Append("left join cnt_parvalmedian parval on parinf.idformato = parval.idformato and parinf.opcionvalor=parval.idvalor ");
            stbuilder.Append("order by parinf.idformato,parinf.idcpto");

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarParametroMediosDian", ref dsdata, "tblparinfmedian");

            return dsdata;
        }

        public bool EliminaParametroMediosDian(int idformato, int idcpto, int OpcParametro, string ValorParametro, OdbcConnection myconnect)
        {
            string stmysql = "delete from cnt_parinfmedian where idformato='" + idformato + "' and idcpto='" + idcpto + "' and opcionparam='" + OpcParametro + "' and valorparam='" + ValorParametro + "'";
            _ok = _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarParametroMediosDian");
            return _ok;
        }

        public bool ValidarGrupocuenta(int idformato, int idcpto, string Cuenta, string NivelCuenta, OdbcConnection myconnect)
        {
            string CuentaReal;

            switch (NivelCuenta.Trim())
            {
                case "1":
                    CuentaReal = Strings.Mid(Cuenta.Trim(), 1, 1) + "%";
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    break;
                case "2":
                    CuentaReal = Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 1) + "000000000000", 12);
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    CuentaReal = Strings.Mid(Cuenta.Trim(), 1, 2) + "%";
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    break;
                case "3":
                    CuentaReal = Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 2) + "000000000000", 12);
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    CuentaReal = Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 4) + "0000", 4) + "%";
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    break;
                case "4":
                    CuentaReal = Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 2) + "000000000000", 12);
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    CuentaReal = Strings.Left(Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 4) + "0000", 4) + "000000000000", 12);
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    CuentaReal = Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 6) + "000000", 6) + "%";
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    break;
                case "5":
                case "6":
                    CuentaReal = Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 2) + "000000000000", 12);
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    CuentaReal = Strings.Left(Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 4) + "0000", 4) + "000000000000", 12);
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    CuentaReal = Strings.Left(Strings.Left(Strings.Mid(Cuenta.Trim(), 1, 6) + "000000", 6) + "000000000000", 12);
                    if (BuscarCuentaMediosDianParametrizada(idformato, idcpto, Cuenta, CuentaReal, myconnect))
                        return true;
                    break;
            }
            return false;
        }

        public bool BuscarCuentaMediosDianParametrizada(int idformato, int idcpto, string Cuenta, string CuentaBuscar, OdbcConnection myconnect)
        {
            string CuentaNo = Strings.Left(Cuenta.Trim() + "000000000000", 12);
            string SqlDml = "select valorparam from cnt_parinfmedian where idformato='" + idformato + "' and idcpto='" + idcpto + "' and valorparam like '" + CuentaBuscar + "' and valorparam<>'" + CuentaNo + "' and opcionparam='1'";
            _ok = _odbcConnect.ExecuteQueryconec(SqlDml, myconnect, "BuscarCuentaMediosDianParametrizada");
            return _ok;
        }

        public DataSet BuscarParamValoresFormato(int idformato, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select idformato,idvalor,descripcion ");
            stbuilder.Append("from cnt_parvalmedian ");
            stbuilder.Append("where idformato=" + idformato);
            stbuilder.Append(" order by idvalor");

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarParamValoresFormato", ref dsdata, "tblparvalfor");

            return dsdata;
        }

        #endregion
    }
}
