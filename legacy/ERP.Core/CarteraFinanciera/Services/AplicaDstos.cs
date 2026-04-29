using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using ERP.Core.Compartido.Datos;

namespace ERP.Core.CarteraFinanciera.Services
{
    public class AplicaDstos
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        public ClsConect.odbcConect varini = new ClsConect.odbcConect();
        private ClsConect clsConect = new ClsConect();

        public string strConect;
        public string CodCompania;
        private string stmysql;
        private bool ok;
        private string DetalleDes, CicloDes;

        public AplicaDstos()
        {
            clsConect.MyOdbcConect(varini);
        }

        public void AplicaDestos(string comprobante, double ConseCpte, DateTime FechaMovto, int OpAplicacion, string Usuario, string Planilla, int periodicidad, OdbcConnection myconnect, Form Myform, string Adicional = "0", string FormaDstoAdicional = "0",
             string empresa = "9999", string Agencia = "9999", string Cencosto = "99999999", string ciclo = "1", string Detalle = "1")
        {
            double VlrDsto = 0;
            int CptoFavor = 0, CptoMovCap = 0;
            //ParamSys msgsasconf = new ParamSys();
            string tipoMov = "99", CptoExt = "99", InCuoExt = "N";
            //ParamCop msgcoppar = new ParamCop();
            string CptoInt = "99", MovtosExtrasNom = "Y";
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
            //Barraprogress Proges = new Barraprogress("Aplicando los descuentos, Por favor espere...", Myform);
            int Tipolinea = 0, canreg = 0, fila = 0;
            string CobraCodeudor = "N";
            string TipoNomina = "0";
            string WhereAgencia, WhereAgencia2;
            string WhereEmpresa, WhereEmpresa2;
            string WhereCencosto, WhereCencosto2;
            int CicloAplicar = 999999;
            int cicloDesc = 0;
            int AtraDesde = 0;
            int AtraHasta = 0;
            int contador = 0;
            int Nextras = 0;
            int Lincredito = 0;
            double NumeCredito = 0;

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
                WhereAgencia2 = "";
            }
            else
            {
                WhereAgencia = "  and Agencia = '" + ("0000" + Agencia).Substring(("0000" + Agencia).Length - 4) + "' ";
                WhereAgencia2 = "  and a.Agencia = '" + ("0000" + Agencia).Substring(("0000" + Agencia).Length - 4) + "' ";
            }
            if (empresa == "Todos")
            {
                WhereEmpresa = "";
                WhereEmpresa2 = "";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + "' ";
                WhereEmpresa2 = " and a.Empresa = '" + ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + "' ";
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
                WhereCencosto2 = "";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + ("00000000" + Cencosto).Substring(("00000000" + Cencosto).Length - 8) + "' ";
                WhereCencosto2 = " and a.cencosto = '" + ("00000000" + Cencosto).Substring(("00000000" + Cencosto).Length - 8) + "' ";
            }

            DetalleDes = Detalle;
            CicloDes = ciclo;

            stmysql = "update cop_nomdes set vlr_prestamos_apli = 0,vlr_aportes_apli = 0,vlr_interes_apli = 0, vlr_mora_apli= 0,vlr_seguro_apli = 0,vlr_admon_apli = 0,vlr_extras_apli = 0 " +
                     " where  periodo = '" + Planilla + "' and periodicidad ='" + periodicidad + "' and  adicional = '" + Adicional + "' " +
                      WhereAgencia + WhereEmpresa + WhereCencosto;

            clsConect.ExecuteQueryconec(stmysql, myconnect, "GrabaPlanilla");

