using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Cartera
{
    public partial class Clscartera
    {

        // ==========================================================================
        // AnulaComprobante
        // ==========================================================================
        public bool AnulaComprobante(string Comprobante, double ConseCpte, string CpteAnulacion, double ConseCpteAnulacion, DateTime FechaAnulacion, string Detalle, string Usuario, OdbcConnection Myconnect, Form myform)
        {
            string stmysql;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Anulando Comprobante :  " + Comprobante + "-" + ConseCpte, myform);
            double dif = 0;
            bool OK = false;
            int sw1 = 0;
            int TipoLinea = 0;
            ERP.Core.Tesoreria.Services.clstesoreria msgtes = new ERP.Core.Tesoreria.Services.clstesoreria(varini.pstUsuario);
            string fecFact = Strings.Format(FechaAnulacion, varini.PstForFec);
            string FecVence = Strings.Format(FechaAnulacion, varini.PstForFec);
            string FecProg = Strings.Format(FechaAnulacion, varini.PstForFec);
            string Cuenta;
            string factura = "0";
            string StTes = "N";
            int canreg = 0;
            int fila = 0;
            string DetaAnulacion = "Anulado con el comprobante " + CpteAnulacion + " - " + ConseCpteAnulacion;
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos msgdep = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();
            string StCodigoter = "0";
            int StLineaAux = 0;
            DateTime StFecSol = DateTime.Now;
            string StFecAprobado = DateTime.Now.ToString();
            string StFecpago = DateTime.Now.ToString();
            string StAprobado = "A";
            string StEstadoSol = "A";
            double StValSolicitado = 0;
            double StValAprobado = 0;
            string StOblinea = " ";
            string StObAprobado = " ";
            string StCerrado = "C";
            string stObSolicitud = " ";
            DataSet dscompania = new DataSet();
            double IdCptoAnticipo = 0;

            stmysql = "select codigoter,lincred, numero,movto.cuenta,vlr_debito,vlr_credito,cencos,agencia, movto.cod_movto,tipo_movto, ciclos, nit, factura,NRO_EXTRA "
                    + ",DOMTO_CRUCE,NUM_DOC_CRUCE,detalle,FecVence,SECUENCIA,codigo_banco,Numcheque,fecha_movto,ReliqCuota,idsolcre,feccausacdtas,idsolaux,movto.LineaAhorro  from cop_movimto movto inner join cop_codmov codmov on movto.cod_movto =codmov.cod_movto"
                    + " where compronte = '" + Comprobante + "' and numero_domto = " + ConseCpte;

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconnect, "AnulaComprobante", ref myRead, "TblAnulaCompronte");
            canreg = myRead.Tables["TblAnulaCompronte"].Rows.Count;

            msgbarra.ValorMinimoMaximo(0, canreg);
            msgbarra.Show();
            Application.DoEvents();

            this.msgcofsys.BuscarCompania(ref varini.sptCodEmpr, ref dscompania, Myconnect);
            IdCptoAnticipo = Convert.ToDouble(dscompania.Tables["tblcompania"].Rows[0]["CptoAnticipo"]);

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblAnulaCompronte"].Rows[fila];

                sw1 = 0;

                if (row["codigoter"].ToString() == "99999999999999" && row["lincred"].ToString() == "9999")
                {
                    //GrabaContrapartida(CpteAnulacion, ConseCpteAnulacion, row["codigoter"].ToString(), row["lincred"].ToString(), row["cuenta"].ToString(), row["nit"].ToString(), row["cod_movto"].ToString(), FechaAnulacion, row["vlr_credito"].ToString(), row["vlr_debito"].ToString(), "9999", Myconnect, row["factura"].ToString(), Usuario, row["DOMTO_CRUCE"].ToString()   , row["NUM_DOC_CRUCE"].ToString(), row["detalle"].ToString(), row["FecVence"].ToString(), row["cencos"].ToString());
                    sw1 = 1;
                    if (Convert.ToDouble(row["idsolaux"]) > 0)
                    {
                        //OK = this.BuscaSolicitudAuxilio(row["idsolaux"], Myconnect, ref StCodigoter, ref StLineaAux, ref StFecSol, ref StFecAprobado, ref StFecpago, ref StAprobado, ref StEstadoSol, ref StValSolicitado, ref StValAprobado, ref StOblinea, ref StObAprobado, ref stObSolicitud, ref StCerrado);
                        if (OK == true)
                        {
                            if (StEstadoSol == "A")
                            {
                                if (StCerrado == "Y")
                                {
                                    //this.GrabaSolicitudAuxliios(row["idsolaux"], Myconnect, StCodigoter, StLineaAux, StFecSol, StFecAprobado, StFecpago, StAprobado, StEstadoSol, StValSolicitado, StValAprobado, StOblinea, StObAprobado, stObSolicitud, "N");
                                }
                                else
                                {
                                    //this.GrabaSolicitudAuxliios(row["idsolaux"], Myconnect, StCodigoter, StLineaAux, StFecSol, StFecAprobado, StFecpago, StAprobado, StEstadoSol, StValSolicitado, StValAprobado, StOblinea, StObAprobado, stObSolicitud, "Y");
                                }
                            }
                        }
                    }
                }

                if (sw1 == 0)
                {
                    //switch (row["tipo_movto"].ToString())
                    //{
                    //    case "1":
                    //        this.GrabaCapital(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "2":
                    //        // intereses
                    //        this.GrabaInteres(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "3":
                    //        // mora
                    //        this.GrabaMora(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "4":
                    //        this.GrabaCapitalAportes(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "5":
                    //        // seguro
                    //        this.GrabaSeguro(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "6":
                    //        // admon
                    //        this.GrabaAdmon(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "7":
                    //    case "13":
                    //        this.GrabaCapitalAhorros(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "8":
                    //        // retencion
                    //        this.GrabaRetencion(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "9":
                    //        this.BuscaLinea(row["lincred"], Myconnect, ref TipoLinea);
                    //        if (TipoLinea == 2)
                    //        {
                    //            this.GrabaGmf(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        }
                    //        else if (TipoLinea == 3)
                    //        {
                    //            this.GrabaCapitalServicios(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        }
                    //        break;
                    //    case "10":
                    //        this.GrabaExtra(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["NRO_EXTRA"].ToString(), row["cod_movto"], Usuario);
                    //        break;
                    //    case "11":
                    //        GrabaCapitalCdats(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //    case "12":
                    //        GrabaInteresesCdats(CpteAnulacion, ConseCpteAnulacion, row["codigoter"], row["lincred"], row["numero"], row["vlr_credito"], row["vlr_debito"], FechaAnulacion.ToString("yyyyMM"), FechaAnulacion, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario);
                    //        break;
                    //}
                    //this.EliminaCuotaAnticipadaSecuencia(row["SECUENCIA"], Myconnect);

                    //eliminar_Sipla_Doc_Anulacion(Myconnect, row["SECUENCIA"], Comprobante, ConseCpte, CpteAnulacion, ConseCpteAnulacion, Usuario);

                    //if (row["Numcheque"].ToString().Trim() != "")
                    //{
                    //    msgdep.ElimanarCanje(row["numero"], row["codigo_banco"].ToString(), row["Numcheque"].ToString(), row["fecha_movto"], Myconnect);
                    //}
                    //if (Convert.ToDouble(row["ReliqCuota"]) > 0)
                    //{
                    //    GrabarNuevaCuota(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToDouble(row["ReliqCuota"]), Myconnect);
                    //}

                    //if (Convert.ToDouble(row["idsolcre"]) > 0)
                    //{
                    //    this.ReviveSolicitudCredito(row["idsolcre"], row["codigoter"], row["lincred"], row["numero"], Myconnect);
                    //    EliminaCuotaAnticipadaSolicitudCredito(row["idsolcre"], IdCptoAnticipo, Myconnect);
                    //}

                    //if (!(row["feccausacdtas"] is DBNull))
                    //{
                    //    DateTime feccausa = Convert.ToDateTime(row["feccausacdtas"]);
                    //    if (feccausa != new DateTime(1900, 1, 1))
                    //    {
                    //        if (row["LineaAhorro"] is DBNull || Convert.ToInt32(row["LineaAhorro"]) == 9999)
                    //        {
                    //            this.ReviveFecCausacionCdats(row["codigoter"], row["lincred"], row["numero"], feccausa, Myconnect, "A");
                    //        }
                    //        else
                    //        {
                    //            this.ReviveFecCausacionAhorro(row["codigoter"], row["lincred"], row["numero"], feccausa, row["LineaAhorro"], Myconnect, "0");
                    //        }
                    //    }
                    //}
                    msgbarra.PerformStep();
                }

                fila += 1;
            }

            msgbarra.Dispose();
            msgbarra.Close();
            myRead.Dispose();

            OK = this.CierreDocumento(CpteAnulacion, ConseCpteAnulacion, Myconnect);
            if (OK == true)
            {
                this.AnulaDocumento(Comprobante, ConseCpte, Myconnect, DetaAnulacion);
                return true;
            }
            else
            {
                return false;
            }
        }

        // ==========================================================================
        // BorraRegistro
        // ==========================================================================
        public bool BorraRegistro(string Comprobante, double NumCpte, double Secuencia, int periodo, OdbcConnection myconnet)
        {
            string stmysql;
            bool ok;
            string codigoter = "999999999999";
            int lincred = 0;
            double numero = 0;
            double debito = 0;
            double credito = 0;
            int cencos = 0;
            string agencia = " ";
            string cod_movto = " ";
            int ciclos = 0;
            int nro_extra = 0;
            int Tipo_movto = 0;
            double idsolcre = 0;
            string CptoTes = "0";
            string ConseTes = "0";
            DateTime Fecfac;
            DateTime FecVen;
            DateTime fecPro;
            string cuenta = "999999999999";
            string factura = "0";
            string CuotaReliq = "N";
            string codigo_banco = " ";
            string Numcheque = " ";
            DateTime fecha_movto = new DateTime(1950, 1, 1);
            string nit = "99999999999999";
            string StFecCausaCdats = "";
            double StNumSolAux = 0;
            ERP.Core.Tesoreria.Services.clstesoreria msgtes = new ERP.Core.Tesoreria.Services.clstesoreria();
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos msgdep = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            string StCodigoter = "0";
            int StLineaAux = 0;
            DateTime StFecSol = DateTime.Now;
            string StFecAprobado = DateTime.Now.ToString();
            string StFecpago = DateTime.Now.ToString();
            string StAprobado = "A";
            string StEstadoSol = "A";
            double StValSolicitado = 0;
            double StValAprobado = 0;
            string StOblinea = " ";
            string StObAprobado = " ";
            string StCerrado = "C";
            string stObSolicitud = " ";
            DataSet dscompania = new DataSet();
            double IdCptoAnticipo = 0;

            this.msgcofsys.BuscarCompania(ref varini.sptCodEmpr, ref dscompania, myconnet);
            IdCptoAnticipo = Convert.ToDouble(dscompania.Tables["tblcompania"].Rows[0]["CptoAnticipo"]);

            StBuilder.Append("select codigoter,lincred,numero,vlr_debito,vlr_credito,cencos,agencia,cod_movto,ciclos,nro_extra,nit,cuenta,factura,ReliqCuota, ");
            StBuilder.Append("codigo_banco,Numcheque,fecha_movto,idsolcre,feccausacdtas,idsolaux ");
            StBuilder.Append("from cop_movimto where SECUENCIA = " + Secuencia + " and COMPRONTE = '" + Comprobante + "' and NUMERO_DOMTO = " + NumCpte);

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnet, "BorraRegistro", ref dsdata, "TblBorrar");
            if (ok == false)
            {
                return false;
            }
            else
            {
                DataRow row = dsdata.Tables["TblBorrar"].Rows[0];
                codigoter = row["codigoter"].ToString();
                lincred = Convert.ToInt32(row["lincred"]);
                numero = Convert.ToDouble(row["numero"]);
                debito = Convert.ToDouble(row["vlr_debito"]);
                credito = Convert.ToDouble(row["vlr_credito"]);
                cencos = Convert.ToInt32(row["cencos"]);
                agencia = row["agencia"].ToString();
                cod_movto = row["cod_movto"].ToString();
                ciclos = Convert.ToInt32(row["ciclos"]);
                nro_extra = Convert.ToInt32(row["nro_extra"]);

                if (row["nit"] is DBNull)
                {
                    nit = "";
                }
                else
                {
                    nit = row["nit"].ToString();
                }

                if (row["factura"] is DBNull)
                {
                    factura = "";
                }
                else
                {
                    factura = row["factura"].ToString();
                }

                cuenta = row["cuenta"].ToString();
                CuotaReliq = row["ReliqCuota"].ToString();
                codigo_banco = row["codigo_banco"].ToString();
                Numcheque = row["Numcheque"].ToString();
                fecha_movto = Convert.ToDateTime(row["fecha_movto"]);
                idsolcre = Convert.ToDouble(row["idsolcre"]);

                if (!(row["feccausacdtas"] is DBNull))
                {
                    DateTime feccausa = Convert.ToDateTime(row["feccausacdtas"]);
                    if (feccausa != new DateTime(1900, 1, 1))
                    {
                        StFecCausaCdats = feccausa.ToString();
                    }
                }
                StNumSolAux = Convert.ToDouble(row["idsolaux"]);
            }

            //this.BuscaTipoMovto(cod_movto, myconnet, ref Tipo_movto);

            switch (Tipo_movto)
            {
                case 1:
                case 4:
                case 7:
                case 11:
                case 12:
                case 13:
                    BorraCapital(codigoter, lincred, numero, debito, credito, periodo, myconnet);
                    BorraCopmora(codigoter, lincred, numero, debito, credito, periodo, ciclos, "C", myconnet);
                    break;
                case 2:
                    BorraCopmora(codigoter, lincred, numero, debito, credito, periodo, ciclos, "I", myconnet);
                    break;
                case 3:
                    BorraCopmora(codigoter, lincred, numero, debito, credito, periodo, ciclos, "M", myconnet);
                    break;
                case 5:
                    BorraCopmora(codigoter, lincred, numero, debito, credito, periodo, ciclos, "S", myconnet);
                    break;
                case 6:
                    BorraCopmora(codigoter, lincred, numero, debito, credito, periodo, ciclos, "A", myconnet);
                    break;
                case 10:
                    BorraCapital(codigoter, lincred, numero, debito, credito, periodo, myconnet);
                    BorraExtras(codigoter, lincred, numero, debito, credito, periodo, nro_extra, myconnet);
                    BorraCopmora(codigoter, lincred, numero, debito, credito, periodo, ciclos, "EX", myconnet, nro_extra);
                    break;
            }

            if (factura == null)
            {
                factura = "0";
            }

            BorraDocumentos(Comprobante, NumCpte, debito, credito, myconnet);

            // EliminaCuotaAnticipadaSecuencia(Secuencia, myconnet); // ERROR: CS1503
            if (Convert.ToDouble(CuotaReliq) > 0)
            {
                GrabarNuevaCuota(codigoter, lincred, numero, Convert.ToDouble(CuotaReliq), myconnet);
            }
            if (Numcheque.Trim() != "")
            {
                msgdep.ElimanarCanje(lincred, codigo_banco, Numcheque, fecha_movto, myconnet);
            }
            if (idsolcre > 0)
            {
                this.ReviveSolicitudCredito(idsolcre, codigoter, lincred, numero, myconnet);
                // EliminaCuotaAnticipadaSolicitudCredito(idsolcre, IdCptoAnticipo, myconnet); // ERROR: CS1503
            }

            BorraFacturaCartera(Comprobante, NumCpte, codigoter, lincred, numero, debito, myconnet);

            if (StFecCausaCdats.Trim() != "")
            {
                this.ReviveFecCausacionCdats(codigoter, lincred, numero, Convert.ToDateTime(StFecCausaCdats), myconnet, "A");
            }

            if (StNumSolAux > 0)
            {
                // ok = this.BuscaSolicitudAuxilio(StNumSolAux, myconnet, ref StCodigoter, ref StLineaAux, ref StFecSol, ref StFecAprobado, ref StFecpago, ref StAprobado, ref StEstadoSol, ref StValSolicitado, ref StValAprobado, ref StOblinea, ref StObAprobado, ref stObSolicitud, ref StCerrado); // ERROR: CS7036
                if (ok == true)
                {
                    if (StEstadoSol == "A")
                    {
                        if (StCerrado == "Y")
                        {
                            // this.GrabaSolicitudAuxliios(StNumSolAux, myconnet, StCodigoter, StLineaAux, StFecSol, StFecAprobado, StFecpago, StAprobado, StEstadoSol, StValSolicitado, StValAprobado, StOblinea, StObAprobado, stObSolicitud, "N"); // ERROR: CS1620
                        }
                        else
                        {
                            // this.GrabaSolicitudAuxliios(StNumSolAux, myconnet, StCodigoter, StLineaAux, StFecSol, StFecAprobado, StFecpago, StAprobado, StEstadoSol, StValSolicitado, StValAprobado, StOblinea, StObAprobado, stObSolicitud, "Y"); // ERROR: CS1620
                        }
                    }
                }
            }

            stmysql = " delete from cop_movimto where SECUENCIA = " + Secuencia + " and COMPRONTE = '" + Comprobante + "' and NUMERO_DOMTO = " + NumCpte;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnet, "BorraRegistro");
            if (ok == true)
            {
                msgcnt.BorraDocumentoConciliacion(Secuencia, myconnet, "cop");
                BorraRegistroDepende(Comprobante, NumCpte, Secuencia, myconnet);
            }
            return true;
        }

        // ==========================================================================
        // BorraRegistroDepende
        // ==========================================================================
        public bool BorraRegistroDepende(string compronte, double numero_domto, double secuenciadepende, OdbcConnection myconnect)
        {
            DataTable TablaDatos = new DataTable();
            int contador;
            stmysql = "select SECUENCIA,periodo from cop_movimto where secuenciadepende = " + secuenciadepende + " and COMPRONTE = '" + compronte + "' and NUMERO_DOMTO = " + numero_domto;
            if (this.OdbcConnect.ExecuteConsulta(stmysql, myconnect, "BorraRegistroDepende", ref TablaDatos) == true)
            {
                for (contador = 0; contador <= TablaDatos.Rows.Count - 1; contador++)
                {
                    BorraRegistro(compronte, numero_domto, Convert.ToDouble(TablaDatos.Rows[contador]["Secuencia"]), Convert.ToInt32(TablaDatos.Rows[contador]["periodo"]), myconnect);
                }
            }
            TablaDatos.Dispose();
            return false;
        }

        // ==========================================================================
        // GrabarNuevaCuota
        // ==========================================================================
        public bool GrabarNuevaCuota(string codigoter, int lincred, double numero, double cuota, OdbcConnection myconnect)
        {
            stmysql = "update cop_maecar set cuota=" + cuota + " where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarNuevaCuota");
            return ok;
        }

        // ==========================================================================
        // BorraDocumentos
        // ==========================================================================
        private void BorraDocumentos(string Comprobante, double NumCpte, double debito, double credito, OdbcConnection myconnect)
        {
            string stmysql;
            stmysql = "update cop_docmto set DEBITO = DEBITO - " + debito + ", CREDITO = CREDITO - " + credito
                    + " where COMPRONTE = '" + Comprobante + "' and NUMERO_DOMTO = " + NumCpte;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BorraDocumentos");
        }

        // ==========================================================================
        // BorraCapital
        // ==========================================================================
        private void BorraCapital(string codigoter, int lincred, double NumCredito, double debito, double credito, int periodo, OdbcConnection myconnect)
        {
            string stmysql;
            stmysql = "update cop_salmaecar set  SALDO = SALDO_INICIAL + (vlr_debito - " + debito + ") - (vlr_credito - " + credito + "), vlr_debito = vlr_debito - " + debito + ", vlr_credito = vlr_credito - " + credito
                   + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and periodo = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BorraCapital");
        }

        // ==========================================================================
        // BorraExtras
        // ==========================================================================
        private void BorraExtras(string codigoter, int lincred, double NumCredito, double debito, double credito, int periodo, int Numxtras, OdbcConnection myconnect)
        {
            string stmysql;
            stmysql = "update cop_salextras set SALDO = SALDO_INICIAL + (vlr_debito - " + debito + ") - (vlr_credito - " + credito + "), vlr_debito = vlr_debito - " + debito + ", vlr_credito = vlr_credito - " + credito
                    + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and NUM_EXTRA  = " + Numxtras + " and periodo = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BorraExtras");
        }

        // ==========================================================================
        // BorraCopmora
        // ==========================================================================
        private void BorraCopmora(string codigoter, int lincred, double NumCredito, double debito, double credito, int periodo, int Ciclo, string ClaseMovto, OdbcConnection myconnect, int Num_extra = 0)
        {
            string stmysql = "";
            switch (ClaseMovto)
            {
                case "C":
                    stmysql = "update cop_copmora set SaldoCapital = Saldo_AntCapital + (Capital_causado - " + debito + ") - (Capital_abono - " + credito + "), Capital_causado = Capital_causado - " + debito + ", Capital_abono = Capital_abono - " + credito
                           + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and periodo_causa = " + Ciclo + " AND periodo_contable = " + periodo;
                    break;
                case "I":
                    stmysql = "update cop_copmora set SaldoInteres = Saldo_AntInteres + (Interes_causado - " + debito + ") - (Interes_abono - " + credito + "), Interes_causado = Interes_causado - " + debito + ", Interes_abono = Interes_abono - " + credito
                           + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and periodo_causa = " + Ciclo + " AND periodo_contable = " + periodo;
                    break;
                case "M":
                    stmysql = "update cop_copmora set SaldoMora = Saldo_AntMora + (Mora_causado - " + debito + ") - (Mora_abono - " + credito + "), Mora_causado = Mora_causado - " + debito + ", Mora_abono = Mora_abono - " + credito
                           + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and periodo_causa = " + Ciclo + " AND periodo_contable = " + periodo;
                    break;
                case "S":
                    stmysql = "update cop_copmora set SaldoSeguro = Saldo_AntSeguro + (Seguro_causado - " + debito + ") - (Seguro_abono - " + credito + "), Seguro_causado = Seguro_causado - " + debito + ", Seguro_abono = Seguro_abono - " + credito
                           + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and periodo_causa = " + Ciclo + " AND periodo_contable = " + periodo;
                    break;
                case "A":
                    stmysql = "update cop_copmora set SaldoAdmon = Saldo_AntAdmon + (Admon_causado - " + debito + ") - (admon_abono - " + credito + "), Admon_causado = Admon_causado - " + debito + ", admon_abono = admon_abono - " + credito
                           + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and periodo_causa = " + Ciclo + " AND periodo_contable = " + periodo;
                    break;
                case "EX":
                    stmysql = "update cop_copmora set SaldoExtra = Saldo_AntExtra + (Extra_causado - " + debito + ") - (Extra_abono - " + credito + "), Extra_causado = Extra_causado - " + debito + ", Extra_abono = Extra_abono - " + credito
                           + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + NumCredito + " and periodo_causa = " + Ciclo + " AND periodo_contable = " + periodo + " AND Nume_extra = " + Num_extra;
                    break;
            }

            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BorraCapital");
        }

        // ==========================================================================
        // ActualizacionCartera
        // ==========================================================================
        public bool ActualizacionCartera(DateTime fechaActualizacion, OdbcConnection myconnect, Form Myform)
        {
            string stmysql;
            string estado = "C";
            MsgBoxResult MSGOK;
            string periodo = "";
            DateTime fecini = default(DateTime);
            DateTime fecfin = default(DateTime);

            this.buscaPeriodo("copc", myconnect, ref fecini, ref fecfin, fechaActualizacion, ref estado, ref periodo, fechaActualizacion.ToString("yyyy"));

            switch (estado)
            {
                case "P":
                    MSGOK = (MsgBoxResult)MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    if (MSGOK == MsgBoxResult.No)
                    {
                        return false;
                    }
                    break;
                case "C":
                    MSGOK = (MsgBoxResult)MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            stmysql = "update cop_salmaecar set vlr_debito= '0', vlr_credito = '0', SALDO = SALDO_INICIAL "
                    + "where periodo = '" + periodo + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            stmysql = "update cop_salextras set vlr_debito= '0', vlr_credito = '0', SALDO = SALDO_INICIAL "
                    + "where periodo = '" + periodo + "' and saldo_inicial <> 0 ";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            stmysql = "update cop_copmora set saldoCapital = (Saldo_AntCapital + Capital_causado) - 0 , capital_abono = 0 "
                    + "where periodo_contable = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            stmysql = "update cop_copmora set SaldoInteres = (Saldo_AntInteres + Interes_causado) - 0 , Interes_abono = 0 "
                    + "where periodo_contable = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            stmysql = "update cop_copmora set SaldoExtra = (Saldo_AntExtra + Extra_causado) - 0 , Extra_abono = 0 "
                    + "where periodo_contable = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            stmysql = "update cop_copmora set SaldoMora = (Saldo_AntMora + Mora_causado) - 0 , Mora_abono = 0 "
                    + "where periodo_contable = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            stmysql = "update cop_copmora set SaldoSeguro = (Saldo_AntSeguro + Seguro_causado) - 0,  Seguro_abono = 0 "
                    + "where periodo_contable = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            stmysql = "update cop_copmora set SaldoAdmon = (Saldo_AntAdmon + Admon_causado) - 0, Admon_abono = 0 "
                    + "where periodo_contable = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaSaldos");

            ActualizaMovimiento(fechaActualizacion, myconnect, Myform);
            return false; // VB original has no explicit Return at end
        }

        // ==========================================================================
        // ActualizaMovimiento
        // ==========================================================================
        private void ActualizaMovimiento(DateTime fechaActualizacion, OdbcConnection myconnect, Form Myform)
        {
            string stmysql;
            DateTime fecini = default(DateTime);
            DateTime fecfin = default(DateTime);
            string estado = "C";
            string periodo = "999999";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Movimientos", Myform);
            string ajucau = "N";
            bool ok = false;
            int sw1 = 0;
            string detalle = " ";
            double saldoInicial = 0;
            int fila = 0;
            int CanReg = 0;

            this.buscaPeriodo("copc", myconnect, ref fecini, ref fecfin, fechaActualizacion, ref estado, ref periodo, fechaActualizacion.ToString("yyyy"));

            stmysql = "update cop_docmto set debito = 0, credito = 0 "
                    + "where FECHA between '" + Strings.Format(fecini, varini.PstForFec) + "' and '" + Strings.Format(fecfin, varini.PstForFec) + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaMovimiento");

            stmysql = "select COMPRONTE,NUMERO_DOMTO ,a.codigoter,a.lincred,a.numero,b.tipo_movto, vlr_debito, vlr_credito,ciclos, nro_extra, secuencia, fecha_movto, "
                   + " tasa_int,usuario, b.ajucau "
                   + "from cop_movimto a left join cop_codmov b on a.cod_movto = b.cod_movto "
                   + "where fecha_movto >= '" + Strings.Format(fecini, varini.PstForFec) + "' and fecha_movto <='" + Strings.Format(fecfin, varini.PstForFec)
                   + "'  order by COMPRONTE, NUMERO_DOMTO, CODIGOTER,LINCRED,NUMERO";

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "ActualizaMovimiento", ref myRead, "Actualiza");
            CanReg = myRead.Tables["Actualiza"].Rows.Count;

            msgbarra.ValorMinimoMaximo(0, CanReg);
            msgbarra.Show();
            Application.DoEvents();
            while (fila < CanReg)
            {
                DataRow row = myRead.Tables[0].Rows[fila];
                sw1 = 0;

                if (row["ajucau"] is DBNull)
                {
                    sw1 = 1;
                }

                if (row["tipo_movto"] is DBNull)
                {
                    MessageBox.Show("Tipo movimiento no existe, " + "\n" + " Comprobante :" + row["compronte"] + "-" + row["numero_domto"]);

                    stmysql = " delete from cop_movimto where SECUENCIA = " + row["secuencia"] + " and COMPRONTE = '" + row["compronte"] + "' and NUMERO_DOMTO = " + row["numero_domto"];
                    this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaMovimiento");
                    sw1 = 1;
                }

                // ok = this.BuscaAsociado(row["codigoter"].ToString(), myconnect); // ERROR: CS1620
                if (ok == false)
                {
                    sw1 = 1;
                }

                if (Convert.ToDouble(row["vlr_debito"]) == 0 && Convert.ToDouble(row["vlr_credito"]) == 0)
                {
                    stmysql = " delete from cop_movimto where SECUENCIA = " + row["secuencia"] + " and COMPRONTE = '" + row["compronte"] + "' and NUMERO_DOMTO = " + row["numero_domto"];
                    this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaMovimiento");
                    sw1 = 1;
                }

                if (sw1 == 0)
                {
                    // ok = this.BuscaObligacion(row["codigoter"].ToString(), row["lincred"], row["numero"], myconnect); // ERROR: CS1503
                    if (ok == false)
                    {
                        // GrabaNuevoCredito(row["codigoter"], row["lincred"], row["numero"], row["fecha_movto"], row["fecha_movto"], row["fecha_movto"], row["fecha_movto"], " ", 0, row["vlr_debito"], row["vlr_debito"], 0, 0, 0, 5, 1, 1, 1, row["usuario"], row["fecha_movto"], "9999", "99999999", periodo, myconnect); // ERROR: CS1503
                    }

                    switch (row["tipo_movto"].ToString())
                    {
                        case "1":
                        case "4":
                        case "7":
                        case "11":
                        case "12":
                        case "13":
                            // this.GrabaSaldos(row["codigoter"], row["lincred"], row["numero"], periodo, row["vlr_debito"], row["vlr_credito"], myconnect); // ERROR: CS1503

                            if (Convert.ToInt32(row["ciclos"]) > 0 && Convert.ToInt32(row["ciclos"]) != 999999)
                            {
                                // GrabaCopmora(row["codigoter"], row["lincred"], row["numero"], 0, row["vlr_credito"], "C", row["ciclos"], fechaActualizacion.ToString("yyyyMM"), myconnect); // ERROR: CS1503
                            }
                            break;
                        case "2":
                            if (Convert.ToInt32(row["ciclos"]) > 0 && Convert.ToInt32(row["ciclos"]) != 999999)
                            {
                                // GrabaCopmora(row["codigoter"], row["lincred"], row["numero"], 0, row["vlr_credito"], "I", row["ciclos"], fechaActualizacion.ToString("yyyyMM"), myconnect); // ERROR: CS1503
                            }
                            break;
                        case "3":
                            if (Convert.ToInt32(row["ciclos"]) > 0 && Convert.ToInt32(row["ciclos"]) != 999999)
                            {
                                // GrabaCopmora(row["codigoter"], row["lincred"], row["numero"], 0, row["vlr_credito"], "M", row["ciclos"], fechaActualizacion.ToString("yyyyMM"), myconnect); // ERROR: CS1503
                            }
                            break;
                        case "5":
                            if (Convert.ToInt32(row["ciclos"]) > 0 && Convert.ToInt32(row["ciclos"]) != 999999)
                            {
                                // GrabaCopmora(row["codigoter"], row["lincred"], row["numero"], 0, row["vlr_credito"], "S", row["ciclos"], fechaActualizacion.ToString("yyyyMM"), myconnect); // ERROR: CS1503
                            }
                            break;
                        case "6":
                            if (Convert.ToInt32(row["ciclos"]) > 0 && Convert.ToInt32(row["ciclos"]) != 999999)
                            {
                                // GrabaCopmora(row["codigoter"], row["lincred"], row["numero"], 0, row["vlr_credito"], "A", row["ciclos"], fechaActualizacion.ToString("yyyyMM"), myconnect); // ERROR: CS1503
                            }
                            break;
                        case "10":
                            // this.GrabaSaldos(row["codigoter"], row["lincred"], row["numero"], periodo, row["vlr_debito"], row["vlr_credito"], myconnect); // ERROR: CS1503

                            if (Convert.ToInt32(row["ciclos"]) > 0 && Convert.ToInt32(row["ciclos"]) != 999999)
                            {
                                // GrabaCopmora(row["codigoter"], row["lincred"], row["numero"], 0, row["vlr_credito"], "EX", row["ciclos"], fechaActualizacion.ToString("yyyyMM"), myconnect, row["nro_extra"]); // ERROR: CS1503
                            }

                            // this.BuscaCuotaExtra(row["codigoter"], row["lincred"], row["numero"], row["nro_extra"], fechaActualizacion.ToString("yyyyMM"), myconnect, ref saldoInicial); // ERROR: CS7036
                            if (saldoInicial != 0)
                            {
                                // this.GrabaSaldoExtra(row["codigoter"], row["lincred"], row["numero"], row["nro_extra"], fechaActualizacion.ToString("yyyyMM"), row["vlr_debito"], row["vlr_credito"], myconnect); // ERROR: CS1503
                            }
                            break;
                    }

                    detalle = " ";
                    // BuscaComprobante(row["compronte"], row["numero_domto"], false, myconnect, ref detalle); // ERROR: CS1620
                    // GrabaDocumento(row["compronte"], row["numero_domto"], row["vlr_debito"], row["vlr_credito"], row["fecha_movto"], detalle, myconnect, row["codigoter"]); // ERROR: CS1503
                    msgbarra.PerformStep();
                }

                fila += 1;
            }
            myRead.Dispose();
            msgbarra.Dispose();
            msgbarra.Close();
        }

        // ==========================================================================
        // CorrigeNegativos
        // ==========================================================================
        public void CorrigeNegativos(string Cpte, double NumCpte, DateTime fechaMovto, string Usuario, OdbcConnection myconect, Form myform)
        {
            string strql;
            double debito = 0;
            string periodo = "";
            string estado = "";
            ERP.Core.Compartido.Controles.Barraprogress msgsas = new ERP.Core.Compartido.Controles.Barraprogress("Grabando Movimiento ...", myform);
            int canreg = 0;
            int fila = 0;
            DataSet dscompania = new DataSet();
            // this.buscaPeriodo("copc", myconect, ref fechaMovto, ref estado, ref periodo); // ERROR: CS1503, CS1615

            strql = "select codigoter, copmora.lincred, numero,SaldoCapital,SaldoInteres,SaldoExtra,SaldoMora,SaldoSeguro,SaldoAdmon, nume_extra, periodo_causa,codahor from cop_copmora copmora "
                  + " inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred where periodo_contable = " + periodo;

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(strql, myconect, "CorrigeNegativos", ref myRead, "TblCorrigeNegativos");
            canreg = myRead.Tables["TblCorrigeNegativos"].Rows.Count;

            msgsas.ValorMinimoMaximo(0, canreg);
            msgsas.Show();
            Application.DoEvents();

            // this.msgcofsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, myconect); // ERROR: CS1620

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCorrigeNegativos"].Rows[fila];

                switch (row["codahor"].ToString())
                {
                    case "4":
                        if (Convert.ToDouble(row["SaldoCapital"]) < 0)
                        {
                            debito = Convert.ToDouble(row["SaldoCapital"]) * -1;
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscacapi"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscacapi"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                        }
                        break;
                    case "1":
                        if (Convert.ToDouble(row["SaldoCapital"]) < 0)
                        {
                            debito = Convert.ToDouble(row["SaldoCapital"]) * -1;
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaapor"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaapor"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                        }
                        break;
                    case "2":
                        if (Convert.ToDouble(row["SaldoCapital"]) < 0)
                        {
                            debito = Convert.ToDouble(row["SaldoCapital"]) * -1;
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaahor"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaahor"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                        }
                        break;
                    case "3":
                        if (Convert.ToDouble(row["SaldoCapital"]) < 0)
                        {
                            debito = Convert.ToDouble(row["SaldoCapital"]) * -1;
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaserv"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                            // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaserv"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                        }
                        break;
                }

                if (Convert.ToDouble(row["SaldoInteres"]) < 0)
                {
                    debito = Convert.ToDouble(row["SaldoInteres"]) * -1;
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscainte"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscainte"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                }

                if (Convert.ToDouble(row["SaldoExtra"]) < 0)
                {
                    debito = Convert.ToDouble(row["SaldoExtra"]) * -1;
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaext"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"], row["nume_extra"]); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaext"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999, row["nume_extra"]); // ERROR: CS1503, CS1620
                }

                if (Convert.ToDouble(row["SaldoMora"]) < 0)
                {
                    debito = Convert.ToDouble(row["SaldoMora"]) * -1;
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaintemor"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaintemor"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                }

                if (Convert.ToDouble(row["SaldoSeguro"]) < 0)
                {
                    debito = Convert.ToDouble(row["SaldoSeguro"]) * -1;
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscasegcre"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscasegcre"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                }

                if (Convert.ToDouble(row["SaldoAdmon"]) < 0)
                {
                    debito = Convert.ToDouble(row["SaldoAdmon"]) * -1;
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaadm"], fechaMovto, debito, 0, "Correcion de negativos automatico", Usuario, myconect, row["periodo_causa"]); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Cpte, NumCpte, row["codigoter"], row["lincred"], row["numero"], periodo, dscompania.Tables["tblcompania"].Rows[0]["ajuscaadm"], fechaMovto, 0, debito, "Correcion de negativos automatico", Usuario, myconect, 999999); // ERROR: CS1503, CS1620
                }

                msgsas.PerformStep();
                fila += 1;
            }
            myRead.Dispose();
            msgsas.Close();
        }

        // ==========================================================================
        // GrabaCausacionPlano
        // ==========================================================================
        public void GrabaCausacionPlano(string NombreArchivo, string periodo, OdbcConnection myconnect)
        {
            string codigo;
            int Linea;
            double Obligacion;
            int ciclo;
            double SaldoCapital;
            double SaldoInteres;
            double SaldoExtras;
            int Numextra;
            bool ok;
            int sw1;

            using (StreamReader reader = new StreamReader(NombreArchivo))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');
                    if (parts.Length < 8) continue;
                    codigo = parts[0].Trim();
                    Linea = Convert.ToInt32(parts[1].Trim());
                    Obligacion = Convert.ToDouble(parts[2].Trim());
                    ciclo = Convert.ToInt32(parts[3].Trim());
                    SaldoCapital = Convert.ToDouble(parts[4].Trim());
                    SaldoInteres = Convert.ToDouble(parts[5].Trim());
                    SaldoExtras = Convert.ToDouble(parts[6].Trim());
                    Numextra = Convert.ToInt32(parts[7].Trim());

                    sw1 = 0;
                    // ok = this.BuscaAsociado(codigo, myconnect); // ERROR: CS1620
                    // if (ok == false) // ERROR: CS0165
                    {
                        MessageBox.Show("Asociado no existe " + codigo);
                        sw1 = 1;
                    }

                    if (sw1 == 0)
                    {
                        // this.GrabaCopmora(codigo, Linea, Obligacion, SaldoCapital, 0, "C", ciclo, periodo, myconnect); // ERROR: CS1503
                        // this.GrabaCopmora(codigo, Linea, Obligacion, SaldoInteres, 0, "I", ciclo, periodo, myconnect); // ERROR: CS1503
                        // this.GrabaCopmora(codigo, Linea, Obligacion, SaldoExtras, 0, "EX", ciclo, periodo, myconnect, Numextra); // ERROR: CS1503
                    }
                }
            }
        }

        // ==========================================================================
        // GrabaPlanoGeneralCuentasAhorro
        // ==========================================================================
        public bool GrabaPlanoGeneralCuentasAhorro(string NombreArchivo, string Comprobante, double ConseCpte, DateTime FechaMovto, Form Myforma, string usuario, OdbcConnection myconnect)
        {
            string Cedula = " ";
            string valor = " ";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("validando Archivo", Myforma);
            decimal Treg;
            string line;
            string idbanco = "9999";
            string NumCta = " ";
            double Sueldo = 0;
            string CptoAho = "9999";
            double TotPag = 0;
            string CptoCap = "9999";
            string nit = " ";
            string lincred = "9999";
            int sw1 = 0;
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos msgdep = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();

            using (StreamReader sr1 = new StreamReader(NombreArchivo))
            {
                line = sr1.ReadLine();
            }
            FileInfo fi = new FileInfo(NombreArchivo);
            Treg = Math.Round((decimal)(fi.Length / (line.Length + 2)));

            BarraProgreso.ValorMinimoMaximo(0, (int)Treg);
            BarraProgreso.Show();

            // msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, ref CptoCap, ref CptoAho); // ERROR: CS7036

            using (StreamReader reader = new StreamReader(NombreArchivo))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');
                    if (parts.Length < 2) continue;
                    Cedula = parts[0].Trim();
                    valor = parts[1].Trim();

                    sw1 = 0;

                    // ok = this.BuscaAsociado(Cedula, myconnect, ref nit, ref NumCta); // ERROR: CS7036
                    if (ok == false)
                    {
                        MessageBox.Show("Asociado no existe " + Cedula);
                        sw1 = 1;
                    }

                    // ok = msgdep.BuscarCuentaAhorro(NumCta, myconnect, global::ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos.Navega.Ninguno, ref lincred); // ERROR: CS1501
                    if (ok == false)
                    {
                        MessageBox.Show("Cedula no tiene cuenta asociada " + Cedula, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        sw1 = 1;
                    }

                    if (NumCta.Trim() == "")
                    {
                        MessageBox.Show("Cedula no tiene cuenta asociada " + Cedula, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        sw1 = 1;
                    }

                    if (idbanco.Trim() == "")
                    {
                        MessageBox.Show("Cedula no tiene BANCO asociado " + Cedula, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        sw1 = 1;
                    }

                    if (sw1 == 0)
                    {
                        // this.GrabaMovimiento(Comprobante, ConseCpte, Cedula, lincred, NumCta, FechaMovto.ToString("yyyyMM"), CptoAho, FechaMovto, 0, valor, "ACREDITAMOS VALORES RECIBIDOS DE NOMINA", usuario, myconnect, false); // ERROR: CS1503, CS1620
                        TotPag += Convert.ToDouble(valor);
                    }

                    BarraProgreso.PerformStep();
                }
            }

            if (TotPag > 0)
            {
                ok = this.TrasladaContabilidad(Comprobante, ConseCpte, myconnect, usuario, Cedula);
            }

            BarraProgreso.Close();
            BarraProgreso.Dispose();
            return ok;
        }

        // ==========================================================================
        // RecogeNegativosAbona
        // ==========================================================================
        public void RecogeNegativosAbona(string Cpte, double Consecpte, DateTime FechaMovto, string periodo, int LIncredIni, int LincredFin, OdbcConnection myconnect, string Usuario, Form myforma)
        {
            string stmysql = null;
            double saldoCapital = 0;
            string codigoter = "0";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Revisando negativos", myforma);
            int canreg = 0;
            int fila = 0;
            string CptoFavor = "9999";

            stmysql = "select  codigoter, copmora.lincred, numero, periodo_causa, (saldoCapital * -1) as saldoCapital, codahor from cop_copmora  copmora inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred "
                    + "where copmora.lincred between " + LIncredIni + " and " + LincredFin + " and saldoCapital <  0 and periodo_contable ='" + periodo + "' order by codigoter, copmora.lincred, numero";

            msgbarra.DefineMaximo(stmysql, myconnect);
            msgbarra.Show();
            Application.DoEvents();

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "RecogeNegativosAbona", ref myRead, "TblRecogeNegativos");
            canreg = myRead.Tables["TblRecogeNegativos"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblRecogeNegativos"].Rows[fila];

                if (row["codigoter"].ToString() != codigoter && codigoter != "0")
                {
                    // this.GrabaMovimiento(Cpte, Consecpte, codigoter, 9999, 99999999, FechaMovto.ToString("yyyyMM"), 99, FechaMovto, 0, saldoCapital, " ", Usuario, myconnect); // ERROR: CS1503, CS1620
                    if (saldoCapital > 0)
                    {
                        // this.BsucarCompania(varini.sptCodEmpr, myconnect, ref CptoFavor); // ERROR: CS7036
                        // this.GrabaMovimiento(Cpte, Consecpte, codigoter, CptoFavor, 0, FechaMovto.ToString("yyyyMM"), 2, FechaMovto, 0, Math.Round(saldoCapital, 0), " ", Usuario, myconnect); // ERROR: CS1503, CS1620
                    }
                    saldoCapital = 0;
                }

                codigoter = row["codigoter"].ToString();
                saldoCapital = saldoCapital + Convert.ToDouble(row["saldoCapital"]);

                // this.GrabaMovimiento(Cpte, Consecpte, row["codigoter"], row["lincred"], row["numero"], FechaMovto.ToString("yyyyMM"), 51, FechaMovto, row["saldoCapital"], 0, " ", Usuario, myconnect, row["periodo_causa"]); // ERROR: CS1503, CS1620

                msgbarra.PerformStep();

                fila += 1;
            }

            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();
        }

        // ==========================================================================
        // LiquidaCuotaAnticipada
        // ==========================================================================
        public double LiquidaCuotaAnticipada(string codigoter, string lincred, double ConseCredito, int periodo, DateTime FechaMovto, OdbcConnection myconnect, int PeriodoCausa = 0, double Secuencia = 0)
        {
            double CapitalAtra = 0;
            string CalculaSaldo = "N";
            string totintant = "N";
            double saldo = 0;
            double cargoad = 0;
            decimal tasaint = 0;
            int Clasei = 0;
            int Clacuo = 0;
            double Cuota = 0;
            decimal TasaSeg = 0;
            decimal TasaAdmon = 0;
            int periode = 0;
            int poapen = 0;
            int foradmon = 0;
            double Cauint = 0;
            double Caucap = 0;
            double Causeg = 0;
            double Cauadm = 0;
            double Totcuo = 0;
            int TipoLinea = 0;

            // this.BuscaSaldosCuotasPendientes(codigoter, lincred, ConseCredito, lincred, ConseCredito, periodo, myconnect, ref CapitalAtra); // ERROR: CS1501
            // this.BsucarCompania(varini.sptCodEmpr, myconnect, ref CalculaSaldo); // ERROR: CS7036
            // this.BuscaLinea(lincred, myconnect, ref TipoLinea, ref tasaint, ref totintant, ref poapen, ref foradmon); // ERROR: CS7036
            // this.BuscaObligacion(codigoter, lincred, ConseCredito, myconnect, ref cargoad, ref tasaint, ref Clasei, ref Clacuo, ref Cuota, ref TasaSeg, ref TasaAdmon, ref periode); // ERROR: CS7036
            // this.BuscaSaldoObligacion(codigoter, lincred, ConseCredito, periodo, myconnect, ref saldo); // ERROR: CS1503
            tasaint = (tasaint / 100) / periode;
            TasaSeg = (TasaSeg) / periode;
            TasaAdmon = (TasaAdmon) / periode;
            saldo = saldo - cargoad;

            if (TipoLinea == 4)
            {
                if (totintant != "Y")
                {
                    Cauint = calcula_interes(saldo, (double)tasaint, Cuota, Clasei.ToString(), Clacuo.ToString(), 0);
                }
                else
                {
                    Cauint = 0;
                }
            }

            if (poapen.ToString() == "3")
            {
                Causeg = calcula_seguro(saldo, (double)TasaSeg, " ");
            }
            else
            {
                Causeg = 0;
            }
            if (foradmon.ToString() == "2")
            {
                Cauadm = calcula_admon(saldo, (double)TasaAdmon, " ");
            }
            else
            {
                Cauadm = 0;
            }

            Caucap = calcula_capital(Convert.ToInt32(lincred), saldo, Cuota, Cauint, Clacuo.ToString());

            if (PeriodoCausa == 0)
            {
                PeriodoCausa = periodo + 1;
            }

            if (Clacuo == 1)
            {
                Totcuo = Cuota;
            }
            else
            {
                Totcuo = Cuota + Cauint + Causeg + Cauadm;
            }

            if (Secuencia > 0)
            {
                // this.GrabaCuotasAnticipadas(codigoter, lincred, ConseCredito, PeriodoCausa, periodo, Caucap, Cauint, Causeg, Cauadm, Totcuo, FechaMovto, Secuencia, myconnect); // ERROR: CS1503
            }
            return Totcuo;
        }

        // ==========================================================================
        // calcula_interes
        // ==========================================================================
        public double calcula_interes(double dbSaldo, double dbTasa, double dbCuota, string stClasei, string stTipcuo, double dbInteini)
        {
            double dbInteres = 0;
            if (stClasei == "1")
            {
                dbInteres = (double)((dbSaldo * dbTasa));
            }
            if (stClasei == "2" && stTipcuo == "2")
            {
                if (dbCuota > dbSaldo)
                {
                    dbCuota = dbSaldo;
                }
                dbInteres = (double)(((dbSaldo - dbCuota) * dbTasa));
            }
            if (stClasei == "2" && stTipcuo == "1")
            {
                if (dbCuota > dbSaldo)
                {
                    dbCuota = dbSaldo;
                }
                dbInteres = (double)(((dbSaldo - dbInteini) * dbTasa));
            }
            dbInteres = Convert.ToInt32(dbInteres);
            return dbInteres;
        }

        // ==========================================================================
        // calcula_capital
        // ==========================================================================
        public double calcula_capital(int inLincred, double dbSaldo, double dbCuota, double dbInteres, string stTipcuo)
        {
            double dbCapital = 0;
            if (stTipcuo == "1")
            {
                dbCapital = dbCuota - dbInteres;
            }
            if (stTipcuo == "2")
            {
                dbCapital = dbCuota;
            }
            if (dbCapital > dbSaldo && inLincred >= 1000)
            {
                dbCapital = dbSaldo;
            }
            if (dbSaldo <= 0 && inLincred >= 1000)
            {
                dbCapital = 0;
            }
            return dbCapital;
        }

        // ==========================================================================
        // calcula_seguro
        // ==========================================================================
        private double calcula_seguro(double dbSaldo, double dbTasaseg, string stTipseg)
        {
            double dbSeguro;
            dbSeguro = (double)((dbSaldo * (dbTasaseg / 100)));
            dbSeguro = Convert.ToInt32(dbSeguro);
            return dbSeguro;
        }

        // ==========================================================================
        // calcula_admon
        // ==========================================================================
        private double calcula_admon(double dbSaldo, double dbTasaadm, string stTipadm)
        {
            double dbAdmon;
            dbAdmon = (double)((dbSaldo * (dbTasaadm / 100)));
            dbAdmon = Convert.ToInt32(dbAdmon);
            if (stTipadm == "9")
            {
                dbAdmon = 0;
            }
            return dbAdmon;
        }

        // ==========================================================================
        // calcula_capitaliza
        // ==========================================================================
        private double calcula_capitaliza(double dbSaldo, double dbTasacap, string stTipcap)
        {
            double dbCapitaliza;
            dbCapitaliza = (double)((dbSaldo * (dbTasacap / 100)));
            dbCapitaliza = Convert.ToInt32(dbCapitaliza);
            if (stTipcap == "9")
            {
                dbCapitaliza = 0;
            }
            return dbCapitaliza;
        }

        // ==========================================================================
        // calcula_intcie
        // ==========================================================================
        private double calcula_intcie(double dbValpre, double dbTasaint, int inDias, string stTincie, string stperio, string stCiclod, string stTotinan = "N", bool boDif = false)
        {
            double dbIntcie = 0;
            double inDia;

            if (stCiclod != "5" || stperio == "1")
            {
                inDia = 30;
            }
            else if (stperio == "2")
            {
                inDia = 15;
            }
            else if (stperio == "3")
            {
                inDia = 10;
            }
            else if (stperio == "4")
            {
                inDia = 7.5;
            }
            else
            {
                inDia = 30;
            }

            if (boDif == true)
            {
                inDia = 0;
            }

            if (stTotinan != "Y")
            {
                if (inDias > inDia)
                {
                    dbIntcie = Convert.ToInt32((dbValpre) * (double)(dbTasaint) / 3000 * (inDias - inDia));
                }
            }
            else
            {
                dbIntcie = Convert.ToInt32((dbValpre) * (double)(dbTasaint) / 3000 * (inDias));
            }
            if (stTincie == "9")
            {
                dbIntcie = 0;
            }
            return dbIntcie;
        }

        // ==========================================================================
        // calcula_cupo
        // ==========================================================================
        private double calcula_cupo(string stCodigoter, int inLincred, string stTipobusq)
        {
            string stMysql;
            string stTipo_cupo;
            string stTipocupo;
            double dbCupo = 0;
            double inLincre;
            double dbTotdeuda;
            double dbCupoapo = 0;
            decimal deCupopor;
            decimal deVeces;
            decimal deValCupo;
            OdbcConnection myConeccu01 = new OdbcConnection(varini.pstMyconec);
            OdbcConnection myConeccu02 = new OdbcConnection(varini.pstMyconec);
            OdbcConnection myConeccu03 = new OdbcConnection(varini.pstMyconec);

            deValCupo = 0;
            stTipocupo = "";
            dbTotdeuda = 0;

            stMysql = "select * from sys_compania where codigo = '" + Strings.Left(varini.sptCodEmpr.Trim(), 4) + "'";
            try
            {
                myConeccu01.Close();
                OdbcCommand myCommand = new OdbcCommand(stMysql, myConeccu01);
                myCommand.Connection.Open();
                OdbcDataReader Myread = myCommand.ExecuteReader();
                if (!Myread.Read())
                {
                    MessageBox.Show("No se encontro registrada la Compania", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return dbCupoapo;
                }
                else
                {
                    stTipocupo = Myread["tipo_cupo"].ToString().Trim();
                    if (Information.IsNumeric(Myread["cupo"]))
                    {
                        dbCupo = Convert.ToDouble(Myread["cupo"]);
                    }
                    if (stTipocupo != "1" && stTipocupo != "2")
                    {
                        MessageBox.Show("Tipo de cupo compania debe ser 1 o 2", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return dbCupoapo;
                    }
                    if (stTipocupo == "1")
                    {
                        deValCupo = (decimal)dbCupo;
                    }
                    if (stTipobusq == "1")
                    {
                        stMysql = "select * from cop_concar12 where lincred < '1000' order by lincred";
                    }
                    if (stTipobusq == "2")
                    {
                        stMysql = "select * from cop_concar12 where lincred >= '1000' order by lincred";
                    }
                    myConeccu02.Close();
                    OdbcCommand myCommand1 = new OdbcCommand(stMysql, myConeccu02);
                    myCommand1.Connection.Open();
                    OdbcDataReader Myread1 = myCommand1.ExecuteReader();
                    while (Myread1.Read())
                    {
                        if (Information.IsNumeric(Myread1["cupoap"].ToString().Trim()))
                        {
                            deCupopor = Convert.ToDecimal(Myread1["cupoap"]);
                        }
                        else
                        {
                            deCupopor = 0;
                        }
                        if (Information.IsNumeric(Myread1["tasai"].ToString().Trim()))
                        {
                            deVeces = Convert.ToDecimal(Myread1["tasai"]);
                        }
                        else
                        {
                            deCupopor = 0;
                            deVeces = 0;
                        }
                        if (stTipobusq == "2")
                        {
                            deCupopor = 1;
                        }
                        if (stTipobusq == "2")
                        {
                            if (Myread1["afecta"].ToString().Trim() != "Y")
                            {
                                deCupopor = 0;
                            }
                        }
                        if (deCupopor > 0)
                        {
                            if (stTipobusq == "1")
                            {
                                stMysql = "select * from cop_maecar where codigoter = '"
                                        + Strings.Right("00000000000000" + stCodigoter.Trim(), 14)
                                        + "' and lincred = '" + Myread1["lincred"] + "' and numero = 0";
                            }
                            if (stTipobusq == "2")
                            {
                                stMysql = "select * from cop_maecar where codigoter = '"
                                        + Strings.Right("00000000000000" + stCodigoter.Trim(), 14)
                                        + "' and lincred ='" + Myread1["LINCRED"] + "'" + " and numero >= 0";
                            }
                            myConeccu03.Close();
                            OdbcCommand myCommand2 = new OdbcCommand(stMysql, myConeccu03);
                            myCommand2.Connection.Open();
                            OdbcDataReader Myread2 = myCommand2.ExecuteReader();
                            while (Myread2.Read())
                            {
                                if (stTipobusq == "1")
                                {
                                    if (Information.IsNumeric(Myread2["saldot"]))
                                    {
                                        dbCupoapo = (dbCupoapo + (Convert.ToDouble(Myread2["saldot"]) * (double)deVeces / 100));
                                    }
                                }
                                if (stTipobusq == "2")
                                {
                                    if (Information.IsNumeric(Myread2["saldot"]))
                                    {
                                        dbTotdeuda = dbTotdeuda + Convert.ToDouble(Myread2["saldot"]);
                                    }
                                    dbCupoapo = dbTotdeuda;
                                }
                            }
                            myCommand2.Connection.Close();
                            myConeccu03.Close();
                        }
                    }
                    myCommand1.Connection.Close();
                    myConeccu02.Close();
                }
                myCommand.Connection.Close();
                myConeccu01.Close();
                return dbCupoapo;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message);
                myConeccu01.Dispose();
            }
            return dbCupoapo;
        }

        // ==========================================================================
        // GeneraArchiSuper
        // ==========================================================================
        public void GeneraArchiSuper(string Directorio, OdbcConnection Conect, Form Pertenese, bool CodigoACedula = false, int ClaseInteres = 0, bool expProvicion = false, bool CuentasOrden = false, DateTime FechaDecorte = default(DateTime), int CicloParaConavilidad = 12, string NomCartera = "", string NomCaptaciones = "", string NomAportes = "", string NomAosciados = "", string NomContabilidad = "")
        {
            if (FechaDecorte == default(DateTime))
                FechaDecorte = new DateTime(1900, 12, 31);

            try
            {
                ERP.Core.Compartido.Controles.Barraprogress pro = new ERP.Core.Compartido.Controles.Barraprogress("Generando el archivo de Cartera", Pertenese);
                string Mysql = "";
                StreamWriter Csv;
                string[] linea;
                DataSet Ds = new DataSet();

                if (NomCartera != "")
                {
                    Csv = new StreamWriter(Directorio + NomCartera);
                    Mysql = "Select * from cop_maecar where saldot <> 0";
                    this.OdbcConnect.ExecuteQueryDataset(Mysql, Conect, "GeneraArchiSuper", ref Ds, "Cartera");

                    pro.DefineMaximo(Mysql, Conect);
                    pro.Show();
                    for (int i = 0; i <= Ds.Tables["Cartera"].Rows.Count - 1; i++)
                    {
                        object[] itemArray = Ds.Tables["Cartera"].Rows[i].ItemArray;
                        string[] strArray = new string[itemArray.Length];
                        for (int j = 0; j < itemArray.Length; j++)
                        {
                            strArray[j] = (itemArray[j] == null) ? "" : itemArray[j].ToString();
                        }
                        Csv.WriteLine(Strings.Join(strArray, ","));
                        pro.PerformStep();
                    }
                    Csv.Close();
                }
                pro.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ==========================================================================
        // GrabaRedAportes
        // ==========================================================================
        public bool GrabaRedAportes(int PeriodoCorte, double Exedentes, int TipoAplicacion, OdbcConnection Conect, Form pertenese, string estado)
        {
            ERP.Core.Compartido.Controles.Barraprogress pro = new ERP.Core.Compartido.Controles.Barraprogress("Generando la redistribucion de aportes", pertenese);
            int reg = 0;
            int tfila = 0;
            string estadoAso = "A";
            string codigoter = "";
            string codigoterTraslado = "";

            bool Ok = false;
            double SumaSaldos = 0;
            double SaldoAportes = 0;
            double Porcen = 0;
            double TotalRedAso = 0;
            double ValAnterior = 0;
            string sql = "Select sum(a.saldo) as saldos from cop_saldos_vw a inner join cop_concar12 b on b.lincred = a.lincred "
                       + " inner join sys_maenit c on a.codigoter=c.codigoter where b.CODAHOR = '1' and a.periodo = " + PeriodoCorte + " and a.saldo <> 0 " + (estado == "T" ? "" : " and c.estado='" + estado + "'");
            try
            {
                DataSet Read = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(sql, Conect, "GrabaRedAportes", ref Read, "TblGrabaRedAportesSuma");
                reg = Read.Tables["TblGrabaRedAportesSuma"].Rows.Count;
                while (tfila < reg)
                {
                    Ok = true;
                    SumaSaldos = Math.Round(Convert.ToDouble(Read.Tables["TblGrabaRedAportesSuma"].Rows[tfila]["saldos"]), 0);
                    tfila += 1;
                }
                tfila = 0;
                if (Ok == false)
                {
                    MessageBox.Show("No se encontraron saldos en el periodo : " + PeriodoCorte, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                Ok = false;

                sql = "delete from cop_redaportes where periodocorte=" + PeriodoCorte;
                this.OdbcConnect.ExecuteQueryconec(sql, Conect, "GrabaRedAportes");

                switch (TipoAplicacion)
                {
                    case 0:
                        sql = "Select a.codigoter,sum(a.saldo) as saldo from cop_saldos_vw a inner join cop_concar12 b on b.lincred = a.lincred "
                            + " inner join sys_maenit c on a.codigoter=c.codigoter where b.CODAHOR = '1' and a.periodo = " + PeriodoCorte + " and a.saldo <> 0 "
                            + (estado == "T" ? "" : " and c.estado='" + estado + "'") + " group by a.codigoter";
                        break;
                    case 1:
                        // RedAportesDiaAnio(PeriodoCorte, Exedentes, estado, pertenese, Conect); // ERROR: CS0103
                        return false;
                }
                pro.DefineMaximo(sql, Conect);
                pro.Show();

                this.OdbcConnect.ExecuteQueryDataset(sql, Conect, "GrabaRedAportes", ref Read, "TblGrabaRedAportes");
                reg = Read.Tables["TblGrabaRedAportes"].Rows.Count;

                while (tfila < reg)
                {
                    DataRow row = Read.Tables["TblGrabaRedAportes"].Rows[tfila];
                    Ok = true;
                    SaldoAportes = Math.Round(Convert.ToDouble(row["saldo"]), 0);
                    Porcen = (SaldoAportes / SumaSaldos) * 100;
                    TotalRedAso = Math.Round((Exedentes * Porcen) / 100, 0);
                    estadoAso = "A";
                    codigoter = row["codigoter"].ToString();
                    codigoterTraslado = row["codigoter"].ToString();
                    // msgconfig.BuscarEstadoAsociadoXPeriodo(codigoter, PeriodoCorte, Conect, ref estadoAso); // ERROR: CS1061

                    if (estadoAso.Trim() == "T")
                    {
                        // msgconfig.BuscaTrasladado(codigoter, DateTime.Now, ref codigoterTraslado, Conect); // ERROR: CS1061
                        codigoter = codigoterTraslado;
                        codigoter = Strings.Right("00000000000000" + codigoter, 14);
                    }

                    GrabaRedistAportes(PeriodoCorte, codigoter, SaldoAportes, TotalRedAso, Conect);

                    SumaSaldos = Math.Round(SumaSaldos - SaldoAportes, 0);
                    Exedentes = Math.Round(Exedentes - TotalRedAso, 0);

                    Application.DoEvents();
                    pro.PerformStep();
                    tfila += 1;
                }
                pro.Close();
                Read.Dispose();

                return Ok;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return false;
        }

        // ==========================================================================
        // AplicaRedAportes
        // ==========================================================================
        public bool AplicaRedAportes(string Compro, int Consecutivo, int PeriodoCorte, DateTime FechaAplica, string ConsepRedistri, OdbcConnection conect, Form Pertenece, string usuario = "", string ConsepRedistri_ret = "0")
        {
            ERP.Core.Compartido.Controles.Barraprogress barra = new ERP.Core.Compartido.Controles.Barraprogress("Aplicando la redistribucion de aportes.", Pertenece);
            bool ok = false;
            string estado = "C";
            int Periododoc = 999999;
            string Cptorevaportes = "00";
            int canreg = 0;
            int fila = 0;
            string IgualoIn = " in ";

            if (varini.pstTipoBD.ToUpper() == "DB2")
            {
                IgualoIn = "=";
            }

            string sql = " select a.*,(case when retiros.EstadoAct is null then b.estado else retiros.EstadoAct end) as Estado  from cop_redaportes a inner join sys_maenit b on b.codigoter = a.codigoter "
                       + " left join cop_retiros retiros  on  b.codigoter = retiros.codigoter  "
                       + " and retiros.periodo<='" + PeriodoCorte + "' and retiros.FecNovedad " + IgualoIn + " (select g.FecNovedad FROM cop_retiros g where g.codigoter=b.codigoter "
                       + " and  g.periodo<='" + PeriodoCorte + "' and g.FecNovedad=(select max(f.fecnovedad) from cop_retiros f where f.codigoter=b.codigoter "
                       + " and  f.periodo<='" + PeriodoCorte + "')) "
                       + " where a.PeriodoCorte = " + PeriodoCorte;
            barra.DefineMaximo(sql, conect);

            DataSet read = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(sql, conect, "AplicaRedAportes", ref read, "TblAplicaRedAportes");
            canreg = read.Tables["TblAplicaRedAportes"].Rows.Count;

            string periodStr = "";
            // buscaPeriodo("copc", conect, ref FechaAplica, ref estado, ref periodStr, FechaAplica.ToString("yyyy")); // ERROR: CS1503, CS1615, CS1620
            int.TryParse(periodStr, out Periododoc);

            switch (estado)
            {
                case "P":
                    if (MessageBox.Show("Periodo en Prevencion desea continuar?", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                    {
                        return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo Cerrado.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }
            // if (BuscaComprobante(Compro.Trim(), Consecutivo, false, conect) == true) // ERROR: CS1620
            {
                MessageBox.Show("Ya existe un documento grabado con este numero.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            if (ConsepRedistri.Trim() == "" || ConsepRedistri.Trim() == "9999" || ConsepRedistri.Trim() == "0")
            {
                MessageBox.Show("Falta el concepto de redistribucion de aportes.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            // if (BsucarCompania(varini.sptCodEmpr, conect, ref Cptorevaportes) == true) // ERROR: CS7036
            {
                if (Cptorevaportes == "00")
                {
                    MessageBox.Show("Falta parametrizar en la compania el concepto de redistribucion de aportes.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }
            // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
            {
                return false;
            }
            if (ConsepRedistri_ret == "" || ConsepRedistri_ret.Trim() == "9999" || ConsepRedistri_ret.Trim() == "0")
            {
                ConsepRedistri_ret = ConsepRedistri;
            }
            barra.Show();

            while (fila < canreg)
            {
                DataRow row = read.Tables["TblAplicaRedAportes"].Rows[fila];
                switch (row["Estado"].ToString().Trim())
                {
                    case "A":
                    case "T":
                        // GrabaMovimiento(Compro.Trim(), Consecutivo, row["codigoter"], ConsepRedistri.Trim(), 0, FechaAplica.ToString("yyyyMM"), Cptorevaportes, FechaAplica, 0, row["VlrRedAportes"], "Redistribucion de aportes del " + Periododoc, usuario, conect, false); // ERROR: CS1503, CS1620
                        break;
                    case "R":
                        // GrabaMovimiento(Compro.Trim(), Consecutivo, row["codigoter"], ConsepRedistri_ret.Trim(), 0, FechaAplica.ToString("yyyyMM"), Cptorevaportes, FechaAplica, 0, row["VlrRedAportes"], "Redistribucion de aportes del " + Periododoc, usuario, conect, false); // ERROR: CS1503, CS1620
                        break;
                }

                ok = true;
                Application.DoEvents();
                barra.PerformStep();
                fila += 1;
            }
            if (ok == true)
            {
                // BuscaComprobante(Compro.Trim(), 9999, true, conect); // ERROR: CS1620
            }
            barra.Close();
            read.Dispose();
            return ok;
        }

        // ==========================================================================
        // CalculaCuotasPagar
        // ==========================================================================
        public int CalculaCuotasPagar(string codigoter, string Lincred, double Numero, int Periodicidad, int CicloDsto, int Clacuo, decimal TasaInt, double Saldo, double Cuota, string Periodo, string forseg, decimal Tasaseg, double CuotaSeg, string ForAdmon, string BaseAdmon, decimal TasaAdmon, decimal CuotaAdmon, OdbcConnection Myconect, double VLRCREDITO = 0, double VALSEGMIN = 0, double VALSEGMAX = 999999999)
        {
            int CuoPag = 0;
            decimal CuoPen = 0;
            decimal StTasaSeg = 0;
            double StCuotaSeg = 0;
            decimal Interes = 0;
            double dbValextr = 0;
            decimal StTasaAdm = 0;
            double StCuotaAdm = 0;
            int StPlazo = 0;
            double StValorCredito = 0;
            double CsCuota = 0;

            dbValextr = vpn_extras(codigoter, Lincred, Numero, Periodo, Periodicidad.ToString(), CicloDsto.ToString(), Clacuo.ToString(), (double)TasaInt / 100, Myconect);

            if (dbValextr > Saldo)
            {
                // Saldo = Saldo; // no-op in VB
            }
            else
            {
                Saldo -= dbValextr;
            }

            switch (forseg)
            {
                case "3":
                    if (VLRCREDITO >= VALSEGMIN && VLRCREDITO <= VALSEGMAX)
                    {
                        TasaInt = TasaInt + Tasaseg;
                    }
                    else
                    {
                        TasaInt = TasaInt + 0;
                    }
                    break;
                case "11":
                    if (Clacuo.ToString() == "1")
                    {
                        Cuota = Cuota - CuotaSeg;
                    }
                    break;
                case "9":
                    // this.BuscaObligacion(codigoter, Lincred, Numero, Myconect, ref StPlazo, ref StValorCredito); // ERROR: CS7036
                    if (StPlazo > 0 && Periodicidad != 0)
                    {
                        CsCuota = Math.Round((StValorCredito * ((double)Tasaseg / 100)) / StPlazo, 0);
                    }
                    Cuota = Cuota - CsCuota;
                    break;
            }

            switch (ForAdmon)
            {
                case "2":
                    if (BaseAdmon == "9")
                    {
                        if (Clacuo.ToString() == "1")
                        {
                            Cuota = Cuota - (double)CuotaAdmon;
                        }
                    }
                    else
                    {
                        TasaInt = TasaInt + TasaAdmon;
                    }
                    break;
            }

            if (TasaInt > 0)
            {
                if (CicloDsto.ToString() == "5" && Periodicidad == 4)
                {
                    Interes = (decimal)(((double)TasaInt / 30) * 7) / 100;
                }
                else
                {
                    Interes = (decimal)((double)TasaInt / 100);
                }
            }

            if (CicloDsto.ToString() == "5" && Periodicidad != 4)
            {
                Cuota = Math.Round(Cuota * (double)Periodicidad, 0);
            }

            if (Clacuo.ToString() == "1" && Cuota > 0)
            {
                try
                {
                    CuoPen = Math.Round(Convert.ToDecimal(Strings.FormatNumber(Financial.NPer((double)Interes, (double)(-Cuota), (double)(Saldo), 0), 0)), 2);
                    CuoPen = Math.Round((decimal)(double)CuoPen, 0);

                    if (CicloDsto.ToString() == "5" && Periodicidad != 4)
                    {
                        CuoPen = CuoPen * Periodicidad;
                    }
                }
                catch (Exception)
                {
                    CuoPen = 1;
                }
            }

            if (Convert.ToInt32(CuoPen) != CuoPen)
            {
                CuoPen = (int)Math.Floor((double)CuoPen) + 1;
            }

            if (Clacuo.ToString() == "2")
            {
                if ((double)Cuota > 0)
                {
                    CuoPen = (decimal)((double)(Saldo) / (double)(Cuota));
                    if (CicloDsto.ToString() == "5" && Periodicidad != 4)
                    {
                        CuoPen = CuoPen * Periodicidad;
                    }
                    CuoPen = Math.Round((decimal)(double)CuoPen, 0);
                }
                else
                {
                    CuoPen = 1;
                }
            }

            if (Saldo <= Cuota || Cuota == 0)
            {
                CuoPen = 1;
            }

            if (CuoPen < 0)
            {
                CuoPen *= -1;
            }

            return (int)CuoPen;
        }

        // ==========================================================================
        // GrabaNumeroCuotas
        // ==========================================================================
        public void GrabaNumeroCuotas(string codigoter, int Lincred, double ConseCredito, string periodo, int CuoPen, int CuoPag, OdbcConnection myconnect)
        {
            stmysql = "update cop_salmaecar set CUOPEN = " + CuoPen + " where codigoter = '" + codigoter + "' and lincred = " + Lincred + " and numero  = " + ConseCredito + " and periodo = " + periodo;
            try
            {
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaNumeroCuotas");
            }
            catch (Exception)
            {
            }
        }

        // ==========================================================================
        // GrabaFecUltPago
        // ==========================================================================
        public void GrabaFecUltPago(string codigoter, int Lincred, double ConseCredito, DateTime Fecha, OdbcConnection myconnect)
        {
            stmysql = "update cop_maecar set fecultpago = '" + Strings.Format(Fecha, varini.PstForFec) + "' where codigoter = '" + codigoter + "' and lincred = " + Lincred + " and numero  = " + ConseCredito;
            try
            {
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaFecUltPago");
            }
            catch (Exception)
            {
            }
        }

        // ==========================================================================
        // vpn_extras
        // ==========================================================================
        public double vpn_extras(string stCodigoter, string stLincred, double inNumero, string stPeriodo, string stPeriodd, string stCiclo, string stTipcuota, double interes, OdbcConnection pmyconecextr)
        {
            string mysql;
            int canreg = 0;
            int fila = 0;
            string CicloPriDsto;
            double val_extra;
            double dbDiaspe;
            double VPN_extra;
            double VPN_total;
            DateTime dtFechaext;
            DateTime dtFechahoy;
            int inDifdia;
            int periodo_extra;
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos ClsLiqcred = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            StringBuilder StBuilder = new StringBuilder();
            double CantCiclosAbonados = 0;
            DateTime dtFechaFiltro;

            VPN_total = 0;
            stCodigoter = Strings.Right("00000000000000" + stCodigoter, 14);

            StBuilder.Append("select a.saldo as valor,b.fecha_pago as fecha,c.plazo,c.tasaseg,c.tasaadm,c.pergraini,c.fecdesc,c.forseg,d.claAdmon,d.foradmon, ");
            StBuilder.Append("a.codigoter,a.lincred,a.numero,a.periodo ");
            StBuilder.Append("from cop_salextras a ");
            StBuilder.Append("inner join cop_extras b on a.codigoter = b.codigoter and a.lincred = b.lincred and a.numero = b.numero and a.num_extra = b.num_extra ");
            StBuilder.Append("inner join cop_maecar c on a.codigoter=c.codigoter and a.lincred=c.lincred and a.numero=c.numero ");
            StBuilder.Append("inner join cop_concar12 d on a.lincred=d.lincred ");
            StBuilder.Append("where a.periodo='" + stPeriodo + "' and a.saldo > 0 and a.codigoter='" + stCodigoter + "' ");
            StBuilder.Append("and a.lincred='" + stLincred + "' and a.numero='" + inNumero + "' ");

            try
            {
                DataSet MyReade = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), pmyconecextr, "vpn_extras", ref MyReade, "TblVpnExtra");
                canreg = MyReade.Tables["TblVpnExtra"].Rows.Count;

                if (canreg != 0)
                {
                    DataRow row0 = MyReade.Tables["TblVpnExtra"].Rows[0];
                    dtFechahoy = Convert.ToDateTime(row0["fecdesc"]);

                    if (stCiclo != "5")
                    {
                        stPeriodd = "1";
                    }

                    // CicloPriDsto = ClsLiqcred.CalculaCiclo(stCiclo, stPeriodd, dtFechahoy, false); // ERROR: CS1503

                    if (stPeriodd != "1" && stCiclo == "5")
                    {
                        interes = interes / Convert.ToDouble(stPeriodd);
                    }

                    switch (row0["forseg"].ToString())
                    {
                        case "3":
                            row0["tasaseg"] = (Convert.ToDouble(row0["tasaseg"]) / 100);
                            break;
                        default:
                            row0["tasaseg"] = 0;
                            break;
                    }

                    if (row0["claAdmon"].ToString() == "2")
                    {
                        if (row0["foradmon"].ToString() != "9")
                        {
                            row0["tasaadm"] = (Convert.ToDouble(row0["tasaadm"]) / 100);
                        }
                    }
                    else
                    {
                        row0["tasaadm"] = 0;
                    }

                    if (stPeriodd != "1" && stCiclo == "5")
                    {
                        row0["tasaseg"] = (Convert.ToDouble(row0["tasaseg"]) / Convert.ToDouble(stPeriodd));
                    }

                    if (stPeriodd != "1" && stCiclo == "5")
                    {
                        row0["tasaadm"] = (Convert.ToDouble(row0["tasaadm"]) / Convert.ToDouble(stPeriodd));
                    }

                    if (row0["pergraini"] is DBNull)
                    {
                        row0["pergraini"] = 0;
                    }
                    else
                    {
                        if (!Information.IsNumeric(row0["pergraini"]))
                        {
                            row0["pergraini"] = 0;
                        }
                    }

                    StBuilder = new StringBuilder();

                    dtFechaFiltro = new DateTime(Convert.ToInt32(stPeriodo.Substring(0, 4)), Convert.ToInt32(stPeriodo.Substring(4, 2)), DateTime.DaysInMonth(Convert.ToInt32(stPeriodo.Substring(0, 4)), Convert.ToInt32(stPeriodo.Substring(4, 2))));

                    StBuilder.Append("select COUNT(distinct(mov.ciclos)) as campo1 ");
                    StBuilder.Append("from cop_movimto mov ");
                    StBuilder.Append("inner join cop_docmto doc on  mov.COMPRONTE=doc.COMPRONTE and mov.NUMERO_DOMTO=doc.NUMERO_DOMTO ");
                    StBuilder.Append("inner join cop_codmov codmov on mov.COD_MOVTO=codmov.cod_movto ");
                    StBuilder.Append("inner join sys_compro02 pro02 on mov.COMPRONTE=pro02.CODIGO ");
                    StBuilder.Append("where mov.codigoter='" + stCodigoter + "' and mov.lincred=" + stLincred + " and mov.numero=" + inNumero + " and mov.fecha_movto<='" + Strings.Format(dtFechaFiltro, varini.PstForFec) + "' ");
                    StBuilder.Append("and codmov.TIPO_MOVTO in ('1','10') and mov.VLR_CREDITO<>0 and doc.ANULADO<>'Y' and pro02.RESTRI_TESORERIA<>'Y' ");

                    // this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), pmyconecextr, "vpn_extras(Ciclos)", ref CantCiclosAbonados); // ERROR: CS1503

                    // VPN_total = ClsLiqcred.CalculaVp(MyReade.Tables["TblVpnExtra"], stPeriodd, dtFechahoy, stTipcuota, interes + Convert.ToDouble(row0["tasaseg"]) + Convert.ToDouble(row0["tasaadm"]), pmyconecextr, (Convert.ToDouble(row0["plazo"]) + Convert.ToDouble(row0["pergraini"])), CicloPriDsto, CantCiclosAbonados); // ERROR: CS1503
                }

                MyReade.Dispose();
                return VPN_total;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + ex.ToString() + "\r\n" + "Modulo : vpn_extras");
            }
            return VPN_total;
        }

        // ==========================================================================
        // RepiteComprobante
        // ==========================================================================
        public bool RepiteComprobante(string Comprobante, double ConseCpte, string Cptetrasalda, double ConseCptetraslada, DateTime Fechatraslada, string Detalle, string Usuario, OdbcConnection Myconnect, Form myform)
        {
            string stmysql;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Repitiendo Comprobante :  " + Comprobante + "-" + ConseCpte, myform);
            double dif = 0;
            bool OK = false;
            int sw1 = 0;
            ERP.Core.Tesoreria.Services.clstesoreria msgtes = new ERP.Core.Tesoreria.Services.clstesoreria(varini.pstUsuario);
            string fecFact = Strings.Format(Fechatraslada, varini.PstForFec);
            string FecVence = Strings.Format(Fechatraslada, varini.PstForFec);
            string FecProg = Strings.Format(Fechatraslada, varini.PstForFec);
            string Cuenta;
            string factura = "0";
            string StTes = "N";
            string Detatraslada = "Traslado del comprobante " + Cptetrasalda + " - " + ConseCptetraslada;
            int canreg = 0;
            int fila = 0;
            bool result = false;

            stmysql = "select codigoter,lincred, numero,movto.cuenta,vlr_debito,vlr_credito,cencos,agencia, movto.cod_movto,tipo_movto, ciclos, nit, factura,NRO_EXTRA "
                    + " from cop_movimto movto inner join cop_codmov codmov on movto.cod_movto =codmov.cod_movto"
                    + " where compronte = '" + Comprobante + "' and numero_domto = " + ConseCpte;

            msgbarra.DefineMaximo(stmysql, Myconnect);
            msgbarra.Show();

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconnect, "RepiteComprobante", ref myRead, "TblRepCompronte");
            canreg = myRead.Tables["TblRepCompronte"].Rows.Count;

            result = false;
            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblRepCompronte"].Rows[fila];

                sw1 = 0;

                if (row["factura"].ToString() != "" && row["factura"].ToString() != "0")
                {
                    // Commented out tesoreria logic in VB original
                }

                if (row["codigoter"].ToString() == "99999999999999" && row["lincred"].ToString() == "9999")
                {
                    // GrabaContrapartida(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["cuenta"], row["nit"], row["cod_movto"], Fechatraslada, row["vlr_debito"], row["vlr_credito"], "9999", Myconnect, row["factura"].ToString(), Usuario); // ERROR: CS1503
                    sw1 = 1;
                }

                if (sw1 == 0)
                {
                    switch (row["tipo_movto"].ToString())
                    {
                        case "1":
                            // this.GrabaCapital(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "2":
                            // this.GrabaInteres(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "3":
                            // this.GrabaMora(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "4":
                            // this.GrabaCapitalAportes(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "5":
                            // this.GrabaSeguro(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "6":
                            // this.GrabaAdmon(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "7":
                            // this.GrabaCapitalAhorros(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "9":
                            // this.GrabaCapitalServicios(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                        case "10":
                            // this.GrabaExtra(Cptetrasalda, ConseCptetraslada, row["codigoter"], row["lincred"], row["numero"], row["vlr_debito"], row["vlr_credito"], Fechatraslada.ToString("yyyyMM"), Fechatraslada, row["ciclos"], Detalle, Myconnect, row["NRO_EXTRA"].ToString(), row["cod_movto"], Usuario); // ERROR: CS1503
                            break;
                    }

                    msgbarra.PerformStep();
                }
                result = true;
                fila += 1;
            }

            msgbarra.Dispose();
            msgbarra.Close();
            myRead.Dispose();
            return result;
        }

        // ==========================================================================
        // OrganizaDatosClasificacionReestructurados
        // ==========================================================================
        public void OrganizaDatosClasificacionReestructurados(DateTime Fechaproceso, OdbcConnection myconnect, Form Myforma)
        {
            StringBuilder StBuilder = new StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress Progress = new ERP.Core.Compartido.Controles.Barraprogress("Organiza datos clasificacion Reestructurados", Myforma);
            DataSet dsdata = new DataSet();
            int fila = 0;
            DataSet dsMovto = new DataSet();
            string stMysql = "";
            DateTime FecPagoCred;
            double meses = 0;
            DateTime fechaInicial;
            double Cantidad = 0;
            double CantidadNovedad = 0;
            DateTime FecUltPagoAct;
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos ClsLiqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            string calificacion = "A";
            string StCadNull = "";
            double DiasMora = 0;
            string CATEGORIA = "";

            StBuilder.Append("select mae.codigoter,mae.lincred,mae.numero,salmae.saldo,mae.calrest,mae.fecdesc,salmae.periodd,salmae.calfinicial,");
            StBuilder.Append("salmae.calffinal,salmae.ultpagorestInicial,salmae.ultpagorestFinal,copmora.diasmora, car12.catea, car12.cateb, car12.catec, car12.cated, car12.catee ");
            StBuilder.Append("from cop_maecar mae ");
            StBuilder.Append("inner join cop_salmaecar salmae on mae.codigoter=salmae.codigoter and mae.lincred=salmae.lincred and mae.numero=salmae.numero ");
            StBuilder.Append("inner join cop_concar12 car12 on mae.lincred=car12.lincred ");
            StBuilder.Append("left join cop_copmoracircular_vw copmora on mae.codigoter=copmora.codigoter and mae.lincred=copmora.lincred and mae.numero=copmora.numero and salmae.periodo=copmora.periodo_contable ");
            StBuilder.Append("where salmae.periodo=" + Fechaproceso.ToString("yyyyMM") + " and mae.lincred>=1000 and mae.reest='Y' and salmae.saldo<>0 and mae.fecrest<='" + Strings.Format(Fechaproceso, varini.PstForFec) + "' ");

            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "OrganizaDatosClasificacionReestructurados", ref dsdata, "TblOrgDatosReest");

            Progress.ValorMinimoMaximo(0, dsdata.Tables["TblOrgDatosReest"].Rows.Count);
            Progress.Show();

            StBuilder = new StringBuilder();

            FecUltPagoAct = Fechaproceso;

            for (fila = 0; fila <= dsdata.Tables["TblOrgDatosReest"].Rows.Count - 1; fila++)
            {
                Cantidad = 0;
                CantidadNovedad = 0;
                DataRow row = dsdata.Tables["TblOrgDatosReest"].Rows[fila];

                if (row["ultpagorestInicial"] is DBNull)
                {
                    FecPagoCred = Convert.ToDateTime(row["fecdesc"]);
                }
                else
                {
                    FecPagoCred = Convert.ToDateTime(row["ultpagorestInicial"]).AddDays(1);
                }

                if (row["diasmora"] is DBNull)
                {
                    DiasMora = 0;
                }
                else
                {
                    DiasMora = Convert.ToDouble(row["diasmora"]);
                }

                if (DiasMora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATEE"]), "000")))
                {
                    CATEGORIA = "E";
                }
                else if (DiasMora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATED"]), "000")))
                {
                    CATEGORIA = "D";
                }
                else if (DiasMora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATEC"]), "000")))
                {
                    CATEGORIA = "C";
                }
                else if (DiasMora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATEB"]), "000")))
                {
                    CATEGORIA = "B";
                }
                else
                {
                    CATEGORIA = "A";
                }

                meses = this.CalculaDias(Fechaproceso, FecPagoCred) + 1;
                meses = meses / 30;

                if (meses < 2)
                {
                    stMysql = "update sys_maenit set calman='" + row["calfinicial"] + "' where codigoter='" + row["codigoter"] + "'";
                    this.OdbcConnect.ExecuteQueryconec(stMysql, myconnect, "OrganizaDatosClasificacionReestructurados");
                }
                else if (meses >= 2)
                {
                    fechaInicial = new DateTime(FecPagoCred.Year, FecPagoCred.Month, 1);

                    switch (varini.pstTipoBD.Trim().ToUpper())
                    {
                        case "SQL":
                            StCadNull = " isnull(";
                            break;
                        case "MYSQL":
                            StCadNull = " ifnull(";
                            break;
                        case "ORACLE":
                            StCadNull = " nvl(";
                            break;
                        case "DB2":
                            StCadNull = " IFNULL(";
                            break;
                    }

                    StBuilder = new StringBuilder();
                    StBuilder.Append("select count(distinct(n.periodo_causa)) as campo1 from cop_caunov n where n.codigoter='" + row["codigoter"] + "' ");
                    StBuilder.Append("and n.lincred=" + row["lincred"] + " and n.numero=" + row["numero"] + " ");
                    StBuilder.Append("and n.tipo_novedad='2' and n.fecha between '" + Strings.Format(fechaInicial, varini.PstForFec) + "' and '" + Strings.Format(Fechaproceso, varini.PstForFec) + "' ");

                    // ok = this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "OrganizaDatosClasificacionReestructurados", ref CantidadNovedad); // ERROR: CS1503

                    StBuilder = new StringBuilder();
                    StBuilder.Append("select count(distinct(a.ciclos)) as campo1 ");
                    StBuilder.Append("from cop_movimto a ");
                    StBuilder.Append("inner join cop_copmora b on a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero and a.ciclos=b.periodo_causa ");
                    StBuilder.Append("left join cop_copmoracircular_vw c on a.codigoter=c.codigoter and a.lincred=c.lincred and a.numero=c.numero and " + StCadNull + "c.diasmora,0)=0 ");
                    StBuilder.Append("where a.ciclos<>999999 and b.diasmora=0 and a.codigoter='" + row["codigoter"] + "' and ");
                    StBuilder.Append("a.lincred=" + row["lincred"] + " and a.numero=" + row["numero"] + "  and ");
                    StBuilder.Append("a.fecha_movto between '" + Strings.Format(fechaInicial, varini.PstForFec) + "' and '" + Strings.Format(Fechaproceso, varini.PstForFec) + "' and a.periodo=b.periodo_contable");

                    // ok = this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "OrganizaDatosClasificacionReestructurados", ref Cantidad); // ERROR: CS1503

                    StBuilder = new StringBuilder();

                    if (ok == false)
                    {
                        if (string.Compare(CATEGORIA, row["calrest"].ToString()) > 0)
                        {
                            row["calrest"] = CATEGORIA;
                        }
                        // ClsLiqcredito.ActualizaCalificacionReest(row["codigoter"], row["lincred"], row["numero"], row["calrest"], Fechaproceso, myconnect, 1); // ERROR: CS1503
                    }
                    else
                    {
                        if (Information.IsNumeric(row["periodd"]))
                        {
                            Cantidad = (Cantidad + CantidadNovedad) / Convert.ToDouble(row["periodd"]);
                            if (Cantidad >= 2)
                            {
                                switch (row["calfinicial"].ToString())
                                {
                                    case "E":
                                        calificacion = "D";
                                        break;
                                    case "D":
                                        calificacion = "C";
                                        break;
                                    case "C":
                                        calificacion = "B";
                                        break;
                                    case "B":
                                        calificacion = "A";
                                        break;
                                    case "A":
                                        calificacion = "A";
                                        break;
                                }
                                // ClsLiqcredito.ActualizaCalificacionReest(row["codigoter"], row["lincred"], row["numero"], calificacion, Fechaproceso, myconnect, 1); // ERROR: CS1503
                            }
                            else
                            {
                                if (string.Compare(CATEGORIA, row["calrest"].ToString()) > 0)
                                {
                                    row["calrest"] = CATEGORIA;
                                }
                                // ClsLiqcredito.ActualizaCalificacionReest(row["codigoter"], row["lincred"], row["numero"], row["calrest"], Fechaproceso, myconnect, 1); // ERROR: CS1503
                            }
                        }
                    }

                    // ClsLiqcredito.ActualizaFechaRevisionReest(row["codigoter"], row["lincred"], row["numero"], FecUltPagoAct, Fechaproceso, myconnect, 1); // ERROR: CS1503
                }
            }
            Progress.Close();
            Progress.Dispose();
        }

        // ==========================================================================
        // OrganizaDatosClasificacion
        // ==========================================================================
        public void OrganizaDatosClasificacion(DateTime Fechaproceso, OdbcConnection pmyConeConect, Form Myforma, ref bool EROR)
        {
            string stmysql;
            string MYSQL;
            double diasmora;
            string CATEGORIA;
            double capital;
            double interes;
            double mora;
            double cuoint;
            double salext;
            int sw1;
            int Cuopen;
            decimal TasaInt = 0;
            DateTime Fecvence;
            ERP.Core.Compartido.Controles.Barraprogress Progress = new ERP.Core.Compartido.Controles.Barraprogress("Organiza datos clasificacion", Myforma);
            int canreg = 0;
            int fila = 0;
            string StTieneReest = "0";

            stmysql = "Select copmae.codigoter,copmae.lincred,copmae.numero,diasmora, catea, cateb, catec, cated, catee,fogacla,copmae.clacuo,saldo.cuota,copsalext.salext, saldo.saldo,saldo.TASAINT, saldo.periodd, copmae.fecvemto, copmae.plazo, copmae.fecdesc,maenit.calman,parame12.priori,copmae.reest, copmae.NUMERO_SOLI , saldo.CICLOD   "
                     + " from cop_maecar copmae inner join cop_salmaecar saldo on copmae.codigoter = saldo.codigoter and copmae.lincred = saldo.lincred and copmae.numero = saldo.numero and periodo = " + Strings.Format(Fechaproceso, "yyyyMM")
                     + " inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter"
                     + " inner join  cop_concar12 parame12 on copmae.lincred = parame12.lincred  left join cop_salextras_vw copsalext on copmae.codigoter = copsalext.codigoter and copmae.lincred = copsalext.lincred and copmae.numero = copsalext.numero "
                     + " where FECFACT <= '" + Strings.Format(Fechaproceso, varini.PstForFec) + "' and debcre = 'D' and fogacla > '0' and saldo.saldo <> 0";

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, pmyConeConect, "OrganizaDatosClasificacion", ref myRead, "TblOrgDatosClasif");
            canreg = myRead.Tables["TblOrgDatosClasif"].Rows.Count;

            Progress.ValorMinimoMaximo(0, canreg);
            Progress.Show();

            MYSQL = "update sys_maenit set dias_mora = 0, categoria1 = 'A', categoria2 = 'A', categoria3 = 'A', categoria4 = 'A', dias_categoria = 0 ";
            this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");

            while (fila < canreg)
            {
                Application.DoEvents();
                DataRow row = myRead.Tables["TblOrgDatosClasif"].Rows[fila];
                sw1 = 0;

                switch (row["fogacla"].ToString())
                {
                    case "":
                        sw1 = 1;
                        break;
                    case "1":
                    case "2":
                    case "3":
                    case "5":
                        if (row["catee"].ToString() == "" || row["CATED"].ToString() == "" || row["CATEC"].ToString() == "" || row["CATEB"].ToString() == "")
                        {
                            MessageBox.Show("Parametros de dias de categorias no parametrisados" + "\n" + " el proceso no continuara hasta que no parametrize cada linea", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            EROR = true;
                            goto ExitLoop;
                        }
                        break;
                    default:
                        sw1 = 1;
                        break;
                }

                if (sw1 == 0)
                {
                    MYSQL = "update cop_maecar set categoria = ' ' , diasmora = 0 where codigoter ='" + row["codigoter"] + "' and lincred =" + row["lincred"] + " and numero = " + row["numero"];
                    this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");

                    diasmora = 0;
                    MYSQL = "select max(DiasMora) as campo1 from cop_copmora  where codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"] + " and periodo_contable = " + Strings.Format(Fechaproceso, "yyyyMM")
                           + " and (SaldoCapital + saldoextra + saldoInteres + SaldoMora+SaldoSeguro+SaldoAdmon+SaldoOtros) <> 0 ";
                    // this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion", ref diasmora); // ERROR: CS1503

                    if (diasmora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATEE"]), "000")))
                    {
                        CATEGORIA = "E";
                    }
                    else if (diasmora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATED"]), "000")))
                    {
                        CATEGORIA = "D";
                    }
                    else if (diasmora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATEC"]), "000")))
                    {
                        CATEGORIA = "C";
                    }
                    else if (diasmora > Convert.ToDouble(Strings.Format(Convert.ToInt32(row["CATEB"]), "000")))
                    {
                        CATEGORIA = "B";
                    }
                    else
                    {
                        CATEGORIA = "A";
                    }

                    if (row["calman"].ToString().Trim() != "")
                    {
                        StTieneReest = myRead.Tables["TblOrgDatosClasif"].Select("codigoter='" + row["codigoter"] + "' and reest='Y'").Length.ToString();
                        if (Information.IsNumeric(StTieneReest))
                        {
                            if (Convert.ToDouble(StTieneReest) <= 0)
                            {
                                CATEGORIA = row["calman"].ToString();
                            }
                            else
                            {
                                if (row["reest"].ToString() == "Y")
                                {
                                    CATEGORIA = row["calman"].ToString();
                                }
                            }
                        }
                        else
                        {
                            CATEGORIA = row["calman"].ToString();
                        }
                    }

                    capital = 0;
                    interes = 0;
                    cuoint = 0;
                    mora = 0;
                    // buscaVencido(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(Strings.Format(Fechaproceso, "yyyyMM")), pmyConeConect, CATEGORIA, Convert.ToInt32(row["DIASMORA"]), ref capital, ref interes, cuoint, ref mora); // ERROR: CS1620

                    if (row["priori"] is DBNull)
                    {
                        Fecvence = Convert.ToDateTime(row["fecdesc"]);
                        // Fecvence = this.CalculaFechaVence(row["NUMERO_SOLI"], row["cuota"], row["fecdesc"], row["plazo"], pmyConeConect, row["periodd"], row["CICLOD"]); // ERROR: CS1503
                    }
                    else
                    {
                        if (row["priori"].ToString() != "99")
                        {
                            Fecvence = Convert.ToDateTime(row["fecdesc"]);
                            // Fecvence = this.CalculaFechaVence(row["NUMERO_SOLI"], row["cuota"], row["fecdesc"], row["plazo"], pmyConeConect, row["periodd"], row["CICLOD"]); // ERROR: CS1503
                        }
                        else
                        {
                            if (row["fecvemto"] is DBNull)
                            {
                                Fecvence = Convert.ToDateTime(row["fecdesc"]);
                                // Fecvence = this.CalculaFechaVence(row["NUMERO_SOLI"], row["cuota"], row["fecdesc"], row["plazo"], pmyConeConect, row["periodd"], row["CICLOD"]); // ERROR: CS1503
                            }
                            else
                            {
                                Fecvence = Convert.ToDateTime(row["fecvemto"]);
                                if (Convert.ToDateTime(row["fecvemto"]) <= Convert.ToDateTime(row["fecdesc"]))
                                {
                                    Fecvence = Convert.ToDateTime(row["fecdesc"]);
                                    // Fecvence = this.CalculaFechaVence(row["NUMERO_SOLI"], row["cuota"], row["fecdesc"], row["plazo"], pmyConeConect, row["periodd"], row["CICLOD"]); // ERROR: CS1503
                                }
                            }
                        }
                    }

                    MYSQL = " UPDATE COP_MAECAR SET FECVEMTO = '" + Strings.Format(Fecvence, varini.PstForFec) + "',CATEGORIA = '" + CATEGORIA + "',CAPATR = " + capital + ",cuoint = " + cuoint + ", intatr = " + interes + ", intmor = " + mora + ",diasmora = " + diasmora + " WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND LINCRED = " + row["LINCRED"] + " AND NUMERO = " + row["NUMERO"];
                    this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");

                    MYSQL = " UPDATE SYS_MAENIT SET DIAS_MORA = " + row["DIASMORA"] + " WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND DIAS_MORA < " + row["DIASMORA"];
                    this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");

                    switch (row["fogacla"].ToString())
                    {
                        case "1":
                            MYSQL = " UPDATE SYS_MAENIT SET CATEGORIA1 = '" + CATEGORIA + "' WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND CATEGORIA1 < '" + CATEGORIA + "'";
                            this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");
                            break;
                        case "2":
                            MYSQL = " UPDATE SYS_MAENIT SET CATEGORIA2 = '" + CATEGORIA + "', dias_categoria = " + diasmora + " WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND CATEGORIA2 < '" + CATEGORIA + "'";
                            this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");
                            MYSQL = " UPDATE SYS_MAENIT SET dias_categoria = " + diasmora + " WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND dias_categoria < '" + diasmora + "'";
                            this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");
                            break;
                        case "3":
                            MYSQL = " UPDATE SYS_MAENIT SET CATEGORIA3 = '" + CATEGORIA + "' WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND CATEGORIA3 < '" + CATEGORIA + "'";
                            this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");
                            break;
                        case "5":
                            MYSQL = " UPDATE SYS_MAENIT SET CATEGORIA4 = '" + CATEGORIA + "' WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND CATEGORIA4 < '" + CATEGORIA + "'";
                            this.OdbcConnect.ExecuteQueryconec(MYSQL, pmyConeConect, "OrganizaDatosClasificacion");
                            break;
                    }
                }
                Progress.PerformStep();
                fila += 1;
            }
        ExitLoop:
            Progress.Close();
            Progress.Dispose();
            myRead.Dispose();
        }

        // ==========================================================================
        // AplicaLeyArrastre
        // ==========================================================================
        public void AplicaLeyArrastre(DateTime fechaProceso, Form Myforma, OdbcConnection pmyConeConect)
        {
            int reg = 0;
            int tfila = 0;
            ERP.Core.Compartido.Controles.Barraprogress progress = new ERP.Core.Compartido.Controles.Barraprogress("Aplicando ley de arrastre", Myforma);

            stmysql = "Select copmae.codigoter,copmae.lincred,copmae.numero,diasmora, fogacla, categoria1, categoria2, categoria3, categoria4,maenit.calman from cop_maecar copmae inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter inner join  cop_concar12 parame12 on copmae.lincred = parame12.lincred "
                    + " inner join cop_salmaecar saldos on copmae.codigoter = saldos.codigoter and copmae.lincred = saldos.lincred and copmae.numero = saldos.numero and periodo =  " + Strings.Format(fechaProceso, "yyyyMM")
                    + " where  copmae.FECFACT <= '" + Strings.Format(fechaProceso, varini.PstForFec) + "' and saldos.saldo <> 0 and debcre = 'D' and fogacla > '0'";

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, pmyConeConect, "AplicaLeyArrastre", ref myRead, "TblLeyArrastre");
            reg = myRead.Tables["TblLeyArrastre"].Rows.Count;

            progress.ValorMinimoMaximo(0, reg);
            progress.Show();

            while (tfila < reg)
            {
                Application.DoEvents();
                DataRow row = myRead.Tables["TblLeyArrastre"].Rows[tfila];
                switch (row["fogacla"].ToString())
                {
                    case "1":
                        stmysql = " UPDATE cop_maecar set CATEGORIA = '" + row["categoria1"] + "' WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND CATEGORIA < '" + row["categoria1"] + "' "
                                + " and lincred in (select lincred from cop_concar12 where fogacla='1')";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "AplicaLeyArrastre");
                        break;
                    case "2":
                        stmysql = " UPDATE cop_maecar set CATEGORIA = '" + row["categoria2"] + "' WHERE CODIGOTER = '" + row["CODIGOTER"] + "' and LINCRED = " + row["LINCRED"] + " AND CATEGORIA < '" + row["categoria2"] + "' "
                                + " and lincred in (select lincred from cop_concar12 where fogacla='2')";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "AplicaLeyArrastre");
                        break;
                    case "3":
                        stmysql = " UPDATE cop_maecar set CATEGORIA = '" + row["categoria3"] + "' WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND CATEGORIA < '" + row["categoria3"] + "' "
                                + " and lincred in (select lincred from cop_concar12 where fogacla='3')";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "AplicaLeyArrastre");
                        break;
                    case "5":
                        stmysql = " UPDATE cop_maecar set CATEGORIA = '" + row["categoria4"] + "' WHERE CODIGOTER = '" + row["CODIGOTER"] + "' AND CATEGORIA < '" + row["categoria4"] + "' "
                                + " and lincred in (select lincred from cop_concar12 where fogacla='5')";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "AplicaLeyArrastre");
                        break;
                }
                progress.PerformStep();
                tfila += 1;
            }

            progress.Close();
            progress.Dispose();
            myRead.Dispose();
        }

        // ==========================================================================
        // LiquidaIntCtasorden
        // ==========================================================================
        public void LiquidaIntCtasorden(DateTime Fechaproceso, Form Myforma, OdbcConnection pmyConeConect)
        {
            int DIAS;
            int CXC;
            int CA01_CUOINT;
            int CA01_INTATR;
            int CA01_INTMOR;
            int CA01_VALMENOS;
            double SaldoMora;
            decimal Tasa = 0;
            int canreg = 0;
            int fila = 0;

            ERP.Core.Compartido.Controles.Barraprogress Progress = new ERP.Core.Compartido.Controles.Barraprogress("Interes cuentas de orden", Myforma);

            stmysql = "select tasa_mora as Campo1 from sys_compania where codigo = '" + varini.sptCodEmpr + "'";
            // this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "LiquidaIntCtasorden", ref Tasa); // ERROR: CS1503

            Progress.ValorMinimoMaximo(0, 1);
            Progress.Show();

            stmysql = "update cop_maecar set cuoint = 0,intatr = 0, intmor = 0, valmenos = 0 where lincred >= 1000";
            this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "LiquidaIntCtasorden");

            stmysql = "update COP_COPMORA set cxc = 0 where lincred >= 1000 and PERIODO_CONTABLE = " + Strings.Format(Fechaproceso, "yyyyMM");
            this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "LiquidaIntCtasorden");

            stmysql = "SELECT COPMORA.CODIGOTER, COPMORA.LINCRED,COPMORA.NUMERO,PERIODO_CONTABLE, PERIODO_CAUSA, COPMAE.DIASMORA AS DIAS, COPMORA.DIASMORA,COPMAE.CATEGORIA, copmora.cxc, SALDOINTERES, SALDOMORA, SaldoCapital + SaldoMora as SaldoCapital, CATEC "
                    + " FROM COP_COPMORA  COPMORA INNER JOIN COP_MAECAR COPMAE ON COPMORA.CODIGOTER = COPMAE.CODIGOTER AND COPMORA.LINCRED = COPMAE.LINCRED AND COPMORA.NUMERO = COPMAE.NUMERO"
                    + " INNER JOIN COP_CONCAR12 PARAME12 ON copmora.lincred = parame12.lincred "
                    + " WHERE debcre = 'D' and fogacla > '0' and COPMORA.PERIODO_CONTABLE = " + Strings.Format(Fechaproceso, "yyyyMM");

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, pmyConeConect, "LiquidaIntCtasorden", ref myRead, "TblLiqIntCtaOrden");
            canreg = myRead.Tables["TblLiqIntCtaOrden"].Rows.Count;

            Progress.ValorMinimoMaximo(0, canreg);
            Progress.Show();

            while (fila < canreg)
            {
                Application.DoEvents();
                DataRow row = myRead.Tables["TblLiqIntCtaOrden"].Rows[fila];
                CA01_INTATR = 0;
                CA01_INTMOR = 0;
                CA01_CUOINT = 0;
                CA01_VALMENOS = 0;
                CXC = 0;

                if (string.Compare(row["CATEGORIA"].ToString(), "C") >= 0)
                {
                    DIAS = Convert.ToInt32(row["DIAS"]) - (Convert.ToInt32(row["CATEC"]) + 1);
                    if (Convert.ToInt32(row["DIASMORA"]) > DIAS && Convert.ToInt32(row["cxc"]) == 0)
                    {
                        CA01_INTATR = Convert.ToInt32(row["SaldoInteres"]);
                        SaldoMora = Convert.ToDouble(row["SaldoMora"]);

                        if (Convert.ToInt32(row["diasMora"]) >= Convert.ToInt32(row["CATEC"]) && Convert.ToDouble(row["SALDOMORA"]) > 0)
                        {
                            double ca01Valmenos = CA01_VALMENOS;
                            CalculaDiasMora(ref ca01Valmenos, Convert.ToDouble(row["saldoCapital"]), ref SaldoMora, Convert.ToInt32(row["DiasMora"]), Convert.ToInt32(row["catec"]), Tasa);
                            CA01_VALMENOS = (int)ca01Valmenos;
                        }
                        CA01_INTMOR = (int)SaldoMora;
                    }
                    else
                    {
                        CA01_CUOINT = Convert.ToInt32(row["SaldoInteres"]);
                        CA01_VALMENOS = Convert.ToInt32(row["SaldoMora"]);
                        CXC = 9;
                    }
                }
                else
                {
                    CA01_INTATR = Convert.ToInt32(row["SaldoInteres"]);
                    CA01_INTMOR = Convert.ToInt32(row["SaldoMora"]);
                }

                stmysql = "update cop_maecar set intatr = intatr + " + CA01_INTATR + ","
                         + "intmor = intmor + " + CA01_INTMOR + ","
                         + "cuoint = cuoint + " + CA01_CUOINT + ","
                         + "valmenos = valmenos + " + CA01_VALMENOS
                         + " where codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"];
                this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "LiquidaIntCtasorden");

                stmysql = "update cop_copmora set cxc = " + CXC
                         + " where codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"]
                         + " and PERIODO_CONTABLE = " + Strings.Format(Fechaproceso, "yyyyMM") + " and PERIODO_CAUSA = " + row["periodo_causa"];
                this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "LiquidaIntCtasorden");

                Progress.PerformStep();
                fila += 1;
            }

            Progress.Close();
            Progress.Dispose();
            myRead.Dispose();
        }

        // ==========================================================================
        // liquidaProvision
        // ==========================================================================
        public void liquidaProvision(DateTime fechaproceso, Form Myforma, OdbcConnection pmyConeConect)
        {
            decimal ProvConCatB, ProvConCatC, ProvConCatD, ProvConCatE;
            decimal ProvComCatB, ProvComCatC, ProvComCatD, ProvComCatE;
            decimal ProvVivCatB, ProvVivCatC, ProvVivCatD, ProvVivCatE;
            decimal ProvMicCatB, ProvMicCatC, ProvMicCatD, ProvMicCatE;
            DataSet DsDataSet = new DataSet();
            decimal ValorProvision;

            // ok = msgconfig.BuscaParProvision(1, fechaproceso.ToString("yyyyMM"), pmyConeConect, ref DsDataSet); // ERROR: CS1061
            if (ok)
            {
                DataRow r = DsDataSet.Tables["tblparprov"].Rows[0];
                ProvConCatB = Convert.ToDecimal(r["Tasab"]);
                ProvConCatC = Convert.ToDecimal(r["Tasac"]);
                ProvConCatD = Convert.ToDecimal(r["Tasad"]);
                ProvConCatE = Convert.ToDecimal(r["Tasae"]);
            }
            else
            {
                MessageBox.Show("Parametros de Tasas de provision no estan creadas CONSUMO", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ProvConCatB = 0; ProvConCatC = 0; ProvConCatD = 0; ProvConCatE = 0;
            }

            // ok = msgconfig.BuscaParProvision(2, fechaproceso.ToString("yyyyMM"), pmyConeConect, ref DsDataSet); // ERROR: CS1061
            if (ok)
            {
                DataRow r = DsDataSet.Tables["tblparprov"].Rows[0];
                ProvComCatB = Convert.ToDecimal(r["Tasab"]);
                ProvComCatC = Convert.ToDecimal(r["Tasac"]);
                ProvComCatD = Convert.ToDecimal(r["Tasad"]);
                ProvComCatE = Convert.ToDecimal(r["Tasae"]);
            }
            else
            {
                MessageBox.Show("Parametros de Tasas de provision no estan creadas COMERCIAL", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ProvComCatB = 0; ProvComCatC = 0; ProvComCatD = 0; ProvComCatE = 0;
            }

            // ok = msgconfig.BuscaParProvision(3, fechaproceso.ToString("yyyyMM"), pmyConeConect, ref DsDataSet); // ERROR: CS1061
            if (ok)
            {
                DataRow r = DsDataSet.Tables["tblparprov"].Rows[0];
                ProvVivCatB = Convert.ToDecimal(r["Tasab"]);
                ProvVivCatC = Convert.ToDecimal(r["Tasac"]);
                ProvVivCatD = Convert.ToDecimal(r["Tasad"]);
                ProvVivCatE = Convert.ToDecimal(r["Tasae"]);
            }
            else
            {
                MessageBox.Show("Parametros de Tasas de provision no estan creadas VIVIENDA", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ProvVivCatB = 0; ProvVivCatC = 0; ProvVivCatD = 0; ProvVivCatE = 0;
            }

            // ok = msgconfig.BuscaParProvision(4, fechaproceso.ToString("yyyyMM"), pmyConeConect, ref DsDataSet); // ERROR: CS1061
            if (ok)
            {
                DataRow r = DsDataSet.Tables["tblparprov"].Rows[0];
                ProvMicCatB = Convert.ToDecimal(r["Tasab"]);
                ProvMicCatC = Convert.ToDecimal(r["Tasac"]);
                ProvMicCatD = Convert.ToDecimal(r["Tasad"]);
                ProvMicCatE = Convert.ToDecimal(r["Tasae"]);
            }
            else
            {
                MessageBox.Show("Parametros de Tasas de provision no estan creadas MICROCREDITO", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ProvMicCatB = 0; ProvMicCatC = 0; ProvMicCatD = 0; ProvMicCatE = 0;
            }

            stmysql = "Select copmae.codigoter,copmae.lincred,numero,diasmora, fogacla,dias_categoria,copmae.categoria from cop_maecar copmae inner "
                    + "join sys_maenit maenit on copmae.codigoter = maenit.codigoter inner join  cop_concar12 parame12 on parame12.lincred = copmae.lincred "
                    + "where  copmae.FECFACT <= '" + Strings.Format(fechaproceso, varini.PstForFec) + "' and debcre = 'D' and fogacla > '0'";

            ERP.Core.Compartido.Controles.Barraprogress progress = new ERP.Core.Compartido.Controles.Barraprogress("Liquidacion de Provision", Myforma);
            int reg = 0;
            int tfila = 0;

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, pmyConeConect, "liquidaProvision", ref myRead, "TblLiqProvision");
            reg = myRead.Tables["TblLiqProvision"].Rows.Count;

            progress.ValorMinimoMaximo(0, reg);
            progress.Show();

            while (tfila < reg)
            {
                Application.DoEvents();
                DataRow row = myRead.Tables["TblLiqProvision"].Rows[tfila];
                ValorProvision = 0;
                switch (row["fogacla"].ToString())
                {
                    case "1":
                        switch (row["categoria"].ToString())
                        {
                            case "E": ValorProvision = ProvComCatE; break;
                            case "D": ValorProvision = ProvComCatD; break;
                            case "C": ValorProvision = ProvComCatC; break;
                            case "B": ValorProvision = ProvComCatB; break;
                            case "A": ValorProvision = 0; break;
                            default: ValorProvision = 0; break;
                        }
                        stmysql = " UPDATE cop_maecar set tasa_provi = " + ValorProvision + " WHERE codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"];
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "liquidaProvision");
                        break;
                    case "2":
                        switch (row["categoria"].ToString())
                        {
                            case "E":
                                if (Convert.ToInt32(row["dias_categoria"]) > 360)
                                    ValorProvision = 100;
                                else
                                    ValorProvision = ProvConCatE;
                                break;
                            case "D": ValorProvision = ProvConCatD; break;
                            case "C": ValorProvision = ProvConCatC; break;
                            case "B": ValorProvision = ProvConCatB; break;
                            case "A": ValorProvision = 0; break;
                            default: ValorProvision = 0; break;
                        }
                        stmysql = " UPDATE cop_maecar set tasa_provi = " + ValorProvision + " WHERE codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"];
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "liquidaProvision");
                        break;
                    case "3":
                        switch (row["categoria"].ToString())
                        {
                            case "E":
                                if (Convert.ToInt32(row["diasMora"]) > 1080)
                                    ValorProvision = 100;
                                else if (Convert.ToInt32(row["diasMora"]) > 720 && Convert.ToInt32(row["diasMora"]) <= 1080)
                                    ValorProvision = 60;
                                else if (Convert.ToInt32(row["diasMora"]) <= 720)
                                    ValorProvision = ProvVivCatE;
                                else
                                    ValorProvision = ProvVivCatE;
                                break;
                            case "D": ValorProvision = ProvVivCatD; break;
                            case "C": ValorProvision = ProvVivCatC; break;
                            case "B": ValorProvision = ProvVivCatB; break;
                            case "A": ValorProvision = 0; break;
                            default: ValorProvision = 0; break;
                        }
                        stmysql = " UPDATE cop_maecar set tasa_provi = " + ValorProvision + " WHERE codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"];
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "liquidaProvision");
                        break;
                    case "5":
                        switch (row["categoria"].ToString())
                        {
                            case "E": ValorProvision = ProvMicCatE; break;
                            case "D": ValorProvision = ProvMicCatD; break;
                            case "C": ValorProvision = ProvMicCatC; break;
                            case "B": ValorProvision = ProvMicCatB; break;
                            case "A": ValorProvision = 0; break;
                            default: ValorProvision = 0; break;
                        }
                        stmysql = "UPDATE cop_maecar set tasa_provi = " + ValorProvision + " WHERE codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"];
                        this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "liquidaProvision");
                        break;
                }
                progress.PerformStep();
                tfila += 1;
            }

            myRead.Dispose();

            stmysql = "Select copmae.codigoter, sum(saldo) as SALDO from cop_maecar copmae inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter inner join cop_saldos_vw saldos on  saldos.codigoter = copmae.codigoter and saldos.lincred = copmae.lincred and saldos.numero = copmae.numero and saldos.periodo = " + Strings.Format(fechaproceso, "yyyyMM") + " inner join cop_concar12 parame12 on parame12.lincred = copmae.lincred where copmae.FECFACT <= '" + Strings.Format(fechaproceso, varini.PstForFec) + "' and codahor = '1' GROUP BY COPMAE.CODIGOTER";
            SumaAportesObligaciones(fechaproceso, stmysql, "AP", pmyConeConect);

            stmysql = "Select copmae.codigoter, sum(saldo) as saldo from cop_maecar copmae inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter inner join cop_saldos_vw saldos on  saldos.codigoter = copmae.codigoter and saldos.lincred = copmae.lincred and saldos.numero = copmae.numero and saldos.periodo = " + Strings.Format(fechaproceso, "yyyyMM") + " inner join cop_concar12 parame12 on parame12.lincred = copmae.lincred where copmae.FECFACT <= '" + Strings.Format(fechaproceso, varini.PstForFec) + "' and parame12.fogacla > '0'  GROUP BY COPMAE.CODIGOTER";
            SumaAportesObligaciones(fechaproceso, stmysql, "Deuda", pmyConeConect);
            progress.Close();
            progress.Dispose();
        }

        // ==========================================================================
        // CalculaProvision
        // ==========================================================================
        public void CalculaProvision(DateTime fechaProceso, Form Myforma, OdbcConnection pmyConeConect)
        {
            double abomes;
            double WGARA_APLICA;
            double TOTAL;
            double PROV;
            double Avaluo;
            decimal i = 0;
            int clasegar;
            string Campo = "G";
            int DiasMora;
            int canreg = 0;
            int fila = 0;

            ERP.Core.Compartido.Controles.Barraprogress progress = new ERP.Core.Compartido.Controles.Barraprogress("Calcula Provision", Myforma);

            stmysql = "Select copmae.codigoter,copmae.lincred,COPMAE.numero, copmae.diasmora,maenit.saldo_aporte * -1 as saldo_aporte, saldo_deuda, saldos.saldo, AVALUO_CCIAL, tasa_provi, copmae.clasegar"
                    + " from cop_maecar copmae inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter  inner join cop_saldos_vw saldos on saldos.codigoter = copmae.codigoter and saldos.lincred = copmae.lincred and saldos.numero = copmae.numero and saldos.periodo = " + Strings.Format(fechaProceso, "yyyyMM") + " inner join  cop_concar12 parame12 on parame12.lincred = copmae.lincred  "
                    + " left join cop_garantia copgar on copgar.codigoter = copmae.codigoter and copgar.lincred = copmae.lincred and copgar.numero = copmae.numero "
                    + " where  copmae.FECFACT <= '" + Strings.Format(fechaProceso, varini.PstForFec) + "' and fogacla > '0' and saldos.saldo <> 0";

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, pmyConeConect, "CalculaProvision", ref myRead, "TblCalculaProv");
            canreg = myRead.Tables["TblCalculaProv"].Rows.Count;

            progress.ValorMinimoMaximo(0, canreg);
            progress.Show();

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCalculaProv"].Rows[fila];
                Application.DoEvents();
                DiasMora = Convert.ToInt32(row["DiasMora"]);
                WGARA_APLICA = 0;

                try { clasegar = Convert.ToInt32(row["clasegar"]); } catch { clasegar = 1; }

                try { i = (Convert.ToDecimal(row["saldo"]) / Convert.ToDecimal(row["saldo_deuda"])) * 100; } catch { }

                abomes = Math.Round(Convert.ToDouble(row["saldo_aporte"]) * ((double)i / 100));

                if (clasegar > 1 && clasegar != 9 && clasegar != 10 && clasegar != 11 && clasegar != 12)
                {
                    if (row["AVALUO_CCIAL"] is DBNull)
                    {
                        Avaluo = 0;
                        DiasMora = 999;
                    }
                    else
                    {
                        Avaluo = Convert.ToDouble(row["AVALUO_CCIAL"]);
                    }

                    switch (clasegar)
                    {
                        case 2:
                            if (DiasMora > 1080)
                                WGARA_APLICA = 0;
                            else if (DiasMora > 900)
                                WGARA_APLICA = Math.Round(Avaluo * 0.15);
                            else if (DiasMora > 720)
                                WGARA_APLICA = Math.Round(Avaluo * 0.3);
                            else if (DiasMora > 540)
                                WGARA_APLICA = Math.Round(Avaluo * 0.5);
                            else
                                WGARA_APLICA = Math.Round(Avaluo * 0.7);
                            break;
                        default:
                            if (DiasMora > 720)
                                WGARA_APLICA = 0;
                            else if (DiasMora > 540)
                                WGARA_APLICA = Math.Round(Avaluo * 0.5);
                            else
                                WGARA_APLICA = Math.Round(Avaluo * 0.7);
                            break;
                    }
                }

                TOTAL = Math.Round(Convert.ToDouble(row["SALDO"]) - abomes - WGARA_APLICA);
                PROV = Math.Round(TOTAL * (Convert.ToDouble(row["tasa_provi"]) / 100));
                if (PROV < 0)
                {
                    PROV = 0;
                }

                stmysql = "update cop_maecar set vlr_provi = " + PROV + ", APORTES = " + abomes + ", diasMora = " + DiasMora + " where codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"];
                this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "CalculaProvision");
                progress.PerformStep();
                fila += 1;
            }

            progress.Close();
            progress.Dispose();
            myRead.Dispose();
        }

        // ==========================================================================
        // GrabatosClasificacion
        // ==========================================================================
        public void GrabatosClasificacion(DateTime fechaproceso, Form Myforma, OdbcConnection pmyConeConect)
        {
            bool ok = true;
            int clasegar;
            double TOTAL;
            int fogacla;
            double saldo;
            string Campo = "G";
            double VlrProInt;
            ERP.Core.Compartido.Controles.Barraprogress progress = new ERP.Core.Compartido.Controles.Barraprogress("Grabados Clasificacion", Myforma);
            int reg = 0;
            int tfila = 0;

            stmysql = "SELECT copmae.codigoter,copmae.lincred, copmae.numero,copmae.clasegar, copmae.diasmora, copmae.categoria,parame12.fogacla,"
              + " saldo.clades, copmae.vlr_provi, copmae.tasa_provi, copmae.intatr, copmae.intmor,copmae.valmenos, copmae.cuoint,copmae.capatr,copmae.aportes,"
              + " copmae.clasegar,parame12.centroco, maenit.nombre,saldo.saldo  FROM  cop_maecar copmae inner join  sys_maenit maenit on copmae.codigoter = maenit.codigoter"
              + " inner join cop_concar12 parame12 on copmae.lincred = parame12.lincred inner join cop_saldos_vw saldo on saldo.codigoter = copmae.codigoter and "
              + " saldo.lincred = copmae.lincred and saldo.numero = copmae.numero and saldo.periodo = " + Strings.Format(fechaproceso, "yyyyMM")
              + " where debcre = 'D' and fogacla > '0' and saldo.saldo <> 0 AND COPMAE.FECFACT <= '" + Strings.Format(fechaproceso, varini.PstForFec) + "'";

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, pmyConeConect, "GrabatosClasificacion", ref myRead, "TblGrabDatosClasif");
            reg = myRead.Tables["TblGrabDatosClasif"].Rows.Count;

            progress.ValorMinimoMaximo(0, reg);
            progress.Show();

            stmysql = " delete from cop_copclas where periodo_contable = " + Strings.Format(fechaproceso, "yyyyMM");
            this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "GrabatosClasificacion");

            while (tfila < reg)
            {
                DataRow row = myRead.Tables["TblGrabDatosClasif"].Rows[tfila];
                Application.DoEvents();
                ok = true;
                try { clasegar = Convert.ToInt32(row["clasegar"]); } catch { clasegar = 1; }

                switch (clasegar)
                {
                    case 1:
                    case 9:
                    case 10:
                    case 11:
                    case 12:
                        clasegar = 0;
                        break;
                    default:
                        clasegar = 1;
                        break;
                }

                TOTAL = 0;

                if (row["fogacla"].ToString() == "")
                {
                    fogacla = 0;
                }
                else
                {
                    fogacla = Convert.ToInt32(row["fogacla"]);
                }
                if (row["SALDO"] is DBNull)
                {
                    saldo = 0;
                }
                else
                {
                    saldo = Convert.ToDouble(row["SALDO"]);
                }

                if (string.Compare(row["CATEGORIA"].ToString(), "B") > 0)
                {
                    VlrProInt = Convert.ToDouble(row["intatr"]) + Convert.ToDouble(row["intmor"]);
                }
                else
                {
                    VlrProInt = 0;
                }

                if (row["CLADES"] is DBNull || (row["CLADES"].ToString() != "1" && row["CLADES"].ToString() != "2"))
                {
                    MessageBox.Show("Obligacion con clase de descuento errado, por favor revise "
                    + "\n" + "obligacion no se incluira en la clasificacion. Verifique "
                    + "\n" + "y corra de nuevo el proceso."
                    + "\n" + "Codigo : " + row["codigoter"] + "  Obligacion : " + row["lincred"] + " " + row["numero"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ok = false;
                }

                if (ok == true)
                {
                    stmysql = " insert into cop_copclas(codigoter,lincred, numero, periodo_contable, clasec, clagar, clades, catego, cpto, cencos,"
                                    + "nombre, saldot, salint, salmor, salord, salpro, tasa, diasmora, aporte, provint, salmoro)"
                                    + " values ('" + row["codigoter"] + "'," + row["lincred"] + "," + row["numero"]
                                    + "," + Strings.Format(fechaproceso, "yyyyMM") + "," + fogacla + "," + clasegar + ",'" + row["CLADES"] + "','"
                                    + row["CATEGORIA"] + "'," + row["LINCRED"] + ",'" + row["centroco"] + "','" + row["nombre"] + "'," + saldo + ","
                                    + row["INTATR"] + "," + row["INTMOR"] + "," + row["cuoint"] + "," + row["VLR_PROVI"] + ",'" + row["TASA_PROVI"]
                                    + "'," + row["DIASMORA"] + "," + row["APORTES"] + "," + VlrProInt + "," + row["valmenos"] + ")";
                    this.OdbcConnect.ExecuteQueryconec(stmysql, pmyConeConect, "GrabatosClasificacion");
                }
                progress.PerformStep();
                tfila += 1;
            }

            progress.Close();
            progress.Dispose();
            myRead.Dispose();
        }

        // ==========================================================================
        // SumaAportesObligaciones
        // ==========================================================================
        public void SumaAportesObligaciones(DateTime FechaProceso, string stmysql, string opt, OdbcConnection Myconnect)
        {
            double saldot;
            int sw1 = 0;
            double tsaldot;
            int reg = 0;
            int tfila = 0;
            DataSet myRead = new DataSet();
            DataSet dscompania = new DataSet();

            // this.msgcofsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, Myconnect); // ERROR: CS1620

            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconnect, "SumaAportesObligaciones", ref myRead, "TblSumAporOblig");
            reg = myRead.Tables["TblSumAporOblig"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myRead.Tables["TblSumAporOblig"].Rows[tfila];
                if (row["saldo"] is DBNull)
                {
                    saldot = 0;
                }
                else
                {
                    saldot = Convert.ToDouble(row["saldo"]);
                }
                switch (opt)
                {
                    case "AP":
                        if (dscompania.Tables["tblcompania"].Rows[0]["calcprovaportes"].ToString() == "N")
                        {
                            saldot = 0;
                        }
                        stmysql = "update sys_maenit set saldo_aporte = '" + saldot + "' where codigoter = '" + row["codigoter"] + "'";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "liquidaProvision");
                        break;
                    case "Deuda":
                        stmysql = "update sys_maenit set saldo_deuda = '" + saldot + "' where codigoter = '" + row["codigoter"] + "'";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "liquidaProvision");
                        break;
                }
                tfila += 1;
            }
            myRead.Dispose();
        }

        // ==========================================================================
        // CalculaDiasMora
        // ==========================================================================
        private void CalculaDiasMora(ref double CA01_VALMENOS, double SaldoCapital, ref double SaldoMora, int Dias, int DiasC, decimal Tasa)
        {
            int Tdias;
            double Valor;
            double ValMora;
            Tdias = Dias - DiasC;
            Valor = Math.Round((SaldoCapital * ((double)Tasa / 100) / 30) * Tdias, 0);
            ValMora = SaldoMora - Valor;
            if (ValMora < 0)
            {
                CA01_VALMENOS = CA01_VALMENOS + SaldoMora;
                SaldoMora = 0;
            }
            else
            {
                SaldoMora = ValMora;
                CA01_VALMENOS = CA01_VALMENOS + Valor;
            }
        }

        // ==========================================================================
        // buscaVencido
        // ==========================================================================
        private void buscaVencido(string codigoter, int lincred, double numero, int ciclo, OdbcConnection pmyConect, string CATEGORIA, ref int DiasMora, ref double Capital, ref double interes, double cuoint, ref double mora, string opcion = "Vencido")
        {
            string stmysql = "select SaldoCapital capital, saldoInteres Interes, saldoMora mora, diasmora, parame12.catec from cop_copmora copmora inner join cop_concar12 parame12 on parame12.lincred = copmora.lincred  where codigoter = '" + codigoter + "' and copmora.lincred = " + lincred + " and numero = " + numero + " and periodo_contable = " + ciclo;
            int canreg = 0;
            int fila = 0;
            int dias;

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, pmyConect, "buscaVencido", ref myRead, "TblBuscaVenc");
            canreg = myRead.Tables["TblBuscaVenc"].Rows.Count;

            cuoint = 0;
            interes = 0;
            Capital = 0;
            mora = 0;
            switch (opcion)
            {
                case "Vencido":
                    while (fila < canreg)
                    {
                        DataRow row = myRead.Tables["TblBuscaVenc"].Rows[fila];
                        if (string.Compare(CATEGORIA, "C") >= 0)
                        {
                            dias = DiasMora - Convert.ToInt32(row["catec"]);

                            if (dias > Convert.ToInt32(row["diasmora"]))
                            {
                                interes = interes + Convert.ToDouble(row["Interes"]);
                                mora = mora + Convert.ToDouble(row["Mora"]);
                            }
                            else
                            {
                                cuoint = cuoint + Convert.ToDouble(row["Interes"]);
                                mora = mora + Convert.ToDouble(row["Mora"]);
                            }
                        }
                        else
                        {
                            if (Convert.ToInt32(row["DIASMORA"]) > Convert.ToInt32(row["catec"]))
                            {
                                cuoint = cuoint + Convert.ToDouble(row["Interes"]);
                            }
                            else
                            {
                                interes = interes + Convert.ToDouble(row["Interes"]);
                            }
                            mora = mora + Convert.ToDouble(row["Mora"]);
                        }
                        Capital = Capital + Convert.ToDouble(row["capital"]);
                        fila += 1;
                    }
                    break;
            }

            myRead.Dispose();
        }

        // ==========================================================================
        // CalculaNovedad
        // ==========================================================================
        public void CalculaNovedad(string codigoter, int lincred, double Conselinea, string Periodo, string Ciclo, int Periodd, double CapCausado, OdbcConnection myconnect,
            ref double CapCauNov, ref double IntCauNov, ref int NumCuotas, ref string AplicaExtras, ref string MotivoNov, ref string TipoNovedad,
            ref double TasaUsura, string CicloDesc = "5")
        {
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos msgliqcred = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            int TipoNov = 0;
            int NumCiclos = 0;
            int TipoLinea = 0;
            string StTipoNovedad = "";
            double CapCau = 0;
            double IntCau = 0;
            int CiclosNov = 0;
            double ExtraCau = 0;
            int Periodicidad = 0;
            DateTime FecIni = default(DateTime);
            DateTime FecFin = default(DateTime);
            int PeriodoCausa = 0;
            int mes;
            int anhio;
            string stperciclo;
            string valciclo;
            int sw1;

            // ok = this.BuscaNovedadCausa(codigoter, lincred, Conselinea, Ciclo, myconnect, ref TipoNov, ref MotivoNov, ref Periodicidad, ref NumCiclos, ref AplicaExtras); // ERROR: CS7036
            this.BuscaLinea(lincred, myconnect, ref TipoLinea);

            TipoNovedad = StTipoNovedad;
            PeriodoCausa = Convert.ToInt32(Ciclo);

            if (ok == true)
            {
                NumCuotas = NumCiclos;
            }
            else
            {
                NumCuotas = 0;
            }

            if (Periodd != Periodicidad)
            {
                return;
            }

            switch (TipoNov)
            {
                case 3:
                case 4:
                    TipoNovedad = TipoNov.ToString();
                    if (TipoNov == 4)
                    {
                        if (TipoLinea == 4 || TipoLinea == 5)
                        {
                            CapCausado = 0;
                        }
                    }

                    anhio = Convert.ToInt32(PeriodoCausa.ToString().Substring(0, 4));
                    mes = Convert.ToInt32(PeriodoCausa.ToString().Substring(4));
                    stperciclo = PeriodoCausa.ToString();

                    for (CiclosNov = 1; CiclosNov <= NumCiclos; CiclosNov++)
                    {
                        sw1 = 0;

                        if (CicloDesc != "5" && Periodd.ToString() != "1")
                        {
                            switch (Periodd.ToString())
                            {
                                case "2": NumCiclos = 24; break;
                                case "3": NumCiclos = 36; break;
                                case "4": NumCiclos = 52; break;
                                case "5": NumCiclos = 365; break;
                            }
                            mes += 1;
                            if (mes > NumCiclos)
                            {
                                mes -= NumCiclos;
                                anhio += 1;
                            }
                            stperciclo = anhio.ToString() + Strings.Right("00" + mes.ToString(), 2);
                            // this.CalcuFecProximoCiclo(stperciclo, Periodd.ToString(), CicloDesc, ref FecIni, ref FecFin, false); // ERROR: CS1503, CS1620
                            if (FecFin.Day == 31)
                            {
                                FecFin = FecFin.AddDays(-1);
                            }
                            // valciclo = msgliqcred.CalculaCiclo(CicloDesc, Periodd.ToString(), FecFin, false); // ERROR: CS1503
                            // if (valciclo == "999999") // ERROR: CS0165
                            {
                                sw1 = 1;
                            }
                        }

                        if (sw1 == 0)
                        {
                            liquidaNovedad(codigoter, lincred, Conselinea, Periodo, ref PeriodoCausa, CapCausado, TipoLinea, myconnect, ref CapCau, ref IntCau, ref TasaUsura);
                            IntCauNov += IntCau;
                            CapCauNov += CapCau;
                            if (TipoLinea != 4 && TipoLinea != 5)
                            {
                                CapCausado += CapCau;
                            }
                        }
                    }

                    if (TipoLinea == 4 || TipoLinea == 5)
                    {
                        CapCauNov = 0;
                    }
                    break;
            }
        }

        // ==========================================================================
        // liquidaNovedad
        // ==========================================================================
        private void liquidaNovedad(string Codigoter, int Lincred, double ConseLinea, string Periodo, ref int PeriodoCausa, double Capcausado, int TipoLinea, OdbcConnection myconnect,
            ref double CapCau, ref double IntCau, ref double TasaUsura)
        {
            double Saldo = 0;
            decimal Tasai = 0;
            double Cuota = 0;
            int Claint = 0;
            int TipCuo = 0;
            int Intcie = 0;
            int Periode = 0;
            int CicloDesc = 0;
            decimal puntosdtf = 0;
            DateTime FechaIni;
            DateTime FechaFin;
            DataSet dslinea = new DataSet();
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcredito = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();

            // this.BuscaObligacion(Codigoter, Lincred, ConseLinea, myconnect, ref Claint, ref TipCuo, ref puntosdtf); // ERROR: CS7036
            // this.BuscaSaldoObligacion(Codigoter, Lincred, ConseLinea, Periodo, myconnect, ref Saldo, ref Cuota, ref Tasai, ref CicloDesc, ref Periode); // ERROR: CS7036

            if (Lincred >= 1000)
            {
                // this.msgconfig.BuscaLinea(Lincred, ref dslinea, myconnect); // ERROR: CS1615, CS1620
                DataRow lineaRow = dslinea.Tables["tbllineas"].Rows[0];
                if (lineaRow["tipointeres"].ToString() == "1")
                {
                    // Tasai = clsliqcredito.ConversionDTFaNMV(lineaRow["dtf"], puntosdtf); // ERROR: CS1503
                }
            }

            if (TasaUsura > 0 && (double)Tasai > TasaUsura)
            {
                Tasai = (decimal)TasaUsura;
            }

            Tasai = Tasai / 100;
            Saldo -= Capcausado;

            if (Lincred >= 1000)
            {
                if (Saldo <= 0)
                {
                    IntCau = 0;
                    CapCau = 0;
                    return;
                }
            }

            if (Periode.ToString() == "4" && CicloDesc.ToString() == "5")
            {
                Tasai = (decimal)((double)Tasai / 30 * 7);
            }

            IntCau = this.calcula_interes(Saldo, (double)Tasai, Cuota, Claint.ToString(), TipCuo.ToString(), Intcie);

            if (Periode.ToString() != "4" && CicloDesc.ToString() == "5")
            {
                IntCau = Math.Round(IntCau / Periode, 0);
            }

            switch (TipCuo)
            {
                case 1:
                    CapCau = Cuota - IntCau;
                    break;
                case 2:
                    CapCau = Cuota;
                    break;
            }
        }

        // ==========================================================================
        // CuotaExtraXpagar
        // ==========================================================================
        public bool CuotaExtraXpagar(string CodigoAso, int Lincred, double NumeroObliga, int PeriodoConta, DateTime FechaIni, DateTime FechaFin, OdbcConnection Myconec, ref double Saldo)
        {
            string sql;
            bool OK=false;
            Saldo = 0;
            sql = "Select b.saldo as campo1 from cop_extras a inner join cop_salextras b on "
                + "b.num_Extra = a.num_extra and b.lincred = a.lincred and b.codigoter = a.codigoter and "
                + " b.periodo = " + PeriodoConta + " where a.FECHA_PAGO between '" + Strings.Format(FechaIni, varini.PstForFec) + "' and '"
                + Strings.Format(FechaFin, varini.PstForFec) + "' and b.saldo > 0 and a.lincred = '" + Lincred + "' and a.numero  = " + NumeroObliga;
            // OK = this.OdbcConnect.ExecuteQueryconec(sql, Myconec, "CuotaExtraXpagar", ref Saldo); // ERROR: CS1503
             return OK; // ERROR: CS0165
        }

        // ==========================================================================
        // CargaRecogeDeudas
        // ==========================================================================
        public void CargaRecogeDeudas(double NumeroSolicitud, string Codigoter, int Lincred, double ValorCredito, DateTime Fecha, Form Myforma, OdbcConnection Myconnet)
        {
            // frmRecDeudas FrmRecDeudas = new frmRecDeudas(Myconnet); // ERROR: CS0246
            string Nombre = " ";
            string DescLinea = " ";
            string Periodo;
            Periodo = Strings.Format(Fecha, "yyyyMM");
            // this.BuscaAsociado(Codigoter, Myconnet, ref Nombre); // ERROR: CS7036
            // this.BuscaLinea(Lincred, Myconnet, ref DescLinea); // ERROR: CS1503

            // FrmRecDeudas.txtCodigoter.Text = Codigoter; // ERROR: CS0103
            // FrmRecDeudas.txtLinea.Text = Lincred.ToString(); // ERROR: CS0103
            // FrmRecDeudas.LblNombre.Text = Nombre; // ERROR: CS0103
            // FrmRecDeudas.LblDescLinea.Text = DescLinea; // ERROR: CS0103
            // FrmRecDeudas.LblCredito.Text = ValorCredito.ToString(); // ERROR: CS0103
            // FrmRecDeudas.DtpFecha.Value = Fecha; // ERROR: CS0103
            // FrmRecDeudas.periodo = Periodo; // ERROR: CS0103
            // FrmRecDeudas.DatGriCreditos.DataSource = CargaCreditos(Codigoter, Fecha.ToString("yyyyMM"), Myconnet); // ERROR: CS0103
            // FrmRecDeudas.Tag = NumeroSolicitud; // ERROR: CS0103
            // FrmRecDeudas.ShowDialog(Myforma); // ERROR: CS0103

            // FrmRecDeudas.Close(); // ERROR: CS0103
            // FrmRecDeudas.Dispose(); // ERROR: CS0103
        }

        // CargaCreditos, BuscaDeudaRecogida, GrabaDeudaRecogida → see Clscartera.Part5.cs

    } // end partial class Clscartera
} // end namespace msgcop
