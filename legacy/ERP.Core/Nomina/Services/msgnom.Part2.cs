using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Nomina.Services
{
    public partial class msgnom
    {
        public bool LiquidacionPlanilla(int idPlanilla, int idempresa, string usuario, Form Myforma, OdbcConnection myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            DataSet DsDataset = new DataSet();
            double Salario = 0, Idempleado = 0, VlrLiq = 0;
            int Periodicidad = 0, Tiempo = 0;
            double reg = 0;
            int ClauxTra = 0, CicloPtra = 0, SW1 = 0;
            double SalBaseLiq = 0;
            string AfiFondo = "N";
            double TotalDevengado = 0, SubTrasporte = 0, SalarioMinimo = 0;
            DateTime fecini = DateTime.MinValue, fecfin = DateTime.MinValue;
            int HorasAus = 0, HorasAusIncap = 0;
            int DiasCiclo = 0;
            double Horaliq = 0;
            int ClaSalario = 0, IdSalBasico = 0, IdAusCap = 0;
            DateTime Fecing = DateTime.MinValue, Fecret = DateTime.MinValue;
            double BaseLiqIncap = 0;
            int Estado = 0;
            DateTime FecReing = DateTime.MinValue;
            int Contrato = 0;
            string idcencos = "";
            double SalMinimo = 0;
            string LiqSoloNov = "N", LiqSalAut = "N", NoLiqAusent = "N", NoliqLib = "N";
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidando planilla", Myforma);
            double BaseFdo = 0;
            int CicloMes = 0, PerLiq = 0;
            int HorasTmpPlaAnt = 0, HorasPlanillaAnt = 0, cicloPagSegSoc = 0;
            double SalBaseLiqTmp = 0;
            int PlaIni = 999999;
            string LiqSoloAnticipos = "N", HaceCruceAnticipos = "N";
            int TipoEmpleado = 0;
            string msg = "", Liquidado = "";
            DateTime DtpFecLiqTotal = DateTime.MinValue;
            int IdCptoVac = 0;

            // ok = msgconfig.BuscaPeriodosPagos(idPlanilla, idempresa, myconnect, DsDataset); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Planilla no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            else
            {
                DataRow rowPerpagos = DsDataset.Tables["tblperpagos"].Rows[0];
                fecini = Convert.ToDateTime(rowPerpagos["Fecinicial"]);
                fecfin = Convert.ToDateTime(rowPerpagos["FechaFinal"]);
                LiqSoloNov = Convert.ToString(rowPerpagos["SoloNov"]);
                LiqSalAut = Convert.ToString(rowPerpagos["noliqsalaut"]);
                NoLiqAusent = Convert.ToString(rowPerpagos["noliqausen"]);
                NoliqLib = Convert.ToString(rowPerpagos["noliqLib"]);
                PerLiq = Convert.ToInt32(rowPerpagos["idperiodo"]);
                CicloMes = Convert.ToInt32(rowPerpagos["CicloMes"]);
                LiqSoloAnticipos = Convert.ToString(rowPerpagos["LiqAnt"]);
                HaceCruceAnticipos = Convert.ToString(rowPerpagos["CruceAnt"]);

                DiasCiclo = (int)DateAndTime.DateDiff(DateInterval.Day, fecini, fecfin) + 1;
                if (fecfin.Day == 31)
                {
                    DiasCiclo -= 1;
                }

                if (fecfin.Month == 2 && DiasCiclo < 15)
                {
                    if (fecfin.Day <= 28)
                    {
                        DiasCiclo += 2;
                        fecfin = fecfin.AddDays(2);
                    }
                    else if (fecfin.Day == 29)
                    {
                        DiasCiclo += 1;
                        fecfin = fecfin.AddDays(1);
                    }
                }
            }

            ok = msgconfig.BuscaEmpresa(idempresa, myconnect, DsDataset);
            if (!ok)
            {
                MessageBox.Show("Empresa no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            else
            {
                SalMinimo = Convert.ToDouble(DsDataset.Tables["tblempresas"].Rows[0]["ValSalMinimo"]);
                IdCptoVac = Convert.ToInt32(DsDataset.Tables["tblempresas"].Rows[0]["IdVacasiones"]);
            }

            EliminaLiquidacion(idPlanilla, idempresa, myconnect);

            if (LiqSoloAnticipos == "Y")
            {
                this.LiquidacionPlanillaSoloAnticipos(idPlanilla, idempresa, usuario, Myforma, myconnect);
                return false;
            }

            if (HaceCruceAnticipos == "Y")
            {
                this.LiquidacionPlanillaCruceAnticipos(idPlanilla, idempresa, usuario, Myforma, myconnect);
                return false;
            }

            StBuilder.Append("select idnomina,idempleado,salario,claseSalario,ideps,idpension,idarp,idsena,idicbf,clanom,ClauxTra,CicloPtra,AfiFondo,estado,");
            StBuilder.Append("Fecing,FecRetiro,FecReingreso,Contrato,idcencos,CicloApo,TipEmpleado,Liquidado,FecLiquidado ");
            StBuilder.Append(" from nom_empleados ");
            StBuilder.Append(" where  idnomina = '" + idempresa + "' and ClaNom = '" + DsDataset.Tables["tblperpagos"].Rows[0]["Periodicidad"] + "' ");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "LiquidacionPlanilla", ref DsDataset, "tblLiqEmpl");
            MsgBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblLiqEmpl"].Rows.Count);
            MsgBarra.Show();

            while (reg < DsDataset.Tables["tblLiqEmpl"].Rows.Count)
            {
                Fecing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["fecing"]);
                Fecret = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecRetiro"]);
                FecReing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecReingreso"]);
                DtpFecLiqTotal = new DateTime(1950, 1, 1);

                if (FecReing != new DateTime(1950, 1, 1))
                {
                    if (FecReing > Fecing)
                    {
                        Fecing = FecReing;
                    }
                }

                if (Fecing > fecfin)
                {
                    goto Siguiente;
                }

                DataRow rowEmpl = DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg];
                SalBaseLiq = 0; HorasAus = 0; TotalDevengado = 0; PlaIni = 999999;
                Salario = Convert.ToDouble(rowEmpl["salario"]);
                Periodicidad = Convert.ToInt32(rowEmpl["clanom"]);
                Idempleado = Convert.ToDouble(rowEmpl["idempleado"]);
                ClauxTra = Convert.ToInt32(rowEmpl["ClauxTra"]);
                CicloPtra = Convert.ToInt32(rowEmpl["CicloPtra"]);
                AfiFondo = Convert.ToString(rowEmpl["AfiFondo"]);
                ClaSalario = Convert.ToInt32(rowEmpl["clasesalario"]);
                Estado = Convert.ToInt32(rowEmpl["estado"]);
                idcencos = Convert.ToString(rowEmpl["idcencos"]);
                cicloPagSegSoc = Convert.ToInt32(rowEmpl["CicloApo"]);
                TipoEmpleado = Convert.ToInt32(rowEmpl["TipEmpleado"]);
                Liquidado = Convert.ToString(rowEmpl["Liquidado"]);

                // ok = this.BuscaLiquidacionVacaciones(idPlanilla, idempresa, Idempleado, myconnect, null, IdCptoVac, Convert.ToDateTime(DsDataset.Tables["tblperpagos"].Rows[0]["FechaFinal"])); // ERROR: CS1503, CS1620
                if (ok)
                {
                    goto Siguiente;
                }

                if (!(rowEmpl["FecLiquidado"] is DBNull))
                {
                    DtpFecLiqTotal = Convert.ToDateTime(rowEmpl["FecLiquidado"]);
                }

                DataRow rowEmpresa = DsDataset.Tables["tblempresas"].Rows[0];

                switch (ClaSalario)
                {
                    case 1:
                        switch (TipoEmpleado)
                        {
                            case 1:
                            case 4: // Empleados activos
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalBasico"]);
                                msg = "(Salario Basico)";
                                break;
                            case 2: // Empleados pensionados
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                msg = "(Salario Pension)";
                                break;
                            case 3: // Empleados con tramites de jubilacion
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdAusVaca"]);
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
                            case 4: // Empleados activos
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalIntegral"]);
                                msg = "(Salario Integral)";
                                break;
                            case 2: // Empleados pensionados
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                msg = "(Salario Pension)";
                                break;
                            case 3: // Empleados con tramites de jubilacion
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdAusVaca"]);
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
                        IdSalBasico = Convert.ToInt32(rowEmpresa["IdAprSena"]);
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
                        if (Estado == 2)
                        {
                            if (Fecret < fecini)
                            {
                                goto Siguiente;
                            }
                            else
                            {
                                if (Liquidado == "Y")
                                {
                                    if (DtpFecLiqTotal >= fecini && DtpFecLiqTotal <= fecfin)
                                    {
                                        goto Siguiente;
                                    }
                                }

                                if (Fecret >= fecini && Fecret <= fecfin)
                                {
                                    HorasAus = (int)((DateAndTime.DateDiff(DateInterval.Day, fecfin, Fecret) * -1) * 8);
                                }
                            }
                        }

                        HorasAus += RevisaAusentismo(idempresa, idPlanilla, Idempleado, fecini, fecfin, usuario, myconnect, ref SalBaseLiq);
                        if (Fecing > fecini)
                        {
                            HorasAus += (int)(DateAndTime.DateDiff(DateInterval.Day, fecini, Fecing) * 8);
                        }

                        // HorasAusIncap = this.RevisaAusentIncapacidad(idPlanilla, idempresa, Idempleado, Fecing, fecini, fecfin, usuario, myconnect, SalBaseLiq); // ERROR: CS1620
                        VlrLiq = 0;
                    }

                    if (LiqSalAut == "N")
                    {
                        // VlrLiq = LiquidaBasico(IdSalBasico, DiasCiclo, HorasAus + HorasAusIncap, Salario, ref Horaliq); // ERROR: CS1503
                        GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdSalBasico, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Horaliq, 0, VlrLiq, usuario, idcencos, myconnect);
                        TotalDevengado += VlrLiq;
                        if (Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["basealq"]) == 1)
                        {
                            SalBaseLiq += VlrLiq;
                            Salario = VlrLiq;
                        }
                    }
                }

                // Auxilio de Transporte
                int IdTrasporte = Convert.ToInt32(rowEmpresa["IdTrasporte"]);
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
                        // VlrLiq = LiquidaAuxTrasporte(IdTrasporte, Salario, ClaSalario, Horaliq, myconnect); // ERROR: CS1503

                        switch (ClauxTra)
                        {
                            case 1:
                                switch (CicloPtra)
                                {
                                    case 1:
                                        if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 1)
                                        {
                                            VlrLiq = 0;
                                        }
                                        break;
                                    case 2:
                                        if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                        {
                                            VlrLiq = 0;
                                        }
                                        else
                                        {
                                            // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect); // ERROR: CS0266
                                            VlrLiq += LiquidaAuxTrasporte(IdTrasporte, Salario, ClaSalario, HorasTmpPlaAnt, myconnect);
                                        }
                                        break;
                                }
                                break;
                            case 2:
                                VlrLiq = 0;
                                break;
                        }

                        if (VlrLiq > 0)
                        {
                            GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdTrasporte, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                            TotalDevengado += VlrLiq;
                        }
                        if (Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["basealq"]) == 1)
                        {
                            SalBaseLiq += VlrLiq;
                        }
                    }
                }

                // Aportes Sociales
                if (AfiFondo == "Y")
                {
                    int IdApoSoc = Convert.ToInt32(rowEmpresa["IdApoSoc"]);
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
                            // VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdApoSoc, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615
                            GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdApoSoc, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                        }
                    }
                }

                // this.LiquidaMovimientos(idPlanilla, idempresa, Idempleado, usuario, myconnect, SalBaseLiq, TotalDevengado); // ERROR: CS1620

                if (ClaSalario == 2)
                {
                    SalBaseLiq = Math.Round(SalBaseLiq * 0.7, 2);
                }

                // Fondo de Solidaridad
                if (TipoEmpleado == 1)
                {
                    int IdFdoSolid = Convert.ToInt32(rowEmpresa["IdFdoSolid"]);
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
                            BaseFdo = BuscaBaseFdoSolidaridad(PerLiq, idPlanilla, idempresa, Idempleado, IdFdoSolid, CicloMes, Salario, Periodicidad, myconnect);
                            if (BaseFdo > 0)
                            {
                                if (ClaSalario == 2)
                                {
                                    BaseFdo = Math.Round(BaseFdo * 0.7, 2);
                                }
                                VlrLiq = LiquidaFdoSolidaridad(IdFdoSolid, BaseFdo, Periodicidad, DsDataset);
                                if (VlrLiq > 0)
                                {
                                    GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdFdoSolid, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
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

                    switch (ClaSalario)
                    {
                        case 6:
                        case 7:
                            SalBaseLiq = Math.Round(SalMinimo / 240) * Horaliq;
                            // VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615

                            if (cicloPagSegSoc == 2)
                            {
                                if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                {
                                    VlrLiq = 0;
                                    SW1 = 1;
                                }
                                else
                                {
                                    // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect); // ERROR: CS0266
                                    SalBaseLiq = Math.Round(SalMinimo / 240) * HorasTmpPlaAnt;
                                    // VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615
                                }
                            }

                            if (VlrLiq > 0)
                            {
                                GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdEps, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                                SW1 = 1;
                            }
                            break;
                    }

                    if (SW1 == 0)
                    {
                        if (SalBaseLiq < Math.Round(SalMinimo / 240) * Horaliq)
                        {
                            SalBaseLiq = Math.Round(SalMinimo / 240) * Horaliq;
                        }
                        // VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615

                        if (cicloPagSegSoc == 2)
                        {
                            if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                            {
                                VlrLiq = 0;
                            }
                            else
                            {
                                // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect, ref PlaIni); // ERROR: CS0266
                                SalBaseLiqTmp = this.BuscaAcumuladosPlanillaAnt(PlaIni, idempresa, Idempleado, myconnect);

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

                                    // VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiqTmp); // ERROR: CS1615
                                }
                            }
                        }

                        if (VlrLiq > 0)
                        {
                            GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdEps, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
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

                            // VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, Idpension, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615

                            if (cicloPagSegSoc == 2)
                            {
                                if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                {
                                    VlrLiq = 0;
                                }
                                else
                                {
                                    // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect, ref PlaIni); // ERROR: CS0266
                                    SalBaseLiqTmp = this.BuscaAcumuladosPlanillaAnt(PlaIni, idempresa, Idempleado, myconnect);

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

                                        // VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, Idpension, ref Tiempo, usuario, myconnect, SalBaseLiqTmp); // ERROR: CS1615
                                    }
                                }
                            }

                            if (VlrLiq > 0)
                            {
                                GrabaLiquidacion(idPlanilla, idempresa, Idempleado, Idpension, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                            }
                        }
                    }
                }

                // Libranzas
                if (NoliqLib == "N")
                {
                    this.LiquidaLibranzas(idPlanilla, idempresa, Convert.ToDouble(rowEmpl["idempleado"]), Periodicidad, Convert.ToDouble(rowEmpl["salario"]), TotalDevengado, SubTrasporte, SalarioMinimo, Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["Ciclomes"]), Convert.ToDateTime(DsDataset.Tables["tblperpagos"].Rows[0]["FechaFinal"]), usuario, myconnect);
                }

            Siguiente:
                MsgBarra.PerformStep();
                reg += 1;
            }

            if (reg > 0)
            {
                this.LiquidaRetFtePlanilla(idempresa, idPlanilla, usuario, myconnect);
                this.AplicaVacacionesLiqPlanilla(idPlanilla, idempresa, myconnect);
                this.AplicaLiqTotalEmpPlanilla(idPlanilla, idempresa, myconnect);
            }

            MsgBarra.Close();
            MsgBarra.Dispose();

            return false;
        }

        public bool LiquidacionPlanillaSoloAnticipos(int idPlanilla, int idempresa, string usuario, Form Myforma, OdbcConnection myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            DataSet DsDataset = new DataSet();
            double Salario = 0, Idempleado = 0, VlrLiq = 0;
            int Periodicidad = 0, Tiempo = 0;
            double reg = 0;
            int ClauxTra = 0, CicloPtra = 0, SW1 = 0;
            double SalBaseLiq = 0;
            string AfiFondo = "N";
            double TotalDevengado = 0, SubTrasporte = 0, SalarioMinimo = 0;
            DateTime fecini = DateTime.MinValue, fecfin = DateTime.MinValue;
            int HorasAus = 0, HorasAusIncap = 0;
            int DiasCiclo = 0;
            double Horaliq = 0;
            int ClaSalario = 0, IdSalBasico = 0, IdAusCap = 0;
            DateTime Fecing = DateTime.MinValue, Fecret = DateTime.MinValue;
            double BaseLiqIncap = 0;
            int Estado = 0;
            DateTime FecReing = DateTime.MinValue;
            int Contrato = 0;
            string idcencos = "";
            double SalMinimo = 0;
            string LiqSoloNov = "N", LiqSalAut = "N", NoLiqAusent = "N", NoliqLib = "N";
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidando planilla Solo Anticipos", Myforma);
            double BaseFdo = 0;
            int CicloMes = 0, PerLiq = 0;
            int HorasTmpPlaAnt = 0, HorasPlanillaAnt = 0, cicloPagSegSoc = 0;
            double SalBaseLiqTmp = 0;
            int PlaIni = 999999;
            string Liquidado = "";
            DateTime DtpFecLiqTotal = DateTime.MinValue;

            int IdAntSalario = 0, IdAntAuxTrans = 0, IdAntAprendiz = 0;
            DataSet DsDatasetAnt = new DataSet();
            DateTime DtFecFIn = DateTime.MinValue;
            int TipoEmpleado = 0;
            string msg = "";
            int DiasCicloOriginal = 0, HorasLiqAnt = 0;
            bool EstaVacacion = false;
            DataSet dsvacacion = new DataSet();
            double FactorLiq = 0, FactorLiqVac = 0;
            DateTime FecLiqVaca = DateTime.MinValue;

            // ok = msgconfig.BuscaPeriodosPagos(idPlanilla, idempresa, myconnect, DsDataset); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Planilla no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            else
            {
                DataRow rowPerpagos = DsDataset.Tables["tblperpagos"].Rows[0];
                fecini = Convert.ToDateTime(rowPerpagos["Fecinicial"]);
                fecfin = Convert.ToDateTime(rowPerpagos["FechaFinal"]);
                LiqSoloNov = Convert.ToString(rowPerpagos["SoloNov"]);
                LiqSalAut = Convert.ToString(rowPerpagos["noliqsalaut"]);
                NoLiqAusent = Convert.ToString(rowPerpagos["noliqausen"]);
                NoliqLib = Convert.ToString(rowPerpagos["noliqLib"]);
                PerLiq = Convert.ToInt32(rowPerpagos["idperiodo"]);
                CicloMes = Convert.ToInt32(rowPerpagos["CicloMes"]);

                DtFecFIn = new DateTime(fecfin.Year, fecfin.Month, DateTime.DaysInMonth(fecfin.Year, fecfin.Month));

                DiasCiclo = (int)DateAndTime.DateDiff(DateInterval.Day, fecini, DtFecFIn) + 1;
                if (DtFecFIn.Day == 31)
                {
                    DiasCiclo -= 1;
                }

                if (fecfin.Month == 2 && DiasCiclo < 15)
                {
                    if (fecfin.Day <= 28)
                    {
                        fecfin = fecfin.AddDays(2);
                    }
                    else if (fecfin.Day == 29)
                    {
                        fecfin = fecfin.AddDays(1);
                    }
                }

                if (DtFecFIn.Month == 2 && DiasCiclo < 30)
                {
                    if (DtFecFIn.Day <= 28)
                    {
                        DiasCiclo += 2;
                    }
                    else if (DtFecFIn.Day == 29)
                    {
                        DiasCiclo += 1;
                    }
                }

                DiasCicloOriginal = (int)DateAndTime.DateDiff(DateInterval.Day, fecini, fecfin) + 1;
                if (fecfin.Day == 31)
                {
                    DiasCicloOriginal -= 1;
                }

                if (fecfin.Month == 2 && DiasCicloOriginal < 15)
                {
                    if (fecfin.Day <= 28)
                    {
                        fecfin = fecfin.AddDays(2);
                    }
                    else if (fecfin.Day == 29)
                    {
                        fecfin = fecfin.AddDays(1);
                    }
                }

                if (fecfin.Month == 2 && DiasCicloOriginal > 15 && DiasCicloOriginal < 30)
                {
                    if (fecfin.Day <= 28)
                    {
                        DiasCicloOriginal += 2;
                    }
                    else if (fecfin.Day == 29)
                    {
                        DiasCicloOriginal += 1;
                    }
                }
            }

            ok = msgconfig.BuscaEmpresa(idempresa, myconnect, DsDataset);
            if (!ok)
            {
                MessageBox.Show("Empresa no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            else
            {
                SalMinimo = Convert.ToDouble(DsDataset.Tables["tblempresas"].Rows[0]["ValSalMinimo"]);
            }

            EliminaLiquidacion(idPlanilla, idempresa, myconnect);

            StBuilder.Append("select idnomina,idempleado,salario,claseSalario,ideps,idpension,idarp,idsena,idicbf,clanom,ClauxTra,CicloPtra,AfiFondo,estado,");
            StBuilder.Append("Fecing,FecRetiro,FecReingreso,Contrato,idcencos,CicloApo,TipEmpleado,Liquidado,FecLiquidado ");
            StBuilder.Append(" from nom_empleados ");
            StBuilder.Append(" where  idnomina = '" + idempresa + "' and ClaNom = '" + DsDataset.Tables["tblperpagos"].Rows[0]["Periodicidad"] + "' ");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "LiquidacionPlanilla", ref DsDataset, "tblLiqEmpl");
            MsgBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblLiqEmpl"].Rows.Count);
            MsgBarra.Show();

            while (reg < DsDataset.Tables["tblLiqEmpl"].Rows.Count)
            {
                Fecing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["fecing"]);
                Fecret = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecRetiro"]);
                FecReing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecReingreso"]);
                DtpFecLiqTotal = new DateTime(1950, 1, 1);
                FecLiqVaca = new DateTime(1950, 1, 1);
                EstaVacacion = false;
                FactorLiqVac = 0;
                FactorLiq = 0;

                if (FecReing != new DateTime(1950, 1, 1))
                {
                    if (FecReing > Fecing)
                    {
                        Fecing = FecReing;
                    }
                }

                if (Fecing > fecfin)
                {
                    goto Siguiente;
                }

                DataRow rowEmpl = DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg];
                SalBaseLiq = 0; HorasAus = 0; TotalDevengado = 0; PlaIni = 999999;
                Salario = Convert.ToDouble(rowEmpl["salario"]);
                Periodicidad = Convert.ToInt32(rowEmpl["clanom"]);
                Idempleado = Convert.ToDouble(rowEmpl["idempleado"]);
                ClauxTra = Convert.ToInt32(rowEmpl["ClauxTra"]);
                CicloPtra = Convert.ToInt32(rowEmpl["CicloPtra"]);
                AfiFondo = Convert.ToString(rowEmpl["AfiFondo"]);
                ClaSalario = Convert.ToInt32(rowEmpl["clasesalario"]);
                Estado = Convert.ToInt32(rowEmpl["estado"]);
                idcencos = Convert.ToString(rowEmpl["idcencos"]);
                cicloPagSegSoc = Convert.ToInt32(rowEmpl["CicloApo"]);
                TipoEmpleado = Convert.ToInt32(rowEmpl["TipEmpleado"]);
                Liquidado = Convert.ToString(rowEmpl["Liquidado"]);

                if (!(rowEmpl["FecLiquidado"] is DBNull))
                {
                    DtpFecLiqTotal = Convert.ToDateTime(rowEmpl["FecLiquidado"]);
                }

                DataRow rowEmpresa = DsDataset.Tables["tblempresas"].Rows[0];

                switch (ClaSalario)
                {
                    case 1:
                        switch (TipoEmpleado)
                        {
                            case 1:
                            case 4: // Empleados activos
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalBasico"]);
                                msg = "(Salario Basico)";
                                break;
                            case 2: // Empleados pensionados
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                msg = "(Salario Pension)";
                                break;
                            case 3: // Empleados con tramites de jubilacion
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdAusVaca"]);
                                msg = "(Salario Tramites)";
                                break;
                        }
                        IdAntSalario = Convert.ToInt32(rowEmpresa["IdApoSoc1"]);
                        if (IdSalBasico != 0)
                        {
                            SW1 = 0;
                            // ok = this.msgconfig.BuscaCptos(IdSalBasico, myconnect, DsDataset); // ERROR: CS1503
                            if (!ok)
                            {
                                MessageBox.Show("Concepto de liquidacion " + msg + " no esta creado. " + IdSalBasico, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                SW1 = 1;
                            }
                            else
                            {
                                // ok = this.msgconfig.BuscaCptos(IdAntSalario, myconnect, DsDatasetAnt); // ERROR: CS1503
                                if (!ok)
                                {
                                    MessageBox.Show("Concepto de liquidacion (Anticipo Salario) no esta creado. " + IdAntSalario, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    SW1 = 1;
                                }
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
                            case 4: // Empleados activos
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalIntegral"]);
                                msg = "(Salario Basico)";
                                break;
                            case 2: // Empleados pensionados
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                msg = "(Salario Pension)";
                                break;
                            case 3: // Empleados con tramites de jubilacion
                                IdSalBasico = Convert.ToInt32(rowEmpresa["IdAusVaca"]);
                                msg = "(Salario Tramites)";
                                break;
                        }
                        IdAntSalario = Convert.ToInt32(rowEmpresa["IdApoSoc1"]);
                        if (IdSalBasico != 0)
                        {
                            SW1 = 0;
                            // ok = this.msgconfig.BuscaCptos(IdSalBasico, myconnect, DsDataset); // ERROR: CS1503
                            if (!ok)
                            {
                                MessageBox.Show("Concepto de liquidacion " + msg + " no esta creado. " + IdSalBasico, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                SW1 = 1;
                            }
                            else
                            {
                                // ok = this.msgconfig.BuscaCptos(IdAntSalario, myconnect, DsDatasetAnt); // ERROR: CS1503
                                if (!ok)
                                {
                                    MessageBox.Show("Concepto de liquidacion (Anticipo Salario) no esta creado. " + IdAntSalario, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    SW1 = 1;
                                }
                            }
                        }
                        else
                        {
                            SW1 = 1;
                        }
                        break;
                    case 6:
                    case 7:
                        IdSalBasico = Convert.ToInt32(rowEmpresa["IdAprSena"]);
                        IdAntSalario = Convert.ToInt32(rowEmpresa["IdPrimaServ3"]);
                        if (IdSalBasico != 0)
                        {
                            SW1 = 0;
                            // ok = this.msgconfig.BuscaCptos(IdSalBasico, myconnect, DsDataset); // ERROR: CS1503
                            if (!ok)
                            {
                                MessageBox.Show("Concepto de liquidacion (Aprendiz Sena) no esta creado. " + IdSalBasico, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                SW1 = 1;
                            }
                            else
                            {
                                // ok = this.msgconfig.BuscaCptos(IdAntSalario, myconnect, DsDatasetAnt); // ERROR: CS1503
                                if (!ok)
                                {
                                    MessageBox.Show("Concepto de liquidacion (Anticipo Aprendiz Sena) no esta creado. " + IdAntSalario, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    SW1 = 1;
                                }
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
                    // SE DEJA EN COMENTARIO PARA LAS PLANILLAS QUE SON SOLO ANTICIPO, YA QUE LAS NOVEDADES DEBEN APLICARSE EN LA DE CRUCE.

                    if (NoLiqAusent == "N")
                    {
                        HorasAus = 0;
                        if (Estado == 2)
                        {
                            if (Fecret < fecini)
                            {
                                goto Siguiente;
                            }
                            else
                            {
                                if (Liquidado == "Y")
                                {
                                    if (DtpFecLiqTotal >= fecini && DtpFecLiqTotal <= fecfin)
                                    {
                                        goto Siguiente;
                                    }
                                }

                                if (Fecret >= fecini && Fecret <= fecfin)
                                {
                                    HorasAus = (int)((DateAndTime.DateDiff(DateInterval.Day, fecfin, Fecret) * -1) * 8);
                                }
                            }
                        }

                        double _salBaseLiqDummy = 0;
                        // HorasAus += RevisaAusentismo(idempresa, idPlanilla, Idempleado, fecini, fecfin, usuario, myconnect, ref _salBaseLiqDummy, "A", null, "LA"); // ERROR: CS1503

                        if (Fecing > fecini)
                        {
                            HorasAus += (int)(DateAndTime.DateDiff(DateInterval.Day, fecini, Fecing) * 8);
                        }
                    }

                    if (LiqSalAut == "N")
                    {
                        // No se valida las vacaciones para las planillas de anticipos.

                        FactorLiq = Convert.ToDouble(DsDatasetAnt.Tables["tblcptos"].Rows[0]["factor"]);

                        // VlrLiq = LiquidaBasico(IdSalBasico, DiasCiclo, 0, Salario, ref Horaliq); // ERROR: CS1503

                        if (EstaVacacion)
                        {
                            FactorLiq = FactorLiqVac;
                            IdAntSalario = IdSalBasico;
                        }

                        VlrLiq = Math.Round(VlrLiq * (FactorLiq / 100));

                        HorasLiqAnt = DiasCiclo * 8; // DiasCicloOriginal

                        VlrLiq = Math.Round((VlrLiq / HorasLiqAnt) * (HorasLiqAnt - (HorasAus + HorasAusIncap)));

                        GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdAntSalario, 0, Convert.ToInt32(DsDatasetAnt.Tables["tblcptos"].Rows[0]["natur"]), 0, 0, VlrLiq, usuario, idcencos, myconnect);
                        TotalDevengado += VlrLiq;
                        Salario = VlrLiq;
                        if (Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["basealq"]) == 1)
                        {
                            SalBaseLiq += VlrLiq;
                        }
                    }
                }

                // Auxilio de Transporte (Anticipos)
                int IdTrasporteAnt = Convert.ToInt32(rowEmpresa["IdTrasporte"]);
                if (IdTrasporteAnt != 0)
                {
                    SW1 = 0; Tiempo = 0;
                    // ok = this.msgconfig.BuscaCptos(IdTrasporteAnt, myconnect, DsDataset); // ERROR: CS1503
                    if (!ok)
                    {
                        MessageBox.Show("Concepto de liquidacion (Aux. Transporte) no esta creado. " + IdTrasporteAnt, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }
                    else
                    {
                        IdAntAuxTrans = Convert.ToInt32(rowEmpresa["idaposoc2"]);
                        // ok = this.msgconfig.BuscaCptos(IdAntAuxTrans, myconnect, DsDatasetAnt); // ERROR: CS1503
                        if (!ok)
                        {
                            MessageBox.Show("Concepto de liquidacion (Anticipo Auxilio Transporte) no esta creado. " + IdAntAuxTrans, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }
                    }

                    if (ClauxTra == 2)
                    {
                        SW1 = 1;
                    }

                    if (SW1 == 0 && LiqSoloNov == "N" && TipoEmpleado == 1)
                    {
                        // VlrLiq = LiquidaAuxTrasporte(IdTrasporteAnt, Salario, ClaSalario, Horaliq, myconnect); // ERROR: CS1503

                        FactorLiq = Convert.ToDouble(DsDatasetAnt.Tables["tblcptos"].Rows[0]["factor"]);
                        if (EstaVacacion)
                        {
                            FactorLiq = FactorLiqVac;
                            IdAntAuxTrans = IdTrasporteAnt;
                        }

                        VlrLiq = Math.Round(VlrLiq * (FactorLiq / 100));

                        HorasLiqAnt = DiasCiclo * 8; // DiasCicloOriginal

                        VlrLiq = Math.Round((VlrLiq / HorasLiqAnt) * (HorasLiqAnt - (HorasAus + HorasAusIncap)));

                        switch (ClauxTra)
                        {
                            case 1:
                                switch (CicloPtra)
                                {
                                    case 1:
                                        if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 1)
                                        {
                                            VlrLiq = 0;
                                        }
                                        break;
                                    case 2:
                                        if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                        {
                                            VlrLiq = 0;
                                        }
                                        else
                                        {
                                            // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect); // ERROR: CS0266
                                            VlrLiq += LiquidaAuxTrasporte(IdTrasporteAnt, Salario, ClaSalario, HorasTmpPlaAnt, myconnect);
                                        }
                                        break;
                                }
                                break;
                            case 2:
                                VlrLiq = 0;
                                break;
                        }

                        if (VlrLiq > 0)
                        {
                            GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdAntAuxTrans, 0, Convert.ToInt32(DsDatasetAnt.Tables["tblcptos"].Rows[0]["natur"]), 0, 0, VlrLiq, usuario, idcencos, myconnect);
                            TotalDevengado += VlrLiq;
                        }
                        if (Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["basealq"]) == 1)
                        {
                            SalBaseLiq += VlrLiq;
                        }
                    }
                }

                // Aportes Sociales (commented out in VB)
                // ...

                // this.LiquidaMovimientos(idPlanilla, idempresa, Idempleado, usuario, myconnect, SalBaseLiq, TotalDevengado); // ERROR: CS1620

                if (ClaSalario == 2)
                {
                    SalBaseLiq = Math.Round(SalBaseLiq * 0.7, 2);
                }

                // Fondo de Solidaridad (commented out in VB)
                // ...

                // EPS y Pension solo cuando EstaVacacion = True
                if (EstaVacacion)
                {
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

                        switch (ClaSalario)
                        {
                            case 6:
                            case 7:
                                SalBaseLiq = Math.Round(SalMinimo / 240) * HorasLiqAnt;
                                // VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615

                                if (cicloPagSegSoc == 2)
                                {
                                    if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                    {
                                        VlrLiq = 0;
                                        SW1 = 1;
                                    }
                                    else
                                    {
                                        // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect); // ERROR: CS0266
                                        SalBaseLiq = Math.Round(SalMinimo / 240) * HorasTmpPlaAnt;
                                        // VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615
                                    }
                                }

                                if (VlrLiq > 0)
                                {
                                    GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdEps, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                                    SW1 = 1;
                                }
                                break;
                        }

                        if (SW1 == 0)
                        {
                            // VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615

                            if (cicloPagSegSoc == 2)
                            {
                                if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                {
                                    VlrLiq = 0;
                                }
                                else
                                {
                                    // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect, ref PlaIni); // ERROR: CS0266
                                    SalBaseLiqTmp = this.BuscaAcumuladosPlanillaAnt(PlaIni, idempresa, Idempleado, myconnect);

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

                                        // VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, IdEps, ref Tiempo, usuario, myconnect, SalBaseLiqTmp); // ERROR: CS1615
                                    }
                                }
                            }

                            if (VlrLiq > 0)
                            {
                                GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdEps, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
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
                                // VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, Idpension, ref Tiempo, usuario, myconnect, SalBaseLiq); // ERROR: CS1615

                                if (cicloPagSegSoc == 2)
                                {
                                    if (Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["CicloMes"]) != 2)
                                    {
                                        VlrLiq = 0;
                                    }
                                    else
                                    {
                                        // HorasTmpPlaAnt = this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect, ref PlaIni); // ERROR: CS0266
                                        SalBaseLiqTmp = this.BuscaAcumuladosPlanillaAnt(PlaIni, idempresa, Idempleado, myconnect);

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

                                            // VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, Idpension, ref Tiempo, usuario, myconnect, SalBaseLiqTmp); // ERROR: CS1615
                                        }
                                    }
                                }

                                if (VlrLiq > 0)
                                {
                                    GrabaLiquidacion(idPlanilla, idempresa, Idempleado, Idpension, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                                }
                            }
                        }
                    }
                }

                // Libranzas
                if (NoliqLib == "N")
                {
                    this.LiquidaLibranzas(idPlanilla, idempresa, Convert.ToDouble(rowEmpl["idempleado"]), Periodicidad, Convert.ToDouble(rowEmpl["salario"]), TotalDevengado, SubTrasporte, SalarioMinimo, Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["Ciclomes"]), Convert.ToDateTime(DsDataset.Tables["tblperpagos"].Rows[0]["FechaFinal"]), usuario, myconnect);
                }

            Siguiente:
                MsgBarra.PerformStep();
                reg += 1;
            }

            if (reg > 0)
            {
                this.AplicaVacacionesLiqPlanilla(idPlanilla, idempresa, myconnect);
                this.AplicaLiqTotalEmpPlanilla(idPlanilla, idempresa, myconnect);
            }

            MsgBarra.Close();
            MsgBarra.Dispose();

            return false;
        }
    }
}
