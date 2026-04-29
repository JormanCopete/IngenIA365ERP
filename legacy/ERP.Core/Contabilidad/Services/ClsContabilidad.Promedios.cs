// _chunk_f1.cs — VB 6235-7082: CalculaPromedios … GeneraAportes
using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Windows.Forms;

namespace ERP.Core.Contabilidad.Services
{
    public partial class ClsContabilidad
    {
        // VB 6235
        public void CalculaPromedios(DateTime Fecini, DateTime FecFin, Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Calcula Promedios", myforma);
            msgbarra.Show();

            EliminaPromedios(int.Parse(Fecini.ToString("yyyyMM")), int.Parse(FecFin.ToString("yyyyMM")), myconnect);
            CalculaPromedioDisponible(Fecini, FecFin, msgbarra, myforma, myconnect);
            CalculaPromedioDepositos(Fecini, FecFin, msgbarra, myforma, myconnect);
            CalculaCartera(Fecini, FecFin, msgbarra, myforma, myconnect);
            CalculaPromedioAportes(Fecini, FecFin, msgbarra, myforma, myconnect);
            CalculaAhorContractual(Fecini, FecFin, msgbarra, myforma, myconnect);
            CalculaAhorPermanente(Fecini, FecFin, msgbarra, myforma, myconnect);
            CalculaPromedioMensual(int.Parse(Fecini.ToString("yyyyMM")), int.Parse(FecFin.ToString("yyyyMM")), msgbarra, myforma, myconnect);
            msgbarra.Close();
            msgbarra.Dispose();
        }

        // VB 6253
        private void EliminaPromedios(int PeriodoInicial, int PeriodoFinal, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("delete from cnt_riesgo where periodo between '" + PeriodoInicial + "' and '" + PeriodoFinal + "'");
            conect.ExecuteQueryconec(sb.ToString(), myconnect, "EliminaPromedios");
        }

        // VB 6261 — returns int[] with 1-based indices [1..12]
        private int[] Ordenbandas(DateTime fecini, DateTime Fecfin)
        {
            int[] bandas = new int[13];
            bandas[1] = fecini.Month;
            for (int Banda = 2; Banda <= 12; Banda++)
                bandas[Banda] = int.Parse(fecini.AddMonths(Banda - 1).ToString("MM"));
            return bandas;
        }

