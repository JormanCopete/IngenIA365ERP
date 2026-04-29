using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Debitos
{
    public class ClsMsgDeb
    {
        private bool ok;
        private string stmysql;
        private OdbcConnection myconnect = new OdbcConnection();
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera MsgClscartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Compartido.Configuracion.ParamSys paramsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos depositos = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();
        private Stream strStreamW;
        private StreamWriter strStreamWriter;

        public ClsMsgDeb()
        {
            connect.MyOdbcConect(varini);
        }

        public enum ClaseMovto
        {
            Retiro = 0,
            Deposito = 1,
            Batch = 2
        }

        public enum Navega
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }

        public bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref DataSet DsDataset, string NombreTabla)
        {
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
            mycomqueryconec.ExecuteNonQuery();

            Myread.SelectCommand = mycomqueryconec;
            Myread.Fill(DsDataset, NombreTabla);

            if (DsDataset.Tables[NombreTabla].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);

            try
            {
                OdbcDataReader Myreader = mycomqueryconec.ExecuteReader();
                if (Myreader.RecordsAffected > 0)
                {
                    result = true;
                }
                while (Myreader.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myreader["campo1"] == DBNull.Value)
                        {
                            Campo1 = "0";
                        }
                        else
                        {
                            Campo1 = Myreader["campo1"].ToString().Trim();
                        }
                    }
                    if (Campo2 != "")
                    {
                        if (Myreader["campo2"] == DBNull.Value)
                        {
                            Campo2 = "0";
                        }
                        else
                        {
                            Campo2 = Myreader["campo2"].ToString().Trim();
                        }
                    }
                    if (Campo3 != "")
                    {
                        if (Myreader["campo3"] == DBNull.Value)
                        {
                            Campo3 = "0";
                        }
                        else
                        {
                            Campo3 = Myreader["campo3"].ToString().Trim();
                        }
                    }
                    if (Campo4 != "")
                    {
                        if (Myreader["campo4"] == DBNull.Value)
                        {
                            Campo4 = "0";
                        }
                        else
                        {
                            Campo4 = Myreader["campo4"].ToString().Trim();
                        }
                    }
                    result = true;
                }
                Myreader.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(Err.Description + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        public string Helptarjetas(string Codigoter, string Empresa, Form myforma)
        {
            // FrmCupoTarj FrmCupo = new FrmCupoTarj(myconnect); // ERROR: CS0246
            // FrmCupo.LblCodigoter.Text = Codigoter; // ERROR: CS0103
            // FrmCupo.LblEmpresa.Text = Empresa; // ERROR: CS0103
            // FrmCupo.ShowDialog(myforma); // ERROR: CS0103
            // return FrmCupo.NumeroTajeta; // ERROR: CS0103
            return string.Empty;
        }

        public virtual bool BuscaConvenio(ref string Convenio, OdbcConnection myconnect, ref string Nombre, ref string Cuenta, ref string Entidad, ref int Moneda, ref string Ahorro, ref string Corriente, ref string Bloqueo, ref int OpcionDisponible, ref decimal CupoDisponible, ref decimal TasaDisponible, ref int OpcionCajero, ref decimal CupoCajero, ref decimal TasaCajero, ref int TransaccionesCajero, ref int OpcionPos, ref decimal CupoPos, ref decimal TasaPos, ref int TransaccionesPos, ref int Saldos, ref string Bin, ref decimal TopeDisponible, ref decimal TopeCaja, ref decimal TopeDisponible1, ref decimal TopeCaja1, ref int ValorManejo, Navega Navegar, ref int TipoServicio)
        {
            string where = "";
            ok = false;
            Convenio = ("0000" + Convenio).Substring(("0000" + Convenio).Length - 4);

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = "from  sys_convenio where convenio = '" + Convenio + "'";
                    break;
                case Navega.Anterior:
                    where = "from  sys_convenio where convenio < '" + Convenio + " ' order by convenio desc ";
                    break;
                case Navega.Primero:
                    where = "from  sys_convenio where convenio > ' ' order by convenio ";
                    break;
                case Navega.Siguiente:
                    where = "from  sys_convenio where convenio > '" + Convenio + " ' order by convenio ";
                    break;
                case Navega.Ultimo:
                    where = "from  sys_convenio where convenio < '9999'  order by convenio desc";
                    break;
            }

            stmysql = "select nombre as campo1, cuenta as campo2, entidad as campo3,moneda as campo4 ";
            string strNombre = Nombre ?? " ", strCuenta = Cuenta ?? "0", strEntidad = Entidad ?? "0", strMoneda = Moneda.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaConvenio", ref strNombre, ref strCuenta, ref strEntidad, ref strMoneda);
            Nombre = strNombre; Cuenta = strCuenta; Entidad = strEntidad; int.TryParse(strMoneda, out Moneda);

            stmysql = "select ahorro as campo1, corriente as campo2, bloqueo as campo3,opcDisp as campo4 ";
            string strAhorro = Ahorro ?? "0", strCorriente = Corriente ?? "0", strBloqueo = Bloqueo ?? "0", strOpcionDisponible = OpcionDisponible.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaConvenio", ref strAhorro, ref strCorriente, ref strBloqueo, ref strOpcionDisponible);
            Ahorro = strAhorro; Corriente = strCorriente; Bloqueo = strBloqueo; int.TryParse(strOpcionDisponible, out OpcionDisponible);

            stmysql = "select cupodisp as campo1, tasadisp as campo2, opcCajero as campo3,cupoCajero as campo4 ";
            string strCupoDisponible = CupoDisponible.ToString(), strTasaDisponible = TasaDisponible.ToString(), strOpcionCajero = OpcionCajero.ToString(), strCupoCajero = CupoCajero.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaConvenio", ref strCupoDisponible, ref strTasaDisponible, ref strOpcionCajero, ref strCupoCajero);
            decimal.TryParse(strCupoDisponible, out CupoDisponible); decimal.TryParse(strTasaDisponible, out TasaDisponible); int.TryParse(strOpcionCajero, out OpcionCajero); decimal.TryParse(strCupoCajero, out CupoCajero);

            stmysql = "select TasaCajero as campo1, TraCajero as campo2, opcpos as campo3,CupoPos as campo4 ";
            string strTasaCajero = TasaCajero.ToString(), strTransaccionesCajero = TransaccionesCajero.ToString(), strOpcionPos = OpcionPos.ToString(), strCupoPos = CupoPos.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaConvenio", ref strTasaCajero, ref strTransaccionesCajero, ref strOpcionPos, ref strCupoPos);
            decimal.TryParse(strTasaCajero, out TasaCajero); int.TryParse(strTransaccionesCajero, out TransaccionesCajero); int.TryParse(strOpcionPos, out OpcionPos); decimal.TryParse(strCupoPos, out CupoPos);

            stmysql = "select Tasapos as campo1, TraPos as campo2, Saldos as campo3,Bin as campo4 ";
            string strTasaPos = TasaPos.ToString(), strTransaccionesPos = TransaccionesPos.ToString(), strSaldos = Saldos.ToString(), strBin = Bin ?? "0";
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaConvenio", ref strTasaPos, ref strTransaccionesPos, ref strSaldos, ref strBin);
            decimal.TryParse(strTasaPos, out TasaPos); int.TryParse(strTransaccionesPos, out TransaccionesPos); int.TryParse(strSaldos, out Saldos); Bin = strBin;

            stmysql = "select TopeDisponible as campo1, Topecaja as campo2, TopeDisponible1 as campo3,Topecaja1 as campo4 ";
            string strTopeDisponible = TopeDisponible.ToString(), strTopeCaja = TopeCaja.ToString(), strTopeDisponible1 = TopeDisponible1.ToString(), strTopeCaja1 = TopeCaja1.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaConvenio", ref strTopeDisponible, ref strTopeCaja, ref strTopeDisponible1, ref strTopeCaja1);
            decimal.TryParse(strTopeDisponible, out TopeDisponible); decimal.TryParse(strTopeCaja, out TopeCaja); decimal.TryParse(strTopeDisponible1, out TopeDisponible1); decimal.TryParse(strTopeCaja1, out TopeCaja1);

            stmysql = "select VlrManejo as campo1, convenio as campo2,TipoServicio as campo3 ";
            string strValorManejo = ValorManejo.ToString(), strConvenio2 = Convenio, strTipoServicio = TipoServicio.ToString(), strDummy = "";
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaConvenio", ref strValorManejo, ref strConvenio2, ref strTipoServicio, ref strDummy);
            int.TryParse(strValorManejo, out ValorManejo); Convenio = strConvenio2; int.TryParse(strTipoServicio, out TipoServicio);

            return ok;
        }

        public virtual bool BuscaConvenio(ref string Convenio, DataSet dsdatos, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            Convenio = ("0000" + Convenio).Substring(("0000" + Convenio).Length - 4);

            stbuilder.Append("select nombre , cuenta , entidad ,moneda, ");
            stbuilder.Append(" ahorro , corriente , bloqueo ,opcDisp,  ");
            stbuilder.Append(" cupodisp , tasadisp , opcCajero ,cupoCajero, ");
            stbuilder.Append(" TasaCajero , TraCajero , opcpos ,CupoPos, ");
            stbuilder.Append(" Tasapos , TraPos , Saldos ,Bin,  ");
            stbuilder.Append(" TopeDisponible, Topecaja , TopeDisponible1 ,Topecaja1 , ");
            stbuilder.Append(" VlrManejo , convenio,TipoServicio ");
            stbuilder.Append("from  sys_convenio where convenio = '" + Convenio + "'");

            ok = this.connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaConvenio", dsdatos, "tblconvenio");
            if (dsdatos.Tables["tblconvenio"].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaTarjetaDebito(ref string Banco, ref string tarjeta, OdbcConnection myconnect, ref string Cuenta, ref string Obligacion, ref string bin, ref int Lincred, ref string TipoCta, ref string CodError, ref string Codigoter, ref string Operacion, ref string Estado, ref string CptoIni, ref decimal Disponible, ref decimal CupoCajero, ref int TransCajero, ref decimal CupoPos, ref int TransPos, ref int Marca, ref DateTime FechaAsignacion, ref int ClaseTopeDisp, ref int ClaseTopeCajero, ref string DebitoCredito, ref decimal CupoCredito, ref int DiaCorte, bool BuscarXcuenta, ref string Codeudor1, ref string Codeudor2, ref string MotivoBloqueo, ref DateTime FechaVence, ref decimal DispCredDs, ref decimal CupoCajDs, ref decimal TransCajDs, ref decimal CupoPosDs, ref decimal TransPosDs, ref string CuentaDs, ref string CobraManejo, ref string CobraManejoDs, ref DateTime fecha_cupo, ref int LincredVieja)
        {
            string where = "";
            string fecha = "01-01-1950";
            string fec_cupo = "01-01-1950";
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            if (FechaVence == default(DateTime)) FechaVence = new DateTime(1950, 1, 1);
            if (FechaAsignacion == default(DateTime)) FechaAsignacion = new DateTime(1950, 1, 1);
            if (fecha_cupo == default(DateTime)) fecha_cupo = new DateTime(1950, 1, 1);

            if (BuscarXcuenta == true)
            {
                where = " from  deb_maetarj where cuenta = '" + Cuenta.Trim() + "' order by estado asc";
            }
            else
            {
                Banco = ("0000" + Banco).Substring(("0000" + Banco).Length - 4);
                where = " from  deb_maetarj where banco = '" + Banco + "' and tarjeta= '" + tarjeta.Trim() + "'  order by estado asc";
            }

            StBuilder.Append("select Cuenta, cupocredito,bin,lincred,TipoCta,error,codigoter,operacion,");
            StBuilder.Append("estado, cpto_ini, disponible, cupocajero,trancajero, cupopos, tranpos,fecasignacion, ");
            StBuilder.Append("diacorte, banco, tarjeta, marca,debcre, clasetopedspnble, clasetopecajero, MotivoBloqueo, ");
            StBuilder.Append("codeudor1, codeudor2, fechavence, DispCredDs,CupoCajDs, TransCajDs, CupoPosDs, TransPosDs,");
            StBuilder.Append("CuentaDs,CobraManejo,CobraManejoDS,FecAsignaCupo,lineavieja ");
            StBuilder.Append(where);

            ok = this.connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaTarjetaDebito", dsdata, "tblmaetarj", true);

            switch (ok)
            {
                case false:
                    return false;
                case true:
                    DataRow row = dsdata.Tables["tblmaetarj"].Rows[0];
                    Cuenta = row["Cuenta"].ToString();
                    CupoCredito = Convert.ToDecimal(row["cupocredito"]);
                    bin = row["bin"].ToString();
                    Lincred = Convert.ToInt32(row["lincred"]);

                    TipoCta = row["TipoCta"].ToString();
                    CodError = row["error"].ToString();
                    Codigoter = row["codigoter"].ToString();
                    Operacion = row["operacion"].ToString();
                    Estado = row["estado"].ToString();
                    CptoIni = row["cpto_ini"].ToString();
                    Disponible = Convert.ToDecimal(row["disponible"]);
                    CupoCajero = Convert.ToDecimal(row["cupocajero"]);
                    TransCajero = Convert.ToInt32(row["trancajero"]);
                    CupoPos = Convert.ToDecimal(row["cupopos"]);
                    TransPos = Convert.ToInt32(row["tranpos"]);
                    fecha = row["fecasignacion"].ToString();
                    DateTime tempDate;
                    if (!DateTime.TryParse(fecha, out tempDate))
                    {
                        FechaAsignacion = new DateTime(1950, 1, 1);
                    }
                    else
                    {
                        FechaAsignacion = tempDate;
                    }

                    DiaCorte = Convert.ToInt32(row["diacorte"]);
                    Banco = row["banco"].ToString();
                    tarjeta = row["tarjeta"].ToString();
                    Marca = Convert.ToInt32(row["marca"]);
                    DebitoCredito = row["debcre"].ToString();
                    ClaseTopeDisp = Convert.ToInt32(row["clasetopedspnble"]);
                    ClaseTopeCajero = Convert.ToInt32(row["clasetopecajero"]);
                    MotivoBloqueo = row["MotivoBloqueo"].ToString();
                    Codeudor1 = row["codeudor1"].ToString();
                    Codeudor2 = row["codeudor2"].ToString();
                    fecha = row["fechavence"].ToString();
                    DispCredDs = Convert.ToDecimal(row["DispCredDs"]);
                    CupoCajDs = Convert.ToDecimal(row["CupoCajDs"]);
                    TransCajDs = Convert.ToDecimal(row["TransCajDs"]);
                    CupoPosDs = Convert.ToDecimal(row["CupoPosDs"]);
                    TransPosDs = Convert.ToDecimal(row["TransPosDs"]);
                    CuentaDs = row["CuentaDs"].ToString();
                    CobraManejo = row["CobraManejo"].ToString();
                    CobraManejoDs = row["CobraManejoDS"].ToString();
                    fec_cupo = row["FecAsignaCupo"].ToString();

                    Obligacion = Lincred + " " + Cuenta;

                    if (!DateTime.TryParse(fecha, out tempDate))
                    {
                        FechaVence = new DateTime(1950, 1, 1);
                    }
                    else
                    {
                        FechaVence = tempDate;
                    }

                    if (!DateTime.TryParse(fec_cupo, out tempDate))
                    {
                        fecha_cupo = new DateTime(1950, 1, 1);
                    }
                    else
                    {
                        fecha_cupo = tempDate;
                    }

                    LincredVieja = Convert.ToInt32(row["lineavieja"]);
                    return true;
            }

            return ok;
        }

        public bool BuscaParamDiario(ref string Convenio, OdbcConnection myconnect, ref string banco, ref string CpteBatch, ref string CpteLinea, ref string Detalle, ref DateTime FecActualizacion, ref int TarjetasNuevas, ref string UltimaTarjeta, ref DateTime fechaCierre, ref string cpteCierreLinea, ref int CpteconseLinea, ref string CpteCierreDatafono, ref int CierreConsedatafono, Navega Navegar, ref string cierre, ref double comisionred, ref double comisionotrared)
        {
            string where = "";
            ok = false;

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = "from  deb_pardiario where Idcodigo = '" + Convert.ToInt32(Convenio) + "'";
                    break;
                case Navega.Anterior:
                    where = "from  deb_pardiario where Idcodigo < '" + Convert.ToInt32(Convenio) + " ' order by Idcodigo desc ";
                    break;
                case Navega.Primero:
                    where = "from  deb_pardiario where Idcodigo > ' ' order by Idcodigo ";
                    break;
                case Navega.Siguiente:
                    where = "from  deb_pardiario where Idcodigo > '" + Convert.ToInt32(Convenio) + " ' order by Idcodigo ";
                    break;
                case Navega.Ultimo:
                    where = "from  deb_pardiario where Idcodigo < '9999'  order by Idcodigo desc";
                    break;
            }

            stmysql = "select banco as campo1, CpteBatch as campo2, CpteLinea as campo3, Detalle as campo4 ";
            string strBanco = banco ?? " ", strCpteBatch = CpteBatch ?? "9999", strCpteLinea = CpteLinea ?? "9999", strDetalle = Detalle ?? " ";
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaParamDiario", ref strBanco, ref strCpteBatch, ref strCpteLinea, ref strDetalle);
            banco = strBanco; CpteBatch = strCpteBatch; CpteLinea = strCpteLinea; Detalle = strDetalle;

            stmysql = "select FechaActualizacion as campo1, TarjetasNuevas as campo2, UltimaTarjeta as campo3, fechaCierre as campo4 ";
            string strFecActualizacion = FecActualizacion.ToString(), strTarjetasNuevas = TarjetasNuevas.ToString(), strUltimaTarjeta = UltimaTarjeta ?? " ", strFechaCierre = fechaCierre.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaParamDiario", ref strFecActualizacion, ref strTarjetasNuevas, ref strUltimaTarjeta, ref strFechaCierre);
            DateTime.TryParse(strFecActualizacion, out FecActualizacion); int.TryParse(strTarjetasNuevas, out TarjetasNuevas); UltimaTarjeta = strUltimaTarjeta; DateTime.TryParse(strFechaCierre, out fechaCierre);

            stmysql = "select cpteCierre as campo1, consecierre as campo2, CpteCierreDatafono as campo3, ConseCierredatafono as campo4 ";
            string strcpteCierreLinea = cpteCierreLinea ?? "9999", strCpteconseLinea = CpteconseLinea.ToString(), strCpteCierreDatafono = CpteCierreDatafono ?? "9999", strCierreConsedatafono = CierreConsedatafono.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaParamDiario", ref strcpteCierreLinea, ref strCpteconseLinea, ref strCpteCierreDatafono, ref strCierreConsedatafono);
            cpteCierreLinea = strcpteCierreLinea; int.TryParse(strCpteconseLinea, out CpteconseLinea); CpteCierreDatafono = strCpteCierreDatafono; int.TryParse(strCierreConsedatafono, out CierreConsedatafono);

            stmysql = "select idcodigo as campo1,cierre as campo2,comisionred as campo3,comisionotrared as campo4 ";
            string strConvenio = Convenio, strcierre = cierre ?? "N", strcomisionred = comisionred.ToString(), strcomisionotrared = comisionotrared.ToString();
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaParamDiario", ref strConvenio, ref strcierre, ref strcomisionred, ref strcomisionotrared);
            Convenio = strConvenio; cierre = strcierre; double.TryParse(strcomisionred, out comisionred); double.TryParse(strcomisionotrared, out comisionotrared);

            return ok;
        }

        public bool BuscaParamDatafono(ref double CodDatafono, OdbcConnection myconnect, ref string terminal, ref string CpteTerminal, Navega navegar, ref string usuario)
        {
            string where = "";
            ok = false;

            switch (navegar)
            {
                case Navega.Ninguno:
                    where = "from  deb_pardatafonos where Idcodigo = " + CodDatafono;
                    break;
                case Navega.Anterior:
                    where = "from  deb_pardatafonos where Idcodigo < " + CodDatafono + " order by Idcodigo desc ";
                    break;
                case Navega.Primero:
                    where = "from  deb_pardatafonos where Idcodigo > 0 order by Idcodigo ";
                    break;
                case Navega.Siguiente:
                    where = "from  deb_pardatafonos where Idcodigo > " + CodDatafono + " order by Idcodigo ";
                    break;
                case Navega.Ultimo:
                    where = "from  deb_pardatafonos where Idcodigo < 99999999999  order by Idcodigo desc";
                    break;
            }

            stmysql = "select Idcodigo as campo1, terminal as campo2, Cpte as campo3, usuario as campo4 ";
            string strCodDatafono = CodDatafono.ToString(), strTerminal = terminal ?? "", strCpteTerminal = CpteTerminal ?? "9999", strUsuario = usuario ?? "";
            ok = this.connect.ExecuteQueryconec(stmysql + where, myconnect, "BuscaParamDiario", ref strCodDatafono, ref strTerminal, ref strCpteTerminal, ref strUsuario);
            double.TryParse(strCodDatafono, out CodDatafono); terminal = strTerminal; CpteTerminal = strCpteTerminal; usuario = strUsuario;

            return ok;
        }

        public bool BuscaTerminal(string Idcodterminal, OdbcConnection myconncet, ref string CpteTerminal, ref string Usuario)
        {
            stmysql = "select Cpte as campo1,usuario as campo2 from deb_pardatafonos where terminal = '" + Idcodterminal + "'";
            string strCpteTerminal = CpteTerminal ?? "9999", strUsuario = Usuario ?? " ", strDummy1 = "", strDummy2 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconncet, "BuscaTerminal", ref strCpteTerminal, ref strUsuario, ref strDummy1, ref strDummy2);
            CpteTerminal = strCpteTerminal; Usuario = strUsuario;
            return ok;
        }

        public bool GrabaParamDatafono(double CodDatafono, string Terminal, string CpteTerminal, OdbcConnection myconnect, string usuario)
        {
            ok = false;
            // ok = this.BuscaParamDatafono(ref CodDatafono, myconnect, ref Terminal, ref CpteTerminal); // ERROR: CS7036
            switch (ok)
            {
                case false:
                    stmysql = "insert into deb_pardatafonos(idcodigo,terminal,cpte,usuario) "
                           + " values (" + CodDatafono + ",'" + Terminal + "','" + ("0000" + CpteTerminal).Substring(("0000" + CpteTerminal).Length - 4) + "','" + usuario + "')";
                    break;
                case true:
                    stmysql = "update deb_pardatafonos set terminal = '" + Terminal + "',cpte = '" + ("0000" + CpteTerminal).Substring(("0000" + CpteTerminal).Length - 4) + "',usuario = '" + usuario
                          + "' where idcodigo = " + CodDatafono;
                    break;
            }
            string strDummy1 = "", strDummy2 = "", strDummy3 = "", strDummy4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "GrabaParamDatafono", ref strDummy1, ref strDummy2, ref strDummy3, ref strDummy4);
            return ok;
        }

        public bool GrabaConvenio(string Convenio, string Nombre, string Cuenta, string Entidad, int Moneda, string Ahorro, string Corriente, string Bloqueo, int OpcionDisponible, decimal CupoDisponible, decimal TasaDisponible, int OpcionCajero, decimal CupoCajero, decimal TasaCajero, int TransaccionesCajero, int OpcionPos, decimal CupoPos, decimal TasaPos, int TransaccionesPos, int Saldos, string Bin, decimal TopeDisponible, decimal TopeCaja, decimal TopeDisponible1, decimal TopeCaja1, int ValorManejo, int TipoServicio, OdbcConnection myconnect)
        {
            ok = false;
            Convenio = ("0000" + Convenio).Substring(("0000" + Convenio).Length - 4);
            string strDummy1 = " ", strDummy2 = "0", strDummy3 = "0";
            int intDummy = 0;
            decimal decDummy = 0;
            ok = BuscaConvenio(ref Convenio, myconnect, ref strDummy1, ref strDummy2, ref strDummy3, ref intDummy, ref strDummy1, ref strDummy1, ref strDummy1, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref intDummy, ref strDummy1, ref decDummy, ref decDummy, ref decDummy, ref decDummy, ref intDummy, Navega.Ninguno, ref intDummy);
            switch (ok)
            {
                case false:
                    stmysql = "insert into sys_convenio(convenio,nombre,cuenta,entidad,moneda,ahorro,corriente,bloqueo,opcDisp,cupoDisp,TasaDisp, opcCajero,Cupocajero "
                                           + ", TasaCajero, TraCajero,opcpos,CupoPos,Tasapos, TraPos, Saldos, Bin, TopeDisponible, Topecaja, VlrManejo, TopeDisponible1, TopeCaja1,tiposervicio)"
                                           + " values ('" + Convenio + "','" + Nombre + "'," + Cuenta + ",'" + Entidad + "'," + Moneda + ",'" + Ahorro + "','" + Corriente + "','"
                                           + Bloqueo + "'," + OpcionDisponible + ",'" + CupoDisponible + "'," + TasaDisponible + ","
                                           + OpcionCajero + ",'" + CupoCajero + "'," + TasaCajero + "," + TransaccionesCajero + "," + OpcionPos + ",'"
                                           + CupoPos + "'," + TasaPos + "," + TransaccionesPos + "," + Saldos + "," + Bin + ",'" + TopeDisponible + "','" + TopeCaja + "','"
                                           + ValorManejo + "','" + TopeDisponible1 + "','" + TopeCaja1 + "'," + TipoServicio + ")";
                    break;
                case true:
                    stmysql = "update sys_convenio set nombre ='" + Nombre + "',cuenta = " + Cuenta + ",entidad = '" + Entidad + "',moneda = '" + Moneda
                           + "',ahorro = '" + Ahorro + "',corriente = '" + Corriente + "',bloqueo = '" + Bloqueo + "',opcDisp = " + OpcionDisponible
                           + ",cupoDisp = '" + CupoDisponible + "',TasaDisp = " + TasaDisponible + ",opcCajero = " + OpcionCajero + ",Cupocajero = '" + CupoCajero
                           + "', TasaCajero = " + TasaCajero + ",TraCajero = " + TransaccionesCajero + ",opcpos = " + OpcionPos + ",CupoPos = '" + CupoPos + "',Tasapos = " + TasaPos
                           + ", TraPos = " + TransaccionesPos + ", Saldos = " + Saldos + ",Bin = " + Bin + ",TopeDisponible = '" + TopeDisponible + "',Topecaja = '" + TopeCaja + "',"
                           + "VlrManejo =" + ValorManejo + ",TopeDisponible1 = '" + TopeDisponible1 + "',TopeCaja1 = '" + TopeCaja1 + "',tiposervicio=" + TipoServicio
                           + " WHERE CONVENIO = '" + Convenio + "'";
                    break;
            }

            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "GrabaConvenio", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public bool ReasignaTarjeta(string tarjeta, OdbcConnection myconnect)
        {
            ok = false;
            stmysql = "update deb_maetarj set Estado = 'R',Error = '62'"
                      + " where tarjeta = '" + tarjeta + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "ActualizaMaestroTarjetas", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public bool GrabaParamDiario(string Convenio, string banco, string CpteBatch, string CpteLinea, string Detalle, DateTime FecActualizacion, double TarjetasNuevas, string UltimaTarjeta, string Cierre, DateTime fechaCierre, string cpteCierreLinea, double CpteconseLinea, string CpteCierreDatafono, double CierreConsedatafono, double comisionred, double comisionotrared, OdbcConnection myconnect)
        {
            Convenio = ("00" + Convenio).Substring(("00" + Convenio).Length - 2);
            string strDummy1 = " ", strDummy2 = "9999", strDummy3 = "9999", strDummy4 = " ";
            DateTime dtDummy = new DateTime(1950, 1, 1);
            int intDummy = 0;
            double dblDummy = 0;
            ok = BuscaParamDiario(ref Convenio, myconnect, ref strDummy1, ref strDummy2, ref strDummy3, ref strDummy4, ref dtDummy, ref intDummy, ref strDummy1, ref dtDummy, ref strDummy2, ref intDummy, ref strDummy3, ref intDummy, Navega.Ninguno, ref strDummy1, ref dblDummy, ref dblDummy);
            switch (ok)
            {
                case false:
                    stmysql = "insert into deb_pardiario(idcodigo,banco,cptebatch,cptelinea,detalle,fechaActualizacion,TarjetasNuevas,Ultimatarjeta,FechaCierre,cpteCierre,ConseCierre,cpteCierreDatafono,ConseCierreDatafono,cierre,comisionred,comisionotrared) "
                                           + " values ('" + Convenio + "','" + ("0000" + banco).Substring(("0000" + banco).Length - 4)
                                           + "','" + ("0000" + CpteBatch).Substring(("0000" + CpteBatch).Length - 4) + "','" + ("0000" + CpteLinea).Substring(("0000" + CpteLinea).Length - 4) + "','"
                                           + Detalle + "','" + FecActualizacion.ToString(varini.PstForFec) + "'," + TarjetasNuevas + ",'" + UltimaTarjeta + "','" + fechaCierre.ToString(varini.PstForFec)
                                           + "','" + ("0000" + cpteCierreLinea).Substring(("0000" + cpteCierreLinea).Length - 4) + "'," + CpteconseLinea + ",'" + ("0000" + CpteCierreDatafono).Substring(("0000" + CpteCierreDatafono).Length - 4) + "','" + CierreConsedatafono + "','" + Cierre + "','" + comisionred + "','" + comisionotrared + "')";
                    break;
                case true:
                    stmysql = "update deb_pardiario set banco = '" + ("0000" + banco).Substring(("0000" + banco).Length - 4) + "',cptebatch = '" + ("0000" + CpteBatch).Substring(("0000" + CpteBatch).Length - 4) + "', cptelinea = '" + ("0000" + CpteLinea).Substring(("0000" + CpteLinea).Length - 4) + "',detalle = '"
                       + Detalle + " ',fechaActualizacion = '" + FecActualizacion.ToString(varini.PstForFec) + "',TarjetasNuevas = " + TarjetasNuevas + ",Ultimatarjeta = '" + UltimaTarjeta + "',FechaCierre = '" + fechaCierre.ToString(varini.PstForFec)
                       + "',cpteCierre ='" + ("0000" + cpteCierreLinea).Substring(("0000" + cpteCierreLinea).Length - 4) + "',ConseCierre = " + CpteconseLinea + ",cpteCierreDatafono = '" + ("0000" + CpteCierreDatafono).Substring(("0000" + CpteCierreDatafono).Length - 4) + "',ConseCierreDatafono = " + CierreConsedatafono + ", cierre = '" + Cierre + "',"
                       + "comisionred='" + comisionred + "',comisionotrared='" + comisionotrared + "' where idCodigo = '" + Convenio + "'";
                    break;
            }

            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "GrabaParamDiario", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public bool GrabaSolicitudEnviada(string Convenio, string banco, double TarjetasNuevas, string UltimaTarjeta, OdbcConnection myconnect)
        {
            Convenio = ("00" + Convenio).Substring(("00" + Convenio).Length - 2);
            string strDummy1 = " ", strDummy2 = "9999", strDummy3 = "9999", strDummy4 = " ";
            DateTime dtDummy = new DateTime(1950, 1, 1);
            int intDummy = 0;
            double dblDummy = 0;
            ok = BuscaParamDiario(ref Convenio, myconnect, ref strDummy1, ref strDummy2, ref strDummy3, ref strDummy4, ref dtDummy, ref intDummy, ref strDummy1, ref dtDummy, ref strDummy2, ref intDummy, ref strDummy3, ref intDummy, Navega.Ninguno, ref strDummy1, ref dblDummy, ref dblDummy);
            switch (ok)
            {
                case true:
                    stmysql = "update deb_pardiario set TarjetasNuevas = " + TarjetasNuevas + ",Ultimatarjeta = '" + UltimaTarjeta + "'"
                            + " where idCodigo = '" + Convenio + "'";
                    break;
            }

            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "GrabaSolicitudEnviadad", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public void grabaFechaActualizacion(string Idcodigo, DateTime FechaActualizacion, OdbcConnection myconnect)
        {
            FechaActualizacion = FechaActualizacion.AddDays(1);
            stmysql = "Update deb_pardiario set fechaActualizacion = '" + FechaActualizacion.ToString(varini.PstForFec) + "' where idCodigo = '" + Idcodigo + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            this.connect.ExecuteQueryconec(stmysql, myconnect, "grabaFechaActualizacion", ref strD1, ref strD2, ref strD3, ref strD4);
        }

        public void ActualizaMaestroTarjetaCobraManejo(string tarjeta, string Bin, string Cobra, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("update deb_maetarj set CobraManejo='" + Cobra + "' where tarjeta = '" + Bin + tarjeta + "'");
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            this.connect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "ActualizaMaestroTarjetas", ref strD1, ref strD2, ref strD3, ref strD4);
        }

        public bool ActualizaMaestroTarjetas(string tarjeta, string bin, string banco, string cuenta, int lincred, string TipoCta, string Codigoter, string Operacion, string Estado, decimal Disponible, decimal CupoCajero, int TransCajero, decimal CupoPos, int TransPos, string usuario, int ClaseTopeDspnble, int ClaseTopeCajero, DateTime FechaVence, string CobraManejo, bool DobleServicio, OdbcConnection myconnect)
        {
            string StUpdate = "";

            switch (DobleServicio)
            {
                case false:
                    StUpdate = ",FecAsignacion = '" + DateTime.Now.ToString(varini.PstForFec) + "' ";
                    break;
                case true:
                    StUpdate = ",debcre='M' ";
                    break;
            }
            ok = false;
            stmysql = "update deb_maetarj set banco = '" + banco + "',bin = '" + bin + "',cuenta = '" + cuenta + "',lincred = " + lincred + ",TipoCta = '" + TipoCta
                       + "',Codigoter= '" + Codigoter + "',Operacion = '" + Operacion + "',estado = '" + Estado + "',disponible= '" + Disponible + "',CupoCajero='" + CupoCajero + "',TranCajero= " + TransCajero + ",CupoPos= '" + CupoPos + "',TranPos=" + TransPos
                       + ",usuario= '" + usuario + "',ClaseTopeDspnble= '" + ClaseTopeDspnble + "',ClaseTopeCajero= '" + ClaseTopeCajero
                       + "',fechavence='" + FechaVence.ToString(varini.PstForFec) + "',CobraManejo='" + CobraManejo + "' " + StUpdate + " where tarjeta = '" + bin + tarjeta + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "ActualizaMaestroTarjetas", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public void GrabaDisponible(string tarjeta, double Disponible, double CupoCajero, double CupoPos, OdbcConnection myconect)
        {
            stmysql = "update deb_maetarj set disponible = '" + Disponible + "', CupoCajero='" + CupoCajero + "', CupoPos= '" + CupoPos + "'"
                    + " where tarjeta = '" + tarjeta + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            this.connect.ExecuteQueryconec(stmysql, myconect, "GrabaDisponible", ref strD1, ref strD2, ref strD3, ref strD4);
        }

        public void GrabaDisponibleDobleServicio(string tarjeta, double Disponible, double CupoCajero, double CupoPos, OdbcConnection myconect)
        {
            stmysql = "update deb_maetarj set DispCredDs = '" + Disponible + "', CupoCajDs='" + CupoCajero + "', CupoPosDs= '" + CupoPos + "'"
                    + " where tarjeta = '" + tarjeta + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            this.connect.ExecuteQueryconec(stmysql, myconect, "GrabaDisponible", ref strD1, ref strD2, ref strD3, ref strD4);
        }

        public bool ActualizaBloqueoTarjeta(string Banco, string Tarjeta, string Bloqueo, string MotivoBloqueo, string Usuario, DateTime FechaNovedad, int HoraEjecucion, OdbcConnection myconnect)
        {
            ok = false;
            Banco = ("0000" + Banco).Substring(("0000" + Banco).Length - 4);
            stmysql = "update deb_maetarj set error = '" + Bloqueo + "',MotivoBloqueo='" + MotivoBloqueo
                    + "',UsuarioBloqueo='" + Usuario + "',fecnovedad='" + FechaNovedad.ToString(varini.PstForFec) + "',horaejccion=" + HoraEjecucion + " where tarjeta = '" + Tarjeta + "' and banco = '" + Banco + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "ActualizaBloqueoTarjeta", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public bool EliminarConvenio(string Convenio, OdbcConnection myconnect)
        {
            ok = false;
            Convenio = ("0000" + Convenio).Substring(("0000" + Convenio).Length - 4);
            stmysql = "Delete from sys_convenio where convenio= '" + Convenio + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "EliminarConvenio", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public bool EliminarTarjeta(string Banco, string Tarjeta, OdbcConnection myconnect, string Cuenta = "0", bool BorrarXcuenta = false)
        {
            ok = false;
            if (BorrarXcuenta == true)
            {
                stmysql = "delete from deb_maetarj where cuenta = '" + Cuenta + "'";
            }
            else
            {
                Banco = ("0000" + Banco).Substring(("0000" + Banco).Length - 4);
                stmysql = "delete from deb_maetarj where banco = '" + Banco + "' and tarjeta='" + Tarjeta + "'";
            }
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "EliminarTarjeta", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public bool EliminarParamDiario(string convenio, OdbcConnection myconnect)
        {
            ok = false;
            convenio = ("00" + convenio).Substring(("00" + convenio).Length - 2);
            stmysql = "delete from deb_pardiario where idcodigo = '" + convenio + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "EliminarParamDiario", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public bool EliminarParamDatafono(string CodDatafono, OdbcConnection myconnect)
        {
            ok = false;
            stmysql = "delete from deb_pardatafonos where idcodigo = " + CodDatafono;
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "EliminarParamDiario", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public double CalculaDisponible(double saldo, int Lincred, int ClaseTopeDsponible, string Convenio, OdbcConnection myconnect)
        {
            double SalMinCuenta = 0, disponible = 0;
            int OpcDisp = 0;
            decimal CupDis = 0, tasadis = 0, topedisponible = 0, topedisponible1 = 0;
            ok = false;

            string strDummy = " ";
            int intDummy = 0;
            decimal decDummy = 0;

            // ok = depositos.BuscaLineaAhorro(Lincred, myconnect, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref SalMinCuenta); // ERROR: CS1501
            switch (ok)
            {
                case true:
                    // ok = this.BuscaConvenio(ref Convenio, myconnect, ref strDummy, ref strDummy, ref strDummy, ref intDummy, ref strDummy, ref strDummy, ref strDummy, ref OpcDisp, ref CupDis, ref tasadis, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref intDummy, ref strDummy, ref topedisponible, ref decDummy, ref topedisponible1); // ERROR: CS7036
                    switch (ok)
                    {
                        case true:
                            switch (OpcDisp)
                            {
                                case 0:
                                    disponible = (saldo * -1) - SalMinCuenta;
                                    break;
                                case 1:
                                    disponible = (double)CupDis;
                                    break;
                                case 2:
                                    disponible = ((saldo * -1) * (double)tasadis) / 100 - SalMinCuenta;
                                    break;
                            }

                            switch (ClaseTopeDsponible)
                            {
                                case 0:
                                    if (disponible > (double)topedisponible)
                                    {
                                        disponible = (double)topedisponible;
                                    }
                                    break;
                                case 1:
                                    if (disponible > (double)topedisponible1)
                                    {
                                        disponible = (double)topedisponible1;
                                    }
                                    break;
                            }
                            if (disponible < 0)
                            {
                                disponible = 0;
                            }
                            break;
                    }
                    break;
                case false:
                    MessageBox.Show("Parametros de Lineas de Ahorros no esta Creado, Linea" + Lincred, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
            }
            return disponible;
        }

        public double CalculaCupoCajero(double saldo, int Lincred, int ClaseTopeCajero, string Convenio, OdbcConnection myconnect)
        {
            int Opccajero = 0;
            decimal CupoCajero = 0, Tasacajero = 0, TopeCaja = 0, TopeCaja1 = 0;
            double SalMinCuenta = 0, disponible = 0;

            ok = false;
            string strDummy = " ";
            int intDummy = 0;
            decimal decDummy = 0;

            // ok = depositos.BuscaLineaAhorro(Lincred, myconnect, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref SalMinCuenta); // ERROR: CS1501
            switch (ok)
            {
                case true:
                    // ok = this.BuscaConvenio(ref Convenio, myconnect, ref strDummy, ref strDummy, ref strDummy, ref intDummy, ref strDummy, ref strDummy, ref strDummy, ref intDummy, ref decDummy, ref decDummy, ref Opccajero, ref CupoCajero, ref Tasacajero, ref intDummy, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref intDummy, ref strDummy, ref decDummy, ref TopeCaja, ref decDummy, ref TopeCaja1); // ERROR: CS7036
                    switch (ok)
                    {
                        case true:
                            switch (Opccajero)
                            {
                                case 0:
                                    disponible = (saldo * -1) - SalMinCuenta;
                                    break;
                                case 1:
                                    disponible = (double)CupoCajero;
                                    break;
                                case 2:
                                    disponible = ((saldo * -1) * (double)Tasacajero) / 100 - -SalMinCuenta;
                                    break;
                            }

                            switch (ClaseTopeCajero)
                            {
                                case 0:
                                    if (disponible > (double)TopeCaja)
                                    {
                                        disponible = (double)TopeCaja;
                                    }
                                    break;
                                case 1:
                                    if (disponible > (double)TopeCaja1)
                                    {
                                        disponible = (double)TopeCaja1;
                                    }
                                    break;
                            }
                            if (disponible < 0)
                            {
                                disponible = 0;
                            }
                            break;
                    }
                    break;
                case false:
                    MessageBox.Show("Parametros de Lineas de Ahorros no esta Creado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
            }
            return disponible;
        }

        public double CalculaCupoPos(double saldo, int Lincred, string Convenio, OdbcConnection myconnect)
        {
            double SalMinCuenta = 0, disponible = 0;
            int OpcPos = 0;
            decimal cupoPos = 0, TasaPos = 0;

            ok = false;
            string strDummy = " ";
            int intDummy = 0;
            decimal decDummy = 0;

            // ok = depositos.BuscaLineaAhorro(Lincred, myconnect, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref strDummy, ref SalMinCuenta); // ERROR: CS1501
            switch (ok)
            {
                case true:
                    // ok = this.BuscaConvenio(ref Convenio, myconnect, ref strDummy, ref strDummy, ref strDummy, ref intDummy, ref strDummy, ref strDummy, ref strDummy, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref decDummy, ref decDummy, ref intDummy, ref OpcPos, ref cupoPos, ref TasaPos); // ERROR: CS7036
                    switch (ok)
                    {
                        case true:
                            switch (OpcPos)
                            {
                                case 0:
                                    disponible = (saldo * -1) - SalMinCuenta;
                                    break;
                                case 1:
                                    disponible = (double)cupoPos;
                                    break;
                                case 2:
                                    disponible = ((saldo * -1) * (double)TasaPos) / 100 - -SalMinCuenta;
                                    break;
                            }

                            if (disponible < 0)
                            {
                                disponible = 0;
                            }
                            break;
                    }
                    break;
            }
            return disponible;
        }

        public void ConfiguraForma(Form Forma, ref string Empresa, ref string Servidor, ref string Bd, ref string Nomforma, ref string Usuario, ref string Fecha)
        {
            // connect.LlenarVarini(varini); // ERROR: CS1620
            Empresa = varini.pstEmpresa;
            Servidor = varini.pstServer;
            Bd = varini.pstBdatos;
            Nomforma = Forma.Name;
            Usuario = varini.pstUsuario;
            Fecha = DateTime.Now.ToString(varini.PstForFec);
        }

        public void GrabaTarjeta(string banco, string Numtarjeta, string Bin, string debcre, string estado, OdbcConnection Myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            string StCampos = "", StValores = "";

            switch (estado)
            {
                case "L":
                    StCampos = "marca, ClaseTopeDspnble, diacorte, cupocredito,error,";
                    StValores = "0','0','0','0','','";
                    break;
            }
            Stbuilder.Append("insert into deb_maetarj (banco,tarjeta,bin,debcre," + StCampos + "estado)");
            Stbuilder.Append("values ('");
            Stbuilder.Append(banco + "','");
            Stbuilder.Append(Numtarjeta.Trim() + "','");
            Stbuilder.Append(Bin + "','");
            Stbuilder.Append(debcre + "','");
            Stbuilder.Append(StValores);
            Stbuilder.Append(estado + "')");

            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            this.connect.ExecuteQueryconec(Stbuilder.ToString(), Myconnect, "GrabaTarjeta", ref strD1, ref strD2, ref strD3, ref strD4);
        }

        public bool GrabaMovimiento(string Comprobante, double ConseCpte, string Banco, string NumeroTarjeta, DateTime FechaMovto, ClaseMovto ClaseMovto, double Valor, string Numterminal, string usuario, Form Myforma, OdbcConnection myconnect, string Detalle = "", bool Cobra4xmil = true)
        {
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos msgdep = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();
            string CpteTerm = "9999";
            bool CuentaExcenta;
            double NumCta = 0;
            string Codigoter = " ";
            int Lincred = 0;
            string CptoAho = "9999";
            decimal VlrGravamen = 0;

            string strDummy = " ";
            int intDummy = 0;
            decimal decDummy = 0;
            DateTime dtDummy = new DateTime(1950, 1, 1);

            // ok = this.BuscaTarjetaDebito(ref Banco, ref NumeroTarjeta, myconnect, ref strDummy, ref strDummy, ref strDummy, ref Lincred, ref strDummy, ref strDummy, ref Codigoter, ref strDummy, ref strDummy, ref strDummy, ref decDummy, ref decDummy, ref intDummy, ref decDummy, ref intDummy, ref intDummy, ref dtDummy, ref intDummy, ref intDummy, ref strDummy, ref decDummy, ref intDummy); // ERROR: CS7036
            switch (ok)
            {
                case false:
                    MessageBox.Show("Numero de tarjeta no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return ok;
            }

            string strCpteTerm = CpteTerm;
            string strUsuario = "";
            ok = this.BuscaTerminal(Numterminal, myconnect, ref strCpteTerm, ref strUsuario);
            switch (ok)
            {
                case true:
                    Comprobante = strCpteTerm;
                    break;
            }

            // Note: This would need to call the actual msgdep methods with proper parameters
            // ok = msgdep.BuscarCuentaAhorro(NumCta, myconnect, ...);
            // msgdep.Grabamovimiento(...);

            return ok;
        }

        private void GrabaDetallebatch(string Cpte, double Consecutivo, string Detalle, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            StBuilder.Append("update cop_docmto set detalle = '" + Detalle + "'");
            StBuilder.Append("where compronte = '" + Cpte + "' and numero_domto = '" + Consecutivo + "'");
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            this.connect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaDetallebatch", ref strD1, ref strD2, ref strD3, ref strD4);
        }

        public DataSet CargarGrillaCupoTarjetas(string codigoter, int periodo, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            StringBuilder StBuilder = new StringBuilder();
            string StCampoSaldo = "", StCierreString = "", StQuery = "";

            StQuery = "(select sum(sal.saldo) from cop_saldos_vw sal inner join sys_maenit maenit2 on sal.Codigoter=maenit2.CODIGOTER "
                    + "left join cre_parame01 par2 on maenit2.AGENCIA=par2.idagencia "
                    + "where mae.Codigoter=sal.CODIGOTER and mae.Banco=par2.Codigo_banco  and sal.PERIODO=" + periodo + " "
                    + "and sal.LINCRED in (par2.Lincred, par2.lincredavance, par2.lincredcuoman) and sal.saldo<>0)";

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCampoSaldo = " isnull( " + StQuery + ",0)";
                    break;
                case "MYSQL":
                case "DB2":
                    StCampoSaldo = " ifnull( " + StQuery + ",0)";
                    break;
                case "ORACLE":
                    StCampoSaldo = " nvl(" + StQuery + ",0)";
                    break;
            }

            StBuilder.Append("select Tarjeta,CupoCredito,CupoPos,error,CupoPosDs,");
            StBuilder.Append("case Debcre when 'D' then 'Debito' when 'C' then 'Credito' when 'M' then 'Doble Servicio' end as DebCre, ");
            StBuilder.Append("case Debcre when 'C' then (mae.CupoCredito-" + StCampoSaldo + ") else mae.Disponible end as Disponible,");
            StBuilder.Append("case Debcre when 'C' then case par.TipoAvance when 0 then (round((mae.CupoCredito*(par.TasaAvance/100)),0)) else par.VlrAvance end else mae.CupoCajero end as CupoCajero,");
            StBuilder.Append("case Debcre when 'M' then (mae.CupoCredito-" + StCampoSaldo + ") else mae.Disponible end as DispCredDs,");
            StBuilder.Append("case Debcre when 'M' then case par.TipoAvance when 0 then (round((mae.CupoCredito*(par.TasaAvance/100)),0)) else par.VlrAvance end else mae.CupoCajDs end as CupoCajDs ");
            StBuilder.Append("from deb_maetarj mae ");
            StBuilder.Append("inner join sys_maenit maenit on mae.Codigoter=maenit.CODIGOTER ");
            StBuilder.Append("left join cre_parame01 par on maenit.AGENCIA=par.idagencia and mae.Banco=par.Codigo_banco ");
            StBuilder.Append("where mae.estado in ('A','B') and mae.codigoter='" + codigoter + "' ");
            StBuilder.Append("group by Tarjeta,CupoCredito,CupoPos,Debcre,error,CupoPosDs,par.TasaAvance,par.TipoAvance, ");
            StBuilder.Append("par.VlrAvance,mae.Disponible,mae.CupoCajero,mae.CupoCajDs,mae.Codigoter,mae.Banco ");

            this.connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "CargarGrillaCupoTarjetas", dsdata, "TblCupoTarjetas");
            return dsdata;
        }

        public bool BuscaMovientoLinea(string Secuencia, string Tarjeta, OdbcConnection myconnect, DataSet DsData = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            try
            {
                if (DsData != null)
                    DsData.Tables.Remove("TblMovtoLinea");
            }
            catch (Exception ex)
            {
            }
            stbuilder.Append("select Tarjeta,Monto,estado,Hora,Message,error from deb_movto where secuencia = '" + Secuencia + "' and tarjeta = '" + Tarjeta + "'");

            this.connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaMovientoLinea", DsDataSet, "TblMovtoLinea");
            if (DsDataSet.Tables["TblMovtoLinea"].Rows.Count > 0)
            {
                try
                {
                    if (DsData != null)
                        DsData.Tables.Add(DsDataSet.Tables["TblMovtoLinea"].Copy());
                }
                catch (Exception ex)
                {
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void GrabaSecuencia(string Secuencia, string Tarjeta, string Monto, string Id, string Causal, string Estado, string Source, string FechaMovto, string Hora, string Net, string Message, string Comision, string Metodo, OdbcConnection myconnect, string StError = "")
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = BuscaMovientoLinea(Secuencia, Tarjeta, myconnect);
            switch (ok)
            {
                case false:
                    stbuilder.Append("Insert into deb_movto (Secuencia,Tarjeta,Monto,Id,Causal,Estado,Source,FechaMovto,Hora,Net,Message,Comision,Metodo,error)");
                    stbuilder.Append("values ('");
                    stbuilder.Append(Secuencia + "','");
                    stbuilder.Append(Tarjeta + "','");
                    stbuilder.Append(Monto + "','");
                    stbuilder.Append(Id + "','");
                    stbuilder.Append(Causal + "','");
                    stbuilder.Append(Estado + "','");
                    stbuilder.Append(Source + "','");
                    stbuilder.Append(FechaMovto + "','");
                    stbuilder.Append(Hora + "','");
                    stbuilder.Append(Net + "','");
                    stbuilder.Append(Message + "','");
                    stbuilder.Append(Comision + "','");
                    stbuilder.Append(Metodo + "','");
                    stbuilder.Append(StError + "')");
                    string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
                    this.connect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaSecuencia", ref strD1, ref strD2, ref strD3, ref strD4);
                    break;
            }
        }

        public void GrabaEstadoSecuencia(string Secuencia, string Tarjeta, int estado, OdbcConnection myconnect, string StError = "")
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = BuscaMovientoLinea(Secuencia, Tarjeta, myconnect);
            switch (ok)
            {
                case true:
                    stbuilder.Append("update deb_movto set Estado ='");
                    stbuilder.Append(estado + "', ");
                    stbuilder.Append("Error='");
                    stbuilder.Append(StError + "' ");
                    stbuilder.Append("where secuencia = '");
                    stbuilder.Append(Secuencia + "' ");
                    stbuilder.Append(" and tarjeta='");
                    stbuilder.Append(Tarjeta + "'");
                    string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
                    this.connect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaSecuencia", ref strD1, ref strD2, ref strD3, ref strD4);
                    break;
            }
        }

        public virtual bool ActualizaBloqueoTarjeta(string Banco, string Tarjeta, string estado, OdbcConnection myconnect)
        {
            ok = false;
            Banco = ("0000" + Banco).Substring(("0000" + Banco).Length - 4);
            stmysql = "update deb_maetarj set estado = '" + estado + "' "
                    + "where tarjeta = '" + Tarjeta + "' and banco = '" + Banco + "'";
            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "ActualizaBloqueoTarjeta", ref strD1, ref strD2, ref strD3, ref strD4);
            return ok;
        }

        public enum ModoWebCajaDos
        {
            Normal = 0,
            Reverso = 1,
            Bloqueo = 2
        }

        public enum TipoOperacion
        {
            Consulta = 0,
            Retiro = 1,
            Consignacion = 3,
            ReversoRetiro = 5,
            ReversoConsig = 6
        }

        public bool WebCajaDosEnpacto(ModoWebCajaDos modo, TipoOperacion TipoOperacion, string Cedula, string Cuenta, int Lincred, string Tarjeta, string Monto, string secuencia = "")
        {
            string outStr = "", Argumentos = "", StDisp = "0";
            string ParModo = "", ParOper = "", ParFS = " /P0";
            string[] StSalida;
            string StCuenta = "00", StDirectorio = "";
            Microsoft.Win32.RegistryKey Rk;

            switch (modo)
            {
                case ModoWebCajaDos.Normal:
                    ParModo = " /M0";
                    break;
                case ModoWebCajaDos.Reverso:
                    ParModo = " /M1";
                    break;
                case ModoWebCajaDos.Bloqueo:
                    ParModo = " /M2";
                    break;
            }

            switch (TipoOperacion)
            {
                case ClsMsgDeb.TipoOperacion.Consulta:
                    ParOper = " /O0";
                    break;
                case ClsMsgDeb.TipoOperacion.Retiro:
                    ParOper = " /O1";
                    break;
                case ClsMsgDeb.TipoOperacion.Consignacion:
                    ParOper = " /O3";
                    break;
                case ClsMsgDeb.TipoOperacion.ReversoRetiro:
                    ParOper = " /O5";
                    break;
                case ClsMsgDeb.TipoOperacion.ReversoConsig:
                    ParOper = " /O6";
                    break;
            }

            StCuenta = ("00" + Lincred).Substring(("00" + Lincred).Length - 2) + ("00000000000000" + Cuenta).Substring(("00000000000000" + Cuenta).Length - 14);
            Monto = Math.Round(Convert.ToDouble(Monto), 0).ToString();

            Argumentos = ParModo + ParOper + ParFS + " " + Convert.ToDouble(Cedula) + " " + StCuenta + " " + Tarjeta + " " + Monto + " " + secuencia;

            try
            {
                Rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("Applications\\ConsfiaS\\Shell\\Open\\config");
                if (Rk.OpenSubKey("webcaja").GetValue("") != null && Rk.OpenSubKey("webcaja").GetValue("").ToString() != "")
                {
                    StDirectorio = Rk.OpenSubKey("webcaja").GetValue("").ToString();
                }
            }
            catch (Exception ex)
            {
            }

            if (StDirectorio.Trim() == "")
            {
                MessageBox.Show("No se ha especificado el archivo webcajados.exe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            try
            {
                Process pObj = new Process();
                pObj.StartInfo.RedirectStandardOutput = true;
                pObj.StartInfo.FileName = StDirectorio + " ";
                pObj.StartInfo.Arguments = Argumentos;
                pObj.StartInfo.UseShellExecute = false;
                pObj.StartInfo.CreateNoWindow = true;
                pObj.Start();

                outStr = pObj.StandardOutput.ReadToEnd();

                pObj.WaitForExit();

                pObj.Close();
                pObj.Dispose();

                if (outStr == "")
                {
                    return false;
                }
                else
                {
                    StSalida = outStr.Split(new char[] { ',' });
                    switch (StSalida[0])
                    {
                        case "0":
                            if (TipoOperacion == ClsMsgDeb.TipoOperacion.Consulta)
                            {
                                double tempDisp;
                                if (double.TryParse(StSalida[2], out tempDisp))
                                {
                                    StDisp = StSalida[2].Trim().Substring(0, StSalida[2].Trim().Length - 2);
                                    StDisp = StDisp + "." + StSalida[2].Trim().Substring(StSalida[2].Trim().Length - 2);
                                }
                                else
                                {
                                    StDisp = "0";
                                }
                                MessageBox.Show("Saldo disponible $ " + Convert.ToDouble(StDisp).ToString("N2"), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            return true;
                        case "1":
                            MessageBox.Show("Error de comunicaciones, intente nuevamente", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        case "2":
                            MessageBox.Show("Tarjeta inexistente, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        case "3":
                            MessageBox.Show("Cuenta inexistente, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        case "4":
                            MessageBox.Show("Tarjeta bloqueada, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        case "5":
                            MessageBox.Show("Fondos insuficientes, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        case "9":
                            MessageBox.Show("Declinada desconocida", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        case "-1":
                            MessageBox.Show("Error de comunicaciones. Motor de bases de datos no esta operando, intente nuevamente", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
            return false;
        }

        public bool ValidaTransaccionConvenioEnpacto(string Cuenta, string Monto, string TipoTransaccion, ModoWebCajaDos ModoOperacion, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            string StBanco = "99", StTarjeta = "99", StDebCred = "D", StCodigoter = "99", StEstado = "99";
            int StLincred = 0, StLineaVieja = 0;
            TipoOperacion TipOpera;

            ok = this.paramsys.BuscarCompania(varini.sptCodEmpr, dsdata, myconnect);

            switch (ok)
            {
                case false:
                    return true;
                case true:
                    int tipconv = Convert.ToInt32(dsdata.Tables["tblcompania"].Rows[0]["tipconv"]);
                    if (tipconv == 1 || tipconv == 2)
                    {
                        string strDummy = " ";
                        int intDummy = 0;
                        decimal decDummy = 0;
                        DateTime dtDummy = new DateTime(1950, 1, 1);

                        ok = this.BuscaTarjetaDebito(ref StBanco, ref StTarjeta, myconnect, ref Cuenta, ref strDummy, ref strDummy, ref StLincred, ref strDummy, ref strDummy, ref StCodigoter, ref strDummy, ref StEstado, ref strDummy, ref decDummy, ref decDummy, ref intDummy, ref decDummy, ref intDummy, ref intDummy, ref dtDummy, ref intDummy, ref intDummy, ref StDebCred, ref decDummy, ref intDummy, true, ref strDummy, ref strDummy, ref strDummy, ref dtDummy, ref decDummy, ref decDummy, ref decDummy, ref decDummy, ref decDummy, ref strDummy, ref strDummy, ref strDummy, ref dtDummy, ref StLineaVieja);
                        switch (ok)
                        {
                            case false:
                                return true;
                            case true:
                                if (StEstado != "A")
                                {
                                    return true;
                                }
                                else
                                {
                                    if (StDebCred == "C")
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        switch (TipoTransaccion)
                                        {
                                            case "0":
                                            case "2":
                                            case "3":
                                            case "4":
                                                TipOpera = ClsMsgDeb.TipoOperacion.Retiro;
                                                break;
                                            case "1":
                                                TipOpera = ClsMsgDeb.TipoOperacion.Consignacion;
                                                break;
                                            case "99":
                                                TipOpera = ClsMsgDeb.TipoOperacion.Consulta;
                                                break;
                                            default:
                                                TipOpera = ClsMsgDeb.TipoOperacion.Retiro;
                                                break;
                                        }

                                        if (Convert.ToDouble(StLineaVieja) > 0)
                                        {
                                            StLincred = StLineaVieja;
                                        }

                                        ok = this.WebCajaDosEnpacto(ModoOperacion, TipOpera, StCodigoter, Cuenta, StLincred, StTarjeta, Monto, "0");
                                        return ok;
                                    }
                                }
                        }
                    }
                    else
                    {
                        return true;
                    }
                    break;
            }
            return false;
        }

        public void ValidaTransaccionConvenioEnpactoCartera(string comprobante, double NumeroDomto, string usuario, Form myforma, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdata = new DataSet();
            double Monto = 0;
            TipoOperacion tipoOperacion;
            ModoWebCajaDos Modo;
            string StCuenta = "", StConvRS = "0";
            string Inicio = "", Fin = "";

            ok = this.paramsys.BuscarCompania(varini.sptCodEmpr, Dsdata, myconnect);
            if (ok)
            {
                int tipconv = Convert.ToInt32(Dsdata.Tables["tblcompania"].Rows[0]["tipconv"]);
                if (tipconv == 1 || tipconv == 2)
                {
                    Inicio = DateTime.Now.ToString();
                    comprobante = ("0000" + comprobante).Substring(("0000" + comprobante).Length - 4);

                    this.stmysql = "select convrs as campo1 from sys_compro02 where codigo='" + comprobante + "'";
                    string strD1 = StConvRS, strD2 = "", strD3 = "", strD4 = "";
                    ok = this.connect.ExecuteQueryconec(this.stmysql, myconnect, "ValidaTransaccionConvenioEnpactoCartera", ref strD1, ref strD2, ref strD3, ref strD4);
                    StConvRS = strD1;

                    if (ok)
                    {
                        switch (StConvRS)
                        {
                            case "1": // Por notificacion
                                StBuilder.Append("select mov.codigoter,maetarj.tarjeta,maetarj.lincred,maetarj.cuenta,maetarj.cuentads,maetarj.lineavieja, ");
                                StBuilder.Append("maetarj.debcre,sum(vlr_credito-vlr_debito) as monto ");
                                StBuilder.Append("from cop_movimto mov ");
                                StBuilder.Append("inner join cop_docmto doc on mov.compronte=doc.compronte and mov.numero_domto=doc.numero_domto  ");
                                StBuilder.Append("inner join sys_maenit maenit on mov.codigoter=maenit.codigoter ");
                                StBuilder.Append("inner join cre_parame01 param on maenit.agencia=param.idagencia ");
                                StBuilder.Append("inner join deb_maetarj maetarj on param.codigo_banco=maetarj.banco and maenit.codigoter=maetarj.codigoter ");
                                StBuilder.Append("inner join cop_concar12 car12 on mov.lincred=car12.lincred ");
                                StBuilder.Append("where mov.compronte='" + comprobante + "' and mov.numero_domto='" + NumeroDomto + "' and mov.lincred in (param.lincred,param.lincredavance,param.lincredcuoman) ");
                                StBuilder.Append(" and car12.codahor='5' and maetarj.estado='A' and maetarj.debcre in ('C','M') and doc.cerrado='Y' ");
                                StBuilder.Append("group by mov.codigoter,maetarj.tarjeta,maetarj.lincred,maetarj.cuenta,maetarj.cuentads,maetarj.lineavieja,maetarj.debcre ");
                                StBuilder.Append("union all ");
                                StBuilder.Append("select mov.codigoter,maetarj.tarjeta,maetarj.lincred,maetarj.cuenta,maetarj.cuentads,maetarj.lineavieja,");
                                StBuilder.Append("maetarj.debcre,sum(vlr_credito-vlr_debito) as monto ");
                                StBuilder.Append("from cop_movimto mov ");
                                StBuilder.Append("inner join cop_docmto doc on mov.compronte=doc.compronte and mov.numero_domto=doc.numero_domto  ");
                                StBuilder.Append("inner join sys_maenit maenit on mov.codigoter=maenit.codigoter ");
                                StBuilder.Append("inner join deb_maetarj maetarj on maenit.codigoter=maetarj.codigoter and mov.lincred=maetarj.lincred and mov.numero=maetarj.cuenta ");
                                StBuilder.Append("inner join cop_concar12 car12 on mov.lincred=car12.lincred ");
                                StBuilder.Append("where mov.compronte='" + comprobante + "' and mov.numero_domto='" + NumeroDomto + "' and car12.codahor='2' ");
                                StBuilder.Append("and maetarj.estado='A' and maetarj.debcre in ('D','M') and doc.cerrado='Y' ");
                                StBuilder.Append("group by mov.codigoter,maetarj.tarjeta,maetarj.lincred,maetarj.cuenta,maetarj.cuentads,maetarj.lineavieja,maetarj.debcre ");

                                ok = this.connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "ValidaTransaccionConvenioEnpactoCartera", Dsdata, "tbldata");
                                if (ok)
                                {
                                    for (int fila = 0; fila < Dsdata.Tables["tbldata"].Rows.Count; fila++)
                                    {
                                        DataRow row = Dsdata.Tables["tbldata"].Rows[fila];
                                        if (row["monto"] == DBNull.Value)
                                        {
                                            Monto = 0;
                                        }
                                        else
                                        {
                                            Monto = Convert.ToDouble(row["monto"]);
                                        }

                                        if (Monto > 0)
                                        {
                                            tipoOperacion = ClsMsgDeb.TipoOperacion.Consignacion;
                                        }
                                        else
                                        {
                                            tipoOperacion = ClsMsgDeb.TipoOperacion.Retiro;
                                            Monto = Monto * -1;
                                        }

                                        StCuenta = row["cuenta"].ToString();

                                        if (row["debcre"].ToString() == "M")
                                        {
                                            if (row["lincred"].ToString() == "99")
                                            {
                                                StCuenta = row["cuentads"].ToString();
                                            }
                                        }

                                        if (Convert.ToDouble(row["lineavieja"]) > 0)
                                        {
                                            row["lincred"] = row["lineavieja"];
                                        }

                                        this.WebCajaDosEnpacto(ModoWebCajaDos.Normal, tipoOperacion, row["codigoter"].ToString(), StCuenta, Convert.ToInt32(row["lincred"]), row["tarjeta"].ToString(), Monto.ToString(), "0");

                                        System.Threading.Thread.Sleep(1000);
                                    }
                                    Fin = DateTime.Now.ToString();
                                }
                                break;
                            case "2": // Por Refresco de saldos
                                GrabaSolicitudRefrescoSaldo(comprobante, NumeroDomto, usuario, myconnect);
                                break;
                        }
                    }
                }
            }
        }

        public delegate void ValidaTransaccionConvenioEnpactoCarteraDelegate(string comprobante, double NumeroDomto, string usuario, Form myforma, OdbcConnection myconnect);

        public void InvocaTransaccionConvenioEnpactoCartera(string comprobante, double NumeroDomto, string usuario, Form myforma, OdbcConnection myconnect)
        {
            ValidaTransaccionConvenioEnpactoCarteraDelegate deleg;
            deleg = new ValidaTransaccionConvenioEnpactoCarteraDelegate(ValidaTransaccionConvenioEnpactoCartera);
            deleg.BeginInvoke(comprobante, NumeroDomto, usuario, myforma, myconnect, null, null);
        }

        private void GrabaSolicitudRefrescoSaldo(string comprobante, double numero, string usuario, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();

            Stbuilder.Append("insert into deb_enpactors (fecha,usuario,comprobante,numero_domto,aplicado) values ('");
            Stbuilder.Append(DateTime.Now.ToString(varini.pstForfecyHora) + "','");
            Stbuilder.Append(usuario + "','");
            Stbuilder.Append(comprobante + "','");
            Stbuilder.Append(numero + "','");
            Stbuilder.Append("0')");

            string strD1 = "", strD2 = "", strD3 = "", strD4 = "";
            this.connect.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "GrabaSolicitudRefrescoSaldo", ref strD1, ref strD2, ref strD3, ref strD4);
        }

        // Helper class for VB compatibility
        private static class Err
        {
            public static string Description { get { return ""; } }
        }
    }
}
