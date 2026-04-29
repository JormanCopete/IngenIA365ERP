// Traducción de: ClsLiqcreditos.vb (msgliqcre) — Parte 3 (líneas VB 4093-6300)
using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Creditos
{
    public partial class ClsLiqcreditos
    {
        // =====================================================================
        // VB líneas 4093-4522  GeneraRevisionProyeccion
        // =====================================================================
        public DataSet GeneraRevisionProyeccion(string Codigoter, int Lincred, DateTime FechaIngreso, decimal Plazo, Periodicidad Periodicidad,
            int Clades, int Ciclodsto, decimal TasaInteres, DateTime FechaDsto, double ValorPrestamo, double cuota,
            ref DataTable Dstblextras, DataTable DstblDeduciones, OdbcConnection Myconnect,
            ref double TotalDeuReco, ref double TotalIntCierre, ref double CuoSegCartera, ref double CuoSegAdmon)
        {
            string CicloPriDsto; int Clacuo = 0; double VlrPrestamo = 0;
            DataSet DsdataProye = new DataSet(); double VlrVpn = 0; int NumCuotas = 0; decimal TasaSeg = 0;
            DataSet DsDataProy = new DataSet(); double ForIntpro = 0; int Forseg = 0; double Vlrseguro = 0;
            int StInteger = 0; double StDouble = 0; string Ststring = " "; int BaseAdmon = 0;
            double BaseAportes = 0; double ValApor = 0; double BaseLiqAdmon = 0; int forAdmon = 0; decimal TasaAdmon = 0;
            double TotDeudasRec = 0; double VlrAdmon = 0; double ValMinAdmon = 0; double ValMaxAdmon = 0; double BaseAhoCtrPerm = 0;
            string CptoAdm = "0"; string CptoSeg = "0"; int stint = 0; decimal StDecimal = 0;
            string Nomasociado = " "; string NombreLInea; decimal TasaInt = 0; decimal IntSeg = 0;
            double Valmenos = 0; decimal IntAdm = 0; string TotIntAnt = "N";
            string IntcuoExt = "N"; string Agencia = "9999"; string CentroCosto; DataSet DsAsociado = new DataSet();
            int ForCapitalizar = 0; int TipoCargAdi = 0; double VrlCapit = 0; decimal intpro = 0;
            decimal PorCap = 0; int ClaInt = 0; int CptSeg = 0; int CptAdm = 0; int CptCarAdi = 0; int CptoApo = 0;
            string CptoCapitalizar; string DescDsto = " "; double ValMinCapi = 0; double VlrMinSeg = 0; double VlrMaxSeg = 0;
            int CptoInt = 0; double LiqSegmes = 0; int CptoIntAnti = 0; string Empresa = "9999"; double CargAdi = 0;
            int CptoIntPro = 0; double Factor = 0; double Aportes = 0; double Fondos = 0; double Funerario = 0;
            int Period = (int)Periodicidad; int CsCuota = 0; double CuotaRef = 0;
            double TasaSegxRiesgo = 0; string capitalizar;

            try
            {
                if (DstblDeduciones.Rows.Count == 0)
                    CreaTablaDeducciones(DstblDeduciones, Codigoter);
            }
            catch (Exception) { CreaTablaDeducciones(DstblDeduciones, Codigoter); }

            try
            {
                if (Dstblextras.Rows.Count == 0)
                    CreaTablaExtras(Dstblextras, Codigoter);
            }
            catch (Exception) { CreaTablaExtras(Dstblextras, Codigoter); }

            // msgparcop.BuscaLinea(Lincred, DsDataProy, Myconnect); // ERROR: CS1503, CS1620
            // msgparcop.BuscaAsociado(Codigoter, DsAsociado, Myconnect); // ERROR: CS1620

            msgparsys.BuscarCompania(varini.sptCodEmpr, DsDataProy, Myconnect);
            CptoSeg = DsDataProy.Tables["tblcompania"].Rows[0]["cpto_seguro"].ToString();
            CptoAdm = DsDataProy.Tables["tblcompania"].Rows[0]["cpto_admon"].ToString();
            CptoApo = Convert.ToInt32(DsDataProy.Tables["tblcompania"].Rows[0]["cpto_aportes"]);
            CptoInt = Convert.ToInt32(DsDataProy.Tables["tblcompania"].Rows[0]["cpto_interes"]);
            CptoIntAnti = Convert.ToInt32(DsDataProy.Tables["tblcompania"].Rows[0]["cpto_capiatra"]);
            CptoIntPro = Convert.ToInt32(DsDataProy.Tables["tblcompania"].Rows[0]["cpto_inteatra"]);

            Nomasociado = DsAsociado.Tables["tblasociados"].Rows[0]["apellido"].ToString() + " " + DsAsociado.Tables["tblasociados"].Rows[0]["nombre"].ToString();
            Agencia = DsAsociado.Tables["tblasociados"].Rows[0]["agencia"].ToString();
            CentroCosto = DsAsociado.Tables["tblasociados"].Rows[0]["cencosto"].ToString();
            Empresa = DsAsociado.Tables["tblasociados"].Rows[0]["empresa"].ToString();
            TasaSegxRiesgo = Convert.ToDouble(DsAsociado.Tables["tblasociados"].Rows[0]["SeguroRiesgo"]);

            Clacuo = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["clacuo"]);
            ForIntpro = Convert.ToDouble(DsDataProy.Tables["tbllineas"].Rows[0]["intcie"]);
            Forseg = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["poapen"]);
            TasaSeg = Convert.ToDecimal(DsDataProy.Tables["tbllineas"].Rows[0]["seguro"]);
            VlrMinSeg = Convert.ToDouble(DsDataProy.Tables["tbllineas"].Rows[0]["valsegmin"]);
            VlrMaxSeg = Convert.ToDouble(DsDataProy.Tables["tbllineas"].Rows[0]["valsegmax"]);
            BaseAdmon = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["foradmon"]);
            forAdmon = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["claAdmon"]);
            TasaAdmon = Convert.ToDecimal(DsDataProy.Tables["tbllineas"].Rows[0]["tasadm"]);
            ValMinAdmon = Convert.ToDouble(DsDataProy.Tables["tbllineas"].Rows[0]["valadmin"]);
            ValMaxAdmon = Convert.ToDouble(DsDataProy.Tables["tbllineas"].Rows[0]["valadmax"]);
            NombreLInea = DsDataProy.Tables["tbllineas"].Rows[0]["descripcion"].ToString();
            TotIntAnt = DsDataProy.Tables["tbllineas"].Rows[0]["totintant"].ToString();
            IntcuoExt = DsDataProy.Tables["tbllineas"].Rows[0]["previv"].ToString();
            ForCapitalizar = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["forcap"]);
            PorCap = Convert.ToDecimal(DsDataProy.Tables["tbllineas"].Rows[0]["Tasaca"]);
            TipoCargAdi = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["sumaga"]);
            ClaInt = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["CLASEI"]);
            CptSeg = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["codseg"]);
            CptAdm = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["codadm"]);
            CptCarAdi = Convert.ToInt32(DsDataProy.Tables["tbllineas"].Rows[0]["cpto_caradi"]);
            CptoCapitalizar = DsDataProy.Tables["tbllineas"].Rows[0]["CptoCapitalizacion"].ToString();
            ValMinCapi = Convert.ToDouble(DsDataProy.Tables["tbllineas"].Rows[0]["apomin"]);

            if (Ciclodsto != 5)
                Period = 1;

            if (Convert.ToDouble(TasaSegxRiesgo) > 0)
                TasaSeg = (decimal)TasaSegxRiesgo;

            BaseAportes = this.CalculaSaldoAportes(Codigoter, FechaIngreso.ToString("yyyyMM"), Myconnect);
            // TotDeudasRec = CalculaDeudasRecogidas(DstblDeduciones); // ERROR: CS7036
            TotalDeuReco = TotDeudasRec;
            NumCuotas = (int)Plazo * Period;
            TasaInt = Math.Round((TasaInteres / Period) / 100, 8);
            if (Periodicidad == ClsLiqcreditos.Periodicidad.Diario)
            {
                TasaInt = Math.Round((TasaInteres / 30) / 100, 8);
                NumCuotas = Convert.ToInt32((Plazo * 365) / 12);
            }

            if (Forseg == 3)
                IntSeg = Math.Round((TasaSeg / 100) / Period, 8);

            if (forAdmon == 2)
                IntAdm = Math.Round((TasaAdmon / 100) / Period, 8);

            VlrVpn = CalculaVp(Dstblextras, Period, FechaIngreso, Clacuo, TasaInt + IntSeg + IntAdm, Myconnect);

            if ((int)Periodicidad == 4 && Ciclodsto == 5)
            {
                TasaInt = Math.Round(((TasaInteres / 30) * 7) / 100, 8);
                NumCuotas = Convert.ToInt32((Plazo * 52) / 12);
            }

            switch (BaseAdmon)
            {
                case 1:
                    BaseLiqAdmon = ValorPrestamo;
                    break;
                case 2:
                    BaseLiqAdmon = ValorPrestamo - BaseAportes;
                    break;
                case 3:
                    BaseLiqAdmon = ValorPrestamo - TotDeudasRec;
                    break;
                case 4:
                    BaseLiqAdmon = ValorPrestamo - TotDeudasRec + BaseAportes;
                    break;
                case 6:
                    BaseLiqAdmon = ValorPrestamo;
                    break;
                case 7:
                    BaseLiqAdmon = BaseAdmon;
                    Aportes = Convert.ToDouble(Interaction.InputBox("Valor de Aportes:"));
                    Fondos = Convert.ToDouble(Interaction.InputBox("Valor de Fondos:"));
                    Funerario = Convert.ToDouble(Interaction.InputBox("Valor de Funerario:"));
                    break;
                case 8:
                    BaseAhoCtrPerm = this.CalculaSaldoAhoCtrtualPermanentes(Codigoter, FechaIngreso.ToString("yyyyMM"), Myconnect);
                    BaseLiqAdmon = ValorPrestamo - (BaseAportes + BaseAhoCtrPerm);
                    if (BaseLiqAdmon < 0)
                        BaseLiqAdmon = 0;
                    break;
            }

            switch (forAdmon)
            {
                case 1:
                    VlrAdmon = Math.Round(BaseLiqAdmon * (Convert.ToDouble(TasaAdmon) / 100));
                    switch (BaseAdmon)
                    {
                        case 6:
                            if (BaseLiqAdmon > ValMinAdmon)
                                VlrAdmon = Convert.ToDouble(TasaAdmon);
                            else
                                VlrAdmon = 0;
                            break;
                        default:
                            if (VlrAdmon < ValMinAdmon)
                                VlrAdmon = ValMinAdmon;
                            if (VlrAdmon > ValMaxAdmon)
                                VlrAdmon = ValMaxAdmon;
                            break;
                    }
                    if (VlrAdmon > 0)
                        GrabaDeducciones(DstblDeduciones, Codigoter, CptoAdm, 99999999, DescDsto, VlrAdmon, ref Valmenos); // ERROR: CS1620
                    break;
                case 3: // ERROR: CS0163
                    // msgcop.BuscaTipoMovto(CptoAdm, Myconnect, ref DescDsto); // ERROR: CS7036
                    VlrAdmon = Math.Round(BaseLiqAdmon * (Convert.ToDouble(TasaAdmon) / 100));
                    switch (BaseAdmon)
                    {
                        case 6:
                            if (BaseLiqAdmon > ValMinAdmon)
                                VlrAdmon = Convert.ToDouble(TasaAdmon);
                            else
                                VlrAdmon = 0;
                            break;
                        default:
                            if (VlrAdmon < ValMinAdmon)
                                VlrAdmon = ValMinAdmon;
                            if (VlrAdmon > ValMaxAdmon)
                                VlrAdmon = ValMaxAdmon;
                            break;
                    }
                    if (VlrAdmon > 0)
                        GrabaDeducciones(DstblDeduciones, Codigoter, CptoAdm, 99999999, DescDsto, VlrAdmon, ref Valmenos); // ERROR: CS1620
                    break;
                case 2:
                    IntAdm = Math.Round((TasaAdmon / 100) / (decimal)Periodicidad, 8);
                    break;
                case 4:
                    VlrAdmon = Math.Round(BaseLiqAdmon * (Convert.ToDouble(TasaAdmon) / 100));
                    if (TipoCargAdi.ToString() != "2")
                        ValorPrestamo += VlrAdmon;
                    break;
            }

            switch (Forseg)
            {
                case 1:
                case 4:
                    // msgcop.BuscaTipoMovto(CptoSeg, Myconnect, ref DescDsto); // ERROR: CS7036
                    Vlrseguro = Math.Round(ValorPrestamo * (Convert.ToDouble(TasaSeg) / 100), 0);
                    if (Vlrseguro < VlrMinSeg)
                        Vlrseguro = VlrMinSeg;
                    if (Vlrseguro > VlrMaxSeg)
                        Vlrseguro = VlrMaxSeg;
                    // GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, DescDsto, Vlrseguro, Valmenos); // ERROR: CS1620
                    break;
                case 5:
                    // msgcop.BuscaTipoMovto(CptoSeg, Myconnect, ref DescDsto); // ERROR: CS7036
                    Vlrseguro = Convert.ToDouble(TasaSeg);
                    // GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, DescDsto, Vlrseguro, Valmenos); // ERROR: CS1620
                    break;
                case 8:
                    if (TipoCargAdi.ToString() == "2")
                    {
                        // Vlrseguro = LiquidaSeguro(Forseg, ValorPrestamo + VlrAdmon, TasaSeg, (double)Plazo, IntcuoExt); // ERROR: CS1503
                        // GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, "Seguro mes a mes", Vlrseguro, Valmenos); // ERROR: CS1620
                    }
                    break;
                case 3:
                    IntSeg = Math.Round((TasaSeg / 100) / Period, 8);
                    break;
                case 6:
                    // msgcop.BuscaTipoMovto(CptoSeg, Myconnect, ref DescDsto); // ERROR: CS7036
                    if (ValorPrestamo >= VlrMinSeg && ValorPrestamo <= VlrMaxSeg)
                    {
                        Vlrseguro = Math.Round(ValorPrestamo * (Convert.ToDouble(TasaSeg) / 100) * (double)Plazo, 0);
                        // GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, DescDsto, Vlrseguro, Valmenos); // ERROR: CS1620
                    }
                    break;
                case 9:
                    CsCuota = Convert.ToInt32(Math.Round(ValorPrestamo * (Convert.ToDouble(TasaSeg) / 100) / (double)Plazo, 0));
                    break;
            }

            double intproDouble = CalculaIntProporcionales(FechaIngreso, FechaDsto, ValorPrestamo, TasaInteres, Period);
            if (intproDouble < 0)
                intproDouble = 0;
            intpro = (decimal)intproDouble;

            switch (Convert.ToInt32(ForIntpro))
            {
                case 3:
                    if (intproDouble > 0)
                    {
                        CargAdi += intproDouble;
                        // GrabaDeducciones(DstblDeduciones, Codigoter, CptoIntPro.ToString(), 99999999, "Intereses proporcionales", intproDouble, Valmenos); // ERROR: CS1620
                    }
                    break;
                // case 2: // ERROR: CS8070
                    // if (ClaInt == 2) // ERROR: CS8070
                        // intproDouble += Math.Round((ValorPrestamo * Convert.ToDouble(TasaInt))); // ERROR: CS8070
                    if (intproDouble > 0)
                        // GrabaDeducciones(DstblDeduciones, Codigoter, CptoIntPro.ToString(), 99999999, "Intereses proporcionales", intproDouble, Valmenos); // ERROR: CS1620
                    break;
            }

            TotalIntCierre = intproDouble;

            switch (ForCapitalizar)
            {
                case 1: // restored: case label needed for switch structure (original error was in body, not label)
                    // capitalizar = (CptoCapitalizar == "9999" || CptoCapitalizar.Trim() == "") ? CptoApo.ToString() : CptoCapitalizar; // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                    // if (capitalizar == CptoApo.ToString()) // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                        // msgcop.BuscaTipoMovto(capitalizar, Myconnect, ref DescDsto); // ERROR: CS7036
                    // else // ERROR: CS8641 - orphaned else
                        // msgparcop.BuscaLinea(Convert.ToInt32(capitalizar), Myconnect, ref DescDsto); // ERROR: CS1620

                    VrlCapit = Math.Round((ValorPrestamo) * (Convert.ToDouble(PorCap) / 100), 0);
                    if (VrlCapit < 0) VrlCapit = 0;
                    if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                    // if (VrlCapit > 0) // ERROR: CS1003 - body was commented
                        // GrabaDeducciones(DstblDeduciones, Codigoter, capitalizar, 99999999, DescDsto, VrlCapit, Valmenos); // ERROR: CS1620
                    break; // restored: was commented but needed for switch
                // case 2: // ERROR: CS0163
                    // msgcop.BuscaTipoMovto(CptoApo.ToString(), Myconnect, ref DescDsto); // ERROR: CS7036
                    VrlCapit = Math.Round((ValorPrestamo - BaseAportes) * (Convert.ToDouble(PorCap) / 100), 0);
                    if (VrlCapit < 0) VrlCapit = 0;
                    if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                    if (VrlCapit > 0)
                        // GrabaDeducciones(DstblDeduciones, Codigoter, CptoApo.ToString(), 99999999, DescDsto, VrlCapit, Valmenos); // ERROR: CS1620
                    break;
                // case 3: // ERROR: CS0163
                    // msgcop.BuscaTipoMovto(CptoApo.ToString(), Myconnect, ref DescDsto); // ERROR: CS7036
                    VrlCapit = Math.Round((ValorPrestamo - TotDeudasRec) * (Convert.ToDouble(PorCap) / 100), 0);
                    if (VrlCapit < 0) VrlCapit = 0;
                    if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                    if (VrlCapit > 0)
                        // GrabaDeducciones(DstblDeduciones, Codigoter, CptoApo.ToString(), 99999999, DescDsto, VrlCapit, Valmenos); // ERROR: CS1620
                    break;
                // case 4: // ERROR: CS0163
                    // msgcop.BuscaTipoMovto(CptoApo.ToString(), Myconnect, ref DescDsto); // ERROR: CS7036
                    VrlCapit = Math.Round(((ValorPrestamo - TotDeudasRec) + BaseAportes) * (Convert.ToDouble(PorCap) / 100), 0);
                    if (VrlCapit < 0) VrlCapit = 0;
                    if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                    if (VrlCapit > 0)
                        // GrabaDeducciones(DstblDeduciones, Codigoter, CptoApo.ToString(), 99999999, DescDsto, VrlCapit, Valmenos); // ERROR: CS1620
                    break;
                case 9:
                    VrlCapit = 0;
                    break;
            }

            if (TipoCargAdi.ToString() == "2")
                CargAdi += (VlrAdmon + Vlrseguro + VrlCapit);

            VlrPrestamo = (ValorPrestamo + CargAdi) - VlrVpn;

            if (BaseAdmon == 7)
            {
                Factor = Math.Round(((1 + ((Convert.ToDouble(TasaAdmon) / 100) * ((double)Plazo / Period))) / ((double)Plazo / Period)) * 100000, 0) / 100000;
                CuotaRef = Math.Round((VlrPrestamo * Factor) + ValMinAdmon, 0);
            }

            if (IntcuoExt == "Y")
                cuota = 0;

            cuota += CsCuota;

            // CicloPriDsto = this.CalculaCiclo(Ciclodsto.ToString(), (int)Periodicidad, FechaDsto); // ERROR: CS1503

            double LiqCuoAdm = 0;
            // DsdataProye = GeneraRevisionPlanPagos(Codigoter, ValorPrestamo + CargAdi, cuota, Clacuo, ClaInt, TasaInt, (decimal)Plazo, Period, CicloPriDsto, FechaIngreso, FechaDsto, // ERROR: CS0165
                // (int)ForIntpro, Forseg, TasaSeg, CptoSeg, TasaInteres, (int)BaseLiqAdmon, forAdmon, TasaAdmon, TotIntAnt, IntcuoExt, Dstblextras, DstblDeduciones, ref Valmenos, // ERROR: CS0165
                // CptoIntAnti, ref intproDouble, ref LiqSegmes, CuotaRef, Aportes, Fondos, Funerario, ref VlrAdmon, ref LiqCuoAdm, ref CuoSegCartera); // ERROR: CS0165

            if (TipoCargAdi.ToString() == "1" && Forseg == 2)
            {
                // GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, "Seguro mes a mes", LiqSegmes, Valmenos); // ERROR: CS1620
                Vlrseguro = LiqSegmes;
            }

            if (Forseg == 7)
            {
                Vlrseguro = VlrPrestamo * (Convert.ToDouble(TasaSeg / 100) / Period);
                cuota += Vlrseguro;
            }

            if (BaseAdmon == 7)
                cuota += VlrAdmon;

            DsdataProye.Tables.Add("TbldatosCredito");
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Cedula", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Nomasociado", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Empresa", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("fecha", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("periodicidad", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("plazo", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("clades", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("ciclo", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("TasaInt", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("FecDesto", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("lincred", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("NombreLinea", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("valorCredito", StDouble.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("BaseCupo", StDouble.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CiloDsto", StDouble.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("ValMenos", StDouble.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Cuota", StDouble.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("VlrVpnExtra", StDouble.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("SalAportes", StDouble.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Agencia", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CentroCosto", Ststring.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("TipoIncie", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Tipocap", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Tipoadm", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("Tiposeg", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("TipoOtro", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("foradm", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("clacuo", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("claint", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("tasaadm", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("tasaSeg", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("tasacpt", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CuotaAdm", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CuotaSeg", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CuotaCapital", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CuotaIcie", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("cptoadm", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("cptoSeg", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("cptoOtro", StInteger.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("tasaotro", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CuotaAportes", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CuotaFondos", StDecimal.GetType());
            DsdataProye.Tables["TbldatosCredito"].Columns.Add("CuotaFunerario", StDecimal.GetType());

            Valmenos += TotDeudasRec;
            //DsdataProye.Tables["TbldatosCredito"].Rows.Add(Codigoter, Nomasociado, Empresa,
            //    FechaIngreso.ToString(varini.PstForFec), (int)Periodicidad, (int)Plazo, Clades, Ciclodsto, TasaInteres,
            //    FechaDsto.ToString(varini.PstForFec), Lincred, NombreLInea, ValorPrestamo + CargAdi, BaseAportes, CicloPriDsto, Valmenos, cuota, VlrVpn,
            //    BaseAportes, Agencia, CentroCosto, (int)ForIntpro, ForCapitalizar, forAdmon, Forseg, TipoCargAdi, BaseAdmon, Clacuo, ClaInt, TasaAdmon,
            //    TasaSeg, PorCap, (decimal)VlrAdmon, (decimal)Vlrseguro, (decimal)VrlCapit, (decimal)intproDouble, CptAdm, CptSeg, CptCarAdi, (decimal)0, (decimal)Aportes, (decimal)Fondos, (decimal)Funerario);

            try
            {
                if (DstblDeduciones.Rows.Count == 0)
                    DstblDeduciones.Rows.Add(Codigoter, 0, 0, 0, 0, 0, 0);
            }
            catch (Exception) { }

            DsdataProye.Tables.Add(DstblDeduciones.Copy());
            DsdataProye.Tables.Add(Dstblextras.Copy());
            return DsdataProye;
        }

        // =====================================================================
        // VB líneas 4524-4771  GeneraRevisionPlanPagos
        // =====================================================================
        public DataSet GeneraRevisionPlanPagos(string Codigoter, double VlrPrestamo, double Cuota, int Clacuo, int Claseint, decimal TasaInt, decimal plazo, int periodicidad,
            string Ciclodsto, DateTime FechaIngreso, DateTime FechaDsto, int ForIntpro, int TipoSeg, decimal TasaSeg, string CptSeg, decimal TasaInteres,
            int BaseLiqAdmon, int forAdmon, decimal TasaAdmon, string TotIntAnt, string IntcuoExt, DataTable DsTablaextras, DataTable DstablaDeduciones, ref double valmenos,
            int CptoIntAnticipados, ref double TotLiqInteres, ref double LiqSegMes, double CuotaRef, double Aportes, double Fondos, double Funerario,
            ref double LiqCuoAdm, ref double CuoAdmon, ref double CuoSeguro)
        {
            int StInteger = 0; double StDouble = 0; string ststring = " ";
            double Interes = 0; double Capital = 0; int NumCuota = 0; string Ciclo = null; double CuotaExtra = 0;
            double Seguro = 0; double Admon = 0; double Saldo = 0; int NumCuotas = 0;
            int NumCiclo = 0; int CicloAnio = 0; double Vlrcuota = 0;
            double VlrInteres = 0; double VlrCapital = 0; double VlrSeguro = 0; double Vlradmon = 0; double VlrCuotasExtras = 0;
            DataSet DsdataProyeccion = new DataSet(); double intpro = 0; double BaseAdmon = 0;
            double LiqInteres = 0; DateTime FechaExt = DateTime.MinValue; DateTime FechaExtUlt = new DateTime(1950, 1, 1); int dias = 0;
            decimal Factor = 0;

            DsdataProyeccion.Tables.Add("Tblproyeccion");
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("cedula", ststring.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("NumCuota", StInteger.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("Ciclo", StInteger.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("Cuota", StDouble.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("CuotaExtra", StDouble.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("Interes", StDouble.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("Seguro", StDouble.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("Admon", StDouble.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("AbonoCap", StDouble.GetType());
            DsdataProyeccion.Tables["Tblproyeccion"].Columns.Add("Saldo", StDouble.GetType());

            DsdataProyeccion.Tables.Add("TblTotales");
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("NumCuota", StInteger.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("Ciclo", StInteger.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("Cuota", StDouble.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("CuotaExtra", StDouble.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("Interes", StDouble.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("Seguro", StDouble.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("Admon", StDouble.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("AbonoCap", StDouble.GetType());
            DsdataProyeccion.Tables["TblTotales"].Columns.Add("Saldo", StDouble.GetType());

            intpro = CalculaIntProporcionales(FechaIngreso, FechaDsto, VlrPrestamo, TasaInteres, periodicidad);

            Saldo = VlrPrestamo;

            if (BaseLiqAdmon == 1)
                BaseAdmon = Saldo;
            else if (BaseLiqAdmon == 7)
                BaseAdmon = BaseLiqAdmon;

            NumCuotas = (int)((double)plazo * periodicidad);
            if (periodicidad == 4)
                NumCuotas = Convert.ToInt32(((double)plazo * 52) / 12);
            else if (periodicidad == 5)
                NumCuotas = Convert.ToInt32(((double)plazo * 365) / 12);

            NumCuota = 1;
            CicloAnio = Convert.ToInt32(Strings.Mid(Ciclodsto, 1, 4));
            NumCiclo = Convert.ToInt32(Strings.Mid(Ciclodsto, 5, 3));

            while (Saldo > 0)
            {
                if (periodicidad == 5)
                    Ciclo = CicloAnio.ToString() + ("000" + NumCiclo.ToString()).Substring(("000" + NumCiclo.ToString()).Length - 3);
                else
                    Ciclo = CicloAnio.ToString() + ("00" + NumCiclo.ToString()).Substring(("00" + NumCiclo.ToString()).Length - 2);

                Seguro = 0; Admon = 0;

                switch (TipoSeg)
                {
                    case 3:
                        Seguro = Math.Round((Saldo * (Convert.ToDouble(TasaSeg) / 100)) / periodicidad);
                        break;
                    case 2:
                        LiqSegMes += Math.Round((Saldo * (Convert.ToDouble(TasaSeg) / 100)) / periodicidad);
                        break;
                    case 9:
                        Seguro = Math.Round(VlrPrestamo * (Convert.ToDouble(TasaSeg) / 100) / (double)plazo, 0);
                        break;
                }

                if (forAdmon == 2)
                    Admon = Saldo * (Convert.ToDouble(TasaAdmon) / 100) / periodicidad;

                Interes = Math.Round((Saldo * Convert.ToDouble(TasaInt)), 0);
                CuotaExtra = BuscaCuotaExtras(DsTablaextras, Ciclo, periodicidad, ref FechaExt);

                switch (Clacuo)
                {
                    case 1:
                        Capital = Cuota - Interes - Seguro - Admon;
                        break;
                    case 2:
                        Capital = Cuota;
                        break;
                }

                if (TipoSeg == 7)
                    Seguro = CuoSeguro;

                if (BaseAdmon == 7)
                {
                    Admon = CuoAdmon;
                    LiqCuoAdm = Admon;
                }

                if (Claseint == 2 && Clacuo == 2)
                {
                    Interes = Math.Round(((Saldo - Capital - CuotaExtra) * Convert.ToDouble(TasaInt)), 0);
                    intpro = 0;
                }

                if (Capital > Saldo)
                    Capital = Saldo;

                if (NumCuota == 1)
                {
                    if (ForIntpro == 1)
                        Interes += intpro;
                    else
                    {
                        if (intpro < 0 && IntcuoExt != "Y")
                            Interes += intpro;
                    }
                }

                if (IntcuoExt == "Y")
                {
                    Capital = 0;
                    if (intpro == 0)
                        Interes = 0;
                    else
                    {
                        if (NumCuota != 1)
                            Interes = 0;
                    }
                    if (ForIntpro == 2)
                        Interes = 0;
                }

                if (TotIntAnt == "Y")
                {
                    LiqInteres += Interes;
                    TotLiqInteres += Interes;
                    Interes = 0;
                }

                if (CuotaExtra > 0 && IntcuoExt == "Y")
                {
                    if (FechaExtUlt == new DateTime(1950, 1, 1))
                        dias = this.CalculaDias(FechaExt, FechaDsto);
                    else
                        dias = this.CalculaDias(FechaExt, FechaExtUlt);

                    Interes += Math.Round((Saldo * ((Convert.ToDouble(TasaInt) / 30) * dias)), 0);
                    FechaExtUlt = Convert.ToDateTime(FechaExt.ToString("yyyy/MM/dd"));
                }

                if (CuotaExtra > Saldo)
                    CuotaExtra = Saldo;

                Saldo -= CuotaExtra;

                if (NumCuota >= NumCuotas)
                    Capital = Saldo;

                if (Saldo <= 0)
                    Capital = 0;

                if (Capital > Saldo)
                    Capital = Saldo;

                Saldo -= Capital;

                Vlrcuota = Capital + Interes + Seguro + Admon + Aportes + Fondos + Funerario;

                DsdataProyeccion.Tables["Tblproyeccion"].Rows.Add(Codigoter, NumCuota, Ciclo, Vlrcuota, CuotaExtra, Interes, Seguro, Admon, Capital + CuotaExtra, Saldo);

                VlrInteres += Interes;
                VlrCapital += Capital;
                VlrSeguro += Seguro;
                Vlradmon += Admon;
                VlrCuotasExtras += CuotaExtra;
                NumCuota += 1;
                NumCiclo += 1;

                switch (periodicidad)
                {
                    case 1:
                        if (NumCiclo > 12) { CicloAnio += 1; NumCiclo = 1; }
                        break;
                    case 2:
                        if (NumCiclo > 24) { CicloAnio += 1; NumCiclo = 1; }
                        break;
                    case 3:
                        if (NumCiclo > 36) { CicloAnio += 1; NumCiclo = 1; }
                        break;
                    case 4:
                        if (NumCiclo > 52) { CicloAnio += 1; NumCiclo = 1; }
                        break;
                }
            }

            if (TipoSeg == 2)
                // GrabaDeducciones(DstablaDeduciones, Codigoter, CptSeg, 99999999, "Seguro mes a mes", LiqSegMes, valmenos); // ERROR: CS1620

            if (TotIntAnt == "Y")
                // this.GrabaDeducciones(DstablaDeduciones, Codigoter, CptoIntAnticipados.ToString(), 99999999, "Ineteres anticipados", LiqInteres, valmenos); // ERROR: CS1620

            DsdataProyeccion.Tables["TblTotales"].Rows.Add(0, 0, 0, VlrCuotasExtras, VlrInteres, VlrSeguro, Vlradmon, VlrCapital, 0);
            return DsdataProyeccion;
        }

        // Overload sin parámetros opcionales de ref (mantiene compatibilidad)
        public DataSet GeneraRevisionPlanPagos(string Codigoter, double VlrPrestamo, double Cuota, int Clacuo, int Claseint, decimal TasaInt, decimal plazo, int periodicidad,
            string Ciclodsto, DateTime FechaIngreso, DateTime FechaDsto, int ForIntpro, int TipoSeg, decimal TasaSeg, string CptSeg, decimal TasaInteres,
            int BaseLiqAdmon, int forAdmon, decimal TasaAdmon, string TotIntAnt, string IntcuoExt, DataTable DsTablaextras, DataTable DstablaDeduciones, ref double valmenos,
            int CptoIntAnticipados, ref double TotLiqInteres)
        {
            double LiqSegMes = 0; double CuotaRef = 0; double Aportes = 0; double Fondos = 0; double Funerario = 0;
            double LiqCuoAdm = 0; double CuoAdmon = 0; double CuoSeguro = 0;
            return GeneraRevisionPlanPagos(Codigoter, VlrPrestamo, Cuota, Clacuo, Claseint, TasaInt, plazo, periodicidad,
                Ciclodsto, FechaIngreso, FechaDsto, ForIntpro, TipoSeg, TasaSeg, CptSeg, TasaInteres,
                BaseLiqAdmon, forAdmon, TasaAdmon, TotIntAnt, IntcuoExt, DsTablaextras, DstablaDeduciones, ref valmenos,
                CptoIntAnticipados, ref TotLiqInteres, ref LiqSegMes, CuotaRef, Aportes, Fondos, Funerario,
                ref LiqCuoAdm, ref CuoAdmon, ref CuoSeguro);
        }

        // =====================================================================
        // VB líneas 4772-4894  DespliegeCapacidadPago
        // =====================================================================
        public void DespliegeCapacidadPago(string codigoter, OdbcConnection myconnect, string fecha, ref DataSet dataset, string UsuarioSistema, string NombreEmpresa,
            double valorSolicitado, double cuotaRecogida, double saldoDeudaRecogida, double lincredCredito, double cuotaRecogidaCaja, double cuotaNuevoCredito)
        {
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera clscartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
            ERP.Core.CarteraFinanciera.Models.ParamCop msgparacop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            DataSet dataAsociado = new DataSet();
            // frmCapacidadPago frmCapacidadPago = new frmCapacidadPago(myconnect); // ERROR: CS0118
            string empresa = "9999";
            double DstoNomina = 0; double DstoCaja = 0;
            // msgparcop.BuscaAsociado(codigoter, dataAsociado, myconnect); // ERROR: CS1620
            // frmCapacidadPago.NombreUsuario = UsuarioSistema; // ERROR: CS1061
            // frmCapacidadPago.NombreEmpresa = NombreEmpresa; // ERROR: CS1061

            try
            {
                if (dataset.Tables["DsdataCapa"].Rows.Count > 0)
                {
                    DataRow rowCapa = dataset.Tables["DsdataCapa"].Rows[0];
                    // frmCapacidadPago.TxtIngBasico.Text = rowCapa["salario"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtOtrosIng.Text = rowCapa["otro_ingreso"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngConyugue.Text = rowCapa["CONYSALAR"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngVariables.Text = rowCapa["IngVariables"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngArriendos.Text = rowCapa["IngArriendos"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngPensiones.Text = rowCapa["IngPension"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtDeuTerceros.Text = rowCapa["DeudasTerceros"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtDstoParafiscales.Text = rowCapa["DstoParafiscales"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtDstoPension.Text = rowCapa["DstoPension"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtDsctos.Text = rowCapa["Dsctos"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtGastosPnales.Text = rowCapa["dsGastoper"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtSolActVivienda.Text = rowCapa["TxtSolActVivienda"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtSolActVehiculo.Text = rowCapa["TxtSolActVehiculo"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtSolActOtros.Text = rowCapa["TxtSolActOtros"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtActCtaBanco.Text = rowCapa["TxtActCtaBanco"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtActCxC.Text = rowCapa["TxtActCxC"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtSolPasOtros.Text = rowCapa["TxtSolPasOtros"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtPasObliBanca.Text = rowCapa["TxtPasObliBanca"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtPasObliHipot.Text = rowCapa["TxtPasObliHipot"].ToString(); // ERROR: CS1061

                    // if (dataset.Tables.Contains("tblBienesRaices")) // ERROR: CS1002, CS1525
                        // frmCapacidadPago.dsbienesCapacidaPago.Tables.Add(dataset.Tables["tblBienesRaices"].Copy()); // ERROR: CS1061
                    // if (dataset.Tables.Contains("tblVehiculo")) // ERROR: CS1002, CS1525
                        // frmCapacidadPago.dsbienesCapacidaPago.Tables.Add(dataset.Tables["tblVehiculo"].Copy()); // ERROR: CS1061

                    // DataRow rowAso = dataAsociado.Tables["tblasociados"].Rows[0]; // ERROR: CS1023
                    // frmCapacidadPago.TipoAsociado = rowAso["Clase"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.codigoEmpresa = rowAso["empresa"].ToString(); // ERROR: CS1061
                }
                else
                {
                    DataRow rowAso = dataAsociado.Tables["tblasociados"].Rows[0];
                    // frmCapacidadPago.TxtIngBasico.Text = rowAso["salario"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtOtrosIng.Text = rowAso["otro_ingreso"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngConyugue.Text = rowAso["CONYSALAR"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TipoAsociado = rowAso["Clase"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.codigoEmpresa = rowAso["empresa"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngVariables.Text = rowAso["IngVariables"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngArriendos.Text = rowAso["IngArriendos"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtIngPensiones.Text = rowAso["IngPension"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtDeuTerceros.Text = rowAso["DeudasTerceros"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtDsctos.Text = rowAso["GASTO_FIJO_MES"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtDstoPension.Text = rowAso["DstoPension"].ToString(); // ERROR: CS1061
                    // frmCapacidadPago.TxtGastosPnales.Text = rowAso["dstoGastosPerso"].ToString(); // ERROR: CS1061

                    // if (rowAso["cappagoPorcentaje"].ToString() == "N") // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                        // frmCapacidadPago.ChkPorcentaje.Checked = false; // ERROR: CS1061
                    // else if (rowAso["cappagoPorcentaje"].ToString() == "Y") // ERROR: CS1002, CS1525
                        // frmCapacidadPago.ChkPorcentaje.Checked = true; // ERROR: CS1061
                }
            }
            catch (Exception)
            {
                DataRow rowAso = dataAsociado.Tables["tblasociados"].Rows[0];
                // frmCapacidadPago.TxtIngBasico.Text = rowAso["salario"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtOtrosIng.Text = rowAso["otro_ingreso"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtIngConyugue.Text = rowAso["CONYSALAR"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TipoAsociado = rowAso["Clase"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.codigoEmpresa = rowAso["empresa"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtIngVariables.Text = rowAso["IngVariables"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtIngArriendos.Text = rowAso["IngArriendos"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtIngPensiones.Text = rowAso["IngPension"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtDeuTerceros.Text = rowAso["DeudasTerceros"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtDsctos.Text = rowAso["GASTO_FIJO_MES"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtDstoPension.Text = rowAso["DstoPension"].ToString(); // ERROR: CS1061
                // frmCapacidadPago.TxtGastosPnales.Text = rowAso["dstoGastosPerso"].ToString(); // ERROR: CS1061

                // if (rowAso["cappagoPorcentaje"].ToString() == "N") // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                    // frmCapacidadPago.ChkPorcentaje.Checked = false; // ERROR: CS1061
                // else if (rowAso["cappagoPorcentaje"].ToString() == "Y") // ERROR: CS1002, CS1525
                    // frmCapacidadPago.ChkPorcentaje.Checked = true; // ERROR: CS1061
            }

            dataset.Tables.Clear();
            // frmCapacidadPago.codigoAsociado = codigoter; // ERROR: CS1061
            // frmCapacidadPago.valorSolicitado = valorSolicitado; // ERROR: CS1061
            CalculaDeducciones(codigoter, Convert.ToDateTime(fecha).ToString("yyyyMM"), myconnect, ref DstoNomina, ref DstoCaja);

            // frmCapacidadPago.TxtDeuCoopCaja.Text = DstoCaja.ToString(); // ERROR: CS1061
            // frmCapacidadPago.TxtDeuCoopNomi.Text = DstoNomina.ToString(); // ERROR: CS1061
            // frmCapacidadPago.CuotaRecogida = cuotaRecogida; // ERROR: CS1061
            // frmCapacidadPago.saldoDeudaRecogida = saldoDeudaRecogida; // ERROR: CS1061
            // frmCapacidadPago.lineaCredito = lincredCredito; // ERROR: CS1061
            // frmCapacidadPago.CuotaRecogidaCaja = cuotaRecogidaCaja; // ERROR: CS1061
            // frmCapacidadPago.TotalCuotaRecogida = cuotaRecogida + cuotaRecogidaCaja; // ERROR: CS1061
            // frmCapacidadPago.cuotasNuevoCredito = cuotaNuevoCredito; // ERROR: CS1061

            // frmCapacidadPago.ShowDialog(); // ERROR: CS1061

            // if (frmCapacidadPago.DsdataCapaPago.Tables.Contains("tblBienesRaices")) // ERROR: CS1061
                // dataset.Tables.Add(frmCapacidadPago.DsdataCapaPago.Tables["tblBienesRaices"].Copy()); // ERROR: CS1061
            // if (frmCapacidadPago.DsdataCapaPago.Tables.Contains("tblVehiculo")) // ERROR: CS1061
                // dataset.Tables.Add(frmCapacidadPago.DsdataCapaPago.Tables["tblVehiculo"].Copy()); // ERROR: CS1061

            // dataset.Tables.Add(frmCapacidadPago.DsdataCapaPago.Tables["DsdataCapa"].Copy()); // ERROR: CS1061
        }

        // =====================================================================
        // VB líneas 4896-4900  CargaVentanaSugeridos
        // =====================================================================
        public void CargaVentanaSugeridos(OdbcConnection myconnect, System.Windows.Forms.Form forma)
        {
            // frmsugeridos FrmSugeridos = new frmsugeridos(myconnect); // ERROR: CS0246
            // FrmSugeridos.StartPosition = FormStartPosition.CenterParent; // ERROR: CS0103
            // FrmSugeridos.Show(forma); // ERROR: CS0103
        }

        // =====================================================================
        // VB líneas 4902-4923  CargaVentanaCptosAdicionales
        // =====================================================================
        public void CargaVentanaCptosAdicionales(string codigoter, int periodo, OdbcConnection myconnect, System.Windows.Forms.Form forma,
            ref int LineaAdi, ref double NumeroAdi, ref double ValorAdi)
        {
            // frmcptoadicionales FrmAdicionales = new frmcptoadicionales(myconnect); // ERROR: CS0246
            // FrmAdicionales.codigoter = Strings.Right("00000000000000" + codigoter, 14); // ERROR: CS0103
            // FrmAdicionales.periodo = periodo; // ERROR: CS0103
            // FrmAdicionales.StartPosition = FormStartPosition.CenterParent; // ERROR: CS0103

            // if (FrmAdicionales.ShowDialog(forma) == DialogResult.Yes) // ERROR: CS0103
            {
                // LineaAdi = Convert.ToInt32(FrmAdicionales.TxtLinea.Text); // ERROR: CS0103
                // NumeroAdi = Convert.ToDouble(FrmAdicionales.TxtNumero.Text); // ERROR: CS0103
                // ValorAdi = Convert.ToDouble(FrmAdicionales.TxtValor.Text); // ERROR: CS0103
            }
            // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
            {
                LineaAdi = 0;
                NumeroAdi = 0;
                ValorAdi = 0;
            }
        }

        // =====================================================================
        // VB líneas 4925-4936  BuscarTotalSolicitudesAprobadas
        // =====================================================================
        public double BuscarTotalSolicitudesAprobadas(DateTime fechafin, OdbcConnection myconnect)
        {
            double vlraprobado = 0;
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            StBuilder.Append("select sum(VALOR_APROBADO) as campo1 ");
            StBuilder.Append("from cop_solcre ");
            StBuilder.Append("where FECHA_APROBA <='" + fechafin.ToString(varini.PstForFec) + "' and estado in ('A','D')");
            string f1 = "0";
            this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "BuscarTotalSolicitudesAprobadas", ref f1);
            vlraprobado = Convert.ToDouble(f1);
            return vlraprobado;
        }

        // =====================================================================
        // VB líneas 4938-4944  BuscarDeducciones
        // =====================================================================
        public virtual DataSet BuscarDeducciones(int NumSolicitud, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select a.codigoter as cedula,a.lincred,a.nume_cred as numero,b.descripcion,a.valor_pago as valor, a.inte_adicional as interes," +
                      " totpar as total,(a.valor_pago+a.inte_adicional) as TotalDeducir from cop_solrecr a inner join cop_concar12 b on a.lincred=b.lincred where numero = " + NumSolicitud;
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarDeducciones", ref dsdata, "tbldeducciones");
            return dsdata;
        }

        // =====================================================================
        // VB líneas 4946-4969  BuscaSolicitudesCredito
        // =====================================================================
        public virtual DataTable BuscaSolicitudesCredito(string NroSolcitud, OdbcConnection Myconnect)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder(); DataSet DsDataset = new DataSet();
            try
            {
                stbuilder.Append("select solcre.numero,solcre.fecha_soli,solcre.codigoter,solcre.lincred,solcre.vlr_solicitud,solcre.tasa_int,solcre.plazo,solcre.cuota,solcre.pagos_mes,solcre.sal_mora,solcre.cupo_disponible,solcre.fec_ingr_coop,solcre.empresa,solcre.cargo,solcre.salario,solcre.otro_ingreso,solcre.fec_ingr_empr,solcre.dscto_mes_emp,solcre.gasto_fijo_mes,solcre.cupo_dismes");
                stbuilder.Append(",solcre.tipo_contrato,solcre.tipo_garantia,solcre.descripcion,solcre.avaluo_ccial,solcre.avaluo_catastro,solcre.asegurado, solcre.por_seguro,solcre.fecven_seguro,solcre.codeudor1,solcre.codeudor2,solcre.codeudor3,solcre.codeudor4,solcre.valor_aprobado,solcre.fecha_aproba,solcre.numero_acta,solcre.fecha_acta,solcre.fecha_programada,");
                stbuilder.Append("solcre.aporte_adicional,solcre.deuda_recoge,solcre.estado,solcre.usuario,solcre.fecha_graba,solcre.conyu_labora,solcre.conyuge,solcre.empresa_labora,solcre.salario_me,solcre.tel_conyuge,solcre.dir_emp_conyu,solcre.ciudad_emp_conyu,solcre.per_acargo_conyu,solcre.observacion,solcre.vehiculo,solcre.tiene_vehiculo,solcre.casa_propia,solcre.fecdesc");
                stbuilder.Append(",solcre.nit,solcre.ciclod,solcre.periodd,solcre.clacuo,solcre.clasei,solcre.clades,solcre.tip_intcie,solcre.tip_cap,solcre.tip_adm,solcre.tip_seg,solcre.tip_otr,solcre.for_adm,solcre.cpto_adm,solcre.cpto_seg,solcre.cpto_otr,solcre.tasaadm,solcre.tasaseg,solcre.tasacpt,solcre.tasaotr,solcre.vlrpre_extra,solcre.aportes,solcre.agencia,solcre.ccosto,solcre.porextra,solcre.cuota_adm,solcre.cuota_seg,solcre.cuota_cptl");
                stbuilder.Append(",solcre.cuota_icie,solcre.cuota_otros,solcre.pergracia,solcre.pergraini,solcre.fepergrini,solcre.pergracuo,solcre.pergradia,solcre.fecha_pergracia,solcre.ciclo_pergracia,solcre.cuex_inmes,solcre.cuex_inant,solcre.pag1cuo,solcre.tippag2,solcre.numpagare,solcre.NumLibranza,solcre.NumCdat,solcre.IngVariables,solcre.IngArriendos,solcre.DeudasTerceros,solcre.Autorizado,");
                stbuilder.Append("solcre.EmpDsto,solcre.NitAseguradora,solcre.NombreAseguradora,solcre.NumPoliza,solcre.Matricula,maenit.apellido,maenit.nombre,solcre.envpagador,solcre.FecEnvPagador,solcre.FecRecPagador,solcre.usuario_autoriza,solcre.tasa_int_sol,solcre.plazo_sol,solcre.periodd_sol,solcre.ciclod_sol,solcre.clades_sol,car12.TOTINTANT,car12.intcie,solcre.cuota_sol,solcre.ente,solcre.dtf,solcre.puntos,solcre.dtf_sol,solcre.puntos_sol ");
                stbuilder.Append("from cop_solcre solcre ");
                stbuilder.Append("inner join sys_maenit maenit on solcre.codigoter = maenit.codigoter ");
                stbuilder.Append("inner join cop_concar12 car12 on solcre.lincred=car12.lincred ");
                stbuilder.Append("where solcre.numero = " + NroSolcitud);

                this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaSolicitudesCredito", ref DsDataset, "tblsolicitudes");
                return DsDataset.Tables["tblsolicitudes"];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString(), "SOlIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return null;
        }

        // =====================================================================
        // VB líneas 4970-5044  ApruebaSolicitud
        // =====================================================================
        public void ApruebaSolicitud(double NumSolicitud, DateTime FechaAprobado, string NumActa, double ValorAprobado, int PlazoAprobado,
            decimal TasaIntAprobada, string CladesAprobada, string PeriodicidadAprobada, string CicloDstoAprobada, string Detalle,
            string ManejaEstudioCredito, string Autorizado, string Pagaduria, DateTime FecEnvPagador, DateTime FecRecPagador, string DevolvioPagador,
            string Usuario, string EstadoActSolicitud, string Ente, OdbcConnection Myconnect, double Cuota, decimal Dtf, decimal Puntos)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string StEstado = ""; string StUsuario = ""; string StFecEnvPagador = ""; string StFecRecPagador = "";

            if (ManejaEstudioCredito == "N")
            {
                StEstado = ",estado = 'A' ";
                StUsuario = ",usuario_autoriza='" + Usuario + "',UsuAutorizo='" + Usuario + "' ";
            }
            else if (ManejaEstudioCredito == "Y")
            {
                if (Autorizado == "Y")
                {
                    StEstado = ",estado = 'D' ";
                    if (EstadoActSolicitud != "A")
                        StUsuario = ",usuario_autoriza='" + Usuario + "',UsuAutorizo='" + Usuario + "' ";
                    else
                        StUsuario = ",UsuAutorizo='" + Usuario + "' ";
                }
                else if (Autorizado == "N")
                {
                    StEstado = ",estado = 'A' ";
                    StUsuario = ",usuario_autoriza='" + Usuario + "' ";
                }
            }

            if (Pagaduria == "Y")
                StFecEnvPagador = ",FecEnvPagador='" + Strings.Format(FecEnvPagador, varini.PstForFec) + "' ";

            if (DevolvioPagador == "Y")
                StFecRecPagador = ",FecRecPagador='" + FecRecPagador.ToString(varini.PstForFec) + "' ";

            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("fecha_aproba = '");
            stbuilder.Append(FechaAprobado.ToString(varini.PstForFec) + "',");
            stbuilder.Append("numero_acta = '");
            stbuilder.Append(NumActa + "', ");
            stbuilder.Append("valor_aprobado = '");
            stbuilder.Append(ValorAprobado + "',");
            stbuilder.Append("plazo = ");
            stbuilder.Append(PlazoAprobado + ",");
            stbuilder.Append("tasa_int = '");
            stbuilder.Append(TasaIntAprobada + "',");
            stbuilder.Append("clades = '");
            stbuilder.Append(CladesAprobada + "',");
            stbuilder.Append("periodd = '");
            stbuilder.Append(PeriodicidadAprobada + "',");
            stbuilder.Append("ciclod = '");
            stbuilder.Append(CicloDstoAprobada + "',");
            stbuilder.Append("Observacion = '");
            stbuilder.Append(Detalle + "',");
            stbuilder.Append("Autorizado = '");
            stbuilder.Append(Autorizado + "',");
            stbuilder.Append("envpagador = '");
            stbuilder.Append(Pagaduria + "',");
            stbuilder.Append("ente = '");
            stbuilder.Append(Ente + "',");
            stbuilder.Append("cuota = ");
            stbuilder.Append(Cuota + ", ");
            stbuilder.Append("dtf = '");
            stbuilder.Append(Dtf + "',");
            stbuilder.Append("puntos = '");
            stbuilder.Append(Puntos + "' ");
            stbuilder.Append(StEstado + StUsuario + StFecEnvPagador + StFecRecPagador);
            stbuilder.Append("where numero = '" + NumSolicitud + "'");

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        // =====================================================================
        // VB líneas 5047-5069  RechazaSolicitud
        // =====================================================================
        public void RechazaSolicitud(double NumSolicitud, string Acta, string Observacion, string Usuario, string Ente,
            string EnvPagaduria, DateTime FecEnvPagaduria, string RecPagaduria, DateTime FecRecPagaduria, OdbcConnection Myconnect)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string StEnvPagaduria = EnvPagaduria == "Y"
                ? ",envpagador='Y',FecEnvPagador='" + FecEnvPagaduria.ToString(varini.PstForFec) + "'"
                : ",envpagador='N'";
            string StRecPagaduria = RecPagaduria == "Y"
                ? ",FecRecPagador='" + FecRecPagaduria.ToString(varini.PstForFec) + "'"
                : "";

            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("estado = 'R', ");
            stbuilder.Append("numero_acta ='");
            stbuilder.Append(Acta + "',");
            stbuilder.Append("OBSERVACION ='");
            stbuilder.Append(Observacion + "', ");
            stbuilder.Append("UsuNovedad = '" + Usuario + "',");
            stbuilder.Append("ente = '" + Ente + "' ");
            stbuilder.Append(StEnvPagaduria + StRecPagaduria);
            stbuilder.Append(" where numero = '" + NumSolicitud + "'");

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        // =====================================================================
        // VB líneas 5071-5092  PendienteSolicitud
        // =====================================================================
        public void PendienteSolicitud(double NumSolicitud, string Estado, string Observacion, string Usuario, OdbcConnection Myconnect,
            string Acta, string Ente, string EnvPagaduria, DateTime FecEnvPagaduria, string RecPagaduria, DateTime FecRecPagaduria)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string StEnvPagaduria = EnvPagaduria == "Y"
                ? ",envpagador='Y',FecEnvPagador='" + FecEnvPagaduria.ToString(varini.PstForFec) + "'"
                : ",envpagador='N'";
            string StRecPagaduria = RecPagaduria == "Y"
                ? ",FecRecPagador='" + FecRecPagaduria.ToString(varini.PstForFec) + "'"
                : "";

            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("estado = '" + Estado + "', ");
            stbuilder.Append("Observacion = '" + Observacion + "', ");
            stbuilder.Append("UsuNovedad = '" + Usuario + "', ");
            stbuilder.Append("numero_acta ='");
            stbuilder.Append(Acta + "',");
            stbuilder.Append("ente = '");
            stbuilder.Append(Ente + "' ");
            stbuilder.Append(StEnvPagaduria + StRecPagaduria);
            stbuilder.Append(" where numero = '" + NumSolicitud + "'");

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        // =====================================================================
        // VB líneas 5094-5113  AnulaSolicitud
        // =====================================================================
        public void AnulaSolicitud(double NumSolicitud, string Usuario, OdbcConnection Myconnect, string Acta, string Ente,
            string EnvPagaduria, DateTime FecEnvPagaduria, string RecPagaduria, DateTime FecRecPagaduria)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            string StEnvPagaduria = EnvPagaduria == "Y"
                ? ",envpagador='Y',FecEnvPagador='" + FecEnvPagaduria.ToString(varini.PstForFec) + "'"
                : ",envpagador='N'";
            string StRecPagaduria = RecPagaduria == "Y"
                ? ",FecRecPagador='" + FecRecPagaduria.ToString(varini.PstForFec) + "'"
                : "";

            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("estado = 'X', ");
            stbuilder.Append("UsuNovedad = '" + Usuario + "',");
            stbuilder.Append("numero_acta ='");
            stbuilder.Append(Acta + "',");
            stbuilder.Append("ente = '" + Ente + "' ");
            stbuilder.Append(StEnvPagaduria + StRecPagaduria);
            stbuilder.Append(" where numero = '" + NumSolicitud + "'");

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        // =====================================================================
        // VB líneas 5115-5126  GrabacionSolicitud
        // =====================================================================
        public void GrabacionSolicitud(double NumSolicitud, DateTime FecGrabacion, DateTime FecDscto, OdbcConnection Myconnect)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("fecha_programada='" + FecGrabacion.ToString(varini.PstForFec) + "',");
            stbuilder.Append("fecdesc='" + FecDscto.ToString(varini.PstForFec) + "' ");
            stbuilder.Append("where numero = '" + NumSolicitud + "'");

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        // =====================================================================
        // VB líneas 5128-5139  SolicitudGrabada
        // =====================================================================
        private void SolicitudGrabada(double NumSolicitud, double VlrDesembolsado, OdbcConnection Myconnect)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("estado = 'G',");
            stbuilder.Append("vlrdesembolsado='" + VlrDesembolsado + "' ");
            stbuilder.Append("where numero = '" + NumSolicitud + "'");

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        // =====================================================================
        // VB líneas 5141-5201  ProcesarCreditoAprobado
        // =====================================================================
        public bool ProcesarCreditoAprobado(double NumSolicitud, double ConseCredito, string comprobante, double NumDomto,
            string usuario, string NitTerceroCheque, System.Windows.Forms.Form myForma, OdbcConnection myconnect)
        {
            DataTable dsdata = new DataTable(); double VlrInteresCierreInicial = 0; double VlrDesembolsado = 0; string Detalle = ""; string ReestrucNovac = "NA";
            ERP.Core.CarteraFinanciera.Reportes.ImpreDoc ImpNota = new ERP.Core.CarteraFinanciera.Reportes.ImpreDoc(usuario);
            DataSet DataPoyeccion = new DataSet(); string Msg = "";

            // VlrInteresCierreInicial = ReimprimirPlandepagos(NumSolicitud, myForma, myconnect, grabar: true, imprime: false, dataProyeccion: ref DataPoyeccion); // ERROR: CS7036
            dsdata = BuscaSolicitudesCredito(NumSolicitud.ToString(), myconnect);
            ok = GrabaDatosCreacionCredito(dsdata, ref ConseCredito, comprobante, NumDomto, usuario, VlrInteresCierreInicial, myconnect, ref Detalle);
            if (ok)
            {
                DataRow row = dsdata.Rows[0];
                // proceso cuotas extras fue movido a GrabaDatosCreacionCredito
                ok = true;
                if (ok)
                {
                    ok = this.ProcesoRecogerCreditos(NumSolicitud, comprobante, NumDomto, Convert.ToDateTime(row["fecha_programada"]), usuario, myForma, myconnect, ConseCredito, ref ReestrucNovac, ref DataPoyeccion);
                    if (ok)
                    {
                        this.ActualizarGarantiasSolicitud(NumSolicitud, ConseCredito, usuario, myconnect);
                        this.ActualizarDatosCredVivienda(NumSolicitud, ConseCredito, myconnect);

                        this.msgcop.GrabaDocumento(comprobante, NumDomto, 0, 0, Convert.ToDateTime(row["fecha_programada"]), Detalle, myconnect);

                        ok = this.ProcesoDesembolsoCredito(comprobante, NumDomto, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, Convert.ToDateTime(row["fecha_programada"]), NitTerceroCheque, usuario, Detalle, ref VlrDesembolsado, myconnect);
                        if (ok)
                        {
                            // this.msgcop.GrabaDocumento(comprobante, NumDomto, 0, 0, Convert.ToDateTime(row["fecha_programada"]), Detalle, myconnect); // ERROR: CS1513

                            // ImprimePlanpagosDirecto(DataPoyeccion, myForma, NumSolicitud); // ERROR: CS1513

                            // try { ImpNota.impre(comprobante, NumDomto, false, "", myconnect); } // ERROR: CS1503
                            // catch (Exception ex) { MessageBox.Show(ex.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information); } // ERROR: CS1073 - orphaned catch

                            this.SolicitudGrabada(NumSolicitud, VlrDesembolsado, myconnect);

                            this.ImprimirLibranzaPagareCarta(NumSolicitud, usuario, myconnect, Convert.ToInt32(row["lincred"]), row["codigoter"].ToString(), (int)ConseCredito);

                            if (ReestrucNovac == "NV")
                                Msg = "con novación";
                            else if (ReestrucNovac == "RT")
                                Msg = "con reestructuración";

                            MessageBox.Show("Proceso de grabación de crédito " + Msg + " termino con éxito", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            ok = true;
                        }
                    }
                }
            }
            return ok;
        }

        // =====================================================================
        // VB líneas 5203-5227  ActualizaFechaRevisionReest
        // =====================================================================
        public void ActualizaFechaRevisionReest(string codigoter, int lincred, double numero, DateTime FechaRevision, DateTime fecha, OdbcConnection myconnect, int opcion = 0)
        {
            string stmysql = ""; string Stmysql2 = "";
            fecha = new DateTime(fecha.Year, fecha.Month, 1);

            switch (opcion)
            {
                case 0:
                    stmysql = "update cop_salmaecar set ultpagorestInicial='" + FechaRevision.ToString(varini.PstForFec) + "',ultpagorestFinal='" + FechaRevision.ToString(varini.PstForFec) + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero + " and periodo=" + fecha.ToString("yyyyMM");
                    break;
                case 1:
                    stmysql = "update cop_salmaecar set ultpagorestFinal='" + FechaRevision.ToString(varini.PstForFec) + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero + " and periodo=" + fecha.ToString("yyyyMM");
                    Stmysql2 = "update cop_salmaecar set ultpagorestInicial='" + FechaRevision.ToString(varini.PstForFec) + "',ultpagorestFinal='" + FechaRevision.ToString(varini.PstForFec) + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero + " and periodo=" + fecha.AddMonths(1).ToString("yyyyMM");
                    break;
            }

            if (stmysql.Trim() != "")
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaFechaRevisionReest(1)");

            if (Stmysql2.Trim() != "")
                this.OdbcConnect.ExecuteQueryconec(Stmysql2, myconnect, "ActualizaFechaRevisionReest(2)");
        }

        // =====================================================================
        // VB líneas 5229-5252  ActualizaCalificacionReest
        // =====================================================================
        public void ActualizaCalificacionReest(string codigoter, int lincred, double numero, string calificacion, DateTime fecha, OdbcConnection myconnect, int opcion = 0)
        {
            string stmysql = ""; string Stmysql2 = "";
            fecha = new DateTime(fecha.Year, fecha.Month, 1);

            switch (opcion)
            {
                case 0:
                    stmysql = "update cop_salmaecar set calfinicial='" + calificacion + "',calffinal='" + calificacion + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero + " and periodo=" + fecha.ToString("yyyyMM");
                    break;
                case 1:
                    stmysql = "update cop_salmaecar set calffinal='" + calificacion + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero + " and periodo=" + fecha.ToString("yyyyMM");
                    Stmysql2 = "update cop_salmaecar set  calfinicial='" + calificacion + "',calffinal='" + calificacion + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero + " and periodo=" + fecha.AddMonths(1).ToString("yyyyMM");
                    break;
            }

            if (stmysql.Trim() != "")
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaCalificacionReest(1)");

            if (Stmysql2.Trim() != "")
            {
                this.OdbcConnect.ExecuteQueryconec(Stmysql2, myconnect, "ActualizaCalificacionReest(2)");
                stmysql = "update sys_maenit set calman='" + calificacion + "' where codigoter='" + codigoter + "'";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaCalificacionReest(3)");
            }
        }

        // =====================================================================
        // VB líneas 5254-5348  ReimprimirPlandepagos
        // =====================================================================
        public double ReimprimirPlandepagos(double NumSolicitud, System.Windows.Forms.Form myForma, OdbcConnection myconnect,
            bool grabar, bool imprime, bool directo,
            OpcionProyeccion OpciProy, ref DataSet dataProyeccion)
        {
            DataSet dsdeduccion = new DataSet(); DataSet dsextras = new DataSet(); DataSet dsproyeccion = new DataSet();
            DataTable dssolicitud = new DataTable(); double NumeroSol = -1; bool PideVal = true;
            DataSet Dsparametros = new DataSet(); double VlrInteresCierre = 0; double VlrAprobado = 0;
            try
            {
                dssolicitud = this.BuscaSolicitudesCredito(NumSolicitud.ToString(), myconnect);
                if (grabar)
                {
                    NumeroSol = NumSolicitud;
                    PideVal = false;
                    this.ReliquidaCreditosRecogidos(NumSolicitud, Convert.ToDateTime(dssolicitud.Rows[0]["fecha_programada"]), myconnect);
                }
                dsdeduccion = this.BuscarDeducciones((int)NumSolicitud, myconnect);
                this.BuscarExtras((int)NumSolicitud, ref dsextras, myconnect);

                DataRow row = dssolicitud.Rows[0];
                // this.msgparcop.BuscaLinea(Convert.ToInt32(row["lincred"]), Dsparametros, myconnect); // ERROR: CS1503, CS1620
                VlrAprobado = Convert.ToDouble(row["valor_aprobado"]);

                if (Dsparametros.Tables["tbllineas"].Rows[0]["intcie"].ToString() == "3")
                {
                    if (Convert.ToDouble(row["VLR_SOLICITUD"]) == VlrAprobado)
                    {
                        VlrInteresCierre = Convert.ToDouble(row["cuota_icie"]);
                        row["valor_aprobado"] = Convert.ToDouble(row["valor_aprobado"]) - Convert.ToDouble(row["cuota_icie"]);
                    }
                }

                if (Dsparametros.Tables["tbllineas"].Rows[0]["sumaga"].ToString() == "2")
                {
                    if (Convert.ToDouble(row["VLR_SOLICITUD"]) == VlrAprobado)
                    {
                        if (Dsparametros.Tables["tbllineas"].Rows[0]["poapen"] is DBNull)
                            row["valor_aprobado"] = Convert.ToDouble(row["valor_aprobado"]) - Convert.ToDouble(row["cuota_seg"]);
                        else
                        {
                            if (Dsparametros.Tables["tbllineas"].Rows[0]["poapen"].ToString() != "7")
                                row["valor_aprobado"] = Convert.ToDouble(row["valor_aprobado"]) - Convert.ToDouble(row["cuota_seg"]);
                        }

                        if (Dsparametros.Tables["tbllineas"].Rows[0]["foradmon"] is DBNull)
                            row["valor_aprobado"] = Convert.ToDouble(row["valor_aprobado"]) - Convert.ToDouble(row["cuota_adm"]);
                        else
                        {
                            if (Dsparametros.Tables["tbllineas"].Rows[0]["foradmon"].ToString() != "7")
                                row["valor_aprobado"] = Convert.ToDouble(row["valor_aprobado"]) - Convert.ToDouble(row["cuota_adm"]);
                        }

                        row["valor_aprobado"] = Convert.ToDouble(row["valor_aprobado"]) - Convert.ToDouble(row["cuota_cptl"]);
                    }
                }
                else
                {
                    if (Dsparametros.Tables["tbllineas"].Rows[0]["claAdmon"].ToString() == "4")
                    {
                        if (Dsparametros.Tables["tbllineas"].Rows[0]["foradmon"].ToString() != "7")
                            row["valor_aprobado"] = Convert.ToDouble(row["valor_aprobado"]) - Convert.ToDouble(row["cuota_adm"]);
                    }
                }

                if (Dsparametros.Tables["tbllineas"].Rows[0]["tipointeres"].ToString() == "1")
                    row["tasa_int"] = row["puntos"];

                DataTable extrasTable = dsextras.Tables["tblextras"];
                DataTable deducTable = dsdeduccion.Tables["tbldeducciones"];
                // dsproyeccion = this.GeneraProyeccion(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1739
                    // Convert.ToDateTime(row["fecha_programada"]), Convert.ToInt32(row["plazo"]), Convert.ToInt32(row["periodd"]), // ERROR: CS1739
                    // Convert.ToInt32(row["clades"]), Convert.ToInt32(row["ciclod"]), Convert.ToDecimal(row["tasa_int"]), // ERROR: CS1739
                    // Convert.ToDateTime(row["fecdesc"]), Convert.ToDouble(row["valor_aprobado"]), // ERROR: CS1739
                    // extrasTable, deducTable, row["pergraini"].ToString(), myconnect, // ERROR: CS1739
                    // numSolicitud: NumeroSol, pideVal: PideVal, opciProy: OpciProy); // ERROR: CS1739
                dataProyeccion = dsproyeccion;

                if (grabar)
                {
                    DataRow rowProy = dsproyeccion.Tables["TbldatosCredito"].Rows[0];
                    this.GrabaIntCierre(NumSolicitud, Convert.ToDouble(rowProy["CuotaIcie"]),
                        Convert.ToDouble(rowProy["Cuota"]), Convert.ToDouble(rowProy["CuotaSeg"]),
                        Convert.ToDouble(rowProy["CuotaAdm"]), Convert.ToDouble(rowProy["CuotaCapital"]), myconnect);
                }

                if (imprime)
                {
                    if (MessageBox.Show("Imprime Plan de pagos? ", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (!grabar)
                        {
                            if (directo)
                                this.ImprimePlanpagosDirecto(dsproyeccion, myForma, NumSolicitud);
                            else
                                this.ImprimePlanpagos(dsproyeccion, myForma, NumSolicitud);
                        }
                    }
                }

                return VlrInteresCierre;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }

        // Overload without ref DataSet (VB Optional ByRef = Nothing)
        public double ReimprimirPlandepagos(double NumSolicitud, System.Windows.Forms.Form myForma, OdbcConnection myconnect,
            bool grabar = true, bool imprime = true, bool directo = false,
            OpcionProyeccion OpciProy = OpcionProyeccion.Linea)
        {
            DataSet ds = null;
            return ReimprimirPlandepagos(NumSolicitud, myForma, myconnect, grabar, imprime, directo, OpciProy, ref ds);
        }

        // =====================================================================
        // VB líneas 5350-5470  ReCalculaDeduciones
        // =====================================================================
        public DataSet ReCalculaDeduciones(double NumSolicitud, System.Windows.Forms.Form myForma, OdbcConnection myconnect,
            bool MuestraVentana = false, DataSet DsSolCredito = null)
        {
            DataSet dsdeduccion = new DataSet(); DataSet dsextras = new DataSet(); DataSet dsproyeccion = new DataSet();
            DataTable dssolicitud = new DataTable(); bool CicloaFecha = false;
            DataSet Dsparametros = new DataSet(); double VlrInteresCierre = 0; DateTime FechaSolicitud = DateTime.MinValue; double VlrSolicitud = 0;
            double VlrCredito = 0; double cuotaIcie = 0; double CuotaAdm = 0; double CuotaSeg = 0; double CuotaCptl = 0;
            string Cedula; int Linea; int plazo; int Periodicidad; int clades; int ciclod; decimal tasainteres;
            DateTime fecha_descuento = DateTime.MinValue; string clacuo; double ValorSolicitud = 0; int fila = 0; string MesGracias = "0";

            // if (NumSolicitud > 0) // ERROR: CS1026, CS1519, CS8124
            {
                // dsdeduccion = this.BuscarDeducciones((int)NumSolicitud, myconnect); // ERROR: CS1001, CS1519, CS8124
                // this.BuscarExtras((int)NumSolicitud, ref dsextras, myconnect); // ERROR: CS1001, CS1519, CS8124
                // dssolicitud = this.BuscaSolicitudesCredito(NumSolicitud.ToString(), myconnect); // ERROR: CS1001, CS1003, CS1519, CS8124
                DataRow row = dssolicitud.Rows[0];
                // VlrSolicitud = Convert.ToDouble(row["VLR_SOLICITUD"]); // ERROR: CS1519, CS8124
                // VlrCredito = Convert.ToDouble(row["valor_aprobado"]); // ERROR: CS1519, CS8124
                // cuotaIcie = Convert.ToDouble(row["cuota_icie"]); // ERROR: CS1519, CS8124
                // CuotaAdm = Convert.ToDouble(row["cuota_adm"]); // ERROR: CS1519, CS8124
                // CuotaSeg = Convert.ToDouble(row["cuota_seg"]); // ERROR: CS1519, CS8124
                // CuotaCptl = Convert.ToDouble(row["cuota_cptl"]); // ERROR: CS1519, CS8124
                // Cedula = row["codigoter"].ToString(); // ERROR: CS1519
                // Linea = Convert.ToInt32(row["lincred"]); // ERROR: CS1519, CS8124
                // plazo = Convert.ToInt32(row["plazo"]); // ERROR: CS1519, CS8124
                // Periodicidad = Convert.ToInt32(row["periodd"]); // ERROR: CS1519, CS8124
                // clades = Convert.ToInt32(row["clades"]); // ERROR: CS1519, CS8124
                // ciclod = Convert.ToInt32(row["ciclod"]); // ERROR: CS1519, CS8124
                // tasainteres = Convert.ToDecimal(row["tasa_int"]); // ERROR: CS1519, CS8124
                // fecha_descuento = Convert.ToDateTime(row["fecdesc"]); // ERROR: CS1519, CS8124
                // clacuo = row["clacuo"].ToString(); // ERROR: CS1519
                // MesGracias = row["PERGRAINI"].ToString(); // ERROR: CS1519

                // if (row["fecha_programada"] is DBNull) // ERROR: CS1026, CS1519, CS8124
                    // FechaSolicitud = Convert.ToDateTime(row["fecha_soli"]); // ERROR: CS1026, CS1519, CS8124
                // else // ERROR: CS1519
                    // FechaSolicitud = Convert.ToDateTime(row["fecha_programada"]); // ERROR: CS1519, CS8124

                // if (Convert.ToDouble(row["valor_aprobado"]) != 0) // ERROR: CS1026, CS1519, CS8124
                    // ValorSolicitud = VlrCredito; // ERROR: CS1026, CS1519, CS8124
                // else // ERROR: CS1519
                {
                    // ValorSolicitud = VlrSolicitud; // ERROR: CS1519
                    // VlrCredito = VlrSolicitud; // ERROR: CS1519
                }
            }
            // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
            {
                // dssolicitud = DsSolCredito.Tables["TbldatosCredito"].Copy(); // ERROR: CS0116, CS1022
                // dsextras.Tables.Add(DsSolCredito.Tables["tblextras"].Copy()); // ERROR: CS0116, CS1002, CS1022, CS1026, CS8124
                // dsdeduccion.Tables.Add(DsSolCredito.Tables["tbldeducciones"].Copy()); // ERROR: CS0116, CS1002, CS1022, CS1026, CS8124

                // for (fila = dsdeduccion.Tables["tbldeducciones"].Rows.Count - 1; fila >= 0; fila += -1) // ERROR: CS0116, CS1022, CS1026, CS8124
                {
                    DataRow rowD = dsdeduccion.Tables["tbldeducciones"].Rows[fila];
                    // if (Convert.ToDouble(rowD["numero"]) == 99999999) // ERROR: CS1022, CS1026, CS8124
                        // dsdeduccion.Tables["tbldeducciones"].Rows.RemoveAt(fila); // ERROR: CS1022, CS1026, CS8124
                }

                DataRow row = dssolicitud.Rows[0];
                // VlrSolicitud = Convert.ToDouble(row["valorCredito"]); // ERROR: CS8803
                VlrCredito = Convert.ToDouble(row["valorCredito"]);
                cuotaIcie = Convert.ToDouble(row["CuotaIcie"]);
                CuotaAdm = Convert.ToDouble(row["CuotaAdm"]);
                CuotaSeg = Convert.ToDouble(row["CuotaSeg"]);
                CuotaCptl = Convert.ToDouble(row["CuotaCapital"]);
                Cedula = row["Cedula"].ToString();
                Linea = Convert.ToInt32(row["lincred"]);
                plazo = Convert.ToInt32(row["plazo"]);
                Periodicidad = Convert.ToInt32(row["periodicidad"]);
                clades = Convert.ToInt32(row["clades"]);
                ciclod = Convert.ToInt32(row["ciclo"]);
                tasainteres = Convert.ToDecimal(row["TasaInt"]);
                fecha_descuento = Convert.ToDateTime(row["FecDesto"]);
                clacuo = row["clacuo"].ToString();
                FechaSolicitud = Convert.ToDateTime(row["fecha"]);
                ValorSolicitud = VlrSolicitud;
                MesGracias = row["MesGracias"].ToString();
            }

            // this.msgparcop.BuscaLinea(Linea, Dsparametros, myconnect); // ERROR: CS1503, CS1620

            if (Dsparametros.Tables["tbllineas"].Rows[0]["intcie"].ToString() == "3")
            {
                if (VlrSolicitud == VlrCredito)
                {
                    VlrInteresCierre = Convert.ToDouble(cuotaIcie);
                    ValorSolicitud = Convert.ToDouble(ValorSolicitud) - Convert.ToDouble(cuotaIcie);
                }
            }

            if (Dsparametros.Tables["tbllineas"].Rows[0]["sumaga"].ToString() == "2")
            {
                if (VlrSolicitud == VlrCredito)
                    ValorSolicitud = Convert.ToDouble(ValorSolicitud) - (Convert.ToDouble(CuotaAdm) + Convert.ToDouble(CuotaSeg) + Convert.ToDouble(CuotaCptl));
            }
            else
            {
                if (Dsparametros.Tables["tbllineas"].Rows[0]["claAdmon"].ToString() == "4")
                {
                    if (Dsparametros.Tables["tbllineas"].Rows[0]["foradmon"].ToString() != "7")
                    {
                        if (VlrSolicitud == VlrCredito)
                            ValorSolicitud = Convert.ToDouble(ValorSolicitud) - Convert.ToDouble(CuotaAdm);
                    }
                }
            }

            // dsproyeccion = this.GeneraProyeccion(Cedula, Linea, FechaSolicitud, plazo, Periodicidad, // ERROR: CS1739
                // clades, ciclod, tasainteres, fecha_descuento, ValorSolicitud, // ERROR: CS1739
                // dsextras.Tables["tblextras"], dsdeduccion.Tables["tbldeducciones"], MesGracias, myconnect, // ERROR: CS1739
                // clacuo: Convert.ToInt32(clacuo)); // ERROR: CS1739

            if (MuestraVentana)
            {
                // this.CargaDeduciones(dsproyeccion, myForma, myconnect, -1); // ERROR: CS1503
            }

            return dsproyeccion;
        }

        // =====================================================================
        // VB líneas 5494-5709  GrabaDatosCreacionCredito
        // (comentado bloque MostrarCicloaFecha VB 5472-5492 omitido por ser comentario)
        // =====================================================================
        private bool GrabaDatosCreacionCredito(DataTable dsdata, ref double ConseCredito, string comprobante,
            double NumDomto, string usuario, double VlrInteresCierreInicial, OdbcConnection myconnect, ref string Detalle)
        {
            DateTime fecvence = DateTime.MinValue; double CargosAdicionales = 0; string DetalleRegistro = ""; int LineaCptalizacion = 1; double InteresCierre = 0;
            DataSet dscompania = new DataSet(); DataSet dslinea = new DataSet(); string Transaccion = "02"; string CtaMovtos = "999999999999"; double IntProporcional = 0;
            bool Okk = false; string StForPagare = "99"; double StNumPagare = 0; bool AsignoPagare = false; DateTime DtFecDescuento = new DateTime(1950, 1, 1);
            int diasper = 0; DateTime FecProporcionales = new DateTime(1950, 1, 1); bool ActFecProp = false;

            this.msgparsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);

            Transaccion = dscompania.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"].ToString();
            StForPagare = dscompania.Tables["tblcompania"].Rows[0]["formapagare"].ToString();

            DataRow row = dsdata.Rows[0];
            // this.msgparcop.BuscaLinea(Convert.ToInt32(row["lincred"]), dslinea, myconnect); // ERROR: CS1503, CS1620
            // fecvence = this.msgcop.CalculaFechaVence(Convert.ToDouble(row["numero"]), Convert.ToDouble(row["cuota"]), // ERROR: CS1503
                // Convert.ToDateTime(row["fecdesc"]), Convert.ToDouble(row["plazo"]), myconnect, // ERROR: CS1503
                // Convert.ToInt32(row["periodd"]), Convert.ToInt32(row["ciclod"])); // ERROR: CS1503

            if (row["intcie"].ToString() == "3")
                row["valor_aprobado"] = (Convert.ToDouble(row["valor_aprobado"]) - VlrInteresCierreInicial) + Convert.ToDouble(row["cuota_icie"]);

            if (row["tip_otr"].ToString() == "2")
                CargosAdicionales = Convert.ToDouble(row["cuota_otros"]);

            if (row["pergraini"] is DBNull)
                row["pergraini"] = 0;

            if (row["fecha_pergracia"] is DBNull)
                row["fecha_pergracia"] = new DateTime(1950, 1, 1);

            string obsText = row["observacion"].ToString().Trim();
            if (obsText != "")
            {
                if (obsText.Length > 80)
                {
                    DetalleRegistro = "PRESTAMO " + dslinea.Tables["tbllineas"].Rows[0]["descripcion"].ToString() + " " + row["lincred"].ToString() + "-" + ConseCredito + " GRABADO DESDE APROBACION DE CREDITOS";
                    DetalleRegistro = DetalleRegistro.Substring(0, Math.Min(80, DetalleRegistro.Length));
                }
                else
                    DetalleRegistro = obsText.Substring(0, Math.Min(80, obsText.Length));
            }
            else
            {
                DetalleRegistro = "PRESTAMO " + dslinea.Tables["tbllineas"].Rows[0]["descripcion"].ToString() + " " + row["lincred"].ToString() + "-" + ConseCredito + " GRABADO DESDE APROBACION DE CREDITOS";
                DetalleRegistro = DetalleRegistro.Substring(0, Math.Min(80, DetalleRegistro.Length));
            }

            Detalle = DetalleRegistro;

            if (dscompania.Tables["tblcompania"].Rows[0]["CtrlConse"].ToString() == "3")
            {
                if (StForPagare == "0" || StForPagare == "2")
                    StNumPagare = Convert.ToDouble(row["NUMPAGARE"]);
                else if (StForPagare == "1")
                {
                    StNumPagare = this.BuscaConsePagare(myconnect);
                    AsignoPagare = true;
                }
                ConseCredito = StNumPagare;
            }

            // Okk = this.msgcop.GrabaNuevoCredito(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, // ERROR: CS1739
                // Convert.ToDateTime(row["fecha_soli"]), Convert.ToDateTime(row["fecha_aproba"]), Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                // Convert.ToDateTime(row["fecdesc"]), row["nit"].ToString(), // ERROR: CS1739
                // Convert.ToInt32(row["plazo"]), Convert.ToDouble(row["vlr_solicitud"]), Convert.ToDouble(row["valor_aprobado"]), // ERROR: CS1739
                // Convert.ToDouble(row["valor_aprobado"]), Convert.ToDouble(row["cuota"]), Convert.ToDecimal(row["tasa_int"]), // ERROR: CS1739
                // Convert.ToInt32(row["ciclod"]), Convert.ToInt32(row["periodd"]), // ERROR: CS1739
                // Convert.ToInt32(row["clacuo"]), Convert.ToInt32(row["clasei"]), // ERROR: CS1739
                // usuario, DateTime.Now, row["agencia"].ToString(), row["ccosto"].ToString(), // ERROR: CS1739
                // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), myconnect, // ERROR: CS1739
                // row["TIPO_GARANTIA"].ToString(), Convert.ToInt32(row["clades"]), // ERROR: CS1739
                // Convert.ToDecimal(row["tasaadm"]), Convert.ToDecimal(row["tasaseg"]), // ERROR: CS1739
                // CargosAdicionales: CargosAdicionales, // ERROR: CS1739
                // vlrpreExtra: Convert.ToDouble(row["vlrpre_extra"]), // ERROR: CS1739
                // pergraini: Convert.ToInt32(row["pergraini"]), // ERROR: CS1739
                // fechaPergracia: Convert.ToDateTime(row["fecha_pergracia"]), // ERROR: CS1739
                // pergracia: Convert.ToInt32(row["pergracia"]), // ERROR: CS1739
                // cicloPergracia: Convert.ToInt32(row["ciclo_pergracia"]), // ERROR: CS1739
                // codeudor1: row["codeudor1"].ToString(), codeudor2: row["codeudor2"].ToString(), // ERROR: CS1739
                // codeudor3: row["codeudor3"].ToString(), codeudor4: row["codeudor4"].ToString(), // ERROR: CS1739
                // cuotaAdm: Convert.ToDouble(row["cuota_adm"]), cuotaSeg: Convert.ToDouble(row["cuota_seg"]), // ERROR: CS1739
                // cuotaCptl: Convert.ToDouble(row["cuota_cptl"]), cuotaIcie: Convert.ToDouble(row["cuota_icie"]), // ERROR: CS1739
                // cuotaOtros: Convert.ToDouble(row["cuota_otros"]), // ERROR: CS1739
                // numero: Convert.ToDouble(row["numero"]), // ERROR: CS1739
                // cuexInmes: row["cuex_inmes"].ToString(), cuexInant: row["cuex_inant"].ToString(), // ERROR: CS1739
                // pag1cuo: row["pag1cuo"].ToString(), tippag2: row["tippag2"].ToString(), // ERROR: CS1739
                // fecvence: fecvence, empdsto: row["empdsto"].ToString(), // ERROR: CS1739
                // tipSeg: row["tip_seg"].ToString(), // ERROR: CS1739
                // totintant: row["totintant"].ToString(), // ERROR: CS1739
                // numpagare: Convert.ToDouble(row["NUMPAGARE"])); // ERROR: CS1739

            if (Okk)
            {
                // Proceso cuotas extras
                this.ProcesoCuotasExtrasCredito(Convert.ToDouble(row["numero"]), row["codigoter"].ToString(),
                    Convert.ToInt32(row["lincred"]), ConseCredito,
                    Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), myconnect);

                if (dslinea.Tables["tbllineas"].Rows[0]["tipointeres"].ToString() == "1")
                {
                    this.stmysql = "update cop_maecar set dtf='" + row["dtf"].ToString() + "',puntos='" + row["puntos"].ToString() + "' where codigoter='" + row["codigoter"].ToString() + "' and lincred=" + row["lincred"].ToString() + " and numero=" + ConseCredito;
                    this.OdbcConnect.ExecuteQueryconec(this.stmysql, myconnect, "GrabaDatosCreacionCredito(Act. Dtf y puntos)");
                }

                DtFecDescuento = Convert.ToDateTime(row["fecdesc"]);

                if (StForPagare == "1")
                {
                    if (!AsignoPagare)
                        StNumPagare = this.BuscaConsePagare(myconnect);
                    AsignaPagareObligacion(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, StNumPagare, myconnect);
                }

                // this.msgcop.GrabaMovimiento(comprobante, NumDomto, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, // ERROR: CS1739
                    // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), Transaccion, Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                    // Convert.ToDouble(row["valor_aprobado"]), 0, // ERROR: CS1739
                    // DetalleRegistro, usuario, myconnect, cuenta: dslinea.Tables["tbllineas"].Rows[0]["cuenta"].ToString(), // ERROR: CS1739
                    // nit: row["nit"].ToString(), codigoterMovto: row["codigoter"].ToString(), // ERROR: CS1739
                    // centralCosto: dslinea.Tables["tbllineas"].Rows[0]["centroco"].ToString(), // ERROR: CS1739
                    // empdsto: row["empdsto"].ToString(), numSolicitud: Convert.ToDouble(row["numero"])); // ERROR: CS1739

                // Seguro
                if (Convert.ToDouble(row["cuota_seg"]) > 0)
                {
                    if (row["tip_seg"].ToString() == "4")
                        this.CargarRegistrosConceptos("S", dsdata, ConseCredito, comprobante, NumDomto, usuario, myconnect);
                    else if (row["tip_seg"].ToString() == "1" || row["tip_seg"].ToString() == "2" || row["tip_seg"].ToString() == "5" || row["tip_seg"].ToString() == "6" || row["tip_seg"].ToString() == "8")
                    {
                        Transaccion = dscompania.Tables["tblcompania"].Rows[0]["cpto_seguro"].ToString();
                        // this.msgcop.GrabaMovimiento(comprobante, NumDomto, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, // ERROR: CS1739
                            // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), Transaccion, Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                            // 0, Convert.ToDouble(row["cuota_seg"]), // ERROR: CS1739
                            // DetalleRegistro, usuario, myconnect, nit: row["nit"].ToString(), // ERROR: CS1739
                            // codigoterMovto: row["codigoter"].ToString(), // ERROR: CS1739
                            // centralCosto: dslinea.Tables["tbllineas"].Rows[0]["centroco"].ToString(), // ERROR: CS1739
                            // empdsto: row["empdsto"].ToString()); // ERROR: CS1739
                    }
                }

                // Administración
                if (Convert.ToDouble(row["cuota_adm"]) > 0)
                {
                    if (row["tip_adm"].ToString() == "3")
                        this.CargarRegistrosConceptos("A", dsdata, ConseCredito, comprobante, NumDomto, usuario, myconnect);
                    else if (row["tip_adm"].ToString() == "1" || row["tip_adm"].ToString() == "4" || row["tip_adm"].ToString() == "5")
                    {
                        Transaccion = dscompania.Tables["tblcompania"].Rows[0]["cpto_admon"].ToString();
                        // this.msgcop.GrabaMovimiento(comprobante, NumDomto, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, // ERROR: CS1739
                            // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), Transaccion, Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                            // 0, Convert.ToDouble(row["cuota_adm"]), // ERROR: CS1739
                            // DetalleRegistro, usuario, myconnect, nit: row["nit"].ToString(), // ERROR: CS1739
                            // codigoterMovto: row["codigoter"].ToString(), // ERROR: CS1739
                            // centralCosto: dslinea.Tables["tbllineas"].Rows[0]["centroco"].ToString(), // ERROR: CS1739
                            // empdsto: row["empdsto"].ToString()); // ERROR: CS1739
                    }
                }

                // Cargos adicionales tipo otro
                if (row["tip_otr"].ToString() == "3")
                    this.CargarRegistrosConceptos("O", dsdata, ConseCredito, comprobante, NumDomto, usuario, myconnect);

                // Capitalización
                if (Convert.ToDouble(row["cuota_cptl"]) > 0)
                {
                    LineaCptalizacion = Convert.ToInt32(dslinea.Tables["tbllineas"].Rows[0]["CptoCapitalizacion"]);
                    ok = this.msgcop.BuscaObligacion(row["codigoter"].ToString(), LineaCptalizacion, 0, myconnect);
                    if (!ok)
                    {
                        if (MessageBox.Show("Asociado no tiene el concepto de capitalizacion creado. " + "\r\n" +
                                            "Presione Si para crear el concepto " + LineaCptalizacion + " - 0. " + "\r\n" +
                                            "Presione No para enviar la capitalizacion por el concepto 01",
                                            "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
                            LineaCptalizacion = 1;
                        else
                        {
                            // this.msgparcop.BuscaLinea(LineaCptalizacion, dslinea, myconnect); // ERROR: CS1503, CS1620
                            if (dslinea.Tables["tbllineas"].Rows.Count <= 0)
                                LineaCptalizacion = 1;
                        }
                    }
                    this.msgcop.BuscaLineaTipoMovto(LineaCptalizacion, myconnect, ref Transaccion);
                    // this.msgcop.GrabaMovimiento(comprobante, NumDomto, row["codigoter"].ToString(), LineaCptalizacion, 0, // ERROR: CS1739
                        // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), Transaccion, Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                        // 0, Convert.ToDouble(row["cuota_cptl"]), // ERROR: CS1739
                        // DetalleRegistro, usuario, myconnect, nit: row["nit"].ToString(), // ERROR: CS1739
                        // codigoterMovto: row["codigoter"].ToString(), // ERROR: CS1739
                        // centralCosto: dslinea.Tables["tbllineas"].Rows[0]["centroco"].ToString(), // ERROR: CS1739
                        // empdsto: row["empdsto"].ToString()); // ERROR: CS1739
                }

                // this.msgparcop.BuscaLinea(Convert.ToInt32(row["lincred"]), dslinea, myconnect); // ERROR: CS1503, CS1620
                InteresCierre = Convert.ToDouble(row["cuota_icie"]);

                if (InteresCierre > 0)
                {
                    if (dslinea.Tables["tbllineas"].Rows[0]["totintant"].ToString() == "Y")
                    {
                        IntProporcional = this.CalculaIntProporcionales(
                            Convert.ToDateTime(row["fecha_programada"]),
                            Convert.ToDateTime(row["fecdesc"]),
                            Convert.ToDouble(row["valor_aprobado"]),
                            Convert.ToDecimal(row["tasa_int"]),
                            Convert.ToInt32(row["periodd"]));
                        if (IntProporcional < 0) IntProporcional = 0;
                        InteresCierre -= IntProporcional;
                        Transaccion = dscompania.Tables["tblcompania"].Rows[0]["cpto_capiatra"].ToString();

                        // this.msgcop.GrabaMovimiento(comprobante, NumDomto, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, // ERROR: CS1739
                            // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), Transaccion, Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                            // 0, InteresCierre, // ERROR: CS1739
                            // DetalleRegistro, usuario, myconnect, nit: row["nit"].ToString(), // ERROR: CS1739
                            // codigoterMovto: row["codigoter"].ToString(), // ERROR: CS1739
                            // centralCosto: dslinea.Tables["tbllineas"].Rows[0]["centroco"].ToString(), // ERROR: CS1739
                            // empdsto: row["empdsto"].ToString()); // ERROR: CS1739
                        InteresCierre = IntProporcional;
                    }
                    if (row["TIP_INTCIE"].ToString() == "2" || row["TIP_INTCIE"].ToString() == "3")
                    {
                        Transaccion = dscompania.Tables["tblcompania"].Rows[0]["cpto_inteatra"].ToString();
                        // this.msgcop.GrabaMovimiento(comprobante, NumDomto, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), ConseCredito, // ERROR: CS1739
                            // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), Transaccion, Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                            // 0, InteresCierre, // ERROR: CS1739
                            // DetalleRegistro, usuario, myconnect, nit: row["nit"].ToString(), // ERROR: CS1739
                            // codigoterMovto: row["codigoter"].ToString(), // ERROR: CS1739
                            // centralCosto: dslinea.Tables["tbllineas"].Rows[0]["centroco"].ToString(), // ERROR: CS1739
                            // empdsto: row["empdsto"].ToString()); // ERROR: CS1739
                        ActFecProp = true;
                    }
                }
                else
                {
                    if (row["TIP_INTCIE"].ToString() == "9")
                        ActFecProp = true;
                }

                if (ActFecProp)
                {
                    // diasper = this.DiasPeriodicidad(Convert.ToInt32(row["periodd"])); // ERROR: CS0266
                    if (DtFecDescuento.Month == 2)
                    {
                        if (DtFecDescuento.Day >= 28)
                            DtFecDescuento = DtFecDescuento.AddDays(2);
                    }
                    else
                    {
                        if (DtFecDescuento.Day == 31)
                            DtFecDescuento = DtFecDescuento.AddDays(-1);
                    }
                    FecProporcionales = DtFecDescuento.AddDays(-diasper);

                    this.stmysql = "update cop_maecar set fecintprop='" + FecProporcionales.ToString(varini.PstForFec) + "' where codigoter='" + row["codigoter"].ToString() + "' and lincred=" + row["lincred"].ToString() + " and numero=" + ConseCredito;
                    this.OdbcConnect.ExecuteQueryconec(this.stmysql, myconnect, "GrabaDatosCreacionCredito(Act. FechaProporcionales)");
                }

                // Actualiza cuota/tasa/clades (VB línea 5705-5706)
                // msgcop.ActualizarCuaotaSalmaecar(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1503
                    // ConseCredito, Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), myconnect); // ERROR: CS1503
            }

            return Okk;
        }

        // =====================================================================
        // VB líneas 5711-5781  CargarRegistrosConceptos
        // =====================================================================
        private void CargarRegistrosConceptos(string TipoCpto, DataTable dsdata, double ConseCredito, string comprobante,
            double NumDomto, string usuario, OdbcConnection myconnect)
        {
            double Cuota = 0; int linea = 0; double VlrConcepto = 0; int NumCuotas = 0; string Transaccion = "02";
            double PeriodoDsto = 0.0; DataSet dscompania = new DataSet(); DataSet dslinea = new DataSet();
            DateTime fecvence = new DateTime(1950, 1, 1); string DetalleRegistro = "";

            DataRow row = dsdata.Rows[0];
            switch (TipoCpto)
            {
                case "S":
                    linea = Convert.ToInt32(row["CPTO_SEG"]);
                    VlrConcepto = Convert.ToDouble(row["cuota_seg"]);
                    DetalleRegistro = "Concepto de seguro del crédito No. " + row["lincred"].ToString() + "-" + ConseCredito;
                    break;
                case "A":
                    linea = Convert.ToInt32(row["CPTO_ADM"]);
                    VlrConcepto = Convert.ToDouble(row["cuota_adm"]);
                    DetalleRegistro = "Concepto de administración del crédito No. " + row["lincred"].ToString() + "-" + ConseCredito;
                    break;
                case "O":
                    linea = Convert.ToInt32(row["CPTO_OTR"]);
                    VlrConcepto = Convert.ToDouble(row["cuota_otros"]);
                    DetalleRegistro = "Concepto cargos adicionales del crédito No. " + row["lincred"].ToString() + "-" + ConseCredito;
                    break;
                default:
                    return;
            }

            if (row["ciclod"].ToString() != "5")
                PeriodoDsto = 1;
            else
            {
                switch (Convert.ToInt32(row["periodd"]))
                {
                    case (int)Periodicidad.Diario:
                        PeriodoDsto = Math.Round((365.0 / 12), 8);
                        break;
                    case (int)Periodicidad.Semanal:
                        PeriodoDsto = Math.Round((52.0 / 12), 8);
                        break;
                    default:
                        PeriodoDsto = Convert.ToDouble(row["periodd"]);
                        break;
                }
            }

            NumCuotas = Convert.ToInt32(Convert.ToDouble(row["plazo"]) * PeriodoDsto);

            if (NumCuotas != 0)
                Cuota = Math.Round((VlrConcepto / NumCuotas), 0);
            else
                Cuota = VlrConcepto;

            // this.msgparcop.BuscaLinea(linea, dslinea, myconnect); // ERROR: CS1503, CS1620

            if (linea >= 1000)
                // fecvence = this.msgcop.CalculaFechaVence(Convert.ToDouble(row["numero"]), Convert.ToDouble(row["cuota"]), // ERROR: CS1503
                    // Convert.ToDateTime(row["fecdesc"]), Convert.ToDouble(row["plazo"]), myconnect, // ERROR: CS1503
                    // Convert.ToInt32(row["periodd"]), Convert.ToInt32(row["ciclod"])); // ERROR: CS1503

            // ok = this.msgcop.GrabaNuevoCredito(row["codigoter"].ToString(), linea, ConseCredito, // ERROR: CS1739
                // Convert.ToDateTime(row["fecha_soli"]), Convert.ToDateTime(row["fecha_aproba"]), // ERROR: CS1739
                // Convert.ToDateTime(row["fecha_programada"]), Convert.ToDateTime(row["fecdesc"]), // ERROR: CS1739
                // row["nit"].ToString(), Convert.ToInt32(row["plazo"]), VlrConcepto, VlrConcepto, VlrConcepto, // ERROR: CS1739
                // Cuota, Convert.ToDecimal(row["tasa_int"]), Convert.ToInt32(row["ciclod"]), Convert.ToInt32(row["periodd"]), // ERROR: CS1739
                // Convert.ToInt32(row["clacuo"]), Convert.ToInt32(row["clasei"]), // ERROR: CS1739
                // usuario, DateTime.Now, row["agencia"].ToString(), row["ccosto"].ToString(), // ERROR: CS1739
                // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), myconnect, // ERROR: CS1739
                // row["TIPO_GARANTIA"].ToString(), Convert.ToInt32(row["clades"]), // ERROR: CS1739
                // codeudor1: row["codeudor1"].ToString(), codeudor2: row["codeudor2"].ToString(), // ERROR: CS1739
                // codeudor3: row["codeudor3"].ToString(), codeudor4: row["codeudor4"].ToString(), // ERROR: CS1739
                // numero: Convert.ToDouble(row["numero"]), fecvence: fecvence, // ERROR: CS1739
                // empdsto: row["empdsto"].ToString()); // ERROR: CS1739

            if (ok)
            {
                this.msgcop.BuscaLineaTipoMovto(linea, myconnect, ref Transaccion);
                // this.msgcop.GrabaMovimiento(comprobante, NumDomto, row["codigoter"].ToString(), linea, ConseCredito, // ERROR: CS1739
                    // Convert.ToDateTime(row["fecha_programada"]).ToString("yyyyMM"), Transaccion, Convert.ToDateTime(row["fecha_programada"]), // ERROR: CS1739
                    // VlrConcepto, 0, DetalleRegistro, usuario, myconnect, // ERROR: CS1739
                    // cuenta: dslinea.Tables["tbllineas"].Rows[0]["cuenta"].ToString(), // ERROR: CS1739
                    // nit: row["nit"].ToString(), // ERROR: CS1739
                    // codigoterMovto: row["codigoter"].ToString(), // ERROR: CS1739
                    // centralCosto: dslinea.Tables["tbllineas"].Rows[0]["centroco"].ToString(), // ERROR: CS1739
                    // empdsto: row["empdsto"].ToString()); // ERROR: CS1739
            }
        }

        // =====================================================================
        // VB líneas 5783-5801  ProcesoCuotasExtrasCredito
        // =====================================================================
        private bool ProcesoCuotasExtrasCredito(double NumSolicitud, string Codigoter, int Lincred, double NumCredito, string periodo, OdbcConnection myconnect)
        {
            DataTable dsextras = new DataTable();
            int Fila = 0;

            try
            {
                // dsextras = this.BuscarExtras(NumSolicitud, myconnect); // ERROR: CS1503

                for (Fila = 0; Fila <= dsextras.Rows.Count - 1; Fila++)
                {
                    DataRow row = dsextras.Rows[Fila];
                    this.GrabarCuotasExtrasCredito(Codigoter, Lincred, NumCredito,
                        Convert.ToInt32(row["numero_cuota"]), Convert.ToDouble(row["valor"]),
                        row["forma_pago"].ToString(), Convert.ToDateTime(row["fecha"]),
                        Convert.ToInt32(periodo), row["tipoextra"].ToString(), myconnect);
                }
                ok = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Proceso Origen: ProcesoCuotasExtrasCredito --> " + ex.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ok = false;
            }
            return ok;
        }

        // =====================================================================
        // VB líneas 5803-5853  GrabarCuotasExtrasCredito
        // =====================================================================
        public void GrabarCuotasExtrasCredito(string Codigoter, int Lincred, double Numero, int NumExtra,
            double Valor, string ForPago, DateTime FecPago, int Periodo, string tipoextra, OdbcConnection Myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();

            // ok = this.msgcop.BuscaExtras(Codigoter, Lincred, Numero, NumExtra, Myconnect); // ERROR: CS1061
            if (!ok)
            {
                StBuilder.Append("insert into cop_extras(CODIGOTER, LINCRED, NUMERO, NUM_EXTRA, VALOR, FORMA_PAGO, FECHA_PAGO, SALDO_ACTUAL, ESTADO,tipoextra) values('");
                StBuilder.Append(Codigoter + "',");
                StBuilder.Append(Lincred + ",");
                StBuilder.Append(Numero + ",");
                StBuilder.Append(NumExtra + ",");
                StBuilder.Append(Valor + ",'");
                StBuilder.Append(ForPago + "','");
                StBuilder.Append(FecPago.ToString(varini.PstForFec) + "',");
                StBuilder.Append(Valor + ",");
                StBuilder.Append("'A','");
                StBuilder.Append(tipoextra + "')");

                ok = this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "GrabarCuotasExtrasCredito");
            }

            if (ok)
            {
                ok = this.msgcop.BuscaSaldoExtrasPactadas(Codigoter, Lincred, Numero, NumExtra, Periodo, Myconnect);
                if (!ok)
                {
                    if (StBuilder.ToString() != "")
                        StBuilder.Replace(StBuilder.ToString(), "");

                    StBuilder.Append("insert into cop_salextras(CODIGOTER, LINCRED, NUMERO, NUM_EXTRA, PERIODO, SALDO, SALDO_INICIAL, vlr_debito, vlr_credito) values('");
                    StBuilder.Append(Codigoter + "',");
                    StBuilder.Append(Lincred + ",");
                    StBuilder.Append(Numero + ",");
                    StBuilder.Append(NumExtra + ",");
                    StBuilder.Append(Periodo + ",");
                    StBuilder.Append(Valor + ",");
                    StBuilder.Append(0 + ",");
                    StBuilder.Append(Valor + ",");
                    StBuilder.Append(0 + ")");
                    this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "GrabarCuotasExtrasCredito(Salextras)");
                }
            }
        }

        // =====================================================================
        // VB líneas 5855-5911  ProcesoDesembolsoCredito
        // =====================================================================
        private bool ProcesoDesembolsoCredito(string comprobante, double NumDocmto, string codigoter, int lincred,
            double Numcredito, DateTime FecMovto, string NitTercero, string Usuario, string Detalle,
            ref double VlrDesembolsado, OdbcConnection myconnect)
        {
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos msgdepLocal = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();
            double Diferencia = 0; string CuenteCruce = "999999999999"; DataSet dscompania = new DataSet();
            double Debito = 0; double Credito = 0;

            try
            {
                string _p1 = ""; string _p2 = ""; string _p3 = ""; string _p4 = ""; string _p5 = ""; string _p6 = "";
                string _p7 = ""; string _p8 = ""; string _p9 = ""; string _p10 = ""; string _diferencia = "0"; string _p12 = "";
                string _p13 = ""; string _p14 = ""; string _p15 = ""; string _p16 = ""; string _p17 = ""; string _cuenteCruce = "999999999999";
                // this.msgcop.BuscaComprobante(comprobante, NumDocmto, false, myconnect, // ERROR: CS1620
                    // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, // ERROR: CS1620
                    // ref _diferencia, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, // ERROR: CS1620
                    // ref _cuenteCruce); // ERROR: CS1620
                Diferencia = Convert.ToDouble(_diferencia);
                CuenteCruce = _cuenteCruce;

                if (Diferencia > 0)
                    VlrDesembolsado = Diferencia;
                else
                    VlrDesembolsado = Diferencia * -1;

                ok = msgdepLocal.CargaCuentasAhorro(codigoter, myconnect);

                if (ok)
                {
                    if (MessageBox.Show("Desembolsar crédito a cuenta de ahorro?", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        DesembolsarCreditoACuentaAhorro(codigoter, comprobante, NumDocmto, FecMovto, Usuario, Detalle, myconnect);
                    this.MarcarDebitoAutomatico(codigoter, lincred, Numcredito, myconnect);
                }

                // Refresca comprobante
                _diferencia = "0"; _cuenteCruce = "999999999999";
                // this.msgcop.BuscaComprobante(comprobante, NumDocmto, false, myconnect, // ERROR: CS1620
                    // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, // ERROR: CS1620
                    // ref _diferencia, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, // ERROR: CS1620
                    // ref _cuenteCruce); // ERROR: CS1620
                Diferencia = Convert.ToDouble(_diferencia);
                CuenteCruce = _cuenteCruce;

                if (CuenteCruce != "999999999999")
                {
                    if (Diferencia != 0)
                    {
                        this.msgparsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);

                        if (Diferencia > 0)
                            Debito = Diferencia;
                        else
                            Credito = Diferencia * -1;

                        // this.msgcop.GrabaMovimiento(comprobante, NumDocmto, "99999999999999", "9999", 0, // ERROR: CS1739
                            // FecMovto.ToString("yyyyMM"), // ERROR: CS1739
                            // dscompania.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"].ToString(), // ERROR: CS1739
                            // FecMovto, Debito, Credito, Detalle, Usuario, myconnect, // ERROR: CS1739
                            // cuenta: CuenteCruce, nit: NitTercero, // ERROR: CS1739
                            // referencia: "PR-" + Numcredito, // ERROR: CS1739
                            // conceptoOtro: "PR", numeroOtro: Numcredito, detalleOtro: Detalle, fechaOtro: FecMovto); // ERROR: CS1739
                    }
                }

                this.msgcop.TrasladaContabilidad(comprobante, NumDocmto, myconnect, Usuario, NitTercero);

                ok = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Proceso Origen: ProcesoDesembolsoCredito --> " + ex.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ok = false;
            }
            return ok;
        }

        // =====================================================================
        // VB líneas 5913-5946  DesembolsarCreditoACuentaAhorro
        // =====================================================================
        private void DesembolsarCreditoACuentaAhorro(string codigoter, string comprobante, double NumDocmto,
            DateTime FecMovto, string Usuario, string Detalle, OdbcConnection myconnect)
        {
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos msgdepLocal = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();
            double VlrDesembolso = 0; double VlrCuenta = 0; double CuentaAhorros = 0; int LineaAhorros = 0;
            DataSet dscompania = new DataSet(); DataSet dslinea = new DataSet();
            string Transaccion = "02";

            string _p1 = ""; string _p2 = ""; string _p3 = ""; string _p4 = ""; string _p5 = ""; string _p6 = "";
            string _vlr = "0"; string _p8 = ""; string _p9 = ""; string _p10 = ""; string _p11 = ""; string _p12 = ""; string _p13 = "";
            // this.msgcop.BuscaComprobante(comprobante, NumDocmto, false, myconnect, // ERROR: CS1501
                // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, // ERROR: CS1501
                // ref _vlr, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13); // ERROR: CS1501
            VlrDesembolso = Convert.ToDouble(_vlr);

            if (VlrDesembolso < 0)
                VlrDesembolso *= -1;

            VlrCuenta = VlrDesembolso;

            // ok = msgdepLocal.AyudaCuentas(codigoter, ref CuentaAhorros, ref LineaAhorros, myconnect, true, ref VlrCuenta); // ERROR: CS7036

            if (ok)
            {
                if (VlrCuenta > VlrDesembolso)
                {
                    MessageBox.Show("No puede desembolsar a cuenta de ahorros un valor mayor que el permitido para este crédito", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DesembolsarCreditoACuentaAhorro(codigoter, comprobante, NumDocmto, FecMovto, Usuario, Detalle, myconnect);
                }

                this.msgparsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);
                // this.msgparcop.BuscaLinea(LineaAhorros, dslinea, myconnect); // ERROR: CS1503, CS1620

                Transaccion = dscompania.Tables["tblcompania"].Rows[0]["cpto_ahorros"].ToString();

                // this.msgcop.GrabaMovimiento(comprobante, NumDocmto, codigoter, LineaAhorros, CuentaAhorros, // ERROR: CS1739
                    // FecMovto.ToString("yyyyMM"), Transaccion, FecMovto, 0, VlrCuenta, Detalle, Usuario, myconnect, // ERROR: CS1739
                    // cuenta: dslinea.Tables["tbllineas"].Rows[0]["cuenta"].ToString(), // ERROR: CS1739
                    // tipoMovto: "CA"); // ERROR: CS1739
            }
        }

        // =====================================================================
        // VB líneas 5948-5959  MarcarDebitoAutomatico
        // =====================================================================
        private void MarcarDebitoAutomatico(string Codigoter, int Lincred, double numero, OdbcConnection myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();

            if (MessageBox.Show("Desea marcar esta obligación para debito automático?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                StBuilder.Append("update cop_maecar set IncluyeDebAuto='Y' ");
                StBuilder.Append("where codigoter='" + Codigoter + "' and lincred=" + Lincred + " and numero=" + numero);
                this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "MarcarDebitoAutomatico");
            }
        }

        // =====================================================================
        // VB líneas 5961-6015  ReliquidaCreditosRecogidos
        // =====================================================================
        public void ReliquidaCreditosRecogidos(double NumSolicitud, DateTime Fecha, OdbcConnection myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            DataSet dscompania = new DataSet(); DataSet dsdatos = new DataSet();
            double SaldoCapNom = 0; double SaldoextNom = 0; double SaldoIntNom = 0; double SaldoMorNom = 0; double SaldoSegNom = 0; double SaldoAdmNom = 0; double SaldoOtrNom = 0;
            double SaldoCapCaj = 0; double SaldoextCaj = 0; double SaldoIntCaj = 0; double SaldoMorCaj = 0; double SaldoSegCaj = 0; double SaldoAdmCaj = 0; double SaldoOtrCaj = 0;
            double TotRec = 0; double Intmes = 0; int fila = 0; double saldo = 0;

            this.msgparsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);

            StBuilder.Append("select solrec.NUMERO,solrec.CODIGOTER,solrec.LINCRED,solrec.NUME_CRED,solrec.VALOR_PAGO,solrec.INTE_ADICIONAL,solrec.TOTPAR,salmae.saldo ");
            StBuilder.Append("from cop_solrecr solrec ");
            StBuilder.Append("inner join cop_salmaecar salmae on solrec.CODIGOTER=salmae.CODIGOTER and solrec.LINCRED=salmae.LINCRED and solrec.NUME_CRED=salmae.numero ");
            StBuilder.Append("where solrec.numero=" + NumSolicitud + " and solrec.TOTPAR='T' " + " and solrec.LINCRED>=1000 and salmae.periodo=" + Fecha.ToString("yyyyMM"));

            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "ReliquidaCreditosRecogidos", ref dsdatos, "tblrecoge");

            for (fila = 0; fila <= dsdatos.Tables["tblrecoge"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdatos.Tables["tblrecoge"].Rows[fila];

                if (row["saldo"] is DBNull)
                    saldo = 0;
                else
                {
                    if (Convert.ToDouble(row["saldo"]) >= 0)
                        saldo = Convert.ToDouble(row["saldo"]);
                    else
                        saldo = 0;
                }

                int opRecDeuda = Convert.ToInt32(dscompania.Tables["tblcompania"].Rows[0]["OpRecDeuda"]);
                switch (opRecDeuda)
                {
                    case 0:
                        // this.msgcop.BuscaCuotasClades(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["nume_cred"]), // ERROR: CS7036
                            // Fecha.ToString("yyyyMM"), global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, myconnect, // ERROR: CS7036
                            // ref SaldoCapNom, ref SaldoextNom, ref SaldoIntNom, ref SaldoMorNom, ref SaldoSegNom, ref SaldoAdmNom, ref SaldoOtrNom); // ERROR: CS7036
                        TotRec = saldo + SaldoIntNom + SaldoMorNom + SaldoSegNom + SaldoAdmNom + SaldoOtrNom;
                        break;
                    case 1:
                        // this.msgcop.BuscaCuotasClades(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["nume_cred"]), // ERROR: CS7036
                            // Fecha.ToString("yyyyMM"), global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, myconnect, // ERROR: CS7036
                            // ref SaldoCapNom, ref SaldoextNom, ref SaldoIntNom, ref SaldoMorNom, ref SaldoSegNom, ref SaldoAdmNom, ref SaldoOtrNom); // ERROR: CS7036
                        this.msgcop.BuscaCuotasClades(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["nume_cred"]),
                            Fecha.ToString("yyyyMM"), global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Caja, myconnect,
                            ref SaldoCapCaj, ref SaldoextCaj);
                        TotRec = saldo + SaldoIntNom + SaldoMorNom + SaldoSegNom + SaldoAdmNom + SaldoOtrNom;
                        TotRec = TotRec - SaldoCapCaj - SaldoextCaj;
                        break;
                    case 2:
                        // this.msgcop.BuscaCuotasClades(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["nume_cred"]), // ERROR: CS7036
                            // Fecha.ToString("yyyyMM"), global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Caja, myconnect, // ERROR: CS7036
                            // ref SaldoCapCaj, ref SaldoextCaj, ref SaldoIntCaj, ref SaldoMorCaj, ref SaldoSegCaj, ref SaldoAdmCaj, ref SaldoOtrCaj); // ERROR: CS7036
                        // this.msgcop.BuscaCuotasClades(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["nume_cred"]), // ERROR: CS7036
                            // Fecha.ToString("yyyyMM"), global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, myconnect, // ERROR: CS7036
                            // ref SaldoCapNom, ref SaldoextNom, ref SaldoIntNom, ref SaldoMorNom, ref SaldoSegNom, ref SaldoAdmNom, ref SaldoOtrNom); // ERROR: CS7036
                        TotRec = saldo + SaldoIntCaj + SaldoMorCaj + SaldoSegCaj + SaldoAdmCaj + SaldoOtrCaj;
                        TotRec = TotRec - SaldoCapNom - SaldoextNom;
                        break;
                }

                Intmes = Math.Round(this.msgcop.BuscaIntMes(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]),
                    Convert.ToDouble(row["nume_cred"]), Fecha, saldo, myconnect), 0);

                // this.msgcop.GrabaDeudaRecogida(NumSolicitud, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1503
                    // Convert.ToDouble(row["nume_cred"]), TotRec, Intmes, "T", Fecha.ToString("yyyyMM"), myconnect); // ERROR: CS1503
            }
        }

        // =====================================================================
        // VB líneas 6017-6284  ProcesoRecogerCreditos
        // =====================================================================
        private bool ProcesoRecogerCreditos(double NumSolicitud, string Comprobante, double NumDocmto, DateTime Fecha,
            string Usuario, System.Windows.Forms.Form MyForma, OdbcConnection myconnect, double ConseCredito,
            ref string ReestrucNovac, ref DataSet DataPoyeccion)
        {
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Recogiendo Créditos", MyForma);
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            DataSet dsdatos = new DataSet(); DataSet dscompania = new DataSet(); DataSet DsDatSet = new DataSet(); DataTable DsReest = new DataTable();
            string StClades = ""; double fila = 0; double InteresRecalculado = 0; double VlrSaldo = 0; double VlrAplicar = 0; bool Reest = false;
            int ClaseDsto = 0; string StTransaccion = "99"; double VlrDebito = 0; double VlrCredito = 0; string CptoIntereses = "02"; string StCalif = "";
            double NoDescPendiente = 0; string tipoNomina = ""; string StMovtoIntereses = ""; string CptoIntIngreso = ""; int DiasVen = 0; string categoria = "A";
            DateTime FecPrimerDsto = DateTime.MinValue; int CptoAnticipos = 0; double SaldoAnticipo = 0; double VlrCuotaAnticipado = 0; string CptoMovCapAnt = "99"; int Tipolinea = 0;
            string descCuoAnticipada = " "; double VlrAcumCuotaAnt = 0; string Cedula = "";
            string Cpto_extra = "";
            double fila_extras = 0;
            string codigoterExtras = "";
            double lincresExtras = 0;
            double numeroExtras = 0;
            double saldoExtras = 0;
            bool OKEXTRAS = false;

            try
            {
                msgbarra.ValorMinimoMaximo(0, 10);
                msgbarra.Show();

                this.msgparsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);

                CptoIntereses = dscompania.Tables["tblcompania"].Rows[0]["CPTO_INTERES"].ToString();
                CptoIntIngreso = dscompania.Tables["tblcompania"].Rows[0]["cpto_inteatra"].ToString();
                ClaseDsto = Convert.ToInt32(dscompania.Tables["tblcompania"].Rows[0]["OpRecDeuda"]);
                tipoNomina = dscompania.Tables["tblcompania"].Rows[0]["CLASE_NOMINA"].ToString();
                CptoAnticipos = Convert.ToInt32(dscompania.Tables["tblcompania"].Rows[0]["CptoAnticipo"]);
                Cpto_extra = dscompania.Tables["tblcompania"].Rows[0]["Cpto_extra"].ToString();

                string _descCuoAnt = " "; string _tipL = "0"; string _dummy = "";
                // ok = this.msgcop.BuscaLinea(CptoAnticipos, myconnect, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _tipL, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _descCuoAnt); // ERROR: CS7036
                Tipolinea = Convert.ToInt32(_tipL);
                descCuoAnticipada = _descCuoAnt;

                switch (Tipolinea)
                {
                    case 3:
                        CptoMovCapAnt = dscompania.Tables["tblcompania"].Rows[0]["cpto_servi"].ToString();
                        break;
                    case 4:
                        CptoMovCapAnt = dscompania.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"].ToString();
                        break;
                    default:
                        CptoMovCapAnt = dscompania.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"].ToString();
                        break;
                }

                // this.msgcop.CreaTablaAgregaDeudasReest(DsReest); // ERROR: CS1061
                Reest = false; ReestrucNovac = "NA";

                StBuilder.Append("select solrec.NUMERO,solrec.CODIGOTER,solrec.LINCRED,solrec.NUME_CRED,solrec.VALOR_PAGO,solrec.INTE_ADICIONAL,solrec.TOTPAR,salmae.saldo,soli.lincred as LineaCredito,soli.fecdesc,mae.reest,salmae.calffinal,con.codahor ");
                StBuilder.Append("from cop_solrecr solrec ");
                StBuilder.Append("inner join cop_maecar mae on solrec.CODIGOTER=mae.CODIGOTER and solrec.LINCRED=mae.LINCRED and solrec.NUME_CRED=mae.numero inner join cop_concar12 con on mae.lincred=con.lincred ");
                StBuilder.Append("left join cop_salmaecar salmae on solrec.CODIGOTER=salmae.CODIGOTER and solrec.LINCRED=salmae.LINCRED and solrec.NUME_CRED=salmae.numero and salmae.periodo=" + Fecha.ToString("yyyyMM") + "  ");
                StBuilder.Append("inner join cop_solcre soli on solrec.numero=soli.numero ");
                StBuilder.Append("where solrec.numero=" + NumSolicitud + " and ((mae.lincred>=1000 and salmae.saldo>0) or mae.lincred<1000) ");

                this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "ProcesoRecogerCreditos", ref dsdatos, "tblrecoge");

                msgbarra.ValorMinimoMaximo(0, dsdatos.Tables["tblrecoge"].Rows.Count);

                for (fila = 0; fila <= dsdatos.Tables["tblrecoge"].Rows.Count - 1; fila++)
                {
                    DataRow row = dsdatos.Tables["tblrecoge"].Rows[(int)fila];

                    VlrDebito = 0; VlrCredito = 0; DiasVen = 0; categoria = "A";

                    if (fila == 0)
                        FecPrimerDsto = Convert.ToDateTime(row["fecdesc"]);

                    if (row["TOTPAR"].ToString() == "T")
                        InteresRecalculado = Convert.ToDouble(row["INTE_ADICIONAL"]);
                    else
                        InteresRecalculado = 0;

                    if (row["saldo"] is DBNull)
                        VlrSaldo = 0;
                    else
                        VlrSaldo = Convert.ToDouble(row["saldo"]);

                    VlrAplicar = Convert.ToDouble(row["VALOR_PAGO"]);

                    if (VlrAplicar < 0)
                    {
                        VlrDebito = VlrAplicar * -1;
                        VlrAplicar = 0;
                    }

                    if (row["TOTPAR"].ToString() == "T")
                    {
                        // ok = msgparcop.BuscaLinea(Convert.ToInt32(row["lincred"]), DsDatSet, myconnect); // ERROR: CS1503, CS1620
                        if (ok)
                        {
                            if (Convert.ToDouble(row["lincred"]) >= 1000)
                            {
                                string codahor = DsDatSet.Tables["tbllineas"].Rows[0]["codahor"].ToString();
                                if (codahor == "4" || codahor == "5")
                                {
                                    // Reest = msgcop.ValidaSiAplicaReest(row["CODIGOTER"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1061
                                        // Convert.ToDouble(row["NUME_CRED"]), Fecha, myconnect, ref categoria); // ERROR: CS1061

                                    if (string.Compare(categoria, "B") >= 0)
                                        Reest = true;
                                    else
                                    {
                                        if (row["reest"].ToString() == "Y")
                                        {
                                            Reest = true;
                                            categoria = row["calffinal"].ToString();
                                        }
                                    }

                                    DsReest.Rows.Add(row["CODIGOTER"], row["lincred"], row["NUME_CRED"],
                                        Convert.ToDouble(VlrAplicar), categoria, row["CODIGOTER"],
                                        Convert.ToInt32(row["LineaCredito"]), Convert.ToDouble(ConseCredito), 0);
                                }
                            }
                        }
                    }

                    if (VlrAplicar > 0)
                    {
                        // this.msgcop.GrabaMovimiento(Comprobante, NumDocmto, row["CODIGOTER"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1739
                            // Convert.ToDouble(row["NUME_CRED"]), Fecha.ToString("yyyyMM"), "99", Fecha, 0, VlrAplicar, // ERROR: CS1739
                            // "RECOGER PRESTAMO", Usuario, myconnect, // ERROR: CS1739
                            // claseDsto: ClaseDsto, esRecobro: true, tipoNomina: tipoNomina); // ERROR: CS1739
                    }
                    else
                    {
                        this.msgcop.BuscaLineaTipoMovto(Convert.ToInt32(row["lincred"]), myconnect, ref StTransaccion);
                        // this.msgcop.GrabaMovimiento(Comprobante, NumDocmto, row["CODIGOTER"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1739
                            // Convert.ToDouble(row["NUME_CRED"]), Fecha.ToString("yyyyMM"), StTransaccion, Fecha, VlrDebito, VlrAplicar, // ERROR: CS1739
                            // "RECOGER PRESTAMO", Usuario, myconnect, tipoNomina: tipoNomina); // ERROR: CS1739
                    }

                    if (VlrAplicar > 0)
                    {
                        VlrDebito = 0;
                        if (Convert.ToDouble(row["lincred"]) >= 1000)
                        {
                            if (VlrAplicar > VlrSaldo)
                                VlrAplicar = VlrSaldo;
                        }

                        // Sección extras
                        System.Text.StringBuilder StBuilderExtras = new System.Text.StringBuilder();
                        DataSet dsdatosExtras = new DataSet();
                        codigoterExtras = row["CODIGOTER"].ToString();
                        lincresExtras = Convert.ToDouble(row["lincred"]);
                        numeroExtras = Convert.ToDouble(row["NUME_CRED"]);

                        StBuilderExtras.Append("select extras.codigoter,extras.lincred,extras.numero,extras.num_extra,sal_extras.SALDO ");
                        StBuilderExtras.Append(" from cop_extras extras inner join cop_salextras sal_extras  on extras.codigoter = sal_extras.codigoter  ");
                        StBuilderExtras.Append(" and extras.lincred = sal_extras.lincred and extras.numero = sal_extras.numero  and extras.num_extra = sal_extras.num_extra   ");
                        StBuilderExtras.Append(" where extras.codigoter = '" + codigoterExtras + "' and extras.LINCRED = " + lincresExtras + " and extras.NUMERO =  " + numeroExtras);
                        StBuilderExtras.Append(" and sal_extras.PERIODO = " + Fecha.ToString("yyyyMM") + " and sal_extras.SALDO > 0");
                        StBuilderExtras.Append(" order by extras.NUM_EXTRA");

                        this.OdbcConnect.ExecuteQueryDataset(StBuilderExtras.ToString(), myconnect, "ProcesoRecogerCredito_extras", ref dsdatosExtras, "tblextras");

                        for (fila_extras = 0; fila_extras <= dsdatosExtras.Tables["tblextras"].Rows.Count - 1; fila_extras++)
                        {
                            DataRow rowExt = dsdatosExtras.Tables["tblextras"].Rows[(int)fila_extras];
                            if (VlrAplicar > 0)
                            {
                                if (VlrAplicar > Convert.ToDouble(rowExt["SALDO"]))
                                {
                                    saldoExtras = Convert.ToDouble(rowExt["SALDO"]);
                                    // this.msgcop.GrabaMovimiento(Comprobante, NumDocmto, codigoterExtras, (int)lincresExtras, numeroExtras, // ERROR: CS1739
                                        // Fecha.ToString("yyyyMM"), Cpto_extra, Fecha, 0, saldoExtras, // ERROR: CS1739
                                        // "RECOGER PRESTAMO EXTRAS", Usuario, myconnect, // ERROR: CS1739
                                        // numExtra: rowExt["num_extra"], tipoNomina: tipoNomina); // ERROR: CS1739
                                    VlrAplicar -= Convert.ToDouble(rowExt["SALDO"]);
                                }
                                else if (VlrAplicar < Convert.ToDouble(rowExt["SALDO"]))
                                {
                                    saldoExtras = VlrAplicar;
                                    // this.msgcop.GrabaMovimiento(Comprobante, NumDocmto, codigoterExtras, (int)lincresExtras, numeroExtras, // ERROR: CS1739
                                        // Fecha.ToString("yyyyMM"), Cpto_extra, Fecha, 0, saldoExtras, // ERROR: CS1739
                                        // "RECOGER PRESTAMO EXTRAS", Usuario, myconnect, // ERROR: CS1739
                                        // numExtra: rowExt["num_extra"], tipoNomina: tipoNomina); // ERROR: CS1739
                                    VlrAplicar = 0;
                                }
                            }
                            else
                                break;
                        }

                        if (VlrAplicar > 0)
                        {
                            this.msgcop.BuscaLineaTipoMovto(Convert.ToInt32(row["lincred"]), myconnect, ref StTransaccion);
                            // this.msgcop.GrabaMovimiento(Comprobante, NumDocmto, row["CODIGOTER"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1739
                                // Convert.ToDouble(row["NUME_CRED"]), Fecha.ToString("yyyyMM"), StTransaccion, Fecha, 0, VlrAplicar, // ERROR: CS1739
                                // "RECOGER PRESTAMO", Usuario, myconnect, tipoNomina: tipoNomina); // ERROR: CS1739

                            if (InteresRecalculado > 0)
                            {
                                VlrCredito = InteresRecalculado;
                                StMovtoIntereses = CptoIntIngreso;
                            }
                            else
                            {
                                VlrDebito = InteresRecalculado * -1;
                                StMovtoIntereses = CptoIntereses;
                            }

                            if (VlrDebito != 0 || VlrCredito != 0)
                            {
                                // this.msgcop.GrabaMovimiento(Comprobante, NumDocmto, row["CODIGOTER"].ToString(), Convert.ToInt32(row["lincred"]), // ERROR: CS1739
                                    // Convert.ToDouble(row["NUME_CRED"]), Fecha.ToString("yyyyMM"), StMovtoIntereses, Fecha, VlrDebito, VlrCredito, // ERROR: CS1739
                                    // "RECOGER PRESTAMO", Usuario, myconnect, tipoNomina: tipoNomina); // ERROR: CS1739
                            }
                        }
                    }

                    if (row["TOTPAR"].ToString() == "T")
                    {
                        SaldoAnticipo = 0; VlrCuotaAnticipado = 0;
                        Cedula = row["codigoter"].ToString();
                        ok = this.msgcop.BuscaSaldoObligacion(row["codigoter"].ToString(), CptoAnticipos, 0,
                            Fecha.ToString("yyyyMM"), myconnect, ref SaldoAnticipo);
                        if (ok)
                        {
                            if (SaldoAnticipo < 0)
                            {
                                StBuilder.Replace(StBuilder.ToString(), "");
                                StBuilder.Append("select sum(a.VLR_CUOTA) as campo1 ");
                                StBuilder.Append("from cop_cuoant a ");
                                StBuilder.Append("where a.codigoter='" + row["codigoter"].ToString() + "' and a.lincred=" + row["lincred"].ToString() + " and a.numero=" + row["numero"].ToString());
                                StBuilder.Append(" and a.estado='O' group by a.codigoter,a.lincred,a.numero ");

                                string _vlrCuo = "0";
                                ok = this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "ProcesoRecogerCreditos(CuotaAnticipada)", ref _vlrCuo);
                                VlrCuotaAnticipado = Convert.ToDouble(_vlrCuo);

                                if (ok)
                                {
                                    if ((SaldoAnticipo * -1) > VlrCuotaAnticipado)
                                        VlrCuotaAnticipado = (SaldoAnticipo * -1);

                                    VlrAcumCuotaAnt = VlrAcumCuotaAnt + VlrCuotaAnticipado;

                                    // this.msgcop.GrabaMovimiento(Comprobante, NumDocmto, row["CODIGOTER"].ToString(), CptoAnticipos, 0, // ERROR: CS1739
                                        // Fecha.ToString("yyyyMM"), CptoMovCapAnt, Fecha, VlrCuotaAnticipado, 0, // ERROR: CS1739
                                        // "RECOGER CUOTA ANTICIPADA", Usuario, myconnect, tipoNomina: tipoNomina); // ERROR: CS1739

                                    // this.msgcop.GrabaDeudaRecogida(NumSolicitud, row["CODIGOTER"].ToString(), CptoAnticipos, 0, // ERROR: CS1503
                                        // VlrAcumCuotaAnt * -1, 0, "T", Fecha.ToString("yyyyMM"), myconnect); // ERROR: CS1503
                                }
                            }
                        }
                    }
                }

                if (Reest)
                {
                    for (fila = 0; fila <= DsReest.Rows.Count - 1; fila++)
                    {
                        DataRow rowR = DsReest.Rows[(int)fila];
                        msgcop.grabaReEstructurados(rowR["CODIGOTER"].ToString(), Convert.ToInt32(rowR["lincred"]),
                            Convert.ToDouble(rowR["numero"]), rowR["CODIGOTER"].ToString(),
                            Convert.ToInt32(rowR["Idlincred"]), Convert.ToDouble(rowR["Idnumero"]),
                            rowR["Categoria"].ToString(), Fecha, Convert.ToDouble(rowR["Saldo"]),
                            Usuario, DateTime.Now, myconnect);
                    }
                    ReestrucNovac = "RT";
                    // StCalif = msgcop.ValidaCalificacionReest(DsReest); // ERROR: CS1061
                    msgcop.GrabaMaecar(DsReest.Rows[0]["codigoter"].ToString(), Convert.ToInt32(DsReest.Rows[0]["Idlincred"]),
                        Convert.ToDouble(DsReest.Rows[0]["Idnumero"]), "Y", Fecha, StCalif, myconnect);
                    this.ActualizaCalificacionReest(DsReest.Rows[0]["codigoter"].ToString(), Convert.ToInt32(DsReest.Rows[0]["Idlincred"]),
                        Convert.ToDouble(DsReest.Rows[0]["Idnumero"]), StCalif, Fecha, myconnect, 0);
                    this.ActualizaFechaRevisionReest(DsReest.Rows[0]["codigoter"].ToString(), Convert.ToInt32(DsReest.Rows[0]["Idlincred"]),
                        Convert.ToDouble(DsReest.Rows[0]["Idnumero"]), FecPrimerDsto, Fecha, myconnect, 0);
                }
                else
                {
                    if (fila > 0)
                        ReestrucNovac = "NV";
                }

                if (VlrAcumCuotaAnt > 0)
                {
                    if (DataPoyeccion != null)
                    {
                        DataPoyeccion.Tables["tbldeducciones"].Rows.Add(Cedula, CptoAnticipos, 0, descCuoAnticipada, VlrAcumCuotaAnt * -1, 0, "T");
                        DataPoyeccion.Tables["TbldatosCredito"].Rows[0]["ValMenos"] =
                            Convert.ToDouble(DataPoyeccion.Tables["TbldatosCredito"].Rows[0]["ValMenos"]) + (VlrAcumCuotaAnt * -1);
                    }
                }

                ok = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Proceso Origen: ProcesoRecogerCreditos --> " + ex.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ok = false;
            }

            msgbarra.Close();
            msgbarra.Dispose();

            return ok;
        }

        // =====================================================================
        // VB líneas 6286-6289  ImprimirLibranzaPagareCarta
        // =====================================================================
        private void ImprimirLibranzaPagareCarta(double NumSolicitud, string Usuario, OdbcConnection myconnect,
            int lincred = 0, string codigoter = "", int Numero = 0)
        {
            ERP.Core.CarteraFinanciera.Reportes.ImpreDoc ImpLibPagare = new ERP.Core.CarteraFinanciera.Reportes.ImpreDoc(Usuario);
            // ImpLibPagare.imp_libran_pagare(NumSolicitud, myconnect, 1, lincred: lincred, codigoter: codigoter, numero: Numero); // ERROR: CS1739
        }

        // =====================================================================
        // VB líneas 6291-6298  ConversionDTFaNMV
        // =====================================================================
        public decimal ConversionDTFaNMV(decimal Dtf, decimal puntos)
        {
            decimal IntEfectivo = 0; decimal InteresPeriodico = 0; decimal N = 12;
            decimal Periodos = Math.Round(1 / N, 8);

            IntEfectivo = (Dtf / 100); // ((1 + (Dtf / 100)) * (1 + (puntos / 100))) - 1
            InteresPeriodico = (decimal)((((Math.Pow((double)(1 + IntEfectivo), (double)Periodos) - 1) * (double)N) + (double)(puntos / 100)) / 12);
            InteresPeriodico = InteresPeriodico * 100;
            return InteresPeriodico;
        }

        // VB línea 6299: 'New función para buscar el número de solicitud de crédito
        // (Continúa en líneas posteriores a 6300 — parte 4)

    } // restored
} // restored
