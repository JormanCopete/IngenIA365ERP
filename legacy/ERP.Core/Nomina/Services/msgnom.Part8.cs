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
        // ExecuteQueryDataset duplicado eliminado (original en msgnom.Part7.cs:2169)

        // Constructor New() is already in Part1 (msgnom.cs)

        public virtual double CalculaPromedioCesantias(int Idnomina, double IdEmpleado, int PlaIniCesant, int PlaFinCesant, DateTime fecIniCesant, DateTime fecFinCesant,
            int Dias, double TopeAux, double ValAuxTra, ERP.Core.Compartido.Controles.Barraprogress msgbarra, OdbcConnection myconnect, ref double DiasLiq, bool actualiza = true)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            int SW1 = 0, DiasCesant = 0;
            string stmysql;
            double Sueldo = 0, SalDia = 0, AcuCesant = 0, PromeCesat = 0;

            Stbuilder.Append("select empl.idnomina,empl.idempleado,empl.salario,empl.clasesalario,empl.fecing,");
            Stbuilder.Append("sum( liqplan.valbasecesant) as AcuCesant,empl.FecReingreso,empl.ClauxTra ");
            Stbuilder.Append("from nom_empleados empl left join nom_liqplan05_vw liqplan ");
            Stbuilder.Append("on empl.idnomina =  liqplan.idnomina and empl.idempleado = liqplan.idempleado ");
            Stbuilder.Append("and liqplan.idplanilla between '" + PlaIniCesant + "' and '" + PlaFinCesant + "' ");
            Stbuilder.Append("where empl.idnomina = '" + Idnomina + "' and empl.idempleado = '" + IdEmpleado + "' ");
            Stbuilder.Append("group by empl.idnomina, empl.idempleado,empl.salario, empl.clasesalario,empl.fecing,empl.FecReingreso,empl.ClauxTra");

            this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "CalculaPromedioPrestaciones", ref DsDataSet, "TblPromCesant");

            if (msgbarra != null)
            {
                msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["TblPromCesant"].Rows.Count, "Calculando promedio Cesantias");
            }

            while (fila < DsDataSet.Tables["TblPromCesant"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["TblPromCesant"].Rows[(int)fila];
                SW1 = 0;

                Sueldo = this.msgconfig.BuscarNovedadesSalarioEmpleadoAnt(Idnomina, IdEmpleado, fecFinCesant, myconnect);

                if (Sueldo == 0)
                {
                    Sueldo = Convert.ToDouble(row["salario"]);
                }

                if (row["AcuCesant"] is DBNull)
                {
                    AcuCesant = 0;
                }
                else
                {
                    AcuCesant = Convert.ToDouble(row["AcuCesant"]);
                }

                if (Convert.ToInt32(row["claseSalario"]) == 2)
                {
                    SW1 = 1;
                }

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                {
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);
                }

                if (Convert.ToDateTime(row["fecing"]) > fecIniCesant)
                {
                    DiasCesant = this.msgconfig.CalculaDias(fecFinCesant, Convert.ToDateTime(row["fecing"]));
                }
                else
                {
                    DiasCesant = Dias;
                }

                DiasLiq = DiasCesant;

                if (AcuCesant > 0 && DiasCesant > 0)
                {
                    PromeCesat = Math.Round(AcuCesant / DiasCesant, 2) * 30;
                }
                else
                {
                    PromeCesat = 0;
                }

                if (Convert.ToInt32(row["ClauxTra"]) != 2)
                {
                    if (Sueldo < TopeAux)
                    {
                        Sueldo += ValAuxTra;
                    }
                }

                if (PromeCesat > 0)
                {
                    PromeCesat = Math.Round((Sueldo + PromeCesat) / 30, 0);
                }

                SalDia = Math.Round(Sueldo / 30, 2);

                if (SalDia > PromeCesat)
                {
                    PromeCesat = SalDia;
                    DiasCesant = 30;
                }

                if (SW1 == 0)
                {
                    if (actualiza)
                    {
                        stmysql = "Update nom_empleados set PromCes = '" + PromeCesat + "',diascesan='" + DiasCesant + "' where idnomina = '" + row["idnomina"] + "' and idempleado = '" + row["idempleado"] + "'";
                        this.msgodbc.ExecuteQueryconec(stmysql, myconnect, "CalculaPromedioCesantias");
                    }
                    else
                    {
                        return PromeCesat;
                    }
                }

                if (msgbarra != null)
                {
                    msgbarra.PerformStep();
                }

                fila += 1;
            }

            return 0;
        }

        // Overload without ref DiasLiq
        public virtual double CalculaPromedioCesantias(int Idnomina, double IdEmpleado, int PlaIniCesant, int PlaFinCesant, DateTime fecIniCesant, DateTime fecFinCesant,
            int Dias, double TopeAux, double ValAuxTra, ERP.Core.Compartido.Controles.Barraprogress msgbarra, OdbcConnection myconnect)
        {
            double DiasLiq = 0;
            return CalculaPromedioCesantias(Idnomina, IdEmpleado, PlaIniCesant, PlaFinCesant, fecIniCesant, fecFinCesant, Dias, TopeAux, ValAuxTra, msgbarra, myconnect, ref DiasLiq, true);
        }

        private void DispersionCabeceraBancodecolombia(StreamWriter StArchivo, DateTime Fecpago, string CuentaOrigen, string TipoCuenta,
            string Nomempresa, string IdEmpresa, string TipoMovto, string SecuenciaLote, int numeroregistro, string SumatoriaCreditos)
        {
            IdEmpresa = IdEmpresa.Replace(".", "");
            IdEmpresa = IdEmpresa.Replace("-", "");
            SumatoriaCreditos = Strings.FormatNumber(SumatoriaCreditos, 2, TriState.UseDefault, TriState.UseDefault, TriState.False);
            SumatoriaCreditos = SumatoriaCreditos.ToString().Replace(".", "");

            StArchivo.Write("1"); // Tipo registro
            StArchivo.Write(Strings.Right("0000000000" + IdEmpresa, 10)); // nit empresa
            StArchivo.Write(Strings.Space(15)); // Nombre entidad que envia
            StArchivo.Write(Strings.Right("000" + TipoMovto, 3)); // Clase de transaccion
            StArchivo.Write(Strings.Space(10)); // Descripcion proposito transacciones
            StArchivo.Write(Fecpago.ToString("yyMMdd")); // Fecha Transmision de lote
            StArchivo.Write(SecuenciaLote); // Secuencia envio de lotes ese dia
            StArchivo.Write(DateTime.Now.ToString("yyMMdd")); // Numero Registro
            StArchivo.Write(numeroregistro); // Numero de registros
            StArchivo.Write(Strings.Replace(Strings.Space(12), " ", "0")); // Sumatoria de debitos
            StArchivo.Write(Strings.Right("000000000000" + SumatoriaCreditos, 12)); // Sumatoria de creditos
            StArchivo.Write(Strings.Right("00000000000" + CuentaOrigen, 11)); // CuentaOrigen
            StArchivo.Write((TipoCuenta == "1") ? "S" : "D"); // TipoCuenta
            StArchivo.WriteLine(Strings.Space(80));
        }

        private void DispersionDetalleBancodeColombia(StreamWriter StArchivo, string Idcliente, string NomCliente, string TipoCuenta,
            string NumCuenta, string ValorAbono, string CodBancoCuenta)
        {
            ValorAbono = Strings.FormatNumber(Convert.ToDouble(ValorAbono), 2, TriState.UseDefault, TriState.UseDefault, TriState.False);

            StArchivo.Write("6");
            StArchivo.Write(Strings.Right("000000000000000" + Idcliente, 15));
            StArchivo.Write(Strings.Left(NomCliente + Strings.Space(18), 18));
            StArchivo.Write(Strings.Right("000000000" + CodBancoCuenta, 9));
            StArchivo.Write(Strings.Right("00000000000000000" + NumCuenta, 17));
            StArchivo.Write((TipoCuenta == "1" || TipoCuenta == "2") ? "S" : "1"); // Indicador lugar de pago
            StArchivo.Write((TipoCuenta == "1") ? "37" : "27"); // tipo Trasaccion
            StArchivo.Write(Strings.Right("0000000000" + ValorAbono, 10)); // formatea

            StArchivo.Write(Strings.Space(9)); // Fecha aplicacion
            StArchivo.Write(Strings.Space(12)); // Referencia
            StArchivo.Write(Strings.Space(1)); // Filler
            StArchivo.WriteLine(Strings.Space(8));
        }

        public bool LiquidaAnticipoCesantias(int IdPlanilla, int Idnomina, double IdEmpleado, DateTime FechaAnticipo, bool Actualiza, string Destinacion, string numresolucion, DateTime fecresolucion, Form Myforma, string Usuario, OdbcConnection myconnect)
        {
            DateTime FechaIni, FechaFin;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Liquidando Anticipo Cesantias", Myforma);
            DataSet DsDataset = new DataSet();
            DateTime fecIniCesant, fecFinCesant;
            double DiasLiquidados = 0;
            int DiasCesant = 0;
            double ValAuxTra = 0, TopeAux = 0;
            int IdTrasporte = 0;
            double IdAntCesantia = 0;
            double ValorCesantias = 0, InteresesCesantias = 0;
            int Meses = 0;
            double IdAntIntCesantia = 0, salariobase = 0;

            msgbarra.Show();

            fecIniCesant = new DateTime(FechaAnticipo.Year, 1, 1);

            this.msgconfig.BuscaEmpleado(Idnomina.ToString(), IdEmpleado.ToString(), myconnect, DsDataset);

            DataRow rowEmp = DsDataset.Tables["tblempleados"].Rows[0];
            if (Convert.ToDateTime(rowEmp["FecReingreso"]) > Convert.ToDateTime(rowEmp["Fecing"]))
            {
                rowEmp["Fecing"] = Convert.ToDateTime(rowEmp["FecReingreso"]);
            }

            if (Convert.ToDateTime(rowEmp["Fecing"]) > fecIniCesant)
            {
                fecIniCesant = Convert.ToDateTime(rowEmp["Fecing"]);
            }

            DiasCesant = this.msgconfig.CalculaDias(FechaAnticipo, fecIniCesant);

            if (DiasCesant > 0)
            {
                ok = this.msgconfig.BuscaEmpresa(Idnomina, myconnect, DsDataset);
                if (ok)
                {
                    DataRow rowEmpresa = DsDataset.Tables["tblempresas"].Rows[0];
                    IdTrasporte = Convert.ToInt32(rowEmpresa["IdTrasporte"]);
                    IdAntCesantia = Convert.ToDouble(rowEmpresa["IdAntCesan"]);
                    IdAntIntCesantia = Convert.ToDouble(rowEmpresa["IdRecNotur"]);

                    this.msgconfig.BuscaCptos(IdTrasporte.ToString(), myconnect, DsDataset);
                    DataRow rowCpto = DsDataset.Tables["tblcptos"].Rows[0];
                    ValAuxTra = Convert.ToDouble(rowCpto["Valor"]);
                    TopeAux = Convert.ToDouble(rowCpto["Saltope"]);
                }

                DataRow rowEmp2 = DsDataset.Tables["tblempleados"].Rows[0];
                salariobase = Convert.ToDouble(rowEmp2["PromCes"]) * 30;
                ValorCesantias = Math.Round((salariobase) * (DiasCesant / 360.0), 0);
                InteresesCesantias = Math.Round(ValorCesantias * (((DiasCesant * 12.0) / 360.0) / 100.0), 0);

                this.GrabaAnticipoCesantia(IdPlanilla, Idnomina, IdEmpleado, FechaAnticipo, salariobase, DiasCesant, ValorCesantias, InteresesCesantias, numresolucion, fecresolucion, Destinacion, (int)IdAntCesantia, (int)IdAntIntCesantia, Usuario, myconnect);

                if (Actualiza)
                {
                    if (ValorCesantias > 0)
                    {
                        this.GrabaMovimiento(IdPlanilla, Idnomina, IdEmpleado, (int)IdAntCesantia, IdPlanilla, 0, ValorCesantias, 1, Usuario, myconnect, Destinacion);
                    }
                    if (InteresesCesantias > 0)
                    {
                        this.GrabaMovimiento(IdPlanilla, Idnomina, IdEmpleado, (int)IdAntIntCesantia, IdPlanilla, 0, InteresesCesantias, 1, Usuario, myconnect, Destinacion);
                    }
                }
            }

            msgbarra.Close();
            msgbarra.Dispose();
            return false;
        }

        public void GrabaAnticipoCesantia(int Idplanilla, int Idnomina, double idempleado, DateTime feccorte, double salbasicoanticipo, int diaslab,
            double valanticipo, double valinteres, string resolucion, DateTime fecresolucion, string destinacion, int idanticipo, int idinteres,
            string Usuario, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = this.BuscaAnticipoCesantia(Idplanilla, Idnomina, idempleado, myconnect);
            if (!ok)
            {
                stbuilder.Append("insert into nom_antcesantia (");
                stbuilder.Append("idnomina, idplanilla, idempleado, feccorte, salbasicoanticipo,diaslab,");
                stbuilder.Append("valanticipo, valinteres, resolucion, fecresolucion, destinacion, idanticipo, idinteres, usuario,fechasys) ");
                stbuilder.Append("values ('");
                stbuilder.Append(Idnomina + "','");
                stbuilder.Append(Idplanilla + "','");
                stbuilder.Append(idempleado + "','");
                stbuilder.Append(feccorte.ToString(varini.PstForFec) + "','");
                stbuilder.Append(salbasicoanticipo + "','");
                stbuilder.Append(diaslab + "','");
                stbuilder.Append(valanticipo + "','");
                stbuilder.Append(valinteres + "','");
                stbuilder.Append(resolucion + "','");
                stbuilder.Append(fecresolucion.ToString(varini.PstForFec) + "','");
                stbuilder.Append(destinacion + "','");
                stbuilder.Append(idanticipo + "','");
                stbuilder.Append(idinteres + "','");
                stbuilder.Append(Usuario + "','");
                stbuilder.Append(DateTime.Now.ToString(varini.pstForfecyHora) + "')");
            }
            else
            {
                stbuilder.Append("update nom_antcesantia set ");
                stbuilder.Append("feccorte = '");
                stbuilder.Append(feccorte.ToString(varini.PstForFec) + "',");
                stbuilder.Append("salbasicoanticipo = '");
                stbuilder.Append(salbasicoanticipo + "',");
                stbuilder.Append("diaslab = '");
                stbuilder.Append(diaslab + "',");
                stbuilder.Append("valanticipo = '");
                stbuilder.Append(valanticipo + "',");
                stbuilder.Append("valinteres = '");
                stbuilder.Append(valinteres + "',");
                stbuilder.Append("resolucion = '");
                stbuilder.Append(resolucion + "',");
                stbuilder.Append("fecresolucion = '");
                stbuilder.Append(fecresolucion.ToString(varini.PstForFec) + "',");
                stbuilder.Append("destinacion = '");
                stbuilder.Append(destinacion + "',");
                stbuilder.Append("idanticipo = '");
                stbuilder.Append(idanticipo + "',");
                stbuilder.Append("idinteres = '");
                stbuilder.Append(idinteres + "',");
                stbuilder.Append("usuario = '");
                stbuilder.Append(Usuario + "',");
                stbuilder.Append("fechasys = '");
                stbuilder.Append(DateTime.Now.ToString(varini.pstForfecyHora) + "' ");
                stbuilder.Append("where idplanilla = '" + Idplanilla + "' and Idnomina ='" + Idnomina + "' and idempleado ='" + idempleado + "'");
            }
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaAnticipoCesantia");
        }

        public bool BuscaAnticipoCesantia(int Idplanilla, int Idnomina, double idempleado, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDatosLiq = new DataSet();
            try
            {
                if (DsDataset != null)
                    DsDataset.Tables.Remove("tblanticpos");
            }
            catch (Exception) { }

            stbuilder.Append("select idnomina, idplanilla, idempleado, feccausaini, feccausafin, feccorte, salbasicoanticipo, ");
            stbuilder.Append("diaslab, valanticipo, valinteres, resolucion, fecresolucion, destinacion, idanticipo, idinteres, usuario,fechasys ");
            stbuilder.Append("from nom_antcesantia where idplanilla = '" + Idplanilla + "' and Idnomina = '" + Idnomina + "' and idempleado = '" + idempleado + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAnticipoCesantia", ref DsDatosLiq, "tblanticpos");
            if (DsDatosLiq.Tables["tblanticpos"].Rows.Count > 0)
            {
                try
                {
                    if (DsDataset != null)
                        DsDataset.Tables.Add(DsDatosLiq.Tables["tblanticpos"].Copy());
                }
                catch (Exception) { }

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaAnticiposCesantiasLiquidados(int idnomina, double idempleado, int planillaini, int planillafin, OdbcConnection myconnect, ref double ValCesantias, ref double ValIntereses)
        {
            DataSet dsdata = new DataSet();
            double IdAntCesan = 0, IdAntInt = 0;
            StringBuilder StBuilder = new StringBuilder();
            double valor = 0;

            ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, dsdata);
            if (ok)
            {
                DataRow rowEmpresa = dsdata.Tables["tblempresas"].Rows[0];
                IdAntCesan = Convert.ToDouble(rowEmpresa["IdAntCesan"]);
                IdAntInt = Convert.ToDouble(rowEmpresa["IdRecNotur"]);

                StBuilder.Append("select SUM(Valor) as campo1 ");
                StBuilder.Append("from nom_liqplan ");
                StBuilder.Append("where idnomina='" + idnomina + "' and idempleado='" + idempleado + "' and ");
                StBuilder.Append("idplanilla between " + planillaini + " and " + planillafin + " and IdCpto='" + IdAntCesan + "'");

                string _v1 = "0", _v2 = "0", _v3 = "0", _v4 = "0";
                this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "BuscaAnticiposCesantiasLiquidados", ref _v1, ref _v2, ref _v3, ref _v4);
                double.TryParse(_v1, out valor);
                ValCesantias = valor;

                valor = 0;
                StBuilder.Remove(0, StBuilder.Length);
                StBuilder.Append("select SUM(Valor) as campo1 ");
                StBuilder.Append("from nom_liqplan ");
                StBuilder.Append("where idnomina='" + idnomina + "' and idempleado='" + idempleado + "' and ");
                StBuilder.Append("idplanilla between " + planillaini + " and " + planillafin + " and IdCpto='" + IdAntInt + "'");

                _v1 = "0"; _v2 = "0"; _v3 = "0"; _v4 = "0";
                this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "BuscaAnticiposCesantiasLiquidados", ref _v1, ref _v2, ref _v3, ref _v4);
                double.TryParse(_v1, out valor);
                ValIntereses = valor;
            }
            return false;
        }

        // Overload without ref ValCesantias/ValIntereses
        public bool BuscaAnticiposCesantiasLiquidados(int idnomina, double idempleado, int planillaini, int planillafin, OdbcConnection myconnect, ref double ValCesantias)
        {
            double ValIntereses = 0;
            return BuscaAnticiposCesantiasLiquidados(idnomina, idempleado, planillaini, planillafin, myconnect, ref ValCesantias, ref ValIntereses);
        }

        public DataSet ConsolidaCesantias(int Idnomina, int CicloIni, int CicloFin, DateTime FecPromedios,
            DateTime FecCausaIni, DateTime FecCausaFin, int DiasTope, string CenCosto, string Compronte, double numdomto, DateTime fecmovto,
            bool ActCnt, Form Myforma, string Usuario, OdbcConnection myconnect)
        {
            DateTime FechaIni, FechaFin;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Consolidando Cesantias", Myforma);
            DataSet DsDataset = new DataSet();
            DateTime fecIniCesant, fecFinCesant;
            double DiasLiquidados = 0;
            int DiasCesant = 0;
            double ValAuxTra = 0, TopeAux = 0;
            int IdTrasporte = 0;
            double IdAntCesantia = 0;
            double ValorCesantias = 0, InteresesCesantias = 0;
            double Fila = 0;
            double IdCesantia = 0, salariobase = 0;
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            double SaldoCuentaProv = 0, SaldoAnticipo = 0;
            DataSet dsdata = new DataSet();
            int estado = 0;
            double dif = 0;
            string CtaGastoCesan = "999999999999", CtaProvCesan = "999999999999", CtaGontraCesan = "999999999999", CtaAntCesan = "999999999999";
            DataSet dsimpresion = new DataSet();
            int SW1 = 0;
            string tipoaux = "", numdocaux = "0", mandocaux = "0";
            int DiasCesantias = 0;

            dsimpresion.Tables.Add("tblimpresion");
            dsimpresion.Tables["tblimpresion"].Columns.Add("cedula", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("apellidos", CtaProvCesan.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("nombres", CtaProvCesan.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("provision", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("anticipo", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("cesantias", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("dias", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("valordia", Fila.GetType());

            ok = this.msgconfig.BuscaPeriodosPagos(CicloIni, Idnomina.ToString(), myconnect, DsDataset);
            if (ok)
            {
                DataRow rowPer = DsDataset.Tables["tblperpagos"].Rows[0];
                fecIniCesant = Convert.ToDateTime(rowPer["Fecinicial"]);
                fecFinCesant = Convert.ToDateTime(rowPer["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de Cesantias no estan creados");
                return null;
            }

            ok = this.msgconfig.BuscaPeriodosPagos(CicloFin, Idnomina.ToString(), myconnect, DsDataset);
            if (ok)
            {
                DataRow rowPer = DsDataset.Tables["tblperpagos"].Rows[0];
                fecFinCesant = Convert.ToDateTime(rowPer["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de Cesantias no estan creados");
                return null;
            }

            DiasCesant = this.msgconfig.CalculaDias(fecFinCesant, fecIniCesant);

            ok = this.msgconfig.BuscaEmpresa(Idnomina, myconnect, DsDataset);
            if (ok)
            {
                DataRow rowEmpresa = DsDataset.Tables["tblempresas"].Rows[0];
                IdTrasporte = Convert.ToInt32(rowEmpresa["IdTrasporte"]);
                IdAntCesantia = Convert.ToDouble(rowEmpresa["IdAntCesan"]);
                IdCesantia = Convert.ToDouble(rowEmpresa["IdCesantias"]);

                this.msgconfig.BuscaCptos(IdTrasporte.ToString(), myconnect, DsDataset);
                DataRow rowCpto = DsDataset.Tables["tblcptos"].Rows[0];
                ValAuxTra = Convert.ToDouble(rowCpto["Valor"]);
                TopeAux = Convert.ToDouble(rowCpto["Saltope"]);
            }

            msgbarra.Show();
            this.CalculaPromedioCesantias(Idnomina, CicloIni, CicloFin, FecCausaIni, FecCausaFin, DiasCesant, TopeAux, ValAuxTra, FecPromedios, msgbarra, myconnect);
            this.msgconfig.BuscaEmpleado(Idnomina, myconnect, Idnomina.ToString(), ref DsDataset, CenCosto);

            if (ActCnt)
            {
                if (numdomto == 0)
                {
                    numdomto = 0;
                    msgcnt.BuscaComprobante(ref Compronte, ref numdomto, true, myconnect);
                }

                ok = msgcnt.BuscaComprobante(ref Compronte, ref numdomto, false, myconnect);
                if (ok)
                {
                    MessageBox.Show("comprobante ya existe en contabilidad, no se permite contabilizar", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return null;
                }
            }

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblempleados"].Rows.Count);

            for (Fila = 0; Fila <= DsDataset.Tables["tblempleados"].Rows.Count - 1; Fila++)
            {
                DataRow row = DsDataset.Tables["tblempleados"].Rows[(int)Fila];
                SaldoAnticipo = 0; SaldoCuentaProv = 0; SW1 = 0;

                ValorCesantias = Convert.ToDouble(row["PromCes"]) * 30;
                DiasCesantias = Convert.ToInt32(row["diascesan"]);

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                {
                    row["fecing"] = Convert.ToDateTime(row["FecReingreso"]);
                }

                DiasCesantias = this.msgconfig.CalculaDias(FecCausaFin, Convert.ToDateTime(row["fecing"]));
                if (FecCausaFin.Month == 2)
                {
                    if (FecCausaFin.Day == 28)
                    {
                        DiasCesantias += 2;
                    }
                    else if (FecCausaFin.Day > 28)
                    {
                        DiasCesantias += 1;
                    }
                }

                if (DiasCesantias > 360)
                {
                    DiasCesantias = 360;
                }

                ValorCesantias = Math.Round(ValorCesantias * (DiasCesantias / 360.0), 0);

                this.BuscaAnticiposCesantiasLiquidados(Idnomina, Convert.ToDouble(row["idempleado"]), CicloIni, CicloFin, myconnect, ref SaldoAnticipo);

                if (IdAntCesantia != 0)
                {
                    ok = this.msgconfig.BuscaCuentasContables(Convert.ToInt32(IdAntCesantia), row["idcencos"].ToString(), myconnect, dsdata);
                    if (ok)
                    {
                        CtaAntCesan = dsdata.Tables["tblcuentas"].Rows[0]["ctagasto"].ToString();
                    }
                    else
                    {
                        MessageBox.Show("Parametros de cuentas contables no existe. Concepto " + IdAntCesantia + " Cen. Costo " + row["idcencos"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }
                }

                ok = this.msgconfig.BuscaCuentasContables(Convert.ToInt32(IdCesantia), row["idcencos"].ToString(), myconnect, dsdata);
                if (ok)
                {
                    CtaGastoCesan = dsdata.Tables["tblcuentas"].Rows[0]["ctagasto"].ToString();
                    CtaProvCesan = dsdata.Tables["tblcuentas"].Rows[0]["ctaprov"].ToString();
                    CtaGontraCesan = dsdata.Tables["tblcuentas"].Rows[0]["ctacontra"].ToString();
                }
                else
                {
                    MessageBox.Show("Parametros de cuentas contables no existe. Concepto " + IdCesantia + " Cen. Costo " + row["idcencos"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SW1 = 1;
                }

                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    if (Convert.ToDateTime(Convert.ToDateTime(row["FecRetiro"]).ToString("dd/MM/yyyy")) <= Convert.ToDateTime(FecCausaFin.ToString("dd/MM/yyyy")))
                    {
                        SW1 = 1;
                    }
                }

                switch (row["tipempleado"].ToString())
                {
                    case "2":
                    case "3":
                        SW1 = 1;
                        break;
                }

                switch (row["clasesalario"].ToString())
                {
                    case "6":
                    case "7":
                        SW1 = 1;
                        break;
                }

                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["Fecing"]))
                {
                    row["Fecing"] = Convert.ToDateTime(row["FecReingreso"]);
                }

                if (Convert.ToDateTime(row["Fecing"]) > FecCausaFin)
                {
                    SW1 = 1;
                }

                if (SW1 == 0)
                {
                    SaldoCuentaProv = msgcnt.BuscarSaldoTecero(CtaProvCesan, row["cedula"].ToString(), FecPromedios.ToString("yyyyMM"), myconnect);
                    if (SaldoCuentaProv != 0)
                    {
                        SaldoCuentaProv = SaldoCuentaProv * -1;
                    }

                    dsimpresion.Tables["tblimpresion"].Rows.Add(row["idempleado"], row["apellidos"], row["nombres"], SaldoCuentaProv, SaldoAnticipo, ValorCesantias, DiasCesantias, Convert.ToDouble(row["PromCes"]));

                    if (ActCnt)
                    {
                        if (SaldoCuentaProv != 0)
                        {
                            // BuscarCuenta: need all ref params up to TipoAuxiliar (param 12)
                            string _tercero = "", _mane = "", _natura = "", _cencos = "";
                            string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                            decimal _tasa = 0; int _estado = 0;
                            string _aplicnt = "", _consibaca = "", _banco = "";
                            string _ctaRef = CtaProvCesan;
                            msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                            if (Convert.ToDouble(mandocaux) > 0)
                            {
                                tipoaux = "CC";
                                numdocaux = fecmovto.ToString("yyyyMMdd");
                            }
                            else
                            {
                                tipoaux = "";
                                numdocaux = "";
                            }
                            msgcnt.GrabaMovimiento(Compronte, numdomto, CtaProvCesan, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE CESANTIAS", numdocaux, SaldoCuentaProv, 0, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE CESANTIAS", 0, null, null, tipoaux);
                        }

                        if (SaldoAnticipo != 0)
                        {
                            string _tercero = "", _mane = "", _natura = "", _cencos = "";
                            string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                            decimal _tasa = 0; int _estado = 0;
                            string _aplicnt = "", _consibaca = "", _banco = "";
                            string _ctaRef = CtaAntCesan;
                            msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                            if (Convert.ToDouble(mandocaux) > 0)
                            {
                                tipoaux = "CC";
                                numdocaux = fecmovto.ToString("yyyyMMdd");
                            }
                            else
                            {
                                tipoaux = "";
                                numdocaux = "";
                            }
                            msgcnt.GrabaMovimiento(Compronte, numdomto, CtaAntCesan, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE CESANTIAS", numdocaux, 0, SaldoAnticipo, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE CESANTIAS", 0, null, null, tipoaux);
                            ValorCesantias = ValorCesantias - SaldoAnticipo;
                        }

                        if (ValorCesantias > 0)
                        {
                            string _tercero = "", _mane = "", _natura = "", _cencos = "";
                            string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                            decimal _tasa = 0; int _estado = 0;
                            string _aplicnt = "", _consibaca = "", _banco = "";
                            string _ctaRef = CtaGontraCesan;
                            msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                            if (Convert.ToDouble(mandocaux) > 0)
                            {
                                tipoaux = "CC";
                                numdocaux = fecmovto.ToString("yyyyMMdd");
                            }
                            else
                            {
                                tipoaux = "";
                                numdocaux = "";
                            }
                            msgcnt.GrabaMovimiento(Compronte, numdomto, CtaGontraCesan, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE CESANTIAS", numdocaux, 0, ValorCesantias, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE CESANTIAS", 0, null, null, tipoaux);
                        }

                        {
                            string _tercero = "", _mane = "", _natura = "", _cencos = "";
                            string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                            decimal _tasa = 0; int _estado = 0;
                            string _aplicnt = "", _consibaca = "", _banco = "";
                            string _ctaRef = CtaGastoCesan;
                            msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                            if (Convert.ToDouble(mandocaux) > 0)
                            {
                                tipoaux = "CC";
                                numdocaux = fecmovto.ToString("yyyyMMdd");
                            }
                            else
                            {
                                tipoaux = "";
                                numdocaux = "";
                            }

                            dif = SaldoCuentaProv - (ValorCesantias + SaldoAnticipo);
                            if (dif > 0)
                            {
                                msgcnt.GrabaMovimiento(Compronte, numdomto, CtaGastoCesan, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE CESANTIAS", numdocaux, 0, dif, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE CESANTIAS", 0, null, null, tipoaux);
                            }
                            else if (dif < 0)
                            {
                                dif = dif * -1;
                                msgcnt.GrabaMovimiento(Compronte, numdomto, CtaGastoCesan, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE CESANTIAS", numdocaux, dif, 0, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE CESANTIAS", 0, null, null, tipoaux);
                            }
                        }
                    }
                }

                msgbarra.PerformStep();
            }

            if (ActCnt)
            {
                if (Fila > 0)
                {
                    ok = msgcnt.CierreDocumento(Compronte, numdomto, myconnect);
                    this.msgimp.ImprimirComprobante(Compronte, numdomto, false, Usuario, myconnect);
                }
            }

            msgbarra.Close();
            msgbarra.Dispose();

            return dsimpresion;
        }

        public DataSet ConsolidarVacaciones(int Idnomina, int CicloIni, int CicloFin, DateTime FecPromedios,
            string CenCosto, string Compronte, double numdomto, DateTime fecmovto,
            bool ActCnt, Form Myforma, string Usuario, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Consolidando Vacaciones", Myforma);
            DataSet dsimpresion = new DataSet();
            double Fila = 0;
            int SW1 = 0;
            string tipoaux = "", numdocaux = "0", mandocaux = "0";
            DataSet DsDataset = new DataSet(), dsdata = new DataSet();
            DateTime fecIniCesant = default(DateTime), fecFinCesant = default(DateTime);
            int DiasCesant = 0;
            double DiasLiquidados = 0, ValorDiaVac = 0;
            double ValAuxTra = 0, TopeAux = 0;
            int IdTrasporte = 0, IdVacConsol = 0;
            double ValorVacaciones = 0;
            DateTime fecIngreso;
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            double SaldoCuentaConsol = 0;
            string CtaVacConsl = "999999999999", CtaGastoVac = "999999999999";
            double ValorConsolidar = 0;
            double Debito = 0, Credito = 0;
            string tercero = "N";
            int IdVacProv = 0;
            string CtaVacProv = "999999999999";
            double SaldoCuentaProv = 0, ValorGasto = 0;

            dsimpresion.Tables.Add("tblimpresion");
            dsimpresion.Tables["tblimpresion"].Columns.Add("cedula", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("apellidos", tipoaux.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("nombres", tipoaux.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("vacaciones", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("diasLiq", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("vlrdiavac", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("vlrconsolidado", Fila.GetType());
            dsimpresion.Tables["tblimpresion"].Columns.Add("fecultvaca", fecIniCesant.GetType());

            ok = this.msgconfig.BuscaPeriodosPagos(CicloIni, Idnomina.ToString(), myconnect, DsDataset);
            if (ok)
            {
                DataRow rowPer = DsDataset.Tables["tblperpagos"].Rows[0];
                fecIniCesant = Convert.ToDateTime(rowPer["Fecinicial"]);
                fecFinCesant = Convert.ToDateTime(rowPer["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de vacaciones no estan creados");
                return null;
            }

            ok = this.msgconfig.BuscaPeriodosPagos(CicloFin, Idnomina.ToString(), myconnect, DsDataset);
            if (ok)
            {
                DataRow rowPer = DsDataset.Tables["tblperpagos"].Rows[0];
                fecFinCesant = Convert.ToDateTime(rowPer["FechaFinal"]);
            }
            else
            {
                MessageBox.Show("Periodos de pago de vacaciones no estan creados");
                return null;
            }

            DiasCesant = this.msgconfig.CalculaDias(fecFinCesant, fecIniCesant);

            ok = this.msgconfig.BuscaEmpresa(Idnomina, myconnect, DsDataset);
            if (ok)
            {
                DataRow rowEmpresa = DsDataset.Tables["tblempresas"].Rows[0];
                IdTrasporte = Convert.ToInt32(rowEmpresa["IdTrasporte"]);
                IdVacConsol = Convert.ToInt32(rowEmpresa["idvacconsol"]);
                IdVacProv = Convert.ToInt32(rowEmpresa["IdVacasiones"]);

                this.msgconfig.BuscaCptos(IdTrasporte.ToString(), myconnect, DsDataset);
                DataRow rowCpto = DsDataset.Tables["tblcptos"].Rows[0];
                ValAuxTra = Convert.ToDouble(rowCpto["Valor"]);
                TopeAux = Convert.ToDouble(rowCpto["Saltope"]);
            }

            msgbarra.Show();
            this.CalculaPromedioVacaciones(Idnomina, CicloIni, CicloFin, fecIniCesant, fecFinCesant, DiasCesant, TopeAux, ValAuxTra, FecPromedios, msgbarra, myconnect);
            this.msgconfig.BuscaEmpleado(Idnomina, myconnect, Idnomina.ToString(), ref DsDataset, CenCosto);

            if (ActCnt)
            {
                if (numdomto == 0)
                {
                    numdomto = 0;
                    msgcnt.BuscaComprobante(ref Compronte, ref numdomto, true, myconnect);
                }

                ok = msgcnt.BuscaComprobante(ref Compronte, ref numdomto, false, myconnect);
                if (ok)
                {
                    MessageBox.Show("comprobante ya existe en contabilidad, no se permite contabilizar", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return null;
                }
            }

            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["tblempleados"].Rows.Count);

            for (Fila = 0; Fila <= DsDataset.Tables["tblempleados"].Rows.Count - 1; Fila++)
            {
                ValorVacaciones = 0; SW1 = 0; fecIngreso = new DateTime(1950, 1, 1); ValorGasto = 0;
                Debito = 0; Credito = 0; tercero = "N"; SaldoCuentaConsol = 0; SaldoCuentaProv = 0;

                DataRow row = DsDataset.Tables["tblempleados"].Rows[(int)Fila];

                if (Convert.ToInt32(row["estado"]) == 2)
                {
                    if (Convert.ToDateTime(Convert.ToDateTime(row["FecRetiro"]).ToString("dd/MM/yyyy")) <= Convert.ToDateTime(FecPromedios.ToString("dd/MM/yyyy")))
                    {
                        SW1 = 1;
                    }
                }

                switch (row["tipempleado"].ToString())
                {
                    case "2":
                    case "3":
                        SW1 = 1;
                        break;
                }

                switch (row["clasesalario"].ToString())
                {
                    case "6":
                    case "7":
                        SW1 = 1;
                        break;
                }

                fecIngreso = Convert.ToDateTime(row["Fecing"]);
                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["Fecing"]))
                {
                    fecIngreso = Convert.ToDateTime(row["FecReingreso"]);
                }

                if (fecIngreso > FecPromedios)
                {
                    SW1 = 1;
                }

                if (SW1 == 0)
                {
                    ok = this.msgconfig.BuscaCuentasContables(IdVacConsol, row["idcencos"].ToString(), myconnect, dsdata);
                    if (ok)
                    {
                        CtaGastoVac = dsdata.Tables["tblcuentas"].Rows[0]["ctagasto"].ToString();
                        CtaVacConsl = dsdata.Tables["tblcuentas"].Rows[0]["ctaprov"].ToString();
                    }
                    else
                    {
                        MessageBox.Show("Parametros de cuentas contables no existe. Concepto " + IdVacConsol + " Cen. Costo " + row["idcencos"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }

                    ok = this.msgconfig.BuscaCuentasContables(IdVacProv, row["idcencos"].ToString(), myconnect, dsdata);
                    if (ok)
                    {
                        CtaVacProv = dsdata.Tables["tblcuentas"].Rows[0]["ctaprov"].ToString();
                    }
                    else
                    {
                        MessageBox.Show("Parametros de cuentas contables no existe. Concepto " + IdVacProv + " Cen. Costo " + row["idcencos"], "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }

                    {
                        string _tercero2 = tercero, _mane = "", _natura = "", _cencos = "";
                        string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                        decimal _tasa = 0; int _estado = 0;
                        string _aplicnt = "", _consibaca = "", _banco = "";
                        string _ctaRef = CtaVacConsl;
                        ok = msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero2, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                        tercero = _tercero2;
                        if (!ok)
                        {
                            MessageBox.Show("Cuenta contable del concepto " + IdVacConsol + " Cen. Costo " + row["idcencos"] + " no existe en el plan de cuentas", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }
                        else
                        {
                            if (tercero == "N")
                            {
                                MessageBox.Show("Cuenta contable del concepto " + IdVacConsol + " Cen. Costo " + row["idcencos"] + " debe exigir tercero", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                SW1 = 1;
                            }
                        }
                    }

                    {
                        string _tercero2 = tercero, _mane = "", _natura = "", _cencos = "";
                        string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                        decimal _tasa = 0; int _estado = 0;
                        string _aplicnt = "", _consibaca = "", _banco = "";
                        string _ctaRef = CtaVacProv;
                        ok = msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero2, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                        if (!ok)
                        {
                            MessageBox.Show("Cuenta contable del concepto " + IdVacProv + " Cen. Costo " + row["idcencos"] + " no existe en el plan de cuentas", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            SW1 = 1;
                        }
                    }

                    if (CtaVacConsl == "999999999999")
                    {
                        MessageBox.Show("Cuenta contable consolidada del concepto " + IdVacConsol + " Cen. Costo " + row["idcencos"] + " no debe ser 999999999999", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }

                    if (CtaVacProv == "999999999999")
                    {
                        MessageBox.Show("Cuenta contable consolidada del concepto " + IdVacProv + " Cen. Costo " + row["idcencos"] + " no debe ser 999999999999", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }

                    if (CtaGastoVac == "999999999999")
                    {
                        MessageBox.Show("Cuenta contable del gasto del concepto " + IdVacConsol + " Cen. Costo " + row["idcencos"] + " no debe ser 999999999999", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SW1 = 1;
                    }
                }

                if (SW1 == 0)
                {
                    int DiasLiquidadosInt = 0;
                    ValorVacaciones = this.LiquidaVacaciones(FecPromedios, fecIngreso, Convert.ToDateTime(row["feccauvac"]), Convert.ToDouble(row["Salario"]), 0, Convert.ToDouble(row["PromeVac"]), ref DiasLiquidadosInt, ref ValorDiaVac);
                    DiasLiquidados = DiasLiquidadosInt;

                    SaldoCuentaConsol = msgcnt.BuscarSaldoTecero(CtaVacConsl, row["cedula"].ToString(), FecPromedios.ToString("yyyyMM"), myconnect);
                    SaldoCuentaConsol = SaldoCuentaConsol * -1;

                    if (SaldoCuentaConsol > 0)
                    {
                        ValorConsolidar = ValorVacaciones - SaldoCuentaConsol;
                    }
                    else
                    {
                        ValorConsolidar = ValorVacaciones;
                    }

                    SaldoCuentaProv = msgcnt.BuscarSaldoTecero(CtaVacProv, row["cedula"].ToString(), FecPromedios.ToString("yyyyMM"), myconnect);
                    if (SaldoCuentaProv != 0)
                    {
                        SaldoCuentaProv = SaldoCuentaProv * -1;
                    }

                    if (ActCnt)
                    {
                        if (ValorConsolidar > 0)
                        {
                            Debito = 0; Credito = ValorConsolidar;
                        }
                        else if (ValorConsolidar < 0)
                        {
                            Debito = ValorConsolidar * -1; Credito = 0;
                        }

                        if (Debito != 0 || Credito != 0)
                        {
                            // Registro del valor de vacaciones a consolidar
                            {
                                string _tercero2 = "", _mane = "", _natura = "", _cencos = "";
                                string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                                decimal _tasa = 0; int _estado = 0;
                                string _aplicnt = "", _consibaca = "", _banco = "";
                                string _ctaRef = CtaVacConsl;
                                msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero2, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                            }
                            if (Convert.ToDouble(mandocaux) > 0)
                            {
                                tipoaux = "CV";
                                numdocaux = fecmovto.ToString("yyyyMMdd");
                            }
                            else
                            {
                                tipoaux = "";
                                numdocaux = "";
                            }

                            msgcnt.GrabaMovimiento(Compronte, numdomto, CtaVacConsl, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE VACACIONES", numdocaux, Debito, Credito, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE VACACIONES", 0, null, null, tipoaux);

                            // Movimiento que saca de la provision
                            if (SaldoCuentaProv > 0)
                            {
                                Debito = SaldoCuentaProv; Credito = 0;
                            }
                            else if (SaldoCuentaProv < 0)
                            {
                                Debito = 0; Credito = SaldoCuentaProv * -1;
                            }

                            if (Debito != 0 || Credito != 0)
                            {
                                {
                                    string _tercero2 = "", _mane = "", _natura = "", _cencos = "";
                                    string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                                    decimal _tasa = 0; int _estado = 0;
                                    string _aplicnt = "", _consibaca = "", _banco = "";
                                    string _ctaRef = CtaVacProv;
                                    msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero2, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                                }
                                if (Convert.ToDouble(mandocaux) > 0)
                                {
                                    tipoaux = "CV";
                                    numdocaux = fecmovto.ToString("yyyyMMdd");
                                }
                                else
                                {
                                    tipoaux = "";
                                    numdocaux = "";
                                }

                                msgcnt.GrabaMovimiento(Compronte, numdomto, CtaVacProv, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE VACACIONES", numdocaux, Debito, Credito, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE VACACIONES", 0, null, null, tipoaux);
                            }

                            // Movimiento que se lleva al gasto
                            ValorGasto = (ValorConsolidar - SaldoCuentaProv);

                            if (ValorGasto > 0)
                            {
                                Debito = ValorGasto; Credito = 0;
                            }
                            else if (ValorGasto < 0)
                            {
                                Debito = 0; Credito = ValorGasto * -1;
                            }

                            if (Debito != 0 || Credito != 0)
                            {
                                {
                                    string _tercero2 = "", _mane = "", _natura = "", _cencos = "";
                                    string _nivel = "", _aplicart = "", _aplites = "", _nombre = "";
                                    decimal _tasa = 0; int _estado = 0;
                                    string _aplicnt = "", _consibaca = "", _banco = "";
                                    string _ctaRef = CtaGastoVac;
                                    msgcnt.BuscarCuenta(ref _ctaRef, myconnect, ref _tercero2, ref _mane, ref _natura, ref _cencos, ref _nivel, ref _aplicart, ref _aplites, ref _nombre, ref _tasa, ref mandocaux, ref _estado, ref _aplicnt, ref _consibaca, ref _banco);
                                }
                                if (Convert.ToDouble(mandocaux) > 0)
                                {
                                    tipoaux = "CV";
                                    numdocaux = fecmovto.ToString("yyyyMMdd");
                                }
                                else
                                {
                                    tipoaux = "";
                                    numdocaux = "";
                                }
                                msgcnt.GrabaMovimiento(Compronte, numdomto, CtaGastoVac, "9999", fecmovto.ToString("yyyyMM"), row["cedula"].ToString(), fecmovto, "CONSOLIDACION DE VACACIONES", numdocaux, Debito, Credito, 0, Usuario, myconnect, 0, tipoaux + "-" + numdocaux, "99999999999999", row["idcencos"].ToString(), "CONSOLIDACION DE VACACIONES", 0, null, null, tipoaux);
                            }
                        }
                    }

                    dsimpresion.Tables["tblimpresion"].Rows.Add(row["idempleado"], row["apellidos"], row["nombres"], ValorVacaciones, DiasLiquidados, ValorDiaVac, ValorConsolidar, Convert.ToDateTime(row["feccauvac"]));
                }

                msgbarra.PerformStep();
            }

            if (ActCnt)
            {
                if (Fila > 0)
                {
                    ok = msgcnt.CierreDocumento(Compronte, numdomto, myconnect);
                    this.msgimp.ImprimirComprobante(Compronte, numdomto, false, Usuario, myconnect);
                }
            }

            msgbarra.Close();
            msgbarra.Dispose();

            return dsimpresion;
        }

        private double CalculaPorcRetencion(int Idempresa, int TipoProcedimiento, double IngresoGravado, OdbcConnection myconnect, ref string StFormula)
        {
            DataSet dsdataset = new DataSet();
            double ValorUVt = 0, UnidadUvt = 0, BaseRteFte = 0;
            double Tarifa = 0, UvtAdicional = 0;
            int UvtInicial = 0;

            this.msgconfig.BuscaEmpresa(Idempresa, myconnect, dsdataset);

            DataRow rowEmpresa = dsdataset.Tables["tblempresas"].Rows[0];
            ValorUVt = Convert.ToDouble(rowEmpresa["vlruvt"]);

            if (ValorUVt != 0)
            {
                UnidadUvt = Math.Round(IngresoGravado / ValorUVt, 4);
                StFormula = "UVT= " + IngresoGravado + "/" + ValorUVt + "=" + UnidadUvt;
            }
            else
            {
                MessageBox.Show("No esta parametrizado el valor del UVT, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            ok = this.msgconfig.BuscarParametrosRetencion(Idempresa, (int)UnidadUvt, myconnect, dsdataset);
            if (ok)
            {
                DataRow rowRet = dsdataset.Tables["tblparretfte"].Rows[0];
                Tarifa = Convert.ToDouble(rowRet["tarifa"]) / 100.0;
                UvtAdicional = Convert.ToDouble(rowRet["uvtadicional"]);
                UvtInicial = Convert.ToInt32(rowRet["uvtinicial"]);

                BaseRteFte = Math.Round(((UnidadUvt - UvtInicial) * Tarifa) + UvtAdicional, 5);

                StFormula = StFormula + "\r\n" + "% Retener=((" + UnidadUvt + "-" + UvtInicial + ") * " + Tarifa + " + " + UvtAdicional;

                if (TipoProcedimiento == 2)
                {
                    StFormula = StFormula + "\r\n" + BaseRteFte + " / " + UnidadUvt + " * 100 ";
                    BaseRteFte = Math.Round((BaseRteFte / UnidadUvt) * 100, 4);
                }

                StFormula = StFormula + " = ";
            }

            return BaseRteFte;
        }

        // Overload without ref StFormula
        private double CalculaPorcRetencion(int Idempresa, int TipoProcedimiento, double IngresoGravado, OdbcConnection myconnect)
        {
            string StFormula = "";
            return CalculaPorcRetencion(Idempresa, TipoProcedimiento, IngresoGravado, myconnect, ref StFormula);
        }

        public DataSet LiquidacionRetFuente(int Idempresa, int TipoProcedimiento, int CicloInicial, int CicloFinal, string CenCosto, Form myforma, OdbcConnection myconnect)
        {
            DataSet dsprocedimiento = new DataSet(), dsdataset = new DataSet(), dsdataliq = new DataSet();
            int Sw1 = 0, Sw2 = 0;
            double fila = 0, fila2 = 0;
            string StString = "";
            double IdEmpleado = 0;
            StringBuilder StBuilder = new StringBuilder();
            double PagosLaborales = 0, DeducionesLaborales = 0, DeducionesLaboralesEPS = 0, PorcRentaExc = 0, PorcDeducExc = 0;
            double NetoIngLab = 0, SalarioPromedio = 0, Descuentos = 0, TopeDeducible = 0, BaseRetencion = 0;
            string StFormula = "";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Calculando retencion en la fuente Proced. " + TipoProcedimiento, myforma);
            int Meses = 0;
            DateTime fecIniCiclo = default(DateTime), fecFinCiclo = default(DateTime), FecIng = default(DateTime), fecini = default(DateTime), fecfin = default(DateTime);

            dsprocedimiento.Tables.Add("tblproc");

            dsprocedimiento.Tables["tblproc"].Columns.Add("idnomina", StString.GetType());
            dsprocedimiento.Tables["tblproc"].Columns.Add("idempleado", StString.GetType());
            dsprocedimiento.Tables["tblproc"].Columns.Add("Nombre", StString.GetType());
            dsprocedimiento.Tables["tblproc"].Columns.Add("IdCpto", Sw1.GetType());
            dsprocedimiento.Tables["tblproc"].Columns.Add("Descripcion", StString.GetType());
            dsprocedimiento.Tables["tblproc"].Columns.Add("Natur", StString.GetType());
            dsprocedimiento.Tables["tblproc"].Columns.Add("Valor", fila.GetType());
            dsprocedimiento.Tables["tblproc"].Columns.Add("ValorLimite", fila.GetType());

            this.msgconfig.BuscaEmpresa(Idempresa, myconnect, dsdataset);

            ok = this.msgconfig.BuscaEmpleado(Idempresa, myconnect, Idempresa.ToString(), ref dsdataset, CenCosto);

            msgbarra.Show();

            if (ok)
            {
                DataRow rowEmpresa = dsdataset.Tables["tblempresas"].Rows[0];
                PorcRentaExc = Convert.ToDouble(rowEmpresa["porcrenta"]);
                PorcDeducExc = Convert.ToDouble(rowEmpresa["porcdeduc"]);

                msgbarra.ValorMinimoMaximo(0, dsdataset.Tables["tblempleados"].Rows.Count);

                ok = this.msgconfig.BuscaPeriodosPagos(CicloInicial, Idempresa.ToString(), myconnect, dsdataset);
                if (ok)
                {
                    DataRow rowPer = dsdataset.Tables["tblperpagos"].Rows[0];
                    fecIniCiclo = Convert.ToDateTime(rowPer["Fecinicial"]);
                    fecFinCiclo = Convert.ToDateTime(rowPer["FechaFinal"]);
                }
                else
                {
                    MessageBox.Show("Periodos de pago de Cesantias no estan creados");
                    return null;
                }

                ok = this.msgconfig.BuscaPeriodosPagos(CicloFinal, Idempresa.ToString(), myconnect, dsdataset);
                if (ok)
                {
                    DataRow rowPer = dsdataset.Tables["tblperpagos"].Rows[0];
                    fecFinCiclo = Convert.ToDateTime(rowPer["FechaFinal"]);
                }
                else
                {
                    MessageBox.Show("Periodos de pago de Cesantias no estan creados");
                    return null;
                }

                for (fila = 0; fila <= dsdataset.Tables["tblempleados"].Rows.Count - 1; fila++)
                {
                    DataRow row = dsdataset.Tables["tblempleados"].Rows[(int)fila];
                    Sw1 = 0;

                    if (Convert.ToInt32(row["estado"]) == 2)
                    {
                        Sw1 = 1;
                    }

                    switch (Convert.ToInt32(row["ClaseSalario"]))
                    {
                        case 6:
                        case 7:
                            Sw1 = 1;
                            break;
                    }

                    if (Sw1 == 0)
                    {
                        fecini = fecIniCiclo;
                        fecfin = fecFinCiclo;

                        switch (TipoProcedimiento)
                        {
                            case 1:
                                Meses = 1;
                                break;
                            case 2:
                                if (Convert.ToDateTime(row["FecReingreso"]) > Convert.ToDateTime(row["fecing"]))
                                {
                                    row["fecing"] = row["FecReingreso"];
                                }

                                FecIng = Convert.ToDateTime(row["fecing"]);

                                if (FecIng > fecini)
                                {
                                    fecini = FecIng;
                                }

                                Meses = (int)(DateAndTime.DateDiff(DateInterval.Month, fecini, fecfin)) + 1;

                                if (Meses >= 12)
                                {
                                    Meses = 13;
                                }
                                break;
                        }

                        StBuilder.Append("select idempleado,idcpto,natur,idcencos,IdSeccion,nombre,sum(valor) as Valor ");
                        StBuilder.Append("from NOM_LIQPLAN12_VW ");
                        StBuilder.Append("where idempleado=" + row["idempleado"] + " and idnomina=" + Idempresa + " ");
                        StBuilder.Append(" and idplanilla between " + CicloInicial + " and " + CicloFinal + " ");
                        StBuilder.Append("group by idempleado,idcpto,natur,idcencos,IdSeccion,nombre ");
                        StBuilder.Append("order by idempleado,natur,idcpto ");

                        dsdataliq.Tables.Clear();

                        ok = (this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "LiquidacionRetFuente", ref dsdataliq, "tbldatos") != null);

                        StBuilder.Remove(0, StBuilder.Length);

                        PagosLaborales = 0; DeducionesLaborales = 0; DeducionesLaboralesEPS = 0; StFormula = "";

                        for (fila2 = 0; fila2 <= dsdataliq.Tables["tbldatos"].Rows.Count - 1; fila2++)
                        {
                            DataRow rowDat = dsdataliq.Tables["tbldatos"].Rows[(int)fila2];
                            Sw2 = 0;

                            if (rowDat["Valor"] is DBNull)
                            {
                                rowDat["Valor"] = 0;
                            }

                            switch (Convert.ToInt32(rowDat["natur"]))
                            {
                                case 1:
                                    PagosLaborales += Convert.ToDouble(rowDat["Valor"]);
                                    break;
                                case 2:
                                    ok = this.msgconfig.BuscaEps(Convert.ToInt32(rowDat["idcpto"]), myconnect);
                                    if (!ok)
                                    {
                                        DeducionesLaborales += Convert.ToDouble(rowDat["valor"]);
                                    }
                                    else
                                    {
                                        DeducionesLaboralesEPS += Convert.ToDouble(rowDat["valor"]);
                                        Sw2 = 1;
                                    }
                                    rowDat["valor"] = Convert.ToDouble(rowDat["valor"]) * -1;
                                    break;
                            }

                            if (Sw2 == 0)
                            {
                                dsprocedimiento.Tables["tblproc"].Rows.Add(Idempresa, rowDat["idempleado"], dsdataset.Tables["tblempleados"].Rows[(int)fila]["apellidos"].ToString() + " " + dsdataset.Tables["tblempleados"].Rows[(int)fila]["nombres"].ToString(), rowDat["idcpto"], rowDat["nombre"], rowDat["natur"], rowDat["valor"], 0);
                            }
                        }

                        NetoIngLab = PagosLaborales - DeducionesLaborales;

                        switch (TipoProcedimiento)
                        {
                            case 1:
                                SalarioPromedio = NetoIngLab;
                                break;
                            case 2:
                                SalarioPromedio = Math.Round(NetoIngLab / Meses, 0);
                                break;
                        }

                        dsprocedimiento.Tables["tblproc"].Rows.Add(Idempresa, row["idempleado"], row["apellidos"].ToString() + " " + row["nombres"].ToString(), 0, "PROMEDIO MENSUAL", "3", SalarioPromedio, 0);

                        Descuentos = Math.Round(SalarioPromedio * (PorcRentaExc / 100.0), 0);

                        SalarioPromedio = SalarioPromedio - Descuentos;

                        dsprocedimiento.Tables["tblproc"].Rows.Add(Idempresa, row["idempleado"], row["apellidos"].ToString() + " " + row["nombres"].ToString(), 0, "RENTA EXCENTA " + PorcRentaExc + "%", "4", Descuentos * -1, 0);

                        TopeDeducible = Math.Round(SalarioPromedio * (PorcDeducExc / 100.0), 0);

                        if (Convert.ToDouble(row["vlrdeduciblertefte"]) > TopeDeducible)
                        {
                            Descuentos = TopeDeducible;
                        }
                        else
                        {
                            Descuentos = Convert.ToDouble(row["vlrdeduciblertefte"]);
                        }

                        if (Meses >= 12)
                        {
                            Meses = 12;
                        }

                        // Se toma el aporte promedio mensual a EPS.
                        DeducionesLaboralesEPS = Math.Round(DeducionesLaboralesEPS / Meses, 0);

                        dsprocedimiento.Tables["tblproc"].Rows.Add(Idempresa, row["idempleado"], row["apellidos"].ToString() + " " + row["nombres"].ToString(), 0, "DEDUCIBLES VIV, MED.PREP, EDUC. ($" + row["vlrdeduciblertefte"] + ")(" + PorcDeducExc + "%)", "5", Descuentos * -1, TopeDeducible);

                        dsprocedimiento.Tables["tblproc"].Rows.Add(Idempresa, row["idempleado"], row["apellidos"].ToString() + " " + row["nombres"].ToString(), 0, "MENOS APORTES EPS", "5", DeducionesLaboralesEPS * -1, 0);

                        SalarioPromedio = SalarioPromedio - Descuentos - DeducionesLaboralesEPS;

                        BaseRetencion = CalculaPorcRetencion(Idempresa, TipoProcedimiento, SalarioPromedio, myconnect, ref StFormula);

                        dsprocedimiento.Tables["tblproc"].Rows.Add(Idempresa, row["idempleado"], row["apellidos"].ToString() + " " + row["nombres"].ToString(), 0, StFormula, "6", BaseRetencion, 0);
                    }

                    msgbarra.PerformStep();
                }
            }

            msgbarra.Close();
            msgbarra.Dispose();

            return dsprocedimiento;
        }

        public void LiquidaRetFtePlanilla(int Idnomina, int IdPlanilla, string Usuario, OdbcConnection myconnect, string TipoReg = "A")
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            double fila = 0;
            string CicloPlanilla = "0";
            double Valor = 0;
            int IdPeriodo = 0;
            string Stmysql = "";
            int plaini = 0;
            double VlrAnterior = 0;

            this.msgconfig.BuscaPeriodosPagos(IdPlanilla, Idnomina.ToString(), myconnect, dsdata);
            CicloPlanilla = dsdata.Tables["tblperpagos"].Rows[0]["CicloMes"].ToString();

            if (CicloPlanilla == "2")
            {
                IdPeriodo = Convert.ToInt32(dsdata.Tables["tblperpagos"].Rows[0]["idperiodo"]);
                stbuilder.Append("select emp.idempleado,emp.TasaRetFte,emp.cicloretfte,emp.tipproretfte,emp.idcencos,nomemp.IdRetfte,nomemp.vlruvt, ");
                stbuilder.Append("nomemp.porcrenta,nomemp.porcdeduc,emp.vlrdeduciblertefte,cpto.natur ");
                stbuilder.Append("from nom_empleados emp ");
                stbuilder.Append("inner join nom_empresas nomemp on emp.idnomina=nomemp.IdEmpresa ");
                stbuilder.Append("inner join nom_cptos cpto on nomemp.IdRetfte=cpto.idcpto ");
                stbuilder.Append("where emp.idnomina=" + Idnomina + " and emp.TasaRetFte<>0  ");

                ok = (this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "LiquidaRetFtePlanilla", ref dsdata, "tbldata") != null);

                if (ok)
                {
                    string _p1 = "0", _p2 = "0", _p3 = "0", _p4 = "0";
                    Stmysql = "select min(idplanilla) as campo1, max(idplanilla) as campo2 from nom_perpagos where idperiodo = '" + IdPeriodo + "' and idempresa = '" + Idnomina + "'";
                    ok = this.msgodbc.ExecuteQueryconec(Stmysql, myconnect, "CalculaDiasPlanillaAnt", ref _p1, ref _p2, ref _p3, ref _p4);
                    int.TryParse(_p1, out plaini);

                    for (fila = 0; fila <= dsdata.Tables["tbldata"].Rows.Count - 1; fila++)
                    {
                        DataRow row = dsdata.Tables["tbldata"].Rows[(int)fila];
                        Valor = 0;
                        switch (Convert.ToInt32(row["tipproretfte"]))
                        {
                            case 0:
                                MessageBox.Show("Empleado " + row["idempleado"] + " no tiene parametrizado el tipo de procedimiento que se debe aplicar.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                break;
                            case 1:
                                Valor = Math.Round(Convert.ToDouble(row["TasaRetFte"]) * Convert.ToDouble(row["vlruvt"]), 0);
                                break;
                            case 2:
                                Valor = this.CalculaIngresoGravadoProc2(Idnomina, Convert.ToDouble(row["idempleado"]), plaini, IdPlanilla, Convert.ToDouble(row["porcrenta"]), Convert.ToDouble(row["porcdeduc"]), Convert.ToDouble(row["vlrdeduciblertefte"]), myconnect);
                                Valor = Math.Round((Convert.ToDouble(row["TasaRetFte"]) / 100.0) * Valor, 0);
                                break;
                        }

                        if (Valor != 0)
                        {
                            GrabaLiquidacion(IdPlanilla, Idnomina, Convert.ToDouble(row["idempleado"]), Convert.ToInt32(row["IdRetfte"]), 0, Convert.ToInt32(row["natur"]), 0, 0, Valor, Usuario, row["idcencos"].ToString(), myconnect, TipoReg);
                        }
                    }
                }
            }
        }

        private double CalculaIngresoGravadoProc2(int idempresa, double idempleado, int cicloinicial, int ciclofinal, double PorcRentaExc, double PorcDeducExc, double Valordeduciblertefte, OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet dsdataset = new DataSet();
            double fila = 0;
            int Sw2 = 0;
            double PagosLaborales = 0, DeducionesLaborales = 0, DeducionesLaboralesEPS = 0;
            double NetoIngLab = 0, Descuentos = 0, TopeDeducible = 0;

            Stbuilder.Append("select idempleado,idcpto,natur,idcencos,IdSeccion,nombre,sum(valor) as Valor ");
            Stbuilder.Append("from NOM_LIQPLAN12_VW ");
            Stbuilder.Append("where idempleado=" + idempleado + " and idnomina=" + idempresa + " ");
            Stbuilder.Append(" and idplanilla between " + cicloinicial + " and " + ciclofinal + " ");
            Stbuilder.Append("group by idempleado,idcpto,natur,idcencos,IdSeccion,nombre ");
            Stbuilder.Append("order by idempleado,natur,idcpto ");

            ok = (this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "CalculaIngresoGravadoProc2", ref dsdataset, "tbldatos") != null);
            if (ok)
            {
                for (fila = 0; fila <= dsdataset.Tables["tbldatos"].Rows.Count - 1; fila++)
                {
                    DataRow row = dsdataset.Tables["tbldatos"].Rows[(int)fila];
                    Sw2 = 0;

                    if (row["Valor"] is DBNull)
                    {
                        row["Valor"] = 0;
                    }

                    switch (Convert.ToInt32(row["natur"]))
                    {
                        case 1:
                            PagosLaborales += Convert.ToDouble(row["Valor"]);
                            break;
                        case 2:
                            ok = this.msgconfig.BuscaEps(Convert.ToInt32(row["idcpto"]), myconnect);
                            if (!ok)
                            {
                                DeducionesLaborales += Convert.ToDouble(row["valor"]);
                            }
                            else
                            {
                                DeducionesLaboralesEPS += Convert.ToDouble(row["valor"]);
                                Sw2 = 1;
                            }
                            break;
                    }
                }

                NetoIngLab = PagosLaborales - DeducionesLaborales;
                Descuentos = Math.Round(NetoIngLab * (PorcRentaExc / 100.0), 0);
                NetoIngLab = NetoIngLab - Descuentos;
                TopeDeducible = Math.Round(NetoIngLab * (PorcDeducExc / 100.0), 0);

                if (Valordeduciblertefte > TopeDeducible)
                {
                    Descuentos = TopeDeducible;
                }
                else
                {
                    Descuentos = Valordeduciblertefte;
                }

                NetoIngLab = NetoIngLab - Descuentos - DeducionesLaboralesEPS;
            }

            return NetoIngLab;
        }

        public double LiquidaCesantiasPeriodoAnterior(int idnomina, double idempleado, DateTime fechaultcesantia, DateTime fechaingreso, int DiasLiq,
            OdbcConnection myconnect, ref int DiasLiqCes, ref double VlrDiaLiq, ref DateTime fecinicescalc, ref DateTime fecfincescalc)
        {
            StringBuilder Stbuilder = new StringBuilder();
            int plaini = 0, plafin = 0, DiasCes = 0;
            DataSet DsDataSet = new DataSet();
            DateTime fecIniCesant = default(DateTime);
            double VlrAuxTrasnp = 0;
            int IdTrasporte = 0;
            double TopeAux = 0, promediocesantias = 0, VlrCesantias = 0;

            Stbuilder.Append("select MIN(a.idplanilla) as campo1,MAX(a.idplanilla) as campo2 ");
            Stbuilder.Append("from nom_liqplan a ");
            Stbuilder.Append("inner join nom_perpagos b on a.idnomina=b.IdEmpresa and a.idplanilla=b.IdPlanilla ");
            Stbuilder.Append("where a.idnomina=" + idnomina + " and  a.idempleado=" + idempleado + " and b.idperiodo between '" + fechaultcesantia.AddMonths(1).Year + "01' and '" + fechaultcesantia.AddMonths(1).Year + "12' ");

            string _p1 = "0", _p2 = "0", _p3 = "0", _p4 = "0";
            ok = this.msgodbc.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "LiquidaCesantiasPeriodoAnterior", ref _p1, ref _p2, ref _p3, ref _p4);
            int.TryParse(_p1, out plaini);
            int.TryParse(_p2, out plafin);

            if (ok)
            {
                ok = this.msgconfig.BuscaPeriodosPagos(plaini, idnomina.ToString(), myconnect, DsDataSet);
                if (ok)
                {
                    DataRow rowPer = DsDataSet.Tables["tblperpagos"].Rows[0];
                    fecIniCesant = Convert.ToDateTime(rowPer["Fecinicial"]);
                }
                else
                {
                    MessageBox.Show("Periodos de pago de Cesantias no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
            }

            Stbuilder.Remove(0, Stbuilder.Length);

            Stbuilder.Append("select MAX(a.Valor*c.ClaNom) as campo1 from nom_liqplan a ");
            Stbuilder.Append("inner join nom_empresas b on a.idnomina=b.IdEmpresa ");
            Stbuilder.Append("inner join nom_empleados c on a.idnomina=c.idnomina and a.idempleado=c.idempleado ");
            Stbuilder.Append("where a.idplanilla between " + plaini + " and " + plafin + " and a.IdCpto=b.IdTrasporte ");

            _p1 = "0"; _p2 = "0"; _p3 = "0"; _p4 = "0";
            this.msgodbc.ExecuteQueryconec(Stbuilder.ToString(), myconnect, "LiquidaCesantiasPeriodoAnterior", ref _p1, ref _p2, ref _p3, ref _p4);
            double.TryParse(_p1, out VlrAuxTrasnp);

            ok = this.msgconfig.BuscaEmpresa(idnomina, myconnect, DsDataSet);
            if (ok)
            {
                DataRow rowEmpresa = DsDataSet.Tables["tblempresas"].Rows[0];
                IdTrasporte = Convert.ToInt32(rowEmpresa["IdTrasporte"]);

                this.msgconfig.BuscaCptos(IdTrasporte.ToString(), myconnect, DsDataSet);
                DataRow rowCpto = DsDataSet.Tables["tblcptos"].Rows[0];
                TopeAux = Convert.ToDouble(rowCpto["Saltope"]);
            }

            if (Convert.ToDateTime(fechaingreso.ToString("dd/MM/yyyy")) > Convert.ToDateTime(fecIniCesant.ToString("dd/MM/yyyy")))
            {
                fecIniCesant = fechaingreso;
            }

            if (Convert.ToDateTime(fechaultcesantia.ToString("dd/MM/yyyy")) > Convert.ToDateTime(fecIniCesant.ToString("dd/MM/yyyy")))
            {
                fecIniCesant = fechaultcesantia.AddDays(1);
                fechaultcesantia = new DateTime(fechaultcesantia.Year, 12, 31);
            }
            else
            {
                if (Convert.ToDateTime(fechaingreso.ToString("dd/MM/yyyy")) != Convert.ToDateTime(fechaultcesantia.ToString("dd/MM/yyyy")))
                {
                    fechaultcesantia = fechaultcesantia.AddYears(1);
                }
                else
                {
                    fechaultcesantia = new DateTime(fechaultcesantia.Year, 12, 31);
                }
            }

            double _diasLiq = 0;
            promediocesantias = this.CalculaPromedioCesantias(idnomina, idempleado, plaini, plafin, fecIniCesant, fechaultcesantia, DiasLiq, TopeAux, VlrAuxTrasnp, null, myconnect, ref _diasLiq, false);

            DiasCes = this.msgconfig.CalculaDias(fechaultcesantia, fecIniCesant) - DiasLiq;

            if (DiasCes < 0)
            {
                DiasCes = 0;
            }

            VlrCesantias = Math.Round(((DiasCes * 30.0) / 360.0) * promediocesantias, 0);
            DiasLiqCes = DiasCes;
            VlrDiaLiq = promediocesantias;

            fecinicescalc = fecIniCesant;
            fecfincescalc = fechaultcesantia;

            return VlrCesantias;
        }

        // Overload without ref params for LiquidaCesantiasPeriodoAnterior
        public double LiquidaCesantiasPeriodoAnterior(int idnomina, double idempleado, DateTime fechaultcesantia, DateTime fechaingreso, int DiasLiq,
            OdbcConnection myconnect)
        {
            int DiasLiqCes = 0;
            double VlrDiaLiq = 0;
            DateTime fecinicescalc = new DateTime(1950, 1, 1);
            DateTime fecfincescalc = new DateTime(1950, 1, 1);
            return LiquidaCesantiasPeriodoAnterior(idnomina, idempleado, fechaultcesantia, fechaingreso, DiasLiq, myconnect, ref DiasLiqCes, ref VlrDiaLiq, ref fecinicescalc, ref fecfincescalc);
        }
    }
}
