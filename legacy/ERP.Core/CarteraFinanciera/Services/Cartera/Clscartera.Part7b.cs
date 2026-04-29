using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using Microsoft.VisualBasic;
#if CRYSTAL_LEGACY
using CrystalDecisions.CrystalReports.Engine;
#endif

namespace ERP.Core.CarteraFinanciera.Services.Cartera
{
    public partial class Clscartera
    {
        // Methods from VB lines 20869-22665

        private void GrabaBaseRedAportes(int Periodo, double Exedentes, string Estado, System.Windows.Forms.Form Myforma, OdbcConnection Myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            int fila = 0;
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Redistribucion de aportes ...Espere un momento", Myforma);
            DateTime FecFin = DateTime.MinValue;
            DateTime Fecini = new DateTime(Convert.ToInt32(Strings.Mid(Periodo.ToString(), 1, 4)), 1, 1);
            int Dias = 0;
            double SumProm = 0;
            double VlrBase;
            double VlrRedist = 0;
            double Porcen = 0;
            string SumPromRet = "0";
            double SaldoDia = 0;
            string IgualoIn = " in ";
            string estadoAso = "A";
            string codigoter = "";
            string codigoterTraslado = "";
            string codigoterAnterio = "";

            // msgcofsys.buscaPeriodo("copc", Myconnect, "", ref FecFin, "", "", Periodo, Convert.ToInt32(Strings.Mid(Periodo.ToString(), 1, 4))); // ERROR: CS1503, CS1620

            Dias = (int)DateAndTime.DateDiff(DateInterval.Day, Fecini, FecFin) + 1;

            if (varini.pstTipoBD.ToUpper() == "DB2")
            {
                IgualoIn = "=";
            }

            Stbuilder.Append("select redapo.codigoter,(case when retiros.EstadoAct is null then maenit.estado else retiros.EstadoAct end) as Estado,maenit.fecha_retiro,sum((saldia1 + saldia2 + saldia3 + saldia4 + saldia5 + saldia6 + saldia7 + saldia8 + saldia9 + saldia10 +");
            Stbuilder.Append("saldia11 + saldia12 + saldia13 + saldia14 + saldia15 + saldia16 + saldia17 + saldia18 + saldia19 + saldia20 + ");
            Stbuilder.Append("saldia21 + saldia22 + saldia23 + saldia24 + saldia25 + saldia26 + saldia27 + saldia28 + saldia29 + saldia30 + saldia31) * -1) / " + Dias + " as SaldoDia ");
            Stbuilder.Append("from cop_redapo01_vw redapo inner join sys_maenit maenit on redapo.codigoter = maenit.codigoter ");
            Stbuilder.Append(" left join cop_retiros retiros on maenit.codigoter = retiros.codigoter ");
            Stbuilder.Append(" and retiros.periodo<='" + Periodo + "' and retiros.FecNovedad " + IgualoIn + " (select g.FecNovedad FROM cop_retiros g where g.codigoter=maenit.codigoter ");
            Stbuilder.Append("and  g.periodo<='" + Periodo + "' and g.FecNovedad=(select max(f.fecnovedad) from cop_retiros f where f.codigoter=maenit.codigoter ");
            Stbuilder.Append(" and  f.periodo<='" + Periodo + "')) ");
            Stbuilder.Append("where ");
            Stbuilder.Append("(saldia1 + saldia2 + saldia3 + saldia4 + saldia5 + saldia6 + saldia7 + saldia8 + saldia9 + saldia10 + ");
            Stbuilder.Append(" saldia11 + saldia12 + saldia13 + saldia14 + saldia15 + saldia16 + saldia17 + saldia18 + saldia19 + saldia20 + ");
            Stbuilder.Append(" saldia21 + saldia22 + saldia23 + saldia24 + saldia25 + saldia26 + saldia27 + saldia28 + saldia29 + saldia30 + saldia31) < 0 ");
            Stbuilder.Append("group by redapo.codigoter, maenit.estado,maenit.fecha_retiro, retiros.EstadoAct");

            this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), Myconnect, "RedAportesDiaAnio", DsDataSet, "tblmovtoapo");

            MsgBarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblmovtoapo"].Rows.Count);
            MsgBarra.Show();

            switch (Estado)
            {
                case "A":
                    SumProm = Convert.ToDouble(DsDataSet.Tables["tblmovtoapo"].Compute("sum(SaldoDia)", "saldodia > 0"));
                    SumPromRet = DsDataSet.Tables["tblmovtoapo"].Compute("sum(SaldoDia)", "estado = 'R' and fecha_retiro <= '" + FecFin + "' ").ToString();
                    if (!Information.IsNumeric(SumPromRet))
                    {
                        SumPromRet = "0";
                    }
                    SumProm -= Convert.ToDouble(SumPromRet);
                    break;
                case "T":
                    SumProm = Math.Round(Convert.ToDouble(DsDataSet.Tables["tblmovtoapo"].Compute("sum(SaldoDia)", "saldodia > 0")), 0);
                    break;
            }

            while (fila < DsDataSet.Tables["tblmovtoapo"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblmovtoapo"].Rows[fila];

                switch (Estado)
                {
                    case "A":
                        switch (row["estado"].ToString())
                        {
                            case "R":
                                if (Convert.ToDateTime(row["fecha_retiro"]) <= FecFin)
                                {
                                    SaldoDia += Convert.ToDouble(row["saldodia"]);
                                    row["saldodia"] = 0;
                                }
                                break;
                        }
                        break;
                }

                if (Convert.ToDouble(row["saldodia"]) > 0)
                {
                    Porcen = Math.Round((Convert.ToDouble(row["saldodia"]) / SumProm) * 100, 8);
                    VlrRedist = Math.Round((Exedentes * Porcen) / 100, 0);

                    estadoAso = "A";
                    codigoter = row["codigoter"].ToString();
                    codigoterTraslado = row["codigoter"].ToString();
                    codigoterAnterio = row["codigoter"].ToString();

                    // msgconfig.BuscarEstadoAsociadoXPeriodo(codigoter, Periodo, Myconnect, "", ref estadoAso); // ERROR: CS1061
                    if (estadoAso.Trim() == "T")
                    {
                        // msgconfig.BuscaTrasladado(codigoter, DateTime.Now, ref codigoterTraslado, Myconnect); // ERROR: CS1061
                        codigoter = codigoterTraslado;
                        codigoter = Strings.Right("00000000000000" + codigoter, 14);
                    }

                    GrabaRedistAportes(Periodo, codigoter, Convert.ToDouble(row["saldodia"]), VlrRedist, Myconnect);
                }

                MsgBarra.PerformStep();
                fila += 1;
            }
            MsgBarra.Close();
            MsgBarra.Dispose();
        }

