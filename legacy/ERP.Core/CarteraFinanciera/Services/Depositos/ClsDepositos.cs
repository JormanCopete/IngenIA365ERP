using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Depositos
{
    public class ClsDepositos
    {
        // ----------------------------------------------------------------
        // Fields
        // ----------------------------------------------------------------
        private bool ok;
        private string stmysql;
        private OdbcConnection myconnect = new OdbcConnection();
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera car = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Compartido.Configuracion.ParamSys paramsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop_car = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();   // was "msgcop" in VB - renamed to avoid namespace conflict
        private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt_cnt = new ERP.Core.Contabilidad.Services.ClsContabilidad(); // was "msgcnt" in VB
        private ERP.Core.Compartido.Datos.ClsConect CargaVarini = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private Regvalidacion Validar = new Regvalidacion();
        private DataSet DatPromedioSaldo = new DataSet();
        private ArrayList lista = new ArrayList();

        // ----------------------------------------------------------------
        // Struct
        // ----------------------------------------------------------------
        public struct Regvalidacion
        {
            public string codigoter;
            public string Nombre;
            public string Cpte;
            public string ConseCpte;
            public int Lincred;
            public int ConseLincred;
            public string DescLincred;
            public string Detalle;
            public double Valor;
            public double Saldo;
            public double SaldoCanje;
            public double SaldoDisponible;
            public int TipoValidadora;
            public string ValNombre;
            public string TituloInforme;
            public string Titulos;
            public int TamLetra;
            public string Usuario;
            public string EmpresaResum;
            public string MuestraSaldo;
        }

        // ----------------------------------------------------------------
        // Enums
        // ----------------------------------------------------------------
        public enum Navega : int
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }
        public enum OpcionesDebAutomatico : int
        {
            Todos_Conceptos = 0,
            ConceptosDeCartera = 1,
            SoloConceptos_AP = 2,
            SoloConceptos_AH = 3,
            ObligacionesMarcadas = 4
        }
        public enum OpcionesBaseCaja : int
        {
            Base = 1,
            Ingresos = 2,
            Salidas = 3,
            Todos = 4
        }
        public enum FormaLiquidacion : int
        {
            LiquidaIntereses = 0,
            Cancelacion = 1
        }
        public enum FormaPagoInteres : int
        {
            Concepto = 0,
            CuentaAhorros = 1,
            Tesoreria = 2,
            Consigna = 3
        }
        public enum TipoPagoInteres : int
        {
            SoloIntereses = 0,
            InteresesAcumulados = 1
        }

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------
        public ClsDepositos()
        {
            CargaVarini.MyOdbcConect(ref Var.varini);
        }

        // ----------------------------------------------------------------
        // Local helper: ExecuteQueryDataset
        // ----------------------------------------------------------------
        private bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref DataSet DsDataset, string NombreTabla)
        {
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandTimeout = 0;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
            mycomqueryconec.ExecuteNonQuery();
            Myread.SelectCommand = mycomqueryconec;
            Myread.Fill(DsDataset, NombreTabla);
            if (DsDataset.Tables[NombreTabla].Rows.Count > 0)
                return true;
            else
                return false;
        }

        // ----------------------------------------------------------------
        // Local helper: ExecuteQueryconec (5 overloads, 0..4 ref string params)
        // ----------------------------------------------------------------
        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento)
        {
            string c1 = null, c2 = null, c3 = null, c4 = null;
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref c1, ref c2, ref c3, ref c4);
        }
        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1)
        {
            string c2 = null, c3 = null, c4 = null;
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref c2, ref c3, ref c4);
        }
        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2)
        {
            string c3 = null, c4 = null;
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref c3, ref c4);
        }
        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3)
        {
            string c4 = null;
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref Campo3, ref c4);
        }
        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            string er = null;
            try
            {
                bool result = false;
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0)
                    result = true;
                if (Myread.Read())
                {
                    if (stMysql.Contains("campo1"))
                    {
                        if (Myread["campo1"] is DBNull)
                            Campo1 = "0";
                        else
                            Campo1 = Myread["campo1"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo2"))
                    {
                        if (Myread["campo2"] is DBNull)
                            Campo2 = "0";
                        else
                            Campo2 = Myread["campo2"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo3"))
                    {
                        if (Myread["campo3"] is DBNull)
                            Campo3 = "0";
                        else
                            Campo3 = Myread["campo3"].ToString().Trim();
                    }
                    if (stMysql.Contains("campo4"))
                    {
                        if (Myread["campo4"] is DBNull)
                            Campo4 = "0";
                        else
                            Campo4 = Myread["campo4"].ToString().Trim();
                    }
                    result = true;
                }
                Myread.Close();
                return result;
            }
            catch
            {
                er = Information.Err().Description;
                return false;
            }
            finally
            {
                if (er != null)
                    throw new Exception(er + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql);
            }
        }

        // ----------------------------------------------------------------
        // BuscaCajeros
        // ----------------------------------------------------------------
        public bool BuscaCajeros(string Cajero, OdbcConnection Conect, ref ListView Wlist)
        {
            string Validadora = " ";
            return BuscaCajeros(Cajero, Conect, ref Wlist, ref Validadora);
        }
        public bool BuscaCajeros(string Cajero, OdbcConnection Conect, ref ListView Wlist, ref string Validadora)
        {
            int canreg = 0, fila = 0;
            string where = " where codCajero = '" + Cajero.Trim().ToLower() + "' and estado = 'A'";
            stmysql = "select compte ,consecpte ,nombre ,estado, fecApertura , Descripcion ,TiempoClave , validadora  from dep_cajeros ";
            DataSet MyRead = new DataSet();
            CargaVarini.ExecuteQueryDataset(stmysql + where, Conect, "BuscaCajeros", ref MyRead, "TblBuscaCajero");
            canreg = MyRead.Tables["TblBuscaCajero"].Rows.Count;
            Wlist.Items.Clear();
            ok = false;
            while (fila < canreg)
            {
                ok = true;
                Validadora = MyRead.Tables["TblBuscaCajero"].Rows[fila]["validadora"].ToString();
                ListViewItem lvi = Wlist.Items.Add(MyRead.Tables["TblBuscaCajero"].Rows[fila]["compte"].ToString());
                lvi.SubItems.Add(MyRead.Tables["TblBuscaCajero"].Rows[fila]["consecpte"].ToString());
                lvi.SubItems.Add(Convert.ToDateTime(MyRead.Tables["TblBuscaCajero"].Rows[fila]["fecApertura"]).ToString("yyyy-MM-dd"));
                lvi.SubItems.Add(MyRead.Tables["TblBuscaCajero"].Rows[fila]["TiempoClave"].ToString());
                lvi.SubItems.Add(MyRead.Tables["TblBuscaCajero"].Rows[fila]["estado"].ToString());
                fila += 1;
            }
            MyRead.Dispose();
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabaValidadora
        // ----------------------------------------------------------------
        public bool GrabaValidadora(string Cajero, int TiempoClave, string Validadora, OdbcConnection conect)
        {
            stmysql = "update dep_cajeros set validadora = '" + Validadora + "',TiempoClave = '" + TiempoClave + "' where codcajero = '" + Cajero.ToLower() + "'";
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabaValidadora");
            return ok;
        }

        // ----------------------------------------------------------------
        // BuscaComproDeposito (overloads)
        // ----------------------------------------------------------------
        public bool BuscaComproDeposito(string Comprobante, int Consecutivo, OdbcConnection conect)
        {
            double Debitos = 0, Creditos = 0, Diferencia = 0;
            int Tiempo = 0;
            DateTime Fecha = new DateTime(1950, 1, 1);
            string CuentaCpte = " ", CencosCpte = "99999999";
            int TipoTrans = 0;
            return BuscaComproDeposito(Comprobante, Consecutivo, conect, ref Debitos, ref Creditos, ref Diferencia, ref Tiempo, ref Fecha, ref CuentaCpte, ref CencosCpte, ref TipoTrans);
        }
        public bool BuscaComproDeposito(string Comprobante, int Consecutivo, OdbcConnection conect,
            ref double Debitos, ref double Creditos, ref double Diferencia,
            ref int Tiempo, ref DateTime Fecha, ref string CuentaCpte,
            ref string CencosCpte, ref int TipoTrans)
        {
            int canreg = 0;
            stmysql = "select compronte,numero_domto, fecha, debito, credito,cerrado,cuenta_contable,cencosto" +
                " ,tiempoClave,cajeros.TipoTrans from cop_docmto copdoc inner join sys_compro02 pardoc on codigo = copdoc.compronte" +
                " left join dep_cajeros cajeros on cajeros.compte = copdoc.compronte and" +
                " cajeros.ConseCpte = copdoc.numero_domto  where  compronte = '" +
                Comprobante + "' and numero_domto = " + Consecutivo;
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "BuscaComproDeposito");
            if (ok)
            {
                DataSet MyRead = new DataSet();
                CargaVarini.ExecuteQueryDataset(stmysql, conect, "BuscaComproDeposito", ref MyRead, "TblBuscaComproDep");
                canreg = MyRead.Tables["TblBuscaComproDep"].Rows.Count;
                if (canreg > 0)
                {
                    DataRow row = MyRead.Tables["TblBuscaComproDep"].Rows[0];
                    if (row["tiempoClave"] == null || row["tiempoClave"] is DBNull)
                    {
                        Debitos = 0;
                        Creditos = 0;
                        Diferencia = 0;
                        Tiempo = 0;
                        Fecha = DateTime.Now;
                        TipoTrans = 0;
                    }
                    else
                    {
                        Debitos = Convert.ToDouble(row["debito"]);
                        Creditos = Convert.ToDouble(row["Credito"]);
                        Diferencia = Convert.ToDouble(row["debito"]) - Convert.ToDouble(row["credito"]);
                        Tiempo = Information.IsNumeric(row["tiempoClave"]) ? Convert.ToInt32(row["tiempoClave"].ToString()) : 0;
                        Fecha = (row["fecha"] == null || row["fecha"] is DBNull) ? DateTime.Now : Convert.ToDateTime(row["fecha"]);
                        TipoTrans = Convert.ToInt32(row["TipoTrans"]);
                    }
                    CuentaCpte = row["cuenta_contable"].ToString();
                    CencosCpte = row["cencosto"].ToString();
                }
                MyRead.Dispose();
            }
            return ok;
        }

        // ----------------------------------------------------------------
        // ValidaComproCajero
        // ----------------------------------------------------------------
        public bool ValidaComproCajero(string Cajero, string Comprobante, int Consecutivo, OdbcConnection conect)
        {
            string Estado = "C";
            return ValidaComproCajero(Cajero, Comprobante, Consecutivo, conect, ref Estado);
        }
        public bool ValidaComproCajero(string Cajero, string Comprobante, int Consecutivo, OdbcConnection conect, ref string Estado)
        {
            stmysql = "select estado as campo1 from dep_cajeros where  Compte = '" +
                Comprobante.Trim() + "' and consecpte = " + Consecutivo.ToString().Trim() + " and codCajero = '" + Cajero.Trim().ToLower() + "'";
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "ValidaComproCajero", ref Estado);
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabarCajero
        // ----------------------------------------------------------------
        public bool GrabarCajero(string Cajero, string Comprobante, int Consecutivo, DateTime Fecha, int Tiempo, OdbcConnection conect, int TipoTrans)
        {
            string nomusu = " ", estado = "A";
            // paramsys.BuscaUsuario(Cajero, conect, ref nomusu); // ERROR: CS1615, CS1620
            if (!ValidaComproCajero(Cajero, Comprobante, Consecutivo, conect))
            {
                stmysql = "insert into dep_cajeros(codcajero,compte,consecpte,fecApertura,TiempoClave,TipoTrans,Nombre,estado) values ('" +
                    Cajero.Trim().ToLower() + "','" + Comprobante + "'," + Consecutivo + ",'" + Fecha.ToString(Var.varini.PstForFec) +
                    "'," + Tiempo + "," + TipoTrans + ",'" + nomusu + "','" + estado + "')";
            }
            else
            {
                stmysql = "update dep_cajeros set TipoTrans=" + TipoTrans +
                    " where Codcajero = '" + Cajero.ToLower() + "' and compte  = '" + Comprobante + "' and ConseCpte = " + Consecutivo;
            }
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabarCajero");
            return ok;
        }

        // ----------------------------------------------------------------
        // CerrarCajero
        // ----------------------------------------------------------------
        public bool CerrarCajero(string Cajero, string Comprobante, int Consecutivo, OdbcConnection conect)
        {
            stmysql = "update dep_cajeros set estado = 'C' where Codcajero = '" + Cajero.ToLower() + "' and compte  = '" + Comprobante + "' and ConseCpte = " + Consecutivo;
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "CerrarCajero");
            if (ok)
            {
                stmysql = "update cop_docmto set CERRADO = 'Y' WHERE COMPRONTE = '" + Comprobante + "' AND NUMERO_DOMTO = " + Consecutivo;
                ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "CerrarCajero");
            }
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabarChequeCanje
        // ----------------------------------------------------------------
        public bool GrabarChequeCanje(double Cuenta, int Cheque, DateTime Fecha, int DiasCanje, DateTime Fecvence,
            string plaza, string Banco, double Valor, int TipoCanje, OdbcConnection conect)
        {
            return GrabarChequeCanje(Cuenta, Cheque, Fecha, DiasCanje, Fecvence, plaza, Banco, Valor, TipoCanje, conect, "C", "9999", 0, "");
        }
        public bool GrabarChequeCanje(double Cuenta, int Cheque, DateTime Fecha, int DiasCanje, DateTime Fecvence,
            string plaza, string Banco, double Valor, int TipoCanje, OdbcConnection conect,
            string Estado, string Compronte, double ConseCompronte, string usuario)
        {
            stmysql = "insert into dep_checanje(num_cuenta,numcheque,fecingreso,diasCanje,fecvence,plaza,banco,valor,estado,TipoCanje,usuario)" +
                " values(" + Cuenta + "," + Cheque + ",'" + Fecha.ToString(Var.varini.PstForFec) + "'," + DiasCanje +
                ",'" + Fecvence.ToString(Var.varini.PstForFec) + "','" + plaza + "'," + Banco + "," + Valor + ",'" + Estado + "','" + TipoCanje + "','" + usuario + "')";
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabarChequeCanje");
            if (ok)
                GrabaFormapagoCheque(Compronte, ConseCompronte, Cuenta.ToString(), Cheque, Banco, Valor, conect, usuario);
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabaFormapagoCheque
        // ----------------------------------------------------------------
        private bool GrabaFormapagoCheque(string comprobante, double ConseComprobante, string Cuenta, int Cheque, string Banco, double Valor,
            OdbcConnection myconnectParam, string usuario)
        {
            string sql;
            sql = "insert into sys_forpago_Cheq(compronte, numero_domto, cheque ,nro_cheque ,nro_cuenta ,banco,usuario)" +
                " values ('" + comprobante + "'," + ConseComprobante + "," + Valor + ",'" + Cheque + "','" + Cuenta + "','" + Banco + "','" + usuario + "')";
            CargaVarini.ExecuteQueryconec(sql, myconnectParam, "GrabaFormapagoCheque");
            return true;
        }

        // ----------------------------------------------------------------
        // ValidaConsecutivoDepositos
        // ----------------------------------------------------------------
        public bool ValidaConsecutivoDepositos(string Compania, ref double Consecutivo, OdbcConnection conect)
        {
            double Conse = 0;
            bool FueAsignado = false;
            stmysql = " select consedep as campo1 from sys_compania where codigo = '" + Compania + "'";
            string conse_str = Conse.ToString();
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "ConsecutivoDepositos", ref conse_str);
            if (conse_str != null && Information.IsNumeric(conse_str))
                Conse = Convert.ToDouble(conse_str);
            if (Consecutivo <= Conse)
            {
                FueAsignado = true;
                Consecutivo = Conse + 1;
            }
            else
            {
                FueAsignado = false;
            }
            return FueAsignado;
        }

        // ----------------------------------------------------------------
        // ConsecutivoDepositos
        // ----------------------------------------------------------------
        public bool ConsecutivoDepositos(string Compania, ref double Consecutivo, OdbcConnection conect)
        {
            return ConsecutivoDepositos(Compania, ref Consecutivo, conect, false);
        }
        public bool ConsecutivoDepositos(string Compania, ref double Consecutivo, OdbcConnection conect, bool Actualiza)
        {
            if (!Actualiza)
            {
                double Conse = 0;
                stmysql = " select consedep as campo1 from sys_compania where codigo = '" + Compania + "'";
                string conse_str = Conse.ToString();
                ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "ConsecutivoDepositos", ref conse_str);
                if (conse_str != null && Information.IsNumeric(conse_str))
                    Conse = Convert.ToDouble(conse_str);
                Consecutivo = Conse + 1;
            }
            else
            {
                stmysql = " update sys_compania set consedep = " + Consecutivo + " where codigo = '" + Compania + "'";
                ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "ConsecutivoDepositos");
            }
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabarCuentaAhorro
        // ----------------------------------------------------------------
        public bool GrabarCuentaAhorro(double Cuenta, OdbcConnection conect, string Codigoter, int Lincred, DateTime fec_crea,
            string repres_legal, string nom_repres, string dir_cial, string Tel1, string Cel,
            string Estado, DateTime fec_exep, string DebitoAuto, DateTime fec_pri_desc, int Tipo_descu,
            string periocidad, string Ciclo, string sello, string protector, string firmas_reg, string firmas_reque,
            string nit_firma1, string nit_firma2, string nit_firma3, string nombre_firma1, string nombre_firma2, string nombre_firma3,
            string nit_benef1, string nit_benef2, string nit_benef3, string nit_benef4, string nit_benef5,
            string nom_benef1, string nom_benef2, string nom_benef3, string nom_benef4, string nom_benef5,
            string porcen_benef1, string porcen_benef2, string porcen_benef3, string porcen_benef4, string porcen_benef5,
            string Excenta, string usuario, string nomusu, DateTime FECVEMTO, int tipocuenta)
        {
            if (!BuscarCuentaAhorro(ref Cuenta, conect))
            {
                // stmysql = "insert into dep_maeahor(num_cuenta,codigoter,lincred,fec_crea,rep_legal,nom_repres,dir_comerci,Telefono,celular,estado,fec_exepcion,debito_automat,fec_primer_des,tipo_descu,periodicidad,ciclo,sello,protector,firmas_regis,firmas_reque,cc_nit_firmareq1,cc_nit_firmareq2,cc_nit_firmareq3,nom_firmareq1,nom_firmareq2,nom_firmareq3," + // ERROR: CS1061
                    // " cc_nit_benefi1, cc_nit_benefi2,cc_nit_benefi3, cc_nit_benefi4, cc_nit_benefi5, nom_benefi1, nom_benefi2, nom_benefi3, nom_benefi4, nom_benefi5, porcen_benefi1, porcen_benefi2, porcen_benefi3, porcen_benefi4, porcen_benefi5,Excenta,usuario,nomusu,fechasys,FECVEMTO,tipocuenta)" + // ERROR: CS1061
                    // " values (" + Cuenta + ",'" + Codigoter + "'," + Lincred + ",'" + fec_crea.ToString(Var.varini.PstForFec) + "','" + Strings.Right("00000000000000" + repres_legal, 14) + "','" + nom_repres + "','" + dir_cial + "','" + Tel1 + "','" + Cel + "','" + // ERROR: CS1061
                    // Estado + "','" + fec_exep.ToString(Var.varini.PstForFec) + "','" + DebitoAuto + "','" + fec_pri_desc.ToString(Var.varini.PstForFec) + "'," + Tipo_descu + ",'" + periocidad + "','" + Ciclo + "','" + sello + "','" + protector + "','" + firmas_reg + "','" + firmas_reque + // ERROR: CS1061
                    // "','" + nit_firma1 + "','" + nit_firma2 + "','" + nit_firma3 + "','" + nombre_firma1 + "','" + nombre_firma2 + "','" + nombre_firma3 + "','" + nit_benef1 + "','" + nit_benef2 + "','" + nit_benef3 + "','" + nit_benef4 + "','" + nit_benef5 + "','" + nom_benef1 + // ERROR: CS1061
                    // "','" + nom_benef2 + "','" + nom_benef3 + "','" + nom_benef4 + "','" + nom_benef5 + "','" + porcen_benef1 + "','" + porcen_benef2 + "','" + porcen_benef3 + "','" + porcen_benef4 + "','" + porcen_benef5 + "','" + Excenta + "','" + usuario + "','" + nomusu + "','" + DateTime.Now.ToString(Var.varini.PstForfecyHora) + "','" + FECVEMTO.ToString(Var.varini.PstForFec) + "','" + tipocuenta + "')"; // ERROR: CS1061
            }
            else
            {
                // stmysql = "update dep_maeahor set codigoter = '" + Codigoter + "',lincred = " + Lincred + ",fec_crea ='" + fec_crea.ToString(Var.varini.PstForFec) + "', rep_legal = '" + Strings.Right("00000000000000" + repres_legal, 14) + "',nom_repres = '" + nom_repres + "',dir_comerci = '" + dir_cial + "',Telefono= '" + Tel1 + "',celular = '" + Cel + // ERROR: CS1061
                    // "',estado = '" + Estado + "',fec_exepcion= '" + fec_exep.ToString(Var.varini.PstForFec) + "',debito_automat = '" + DebitoAuto + "',fec_primer_des = '" + fec_pri_desc.ToString(Var.varini.PstForFec) + "',tipo_descu = " + Tipo_descu + ",periodicidad = '" + periocidad + "',ciclo= '" + Ciclo + "',sello = '" + sello + // ERROR: CS1061
                    // "',protector = '" + protector + "', firmas_regis = '" + firmas_reg + "',firmas_reque = '" + firmas_reque + "',cc_nit_firmareq1 = '" + nit_firma1 + "',cc_nit_firmareq2 = '" + nit_firma2 + "',cc_nit_firmareq3 = '" + nit_firma3 + "',nom_firmareq1 = '" + nombre_firma1 + "',nom_firmareq2= '" + nombre_firma2 + "',nom_firmareq3 = '" + nombre_firma3 + // ERROR: CS1061
                    // "',cc_nit_benefi1 = '" + nit_benef1 + "', cc_nit_benefi2 = '" + nit_benef2 + "',cc_nit_benefi3 = '" + nit_benef3 + "', cc_nit_benefi4 = '" + nit_benef4 + "', cc_nit_benefi5 = '" + nit_benef5 + "',nom_benefi1 = '" + nom_benef1 + "', nom_benefi2 = '" + nom_benef2 + "',nom_benefi3 = '" + nom_benef3 + "', nom_benefi4 = '" + nom_benef4 + // ERROR: CS1061
                    // "', nom_benefi5 = '" + nom_benef5 + "', porcen_benefi1 = '" + porcen_benef1 + "', porcen_benefi2 = '" + porcen_benef2 + "', porcen_benefi3 = '" + porcen_benef3 + "', porcen_benefi4 = '" + porcen_benef4 + "', porcen_benefi5 = '" + porcen_benef5 + "', Excenta = '" + Excenta + "', usuario = '" + usuario + "', nomusu = '" + nomusu + // ERROR: CS1061
                    // "',fechasys='" + DateTime.Now.ToString(Var.varini.PstForfecyHora) + "',FECVEMTO='" + FECVEMTO.ToString(Var.varini.PstForFec) + "',tipocuenta='" + tipocuenta + "'  where num_cuenta = " + Cuenta; // ERROR: CS1061
            }
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabarCuentaAhorro");
            if (Estado == "2" || Estado == "3")
            {
                stmysql = "update dep_maeahor set fec_novedad = '" + DateTime.Now.Date.ToString(Var.varini.PstForFec) + "' where num_cuenta = " + Cuenta;
                ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabarCuentaAhorro");
            }
            return ok;
        }

        // ----------------------------------------------------------------
        // CargaCuentasAhorro
        // ----------------------------------------------------------------
        public bool CargaCuentasAhorro(string Codigoter, OdbcConnection conect)
        {
            DataGridView Grilla = null;
            return CargaCuentasAhorro(Codigoter, conect, ref Grilla);
        }
        public bool CargaCuentasAhorro(string Codigoter, OdbcConnection conect, ref DataGridView Grilla)
        {
            DataSet ds = new DataSet("Datos");
            Codigoter = Strings.Right("000000000000000" + Codigoter, 14);
            stmysql = "Select a.num_cuenta as num_cuenta,a.lincred as Linea," +
                "b.nombre as Descripcion,a.excenta from dep_maeahor a inner join cop_ahorro58 b on b.lincred = a.lincred where a.codigoter = '" + Codigoter + "' and a.estado not in ('2','3')";
            CargaVarini.ExecuteQueryDataset(stmysql, conect, "CargaCuentasAhorro", ref ds, "Cuentas");
            if (ds.Tables["Cuentas"].Rows.Count > 0)
            {
                ok = true;
                if (Grilla != null)
                {
                    Grilla.AutoGenerateColumns = false;
                    Grilla.DataSource = ds.Tables[0];
                }
            }
            else
            {
                ok = false;
            }
            return ok;
        }

        // ----------------------------------------------------------------
        // AyudaCuentas
        // ----------------------------------------------------------------
        public bool AyudaCuentas(string Codigoter, ref double Cuenta, ref int Linea, OdbcConnection conect)
        {
            bool GrabaCreditos = false;
            double ValorDesembolso = 0;
            bool BuscarCuenta = false;
            return AyudaCuentas(Codigoter, ref Cuenta, ref Linea, conect, GrabaCreditos, ref ValorDesembolso, ref BuscarCuenta);
        }
        public bool AyudaCuentas(string Codigoter, ref double Cuenta, ref int Linea, OdbcConnection conect,
            bool GrabaCreditos, ref double ValorDesembolso, ref bool BuscarCuenta)
        {
            // AyudaCuen ayu = new AyudaCuen(conect); // ERROR: CS0246
            // ayu.Codigoter = Codigoter; // ERROR: CS0103
            // if (GrabaCreditos) // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                // ayu.GbxCredito.Visible = true; // ERROR: CS0103
            // else if (BuscarCuenta) // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                // ayu.GrbCuenta.Visible = true; // ERROR: CS0103
            // else
                // ayu.GbxCredito.Visible = false; // ERROR: CS0103
            // ayu.TxtValor.Text = ValorDesembolso.ToString(); // ERROR: CS0103
            // ayu.ShowDialog(); // ERROR: CS0103
            Linea = Var.ResLinea;
            Cuenta = Var.ResCuenta;
            ValorDesembolso = Var.ResValorAcuenta;
            if (Linea != 0 && Cuenta != 0)
                return true;
            else
                return false;
        }

        // ----------------------------------------------------------------
        // BuscarCuentaAhorro - minimal overload (just Cuenta + conect)
        // ----------------------------------------------------------------
        public bool BuscarCuentaAhorro(ref double Cuenta, OdbcConnection conect)
        {
            string Codigoter = ""; int Lincred = 9999;
            DateTime fec_crea = new DateTime(1950,1,1), fec_exep = new DateTime(1950,1,1), fec_pri_desc = new DateTime(1950,1,1);
            string repres_legal = "", nom_repres = "", dir_cial = " ", Tel1 = " ", Cel = " ", Estado = " ";
            bool DebitoAuto = false; int Tipo_descu = 0; int periocidad = 0; string Ciclo = " "; bool sello = false; bool protector = false;
            string firmas_reg = " ", firmas_reque = " ";
            string nit_firma1 = " ", nit_firma2 = " ", nit_firma3 = " ", nombre_firma1 = " ", nombre_firma2 = " ", nombre_firma3 = " ";
            string nit_benef1 = " ", nit_benef2 = " ", nit_benef3 = " ", nit_benef4 = " ", nit_benef5 = " ";
            string nom_benef1 = " ", nom_benef2 = " ", nom_benef3 = " ", nom_benef4 = " ", nom_benef5 = " ";
            string porcen_benef1 = " ", porcen_benef2 = " ", porcen_benef3 = " ", porcen_benef4 = " ", porcen_benef5 = " ";
            bool CuentaExcenta = false;
            DateTime fechasys = new DateTime(1950,1,1), fec_causacion = new DateTime(1950,1,1), FECVEMTO = new DateTime(1950,1,1);
            int tipocuenta = 0;
            return BuscarCuentaAhorro(ref Cuenta, conect, Navega.Ninguno,
                ref Codigoter, ref Lincred, ref fec_crea, ref repres_legal, ref nom_repres, ref dir_cial, ref Tel1, ref Cel,
                ref Estado, ref fec_exep, ref DebitoAuto, ref fec_pri_desc, ref Tipo_descu, ref periocidad, ref Ciclo,
                ref sello, ref protector, ref firmas_reg, ref firmas_reque,
                ref nit_firma1, ref nit_firma2, ref nit_firma3, ref nombre_firma1, ref nombre_firma2, ref nombre_firma3,
                ref nit_benef1, ref nit_benef2, ref nit_benef3, ref nit_benef4, ref nit_benef5,
                ref nom_benef1, ref nom_benef2, ref nom_benef3, ref nom_benef4, ref nom_benef5,
                ref porcen_benef1, ref porcen_benef2, ref porcen_benef3, ref porcen_benef4, ref porcen_benef5,
                ref CuentaExcenta, ref fechasys, ref fec_causacion, ref FECVEMTO, ref tipocuenta);
        }

        // BuscarCuentaAhorro with Navega + Codigoter + Lincred (used in ProcesoDebitoAutomatico)
        public bool BuscarCuentaAhorro(object Cuenta_obj, OdbcConnection conect, Navega Navegar,
            object codigoter_obj, ref int Lincred, ref string _u1, ref string _u2,
            ref string _u3, ref string _u4, ref string _u5, ref string _u6, ref string Estado)
        {
            double Cuenta = Convert.ToDouble(Cuenta_obj);
            string Codigoter = codigoter_obj.ToString();
            DateTime fec_crea = new DateTime(1950,1,1), fec_exep = new DateTime(1950,1,1), fec_pri_desc = new DateTime(1950,1,1);
            string repres_legal = "", nom_repres = "", dir_cial = " ", Tel1 = " ", Cel = " ";
            bool DebitoAuto = false; int Tipo_descu = 0; int periocidad = 0; string Ciclo = " "; bool sello = false; bool protector = false;
            string firmas_reg = " ", firmas_reque = " ";
            string nit_firma1 = " ", nit_firma2 = " ", nit_firma3 = " ", nombre_firma1 = " ", nombre_firma2 = " ", nombre_firma3 = " ";
            string nit_benef1 = " ", nit_benef2 = " ", nit_benef3 = " ", nit_benef4 = " ", nit_benef5 = " ";
            string nom_benef1 = " ", nom_benef2 = " ", nom_benef3 = " ", nom_benef4 = " ", nom_benef5 = " ";
            string porcen_benef1 = " ", porcen_benef2 = " ", porcen_benef3 = " ", porcen_benef4 = " ", porcen_benef5 = " ";
            bool CuentaExcenta = false;
            DateTime fechasys = new DateTime(1950,1,1), fec_causacion = new DateTime(1950,1,1), FECVEMTO = new DateTime(1950,1,1);
            int tipocuenta = 0;
            return BuscarCuentaAhorro(ref Cuenta, conect, Navegar,
                ref Codigoter, ref Lincred, ref fec_crea, ref repres_legal, ref nom_repres, ref dir_cial, ref Tel1, ref Cel,
                ref Estado, ref fec_exep, ref DebitoAuto, ref fec_pri_desc, ref Tipo_descu, ref periocidad, ref Ciclo,
                ref sello, ref protector, ref firmas_reg, ref firmas_reque,
                ref nit_firma1, ref nit_firma2, ref nit_firma3, ref nombre_firma1, ref nombre_firma2, ref nombre_firma3,
                ref nit_benef1, ref nit_benef2, ref nit_benef3, ref nit_benef4, ref nit_benef5,
                ref nom_benef1, ref nom_benef2, ref nom_benef3, ref nom_benef4, ref nom_benef5,
                ref porcen_benef1, ref porcen_benef2, ref porcen_benef3, ref porcen_benef4, ref porcen_benef5,
                ref CuentaExcenta, ref fechasys, ref fec_causacion, ref FECVEMTO, ref tipocuenta);
        }

        // BuscarCuentaAhorro string Cuenta + conect (from Grabamovimiento)
        public bool BuscarCuentaAhorro(string CuentaStr, OdbcConnection conect, Navega Navegar, ref string Codigoter, ref string lincredStr)
        {
            double Cuenta = Information.IsNumeric(CuentaStr) ? Convert.ToDouble(CuentaStr) : 0;
            int lincred = Information.IsNumeric(lincredStr) ? Convert.ToInt32(lincredStr) : 9999;
            DateTime fec_crea = new DateTime(1950,1,1), fec_exep = new DateTime(1950,1,1), fec_pri_desc = new DateTime(1950,1,1);
            string repres_legal = "", nom_repres = "", dir_cial = " ", Tel1 = " ", Cel = " ", Estado = " ";
            bool DebitoAuto = false; int Tipo_descu = 0; int periocidad = 0; string Ciclo = " "; bool sello = false; bool protector = false;
            string firmas_reg = " ", firmas_reque = " ";
            string nit_firma1 = " ", nit_firma2 = " ", nit_firma3 = " ", nombre_firma1 = " ", nombre_firma2 = " ", nombre_firma3 = " ";
            string nit_benef1 = " ", nit_benef2 = " ", nit_benef3 = " ", nit_benef4 = " ", nit_benef5 = " ";
            string nom_benef1 = " ", nom_benef2 = " ", nom_benef3 = " ", nom_benef4 = " ", nom_benef5 = " ";
            string porcen_benef1 = " ", porcen_benef2 = " ", porcen_benef3 = " ", porcen_benef4 = " ", porcen_benef5 = " ";
            bool CuentaExcenta = false;
            DateTime fechasys = new DateTime(1950,1,1), fec_causacion = new DateTime(1950,1,1), FECVEMTO = new DateTime(1950,1,1);
            int tipocuenta = 0;
            bool result = BuscarCuentaAhorro(ref Cuenta, conect, Navegar,
                ref Codigoter, ref lincred, ref fec_crea, ref repres_legal, ref nom_repres, ref dir_cial, ref Tel1, ref Cel,
                ref Estado, ref fec_exep, ref DebitoAuto, ref fec_pri_desc, ref Tipo_descu, ref periocidad, ref Ciclo,
                ref sello, ref protector, ref firmas_reg, ref firmas_reque,
                ref nit_firma1, ref nit_firma2, ref nit_firma3, ref nombre_firma1, ref nombre_firma2, ref nombre_firma3,
                ref nit_benef1, ref nit_benef2, ref nit_benef3, ref nit_benef4, ref nit_benef5,
                ref nom_benef1, ref nom_benef2, ref nom_benef3, ref nom_benef4, ref nom_benef5,
                ref porcen_benef1, ref porcen_benef2, ref porcen_benef3, ref porcen_benef4, ref porcen_benef5,
                ref CuentaExcenta, ref fechasys, ref fec_causacion, ref FECVEMTO, ref tipocuenta);
            lincredStr = lincred.ToString();
            return result;
        }

        // BuscarCuentaAhorro - full 47-parameter version
        public bool BuscarCuentaAhorro(ref double Cuenta, OdbcConnection conect, Navega Navegar,
            ref string Codigoter, ref int Lincred, ref DateTime fec_crea, ref string repres_legal,
            ref string nom_repres, ref string dir_cial, ref string Tel1, ref string Cel,
            ref string Estado, ref DateTime fec_exep, ref bool DebitoAuto, ref DateTime fec_pri_desc,
            ref int Tipo_descu, ref int periocidad, ref string Ciclo, ref bool sello, ref bool protector,
            ref string firmas_reg, ref string firmas_reque, ref string nit_firma1, ref string nit_firma2,
            ref string nit_firma3, ref string nombre_firma1, ref string nombre_firma2, ref string nombre_firma3,
            ref string nit_benef1, ref string nit_benef2, ref string nit_benef3, ref string nit_benef4, ref string nit_benef5,
            ref string nom_benef1, ref string nom_benef2, ref string nom_benef3, ref string nom_benef4, ref string nom_benef5,
            ref string porcen_benef1, ref string porcen_benef2, ref string porcen_benef3, ref string porcen_benef4, ref string porcen_benef5,
            ref bool CuentaExcenta, ref DateTime fechasys, ref DateTime fec_causacion, ref DateTime FECVEMTO, ref int tipocuenta)
        {
            string debauto = "", llesello = "", lleprotec = "", periodi = "1", Excenta = "N";
            string where = " ";
            string fecha = DateTime.Now.ToShortDateString();
            string fec_causaciontemp = new DateTime(1950, 1, 1).ToString();
            string FECVEMTOTEM = new DateTime(1950, 1, 1).ToString();

            if (Navegar == Navega.Ninguno)
            {
                if (Cuenta.ToString() == "0") { Cuenta = 0; return false; }
            }
            if (Navegar == Navega.Ninguno)
                where = " from dep_maeahor where Num_cuenta  = '" + Cuenta + "'";
            else if (Navegar == Navega.Primero)
                where = " from dep_maeahor where Num_cuenta  > ' '" + " order by Num_cuenta " + Var.varini.Pstlimit;
            else if (Navegar == Navega.Anterior)
                where = " from dep_maeahor where Num_cuenta  < '" + Cuenta + "' order by Num_cuenta desc " + Var.varini.Pstlimit;
            else if (Navegar == Navega.Siguiente)
                where = " from dep_maeahor where Num_cuenta  > '" + Cuenta + "' order by Num_cuenta " + Var.varini.Pstlimit;
            else if (Navegar == Navega.Ultimo)
                where = " from dep_maeahor where Num_cuenta  <= '9999'" + " order by Num_cuenta desc " + Var.varini.Pstlimit;

            string c1, c2, c3, c4;
            c1 = Cuenta.ToString(); c2 = Codigoter; c3 = Lincred.ToString(); c4 = fec_crea.ToString();
            stmysql = "Select  " + Var.varini.Psttop + "  num_cuenta as campo1,codigoter as campo2,lincred as campo3,fec_crea as campo4";
            ok = CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (ok) { if (Information.IsNumeric(c1)) Cuenta = Convert.ToDouble(c1); Codigoter = c2; if (Information.IsNumeric(c3)) Lincred = Convert.ToInt32(c3); if (Information.IsDate(c4)) fec_crea = Convert.ToDateTime(c4); }

            c1 = repres_legal; c2 = nom_repres; c3 = dir_cial; c4 = Tel1;
            stmysql = "Select  " + Var.varini.Psttop + "  rep_legal as campo1,nom_repres as campo2,dir_comerci as campo3,Telefono as campo4";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            repres_legal = c1; nom_repres = c2; dir_cial = c3; Tel1 = c4;

            c1 = Cel; c2 = Estado; c3 = fecha; c4 = debauto;
            stmysql = "Select  " + Var.varini.Psttop + "  celular as campo1,estado as campo2,fec_exepcion as campo3,debito_automat as campo4";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            Cel = c1; Estado = c2; fecha = c3; debauto = c4;
            if (Information.IsDate(fecha)) fec_exep = Convert.ToDateTime(fecha);

            c1 = fec_pri_desc.ToString(); c2 = Tipo_descu.ToString(); c3 = periodi; c4 = Ciclo;
            stmysql = "Select  " + Var.varini.Psttop + "  fec_primer_des as campo1,tipo_descu as campo2,periodicidad as campo3,ciclo as campo4";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsDate(c1)) fec_pri_desc = Convert.ToDateTime(c1); if (Information.IsNumeric(c2)) Tipo_descu = Convert.ToInt32(c2); periodi = c3; Ciclo = c4;

            c1 = llesello; c2 = lleprotec; c3 = firmas_reg; c4 = firmas_reque;
            stmysql = "Select  " + Var.varini.Psttop + "  sello as campo1,protector as campo2,firmas_regis as campo3,firmas_reque as campo4";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            llesello = c1; lleprotec = c2; firmas_reg = c3; firmas_reque = c4;

            c1 = nit_firma1; c2 = nit_firma2; c3 = nit_firma3; c4 = nombre_firma1;
            stmysql = "Select  " + Var.varini.Psttop + "  cc_nit_firmareq1 as campo1,cc_nit_firmareq2 as campo2,cc_nit_firmareq3 as campo3,nom_firmareq1 as campo4";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            nit_firma1 = c1; nit_firma2 = c2; nit_firma3 = c3; nombre_firma1 = c4;

            c1 = nombre_firma2; c2 = nombre_firma3; c3 = nit_benef1; c4 = nit_benef2;
            stmysql = "Select  " + Var.varini.Psttop + "  nom_firmareq2 as campo1,nom_firmareq3 as campo2,cc_nit_benefi1 as campo3, cc_nit_benefi2 as campo4 ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            nombre_firma2 = c1; nombre_firma3 = c2; nit_benef1 = c3; nit_benef2 = c4;

            c1 = nit_benef3; c2 = nit_benef4; c3 = nit_benef5; c4 = nom_benef1;
            stmysql = "Select  " + Var.varini.Psttop + "  cc_nit_benefi3 as campo1, cc_nit_benefi4 as campo2, cc_nit_benefi5 as campo3, nom_benefi1 as campo4 ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            nit_benef3 = c1; nit_benef4 = c2; nit_benef5 = c3; nom_benef1 = c4;

            c1 = nom_benef2; c2 = nom_benef3; c3 = nom_benef4; c4 = nom_benef5;
            stmysql = "Select  " + Var.varini.Psttop + "  nom_benefi2 as campo1, nom_benefi3 as campo2, nom_benefi4 as campo3, nom_benefi5 as campo4 ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            nom_benef2 = c1; nom_benef3 = c2; nom_benef4 = c3; nom_benef5 = c4;

            c1 = porcen_benef1; c2 = porcen_benef2; c3 = porcen_benef3; c4 = porcen_benef4;
            stmysql = "Select  " + Var.varini.Psttop + "  porcen_benefi1 as campo1, porcen_benefi2 as campo2, porcen_benefi3 as campo3, porcen_benefi4 as campo4 ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3, ref c4);
            porcen_benef1 = c1; porcen_benef2 = c2; porcen_benef3 = c3; porcen_benef4 = c4;

            c1 = porcen_benef5; c2 = Excenta; c3 = fechasys.ToString(); c4 = null;
            stmysql = "Select " + Var.varini.Psttop + "   porcen_benefi5 as campo1 ,excenta as campo2,fechasys as campo3  ";
            // CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2, ref c3); // ERROR: CS1501
            porcen_benef5 = c1; Excenta = c2; if (Information.IsDate(c3)) fechasys = Convert.ToDateTime(c3);

            c1 = FECVEMTOTEM; c2 = tipocuenta.ToString(); c3 = null; c4 = null;
            stmysql = "Select " + Var.varini.Psttop + "  FECVEMTO as campo1,tipocuenta as campo2   ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscarCuentaAhorro", ref c1, ref c2);
            FECVEMTOTEM = c1; if (Information.IsNumeric(c2)) tipocuenta = Convert.ToInt32(c2);

            if (ok)
            {
                stmysql = "Select " + Var.varini.Psttop + "  FECCIERRE as campo1  from cop_maecar where  codigoter='" + Codigoter + "' and lincred=" + Lincred + " and NUMERO=" + Cuenta;
                c1 = fec_causaciontemp;
                CargaVarini.ExecuteQueryconec(stmysql, conect, "BuscarCuentaAhorro", ref c1);
                fec_causaciontemp = c1;
            }

            CuentaExcenta = (Excenta == "Y");

            switch (debauto)
            {
                case "Y": DebitoAuto = true; break;
                default: DebitoAuto = false; break;
            }
            switch (Tipo_descu.ToString())
            {
                case "1": Tipo_descu = 0; break;
                case "2": Tipo_descu = 1; break;
            }
            sello = (llesello == "Y");
            protector = (lleprotec == "Y");
            switch (periodi)
            {
                case "1": periocidad = 0; break;
                case "2": periocidad = 1; break;
                case "3": periocidad = 2; break;
                case "4": periocidad = 3; break;
                case "5": periocidad = 4; break;
            }
            fec_causacion = Information.IsDate(fec_causaciontemp) ? Convert.ToDateTime(fec_causaciontemp) : new DateTime(1950, 1, 1);
            FECVEMTO = Information.IsDate(FECVEMTOTEM) ? Convert.ToDateTime(FECVEMTOTEM) : new DateTime(1950, 1, 1);
            return ok;
        }

        // ----------------------------------------------------------------
        // EliminarCuentaAhorro
        // ----------------------------------------------------------------
        public bool EliminarCuentaAhorro(ref double Cuenta, OdbcConnection conect)
        {
            stmysql = "Delete from dep_maeahor where Num_cuenta  = '" + Cuenta + "'";
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "EliminarCuentaAhorro");
            return ok;
        }

        // ----------------------------------------------------------------
        // ConsecutivoXLineaAhorro
        // ----------------------------------------------------------------
        public bool ConsecutivoXLineaAhorro(ref int lincred, OdbcConnection conect)
        {
            double Consecutivo = 0;
            bool Actualiza = false;
            return ConsecutivoXLineaAhorro(ref lincred, conect, ref Consecutivo, Actualiza);
        }
        public bool ConsecutivoXLineaAhorro(ref int lincred, OdbcConnection conect, ref double Consecutivo, bool Actualiza)
        {
            if (Actualiza)
            {
                stmysql = "update cop_ahorro58  set Consecutivo =" + Consecutivo + " where lincred = " + lincred;
                ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "ConsecutivoLineaAhorro");
            }
            else
            {
                stmysql = "select Consecutivo as campo1 from cop_ahorro58 where lincred  = " + lincred;
                string consec_str = Consecutivo.ToString();
                ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "ConsecutivoLineaAhorro", ref consec_str);
                if (Information.IsNumeric(consec_str)) Consecutivo = Convert.ToDouble(consec_str);
                Consecutivo += 1;
            }
            return ok;
        }

        // ----------------------------------------------------------------
        // BuscaLineaAhorro - full version (many overloads via helper)
        // ----------------------------------------------------------------
        public bool BuscaLineaAhorro(ref int lincred, OdbcConnection conect)
        {
            string NOMBRE = "", NOMBRE_RESUM = ""; int PERPAGO_INT = 0;
            double SALMIN_INT = 0, VALMIT_TRAN = 0, SALMIN_CUENTA = 0, PORPAGO_INT = 0, VLR_MIN_RFTE = 0;
            double POR_RFTE = 0, gravamen = 0, TOPE_4XMIL = 0, maxefectivo = 0, Consecutivo = 0, tope_retiro = 0;
            int DIAS_CANJE = 0, FORLIQ = 0, DIAS_GRACIA = 0, FORMA_PAG = 0, RetCheGmf = 0, CptoInteres = 0, fuente = 0, DiasCanjeOtras = 0, Equisuper = 0;
            double VALOR_MAX_RET = 0;
            string CPTO_INTERES = "", CPTO_RFTE = "", FORAPLI_4XMIL = "", CPTO_4MIL = "", CBTE_4XMIL = "", COMENTARIO = "";
            string FormatoDian = "", valida_retiro = "N", manajaformaPAP = "N", CuentaTesoreria = " ";
            return BuscaLineaAhorro(ref lincred, conect, Navega.Ninguno,
                ref NOMBRE, ref NOMBRE_RESUM, ref PERPAGO_INT, ref SALMIN_INT, ref VALMIT_TRAN, ref SALMIN_CUENTA, ref PORPAGO_INT, ref VLR_MIN_RFTE,
                ref POR_RFTE, ref DIAS_CANJE, ref FORLIQ, ref DIAS_GRACIA, ref VALOR_MAX_RET, ref CPTO_INTERES, ref CPTO_RFTE, ref FORMA_PAG, ref gravamen,
                ref FORAPLI_4XMIL, ref CPTO_4MIL, ref TOPE_4XMIL, ref CBTE_4XMIL, ref COMENTARIO, ref maxefectivo, ref Consecutivo, ref RetCheGmf, ref CptoInteres,
                ref FormatoDian, ref fuente, ref DiasCanjeOtras, ref valida_retiro, ref tope_retiro, ref manajaformaPAP, ref CuentaTesoreria, ref Equisuper);
        }

        public bool BuscaLineaAhorro(ref int lincred, OdbcConnection conect, Navega Navegar,
            ref string NOMBRE, ref string NOMBRE_RESUM, ref int PERPAGO_INT, ref double SALMIN_INT, ref double VALMIT_TRAN,
            ref double SALMIN_CUENTA, ref double PORPAGO_INT, ref double VLR_MIN_RFTE,
            ref double POR_RFTE, ref int DIAS_CANJE, ref int FORLIQ, ref int DIAS_GRACIA, ref double VALOR_MAX_RET,
            ref string CPTO_INTERES, ref string CPTO_RFTE, ref int FORMA_PAG, ref double gravamen,
            ref string FORAPLI_4XMIL, ref string CPTO_4MIL, ref double TOPE_4XMIL, ref string CBTE_4XMIL,
            ref string COMENTARIO, ref double maxefectivo, ref double Consecutivo,
            ref int RetCheGmf, ref int CptoInteres, ref string FormatoDian, ref int fuente, ref int DiasCanjeOtras,
            ref string valida_retiro, ref double tope_retiro, ref string manajaformaPAP, ref string CuentaTesoreria, ref int Equisuper)
        {
            string where = " ";
            if (Navegar == Navega.Ninguno)
            {
                if (lincred.ToString() == "0") { return false; }
                where = " from cop_ahorro58 where lincred  = " + lincred;
            }
            else if (Navegar == Navega.Primero)
                where = " from cop_ahorro58 where lincred  >  0 order by lincred asc " + Var.varini.Pstlimit;
            else if (Navegar == Navega.Anterior)
                where = " from cop_ahorro58 where lincred  < " + lincred + " order by lincred desc " + Var.varini.Pstlimit;
            else if (Navegar == Navega.Siguiente)
                where = " from cop_ahorro58 where lincred  > " + lincred + " order by lincred  " + Var.varini.Pstlimit;
            else if (Navegar == Navega.Ultimo)
                where = " from cop_ahorro58 where lincred  <= 99999999 order by lincred desc  " + Var.varini.Pstlimit;

            string c1, c2, c3, c4;
            c1 = lincred.ToString(); c2 = NOMBRE; c3 = NOMBRE_RESUM; c4 = PERPAGO_INT.ToString();
            stmysql = "Select " + Var.varini.Psttop + " lincred as campo1, NOMBRE as campo2, NOMBRE_RESUM as campo3, PERPAGO_INT as campo4 ";
            ok = CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) lincred = Convert.ToInt32(c1); NOMBRE = c2; NOMBRE_RESUM = c3; if (Information.IsNumeric(c4)) PERPAGO_INT = Convert.ToInt32(c4);

            c1 = SALMIN_INT.ToString(); c2 = VALMIT_TRAN.ToString(); c3 = SALMIN_CUENTA.ToString(); c4 = PORPAGO_INT.ToString();
            stmysql = "Select " + Var.varini.Psttop + " SALMIN_INT as campo1, VALMIT_TRAN as campo2, SALMIN_CUENTA as campo3, PORPAGO_INT as campo4  ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) SALMIN_INT = Convert.ToDouble(c1); if (Information.IsNumeric(c2)) VALMIT_TRAN = Convert.ToDouble(c2);
            if (Information.IsNumeric(c3)) SALMIN_CUENTA = Convert.ToDouble(c3); if (Information.IsNumeric(c4)) PORPAGO_INT = Convert.ToDouble(c4);

            c1 = VLR_MIN_RFTE.ToString(); c2 = POR_RFTE.ToString(); c3 = DIAS_CANJE.ToString(); c4 = FORLIQ.ToString();
            stmysql = "Select" + Var.varini.Psttop + "  VLR_MIN_RFTE as campo1,  POR_RFTE as campo2, DIAS_CANJE as campo3, FORLIQ as campo4  ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) VLR_MIN_RFTE = Convert.ToDouble(c1); if (Information.IsNumeric(c2)) POR_RFTE = Convert.ToDouble(c2);
            if (Information.IsNumeric(c3)) DIAS_CANJE = Convert.ToInt32(c3); if (Information.IsNumeric(c4)) FORLIQ = Convert.ToInt32(c4);

            c1 = DIAS_GRACIA.ToString(); c2 = VALOR_MAX_RET.ToString(); c3 = CPTO_INTERES; c4 = CPTO_RFTE;
            stmysql = "Select " + Var.varini.Psttop + " DIAS_GRACIA as campo1, VALOR_MAX_RET as campo2, CPTO_INTERES as campo3, CPTO_RFTE as campo4  ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) DIAS_GRACIA = Convert.ToInt32(c1); if (Information.IsNumeric(c2)) VALOR_MAX_RET = Convert.ToDouble(c2);
            CPTO_INTERES = c3; CPTO_RFTE = c4;

            c1 = FORMA_PAG.ToString(); c2 = gravamen.ToString(); c3 = FORAPLI_4XMIL; c4 = CPTO_4MIL;
            stmysql = "Select " + Var.varini.Psttop + " FORMA_PAG as campo1, gravamen as campo2,  FORAPLI_4XMIL as campo3, CPTO_4MIL as campo4  ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) FORMA_PAG = Convert.ToInt32(c1); if (Information.IsNumeric(c2)) gravamen = Convert.ToDouble(c2);
            FORAPLI_4XMIL = c3; CPTO_4MIL = c4;

            c1 = TOPE_4XMIL.ToString(); c2 = CBTE_4XMIL; c3 = COMENTARIO; c4 = maxefectivo.ToString();
            stmysql = "Select " + Var.varini.Psttop + " TOPE_4XMIL as campo1, CBTE_4XMIL as campo2, COMENTARIO as campo3, maxefectivo as campo4  ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) TOPE_4XMIL = Convert.ToDouble(c1); CBTE_4XMIL = c2; COMENTARIO = c3; if (Information.IsNumeric(c4)) maxefectivo = Convert.ToDouble(c4);

            c1 = Consecutivo.ToString(); c2 = RetCheGmf.ToString(); c3 = CptoInteres.ToString(); c4 = FormatoDian;
            stmysql = "Select " + Var.varini.Psttop + " Consecutivo as campo1, RetCheGmf as campo2,CptoInteres as campo3,idformato as campo4  ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) Consecutivo = Convert.ToDouble(c1); if (Information.IsNumeric(c2)) RetCheGmf = Convert.ToInt32(c2);
            if (Information.IsNumeric(c3)) CptoInteres = Convert.ToInt32(c3); FormatoDian = c4;

            c1 = fuente.ToString(); c2 = DiasCanjeOtras.ToString(); c3 = manajaformaPAP; c4 = CuentaTesoreria;
            stmysql = "Select " + Var.varini.Psttop + " fuente as campo1,DIAS_CANJE_otras as campo2, manejaFormaPAP as campo3,CuentaTesoreria  as campo4";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2, ref c3, ref c4);
            if (Information.IsNumeric(c1)) fuente = Convert.ToInt32(c1); if (Information.IsNumeric(c2)) DiasCanjeOtras = Convert.ToInt32(c2); manajaformaPAP = c3; CuentaTesoreria = c4;

            c1 = valida_retiro; c2 = tope_retiro.ToString();
            stmysql = "Select " + Var.varini.Psttop + "  valida_retiro as campo1 ,tope_retiro as campo2  ";
            CargaVarini.ExecuteQueryconec(stmysql + where, conect, "BuscaLineaAhorro", ref c1, ref c2);
            valida_retiro = c1; if (Information.IsNumeric(c2)) tope_retiro = Convert.ToDouble(c2);

            c1 = Equisuper.ToString();
            stmysql = "Select " + Var.varini.Psttop + " EquiSuper as campo1 FROM cop_concar12 where LINCRED =" + lincred;
            CargaVarini.ExecuteQueryconec(stmysql, conect, "BuscaLineaAhorro", ref c1);
            if (Information.IsNumeric(c1)) Equisuper = Convert.ToInt32(c1);

            return ok;
        }

        // Convenience overload used in LiquidacionInteresesCuentasAhorro
        public bool BuscaLineaAhorro(object lincredObj, OdbcConnection conect, Navega Navegar,
            ref string NOMBRE, ref string NOMBRE_RESUM, ref int PERPAGO_INT, ref double SALMIN_INT, ref double VALMIT_TRAN,
            ref double SALMIN_CUENTA, ref double PORPAGO_INT, ref double VLR_MIN_RFTE,
            ref double POR_RFTE, ref int DIAS_CANJE, ref int FORLIQ, ref int DIAS_GRACIA, ref double VALOR_MAX_RET,
            ref string CPTO_INTERES, ref string CPTO_RFTE, ref int FORMA_PAG, ref double gravamen,
            ref string FORAPLI_4XMIL, ref string CPTO_4MIL, ref double TOPE_4XMIL, ref string CBTE_4XMIL,
            ref string COMENTARIO, ref double maxefectivo, ref double Consecutivo,
            ref int RetCheGmf, ref int CptoInteres, ref string FormatoDian, ref int fuente, ref int DiasCanjeOtras,
            ref string valida_retiro, ref double tope_retiro, ref string manajaformaPAP, ref string CuentaTesoreria, ref int Equisuper)
        {
            int lincred = Information.IsNumeric(lincredObj) ? Convert.ToInt32(lincredObj) : 0;
            return BuscaLineaAhorro(ref lincred, conect, Navegar,
                ref NOMBRE, ref NOMBRE_RESUM, ref PERPAGO_INT, ref SALMIN_INT, ref VALMIT_TRAN,
                ref SALMIN_CUENTA, ref PORPAGO_INT, ref VLR_MIN_RFTE,
                ref POR_RFTE, ref DIAS_CANJE, ref FORLIQ, ref DIAS_GRACIA, ref VALOR_MAX_RET,
                ref CPTO_INTERES, ref CPTO_RFTE, ref FORMA_PAG, ref gravamen,
                ref FORAPLI_4XMIL, ref CPTO_4MIL, ref TOPE_4XMIL, ref CBTE_4XMIL,
                ref COMENTARIO, ref maxefectivo, ref Consecutivo,
                ref RetCheGmf, ref CptoInteres, ref FormatoDian, ref fuente, ref DiasCanjeOtras,
                ref valida_retiro, ref tope_retiro, ref manajaformaPAP, ref CuentaTesoreria, ref Equisuper);
        }

        // ----------------------------------------------------------------
        // GrabarLineaAhorro
        // ----------------------------------------------------------------
        public bool GrabarLineaAhorro(ref int lincred, OdbcConnection conect, ref string NOMBRE, ref string NOMBRE_RESUM,
            ref int PERPAGO_INT, ref double SALMIN_INT, ref double VALMIT_TRAN, ref double SALMIN_CUENTA, ref double PORPAGO_INT,
            ref double VLR_MIN_RFTE, ref double POR_RFTE, ref int DIAS_CANJE, ref int FORLIQ, ref int DIAS_GRACIA,
            ref double VALOR_MAX_RET, ref int FORMA_PAG, ref double gravamen, ref int FORAPLI_4XMIL, ref string CPTO_4MIL,
            ref double TOPE_4XMIL, ref string CBTE_4XMIL, ref string COMENTARIO, ref double maxefectivo, ref double Consecutivo,
            string usuario, int RetCheGmf, int CptoInteres, string FormatoDian, int fuente, int DiasCanjeOtras,
            string valida_retiro, double tope_retiro, string manejaFormaPAP, string CuentaTesoreria)
        {
            string nomusu = " ";
            // paramsys.BuscaUsuario(usuario, conect, ref nomusu); // ERROR: CS1615, CS1620
            int tmpLincred = lincred;
            string NOMBRE_r = NOMBRE, NOMBRE_RESUM_r = NOMBRE_RESUM;
            if (!BuscaLineaAhorro(ref tmpLincred, conect))
            {
                // stmysql = "insert into cop_ahorro58(lincred, nombre, nombre_resum, perpago_int, salmin_int,VALMIT_TRAN, salmin_cuenta," + // ERROR: CS1061
                    // " porpago_int,   vlr_min_rfte,  por_rfte, dias_canje,forliq,dias_gracia, forma_pag," + // ERROR: CS1061
                    // " gravamen, forapli_4xmil, CPTO_4MIL,TOPE_4XMIL, cbte_4xmil,maxefectivo, VALOR_MAX_RET,Consecutivo,usuario,RetCheGmf,nomusu,fechasys,CptoInteres,idformato,fuente,DIAS_CANJE_otras,valida_retiro,tope_retiro,manejaFormaPAP,CuentaTesoreria) values ('" + // ERROR: CS1061
                    // lincred + "','" + NOMBRE + "','" + NOMBRE_RESUM + "','" + PERPAGO_INT + "','" + SALMIN_INT + "','" + VALMIT_TRAN + "','" + // ERROR: CS1061
                    // SALMIN_CUENTA + "','" + PORPAGO_INT + "','" + VLR_MIN_RFTE + "','" + POR_RFTE + "','" + DIAS_CANJE + "','" + FORLIQ + "','" + // ERROR: CS1061
                    // DIAS_GRACIA + "','" + FORMA_PAG + "','" + gravamen + "','" + FORAPLI_4XMIL + "','" + CPTO_4MIL + "','" + TOPE_4XMIL + "','" + // ERROR: CS1061
                    // CBTE_4XMIL + "','" + maxefectivo + "','" + VALOR_MAX_RET + "','" + Consecutivo + "','" + usuario + "','" + RetCheGmf + // ERROR: CS1061
                    // "','" + nomusu + "','" + DateTime.Now.ToString(Var.varini.PstForfecyHora) + "'," + CptoInteres + "," + FormatoDian + "," + fuente + "," + DiasCanjeOtras + ",'" + valida_retiro + "'," + tope_retiro + ",'" + manejaFormaPAP + "','" + CuentaTesoreria + "')"; // ERROR: CS1061
            }
            else
            {
                // stmysql = "update cop_ahorro58  set nombre = '" + NOMBRE + "',nombre_resum ='" + NOMBRE_RESUM + "', perpago_int ='" + PERPAGO_INT + "',salmin_int ='" + SALMIN_INT + // ERROR: CS1061
                    // "',valmit_tran ='" + VALMIT_TRAN + "',salmin_cuenta ='" + SALMIN_CUENTA + "',porpago_int ='" + PORPAGO_INT + "',vlr_min_rfte ='" + VLR_MIN_RFTE + // ERROR: CS1061
                    // "',por_rfte ='" + POR_RFTE + "',dias_canje ='" + DIAS_CANJE + "',forliq ='" + FORLIQ + "',dias_gracia ='" + DIAS_GRACIA + "',forma_pag ='" + FORMA_PAG + // ERROR: CS1061
                    // "',gravamen ='" + gravamen + "',forapli_4xmil ='" + FORAPLI_4XMIL + "',cpto_4mil ='" + CPTO_4MIL + "',tope_4xmil ='" + TOPE_4XMIL + "',cbte_4xmil ='" + // ERROR: CS1061
                    // CBTE_4XMIL + "',VALOR_MAX_RET = " + VALOR_MAX_RET + " ,maxefectivo = " + maxefectivo + " , Consecutivo = " + Consecutivo + ", RetCheGmf = '" + RetCheGmf + // ERROR: CS1061
                    // "', usuario = '" + usuario + "', nomusu = '" + nomusu + "', fechasys = '" + DateTime.Now.ToString(Var.varini.PstForfecyHora) + // ERROR: CS1061
                    // "', CptoInteres=" + CptoInteres + ",idformato=" + FormatoDian + ",fuente=" + fuente + ",DIAS_CANJE_otras=" + DiasCanjeOtras + // ERROR: CS1061
                    // ", valida_retiro='" + valida_retiro + "',tope_retiro=" + tope_retiro + ",manejaFormaPAP='" + manejaFormaPAP + "',CuentaTesoreria='" + CuentaTesoreria + "'  where lincred = " + lincred; // ERROR: CS1061
            }
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabarLineaAhorro");
            return ok;
        }

        // ----------------------------------------------------------------
        // EliminarLineaAhorro
        // ----------------------------------------------------------------
        public bool EliminarLineaAhorro(ref int lincred, OdbcConnection conect)
        {
            stmysql = "delete from cop_ahorro58 where lincred = '" + lincred + "'";
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "EliminarLineaAhorro");
            return ok;
        }

        // ----------------------------------------------------------------
        // ProcesoDebitoAutomatico
        // ----------------------------------------------------------------
        public bool ProcesoDebitoAutomatico(ref string Comprobante, ref double Consecutivo,
            string Documento, DateTime Fecha, OdbcConnection conect,
            string Usuario, ComboBox.ObjectCollection ConcepAdicionales, OpcionesDebAutomatico Opcion,
            Form Pertenece, int RespSalMin, int IncluyeObliCobJur, int ClaseDsto,
            int ProcesoIncluye, int HastaCiclo, string Empresa, string Aplica4x1000, ref DataSet dsdata,
            string SoloExtras, string Periodicidad)
        {
            double TotalPendiente = 0; int canreg = 0; int fila = 0; double saldoextras = 0;
            string controla = "N"; double disponible = 0; double Vlr4x1000 = 0; string Apli4x1000 = "N";

            string WhereEmpresa;
            if (Empresa == "Todas")
                WhereEmpresa = "";
            else
                WhereEmpresa = " and maenit.empresa = '" + Empresa + "'";

            int sw;
            stmysql = " select depctas.num_cuenta,depctas.codigoter,depctas.lincred, (saldos.saldo * -1) - parame58.SALMIN_CUENTA  as Disponible, saldos.saldo * -1 as saldo,depctas.Excenta from dep_maeahor depctas "
                + " INNER JOIN  cop_ahorro58 parame58 on parame58.lincred = depctas.lincred  left join cop_saldos_vw saldos on saldos.codigoter = depctas.codigoter and saldos.lincred = depctas.lincred and saldos.numero = depctas.num_cuenta and saldos.periodo = " + Strings.Format(Fecha, "yyyyMM")
                + " inner join sys_maenit maenit on maenit.codigoter = depctas.codigoter " + WhereEmpresa + " where saldos.saldo<>0 " + (ProcesoIncluye == 0 ? " and debito_automat = 'Y' " : "");

            ERP.Core.Compartido.Controles.Barraprogress Pro = new ERP.Core.Compartido.Controles.Barraprogress("Aplicando Debito Automatico.", Pertenece);
            Pro.DefineMaximo(stmysql, conect);
            Pro.Show();
            DataSet MyRead = new DataSet();
            CargaVarini.ExecuteQueryDataset(stmysql, conect, "ProcesoDebitoAutomatico", MyRead, "TblDebAuto");
            canreg = MyRead.Tables["TblDebAuto"].Rows.Count;
            ok = false;
            // car.BuscaComprobante(Comprobante, 0, false, conect, ref controla); // ERROR: CS1620
            if (controla == "Y")
                // car.BuscaComprobante(Comprobante, Consecutivo, true, conect); // ERROR: CS1620

            while (fila < canreg)
            {
                DataRow _row = MyRead.Tables["TblDebAuto"].Rows[fila];
                sw = 0;
                if (_row["DISPONIBLE"] is DBNull)
                {
                    sw = 1;
                }
                else
                {
                    if (RespSalMin == 1)
                    {
                        if (Convert.ToDouble(_row["DISPONIBLE"]) <= 0)
                            sw = 1;
                        else
                            disponible = Convert.ToDouble(_row["DISPONIBLE"]);
                    }
                    else
                    {
                        if ((_row["saldo"] is DBNull) || Convert.ToDouble(_row["saldo"]) == 0)
                            sw = 1;
                        else
                            disponible = Convert.ToDouble(_row["saldo"]);
                    }
                }

                string estadocuenta = "0";
                int _lincredDA = Validar.Lincred;
                string _u1 = " ", _u2 = " ", _u3 = " ", _u4 = " ", _u5 = " ", _u6 = " ";
                BuscarCuentaAhorro(_row["num_cuenta"], conect, Navega.Ninguno, _row["codigoter"], ref _lincredDA, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u6, ref estadocuenta);
                Validar.Lincred = _lincredDA;

                int _estadoCta = Information.IsNumeric(estadocuenta) ? Convert.ToInt32(estadocuenta) : 0;
                switch (_estadoCta)
                {
                    case 2: case 3: sw = 1; break;
                }

                saldoextras = 0;
                // car.BuscaSaldosCuotasPendientes(_row["codigoter"], 0, 0, // ERROR: CS1503
                    // 999999999, 999999999, Strings.Format(Fecha, "yyyyMM"), HastaCiclo, conect, ref _u1, ref saldoextras, ref _u2, ref _u3, ref _u4, ref _u5, // ERROR: CS1503
                    // ref _u6, ref _u1, ref _u2, ref _u3, ref _u4, ref TotalPendiente); // ERROR: CS1503

                switch (Aplica4x1000)
                {
                    case "Y":
                        Apli4x1000 = Convert.ToString(_row["Excenta"]) == "N" ? "Y" : "N";
                        break;
                    case "N":
                        Apli4x1000 = "N";
                        break;
                }
                switch (SoloExtras)
                {
                    case "Y":
                        if (saldoextras <= 0) sw = 1;
                        break;
                    case "N":
                        if (TotalPendiente == 0) sw = 1;
                        break;
                }

                if (sw == 0 && disponible > 0)
                {
                    ok = GrabaCuotasDebAutomatico(Convert.ToString(_row["codigoter"]), disponible, Documento,
                        Fecha, Opcion, conect, Comprobante, Consecutivo, Usuario, ConcepAdicionales,
                        Convert.ToDouble(_row["num_cuenta"]), Convert.ToInt32(_row["lincred"]), IncluyeObliCobJur, ClaseDsto, HastaCiclo, Apli4x1000, ref dsdata, SoloExtras, Periodicidad);
                }
                Pro.PerformStep();
                fila += 1;
            }
            Pro.Close();
            Pro.Dispose();
            MyRead.Dispose();
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabaCuotasDebAutomatico
        // ----------------------------------------------------------------
        public bool GrabaCuotasDebAutomatico(string codigoter, double saldo, string DOMTO, DateTime fecha, OpcionesDebAutomatico Opcion,
            OdbcConnection conect, string Comprobante, double Consecutivo,
            string usuario, ComboBox.ObjectCollection ConcepAdicionales, double Cuenta, int linea,
            int cobrojur, int cladesto, int HastaCiclo, string Aplica4x1000, ref DataSet dsdata, string SoloExtras, string Periodicidad)
        {
            int sw2 = 0;
            double TotalPendiente = 0;
            double disponible = 0; int reg = 0; int cont = 0;
            double SaldoInicial = saldo;
            string cptoAhorro = "99"; bool SinSaldo = false; string where = ""; string wherePeriodd = "";
            string stmysql2; double SaldoExtra = 0; string cptoextras = "99"; string tipotransaccion = "99";
            double VlrAcum4x1000 = 0; double Vlr4x1000 = 0; double VlrADebitar = 0; string cpto4x1000 = "99";
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto tipoDescuento = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto();
            string _u1 = " ", _u2 = " ", _u3 = " ", _u4 = " ", _u5 = " ";
            // paramsys.BuscarCompania(Var.varini.SptCodEmpr, conect, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, // ERROR: CS1061
                // ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref cptoextras, ref cptoAhorro, // ERROR: CS1061
                // ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref cpto4x1000); // ERROR: CS1061

            if (Opcion == OpcionesDebAutomatico.Todos_Conceptos && SoloExtras == "N")
            {
                double valor_anterior = saldo;
                disponible = saldo;
                switch (cladesto)
                {
                    case 0: tipoDescuento = ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo; break;
                    case 1: tipoDescuento = ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina; break;
                    case 2: tipoDescuento = ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Caja; break;
                }

                // ok = msgcop_car.GrabaMovimiento(Comprobante, Consecutivo, codigoter, 9999, 99999999, Strings.Format(fecha, "yyyyMM"), "99", fecha, 0, disponible, "APLICACION DEBITO AUTOMATICO " + fecha, usuario, conect, ref _u1, ref _u2, codigoter, ref _u3, codigoter, tipoDescuento, ref _u4, true, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, HastaCiclo, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, "1"); // ERROR: CS1503, CS1615, CS1620

                if (ok)
                {
                    if (disponible > 0)
                        TotalPendiente = valor_anterior - disponible;
                    else
                        TotalPendiente = saldo;

                    switch (Aplica4x1000)
                    {
                        case "Y": Vlr4x1000 = this.Liquida4x1000(TotalPendiente, Cuenta, fecha, conect); break;
                        case "N": Vlr4x1000 = 0; break;
                    }
                    VlrADebitar = TotalPendiente + Vlr4x1000;
                    VlrAcum4x1000 = Vlr4x1000;
                    return true;
                }
            }
            else
            {
                switch (SoloExtras)
                {
                    case "Y": where = " and copmora.SaldoExtra<>0 "; break;
                    case "N": where = " and (copmora.saldocapital<>0 or copmora.SaldoAdmon<>0 or copmora.SaldoExtra<>0 or copmora.SaldoInteres<>0 or copmora.saldomora<>0 or copmora.saldoOtros<>0 or copmora.saldoSeguro<>0) "; break;
                }
                switch (Periodicidad)
                {
                    case "0": wherePeriodd = ""; break;
                    default: wherePeriodd = " and maecar.periodd='" + Periodicidad + "' "; break;
                }

                stmysql2 = "select copmora.codigoter, copmora.lincred, copmora.numero, copmora.periodo_causa, copmora.periodo_contable,"
                        + "copmora.saldocapital,copmora.SaldoAdmon,copmora.SaldoExtra,copmora.SaldoInteres,copmora.saldomora, copmora.saldoOtros,copmora.saldoSeguro,"
                        + "fogacla,codahor,maecar.cobrojur,maenit.apellido,maenit.nombre,parame12.descripcion from cop_copmora copmora "
                        + "inner join  cop_cuopen cuopen on cuopen.codigoter = copmora.codigoter and cuopen.lincred = copmora.lincred  and cuopen.numero = copmora.numero"
                        + " and cuopen.periodo_causa = copmora.periodo_causa inner join cop_concar12 parame12 on parame12.lincred = copmora.lincred "
                        + " inner join cop_maecar maecar on maecar.codigoter=copmora.codigoter  and copmora.lincred = maecar.lincred  and copmora.numero = maecar.numero"
                        + " inner join sys_maenit maenit on copmora.codigoter=maenit.codigoter where copmora.codigoter = '" + codigoter + "' and copmora.periodo_contable = " + Strings.Format(fecha, "yyyyMM")
                        + (cladesto == 0 ? "" : " and maecar.clades='" + cladesto + "' ") + " and copmora.periodo_causa <= " + HastaCiclo
                        + where + wherePeriodd + (Opcion == OpcionesDebAutomatico.ObligacionesMarcadas ? " and maecar.IncluyeDebAuto='Y'" : "");

                DataSet MyreadPend = new DataSet();
                CargaVarini.ExecuteQueryDataset(stmysql2, conect, "GrabaCuotasDebAutomatico", MyreadPend, "TblGrabaDebAuto");
                reg = MyreadPend.Tables["TblGrabaDebAuto"].Rows.Count;
                bool result = false;
                while (cont < reg)
                {
                    DataRow _r = MyreadPend.Tables["TblGrabaDebAuto"].Rows[cont];
                    SinSaldo = false;
                    if (saldo <= 0) SinSaldo = true;
                    sw2 = 0;
                    disponible = saldo;
                    Vlr4x1000 = 0;
                    SaldoExtra = 0;
                    switch (Opcion)
                    {
                        case OpcionesDebAutomatico.ConceptosDeCartera:
                            if (Convert.ToInt32(_r["fogacla"]) == 0 || Convert.ToInt32(_r["fogacla"]) == 4) sw2 = 1;
                            break;
                        case OpcionesDebAutomatico.SoloConceptos_AP:
                            if (Convert.ToInt32(_r["CodAhor"]) != 1) sw2 = 1;
                            break;
                        case OpcionesDebAutomatico.SoloConceptos_AH:
                            if (Convert.ToInt32(_r["CodAhor"]) != 2) sw2 = 1;
                            break;
                    }
                    if (Opcion != OpcionesDebAutomatico.ObligacionesMarcadas)
                    {
                        if (ConcepAdicionales.Contains(Convert.ToString(_r["lincred"])) == true) sw2 = 0;
                    }
                    if (cobrojur == 0)
                    {
                        if (!(_r["cobrojur"] is DBNull))
                        {
                            if (Convert.ToString(_r["cobrojur"]) == "Y") sw2 = 1;
                        }
                    }

                    if (sw2 == 0)
                    {
                        string _su1 = " ", _su2 = " ", _su3 = " ", _su4 = " ", _su5 = " ";
                        // car.BuscaSaldosCuotasPendientes(_r["codigoter"], _r["lincred"], _r["numero"], // ERROR: CS1503
                            // _r["lincred"], _r["numero"], Strings.Format(fecha, "yyyyMM"), HastaCiclo, conect, ref _su1, ref SaldoExtra, ref _su2, ref _su3, ref _su4, ref _su5, // ERROR: CS1503
                            // ref _su1, ref _su2, ref _su3, ref _su4, ref _su5, ref TotalPendiente); // ERROR: CS1503
                        switch (SoloExtras)
                        {
                            case "Y": TotalPendiente = SaldoExtra; tipotransaccion = cptoextras; break;
                            case "N": tipotransaccion = "99"; break;
                        }
                        if (TotalPendiente != 0)
                        {
                            if (SinSaldo)
                            {
                                dsdata.Tables["TblDebAuto"].Rows.Add(_r["codigoter"], _r["apellido"] + " " + _r["nombre"], Cuenta, _r["lincred"],
                                    _r["numero"], _r["periodo_causa"], TotalPendiente, _r["descripcion"]);
                            }
                            else
                            {
                                switch (Aplica4x1000)
                                {
                                    case "Y":
                                        if (TotalPendiente >= disponible)
                                        {
                                            Vlr4x1000 = this.Liquida4x1000(disponible, Cuenta, fecha, conect);
                                            disponible = disponible - Vlr4x1000;
                                            Vlr4x1000 = this.Liquida4x1000(disponible, Cuenta, fecha, conect);
                                        }
                                        else
                                            Vlr4x1000 = this.Liquida4x1000(TotalPendiente, Cuenta, fecha, conect);
                                        break;
                                    case "N": Vlr4x1000 = 0; break;
                                }
                                if (disponible > TotalPendiente)
                                    disponible = TotalPendiente;
                                else if (TotalPendiente > disponible)
                                    dsdata.Tables["TblDebAuto"].Rows.Add(_r["codigoter"], _r["apellido"] + " " + _r["nombre"], Cuenta, _r["lincred"],
                                        _r["numero"], _r["periodo_causa"], TotalPendiente - disponible, _r["descripcion"]);

                                double valor_anterior2 = disponible;
                                // ok = car.GrabaMovimiento(Comprobante, Consecutivo, _r["codigoter"], _r["lincred"], _r["numero"], Strings.Format(fecha, "yyyyMM"), // ERROR: CS1503
                                    // tipotransaccion, fecha, 0, disponible, "APLICACION DEBITO AUTOMATICO " + fecha, usuario, conect, ref _su1, ref _su2, _r["codigoter"], ref _su3, _r["codigoter"]); // ERROR: CS1503
                                if (ok)
                                {
                                    result = true;
                                    VlrADebitar = VlrADebitar + Vlr4x1000 + (valor_anterior2 - disponible);
                                    saldo -= valor_anterior2 - disponible + Vlr4x1000;
                                    VlrAcum4x1000 = VlrAcum4x1000 + Vlr4x1000;
                                }
                            }
                        }
                    }
                    cont += 1;
                }
                MyreadPend.Dispose();

                if (VlrADebitar != 0)
                {
                    if (VlrAcum4x1000 > 0)
                    {
                        VlrADebitar = VlrADebitar - VlrAcum4x1000;
                        // car.GrabaMovimiento(Comprobante, Consecutivo, codigoter, linea, Cuenta, Strings.Format(fecha, "yyyyMM"), cptoAhorro, fecha, VlrAcum4x1000, 0, "GRAVAMEN FINANCIERO" + fecha, usuario, conect, ref _u1, ref _u2, codigoter, ref _u3, ref _u4, ref _u5, ref _u1, false); // ERROR: CS1503, CS1615, CS1620
                        // car.GrabaMovimiento(Comprobante, Consecutivo, codigoter, linea, Cuenta, Strings.Format(fecha, "yyyyMM"), cpto4x1000, fecha, 0, VlrAcum4x1000, "GRAVAMEN FINANCIERO" + fecha, usuario, conect, ref _u1, ref _u2, codigoter, ref _u3, ref _u4, ref _u5, ref _u1, false); // ERROR: CS1503, CS1615, CS1620
                    }
                    // ok = car.GrabaMovimiento(Comprobante, Consecutivo, codigoter, linea, Cuenta, Strings.Format(fecha, "yyyyMM"), // ERROR: CS1503
                        // cptoAhorro, fecha, VlrADebitar, 0, "APLICACION DEBITO AUTOMATICO " + fecha, usuario, conect, ref _u1, ref _u2, codigoter); // ERROR: CS1503
                    if (ok) result = true;
                }
                return result;
            }
            return false;
        }

        // ----------------------------------------------------------------
        // GrabarImagenFirma
        // ----------------------------------------------------------------
        public void GrabarImagenFirma(long num_cuenta, int numFirma, string photoFilePath, OdbcConnection Conect, bool Requerida)
        {
            string req = Requerida ? "Y" : "N";
            if (photoFilePath != null)
            {
                photoFilePath = photoFilePath.Trim();
                if (File.Exists(photoFilePath) == true)
                {
                    byte[] firma = GetPhoto(photoFilePath);
                    OdbcCommand addEmp = new OdbcCommand("INSERT INTO dep_firmas (num_cuenta,firma,numFirma,requerida) " +
                        "Values(?,?," + numFirma + ",'" + req + "')", Conect);
                    if (CargarImagen(num_cuenta, numFirma, Conect) == null)
                    {
                        addEmp.Parameters.Add("@cuenta", OdbcType.Int, 4).Value = num_cuenta;
                        addEmp.Parameters.Add("@firma", OdbcType.Image, firma.Length).Value = firma;
                        try { addEmp.ExecuteNonQuery(); Interaction.MsgBox("Carge de firma se realizo correctamente", MsgBoxStyle.Information, "SOLIDO"); }
                        catch (Exception ex) { Interaction.MsgBox(ex.Message, MsgBoxStyle.Information, "SOLIDO"); }
                    }
                    else
                        Interaction.MsgBox("Primero Elimine la firma para poder regrabarla.", MsgBoxStyle.Information, "SOLIDO");
                }
                else
                    Interaction.MsgBox("El nombre de archivo es invalido.", MsgBoxStyle.Information, "SOLIDO");
            }
        }

        // ----------------------------------------------------------------
        // EliminarImagenFirma
        // ----------------------------------------------------------------
        public bool EliminarImagenFirma(long num_cuenta, int NumFirma, OdbcConnection Conect)
        {
            stmysql = "delete from dep_firmas where numFirma = " + NumFirma + " and num_cuenta = " + num_cuenta;
            ok = CargaVarini.ExecuteQueryconec(stmysql, Conect, "EliminarImagenFirma");
            return ok;
        }

        // ----------------------------------------------------------------
        // GetPhoto
        // ----------------------------------------------------------------
        public byte[] GetPhoto(string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            System.IO.BinaryReader br = new System.IO.BinaryReader(fs);
            byte[] photo = br.ReadBytes((int)fs.Length);
            br.Close();
            fs.Close();
            return photo;
        }

        // ----------------------------------------------------------------
        // CargarImagen
        // ----------------------------------------------------------------
        public Bitmap CargarImagen(long NumCuenta, int NumFirma, OdbcConnection Conect)
        {
            bool dummy = false;
            return CargarImagen(NumCuenta, NumFirma, Conect, ref dummy);
        }

        public Bitmap CargarImagen(long NumCuenta, int NumFirma, OdbcConnection Conect, ref bool Requerida)
        {
            Bitmap bitmap;
            Requerida = false;
            string req = "N";
            try
            {
                string order = " order by numFirma asc";
                DataSet dsFoto = new DataSet();
                string sql = "select firma,numFirma,requerida from dep_firmas where num_cuenta = " + NumCuenta + " and numFirma = " + NumFirma + order;
                CargaVarini.ExecuteQueryDataset(sql, Conect, "CargarImagen", dsFoto, "0");
                byte[] bits = (byte[])dsFoto.Tables[0].Rows[0].ItemArray[0];
                NumFirma = Convert.ToInt32(dsFoto.Tables[0].Rows[0].ItemArray[1]);
                req = Convert.ToString(dsFoto.Tables[0].Rows[0].ItemArray[2]);
                Requerida = (req.ToUpper() == "Y") ? true : false;
                System.IO.MemoryStream memorybits = new System.IO.MemoryStream(bits);
                bitmap = new Bitmap(memorybits);
                dsFoto.Dispose();
                return bitmap;
            }
            catch (Exception) { return null; }
        }

        // ----------------------------------------------------------------
        // CargarImagenSello
        // ----------------------------------------------------------------
        public Bitmap CargarImagenSello(long NumCuenta, OdbcConnection Conect)
        {
            Bitmap bitmap;
            try
            {
                string order = " order by num_cuenta asc";
                DataSet dsFoto = new DataSet();
                string sql = "select sello from dep_sellos where num_cuenta = " + NumCuenta + order;
                CargaVarini.ExecuteQueryDataset(sql, Conect, "CargarImagenSello", dsFoto, "0");
                byte[] bits = (byte[])dsFoto.Tables[0].Rows[0].ItemArray[0];
                System.IO.MemoryStream memorybits = new System.IO.MemoryStream(bits);
                bitmap = new Bitmap(memorybits);
                dsFoto.Dispose();
                return bitmap;
            }
            catch (Exception) { return null; }
        }

        // ----------------------------------------------------------------
        // EliminarImagenSello
        // ----------------------------------------------------------------
        public bool EliminarImagenSello(long num_cuenta, OdbcConnection Conect)
        {
            stmysql = "delete from dep_sellos where num_cuenta = " + num_cuenta;
            ok = CargaVarini.ExecuteQueryconec(stmysql, Conect, "EliminarImagenSello");
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabarImagenSello
        // ----------------------------------------------------------------
        public void GrabarImagenSello(long num_cuenta, string photoFilePath, OdbcConnection Conect)
        {
            if (photoFilePath != null)
            {
                photoFilePath = photoFilePath.Trim();
                if (File.Exists(photoFilePath) == true)
                {
                    byte[] firma = GetPhoto(photoFilePath);
                    OdbcCommand addEmp = new OdbcCommand("INSERT INTO dep_sellos (num_cuenta,sello) Values(?,?)", Conect);
                    if (CargarImagenSello(num_cuenta, Conect) == null)
                    {
                        addEmp.Parameters.Add("@cuenta", OdbcType.Int, 4).Value = num_cuenta;
                        addEmp.Parameters.Add("@sello", OdbcType.Image, firma.Length).Value = firma;
                        try { addEmp.ExecuteNonQuery(); Interaction.MsgBox("Lacarga del sello se realizo correctamente", MsgBoxStyle.Information, "SOLIDO"); }
                        catch (Exception ex) { Interaction.MsgBox(ex.Message, MsgBoxStyle.Information, "SOLIDO"); }
                    }
                    else
                        Interaction.MsgBox("Primero Elimine el sello para poder regrabarlo.", MsgBoxStyle.Information, "SOLIDO");
                }
                else
                    Interaction.MsgBox("El nombre de archivo es invalido.", MsgBoxStyle.Information, "SOLIDO");
            }
        }

        // ----------------------------------------------------------------
        // CargaLiberaCangePers
        // ----------------------------------------------------------------
        public DataTable CargaLiberaCangePers(ref DateTime fecha, OdbcConnection conect, string codigoter)
        {
            DataSet dataset = new DataSet();
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            stbuilder.Append("select rtrim(cheq.numCheque) as Num_Cheque ,cheq.num_cuenta as Cuenta,cheq.fecingreso as Fecha_ingreso,cheq.diascanje as Dias_Canje , ");
            stbuilder.Append("cheq.fecvence as Fecha_Vence ,cheq.valor as Valor,case cheq.plaza when 'L' then 'Local' else 'Otras plazas' end as plaza ");
            stbuilder.Append("from dep_checanje  cheq inner join dep_maeahor ahor on ahor.num_cuenta = cheq.num_cuenta ");
            stbuilder.Append("where cheq.fecvence <= '" + Strings.Format(fecha, Var.varini.PstForFec) + "' and cheq.estado = 'C' and ahor.codigoter = '" + codigoter + "' ");
            stbuilder.Append("order by cheq.numCheque");
            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), conect, "CargaLiberaCangePres", dataset, "Cheque");
            return dataset.Tables["Cheque"];
        }

        // ----------------------------------------------------------------
        // CargaLiberaCange
        // ----------------------------------------------------------------
        public DataTable CargaLiberaCange(ref DateTime fecha, OdbcConnection conect)
        {
            DataSet ds = new DataSet("Datos");
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            stbuilder.Append("select rtrim(numCheque) as Num_Cheque ,num_cuenta as Cuenta,fecingreso as Fecha_ingreso,diascanje as Dias_Canje , ");
            stbuilder.Append("fecvence as Fecha_Vence ,valor as Valor,case plaza when 'L' then 'Local' else 'Otras plazas' end as plaza ");
            stbuilder.Append("from dep_checanje where fecvence <= '" + Strings.Format(fecha, Var.varini.PstForFec) + "' and estado = 'C' ");
            stbuilder.Append("order by numCheque");
            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), conect, "CargaLiberaCange", ds, "dato");
            return ds.Tables["dato"];
        }

        // ----------------------------------------------------------------
        // GrabaLiberaCange
        // ----------------------------------------------------------------
        public bool GrabaLiberaCange(ref DateTime fecha, OdbcConnection conect, Form pertenece)
        {
            stmysql = "select numCheque ,num_cuenta "
                + ",fecingreso ,diascanje ,fecvence ,valor from "
                + " dep_checanje where fecvence <= '" + Strings.Format(fecha, Var.varini.PstForFec) + "' and estado = 'C'";
            ERP.Core.Compartido.Controles.Barraprogress pro = new ERP.Core.Compartido.Controles.Barraprogress(stmysql, "Liberando Cheques en canje.", conect, pertenece);
            DataSet ds = new DataSet("Datos");
            CargaVarini.ExecuteQueryDataset(stmysql, conect, "CargaLiberaCange", ds, "dato");
            pro.Show();
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                DataRow _r = ds.Tables[0].Rows[i];
                stmysql = "update dep_checanje set estado = 'L' where num_cuenta = " + _r["num_cuenta"]
                    + " and numcheque = " + _r["numcheque"] + " and fecingreso = '" + Strings.Format(Convert.ToDateTime(_r["fecingreso"]), Var.varini.PstForFec) + "'";
                ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabaLiberaCange");
                pro.PerformStep();
            }
            ds.Dispose();
            pro.Close();
            return ok;
        }

        // ----------------------------------------------------------------
        // BuscaSaldoEncanje
        // ----------------------------------------------------------------
        public bool BuscaSaldoEncanje(ref double Cuenta, OdbcConnection conect, ref double Saldo)
        {
            stmysql = "select sum(valor) as campo1 from dep_checanje  where num_cuenta = " + Cuenta + " and estado = 'C'";
            string saldo_str = Saldo.ToString();
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "BuscaSaldoEncanje", ref saldo_str);
            if (Information.IsNumeric(saldo_str)) Saldo = Convert.ToDouble(saldo_str);
            return ok;
        }

        // Convenience overload for non-ref Cuenta
        public bool BuscaSaldoEncanje(object Cuenta, OdbcConnection conect, ref double Saldo)
        {
            double c = Information.IsNumeric(Cuenta) ? Convert.ToDouble(Cuenta) : 0;
            return BuscaSaldoEncanje(ref c, conect, ref Saldo);
        }

        // Convenience overload for string Cuenta
        public bool BuscaSaldoEncanje(string CuentaStr, OdbcConnection conect, ref double Saldo)
        {
            double c = Information.IsNumeric(CuentaStr) ? Convert.ToDouble(CuentaStr) : 0;
            return BuscaSaldoEncanje(ref c, conect, ref Saldo);
        }

        // ----------------------------------------------------------------
        // ElimanarCanje
        // ----------------------------------------------------------------
        public void ElimanarCanje(ref double Cuenta, string banco, string Numcheque, DateTime FecIngreso, OdbcConnection myconnect2)
        {
            stmysql = "delete from dep_checanje where num_cuenta = '" + Cuenta + "' and estado = 'C' and numcheque = " + Numcheque + " and fecingreso = '" + Strings.Format(FecIngreso, Var.varini.PstForFec) + "'";
            CargaVarini.ExecuteQueryconec(stmysql, myconnect2, "ElimanarCanje");
        }

        // Convenience overload (double non-ref)
        public void ElimanarCanje(double Cuenta, string banco, string Numcheque, DateTime FecIngreso, OdbcConnection myconnect2)
        {
            ElimanarCanje(ref Cuenta, banco, Numcheque, FecIngreso, myconnect2);
        }

        // ----------------------------------------------------------------
        // BuscaComprobantesCajero (2 overloads)
        // ----------------------------------------------------------------
        public bool BuscaComprobantesCajero(string Usuario, OdbcConnection conect, ref ComboBox Comprobantes)
        {
            string dummy = "";
            return BuscaComprobantesCajero(Usuario, conect, ref Comprobantes, ref dummy);
        }

        public bool BuscaComprobantesCajero(string Usuario, OdbcConnection conect, ref ComboBox Comprobantes, ref string Validadora)
        {
            stmysql = "select compte, ConseCpte,TiempoClave, validadora from dep_cajeros where codcajero = '"
                + Usuario + "' and estado = 'A' and fecApertura = '" + Strings.Format(DateTime.Now, Var.varini.PstForFec) + "'";
            DataSet ds = new DataSet();
            CargaVarini.ExecuteQueryDataset(stmysql, conect, "BuscaComprobantesCajero", ds, "dato");
            for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                DataRow _r = ds.Tables[0].Rows[i];
                if (Convert.ToString(_r["validadora"]).Trim() != "")
                    Validadora = Convert.ToString(_r["validadora"]);
                Comprobantes.Items.Add(Convert.ToString(_r["compte"]) + "-" + Convert.ToString(_r["ConseCpte"]));
                ok = true;
            }
            ds.Dispose();
            return ok;
        }

        // ----------------------------------------------------------------
        // Liquida4x1000
        // ----------------------------------------------------------------
        public double Liquida4x1000(double VlrBase, double cuenta, DateTime fecha, OdbcConnection conect)
        {
            decimal dummy1 = 0; string dummy2 = "";
            return Liquida4x1000(VlrBase, cuenta, fecha, conect, ref dummy1, ref dummy2);
        }

        public double Liquida4x1000(double VlrBase, double cuenta, DateTime fecha, OdbcConnection conect, ref decimal Tasa, ref string Cpte4x100)
        {
            double vlr4x1000 = 0; string stmysql2; DateTime fechaFinal;
            string codigoter; int lincred; double Tope4x1000; double VlrRetiros;
            DateTime FecIni; DateTime FecFin; double retiros;

            stmysql2 = "select depctas.*, gravamen,FORAPLI_4XMIL,CBTE_4XMIL, tope_4xmil from dep_maeahor depctas inner join cop_ahorro58 parame58 on parame58.lincred = depctas.lincred where num_cuenta = " + cuenta;
            OdbcCommand myCommand = new OdbcCommand(stmysql2, conect);
            myCommand.CommandTimeout = 0;
            OdbcDataReader Myread = myCommand.ExecuteReader();
            codigoter = ""; lincred = 0; Tope4x1000 = 0; VlrRetiros = 0;
            FecIni = DateTime.Now; FecFin = DateTime.Now; retiros = 0;

            if (Myread.Read())
            {
                if (Convert.ToInt32(Myread["FORAPLI_4XMIL"]) == 0)
                {
                    myCommand.Connection.Close();
                    Myread.Close();
                    return 0;
                }
                Tasa = Convert.ToDecimal(Myread["gravamen"]);
                Cpte4x100 = Convert.ToString(Myread["CBTE_4XMIL"]);
                codigoter = Convert.ToString(Myread["codigoter"]);
                lincred = Convert.ToInt32(Myread["lincred"]);
                Tope4x1000 = Convert.ToDouble(Myread["tope_4xmil"]);

                if (Convert.ToString(Myread["Excenta"]) == "N")
                    vlr4x1000 = Math.Round(VlrBase * (double)(Tasa / 100));

                if (Convert.ToString(Myread["Excenta"]) == "Y")
                {
                    Myread.Close();
                    FecIni = new DateTime(fecha.Year, fecha.Month, 1);
                    FecFin = new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month));
                    myCommand.CommandText = "select SUM(a.VLR_DEBITO) as retiros from cop_movimto a inner join cop_docmto b on a.compronte=b.compronte and a.numero_domto=b.numero_domto inner join sys_compro02 c on a.compronte=c.codigo  where codigoter = '" + codigoter + "' and lincred = " + lincred + " and numero = " + cuenta + " and fecha_movto between '"
                        + Strings.Format(FecIni, Var.varini.PstForFec) + "' and '" + Strings.Format(FecFin, Var.varini.PstForFec) + "' and b.anulado<>'Y' and c.restri_tesoreria<>'Y'";
                    myCommand.CommandTimeout = 0;
                    myCommand.Connection = conect;
                    Myread = myCommand.ExecuteReader();
                    if (Myread.Read())
                    {
                        if (Myread["retiros"] is DBNull)
                            retiros = 0;
                        else
                            retiros = Convert.ToDouble(Myread["retiros"]);

                        if (retiros + VlrBase > Tope4x1000)
                        {
                            VlrRetiros = retiros;
                            if (VlrRetiros > Tope4x1000)
                                vlr4x1000 = Math.Round(VlrBase * (double)(Tasa / 100));
                            else
                            {
                                VlrRetiros = (retiros + VlrBase) - Tope4x1000;
                                vlr4x1000 = Math.Round(VlrRetiros * (double)(Tasa / 100));
                            }
                        }
                    }
                }
            }
            Myread.Close();
            return vlr4x1000;
        }

        // ----------------------------------------------------------------
        // Validacion
        // ----------------------------------------------------------------
        public void Validacion(string NombreDispositivo, Regvalidacion Valida)
        {
            try
            {
                Validar = Valida;
                PrintDocument pd = new PrintDocument();
                pd.PrintPage += new PrintPageEventHandler(pd_PrintPage);
                if (NombreDispositivo == null)
                {
                    PrintDialog diag = new PrintDialog();
                    if (diag.ShowDialog() == DialogResult.OK)
                        NombreDispositivo = diag.PrinterSettings.PrinterName;
                    else
                        return;
                }
                pd.PrinterSettings.PrinterName = NombreDispositivo;
                pd.Print();
            }
            finally { }
        }

        private void pd_PrintPage(object sender, PrintPageEventArgs ev)
        {
            float topMargin = 10f;
            Font printFont = new Font("Draft 12cpi", 8, FontStyle.Regular);
            string line;

            line = Strings.Mid(Validar.EmpresaResum, 1, 19) + " " + DateTime.Now.ToString();
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 3, topMargin, new StringFormat());
            topMargin += 15;

            line = Strings.Mid(Validar.codigoter, 3, 14) + " " + Strings.Mid(Validar.Nombre, 1, 38);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 3, topMargin, new StringFormat());
            topMargin += 15;

            line = Strings.Right("0000" + Strings.Mid(Validar.Lincred.ToString(), 1, 4), 4) + " "
                + Strings.Right("00000000" + Strings.Mid(Validar.ConseLincred.ToString(), 1, 8), 10) + " "
                + Strings.Mid(Validar.DescLincred, 1, 14) + " " + Strings.Mid(Validar.Cpte, 1, 4) + " "
                + Strings.Mid(Validar.ConseCpte, 1, 8);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 3, topMargin, new StringFormat());
            topMargin += 15;

            line = Strings.Left(Validar.Usuario + Strings.Space(17), 15)
                + Strings.Mid(Strings.Left(Validar.Detalle + Strings.Space(13), 13), 1, 8) + "   ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 3, topMargin, new StringFormat());

            line = Strings.RSet(Strings.Mid(Strings.Right(Strings.Space(20) + Strings.Format(Validar.Valor, "$###,###,###.00"), 20), 1, 20), 20);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 162, topMargin, new StringFormat());

            if (Validar.MuestraSaldo == "N")
            {
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 162, topMargin, new StringFormat());
            }
            else
            {
                topMargin += 15;
                ev.Graphics.DrawString(Strings.Left("Saldo Total                ", 22), printFont, Brushes.Black, 3, topMargin, new StringFormat());
                line = Strings.RSet(Strings.Mid(Strings.Right(Strings.Space(20) + Strings.Format(Validar.Saldo, "$###,###,###.00"), 20), 1, 20), 20);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 162, topMargin, new StringFormat());

                topMargin += 15;
                ev.Graphics.DrawString(Strings.Left("Saldo en Canje                ", 22), printFont, Brushes.Black, 3, topMargin, new StringFormat());
                line = Strings.RSet(Strings.Mid(Strings.Right(Strings.Space(20) + Strings.Format(Validar.SaldoCanje, "$###,###,###.00"), 20), 1, 20), 20);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 162, topMargin, new StringFormat());

                topMargin += 15;
                ev.Graphics.DrawString(Strings.Left("Saldo Disponible              ", 20), printFont, Brushes.Black, 3, topMargin, new StringFormat());
                line = Strings.RSet(Strings.Mid(Strings.Right(Strings.Space(20) + Strings.Format(Validar.SaldoDisponible, "$###,###,###.00"), 20), 1, 20), 20);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 160, topMargin, new StringFormat());
            }
        }

        // ----------------------------------------------------------------
        // CargaMovimientos
        // ----------------------------------------------------------------
        public DataSet CargaMovimientos(string Compronte, double Numero, OdbcConnection conect)
        {
            DataSet ds = new DataSet("Datos");
            stmysql = " select a.SECUENCIA as Reg , a.numero as Cuenta ,a.lincred as Linea ,b.NOMBRE as Descripcion,case numcheque when '' then 'Efectivo' else "
                    + " 'Cheque' end as Tipo_Pag ,a.vlr_debito as Retiros , a.vlr_credito as Depositos "
                    + " from cop_movimto a inner join cop_ahorro58 b on b.lincred = a.lincred"
                    + " where a.lincred <> 9999 and a.compronte = '" + Compronte + "' and a.numero_domto = " + Numero;
            CargaVarini.ExecuteQueryDataset(stmysql, conect, "CargaMovimientos", ds, "dato");
            return ds;
        }

        // ----------------------------------------------------------------
        // ConfiguraForma
        // ----------------------------------------------------------------
        public void ConfiguraForma(Form Forma)
        {
            string dummy = null;
            ConfiguraForma(Forma, ref dummy, ref dummy, ref dummy, ref dummy, ref dummy, ref dummy);
        }

        public void ConfiguraForma(Form Forma, ref string Empresa, ref string Servidor, ref string Bd,
            ref string Nomforma, ref string Usuario, ref string Fecha)
        {
            CargaVarini.LlenarVarini(ref Var.varini);
            Empresa = Var.varini.pstEmpresa;
            Servidor = Var.varini.pstServer;
            Bd = Var.varini.pstBdatos;
            Nomforma = Forma.Name;
            Usuario = Var.varini.pstUsuario;
            Fecha = Strings.Format(DateTime.Now, Var.varini.PstForFec);
        }

        // ----------------------------------------------------------------
        // Grabamovimiento
        // ----------------------------------------------------------------
        public bool Grabamovimiento(string Comprobante, double ConseCpte, int ClaseMovto, int formapago, double valor, string Cuenta, DateTime FechaMovto,
            string Usuario, Form Myforma, OdbcConnection Myconect)
        {
            return Grabamovimiento(Comprobante, ConseCpte, ClaseMovto, formapago, valor, Cuenta, FechaMovto, Usuario, Myforma, Myconect, false, 0, "9999", " ", true, " ", " ", 0);
        }

        public bool Grabamovimiento(string Comprobante, double ConseCpte, int ClaseMovto, int formapago, double valor, string Cuenta, DateTime FechaMovto,
            string Usuario, Form Myforma, OdbcConnection Myconect,
            bool LiqGmfCpte, int DiasCanje, string banco, string NumCheque, bool AplGmf, string Desprendible, string Detalle, int DiasCanjeOtras)
        {
            int TotRet; MsgBoxResult Botton; string TipoDoc = "N";
            double Vlr4x1000; string Cpte4x1000 = "9999"; string Cpto4x1000 = "9999"; string Cptoaho = "9999";
            double SaldoCanje = 0; double MinRetiro = 0; double SalMinCuenta = 0; double Maxretiro = 0;
            string Codigoter = "99999999999999"; string NitAso = " "; string cencos = "99999999"; double saldot = 0; double SaldoDisponible = 0;
            string lincred = "9999"; string DescLincred = " "; int ForAplGmf = 0;
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera local_msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
            ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
            // dep_fcanje01 Frmcanje = new dep_fcanje01(Myconect);
            int RetCheGmf = 0; double saldocuenta = 0; // ERROR: CS0246
            string usuarioaprobo = " "; double vlrsobregiro = 0; string cpto4x1000Cheque = "9999";

            switch (ClaseMovto)
            {
                case 0: case 4:
                    switch (formapago) { case 1: case 2: NumCheque = "0"; break; }
                    break;
                case 1:
                    switch (formapago)
                    {
                        case 1: case 2:
                            if (formapago == 2) { DiasCanje = 0; DiasCanjeOtras = 0; }
                            // Frmcanje.txtvalor.Text = Strings.FormatNumber(valor, 2); // ERROR: CS0103
                            // Frmcanje.lblcuenta.Text = Cuenta; // ERROR: CS0103
                            // Frmcanje.DtpFecha.Text = Strings.Format(FechaMovto.Date, "dd/MM/yyyy"); // ERROR: CS0103
                            // Frmcanje.DtpFecvence.Text = Strings.Format(FechaMovto.Date, "dd/MM/yyyy"); // ERROR: CS0103
                            // Frmcanje.txtcanje.Text = DiasCanje.ToString(); // ERROR: CS0103
                            // Frmcanje.diascanjeotras = DiasCanjeOtras; // ERROR: CS0103
                            // Frmcanje.TipoCanje = formapago; // ERROR: CS0103
                            // Frmcanje.Compronte = Comprobante; // ERROR: CS0103
                            // Frmcanje.ConseCompronte = ConseCpte; // ERROR: CS0103
                            // Frmcanje.usuario = Usuario; // ERROR: CS0103
                            // if (Frmcanje.ShowDialog(Myforma) == DialogResult.Cancel) // ERROR: CS0103
                            {
                                // Frmcanje.Close(); Frmcanje.Dispose(); return false; // ERROR: CS0103
                            }
                            // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                            {
                                // banco = Frmcanje.txtbanco.Text; // ERROR: CS0103
                                // NumCheque = Frmcanje.txtCheque.Text; // ERROR: CS0103
                                // Frmcanje.Close(); Frmcanje.Dispose(); // ERROR: CS0103
                            }
                            break;
                    }
                    break;
            }

            ok = BuscarCuentaAhorro(Cuenta, Myconect, Navega.Ninguno, ref Codigoter, ref lincred);
            if (!ok) { Interaction.MsgBox("Numero de cuenta de ahorros no existe", MsgBoxStyle.Information, "SOLIDO"); return false; }

            // local_msgcop.BuscaComprobante(Comprobante, 0, false, Myconect, ref TipoDoc); // ERROR: CS1620

            string _cptoahu = Cptoaho; string _cptocap = Var.varini.pstCptocap;
            string _u1 = " "; string _u2 = " "; string _u3 = " "; string _u4 = " "; string _u5 = " ";
            // msgparsys.BuscarCompania(Var.varini.SptCodEmpr, Myconect, ref _u1, ref _cptoahu, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref Cptoaho, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref Cpto4x1000, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref cpto4x1000Cheque); // ERROR: CS1061

            BuscaSaldoEncanje(Cuenta, Myconect, ref SaldoCanje);
            string _minret = MinRetiro.ToString(); string _salmin = SalMinCuenta.ToString();
            string _diascanje = DiasCanje.ToString(); string _maxret = Maxretiro.ToString();
             string _forapl = ForAplGmf.ToString(); string _retche = RetCheGmf.ToString(); // ERROR: CS0103
            // BuscaLineaAhorro(lincred, Myconect, ref _u1, ref DescLincred, ref _u2, ref _u3, ref _minret, ref _salmin, ref _u4, ref _u5, ref _u1, ref _u2, ref _diascanje, ref _u3, ref _u4, ref _maxret, ref _u5, ref _u1, ref _u2, ref _u3, ref _forapl, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _retche); // ERROR: CS1501
            if (Information.IsNumeric(_minret)) MinRetiro = Convert.ToDouble(_minret);
            if (Information.IsNumeric(_salmin)) SalMinCuenta = Convert.ToDouble(_salmin);
            if (Information.IsNumeric(_forapl)) ForAplGmf = Convert.ToInt32(_forapl);
            // if (Information.IsNumeric(_retche)) RetCheGmf = Convert.ToInt32(_retche); // ERROR: CS0103

            // msgparcop.BuscaAsociado(Codigoter, Myconect, ref _u1, ref _u2, ref _u3, ref NitAso, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref cencos); // ERROR: CS7036

            // local_msgcop.BuscaSaldoObligacion(Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Myconect, ref saldot); // ERROR: CS1503
            saldot = saldot * -1;
            SaldoDisponible = Math.Round((saldot - SalMinCuenta - SaldoCanje), 2);

            switch (ClaseMovto)
            {
                case 0: case 4:
                    if (ForAplGmf == 2 && AplGmf == true)
                    {
                        Vlr4x1000 = this.Liquida4x1000(valor, Convert.ToDouble(Cuenta), FechaMovto, Myconect, ref _d1, ref Cpte4x1000);
                        TotRet = (int)(valor + Vlr4x1000);
                        if (TipoDoc == "CP") Cpte4x1000 = Comprobante;
                        if (LiqGmfCpte == true) Cpte4x1000 = Comprobante;
                        // if (formapago == 1 && RetCheGmf == 1) Vlr4x1000 = 0; // ERROR: CS0103
                        if (formapago == 1 || formapago == 2) Cpto4x1000 = cpto4x1000Cheque;
                        if (Vlr4x1000 > 0)
                        {
                            if (ClaseMovto == 4) valor = valor - Vlr4x1000;
                            // local_msgcop.GrabaMovimiento(Cpte4x1000, ConseCpte, Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Cptoaho, FechaMovto, Vlr4x1000, 0, "GRAVAMEN FINANCIERO", Usuario, Myconect, ref _u1, ref NitAso, Desprendible, Codigoter, ref _u2, ref _u3, false, ref _u4, ref _u5, ref _u1, ref _u2, ref banco); // ERROR: CS1503, CS1615, CS1620
                            // local_msgcop.GrabaMovimiento(Cpte4x1000, ConseCpte, Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Cpto4x1000, FechaMovto, 0, Vlr4x1000, "GRAVAMEN FINANCIERO", Usuario, Myconect, ref _u1, ref NitAso, Desprendible, Codigoter, ref _u2, ref _u3, false, ref _u4, ref _u5, ref _u1, ref _u2, ref banco); // ERROR: CS1503, CS1615, CS1620
                        }
                    }
                    // local_msgcop.GrabaMovimiento(Comprobante, ConseCpte, Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Cptoaho, FechaMovto, valor, 0, Detalle, Usuario, Myconect, ref _u1, ref NitAso, Desprendible, Codigoter, ref _u2, ref _u3, false, ref _u4, ref _u5, ref _u1, ref _u2, ref banco, ref NumCheque, ref _u3, ref _u4, ref _u5, ref usuarioaprobo, ref vlrsobregiro); // ERROR: CS1503, CS1615, CS1620
                    if (ClaseMovto == 4) { GrabaEstadoCuentaAhorros(Convert.ToDouble(Cuenta), 3, Myconect); Validar.Detalle = "CANCELACION"; }
                    else Validar.Detalle = "RETIRO";
                    break;

                case 1:
                    // local_msgcop.GrabaMovimiento(Comprobante, ConseCpte, Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Cptoaho, FechaMovto, 0, valor, Detalle, Usuario, Myconect, ref _u1, ref NitAso, Desprendible, Codigoter, ref _u2, ref _u3, false, ref _u4, ref _u5, ref _u1, ref _u2, ref banco, ref NumCheque); // ERROR: CS1503, CS1615, CS1620
                    switch (formapago)
                    {
                        case 0: GrabaFormapago(Comprobante, ConseCpte, valor, 0, "", "0", "", 0, "", 0, "", 0, "", 0, "", "", 1, 0, Myconect); break;
                        case 1: case 2: GrabaFormapago(Comprobante, ConseCpte, 0, valor, banco, NumCheque, "", 0, "", 0, "", 0, "", 0, "", "", 0, 1, Myconect); break;
                    }
                    Validar.Detalle = "DEPOSITO";
                    break;

                case 2:
                    // local_msgcop.GrabaMovimiento(Comprobante, ConseCpte, Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Cptoaho, FechaMovto, valor, 0, Detalle, Usuario, Myconect); // ERROR: CS1503, CS1620
                    if (ForAplGmf == 2 && AplGmf == true)
                    {
                        Vlr4x1000 = this.Liquida4x1000(valor, Convert.ToDouble(Cuenta), FechaMovto, Myconect, ref _d1, ref Cpte4x1000);
                        if (Vlr4x1000 > 0)
                        {
                            // local_msgcop.GrabaMovimiento(Comprobante, ConseCpte, Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Cptoaho, FechaMovto, Vlr4x1000, 0, "GRAVAMEN FINANCIERO", Usuario, Myconect); // ERROR: CS1503, CS1620
                            // local_msgcop.GrabaMovimiento(Comprobante, ConseCpte, Codigoter, lincred, Cuenta, Strings.Format(FechaMovto, "yyyyMM"), Cpto4x1000, FechaMovto, 0, Vlr4x1000, "GRAVAMEN FINANCIERO", Usuario, Myconect); // ERROR: CS1503, CS1620
                        }
                    }
                    break;
            }
            return true;
        }

        // helper decimal field for Grabamovimiento Liquida4x1000 calls
        private decimal _d1 = 0;

        private void GrabaEstadoCuentaAhorros(double Numcuenta, int estado, OdbcConnection myconnect2)
        {
            if (estado == 2 || estado == 3)
                stmysql = "update dep_maeahor  set estado = '" + estado + "',fec_novedad = '" + Strings.Format(DateTime.Now.Date, Var.varini.PstForFec) + "' where num_cuenta = '" + Numcuenta + "'";
            else
                stmysql = "update dep_maeahor set estado = '" + estado + "' where num_cuenta = '" + Numcuenta + "'";
            CargaVarini.ExecuteQueryconec(stmysql, myconnect2, "GrabaEstadoCuentaAhorros");
        }

        // ----------------------------------------------------------------
        // CargaCuentaTraslado
        // ----------------------------------------------------------------
        public void CargaCuentaTraslado(ref string Cuenta, Form Myforma, OdbcConnection myconec)
        {
            // dep_Frmcuenta Frmcuenta = new dep_Frmcuenta(myconec); // ERROR: CS0246
            // if (Frmcuenta.ShowDialog(Myforma) == DialogResult.OK) // ERROR: CS0103
                // Cuenta = Frmcuenta.txtCuenta.Text; // ERROR: CS0103
            // else
                // Cuenta = "0";
            // Frmcuenta.Close(); // ERROR: CS0103
            // Frmcuenta.Dispose(); // ERROR: CS0103
        }

        // ----------------------------------------------------------------
        // BuscaArchivo
        // ----------------------------------------------------------------
        public string BuscaArchivo(string Filter)
        {
            return BuscaArchivo(Filter, " ");
        }

        public string BuscaArchivo(string Filter, string title)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = Filter;
            openDlg.Title = title;
            if (openDlg.ShowDialog() == DialogResult.OK)
                return openDlg.FileName;
            else
                return null;
        }

        // ----------------------------------------------------------------
        // cargarFirmas
        // ----------------------------------------------------------------
        public void cargarFirmas(string Cuenta, string NumeroFirmas, string Titulo,
            string TextoComando, Form Pertenece, string Usuario, OdbcConnection conect)
        {
            if (Information.IsNumeric(NumeroFirmas) == true)
            {
                if (Convert.ToDouble(NumeroFirmas) > 0)
                {
                    // dep_ffirmas VenFirmas = new dep_ffirmas(conect); // ERROR: CS0246
                    // VenFirmas.cmbsig.Text = TextoComando; // ERROR: CS0103
                    // VenFirmas.Text = Titulo; // ERROR: CS0103
                    // VenFirmas.TxtCuenta.Text = Cuenta; // ERROR: CS0103
                    // VenFirmas.NumFirmas = NumeroFirmas; // ERROR: CS0103
                    // VenFirmas.ShowDialog(Pertenece); // ERROR: CS0103
                }
            }
        }

        // ----------------------------------------------------------------
        // cargarSello
        // ----------------------------------------------------------------
        public void cargarSello(double Cuenta, string Titulo,
            Form Pertenece, string Usuario, bool Grabacion, OdbcConnection myconec)
        {
            if (Information.IsNumeric(Cuenta) == true)
            {
                if (Cuenta > 0)
                {
                    // dep_fsello VenFirmas = new dep_fsello(myconec); // ERROR: CS0246
                    // VenFirmas.Text = Titulo; // ERROR: CS0103
                    // VenFirmas.TxtCuenta.Text = Cuenta.ToString(); // ERROR: CS0103
                    // VenFirmas.Grabacion = Grabacion; // ERROR: CS0103
                    // VenFirmas.ShowDialog(Pertenece); // ERROR: CS0103
                }
            }
        }

        // ----------------------------------------------------------------
        // VerFirmas
        // ----------------------------------------------------------------
        public void VerFirmas(string Cuenta, string NumeroFirmas, Form Pertenece, string Usuario, OdbcConnection myconec)
        {
            if (Information.IsNumeric(NumeroFirmas) == true)
            {
                if (Convert.ToDouble(NumeroFirmas) > 0)
                {
                    // dep_fvenfirma VenFirmas = new dep_fvenfirma(myconec); // ERROR: CS0246
                    // VenFirmas.txtcuenta.Text = Cuenta; // ERROR: CS0103
                    // VenFirmas.Firmareq = NumeroFirmas; // ERROR: CS0103
                    // VenFirmas.ShowDialog(Pertenece); // ERROR: CS0103
                }
            }
        }

        // ----------------------------------------------------------------
        // VerDocumento
        // ----------------------------------------------------------------
        public void VerDocumento(string Comprobante, double Consecutivo, Form pertenece, string Usuario, OdbcConnection myconec)
        {
            if (Comprobante != "")
            {
                // dep_fverdocu01 VerDoc = new dep_fverdocu01(myconec); // ERROR: CS0246
                // VerDoc.Compronte = Comprobante; // ERROR: CS0103
                // VerDoc.NumCompronte = Consecutivo; // ERROR: CS0103
                // VerDoc.ShowDialog(pertenece); // ERROR: CS0103
            }
        }

        // ----------------------------------------------------------------
        // BorraMoviento
        // ----------------------------------------------------------------
        public void BorraMoviento(string Comprobante, double NumCpte, double Secuencia, int periodo, OdbcConnection myconnet)
        {
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera local_msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
            string banco = "9999"; string Numcheque = "0"; DateTime FecMovto = new DateTime(1950, 1, 1);
            double cuenta = 0; double credito = 0; double debito = 0; DataSet dsdata = new DataSet();

            stmysql = " select codigo_banco, Numcheque, fecha_movto,numero,vlr_credito,vlr_debito from cop_movimto where SECUENCIA = " + Secuencia + " and COMPRONTE = '" + Comprobante + "' and NUMERO_DOMTO = " + NumCpte;
            CargaVarini.ExecuteQueryDataset(stmysql, myconnet, "BorraMoviento", dsdata, "tblborra", true);

            if (dsdata.Tables["tblborra"].Rows.Count > 0)
            {
                DataRow _r = dsdata.Tables["tblborra"].Rows[0];
                banco = Convert.ToString(_r["codigo_banco"]);
                Numcheque = Convert.ToString(_r["Numcheque"]);
                FecMovto = Convert.ToDateTime(_r["fecha_movto"]);
                cuenta = Convert.ToDouble(_r["numero"]);
                credito = Convert.ToDouble(_r["vlr_credito"]);
                debito = Convert.ToDouble(_r["vlr_debito"]);
            }

            if (!Information.IsNumeric(Numcheque)) Numcheque = "0";

            local_msgcop.BorraRegistro(Comprobante, NumCpte, Secuencia, periodo, myconnet);
            ElimanarCanje(cuenta, banco, Numcheque, FecMovto, myconnet);
            if (credito > 0)
            {
                if (Convert.ToDouble(Numcheque) > 0)
                    GrabaFormapago(Comprobante, NumCpte, 0, credito * -1, banco, Numcheque, "", 0, "", 0, "", 0, "", 0, "", "", 0, -1, myconnet);
                else
                    GrabaFormapago(Comprobante, NumCpte, credito * -1, 0, banco, Numcheque, "", 0, "", 0, "", 0, "", 0, "", "", -1, 0, myconnet);
            }
        }

        // ----------------------------------------------------------------
        // GrabaFormapago
        // ----------------------------------------------------------------
        public void GrabaFormapago(string comprobante, double ConseComprobante, double efectivo,
            double Cantcheque, string banco, string NumeroCheque, string NumCuenta,
            double ValTarjDebito, string NumTarjDebito, double ValTarjCredito, string NumTarjCredito, double otros, string nro_otros,
            double Vlrtitulo, string Nrotitulo, string MotivoPago, int regefectivo, int regcheque, OdbcConnection myconect)
        {
            bool ok2 = false; string mysql;
            ok2 = buscaFormapago(comprobante, ConseComprobante, myconect);
            if (!ok2)
            {
                mysql = "insert into sys_forpago(compronte,numero_domto,efectivo,cheque,banco,nro_cheque,nro_cuenta,"
                      + "tdebito,nro_tdebito,tcredito,nro_tcredito, otros, nro_otros,VLRTITULO,NROTITULO,MotivoPago,regefectivo,regcheque) "
                      + "values('" + comprobante + "','" + ConseComprobante + "','" + efectivo + "','"
                      + Cantcheque + "','" + banco + "','" + NumeroCheque + "','" + NumCuenta + "','"
                      + ValTarjDebito + "','" + NumTarjDebito + "','" + ValTarjCredito + "','" + NumTarjCredito + "','"
                      + otros + "','" + nro_otros + "'," + Vlrtitulo + ",'" + Nrotitulo + "','" + MotivoPago + "'," + regefectivo + "," + regcheque + ")";
            }
            else
            {
                mysql = "update sys_forpago set efectivo=(efectivo + " + efectivo + "), cheque=cheque +(" + Cantcheque + "),tdebito=tdebito +(" + ValTarjDebito + "),"
                      + "tcredito=tcredito + (" + ValTarjCredito + "),banco='" + banco + "',nro_cheque='" + NumeroCheque + "',nro_cuenta='"
                      + NumCuenta + "',nro_tdebito='" + NumTarjDebito + "',nro_tcredito='" + NumTarjCredito + "',nro_otros='"
                      + nro_otros + "',otros=otros + (" + otros + "),VLRTITULO=VLRTITULO + " + Vlrtitulo + ",NROTITULO='" + Nrotitulo + "',MotivoPago='" + MotivoPago
                      + "',regefectivo=regefectivo+" + regefectivo + ",regcheque=regcheque+" + regcheque + " where compronte='" + comprobante + "' and numero_domto='" + ConseComprobante + "'";
            }
            CargaVarini.ExecuteQueryconec(mysql, myconect, "GrabaFormapago");
        }

        // ----------------------------------------------------------------
        // buscaFormapago
        // ----------------------------------------------------------------
        public bool buscaFormapago(string comprobante, double ConseComprobante, OdbcConnection myconect)
        {
            string e = "0", v = "0", b = "0", n = "0";
            return buscaFormapago(comprobante, ConseComprobante, myconect,
                ref e, ref v, ref b, ref n, ref e, ref v, ref b, ref n, ref e, ref v, ref b, ref n, ref e, ref v);
        }

        public bool buscaFormapago(string comprobante, double ConseComprobante, OdbcConnection myconect,
            ref string efectivo, ref string Valorcheque, ref string banco, ref string NumCheque, ref string NumCuenta,
            ref string VlrDebito, ref string NumTarjDebito, ref string VlrCredito, ref string NumTarjCredito,
            ref string otros, ref string nro_otros, ref string Vlrtitulo, ref string Nrotitulo,
            ref string MotivoPago)
        {
            // buscaFormapago with all optional out params — only 2 ref string slots per call
            string _nrocuenta = NumCuenta; string _vlrdebito = VlrDebito;
            string _numtarjdebito = NumTarjDebito; string _vlrcredito = VlrCredito;
            string _numtarjcredito = NumTarjCredito; string _otros = otros;
            string _nroOtros = nro_otros; string _vlrtitulo = Vlrtitulo;
            string _nrotitulo = Nrotitulo; string _motivopago = MotivoPago;

            string stmysql2 = "select efectivo as campo1, cheque as campo2,banco as campo3, nro_cheque as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            ok = CargaVarini.ExecuteQueryconec(stmysql2, myconect, "buscaFormapago", ref efectivo, ref Valorcheque, ref banco, ref NumCheque);

            stmysql2 = "select nro_cuenta as campo1,tdebito as campo2,nro_tdebito as campo3,tcredito as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            CargaVarini.ExecuteQueryconec(stmysql2, myconect, "buscaFormapago", ref _nrocuenta, ref _vlrdebito, ref _numtarjdebito, ref _vlrcredito);
            NumCuenta = _nrocuenta; VlrDebito = _vlrdebito; NumTarjDebito = _numtarjdebito; VlrCredito = _vlrcredito;

            stmysql2 = "select nro_tcredito as campo1,otros as campo2,nro_otros as campo3,VLRTITULO as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            CargaVarini.ExecuteQueryconec(stmysql2, myconect, "buscaFormapago", ref _numtarjcredito, ref _otros, ref _nroOtros, ref _vlrtitulo);
            NumTarjCredito = _numtarjcredito; otros = _otros; nro_otros = _nroOtros; Vlrtitulo = _vlrtitulo;

            stmysql2 = "select Nrotitulo as campo1,MotivoPago as campo2 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            CargaVarini.ExecuteQueryconec(stmysql2, myconect, "buscaFormapago", ref _nrotitulo, ref _motivopago);
            Nrotitulo = _nrotitulo; MotivoPago = _motivopago;
            return ok;
        }

        // ----------------------------------------------------------------
        // GrabaLiberaCangeEscogido
        // ----------------------------------------------------------------
        public bool GrabaLiberaCangeEscogido(ref DateTime fecha, int cuenta, decimal cheque, DateTime fechaingre, OdbcConnection conect, Form pertenece)
        {
            stmysql = "update dep_checanje set estado = 'L' where num_cuenta = '" + cuenta + "' and numcheque = '" + cheque + "' and fecingreso = '" + Strings.Format(fechaingre, Var.varini.PstForFec) + "'";
            ok = CargaVarini.ExecuteQueryconec(stmysql, conect, "GrabaLiberaCangeEscogido");
            return ok;
        }

        // ----------------------------------------------------------------
        // CalculaSaldoPromedio
        // ----------------------------------------------------------------
        public bool CalculaSaldoPromedio(int LineaAhorro, string NumCuenta, DateTime fechaini, DateTime fechafin,
            string cencos, string empresa, string codigoter, Form forma, OdbcConnection myconnect)
        {
            bool dummy = true; double dummy2 = 0;
            return CalculaSaldoPromedio(LineaAhorro, NumCuenta, fechaini, fechafin, cencos, empresa, codigoter, forma, myconnect, ref dummy, ref dummy2);
        }

        public bool CalculaSaldoPromedio(int LineaAhorro, string NumCuenta, DateTime fechaini, DateTime fechafin,
            string cencos, string empresa, string codigoter, Form forma, OdbcConnection myconnect,
            ref bool Imprime, ref double SaldoPromedio)
        {
            double saldot; int dias; int i = 0; int cantidadreg; double saldofinal = 0;
            double saldo_cta; int DiasMov = 0; double SaldoAcu = 0; double saldoinicial = 0;
            DateTime fecanterior;
            ERP.Core.Compartido.Controles.Barraprogress progreso = new ERP.Core.Compartido.Controles.Barraprogress("Calculando Saldo Promedio", forma);

            stmysql = "SELECT copmae.codigoter,copmae.lincred,copmae.numero,saldo.SALDO_INICIAL as SaldoInicial, "
                    + " maenit.nit,maenit.telefono1,maenit.direccion,maenit.FECHA_INGRESO, maenit.estado, copmae.FECFACT,"
                    + " maenit.apellido, maenit.nombre FROM cop_maecar  copmae  inner join"
                    + " sys_maenit maenit  on maenit.codigoter = copmae.codigoter  inner join cop_ahorro58 parame58 on  "
                    + " parame58.lincred = copmae.lincred left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter "
                    + " and saldo.lincred = copmae.lincred and saldo.numero = copmae.numero and saldo.periodo = " + Strings.Format(fechaini, "yyyyMM")
                    + " inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred "
                    + "left join cop_movimto m on m.codigoter =copmae.codigoter and "
                    + " m.lincred = copmae.lincred and m.numero = copmae.numero "
                    + "and m.fecha_Movto between '" + Strings.Format(fechaini, Var.varini.PstForFec) + "' and '" + Strings.Format(fechafin, Var.varini.PstForFec) + "' "
                    + "left join cop_docmto doc on m.compronte=doc.compronte and m.numero_domto=doc.numero_domto and "
                    + "doc.ANULADO<>'Y' left join sys_compro02 compro on doc.COMPRONTE =compro.CODIGO and compro.RESTRI_TESORERIA<>'Y' "
                    + "left join cop_codmov codmov on m.cod_movto = codmov.cod_movto and codmov.tipo_movto  = '7' "
                    + "where parame58.lincred = " + LineaAhorro + (cencos == "Todos" ? "" : " and ccosto='" + Strings.Right("00000000" + cencos, 8) + "'")
                    + (empresa == "Todas" ? "" : " and empresa='" + Strings.Right("0000" + empresa, 4) + "'") + (codigoter == "Todos" ? "" : " and copmae.codigoter='" + Strings.Right("00000000000000" + codigoter, 14) + "'")
                    + (NumCuenta == "Todas" ? "" : " and copmae.numero=" + NumCuenta) + " group by copmae.codigoter,copmae.lincred,copmae.numero, apellido,maenit.nombre, "
                    + "maenit.nit,saldo.SALDO_INICIAL ,maenit.telefono1,maenit.direccion,maenit.FECHA_INGRESO, maenit.estado, copmae.FECFACT";

            progreso.DefineMaximo(stmysql, myconnect);
            progreso.Show();
            DataSet DtDatos = new DataSet();
            int canreg2 = 0; int fila = 0;
            dias = (int)(fechafin - fechaini).TotalDays + 1;
            CargaVarini.ExecuteQueryDataset(stmysql, myconnect, "CalculaSaldoPromedio", DtDatos, "SaldoProm");
            cantidadreg = DtDatos.Tables["SaldoProm"].Rows.Count;
            saldo_cta = 0; fecanterior = fechaini; saldot = 0;

            while (i < cantidadreg)
            {
                DataRow _ri = DtDatos.Tables["SaldoProm"].Rows[i];
                if (Convert.IsDBNull(_ri["SaldoInicial"]))
                    saldot = 0;
                else
                    saldot = Convert.ToDouble(_ri["saldoInicial"]) * -1;
                saldoinicial = saldot;

                string stmysql3 = "select movdiario.codigoter, movdiario.lincred, movdiario.numero, movdiario.fecha_movto,movdiario.saldiario  from cop_movdiario_vw Movdiario inner join cop_saldos_vw saldos on saldos.codigoter = movdiario.codigoter and saldos.lincred = movdiario.lincred "
                    + " and saldos.numero = movdiario.numero and saldos.periodo = " + Strings.Format(fechaini, "yyyyMM") + " where  movdiario.codigoter = '" + Convert.ToString(_ri["codigoter"]) + "' and movdiario.lincred = " + Convert.ToString(_ri["lincred"]) + " and movdiario.numero = " + Convert.ToString(_ri["numero"])
                    + " and movdiario.fecha_movto between '" + Strings.Format(fechaini, Var.varini.PstForFec) + "' and '" + Strings.Format(fechafin, Var.varini.PstForFec) + "'";

                DataSet myRead = new DataSet();
                CargaVarini.ExecuteQueryDataset(stmysql3, myconnect, "CalculaSaldoPromedio", myRead, "TblSalProme");
                canreg2 = myRead.Tables["TblSalProme"].Rows.Count;

                saldo_cta = saldot;
                fecanterior = fechaini;
                while (fila < canreg2)
                {
                    DataRow _rf = myRead.Tables["TblSalProme"].Rows[fila];
                    DiasMov = (int)(Convert.ToDateTime(_rf["fecha_movto"]) - fecanterior).TotalDays;
                    if (DiasMov != 0) SaldoAcu += saldo_cta * DiasMov;
                    saldo_cta += (Convert.ToDouble(_rf["saldiario"]) * -1);
                    if (dias == 0) { if (saldo_cta < saldot) saldot = saldo_cta; }
                    else saldot = saldo_cta;
                    fecanterior = Convert.ToDateTime(_rf["fecha_movto"]);
                    fila += 1;
                }
                myRead.Dispose();

                int ddFin = Convert.ToInt32(Strings.Format(fechafin, "dd"));
                if (DiasMov < ddFin)
                {
                    while (DiasMov != ddFin) { SaldoAcu += saldo_cta; DiasMov += 1; }
                }
                if (dias > 0) { saldot = Math.Round(SaldoAcu / dias, 2); SaldoPromedio = saldot; }

                // car.BuscaSaldoObligacion(Convert.ToString(_ri["codigoter"]), Convert.ToString(_ri["lincred"]), Convert.ToString(_ri["numero"]), Strings.Format(fechafin, "yyyyMM"), myconnect, ref saldofinal); // ERROR: CS1503
                saldofinal = (saldofinal < 0) ? saldofinal * -1 : saldofinal;
                CargaArregloPromedioSaldo(Convert.ToDouble(_ri["numero"]), Convert.ToString(_ri["codigoter"]),
                    Convert.ToString(_ri["apellido"]) + " " + Convert.ToString(_ri["nombre"]),
                    saldoinicial, saldofinal, SaldoPromedio, Convert.ToDateTime(_ri["FECFACT"]),
                    Convert.ToString(_ri["nit"]), Convert.ToString(_ri["direccion"]),
                    Convert.ToString(_ri["telefono1"]), Convert.ToString(_ri["estado"]),
                    Convert.ToDateTime(_ri["FECHA_INGRESO"]));

                fila = 0; SaldoPromedio = 0; DiasMov = 0; saldo_cta = 0; SaldoAcu = 0;
                progreso.PerformStep();
                i = i + 1;
            }
            progreso.Dispose();
            progreso.Close();
            return (i > 0);
        }

        // ----------------------------------------------------------------
        // AgregaColumnasPromedioSaldo
        // ----------------------------------------------------------------
        public void AgregaColumnasPromedioSaldo()
        {
            string a = " "; double b = 0; DateTime d = new DateTime(1950, 1, 1);
            DatPromedioSaldo.Tables.Add("SaldoPromedio");
            DatPromedioSaldo.Tables[0].Columns.Add("num_cuenta", b.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("codigoter", a.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("Nombre", a.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("SaldoIni", b.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("SaldoFin", b.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("SaldoProm", b.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("FecApertura", d.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("cedula", a.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("direccion_asociado", a.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("telefono_asociado", a.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("estado", a.GetType());
            DatPromedioSaldo.Tables[0].Columns.Add("FecIngreso", d.GetType());
            string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            DatPromedioSaldo.Tables["SaldoPromedio"].WriteXml(appPath + "\\SaldoPromedio.xml", System.Data.XmlWriteMode.WriteSchema);
        }

        // ----------------------------------------------------------------
        // CargaArregloPromedioSaldo
        // ----------------------------------------------------------------
        public void CargaArregloPromedioSaldo(double num_cuenta, string codigoter, string nombre,
            double SaldoIni, double SaldoFin, double SaldoProm, DateTime FecApertura,
            string cedula, string direccion_asociado, string telefono_asociado, string estado, DateTime FecIngreso)
        {
            lista.Clear();
            lista.Add(num_cuenta); lista.Add(codigoter); lista.Add(nombre);
            lista.Add(SaldoIni); lista.Add(SaldoFin); lista.Add(SaldoProm);
            lista.Add(FecApertura); lista.Add(cedula); lista.Add(direccion_asociado);
            lista.Add(telefono_asociado); lista.Add(estado); lista.Add(FecIngreso);
            DatPromedioSaldo.Tables["SaldoPromedio"].Rows.Add(lista.ToArray());
        }

        // ----------------------------------------------------------------
        // LimpiaDatasetPromedioSaldo
        // ----------------------------------------------------------------
        public void LimpiaDatasetPromedioSaldo()
        {
            DatPromedioSaldo.Tables["SaldoPromedio"].Rows.Clear();
        }

        // ----------------------------------------------------------------
        // ImprimirReporte
        // ----------------------------------------------------------------
        public void ImprimirReporte(Form pertenese, int periodo, string empresa, string cod_empresa, string nit, string direccion, string telefono, string ccosto,
            int linea, DateTime fecha_ini, DateTime fecha_fin, string asociados, string nomempresa, string nomccosto, string descripcion)
        {
            ERP.Core.Compartido.Reportes.reporte r = new ERP.Core.Compartido.Reportes.reporte("cop_rsalpromahor", false);
            ERP.Core.Compartido.Reportes.config_report confi_report = new ERP.Core.Compartido.Reportes.config_report();
            r.SetDataSource(DatPromedioSaldo.Tables["SaldoPromedio"]);
            r.SetParameterValue("Empresa", empresa);
            r.SetParameterValue("Periodo", periodo);
            r.SetParameterValue("nit", nit);
            r.SetParameterValue("direccion", direccion);
            r.SetParameterValue("telefono", telefono);
            r.SetParameterValue("fecha_ini", fecha_ini);
            r.SetParameterValue("fecha_fin", fecha_fin);
            r.SetParameterValue("cod_empresa", cod_empresa);
            r.SetParameterValue("ccosto", ccosto);
            r.SetParameterValue("linea", linea);
            r.SetParameterValue("asociados", asociados);
            r.SetParameterValue("nomempresa", nomempresa);
            r.SetParameterValue("nomccosto", nomccosto);
            r.SetParameterValue("descripcion", descripcion);
            confi_report.confi_reportes(pertenese, r);
        }

        // ----------------------------------------------------------------
        // CalculaSaldoPromedioCuenta
        // ----------------------------------------------------------------
        public bool CalculaSaldoPromedioCuenta(string codigoter, int LineaAhorro, string NumCuenta, DateTime fechaini, DateTime fechafin, OdbcConnection myconnect)
        {
            bool dummy = true; double dummy2 = 0;
            return CalculaSaldoPromedioCuenta(codigoter, LineaAhorro, NumCuenta, fechaini, fechafin, myconnect, ref dummy, ref dummy2);
        }

        public bool CalculaSaldoPromedioCuenta(string codigoter, int LineaAhorro, string NumCuenta, DateTime fechaini, DateTime fechafin, OdbcConnection myconnect,
            ref bool Imprime, ref double SaldoPromedio)
        {
            double saldot = 0; int dias; int i = 0; int cantidadreg;
            double saldo_cta = 0; int DiasMov = 0; double SaldoAcu = 0; double saldoinicial = 0;
            DateTime fecanterior; double SaldoDiario = 0;

            stmysql = "select movdiario.codigoter, movdiario.lincred, movdiario.numero, movdiario.fecha_movto, sal.saldo_inicial,"
                    + "(sum(movdiario.VLR_DEBITO) - sum(movdiario.VLR_CREDITO)) AS saldiario from cop_movimto movdiario "
                    + "inner join cop_salmaecar saldos on saldos.codigoter = movdiario.codigoter "
                    + "and saldos.lincred = movdiario.lincred and saldos.numero = movdiario.numero "
                    + "and saldos.periodo = " + Strings.Format(fechaini, "yyyyMM")
                    + " left join cop_salmaecar sal on movdiario.codigoter=sal.codigoter and movdiario.lincred=sal.lincred "
                    + "and movdiario.numero=sal.numero and saldos.periodo=sal.periodo"
                    + "left join cop_docmto doc on movdiario.compronte=doc.compronte and movdiario.numero_domto=doc.numero_domto and "
                    + "doc.ANULADO<>'Y' left join sys_compro02 compro on doc.COMPRONTE =compro.CODIGO and compro.RESTRI_TESORERIA<>'Y' "
                    + "left join cop_codmov codmov on movdiario.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9') "
                    + "where  movdiario.codigoter ='" + codigoter + "' and movdiario.lincred =" + LineaAhorro + " and movdiario.numero =" + NumCuenta
                    + " and movdiario.fecha_movto between '" + Strings.Format(fechaini, Var.varini.PstForFec) + "' and '" + Strings.Format(fechafin, Var.varini.PstForFec) + "' "
                    + "group by movdiario.codigoter, movdiario.lincred, movdiario.numero, movdiario.fecha_movto, sal.saldo_inicial";

            DataSet DtDatos = new DataSet();
            CargaVarini.ExecuteQueryDataset(stmysql, myconnect, "CalculaSaldoPromedioCuenta", DtDatos, "SaldoPromCta");
            cantidadreg = DtDatos.Tables["SaldoPromCta"].Rows.Count;
            dias = (int)(fechafin - fechaini).TotalDays + 1;
            fecanterior = fechaini;

            while (i < cantidadreg)
            {
                DataRow _r = DtDatos.Tables["SaldoPromCta"].Rows[i];
                if (i == 0)
                {
                    if (Convert.IsDBNull(_r["Saldo_Inicial"])) saldot = 0;
                    else saldot = Convert.ToDouble(_r["saldo_Inicial"]) * -1;
                    saldoinicial = saldot;
                    saldo_cta = saldot;
                    fecanterior = fechaini;
                }
                DiasMov = (int)(Convert.ToDateTime(_r["fecha_movto"]) - fecanterior).TotalDays;
                if (DiasMov != 0) SaldoAcu += saldo_cta * DiasMov;
                if (_r["saldiario"] is DBNull) SaldoDiario = 0; else SaldoDiario = Convert.ToDouble(_r["saldiario"]);
                saldo_cta += (SaldoDiario * -1);
                if (dias == 0) { if (saldo_cta < saldot) saldot = saldo_cta; }
                else saldot = saldo_cta;
                fecanterior = Convert.ToDateTime(_r["fecha_movto"]);
                i = i + 1;
            }

            int ddFin = Convert.ToInt32(Strings.Format(fechafin, "dd"));
            if (DiasMov < ddFin)
            {
                while (DiasMov != ddFin) { SaldoAcu += saldo_cta; DiasMov += 1; }
            }
            if (dias > 0) { saldot = Math.Round(SaldoAcu / dias, 2); SaldoPromedio = saldot; }
            return (i > 0);
        }

        // ----------------------------------------------------------------
        // LIquidaDiarioAcumuladoMes
        // ----------------------------------------------------------------
        public void LIquidaDiarioAcumuladoMes(string Cpte, string ConseCpte, int LineaArros, DateTime FecIni, DateTime FecFin, DateTime FechaLiquidacion,
            string Usuario, Form Myforma, OdbcConnection Myconect, string empresa, string cencosto, bool LiqRetirado, string StTercerizaCruce)
        {
            DataSet DsLiqInt = new DataSet(); DataSet DsInforme = new DataSet();
            double Fila = 0; int dias = 1; double SaldoDia = 0; int Primer = 0;
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string cuenta = null; string CuentaNew = null;
            double SaldoInicial = 0; int DiasMov = 0; double InteresDia = 0; double Interes = 0; decimal DbTasaInt = 0;
            string codigoter = " "; int lincred = 0; double numero = 0; int DiasLiq = 0; int MesesIngreso = 0;
            string Nombre = " "; double BaseLIq = 0; double SaldoDiario = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquida Intereses Acumulado Diario", Myforma);
            int MesLiq = 0; double SaldoMinimoInt = 0;
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            DateTime fechaUltimaLiquidacion = new DateTime(1950, 1, 1);
            string estado = "Y";
            string _su = " "; decimal _tasaint = 0; double _vlrmin = 0; double _tasaretfte = 0;
            int _forliq = 0; int _cptoint = 0; DateTime _fecing = new DateTime(1950, 1, 1);

            DsLiqInt.Tables.Add("tbldatosLiq");
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("CODIGOTER", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NOMBRE", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("LINCRED", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NUMERO", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("BASE", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("INTERES", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("FechaUltLiquid", typeof(DateTime));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("ValidaLiquida", typeof(string));

            stbuilder.Append(" select copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,saldo.Saldo_inicial as saldoInicial,");
            stbuilder.Append(" parame58.porpago_int,parame58.forliq,parame58.salmin_int,copmae.plazo,copmae.cuota,maenit.FECHA_INGRESO,(sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) AS saldiario,copmae.FECCIERRE,maehor.estado ");
            stbuilder.Append(" from cop_movimto movto ");
            stbuilder.Append(" RIGHT outer join cop_docmto doc on movto.compronte=doc.compronte ");
            stbuilder.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto between '" + Strings.Format(FecIni, Var.varini.PstForFec) + "' and '" + Strings.Format(FecFin, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
            stbuilder.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
            stbuilder.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
            stbuilder.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
            stbuilder.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FecIni, "yyyyMM") + "' ");
            stbuilder.Append(" left join dep_maeahor maehor on  saldo.numero = maehor.num_cuenta  ");
            stbuilder.Append(" where maenit.estado<>'T' and parame58.lincred = " + LineaArros + empresa + cencosto + (LiqRetirado == false ? " and maenit.estado<>'R' " : " "));
            stbuilder.Append(" group by copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,saldo.Saldo_inicial,");
            stbuilder.Append(" parame58.porpago_int,parame58.forliq,parame58.salmin_int,copmae.plazo,copmae.cuota,maenit.FECHA_INGRESO,copmae.FECCIERRE,maehor.estado  ");
            stbuilder.Append(" order by copmae.codigoter, copmae.lincred, copmae.numero,movto.fecha_movto");

            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), Myconect, "LIquidaDiarioAcumuladoMes", DsLiqInt, "tblLiqInt");
            msgbarra.ValorMinimoMaximo(0, DsLiqInt.Tables["tblLiqInt"].Rows.Count);
            msgbarra.Show();

            while (Fila < DsLiqInt.Tables["tblLiqInt"].Rows.Count)
            {
                DataRow _r = DsLiqInt.Tables["tblLiqInt"].Rows[(int)Fila];
                SaldoMinimoInt = Convert.ToDouble(_r["salmin_int"]);
                CuentaNew = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);

                if (cuenta != CuentaNew)
                {
                    if (_r["SaldoInicial"] is DBNull) SaldoDia = 0;
                    else SaldoDia = Convert.ToDouble(_r["saldoInicial"]) * -1;

                    DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, 0, Interes, fechaUltimaLiquidacion, estado);
                    dias = 1; Interes = 0; BaseLIq = 0; InteresDia = 0;
                    codigoter = Convert.ToString(_r["codigoter"]);
                    lincred = Convert.ToInt32(_r["lincred"]);
                    numero = Convert.ToDouble(_r["numero"]);
                    cuenta = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                    MesesIngreso = (int)((FechaLiquidacion.Year - Convert.ToDateTime(_r["fecha_ingreso"]).Year) * 12
                        + FechaLiquidacion.Month - Convert.ToDateTime(_r["fecha_ingreso"]).Month);
                    // DbTasaInt = paramcop.BuscarTasasporPlazos(lincred, Convert.ToInt32(_r["plazo"]), Convert.ToDouble(_r["cuota"]), MesesIngreso, Myconect); // ERROR: CS1061
                    if (DbTasaInt <= 0) DbTasaInt = Convert.ToDecimal(_r["porpago_int"]);
                }

                int _forliqRow = Convert.ToInt32(_r["forliq"]);
                if (_r["fecha_movto"] is DBNull) { MesLiq = Convert.ToInt32(Strings.Format(FecFin, "MM")); DiasMov = Convert.ToInt32(Strings.Format(FecFin, "dd")); }
                else { DiasMov = Convert.ToInt32(Strings.Format(Convert.ToDateTime(_r["fecha_movto"]), "dd")); MesLiq = Convert.ToInt32(Strings.Format(Convert.ToDateTime(_r["fecha_movto"]), "MM")); }

                decimal _tasaCalcular = 0;
                if (DiasMov > 0)
                {
                    if (SaldoMinimoInt <= SaldoDia)
                    {
                        while (dias < DiasMov) { InteresDia = CalculaInteres(_forliqRow, DbTasaInt, 1, SaldoDia); Interes += InteresDia; dias += 1; }
                    }
                    if (_r["saldiario"] is DBNull) SaldoDiario = 0; else SaldoDiario = Convert.ToDouble(_r["saldiario"]);
                    SaldoDia += (SaldoDiario * -1);
                    if (SaldoMinimoInt <= SaldoDia) { InteresDia = CalculaInteres(_forliqRow, DbTasaInt, 1, SaldoDia); Interes += InteresDia; BaseLIq += SaldoDia; }
                }

                Nombre = Convert.ToString(_r["Apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                if (!(_r["FECCIERRE"] is DBNull)) fechaUltimaLiquidacion = Convert.ToDateTime(_r["FECCIERRE"]);
                else fechaUltimaLiquidacion = new DateTime(1950, 1, 1);
                if (!(_r["estado"] is DBNull))
                    estado = Convert.ToString(_r["estado"]).Trim() == "3" ? "N" : "Y";
                else estado = "Y";
                dias += 1;
                Fila += 1;

                if (DsLiqInt.Tables["tblLiqInt"].Rows.Count > (int)Fila)
                {
                    DataRow _rn = DsLiqInt.Tables["tblLiqInt"].Rows[(int)Fila];
                    CuentaNew = Convert.ToString(_rn["codigoter"]) + Convert.ToString(_rn["lincred"]) + Convert.ToString(_rn["numero"]);
                    if (cuenta != CuentaNew)
                    {
                        int DiasMas = 0;
                        DateTime FecFin2 = Convert.ToDateTime(FecFin.ToShortDateString());
                        DateTime FechaDif = new DateTime(FecFin2.Year, MesLiq, 1);
                        while (FechaDif <= FecFin2)
                        {
                            if (FechaDif.Month == FecFin2.Month && FechaDif.Year == FecFin2.Year)
                                DiasMas += (Convert.ToInt32(Strings.Format(FecFin2, "dd")) - dias);
                            else
                                DiasMas += DateTime.DaysInMonth(Convert.ToInt32(Strings.Format(FechaDif, "yyyy")), Convert.ToInt32(Strings.Format(FechaDif, "MM")));
                            FechaDif = FechaDif.AddMonths(1);
                        }
                        if ((DiasMas + 1) > 0 && SaldoMinimoInt <= SaldoDia) { DiasMas += 1; Interes += (InteresDia * DiasMas); }
                    }
                }
                msgbarra.PerformStep();
            }

            DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, 0, Interes, fechaUltimaLiquidacion, estado);
            if (!Information.IsNumeric(ConseCpte)) ConseCpte = "0";
            msgbarra.Close(); msgbarra.Dispose();

            DiasLiq = (int)(FecFin - FecIni).TotalDays;
            DiasLiq = clsliqcredito.CalculaDias(FecFin, FecIni) + 1;
            DsInforme = LiquidacionInteresesCuentasAhorro(DsLiqInt.Tables["tbldatosliq"], Cpte, Convert.ToDouble(ConseCpte), LineaArros.ToString(), FechaLiquidacion, Usuario, DiasLiq, StTercerizaCruce, Myforma, Myconect, FecIni, FecFin);
            if (Convert.ToDouble(ConseCpte) == 0)
                ImprimirLiquidacionIntereses(LineaArros.ToString(), FechaLiquidacion, DsInforme, 5, Myforma, Myconect);
        }

        // ----------------------------------------------------------------
        // ImprimirLiquidacionIntereses
        // ----------------------------------------------------------------
        public void ImprimirLiquidacionIntereses(string Lincred, DateTime fechaLiquidacion, DataSet DsDataSet, int Opcion, Form Myforma, OdbcConnection myconnect)
        {
            string descripcion = " "; string Nit = " "; string Direccion = " "; string Telefono = " "; string NomEmpresa = " ";
            string _u = " ";
            ERP.Core.Compartido.Reportes.reporte r = new ERP.Core.Compartido.Reportes.reporte("cop_rliquidahor", false);
            ERP.Core.Compartido.Reportes.config_report confi_report = new ERP.Core.Compartido.Reportes.config_report();
            // car.BuscaLinea(Lincred, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref descripcion); // ERROR: CS7036
            // paramsys.BuscarCompania(Var.varini.SptCodEmpr, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref Nit, ref Direccion, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref NomEmpresa, ref Telefono); // ERROR: CS1061
            r.SetDataSource(DsDataSet.Tables["DatosLiq"]);
            r.SetParameterValue("Empresa", NomEmpresa);
            r.SetParameterValue("Periodo", Strings.Format(fechaLiquidacion, "yyyyMM"));
            r.SetParameterValue("nit", Nit);
            r.SetParameterValue("direccion", Direccion);
            r.SetParameterValue("telefono", Telefono);
            r.SetParameterValue("opcion", Opcion);
            r.SetParameterValue("descripcion", descripcion);
            confi_report.confi_reportes(Myforma, r);
        }

        // ----------------------------------------------------------------
        // LiquidacionInteresesCuentasAhorro (private)
        // ----------------------------------------------------------------
        private DataSet LiquidacionInteresesCuentasAhorro(DataTable DsDatable, string Cpte, double ConseCpte, string lincred, DateTime FechaLiquidacion, string Usuario, int DiasLiq, string StTercerizaCruce, Form Myforma, OdbcConnection myconnect)
        {
            return LiquidacionInteresesCuentasAhorro(DsDatable, Cpte, ConseCpte, lincred, FechaLiquidacion, Usuario, DiasLiq, StTercerizaCruce, Myforma, myconnect, new DateTime(1950, 1, 1), new DateTime(1950, 1, 1));
        }

        private DataSet LiquidacionInteresesCuentasAhorro(DataTable DsDatable, string Cpte, double ConseCpte, string lincred, DateTime FechaLiquidacion, string Usuario, int DiasLiq, string StTercerizaCruce, Form Myforma, OdbcConnection myconnect, DateTime fecha_Ini_liq, DateTime fecha_final_liq)
        {
            double fila = 0; double SalMinimoInt = 0; decimal TasaInt = 0; double VlrMinRetfte = 0; double TasaRetfte = 0; int Forliq = 0; int CptoInteres = 0;
            string CptoRetfte = " "; string CptoIntAhorro = " "; string CptoAho = " "; double Interes = 0;
            int plazo = 0; double cuota = 0; string StCuentaCruce = "999999999999";
            bool ActDatos = false; int CptoInteresAhorro = 0; decimal DbTasaInt = 0; DateTime FecIngreso = new DateTime(1950, 1, 1); int MesesIngreso = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Contabilizando Liquidacion", Myforma);
            DataSet DatLiquidacion = new DataSet(); decimal tasaCalcular = 0;
            string _u = " "; int _perpag = 0;

            DatLiquidacion.Tables.Add("DatosLiq");
            DatLiquidacion.Tables[0].Columns.Add("lincred", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("num_cuenta", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("codigoter", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("Nombre", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("base", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("interes", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("retfte", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("Total", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("TasaInt", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("DiasLiq", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("FechaUltimaliq", typeof(DateTime));
            DatLiquidacion.Tables[0].Columns.Add("ValidaLiq", typeof(string));

            msgbarra.ValorMinimoMaximo(0, DsDatable.Rows.Count);
            msgbarra.Show();

            string _sl = SalMinimoInt.ToString(); string _ti = TasaInt.ToString(); string _vm = VlrMinRetfte.ToString();
            string _tr = TasaRetfte.ToString(); string _fl = Forliq.ToString(); string _ci = CptoInteres.ToString();
            // BuscaLineaAhorro(lincred, myconnect, Navega.Ninguno, ref _u, ref _u, ref _u, ref _sl, ref _u, ref _u, ref _ti, ref _vm, ref _tr, ref _u, ref _fl, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _ci); // ERROR: CS1501
            if (Information.IsNumeric(_sl)) SalMinimoInt = Convert.ToDouble(_sl);
            if (Information.IsNumeric(_ti)) TasaInt = Convert.ToDecimal(_ti);
            if (Information.IsNumeric(_vm)) VlrMinRetfte = Convert.ToDouble(_vm);
            if (Information.IsNumeric(_tr)) TasaRetfte = Convert.ToDouble(_tr);
            if (Information.IsNumeric(_fl)) Forliq = Convert.ToInt32(_fl);
            if (Information.IsNumeric(_ci)) CptoInteres = Convert.ToInt32(_ci);

            // paramsys.BuscarCompania(Var.varini.SptCodEmpr, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref CptoAho, ref _u, ref CptoRetfte, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref CptoIntAhorro); // ERROR: CS1061

            if (Cpte.Trim() != "")
                // msgcnt_cnt.BuscaComprobante(Cpte, 0, false, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref StCuentaCruce); // ERROR: CS1503, CS1620

            CptoInteresAhorro = (CptoInteres == 9999) ? Convert.ToInt32(lincred) : CptoInteres;

            while (fila < DsDatable.Rows.Count)
            {
                DataRow _r = DsDatable.Rows[(int)fila];
                ActDatos = (Cpte.Trim() != "");

                // paramcop.BuscaAsociado(Convert.ToString(_r["codigoter"]), myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref FecIngreso); // ERROR: CS7036
                MesesIngreso = (FechaLiquidacion.Year - FecIngreso.Year) * 12 + (FechaLiquidacion.Month - FecIngreso.Month);
                // msgcop_car.BuscaObligacion(Convert.ToString(_r["codigoter"]), lincred, Convert.ToString(_r["numero"]), myconnect, ref _u, ref _u, ref _u, ref _u, ref cuota, ref _u, ref _u, ref _u, ref plazo); // ERROR: CS7036
                // DbTasaInt = paramcop.BuscarTasasporPlazos(Convert.ToInt32(lincred), (plazo / 30), cuota, MesesIngreso, myconnect); // ERROR: CS1061
                if (DbTasaInt <= 0) DbTasaInt = TasaInt;

                switch (Forliq)
                {
                    // case 1: tasaCalcular = paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaNominalAnual, DbTasaInt, DiasLiq); break; // ERROR: CS1503
                    // case 2: tasaCalcular = paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaEfectivaAnual, DbTasaInt, DiasLiq); break; // ERROR: CS1503
                }

                if (Convert.ToDateTime(_r["FechaUltLiquid"]) < fecha_Ini_liq && Convert.ToString(_r["ValidaLiquida"]) == "Y")
                    LiquidaInteresMes(Forliq, tasaCalcular, Convert.ToDouble(_r["Base"]), SalMinimoInt, VlrMinRetfte, (decimal)TasaRetfte, Convert.ToString(_r["codigoter"]), Convert.ToString(_r["NOMBRE"]), lincred, Convert.ToString(_r["numero"]), Cpte, ConseCpte.ToString(), CptoRetfte, CptoInteresAhorro.ToString(), CptoIntAhorro, ActDatos, Usuario, FechaLiquidacion, DatLiquidacion, myconnect, Convert.ToDouble(_r["INTERES"]), DiasLiq, new DateTime(1950,1,1), new DateTime(1950,1,1), StTercerizaCruce, StCuentaCruce, fecha_final_liq, Convert.ToDateTime(_r["FechaUltLiquid"]));
                else
                    DatLiquidacion.Tables["DatosLiq"].Rows.Add(lincred, Convert.ToString(_r["numero"]), Convert.ToString(_r["codigoter"]), Convert.ToString(_r["NOMBRE"]), Convert.ToDouble(_r["Base"]), Convert.ToDouble(_r["INTERES"]), 0, 0, tasaCalcular, DiasLiq, Convert.ToDateTime(_r["FechaUltLiquid"]), "N");

                msgbarra.PerformStep();
                fila += 1;
            }
            msgbarra.Close(); msgbarra.Dispose();
            return DatLiquidacion;
        }

        // ----------------------------------------------------------------
        // LiquidaInteresMes
        // ----------------------------------------------------------------
        public void LiquidaInteresMes(int forliq, decimal porpago_int, double saldot, double SALMIN_INT, double VlrRetMin,
            decimal POR_RFTE, string codigoter, string nombre, string lincred, string numero,
            string Compcte, string ConseCpte, string CptoRetfte, string CptoIntereses, string CptoMovtoInt, bool Actualiza,
            string Usuario, DateTime FecLiquidacion, DataSet DatLiquidacion, OdbcConnection myconnect)
        {
            LiquidaInteresMes(forliq, porpago_int, saldot, SALMIN_INT, VlrRetMin, POR_RFTE, codigoter, nombre, lincred, numero, Compcte, ConseCpte, CptoRetfte, CptoIntereses, CptoMovtoInt, Actualiza, Usuario, FecLiquidacion, DatLiquidacion, myconnect, 0, 0, new DateTime(1950, 1, 1), new DateTime(1950, 1, 1), "N", "N", new DateTime(1950, 1, 1), new DateTime(1950, 1, 1));
        }

        public void LiquidaInteresMes(int forliq, decimal porpago_int, double saldot, double SALMIN_INT, double VlrRetMin,
            decimal POR_RFTE, string codigoter, string nombre, string lincred, string numero,
            string Compcte, string ConseCpte, string CptoRetfte, string CptoIntereses, string CptoMovtoInt, bool Actualiza,
            string Usuario, DateTime FecLiquidacion, DataSet DatLiquidacion, OdbcConnection myconnect,
            double InteresLiq, int DiasInt, DateTime Fecini, DateTime Fecfin,
            string StTercerizaContrapartida, string StCuentaCruce, DateTime fecha_final_liq, DateTime fecha_Ultima_liq)
        {
            decimal i = 0; double interes = 0; double baseret = 0; double retfte = 0; double vlrbase = 0;
            int dias = 0; string MOVINTERES = "99"; double ValorInteres = 0;
            string _u = " "; string NitAsoc = "  "; string validarPagarrtefte = "   ";

            ok = msgcop_car.ValidarClienteenListaNegra(codigoter, false, myconnect);
            if (DiasInt > 0) dias = DiasInt;
            i = porpago_int;
            if (saldot < 0 || saldot == 99999999999999) i = 0;
            if (SALMIN_INT > saldot) i = 0;

            if (InteresLiq == 0)
            {
                switch (forliq)
                {
                    case 1: interes = Math.Round((double)Math.Round((decimal)(saldot * (double)i), 8) * dias, 0); break;
                    case 2: interes = Math.Round((double)Math.Round((decimal)(saldot * (double)i), 8), 0); break;
                }
            }
            else interes = InteresLiq;

            ValorInteres = interes;
            baseret = (dias != 0) ? interes / dias : 0;
            retfte = 0;
            // msgcop_car.BuscaAsociado(codigoter, myconnect, ref _u, ref NitAsoc); // ERROR: CS7036

            if (baseret > VlrRetMin)
            {
                // ok = msgcnt_cnt.BuscarTercero(NitAsoc, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref validarPagarrtefte); // ERROR: CS7036
                if (!ok)
                    retfte = Math.Round((interes * (double)POR_RFTE) / 100);
                else if (validarPagarrtefte.Trim() == "Y")
                    retfte = Math.Round((interes * (double)POR_RFTE) / 100);
                else
                    retfte = 0;

                interes = interes - retfte;
                vlrbase = (POR_RFTE > 0) ? Math.Round(retfte / (double)(POR_RFTE / 100)) : 0;
            }

            if (Actualiza)
            {
                if (interes > 0)
                    // car.GrabaMovimiento(Compcte, Convert.ToDouble(ConseCpte), codigoter, CptoIntereses, numero, Strings.Format(FecLiquidacion, "yyyyMM"), // ERROR: CS1503
                        // CptoMovtoInt, FecLiquidacion, 0, interes, "LIQUIDACION DE INTERESES", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, false, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref fecha_Ultima_liq, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, lincred); // ERROR: CS1503
                if (retfte > 0)
                    // car.GrabaMovimiento(Compcte, Convert.ToDouble(ConseCpte), codigoter, lincred, numero, Strings.Format(FecLiquidacion, "yyyyMM"), CptoRetfte, FecLiquidacion, 0, retfte, "RETENCION EN LA FUENTE - LIQ. INT.", Usuario, // ERROR: CS1503, CS1620
                        // myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, false, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, vlrbase); // ERROR: CS1503, CS1620
                if (StTercerizaContrapartida == "Y" && StCuentaCruce != "999999999999" && (interes + retfte) > 0)
                    // car.GrabaMovimiento(Compcte, Convert.ToDouble(ConseCpte), "99999999999999", "9999", "0", Strings.Format(FecLiquidacion, "yyyyMM"), // ERROR: CS1503
                        // "02", FecLiquidacion, interes + retfte, 0, "LIQUIDACION DE INTERESES", Usuario, myconnect, ref _u, ref StCuentaCruce, ref NitAsoc, ref _u, ref _u, ref _u, ref _u, false); // ERROR: CS1503
                if (interes > 0)
                    GrabaCausacionAhorro(codigoter, Convert.ToInt32(lincred), Convert.ToDouble(numero), fecha_final_liq, "0", myconnect, new DateTime(1950,1,1), "", "A");
            }
            else
            {
                if (interes > 0)
                    DatLiquidacion.Tables["DatosLiq"].Rows.Add(lincred, numero, codigoter, nombre, saldot, ValorInteres, retfte, (ValorInteres - retfte), i, dias, fecha_Ultima_liq, "Y");
                else
                    DatLiquidacion.Tables["DatosLiq"].Rows.Add(lincred, numero, codigoter, nombre, saldot, ValorInteres, retfte, (ValorInteres - retfte), i, dias, fecha_Ultima_liq, "N");
            }
        }

        // ----------------------------------------------------------------
        // VerificarSobreGiro (private)
        // ----------------------------------------------------------------
        private bool VerificarSobreGiro(double SaldoCuenta, double ValorMovto, string usuario,
            Form forma, OdbcConnection myconnect)
        {
            string dummy1 = " "; double dummy2 = 0;
            return VerificarSobreGiro(SaldoCuenta, ValorMovto, usuario, forma, myconnect, ref dummy1, ref dummy2);
        }

        private bool VerificarSobreGiro(double SaldoCuenta, double ValorMovto, string usuario,
            Form forma, OdbcConnection myconnect, ref string usuaprobo, ref double VlrSobreGiro)
        {
            // MsgConfig.FrmLogeo frmlogin = new MsgConfig.FrmLogeo(myconnect); // ERROR: CS0246
            bool sobregiro = false; ok = false;
            string _u = " ";
            // paramsys.BuscaUsuario(usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref sobregiro); // ERROR: CS1501
            if (ValorMovto > SaldoCuenta)
            {
                if (Interaction.MsgBox("Esta cuenta va a quedar en sobregiro. Desea continuar?", MsgBoxStyle.YesNo) == MsgBoxResult.Yes)
                {
                    if (!sobregiro)
                    {
                        // if (frmlogin.ShowDialog(forma) == DialogResult.OK) { VlrSobreGiro = ValorMovto - SaldoCuenta; usuaprobo = Convert.ToString(frmlogin.Tag); ok = true; } // ERROR: CS0103
                    }
                    // else { VlrSobreGiro = ValorMovto - SaldoCuenta; usuaprobo = usuario; ok = true; } // ERROR: CS0103
                }
            }
            // else ok = true; // ERROR: CS0103
            return ok;
        }

        // ----------------------------------------------------------------
        // Buscahuella
        // ----------------------------------------------------------------
        public void Buscahuella(string Codigoter, OdbcConnection myconnect, DataSet DsDataset)
        {
            DataSet DsDattaset = new DataSet();
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            try { if (DsDataset != null) DsDataset.Tables.Remove("tblhuella"); } catch (Exception) { }
            StBuilder.Append("Select StTextHuella from cop_huellafirma where codigoter = '" + Codigoter + "'");
            CargaVarini.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "Buscahuella", DsDattaset, "tblhuella");
        }

        // ----------------------------------------------------------------
        // CalculaInteres (private)
        // ----------------------------------------------------------------
        private double CalculaInteres(int Forliq, decimal TasaInt, int Dias, double Saldo)
        {
            decimal ic = 0; double interes;
            switch (Forliq)
            {
                case 1: ic = Math.Round((((TasaInt / 12) / 30) / 100), 6); break;
                case 2: ic = Math.Round((decimal)(Math.Pow((double)(1 + TasaInt / 100), 1.0 / (360.0 / Dias)) - 1), 5); break;
            }
            interes = Math.Round(Saldo * (double)ic) * Dias;
            return interes;
        }

        // ----------------------------------------------------------------
        // CalculaMaxRetiro
        // ----------------------------------------------------------------
        public double CalculaMaxRetiro(double numcuenta, double saldo, double saldocanje,
            DateTime fecha, int clasemovto, int formapago,
            OdbcConnection myconect)
        {
            double dummy = 0;
            return CalculaMaxRetiro(numcuenta, saldo, saldocanje, fecha, clasemovto, formapago, myconect, ref dummy);
        }

        public double CalculaMaxRetiro(double numcuenta, double saldo, double saldocanje,
            DateTime fecha, int clasemovto, int formapago,
            OdbcConnection myconect, ref double Vlr4xMil)
        {
            int lincred = 0; double SaldoDisponible; string ForAplGmf = " "; int RetCheGmf = 0;
            double Vlr4x1000; double totmaxret = 0; double saldominimo = 0;
            string _u = " ";

            // ok = BuscarCuentaAhorro(numcuenta, myconect, Navega.Ninguno, ref _u, ref lincred); // ERROR: CS1503
            if (!ok) { Interaction.MsgBox("Numero de cuenta de ahorros no existe", MsgBoxStyle.Information, "SOLIDO"); return 0; }

            string _forapl = ForAplGmf; string _retche = RetCheGmf.ToString(); string _salmin = saldominimo.ToString();
            // BuscaLineaAhorro(lincred.ToString(), myconect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _salmin, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _forapl, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _retche); // ERROR: CS1501
            if (Information.IsNumeric(_salmin)) saldominimo = Convert.ToDouble(_salmin);
            if (Information.IsNumeric(_forapl)) ForAplGmf = _forapl;
            if (Information.IsNumeric(_retche)) RetCheGmf = Convert.ToInt32(_retche);

            SaldoDisponible = Math.Round((saldo - saldominimo - saldocanje), 2);
            totmaxret = SaldoDisponible;
            if (clasemovto == 0 || clasemovto == 4)
            {
                if (ForAplGmf.Trim() == "2")
                {
                    if (formapago == 1 && RetCheGmf == 1) Vlr4x1000 = 0;
                    else Vlr4x1000 = Liquida4x1000(SaldoDisponible, numcuenta, fecha, myconect);
                    Vlr4xMil = Vlr4x1000;
                    totmaxret = SaldoDisponible - Vlr4x1000;
                }
            }
            return totmaxret;
        }

        // ----------------------------------------------------------------
        // BuscarNovedadesBaseCaja
        // ----------------------------------------------------------------
        public DataSet BuscarNovedadesBaseCaja(string codcajero, DateTime fecha, OdbcConnection myconnect)
        {
            return BuscarNovedadesBaseCaja(codcajero, fecha, myconnect, OpcionesBaseCaja.Base);
        }

        public DataSet BuscarNovedadesBaseCaja(string codcajero, DateTime fecha, OdbcConnection myconnect, OpcionesBaseCaja novedad)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select novedad,sum(Valor) as valor,count(novedad) as reg from dep_basecaja "
                    + "where Codcajero='" + codcajero + "' and Fecha='" + Strings.Format(fecha, Var.varini.PstForFec) + "' " + (novedad == OpcionesBaseCaja.Todos ? "" : " and novedad='" + (int)novedad + "'")
                    + " group by novedad";
            CargaVarini.ExecuteQueryDataset(stmysql, myconnect, "BuscarNovedadesBaseCaja", dsdata, "TblNovedadesBase");
            return dsdata;
        }

        // ----------------------------------------------------------------
        // BuscarBaseCaja
        // ----------------------------------------------------------------
        public DataSet BuscarBaseCaja(string codcajero, DateTime fecha, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select FechaNovedad,novedad,case novedad when '1' then 'Base' when '2' then 'Ingresos a Caja' when '3' then 'Salidas de Caja' end as TipoNovedad,"
                    + "Valor,UsuRecibe,UsuEntrega from dep_basecaja "
                    + "where Codcajero='" + codcajero + "' and Fecha='" + Strings.Format(fecha, Var.varini.PstForFec) + "' order by fecha";
            CargaVarini.ExecuteQueryDataset(stmysql, myconnect, "BuscarNovedadesBaseCaja", dsdata, "TblBase");
            return dsdata;
        }

        // ----------------------------------------------------------------
        // GrabarNovedadesBase
        // ----------------------------------------------------------------
        public bool GrabarNovedadesBase(string codcajero, DateTime fecha, DataSet dsdata, OdbcConnection myconnect)
        {
            int i = 0;
            if (dsdata.Tables["TblBase"].Rows.Count > 0)
            {
                stmysql = "delete from dep_basecaja where codcajero='" + codcajero + "' and fecha='" + Strings.Format(fecha, Var.varini.PstForFec) + "'";
                CargaVarini.ExecuteQueryconec(stmysql, myconnect, "GrabarNovedadesBase(Elimina)");
                while (i < dsdata.Tables["TblBase"].Rows.Count)
                {
                    DataRow _r = dsdata.Tables["TblBase"].Rows[i];
                    stmysql = "insert into dep_basecaja (Codcajero,FechaNovedad,Fecha,Novedad,Valor,UsuRecibe,UsuEntrega) values ('"
                            + codcajero + "','" + Strings.Format(Convert.ToDateTime(_r["FechaNovedad"]), Var.varini.pstForfecyHora) + "','" + Strings.Format(Convert.ToDateTime(_r["FechaNovedad"]), Var.varini.PstForFec) + "','"
                            + Convert.ToString(_r["novedad"]) + "','" + Convert.ToString(_r["valor"]) + "','" + Convert.ToString(_r["UsuRecibe"]) + "','" + Convert.ToString(_r["UsuEntrega"]) + "')";
                    CargaVarini.ExecuteQueryconec(stmysql, myconnect, "GrabarNovedadesBase");
                    i = i + 1;
                }
                return true;
            }
            else return false;
        }

        // ----------------------------------------------------------------
        // LIquidaSaldoMinimo
        // ----------------------------------------------------------------
        public void LIquidaSaldoMinimo(string Cpte, string ConseCpte, int LineaArros, DateTime FecIni, DateTime FecFin, DateTime FechaLiquidacion,
            string Usuario, Form Myforma, OdbcConnection Myconect, string empresa, string cencosto, bool LiqRetirado, string StTercerizaCruce)
        {
            DataSet DsLiqInt = new DataSet(); DataSet DsInforme = new DataSet();
            double Fila = 0; int dias = 1; double SaldoMinimo = 0; int sw = 0;
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string cuenta = null; string CuentaNew = null; string CuentaCiclo = null;
            double SaldoInicial = 0; int DiasMov = 0; double SaldoDiario = 0;
            string codigoter = " "; int lincred = 0; double numero = 0;
            string Nombre = " "; double BaseLIq = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquida Saldo Minimo Mensual", Myforma);
            int MesLiq = 0; int i = 0; int DiasaLiquidar = 30;
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            string estado = "Y";
            DateTime fechaUltimaLiquidacion = new DateTime(1950, 1, 1);
            double saldoMoviAnterio = 0;
            int verificarEntrada = 0;
            double FilaBuesque = 0;
            string cuentaBusquedad = "";

            DsLiqInt.Tables.Add("tbldatosLiq");
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("CODIGOTER", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NOMBRE", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("LINCRED", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NUMERO", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("BASE", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("INTERES", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("FechaUltLiquid", typeof(DateTime));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("ValidaLiquida", typeof(string));

            stbuilder.Append("SELECT copmae.codigoter,copmae.lincred,copmae.numero,movto.fecha_movto,maenit.nombre,maenit.apellido, ");
            stbuilder.Append("saldo.Saldo_inicial as saldoInicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,parame58.CptoInteres,");
            stbuilder.Append("parame58.perpago_int,(sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) as saldiario,copmae.FECCIERRE,maehor.estado   ");
            stbuilder.Append(" from cop_movimto movto ");
            stbuilder.Append(" RIGHT outer join cop_docmto doc on movto.compronte=doc.compronte ");
            stbuilder.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto between '" + Strings.Format(FecIni, Var.varini.PstForFec) + "' and '" + Strings.Format(FecFin, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
            stbuilder.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
            stbuilder.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
            stbuilder.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
            stbuilder.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FecIni, "yyyyMM") + "' ");
            stbuilder.Append(" left join dep_maeahor maehor on  saldo.numero = maehor.num_cuenta    ");
            stbuilder.Append(" where maenit.estado<>'T' and parame58.lincred = " + LineaArros + empresa + cencosto + (LiqRetirado == false ? " and maenit.estado<>'R' " : " "));
            stbuilder.Append(" group by copmae.codigoter,copmae.lincred,copmae.numero,movto.fecha_movto,maenit.nombre,maenit.apellido, ");
            stbuilder.Append(" saldo.Saldo_inicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,parame58.CptoInteres,");
            stbuilder.Append(" parame58.perpago_int,copmae.FECCIERRE,maehor.estado   ");
            stbuilder.Append(" order by copmae.codigoter, copmae.lincred, copmae.numero,movto.fecha_movto ");

            ok = CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), Myconect, "LIquidaSaldoMinimo", DsLiqInt, "tblLiqSalMin");
            if (ok)
            {
                msgbarra.ValorMinimoMaximo(0, DsLiqInt.Tables["tblLiqSalMin"].Rows.Count);
                msgbarra.Show();

                while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                {
                    DataRow _r = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                    CuentaNew = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                    i = 0;
                    if (cuenta != CuentaNew)
                    {
                        cuentaBusquedad = "";
                        SaldoMinimo = 0; SaldoInicial = 0; sw = 0;
                        SaldoInicial = (_r["SaldoInicial"] is DBNull) ? 0 : Convert.ToDouble(_r["saldoInicial"]) * -1;
                        saldoMoviAnterio = 0;
                        int dia = FecIni.Day;
                        if (dia != 1 && verificarEntrada == 0)
                        {
                            verificarEntrada = 1;
                            DateTime fechaFinalCapital = new DateTime(FecIni.Year, FecIni.Month, dia - 1);
                            DateTime fechaInicioCapital = new DateTime(FecIni.Year, FecIni.Month, 1);
                            saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(Convert.ToDouble(_r["numero"]), Convert.ToInt32(_r["lincred"]), Convert.ToString(_r["codigoter"]), fechaInicioCapital, fechaFinalCapital, Myconect);
                            SaldoInicial += (saldoMoviAnterio * -1);
                        }
                        SaldoMinimo = SaldoInicial;
                        codigoter = Convert.ToString(_r["codigoter"]);
                        lincred = Convert.ToInt32(_r["lincred"]);
                        numero = Convert.ToDouble(_r["numero"]);
                        cuenta = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                        Nombre = Convert.ToString(_r["Apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                        FilaBuesque = Fila;
                        if (SaldoMinimo == 0)
                        {
                            while (FilaBuesque < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                            {
                                DataRow _rb = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)FilaBuesque];
                                cuentaBusquedad = Convert.ToString(_rb["codigoter"]) + Convert.ToString(_rb["lincred"]) + Convert.ToString(_rb["numero"]);
                                if (cuentaBusquedad == cuenta)
                                {
                                    SaldoDiario = (_rb["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_rb["saldiario"]);
                                    SaldoMinimo = (SaldoDiario * -1);
                                    if (SaldoMinimo != 0) break;
                                }
                                FilaBuesque += 1;
                            }
                        }
                        SaldoDiario = 0;
                    }

                    while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                    {
                        DataRow _ri = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                        if (!(_ri["fecha_movto"] is DBNull))
                        {
                            SaldoDiario = (_ri["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_ri["saldiario"]);
                            SaldoInicial += (SaldoDiario * -1);
                            int _diasGracia = Convert.ToInt32(_ri["DIAS_GRACIA"]);
                            if (_diasGracia != 0)
                            {
                                dias = clsliqcredito.CalculaDias(Convert.ToDateTime(_ri["fecha_movto"]), FecIni) + 1;
                                if (dias <= _diasGracia) SaldoMinimo = SaldoInicial;
                            }
                            if (SaldoInicial < SaldoMinimo)
                            {
                                if (_diasGracia == 0)
                                    SaldoMinimo = SaldoInicial;
                                else
                                {
                                    dias = clsliqcredito.CalculaDias(Convert.ToDateTime(_ri["fecha_movto"]), FecIni) + 1;
                                    if (dias > _diasGracia) SaldoMinimo = SaldoInicial;
                                    else SaldoMinimo = 0;
                                }
                            }
                        }
                        if (Fila != DsLiqInt.Tables["tblLiqSalMin"].Rows.Count - 1)
                        {
                            DataRow _rn = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila + 1];
                            CuentaCiclo = Convert.ToString(_rn["codigoter"]) + Convert.ToString(_rn["lincred"]) + Convert.ToString(_rn["numero"]);
                            if (CuentaCiclo != cuenta) break;
                            else { msgbarra.PerformStep(); Fila += 1; }
                        }
                        else break;
                    }

                    string _perpago = Convert.ToString(_r["perpago_int"]);
                    switch (_perpago) { case "6": DiasaLiquidar = 30; break; case "5": DiasaLiquidar = 1; break; case "4": DiasaLiquidar = 90; break; case "3": DiasaLiquidar = 120; break; case "2": DiasaLiquidar = 180; break; case "1": DiasaLiquidar = 360; break; default: DiasaLiquidar = 30; break; }
                    fechaUltimaLiquidacion = (_r["FECCIERRE"] is DBNull) ? new DateTime(1950, 1, 1) : Convert.ToDateTime(_r["FECCIERRE"]);
                    if (!(_r["estado"] is DBNull))
                        estado = Convert.ToString(_r["estado"]).Trim() == "3" ? "N" : "Y";
                    else estado = "Y";

                    Fila += 1;
                    if (SaldoMinimo > 0 && estado == "Y")
                        DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoMinimo, 0, fechaUltimaLiquidacion, "Y");
                    else
                        DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoMinimo, 0, fechaUltimaLiquidacion, "N");
                    msgbarra.PerformStep();
                }

                if (!Information.IsNumeric(ConseCpte)) ConseCpte = "0";
                msgbarra.Close(); msgbarra.Dispose();

                DsInforme = LiquidacionInteresesCuentasAhorro(DsLiqInt.Tables["tbldatosliq"], Cpte, Convert.ToDouble(ConseCpte), LineaArros.ToString(), FechaLiquidacion, Usuario, DiasaLiquidar, StTercerizaCruce, Myforma, Myconect, FecIni, FecFin);
                if (Convert.ToDouble(ConseCpte) == 0)
                    ImprimirLiquidacionIntereses(LineaArros.ToString(), FechaLiquidacion, DsInforme, 1, Myforma, Myconect);
            }
        }

        // ----------------------------------------------------------------
        // LIquidaSaldoPromedio
        // ----------------------------------------------------------------
        public void LIquidaSaldoPromedio(string Cpte, string ConseCpte, int LineaArros, DateTime FecIni, DateTime FecFin, DateTime FechaLiquidacion,
            string Usuario, Form Myforma, OdbcConnection Myconect, string empresa, string cencosto, bool LiqRetirado, string StTercerizaCruce)
        {
            DataSet DsLiqInt = new DataSet(); DataSet DsInforme = new DataSet();
            double Fila = 0; int dias = 1; double SaldoCuenta = 0; int sw = 0;
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string cuenta = null; string CuentaNew = null; string CuentaCiclo = null;
            double SaldoInicial = 0; int DiasMov = 0; double SaldoDiario = 0;
            string codigoter = " "; int lincred = 0; double numero = 0;
            string Nombre = " "; double BaseLIq = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquida Saldo Promedio", Myforma);
            int i = 0; int DiasPromedio = 0; double saldo_cta = 0; double SaldoAcu = 0; string PeriodoMov = "";
            DateTime FechaMovto = new DateTime(1950, 1, 1); DateTime SigFecMovto = new DateTime(1950, 1, 1);
            int DiasaLiquidar = 30;
            DateTime fechaUltimaLiquidacion = new DateTime(1950, 1, 1);
            double saldoMoviAnterio = 0;
            string estado = "Y";

            DsLiqInt.Tables.Add("tbldatosLiq");
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("CODIGOTER", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NOMBRE", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("LINCRED", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NUMERO", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("BASE", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("INTERES", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("FechaUltLiquid", typeof(DateTime));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("ValidaLiquida", typeof(string));

            stbuilder.Append(" select copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,");
            stbuilder.Append(" saldo.Saldo_inicial as saldoInicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,");
            stbuilder.Append(" parame58.perpago_int, (sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) as saldiario,copmae.FECCIERRE,maehor.estado  ");
            stbuilder.Append(" from cop_movimto movto ");
            stbuilder.Append(" RIGHT outer join cop_docmto doc on movto.compronte=doc.compronte ");
            stbuilder.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto between '" + Strings.Format(FecIni, Var.varini.PstForFec) + "' and '" + Strings.Format(FecFin, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
            stbuilder.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
            stbuilder.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
            stbuilder.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
            stbuilder.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FecIni, "yyyyMM") + "' ");
            stbuilder.Append(" left join dep_maeahor maehor on  saldo.numero = maehor.num_cuenta  ");
            stbuilder.Append(" where maenit.estado<>'T' and parame58.lincred = " + LineaArros + empresa + cencosto + (LiqRetirado == false ? " and maenit.estado<>'R' " : " "));
            stbuilder.Append(" group by copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,");
            stbuilder.Append(" saldo.Saldo_inicial ,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,");
            stbuilder.Append(" parame58.perpago_int,copmae.FECCIERRE,maehor.estado   ");
            stbuilder.Append(" order by copmae.codigoter, copmae.lincred, copmae.numero,movto.fecha_movto ");

            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), Myconect, "LIquidaSaldoPromedio", DsLiqInt, "tblLiqSalMin");
            msgbarra.ValorMinimoMaximo(0, DsLiqInt.Tables["tblLiqSalMin"].Rows.Count);
            msgbarra.Show();

            DiasPromedio = (int)(FecFin - FecIni).TotalDays + 1;

            while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
            {
                DataRow _r = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                CuentaNew = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                i = 0;
                if (cuenta != CuentaNew)
                {
                    SaldoCuenta = 0; SaldoInicial = 0; saldo_cta = 0; SaldoAcu = 0; sw = 0;
                    dias = FecIni.Day; DiasMov = FecIni.Day;
                    SaldoInicial = (_r["SaldoInicial"] is DBNull) ? 0 : Convert.ToDouble(_r["saldoInicial"]) * -1;
                    int dias1 = FecIni.Day;
                    saldoMoviAnterio = 0;
                    if (dias1 != 1)
                    {
                        DateTime fechaFinalCapital = new DateTime(FecIni.Year, FecIni.Month, dias1 - 1);
                        DateTime fechaInicioCapital = new DateTime(FecIni.Year, FecIni.Month, 1);
                        saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(Convert.ToDouble(_r["numero"]), Convert.ToInt32(_r["lincred"]), Convert.ToString(_r["codigoter"]), fechaInicioCapital, fechaFinalCapital, Myconect);
                        SaldoInicial += (saldoMoviAnterio * -1);
                    }
                    saldo_cta = SaldoInicial;
                    SaldoCuenta = SaldoInicial;
                    codigoter = Convert.ToString(_r["codigoter"]);
                    lincred = Convert.ToInt32(_r["lincred"]);
                    numero = Convert.ToDouble(_r["numero"]);
                    cuenta = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                    Nombre = Convert.ToString(_r["Apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                    PeriodoMov = Strings.Format(FecIni, "yyyyMM");
                    FechaMovto = FecIni;
                }

                while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                {
                    DataRow _ri = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                    if (!(_ri["fecha_movto"] is DBNull))
                    {
                        SaldoDiario = (_ri["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_ri["saldiario"]);
                    Recorredias:
                        if (PeriodoMov != Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM"))
                        {
                            while (dias <= DateTime.DaysInMonth(FechaMovto.Year, FechaMovto.Month)) { SaldoAcu += saldo_cta; dias += 1; }
                            dias = 1;
                            SigFecMovto = new DateTime(FechaMovto.Year, FechaMovto.Month, 1).AddMonths(1);
                            if (Strings.Format(SigFecMovto, "yyyyMM") == Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM"))
                                PeriodoMov = Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM");
                            else { FechaMovto = SigFecMovto; goto Recorredias; }
                        }
                        DiasMov = Convert.ToInt32(Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "dd"));
                        while (dias < DiasMov) { SaldoAcu += saldo_cta; dias += 1; }
                        saldo_cta += (SaldoDiario * -1);
                        SaldoAcu += saldo_cta;
                        DiasMov += 1;
                        if (DiasPromedio == 0) { if (saldo_cta < SaldoCuenta) SaldoCuenta = saldo_cta; }
                        else SaldoCuenta = Math.Round(SaldoAcu / DiasPromedio, 2);
                        FechaMovto = Convert.ToDateTime(_ri["fecha_movto"]);
                        dias += 1;
                    }
                    if (Fila != DsLiqInt.Tables["tblLiqSalMin"].Rows.Count - 1)
                    {
                        DataRow _rn = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila + 1];
                        CuentaCiclo = Convert.ToString(_rn["codigoter"]) + Convert.ToString(_rn["lincred"]) + Convert.ToString(_rn["numero"]);
                        if (CuentaCiclo != cuenta)
                        {
                            PeriodoMov = Strings.Format(FechaMovto, "yyyyMM");
                            if (PeriodoMov != Strings.Format(FecFin, "yyyyMM"))
                            {
                                while (DiasMov <= DateTime.DaysInMonth(FechaMovto.Year, FechaMovto.Month)) { SaldoAcu += saldo_cta; DiasMov += 1; }
                                DiasMov = 1;
                                if (DiasMov <= Convert.ToInt32(Strings.Format(FecFin, "dd")))
                                    while (DiasMov <= Convert.ToInt32(Strings.Format(FecFin, "dd"))) { SaldoAcu += saldo_cta; DiasMov += 1; }
                                if (DiasPromedio > 0) SaldoCuenta = Math.Round(SaldoAcu / DiasPromedio, 2);
                            }
                            else
                            {
                                if (DiasMov < Convert.ToInt32(Strings.Format(FecFin, "dd")))
                                    while (DiasMov <= Convert.ToInt32(Strings.Format(FecFin, "dd"))) { SaldoAcu += saldo_cta; DiasMov += 1; }
                                if (DiasPromedio > 0) SaldoCuenta = Math.Round(SaldoAcu / DiasPromedio, 2);
                            }
                            break;
                        }
                        else { msgbarra.PerformStep(); Fila += 1; }
                    }
                    else break;
                }

                string _perpago = Convert.ToString(_r["perpago_int"]);
                switch (_perpago) { case "6": DiasaLiquidar = 30; break; case "5": DiasaLiquidar = 1; break; case "4": DiasaLiquidar = 90; break; case "3": DiasaLiquidar = 120; break; case "2": DiasaLiquidar = 180; break; case "1": DiasaLiquidar = 360; break; default: DiasaLiquidar = 30; break; }
                fechaUltimaLiquidacion = (_r["FECCIERRE"] is DBNull) ? new DateTime(1950, 1, 1) : Convert.ToDateTime(_r["FECCIERRE"]);
                if (!(_r["estado"] is DBNull))
                    estado = Convert.ToString(_r["estado"]).Trim() == "3" ? "N" : "Y";
                else estado = "Y";

                Fila += 1;
                if (SaldoCuenta > 0 && estado == "Y")
                    DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoCuenta, 0, fechaUltimaLiquidacion, "Y");
                else
                    DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoCuenta, 0, fechaUltimaLiquidacion, "N");
                msgbarra.PerformStep();
            }

            if (!Information.IsNumeric(ConseCpte)) ConseCpte = "0";
            msgbarra.Close(); msgbarra.Dispose();

            DsInforme = LiquidacionInteresesCuentasAhorro(DsLiqInt.Tables["tbldatosliq"], Cpte, Convert.ToDouble(ConseCpte), LineaArros.ToString(), FechaLiquidacion, Usuario, DiasaLiquidar, StTercerizaCruce, Myforma, Myconect, FecIni, FecFin);
            if (Convert.ToDouble(ConseCpte) == 0)
                ImprimirLiquidacionIntereses(LineaArros.ToString(), FechaLiquidacion, DsInforme, 2, Myforma, Myconect);
        }

        // ----------------------------------------------------------------
        // ValidaMovtoAhorroPermanente
        // ----------------------------------------------------------------
        public bool ValidaMovtoAhorroPermanente(string codigoter, int lincred, double numero, DateTime fechamovto, OdbcConnection myconnect)
        {
            DateTime fechaVemto = new DateTime(1950, 1, 1);
            DataSet dslinea = new DataSet();
            string _u = " "; string _codahor = " "; int _EquiSuper = 0;
            string _fv = fechaVemto.ToString();

            // paramcop.BuscaLinea(lincred, dslinea, myconnect); // ERROR: CS1503, CS1620
            string _codahorRow = Convert.ToString(dslinea.Tables["tbllineas"].Rows[0]["codahor"]);
            int _EquiSuperRow = Convert.ToInt32(dslinea.Tables["tbllineas"].Rows[0]["EquiSuper"]);

            if (_codahorRow == "2" && _EquiSuperRow == 2)
            {
                // msgcop_car.BuscaObligacion(codigoter, lincred.ToString(), numero.ToString(), myconnect, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref _fv); // ERROR: CS7036
                if (Information.IsDate(_fv))
                {
                    fechaVemto = Convert.ToDateTime(_fv);
                    if (Convert.ToDateTime(fechamovto.ToString("dd-MM-yyyy")) > Convert.ToDateTime(fechaVemto.ToString("dd-MM-yyyy")))
                        return true;
                    else
                    {
                        Interaction.MsgBox("Movimiento no se puede realizar. La fecha del movimiento es inferior a la fecha de vencimiento" + Convert.ToChar(13) + "Fecha vencimiento: " + fechaVemto, MsgBoxStyle.Information, "SOLIDO");
                        return false;
                    }
                }
            }
            return true;
        }

        // ----------------------------------------------------------------
        // BuscaTalonario
        // ----------------------------------------------------------------
        public bool BuscaTalonario(OdbcConnection myconect, int cuenta, string inirango, string finrango)
        {
            stmysql = "select *  from dep_talonario where cuenta=" + cuenta + "  and iniciorango= " + inirango + "   and   finalrango= " + finrango;
            return CargaVarini.ExecuteQueryconec(stmysql, myconect, "BuscaTalonario");
        }

        // ----------------------------------------------------------------
        // GrabarLibretaBloq
        // ----------------------------------------------------------------
        public bool GrabarLibretaBloq(OdbcConnection myconnect, DataSet DsDataset)
        {
            int i = 0;
            for (i = 0; i <= DsDataset.Tables["tblGrabarbloqLibreta"].Rows.Count - 1; i++)
            {
                DataRow _r = DsDataset.Tables["tblGrabarbloqLibreta"].Rows[i];
                if (BuscaTalonario(myconnect, Convert.ToInt32(_r["Cuenta"].ToString()), _r["DebIncial"].ToString(), _r["DebFinal"].ToString()))
                {
                    stmysql = "update dep_talonario set bloqlibreta='" + _r["bloqlibreta"].ToString()
                              + "' where cuenta=" + _r["Cuenta"].ToString() + "  and iniciorango =" + _r["DebIncial"].ToString()
                              + "  and finalrango=" + _r["DebFinal"].ToString();
                    ok = CargaVarini.ExecuteQueryconec(stmysql, myconnect, "GrabarLibretaBloq");
                }
            }
            return ok;
        }

        // ----------------------------------------------------------------
        // validarTope_retiro
        // ----------------------------------------------------------------
        public double validarTope_retiro(string codigoter, int lincred, double numero, DateTime fechaMovimto, OdbcConnection myconnect)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string vlr_debito = "  ";
            stbuilder.Append(" select sum(movimto.vlr_debito) as campo1  from cop_movimto movimto");
            stbuilder.Append(" inner join cop_docmto doctom on movimto.COMPRONTE =doctom.COMPRONTE");
            stbuilder.Append(" and movimto.NUMERO_DOMTO =  doctom.NUMERO_DOMTO");
            stbuilder.Append(" inner join sys_compro02 compro02 on compro02.CODIGO = movimto.COMPRONTE ");
            stbuilder.Append(" where movimto.CODIGOTER='" + codigoter + "' and  movimto.lincred=" + lincred + " and  movimto.NUMERO = " + numero + " and movimto.FECHA_MOVTO='" + Strings.Format(fechaMovimto, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" and doctom.Anulado <>'Y' and compro02.RESTRI_TESORERIA <> 'Y' ");
            CargaVarini.ExecuteQueryconec(stbuilder.ToString(), myconnect, "validarTope_retiro", ref vlr_debito);
            if (!Information.IsDBNull(vlr_debito))
            {
                if (Information.IsNumeric(vlr_debito)) return Convert.ToDouble(vlr_debito);
                else return 0;
            }
            else return 0;
        }

        // ----------------------------------------------------------------
        // GrabaCausacionAhorro
        // ----------------------------------------------------------------
        public void GrabaCausacionAhorro(string codigoter, int lincred, double num_cuenta, DateTime FecCausacion, string Estado, OdbcConnection myconnect)
        {
            GrabaCausacionAhorro(codigoter, lincred, num_cuenta, FecCausacion, Estado, myconnect, new DateTime(1950, 1, 1), "", "C");
        }

        public void GrabaCausacionAhorro(string codigoter, int lincred, double num_cuenta, DateTime FecCausacion, string Estado, OdbcConnection myconnect, DateTime FECHA_CANCELA, string usucancela_PAP, string opcion)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder stbuilder2 = new System.Text.StringBuilder();
            switch (opcion.Trim())
            {
                case "C":
                    stbuilder.Append("Update dep_maeahor set Estado='" + Estado + "',FECHA_CANCELA='" + Strings.Format(FECHA_CANCELA, Var.varini.PstForFec) + "',usucancela_PAP='" + usucancela_PAP + "'  where  num_cuenta  = " + num_cuenta);
                    CargaVarini.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaCausacionAhorro");
                    stbuilder2.Append("Update cop_maecar set  FECCIERRE = '" + Strings.Format(FecCausacion, Var.varini.PstForFec) + "' where CODIGOTER='" + codigoter + "' and lincred= " + lincred + "  and NUMERO  = " + num_cuenta);
                    CargaVarini.ExecuteQueryconec(stbuilder2.ToString(), myconnect, "GrabaCausacionAhorro");
                    break;
                case "A":
                    stbuilder.Append("Update cop_maecar set  FECCIERRE = '" + Strings.Format(FecCausacion, Var.varini.PstForFec) + "' where CODIGOTER='" + codigoter + "' and lincred= " + lincred + "  and NUMERO  = " + num_cuenta);
                    CargaVarini.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaCausacionAhorro");
                    break;
            }
        }

        // ----------------------------------------------------------------
        // GrabarNovedad_meahor
        // ----------------------------------------------------------------
        public bool GrabarNovedad_meahor(int num_cuenta, int lincred, string codigoter, DateTime FecNovedad, DateTime fec_crea,
            DateTime fec_primer_des, string tipo_descu, string periodicidad,
            string ciclo, double cuota, int PLAZO, DateTime FECVEMTO, string TipoNovedad, string usuario, OdbcConnection myconnect)
        {
            string nomusu = " ";
            stmysql = "update dep_maeahor set fec_crea='" + Strings.Format(FecNovedad, Var.varini.PstForFec) + "', FECVEMTO='" + Strings.Format(FECVEMTO, Var.varini.PstForFec) + "', "
                      + " fec_primer_des = '" + Strings.Format(fec_primer_des, Var.varini.PstForFec) + "',tipo_descu='" + tipo_descu + "',periodicidad='" + periodicidad + "',ciclo='" + ciclo + "'  where num_cuenta=" + num_cuenta;
            ok = CargaVarini.ExecuteQueryconec(stmysql, myconnect, "GrabarNovedad_meahor/Maestro de Ahorro");
            if (ok)
            {
                stmysql = "update cop_maecar set FECFACT='" + Strings.Format(FecNovedad, Var.varini.PstForFec) + "',FECDESC='" + Strings.Format(fec_primer_des, Var.varini.PstForFec) + "',FECAPROB='" + Strings.Format(FecNovedad, Var.varini.PstForFec) + "', fecvemto='" + Strings.Format(FECVEMTO, Var.varini.PstForFec) + "',"
                          + " CUOTA =" + cuota + ",PLAZO= " + PLAZO + ",CICLOD='" + ciclo + "',PERIODD='" + periodicidad + "',CLADES='" + tipo_descu + "', FECCIERRE='" + Strings.Format(FecNovedad, Var.varini.PstForFec) + "'  where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero = " + num_cuenta;
                ok = CargaVarini.ExecuteQueryconec(stmysql, myconnect, "GrabarNovedad_meahor/ActualizarCartera");
                // msgcop_car.ActualizarCuaotaSalmaecar(codigoter, lincred, num_cuenta, Strings.Format(DateTime.Now, "yyyyMM"), myconnect); // ERROR: CS1503
                // paramsys.BuscaUsuario(usuario, myconnect, ref nomusu); // ERROR: CS1615, CS1620
                stmysql = "insert into dep_novmeahor(lincred, codigoter, num_cuenta,FechaNovedad,fec_crea , fec_primer_des, tipo_descu, periodicidad,ciclo,CUOTA,PLAZO,FECVEMTO , TipoNovedad, Usuario, NomUsu,fechasys ) "
                    + " Values (" + lincred + ",'" + codigoter + "'," + num_cuenta + ",'" + Strings.Format(FecNovedad, Var.varini.PstForFec) + "','" + Strings.Format(fec_crea, Var.varini.PstForFec) + "','" + Strings.Format(fec_primer_des, Var.varini.PstForFec) + "','" + tipo_descu + "','" + periodicidad + "','"
                              + ciclo + "'," + cuota + "," + PLAZO + ",'" + Strings.Format(FECVEMTO, Var.varini.PstForFec) + "','" + TipoNovedad + "',"
                     + "'" + usuario + "','" + nomusu + "','" + Strings.Format(DateTime.Now, Var.varini.pstForfecyHora) + "')";
                ok = CargaVarini.ExecuteQueryconec(stmysql, myconnect, "GrabarNovedad_meahor");
                return ok;
            }
            return false;
        }

        // ----------------------------------------------------------------
        // liquidadPAP (minimal overload)
        // ----------------------------------------------------------------
        public DataSet liquidadPAP(double num_cuenta, int lincred, string codigoter, OdbcConnection myconnect, bool actualizabd, DateTime FechaUltimaCuasacion, DateTime FechaLiquidacion, DateTime FecCompronte)
        {
            double _vlr = 0; FormaPagoInteres _fpi = FormaPagoInteres.Concepto; TipoPagoInteres _pag = TipoPagoInteres.SoloIntereses; bool _vld = false;
            return liquidadPAP(num_cuenta, lincred, codigoter, myconnect, actualizabd, FechaUltimaCuasacion, FechaLiquidacion, FecCompronte,
                "", "", "N", "", 0, 0, ref _vlr, FormaLiquidacion.LiquidaIntereses, ref _fpi, ref _pag, false, "", ref _vld);
        }

        public DataSet liquidadPAP(double num_cuenta, int lincred, string codigoter, OdbcConnection myconnect, bool actualizabd,
            DateTime FechaUltimaCuasacion, DateTime FechaLiquidacion, DateTime FecCompronte,
            string comprobante, string cosecuComprobante, string StTercerizaCruce, string Usuario,
            int LineaAhorros, double NumAhorros, ref double VlrIntAcumulado,
            FormaLiquidacion ForLiquidacion, ref FormaPagoInteres FormaPagoInt, ref TipoPagoInteres Pago,
            bool cierre, string usucancela_PAP, ref bool valida_liquidadPAP)
        {
            double Base = 0; double Interes = 0;
            DataSet DatLiquidacion = new DataSet();
            int forma_pag = 0; double SalMinimoInt = 0; decimal TasaInt = 0; double VlrMinRetfte = 0; double TasaRetfte = 0; int Forliq = 0; int CptoInteres = 0;
            string StCuentaCruce = "999999999999";
            int CptoInteresAhorro = 0;
            int plazo = 0; double cuota = 0;
            string CptoRetfte = " "; string CptoIntAhorro = " "; string CptoAho = " ";
            int AnoIni = 0; int MesIni = 0; int DiaIni = 0;
            int AnoFin = 0; int MesFin = 0; int DiaFin = 0;
            int DiasDeposito = 0; DateTime FecIngreso = new DateTime(1950, 1, 1); int MesesIngreso = 0;
            decimal DbTasaInt = 0;
            string Nombre = " ";
            string CuentaTesoreria = "0";
            string cptocapital = "02";
            int PerPagInt = 0;
            int DiaPromedioLiquidacion = 0;

            DatLiquidacion.Tables.Add("DatosLiq");
            DatLiquidacion.Tables[0].Columns.Add("lincred", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("num_cuenta", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("codigoter", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("Nombre", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("Valor", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("Valorinteres", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("TasaInteres", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("TasaRetfte", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("Total", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("Dias", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("saldoAcumulado", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("Base", typeof(double));

            string _u = " "; string _sl = SalMinimoInt.ToString(); string _ti = TasaInt.ToString();
            string _vm = VlrMinRetfte.ToString(); string _tr = TasaRetfte.ToString();
            string _fl = Forliq.ToString(); string _ci = CptoInteres.ToString();
            string _pp = PerPagInt.ToString(); string _fp = forma_pag.ToString();
            string _ct = CuentaTesoreria;

            // BuscaLineaAhorro(lincred.ToString(), myconnect, Navega.Ninguno, // ERROR: CS1501
                // ref _u, ref _u, ref _pp, ref _sl, ref _u, ref _u, ref _ti, ref _vm, ref _tr, ref _u, ref _fl, // ERROR: CS1501
                // ref _u, ref _u, ref _u, ref _u, ref _fp, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS1501
                // ref _u, ref _ci, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _ct); // ERROR: CS1501
            if (Information.IsNumeric(_sl)) SalMinimoInt = Convert.ToDouble(_sl);
            if (Information.IsNumeric(_ti)) TasaInt = Convert.ToDecimal(_ti);
            if (Information.IsNumeric(_vm)) VlrMinRetfte = Convert.ToDouble(_vm);
            if (Information.IsNumeric(_tr)) TasaRetfte = Convert.ToDouble(_tr);
            if (Information.IsNumeric(_fl)) Forliq = Convert.ToInt32(_fl);
            if (Information.IsNumeric(_ci)) CptoInteres = Convert.ToInt32(_ci);
            if (Information.IsNumeric(_pp)) PerPagInt = Convert.ToInt32(_pp);
            if (Information.IsNumeric(_fp)) forma_pag = Convert.ToInt32(_fp);
            CuentaTesoreria = _ct;

            // paramsys.BuscarCompania(Var.varini.SptCodEmpr, myconnect, // ERROR: CS1061
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS1061
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref CptoAho, ref _u, ref CptoRetfte, ref _u, ref _u, ref _u, // ERROR: CS1061
                // ref _u, ref _u, ref _u, ref CptoIntAhorro); // ERROR: CS1061

            if (comprobante.Trim() != "")
                // msgcnt_cnt.BuscaComprobante(comprobante, 0, false, myconnect, // ERROR: CS1620
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS1620
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref StCuentaCruce); // ERROR: CS1620

            CptoInteresAhorro = (CptoInteres == 9999) ? lincred : CptoInteres;

            // paramcop.BuscaAsociado(codigoter, myconnect, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref FecIngreso); // ERROR: CS7036
            MesesIngreso = (FechaLiquidacion.Year - FecIngreso.Year) * 12 + (FechaLiquidacion.Month - FecIngreso.Month);

            string _cuota = cuota.ToString(); string _plazo = plazo.ToString();
            // msgcop_car.BuscaObligacion(codigoter, lincred.ToString(), num_cuenta.ToString(), myconnect, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref _cuota, ref _u, ref _u, ref _u, ref _plazo); // ERROR: CS7036
            if (Information.IsNumeric(_cuota)) cuota = Convert.ToDouble(_cuota);
            if (Information.IsNumeric(_plazo)) plazo = Convert.ToInt32(_plazo);

            // DbTasaInt = paramcop.BuscarTasasporPlazos(lincred, (plazo / 30), cuota, MesesIngreso, myconnect); // ERROR: CS1061
            if (DbTasaInt <= 0) DbTasaInt = TasaInt;

            switch (PerPagInt.ToString())
            {
                case "6": DiaPromedioLiquidacion = 30; break; case "5": DiaPromedioLiquidacion = 1; break;
                case "4": DiaPromedioLiquidacion = 90; break; case "3": DiaPromedioLiquidacion = 120; break;
                case "2": DiaPromedioLiquidacion = 180; break; case "1": DiaPromedioLiquidacion = 360; break;
                default: DiaPromedioLiquidacion = 30; break;
            }

            DiasDeposito = calcula_Dias_Liquidacion(FechaUltimaCuasacion, FechaLiquidacion);

            switch (forma_pag)
            {
                case 1:
                    LIquidaSaldoMinimo_Individual(num_cuenta, lincred, codigoter, myconnect, FechaUltimaCuasacion, FechaLiquidacion, ref Nombre, ref Base, ref Interes);
                    LiquidaInteresMesPAP(Forliq, DbTasaInt, Base, SalMinimoInt, VlrMinRetfte, (decimal)TasaRetfte, codigoter, Nombre, lincred.ToString(), num_cuenta.ToString(), comprobante, cosecuComprobante, CptoRetfte, CptoInteresAhorro.ToString(), CptoIntAhorro, actualizabd, Usuario, FechaLiquidacion, DatLiquidacion, myconnect, ref Interes, DiasDeposito, new DateTime(1950,1,1), new DateTime(1950,1,1), StTercerizaCruce, StCuentaCruce, "Y", FechaLiquidacion, "N", CptoAho, FecCompronte, ref FormaPagoInt, LineaAhorros, NumAhorros, ref VlrIntAcumulado, CuentaTesoreria, cptocapital, ForLiquidacion, ref Pago, cierre, usucancela_PAP, ref valida_liquidadPAP, DiaPromedioLiquidacion, FechaUltimaCuasacion);
                    break;
                case 2:
                    LIquidaSaldoPromedio_Individual(num_cuenta, lincred, codigoter, myconnect, FechaUltimaCuasacion, FechaLiquidacion, ref Nombre, ref Base, ref Interes, "Y", DiasDeposito);
                    LiquidaInteresMesPAP(Forliq, DbTasaInt, Base, SalMinimoInt, VlrMinRetfte, (decimal)TasaRetfte, codigoter, Nombre, lincred.ToString(), num_cuenta.ToString(), comprobante, cosecuComprobante, CptoRetfte, CptoInteresAhorro.ToString(), CptoIntAhorro, actualizabd, Usuario, FechaLiquidacion, DatLiquidacion, myconnect, ref Interes, DiasDeposito, new DateTime(1950,1,1), new DateTime(1950,1,1), StTercerizaCruce, StCuentaCruce, "Y", FechaLiquidacion, "N", CptoAho, FecCompronte, ref FormaPagoInt, LineaAhorros, NumAhorros, ref VlrIntAcumulado, CuentaTesoreria, cptocapital, ForLiquidacion, ref Pago, cierre, usucancela_PAP, ref valida_liquidadPAP, DiaPromedioLiquidacion, FechaUltimaCuasacion);
                    break;
                case 3:
                case 4:
                    LiquidacionSobreSaldoFinal_Individual(num_cuenta, lincred, codigoter, myconnect, FechaUltimaCuasacion, FechaLiquidacion, ref Nombre, ref Base, ref Interes);
                    LiquidaInteresMesPAP(Forliq, DbTasaInt, Base, SalMinimoInt, VlrMinRetfte, (decimal)TasaRetfte, codigoter, Nombre, lincred.ToString(), num_cuenta.ToString(), comprobante, cosecuComprobante, CptoRetfte, CptoInteresAhorro.ToString(), CptoIntAhorro, actualizabd, Usuario, FechaLiquidacion, DatLiquidacion, myconnect, ref Interes, DiasDeposito, new DateTime(1950,1,1), new DateTime(1950,1,1), StTercerizaCruce, StCuentaCruce, "Y", FechaLiquidacion, "Y", CptoAho, FecCompronte, ref FormaPagoInt, LineaAhorros, NumAhorros, ref VlrIntAcumulado, CuentaTesoreria, cptocapital, ForLiquidacion, ref Pago, cierre, usucancela_PAP, ref valida_liquidadPAP, DiaPromedioLiquidacion, FechaUltimaCuasacion);
                    break;
            }
            return DatLiquidacion;
        }

        // ----------------------------------------------------------------
        // LiquidaInteresMesPAP (minimal overload)
        // ----------------------------------------------------------------
        public void LiquidaInteresMesPAP(int forliq, decimal porpago_int, double saldot, double SALMIN_INT, double VlrRetMin,
            decimal POR_RFTE, string codigoter, string nombre, string lincred, string numero,
            string Compcte, string ConseCpte, string CptoRetfte, string CptoIntereses, string CptoMovtoInt, bool Actualiza,
            string Usuario, DateTime FecLiquidacion, DataSet DatLiquidacion, OdbcConnection myconnect)
        {
            double _il = 0; FormaPagoInteres _fpi = FormaPagoInteres.Concepto; TipoPagoInteres _pag = TipoPagoInteres.SoloIntereses; bool _vld = false; double _vlr = 0;
            LiquidaInteresMesPAP(forliq, porpago_int, saldot, SALMIN_INT, VlrRetMin, POR_RFTE, codigoter, nombre, lincred, numero,
                Compcte, ConseCpte, CptoRetfte, CptoIntereses, CptoMovtoInt, Actualiza, Usuario, FecLiquidacion, DatLiquidacion, myconnect,
                ref _il, 0, new DateTime(1950,1,1), new DateTime(1950,1,1), "N", "999999999999", "N", new DateTime(1950,1,1),
                "N", "9999", new DateTime(1950,1,1), ref _fpi, 0, 0, ref _vlr, "0", "02",
                FormaLiquidacion.LiquidaIntereses, ref _pag, false, "", ref _vld, 0, new DateTime(1950,1,1));
        }

        public void LiquidaInteresMesPAP(int forliq, decimal porpago_int, double saldot, double SALMIN_INT, double VlrRetMin,
            decimal POR_RFTE, string codigoter, string nombre, string lincred, string numero,
            string Compcte, string ConseCpte, string CptoRetfte, string CptoIntereses, string CptoMovtoInt, bool Actualiza,
            string Usuario, DateTime FecLiquidacion, DataSet DatLiquidacion, OdbcConnection myconnect,
            ref double InteresLiq, int DiasInt, DateTime Fecini, DateTime Fecfin,
            string StTercerizaContrapartida, string StCuentaCruce, string manejaFormaPAP, DateTime FechaFinPAP,
            string siSobreSaldoFinal, string cptoAhorro, DateTime FecCompronte, ref FormaPagoInteres FormaPagoInt,
            int LineaAhorros, double NumAhorros, ref double VlrIntAcumulado, string CuentaTesoreria, string cptocapital,
            FormaLiquidacion ForLiquidacion, ref TipoPagoInteres Pago,
            bool cierre, string usucancela_PAP, ref bool valida_liquidadPAP, int DiaPromedioLiquidacion, DateTime FechaUltimaLiquidacion)
        {
            decimal i_rate = 0; double interes = 0; double baseret = 0; double retfte = 0; double vlrbase = 0;
            int dias = 0; string MOVINTERES = "99"; int NumLincred = 0; string EstadoPeriodo = "A"; string TipoTransacion = "9999";
            int Numlinea = 0; int linea = 0; string CuentaAho = " "; string CuentaNit = " "; int CptoCta = 0; int CptoInt = 0;
            string cptointcdats = "9999"; string estado = "0";
            double DIF = 0; double ValorInteres = 0; double SaldoAbonar = 0; double Saldo = 0;
            DateTime feccancela = new DateTime(1950, 1, 1);
            string opcionliquida = "A";
            string detalleDocumento = "   ";
            string NitAsoc = "  "; string validarPagarrtefte = "   ";

            ok = msgcop_car.ValidarClienteenListaNegra(codigoter, false, myconnect);
            string _saldo = Saldo.ToString();
            // car.BuscaSaldoObligacion(codigoter, lincred, numero, Strings.Format(FecCompronte, "yyyyMM"), myconnect, ref _saldo); // ERROR: CS1503
            if (Information.IsNumeric(_saldo)) Saldo = Convert.ToDouble(_saldo) * -1;

            if (DiasInt > 0) dias = DiasInt;

            switch (forliq)
            {
                // case 1: i_rate = paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaNominalAnual, porpago_int, dias); break; // ERROR: CS1503
                // case 2: i_rate = paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaEfectivaAnual, porpago_int, dias); break; // ERROR: CS1503
            }

            if (saldot < 0 || saldot == 99999999999999) i_rate = 0;
            if (SALMIN_INT > saldot) i_rate = 0;

            if (InteresLiq == 0)
            {
                switch (forliq)
                {
                    // case 1: interes = Math.Round(Convert.ToDouble(Math.Round((saldot * i_rate), 8)) * dias, 0); break; // ERROR: CS0019
                    // case 2: interes = Math.Round(Convert.ToDouble(Math.Round((saldot * i_rate), 8)), 0); break; // ERROR: CS0019
                }
            }
            else interes = InteresLiq;

            ValorInteres = interes;
            baseret = (dias > 0) ? interes / dias : 0;
            retfte = 0;

            string _u = " ";
            // msgcop_car.BuscaAsociado(codigoter, myconnect, ref _u, ref NitAsoc, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref CuentaAho); // ERROR: CS7036

            if (baseret > VlrRetMin)
            {
                // msgcnt_cnt.BuscarTercero(NitAsoc, myconnect, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref validarPagarrtefte); // ERROR: CS7036
                if (!ok)
                    retfte = Math.Round((interes * (double)POR_RFTE) / 100);
                else
                {
                    if (validarPagarrtefte == "Y") retfte = Math.Round((interes * (double)POR_RFTE) / 100);
                    else retfte = 0;
                }
                interes = interes - retfte;
                vlrbase = (POR_RFTE > 0) ? Math.Round(retfte / ((double)POR_RFTE / 100)) : 0;
            }

            if (Actualiza)
            {
                // car.buscaPeriodo("copc", myconnect, ref _u, ref _u, ref FecCompronte, ref EstadoPeriodo, ref _u, Strings.Format(FecCompronte, "yyyy")); // ERROR: CS1503, CS1615
                if (EstadoPeriodo == "C") { Interaction.MsgBox("Periodo de trabajo esta cerrado", MsgBoxStyle.Information, "SOLIDO"); return; }
                if (EstadoPeriodo == "P") { if (Interaction.MsgBox("Periodo de trabajo en modo de prevencion, Desea Continuar ?", MsgBoxStyle.YesNo, "SOLIDO") == MsgBoxResult.No) return; }

                NumLincred = Convert.ToInt32(numero);
                TipoTransacion = CptoMovtoInt;
                CptoInt = Convert.ToInt32(CptoIntereses);
                switch (FormaPagoInt)
                {
                    case FormaPagoInteres.Concepto:
                        linea = Convert.ToInt32(CptoIntereses); Numlinea = NumLincred; break;
                    case FormaPagoInteres.CuentaAhorros:
                        linea = LineaAhorros; Numlinea = (int)NumAhorros; TipoTransacion = cptoAhorro; break;
                    case FormaPagoInteres.Tesoreria:
                        linea = Convert.ToInt32(CptoIntereses); Numlinea = NumLincred;
                        if (CuentaTesoreria.Trim() == "") { Interaction.MsgBox("Cuenta Tesoreria No esta Parametrizada " + Convert.ToChar(13) + " en Parametros de Linea de Ahorro", MsgBoxStyle.Information, "SOLIDO"); return; }
                        break;
                    case FormaPagoInteres.Consigna:
                        bool _okCta = false; // BuscarCuentaAhorro(CuentaAho, myconnect, Navega.Ninguno, ref CuentaNit, ref CptoCta); // ERROR: CS1503
                        if (_okCta) // ERROR: CS0103
                        {
                            if (CuentaNit == codigoter) { CptoInt = CptoCta; NumLincred = Convert.ToInt32(CuentaAho); cptointcdats = cptoAhorro; }
                            else { linea = Convert.ToInt32(CptoIntereses); Numlinea = NumLincred; }
                        }
                        // else { linea = Convert.ToInt32(CptoIntereses); Numlinea = NumLincred; } // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                        break; // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                }

                if (dias > 0)
                {
                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, CptoIntereses, numero, Strings.Format(FecCompronte, "yyyyMM"), // ERROR: CS1503
                        // CptoMovtoInt, FecCompronte, 0, interes, "LIQUIDACION DE INTERESES", Usuario, myconnect, // ERROR: CS1503
                        // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, false, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, FechaUltimaLiquidacion, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, lincred); // ERROR: CS1503
                    GrabaCausacionAhorro(codigoter, Convert.ToInt32(lincred), Convert.ToDouble(numero), FecLiquidacion, estado, myconnect, new DateTime(1950,1,1), "", "A");
                }

                if (retfte > 0)
                {
                    // if (siSobreSaldoFinal == "N") // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                        // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, lincred, numero, Strings.Format(FecCompronte, "yyyyMM"), CptoRetfte, FecCompronte, 0, retfte, "RETENCION EN LA FUENTE - LIQ. INT.", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, false, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, vlrbase); // ERROR: CS1503, CS1615, CS1620
                    // else // ERROR: CS1002, CS1525
                        // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, lincred, numero, Strings.Format(FecCompronte, "yyyyMM"), CptoRetfte, FecCompronte, 0, retfte, "RETENCION EN LA FUENTE - LIQ. INT.", Usuario, myconnect, ref _u, ref StCuentaCruce, ref _u, ref _u, ref _u, ref _u, ref _u, false, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, vlrbase); // ERROR: CS1503, CS1615, CS1620
                }

                if (dias > 0 && StTercerizaContrapartida == "Y" && StCuentaCruce != "999999999999")
                {
                    // if ((interes + retfte) > 0) // ERROR: CS1002, CS1525
                        // car.GrabaMovimiento(Compcte, ConseCpte, "99999999999999", "9999", "0", Strings.Format(FecCompronte, "yyyyMM"), // ERROR: CS1503
                            // "02", FecCompronte, interes + retfte, 0, "LIQUIDACION DE INTERESES", Usuario, myconnect, // ERROR: CS1503
                            // ref _u, ref StCuentaCruce, ref NitAsoc, ref _u, ref _u, ref _u, ref _u, false); // ERROR: CS1503
                }

                if (dias > 0 && ForLiquidacion == FormaLiquidacion.LiquidaIntereses)
                {
                    switch (FormaPagoInt)
                    {
                        case FormaPagoInteres.CuentaAhorros:
                            switch (Pago)
                            {
                                case TipoPagoInteres.SoloIntereses:
                                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, CptoInt.ToString(), NumLincred.ToString(), Strings.Format(FecCompronte, "yyyyMM"), CptoMovtoInt, FecCompronte, interes, 0, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, linea.ToString(), Numlinea.ToString(), Strings.Format(FecCompronte, "yyyyMM"), TipoTransacion, FecCompronte, 0, interes, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                                    break;
                                case TipoPagoInteres.InteresesAcumulados:
                                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, CptoInt.ToString(), NumLincred.ToString(), Strings.Format(FecCompronte, "yyyyMM"), CptoMovtoInt, FecCompronte, VlrIntAcumulado + interes, 0, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, linea.ToString(), Numlinea.ToString(), Strings.Format(FecCompronte, "yyyyMM"), TipoTransacion, FecCompronte, 0, VlrIntAcumulado + interes, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                                    break;
                            }
                            break;
                        case FormaPagoInteres.Tesoreria:
                            switch (Pago)
                            {
                                case TipoPagoInteres.SoloIntereses:
                                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, CptoInt.ToString(), NumLincred.ToString(), Strings.Format(FecCompronte, "yyyyMM"), CptoMovtoInt, FecCompronte, interes, 0, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                                    // car.GrabaMovimiento(Compcte, ConseCpte, "99999999999999", "9999", "0", Strings.Format(FecCompronte, "yyyyMM"), "2", FecCompronte, 0, interes, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref CuentaTesoreria, ref NitAsoc, ref _u, ref codigoter, ref _u, ref _u); // ERROR: CS1503, CS1615, CS1620
                                    break;
                                case TipoPagoInteres.InteresesAcumulados:
                                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, CptoInt.ToString(), NumLincred.ToString(), Strings.Format(FecCompronte, "yyyyMM"), CptoMovtoInt, FecCompronte, VlrIntAcumulado + interes, 0, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                                    // car.GrabaMovimiento(Compcte, ConseCpte, "99999999999999", "9999", "0", Strings.Format(FecCompronte, "yyyyMM"), cptocapital, FecCompronte, 0, VlrIntAcumulado + interes, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref CuentaTesoreria, ref NitAsoc, ref _u, ref codigoter, ref _u, ref _u); // ERROR: CS1503, CS1615, CS1620
                                    break;
                            }
                            break;
                    }
                }

                if (ForLiquidacion != FormaLiquidacion.LiquidaIntereses)
                {
                    estado = "3";
                    // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, lincred, NumLincred.ToString(), Strings.Format(FecCompronte, "yyyyMM"), cptoAhorro, FecCompronte, Saldo, 0, "Cancelaci\u00f3n de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                    if (VlrIntAcumulado > 0)
                        // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, CptoInt.ToString(), NumLincred.ToString(), Strings.Format(FecCompronte, "yyyyMM"), CptoMovtoInt, FecCompronte, VlrIntAcumulado, 0, "Cancelaci\u00f3n de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                    if (StCuentaCruce != "999999999999")
                        // car.GrabaMovimiento(Compcte, ConseCpte, "99999999999999", "9999", "0", Strings.Format(FecCompronte, "yyyyMM"), "2", FecCompronte, ValorInteres, 0, "Liquidaci\u00f3n Automatica de Intereses de PAP", Usuario, myconnect, ref _u, ref StCuentaCruce, ref NitAsoc, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620

                    if (ForLiquidacion == FormaLiquidacion.Cancelacion)
                    {
                        SaldoAbonar = Saldo + VlrIntAcumulado;
                        switch (FormaPagoInt)
                        {
                            case FormaPagoInteres.Concepto:
                                string _det = detalleDocumento;
                                // car.BuscaComprobante(Compcte, ConseCpte, false, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _det); // ERROR: CS1501
                                detalleDocumento = _det;
                                // car.CuadreDocumento(Compcte, ConseCpte, Usuario, myconnect, detalleDocumento); // ERROR: CS1503
                                double _dif = DIF;
                                // car.BuscaComprobante(Compcte, ConseCpte, false, myconnect, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _dif); // ERROR: CS1503, CS1620
                                DIF = _dif;
                                if (DIF != 0) Interaction.MsgBox("El comprobante No. " + Compcte + "-" + ConseCpte + " no esta cuadrado, por favor revise", MsgBoxStyle.Information, "SOLIDO");
                                break;
                            case FormaPagoInteres.CuentaAhorros:
                                // car.GrabaMovimiento(Compcte, ConseCpte, codigoter, linea.ToString(), Numlinea.ToString(), Strings.Format(FecCompronte, "yyyyMM"), TipoTransacion, FecCompronte, 0, SaldoAbonar, "Cancelaci\u00f3n de PAP", Usuario, myconnect, ref _u, ref _u, ref _u, ref _u, ref codigoter, ref _u, ref _u, false); // ERROR: CS1503, CS1615, CS1620
                                break;
                            case FormaPagoInteres.Tesoreria:
                                // car.GrabaMovimiento(Compcte, ConseCpte, "99999999999999", "9999", "0", Strings.Format(FecCompronte, "yyyyMM"), cptocapital, FecCompronte, 0, SaldoAbonar, "Cancelaci\u00f3n de PAP", Usuario, myconnect, ref _u, ref CuentaTesoreria, ref NitAsoc, ref _u, ref codigoter, ref _u, ref _u); // ERROR: CS1503, CS1615, CS1620
                                break;
                        }
                    }
                    feccancela = FecCompronte;
                }

                // if (cierre) car.TrasladaContabilidad(Compcte, ConseCpte, myconnect, Usuario); // ERROR: CS1503
                if (estado.Trim() == "3") opcionliquida = "C";

                GrabaCausacionAhorro(codigoter, Convert.ToInt32(lincred), Convert.ToDouble(numero), FecLiquidacion, estado, myconnect, feccancela, usucancela_PAP, opcionliquida);
                valida_liquidadPAP = true;
            }
            else
            {
                DatLiquidacion.Tables["DatosLiq"].Rows.Add(lincred, numero, codigoter, nombre, Saldo, ValorInteres, porpago_int, retfte, (ValorInteres - retfte), dias, Saldo, saldot);
                valida_liquidadPAP = false;
            }
        }

        // ----------------------------------------------------------------
        // LiquidacionSobreSaldoFinal_Individual
        // ----------------------------------------------------------------
        public void LiquidacionSobreSaldoFinal_Individual(double num_cuenta, int lincred, string codigoter, OdbcConnection myconnect,
            DateTime FechaUltimaCuasacion, DateTime FechaLiquidacion, ref string NombreCompleto, ref double Base, ref double Interes)
        {
            DataSet myread = new DataSet();
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            int canreg = 0; double SaldoFinal = 0; double saldot = 0; double Saldiario = 0; double saldoMoviAnterio = 0;
            int fila = 0;

            stbuilder.Append(" SELECT copmae.codigoter,copmae.lincred,copmae.numero, apellido,maenit.nombre as Nombre,  maenit.agencia,  parame12.cuenta, parame12.centroco ,");
            stbuilder.Append(" saldo.saldo, saldo.SALDO_INICIAL as SaldoInicial, (sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) AS saldiario ");
            stbuilder.Append(" from cop_movimto movto ");
            stbuilder.Append(" RIGHT outer join  cop_docmto doc on movto.compronte=doc.compronte ");
            stbuilder.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto > '" + Strings.Format(FechaUltimaCuasacion, Var.varini.PstForFec) + "'  and   movto.fecha_Movto <='" + Strings.Format(FechaLiquidacion, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
            stbuilder.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
            stbuilder.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
            stbuilder.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
            stbuilder.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FechaUltimaCuasacion, "yyyyMM") + "' ");
            stbuilder.Append(" where maenit.estado<>'T' and parame58.lincred = " + lincred + " and  maenit.codigoter ='" + codigoter + "'");
            stbuilder.Append(" and copmae.lincred=" + lincred + " and copmae.numero= " + num_cuenta);
            stbuilder.Append(" group by copmae.codigoter,copmae.lincred,copmae.numero, apellido,maenit.nombre ,  maenit.agencia,  parame12.cuenta, parame12.centroco,");
            stbuilder.Append(" saldo.saldo, saldo.SALDO_INICIAL ");
            stbuilder.Append(" order by copmae.codigoter,copmae.lincred,copmae.numero");

            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "SobreSaldoFinal", myread, "TblSalFinal");
            canreg = myread.Tables["TblSalFinal"].Rows.Count;
            while (fila < canreg)
            {
                DataRow _r = myread.Tables["TblSalFinal"].Rows[fila];
                int dia = FechaUltimaCuasacion.Day;
                saldot = (_r["SaldoInicial"] is DBNull) ? 0 : Convert.ToDouble(_r["SaldoInicial"]);
                if (dia != 1 || saldot == 0)
                {
                    DateTime fechaFinalCapital; DateTime fechaInicioCapital;
                    if (dia != 1)
                    {
                        fechaFinalCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, dia);
                        fechaInicioCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, 1);
                    }
                    else
                    {
                        fechaFinalCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, 1);
                        fechaInicioCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, 1);
                    }
                    saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(num_cuenta, lincred, codigoter, fechaInicioCapital, fechaFinalCapital, myconnect);
                }
                else saldoMoviAnterio = 0;

                Saldiario = (_r["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_r["saldiario"]);
                NombreCompleto = Convert.ToString(_r["apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                SaldoFinal = Math.Round((saldot + Saldiario + saldoMoviAnterio) * -1, 0);
                fila += 1;
            }
            Base = SaldoFinal;
        }

        // ----------------------------------------------------------------
        // LIquidaSaldoMinimo_Individual
        // ----------------------------------------------------------------
        public void LIquidaSaldoMinimo_Individual(double num_cuenta, int lincred, string codigoter, OdbcConnection myconnect,
            DateTime FechaUltimaCuasacion, DateTime FechaLiquidacion, ref string NombreCompleto, ref double Base, ref double Interes)
        {
            DataSet DsLiqInt = new DataSet(); double Fila = 0; int dias = 1; double SaldoMinimo = 0; int sw = 0;
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string cuenta = null; string CuentaNew = null; string CuentaCiclo = null;
            double SaldoInicial = 0; double SaldoDiario = 0; double numero = 0;
            string Nombre = " ";
            double saldoMoviAnterio = 0; double FilaBuesque = 0;
            int i = 0;
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            int verificarEntrada = 0; string cuentaBusquedad = "";

            stbuilder.Append("SELECT copmae.codigoter,copmae.lincred,copmae.numero,movto.fecha_movto,maenit.nombre,maenit.apellido, ");
            stbuilder.Append("saldo.Saldo_inicial as saldoInicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,parame58.CptoInteres,");
            stbuilder.Append("parame58.perpago_int,(sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) as saldiario,maehor.estado   ");
            stbuilder.Append(" from cop_movimto movto ");
            stbuilder.Append(" RIGHT outer join cop_docmto doc on movto.compronte=doc.compronte ");
            stbuilder.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto > '" + Strings.Format(FechaUltimaCuasacion, Var.varini.PstForFec) + "' and  movto.fecha_Movto <= '" + Strings.Format(FechaLiquidacion, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
            stbuilder.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
            stbuilder.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
            stbuilder.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
            stbuilder.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FechaUltimaCuasacion, "yyyyMM") + "' ");
            stbuilder.Append(" left join dep_maeahor maehor on  saldo.numero = maehor.num_cuenta    ");
            stbuilder.Append(" where maenit.estado<>'T' and parame58.lincred = " + lincred + " and maenit.codigoter = '" + codigoter + "'  and copmae.numero=" + num_cuenta);
            stbuilder.Append(" group by copmae.codigoter,copmae.lincred,copmae.numero,movto.fecha_movto,maenit.nombre,maenit.apellido, ");
            stbuilder.Append(" saldo.Saldo_inicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,parame58.CptoInteres,");
            stbuilder.Append(" parame58.perpago_int,maehor.estado  ");
            stbuilder.Append(" order by copmae.codigoter, copmae.lincred, copmae.numero,movto.fecha_movto ");

            ok = CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LIquidaSaldoMinimo", DsLiqInt, "tblLiqSalMin");
            if (ok)
            {
                while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                {
                    DataRow _r = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                    CuentaNew = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                    i = 0;
                    if (cuenta != CuentaNew)
                    {
                        cuentaBusquedad = ""; SaldoMinimo = 0; SaldoInicial = 0; sw = 0;
                        SaldoInicial = (_r["SaldoInicial"] is DBNull) ? 0 : Convert.ToDouble(_r["saldoInicial"]) * -1;
                        saldoMoviAnterio = 0;
                        int dia = FechaUltimaCuasacion.Day;
                        if (dia != 1 && verificarEntrada == 0)
                        {
                            verificarEntrada = 1;
                            DateTime fechaFinalCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, dia);
                            DateTime fechaInicioCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, 1);
                            saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(num_cuenta, lincred, codigoter, fechaInicioCapital, fechaFinalCapital, myconnect);
                            SaldoInicial += (saldoMoviAnterio * -1);
                        }
                        else
                        {
                            if (SaldoInicial == 0)
                            {
                                DateTime fechaFinalCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, dia);
                                DateTime fechaInicioCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, 1);
                                saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(num_cuenta, lincred, codigoter, fechaInicioCapital, fechaFinalCapital, myconnect);
                                SaldoInicial += (saldoMoviAnterio * -1);
                            }
                        }
                        SaldoMinimo = SaldoInicial;
                        codigoter = Convert.ToString(_r["codigoter"]);
                        lincred = Convert.ToInt32(_r["lincred"]);
                        numero = Convert.ToDouble(_r["numero"]);
                        cuenta = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                        Nombre = Convert.ToString(_r["Apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                        if (SaldoMinimo == 0)
                        {
                            while (FilaBuesque < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                            {
                                DataRow _rb = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)FilaBuesque];
                                cuentaBusquedad = Convert.ToString(_rb["codigoter"]) + Convert.ToString(_rb["lincred"]) + Convert.ToString(_rb["numero"]);
                                if (cuenta == cuentaBusquedad)
                                {
                                    SaldoDiario = (_rb["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_rb["saldiario"]);
                                    SaldoMinimo = (SaldoDiario * -1);
                                    if (SaldoMinimo != 0) break;
                                }
                                FilaBuesque += 1;
                            }
                        }
                        SaldoDiario = 0;
                    }
                    while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                    {
                        DataRow _ri = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                        if (!(_ri["fecha_movto"] is DBNull))
                        {
                            SaldoDiario = (_ri["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_ri["saldiario"]);
                            SaldoInicial += (SaldoDiario * -1);
                            int _diasGracia = Convert.ToInt32(_ri["DIAS_GRACIA"]);
                            if (_diasGracia != 0)
                            {
                                dias = clsliqcredito.CalculaDias(Convert.ToDateTime(_ri["fecha_movto"]), FechaUltimaCuasacion) + 1;
                                if (dias <= _diasGracia) SaldoMinimo = SaldoInicial;
                            }
                            if (SaldoInicial < SaldoMinimo)
                            {
                                if (_diasGracia == 0) SaldoMinimo = SaldoInicial;
                                else
                                {
                                    dias = clsliqcredito.CalculaDias(Convert.ToDateTime(_ri["fecha_movto"]), FechaUltimaCuasacion) + 1;
                                    if (dias > _diasGracia) SaldoMinimo = SaldoInicial;
                                    else SaldoMinimo = 0;
                                }
                            }
                        }
                        if (Fila != DsLiqInt.Tables["tblLiqSalMin"].Rows.Count - 1)
                        {
                            DataRow _rn = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila + 1];
                            CuentaCiclo = Convert.ToString(_rn["codigoter"]) + Convert.ToString(_rn["lincred"]) + Convert.ToString(_rn["numero"]);
                            if (CuentaCiclo != cuenta) break;
                            else Fila += 1;
                        }
                        else break;
                    }
                    Fila += 1;
                }
            }
            Base = SaldoMinimo;
            NombreCompleto = Nombre;
        }

        // ----------------------------------------------------------------
        // LIquidaSaldoPromedio_Individual
        // ----------------------------------------------------------------
        public void LIquidaSaldoPromedio_Individual(double num_cuenta, int lincred, string codigoter, OdbcConnection myconnect,
            DateTime FechaUltimaCuasacion, DateTime FechaLiquidacion, ref string NombreCompleto, ref double Base, ref double Interes)
        {
            LIquidaSaldoPromedio_Individual(num_cuenta, lincred, codigoter, myconnect, FechaUltimaCuasacion, FechaLiquidacion, ref NombreCompleto, ref Base, ref Interes, "N", 0);
        }

        public void LIquidaSaldoPromedio_Individual(double num_cuenta, int lincred, string codigoter, OdbcConnection myconnect,
            DateTime FechaUltimaCuasacion, DateTime FechaLiquidacion, ref string NombreCompleto, ref double Base, ref double Interes,
            string manejarFomaPAP, double DiasLiquidacion)
        {
            DataSet DsLiqInt = new DataSet(); double Fila = 0; int dias = 1; double SaldoCuenta = 0;
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string cuenta = null; string CuentaNew = null;
            double SaldoInicial = 0; int DiasMov = 0; double SaldoDiario = 0; double numero = 0;
            string Nombre = " "; double saldo_cta = 0; double SaldoAcu = 0; string PeriodoMov = "";
            int i = 0; int DiasPromedio = 0;
            DateTime FechaMovto = new DateTime(1950, 1, 1); DateTime SigFecMovto = new DateTime(1950, 1, 1);
            double saldoMoviAnterio = 0; int verificarEntrada = 0;

            stbuilder.Append(" select copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,");
            stbuilder.Append(" saldo.Saldo_inicial as saldoInicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,");
            stbuilder.Append(" parame58.perpago_int, (sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) as saldiario ");
            stbuilder.Append(" from cop_movimto movto ");
            stbuilder.Append(" RIGHT outer join cop_docmto doc on movto.compronte=doc.compronte ");
            stbuilder.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto > '" + Strings.Format(FechaUltimaCuasacion, Var.varini.PstForFec) + "' and  movto.fecha_Movto <= '" + Strings.Format(FechaLiquidacion, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
            stbuilder.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
            stbuilder.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
            stbuilder.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
            stbuilder.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FechaUltimaCuasacion, "yyyyMM") + "' ");
            stbuilder.Append(" left join dep_maeahor maehor on  saldo.numero = maehor.num_cuenta and maehor.estado <> 3  ");
            stbuilder.Append(" where maenit.estado<>'T' and parame58.lincred = " + lincred + " and  maenit.codigoter = '" + codigoter + "' and copmae.numero= " + num_cuenta);
            stbuilder.Append(" group by copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,");
            stbuilder.Append(" saldo.Saldo_inicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,");
            stbuilder.Append(" parame58.perpago_int  ");
            stbuilder.Append(" order by copmae.codigoter, copmae.lincred, copmae.numero,movto.fecha_movto ");

            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LIquidaSaldoPromedio", DsLiqInt, "tblLiqSalMin");

            while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
            {
                DataRow _r = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                CuentaNew = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                i = 0;
                if (cuenta != CuentaNew)
                {
                    SaldoCuenta = 0; SaldoInicial = 0; saldo_cta = 0; SaldoAcu = 0;
                    dias = FechaUltimaCuasacion.Day; DiasMov = FechaUltimaCuasacion.Day;
                    int diaFinal = DateTime.DaysInMonth(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month);
                    if (dias == diaFinal)
                    {
                        dias = 1; DiasMov = 1;
                        DateTime fechasig = FechaUltimaCuasacion.AddDays(1);
                        PeriodoMov = Strings.Format(fechasig, "yyyyMM");
                        FechaMovto = fechasig;
                        DiasPromedio = (int)(FechaLiquidacion - fechasig).TotalDays + 1;
                    }
                    else if (dias != 1) { dias += 1; DiasMov += 1; DiasPromedio = (int)(FechaLiquidacion - FechaUltimaCuasacion).TotalDays; PeriodoMov = Strings.Format(FechaUltimaCuasacion, "yyyyMM"); FechaMovto = FechaUltimaCuasacion; }
                    else { dias = 1; DiasMov = 1; DiasPromedio = (int)(FechaLiquidacion - FechaUltimaCuasacion).TotalDays + 1; PeriodoMov = Strings.Format(FechaUltimaCuasacion, "yyyyMM"); FechaMovto = FechaUltimaCuasacion; }

                    SaldoInicial = (_r["SaldoInicial"] is DBNull) ? 0 : Convert.ToDouble(_r["saldoInicial"]) * -1;
                    int dias1 = FechaUltimaCuasacion.Day;
                    saldoMoviAnterio = 0;
                    if (dias1 != 1)
                    {
                        verificarEntrada = 1;
                        DateTime fechaFinalCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, dias1);
                        DateTime fechaInicioCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, 1);
                        saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(num_cuenta, lincred, codigoter, fechaInicioCapital, fechaFinalCapital, myconnect);
                        SaldoInicial += (saldoMoviAnterio * -1);
                    }
                    else if (SaldoInicial == 0)
                    {
                        verificarEntrada = 1;
                        DateTime fechaFinalCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, dias1);
                        DateTime fechaInicioCapital = new DateTime(FechaUltimaCuasacion.Year, FechaUltimaCuasacion.Month, 1);
                        saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(num_cuenta, lincred, codigoter, fechaInicioCapital, fechaFinalCapital, myconnect);
                        SaldoInicial += (saldoMoviAnterio * -1);
                    }
                    saldo_cta = SaldoInicial; SaldoCuenta = SaldoInicial;
                    codigoter = Convert.ToString(_r["codigoter"]);
                    lincred = Convert.ToInt32(_r["lincred"]);
                    numero = Convert.ToDouble(_r["numero"]);
                    cuenta = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                    Nombre = Convert.ToString(_r["Apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                }
                while (Fila < DsLiqInt.Tables["tblLiqSalMin"].Rows.Count)
                {
                    DataRow _ri = DsLiqInt.Tables["tblLiqSalMin"].Rows[(int)Fila];
                    if (!(_ri["fecha_movto"] is DBNull))
                    {
                        SaldoDiario = (_ri["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_ri["saldiario"]);
                    Recorredias:
                        if (PeriodoMov != Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM"))
                        {
                            while (dias <= DateTime.DaysInMonth(FechaMovto.Year, FechaMovto.Month)) { SaldoAcu += saldo_cta; dias += 1; }
                            dias = 1;
                            SigFecMovto = new DateTime(FechaMovto.Year, FechaMovto.Month, 1).AddMonths(1);
                            if (Strings.Format(SigFecMovto, "yyyyMM") == Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM"))
                                PeriodoMov = Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM");
                            else { FechaMovto = SigFecMovto; goto Recorredias; }
                        }
                        DiasMov = Convert.ToInt32(Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "dd"));
                        while (dias < DiasMov) { SaldoAcu += saldo_cta; dias += 1; }
                        saldo_cta += (SaldoDiario * -1);
                        SaldoAcu += saldo_cta; DiasMov += 1;
                        if (DiasPromedio == 0) { if (saldo_cta < SaldoCuenta) SaldoCuenta = saldo_cta; }
                        else SaldoCuenta = Math.Round(SaldoAcu / DiasPromedio, 2);
                        FechaMovto = Convert.ToDateTime(_ri["fecha_movto"]);
                        dias += 1;
                    }
                    Fila += 1;
                }

                PeriodoMov = Strings.Format(FechaMovto, "yyyyMM");
                if (PeriodoMov != Strings.Format(FechaLiquidacion, "yyyyMM"))
                {
                    while (DiasMov <= DateTime.DaysInMonth(FechaMovto.Year, FechaMovto.Month)) { SaldoAcu += saldo_cta; DiasMov += 1; }
                    DiasMov = 1;
                    if (DiasMov <= Convert.ToInt32(Strings.Format(FechaLiquidacion, "dd")))
                        while (DiasMov <= Convert.ToInt32(Strings.Format(FechaLiquidacion, "dd"))) { SaldoAcu += saldo_cta; DiasMov += 1; }
                }
                else
                {
                    if (DiasMov <= Convert.ToInt32(Strings.Format(FechaLiquidacion, "dd")))
                        while (DiasMov <= Convert.ToInt32(Strings.Format(FechaLiquidacion, "dd"))) { SaldoAcu += saldo_cta; DiasMov += 1; }
                }
                if (DiasPromedio > 0) SaldoCuenta = Math.Round(SaldoAcu / DiasPromedio, 2);

                Fila += 1;
            }
            Base = SaldoCuenta;
            NombreCompleto = Nombre;
        }

        // ----------------------------------------------------------------
        // LIquidaSaldoMinimo_Pap
        // ----------------------------------------------------------------
        public void LIquidaSaldoMinimo_Pap(string Cpte, string ConseCpte, int LineaArros, DateTime FecIni, DateTime FecFin, DateTime FechaLiquidacion,
            string Usuario, Form Myforma, OdbcConnection Myconect, string empresa, string cencosto, bool LiqRetirado, string StTercerizaCruce)
        {
            DataSet DsLiqInt = new DataSet(); DataSet DsInforme = new DataSet();
            double Fila = 0; int dias = 1; double SaldoMinimo = 0;
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string cuenta = null; string CuentaNew = null; string CuentaCiclo = null;
            double SaldoInicial = 0; int DiasMov = 0; double SaldoDiario = 0;
            string codigoter = " "; int lincred = 0; double numero = 0; string Nombre = " ";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquida Saldo Minimo Mensual", Myforma);
            int i = 0; int DiasaLiquidar = 30; double FilaBuesque = 0; int filaAso = 0;
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            double saldoMoviAnterio = 0; int verificarEntrada = 0; string cuentaBusquedad = "";
            string codigoter_PAP = ""; int lincred_Pap = 0; double numero_pap = 0; string Nombre_pap = ""; int DiaLiq = 0;

            DsLiqInt.Tables.Add("tbldatosLiq");
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("CODIGOTER", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NOMBRE", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("LINCRED", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NUMERO", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("BASE", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("INTERES", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("Dias_PAP", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("FechaUltLiquid", typeof(DateTime));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("ValidaLiquida", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("FechaLiquidacion", typeof(DateTime));

            stbuilder.Append("SELECT copmae.codigoter,copmae.lincred,copmae.numero, maenit.apellido,maenit.nombre as Nombre,  maenit.agencia,  parame12.cuenta, parame12.centroco ,maeahor.fec_crea,copmae.FECCIERRE,maeahor.FECVEMTO");
            stbuilder.Append(" FROM cop_maecar  copmae  inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  ");
            stbuilder.Append(" and saldo.lincred = copmae.lincred and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FecFin, "yyyyMM") + "' ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred ");
            stbuilder.Append(" inner join dep_maeahor maeahor on  copmae.codigoter = maeahor.codigoter and maeahor.lincred = copmae.lincred ");
            stbuilder.Append(" and copmae.numero = maeahor.num_cuenta");
            stbuilder.Append(" where maenit.estado<>'T'  and parame58.lincred = " + LineaArros + empresa + cencosto + (LiqRetirado == false ? " and maenit.estado<>'R' " : ""));
            stbuilder.Append(" and   maeahor.Estado <> 3  ");

            DataSet dataAsociadoPap = new DataSet();
            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), Myconect, "SobreSaldoFinal", dataAsociadoPap, "TblCuentaPap");
            msgbarra.ValorMinimoMaximo(0, dataAsociadoPap.Tables["TblCuentaPap"].Rows.Count);
            msgbarra.Show();

            for (filaAso = 0; filaAso <= dataAsociadoPap.Tables["TblCuentaPap"].Rows.Count - 1; filaAso++)
            {
                DataRow _row = dataAsociadoPap.Tables["TblCuentaPap"].Rows[filaAso];
                DateTime fecha_creacion = Convert.ToDateTime(Strings.Format(_row["fec_crea"], Var.varini.PstForFec));
                DateTime Fehca_Causacion = Convert.ToDateTime(Strings.Format(_row["FECCIERRE"], Var.varini.PstForFec));
                DateTime fecha_vencimiento = Convert.ToDateTime(Strings.Format(_row["FECVEMTO"], Var.varini.PstForFec));
                DateTime fecha_liquidacion = Convert.ToDateTime(Strings.Format(FecFin, Var.varini.PstForFec));
                DateTime fecha_Sistema = Convert.ToDateTime(Strings.Format(DateTime.Now, Var.varini.PstForFec));
                verificarEntrada = 0; saldoMoviAnterio = 0;
                codigoter_PAP = Convert.ToString(_row["codigoter"]);
                lincred_Pap = Convert.ToInt32(_row["lincred"]);
                numero_pap = Convert.ToDouble(_row["numero"]);
                Nombre_pap = Convert.ToString(_row["Apellido"]) + " " + Convert.ToString(_row["Nombre"]);
                if (Convert.ToDateTime(Strings.Format(fecha_liquidacion, Var.varini.PstForFec)) > Convert.ToDateTime(Strings.Format(fecha_vencimiento, Var.varini.PstForFec)))
                    fecha_liquidacion = fecha_vencimiento;

                if (fecha_creacion < fecha_liquidacion && fecha_liquidacion <= fecha_vencimiento && fecha_liquidacion > Fehca_Causacion && fecha_liquidacion <= fecha_Sistema)
                {
                    DataSet DsLiqInt1 = new DataSet();
                    System.Text.StringBuilder stbuilder2 = new System.Text.StringBuilder();
                    FilaBuesque = 0; Fila = 0; cuenta = null;

                    stbuilder2.Append(" SELECT copmae.codigoter,copmae.lincred,copmae.numero,movto.fecha_movto,maenit.nombre,maenit.apellido, ");
                    stbuilder2.Append(" saldo.Saldo_inicial as saldoInicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,parame58.CptoInteres,");
                    stbuilder2.Append(" parame58.perpago_int,(sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) as saldiario,copmae.FECCIERRE ");
                    stbuilder2.Append(" from cop_movimto movto ");
                    stbuilder2.Append(" RIGHT outer join cop_docmto  doc on movto.compronte=doc.compronte ");
                    stbuilder2.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto > '" + Strings.Format(Fehca_Causacion, Var.varini.PstForFec) + "' and  movto.fecha_Movto <= '" + Strings.Format(fecha_liquidacion, Var.varini.PstForFec) + "' ");
                    stbuilder2.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
                    stbuilder2.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
                    stbuilder2.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
                    stbuilder2.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
                    stbuilder2.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
                    stbuilder2.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
                    stbuilder2.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
                    stbuilder2.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(Fehca_Causacion, "yyyyMM") + "' ");
                    stbuilder2.Append(" where maenit.estado<>'T' and parame58.lincred = " + lincred_Pap + " and maenit.codigoter = '" + codigoter_PAP + "' and copmae.numero=" + numero_pap);
                    stbuilder2.Append(" group by copmae.codigoter,copmae.lincred,copmae.numero,movto.fecha_movto,maenit.nombre,maenit.apellido, ");
                    stbuilder2.Append(" saldo.Saldo_inicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,parame58.CptoInteres,");
                    stbuilder2.Append(" parame58.perpago_int,copmae.FECCIERRE");
                    stbuilder2.Append(" order by copmae.codigoter, copmae.lincred, copmae.numero,movto.fecha_movto ");

                    ok = CargaVarini.ExecuteQueryDataset(stbuilder2.ToString(), Myconect, "LIquidaSaldoMinimo", DsLiqInt1, "tblLiqSalMin");
                    if (ok)
                    {
                        while (Fila < DsLiqInt1.Tables["tblLiqSalMin"].Rows.Count)
                        {
                            DataRow _r = DsLiqInt1.Tables["tblLiqSalMin"].Rows[(int)Fila];
                            CuentaNew = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                            i = 0;
                            if (cuenta != CuentaNew)
                            {
                                cuentaBusquedad = ""; SaldoMinimo = 0; SaldoInicial = 0;
                                SaldoInicial = (_r["SaldoInicial"] is DBNull) ? 0 : Convert.ToDouble(_r["saldoInicial"]) * -1;
                                saldoMoviAnterio = 0;
                                int dia = Fehca_Causacion.Day;
                                if (dia != 1 && verificarEntrada == 0)
                                {
                                    verificarEntrada = 1;
                                    DateTime fechaFinalCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, dia);
                                    DateTime fechaInicioCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, 1);
                                    saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(numero_pap, lincred_Pap, codigoter_PAP, fechaInicioCapital, fechaFinalCapital, Myconect);
                                    SaldoInicial += (saldoMoviAnterio * -1);
                                }
                                else if (SaldoInicial == 0)
                                {
                                    DateTime fechaFinalCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, Fehca_Causacion.Day);
                                    DateTime fechaInicioCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, 1);
                                    saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(numero_pap, lincred_Pap, codigoter_PAP, fechaInicioCapital, fechaFinalCapital, Myconect);
                                    SaldoInicial += (saldoMoviAnterio * -1);
                                }
                                SaldoMinimo = SaldoInicial;
                                codigoter = Convert.ToString(_r["codigoter"]); lincred = Convert.ToInt32(_r["lincred"]);
                                numero = Convert.ToDouble(_r["numero"]);
                                cuenta = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                                Nombre = Convert.ToString(_r["Apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                                FilaBuesque = Fila;
                                if (SaldoMinimo == 0)
                                {
                                    while (FilaBuesque < DsLiqInt1.Tables["tblLiqSalMin"].Rows.Count)
                                    {
                                        DataRow _rb = DsLiqInt1.Tables["tblLiqSalMin"].Rows[(int)FilaBuesque];
                                        cuentaBusquedad = Convert.ToString(_rb["codigoter"]) + Convert.ToString(_rb["lincred"]) + Convert.ToString(_rb["numero"]);
                                        if (cuenta == cuentaBusquedad)
                                        {
                                            SaldoDiario = (_rb["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_rb["saldiario"]);
                                            SaldoMinimo = (SaldoDiario * -1);
                                            if (SaldoMinimo != 0) break;
                                        }
                                        FilaBuesque += 1;
                                    }
                                }
                                SaldoDiario = 0;
                            }
                            while (Fila < DsLiqInt1.Tables["tblLiqSalMin"].Rows.Count)
                            {
                                DataRow _ri = DsLiqInt1.Tables["tblLiqSalMin"].Rows[(int)Fila];
                                if (!(_ri["fecha_movto"] is DBNull))
                                {
                                    SaldoDiario = (_ri["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_ri["saldiario"]);
                                    SaldoInicial += (SaldoDiario * -1);
                                    int _dg = Convert.ToInt32(_ri["DIAS_GRACIA"]);
                                    if (_dg != 0) { dias = clsliqcredito.CalculaDias(Convert.ToDateTime(_ri["fecha_movto"]), Fehca_Causacion) + 1; if (dias <= _dg) SaldoMinimo = SaldoInicial; }
                                    if (SaldoInicial < SaldoMinimo)
                                    { if (_dg == 0) SaldoMinimo = SaldoInicial; else { dias = clsliqcredito.CalculaDias(Convert.ToDateTime(_ri["fecha_movto"]), Fehca_Causacion) + 1; if (dias > _dg) SaldoMinimo = SaldoInicial; else SaldoMinimo = 0; } }
                                }
                                if (Fila != DsLiqInt1.Tables["tblLiqSalMin"].Rows.Count - 1)
                                {
                                    DataRow _rn = DsLiqInt1.Tables["tblLiqSalMin"].Rows[(int)Fila + 1];
                                    CuentaCiclo = Convert.ToString(_rn["codigoter"]) + Convert.ToString(_rn["lincred"]) + Convert.ToString(_rn["numero"]);
                                    if (CuentaCiclo != cuenta) break; else Fila += 1;
                                }
                                else break;
                            }
                            DiaLiq = calcula_Dias_Liquidacion(Fehca_Causacion, fecha_liquidacion);
                            Fila += 1;
                            if (SaldoMinimo > 0 && DiaLiq > 0)
                                DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoMinimo, 0, DiaLiq, Fehca_Causacion, "Y", fecha_liquidacion);
                            else
                                DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoMinimo, 0, DiaLiq, Fehca_Causacion, "N", fecha_liquidacion);
                        }
                        Fila = 0;
                    }
                    Fila = 0;
                }
                else
                    DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter_PAP, Nombre_pap, lincred_Pap, numero_pap, 0, 0, 0, Fehca_Causacion, "N", fecha_liquidacion);
                msgbarra.PerformStep();
            }

            if (!Information.IsNumeric(ConseCpte)) ConseCpte = "0";
            msgbarra.Close(); msgbarra.Dispose();
            if (DsLiqInt.Tables["tbldatosliq"].Rows.Count > 0)
            {
                DsInforme = LiquidacionInteresesCuentasAhorro_PAP(DsLiqInt.Tables["tbldatosliq"], Cpte, Convert.ToDouble(ConseCpte), LineaArros.ToString(), FechaLiquidacion, Usuario, DiasaLiquidar, StTercerizaCruce, Myforma, Myconect, FecFin);
                if (Convert.ToDouble(ConseCpte) == 0)
                    ImprimirLiquidacionIntereses(LineaArros.ToString(), FechaLiquidacion, DsInforme, 1, Myforma, Myconect);
            }
        }

        // ----------------------------------------------------------------
        // LIquidaSaldoPromedio_PAP
        // ----------------------------------------------------------------
        public void LIquidaSaldoPromedio_PAP(string Cpte, string ConseCpte, int LineaArros, DateTime FecIni, DateTime FecFin, DateTime FechaLiquidacion,
            string Usuario, Form Myforma, OdbcConnection Myconect, string empresa, string cencosto, bool LiqRetirado, string StTercerizaCruce)
        {
            DataSet DsLiqInt = new DataSet(); DataSet DsInforme = new DataSet();
            double Fila = 0; int dias = 1; double SaldoCuenta = 0;
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string cuenta = null; string CuentaNew = null;
            double SaldoInicial = 0; int DiasMov = 0; double SaldoDiario = 0;
            string codigoter = " "; int lincred = 0; double numero = 0; string Nombre = " ";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquida Saldo Promedio", Myforma);
            int i = 0; int DiasPromedio = 0; double saldo_cta = 0; double SaldoAcu = 0; string PeriodoMov = "";
            DateTime FechaMovto = new DateTime(1950, 1, 1); DateTime SigFecMovto = new DateTime(1950, 1, 1); int DiasaLiquidar = 30;
            int verificarEntrada = 0; int filaAso = 0;
            string codigoter_PAP = ""; int lincred_Pap = 0; double numero_pap = 0; int DiaLiq = 0; string nombre_pap = "";
            double saldoMoviAnterio = 0;

            DsLiqInt.Tables.Add("tbldatosLiq");
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("CODIGOTER", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NOMBRE", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("LINCRED", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("NUMERO", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("BASE", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("INTERES", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("Dias_PAP", typeof(double));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("FechaUltLiquid", typeof(DateTime));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("ValidaLiquida", typeof(string));
            DsLiqInt.Tables["tbldatosliq"].Columns.Add("FechaLiquidacion", typeof(DateTime));

            stbuilder.Append("SELECT copmae.codigoter,copmae.lincred,copmae.numero, apellido,maenit.nombre as Nombre,  maenit.agencia,  parame12.cuenta, parame12.centroco ,maeahor.fec_crea,copmae.FECCIERRE,maeahor.FECVEMTO");
            stbuilder.Append(" FROM cop_maecar  copmae  inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  ");
            stbuilder.Append(" and saldo.lincred = copmae.lincred and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FecFin, "yyyyMM") + "' ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred ");
            stbuilder.Append(" inner join dep_maeahor maeahor on  copmae.codigoter = maeahor.codigoter and maeahor.lincred = copmae.lincred ");
            stbuilder.Append(" and copmae.numero = maeahor.num_cuenta");
            stbuilder.Append(" where maenit.estado<>'T'  and parame58.lincred = " + LineaArros + empresa + cencosto + (LiqRetirado == false ? " and maenit.estado<>'R' " : ""));
            stbuilder.Append(" and   maeahor.Estado <> 3 ");

            DataSet dataAsociadoPap = new DataSet();
            CargaVarini.ExecuteQueryDataset(stbuilder.ToString(), Myconect, "LIquidaSaldoPromedio", dataAsociadoPap, "TblCuentaPap");
            msgbarra.ValorMinimoMaximo(0, dataAsociadoPap.Tables["TblCuentaPap"].Rows.Count);
            msgbarra.Show();

            for (filaAso = 0; filaAso <= dataAsociadoPap.Tables["TblCuentaPap"].Rows.Count - 1; filaAso++)
            {
                DataRow _row = dataAsociadoPap.Tables["TblCuentaPap"].Rows[filaAso];
                DateTime fecha_creacion = Convert.ToDateTime(Strings.Format(_row["fec_crea"], Var.varini.PstForFec));
                DateTime Fehca_Causacion = Convert.ToDateTime(Strings.Format(_row["FECCIERRE"], Var.varini.PstForFec));
                DateTime fecha_vencimiento = Convert.ToDateTime(Strings.Format(_row["FECVEMTO"], Var.varini.PstForFec));
                DateTime fecha_liquidacion = Convert.ToDateTime(Strings.Format(FecFin, Var.varini.PstForFec));
                DateTime fecha_Sistema = Convert.ToDateTime(Strings.Format(DateTime.Now, Var.varini.PstForFec));
                saldoMoviAnterio = 0;
                codigoter_PAP = Convert.ToString(_row["codigoter"]);
                lincred_Pap = Convert.ToInt32(_row["lincred"]);
                numero_pap = Convert.ToDouble(_row["numero"]);
                nombre_pap = Convert.ToString(_row["Apellido"]) + " " + Convert.ToString(_row["Nombre"]);
                if (Convert.ToDateTime(Strings.Format(fecha_liquidacion, Var.varini.PstForFec)) > Convert.ToDateTime(Strings.Format(fecha_vencimiento, Var.varini.PstForFec)))
                    fecha_liquidacion = fecha_vencimiento;

                if (fecha_creacion < fecha_liquidacion && fecha_liquidacion <= fecha_vencimiento && fecha_liquidacion > Fehca_Causacion && fecha_liquidacion <= fecha_Sistema)
                {
                    DataSet DsLiqInt1 = new DataSet();
                    System.Text.StringBuilder stbuilder2 = new System.Text.StringBuilder();
                    cuenta = ""; Fila = 0;

                    stbuilder2.Append(" select copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,");
                    stbuilder2.Append(" saldo.Saldo_inicial as saldoInicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,");
                    stbuilder2.Append(" parame58.perpago_int, (sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) as saldiario,copmae.FECCIERRE ");
                    stbuilder2.Append(" from cop_movimto movto ");
                    stbuilder2.Append(" RIGHT outer join cop_docmto doc on movto.compronte=doc.compronte ");
                    stbuilder2.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto > '" + Strings.Format(Fehca_Causacion, Var.varini.PstForFec) + "' and  movto.fecha_Movto <= '" + Strings.Format(fecha_liquidacion, Var.varini.PstForFec) + "' ");
                    stbuilder2.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
                    stbuilder2.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
                    stbuilder2.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
                    stbuilder2.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
                    stbuilder2.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
                    stbuilder2.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
                    stbuilder2.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
                    stbuilder2.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(Fehca_Causacion, "yyyyMM") + "' ");
                    stbuilder2.Append(" where maenit.estado<>'T' and parame58.lincred = " + LineaArros + " and  maenit.codigoter = '" + codigoter_PAP + "' and copmae.numero= " + numero_pap);
                    stbuilder2.Append(" group by copmae.codigoter, copmae.lincred, copmae.numero, movto.fecha_movto,maenit.nombre, maenit.apellido,");
                    stbuilder2.Append(" saldo.Saldo_inicial,parame58.porpago_int,parame58.forliq,parame58.DIAS_GRACIA,");
                    stbuilder2.Append(" parame58.perpago_int,copmae.FECCIERRE  ");
                    stbuilder2.Append(" order by copmae.codigoter, copmae.lincred, copmae.numero,movto.fecha_movto  ");

                    CargaVarini.ExecuteQueryDataset(stbuilder2.ToString(), Myconect, "LIquidaSaldoPromedio", DsLiqInt1, "tblLiqSalMin");
                    DiaLiq = calcula_Dias_Liquidacion(Fehca_Causacion, fecha_liquidacion);

                    while (Fila < DsLiqInt1.Tables["tblLiqSalMin"].Rows.Count)
                    {
                        DataRow _r = DsLiqInt1.Tables["tblLiqSalMin"].Rows[(int)Fila];
                        CuentaNew = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                        i = 0;
                        if (cuenta != CuentaNew)
                        {
                            SaldoCuenta = 0; SaldoInicial = 0; saldo_cta = 0; SaldoAcu = 0;
                            dias = Fehca_Causacion.Day; DiasMov = Fehca_Causacion.Day;
                            int diaFinal = DateTime.DaysInMonth(Fehca_Causacion.Year, Fehca_Causacion.Month);
                            if (dias == diaFinal)
                            { dias = 1; DiasMov = 1; DateTime fechasig = Fehca_Causacion.AddDays(1); PeriodoMov = Strings.Format(fechasig, "yyyyMM"); FechaMovto = fechasig; DiasPromedio = (int)(fecha_liquidacion - fechasig).TotalDays + 1; }
                            else if (dias != 1)
                            { dias += 1; DiasMov += 1; DiasPromedio = (int)(fecha_liquidacion - Fehca_Causacion).TotalDays; PeriodoMov = Strings.Format(Fehca_Causacion, "yyyyMM"); FechaMovto = Fehca_Causacion; }
                            else
                            { dias = 1; DiasMov = 1; DiasPromedio = (int)(fecha_liquidacion - Fehca_Causacion).TotalDays + 1; PeriodoMov = Strings.Format(Fehca_Causacion, "yyyyMM"); FechaMovto = Fehca_Causacion; }

                            SaldoInicial = (_r["SaldoInicial"] is DBNull) ? 0 : Convert.ToDouble(_r["saldoInicial"]) * -1;
                            int dias1 = Fehca_Causacion.Day;
                            if (dias1 != 1)
                            {
                                verificarEntrada = 1;
                                DateTime fechaFinalCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, dias1);
                                DateTime fechaInicioCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, 1);
                                saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(numero_pap, lincred_Pap, codigoter_PAP, fechaInicioCapital, fechaFinalCapital, Myconect);
                                SaldoInicial += (saldoMoviAnterio * -1);
                            }
                            else if (SaldoInicial == 0)
                            {
                                verificarEntrada = 1;
                                DateTime fechaFinalCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, dias1);
                                DateTime fechaInicioCapital = new DateTime(Fehca_Causacion.Year, Fehca_Causacion.Month, 1);
                                saldoMoviAnterio = calcularSaldoAnterioCausacio_PAP(numero_pap, lincred_Pap, codigoter_PAP, fechaInicioCapital, fechaFinalCapital, Myconect);
                                SaldoInicial += (saldoMoviAnterio * -1);
                            }
                            saldo_cta = SaldoInicial; SaldoCuenta = SaldoInicial;
                            codigoter = Convert.ToString(_r["codigoter"]); lincred = Convert.ToInt32(_r["lincred"]);
                            numero = Convert.ToDouble(_r["numero"]);
                            cuenta = Convert.ToString(_r["codigoter"]) + Convert.ToString(_r["lincred"]) + Convert.ToString(_r["numero"]);
                            Nombre = Convert.ToString(_r["Apellido"]) + " " + Convert.ToString(_r["Nombre"]);
                        }
                        while (Fila < DsLiqInt1.Tables["tblLiqSalMin"].Rows.Count)
                        {
                            DataRow _ri = DsLiqInt1.Tables["tblLiqSalMin"].Rows[(int)Fila];
                            if (!(_ri["fecha_movto"] is DBNull))
                            {
                                SaldoDiario = (_ri["saldiario"] is DBNull) ? 0 : Convert.ToDouble(_ri["saldiario"]);
                            Recorredias:
                                if (PeriodoMov != Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM"))
                                {
                                    while (dias <= DateTime.DaysInMonth(FechaMovto.Year, FechaMovto.Month)) { SaldoAcu += saldo_cta; dias += 1; }
                                    dias = 1;
                                    SigFecMovto = new DateTime(FechaMovto.Year, FechaMovto.Month, 1).AddMonths(1);
                                    if (Strings.Format(SigFecMovto, "yyyyMM") == Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM"))
                                        PeriodoMov = Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "yyyyMM");
                                    else { FechaMovto = SigFecMovto; goto Recorredias; }
                                }
                                DiasMov = Convert.ToInt32(Strings.Format(Convert.ToDateTime(_ri["fecha_movto"]), "dd"));
                                while (dias < DiasMov) { SaldoAcu += saldo_cta; dias += 1; }
                                saldo_cta += (SaldoDiario * -1);
                                SaldoAcu += saldo_cta; DiasMov += 1;
                                if (DiasPromedio == 0) { if (saldo_cta < SaldoCuenta) SaldoCuenta = saldo_cta; }
                                else SaldoCuenta = Math.Round(SaldoAcu / DiasPromedio, 2);
                                FechaMovto = Convert.ToDateTime(_ri["fecha_movto"]); dias += 1;
                            }
                            Fila += 1;
                        }

                        PeriodoMov = Strings.Format(FechaMovto, "yyyyMM");
                        if (PeriodoMov != Strings.Format(fecha_liquidacion, "yyyyMM"))
                        {
                            while (DiasMov <= DateTime.DaysInMonth(FechaMovto.Year, FechaMovto.Month)) { SaldoAcu += saldo_cta; DiasMov += 1; }
                            DiasMov = 1;
                            if (DiasMov <= Convert.ToInt32(Strings.Format(fecha_liquidacion, "dd")))
                                while (DiasMov <= Convert.ToInt32(Strings.Format(fecha_liquidacion, "dd"))) { SaldoAcu += saldo_cta; DiasMov += 1; }
                        }
                        else
                        {
                            if (DiasMov <= Convert.ToInt32(Strings.Format(fecha_liquidacion, "dd")))
                                while (DiasMov <= Convert.ToInt32(Strings.Format(fecha_liquidacion, "dd"))) { SaldoAcu += saldo_cta; DiasMov += 1; }
                        }
                        if (DiasPromedio > 0) SaldoCuenta = Math.Round(SaldoAcu / DiasPromedio, 2);

                        if (SaldoCuenta > 0)
                            DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoCuenta, 0, DiaLiq, Fehca_Causacion, "Y", fecha_liquidacion);
                        else
                            DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter, Nombre, lincred, numero, SaldoCuenta, 0, DiaLiq, Fehca_Causacion, "N", fecha_liquidacion);
                        Fila += 1;
                    }
                    Fila = 0;
                }
                else
                    DsLiqInt.Tables["tbldatosliq"].Rows.Add(codigoter_PAP, nombre_pap, lincred_Pap, numero_pap, 0, 0, 0, Fehca_Causacion, "N", fecha_liquidacion);

                Fila = 0;
                msgbarra.PerformStep();
            }

            if (!Information.IsNumeric(ConseCpte)) ConseCpte = "0";
            msgbarra.Close(); msgbarra.Dispose();

            DsInforme = LiquidacionInteresesCuentasAhorro_PAP(DsLiqInt.Tables["tbldatosliq"], Cpte, Convert.ToDouble(ConseCpte), LineaArros.ToString(), FechaLiquidacion, Usuario, DiasaLiquidar, StTercerizaCruce, Myforma, Myconect, FecFin);
            if (Convert.ToDouble(ConseCpte) == 0)
                ImprimirLiquidacionIntereses(LineaArros.ToString(), FechaLiquidacion, DsInforme, 2, Myforma, Myconect);
        }

        // ----------------------------------------------------------------
        // LiquidacionInteresesCuentasAhorro_PAP (private)
        // ----------------------------------------------------------------
        private DataSet LiquidacionInteresesCuentasAhorro_PAP(DataTable DsDatable, string Cpte, double ConseCpte, string lincred, DateTime FechaLiquidacion, string Usuario, int DiasLiq, string StTercerizaCruce, Form Myforma, OdbcConnection myconnect, DateTime fecha_final_liq)
        {
            double fila = 0; double SalMinimoInt = 0; decimal TasaInt = 0; double VlrMinRetfte = 0; double TasaRetfte = 0; int Forliq = 0; int CptoInteres = 0;
            string CptoRetfte = " "; string CptoIntAhorro = " "; string CptoAho = " "; double Interes = 0; int plazo = 0; double cuota = 0;
            string StCuentaCruce = "999999999999"; bool ActDatos = false; int CptoInteresAhorro = 0; decimal DbTasaInt = 0;
            DateTime FecIngreso = new DateTime(1950, 1, 1); int MesesIngreso = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Contabilizando Liquidacion", Myforma);
            int PerPagInt = 0; int DiaPromedioLiquidacion = 0; decimal tasaCalcular = 0;
            DataSet DatLiquidacion = new DataSet();
            DatLiquidacion.Tables.Add("DatosLiq");
            DatLiquidacion.Tables[0].Columns.Add("lincred", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("num_cuenta", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("codigoter", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("Nombre", typeof(string));
            DatLiquidacion.Tables[0].Columns.Add("base", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("interes", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("retfte", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("Total", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("TasaInt", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("DiasLiq", typeof(double));
            DatLiquidacion.Tables[0].Columns.Add("FechaUltimaliq", typeof(DateTime));
            DatLiquidacion.Tables[0].Columns.Add("ValidaLiq", typeof(string));

            msgbarra.ValorMinimoMaximo(0, DsDatable.Rows.Count);
            msgbarra.Show();

            string _u = " "; string _sl = SalMinimoInt.ToString(); string _ti = TasaInt.ToString();
            string _vm = VlrMinRetfte.ToString(); string _tr = TasaRetfte.ToString();
            string _fl = Forliq.ToString(); string _ci = CptoInteres.ToString(); string _pp = PerPagInt.ToString();

            // BuscaLineaAhorro(lincred, myconnect, Navega.Ninguno, // ERROR: CS1501
                // ref _u, ref _u, ref _pp, ref _sl, ref _u, ref _u, ref _ti, ref _vm, ref _tr, ref _u, ref _fl, // ERROR: CS1501
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS1501
                // ref _u, ref _ci); // ERROR: CS1501
            if (Information.IsNumeric(_sl)) SalMinimoInt = Convert.ToDouble(_sl);
            if (Information.IsNumeric(_ti)) TasaInt = Convert.ToDecimal(_ti);
            if (Information.IsNumeric(_vm)) VlrMinRetfte = Convert.ToDouble(_vm);
            if (Information.IsNumeric(_tr)) TasaRetfte = Convert.ToDouble(_tr);
            if (Information.IsNumeric(_fl)) Forliq = Convert.ToInt32(_fl);
            if (Information.IsNumeric(_ci)) CptoInteres = Convert.ToInt32(_ci);
            if (Information.IsNumeric(_pp)) PerPagInt = Convert.ToInt32(_pp);

            // paramsys.BuscarCompania(Var.varini.SptCodEmpr, myconnect, // ERROR: CS1061
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS1061
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref CptoAho, ref _u, ref CptoRetfte, ref _u, ref _u, ref _u, // ERROR: CS1061
                // ref _u, ref _u, ref _u, ref CptoIntAhorro); // ERROR: CS1061

            if (Cpte.Trim() != "")
                // msgcnt_cnt.BuscaComprobante(Cpte, 0, false, myconnect, // ERROR: CS1620
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS1620
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref StCuentaCruce); // ERROR: CS1620

            CptoInteresAhorro = (CptoInteres == 9999) ? Convert.ToInt32(lincred) : CptoInteres;

            while (fila < DsDatable.Rows.Count)
            {
                DataRow _r = DsDatable.Rows[(int)fila];
                ActDatos = (Cpte.Trim() != "");
                // paramcop.BuscaAsociado(Convert.ToString(_r["codigoter"]), myconnect, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref FecIngreso); // ERROR: CS7036
                MesesIngreso = (FechaLiquidacion.Year - FecIngreso.Year) * 12 + (FechaLiquidacion.Month - FecIngreso.Month);
                string _cuota = cuota.ToString(); string _plazo = plazo.ToString();
                // msgcop_car.BuscaObligacion(Convert.ToString(_r["codigoter"]), lincred, Convert.ToString(_r["numero"]), myconnect, // ERROR: CS7036
                    // ref _u, ref _u, ref _u, ref _u, ref _cuota, ref _u, ref _u, ref _u, ref _plazo); // ERROR: CS7036
                if (Information.IsNumeric(_cuota)) cuota = Convert.ToDouble(_cuota);
                if (Information.IsNumeric(_plazo)) plazo = Convert.ToInt32(_plazo);
                // DbTasaInt = paramcop.BuscarTasasporPlazos(Convert.ToInt32(lincred), (plazo / 30), cuota, MesesIngreso, myconnect); // ERROR: CS1061
                if (DbTasaInt <= 0) DbTasaInt = TasaInt;

                switch (PerPagInt.ToString())
                {
                    case "6": DiaPromedioLiquidacion = 30; break; case "5": DiaPromedioLiquidacion = 1; break;
                    case "4": DiaPromedioLiquidacion = 90; break; case "3": DiaPromedioLiquidacion = 120; break;
                    case "2": DiaPromedioLiquidacion = 180; break; case "1": DiaPromedioLiquidacion = 360; break;
                    default: DiaPromedioLiquidacion = 30; break;
                }

                switch (Forliq)
                {
                    // case 1: tasaCalcular = paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaNominalAnual, DbTasaInt, Convert.ToDouble(_r["Dias_PAP"])); break; // ERROR: CS1503
                    // case 2: tasaCalcular = paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaEfectivaAnual, DbTasaInt, Convert.ToDouble(_r["Dias_PAP"])); break; // ERROR: CS1503
                }

                if (Convert.ToString(_r["ValidaLiquida"]) == "Y")
                {
                    double _il = Convert.ToDouble(_r["INTERES"]);
                    LiquidaInteresMes(Forliq, tasaCalcular, Convert.ToDouble(_r["Base"]), SalMinimoInt, VlrMinRetfte, (decimal)TasaRetfte,
                        Convert.ToString(_r["codigoter"]), Convert.ToString(_r["NOMBRE"]), lincred, Convert.ToString(_r["numero"]),
                        Cpte, ConseCpte.ToString(), CptoRetfte, CptoInteresAhorro.ToString(), CptoIntAhorro, ActDatos, Usuario,
                        FechaLiquidacion, DatLiquidacion, myconnect, _il, Convert.ToInt32(_r["Dias_PAP"]),
                        new DateTime(1950,1,1), new DateTime(1950,1,1), StTercerizaCruce, StCuentaCruce,
                        Convert.ToDateTime(_r["FechaLiquidacion"]), Convert.ToDateTime(_r["FechaUltLiquid"]));
                }
                else
                    DatLiquidacion.Tables["DatosLiq"].Rows.Add(lincred, Convert.ToString(_r["numero"]), Convert.ToString(_r["codigoter"]), Convert.ToString(_r["NOMBRE"]), Convert.ToDouble(_r["Base"]), Convert.ToDouble(_r["INTERES"]), 0, 0, tasaCalcular, Convert.ToDouble(_r["Dias_PAP"]), Convert.ToDateTime(_r["FechaUltLiquid"]), "N");

                msgbarra.PerformStep();
                fila += 1;
            }
            msgbarra.Close(); msgbarra.Dispose();
            return DatLiquidacion;
        }

        // ----------------------------------------------------------------
        // calcula_Dias_Liquidacion
        // ----------------------------------------------------------------
        public int calcula_Dias_Liquidacion(DateTime Fehca_Causacion, DateTime fecha_liquidacion)
        {
            int DiaLiq = 0;
            int AnoIni = Convert.ToInt32(Strings.Format(Fehca_Causacion, "yyyy"));
            int MesIni = Convert.ToInt32(Strings.Format(Fehca_Causacion, "MM"));
            int DiaIni = Convert.ToInt32(Strings.Format(Fehca_Causacion, "dd"));
            int AnoFin = Convert.ToInt32(Strings.Format(fecha_liquidacion, "yyyy"));
            int MesFin = Convert.ToInt32(Strings.Format(fecha_liquidacion, "MM"));
            int DiaFin = Convert.ToInt32(Strings.Format(fecha_liquidacion, "dd"));

            if (DiaFin > 30) DiaFin = 30;
            if (DiaIni > 30) DiaIni = 30;
            if (MesFin == 2 && DiaFin >= 28) DiaFin = 30;
            if (MesIni == 2 && DiaIni >= 28) DiaIni = 30;

            DiaLiq = (((AnoFin - AnoIni) * 360) + ((MesFin - MesIni) * 30) + (DiaFin - DiaIni));
            return DiaLiq;
        }

        // ----------------------------------------------------------------
        // calcularSaldoAnterioCausacio_PAP
        // ----------------------------------------------------------------
        public double calcularSaldoAnterioCausacio_PAP(double num_cuenta, int lincred, string codigoter, DateTime FechaIni, DateTime FechaFin, OdbcConnection myconnect)
        {
            string saldoDiario = " ";
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();

            stbuilder.Append(" SELECT copmae.codigoter,copmae.lincred,copmae.numero, (sum(movto.VLR_DEBITO) - sum(movto.VLR_CREDITO)) AS campo1 ");
            stbuilder.Append(" from cop_movimto movto ");
            stbuilder.Append(" RIGHT outer join cop_docmto  doc on movto.compronte=doc.compronte ");
            stbuilder.Append(" and movto.numero_domto=doc.numero_domto  and doc.ANULADO<>'Y'  and  movto.fecha_Movto between '" + Strings.Format(FechaIni, Var.varini.PstForFec) + "' and '" + Strings.Format(FechaFin, Var.varini.PstForFec) + "' ");
            stbuilder.Append(" RIGHT outer join sys_compro02 compro on doc.COMPRONTE = compro.CODIGO  and compro.RESTRI_TESORERIA<>'Y' ");
            stbuilder.Append(" RIGHT outer join cop_codmov codmov on movto.COD_MOVTO=codmov.cod_movto and codmov.TIPO_MOVTO not in ('8','9')  ");
            stbuilder.Append(" RIGHT outer join  cop_maecar  copmae  on movto.CODIGOTER =copmae.CODIGOTER and movto.LINCRED = copmae.LINCRED and movto.NUMERO = copmae.NUMERO");
            stbuilder.Append(" inner join   sys_maenit maenit  on maenit.codigoter = copmae.codigoter  ");
            stbuilder.Append(" inner join cop_ahorro58 parame58 on   parame58.lincred = copmae.lincred ");
            stbuilder.Append(" inner join cop_concar12 parame12 on  copmae.lincred = parame12.lincred  ");
            stbuilder.Append(" left join cop_salmaecar saldo on saldo.codigoter = copmae.codigoter  and saldo.lincred = copmae.lincred ");
            stbuilder.Append(" and saldo.numero = copmae.numero and saldo.periodo = '" + Strings.Format(FechaIni, "yyyyMM") + "' ");
            stbuilder.Append(" where maenit.estado<>'T' and parame58.lincred = " + lincred + " and  maenit.codigoter ='" + codigoter + "'");
            stbuilder.Append(" and copmae.lincred=" + lincred + " and copmae.numero= " + num_cuenta);
            stbuilder.Append(" group by copmae.codigoter,copmae.lincred,copmae.numero ");

            CargaVarini.ExecuteQueryconec(stbuilder.ToString(), myconnect, "calcularSaldoAnterioCausacio_PAP", ref saldoDiario);
            if (Information.IsDBNull(saldoDiario)) return 0;
            else { if (Information.IsNumeric(saldoDiario)) return Convert.ToDouble(saldoDiario); else return 0; }
        }

        // ----------------------------------------------------------------
        // Calcula_Resta_FechaVemto
        // ----------------------------------------------------------------
        public DateTime Calcula_Resta_FechaVemto(int plazo, DateTime fechaDeposito)
        {
            DateTime fecha = fechaDeposito.AddDays(1);
            double meses = (double)plazo / 30;
            return fecha.AddMonths((int)(meses * -1));
        }

        // ----------------------------------------------------------------
        // CargaGrillaRenovacion
        // ----------------------------------------------------------------
        public DataSet CargaGrillaRenovacion(DateTime FechaIncial, DateTime FechaFinal, int Periodo, int lincred, int plazo, OdbcConnection myconect)
        {
            DataSet DatCdats = new DataSet();
            string _sql = " select maeahor.num_cuenta,{fn concat({fn concat(maenit.apellido , ' ')},maenit.nombre)} as Nombre,"
                + "  copmae.FECCIERRE,maeahor.FECVEMTO,copmae.Plazo,salmae.CUOTA,salmae.CICLOD,salmae.PERIODD, salmae.CLADES,maeahor.lincred ,maeahor.codigoter ,maeahor.fec_crea  "
                + "  from dep_maeahor maeahor inner join sys_maenit maenit on maeahor.codigoter = maenit.codigoter "
                + " inner join cop_salmaecar salmae on maeahor.codigoter = salmae.codigoter and maeahor.lincred = salmae.lincred and maeahor.num_cuenta = salmae.numero and salmae.periodo = " + Periodo
                + " inner join cop_maecar copmae on maeahor.codigoter = copmae.codigoter  and maeahor.lincred = salmae.LINCRED  and  maeahor.num_cuenta = copmae.NUMERO  "
                + " inner join cop_ahorro58 parame58 on  copmae.lincred =  parame58.lincred  "
                + " where maeahor.lincred = " + lincred + "  and maeahor.FECVEMTO between '" + Strings.Format(FechaIncial, Var.varini.PstForFec) + "' and '" + Strings.Format(FechaFinal, Var.varini.PstForFec) + "'"
                + " and maeahor.estado='0' and salmae.saldo <> 0 and copmae.plazo=" + plazo + "  and parame58.manejaFormaPAP = 'Y' ";
            CargaVarini.ExecuteQueryDataset(_sql, myconect, "CargaGrillaRenovacion", DatCdats, "TblRenovacion");
            return DatCdats;
        }

        // ----------------------------------------------------------------
        // validaFechaVencimiento
        // ----------------------------------------------------------------
        public bool validaFechaVencimiento(double numcuentas, DateTime fechaMovimiento, OdbcConnection myconect, ref DateTime fecha_Vecimiento)
        {
            DateTime fechaVencimiento = new DateTime(1950, 1, 1);
            string _u = " "; int _lincred = 0; string _codigoter = " "; double _numero = 0;
            string _fv = fechaVencimiento.ToString();
            // Call BuscarCuentaAhorro with the fechaVencimiento ref at position 47
            // BuscarCuentaAhorro(numcuentas, myconect, Navega.Ninguno, // ERROR: CS7036
                // ref _u, ref _lincred, ref _codigoter, ref _numero, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                // ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, // ERROR: CS7036
                // ref _u, ref _u, ref _fv); // ERROR: CS7036
            if (Information.IsDate(_fv)) fechaVencimiento = Convert.ToDateTime(_fv);
            fecha_Vecimiento = fechaVencimiento;
            if (Convert.ToDateTime(Strings.Format(fechaVencimiento, Var.varini.PstForFec)) < Convert.ToDateTime(Strings.Format(fechaMovimiento, Var.varini.PstForFec)))
                return true;
            else return false;
        }
    }
}
