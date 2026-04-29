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
        public void OrganizadatosCertificados(int AnioCertificar, string Lugar, string NombrePagador, string CedulaPagador, int Ciudad, DateTime FechaGeneracion,
            System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando datos para certificados, por favor espere ...", myforma);
            int fila = 0;
            double Val34 = 0, val35 = 0, val36 = 0, val37 = 0, val38 = 0, val39 = 0, val40 = 0, val41 = 0, val42 = 0, val43 = 0;
            DateTime Fecini = default(DateTime), Fecfin = default(DateTime);
            DateTime FechaInicial = default(DateTime), FechaFinal = default(DateTime);

            // this.msgconfig.EliminaImpCretificados(AnioCertificar, myconnect); // ERROR: CS1061
            stbuilder.Append("select empl.idnomina, empl.idempleado,liq09.valor,empl.estado,empl.fecretiro,empl.fecing,empl.clase,cptos.lineacer ");
            stbuilder.Append("from nom_empleados empl inner join nom_liqplan09_vw  liq09 ");
            stbuilder.Append("on empl.idnomina = liq09.idnomina and empl.idempleado = liq09.idempleado ");
            stbuilder.Append("inner join nom_empresas nomemp on empl.idnomina = nomemp.idempresa ");
            stbuilder.Append("inner join nom_cptos cptos on liq09.idcpto = cptos.idcpto ");
            stbuilder.Append("where liq09.idyear = '" + AnioCertificar + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "OrganizadatosCertificados", DsDataSet, "TblCert");

            FechaInicial = new DateTime(AnioCertificar, 1, 1);
            FechaFinal = new DateTime(AnioCertificar, 12, 31);

            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["TblCert"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataSet.Tables["TblCert"].Rows.Count)
            {
                Val34 = 0; val35 = 0; val36 = 0; val37 = 0; val38 = 0; val39 = 0; val40 = 0; val41 = 0;
                val42 = 0; val43 = 0;
                Fecini = FechaInicial;
                Fecfin = FechaFinal;

                DataRow row = DsDataSet.Tables["TblCert"].Rows[fila];

                if (Convert.ToDateTime(row["fecing"]) > Fecini)
                {
                    Fecini = Convert.ToDateTime(row["fecing"]);
                }
                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    if (Convert.ToDateTime(row["fecretiro"]) < Fecfin)
                    {
                        Fecfin = Convert.ToDateTime(row["fecretiro"]);
                    }
                }
                if (row["lineacer"].ToString().Trim() == "")
                {
                    row["lineacer"] = "0";
                }
                switch (Convert.ToInt32(row["lineacer"]))
                {
                    case 34:
                        Val34 = Convert.ToDouble(row["valor"]);
                        break;
                    case 35:
                        val35 = Convert.ToDouble(row["valor"]);
                        break;
                    case 36:
                        val36 = Convert.ToDouble(row["valor"]);
                        break;
                    case 37:
                        val37 = Convert.ToDouble(row["valor"]);
                        break;
                    case 38:
                        val38 = Convert.ToDouble(row["valor"]);
                        break;
                    case 39:
                        val39 = Convert.ToDouble(row["valor"]);
                        break;
                    case 40:
                        val40 = Convert.ToDouble(row["valor"]);
                        break;
                    case 41:
                        val41 = Convert.ToDouble(row["valor"]);
                        break;
                    case 42:
                        val42 = Convert.ToDouble(row["valor"]);
                        break;
                    case 43:
                        val43 = Convert.ToDouble(row["valor"]);
                        break;
                }
                val39 = Val34 + val35 + val36 + val37 + val38;
                // this.msgconfig.GrabaImpCretificados(AnioCertificar, row["idnomina"], row["idempleado"], Val34, val35, val36, val37, val38, val39, val40, val41, val42, val43, Fecini, Fecfin, FechaGeneracion, Lugar, NombrePagador, CedulaPagador, 0, 0, Ciudad, myconnect); // ERROR: CS1061
                msgbarra.PerformStep();

                fila += 1;
            }
            msgbarra.Close();
            msgbarra.Dispose();
        }

        public void GrabaPagosManual(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, int natur, double tiempo, int dias, double valor, string Usuario,
            string cencos, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            string ConvDias = "N";
            ok = BuscaPagosManual(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect);
            switch (ok)
            {
                case false:
                    // ok = this.msgconfig.BuscaEmpleado(Idnomina, idempleado, myconnect, DsDataset); // ERROR: CS1503
                    if (ok)
                    {
                        cencos = Convert.ToString(DsDataset.Tables["tblempleados"].Rows[0]["idcencos"]);
                    }

                    // ok = this.msgconfig.BuscaCptos(Idpcto, myconnect, DsDataset); // ERROR: CS1503
                    if (ok)
                    {
                        ConvDias = Convert.ToString(DsDataset.Tables["tblcptos"].Rows[0]["cdias"]);
                        switch (ConvDias)
                        {
                            case "Y":
                                dias = (int)Math.Round(tiempo / 8, 0);
                                break;
                            default:
                                dias = (int)tiempo;
                                break;
                        }
                    }

                    stbuilder.Append("insert into nom_pagman (");
                    stbuilder.Append("idplanilla, Idnomina,idempleado,idcpto,consecutivo,natur,dia,tiempo,Valor,cencos,usuario,fechasys) ");
                    stbuilder.Append("values ('");
                    stbuilder.Append(Idplanilla + "','");
                    stbuilder.Append(Idnomina + "','");
                    stbuilder.Append(idempleado + "','");
                    stbuilder.Append(Idpcto + "','");
                    stbuilder.Append(Consecutivo + "','");
                    stbuilder.Append(natur + "','");
                    stbuilder.Append(dias + "','");
                    stbuilder.Append(tiempo + "','");
                    stbuilder.Append(valor + "','");
                    stbuilder.Append(cencos + "','");
                    stbuilder.Append(Usuario + "','");
                    stbuilder.Append(DateTime.Now.ToString(varini.PstForFec) + "')");
                    break;
                case true:
                    stbuilder.Append("update nom_pagman set ");
                    stbuilder.Append("tiempo = '");
                    stbuilder.Append(tiempo + "',");
                    stbuilder.Append("natur = '");
                    stbuilder.Append(natur + "',");
                    stbuilder.Append("Valor = '");
                    stbuilder.Append(valor + "',");
                    stbuilder.Append("usuario = '");
                    stbuilder.Append(Usuario + "',");
                    stbuilder.Append("fechasys = '");
                    stbuilder.Append(DateTime.Now.ToString(varini.PstForFec) + "' ");
                    stbuilder.Append("where idplanilla = '" + Idplanilla + "' and Idnomina ='" + Idnomina + "' and idempleado ='" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");
                    break;
            }
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaPagosManual");
        }

        public bool BuscaPagosManual(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();
            try
            {
                DsDataset.Tables.Remove("tblpagoman");
            }
            catch (Exception) { }

            stbuilder.Append("select idplanilla,idempleado,idcpto,consecutivo,tiempo,valor,forpago,usuario ");
            stbuilder.Append("from nom_pagman where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "' and idempleado = '" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaPagosManual", DsDatosLiq, "tblpagoman");
            if (DsDatosLiq.Tables["tblpagoman"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDatosLiq.Tables["tblpagoman"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaPagosManual(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect)
        {
            DataSet _ds = new DataSet();
            return BuscaPagosManual(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect, ref _ds);
        }

        private void EliminaPagosManual(int Idplanilla, int Idnomina, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("delete from nom_pagman where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaPagosManual");
        }

        private double BuscaBaseFdoSolidaridad(int PerLiq, int idplanilla, int idnomina, double Idempleado, int CptoSal, int CicloMes, double Salario, int Periodicidad, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string _plaini = "0";
            double BaseAcum = 0;
            decimal TasaLiq = 0;
            StringBuilder stbuil = new StringBuilder();

            switch (Periodicidad)
            {
                case 1:
                    return Salario;
                case 2:
                    switch (CicloMes)
                    {
                        case 1:
                            return 0;
                        default:
                            if (CicloMes > 2)
                            {
                                return 0;
                            }
                            break;
                    }
                    break;
            }

            stbuilder.Append("SELECT MIN(IDPLANILLA) as campo1 FROM NOM_PERPAGOS WHERE IDPERIODO = '" + PerLiq + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscaBaseFdoSolidaridad", ref _plaini);

            int Plaini = 0;
            int.TryParse(_plaini, out Plaini);

            stbuil.Append("select sum(nomliq.valor) as campo1 ");
            stbuil.Append("from nom_liqplan nomliq inner join nom_cptos cptos on nomliq.idcpto = cptos.idcpto ");
            stbuil.Append("where idplanilla between '" + Plaini + "' and '" + idplanilla + "' ");
            stbuil.Append("and idnomina = '" + idnomina + "' and idempleado = '" + Idempleado + "' and cptos.natur = 1 and cptos.basealq = 1 and cptos.clase <>4 ");

            string _baseAcum = "0";
            this.msgodbc.ExecuteQueryconec(stbuil.ToString(), myconnect, "BuscaAcumuladosCicloConcepto", ref _baseAcum);
            double.TryParse(_baseAcum, out BaseAcum);

            return BaseAcum;
        }

        private DataTable CargarDatosAusentismo(double IdNomina, double IdEmpleado, double IdCpto, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdataset = new DataSet();

            stbuilder.Append("select Consecutivo,FecInicial,FecFinal, case prorroga when 1 then 'SI' else 'NO' end as Prorroga ");
            stbuilder.Append("from nom_ausentismos ");
            stbuilder.Append("where IdNomina=" + IdNomina + " and IdEmpleado=" + IdEmpleado + " and Idcpto=" + IdCpto);
            stbuilder.Append(" order by Consecutivo");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargarDatosAusentismo", dsdataset, "tblausentismo");
            return dsdataset.Tables["tblausentismo"];
        }

        public void CargarVentanaAusentismoxEmpleado(double Idnomina, double IdEmpleado, double IdCpto, System.Windows.Forms.Form Forma, OdbcConnection myconnect, ref double ConsecAusentismo, bool Modal)
        {
            // nom_frmayuausen frmausentismo = new nom_frmayuausen(); // ERROR: CS0246
            DataTable dttable = new DataTable();

            dttable = this.CargarDatosAusentismo(Idnomina, IdEmpleado, IdCpto, myconnect);

            // frmausentismo.DgwAusentismo.DataSource = dttable; // ERROR: CS0103
            // frmausentismo.DgwAusentismo.AutoGenerateColumns = false; // ERROR: CS0103
            // frmausentismo.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent; // ERROR: CS0103
            switch (Modal)
            {
                case false:
                    // frmausentismo.Show(Forma); // ERROR: CS0103
                    break;
                case true:
                    // frmausentismo.ShowDialog(Forma); // ERROR: CS0103
                    // ConsecAusentismo = frmausentismo.NumConsec; // ERROR: CS0103
                    break;
            }
        }

        public void CargarVentanaAusentismoxEmpleado(double Idnomina, double IdEmpleado, double IdCpto, System.Windows.Forms.Form Forma, OdbcConnection myconnect)
        {
            double ConsecAusentismo = 0;
            CargarVentanaAusentismoxEmpleado(Idnomina, IdEmpleado, IdCpto, Forma, myconnect, ref ConsecAusentismo, false);
        }

        private double CalculaDiasPlanillaAnt(int IdPeriodo, int idempresa, double IdEmpleado, DateTime FecIng, DateTime Fecret,
            int Estado, string Usuario, OdbcConnection myconnect, ref int IdPlanillaIni)
        {
            string Stmysql;
            string plaini = "999999", plafin = "999999";
            int HorasAus = 0, HorasAusIncap = 0;
            double DiasCiclo;
            DataSet DsDataset = new DataSet();
            DateTime FecIni = default(DateTime), fecfin = default(DateTime);
            string NoLiqAusent;
            int HorasCiclo = 0;

            Stmysql = "select min(idplanilla) as campo1, max(idplanilla) as campo2 from nom_perpagos where idperiodo = '" + IdPeriodo + "' and idempresa = '" + idempresa + "'";
            ok = this.msgodbc.ExecuteQueryconec(Stmysql, myconnect, "CalculaDiasPlanillaAnt", ref plaini, ref plafin);

            if (!ok)
            {
                return 0;
            }

            int plainiInt = 0;
            int.TryParse(plaini, out plainiInt);

            // ok = msgconfig.BuscaPeriodosPagos(plainiInt, idempresa, myconnect, DsDataset); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Planilla " + plaini + " no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            IdPlanillaIni = plainiInt;
            DataRow rowPer = DsDataset.Tables["tblperpagos"].Rows[0];
            FecIni = Convert.ToDateTime(rowPer["Fecinicial"]);
            fecfin = Convert.ToDateTime(rowPer["FechaFinal"]);
            NoLiqAusent = Convert.ToString(rowPer["noliqausen"]);

            if (FecIng > FecIni)
            {
                FecIni = FecIng;
            }

            DiasCiclo = DateAndTime.DateDiff(DateInterval.Day, FecIni, fecfin) + 1;
            if (fecfin.Day == 31)
            {
                DiasCiclo -= 1;
            }

            if (fecfin.Month == 2 && DiasCiclo < 15)
            {
                switch (fecfin.Day)
                {
                    case 29:
                        DiasCiclo += 1;
                        fecfin = fecfin.AddDays(1);
                        break;
                    default:
                        if (fecfin.Day <= 28)
                        {
                            DiasCiclo += 2;
                            fecfin = fecfin.AddDays(2);
                        }
                        break;
                }
            }

            // Commented out block in VB source (NoLiqAusent logic)

            HorasCiclo = (int)((DiasCiclo * 8) - (HorasAus + HorasAusIncap));

            return HorasCiclo;
        }

        private double CalculaDiasPlanillaAnt(int IdPeriodo, int idempresa, double IdEmpleado, DateTime FecIng, DateTime Fecret,
            int Estado, string Usuario, OdbcConnection myconnect)
        {
            int IdPlanillaIni = 999999;
            return CalculaDiasPlanillaAnt(IdPeriodo, idempresa, IdEmpleado, FecIng, Fecret, Estado, Usuario, myconnect, ref IdPlanillaIni);
        }

        private int RevisaAusentismoPlanillaAnt(int Idnomina, int idplanilla, double idempleado, DateTime fecini, DateTime FecFin, string Usuario, OdbcConnection myconnect)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            DateTime fecAusini = default(DateTime), FecAusFin = default(DateTime);
            int sw1 = 0;
            DateTime FecIniLiq = default(DateTime), FecFinLiq = default(DateTime);
            int Horas = 0;

            // BuscaAusentismoTrabajador(Idnomina, idempleado, myconnect, DsDataSet); // ERROR: CS1620
            while (Fila < DsDataSet.Tables["tblnovausent"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblnovausent"].Rows[Fila];
                sw1 = 0;
                fecAusini = Convert.ToDateTime(row["FecInicial"]);
                FecAusFin = Convert.ToDateTime(row["FecFinal"]);
                if (fecAusini < fecini && FecAusFin < fecini)
                {
                    sw1 = 1;
                }

                if (fecAusini > FecFin && FecAusFin > FecFin)
                {
                    sw1 = 1;
                }

                if (Convert.ToString(row["clase"]) == "5")
                {
                    sw1 = 1;
                }

                if (sw1 == 0)
                {
                    if (fecAusini < fecini)
                    {
                        FecIniLiq = fecini;
                    }
                    else
                    {
                        FecIniLiq = fecAusini;
                    }

                    if (FecAusFin > FecFin)
                    {
                        FecFinLiq = FecFin;
                    }
                    else
                    {
                        FecFinLiq = FecAusFin;
                    }

                    Horas += (int)((DateAndTime.DateDiff(DateInterval.Day, FecIniLiq, FecFinLiq) + 1) * 8);
                }

                Fila += 1;
            }
            return Horas;
        }

        private int RevisaAusentIncapacidadPlanillaAnt(int idplanilla, int Idnomina, double idempleado, DateTime Fecing, DateTime fecini, DateTime FecFin, string usuario, OdbcConnection myconnect, ref double SalBaseLiq)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            DateTime fecAusini = default(DateTime), FecAusFin = default(DateTime);
            int sw1 = 0;
            DateTime FecIniLiq = default(DateTime), FecFinLiq = default(DateTime);
            int Horas = 0;
            double BaseLiqIncap = 0, VlrLiq = 0;

            // BuscaAusentismoTrabajador(Idnomina, idempleado, myconnect, DsDataSet); // ERROR: CS1620
            while (Fila < DsDataSet.Tables["tblnovausent"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblnovausent"].Rows[Fila];
                sw1 = 0;
                fecAusini = Convert.ToDateTime(row["FecInicial"]);
                FecAusFin = Convert.ToDateTime(row["FecFinal"]);
                if (fecAusini < fecini && FecAusFin < fecini)
                {
                    sw1 = 1;
                }

                if (fecAusini > FecFin && FecAusFin > FecFin)
                {
                    sw1 = 1;
                }

                if (Convert.ToString(row["clase"]) != "5")
                {
                    sw1 = 1;
                }

                if (sw1 == 0)
                {
                    if (fecAusini < fecini)
                    {
                        FecIniLiq = fecini;
                    }
                    else
                    {
                        FecIniLiq = fecAusini;
                    }

                    if (FecAusFin > FecFin)
                    {
                        FecFinLiq = FecFin;
                    }
                    else
                    {
                        FecFinLiq = FecAusFin;
                    }

                    Horas += (int)((DateAndTime.DateDiff(DateInterval.Day, FecIniLiq, FecFinLiq) + 1) * 8);
                    if (Fecing > fecini)
                    {
                        Horas += (int)((DateAndTime.DateDiff(DateInterval.Day, fecini, Fecing) + 1) * 8);
                    }

                    BaseLiqIncap = Convert.ToDouble(row["base"]);

                    VlrLiq = Math.Round((BaseLiqIncap / 240) * Horas, 0);

                    if (Convert.ToInt32(row["basealq"]) == 1)
                    {
                        SalBaseLiq += VlrLiq;
                    }
                }

                Fila += 1;
            }
            return Horas;
        }

        private int RevisaAusentIncapacidadPlanillaAnt(int idplanilla, int Idnomina, double idempleado, DateTime Fecing, DateTime fecini, DateTime FecFin, string usuario, OdbcConnection myconnect)
        {
            double SalBaseLiq = 0;
            return RevisaAusentIncapacidadPlanillaAnt(idplanilla, Idnomina, idempleado, Fecing, fecini, FecFin, usuario, myconnect, ref SalBaseLiq);
        }

        private double BuscaAcumuladosPlanillaAnt(int IdPlanilla, int IdEmpresa, double IdEmpleado, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsData = new DataSet();

            StBuilder.Append("select idnomina,idempleado,sum(liq08.valor) as valor  ");
            StBuilder.Append("from nom_liqplan08_vw liq08 ");
            StBuilder.Append("inner join nom_cptos cptos on liq08.idcpto=cptos.idcpto ");
            StBuilder.Append("where cptos.basealq ='1' and liq08.idplanilla=" + IdPlanilla + " and liq08.idnomina=" + IdEmpresa + " and liq08.idempleado=" + IdEmpleado + " ");
            StBuilder.Append("group by idnomina,idempleado");

            ok = this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaAcumuladosPlanillaAnt", DsData, "tblacum");
            if (ok)
            {
                return Convert.ToDouble(DsData.Tables["tblacum"].Rows[0]["valor"]);
            }
            else
            {
                return 0;
            }
        }

        public bool BuscaLiquidacionVacaciones(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();
            try
            {
                DsDataset.Tables.Remove("tblliqvac");
            }
            catch (Exception) { }

            stbuilder.Append("select a.idplanilla,a.idempleado,a.idcpto,a.consecutivo,a.tiempo,a.natur,a.valor,a.usuario,b.nombre, ");
            stbuilder.Append("a.fechaliq,a.fechacauini,a.fechacaufin,a.fechadisini,a.fechadisfin,a.fechareint,a.contabilizo ");
            stbuilder.Append("from nom_liqvac a ");
            stbuilder.Append("inner join nom_cptos b on a.idcpto=b.idcpto ");
            stbuilder.Append("where a.idplanilla = '" + Idplanilla + "' and a.Idnomina = '" + Idnomina + "' and a.idempleado = '" + idempleado + "' and a.idcpto = '" + Idpcto + "' and a.consecutivo = '" + Consecutivo + "' ");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaLiquidacionVacaciones", DsDatosLiq, "tblliqvac");
            if (DsDatosLiq.Tables["tblliqvac"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDatosLiq.Tables["tblliqvac"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaLiquidacionVacaciones(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect)
        {
            DataSet _ds = new DataSet();
            return BuscaLiquidacionVacaciones(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect, ref _ds);
        }

        public virtual bool BuscaLiquidacionVacaciones(int Idplanilla, int Idnomina, double idempleado, OdbcConnection myconnect, ref DataSet DsDataset, int IdCptoVaca, DateTime fecfinPLan)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();
            DateTime fecdisfin = default(DateTime);
            try
            {
                DsDataset.Tables.Remove("tblliqvac");
            }
            catch (Exception) { }

            stbuilder.Append("select a.idplanilla,a.idempleado,a.idcpto,a.consecutivo,a.tiempo,a.natur,a.valor,a.usuario,b.nombre, ");
            stbuilder.Append("a.fechaliq,a.fechacauini,a.fechacaufin,a.fechadisini,a.fechadisfin,a.fechareint,a.contabilizo ");
            stbuilder.Append("from nom_liqvac a ");
            stbuilder.Append("inner join nom_cptos b on a.idcpto=b.idcpto ");
            stbuilder.Append("where a.idplanilla = '" + Idplanilla + "' and a.Idnomina = '" + Idnomina + "' and a.idempleado = '" + idempleado + "' ");
            if (Convert.ToDouble(IdCptoVaca) > 0)
            {
                stbuilder.Append(" and a.idcpto=" + IdCptoVaca);
            }

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaLiquidacionVacaciones", DsDatosLiq, "tblliqvac");
            if (DsDatosLiq.Tables["tblliqvac"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDatosLiq.Tables["tblliqvac"].Copy());
                }
                catch (Exception) { }

                if (Convert.ToDouble(IdCptoVaca) > 0)
                {
                    if (fecfinPLan != new DateTime(1950, 1, 1))
                    {
                        fecdisfin = Convert.ToDateTime(DsDatosLiq.Tables["tblliqvac"].Rows[0]["fechadisfin"]);
                        if (Convert.ToDateTime(fecdisfin.ToString("dd-MM-yyyy")) < Convert.ToDateTime(fecfinPLan.ToString("dd-MM-yyyy")))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual bool BuscaLiquidacionVacaciones(int Idplanilla, int Idnomina, double idempleado, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            return BuscaLiquidacionVacaciones(Idplanilla, Idnomina, idempleado, myconnect, ref DsDataset, 0, new DateTime(1950, 1, 1));
        }

        public virtual bool BuscaLiquidacionVacaciones(int Idplanilla, int Idnomina, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();
            try
            {
                DsDataset.Tables.Remove("tblliqvac");
            }
            catch (Exception) { }

            stbuilder.Append("select a.idplanilla,a.idempleado,a.idcpto,a.consecutivo,a.tiempo,a.natur,a.valor,a.usuario,a.dia,a.Cencos,b.nombre, ");
            stbuilder.Append("a.fechaliq,a.fechacauini,a.fechacaufin,a.fechadisini,a.fechadisfin,a.fechareint,a.contabilizo ");
            stbuilder.Append("from nom_liqvac a ");
            stbuilder.Append("inner join nom_cptos b on a.idcpto=b.idcpto ");
            stbuilder.Append("where a.idplanilla = '" + Idplanilla + "' and a.Idnomina = '" + Idnomina + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaLiquidacionVacaciones", DsDatosLiq, "tblliqvac");
            if (DsDatosLiq.Tables["tblliqvac"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDatosLiq.Tables["tblliqvac"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public void AplicaVacacionesLiqPlanilla(int IdPlanilla, int idnomina, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            double fila = 0;

            ok = this.BuscaLiquidacionVacaciones(IdPlanilla, idnomina, myconnect, ref dsdata);
            if (ok)
            {
                for (fila = 0; fila <= dsdata.Tables["tblliqvac"].Rows.Count - 1; fila++)
                {
                    DataRow row = dsdata.Tables["tblliqvac"].Rows[(int)fila];
                    if (Convert.ToString(row["contabilizo"]) == "Y")
                    {
                        // this.GrabaLiquidacion(IdPlanilla, idnomina, row["idempleado"], row["idcpto"], row["consecutivo"], row["natur"], row["tiempo"], row["dia"], row["valor"], row["usuario"], row["Cencos"], myconnect, "V"); // ERROR: CS1503
                    }
                }
            }
        }

        public void GrabaLiquidacionVacaciones(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, int natur, double tiempo,
            int dias, double valor, string Usuario, string cencos, OdbcConnection myconnect, string tiporeg = "A", DateTime FecLiq = default(DateTime),
            DateTime FecCauIni = default(DateTime), DateTime FecCauFin = default(DateTime), DateTime FecDisIni = default(DateTime), DateTime FecDisFin = default(DateTime), DateTime FecReint = default(DateTime), string contabilizo = "N")
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            string ConvDias = "N";

            // Fix default dates to 1/1/1950 as in VB
            if (FecLiq == default(DateTime)) FecLiq = new DateTime(1950, 1, 1);
            if (FecCauIni == default(DateTime)) FecCauIni = new DateTime(1950, 1, 1);
            if (FecCauFin == default(DateTime)) FecCauFin = new DateTime(1950, 1, 1);
            if (FecDisIni == default(DateTime)) FecDisIni = new DateTime(1950, 1, 1);
            if (FecDisFin == default(DateTime)) FecDisFin = new DateTime(1950, 1, 1);
            if (FecReint == default(DateTime)) FecReint = new DateTime(1950, 1, 1);

            ok = BuscaLiquidacionVacaciones(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect);
            switch (ok)
            {
                case false:
                    // ok = this.msgconfig.BuscaEmpleado(Idnomina, idempleado, myconnect, DsDataset); // ERROR: CS1503
                    if (ok)
                    {
                        cencos = Convert.ToString(DsDataset.Tables["tblempleados"].Rows[0]["idcencos"]);
                    }

                    // ok = this.msgconfig.BuscaCptos(Idpcto, myconnect, DsDataset); // ERROR: CS1503
                    if (ok)
                    {
                        ConvDias = Convert.ToString(DsDataset.Tables["tblcptos"].Rows[0]["cdias"]);
                        switch (ConvDias)
                        {
                            case "Y":
                                dias = (int)Math.Round(tiempo / 8, 0);
                                break;
                            default:
                                dias = (int)tiempo;
                                break;
                        }
                    }

                    stbuilder.Append("insert into nom_liqvac (");
                    stbuilder.Append("idplanilla, Idnomina,idempleado,idcpto,consecutivo,natur,dia,tiempo,Valor,cencos,usuario,fechasys,tiporeg, ");
                    stbuilder.Append("fechaliq,fechacauini,fechacaufin,fechadisini,fechadisfin,fechareint,contabilizo) ");
                    stbuilder.Append("values ('");
                    stbuilder.Append(Idplanilla + "','");
                    stbuilder.Append(Idnomina + "','");
                    stbuilder.Append(idempleado + "','");
                    stbuilder.Append(Idpcto + "','");
                    stbuilder.Append(Consecutivo + "','");
                    stbuilder.Append(natur + "','");
                    stbuilder.Append(dias + "','");
                    stbuilder.Append(tiempo + "','");
                    stbuilder.Append(Math.Round(valor, 0) + "','");
                    stbuilder.Append(cencos + "','");
                    stbuilder.Append(Usuario + "','");
                    stbuilder.Append(DateTime.Now.ToString(varini.pstForfecyHora) + "','");
                    stbuilder.Append(tiporeg + "','");
                    stbuilder.Append(FecLiq.ToString(varini.PstForFec) + "','");
                    stbuilder.Append(FecCauIni.ToString(varini.PstForFec) + "','");
                    stbuilder.Append(FecCauFin.ToString(varini.PstForFec) + "','");
                    stbuilder.Append(FecDisIni.ToString(varini.PstForFec) + "','");
                    stbuilder.Append(FecDisFin.ToString(varini.PstForFec) + "','");
                    stbuilder.Append(FecReint.ToString(varini.PstForFec) + "','");
                    stbuilder.Append(contabilizo + "')");
                    break;
                case true:
                    stbuilder.Append("update nom_liqvac set ");
                    stbuilder.Append("tiempo = '");
                    stbuilder.Append(tiempo + "',");
                    stbuilder.Append("natur = '");
                    stbuilder.Append(natur + "',");
                    stbuilder.Append("Valor = '");
                    stbuilder.Append(valor + "',");
                    stbuilder.Append("usuario = '");
                    stbuilder.Append(Usuario + "',");
                    stbuilder.Append("tiporeg = '");
                    stbuilder.Append(tiporeg + "',");
                    stbuilder.Append("fechasys = '");
                    stbuilder.Append(DateTime.Now.ToString(varini.pstForfecyHora) + "', ");
                    stbuilder.Append("fechaliq = '");
                    stbuilder.Append(FecLiq.ToString(varini.PstForFec) + "', ");
                    stbuilder.Append("fechacauini = '");
                    stbuilder.Append(FecCauIni.ToString(varini.PstForFec) + "', ");
                    stbuilder.Append("fechacaufin = '");
                    stbuilder.Append(FecCauFin.ToString(varini.PstForFec) + "', ");
                    stbuilder.Append("fechadisini = '");
                    stbuilder.Append(FecDisIni.ToString(varini.PstForFec) + "', ");
                    stbuilder.Append("fechadisfin = '");
                    stbuilder.Append(FecDisFin.ToString(varini.PstForFec) + "', ");
                    stbuilder.Append("fechareint = '");
                    stbuilder.Append(FecReint.ToString(varini.PstForFec) + "', ");
                    stbuilder.Append("contabilizo='");
                    stbuilder.Append(contabilizo + "' ");
                    stbuilder.Append("where idplanilla = '" + Idplanilla + "' and Idnomina ='" + Idnomina + "' and idempleado ='" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");
                    break;
            }
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaLiquidacionVacaciones");
        }

        public void EliminaLiquidacionVacaciones(int Idplanilla, int Idnomina, double idempleado, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("delete from nom_liqvac where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "' and idempleado='" + idempleado + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaLiquidacion");
        }

        public bool LiquidacionPlanillaVacacionesCptoAutom(int idPlanilla, int idempresa, double IdEmpleado, double VlrVacaciones, double DiasVacaciones, string usuario, System.Windows.Forms.Form Myforma,
            DateTime FecDisfruteIni, DateTime FecDisfruteFin, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            double Salario = 0;
            int Periodicidad = 0, Tiempo = 0;
            double VlrLiq = 0;
            double reg = 0;
            int ClauxTra = 0, CicloPtra = 0, SW1 = 0;
            double SalBaseLiq = 0;
            string AfiFondo = "N";
            double TotalDevengado = 0, SubTrasporte = 0, SalarioMinimo = 0;
            DateTime fecini = default(DateTime), fecfin = default(DateTime);
            int HorasAus = 0, HorasAusIncap = 0;
            int DiasCiclo = 0;
            double Horaliq = 0;
            int ClaSalario = 0, IdSalBasico = 0, IdAusCap = 0;
            DateTime Fecing = default(DateTime), Fecret = default(DateTime);
            double BaseLiqIncap = 0;
            int Estado = 0;
            DateTime FecReing = default(DateTime);
            int Contrato = 0;
            string idcencos;
            double SalMinimo = 0;
            string LiqSoloNov = "N", LiqSalAut = "N", NoLiqAusent = "N", NoliqLib = "N";
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidando planilla vacaciones", Myforma);
            double BaseFdo = 0;
            int CicloMes = 0, PerLiq = 0;
            int HorasTmpPlaAnt = 0, HorasPlanillaAnt = 0, cicloPagSegSoc = 0;
            double SalBaseLiqTmp = 0;
            int PlaIni = 999999;
            string LiqSoloAnticipos = "N", HaceCruceAnticipos = "N";
            int TipoEmpleado = 0;
            string msg = "";
            double SalarioMes = 0, SalBasicoMes = 0;
            bool NoPagaSalario = false;

            // ok = msgconfig.BuscaPeriodosPagos(idPlanilla, idempresa, myconnect, DsDataset); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Planilla no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            DataRow rowPer = DsDataset.Tables["tblperpagos"].Rows[0];
            fecini = Convert.ToDateTime(rowPer["Fecinicial"]);
            fecfin = Convert.ToDateTime(rowPer["FechaFinal"]);
            LiqSoloNov = Convert.ToString(rowPer["SoloNov"]);
            LiqSalAut = Convert.ToString(rowPer["noliqsalaut"]);
            NoLiqAusent = Convert.ToString(rowPer["noliqausen"]);
            NoliqLib = Convert.ToString(rowPer["noliqLib"]);
            PerLiq = Convert.ToInt32(rowPer["idperiodo"]);
            CicloMes = Convert.ToInt32(rowPer["CicloMes"]);
            LiqSoloAnticipos = Convert.ToString(rowPer["LiqAnt"]);
            HaceCruceAnticipos = Convert.ToString(rowPer["CruceAnt"]);

            if (fecfin > FecDisfruteFin)
            {
                NoPagaSalario = true;
            }

            DiasCiclo = (int)(DateAndTime.DateDiff(DateInterval.Day, fecini, fecfin) + 1);
            if (fecfin.Day == 31)
            {
                DiasCiclo -= 1;
            }

            if (fecfin.Month == 2 && DiasCiclo < 15)
            {
                switch (fecfin.Day)
                {
                    case 29:
                        DiasCiclo += 1;
                        fecfin = fecfin.AddDays(1);
                        break;
                    default:
                        if (fecfin.Day <= 28)
                        {
                            DiasCiclo += 2;
                            fecfin = fecfin.AddDays(2);
                        }
                        break;
                }
            }

            ok = msgconfig.BuscaEmpresa(idempresa, myconnect, DsDataset);
            if (!ok)
            {
                MessageBox.Show("Empresa no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            SalMinimo = Convert.ToDouble(DsDataset.Tables["tblempresas"].Rows[0]["ValSalMinimo"]);

            StBuilder.Append("select idnomina,idempleado,salario,claseSalario,ideps,idpension,idarp,idsena,idicbf,clanom,ClauxTra,CicloPtra,AfiFondo,estado,");
            StBuilder.Append("Fecing,FecRetiro,FecReingreso,Contrato,idcencos,CicloApo,TipEmpleado ");
            StBuilder.Append(" from nom_empleados ");
            StBuilder.Append(" where  idnomina = '" + idempresa + "' and IdEmpleado='" + IdEmpleado + "' and ClaNom = '" + DsDataset.Tables["tblperpagos"].Rows[0]["Periodicidad"] + "' ");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "LiquidacionPlanilla", DsDataset, "tblLiqEmpl");
            MsgBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblLiqEmpl"].Rows.Count);
            MsgBarra.Show();

            Salario = VlrVacaciones;
            SalBaseLiq = VlrVacaciones;
            Tiempo = (int)(DiasVacaciones * 8);
            Horaliq = Tiempo;

            while (reg < DsDataset.Tables["tblLiqEmpl"].Rows.Count)
            {
                Fecing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["fecing"]);
                Fecret = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecRetiro"]);
                FecReing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecReingreso"]);
                if (Fecing > fecfin)
                {
                    goto Siguiente;
                }

                if (FecReing != new DateTime(1950, 1, 1))
                {
                    if (FecReing > fecfin)
                    {
                        goto Siguiente;
                    }
                    else
                    {
                        Fecing = FecReing;
                    }
                }

                DataRow rowEmpl = DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg];
                HorasAus = 0; TotalDevengado = 0; PlaIni = 999999;
                SalBasicoMes = 0;
                SalarioMes = Convert.ToDouble(rowEmpl["salario"]);
                Periodicidad = Convert.ToInt32(rowEmpl["clanom"]);
                IdEmpleado = Convert.ToDouble(rowEmpl["idempleado"]);
                ClauxTra = Convert.ToInt32(rowEmpl["ClauxTra"]);
                CicloPtra = Convert.ToInt32(rowEmpl["CicloPtra"]);
                AfiFondo = Convert.ToString(rowEmpl["AfiFondo"]);
                ClaSalario = Convert.ToInt32(rowEmpl["clasesalario"]);
                Estado = Convert.ToInt32(rowEmpl["estado"]);
                idcencos = Convert.ToString(rowEmpl["idcencos"]);
                cicloPagSegSoc = Convert.ToInt32(rowEmpl["CicloApo"]);
                TipoEmpleado = Convert.ToInt32(rowEmpl["TipEmpleado"]);

                DataRow rowEmp = DsDataset.Tables["tblempresas"].Rows[0];

                if (LiqSoloAnticipos == "N" && HaceCruceAnticipos == "N")
                {
                    switch (ClaSalario)
                    {
                        case 1:
                            switch (TipoEmpleado)
                            {
                                case 1:
                                case 4:
                                    IdSalBasico = Convert.ToInt32(rowEmp["IdSalBasico"]);
                                    msg = "(Salario Basico)";
                                    break;
                                case 2:
                                    IdSalBasico = Convert.ToInt32(rowEmp["IdVacaConsol"]);
                                    msg = "(Salario Pension)";
                                    break;
                                case 3:
                                    IdSalBasico = Convert.ToInt32(rowEmp["IdAusVaca"]);
                                    msg = "(Salario Tramites)";
                                    break;
                            }

                            if (IdSalBasico != 0)
                            {
                                SW1 = 0;
                                // ok = this.msgconfig.BuscaCptos(IdSalBasico, myconnect, DsDataset); // ERROR: CS1503
                                if (!ok)
                                {
                                    MessageBox.Show("Concepto de liquidacion " + msg + " no esta creado. " + IdSalBasico, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    SW1 = 1;
                                }
                            }
                            else
                            {
                                SW1 = 1;
                            }
                            break;
                        case 2:
                            switch (TipoEmpleado)
                            {
                                case 1:
                                case 4:
                                    IdSalBasico = Convert.ToInt32(rowEmp["IdSalIntegral"]);
                                    msg = "(Salario Integral)";
                                    break;
                                case 2:
                                    IdSalBasico = Convert.ToInt32(rowEmp["IdVacaConsol"]);
                                    msg = "(Salario Pension)";
                                    break;
                                case 3:
                                    IdSalBasico = Convert.ToInt32(rowEmp["IdAusVaca"]);
                                    msg = "(Salario Tramites)";
                                    break;
                            }

                            if (IdSalBasico != 0)
                            {
                                SW1 = 0;
                                // ok = this.msgconfig.BuscaCptos(IdSalBasico, myconnect, DsDataset); // ERROR: CS1503
                                if (!ok)
                                {
                                    MessageBox.Show("Concepto de liquidacion " + msg + " no esta creado. " + IdSalBasico, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    SW1 = 1;
                                }
                            }
                            else
                            {
                                SW1 = 1;
                            }
                            break;
                        case 6:
                        case 7:
                            IdSalBasico = Convert.ToInt32(rowEmp["IdAprSena"]);
                            if (IdSalBasico != 0)
                            {
                                SW1 = 0;
                                // ok = this.msgconfig.BuscaCptos(IdSalBasico, myconnect, DsDataset); // ERROR: CS1503
                                if (!ok)
                                {
                                    MessageBox.Show("Concepto de liquidacion (Aprendiz Sena) no esta creado. " + IdSalBasico, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    SW1 = 1;
                                }
                            }
                            else
                            {
                                SW1 = 1;
                            }
                            break;
                    }

                    if (SW1 == 0)
                    {
                        if (NoLiqAusent == "N")
                        {
                            FecDisfruteIni = Convert.ToDateTime(FecDisfruteIni.ToString("dd-MM-yyyy"));
                            if (Estado == 2)
                            {
                                if (Fecret < fecini)
                                {
                                    goto Siguiente;
                                }
                                else
                                {
                                    if (Fecret >= fecini && Fecret <= FecDisfruteIni)
                                    {
                                        HorasAus = (int)((DateAndTime.DateDiff(DateInterval.Day, FecDisfruteIni, Fecret) * -1) * 8);
                                    }
                                }
                            }

                            // HorasAus += RevisaAusentismo(idempresa, idPlanilla, IdEmpleado, fecini, FecDisfruteIni.AddDays(-1), usuario, myconnect, SalBasicoMes, "", null, "LV"); // ERROR: CS1503, CS1620
                            if (Fecing > fecini)
                            {
                                HorasAus += (int)(DateAndTime.DateDiff(DateInterval.Day, FecDisfruteIni, Fecing) * 8);
                            }

                            if (FecDisfruteFin > fecfin)
                            {
                                FecDisfruteFin = fecfin;
                            }

                            switch (FecDisfruteFin.Day)
                            {
                                case 31:
                                    FecDisfruteFin = FecDisfruteFin.AddDays(-1);
                                    break;
                                default:
                                    if (FecDisfruteFin.Month == 2)
                                    {
                                        if (FecDisfruteFin.Day >= 29)
                                        {
                                            FecDisfruteFin = FecDisfruteFin.AddDays(1);
                                        }
                                        else if (FecDisfruteFin.Day == 28)
                                        {
                                            FecDisfruteFin = FecDisfruteFin.AddDays(2);
                                        }
                                    }
                                    break;
                            }

                            HorasAus += (int)((DateAndTime.DateDiff(DateInterval.Day, FecDisfruteIni, FecDisfruteFin) + 1) * 8);
                            // HorasAusIncap = this.RevisaAusentIncapacidad(idPlanilla, idempresa, IdEmpleado, Fecing, fecini, FecDisfruteIni.AddDays(-1), usuario, myconnect, SalBasicoMes, "", null, "LV"); // ERROR: CS1503, CS1620
                            VlrLiq = 0;
                        }

                        if (LiqSalAut == "N")
                        {
                            if (!NoPagaSalario)
                            {
                                // VlrLiq = LiquidaBasico(IdSalBasico, DiasCiclo, HorasAus + HorasAusIncap, SalarioMes, Horaliq); // ERROR: CS1620
                                this.GrabaLiquidacionVacaciones(idPlanilla, idempresa, IdEmpleado, IdSalBasico, idPlanilla, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);

                                TotalDevengado += VlrLiq;
                                if (Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["basealq"]) == 1)
                                {
                                    SalBasicoMes += VlrLiq;
                                    SalarioMes = VlrLiq;
                                }
                            }
                        }
                    }

                    int IdTrasporte = Convert.ToInt32(rowEmp["IdTrasporte"]);
                    if (IdTrasporte != 0)
                    {
                        SW1 = 0; Tiempo = 0;
                        // ok = this.msgconfig.BuscaCptos(IdTrasporte, myconnect, DsDataset); // ERROR: CS1503
                        if (!ok)
                        {
                            MessageBox.Show("Concepto de liquidacion (Aux. Transporte) no esta creado. " + IdTrasporte, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }

                        if (ClauxTra == 2)
                        {
                            SW1 = 1;
                        }

                        if (SW1 == 0 && LiqSoloNov == "N" && TipoEmpleado == 1)
                        {
                            // VlrLiq = LiquidaAuxTrasporte(IdTrasporte, SalarioMes, ClaSalario, Horaliq, myconnect); // ERROR: CS1503

                            if (ClauxTra == 2)
                            {
                                VlrLiq = 0;
                            }

                            if (VlrLiq > 0)
                            {
                                this.GrabaLiquidacionVacaciones(idPlanilla, idempresa, IdEmpleado, IdTrasporte, idPlanilla, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                                TotalDevengado += VlrLiq;
                            }
                            if (Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["basealq"]) == 1)
                            {
                                SalBasicoMes += VlrLiq;
                            }
                        }
                    }

                    Salario += SalarioMes;
                    SalBaseLiq += SalBasicoMes;
                }

                if (AfiFondo == "Y")
                {
                    int IdApoSoc = Convert.ToInt32(rowEmp["IdApoSoc"]);
                    if (IdApoSoc != 0)
                    {
                        SW1 = 0; Tiempo = 0;
                        // ok = this.msgconfig.BuscaCptos(IdApoSoc, myconnect, DsDataset); // ERROR: CS1503
                        if (!ok)
                        {
                            MessageBox.Show("Concepto de liquidacion (Aportes Sociales) no esta creado." + IdApoSoc, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }

                        if (SW1 == 0 && LiqSoloNov == "N")
                        {
                            VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdApoSoc, Tiempo, usuario, myconnect, SalBaseLiq);
                            this.GrabaLiquidacionVacaciones(idPlanilla, idempresa, IdEmpleado, IdApoSoc, idPlanilla, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                        }
                    }
                }

                if (ClaSalario == 2)
                {
                    SalBaseLiq = Math.Round(SalBaseLiq * 0.7, 2);
                }

                if (TipoEmpleado == 1 || TipoEmpleado == 2)
                {
                    int IdFdoSolid = Convert.ToInt32(rowEmp["IdFdoSolid"]);
                    if (IdFdoSolid != 0)
                    {
                        SW1 = 0; Tiempo = 0;
                        // ok = this.msgconfig.BuscaCptos(IdFdoSolid, myconnect, DsDataset); // ERROR: CS1503
                        if (!ok)
                        {
                            MessageBox.Show("Concepto de fondo de solidaridad no esta creado." + IdFdoSolid, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }

                        if (SW1 == 0 && LiqSoloNov == "N")
                        {
                            BaseFdo = BuscaBaseFdoSolidaridad(PerLiq, idPlanilla, idempresa, IdEmpleado, IdFdoSolid, CicloMes, Salario, Periodicidad, myconnect);
                            if (Salario > 0)
                            {
                                VlrLiq = LiquidaFdoSolidaridad(IdFdoSolid, Salario, Periodicidad, DsDataset);
                                if (VlrLiq > 0)
                                {
                                    GrabaLiquidacionVacaciones(idPlanilla, idempresa, IdEmpleado, IdFdoSolid, idPlanilla, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                                }
                            }
                        }
                    }
                }

                // EPS
                int IdEps = Convert.ToInt32(rowEmpl["IdEps"]);
                if (IdEps != 0)
                {
                    SW1 = 0; Tiempo = 0;
                    // ok = this.msgconfig.BuscaCptos(IdEps, myconnect, DsDataset); // ERROR: CS1503
                    if (!ok)
                    {
                        MessageBox.Show("Concepto de liquidacion (EPS) no esta creado." + IdEps, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }

                    if (ClaSalario == 6 || ClaSalario == 7)
                    {
                        SalBaseLiq = Math.Round(SalMinimo / 240) * Horaliq;
                        VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, Tiempo, usuario, myconnect, SalBaseLiq);

                        if (cicloPagSegSoc == 2)
                        {
                            if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                            {
                                VlrLiq = 0;
                                SW1 = 1;
                            }
                            else
                            {
                                HorasTmpPlaAnt = (int)this.CalculaDiasPlanillaAnt(PerLiq, idempresa, IdEmpleado, Fecing, Fecret, Estado, usuario, myconnect);
                                SalBaseLiq = Math.Round(SalMinimo / 240) * HorasTmpPlaAnt;
                                VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, Tiempo, usuario, myconnect, SalBaseLiq);
                            }
                        }

                        if (VlrLiq > 0)
                        {
                            GrabaLiquidacionVacaciones(idPlanilla, idempresa, IdEmpleado, IdEps, idPlanilla, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                            SW1 = 1;
                        }
                    }

                    if (SW1 == 0)
                    {
                        if (SalBaseLiq < Math.Round(SalMinimo / 240) * Horaliq)
                        {
                            SalBaseLiq = Math.Round(SalMinimo / 240) * Horaliq;
                        }
                        VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, Tiempo, usuario, myconnect, SalBaseLiq);
                        if (cicloPagSegSoc == 2)
                        {
                            if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                            {
                                VlrLiq = 0;
                            }
                            else
                            {
                                HorasTmpPlaAnt = (int)this.CalculaDiasPlanillaAnt(PerLiq, idempresa, IdEmpleado, Fecing, Fecret, Estado, usuario, myconnect, ref PlaIni);
                                SalBaseLiqTmp = this.BuscaAcumuladosPlanillaAnt(PlaIni, idempresa, IdEmpleado, myconnect);

                                if (SalBaseLiqTmp > 0)
                                {
                                    if (ClaSalario == 2)
                                    {
                                        SalBaseLiqTmp = Math.Round(SalBaseLiqTmp * 0.7, 2);
                                    }

                                    if (SalBaseLiqTmp < Math.Round(SalMinimo / 240) * HorasTmpPlaAnt)
                                    {
                                        SalBaseLiqTmp = Math.Round(SalMinimo / 240) * HorasTmpPlaAnt;
                                    }

                                    VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, Tiempo, usuario, myconnect, SalBaseLiqTmp);
                                }
                            }
                        }
                        if (VlrLiq > 0)
                        {
                            GrabaLiquidacionVacaciones(idPlanilla, idempresa, IdEmpleado, IdEps, idPlanilla, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                        }
                    }
                }

                // Pension
                if (TipoEmpleado == 1 || TipoEmpleado == 2)
                {
                    int Idpension = Convert.ToInt32(rowEmpl["Idpension"]);
                    if (Idpension != 0)
                    {
                        SW1 = 0; Tiempo = 0;
                        // ok = this.msgconfig.BuscaCptos(Idpension, myconnect, DsDataset); // ERROR: CS1503
                        if (!ok)
                        {
                            MessageBox.Show("Concepto de liquidacion (Pension) no esta creado." + Idpension, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }

                        if (ClaSalario == 6 || ClaSalario == 7)
                        {
                            SW1 = 1;
                        }

                        if (SW1 == 0)
                        {
                            if (SalBaseLiq < Math.Round(SalMinimo / 240) * Horaliq)
                            {
                                SalBaseLiq = Math.Round(SalMinimo / 240) * Horaliq;
                            }

                            VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, Idpension, Tiempo, usuario, myconnect, SalBaseLiq);

                            if (cicloPagSegSoc == 2)
                            {
                                if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                {
                                    VlrLiq = 0;
                                }
                                else
                                {
                                    HorasTmpPlaAnt = (int)this.CalculaDiasPlanillaAnt(PerLiq, idempresa, IdEmpleado, Fecing, Fecret, Estado, usuario, myconnect, ref PlaIni);
                                    SalBaseLiqTmp = this.BuscaAcumuladosPlanillaAnt(PlaIni, idempresa, IdEmpleado, myconnect);

                                    if (SalBaseLiqTmp > 0)
                                    {
                                        if (ClaSalario == 2)
                                        {
                                            SalBaseLiqTmp = Math.Round(SalBaseLiqTmp * 0.7, 2);
                                        }

                                        if (SalBaseLiqTmp < Math.Round(SalMinimo / 240) * HorasTmpPlaAnt)
                                        {
                                            SalBaseLiqTmp = Math.Round(SalMinimo / 240) * HorasTmpPlaAnt;
                                        }

                                        VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, Idpension, Tiempo, usuario, myconnect, SalBaseLiqTmp);
                                    }
                                }
                            }

                            if (VlrLiq > 0)
                            {
                                GrabaLiquidacionVacaciones(idPlanilla, idempresa, IdEmpleado, Idpension, idPlanilla, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                            }
                        }
                    }
                }

            Siguiente:
                MsgBarra.PerformStep();
                reg += 1;
            }

            MsgBarra.Close();
            MsgBarra.Dispose();
            return false;
        }

        public double LiquidaIndemnizacion(int idempresa, DateTime FecLiq, DateTime FecIngreso, DateTime FecVenceContrato, double Salario, double SalarioBase, int ClaseContrato, double CausaRetiro, bool RegEspecial, OdbcConnection myconnect, ref double DiasLiqIndem, ref double VlrDiaIndem)
        {
            DataSet dsempresa = new DataSet();
            DataSet dscausaret = new DataSet();
            int StPeriodoPrueba = 0;
            double DiasLiq = 0, AniosLiq = 0, SMMLV = 0;
            double CantSMMLV = 0, CantDiasPrimerAnio = 0, CantDiasAnioResto = 0, SalBasLiq = 0;
            double VlrLiquidado = 0, DiasPerPrueba = 0, SalBase = 0;

            this.msgconfig.BuscaEmpresa(idempresa, myconnect, dsempresa);

            DataRow rowEmp = dsempresa.Tables["tblempresas"].Rows[0];
            StPeriodoPrueba = Convert.ToInt32(rowEmp["periodoprueba"]);
            SMMLV = Convert.ToDouble(rowEmp["ValSalMinimo"]);
            DiasPerPrueba = StPeriodoPrueba * 30;

            DiasLiqIndem = 0;
            VlrDiaIndem = 0;

            if (SMMLV <= 0)
            {
                MessageBox.Show("Salario minimo mensual legal vigente no esta parametrizado, revisar parametros empresa", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            // ok = this.msgconfig.BuscaCausalesRetiro(CausaRetiro, myconnect, dscausaret); // ERROR: CS1503

            if (ok)
            {
                if (Convert.ToInt32(dscausaret.Tables["tblcauret"].Rows[0]["Indem"]) == 1)
                {
                    SalBase = Math.Round(Salario / 30, 2);
                    SalBasLiq = SalarioBase;

                    if (SalBase > SalBasLiq)
                    {
                        SalBasLiq = SalBase;
                    }

                    switch (ClaseContrato)
                    {
                        case 1:
                        case 2:
                        case 5: // Contrato a termino
                            DiasLiq = this.msgconfig.CalculaDias(FecVenceContrato, FecLiq);
                            if (DiasLiq > 0)
                            {
                                VlrLiquidado = Math.Round(DiasLiq * SalBasLiq, 2);
                                DiasLiqIndem = DiasLiq;
                                VlrDiaIndem = SalBasLiq;
                            }
                            break;

                        case 0: // Contrato Indefinido
                            CantSMMLV = Salario / SMMLV;
                            DiasLiq = this.msgconfig.CalculaDias(FecLiq, FecIngreso);

                            if (DiasLiq > DiasPerPrueba)
                            {
                                AniosLiq = Math.Round(DiasLiq / 360, 8);

                                if (FecIngreso.Year < 1991)
                                {
                                    if (!RegEspecial)
                                    {
                                        RegEspecial = true;
                                    }
                                }

                                if (!RegEspecial)
                                {
                                    if (CantSMMLV < 10)
                                    {
                                        CantDiasPrimerAnio = 30;
                                        CantDiasAnioResto = 20;
                                    }
                                    else
                                    {
                                        CantDiasPrimerAnio = 20;
                                        CantDiasAnioResto = 15;
                                    }

                                    if (AniosLiq <= 1)
                                    {
                                        CantDiasAnioResto = 0;
                                    }
                                    else
                                    {
                                        AniosLiq = AniosLiq - 1;
                                    }
                                }
                                else
                                {
                                    if (AniosLiq <= 1)
                                    {
                                        CantDiasPrimerAnio = 45;
                                        CantDiasAnioResto = 0;
                                    }
                                    else if (AniosLiq > 1 && AniosLiq < 5)
                                    {
                                        CantDiasPrimerAnio = 45;
                                        CantDiasAnioResto = 15;
                                    }
                                    else if (AniosLiq >= 5 && AniosLiq < 10)
                                    {
                                        CantDiasPrimerAnio = 45;
                                        CantDiasAnioResto = 20;
                                    }
                                    else if (AniosLiq >= 10)
                                    {
                                        CantDiasPrimerAnio = 45;
                                        CantDiasAnioResto = 40;
                                    }

                                    if (AniosLiq > 1)
                                    {
                                        AniosLiq = AniosLiq - 1;
                                    }
                                }

                                DiasLiq = AniosLiq * 360;
                                VlrLiquidado = Math.Round(((DiasLiq * CantDiasAnioResto) / 360) * SalBasLiq, 2);
                                VlrLiquidado += Math.Round(CantDiasPrimerAnio * SalBasLiq, 2);

                                DiasLiq = (DiasLiq * CantDiasAnioResto) / 360;
                                DiasLiqIndem = Math.Round(DiasLiq + CantDiasPrimerAnio, 4);
                                VlrDiaIndem = SalBasLiq;
                            }
                            break;
                    }
                }
            }
            return VlrLiquidado;
        }

        public double LiquidaIndemnizacion(int idempresa, DateTime FecLiq, DateTime FecIngreso, DateTime FecVenceContrato, double Salario, double SalarioBase, int ClaseContrato, double CausaRetiro, bool RegEspecial, OdbcConnection myconnect)
        {
            double DiasLiqIndem = 0;
            double VlrDiaIndem = 0;
            return LiquidaIndemnizacion(idempresa, FecLiq, FecIngreso, FecVenceContrato, Salario, SalarioBase, ClaseContrato, CausaRetiro, RegEspecial, myconnect, ref DiasLiqIndem, ref VlrDiaIndem);
        }

        public DataTable CalculaSalarioProporcional(int idempresa, int idPlanilla, double Idempleado, DateTime Fecing, DateTime fecfin, string usuario, OdbcConnection myconnect, string TipoReg = "A")
        {
            DateTime fechaIniPlanilla = default(DateTime);
            int HorasLiq = 0, HorasAus = 0, HorasAusIncap = 0;
            double ValAuxTransp = 0;
            double vlrLiq = 0;
            DataSet DsDataset = new DataSet();
            int DiasCiclo = 0, IdSalBasico = 0;
            double Salario = 0, BaseLiq = 0;
            DataSet dsdata = new DataSet();
            string StString = "";
            DataSet dsperpagos = new DataSet();
            string Stmysql;
            int plaini = 999999, plafin = 999999;
            double fila = 0;
            int IdPeriodo;
            DateTime FecIniPla = default(DateTime), FecFInPla = default(DateTime);
            StringBuilder Stbuilder = new StringBuilder();
            int sw1 = 0;

            dsdata.Tables.Add("tblliq");
            dsdata.Tables["tblliq"].Columns.Add("idcpto", StString.GetType());
            dsdata.Tables["tblliq"].Columns.Add("consecutivo", StString.GetType());
            dsdata.Tables["tblliq"].Columns.Add("tiempo", StString.GetType());
            dsdata.Tables["tblliq"].Columns.Add("valor", StString.GetType());

            // ok = this.msgconfig.BuscaPeriodosPagos(idPlanilla, idempresa, myconnect, DsDataset); // ERROR: CS1503

            if (!ok)
            {
                MessageBox.Show("Periodo de pago no esta creado, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return dsdata.Tables["tblliq"];
            }

            if (string.Compare(fecfin.ToString("dd-MM-yyyy"), Convert.ToDateTime(DsDataset.Tables["tblperpagos"].Rows[0]["FechaFinal"]).ToString("dd-MM-yyyy")) > 0)
            {
                return dsdata.Tables["tblliq"];
            }

            fechaIniPlanilla = Convert.ToDateTime(DsDataset.Tables["tblperpagos"].Rows[0]["Fecinicial"]);

            if (Convert.ToString(DsDataset.Tables["tblperpagos"].Rows[0]["CruceAnt"]) == "Y")
            {
                fechaIniPlanilla = new DateTime(fechaIniPlanilla.Year, fechaIniPlanilla.Month, 1);
            }

            if (string.Compare(fecfin.ToString("dd-MM-yyyy"), Convert.ToDateTime(fechaIniPlanilla).ToString("dd-MM-yyyy")) < 0)
            {
                return dsdata.Tables["tblliq"];
            }

            this.msgconfig.BuscaEmpresa(idempresa, myconnect, DsDataset);

            IdPeriodo = Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["idperiodo"]);

            DiasCiclo = (int)(DateAndTime.DateDiff(DateInterval.Day, fechaIniPlanilla, fecfin) + 1);
            if (fecfin.Day == 31)
            {
                DiasCiclo -= 1;
            }

            if (fecfin.Month == 2 && DiasCiclo < 15)
            {
                switch (fecfin.Day)
                {
                    case 29:
                        DiasCiclo += 1;
                        fecfin = fecfin.AddDays(1);
                        break;
                    default:
                        if (fecfin.Day <= 28)
                        {
                            DiasCiclo += 2;
                            fecfin = fecfin.AddDays(2);
                        }
                        break;
                }
            }

            // ok = this.msgconfig.BuscaEmpleado(idempresa, Idempleado, myconnect, DsDataset); // ERROR: CS1503

            if (Convert.ToString(DsDataset.Tables["tblperpagos"].Rows[0]["NoLiqAusen"]) == "N")
            {
                if (Convert.ToInt32(DsDataset.Tables["tblempleados"].Rows[0]["estado"]) == 2)
                {
                    if (Convert.ToDateTime(DsDataset.Tables["tblempleados"].Rows[0]["FecRetiro"]) < fechaIniPlanilla)
                    {
                        return dsdata.Tables["tblliq"];
                    }
                    else
                    {
                        if (Convert.ToDateTime(DsDataset.Tables["tblempleados"].Rows[0]["FecRetiro"]) >= fechaIniPlanilla && Convert.ToDateTime(DsDataset.Tables["tblempleados"].Rows[0]["FecRetiro"]) <= fecfin)
                        {
                            HorasAus = (int)((DateAndTime.DateDiff(DateInterval.Day, fecfin, Convert.ToDateTime(DsDataset.Tables["tblempleados"].Rows[0]["FecRetiro"])) * -1) * 8);
                        }
                    }
                }

                // HorasAus += this.RevisaAusentismo(idempresa, idPlanilla, Idempleado, fechaIniPlanilla, fecfin, usuario, myconnect, BaseLiq, TipoReg, dsdata); // ERROR: CS1620
                if (Fecing > fechaIniPlanilla)
                {
                    HorasAus += (int)(DateAndTime.DateDiff(DateInterval.Day, fechaIniPlanilla, Fecing) * 8);
                }

                // HorasAusIncap = this.RevisaAusentIncapacidad(idPlanilla, idempresa, Idempleado, Fecing, fechaIniPlanilla, fecfin, usuario, myconnect, BaseLiq, TipoReg, dsdata); // ERROR: CS1620
                vlrLiq = 0;
            }

            if (Convert.ToString(DsDataset.Tables["tblperpagos"].Rows[0]["NoliqSalAut"]) == "N")
            {
                DataRow rowEmpresa = DsDataset.Tables["tblempresas"].Rows[0];
                switch (Convert.ToInt32(DsDataset.Tables["tblempleados"].Rows[0]["clasesalario"]))
                {
                    case 1:
                        switch (Convert.ToInt32(DsDataset.Tables["tblempleados"].Rows[0]["TipEmpleado"]))
                        {
                            case 1:
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalBasico"]);
                                break;
                            case 2:
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                break;
                            case 3:
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdAusVaca"]);
                                break;
                        }
                        break;
                    case 2:
                        switch (Convert.ToInt32(DsDataset.Tables["tblempleados"].Rows[0]["TipEmpleado"]))
                        {
                            case 1:
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalIntegral"]);
                                break;
                            case 2:
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                break;
                            case 3:
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdAusVaca"]);
                                break;
                        }
                        break;
                    case 6:
                    case 7:
                        IdSalBasico = Convert.ToInt32(rowEmpresa["IdAprSena"]);
                        break;
                }

                Salario = Convert.ToDouble(DsDataset.Tables["tblempleados"].Rows[0]["salario"]);
                // vlrLiq = LiquidaBasico(IdSalBasico, DiasCiclo, HorasAus + HorasAusIncap, Salario, HorasLiq); // ERROR: CS1620

                if (vlrLiq > 0)
                {
                    dsdata.Tables["tblliq"].Rows.Add(IdSalBasico, 0, HorasLiq, vlrLiq);
                }
            }

            if (Convert.ToString(DsDataset.Tables["tblperpagos"].Rows[0]["NoliqSalAut"]) == "N")
            {
                if (Convert.ToInt32(DsDataset.Tables["tblempleados"].Rows[0]["ClauxTra"]) == 1)
                {
                    if (Convert.ToInt32(DsDataset.Tables["tblempleados"].Rows[0]["TipEmpleado"]) == 1)
                    {
                        vlrLiq = LiquidaAuxTrasporte(Convert.ToInt32(DsDataset.Tables["tblempresas"].Rows[0]["IdTrasporte"]), Salario, Convert.ToInt32(DsDataset.Tables["tblempleados"].Rows[0]["clasesalario"]), HorasLiq, myconnect);
                        if (vlrLiq > 0)
                        {
                            dsdata.Tables["tblliq"].Rows.Add(DsDataset.Tables["tblempresas"].Rows[0]["IdTrasporte"], 0, HorasLiq, vlrLiq);
                        }
                    }
                }
            }

            if (Convert.ToString(DsDataset.Tables["tblperpagos"].Rows[0]["CruceAnt"]) == "Y")
            {
                Stmysql = "select min(idplanilla) as campo1, max(idplanilla) as campo2 from nom_perpagos where idperiodo = '" + IdPeriodo + "' and idempresa = '" + idempresa + "'";

                string _plaini = plaini.ToString(), _plafin = plafin.ToString();
                ok = this.msgodbc.ExecuteQueryconec(Stmysql, myconnect, "CalculaDiasPlanillaAnt", ref _plaini, ref _plafin);
                int.TryParse(_plaini, out plaini);
                int.TryParse(_plafin, out plafin);

                if (ok)
                {
                    // ok = this.msgconfig.BuscaPeriodosPagos(plaini, idempresa, myconnect, dsperpagos); // ERROR: CS1503
                    if (ok)
                    {
                        if (Convert.ToString(dsperpagos.Tables["tblperpagos"].Rows[0]["LiqAnt"]) == "N")
                        {
                            return dsdata.Tables["tblliq"];
                        }
                        FecIniPla = Convert.ToDateTime(dsperpagos.Tables["tblperpagos"].Rows[0]["Fecinicial"]);
                    }
                    // ok = this.msgconfig.BuscaPeriodosPagos(plafin, idempresa, myconnect, dsperpagos); // ERROR: CS1503
                    if (ok)
                    {
                        FecFInPla = Convert.ToDateTime(dsperpagos.Tables["tblperpagos"].Rows[0]["FechaFinal"]);
                    }
                    Stbuilder.Append("select liqpla.idplanilla, liqpla.idnomina, liqpla.idempleado, liqpla.IdCpto, liqpla.consecutivo, liqpla.natur, ");
                    Stbuilder.Append("liqpla.dia, liqpla.Tiempo, liqpla.Valor, liqpla.Forpago, liqpla.Cencos,nomemp.Liquidado,nomemp.FecLiquidado,nomemp.FecReingreso,nomemp.fecing ");
                    Stbuilder.Append("from nom_liqplan liqpla ");
                    Stbuilder.Append("inner join nom_empresas emp on liqpla.idnomina = emp.IdEmpresa ");
                    Stbuilder.Append("inner join nom_empleados nomemp on liqpla.idnomina=nomemp.idnomina and liqpla.idempleado=nomemp.idempleado ");
                    Stbuilder.Append("where liqpla.idplanilla = " + plaini + " and liqpla.idnomina = " + idempresa + " and liqpla.idempleado='" + Idempleado + "' and ");
                    Stbuilder.Append("liqpla.IdCpto in (emp.IdApoSoc1,emp.IdApoSoc2,emp.IdPrimaServ3) ");
                    Stbuilder.Append("order by liqpla.idempleado, liqpla.IdCpto, liqpla.consecutivo");

                    ok = this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "AplicandoCruceAnticipos", DsDataset, "tblliq");
                    if (ok)
                    {
                        for (fila = 0; fila <= DsDataset.Tables["tblliq"].Rows.Count - 1; fila++)
                        {
                            sw1 = 0;
                            DataRow rowLiq = DsDataset.Tables["tblliq"].Rows[(int)fila];

                            Stbuilder.Replace(Stbuilder.ToString(), "");

                            Stbuilder.Append("select liqpla.idplanilla, liqpla.idnomina, liqpla.idempleado, liqpla.IdCpto, liqpla.consecutivo, liqpla.natur, ");
                            Stbuilder.Append("liqpla.dia, liqpla.Tiempo, liqpla.Valor, liqpla.Forpago, liqpla.Cencos,nomemp.Liquidado,nomemp.FecLiquidado,nomemp.FecReingreso,nomemp.fecing ");
                            Stbuilder.Append("from nom_liqplan liqpla ");
                            Stbuilder.Append("inner join nom_empresas emp on liqpla.idnomina = emp.IdEmpresa ");
                            Stbuilder.Append("inner join nom_empleados nomemp on liqpla.idnomina=nomemp.idnomina and liqpla.idempleado=nomemp.idempleado ");
                            Stbuilder.Append("where liqpla.idplanilla = " + plafin + " and liqpla.idnomina = " + idempresa + " and liqpla.idempleado=" + DsDataset.Tables["tblliq"].Rows[(int)fila]["idempleado"] + " and ");
                            Stbuilder.Append("liqpla.IdCpto =" + DsDataset.Tables["tblliq"].Rows[(int)fila]["IdCpto"] + " ");
                            Stbuilder.Append("order by liqpla.idempleado, liqpla.IdCpto, liqpla.consecutivo");

                            ok = this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "AplicandoCruceAnticipos", dsdata, "tblant");
                            if (ok)
                            {
                                rowLiq["Valor"] = Convert.ToDouble(rowLiq["Valor"]) - Convert.ToDouble(dsdata.Tables["tblant"].Rows[0]["Valor"]);
                            }
                            dsdata.Tables["tblant"].Rows.Clear();

                            if (sw1 == 0)
                            {
                                if (Convert.ToDouble(rowLiq["Valor"]) > 0)
                                {
                                    if (Convert.ToString(rowLiq["natur"]) == "1")
                                    {
                                        rowLiq["natur"] = "2";
                                    }

                                    dsdata.Tables["tblliq"].Rows.Add(rowLiq["IdCpto"], 0, 0, Convert.ToDouble(rowLiq["Valor"]));
                                }
                            }
                        }
                    }
                }
            }

            return dsdata.Tables["tblliq"];
        }

        public bool BuscaMaestroLiquidacionEmpl(int idplanilla, int idnomina, double idempleado, OdbcConnection myconnect, ref DataSet DsDatLiq)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            try
            {
                DsDatLiq.Tables.Remove("tblliqemp");
            }
            catch (Exception) { }

            StBuilder.Append("select idplanilla,idnomina,idempleado,salario,fecingreso,clacontrato,fecvencecontrato,regimenespecial,");
            StBuilder.Append("fecliquida,causal,diaslncesantia,diaslnprima,diaslnvacacion,antcesantiaanterior,antcesantiaactual,");
            StBuilder.Append("fecultpagvac,fecultpagprim,basecesantias,baseprimas,basevacaciones,baseindemnizacion,diascesantias,");
            StBuilder.Append("diasprimas,diasvacaciones,diasindemnizacion,contabilizo,fecultpagces,fecultpagintces ");
            StBuilder.Append("from nom_maeliqemp ");
            StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "'");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaMaestroLiquidacionEmpl", DsDatosLiq, "tblliqemp");
            if (DsDatosLiq.Tables["tblliqemp"].Rows.Count > 0)
            {
                try
                {
                    DsDatLiq.Tables.Add(DsDatosLiq.Tables["tblliqemp"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaMaestroLiquidacionEmpl(int idplanilla, int idnomina, double idempleado, OdbcConnection myconnect)
        {
            DataSet _ds = new DataSet();
            return BuscaMaestroLiquidacionEmpl(idplanilla, idnomina, idempleado, myconnect, ref _ds);
        }

        public bool GrabaMaestroLiquidacionEmpl(int idplanilla, int idnomina, double idempleado, double salario, DateTime fecingreso,
            int clacontrato, DateTime fecvencecontrato, string regimenespecial, DateTime fecliquida, int causal, double diaslncesantia,
            double diaslnprima, double diaslnvacacion, double antcesantiaanterior, double antcesantiaactual, DateTime fecultpagvac, DateTime fecultpagprim,
            double basecesantias, double baseprimas, double basevacaciones, double baseindemnizacion, double diascesantias, double diasprimas,
            double diasvacaciones, double diasindemnizacion, string contabilizo, string Usuario, DateTime fecultpagces, DateTime fecultpagintces, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            ok = this.BuscaMaestroLiquidacionEmpl(idplanilla, idnomina, idempleado, myconnect);
            switch (ok)
            {
                case false:
                    StBuilder.Append("insert into nom_maeliqemp (idplanilla,idnomina,idempleado,salario,fecingreso,clacontrato,fecvencecontrato,regimenespecial,");
                    StBuilder.Append("fecliquida,causal,diaslncesantia,diaslnprima,diaslnvacacion,antcesantiaanterior,antcesantiaactual,");
                    StBuilder.Append("fecultpagvac,fecultpagprim,basecesantias,baseprimas,basevacaciones,baseindemnizacion,diascesantias,");
                    StBuilder.Append("diasprimas,diasvacaciones,diasindemnizacion,contabilizo,fechasys,usuario,fecultpagces,fecultpagintces) values (");
                    StBuilder.Append(idplanilla + ",");
                    StBuilder.Append(idnomina + ",");
                    StBuilder.Append(idempleado + ",'");
                    StBuilder.Append(salario + "','");
                    StBuilder.Append(fecingreso.ToString(varini.PstForFec) + "',");
                    StBuilder.Append(clacontrato + ",'");
                    StBuilder.Append(fecvencecontrato.ToString(varini.PstForFec) + "','");
                    StBuilder.Append(regimenespecial + "','");
                    StBuilder.Append(fecliquida.ToString(varini.PstForFec) + "',");
                    StBuilder.Append(causal + ",'");
                    StBuilder.Append(diaslncesantia + "','");
                    StBuilder.Append(diaslnprima + "','");
                    StBuilder.Append(diaslnvacacion + "','");
                    StBuilder.Append(antcesantiaanterior + "','");
                    StBuilder.Append(antcesantiaactual + "','");
                    StBuilder.Append(fecultpagvac.ToString(varini.PstForFec) + "','");
                    StBuilder.Append(fecultpagprim.ToString(varini.PstForFec) + "','");
                    StBuilder.Append(basecesantias + "','");
                    StBuilder.Append(baseprimas + "','");
                    StBuilder.Append(basevacaciones + "','");
                    StBuilder.Append(baseindemnizacion + "','");
                    StBuilder.Append(diascesantias + "','");
                    StBuilder.Append(diasprimas + "','");
                    StBuilder.Append(diasvacaciones + "','");
                    StBuilder.Append(diasindemnizacion + "','");
                    StBuilder.Append(contabilizo + "','");
                    StBuilder.Append(DateTime.Now.ToString(varini.pstForfecyHora) + "','");
                    StBuilder.Append(Usuario + "','");
                    StBuilder.Append(fecultpagces.ToString(varini.PstForFec) + "','");
                    StBuilder.Append(fecultpagintces.ToString(varini.PstForFec) + "')");
                    break;
                case true:
                    StBuilder.Append("update nom_maeliqemp set ");
                    StBuilder.Append("salario='" + salario + "',");
                    StBuilder.Append("fecingreso='" + fecingreso.ToString(varini.PstForFec) + "',");
                    StBuilder.Append("clacontrato=" + clacontrato + ",");
                    StBuilder.Append("fecvencecontrato='" + fecvencecontrato.ToString(varini.PstForFec) + "',");
                    StBuilder.Append("regimenespecial='" + regimenespecial + "',");
                    StBuilder.Append("fecliquida='" + fecliquida.ToString(varini.PstForFec) + "',");
                    StBuilder.Append("causal=" + causal + ",");
                    StBuilder.Append("diaslncesantia='" + diaslncesantia + "',");
                    StBuilder.Append("diaslnprima='" + diaslnprima + "',");
                    StBuilder.Append("diaslnvacacion='" + diaslnvacacion + "',");
                    StBuilder.Append("antcesantiaanterior='" + antcesantiaanterior + "',");
                    StBuilder.Append("antcesantiaactual='" + antcesantiaactual + "',");
                    StBuilder.Append("fecultpagvac='" + fecultpagvac.ToString(varini.PstForFec) + "',");
                    StBuilder.Append("fecultpagprim='" + fecultpagprim.ToString(varini.PstForFec) + "',");
                    StBuilder.Append("basecesantias='" + basecesantias + "',");
                    StBuilder.Append("baseprimas='" + baseprimas + "',");
                    StBuilder.Append("basevacaciones='" + basevacaciones + "',");
                    StBuilder.Append("baseindemnizacion='" + baseindemnizacion + "',");
                    StBuilder.Append("diascesantias='" + diascesantias + "',");
                    StBuilder.Append("diasprimas='" + diasprimas + "',");
                    StBuilder.Append("diasvacaciones='" + diasvacaciones + "',");
                    StBuilder.Append("diasindemnizacion='" + diasindemnizacion + "',");
                    StBuilder.Append("contabilizo='" + contabilizo + "',");
                    StBuilder.Append("fechasys='" + DateTime.Now.ToString(varini.pstForfecyHora) + "',");
                    StBuilder.Append("usuario='" + Usuario + "', ");
                    StBuilder.Append("fecultpagces='" + fecultpagces.ToString(varini.PstForFec) + "', ");
                    StBuilder.Append("fecultpagintces='" + fecultpagintces.ToString(varini.PstForFec) + "' ");
                    StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "'");
                    break;
            }
            ok = this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaMaestroLiquidacionEmpl");
            return ok;
        }

        public bool BuscaDetalleLiquidacionEmpl(int idplanilla, int idnomina, double idempleado, double idcpto, double consecutivo, OdbcConnection myconnect, ref DataSet DsDatLiq)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            try
            {
                DsDatLiq.Tables.Remove("tblliqdet");
            }
            catch (Exception) { }

            StBuilder.Append("select idplanilla, idnomina, idempleado, idcpto, consecutivo, tiempo, valor, natur ");
            StBuilder.Append("from nom_detliqemp ");
            StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "' ");
            StBuilder.Append("and idcpto='" + idcpto + "' and consecutivo='" + consecutivo + "' ");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaDetalleLiquidacionEmpl", DsDatosLiq, "tblliqdet");
            if (DsDatosLiq.Tables["tblliqdet"].Rows.Count > 0)
            {
                try
                {
                    DsDatLiq.Tables.Add(DsDatosLiq.Tables["tblliqdet"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaDetalleLiquidacionEmpl(int idplanilla, int idnomina, double idempleado, double idcpto, double consecutivo, OdbcConnection myconnect)
        {
            DataSet _ds = new DataSet();
            return BuscaDetalleLiquidacionEmpl(idplanilla, idnomina, idempleado, idcpto, consecutivo, myconnect, ref _ds);
        }

        public virtual bool BuscaDetalleLiquidacionEmpl(int idplanilla, int idnomina, double idempleado, OdbcConnection myconnect, ref DataSet DsDatLiq)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            try
            {
                DsDatLiq.Tables.Remove("tblliqdet");
            }
            catch (Exception) { }

            StBuilder.Append("select idplanilla, idnomina, idempleado, idcpto, consecutivo, tiempo, valor, natur ");
            StBuilder.Append("from nom_detliqemp ");
            StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "' ");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaDetalleLiquidacionEmpl", DsDatosLiq, "tblliqdet");
            if (DsDatosLiq.Tables["tblliqdet"].Rows.Count > 0)
            {
                try
                {
                    DsDatLiq.Tables.Add(DsDatosLiq.Tables["tblliqdet"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual bool BuscaLiquidacionEmplxPlanilla(int idplanilla, int idnomina, OdbcConnection myconnect, ref DataSet DsDatLiq)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            try
            {
                DsDatLiq.Tables.Remove("tblliqdet");
            }
            catch (Exception) { }

            StBuilder.Append("select maeliq.idplanilla, maeliq.idnomina, maeliq.idempleado, nomdet.idcpto, nomdet.consecutivo, nomdet.tiempo, nomdet.valor, nomdet.natur, ");
            StBuilder.Append("maeliq.usuario,emp.idcencos ");
            StBuilder.Append("from nom_maeliqemp maeliq ");
            StBuilder.Append("inner join nom_detliqemp nomdet on maeliq.idplanilla=nomdet.idplanilla and maeliq.idnomina=nomdet.idnomina and maeliq.idempleado=nomdet.idempleado ");
            StBuilder.Append("inner join nom_empleados emp on maeliq.idempleado=emp.idempleado ");
            StBuilder.Append("where maeliq.idplanilla='" + idplanilla + "' and maeliq.idnomina='" + idnomina + "' and maeliq.contabilizo='Y' ");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaLiquidacionEmplxPlanilla", DsDatosLiq, "tblliqdet");
            if (DsDatosLiq.Tables["tblliqdet"].Rows.Count > 0)
            {
                try
                {
                    DsDatLiq.Tables.Add(DsDatosLiq.Tables["tblliqdet"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool GrabaDetalleLiquidacionEmpl(int idplanilla, int idnomina, double idempleado, double idcpto, double consecutivo,
            double tiempo, double valor, int natur, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            ok = this.BuscaDetalleLiquidacionEmpl(idplanilla, idnomina, idempleado, idcpto, consecutivo, myconnect);
            switch (ok)
            {
                case false:
                    StBuilder.Append("insert into nom_detliqemp (idplanilla, idnomina, idempleado, idcpto, consecutivo, tiempo, valor, natur) ");
                    StBuilder.Append("values (");
                    StBuilder.Append(idplanilla + ",");
                    StBuilder.Append(idnomina + ",");
                    StBuilder.Append(idempleado + ",'");
                    StBuilder.Append(idcpto + "','");
                    StBuilder.Append(consecutivo + "','");
                    StBuilder.Append(tiempo + "','");
                    StBuilder.Append(valor + "','");
                    StBuilder.Append(natur + "')");
                    break;
                case true:
                    StBuilder.Append("update nom_detliqemp set ");
                    StBuilder.Append("tiempo='" + tiempo + "',");
                    StBuilder.Append("valor='" + valor + "',");
                    StBuilder.Append("natur=" + natur + " ");
                    StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "' ");
                    StBuilder.Append("and idcpto='" + idcpto + "' and consecutivo='" + consecutivo + "' ");
                    break;
            }
            ok = this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaDetalleLiquidacionEmpl");
            return ok;
        }

        public void EliminaDetalleLiquidacionEmp(int idplanilla, int idnomina, double idempleado, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("delete from nom_detliqemp ");
            StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "' ");

            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "EliminaDetalleLiquidacionEmp");
        }

        public void ActualizaMaeLiquidacionEmp(int idplanilla, int idnomina, double idempleado, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("update nom_maeliqemp set ");
            StBuilder.Append("contabilizo='Y' ");
            StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "' ");

            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "ActualizaMaeLiquidacionEmp");
        }

        public void ActualizaEmplLiquidacionTotal(int idnomina, double idempleado, DateTime FecLiquidacion, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("update nom_empleados set ");
            StBuilder.Append("Liquidado='Y',");
            StBuilder.Append("FecLiquidado='");
            StBuilder.Append(FecLiquidacion.ToString(varini.PstForFec) + "' ");
            StBuilder.Append("where idnomina='" + idnomina + "' and idempleado='" + idempleado + "' ");

            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "ActualizaEmplLiquidacionTotal");
        }

        public void AplicaLiqTotalEmpPlanilla(int IdPlanilla, int idnomina, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            double fila = 0;

            ok = this.BuscaLiquidacionEmplxPlanilla(IdPlanilla, idnomina, myconnect, ref dsdata);
            if (ok)
            {
                for (fila = 0; fila <= dsdata.Tables["tblliqdet"].Rows.Count - 1; fila++)
                {
                    DataRow row = dsdata.Tables["tblliqdet"].Rows[(int)fila];
                    // ok = this.BuscaLiquidacion(IdPlanilla, idnomina, row["idempleado"], row["idcpto"], row["consecutivo"], myconnect); // ERROR: CS1503
                    if (ok)
                    {
                        row["consecutivo"] = Convert.ToString(row["idcpto"]) + IdPlanilla.ToString();
                    }
                    // this.GrabaLiquidacion(IdPlanilla, idnomina, row["idempleado"], row["idcpto"], row["consecutivo"], row["natur"], row["tiempo"], row["tiempo"], row["valor"], row["usuario"], row["idcencos"], myconnect, "T"); // ERROR: CS1503
                }
            }
        }

        public void ActualizaLiquidacionVac(int idplanilla, int idnomina, double idempleado, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("update nom_liqvac set ");
            StBuilder.Append("contabilizo='Y' ");
            StBuilder.Append("where idplanilla='" + idplanilla + "' and idnomina='" + idnomina + "' and idempleado='" + idempleado + "' ");

            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "ActualizaLiquidacionVac");
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
                mycomqueryconec.Connection = appadoConect;
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0)
                {
                    result = true;
                }
                while (Myread.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myread["campo1"] is DBNull)
                        {
                            Campo1 = "0";
                        }
                        else
                        {
                            Campo1 = Convert.ToString(Myread["campo1"]);
                        }
                    }
                    if (Campo2 != "")
                    {
                        if (Myread["campo2"] is DBNull)
                        {
                            Campo2 = "0";
                        }
                        else
                        {
                            Campo2 = Convert.ToString(Myread["campo2"]);
                        }
                    }
                    if (Campo3 != "")
                    {
                        if (Myread["campo3"] is DBNull)
                        {
                            Campo3 = "0";
                        }
                        else
                        {
                            Campo3 = Convert.ToString(Myread["campo3"]);
                        }
                    }
                    if (Campo4 != "")
                    {
                        if (Myread["campo4"] is DBNull)
                        {
                            Campo4 = "0";
                        }
                        else
                        {
                            Campo4 = Convert.ToString(Myread["campo4"]);
                        }
                    }
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(Information.Err().Description + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento)
        {
            string Campo1 = "", Campo2 = "", Campo3 = "", Campo4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref Campo3, ref Campo4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1)
        {
            string Campo2 = "", Campo3 = "", Campo4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref Campo3, ref Campo4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2)
        {
            string Campo3 = "", Campo4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref Campo1, ref Campo2, ref Campo3, ref Campo4);
        }

        public DataSet ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref DataSet DsDataSet, string Nomtabla)
        {
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandTimeout = 1000;
                mycomqueryconec.Connection = appadoConect;
                MyDataAdater.SelectCommand = mycomqueryconec;
                MyDataAdater.Fill(DsDataSet, Nomtabla);

                return DsDataSet;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Information.Err().Description + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
        }
    }
}
