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
        // EliminaLiquidacion duplicado eliminado (original en msgnom.Part3.cs:2102)

        // ── OrganizaDatosContabilizacion (line 3512) ────────────────────────
        public void OrganizaDatosContabilizacion(int idplanilla, int idnomina,
            string usuario, Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            double fila = 0;
            int sw1 = 0;
            DataSet DsDataset = new DataSet();
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            double debito = 0, credito = 0;
            string cuenta, nit;
            string aplter = "N", aplcencos = "N", tipoaux = "0";
            int CptoIncCxc = 0;
            int ForActCont = 0;
            string NitEmp = "";
            int TrasTer = 0;
            string Numdoc = " ", Tipodoc = " ";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Organiza datos de la planilla : " + idplanilla, myforma);

            EliminaTemporal(idplanilla.ToString(), idnomina, myconnect);

            stbuilder.Append("select liqplan.idplanilla,liqplan.idnomina,liqplan.idempleado,liqplan.IdCpto,liqplan.consecutivo,liqplan.natur,liqplan.dia,liqplan.Tiempo,liqplan.Valor,liqplan.Forpago,liqplan.Cencos, ");
            stbuilder.Append("empl.idcencos,empl.Cedula,ctas.ctagasto,ctas.ctacontra,ctas.ctaprov,cptos.nit cptonit ");
            stbuilder.Append("from nom_liqplan liqplan ");
            stbuilder.Append("inner join nom_empleados empl on liqplan.idnomina = empl.idnomina and liqplan.idempleado = empl.idempleado ");
            stbuilder.Append("inner join nom_cptos cptos on liqplan.idcpto = cptos.idcpto ");
            stbuilder.Append("left join nom_cuentas ctas on liqplan.idcpto = ctas.idcpto and empl.idcencos = ctas.idcencos ");
            stbuilder.Append("where liqplan.idplanilla = '" + idplanilla + "' and liqplan.Idnomina = '" + idnomina + "' and liqplan.tiporeg not in ('V','T') ");
            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "OrganizaDatosContabilizacion", ref DsDataset, "tblliqplan");

            // ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, ref DsDataset); // ERROR: CS1615
            switch (ok)
            {
                case false:
                    MessageBox.Show("Parametros de la nomina no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                case true:
                    DataRow rowEmp = DsDataset.Tables["tblempresas"].Rows[0];
                    ForActCont = Convert.ToInt32(rowEmp["ForActCont"]);
                    NitEmp = rowEmp["nit"].ToString();
                    TrasTer = Convert.ToInt32(rowEmp["TrasTer"]);
                    CptoIncCxc = Convert.ToInt32(rowEmp["cptoincapcxc"]);
                    break;
            }

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblliqplan"].Rows.Count);
            msgbarra.Show();

            // Dummy ref variables for BuscarCuenta skipped params
            string _natura = "0", _cencos = "0", _nivel = "0", _aplicart = "0";
            string _apliTes = "0", _nombre = "0";
            decimal _tasa = 0;
            int _estado = 0;
            string _apliCnt = "0", _consiBaca = "0", _bancoConsiBanca = "0";

            while (fila < DsDataset.Tables["tblliqplan"].Rows.Count)
            {
                DataRow row = DsDataset.Tables["tblliqplan"].Rows[(int)fila];
                sw1 = 0; debito = 0; credito = 0;

                if (row["ctaprov"] is DBNull)
                {
                    MessageBox.Show("concepto no esta parametrizado cpto : " + row["idcpto"] + "centro costo : " + row["idcencos"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    sw1 = 1;
                }

                if (sw1 == 0)
                {
                    if (row["ctaprov"].ToString() != "999999999999")
                    {
                        cuenta = row["ctaprov"].ToString();
                    }
                    else
                    {
                        cuenta = row["ctagasto"].ToString();
                    }

                    // BuscarCuenta: p3=aplter(Tercero), p4=aplcencos(mane_cencos), p5-p10 skip, p11=skip(Tasa), p12=tipoaux(TipoAuxiliar)
                    msgcnt.BuscarCuenta(ref cuenta, myconnect,
                        ref aplter, ref aplcencos, ref _natura, ref _cencos,
                        ref _nivel, ref _aplicart, ref _apliTes, ref _nombre,
                        ref _tasa, ref tipoaux, ref _estado,
                        ref _apliCnt, ref _consiBaca, ref _bancoConsiBanca);

                    nit = RevisaNit(aplter, ForActCont, NitEmp, row["cptonit"].ToString(), TrasTer, row["cedula"].ToString(), row["idempleado"].ToString());

                    int natur = Convert.ToInt32(row["natur"]);
                    double valor = Convert.ToDouble(row["valor"]);

                    switch (natur)
                    {
                        case 1:
                            if (valor >= 0)
                                debito = valor;
                            else
                                credito = valor * -1;
                            break;
                        case 2:
                        case 0:
                            if (valor >= 0)
                                credito = valor;
                            else
                                debito = valor * -1;
                            break;
                    }

                    if (Convert.ToInt32(tipoaux) > 0)
                    {
                        Tipodoc = "PL";
                        Numdoc = idplanilla.ToString();
                    }

                    GrabaTemporal(idplanilla.ToString(), idnomina, Convert.ToInt32(row["idcpto"]), Convert.ToDouble(row["consecutivo"]), row["idcencos"].ToString(), cuenta, nit, Tipodoc, Numdoc, debito, credito, Convert.ToDouble(row["idempleado"]), myconnect);

                    debito = 0; credito = 0; Tipodoc = " "; Numdoc = " ";

                    string ctacontra = row["ctacontra"].ToString();
                    msgcnt.BuscarCuenta(ref ctacontra, myconnect,
                        ref aplter, ref aplcencos, ref _natura, ref _cencos,
                        ref _nivel, ref _aplicart, ref _apliTes, ref _nombre,
                        ref _tasa, ref tipoaux, ref _estado,
                        ref _apliCnt, ref _consiBaca, ref _bancoConsiBanca);

                    nit = RevisaNit(aplter, ForActCont, NitEmp, "0", TrasTer, row["cedula"].ToString(), row["idempleado"].ToString());

                    // Commented out in VB:
                    // Select Case .Item("IdCpto")
                    //     Case CptoIncCxc
                    //         .Item("natur") = 2
                    // End Select

                    switch (natur)
                    {
                        case 1:
                            if (valor >= 0)
                                credito = valor;
                            else
                                debito = valor * -1;
                            break;
                        case 2:
                            if (valor >= 0)
                                debito = valor;
                            else
                                credito = valor * -1;
                            break;
                        case 0:
                            debito = 0;
                            credito = 0;
                            break;
                    }

                    if (Convert.ToInt32(tipoaux) > 0)
                    {
                        Tipodoc = "PL";
                        Numdoc = idplanilla.ToString();
                    }

                    if (debito > 0 || credito > 0)
                    {
                        GrabaTemporal(idplanilla.ToString(), idnomina, 0, 0, row["idcencos"].ToString(), ctacontra, nit, Tipodoc, Numdoc, debito, credito, Convert.ToDouble(row["idempleado"]), myconnect);
                    }
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            msgbarra.Close();
            msgbarra.Dispose();
            IncluyeProvision(idplanilla, idnomina, usuario, myforma, myconnect);
        }

        // ── IncluyeProvision (line 3645) ────────────────────────────────────
        public void IncluyeProvision(int idplanilla, int idnomina, string usuario,
            Form myforma, OdbcConnection myconnect,
            string OpcionGeneradora = "", string IdEmpleadoVac = "")
        {
            StringBuilder stbuilder = new StringBuilder();
            double fila = 0;
            int sw1 = 0, IdSalIntegral;
            double Baseprov, ValLiq = 0;
            int idTransaporte = 0;
            DataSet DsDataset = new DataSet();
            string cuentaprov, cuentaGasto, nit, IdAprSena;
            double ValSalMinimo;
            int ForActCont = 0;
            string NitEmp = "";
            int TrasTer = 0, IdAdmArp;
            bool AprSena;
            int IdVaca;
            string Detalle;
            StringBuilder StbuilderCpto = new StringBuilder();
            int Periodicidad = 0;
            bool SalIntegral;
            double IdCptoICBF = 0, IdCptoSena = 0, IdCptoCCF = 0, IdCptoPrima = 0, IdCptoMesada = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Revisando provisiones ,Por favor espere", myforma);

            // ok = this.msgconfig.BuscaPeriodosPagos(idplanilla, idnomina, myconnect, ref DsDataset); // ERROR: CS1503, CS1615
            switch (ok)
            {
                case true:
                    Periodicidad = Convert.ToInt32(DsDataset.Tables["tblperpagos"].Rows[0]["Periodicidad"]);
                    break;
            }

            // ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, ref DsDataset); // ERROR: CS1615
            switch (ok)
            {
                case false:
                    MessageBox.Show("Parametros de la nomina no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                case true:
                    DataRow rEmp = DsDataset.Tables["tblempresas"].Rows[0];
                    ForActCont = Convert.ToInt32(rEmp["ForActCont"]);
                    NitEmp = rEmp["nit"].ToString();
                    TrasTer = Convert.ToInt32(rEmp["TrasTer"]);
                    break;
            }

            StbuilderCpto.Append("Select salario,clase,tasaprov,BaseProv,basevac,nit,idcpto,natur from nom_cptos where tasaprov > 0 ");
            this.msgodbc.ExecuteQueryDataset(StbuilderCpto.ToString(), myconnect, "ContabilizaPlanilla", ref DsDataset, "tblcptos");

            stbuilder.Append("select contpla.idplanilla,contpla.idcpto,contpla.consecutivo,contpla.idcencos,contpla.cuenta,contpla.nit,contpla.Tipodoc,contpla.numdoc,contpla.debito,contpla.credito,cptos.tasaprov,cptos.BaseProv,cptos.clase, ");
            stbuilder.Append("cptos.salario,cptos.basevac,ctas.ctagasto,ctas.ctacontra,ctas.ctaprov,cptos.nit as cptonit,contpla.idempleado ");
            stbuilder.Append("from nom_contpla contpla ");
            stbuilder.Append("inner join nom_cptos cptos on contpla.idcpto = cptos.idcpto ");
            stbuilder.Append("left join nom_cuentas ctas on contpla.idcpto = ctas.idcpto and contpla.idcencos = ctas.idcencos ");
            stbuilder.Append("where contpla.idplanilla = '" + idplanilla + "' and contpla.Idnomina = '" + idnomina + "'");
            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ContabilizaPlanilla", ref DsDataset, "tblprov");

            // ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, ref DsDataset); // ERROR: CS1615
            switch (ok)
            {
                case false:
                    MessageBox.Show("Parametros de la nomina no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                case true:
                    DataRow rEmp2 = DsDataset.Tables["tblempresas"].Rows[0];
                    Detalle = rEmp2["nomres"].ToString();
                    IdSalIntegral = Convert.ToInt32(rEmp2["IdSalIntegral"]);
                    idTransaporte = Convert.ToInt32(rEmp2["IdTrasporte"]);
                    IdAprSena = rEmp2["IdAprSena"].ToString();
                    IdAdmArp = Convert.ToInt32(rEmp2["IdAdmArp"]);
                    ValSalMinimo = Convert.ToDouble(rEmp2["valsalminimo"]);
                    IdVaca = Convert.ToInt32(rEmp2["IdVacasiones"]);
                    IdCptoICBF = Convert.ToDouble(rEmp2["IdIcbf"]);
                    IdCptoSena = Convert.ToDouble(rEmp2["IdSena"]);
                    IdCptoPrima = Convert.ToDouble(rEmp2["IdPrimaServ"]);
                    IdCptoMesada = Convert.ToDouble(rEmp2["IdPrimaServ2"]);
                    break;
            }

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblprov"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataset.Tables["tblprov"].Rows.Count)
            {
                sw1 = 0;
                DataRow row = DsDataset.Tables["tblprov"].Rows[(int)fila];
                ValLiq = 0;
                cuentaGasto = row["ctagasto"].ToString();
                cuentaprov = row["ctaprov"].ToString();
                Baseprov = 0;

                if (Convert.ToInt32(row["idcpto"]) == IdSalIntegral)
                {
                    SalIntegral = true;
                    Baseprov = Convert.ToDouble(row["debito"]);
                }
                else
                {
                    SalIntegral = false;
                    Baseprov = Convert.ToDouble(row["debito"]);
                }

                if (IdAprSena == row["idcpto"].ToString())
                {
                    Baseprov = Convert.ToDouble(row["debito"]);
                    AprSena = true;
                }
                else
                {
                    AprSena = false;
                }

                CalculaProvision(DsDataset.Tables["tblcptos"], Convert.ToDouble(row["consecutivo"]),
                    row["idcencos"].ToString(), row["nit"].ToString(), idplanilla, idnomina,
                    Convert.ToInt32(row["idcpto"]), Baseprov, Convert.ToDouble(row["credito"]),
                    idTransaporte, Convert.ToInt32(row["salario"]), ForActCont, NitEmp,
                    AprSena, IdAdmArp, ValSalMinimo, SalIntegral, IdVaca,
                    Convert.ToInt32(IdCptoSena), Convert.ToInt32(IdCptoICBF),
                    Convert.ToInt32(IdCptoPrima), Convert.ToInt32(IdCptoMesada),
                    myconnect, OpcionGeneradora, row["idempleado"].ToString());

                fila += 1;
                msgbarra.PerformStep();
            }

            msgbarra.Close();
            msgbarra.Dispose();
        }

        // ── CalculaProvision (line 3754) ────────────────────────────────────
        private void CalculaProvision(DataTable DsdataTable, double Consecutivo,
            string Idcencos, string Nit, int idplanilla, int idnomina,
            int IdCpto, double BaseProv, double Credito, int idTransaporte,
            int CptoSalario, int ForActCont, string NitEmp, bool Aprsena,
            int IdAdmArp, double Salminimo, bool SalarioIntegral, int Idvaca,
            int IdSena, int IdICBF, int IdPrima, int IdMesada,
            OdbcConnection myconnect, string OpcionGenerador = "", string IdEmpleadoVac = "")
        {
            double ValLiq = 0;
            int fila = 0, TrasTer = 0;
            DataSet dsDataSet = new DataSet();
            string cuentaGasto, cuentaprov, Tipodoc = " ", Numdoc = " ";
            int clasesalario = 0;
            string aplter = "N", aplcencos = "N", tipoaux = "0";
            int Contrato;
            DataSet dsdata = new DataSet();
            double BaseLiq = 0;
            string nitempleado = "", nitprov = " ", CptoARPEmp = "00";
            int sw1 = 0;
            double CptoCCF = 0;
            int TipEmpleado = 0;
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            DataSet dsempresa = new DataSet();
            string CptoIntCesant = "00", CptoCesant = "00", CptoProvVaca = "00";
            int IdCptoEps = 0, IdCptoPension = 0, IdCptoPrimExtra = 0;
            string PrimaExtra = "N";
            double PorArp = 0;
            DataSet dsdatacpto = new DataSet();

            // Dummy ref variables for BuscarCuenta skipped params
            string _natura = "0", _cencos = "0", _nivel = "0", _aplicart = "0";
            string _apliTes = "0", _nombre = "0";
            decimal _tasa = 0;
            int _estado = 0;
            string _apliCnt = "0", _consiBaca = "0", _bancoConsiBanca = "0";

            // Commented out in VB:
            // Select Case OpcionGenerador
            //     Case "", "LT"
            //         nitempleado = Nit
            //     Case "VC"
            //         nitempleado = IdEmpleadoVac
            // End Select

            nitempleado = IdEmpleadoVac;

            if (nitempleado.Trim() != "")
            {
                // ok = this.msgconfig.BuscaEmpleado(idnomina, nitempleado, myconnect, ref dsDataSet); // ERROR: CS1503, CS1615
                switch (ok)
                {
                    case true:
                        DataRow rowEmp = dsDataSet.Tables["tblempleados"].Rows[0];
                        clasesalario = Convert.ToInt32(rowEmp["clasesalario"]);
                        CptoARPEmp = rowEmp["IdArp"].ToString();
                        CptoCCF = Convert.ToDouble(rowEmp["IdSubsFam"]);
                        TipEmpleado = Convert.ToInt32(rowEmp["TipEmpleado"]);
                        IdCptoEps = Convert.ToInt32(rowEmp["IdEps"]);
                        IdCptoPension = Convert.ToInt32(rowEmp["Idpension"]);
                        PrimaExtra = rowEmp["primextra"].ToString();
                        nitempleado = Nit;
                        break;
                }
            }

            // this.msgconfig.BuscaEmpresa(idnomina, myconnect, ref dsempresa); // ERROR: CS1615

            CptoIntCesant = dsempresa.Tables["tblempresas"].Rows[0]["IdIntCesantias"].ToString();
            CptoProvVaca = dsempresa.Tables["tblempresas"].Rows[0]["IdVacasiones"].ToString();
            CptoCesant = dsempresa.Tables["tblempresas"].Rows[0]["IdCesantias"].ToString();
            IdCptoPrimExtra = Convert.ToInt32(dsempresa.Tables["tblempresas"].Rows[0]["IDPRIMASERV1"]);

            if (SalarioIntegral == true)
            {
                BaseLiq = BaseProv;
                BaseProv = Math.Round(BaseProv * 0.7);
            }

            // this.msgconfig.BuscaCptos(IdCpto, myconnect, ref dsdata); // ERROR: CS1503, CS1615

            while (fila < DsdataTable.Rows.Count)
            {
                DataRow row = DsdataTable.Rows[fila];
                ValLiq = 0; sw1 = 0;
                Nit = nitempleado;

                // this.msgconfig.BuscaCptos(Convert.ToInt32(row["idcpto"]), myconnect, ref dsdatacpto); // ERROR: CS1503, CS1615

                int clasecpto = Convert.ToInt32(dsdatacpto.Tables["tblcptos"].Rows[0]["clasecpto"]);

                switch (clasecpto)
                {
                    case 1:
                        if (Convert.ToInt32(row["idcpto"]) != IdCptoEps)
                            sw1 = 1;
                        break;
                    case 2:
                        if (Convert.ToInt32(row["idcpto"]) != IdCptoPension)
                            sw1 = 1;
                        break;
                    case 3:
                        if (row["idcpto"].ToString() != CptoCesant)
                            sw1 = 1;
                        break;
                    case 4:
                        if (row["idcpto"].ToString() != CptoIntCesant)
                            sw1 = 1;
                        break;
                    case 5:
                        if (Convert.ToInt32(row["idcpto"]) != IdPrima)
                        {
                            if (IdCptoPrimExtra == Convert.ToInt32(row["idcpto"]))
                            {
                                if (PrimaExtra == "N")
                                    sw1 = 1;
                            }
                            else
                            {
                                sw1 = 1;
                            }
                        }
                        break;
                    case 6:
                        if (row["idcpto"].ToString() != CptoProvVaca)
                            sw1 = 1;
                        break;
                    case 7:
                        if (Convert.ToDouble(row["idcpto"]) != CptoCCF)
                            sw1 = 1;
                        break;
                    case 8:
                        if (Convert.ToInt32(row["idcpto"]) != IdSena)
                            sw1 = 1;
                        break;
                    case 9:
                        if (Convert.ToInt32(row["idcpto"]) != IdICBF)
                            sw1 = 1;
                        break;
                    case 10:
                        if (row["idcpto"].ToString() != CptoARPEmp)
                        {
                            sw1 = 1;
                        }
                        else
                        {
                            // PorArp = this.msgconfig.BuscaTarifaArpEmpleado(idnomina, IdEmpleadoVac, myconnect); // ERROR: CS1061
                            if (PorArp != 0)
                            {
                                row["tasaprov"] = PorArp;
                            }
                        }
                        break;
                }

                if (sw1 == 0)
                {
                    switch (OpcionGenerador)
                    {
                        case "":
                        case "LT":
                            int rowIdCpto = Convert.ToInt32(row["idcpto"]);
                            if (rowIdCpto == CptoCCF || rowIdCpto == IdSena || rowIdCpto == IdICBF)
                            {
                                DataRow rowDsdata = dsdata.Tables["tblcptos"].Rows[0];
                                if (Convert.ToInt32(rowDsdata["basealq"]) == 0)
                                    sw1 = 1;
                                if (TipEmpleado == 2 || TipEmpleado == 3)
                                    sw1 = 1;
                                if (OpcionGenerador == "LT")
                                {
                                    string clase = rowDsdata["clase"].ToString();
                                    if (clase == "2" || clase == "4")
                                        sw1 = 1;
                                }
                            }
                            else if (rowIdCpto.ToString() == CptoARPEmp)
                            {
                                DataRow rowDsdata = dsdata.Tables["tblcptos"].Rows[0];
                                string clase = rowDsdata["clase"].ToString();
                                if (clase == "2" || clase == "4")
                                    sw1 = 1;
                                if (Convert.ToInt32(rowDsdata["basealq"]) == 0)
                                    sw1 = 1;
                                if (TipEmpleado == 2 || TipEmpleado == 3)
                                    sw1 = 1;
                            }
                            else if (rowIdCpto.ToString() == CptoIntCesant || rowIdCpto.ToString() == CptoProvVaca
                                || rowIdCpto.ToString() == CptoCesant || rowIdCpto == IdPrima || rowIdCpto == IdMesada)
                            {
                                if (TipEmpleado == 2 || TipEmpleado == 3)
                                {
                                    if (rowIdCpto != IdMesada)
                                        sw1 = 1;
                                }

                                if (SalarioIntegral == true)
                                {
                                    if (rowIdCpto.ToString() == CptoIntCesant || rowIdCpto.ToString() == CptoCesant
                                        || rowIdCpto == IdPrima || rowIdCpto == IdMesada)
                                    {
                                        sw1 = 1;
                                    }
                                }

                                if (OpcionGenerador == "LT")
                                {
                                    if (CptoProvVaca == IdCpto.ToString())
                                        sw1 = 1;
                                }
                            }
                            break;

                        case "VC":
                            int rowIdCptoVC = Convert.ToInt32(row["idcpto"]);
                            if (rowIdCptoVC == Convert.ToInt32(CptoCCF) || rowIdCptoVC == IdSena || rowIdCptoVC == IdICBF)
                            {
                                sw1 = 0;
                                DataRow rowDsdata = dsdata.Tables["tblcptos"].Rows[0];
                                if (Convert.ToInt32(rowDsdata["basealq"]) == 0)
                                    sw1 = 1;
                                if (TipEmpleado == 2 || TipEmpleado == 3)
                                    sw1 = 1;
                                if (CptoProvVaca == IdCpto.ToString())
                                {
                                    row["BaseProv"] = 0;
                                    row["basevac"] = 1;
                                }
                            }
                            else if (rowIdCptoVC == IdCptoEps || rowIdCptoVC == IdCptoPension)
                            {
                                sw1 = 0;
                            }
                            else if (rowIdCptoVC.ToString() == CptoARPEmp)
                            {
                                DataRow rowDsdata = dsdata.Tables["tblcptos"].Rows[0];
                                string clase = rowDsdata["clase"].ToString();
                                if (clase == "2" || clase == "4")
                                    sw1 = 1;
                                if (Convert.ToInt32(rowDsdata["basealq"]) == 0)
                                    sw1 = 1;
                                if (TipEmpleado == 2 || TipEmpleado == 3)
                                    sw1 = 1;
                                if (CptoProvVaca == IdCpto.ToString())
                                    sw1 = 1;
                            }
                            else if (rowIdCptoVC.ToString() == CptoIntCesant || rowIdCptoVC.ToString() == CptoProvVaca
                                || rowIdCptoVC.ToString() == CptoCesant || rowIdCptoVC == IdPrima || rowIdCptoVC == IdMesada)
                            {
                                // Commented out in VB:
                                // Select Case CptoProvVaca
                                //     Case IdCpto
                                //         sw1 = 1
                                // End Select
                            }
                            else
                            {
                                sw1 = 1;
                            }
                            break;
                    }

                    // Check clase for salario 6,7
                    {
                        DataRow rowDsdata = dsdata.Tables["tblcptos"].Rows[0];
                        string clase = rowDsdata["clase"].ToString();
                        if (clase == "2" || clase == "4" || clase == "5")
                        {
                            if (clasesalario == 6 || clasesalario == 7)
                                sw1 = 1;
                        }
                    }

                    if (sw1 == 0)
                    {
                        int baseProvRow = Convert.ToInt32(row["BaseProv"]);
                        double tasaprov = Convert.ToDouble(row["tasaprov"]);
                        int baseVac = Convert.ToInt32(row["basevac"]);
                        string claseRow = row["clase"].ToString();
                        int rowIdCptoCalc = Convert.ToInt32(row["idcpto"]);

                        switch (baseProvRow)
                        {
                            case 0:
                                if (claseRow != "4")
                                {
                                    if (CptoSalario == 1)
                                    {
                                        ValLiq = Math.Round((BaseProv * tasaprov) / 100);
                                    }
                                    else
                                    {
                                        if (baseVac == 1)
                                        {
                                            ValLiq = Math.Round((BaseProv * tasaprov) / 100);
                                        }
                                    }
                                }

                                if (Aprsena == true)
                                {
                                    if (IdAdmArp != rowIdCptoCalc)
                                    {
                                        ValLiq = 0;
                                    }
                                    else
                                    {
                                        ValLiq = Math.Round((Salminimo * tasaprov) / 100);
                                    }
                                }

                                if (SalarioIntegral == true)
                                {
                                    if (Idvaca == rowIdCptoCalc)
                                    {
                                        ValLiq = Math.Round((BaseLiq * tasaprov) / 100);
                                    }
                                }

                                if (claseRow == "1")
                                {
                                    if (rowIdCptoCalc == IdPrima)
                                    {
                                        if (TipEmpleado != 1 && TipEmpleado != 4)
                                            ValLiq = 0;
                                    }
                                    else if (rowIdCptoCalc == IdMesada)
                                    {
                                        if (TipEmpleado == 1 || TipEmpleado == 4)
                                            ValLiq = 0;
                                    }
                                }
                                break;

                            case 2:
                                if (IdCpto == idTransaporte)
                                {
                                    ValLiq = 0;
                                }
                                else
                                {
                                    if (CptoSalario == 1)
                                    {
                                        ValLiq = Math.Round((BaseProv * tasaprov) / 100);
                                    }
                                }

                                if (SalarioIntegral == true)
                                {
                                    if (Idvaca == rowIdCptoCalc)
                                    {
                                        ValLiq = Math.Round((BaseLiq * tasaprov) / 100);
                                    }
                                }

                                if (Aprsena == true)
                                {
                                    if (IdAdmArp != rowIdCptoCalc)
                                    {
                                        ValLiq = 0;
                                    }
                                    else
                                    {
                                        if (clasesalario == 6)
                                            ValLiq = 0;
                                    }
                                }
                                break;

                            case 3:
                                if (IdCpto == rowIdCptoCalc)
                                {
                                    ValLiq = Math.Round((Credito * tasaprov) / 100);
                                }
                                break;

                            case 5:
                                if (IdCpto == idTransaporte)
                                {
                                    ValLiq = 0;
                                }
                                else
                                {
                                    if (CptoSalario == 1)
                                    {
                                        ValLiq = Math.Round((BaseProv * tasaprov) / 100);
                                    }
                                }

                                if (Aprsena == false)
                                {
                                    ValLiq = 0;
                                }
                                break;
                        }

                        if (ValLiq > 0)
                        {
                            // ok = this.msgconfig.BuscaCuentasContables(Convert.ToInt32(row["idcpto"]), Idcencos, myconnect, ref dsDataSet); // ERROR: CS1615
                            switch (ok)
                            {
                                case false:
                                    MessageBox.Show("Cuenta Contables del concepto no estan creadas" + row["idcpto"] + " " + Idcencos, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    break;
                                case true:
                                    cuentaGasto = dsDataSet.Tables["tblcuentas"].Rows[0]["ctagasto"].ToString();
                                    cuentaprov = dsDataSet.Tables["tblcuentas"].Rows[0]["ctaprov"].ToString();

                                    msgcnt.BuscarCuenta(ref cuentaGasto, myconnect,
                                        ref aplter, ref aplcencos, ref _natura, ref _cencos,
                                        ref _nivel, ref _aplicart, ref _apliTes, ref _nombre,
                                        ref _tasa, ref tipoaux, ref _estado,
                                        ref _apliCnt, ref _consiBaca, ref _bancoConsiBanca);

                                    Nit = RevisaNit(aplter, ForActCont, NitEmp, row["nit"].ToString(), TrasTer, Nit, Nit);
                                    Tipodoc = " "; Numdoc = " ";

                                    if (Convert.ToInt32(tipoaux) > 0)
                                    {
                                        Tipodoc = "PL";
                                        Numdoc = idplanilla.ToString();
                                    }

                                    msgcnt.BuscarCuenta(ref cuentaprov, myconnect,
                                        ref aplter, ref aplcencos, ref _natura, ref _cencos,
                                        ref _nivel, ref _aplicart, ref _apliTes, ref _nombre,
                                        ref _tasa, ref tipoaux, ref _estado,
                                        ref _apliCnt, ref _consiBaca, ref _bancoConsiBanca);

                                    nitprov = RevisaNit(aplter, ForActCont, NitEmp, row["nit"].ToString(), TrasTer, nitempleado, nitempleado);

                                    Tipodoc = " "; Numdoc = " ";
                                    if (Convert.ToInt32(tipoaux) > 0)
                                    {
                                        Tipodoc = "PL";
                                        Numdoc = idplanilla.ToString();
                                    }

                                    int naturRow = Convert.ToInt32(row["natur"]);
                                    if (naturRow == 0)
                                    {
                                        GrabaTemporal(idplanilla.ToString(), idnomina, rowIdCptoCalc, Consecutivo, Idcencos, cuentaGasto, Nit, Tipodoc, Numdoc, ValLiq, 0, Convert.ToDouble(IdEmpleadoVac), myconnect);
                                        ValLiq = ValLiq - Credito;
                                        GrabaTemporal(idplanilla.ToString(), idnomina, rowIdCptoCalc, Consecutivo, Idcencos, cuentaprov, nitprov, Tipodoc, Numdoc, 0, ValLiq, Convert.ToDouble(IdEmpleadoVac), myconnect);
                                    }
                                    else
                                    {
                                        GrabaTemporal(idplanilla.ToString(), idnomina, rowIdCptoCalc, Consecutivo, Idcencos, cuentaGasto, Nit, Tipodoc, Numdoc, ValLiq, 0, Convert.ToDouble(IdEmpleadoVac), myconnect);
                                        GrabaTemporal(idplanilla.ToString(), idnomina, rowIdCptoCalc, Consecutivo, Idcencos, cuentaprov, nitprov, Tipodoc, Numdoc, 0, ValLiq, Convert.ToDouble(IdEmpleadoVac), myconnect);
                                    }
                                    break;
                            }
                        }
                    }
                }

                fila += 1;
            }
        }

        // ── RevisaNit (line 4149) ───────────────────────────────────────────
        private string RevisaNit(string aplter, int ForActCont, string NitEmp,
            string cptonit, int TrasTer, string cedula, string idempleado)
        {
            string nit = " ";
            switch (aplter)
            {
                case "Y":
                    switch (ForActCont)
                    {
                        case 0:
                            nit = NitEmp;
                            break;
                        case 1:
                            if (cptonit.Trim() != "")
                            {
                                if (Convert.ToDouble(cptonit.Trim()) > 0)
                                {
                                    nit = cptonit;
                                }
                                else
                                {
                                    switch (TrasTer)
                                    {
                                        case 0:
                                            nit = cedula;
                                            break;
                                        case 1:
                                            nit = idempleado;
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                switch (TrasTer)
                                {
                                    case 0:
                                        nit = cedula;
                                        break;
                                    case 1:
                                        nit = idempleado;
                                        break;
                                }
                            }
                            break;
                    }
                    break;
            }

            if (nit.Trim() != "")
            {
                nit = Convert.ToDouble(nit).ToString();
            }
            return nit;
        }

        // ── GrabaTemporal (line 4185) ───────────────────────────────────────
        private void GrabaTemporal(string idplanilla, int Idnomina, int idcpto,
            double consecutivo, string idcencos, string cuenta, string nit,
            string Tipodoc, string numdoc, double debito, double credito,
            double idempleado, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = BuscaTemporal(idplanilla, Idnomina, idcpto, consecutivo, idcencos, cuenta, nit, Tipodoc, numdoc, idempleado, myconnect);
            switch (ok)
            {
                case false:
                    stbuilder.Append("insert into nom_contpla(idplanilla,Idnomina,idcpto,consecutivo,idcencos,cuenta,nit,Tipodoc,numdoc,debito,credito,idempleado)");
                    stbuilder.Append("values ('");
                    stbuilder.Append(idplanilla + "','");
                    stbuilder.Append(Idnomina + "','");
                    stbuilder.Append(idcpto + "','");
                    stbuilder.Append(consecutivo + "','");
                    stbuilder.Append(idcencos + "','");
                    stbuilder.Append(cuenta + "','");
                    stbuilder.Append(nit + "','");
                    stbuilder.Append(Tipodoc + "','");
                    stbuilder.Append(numdoc + "','");
                    stbuilder.Append(debito + "','");
                    stbuilder.Append(credito + "','");
                    stbuilder.Append(idempleado + "')");
                    break;
                case true:
                    stbuilder.Append("update nom_contpla set debito = debito + '");
                    stbuilder.Append(debito + "',");
                    stbuilder.Append("credito = credito + '");
                    stbuilder.Append(credito + "' ");
                    stbuilder.Append("where idplanilla = '" + idplanilla + "' and idnomina = '" + Idnomina + "' and idcpto = '" + idcpto + "' and consecutivo = ' " + consecutivo + "' and idcencos ='" + idcencos + "' ");
                    stbuilder.Append(" and cuenta = '" + cuenta + "' and  nit = '" + nit + "' and Tipodoc = '" + Tipodoc + "' and numdoc = '" + numdoc + "' and idempleado='" + idempleado + "'");
                    break;
            }

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaTemporal");
        }

        // ── BuscaTemporal (line 4221) ───────────────────────────────────────
        private bool BuscaTemporal(string idplanilla, int Idnomina, int idcpto,
            double consecutivo, string idcencos, string cuenta, string nit,
            string Tipodoc, string numdoc, double idempleado,
            OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();

            try
            {
                if (DsDataset != null)
                    DsDataset.Tables.Remove("tblcontplan");
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select debito, credito from nom_contpla ");
            stbuilder.Append("where idplanilla = '" + idplanilla + "' and idnomina = '" + Idnomina + "' and idcpto = '" + idcpto + "' and consecutivo = ' " + consecutivo + "' and idcencos ='" + idcencos + "' and cuenta = '" + cuenta + "' and  nit = '" + nit + "' and Tipodoc = '" + Tipodoc + "' and numdoc = '" + numdoc + "' and idempleado='" + idempleado + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaTemporal", ref DsDatosLiq, "tblcontplan");
            if (DsDatosLiq.Tables["tblcontplan"].Rows.Count > 0)
            {
                try
                {
                    if (DsDataset != null)
                        DsDataset.Tables.Add(DsDatosLiq.Tables["tblcontplan"].Copy());
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

        // ── EliminaTemporal (line 4251) ─────────────────────────────────────
        public bool EliminaTemporal(string idplanilla, int idnomina, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_contpla ");
            stbuilder.Append("where idplanilla = '" + idplanilla + "' and idnomina = '" + idnomina + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaTemporal");
            return false;
        }

        // ── ContabilizaPlanilla (line 4263) ─────────────────────────────────
        public bool ContabilizaPlanilla(int idplanilla, int idnomina, string Cpte,
            string ConseCpte, DateTime fechaMovtto, string usuario,
            Form myforma, OdbcConnection myconnect, bool ImpCpte = false)
        {
            StringBuilder stbuilder = new StringBuilder();
            double fila = 0;
            int sw1 = 0;
            DataSet DsDataset = new DataSet();
            double diff = 0, debito = 0, credito = 0;
            string AplTer = "N";
            string Nit = null, StNumDocAux = "";
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            DataSet dspagos = new DataSet();
            string NitEmpresa;
            string Detalle, Cencos;
            int CptoIncCxc = 0;

            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Contabilizando planilla : " + idplanilla + ", Por favor espere", myforma);

            stbuilder.Append("select idplanilla,idcpto,consecutivo,idcencos,cuenta,nit,Tipodoc,numdoc,sum(debito) as debito,sum(credito) as credito ");
            stbuilder.Append("from nom_contpla  ");
            stbuilder.Append("where idplanilla = '" + idplanilla + "' and Idnomina = '" + idnomina + "'");
            stbuilder.Append("group by idplanilla,idcpto,consecutivo,idcencos,cuenta,nit,Tipodoc,numdoc");
            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ContabilizaPlanilla", ref DsDataset, "tblcontplan");

            // ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, ref DsDataset); // ERROR: CS1615
            switch (ok)
            {
                case false:
                    MessageBox.Show("Parametros de la nomina no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                case true:
                    DataRow rowEmp = DsDataset.Tables["tblempresas"].Rows[0];
                    Detalle = rowEmp["nomres"].ToString();
                    NitEmpresa = rowEmp["Nit"].ToString();
                    break;
            }

            // ok = this.msgconfig.BuscaPeriodosPagos(idplanilla, idnomina, myconnect, ref dspagos); // ERROR: CS1503, CS1615
            switch (ok)
            {
                case true:
                    string detPagos = dspagos.Tables["tblperpagos"].Rows[0]["Detalle"].ToString().Trim();
                    if (detPagos != "")
                    {
                        Detalle = dspagos.Tables["tblperpagos"].Rows[0]["Detalle"].ToString();
                    }
                    break;
            }

            double ConseCpteD = 0;
            double.TryParse(ConseCpte, out ConseCpteD);

            if (ConseCpte.Trim() == "0" || ConseCpte.Trim() == "")
            {
                ConseCpteD = 0;
                msgcnt.BuscaComprobante(ref Cpte, ref ConseCpteD, true, myconnect);
            }

            ok = msgcnt.BuscaComprobante(ref Cpte, ref ConseCpteD, false, myconnect);
            switch (ok)
            {
                case true:
                    MessageBox.Show("comprobante ya existe en contabilidad, no se permite contabilizar", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            ConseCpte = ConseCpteD.ToString();

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblcontplan"].Rows.Count);
            msgbarra.Show();

            // Se graba la cabecera del documento con beneficiario el nit de la empresa
            if (idplanilla != 9999)
            {
                ok = msgcnt.BuscarTercero(NitEmpresa.Trim(), myconnect);
                switch (ok)
                {
                    case true:
                        msgcnt.GrabaDocumento(Cpte, ConseCpteD, 0, 0, fechaMovtto, Detalle, 0, myconnect, NitEmpresa.Trim(), "cont");
                        break;
                }
            }

            // Dummy ref variables for BuscarCuenta
            string _aplcencos2 = "N";
            string _natura2 = "0", _cencos2 = "0", _nivel2 = "0", _aplicart2 = "0";
            string _apliTes2 = "0", _nombre2 = "0";
            decimal _tasa2 = 0;
            int _estado2 = 0;
            string _apliCnt2 = "0", _consiBaca2 = "0", _bancoConsiBanca2 = "0";
            string _tipoaux2 = "0";

            while (fila < DsDataset.Tables["tblcontplan"].Rows.Count)
            {
                sw1 = 0;
                DataRow row = DsDataset.Tables["tblcontplan"].Rows[(int)fila];

                string cuentaRow = row["cuenta"].ToString();
                msgcnt.BuscarCuenta(ref cuentaRow, myconnect,
                    ref AplTer, ref _aplcencos2, ref _natura2, ref _cencos2,
                    ref _nivel2, ref _aplicart2, ref _apliTes2, ref _nombre2,
                    ref _tasa2, ref _tipoaux2, ref _estado2,
                    ref _apliCnt2, ref _consiBaca2, ref _bancoConsiBanca2);

                switch (AplTer)
                {
                    case "Y":
                        ok = msgcnt.BuscarTercero(row["nit"].ToString(), myconnect);
                        switch (ok)
                        {
                            case false:
                                MessageBox.Show("Tercero no esta creado en contabilidad " + row["nit"] + " Cuenta: " + row["cuenta"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                sw1 = 1;
                                break;
                            case true:
                                Nit = row["nit"].ToString();
                                break;
                        }
                        break;
                    default:
                        Nit = " ";
                        break;
                }

                Cencos = row["idcencos"].ToString();

                diff = Convert.ToDouble(row["debito"]) - Convert.ToDouble(row["credito"]);
                if (diff < 0)
                {
                    credito = diff * -1;
                    debito = 0;
                }
                else
                {
                    debito = diff;
                    credito = 0;
                }

                string _cencNom = "", _cencNomRes = "";
                string CencosRef = Cencos;
                msgcnt.BuscarCencos(ref CencosRef, myconnect, ref _cencNom, ref _cencNomRes);

                if (sw1 == 0)
                {
                    if (debito > 0 || credito > 0)
                    {
                        StNumDocAux = Convert.ToDouble(Cencos) + row["numdoc"].ToString();
                        StNumDocAux = Strings.Left(StNumDocAux, 10);
                        msgcnt.GrabaMovimiento(Cpte, ConseCpteD, row["cuenta"].ToString(), "9999",
                            fechaMovtto.ToString("yyyyMM"), Nit, fechaMovtto, Detalle, StNumDocAux,
                            debito, credito, 0, usuario, myconnect, 0,
                            row["Tipodoc"] + "-" + StNumDocAux,
                            row["nit"].ToString(), Cencos, Detalle, 0, "cont", null,
                            row["Tipodoc"].ToString(), fechaMovtto, Detalle);
                    }
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            ok = msgcnt.CierreDocumento(Cpte, ConseCpteD, myconnect);

            if (ImpCpte == true)
            {
                this.msgimp.ImprimirComprobante(Cpte, ConseCpteD, false, usuario, myconnect);
            }
            msgbarra.Close();
            msgbarra.Dispose();
            return ok;
        }

        // ── GeneraPlanosBancos (line 4383) ──────────────────────────────────
        public void GeneraPlanosBancos(int idplanilla, int idnomina, int Idbanco,
            string NomArchivo, DateTime fechaEnvio, string OfcRecaudo,
            string usuario, Form myforma, OdbcConnection myconnect,
            string CuentaOrgDisperFondos = "")
        {
            StringBuilder stbuilder = new StringBuilder();
            double fila = 0;
            int sw1 = 0;
            string TipoCta, FormatoBanco = "0";
            DataSet DsDataset = new DataSet();
            string CuentaOrgFondos, TotalTransferencia = "";
            StreamWriter StWriter;

            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Generado informacion : " + idplanilla + ", Por favor espere", myforma);

            stbuilder.Append("select liqnom.idplanilla,liqnom.idnomina,liqnom.idempleado,liqnom.devengos,liqnom.deduciones,liqnom.totpagar,empl.Cedula,");
            stbuilder.Append("empl.apellidos,empl.nombres ,empl.ClaCta,empl.NumCta,empl.idbanco,empl.Direccion,empl.email ");
            stbuilder.Append("from nom_liqplan04_vw liqnom inner join nom_empleados empl on liqnom.idnomina = empl.idnomina and liqnom.idempleado = empl.idempleado ");
            stbuilder.Append("where liqnom.idplanilla = '" + idplanilla + "' and liqnom.Idnomina = '" + idnomina + "' and totpagar > 0");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ContabilizaPlanilla", ref DsDataset, "tblliqplan");

            // ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, ref DsDataset); // ERROR: CS1615
            switch (ok)
            {
                case false:
                    MessageBox.Show("Parametros de la nomina no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
            }

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblliqplan"].Rows.Count);
            msgbarra.Show();

            // BuscaBanco: p1=Idbanco, p2=myconnect, p3-p14 skip, p15=skip(Copias), p16=FormatoBanco(EstruDispersion)
            {
                string _bancoStr = Idbanco.ToString();
                string _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "", _p11 = "";
                string _p13 = "0", _p14 = "0", _p15 = "0";
                string _p17 = "N", _p18 = "0", _p19 = "0", _p20 = "0", _p21 = "N", _p22 = "N";
                msgconfigcop.BuscaBanco(ref _bancoStr, myconnect,
                    ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9,
                    ref _p10, ref _p11, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
                    ref _p13, ref _p14, ref _p15, ref FormatoBanco,
                    ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22);
            }

            SaveFileDialog CrearArchivo = new SaveFileDialog();
            CrearArchivo.Filter = "Guardar como tipo *.txt |*.txt";
            CrearArchivo.Title = "Guardar como...               Programa: [DisFondos]";
            CrearArchivo.FileName = "";
            CrearArchivo.ShowDialog(myforma);
            StWriter = new StreamWriter(CrearArchivo.OpenFile(), Encoding.Default);

            switch (Convert.ToInt32(FormatoBanco))
            {
                case 5:
                    this.DispersionCabeceraBancoAvvillas(StWriter, fechaEnvio);
                    break;
                case 4:
                    string SecuenciaLote = "";
                    int numeroregistro = 0;
                    decimal SumatoriaCredito = 0;
                    double fila1;
                    SecuenciaLote = Interaction.InputBox("Secuencia Envio de lotes  A B C.....", "SOLIDO", "A");
                    if (SecuenciaLote.Trim() == "")
                    {
                        MessageBox.Show("Debe escoger un Secuencia.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    for (fila1 = 0; fila1 <= DsDataset.Tables["tblliqplan"].Rows.Count - 1; fila1++)
                    {
                        DataRow rLiq = DsDataset.Tables["tblliqplan"].Rows[(int)fila1];
                        numeroregistro = numeroregistro + 1;
                        SumatoriaCredito = SumatoriaCredito + Convert.ToDecimal(rLiq["totpagar"]);
                    }

                    DataRow rEmp4 = DsDataset.Tables["tblempresas"].Rows[0];
                    this.DispersionCabeceraBancodecolombia(StWriter, fechaEnvio, CuentaOrgDisperFondos,
                        rEmp4["TipoCuenta"].ToString(), rEmp4["Nombre"].ToString(), rEmp4["Nit"].ToString(),
                        "225", SecuenciaLote, numeroregistro, SumatoriaCredito.ToString());
                    break;
                default:
                    ImprimeCabezera(Convert.ToInt32(FormatoBanco), NomArchivo, fechaEnvio, Convert.ToInt32(OfcRecaudo), DsDataset, myconnect);
                    break;
            }

            while (fila < DsDataset.Tables["tblliqplan"].Rows.Count)
            {
                sw1 = 0;
                DataRow row = DsDataset.Tables["tblliqplan"].Rows[(int)fila];

                switch (Convert.ToInt32(FormatoBanco))
                {
                    case 1:
                        if (Convert.ToInt32(row["ClaCta"]) == 1)
                            TipoCta = "02";
                        else
                            TipoCta = "01";

                        GrabaMovtoBancoBogota(NomArchivo, 2, "C", row["Cedula"].ToString(),
                            row["apellidos"] + " " + row["nombres"], Convert.ToInt32(TipoCta), "0",
                            row["NumCta"].ToString(), Convert.ToDouble(row["totpagar"]),
                            Convert.ToInt32(row["idbanco"]), 1, usuario, "N");
                        break;

                    case 5:
                        CuentaOrgFondos = DsDataset.Tables["tblempresas"].Rows[0]["CtaOrgFondos"].ToString();
                        TipoCta = DsDataset.Tables["tblempresas"].Rows[0]["TipoCuenta"].ToString();

                        this.DispersionDetalleBancoAvvillas(StWriter, "000023", TipoCta, CuentaOrgFondos, "052",
                            row["ClaCta"].ToString(), row["NumCta"].ToString(), (fila + 1).ToString(),
                            row["totpagar"].ToString(), "000000",
                            row["apellidos"] + " " + row["nombres"], row["Cedula"].ToString());
                        break;

                    case 6:
                        CuentaOrgFondos = OfcRecaudo.Substring(0, 4) + "00" +
                            DsDataset.Tables["tblempresas"].Rows[0]["CtaOrgFondos"].ToString().Substring(0, 10);

                        if (Convert.ToInt32(row["ClaCta"]) == 1)
                            TipoCta = "0200";
                        else
                            TipoCta = "0100";

                        this.GrabaMovtoBancoBbva(NomArchivo, "01", row["Cedula"].ToString(), 0,
                            row["idbanco"].ToString(), CuentaOrgFondos, Convert.ToInt32(TipoCta),
                            row["NumCta"].ToString(), Convert.ToDouble(row["totpagar"]), fechaEnvio,
                            OfcRecaudo, row["apellidos"] + " " + row["nombres"],
                            row["direccion"].ToString(), " ", " ", "Liquidacion nomina numero " + idplanilla,
                            " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ");
                        break;

                    case 4:
                        this.DispersionDetalleBancodeColombia(StWriter, row["Cedula"].ToString(),
                            row["apellidos"] + " " + row["nombres"], row["ClaCta"].ToString(),
                            row["NumCta"].ToString(), row["totpagar"].ToString(), row["idbanco"].ToString());
                        break;

                    default:
                        GrabaMovtoBancoCartera(NomArchivo, row["Cedula"].ToString(), Convert.ToDouble(row["totpagar"]));
                        break;
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            switch (Convert.ToInt32(FormatoBanco))
            {
                case 5:
                    TotalTransferencia = DsDataset.Tables["tblliqplan"].Compute("sum(totpagar)", "").ToString();
                    TotalTransferencia = Strings.FormatNumber(Convert.ToDouble(TotalTransferencia), 0).ToString();
                    TotalTransferencia = TotalTransferencia.Replace(",", "");
                    TotalTransferencia = TotalTransferencia.Replace("-", "");
                    TotalTransferencia = Strings.Right("000000000000000000" + TotalTransferencia, 18) + Strings.Right("00", 2);

                    StWriter.Write("03");
                    StWriter.Write(Strings.Right("000000000" + DsDataset.Tables["tblliqplan"].Rows.Count, 9));
                    StWriter.WriteLine(Strings.Right("00000000000000000000" + TotalTransferencia, 20));
                    StWriter.Close();
                    break;
                case 4:
                    StWriter.Close();
                    break;
            }

            msgbarra.Close();
            msgbarra.Dispose();
        }

        // ── ImprimeCabezera (line 4528) ─────────────────────────────────────
        private void ImprimeCabezera(int Idbanco, string Archivo, DateTime FechaEnvio,
            int OficinaRecaudo, DataSet DsDataset, OdbcConnection myconnect)
        {
            switch (Idbanco)
            {
                case 1:
                    DataRow rEmp = DsDataset.Tables["tblempresas"].Rows[0];
                    GrabaCabezaBancoBogota(Archivo, 1, FechaEnvio.ToString("yyyyMMdd"),
                        Convert.ToInt32(rEmp["TipoCuenta"]), Convert.ToDouble(rEmp["CtaOrgFondos"]),
                        rEmp["NomRes"].ToString(), rEmp["Nit"].ToString(), 1, OficinaRecaudo,
                        FechaEnvio.ToString("yyyyMMdd"),
                        Convert.ToInt32(rEmp["CtaOrgFondos"].ToString().Trim().Substring(0, 3)), "N");
                    break;
                case 5:
                    // Me.DispersionCabeceraBancoAvvillas(Archivo, FechaEnvio)
                    break;
            }
        }

        // ── DispersionCabeceraBancoAvvillas (line 4539) ─────────────────────
        private void DispersionCabeceraBancoAvvillas(StreamWriter StArchivo, DateTime Fecpago)
        {
            StArchivo.Write("01");
            StArchivo.Write(Fecpago.ToString("yyyyMMdd"));
            StArchivo.Write(Strings.Replace(DateTime.Now.ToString("HH:mm:ss"), ":", ""));
            StArchivo.Write("088");
            StArchivo.Write("02");
            StArchivo.Write(Strings.Replace(Strings.Space(50), " ", "B"));
            StArchivo.WriteLine(Strings.Replace(Strings.Space(120), " ", "0"));
        }

        // ── DispersionDetalleBancoAvvillas (line 4554) ──────────────────────
        private void DispersionDetalleBancoAvvillas(StreamWriter StArchivo, string TipoMovto,
            string TipoCtaDispersora, string CuentaDispersora, string CodBancoCuenta,
            string TipoCuenta, string NumCuenta, string Secuencia, string ValorAbono,
            string Comprobante, string NomCliente, string Idcliente)
        {
            ValorAbono = Strings.FormatNumber(Convert.ToDouble(ValorAbono), 2, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.False).ToString();
            ValorAbono = ValorAbono.Replace(".", "");

            StArchivo.Write("02");
            StArchivo.Write(Strings.Right("000000" + TipoMovto, 6));
            StArchivo.Write(Strings.Right("00" + (TipoCtaDispersora == "1" ? "01" : "06"), 2));
            StArchivo.Write(Strings.Right("0000000000000000" + CuentaDispersora, 16));
            StArchivo.Write(Strings.Right("000" + CodBancoCuenta, 3));
            StArchivo.Write(TipoCuenta == "1" ? "01" : "06");
            StArchivo.Write(Strings.Right("0000000000000000" + NumCuenta, 16));
            StArchivo.Write(Strings.Right("000000000" + Secuencia, 9));
            StArchivo.Write(Strings.Right("000000000000000000" + ValorAbono, 18));
            StArchivo.Write(Strings.Right("0000000000000000" + Comprobante, 16));
            StArchivo.Write(Strings.Replace(Strings.Space(16), " ", "0"));
            StArchivo.Write(Strings.Replace(Strings.Space(16), " ", "0"));
            StArchivo.Write(Strings.Left(NomCliente + Strings.Space(30), 30));
            StArchivo.Write(Strings.Right("00000000000" + Idcliente, 11));
            StArchivo.Write(Strings.Replace(Strings.Space(6), " ", "0"));
            StArchivo.Write(Strings.Replace(Strings.Space(2), " ", "0"));
            StArchivo.WriteLine(Strings.Replace(Strings.Space(20), " ", "0"));
        }

        // ── GrabaMovtoBancoBogota (line 4585) ───────────────────────────────
        private void GrabaMovtoBancoBogota(string Archivo, int TipoRegistro, string TipoIdent,
            string Cedula, string Nombre, int TipoCta, string NumCtaEmp, string NumCtaEmpl,
            double NetoPagar, int IdbancoEmpl, int CodofcEmpl, string Usuario, string IndFax)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(TipoRegistro);
                StrDisp.Write(TipoIdent);
                StrDisp.Write(Strings.Right("00000000000" + Cedula, 11));
                StrDisp.Write(Strings.Left(Nombre + "                                        ", 40));
                StrDisp.Write("0");
                StrDisp.Write(TipoCta);
                StrDisp.Write(Strings.Left(NumCtaEmpl + "                 ", 17));
                StrDisp.Write(Strings.Right("0000000000000000" + NetoPagar, 16) + "00");
                StrDisp.Write("A");
                StrDisp.Write("000");
                StrDisp.Write(Strings.Right("000" + IdbancoEmpl, 3));
                StrDisp.Write(Strings.Right("0000" + CodofcEmpl, 4));
                StrDisp.Write(Strings.Left(Usuario + "         ", 9));
                StrDisp.Write(" ");
                StrDisp.Write("                                                                      ");
                StrDisp.Write("0");
                StrDisp.Write("0000000000");
                StrDisp.Write("N");
                StrDisp.Write("                                                         ");
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        // ── GrabaMovtoBancoCartera (line 4613) ──────────────────────────────
        private void GrabaMovtoBancoCartera(string Archivo, string Cedula, double NetoPagar)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(Cedula + ",");
                StrDisp.Write(NetoPagar);
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        // ── GrabaMovtoBancoBbva (line 4624) ─────────────────────────────────
        private void GrabaMovtoBancoBbva(string Archivo, string TipoIdent, string Cedula,
            int Formapago, string bancoReceptor, string NumeroCuentaBBva, int TipoCta,
            string NumCta, double NetoPagar, DateTime Fecpago, string CodigoOficina,
            string Nombre, string Direccion1, string Direccion2, string email,
            string Concepto1, string Concepto2, string Concepto3, string Concepto4,
            string Concepto5, string Concepto6, string Concepto7, string Concepto8,
            string Concepto9, string Concepto10, string Concepto11, string Concepto12,
            string Concepto13, string Concepto14, string Concepto15, string Concepto16,
            string Concepto17, string Concepto18, string Concepto19, string Concepto20,
            string Concepto21, string Concepto22)
        {
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(TipoIdent);
                StrDisp.Write(Strings.Right("0000000000000000" + Cedula, 15));
                StrDisp.Write(Formapago);
                StrDisp.Write("1");
                StrDisp.Write(Strings.Right("0000" + bancoReceptor, 4));
                StrDisp.Write(Strings.Left(CodigoOficina + "     ", 4));
                StrDisp.Write("00");
                StrDisp.Write(Strings.Right("0000" + TipoCta, 4));
                StrDisp.Write(Strings.Left(NumCta + "                 ", 6));
                StrDisp.Write("0000000000000000000");
                StrDisp.Write(Strings.Right("0000000000000000" + NetoPagar, 13) + "00");
                StrDisp.Write(Fecpago.ToString("yyyyMMdd"));
                StrDisp.Write("0000");
                StrDisp.Write(Strings.Left(Nombre + "                                    ", 36));
                StrDisp.Write(Strings.Left(Direccion1 + "                                        ", 36));
                StrDisp.Write(Strings.Left(Direccion2 + "                                        ", 36));
                StrDisp.Write(Strings.Left(email + "                                                  ", 48));
                StrDisp.Write(Strings.Left(Concepto1 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto2 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto3 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto4 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto5 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto6 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto7 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto8 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto9 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto10 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto11 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto12 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto13 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto14 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto15 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto16 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto17 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto18 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto19 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto20 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto21 + "                                        ", 40));
                StrDisp.Write(Strings.Left(Concepto22 + "                                        ", 40));
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        // ── GrabaCabezaBancoBogota (line 4673) ──────────────────────────────
        private void GrabaCabezaBancoBogota(string Archivo, int TipoRegistro, string Feclote,
            int TipoCta, double NumCuenta, string NomEmpresa, string NitEmp,
            int CtrlTrans, int OfcRecaudo, string FecApli, int Ctaofi, string Identiemp)
        {
            if (TipoCta == 1)
            {
                TipoCta = 2;
            }
            using (StreamWriter StrDisp = File.AppendText(Archivo))
            {
                StrDisp.Write(TipoRegistro);
                StrDisp.Write(Feclote);
                StrDisp.Write("000000000000000000000000");
                StrDisp.Write(TipoCta);
                StrDisp.Write("00000000");
                StrDisp.Write(Strings.Right("000000000" + NumCuenta, 9));
                StrDisp.Write(Strings.Left(NomEmpresa + "                                       ", 40));
                StrDisp.Write(Strings.Right("00000000000" + NitEmp, 11));
                StrDisp.Write(Strings.Right("000" + CtrlTrans, 3));
                StrDisp.Write(Strings.Right("0000" + OfcRecaudo, 4));
                StrDisp.Write(Feclote);
                StrDisp.Write(Strings.Right("000" + Ctaofi, 3));
                StrDisp.Write(Identiemp);
                StrDisp.Write("                             ");
                StrDisp.Write("                                                                               ");
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }
    }
}
