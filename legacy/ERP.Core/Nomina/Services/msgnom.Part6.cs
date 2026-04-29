using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Nomina.Services
{
    public partial class msgnom
    {
        // =====================================================================
        // Acumulados, Planilla Unica, Novedades
        // =====================================================================

        public void TrasladoNuevoPeriodo(string periodoActual, string NuevoPeriodo, Form Myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            int fila = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Traslando nuevo periodo " + periodoActual + " al " + NuevoPeriodo, Myforma);

            stbuilder.Append("select IdNomina, IdEmpleado,Idcpto,Consecutivo,saldic ");
            stbuilder.Append("from nom_sallib_vw ");
            stbuilder.Append(" where periodo = '");
            stbuilder.Append(periodoActual + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "TrasladoNuevoPerioso", ref DsDataSet, "tblsallib");

            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblsallib"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataSet.Tables["tblsallib"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblsallib"].Rows[fila];
                // this.GrabaSaldoInicialLibranzas(row["idnomina"], row["idempleado"], row["idcpto"], row["consecutivo"], NuevoPeriodo, row["saldic"], myconnect); // ERROR: CS1503
                msgbarra.PerformStep();
                fila += 1;
            }

            msgbarra.Close();
            msgbarra.Dispose();
        }

        public DataTable HelpAcumuladosCpto(int idnomina, double idempleado, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsDataset = new DataSet();

            stbuilder.Append("select liq08.idcpto,liq08.consecutivo,cptos.nombre,sum(liq08.dias) tiempo,");
            stbuilder.Append("case liq08.natur when 1 then sum(liq08.valor) else 0 end  as devengo,");
            stbuilder.Append("case liq08.natur when 2 then sum(liq08.valor) else 0 end  as Deduccion ");
            stbuilder.Append("from nom_liqplan08_vw liq08 inner join nom_cptos cptos ");
            stbuilder.Append("on liq08.idcpto = cptos.idcpto ");
            stbuilder.Append("where idnomina = '" + idnomina + "' and idempleado = '" + idempleado + "'");
            stbuilder.Append("group by liq08.idcpto,liq08.consecutivo,nombre,liq08.natur");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "HelpAcumuladosCpto", ref dsDataset, "Tblacumulados");
            return dsDataset.Tables["Tblacumulados"];
        }

        public DataTable BuscaAcumuladosCiclo(int idnomina, double idempleado, int CicloInicial, int CicloFinal, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsDataset = new DataSet();

            stbuilder.Append("select liq08.idcpto,liq08.consecutivo,cptos.nombre,sum(liq08.dias) tiempo,");
            stbuilder.Append("case liq08.natur when 1 then sum(liq08.valor) else 0 end  as devengo,");
            stbuilder.Append("case liq08.natur when 2 then sum(liq08.valor) else 0 end  as Deduccion ");
            stbuilder.Append("from nom_liqplan08_vw liq08 inner join nom_cptos cptos ");
            stbuilder.Append("on liq08.idcpto = cptos.idcpto ");
            stbuilder.Append("where idplanilla between '" + CicloInicial + "' and '" + CicloFinal + "' ");
            stbuilder.Append("and idnomina = '" + idnomina + "' and idempleado = '" + idempleado + "' ");
            stbuilder.Append("group by liq08.idcpto,liq08.consecutivo,nombre,liq08.natur");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAcumuladosCiclo", ref dsDataset, "Tblacumulados");
            return dsDataset.Tables["Tblacumulados"];
        }

        public double BuscaAcumuladosCicloConcepto(int idnomina, double idempleado, int CicloInicial, int CicloFinal, int Idcpto, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsDataset = new DataSet();

            stbuilder.Append("select liq08.idcpto,liq08.consecutivo,cptos.nombre,sum(liq08.dias) tiempo,");
            stbuilder.Append("case liq08.natur when 1 then sum(liq08.valor) else 0 end  as devengo,");
            stbuilder.Append("case liq08.natur when 2 then sum(liq08.valor) else 0 end  as Deduccion ");
            stbuilder.Append("from nom_liqplan08_vw liq08 inner join nom_cptos cptos ");
            stbuilder.Append("on liq08.idcpto = cptos.idcpto ");
            stbuilder.Append("where idplanilla between '" + CicloInicial + "' and '" + CicloFinal + "' ");
            stbuilder.Append("and idnomina = '" + idnomina + "' and idempleado = '" + idempleado + "' and liq08.idcpto = '" + Idcpto + "'");
            stbuilder.Append("group by liq08.idcpto,liq08.consecutivo,nombre,liq08.natur");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAcumuladosCicloConcepto", ref dsDataset, "Tblacumulados");
            if (dsDataset.Tables["Tblacumulados"].Rows.Count > 0)
            {
                if (Convert.ToDouble(dsDataset.Tables["Tblacumulados"].Rows[0]["devengo"]) > 0)
                {
                    return Convert.ToDouble(dsDataset.Tables["Tblacumulados"].Rows[0]["devengo"]);
                }
                else
                {
                    return Convert.ToDouble(dsDataset.Tables["Tblacumulados"].Rows[0]["Deduccion"]);
                }
            }
            return 0;
        }

        public DataTable BuscaTodosAcumuladosCiclo(int CicloInicial, int CicloFinal, int idnomina, OdbcConnection myconnect, string codaso = "")
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsDataset = new DataSet();
            string codigoter = "";
            if (codaso.Trim() != "Todos")
            {
                codigoter = " and   idempleado  ='" + codaso + "'";
            }
            stbuilder.Append("select  idnomina,idempleado,sum(valor) as valor ");
            stbuilder.Append("from nom_liqplan08_vw ");
            stbuilder.Append("where idplanilla between '" + CicloInicial + "' and '" + CicloFinal + "' and idnomina = '" + idnomina + "'  " + codigoter);
            stbuilder.Append("group by idnomina,idempleado ");
            stbuilder.Append("order by idnomina,idempleado ");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAcumuladosCiclo", ref dsDataset, "Tblacumempl");
            return dsDataset.Tables["Tblacumempl"];
        }

        public void CargaAcumulados(int idnomina, double idempleado, Form Myforma, OdbcConnection myconnect)
        {
            // nom_frmrescptos frmAcumulados = new nom_frmrescptos(); // ERROR: CS0246
            // frmAcumulados.TxtIdEmpresa.Text = idnomina.ToString(); // ERROR: CS0103
            // frmAcumulados.TxtidCodigo.Text = idempleado.ToString(); // ERROR: CS0103
            // frmAcumulados.myconnect = myconnect; // ERROR: CS0103
            // frmAcumulados.ShowDialog(Myforma); // ERROR: CS0103
        }

        public void GrabaArchivoAutLiq(string Archivo, string idempresa, string periodo, DateTime Fecpago, Form myforma, OdbcConnection myconnect)
        {
            DateTime FechaIni = default(DateTime);
            DateTime Fechafin = default(DateTime);
            string stmysql;
            ERP.Core.Compartido.Controles.Barraprogress Msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Archivo de Autoliquidacion de aportes", myforma);

            stmysql = "delete from nom_preliq  where idnomina = '" + idempresa + "' and periodo= " + periodo;
            this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GrabaArchivoAutLiq");
            stmysql = "delete from nom_respreliq  ";
            this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GrabaArchivoAutLiq");

            // this.msgconfig.buscaPeriodo(myconnect, ref FechaIni, ref Fechafin, periodo, Strings.Mid(periodo, 1, 4)); // ERROR: CS7036
            if (Fechafin.Day > 30)
            {
                Fechafin = Fechafin.AddDays(-1);
            }
            AcumulaEmpresa(idempresa, FechaIni, Fechafin, myforma, Msgbarra, myconnect, Convert.ToInt32(periodo));
            RevisaAcumulados(idempresa, Convert.ToInt32(periodo), FechaIni, Fechafin, myforma, Msgbarra, myconnect);
            RevisaMinimo(idempresa, Convert.ToInt32(periodo), FechaIni, Fechafin, myforma, Msgbarra, myconnect);
            LiquidaAportes(idempresa, Convert.ToInt32(periodo), FechaIni, Fechafin, myforma, Msgbarra, myconnect);

            GeneraPlanoPlanillaUnica(Archivo, idempresa, Convert.ToInt32(periodo), FechaIni, Fechafin, Fecpago, myforma, Msgbarra, myconnect);
            Msgbarra.Close();
            Msgbarra.Dispose();
        }

        private void AcumulaEmpresa(string idempresa, DateTime FechaIni, DateTime Fechafin, Form myforma, ERP.Core.Compartido.Controles.Barraprogress Msgbarra, OdbcConnection myconnect, int periodo)
        {
            DataSet DsDataSet = new DataSet();
            int fila = 0;
            int Sw1 = 0;
            double Salbas;
            double Sueldo = 0;
            int ClaseSalario;
            int Diast;
            int Reg = 1;
            string Npension, Nsalud, NRiesgo, Clasen, Ing, Ret;
            string Integral = " ";
            DateTime Fecini, Fecfin;
            double Valajus;
            double SalarioMinimo = 0;
            double Salario;
            double salarioActual;

            // ok = this.msgconfig.BuscaParAutLiqAportes(idempresa, myconnect, DsDataSet); // ERROR: CS1503
            if (ok == false)
            {
                MessageBox.Show("Parametros de autiliquidacion de aportes no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // msgconfig.BuscaEmpleado(idempresa, myconnect, idempresa, DsDataSet); // ERROR: CS1503

            Msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblempleados"].Rows.Count);
            Msgbarra.Show();

            while (fila < DsDataSet.Tables["tblempleados"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblempleados"].Rows[fila];
                Sw1 = 0;

                salarioActual = Convert.ToDouble(row["Salario"]);
                ClaseSalario = Convert.ToInt32(row["ClaseSalario"]);
                Salario = Convert.ToDouble(row["Salario"]);
                Npension = " "; Nsalud = " "; NRiesgo = " "; Clasen = " "; Ing = " "; Ret = " ";
                Integral = " ";

                Fecini = FechaIni; Fecfin = Fechafin;

                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    if (Convert.ToDateTime(row["FecRetiro"]) < FechaIni)
                    {
                        Sw1 = 1;
                    }
                }

                if (Convert.ToDateTime(row["fecing"]) > Fechafin)
                {
                    Sw1 = 1;
                }

                switch (Convert.ToInt32(row["tipempleado"]))
                {
                    case 2:
                    case 3:
                        Sw1 = 1;
                        break;
                }

                if (Convert.ToDateTime(row["fecing"]) >= FechaIni)
                {
                    Fecini = Convert.ToDateTime(row["fecing"]);
                    Npension = "G"; Nsalud = "G"; NRiesgo = "G"; Clasen = "G"; Ing = "X";
                }

                if (Convert.ToDateTime(row["FecRetiro"]) >= FechaIni && Convert.ToDateTime(row["FecRetiro"]) <= Fechafin)
                {
                    Fecfin = Convert.ToDateTime(row["FecRetiro"]);
                    Npension = "R"; Nsalud = "R"; NRiesgo = "R"; Clasen = "R"; Ret = "X";
                }

                Diast = ((Fecfin.Year - Fecini.Year) * 360) + ((Fecfin.Month - Fecini.Month) * 30) + ((Fecfin.Day - Fecini.Day) + 1);
                switch (Fecfin.Month)
                {
                    case 2:
                        if (Fecfin.Day == 28)
                        {
                            Diast += 2;
                        }
                        else if (Fecfin.Day == 29)
                        {
                            Diast += 1;
                        }
                        break;
                }
                if (Diast > 30)
                {
                    Diast = 30;
                }
                Salbas = Math.Round(Salario / 30 * Diast, 2);
                Valajus = (Convert.ToDouble(DsDataSet.Tables["tblparautApor"].Rows[0]["SMLV"]) / 30) * Diast;
                if (Valajus > Salbas)
                {
                    Salbas = Valajus;
                }

                if (ClaseSalario == 2)
                {
                    Integral = "X";
                    Salbas = Salbas * 0.7;
                }

                SalarioMinimo = Convert.ToDouble(DsDataSet.Tables["tblparautApor"].Rows[0]["SMLV"]);

                if (SalarioMinimo > Salbas)
                {
                    Salbas = SalarioMinimo;
                }

                if (Sw1 == 0)
                {
                    // this.msgconfig.GrabaNompreliq(row["idnomina"], row["idempleado"], 0, row["Cedula"], 0, Salbas, 0, Diast, 0, 0, 0, 0, 0, 0, 0, Clasen, Npension, Nsalud, NRiesgo, row["IdEps"], row["Idpension"], // ERROR: CS1061
                    // row["IdArp"], 0, 0, Ing, Ret, " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", 0, varini.pstUsuario, DateTime.Now, row["ClaseSalario"], Fecini, Fecfin, row["apellidos"].ToString().Trim() + " " + row["nombres"].ToString().Trim(), Reg, 0, // ERROR: CS1061
                    // 0, 0, 0, 0, 0, Salario, SalarioMinimo, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Integral, 0, 0, 0, 0, 0, 0, row["IdSubsFam"], myconnect, null, periodo, salarioActual); // ERROR: CS1061

                    this.RevisaAusentismos(row["idnomina"].ToString(), row["idempleado"].ToString(), Convert.ToInt32(FechaIni.ToString("yyyyMM")), FechaIni, Fechafin, myconnect, periodo);
                    RevisaNovedad(row["idnomina"].ToString(), row["idempleado"].ToString(), Convert.ToInt32(FechaIni.ToString("yyyyMM")), FechaIni, Fechafin, myconnect, periodo);
                }
                Msgbarra.PerformStep();
                Reg += 1;

                fila += 1;
            }
        }

        private void RevisaAcumulados(string idempresa, int Idperiodo, DateTime FechaIni, DateTime Fechafin, Form myforma, ERP.Core.Compartido.Controles.Barraprogress Msgbarra, OdbcConnection myconnect)
        {
            DataSet DsDataSet = new DataSet();
            int fila = 0;
            int Sw1 = 0;
            double Sueldo = 0;
            string Stmysql;
            int Plaini = 0;
            int Plafin = 0;
            StringBuilder Stbuilder = new StringBuilder();
            double Valor = 0;

            Stmysql = "select min(idplanilla) as campo1, max(idplanilla) as campo2 from nom_perpagos where idperiodo = '" + Idperiodo + "' and idempresa = '" + idempresa + "'";
            string _p1 = Plaini.ToString(); string _p2 = Plafin.ToString();
            this.msgodbc.ExecuteQueryconec(Stmysql, myconnect, "RevisaAcumulados", ref _p1, ref _p2);
            int.TryParse(_p1, out Plaini); int.TryParse(_p2, out Plafin);

            Stbuilder.Append("SELECT LIQ08.idplanilla, LIQ08.idnomina, liq08.idempleado,liq08.idcpto, liq08.consecutivo, liq08.natur, liq08.tiempo,liq08.valor, ");
            Stbuilder.Append("empl.clasesalario,empl.tipempleado ");
            Stbuilder.Append("FROM NOM_LIQPLAN08_VW LIQ08 ");
            Stbuilder.Append("INNER JOIN NOM_CPTOS CPTOS ON LIQ08.IDCPTO = CPTOS.IDCPTO ");
            Stbuilder.Append("inner join nom_empleados empl on liq08.idnomina = empl.idnomina and liq08.idempleado = empl.idempleado");
            Stbuilder.Append(" where LIQ08.idnomina = '" + idempresa + "' and LIQ08.idplanilla between '" + Plaini + "' and '" + Plafin + "' and cptos.basealq = 1 and cptos.clase <> 4");
            Stbuilder.Append(" order by  LIQ08.idplanilla, LIQ08.idnomina, liq08.idempleado,liq08.idcpto, liq08.consecutivo");

            this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "RevisaAcumulados", ref DsDataSet, "tblacumulado");

            Msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblacumulado"].Rows.Count, "Revisando Acumulados");

            while (fila < DsDataSet.Tables["tblacumulado"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblacumulado"].Rows[fila];
                Valor = 0; Sw1 = 0;
                Valor = Convert.ToDouble(row["valor"]);

                if (Convert.ToInt32(row["ClaseSalario"]) == 2)
                {
                    Valor = Valor * 0.7;
                }

                switch (Convert.ToInt32(row["natur"]))
                {
                    case 1:
                        // Valor = Valor;
                        break;
                    case 2:
                        Valor = Valor * -1;
                        break;
                    default:
                        Valor = 0;
                        break;
                }

                switch (Convert.ToInt32(row["tipempleado"]))
                {
                    case 2:
                    case 3:
                        Sw1 = 1;
                        break;
                }

                if (Sw1 == 0)
                {
                    // msgconfig.SumaValorNompreliq(row["idnomina"], row["idempleado"], 0, Valor, myconnect, Idperiodo); // ERROR: CS1061
                }
                Msgbarra.PerformStep();

                fila += 1;
            }
        }

        private void RevisaMinimo(string idempresa, int Idperiodo, DateTime FechaIni, DateTime Fechafin, Form myforma, ERP.Core.Compartido.Controles.Barraprogress Msgbarra, OdbcConnection myconnect)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            double ValAjus;
            double salario;
            // this.msgconfig.BuscaNompreliq(myconnect, Idperiodo, DsDataSet); // ERROR: CS1061

            Msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["Tblpreliq"].Rows.Count, "Revisando Minimo");
            Msgbarra.Show();

            while (Fila < DsDataSet.Tables["Tblpreliq"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["Tblpreliq"].Rows[Fila];
                // ok = this.msgconfig.BuscaEmpleado(row["idnomina"], row["idempleado"], myconnect, DsDataSet); // ERROR: CS1503
                if (ok == true)
                {
                    switch (Convert.ToInt32(DsDataSet.Tables["tblempleados"].Rows[0]["tipempleado"]))
                    {
                        case 1:
                        case 4:
                            switch (DsDataSet.Tables["tblempleados"].Rows[0]["ClaseSalario"].ToString())
                            {
                                case "2":
                                    row["integral"] = "X";
                                    break;
                                case "6":
                                    row["APRESENA"] = "Z";
                                    break;
                                case "7":
                                    row["APRESENA"] = "X";
                                    break;
                                default:
                                    row["integral"] = " ";
                                    break;
                            }

                            if (Convert.ToDouble(row["salbas"]) == 0)
                            {
                                row["salbas"] = row["valor"];
                            }

                            if (Convert.ToDouble(row["VALSENA"]) > 0 && Convert.ToDouble(row["VALICBF"]) > 0 && Convert.ToDouble(row["VALCCF"]) > 0)
                            {
                                row["IBCCCF"] = row["VALOR"];
                            }

                            if (Convert.ToDouble(row["SALBAS"]) > Convert.ToDouble(row["VALOR"]))
                            {
                                if (row["RET"].ToString() != "X")
                                {
                                    row["IBC"] = row["VALOR"];
                                }
                                else
                                {
                                    row["IBC"] = row["Salminimo"];
                                }
                            }
                            else
                            {
                                if (Convert.ToInt32(DsDataSet.Tables["tblempleados"].Rows[0]["ClaseSalario"]) == 2)
                                {
                                    row["IBC"] = row["SALBAS"];
                                }
                                else
                                {
                                    row["IBC"] = row["VALOR"];
                                }
                            }

                            ValAjus = Convert.ToDouble(row["valor"]) - Convert.ToDouble(row["SALBAS"]);
                            if (ValAjus > 1005)
                            {
                                row["VST"] = "X";
                            }

                            // ok = this.msgconfig.BuscaCptos(row["cpension"], myconnect, DsDataSet); // ERROR: CS1503
                            if (ok == true)
                            {
                                row["codpilpen"] = DsDataSet.Tables["tblcptos"].Rows[0]["idadmin"];
                            }

                            switch (Convert.ToInt32(DsDataSet.Tables["tblempleados"].Rows[0]["tipempleado"]))
                            {
                                case 4:
                                    row["codpilpen"] = "    ";
                                    row["ValPension"] = 0;
                                    break;
                            }

                            // ok = this.msgconfig.BuscaCptos(row["csalud"], myconnect, DsDataSet); // ERROR: CS1503
                            if (ok == true)
                            {
                                row["codpileps"] = DsDataSet.Tables["tblcptos"].Rows[0]["idadmin"];
                            }

                            // ok = this.msgconfig.BuscaCptos(row["criesgos"], myconnect, DsDataSet); // ERROR: CS1503
                            if (ok == true)
                            {
                                row["codpilarp"] = DsDataSet.Tables["tblcptos"].Rows[0]["idadmin"];
                            }

                            // ok = this.msgconfig.BuscaCptos(row["ccajacomp"], myconnect, DsDataSet); // ERROR: CS1503
                            if (ok == true)
                            {
                                row["codpilccf"] = DsDataSet.Tables["tblcptos"].Rows[0]["idadmin"];
                            }
                            salario = Convert.ToDouble(DsDataSet.Tables["tblempleados"].Rows[0]["tipempleado"]);

                            // this.msgconfig.GrabaNompreliq(row["idnomina"], row["idempleado"], row["conse"], row["cedula"], row["Valor"], row["Salbas"], row["Ibc"], row["Diast"], row["Diant"], row["dianov"], row["Valsalud"], // ERROR: CS1061
                                           // row["ValPension"], row["Valriezgo"], row["Valsolidar"], row["Sucur"], row["Clasen"], row["Npension"], row["Nsalud"], row["Nriesgo"], row["Csalud"], row["Cpension"], row["criesgos"], row["valeg"], row["valmat"], // ERROR: CS1061
                                          // row["ing"], row["ret"], row["tda"], row["tae"], row["Vsp"], row["Vst"], row["Sln"], row["Ige"], row["Lma"], row["Vac"], row["Vte"], row["avp"], row["Irp"], row["Tasarp"], // ERROR: CS1061
                                          // row["Usu"], row["Fepro"], row["Clasal"], row["fechai"], row["fechaf"], row["Nombre"], row["noreg"], row["Totemp"], row["auteg"], row["autmat"], row["valupc"], row["cva"], row["cve"], row["sueldo"], row["Salminimo"], // ERROR: CS1061
                                          // row["Clainc"], row["pror"], row["diasafe"], row["ibcarp"], row["codenov"], row["diasirp"], row["codpilpen"], row["codpileps"], row["codpilarp"], row["codpilccf"], row["valccf"], row["integral"], row["valsena"], row["Valicbf"], row["Valesap"], // ERROR: CS1061
                                          // row["valame"], row["ibcccf"], row["apresena"], row["ccajacomp"], myconnect, null, Idperiodo, salario); // ERROR: CS1061
                            break;
                    }
                    Msgbarra.PerformStep();
                }

                Fila += 1;
            }
        }

        private void LiquidaAportes(string idempresa, int Idperiodo, DateTime FechaIni, DateTime Fechafin, Form myforma, ERP.Core.Compartido.Controles.Barraprogress Msgbarra, OdbcConnection myconnect)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            double ValAjus;
            double PorSalud = 0;
            double PorPension = 0;
            double PorArp = 0;
            double Salmin = 0;
            double ValbaseFs1 = 0;
            double ValbaseFs2 = 0;
            double PorFodsol;
            double TarifaCCf = 0;
            double TarifaSena = 0;
            double TarifaIcbf = 0;
            double TarifaEsap = 0;
            double TarifAme = 0;
            double IBCARP = 0;
            double BaseArp = 0;
            string stmysql;
            double Valbasico = 0;
            double dif = 0;
            int Plaini = 0;
            int Plafin = 0;
            int idcptoVac = 0;
            double ValVac = 0;
            double SalMinDia = 0;
            double VlrDiaIbc = 0;
            bool Redondear = true;
            DataSet dsdata = new DataSet();
            double SalarioMinimo = 0;
            int DiasTra;
            double salarioAct;

            stmysql = "select min(idplanilla) as campo1, max(idplanilla) as campo2 from nom_perpagos where idperiodo = '" + Idperiodo + "' and idempresa = '" + idempresa + "'";
            string _p1 = Plaini.ToString(); string _p2 = Plafin.ToString();
            this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "RevisaAcumulados", ref _p1, ref _p2);
            int.TryParse(_p1, out Plaini); int.TryParse(_p2, out Plafin);

            // this.msgconfig.BuscaNompreliq(myconnect, Idperiodo, DsDataSet); // ERROR: CS1061
            // ok = msgconfig.BuscaEmpresa(idempresa, myconnect, DsDataSet); // ERROR: CS1503
            if (ok == true)
            {
                // ok = this.msgconfig.BuscaCptos(DsDataSet.Tables["tblempresas"].Rows[0]["IdFdoSolid"], myconnect, DsDataSet); // ERROR: CS1503
                idcptoVac = Convert.ToInt32(DsDataSet.Tables["tblempresas"].Rows[0]["IdVacasiones"]);
            }
            else
            {
                return;
            }

            // ok = this.msgconfig.BuscaParAutLiqAportes(idempresa, myconnect, DsDataSet); // ERROR: CS1503
            if (ok == true)
            {
                DataRow parRow = DsDataSet.Tables["tblparautApor"].Rows[0];
                PorSalud = Convert.ToDouble(parRow["Salud"]) / 100;
                PorPension = Convert.ToDouble(parRow["Pension"]) / 100;
                PorArp = Convert.ToDouble(parRow["Arp"]) / 100;
                Salmin = Convert.ToDouble(parRow["SMLV"]);
                PorFodsol = Convert.ToDouble(parRow["FdoSol"]) / 100;
                TarifaCCf = Convert.ToDouble(parRow["Ccf"]) / 100;
                TarifaSena = Convert.ToDouble(parRow["Sena"]) / 100;
                TarifaIcbf = Convert.ToDouble(parRow["Icbf"]) / 100;
                TarifaEsap = Convert.ToDouble(parRow["Esap"]) / 100;
                TarifAme = Convert.ToDouble(parRow["MinEdu"]) / 100;
            }

            Msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["Tblpreliq"].Rows.Count, "liquidacion de Aportes");
            Msgbarra.Show();

            while (Fila < DsDataSet.Tables["Tblpreliq"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["Tblpreliq"].Rows[Fila];
                Redondear = true;
                DiasTra = 0;
                // this.msgconfig.BuscaEmpleado(idempresa, row["idempleado"], myconnect, dsdata); // ERROR: CS1503

                if (row["apresena"].ToString() == "X" || row["apresena"].ToString() == "Z")
                {
                    row["IBC"] = row["Salbas"];
                }

                SalarioMinimo = RedondearBasesMiles(Convert.ToDouble(row["salminimo"]), row["idempleado"].ToString(), myconnect);

                if (row["VAC"].ToString() == "X")
                {
                    int diasDiff = Convert.ToInt32(row["diast"]) - Convert.ToInt32(row["dianovvac"]);
                    if (diasDiff <= 0)
                    {
                        row["IBC"] = Math.Round((Convert.ToDouble(row["Salbas"]) / 30) * Convert.ToInt32(row["dianovvac"]), 0);
                    }
                    else
                    {
                        row["IBC"] = Convert.ToDouble(row["IBC"]) + Math.Round((Convert.ToDouble(row["Salbas"]) / 30) * Convert.ToInt32(row["dianovvac"]), 0);
                    }

                    stmysql = "select round(" + row["IBC"] + ",-3) as campo1 from nom_preliq where idempleado ='" + row["idempleado"] + "' and periodo = " + Idperiodo;
                    string _ibcarp = "0";
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _ibcarp);
                    double.TryParse(_ibcarp, out IBCARP);
                    row["IBC"] = IBCARP;
                    IBCARP = 0;
                    Redondear = true;
                }

                if (Convert.ToDouble(row["IBC"]) < SalarioMinimo)
                {
                    if (row["ING"].ToString() != "X" && row["RET"].ToString() != "X" && row["SLN"].ToString() != "X")
                    {
                        row["IBC"] = row["salminimo"];
                    }
                    else
                    {
                        row["IBC"] = row["Valor"];
                    }
                }

                if (row["integral"].ToString() == "X")
                {
                    row["IBCCCF"] = row["IBC"];
                }

                SalMinDia = Convert.ToDouble(row["salminimo"]) / 30;

                int diasNov = Convert.ToInt32(row["diast"]) - Convert.ToInt32(row["dianov"]);
                if (diasNov == 0)
                {
                    VlrDiaIbc = Convert.ToDouble(row["Salbas"]) / 30;
                    DiasTra = 30;
                }
                else
                {
                    VlrDiaIbc = Convert.ToDouble(row["IBC"]) / (Convert.ToInt32(row["diast"]) - Convert.ToInt32(row["dianov"]));
                    DiasTra = (Convert.ToInt32(row["diast"]) - (Convert.ToInt32(row["dianov"]) - Convert.ToInt32(row["dianovvac"]) - Convert.ToInt32(row["dianovige"])));
                }

                if (VlrDiaIbc < SalMinDia)
                {
                    if (row["ING"].ToString() != "X" && row["RET"].ToString() != "X" && row["SLN"].ToString() != "X")
                    {
                        row["IBC"] = Math.Round(SalMinDia * Convert.ToInt32(row["diast"]), 0);
                    }
                    Redondear = false;
                }
                else
                {
                    if (Convert.ToDouble(row["IBC"]) < SalarioMinimo)
                    {
                        Redondear = true;
                    }
                }

                if (Convert.ToDouble(row["IBC"]) == SalarioMinimo)
                {
                    stmysql = "select round(" + row["IBC"] + ",-3) as campo1 from nom_preliq where idempleado ='" + row["idempleado"] + "' and periodo = " + Idperiodo;
                    string _ibcarp2 = "0";
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _ibcarp2);
                    double.TryParse(_ibcarp2, out IBCARP);
                    row["IBC"] = IBCARP;
                    IBCARP = 0;
                    Redondear = true;
                }

                // ING
                if (row["ING"].ToString() == "X")
                {
                    if (DiasTra < 30)
                    {
                        if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                        {
                            row["IBC"] = row["Valor"];
                        }
                        else
                        {
                            row["IBC"] = (SalarioMinimo / 30) * DiasTra;
                        }
                        Redondear = true;
                    }
                }

                // RET
                if (row["RET"].ToString() == "X")
                {
                    if (DiasTra < 30)
                    {
                        if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                        {
                            row["IBC"] = row["Valor"];
                        }
                        else
                        {
                            row["IBC"] = (SalarioMinimo / 30) * DiasTra;
                        }
                        Redondear = true;
                    }
                }

                // SLN
                if (row["SLN"].ToString() == "X")
                {
                    if (DiasTra < 30)
                    {
                        if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                        {
                            row["IBC"] = row["Valor"];
                        }
                        else
                        {
                            row["IBC"] = (SalarioMinimo / 30) * DiasTra;
                        }
                        Redondear = true;
                    }
                }

                if (Redondear == true)
                {
                    stmysql = "select round(" + row["IBC"] + ",-3) as campo1 from nom_preliq where idempleado ='" + row["idempleado"] + "' and periodo = " + Idperiodo;
                    string _ibcarp3 = "0";
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _ibcarp3);
                    double.TryParse(_ibcarp3, out IBCARP);
                    row["IBC"] = IBCARP;
                    IBCARP = 0;
                }

                // IBCCCF check
                if (Convert.ToDouble(row["IBCCCF"]) < Convert.ToDouble(row["salminimo"]))
                {
                    row["IBCCCF"] = row["salminimo"];
                    // ING for IBCCCF
                    if (row["ING"].ToString() == "X")
                    {
                        if (DiasTra < 30)
                        {
                            if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                            {
                                row["IBCCCF"] = row["Valor"];
                            }
                            else
                            {
                                row["IBCCCF"] = (SalarioMinimo / 30) * DiasTra;
                            }
                            Redondear = true;
                        }
                    }

                    // RET for IBCCCF
                    if (row["RET"].ToString() == "X")
                    {
                        if (DiasTra < 30)
                        {
                            if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                            {
                                row["IBCCCF"] = row["Valor"];
                            }
                            else
                            {
                                row["IBCCCF"] = (SalarioMinimo / 30) * DiasTra;
                            }
                            Redondear = true;
                        }
                    }

                    // SLN for IBCCCF
                    if (row["SLN"].ToString() == "X")
                    {
                        if (DiasTra < 30)
                        {
                            if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                            {
                                row["IBCCCF"] = row["Valor"];
                            }
                            else
                            {
                                row["IBCCCF"] = (SalarioMinimo / 30) * DiasTra;
                            }
                            Redondear = true;
                        }
                    }
                }

                row["valsalud"] = Math.Round(Convert.ToDouble(row["IBC"]) * PorSalud, 0);
                row["valsalud"] = this.RedondearBasesCentenas(row["valsalud"].ToString(), Convert.ToDouble(row["Valor"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect);

                if (row["apresena"].ToString() == "0")
                {
                    switch (Convert.ToInt32(dsdata.Tables["tblempleados"].Rows[0]["tipempleado"]))
                    {
                        case 1:
                            row["valpension"] = Math.Round(Convert.ToDouble(row["IBC"]) * PorPension, 0);
                            row["valpension"] = this.RedondearBasesCentenas(row["valpension"].ToString(), Convert.ToDouble(row["IBC"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect);
                            break;
                        default:
                            row["valpension"] = 0;
                            break;
                    }
                }

                // ok = this.msgconfig.BuscaAdmArp(row["criesgos"], myconnect, DsDataSet); // ERROR: CS1503
                if (ok == true)
                {
                    row["IBCARP"] = row["IBC"];
                    if (Convert.ToInt32(row["diast"]) == 0)
                    {
                        BaseArp = 0;
                    }
                    else
                    {
                        BaseArp = Convert.ToDouble(row["IBCARP"]) / Convert.ToInt32(row["diast"]);
                    }

                    if (BaseArp > 307666.6666)
                    {
                        row["IBCARP"] = 307666.6666 * Convert.ToInt32(row["diast"]);
                    }

                    if (row["VAC"].ToString() == "X" || row["IGE"].ToString() == "X" || row["LMA"].ToString() == "X" || row["SLN"].ToString() == "X" || row["IRP"].ToString() == "X")
                    {
                        int diasDiffArp = Convert.ToInt32(row["diast"]) - Convert.ToInt32(row["dianov"]);
                        if (diasDiffArp <= 0)
                        {
                            row["IBCARP"] = 0;
                            IBCARP = 0;
                        }
                        else
                        {
                            row["IBCARP"] = (Convert.ToDouble(row["IBC"]) / Convert.ToInt32(row["diast"])) * (Convert.ToInt32(row["diast"]) - Convert.ToInt32(row["dianov"]));
                            stmysql = "select round(" + row["IBCARP"] + ",-3) as campo1 from nom_preliq where idempleado ='" + row["idempleado"] + "' and periodo = " + Idperiodo;
                            string _ibcarpT = "0";
                            this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _ibcarpT);
                            double.TryParse(_ibcarpT, out IBCARP);
                        }

                        row["IBCARP"] = IBCARP;
                        IBCARP = 0;
                    }
                    else if (row["VAC"].ToString() == "Z" || row["IGE"].ToString() == "Z" || row["LMA"].ToString() == "Z" || row["SLN"].ToString() == "Z" || row["IRP"].ToString() == "Z")
                    {
                        Valbasico = (Convert.ToDouble(row["salbas"]) / 30) * (Convert.ToInt32(row["diast"]) - Convert.ToInt32(row["dianov"]));
                        dif = Convert.ToDouble(row["valor"]) - Convert.ToDouble(row["salbas"]);
                        if (dif < 0)
                        {
                            Valbasico = Convert.ToDouble(row["ibc"]);
                        }
                        else
                        {
                            Valbasico += dif;
                        }

                        row["IBCARP"] = Valbasico;
                    }

                    if (BaseArp < SalMinDia)
                    {
                        row["IBCARP"] = Math.Round(SalMinDia * Convert.ToInt32(row["diast"]), 0);
                        // ING for IBCARP
                        if (row["ING"].ToString() == "X")
                        {
                            if (DiasTra < 30)
                            {
                                if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                                {
                                    row["IBCARP"] = row["Valor"];
                                }
                                else
                                {
                                    row["IBCARP"] = (SalarioMinimo / 30) * DiasTra;
                                }
                                Redondear = true;
                            }
                        }

                        // RET for IBCARP
                        if (row["RET"].ToString() == "X")
                        {
                            if (DiasTra < 30)
                            {
                                if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                                {
                                    row["IBCARP"] = row["Valor"];
                                }
                                else
                                {
                                    row["IBCARP"] = (SalarioMinimo / 30) * DiasTra;
                                }
                                Redondear = true;
                            }
                        }

                        // SLN for IBCARP
                        if (row["SLN"].ToString() == "X")
                        {
                            if (DiasTra < 30)
                            {
                                if (Convert.ToDouble(row["SUELDO"]) >= Convert.ToDouble(row["salminimo"]))
                                {
                                    row["IBCARP"] = row["Valor"];
                                }
                                else
                                {
                                    row["IBCARP"] = (SalarioMinimo / 30) * DiasTra;
                                }
                                Redondear = true;
                            }
                        }
                    }
                    else
                    {
                        if (Convert.ToDouble(row["IBCARP"]) > SalarioMinimo)
                        {
                            stmysql = "select round(" + row["IBCARP"] + ",-3) as campo1 from nom_preliq where idempleado ='" + row["idempleado"] + "' and periodo = " + Idperiodo;
                            string _ibcarpR = "0";
                            this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _ibcarpR);
                            double.TryParse(_ibcarpR, out IBCARP);
                            row["IBCARP"] = IBCARP;
                        }
                    }

                    // PorArp = this.msgconfig.BuscaTarifaArpEmpleado(row["idnomina"], row["idempleado"], myconnect); // ERROR: CS1061
                    PorArp = PorArp / 100;

                    row["valriezgo"] = Math.Round(Convert.ToDouble(row["IBCARP"]) * PorArp, 0);
                    row["valriezgo"] = this.RedondearBasesCentenas(row["valriezgo"].ToString(), Convert.ToDouble(row["ibc"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect, row["IGE"].ToString(), "ARP");

                    row["tasarp"] = Math.Round(PorArp * 100, 4);
                }
                else
                {
                    MessageBox.Show("Entidad de riesgos Profesionales no esta creada " + row["criesgos"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                ValbaseFs1 = (Salmin / 30) * 4;
                ValbaseFs2 = Convert.ToDouble(row["IBC"]) / 30;

                if (ValbaseFs2 >= ValbaseFs1)
                {
                    row["valsolidar"] = LiquidaFdoSolidaridad(0, Convert.ToDouble(row["IBC"]), 1, DsDataSet);
                    row["valsolidar"] = this.RedondearBasesCentenas(row["valsolidar"].ToString(), Convert.ToDouble(row["IBC"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect);
                }

                if (TarifaCCf > 0 && row["apresena"].ToString() == "0")
                {
                    row["valccf"] = Math.Round(Convert.ToDouble(row["IBC"]) * TarifaCCf, 0);
                    row["valccf"] = this.RedondearBasesCentenas(row["valccf"].ToString(), Convert.ToDouble(row["IBC"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect);
                    row["IBCCCF"] = row["IBC"];
                }

                if (TarifaSena > 0 && row["apresena"].ToString() == "0")
                {
                    row["valsena"] = Math.Round(Convert.ToDouble(row["IBCCCF"]) * TarifaSena, 0);
                    row["valsena"] = this.RedondearBasesCentenas(row["valsena"].ToString(), Convert.ToDouble(row["IBCCCF"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect);
                }

                if (TarifaIcbf > 0 && row["apresena"].ToString() == "0")
                {
                    row["valicbf"] = Math.Round(Convert.ToDouble(row["IBCCCF"]) * TarifaIcbf, 0);
                    row["valicbf"] = this.RedondearBasesCentenas(row["valicbf"].ToString(), Convert.ToDouble(row["IBCCCF"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect, "", "ICBF");
                }

                if (TarifaEsap > 0 && row["apresena"].ToString() == "0")
                {
                    row["valesap"] = Math.Round(Convert.ToDouble(row["IBC"]) * TarifaEsap, 0);
                    row["valesap"] = this.RedondearBasesCentenas(row["valesap"].ToString(), Convert.ToDouble(row["IBC"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect);
                }

                if (TarifAme > 0)
                {
                    row["valame"] = Math.Round(Convert.ToDouble(row["IBC"]) * TarifAme, 0);
                    row["valame"] = this.RedondearBasesCentenas(row["valame"].ToString(), Convert.ToDouble(row["IBC"]), SalarioMinimo, DiasTra, row["idempleado"].ToString(), row["apresena"].ToString(), row["RET"].ToString(), row["ING"].ToString(), myconnect);
                }

                switch (row["clasen"].ToString())
                {
                    case "V":
                        row["VAC"] = "X";
                        break;
                    case "M":
                        row["LMA"] = "X";
                        break;
                    case "G":
                        row["ING"] = "X";
                        break;
                    case "R":
                        row["RET"] = "X";
                        break;
                    case "I":
                        row["IGE"] = "X";
                        break;
                }

                switch (row["apresena"].ToString())
                {
                    case "X":
                        row["IBCCCF"] = 0;
                        break;
                    case "Z":
                        row["valriezgo"] = 0;
                        row["IBCARP"] = 0;
                        row["Tasarp"] = 0;
                        row["IBCCCF"] = 0;
                        break;
                }
                salarioAct = Convert.ToDouble(dsdata.Tables["tblempleados"].Rows[0]["salario"]);

                // this.msgconfig.GrabaNompreliq(row["idnomina"], row["idempleado"], row["conse"], row["cedula"], row["Valor"], row["Salbas"], row["Ibc"], row["Diast"], row["Diant"], row["dianov"], row["Valsalud"], // ERROR: CS1061
                               // row["ValPension"], row["Valriezgo"], row["Valsolidar"], row["Sucur"], row["Clasen"], row["Npension"], row["Nsalud"], row["Nriesgo"], row["Csalud"], row["Cpension"], row["criesgos"], row["valeg"], row["valmat"], // ERROR: CS1061
                              // row["ing"], row["ret"], row["tda"], row["tae"], row["Vsp"], row["Vst"], row["Sln"], row["Ige"], row["Lma"], row["Vac"], row["Vte"], row["avp"], row["Irp"], row["Tasarp"], // ERROR: CS1061
                              // row["Usu"], row["Fepro"], row["Clasal"], row["fechai"], row["fechaf"], row["Nombre"], row["noreg"], row["Totemp"], row["auteg"], row["autmat"], row["valupc"], row["cva"], row["cve"], row["sueldo"], row["Salminimo"], // ERROR: CS1061
                              // row["Clainc"], row["pror"], row["diasafe"], row["ibcarp"], row["codenov"], row["diasirp"], row["codpilpen"], row["codpileps"], row["codpilarp"], row["codpilccf"], row["valccf"], row["integral"], row["valsena"], row["Valicbf"], row["Valesap"], // ERROR: CS1061
                              // row["valame"], row["ibcccf"], row["apresena"], row["ccajacomp"], myconnect, Redondear, Idperiodo, salarioAct); // ERROR: CS1061
                Msgbarra.PerformStep();

                Fila += 1;
            }
        }

        public virtual double RedondearBasesMiles(double Valor, string idempleado, OdbcConnection myconnect)
        {
            StringBuilder StMysql = new StringBuilder();
            double VlrBase = 0;

            StMysql.Append("select ");
            StMysql.Append("round('");
            StMysql.Append(Valor + "',-3) as campo1 ");
            StMysql.Append("from nom_preliq  where idempleado = '" + idempleado + "'");

            string _p1 = "0";
            this.msgodbc.ExecuteQueryconec(StMysql.ToString(), myconnect, "RedondearBasesMiles", ref _p1);
            double.TryParse(_p1, out VlrBase);
            Valor = VlrBase;

            return Valor;
        }

        private double RedondearBasesCentenas(string Valor, double IBC, double SalarioMinimo, int DiasLiq, string idempleado, string AprSena, string NovRet,
            string NovIng, OdbcConnection myconnect, string NovIge = "", string LiqES = "")
        {
            StringBuilder StMysql = new StringBuilder();
            double VlrBase = 0;
            double SalMinDia;
            double VlrDiaIbc = 0;
            SalMinDia = Math.Round(SalarioMinimo / 30, 0);
            VlrDiaIbc = Math.Round(IBC / DiasLiq, 0);

            if (IBC == SalarioMinimo)
            {
                VlrDiaIbc = SalMinDia;
            }

            if (VlrDiaIbc <= SalMinDia)
            {
                // ICBF special case
                if (LiqES == "ICBF")
                {
                    if (DiasLiq < 30)
                    {
                        StMysql.Append("select ");
                        StMysql.Append("round('");
                        StMysql.Append(Valor + "',-2) as campo1 ");
                        StMysql.Append("from nom_preliq  where idempleado = '" + idempleado + "'");

                        string _pIcbf = "0";
                        this.ExecuteQueryconec(StMysql.ToString(), myconnect, "RedondearBasesDecenas", ref _pIcbf);
                        double.TryParse(_pIcbf, out VlrBase);
                        Valor = VlrBase.ToString();
                        return Convert.ToDouble(Valor);
                    }
                }

                if (Strings.Right(Valor, 2).CompareTo("50") > 0)
                {
                    StMysql.Append("select ");
                    StMysql.Append("round('");
                    StMysql.Append(Valor + "',-2) as campo1 ");
                    StMysql.Append("from nom_preliq  where idempleado = '" + idempleado + "'");

                    string _pR = "0";
                    this.ExecuteQueryconec(StMysql.ToString(), myconnect, "RedondearBasesDecenas", ref _pR);
                    double.TryParse(_pR, out VlrBase);
                    Valor = VlrBase.ToString();
                }
                else
                {
                    switch (AprSena)
                    {
                        case "X":
                        case "Z":
                            // NovRet
                            switch (NovRet)
                            {
                                case "X":
                                case "Z":
                                    StMysql.Append("'");
                                    StMysql.Append(Valor + "',");
                                    break;
                                default:
                                    StMysql.Append("'");
                                    StMysql.Append(Valor + "',");
                                    break;
                            }

                            // NovIng
                            if (NovIng == "X")
                            {
                                StMysql.Remove(0, StMysql.Length);
                                StMysql.Append("select ");
                                StMysql.Append("round('");
                                StMysql.Append(Valor + "',-2) as campo1 ");
                                StMysql.Append("from nom_preliq  where idempleado = '" + idempleado + "'");
                                string _pIng = "0";
                                this.ExecuteQueryconec(StMysql.ToString(), myconnect, "RedondearBasesDecenas", ref _pIng);
                                double.TryParse(_pIng, out VlrBase);
                                Valor = VlrBase.ToString();
                            }

                            // NovIge
                            if (NovIge == "X" || NovIge == "Z")
                            {
                                if (LiqES != "ARP")
                                {
                                    StMysql.Remove(0, StMysql.Length);
                                    StMysql.Append("select ");
                                    StMysql.Append("round('");
                                    StMysql.Append(Valor + "',-2) as campo1 ");
                                    StMysql.Append("from nom_preliq  where idempleado = '" + idempleado + "'");
                                    string _pIge = "0";
                                    this.ExecuteQueryconec(StMysql.ToString(), myconnect, "RedondearBasesDecenas", ref _pIge);
                                    double.TryParse(_pIge, out VlrBase);
                                    Valor = VlrBase.ToString();
                                }
                            }
                            break;
                        default:
                            StMysql.Append("'");
                            StMysql.Append(Valor + "',");
                            break;
                    }
                }
            }
            else
            {
                if (Strings.Right(Valor, 2) == "50")
                {
                    Valor = (Convert.ToDouble(Valor) - 1).ToString();
                }

                StMysql.Append("select ");
                StMysql.Append("round('");
                StMysql.Append(Valor + "',-2) as campo1 ");
                StMysql.Append("from nom_preliq  where idempleado = '" + idempleado + "'");
                string _pElse = "0";
                this.ExecuteQueryconec(StMysql.ToString(), myconnect, "RedondearBasesDecenas", ref _pElse);
                double.TryParse(_pElse, out VlrBase);
                Valor = VlrBase.ToString();
            }

            return Convert.ToDouble(Valor);
        }

        private void GeneraPlanoPlanillaUnica(string NomArchivo, string idempresa, int Idperiodo, DateTime Fechaini, DateTime Fecfin, DateTime Fechapago, Form myforma, ERP.Core.Compartido.Controles.Barraprogress Msgbarra, OdbcConnection myconnect)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            int Reg = 1;
            string Correcion = " ";
            string Formulario = " ";
            string FechaCorrecion = "";
            double TotNomina = 0;
            string stmysql = null;
            int TotEmpl = 0;
            double PorPension;
            double PorSalud = 0;
            double PorArp = 0;
            double TarifaCCf = 0;
            double TarifaSena = 0;
            double TarifaIcbf = 0;
            double TarifaEsap = 0;
            double TarifAme = 0;
            int TipoPag = 0;
            int TipoPension = 0;
            string Apellido1, Apellido2, Nombre1, Nombre2;
            string NomEps, NitEps, DvEps;
            int RegEps = 0;
            int RegAutoRige;
            double ValorRige;
            int RegAutoLma;
            double ValorLma = 0;
            DateTime Fecha = Fechaini.AddMonths(1);
            int DiasPen = 0;
            int DiasArp = 0;
            int DiasCaja = 0;
            double IbcPension = 0;
            string PorPen;
            string TasArp;
            string TasaCCf, TasaIcbf, TasaSena, CodPilCcf;
            double SalarioBasico = 0;
            double ValbaseFs1;
            double ValbaseFs2 = 0;
            double baseFdo = 0;
            double Valeg = 0;
            string Auteg = " ";
            double Valsol = 0;
            double Valsub = 0;
            string AutLMA = " ";
            string FormaPresenta = "U";
            string NumSucursal = " ";
            DataSet dsdata = new DataSet();
            StreamWriter StrStream = new StreamWriter(NomArchivo, false);
            StrStream.Close();

            // this.msgconfig.BuscaNompreliq(myconnect, Idperiodo, DsDataSet); // ERROR: CS1061
            // ok = this.msgconfig.BuscaParAutLiqAportes(idempresa, myconnect, DsDataSet); // ERROR: CS1503
            if (ok == true)
            {
                DataRow parRow = DsDataSet.Tables["tblparautApor"].Rows[0];
                PorSalud = Math.Round(Convert.ToDouble(parRow["Salud"]) / 100, 5);
                PorPension = Math.Round(Convert.ToDouble(parRow["Pension"]) / 100, 5);
                PorArp = Math.Round(Convert.ToDouble(parRow["Arp"]) / 100, 5);
                TarifaCCf = Math.Round(Convert.ToDouble(parRow["Ccf"]) / 100, 5);
                TarifaSena = Math.Round(Convert.ToDouble(parRow["Sena"]) / 100, 5);
                TarifaIcbf = Math.Round(Convert.ToDouble(parRow["Icbf"]) / 100, 5);
                TarifaEsap = Convert.ToDouble(Strings.Format(Math.Round(Convert.ToDouble(parRow["Esap"]) / 100, 5), "0.00000"));
                TarifAme = Math.Round(Convert.ToDouble(parRow["MinEdu"]) / 100, 5);
                switch (Convert.ToInt32(parRow["forpresenta"]))
                {
                    case 0:
                        FormaPresenta = "U";
                        break;
                    case 1:
                        FormaPresenta = "S";
                        NumSucursal = "01";
                        break;
                    case 2:
                        FormaPresenta = "C";
                        break;
                }
            }

            // ok = msgconfig.BuscaEmpresa(idempresa, myconnect, DsDataSet); // ERROR: CS1503
            if (ok == true)
            {
                // this.msgconfig.BuscaCptos(DsDataSet.Tables["tblempresas"].Rows[0]["IdFdoSolid"], myconnect, DsDataSet); // ERROR: CS1503
            }
            else
            {
                return;
            }

            Msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["Tblpreliq"].Rows.Count, "Genrando Archivo Plano " + NomArchivo);
            Msgbarra.Show();

            stmysql = "select sum(preliq.ibc) as campo1 from nom_preliq preliq "
                + "inner join NOM_EMPLEADOS empl on preliq.IDNOMINA=empl.idnomina and preliq.IDEMPLEADO=empl.IDEMPLEADO "
                + "where preliq.idnomina = '" + idempresa + "' and empl.CLASESALARIO not in (6,7) and periodo = " + Idperiodo;

            string _pTotNom = "0";
            this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _pTotNom);
            double.TryParse(_pTotNom, out TotNomina);

            stmysql = "select count(*) as campo1 from nom_preliq where idnomina = '" + idempresa + "' and periodo = " + Idperiodo;
            string _pTotEmpl = "0";
            this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _pTotEmpl);
            int.TryParse(_pTotEmpl, out TotEmpl);

            DataRow parAutRow = DsDataSet.Tables["tblparautApor"].Rows[0];
            if (Information.IsNumeric(parAutRow["NumFor"]) == false)
            {
                parAutRow["NumFor"] = "0";
            }
            if (Convert.ToInt32(parAutRow["NumFor"]) > 0)
            {
                Correcion = "X";
                Formulario = parAutRow["formulario"].ToString();
                FechaCorrecion = Convert.ToDateTime(parAutRow["FecCorrecion"]).ToString("yyyy-MM-dd");
            }
            else
            {
                Formulario = " ";
                Correcion = "E";
                FechaCorrecion = "          ";
            }
            switch (Convert.ToInt32(parAutRow["TipoApor"]))
            {
                case 0:
                    TipoPag = 1;
                    break;
                default:
                    TipoPag = 5;
                    break;
            }
            PlanillaUnicaTipo1(NomArchivo, "1", "00001", parAutRow["Nombre"].ToString(), "NI", parAutRow["NumIdent"].ToString(), parAutRow["Dv"].ToString(), Correcion, Formulario, FechaCorrecion, FormaPresenta, NumSucursal, " ", parAutRow["admArp"].ToString(), Fechaini.Year.ToString(), Fechaini.Month.ToString(), Fechapago.Year.ToString(), Fechapago.Month.ToString(), "0", Fechapago, TotEmpl.ToString(), TotNomina.ToString(), TipoPag.ToString(), "0");

            while (Fila < DsDataSet.Tables["Tblpreliq"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["Tblpreliq"].Rows[Fila];
                DiasPen = Convert.ToInt32(row["DIAST"]);
                DiasArp = Convert.ToInt32(row["DIAST"]);
                DiasCaja = Convert.ToInt32(row["DIAST"]);
                IbcPension = Convert.ToDouble(row["IBC"]);
                // PorPen = PorPension.ToString(); // ERROR: CS0165
                TasArp = PorArp.ToString();
                TasaCCf = TarifaCCf.ToString();
                TasaIcbf = TarifaIcbf.ToString();
                TasaSena = TarifaSena.ToString();
                CodPilCcf = DsDataSet.Tables["tblparautApor"].Rows[0]["AdmCcf"].ToString();
                if (TipoPag == 1)
                {
                    TipoPension = 1;
                }
                else
                {
                    TipoPension = 31;
                }

                // PorArp = this.msgconfig.BuscaTarifaArpEmpleado(idempresa, row["cedula"], myconnect); // ERROR: CS1061
                PorArp = Math.Round(PorArp / 100, 5);
                TasArp = PorArp.ToString();

                // this.msgconfig.BuscaEmpleado(idempresa, row["cedula"], myconnect, dsdata); // ERROR: CS1503

                switch (row["apresena"].ToString())
                {
                    case "X":
                        TipoPension = 19;
                        DiasPen = 0; DiasCaja = 0; PorPen = "0.00000"; IbcPension = 0; TasaCCf = "0";
                        TasaCCf = "0.00000"; TasaIcbf = "0.00000"; TasaSena = "0.00000";
                        row["CODPILPEN"] = " "; CodPilCcf = " ";
                        break;
                    case "Z":
                        TipoPension = 12;
                        DiasPen = 0; DiasCaja = 0; DiasArp = 0; PorPen = "0.00000"; IbcPension = 0; TasArp = "0.00000";
                        TasaCCf = "0.00000"; TasaIcbf = "0.00000"; TasaSena = "0.00000";
                        row["apresena"] = "X";
                        row["CODPILPEN"] = " "; CodPilCcf = " ";
                        break;
                }

                Apellido1 = null; Apellido2 = null; Nombre1 = null; Nombre2 = null;
                Separanombres(row["apellidos"].ToString().Trim() + " " + row["nombres"].ToString().Trim(), ref Apellido1, ref Apellido2, ref Nombre1, ref Nombre2);
                Apellido1 = Strings.Replace(Apellido1, "\u00D1", "N");
                Apellido2 = Strings.Replace(Apellido2, "\u00D1", "N");
                Nombre1 = Strings.Replace(Nombre1, "\u00D1", "N");
                Nombre2 = Strings.Replace(Nombre2, "\u00D1", "N");

                if (Convert.ToDouble(row["valsalud"]) > 0)
                {
                    // ok = this.msgconfig.BuscaEps(row["CSALUD"], myconnect, DsDataSet); // ERROR: CS1503
                    if (ok == false)
                    {
                        MessageBox.Show("Codigo de eps no esta creada " + row["CSALUD"]);
                    }
                    else
                    {
                        NomEps = DsDataSet.Tables["tbleps"].Rows[0]["nombre"].ToString();
                        NitEps = DsDataSet.Tables["tbleps"].Rows[0]["nit"].ToString();
                        DvEps = DsDataSet.Tables["tbleps"].Rows[0]["dv"].ToString();
                        RegEps += 1;
                        if (Convert.ToDouble(row["valmat"]) > 0)
                        {
                            RegAutoLma = 1;
                            ValorLma = Convert.ToDouble(row["valmat"]);
                            AutLMA = row["autmat"].ToString();
                        }
                        else
                        {
                            ValorLma = 0;
                            RegAutoLma = 0;
                            AutLMA = " ";
                        }
                        if (row["IRP"].ToString() != "X")
                        {
                            Auteg = row["Auteg"].ToString();
                            Valeg = Convert.ToDouble(row["Valeg"]);
                        }
                        else
                        {
                            Auteg = "0";
                            Valeg = 0;
                        }

                        // this.msgconfig.GrabaNomRespreliqEps(row["CODPILEPS"], NomEps, NitEps, DvEps, RegEps, row["ibc"], row["valsalud"], 0, 0, Auteg, Valeg, RegAutoLma, ValorLma, 0, 0, "04", myconnect); // ERROR: CS1061
                    }
                }

                if (Convert.ToDouble(row["valriezgo"]) > 0)
                {
                    // ok = this.msgconfig.BuscaAdmArp(row["Criesgos"], myconnect, DsDataSet); // ERROR: CS1503
                    if (ok == false)
                    {
                        MessageBox.Show("Codigo de eps no esta creada " + row["Criesgos"]);
                    }
                    else
                    {
                        NomEps = DsDataSet.Tables["tblarp"].Rows[0]["nombre"].ToString();
                        NitEps = DsDataSet.Tables["tblarp"].Rows[0]["nit"].ToString();
                        DvEps = DsDataSet.Tables["tblarp"].Rows[0]["dv"].ToString();
                        RegEps += 1;
                        if (row["IRP"].ToString() == "X")
                        {
                            Auteg = row["Auteg"].ToString();
                            Valeg = Convert.ToDouble(row["Valeg"]);
                        }
                        else
                        {
                            Auteg = "0";
                            Valeg = 0;
                        }
                        // this.msgconfig.GrabaNomRespreliqEps(row["CODPILARP"], NomEps, NitEps, DvEps, RegEps, row["ibcarp"], row["valriezgo"], 0, 0, 0, 0, 0, 0, Auteg, Valeg, "05", myconnect); // ERROR: CS1061
                    }
                }

                ValbaseFs1 = (Convert.ToDouble(row["salminimo"]) / 30) * 4;
                ValbaseFs2 = Convert.ToDouble(row["IBC"]) / 30;
                Valsol = 0; Valsub = 0;
                if (ValbaseFs2 >= ValbaseFs1)
                {
                    row["valsolidar"] = LiquidaFdoSolidaridad(0, Convert.ToDouble(row["IBC"]), 1, DsDataSet);
                    baseFdo = Convert.ToDouble(row["IBC"]) * 0.01;
                    if (Convert.ToDouble(row["valsolidar"]) > baseFdo)
                    {
                        Valsol = Convert.ToDouble(row["valsolidar"]) * 0.25;
                        Valsub = Convert.ToDouble(row["valsolidar"]) * 0.75;
                    }
                    else
                    {
                        Valsol = Convert.ToDouble(row["valsolidar"]) * 0.5;
                        Valsub = Convert.ToDouble(row["valsolidar"]) * 0.5;
                    }

                    if (Strings.Right(Valsol.ToString(), 2) == "50")
                    {
                        Valsol -= 1;
                    }

                    if (Strings.Right(Valsub.ToString(), 2) == "50")
                    {
                        Valsub -= 1;
                    }

                    stmysql = "select round(" + Valsol + ",-2) as campo1, round(" + Valsub + ",-2) as campo2 from nom_preliq where cedula ='" + row["cedula"] + "'";
                    string _pSol = Valsol.ToString(); string _pSub = Valsub.ToString();
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "GeneraPlanoPlanillaUnica", ref _pSol, ref _pSub);
                    double.TryParse(_pSol, out Valsol); double.TryParse(_pSub, out Valsub);
                }

                SalarioBasico = Convert.ToDouble(dsdata.Tables["tblempleados"].Rows[0]["salario"]);

                switch (row["INTEGRAL"].ToString())
                {
                    case "X":
                        // ok = this.msgconfig.BuscaEmpleado(idempresa, row["cedula"], myconnect, DsDataSet); // ERROR: CS1503
                        if (ok == true)
                        {
                            SalarioBasico = Convert.ToDouble(DsDataSet.Tables["tblempleados"].Rows[0]["salario"]);
                        }
                        break;
                    default:
                        if (row["ING"].ToString() == "X" || row["RET"].ToString() == "X" || row["SLN"].ToString() == "X")
                        {
                            if ((Convert.ToInt32(row["DIAST"]) - Convert.ToInt32(row["DIANOV"])) != 30)
                            {
                                SalarioBasico = Convert.ToDouble(row["SUELDO"]);
                                if (SalarioBasico < Convert.ToDouble(row["salminimo"]))
                                {
                                    SalarioBasico = Convert.ToDouble(row["salminimo"]);
                                }
                            }
                        }
                        break;
                }

                switch (Convert.ToInt32(dsdata.Tables["tblempleados"].Rows[0]["tipempleado"]))
                {
                    case 4:
                        row["Valpension"] = 0;
                        PorPen = "0.00000";
                        DiasPen = 0;
                        row["CODPILPEN"] = " ";
                        IbcPension = 0;
                        break;
                }

                // PlanillaUnicaTipo2_2008(NomArchivo, "02", Reg.ToString(), "CC", row["cedula"].ToString(), TipoPension, 0, " ", " ", DsDataSet.Tables["tblparautApor"].Rows[0]["IdDepart"].ToString(), DsDataSet.Tables["tblparautApor"].Rows[0]["IdMunicipio"].ToString(), Apellido1, Apellido2, Nombre1, Nombre2, row["ing"].ToString(), row["ret"].ToString(), row["Tda"].ToString(), row["TAE"].ToString(), " ", " ", row["VSP"].ToString(), row["VTE"].ToString(), row["VST"].ToString(), row["SLN"].ToString(), // ERROR: CS0165
                        // row["IGE"].ToString(), row["LMA"].ToString(), row["VAC"].ToString(), row["AVP"].ToString(), " ", row["IRP"].ToString(), row["CODPILPEN"].ToString(), " ", row["CODPILEPS"].ToString(), " ", CodPilCcf, DiasPen.ToString(), row["DIAST"].ToString(), (DiasArp - Convert.ToInt32(row["DIANOV"])).ToString(), DiasCaja.ToString(), SalarioBasico.ToString(), row["INTEGRAL"].ToString(), IbcPension.ToString(), row["IBC"].ToString(), row["IBCARP"].ToString(), row["IBCCCF"].ToString(), PorPen, row["Valpension"].ToString(), "000000000", "000000000", // ERROR: CS0165
                        // row["Valpension"].ToString(), Valsol.ToString(), Valsub.ToString(), "000000000", PorSalud.ToString(), row["valsalud"].ToString(), row["valupc"].ToString(), " ", "0", AutLMA, ValorLma.ToString(), TasArp, "000000000", row["valriezgo"].ToString(), TasaCCf, row["valccf"].ToString(), TasaSena, row["valsena"].ToString(), TasaIcbf, row["valicbf"].ToString(), TarifaEsap.ToString(), row["valesap"].ToString(), TarifAme.ToString(), row["valame"].ToString()); // ERROR: CS0165

                Msgbarra.PerformStep();

                Reg += 1;
                Fila += 1;
            }
            if (Fila > 0)
            {
                PlanillaUnicaResumen(NomArchivo, myconnect);
            }
        }

        private void PlanillaUnicaResumen(string archivo, OdbcConnection myconnect)
        {
            DataSet dsDataset = new DataSet();
            int Fila = 0;
            string Tipo;
            int RegArp = 0;
            int Regeps = 0;
            // this.msgconfig.BuscaNomRespreliq(myconnect, dsDataset); // ERROR: CS1061
            while (Fila < dsDataset.Tables["tblRespreLiq"].Rows.Count)
            {
                DataRow row = dsDataset.Tables["tblRespreLiq"].Rows[Fila];
                switch (row["CLASE"].ToString())
                {
                    case "04":
                        Regeps += 1;
                        Tipo = "04";
                        PlanillaUnicaTipo4(archivo, Tipo, Regeps.ToString(), row["codpil"].ToString(), row["NIT"].ToString(), row["dv"].ToString(), row["APORTE"].ToString(), Convert.ToDouble(row["VALUPC"]), row["AUTORIGE"].ToString(), Convert.ToDouble(row["VALORIGE"]), row["AUTORLMA"].ToString(), Convert.ToDouble(row["VALORLMA"]), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                        break;
                    case "05":
                        RegArp += 1;
                        Tipo = "05";
                        if (row["VALORIRP"].ToString() == "0")
                        {
                            row["AUTORIRP"] = " ";
                        }
                        PlanillaUnicaTipo5(archivo, Tipo, RegArp.ToString(), row["codpil"].ToString(), row["NIT"].ToString(), row["dv"].ToString(), row["APORTE"].ToString(), row["AUTORIRP"].ToString(), Convert.ToDouble(row["VALORIRP"]), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                        break;
                }
                Fila += 1;
            }
        }

        private void Separanombres(string Nombre, ref string Apellidos1, ref string Apellidos2, ref string nombre1, ref string nombre2)
        {
            string[] CadenaNombres;
            CadenaNombres = Strings.Split(Nombre.Trim());
            switch (CadenaNombres.Length)
            {
                case 1:
                    Apellidos1 = CadenaNombres[0];
                    break;
                case 2:
                    Apellidos1 = CadenaNombres[0];
                    nombre1 = CadenaNombres[1];
                    break;
                case 3:
                    Apellidos1 = CadenaNombres[0];
                    Apellidos2 = CadenaNombres[1];
                    nombre1 = CadenaNombres[2];
                    break;
                case 4:
                    Apellidos1 = CadenaNombres[0];
                    Apellidos2 = CadenaNombres[1];
                    nombre1 = CadenaNombres[2];
                    nombre2 = CadenaNombres[3];
                    break;
                case 5:
                    Apellidos1 = CadenaNombres[0];
                    Apellidos2 = CadenaNombres[1];
                    nombre1 = CadenaNombres[2];
                    nombre2 = CadenaNombres[3] + " " + CadenaNombres[4];
                    break;
            }
        }

        private void PlanillaUnicaTipo1(string Archivo, string tipo, string conse, string razon, string tipoDoc, string nit, string Dv, string Correcion, string Formulario, string FechaCorrecion, string FormaPres,
            string Sucursal, string Nomsuc, string CodArp, string Anope, string MesPe, string Anosa, string Messa, string NumRadicacion, DateTime FechaPago, string NumTotPens, string Vlrtotalnomina, string TipoPag, string Codoperador)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(Strings.Right("00" + tipo, 2));
                StrDisp.Write(Strings.Right("00000" + conse, 5));
                StrDisp.Write(Strings.Left(razon + "                                                                                                                                                                                                   ", 200));
                StrDisp.Write(tipoDoc);
                StrDisp.Write(Strings.Left(nit + "                ", 16));
                StrDisp.Write(Dv);
                StrDisp.Write(Correcion);
                StrDisp.Write(Strings.Right("          " + Formulario, 10));
                StrDisp.Write(Strings.Right(FechaCorrecion + "          ", 10));
                StrDisp.Write(FormaPres);
                StrDisp.Write(Strings.Left(Sucursal + "          ", 10));
                StrDisp.Write(Strings.Left(Nomsuc + "                                        ", 40));
                StrDisp.Write(Strings.Left(CodArp + "       ", 6));
                StrDisp.Write(Anope);
                StrDisp.Write("-");
                StrDisp.Write(Strings.Right("00" + MesPe, 2));
                StrDisp.Write(Anosa);
                StrDisp.Write("-");
                StrDisp.Write(Strings.Right("00" + Messa, 2));
                StrDisp.Write(Strings.Right("00000000000" + NumRadicacion, 10));
                StrDisp.Write(FechaPago.ToString("yyyy-MM-dd"));
                StrDisp.Write(Strings.Right("00000" + NumTotPens, 5));
                StrDisp.Write(Strings.Right("000000000000" + Vlrtotalnomina, 12));
                StrDisp.Write(TipoPag);
                StrDisp.Write(Strings.Right("00" + Codoperador, 2));
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        private void PlanillaUnicaTipo1_2008(string Archivo, string razon, string tipoDoc, string nit, string Dv, string Sucursal, string Nomsuc, string ClaseAportante, string NatJuridica, string TipoPerona,
            string formaPrest, string Direccion, int Idciudad, int IdDepartamento, int IdActEconomica, string Telefono, string Fax, string Email, string IdRepreLegal, int DvReprelegal, string TipoIdentRepreLegal, string PrimerApellidoRepreLegal, string SegundoApellidoRepreLegal,
            string PrimerNombreRepreLegal, string SegundoNombreRepreLegal, string FecConcordato, int TipoAccion, string FecTerminoAct, string Codoperador, int Periodopago, int TipoAportante)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(Strings.Left(razon + "                                                                                                                                                                                        ", 200));
                StrDisp.Write(tipoDoc);
                StrDisp.Write(Strings.Left(nit + "                ", 16));
                StrDisp.Write(Dv);
                StrDisp.Write(Strings.Left(Sucursal + "          ", 10));
                StrDisp.Write(Strings.Left(Nomsuc + "                                        ", 40));
                StrDisp.Write(ClaseAportante);
                StrDisp.Write(NatJuridica);
                StrDisp.Write(TipoPerona);
                StrDisp.Write(formaPrest);
                StrDisp.Write(Strings.Left(Direccion + "                                        ", 40));
                StrDisp.Write(Strings.Left(Idciudad + "   ", 3));
                StrDisp.Write(Strings.Left(IdDepartamento + "  ", 2));
                StrDisp.Write(Strings.Left(IdActEconomica + "    ", 4));
                StrDisp.Write(Strings.Left(Telefono + "          ", 10));
                StrDisp.Write(Strings.Left(Fax + "          ", 10));
                StrDisp.Write(Strings.Left(Email + "                                                            ", 60));
                StrDisp.Write(Strings.Left(IdRepreLegal + "                ", 16));
                StrDisp.Write(DvReprelegal.ToString());
                StrDisp.Write(TipoIdentRepreLegal);
                StrDisp.Write(Strings.Left(PrimerApellidoRepreLegal + "                    ", 20));
                StrDisp.Write(Strings.Left(SegundoApellidoRepreLegal + "                              ", 30));
                StrDisp.Write(Strings.Left(PrimerNombreRepreLegal + "                    ", 20));
                StrDisp.Write(Strings.Left(SegundoNombreRepreLegal + "                              ", 30));
                StrDisp.Write(Strings.Left(FecConcordato + "          ", 10));
                StrDisp.Write(TipoAccion.ToString());
                StrDisp.Write(Strings.Left(FecTerminoAct + "          ", 10));
                StrDisp.Write(Strings.Right("00" + Codoperador, 2));
                StrDisp.Write(Periodopago.ToString());
                StrDisp.Write(TipoAportante.ToString());
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        private void PlanillaUnicaTipo4(string Archivo, string Tipo, string Secuencia, string IdEps, string NitEps, string DvEps, string ValorEps, double ValorUpc, string AutoricacionIge, double Valorige, string AutorizacionLma, double ValorLma, double ValorNeto, int DiasMora,
            double ValorMora, double ValorMoraUpc, double Total, double TotalUpc, double Formulario, double ValorAfavor, double ValorAfavorUpc, double TotalApagar, double TotalAPagarUpc, double TotalApagarAdm, double FondoSoli, int TotalAfiliados)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(Strings.Right("00" + Tipo.Trim(), 2));
                StrDisp.Write(Strings.Right("00000" + Secuencia, 5));
                StrDisp.Write(Strings.Left(IdEps + "      ", 6));
                StrDisp.Write(Strings.Left(NitEps + "                ", 16));
                StrDisp.Write(Strings.Right("0" + DvEps.Trim(), 1));
                StrDisp.Write(Strings.Right("0000000000" + ValorEps, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorUpc, 10));
                StrDisp.Write(Strings.Left(AutoricacionIge + "               ", 15));
                StrDisp.Write(Strings.Right("0000000000" + Valorige, 10));
                StrDisp.Write(Strings.Left(AutorizacionLma + "               ", 15));
                StrDisp.Write(Strings.Right("0000000000" + ValorLma, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorNeto, 10));
                StrDisp.Write(Strings.Right("0000" + DiasMora, 4));
                StrDisp.Write(Strings.Right("0000000000" + ValorMora, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorMoraUpc, 10));
                StrDisp.Write(Strings.Right("0000000000" + Total, 10));
                StrDisp.Write(Strings.Right("0000000000" + TotalUpc, 10));
                StrDisp.Write(Strings.Right("0000000000" + Formulario, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorAfavor, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorAfavorUpc, 10));
                StrDisp.Write(Strings.Right("0000000000" + TotalApagar, 10));
                StrDisp.Write(Strings.Right("0000000000" + TotalAPagarUpc, 10));
                StrDisp.Write(Strings.Right("0000000000" + TotalApagarAdm, 10));
                StrDisp.Write(Strings.Right("0000000000" + FondoSoli, 10));
                StrDisp.Write(Strings.Right("000000" + TotalAfiliados, 6));
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        private void PlanillaUnicaTipo5(string Archivo, string Tipo, string Secuencia, string Idarp, string Nit, string Dv, string ValorArp, string AutoricacionIrp, double Valorirp, double ValorOrosSub, double ValorNeto, int DiasMora,
            double ValorMora, double Subtotal, double Formulario, double ValorAfavor, double TotalApagar, double FondoSoli, int TotalAfiliados)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(Strings.Right("00" + Tipo.Trim(), 2));
                StrDisp.Write(Strings.Right("00000" + Secuencia, 5));
                StrDisp.Write(Strings.Left(Idarp + "      ", 6));
                StrDisp.Write(Strings.Left(Nit + "                ", 16));
                StrDisp.Write(Strings.Right("0" + Dv.Trim(), 1));
                StrDisp.Write(Strings.Right("0000000000" + ValorArp, 10));
                StrDisp.Write(Strings.Left(AutoricacionIrp + "               ", 15));
                StrDisp.Write(Strings.Right("0000000000" + Valorirp, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorOrosSub, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorNeto, 10));
                StrDisp.Write(Strings.Right("0000" + DiasMora, 4));
                StrDisp.Write(Strings.Right("0000000000" + ValorMora, 10));
                StrDisp.Write(Strings.Right("0000000000" + Subtotal, 10));
                StrDisp.Write(Strings.Right("0000000000" + Formulario, 10));
                StrDisp.Write(Strings.Right("0000000000" + ValorAfavor, 10));
                StrDisp.Write(Strings.Right("0000000000" + TotalApagar, 10));
                StrDisp.Write(Strings.Right("0000000000" + FondoSoli, 10));
                StrDisp.Write(Strings.Right("000000" + TotalAfiliados, 6));
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        private void PlanillaUnicaTipo2_2008(string Archivo, string TIPO, string CONSE, string TIPODOC, string NIT, int TipoCotizante, int SubCotizante, string ExtNoPension, string ColResExterior, string CodDepartamento, string CodMunicipio, string APELLIDO1,
            string APELLIDO2, string NOMBRE1, string NOMBRE2, string NOVING, string NOVRET, string NOVTDE, string NOVTAE, string NOVTDP, string NOVTAP, string NOVVSP, string NOVVTE, string NOVVST, string NOVSLN,
            string NOVIGE, string NOVLMA, string NOVVAC, string NOVAVP, string NOVVCT, string NOVIRP, string CODPENSI, string CODPENSITRAS, string CODEPS, string CODEPSTRAS, string CODCCF, string DIASPENS, string DIASEPS,
            string DIASARP, string DIASCAJA, string IBC, string INTEGRAL, string IBCPENSION, string IBCEPS, string IBCARP, string IBCCCF, string TARIFAPENSION, string APORTEPENSION, string APORTEVOLUNTA, string APORTEVOLUNTAEMP, string TOTALCOTIZACI,
            string APORTEFONDSOLI, string APORTEFONSOLISUB, string VALORNORETEN, string TARIFAEPSV, string APORTEEPS, string VALORUPC, string AUTORIZACION, string VALORINC, string AUTORIZACIONLMA, string VALORLMA, string TARIFAARP, string CENTROTRAB, string APORTEARP,
            string TARIFACCF, string APORTECCF, string TARIFASENA, string APORTESENA, string TARIFAICBF, string APORTEICBF, string TARIFAESAP, string APORTEESAP, string TARIFAME, string APORTAME)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(TIPO);
                StrDisp.Write(Strings.Right("00000" + CONSE, 5));
                StrDisp.Write(TIPODOC);
                StrDisp.Write(Strings.Left(NIT + "                ", 16));
                StrDisp.Write(Strings.Right("00" + TipoCotizante, 2));
                StrDisp.Write(Strings.Right("00" + SubCotizante, 2));
                StrDisp.Write(Strings.Right(" " + ExtNoPension, 1));
                StrDisp.Write(Strings.Right(" " + ColResExterior, 1));
                StrDisp.Write(Strings.Right("00" + CodDepartamento, 2));
                StrDisp.Write(Strings.Right("000" + CodMunicipio, 3));
                StrDisp.Write(Strings.Left(APELLIDO1 + "                    ", 20));
                StrDisp.Write(Strings.Left(APELLIDO2 + "                                ", 30));
                StrDisp.Write(Strings.Left(NOMBRE1 + "                    ", 20));
                StrDisp.Write(Strings.Left(NOMBRE2 + "                                ", 30));
                StrDisp.Write(Strings.Right(" " + NOVING, 1));
                StrDisp.Write(Strings.Right(" " + NOVRET, 1));
                StrDisp.Write(Strings.Right(" " + NOVTDE, 1));
                StrDisp.Write(Strings.Right(" " + NOVTAE, 1));
                StrDisp.Write(Strings.Right(" " + NOVTDP, 1));
                StrDisp.Write(Strings.Right(" " + NOVTAP, 1));
                StrDisp.Write(Strings.Right(" " + NOVVSP, 1));
                StrDisp.Write(Strings.Right(" " + NOVVTE, 1));
                StrDisp.Write(Strings.Right(" " + NOVVST, 1));
                StrDisp.Write(Strings.Right(" " + NOVSLN, 1));
                StrDisp.Write(Strings.Right(" " + NOVIGE, 1));
                StrDisp.Write(Strings.Right(" " + NOVLMA, 1));
                StrDisp.Write(Strings.Right(" " + NOVVAC, 1));
                StrDisp.Write(Strings.Right(" " + NOVAVP, 1));
                StrDisp.Write(Strings.Right(" " + NOVVCT, 1));
                StrDisp.Write(Strings.Right("00" + NOVIRP.Trim(), 2));
                StrDisp.Write(Strings.Left(CODPENSI + "      ", 6));
                StrDisp.Write(Strings.Left(CODPENSITRAS + "      ", 6));
                StrDisp.Write(Strings.Left(CODEPS + "      ", 6));
                StrDisp.Write(Strings.Left(CODEPSTRAS + "      ", 6));
                StrDisp.Write(Strings.Left(CODCCF + "      ", 6));
                StrDisp.Write(Strings.Right("00" + DIASPENS, 2));
                StrDisp.Write(Strings.Right("00" + DIASEPS, 2));
                StrDisp.Write(Strings.Right("00" + DIASARP, 2));
                StrDisp.Write(Strings.Right("00" + DIASCAJA, 2));
                StrDisp.Write(Strings.Right("000000000" + IBC, 9));
                StrDisp.Write(Strings.Right(" " + INTEGRAL, 1));
                StrDisp.Write(Strings.Right("000000000" + IBCPENSION, 9));
                StrDisp.Write(Strings.Right("000000000" + IBCEPS, 9));
                StrDisp.Write(Strings.Right("000000000" + IBCARP, 9));
                StrDisp.Write(Strings.Right("000000000" + IBCCCF, 9));
                StrDisp.Write(Strings.Left(TARIFAPENSION + "00000", 7));
                StrDisp.Write(Strings.Right("000000000" + APORTEPENSION, 9));
                StrDisp.Write(Strings.Right("000000000" + APORTEVOLUNTA, 9));
                StrDisp.Write(Strings.Right("000000000" + APORTEVOLUNTAEMP, 9));
                StrDisp.Write(Strings.Right("000000000" + TOTALCOTIZACI, 9));
                StrDisp.Write(Strings.Right("000000000" + APORTEFONDSOLI, 9));
                StrDisp.Write(Strings.Right("000000000" + APORTEFONSOLISUB, 9));
                StrDisp.Write(Strings.Right("000000000" + VALORNORETEN, 9));
                StrDisp.Write(Strings.Left(TARIFAEPSV + "00000", 7));
                StrDisp.Write(Strings.Right("000000000" + APORTEEPS, 9));
                StrDisp.Write(Strings.Right("000000000" + VALORUPC, 9));
                StrDisp.Write(Strings.Left(AUTORIZACION + "               ", 15));
                StrDisp.Write(Strings.Right("000000000" + VALORINC, 9));
                StrDisp.Write(Strings.Left(AUTORIZACIONLMA + "               ", 15));
                StrDisp.Write(Strings.Right("000000000" + VALORLMA, 9));
                StrDisp.Write(Strings.Left(TARIFAARP + "000000000", 9));
                StrDisp.Write(Strings.Right("000000000" + CENTROTRAB, 9));
                StrDisp.Write(Strings.Right("000000000" + APORTEARP, 9));
                StrDisp.Write(Strings.Left(TARIFACCF + "00000", 7));
                StrDisp.Write(Strings.Right("000000000" + APORTECCF, 9));
                StrDisp.Write(Strings.Left(TARIFASENA + "00000", 7));
                StrDisp.Write(Strings.Right("000000000" + APORTESENA, 9));
                StrDisp.Write(Strings.Left(TARIFAICBF + "00000", 7));
                StrDisp.Write(Strings.Right("000000000" + APORTEICBF, 9));
                StrDisp.Write(Strings.Left(Convert.ToDouble(TARIFAESAP).ToString("0.00000") + "00000", 7));
                StrDisp.Write(Strings.Right("000000000" + APORTEESAP, 9));
                StrDisp.Write(Strings.Left(Convert.ToDouble(TARIFAME).ToString("0.00000") + "00000", 7));
                StrDisp.Write(Strings.Right("000000000" + APORTAME, 9));
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        public void RevisaNovedad(string idempresa, string Idempleado, int Idperiodo, DateTime FechaIni, DateTime Fechafin, OdbcConnection myconnect, int periodo)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            double Valinc = 0;
            double salarioAct;

            // this.msgconfig.BuscaNompreliq(idempresa, Idempleado, 0, periodo, myconnect, DsDataSet); // ERROR: CS1061
            // this.msgconfig.BuscaNovLiqAportes(Idperiodo, idempresa, Idempleado, myconnect, DsDataSet); // ERROR: CS1061
            while (Fila < DsDataSet.Tables["tblnovliqApor"].Rows.Count)
            {
                Valinc = Convert.ToDouble(DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Valinc"]);

                DataRow row = DsDataSet.Tables["Tblpreliq"].Rows[0];
                switch (Convert.ToInt32(DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["novedad"]))
                {
                    case 13: // Vac
                        row["VAC"] = "Z";
                        if (row["CLASEN"].ToString() != "R")
                        {
                            row["Npension"] = "V";
                            row["Nsalud"] = "V";
                            row["Nriesgo"] = "V";
                            row["Clasen"] = "V";
                        }
                        break;
                    case 0:
                        row["ing"] = "X";
                        break;
                    case 1:
                        row["RET"] = "X";
                        break;
                    case 2:
                        row["TDA"] = "X";
                        break;
                    case 3:
                        row["Tae"] = "X";
                        if (row["clasen"].ToString() != "R")
                        {
                            if (row["csalud"].ToString() == DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["codent"].ToString())
                            {
                                row["Nsalud"] = "T";
                            }
                            if (row["cpension"].ToString() == DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["codent"].ToString())
                            {
                                row["Npension"] = "T";
                            }
                            if (row["criesgos"].ToString() == DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["codent"].ToString())
                            {
                                row["Nriesgos"] = "T";
                            }
                        }
                        break;
                    case 4:
                        row["VSP"] = "X";
                        break;
                    case 5:
                        row["VST"] = "X";
                        break;
                    case 6:
                        row["SLN"] = "X";
                        break;
                    case 7:
                        row["IGE"] = "Z";
                        if (row["CLASEN"].ToString() != "R")
                        {
                            row["Npension"] = "I";
                            row["Nsalud"] = "I";
                            row["Nriesgo"] = "I";
                            row["Clasen"] = "I";
                        }
                        if (Valinc > 0)
                        {
                            row["auteg"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["noautoriza"];
                            row["Valeg"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Valinc"];
                        }
                        break;
                    case 10:
                        row["IRP"] = "Z";
                        if (row["CLASEN"].ToString() != "R")
                        {
                            row["Nriesgo"] = "I";
                            row["Clasen"] = "P";
                        }
                        row["Diasirp"] = Convert.ToInt32(row["Diasirp"]) + Convert.ToInt32(DsDataSet.Tables["tblnovliqApor"].Rows[0]["diasnov"]);
                        if (Valinc > 0)
                        {
                            row["auteg"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["noautoriza"];
                            row["Valeg"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Valinc"];
                        }
                        break;
                    case 12:
                        row["LMA"] = "X";
                        if (row["CLASEN"].ToString() != "R")
                        {
                            row["Npension"] = "I";
                            row["Nsalud"] = "I";
                            row["Nriesgo"] = "I";
                            row["Clasen"] = "M";
                        }
                        if (Valinc > 0)
                        {
                            row["autmat"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["noautoriza"];
                            row["Valmat"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Valinc"];
                        }
                        break;
                    case 14:
                        row["VTE"] = "X";
                        break;
                }
                if (Convert.ToDouble(DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Upc"]) > 0)
                {
                    row["Upc"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Upc"];
                }
                if (Convert.ToInt32(DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Diasnov"]) > 0)
                {
                    row["Dianov"] = Convert.ToInt32(row["Dianov"]) + Convert.ToInt32(DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["Diasnov"]);
                }
                row["Codenov"] = DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["codent"];
                row["Diant"] = Convert.ToDateTime(DsDataSet.Tables["tblnovliqApor"].Rows[Fila]["fechai"]).Day - 1;
                if (Convert.ToInt32(row["Diant"]) == 0)
                {
                    row["Diant"] = 30;
                }

                ok = this.msgconfig.BuscaEmpleado(idempresa, Idempleado, myconnect, DsDataSet);
                if (ok == true)
                {
                    salarioAct = Convert.ToDouble(DsDataSet.Tables["tblempleados"].Rows[0]["salario"]);
                }
                else
                {
                    salarioAct = 0;
                }

                // this.msgconfig.GrabaNompreliq(row["idnomina"], row["idempleado"], row["conse"], row["cedula"], row["Valor"], row["Salbas"], row["Ibc"], row["Diast"], row["Diant"], row["dianov"], row["Valsalud"], // ERROR: CS1061
                 // row["ValPension"], row["Valriezgo"], row["Valsolidar"], row["Sucur"], row["Clasen"], row["Npension"], row["Nsalud"], row["Nriesgo"], row["Csalud"], row["Cpension"], row["criesgos"], row["valeg"], row["valmat"], // ERROR: CS1061
                // row["ing"], row["ret"], row["tda"], row["tae"], row["Vsp"], row["Vst"], row["Sln"], row["Ige"], row["Lma"], row["Vac"], row["Vte"], row["avp"], row["Irp"], row["Tasarp"], // ERROR: CS1061
                // row["Usu"], row["Fepro"], row["Clasal"], row["fechai"], row["fechaf"], row["Nombre"], row["noreg"], row["Totemp"], row["auteg"], row["autmat"], row["valupc"], row["cva"], row["cve"], row["sueldo"], row["Salminimo"], // ERROR: CS1061
                // row["Clainc"], row["pror"], row["diasafe"], row["ibcarp"], row["codenov"], row["diasirp"], row["codpilpen"], row["codpileps"], row["codpilarp"], row["codpilccf"], row["valccf"], row["integral"], row["valsena"], row["Valicbf"], row["Valesap"], // ERROR: CS1061
                // row["valame"], row["ibcccf"], row["apresena"], row["ccajacomp"], myconnect, null, periodo, salarioAct); // ERROR: CS1061

                Fila += 1;
            }
        }

        public void RevisaAusentismos(string idempresa, string Idempleado, int Idperiodo, DateTime FechaIni, DateTime Fechafin, OdbcConnection myconnect, int periodo)
        {
            DataSet DsDataSet = new DataSet();
            int fila = 0;
            int Sw1 = 0;
            int Diast;
            int Diant = 0;
            int DiasNov = 0;
            string VAC = null, LMA = null, IGE = null, SLN = null, IRP = null, DIASIRP = null;
            int DiasVac = 0;
            int DiasIge = 0;
            // BuscaAusentismoTrabajador(Convert.ToInt32(idempresa), Convert.ToDouble(Idempleado), myconnect, DsDataSet); // ERROR: CS1620
            while (fila < DsDataSet.Tables["tblnovausent"].Rows.Count)
            {
                Sw1 = 0; DiasVac = 0; DiasIge = 0;
                DataRow row = DsDataSet.Tables["tblnovausent"].Rows[fila];
                if (Convert.ToDateTime(row["FecInicial"]) < FechaIni && Convert.ToDateTime(row["FecFinal"]) < FechaIni)
                {
                    Sw1 = 1;
                }

                if (Convert.ToDateTime(row["FecInicial"]) > Fechafin && Convert.ToDateTime(row["FecFinal"]) > Fechafin)
                {
                    Sw1 = 1;
                }

                if (Sw1 == 0)
                {
                    if (Convert.ToDateTime(row["FecInicial"]) < FechaIni)
                    {
                        row["FecInicial"] = FechaIni;
                    }
                    if (Convert.ToDateTime(row["FecFinal"]) > Fechafin)
                    {
                        row["FecFinal"] = Fechafin;
                    }

                    Diast = (Convert.ToDateTime(row["FecFinal"]).Day - Convert.ToDateTime(row["FecInicial"]).Day) + 1;
                    Diant = Convert.ToDateTime(row["FecInicial"]).Day - 1;
                    if (Diant == 0)
                    {
                        Diant = 30;
                    }
                    DiasNov = Diast;

                    switch (Convert.ToDateTime(row["FecInicial"]).Month)
                    {
                        case 2:
                            if (Convert.ToDateTime(row["FecInicial"]).Day == 28)
                            {
                                DiasNov += 2;
                            }
                            else if (Convert.ToDateTime(row["FecInicial"]).Day == 29)
                            {
                                DiasNov += 1;
                            }
                            break;
                    }

                    ok = this.msgconfig.BuscaCptos(row["idcpto"].ToString(), myconnect, DsDataSet);
                    if (ok == true)
                    {
                        switch (DsDataSet.Tables["tblcptos"].Rows[0]["idauto"].ToString())
                        {
                            case "V":
                                VAC = "X";
                                DiasVac = DiasNov;
                                break;
                            case "M":
                                LMA = "X";
                                DiasIge = DiasNov;
                                break;
                            case "I":
                                IGE = "X";
                                DiasIge = DiasNov;
                                break;
                            case "L":
                                SLN = "X";
                                break;
                            case "A":
                                IRP = "X";
                                break;
                        }
                        DIASIRP = Diast.ToString();
                    }
                    // this.msgconfig.GrabaAusentNompreliq(idempresa, Idempleado, 0, Diant, DiasNov, VAC, LMA, IGE, SLN, IRP, DIASIRP, 0, myconnect, periodo, DiasVac, DiasIge); // ERROR: CS1061
                }

                fila += 1;
            }
        }
    }
}
