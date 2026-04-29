// Traducción de: Clsmsgcli.vb (msgcli)
using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Services
{
    // ─── clsmsgbase ──────────────────────────────────────────────────────────────
    public class clsmsgbase
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private OdbcDataAdapter MyDataAdater = new OdbcDataAdapter();
        private OdbcConnection stConnect = new OdbcConnection();
        private string stForfect, stCodEmpresa;

        public OdbcConnection Myconnect
        {
            get { return stConnect; }
            set { stConnect = value; }
        }
        public string PstForfect
        {
            get { return stForfect; }
            set { stForfect = value; }
        }
        public string PstCodEmpresa
        {
            get { return stCodEmpresa; }
            set { stCodEmpresa = value; }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento)
        {
            string c1 = "", c2 = "", c3 = "", c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref c1, ref c2, ref c3, ref c4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
                mycomqueryconec.Connection = appadoConect;
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0) result = true;
                while (Myread.Read())
                {
                    if (Campo1 != "")
                        Campo1 = Myread["campo1"] == DBNull.Value ? "0" : Myread["campo1"].ToString();
                    if (Campo2 != "")
                        Campo2 = Myread["campo2"] == DBNull.Value ? "0" : Myread["campo2"].ToString();
                    if (Campo3 != "")
                        Campo3 = Myread["campo3"] == DBNull.Value ? "0" : Myread["campo3"].ToString();
                    if (Campo4 != "")
                        Campo4 = Myread["campo4"] == DBNull.Value ? "0" : Myread["campo4"].ToString();
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql,
                    "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        public DataSet ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref DataSet DsDataSet, string Nomtabla)
        {
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandTimeout = 1000;
                mycomqueryconec.Connection = appadoConect;
                MyDataAdater.SelectCommand = mycomqueryconec;
                MyDataAdater.Fill(DsDataSet, Nomtabla);
                return DsDataSet;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql,
                    "SORTEC", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
        }
    }

    // ─── Clsmsgcli ───────────────────────────────────────────────────────────────
    public class Clsmsgcli
    {
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private OdbcDataAdapter MyDataAdater = new OdbcDataAdapter();
        private OdbcConnection stConnect;

        private string StNombres, StApellidos, StCedula, Stexpedida, StNit, StRazonsocial;
        private string StDireccion, StTelefono1, StTelefono2, StMovil, Stemail;
        // VB: int backing but property is String (VB implicit int→string coercion)
        private int StCiudad, StTipo, Stsexo;
        private int StDv, StCausaret, StClaContrato, StTipofact, StEstImpl, StAsesorImpl;
        private DateTime StFecIng, StFecReIng, StFechaRetiro, StFecVenCont, StFechaFirmo;
        private DateTime StFecIniImpl, StFecFinImpl;
        private int StplazoImpl;
        private double StValMen, StHosting;
        // VB: int backing but property is Double
        private int StVendedor, StestCont;
        // VB: Decimal backing but property is Double
        private decimal StTasaIca = 0;
        private string stForfect, Stusuario;
        private int Stsistema = 0;
        private bool ok;

        public OdbcConnection Myconnect { get { return stConnect; } set { stConnect = value; } }
        public string PstForfect { get { return stForfect; } set { stForfect = value; } }
        public string PstUsuario { get { return Stusuario; } set { Stusuario = value; } }
        public string Nombre { get { return StNombres; } set { StNombres = value; } }
        public string Apellidos { get { return StApellidos; } set { StApellidos = value; } }
        public string Cedula { get { return StCedula; } set { StCedula = value; } }
        public string Expedida { get { return Stexpedida; } set { Stexpedida = value; } }
        public string Nit { get { return StNit; } set { StNit = value; } }
        public string Razonsocial { get { return StRazonsocial; } set { StRazonsocial = value; } }
        public string Direccion { get { return StDireccion; } set { StDireccion = value; } }
        public string Telefono1 { get { return StTelefono1; } set { StTelefono1 = value; } }
        public string Telefono2 { get { return StTelefono2; } set { StTelefono2 = value; } }
        public string Movil { get { return StMovil; } set { StMovil = value; } }
        public string Email { get { return Stemail; } set { Stemail = value; } }

        // int backing, property String (VB implicit coercion)
        public string Ciudad
        {
            get { return StCiudad.ToString(); }
            set { int.TryParse(value, out StCiudad); }
        }
        public string TipoDocumento
        {
            get { return StTipo.ToString(); }
            set { int.TryParse(value, out StTipo); }
        }
        public string Sexo
        {
            get { return Stsexo.ToString(); }
            set { int.TryParse(value, out Stsexo); }
        }

        public int Dv { get { return StDv; } set { StDv = value; } }
        public int CausaRetiro { get { return StCausaret; } set { StCausaret = value; } }
        public int ClaseContrato { get { return StClaContrato; } set { StClaContrato = value; } }
        public int Tipofact { get { return StTipofact; } set { StTipofact = value; } }
        public int EstImpl { get { return StEstImpl; } set { StEstImpl = value; } }
        public int AsesorImpl { get { return StAsesorImpl; } set { StAsesorImpl = value; } }
        public DateTime FecIng { get { return StFecIng; } set { StFecIng = value; } }
        public DateTime FecReIng { get { return StFecReIng; } set { StFecReIng = value; } }
        public DateTime FechaRetiro { get { return StFechaRetiro; } set { StFechaRetiro = value; } }
        public DateTime FecVenCont { get { return StFecVenCont; } set { StFecVenCont = value; } }
        public DateTime FechaFirmo { get { return StFechaFirmo; } set { StFechaFirmo = value; } }
        public DateTime FecIniImpl { get { return StFecIniImpl; } set { StFecIniImpl = value; } }
        public DateTime FecFinImpl { get { return StFecFinImpl; } set { StFecFinImpl = value; } }
        public int plazoImpl { get { return StplazoImpl; } set { StplazoImpl = value; } }
        public double ValMen { get { return StValMen; } set { StValMen = value; } }
        // Decimal backing, property Double
        public double TasaIca { get { return (double)StTasaIca; } set { StTasaIca = (decimal)value; } }
        // int backing, property Double
        public double Vendedor { get { return StVendedor; } set { StVendedor = (int)value; } }
        public double EstCont { get { return StestCont; } set { StestCont = (int)value; } }
        public int sistema { get { return Stsistema; } set { Stsistema = value; } }
        public double ValMenHosting { get { return StHosting; } set { StHosting = value; } }

        private bool BuscaCliente(int codigo)
        {
            var stbuilder = new StringBuilder();
            var DsDataset = new DataSet();
            stbuilder.Append("select Codigo,Tipo,Cedula,Expedida,sexo,Nombres,Apellidos,Nit,Dv,Razonsocial,Direccion,Telefono1,");
            stbuilder.Append("telefono2,Movil,email,Ciudad,FecIng,FecReIng,FechaRetiro,Causaret,ClaContrato,FecVenCont,ValMen,Tipofact,");
            stbuilder.Append(" FechaFirmo,Vendedor,estCont,FecIniImpl,plazoImpl,FecFinImpl,EstImpl,AsesorImpl,sistema,ValMenHosting ");
            stbuilder.Append("from cli_maecli where codigo = '" + codigo + "'");
            connect.ExecuteQueryDataset(stbuilder.ToString(), stConnect, "BuscaCliente", ref DsDataset, "tblcliente");
            return DsDataset.Tables["tblcliente"].Rows.Count > 0;
        }

        public bool BuscaCamposCliente(int codigo)
        {
            var stbuilder = new StringBuilder();
            var DsDataset = new DataSet();
            stbuilder.Append("select Codigo,Tipo,Cedula,Expedida,sexo,Nombres,Apellidos,Nit,Dv,Razonsocial,Direccion,Telefono1,");
            stbuilder.Append("telefono2,Movil,email,Ciudad,FecIng,FecReIng,FechaRetiro,Causaret,ClaContrato,FecVenCont,ValMen,Tipofact,");
            stbuilder.Append(" FechaFirmo,Vendedor,estCont,FecIniImpl,plazoImpl,FecFinImpl,EstImpl,AsesorImpl,sistema,tasaica,ValMenHosting ");
            stbuilder.Append("from cli_maecli where codigo = '" + codigo + "'");
            connect.ExecuteQueryDataset(stbuilder.ToString(), stConnect, "BuscaCliente", ref DsDataset, "tblcliente");

            if (DsDataset.Tables["tblcliente"].Rows.Count > 0)
            {
                var r = DsDataset.Tables["tblcliente"].Rows[0];
                StCedula      = r["Cedula"].ToString();
                StTipo        = Convert.ToInt32(r["Tipo"]);
                StApellidos   = r["Apellidos"].ToString();
                StAsesorImpl  = Convert.ToInt32(r["AsesorImpl"]);
                StCausaret    = Convert.ToInt32(r["Causaret"]);
                StCiudad      = Convert.ToInt32(r["Ciudad"]);
                StClaContrato = Convert.ToInt32(r["ClaContrato"]);
                StDireccion   = r["Direccion"].ToString();
                StDv          = Convert.ToInt32(r["Dv"]);
                Stemail       = r["email"].ToString();
                StestCont     = Convert.ToInt32(r["estCont"]);
                StEstImpl     = Convert.ToInt32(r["EstImpl"]);
                Stexpedida    = r["expedida"].ToString();
                StFecFinImpl  = Convert.ToDateTime(r["FecFinImpl"]);
                StFechaFirmo  = Convert.ToDateTime(r["FechaFirmo"]);
                StFechaRetiro = Convert.ToDateTime(r["FechaRetiro"]);
                StFecIng      = Convert.ToDateTime(r["FecIng"]);
                StFecIniImpl  = Convert.ToDateTime(r["FecIniImpl"]);
                StFecReIng    = Convert.ToDateTime(r["FecReIng"]);
                StFecVenCont  = Convert.ToDateTime(r["FecVenCont"]);
                StMovil       = r["Movil"].ToString();
                StNit         = r["Nit"].ToString();
                StNombres     = r["Nombres"].ToString();
                StplazoImpl   = Convert.ToInt32(r["plazoImpl"]);
                StRazonsocial = r["Razonsocial"].ToString();
                Stsexo        = Convert.ToInt32(r["sexo"]);
                StTelefono1   = r["Telefono1"].ToString();
                StTelefono2   = r["Telefono2"].ToString();
                StTipofact    = Convert.ToInt32(r["Tipofact"]);
                StValMen      = Convert.ToDouble(r["ValMen"]);
                StVendedor    = Convert.ToInt32(r["Vendedor"]);
                Stsistema     = Convert.ToInt32(r["sistema"]);
                StTasaIca     = Convert.ToDecimal(r["Tasaica"]);
                StHosting     = Convert.ToDouble(r["ValMenHosting"]);
                return true;
            }
            return false;
        }

        public bool GrabaCliente(int codigo)
        {
            var stbuilder = new StringBuilder();
            ok = BuscaCliente(codigo);
            if (!ok)
            {
                stbuilder.Append("insert into cli_maecli(Codigo,Tipo,Cedula,Expedida,sexo,Nombres,Apellidos,Nit,Dv,Razonsocial,Direccion,Telefono1,");
                stbuilder.Append("telefono2,Movil,email,Ciudad,FecIng,FecReIng,FechaRetiro,Causaret,ClaContrato,FecVenCont,ValMen,Tipofact,");
                stbuilder.Append(" FechaFirmo,Vendedor,estCont,FecIniImpl,plazoImpl,FecFinImpl,EstImpl,AsesorImpl,TasaIca,sistema,ValMenHosting) ");
                stbuilder.Append("values ('" + codigo + "','");
                stbuilder.Append(StTipo + "','");
                stbuilder.Append(StCedula + "','");
                stbuilder.Append(Stexpedida + "','");
                stbuilder.Append(Stsexo + "','");
                stbuilder.Append(StNombres + "','");
                stbuilder.Append(StApellidos + "','");
                stbuilder.Append(StNit + "','");
                stbuilder.Append(StDv + "','");
                stbuilder.Append(StRazonsocial + "','");
                stbuilder.Append(StDireccion + "','");
                stbuilder.Append(StTelefono1 + "','");
                stbuilder.Append(StTelefono2 + "','");
                stbuilder.Append(StMovil + "','");
                stbuilder.Append(Stemail + "','");
                stbuilder.Append(StCiudad + "','");
                stbuilder.Append(StFecIng.ToString(stForfect) + "','");
                stbuilder.Append(StFecReIng.ToString(stForfect) + "','");
                stbuilder.Append(StFechaRetiro.ToString(stForfect) + "','");
                stbuilder.Append(StCausaret + "','");
                stbuilder.Append(StClaContrato + "','");
                stbuilder.Append(StFecVenCont.ToString(stForfect) + "','");
                stbuilder.Append(StValMen + "','");
                stbuilder.Append(StTipofact + "','");
                stbuilder.Append(StFechaFirmo.ToString(stForfect) + "','");
                stbuilder.Append(StVendedor + "','");
                stbuilder.Append(StestCont + "','");
                stbuilder.Append(StFecIniImpl.ToString(stForfect) + "','");
                stbuilder.Append(StplazoImpl + "','");
                stbuilder.Append(StFecFinImpl.ToString(stForfect) + "','");
                stbuilder.Append(StEstImpl + "','");
                stbuilder.Append(StAsesorImpl + "','");
                stbuilder.Append(TasaIca + "','");
                stbuilder.Append(sistema + "','");
                stbuilder.Append(StHosting + "')");
            }
            else
            {
                stbuilder.Append("update cli_maecli set ");
                stbuilder.Append("Tipo = '" + StTipo + "',");
                stbuilder.Append("Cedula = '" + StCedula + "',");
                stbuilder.Append("Expedida = '" + Stexpedida + "',");
                stbuilder.Append("sexo = '" + Stsexo + "',");
                stbuilder.Append("Nombres = '" + StNombres + "',");
                stbuilder.Append("Apellidos = '" + StApellidos + "',");
                stbuilder.Append("Nit = '" + StNit + "',");
                stbuilder.Append("Dv = '" + StDv + "',");
                stbuilder.Append("Razonsocial = '" + StRazonsocial + "',");
                stbuilder.Append("Direccion = '" + StDireccion + "',");
                stbuilder.Append("Telefono1 = '" + StTelefono1 + "',");
                stbuilder.Append("telefono2 = '" + StTelefono2 + "',");
                stbuilder.Append("Movil = '" + StMovil + "',");
                stbuilder.Append("email = '" + Stemail + "',");
                stbuilder.Append("Ciudad = '" + StCiudad + "',");
                stbuilder.Append("FecIng = '" + StFecIng.ToString(stForfect) + "',");
                stbuilder.Append("FecReIng = '" + StFecReIng.ToString(stForfect) + "',");
                stbuilder.Append("FechaRetiro = '" + StFechaRetiro.ToString(stForfect) + "',");
                stbuilder.Append("ClaContrato = '" + StClaContrato + "',");
                stbuilder.Append("FecVenCont = '" + StFecVenCont.ToString(stForfect) + "',");
                stbuilder.Append("ValMen = '" + StValMen + "',");
                stbuilder.Append("Tipofact = '" + StTipofact + "',");
                stbuilder.Append("FechaFirmo = '" + StFechaFirmo.ToString(stForfect) + "',");
                stbuilder.Append("Vendedor = '" + StVendedor + "',");
                stbuilder.Append("estCont = '" + StestCont + "',");
                stbuilder.Append("FecIniImpl = '" + StFecIniImpl.ToString(stForfect) + "',");
                stbuilder.Append("plazoImpl = '" + StplazoImpl + "',");
                stbuilder.Append("FecFinImpl = '" + StFecFinImpl.ToString(stForfect) + "',");
                stbuilder.Append("EstImpl = '" + StEstImpl + "',");
                stbuilder.Append("AsesorImpl = '" + StAsesorImpl + "', ");
                stbuilder.Append("sistema = '" + sistema + "', ");
                stbuilder.Append("Tasaica = '" + TasaIca + "',");
                stbuilder.Append("ValMenHosting = '" + StHosting + "' ");
                stbuilder.Append("where codigo = '" + codigo + "'");
            }
            string _f1 = "", _f2 = "", _f3 = "", _f4 = "";
            connect.ExecuteQueryconec(stbuilder.ToString(), stConnect, "BuscaCliente", ref _f1, ref _f2, ref _f3, ref _f4);
            return false; // VB: no explicit return — default Boolean = False
        }

        public int HelpClientes(Form Myforma)
        {
            var msgsas = new ERP.Core.Compartido.Utilidades.Ayuda(PstUsuario);
            string result = msgsas.CargaAyuda("cli_maecli", "codigo", "RazonSocial", "Nit",
                Myconnect, Myforma, "Nombre", "Nit/Cedula");
            int ret = 0;
            int.TryParse(result, out ret);
            return ret;
        }

        // ─── Private ExecuteQuery helpers (dead code — all DB calls go via connect) ─
        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
                mycomqueryconec.Connection = appadoConect;
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0) result = true;
                while (Myread.Read())
                {
                    if (Campo1 != "")
                        Campo1 = Myread["campo1"] == DBNull.Value ? "0" : Myread["campo1"].ToString();
                    if (Campo2 != "")
                        Campo2 = Myread["campo2"] == DBNull.Value ? "0" : Myread["campo2"].ToString();
                    if (Campo3 != "")
                        Campo3 = Myread["campo3"] == DBNull.Value ? "0" : Myread["campo3"].ToString();
                    if (Campo4 != "")
                        Campo4 = Myread["campo4"] == DBNull.Value ? "0" : Myread["campo4"].ToString();
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql,
                    "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        private DataSet ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref DataSet DsDataSet, string Nomtabla)
        {
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandTimeout = 1000;
                mycomqueryconec.Connection = appadoConect;
                MyDataAdater.SelectCommand = mycomqueryconec;
                MyDataAdater.Fill(DsDataSet, Nomtabla);
                return DsDataSet;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql,
                    "SORTEC", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
        }
    }

    // ─── Clsmsgconfig ────────────────────────────────────────────────────────────
    public class Clsmsgconfig
    {
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private OdbcDataAdapter MyDataAdater = new OdbcDataAdapter();
        private string Stdetalle, StMensaje, stForfect, Stusuario;
        private DateTime StFecPago, StFechaInicial, StFechaFinal;
        private int StClaseContrato, StEstado, StPeriodo = 0;
        private OdbcConnection stConnect;
        private bool ok;

        public string Detalle { get { return Stdetalle; } set { Stdetalle = value; } }
        public DateTime FecPago { get { return StFecPago; } set { StFecPago = value; } }
        public int ClaseContrato { get { return StClaseContrato; } set { StClaseContrato = value; } }
        public DateTime FechaInicial { get { return StFechaInicial; } set { StFechaInicial = value; } }
        public DateTime FechaFinal { get { return StFechaFinal; } set { StFechaFinal = value; } }
        public int Estado { get { return StEstado; } set { StEstado = value; } }
        public string Mensaje { get { return StMensaje; } set { StMensaje = value; } }
        public int Periodo { get { return StPeriodo; } set { StPeriodo = value; } }
        public OdbcConnection Myconnect { get { return stConnect; } set { stConnect = value; } }
        public string PstForfect { get { return stForfect; } set { stForfect = value; } }
        public string PstUsuario { get { return Stusuario; } set { Stusuario = value; } }

        public void GrabaParaFacturacion()
        {
            var stbuilder = new StringBuilder();
            ok = BuscaParaFacturacion();
            if (!ok)
            {
                stbuilder.Append("insert into cli_parfact(periodo,detalle,FechaPago,Clacont,Fecini,Fecfin,Estado,mensaje)");
                stbuilder.Append("values ('" + Periodo + "','");
                stbuilder.Append(Stdetalle + "','");
                stbuilder.Append(FecPago.ToString(stForfect) + "','");
                stbuilder.Append(ClaseContrato + "','");
                stbuilder.Append(FechaInicial.ToString(stForfect) + "','");
                stbuilder.Append(FechaFinal.ToString(stForfect) + "','");
                stbuilder.Append(Estado + "','");
                stbuilder.Append(Mensaje + "')");
            }
            else
            {
                stbuilder.Append("update cli_parfact set ");
                stbuilder.Append("detalle = '" + Detalle + "',");
                stbuilder.Append("FechaPago = '" + FecPago.ToString(stForfect) + "',");
                stbuilder.Append("Clacont = '" + ClaseContrato + "',");
                stbuilder.Append("Fecini = '" + FechaInicial.ToString(stForfect) + "',");
                stbuilder.Append("Fecfin = '" + FechaFinal.ToString(stForfect) + "',");
                stbuilder.Append("Estado = '" + Estado + "',");
                stbuilder.Append("mensaje = '" + Mensaje + "'");
                stbuilder.Append(" where periodo = '" + Periodo + "'");
            }
            string _f1 = "", _f2 = "", _f3 = "", _f4 = "";
            connect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "GrabaParaFacturacion", ref _f1, ref _f2, ref _f3, ref _f4);
        }

        public void CierraCiclo()
        {
            var stbuilder = new StringBuilder();
            ok = BuscaParaFacturacion();
            if (ok)
            {
                stbuilder.Append("update cli_parfact set ");
                stbuilder.Append("estado = '1'");
                stbuilder.Append(" where periodo = '" + Periodo + "'");
            }
            string _f1 = "", _f2 = "", _f3 = "", _f4 = "";
            connect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "GrabaParaFacturacion", ref _f1, ref _f2, ref _f3, ref _f4);
        }

        private bool BuscaParaFacturacion()
        {
            var stbuilder = new StringBuilder();
            var DsDataSet = new DataSet();
            stbuilder.Append("select periodo,detalle from cli_parfact where periodo = '" + Periodo + "'");
            connect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaParaFacturacion", ref DsDataSet, "tblparFact");
            return DsDataSet.Tables["tblparFact"].Rows.Count > 0;
        }

        public bool BuscaCamposFacturacion()
        {
            var stbuilder = new StringBuilder();
            var DsDataSet = new DataSet();
            stbuilder.Append("select periodo,detalle,FechaPago,Clacont,Fecini,Fecfin,Estado,mensaje ");
            stbuilder.Append("from cli_parfact where periodo = '" + Periodo + "'");
            connect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "GrabaParaFacturacion", ref DsDataSet, "tblparFact");

            if (DsDataSet.Tables["tblparFact"].Rows.Count > 0)
            {
                var r = DsDataSet.Tables["tblparFact"].Rows[0];
                Detalle       = r["detalle"].ToString();
                FecPago       = Convert.ToDateTime(r["FechaPago"]);
                ClaseContrato = Convert.ToInt32(r["Clacont"]);
                FechaInicial  = Convert.ToDateTime(r["Fecini"]);
                FechaFinal    = Convert.ToDateTime(r["Fecfin"]);
                Estado        = Convert.ToInt32(r["Estado"]);
                Mensaje       = r["mensaje"].ToString();
                return true;
            }
            return false;
        }

        public int HelpCiclos(Form Myforma)
        {
            var msgsas = new ERP.Core.Compartido.Utilidades.Ayuda(PstUsuario);
            string result = msgsas.CargaAyuda("cli_parfact", "periodo", "detalle", "FechaPago",
                Myconnect, Myforma, "Nombre", "Fecha Pago");
            int ret = 0;
            int.TryParse(result, out ret);
            return ret;
        }

        // VB stub: body builds SQL but ExecuteQueryDataset call is commented out; returns null
        public DataSet BuscaDatosFacturacion(string idcodigo)
        {
            var stbuilder = new StringBuilder();
            stbuilder.Append("select sevisiem, servsolido,tranfact from inv_facturas ");
            stbuilder.Append(" where idcodigo = '" + idcodigo + "'");
            // Me.ExecuteQueryDataset() — commented out in original VB
            return null;
        }

        public DataTable BuscaParametrosXml(string NomArchivo)
        {
            var reader = new XmlTextReader(NomArchivo);
            var DsDataSet = new DataSet();
            string Ststring = " ";
            string Servsiem = null, ServSolido = null, Servweb = null, Codtran = null;
            DsDataSet.Tables.Add("tblparfact");
            DsDataSet.Tables["tblparfact"].Columns.Add("Servsiem", Ststring.GetType());
            DsDataSet.Tables["tblparfact"].Columns.Add("Servsolido", Ststring.GetType());
            DsDataSet.Tables["tblparfact"].Columns.Add("Servweb", Ststring.GetType());
            DsDataSet.Tables["tblparfact"].Columns.Add("Codtran", Ststring.GetType());

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    while (reader.MoveToNextAttribute())
                    {
                        switch (reader.Name)
                        {
                            case "Servsiem":   Servsiem   = reader.Value; break;
                            case "Servsolido": ServSolido = reader.Value; break;
                            case "Codtran":    Codtran    = reader.Value; break;
                            case "Servweb":    Servweb    = reader.Value; break;
                        }
                    }
                }
            }
            DsDataSet.Tables["tblparfact"].Rows.Add(Servsiem, ServSolido, Servweb, Codtran);
            return DsDataSet.Tables["tblparfact"];
        }

        // ─── Private ExecuteQuery helpers (dead code) ──────────────────────────────
        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
                mycomqueryconec.Connection = appadoConect;
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0) result = true;
                while (Myread.Read())
                {
                    if (Campo1 != "")
                        Campo1 = Myread["campo1"] == DBNull.Value ? "0" : Myread["campo1"].ToString();
                    if (Campo2 != "")
                        Campo2 = Myread["campo2"] == DBNull.Value ? "0" : Myread["campo2"].ToString();
                    if (Campo3 != "")
                        Campo3 = Myread["campo3"] == DBNull.Value ? "0" : Myread["campo3"].ToString();
                    if (Campo4 != "")
                        Campo4 = Myread["campo4"] == DBNull.Value ? "0" : Myread["campo4"].ToString();
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql,
                    "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        private DataSet ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref DataSet DsDataSet, string Nomtabla)
        {
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandTimeout = 1000;
                mycomqueryconec.Connection = appadoConect;
                MyDataAdater.SelectCommand = mycomqueryconec;
                MyDataAdater.Fill(DsDataSet, Nomtabla);
                return DsDataSet;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql,
                    "SORTEC", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
        }
    }

    // ─── ClsMsgProcesos ───────────────────────────────────────────────────────────
    public class ClsMsgProcesos : clsmsgbase
    {
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private clsimpresion msgimp = new clsimpresion();
        private string stForfect, StDetalle, Stnit, stUsuario;
        private double stNumFactura = 0, StSubtotal, Stneto = 0, StVlrIva, StHostingDummy;
        // VB: Decimal backing
        private decimal StTasaIva;
        private bool ok;
        private string StRazonSocial;
        // VB: Double backing for StIdcodigo, int backing for StPeriodo
        private double StIdcodigo;
        private int StPeriodo;
        private DateTime Stfechapago, StfechaMovto;
        private double StRetFte = 0, StVlrRetFte = 0, StVlrIca = 0;
        private decimal StTasaIca = 0;

        public string Pstusuario { get { return stUsuario; } set { stUsuario = value; } }
        // VB: double backing, property String
        public string Idcodigo
        {
            get { return StIdcodigo.ToString(); }
            set { double.TryParse(value, out StIdcodigo); }
        }
        public string Detalle { get { return StDetalle; } set { StDetalle = value; } }
        public string Nit { get { return Stnit; } set { Stnit = value; } }
        public string RazonSocial { get { return StRazonSocial; } set { StRazonSocial = value; } }
        public DateTime FechaPago { get { return Stfechapago; } set { Stfechapago = value; } }
        public DateTime FechaMovto { get { return StfechaMovto; } set { StfechaMovto = value; } }
        public double NumFactura { get { return stNumFactura; } set { stNumFactura = value; } }
        public double Subtotal { get { return StSubtotal; } set { StSubtotal = value; } }
        public double VlrRetFte { get { return StVlrRetFte; } set { StVlrRetFte = value; } }
        public double TasaIca { get { return (double)StTasaIca; } set { StTasaIca = (decimal)value; } }
        public double VlrIca { get { return StVlrIca; } set { StVlrIca = value; } }
        public double RetFte { get { return StRetFte; } set { StRetFte = value; } }
        public double Neto { get { return Stneto; } set { Stneto = value; } }
        public double VlrIva { get { return StVlrIva; } set { StVlrIva = value; } }
        // VB: int backing, property Double
        public double Idperiodo { get { return StPeriodo; } set { StPeriodo = (int)value; } }
        public decimal TasaIva { get { return StTasaIva; } set { StTasaIva = value; } }

        public DataSet GeneraFacturacionLotes(int periodo, DateTime FechaMovtoParam, int TipFact,
            string Detalle, DateTime Fechavence, int IdServSiem, int IdServSolido, int IdServHosting,
            int CodTran, Form myforma)
        {
            var stbuilder = new StringBuilder();
            var DsDataset = new DataSet();
            int fila = 0;
            decimal RetFteLocal = 0;
            var invConfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Facturacion Por lotes", myforma);
            int ClaIva = 0;
            decimal TasaIvaLocal = 0;
            double IdFactura = 0;
            string StString = " ";
            double VlrBrutoAcum = 0;
            var msgconfig = new ERP.Core.Compartido.Configuracion.ParamSys();
            var dscompania = new DataSet();
            double SMLV = 0;

            DsDataset.Tables.Add("TblFact");
            DsDataset.Tables["TblFact"].Columns.Add("Idfactura", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("Idcodigo", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("nit", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("Razonsocial", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("Valor", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("VlrIva", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("VlrRetFte", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("VlrIca", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("VlrNeto", StString.GetType());

            stbuilder.Append("select codigo,cedula,nit,tipo,valmen,nombres,apellidos,razonsocial,direccion,sistema,tasaica,ValMenHosting ");
            stbuilder.Append(" from cli_maecli  where estcont = 0 and (valmen > 0 or ValMenHosting>0) and clacontrato = " + TipFact);
            connect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "GeneraFacturacionLotes", ref DsDataset, "tblclientes");

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblclientes"].Rows.Count);
            msgbarra.Show();

            PstCodEmpresa = "0001";
            string _codEmpresa = PstCodEmpresa;
            msgconfig.BuscarCompania(ref _codEmpresa, ref dscompania, Myconnect);

            SMLV = Convert.ToDouble(dscompania.Tables["tblcompania"].Rows[0]["salario_minimo"]);

            while (fila < DsDataset.Tables["tblclientes"].Rows.Count)
            {
                StVlrRetFte = 0; RetFteLocal = 0; VlrIca = 0; TasaIca = 0; VlrBrutoAcum = 0;
                var row = DsDataset.Tables["tblclientes"].Rows[fila];

                if (Convert.ToInt32(row["tipo"]) == 0)
                {
                    Nit = row["cedula"].ToString().Trim();
                    RazonSocial = row["apellidos"].ToString() + " " + row["nombres"].ToString();
                }
                else
                {
                    Nit = row["nit"].ToString().Trim();
                    RazonSocial = row["razonsocial"].ToString();
                }

                string Item = IdServSiem.ToString();
                switch (Convert.ToInt32(row["sistema"]))
                {
                    case 0: Item = IdServSiem.ToString(); break;
                    case 1: Item = IdServSolido.ToString(); break;
                }

                BuscaProductosHelper(Item, Myconnect, out ClaIva, out TasaIvaLocal, out RetFteLocal);

                if (Convert.ToInt32(row["sistema"]) == 1)
                    row["valmen"] = Math.Round(Convert.ToDouble(row["valmen"]) * SMLV, 0);

                switch (ClaIva)
                {
                    case 0:
                        VlrIva = 0; TasaIva = 0;
                        Subtotal = Convert.ToDouble(row["valmen"]);
                        break;
                    case 1:
                        VlrIva = Math.Round(Convert.ToDouble(row["valmen"]) * ((double)TasaIvaLocal / 100), 0);
                        Subtotal = Convert.ToDouble(row["valmen"]);
                        break;
                    case 2:
                        VlrIva = Math.Round(Convert.ToDouble(row["valmen"]) * (double)TasaIvaLocal / (100 + (double)TasaIvaLocal), 0);
                        row["valmen"] = Convert.ToDouble(row["valmen"]) - VlrIva;
                        Subtotal = Convert.ToDouble(row["valmen"]);
                        break;
                }

                if (RetFteLocal > 0 && Convert.ToInt32(row["tipo"]) == 1)
                    VlrRetFte = Math.Round(Subtotal * ((double)RetFteLocal / 100), 0);

                if (Convert.ToDouble(row["ValMenHosting"]) == 0)
                {
                    if (Convert.ToDecimal(row["TasaIca"]) > 0 && Convert.ToInt32(row["tipo"]) == 1 && Subtotal >= 165633)
                    {
                        VlrIca = Math.Round(Subtotal * (Convert.ToDouble(row["TasaIca"]) / 1000), 0);
                        TasaIca = Convert.ToDouble(row["TasaIca"]);
                    }
                }

                Neto = (Subtotal + VlrIva) - (VlrRetFte + VlrIca);

                invConfig.BuscaConseFactura("1", Myconnect, ref IdFactura);
                NumFactura = IdFactura;
                Idcodigo = row["codigo"].ToString();
                Idperiodo = periodo;
                FechaPago = Fechavence;
                this.FechaMovto = FechaMovtoParam;
                this.Detalle = Detalle;
                RetFte = (double)RetFteLocal;

                GrabaConseFactura();
                ok = GrabaFactura(int.Parse(Item), 1, Convert.ToDouble(row["valmen"]));
                if (ok)
                {
                    VlrBrutoAcum = Subtotal;
                    DsDataset.Tables["TblFact"].Rows.Add(NumFactura, row["codigo"], Nit, RazonSocial,
                        row["valmen"], VlrIva, VlrRetFte, VlrIca, Neto);
                }

                if (Convert.ToDouble(row["ValMenHosting"]) > 0)
                {
                    string ItemH = IdServHosting.ToString();
                    BuscaProductosHelper(ItemH, Myconnect, out ClaIva, out TasaIvaLocal, out RetFteLocal);
                    VlrRetFte = 0; VlrIca = 0;

                    if (Convert.ToInt32(row["sistema"]) == 1)
                        row["ValMenHosting"] = Math.Round(Convert.ToDouble(row["ValMenHosting"]) * SMLV, 0);

                    switch (ClaIva)
                    {
                        case 0:
                            VlrIva = 0; TasaIva = 0;
                            Subtotal = Convert.ToDouble(row["ValMenHosting"]);
                            break;
                        case 1:
                            VlrIva = Math.Round(Convert.ToDouble(row["ValMenHosting"]) * ((double)TasaIvaLocal / 100), 0);
                            Subtotal = Convert.ToDouble(row["ValMenHosting"]);
                            break;
                        case 2:
                            VlrIva = Math.Round(Convert.ToDouble(row["ValMenHosting"]) * (double)TasaIvaLocal / (100 + (double)TasaIvaLocal), 0);
                            row["ValMenHosting"] = Convert.ToDouble(row["ValMenHosting"]) - VlrIva;
                            Subtotal = Convert.ToDouble(row["ValMenHosting"]);
                            break;
                    }

                    if (RetFteLocal > 0 && Convert.ToInt32(row["tipo"]) == 1)
                        VlrRetFte = Math.Round(Subtotal * ((double)RetFteLocal / 100), 0);

                    if (Convert.ToDecimal(row["TasaIca"]) > 0 && Convert.ToInt32(row["tipo"]) == 1
                        && (Subtotal + VlrBrutoAcum) >= 165633)
                    {
                        VlrIca = Math.Round((Subtotal + VlrBrutoAcum) * (Convert.ToDouble(row["TasaIca"]) / 1000), 0);
                        TasaIca = Convert.ToDouble(row["TasaIca"]);
                    }

                    Neto = (Subtotal + VlrIva) - (VlrRetFte + VlrIca);
                    NumFactura = IdFactura;
                    Idcodigo = row["codigo"].ToString();
                    Idperiodo = periodo;
                    FechaPago = Fechavence;
                    this.FechaMovto = FechaMovtoParam;
                    this.Detalle = Detalle;
                    RetFte = (double)RetFteLocal;

                    ok = GrabaFactura(int.Parse(ItemH), 1, Convert.ToDouble(row["ValMenHosting"]));
                    if (ok)
                    {
                        DsDataset.Tables["TblFact"].Rows.Add(NumFactura, row["codigo"], Nit, RazonSocial,
                            row["ValMenHosting"], VlrIva, VlrRetFte, VlrIca, Neto);
                    }
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            msgbarra.Close();
            msgbarra.Dispose();
            ContabilizaFacturacion(periodo, CodTran, FechaMovtoParam, Fechavence, myforma);
            return DsDataset;
        }

        public bool ContabilizaFacturacion(int Periodo, int IdTipomovto, DateTime fechaMovto,
            DateTime FechaVence, Form Myforma)
        {
            var stbuilder = new StringBuilder();
            var DsDataset = new DataSet();
            int fila = 0;
            string Cencos = "99999999";
            var msginv_inst = new ERP.Core.Inventario.Services.msginv();
            var contab = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            var invConfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Contabiliza Facturación !!!", Myforma);
            string CuentaIva = "999999999999", Cuentadsto = "999999999999";
            string VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999";
            string InvNoGravado = "999999999999", InvGravado = "999999999999", CuentaIca = "999999999999";
            int CtrlExistencia = 0;
            string Cptecartera = "9999", CuentaCreditos = "99999999999999";
            string CpteTran = "9999", CpteCost = "9999", RetFteStr = "99999999999999";
            string ConseCpte = null;
            int canreg = 0, Sw1 = 0;

            stbuilder.Append("select docfact.idfactura,docfact.nit as idcliente,docfact.Idperiodo,docfact.FechaVence, movtofact.tasaIva ,idgruproducto, sum(movtofact.Subtotal) as subtotal, ");
            stbuilder.Append("sum(movtofact.Neto) as neto, sum(movtofact.Iva) as Iva , sum(movtofact.VlrRetfte) as VlrRetfte,sum(movtofact.vlrica) as VlrIca ");
            stbuilder.Append("from cli_movtofact movtofact inner join cli_docfact docfact  on movtofact.idfactura = docfact.idfactura ");
            stbuilder.Append("inner join inv_productos prod on movtofact.iditem = prod.idproducto ");
            stbuilder.Append("where docfact.Idperiodo = '" + Periodo + "' and docfact.estado = 'A' ");
            stbuilder.Append("group by docfact.idfactura,docfact.nit,docfact.Idperiodo,docfact.FechaVence, movtofact.tasaIva ,idgruproducto ");
            stbuilder.Append("order by docfact.idfactura,docfact.nit,docfact.Idperiodo,docfact.FechaVence, movtofact.tasaIva ,idgruproducto");
            connect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "GeneraFacturacionLotes", ref DsDataset, "tblFacturas");

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblFacturas"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataset.Tables["tblFacturas"].Rows.Count)
            {
                var row = DsDataset.Tables["tblFacturas"].Rows[fila];

                // BuscaGrupo: extracts Cencos (p6)
                string _idGru = row["IdGruProducto"].ToString();
                string _gDesc = " ", _gResum = " ", _gSecund = " ", _gDescSec = " ", _gLimVentas = " ";
                int _gCantRestrig = 0;
                invConfig.BuscaGrupo(ref _idGru, Myconnect,
                    ref _gDesc, ref _gResum,
                    ERP.Core.Inventario.Services.ClsInvConfig.Navega.Ninguno,
                    ref Cencos, ref _gSecund, ref _gDescSec, ref _gLimVentas, ref _gCantRestrig);

                // BuscaCuentaContable
                string _costoNograv = "999999999999", _costoGrav = "999999999999";
                invConfig.BuscaCuentaContable(row["IdGruProducto"].ToString(), IdTipomovto, Myconnect,
                    ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas,
                    ref _costoNograv, ref _costoGrav,
                    ref InvNoGravado, ref InvGravado, ref Neto, ref RetFteStr, ref CuentaIca);

                // BuscaTipomovto: extracts CpteTran (p5), CtrlExistencia (p8, ref string → int bridge)
                int _idTipomovto = IdTipomovto;
                string _tDesc = " ", _tResum = " ", _tCpteCost = " ", _tClaDoc = " ";
                double _tSecuencia = 0; string _tCtrlExist = "0"; int _tActCont = 0;
                var _tNaveg = ERP.Core.Inventario.Services.ClsInvConfig.Navega.Ninguno;
                string _tCtrlPrecio = " ", _tCtrlFact = " ", _tVlrTotal = " ", _tCostea = " ";
                string _tDevol = " ", _tTrasladaCont = " ", _tOrdenPed = " ", _tBonif = " ", _tValCup = " ";
                invConfig.BuscaTipomovto(ref _idTipomovto, Myconnect,
                    ref _tDesc, ref _tResum, ref CpteTran, ref _tCpteCost, ref _tSecuencia,
                    ref _tCtrlExist, ref _tClaDoc, ref _tActCont, ref _tNaveg, ref _tCtrlPrecio,
                    ref _tCtrlFact, ref _tVlrTotal, ref _tCostea, ref _tDevol, ref _tTrasladaCont,
                    ref _tOrdenPed, ref _tBonif, ref _tValCup);
                int.TryParse(_tCtrlExist, out CtrlExistencia);

                // BuscaComprobante: extracts CuentaCreditos (p19=Cuenta)
                double _consec = 0;
                string _ctrlConse = " ", _tipoDoc = " "; double _debitos = 0, _creditos = 0;
                string _cerrado = "N", _anulado = "N"; double _diferencia = 0;
                string _detalle = " ", _doctipo = "NC", _nodoc = " ";
                DateTime _fecMovBusc = new DateTime(1950, 1, 1);
                string _nombre = " ", _idbenef = "99999999999999", _cencCpte = "99999999";
                string _modulo = null; int _periodoB = 0; string _cantFact = "0", _detAuto = " ";
                contab.BuscaComprobante(ref CpteTran, ref _consec, false, Myconnect,
                    ref _ctrlConse, ref _tipoDoc, ref _debitos, ref _creditos,
                    ref _cerrado, ref _anulado, ref _diferencia, ref _detalle,
                    ref _doctipo, ref _nodoc, ref _fecMovBusc, ref _nombre,
                    ref _idbenef, ref _cencCpte, ref CuentaCreditos,
                    ref _modulo, ref _periodoB, ref _cantFact, ref _detAuto);
                Sw1 = 0;

                if (CpteTran == "9999")
                {
                    MessageBox.Show("Comprobante de la linea " + IdTipomovto + " no esta parametrizado",
                        "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break; // Exit Do
                    // Return False — unreachable after break, preserved for faithfulness
                }

                ConseCpte = row["idfactura"].ToString();
                row["idcliente"] = row["idcliente"].ToString().Trim();

                if (CtrlExistencia == 1)
                {
                    double _neto = Convert.ToDouble(row["neto"]);
                    double _iva  = Convert.ToDouble(row["Iva"]);
                    double _subt = Convert.ToDouble(row["Subtotal"]);
                    double _vlrR = Convert.ToDouble(row["VlrRetfte"]);
                    double _vlrI = Convert.ToDouble(row["Vlrica"]);
                    string _idCli = row["idcliente"].ToString();
                    string _periodo = fechaMovto.ToString("yyyyMM");

                    if (_neto > 0)
                    {
                        contab.GrabaMovimiento(CpteTran, double.Parse(ConseCpte), Neto, "9999",
                            _periodo, _idCli, fechaMovto,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            ConseCpte, _neto, 0.0, 0.0, Pstusuario, Myconnect,
                            0, "FC-" + ConseCpte, _idCli, Cencos,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            0.0, "cont", null, "FC", FechaVence,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            false, 0.0, "CH", "TES", true, "0", false);
                    }

                    if (_iva > 0)
                    {
                        contab.GrabaMovimiento(CpteTran, double.Parse(ConseCpte), CuentaIva, "9999",
                            _periodo, _idCli, fechaMovto,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            " ", 0.0, _iva, _iva, Pstusuario, Myconnect,
                            0, " ", "99999999999999", Cencos,
                            " ", 0.0, null, null, " ", new DateTime(1950, 1, 1),
                            null, false, 0.0, "CH", "TES", true, "0", false);
                    }

                    if (_neto > 0 && _iva > 0)
                    {
                        contab.GrabaMovimiento(CpteTran, double.Parse(ConseCpte), VentasGrabadas, "9999",
                            _periodo, _idCli, fechaMovto,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            " ", 0.0, _subt, _subt, Pstusuario, Myconnect,
                            0, " ", "99999999999999", Cencos,
                            " ", 0.0, null, null, " ", new DateTime(1950, 1, 1),
                            null, false, 0.0, "CH", "TES", true, "0", false);
                    }

                    if (_neto > 0 && _iva == 0)
                    {
                        contab.GrabaMovimiento(CpteTran, double.Parse(ConseCpte), ventasNoGrabadas, "9999",
                            _periodo, _idCli, fechaMovto,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            " ", 0.0, _neto, _neto, Pstusuario, Myconnect,
                            0, " ", "99999999999999", Cencos,
                            " ", 0.0, null, null, " ", new DateTime(1950, 1, 1),
                            null, false, 0.0, "CH", "TES", true, "0", false);
                    }

                    if (_vlrR > 0)
                    {
                        contab.GrabaMovimiento(CpteTran, double.Parse(ConseCpte), RetFteStr, "9999",
                            _periodo, _idCli, fechaMovto,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            " ", _vlrR, 0.0, 0.0, Pstusuario, Myconnect,
                            0, " ", "99999999999999", Cencos,
                            " ", 0.0, null, null, " ", new DateTime(1950, 1, 1),
                            null, false, 0.0, "CH", "TES", true, "0", false);
                    }

                    if (_vlrI > 0)
                    {
                        contab.GrabaMovimiento(CpteTran, double.Parse(ConseCpte), CuentaIca, "9999",
                            _periodo, _idCli, fechaMovto,
                            "Facturacion Automatica periodo facturado " + Periodo,
                            " ", _vlrI, 0.0, 0.0, Pstusuario, Myconnect,
                            0, " ", "99999999999999", Cencos,
                            " ", 0.0, null, null, " ", new DateTime(1950, 1, 1),
                            null, false, 0.0, "CH", "TES", true, "0", false);
                    }
                }

                msgbarra.PerformStep();
                fila += 1;

                if (fila >= DsDataset.Tables["tblFacturas"].Rows.Count)
                {
                    Sw1 = 0;
                }
                else if (ConseCpte == DsDataset.Tables["tblFacturas"].Rows[fila]["idfactura"].ToString())
                {
                    Sw1 = 1;
                }

                if (Sw1 == 0)
                {
                    ok = contab.CierreDocumento(CpteTran, double.Parse(ConseCpte), Myconnect);
                    if (ok)
                    {
                        NumFactura = double.Parse(ConseCpte);
                        Grabaestado("C", double.Parse(ConseCpte));
                    }
                }
            }

            msgbarra.Close();
            msgbarra.Dispose();
            return ok;
        }

        public DataSet ImpresionFacturas(int periodo, double NumInicial, double NumFinal,
            string PrintName, Form myforma)
        {
            var stbuilder = new StringBuilder();
            var DsDataset = new DataSet();
            int fila = 0;
            var invConfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            var msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Impresion de facturas", myforma);
            string StString = " ";
            msgimp.Myconnect = Myconnect;
            msgimp.PstCodEmpresa = "0001";

            DsDataset.Tables.Add("TblFact");
            DsDataset.Tables["TblFact"].Columns.Add("Idfactura", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("nit", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("Razonsocial", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("Valor", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("VlrIva", StString.GetType());
            DsDataset.Tables["TblFact"].Columns.Add("IdCodigo", StString.GetType());

            stbuilder.Append("select idfactura,detalle,fechavence,docfact.nit,subtotal,neto,iva,idcodigo,idperiodo,maecli.razonsocial,maecli.Nombres,maecli.apellidos, maecli.tipo");
            stbuilder.Append(" from cli_docfact docfact inner join cli_maecli maecli on docfact.idcodigo =  maecli.codigo ");
            stbuilder.Append("where idperiodo = '" + periodo + "' and idfactura between " + NumInicial + " and " + NumFinal);
            connect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "ImpresionFacturas", ref DsDataset, "tblfacturas");

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblfacturas"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataset.Tables["tblfacturas"].Rows.Count)
            {
                var row = DsDataset.Tables["tblfacturas"].Rows[fila];

                if (Convert.ToInt32(row["tipo"]) == 0)
                {
                    Nit = row["cedula"].ToString(); // Note: "cedula" not in query — preserved bug
                    RazonSocial = row["apellidos"].ToString() + " " + row["nombres"].ToString();
                }
                else
                {
                    Nit = row["nit"].ToString();
                    RazonSocial = row["razonsocial"].ToString();
                }

                msgimp.ImprimeInformesFacturacion(Convert.ToDouble(row["idfactura"]), PrintName);

                DsDataset.Tables["TblFact"].Rows.Add(
                    row["idfactura"], row["nit"], row["idfactura"], row["neto"], row["Iva"]);

                fila += 1;
                msgbarra.PerformStep();
            }

            msgbarra.Close();
            msgbarra.Dispose();
            return DsDataset;
        }

        // ─── Private helpers ───────────────────────────────────────────────────────
        private void GrabaConseFactura()
        {
            var stbuilder = new StringBuilder();
            stbuilder.Append("update inv_facturas set consefact = '" + NumFactura + "'");
            string _f1 = "", _f2 = "", _f3 = "", _f4 = "";
            connect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "GrabaConseFactura", ref _f1, ref _f2, ref _f3, ref _f4);
        }

        private bool GrabaFactura(int Item, int Cantidad, double ValUnidad)
        {
            bool _ok = GrabaDocFactura();
            if (_ok)
                GrabaMotoFactura(Item, Cantidad, ValUnidad);
            return _ok;
        }

        private bool GrabaDocFactura()
        {
            var stbuilder = new StringBuilder();
            ok = Buscafactura();
            if (!ok)
            {
                stbuilder.Append("insert into cli_docfact(idfactura,Detalle,Fechavence,nit,Subtotal,neto,Iva,Idcodigo,Idperiodo,fecha,VlrRetfte,VlrIca,usuario) ");
                stbuilder.Append("Values ('");
                stbuilder.Append(NumFactura + "','");
                stbuilder.Append(Detalle + "','");
                stbuilder.Append(FechaPago.ToString(stForfect) + "','");
                stbuilder.Append(Nit + "','");
                stbuilder.Append(Subtotal + "','");
                stbuilder.Append(Neto + "','");
                stbuilder.Append(VlrIva + "','");
                stbuilder.Append(Idcodigo + "','");
                stbuilder.Append(Idperiodo + "','");
                stbuilder.Append(FechaMovto.ToString(stForfect) + "','");
                stbuilder.Append(VlrRetFte + "','");
                stbuilder.Append(VlrIca + "','");
                stbuilder.Append(Pstusuario + "')");
            }
            else
            {
                stbuilder.Append("update cli_docfact set ");
                stbuilder.Append("Subtotal = Subtotal + '" + Subtotal + "',");
                stbuilder.Append("neto = neto + '" + Neto + "',");
                stbuilder.Append("VlrRetfte = VlrRetfte + '" + VlrRetFte + "',");
                stbuilder.Append("VlrIca = VlrIca + '" + VlrIca + "',");
                stbuilder.Append("Iva = Iva + '" + VlrIva + "'");
                stbuilder.Append(" where idfactura = '" + NumFactura + "'");
            }
            string _f1 = "", _f2 = "", _f3 = "", _f4 = "";
            ok = connect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "Grabadocfact", ref _f1, ref _f2, ref _f3, ref _f4);
            return ok;
        }

        private bool Grabaestado(string estado, double Idfactura)
        {
            var stbuilder = new StringBuilder();
            ok = Buscafactura();
            if (ok)
            {
                stbuilder.Append("update cli_docfact set ");
                stbuilder.Append("estado = '" + estado + "'");
                stbuilder.Append(" where idfactura = '" + Idfactura + "'");
            }
            string _f1 = "", _f2 = "", _f3 = "", _f4 = "";
            ok = connect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "Grabadocfact", ref _f1, ref _f2, ref _f3, ref _f4);
            return ok;
        }

        private void GrabaMotoFactura(int Item, int Cantidad, double VlrUnidad)
        {
            var stbuilder = new StringBuilder();
            ok = BuscaMovtoFactura(Item);
            if (!ok)
            {
                stbuilder.Append("insert into cli_movtofact(idfactura,IdItem,Cantidad,ValUnidad,Subtotal,neto,Iva,TasaIva,Retfte,VlrRetfte,TasaIca,Vlrica) ");
                stbuilder.Append("Values ('");
                stbuilder.Append(NumFactura + "','");
                stbuilder.Append(Item + "','");
                stbuilder.Append(Cantidad + "','");
                stbuilder.Append(VlrUnidad + "','");
                stbuilder.Append(Subtotal + "','");
                stbuilder.Append(Neto + "','");
                stbuilder.Append(VlrIva + "','");
                stbuilder.Append(TasaIva + "','");
                stbuilder.Append(RetFte + "','");
                stbuilder.Append(VlrRetFte + "','");
                stbuilder.Append(TasaIca + "','");
                stbuilder.Append(VlrIca + "')");
            }
            else
            {
                stbuilder.Append("update cli_movtofact set ");
                stbuilder.Append("Cantidad = Cantidad + '" + Cantidad + "',");
                stbuilder.Append("Subtotal = Subtotal + '" + Subtotal + "',");
                stbuilder.Append("neto = neto + '" + Neto + "',");
                stbuilder.Append("VlrRetfte = VlrRetfte + '" + VlrRetFte + "',");
                stbuilder.Append("Vlrica = Vlrica + '" + VlrIca + "',");
                stbuilder.Append("Iva = Iva + '" + VlrIva + "'");
                stbuilder.Append(" where idfactura = '" + NumFactura + "' and IdItem = '" + Item + "'");
            }
            string _f1 = "", _f2 = "", _f3 = "", _f4 = "";
            connect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "GrabaMotoFactura", ref _f1, ref _f2, ref _f3, ref _f4);
        }

        private bool Buscafactura()
        {
            var stbuilder = new StringBuilder();
            var DsDataSet = new DataSet();
            stbuilder.Append("select idfactura,detalle,Fechavence,Nit from cli_docfact where Idfactura = '" + NumFactura + "'");
            connect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "Buscafactura", ref DsDataSet, "tblFactura");
            return DsDataSet.Tables["tblFactura"].Rows.Count > 0;
        }

        private bool BuscaMovtoFactura(int Item)
        {
            var stbuilder = new StringBuilder();
            var DsDataSet = new DataSet();
            stbuilder.Append("select idfactura,Iditem from cli_movtofact where Idfactura = '" + NumFactura + "' and IdItem = '" + Item + "'");
            connect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaMovtoFactura", ref DsDataSet, "tblmovtoFactura");
            return DsDataSet.Tables["tblmovtoFactura"].Rows.Count > 0;
        }

        // ─── BuscaProductos wrapper ────────────────────────────────────────────────
        // VB call: msginvconfig.BuscaProductos(Item, Myconnect, , , , , , , , ClaIva, TasaIva, ...x19..., RetFte)
        // p10=ClaIva(ref string→int), p11=TasaIva(ref string→decimal), p31=RetFte(ref string→decimal)
        private void BuscaProductosHelper(string itemId, OdbcConnection myconnect,
            out int ClaIva, out decimal TasaIva, out decimal RetFteOut)
        {
            var invConfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            string _idProd = itemId;
            string _d1 = " ", _d2 = " ", _d3 = " ", _d4 = " ", _d5 = " ", _d6 = " ", _d7 = " ";
            string _claIvaStr = "0", _tasaIvaStr = "0";
            string _d8  = " ", _d9  = " ", _d10 = " ", _d11 = " ", _d12 = " ", _d13 = " ";
            string _d14 = " ", _d15 = " ", _d16 = " ", _d17 = " ", _d18 = " ", _d19 = " ";
            string _d20 = " "; // PorDsto
            var _naveg = ERP.Core.Inventario.Services.ClsInvConfig.Navega.Ninguno;
            string _d21 = " ", _d22 = " ", _d23 = " ", _d24 = " ";
            string _retFteStr = "0";
            string _d25 = " ", _d26 = " ", _d27 = " ", _d28 = " ", _d29 = " ", _d30 = " ";

            invConfig.BuscaProductos(
                ref _idProd, myconnect,
                ref _d1,  ref _d2,  ref _d3, ref _d4, ref _d5, ref _d6, ref _d7,
                ref _claIvaStr, ref _tasaIvaStr,
                ref _d8,  ref _d9,  ref _d10, ref _d11, ref _d12, ref _d13,
                ref _d14, ref _d15, ref _d16, ref _d17, ref _d18, ref _d19,
                ref _d20, " ", ref _naveg,
                ref _d21, ref _d22, ref _d23, ref _d24,
                ref _retFteStr,
                ref _d25, ref _d26, ref _d27, ref _d28, ref _d29, ref _d30);

            ClaIva = 0;   int.TryParse(_claIvaStr,  out ClaIva);
            TasaIva = 0;  decimal.TryParse(_tasaIvaStr, out TasaIva);
            RetFteOut = 0; decimal.TryParse(_retFteStr, out RetFteOut);
        }
    }

    // ─── clsimpresion ─────────────────────────────────────────────────────────────
    public class clsimpresion : clsmsgbase
    {
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        // int backing, property String (VB implicit int→string coercion)
        private int StTipoInforme;
        public string TipoInforme
        {
            get { return StTipoInforme.ToString(); }
            set { int.TryParse(value, out StTipoInforme); }
        }

        public void ImprimeInformesFacturacion(double NumeroFactura, string Printname)
        {
            if (StTipoInforme == 0)
                ImprimeFacturas(NumeroFactura, Printname);
        }

        private void ImprimeFacturas(double NumeroFactura, string Printname)
        {
            var Informe = new ERP.Core.Compartido.Reportes.reporte("factura1");
            var Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            var msgletras = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
            var invConfig = new ERP.Core.Inventario.Services.ClsInvConfig();

            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            string Resolucion = " ";
            DateTime FecResol = new DateTime(1950, 1, 1);
            int RegimenIva = 0;
            string Prefijo = " ";
            double NumInicial = 0, Numfinal = 0;
            string Ciudad = null;

            BuscaDatosFacturacionHelper("1", Myconnect, out Resolucion, out FecResol, out RegimenIva,
                out Prefijo, out NumInicial, out Numfinal);

            BuscarCompaniaHelper(PstCodEmpresa, Myconnect,
                out stnit, out DirEmp, out Nomcompania, out Telemp, out Ciudad);

            Informe.SetParameterValue("nit",         stnit);
            Informe.SetParameterValue("regimen",     stnit);
            Informe.SetParameterValue("direccion",   DirEmp);
            Informe.SetParameterValue("telefono",    Telemp);
            Informe.SetParameterValue("nombre",      Nomcompania);
            Informe.SetParameterValue("resolucion",  Resolucion);
            Informe.SetParameterValue("fecresol",    FecResol);
            Informe.SetParameterValue("numinicial",  NumInicial);
            Informe.SetParameterValue("numfinal",    Numfinal);
            Informe.SetParameterValue("prefijo",     Prefijo);
            Informe.SetParameterValue("enletras",    stnit);
            Informe.SetParameterValue("ciudad",      Ciudad);
            Informe.SetParameterValue("NumFactura",  NumeroFactura);
            Informe.PrintToPrinter(2, false, 0, 0);
        }

        // ─── BuscarCompania wrapper ────────────────────────────────────────────────
        // VB call: BuscarCompania(PstCodEmpresa, Myconnect, ..., stnit(p15), DirEmp(p16),
        //   ..., Nomcompania(p26), Telemp(p27), ..., Ciudad(p35))
        private void BuscarCompaniaHelper(string codEmpr, OdbcConnection myconnect,
            out string stnit, out string DirEmp, out string Nomcompania,
            out string Telemp, out string Ciudad)
        {
            string _cuentaUtil = " ", _cptoCap = "00", _calcSaldo = "N", _cptoAfavor = "0";
            int _tipoLiq = 0; decimal _tasaMora = 0; int _diasGracia = 0; decimal _tasaUsura = 0;
            int _baseLiq = 0; int _ctrlConse = 0; int _conseCredit = 0; string _cptoRevapo = "00";
            string _nit = " ", _direccion = " ", _nomres = " ";
            string _cptoServ = "9999", _cptoExt = "9999", _cptoAho = "9999", _cptoApo = "9999";
            string _cptoRetFte = "9999", _undred = "", _cptoCdats = "", _cptoIntCdats = "9999";
            string _nombre = " ", _telefono = "0";
            string _cpto4Mil = "9999", _cptoIntAhorro = "99", _cptoAnticipo = "9999";
            double _conseCdat = 0, _conseDep = 0; int _opRecDeuda = 0; string _cpteAnticipo = "9999";
            string _ciudad = " ", _genCobro = " "; double _vlrConsulta = 0; string _cpteConsulta = " ";
            string _nomResum = "", _cptoIntAnt = "9999", _cptoint = "9999", _cptomor = "9999";
            double _limDiarioLava = 0, _limMesLava = 0;
            string _manEstudio = "N", _cobraCod = "N", _porcEndeu = "0", _manCap = "0";
            string _serverSmtp = "", _passEnvio = "", _correoEnvio = "", _tipoNomina = "", _cptoIntAntic = " ";
            string _depto = " ", _jefeCartera = " ", _cpto4MilCheque = "9999", _cpteFavor = "9999";
            char _retenaux = 'N'; double _valorRetenaux = 0, _porcenRetenaux = 0;
            string _cuentaRetenaux = "999999999999", _consecFact = " ", _numCodeCredit = "   ";
            string _modifCuota = " "; char _conciliaBanca = 'N'; int _numPagare = 0;
            string _pagareNotas = "N", _formaPagare = "0", _controlaDep = "Y";
            string _paramcausacion = "Y", _disableTasaI = "N", _forapl = "1";
            int _convEnpacto = 0; string _feec = "EST";

            // msgparsys.BuscarCompania(codEmpr, myconnect, // ERROR: CS1503
                // ref _cuentaUtil, ref _cptoCap, ref _calcSaldo, ref _cptoAfavor, // ERROR: CS1503
                // ref _tipoLiq,    ref _tasaMora, ref _diasGracia, ref _tasaUsura, // ERROR: CS1503
                // ref _baseLiq,    ref _ctrlConse, ref _conseCredit, ref _cptoRevapo, // ERROR: CS1503
                // ref _nit,        // p15 // ERROR: CS1503
                // ref _direccion,  // p16 // ERROR: CS1503
                // ref _nomres,     // p17 // ERROR: CS1503
                // ref _cptoServ, ref _cptoExt, ref _cptoAho, ref _cptoApo, // ERROR: CS1503
                // ref _cptoRetFte, // p22 // ERROR: CS1503
                // ref _undred, ref _cptoCdats, ref _cptoIntCdats, // ERROR: CS1503
                // ref _nombre,     // p26 // ERROR: CS1503
                // ref _telefono,   // p27 // ERROR: CS1503
                // ref _cpto4Mil, ref _cptoIntAhorro, ref _cptoAnticipo, // ERROR: CS1503
                // ref _conseCdat, ref _conseDep, ref _opRecDeuda, ref _cpteAnticipo, // ERROR: CS1503
                // ref _ciudad,     // p35 // ERROR: CS1503
                // ref _genCobro, ref _vlrConsulta, ref _cpteConsulta, // ERROR: CS1503
                // ref _nomResum, ref _cptoIntAnt, ref _cptoint, ref _cptomor, // ERROR: CS1503
                // ref _limDiarioLava, ref _limMesLava, ref _manEstudio, ref _cobraCod, // ERROR: CS1503
                // ref _porcEndeu, ref _manCap, ref _serverSmtp, ref _passEnvio, // ERROR: CS1503
                // ref _correoEnvio, ref _tipoNomina, ref _cptoIntAntic, // ERROR: CS1503
                // ref _depto, ref _jefeCartera, ref _cpto4MilCheque, ref _cpteFavor, // ERROR: CS1503
                // ref _retenaux, ref _valorRetenaux, ref _porcenRetenaux, ref _cuentaRetenaux, // ERROR: CS1503
                // ref _consecFact, ref _numCodeCredit, ref _modifCuota, ref _conciliaBanca, // ERROR: CS1503
                // ref _numPagare, ref _pagareNotas, ref _formaPagare, ref _controlaDep, // ERROR: CS1503
                // ref _paramcausacion, ref _disableTasaI, ref _forapl, ref _convEnpacto, ref _feec); // ERROR: CS1503

            stnit       = _nit;
            DirEmp      = _direccion;
            Nomcompania = _nombre;   // p26
            Telemp      = _telefono; // p27
            Ciudad      = _ciudad;   // p35
        }

        // ─── BuscaDatosFacturacion wrapper ─────────────────────────────────────────
        // VB call: BuscaDatosFacturacion(1, Myconnect, Resolucion, FecResol, RegimenIva,
        //   Prefijo, NumInicial, Numfinal)
        // C# signature uses ref string for FecResol, NumInicial, NumFinal; needs bridges
        private void BuscaDatosFacturacionHelper(string idCodigo, OdbcConnection myconnect,
            out string Resolucion, out DateTime FecResol, out int RegimenIva,
            out string Prefijo, out double NumInicial, out double Numfinal)
        {
            var invConfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            string _idCod = idCodigo;
            string _resolucion = " ", _fechResolStr = " "; int _regimenIva = 0;
            string _prefijo = " ", _numInicialStr = "0", _numFinalStr = "0";
            double _consefact = 0;
            var _naveg = ERP.Core.Inventario.Services.ClsInvConfig.Navega.Ninguno;
            string _cpteCart = "9999"; int _lincred = 9999, _clades = 0, _plazo = 0;
            string _cpteCartEmpl = "9999"; int _lincredEmpl = 9999, _cladesEmpl = 0, _plazoEmpl = 0;
            string _cpteCartPatro = "9999"; int _lincredPatro = 9999, _cladesPatro = 0, _plazoPatro = 0;
            int _tipMovAjuInv = 0;
            string _cpteCartTeresp = "9999"; int _lincredTeresp = 9999, _cladesTeresp = 0, _plazoTeresp = 0;
            string _cpteCartTer = "9999"; int _lincredTer = 9999, _cladesTer = 0, _plazoTer = 0;
            int _actcostos = 0;
            string _impTir = "N", _abreReg = "N", _grpComis = " ";
            double _tasaRtf = 0;
            string _prevenCosto = "N", _soloDesc = "N", _ivaConDesc = "N";

            invConfig.BuscaDatosFacturacion(ref _idCod, myconnect,
                ref _resolucion, ref _fechResolStr, ref _regimenIva, ref _prefijo,
                ref _numInicialStr, ref _numFinalStr, ref _consefact, _naveg,
                ref _cpteCart, ref _lincred, ref _clades, ref _plazo,
                ref _cpteCartEmpl, ref _lincredEmpl, ref _cladesEmpl, ref _plazoEmpl,
                ref _cpteCartPatro, ref _lincredPatro, ref _cladesPatro, ref _plazoPatro,
                ref _tipMovAjuInv, ref _cpteCartTeresp, ref _lincredTeresp, ref _cladesTeresp,
                ref _plazoTeresp, ref _cpteCartTer, ref _lincredTer, ref _cladesTer,
                ref _plazoTer, ref _actcostos, ref _impTir, ref _abreReg,
                ref _grpComis, ref _tasaRtf, ref _prevenCosto, ref _soloDesc, ref _ivaConDesc);

            Resolucion = _resolucion;
            DateTime.TryParse(_fechResolStr, out FecResol);
            RegimenIva = _regimenIva;
            Prefijo    = _prefijo;
            double.TryParse(_numInicialStr, out NumInicial);
            double.TryParse(_numFinalStr,   out Numfinal);
        }
    }
}
