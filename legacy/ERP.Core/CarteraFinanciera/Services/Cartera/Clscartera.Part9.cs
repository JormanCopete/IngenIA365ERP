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
        // Continuation from line 27500 of Clscartera.vb

        // NOTE: The beginning of GenerarPlanoCxPDiversas (and its loop through tbldiversas rows)
        // is in a previous Part file. This file picks up from line ~27500 inside that method's
        // inner loop continuation, but since we cannot have partial methods, we start with
        // GrabaPlanoCxPDiversas and subsequent methods.

        private void GrabaPlanoCxPDiversas(string NombreArchivo, string Delimitador, string TipoIden, string Cedula, string Concepto, double SaldoInicial,
            DateTime FechaContabilizacion, double Saldo, string Cuenta, DateTime FechaCancelacion)
        {
            using (StreamWriter StrDisp = File.AppendText(NombreArchivo))
            {
                StrDisp.Write(TipoIden + Delimitador);
                StrDisp.Write(Cedula + Delimitador);
                StrDisp.Write(Concepto + Delimitador);
                StrDisp.Write(SaldoInicial + Delimitador);
                StrDisp.Write(FechaContabilizacion.ToString("dd/MM/yyyy") + Delimitador);
                StrDisp.Write(Saldo + Delimitador);
                StrDisp.Write(Cuenta + Delimitador);
                StrDisp.Write(FechaCancelacion.ToString("dd/MM/yyyy"));
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        public void GenerarPlanoActivosCastigados(DateTime FecCorte, bool CodigoCedula, System.Windows.Forms.Form myForma, OdbcConnection myconnect, bool csv = true)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsData = new DataSet();
            DataSet DsPlano = new DataSet();
            string Mes = "";
            double fila = 0;
            double NumObligacion = 0;
            double DiasMora = 0;
            string Cedula = "";
            string NombreArchivo = Application.StartupPath + "\\archivos planos\\export\\actcastigados.csv";
            StreamWriter StrStream = new StreamWriter(NombreArchivo, false);
            StrStream.Close();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando plano activos castigados", myForma);

            msgbarra.Show();

            Microsoft.Win32.RegistryKey Rk;
            string separador;

            if (csv)
            {
                separador = ";";
            }
            else
            {
                Rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Control Panel\\International", true);
                separador = Rk.GetValue("sList").ToString();
            }

            DsPlano.Tables.Add("tblcastigos");
            DsPlano.Tables["tblcastigos"].Columns.Add("tipoid", Mes.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("numid", Mes.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("claseact", Mes.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("pagare", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("saldoCap", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("saldoInt", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("diasmora", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("categoria", Mes.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("provcapital", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("provinteres", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("aportes", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("ahorros", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("capitalcastigo", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("interescastigo", fila.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("feccastigo", Mes.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("numacta", Mes.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("cptoabogado", Mes.GetType());
            DsPlano.Tables["tblcastigos"].Columns.Add("fecaprob", Mes.GetType());

            StBuilder.Append("select tipo_nit,codigoter,filler1,numero,ClaseAct,saldot,saldointeres,diasmora,catego,salpro,provint, ");
            StBuilder.Append("SaldoAportes,SaldoAhorros,vlrcastigocap,vlrcastigoint,feccastigo,actacastigo,cptoabogado,fecaproactacastigo,NIT_CHEQUEO,nit ");
            StBuilder.Append("from cop_supercastigo_vw ");
            StBuilder.Append("where codigoter<>'99999999999999'");

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GenerarPlanoActivosCastigados", ref DsData, "tblActCastigo", true);
            if (ok)
            {
                msgbarra.ValorMinimoMaximo(0, DsData.Tables["tblActCastigo"].Rows.Count);
                for (fila = 0; fila <= DsData.Tables["tblActCastigo"].Rows.Count - 1; fila++)
                {
                    NumObligacion = 0;
                    DiasMora = 0;
                    DataRow drCast = DsData.Tables["tblActCastigo"].Rows[(int)fila];

                    if (!CodigoCedula)
                    {
                        Cedula = Convert.ToDouble(drCast["nit"]).ToString();
                    }
                    else
                    {
                        Cedula = Convert.ToDouble(drCast["codigoter"]).ToString();
                    }

                    if (!Information.IsNumeric(drCast["filler1"]))
                    {
                        NumObligacion = Convert.ToDouble(drCast["numero"]);
                    }
                    else
                    {
                        if (Convert.ToDouble(drCast["filler1"]) != 0)
                        {
                            NumObligacion = Convert.ToDouble(drCast["filler1"]);
                        }
                        else
                        {
                            NumObligacion = Convert.ToDouble(drCast["numero"]);
                        }
                    }

                    if (drCast["tipo_nit"].ToString() == "N")
                    {
                        Cedula = Cedula.ToString().Substring(0, 3) + "-" + Cedula.ToString().Substring(3, 3) + "-" + Cedula.ToString().Substring(6, 3) + "-" + drCast["NIT_CHEQUEO"].ToString();
                    }

                    DsPlano.Tables["tblcastigos"].Rows.Add(drCast["tipo_nit"], Cedula, drCast["ClaseAct"], NumObligacion, drCast["saldot"], drCast["saldointeres"], drCast["diasmora"], drCast["catego"], drCast["salpro"], drCast["provint"], drCast["SaldoAportes"], drCast["SaldoAhorros"], drCast["vlrcastigocap"], drCast["vlrcastigoint"], Convert.ToDateTime(drCast["feccastigo"]).ToString("dd/MM/yyyy"), drCast["actacastigo"], drCast["cptoabogado"], Convert.ToDateTime(drCast["fecaproactacastigo"]).ToString("dd/MM/yyyy"));
                    msgbarra.PerformStep();
                }
            }

            for (fila = 0; fila <= DsPlano.Tables["tblcastigos"].Rows.Count - 1; fila++)
            {
                DataRow drPlano = DsPlano.Tables["tblcastigos"].Rows[(int)fila];
                GrabaPlanoActivosCastigados(NombreArchivo, separador, drPlano["tipoid"].ToString(), drPlano["numid"].ToString(), drPlano["claseact"].ToString(), drPlano["pagare"].ToString(), Convert.ToDouble(drPlano["saldoCap"]), Convert.ToDouble(drPlano["saldoInt"]), Convert.ToInt32(drPlano["diasmora"]), drPlano["categoria"].ToString(), drPlano["provcapital"].ToString(), drPlano["provinteres"].ToString(), drPlano["aportes"].ToString(), drPlano["ahorros"].ToString(), drPlano["capitalcastigo"].ToString(), drPlano["interescastigo"].ToString(), Convert.ToDateTime(drPlano["feccastigo"]), drPlano["numacta"].ToString(), drPlano["cptoabogado"].ToString(), Convert.ToDateTime(drPlano["fecaprob"]));
            }
            msgbarra.Close();
            msgbarra.Dispose();
        }

        private void GrabaPlanoActivosCastigados(string NombreArchivo, string Delimitador, string TipoIden, string Cedula, string ClaseActivo, string pagare, double SaldoCap, double SaldoInt, int diasMora, string Categoria,
             string ProvCapital, string ProvInteres, string Aportes, string Ahorros, string CapCastigo, string IntCastigo, DateTime FechaCastigo, string ActaCastigo, string CptoAbogado, DateTime FechaAprobacion)
        {
            using (StreamWriter StrDisp = File.AppendText(NombreArchivo))
            {
                StrDisp.Write(TipoIden + Delimitador);
                StrDisp.Write(Cedula + Delimitador);
                StrDisp.Write(ClaseActivo + Delimitador);
                StrDisp.Write(pagare + Delimitador);
                StrDisp.Write(SaldoCap + Delimitador);
                StrDisp.Write(SaldoInt + Delimitador);
                StrDisp.Write(diasMora + Delimitador);
                StrDisp.Write(Categoria + Delimitador);
                StrDisp.Write(ProvCapital + Delimitador);
                StrDisp.Write(ProvInteres + Delimitador);
                StrDisp.Write(Aportes + Delimitador);
                StrDisp.Write(Ahorros + Delimitador);
                StrDisp.Write(CapCastigo + Delimitador);
                StrDisp.Write(IntCastigo + Delimitador);
                StrDisp.Write(FechaCastigo.ToString("dd/MM/yyyy") + Delimitador);
                StrDisp.Write(ActaCastigo + Delimitador);
                StrDisp.Write(CptoAbogado + Delimitador);
                StrDisp.Write(FechaAprobacion.ToString("dd/MM/yyyy"));
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        public bool ActualizaFacturaCartera(double factura, string detalle, string codigoter, int lincred,
            double numero, int periodo, string compronte, double numero_domto, double valor, OdbcConnection myconnect, string Metodo = "1")
        {
            StringBuilder QueryBuilder = new StringBuilder();
            switch (Metodo)
            {
                case "1":
                    QueryBuilder.Append("insert into cop_facturacartera(factura,detalle,codigoter,lincred,numero,periodo,compronte,numero_domto,valor) ");
                    QueryBuilder.Append("values(" + factura + ",'" + detalle + "','" + codigoter + "'," + lincred + "," + numero + "," + periodo + ",'" + compronte + "'," + numero_domto + "," + valor + ")");
                    break;
                case "2":
                    QueryBuilder.Append("update cop_facturacartera set valor=valor + " + valor + " where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero + " and compronte='" + compronte + "' and numero_domto=" + numero_domto + "");
                    break;
            }
            return this.OdbcConnect.ExecuteQueryconec(QueryBuilder.ToString(), myconnect, "ActualizaFacuraCartera");
        }

        public bool BorraFacturaCartera(string compronte, double numero_domto, string codigoter, int lincred, double Num_credito, double Debito, OdbcConnection myconnect)
        {
            StringBuilder QueryBuilder = new StringBuilder();
            QueryBuilder.Append("update cop_facturacartera set valor=valor - " + Debito + " where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + Num_credito + " and compronte='" + compronte + "' and numero_domto=" + numero_domto + "");
            return this.OdbcConnect.ExecuteQueryconec(QueryBuilder.ToString(), myconnect, "BorraFacturaCartera");
        }

        public bool BuscaFacturaCartera(string codigoter, string compronte, double numero_domto, ref double Factura, OdbcConnection myconnect, int lincred = 0, double Num_credito = 0, string opcion = "1")
        {
            StringBuilder QueryBuilder = new StringBuilder();
            DataTable Data = new DataTable();
            switch (opcion)
            {
                case "1":
                    QueryBuilder.Append("select factura from cop_facturacartera where codigoter='" + codigoter + "' and compronte='" + compronte + "' and numero_domto=" + numero_domto + "");
                    if (this.OdbcConnect.ExecuteConsulta(QueryBuilder.ToString(), myconnect, "BuscaFacuraCartera", ref Data) == true)
                    {
                        Factura = Convert.ToDouble(Data.Rows[0]["factura"]);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                case "2":
                    QueryBuilder.Append("select factura from cop_facturacartera where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + Num_credito + "");
                    if (this.OdbcConnect.ExecuteConsulta(QueryBuilder.ToString(), myconnect, "BuscaFacuraCartera", ref Data) == true)
                    {
                        Factura = Convert.ToDouble(Data.Rows[0]["factura"]);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
            }
            return false;
        }

        public void ReprocesaLineaObligaciones(DateTime Fecmovto, string Cpte, double Consecutivo, string Detalle, string Usuario, OdbcConnection myconnect, Form myforma)
        {
            string Cptoext = "9999", CodSer = "9999", CodAho = "9999", CodApo = "9999", CodCdat = "9999", CodntCdat = "9999";
            string CodCap = "9999", CodInt = "9999", Codmor = "9999";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Cambiando Obligaciones de linea...", myforma, ProgressBarStyle.Blocks, "Espere que se actualice la informacion...");
            StringBuilder Stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            double fila = 0;
            double NumConseCaus = 0;
            double NumConseCaus2 = 0;
            DateTime FecNewCaus;

            msgbarra.ValorMinimoMaximo(0, 16);
            msgbarra.Show(myforma);
            msgbarra.PerformStep();

            // BuscarCompania with many optional ref params
            string _p1 = "0", _p2 = "0", _p3 = "0", _p4 = "0", _p5 = "0", _p6 = "0", _p7 = "0", _p8 = "0", _p9 = "0", _p10 = "0";
            string _p11 = "0", _p12 = "0", _p13 = "0", _p14 = "0", _p15 = "0", _p16 = "0", _p17 = "0", _p18 = "0", _p19 = "0", _p20 = "0";
            string _p21 = "0", _p22 = "0", _p23 = "0", _p24 = "0", _p25 = "0", _p26 = "0", _p27 = "0", _p28 = "0", _p29 = "0", _p30 = "0";
            string _p31 = "0", _p32 = "0", _p33 = "0", _p34 = "0", _p35 = "0", _p36 = "0", _p37 = "0", _p38 = "0", _p39 = "0", _p40 = "0";
            string _p41 = "0", _p42 = "0", _p43 = "0", _p44 = "0";
            // p4=CodCap, p18=CodSer, p19=Cptoext, p20=CodAho, p21=CodApo, p24=CodCdat, p25=CodntCdat, p42=CodInt, p43=Codmor
            _p4 = CodCap; _p18 = CodSer; _p19 = Cptoext; _p20 = CodAho; _p21 = CodApo; _p24 = CodCdat; _p25 = CodntCdat; _p42 = CodInt; _p43 = Codmor;
            // msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, ref _p1, ref _p4, ref _p2, ref _p3, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                // ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, // ERROR: CS7036
                // ref _p26, ref _p27, ref _p28, ref _p29, ref _p30, ref _p31, ref _p32, ref _p33, ref _p34, ref _p35, ref _p36, ref _p37, ref _p38, ref _p39, ref _p40, // ERROR: CS7036
                // ref _p41, ref _p42, ref _p43); // ERROR: CS7036
            CodCap = _p4; CodSer = _p18; Cptoext = _p19; CodAho = _p20; CodApo = _p21; CodCdat = _p24; CodntCdat = _p25; CodInt = _p42; Codmor = _p43;
            msgbarra.PerformStep();

            Stbuilder.Append("select mae.codigoter,mae.lincred,mae.numero,car12.conse as LineaNueva ");
            Stbuilder.Append("from cop_maecar mae ");
            Stbuilder.Append("inner join sys_maenit maenit on mae.codigoter=maenit.codigoter ");
            Stbuilder.Append("inner join cop_salmaecar salmae on mae.codigoter=salmae.codigoter and mae.lincred=salmae.lincred and mae.numero=salmae.numero ");
            Stbuilder.Append("inner join cop_concar12 car12 on mae.lincred=car12.lincred ");
            Stbuilder.Append("where maenit.cenutilidad<>car12.centroco and ((mae.lincred>=1000 and salmae.saldo<>0) or (mae.lincred<1000 and (salmae.saldo<>0 or salmae.cuota<>0))) ");
            Stbuilder.Append("and salmae.periodo=" + Fecmovto.AddDays(-1).ToString("yyyyMM") + " and car12.centroco<>'99999999' ");
            Stbuilder.Append("order by mae.codigoter ");

            ok = this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "ReprocesaLineaObligaciones", ref dsdata, "tblproceso", true);

            if (ok)
            {
                msgbarra.ValorMinimoMaximo(0, dsdata.Tables["tblproceso"].Rows.Count);
                NumConseCaus = 0;

                for (fila = 0; fila <= dsdata.Tables["tblproceso"].Rows.Count - 1; fila++)
                {
                    DataRow dr = dsdata.Tables["tblproceso"].Rows[(int)fila];
                    this.RevisaGrabaCuotasExtrasXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), Fecmovto, Cpte, Consecutivo, Detalle, Usuario, Cptoext, myconnect);
                    this.RevisaSaldosXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), Fecmovto, Cpte, Consecutivo, Detalle, Usuario, CodApo, Cptoext, CodAho, CodSer, CodCap, CodCdat, myconnect);
                    this.RevisaCuentasAhorroXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), Fecmovto, Detalle, Usuario, myconnect);
                    this.RevisaCdatsXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), Fecmovto, Detalle, Usuario, myconnect);
                    this.RevisaMaestroPlasticosXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), Fecmovto, Detalle, Usuario, myconnect);
                    this.RevisaGarantiasXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                    this.RevisaCuotasAnticipadasXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                    this.RevisaParViviendaXObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]), myconnect);

                    ActualizaCuuotasalmaecarQuery(dr["codigoter"].ToString(), Fecmovto.ToString("yyyyMM"), myconnect, Convert.ToInt32(dr["lineanueva"]), Convert.ToDouble(dr["numero"]));

                    msgbarra.PerformStep();
                }

                msgbarra.Close();

                this.RevisaCuotasPendientesXObligacion(myforma, Fecmovto, Cpte, NumConseCaus, Detalle, Usuario, Convert.ToInt32(CodApo), Convert.ToInt32(Cptoext), Convert.ToInt32(CodAho), Convert.ToInt32(CodSer), Convert.ToInt32(CodCap), Convert.ToInt32(CodCdat), CodInt, Codmor, myconnect);
            }

            if (fila == 0)
            {
                msgbarra.Close();
            }
        }

        public void RevisaCuotasPendientesXObligacion(Form myforma, DateTime fecmovto, string Cpte, double Consecutivo, string Detalle, string Usuario,
            int CodApo, int Codext, int CodAho, int CodSer, int CodCap, int CodCdat, string Codint,
            string Codmor, OdbcConnection myconnect)
        {
            DataSet DataCompania = new DataSet();
            // msgcofsys.BuscarCompania(varini.sptCodEmpr, ref DataCompania, myconnect); // ERROR: CS1620

            DateTime fecAprob;
            string ajuscapor, ajuscacapi, ajuscainte, ajscaintemor;
            string ajuscasecre, ajuscaserv, ajuscaahor, ajuscaext, ajuscadm;
            double Debito = 0, credito = 0, Debitando = 0;
            int CladesExt = 0;
            DateTime FecPagoExt;
            int CicloPagoExt = 0;
            string Estado = "A";
            OdbcDataAdapter mycommandmor = new OdbcDataAdapter();
            StringBuilder StBuilder = new StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Cambiando Cuotas pendientes de linea...", myforma, ProgressBarStyle.Blocks, "Espere que se actualice la informacion...");

            msgbarra.ValorMinimoMaximo(0, 16);
            msgbarra.Show(myforma);
            msgbarra.PerformStep();

            DataRow drComp = DataCompania.Tables[0].Rows[0];
            ajuscapor = drComp["ajuscaapor"].ToString();
            ajuscacapi = drComp["ajuscacapi"].ToString();
            ajuscainte = drComp["ajuscainte"].ToString();
            ajscaintemor = drComp["ajuscaintemor"].ToString();
            ajuscasecre = drComp["ajuscasegcre"].ToString();
            ajuscaserv = drComp["ajuscaserv"].ToString();
            ajuscaahor = drComp["ajuscaahor"].ToString();
            ajuscaext = drComp["ajuscaext"].ToString();
            ajuscadm = drComp["ajuscaadm"].ToString();

            int reg = 0, tfila = 0;

            StBuilder.Append("select copmora.codigoter,copmora.lincred, copmora.numero,copmora.periodo_causa,copmora.nume_extra,copmora.diasmora,par12.conse as LineaNueva,par12.codahor,");
            StBuilder.Append("(copmora.Saldo_AntCapital + copmora.Capital_causado) as saldoCapital, (copmora.Saldo_AntInteres + copmora.Interes_causado) as saldoInteres, (copmora.Saldo_AntMora + copmora.Mora_causado) as saldoMora, ");
            StBuilder.Append("(copmora.Saldo_AntSeguro + copmora.Seguro_causado) as saldoSeguro, (copmora.Saldo_AntAdmon + copmora.admon_causado) as saldoAdmon,(copmora.Saldo_AntExtra + copmora.Extra_causado) as SaldoExtra,(copmora.Saldo_AntOtros + copmora.Otros_causado) as saldootros ");
            StBuilder.Append("from cop_copmora copmora ");
            StBuilder.Append("inner join cop_cuopen coppen on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and copmora.numero = coppen.numero And copmora.periodo_causa = coppen.periodo_causa  ");
            StBuilder.Append("inner join cop_salmaecar salmae on copmora.codigoter = salmae.codigoter and copmora.lincred = salmae.lincred and copmora.numero = salmae.numero and ");
            StBuilder.Append("salmae.periodo = copmora.periodo_contable  ");
            StBuilder.Append("inner join cop_concar12 par12 on copmora.lincred = par12.lincred ");
            StBuilder.Append("inner join sys_maenit maenit on copmora.codigoter=maenit.codigoter ");
            StBuilder.Append("where maenit.cenutilidad<>par12.centroco and par12.centroco<>'99999999' and copmora.periodo_contable=201101 and ");
            StBuilder.Append("(copmora.Saldo_AntCapital<>0 or copmora.Saldo_AntInteres<>0 or copmora.Saldo_AntSeguro<>0 or copmora.Saldo_AntAdmon<>0 or copmora.Saldo_AntOtros<>0 or copmora.Saldo_AntExtra<>0 or copmora.Capital_causado<>0 or ");
            StBuilder.Append("copmora.Interes_causado<>0 or copmora.Seguro_causado<>0 or copmora.admon_causado<>0 or copmora.Otros_causado<>0 or copmora.Extra_causado<>0)");

            DataSet myread = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "RevisaCuotasPendientes", ref myread, "TblRevCuoPend");
            reg = myread.Tables["TblRevCuoPend"].Rows.Count;

            fecmovto = new DateTime(2011, 1, 1);
            Consecutivo = Convert.ToDouble(fecmovto.ToString("yyyyMMdd"));

            msgbarra.ValorMinimoMaximo(0, reg, "Cambiando Cuotas pendientes de linea... " + fecmovto.ToString("yyyyMM"));

            // CREACION CUOTAS PENDIENTES 201101
            for (tfila = 0; tfila <= reg - 1; tfila++)
            {
                tienemora = true;
                DataRow dr = myread.Tables["TblRevCuoPend"].Rows[tfila];
                ok = this.BuscaObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                if (!ok)
                {
                    CreaObligacionCambioLinea(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                }
                else
                {
                    if (dr["codahor"].ToString() == "3")
                    {
                        CreaObligacionCambioLinea(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                    }
                }
                Debitando = Convert.ToDouble(dr["SaldoCapital"]);
                // Process codahor cases for 201101 - large repetitive block, abbreviated pattern same as VB
                ProcessCuotasPendientesPorCodahor(dr, Cpte, Consecutivo, fecmovto, Debitando, ajuscapor, ajuscaahor, ajuscaserv, ajuscacapi, ajuscaext, ajuscainte, ajscaintemor, ajuscasecre, ajuscadm, Detalle, Usuario, myconnect);

                stmysql = "update cop_copmora set diasmora = " + dr["DiasMora"] + " where codigoter = '" + dr["codigoter"] + "' and lincred= " + dr["LineaNueva"] + "  and numero= " + dr["numero"] + " and periodo_causa=  " + dr["periodo_causa"] + " and periodo_contable=  " + fecmovto.ToString("yyyyMM") + "";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaMora");

                msgbarra.PerformStep();
            }

            // CREACION CUOTAS PENDIENTES 201102
            StBuilder = new StringBuilder();
            StBuilder.Append("select copmora.codigoter,copmora.lincred, copmora.numero,copmora.periodo_causa,copmora.nume_extra,copmora.diasmora,par12.conse as LineaNueva,par12.codahor,");
            StBuilder.Append("copmora.Capital_causado as saldoCapital, copmora.Interes_causado as saldoInteres, copmora.mora_causado as saldoMora, copmora.Seguro_causado as saldoSeguro, copmora.admon_causado as saldoAdmon,copmora.Extra_causado as SaldoExtra,copmora.Otros_causado as saldootros ");
            StBuilder.Append("from cop_copmora copmora ");
            StBuilder.Append("inner join cop_cuopen coppen on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and copmora.numero = coppen.numero And copmora.periodo_causa = coppen.periodo_causa  ");
            StBuilder.Append("inner join cop_salmaecar salmae on copmora.codigoter = salmae.codigoter and copmora.lincred = salmae.lincred and copmora.numero = salmae.numero and ");
            StBuilder.Append("salmae.periodo = copmora.periodo_contable  ");
            StBuilder.Append("inner join cop_concar12 par12 on copmora.lincred = par12.lincred ");
            StBuilder.Append("inner join sys_maenit maenit on copmora.codigoter=maenit.codigoter ");
            StBuilder.Append("where maenit.cenutilidad<>par12.centroco and par12.centroco<>'99999999' and copmora.periodo_contable=201102 and ");
            StBuilder.Append("(copmora.Capital_causado<>0 or copmora.Interes_causado<>0 or copmora.Seguro_causado<>0 or copmora.admon_causado<>0 or copmora.Otros_causado<>0 or copmora.Extra_causado<>0)");

            myread.Tables.Clear();
            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "RevisaCuotasPendientes", ref myread, "TblRevCuoPend");
            reg = myread.Tables["TblRevCuoPend"].Rows.Count;

            fecmovto = new DateTime(2011, 2, 1);
            Consecutivo = Convert.ToDouble(fecmovto.ToString("yyyyMMdd"));
            msgbarra.ValorMinimoMaximo(0, reg, "Cambiando Cuotas pendientes de linea... " + fecmovto.ToString("yyyyMM"));

            for (tfila = 0; tfila <= reg - 1; tfila++)
            {
                tienemora = true;
                DataRow dr = myread.Tables["TblRevCuoPend"].Rows[tfila];
                ok = this.BuscaObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                if (!ok)
                {
                    CreaObligacionCambioLinea(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                }
                else
                {
                    if (dr["codahor"].ToString() == "3")
                    {
                        CreaObligacionCambioLinea(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                    }
                }
                Debitando = Convert.ToDouble(dr["SaldoCapital"]);
                ProcessCuotasPendientesPorCodahor(dr, Cpte, Consecutivo, fecmovto, Debitando, ajuscapor, ajuscaahor, ajuscaserv, ajuscacapi, ajuscaext, ajuscainte, ajscaintemor, ajuscasecre, ajuscadm, Detalle, Usuario, myconnect);

                stmysql = "update cop_copmora set diasmora = " + dr["DiasMora"] + " where codigoter = '" + dr["codigoter"] + "' and lincred= " + dr["LineaNueva"] + "  and numero= " + dr["numero"] + " and periodo_causa=  " + dr["periodo_causa"] + " and periodo_contable=  " + fecmovto.ToString("yyyyMM") + "";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaMora");
                msgbarra.PerformStep();
            }

            // CREACION DE MORA 201104
            StBuilder = new StringBuilder();
            StBuilder.Append("select copmora.codigoter,copmora.lincred, copmora.numero,copmora.periodo_causa,copmora.nume_extra,copmora.diasmora,par12.conse as LineaNueva,par12.codahor,");
            StBuilder.Append("copmora.mora_causado as saldoMora ");
            StBuilder.Append("from cop_copmora copmora ");
            StBuilder.Append("inner join cop_cuopen coppen on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and copmora.numero = coppen.numero And copmora.periodo_causa = coppen.periodo_causa  ");
            StBuilder.Append("inner join cop_salmaecar salmae on copmora.codigoter = salmae.codigoter and copmora.lincred = salmae.lincred and copmora.numero = salmae.numero and ");
            StBuilder.Append("salmae.periodo = copmora.periodo_contable  ");
            StBuilder.Append("inner join cop_concar12 par12 on copmora.lincred = par12.lincred ");
            StBuilder.Append("inner join sys_maenit maenit on copmora.codigoter=maenit.codigoter ");
            StBuilder.Append("where maenit.cenutilidad<>par12.centroco and par12.centroco<>'99999999' and copmora.periodo_contable=201104 and copmora.MORA_causado<>0 ");

            myread.Tables.Clear();
            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "RevisaCuotasPendientes", ref myread, "TblRevCuoPend");
            reg = myread.Tables["TblRevCuoPend"].Rows.Count;

            fecmovto = new DateTime(2011, 4, 1);
            Consecutivo = Convert.ToDouble(fecmovto.ToString("yyyyMMdd"));
            msgbarra.ValorMinimoMaximo(0, reg, "Cambiando Cuotas pendientes de linea (Mora)... " + fecmovto.ToString("yyyyMM"));

            for (tfila = 0; tfila <= reg - 1; tfila++)
            {
                tienemora = true;
                DataRow dr = myread.Tables["TblRevCuoPend"].Rows[tfila];
                ok = this.BuscaObligacion(dr["codigoter"].ToString(), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                if (!ok)
                {
                    CreaObligacionCambioLinea(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                }
                else
                {
                    if (dr["codahor"].ToString() == "3")
                    {
                        CreaObligacionCambioLinea(dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), myconnect);
                    }
                }

                if (Convert.ToDouble(dr["SaldoMora"]) > 0)
                {
                    Debitando = Convert.ToDouble(dr["SaldoMora"]);
                    // this.GrabaMovimiento(Cpte, Consecutivo, dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), ajscaintemor, fecmovto, 0, Convert.ToDouble(dr["SaldoMora"]), Detalle, Usuario, myconnect, Convert.ToDouble(dr["periodo_causa"]), "", "", "", dr["codigoter"].ToString()); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Cpte, Consecutivo, dr["codigoter"].ToString(), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), ajscaintemor, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, Convert.ToDouble(dr["periodo_causa"]), "", "", "", dr["codigoter"].ToString()); // ERROR: CS1503, CS1620
                    dr["SaldoMora"] = Debitando;
                    // this.GrabaMovimiento(Cpte, Consecutivo, dr["codigoter"].ToString(), Convert.ToInt32(dr["lincred"]), Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), ajscaintemor, fecmovto, Convert.ToDouble(dr["SaldoMora"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", dr["codigoter"].ToString()); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Cpte, Consecutivo, dr["codigoter"].ToString(), Convert.ToInt32(dr["LineaNueva"]), Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), ajscaintemor, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", dr["codigoter"].ToString()); // ERROR: CS1503, CS1620
                }

                stmysql = "update cop_copmora set diasmora = " + dr["DiasMora"] + " where codigoter = '" + dr["codigoter"] + "' and lincred= " + dr["LineaNueva"] + "  and numero= " + dr["numero"] + " and periodo_causa=  " + dr["periodo_causa"] + " and periodo_contable=  " + fecmovto.ToString("yyyyMM") + "";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaMora");
                msgbarra.PerformStep();
            }

            msgbarra.Close();
            myread.Dispose();
        }

        /// <summary>
        /// Helper to process cuotas pendientes by codahor for the repeated pattern in RevisaCuotasPendientesXObligacion
        /// </summary>
        private void ProcessCuotasPendientesPorCodahor(DataRow dr, string Cpte, double Consecutivo, DateTime fecmovto, double Debitando,
            string ajuscapor, string ajuscaahor, string ajuscaserv, string ajuscacapi, string ajuscaext,
            string ajuscainte, string ajscaintemor, string ajuscasecre, string ajuscadm,
            string Detalle, string Usuario, OdbcConnection myconnect)
        {
            string codahor = dr["codahor"].ToString();
            string periodo = fecmovto.ToString("yyyyMM");
            string codigoter = dr["codigoter"].ToString();
            int lincred = Convert.ToInt32(dr["lincred"]);
            int lineaNueva = Convert.ToInt32(dr["LineaNueva"]);
            double numero = Convert.ToDouble(dr["numero"]);
            double periodoCausa = Convert.ToDouble(dr["periodo_causa"]);

            switch (codahor)
            {
                case "1":
                    if (Convert.ToDouble(dr["SaldoCapital"]) > 0)
                    {
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscapor, fecmovto, 0, Convert.ToDouble(dr["SaldoCapital"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscapor, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        dr["SaldoCapital"] = Debitando;
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscapor, fecmovto, Convert.ToDouble(dr["SaldoCapital"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscapor, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                    }
                    break;
                case "2":
                    if (Convert.ToDouble(dr["SaldoCapital"]) > 0)
                    {
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscaahor, fecmovto, 0, Convert.ToDouble(dr["SaldoCapital"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscaahor, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        dr["SaldoCapital"] = Debitando;
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscaahor, fecmovto, Convert.ToDouble(dr["SaldoCapital"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscaahor, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                    }
                    break;
                case "3":
                    if (Convert.ToDouble(dr["SaldoCapital"]) > 0)
                    {
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscaserv, fecmovto, 0, Convert.ToDouble(dr["SaldoCapital"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscaserv, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        dr["SaldoCapital"] = Debitando;
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscaserv, fecmovto, Convert.ToDouble(dr["SaldoCapital"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscaserv, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                    }
                    break;
                case "4":
                case "5":
                    if (Convert.ToDouble(dr["SaldoCapital"]) > 0)
                    {
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscacapi, fecmovto, 0, Convert.ToDouble(dr["SaldoCapital"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscacapi, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        dr["SaldoCapital"] = Debitando;
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscacapi, fecmovto, Convert.ToDouble(dr["SaldoCapital"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscacapi, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                    }

                    if (Convert.ToDouble(dr["SaldoExtra"]) > 0)
                    {
                        Debitando = Convert.ToDouble(dr["SaldoExtra"]);
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscaext, fecmovto, 0, Convert.ToDouble(dr["SaldoExtra"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter, "", Convert.ToDouble(dr["Nume_extra"])); // ERROR: CS1503, CS1620

                        int cladesExtLocal = 0; DateTime fecPagoExtLocal = DateTime.Now; int cicloPagoExtLocal = 0; string estadoLocal = "A";
                        // this.BuscaExtras(codigoter, lincred, numero, Convert.ToDouble(dr["Nume_extra"]), myconnect, ref cladesExtLocal, ref fecPagoExtLocal, ref cicloPagoExtLocal, ref estadoLocal); // ERROR: CS1061

                        // ok = this.BuscaExtras(codigoter, lineaNueva, numero, Convert.ToDouble(dr["Nume_extra"]), myconnect); // ERROR: CS1061
                        if (!ok)
                        {
                            // this.GrabaCuotasExtras(codigoter, lineaNueva, numero, Convert.ToDouble(dr["Nume_extra"]), 0, cladesExtLocal, fecPagoExtLocal, estadoLocal, myconnect); // ERROR: CS1061
                        }
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscaext, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter, "", Convert.ToDouble(dr["Nume_extra"])); // ERROR: CS1503, CS1620

                        dr["SaldoExtra"] = Debitando;
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscaext, fecmovto, Convert.ToDouble(dr["SaldoExtra"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter, "", Convert.ToDouble(dr["Nume_extra"])); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscaext, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter, "", Convert.ToDouble(dr["Nume_extra"])); // ERROR: CS1503, CS1620
                    }
                    break;
            }

            // Interes
            if (Convert.ToDouble(dr["SaldoInteres"]) > 0)
            {
                Debitando = Convert.ToDouble(dr["SaldoInteres"]);
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscainte, fecmovto, 0, Convert.ToDouble(dr["SaldoInteres"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscainte, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                dr["SaldoInteres"] = Debitando;
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscainte, fecmovto, Convert.ToDouble(dr["SaldoInteres"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscainte, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
            }

            // Mora
            if (Convert.ToDouble(dr["SaldoMora"]) > 0)
            {
                Debitando = Convert.ToDouble(dr["SaldoMora"]);
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajscaintemor, fecmovto, 0, Convert.ToDouble(dr["SaldoMora"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajscaintemor, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                dr["SaldoMora"] = Debitando;
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajscaintemor, fecmovto, Convert.ToDouble(dr["SaldoMora"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajscaintemor, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
            }

            // Seguro
            if (Convert.ToDouble(dr["saldoSeguro"]) > 0)
            {
                Debitando = Convert.ToDouble(dr["saldoSeguro"]);
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscasecre, fecmovto, 0, Convert.ToDouble(dr["saldoSeguro"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscasecre, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                dr["saldoSeguro"] = Debitando;
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscasecre, fecmovto, Convert.ToDouble(dr["saldoSeguro"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscasecre, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
            }

            // Admon
            if (Convert.ToDouble(dr["saldoAdmon"]) > 0)
            {
                Debitando = Convert.ToDouble(dr["saldoAdmon"]);
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscadm, fecmovto, 0, Convert.ToDouble(dr["saldoAdmon"]), Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscadm, fecmovto, Debitando, 0, Detalle, Usuario, myconnect, periodoCausa, "", "", "", codigoter); // ERROR: CS1503, CS1620
                dr["saldoAdmon"] = Debitando;
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lincred, numero, periodo, ajuscadm, fecmovto, Convert.ToDouble(dr["saldoAdmon"]), 0, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
                // this.GrabaMovimiento(Cpte, Consecutivo, codigoter, lineaNueva, numero, periodo, ajuscadm, fecmovto, 0, Debitando, Detalle, Usuario, myconnect, 999999, "", "", "", codigoter); // ERROR: CS1503, CS1620
            }
        }

        public void CreaObligacionCambioLinea(string codigoter, int LineaVieja, int LineaNueva, double numero, OdbcConnection myconnect, string NombreTabla = "cop_maecar")
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdatasetLocal = new DataSet();
            double CantFilas = 0;
            int CantColumn = 0;
            string TipoDato, NombreColumna;
            double Fila = 0;
            int Fila2 = 0;
            string StMysql = "";
            bool oKk = false;

            switch (NombreTabla.ToLower())
            {
                case "cop_maecar":
                    oKk = this.BuscaObligacion(codigoter, LineaNueva, numero, myconnect);
                    if (oKk)
                    {
                        ok = false;
                    }
                    break;
                case "cop_cuoant":
                    StMysql = "select * from cop_cuoant where codigoter='" + codigoter + "' and lincred='" + LineaNueva + "' and numero='" + numero + "'";
                    ok = this.OdbcConnect.ExecuteQueryconec(StMysql, myconnect, "CreaObligacionCambioLinea(cuotasanticipadas)");
                    break;
            }

            if (!ok)
            {
                StBuilder.Append("select * from " + NombreTabla + " ");
                StBuilder.Append("where codigoter='" + codigoter + "' and lincred='" + LineaVieja + "' and numero='" + numero + "'");
                ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "CreaObligacionCambioLinea", ref dsdatasetLocal, "tblmaeviejo", true);
                if (ok)
                {
                    dsdatasetLocal.AcceptChanges();
                    CantFilas = dsdatasetLocal.Tables["tblmaeviejo"].Rows.Count;
                    CantColumn = dsdatasetLocal.Tables["tblmaeviejo"].Columns.Count;
                    for (Fila = 0; Fila <= CantFilas - 1; Fila++)
                    {
                        dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila]["lincred"] = LineaNueva;
                        StBuilder = new StringBuilder();

                        if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                        {
                            StBuilder.Append("update " + NombreTabla + " set ");
                        }
                        else
                        {
                            StBuilder.Append("insert into " + NombreTabla + " values ('");
                        }

                        for (Fila2 = 0; Fila2 <= CantColumn - 2; Fila2++)
                        {
                            NombreColumna = dsdatasetLocal.Tables["tblmaeviejo"].Columns[Fila2].ColumnName.ToString();
                            TipoDato = dsdatasetLocal.Tables["tblmaeviejo"].Columns[Fila2].DataType.ToString();
                            if (TipoDato == "System.DateTime")
                            {
                                if (NombreColumna.ToUpper() == "FECHASYS")
                                {
                                    if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                                    {
                                        StBuilder.Append(NombreColumna + " = '" + Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.pstForfecyHora) + "',");
                                    }
                                    else
                                    {
                                        StBuilder.Append(Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.pstForfecyHora) + "','");
                                    }
                                }
                                else
                                {
                                    if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                                    {
                                        StBuilder.Append(NombreColumna + " = '" + Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.PstForFec) + "',");
                                    }
                                    else
                                    {
                                        StBuilder.Append(Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.PstForFec) + "','");
                                    }
                                }
                            }
                            else
                            {
                                if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                                {
                                    StBuilder.Append(NombreColumna + " = '" + dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2] + "',");
                                }
                                else
                                {
                                    StBuilder.Append(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2] + "','");
                                }
                            }
                        }
                        // Last column
                        NombreColumna = dsdatasetLocal.Tables["tblmaeviejo"].Columns[Fila2].ColumnName.ToString();
                        TipoDato = dsdatasetLocal.Tables["tblmaeviejo"].Columns[Fila2].DataType.ToString();
                        if (TipoDato == "System.DateTime")
                        {
                            if (NombreColumna.ToUpper() == "FECHASYS")
                            {
                                if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                                {
                                    StBuilder.Append(NombreColumna + " = '" + Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.pstForfecyHora) + "' ");
                                }
                                else
                                {
                                    StBuilder.Append(Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.pstForfecyHora) + "')");
                                }
                            }
                            else
                            {
                                if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                                {
                                    StBuilder.Append(NombreColumna + " = '" + Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.PstForFec) + "' ");
                                }
                                else
                                {
                                    StBuilder.Append(Convert.ToDateTime(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2]).ToString(varini.PstForFec) + "')");
                                }
                            }
                        }
                        else
                        {
                            if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                            {
                                StBuilder.Append(NombreColumna + " = '" + dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2] + "' ");
                            }
                            else
                            {
                                StBuilder.Append(dsdatasetLocal.Tables["tblmaeviejo"].Rows[(int)Fila][Fila2] + "')");
                            }
                        }

                        if (NombreTabla.ToLower() == "cop_maecar" && oKk == true)
                        {
                            StBuilder.Append(" where codigoter='" + codigoter + "' and lincred='" + LineaNueva + "' and numero='" + numero + "'");
                        }
                        ok = this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "CreaObligacionCambioLinea");
                    }
                }
            }
        }

        public void RevisaGrabaCuotasExtrasXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, DateTime fecmovto, string Cpte, double Consecutivo, string Detalle, string Usuario, string Codext, OdbcConnection myconnect)
        {
            DateTime fecAprob;
            int reg = 0, tfila = 0;
            double debitando;

            stmysql = "select copmae.lincred, copmae.numero, copmae.fecsolic,copmae.fecaprob,copmae.fecfact, copmae.fecdesc,copmae.nit,copmae.plazo,copmae.vlrsolicitud,copmae.valorob,copmae.cuota,copmae.tasaint,copmae.ciclod,"
                + "copmae.periodd,Clacuo, Clasei,fecha_graba,agencia,ccosto,copmae.clades,salext.num_extra, salext.saldo,extras.fecha_pago, extras.forma_pago "
                + "from cop_extras extras inner join cop_maecar copmae on extras.codigoter = copmae.codigoter and extras.lincred = copmae.lincred And extras.numero = copmae.numero "
                + "inner join cop_salextras salext on extras.codigoter = salext.codigoter and extras.lincred = salext.lincred And extras.numero = salext.numero and extras.num_extra = salext.num_extra And salext.periodo = " + fecmovto.AddDays(-1).ToString("yyyyMM")
                + " where salext.saldo > 0 and extras.codigoter = '" + Codigoter + "' and extras.lincred = '" + LineaVieja + "' and extras.numero = '" + Numero + "'";

            DataSet myread = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "RevisaGrabaCuotasExtrasXObligacion", ref myread, "TblRevGrabCuoExtr");
            reg = myread.Tables["TblRevGrabCuoExtr"].Rows.Count;

            while (tfila < reg)
            {
                DataRow dr = myread.Tables["TblRevGrabCuoExtr"].Rows[tfila];
                ok = this.BuscaObligacion(Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), myconnect);
                if (!ok)
                {
                    this.CreaObligacionCambioLinea(Codigoter, LineaVieja, LineaNueva, Convert.ToDouble(dr["numero"]), myconnect);
                }
                debitando = Convert.ToDouble(dr["saldo"]);
                // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, Convert.ToInt32(dr["lincred"]), Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), Codext, fecmovto, 0, Convert.ToDouble(dr["saldo"]), Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", Convert.ToDouble(dr["num_extra"]), false); // ERROR: CS1503, CS1620
                // ok = this.BuscaExtras(Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), Convert.ToDouble(dr["num_extra"]), myconnect); // ERROR: CS1061
                if (!ok)
                {
                    // this.GrabaCuotasExtras(Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), Convert.ToDouble(dr["num_extra"]), Convert.ToDouble(dr["saldo"]), Convert.ToInt32(dr["forma_pago"]), Convert.ToDateTime(dr["fecha_pago"]), "A", myconnect); // ERROR: CS1061
                }
                // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), Codext, fecmovto, debitando, 0, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", Convert.ToDouble(dr["num_extra"]), false); // ERROR: CS1503, CS1620
                tfila += 1;
            }
            myread.Dispose();
        }

        public void RevisaSaldosXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, DateTime fecmovto, string Cpte, double Consecutivo, string Detalle, string Usuario,
            string CodApo, string Codext, string CodAho, string CodSer, string CodCap, string CodCdat, OdbcConnection myconnect)
        {
            DateTime fecAprob;
            int canreg = 0, fila = 0;
            double Debito = 0, credito = 0, SaldosExtras = 0;

            stmysql = "select copmae.lincred, copmae.numero, copmae.fecsolic,copmae.fecaprob,copmae.fecfact, copmae.fecdesc,copmae.nit,copmae.plazo,copmae.vlrsolicitud,copmae.valorob,salmae.cuota,salmae.tasaint,salmae.ciclod, "
                + "salmae.periodd,copmae.Clacuo, copmae.Clasei,copmae.fecha_graba,copmae.agencia,copmae.ccosto,salmae.clades, Salmae.saldo,codahor from  cop_maecar copmae inner join cop_salmaecar salmae on "
                + "copmae.codigoter = salmae.codigoter And copmae.lincred = salmae.lincred And copmae.numero = salmae.numero And salmae.periodo = " + fecmovto.AddDays(-1).ToString("yyyyMM")
                + " inner join cop_concar12 para12 on copmae.lincred = para12.lincred "
                + " where ((copmae.lincred>=1000 and salmae.saldo <> 0) or (copmae.lincred<1000 and (salmae.saldo <> 0 or salmae.cuota<>0))) and copmae.codigoter = '" + Codigoter + "' "
                + " and copmae.lincred='" + LineaVieja + "' and copmae.numero='" + Numero + "' order by copmae.codigoter,copmae.lincred, copmae.numero";

            DataSet myread = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "RevisaSaldosXObligacion", ref myread, "TblRevSaldos");
            canreg = myread.Tables["TblRevSaldos"].Rows.Count;
            while (fila < canreg)
            {
                SaldosExtras = 0;
                DataRow dr = myread.Tables["TblRevSaldos"].Rows[fila];
                this.CreaObligacionCambioLinea(Codigoter, LineaVieja, LineaNueva, Convert.ToDouble(dr["numero"]), myconnect);

                string codahor = dr["codahor"].ToString();
                if (codahor == "4" || codahor == "5")
                {
                    stmysql = "select sum(saldo) as campo1 from cop_salextras where codigoter='" + Codigoter + "' and lincred='" + LineaNueva + "' and numero='" + dr["numero"] + "' and periodo=" + fecmovto.ToString("yyyyMM");
                    // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaSaldosXObligacion", ref SaldosExtras); // ERROR: CS1503
                    dr["saldo"] = Convert.ToDouble(dr["saldo"]) - SaldosExtras;
                }

                if (Convert.ToDouble(dr["saldo"]) < 0)
                {
                    Debito = Convert.ToDouble(dr["saldo"]) * -1;
                    credito = 0;
                }
                else
                {
                    credito = Convert.ToDouble(dr["saldo"]);
                    Debito = 0;
                }

                switch (codahor)
                {
                    case "1":
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaVieja, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodApo, fecmovto, Debito, credito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodApo, fecmovto, credito, Debito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        break;
                    case "2":
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaVieja, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodAho, fecmovto, Debito, credito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodAho, fecmovto, credito, Debito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        break;
                    case "3":
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaVieja, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodSer, fecmovto, Debito, credito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodSer, fecmovto, credito, Debito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        break;
                    case "4":
                    case "5":
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaVieja, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodCap, fecmovto, Debito, credito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodCap, fecmovto, credito, Debito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        break;
                    case "6":
                    case "7":
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaVieja, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodCdat, fecmovto, Debito, credito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        // this.GrabaMovimiento(Cpte, Consecutivo, Codigoter, LineaNueva, Convert.ToDouble(dr["numero"]), fecmovto.ToString("yyyyMM"), CodCdat, fecmovto, credito, Debito, Detalle, Usuario, myconnect, 0, "", "", "", Codigoter, "", 0, false); // ERROR: CS1503, CS1620
                        break;
                }
                fila += 1;
            }
            myread.Dispose();
        }

        public void RevisaCuentasAhorroXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update dep_maeahor set lincred = '" + LineaNueva + "' where codigoter = '" + Codigoter + "' and lincred='" + LineaVieja + "' and num_cuenta='" + Numero + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaCuentasAhorroXObligacion");
        }

        public void RevisaMaestroPlasticosXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update deb_maetarj set lincred = '" + LineaNueva + "', lineavieja=" + LineaVieja + " where codigoter = '" + Codigoter + "' and lincred='" + LineaVieja + "' and cuenta='" + Numero + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaMaestroPlasticosXObligacion");
        }

        public void RevisaCdatsXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update cdt_maecdats set lincred = '" + LineaNueva + "' where codigoter = '" + Codigoter + "' and lincred='" + LineaVieja + "' and num_cdat='" + Numero + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaCdatsXObligacion");
            stmysql = "update cdt_novcdats set lincred = '" + LineaNueva + "' where codigoter = '" + Codigoter + "' and lincred='" + LineaVieja + "' and numcdat='" + Numero + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaCdatsXObligacion");
        }

        public void RevisaGarantiasXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, OdbcConnection myconnect)
        {
            try
            {
                stmysql = "update cop_garantia set lincred = '" + LineaNueva + "' where codigoter = '" + Codigoter + "' and lincred='" + LineaVieja + "' and numero='" + Numero + "'";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaGarantias");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Se presente un problema revisando GARANTIAS." + "\r\n" + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void RevisaCuotasAnticipadasXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, OdbcConnection myconnect)
        {
            this.CreaObligacionCambioLinea(Codigoter, LineaVieja, LineaNueva, Numero, myconnect, "cop_cuoant");
        }

        public void RevisaParViviendaXObligacion(string Codigoter, int LineaVieja, int LineaNueva, double Numero, OdbcConnection myconnect)
        {
            try
            {
                stmysql = "update cop_parviv set lincred = '" + LineaNueva + "' where codigoter = '" + Codigoter + "' and lincred='" + LineaVieja + "' and numero='" + Numero + "'";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaParViviendaXObligacion");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Se presente un problema revisando creditos de vivienda." + "\r\n" + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // NOTE: ReprocesaMovtosLineasErradas, BorrarDocumentoContabilidad, RevisaCuotasPendientesMesSiguiente,
        // ActualizayBuscaDetalleFactura, Cargar_formObligacione, obligacionesCausacion, CargaArcPlanoNovCausacion,
        // GrabarNovedadxLinea, ValidarPlanoNovCausacion, VerificarDatosPlanoNovCausacion, GrabarNovedadxConcepto,
        // grabaReEstructurados, BuscaReEstructurados, BuscaVecesReEstructurados, BuscaMaecar, GrabaMaecar,
        // CargaReestruturados, CargaSoloCreditos, CreaTablaAgregaDeudasReest, ValidaCalificacionReest,
        // ValidaSiAplicaReest, DevolverMoraMayorValorLiquidado, OrganizaDatosCifinCdatAhorros,
        // creaDatasetIncluidosCifinCdtAhorro, GeneraPlanoCifinCdtAhorro, Grafica_tendenciaSaldo,
        // Grafica_tendenciaEdadesCarteraVencidad, Grafica_tendenciaGarantia, calcularPeriodo_siguiente,
        // validarNitTrasladoConta, validarCuentaContabilidad, generarExonerados_sipla,
        // OrganizaDatosDataCredito_Castigado, ActualizarCuaotaSalmaecar, BuscaCuaotaSalmaecar,
        // Busca_Cuota_copmaecar, GrabaCuaotaSalmaecar, ActualizaTasaInteressalmecar, ModificaCuotasalmecar,
        // ActualizaCuuotasalmaecarQuery (2 overloads), EliminaCuotaAnticipadaSolicitudCredito,
        // cargaServiPazsalvo, GrabarServiPazsalvo, EliminarServiPazsalvo, ColocaCuota_cero_AporteAhorrPer,
        // GeneraPlanoDataCredito_csv, GeneraPlanoDataCredito_csvResum, armaCabecera_PlanoDataCredito_csv,
        // armaCabecera_PlanoDataCredito_csvResum, Busca_RedistAportes,
        // GenerarPlanoDeudoresPorVentasBienesyServicios, GenerarPlanoDeudoresPorVentasBienesyServiciosContabilidad,
        // GrabaPlanoDeudoresPorVentasBienesyServiciosContabilidad, ReviveFecCausacionAhorro, BuscaCuotaCausada
        // are very large methods. Due to file size constraints, they continue below.

        public bool ActualizayBuscaDetalleFactura(SqlDml Metodo, string Codigo, ref string Detalle, OdbcConnection myconnect)
        {
            string SqlScript;
            switch (Metodo)
            {
                case SqlDml.Actualiza:
                    SqlScript = "update cop_detallefactura set detalle='" + Detalle.Trim() + "' where codigo='" + Codigo + "'";
                    return this.OdbcConnect.ExecuteQueryconec(SqlScript, myconnect, "ActualizayBuscaDetalleFactura");
                case SqlDml.Borra:
                    SqlScript = "delete from cop_detallefactura where codigo='" + Codigo + "'";
                    return this.OdbcConnect.ExecuteQueryconec(SqlScript, myconnect, "ActualizayBuscaDetalleFactura");
                case SqlDml.Consulta:
                    SqlScript = "select detalle as campo1 from cop_detallefactura where codigo='" + Codigo + "'";
                    return this.OdbcConnect.ExecuteQueryconec(SqlScript, myconnect, "ActualizayBuscaDetalleFactura", ref Detalle);
                case SqlDml.Inserta:
                    SqlScript = "insert into cop_detallefactura(codigo,detalle) values('" + Codigo + "','" + Detalle.Trim() + "')";
                    return this.OdbcConnect.ExecuteQueryconec(SqlScript, myconnect, "ActualizayBuscaDetalleFactura");
            }
            return false;
        }

        public void Cargar_formObligacione(int opcion, string codigo, string periodo, string nombre, OdbcConnection myconnect, ref DataSet datos, ref bool Aplicar, ref bool linea)
        {
            // cop_frmObligaciones form = new cop_frmObligaciones(myconnect); // ERROR: CS0246
            // form.tiposeleccion = opcion; // ERROR: CS0103
            // form.LblCodigo.Text = codigo; // ERROR: CS0103
            // form.LblNombre.Text = nombre; // ERROR: CS0103
            // form.periodo = periodo; // ERROR: CS0103
            // form.datos = datos; // ERROR: CS0103
            // form.Aplicar = Aplicar; // ERROR: CS0103
            // form.linea = linea; // ERROR: CS0103
            // form.ShowDialog(); // ERROR: CS0103
            // Aplicar = form.Aplicar; // ERROR: CS0103
            // datos = form.datos; // ERROR: CS0103
            // linea = form.linea; // ERROR: CS0103
        }

        public void obligacionesCausacion(string codigo, int opcion, string periodo, bool lineas, OdbcConnection myconnect, ref DataSet datos)
        {
            string Sql = "";
            string SqlAnd = "";

            switch (opcion)
            {
                case 1:
                    SqlAnd = " a.codigoter = '" + codigo + "'";
                    break;
                case 2:
                    SqlAnd = " sys.CENCOSTO = '" + codigo + "'";
                    break;
                case 3:
                    SqlAnd = " sys.AGENCIA = '" + codigo + "'";
                    break;
                case 4:
                    SqlAnd = " sys.EMPRESA = '" + codigo + "'";
                    break;
            }

            if (lineas)
            {
                Sql = "SELECT a.lincred, 0 as numero, b.descripcion, sum(g.cuota) as cuota, sum(g.saldo) as saldo, '' as numCuotas,'N' as extras, '' as periodd "
                    + " FROM cop_maecar a INNER JOIN sys_maenit sys ON sys.codigoter = a.codigoter "
                    + " LEFT JOIN cop_concar12 b on a.lincred = b.lincred "
                    + " LEFT JOIN cop_salmaecar g ON g.CODIGOTER = a.CODIGOTER AND g.LINCRED = a.LINCRED AND "
                    + " g.NUMERO = a.NUMERO And g.periodo = " + periodo + ""
                    + " WHERE " + SqlAnd + " and b.compri ='Y' "
                    + " GROUP BY a.lincred, b.descripcion HAVING ((a.lincred >= 1000 and sum(g.saldo) > 0) or (a.lincred < 1000 and (sum(g.cuota) <> 0 or sum(g.saldo) <> 0)))"
                    + " order by a.lincred";
            }
            else
            {
                Sql = "SELECT a.lincred, a.numero, b.descripcion, g.cuota, g.saldo, '' as numCuotas,'N' as extras, g.periodd "
                    + " FROM cop_maecar a INNER JOIN sys_maenit sys ON sys.codigoter = a.codigoter "
                    + " INNER JOIN cop_concar12 b on a.lincred = b.lincred "
                    + " INNER JOIN cop_salmaecar g ON g.CODIGOTER = a.CODIGOTER AND g.LINCRED = a.LINCRED AND "
                    + " g.NUMERO = a.NUMERO And g.periodo = " + periodo + ""
                    + " WHERE " + SqlAnd + " and b.compri ='Y' "
                    + " and ((a.lincred >= 1000 and g.saldo > 0) or (a.lincred < 1000 and (g.cuota <> 0 or g.saldo <> 0))) order by a.lincred";
            }

            this.OdbcConnect.ExecuteQueryDataset(Sql, myconnect, "obligacionesCausacion", ref datos, "obligacion");
        }

        public void grabaReEstructurados(string codigoter, int Lincred, double numero, string Idcodigoter, int IdLincred, double Idnumero, string Categoria, DateTime Fecha, double Valor,
            string Usuario, DateTime Fecsys, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            double veces = 0;

            veces = BuscaVecesReEstructurados(codigoter, Lincred, numero, myconnect);
            veces = veces + 1;

            ok = BuscaReEstructurados(codigoter, Lincred, numero, myconnect);

            if (!ok)
            {
                Stbuilder.Append("insert into cop_maerest (codigoter,lincred,numero,IdCodigoter,Idlincred,Idnumero,categoria,fecha,Valor,usuario,fechasys,veces) values ('");
                Stbuilder.Append(codigoter + "','");
                Stbuilder.Append(Lincred + "','");
                Stbuilder.Append(numero + "','");
                Stbuilder.Append(Idcodigoter + "','");
                Stbuilder.Append(IdLincred + "','");
                Stbuilder.Append(Idnumero + "','");
                Stbuilder.Append(Categoria + "','");
                Stbuilder.Append(Fecha.ToString(varini.PstForFec) + "','");
                Stbuilder.Append(Valor + "','");
                Stbuilder.Append(Usuario + "','");
                Stbuilder.Append(Fecsys.ToString(varini.pstForfecyHora) + "','");
                Stbuilder.Append(veces + "')");
            }
            else
            {
                Stbuilder.Append("update cop_maerest set ");
                Stbuilder.Append("categoria = '");
                Stbuilder.Append(Categoria + "',");
                Stbuilder.Append("fecha = '");
                Stbuilder.Append(Fecha.ToString(varini.PstForFec) + "',");
                Stbuilder.Append("Valor = '");
                Stbuilder.Append(Valor + "',");
                Stbuilder.Append("veces = '");
                Stbuilder.Append(veces + "' ");
                Stbuilder.Append("where codigoter = '" + codigoter + "' and lincred = '" + Lincred + "' and numero = '" + numero + "'");
            }
            ok = this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "grabaReEstructurados");
        }

        public bool BuscaReEstructurados(string codigoter, int Lincred, double numero, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsData = new DataSet();

            try
            {
                if (DsDataset != null) DsDataset.Tables.Remove("Tblmaerest");
            }
            catch (Exception) { }

            Stbuilder.Append("select IdCodigoter, Idlincred, Idnumero ,categoria, fecha,valor,veces ");
            Stbuilder.Append("from cop_maerest ");
            Stbuilder.Append("where codigoter ='" + codigoter + "' and lincred = '" + Lincred + "' and numero = '" + numero + "'");
            ok = this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "BuscaReEstructurados", ref DsData, "Tblmaerest");
            if (ok)
            {
                try
                {
                    if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["Tblmaerest"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public double BuscaVecesReEstructurados(string codigoter, int Lincred, double numero, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            double Veces = 0;

            Stbuilder.Append("select max(veces) as campo1 ");
            Stbuilder.Append("from cop_maerest ");
            Stbuilder.Append("where idCodigoter ='" + codigoter + "' and idlincred = '" + Lincred + "' and idnumero = '" + numero + "'");

            // ok = this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "BuscaVecesReEstructurados", ref Veces); // ERROR: CS1503

            return Veces;
        }

        public double BuscaMaecar(string codigoter, int lincred, double Numcredito, DateTime FechaCorte, OdbcConnection myconnect, ref DateTime Fecven)
        {
            string Fecvence = " ";
            int DiaMes = 0;
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("select min(fecha_movto) as campo1 from cop_copmora copmora inner join cop_cuopen coppen ");
            StBuilder.Append("on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and coppen.numero = copmora.numero and copmora.periodo_causa = coppen.periodo_causa");
            StBuilder.Append(" where copmora.codigoter = '" + codigoter + "' and copmora.lincred =  " + lincred + " and copmora.numero = " + Numcredito);
            StBuilder.Append(" and copmora.periodo_contable = '" + FechaCorte.ToString("yyyyMM") + "' and (copmora.saldoCapital > 0 or copmora.saldoextra > 0 or copmora.saldoInteres > 0 or copmora.saldomora > 0)  ");
            StBuilder.Append(" group by copmora.codigoter, copmora.lincred, copmora.numero");

            ok = this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "BuscaIntMes", ref Fecvence);
            if (!ok)
            {
                Fecven = new DateTime(1950, 1, 1);
                return 0;
            }

            // DiaMes = CalculaDias(FechaCorte, Fecvence); // ERROR: CS1503
            return DiaMes;
        }

        public double BuscaMaecar(string codigoter, int lincred, double Numcredito, DateTime FechaCorte, OdbcConnection myconnect)
        {
            DateTime fecven = new DateTime(1950, 1, 1);
            return BuscaMaecar(codigoter, lincred, Numcredito, FechaCorte, myconnect, ref fecven);
        }

        public void GrabaMaecar(string codigoter, int Lincred, double Numero, string Reest, DateTime fecrest, string calrest, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            ok = this.BuscaObligacion(codigoter, Lincred, Numero, myconnect);
            if (ok)
            {
                StBuilder.Append("Update cop_maecar set reest = '");
                StBuilder.Append(Reest + "',");
                StBuilder.Append("fecrest='");
                StBuilder.Append(fecrest.ToString(varini.PstForFec) + "',");
                StBuilder.Append("calrest='");
                StBuilder.Append(calrest + "' ");
                StBuilder.Append("where codigoter = '" + codigoter + "' and lincred = '" + Lincred + "' and numero = '" + Numero + "'");
            }
            this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaMaecar");
        }

        public bool ActualizarCuaotaSalmaecar(string codigoter, string lincred, double ConseCredito, int periodo, OdbcConnection Myconnect)
        {
            double cuota = 0;
            decimal tasaint = 0;
            string ciclod = "0", clades = "0", periodd = "0";
            ok = Busca_Cuota_copmaecar(codigoter, lincred, ConseCredito, Myconnect, ref cuota, ref tasaint, ref ciclod, ref clades, ref periodd);
            if (ok)
            {
                if (GrabaCuaotaSalmaecar(codigoter, lincred, ConseCredito, periodo, Myconnect, TipoActualizacion.Todos, ref cuota, ref tasaint, ref ciclod, ref clades, ref periodd))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public bool BuscaCuaotaSalmaecar(string codigoter, string lincred, double ConseCredito, int periodo, OdbcConnection Myconnect, ref double cuota, ref decimal tasaint, ref string ciclod, ref string clades, ref string periodd)
        {
            string stmysqlLocal;
            bool okLocal;
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysqlLocal = "select CUOTA as campo1, TASAINT as campo2,CICLOD as campo3,PERIODD as campo4 from cop_salmaecar where codigoter = '" + codigoter + "'"
                  + " and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo = " + periodo;
            string _cuota = cuota.ToString(), _tasaint = tasaint.ToString(), _ciclod = ciclod, _periodd = periodd;
            okLocal = this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, Myconnect, "BuscaCuaotaSalmaecar", ref _cuota, ref _tasaint, ref _ciclod, ref _periodd);
            double.TryParse(_cuota, out cuota); decimal.TryParse(_tasaint, out tasaint); ciclod = _ciclod; periodd = _periodd;

            stmysqlLocal = "select CLADES as campo1  from cop_salmaecar where codigoter = '" + codigoter + "'"
                       + " and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo = " + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, Myconnect, "BuscaCuaotaSalmaecar", ref clades);
            return okLocal;
        }

        public bool Busca_Cuota_copmaecar(string codigoter, string lincred, double ConseCredito, OdbcConnection Myconnect, ref double cuota, ref decimal tasaint, ref string ciclod, ref string clades, ref string periodd)
        {
            string stmysqlLocal;
            bool okLocal;
            string sttasaint = "0", stclades = "0", stciclod = "0", stcuota = "0", stperiodd = "0";

            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysqlLocal = "select CUOTA as campo1, TASAINT as campo2,CICLOD as campo3,PERIODD as campo4 from cop_maecar where codigoter = '" + codigoter + "'"
                  + " and lincred = " + lincred + " and numero = " + ConseCredito;
            okLocal = this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, Myconnect, "BuscaCuaota_copmaecar", ref stcuota, ref sttasaint, ref stciclod, ref stperiodd);
            stmysqlLocal = " select CLADES as campo1  from cop_maecar where codigoter = '" + codigoter + "'"
                 + " and lincred = " + lincred + " and numero = " + ConseCredito;
            this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, Myconnect, "BuscaCuaota_copmaecar", ref stclades);

            cuota = Information.IsNumeric(stcuota) ? Convert.ToDouble(stcuota) : 0;
            tasaint = Information.IsNumeric(sttasaint) ? Convert.ToDecimal(sttasaint) : 0;
            ciclod = Information.IsNumeric(stciclod) ? stciclod : "0";
            periodd = Information.IsNumeric(stperiodd) ? stperiodd : "0";
            clades = Information.IsNumeric(stclades) ? stclades : "0";

            return okLocal;
        }

        public bool GrabaCuaotaSalmaecar(string codigoter, string lincred, double ConseCredito, int periodo, OdbcConnection Myconnect, TipoActualizacion tipoActualizacion, ref double cuota, ref decimal tasaint, ref string ciclod, ref string clades, ref string periodd)
        {
            // ok = BuscaSaldoObligacion(codigoter, lincred, ConseCredito, periodo, Myconnect); // ERROR: CS1501
            if (ok)
            {
                switch (tipoActualizacion)
                {
                    case TipoActualizacion.Todos:
                        stmysql = " update cop_salmaecar set CUOTA= " + cuota + ","
                                   + " TASAINT=" + tasaint + ",ciclod='" + ciclod + "',PERIODD ='" + periodd + "',CLADES ='" + clades + "'"
                                   + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + ConseCredito + " and PERIODO = " + periodo;
                        break;
                    case TipoActualizacion.Cuota:
                        stmysql = " update cop_salmaecar set CUOTA= " + cuota + ","
                               + "ciclod='" + ciclod + "',PERIODD ='" + periodd + "',CLADES ='" + clades + "'"
                               + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + ConseCredito + " and PERIODO = " + periodo;
                        break;
                }
            }
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "GrabaCuaotaSalmaecar");
            return ok;
        }

        public bool ActualizaTasaInteressalmecar(string codigoter, int lincred, double numero, double TasaInt, int periodo, OdbcConnection myconnect)
        {
            stmysql = "update cop_salmaecar set TASAINT = " + TasaInt + " where lincred = " + lincred
                  + " and numero = " + numero + " and codigoter = '" + codigoter + "'  and  periodo=" + periodo;
            return this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaTasaInteres");
        }

        public bool ModificaCuotasalmecar(string codigoter, int lincred, double numero, double Cuota, int periodo, OdbcConnection myconnect)
        {
            stmysql = "update cop_salmaecar set CUOTA = " + Cuota + " where lincred = " + lincred
                  + " and numero = " + numero + " and codigoter = '" + codigoter + "'  and  periodo=" + periodo;
            return this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaTasaInteres");
        }

        public bool ActualizaCuuotasalmaecarQuery(string Codigoter, string periodo, OdbcConnection myconnect)
        {
            // ok = msgconfig.BuscaAsociado(Codigoter, myconnect, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno); // ERROR: CS1503, CS1620
            if (ok)
            {
                switch (varini.pstTipoBD.ToUpper())
                {
                    case "DB2":
                        stmysql = " update COP_SALMAECAR salmecar "
                               + " set salmecar.CUOTA =(select maecar.CUOTA from cop_maecar maecar where maecar.codigoter = salmecar.codigoter And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                               + ", salmecar.TASAINT =(select maecar.TASAINT from cop_maecar maecar  where maecar.codigoter = salmecar.codigoter  And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                               + ", salmecar.CICLOD = (select maecar.CICLOD from cop_maecar maecar where maecar.codigoter = salmecar.codigoter  And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                               + ", salmecar.PERIODD = (select maecar.PERIODD from cop_maecar maecar where maecar.codigoter = salmecar.codigoter And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                               + ", salmecar.CLADES = (select maecar.CLADES from cop_maecar maecar where maecar.codigoter = salmecar.codigoter   And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                               + "  where salmecar.periodo = " + periodo + "  and  (salmecar.codigoter,salmecar.lincred,salmecar.numero) in (select codigoter,lincred,numero from cop_maecar  where codigoter='" + Codigoter + "')";
                        break;
                    case "SQL":
                        stmysql = " update COP_SALMAECAR set COP_SALMAECAR.CUOTA = cop_maecar.CUOTA , COP_SALMAECAR.TASAINT = cop_maecar.TASAINT"
                                  + " ,COP_SALMAECAR.CICLOD = cop_maecar.CICLOD, COP_SALMAECAR.PERIODD = cop_maecar.PERIODD , COP_SALMAECAR.CLADES = cop_maecar.CLADES"
                                  + " from COP_SALMAECAR inner join cop_maecar  "
                                  + " on   COP_SALMAECAR.codigoter = cop_maecar.codigoter "
                                  + " and  COP_SALMAECAR.LINCRED = cop_maecar.LINCRED "
                                  + " and  COP_SALMAECAR.numero = cop_maecar.NUMERO "
                                  + " where COP_SALMAECAR.PERIODO = " + periodo + " AND cop_maecar.codigoter='" + Codigoter + "'";
                        break;
                    case "ORACLE":
                        stmysql = " update COP_SALMAECAR salmecar "
                                  + " set salmecar.CUOTA =(select maecar.CUOTA from cop_maecar maecar where maecar.codigoter = salmecar.codigoter And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                  + ", salmecar.TASAINT =(select maecar.TASAINT from cop_maecar maecar  where maecar.codigoter = salmecar.codigoter  And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                  + ", salmecar.CICLOD = (select maecar.CICLOD from cop_maecar maecar where maecar.codigoter = salmecar.codigoter  And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                  + ", salmecar.PERIODD = (select maecar.PERIODD from cop_maecar maecar where maecar.codigoter = salmecar.codigoter And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                  + ", salmecar.CLADES = (select maecar.CLADES from cop_maecar maecar where maecar.codigoter = salmecar.codigoter   And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                  + "  where (salmecar.codigoter,salmecar.lincred,salmecar.numero) in (select codigoter,lincred,numero from cop_maecar  where codigoter='" + Codigoter + "')"
                                  + "  and  salmecar.periodo = " + periodo;
                        break;
                    case "MYSQL":
                        stmysql = "update COP_SALMAECAR,cop_maecar set COP_SALMAECAR.CUOTA = cop_maecar.CUOTA , COP_SALMAECAR.TASAINT = cop_maecar.TASAINT "
                        + ",COP_SALMAECAR.CICLOD = cop_maecar.CICLOD, COP_SALMAECAR.PERIODD = cop_maecar.PERIODD , COP_SALMAECAR.CLADES = cop_maecar.CLADES"
                        + " where COP_SALMAECAR.codigoter = cop_maecar.codigoter"
                        + " and   COP_SALMAECAR.LINCRED = cop_maecar.LINCRED"
                        + " and   COP_SALMAECAR.NUMERO = cop_maecar.NUMERO "
                        + " and COP_SALMAECAR.PERIODO = " + periodo + " and cop_maecar.codigoter='" + Codigoter + "' ";
                        break;
                }
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaCuuotasalmaecarQuery");
            }
            return ok;
        }

        public bool ActualizaCuuotasalmaecarQuery(string Codigoter, string periodo, OdbcConnection myconnect, int lineanueva, double numeronuevo)
        {
            // ok = msgconfig.BuscaAsociado(Codigoter, myconnect, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno); // ERROR: CS1503, CS1620
            if (ok)
            {
                switch (varini.pstTipoBD.ToUpper())
                {
                    case "DB2":
                        stmysql = " update COP_SALMAECAR salmecar "
                                   + " set salmecar.CUOTA =(select maecar.CUOTA from cop_maecar maecar where maecar.codigoter = salmecar.codigoter And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                   + ", salmecar.TASAINT =(select maecar.TASAINT from cop_maecar maecar  where maecar.codigoter = salmecar.codigoter  And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                   + ", salmecar.CICLOD = (select maecar.CICLOD from cop_maecar maecar where maecar.codigoter = salmecar.codigoter  And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                   + ", salmecar.PERIODD = (select maecar.PERIODD from cop_maecar maecar where maecar.codigoter = salmecar.codigoter And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                   + ", salmecar.CLADES = (select maecar.CLADES from cop_maecar maecar where maecar.codigoter = salmecar.codigoter   And maecar.LINCRED = salmecar.LINCRED  and  maecar.numero = salmecar.NUMERO)"
                                   + "  where (salmecar.codigoter,salmecar.lincred,salmecar.numero) in (select codigoter,lincred,numero from cop_maecar  where codigoter='" + Codigoter + "'  and lincred=" + lineanueva + " and numero=" + numeronuevo + " )"
                                   + "  and  salmecar.periodo = " + periodo;
                        break;
                    case "SQL":
                        stmysql = " update COP_SALMAECAR set COP_SALMAECAR.CUOTA = cop_maecar.CUOTA , COP_SALMAECAR.TASAINT = cop_maecar.TASAINT"
                                  + " ,COP_SALMAECAR.CICLOD = cop_maecar.CICLOD, COP_SALMAECAR.PERIODD = cop_maecar.PERIODD , COP_SALMAECAR.CLADES = cop_maecar.CLADES"
                                  + " from COP_SALMAECAR inner join cop_maecar  "
                                  + " on   COP_SALMAECAR.codigoter = cop_maecar.codigoter "
                                  + " and  COP_SALMAECAR.LINCRED = cop_maecar.LINCRED "
                                  + " and  COP_SALMAECAR.numero = cop_maecar.NUMERO "
                                  + " where COP_SALMAECAR.PERIODO = " + periodo + " and cop_maecar.codigoter='" + Codigoter + "' and cop_maecar.LINCRED=" + lineanueva + "   and  cop_maecar.numero " + numeronuevo + "";
                        break;
                    case "ORACLE":
                        stmysql = " update COP_SALMAECAR salmecar "
                                  + " set salmecar.CUOTA =(select maecar.CUOTA from cop_maecar maecar where maecar.codigoter = '" + Codigoter + "' And maecar.LINCRED = " + lineanueva + "  and  maecar.numero = " + numeronuevo + " )"
                                  + ", salmecar.TASAINT =(select maecar.TASAINT from cop_maecar maecar  where maecar.codigoter = '" + Codigoter + "' And maecar.LINCRED = " + lineanueva + "  and  maecar.numero = " + numeronuevo + " )"
                                  + ", salmecar.CICLOD = (select maecar.CICLOD from cop_maecar maecar where maecar.codigoter = '" + Codigoter + "' And maecar.LINCRED = " + lineanueva + "  and  maecar.numero = " + numeronuevo + " )"
                                  + ", salmecar.PERIODD = (select maecar.PERIODD from cop_maecar maecar where maecar.codigoter = '" + Codigoter + "' And maecar.LINCRED = " + lineanueva + "  and  maecar.numero = " + numeronuevo + " )"
                                  + ", salmecar.CLADES = (select maecar.CLADES from cop_maecar maecar where maecar.codigoter = '" + Codigoter + "' And maecar.LINCRED =  " + lineanueva + "  and  maecar.numero = " + numeronuevo + " )"
                                  + "  where (salmecar.codigoter,salmecar.lincred,salmecar.numero) in (select codigoter,lincred,numero from cop_maecar  where codigoter='" + Codigoter + "' And LINCRED =  " + lineanueva + "  and  numero = " + numeronuevo + " )"
                                  + "  and  salmecar.periodo = " + periodo;
                        break;
                    case "MYSQL":
                        stmysql = "update COP_SALMAECAR,cop_maecar set COP_SALMAECAR.CUOTA = cop_maecar.CUOTA , COP_SALMAECAR.TASAINT = cop_maecar.TASAINT "
                                   + ",COP_SALMAECAR.CICLOD = cop_maecar.CICLOD, COP_SALMAECAR.PERIODD = cop_maecar.PERIODD , COP_SALMAECAR.CLADES = cop_maecar.CLADES"
                                   + " where COP_SALMAECAR.codigoter = cop_maecar.codigoter"
                                   + " and   COP_SALMAECAR.LINCRED = cop_maecar.LINCRED"
                                   + " and   COP_SALMAECAR.NUMERO = cop_maecar.NUMERO "
                                   + " and COP_SALMAECAR.PERIODO = " + periodo + " and cop_maecar.codigoter='" + Codigoter + "' and cop_maecar.LINCRED=" + lineanueva + "   and  cop_maecar.numero " + numeronuevo + "";
                        break;
                }
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaCuuotasalmaecarQuery");
            }
            return ok;
        }

        public void EliminaCuotaAnticipadaSolicitudCredito(string NumSolicitud, string IdCptoAnticipada, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            Stbuilder.Append("delete from cop_solrecr where numero = '" + NumSolicitud + "' and lincred='" + IdCptoAnticipada + "'");
            this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "EliminaCuotaAnticipadaSolicitudCredito");
        }

        public bool cargaServiPazsalvo(OdbcConnection myconnect, DataSet DsDataset = null)
        {
            DataSet DsData = new DataSet();
            try
            {
                if (DsDataset != null) DsDataset.Tables.Remove("tblRangoScoring");
            }
            catch (Exception) { }

            stmysql = "Select LINCRED,Servicant_pazsalvo from cop_concar12 where Servicant_pazsalvo>0 ";
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "cargaServiPazsalvo", ref DsData, "tblServPazsalvo");

            if (DsData.Tables["tblServPazsalvo"].Rows.Count > 0)
            {
                try
                {
                    if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tblServPazsalvo"].Copy());
                }
                catch (Exception) { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool GrabarServiPazsalvo(OdbcConnection myconnect, DataSet DsDataset)
        {
            int i = 0;
            for (i = 0; i <= DsDataset.Tables["tblServPazsalvo"].Rows.Count - 1; i++)
            {
                DataRow dr = DsDataset.Tables["tblServPazsalvo"].Rows[i];
                stmysql = " update cop_concar12 set Servicant_pazsalvo=" + dr["Servicant_pazsalvo"] + " where LINCRED=" + dr["LINCRED"];
                ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarServiPazsalvo");
            }
            return ok;
        }

        public bool EliminarServiPazsalvo(OdbcConnection myconnect, int lincred)
        {
            stmysql = " update cop_concar12 set Servicant_pazsalvo=0 where LINCRED=" + lincred;
            ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarRangoscoring");
            return ok;
        }

        public bool Busca_RedistAportes(int Periodo, string Codigoter, OdbcConnection Myconnect)
        {
            stmysql = "select PeriodoCorte,Codigoter from cop_redaportes  where PeriodoCorte='" + Periodo + "' and Codigoter='" + Codigoter + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "Busca_RedistAportes");
            return ok;
        }

        private void ReviveFecCausacionAhorro(string Codigoter, int Lincred, double numahorro, DateTime FecCausacion, int lineaAhorro, OdbcConnection myconnect, string estado = "")
        {
            StringBuilder stbuilder = new StringBuilder();
            string StFecha = "01-01-1900";
            int Sw1 = 0;
            string mysql = "";
            stbuilder.Append("select max(feccausacdtas) as campo1 from cop_movimto ");
            stbuilder.Append(" where codigoter='" + Codigoter + "' and lincred=" + Lincred + " and numero  = " + numahorro);
            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "ReviveFecCausacionCdats", ref StFecha);

            if (Information.IsDate(StFecha))
            {
                if (Convert.ToDateTime(StFecha) != new DateTime(1900, 1, 1) && Convert.ToDateTime(StFecha).ToString("dd-MM-yyyy").CompareTo(FecCausacion.ToString("dd-MM-yyyy")) > 0)
                {
                    Sw1 = 1;
                }
            }

            if (Sw1 == 0)
            {
                stbuilder = new StringBuilder();
                stbuilder.Append("  Update cop_maecar set FECCIERRE = '" + FecCausacion.ToString(varini.PstForFec) + "' ");
                stbuilder.Append("  where codigoter='" + Codigoter + "' and  LINCRED=" + lineaAhorro + " and numero  = " + numahorro);
                this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "ReviveFecCausacionAhorro");

                mysql = " update dep_maeahor set estado='" + estado + "'  where  codigoter='" + Codigoter + "'  and LINCRED=" + lineaAhorro + "  and num_cuenta=" + numahorro;
                this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "ReviveFecCausacionAhorro1");
            }
        }

        public bool BuscaCuotaCausada(string codigoter, string lincred, double numero, double PeriodoCausa, OdbcConnection Myconnect)
        {
            string stmysqlLocal;
            stmysqlLocal = "select * from cop_copmora where codigoter='" + codigoter + "' and lincred='" + lincred
                 + "' and  numero =" + numero + " and periodo_causa='" + PeriodoCausa + "'";
            bool Ok = this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, Myconnect, "BuscaCuotaCausada");
            return Ok;
        }

    } // end partial class Clscartera
} // end namespace msgcop
