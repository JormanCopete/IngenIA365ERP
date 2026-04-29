using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using ERP.Core.Compartido.Configuracion;

namespace ERP.Core.CDT.Models
{
    /// <summary>
    /// Clase para parametros de CDTs
    /// </summary>
    public class ParamCdt
    {
        #region Campos privados

#if EXCEL_LEGACY
        private Excel.Application _mExcel;
#endif
        private OdbcCommand _mycomqueryconec = new OdbcCommand();
        private string _stmysql;
        private ERP.Core.Compartido.Datos.ClsConect _odbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect _varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ParamSys _paramsys = new ParamSys();
        private bool _ok;

        #endregion

        #region Enumeraciones

        public enum Navega
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }

        #endregion

        #region Constructor y Destructor

        public ParamCdt()
        {
            _odbcConnect.MyOdbcConect(ref _varini);
        }

        ~ParamCdt()
        {
        }

        #endregion

        #region Metodos privados

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            string er = string.Empty;
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

        private bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref DataSet DsDataset, string NombreTabla)
        {
            OdbcDataAdapter myread = new OdbcDataAdapter();
            _mycomqueryconec.CommandText = stMysql;
            _mycomqueryconec.Connection = appadoConect;
            _mycomqueryconec.CommandText = Strings.Replace(_mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
            _mycomqueryconec.ExecuteNonQuery();

            myread.SelectCommand = _mycomqueryconec;
            myread.Fill(DsDataset, NombreTabla);

            return DsDataset.Tables[NombreTabla].Rows.Count > 0;
        }

        #endregion

        #region Metodos publicos

        public bool BuscaParamCdats(ref int lincred, OdbcConnection myconnect,
            ref string descripcion, ref double TasaMin, ref double PorRetFte, ref double VlrMinRte,
            ref int CptoInt, ref int IncreMen,
            ref int idformato, ref int idcpto, ref int fuente,
            ref string cuentatesoreria, ref int ForPagint, ref string tipointeres)
        {
            return BuscaParamCdats(ref lincred, myconnect,
                ref descripcion, ref TasaMin, ref PorRetFte, ref VlrMinRte,
                ref CptoInt, ref IncreMen, Navega.Ninguno,
                ref idformato, ref idcpto, ref fuente,
                ref cuentatesoreria, ref ForPagint, ref tipointeres);
        }

        public bool BuscaParamCdats(ref int lincred, OdbcConnection myconnect,
            ref string descripcion, ref double TasaMin, ref double PorRetFte, ref double VlrMinRte,
            ref int CptoInt, ref int IncreMen, Navega Navegar,
            ref int idformato, ref int idcpto, ref int fuente,
            ref string cuentatesoreria, ref int ForPagint, ref string tipointeres)
        {
            string where = "";
            _stmysql = "";
            bool ok = false;

            if (Navegar == Navega.Ninguno)
            {
                where = "from cdt_parame58 where lincred =" + lincred;
            }
            else if (Navegar == Navega.Primero)
            {
                where = "from cdt_parame58 where lincred >0 order by lincred" + _varini.Pstlimit;
            }
            else if (Navegar == Navega.Siguiente)
            {
                where = "from cdt_parame58 where lincred >" + lincred + " order by lincred" + _varini.Pstlimit;
            }
            else if (Navegar == Navega.Anterior)
            {
                where = "from cdt_parame58 where lincred <" + lincred + " order by lincred desc" + _varini.Pstlimit;
            }
            else if (Navegar == Navega.Ultimo)
            {
                where = "from cdt_parame58 where lincred <= 999999 order by lincred desc" + _varini.Pstlimit;
            }

            string sLincred = lincred.ToString();
            string sTasaMin = TasaMin.ToString();
            string sPorRetFte = PorRetFte.ToString();
            string sVlrMinRte = VlrMinRte.ToString();
            string sCptoInt = CptoInt.ToString();
            string sIncreMen = IncreMen.ToString();
            string sIdformato = idformato.ToString();
            string sIdcpto = idcpto.ToString();
            string sFuente = fuente.ToString();
            string sForPagint = ForPagint.ToString();

            _stmysql = "select" + _varini.Psttop + " descripcion as campo1,tasamin as campo2,porretfte as campo3,lincred as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscaParamCdats", ref descripcion, ref sTasaMin, ref sPorRetFte, ref sLincred);

            _stmysql = "select" + _varini.Psttop + " vlrminret as campo1,cptoint as campo2, incremen as campo3, cuentatesoreria as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscaParamCdats", ref sVlrMinRte, ref sCptoInt, ref sIncreMen, ref cuentatesoreria);

            _stmysql = "select" + _varini.Psttop + " idformato as campo1,idcpto as campo2, fuente as campo3,ForPagint as campo4 ";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscaParamCdats", ref sIdformato, ref sIdcpto, ref sFuente, ref sForPagint);

            _stmysql = "select" + _varini.Psttop + " tipointeres as campo1 ";
            string dummy1 = "", dummy2 = "", dummy3 = "";
            ok = _odbcConnect.ExecuteQueryconec(_stmysql + where, myconnect, "BuscaParamCdats", ref tipointeres, ref dummy1, ref dummy2, ref dummy3);

            int.TryParse(sLincred, out lincred);
            double.TryParse(sTasaMin, out TasaMin);
            double.TryParse(sPorRetFte, out PorRetFte);
            double.TryParse(sVlrMinRte, out VlrMinRte);
            int.TryParse(sCptoInt, out CptoInt);
            int.TryParse(sIncreMen, out IncreMen);
            int.TryParse(sIdformato, out idformato);
            int.TryParse(sIdcpto, out idcpto);
            int.TryParse(sFuente, out fuente);
            int.TryParse(sForPagint, out ForPagint);

            return ok;
        }

        public bool BuscaParamCdats(ref int lincred, OdbcConnection myconnect)
        {
            string descripcion = "";
            double TasaMin = 0, PorRetFte = 0, VlrMinRte = 0;
            int CptoInt = 0, IncreMen = 0, idformato = 0, idcpto = 0, fuente = 0, ForPagint = 0;
            string cuentatesoreria = " ", tipointeres = "0";
            return BuscaParamCdats(ref lincred, myconnect, ref descripcion, ref TasaMin, ref PorRetFte, ref VlrMinRte,
                ref CptoInt, ref IncreMen, Navega.Ninguno, ref idformato, ref idcpto, ref fuente,
                ref cuentatesoreria, ref ForPagint, ref tipointeres);
        }

        public bool GrabarParamCdats(int lincred, string descripcion, double tasamin, double porretfte,
            double vlrminrte, int cptoInt, double incremen, string usuario, int idformato, int idcpto,
            int fuente, OdbcConnection myconnect, string cuenta, int ForPagint, string tipointeres)
        {
            bool ok = false;
            string stmysql = "";
            string nomusu = " ";
            object dummyCreditoMin = 0, dummyCreditoMax = 0, dummyCreditoGraMin = 0, dummyCreditoGraMax = 0;
            string dummyPass = "", dummyGrupo = "", dummyCedula = "";
            bool dummyBool = false;
            DateTime dummyFechaCrea = new DateTime(1950, 1, 1), dummyFechaVence = new DateTime(1950, 1, 1);
            _paramsys.BuscaUsuario(ref usuario, myconnect,
                ref nomusu, ref dummyCreditoMin, ref dummyCreditoMax,
                ref dummyPass, ref dummyGrupo, ref dummyBool, ref dummyFechaCrea,
                ref dummyFechaVence, ref dummyBool, ref dummyBool,
                ref dummyCreditoGraMin, ref dummyCreditoGraMax, ref dummyCedula, ref dummyBool);

            ok = BuscaParamCdats(ref lincred, myconnect);
            if (!ok)
            {
                stmysql = "insert into cdt_parame58(lincred,descripcion,TasaMin,PorRetFte,VlrMinRet,CptoInt,IncreMen,usuario,nomusu,fechasys,idformato,idcpto,fuente, cuentatesoreria,ForPagint,tipointeres)" +
                    " Values ( " + lincred + ",'" + descripcion + "','" + tasamin + "','" + porretfte +
                    "','" + vlrminrte + "'," + cptoInt + ",'" + incremen + "','" + usuario + "','" + nomusu + "','" + Strings.Format(DateTime.Now, _varini.pstForfecyHora) +
                    "','" + idformato + "','" + idcpto + "','" + fuente + "','" + cuenta + "','" + ForPagint + "','" + tipointeres + "')";
            }
            else
            {
                stmysql = "update cdt_parame58 set descripcion = '" + descripcion + "',TasaMin = " + tasamin + ", PorRetFte = " + porretfte +
                    ",VlrMinRet = " + vlrminrte + ", CptoInt = " + cptoInt +
                    ",IncreMen = " + incremen + ",usuario = '" + usuario + "',nomusu = '" + nomusu + "',fechasys = '" + Strings.Format(DateTime.Now, _varini.pstForfecyHora) +
                    "',idformato = '" + idformato + "',idcpto = '" + idcpto + "',fuente = '" + fuente + "',cuentatesoreria='" + cuenta + "',ForPagint = '" + ForPagint + "',tipointeres='" + tipointeres + "' where lincred = " + lincred;
            }
            ok = _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarParamCdats");

            return ok;
        }

        public bool EliminarParamCdats(int lincred, OdbcConnection myconnect)
        {
            string stmysql = "Delete from cdt_parame58 where lincred = " + lincred;
            bool ok = _odbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarParamCdats");
            return ok;
        }

        public void CargarVentanaTasas(int LineaCdat, Form forma, OdbcConnection Myconnect)
        {
            ERP.Core.Compartido.Configuracion.frmtasas ftasas = new ERP.Core.Compartido.Configuracion.frmtasas();
            DataSet dsdatos = BuscarTasasporPlazos(LineaCdat, Myconnect);

            ftasas.DgvTasas.AutoGenerateColumns = false;
            ftasas.DgvTasas.DataSource = dsdatos.Tables["tbltasas"];
            ftasas.LineaCdat = LineaCdat;
            ftasas.myconexion = Myconnect;
            ftasas.StartPosition = FormStartPosition.CenterParent;
            ftasas.ShowDialog(forma);
        }

        public DataSet BuscarTasasporPlazos(int lincred, OdbcConnection myconnect)
        {
            DataSet dstasas = new DataSet();
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select lincred,vlrinicial,vlrfinal,plazoinicial,plazofinal,tasa ");
            stbuilder.Append("from cdt_tasasplazos ");
            stbuilder.Append("where lincred=" + lincred);
            stbuilder.Append(" order by lincred,vlrinicial,vlrfinal,plazoinicial,plazofinal");

            _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarTasasporPlazos",ref dstasas, "tbltasas");
            return dstasas;
        }

        public virtual decimal BuscarTasasporPlazos(int lincred, int Plazo, double Monto, OdbcConnection myconnect)
        {
            DataSet dstasas = new DataSet();
            decimal Tasa = 0;
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select lincred,vlrinicial,vlrfinal,plazoinicial,plazofinal,tasa ");
            stbuilder.Append("from cdt_tasasplazos ");
            stbuilder.Append("where lincred=" + lincred + " and " + Monto + ">=vlrinicial and " + Monto + "<=vlrfinal");
            stbuilder.Append(" and " + Plazo + ">=plazoinicial and " + Plazo + "<=plazofinal");

            _ok = _odbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarTasasporPlazos", ref dstasas, "tbltasas");
            if (_ok)
            {
                Tasa = Convert.ToDecimal(dstasas.Tables["tbltasas"].Rows[0]["tasa"]);
            }
            return Tasa;
        }

        public void EliminarTasasPorPlazos(int lincred, OdbcConnection myconnect)
        {
            _stmysql = "delete from cdt_tasasplazos where lincred=" + lincred;
            _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminarTasasPorPlazos");
        }

        public void GrabarTasasporPlazos(int lincred, double vlrinicial, double vlrfinal,
            double plazoinicial, double plazofinal, decimal tasa, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("insert into cdt_tasasplazos (lincred,vlrinicial,vlrfinal,plazoinicial,plazofinal,tasa,fecactualizacion) values ( ");
            stbuilder.Append(lincred + ",'");
            stbuilder.Append(vlrinicial + "','");
            stbuilder.Append(vlrfinal + "','");
            stbuilder.Append(plazoinicial + "','");
            stbuilder.Append(plazofinal + "','");
            stbuilder.Append(tasa + "','");
            stbuilder.Append(Strings.Format(DateTime.Now, _varini.pstForfecyHora) + "')");

            _odbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabarTasasporPlazos");
        }

        public bool EliminarTasasPorPlazosRegistro(int lincred, double vlrinicial, double vlrfinal,
            double plazoinicial, double plazofinal, OdbcConnection myconnect)
        {
            _stmysql = "delete from cdt_tasasplazos where lincred=" + lincred + " and vlrinicial=" + vlrinicial +
                " and vlrfinal=" + vlrfinal + " and plazoinicial=" + plazoinicial + " and plazofinal=" + plazofinal;
            _ok = _odbcConnect.ExecuteQueryconec(_stmysql, myconnect, "EliminarTasasPorPlazosRegistro");
            return _ok;
        }

        #endregion
    }
}