        private void GrabaRedistAportes(int Periodo, string Codigoter, double VlrBase, double VlrRedAportes, OdbcConnection Myconnect)
        {
            Codigoter = Strings.Right("00000000000000" + Codigoter, 14);

            StringBuilder Stbuilder = new StringBuilder();
            switch (Busca_RedistAportes(Periodo, Codigoter, Myconnect))
            {
                case false:
                    Stbuilder.Append("insert into cop_redaportes (PeriodoCorte, Codigoter, VlrBase, VlrRedAportes) values ('");
                    Stbuilder.Append(Periodo + "','");
                    Stbuilder.Append(Codigoter + "','");
                    Stbuilder.Append(VlrBase + "','");
                    Stbuilder.Append(VlrRedAportes + "')");
                    break;
                case true:
                    Stbuilder.Append(" Update cop_redaportes set VlrBase =" + VlrBase + " + VlrBase,VlrRedAportes= " + VlrRedAportes + " + VlrRedAportes ");
                    Stbuilder.Append("  where PeriodoCorte='" + Periodo + "' and Codigoter='" + Codigoter + "'");
                    break;
            }

            this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), Myconnect, "GrabaRedistAportes");
        }

        private void BuscaMovtosRedApo(int Periodo, string estado, System.Windows.Forms.Form Myforma, OdbcConnection Myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Redistribucion de aportes ...Espere un momento", Myforma);
            DateTime FecFin = DateTime.MinValue;
            DateTime Fecini = new DateTime(Convert.ToInt32(Strings.Mid(Periodo.ToString(), 1, 4)), 1, 1);
            string StWhere = "";
            string IgualoIn = " in ";
            // msgcofsys.buscaPeriodo("copc", Myconnect, "", ref FecFin, "", "", Periodo, Convert.ToInt32(Strings.Mid(Periodo.ToString(), 1, 4))); // ERROR: CS1503, CS1620

            if (varini.pstTipoBD.ToUpper() == "DB2")
            {
                IgualoIn = "=";
            }

            Stbuilder.Append("select movto.codigoter,movto.lincred,movto.numero,fecha_movto,sum(movto.vlr_debito) Debito,sum(movto.vlr_credito) Credito ");
            Stbuilder.Append("from cop_movimto movto inner join cop_concar12 par12 on movto.lincred = par12.lincred ");
            Stbuilder.Append("inner join sys_maenit maenit on movto.codigoter = maenit.codigoter ");
            switch (estado)
            {
                default:
                    if (estado != "T")
                    {
                        Stbuilder.Append(" left join cop_retiros retiros on maenit.codigoter = retiros.codigoter ");
                        Stbuilder.Append(" and retiros.periodo<='" + Periodo + "' and retiros.FecNovedad " + IgualoIn + " (select g.FecNovedad FROM cop_retiros g where g.codigoter=maenit.codigoter ");
                        Stbuilder.Append("and  g.periodo<='" + Periodo + "' and g.FecNovedad=(select max(f.fecnovedad) from cop_retiros f where f.codigoter=maenit.codigoter ");
                        Stbuilder.Append(" and  f.periodo<='" + Periodo + "')) ");
                    }
                    break;
            }
            Stbuilder.Append("where par12.codahor = 1 and maenit.clase = 5 and fecha_movto between '" + Strings.Format(Fecini, varini.PstForFec) + "' and '" + Strings.Format(FecFin, varini.PstForFec) + "' ");
            switch (estado)
            {
                case "A":
                    Stbuilder.Append(" and (maenit.estado='A' or maenit.estado= 'S' or maenit.estado= 'R') and  ( retiros.EstadoAct ='A' or retiros.EstadoAct ='S' or retiros.EstadoAct is null)  ");
                    break;
                case "R":
                    Stbuilder.Append(" and (maenit.estado='A' or maenit.estado='R' or maenit.estado= 'S') and  retiros.EstadoAct ='R' ");
                    break;
            }
            Stbuilder.Append("group by movto.codigoter,movto.lincred,movto.numero,movto.fecha_movto");

            this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), Myconnect, "RedAportesDiaAnio", DsDataSet, "tblmovtoapo");

            MsgBarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblmovtoapo"].Rows.Count);
            MsgBarra.Show();

            while (fila < DsDataSet.Tables["tblmovtoapo"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblmovtoapo"].Rows[(int)fila];
                GrabaRedapo(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDateTime(row["fecha_movto"]), 0, Convert.ToDouble(row["debito"]), Convert.ToDouble(row["credito"]), Myconnect);

                MsgBarra.PerformStep();
                fila += 1;
            }

            MsgBarra.Close();
            MsgBarra.Dispose();
        }

        private void BuscaSaldoInicialRedApo(int Periodo, string estado, System.Windows.Forms.Form Myforma, OdbcConnection Myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Busca Saldos Iniciales ...Espere un momento", Myforma);
            DateTime FecFin = DateTime.MinValue;
            DateTime Fecini = new DateTime(Convert.ToInt32(Strings.Mid(Periodo.ToString(), 1, 4)), 1, 1);
            DateTime Fechamovto;
            string IgualoIn = " in ";
            // msgcofsys.buscaPeriodo("copc", Myconnect, "", ref FecFin, "", "", Periodo, Convert.ToInt32(Strings.Mid(Periodo.ToString(), 1, 4))); // ERROR: CS1503, CS1620

            if (varini.pstTipoBD.ToUpper() == "DB2")
            {
                IgualoIn = "=";
            }

            Stbuilder.Append("select salmae.codigoter,salmae.lincred,salmae.numero,salmae.periodo,sum(saldo_inicial) SaldoInicial ");
            Stbuilder.Append("from cop_salmaecar salmae inner join cop_concar12 par12 on salmae.lincred = par12.lincred ");
            Stbuilder.Append("inner join sys_maenit maenit on salmae.codigoter = maenit.codigoter ");
            if (estado != "T")
            {
                Stbuilder.Append(" left join cop_retiros retiros on maenit.codigoter = retiros.codigoter ");
                Stbuilder.Append(" and retiros.periodo<='" + Periodo + "' and retiros.FecNovedad " + IgualoIn + " (select g.FecNovedad FROM cop_retiros g where g.codigoter=maenit.codigoter ");
                Stbuilder.Append("and  g.periodo<='" + Periodo + "' and g.FecNovedad=(select max(f.fecnovedad) from cop_retiros f where f.codigoter=maenit.codigoter ");
                Stbuilder.Append(" and  f.periodo<='" + Periodo + "')) ");
            }
            Stbuilder.Append("where par12.codahor = 1 and maenit.clase = 5 and salmae.periodo between '" + Strings.Format(Fecini, "yyyyMM") + "' and '" + Strings.Format(FecFin, "yyyyMM") + "' ");
            switch (estado)
            {
                case "A":
                    Stbuilder.Append(" and (maenit.estado='A' or maenit.estado= 'S' or maenit.estado= 'R') and  ( retiros.EstadoAct ='A' or retiros.EstadoAct ='S' or retiros.EstadoAct is null)  ");
                    break;
                case "R":
                    Stbuilder.Append(" and (maenit.estado='A' or maenit.estado='R' or maenit.estado= 'S') and  retiros.EstadoAct ='R'  ");
                    break;
            }
            Stbuilder.Append("group by salmae.codigoter,salmae.lincred,salmae.numero,salmae.periodo");

            this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), Myconnect, "BuscaSaldoInicialRedApo", DsDataSet, "tblSalIniApo");

            MsgBarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblSalIniApo"].Rows.Count);
            MsgBarra.Show();

            while (fila < DsDataSet.Tables["tblSalIniApo"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblSalIniApo"].Rows[(int)fila];
                Fechamovto = new DateTime(Convert.ToInt32(Strings.Mid(row["periodo"].ToString(), 1, 4)), Convert.ToInt32(Strings.Mid(row["periodo"].ToString(), 5, 2)), 1);
                GrabaRedapo(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Fechamovto, Convert.ToDouble(row["SaldoInicial"]), 0, 0, Myconnect);

                MsgBarra.PerformStep();
                fila += 1;
            }

            MsgBarra.Close();
            MsgBarra.Dispose();
        }

        private double EliminaCopRedapo(int Periodo, OdbcConnection Myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            Stbuilder.Append("delete from cop_redapo ");
            this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), Myconnect, "EliminaCopRedapo");
            return 0;
        }

        private void GrabaRedapo(string codigoter, int lincred, DateTime fecha, double Salini, double debito, double Credito, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            string[] StInsCampo = new string[] { "SalDia0", "SalDia1", "SalDia2", "SalDia3", "SalDia4", "SalDia5", "SalDia6", "SalDia7", "SalDia8", "SalDia9", "SalDia10", "SalDia11", "SalDia12", "SalDia13", "SalDia14", "SalDia15",
                                     "SalDia16", "SalDia17", "SalDia18", "SalDia19", "SalDia20", "SalDia21", "SalDia22", "SalDia23", "SalDia24", "SalDia25", "SalDia26", "SalDia27", "SalDia28", "SalDia29", "SalDia30", "SalDia31" };
            string[] StUpdcampo = new string[] { "SalDia0", "SalDia1 = SalDia1 + '", "SalDia2 = SalDia2 + '", "SalDia3 = SalDia3 + '", "SalDia4 = SalDia4 + '", "SalDia5 = SalDia5 + '", "SalDia6 = SalDia6 + '", "SalDia7 = SalDia7 + '",
                                      "SalDia8 = SalDia8 + '", "SalDia9 = SalDia9 + '", "SalDia10 = SalDia10 + '", "SalDia11 = SalDia11 + '", "SalDia12 = SalDia12 + '", "SalDia13 = SalDia13 + '", "SalDia14 = SalDia14 + '", "SalDia15 = SalDia15 + '",
                                      "SalDia16 = SalDia16 + '", "SalDia17 = SalDia17 + '", "SalDia18 = SalDia18 + '", "SalDia19 = SalDia19 + '", "SalDia20 = SalDia20 + '", "SalDia21 = SalDia21 + '", "SalDia22 = SalDia22 + '", "SalDia23 = SalDia23 + '",
                                      "SalDia24 = SalDia24 + '", "SalDia25= SalDia25 + '", "SalDia26 = SalDia26 + '", "SalDia27 = SalDia27 + '", "SalDia28 = SalDia28 + '", "SalDia29 = SalDia29 + '", "SalDia30 = SalDia30 + '", "SalDia31 = SalDia31 + '" };
            ok = BuscaRedApo(Strings.Format(fecha, "yyyyMM"), codigoter, lincred, myconnect);

            switch (ok)
            {
                case false:
                    Stbuilder.Append("insert into cop_redapo (codigoter,lincred,periodo,Salini," + StInsCampo[fecha.Day] + ") ");
                    Stbuilder.Append("Values ('");
                    Stbuilder.Append(codigoter + "','");
                    Stbuilder.Append(lincred + "','");
                    Stbuilder.Append(Strings.Format(fecha, "yyyyMM") + "','");
                    Stbuilder.Append(Salini + "','");
                    Stbuilder.Append((debito - Credito) + "')");
                    break;
                case true:
                    Stbuilder.Append("update cop_redapo set ");
                    Stbuilder.Append(StUpdcampo[fecha.Day]);
                    Stbuilder.Append((debito - Credito) + "',");
                    Stbuilder.Append("Salini = Salini + '");
                    Stbuilder.Append(Salini + "'");
                    Stbuilder.Append(" where codigoter = '" + codigoter + "' and lincred = '" + lincred + "' and periodo = '" + Strings.Format(fecha, "yyyyMM") + "'");
                    break;
            }
            this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "GrabaRiesgo");
        }

        // Overload without optional DataSet parameter
        private bool BuscaRedApo(object Periodo, string Codigoter, int Lincred, OdbcConnection Myconnect)
        {
            DataSet ds = null;
            return BuscaRedApo(Periodo, Codigoter, Lincred, Myconnect, ref ds);
        }

        private bool BuscaRedApo(object Periodo, string Codigoter, int Lincred, OdbcConnection Myconnect, ref DataSet DsDataSet)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataEmpresa = new DataSet();

            try
            {
                DsDataSet.Tables.Remove("tblredapo");
            }
            catch (Exception)
            {
            }

            Stbuilder.Append("select periodo,codigoter,lincred,Salini ,saldia1,saldia2,saldia3,saldia4,saldia5,saldia6,saldia7,saldia8,saldia9,saldia10,");
            Stbuilder.Append("saldia11,saldia12,saldia13,saldia14,saldia15,saldia16,saldia17,saldia18,saldia19,saldia20,");
            Stbuilder.Append("saldia21,saldia22,saldia23,saldia24,saldia25,saldia26,saldia27,saldia28,saldia29,saldia30 ");
            Stbuilder.Append("from cop_redapo where periodo = '" + Periodo + "' and codigoter = '" + Codigoter + "' and lincred = '" + Lincred + "'");

            this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), Myconnect, "BuscaRedApo", DsDataEmpresa, "tblredapo");
            if (DsDataEmpresa.Tables["tblredapo"].Rows.Count > 0)
            {
                try
                {
                    DsDataSet.Tables.Add(DsDataEmpresa.Tables["tblredapo"].Copy());
                    return true;
                }
                catch (Exception)
                {
                    return true;
                }
            }
            return false;
        }

        public DataSet CargaSolicitudesAprobacion(string NroSolicitud, DateTime fechaini, DateTime fechafin, string codigoter,
            string estado, string estudiocredito, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string whereestado = "";
            string wherecodigoter = "";
            string where_ = "";
            DataSet dsdataset_ = new DataSet();

            NroSolicitud = NroSolicitud.Trim();
            if (NroSolicitud != "Todos")
            {
                whereestado = " numero= " + NroSolicitud + " and estado<>'X' " + (estudiocredito != "Y" ? "" : " and estado<>'P'");
            }
            else
            {
                where_ = " fecha_soli between '" + Strings.Format(fechaini, varini.PstForFec) + "' and '" + Strings.Format(fechafin, varini.PstForFec) + "' ";
                if (estado != "T")
                {
                    whereestado = " and estado='" + estado + "'";
                }
                else
                {
                    whereestado = " and estado<>'X' " + (estudiocredito != "Y" ? "" : " and estado<>'P'");
                }
                codigoter = codigoter.Trim();
                if (codigoter != "Todos")
                {
                    wherecodigoter = " and codigoter='" + Strings.Right("00000000000000" + codigoter, 14) + "'";
                }
            }

            stbuilder.Append("select numero,fecha_soli,codigoter,linea,vlr_solicitud,tasa_int,plazo,NombreAso,");
            stbuilder.Append("estado,fecdesc,clades,VALOR_APROBADO ");
            stbuilder.Append("from cop_solcre_vw ");
            stbuilder.Append("where " + where_ + whereestado + wherecodigoter);

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargaSolicitudesAprobacion", dsdataset_, "tblsolicitudes");
            return dsdataset_;
        }

        public DataSet CargaDatosEstudioExclusion(string BuscarPor, string codigo, int periodo, string clades,
            int diasmora, string cobroprejuridico, string cobrojuridico,
            string compromiso, DateTime fecini, DateTime fecfin, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string WhereBuscar = "";
            string WhereCobro = "";
            int i = 0;
            string nomcodeudor = "";
            DataSet dsdatcobjur = new DataSet();
            DataSet dsdata = new DataSet();
            DataSet dsdatacodeudores = new DataSet();
            DataSet dsdataobligacion = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress msbarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Informacion", myforma);

            if (codigo != "Todas")
            {
                switch (BuscarPor)
                {
                    case "1":
                        WhereBuscar = " maecar.codigoter='" + Strings.Right("00000000000000" + codigo, 14) + "' and ";
                        break;
                    case "2":
                        WhereBuscar = " maenit.empresa='" + Strings.Right("0000" + codigo, 4) + "' and ";
                        break;
                    case "3":
                        WhereBuscar = " maenit.cencosto='" + Strings.Right("00000000" + codigo, 8) + "' and ";
                        break;
                    case "4":
                        WhereBuscar = " maenit.agencia='" + Strings.Right("0000" + codigo, 4) + "' and ";
                        break;
                }
            }

            if (cobroprejuridico == "N" && cobrojuridico == "N")
            {
                WhereCobro = " (maecar.COBROJUR<>'Y' AND maecar.COBROJUR<>'P') and ";
            }
            else if (cobroprejuridico == "N")
            {
                WhereCobro = " maecar.COBROJUR<>'P' and ";
            }
            else if (cobrojuridico == "N")
            {
                WhereCobro = " maecar.COBROJUR<>'Y' and ";
            }

            stbuilder.Append("select maecar.codigoter,maecar.lincred,maecar.numero,maecar.valorob,sal.cuota,maecar.fecfact,maecar.fecultpago,");
            stbuilder.Append("maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sum(saldointeres) as SaldoInteres,");
            stbuilder.Append("case car12.codahor when '3' then 0 else sal.saldo end as Saldo,case car12.codahor when '3' then sum(saldoCapital) else 0 end as SaldoCapital,");
            stbuilder.Append("sum(saldoseguro+saldoadmon+saldootros) as SaldoOtros, sum(saldomora) as saldomora,sal.clades,car12.descripcion,");
            stbuilder.Append("(select max(a.catego) from cop_copclas a where a.periodo_contable=(select max(b.periodo_contable) from cop_copclas b ");
            stbuilder.Append("  where a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero) ");
            stbuilder.Append(" and a.codigoter=maecar.codigoter and a.lincred=maecar.lincred and a.numero=maecar.numero) as Categoria, ");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='1') as Aportes,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='6') as Cdats,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper=4) as AhorroPermanente,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper<>4) as Ahorros,emp.nombre as nom_empresa,car12.codahor ");
            stbuilder.Append("from cop_maecar maecar ");
            stbuilder.Append("left join cop_salmaecar sal on maecar.codigoter=sal.codigoter and maecar.lincred=sal.lincred and maecar.numero=sal.numero ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred=car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter=maenit.codigoter ");
            stbuilder.Append("inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa ");
            stbuilder.Append("left join cop_copmoracircular_vw cirmora on maecar.codigoter=cirmora.codigoter and maecar.lincred=cirmora.lincred ");
            stbuilder.Append(" and maecar.numero=cirmora.numero and sal.periodo=cirmora.periodo_contable and cirmora.diasmora>=" + diasmora);
            stbuilder.Append(" left join cop_copmora copmora on maecar.codigoter=copmora.codigoter and maecar.lincred=copmora.lincred ");
            stbuilder.Append("and maecar.numero=copmora.numero and sal.periodo=copmora.periodo_contable ");
            stbuilder.Append("where " + WhereBuscar + WhereCobro + " sal.periodo=" + periodo + " and ");
            stbuilder.Append(" (((sal.saldo<>0 or sal.cuota<>0) and car12.codahor='3') or (sal.saldo<>0 and (car12.codahor='4' or car12.codahor='5')) )");
            stbuilder.Append(clades == "3" ? "" : " and sal.clades='" + clades + "' ");
            stbuilder.Append("group by maecar.codigoter,maecar.lincred,maecar.numero,maecar.valorob,sal.cuota,maecar.fecfact,maecar.fecultpago,");
            stbuilder.Append("sal.saldo,car12.codahor,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sal.clades,sal.periodo,car12.descripcion,emp.nombre ");
            stbuilder.Append("order by maecar.codigoter,maecar.lincred,maecar.numero");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargaDatosEstudioExclusion", dsdatcobjur, "tblExclusion");
            switch (ok)
            {
                case true:
                    msbarra.ValorMinimoMaximo(0, dsdatcobjur.Tables["tblExclusion"].Rows.Count);
                    msbarra.Show();

                    while (i < dsdatcobjur.Tables["tblExclusion"].Rows.Count)
                    {
                        dsdata = null;
                        DataRow rowExcl = dsdatcobjur.Tables["tblExclusion"].Rows[i];
                        if (nomcodeudor != (rowExcl["codigoter"].ToString() + rowExcl["nombre"].ToString() + rowExcl["apellido"].ToString()))
                        {
                            dsdata = this.CargaDatosEstudioExclusionCodeudores(rowExcl["codigoter"].ToString(), periodo, myconnect);
                            if (dsdata.Tables.Count != 0)
                            {
                                if (dsdatacodeudores.Tables.Count == 0)
                                {
                                    dsdatacodeudores.Tables.Add(dsdata.Tables["tblcodeudor"].Copy());
                                }
                                else
                                {
                                    for (int j = 0; j <= dsdata.Tables["tblcodeudor"].Rows.Count - 1; j++)
                                    {
                                        DataRow rowCode = dsdata.Tables["tblcodeudor"].Rows[j];
                                        dsdatacodeudores.Tables["tblcodeudor"].Rows.Add(rowCode["codeudor"], rowCode["codigoter"], rowCode["lincred"], rowCode["numero"],
                                            rowCode["valorob"], rowCode["cuota"], rowCode["fecfact"], rowCode["fecultpago"], rowCode["CLASEGAR"], rowCode["diasmora"], rowCode["fogacla"], rowCode["nombre"],
                                            rowCode["apellido"], rowCode["SaldoInteres"], rowCode["saldo"], rowCode["SaldoCapital"], rowCode["SaldoOtros"], rowCode["saldomora"], rowCode["clades"], rowCode["descripcion"], rowCode["Categoria"], rowCode["Aportes"],
                                            rowCode["Cdats"], rowCode["AhorroPermanente"], rowCode["Ahorros"], rowCode["nom_empresa"]);
                                    }
                                }
                            }
                            dsdata = null;
                            nomcodeudor = rowExcl["codigoter"].ToString() + rowExcl["nombre"].ToString() + rowExcl["apellido"].ToString();
                        }

                        i += 1;
                        msbarra.PerformStep();
                    }
                    msbarra.Dispose();
                    msbarra.Close();
                    if (dsdatacodeudores.Tables.Count == 0)
                    {
                        ConfigDatasetEstExclusionCodeudor(ref dsdatacodeudores);
                    }
                    dsdatcobjur.Tables.Add(dsdatacodeudores.Tables["tblcodeudor"].Copy());
                    break;
            }
            return dsdatcobjur;
        }

        public void ConfigDatasetEstExclusionCodeudor(ref DataSet dataset)
        {
            string StString = " ";
            int StInteger = 0;
            double StDouble = 0;
            DateTime StFecha = DateTime.Now;

            dataset.Tables.Add("tblcodeudor");
            dataset.Tables["tblcodeudor"].Columns.Add("codeudor", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("codigoter", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("lincred", StInteger.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("numero", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("valorob", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("cuota", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("fecfact", StFecha.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("fecultpago", StFecha.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("saldo", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("CLASEGAR", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("diasmora", StInteger.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("fogacla", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("nombre", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("apellido", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("SaldoInteres", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("SaldoOtros", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("saldomora", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("clades", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("descripcion", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("Categoria", StString.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("Aportes", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("Cdats", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("AhorroPermanente", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("Ahorros", StDouble.GetType());
            dataset.Tables["tblcodeudor"].Columns.Add("nom_empresa", StString.GetType());

            dataset.Tables["tblcodeudor"].Rows.Add("", "", 0, 0, 0, 0, DateTime.Now, DateTime.Now, 0, "", 0, "", "", "", 0, 0, 0, "", "", "", 0, 0, 0, 0, "");
        }

        public DataSet CargaDatosEstudioExclusionCodeudores(string codigoter, int periodo, OdbcConnection myconnect)
        {
            DataSet dsCodeudores = new DataSet();
            DataSet dsdata = new DataSet();
            int fila = 0;
            int l = 0;
            DataSet dsdatosdata = new DataSet();
            DateTime fecha = new DateTime(1950, 1, 1);

            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("select '" + codigoter + "' as codeudor,maecar.codigoter,maecar.lincred, maecar.numero, maecar.valorob, sal.cuota, maecar.fecfact, maecar.fecultpago, ");
            stbuilder.Append("sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sum(saldointeres) as SaldoInteres,");
            stbuilder.Append("sum(saldoseguro+saldoadmon+saldootros) as SaldoOtros, sum(saldomora) as saldomora,sal.clades,car12.descripcion,");
            stbuilder.Append("(select max(a.catego) from cop_copclas a where a.periodo_contable=(select max(b.periodo_contable) from cop_copclas b ");
            stbuilder.Append("  where a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero) ");
            stbuilder.Append(" and a.codigoter=maecar.codigoter and a.lincred=maecar.lincred and a.numero=maecar.numero) as Categoria, ");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='1') as Aportes,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='6') as Cdats,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper=4) as AhorroPermanente,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper<>4) as Ahorros,emp.nombre as nom_empresa ");
            stbuilder.Append("from cop_maecar maecar ");
            stbuilder.Append("inner join cop_salmaecar sal on maecar.codigoter=sal.codigoter and maecar.lincred=sal.lincred and maecar.numero=sal.numero ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred=car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter=maenit.codigoter ");
            stbuilder.Append("inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa ");
            stbuilder.Append("left join cop_copmoracircular_vw cirmora on maecar.codigoter=cirmora.codigoter and maecar.lincred=cirmora.lincred ");
            stbuilder.Append(" and maecar.numero=cirmora.numero and sal.periodo=cirmora.periodo_contable ");
            stbuilder.Append("left join cop_copmora copmora on maecar.codigoter=copmora.codigoter and maecar.lincred=copmora.lincred ");
            stbuilder.Append("and maecar.numero=copmora.numero and sal.periodo=copmora.periodo_contable ");
            stbuilder.Append("where sal.periodo=" + periodo + " and (maecar.codeudor1='" + codigoter + "' or maecar.codeudor2='" + codigoter + "' or maecar.codeudor3='" + codigoter + "' or maecar.codeudor4='" + codigoter + "') ");
            stbuilder.Append("and maecar.codigoter<>'" + codigoter + "' and ((sal.saldo<>0 and maecar.lincred>=1000) or ((sal.saldo<>0 or sal.cuota<>0) and maecar.lincred<1000)) ");
            stbuilder.Append("group by maecar.codigoter,maecar.lincred,maecar.numero,maecar.valorob,sal.cuota,maecar.fecfact,maecar.fecultpago,");
            stbuilder.Append("sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sal.clades,sal.periodo,car12.descripcion,emp.nombre ");
            stbuilder.Append("order by maecar.codigoter,maecar.lincred,maecar.numero");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargaDatosEstudioExclusionCodeudores", dsCodeudores, "tblcodeudor");
            return dsCodeudores;
        }

        public string BuscaAsociadosNoGestionados(DateTime FechaProceso, int diasini, int diasfin, int lineaini, int lineafin,
            string EmpresaIni, string EmpresaFin, string Clades, string CobJur, string Ordenamiento, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string wherecobro = "";
            string whereclades = "";
            string wherecobro2 = "";
            string whereclades2 = "";
            DataSet dsdata = new DataSet();
            string ordenar = "";
            string cedula = "";
            int i = 0;

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    stmysql = " and {fn concat(gescob.codigoter,{fn concat(rtrim(gescob.lincred),rtrim(gescob.numero))})} not in " +
                            "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                            "from cop_novfecgestion nov " +
                            "where '" + Strings.Format(FechaProceso, varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                            "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                    break;
                case "MYSQL":
                case "ORACLE":
                case "POSTGRESQL":
                    stmysql = " and concat(gescob.codigoter,concat(rtrim(gescob.lincred),rtrim(gescob.numero))) not in " +
                              "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                              "from cop_novfecgestion nov " +
                              "where '" + Strings.Format(FechaProceso, varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                              "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                    break;
                case "DB2":
                    stmysql = " and (gescob.codigoter || (rtrim(gescob.lincred) || rtrim(gescob.numero))) not in " +
                            "(select (nov.codigoter || (rtrim(nov.lincred) || rtrim(nov.numero))) " +
                            "from cop_novfecgestion nov " +
                            "where '" + Strings.Format(FechaProceso, varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                            "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                    break;
            }

            switch (Clades)
            {
                case "N":
                    whereclades = " and sal.clades='1' ";
                    whereclades2 = " and sal.clades='1' ";
                    break;
                case "C":
                    whereclades = " and sal.clades='2' ";
                    whereclades2 = " and sal.clades='2' ";
                    break;
            }

            switch (CobJur)
            {
                case "N":
                    wherecobro = " and maecar.cobrojur<>'Y' and maecar.cobrojur<>'P' ";
                    wherecobro2 = " and maecar2.cobrojur<>'Y' and maecar2.cobrojur<>'P' ";
                    break;
                case "P":
                    wherecobro = " and maecar.cobrojur<>'Y' ";
                    wherecobro2 = " and maecar2.cobrojur<>'Y' ";
                    break;
                case "Y":
                    wherecobro = " and maecar.cobrojur<>'P' ";
                    wherecobro2 = " and maecar2.cobrojur<>'P' ";
                    break;
            }

            switch (Ordenamiento)
            {
                case "0":
                    ordenar = "order by gescob.codigoter";
                    break;
                case "1":
                    ordenar = "order by SaldoDeuda desc";
                    break;
                case "2":
                    ordenar = "order by diasmora desc";
                    break;
            }

            stbuilder.Append("select gescob.codigoter,max(gescob.diasmora) as diasmora,");
            stbuilder.Append("(select sum(saldocapital + saldoextra +saldointeres+ saldoseguro+ saldoadmon+saldootros +saldomora) ");
            stbuilder.Append("from cop_copmora copmora2 ");
            stbuilder.Append("inner join cop_maecar maecar2 on copmora2.codigoter=maecar2.codigoter and copmora2.lincred=maecar2.lincred and copmora2.numero=maecar2.numero ");
            stbuilder.Append(wherecobro2 + whereclades2 + " where copmora2.codigoter = gescob.codigoter and copmora2.periodo_contable =gescob.periodo_contable ");
            stbuilder.Append("and (saldocapital>0 or saldointeres>0 or saldoseguro>0 or saldoadmon>0 or saldootros>0 or saldoextra>0 or saldomora>0) ");
            stbuilder.Append("and copmora2.diasmora between " + diasini + " and " + diasfin + " and copmora2.lincred between " + lineaini + " and " + lineafin + ") as SaldoDeuda  ");
            stbuilder.Append("from cop_buscagestion_vw gescob ");
            stbuilder.Append("inner join sys_maenit maenit on gescob.codigoter=maenit.codigoter and maenit.engestion='N' ");
            stbuilder.Append("inner join cop_maecar maecar on gescob.codigoter=maecar.codigoter and gescob.lincred=maecar.lincred and ");
            stbuilder.Append("gescob.numero=maecar.numero " + wherecobro + " ");
            stbuilder.Append("inner join cop_salmaecar sal on maecar.codigoter=sal.codigoter and maecar.lincred=sal.lincred and ");
            stbuilder.Append("maecar.numero=sal.numero and sal.periodo=gescob.periodo_contable " + whereclades);
            stbuilder.Append(" where gescob.periodo_contable=" + Strings.Format(FechaProceso, "yyyyMM") + " and gescob.diasmora between  " + diasini + " and " + diasfin);
            stbuilder.Append(" and gescob.lincred between " + lineaini + " and " + lineafin + " and ((maecar.lincred>=1000 and sal.saldo>0) or (maecar.lincred<1000 and (sal.saldo<>0 or sal.cuota<>0))) ");
            stbuilder.Append("and maenit.empresa between '" + EmpresaIni + "' and '" + EmpresaFin + "' " + stmysql);
            stbuilder.Append("group by gescob.codigoter,gescob.periodo_contable,sal.clades " + ordenar);

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAsociadosNoGestionados", dsdata, "tblAsoNoGes");
            switch (ok)
            {
                case true:
                    ok = false;
                    while (i < dsdata.Tables["tblAsoNoGes"].Rows.Count)
                    {
                        ok = ActAsociadoGestionadoMaenit(dsdata.Tables["tblAsoNoGes"].Rows[i]["codigoter"].ToString(), "Y", myconnect);
                        switch (ok)
                        {
                            case true:
                                cedula = dsdata.Tables["tblAsoNoGes"].Rows[i]["codigoter"].ToString();
                                goto ExitWhile_BuscaAso;
                        }
                        i += 1;
                    }
                    ExitWhile_BuscaAso:;
                    break;
            }
            return cedula;
        }

        public bool ActAsociadoGestionadoMaenit(string codigoter, string Novedad, OdbcConnection myconnect)
        {
            bool okk = false;
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "update sys_maenit set engestion='" + Novedad + "' where codigoter='" + codigoter + "' and engestion<>'" + Novedad + "'";
            okk = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActAsociadoGestionadoMaenit");
            return okk;
        }

        public bool ActMaestroGestionAsociado(string codigoter, double IdGestion, int periodo, OdbcConnection myconnect)
        {
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "update cop_maegescob set Gestionado='Y' where codigoter='" + codigoter + "' and secuencia<" + IdGestion + " and periodo=" + periodo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActAsociadoGestionadoMaenit");
            return ok;
        }

        public DataTable CargaDatosGestionCobros(string codigoter, DateTime fecha, string cobjuridico, string cobprejuridico, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            OdbcCommand Mycommand = new OdbcCommand();
            DataSet Datset = new DataSet();
            string wherecobjur = "";

            if (cobjuridico == "N" && cobprejuridico == "N")
            {
                wherecobjur = " and maecar.cobrojur<>'Y' and maecar.cobrojur<>'P' ";
            }
            else if (cobprejuridico == "N")
            {
                wherecobjur = " and maecar.cobrojur<>'P' ";
            }
            else if (cobjuridico == "N")
            {
                wherecobjur = " and maecar.cobrojur<>'Y' ";
            }

            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stbuilder.Append("select maecar.lincred,maecar.numero,car12.descripcion,sal.cuota,");
            stbuilder.Append("case car12.codahor when '4' then sal.saldo when '5' then sal.saldo else sal.saldo*-1 end as saldo,");
            stbuilder.Append("(copmora.saldocapital+copmora.saldoextra) as capital,copmora.saldointeres as interes,");
            stbuilder.Append("copmora.saldomora as mora,(copmora.saldoseguro+copmora.saldoadmon+copmora.saldootros) as otros,");
            stbuilder.Append("(case (case car12.codahor when '4' then 'C' when '5' then 'C' else 'A' end) when 'C' then sal.saldo else copmora.saldocapital end +");
            stbuilder.Append("copmora.saldointeres+copmora.saldomora+copmora.saldoseguro+copmora.saldoadmon+copmora.saldootros) as Total,");
            stbuilder.Append("gest.diasmora, maecar.FECULTPAGO, car12.codahor, ");
            stbuilder.Append("(SELECT sum(mor.SaldoCapital+mor.SaldoExtra+mor.SaldoInteres+mor.SaldoMora+mor.SaldoSeguro+mor.SaldoAdmon+mor.SaldoOtros) from cop_copmora mor ");
            stbuilder.Append("inner join cop_cuopen cuopen on mor.codigoter = cuopen.codigoter and mor.lincred = cuopen.lincred and ");
            stbuilder.Append("mor.numero = cuopen.numero And mor.periodo_contable = cuopen.periodo_contable And mor.periodo_causa = cuopen.periodo_causa ");
            stbuilder.Append("where mor.codigoter = maecar.codigoter and mor.lincred =maecar.lincred and mor.numero = maecar.numero and ");
            stbuilder.Append("mor.Periodo_contable = sal.periodo and cuopen.fecha_movto>'" + Strings.Format(fecha, varini.PstForFec) + "') as CuotaCiclo ");
            stbuilder.Append("from cop_maecar maecar ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred=car12.lincred ");
            stbuilder.Append("inner join cop_salmaecar sal on maecar.codigoter=sal.codigoter and maecar.lincred=sal.lincred and maecar.numero = sal.numero ");
            stbuilder.Append("inner join cop_copmora_vw copmora on maecar.codigoter = copmora.codigoter and maecar.lincred = copmora.lincred and ");
            stbuilder.Append("maecar.numero = copmora.numero And copmora.periodo_contable = sal.periodo ");
            stbuilder.Append("inner join cop_copmoracircular_vw gest on maecar.codigoter = gest.codigoter and maecar.lincred = gest.lincred and ");
            stbuilder.Append("maecar.numero = gest.numero And gest.periodo_contable = sal.periodo ");
            stbuilder.Append("where maecar.codigoter='" + codigoter + "' and sal.periodo=" + Strings.Format(fecha, "yyyyMM") + wherecobjur + " and ");
            stbuilder.Append("((maecar.lincred>=1000 and sal.saldo<>0) or (maecar.lincred<1000 and (sal.cuota<>0 or sal.saldo<>0)))");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "Carga Datos Gestion Cobros", Datset, "TblObliGestion");

            return Datset.Tables["TblObliGestion"];
        }

        // Overload without optional detalle parameter
        public DataSet BuscarUltimaGestionAsociado(string codigoter, OdbcConnection myconnect)
        {
            string detalle = "";
            return BuscarUltimaGestionAsociado(codigoter, myconnect, ref detalle);
        }

        public DataSet BuscarUltimaGestionAsociado(string codigoter, OdbcConnection myconnect, ref string detalle)
        {
            StringBuilder stbuilder = new StringBuilder();
            StringBuilder stbuilder2 = new StringBuilder();
            DataSet dsdata = new DataSet();
            DataSet dsdetalle = new DataSet();
            double cant = 0;
            double i = 0;

            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stbuilder.Append("select mae.secuencia,mae.fechagestion,mae.estado,mae.detalle,mae.usuario,mae.ValorTotalAtrasado,mae.periodo,usu.nombre ");
            stbuilder.Append("from cop_maegescob mae ");
            stbuilder.Append("inner join sys_sasusu usu on mae.usuario=usu.login ");
            stbuilder.Append("where mae.codigoter='" + codigoter + "' and ");
            stbuilder.Append("mae.secuencia=(select max(secuencia) from cop_maegescob ges where ges.codigoter='" + codigoter + "') ");

            stbuilder2.Append("select mae.secuencia,mae.detalle ");
            stbuilder2.Append("from cop_maegescob mae ");
            stbuilder2.Append("where mae.codigoter='" + codigoter + "' and acumulado = 'Y' order by mae.secuencia asc");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder2.ToString(), myconnect, "BuscarUltimaGestionAsociadoDet", dsdetalle, "tblultgestiondet");
            cant = dsdetalle.Tables["tblultgestiondet"].Rows.Count;
            switch (ok)
            {
                case true:
                    while (i < cant)
                    {
                        detalle += dsdetalle.Tables["tblultgestiondet"].Rows[(int)i]["detalle"].ToString() + ((char)13).ToString();
                        i += 1;
                    }
                    break;
            }
            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarUltimaGestionAsociado", dsdata, "tblultgestion");
            return dsdata;
        }

        public virtual bool BuscarParametrosCobranza(string usuario, int periodo, ref DataSet dsdata, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select usuario,periodo,ultcodigoter,dia_ini,dia_fin,lincred_ini,lincred_fin,EmpresaIni,EmpresaFin,clades,cobjuridico,Orden ");
            stbuilder.Append("from cop_gespara ");
            stbuilder.Append("where usuario ='" + usuario + "' and periodo=" + periodo);

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarParametrosCobranza", dsdata, "tblparametro");
            return ok;
        }

        public virtual bool BuscarGestionCobranza(int periodo, string codigoter, int lincred, double numero,
            int IdGesCob, OdbcConnection myconnect, ref DataSet dsdata)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select codigoter,periodo,lincred,numero,IdCodmaeges,usuario,fecha_gestion,fecha_pago,OtrosAtr,saldot,capatr,intatr,moracum,cuota,diasmora ");
            stbuilder.Append("from cop_gesmaes ");
            stbuilder.Append("where codigoter ='" + codigoter + "' and periodo=" + periodo + " and lincred= " + lincred + " and numero=" + numero + " and IdCodmaeges=" + IdGesCob);

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarParametrosCobranza", dsdata, "tbldetallegestion");
            return ok;
        }

        public bool GrabarDatosGestionCobros(DateTime fechaproceso, string usuario, string estado, double idsecuencia,
            DataSet dsdatos, OdbcConnection myconnect)
        {
            double IdGestion = 0;
            ok = false;
            IdGestion = GrabaMaestroCobros(idsecuencia, fechaproceso, usuario, dsdatos.Tables["tblMaestro"], myconnect);
            if (IdGestion > 0)
            {
                ok = GrabaDetalleCobros(fechaproceso, usuario, IdGestion, estado, dsdatos.Tables["tblDetalle"], myconnect);
            }
            return ok;
        }

        public double GrabaMaestroCobros(double IdGestion, DateTime fecha, string usuario, DataTable dttable, OdbcConnection myconnect)
        {
            double consecutivo = 0;
            DataSet dsdata = new DataSet();
            StringBuilder stbuilder = new StringBuilder();
            string FechaSys;

            DataRow row = dttable.Rows[0];
            if (IdGestion != 0)
            {
                stbuilder.Append("update cop_maegescob set Detalle='" + row["Detalle"] + "' ");
                stbuilder.Append("where secuencia=" + IdGestion);
                ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaMaestroCobros");
                consecutivo = IdGestion;
            }
            else
            {
                switch (varini.pstTipoBD.ToUpper())
                {
                    case "DB2":
                        FechaSys = Strings.Format(row["FechaGestion"], varini.PstForFec) + "-" + Strings.Format(DateTime.Now, varini.pstForHora);
                        break;
                    default:
                        FechaSys = Strings.Format(Convert.ToDateTime(Strings.Format(row["FechaGestion"], varini.PstForFec) + " " + Strings.Format(DateTime.Now, varini.pstForHora)), varini.pstForfecyHora);
                        break;
                }
                stbuilder.Append("insert into cop_maegescob (Periodo, Usuario, Codigoter, FechaInicioGestion, FechaGestion, Detalle, estado,");
                stbuilder.Append("ValorTotalAtrasado, LineaIni, LineaFin, DiaIni, DiaFin, empresaini, empresaFin,clades,cobjuridico) values (");
                stbuilder.Append(Strings.Format(fecha, "yyyyMM") + ",'" + usuario + "','" + row["Codigoter"] + "','" + Strings.Format(row["FechaInicioGestion"], varini.pstForfecyHora) + "',");
                stbuilder.Append("'" + FechaSys + "',");
                stbuilder.Append("'" + row["Detalle"] + "','" + row["estado"] + "'," + row["ValorGestion"] + "," + row["LineaIni"] + ",");
                stbuilder.Append(row["LineaFin"] + "," + row["DiaIni"] + "," + row["DiaFin"] + ",'" + row["EmpresaIni"] + "',");
                stbuilder.Append("'" + row["EmpresaFin"] + "','" + row["Clades"] + "','" + row["CobJuridico"] + "')");

                ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaMaestroCobros");
                switch (ok)
                {
                    case true:
                        dsdata = BuscarUltimaGestionAsociado(row["Codigoter"].ToString(), myconnect);
                        if (dsdata.Tables["tblultgestion"].Rows.Count > 0)
                        {
                            consecutivo = Convert.ToDouble(dsdata.Tables["tblultgestion"].Rows[0]["secuencia"]);
                            ActMaestroGestionAsociado(row["codigoter"].ToString(), consecutivo, Convert.ToInt32(Strings.Format(fecha, "yyyyMM")), myconnect);
                        }
                        break;
                }
            }

            return consecutivo;
        }

        public bool GrabaDetalleCobros(DateTime fecha, string usuario, double secuencia, string estado, DataTable dttable, OdbcConnection myconnect)
        {
            int i = 0;
            DataSet dsdata = new DataSet();
            StringBuilder stbuilder = new StringBuilder();
            bool okk = false;
            string FechaSys;

            for (i = 0; i <= dttable.Rows.Count - 1; i++)
            {
                DataRow row = dttable.Rows[i];
                ok = this.BuscarGestionCobranza(Convert.ToInt32(Strings.Format(fecha, "yyyyMM")), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(secuencia), myconnect, ref dsdata);
                switch (ok)
                {
                    case false:
                        switch (varini.pstTipoBD.ToUpper())
                        {
                            case "DB2":
                                FechaSys = Strings.Format(row["FechaGestion"], varini.PstForFec) + "-" + Strings.Format(DateTime.Now, varini.pstForHora);
                                break;
                            default:
                                FechaSys = Strings.Format(Convert.ToDateTime(Strings.Format(row["FechaGestion"], varini.PstForFec) + " " + Strings.Format(DateTime.Now, varini.pstForHora)), varini.pstForfecyHora);
                                break;
                        }

                        stbuilder.Append("insert into cop_gesmaes (periodo,usuario,codigoter,lincred,numero,IdCodmaeges,fecha_gestion,fecha_pago,");
                        stbuilder.Append("saldot,capatr,intatr,moracum,OtrosAtr,cuota,diasmora) values (");
                        stbuilder.Append(Strings.Format(fecha, "yyyyMM") + ",'" + usuario + "','" + row["codigoter"] + "'," + row["lincred"] + ",");
                        stbuilder.Append(row["numero"] + "," + secuencia + ",'" + FechaSys + "',");
                        stbuilder.Append("'" + Strings.Format(Convert.ToDateTime(row["FechaCompromiso"]), varini.PstForFec) + "'," + row["saldo"] + "," + row["capital"] + "," + row["interes"] + ",");
                        stbuilder.Append(row["mora"] + "," + row["otros"] + "," + row["cuota"] + "," + row["diasmora"] + ")");

                        ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaDetalleCobros");
                        switch (ok)
                        {
                            case true:
                                stbuilder.Replace(stbuilder.ToString(), "");
                                okk = true;
                                stbuilder.Append("update cop_copmora set gestionado='Y' ");
                                stbuilder.Append("where codigoter='" + row["codigoter"] + "' and lincred=" + row["lincred"]);
                                stbuilder.Append(" and numero=" + row["numero"] + " and periodo_contable=" + Strings.Format(fecha, "yyyyMM"));
                                stbuilder.Append(" and (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon <> 0)");
                                this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaDetalleCobros(ActCopMora)");
                                break;
                        }
                        stbuilder.Replace(stbuilder.ToString(), "");
                        break;
                    case true:
                        stbuilder.Append("update cop_gesmaes set fecha_pago='" + Strings.Format(Convert.ToDateTime(row["FechaCompromiso"]), varini.PstForFec) + "',usuario='" + usuario + "',");
                        stbuilder.Append("saldot=" + row["saldo"] + ",capatr=" + row["capital"] + ",intatr=" + row["interes"] + ",");
                        stbuilder.Append("moracum=" + row["mora"] + ",OtrosAtr=" + row["otros"] + ",cuota=" + row["cuota"] + ",diasmora=" + row["diasmora"] + " ");
                        stbuilder.Append("where periodo=" + Strings.Format(fecha, "yyyyMM") + " and codigoter='" + row["codigoter"] + "' and ");
                        stbuilder.Append("lincred=" + row["lincred"] + " and numero=" + row["numero"] + " and IdCodmaeges=" + secuencia);

                        ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaDetalleCobros");
                        okk = true;
                        stbuilder.Replace(stbuilder.ToString(), "");
                        break;
                }
                if (ok)
                {
                    switch (estado)
                    {
                        case "3":
                            GrabarFechasCompromisos(secuencia, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToDateTime(row["fechacompromiso"]), myconnect);
                            break;
                    }
                }
            }
            return okk;
        }

        public bool GrabarFechasCompromisos(double IDCODMAEGES, string codigoter, int LINCRED, double NUMERO, DateTime FECHACOMPROMISO, OdbcConnection myconnect)
        {
            stmysql = "insert into COP_NOVFECGESTION (IDCODMAEGES,codigoter,LINCRED,NUMERO,FECHA,FECHACOMPROMISO) " +
                "values (" + IDCODMAEGES + ",'" + codigoter + "'," + LINCRED + "," + NUMERO + ",'" + Strings.Format(DateTime.Now, varini.pstForfecyHora) + "','" + Strings.Format(FECHACOMPROMISO, varini.PstForFec) + "')";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarFechasCompromisos");
            return ok;
        }

        public string BuscaASociadosGestionados(DateTime FechaProceso, string estado, int lineaini, int lineafin, int diaini,
            int diafin, string EmpresaIni, string EmpresaFin, string clades, string CobJuridico, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet dtdatos = new DataSet();
            int i = 0;
            string cedula = "";

            stmysql = "";
            switch (estado)
            {
                case "3":
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "SQL":
                            stmysql = " and {fn concat(gescob.codigoter,{fn concat(rtrim(gescob.lincred),rtrim(gescob.numero))})} not in " +
                                    "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                    "from cop_novfecgestion nov " +
                                    "where '" + Strings.Format(FechaProceso, varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                    "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                            break;
                        case "MYSQL":
                        case "ORACLE":
                        case "POSGRESQL":
                            stmysql = " and concat(gescob.codigoter,concat(rtrim(gescob.lincred),rtrim(gescob.numero))) not in " +
                                      "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                      "from cop_novfecgestion nov " +
                                      "where '" + Strings.Format(FechaProceso, varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                      "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                            break;
                        case "DB2":
                            stmysql = " and (gescob.codigoter || (rtrim(gescob.lincred) || rtrim(gescob.numero))) not in " +
                                    "(select (nov.codigoter || (rtrim(nov.lincred) || rtrim(nov.numero))) " +
                                    "from cop_novfecgestion nov " +
                                    "where '" + Strings.Format(FechaProceso, varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                    "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                            break;
                    }
                    break;
            }

            StBuilder.Append("select mae.codigoter,max(mae.secuencia) as Secuencia from cop_maegescob mae ");
            StBuilder.Append("inner join cop_gesmaes gescob on mae.secuencia=gescob.IdCodmaeges ");
            StBuilder.Append("inner join sys_maenit maenit on mae.codigoter = maenit.codigoter and maenit.engestion='N' ");
            StBuilder.Append("where mae.periodo=" + Strings.Format(FechaProceso, "yyyyMM") + " and mae.estado='" + estado + "' and mae.lineaini=" + lineaini + " and mae.lineafin=" + lineafin);
            StBuilder.Append(" and mae.diaini=" + diaini + " and mae.diafin=" + diafin + " and mae.Empresaini='" + EmpresaIni + "' and mae.EmpresaFin='" + EmpresaFin + "' ");
            StBuilder.Append("and mae.clades='" + clades + "' and mae.cobjuridico='" + CobJuridico + "' and Gestionado='N' " + stmysql);
            StBuilder.Append("group by mae.codigoter order by secuencia");

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaASociadosGestionados", dtdatos, "TblAsocGest");
            switch (ok)
            {
                case true:
                    ok = false;
                    while (i < dtdatos.Tables["TblAsocGest"].Rows.Count)
                    {
                        ok = ActAsociadoGestionadoMaenit(dtdatos.Tables["TblAsocGest"].Rows[i]["codigoter"].ToString(), "Y", myconnect);
                        switch (ok)
                        {
                            case true:
                                cedula = dtdatos.Tables["TblAsocGest"].Rows[i]["codigoter"].ToString();
                                goto ExitWhile_BuscaGest;
                        }
                        i += 1;
                    }
                    ExitWhile_BuscaGest:;
                    break;
            }
            return cedula;
        }

        public virtual DataSet BuscarUltimaGestionAsociado(string codigoter, DateTime fecha, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            int i = 0;
            DataSet dsdata = new DataSet();
            DataSet dsfeccompro = new DataSet();
            bool resp = false;

            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stbuilder.Append("select mae.secuencia,mae.fechagestion,mae.estado,mae.detalle,mae.usuario,mae.ValorTotalAtrasado,mae.periodo,usu.nombre ");
            stbuilder.Append("from cop_maegescob mae ");
            stbuilder.Append("inner join sys_sasusu usu on mae.usuario=usu.login ");
            stbuilder.Append("where mae.codigoter='" + codigoter + "' and ");
            stbuilder.Append("mae.secuencia=(select max(secuencia) from cop_maegescob ges where mae.codigoter=ges.codigoter) ");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarUltimaGestionAsociado", dsdata, "tblultgestion");
            switch (ok)
            {
                case true:
                    if (dsdata.Tables["tblultgestion"].Rows[0]["estado"].ToString() == "3")
                    {
                        dsfeccompro = BuscarUltimafechaCompromiso(Convert.ToInt32(dsdata.Tables["tblultgestion"].Rows[0]["secuencia"]), myconnect);
                        if (dsfeccompro.Tables["tblfecprolongada"].Rows.Count != 0)
                        {
                            while (i < dsfeccompro.Tables["tblfecprolongada"].Rows.Count)
                            {
                                DataRow rowFec = dsfeccompro.Tables["tblfecprolongada"].Rows[i];
                                if (Convert.ToDateTime(rowFec["FECHACOMPROMISO"]) > fecha)
                                {
                                    resp = true;
                                }
                                i += 1;
                            }
                        }
                    }
                    break;
            }

            if (!resp)
            {
                dsdata.Tables["tblultgestion"].Rows.Clear();
            }
            return dsdata;
        }

        public DataSet BuscarUltimafechaCompromiso(int IDCODMAEGES, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select LINCRED,NUMERO,FECHA,FECHACOMPROMISO from COP_NOVFECGESTION " +
                " where IDCODMAEGES=" + IDCODMAEGES + " and fecha = (select max(fecha) from COP_NOVFECGESTION where IDCODMAEGES=" + IDCODMAEGES + ") " +
                "order by fecha,LINCRED,NUMERO";
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarUltimafechaCompromiso", dsdata, "tblfecprolongada");
            return dsdata;
        }

        public DataSet BuscaDatosCodeudoresObligacion(string codigoter, int lincred, double numero, OdbcConnection myconnect, string periodo)
        {
            StringBuilder stbuilder = new StringBuilder();
            string nombre;
            DataSet dsdatacodeudor = new DataSet();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    nombre = "{fn concat(apellido,{fn concat(' ',nombre)})} as Nombre";
                    break;
                case "DB2":
                    nombre = " (apellido || ' ' || nombre) as Nombre";
                    break;
                default:
                    nombre = "concat(apellido,concat(' ',nombre)) as Nombre";
                    break;
            }

            stbuilder.Append("select maenit.codigoter," + nombre + ",maenit.direccion,maenit.telefono1,maenit.movil, '' as estado ");
            stbuilder.Append("from sys_maenit maenit ");
            stbuilder.Append("inner join cop_maecar maecar on (maecar.codeudor1=maenit.codigoter or maecar.codeudor2=maenit.codigoter ");
            stbuilder.Append("or maecar.codeudor3=maenit.codigoter or maecar.codeudor4=maenit.codigoter) ");
            stbuilder.Append("where maecar.codigoter='" + codigoter + "' and maecar.lincred=" + lincred + " and maecar.numero=" + numero);

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaDatosCodeudoresObligacion", dsdatacodeudor, "tblcodeudor");

            int fila;
            string estadoVal;

            for (fila = 0; fila <= dsdatacodeudor.Tables["tblcodeudor"].Rows.Count - 1; fila++)
            {
                DataRow rowCod = dsdatacodeudor.Tables["tblcodeudor"].Rows[fila];
                estadoVal = null;
                // this.msgconfig.BuscarEstadoAsociadoXPeriodo(rowCod["codigoter"].ToString(), Convert.ToDouble(periodo), myconnect, "", ref estadoVal); // ERROR: CS1061

                switch (estadoVal)
                {
                    case "R": estadoVal = "Retirado"; break;
                    case "A": estadoVal = "Activo"; break;
                    case "S": estadoVal = "Suspendido"; break;
                    case "T": estadoVal = "Trasladado"; break;
                }
                rowCod["estado"] = estadoVal;
            }

            return dsdatacodeudor;
        }

        // Overload without optional CantExtrasPact parameter
        public double BuscaSaldoExtrasPactadas(string codigoter, int lincred, double numero, DateTime fecha, OdbcConnection myconnect)
        {
            double CantExtrasPact = 0;
            return BuscaSaldoExtrasPactadas(codigoter, lincred, numero, fecha, myconnect, ref CantExtrasPact);
        }

        public double BuscaSaldoExtrasPactadas(string codigoter, int lincred, double numero, DateTime fecha, OdbcConnection myconnect, ref double CantExtrasPact)
        {
            StringBuilder stbuilder = new StringBuilder();
            double salextpactadas = 0;

            stbuilder.Append("select sum(salext.saldo) as campo1,count(ext.num_extra) as campo2 ");
            stbuilder.Append("from cop_extras ext ");
            stbuilder.Append("inner join cop_salextras salext on ext.codigoter=salext.codigoter and ext.lincred=salext.lincred ");
            stbuilder.Append("and ext.numero=salext.numero and ext.num_extra=salext.num_extra ");
            stbuilder.Append("where salext.periodo=" + Strings.Format(fecha, "yyyyMM") + " and salext.saldo>0 and ");
            stbuilder.Append("ext.codigoter='" + codigoter + "' and ext.lincred=" + lincred + " and ext.numero=" + numero);

            string _campo1 = salextpactadas.ToString();
            string _campo2 = CantExtrasPact.ToString();
            ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscaSaldoExtrasPactadas", ref _campo1, ref _campo2);
            double.TryParse(_campo1, out salextpactadas);
            double.TryParse(_campo2, out CantExtrasPact);
            return salextpactadas;
        }

        public bool VerificaAbonoCapitalCredito(string codigoter, int lincred, double numero,
            DateTime fecha, double vlrabono, OdbcConnection myconnect)
        {
            double saldoextraspactadas = 0;
            double saldo = 0;
            double saldocapital = 0;
            double saldoextra = 0;
            double saldodispabono = 0;
            double saldot = 0;
            double saldoext = 0;

            saldoextraspactadas = this.BuscaSaldoExtrasPactadas(codigoter, lincred, numero, fecha, myconnect);
            if (saldoextraspactadas == 0)
            {
                // this.BuscaSaldosCuotasPendientes(codigoter, lincred, numero, lincred, numero, Strings.Format(fecha, "yyyyMM"), myconnect, ref saldocapital, ref saldoextra, 0, 0, 0, 0, 0, 0, 0, 0, ref saldo); // ERROR: CS1501
                saldot = saldo - saldocapital - saldoextra - vlrabono;
                if (saldot <= 0)
                {
                    if (saldocapital != 0)
                    {
                        MessageBox.Show("No puede hacer abono a capital. Maximo valor que puede abonar a esta obligacion es: $ " + Strings.FormatNumber((saldo - saldocapital - saldoextra), 2), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                }
                return true;
            }
            else
            {
                // this.BuscaSaldosCuotasPendientes(codigoter, lincred, numero, lincred, numero, Strings.Format(fecha, "yyyyMM"), myconnect, ref saldocapital, ref saldoextra, 0, 0, 0, 0, 0, 0, 0, 0, ref saldo); // ERROR: CS1501
                saldot = saldo - saldocapital - saldoextra;
                saldoext = saldoextraspactadas - saldoextra;
                if (saldot <= 0)
                {
                    MessageBox.Show("No puede hacer abono a capital, la cuota causada cubre el saldo de la obligacion.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                saldodispabono = saldot - saldoext;
                if (saldodispabono <= 0)
                {
                    MessageBox.Show("No puede hacer abono a capital, debe hacer abono a extras." + ((char)13).ToString() +
                           "Las extras pactadas cubren el saldo de la obligacion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                else
                {
                    if (vlrabono > saldodispabono)
                    {
                        MessageBox.Show("El valor maximo de abono a capital es de " + Strings.FormatNumber(saldodispabono, 2) + ((char)13).ToString() +
                               "El resto debe ser abono a extras", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
        }

        public DataSet CargarGrillaMaestroCirculares(string codigoter, int periodoini, int periodofin, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stbuilder.Append("select mae.numaviso,mae.codigoter,mae.periodo,mae.clasecpto,mae.fecha,mae.direccion,mae.telefono,mae.codciudad,");
            stbuilder.Append("mae.detalle1,mae.detalle2,mae.dia_ini,mae.dia_fin,mae.enviacodeudor,mae.leyarrastre,ciu.nombre_ciudad, ");
            stbuilder.Append("case mae.clasecpto when '1' then 'Todos' when '2' then 'Cartera' when '3' then 'Cptos_a_favor' end as clase ");
            stbuilder.Append("from cop_maecircobro mae ");
            stbuilder.Append("inner join sys_ciudad57 ciu on codciudad = ciu.ciudad ");
            stbuilder.Append("where mae.codigoter='" + codigoter + "' and mae.periodo between " + periodoini + " and " + periodofin);

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargarGrillaMaestroCirculares", dsdata, "tblmaecircular");
            return dsdata;
        }

        public DataSet CargarGrillaDetalleCirculares(string codigoter, int periodo, string numaviso, string clasecpto, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stbuilder.Append("select det.numaviso,det.codigoter,det.periodo,det.clasecpto,det.ciclo,det.lincred,det.numero,det.diasmora,");
            stbuilder.Append("det.saldocapital,det.saldoextra,det.saldointeres,det.saldomora,(det.saldoseguro+det.saldoadmon+det.saldootros) as saldootro,");
            stbuilder.Append("det.fechavemto,det.codeudor1,det.telefono1,det.direccion1,det.ciudad1,det.codeudor2,det.telefono2,det.direccion2,det.ciudad2, ");
            stbuilder.Append("det.codeudor3,det.telefono3,det.direccion3,det.ciudad3,det.codeudor4,det.telefono4,det.direccion4,det.ciudad4 ");
            stbuilder.Append("from cop_detcircobro det ");
            stbuilder.Append("where det.numaviso='" + numaviso + "' and det.codigoter='" + codigoter + "' and det.periodo between " + periodo + " and det.clasecpto='" + clasecpto + "'");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargarGrillaDetalleCirculares", dsdata, "tbldetcircular");
            return dsdata;
        }

        public void CargaVentanaConsultaCirculares(string codigoter, int periodoini, int periodofin, System.Windows.Forms.Form forma, OdbcConnection myconnect)
        {
            // msgcop.frmcirculares frmcircular = new msgcop.frmcirculares(); // ERROR: CS0234

            // frmcircular.TxtCodigoter.Text = codigoter; // ERROR: CS0103
            // frmcircular.TxtPeriodoIni.Text = periodoini.ToString(); // ERROR: CS0103
            // frmcircular.TxtPeriodoFin.Text = periodofin.ToString(); // ERROR: CS0103
            // frmcircular.connect = myconnect; // ERROR: CS0103
            // frmcircular.Show(forma); // ERROR: CS0103
        }

        public void ActualizaCamposMoraCIFIN(DateTime fechainicial, DateTime fechafinal, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string camponull = "";

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    camponull = "isnull(";
                    break;
                case "MYSQL":
                case "DB2":
                    camponull = "ifnull(";
                    break;
                case "ORACLE":
                    camponull = "NVL(";
                    break;
                case "POSTGRE":
                    camponull = "NVL(";
                    break;
            }

            switch (varini.pstTipoBD.ToUpper())
            {
                case "DB2":
                    stbuilder.Append("update cop_maecar set saldoMoraCifin = ");
                    stbuilder.Append("(" + camponull + "(Select sum(saldocapital+saldoextra+saldointeres+saldomora+saldoseguro+saldoadmon+saldootros) ");
                    stbuilder.Append(" from cop_copmora where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter ");
                    stbuilder.Append(" and lincred = cop_maecar.lincred and numero = cop_maecar.numero and diasmora>0 group by periodo_contable),0)), ");
                    stbuilder.Append(" diasMoraCifin =(" + camponull + "(Select max(diasmora) from cop_copmora ");
                    stbuilder.Append(" where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter and lincred = cop_maecar.lincred ");
                    stbuilder.Append(" and numero = cop_maecar.numero  and (saldocapital>0 or saldoextra>0 or saldointeres>0 or saldomora>0 or saldoseguro>0 or saldoadmon>0 or saldootros>0) ");
                    stbuilder.Append(" group by periodo_contable),0)),");
                    stbuilder.Append(" CuotasMoraCifin = (" + camponull + "(select count(periodo_causa) from cop_copmora ");
                    stbuilder.Append(" where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter ");
                    stbuilder.Append(" and lincred = cop_maecar.lincred and numero = cop_maecar.numero and diasmora>0 and (saldocapital>0 or saldoextra>0 or saldointeres>0 or saldomora>0 or saldoseguro>0 or saldoadmon>0 or saldootros>0) ");
                    stbuilder.Append(" group by periodo_contable),0)),");
                    stbuilder.Append(" fechainicifin='" + Strings.Format(fechainicial, varini.PstForFec) + "',fechafincifin='" + Strings.Format(fechafinal, varini.PstForFec) + "' ");
                    stbuilder.Append(" where lincred>=1000 ");
                    break;
                default:
                    stbuilder.Append("update cop_maecar set saldoMoraCifin = ");
                    stbuilder.Append("(case " + camponull + "cast((Select sum(saldocapital+saldoextra+saldointeres+saldomora+saldoseguro+saldoadmon+saldootros) ");
                    stbuilder.Append(" from cop_copmora where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter ");
                    stbuilder.Append(" and lincred = cop_maecar.lincred and numero = cop_maecar.numero and diasmora>0 group by periodo_contable) as decimal),0) ");
                    stbuilder.Append(" when 0 then 0 else cast((Select sum(saldocapital+saldoextra+saldointeres+saldomora+saldoseguro+saldoadmon+saldootros) ");
                    stbuilder.Append(" from cop_copmora where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter ");
                    stbuilder.Append(" and lincred = cop_maecar.lincred and numero = cop_maecar.numero and diasmora>0 group by periodo_contable) as decimal) end), ");
                    stbuilder.Append("diasMoraCifin = (case " + camponull + "cast((Select max(diasmora) from cop_copmora ");
                    stbuilder.Append(" where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter and lincred = cop_maecar.lincred ");
                    stbuilder.Append(" and numero = cop_maecar.numero  and (saldocapital>0 or saldoextra>0 or saldointeres>0 or saldomora>0 or saldoseguro>0 or saldoadmon>0 or saldootros>0) ");
                    stbuilder.Append(" group by periodo_contable) as decimal),0) when 0 then 0 else ");
                    stbuilder.Append(" cast((Select max(diasmora) from cop_copmora where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter ");
                    stbuilder.Append(" and lincred = cop_maecar.lincred and numero = cop_maecar.numero and (saldocapital>0 or saldoextra>0 or saldointeres>0 or saldomora>0 or saldoseguro>0 or saldoadmon>0 or saldootros>0) ");
                    stbuilder.Append(" group by periodo_contable) as decimal) end),");
                    stbuilder.Append("CuotasMoraCifin = (case " + camponull + "cast((select count(periodo_causa) from cop_copmora ");
                    stbuilder.Append(" where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter ");
                    stbuilder.Append(" and lincred = cop_maecar.lincred and numero = cop_maecar.numero and diasmora>0 and (saldocapital>0 or saldoextra>0 or saldointeres>0 or saldomora>0 or saldoseguro>0 or saldoadmon>0 or saldootros>0) ");
                    stbuilder.Append(" group by periodo_contable) as decimal),0) when 0 then 0 else ");
                    stbuilder.Append("cast((select count(periodo_causa) from cop_copmora ");
                    stbuilder.Append(" where periodo_contable =" + Strings.Format(fechafinal, "yyyyMM") + " and codigoter = cop_maecar.Codigoter ");
                    stbuilder.Append(" and lincred = cop_maecar.lincred and numero = cop_maecar.numero and diasmora>0  and (saldocapital>0 or saldoextra>0 or saldointeres>0 or saldomora>0 or saldoseguro>0 or saldoadmon>0 or saldootros>0) ");
                    stbuilder.Append(" group by periodo_contable) as decimal) end),");
                    stbuilder.Append("fechainicifin='" + Strings.Format(fechainicial, varini.PstForFec) + "',fechafincifin='" + Strings.Format(fechafinal, varini.PstForFec) + "' ");
                    stbuilder.Append("where lincred>=1000 ");
                    break;
            }

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "Actualiza Campos Mora CIFIN");
        }

        public void ActualizaEstadoCifin(string periodo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string camponull = "";
            string periodoinicial = "";

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    camponull = "isnull(";
                    break;
                case "MYSQL":
                case "DB2":
                    camponull = "ifnull(";
                    break;
                case "ORACLE":
                    camponull = "NVL(";
                    break;
                case "POSTGRE":
                    camponull = "NVL(";
                    break;
            }

            if (Strings.Mid(periodo, 5) == "01")
            {
                periodoinicial = (Convert.ToInt32(Strings.Mid(periodo, 1, 4)) - 1) + "12";
            }
            else
            {
                periodoinicial = (Convert.ToInt32(periodo) - 1).ToString();
            }

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                case "ORACLE":
                case "DB2":
                    stbuilder.Append("update cop_salmaecar set estadocifin= " + camponull + "(select max(b.estadocifin) from cop_salmaecar b where ");
                    stbuilder.Append("cop_salmaecar.codigoter = b.codigoter And cop_salmaecar.lincred = b.lincred And cop_salmaecar.numero = b.numero ");
                    stbuilder.Append("and b.periodo=" + periodoinicial + " group by b.periodo),'01') where lincred>=1000 and periodo=" + periodo);
                    break;
                case "MYSQL":
                    stbuilder.Append("update cop_salmaecar a, cop_salmaecar b  set a.estadocifin =" + camponull + "b.estadocifin,'01') ");
                    stbuilder.Append("where a.codigoter = b.codigoter And a.lincred = b.lincred And a.numero = b.numero and b.periodo=" + periodoinicial);
                    stbuilder.Append(" and a.lincred>=1000 and a.periodo=" + periodo);
                    break;
                case "POSTGRE":
                    // VB original: empty case for POSTGRE
                    break;
            }

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "Actualiza Estado Cifin");
        }

        public DataTable ReclasificacionIngresos(DateTime FechaProceso, string Salmes, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            string StString = " ";
            int Fila = 0;

            DsDataSet.Tables.Add("Tblcontint");
            DsDataSet.Tables["Tblcontint"].Columns.Add("periodo", StString.GetType());
            DsDataSet.Tables["Tblcontint"].Columns.Add("lincred", StString.GetType());
            DsDataSet.Tables["Tblcontint"].Columns.Add("Descripcion", StString.GetType());
            DsDataSet.Tables["Tblcontint"].Columns.Add("cntsaldo", StString.GetType());
            DsDataSet.Tables["Tblcontint"].Columns.Add("Saldo", StString.GetType());
            DsDataSet.Tables["Tblcontint"].Columns.Add("Dif", StString.GetType());

            stbuilder.Append("select case when cnttemp.periodo is null then " + Strings.Format(FechaProceso, "yyyy") + " else cnttemp.periodo end as periodo,");
            stbuilder.Append("cnttemp.lincred,case when " + Salmes + " is null then 0 else " + Salmes + " end as cntsaldo");
            stbuilder.Append(", interes saldo, interes - case when " + Salmes + " is null then 0 else " + Salmes + " end as dif, coptemp.descripcion  ");
            stbuilder.Append("from cnt_coptemp03_vw cnttemp left join cop_coptemp03_vw coptemp on cnttemp.lincred = coptemp.lincred  and periodo_contable = " + Strings.Format(FechaProceso, "yyyyMM"));
            stbuilder.Append(" where (" + Salmes + "<> 0 Or interes <> 0) ");
            stbuilder.Append("group by case when cnttemp.periodo is null then " + Strings.Format(FechaProceso, "yyyy") + " else cnttemp.periodo end,cnttemp.lincred,");
            stbuilder.Append("case when " + Salmes + " is null then 0 else " + Salmes + " end ,interes ,coptemp.descripcion");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ReclasificacionIngresos", DsDataSet, "Tblcontint");

            return DsDataSet.Tables["Tblcontint"];
        }

        public void OrganizaDatosIngresos(DateTime Fechaproceso, OdbcConnection Myconnect, System.Windows.Forms.Form Myforma)
        {
            DateTime Fecini = new DateTime(Fechaproceso.Year, Fechaproceso.Month, 1);
            int CptoInt = 9999;
            string Cuenta = "999999999999";

            EliminaClaint(Strings.Format(Fechaproceso, "yyyyMM"), Myconnect);

            // BuscarCompania with param position 41 = CptoInt
            string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "";
            string _p11 = "", _p12 = "", _p13 = "", _p14 = "", _p15 = "", _p16 = "", _p17 = "", _p18 = "", _p19 = "", _p20 = "";
            string _p21 = "", _p22 = "", _p23 = "", _p24 = "", _p25 = "", _p26 = "", _p27 = "", _p28 = "", _p29 = "", _p30 = "";
            string _p31 = "", _p32 = "", _p33 = "", _p34 = "", _p35 = "", _p36 = "", _p37 = "", _p38 = "", _p39 = "";
            string _p40 = CptoInt.ToString();
            // this.msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                // ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, // ERROR: CS7036
                // ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27, ref _p28, ref _p29, ref _p30, // ERROR: CS7036
                // ref _p31, ref _p32, ref _p33, ref _p34, ref _p35, ref _p36, ref _p37, ref _p38, ref _p39, ref _p40); // ERROR: CS7036
            int.TryParse(_p40, out CptoInt);

            // BuscaTipoMovto with param position 6 = Cuenta
            string _t1 = "", _t2 = "", _t3 = "", _t4 = "", _t5 = Cuenta;
            // this.BuscaTipoMovto(CptoInt, Myconnect, ref _t1, ref _t2, ref _t3, ref _t4, ref _t5); // ERROR: CS1620
            Cuenta = _t5;

            GrabaAbonosIntereses(Fecini, Fechaproceso, Cuenta, Myconnect, Myforma);
            GrabaDifIntereses(Strings.Format(Fechaproceso, "yyyyMM"), Strings.Format(Fechaproceso.AddMonths(-1), "yyyyMM"), Cuenta, Myconnect, Myforma);
        }

        private object GrabaAbonosIntereses(DateTime FecIni, DateTime FecFin, string Cuenta, OdbcConnection Myconnect, System.Windows.Forms.Form Myforma)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdataset = new DataSet();
            int Fila = 0;
            double fogacla = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Intereses abonados del ciclo ", Myforma);

            StBuilder.Append("select copmov.lincred,par12.fogacla, par12.centroco,sum(copmov.vlr_debito - copmov.vlr_credito) Cartera ");
            StBuilder.Append("from cop_movimto copmov inner join cop_concar12 par12 on copmov.lincred = par12.lincred ");
            StBuilder.Append("where fecha_movto between '" + Strings.Format(FecIni, varini.PstForFec) + "' and '" + Strings.Format(FecFin, varini.PstForFec) + " '  and copmov.cuenta = '" + Cuenta + "'");
            StBuilder.Append("group by copmov.lincred, par12.fogacla,par12.centroco");

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), Myconnect, "GrabaAbonosIntereses", Dsdataset, "tblaboInt");

            msgbarra.ValorMinimoMaximo(0, Dsdataset.Tables["tblaboInt"].Rows.Count);
            msgbarra.Show();

            while (Fila < Dsdataset.Tables["tblaboInt"].Rows.Count)
            {
                DataRow row = Dsdataset.Tables["tblaboInt"].Rows[Fila];

                if (Convert.IsDBNull(row["fogacla"]) == true)
                {
                    fogacla = 0;
                }
                else
                {
                    if (row["fogacla"].ToString().Trim() == "")
                    {
                        fogacla = 0;
                    }
                    else
                    {
                        fogacla = Convert.ToDouble(row["fogacla"]);
                    }
                }

                GrabaCopClaInt(Strings.Format(FecFin, "yyyyMM"), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["cartera"]) * -1, Convert.ToDouble(row["centroco"]), fogacla, Myconnect);

                Fila += 1;
                msgbarra.PerformStep();
            }
            msgbarra.Close();
            msgbarra.Dispose();
            return null;
        }

        private void EliminaClaint(object periodo, OdbcConnection Myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            StBuilder.Append("delete from cop_claint ");
            StBuilder.Append("where periodo = '" + periodo + "'");
            this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "EliminaClaint");
        }

        private object GrabaDifIntereses(object Periodo, object PerAnt, string Cuenta, OdbcConnection Myconnect, System.Windows.Forms.Form Myforma)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdataset = new DataSet();
            int Fila = 0;
            double cartera;

            StBuilder.Append("select copclas.clasec,copclas.lincred,par12.fogacla,copclas.periodo_contable,(copclar.salcxc - copclas.salcxc) cartera ");
            StBuilder.Append("from cop_coptemp04_vw copclas left join cop_coptemp04_vw copclar on  ");
            StBuilder.Append("copclas.clasec = copclar.clasec And copclas.lincred = copclar.lincred ");
            StBuilder.Append("and copclar.periodo_contable = '" + Periodo + "' ");
            StBuilder.Append("inner join cop_concar12 par12 on copclas.lincred = par12.lincred ");
            StBuilder.Append("where copclas.periodo_contable = '" + PerAnt + "'");

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), Myconnect, "GrabaAbonosIntereses", Dsdataset, "tblaboInt");

            while (Fila < Dsdataset.Tables["tblaboInt"].Rows.Count)
            {
                DataRow row = Dsdataset.Tables["tblaboInt"].Rows[Fila];
                if (Convert.IsDBNull(row["cartera"]) == true)
                {
                    cartera = 0;
                }
                else
                {
                    cartera = Convert.ToDouble(row["cartera"]);
                }
                GrabaCopClaInt(Periodo.ToString(), Convert.ToInt32(row["lincred"]), cartera, 0, Convert.ToDouble(row["fogacla"]), Myconnect);

                Fila += 1;
            }
            return null;
        }

        private void GrabaCopClaInt(string periodo, int lincred, double cartera, double Cencos, double ClaseCartera, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            ok = BuscaCopClaInt(periodo, lincred, myconnect);
            switch (ok)
            {
                case false:
                    StBuilder.Append("insert into cop_claint(periodo,lincred, cartera,cencos,clasecart) values ('");
                    StBuilder.Append(periodo + "','");
                    StBuilder.Append(lincred + "','");
                    StBuilder.Append(cartera + "','");
                    StBuilder.Append(Cencos + "','");
                    StBuilder.Append(ClaseCartera + "')");
                    break;
                case true:
                    StBuilder.Append("Update cop_claint set ");
                    StBuilder.Append("cartera = cartera + '");
                    StBuilder.Append(cartera + "' ");
                    StBuilder.Append("where periodo = '" + periodo + "' and lincred = '" + lincred + "'");
                    break;
            }

            this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaCopClaInt");
        }

        private bool BuscaCopClaInt(string periodo, int lincred, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();

            try
            {
                dsdataset.Tables.Remove("tblclaint");
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select periodo, lincred,cartera,cencos,ClaseCart ");
            stbuilder.Append("from cop_claint where periodo = '");
            stbuilder.Append(periodo + "' and lincred = '");
            stbuilder.Append(lincred + "'");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaCopClaInt", DsDataNovedad, "tblclaint");
            if (DsDataNovedad.Tables["tblclaint"].Rows.Count > 0)
            {
                try
                {
                    dsdataset.Tables.Add(DsDataNovedad.Tables["tblclaint"].Copy());
                }
                catch (Exception)
                {
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool GenerarArchivoCIFIN(string empresa, string paquete, string tiporeporte, string tipoentidad,
            string codigoentidad, DateTime fechainicial, DateTime fechafinal, string imprimecodeudores, string renumeracion,
            StreamWriter StArchivo, string CodEmpresaAppl, string MoraNomina, System.Windows.Forms.Form myforma, OdbcConnection myconnect, string CSV = "", string ClaveCifin = "")
        {
            ERP.Core.Compartido.Controles.Barraprogress barraprogress = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Campos CIFIN", myforma);
            DataSet DsDatosCifin = new DataSet();

            switch (paquete.Trim())
            {
                case "23":
                case "24":
                    DsDatosCifin = this.OrganizaDatosCifinCdatAhorros(empresa, tiporeporte, fechafinal, paquete, myforma, myconnect);
                    this.GeneraPlanoCifinCdtAhorro(paquete, tiporeporte, tipoentidad, codigoentidad, fechafinal, ClaveCifin, StArchivo, DsDatosCifin, myforma, CSV);
                    break;
                default:
                    barraprogress.Show();
                    this.ActualizaCamposMoraCIFIN(fechainicial, fechafinal, myconnect);
                    this.ActualizaEstadoCifin(Strings.Format(fechafinal, "yyyyMM"), myconnect);
                    barraprogress.Close();
                    barraprogress.Dispose();

                    DsDatosCifin = this.OrganizaDatosCifin(empresa, tiporeporte, fechafinal, imprimecodeudores, renumeracion, MoraNomina, myforma, myconnect);
                    DsDatosCifin = OrganizaDatosCifinCastigo(empresa, tiporeporte, fechafinal, DsDatosCifin, imprimecodeudores, MoraNomina, myforma, myconnect);
                    this.GeneraPlanoCifin(paquete, tiporeporte, tipoentidad, codigoentidad, fechafinal, StArchivo, DsDatosCifin, myforma, CSV);
                    if (DsDatosCifin.Tables["NovedadesCIFIN"].Rows.Count > 0)
                    {
                        ImprimeNovedadesCifin(CodEmpresaAppl, DsDatosCifin.Tables["NovedadesCIFIN"], fechainicial, fechafinal, tiporeporte, myforma, myconnect);
                    }
                    break;
            }

            return true;
        }

        private void ImprimeNovedadesCifin(string CodEmpresa, DataTable dsdata, DateTime fechaini, DateTime fechafin, string tiporeporte,
            System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.reporte report = new ERP.Core.Compartido.Reportes.reporte("cop_rinfnovcifin");
            ERP.Core.Compartido.Forms.imprimir EditImpresion = new ERP.Core.Compartido.Forms.imprimir();
            string nit = " ";
            string direccion = " ";
            string telefono = " ";
            string nomempresa = " ";

            // BuscarCompania extracting p15=nit, p16=direccion, p26=nomempresa, p27=telefono
            string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "";
            string _p11 = "", _p12 = "", _p13 = "";
            string _p14 = nit, _p15 = direccion, _p16 = "", _p17 = "", _p18 = "", _p19 = "", _p20 = "";
            string _p21 = "", _p22 = "", _p23 = "", _p24 = "", _p25 = nomempresa, _p26 = telefono;
            // this.msgcofsys.BuscarCompania(CodEmpresa, myconnect, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                // ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, // ERROR: CS7036
                // ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26); // ERROR: CS7036
            nit = _p14;
            direccion = _p15;
            nomempresa = _p25;
            telefono = _p26;

            report.SetDataSource(dsdata);
            report.SetParameterValue("nombre_empresa", nomempresa);
            report.SetParameterValue("nit", nit);
            report.SetParameterValue("direccion", direccion);
            report.SetParameterValue("telefono", telefono);
            report.SetParameterValue("tiporeporte", tiporeporte);
            report.SetParameterValue("fechaini", fechaini);
            report.SetParameterValue("fechafin", fechafin);

            // EditImpresion.CrystalReportViewer1.ReportSource = report; // ERROR: CS1061
            EditImpresion.Show(myforma);
        }

        private void GeneraPlanoCifin(string paquete, string tiporeporte, string tipoentidad, string codigoentidad,
            DateTime fechafinal, StreamWriter StArchivo, DataSet dsdata, System.Windows.Forms.Form myforma, string rutaCSV = "")
        {
            ERP.Core.Compartido.Controles.Barraprogress barraprogress = new ERP.Core.Compartido.Controles.Barraprogress("Generando Archivo plano CIFIN", myforma);
            int fila = 0;
            string espacios60 = Strings.Space(60);
            decimal total = 0;

            barraprogress.ValorMinimoMaximo(0, dsdata.Tables["incluidoscifin"].Rows.Count);
            barraprogress.Show();

            // Imprimimos el registro tipo 1. Control del reporte
            StArchivo.Write("1"); //Tipo Registro
            StArchivo.Write(Strings.Right("00" + paquete, 2)); //Codigo Paquete
            StArchivo.Write(Strings.Right("000" + tipoentidad, 3)); //tipo entidad
            StArchivo.Write(Strings.Right("000" + codigoentidad, 3)); //tipo entidad
            StArchivo.Write("          "); //reservado
            StArchivo.Write(Strings.Right("00" + tiporeporte, 2)); //tipo reporte
            StArchivo.WriteLine(fechafinal.ToString("yyyyMMdd")); //fecha corte

            // Imprimimos el registro tipo 3. Renumeracion
            for (fila = 0; fila <= dsdata.Tables["Renumeracion"].Rows.Count - 1; fila++)
            {
                DataRow rowRen = dsdata.Tables["Renumeracion"].Rows[fila];
                StArchivo.Write("3"); // Tipo Registro
                StArchivo.Write(rowRen["ObligacionAnterior"]); //Obligacion Anterior
                StArchivo.Write(Strings.Right("      " + rowRen["SucursalAnterior"], 6)); //Sucursal
                StArchivo.Write(rowRen["ObligacionNueva"]); //Obligacion Actual
                StArchivo.WriteLine(Strings.Right("      " + rowRen["SucursalNueva"], 6)); //Sucursal actual
            }

            // Imprimimos el registro tipo 2. Detalles
            for (fila = 0; fila <= dsdata.Tables["incluidoscifin"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdata.Tables["incluidoscifin"].Rows[fila];
                StArchivo.Write("2"); // Tipo Registro
                StArchivo.Write(row["tipoid"]); //Tipo ID
                StArchivo.Write(Strings.Right("000000000000000" + row["numid"], 15)); //Numero ID
                StArchivo.Write(Strings.Left(row["nombretitular"].ToString() + espacios60, 60)); //Nombre Titular
                StArchivo.Write("          "); //Reservado
                StArchivo.Write(row["obligacion"]); //Numero Obligacion
                StArchivo.Write(row["sucursal"]); //sucursal
                StArchivo.Write(row["calidad"]); //calidad
                StArchivo.Write(row["calificacion"]); //calificacion
                StArchivo.Write(row["estadotitular"]); //estado del titular
                StArchivo.Write(row["estado"]); //estado de la obligacion
                StArchivo.Write(row["edadmora"]); //Edad de mora
                StArchivo.Write(row["aniosmora"]); //Anios de mora
                StArchivo.Write(row["fechacorte"]); //Fecha de corte
                StArchivo.Write(row["fechainicial"]); //Fecha inicial obligacion
                StArchivo.Write(row["fechafin"]); //Fecha fin obligacion
                StArchivo.Write(row["fechaexigibildad"]); //Fecha exigibildad obligacion
                StArchivo.Write(row["fechaprescripcion"]); //Fecha prescripcion obligacion
                StArchivo.Write(row["fechapago"]); //Fecha pago obligacion
                StArchivo.Write(row["modoextincion"]); //Modo extincion obligacion
                StArchivo.Write(row["tipopago"]); //tipo pago obligacion
                StArchivo.Write(row["periodicidad"]); //periodicidad pago obligacion
                StArchivo.Write(Strings.Right("000" + row["probabilidadnopago"], 3)); //probabilidad no pago
                StArchivo.Write(Strings.Right("000" + row["cuotaspagas"], 3)); //Numero cuotas pagadas
                StArchivo.Write(Strings.Right("000" + row["cuotaspactadas"], 3)); //Numero cuotas pactadas
                StArchivo.Write(Strings.Right("000" + row["cuotasenmora"], 3)); // Cuotas en mora
                StArchivo.Write(Strings.Right("000000000000" + row["valorinicial"].ToString(), 12)); // Valor Inicial
                StArchivo.Write(Strings.Right("000000000000" + row["valormora"].ToString(), 12)); // Valor mora
                StArchivo.Write(Strings.Right("000000000000" + row["saldo"].ToString(), 12)); // Saldo Obligacion
                StArchivo.Write(Strings.Right("000000000000" + row["cuota"].ToString(), 12)); // Cuota Obligacion
                StArchivo.Write("            "); // Cargo Fijo
                StArchivo.Write(Strings.Right("000" + row["lineacredito"], 3)); // Linea de Credito
                StArchivo.Write("   "); // Clausula de permanencia
                StArchivo.Write(Strings.Right("000" + row["tipocontrato"], 3)); // tipo contrato
                StArchivo.Write(Strings.Right("000" + row["estadocontrato"], 3)); // Estado contrato
                StArchivo.Write("  "); // Vigencia Contrato
                StArchivo.Write("   "); // Numero Meses Contrato
                StArchivo.Write(Strings.Right("000" + row["naturalezajuridica"], 3)); // Naturaleza Juridica
                StArchivo.Write(Strings.Right("00" + row["modalidadcredito"], 2)); // Modalidad Credito
                StArchivo.Write(Strings.Right("00" + row["tipomoneda"], 2)); // tipo moneda
                StArchivo.Write(Strings.Right("00" + row["tipogarantia"], 2)); // tipo garantia
                StArchivo.Write(Strings.Right("000000000000" + row["valorgarantia"].ToString(), 12)); // valor garantia
                StArchivo.Write(row["obligacionreestructurada"]); //obligacion reestructurada
                StArchivo.Write(Strings.Right("  " + row["naturalezareestructura"], 2)); //naturaleza reestructurada
                StArchivo.Write(Strings.Right("   " + row["numeroreestructura"], 3)); //numero reestructuraciones
                StArchivo.Write("  "); // Clase Tarjeta
                StArchivo.Write("    "); // Cheques devueltos
                StArchivo.Write("  "); // Categoria servicios
                StArchivo.Write("  "); // Plazo
                StArchivo.Write("      "); // Dias Cartera
                StArchivo.Write("  "); // Tipo cuenta
                StArchivo.Write("            "); // Cupo sobregiro
                StArchivo.Write("   "); // Dias Autorizados
                StArchivo.Write(Strings.Left(row["direccioncasa"].ToString() + espacios60, 60)); //Direccion Casa
                StArchivo.Write(Strings.Left(row["telefonocasa"].ToString() + espacios60, 20)); //Telefono Casa
                StArchivo.Write(Strings.Right("000000" + row["codigociudadcasa"], 6)); //Codigo ciudad Casa
                StArchivo.Write(Strings.Left(row["ciudadcasa"].ToString() + espacios60, 20)); //CIudad Casa
                StArchivo.Write(Strings.Right("000" + row["codigodptocasa"], 3)); //Codigo Departamento Casa
                StArchivo.Write(Strings.Left(row["deptocasa"].ToString().ToUpper() + espacios60, 20)); //Departamento Casa
                StArchivo.Write(Strings.Left(row["nombreempresa"].ToString() + espacios60, 60)); //Nombre empresa
                StArchivo.Write(Strings.Left(row["direccionempresa"].ToString() + espacios60, 60)); //Direccion empresa
                StArchivo.Write(Strings.Left(row["telefonoempresa"].ToString() + espacios60, 20)); //Telefono Empresa
                StArchivo.Write(Strings.Right("000000" + row["codigociudadempresa"], 6)); //Codigo ciudad Empresa
                StArchivo.Write(Strings.Left(row["ciudadempresa"].ToString() + espacios60, 20)); //CIudad Empresa
                StArchivo.Write(Strings.Right("000" + row["codigodptoempresa"], 3)); //Codigo Departamento Empresa
                StArchivo.Write(Strings.Left(row["deptoempresa"].ToString().ToUpper() + espacios60, 20)); //Departamento Empresa
                StArchivo.Write("        "); // Fecha excension
                StArchivo.Write("        "); // Fecha fin excension
                StArchivo.Write("  "); // Renovaciones CDAT
                StArchivo.Write("  "); // Cuenta excenta
                StArchivo.Write("  "); // Tipo id originaria
                StArchivo.Write("              "); // num id originaria
                StArchivo.Write("   "); // Tipo Entidad Originaria
                StArchivo.Write("   "); // Cod Entidad Originaria
                StArchivo.Write("  "); // Tipo Fideicomiso
                StArchivo.Write("            "); // Numero Fideicomiso
                StArchivo.Write(espacios60); // Nombre Fideicomiso
                StArchivo.Write("    "); // Tipo Deuda Cartera
                StArchivo.Write("    "); // Tipo Poliza
                StArchivo.WriteLine("      "); // Codigo ramo

                barraprogress.PerformStep();
            }

            // Imprimimos el registro tipo 9. Control de fin de archivo
            total = dsdata.Tables["incluidoscifin"].Rows.Count + dsdata.Tables["Renumeracion"].Rows.Count + 2;
            total = Convert.ToDecimal(Strings.FormatNumber(Math.Round(total, 0), 0, Microsoft.VisualBasic.TriState.False, Microsoft.VisualBasic.TriState.False, Microsoft.VisualBasic.TriState.False));
            StArchivo.Write("9"); //Tipo Registro
            StArchivo.Write(Strings.Right("00000000" + total, 8)); //Numero de registros
            StArchivo.Write(Strings.Right("00000000" + dsdata.Tables["incluidoscifin"].Rows.Count, 8)); //Numero de registros tipo 2
            StArchivo.Write(Strings.Right("00000000" + dsdata.Tables["Renumeracion"].Rows.Count, 8)); //Numero de registros tipo 3
            StArchivo.Write("00000000"); //Numero de registros tipo 4
            StArchivo.Close();

            if (dsdata.Tables["CambioEstado"].Rows.Count > 0)
            {
                StreamWriter StArcCamEstado = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\archivos planos\\export\\CambioEstadoCifin.txt", false);
                barraprogress.ValorMinimoMaximo(0, dsdata.Tables["CambioEstado"].Rows.Count);
                barraprogress.Titulo("Generando Archivo para Cambio de Estado");

                for (fila = 0; fila <= dsdata.Tables["CambioEstado"].Rows.Count - 1; fila++)
                {
                    DataRow rowCE = dsdata.Tables["CambioEstado"].Rows[fila];
                    rowCE["saldomora"] = Strings.Right(Strings.Space(12) + Strings.FormatNumber(Math.Round(Convert.ToDouble(rowCE["saldomora"]), 0), 0, Microsoft.VisualBasic.TriState.False, Microsoft.VisualBasic.TriState.False, Microsoft.VisualBasic.TriState.False), 12);
                    rowCE["saldo"] = Strings.Right(Strings.Space(12) + Strings.FormatNumber(Math.Round(Convert.ToDouble(rowCE["saldo"]), 0), 0, Microsoft.VisualBasic.TriState.False, Microsoft.VisualBasic.TriState.False, Microsoft.VisualBasic.TriState.False), 12);
                    if (rowCE["SaldoMora"].ToString().Trim() == "")
                    {
                        rowCE["SaldoMora"] = Strings.Space(11) + "0";
                    }
                    if (rowCE["saldo"].ToString().Trim() == "")
                    {
                        rowCE["saldo"] = Strings.Space(11) + "0";
                    }
                    StArcCamEstado.Write(rowCE["Obligacion"]); //Obligacion
                    StArcCamEstado.Write(rowCE["Sucursal"]); //Sucursal
                    StArcCamEstado.Write(rowCE["Estado"]); //Estado
                    StArcCamEstado.Write(rowCE["Calificacion"]); //Calificacion
                    StArcCamEstado.Write(rowCE["EdadMora"]); //EdadMora
                    StArcCamEstado.Write(rowCE["Saldo"]); //Saldo
                    StArcCamEstado.WriteLine(rowCE["SaldoMora"]); //Mora

                    barraprogress.PerformStep();
                }
                StArcCamEstado.Close();
                StArcCamEstado.Dispose();

                MessageBox.Show("Se genero un archivo plano para el cambio de estado de algunas obligaciones" + ((char)13).ToString() +
                "Ubicacion:" + AppDomain.CurrentDomain.BaseDirectory + "\\archivos planos\\export\\CambioEstadoCifin.txt", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //      generar archivo CSV
            TextWriter readWriter;
            if (rutaCSV != "")
            {
                barraprogress.ValorMinimoMaximo(0, dsdata.Tables["incluidoscifin"].Rows.Count);
                barraprogress.Titulo("Generando Archivo plano CIFIN CSV");
                readWriter = File.CreateText(rutaCSV);
                //********************************
                readWriter.Write("Tipo Registro,"); // Tipo Registro
                readWriter.Write("Tipo ID,");
                readWriter.Write("Numero ID,");
                readWriter.Write("Nombre Titular,");
                readWriter.Write("Reservado,");
                readWriter.Write("Numero Obligacion,");
                readWriter.Write("Sucursal,");
                readWriter.Write("Calidad,");
                readWriter.Write("Calificacion,");
                readWriter.Write("Estado del titular,");
                readWriter.Write("Estado de la obligacion,");
                readWriter.Write("Edad de mora,");
                readWriter.Write("Anios de mora,");
                readWriter.Write("Fecha de corte,");
                readWriter.Write("Fecha inicial obligacion,");
                readWriter.Write("Fecha fin obligacion,");
                readWriter.Write("Fecha exigibildad obligacion,");
                readWriter.Write("Fecha prescripcion obligacion,");
                readWriter.Write("Fecha pago obligacion,");
                readWriter.Write("Modo extincion obligacion,");
                readWriter.Write("Tipo pago obligacion,");
                readWriter.Write("Periodicidad pago obligacion,");
                readWriter.Write("Probabilidad no pago,");
                readWriter.Write("Numero cuotas pagadas,");
                readWriter.Write("Numero cuotas pactadas,");
                readWriter.Write("Cuotas en mora,");
                readWriter.Write("Valor Inicial,");
                readWriter.Write("Valor mora,");
                readWriter.Write("Saldo Obligacion,");
                readWriter.Write("Cuota Obligacion,");
                readWriter.Write("Cargo Fijo,");
                readWriter.Write("Linea de Credito,");
                readWriter.Write("Clausula de permanencia,");
                readWriter.Write("Tipo contrato,");
                readWriter.Write("Estado contrato,");
                readWriter.Write("Vigencia Contrato,");
                readWriter.Write("Numero Meses Contrato,");
                readWriter.Write("Naturaleza Juridica,");
                readWriter.Write("Modalidad Credito,");
                readWriter.Write("Tipo moneda,");
                readWriter.Write("Tipo garantia,");
                readWriter.Write("Valor garantia,");
                readWriter.Write("Obligacion reestructurada,");
                readWriter.Write("Naturaleza reestructurada,");
                readWriter.Write("Numero reestructuraciones,");
                readWriter.Write("Clase Tarjeta,");
                readWriter.Write("Cheques devueltos,");
                readWriter.Write("Categoria servicios,");
                readWriter.Write("Plazo,");
                readWriter.Write("Dias Cartera,");
                readWriter.Write("Tipo cuenta,");
                readWriter.Write("Cupo sobregiro,");
                readWriter.Write("Dias Autorizados,");
                readWriter.Write("Direccion Casa,");
                readWriter.Write("Telefono Casa,");
                readWriter.Write("Codigo ciudad Casa,");
                readWriter.Write("CIudad Casa,");
                readWriter.Write("Codigo Departamento Casa,");
                readWriter.Write("Departamento Casa,");
                readWriter.Write("Nombre empresa,");
                readWriter.Write("Direccion empresa,");
                readWriter.Write("Telefono Empresa,");
                readWriter.Write("Codigo ciudad Empresa,");
                readWriter.Write("CIudad Empresa,");
                readWriter.Write("Codigo Departamento Empresa,");
                readWriter.Write("Departamento Empresa,");
                readWriter.Write("Fecha excension,");
                readWriter.Write("Fecha fin excension,");
                readWriter.Write("Renovaciones CDAT,");
                readWriter.Write("Cuenta excenta,");
                readWriter.Write("Tipo id originaria,");
                readWriter.Write("Num id originaria,");
                readWriter.Write("Tipo Entidad Originaria,");
                readWriter.Write("Cod Entidad Originaria,");
                readWriter.Write("Tipo Fideicomiso,");
                readWriter.Write("Numero Fideicomiso,");
                readWriter.Write("Nombre Fideicomiso,");
                readWriter.Write("Tipo Deuda Cartera,");
                readWriter.Write("Tipo Poliza,");
                readWriter.WriteLine("Codigo ramo");

                //********************************
                for (fila = 0; fila <= dsdata.Tables["incluidoscifin"].Rows.Count - 1; fila++)
                {
                    DataRow row = dsdata.Tables["incluidoscifin"].Rows[fila];
                    readWriter.Write("2" + ","); // Tipo Registro
                    readWriter.Write(row["tipoid"] + ","); //Tipo ID
                    readWriter.Write(Strings.Right("000000000000000" + row["numid"], 15) + ","); //Numero ID
                    readWriter.Write(Strings.Left(row["nombretitular"].ToString() + espacios60, 60) + ","); //Nombre Titular
                    readWriter.Write("          " + ","); //Reservado
                    readWriter.Write(row["obligacion"] + ","); //Numero Obligacion
                    readWriter.Write(row["sucursal"] + ","); //sucursal
                    readWriter.Write(row["calidad"] + ","); //calidad
                    readWriter.Write(row["calificacion"] + ","); //calificacion
                    readWriter.Write(row["estadotitular"] + ","); //estado del titular
                    readWriter.Write(row["estado"] + ","); //estado de la obligacion
                    readWriter.Write(row["edadmora"] + ","); //Edad de mora
                    readWriter.Write(row["aniosmora"] + ","); //Anios de mora
                    readWriter.Write(row["fechacorte"] + ","); //Fecha de corte
                    readWriter.Write(row["fechainicial"] + ","); //Fecha inicial obligacion
                    readWriter.Write(row["fechafin"] + ","); //Fecha fin obligacion
                    readWriter.Write(row["fechaexigibildad"] + ","); //Fecha exigibildad obligacion
                    readWriter.Write(row["fechaprescripcion"] + ","); //Fecha prescripcion obligacion
                    readWriter.Write(row["fechapago"] + ","); //Fecha pago obligacion
                    readWriter.Write(row["modoextincion"] + ","); //Modo extincion obligacion
                    readWriter.Write(row["tipopago"] + ","); //tipo pago obligacion
                    readWriter.Write(row["periodicidad"] + ","); //periodicidad pago obligacion
                    readWriter.Write(Strings.Right("000" + row["probabilidadnopago"], 3) + ","); //probabilidad no pago
                    readWriter.Write(Strings.Right("000" + row["cuotaspagas"], 3) + ","); //Numero cuotas pagadas
                    readWriter.Write(Strings.Right("000" + row["cuotaspactadas"], 3) + ","); //Numero cuotas pactadas
                    readWriter.Write(Strings.Right("000" + row["cuotasenmora"], 3) + ","); // Cuotas en mora
                    readWriter.Write(Strings.Right("000000000000" + row["valorinicial"].ToString(), 12) + ","); // Valor Inicial
                    readWriter.Write(Strings.Right("000000000000" + row["valormora"].ToString(), 12) + ","); // Valor mora
                    readWriter.Write(Strings.Right("000000000000" + row["saldo"].ToString(), 12) + ","); // Saldo Obligacion
                    readWriter.Write(Strings.Right("000000000000" + row["cuota"].ToString(), 12) + ","); // Cuota Obligacion
                    readWriter.Write("            " + ","); // Cargo Fijo
                    readWriter.Write(Strings.Right("000" + row["lineacredito"], 3) + ","); // Linea de Credito
                    readWriter.Write("   " + ","); // Clausula de permanencia
                    readWriter.Write(Strings.Right("000" + row["tipocontrato"], 3) + ","); // tipo contrato
                    readWriter.Write(Strings.Right("000" + row["estadocontrato"], 3) + ","); // Estado contrato
                    readWriter.Write("  " + ","); // Vigencia Contrato
                    readWriter.Write("   " + ","); // Numero Meses Contrato
                    readWriter.Write(Strings.Right("000" + row["naturalezajuridica"], 3) + ","); // Naturaleza Juridica
                    readWriter.Write(Strings.Right("00" + row["modalidadcredito"], 2) + ","); // Modalidad Credito
                    readWriter.Write(Strings.Right("00" + row["tipomoneda"], 2) + ","); // tipo moneda
                    readWriter.Write(Strings.Right("00" + row["tipogarantia"], 2) + ","); // tipo garantia
                    readWriter.Write(Strings.Right("000000000000" + row["valorgarantia"].ToString(), 12) + ","); // valor garantia
                    readWriter.Write(row["obligacionreestructurada"] + ","); //obligacion reestructurada
                    readWriter.Write(Strings.Right("  " + row["naturalezareestructura"], 2) + ","); //naturaleza reestructurada
                    readWriter.Write(Strings.Right("   " + row["numeroreestructura"], 3) + ","); //numero reestructuraciones
                    readWriter.Write("  " + ","); // Clase Tarjeta
                    readWriter.Write("    " + ","); // Cheques devueltos
                    readWriter.Write("  " + ","); // Categoria servicios
                    readWriter.Write("  " + ","); // Plazo
                    readWriter.Write("      " + ","); // Dias Cartera
                    readWriter.Write("  " + ","); // Tipo cuenta
                    readWriter.Write("            " + ","); // Cupo sobregiro
                    readWriter.Write("   " + ","); // Dias Autorizados
                    readWriter.Write(Strings.Left(row["direccioncasa"].ToString() + espacios60, 60) + ","); //Direccion Casa
                    readWriter.Write(Strings.Left(row["telefonocasa"].ToString() + espacios60, 20) + ","); //Telefono Casa
                    readWriter.Write(Strings.Right("000000" + row["codigociudadcasa"], 6) + ","); //Codigo ciudad Casa
                    readWriter.Write(Strings.Left(row["ciudadcasa"].ToString() + espacios60, 20) + ","); //CIudad Casa
                    readWriter.Write(Strings.Right("000" + row["codigodptocasa"], 3) + ","); //Codigo Departamento Casa
                    readWriter.Write(Strings.Left(row["deptocasa"].ToString().ToUpper() + espacios60, 20) + ","); //Departamento Casa
                    readWriter.Write(Strings.Left(row["nombreempresa"].ToString() + espacios60, 60) + ","); //Nombre empresa
                    readWriter.Write(Strings.Left(row["direccionempresa"].ToString() + espacios60, 60) + ","); //Direccion empresa
                    readWriter.Write(Strings.Left(row["telefonoempresa"].ToString() + espacios60, 20) + ","); //Telefono Empresa
                    readWriter.Write(Strings.Right("000000" + row["codigociudadempresa"], 6) + ","); //Codigo ciudad Empresa
                    readWriter.Write(Strings.Left(row["ciudadempresa"].ToString() + espacios60, 20) + ","); //CIudad Empresa
                    readWriter.Write(Strings.Right("000" + row["codigodptoempresa"], 3) + ","); //Codigo Departamento Empresa
                    readWriter.Write(Strings.Left(row["deptoempresa"].ToString().ToUpper() + espacios60, 20) + ","); //Departamento Empresa
                    readWriter.Write("        " + ","); // Fecha excension
                    readWriter.Write("        " + ","); // Fecha fin excension
                    readWriter.Write("  " + ","); // Renovaciones CDAT
                    readWriter.Write("  " + ","); // Cuenta excenta
                    readWriter.Write("  " + ","); // Tipo id originaria
                    readWriter.Write("              " + ","); // num id originaria
                    readWriter.Write("   " + ","); // Tipo Entidad Originaria
                    readWriter.Write("   " + ","); // Cod Entidad Originaria
                    readWriter.Write("  " + ","); // Tipo Fideicomiso
                    readWriter.Write("            " + ","); // Numero Fideicomiso
                    readWriter.Write(espacios60 + ","); // Nombre Fideicomiso
                    readWriter.Write("    " + ","); // Tipo Deuda Cartera
                    readWriter.Write("    " + ","); // Tipo Poliza
                    readWriter.WriteLine("      "); // Codigo ramo

                    barraprogress.PerformStep();
                }
                readWriter.Close();
                ((IDisposable)readWriter).Dispose();
            }

            barraprogress.Close();
            barraprogress.Dispose();
        }
    }
}
