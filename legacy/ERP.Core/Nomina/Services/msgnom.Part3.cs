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
        public bool LiquidacionPlanillaCruceAnticipos(int idPlanilla, int idempresa, string usuario, System.Windows.Forms.Form Myforma, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            double Salario = 0, Idempleado = 0, VlrLiq = 0, reg = 0, SalBaseLiq = 0, BaseLiqIncap = 0;
            int Periodicidad = 0, Tiempo = 0, ClauxTra = 0, CicloPtra = 0, SW1 = 0, HorasAus = 0, HorasAusIncap = 0;
            int DiasCiclo = 0, ClaSalario = 0, IdSalBasico = 0, IdAusCap = 0, Estado = 0;
            double Horaliq = 0;
            string AfiFondo = "N";
            double TotalDevengado = 0, SubTrasporte = 0, SalarioMinimo = 0;
            DateTime fecini = default(DateTime), fecfin = default(DateTime), Fecing = default(DateTime), Fecret = default(DateTime);
            DateTime FecReing = default(DateTime);
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
            DateTime DtFecFIn = default(DateTime);
            int TipoEmpleado = 0;
            string msg = "", Liquidado = "";
            DateTime DtpFecLiqTotal = default(DateTime);
            DataSet dsvacaciones = new DataSet();
            DateTime FecLiqVaca = default(DateTime);
            int IdcptoVac = 0;

            // ok = msgconfig.BuscaPeriodosPagos(idPlanilla, idempresa, myconnect, DsDataset); // ERROR: CS1503
            switch (ok)
            {
                case false:
                    MessageBox.Show("Planilla no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                case true:
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
                    }
                    break;
            }

            DtFecFIn = new DateTime(fecini.Year, fecini.Month, 1);

            DiasCiclo = (int)(fecfin - DtFecFIn).Days + 1;
            if (fecfin.Day == 31)
            {
                DiasCiclo -= 1;
            }

            if (fecfin.Month == 2 && DiasCiclo < 15)
            {
                switch (fecfin.Day)
                {
                    case 28:
                        fecfin = fecfin.AddDays(2);
                        break;
                    default:
                        if (fecfin.Day <= 28)
                        {
                            fecfin = fecfin.AddDays(2);
                        }
                        else if (fecfin.Day == 29)
                        {
                            fecfin = fecfin.AddDays(1);
                        }
                        break;
                }
            }

            if (DtFecFIn.Month == 2 && DiasCiclo > 15 && DiasCiclo < 30)
            {
                switch (fecfin.Day)
                {
                    case 28:
                        DiasCiclo += 2;
                        break;
                    default:
                        if (fecfin.Day <= 28)
                        {
                            DiasCiclo += 2;
                        }
                        else if (fecfin.Day == 29)
                        {
                            DiasCiclo += 1;
                        }
                        break;
                }
            }

            ok = msgconfig.BuscaEmpresa(idempresa, myconnect, DsDataset);
            switch (ok)
            {
                case false:
                    MessageBox.Show("Empresa no esta creada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                case true:
                    SalMinimo = Convert.ToDouble(DsDataset.Tables["tblempresas"].Rows[0]["ValSalMinimo"]);
                    IdcptoVac = Convert.ToInt32(DsDataset.Tables["tblempresas"].Rows[0]["IdVacasiones"]);
                    break;
            }

            EliminaLiquidacion(idPlanilla, idempresa, myconnect);

            StBuilder.Append("select idnomina,idempleado,salario,claseSalario,ideps,idpension,idarp,idsena,idicbf,clanom,ClauxTra,CicloPtra,AfiFondo,estado,");
            StBuilder.Append("Fecing,FecRetiro,FecReingreso,Contrato,idcencos,CicloApo,TipEmpleado,Liquidado,FecLiquidado ");
            StBuilder.Append(" from nom_empleados ");
            StBuilder.Append(" where  idnomina = '" + idempresa + "' and ClaNom = '" + DsDataset.Tables["tblperpagos"].Rows[0]["Periodicidad"] + "' ");

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "LiquidacionPlanilla", DsDataset, "tblLiqEmpl");
            MsgBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblLiqEmpl"].Rows.Count);
            MsgBarra.Show();

            this.AplicaVacacionesLiqPlanilla(idPlanilla, idempresa, myconnect);

            while (reg < DsDataset.Tables["tblLiqEmpl"].Rows.Count)
            {
                Fecing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["fecing"]);
                Fecret = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecRetiro"]);
                FecReing = Convert.ToDateTime(DsDataset.Tables["tblLiqEmpl"].Rows[(int)reg]["FecReingreso"]);
                DtpFecLiqTotal = new DateTime(1950, 1, 1);
                FecLiqVaca = new DateTime(1950, 1, 1);

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

                {
                    DataRow rowEmpresa = DsDataset.Tables["tblempresas"].Rows[0];
                    switch (ClaSalario)
                    {
                        case 1:
                            switch (TipoEmpleado)
                            {
                                case 1:
                                case 4:
                                    IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalBasico"]);
                                    msg = "(Salario Basico)";
                                    break;
                                case 2:
                                    IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                    msg = "(Salario Pension)";
                                    break;
                                case 3:
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
                                case 4:
                                    IdSalBasico = Convert.ToInt32(rowEmpresa["IdSalIntegral"]);
                                    msg = "(Salario Basico)";
                                    break;
                                case 2:
                                    IdSalBasico = Convert.ToInt32(rowEmpresa["IdVacaConsol"]);
                                    msg = "(Salario Pension)";
                                    break;
                                case 3:
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
                                if (Fecret < DtFecFIn)
                                {
                                    goto Siguiente;
                                }
                                else
                                {
                                    if (Liquidado == "Y")
                                    {
                                        if (DtpFecLiqTotal >= DtFecFIn && DtpFecLiqTotal <= fecfin)
                                        {
                                            goto Siguiente;
                                        }
                                    }

                                    if (Fecret >= DtFecFIn && Fecret <= fecfin)
                                    {
                                        HorasAus = (int)((fecfin - Fecret).Days) * 8;
                                    }
                                }
                            }
                            this.CalculaDiasPlanillaAnt(PerLiq, idempresa, Idempleado, Fecing, Fecret, Estado, usuario, myconnect, ref PlaIni);

                            // ok = this.BuscaLiquidacionVacaciones(PlaIni, idempresa, Idempleado, myconnect, dsvacaciones, IdcptoVac, Convert.ToDateTime(DsDataset.Tables["tblperpagos"].Rows[0]["FechaFinal"])); // ERROR: CS1503, CS1620
                            if (ok)
                            {
                                if (Convert.ToString(dsvacaciones.Tables["tblliqvac"].Rows[0]["contabilizo"]) == "Y")
                                {
                                    FecLiqVaca = Convert.ToDateTime(dsvacaciones.Tables["tblliqvac"].Rows[0]["fechadisfin"]);
                                    if (FecLiqVaca > fecfin)
                                    {
                                        goto Siguiente;
                                    }
                                }
                            }

                            HorasAus += RevisaAusentismo(idempresa, idPlanilla, Idempleado, DtFecFIn, fecfin, usuario, myconnect);
                            if (Fecing > DtFecFIn)
                            {
                                HorasAus += (int)(Fecing - DtFecFIn).Days * 8;
                            }

                            HorasAusIncap = this.RevisaAusentIncapacidad(idPlanilla, idempresa, Idempleado, Fecing, DtFecFIn, fecfin, usuario, myconnect, ref SalBaseLiq);
                            VlrLiq = 0;
                        }

                        if (LiqSalAut == "N")
                        {
                            int horaLiqInt = 0;
                            VlrLiq = LiquidaBasico(IdSalBasico, DiasCiclo, HorasAus + HorasAusIncap, Salario, ref horaLiqInt);
                            Horaliq = horaLiqInt;
                            GrabaLiquidacion(idPlanilla, idempresa, Idempleado, IdSalBasico, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Horaliq, 0, VlrLiq, usuario, idcencos, myconnect);
                            TotalDevengado += VlrLiq;
                            if (Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["basealq"]) == 1)
                            {
                                SalBaseLiq += VlrLiq;
                                Salario = VlrLiq;
                            }
                        }
                    }

                    // Transporte
                    int idTrasporte = Convert.ToInt32(rowEmpresa["IdTrasporte"]);
                    if (idTrasporte != 0)
                    {
                        SW1 = 0; Tiempo = 0;
                        // ok = this.msgconfig.BuscaCptos(idTrasporte, myconnect, DsDataset); // ERROR: CS1503
                        if (!ok)
                        {
                            MessageBox.Show("Concepto de liquidacion (Aux. Transporte) no esta creado. " + idTrasporte, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }

                        if (ClauxTra == 2)
                        {
                            SW1 = 1;
                        }

                        if (SW1 == 0 && LiqSoloNov == "N" && TipoEmpleado == 1)
                        {
                            VlrLiq = LiquidaAuxTrasporte(idTrasporte, Salario, ClaSalario, (int)Horaliq, myconnect);

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
                                                VlrLiq += LiquidaAuxTrasporte(idTrasporte, Salario, ClaSalario, HorasTmpPlaAnt, myconnect);
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
                                GrabaLiquidacion(idPlanilla, idempresa, Idempleado, idTrasporte, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
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
                        int idApoSoc = Convert.ToInt32(rowEmpresa["IdApoSoc"]);
                        if (idApoSoc != 0)
                        {
                            SW1 = 0; Tiempo = 0;
                            // ok = this.msgconfig.BuscaCptos(idApoSoc, myconnect, DsDataset); // ERROR: CS1503
                            if (!ok)
                            {
                                MessageBox.Show("Concepto de liquidacion (Aportes Sociales) no esta creado." + idApoSoc, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                SW1 = 1;
                            }

                            if (SW1 == 0 && LiqSoloNov == "N")
                            {
                                VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, idApoSoc, Tiempo, usuario, myconnect, SalBaseLiq);
                                GrabaLiquidacion(idPlanilla, idempresa, Idempleado, idApoSoc, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                            }
                        }
                    }

                    this.LiquidaMovimientos(idPlanilla, idempresa, Idempleado, usuario, myconnect, ref SalBaseLiq, ref TotalDevengado);

                    if (ClaSalario == 2)
                    {
                        SalBaseLiq = Math.Round(SalBaseLiq * 0.7, 2);
                    }

                    // Fondo de Solidaridad
                    if (TipoEmpleado == 1)
                    {
                        int idFdoSolid = Convert.ToInt32(rowEmpresa["IdFdoSolid"]);
                        if (idFdoSolid != 0)
                        {
                            SW1 = 0; Tiempo = 0;
                            // ok = this.msgconfig.BuscaCptos(idFdoSolid, myconnect, DsDataset); // ERROR: CS1503
                            if (!ok)
                            {
                                MessageBox.Show("Concepto de fondo de solidaridad no esta creado." + idFdoSolid, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                SW1 = 1;
                            }

                            if (SW1 == 0 && LiqSoloNov == "N")
                            {
                                BaseFdo = BuscaBaseFdoSolidaridad(PerLiq, idPlanilla, idempresa, Idempleado, idFdoSolid, CicloMes, Salario, Periodicidad, myconnect);
                                if (BaseFdo > 0)
                                {
                                    if (ClaSalario == 2)
                                    {
                                        BaseFdo = Math.Round(BaseFdo * 0.7, 2);
                                    }
                                    VlrLiq = LiquidaFdoSolidaridad(idFdoSolid, BaseFdo, Periodicidad, DsDataset);
                                    if (VlrLiq > 0)
                                    {
                                        GrabaLiquidacion(idPlanilla, idempresa, Idempleado, idFdoSolid, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                                    }
                                }
                            }
                        }
                    }
                }

                // EPS
                int idEps = Convert.ToInt32(rowEmpl["IdEps"]);
                if (idEps != 0)
                {
                    SW1 = 0; Tiempo = 0;
                    // ok = this.msgconfig.BuscaCptos(idEps, myconnect, DsDataset); // ERROR: CS1503
                    if (!ok)
                    {
                        MessageBox.Show("Concepto de liquidacion (EPS) no esta creado." + idEps, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }

                    if (ClaSalario == 6 || ClaSalario == 7)
                    {
                        SalBaseLiq = Math.Round(SalMinimo / 240) * Horaliq;
                        VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, idEps, Tiempo, usuario, myconnect, SalBaseLiq);

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
                                VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, idEps, Tiempo, usuario, myconnect, SalBaseLiq);
                            }
                        }

                        if (VlrLiq > 0)
                        {
                            GrabaLiquidacion(idPlanilla, idempresa, Idempleado, idEps, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                            SW1 = 1;
                        }
                    }

                    if (SW1 == 0)
                    {
                        if (SalBaseLiq < Math.Round(SalMinimo / 240) * Horaliq)
                        {
                            SalBaseLiq = Math.Round(SalMinimo / 240) * Horaliq;
                        }
                        VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, idEps, Tiempo, usuario, myconnect, SalBaseLiq);

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

                                    VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, idEps, Tiempo, usuario, myconnect, SalBaseLiqTmp);
                                }
                            }
                        }

                        if (VlrLiq > 0)
                        {
                            GrabaLiquidacion(idPlanilla, idempresa, Idempleado, idEps, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                        }
                    }
                }

                // Pension
                if (TipoEmpleado == 1 || TipoEmpleado == 2)
                {
                    int idPension = Convert.ToInt32(rowEmpl["Idpension"]);
                    if (idPension != 0)
                    {
                        SW1 = 0; Tiempo = 0;
                        // ok = this.msgconfig.BuscaCptos(idPension, myconnect, DsDataset); // ERROR: CS1503
                        if (!ok)
                        {
                            MessageBox.Show("Concepto de liquidacion (Pension) no esta creado." + idPension, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                            VlrLiq = LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, idPension, Tiempo, usuario, myconnect, SalBaseLiq);

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

                                        VlrLiq += LiquidaConcepto(idPlanilla, idempresa, Salario, Periodicidad, idPension, Tiempo, usuario, myconnect, SalBaseLiqTmp);
                                    }
                                }
                            }

                            if (VlrLiq > 0)
                            {
                                GrabaLiquidacion(idPlanilla, idempresa, Idempleado, idPension, 0, Convert.ToInt32(DsDataset.Tables["tblcptos"].Rows[0]["natur"]), Tiempo, 0, VlrLiq, usuario, idcencos, myconnect);
                            }
                        }
                    }
                }

                if (NoliqLib == "N")
                {
                    this.LiquidaLibranzas(idPlanilla, idempresa, Convert.ToDouble(rowEmpl["idempleado"]), Periodicidad, Convert.ToDouble(rowEmpl["salario"]), TotalDevengado, SubTrasporte, SalarioMinimo, Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["Ciclomes"]), Convert.ToDateTime(DsDataset.Tables["tblperpagos"].Rows[0]["FechaFinal"]), usuario, myconnect);
                }

            Siguiente:
                MsgBarra.PerformStep();
                reg += 1;
            }

            this.LiquidaRetFtePlanilla(idempresa, idPlanilla, usuario, myconnect);
            this.AplicaLiqTotalEmpPlanilla(idPlanilla, idempresa, myconnect);
            this.AplicandoCruceAnticipos(PerLiq, idPlanilla, idempresa, usuario, Myforma, myconnect);

            MsgBarra.Close();
            MsgBarra.Dispose();

            return false; // VB implicit return
        }

        private bool AplicandoCruceAnticipos(int IdPeriodo, int IdPlanilla, int IdEmpresa, string Usuario, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            string Stmysql = "";
            int plaini = 999999, plafin = 999999;
            double fila = 0;
            DataSet DsDataset = new DataSet();
            DataSet dsperpagos = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Aplicando Cruce Anticipos", myforma);
            DateTime FecIniPla = default(DateTime), FecFInPla = default(DateTime), FecLiquidado = default(DateTime);
            int sw1 = 0;
            DateTime FecIngreso = default(DateTime);
            DataSet dsdata = new DataSet();

            Stmysql = "select min(idplanilla) as campo1, max(idplanilla) as campo2 from nom_perpagos where idperiodo = '" + IdPeriodo + "' and idempresa = '" + IdEmpresa + "'";
            // ok = this.msgodbc.ExecuteQueryconec(Stmysql, myconnect, "CalculaDiasPlanillaAnt", ref plaini, ref plafin); // ERROR: CS1503

            if (ok)
            {
                // ok = this.msgconfig.BuscaPeriodosPagos(plaini, IdEmpresa, myconnect, dsperpagos); // ERROR: CS1503
                if (ok)
                {
                    if (Convert.ToString(dsperpagos.Tables["tblperpagos"].Rows[0]["LiqAnt"]) == "N")
                    {
                        return false;
                    }
                    FecIniPla = Convert.ToDateTime(dsperpagos.Tables["tblperpagos"].Rows[0]["Fecinicial"]);
                }
                // ok = this.msgconfig.BuscaPeriodosPagos(plafin, IdEmpresa, myconnect, dsperpagos); // ERROR: CS1503
                if (ok)
                {
                    FecFInPla = Convert.ToDateTime(dsperpagos.Tables["tblperpagos"].Rows[0]["FechaFinal"]);
                }

                Stbuilder.Append("select liqpla.idplanilla, liqpla.idnomina, liqpla.idempleado, liqpla.IdCpto, liqpla.consecutivo, liqpla.natur, ");
                Stbuilder.Append("liqpla.dia, liqpla.Tiempo, liqpla.Valor, liqpla.Forpago, liqpla.Cencos,nomemp.Liquidado,nomemp.FecLiquidado,nomemp.FecReingreso,nomemp.fecing ");
                Stbuilder.Append("from nom_liqplan liqpla ");
                Stbuilder.Append("inner join nom_empresas emp on liqpla.idnomina = emp.IdEmpresa ");
                Stbuilder.Append("inner join nom_empleados nomemp on liqpla.idnomina=nomemp.idnomina and liqpla.idempleado=nomemp.idempleado ");
                Stbuilder.Append("where liqpla.idplanilla = " + plaini + " and liqpla.idnomina = " + IdEmpresa + " and ");
                Stbuilder.Append("liqpla.IdCpto in (emp.IdApoSoc1,emp.IdApoSoc2,emp.IdPrimaServ3) ");
                Stbuilder.Append("order by liqpla.idempleado, liqpla.IdCpto, liqpla.consecutivo");

                ok = this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "AplicandoCruceAnticipos", DsDataset, "tblliq");
                if (ok)
                {
                    MsgBarra.ValorMinimoMaximo(0, DsDataset.Tables["tblliq"].Rows.Count);
                    MsgBarra.Show();

                    for (fila = 0; fila <= DsDataset.Tables["tblliq"].Rows.Count - 1; fila++)
                    {
                        sw1 = 0;
                        DataRow rowLiq = DsDataset.Tables["tblliq"].Rows[(int)fila];

                        if (Convert.ToString(rowLiq["Liquidado"]) == "Y")
                        {
                            FecIngreso = Convert.ToDateTime(rowLiq["fecing"]);
                            if (Convert.ToDateTime(rowLiq["FecReingreso"]) != new DateTime(1950, 1, 1) && Convert.ToDateTime(rowLiq["FecReingreso"]) > FecIngreso)
                            {
                                FecIngreso = Convert.ToDateTime(rowLiq["FecReingreso"]);
                            }
                            FecLiquidado = Convert.ToDateTime(rowLiq["FecLiquidado"]);

                            if (FecLiquidado >= FecIniPla && FecLiquidado <= FecFInPla)
                            {
                                if (FecLiquidado >= FecIngreso)
                                {
                                    sw1 = 1;
                                }
                            }
                        }

                        Stbuilder.Replace(Stbuilder.ToString(), "");

                        Stbuilder.Append("select liqpla.idplanilla, liqpla.idnomina, liqpla.idempleado, liqpla.IdCpto, liqpla.consecutivo, liqpla.natur, ");
                        Stbuilder.Append("liqpla.dia, liqpla.Tiempo, liqpla.Valor, liqpla.Forpago, liqpla.Cencos,nomemp.Liquidado,nomemp.FecLiquidado,nomemp.FecReingreso,nomemp.fecing ");
                        Stbuilder.Append("from nom_liqplan liqpla ");
                        Stbuilder.Append("inner join nom_empresas emp on liqpla.idnomina = emp.IdEmpresa ");
                        Stbuilder.Append("inner join nom_empleados nomemp on liqpla.idnomina=nomemp.idnomina and liqpla.idempleado=nomemp.idempleado ");
                        Stbuilder.Append("where liqpla.idplanilla = " + plafin + " and liqpla.idnomina = " + IdEmpresa + " and liqpla.idempleado=" + DsDataset.Tables["tblliq"].Rows[(int)fila]["idempleado"] + " and ");
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
                                GrabaLiquidacion(IdPlanilla, IdEmpresa, rowLiq["idempleado"], Convert.ToInt32(rowLiq["IdCpto"]), Convert.ToDouble(rowLiq["consecutivo"]), Convert.ToInt32(rowLiq["natur"]), Convert.ToDouble(rowLiq["tiempo"]), 0, rowLiq["valor"], Usuario, Convert.ToString(rowLiq["cencos"]), myconnect);
                            }
                        }

                        MsgBarra.PerformStep();
                    }

                    MsgBarra.Close();
                    MsgBarra.Dispose();
                }
            }

            return false; // VB implicit return
        }

        private double LiquidaBasico(int Idcpto, int Dias, int HorasAus, double Sueldo, ref int HoraLiq)
        {
            double VlrLiq = 0;
            int Horas = 0;
            Horas = (Dias * 8) - HorasAus;
            VlrLiq = Math.Round((Sueldo / 240) * Horas);
            HoraLiq = Horas;
            return VlrLiq;
        }

        private double LiquidaBasico(int Idcpto, int Dias, int HorasAus, double Sueldo)
        {
            int HoraLiq = 0;
            return LiquidaBasico(Idcpto, Dias, HorasAus, Sueldo, ref HoraLiq);
        }

        private double LiquidaFdoSolidaridad(int IdCpto, double Baseliq, int Periodicidad, DataSet DsDataFdo)
        {
            double VlrLiq = 0, BaseMen = 0, BaseFdosol = 0;
            double fdosolmay0 = 0, fdosolmay1 = 0, fdosolmay2 = 0, fdosolmay3 = 0, fdosolmay4 = 0, fdosolmay5 = 0;
            double fdotasamay0 = 0, fdotasamay1 = 0, fdotasamay2 = 0, fdotasamay3 = 0, fdotasamay4 = 0, fdotasamay5 = 0;
            double TasaLiq = 0;
            try
            {
                if (DsDataFdo.Tables["tblcptos"].Rows.Count <= 0)
                {
                    return VlrLiq;
                }
            }
            catch (Exception)
            {
                return VlrLiq;
            }

            DataRow rowEmpresa = DsDataFdo.Tables["tblempresas"].Rows[0];
            fdosolmay0 = Convert.ToDouble(DsDataFdo.Tables["tblcptos"].Rows[0]["valor"]);
            fdosolmay1 = Convert.ToDouble(rowEmpresa["fdosolmay1"]);
            fdosolmay2 = Convert.ToDouble(rowEmpresa["fdosolmay2"]);
            fdosolmay3 = Convert.ToDouble(rowEmpresa["fdosolmay3"]);
            fdosolmay4 = Convert.ToDouble(rowEmpresa["fdosolmay4"]);
            fdosolmay5 = Convert.ToDouble(rowEmpresa["fdosolmay5"]);
            fdotasamay0 = Convert.ToDouble(DsDataFdo.Tables["tblcptos"].Rows[0]["factor"]);
            fdotasamay1 = Convert.ToDouble(rowEmpresa["fdotasamay1"]);
            fdotasamay2 = Convert.ToDouble(rowEmpresa["fdotasamay2"]);
            fdotasamay3 = Convert.ToDouble(rowEmpresa["fdotasamay3"]);
            fdotasamay4 = Convert.ToDouble(rowEmpresa["fdotasamay4"]);
            fdotasamay5 = Convert.ToDouble(rowEmpresa["fdotasamay5"]);

            BaseMen = Baseliq;
            TasaLiq = fdotasamay0;
            if (BaseMen > fdosolmay5)
            {
                TasaLiq += fdotasamay5;
            }
            else
            {
                if (BaseMen > fdosolmay4)
                {
                    TasaLiq += fdotasamay4;
                }
                else
                {
                    if (BaseMen > fdosolmay3)
                    {
                        TasaLiq += fdotasamay3;
                    }
                    else
                    {
                        if (BaseMen > fdosolmay2)
                        {
                            TasaLiq += fdotasamay2;
                        }
                        else
                        {
                            if (BaseMen > fdosolmay1)
                            {
                                TasaLiq += fdotasamay1;
                            }
                            else
                            {
                                if (BaseMen > fdosolmay0)
                                {
                                    TasaLiq = fdotasamay0;
                                }
                                else
                                {
                                    TasaLiq = 0;
                                }
                            }
                        }
                    }
                }
            }

            if (TasaLiq == 0)
            {
                BaseFdosol = 0;
                VlrLiq = 0;
            }
            else
            {
                BaseFdosol = Baseliq;
                VlrLiq = Math.Round(BaseFdosol * (TasaLiq / 100));
            }

            return VlrLiq;
        }

        private int RevisaAusentismo(int Idnomina, int idplanilla, double idempleado, DateTime fecini, DateTime FecFin, string Usuario, OdbcConnection myconnect, ref double SalBaseLiq, string TipoReg, ref DataSet DsDataLiqPro, string TipoLiq)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            DateTime fecAusini = default(DateTime), FecAusFin = default(DateTime);
            int sw1 = 0, TmpHoras = 0;
            DateTime FecIniLiq = default(DateTime), FecFinLiq = default(DateTime);
            int Horas = 0;
            double BaseLiqIncap = 0, VlrLiq = 0;
            decimal Factor = 0;
            DataSet dsdata = new DataSet();
            DateTime FecFinPpal = FecFin;

            // BuscaAusentismoTrabajador(Idnomina, idempleado, myconnect, DsDataSet); // ERROR: CS1620
            while (Fila < DsDataSet.Tables["tblnovausent"].Rows.Count)
            {
                DataRow rowAus = DsDataSet.Tables["tblnovausent"].Rows[Fila];
                sw1 = 0;
                FecFin = FecFinPpal;
                fecAusini = Convert.ToDateTime(rowAus["FecInicial"]);
                FecAusFin = Convert.ToDateTime(rowAus["FecFinal"]);

                if (TipoLiq == "LA")
                {
                    if (fecAusini > FecFin)
                    {
                        sw1 = 1;
                    }

                    FecFin = new DateTime(FecFin.Year, FecFin.Month, DateTime.DaysInMonth(FecFin.Year, FecFin.Month));
                    if (FecFin.Day == 31)
                    {
                        FecFin = FecFin.AddDays(-1);
                    }
                }

                if (fecAusini < fecini && FecAusFin < fecini)
                {
                    sw1 = 1;
                }

                if (fecAusini > FecFin && FecAusFin > FecFin)
                {
                    sw1 = 1;
                }

                if (Convert.ToString(rowAus["clase"]) == "5")
                {
                    sw1 = 1;
                }

                if (sw1 == 0)
                {
                    // this.msgconfig.BuscaCptos(Convert.ToInt32(rowAus["idcpto"]), myconnect, dsdata); // ERROR: CS1503

                    BaseLiqIncap = Convert.ToDouble(rowAus["base"]);

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

                    if (FecFinLiq.Month == 2)
                    {
                        if (FecFinLiq.Day > 28)
                        {
                            FecFinLiq = FecFinLiq.AddDays(1);
                        }
                        else if (FecFinLiq.Day == 28)
                        {
                            FecFinLiq = FecFinLiq.AddDays(2);
                        }
                    }

                    if (FecIniLiq.ToString("dd/MM/yyyy") == FecFinLiq.ToString("dd/MM/yyyy"))
                    {
                        if (Convert.ToDouble(rowAus["horas"]) > 0 && Convert.ToDouble(rowAus["horas"]) <= 8)
                        {
                            TmpHoras = Convert.ToInt32(rowAus["horas"]);
                        }
                        else
                        {
                            TmpHoras = ((int)(FecFinLiq - FecIniLiq).Days + 1) * 8;
                        }
                    }
                    else
                    {
                        TmpHoras = ((int)(FecFinLiq - FecIniLiq).Days + 1) * 8;
                    }

                    Horas += TmpHoras;

                    if (Convert.ToDouble(dsdata.Tables["tblcptos"].Rows[0]["factor"]) > 0)
                    {
                        Factor = Convert.ToDecimal(dsdata.Tables["tblcptos"].Rows[0]["factor"]) / 100;
                        VlrLiq = Math.Round(((BaseLiqIncap * (double)Factor) / 240) * TmpHoras, 0);
                    }
                    else
                    {
                        VlrLiq = 0;
                    }

                    if (DsDataLiqPro == null)
                    {
                        if (TipoLiq == "LV")
                        {
                            // this.msgconfig.BuscaEmpleado(Idnomina, idempleado, myconnect, dsdata); // ERROR: CS1503
                            this.GrabaLiquidacionVacaciones(idplanilla, Idnomina, idempleado, Convert.ToInt32(rowAus["Idcpto"]), idplanilla, Convert.ToInt32(dsdata.Tables["tblcptos"].Rows[0]["natur"]), TmpHoras, 0, VlrLiq, Usuario, Convert.ToString(dsdata.Tables["tblempleados"].Rows[0]["idcencos"]), myconnect);
                        }
                        else
                        {
                            GrabaLiquidacion(idplanilla, Idnomina, idempleado, Convert.ToInt32(rowAus["Idcpto"]), 0, 1, TmpHoras, 0, VlrLiq, Usuario, " ", myconnect, TipoReg);
                        }
                    }
                    else
                    {
                        DsDataLiqPro.Tables["tblliq"].Rows.Add(rowAus["Idcpto"], 0, TmpHoras, VlrLiq);
                    }

                    if (Convert.ToInt32(rowAus["basealq"]) == 1)
                    {
                        SalBaseLiq += VlrLiq;
                    }
                }

                Fila += 1;
            }
            return Horas;
        }

        // Overloads for RevisaAusentismo (VB Optional params)
        private int RevisaAusentismo(int Idnomina, int idplanilla, double idempleado, DateTime fecini, DateTime FecFin, string Usuario, OdbcConnection myconnect)
        {
            double SalBaseLiq = 0;
            DataSet DsDataLiqPro = null;
            return RevisaAusentismo(Idnomina, idplanilla, idempleado, fecini, FecFin, Usuario, myconnect, ref SalBaseLiq, "A", ref DsDataLiqPro, "LP");
        }

        private int RevisaAusentismo(int Idnomina, int idplanilla, double idempleado, DateTime fecini, DateTime FecFin, string Usuario, OdbcConnection myconnect, ref double SalBaseLiq)
        {
            DataSet DsDataLiqPro = null;
            return RevisaAusentismo(Idnomina, idplanilla, idempleado, fecini, FecFin, Usuario, myconnect, ref SalBaseLiq, "A", ref DsDataLiqPro, "LP");
        }

        private int RevisaAusentismo(int Idnomina, int idplanilla, double idempleado, DateTime fecini, DateTime FecFin, string Usuario, OdbcConnection myconnect, ref double SalBaseLiq, string TipoReg)
        {
            DataSet DsDataLiqPro = null;
            return RevisaAusentismo(Idnomina, idplanilla, idempleado, fecini, FecFin, Usuario, myconnect, ref SalBaseLiq, TipoReg, ref DsDataLiqPro, "LP");
        }

        private int RevisaAusentismo(int Idnomina, int idplanilla, double idempleado, DateTime fecini, DateTime FecFin, string Usuario, OdbcConnection myconnect, ref double SalBaseLiq, string TipoReg, ref DataSet DsDataLiqPro)
        {
            return RevisaAusentismo(Idnomina, idplanilla, idempleado, fecini, FecFin, Usuario, myconnect, ref SalBaseLiq, TipoReg, ref DsDataLiqPro, "LP");
        }

        private double CalculaDiasProrrogaIncap(double Idempleado, int IdCpto, double Consecutivo, OdbcConnection myconnect, ref double Dias)
        {
            StringBuilder StBuilder = new StringBuilder();
            double fila = 0;
            DataSet dsdata = new DataSet();
            DateTime FecFinLiq = default(DateTime), FecIniLiq = default(DateTime);
            double tmpdias = 0;

            StBuilder.Append("select FecInicial,FecFinal,prorroga,horas,cptoprorroga,consecprorroga ");
            StBuilder.Append("from nom_ausentismos ");
            StBuilder.Append("where IdEmpleado=" + Idempleado + " and Idcpto=" + IdCpto + " and Consecutivo=" + Consecutivo);

            this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "CalculaDiasProrrogaIncap", dsdata, "tblprorroga");

            for (fila = 0; fila <= dsdata.Tables["tblprorroga"].Rows.Count - 1; fila++)
            {
                DataRow rowPro = dsdata.Tables["tblprorroga"].Rows[(int)fila];
                FecIniLiq = Convert.ToDateTime(rowPro["FecInicial"]);
                FecFinLiq = Convert.ToDateTime(rowPro["FecFinal"]);

                if (FecFinLiq.Month == 2)
                {
                    if (FecFinLiq.Day > 28)
                    {
                        FecFinLiq = FecFinLiq.AddDays(1);
                    }
                    else if (FecFinLiq.Day == 28)
                    {
                        FecFinLiq = FecFinLiq.AddDays(2);
                    }
                }

                if (FecIniLiq.ToString("dd/MM/yyyy") == FecFinLiq.ToString("dd/MM/yyyy"))
                {
                    if (Convert.ToDouble(rowPro["horas"]) > 0)
                    {
                        tmpdias = Math.Round(Convert.ToDouble(rowPro["horas"]) / 8, 0);
                    }
                    else
                    {
                        tmpdias = (FecFinLiq - FecIniLiq).Days + 1;
                    }
                }
                else
                {
                    tmpdias = (FecFinLiq - FecIniLiq).Days + 1;
                }

                Dias += tmpdias;

                if (Convert.ToInt32(rowPro["prorroga"]) == 1)
                {
                    CalculaDiasProrrogaIncap(Idempleado, Convert.ToInt32(rowPro["cptoprorroga"]), Convert.ToDouble(rowPro["consecprorroga"]), myconnect, ref Dias);
                }
            }

            return Dias;
        }

        private double CalculaDiasProrrogaIncap(double Idempleado, int IdCpto, double Consecutivo, OdbcConnection myconnect)
        {
            double Dias = 0;
            return CalculaDiasProrrogaIncap(Idempleado, IdCpto, Consecutivo, myconnect, ref Dias);
        }

        private int RevisaAusentIncapacidad(int idplanilla, int Idnomina, double idempleado, DateTime Fecing, DateTime fecini, DateTime FecFin, string usuario, OdbcConnection myconnect, ref double SalBaseLiq, string TipoReg, ref DataSet DsDataLiqPro, string TipoLiq)
        {
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            DateTime fecAusini = default(DateTime), FecAusFin = default(DateTime);
            int sw1 = 0;
            DateTime FecIniLiq = default(DateTime), FecFinLiq = default(DateTime);
            int Horas = 0;
            double BaseLiqIncap = 0, VlrLiq = 0;
            DataSet dsdata = new DataSet();
            decimal Factor = 0;
            int TmpHoras = 0;
            DataSet dsEmpresa = new DataSet();
            int DiasIncapPagas = 0, CptoIncapCxC = 0;
            int CantHorasLiq = 0;
            double DiasIncap = 0, FactorIncapEmpresa = 0;

            this.msgconfig.BuscaEmpresa(Idnomina, myconnect, dsEmpresa);

            DiasIncapPagas = Convert.ToInt32(dsEmpresa.Tables["tblempresas"].Rows[0]["diastope"]);
            CptoIncapCxC = Convert.ToInt32(dsEmpresa.Tables["tblempresas"].Rows[0]["cptoincapcxc"]);
            FactorIncapEmpresa = Convert.ToDouble(dsEmpresa.Tables["tblempresas"].Rows[0]["factorincap"]) / 100;

            DiasIncapPagas = DiasIncapPagas * 8;

            // BuscaAusentismoTrabajador(Idnomina, idempleado, myconnect, DsDataSet); // ERROR: CS1620
            while (Fila < DsDataSet.Tables["tblnovausent"].Rows.Count)
            {
                DataRow rowAus = DsDataSet.Tables["tblnovausent"].Rows[Fila];
                sw1 = 0; DiasIncap = 0;
                fecAusini = Convert.ToDateTime(rowAus["FecInicial"]);
                FecAusFin = Convert.ToDateTime(rowAus["FecFinal"]);
                if (fecAusini < fecini && FecAusFin < fecini)
                {
                    sw1 = 1;
                }

                if (fecAusini > FecFin && FecAusFin > FecFin)
                {
                    sw1 = 1;
                }

                if (Convert.ToString(rowAus["clase"]) != "5")
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

                    if (FecFinLiq.Month == 2)
                    {
                        if (FecFinLiq.Day > 28)
                        {
                            FecFinLiq = FecFinLiq.AddDays(1);
                        }
                        else if (FecFinLiq.Day == 28)
                        {
                            FecFinLiq = FecFinLiq.AddDays(2);
                        }
                    }

                    if (FecIniLiq.ToString("dd/MM/yyyy") == FecFinLiq.ToString("dd/MM/yyyy"))
                    {
                        if (Convert.ToDouble(rowAus["horas"]) > 0)
                        {
                            TmpHoras = Convert.ToInt32(rowAus["horas"]);
                        }
                        else
                        {
                            TmpHoras = ((int)(FecFinLiq - FecIniLiq).Days + 1) * 8;
                        }
                    }
                    else
                    {
                        TmpHoras = ((int)(FecFinLiq - FecIniLiq).Days + 1) * 8;
                    }

                    if (Fecing > fecini)
                    {
                        TmpHoras += ((int)(Fecing - fecini).Days + 1) * 8;
                    }

                    BaseLiqIncap = Convert.ToDouble(rowAus["base"]);

                    if (TmpHoras <= DiasIncapPagas)
                    {
                        // ok = this.msgconfig.BuscaCptos(Convert.ToInt32(rowAus["idcpto"]), myconnect, dsdata); // ERROR: CS1503

                        if (ok)
                        {
                            if (Convert.ToInt32(rowAus["claseinc"]) != 1)
                            {
                                Factor = 1;
                            }
                            else
                            {
                                if (Convert.ToDouble(dsdata.Tables["tblcptos"].Rows[0]["factor"]) > 0)
                                {
                                    Factor = Convert.ToDecimal(dsdata.Tables["tblcptos"].Rows[0]["factor"]) / 100;
                                }
                                else
                                {
                                    Factor = 1;
                                }

                                if (Convert.ToInt32(rowAus["prorroga"]) == 1)
                                {
                                    DiasIncap = CalculaDiasProrrogaIncap(idempleado, Convert.ToInt32(rowAus["cptoprorroga"]), Convert.ToDouble(rowAus["consecprorroga"]), myconnect, ref DiasIncap);
                                    if ((DiasIncap + Math.Round((double)TmpHoras / 8)) > 90)
                                    {
                                        Factor = (decimal)FactorIncapEmpresa;
                                    }
                                }
                            }
                        }

                        VlrLiq = Math.Round(((BaseLiqIncap * (double)Factor) / 240) * TmpHoras, 0);

                        if (DsDataLiqPro == null)
                        {
                            if (TipoLiq == "LV")
                            {
                                // this.msgconfig.BuscaEmpleado(Idnomina, idempleado, myconnect, dsdata); // ERROR: CS1503
                                this.GrabaLiquidacionVacaciones(idplanilla, Idnomina, idempleado, Convert.ToInt32(rowAus["Idcpto"]), idplanilla, Convert.ToInt32(dsdata.Tables["tblcptos"].Rows[0]["natur"]), TmpHoras, 0, VlrLiq, usuario, Convert.ToString(dsdata.Tables["tblempleados"].Rows[0]["idcencos"]), myconnect);
                            }
                            else
                            {
                                GrabaLiquidacion(idplanilla, Idnomina, idempleado, Convert.ToInt32(rowAus["idcpto"]), Convert.ToDouble(rowAus["Consecutivo"]), 1, TmpHoras, 0, VlrLiq, usuario, "", myconnect, TipoReg);
                            }
                        }
                        else
                        {
                            DsDataLiqPro.Tables["tblliq"].Rows.Add(rowAus["idcpto"], rowAus["Consecutivo"], TmpHoras, VlrLiq);
                        }

                        if (Convert.ToInt32(rowAus["basealq"]) == 1)
                        {
                            SalBaseLiq += VlrLiq;
                        }
                    }
                    else
                    {
                        // TmpHoras > DiasIncapPagas
                        if (Convert.ToString(rowAus["claseinc"]) == "1")
                        {
                            // ok = this.msgconfig.BuscaCptos(CptoIncapCxC, myconnect, dsdata); // ERROR: CS1503
                            if (!ok)
                            {
                                MessageBox.Show("Concepto de ausentismo por incapacidad que genera cuenta por cobrar no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                DiasIncapPagas = TmpHoras;
                                CantHorasLiq = TmpHoras;
                            }
                            else
                            {
                                if (Convert.ToInt32(rowAus["claseinc"]) != 1)
                                {
                                    Factor = 1;
                                }
                                else
                                {
                                    if (Convert.ToDouble(dsdata.Tables["tblcptos"].Rows[0]["factor"]) > 0)
                                    {
                                        Factor = Convert.ToDecimal(dsdata.Tables["tblcptos"].Rows[0]["factor"]) / 100;
                                    }
                                    else
                                    {
                                        Factor = 1;
                                    }

                                    if (Convert.ToInt32(rowAus["prorroga"]) == 1)
                                    {
                                        DiasIncap = CalculaDiasProrrogaIncap(idempleado, Convert.ToInt32(rowAus["cptoprorroga"]), Convert.ToDouble(rowAus["consecprorroga"]), myconnect, ref DiasIncap);
                                        if ((DiasIncap + Math.Round((double)TmpHoras / 8)) > 90)
                                        {
                                            Factor = (decimal)FactorIncapEmpresa;
                                        }
                                    }
                                }

                                CantHorasLiq = TmpHoras - DiasIncapPagas;

                                if (Convert.ToInt32(rowAus["prorroga"]) == 1)
                                {
                                    CantHorasLiq = TmpHoras;
                                }

                                VlrLiq = Math.Round(((BaseLiqIncap * (double)Factor) / 240) * CantHorasLiq, 0);

                                if (DsDataLiqPro == null)
                                {
                                    if (TipoLiq == "LV")
                                    {
                                        // this.msgconfig.BuscaEmpleado(Idnomina, idempleado, myconnect, dsdata); // ERROR: CS1503
                                        this.GrabaLiquidacionVacaciones(idplanilla, Idnomina, idempleado, CptoIncapCxC, idplanilla, Convert.ToInt32(dsdata.Tables["tblcptos"].Rows[0]["natur"]), CantHorasLiq, 0, VlrLiq, usuario, Convert.ToString(dsdata.Tables["tblempleados"].Rows[0]["idcencos"]), myconnect);
                                    }
                                    else
                                    {
                                        GrabaLiquidacion(idplanilla, Idnomina, idempleado, CptoIncapCxC, Convert.ToDouble(rowAus["Consecutivo"]), 1, CantHorasLiq, 0, VlrLiq, usuario, "", myconnect, TipoReg);
                                    }
                                }
                                else
                                {
                                    DsDataLiqPro.Tables["tblliq"].Rows.Add(CptoIncapCxC, rowAus["Consecutivo"], CantHorasLiq, VlrLiq);
                                }

                                if (Convert.ToInt32(rowAus["basealq"]) == 1)
                                {
                                    SalBaseLiq += VlrLiq;
                                }

                                CantHorasLiq = DiasIncapPagas;
                                if (Convert.ToInt32(rowAus["prorroga"]) == 1)
                                {
                                    CantHorasLiq = 0;
                                }
                            }
                        }
                        else
                        {
                            CantHorasLiq = TmpHoras;
                        }

                        // ok = this.msgconfig.BuscaCptos(Convert.ToInt32(rowAus["idcpto"]), myconnect, dsdata); // ERROR: CS1503

                        if (ok)
                        {
                            if (Convert.ToInt32(rowAus["claseinc"]) != 1)
                            {
                                Factor = 1;
                            }
                            else
                            {
                                if (Convert.ToDouble(dsdata.Tables["tblcptos"].Rows[0]["factor"]) > 0)
                                {
                                    Factor = Convert.ToDecimal(dsdata.Tables["tblcptos"].Rows[0]["factor"]) / 100;
                                }
                                else
                                {
                                    Factor = 1;
                                }
                            }
                        }

                        VlrLiq = Math.Round(((BaseLiqIncap * (double)Factor) / 240) * CantHorasLiq, 0);
                        if (VlrLiq > 0)
                        {
                            if (DsDataLiqPro == null)
                            {
                                if (TipoLiq == "LV")
                                {
                                    // this.msgconfig.BuscaEmpleado(Idnomina, idempleado, myconnect, dsdata); // ERROR: CS1503
                                    this.GrabaLiquidacionVacaciones(idplanilla, Idnomina, idempleado, Convert.ToInt32(rowAus["idcpto"]), idplanilla, Convert.ToInt32(dsdata.Tables["tblcptos"].Rows[0]["natur"]), CantHorasLiq, 0, VlrLiq, usuario, Convert.ToString(dsdata.Tables["tblempleados"].Rows[0]["idcencos"]), myconnect);
                                }
                                else
                                {
                                    GrabaLiquidacion(idplanilla, Idnomina, idempleado, Convert.ToInt32(rowAus["idcpto"]), idplanilla, 1, CantHorasLiq, 0, VlrLiq, usuario, "", myconnect, TipoReg);
                                }
                            }
                            else
                            {
                                DsDataLiqPro.Tables["tblliq"].Rows.Add(rowAus["idcpto"], idplanilla, CantHorasLiq, VlrLiq);
                            }

                            if (Convert.ToInt32(rowAus["basealq"]) == 1)
                            {
                                SalBaseLiq += VlrLiq;
                            }
                        }
                    }

                    Horas += TmpHoras;
                }

                Fila += 1;
            }
            return Horas;
        }

        // Overloads for RevisaAusentIncapacidad (VB Optional params)
        private int RevisaAusentIncapacidad(int idplanilla, int Idnomina, double idempleado, DateTime Fecing, DateTime fecini, DateTime FecFin, string usuario, OdbcConnection myconnect)
        {
            double SalBaseLiq = 0;
            DataSet DsDataLiqPro = null;
            return RevisaAusentIncapacidad(idplanilla, Idnomina, idempleado, Fecing, fecini, FecFin, usuario, myconnect, ref SalBaseLiq, "A", ref DsDataLiqPro, "LP");
        }

        private int RevisaAusentIncapacidad(int idplanilla, int Idnomina, double idempleado, DateTime Fecing, DateTime fecini, DateTime FecFin, string usuario, OdbcConnection myconnect, ref double SalBaseLiq)
        {
            DataSet DsDataLiqPro = null;
            return RevisaAusentIncapacidad(idplanilla, Idnomina, idempleado, Fecing, fecini, FecFin, usuario, myconnect, ref SalBaseLiq, "A", ref DsDataLiqPro, "LP");
        }

        private int RevisaAusentIncapacidad(int idplanilla, int Idnomina, double idempleado, DateTime Fecing, DateTime fecini, DateTime FecFin, string usuario, OdbcConnection myconnect, ref double SalBaseLiq, string TipoReg)
        {
            DataSet DsDataLiqPro = null;
            return RevisaAusentIncapacidad(idplanilla, Idnomina, idempleado, Fecing, fecini, FecFin, usuario, myconnect, ref SalBaseLiq, TipoReg, ref DsDataLiqPro, "LP");
        }

        private int RevisaAusentIncapacidad(int idplanilla, int Idnomina, double idempleado, DateTime Fecing, DateTime fecini, DateTime FecFin, string usuario, OdbcConnection myconnect, ref double SalBaseLiq, string TipoReg, ref DataSet DsDataLiqPro)
        {
            return RevisaAusentIncapacidad(idplanilla, Idnomina, idempleado, Fecing, fecini, FecFin, usuario, myconnect, ref SalBaseLiq, TipoReg, ref DsDataLiqPro, "LP");
        }

        public double LiquidaConcepto(int Idplanilla, int Idnomina, double Salario, int Periodicidad, int IdCpto, int Tiempo, string Usuario, OdbcConnection myconnect, double SalBaseLiq)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsDataCpto = new DataSet();
            decimal VlrHora = 0;
            double VlrDia = 0, VlrLiq = 0;
            decimal BaseLiq = 0;

            // this.msgconfig.BuscaCptos(IdCpto, myconnect, dsDataCpto); // ERROR: CS1503

            DataRow rowCpto = dsDataCpto.Tables["tblcptos"].Rows[0];
            if (Convert.ToString(rowCpto["cdias"]) == "Y")
            {
                if (Tiempo > 0)
                {
                    VlrHora = (decimal)(Salario / 240);
                }
            }

            switch (Convert.ToInt32(rowCpto["clase"]))
            {
                case 1:
                    VlrLiq = Math.Round((double)(VlrHora * Tiempo));
                    break;
                case 7:
                    switch (Convert.ToInt32(rowCpto["base"]))
                    {
                        case 1:
                            BaseLiq = (decimal)Salario;
                            break;
                        case 2:
                            BaseLiq = (decimal)SalBaseLiq;
                            break;
                        case 3:
                            BaseLiq = Convert.ToDecimal(rowCpto["saltope"]);
                            break;
                    }

                    if (Convert.ToDouble(rowCpto["factor"]) != 0)
                    {
                        VlrLiq = (double)(BaseLiq * (Convert.ToDecimal(rowCpto["factor"]) / 100));
                    }

                    switch (Convert.ToInt32(rowCpto["natur"]))
                    {
                        case 2:
                            if (BaseLiq <= Convert.ToDecimal(rowCpto["valor"]) && Convert.ToDouble(rowCpto["valor"]) > 0)
                            {
                                VlrLiq = 0;
                            }
                            break;
                        case 1:
                            if (Convert.ToDouble(rowCpto["valor"]) > 0)
                            {
                                VlrLiq = Convert.ToDouble(rowCpto["valor"]);
                            }
                            break;
                    }

                    if (Salario > Convert.ToDouble(rowCpto["saltope"]) && Convert.ToDouble(rowCpto["saltope"]) > 0)
                    {
                        VlrLiq = 0;
                    }
                    break;
            }

            return VlrLiq;
        }

        public double LiquidaConcepto(int Idplanilla, int Idnomina, double Salario, int Periodicidad, int IdCpto, int Tiempo, string Usuario, OdbcConnection myconnect)
        {
            return LiquidaConcepto(Idplanilla, Idnomina, Salario, Periodicidad, IdCpto, Tiempo, Usuario, myconnect, 0);
        }

        private double LiquidaEps(int Idplanilla, int Idnomina, double Salario, int Periodicidad, int IdCpto, int Tiempo, string Usuario, OdbcConnection myconnect, double SalBaseLiq)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsDataCpto = new DataSet();
            decimal VlrHora = 0;
            double VlrDia = 0, VlrLiq = 0;
            decimal BaseLiq = 0;

            // this.msgconfig.BuscaCptos(IdCpto, myconnect, dsDataCpto); // ERROR: CS1503

            DataRow rowCpto = dsDataCpto.Tables["tblcptos"].Rows[0];
            if (Convert.ToString(rowCpto["cdias"]) == "Y")
            {
                if (Tiempo > 0)
                {
                    VlrHora = (decimal)(Salario / 240);
                }
            }

            switch (Convert.ToInt32(rowCpto["clase"]))
            {
                case 1:
                    VlrLiq = Math.Round((double)(VlrHora * Tiempo));
                    break;
                case 7:
                    switch (Convert.ToInt32(rowCpto["base"]))
                    {
                        case 1:
                            BaseLiq = (decimal)Salario;
                            break;
                        case 2:
                            BaseLiq = (decimal)SalBaseLiq;
                            break;
                        case 3:
                            BaseLiq = Convert.ToDecimal(rowCpto["saltope"]);
                            break;
                    }

                    if (BaseLiq < Convert.ToDecimal(rowCpto["saltope"]) && Convert.ToDouble(rowCpto["saltope"]) > 0)
                    {
                        BaseLiq = Convert.ToDecimal(rowCpto["saltope"]);
                    }

                    if (Convert.ToDouble(rowCpto["factor"]) != 0)
                    {
                        VlrLiq = (double)(BaseLiq * (Convert.ToDecimal(rowCpto["factor"]) / 100));
                    }

                    switch (Convert.ToInt32(rowCpto["natur"]))
                    {
                        case 2:
                            if (BaseLiq <= Convert.ToDecimal(rowCpto["valor"]) && Convert.ToDouble(rowCpto["valor"]) > 0)
                            {
                                VlrLiq = 0;
                            }
                            break;
                        case 1:
                            if (Convert.ToDouble(rowCpto["valor"]) > 0)
                            {
                                VlrLiq = Convert.ToDouble(rowCpto["valor"]);
                            }
                            break;
                    }
                    break;
            }

            return VlrLiq;
        }

        private double LiquidaEps(int Idplanilla, int Idnomina, double Salario, int Periodicidad, int IdCpto, int Tiempo, string Usuario, OdbcConnection myconnect)
        {
            return LiquidaEps(Idplanilla, Idnomina, Salario, Periodicidad, IdCpto, Tiempo, Usuario, myconnect, 0);
        }

        private double LiquidaAuxTrasporte(int idcpto, double Salario, int ClaseSalario, int tiempo, OdbcConnection myconnect)
        {
            DataSet dsDataCpto = new DataSet();
            decimal BaseLiq = 0;
            int Dias = 0;
            double VlrLiq = 0;
            // this.msgconfig.BuscaCptos(idcpto, myconnect, dsDataCpto); // ERROR: CS1503

            Dias = (tiempo / 8);
            DataRow rowCpto = dsDataCpto.Tables["tblcptos"].Rows[0];
            switch (Convert.ToInt32(rowCpto["base"]))
            {
                case 1:
                    BaseLiq = (decimal)Salario;
                    break;
                case 3:
                    BaseLiq = Convert.ToDecimal(rowCpto["saltope"]);
                    break;
            }
            switch (ClaseSalario)
            {
                case 2:
                    VlrLiq = Math.Round((Convert.ToDouble(rowCpto["valor"]) / 240) * tiempo, 0);
                    break;
                case 1:
                    VlrLiq = Math.Round((Convert.ToDouble(rowCpto["valor"]) / 30) * Dias, 0);
                    break;
            }

            if (Salario > Convert.ToDouble(rowCpto["saltope"]) && Convert.ToDouble(rowCpto["saltope"]) > 0)
            {
                VlrLiq = 0;
            }

            return VlrLiq;
        }

        private object LiquidaMovimientos(int idPlanilla, int Idnomina, double idempleado, string Usuario, OdbcConnection myconnect, ref double SalBaseLiq, ref double TotalDevengado)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataMovto = new DataSet();
            int Fila = 0, Sw1 = 0;

            stbuilder.Append("select movto.idplanilla,idnomina,idempleado,movto.IdCpto,movto.consecutivo,Tiempo,movto.Valor,Forpago,cptos.natur,cptos.basealq ");
            stbuilder.Append("from nom_movtos movto inner join nom_cptos cptos on movto.idcpto = cptos.idcpto ");
            stbuilder.Append("where idplanilla = '" + idPlanilla + "' and Idnomina = '" + Idnomina + "' and idempleado = '" + idempleado + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LiquidaMovimientos", DsDataMovto, "tblmovtos");

            while (Fila < DsDataMovto.Tables["tblmovtos"].Rows.Count)
            {
                DataRow rowMov = DsDataMovto.Tables["tblmovtos"].Rows[Fila];

                this.GrabaLiquidacion(idPlanilla, Idnomina, idempleado, Convert.ToInt32(rowMov["idcpto"]), Convert.ToDouble(rowMov["consecutivo"]), Convert.ToInt32(rowMov["natur"]), Convert.ToDouble(rowMov["tiempo"]), 0, Convert.ToDouble(rowMov["valor"]), Usuario, "", myconnect);
                if (Convert.ToInt32(rowMov["natur"]) == 1)
                {
                    TotalDevengado += Convert.ToDouble(rowMov["valor"]);
                }
                if (Convert.ToInt32(rowMov["basealq"]) == 1)
                {
                    SalBaseLiq += Convert.ToDouble(rowMov["valor"]);
                }

                Fila += 1;
            }
            return null;
        }

        private object LiquidaMovimientos(int idPlanilla, int Idnomina, double idempleado, string Usuario, OdbcConnection myconnect)
        {
            double SalBaseLiq = 0;
            double TotalDevengado = 0;
            return LiquidaMovimientos(idPlanilla, Idnomina, idempleado, Usuario, myconnect, ref SalBaseLiq, ref TotalDevengado);
        }

        private object LiquidaLibranzas(int idPlanilla, int Idnomina, double idempleado, int Periodicidad, double SalarioMensual, double TotalDevengado, double SubTrasporte, double SalarioMinimo, int Ciclodsto, DateTime FechaFinal, string Usuario, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataLib = new DataSet();
            double Wreg = 0, VlrCuota = 0;
            int sw1 = 0, natur = 0;
            double SaldoLib = 0;
            DataSet dsDataCpto = new DataSet();

            // this.BuscaLibranzasAsociado(Idnomina, idempleado, myconnect, DsDataLib); // ERROR: CS1620

            while (Wreg < DsDataLib.Tables["tblnovLibranza"].Rows.Count)
            {
                DataRow rowLib = DsDataLib.Tables["tblnovLibranza"].Rows[(int)Wreg];
                sw1 = 0; VlrCuota = 0;
                if (Convert.ToDateTime(rowLib["fechadsto"]) > FechaFinal)
                {
                    sw1 = 1;
                }

                // this.BuscaSaldoLibranzas(Idnomina, idempleado, Convert.ToInt32(rowLib["idcpto"]), Convert.ToDouble(rowLib["consecutivo"]), FechaFinal, myconnect, ref SaldoLib); // ERROR: CS7036
                if (SaldoLib <= 0 && Convert.ToString(rowLib["numcuotas"]) != "9999" && Convert.ToInt32(rowLib["TipoCuota"]) == 1)
                {
                    sw1 = 1;
                }

                if (Convert.ToString(rowLib["numcuotas"]) == "9999" && Convert.ToString(rowLib["estado"]) == "E")
                {
                    sw1 = 1;
                }

                if (sw1 == 0)
                {
                    switch (Convert.ToInt32(rowLib["TipoCuota"]))
                    {
                        case 0:
                            switch (Convert.ToInt32(rowLib["baseliq"]))
                            {
                                case 0:
                                    VlrCuota = Math.Round(SalarioMensual * (Convert.ToDouble(rowLib["TasaInt"]) / 100), 0);
                                    break;
                                case 1:
                                    VlrCuota = Math.Round(TotalDevengado * (Convert.ToDouble(rowLib["TasaInt"]) / 100), 0);
                                    break;
                                case 2:
                                    VlrCuota = Math.Round((TotalDevengado - SubTrasporte) * (Convert.ToDouble(rowLib["TasaInt"]) / 100), 0);
                                    break;
                                case 3:
                                    VlrCuota = Math.Round((TotalDevengado - SalarioMinimo) * (Convert.ToDouble(rowLib["TasaInt"]) / 100), 0);
                                    break;
                            }
                            break;
                        case 1:
                            VlrCuota = Convert.ToDouble(rowLib["Cuota"]);
                            break;
                    }

                    switch (Convert.ToInt32(rowLib["ciclodsto"]))
                    {
                        case 1:
                            if (Ciclodsto != 1)
                            {
                                VlrCuota = 0;
                            }
                            break;
                        case 2:
                            if (Ciclodsto != 2)
                            {
                                VlrCuota = 0;
                            }
                            break;
                    }

                    if (VlrCuota > 0)
                    {
                        // this.msgconfig.BuscaCptos(Convert.ToInt32(rowLib["idcpto"]), myconnect, dsDataCpto); // ERROR: CS1503
                        natur = Convert.ToInt32(dsDataCpto.Tables["tblcptos"].Rows[0]["natur"]);

                        GrabaLiquidacion(idPlanilla, Idnomina, idempleado, Convert.ToInt32(rowLib["idcpto"]), Convert.ToDouble(rowLib["consecutivo"]), natur, 0, 0, VlrCuota, Usuario, "", myconnect);
                    }
                }

                Wreg += 1;
            }
            return null;
        }

        private object LiquidaDeduccionesAutomaticas(int Idplanilla, int Idnomina, double idempleado, double Salario, double SalBaseLiq, int Periodicidad, string Usuario, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataAut = new DataSet();
            double Wreg = 0;
            double VlrLiq = 0;
            int sw1 = 0;

            stbuilder.Append("select idcpto,nombre,nomres,clase,natur,valor,factor,base,salario,cdias,extie,exval,basealq,saltope,lineacer,columna,");
            stbuilder.Append("afeprest,AFERETFTE,ESPREST,MSALDO,PRIOR ");
            stbuilder.Append("where natur = 2 and clase  = 7 and base = 1");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LiquidaDeduccionesAutomaticas", DsDataAut, "tblCptosAut");
            while (Wreg < DsDataAut.Tables["tblCptosAut"].Rows.Count)
            {
                DataRow rowAut = DsDataAut.Tables["tblCptosAut"].Rows[(int)Wreg];

                VlrLiq = this.LiquidaConcepto(Idplanilla, Idnomina, Salario, Periodicidad, Convert.ToInt32(rowAut["idcpto"]), 0, Usuario, myconnect, SalBaseLiq);
                if (VlrLiq > 0)
                {
                    // Commented out in VB: GrabaLiquidacion(...)
                }

                Wreg += 1;
            }
            return null;
        }

        public void GrabaLiquidacion(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, int natur, double tiempo, int dias, double valor, string Usuario, string cencos, OdbcConnection myconnect, string tiporeg)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            string ConvDias = "N";
            ok = BuscaLiquidacion(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect);
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
                        if (ConvDias == "Y")
                        {
                            dias = (int)Math.Round(tiempo / 8, 0);
                        }
                        else
                        {
                            dias = (int)tiempo;
                        }
                    }

                    stbuilder.Append("insert into nom_liqplan (");
                    stbuilder.Append("idplanilla, Idnomina,idempleado,idcpto,consecutivo,natur,dia,tiempo,Valor,cencos,usuario,fechasys,tiporeg) ");
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
                    stbuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "','");
                    stbuilder.Append(tiporeg + "')");
                    break;
                case true:
                    stbuilder.Append("update nom_liqplan set ");
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
                    stbuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "' ");
                    stbuilder.Append("where idplanilla = '" + Idplanilla + "' and Idnomina ='" + Idnomina + "' and idempleado ='" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");
                    break;
            }
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaLiquidacion");
        }

        public void GrabaLiquidacion(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, int natur, double tiempo, int dias, double valor, string Usuario, string cencos, OdbcConnection myconnect)
        {
            GrabaLiquidacion(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, natur, tiempo, dias, valor, Usuario, cencos, myconnect, "A");
        }

        // GrabaLiquidacion overload accepting object params (for calls passing DataRow items directly)
        public void GrabaLiquidacion(int Idplanilla, int Idnomina, object idempleado, int Idpcto, double Consecutivo, object natur, object tiempo, int dias, object valor, string Usuario, string cencos, OdbcConnection myconnect)
        {
            GrabaLiquidacion(Idplanilla, Idnomina, Convert.ToDouble(idempleado), Idpcto, Consecutivo, Convert.ToInt32(natur), Convert.ToDouble(tiempo), dias, Convert.ToDouble(valor), Usuario, cencos, myconnect, "A");
        }

        public bool BuscaLiquidacion(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();
            try
            {
                DsDataset.Tables.Remove("tblliqplan");
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select idplanilla,idempleado,idcpto,consecutivo,tiempo,valor,forpago,usuario ");
            stbuilder.Append("from nom_liqplan where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "' and idempleado = '" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LiquidaDevengos", DsDatosLiq, "tblliqplan");
            if (DsDatosLiq.Tables["tblliqplan"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDatosLiq.Tables["tblliqplan"].Copy());
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

        public bool BuscaLiquidacion(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect)
        {
            DataSet DsDataset = null;
            return BuscaLiquidacion(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect, ref DsDataset);
        }

        private void EliminaLiquidacion(int Idplanilla, int Idnomina, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            stbuilder.Append("delete from nom_liqplan where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaLiquidacion");
        }

        public void GrabaMovimiento(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, double tiempo, double valor, int formaPago, string Usuario, OdbcConnection myconnect, string Detalle)
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = this.BuscaMovimiento(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect);
            switch (ok)
            {
                case false:
                    stbuilder.Append("insert into nom_movtos (");
                    stbuilder.Append("idplanilla, Idnomina,idempleado,idcpto,consecutivo,tiempo,Valor,Forpago,Detalle,usuario,fechasys) ");
                    stbuilder.Append("values ('");
                    stbuilder.Append(Idplanilla + "','");
                    stbuilder.Append(Idnomina + "','");
                    stbuilder.Append(idempleado + "','");
                    stbuilder.Append(Idpcto + "','");
                    stbuilder.Append(Consecutivo + "','");
                    stbuilder.Append(tiempo + "','");
                    stbuilder.Append(valor + "','");
                    stbuilder.Append(formaPago + "','");
                    stbuilder.Append(Detalle + "','");
                    stbuilder.Append(Usuario + "','");
                    stbuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "')");
                    break;
                case true:
                    stbuilder.Append("update nom_movtos set ");
                    stbuilder.Append("tiempo = '");
                    stbuilder.Append(tiempo + "',");
                    stbuilder.Append("Valor = '");
                    stbuilder.Append(valor + "',");
                    stbuilder.Append("Forpago = '");
                    stbuilder.Append(formaPago + "',");
                    stbuilder.Append("Detalle = '");
                    stbuilder.Append(Detalle + "',");
                    stbuilder.Append("usuario = '");
                    stbuilder.Append(Usuario + "',");
                    stbuilder.Append("fechasys = '");
                    stbuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "' ");
                    stbuilder.Append("where idplanilla = '" + Idplanilla + "' and Idnomina ='" + Idnomina + "' and idempleado ='" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");
                    break;
            }
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaLiquidacion");
        }

        public void GrabaMovimiento(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, double tiempo, double valor, int formaPago, string Usuario, OdbcConnection myconnect)
        {
            GrabaMovimiento(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, tiempo, valor, formaPago, Usuario, myconnect, " ");
        }

        // GrabaMovimiento overload accepting object/string params
        public void GrabaMovimiento(int Idplanilla, int Idnomina, object idempleado, object Idpcto, double Consecutivo, object tiempo, object valor, int formaPago, string Usuario, OdbcConnection myconnect)
        {
            GrabaMovimiento(Idplanilla, Idnomina, Convert.ToDouble(idempleado), Convert.ToInt32(Idpcto), Consecutivo, Convert.ToDouble(tiempo), Convert.ToDouble(valor), formaPago, Usuario, myconnect, " ");
        }

        public bool BuscaMovimiento(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();
            try
            {
                DsDataset.Tables.Remove("tblmovtos");
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select idplanilla,idempleado,idcpto,consecutivo,tiempo,valor,forpago,usuario,detalle ");
            stbuilder.Append("from nom_movtos where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "' and idempleado = '" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LiquidaDevengos", DsDatosLiq, "tblmovtos");
            if (DsDatosLiq.Tables["tblmovtos"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDatosLiq.Tables["tblmovtos"].Copy());
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

        public bool BuscaMovimiento(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect)
        {
            DataSet DsDataset = null;
            return BuscaMovimiento(Idplanilla, Idnomina, idempleado, Idpcto, Consecutivo, myconnect, ref DsDataset);
        }

        public void EliminaMovimiento(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            stbuilder.Append("delete from nom_movtos where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "' and idempleado = '" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaLiquidacion");
        }

        public bool CargaArchivoPlano(string NombreArchivo, int Idplanilla, int idnomina, System.Windows.Forms.Form Myforma, string usuario, bool ValArchivo, OdbcConnection myconnect)
        {
            StreamReader strStreamReader = null;
            string line = "";
            double TotReg = 0;
            int sw1 = 0;
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Cargando Informacion plano " + NombreArchivo, Myforma);
            string Idempleado = "", IdCpto = "", Valor = "", Tiempo = "", Dectiempo = "", Natur = "0";
            double Salario = 0;
            decimal TasaLiq = 0;
            int cedula = 0;
            string ststring = " ";
            double ValLiq = 0;
            DataSet Dsdataset = new DataSet();

            Dsdataset.Tables.Add("tblvalplano");
            Dsdataset.Tables["tblvalplano"].Columns.Add("idempleado", ststring.GetType());
            Dsdataset.Tables["tblvalplano"].Columns.Add("nomempleado", ststring.GetType());
            Dsdataset.Tables["tblvalplano"].Columns.Add("idcpto", ststring.GetType());
            Dsdataset.Tables["tblvalplano"].Columns.Add("idnomcpto", ststring.GetType());
            Dsdataset.Tables["tblvalplano"].Columns.Add("Valor", ststring.GetType());
            Dsdataset.Tables["tblvalplano"].Columns.Add("tiempo", ststring.GetType());

            Dsdataset.Tables.Add("tblerrores");
            Dsdataset.Tables["tblerrores"].Columns.Add("idcodigo", ststring.GetType());
            Dsdataset.Tables["tblerrores"].Columns.Add("descripcion", ststring.GetType());

            strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();
            strStreamReader.Close();

            TotReg = Math.Round((double)(FileSystem.FileLen(NombreArchivo) / (Strings.Len(line) + 2)));
            BarraProgreso.ValorMinimoMaximo(0, (int)TotReg);
            BarraProgreso.Show();

            FileSystem.FileOpen(1, NombreArchivo, OpenMode.Input);

            while (FileSystem.EOF(1) == false)
            {
                sw1 = 0;
                line = FileSystem.LineInput(1);
                Idempleado = Strings.Mid(line, 1, 10);
                IdCpto = Strings.Mid(line, 11, 3);
                Valor = Strings.Mid(line, 14, 10);
                Tiempo = Strings.Mid(line, 24, 3);
                Dectiempo = Strings.Mid(line, 27, 2);

                if (!Information.IsNumeric(Valor))
                {
                    Valor = "0";
                }

                if (!Information.IsNumeric(Tiempo))
                {
                    Tiempo = "0";
                }

                ok = this.msgconfig.BuscaCptos(IdCpto, myconnect, Dsdataset);
                switch (ok)
                {
                    case false:
                        Dsdataset.Tables["tblerrores"].Rows.Add(IdCpto, "Concepto no esta creado");
                        sw1 = 1;
                        break;
                    case true:
                        Natur = Convert.ToString(Dsdataset.Tables["tblcptos"].Rows[0]["natur"]);
                        TasaLiq = Convert.ToDecimal(Dsdataset.Tables["tblcptos"].Rows[0]["factor"]);
                        break;
                }

                // ok = this.msgconfig.BuscaEmpleado(idnomina, Idempleado, myconnect, Dsdataset); // ERROR: CS1503
                if (!ok)
                {
                    cedula = Convert.ToInt32(Idempleado);
                    // ok = this.msgconfig.BuscaEmpleadoCedula(cedula, myconnect, Dsdataset); // ERROR: CS1503
                    if (ok)
                    {
                        Idempleado = Convert.ToString(Dsdataset.Tables["tblempleados"].Rows[0]["idempleado"]);
                    }
                    else
                    {
                        Dsdataset.Tables["tblerrores"].Rows.Add(Idempleado, "Cedula no esta creada");
                        sw1 = 1;
                    }
                }

                if (sw1 == 0)
                {
                    if (Convert.ToDouble(Valor) == 0)
                    {
                        Salario = Convert.ToDouble(Dsdataset.Tables["tblempleados"].Rows[0]["salario"]);
                        ValLiq = Math.Round(((Salario / 240) * Convert.ToDouble(Tiempo)) * (double)TasaLiq, 0);
                    }
                    else
                    {
                        ValLiq = Convert.ToDouble(Valor);
                    }
                }

                if (sw1 == 0 && ValLiq > 0 && ValArchivo == false)
                {
                    this.GrabaMovimiento(Idplanilla, idnomina, Convert.ToDouble(Idempleado), Convert.ToInt32(IdCpto), 0, Convert.ToDouble(Tiempo), ValLiq, 1, usuario, myconnect);
                    Dsdataset.Tables["tblvalplano"].Rows.Add(Idempleado, Convert.ToString(Dsdataset.Tables["tblempleados"].Rows[0]["apellidos"]) + " " + Convert.ToString(Dsdataset.Tables["tblempleados"].Rows[0]["nombres"]), IdCpto, Convert.ToString(Dsdataset.Tables["tblcptos"].Rows[0]["nombre"]), ValLiq, Tiempo);
                }

                BarraProgreso.PerformStep();
            }

            FileSystem.FileClose(1);
            BarraProgreso.Close();
            BarraProgreso.Dispose();

            if (Dsdataset.Tables["tblerrores"].Rows.Count > 0)
            {
                if (ValArchivo == true)
                {
                    this.msgimp.ImprimeErrores(Idplanilla, idnomina, Dsdataset.Tables["tblerrores"], Myforma, myconnect);
                    return false;
                }
                else
                {
                    this.msgimp.ImprimedatosImpo(Idplanilla, idnomina, Dsdataset.Tables["tblvalplano"], Myforma, myconnect);
                }
            }
            else
            {
                this.msgimp.ImprimedatosImpo(Idplanilla, idnomina, Dsdataset.Tables["tblvalplano"], Myforma, myconnect);
                return true;
            }

            return false; // VB implicit return
        }

        public bool EliminaLiquidacion(int Idplanilla, int Idnomina, double idempleado, int Idpcto, double Consecutivo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            stbuilder.Append("delete from nom_liqplan where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "' and idempleado = '" + idempleado + "' and idcpto = '" + Idpcto + "' and consecutivo = '" + Consecutivo + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaLiquidacion");
            return false; // VB implicit return
        }
    }
}
