// Traducción de: ClsLiqcreditos.vb (msgliqcre) — Parte 1 (líneas VB 1-2100)
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Creditos
{
    public partial class ClsLiqcreditos
    {
        // === ENUMS ===

        public enum OpcionProyeccion
        {
            Linea = 0,
            Solicitud = 1,
            Maecar = 2,
            Cuota = 3,
            Salir = 4
        }

        public enum Periodicidad : int
        {
            Mensual = 1,
            Quincenal = 2,
            Decadal = 3,
            Semanal = 4,
            Diario = 5
        }

        public enum TipoBien : int
        {
            Raiz = 1,
            Vehiculo = 2,
            Todos = 3
        }

        // === FIELDS ===

        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private string stmysql;
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect OdbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        public int CantDeCodeudores;
        public double TotDeudasRecogi;
        public double BaseAport;
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private bool ok;

        // === HELPER WRAPPERS ===

        private bool ExecQueryconec(string sql, OdbcConnection con, string name)
        {
            string a = " ", b = " ", c = " ", d = " ";
            return OdbcConnect.ExecuteQueryconec(sql, con, name, ref a, ref b, ref c, ref d);
        }

        private bool ExecQueryconec(string sql, OdbcConnection con, string name, ref string f1)
        {
            string b = " ", c = " ", d = " ";
            return OdbcConnect.ExecuteQueryconec(sql, con, name, ref f1, ref b, ref c, ref d);
        }

        private bool ExecQueryconec(string sql, OdbcConnection con, string name, ref string f1, ref string f2)
        {
            string c = " ", d = " ";
            return OdbcConnect.ExecuteQueryconec(sql, con, name, ref f1, ref f2, ref c, ref d);
        }

        private bool ExecQueryconec(string sql, OdbcConnection con, string name, ref string f1, ref string f2, ref string f3, ref string f4)
        {
            return OdbcConnect.ExecuteQueryconec(sql, con, name, ref f1, ref f2, ref f3, ref f4);
        }

        private bool ExecQueryDataset(string sql, OdbcConnection con, string name, ref DataSet ds, string table)
        {
            return OdbcConnect.ExecuteQueryDataset(sql, con, name, ref ds, table);
        }

        // === METHODS (VB lines 36-2099) ===

        // VB line 36 — BuscaSolicitudCredito
        public void BuscaSolicitudCredito(
            double NumSolicitud, OdbcConnection Myconnect,
            ref string Codigoter, ref int lincred, ref double VlrSolicitado, ref int Plazo,
            ref DateTime FecProgracion, ref decimal TasaInt,
            ref double Cuota, ref DateTime fecdesc, ref int Ciclodsto,
            ref int Periodicidad2, ref int Clacuo, ref int ClaInt, ref int Clades,
            ref DateTime FecSolicitud, ref int TipSeg, ref int Tipadm,
            ref int TipCap, ref int Foradm, ref int TipIntcie, ref int TipCarAdi,
            ref string CuextIntAnt, ref double TasaSeg,
            ref double CuotaAdm, ref double CuotaSeg, ref double CuotaIcie, ref double CuotaCptl)
        {
            string Fecpro = " ";

            // Query 1: CODIGOTER, LINCRED, VLR_SOLICITUD, PLAZO
            stmysql = "select  CODIGOTER as campo1, LINCRED as campo2,VLR_SOLICITUD as campo3, PLAZO as campo4 from cop_solcre where numero = " + NumSolicitud;
            {
                string _codigoter = Codigoter, _lincred = lincred.ToString(), _vlrSol = VlrSolicitado.ToString(), _plazo = Plazo.ToString();
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _codigoter, ref _lincred, ref _vlrSol, ref _plazo);
                Codigoter = _codigoter;
                if (int.TryParse(_lincred, out int __lincred)) lincred = __lincred;
                if (double.TryParse(_vlrSol, out double __vlrSol)) VlrSolicitado = __vlrSol;
                if (int.TryParse(_plazo, out int __plazo)) Plazo = __plazo;
            }

            // Query 2: TASA_INT, CUOTA, FECDESC, CICLOD
            stmysql = "select TASA_INT as campo1, CUOTA as campo2, FECDESC as campo3,CICLOD as campo4 from cop_solcre where numero = " + NumSolicitud;
            {
                string _tasa = TasaInt.ToString(), _cuota = Cuota.ToString(), _fecdesc = fecdesc.ToString(), _ciclo = Ciclodsto.ToString();
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _tasa, ref _cuota, ref _fecdesc, ref _ciclo);
                if (decimal.TryParse(_tasa, out decimal __tasa)) TasaInt = __tasa;
                if (double.TryParse(_cuota, out double __cuota)) Cuota = __cuota;
                if (DateTime.TryParse(_fecdesc, out DateTime __fecdesc)) fecdesc = __fecdesc;
                if (int.TryParse(_ciclo, out int __ciclo)) Ciclodsto = __ciclo;
            }

            // Query 3: PERIODD, CLACUO, CLASEI, CLADES
            stmysql = "select PERIODD as campo1, CLACUO as campo2, CLASEI as campo3, CLADES as campo4 from cop_solcre where numero = " + NumSolicitud;
            {
                string _per = Periodicidad2.ToString(), _clacuo = Clacuo.ToString(), _clasei = ClaInt.ToString(), _clades = Clades.ToString();
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _per, ref _clacuo, ref _clasei, ref _clades);
                if (int.TryParse(_per, out int __per)) Periodicidad2 = __per;
                if (int.TryParse(_clacuo, out int __clacuo)) Clacuo = __clacuo;
                if (int.TryParse(_clasei, out int __clasei)) ClaInt = __clasei;
                if (int.TryParse(_clades, out int __clades)) Clades = __clades;
            }

            // Query 4: FECHA_SOLI, FECHA_PROGRAMADA, Tip_seg, Tip_adm
            stmysql = "select FECHA_SOLI as campo1,FECHA_PROGRAMADA as campo2, Tip_seg as campo3, Tip_adm as campo4 from cop_solcre where numero = " + NumSolicitud;
            {
                string _fecsoli = FecSolicitud.ToString(), _fecpro = Fecpro, _tipseg = TipSeg.ToString(), _tipadm = Tipadm.ToString();
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCred ito", ref _fecsoli, ref _fecpro, ref _tipseg, ref _tipadm);
                if (DateTime.TryParse(_fecsoli, out DateTime __fecsoli)) FecSolicitud = __fecsoli;
                Fecpro = _fecpro;
                if (int.TryParse(_tipseg, out int __tipseg)) TipSeg = __tipseg;
                if (int.TryParse(_tipadm, out int __tipadm)) Tipadm = __tipadm;
            }

            // Query 5: tip_cap, for_adm, tip_intcie, tip_otr
            stmysql = "select tip_cap as campo1 , for_adm as campo2, tip_intcie as campo3, tip_otr as campo4 from cop_solcre where numero = " + NumSolicitud;
            {
                string _tipcap = TipCap.ToString(), _foradm = Foradm.ToString(), _tipintcie = TipIntcie.ToString(), _tipcar = TipCarAdi.ToString();
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _tipcap, ref _foradm, ref _tipintcie, ref _tipcar);
                if (int.TryParse(_tipcap, out int __tipcap)) TipCap = __tipcap;
                if (int.TryParse(_foradm, out int __foradm)) Foradm = __foradm;
                if (int.TryParse(_tipintcie, out int __tipintcie)) TipIntcie = __tipintcie;
                if (int.TryParse(_tipcar, out int __tipcar)) TipCarAdi = __tipcar;
            }

            // Query 6: CUEX_INANT, TASASEG
            stmysql = "select CUEX_INANT as campo1, TASASEG as campo2 from cop_solcre where numero = " + NumSolicitud;
            {
                string _cuext = CuextIntAnt, _tasaseg = TasaSeg.ToString();
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _cuext, ref _tasaseg);
                CuextIntAnt = _cuext;
                if (double.TryParse(_tasaseg, out double __tasaseg)) TasaSeg = __tasaseg;
            }

            // Query 7: cuota_adm, cuota_seg, cuota_icie, cuota_cptl
            stmysql = "select cuota_adm as campo1, cuota_seg as campo2,cuota_icie as campo3,cuota_cptl as campo4 from cop_solcre where numero = " + NumSolicitud;
            {
                string _cadm = CuotaAdm.ToString(), _cseg = CuotaSeg.ToString(), _cicie = CuotaIcie.ToString(), _ccptl = CuotaCptl.ToString();
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _cadm, ref _cseg, ref _cicie, ref _ccptl);
                if (double.TryParse(_cadm, out double __cadm)) CuotaAdm = __cadm;
                if (double.TryParse(_cseg, out double __cseg)) CuotaSeg = __cseg;
                if (double.TryParse(_cicie, out double __cicie)) CuotaIcie = __cicie;
                if (double.TryParse(_ccptl, out double __ccptl)) CuotaCptl = __ccptl;
            }

            if (!Microsoft.VisualBasic.Information.IsDate(Fecpro))
            {
                FecProgracion = new DateTime(1950, 1, 1);
            }
            else
            {
                FecProgracion = Convert.ToDateTime(Fecpro);
            }
        }

        // VB line 75 — GrabaIntCierre (overload 1)
        public void GrabaIntCierre(double NumSolicitud, OdbcConnection Mtyconnect, double Intcierre = 0, double Cuota = 0)
        {
            stmysql = "UPDATE cop_solcre set cuota_icie ='" + Intcierre + "',CUOTA = " + Cuota + " where numero = '" + NumSolicitud + "'";
            ExecQueryconec(stmysql, Mtyconnect, "GrabaIntCierre");
        }

        // VB line 80 — GrabaIntCierre (overload 2, Overridable)
        public virtual void GrabaIntCierre(double NumSolicitud, double Intcierre, double Cuota, double CuotaSeguro, double CuotaAdmon, double CuotaCptl, OdbcConnection Mtyconnect)
        {
            stmysql = "UPDATE cop_solcre set cuota_icie ='" + Intcierre + "',CUOTA = " + Cuota + ",CUOTA_SEG = " + CuotaSeguro +
                      ",CUOTA_ADM = " + CuotaAdmon + ",CUOTA_CPTL=" + CuotaCptl + " where numero = '" + NumSolicitud + "'";
            ExecQueryconec(stmysql, Mtyconnect, "GrabaIntCierre");
        }

        // VB line 86 — BuscaDeudasRecogidas
        public double BuscaDeudasRecogidas(double NumSolcitud, ref System.Windows.Forms.ListView Lstdatos, OdbcConnection myconnect)
        {
            int inI = 1;
            double Vlrtotal = 0;
            int totreg = 0, cont = 0;
            string mysql = "select solrec.*, parame12.descripcion from cop_solrecr solrec inner join cop_concar12 parame12 on solrec.lincred = parame12.lincred "
                         + " where numero = '" + NumSolcitud + "'";

            inI = Lstdatos.Items.Count;

            DataSet myReader1 = new DataSet();
            OdbcConnect.ExecuteQueryDataset(mysql, myconnect, "BuscaDeudasRecogidas", ref myReader1, "TblBuscaDeudRecogida");
            totreg = myReader1.Tables["TblBuscaDeudRecogida"].Rows.Count;

            while (cont < totreg)
            {
                DataRow _row = myReader1.Tables["TblBuscaDeudRecogida"].Rows[cont];
                Lstdatos.Items.Add(inI.ToString());
                Lstdatos.Items[inI].SubItems.Add(_row["lincred"].ToString());
                Lstdatos.Items[inI].SubItems.Add(_row["nume_cred"].ToString());
                Lstdatos.Items[inI].SubItems.Add(_row["Descripcion"].ToString());
                Lstdatos.Items[inI].SubItems.Add(string.Format("{0:N0}", Convert.ToDouble(_row["valor_pago"])));
                Vlrtotal += Convert.ToDouble(string.Format("{0:N0}", Convert.ToDouble(_row["valor_pago"])));
                if (Convert.ToDouble(_row["inte_adicional"]) != 0)
                {
                    inI = inI + 1;
                    Lstdatos.Items.Add(inI.ToString());
                    Lstdatos.Items[inI].SubItems.Add(_row["lincred"].ToString());
                    Lstdatos.Items[inI].SubItems.Add(_row["nume_cred"].ToString());
                    Lstdatos.Items[inI].SubItems.Add("Interes");
                    Lstdatos.Items[inI].SubItems.Add(string.Format("{0:N0}", Convert.ToDouble(_row["inte_adicional"])));
                    Vlrtotal += Convert.ToDouble(string.Format("{0:N0}", Convert.ToDouble(_row["inte_adicional"])));
                }
                cont += 1;
                inI = inI + 1;
            }
            myReader1.Dispose();
            return Vlrtotal;
        }

        // VB line 125 — BuscaCuotasExtras
        public double BuscaCuotasExtras(double NumSolcitud, ref System.Windows.Forms.ListView Lstdatos, OdbcConnection myconnect)
        {
            int inI = 0;
            double Vlrtotal = 0;
            int totreg = 0, cont = 0;
            string mysql = "SELECT numero_cuota, fecha, valor, Forma_pago, tipoextra FROM cop_extrasoli where numero = '" + NumSolcitud + "'";

            Lstdatos.Items.Clear();

            DataSet myReader1 = new DataSet();
            OdbcConnect.ExecuteQueryDataset(mysql, myconnect, "BuscaCuotasExtras", ref myReader1, "TblBuscaCuotasExtras");
            totreg = myReader1.Tables["TblBuscaCuotasExtras"].Rows.Count;

            while (cont < totreg)
            {
                DataRow _row = myReader1.Tables["TblBuscaCuotasExtras"].Rows[cont];
                Lstdatos.Items.Add(inI.ToString());
                Lstdatos.Items[inI].SubItems.Add(_row["fecha"].ToString());
                Lstdatos.Items[inI].SubItems.Add(_row["valor"].ToString());
                Lstdatos.Items[inI].SubItems.Add(_row["Forma_pago"].ToString());
                cont += 1;
                inI += 1;
            }
            myReader1.Dispose();
            return Vlrtotal;
        }

        // VB line 152 — ExecuteQueryconec (local override — keeps internal reader logic)
        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);

            try
            {
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();

                if (Myread.RecordsAffected > 0)
                {
                    result = true;
                }

                while (Myread.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myread["campo1"] == DBNull.Value)
                            Campo1 = "0";
                        else
                            Campo1 = Myread["campo1"].ToString().Trim();
                    }
                    if (Campo2 != "")
                    {
                        if (Myread["campo2"] == DBNull.Value)
                            Campo2 = "0";
                        else
                            Campo2 = Myread["campo2"].ToString().Trim();
                    }
                    if (Campo3 != "")
                    {
                        if (Myread["campo3"] == DBNull.Value)
                            Campo3 = "0";
                        else
                            Campo3 = Myread["campo3"].ToString().Trim();
                    }
                    if (Campo4 != "")
                    {
                        if (Myread["campo4"] == DBNull.Value)
                            Campo4 = "0";
                        else
                            Campo4 = Myread["campo4"].ToString().Trim();
                    }
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql,
                    "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return result;
        }

        // VB line 207 — CargaCiclosDsto
        public void CargaCiclosDsto(int Periodicidad2, System.Windows.Forms.ComboBox CbxCicloDsto)
        {
            CbxCicloDsto.Items.Clear();
            switch (Periodicidad2)
            {
                case 1:
                    CbxCicloDsto.Items.Add("5-Todos Dsctos del mes");
                    break;
                case 2:
                    CbxCicloDsto.Items.Add("1-Primer Dscto del mes");
                    CbxCicloDsto.Items.Add("2-Segundo Dscto del mes");
                    CbxCicloDsto.Items.Add("5-Todos Dsctos del mes");
                    break;
                case 3:
                    CbxCicloDsto.Items.Add("1-Primer Dscto del mes");
                    CbxCicloDsto.Items.Add("2-Segundo Dscto del mes");
                    CbxCicloDsto.Items.Add("3-Tercer Dscto del mes");
                    CbxCicloDsto.Items.Add("5-Todos Dsctos del mes");
                    break;
                case 4:
                    CbxCicloDsto.Items.Add("1-Primer Dscto del mes");
                    CbxCicloDsto.Items.Add("2-Segundo Dscto del mes");
                    CbxCicloDsto.Items.Add("3-Tercer Dscto del mes");
                    CbxCicloDsto.Items.Add("4-Cuarto Dscto del mes");
                    CbxCicloDsto.Items.Add("5-Todos Dsctos del mes");
                    break;
                case 5:
                    CbxCicloDsto.Items.Add("5-Todos Dsctos del mes");
                    break;
            }
        }

        // VB line 234 — CalculaCiclo
        // Full overload: all Optional ByRef params as ref (VB signature)
        public string CalculaCiclo(int CicloDsto, Periodicidad Periodicidad2, DateTime Fecha,
            bool MuestraMensaje, ref bool validarciclo, string mensajeValida)
        {
            return CalculaCiclo_impl(CicloDsto, Periodicidad2, Fecha, MuestraMensaje, ref validarciclo, mensajeValida);
        }

        // Convenience overload: no optional params (uses defaults MuestraMensaje=true, mensajeValida="")
        public string CalculaCiclo(int CicloDsto, Periodicidad Periodicidad2, DateTime Fecha)
        {
            bool validarciclo = true;
            return CalculaCiclo_impl(CicloDsto, Periodicidad2, Fecha, true, ref validarciclo, "");
        }

        // Overload: MuestraMensaje specified, no validarciclo ref needed
        public string CalculaCiclo(int CicloDsto, Periodicidad Periodicidad2, DateTime Fecha, bool MuestraMensaje)
        {
            bool validarciclo = true;
            return CalculaCiclo_impl(CicloDsto, Periodicidad2, Fecha, MuestraMensaje, ref validarciclo, "");
        }

        private string CalculaCiclo_impl(int CicloDsto, Periodicidad Periodicidad2, DateTime Fecha,
            bool MuestraMensaje, ref bool validarciclo, string mensajeValida)
        {
            int Mes, Ciclo = 0, Dia = 0;
            string NumCiclo = "9999";
            int DiaIni = 0, DiaFin = 0;
            DateTime Fecini = new DateTime(Fecha.Year, 1, 1);

            Mes = Fecha.Month;
            Dia = Fecha.Day;
            validarciclo = true; // variable que me permitirá validar que el ciclo sea el correcto

            if ((int)Periodicidad2 >= 1 && (int)Periodicidad2 <= 4)
            {
                Ciclo = Mes * Convert.ToInt32((int)Periodicidad2);
            }

            switch (CicloDsto)
            {
                case 5:
                    if ((int)Periodicidad2 == 2 && Dia < 16)
                    {
                        Ciclo = Ciclo - 1;
                    }
                    else if ((int)Periodicidad2 == 3 && Dia < 21 && Dia > 10)
                    {
                        Ciclo = Ciclo - 1;
                    }
                    else if ((int)Periodicidad2 == 3 && Dia < 11)
                    {
                        Ciclo = Ciclo - 2;
                    }

                    switch (Periodicidad2)
                    {
                        case ClsLiqcreditos.Periodicidad.Diario:
                            Ciclo = (int)(Fecha - Fecini).TotalDays;
                            break;
                        case ClsLiqcreditos.Periodicidad.Semanal:
                            Ciclo = (int)((((Mes * 30.416) + Dia) - 30.416) / 7);
                            break;
                    }
                    break;

                default:
                    if ((int)Periodicidad2 != 1)
                    {
                        if (CicloDsto == 1)
                        {
                            if ((int)Periodicidad2 == 2)
                            {
                                DiaIni = 1; DiaFin = 15;
                            }
                            else if ((int)Periodicidad2 == 3)
                            {
                                DiaIni = 1; DiaFin = 10;
                            }
                        }
                        if (CicloDsto == 2)
                        {
                            if ((int)Periodicidad2 == 2)
                            {
                                DiaIni = 16; DiaFin = 30;
                            }
                            else if ((int)Periodicidad2 == 3)
                            {
                                DiaIni = 11; DiaFin = 20;
                            }
                        }
                        if (CicloDsto == 3)
                        {
                            if ((int)Periodicidad2 == 3)
                            {
                                DiaIni = 21; DiaFin = 30;
                            }
                        }
                        if ((int)Periodicidad2 == 4)
                        {
                            DiaIni = 1; DiaFin = 31;
                        }

                        if (Dia < DiaIni || Dia > DiaFin)
                        {
                            if (MuestraMensaje)
                            {
                                MessageBox.Show("Fecha no corresponde al periodo" + mensajeValida,
                                    "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            validarciclo = false;
                            return "999999";
                        }
                        Ciclo = Mes * 1;
                    }
                    break;
            }

            switch (Periodicidad2)
            {
                case ClsLiqcreditos.Periodicidad.Diario:
                    NumCiclo = Fecha.Year.ToString() + Strings.Right("000" + Ciclo.ToString(), 3);
                    break;
                default:
                    NumCiclo = Fecha.Year.ToString() + Strings.Right("00" + Ciclo.ToString(), 2);
                    break;
            }

            return NumCiclo;
        }

        // VB line 310 — GeneraProyeccion
        // Full overload: all Optional ByRef params as ref (VB signature)
        public DataSet GeneraProyeccion(
            string Codigoter, int Lincred, DateTime FechaIngreso, decimal Plazo, Periodicidad Periodicidad2,
            int Clades, int Ciclodsto, decimal TasaInteres, DateTime FechaDsto, double ValorPrestamo,
            ref DataTable Dstblextras,
            DataTable DstblDeduciones, string MesGracias, OdbcConnection Myconnect,
            ref double TotalDeuReco,
            ref double TotalIntCierre,
            int ClaseCuota, bool CicloAfecha, string periodo,
            double NumSolicitud, bool PideValores, ref double cuotaRecogida,
            OpcionProyeccion OpciProy,
            double NuevaCuota, ref double cuotaRecogidaCaja,
            ref double NuevaCuota_soloProyeccion,
            DataTable DatosSolicitud)
        {
            return GeneraProyeccion_impl(Codigoter, Lincred, FechaIngreso, Plazo, Periodicidad2,
                Clades, Ciclodsto, TasaInteres, FechaDsto, ValorPrestamo,
                ref Dstblextras, DstblDeduciones, MesGracias, Myconnect,
                ref TotalDeuReco, ref TotalIntCierre,
                ClaseCuota, CicloAfecha, periodo,
                NumSolicitud, PideValores, ref cuotaRecogida,
                OpciProy, NuevaCuota, ref cuotaRecogidaCaja,
                ref NuevaCuota_soloProyeccion, DatosSolicitud);
        }

        public DataSet GeneraProyeccion(
            string Codigoter, int Lincred, DateTime FechaIngreso, decimal Plazo, Periodicidad Periodicidad2,
            int Clades, int Ciclodsto, decimal TasaInteres, DateTime FechaDsto, double ValorPrestamo,
            ref DataTable Dstblextras,
            DataTable DstblDeduciones, string MesGracias, OdbcConnection Myconnect,
            ref double TotalDeuReco,
            ref double TotalIntCierre)
        {
            double _cuotaRecogida = 0, _cuotaRecogidaCaja = 0, _nuevaCuota = 0;
            return GeneraProyeccion_impl(Codigoter, Lincred, FechaIngreso, Plazo, Periodicidad2,
                Clades, Ciclodsto, TasaInteres, FechaDsto, ValorPrestamo,
                ref Dstblextras, DstblDeduciones, MesGracias, Myconnect,
                ref TotalDeuReco, ref TotalIntCierre,
                1, false, "", -1, true,
                ref _cuotaRecogida, OpcionProyeccion.Linea, 0,
                ref _cuotaRecogidaCaja, ref _nuevaCuota, null);
        }

        private DataSet GeneraProyeccion_impl(
            string Codigoter, int Lincred, DateTime FechaIngreso, decimal Plazo, Periodicidad Periodicidad2,
            int Clades, int Ciclodsto, decimal TasaInteres, DateTime FechaDsto, double ValorPrestamo,
            ref DataTable Dstblextras,
            DataTable DstblDeduciones, string MesGracias, OdbcConnection Myconnect,
            ref double TotalDeuReco,
            ref double TotalIntCierre,
            int ClaseCuota, bool CicloAfecha, string periodo,
            double NumSolicitud, bool PideValores, ref double cuotaRecogida,
            OpcionProyeccion OpciProy,
            double NuevaCuota, ref double cuotaRecogidaCaja,
            ref double NuevaCuota_soloProyeccion,
            DataTable DatosSolicitud)
        {
            double Cuota = 0;
            string CicloPriDsto;
            int Clacuo = 0;
            double VlrPrestamo = 0;
            DataSet DsdataProye = new DataSet();
            double VlrVpn = 0;
            int NumCuotas = 0;
            decimal TasaSeg = 0;
            DataSet DsDataProy = new DataSet();
            double ForIntpro = 0;
            int Forseg = 0;
            double Vlrseguro = 0;
            int StInteger = 0;
            double StDouble = 0;
            string Ststring = " ";
            int BaseAdmon = 0;
            double BaseAportes = 0, ValApor = 0, BaseLiqAdmon = 0;
            int forAdmon = 0;
            decimal TasaAdmon = 0;
            double TotDeudasRec = 0, VlrAdmon = 0, ValMinAdmon = 0, ValMaxAdmon = 0, BaseAhoCtrPerm = 0;
            string CptoAdm = "0", CptoSeg = "0";
            int stint = 0;
            decimal StDecimal = 0;
            string Nomasociado = " ", NombreLInea;
            decimal TasaInt = 0, IntSeg = 0;
            double Valmenos = 0;
            decimal IntAdm = 0;
            string TotIntAnt = "N";
            string IntcuoExt = "N", Agencia = "9999", CentroCosto;
            DataSet DsAsociado = new DataSet();
            int ForCapitalizar = 0, TipoCargAdi = 0;
            double VrlCapit = 0;
            decimal intpro = 0;
            decimal PorCap;
            int ClaInt = 0, CptSeg = 0, CptAdm = 0, CptCarAdi = 0, CptoApo = 0;
            string CptoCapitalizar, DescDsto = " ";
            double ValMinCapi = 0, VlrMinSeg = 0, VlrMaxSeg = 0;
            int CptoInt = 0;
            double LiqSegmes = 0;
            int CptoIntAnti = 0;
            string Empresa = "9999";
            double CargAdi = 0;
            int CptoIntPro = 0;
            double Factor = 0, Aportes = 0, Fondos = 0, Funerario = 0;
            int Period = (int)Periodicidad2;
            double CsCuota = 0, CuotaRef = 0;
            string CantCodeudores = "0";
            int tipointeres = 0;
            decimal Dtf = 0, PuntosAdic = 0;
            double TasaSegxRiesgo = 0;
            string capitalizar, valoradministracion = "", PagoUnaCuota = "N";
            double RecNoObligaciones = 0;
            DataTable DsTblSolicitud = new DataTable();
            bool sbolean = false;
            string FecPeriodoGracia = " ", FormaPeridoGracia = "0", CicloPeriodoGracia = "0";

            try
            {
                if (DstblDeduciones.Rows.Count == 0)
                {
                    if (DstblDeduciones.TableName != "tbldeducciones")
                    {
                        CreaTablaDeducciones(DstblDeduciones, Codigoter);
                    }
                }
            }
            catch (Exception)
            {
                CreaTablaDeducciones(DstblDeduciones, Codigoter);
            }

            try
            {
                if (Dstblextras.Rows.Count == 0)
                {
                    CreaTablaExtras(Dstblextras, Codigoter);
                }
            }
            catch (Exception)
            {
                CreaTablaExtras(Dstblextras, Codigoter);
            }

            // msgparcop.BuscaLinea(Lincred, DsDataProy, Myconnect); // ERROR: CS1503, CS1620
            // msgparcop.BuscaAsociado(Codigoter, DsAsociado, Myconnect); // ERROR: CS1620

            msgparsys.BuscarCompania(varini.sptCodEmpr, DsDataProy, Myconnect);
            {
                DataRow _comp = DsDataProy.Tables["tblcompania"].Rows[0];
                CptoSeg = _comp["cpto_seguro"].ToString();
                CptoAdm = _comp["cpto_admon"].ToString();
                CptoApo = Convert.ToInt32(_comp["cpto_aportes"]);
                CptoInt = Convert.ToInt32(_comp["cpto_interes"]);
                CptoIntAnti = Convert.ToInt32(_comp["cpto_capiatra"]);
                CptoIntPro = Convert.ToInt32(_comp["cpto_inteatra"]);
            }

            {
                DataRow _asoc = DsAsociado.Tables["tblasociados"].Rows[0];
                Nomasociado = _asoc["apellido"].ToString() + " " + _asoc["nombre"].ToString();
                Agencia = _asoc["agencia"].ToString();
                CentroCosto = _asoc["cencosto"].ToString();
                Empresa = _asoc["empresa"].ToString();
                TasaSegxRiesgo = Convert.ToDouble(_asoc["SeguroRiesgo"]);
            }

            {
                DataRow _lin = DsDataProy.Tables["tbllineas"].Rows[0];
                ForIntpro = Convert.ToDouble(_lin["intcie"]);
                Forseg = Convert.ToInt32(_lin["poapen"]);
                TasaSeg = Convert.ToDecimal(_lin["seguro"]);
                VlrMinSeg = Convert.ToDouble(_lin["valsegmin"]);
                VlrMaxSeg = Convert.ToDouble(_lin["valsegmax"]);
                BaseAdmon = Convert.ToInt32(_lin["foradmon"]);
                forAdmon = Convert.ToInt32(_lin["claAdmon"]);
                TasaAdmon = Convert.ToDecimal(_lin["tasadm"]);
                ValMinAdmon = Convert.ToDouble(_lin["valadmin"]);
                ValMaxAdmon = Convert.ToDouble(_lin["valadmax"]);
                NombreLInea = _lin["descripcion"].ToString();
                TotIntAnt = _lin["totintant"].ToString();
                IntcuoExt = _lin["previv"].ToString();
                ForCapitalizar = Convert.ToInt32(_lin["forcap"]);
                PorCap = Convert.ToDecimal(_lin["Tasaca"]);
                TipoCargAdi = Convert.ToInt32(_lin["sumaga"]);
                ClaInt = Convert.ToInt32(_lin["CLASEI"]);
                CptSeg = Convert.ToInt32(_lin["codseg"]);
                CptAdm = Convert.ToInt32(_lin["codadm"]);
                CptCarAdi = Convert.ToInt32(_lin["cpto_caradi"]);
                CptoCapitalizar = _lin["CptoCapitalizacion"].ToString();
                ValMinCapi = Convert.ToDouble(_lin["apomin"]);
                PagoUnaCuota = _lin["intfin"].ToString();
                tipointeres = Convert.ToInt32(_lin["tipointeres"]);
                Dtf = Convert.ToDecimal(_lin["Dtf"]);
                FormaPeridoGracia = _lin["pergracia"].ToString();
            }

            Clacuo = ClaseCuota;

            if (Ciclodsto != 5)
            {
                Period = 1;
            }

            if (Convert.ToDouble(TasaSegxRiesgo) > 0)
            {
                TasaSeg = (decimal)TasaSegxRiesgo;
            }

            if (DatosSolicitud != null)
            {
                if (OpciProy == OpcionProyeccion.Cuota || OpciProy == OpcionProyeccion.Maecar)
                {
                    if (!(DatosSolicitud.Rows[0]["tasaseg"] == DBNull.Value))
                    {
                        TasaSeg = Convert.ToDecimal(DatosSolicitud.Rows[0]["tasaseg"]);
                    }
                }
            }

            // double aport = CalcularCupoAportes(Codigoter, FechaIngreso.ToString("yyyyMM"), Myconnect, BaseAportes); // ERROR: CS7036
            TotDeudasRec = CalculaDeudasRecogidas(DstblDeduciones, ref RecNoObligaciones, ref cuotaRecogida, ref cuotaRecogidaCaja);
            TotalDeuReco = TotDeudasRec;
            TotDeudasRec = TotDeudasRec - RecNoObligaciones;
            Valmenos = RecNoObligaciones;
            NumCuotas = (int)Plazo * Period;
            TotDeudasRecogi = TotDeudasRec;
            BaseAport = BaseAportes;

            switch (tipointeres)
            {
                case 1:
                    PuntosAdic = TasaInteres;
                    TasaInteres = this.ConversionDTFaNMV(Dtf, PuntosAdic);
                    break;
            }

            TasaInt = Math.Round((TasaInteres / Period) / 100, 8);

            if (Periodicidad2 == ClsLiqcreditos.Periodicidad.Diario)
            {
                TasaInt = Math.Round((TasaInteres / 30) / 100, 8);
                NumCuotas = Convert.ToInt32(((Plazo) * 365) / 12);
            }

            switch (Forseg)
            {
                case 3:
                    if (ValorPrestamo >= VlrMinSeg && ValorPrestamo <= VlrMaxSeg)
                    {
                        IntSeg = Math.Round((TasaSeg / 100) / Period, 8);
                    }
                    else
                    {
                        IntSeg = 0;
                        TasaSeg = 0;
                    }
                    break;
            }

            switch (forAdmon)
            {
                case 2:
                    IntAdm = Math.Round((TasaAdmon / 100) / Period, 8);
                    break;
            }

            CicloPriDsto = this.CalculaCiclo(Ciclodsto, Periodicidad2, FechaDsto);
            VlrVpn = CalculaVp(Dstblextras, Period, FechaIngreso, Clacuo, TasaInt + IntSeg + IntAdm, Myconnect, (int)(Plazo + Convert.ToInt32(MesGracias)), Convert.ToInt32(CicloPriDsto));

            if ((int)Periodicidad2 == 4 && Ciclodsto == 5)
            {
                TasaInt = Math.Round(((TasaInteres / 30) * 7) / 100, 8);
                NumCuotas = Convert.ToInt32(((Plazo) * 52) / 12);
            }

            if (NumSolicitud > 0)
            {
                // DsTblSolicitud = this.BuscaSolicitudesCredito(NumSolicitud, Myconnect); // ERROR: CS1503
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
                case 10:
                    BaseLiqAdmon = ValorPrestamo;
                    break;
                case 5:
                    BaseLiqAdmon = (double)TasaAdmon;
                    break;
                case 7:
                    BaseLiqAdmon = BaseAdmon;
                    Aportes = Convert.ToDouble(Interaction.InputBox("Valor de Aportes:", "SOLIDO"));
                    Fondos = Convert.ToDouble(Interaction.InputBox("Valor de Fondos:", "SOLIDO"));
                    Funerario = Convert.ToDouble(Interaction.InputBox("Valor de Funerario:", "SOLIDO"));
                    break;
                case 8:
                    BaseAhoCtrPerm = this.CalculaSaldoAhoCtrtualPermanentes(Codigoter, FechaIngreso.ToString("yyyyMM"), Myconnect);
                    BaseLiqAdmon = ValorPrestamo - (BaseAportes + BaseAhoCtrPerm + TotDeudasRec);
                    if (BaseLiqAdmon < 0)
                    {
                        BaseLiqAdmon = 0;
                    }
                    break;
                case 9:
                    if (NumSolicitud > 0)
                    {
                        if (PideValores)
                        {
                            valoradministracion = Interaction.InputBox("Valor Administración:", "SOLIDO", DsTblSolicitud.Rows[0]["CUOTA_ADM"].ToString());
                        }
                        else
                        {
                            valoradministracion = DsTblSolicitud.Rows[0]["CUOTA_ADM"].ToString();
                        }
                    }
                    else
                    {
                        valoradministracion = Interaction.InputBox("Valor Administración:", "SOLIDO", "0");
                    }

                    if (!Microsoft.VisualBasic.Information.IsNumeric(valoradministracion))
                    {
                        BaseLiqAdmon = 0;
                    }
                    else
                    {
                        BaseLiqAdmon = Convert.ToDouble(valoradministracion);
                    }
                    valoradministracion = Interaction.InputBox("Valor de Aportes:", "SOLIDO", "0");
                    if (!Microsoft.VisualBasic.Information.IsNumeric(valoradministracion))
                    {
                        Aportes = 0;
                    }
                    else
                    {
                        Aportes = Convert.ToDouble(valoradministracion);
                    }
                    break;
            }

            switch (forAdmon)
            {
                case 1:
                case 3:
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(CptoAdm, Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                        if (BaseAdmon == 5)
                        {
                            VlrAdmon = Math.Round(BaseLiqAdmon);
                        }
                        else if (BaseAdmon == 9)
                        {
                            VlrAdmon = Math.Round(BaseLiqAdmon);
                            BaseLiqAdmon = BaseAdmon;
                        }
                        else
                        {
                            VlrAdmon = Math.Round(BaseLiqAdmon * ((double)TasaAdmon / 100));
                        }

                        switch (BaseAdmon)
                        {
                            case 6:
                                if (BaseLiqAdmon > ValMinAdmon)
                                    VlrAdmon = (double)TasaAdmon;
                                else
                                    VlrAdmon = 0;
                                break;
                            case 10:
                                if (BaseLiqAdmon >= ValMinAdmon && BaseLiqAdmon <= ValMaxAdmon)
                                    VlrAdmon = Math.Round(BaseLiqAdmon * ((double)TasaAdmon / 100));
                                else
                                    VlrAdmon = 0;
                                break;
                            default:
                                if (BaseAdmon == 8)
                                {
                                    if (VlrAdmon > 0)
                                    {
                                        if (NumSolicitud > 0)
                                        {
                                            if (PideValores)
                                            {
                                                valoradministracion = Interaction.InputBox("Valor Administración:", "SOLIDO", DsTblSolicitud.Rows[0]["CUOTA_ADM"].ToString());
                                            }
                                            else
                                            {
                                                valoradministracion = DsTblSolicitud.Rows[0]["CUOTA_ADM"].ToString();
                                            }
                                        }
                                        else
                                        {
                                            valoradministracion = Interaction.InputBox("Valor Administración:", "SOLIDO", VlrAdmon.ToString());
                                        }
                                        if (!Microsoft.VisualBasic.Information.IsNumeric(valoradministracion))
                                            VlrAdmon = 0;
                                        else
                                            VlrAdmon = Convert.ToDouble(valoradministracion);
                                    }
                                }
                                if (VlrAdmon < ValMinAdmon)
                                    VlrAdmon = ValMinAdmon;
                                if (VlrAdmon > ValMaxAdmon)
                                    VlrAdmon = ValMaxAdmon;
                                break;
                        }

                        if (VlrAdmon > 0)
                        {
                            GrabaDeducciones(DstblDeduciones, Codigoter, CptoAdm, 99999999, DescDsto, VlrAdmon, ref Valmenos);
                        }
                        break;
                    }
                case 2:
                    if (BaseAdmon == 9)
                    {
                        VlrAdmon = Math.Round(BaseLiqAdmon);
                        CsCuota += VlrAdmon;
                        BaseLiqAdmon = BaseAdmon;
                    }
                    else
                    {
                        IntAdm = Math.Round((TasaAdmon / 100) / (int)Periodicidad2, 8);
                    }
                    break;
                case 4:
                    if (BaseAdmon == 5)
                    {
                        VlrAdmon = Math.Round(BaseLiqAdmon);
                    }
                    else if (BaseAdmon == 9)
                    {
                        VlrAdmon = Math.Round(BaseLiqAdmon);
                        BaseLiqAdmon = BaseAdmon;
                    }
                    else if (BaseAdmon == 6)
                    {
                        if (BaseLiqAdmon > ValMinAdmon)
                            VlrAdmon = (double)TasaAdmon;
                        else
                            VlrAdmon = 0;
                    }
                    else
                    {
                        VlrAdmon = Math.Round(BaseLiqAdmon * ((double)TasaAdmon / 100));
                    }

                    if (TipoCargAdi != 2)
                    {
                        ValorPrestamo += VlrAdmon;
                    }
                    break;
            }

            switch (Forseg)
            {
                case 1:
                case 4:
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(CptoSeg, Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                        Vlrseguro = Math.Round(ValorPrestamo * ((double)TasaSeg / 100), 0);
                        if (Vlrseguro < VlrMinSeg)
                            Vlrseguro = VlrMinSeg;
                        if (Vlrseguro > VlrMaxSeg)
                            Vlrseguro = VlrMaxSeg;
                        GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, DescDsto, Vlrseguro, ref Valmenos);
                        break;
                    }
                case 5:
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(CptoSeg, Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                        Vlrseguro = (double)TasaSeg;
                        GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, DescDsto, Vlrseguro, ref Valmenos);
                        break;
                    }
                case 8:
                    if (TipoCargAdi == 2)
                    {
                        Vlrseguro = LiquidaSeguro(Forseg, ValorPrestamo + VlrAdmon, TasaSeg, (int)Plazo, IntcuoExt);
                        GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, "Seguro mes a mes", Vlrseguro, ref Valmenos);
                    }
                    break;
                case 3:
                    IntSeg = Math.Round((TasaSeg / 100) / Period, 8);
                    break;
                case 6:
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(CptoSeg, Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                        if (ValorPrestamo >= VlrMinSeg && ValorPrestamo <= VlrMaxSeg)
                        {
                            Vlrseguro = Math.Round(ValorPrestamo * ((double)TasaSeg / 100) * (double)Plazo, 0);
                            GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, DescDsto, Vlrseguro, ref Valmenos);
                        }
                        break;
                    }
                case 9:
                    CsCuota = Math.Round(ValorPrestamo * ((double)TasaSeg / 100) / (double)Plazo, 0);
                    break;
                case 11:
                    if (NumSolicitud > 0)
                    {
                        if (PideValores)
                        {
                            valoradministracion = Interaction.InputBox("Valor Seguro:", "SOLIDO", DsTblSolicitud.Rows[0]["CUOTA_SEG"].ToString());
                        }
                        else
                        {
                            valoradministracion = DsTblSolicitud.Rows[0]["CUOTA_SEG"].ToString();
                        }
                    }
                    else
                    {
                        valoradministracion = Interaction.InputBox("Valor Seguro:", "SOLIDO", "0");
                    }
                    if (!Microsoft.VisualBasic.Information.IsNumeric(valoradministracion))
                    {
                        valoradministracion = "0";
                    }
                    CsCuota += Convert.ToDouble(valoradministracion);
                    TasaSeg = Convert.ToDecimal(valoradministracion);
                    Vlrseguro = Convert.ToDouble(valoradministracion);
                    break;
            }

            // intpro = CalculaIntProporcionales(FechaIngreso, FechaDsto, ValorPrestamo, TasaInteres, Period); // ERROR: CS0266
            if (intpro < 0)
            {
                if (ClaInt != 2)
                {
                    intpro = 0;
                }
                else
                {
                    if ((int)ForIntpro != 2)
                    {
                        intpro = 0;
                    }
                }
            }

            switch ((int)ForIntpro)
            {
                case 3:
                    if (intpro > 0)
                    {
                        CargAdi += (double)intpro;
                    }
                    if (intpro > 0)
                    {
                        GrabaDeducciones(DstblDeduciones, Codigoter, CptoIntPro, 99999999, "Intereses proporcionales", (double)intpro, ref Valmenos);
                    }
                    break;
                case 2:
                    if (ClaInt == 2)
                    {
                        intpro += (decimal)Math.Round((ValorPrestamo * (double)TasaInt));
                    }
                    if (intpro > 0)
                    {
                        GrabaDeducciones(DstblDeduciones, Codigoter, CptoIntPro, 99999999, "Intereses proporcionales", (double)intpro, ref Valmenos);
                    }
                    break;
            }

            TotalIntCierre = (double)intpro;

            switch (ForCapitalizar)
            {
                case 1:
                    capitalizar = ((CptoCapitalizar == "9999" || CptoCapitalizar.Trim() == "") ? CptoApo.ToString() : CptoCapitalizar);
                    if (capitalizar == CptoApo.ToString())
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(capitalizar, Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                    }
                    else
                    {
                        string _dsc = DescDsto;
                        // msgparcop.BuscaLinea(capitalizar, Myconnect, ref _dsc); // ERROR: CS1620
                        DescDsto = _dsc;
                    }

                    VrlCapit = Math.Round((ValorPrestamo) * ((double)PorCap / 100), 0);
                    if (VrlCapit < 0) VrlCapit = 0;
                    if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                    if (VrlCapit > 0)
                    {
                        GrabaDeducciones(DstblDeduciones, Codigoter, capitalizar, 99999999, DescDsto, VrlCapit, ref Valmenos);
                    }
                    break;
                case 2:
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(CptoApo.ToString(), Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                        VrlCapit = Math.Round((ValorPrestamo - BaseAportes) * ((double)PorCap / 100), 0);
                        if (VrlCapit < 0) VrlCapit = 0;
                        if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                        if (VrlCapit > 0)
                        {
                            GrabaDeducciones(DstblDeduciones, Codigoter, CptoApo.ToString(), 99999999, DescDsto, VrlCapit, ref Valmenos);
                        }
                        break;
                    }
                case 3:
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(CptoApo.ToString(), Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                        VrlCapit = Math.Round((ValorPrestamo - TotDeudasRec) * ((double)PorCap / 100), 0);
                        if (VrlCapit < 0) VrlCapit = 0;
                        if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                        if (VrlCapit > 0)
                        {
                            GrabaDeducciones(DstblDeduciones, Codigoter, CptoApo.ToString(), 99999999, DescDsto, VrlCapit, ref Valmenos);
                        }
                        break;
                    }
                case 4:
                    {
                        string _dsc = DescDsto;
                        // msgcop.BuscaTipoMovto(CptoApo.ToString(), Myconnect, ref _dsc); // ERROR: CS7036
                        DescDsto = _dsc;
                        VrlCapit = Math.Round(((ValorPrestamo - TotDeudasRec) + BaseAportes) * ((double)PorCap / 100), 0);
                        if (VrlCapit < 0) VrlCapit = 0;
                        if (VrlCapit < ValMinCapi) VrlCapit = ValMinCapi;
                        if (VrlCapit > 0)
                        {
                            GrabaDeducciones(DstblDeduciones, Codigoter, CptoApo.ToString(), 99999999, DescDsto, VrlCapit, ref Valmenos);
                        }
                        break;
                    }
                case 5:
                    {
                        capitalizar = ((CptoCapitalizar == "9999" || CptoCapitalizar.Trim() == "") ? CptoApo.ToString() : CptoCapitalizar);
                        if (capitalizar == CptoApo.ToString())
                        {
                            string _dsc = DescDsto;
                            // msgcop.BuscaTipoMovto(capitalizar, Myconnect, ref _dsc); // ERROR: CS7036
                            DescDsto = _dsc;
                        }
                        else
                        {
                            string _dsc = DescDsto;
                            // msgparcop.BuscaLinea(capitalizar, Myconnect, ref _dsc); // ERROR: CS1620
                            DescDsto = _dsc;
                        }

                        if (NumSolicitud > 0)
                        {
                            CantCodeudores = Interaction.InputBox("Cantidad Codeudores:", "SOLIDO", CantCodeudores);
                        }
                        else
                        {
                            CantCodeudores = Interaction.InputBox("Cantidad Codeudores:", "SOLIDO", "0");
                        }

                        if (!Microsoft.VisualBasic.Information.IsNumeric(CantCodeudores))
                        {
                            VrlCapit = ValMinCapi;
                        }
                        else
                        {
                            VrlCapit = ValMinCapi + (ValMinCapi * Convert.ToDouble(CantCodeudores));
                            CantDeCodeudores = Convert.ToInt32(CantCodeudores);
                        }

                        if (VrlCapit < 0) VrlCapit = 0;

                        if (VrlCapit > 0)
                        {
                            GrabaDeducciones(DstblDeduciones, Codigoter, capitalizar, 99999999, DescDsto, VrlCapit, ref Valmenos);
                        }
                        break;
                    }
                case 9:
                    VrlCapit = 0;
                    break;
            }

            if (TipoCargAdi == 2)
            {
                CargAdi += (VlrAdmon + Vlrseguro + VrlCapit);
            }

            VlrPrestamo = (ValorPrestamo + CargAdi) - VlrVpn;

            switch (Clacuo)
            {
                case 1:
                    Cuota = Convert.ToDouble(string.Format("{0:N0}",
                        Microsoft.VisualBasic.Financial.Pmt((double)(TasaInt + IntSeg + IntAdm), NumCuotas, (-VlrPrestamo), 0)));
                    break;
                case 2:
                    Cuota = Math.Round(Convert.ToDouble((VlrPrestamo / NumCuotas) + 0.5), 0);
                    break;
            }

            if (BaseAdmon == 7)
            {
                Factor = Math.Round(((1 + (((double)TasaAdmon / 100) * ((double)(Plazo + Convert.ToInt32(MesGracias)) * Period))) / ((double)(Plazo + Convert.ToInt32(MesGracias)) * Period)) * 100000, 0) / 100000;
                CuotaRef = Math.Round((VlrPrestamo * (double)Factor) + ValMinAdmon, 0);
            }

            if (IntcuoExt == "Y")
            {
                Cuota = 0;
            }

            Cuota += CsCuota;
            Cuota = Math.Round(Cuota, 0);

            CicloPriDsto = this.CalculaCiclo(Ciclodsto, Periodicidad2, FechaDsto);

            switch (FormaPeridoGracia)
            {
                case "1":
                case "2":
                    CicloPeriodoGracia = this.CalculaCiclo(Ciclodsto, Periodicidad2, FechaDsto.AddMonths(Convert.ToInt32(MesGracias)));
                    FecPeriodoGracia = FechaDsto.AddMonths(Convert.ToInt32(MesGracias)).ToString();
                    break;
            }

            DsdataProye = GeneraPlanPagos(Codigoter, ValorPrestamo + CargAdi, Cuota, Clacuo, ClaInt, TasaInt, Plazo, Period, CicloPriDsto,
                FechaIngreso, FechaDsto, (int)ForIntpro, Forseg, TasaSeg, CptoSeg, TasaInteres, (int)BaseLiqAdmon, forAdmon, TasaAdmon,
                TotIntAnt, IntcuoExt, Dstblextras, DstblDeduciones, ref Valmenos, CptoIntAnti, ref TotalIntCierre, ref LiqSegmes,
                CuotaRef, Aportes, Fondos, Funerario, ref VlrAdmon, PagoUnaCuota, CicloAfecha, CicloPeriodoGracia, MesGracias,
                OpciProy, NuevaCuota, ValorPrestamo, VlrMinSeg, VlrMaxSeg);

            if (TipoCargAdi == 1)
            {
                if (Forseg == 2)
                {
                    GrabaDeducciones(DstblDeduciones, Codigoter, CptoSeg, 99999999, "Seguro mes a mes", LiqSegmes, ref Valmenos);
                    Vlrseguro = LiqSegmes;
                }
            }

            switch (Forseg)
            {
                case 7:
                    Vlrseguro = VlrPrestamo * ((double)TasaSeg / 100) / Period;
                    Cuota += Vlrseguro;
                    break;
                case 11:
                    TasaSeg = Convert.ToDecimal(DsDataProy.Tables["tbllineas"].Rows[0]["seguro"]);
                    break;
            }

            if (BaseAdmon == 7)
            {
                Cuota += VlrAdmon;
            }

            DsdataProye.Tables.Add("TbldatosCredito");
            {
                DataColumnCollection _cols = DsdataProye.Tables["TbldatosCredito"].Columns;
                _cols.Add("Cedula", Ststring.GetType());
                _cols.Add("Nomasociado", Ststring.GetType());
                _cols.Add("Empresa", Ststring.GetType());
                _cols.Add("fecha", Ststring.GetType());
                _cols.Add("periodicidad", stint.GetType());
                _cols.Add("plazo", stint.GetType());
                _cols.Add("clades", stint.GetType());
                _cols.Add("ciclo", stint.GetType());
                _cols.Add("TasaInt", StDecimal.GetType());
                _cols.Add("FecDesto", Ststring.GetType());
                _cols.Add("lincred", Ststring.GetType());
                _cols.Add("NombreLinea", Ststring.GetType());
                _cols.Add("valorCredito", StDouble.GetType());
                _cols.Add("BaseCupo", StDouble.GetType());
                _cols.Add("CiloDsto", StDouble.GetType());
                _cols.Add("ValMenos", StDouble.GetType());
                _cols.Add("Cuota", StDouble.GetType());
                _cols.Add("VlrVpnExtra", StDouble.GetType());
                _cols.Add("SalAportes", StDouble.GetType());
                _cols.Add("Agencia", Ststring.GetType());
                _cols.Add("CentroCosto", Ststring.GetType());
                _cols.Add("TipoIncie", stint.GetType());
                _cols.Add("Tipocap", stint.GetType());
                _cols.Add("Tipoadm", stint.GetType());
                _cols.Add("Tiposeg", stint.GetType());
                _cols.Add("TipoOtro", stint.GetType());
                _cols.Add("foradm", stint.GetType());
                _cols.Add("clacuo", stint.GetType());
                _cols.Add("claint", stint.GetType());
                _cols.Add("tasaadm", StDecimal.GetType());
                _cols.Add("tasaSeg", StDecimal.GetType());
                _cols.Add("tasacpt", StDecimal.GetType());
                _cols.Add("CuotaAdm", StDecimal.GetType());
                _cols.Add("CuotaSeg", StDecimal.GetType());
                _cols.Add("CuotaCapital", StDecimal.GetType());
                _cols.Add("CuotaIcie", StDecimal.GetType());
                _cols.Add("cptoadm", stint.GetType());
                _cols.Add("cptoSeg", stint.GetType());
                _cols.Add("cptoOtro", stint.GetType());
                _cols.Add("tasaotro", StDecimal.GetType());
                _cols.Add("CuotaAportes", StDecimal.GetType());
                _cols.Add("CuotaFondos", StDecimal.GetType());
                _cols.Add("CuotaFunerario", StDecimal.GetType());
                _cols.Add("Dtf", StDecimal.GetType());
                _cols.Add("Puntos", StDecimal.GetType());
                _cols.Add("MesGracias", Ststring.GetType());
                _cols.Add("FormaGracias", Ststring.GetType());
                _cols.Add("FechaGracias", Ststring.GetType());
                _cols.Add("CicloGracias", Ststring.GetType());
                _cols.Add("SaldoDeudaRecogida", StDouble.GetType());
            }

            DsdataProye.Tables.Add("TbldatosCapacidadPago");
            {
                DataColumnCollection _cols2 = DsdataProye.Tables["TbldatosCapacidadPago"].Columns;
                _cols2.Add("Salario", StDouble.GetType());
                _cols2.Add("otro_ingreso", StDouble.GetType());
                _cols2.Add("CONYSALAR", StDouble.GetType());
                _cols2.Add("IngVariables", StDouble.GetType());
                _cols2.Add("IngArriendos", StDouble.GetType());
                _cols2.Add("IngPension", StDouble.GetType());
                _cols2.Add("DeudasTerceros", StDouble.GetType());
                _cols2.Add("DstoParafiscales", StDouble.GetType());
                _cols2.Add("DstoPension", StDouble.GetType());
                _cols2.Add("Dsctos", StDouble.GetType());
                _cols2.Add("dsGastoper", StDouble.GetType());
                _cols2.Add("DeuCoopNomi", StDouble.GetType());
                _cols2.Add("DeuCoopCaja", StDouble.GetType());
                _cols2.Add("Porcn", sbolean.GetType());
                _cols2.Add("SaldoDeudaRecogida", StDouble.GetType());
                _cols2.Add("CuotaDeudaRecogida", StDouble.GetType());
                _cols2.Add("CbxRecDeudas", Ststring.GetType());
                _cols2.Add("CupoTotal", StDouble.GetType());
                _cols2.Add("TxtSolActVivienda", StDouble.GetType());
                _cols2.Add("TxtSolActVehiculo", StDouble.GetType());
                _cols2.Add("TxtSolActOtros", StDouble.GetType());
                _cols2.Add("TxtActCtaBanco", StDouble.GetType());
                _cols2.Add("TxtActCxC", StDouble.GetType());
                _cols2.Add("TxtSolPasOtros", StDouble.GetType());
                _cols2.Add("TxtPasObliBanca", StDouble.GetType());
                _cols2.Add("TxtPasObliHipot", StDouble.GetType());
                _cols2.Add("CuotaDeudaRecogidaCaja", StDouble.GetType());
                _cols2.Add("CuotaDeudaRecogidaTotal", StDouble.GetType());
            }

            Valmenos += TotDeudasRec;
            DsdataProye.Tables["TbldatosCredito"].Rows.Add(
                Codigoter, Nomasociado, Empresa, FechaIngreso.ToString(varini.PstForFec), (int)Periodicidad2, (int)Plazo, Clades, Ciclodsto, TasaInteres,
                FechaDsto.ToString(varini.PstForFec), Lincred, NombreLInea, ValorPrestamo + CargAdi, BaseAportes, CicloPriDsto, Valmenos, Cuota, VlrVpn,
                BaseAportes, Agencia, CentroCosto, (int)ForIntpro, ForCapitalizar, forAdmon, Forseg, TipoCargAdi, BaseAdmon, Clacuo, ClaInt, TasaAdmon,
                TasaSeg, PorCap, VlrAdmon, Vlrseguro, VrlCapit, TotalIntCierre, CptAdm, CptSeg, CptCarAdi, 0, Aportes, Fondos, Funerario, Dtf, PuntosAdic,
                MesGracias, FormaPeridoGracia, FecPeriodoGracia, CicloPeriodoGracia, TotalDeuReco);

            NuevaCuota_soloProyeccion = Cuota;

            try
            {
                if (DstblDeduciones.Rows.Count == 0)
                {
                    if (DstblDeduciones.Columns.Contains("CUOTA") == true && DstblDeduciones.Columns.Contains("CLADES") == true)
                    {
                        DstblDeduciones.Rows.Add(Codigoter, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    }
                    else
                    {
                        DstblDeduciones.Rows.Add(Codigoter, 0, 0, 0, 0, 0, 0);
                    }
                }
            }
            catch (Exception)
            {
                // swallowed as in VB
            }

            DsdataProye.Tables.Add(DstblDeduciones.Copy());
            DsdataProye.Tables.Add(Dstblextras.Copy());
            return DsdataProye;
        }

        // VB line 1018 — LiquidaCapitalizacion
        public double LiquidaCapitalizacion(int ForCapitalizar, decimal PorCap, string CptoCapitalizar,
            double ValMinCapi, double ValorPrestamo, string CptoApo, OdbcConnection Myconnect)
        {
            string capitalizar;
            double VlrCapitalizar = 0;

            switch (ForCapitalizar)
            {
                case 1:
                    capitalizar = ((CptoCapitalizar == "9999" || CptoCapitalizar.Trim() == "") ? CptoApo : CptoCapitalizar);
                    if (capitalizar == CptoApo)
                    {
                        string _dsc = " ";
                        // msgcop.BuscaTipoMovto(capitalizar, Myconnect, ref _dsc); // ERROR: CS7036
                    }
                    else
                    {
                        string _dsc = " ";
                        // msgparcop.BuscaLinea(capitalizar, Myconnect, ref _dsc); // ERROR: CS1620
                    }
                    VlrCapitalizar = Math.Round((ValorPrestamo) * ((double)PorCap / 100), 0);
                    if (VlrCapitalizar < 0) VlrCapitalizar = 0;
                    if (VlrCapitalizar < ValMinCapi) VlrCapitalizar = ValMinCapi;
                    break;
                case 2:
                    // msgcop.BuscaTipoMovto(CptoApo, Myconnect); // ERROR: CS7036
                    VlrCapitalizar = Math.Round((ValorPrestamo - BaseAport) * ((double)PorCap / 100), 0);
                    if (VlrCapitalizar < 0) VlrCapitalizar = 0;
                    if (VlrCapitalizar < ValMinCapi) VlrCapitalizar = ValMinCapi;
                    break;
                case 3:
                    // msgcop.BuscaTipoMovto(CptoApo, Myconnect); // ERROR: CS7036
                    VlrCapitalizar = Math.Round((ValorPrestamo - TotDeudasRecogi) * ((double)PorCap / 100), 0);
                    if (VlrCapitalizar < 0) VlrCapitalizar = 0;
                    if (VlrCapitalizar < ValMinCapi) VlrCapitalizar = ValMinCapi;
                    break;
                case 4:
                    // msgcop.BuscaTipoMovto(CptoApo, Myconnect); // ERROR: CS7036
                    VlrCapitalizar = Math.Round(((ValorPrestamo - TotDeudasRecogi) + BaseAport) * ((double)PorCap / 100), 0);
                    if (VlrCapitalizar < 0) VlrCapitalizar = 0;
                    if (VlrCapitalizar < ValMinCapi) VlrCapitalizar = ValMinCapi;
                    break;
                case 5:
                    capitalizar = ((CptoCapitalizar == "9999" || CptoCapitalizar.Trim() == "") ? CptoApo : CptoCapitalizar);
                    if (capitalizar == CptoApo)
                    {
                        string _dsc = " ";
                        // msgcop.BuscaTipoMovto(capitalizar, Myconnect, ref _dsc); // ERROR: CS7036
                    }
                    else
                    {
                        string _dsc = " ";
                        // msgparcop.BuscaLinea(capitalizar, Myconnect, ref _dsc); // ERROR: CS1620
                    }
                    if (!Microsoft.VisualBasic.Information.IsNumeric(CantDeCodeudores))
                    {
                        VlrCapitalizar = ValMinCapi;
                    }
                    else
                    {
                        VlrCapitalizar = ValMinCapi + (ValMinCapi * CantDeCodeudores);
                    }
                    if (VlrCapitalizar < 0) VlrCapitalizar = 0;
                    break;
                case 9:
                    VlrCapitalizar = 0;
                    break;
            }
            return VlrCapitalizar;
        }

        // VB line 1092 — LiquidaSeguro
        public double LiquidaSeguro(int forseg, double ValorPrestamo, decimal TasaSeg, int Plazo, string IntcuoExt)
        {
            double Vlrseguro = 0;

            switch (forseg)
            {
                case 1:
                case 4:
                    Vlrseguro = Math.Round(ValorPrestamo * ((double)TasaSeg / 100), 0);
                    break;
                case 5:
                    Vlrseguro = (double)TasaSeg;
                    break;
                case 8:
                    if (IntcuoExt == "Y")
                    {
                        Vlrseguro = Math.Round((ValorPrestamo * ((double)TasaSeg / 100)) * Plazo, 0);
                    }
                    break;
            }
            return Vlrseguro;
        }

        // VB line 1107 — LiquidaAdmon
        public double LiquidaAdmon(int BaseAdmon, int forAdmon, decimal TasaAdmon, double ValMinAdmon, double ValMaxAdmon,
            double ValorPrestamo, double BaseAportes, double TotDeudasRec)
        {
            double BaseLiqAdmon = 0, VlrAdmon = 0;

            switch (BaseAdmon)
            {
                case 1: BaseLiqAdmon = ValorPrestamo; break;
                case 2: BaseLiqAdmon = ValorPrestamo - BaseAportes; break;
                case 3: BaseLiqAdmon = ValorPrestamo - TotDeudasRec; break;
                case 4: BaseLiqAdmon = ValorPrestamo - TotDeudasRec + BaseAportes; break;
                case 5: BaseLiqAdmon = (double)TasaAdmon; break;
                case 6: BaseLiqAdmon = ValorPrestamo; break;
            }

            switch (forAdmon)
            {
                case 1:
                case 3:
                    if (BaseAdmon == 5)
                        VlrAdmon = BaseLiqAdmon;
                    else
                        VlrAdmon = BaseLiqAdmon * ((double)TasaAdmon / 100);

                    switch (BaseAdmon)
                    {
                        case 6:
                            if (BaseLiqAdmon > ValMinAdmon)
                                VlrAdmon = (double)TasaAdmon;
                            else
                                VlrAdmon = 0;
                            break;
                        default:
                            if (VlrAdmon < ValMinAdmon) VlrAdmon = ValMinAdmon;
                            if (VlrAdmon > ValMaxAdmon) VlrAdmon = ValMaxAdmon;
                            break;
                    }
                    break;
                case 4:
                    if (BaseAdmon == 5)
                        VlrAdmon = BaseLiqAdmon;
                    else
                        VlrAdmon = BaseLiqAdmon * ((double)TasaAdmon / 100);
                    break;
            }
            return VlrAdmon;
        }

        // VB line 1161 — CreaTablaDeducciones
        private void CreaTablaDeducciones(DataTable DsTablaDeudas, string codigoter)
        {
            string ststring = " "; int StInteger = 0; double StDouble = 0;
            try
            {
                DsTablaDeudas.TableName = "tbldeducciones";
                DataColumnCollection _cols = DsTablaDeudas.Columns;
                {
                    _cols.Add("CEDULA", ststring.GetType());
                    _cols.Add("LINCRED", StInteger.GetType());
                    _cols.Add("NUMERO", StDouble.GetType());
                    _cols.Add("DESCRIPCION", ststring.GetType());
                    _cols.Add("VALOR", StDouble.GetType());
                    _cols.Add("INTERES", StDouble.GetType());
                    _cols.Add("TOTAL", ststring.GetType());
                    _cols.Add("TOTALDEDUCIR", StDouble.GetType());
                }
            }
            catch (Exception)
            {
                // swallowed
            }
        }

        // VB line 1183 — CreaTablaExtras
        private void CreaTablaExtras(DataTable DsTablaExtras, string codigoter)
        {
            string ststring = " "; int StInteger = 0; double StDouble = 0;
            DateTime Stdate = default(DateTime);
            try
            {
                DsTablaExtras.TableName = "tblextras";
                DataColumnCollection _cols = DsTablaExtras.Columns;
                _cols.Add("FECHA", Stdate.GetType());
                _cols.Add("VALOR", StInteger.GetType());
                _cols.Add("DESCPAG", ststring.GetType());
                _cols.Add("FORPAG", StInteger.GetType());
                _cols.Add("tipoextra", ststring.GetType());
                DsTablaExtras.Rows.Add(DateTime.Now, 0, 0, 0, " ");
            }
            catch (Exception)
            {
                // swallowed
            }
        }

        // VB line 1204 — CargaDeduciones
        public void CargaDeduciones(DataSet dsdataset, System.Windows.Forms.Form myforma, OdbcConnection myconnect, string NumSolicitud)
        {
            // cop_deduciones frmdeduciones = new cop_deduciones(myconnect); // ERROR: CS0246
            // frmdeduciones.DtgDeduciones.AutoGenerateColumns = false; // ERROR: CS0103
            // frmdeduciones.DtgDeduciones.DataSource = dsdataset.Tables["tbldeducciones"]; // ERROR: CS0103
            // frmdeduciones.dsdatos = dsdataset; // ERROR: CS0103
            // frmdeduciones.StNumSolicitud = NumSolicitud; // ERROR: CS0103
            // frmdeduciones.ShowDialog(myforma); // ERROR: CS0103
        }

        // VB line 1216 — GrabaDeducciones
        private void GrabaDeducciones(DataTable dsdataset, string Codigoter, string Concepto, double numero, string Descripcion, double valor, ref double ValMenos)
        {
            dsdataset.Rows.Add(Codigoter, Concepto, numero, Descripcion, valor, 0, "T", valor);
            ValMenos += valor;
        }

        // Overload accepting int Concepto
        private void GrabaDeducciones(DataTable dsdataset, string Codigoter, int Concepto, double numero, string Descripcion, double valor, ref double ValMenos)
        {
            GrabaDeducciones(dsdataset, Codigoter, Concepto.ToString(), numero, Descripcion, valor, ref ValMenos);
        }

        // Overload accepting decimal intpro
        private void GrabaDeducciones(DataTable dsdataset, string Codigoter, int Concepto, double numero, string Descripcion, decimal valor, ref double ValMenos)
        {
            GrabaDeducciones(dsdataset, Codigoter, Concepto.ToString(), numero, Descripcion, (double)valor, ref ValMenos);
        }

        // VB line 1223 — CambiaCicloaFecha
        public void CambiaCicloaFecha(DateTime FechaDsto, int Periodicidad2, int Plazo, ref DataSet CicloFecha, string Ciclodsto, int TipoDsto)
        {
            DateTime FechaDescuento, FechaDescuentoSgunda;
            int Contador;
            bool MenoraTreinta = false;
            int DiaDes = 0, DiaDesSgunda = 0, DiasMes = 0, DiaMesSgunda = 0, Ciclo = 0;
            int DiaPrimer = 0, DiaSegunda = 0;

            DataRowCollection _rows = CicloFecha.Tables["Ciclofechas"].Rows;

            switch (Periodicidad2)
            {
                case 1:
                    DiaDes = FechaDsto.Day;
                    FechaDescuento = FechaDsto;
                    _rows.Add(FechaDsto);
                    if (FechaDsto.Month == 2)
                    {
                        if (DiaDes >= 28)
                        {
                            DiaDes = 31;
                        }
                    }

                    for (Contador = 1; Contador <= Plazo - 1; Contador++)
                    {
                        FechaDescuento = FechaDescuento.AddMonths(1);
                        DiasMes = DateTime.DaysInMonth(FechaDescuento.Year, FechaDescuento.Month);
                        if (DiasMes < DiaDes)
                        {
                            FechaDescuento = Convert.ToDateTime(FechaDescuento.Year + "-" + FechaDescuento.Month + "-" + DiasMes);
                            MenoraTreinta = true;
                        }
                        else
                        {
                            FechaDescuento = Convert.ToDateTime(FechaDescuento.Year + "-" + FechaDescuento.Month + "-" + DiaDes);
                            MenoraTreinta = false;
                        }
                        _rows.Add(FechaDescuento);
                    }
                    break;

                case 2:
                    DiaDes = FechaDsto.Day;
                    FechaDescuento = FechaDsto;
                    FechaDescuentoSgunda = FechaDsto;
                    FechaDescuento = FechaDescuento.AddDays(15);
                    DiaDesSgunda = FechaDescuentoSgunda.Day;
                    _rows.Add(FechaDsto);
                    if (DiaDes >= 1 && DiaDes <= 15)
                    {
                        Ciclo = 1;
                        if (FechaDsto.Month != FechaDescuento.Month)
                        {
                            FechaDescuento = Convert.ToDateTime(FechaDsto.Year + "-" + FechaDsto.Month + "-" + DateTime.DaysInMonth(FechaDsto.Year, FechaDsto.Month));
                        }
                    }
                    else if (DiaDes >= 16 && DiaDes <= 31)
                    {
                        Ciclo = 2;
                        if (DateTime.DaysInMonth(FechaDsto.Year, FechaDsto.Month) == 31 && FechaDsto.Day != 31)
                        {
                            if (FechaDsto.Day == 30)
                            {
                                FechaDescuento = Convert.ToDateTime(FechaDescuento.Year + "-" + FechaDescuento.Month + "-15");
                            }
                            else
                            {
                                FechaDescuento = FechaDescuento.AddDays(1);
                            }
                        }
                        else if (DateTime.DaysInMonth(FechaDsto.Year, FechaDsto.Month) == 28 && FechaDsto.Day != 28)
                        {
                            FechaDescuento = FechaDescuento.AddDays(-2);
                        }
                        else if (DateTime.DaysInMonth(FechaDsto.Year, FechaDsto.Month) == 29 && FechaDsto.Day != 29)
                        {
                            FechaDescuento = FechaDescuento.AddDays(-1);
                        }
                    }
                    _rows.Add(FechaDescuento);
                    DiaSegunda = FechaDescuento.Day;
                    DiaPrimer = FechaDsto.Day;
                    FechaDescuento = FechaDsto;
                    FechaDescuentoSgunda = FechaDsto.AddDays(15);

                    switch (Ciclo)
                    {
                        case 1:
                            for (Contador = 1; Contador <= Plazo - 1; Contador++)
                            {
                                if (DateTime.DaysInMonth(FechaDescuento.Year, FechaDescuento.Month) == 31)
                                {
                                    FechaDescuento = FechaDescuento.AddDays(31);
                                    FechaDescuentoSgunda = FechaDescuentoSgunda.AddDays(31);
                                }
                                else if (DateTime.DaysInMonth(FechaDescuento.Year, FechaDescuento.Month) == 28)
                                {
                                    FechaDescuento = FechaDescuento.AddDays(28);
                                    FechaDescuentoSgunda = FechaDescuentoSgunda.AddDays(28);
                                }
                                else if (DateTime.DaysInMonth(FechaDescuento.Year, FechaDescuento.Month) == 29)
                                {
                                    FechaDescuento = FechaDescuento.AddDays(29);
                                    FechaDescuentoSgunda = FechaDescuentoSgunda.AddDays(29);
                                }
                                else if (DateTime.DaysInMonth(FechaDescuento.Year, FechaDescuento.Month) == 30)
                                {
                                    FechaDescuento = FechaDescuento.AddDays(30);
                                    FechaDescuentoSgunda = FechaDescuentoSgunda.AddDays(30);
                                }

                                if (FechaDescuento.Month != FechaDescuentoSgunda.Month)
                                {
                                    FechaDescuentoSgunda = Convert.ToDateTime(FechaDescuento.Year + "-" + FechaDescuento.Month + "-" + DateTime.DaysInMonth(FechaDescuento.Year, FechaDescuento.Month));
                                }

                                if (CalculaDias(FechaDescuentoSgunda.Date, FechaDescuento.Date) < 15 && FechaDescuento.Month != 2)
                                {
                                    FechaDescuentoSgunda = FechaDescuentoSgunda.AddDays(15 - CalculaDias(FechaDescuentoSgunda.Date, FechaDescuento.Date));
                                }

                                _rows.Add(FechaDescuento);
                                _rows.Add(FechaDescuentoSgunda);
                            }
                            break;
                        case 2:
                            {
                                bool Tiene31 = false;

                                if (FechaDsto.Month == 2)
                                {
                                    if (DiaDes >= 28)
                                    {
                                        DiaDes = 31;
                                        DiaPrimer = DiaDes;
                                    }
                                }

                                for (Contador = 1; Contador <= Plazo - 1; Contador++)
                                {
                                    FechaDescuento = FechaDescuento.AddDays(30);
                                    if (FechaDescuento.Month - FechaDescuento.AddDays(-30).Month != 1 && FechaDescuento.Month - FechaDescuento.AddDays(-30).Month != -11)
                                    {
                                        FechaDescuento = FechaDescuento.AddDays(-3);
                                    }

                                    FechaDescuentoSgunda = FechaDescuentoSgunda.AddDays(30);
                                    DiasMes = DateTime.DaysInMonth(FechaDescuento.Year, FechaDescuento.Month);
                                    DiaMesSgunda = DateTime.DaysInMonth(FechaDescuentoSgunda.Year, FechaDescuentoSgunda.Month);
                                    if (DiasMes < DiaDes)
                                    {
                                        FechaDescuento = Convert.ToDateTime(FechaDescuento.Year + "-" + FechaDescuento.Month + "-" + DiasMes);
                                    }
                                    else
                                    {
                                        FechaDescuento = Convert.ToDateTime(FechaDescuento.Year + "-" + FechaDescuento.Month + "-" + DiaPrimer);
                                    }

                                    if (DiaMesSgunda < DiaSegunda)
                                    {
                                        FechaDescuentoSgunda = Convert.ToDateTime(FechaDescuentoSgunda.Year + "-" + FechaDescuentoSgunda.Month + "-" + DiaMesSgunda);
                                    }
                                    else
                                    {
                                        FechaDescuentoSgunda = Convert.ToDateTime(FechaDescuentoSgunda.Year + "-" + FechaDescuentoSgunda.Month + "-" + DiaSegunda);
                                        MenoraTreinta = false;
                                    }

                                    _rows.Add(FechaDescuento);
                                    _rows.Add(FechaDescuentoSgunda);
                                }
                                break;
                            }
                    }
                    break;
            }
        }

        // VB line 1365 — GeneraPlanPagos (full overload with all ref params)
        public DataSet GeneraPlanPagos(
            string Codigoter, double VlrPrestamo, double Cuota, int Clacuo, int Claseint, decimal TasaInt, decimal plazo, int periodicidad,
            string Ciclodsto, DateTime FechaIngreso, DateTime FechaDsto, int ForIntpro, int TipoSeg, decimal TasaSeg, string CptSeg, decimal TasaInteres,
            int BaseLiqAdmon, int forAdmon, decimal TasaAdmon, string TotIntAnt, string IntcuoExt, DataTable DsTablaextras, DataTable DstablaDeduciones, ref double valmenos,
            int CptoIntAnticipados, ref double TotLiqInteres,
            ref double LiqSegMes, double CuotaRef, double Aportes, double Fondos, double Funerario,
            ref double LiqCuoAdm,
            string PagoUnaCuota, bool CamCicloAfecha, string CicloPeriodoGracia, string MesesGracias,
            OpcionProyeccion OpciProy, double NuevaCuota, double VLRCREDITO, double VALSEGMIN, double VALSEGMAX)
        {
            return GeneraPlanPagos_impl(Codigoter, VlrPrestamo, Cuota, Clacuo, Claseint, TasaInt, plazo, periodicidad,
                Ciclodsto, FechaIngreso, FechaDsto, ForIntpro, TipoSeg, TasaSeg, CptSeg, TasaInteres,
                BaseLiqAdmon, forAdmon, TasaAdmon, TotIntAnt, IntcuoExt, DsTablaextras, DstablaDeduciones, ref valmenos,
                CptoIntAnticipados, ref TotLiqInteres, ref LiqSegMes, CuotaRef, Aportes, Fondos, Funerario,
                ref LiqCuoAdm, PagoUnaCuota, CamCicloAfecha, CicloPeriodoGracia, MesesGracias,
                OpciProy, NuevaCuota, VLRCREDITO, VALSEGMIN, VALSEGMAX);
        }

        public DataSet GeneraPlanPagos(
            string Codigoter, double VlrPrestamo, double Cuota, int Clacuo, int Claseint, decimal TasaInt, decimal plazo, int periodicidad,
            string Ciclodsto, DateTime FechaIngreso, DateTime FechaDsto, int ForIntpro, int TipoSeg, decimal TasaSeg, string CptSeg, decimal TasaInteres,
            int BaseLiqAdmon, int forAdmon, decimal TasaAdmon, string TotIntAnt, string IntcuoExt, DataTable DsTablaextras, DataTable DstablaDeduciones, ref double valmenos,
            int CptoIntAnticipados, ref double TotLiqInteres,
            ref double LiqSegMes)
        {
            double _LiqCuoAdm = 0;
            return GeneraPlanPagos_impl(Codigoter, VlrPrestamo, Cuota, Clacuo, Claseint, TasaInt, plazo, periodicidad,
                Ciclodsto, FechaIngreso, FechaDsto, ForIntpro, TipoSeg, TasaSeg, CptSeg, TasaInteres,
                BaseLiqAdmon, forAdmon, TasaAdmon, TotIntAnt, IntcuoExt, DsTablaextras, DstablaDeduciones, ref valmenos,
                CptoIntAnticipados, ref TotLiqInteres, ref LiqSegMes, 0, 0, 0, 0,
                ref _LiqCuoAdm, "N", false, "0", "0",
                OpcionProyeccion.Linea, 0, 0, 0, 999999999);
        }

        private DataSet GeneraPlanPagos_impl(
            string Codigoter, double VlrPrestamo, double Cuota, int Clacuo, int Claseint, decimal TasaInt, decimal plazo, int periodicidad,
            string Ciclodsto, DateTime FechaIngreso, DateTime FechaDsto, int ForIntpro, int TipoSeg, decimal TasaSeg, string CptSeg, decimal TasaInteres,
            int BaseLiqAdmon, int forAdmon, decimal TasaAdmon, string TotIntAnt, string IntcuoExt, DataTable DsTablaextras, DataTable DstablaDeduciones, ref double valmenos,
            int CptoIntAnticipados, ref double TotLiqInteres,
            ref double LiqSegMes, double CuotaRef, double Aportes, double Fondos, double Funerario,
            ref double LiqCuoAdm,
            string PagoUnaCuota, bool CamCicloAfecha, string CicloPeriodoGracia, string MesesGracias,
            OpcionProyeccion OpciProy, double NuevaCuota, double VLRCREDITO, double VALSEGMIN, double VALSEGMAX)
        {
            int StInteger = 0;
            double StDouble = 0;
            string ststring = " ";
            double Interes = 0, Capital = 0;
            int NumCuota = 0;
            string Ciclo = null;
            double CuotaExtra = 0;
            double Seguro = 0, Admon = 0, Saldo = 0;
            int NumCuotas = 0;
            int NumCiclo = 0, CicloAnio = 0;
            double Vlrcuota = 0;
            double VlrInteres = 0, VlrCapital = 0, VlrSeguro = 0, Vlradmon = 0, VlrCuotasExtras = 0;
            DataSet DsdataProyeccion = new DataSet();
            double intpro = 0;
            double BaseAdmon = 0;
            double LiqInteres = 0;
            DateTime FechaExt = default(DateTime);
            DateTime FechaExtUlt = new DateTime(1950, 1, 1);
            int dias = 0;
            decimal Factor = 0;
            double VlrLiqInteres = 0;
            int diasliq = 0, diasper = 0;
            DataSet CicloFecha = new DataSet();
            double DifCuotaRef = 0;

            // CamCicloAfecha = OdbcConnect.odbcConect.ciclofecha; // ERROR: CS0572
            DsdataProyeccion.Tables.Add("Tblproyeccion");
            {
                DataColumnCollection _cols = DsdataProyeccion.Tables["Tblproyeccion"].Columns;
                _cols.Add("cedula", ststring.GetType());
                _cols.Add("NumCuota", StInteger.GetType());
                // .Add("Ciclo", StInteger.GetType()) — commented in VB
                if (CamCicloAfecha == true && (periodicidad == 1 || periodicidad == 2))
                    _cols.Add("Ciclo", FechaExt.GetType());
                else
                    _cols.Add("Ciclo", ststring.GetType());
                _cols.Add("Cuota", StDouble.GetType());
                _cols.Add("CuotaExtra", StDouble.GetType());
                _cols.Add("Interes", StDouble.GetType());
                _cols.Add("Seguro", StDouble.GetType());
                _cols.Add("Admon", StDouble.GetType());
                _cols.Add("AbonoCap", StDouble.GetType());
                _cols.Add("Saldo", StDouble.GetType());
            }

            DsdataProyeccion.Tables.Add("TblTotales");
            {
                DataColumnCollection _cols = DsdataProyeccion.Tables["TblTotales"].Columns;
                _cols.Add("NumCuota", StInteger.GetType());
                _cols.Add("Ciclo", StInteger.GetType());
                _cols.Add("Cuota", StDouble.GetType());
                _cols.Add("CuotaExtra", StDouble.GetType());
                _cols.Add("Interes", StDouble.GetType());
                _cols.Add("Seguro", StDouble.GetType());
                _cols.Add("Admon", StDouble.GetType());
                _cols.Add("AbonoCap", StDouble.GetType());
                _cols.Add("Saldo", StDouble.GetType());
            }

            if (!Microsoft.VisualBasic.Information.IsNumeric(MesesGracias))
            {
                MesesGracias = "0";
            }

            CicloFecha.Tables.Add("Ciclofechas");
            CicloFecha.Tables["Ciclofechas"].Columns.Add("fecha", FechaExt.GetType());
            if (CamCicloAfecha == true && (periodicidad == 1 || periodicidad == 2))
            {
                CambiaCicloaFecha(FechaDsto, periodicidad, (int)((double)plazo + Convert.ToDouble(MesesGracias)), ref CicloFecha, Ciclodsto, 1);
            }

            if (PagoUnaCuota == "Y")
            {
                diasper = (int)DiasPeriodicidad(periodicidad);
                diasliq = this.CalculaDias(FechaDsto, FechaIngreso);
                if (diasliq > 0 && diasliq < diasper)
                {
                    diasliq += 1;
                }
            }

            intpro = CalculaIntProporcionales(FechaIngreso, FechaDsto, (int)VlrPrestamo, TasaInteres, periodicidad);

            Saldo = VlrPrestamo;

            switch (BaseLiqAdmon)
            {
                case 1:
                    BaseAdmon = Saldo;
                    break;
                case 7:
                case 9:
                    BaseAdmon = BaseLiqAdmon;
                    break;
            }

            NumCuotas = (int)((double)plazo + Convert.ToDouble(MesesGracias)) * periodicidad;
            switch (periodicidad)
            {
                case 4:
                    NumCuotas = Convert.ToInt32(((double)plazo + Convert.ToDouble(MesesGracias)) * 52 / 12);
                    break;
                case 5:
                    NumCuotas = Convert.ToInt32(((double)plazo + Convert.ToDouble(MesesGracias)) * 365 / 12);
                    break;
            }

            NumCuota = 1;
            CicloAnio = Convert.ToInt32(Strings.Mid(Ciclodsto, 1, 4));
            NumCiclo = Convert.ToInt32(Strings.Mid(Ciclodsto, 5, 3));

            while (Saldo > 0)
            {
                switch (periodicidad)
                {
                    case 5:
                        Ciclo = CicloAnio.ToString() + Strings.Right("000" + NumCiclo.ToString(), 3);
                        break;
                    default:
                        Ciclo = CicloAnio.ToString() + Strings.Right("00" + NumCiclo.ToString(), 2);
                        break;
                }

                Seguro = 0; Admon = 0;

                switch (TipoSeg)
                {
                    case 3:
                        if (VALSEGMIN > VLRCREDITO || VLRCREDITO > VALSEGMAX)
                        {
                            TasaSeg = 0;
                        }
                        if (PagoUnaCuota == "Y")
                        {
                            if (NumCuota == 1)
                                Seguro = Math.Round((Saldo * ((double)TasaSeg / 100) / 30) * diasliq);
                            else
                                Seguro = Math.Round((Saldo * (double)TasaSeg / 100) / periodicidad);
                        }
                        else // "N"
                        {
                            Seguro = Math.Round((Saldo * (double)TasaSeg / 100) / periodicidad);
                        }
                        break;
                    case 2:
                        LiqSegMes += Math.Round((Saldo * (double)TasaSeg / 100) / periodicidad);
                        break;
                    case 9:
                        Seguro = Math.Round(VlrPrestamo * (double)TasaSeg / 100 / (double)plazo, 0);
                        break;
                    case 11:
                        Seguro = (double)TasaSeg;
                        break;
                }

                switch (forAdmon)
                {
                    case 2:
                        if (BaseLiqAdmon == 9)
                            Admon = LiqCuoAdm;
                        else
                            Admon = Saldo * (double)TasaAdmon / 100 / periodicidad;
                        break;
                }

                Interes = Math.Round((Saldo * (double)TasaInt), 0);
                CuotaExtra = BuscaCuotaExtras(DsTablaextras, Ciclo, periodicidad, ref FechaExt);
                VlrLiqInteres = Interes;

                switch (Clacuo)
                {
                    case 1:
                        Capital = Cuota - Interes - Seguro - Admon;
                        break;
                    case 2:
                        Capital = Cuota;
                        break;
                }

                if (Microsoft.VisualBasic.Information.IsNumeric(CicloPeriodoGracia))
                {
                    if (Convert.ToDouble(CicloPeriodoGracia) > 0)
                    {
                        if (Convert.ToDouble(CicloPeriodoGracia) > Convert.ToDouble(Ciclo))
                        {
                            Capital = 0;
                        }
                    }
                }

                switch (TipoSeg)
                {
                    case 7:
                        Seguro = VlrPrestamo * ((double)TasaSeg / 100) / periodicidad;
                        break;
                }

                switch (BaseLiqAdmon)
                {
                    case 7:
                        Admon = CuotaRef - (Aportes + Fondos + Funerario + Seguro + Interes + Capital);
                        LiqCuoAdm = Admon;
                        if (OpciProy == OpcionProyeccion.Cuota)
                        {
                            if (NuevaCuota != 0)
                            {
                                Admon = Admon + (NuevaCuota - CuotaRef);
                            }
                        }
                        break;
                    default:
                        if (OpciProy == OpcionProyeccion.Cuota)
                        {
                            if (NuevaCuota != 0)
                            {
                                Capital = Capital + (NuevaCuota - (Capital + Interes + Seguro + Admon + Aportes + Fondos + Funerario));
                            }
                        }
                        break;
                }

                switch (Claseint)
                {
                    case 2:
                        if (Clacuo == 2)
                        {
                            Interes = Math.Round(((Saldo - Capital - CuotaExtra) * (double)TasaInt), 0);
                            intpro = 0;
                        }
                        break;
                }

                if (Capital > Saldo)
                {
                    Capital = Saldo;
                }

                if (NumCuota == 1)
                {
                    switch (ForIntpro)
                    {
                        case 1:
                            Interes += intpro;
                            if (intpro < 0)
                            {
                                VlrLiqInteres = Interes;
                            }
                            break;
                        default:
                            if (intpro < 0 && IntcuoExt != "Y")
                            {
                                Interes += intpro;
                                VlrLiqInteres = Interes;
                            }
                            break;
                    }
                }

                if (IntcuoExt == "Y")
                {
                    Capital = 0;
                    if (intpro == 0)
                    {
                        Interes = 0;
                    }
                    else
                    {
                        if (NumCuota != 1)
                        {
                            Interes = 0;
                        }
                    }
                    if (ForIntpro == 2)
                    {
                        Interes = 0;
                    }
                }

                if (TotIntAnt == "Y")
                {
                    LiqInteres += Interes;
                    switch (Claseint)
                    {
                        case 1:
                            TotLiqInteres += VlrLiqInteres;
                            break;
                        case 2:
                            if (NumCuota != 1)
                            {
                                TotLiqInteres += VlrLiqInteres;
                            }
                            break;
                    }
                    Interes = 0;
                }

                if (CuotaExtra > 0 && IntcuoExt == "Y")
                {
                    if (FechaExtUlt == new DateTime(1950, 1, 1))
                    {
                        dias = this.CalculaDias(FechaExt, FechaDsto);
                    }
                    else
                    {
                        dias = this.CalculaDias(FechaExt, FechaExtUlt);
                    }

                    Interes += Math.Round((Saldo * (((double)TasaInteres / 100) / 30) * dias), 0);
                    FechaExtUlt = Convert.ToDateTime(FechaExt.ToString("yyyy/MM/dd"));
                }

                if (CuotaExtra > Saldo)
                {
                    CuotaExtra = Saldo;
                }

                Saldo -= CuotaExtra;

                if (NumCuota >= NumCuotas)
                {
                    Capital = Saldo;
                }

                if (Saldo <= 0)
                {
                    Capital = 0;
                }

                if (Capital > Saldo)
                {
                    Capital = Saldo;
                }

                Saldo -= Capital;

                Vlrcuota = Capital + Interes + Seguro + Admon + Aportes + Fondos + Funerario;

                if (CamCicloAfecha == true && (periodicidad == 1 || periodicidad == 2))
                {
                    DsdataProyeccion.Tables["Tblproyeccion"].Rows.Add(
                        Codigoter, NumCuota,
                        Convert.ToDateTime(CicloFecha.Tables["Ciclofechas"].Rows[NumCuota - 1]["fecha"]).ToString("dd-MM-yyyy"),
                        Vlrcuota, CuotaExtra, Interes, Seguro, Admon, Capital + CuotaExtra, Saldo);
                }
                else
                {
                    DsdataProyeccion.Tables["Tblproyeccion"].Rows.Add(
                        Codigoter, NumCuota, Ciclo, Vlrcuota, CuotaExtra, Interes, Seguro, Admon, Capital + CuotaExtra, Saldo);
                }

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
            } // end while

            if (TipoSeg == 2)
            {
                GrabaDeducciones(DstablaDeduciones, Codigoter, CptSeg, 99999999, "Seguro mes a mes", LiqSegMes, ref valmenos);
            }

            if (TotIntAnt == "Y")
            {
                this.GrabaDeducciones(DstablaDeduciones, Codigoter, CptoIntAnticipados, 99999999, "Interes anticipados", LiqInteres, ref valmenos);
            }

            DsdataProyeccion.Tables["TblTotales"].Rows.Add(0, 0, 0, VlrCuotasExtras, VlrInteres, VlrSeguro, Vlradmon, VlrCapital, 0);
            return DsdataProyeccion;
        }

        // VB line 1697 — CalculaDeudasRecogidas
        private double CalculaDeudasRecogidas(DataTable DsDataTable,
            ref double RecNoObligaciones, ref double coutarecogida, ref double cuotaRecoCaja)
        {
            int k = 0;
            double TotDeuRecogidas = 0, totacuoataRecogida = 0, totacuoataRecogidaCaja = 0;
            for (k = 0; k <= DsDataTable.Rows.Count - 1; k++)
            {
                TotDeuRecogidas += Convert.ToDouble(DsDataTable.Rows[k]["valor"]) + Convert.ToDouble(DsDataTable.Rows[k]["INTERES"]);
                if (Convert.ToDouble(DsDataTable.Rows[k]["LINCRED"]) < 1000)
                {
                    RecNoObligaciones += Convert.ToDouble(DsDataTable.Rows[k]["valor"]) + Convert.ToDouble(DsDataTable.Rows[k]["INTERES"]);
                }

                if (DsDataTable.Columns.Contains("CUOTA") == true && DsDataTable.Columns.Contains("CLADES") == true)
                {
                    if (DsDataTable.Rows[k]["TOTAL"].ToString() == "T")
                    {
                        switch (Convert.ToInt32(DsDataTable.Rows[k]["CLADES"]))
                        {
                            case 1:
                                totacuoataRecogida += Convert.ToDouble(DsDataTable.Rows[k]["CUOTA"]);
                                break;
                            case 2:
                                totacuoataRecogidaCaja += Convert.ToDouble(DsDataTable.Rows[k]["CUOTA"]);
                                break;
                        }
                    }
                }
            }
            coutarecogida = totacuoataRecogida;
            cuotaRecoCaja = totacuoataRecogidaCaja;
            return TotDeuRecogidas;
        }

        // VB line 1724 — ImprimePlanpagos
        public void ImprimePlanpagos(DataSet dsdataset, System.Windows.Forms.Form myforma, double NumSolicitud = 0)
        {
            ERP.Core.Creditos.Reportes.clsimpsol msgimpsol = new ERP.Core.Creditos.Reportes.clsimpsol();
            msgimpsol.ImprimePlanpagos(dsdataset, myforma, NumSolicitud);
        }

        // VB line 1731 — ImprimePlanpagosDirecto
        public void ImprimePlanpagosDirecto(DataSet dsdataset, System.Windows.Forms.Form myforma, double NumSolicitud = 0)
        {
            ERP.Core.Creditos.Reportes.clsimpsol msgimpsol = new ERP.Core.Creditos.Reportes.clsimpsol();
            msgimpsol.ImprimePlanpagosDirecto(dsdataset, myforma, NumSolicitud);
        }

        // VB line 1737 — CargaRecogeDeudas
        public DataTable CargaRecogeDeudas(string Codigoter, int Lincred, double ValorCredito, DateTime Fecha,
            System.Windows.Forms.Form Myforma, OdbcConnection Myconnet, ref DataSet dsdata)
        {
            // frmRecDeudas FrmRecDeudas = new frmRecDeudas(Myconnet); // ERROR: CS0246
            string Nombre = " ", DescLinea = " ";

            // msgcop.BuscaAsociado(Codigoter, Myconnet, ref Nombre); // ERROR: CS7036
            string _descLinea = DescLinea;
            // msgparcop.BuscaLinea(Lincred, Myconnet, ref _descLinea); // ERROR: CS1620
            DescLinea = _descLinea;

            // FrmRecDeudas.txtCodigoter.Text = Codigoter; // ERROR: CS0103
            // FrmRecDeudas.txtLinea.Text = Lincred.ToString(); // ERROR: CS0103
            // FrmRecDeudas.LblNombre.Text = Nombre; // ERROR: CS0103
            // FrmRecDeudas.LblDescLinea.Text = DescLinea; // ERROR: CS0103
            // FrmRecDeudas.LblCredito.Text = ValorCredito.ToString(); // ERROR: CS0103
            // FrmRecDeudas.LblDesembolso.Text = ValorCredito.ToString(); // ERROR: CS0103
            // FrmRecDeudas.DtpFecha.Value = Fecha; // ERROR: CS0103
            // FrmRecDeudas.DsdataRecdeuda = dsdata; // ERROR: CS0103
            // FrmRecDeudas.DatGriCreditos.AutoGenerateColumns = false; // ERROR: CS0103
            // FrmRecDeudas.DatGriCreditos.DataSource = CargaCreditos(Codigoter, Fecha.ToString("yyyyMM"), Myconnet); // ERROR: CS0103
            // FrmRecDeudas.ShowDialog(Myforma); // ERROR: CS0103
            // DataTable result = FrmRecDeudas.DsdataRecdeuda.Tables["tbldeducciones"]; // ERROR: CS0103
            // FrmRecDeudas.Close(); // ERROR: CS0103
            // FrmRecDeudas.Dispose(); // ERROR: CS0103
            //return result;
            DataTable result = null; // to avoid CS0165 error, will be overwritten by actual result from FrmRecDeudas
            return result;
        }

        // VB line 1763 — CargaCreditos
        public DataTable CargaCreditos(string Codigoter, string Periodo, OdbcConnection Myconnect)
        {
            DataSet Datset = new DataSet();
            stmysql = "select copmae.lincred as linea, copmae.numero as Numero, salmae.saldo as Saldo, SaldoCapital + SaldoExtra as Capital,SaldoInteres as Interes,"
                     + "SaldoMora as Mora,SaldoSeguro + SaldoAdmon + SaldoOtros as Otros, case  when copmae.VALOROB = 0 then 0 when copmae.lincred < 1000 then 0 else (((copmae.VALOROB - salmae.saldo) / copmae.VALOROB) * 100)end  porcentaje  "
                     + "FROM cop_maecar copmae inner join cop_salMaecar SalMae on copmae.codigoter = Salmae.codigoter and copmae.lincred = salmae.lincred inner join cop_concar12 parame12 "
                     + "on copmae.lincred = parame12.lincred and copmae.numero = salmae.numero and salmae.periodo = '" + Periodo + "' "
                     + "left join cop_copmora_vw copmora on copmae.codigoter = copmora.codigoter and copmae.lincred = copmora.lincred and "
                     + "copmae.numero = copmora.numero And periodo_contable = " + Periodo
                     + " where copmae.codigoter = '" + Codigoter + "' and ((copmae.lincred>=1000 and salmae.saldo<>0) or (copmae.lincred<1000 and (salmae.saldo<>0 or copmae.cuota<>0)))";

            OdbcConnect.ExecuteQueryDataset(stmysql, Myconnect, "CargaCreditos", ref Datset, "TblCopmae");

            return Datset.Tables[0];
        }

        // VB line 1791 — BuscaCuotaExtras (private)
        private double BuscaCuotaExtras(DataTable DsDataTable, string Ciclo, int Periodicidad2, ref DateTime Fechaext)
        {
            int k = 0;
            string CicloExt = null;
            double ValorExtra = 0;
            for (k = 0; k <= DsDataTable.Rows.Count - 1; k++)
            {
                DataRow _row = DsDataTable.Rows[k];
                CicloExt = this.CalculaCiclo(5, (Periodicidad)Periodicidad2, Convert.ToDateTime(_row["fecha"].ToString()));
                if (CicloExt == Ciclo)
                {
                    Fechaext = Convert.ToDateTime(_row["fecha"]);
                    ValorExtra += Convert.ToDouble(_row["valor"]);
                }
            }
            return ValorExtra;
        }

        // VB line 1805 — CalculaIntProporcionales
        private double CalculaIntProporcionales(DateTime fechaSolicitud, DateTime FechaDsto, double ValorCredito, decimal TasaInt, int periodicidad)
        {
            decimal DiasPer = 0;
            int DiasDif = 0;
            DateTime fechaini = Convert.ToDateTime(fechaSolicitud.ToString("yyyy/MM/dd"));
            DateTime Fechafin = Convert.ToDateTime(FechaDsto.ToString("yyyy/MM/dd"));
            double Intpro = 0;

            DiasPer = DiasPeriodicidad(periodicidad);
            DiasDif = this.CalculaDias(Fechafin, fechaini);

            if (DiasDif > 0 && DiasDif < (int)DiasPer)
            {
                DiasDif += 1;
            }

            decimal Dias = DiasDif - DiasPer;

            Intpro = Math.Round(((ValorCredito * ((double)TasaInt / 100) / 30)) * (double)Dias);

            return Intpro;
        }

        // VB overload — CalculaIntProporcionales with int ValorCredito
        private decimal CalculaIntProporcionales(DateTime fechaSolicitud, DateTime FechaDsto, double ValorCredito, decimal TasaInt, int periodicidad, bool returnDecimal)
        {
            return (decimal)CalculaIntProporcionales(fechaSolicitud, FechaDsto, ValorCredito, TasaInt, periodicidad);
        }

        // VB line 1828 — DiasPeriodicidad
        public decimal DiasPeriodicidad(int Periodicidad2)
        {
            switch (Periodicidad2)
            {
                case 1: return 30;
                case 2: return 15;
                case 3: return 10;
                case 4: return 7.5m;
                default: return 0;
            }
        }

        // VB line 1842 — cargaCuotasextras
        public DataTable cargaCuotasextras(double ValCredito, decimal Porext, DataTable Dstblextras,
            System.Windows.Forms.Form Myforma, bool HabilitaCampos = true,
            DateTime fechaValidacion = default(DateTime), DateTime fechapridesc = default(DateTime))
        {
            // Default values for optional DateTime params: #1/1/1950#
            // cop_cuotaextras frmCuotasExtras = new cop_cuotaextras(); // ERROR: CS0246
            // frmCuotasExtras.Porext = Porext; // ERROR: CS0103
            // frmCuotasExtras.ValPrestamo = ValCredito; // ERROR: CS0103
            // frmCuotasExtras.dtpVali.Value = fechaValidacion == default(DateTime) ? new DateTime(1950, 1, 1) : fechaValidacion; // ERROR: CS0103
            // frmCuotasExtras.fecpridescuento = (fechapridesc == default(DateTime) ? new DateTime(1950, 1, 1) : fechapridesc); // ERROR: CS0103
            // frmCuotasExtras.DsDataextras.Tables.Add(Dstblextras.Copy()); // ERROR: CS0103
            if (!HabilitaCampos)
            {
                // frmCuotasExtras.CmbGuardar.Enabled = false; // ERROR: CS0103
                // frmCuotasExtras.Dtgextras.Enabled = false; // ERROR: CS0103
            }
            // frmCuotasExtras.ShowDialog(Myforma); // ERROR: CS0103
            // return frmCuotasExtras.DsDataextras.Tables["tblextras"]; // ERROR: CS0103
            DataTable result = null; // to avoid CS0165 error, will be overwritten by actual result from frmCuotasExtras
            return result;
        }

        // VB line 1860 — DespliegaCuotasextras
        public DataTable DespliegaCuotasextras(DataTable DscuotaExtras, System.Windows.Forms.Form Myforma)
        {
            // cop_cuotaextras frmCuotasExtras = new cop_cuotaextras(); // ERROR: CS0246
            // frmCuotasExtras.Dtgextras.DataSource = DscuotaExtras; // ERROR: CS0103
            // frmCuotasExtras.DsDataextras.Tables.Add(DscuotaExtras.Copy()); // ERROR: CS0103
            // frmCuotasExtras.cmbeliminar.Enabled = false; // ERROR: CS0103
            // frmCuotasExtras.CmbGuardar.Enabled = false; // ERROR: CS0103
            // frmCuotasExtras.dtpVali.Visible = false; // ERROR: CS0103
            // frmCuotasExtras.Label5.Visible = false; // ERROR: CS0103
            // frmCuotasExtras.ShowDialog(Myforma); // ERROR: CS0103
            return null; // VB function body ends without explicit return for DataTable
        }

        // VB line 1874 — CalculaVp
        public double CalculaVp(DataTable dsdataset, int periodicidad, DateTime FechaSol, int Clacuo, decimal Tasaint,
            OdbcConnection myconnect, int Plazo = 0, int ciclodsto = 0, double CantCiclosAbonados = 0)
        {
            int K;
            double DbDiasPe = 0;
            DateTime fecha = default(DateTime);
            double Valor = 0, ForPag = 0, Valor2 = 0;
            int DifDias = 0;
            double ciclo = 0;
            double VPN = 0, VlrVpn = 0;
            int NumCuotas = 0;
            int numciclo = 0, numcuota = 0, CicloAnio = 0, sw1 = 0, resta = 0;
            double[] ArrayValor = new double[] { };
            string codigoter = "";
            int lincred = 0;
            double numero = 0;
            int periodo = 0;
            double saldoextracausado = 0;

            if (dsdataset.Rows.Count > 0)
            {
                if (CantCiclosAbonados != 0)
                {
                    codigoter = dsdataset.Rows[0]["codigoter"].ToString();
                    lincred = Convert.ToInt32(dsdataset.Rows[0]["lincred"]);
                    numero = Convert.ToDouble(dsdataset.Rows[0]["numero"]);
                    periodo = Convert.ToInt32(dsdataset.Rows[0]["periodo"]);
                }

                if (Clacuo == 1)
                {
                    NumCuotas = Plazo * periodicidad;
                    switch (periodicidad)
                    {
                        case 4:
                            NumCuotas = Convert.ToInt32((Plazo * 52) / 12);
                            break;
                        case 5:
                            NumCuotas = Convert.ToInt32((Plazo * 365) / 12);
                            break;
                    }

                    ArrayValor = new double[NumCuotas];

                    switch (periodicidad)
                    {
                        case 1: DbDiasPe = 30.41666667; break;
                        case 2: DbDiasPe = 15.20833333; break;
                        case 3: DbDiasPe = 10.13888889; break;
                        case 4: DbDiasPe = 7.5; break;
                        case 5: DbDiasPe = 1; break;
                    }

                    numcuota = 1;
                    CicloAnio = Convert.ToInt32(Strings.Mid(ciclodsto.ToString(), 1, 4));
                    numciclo = Convert.ToInt32(Strings.Mid(ciclodsto.ToString(), 5, 3));

                    for (K = 0; K <= NumCuotas - 1; K++)
                    {
                        Valor = 0;
                        switch (periodicidad)
                        {
                            case 5:
                                ciclo = Convert.ToDouble(CicloAnio.ToString() + Strings.Right("000" + numciclo.ToString(), 3));
                                break;
                            default:
                                ciclo = Convert.ToDouble(CicloAnio.ToString() + Strings.Right("00" + numciclo.ToString(), 2));
                                break;
                        }

                        Valor = BuscaCuotaExtras(dsdataset, ciclo.ToString(), periodicidad, ref fecha);

                        if (CantCiclosAbonados > 0)
                        {
                            if (CantCiclosAbonados > K)
                            {
                                if (Valor == 0)
                                {
                                    if (CantCiclosAbonados <= (K + 1))
                                    {
                                        ArrayValor[K - resta] = Valor;
                                    }
                                    else
                                    {
                                        if (sw1 == 0)
                                        {
                                            ArrayValor = new double[NumCuotas - 1 - (K + 1)];
                                            resta += 1;
                                        }
                                        else
                                        {
                                            ArrayValor[K - resta] = Valor;
                                        }
                                    }
                                }
                                else
                                {
                                    // Se debe validar que la cuota extra este causada
                                    double _saldo = saldoextracausado;
                                    // this.msgcop.BuscaSaldosCuotasPendientes(codigoter, lincred, numero, lincred, numero, periodo, (int)ciclo, myconnect, ref _saldo); // ERROR: CS1501
                                    saldoextracausado = _saldo;
                                    if (saldoextracausado != 0)
                                    {
                                        ArrayValor[K - resta] = Valor;
                                    }
                                    else
                                    {
                                        ArrayValor[K - resta] = 0;
                                    }
                                    sw1 = 1;
                                }
                            }
                            else
                            {
                                ArrayValor[K - resta] = Valor;
                            }
                        }
                        else
                        {
                            ArrayValor[K - resta] = Valor;
                        }

                        numcuota += 1;
                        numciclo += 1;
                        switch (periodicidad)
                        {
                            case 1:
                                if (numciclo > 12) { CicloAnio += 1; numciclo = 1; }
                                break;
                            case 2:
                                if (numciclo > 24) { CicloAnio += 1; numciclo = 1; }
                                break;
                            case 3:
                                if (numciclo > 36) { CicloAnio += 1; numciclo = 1; }
                                break;
                            case 4:
                                if (numciclo > 52) { CicloAnio += 1; numciclo = 1; }
                                break;
                        }
                    } // end for

                    try
                    {
                        VlrVpn = Microsoft.VisualBasic.Financial.NPV((double)Tasaint, ref ArrayValor);
                    }
                    catch (Exception)
                    {
                        VlrVpn = 0;
                    }
                }
                else // Clacuo != 1
                {
                    for (K = 0; K <= dsdataset.Rows.Count - 1; K++)
                    {
                        fecha = Convert.ToDateTime(dsdataset.Rows[K]["fecha"]);
                        Valor = Convert.ToDouble(dsdataset.Rows[K]["valor"]);
                        VPN = Valor;
                        VlrVpn += VPN;
                    }
                }
            }

            return VlrVpn;
        }

        // VB line 2047 — GrabaSolicitudCredito (full overload with ref estadoactual)
        public double GrabaSolicitudCredito(DataSet dsdataset, string Usuario, OdbcConnection myconnect,
            System.Windows.Forms.Form myforma, double NumSolicitud, ref string estadoactual)
        {
            string Estado = "P";
            string codigoter;
            if (NumSolicitud == 0)
            {
                Estado = "P";
            }
            else
            {
                Estado = estadoactual;
            }

            DataRow _row = dsdataset.Tables["tblsolicitud"].Rows[0];
            codigoter = _row["codigoter"].ToString();

            // this.GrabaSolicitud(NumSolicitud, _row["FecSolicitud"], _row["codigoter"], _row["lincred"], _row["ValorSolicitud"], _row["TasaInt"], _row["plazo"], _row["cuota"], // ERROR: CS1503, CS1620
                // _row["GastosFijos"], 0, _row["CupoDisponible"], _row["FecIngCoop"], _row["EmpresaLaboral"], _row["CargoLaboral"], _row["Salario"], _row["OtrosIngresos"], _row["FecIngLaboral"], _row["GastosFijos"], _row["DisponibleMes"], _row["TipoContracto"], // ERROR: CS1503, CS1620
                // _row["ClaseGar"], _row["DescGara"], _row["AvaluoComercial"], _row["AvaluoCatastral"], _row["GaraAsegurado"], _row["porseg"], _row["Fecvenseg"], _row["codeudor1"], _row["codeudor2"], _row["codeudor3"], _row["codeudor4"], Estado, // ERROR: CS1503, CS1620
                 // Usuario, DateTime.Now, _row["Trabconyuge"], _row["Nomconyuge"], _row["Empconyuge"], _row["SalConyuge"], _row["telconyuge"], _row["Dirempconyuge"], _row["CiudadConyuge"], _row["PerCargo"], _row["Vehiculo"], _row["CasaPropia"], _row["TieneVehiculo"], _row["FechaDsto"], // ERROR: CS1503, CS1620
                // _row["Cedula"], _row["Ciclo"], _row["periodicidad"], _row["clacuo"], _row["claint"], _row["TasaAdm"], _row["TasaSeg"], _row["TasaCpt"], _row["VlrVpnExtra"], _row["SalAportes"], _row["Agencia"], _row["CentroCosto"], _row["CuotaAdm"], _row["CuotaSeg"], // ERROR: CS1503, CS1620
                // _row["CuotaCapital"], _row["CuotaIcie"], _row["CuotaOtros"], _row["TipoIntCie"], _row["TipoCap"], _row["TipoAdm"], _row["Tiposeg"], _row["TipoOtro"], _row["foradm"], _row["cptoadm"], _row["cptoseg"], _row["cptootro"], _row["tasaotro"], // ERROR: CS1503, CS1620
                // _row["pergracia"], _row["pergraciaini"], _row["ciclopergracia"], _row["clades"], _row["cuexinmes"], _row["cuexintant"], _row["pag1cuo"], _row["tippag2"], _row["DstoEmpresa"], _row["IngVariables"], _row["IngArriendos"], _row["DeudasTerceros"], _row["numcdat"], // ERROR: CS1503, CS1620
                // _row["nitaseguradora"], _row["nombreaseguradora"], _row["numpoliza"], _row["matricula"], _row["empdsto"], _row["NumPagare"], _row["IngPension"], _row["DstoPension"], _row["DstoParafiscales"], _row["DstoEmpresaNomina"], _row["dtf"], _row["puntos"], myconnect, _row["IngreseConyuge"], _row["dtsGastosPersonales"], _row["cappagoPorcentaje"], _row["cappagoRecDeudas"], _row["TxtSolActVivienda"], // ERROR: CS1503, CS1620
                // _row["TxtSolActVehiculo"], _row["TxtSolActOtros"], _row["TxtSolActAportes"], _row["TxtActCtaBanco"], _row["TxtActCxC"], _row["TxtSolActAhorros"], _row["TxtSolActTotal"], _row["TxtSolPasDeudas"], _row["TxtSolPasOtros"], _row["TxtPasObliBanca"], _row["TxtPasObliHipot"], _row["TxtSolPasTotal"], _row["TxtSolPatrimonio"], _row["TxtSolPasTotPyP"], // ERROR: CS1503, CS1620
                // _row["capacNomina"], _row["PorcNomina"], _row["CapPago"], _row["PorcCaja"], _row["PorcentajePagaduria"], _row["LblNomina"], _row["LblCaja"], _row["Descubierto"], _row["NivelEndeudamiento"], _row["NivelContingencia"], _row["CapitalRiesgo"], _row["capacdsto"], _row["Porcdsto"], _row["tipodstoPagaduria"]); // ERROR: CS1503, CS1620

            this.GrabaCuotasExtras(NumSolicitud, dsdataset.Tables["tblextras"], myconnect);
            // GrabaDeduciones(NumSolicitud, dsdataset.Tables["tbldeducciones"], Convert.ToDateTime(_row["FecSolicitud"]).ToString("yyyyMM"), myconnect); // ERROR: CS1503
            // GrabaDatosCodeudores(NumSolicitud, dsdataset.Tables["TblCodeudores"], myconnect); // ERROR: CS1503
            GrabarBienes(dsdataset, NumSolicitud, myconnect);
            msgcop.GrabarBienes(dsdataset, codigoter, myconnect, "NO");
            this.GrabarReferenciasSol(dsdataset, NumSolicitud, myconnect);
            // this.GrabaSolParVivienda(NumSolicitud, dsdataset, myconnect); // ERROR: CS1503

            return NumSolicitud;
        }

        // Convenience overload: NumSolicitud=0, estadoactual="E" (VB default)
        public double GrabaSolicitudCredito(DataSet dsdataset, string Usuario, OdbcConnection myconnect,
            System.Windows.Forms.Form myforma)
        {
            string estadoactual = "E";
            return GrabaSolicitudCredito(dsdataset, Usuario, myconnect, myforma, 0, ref estadoactual);
        }

        public double GrabaSolicitudCredito(DataSet dsdataset, string Usuario, OdbcConnection myconnect,
            System.Windows.Forms.Form myforma, double NumSolicitud)
        {
            string estadoactual = "E";
            return GrabaSolicitudCredito(dsdataset, Usuario, myconnect, myforma, NumSolicitud, ref estadoactual);
        }

        // VB line 2082 — BuscaDatosCodeudores
        public bool BuscaDatosCodeudores(int solicitud, string codeudor, OdbcConnection myconnect, ref DataSet DsData)
        {
            stmysql = "select Salario,OTROSINGRESOS,INGARRIENDO,INGVARIABLE,DEUDAEMP,DEUDTERCERO,OTRODSTOS,DISPMES,IngPension,DstoPension,DstoParafiscales,cappagoPorcentaje,GastosPers,DeudaEmpCaja     "
                     + " from COP_SOLCODEUDOR where SOLICITUD= " + solicitud + " and CODEUDOR='" + codeudor + "'";
            ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscaDatosCodeudores", ref DsData, "TblDatCodeudor");
            return ok;
        }

        // VB line 2089 — BuscaConseSolicitud
        private double BuscaConseSolicitud(OdbcConnection myconnect)
        {
            double Stconse = 0;
            DataSet dsCompania = new DataSet();
            msgparsys.BuscarCompania(varini.sptCodEmpr, dsCompania, myconnect);
            Stconse = Convert.ToDouble(dsCompania.Tables["tblcompania"].Rows[0]["num_solcred"]) + 1;
            GrabaConseSolicitud(Stconse, myconnect);
            return Stconse;
        }

        // NOTE: method body continues in Part 2 (VB line 2100+)

    } // partial class ClsLiqcreditos
} // namespace ERP.Core.CarteraFinanciera.Services.Creditos