            try
            {
                switch (FormaDstoAdicional)
                {
                    case "2":
                        stmysql = "select v.codigoter, sum(v.valor) as valor, a.lincred,a.numero,(select count(codigoter) from cop_nomdes where codigoter=a.codigoter and empresa = a.empresa and cencosto = a.cencosto and agencia  = a.agencia " +
                                   " and periodo = a.periodo and periodicidad =  a.periodicidad and adicional = a.adicional )as nextras,pl.atrasextras from cop_valdesc v " +
                                   " inner join cop_nomdes a on a.empresa = case  when v.empresa  = ' ' then a.empresa  else v.empresa end and " +
                                   " a.cencosto = case  when v.cencosto  = ' ' then a.cencosto  else v.cencosto end and " +
                                   " a.agencia = case  when v.agencia  = ' ' then a.agencia  else v.agencia end and " +
                                   " a.periodo = v.periodo and a.periodicidad =  v.periodicidad  and " +
                                   " a.adicional = v.adicional And a.codigoter = v.codigoter And a.lincred = a.lincred " +
                                   " inner join cop_nomconce c on v.comcep=c.cpto_extras and a.lincred=c.lincred and v.empresa=c.empresa " +
                                   " inner join cop_nompla pl on v.empresa=pl.empresa and v.agencia=pl.agencia and v.cencosto=pl.cencosto and v.periodo=pl.periodo " +
                                   " and v.periodicidad=pl.periodicidad and v.adicional=pl.adicional " +
                                   "where  a.periodo = " + Planilla + " and a.periodicidad = '" + periodicidad +
                                                                    "' and v.valor<>0 and a.adicional ='" + Adicional + "' " +
                                                                    WhereAgencia2 + WhereEmpresa2 + WhereCencosto2 +
                                                                    "group by v.codigoter,v.comcep,v.cencosto,v.empresa,v.agencia,v.periodo,v.periodicidad,v.adicional," +
                                                                    "v.Valor, a.lincred, a.numero,a.codigoter,a.empresa,a.cencosto,a.agencia,a.periodo ,a.periodicidad ,a.adicional,pl.atrasextras order by v.codigoter";
                        break;
                    default:
                        stmysql = "select codigoter, sum(valor) as valor from cop_valdesc " +
                                                         "where  periodo = " + Planilla + " and periodicidad = " + periodicidad +
                                                         " and adicional ='" + Adicional + "' " +
                                                         WhereAgencia + WhereEmpresa + WhereCencosto +
                                                         " group by codigoter";
                        break;
                }

                //Proges.DefineMaximo(stmysql, myconnect);
                //Proges.Show();

                DataSet myread = new DataSet();
                this.clsConect.ExecuteQueryDataset(stmysql, myconnect, "AplicaDestos", myread, "TblApliDstos");
                canreg = myread.Tables["TblApliDstos"].Rows.Count;

                //msgsasconf.BuscarCompania(varini.sptCodEmpr, myconnect, ref CptoMovCap, ref CptoFavor, ref CptoExt, ref CptoInt, ref CobraCodeudor, ref TipoNomina);

                //msgcoppar.BuscarPlanillaNomina(Agencia, empresa, Cencosto, Planilla, periodicidad, Adicional, myconnect, ref cicloDesc, ref AtraDesde, ref AtraHasta);

                //ok = msgcop.BuscaLinea(CptoFavor, myconnect, ref Tipolinea, ref InCuoExt);
                switch (Tipolinea)
                {
                    case 3:
                        //msgcop.BsucarCompania(varini.sptCodEmpr, myconnect, ref CptoMovCap);
                        break;
                    case 4:
                        //msgcop.BsucarCompania(varini.sptCodEmpr, myconnect, ref CptoMovCap, ref CptoFavor);
                        break;
                    default:
                        MessageBox.Show("Revise el tipo de linea del concepto a favor incorrecto", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                }

                CicloAplicar = int.Parse(Planilla);

                while (fila < canreg)
                {
                    Application.DoEvents();
                    DataRow row = myread.Tables["TblApliDstos"].Rows[fila];
                    VlrDsto = Convert.ToDouble(row["valor"]);

                    switch (FormaDstoAdicional)
                    {
                        case "2":
                            string atrasextras = row["atrasextras"].ToString().Trim();
                            switch (atrasextras)
                            {
                                case "2":
                                    tipoMov = "99";
                                    Nextras = Convert.ToInt32(row["nextras"]);
                                    for (contador = 0; contador <= Nextras - 1; contador++)
                                    {
                                        if (VlrDsto > 0)
                                        {
                                            msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), 9999, 99999999, Convert.ToInt32(FechaMovto.ToString("yyyyMM")), tipoMov, FechaMovto, 0, ref VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, Clades: global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, NumExtra: CicloAplicar, EmpDsto: empresa, TipoNomina: TipoNomina);
                                            VlrDsto = Math.Round(VlrDsto, 0);
                                        }
                                        if (Nextras > 1)
                                        {
                                            fila += 1;
                                        }
                                    }
                                    if (Nextras > 1)
                                    {
                                        fila -= 1;
                                    }

                                    if (VlrDsto > 0)
                                    {
                                        if (CobraCodeudor == "Y")
                                        {
                                            //ok = msgcop.NominaPendienteCodeuda(Agencia, empresa, Cencosto, Planilla, periodicidad, Adicional, myconnect, row["codigoter"].ToString());
                                            if (ok)
                                            {
                                                AplicaDestosCodeuda(comprobante, ConseCpte, empresa, Agencia, Cencosto, FechaMovto, Usuario, Planilla,
                                                periodicidad, myconnect, row["codigoter"].ToString(), Adicional, FormaDstoAdicional, ref VlrDsto);
                                            }
                                        }
                                        if (TipoNomina == "3")
                                        {
                                            if (VlrDsto > 0)
                                            {
                                                int CicoIni = AtraDesde;
                                                if (AtraHasta.ToString().Length == 6)
                                                {
                                                    while (CicoIni <= AtraHasta)
                                                    {
                                                        if (VlrDsto > 0)
                                                        {
                                                            //msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), 9999, 99999999, FechaMovto.ToString("yyyyMM"), tipoMov, FechaMovto, 0, VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, CicoIni, empresa, TipoNomina);
                                                            VlrDsto = Math.Round(VlrDsto, 0);
                                                        }
                                                        //msgcop.CalcuFecProximoCiclo(ref CicoIni, periodicidad, cicloDesc, true);
                                                    }
                                                }
                                            }
                                        }
                                        if (VlrDsto > 0)
                                        {
                                           // msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), CptoFavor, 0, FechaMovto.ToString("yyyyMM"), CptoMovCap.ToString(), FechaMovto, 0, VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, CicloAplicar, "", TipoNomina);
                                        }
                                    }
                                    break;
                                default:
                                    tipoMov = CptoExt;
                                    if (row["lincred"] == DBNull.Value)
                                    {
                                        Lincredito = 9999;
                                    }
                                    else
                                    {
                                        Lincredito = Convert.ToInt32(row["lincred"]);
                                    }

                                    if (row["numero"] == DBNull.Value)
                                    {
                                        NumeCredito = 99999999;
                                    }
                                    else
                                    {
                                        NumeCredito = Convert.ToDouble(row["numero"]);
                                    }

                                    Nextras = Convert.ToInt32(row["nextras"]);
                                    for (contador = 0; contador <= Nextras - 1; contador++)
                                    {
                                        if (VlrDsto > 0)
                                        {
                                            //msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), FormaDstoAdicional == "2" ? Lincredito : 9999, FormaDstoAdicional == "2" ? NumeCredito : 99999999, FechaMovto.ToString("yyyyMM"), tipoMov, FechaMovto, 0, VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, CicloAplicar, empresa, TipoNomina);
                                            VlrDsto = Math.Round(VlrDsto, 0);
                                        }
                                        if (Nextras > 1)
                                        {
                                            fila += 1;

                                            if (myread.Tables["TblApliDstos"].Rows.Count < fila)
                                            {
                                                Lincredito = 9999;
                                                NumeCredito = 99999999;
                                            }
                                            else
                                            {
                                                if (myread.Tables["TblApliDstos"].Rows[fila]["lincred"] == DBNull.Value)
                                                {
                                                    Lincredito = 9999;
                                                }
                                                else
                                                {
                                                    Lincredito = Convert.ToInt32(myread.Tables["TblApliDstos"].Rows[fila]["lincred"]);
                                                }

                                                if (myread.Tables["TblApliDstos"].Rows[fila]["numero"] == DBNull.Value)
                                                {
                                                    NumeCredito = 99999999;
                                                }
                                                else
                                                {
                                                    NumeCredito = Convert.ToDouble(myread.Tables["TblApliDstos"].Rows[fila]["numero"]);
                                                }
                                            }
                                        }
                                    }
                                    if (Nextras > 1)
                                    {
                                        fila -= 1;
                                    }
                                    if (VlrDsto > 0)
                                    {
                                        //msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), FormaDstoAdicional == "2" ? Lincredito : 9999, FormaDstoAdicional == "2" ? NumeCredito : 99999999, FechaMovto.ToString("yyyyMM"), tipoMov, FechaMovto, 0, VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, CicloAplicar, empresa, TipoNomina);
                                        VlrDsto = Math.Round(VlrDsto, 0);
                                    }

                                    if (VlrDsto > 0)
                                    {
                                        if (CobraCodeudor == "Y")
                                        {
                                            //ok = msgcop.NominaPendienteCodeuda(Agencia, empresa, Cencosto, Planilla, periodicidad, Adicional, myconnect, row["codigoter"].ToString());
                                            if (ok)
                                            {
                                                AplicaDestosCodeuda(comprobante, ConseCpte, empresa, Agencia, Cencosto, FechaMovto, Usuario, Planilla,
                                                periodicidad, myconnect, row["codigoter"].ToString(), Adicional, FormaDstoAdicional, ref VlrDsto);
                                            }
                                        }
                                        if (TipoNomina == "3")
                                        {
                                            if (VlrDsto > 0)
                                            {
                                                int CicoIni = AtraDesde;
                                                if (AtraHasta.ToString().Length == 6)
                                                {
                                                    while (CicoIni <= AtraHasta)
                                                    {
                                                        if (VlrDsto > 0)
                                                        {
                                                            //msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), 9999, 99999999, FechaMovto.ToString("yyyyMM"), tipoMov, FechaMovto, 0, VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, CicoIni, empresa, TipoNomina);
                                                            VlrDsto = Math.Round(VlrDsto, 0);
                                                        }
                                                        //msgcop.CalcuFecProximoCiclo(ref CicoIni, periodicidad, cicloDesc, true);
                                                    }
                                                }
                                            }
                                        }
                                        if (VlrDsto > 0)
                                        {
                                            //msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), CptoFavor, 0, FechaMovto.ToString("yyyyMM"), CptoMovCap.ToString(), FechaMovto, 0, VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, CicloAplicar, "", TipoNomina);
                                        }
                                    }
                                    break;
                            }
                            break;
                        default:
                            tipoMov = "99";

                            switch (FormaDstoAdicional)
                            {
                                case "1":
                                case "6":
                                    MovtosExtrasNom = "N";
                                    break;
                            }

                            if (VlrDsto > 0)
                            {
                                //msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), FormaDstoAdicional == "2" ? Lincredito : 9999, FormaDstoAdicional == "2" ? NumeCredito : 99999999, FechaMovto.ToString("yyyyMM"), tipoMov, FechaMovto, 0, VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, CicloAplicar, empresa, TipoNomina, MovtosExtrasNom);
                                VlrDsto = Math.Round(VlrDsto, 0);
                            }

                            if (VlrDsto > 0)
                            {
                                if (CobraCodeudor == "Y")
                                {
                                    //ok = msgcop.NominaPendienteCodeuda(Agencia, empresa, Cencosto, Planilla, periodicidad, Adicional, myconnect, row["codigoter"].ToString());
                                    if (ok)
                                    {
                                        AplicaDestosCodeuda(comprobante, ConseCpte, empresa, Agencia, Cencosto, FechaMovto, Usuario, Planilla,
                                        periodicidad, myconnect, row["codigoter"].ToString(), Adicional, FormaDstoAdicional, ref VlrDsto);
                                    }
                                }
                                if (TipoNomina == "3")
                                {
                                    if (VlrDsto > 0)
                                    {
                                        int CicoIni = AtraDesde;
                                        if (AtraHasta.ToString().Length == 6)
                                        {
                                            while (CicoIni <= AtraHasta)
                                            {
                                                if (VlrDsto > 0)
                                                {
                                                    msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), 9999, 99999999, Convert.ToInt32(FechaMovto.ToString("yyyyMM")), tipoMov, FechaMovto, 0, ref VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, Clades: global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, NumExtra: CicoIni, EmpDsto: empresa, TipoNomina: TipoNomina);
                                                    VlrDsto = Math.Round(VlrDsto, 0);
                                                }
                                                //msgcop.CalcuFecProximoCiclo(ref CicoIni, periodicidad, cicloDesc, true);
                                            }
                                        }
                                    }
                                }
                                if (VlrDsto > 0)
                                {
                                    msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codigoter"].ToString(), CptoFavor, 0, Convert.ToInt32(FechaMovto.ToString("yyyyMM")), CptoMovCap.ToString(), FechaMovto, 0, ref VlrDsto, "Aplicacion de descuentos de nomina", Usuario, myconnect, Clades: global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, NumExtra: CicloAplicar, TipoNomina: TipoNomina);
                                }
                            }
                            break;
                    }

                    //Proges.PerformStep();
                    fila += 1;
                }

                myread.Dispose();
                //Proges.Close();
                //Proges.Dispose();

                GrabaPlanilla(empresa, Agencia, periodicidad, Cencosto, int.Parse(Planilla), Adicional, comprobante, ConseCpte, Usuario, myconnect, TipoNomina);

                MessageBox.Show("El proceso termino.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //Proges.Dispose();
                //Proges.Close();
            }
        }

        public void AplicaDestosCodeuda(string comprobante, double ConseCpte,
              string empresa, string Agencia,
              string Cencosto, DateTime FechaMovto, string Usuario,
              string Planilla, int periodicidad, OdbcConnection myconnect,
              string Codigoter, string Adicional,
              string FormaDstoAdicional, ref double VlrDsto)
        {
            int CptoFavor = 0, CptoMovCap = 0;
            //ParamSys msgsasconf = new ParamSys();
            string tipoMov = "99", CptoExt = "99", InCuoExt = "N";
            //ParamCop msgcoppar = new ParamCop();
            string CptoInt = "99";
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
            int Tipolinea = 0, reg = 0, cont = 0;
            string TipoNomina = "0";

            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
            }
            else
            {
                WhereAgencia = "  and Agencia = '" + ("0000" + Agencia).Substring(("0000" + Agencia).Length - 4) + "' ";
            }
            if (empresa == "Todos")
            {
                WhereEmpresa = "";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + "' ";
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + ("00000000" + Cencosto).Substring(("00000000" + Cencosto).Length - 8) + "' ";
            }

            stmysql = "update cop_nomdes set vlr_prestamos_apli = 0,vlr_aportes_apli = 0,vlr_interes_apli = 0, vlr_mora_apli= 0,vlr_seguro_apli = 0,vlr_admon_apli = 0,vlr_extras_apli = 0 " +
                     " where  periodo = '" + Planilla + "' and periodicidad ='" + periodicidad + "' and  adicional = '" + Adicional + "' and codigoter = '" +
                      Codigoter + "' and codeuda <> '99999999999999' " +
                      WhereAgencia + WhereEmpresa + WhereCencosto;

            clsConect.ExecuteQueryconec(stmysql, myconnect, "GrabaPlanilla");

            try
            {
                stmysql = " select * from cop_nomdes " +
                " where  periodo = '" + Planilla + "' and periodicidad ='" + periodicidad + "' and  adicional = '" +
                Adicional + "' and codigoter  = '" + Codigoter + "' and codeuda <> '99999999999999' " +
                WhereAgencia + WhereEmpresa + WhereCencosto;

                DataSet myreader = new DataSet();
                this.clsConect.ExecuteQueryDataset(stmysql, myconnect, "AplicaDestosCodeuda", myreader, "Tblcodeuda");
                reg = myreader.Tables["Tblcodeuda"].Rows.Count;

                //msgsasconf.BuscarCompania(varini.sptCodEmpr, myconnect, ref CptoMovCap, ref CptoFavor, ref CptoExt, ref CptoInt, ref CobraCodeudor, ref TipoNomina);

                //ok = msgcop.BuscaLinea(CptoFavor, myconnect, ref Tipolinea, ref InCuoExt);
                switch (Tipolinea)
                {
                    case 3:
                        //msgcop.BsucarCompania(varini.sptCodEmpr, myconnect, ref CptoMovCap);
                        break;
                    case 4:
                        //msgcop.BsucarCompania(varini.sptCodEmpr, myconnect, ref CptoMovCap, ref CptoFavor);
                        break;
                    default:
                        MessageBox.Show("Revise el tipo de linea del concepto a favor incorrecto", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                }

                while (cont < reg)
                {
                    Application.DoEvents();
                    DataRow row = myreader.Tables["Tblcodeuda"].Rows[cont];

                    switch (FormaDstoAdicional)
                    {
                        case "2":
                            tipoMov = CptoExt;
                            break;
                        default:
                            tipoMov = "99";
                            break;
                    }

                    if (VlrDsto > 0)
                    {
                        //msgcop.GrabaMovimiento(comprobante, ConseCpte, row["codeuda"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]),
                        //FechaMovto.ToString("yyyyMM"), tipoMov, FechaMovto, 0, VlrDsto, "Aplicacion de Descto nomina codeuda",
                        //Usuario, myconnect, "", global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, 0, empresa, TipoNomina, "", row["codigoter"].ToString());

                        VlrDsto = Math.Round(VlrDsto, 0);
                    }

                    cont += 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public object GrabaPlanilla(string empresa, string Agencia, int Periodicidad, string Cencosto, int Planilla, string Adicional,
        string Comprobante, double ConseCompro, string Usuario, OdbcConnection Myconnect,
        string TipoNomina = "")
        {
            string CicloAplicar = "";
            int totreg = 0, tfila = 0;

            if (TipoNomina == "3")
            {
                CicloAplicar = " and CICLOS ='" + Planilla + "' ";
            }
            else
            {
                CicloAplicar = "";
            }

            stmysql = "select codigoter,lincred,numero,tipo_movto,sum(vlr_debito) as debito,sum(vlr_credito) as credito from cop_movimto copmov inner join cop_codmov codmov on " +
                    "copmov.cod_movto = codmov.cod_movto " +
                    "where compronte = '" + Comprobante + "' and numero_domto = " + ConseCompro +
                    CicloAplicar + " group by codigoter, lincred,numero,tipo_movto " +
                    "order by codigoter, lincred,numero,tipo_movto ";

            DataSet Myread = new DataSet();
            this.clsConect.ExecuteQueryDataset(stmysql, Myconnect, "GrabaPlanilla", Myread, "TblGrabPlanilla");
            totreg = Myread.Tables["TblGrabPlanilla"].Rows.Count;

            while (tfila < totreg)
            {
                DataRow row = Myread.Tables["TblGrabPlanilla"].Rows[tfila];
                Application.DoEvents();
                ActualizaPlanilla(empresa, Agencia, Periodicidad, Cencosto, Planilla, Adicional, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToDouble(row["credito"]), Convert.ToInt32(row["tipo_movto"]), Myconnect);
                ActualizaCopmae(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToDouble(row["credito"]), Convert.ToInt32(row["tipo_movto"]), Planilla.ToString(), Myconnect);
                tfila += 1;
            }

            Myread.Dispose();
            return null;
        }

        private void ActualizaCopmae(string Codigoter, int lincred, double Numero, double Valor, int TipoMovto, string Planilla, OdbcConnection myconnect)
        {
            switch (TipoMovto)
            {
                case 1:
                case 4:
                case 7:
                case 10:
                    stmysql = "update cop_maecar set CapAplNom = '" + Valor + "'";
                    break;
                case 2:
                    stmysql = "update cop_maecar set IntAplNom = '" + Valor + "'";
                    break;
                case 3:
                    stmysql = "update cop_maecar set MorAplNom = '" + Valor + "'";
                    break;
                case 5:
                    stmysql = "update cop_maecar set SegAplNom = '" + Valor + "'";
                    break;
                case 6:
                    stmysql = "update cop_maecar set AdmAplNom = '" + Valor + "'";
                    break;
            }

            stmysql = stmysql + ", planilla = " + Planilla + " where codigoter = '" + Codigoter + "' and lincred = " + lincred + " and numero =" + Numero;
            clsConect.ExecuteQueryconec(stmysql, myconnect, "ActualizaCopmae");
        }

        private void ActualizaPlanilla(string empresa, string Agencia, int Periodicidad, string Cencosto, int Planilla, string Adicional, string Codigoter,
        int lincred, double Numero, double Credito, int TipoMovto, OdbcConnection Myconnect)
        {
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string CampoAplicar = "", EmpApli, AgeApli, CenApli;

            if (Agencia == "Todos")
            {
                WhereAgencia = " and Agencia = '9999' ";
                AgeApli = "9999";
            }
            else
            {
                WhereAgencia = "  and Agencia = '" + ("0000" + Agencia).Substring(("0000" + Agencia).Length - 4) + "' ";
                AgeApli = ("0000" + Agencia).Substring(("0000" + Agencia).Length - 4);
            }
            if (empresa == "Todos")
            {
                WhereEmpresa = " and Empresa = '9999' ";
                EmpApli = "9999";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + "' ";
                EmpApli = ("0000" + empresa).Substring(("0000" + empresa).Length - 4);
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = " and cencosto = '99999999' ";
                CenApli = "99999999";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + ("00000000" + Cencosto).Substring(("00000000" + Cencosto).Length - 8) + "' ";
                CenApli = ("00000000" + Cencosto).Substring(("00000000" + Cencosto).Length - 8);
            }

            switch (TipoMovto)
            {
                case 1:
                    if (lincred >= 1000)
                    {
                        stmysql = "update cop_nomdes set vlr_prestamos_apli = '" + Credito + "'";
                        CampoAplicar = "vlr_prestamos_apli";
                    }
                    else
                    {
                        stmysql = "update cop_nomdes set vlr_aportes_apli = '" + Credito + "'";
                        CampoAplicar = "vlr_aportes_apli";
                    }
                    break;
                case 2:
                    stmysql = "update cop_nomdes set vlr_interes_apli = '" + Credito + "'";
                    CampoAplicar = "vlr_interes_apli";
                    break;
                case 3:
                    stmysql = "update cop_nomdes set vlr_mora_apli = '" + Credito + "'";
                    CampoAplicar = "vlr_mora_apli";
                    break;
                case 4:
                case 7:
                    stmysql = "update cop_nomdes set vlr_aportes_apli = '" + Credito + "'";
                    CampoAplicar = "vlr_aportes_apli";
                    break;
                case 5:
                    stmysql = "update cop_nomdes set vlr_seguro_apli = '" + Credito + "'";
                    CampoAplicar = "vlr_seguro_apli";
                    break;
                case 6:
                    stmysql = "update cop_nomdes set vlr_admon_apli = '" + Credito + "'";
                    CampoAplicar = "vlr_admon_apli";
                    break;
                case 10:
                    stmysql = "update cop_nomdes set vlr_extras_apli = '" + Credito + "'";
                    CampoAplicar = "vlr_extras_apli";
                    break;
            }

            stmysql = stmysql + " where codigoter = '" +
                     Codigoter + "' and periodo = '" + Planilla + "' and periodicidad ='" + Periodicidad + "' and  adicional = '" + Adicional +
                     "' and lincred = '" + lincred + "' and numero = '" + Numero + "' " + WhereAgencia + WhereEmpresa + WhereCencosto;

            if (clsConect.ExecuteQueryconec(stmysql, Myconnect, "ActualizaPlanilla") == false)
            {
                stmysql = "select codigoter,lincred, numero from cop_nomdes where codigoter='" +
                          Codigoter + "' and periodo = '" + Planilla + "' and periodicidad ='" + Periodicidad + "' and  adicional = '" + Adicional +
                          "' and lincred = '" + lincred + "' and numero = '" + Numero + "' " + WhereAgencia + WhereEmpresa + WhereCencosto;
                if (clsConect.ExecuteQueryconec(stmysql, Myconnect, "ActualizaPlanilla") == false)
                {
                    stmysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle,ciclo," + CampoAplicar + ") " +
                              "values('" + EmpApli + "','" + AgeApli + "','" + CenApli + "'," + Planilla + ",'" + Periodicidad + "','" + Adicional + "','" + Codigoter + "'," + lincred + "," + Numero + ",'" + DetalleDes + "','" + CicloDes + "'," + Credito + ")";
                    clsConect.ExecuteQueryconec(stmysql, Myconnect, "ActualizaPlanilla");
                }
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = mycomqueryconec.CommandText.Replace("''", "' '");

            try
            {
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();

                if (Myread.RecordsAffected > 0)
                {
                    result = true;
                }

                if (Myread.Read())
                {
                    if (!string.IsNullOrEmpty(Campo1))
                    {
                        if (Myread["campo1"] == DBNull.Value)
                        {
                            Campo1 = "0";
                        }
                        else
                        {
                            Campo1 = Myread["campo1"].ToString().Trim();
                        }
                    }
                    if (!string.IsNullOrEmpty(Campo2))
                    {
                        if (Myread["campo2"] == DBNull.Value)
                        {
                            Campo2 = "0";
                        }
                        else
                        {
                            Campo2 = Myread["campo2"].ToString().Trim();
                        }
                    }
                    if (!string.IsNullOrEmpty(Campo3))
                    {
                        if (Myread["campo3"] == DBNull.Value)
                        {
                            Campo3 = "0";
                        }
                        else
                        {
                            Campo3 = Myread["campo3"].ToString().Trim();
                        }
                    }
                    if (!string.IsNullOrEmpty(Campo4))
                    {
                        if (Myread["campo4"] == DBNull.Value)
                        {
                            Campo4 = "0";
                        }
                        else
                        {
                            Campo4 = Myread["campo4"].ToString().Trim();
                        }
                    }
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception)
            {
                result = false;
                MessageBox.Show("Error" + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return result;
        }
    }
}