        // VB 6271
        private void CalculaPromedioDisponible(DateTime Fecini, DateTime FecFin, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            double SaldoInicial = 0;

            sb.Append("select cuenta,fecha,sum(vlr_debito) Debito,sum(vlr_credito) Credito ");
            sb.Append("from cnt_movimto ");
            sb.Append("where cuenta like '11%' and fecha between '" + Fecini.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "'");
            sb.Append("group by cuenta,fecha");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaPromedioDisponible", ref dsDataset, "tbldispo");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["tbldispo"].Rows.Count, "Calcula Promedio Disponible");

            while (Fila < dsDataset.Tables["tbldispo"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tbldispo"].Rows[Fila];
                SaldoInicial = BuscarSaldoCuenta(
                    row["cuenta"].ToString(),
                    Convert.ToDateTime(row["fecha"]).AddMonths(-1).ToString("yyyyMM"),
                    "9999", "99999999", myconnect);

                if (row["cuenta"].ToString().Substring(0, 4) != "1120")
                    GrabaRiesgo(row["cuenta"].ToString(), Convert.ToDateTime(row["fecha"]), SaldoInicial,
                        Convert.ToDouble(row["debito"]), Convert.ToDouble(row["Credito"]), myconnect);

                msgbarra.PerformStep();
                Fila++;
            }
            // BuscaCuentaSinMovtoRiesgo("11", Fecini, FecFin, myforma, myconnect); // ERROR: CS0103
        }

        // VB 6302
        private void CalculaPromedioDepositos(DateTime Fecini, DateTime FecFin, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            double SaldoInicial = 0;

            sb.Append("select cuenta,fecha,sum(vlr_debito) Debito,sum(vlr_credito) Credito ");
            sb.Append("from cnt_movimto ");
            sb.Append("where cuenta like '2105%' and fecha between '" + Fecini.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "'");
            sb.Append("group by cuenta,fecha");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaPromedioDepositos", ref dsDataset, "tbldepositos");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["tbldepositos"].Rows.Count, "Calcula Promedio Depositos");

            while (Fila < dsDataset.Tables["tbldepositos"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tbldepositos"].Rows[Fila];
                SaldoInicial = BuscarSaldoCuenta(
                    row["cuenta"].ToString(),
                    Convert.ToDateTime(row["fecha"]).AddMonths(-1).ToString("yyyyMM"),
                    "9999", "99999999", myconnect) * -1;

                GrabaRiesgo(row["cuenta"].ToString(), Convert.ToDateTime(row["fecha"]), SaldoInicial,
                    Convert.ToDouble(row["Credito"]), Convert.ToDouble(row["debito"]), myconnect);

                msgbarra.PerformStep();
                Fila++;
            }
            // BuscaCuentaSinMovtoRiesgo("2105", Fecini, FecFin, myforma, myconnect); // ERROR: CS0103
        }

        // VB 6330
        private void CalculaPromedioAportes(DateTime Fecini, DateTime FecFin, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            double SaldoInicial = 0;

            sb.Append("select cntmov.cuenta,cntmov.fecha,sum(cntmov.vlr_debito) Debito,sum(cntmov.vlr_credito) Credito ");
            sb.Append("from cnt_movimto cntmov inner join sys_compro02 par02 on cntmov.compronte = par02.codigo ");
            sb.Append("where cntmov.cuenta like '3105%' and cntmov.fecha between '" + Fecini.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "' ");
            sb.Append("and par02.documento <> 'SI' ");
            sb.Append("group by cntmov.cuenta,cntmov.fecha");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaPromedioAportes", ref dsDataset, "tblaportes");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["tblaportes"].Rows.Count, "Calcula Promedio Aportes");

            while (Fila < dsDataset.Tables["tblaportes"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tblaportes"].Rows[Fila];
                SaldoInicial = BuscarSaldoCuenta(
                    row["cuenta"].ToString(),
                    Convert.ToDateTime(row["fecha"]).AddMonths(-1).ToString("yyyyMM"),
                    "9999", "99999999", myconnect);

                GrabaRiesgo(row["cuenta"].ToString(), Convert.ToDateTime(row["fecha"]), SaldoInicial,
                    Convert.ToDouble(row["debito"]), Convert.ToDouble(row["Credito"]), myconnect);

                msgbarra.PerformStep();
                Fila++;
            }
            // BuscaCuentaSinMovtoRiesgo("3105", Fecini, FecFin, myforma, myconnect); // ERROR: CS0103
        }

        // VB 6359
        private void CalculaAhorContractual(DateTime Fecini, DateTime FecFin, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            double SaldoInicial = 0;

            sb.Append("select cntmov.cuenta,cntmov.fecha,sum(cntmov.vlr_debito) Debito,sum(cntmov.vlr_credito) Credito ");
            sb.Append("from cnt_movimto cntmov inner join sys_compro02 par02 on cntmov.compronte = par02.codigo ");
            sb.Append("where cntmov.cuenta like '2125%' and cntmov.fecha between '" + Fecini.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "' ");
            sb.Append("and par02.documento <> 'SI' ");
            sb.Append("group by cntmov.cuenta,cntmov.fecha");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaAhorContractual", ref dsDataset, "tbldepCont");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["tbldepCont"].Rows.Count, "Calcula Promedio Ahorro Contractual");

            while (Fila < dsDataset.Tables["tbldepCont"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tbldepCont"].Rows[Fila];
                SaldoInicial = BuscarSaldoCuenta(
                    row["cuenta"].ToString(),
                    Convert.ToDateTime(row["fecha"]).AddMonths(-1).ToString("yyyyMM"),
                    "9999", "99999999", myconnect);

                GrabaRiesgo(row["cuenta"].ToString(), Convert.ToDateTime(row["fecha"]), SaldoInicial,
                    Convert.ToDouble(row["debito"]), Convert.ToDouble(row["Credito"]), myconnect);

                msgbarra.PerformStep();
                Fila++;
            }
            // BuscaCuentaSinMovtoRiesgo("2125", Fecini, FecFin, myforma, myconnect); // ERROR: CS0103
        }

        // VB 6389
        private void CalculaAhorPermanente(DateTime Fecini, DateTime FecFin, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            double SaldoInicial = 0;

            sb.Append("select cntmov.cuenta,cntmov.fecha,sum(cntmov.vlr_debito) Debito,sum(cntmov.vlr_credito) Credito ");
            sb.Append("from cnt_movimto cntmov inner join sys_compro02 par02 on cntmov.compronte = par02.codigo ");
            sb.Append("where cntmov.cuenta like '2130%' and cntmov.fecha between '" + Fecini.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "' ");
            sb.Append("and par02.documento <> 'SI' ");
            sb.Append("group by cntmov.cuenta,cntmov.fecha");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaAhorContractual", ref dsDataset, "tbldepCont");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["tbldepCont"].Rows.Count, "Calcula Promedio Ahorro Permanente");

            while (Fila < dsDataset.Tables["tbldepCont"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tbldepCont"].Rows[Fila];
                SaldoInicial = BuscarSaldoCuenta(
                    row["cuenta"].ToString(),
                    Convert.ToDateTime(row["fecha"]).AddMonths(-1).ToString("yyyyMM"),
                    "9999", "99999999", myconnect);

                GrabaRiesgo(row["cuenta"].ToString(), Convert.ToDateTime(row["fecha"]), SaldoInicial,
                    Convert.ToDouble(row["debito"]), Convert.ToDouble(row["Credito"]), myconnect);

                msgbarra.PerformStep();
                Fila++;
            }
            // BuscaCuentaSinMovtoRiesgo("2130", Fecini, FecFin, myforma, myconnect); // ERROR: CS0103
        }

        // VB 6421 — dead code (columns "cuenta","fecha" don't exist in the query), preserved as-is
        private void CalculaPromedioCdat(DateTime Fecini, DateTime FecFin, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            double SaldoInicial = 0;

            sb.Append("select cdats.codigoter,cdats.lincred,cdats.num_cdat,salmae.saldo,FecVence,par58.poranual ");
            sb.Append("from  cdt_maecdats  cdats ");
            sb.Append("inner join cop_salmaecar salmae on ");
            sb.Append("cdats.codigoter = salmae.codigoter and cdats.lincred = salmae.lincred and ");
            sb.Append("cdats.num_cdat = salmae.numero and salmae.periodo = '" + FecFin.ToString("yyyyMM") + "' ");
            sb.Append("inner join cdt_parame58 par58 on cdats.lincred = par58.lincred ");
            sb.Append("inner join cop_concar12 par12 on  cdats.lincred = par12.lincred ");
            sb.Append("where(salmae.saldo <> 0)");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaPromedioCdat", ref dsDataset, "tblCdats");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["tblCdats"].Rows.Count, "Calcula Promedio Cdats");

            while (Fila < dsDataset.Tables["tblCdats"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tblCdats"].Rows[Fila];
                SaldoInicial = BuscarSaldoCuenta(
                    row["cuenta"].ToString(),
                    Convert.ToDateTime(row["fecha"]).AddMonths(-1).ToString("yyyyMM"),
                    "9999", "99999999", myconnect) * -1;
                GrabaRiesgo(row["cuenta"].ToString(), Convert.ToDateTime(row["fecha"]), SaldoInicial,
                    Convert.ToDouble(row["debito"]), Convert.ToDouble(row["Credito"]), myconnect);

                msgbarra.PerformStep();
                Fila++;
            }
        }

        // VB 6452
        private void CalculaCartera(DateTime Fecini, DateTime FecFin, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            DataTable dsDataDed = new DataTable();
            // var liqcre = new msgliqcre.ClsLiqcreditos(); // ERROR: CS0246
            string ststring = " ";
            int StInteger = 0;
            double StDouble = 0;
            double Valmenos = 0;
            int Fila = 0;
            double SaldoInicial = 0;
            string CicloDsto = " ";
            int Rows = 0;
            decimal TasaInt = 0;
            DataSet DsDataProye = new DataSet();
            int DiaIni = 0;
            int NumCuota = 0;
            DateTime NextPeriodo = FecFin;
            double Interes = 0, Capital = 0, Seguro = 0, admon = 0;

            dsDataDed.TableName = "tbldeducciones";
            dsDataDed.Columns.Add("CEDULA", ststring.GetType());
            dsDataDed.Columns.Add("LINCRED", StInteger.GetType());
            dsDataDed.Columns.Add("NUMERO", StDouble.GetType());
            dsDataDed.Columns.Add("DESCRIPCION", ststring.GetType());
            dsDataDed.Columns.Add("VALOR", StDouble.GetType());
            dsDataDed.Columns.Add("INTERES", StDouble.GetType());
            dsDataDed.Columns.Add("TOTAL", StDouble.GetType());

            EliminaCopriesgo(myconnect);

            sb.Append("Select copmae.codigoter, copmae.lincred, copmae.numero,salmae.cuota,copmae.clacuo,salmae.tasaint,salmae.periodd,");
            sb.Append("copmae.Clasei, salmae.Clades, copmae.tasaadm, copmae.plazo,salmae.ciclod,tasaseg,copmae.fecfact, copclas.saldot,copclas.clasec, copmae.PERGRADIA, copmae.PERGRAINI,copmae.valorob,concar.valsegMin,concar.valsegMax   ");
            sb.Append("from cop_maecar copmae inner join cop_copclas copclas ");
            sb.Append("on copmae.codigoter = copclas.codigoter and copmae.lincred = copclas.lincred and ");
            sb.Append("copmae.numero = copclas.numero And copclas.periodo_contable = '" + FecFin.ToString("yyyyMM") + "' ");
            sb.Append(" inner join cop_salmaecar salmae on copmae.codigoter=salmae.codigoter  and copmae.lincred = salmae.lincred  and copmae.numero=salmae.numero  and salmae.periodo=" + FecFin.ToString("yyyyMM"));
            sb.Append(" inner join cop_concar12 concar on copmae.lincred=concar.lincred ");
            sb.Append(" where copclas.catego = 'A' ");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaCartera", ref dsDataset, "TblCartera");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["TblCartera"].Rows.Count, "Calcula Promedio Cartera");

            while (Fila < dsDataset.Tables["TblCartera"].Rows.Count)
            {
                DataRow carteraRow = dsDataset.Tables["TblCartera"].Rows[Fila];
                Rows = 0;
                TasaInt = Math.Round(
                    (Convert.ToDecimal(carteraRow["tasaint"]) / Convert.ToDecimal(carteraRow["periodd"])) / 100m, 8);
                NextPeriodo = FecFin;
                NumCuota = 0; Capital = 0; Interes = 0;

                if (carteraRow["periodd"].ToString() != "1")
                {
                    if (carteraRow["ciclod"].ToString() == "1")
                    {
                        if (carteraRow["periodd"].ToString() == "2")
                            DiaIni = 15;
                        else if (carteraRow["periodd"].ToString() == "3")
                            DiaIni = 10;
                    }
                    if (carteraRow["ciclod"].ToString() == "2")
                    {
                        if (carteraRow["periodd"].ToString() == "2")
                            DiaIni = 30;
                        else if (carteraRow["periodd"].ToString() == "3")
                            DiaIni = 20;
                    }
                    if (carteraRow["ciclod"].ToString() == "3")
                    {
                        switch (carteraRow["periodd"].ToString())
                        {
                            case "3": DiaIni = 30; break;
                            case "4": DiaIni = 21; break;
                        }
                    }
                    if (carteraRow["ciclod"].ToString() == "4")
                    {
                        switch (carteraRow["periodd"].ToString())
                        {
                            case "4": DiaIni = 30; break;
                        }
                    }
                    if (carteraRow["ciclod"].ToString() == "5")
                        DiaIni = 1;
                }
                else
                {
                    DiaIni = 1;
                }

                if (FecFin.AddMonths(1).Month == 2)
                {
                    if (DiaIni > 28)
                        DiaIni = 28;
                }

                if (carteraRow["PERGRAINI"] is DBNull)
                    carteraRow["PERGRAINI"] = "0";
                else if (!Microsoft.VisualBasic.Information.IsNumeric(carteraRow["PERGRAINI"]))
                    carteraRow["PERGRAINI"] = "0";

                // liqcre.BuscarExtras( // ERROR: CS0103
                    // carteraRow["codigoter"].ToString(), // ERROR: CS0103
                    // Convert.ToInt32(carteraRow["lincred"]), // ERROR: CS0103
                    // Convert.ToDouble(carteraRow["numero"]), // ERROR: CS0103
                    // FecFin.ToString("yyyyMM"), // ERROR: CS0103
                    // ref dsDataset, // ERROR: CS0103
                    // myconnect); // ERROR: CS0103

                // CicloDsto = liqcre.CalculaCiclo( // ERROR: CS0246
                    // Convert.ToInt32(carteraRow["ciclod"]), // ERROR: CS0246
                    // (msgliqcre.ClsLiqcreditos.Periodicidad)Convert.ToInt32(carteraRow["periodd"]), // ERROR: CS0246
                    // DateTime.Parse(FecFin.AddMonths(1).ToString("yyyy/MM/") + DiaIni)); // ERROR: CS0246

                double _liqSegMes = 0, _liqCuoAdm = 0, _totLiqInteres = 0;
                // DsDataProye = liqcre.GeneraPlanPagos( // ERROR: CS0103
                    // carteraRow["codigoter"].ToString(), // ERROR: CS0103
                    // Convert.ToDouble(carteraRow["saldot"]), // ERROR: CS0103
                    // Convert.ToDouble(carteraRow["cuota"]), // ERROR: CS0103
                    // Convert.ToInt32(carteraRow["clacuo"]), // ERROR: CS0103
                    // Convert.ToInt32(carteraRow["clasei"]), // ERROR: CS0103
                    // TasaInt, // ERROR: CS0103
                    // Convert.ToDecimal(carteraRow["plazo"]), // ERROR: CS0103
                    // Convert.ToInt32(carteraRow["periodd"]), // ERROR: CS0103
                    // CicloDsto, // ERROR: CS0103
                    // Convert.ToDateTime(carteraRow["fecfact"]), // ERROR: CS0103
                    // Convert.ToDateTime(carteraRow["fecfact"]), // ERROR: CS0103
                    // 0, 0, 0m, "0", 0m, 0, 0, 0m, "0", "0", // ERROR: CS0103
                    // dsDataset.Tables["tblextras"], // ERROR: CS0103
                    // dsDataDed, // ERROR: CS0103
                    // ref Valmenos, // ERROR: CS0103
                    // 0, // ERROR: CS0103
                    // ref _totLiqInteres, // ERROR: CS0103
                    // ref _liqSegMes, 0, 0, 0, 0, ref _liqCuoAdm, "N", false, // ERROR: CS0103
                    // carteraRow["PERGRADIA"].ToString(), // ERROR: CS0103
                    // carteraRow["PERGRAINI"].ToString(), // ERROR: CS0103
                    // msgliqcre.ClsLiqcreditos.OpcionProyeccion.Linea, // ERROR: CS0103
                    // 0, // ERROR: CS0103
                    // Convert.ToDouble(carteraRow["valorob"]), // ERROR: CS0103
                    // Convert.ToDouble(carteraRow["valsegMin"]), // ERROR: CS0103
                    // Convert.ToDouble(carteraRow["valsegMax"])); // ERROR: CS0103

                while (Rows < DsDataProye.Tables["Tblproyeccion"].Rows.Count)
                {
                    DataRow proyRow = DsDataProye.Tables["Tblproyeccion"].Rows[Rows];

                    if (NumCuota == Convert.ToInt32(carteraRow["periodd"]))
                    {
                        NextPeriodo = NextPeriodo.AddMonths(1);
                        GrabaCopriesgo(
                            carteraRow["codigoter"].ToString(),
                            carteraRow["lincred"].ToString(),
                            carteraRow["numero"].ToString(),
                            Convert.ToInt32(proyRow["numcuota"]),
                            NextPeriodo.ToString("yyyyMM"),
                            Convert.ToDouble(proyRow["cuota"]),
                            Convert.ToDouble(proyRow["cuotaextra"]),
                            Interes, Seguro, admon, Capital,
                            Convert.ToDouble(proyRow["saldo"]),
                            Convert.ToDouble(carteraRow["saldot"]),
                            Convert.ToInt32(carteraRow["clasec"]),
                            myconnect);
                        NumCuota = 0; Capital = 0; Interes = 0; Seguro = 0; admon = 0;
                    }

                    Interes += Convert.ToDouble(proyRow["interes"]);
                    Capital += Convert.ToDouble(proyRow["abonocap"]);
                    Seguro += Convert.ToDouble(proyRow["seguro"]);
                    admon  += Convert.ToDouble(proyRow["admon"]);
                    NumCuota++;
                    Rows++;
                }

                if (Convert.ToDouble(carteraRow["saldot"]) < 0)
                {
                    Capital = Convert.ToDouble(carteraRow["saldot"]);
                    NextPeriodo = NextPeriodo.AddMonths(13);
                    GrabaCopriesgo(
                        carteraRow["codigoter"].ToString(),
                        carteraRow["lincred"].ToString(),
                        carteraRow["numero"].ToString(),
                        0, NextPeriodo.ToString("yyyyMM"),
                        0, 0, Interes, 0, 0, Capital, 0,
                        Convert.ToDouble(carteraRow["saldot"]),
                        Convert.ToInt32(carteraRow["clasec"]),
                        myconnect);
                    NumCuota = 0; Capital = 0; Interes = 0; Seguro = 0; admon = 0;
                }

                if (Interes != 0 || Capital != 0)
                {
                    NextPeriodo = NextPeriodo.AddMonths(1);
                    GrabaCopriesgo(
                        carteraRow["codigoter"].ToString(),
                        carteraRow["lincred"].ToString(),
                        carteraRow["numero"].ToString(),
                        0, NextPeriodo.ToString("yyyyMM"),
                        0, 0, Interes, 0, 0, Capital, 0,
                        Convert.ToDouble(carteraRow["saldot"]),
                        Convert.ToInt32(carteraRow["clasec"]),
                        myconnect);
                    NumCuota = 0; Capital = 0; Interes = 0; Seguro = 0; admon = 0;
                }

                msgbarra.PerformStep();
                Fila++;
            }
        }

        // VB 6602
        private void CalculaPromedioMensual(int PeriodoInicial, int PeriodoFinal, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            double promedio = 0;

            sb.Append("select riesgo.cuenta,riesgo.periodo,round(((riesgo01.saldia1 + riesgo01.saldia2 + riesgo01.saldia3 + riesgo01.saldia4 + riesgo01.saldia5 + riesgo01.saldia6 + ");
            sb.Append("riesgo01.saldia7 + riesgo01.saldia8 + riesgo01.saldia9 + riesgo01.saldia10 + riesgo01.saldia11 + riesgo01.saldia12 + riesgo01.saldia13 + riesgo01.saldia14 + ");
            sb.Append("riesgo01.saldia15 + riesgo01.saldia16 + riesgo01.saldia17 + riesgo01.saldia18 + riesgo01.saldia19 + riesgo01.saldia20 + riesgo01.saldia21 + riesgo01.saldia22 + ");
            sb.Append("riesgo01.saldia23 + riesgo01.saldia24 + riesgo01.saldia25 + riesgo01.saldia26 + riesgo01.saldia27 + riesgo01.saldia28 + riesgo01.saldia29 + riesgo01.saldia30 + ");
            sb.Append("riesgo01.saldia31)), 2) as promedio31, ");

            sb.Append("round(((riesgo01.saldia1 + riesgo01.saldia2 + riesgo01.saldia3 + riesgo01.saldia4 + riesgo01.saldia5 + riesgo01.saldia6 + ");
            sb.Append("riesgo01.saldia7 + riesgo01.saldia8 + riesgo01.saldia9 + riesgo01.saldia10 + riesgo01.saldia11 + riesgo01.saldia12 + riesgo01.saldia13 + riesgo01.saldia14 + ");
            sb.Append("riesgo01.saldia15 + riesgo01.saldia16 + riesgo01.saldia17 + riesgo01.saldia18 + riesgo01.saldia19 + riesgo01.saldia20 + riesgo01.saldia21 + riesgo01.saldia22 + ");
            sb.Append("riesgo01.saldia23 + riesgo01.saldia24 + riesgo01.saldia25 + riesgo01.saldia26 + riesgo01.saldia27 + riesgo01.saldia28 + riesgo01.saldia29 + riesgo01.saldia30)), 2) as promedio30, ");

            sb.Append("round(((riesgo01.saldia1 + riesgo01.saldia2 + riesgo01.saldia3 + riesgo01.saldia4 + riesgo01.saldia5 + riesgo01.saldia6 + ");
            sb.Append("riesgo01.saldia7 + riesgo01.saldia8 + riesgo01.saldia9 + riesgo01.saldia10 + riesgo01.saldia11 + riesgo01.saldia12 + riesgo01.saldia13 + riesgo01.saldia14 + ");
            sb.Append("riesgo01.saldia15 + riesgo01.saldia16 + riesgo01.saldia17 + riesgo01.saldia18 + riesgo01.saldia19 + riesgo01.saldia20 + riesgo01.saldia21 + riesgo01.saldia22 + ");
            sb.Append("riesgo01.saldia23 + riesgo01.saldia24 + riesgo01.saldia25 + riesgo01.saldia26 + riesgo01.saldia27 + riesgo01.saldia28 )), 2) as promedio28 ");

            sb.Append("from cnt_riesgo  riesgo ");
            sb.Append("inner join cnt_riesgo01_vw riesgo01 ");
            sb.Append("on  riesgo.cuenta = riesgo01.cuenta and riesgo.periodo = riesgo01.periodo ");
            sb.Append("where riesgo.periodo between '" + PeriodoInicial + "' and '" + PeriodoFinal + "'");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "CalculaPromedioMensual", ref dsDataset, "tblprom");

            msgbarra.ValorMinimoMaximo(0, dsDataset.Tables["tblprom"].Rows.Count, "Calculando promedio Mensual");

            while (Fila < dsDataset.Tables["tblprom"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tblprom"].Rows[Fila];
                switch (int.Parse(row["periodo"].ToString().Substring(4, 2)))
                {
                    case 1: case 3: case 5: case 7: case 8: case 10: case 12:
                        promedio = Math.Round(Convert.ToDouble(row["promedio31"]) / 31, 2);
                        break;
                    case 2:
                        promedio = Math.Round(Convert.ToDouble(row["promedio28"]) / 28, 2);
                        break;
                    case 4: case 6: case 9: case 11:
                        promedio = Math.Round(Convert.ToDouble(row["promedio30"]) / 30, 2);
                        break;
                }
                GrabaPromedio(row["cuenta"].ToString(), Convert.ToInt32(row["periodo"]), promedio, myconnect);
                msgbarra.PerformStep();
                Fila++;
            }
        }

        // VB 6653
        private void GrabaCopriesgo(string codigoter, string lincred, string Numero, int numcuota, string periodo,
            double cuota, double cuotaExt, double Interes, double Seguro, double admon,
            double AboCapital, double Saldo, double Saldot, int ClaCartera, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            bool ok = BuscaCopriesgo(codigoter, lincred, Numero, periodo, myconnect);
            if (!ok)
            {
                sb.Append("insert into cop_riesgo (");
                sb.Append("codigoter,lincred,Numero,numcuota,periodo,cuota, cuotaExt,Interes,Seguro,admon,AboCapital,Saldo,Saldot,ClaCart) ");
                sb.Append("values ('");
                sb.Append(codigoter + "','");
                sb.Append(lincred + "','");
                sb.Append(Numero + "','");
                sb.Append(numcuota + "','");
                sb.Append(periodo + "','");
                sb.Append(cuota + "','");
                sb.Append(cuotaExt + "','");
                sb.Append(Interes + "','");
                sb.Append(Seguro + "','");
                sb.Append(admon + "','");
                sb.Append(AboCapital + "','");
                sb.Append(Saldo + "','");
                sb.Append(Saldot + "','");
                sb.Append(ClaCartera + "')");
            }
            else
            {
                sb.Append("update cop_riesgo set ");
                sb.Append("cuota = cuota + '");
                sb.Append(cuota + "',");
                sb.Append("cuotaExt = cuotaExt + '");
                sb.Append(cuotaExt + "',");
                sb.Append("Interes = Interes + '");
                sb.Append(Interes + "',");
                sb.Append("Seguro = Seguro + '");
                sb.Append(Seguro + "',");
                sb.Append("admon = admon + '");
                sb.Append(admon + "',");
                sb.Append("AboCapital = AboCapital + '");
                sb.Append(AboCapital + "',");
                sb.Append("Saldo = Saldo + '");
                sb.Append(Saldo + "' ");
                sb.Append("where codigoter = '" + codigoter + "' and lincred = '" + lincred + "' and numero = '" + Numero + "' and periodo = '" + periodo + "'");
            }
            conect.ExecuteQueryconec(sb.ToString(), myconnect, "GrabaCopriesgo");
        }

        // VB 6700 — short overload (no DsDataset)
        private bool BuscaCopriesgo(string codigoter, string lincred, string Numero, string periodo, OdbcConnection myconnect)
        {
            DataSet dummy = null;
            return BuscaCopriesgo(codigoter, lincred, Numero, periodo, myconnect, dummy);
        }

        // VB 6700 — full overload
        private bool BuscaCopriesgo(string codigoter, string lincred, string Numero, string periodo, OdbcConnection myconnect, DataSet DsDataset)
        {
            var sb = new System.Text.StringBuilder();
            DataSet DsData = new DataSet();
            try { if (DsDataset != null) DsDataset.Tables.Remove("tlcopriesgo"); } catch { }
            sb.Append("Select codigoter,lincred,Numero,numcuota,periodo,cuota,cuotaExt,Interes,Seguro,admon,AboCapital,Saldo,Saldot,ClaCart ");
            sb.Append("from cop_riesgo ");
            sb.Append("where codigoter = '" + codigoter + "' and lincred = '" + lincred + "' and numero = '" + Numero + "' and periodo = '" + periodo + "'");
            bool ok = conect.ExecuteQueryDataset(sb.ToString(), myconnect, "BuscaCopriesgo", ref DsData, "tlcopriesgo");
            if (ok)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tlcopriesgo"].Copy()); } catch { }
                return true;
            }
            return false;
        }

        // VB 6726 — Overridable / all records
        public virtual bool BuscaCopriesgo(OdbcConnection myconnect, DataSet DsDataset)
        {
            var sb = new System.Text.StringBuilder();
            DataSet DsData = new DataSet();
            try { if (DsDataset != null) DsDataset.Tables.Remove("tlcopriesgo"); } catch { }
            sb.Append("Select codigoter,lincred,Numero,numcuota,periodo,cuota,cuotaExt,Interes,Seguro,admon,AboCapital,Saldo,Saldot,ClaCart ");
            sb.Append("from cop_riesgo ");
            bool ok = conect.ExecuteQueryDataset(sb.ToString(), myconnect, "BuscaCopriesgo", ref DsData, "tlcopriesgo");
            if (ok)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tlcopriesgo"].Copy()); } catch { }
                return true;
            }
            return false;
        }

        // VB 6754
        public bool EliminaCopriesgo(OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("delete from cop_riesgo ");
            bool ok = conect.ExecuteQueryconec(sb.ToString(), myconnect, "EliminaCopriesgo");
            return ok;
        }

        // VB 6763
        private void GrabaRiesgo(string cuenta, DateTime fecha, double Salini, double debito, double Credito, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            string[] StInsCampo = {
                "SalDia0","SalDia1","SalDia2","SalDia3","SalDia4","SalDia5","SalDia6","SalDia7","SalDia8","SalDia9",
                "SalDia10","SalDia11","SalDia12","SalDia13","SalDia14","SalDia15",
                "SalDia16","SalDia17","SalDia18","SalDia19","SalDia20","SalDia21","SalDia22","SalDia23",
                "SalDia24","SalDia25","SalDia26","SalDia27","SalDia28","SalDia29","SalDia30","SalDia31"
            };
            string[] StUpdcampo = {
                "SalDia0",
                "SalDia1 = SalDia1 + '","SalDia2 = SalDia2 + '","SalDia3 = SalDia3 + '","SalDia4 = SalDia4 + '",
                "SalDia5 = SalDia5 + '","SalDia6 = SalDia6 + '","SalDia7 = SalDia7 + '",
                "SalDia8 = SalDia8 + '","SalDia9 = SalDia9 + '","SalDia10 = SalDia10 + '",
                "SalDia11 = SalDia11 + '","SalDia12 = SalDia12 + '","SalDia13 = SalDia13 + '",
                "SalDia14 = SalDia14 + '","SalDia15 = SalDia15 + '",
                "SalDia16 = SalDia16 + '","SalDia17 = SalDia17 + '","SalDia18 = SalDia18 + '",
                "SalDia19 = SalDia19 + '","SalDia20 = SalDia20 + '","SalDia21 = SalDia21 + '",
                "SalDia22 = SalDia22 + '","SalDia23 = SalDia23 + '",
                "SalDia24 = SalDia24 + '","SalDia25= SalDia25 + '","SalDia26 = SalDia26 + '",
                "SalDia27 = SalDia27 + '","SalDia28 = SalDia28 + '","SalDia29 = SalDia29 + '",
                "SalDia30 = SalDia30 + '","SalDia31 = SalDia31 + '"
            };

            bool ok = BuscaRiesgo(cuenta, int.Parse(fecha.ToString("yyyyMM")), myconnect);
            if (!ok)
            {
                sb.Append("insert into cnt_riesgo (cuenta,periodo,salini," + StInsCampo[fecha.Day] + ") ");
                sb.Append("Values ('");
                sb.Append(cuenta + "','");
                sb.Append(fecha.ToString("yyyyMM") + "','");
                sb.Append(Salini + "','");
                sb.Append((debito - Credito) + "')");
            }
            else
            {
                sb.Append("update cnt_riesgo set ");
                sb.Append(StUpdcampo[fecha.Day]);
                sb.Append((debito - Credito) + "',");
                sb.Append("Salini = '");
                sb.Append(Salini + "'");
                sb.Append(" where cuenta = '" + cuenta + "' and periodo = '" + fecha.ToString("yyyyMM") + "'");
            }
            conect.ExecuteQueryconec(sb.ToString(), myconnect, "GrabaRiesgo");
        }

        // VB 6795
        private void GrabaPromedio(string cuenta, int Periodo, double Promedio, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("update cnt_riesgo set ");
            sb.Append("promedio = '");
            sb.Append(Promedio + "'");
            sb.Append(" where cuenta = '" + cuenta + "' and periodo = '" + Periodo + "'");
            conect.ExecuteQueryconec(sb.ToString(), myconnect, "GrabaPromedio");
        }

        // VB 6807 — short overload (no DsDataset)
        private bool BuscaRiesgo(string cuenta, int Periodo, OdbcConnection myconnect)
        {
            DataSet dummy = null;
            return BuscaRiesgo(cuenta, Periodo, myconnect, ref dummy);
        }

        // VB 6807 — full overload
        private bool BuscaRiesgo(string cuenta, int Periodo, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            var sb = new System.Text.StringBuilder();
            DataSet DsData = new DataSet();
            try { DsData.Tables.Remove("tblriesgo"); } catch { }

            sb.Append("select cuenta,periodo,SalDia1,SalDia2,SalDia3,SalDia4 from cnt_riesgo ");
            sb.Append("where cuenta = '" + cuenta + "' and periodo = '" + Periodo + "'");

            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "BuscaRiesgo", ref DsData, "tblriesgo");

            if (DsData.Tables["tblriesgo"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tblriesgo"].Copy()); } catch { }
                return true;
            }
            return false;
        }

        // VB 6834
        public DataTable GeneraPlanoRiesgo(string NomArchivo, DateTime Fecini, DateTime Fecfin, int DiasLiq, OdbcConnection myconnect)
        {
            return GeneraPlanoRiesgo(NomArchivo, Fecini, Fecfin, DiasLiq, myconnect, true);
        }

        public DataTable GeneraPlanoRiesgo(string NomArchivo, DateTime Fecini, DateTime Fecfin, int DiasLiq, OdbcConnection myconnect, bool csv)
        {
            DataTable DsDataRiezgo = new DataTable();
            double Stdouble = 0;
            string Ststring = " ";
            int PeriodoInicial = int.Parse(Fecini.ToString("yyyyMM"));
            int PeriodoFinal   = int.Parse(Fecfin.ToString("yyyyMM"));
            int[] ordenbandas  = new int[13];

            DsDataRiezgo.TableName = "tblriesgo";
            DsDataRiezgo.Columns.Add("UndCapt",  Ststring.GetType());
            DsDataRiezgo.Columns.Add("Renglon",  Ststring.GetType());
            DsDataRiezgo.Columns.Add("Nombre",   Ststring.GetType());
            DsDataRiezgo.Columns.Add("Saldo",    Stdouble.GetType());
            DsDataRiezgo.Columns.Add("Banda1",   Stdouble.GetType());
            DsDataRiezgo.Columns.Add("Banda2",   Stdouble.GetType());
            DsDataRiezgo.Columns.Add("Banda3",   Stdouble.GetType());
            DsDataRiezgo.Columns.Add("Banda4",   Stdouble.GetType());
            DsDataRiezgo.Columns.Add("Banda5",   Stdouble.GetType());
            DsDataRiezgo.Columns.Add("Banda6",   Stdouble.GetType());
            DsDataRiezgo.Columns.Add("Banda7",   Stdouble.GetType());

            ordenbandas = Ordenbandas(Fecini, Fecfin);

            // GeneraDisponible(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, DiasLiq, ordenbandas, myconnect); // ERROR: CS0103
            // GeneraInversiones(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            GeneraFondoLiquidez(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect);
            // GenerarOtrosRubros("1", "020", "COMPROMISOS DE REVENTA DE INVERSIONES",                   "120100000000", DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarOtrosRubros("1", "025", "COMPROMISOS DE REVENTA DE CARTERA",                       "120200000000", DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarOtrosRubros("1", "028", "INVERSIONES PARA MANTENER HASTA EL VENCIMIENTO",          "000000000000", DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarOtrosRubros("1", "035", "DERECHO DE COMPRA DE INVERSIONES",                        "000000000000", DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarOtrosRubros("1", "038", "INVERSIONES DISPONIBLES PARA LA VENTA",                   "000000000000", DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarOtrosRubros("1", "045", "INVENTARIOS",                                              "130000000000", DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraCarteraConsumo(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraCarteraComercial(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraCarteraVivienda(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraCarteraMicrocredito(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarOtrosRubros("1", "075", "CUENTAS POR COBRAR",                                      "160000000000", DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraPropiedadPlantaEquipo(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraDiferidos(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraOtrosActivos(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraContingentesDeudoras(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraTotalesPosicionesActivas(DsDataRiezgo, myconnect); // ERROR: CS0103
            GeneraDepositos(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, DiasLiq, ordenbandas, myconnect);
            // GeneraCdats(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraDepAhorroContractual(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, DiasLiq, ordenbandas, myconnect); // ERROR: CS0103
            // GeneraDepAhorroPermanente(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, DiasLiq, ordenbandas, myconnect); // ERROR: CS0103
            // GeneraRecompraInversiones(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraRecompraCartera(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraCreditoBancos(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraCtasPagar(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraImpuestos(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraFondosSociales(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraOtrosPasivos(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraPasivosEstimados(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraTitulosInversion(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraContingenteAcreedoras(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraTotalesPosicionesPasivas(DsDataRiezgo, myconnect); // ERROR: CS0103
            GeneraAportes(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, ordenbandas, myconnect);
            // GenerarReservas(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarFondos(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GenerarSuperAvit(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraExcedPerd(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            // GeneraTotalesPosicionesPatrimonio(DsDataRiezgo, myconnect); // ERROR: CS0103
            // GeneraEvalPerAnt(NomArchivo, DsDataRiezgo, PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103

            if (NomArchivo.Trim() != "")
                GrabaPlanoRiesgo(NomArchivo, DsDataRiezgo, csv);

            return DsDataRiezgo;
        }

        // VB 6904
        private void GrabaPlanoRiesgo(string Archivo, DataTable DaTaTable, bool csv = true)
        {
            int fila = 0;
            StreamWriter strStream = new StreamWriter(Archivo, false);
            strStream.Close();
            if (DaTaTable.Rows.Count > 0)
            {
                while (fila < DaTaTable.Rows.Count)
                {
                    DataRow row = DaTaTable.Rows[fila];
                    PlanoRiesgo(Archivo,
                        Convert.ToInt32(row["UndCapt"]),
                        row["Renglon"].ToString(),
                        row["Nombre"].ToString(),
                        Convert.ToDouble(row["Saldo"]),
                        Convert.ToDouble(row["Banda1"]),
                        Convert.ToDouble(row["Banda2"]),
                        Convert.ToDouble(row["Banda3"]),
                        Convert.ToDouble(row["Banda4"]),
                        Convert.ToDouble(row["Banda5"]),
                        Convert.ToDouble(row["Banda6"]),
                        Convert.ToDouble(row["Banda7"]));
                    fila++;
                }
            }
            MessageBox.Show("Plano Generado  " + Archivo);
        }

        // VB 6920
        private void PlanoRiesgo(string Archivo, int UndCaptura, string renglon, string Descripcion,
            double Saldo, double banda1, double banda2, double banda3, double banda4,
            double banda5, double banda6, double banda7, bool csv = true)
        {
            string separador;
            if (csv)
            {
                separador = ";";
            }
            else
            {
                Microsoft.Win32.RegistryKey Rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Control Panel\\International", true);
                separador = (string)Rk.GetValue("sList");
            }

            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(UndCaptura + separador);
                StrDisp.Write(renglon + separador);
                StrDisp.Write(Descripcion + separador);
                StrDisp.Write(Saldo + separador);
                StrDisp.Write(banda1 + separador);
                StrDisp.Write(banda2 + separador);
                StrDisp.Write(banda3 + separador);
                StrDisp.Write(banda4 + separador);
                StrDisp.Write(banda5 + separador);
                StrDisp.Write(banda6 + separador);
                StrDisp.Write(banda7);
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        // VB 6949
        private void GeneraFondoLiquidez(string NomArchivo, DataTable DsdataRiesgo, int PeriodoInicial, int PeriodoFinal, OdbcConnection myconnect)
        {
            double SaldoFondo     = BuscarSaldoCuenta("120300000000", PeriodoFinal.ToString(), "9999", "99999999", myconnect);
            double SaldoLiquidez  = BuscarSaldoCuenta("112000000000", PeriodoFinal.ToString(), "9999", "99999999", myconnect);
            double Saldo = SaldoFondo + SaldoLiquidez;
            // GrabaDsDataRiesgo(DsdataRiesgo, 1, "015", "Fondo Liquidez", Saldo, 0, 0, 0, 0, 0, 0, Saldo); // ERROR: CS0103
        }

        // VB 6961
        private void GeneraDepositos(string NomArchivo, DataTable DsdataRiesgo, int PeriodoInicial, int PeriodoFinal, int DiasLiq, int[] Ordenbandas, OdbcConnection myconnect)
        {
            var sb      = new System.Text.StringBuilder();
            var stbuil  = new System.Text.StringBuilder();
            DataSet DsDataset = new DataSet();
            double SaldoDepositos = 0, ValProm = 0, ValDist = 0;
            int fila = 0;
            double Banda1 = 0, Banda2 = 0, Banda3 = 0, Banda4 = 0, Banda5 = 0, Banda6 = 0, Banda7 = 0;
            decimal PorCen = 0;
            double promedio = 0, dif = 0;

            // promedio = BuscaPromedioDepositos(PeriodoInicial, PeriodoFinal, DiasLiq, myconnect); // ERROR: CS0103
            SaldoDepositos = BuscarSaldoCuenta("210500000000", PeriodoFinal.ToString(), "9999", "99999999", myconnect) * -1;
            dif = Math.Round(SaldoDepositos - promedio, 2);

            if (dif > 0)
            {
                stbuil.Append("select sum(promedio) as campo1 ");
                stbuil.Append("from cnt_riesgo03_vw ");
                stbuil.Append("where periodo between '" + PeriodoInicial + "' and '" + PeriodoFinal + "' and cuenta = '2105'");
                stbuil.Append("and promedio < '" + promedio + "'");

                DataSet _dsVP = new DataSet();
                conect.ExecuteQueryDataset(stbuil.ToString(), myconnect, "GeneraDepositos", ref _dsVP, "tblvp");
                if (_dsVP.Tables["tblvp"].Rows.Count > 0 && !(_dsVP.Tables["tblvp"].Rows[0]["campo1"] is DBNull))
                    ValProm = Convert.ToDouble(_dsVP.Tables["tblvp"].Rows[0]["campo1"]);

                sb.Append("select periodo,promedio ");
                sb.Append("from cnt_riesgo03_vw ");
                sb.Append("where periodo between '" + PeriodoInicial + "' and '" + PeriodoFinal + "' and cuenta = '2105'");
                sb.Append("and promedio < '" + promedio + "'");
                conect.ExecuteQueryDataset(sb.ToString(), myconnect, "GeneraDepositos", ref DsDataset, "tblnovedades");

                Banda1 = 0; Banda2 = 0; Banda3 = 0; Banda4 = 0; Banda5 = 0; Banda6 = 0; Banda7 = 0;
                ValDist = dif - ValProm;
                Banda7 = promedio;

                while (fila < DsDataset.Tables["tblnovedades"].Rows.Count)
                {
                    DataRow row = DsDataset.Tables["tblnovedades"].Rows[fila];
                    int mes = int.Parse(row["periodo"].ToString().Substring(4, 2));
                    if      (mes == Ordenbandas[1]) { PorCen = Math.Round(Convert.ToDecimal(row["promedio"]) / Convert.ToDecimal(ValProm), 8); Banda1 += Math.Round((dif * (double)PorCen), 2); }
                    else if (mes == Ordenbandas[2]) { PorCen = Math.Round(Convert.ToDecimal(row["promedio"]) / Convert.ToDecimal(ValProm), 8); Banda2 += Math.Round((dif * (double)PorCen), 2); }
                    else if (mes == Ordenbandas[3]) { PorCen = Math.Round(Convert.ToDecimal(row["promedio"]) / Convert.ToDecimal(ValProm), 8); Banda3 += Math.Round((dif * (double)PorCen), 2); }
                    else if (mes == Ordenbandas[4] || mes == Ordenbandas[5] || mes == Ordenbandas[6]) { PorCen = Math.Round(Convert.ToDecimal(row["promedio"]) / Convert.ToDecimal(ValProm), 8); Banda4 += Math.Round((dif * (double)PorCen), 2); }
                    else if (mes == Ordenbandas[7] || mes == Ordenbandas[8] || mes == Ordenbandas[9]) { PorCen = Math.Round(Convert.ToDecimal(row["promedio"]) / Convert.ToDecimal(ValProm), 8); Banda5 += Math.Round((dif * (double)PorCen), 2); }
                    else if (mes == Ordenbandas[10] || mes == Ordenbandas[11] || mes == Ordenbandas[12]) { PorCen = Math.Round(Convert.ToDecimal(row["promedio"]) / Convert.ToDecimal(ValProm), 8); Banda6 += Math.Round((dif * (double)PorCen), 2); }
                    fila++;
                }
                // GrabaDsDataRiesgo(DsdataRiesgo, 2, "005", "DEPOSITOS DE AHORROS", SaldoDepositos, Banda1, Banda2, Banda3, Banda4, Banda5, Banda6, Banda7); // ERROR: CS0103
            }
            else
            {
                // GrabaDsDataRiesgo(DsdataRiesgo, 2, "005", "DEPOSITOS DE AHORROS", SaldoDepositos, 0, 0, 0, 0, 0, 0, SaldoDepositos); // ERROR: CS0103
            }
        }

        // VB 7029
        private void GeneraAportes(string NomArchivo, DataTable DsdataRiesgo, int PeriodoInicial, int PeriodoFinal, int[] Ordenbandas, OdbcConnection myconnect)
        {
            var sb = new System.Text.StringBuilder();
            double Vlrtotal = 0, SaldoAportes = 0;
            DataSet DsDataset = new DataSet();
            int fila = 0;
            double Banda1 = 0, Banda2 = 0, Banda3 = 0, Banda4 = 0, Banda5 = 0, Banda6 = 0, Banda7 = 0;
            double promedio = 0;

            // promedio = BuscaPromedioAportes(PeriodoInicial, PeriodoFinal, myconnect); // ERROR: CS0103
            SaldoAportes = BuscarSaldoCuenta("310500000000", PeriodoFinal.ToString(), "9999", "99999999", myconnect) * -1;

            sb.Append("select periodo, sum(saldia1 + saldia2 + saldia3 + saldia4 + saldia5 + saldia6 + saldia7 + saldia8 + saldia9 + saldia10 + saldia11 + saldia12 + ");
            sb.Append("saldia13 + saldia14 + saldia15 + saldia16 + saldia17 + saldia18 + saldia19 + saldia20 + saldia21 + saldia22 + saldia23 + saldia24 + ");
            sb.Append("saldia25 + saldia26 + saldia27 + saldia28 + saldia29 + saldia30 + saldia31) as Saldo ");
            sb.Append("from cnt_riesgo where cuenta like '3105%' and periodo between " + PeriodoInicial + " and " + PeriodoFinal);
            sb.Append(" group by periodo ");
            sb.Append("order by periodo ");
            conect.ExecuteQueryDataset(sb.ToString(), myconnect, "GeneraAportes", ref DsDataset, "tblaportes");

            Banda1 = 0; Banda2 = 0; Banda3 = 0; Banda4 = 0; Banda5 = 0; Banda6 = 0; Banda7 = 0;

            while (fila < DsDataset.Tables["tblaportes"].Rows.Count)
            {
                DataRow row = DsDataset.Tables["tblaportes"].Rows[fila];
                int mes = int.Parse(row["periodo"].ToString().Substring(4, 2));
                if      (mes == Ordenbandas[1])  Banda1 += Math.Round(Convert.ToDouble(row["saldo"]), 2);
                else if (mes == Ordenbandas[2])  Banda2 += Math.Round(Convert.ToDouble(row["saldo"]), 2);
                else if (mes == Ordenbandas[3])  Banda3 += Math.Round(Convert.ToDouble(row["saldo"]), 2);
                else if (mes == Ordenbandas[4] || mes == Ordenbandas[5] || mes == Ordenbandas[6])  Banda4 += Math.Round(Convert.ToDouble(row["saldo"]), 2);
                else if (mes == Ordenbandas[7] || mes == Ordenbandas[8] || mes == Ordenbandas[9])  Banda5 += Math.Round(Convert.ToDouble(row["saldo"]), 2);
                else if (mes == Ordenbandas[10] || mes == Ordenbandas[11] || mes == Ordenbandas[12]) Banda6 += Math.Round(Convert.ToDouble(row["saldo"]), 2);
                else Banda7 += Math.Round(Convert.ToDouble(row["saldo"]), 2);
                fila++;
            }

            Vlrtotal = Banda1 + Banda2 + Banda3 + Banda4 + Banda5 + Banda6 + Banda7;
            if (Vlrtotal < 0)
                Banda7 = SaldoAportes;
            else
                Banda7 = SaldoAportes - Vlrtotal;

            // GrabaDsDataRiesgo(DsdataRiesgo, 3, "005", "APORTES SOCIALES", SaldoAportes, Banda1, Banda2, Banda3, Banda4, Banda5, Banda6, Banda7); // ERROR: CS0103
        }

    } // partial class
} // namespace
