using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Nomina.Services
{
    public partial class msgnom
    {
        public void CalculaPromedioPrestaciones(int Idnomina, int PlaIniCesant, int PlaFinCesant, int PlaIniPrima, int PlaFinPrima, int PlaIniVaca, int PlaFinVaca, int PlaIniIndem, int PlaFinIndem, DateTime fecpromediosalario, System.Windows.Forms.Form Myforma, OdbcConnection myconnect)
        {
            DataSet DsDataSet = new DataSet();
            DateTime fecIniCesant = default(DateTime), fecFinCesant = default(DateTime);
            int IdTrasporte = 0;
            double ValAuxTra = 0, TopeAux = 0;
            int DiasCesant = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Calculando promedio Cesantias", Myforma);
            int DiasPrima = 0;
            DateTime fecIniPrima = default(DateTime), fecFinprima = default(DateTime);
            int DiasVac = 0;
            DateTime fecIniVac = default(DateTime), fecFinVac = default(DateTime);
            int DiasIndem = 0;
            DateTime fecIniIndem = default(DateTime), fecFinIndem = default(DateTime);

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaIniCesant, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecIniCesant = Convert.ToDateTime(row["Fecinicial"]);
                fecFinCesant = Convert.ToDateTime(row["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de Cesantias no estan creados");
                return;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaFinCesant, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecFinCesant = Convert.ToDateTime(row["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de Cesantias no estan creados");
                return;
            }

            DiasCesant = this.msgconfig.CalculaDias(fecFinCesant, fecIniCesant);
            if (fecFinCesant.Month == 2)
            {
                if (fecFinCesant.Day == 28)
                    DiasCesant += 2;
                else if (fecFinCesant.Day > 28)
                    DiasCesant += 1;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaIniPrima, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecIniPrima = Convert.ToDateTime(row["Fecinicial"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de primas no estan creados");
                return;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaFinPrima, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecFinprima = Convert.ToDateTime(row["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de primas no estan creados");
                return;
            }

            DiasPrima = this.msgconfig.CalculaDias(fecFinprima, fecIniPrima);
            if (fecFinprima.Month == 2)
            {
                if (fecFinprima.Day == 28)
                    DiasPrima += 2;
                else if (fecFinprima.Day > 28)
                    DiasPrima += 1;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaIniVaca, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecIniVac = Convert.ToDateTime(row["Fecinicial"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de Vacaciones no estan creados");
                return;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaFinVaca, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecFinVac = Convert.ToDateTime(row["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de Vacaciones no estan creados");
                return;
            }

            DiasVac = this.msgconfig.CalculaDias(fecFinVac, fecIniVac);
            if (fecFinVac.Month == 2)
            {
                if (fecFinVac.Day == 28)
                    DiasVac += 2;
                else if (fecFinVac.Day > 28)
                    DiasVac += 1;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaIniIndem, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecIniIndem = Convert.ToDateTime(row["Fecinicial"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de indemnizacion no estan creados");
                return;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(PlaFinIndem, Idnomina, myconnect, DsDataSet); // ERROR: CS1503
            if (ok)
            {
                DataRow row = DsDataSet.Tables["tblperpagos"].Rows[0];
                fecFinIndem = Convert.ToDateTime(row["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de indemnizacion no estan creados");
                return;
            }

            DiasIndem = this.msgconfig.CalculaDias(fecFinIndem, fecIniIndem);
            if (fecFinIndem.Month == 2)
            {
                if (fecFinIndem.Day == 28)
                    DiasIndem += 2;
                else if (fecFinIndem.Day > 28)
                    DiasIndem += 1;
            }

            ok = this.msgconfig.BuscaEmpresa(Idnomina, myconnect, DsDataSet);
            if (ok)
            {
                DataRow rowEmp = DsDataSet.Tables["tblempresas"].Rows[0];
                IdTrasporte = Convert.ToInt32(rowEmp["IdTrasporte"]);

                // this.msgconfig.BuscaCptos(IdTrasporte, myconnect, DsDataSet); // ERROR: CS1503
                DataRow rowCpto = DsDataSet.Tables["tblcptos"].Rows[0];
                ValAuxTra = Convert.ToDouble(rowCpto["Valor"]);
                TopeAux = Convert.ToDouble(rowCpto["Saltope"]);
            }

            msgbarra.Show();
            CalculaPromedioCesantias(Idnomina, PlaIniCesant, PlaFinCesant, fecIniCesant, fecFinCesant, DiasCesant, TopeAux, ValAuxTra, fecpromediosalario, msgbarra, myconnect);
            CalculaPromedioPrima(Idnomina, PlaIniPrima, PlaFinPrima, fecIniPrima, fecFinprima, DiasPrima, TopeAux, ValAuxTra, fecpromediosalario, msgbarra, myconnect);
            CalculaPromedioVacaciones(Idnomina, PlaIniVaca, PlaFinVaca, fecIniVac, fecFinVac, DiasVac, TopeAux, ValAuxTra, fecpromediosalario, msgbarra, myconnect);
            CalculaPromedioIndemnizacion(Idnomina, PlaIniIndem, PlaFinIndem, fecIniIndem, fecFinIndem, DiasIndem, TopeAux, ValAuxTra, fecpromediosalario, msgbarra, myconnect);
            msgbarra.Close();
            msgbarra.Dispose();
        }

        private void CalculaPromedioCesantias(int Idnomina, int PlaIniCesant, int PlaFinCesant, DateTime fecIniCesant, DateTime fecFinCesant, int Dias, double TopeAux, double ValAuxTra, DateTime fecpromediosalario, ERP.Core.Compartido.Controles.Barraprogress msgbarra, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            int SW1 = 0, DiasCesant = 0;
            string stmysql;
            double Sueldo = 0, SalDia = 0, AcuCesant = 0, PromeCesat = 0, DiasValida = 0;
            DateTime fecfin = default(DateTime);

            Stbuilder.Append("select empl.idnomina,empl.idempleado,empl.salario,empl.clasesalario,empl.fecing,");
            Stbuilder.Append("sum( liqplan.valbasecesant) as AcuCesant,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra ");
            Stbuilder.Append("from nom_empleados empl left join nom_liqplan05_vw liqplan ");
            Stbuilder.Append("on empl.idnomina =  liqplan.idnomina and empl.idempleado = liqplan.idempleado ");
            Stbuilder.Append("and liqplan.idplanilla between '" + PlaIniCesant + "' and '" + PlaFinCesant + "' ");
            Stbuilder.Append("where empl.idnomina = '" + Idnomina + "' ");
            Stbuilder.Append("group by empl.idnomina, empl.idempleado,empl.salario, empl.clasesalario,empl.fecing,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra");

            this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "CalculaPromedioPrestaciones", DsDataSet, "TblPromCesant");
            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["TblPromCesant"].Rows.Count, "Calculando promedio Cesantias");

            while (fila < DsDataSet.Tables["TblPromCesant"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["TblPromCesant"].Rows[(int)fila];
                SW1 = 0;
                DiasValida = 0;

                fecfin = fecFinCesant;

                if (row["AcuCesant"] is DBNull)
                    AcuCesant = 0;
                else
                    AcuCesant = Convert.ToDouble(row["AcuCesant"]);

                if (Convert.ToInt32(row["claseSalario"]) == 2)
                    SW1 = 1;

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);

                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    // DiasValida = this.msgconfig.CalculaDias(row["FecRetiro"], row["fecing"]); // ERROR: CS1503
                    if (DiasValida > 0 && DiasValida < 360)
                        fecfin = Convert.ToDateTime(row["FecRetiro"]);
                }

                if ((Convert.ToDateTime(row["fecing"]) > fecIniCesant) || (fecfin < fecFinCesant))
                {
                    // DiasCesant = this.msgconfig.CalculaDias(fecfin, row["fecing"]); // ERROR: CS1503
                    if (fecfin.Month == 2)
                    {
                        if (fecfin.Day == 28)
                            DiasCesant += 2;
                        else if (fecfin.Day > 28)
                            DiasCesant += 1;
                    }
                }
                else
                {
                    DiasCesant = Dias;
                }

                // Sueldo = this.msgconfig.ValidaNovedadSalario(Idnomina, row["idempleado"], row["salario"], fecpromediosalario, myconnect); // ERROR: CS1061

                if (AcuCesant > 0 && DiasCesant > 0)
                    PromeCesat = Math.Round(AcuCesant / DiasCesant, 4) * 30;
                else
                    PromeCesat = 0;

                if (Convert.ToInt32(row["ClauxTra"]) != 2)
                {
                    if (Sueldo < TopeAux)
                        Sueldo += ValAuxTra;
                }

                if (PromeCesat > 0)
                    PromeCesat = Math.Round((Sueldo + PromeCesat) / 30, 4);

                SalDia = Math.Round(Sueldo / 30, 4);

                if (SalDia > PromeCesat)
                {
                    PromeCesat = SalDia;
                    DiasCesant = 30;
                }

                if (SW1 == 0)
                {
                    stmysql = "Update nom_empleados set PromCes = '" + PromeCesat + "',diascesan='" + DiasCesant + "' where idnomina = '" + row["idnomina"] + "' and idempleado = '" + row["idempleado"] + "'";
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "CalculaPromedioCesantias");
                }

                msgbarra.PerformStep();
                fila += 1;
            }
        }

        private void CalculaPromedioPrima(int Idnomina, int PlaIniPrima, int PlaFinPrima, DateTime fecIniPrima, DateTime fecFinPrima, int Dias, double TopeAux, double ValAuxTra, DateTime fecpromediosalario, ERP.Core.Compartido.Controles.Barraprogress msgbarra, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            int SW1 = 0, DiasPrima = 0;
            string stmysql;
            double Sueldo = 0, SalDia = 0, AcuPrima = 0, PromePrima = 0, DiasValida = 0;
            DateTime fecfin = default(DateTime);

            Stbuilder.Append("select empl.idnomina,empl.idempleado,empl.salario,empl.clasesalario,empl.fecing,");
            Stbuilder.Append("sum( liqplan.valbaseprima) as AcuPrima,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra ");
            Stbuilder.Append("from nom_empleados empl left join nom_liqplan05_vw liqplan ");
            Stbuilder.Append("on empl.idnomina =  liqplan.idnomina and empl.idempleado = liqplan.idempleado ");
            Stbuilder.Append("and liqplan.idplanilla between '" + PlaIniPrima + "' and '" + PlaFinPrima + "' ");
            Stbuilder.Append("where empl.idnomina = '" + Idnomina + "'");
            Stbuilder.Append("group by empl.idnomina, empl.idempleado,empl.salario, empl.clasesalario,empl.fecing,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra");

            this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "CalculaPromedioPrestaciones", DsDataSet, "TblPromPrima");
            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["TblPromPrima"].Rows.Count, "Calculando promedio Prima de Salarios");

            while (fila < DsDataSet.Tables["TblPromPrima"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["TblPromPrima"].Rows[(int)fila];
                SW1 = 0;

                fecfin = fecFinPrima;

                if (row["AcuPrima"] is DBNull)
                    AcuPrima = 0;
                else
                    AcuPrima = Convert.ToDouble(row["AcuPrima"]);

                if (Convert.ToInt32(row["claseSalario"]) == 2)
                    SW1 = 1;

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);

                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    // DiasValida = this.msgconfig.CalculaDias(row["FecRetiro"], row["fecing"]); // ERROR: CS1503
                    if (DiasValida > 0 && DiasValida < 360)
                        fecfin = Convert.ToDateTime(row["FecRetiro"]);
                }

                if ((Convert.ToDateTime(row["fecing"]) > fecIniPrima) || (fecfin < fecFinPrima))
                {
                    // DiasPrima = this.msgconfig.CalculaDias(fecfin, row["fecing"]); // ERROR: CS1503
                    if (fecfin.Month == 2)
                    {
                        if (fecfin.Day == 28)
                            DiasPrima += 2;
                        else if (fecfin.Day > 28)
                            DiasPrima += 1;
                    }
                }
                else
                {
                    DiasPrima = Dias;
                }

                // Sueldo = this.msgconfig.ValidaNovedadSalario(Idnomina, row["idempleado"], row["salario"], fecpromediosalario, myconnect); // ERROR: CS1061

                if (AcuPrima > 0 && DiasPrima > 0)
                    PromePrima = Math.Round(AcuPrima / DiasPrima, 4) * 30;
                else
                    PromePrima = 0;

                if (Convert.ToInt32(row["ClauxTra"]) != 2)
                {
                    if (Sueldo < TopeAux)
                        Sueldo += ValAuxTra;
                }

                if (PromePrima > 0)
                    PromePrima = Math.Round((Sueldo + PromePrima) / 30, 4);

                SalDia = Math.Round(Sueldo / 30, 4);

                if (SalDia > PromePrima)
                {
                    PromePrima = SalDia;
                    DiasPrima = 30;
                }

                if (SW1 == 0)
                {
                    stmysql = "Update nom_empleados set PromPri = '" + PromePrima + "',DiasPrima='" + DiasPrima + "' where idnomina = '" + row["idnomina"] + "' and idempleado = '" + row["idempleado"] + "'";
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "CalculaPromedioCesantias");
                }

                msgbarra.PerformStep();
                fila += 1;
            }
        }

        private void CalculaPromedioVacaciones(int Idnomina, int PlaIniVaca, int PlaFinVaca, DateTime fecInivaca, DateTime fecFinVaca, int Dias, double TopeAux, double ValAuxTra, DateTime fecpromediosalario, ERP.Core.Compartido.Controles.Barraprogress msgbarra, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            int SW1 = 0, Diasvac = 0;
            string stmysql;
            double Sueldo = 0, SalDia = 0, AcuVac = 0, PromeVac = 0, DiasValida = 0;
            DateTime fecfin = default(DateTime);

            Stbuilder.Append("select empl.idnomina,empl.idempleado,empl.salario,empl.clasesalario,empl.fecing,");
            Stbuilder.Append("sum( liqplan.valbasevaca) as AcuVac,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra ");
            Stbuilder.Append("from nom_empleados empl left join nom_liqplan05_vw liqplan ");
            Stbuilder.Append("on empl.idnomina =  liqplan.idnomina and empl.idempleado = liqplan.idempleado ");
            Stbuilder.Append("and liqplan.idplanilla between '" + PlaIniVaca + "' and '" + PlaFinVaca + "' ");
            Stbuilder.Append("where empl.idnomina = '" + Idnomina + "'");
            Stbuilder.Append("group by empl.idnomina, empl.idempleado,empl.salario, empl.clasesalario,empl.fecing,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra");

            this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "CalculaPromedioPrestaciones", DsDataSet, "TblPromvaca");
            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["TblPromvaca"].Rows.Count, "Calculando promedio Prima de Salarios");

            while (fila < DsDataSet.Tables["TblPromvaca"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["TblPromvaca"].Rows[(int)fila];
                SW1 = 0;

                fecfin = fecFinVaca;

                if (row["AcuVac"] is DBNull)
                    AcuVac = 0;
                else
                    AcuVac = Convert.ToDouble(row["AcuVac"]);

                if (Convert.ToInt32(row["claseSalario"]) == 2)
                    SW1 = 1;

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);

                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    // DiasValida = this.msgconfig.CalculaDias(row["FecRetiro"], row["fecing"]); // ERROR: CS1503
                    if (DiasValida > 0 && DiasValida < 360)
                        fecfin = Convert.ToDateTime(row["FecRetiro"]);
                }

                if ((Convert.ToDateTime(row["fecing"]) > fecInivaca) || (fecfin < fecFinVaca))
                {
                    // Diasvac = this.msgconfig.CalculaDias(fecfin, row["fecing"]); // ERROR: CS1503
                    if (fecfin.Month == 2)
                    {
                        if (fecfin.Day == 28)
                            Diasvac += 2;
                        else if (fecfin.Day > 28)
                            Diasvac += 1;
                    }
                }
                else
                {
                    Diasvac = Dias;
                }

                // Sueldo = this.msgconfig.ValidaNovedadSalario(Idnomina, row["idempleado"], row["salario"], fecpromediosalario, myconnect); // ERROR: CS1061

                if (AcuVac > 0 && Diasvac > 0)
                    PromeVac = Math.Round(AcuVac / Diasvac, 4) * 30;
                else
                    PromeVac = 0;

                if (PromeVac > 0)
                    PromeVac = Math.Round((Sueldo + PromeVac) / 30, 4);

                SalDia = Math.Round(Sueldo / 30, 4);

                // Commented out in VB original:
                // if (Convert.ToInt32(row["ClauxTra"]) != 2)
                // {
                //     if (Sueldo < TopeAux)
                //         SalDia += (ValAuxTra / 30);
                // }

                if (SalDia > PromeVac)
                {
                    PromeVac = SalDia;
                    Diasvac = 30;
                }

                if (SW1 == 0)
                {
                    stmysql = "Update nom_empleados set Promevac = '" + PromeVac + "',DisVac='" + Diasvac + "' where idnomina = '" + row["idnomina"] + "' and idempleado = '" + row["idempleado"] + "'";
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "CalculaPromedioVacaciones");
                }

                msgbarra.PerformStep();
                fila += 1;
            }
        }

        private void CalculaPromedioIndemnizacion(int Idnomina, int PlaIniIndem, int PlaFinIndem, DateTime fecIniIndem, DateTime fecFinIndem, int Dias, double TopeAux, double ValAuxTra, DateTime fecpromediosalario, ERP.Core.Compartido.Controles.Barraprogress msgbarra, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            int SW1 = 0, DiasIndem = 0;
            string stmysql;
            double Sueldo = 0, SalDia = 0, AcuIndem = 0, PromeIndem = 0, DiasValida = 0;
            DateTime fecfin = default(DateTime);

            Stbuilder.Append("select empl.idnomina,empl.idempleado,empl.salario,empl.clasesalario,empl.fecing,");
            Stbuilder.Append("sum( liqplan.ValBaseIndem) as AcuIndem,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra ");
            Stbuilder.Append("from nom_empleados empl left join nom_liqplan05_vw liqplan ");
            Stbuilder.Append("on empl.idnomina =  liqplan.idnomina and empl.idempleado = liqplan.idempleado ");
            Stbuilder.Append("and liqplan.idplanilla between '" + PlaIniIndem + "' and '" + PlaFinIndem + "' ");
            Stbuilder.Append("where empl.idnomina = '" + Idnomina + "' ");
            Stbuilder.Append("group by empl.idnomina, empl.idempleado,empl.salario, empl.clasesalario,empl.fecing,empl.estado,empl.FecRetiro,empl.FecReingreso,empl.ClauxTra");

            this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "CalculaPromedioPrestaciones", DsDataSet, "TblPromCesant");
            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["TblPromCesant"].Rows.Count, "Calculando promedio Indemnizacion");

            while (fila < DsDataSet.Tables["TblPromCesant"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["TblPromCesant"].Rows[(int)fila];
                SW1 = 0;

                fecfin = fecFinIndem;

                if (row["AcuIndem"] is DBNull)
                    AcuIndem = 0;
                else
                    AcuIndem = Convert.ToDouble(row["AcuIndem"]);

                if (Convert.ToInt32(row["claseSalario"]) == 2)
                    SW1 = 1;

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);

                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    // DiasValida = this.msgconfig.CalculaDias(row["FecRetiro"], row["fecing"]); // ERROR: CS1503
                    if (DiasValida > 0 && DiasValida < 360)
                        fecfin = Convert.ToDateTime(row["FecRetiro"]);
                }

                if ((Convert.ToDateTime(row["fecing"]) > fecIniIndem) || (fecfin < fecFinIndem))
                {
                    // DiasIndem = this.msgconfig.CalculaDias(fecfin, row["fecing"]); // ERROR: CS1503
                    if (fecfin.Month == 2)
                    {
                        if (fecfin.Day == 28)
                            DiasIndem += 2;
                        else if (fecfin.Day > 28)
                            DiasIndem += 1;
                    }
                }
                else
                {
                    DiasIndem = Dias;
                }

                // Sueldo = this.msgconfig.ValidaNovedadSalario(Idnomina, row["idempleado"], row["salario"], fecpromediosalario, myconnect); // ERROR: CS1061

                if (AcuIndem > 0 && DiasIndem > 0)
                    PromeIndem = Math.Round(AcuIndem / DiasIndem, 4) * 30;
                else
                    PromeIndem = 0;

                SalDia = Math.Round(Sueldo / 30, 4);

                // Commented out in VB original:
                // if (Convert.ToInt32(row["ClauxTra"]) != 2)
                // {
                //     if (Sueldo < TopeAux)
                //         SalDia += (ValAuxTra / 30);
                // }

                if (PromeIndem > 0)
                    PromeIndem = Math.Round((Sueldo + PromeIndem) / 30, 4);

                if (SalDia > PromeIndem)
                {
                    PromeIndem = SalDia;
                    DiasIndem = 30;
                }

                if (SW1 == 0)
                {
                    stmysql = "Update nom_empleados set PromeInde = '" + PromeIndem + "',diasindem='" + DiasIndem + "' where idnomina = '" + row["idnomina"] + "' and idempleado = '" + row["idempleado"] + "'";
                    this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "CalculaPromedioIndemnizacion");
                }

                msgbarra.PerformStep();
                fila += 1;
            }
        }

        public double LiquidaCesantias(DateTime FechaLiq, DateTime FechaIngreso, int DiasLiq, int DiasTrabajados, double PromedioCesantias, double Sueldo, string EmpRegEspecial, ref int DiasLiqCes, ref double VlrDiaLiq)
        {
            int DiasCes = 0;
            double SalBas = 0, SalLiq = 0, VlrCesantias = 0;
            SalBas = Math.Round(Sueldo / 30, 2);
            if (FechaIngreso.Year < 1991 && EmpRegEspecial == "N")
            {
                DiasCes = DiasTrabajados - DiasLiq;
            }
            else
            {
                if (FechaIngreso.Year == FechaLiq.Year)
                    DiasCes = DiasTrabajados - DiasLiq;
                else
                    DiasCes = (((FechaLiq.Month - 1) * 30) + FechaLiq.Day) - DiasLiq;
            }

            if (DiasCes < 0)
                DiasCes = 0;

            if (PromedioCesantias > SalBas)
                SalLiq = PromedioCesantias;
            else
                SalLiq = SalBas;

            VlrCesantias = Math.Round(((DiasCes * 30) / 360.0) * SalLiq, 0);
            DiasLiqCes = DiasCes;
            VlrDiaLiq = SalLiq;
            return VlrCesantias;
        }

        // Overload without optional ref params
        public double LiquidaCesantias(DateTime FechaLiq, DateTime FechaIngreso, int DiasLiq, int DiasTrabajados, double PromedioCesantias, double Sueldo, string EmpRegEspecial)
        {
            int DiasLiqCes = 0;
            double VlrDiaLiq = 0;
            return LiquidaCesantias(FechaLiq, FechaIngreso, DiasLiq, DiasTrabajados, PromedioCesantias, Sueldo, EmpRegEspecial, ref DiasLiqCes, ref VlrDiaLiq);
        }

        public void GrabaBatchContabilizarPrestaciones(int Idplanilla, int idnomina, double Idempleado, int Idcpto, int Consecutivo, double Valor, OdbcConnection myconnect, int IdPlanillaAct, string TipoDocCruce)
        {
            DataSet DsDataSet = new DataSet();
            string Cuenta;
            double debito = 0, credito = 0;
            string CuentaCierre = null;
            double StNumDocCruce = 0;
            string Nit = "";
            int Idperiodo = 0;
            double SaldoCuentaProv = 0, VlrConsolidad = 0;
            string StCuentaContra = "999999999999";
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();

            ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, DsDataSet);
            if (!ok)
            {
                MessageBox.Show("Parametros de empresa no estan Creados");
                return;
            }
            else
            {
                CuentaCierre = DsDataSet.Tables["tblempresas"].Rows[0]["IdCntrprtida"].ToString();
            }

            // ok = this.msgconfig.BuscaCptos(Idcpto, myconnect, DsDataSet); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Parametros del Concepto no estan Creados");
                return;
            }

            // ok = this.msgconfig.BuscaEmpleado(idnomina, Idempleado, myconnect, DsDataSet); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Hoja de vida no esta creada " + Idempleado);
                return;
            }

            // ok = this.msgconfig.BuscaCuentasContables(Idcpto, DsDataSet.Tables["tblempleados"].Rows[0]["idcencos"], myconnect, DsDataSet); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Parametros de Cuentas contables no estan Creados " + Idcpto);
                return;
            }

            DataRow rowCuenta = DsDataSet.Tables["tblcuentas"].Rows[0];
            if (rowCuenta["ctaprov"].ToString() != "999999999999")
                Cuenta = rowCuenta["ctaprov"].ToString();
            else
                Cuenta = rowCuenta["ctagasto"].ToString();
            StCuentaContra = rowCuenta["ctacontra"].ToString();

            Nit = DsDataSet.Tables["tblempleados"].Rows[0]["Cedula"].ToString();

            DataRow rowCpto = DsDataSet.Tables["tblcptos"].Rows[0];

            if (TipoDocCruce.Trim() == "LT")
            {
                object idcptoVal = rowCpto["idcpto"];
                object idApoSoc1 = DsDataSet.Tables["tblempresas"].Rows[0]["IdApoSoc1"];
                object idApoSoc2 = DsDataSet.Tables["tblempresas"].Rows[0]["IdApoSoc2"];
                object idPrimaServ3 = DsDataSet.Tables["tblempresas"].Rows[0]["IdPrimaServ3"];

                if (idcptoVal.Equals(idApoSoc1) || idcptoVal.Equals(idApoSoc2) || idcptoVal.Equals(idPrimaServ3))
                {
                    rowCpto["natur"] = "2";
                }

                if (DsDataSet.Tables["tblempresas"].Rows[0]["IdCesantias"].Equals(rowCpto["idcpto"]))
                {
                    // ok = this.msgconfig.BuscaPeriodosPagos(IdPlanillaAct, idnomina, myconnect, DsDataSet); // ERROR: CS1503
                    if (ok)
                    {
                        Idperiodo = Convert.ToInt32(DsDataSet.Tables["tblperpagos"].Rows[0]["Idperiodo"]);
                        // SaldoCuentaProv = msgcnt.BuscarSaldoTecero(StCuentaContra, Nit, Idperiodo, myconnect); // ERROR: CS1503
                        if (SaldoCuentaProv != 0)
                        {
                            SaldoCuentaProv = SaldoCuentaProv * -1;
                            if (SaldoCuentaProv > Valor)
                            {
                                VlrConsolidad = Valor;
                                Valor = 0;
                            }
                            else
                            {
                                VlrConsolidad = SaldoCuentaProv;
                                Valor = Valor - SaldoCuentaProv;
                            }
                        }
                    }
                }
            }

            if (rowCpto["natur"].ToString() == "1")
                debito = Valor;
            else if (rowCpto["natur"].ToString() == "2")
                credito = Valor;

            if (Information.IsNumeric(rowCpto["nit"].ToString().Trim()))
            {
                if (Convert.ToDouble(rowCpto["nit"].ToString().Trim()) > 0)
                    Nit = rowCpto["nit"].ToString().Trim();
            }

            if (Idplanilla == 9999)
                StNumDocCruce = IdPlanillaAct;
            else
                StNumDocCruce = Idplanilla;

            if (debito != 0 || credito != 0)
            {
                // this.GrabaTemporal(Idplanilla, idnomina, Idcpto, Consecutivo, DsDataSet.Tables["tblempleados"].Rows[0]["idcencos"], Cuenta, Nit, TipoDocCruce, StNumDocCruce, debito, credito, Idempleado, myconnect); // ERROR: CS1503
                // this.GrabaTemporal(Idplanilla, idnomina, 0, Consecutivo, DsDataSet.Tables["tblempleados"].Rows[0]["idcencos"], CuentaCierre, DsDataSet.Tables["tblempleados"].Rows[0]["Cedula"].ToString(), TipoDocCruce, StNumDocCruce, credito, debito, Idempleado, myconnect); // ERROR: CS1503
            }

            if (VlrConsolidad != 0)
            {
                if (rowCpto["natur"].ToString() == "1")
                    debito = VlrConsolidad;
                else if (rowCpto["natur"].ToString() == "2")
                    credito = VlrConsolidad;

                if (debito != 0 || credito != 0)
                {
                    // this.GrabaTemporal(Idplanilla, idnomina, Idcpto, Consecutivo, DsDataSet.Tables["tblempleados"].Rows[0]["idcencos"], StCuentaContra, Nit, TipoDocCruce, StNumDocCruce, debito, credito, Idempleado, myconnect); // ERROR: CS1503
                    // this.GrabaTemporal(Idplanilla, idnomina, 0, Consecutivo, DsDataSet.Tables["tblempleados"].Rows[0]["idcencos"], CuentaCierre, DsDataSet.Tables["tblempleados"].Rows[0]["Cedula"].ToString(), TipoDocCruce, StNumDocCruce, credito, debito, Idempleado, myconnect); // ERROR: CS1503
                }
            }
        }

        // Overload with default optional params
        public void GrabaBatchContabilizarPrestaciones(int Idplanilla, int idnomina, double Idempleado, int Idcpto, int Consecutivo, double Valor, OdbcConnection myconnect)
        {
            GrabaBatchContabilizarPrestaciones(Idplanilla, idnomina, Idempleado, Idcpto, Consecutivo, Valor, myconnect, 999999, "VC");
        }

        public double LiquidaAnticipos(DateTime FechaLiq, DateTime FechaIngreso, double VlrAntiAnt, double VlrAnti)
        {
            double VlrLiq = 0;
            if (FechaLiq.Year < 1991)
                VlrLiq = VlrAntiAnt + VlrAnti;
            else
                VlrLiq = VlrAnti;

            return VlrLiq;
        }

        public double LiquidaInteresCesantias(DateTime FechaLiq, DateTime FechaIngreso, int DiasLiq, int DiasTrabajados, double VlrCesantias, ref int DiasLiqInt, bool Aniocorriente)
        {
            int DiasInt = 0;

            if (Aniocorriente)
            {
                if (FechaIngreso.Year == FechaLiq.Year)
                    DiasInt = DiasTrabajados - DiasLiq;
                else
                    DiasInt = (((FechaLiq.Month - 1) * 30) + FechaLiq.Day) - DiasLiq;
            }
            else
            {
                DiasInt = this.msgconfig.CalculaDias(FechaLiq, FechaIngreso) - DiasLiq;
            }

            if (DiasInt < 0)
                DiasInt = 0;

            VlrCesantias = Math.Round(((VlrCesantias * DiasInt) * 0.12) / 360, 0);
            DiasLiqInt = DiasInt;

            return VlrCesantias;
        }

        // Overload without optional ref params
        public double LiquidaInteresCesantias(DateTime FechaLiq, DateTime FechaIngreso, int DiasLiq, int DiasTrabajados, double VlrCesantias)
        {
            int DiasLiqInt = 0;
            return LiquidaInteresCesantias(FechaLiq, FechaIngreso, DiasLiq, DiasTrabajados, VlrCesantias, ref DiasLiqInt, true);
        }

        public double LiquidaIndemnizacion(DateTime FechaLiq, DateTime FechaIngreso, double Sueldo, int ClaseContrato, DateTime FecvenContrato, int DiasTrabajados, double Promedio, ref int DiasLiqIdemn, ref double VlrDiasLiq)
        {
            int DiasIdn = 0;
            double SalBas = 0, VlrIden = 0, Salliq = 0;
            int DiasLiq = 0, DiasPorAnio = 0;

            SalBas = Math.Round(Sueldo / 30, 2);
            if (DiasTrabajados > 360)
                DiasLiq = 20;

            DiasPorAnio = 30;

            if (ClaseContrato == 1)
            {
                DiasIdn = ((FecvenContrato.Year - FechaLiq.Year) * 360) - ((FecvenContrato.Month - FechaLiq.Month) * 30) - (FecvenContrato.Day - FechaLiq.Day) + 1;
            }
            else
            {
                if (DiasLiq < 361)
                {
                    DiasIdn = 45;
                }
                else
                {
                    DiasIdn = (int)Math.Round(((DiasTrabajados - 360) / 360.0) * DiasLiq, 0);
                    DiasIdn += DiasPorAnio;
                }
            }

            if (Promedio > SalBas)
                Salliq = Promedio;
            else
                Salliq = SalBas;

            VlrIden = Math.Round(DiasIdn * Salliq, 0);
            VlrDiasLiq = Salliq;
            DiasLiqIdemn = DiasIdn;
            return VlrIden;
        }

        // Overload without optional ref params
        public double LiquidaIndemnizacion(DateTime FechaLiq, DateTime FechaIngreso, double Sueldo, int ClaseContrato, DateTime FecvenContrato, int DiasTrabajados, double Promedio)
        {
            int DiasLiqIdemn = 0;
            double VlrDiasLiq = 0;
            return LiquidaIndemnizacion(FechaLiq, FechaIngreso, Sueldo, ClaseContrato, FecvenContrato, DiasTrabajados, Promedio, ref DiasLiqIdemn, ref VlrDiasLiq);
        }

        public virtual double LiquidaPrimaServicios(DateTime FechaLiq, DateTime FechaIngreso, DateTime FecCauPrima, double Sueldo, int DiasLiq, double PromedioPrimas, ref int DiasLiqPrima, ref double VlrDiaLiq)
        {
            int DiasPrima = 0;
            double SalBas = 0, SalLiq = 0, VlrPrimaServ = 0;
            DateTime FechaUltLiq;
            SalBas = Math.Round(Sueldo / 30, 2);
            if (FechaIngreso >= FecCauPrima)
                FechaUltLiq = FechaIngreso;
            else
                FechaUltLiq = FecCauPrima.AddDays(1);

            DiasPrima = this.msgconfig.CalculaDias(FechaLiq, FechaUltLiq) - DiasLiq;

            if (DiasPrima < 0)
                DiasPrima = 0;

            if (PromedioPrimas > SalBas)
                SalLiq = PromedioPrimas;
            else
                SalLiq = SalBas;

            VlrPrimaServ = Math.Round(((DiasPrima * 30) / 360.0) * SalLiq, 0);
            DiasLiqPrima = DiasPrima;
            VlrDiaLiq = SalLiq;
            return VlrPrimaServ;
        }

        // Overload without optional ref params
        public virtual double LiquidaPrimaServicios(DateTime FechaLiq, DateTime FechaIngreso, DateTime FecCauPrima, double Sueldo, int DiasLiq, double PromedioPrimas)
        {
            int DiasLiqPrima = 0;
            double VlrDiaLiq = 0;
            return LiquidaPrimaServicios(FechaLiq, FechaIngreso, FecCauPrima, Sueldo, DiasLiq, PromedioPrimas, ref DiasLiqPrima, ref VlrDiaLiq);
        }

        public virtual DataSet LiquidaPrimaServicios(int IdPlanilla, int IdNomina, int IdCptoPrima, int PlanInicial, int PlanFinal, DateTime FecCauPrimaIni, DateTime FecCauPrimaFin, DateTime FecCortePromedio, int TopeDiaLiq, string Cencosto, bool Actmovto, string Usuario, System.Windows.Forms.Form Myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            DataSet dsDataLiq = new DataSet();
            double fila = 0;
            int FactDia = 0, Dias = 0, Tiempo = 0;
            double Promedio = 0, Suedia = 0, VlrPrima = 0;
            string Stsring = " ";
            StringBuilder stbuilliq = new StringBuilder();
            DateTime FecCauPrimaInicial = default(DateTime);
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidando Prima de Servicios", Myforma);
            int IdTrasporte = 0;
            double ValAuxTra = 0, TopeAux = 0, Sueldo = 0;

            if (Cencosto.Trim() == "")
                Cencosto = "Todos";

            stbuilder.Append("SELECT empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,liqplan06.idcpto,cptos.nombre nomcpto,empl.estado , '0' tiempo, '0' promedio,'0' basico ,'0' dias , '0' Vlrprima,sum(liqplan06.ValbasePrima) Valor ");
            stbuilder.Append("FROM  nom_empleados empl left join nom_liqplan06_vw liqplan06 on liqplan06.idnomina = empl.idnomina and liqplan06.idempleado = empl.idempleado ");
            stbuilder.Append("inner join nom_cptos cptos on liqplan06.idcpto = cptos.idcpto ");
            stbuilder.Append("where liqplan06.idnomina = '" + IdNomina + "' and liqplan06.idplanilla between '" + PlanInicial + "' and '" + PlanFinal + "' and empl.estado <> 2 and empl.ClaseSalario not in ('2','6','7') and liqplan06.ValbasePrima > 0 ");
            if (Cencosto.Trim() != "Todos")
                stbuilder.Append("and empl.idcencos = '" + Cencosto.Trim() + "'");
            stbuilder.Append("group by  empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,liqplan06.idcpto,cptos.nombre,empl.estado");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LiquidaPrimaServicios", dsDataLiq, "tblliqprima");

            stbuilliq.Append("SELECT empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,empl.estado,empl.clanom,empl.fecing,empl.Salario,sum(liqplan06.ValbasePrima) as Valor, SUM(DiaMenos) as DiaMenos,empl.tipempleado,empl.FecReingreso,empl.ClauxTra ");
            stbuilliq.Append("FROM nom_empleados empl left join nom_liqplan06_vw liqplan06  on liqplan06.idnomina = empl.idnomina and liqplan06.idempleado = empl.idempleado ");
            stbuilliq.Append("inner join nom_cptos cptos on liqplan06.idcpto = cptos.idcpto ");
            stbuilliq.Append("where liqplan06.idnomina = '" + IdNomina + "' and liqplan06.idplanilla between '" + PlanInicial + "' and '" + PlanFinal + "' and empl.estado <> 2 and empl.ClaseSalario not in ('2','6','7') ");
            if (Cencosto.Trim() != "Todos")
                stbuilliq.Append("and empl.idcencos = '" + Cencosto.Trim() + "'");
            stbuilliq.Append("group by  empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,empl.estado,empl.clanom,empl.fecing,empl.Salario,empl.tipempleado,empl.FecReingreso,empl.ClauxTra ");

            this.msgodbc.ExecuteQueryDataset(stbuilliq.ToString(), myconnect, "LiquidaPrimaServicios", DsDataset, "tblliqempl");

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblliqempl"].Rows.Count);
            msgbarra.Show();

            ok = this.msgconfig.BuscaEmpresa(IdNomina, myconnect, DsDataset);
            if (ok)
            {
                IdTrasporte = Convert.ToInt32(DsDataset.Tables["tblempresas"].Rows[0]["IdTrasporte"]);

                // this.msgconfig.BuscaCptos(IdTrasporte, myconnect, DsDataset); // ERROR: CS1503
                ValAuxTra = Convert.ToDouble(DsDataset.Tables["tblcptos"].Rows[0]["Valor"]);
                TopeAux = Convert.ToDouble(DsDataset.Tables["tblcptos"].Rows[0]["Saltope"]);
            }

            while (fila < DsDataset.Tables["tblliqempl"].Rows.Count)
            {
                DataRow row = DsDataset.Tables["tblliqempl"].Rows[(int)fila];
                Promedio = 0;
                Sueldo = 0;
                Suedia = Math.Round(Convert.ToDouble(row["Salario"]) / 30, 2);

                switch (Convert.ToInt32(row["clanom"]))
                {
                    case 0:
                    case 1:
                    case 2:
                        FactDia = 360;
                        break;
                    case 3:
                    case 4:
                    case 5:
                        FactDia = 365;
                        break;
                    default:
                        FactDia = 360;
                        break;
                }

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);

                if (FecCauPrimaIni > Convert.ToDateTime(row["fecing"]))
                    FecCauPrimaInicial = FecCauPrimaIni;
                else
                    FecCauPrimaInicial = Convert.ToDateTime(row["fecing"]);

                Dias = CalculaDiac(FecCauPrimaInicial, FecCauPrimaFin, FactDia);
                Tiempo = CalculaDiap(FecCauPrimaInicial, FecCortePromedio, FactDia);

                if (FactDia == 360)
                {
                    if (Dias > 180)
                        Dias = 180;
                    if (Tiempo > 180)
                        Tiempo = 180;
                }
                else if (FactDia == 365)
                {
                    if (Dias > 184)
                        Dias = 184;
                    if (Tiempo > 184)
                        Tiempo = 184;
                }

                if (!(row["DiaMenos"] is DBNull))
                    Dias -= Convert.ToInt32(row["DiaMenos"]);

                if (!(row["Valor"] is DBNull))
                    Promedio = Math.Round((Convert.ToDouble(row["Valor"]) / Dias) * 30, 2);

                Sueldo = Convert.ToDouble(row["Salario"]);

                if (row["tipempleado"].ToString() == "1")
                {
                    if (Convert.ToInt32(row["ClauxTra"]) != 2)
                    {
                        if (Sueldo < TopeAux)
                            Sueldo += ValAuxTra;
                    }
                }

                Promedio = (Promedio + Sueldo) / 30;

                if (row["tipempleado"].ToString() == "2" || row["tipempleado"].ToString() == "3")
                    FactDia = Tiempo;

                if (Dias > TopeDiaLiq)
                    VlrPrima = Math.Round(((Tiempo * 30) / (double)FactDia) * Promedio);
                else
                    VlrPrima = 0;

                if (Convert.ToInt32(row["estado"]) == 2)
                    VlrPrima = 0;

                dsDataLiq.Tables["tblliqprima"].Rows.Add(row["idnomina"], row["idempleado"], row["apellidos"], row["nombres"], 0, " ", 0, Dias, Promedio, Suedia, Tiempo, VlrPrima, row["Valor"]);

                if (Actmovto)
                {
                    if (VlrPrima > 0)
                    {
                        this.GrabaMovimiento(IdPlanilla, IdNomina, row["idempleado"], IdCptoPrima, IdPlanilla, Tiempo, VlrPrima, 1, Usuario, myconnect);
                        // this.msgconfig.ActualizaFecCausaPrimas(IdNomina, row["idempleado"], FecCauPrimaFin, myconnect); // ERROR: CS1061
                    }
                }

                msgbarra.PerformStep();
                fila += 1;
            }

            msgbarra.Close();
            msgbarra.Dispose();
            return dsDataLiq;
        }

        public DataSet LiquidaInteresesCesantias(int IdPlanilla, int IdNomina, int IdCptoPrima, int PlanInicial, int PlanFinal, DateTime FecCauPrimaIni, DateTime FecCauPrimaFin, DateTime FecCortePromedio, int TopeDiaLiq, string Cencosto, bool Actmovto, string Usuario, System.Windows.Forms.Form Myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            DataSet dsDataLiq = new DataSet();
            double fila = 0;
            int FactDia = 0, Dias = 0, Tiempo = 0;
            double Promedio = 0, Suedia = 0, VlrPrima = 0, Sueldo = 0;
            string Stsring = " ";
            StringBuilder stbuilliq = new StringBuilder();
            DateTime FecCauPrimaInicial = default(DateTime);
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidando Prima de Servicios", Myforma);
            double IdplanillaIni = 0, VlrIntAnticipo = 0, TopeAux = 0, ValAuxTra = 0;

            if (Cencosto.Trim() == "")
                Cencosto = "Todos";

            stbuilder.Append("SELECT empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,liqplan06.idcpto,cptos.nombre nomcpto,empl.estado , '0' tiempo, '0' promedio,'0' basico ,'0' dias , '0' Vlrprima,sum(liqplan06.ValbaseCesant) Valor ");
            stbuilder.Append("FROM  nom_empleados empl left join nom_liqplan11_vw liqplan06 on liqplan06.idnomina = empl.idnomina and liqplan06.idempleado = empl.idempleado ");
            stbuilder.Append("inner join nom_cptos cptos on liqplan06.idcpto = cptos.idcpto ");
            stbuilder.Append("where empl.idnomina = '" + IdNomina + "' and liqplan06.idplanilla between '" + PlanInicial + "' and '" + PlanFinal + "' and empl.estado <> 2 and empl.ClaseSalario not in ('2','6','7') and liqplan06.ValbaseCesant > 0 ");
            if (Cencosto.Trim() != "Todos")
                stbuilder.Append("and empl.idcencos = '" + Cencosto.Trim() + "'");
            stbuilder.Append("group by  empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,liqplan06.idcpto,cptos.nombre,empl.estado");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LiquidaPrimaServicios", dsDataLiq, "tblliqprima");

            stbuilliq.Append("SELECT empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,empl.estado,empl.clanom,empl.fecing,empl.Salario,sum(liqplan06.ValbaseCesant) as Valor, SUM(DiaMenos) as DiaMenos,empl.FecReingreso,empl.tipempleado ");
            stbuilliq.Append("FROM  nom_empleados empl left join nom_liqplan11_vw liqplan06 on liqplan06.idnomina = empl.idnomina and liqplan06.idempleado = empl.idempleado ");
            stbuilliq.Append("inner join nom_cptos cptos on liqplan06.idcpto = cptos.idcpto ");
            stbuilliq.Append("where liqplan06.idnomina = '" + IdNomina + "' and liqplan06.idplanilla between '" + PlanInicial + "' and '" + PlanFinal + "' and empl.estado <> 2 and empl.ClaseSalario not in ('2','6','7') ");
            if (Cencosto.Trim() != "Todos")
                stbuilliq.Append("and empl.idcencos = '" + Cencosto.Trim() + "'");
            stbuilliq.Append("group by empl.idnomina,empl.idempleado,empl.apellidos,empl.nombres,empl.estado,empl.clanom,empl.fecing,empl.Salario,empl.FecReingreso,empl.tipempleado");

            this.msgodbc.ExecuteQueryDataset(stbuilliq.ToString(), myconnect, "LiquidaPrimaServicios", DsDataset, "tblliqempl");

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblliqempl"].Rows.Count);
            msgbarra.Show();

            ok = this.msgconfig.BuscaEmpresa(IdNomina, myconnect, DsDataset);
            if (ok)
            {
                // this.msgconfig.BuscaCptos(Convert.ToInt32(DsDataset.Tables["tblempresas"].Rows[0]["IdTrasporte"]), myconnect, DsDataset); // ERROR: CS1503
                ValAuxTra = Convert.ToDouble(DsDataset.Tables["tblcptos"].Rows[0]["Valor"]);
                TopeAux = Convert.ToDouble(DsDataset.Tables["tblcptos"].Rows[0]["Saltope"]);
            }

            while (fila < DsDataset.Tables["tblliqempl"].Rows.Count)
            {
                DataRow row = DsDataset.Tables["tblliqempl"].Rows[(int)fila];
                Promedio = 0;
                Sueldo = 0;
                Suedia = Math.Round(Convert.ToDouble(row["Salario"]) / 30, 2);

                switch (Convert.ToInt32(row["clanom"]))
                {
                    case 0:
                    case 1:
                    case 2:
                        FactDia = 360;
                        break;
                    case 3:
                    case 4:
                    case 5:
                        FactDia = 365;
                        break;
                    default:
                        FactDia = 360;
                        break;
                }

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);

                if (FecCauPrimaIni > Convert.ToDateTime(row["fecing"]))
                    FecCauPrimaInicial = FecCauPrimaIni;
                else
                    FecCauPrimaInicial = Convert.ToDateTime(row["fecing"]);

                Dias = CalculaDiac(FecCauPrimaInicial, FecCauPrimaFin, FactDia);
                Tiempo = CalculaDiap(FecCauPrimaInicial, FecCortePromedio, FactDia);

                if (FactDia == 360)
                {
                    if (Dias > 360)
                        Dias = 360;
                    if (Tiempo > 360)
                        Tiempo = 360;
                }
                else if (FactDia == 365)
                {
                    if (Dias > 365)
                        Dias = 365;
                    if (Tiempo > 365)
                        Tiempo = 365;
                }

                if (!(row["DiaMenos"] is DBNull))
                    Dias -= Convert.ToInt32(row["DiaMenos"]);

                if (!(row["Valor"] is DBNull))
                    Promedio = Math.Round((Convert.ToDouble(row["Valor"]) / Dias) * 30, 2);

                Sueldo = Convert.ToDouble(row["Salario"]);

                if (Sueldo < TopeAux)
                    Sueldo += ValAuxTra;

                Promedio = (Promedio + Sueldo) / 30;

                if (Dias > TopeDiaLiq)
                    VlrPrima = Math.Round(((((Tiempo * 30) / (double)FactDia) * Promedio) * ((0.12 * Dias) / (double)FactDia)));
                else
                    VlrPrima = 0;

                if (row["tipempleado"].ToString() == "2" || row["tipempleado"].ToString() == "3")
                    VlrPrima = 0;

                if (Convert.ToInt32(row["estado"]) == 2)
                    VlrPrima = 0;

                double _valCes = 0;
                VlrIntAnticipo = 0;
                this.BuscaAnticiposCesantiasLiquidados(IdNomina, Convert.ToDouble(row["idempleado"]), PlanInicial, PlanFinal, myconnect, ref _valCes, ref VlrIntAnticipo);
                if (VlrIntAnticipo > 0)
                    VlrPrima -= VlrIntAnticipo;

                dsDataLiq.Tables["tblliqprima"].Rows.Add(row["idnomina"], row["idempleado"], row["apellidos"], row["nombres"], 0, " ", 0, Tiempo, Promedio, Suedia, Dias, VlrPrima, row["Valor"]);

                if (Actmovto)
                {
                    if (VlrPrima > 0)
                    {
                        this.GrabaMovimiento(IdPlanilla, IdNomina, row["idempleado"], IdCptoPrima, IdPlanilla, Tiempo, VlrPrima, 1, Usuario, myconnect);
                    }
                }

                msgbarra.PerformStep();
                fila += 1;
            }

            msgbarra.Close();
            msgbarra.Dispose();
            return dsDataLiq;
        }

        private int CalculaDiac(DateTime FecCauPrimaIni, DateTime FecCauPrimaFin, int FactDia)
        {
            int dias = 0;
            if (FactDia == 360)
            {
                dias = ((FecCauPrimaFin.Year - FecCauPrimaIni.Year) * 360) + ((FecCauPrimaFin.Month - FecCauPrimaIni.Month) * 30) + (FecCauPrimaFin.Day - FecCauPrimaIni.Day) + 1;
            }
            return dias;
        }

        private int CalculaDiap(DateTime FecCauPrimaIni, DateTime FecCortePromedio, int FactDia)
        {
            int dias = 0;
            if (FactDia == 360)
            {
                dias = ((FecCortePromedio.Year - FecCauPrimaIni.Year) * 360) + ((FecCortePromedio.Month - FecCauPrimaIni.Month) * 30) + (FecCortePromedio.Day - FecCauPrimaIni.Day) + 1;
            }
            return dias;
        }

        public double LiquidaVacaciones(DateTime FechaLiq, DateTime FechaIngreso, DateTime FecCauVacaciones, double Sueldo, int DiasLiq, double PromedioVacaciones, ref int DiasLiqVac, ref double VlrDiaLiq)
        {
            int Diasvac = 0;
            double SalBas = 0, SalLiq = 0, VlrVacaciones = 0;
            DateTime FechaUltLiq;
            SalBas = Math.Round(Sueldo / 30, 2);
            if (FechaIngreso >= FecCauVacaciones)
                FechaUltLiq = FechaIngreso;
            else
                FechaUltLiq = FecCauVacaciones.AddDays(1);

            Diasvac = this.msgconfig.CalculaDias(FechaLiq, FechaUltLiq) - DiasLiq;

            if (Diasvac < 0)
                Diasvac = 0;

            if (PromedioVacaciones > SalBas)
                SalLiq = PromedioVacaciones;
            else
                SalLiq = SalBas;

            VlrVacaciones = Math.Round(((Diasvac * 15) / 360.0) * SalLiq, 0);
            DiasLiqVac = Diasvac;
            VlrDiaLiq = SalLiq;
            return VlrVacaciones;
        }

        // Overload without optional ref params
        public double LiquidaVacaciones(DateTime FechaLiq, DateTime FechaIngreso, DateTime FecCauVacaciones, double Sueldo, int DiasLiq, double PromedioVacaciones)
        {
            int DiasLiqVac = 0;
            double VlrDiaLiq = 0;
            return LiquidaVacaciones(FechaLiq, FechaIngreso, FecCauVacaciones, Sueldo, DiasLiq, PromedioVacaciones, ref DiasLiqVac, ref VlrDiaLiq);
        }

        public virtual double LiquidaVacaciones(DateTime FecCauVacIni, DateTime FecCauVacFin, double Salbas, ref int DiasLiqVac, ref double VlrDiaLiq)
        {
            int Diasvac = 0;
            double VlrVacaciones = 0;

            Diasvac = this.msgconfig.CalculaDias(FecCauVacFin, FecCauVacIni);

            if (Diasvac < 0)
                Diasvac = 0;

            VlrVacaciones = Math.Round(Diasvac * Salbas, 0);
            DiasLiqVac = Diasvac;
            VlrDiaLiq = Salbas;
            return VlrVacaciones;
        }

        // Overload without optional ref params
        public virtual double LiquidaVacaciones(DateTime FecCauVacIni, DateTime FecCauVacFin, double Salbas)
        {
            int DiasLiqVac = 0;
            double VlrDiaLiq = 0;
            return LiquidaVacaciones(FecCauVacIni, FecCauVacFin, Salbas, ref DiasLiqVac, ref VlrDiaLiq);
        }

        public DataTable GeneraConsultaDinamica(DataTable dsdataTable, DataTable dsdataWhere, OdbcConnection myconnect, string planIni, string planFin)
        {
            StringBuilder stbuilder = new StringBuilder();
            string stmysql;
            DataSet DsDataSet = new DataSet();
            string StJoin = null;

            DataRow row = dsdataTable.Rows[0];

            stbuilder.Append("select ");

            if (Convert.ToBoolean(row["codigo"]))
                stbuilder.Append("idempleado,");

            if (Convert.ToBoolean(row["nombre"]))
                stbuilder.Append("nombres,");

            if (Convert.ToBoolean(row["apellidos"]))
                stbuilder.Append("apellidos,");

            if (Convert.ToBoolean(row["direccion"]))
                stbuilder.Append("Direccion,");

            if (Convert.ToBoolean(row["telefono"]))
                stbuilder.Append("Telefono,");

            if (Convert.ToBoolean(row["celular"]))
                stbuilder.Append("Movil,");

            if (Convert.ToBoolean(row["cedula"]))
                stbuilder.Append("Cedula,");

            if (Convert.ToBoolean(row["expedida"]))
                stbuilder.Append("Expedida,");

            if (Convert.ToBoolean(row["tipocedula"]))
                stbuilder.Append("case empl.Clase when 0 then 'Cedula' when 1 then 'Nit' when 2 then 'Cedula Extranjeria' when 3 then 'Tarjeta de Identidad' else 'no definida' end  as \"Tipo Cedula\",");

            if (Convert.ToBoolean(row["sexo"]))
                stbuilder.Append("case Sexo when 1 then 'Masculino' when 2 then 'Femenino' end Sexo,");

            if (Convert.ToBoolean(row["GrpSag"]))
                stbuilder.Append("case GrupSang when '1' then 'A' when '2' then 'B' when '3' then 'AB' when '4' then 'O' end as GrupSang ,");

            if (Convert.ToBoolean(row["factorRh"]))
                stbuilder.Append("FactorRh,");

            if (Convert.ToBoolean(row["Libreta"]))
                stbuilder.Append("LibMilitar,");

            if (Convert.ToBoolean(row["NoDist"]))
                stbuilder.Append("DisMilitar,");

            if (Convert.ToBoolean(row["Licencia"]))
                stbuilder.Append("LicCon,");

            if (Convert.ToBoolean(row["categoria"]))
                stbuilder.Append("CarLic,");

            if (Convert.ToBoolean(row["fecvenlic"]))
                stbuilder.Append("fecvenlic,");

            if (Convert.ToBoolean(row["ciudad"]))
            {
                stbuilder.Append("empl.IdCiudad as \"Ciudad\",tblciudad.nombre_ciudad as \"Nombre\",");
                StJoin = StJoin + " left join sys_ciudad57 tblciudad on empl.IdCiudad = tblciudad.ciudad";
            }

            if (Convert.ToBoolean(row["nivAcad"]))
                stbuilder.Append("case empl.NivelAca when 1 then 'Primaria' when 2 then 'Secundaria' when 3 then 'Tecnico'  when 4 then 'Tecnologo' when 5 then 'Superior' when 6 then 'Oros' end as \"Nivel Academico\",");

            if (Convert.ToBoolean(row["email"]))
                stbuilder.Append("empl.email,");

            if (Convert.ToBoolean(row["Salario"]))
                stbuilder.Append("empl.Salario,");

            if (Convert.ToBoolean(row["ClaSalario"]))
                stbuilder.Append("case empl.ClaseSalario when 1 then 'Salario Normal' when 2 then 'Salario Integral' when 3 then 'Salario por Horas' when 4 then 'Por Destajo' when 5 then 'Proporcional' when 6 then 'Aprendiz Sena Etapa Lectiva' when 7 then 'Aprendiz Sena Etapa Productiva' end as \"Clase Salario\",");

            if (Convert.ToBoolean(row["Seccion"]))
                stbuilder.Append("empl.IdSeccion,");

            if (Convert.ToBoolean(row["Periodicidad"]))
                stbuilder.Append("case empl.ClaNom when 1 then 'Mensual' when 2 then 'Quincenal' end  as \"Periodicidad\",");

            if (Convert.ToBoolean(row["Forpag"]))
                stbuilder.Append("case empl.FormaPago when 1 then 'Efectivo' when 2 then 'Consignacion' when 3 then 'Cheque' when 4 then 'Traslado Electronico' end as \"Forma Pago\",");

            if (Convert.ToBoolean(row["CenCosto"]))
            {
                stbuilder.Append("empl.idcencos,tblcencos.nombre as \"Descripcion\",");
                StJoin = StJoin + " left join nom_cencos tblcencos on empl.IdCencos = tblcencos.IdCencos";
            }

            if (Convert.ToBoolean(row["Cargo"]))
            {
                stbuilder.Append("empl.Idcargo,tblcargos.nombre as \"Descripcion\",");
                StJoin = StJoin + " left join sys_cargo55 tblcargos on empl.Idcargo = tblcargos.codigo_cargo";
            }

            if (Convert.ToBoolean(row["FechaIngreso"]))
                stbuilder.Append("empl.fecing,");

            if (Convert.ToBoolean(row["ClaCont"]))
            {
                stbuilder.Append("case empl.Contrato when 0 then  'Contrato Indefinido' when 1  then 'Contrato Termino Fijo 1 Anio' when 2 then 'Contrato Termino Fijo 3 Anio' ");
                stbuilder.Append("when 3 then 'Indefinido Especial' when 4 then 'Por Horas' when 5 then 'Contrato Termino Fijo Inferior a un Anio' when 6 then 'Contrato de Aprendizaje' end as \"Tipo Contrato\",");
            }

            if (Convert.ToBoolean(row["FecVenCont"]))
                stbuilder.Append("FecVence,");

            if (Convert.ToBoolean(row["pasaJud"]))
                stbuilder.Append("Telefono,");

            if (Convert.ToBoolean(row["TallaPant"]))
                stbuilder.Append("TallaPan,");

            if (Convert.ToBoolean(row["TallaCamisa"]))
                stbuilder.Append("TallaCa,");

            if (Convert.ToBoolean(row["TallaZapatos"]))
                stbuilder.Append("TallaZap,");

            if (Convert.ToBoolean(row["TallaOverol"]))
                stbuilder.Append("tallaoverol,");

            if (Convert.ToBoolean(row["estado"]))
                stbuilder.Append("case empl.estado when 0 then 'Activo' when 1 then 'Activo' when 2 then 'Retiro' end as \"Estado\",");

            if (Convert.ToBoolean(row["fecret"]))
                stbuilder.Append("empl.FecRetiro,");

            if (Convert.ToBoolean(row["Cauret"]))
            {
                stbuilder.Append("empl.MotRet,tblcauret.nombre as Descripcion,");
                StJoin = StJoin + " left join nom_cauret tblcauret on empl.MotRet = tblcauret.idcodigo";
            }

            if (Convert.ToBoolean(row["empresa"]))
                stbuilder.Append("empl.idnomina,");

            if (Convert.ToBoolean(row["fecnace"]))
                stbuilder.Append("empl.fecnace,");

            // new
            if (Convert.ToBoolean(row["Eps"]))
            {
                stbuilder.Append("empl.idEps,tblEps.nombre as Nomb_Eps,");
                StJoin = StJoin + " left join nom_eps tblEps on empl.idEps = tblEps.idcodigo";
            }

            if (Convert.ToBoolean(row["Cesantias"]))
            {
                stbuilder.Append("empl.idcesantias,tblcesant.nombre as Nom_Cesantias,");
                StJoin = StJoin + " left join nom_cesantias tblcesant on empl.idcesantias = tblcesant.Idcodigo";
            }

            if (Convert.ToBoolean(row["Pensiones"]))
            {
                stbuilder.Append("empl.idpension,tblpension.nombre as Nomb_Pensiones,");
                StJoin = StJoin + " left join nom_pensiones tblpension on empl.idpension = tblpension.idcodigo";
            }

            if (Convert.ToBoolean(row["CajaComp"]))
            {
                stbuilder.Append("empl.idSubsFam,tblcptos.nombre as Nomb_CajaDcompesacion,");
                StJoin = StJoin + " left join nom_cptos tblcptos on empl.idSubsFam = tblcptos.idcpto";
            }

            if (Convert.ToBoolean(row["Riesgos"]))
            {
                stbuilder.Append("empl.idarp,tblArp.nombre as Nomb_Arp,");
                StJoin = StJoin + " left join nom_arp tblArp on empl.idarp = tblArp.idcodigo";
            }

            if (Convert.ToBoolean(row["TarifaARP"]))
            {
                stbuilder.Append("empl.idtarifaArp,tblarptar.nombre as Nomb_TarifaArp,");
                StJoin = StJoin + " left join nom_arptarifa tblarptar on empl.idtarifaArp = tblarptar.idcodigo";
            }

            if (Convert.ToBoolean(row["SalPromedio"]))
            {
                string funtion;
                string FechaI = " ";
                string FechaF = " ";

                switch (varini.pstTipoBD.ToUpper())
                {
                    case "SQL":
                        funtion = "  DBO.";
                        break;
                    case "DB2":
                        funtion = " " + varini.pstBdatos + ".";
                        FechaI = " cast(";
                        FechaF = " as date)";
                        break;
                    default:
                        funtion = "  ";
                        break;
                }

                stbuilder.Append(funtion + "promsalario(empl.Salario, cpto.valor, ");
                stbuilder.Append("(select  (case when sum( liqplan.valbasecesant) is null then 0 else  sum( liqplan.valbasecesant) end )as Acumulado FROM nom_liqplan05_vw liqplan WHERE liqplan.idnomina = empl.idnomina and liqplan.idempleado = empl.idempleado and liqplan.idplanilla between '" + planIni + "' and '" + planFin + "')");
                stbuilder.Append(",cpto.saltope," + FechaI + "planI.Fecinicial" + FechaF + "," + FechaI + "planF.FechaFinal" + FechaF + ") as Salario_Promedio,");

                StJoin = StJoin + " inner join nom_empresas Nomemp ON Nomemp.IdEmpresa = empl.idnomina";
                StJoin = StJoin + " inner join nom_cptos cpto ON cpto.idcpto = Nomemp.IdTrasporte";
                StJoin = StJoin + " left join nom_perpagos planI ON planI.IdPlanilla= '" + planIni + "' AND planI.IdEmpresa = empl.idnomina";
                StJoin = StJoin + " left join nom_perpagos planF ON planF.IdPlanilla= '" + planFin + "' AND planF.IdEmpresa = empl.idnomina";
            }

            stbuilder.Append("from nom_empleados empl " + StJoin);

            if (dsdataWhere.Rows.Count > 0)
            {
                DataRow rowW = dsdataWhere.Rows[0];
                stbuilder.Append(" where ");
                stbuilder.Append(" empl.idnomina " + rowW["empresa"] + " and ");
                stbuilder.Append(" empl.idcencos " + rowW["cencos"] + " and ");
                stbuilder.Append(" empl.idseccion " + rowW["seccion"] + " and ");
                stbuilder.Append(" ClaNom " + rowW["periodicidad"] + " and ");
                stbuilder.Append(" empl.IdCiudad " + rowW["ciudad"] + " and ");
                stbuilder.Append(" empl.estado " + rowW["estado"]);
            }

            stmysql = stbuilder.ToString().Replace(",from", " from");
            this.msgodbc.ExecuteQueryDataset(stmysql, myconnect, "GeneraConsultaDinamica", DsDataSet, "tblconflex");

            return DsDataSet.Tables["tblconflex"];
        }

        // Overload with default optional params
        public DataTable GeneraConsultaDinamica(DataTable dsdataTable, DataTable dsdataWhere, OdbcConnection myconnect)
        {
            return GeneraConsultaDinamica(dsdataTable, dsdataWhere, myconnect, "", "");
        }
    }
}
