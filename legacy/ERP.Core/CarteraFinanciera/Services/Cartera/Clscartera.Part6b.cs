using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Cartera
{
    public partial class Clscartera
    {
        // Methods from VB lines 14499-17527 of Clscartera.vb

        #region NominaDescSoloExtras (VB line 14499)

        public bool NominaDescSoloExtras(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
            int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection ConectSub, Form Pertenece)
        {
            string MysqlCodeu, MysqlCodeu1;
            ArrayList Codeudores = new ArrayList();
            bool Atrasados = false;
            string cobracodeudor = "";
            ERP.Core.Compartido.Configuracion.ParamSys paramSys = new ERP.Core.Compartido.Configuracion.ParamSys();
            ERP.Core.CarteraFinanciera.Models.ParamCop parametros = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            ERP.Core.Compartido.Controles.Barraprogress progreso = new ERP.Core.Compartido.Controles.Barraprogress("Generando descuentos solo Extras.", Pertenece);

            string Mysql, mysql1 = "";
            Microsoft.VisualBasic.MsgBoxResult res;
            double tot_apor;
            string stwhere, stwhere1;
            string pdadesdec = "";
            string WhereHastaCiclo;
            int Clades = 0;
            string Detalle = "";
            DateTime FecIni = default(DateTime);
            DateTime FecFin = default(DateTime);
            int CicloMes = 0;
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string WhereSaldo = "";
            int CantiDescu = 0;
            string AgenciaHv = "", CencostoHv = "";
            char OpcionExtras = '1'; // 1-Solo las cuotas extras del ciclo; 2-Incluir tambien cuotas extras atrasadas; 3-Incluir cuotas atrasadas que no son extras; 4-Incluir cuotas extras atrasadas y demas cuotas atrasadas
            // cop_fopciondescuentos OpciDesc = new cop_fopciondescuentos(); // ERROR: CS0246
            string Sql, pl_cencosto = "", pl_agencia = "", pl_empresa = "", WhereCuopen = "";

            int i = 0;

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
                AgenciaHv = "9999";
            }
            else
            {
                WhereAgencia = "  and a.Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
                AgenciaHv = "a.agencia";
                pl_agencia = " and agencia='" + Strings.Right("0000" + Agencia, 4) + "' ";
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
            }
            else
            {
                WhereEmpresa = " and a.EmpDsto = '" + Strings.Right("0000" + Empresa, 4) + "' ";
                pl_empresa = " and empresa='" + Strings.Right("0000" + Empresa, 4) + "' ";
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
                CencostoHv = "99999999";
            }
            else
            {
                WhereCencosto = " and a.cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
                CencostoHv = "a.cencosto";
                pl_cencosto = " and cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
            }

            stwhere = " a.Periodo_contable = '" + PeriodoNomina + "' ";
            stwhere1 = " " + WhereAgencia + WhereEmpresa + WhereCencosto;

            // parametros.BuscarPlanillaNomina(Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad, // ERROR: CS1061
                // Adicional, ConectSub, ref Clades, ref Detalle, ref FecIni, ref FecFin, ref CicloMes); // ERROR: CS1061

            if (MessageBox.Show("Desea incluir las demas cuotas atrasadas en la extras ?", "SOLIDO",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // OpciDesc.ShowDialog(Pertenece); // ERROR: CS0103
                // OpcionExtras = OpciDesc.OpcionSeleccionada; // ERROR: CS0103
                // pdadesdec = OpciDesc.TxtCiclo.Text; // ERROR: CS0103
                // OpciDesc.Dispose(); // ERROR: CS0103

                switch (OpcionExtras)
                {
                    case '0':
                        WhereHastaCiclo = " and a.periodo_causa = '" + CicloAAAAPP + "' ";
                        Atrasados = false;
                        WhereCuopen = " and (d.saldo <> 0 or a.cuota <> 0)";
                        Sql = "update cop_nompla set atrasextras='" + OpcionExtras + "' where periodo=" + CicloAAAAPP + " and periodicidad='" + Periodicidad + "' and adicional='" + Adicional + "' and ciclo='" + CicloMes + "'" + pl_cencosto + pl_agencia + pl_empresa;
                        break;
                    case '1':
                    case '2':
                        WhereHastaCiclo = " and a.periodo_causa <= '" + pdadesdec + "' ";
                        Atrasados = true;
                        Sql = "update cop_nompla set atrasextras='" + OpcionExtras + "' where periodo=" + CicloAAAAPP + " and periodicidad='" + Periodicidad + "' and adicional='" + Adicional + "' and ciclo='" + CicloMes + "'" + pl_cencosto + pl_agencia + pl_empresa;
                        break;
                    default:
                        WhereHastaCiclo = " and a.periodo_causa = '" + CicloAAAAPP + "' ";
                        Atrasados = false;
                        WhereCuopen = " and (d.saldo <> 0 or a.cuota <> 0)";
                        Sql = "update cop_nompla set atrasextras='0' where periodo=" + CicloAAAAPP + " and periodicidad='" + Periodicidad + "' and adicional='" + Adicional + "' and ciclo='" + CicloMes + "'" + pl_cencosto + pl_agencia + pl_empresa;
                        break;
                }
            }
            else
            {
                OpcionExtras = '0';
                WhereHastaCiclo = " and a.periodo_causa = '" + CicloAAAAPP + "' ";
                Atrasados = false;
                WhereCuopen = " and (d.saldo <> 0 or d.cuota <> 0) ";
                Sql = "update cop_nompla set atrasextras='" + OpcionExtras + "' where periodo=" + CicloAAAAPP + " and periodicidad='" + Periodicidad + "' and adicional='" + Adicional + "' and ciclo='" + CicloMes + "'" + pl_cencosto + pl_agencia + pl_empresa;
            }

            this.OdbcConnect.ExecuteQueryconec(Sql, ConectSub, "NominaDescSoloExtras");

            // BuscarCompania: p48 = cobracodeudor
            string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "";
            string _p11 = "", _p12 = "", _p13 = "", _p14 = "", _p15 = "", _p16 = "", _p17 = "", _p18 = "", _p19 = "", _p20 = "";
            string _p21 = "", _p22 = "", _p23 = "", _p24 = "", _p25 = "", _p26 = "", _p27 = "", _p28 = "", _p29 = "", _p30 = "";
            string _p31 = "", _p32 = "", _p33 = "", _p34 = "", _p35 = "", _p36 = "", _p37 = "", _p38 = "", _p39 = "", _p40 = "";
            string _p41 = "", _p42 = "", _p43 = "", _p44 = "", _p45 = "", _p46 = "", _p47 = "";
            cobracodeudor = "";
            // paramSys.BuscarCompania(varini.sptCodEmpr, ConectSub, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                // ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, // ERROR: CS7036
                // ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27, ref _p28, ref _p29, ref _p30, // ERROR: CS7036
                // ref _p31, ref _p32, ref _p33, ref _p34, ref _p35, ref _p36, ref _p37, ref _p38, ref _p39, ref _p40, // ERROR: CS7036
                // ref _p41, ref _p42, ref _p43, ref _p44, ref _p45, ref _p46, ref cobracodeudor); // ERROR: CS7036

            // Build main query based on OpcionExtras
            switch (OpcionExtras)
            {
                case '2':
                    Mysql = "select a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
                         " a.codeudor2,a.codeudor3,a.codeudor4 from cop_cuopen_vw a " +
                         " inner join sys_maenit c on c.codigoter = a.codigoter " +
                         " left join cop_salmaecar d on d.Periodo = '" + PeriodoNomina + "' and d.codigoter = a.codigoter " +
                         " and d.lincred = a.lincred and d.numero = a.numero  where  " +
                         stwhere + WhereHastaCiclo + " and c.estado <> 'R' and c.estado <> 'T' and a.percidad ='" + Periodicidad +
                         "'  and a.clades = '1' " + stwhere1 +
                         " and ((a.lincred >= 1000 and d.saldo <> 0) or (a.lincred < 1000 " + WhereCuopen + ")) " +
                         " AND (CAUSA_CAPI+ CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0  " +
                         " group by a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
                         " a.codeudor2,a.codeudor3,a.codeudor4  order by a.codigoter asc";
                    break;
                default:
                    Mysql = "select a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
                            " a.codeudor2,a.codeudor3,a.codeudor4 from cop_cuopen_vw a " +
                            " inner join sys_maenit c on c.codigoter = a.codigoter " +
                            " left join cop_salmaecar d on d.Periodo = '" + PeriodoNomina + "' and d.codigoter = a.codigoter " +
                            " and d.lincred = a.lincred and d.numero = a.numero  where  " +
                            stwhere + WhereHastaCiclo + " and c.estado <> 'R' and a.percidad ='" + Periodicidad +
                            "'  and a.clades = '1' " + stwhere1 +
                            " and (a.lincred >= 1000 and d.saldo <> 0) " +
                            " AND  CAUSA_EXTR <> 0  " +
                            " group by a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
                            " a.codeudor2,a.codeudor3,a.codeudor4  order by a.codigoter asc";
                    break;
            }

            try
            {
                DataSet myReader = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(Mysql, ConectSub, "NominaDescSoloExtras", ref myReader, "DATOSPrincipal");
                CantiDescu = myReader.Tables["DATOSPrincipal"].Rows.Count;
                myReader.Tables.Add("DATOSSubConsulta");
                progreso.ValorMinimoMaximo(0, CantiDescu);
                progreso.Show();
                ArrayList Aplicados = new ArrayList();

                for (i = 0; i <= CantiDescu - 1; i++)
                {
                    DataRow row = myReader.Tables["DATOSPrincipal"].Rows[i];

                    Application.DoEvents();
                    progreso.PerformStep();

                    if (Aplicados.Contains(row["codigoter"].ToString() + row["lincred"].ToString() +
                        row["numero"].ToString()) == false)
                    {
                        switch (OpcionExtras)
                        {
                            case '0':
                            case '1':
                                Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                  "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";
                                mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",rtrim('" +
                                CicloAAAAPP + "') as periodo,a.percidad,rtrim('" +
                                Adicional + "') as adicional, a.codigoter,a.lincred,a.numero,rtrim('" +
                                Detalle + "')as detalle,rtrim('" +
                                CicloMes + "') as ciclo,rtrim('0') as vlr_aportes, rtrim('0') as causa_capi ,case c.PREVIV when 'Y' then sum(a.causa_inte) else 0 end " +
                                "as causa_inte,sum(a.causa_extr) as causa_extr,rtrim('0') as vlr_mora,rtrim('0') as causa_segu," +
                                "rtrim('0') as causa_admi,rtrim('0') as causa_otro" +
                                " from cop_cuopen_vw a " +
                                " inner join cop_concar12 c on c.lincred = a.lincred " +
                                " where a.codigoter ='" +
                                row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                "' and a.numero='" + row["numero"] + "' and a.clades = '1' and CAUSA_EXTR <> 0  group by   a.EmpDsto,a.agencia,a.cencosto,a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                                break;

                            case '2':
                                Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                  "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";
                                mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",'" +
                                CicloAAAAPP + "' as periodo,a.percidad,'" +
                                Adicional + "' as adicional, a.codigoter,a.lincred,a.numero,'" +
                                Detalle + "' as detalle,'" +
                                CicloMes + "' as ciclo, " + ((row["lincred"].ToString().CompareTo("1000") >= 0) ?
                                " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                                " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                                " sum(a.causa_inte) " +
                                " as causa_inte,sum(a.causa_extr) as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu," +
                                " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro" +
                                " from cop_cuopen_vw a  " +
                                " inner join cop_concar12 c on c.lincred = a.lincred " +
                                " where a.codigoter ='" +
                                row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                "' and a.numero='" + row["numero"] + "' and a.clades = '1' and (CAUSA_CAPI+ CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
                                " group by   a.EmpDsto,a.agencia,a.cencosto,a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                                break;

                            default:
                                Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                               "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";
                                mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",rtrim('" +
                                CicloAAAAPP + "') as periodo,a.percidad,rtrim('" +
                                Adicional + "') as adicional, a.codigoter,a.lincred,a.numero,rtrim('" +
                                Detalle + "')as detalle,rtrim('" +
                                CicloMes + "') as ciclo,rtrim('0') as vlr_aportes, rtrim('0') as causa_capi ,case c.PREVIV when 'Y' then sum(a.causa_inte) else 0 end " +
                                "as causa_inte,sum(a.causa_extr) as causa_extr,rtrim('0') as vlr_mora,rtrim('0') as causa_segu," +
                                "rtrim('0') as causa_admi,rtrim('0') as causa_otro" +
                                " from cop_cuopen_vw a " +
                                " inner join cop_concar12 c on c.lincred = a.lincred " +
                                " where a.codigoter ='" +
                                row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                "' and a.numero='" + row["numero"] + "' and a.clades = '1' and CAUSA_EXTR <> 0  group by   a.EmpDsto,a.agencia,a.cencosto,a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                                break;
                        }

                        this.OdbcConnect.ExecuteQueryconec(Mysql + mysql1, ConectSub, "NominaDescSoloExtras");
                    }

                    ok = true;

                    Aplicados.Add(row["codigoter"].ToString() + row["lincred"].ToString() +
                          row["numero"].ToString());

                    if (cobracodeudor == "Y")
                    {
                        if (Atrasados == true)
                        {
                            for (int j = 1; j <= 4; j++)
                            {
                                MysqlCodeu = "";
                                MysqlCodeu1 = "";
                                string CodCodeudor;
                                CodCodeudor = Strings.Right("00000000000000" + row["codeudor" + j].ToString().Trim(), 14);
                                if (CodCodeudor != "00000000000000" &&
                                    CodCodeudor != row["codigoter"].ToString())
                                {
                                    Codeudores.Add(CodCodeudor);

                                    MysqlCodeu = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                          "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras,Codeuda) ";

                                    MysqlCodeu1 = "select a.EmpDsto,a.agencia,a.cencosto,'" +
                                     CicloAAAAPP + "' as periodo,'" + Periodicidad + "' as percidad,'" +
                                     Adicional + "' as adicional,'" +
                                     CodCodeudor + "' as codigoter,a.lincred,a.numero,'" +
                                     Detalle + "' as detalle,'" +
                                     CicloMes + "' as ciclo,rtrim('0') as vlr_aportes, rtrim('0') as causa_capi ,case c.PREVIV when 'Y' then sum(a.causa_inte) else 0 end " +
                                     " as causa_inte,sum(a.causa_extr) as causa_extr,rtrim('0') as vlr_mora,rtrim('0') as causa_segu," +
                                     " rtrim('0') as causa_admi,rtrim('0') as causa_otro,a.codigoter as Codeuda " +
                                     " from cop_cuopen_vw a " +
                                     " inner join cop_concar12 c on c.lincred = a.lincred " +
                                     " where a.clades = '1' and " +
                                     " a.codigoter ='" +
                                      row["codigoter"].ToString() + "' and " + stwhere + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                     "' and a.numero='" + row["numero"] + "' and CAUSA_EXTR <> 0 and a.diasmora > 0 and a.periodo_causa < " + pdadesdec +
                                     " group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";

                                    this.OdbcConnect.ExecuteQueryconec(MysqlCodeu + MysqlCodeu1, ConectSub, "NominaDescTodosGrabacodeuda");
                                }
                            }
                        }
                    }
                }

                myReader.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message);
            }
            if (progreso != null)
            {
                progreso.Close();
            }

            if (ok == false)
            {
                MessageBox.Show("No se encontraron datos.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                if (i != CantiDescu)
                {
                    MessageBox.Show("El Proceso Termino Pero no aplico todos los Descuentos .", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else
                {
                    MessageBox.Show("El Proceso Termino Correctamente.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            return ok;
        }

        #endregion

        #region ValidaEquivalenciasDeNomina (VB line 14783)

        public bool ValidaEquivalenciasDeNomina(string Agencia, string Empresa, string Cencosto,
            OdbcConnection conect)
        {
            string mysql1;
            bool SiHay = false;
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string linea;
            int canreg = 0, fila = 0;

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
            }
            else
            {
                WhereAgencia = "  and b.Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
            }
            else
            {
                WhereEmpresa = " and b.Empresa = '" + Strings.Right("0000" + Empresa, 4) + "' ";
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
            }
            else
            {
                WhereCencosto = " and b.cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
            }

            mysql1 = "select lincred,Agencia,Empresa,Cencosto from cop_nomdes b where lincred not in (select lincred from cop_nomconce b where lincred = lincred " +
                WhereAgencia + WhereEmpresa + WhereCencosto + ") " + WhereAgencia + WhereEmpresa + WhereCencosto + " group by lincred,Agencia,Empresa,Cencosto  order by empresa,lincred";

            DataSet myReader0 = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(mysql1, conect, "ValidaEquivalenciasDeNomina", ref myReader0, "TblValEquivDeNom");
            canreg = myReader0.Tables["TblValEquivDeNom"].Rows.Count;

            StreamWriter read1;
            read1 = File.CreateText(Application.StartupPath + "\\lineas sin equivalencia.txt");
            read1.WriteLine("Lincred  Agencia   Empresa   Cencosto");
            while (fila < canreg)
            {
                DataRow row = myReader0.Tables["TblValEquivDeNom"].Rows[fila];
                linea = Strings.Left(row["lincred"].ToString() + "         ", 9);
                linea += Strings.Left(row["Agencia"].ToString() + "          ", 10);
                linea += Strings.Left(row["Empresa"].ToString() + "          ", 10);
                linea += Strings.Left(row["cencosto"].ToString() + "         ", 8);
                read1.WriteLine(linea);
                SiHay = true;
                fila += 1;
            }
            read1.Close();
            myReader0.Dispose();
            if (SiHay == true)
            {
                MessageBox.Show("No se puede generar el archivo plano.\r\n" +
                    "Una o mas de las lineas de credito no tienen equivalencia.\r\n" +
                    "Se genero un archivo con las lineas de credito.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start(Application.StartupPath + "\\lineas sin equivalencia.txt");
                return false;
            }
            return true;
        }

        #endregion

        #region NomiBorrarValoreDescu (VB line 14844)

        public bool NomiBorrarValoreDescu(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
            int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection Conect, Form Pertenece)
        {
            ERP.Core.Compartido.Controles.Barraprogress Pro = new ERP.Core.Compartido.Controles.Barraprogress("Cargando valores desde la Base de Datos.", Pertenece);

            string mysql1;
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string AgenciaUp;
            string EmpresaUp;
            string CencostoUp;
            string linea;

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
                AgenciaUp = "";
            }
            else
            {
                WhereAgencia = "  and Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
                AgenciaUp = Strings.Right("0000" + Agencia, 4);
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
                EmpresaUp = "";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + Strings.Right("0000" + Empresa, 4) + "' ";
                EmpresaUp = Strings.Right("0000" + Empresa, 4);
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
                CencostoUp = "";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
                CencostoUp = Strings.Right("00000000" + Cencosto, 8);
            }

            ok = false;

            stmysql = "update cop_valdesc  set valor = 0 where periodo = '" + CicloAAAAPP + "' and adicional ='" + Adicional + "'" +
                " and periodicidad = '" + Periodicidad + "' " + WhereAgencia + WhereEmpresa + WhereCencosto;

            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiBorrarValoreDescu.Delete");

            return ok;
        }

        #endregion

        #region NomiCargaValoresDesdeTabla (VB line 14890)

        public bool NomiCargaValoresDesdeTabla(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
            int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection Conect, Form Pertenece)
        {
            ERP.Core.Compartido.Controles.Barraprogress Pro = new ERP.Core.Compartido.Controles.Barraprogress("Cargando valores desde la Base de Datos.", Pertenece);

            string mysql1;
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string AgenciaUp;
            string EmpresaUp;
            string CencostoUp;
            string linea;
            string Empre, Agen, Cencos, SqlConceptos, VarInteres, VarExtra;
            bool InteresDif = false, ExtraDif = false;
            DataSet DsConceptos = new DataSet();
            int Contador;
            string stwhere_local = "";
            string mysql = "";

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
                AgenciaUp = "";
                Agen = "9999";
            }
            else
            {
                WhereAgencia = "  and a.Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
                AgenciaUp = Strings.Right("0000" + Agencia, 4);
                Agen = AgenciaUp;
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
                EmpresaUp = "";
                Empre = "9999";
            }
            else
            {
                WhereEmpresa = " and a.Empresa = '" + Strings.Right("0000" + Empresa, 4) + "' ";
                EmpresaUp = Strings.Right("0000" + Empresa, 4);
                Empre = EmpresaUp;
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
                CencostoUp = "";
                Cencos = "99999999";
            }
            else
            {
                WhereCencosto = " and a.cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
                CencostoUp = Strings.Right("00000000" + Cencosto, 8);
                Cencos = CencostoUp;
            }
            stwhere_local = WhereAgencia + WhereEmpresa + WhereCencosto;

            mysql = "delete from cop_valdesc  where " +
                          " periodo = '" + Strings.Trim(CicloAAAAPP.ToString()) +
                          "' and periodicidad = '" + Periodicidad +
                          "' and adicional = '" + Adicional + "' " + Strings.Replace(stwhere_local, "a.", "", 1, -1, CompareMethod.Text);
            this.OdbcConnect.ExecuteQueryconec(mysql, Conect, "BorrarTablacop_valdesc");

            SqlConceptos = "select copto_nomina, cpto_interes,cpto_extras from  cop_nomconce where empresa='" + Empre + "' and agencia='" + Agen + "' and cencosto='" + Cencos + "'";
            this.OdbcConnect.ExecuteQueryDataset(SqlConceptos, Conect, "NomiCargaValoresDesdeTabla", ref DsConceptos, "concepto");

            VarExtra = " + a.vlr_extras ";
            VarInteres = " + a.vlr_interes ";

            // Nuevo codigo para Separar los conceptos de capital, interes y extras
            for (Contador = 0; Contador <= DsConceptos.Tables["concepto"].Rows.Count - 1; Contador++)
            {
                DataRow rowC = DsConceptos.Tables["concepto"].Rows[Contador];
                if (rowC["copto_nomina"].ToString() != rowC["cpto_extras"].ToString())
                {
                    ExtraDif = true;
                    VarExtra = "";
                    break;
                }
            }

            for (Contador = 0; Contador <= DsConceptos.Tables["concepto"].Rows.Count - 1; Contador++)
            {
                DataRow rowC = DsConceptos.Tables["concepto"].Rows[Contador];
                if (rowC["copto_nomina"].ToString() != rowC["cpto_interes"].ToString())
                {
                    InteresDif = true;
                    VarInteres = "";
                    break;
                }
            }

            if (ValidaEquivalenciasDeNomina(Agencia, Empresa, Cencosto, Conect) == false)
            {
                return false;
            }

            mysql1 = "select a.cencosto,a.empresa,b.copto_nomina,b.cpto_interes,b.cpto_extras,a.agencia,a.periodo,a.periodicidad,a.adicional,c.codigoter, sum(a.vlr_aportes + a.vlr_prestamos " + VarInteres + VarExtra + " + a.vlr_seguro + a.vlr_mora + a.vlr_admon + a.vlr_otras) as total " +
                " from cop_nomdes a left join cop_nomconce b on " +
                " b.cencosto = case  when '" + CencostoUp + "' = '' then b.cencosto  else a.cencosto end and " +
                " b.empresa = case  when '" + EmpresaUp + "' = '' then b.empresa  else a.empresa end and " +
                " b.agencia = case  when '" + AgenciaUp + "' = '' then b.agencia  else a.agencia end and " +
                " b.lincred = a.lincred inner join sys_maenit c on c.codigoter = a.codigoter" +
                " where a.periodo = '" + CicloAAAAPP + "' and a.adicional ='" + Adicional + "'" +
                " and a.periodicidad = '" + Periodicidad + "' " + WhereAgencia + WhereEmpresa + WhereCencosto +
                " and a.lincred = b.lincred  group by a.cencosto,a.empresa,b.copto_nomina,b.cpto_interes,b.cpto_extras,a.agencia," +
                "a.periodo,a.periodicidad,a.adicional,c.codigoter order by c.codigoter";

            DataSet ds = new DataSet();
            mysql1 = Strings.Replace(mysql1, "''", "' '", 1, -1, CompareMethod.Text);
            this.OdbcConnect.ExecuteQueryDataset(mysql1, Conect, "NomiCargaValoresDesdeTabla", ref ds, "Datos");
            int con = ds.Tables["Datos"].Rows.Count;

            Pro.ValorMinimoMaximo(0, con);
            Pro.Show();
            ok = false;
            string cod = "";
            int i;
            for (i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
            {
                Application.DoEvents();
                DataRow row = ds.Tables[0].Rows[i];

                stmysql = "insert into cop_valdesc (cencosto,empresa,agencia,periodo,periodicidad,adicional,codigoter,comcep,valor) values ('" +
                    CencostoUp + "','" + EmpresaUp + "','" + AgenciaUp + "','" + CicloAAAAPP + "','" + Periodicidad + "','" + Adicional + "','" +
                    row["codigoter"].ToString() +
                    "','" + row["copto_nomina"] + "','" + row["total"] + "') ";

                this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiCargaValoresDesdeTabla.Insert");

                Pro.PerformStep();
                ok = true;
                cod = row["codigoter"].ToString();
            }

            // Nuevas operaciones para insertar los conceptos diferentes a capital (interes y extras)
            if (ExtraDif == true)
            {
                mysql1 = "select a.cencosto,a.empresa,b.cpto_extras,a.agencia,a.periodo,a.periodicidad,a.adicional,c.codigoter, sum(a.vlr_extras) as total " +
                    " from cop_nomdes a left join cop_nomconce b on " +
                    " b.cencosto = case  when '" + CencostoUp + "' = '' then b.cencosto  else a.cencosto end and " +
                    " b.empresa = case  when '" + EmpresaUp + "' = '' then b.empresa  else a.empresa end and " +
                    " b.agencia = case  when '" + AgenciaUp + "' = '' then b.agencia  else a.agencia end and " +
                    " b.lincred = a.lincred inner join sys_maenit c on c.codigoter = a.codigoter" +
                    " where a.periodo = '" + CicloAAAAPP + "' and a.adicional ='" + Adicional + "'" +
                    " and a.periodicidad = '" + Periodicidad + "' " + WhereAgencia + WhereEmpresa + WhereCencosto +
                    " and a.lincred = b.lincred and a.vlr_extras<>0 group by a.cencosto,a.empresa,b.cpto_extras,a.agencia," +
                    "a.periodo,a.periodicidad,a.adicional,c.codigoter order by c.codigoter";

                DataSet dsExtra = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(mysql1, Conect, "NomiCargaValoresDesdeTabla", ref dsExtra, "Datos");
                con = dsExtra.Tables["Datos"].Rows.Count;

                for (Contador = 0; Contador <= dsExtra.Tables[0].Rows.Count - 1; Contador++)
                {
                    Application.DoEvents();
                    DataRow row = dsExtra.Tables[0].Rows[Contador];
                    stmysql = "select comcep from cop_valdesc where  periodo = '" + CicloAAAAPP + "' " +
                                                        " and periodicidad =  '" + Periodicidad + "' and adicional = '" + Adicional +
                                                        "' and codigoter = '" + row["codigoter"].ToString() + "' and comcep = '" + row["cpto_extras"].ToString() + "'";

                    if (this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiCargaValoresDesdeTabla.Select") == true)
                    {
                        stmysql = "update cop_valdesc set valor=valor + " + row["total"] + " where  periodo = '" + CicloAAAAPP + "' " +
                                                                                 " and periodicidad =  '" + Periodicidad + "' and adicional = '" + Adicional +
                                                                                 "' and codigoter = '" + row["codigoter"].ToString() + "' and comcep = '" + row["cpto_extras"].ToString() + "'";

                        this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiCargaValoresDesdeTabla.Update");
                    }
                    else
                    {
                        stmysql = "insert into cop_valdesc (cencosto,empresa,agencia,periodo,periodicidad,adicional,codigoter,comcep,valor) values ('" +
                                              CencostoUp + "','" + EmpresaUp + "','" + AgenciaUp + "','" + CicloAAAAPP + "','" + Periodicidad + "','" + Adicional + "','" +
                                              row["codigoter"].ToString() +
                                              "','" + row["cpto_extras"] + "','" + row["total"] + "') ";

                        this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiCargaValoresDesdeTabla.Insert");
                    }
                }
            }

            if (InteresDif == true)
            {
                mysql1 = "select a.cencosto,a.empresa,b.cpto_interes,a.agencia,a.periodo,a.periodicidad,a.adicional,c.codigoter, sum(a.vlr_interes) as total " +
                    " from cop_nomdes a left join cop_nomconce b on " +
                    " b.cencosto = case  when '" + CencostoUp + "' = '' then b.cencosto  else a.cencosto end and " +
                    " b.empresa = case  when '" + EmpresaUp + "' = '' then b.empresa  else a.empresa end and " +
                    " b.agencia = case  when '" + AgenciaUp + "' = '' then b.agencia  else a.agencia end and " +
                    " b.lincred = a.lincred inner join sys_maenit c on c.codigoter = a.codigoter" +
                    " where a.periodo = '" + CicloAAAAPP + "' and a.adicional ='" + Adicional + "'" +
                    " and a.periodicidad = '" + Periodicidad + "' " + WhereAgencia + WhereEmpresa + WhereCencosto +
                    " and a.lincred = b.lincred and a.vlr_interes<>0 group by a.cencosto,a.empresa,b.cpto_interes,a.agencia," +
                    "a.periodo,a.periodicidad,a.adicional,c.codigoter order by c.codigoter";

                mysql1 = Strings.Replace(mysql1, "''", "' '", 1, -1, CompareMethod.Text);
                DataSet dsInteres = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(mysql1, Conect, "NomiCargaValoresDesdeTabla", ref dsInteres, "Datos");
                con = dsInteres.Tables["Datos"].Rows.Count;

                for (Contador = 0; Contador <= dsInteres.Tables[0].Rows.Count - 1; Contador++)
                {
                    Application.DoEvents();
                    DataRow row = dsInteres.Tables[0].Rows[Contador];
                    stmysql = "select comcep from cop_valdesc where  periodo = '" + CicloAAAAPP + "' " +
                                                        " and periodicidad =  '" + Periodicidad + "' and adicional = '" + Adicional +
                                                        "' and codigoter = '" + row["codigoter"].ToString() + "' and comcep = '" + row["cpto_interes"].ToString() + "'";

                    if (this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiCargaValoresDesdeTabla.Select") == true)
                    {
                        stmysql = "update cop_valdesc set valor=valor + " + row["total"] + " where  periodo = '" + CicloAAAAPP + "' " +
                                                                                 " and periodicidad =  '" + Periodicidad + "' and adicional = '" + Adicional +
                                                                                 "' and codigoter = '" + row["codigoter"].ToString() + "' and comcep = '" + row["cpto_interes"].ToString() + "'";

                        this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiCargaValoresDesdeTabla.Update");
                    }
                    else
                    {
                        stmysql = "insert into cop_valdesc (cencosto,empresa,agencia,periodo,periodicidad,adicional,codigoter,comcep,valor) values ('" +
                                              CencostoUp + "','" + EmpresaUp + "','" + AgenciaUp + "','" + CicloAAAAPP + "','" + Periodicidad + "','" + Adicional + "','" +
                                              row["codigoter"].ToString() +
                                              "','" + row["cpto_interes"] + "','" + row["total"] + "') ";

                        this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NomiCargaValoresDesdeTabla.Insert");
                    }
                }
            }
            Pro.Dispose();
            Pro.Close();

            // final del codigo nuevas operaciones
            if (ok == false)
            {
                MessageBox.Show("No hay datos  en los descuentos de nomina.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Operacion finalizada con exito.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return ok;
        }

        #endregion

        #region BuscarValorDescuento (VB line 15136) - with overloads for Optional ByRef

        // Overload without ref ValorDescuento
        public bool BuscarValorDescuento(string Agencia, string Empresa, string Cencosto,
            int Ciclo, string Periodicidad, string Adicional, OdbcConnection Conect,
            string Equivalencia, string Codigoter)
        {
            double dummy = 0;
            return BuscarValorDescuento(Agencia, Empresa, Cencosto, Ciclo, Periodicidad, Adicional, Conect, Equivalencia, Codigoter, ref dummy);
        }

        public bool BuscarValorDescuento(string Agencia, string Empresa, string Cencosto,
            int Ciclo, string Periodicidad, string Adicional, OdbcConnection Conect,
            string Equivalencia, string Codigoter, ref double ValorDescuento)
        {
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto, Where;

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
            }
            else
            {
                WhereAgencia = " and Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + Strings.Right("0000" + Empresa, 4) + "' ";
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
            }

            Where = " where periodo = '" + Ciclo + "' and periodicidad = '" +
                    Periodicidad + "' and adicional = '" + Adicional + "' and codigoter = '" +
                    Strings.Right("00000000000000" + Codigoter, 14) + "' and comcep = '" + Equivalencia + "' " + WhereAgencia + WhereEmpresa + WhereCencosto;

            stmysql = "select valor as campo1 from cop_valdesc ";
            // ExecuteQueryconec with ref double for campo1
            string _valDesc = "0";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql + Where, Conect, "BuscarValorDescuento", ref _valDesc);
            double.TryParse(_valDesc, out ValorDescuento);

            return ok;
        }

        #endregion

        #region RevisaCuentas (VB line 15170)

        public void RevisaCuentas(DateTime FecCorte, int Opcion, OdbcConnection myconnect)
        {
            string CuentaInicial = "", CuentaFinal = "";
            string Descripcion = null;
            int canreg = 0, fila = 0;

            switch (Opcion)
            {
                case 0:
                    CuentaInicial = "140405000000";
                    CuentaFinal = "146535000000";
                    break;
                case 1:
                    CuentaInicial = "165505000000";
                    CuentaFinal = "165549000000";
                    break;
                case 2:
                    CuentaInicial = "812020000000";
                    CuentaFinal = "812042000000";
                    break;
                case 3:
                    CuentaInicial = "148905000000";
                    CuentaFinal = "149527000000";
                    break;
                case 4:
                    CuentaInicial = "169205000000";
                    CuentaFinal = "169757000000";
                    break;
                case 5:
                    CuentaInicial = "149805000000";
                    CuentaFinal = "149805999999";
                    break;
                case 6:
                    CuentaInicial = "149810000000";
                    CuentaFinal = "149810999999";
                    break;
            }

            stmysql = "select coprcta.CUENTA from  cnt_maecuen coprcta where  "
                + " coprcta.cuenta between  '" + CuentaInicial + "' and '" + CuentaFinal + "'"
                + "and  COPRCTA.CUENTA NOT IN(SELECT CUENTA  FROM cnt_salcuen_vw  where periodo = '" + FecCorte.ToString("yyyy") + "')";

            DataSet myread = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "RevisaCuentas", ref myread, "TblRevCtaSaldo");
            canreg = myread.Tables["TblRevCtaSaldo"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myread.Tables["TblRevCtaSaldo"].Rows[fila];
                Descripcion = " ";
                stmysql = "select descripcion as campo1 from cop_concar12 where lincred = " + (int.Parse(Strings.Mid(row["cuenta"].ToString(), 7, 3)) + 1000);
                string _desc = " ";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCuentaContable", ref _desc);
                Descripcion = _desc;
                msgcnt.GrabaCuentaContable(row["cuenta"].ToString(), "N", "N", "N", "N", "N", "N", "N", "N", "N", "N", " ", "D", Descripcion, 6, 0, "99999999", "", "", "N", "Y", "", FecCorte.ToString("yyyyMM"), myconnect);
                fila += 1;
            }
            myread.Dispose();
        }

        #endregion

        #region RevisaCuentasSaldo (VB line 15222)

        public void RevisaCuentasSaldo(DateTime FecCorte, int Opcion, OdbcConnection myconnect)
        {
            string CuentaInicial = "", CuentaFinal = "";
            string Descripcion = null;
            int canreg = 0, fila = 0;
            StringBuilder Stbuilder = new StringBuilder();

            switch (Opcion)
            {
                case 0:
                    CuentaInicial = "140405000000";
                    CuentaFinal = "146535000000";
                    break;
                case 1:
                    CuentaInicial = "165505000000";
                    CuentaFinal = "165549000000";
                    break;
                case 2:
                    CuentaInicial = "812020000000";
                    CuentaFinal = "812042000000";
                    break;
                case 3:
                    CuentaInicial = "148905000000";
                    CuentaFinal = "149527000000";
                    break;
                case 4:
                    CuentaInicial = "169205000000";
                    CuentaFinal = "169757000000";
                    break;
                case 5:
                    CuentaInicial = "149805000000";
                    CuentaFinal = "149805999999";
                    break;
                case 6:
                    CuentaInicial = "149810000000";
                    CuentaFinal = "149810999999";
                    break;
                case 7:
                    CuentaInicial = "415005000000";
                    CuentaFinal = "415015999999";
                    break;
            }

            switch (Opcion)
            {
                case 7:
                    Stbuilder.Append("select coprcta.CUENTA from  cop_coprcta02_vw coprcta ");
                    Stbuilder.Append("where  coprcta.periodo_contable =" + FecCorte.ToString("yyyyMM") + " AND coprcta.cuenta between  '" + CuentaInicial + "' and '" + CuentaFinal + "' ");
                    Stbuilder.Append("and  COPRCTA.CUENTA NOT IN(SELECT CUENTA  FROM cnt_salcuen_vw  where periodo = '" + FecCorte.ToString("yyyyMM") + "')");
                    break;
                default:
                    Stbuilder.Append("select coprcta.CUENTA from  cop_coprcta_vw coprcta where  coprcta.periodo_contable =" + FecCorte.ToString("yyyyMM"));
                    Stbuilder.Append(" AND coprcta.cuenta between  '" + CuentaInicial + "' and '" + CuentaFinal + "' ");
                    Stbuilder.Append("and  COPRCTA.CUENTA NOT IN(SELECT CUENTA  FROM cnt_salcuen_vw  where periodo = '" + FecCorte.ToString("yyyy") + "')");
                    break;
            }

            DataSet myread = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "RevisaCuentasSaldo", ref myread, "TblRevCtaSaldo");
            canreg = myread.Tables["TblRevCtaSaldo"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myread.Tables["TblRevCtaSaldo"].Rows[fila];
                Descripcion = " ";
                stmysql = "select descripcion as campo1 from cop_concar12 where lincred = " + (int.Parse(Strings.Mid(row["cuenta"].ToString(), 7, 3)) + 1000);
                string _desc = " ";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCuentaContable", ref _desc);
                Descripcion = _desc;
                msgcnt.GrabaCuentaContable(row["cuenta"].ToString(), "N", "N", "N", "N", "N", "N", "N", "N", "N", "N", " ", "D", Descripcion, 6, 0, "99999999", "", "", "N", "Y", "", FecCorte.ToString("yyyyMM"), myconnect);
                fila += 1;
            }
            myread.Dispose();
            RevisaCuentas(FecCorte, Opcion, myconnect);
        }

        #endregion

        #region BuscaCredritosCoodeudor (VB line 15290)

        public DataSet BuscaCredritosCoodeudor(string Codigoter, string Periodo, OdbcConnection myconnect)
        {
            DataSet DsdataSet = new DataSet();

            stmysql = "select copmae.codigoter, copmae.lincred, copmae.numero ,nombre, SaldoCapital + SaldoExtra as capital, SaldoInteres as Interes,SaldoMora as mora,SaldoSeguro + SaldoAdmon as otros,"
                    + "SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon as total, salmae.saldo "
                    + "from cop_maecar copmae inner join cop_salmaecar salmae on copmae.codigoter = salmae.codigoter and copmae.lincred = salmae.lincred and copmae.numero = salmae.numero and salmae.periodo = '" + Periodo + "'"
                    + "inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter "
                    + "left join cop_copmora_vw  copmora on copmae.codigoter = copmora.codigoter and copmae.lincred = copmora.lincred and copmae.numero = copmora.numero and periodo_contable = '" + Periodo + "'"
                    + "where  copmae.lincred >= 1000 and salmae.saldo <> 0 and (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon) > 0 and "
                    + "(codeudor1 = '" + Codigoter + "' or codeudor2 = '" + Codigoter + "' or codeudor3 = '" + Codigoter + "' or codeudor4 = '" + Codigoter + "')";

            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "CargaCredritosCoodeudor", ref DsdataSet, "TblCreCodeudor");
            return DsdataSet;
        }

        #endregion

        #region GrabaValorDescuento (VB line 15305) - with overloads for Optional ByRef

        // Overload without ref NuevoValorDescuento
        public bool GrabaValorDescuento(string Agencia, string Empresa, string Cencosto,
            int Ciclo, string Periodicidad, string Adicional, OdbcConnection Conect,
            string Equivalencia, string Codigoter)
        {
            double dummy = 0;
            return GrabaValorDescuento(Agencia, Empresa, Cencosto, Ciclo, Periodicidad, Adicional, Conect, Equivalencia, Codigoter, ref dummy);
        }

        public bool GrabaValorDescuento(string Agencia, string Empresa, string Cencosto,
            int Ciclo, string Periodicidad, string Adicional, OdbcConnection Conect,
            string Equivalencia, string Codigoter, ref double NuevoValorDescuento)
        {
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string EmpreUp, AgenciUp, CencosUp;
            if (Agencia == "Todos")
            {
                WhereAgencia = "";
                AgenciUp = "";
            }
            else
            {
                WhereAgencia = " and Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
                AgenciUp = Strings.Right("0000" + Agencia, 4);
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
                EmpreUp = "";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + Strings.Right("0000" + Empresa, 4) + "' ";
                EmpreUp = Strings.Right("0000" + Empresa, 4);
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
                CencosUp = "";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
                CencosUp = Strings.Right("00000000" + Cencosto, 8);
            }

            stmysql = "Select codigoter as campo1 from sys_maenit a where codigoter = '" + Strings.Right("00000000000000" + Codigoter, 14)
                + "' " + WhereAgencia + WhereEmpresa + WhereCencosto;

            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "GrabaValorDescuento.BuscaAso");
            if (ok == false)
            {
                return false;
            }
            ok = false;
            ok = BuscarValorDescuento(Agencia, Empresa, Cencosto, Ciclo, Periodicidad, Adicional, Conect, Equivalencia, Codigoter);
            if (ok == true)
            {
                stmysql = "update cop_valdesc set valor = " + NuevoValorDescuento +
                          " where periodo = '" + Ciclo + "' and periodicidad = '" +
                          Periodicidad + "' and adicional =  '" + Adicional + "' " + WhereAgencia + WhereEmpresa + WhereCencosto +
                          " and codigoter = '" + Strings.Right("00000000000000" + Codigoter, 14) + "'  and comcep = '" + Equivalencia + "' ";
            }
            else
            {
                stmysql = "insert into cop_valdesc (cencosto,empresa,agencia,periodo,periodicidad,adicional,codigoter,comcep,valor) values ('" +
                   CencosUp + "','" + EmpreUp + "','" + AgenciUp + "','" + Ciclo + "','" + Periodicidad + "','" + Adicional + "','" +
                   Strings.Right("00000000000000" + Codigoter, 14) + "','" + Equivalencia + "','" + NuevoValorDescuento + "') ";
            }

            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "GrabaValorDescuento");
            return ok;
        }

        #endregion

        #region NominaPendienteCodeuda (VB line 15361)

        public bool NominaPendienteCodeuda(string Agencia, string Empresa, string Cencosto,
            int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection Conect,
            string codigoter)
        {
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string Codeuda = " ";
            if (Agencia == "Todos")
            {
                WhereAgencia = "";
            }
            else
            {
                WhereAgencia = "  and Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + Strings.Right("0000" + Empresa, 4) + "' ";
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
            }

            stmysql = "Select min(codeuda) as campo1 from cop_nomdes  "
                     + " where  periodo = '" + CicloAAAAPP + "' and periodicidad ='" + Periodicidad + "' and  adicional = '" + Adicional + "' " +
                       " and codeuda <> '99999999999999' and codigoter = '" + codigoter + "' " +
                       WhereAgencia + WhereEmpresa + WhereCencosto;

            string _codeuda = " ";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NominaPendienteCodeuda", ref _codeuda);
            Codeuda = _codeuda;

            if (ok == true)
            {
                if (Codeuda != "99999999999999")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region ExecuteQueryDataset (VB line 15403)

        public bool ExecuteQueryDataset(string stMysqlParam, OdbcConnection appadoConect, string NombreProcedimiento, ref DataSet DsDataset, string NombreTabla)
        {
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            stMysqlParam = Strings.Replace(stMysqlParam, "''", "' '", 1, -1, CompareMethod.Text);
            mycomqueryconec.CommandText = stMysqlParam;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.ExecuteNonQuery();

            Myread.SelectCommand = mycomqueryconec;
            Myread.Fill(DsDataset, NombreTabla);

            if (DsDataset.Tables[NombreTabla].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region BuscarAsociadoEnListas (VB line 15420) - overloads

        public bool BuscarAsociadoEnListas(string codigoter, string Lista, OdbcConnection myconnect, ref DataSet dsdata)
        {
            DateTime fechanov = default(DateTime);
            return BuscarAsociadoEnListas(codigoter, Lista, myconnect, ref dsdata, ref fechanov);
        }

        public bool BuscarAsociadoEnListas(string codigoter, string Lista, OdbcConnection myconnect, ref DataSet dsdata, ref DateTime fechanov)
        {
            string sql = "";
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            if (fechanov == default(DateTime))
            {
                sql = " and fecha=(select max(fecha) from cop_listanegra where codigoter='" + codigoter + "' and codlista='" + Lista + "')";
            }
            else
            {
                sql = " and fecha='" + fechanov.ToString(varini.PstForFec) + "'";
            }

            stmysql = "select estado,fecha,tipo_nit, nit, tipo_persona, razon_social, nombre, nacionalidad,direccion, telefono, observaciones,fechVence from cop_listanegra " +
                " where codigoter='" + codigoter + "' and codlista='" + Lista + "' " + sql;

            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarAsociadoEnListas", ref dsdata, "Tbllista");
            return ok;
        }

        // Overridable overload: BuscarAsociadoEnListas(codigoter, myconnect, ref lista, ref FechaVence)
        public virtual bool BuscarAsociadoEnListas(string codigoter, OdbcConnection myconnect, ref string lista, ref string FechaVence)
        {
            DataSet dsdata = new DataSet();
            int i = 0;
            codigoter = Strings.Right("00000000000000" + codigoter, 14);

            stmysql = "select a.codigoter,a.codlista,a.estado,a.fecha,a.nit,a.nombre,a.tipo_nit,a.razon_social,a.tipo_persona,a.fechVence,a.nacionalidad,a.direccion,a.telefono,a.observaciones,b.Vali_fech_venci  from cop_listanegra a inner join sys_parlistas b  on  a.codlista = b.codigo  where a.codigoter='" + codigoter + "' and " +
                "a.fecha=(select max(fecha) from cop_listanegra b where a.codlista = b.codlista And a.codigoter = b.codigoter group by codlista)";
            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarAsociadoEnListas", ref dsdata, "TblLisAsociado");
            switch (ok)
            {
                case true:
                    DateTime compareFecha = new DateTime(1950, 1, 1);

                    ok = false;
                    while (i < dsdata.Tables["TblLisAsociado"].Rows.Count)
                    {
                        if (dsdata.Tables["TblLisAsociado"].Rows[i]["estado"].ToString() == "A")
                        {
                            switch (dsdata.Tables["TblLisAsociado"].Rows[i]["Vali_fech_venci"].ToString())
                            {
                                case "Y":
                                    if (DateTime.Compare(compareFecha, Convert.ToDateTime(dsdata.Tables["TblLisAsociado"].Rows[i]["fechVence"])) < 0)
                                    {
                                        if (DateTime.Compare(Convert.ToDateTime(dsdata.Tables["TblLisAsociado"].Rows[i]["fechVence"]), DateTime.Now) > 0)
                                        {
                                            lista = dsdata.Tables["TblLisAsociado"].Rows[i]["codlista"].ToString();
                                            ok = true;
                                            FechaVence = dsdata.Tables["TblLisAsociado"].Rows[i]["fechVence"].ToString();
                                        }
                                        else
                                        {
                                            this.GrabarAsociadoEnListas(dsdata.Tables["TblLisAsociado"].Rows[i]["codigoter"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["codlista"].ToString(), Convert.ToDateTime(dsdata.Tables["TblLisAsociado"].Rows[i]["fecha"]), "R", dsdata.Tables["TblLisAsociado"].Rows[i]["nit"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["nombre"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["razon_social"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["tipo_nit"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["tipo_persona"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["nacionalidad"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["direccion"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["telefono"].ToString(), dsdata.Tables["TblLisAsociado"].Rows[i]["observaciones"].ToString(), myconnect, "N", Convert.ToDateTime(dsdata.Tables["TblLisAsociado"].Rows[i]["fechVence"]));
                                            ok = false;
                                        }
                                    }
                                    break;
                                case "N":
                                    lista = dsdata.Tables["TblLisAsociado"].Rows[i]["codlista"].ToString();
                                    ok = true;
                                    break;
                            }
                        }
                        else
                        {
                            ok = false;
                        }
                        i = i + 1;
                    }
                    break;
            }
            return ok;
        }

        // Convenience overloads for default ref params
        public virtual bool BuscarAsociadoEnListas(string codigoter, OdbcConnection myconnect)
        {
            string lista = "0";
            string FechaVence = " ";
            return BuscarAsociadoEnListas(codigoter, myconnect, ref lista, ref FechaVence);
        }

        #endregion

        #region GrabarAsociadoEnListas (VB line 15475)

        public bool GrabarAsociadoEnListas(string codigoter, string lista, DateTime fecha, string estado, string cedula, string nombre, string RazonSocial,
            string TipoId, string TipoPersona, string Nacionalidad, string Direccion, string Telefono, string Observacion, OdbcConnection myconnect, string Importado = "N", DateTime fechVence = default(DateTime))
        {
            if (fechVence == default(DateTime))
            {
                fechVence = new DateTime(1950, 1, 1);
            }
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            ok = this.BuscarAsociadoEnListas(codigoter, lista, myconnect, ref dsdata, ref fecha);
            switch (ok)
            {
                case false:
                    stbuilder.Append("insert into cop_listanegra (codigoter, codlista, fecha, estado, tipo_nit, nit, tipo_persona, razon_social, nombre, nacionalidad,");
                    stbuilder.Append("direccion, telefono, observaciones, Importado,fechVence) values('");
                    stbuilder.Append(codigoter + "','");
                    stbuilder.Append(lista + "','");
                    stbuilder.Append(fecha.ToString(varini.PstForFec) + "','");
                    stbuilder.Append(estado + "','");
                    stbuilder.Append(TipoId + "','");
                    stbuilder.Append(cedula + "','");
                    stbuilder.Append(TipoPersona + "','");
                    stbuilder.Append(RazonSocial + "','");
                    stbuilder.Append(nombre + "',");
                    stbuilder.Append(Nacionalidad + ",'");
                    stbuilder.Append(Direccion + "','");
                    stbuilder.Append(Telefono + "','");
                    stbuilder.Append(Observacion + "','");
                    stbuilder.Append(Importado + "','");
                    stbuilder.Append(fechVence.ToString(varini.pstForfecyHora) + "')");
                    break;
                case true:
                    stbuilder.Append("update cop_listanegra set tipo_nit='" + TipoId + "',");
                    stbuilder.Append("nit='" + cedula + "',");
                    stbuilder.Append("estado='" + estado + "',");
                    stbuilder.Append("tipo_persona='" + TipoPersona + "',");
                    stbuilder.Append("razon_social='" + RazonSocial + "',");
                    stbuilder.Append("nombre='" + nombre + "',");
                    stbuilder.Append("Nacionalidad=" + Nacionalidad + ",");
                    stbuilder.Append("Direccion='" + Direccion + "',");
                    stbuilder.Append("Telefono='" + Telefono + "',");
                    stbuilder.Append("Observaciones='" + Observacion + "',");
                    stbuilder.Append("fechVence='" + fechVence.ToString(varini.pstForfecyHora) + "'   where codigoter='" + codigoter + "' and codlista='" + lista + "' and fecha='" + fecha.ToString(varini.PstForFec) + "'");
                    break;
            }
            ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabarAsociadoEnListas");
            return ok;
        }

        #endregion

        #region PlanoListasNegras (VB line 15518)

        public bool PlanoListasNegras(string nomarchivo, Form forma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Cargando Archivo Plano", forma);
            decimal TotReg;
            string line;
            bool okk;
            ArrayList arreglo = new ArrayList();
            bool NoExisteFicha;
            string lista, codigo, tiponit, cedula, nombre, razsocial;
            string tipopersona, nacionalidad, fecha, direccion, telefono, observacion;
            string CodListas = "";
            strStreamReader = new StreamReader(nomarchivo);
            line = strStreamReader.ReadLine();
            strStreamReader.Close();
            try
            {
                ok = this.ValidarPlanoListaNegra(nomarchivo, forma, myconnect);
                if (ok == true)
                {
                    TotReg = Math.Round((decimal)(FileSystem.FileLen(nomarchivo) / (line.Length + 2)));
                    BarraProgreso.ValorMinimoMaximo(0, (int)TotReg);
                    BarraProgreso.Show();

                    FileSystem.FileOpen(1, nomarchivo, OpenMode.Input);

                    while (FileSystem.EOF(1) == false)
                    {
                        lista = ""; codigo = ""; tiponit = ""; cedula = ""; nombre = ""; razsocial = "";
                        tipopersona = ""; nacionalidad = ""; direccion = ""; telefono = ""; fecha = ""; observacion = "";
                        FileSystem.Input(1, ref lista);
                        FileSystem.Input(1, ref codigo);
                        FileSystem.Input(1, ref tiponit);
                        FileSystem.Input(1, ref cedula);
                        FileSystem.Input(1, ref nombre);
                        FileSystem.Input(1, ref razsocial);
                        FileSystem.Input(1, ref tipopersona);
                        FileSystem.Input(1, ref nacionalidad);
                        FileSystem.Input(1, ref direccion);
                        FileSystem.Input(1, ref telefono);
                        FileSystem.Input(1, ref fecha);
                        FileSystem.Input(1, ref observacion);
                        if (CodListas.Contains(lista) == false)
                        {
                            CodListas += lista + ",";
                        }
                        this.GrabarAsociadoEnListas(codigo, lista, Convert.ToDateTime(fecha), "A", cedula, nombre, razsocial, tiponit, tipopersona, nacionalidad, direccion, telefono, observacion, myconnect, "Y");
                        BarraProgreso.PerformStep();
                    }
                    BarraProgreso.Close();
                    BarraProgreso.Dispose();
                    FileSystem.FileClose(1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                FileSystem.FileClose(1);
                ok = false;
            }
            return ok;
        }

        #endregion

        #region ValidarPlanoListaNegra (VB line 15574)

        public bool ValidarPlanoListaNegra(string nomarchivo, Form forma, OdbcConnection myconnect)
        {
            string mensaje = "";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Verificando Archivo Plano", forma);
            decimal TotReg;
            string line;
            bool okk;
            ArrayList arreglo = new ArrayList();
            bool NoExisteFicha = false;
            string nomruta = AppDomain.CurrentDomain.BaseDirectory + "\\ListasNegras.txt";
            string lista, codigo, tiponit, cedula, nombre, razsocial;
            string tipopersona, nacionalidad, fecha;

            okk = true;
            strStreamWriter = new StreamWriter(nomruta, false);
            strStreamReader = new StreamReader(nomarchivo);
            line = strStreamReader.ReadLine();

            try
            {
                TotReg = Math.Round((decimal)(FileSystem.FileLen(nomarchivo) / (line.Length + 2)));
                arreglo.Clear();

                BarraProgreso.ValorMinimoMaximo(0, (int)TotReg);
                BarraProgreso.Show();

                arreglo.Add("Cod. Lista");
                arreglo.Add("Codigo");
                strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                while (line.Trim().Length > 20)
                {
                    mensaje = "";
                    arreglo.Clear();
                    arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                    lista = (string)arreglo[0];
                    codigo = (string)arreglo[1];
                    tiponit = (string)arreglo[2];
                    cedula = (string)arreglo[3];
                    nombre = (string)arreglo[4];
                    tipopersona = (string)arreglo[6];
                    nacionalidad = (string)arreglo[7];
                    fecha = (string)arreglo[10];
                    arreglo.Clear();
                    arreglo.Add(lista);
                    arreglo.Add(codigo);

                    VerificarDatosLista(lista, codigo, tiponit, cedula, nombre, tipopersona, nacionalidad, fecha, myconnect, ref mensaje);

                    if (mensaje.Trim() != "")
                    {
                        NoExisteFicha = true;
                        arreglo.Add(mensaje);
                        strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "         "));
                    }

                    line = strStreamReader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    BarraProgreso.PerformStep();
                }
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                strStreamReader.Close();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                if (NoExisteFicha == true)
                {
                    // VB: MsgBox(OkOnly) is always truthy
                    MessageBox.Show("El archivo plano tiene los siguientes errores.", "SOLIDO", MessageBoxButtons.OK);
                    System.Diagnostics.Process.Start(nomruta);
                    okk = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                okk = false;
            }
            return okk;
        }

        #endregion

        #region VerificarDatosLista (VB line 15650)

        public void VerificarDatosLista(string lista, string codigoter, string tiponit, string cedula, string nombre, string tipopersona, string nacionalidad, string fecha, OdbcConnection myconnect)
        {
            string mensaje = "";
            VerificarDatosLista(lista, codigoter, tiponit, cedula, nombre, tipopersona, nacionalidad, fecha, myconnect, ref mensaje);
        }

        public void VerificarDatosLista(string lista, string codigoter, string tiponit, string cedula, string nombre, string tipopersona, string nacionalidad, string fecha, OdbcConnection myconnect, ref string mensaje)
        {
            if (Information.IsNumeric(lista) == false)
            {
                mensaje = "El codigo de la lista debe ser numerico.  ";
            }
            else
            {
                // ok = this.msgcofsys.BuscarListaNegra(lista, myconnect); // ERROR: CS1061
                if (ok == false)
                {
                    mensaje = "El codigo de la lista no existe. Por favor cree esta lista.  ";
                }
            }
            if (Information.IsNumeric(codigoter) == false)
            {
                mensaje += "El codigo debe ser numerico.  ";
            }
            if (Information.IsNumeric(tiponit) == false)
            {
                mensaje += "El Tipo de identificacion debe ser numerico.  ";
            }
            else if (Convert.ToInt32(tiponit) < 0 || Convert.ToInt32(tiponit) > 5)
            {
                mensaje += "Tipo de identificacion esta fuera de intervalo.  ";
            }
            if (Information.IsNumeric(cedula) == false)
            {
                mensaje += "El No de identificacion debe ser numerico.  ";
            }
            if (nombre.Trim() == "")
            {
                mensaje += "No se pueden agregar personas que no tengan nombre.  ";
            }
            if (Information.IsNumeric(tipopersona) == false)
            {
                mensaje += "La clase de persona debe ser numerico.  ";
            }
            else if (Convert.ToInt32(tipopersona) < 0 || Convert.ToInt32(tipopersona) > 1)
            {
                mensaje += "Clase de persona esta fuera de intervalo.  ";
            }
            if (Information.IsNumeric(nacionalidad) == false)
            {
                mensaje += "El codigo de la nacionalidad debe ser numerico.  ";
            }
            else
            {
                // ok = this.msgcofsys.BuscarPaises(nacionalidad, myconnect); // ERROR: CS1061
                if (ok == false)
                {
                    mensaje += "Codigo de nacionalidad no existe.  ";
                }
            }
            if (Information.IsDate(fecha) == false)
            {
                mensaje += "Debe ingresar una fecha valida.  ";
            }
        }

        #endregion

        #region SaldoDeudasRecogidas (VB line 15693) - with overloads for Optional ByRef

        public double SaldoDeudasRecogidas(int numsolicitud, OdbcConnection myconnect)
        {
            double VlrCuota = 0, vlrCuotaNomina = 0, vlrCuotaCaja = 0, vrlsaldoTotaTOTPAR = 0, saldoTotal = 0;
            return SaldoDeudasRecogidas(numsolicitud, myconnect, ref VlrCuota, ref vlrCuotaNomina, ref vlrCuotaCaja, ref vrlsaldoTotaTOTPAR, ref saldoTotal);
        }

        public double SaldoDeudasRecogidas(int numsolicitud, OdbcConnection myconnect, ref double VlrCuota, ref double vlrCuotaNomina, ref double vlrCuotaCaja, ref double vrlsaldoTotaTOTPAR, ref double saldoTotal)
        {
            double saldo = 0, cuota = 0, cuotaNomina = 0, cuotaCaja = 0, _vrlsaldoTotaTOTPAR = 0;
            DataSet dsdata = new DataSet();
            int i = 0;
            double _saldoTotal = 0;
            stmysql = "select solrecr.valor_pago,(case maecar.CICLOD when '5' then maecar.CUOTA*maecar.periodd else maecar.cuota end)  as  cuota ,solrecr.TOTPAR,maecar.CLADES  from cop_solrecr solrecr " +
                      " inner join cop_maecar maecar on solrecr.codigoter=maecar.codigoter " +
                      "and solrecr.lincred=maecar.lincred and solrecr.nume_cred=maecar.numero " +
                      "where solrecr.numero = " + numsolicitud;
            ok = this.ExecuteQueryDataset(stmysql, myconnect, "SaldoDeudasRecogidas", ref dsdata, "TblDeudasRecogidas");
            if (ok == true)
            {
                while (i < dsdata.Tables["TblDeudasRecogidas"].Rows.Count)
                {
                    saldo = Convert.ToDouble(dsdata.Tables["TblDeudasRecogidas"].Rows[i]["valor_pago"]);
                    _saldoTotal += Convert.ToDouble(dsdata.Tables["TblDeudasRecogidas"].Rows[i]["valor_pago"]);
                    if (dsdata.Tables["TblDeudasRecogidas"].Rows[i]["TOTPAR"].ToString() == "T")
                    {
                        cuota = cuota + Convert.ToDouble(dsdata.Tables["TblDeudasRecogidas"].Rows[i]["cuota"]);
                        _vrlsaldoTotaTOTPAR += Convert.ToDouble(dsdata.Tables["TblDeudasRecogidas"].Rows[i]["valor_pago"]);
                        switch (dsdata.Tables["TblDeudasRecogidas"].Rows[i]["CLADES"].ToString().Trim())
                        {
                            case "1":
                                cuotaNomina = cuotaNomina + Convert.ToDouble(dsdata.Tables["TblDeudasRecogidas"].Rows[i]["cuota"]);
                                break;
                            case "2":
                                cuotaCaja = cuotaCaja + Convert.ToDouble(dsdata.Tables["TblDeudasRecogidas"].Rows[i]["cuota"]);
                                break;
                        }
                    }
                    i = i + 1;
                }
            }
            VlrCuota = cuota;
            vlrCuotaNomina = cuotaNomina;
            vlrCuotaCaja = cuotaCaja;
            vrlsaldoTotaTOTPAR = _vrlsaldoTotaTOTPAR;
            saldoTotal = _saldoTotal;
            return saldo;
        }

        #endregion

        #region ActualizarAsociado (VB line 15729)

        public bool ActualizarAsociado(string codigoter, DataSet dsdata, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            DataRow row = dsdata.Tables["TblActualizaAsoc"].Rows[0];
            stbuilder.Append("update sys_maenit set direccion='" + row["direccion"] + "',");
            stbuilder.Append("telefono1='" + row["telefono"] + "',");
            stbuilder.Append("estado_civil='" + row["estadocivil"] + "',");
            stbuilder.Append("Fecnacem='" + Convert.ToDateTime(row["FecNacimiento"]).ToString(varini.PstForFec) + "',");
            stbuilder.Append("empresa_labora='" + row["EmpresaLabora"] + "',");
            stbuilder.Append("FeIng_Empresa='" + Convert.ToDateTime(row["FeIngEmpresa"]).ToString(varini.PstForFec) + "',");
            stbuilder.Append("contracto='" + row["TipoContrato"] + "',");
            stbuilder.Append("salario='" + row["salario"] + "',OTRO_INGRESO='" + row["OTRO_INGRESO"] + "',");
            stbuilder.Append("conyuge='" + row["Nomconyuge"] + "',");
            stbuilder.Append("conytelef='" + row["Teleconyuge"] + "',");
            stbuilder.Append("conydirec='" + row["Dirempconyuge"] + "',");
            stbuilder.Append("conyempr='" + row["Empconyuge"] + "',");
            stbuilder.Append("conyciud='" + row["Ciuconyuge"] + "',");
            stbuilder.Append("conysalar='" + row["salarconyuge"] + "', ");
            stbuilder.Append("cesantias='" + row["cesantias"] + "', ");
            stbuilder.Append("cargo='" + row["idcargo"] + "', ");
            stbuilder.Append("DPTO_CIUDAD='" + row["idciudad"] + "', ");
            stbuilder.Append("IngArriendos=" + Convert.ToDouble(row["TxtIngArriendos"]) + ", ");
            stbuilder.Append("IngPension=" + Convert.ToDouble(row["TxtPensiones"]) + ", ");
            stbuilder.Append("DeudasTerceros =" + Convert.ToDouble(row["TxtDeudasTerceros"]) + ", ");
            stbuilder.Append("GASTO_FIJO_MES =" + Convert.ToDouble(row["TxtGastosMes"]) + ", ");
            stbuilder.Append("DstoPension =" + Convert.ToDouble(row["TxtDstoPension"]) + ", ");
            stbuilder.Append("cappagoPorcentaje  ='" + row["ChkPorcentaje"] + "', ");
            stbuilder.Append("dstoGastosPerso  =" + Convert.ToDouble(row["TxtGastosPnales"]) + ", ");
            stbuilder.Append("IngVariables   =" + Convert.ToDouble(row["TxtIngVariables"]) + " ");
            stbuilder.Append("where codigoter='" + codigoter + "'");

            ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "ActualizarAsociado");
            return ok;
        }

        #endregion

        #region VerificarSobreGiro (VB line 15766) - with overloads for Optional ByRef

        public bool VerificarSobreGiro(string codigoter, int lincred, double numero, string TipoTransaccion,
            double debito, double credito, int periodo, string usuario, Form forma, OdbcConnection myconnect)
        {
            string UsuAprobo = " ";
            double VlrSobregiro = 0;
            return VerificarSobreGiro(codigoter, lincred, numero, TipoTransaccion, debito, credito, periodo, usuario, forma, myconnect, ref UsuAprobo, ref VlrSobregiro);
        }

        public bool VerificarSobreGiro(string codigoter, int lincred, double numero, string TipoTransaccion,
            double debito, double credito, int periodo, string usuario, Form forma, OdbcConnection myconnect,
            ref string UsuAprobo, ref double VlrSobregiro)
        {
            string tipo_movto = "0";
            double saldo = 0;
            bool sobregiro = false;
            double saldonew = 0;
            bool controlsaldo = false;
            // MsgConfig.FrmLogeo frmlogin = new MsgConfig.FrmLogeo(myconnect); // ERROR: CS0246
            ok = false;
            // this.msgconfig.BuscaLinea(lincred, myconnect, ref _dummy1, ref _dummy2, ref _dummy3, ref _dummy4, ref _dummy5, ref _dummy6, ref _dummy7, ref _dummy8, ref _dummy9, ref _dummy10, ref _dummy11, ref _dummy12, ref _dummy13, ref _dummy14, ref controlsaldo); // ERROR: CS7036
            switch (controlsaldo)
            {
                case true:
                    // this.msgcofsys.BuscaUsuario(usuario, myconnect, ref _dummy1, ref _dummy2, ref _dummy3, ref _dummy4, ref _dummy5, ref _dummy6, ref _dummy7, ref _dummy8, ref _dummy9, ref _dummy10, ref _dummy11, ref _dummy12, ref _dummy13, ref _dummy14, ref _dummy15, ref sobregiro); // ERROR: CS1501
                    // BuscaTipoMovto(TipoTransaccion, myconnect, ref tipo_movto); // ERROR: CS7036
                    this.BuscaSaldoObligacion(codigoter, lincred, numero, periodo, myconnect, ref saldo);
                    saldonew = (saldo < 0) ? saldo * -1 : saldo;
                    switch (tipo_movto)
                    {
                        case "1":
                        case "10":
                            if ((credito > 0 && credito > saldonew) || (saldo <= 0 && credito > 0))
                            {
                                if (MessageBox.Show("Esta obligacion va a quedar en sobregiro. Desea continuar?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                                {
                                    if (sobregiro == false)
                                    {
                                        // if (frmlogin.ShowDialog(forma) == DialogResult.OK) // ERROR: CS0103
                                        {
                                            // UsuAprobo = frmlogin.Tag.ToString(); // ERROR: CS0103
                                            if (saldo <= 0)
                                            {
                                                VlrSobregiro = credito;
                                            }
                                            else
                                            {
                                                VlrSobregiro = credito - saldonew;
                                            }
                                            ok = true;
                                        }
                                        // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                                        {
                                            ok = false;
                                        }
                                    }
                                    else
                                    {
                                        UsuAprobo = usuario;
                                        if (saldo <= 0)
                                        {
                                            VlrSobregiro = credito;
                                        }
                                        else
                                        {
                                            VlrSobregiro = credito - saldonew;
                                        }
                                        ok = true;
                                    }
                                }
                            }
                            else
                            {
                                ok = true;
                            }
                            break;
                        case "4":
                        case "7":
                            if ((debito > 0 && debito > saldonew) || (saldo >= 0 && debito > 0))
                            {
                                if (MessageBox.Show("Esta obligacion va a quedar en sobregiro. Desea continuar?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                                {
                                    if (sobregiro == false)
                                    {
                                        // if (frmlogin.ShowDialog() == DialogResult.OK) // ERROR: CS0103
                                        {
                                            if (saldo >= 0)
                                            {
                                                VlrSobregiro = debito;
                                            }
                                            else
                                            {
                                                VlrSobregiro = debito - saldonew;
                                            }
                                            ok = true;
                                            // UsuAprobo = frmlogin.Tag.ToString(); // ERROR: CS0103
                                        }
                                        // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                                        {
                                            ok = false;
                                        }
                                    }
                                    else
                                    {
                                        UsuAprobo = usuario;
                                        if (saldo >= 0)
                                        {
                                            VlrSobregiro = debito;
                                        }
                                        else
                                        {
                                            VlrSobregiro = debito - saldonew;
                                        }
                                        ok = true;
                                    }
                                }
                            }
                            else
                            {
                                ok = true;
                            }
                            break;
                        default:
                            ok = true;
                            break;
                    }
                    return ok;
                case false:
                    return true;
            }
            return ok;
        }

        // Dummy fields for BuscaLinea/BuscaUsuario optional ref params
        private string _dummy1 = "", _dummy2 = "", _dummy3 = "", _dummy4 = "", _dummy5 = "";
        private string _dummy6 = "", _dummy7 = "", _dummy8 = "", _dummy9 = "", _dummy10 = "";
        private string _dummy11 = "", _dummy12 = "", _dummy13 = "", _dummy14 = "", _dummy15 = "";

        #endregion

        #region NomArchivosCuentaCobro (VB line 15847)

#if CRYSTAL_LEGACY
        public void NomArchivosCuentaCobro(CrystalDecisions.CrystalReports.Engine.ReportDocument INFO, string Empresa, string Agencia,
            string nombreCompania, string NombreEmpresa, string periodicidad, int Ciclo, DateTime FechaIni,
            DateTime fechaFin, string Periodocidadnombre, string Cencos, int Adicional, int PerIni,
            int Perfin, string Nota1, string nota2, string UserCartera, string ConceptosFondo,
            string Directorio, int PeriodoCartera)
        {
            try
            {
                ArrayList lis = new ArrayList();

                INFO.SetParameterValue("nombre_empresa", nombreCompania);
                INFO.SetParameterValue("empresa", NombreEmpresa);
                INFO.SetParameterValue("ciclo", Ciclo);
                INFO.SetParameterValue("fec_ini", FechaIni);
                INFO.SetParameterValue("fec_fin", fechaFin);
                INFO.SetParameterValue("peri", Ciclo);
                INFO.SetParameterValue("periodicidad", periodicidad);
                INFO.SetParameterValue("cencosto", Cencos);
                INFO.SetParameterValue("adicional", Adicional);
                INFO.SetParameterValue("codempresa", Empresa);
                INFO.SetParameterValue("agencia", Agencia);
                INFO.SetParameterValue("AtraInicial", PerIni);
                INFO.SetParameterValue("AtraFinal", Perfin);
                INFO.SetParameterValue("nota1", Nota1);
                INFO.SetParameterValue("nota2", nota2);
                INFO.SetParameterValue("Usercartera", UserCartera);
                INFO.SetParameterValue("periodo_contable", PeriodoCartera);
                if (ConceptosFondo.Trim() == "")
                {
                    ConceptosFondo = "0";
                }
                lis.AddRange(Strings.Split(ConceptosFondo, ",", -1, CompareMethod.Text));
                INFO.SetParameterValue("ConceptosFondo", lis.ToArray());

                INFO.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat,
                                             Directorio + "\\" + Ciclo + Empresa + ".pdf");

                INFO.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.ExcelRecord,
                                        Directorio + "\\" + Ciclo + Empresa + ".xls");
                INFO.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
#endif

        #endregion

        #region NomEnviarCorreoCuenCobro (VB line 15893)

        public void NomEnviarCorreoCuenCobro(string Periodo, Form Forma, OdbcConnection conect)
        {
            ERP.Core.Compartido.Configuracion.ParamSys parasys = new ERP.Core.Compartido.Configuracion.ParamSys();
            ERP.Core.Compartido.Controles.Barraprogress pro = new ERP.Core.Compartido.Controles.Barraprogress("Enviando cuentas de cobro por nomina a email.", Forma);
            FolderBrowserDialog dire = new FolderBrowserDialog();
            dire.Description = "Buscar directorio";
            dire.ShowNewFolderButton = false;
            dire.RootFolder = Environment.SpecialFolder.MyComputer;
            string ServerSmtp = "", PasswordEnvio = "", CorreoEnvio = "";
            ArrayList archivos = new ArrayList();
            string Destino, Asunto;
            ArrayList RutasArchivo = new ArrayList();
            string Empresa;
            string nomcompaniaresum = "  ";
            try
            {
                if (dire.ShowDialog() == DialogResult.OK)
                {
                    archivos.AddRange(Directory.GetFiles(dire.SelectedPath, Periodo + "*", SearchOption.TopDirectoryOnly));
                    if (archivos != null)
                    {
                        // BuscarCompania: p17=nomcompaniaresum, p49=ServerSmtp, p50=PasswordEnvio, p51=CorreoEnvio
                        string _u1 = "", _u2 = "", _u3 = "", _u4 = "", _u5 = "", _u6 = "", _u7 = "", _u8 = "", _u9 = "", _u10 = "";
                        string _u11 = "", _u12 = "", _u13 = "", _u14 = "", _u15 = "", _u16 = "";
                        string _u18 = "", _u19 = "", _u20 = "";
                        string _u21 = "", _u22 = "", _u23 = "", _u24 = "", _u25 = "", _u26 = "", _u27 = "", _u28 = "", _u29 = "", _u30 = "";
                        string _u31 = "", _u32 = "", _u33 = "", _u34 = "", _u35 = "", _u36 = "", _u37 = "", _u38 = "", _u39 = "", _u40 = "";
                        string _u41 = "", _u42 = "", _u43 = "", _u44 = "", _u45 = "", _u46 = "", _u47 = "", _u48 = "";
                        // parasys.BuscarCompania(varini.sptCodEmpr, conect, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u6, ref _u7, ref _u8, ref _u9, ref _u10, // ERROR: CS7036
                            // ref _u11, ref _u12, ref _u13, ref _u14, ref _u15, ref nomcompaniaresum, ref _u18, ref _u19, ref _u20, // ERROR: CS7036
                            // ref _u21, ref _u22, ref _u23, ref _u24, ref _u25, ref _u26, ref _u27, ref _u28, ref _u29, ref _u30, // ERROR: CS7036
                            // ref _u31, ref _u32, ref _u33, ref _u34, ref _u35, ref _u36, ref _u37, ref _u38, ref _u39, ref _u40, // ERROR: CS7036
                            // ref _u41, ref _u42, ref _u43, ref _u44, ref _u45, ref _u46, ref _u47, ref _u48, // ERROR: CS7036
                            // ref ServerSmtp, ref PasswordEnvio, ref CorreoEnvio); // ERROR: CS7036

                        Asunto = "Cuenta de cobro " + nomcompaniaresum + "  " + Periodo;
                        DataSet ds = new DataSet();
                        string sql = "Select email ,CODIGO_EMPRESA from cop_empresa13 where email like '%@%'  and CODIGO_EMPRESA <> '9999'";
                        this.OdbcConnect.ExecuteQueryDataset(sql, conect, "NomEnviarCorreoCuenCobro", ref ds, "Empre");
                        pro.ValorMinimoMaximo(0, ds.Tables["Empre"].Rows.Count);

                        pro.Show();
                        for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
                        {
                            Application.DoEvents();
                            pro.PerformStep();
                            Destino = ds.Tables[0].Rows[i]["email"].ToString();
                            Empresa = ds.Tables[0].Rows[i]["CODIGO_EMPRESA"].ToString();
                            RutasArchivo.Clear();
                            if (archivos.Contains(dire.SelectedPath + "\\" + Periodo + Empresa + ".pdf") == true)
                            {
                                RutasArchivo.Add(dire.SelectedPath + "\\" + Periodo + Empresa + ".pdf");

                                if (archivos.Contains(dire.SelectedPath + "\\" + Periodo + Empresa + ".xls") == true)
                                {
                                    RutasArchivo.Add(dire.SelectedPath + "\\" + Periodo + Empresa + ".xls");
                                }

                                if (ServerSmtp.Trim() == "")
                                {
                                    // parasys.EnviarCorreoporOutlook(ServerSmtp, PasswordEnvio, CorreoEnvio, Destino, Asunto, // ERROR: CS1061
                                                               // "Adjunto cuenta de cobro " + nomcompaniaresum + " periodo " + Periodo, RutasArchivo); // ERROR: CS1061
                                }
                                else
                                {
                                    // parasys.EnviarCorreoporSolido(ServerSmtp, PasswordEnvio, CorreoEnvio, Destino, Asunto, // ERROR: CS1061
                                                             // "Adjunto cuenta de cobro " + nomcompaniaresum + "  periodo " + Periodo, RutasArchivo); // ERROR: CS1061
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            pro.Close();
        }

        #endregion

        #region CalcuFecProximoCiclo (VB line 15961) - with overloads for Optional ByRef

        // Overload: no optional params
        public void CalcuFecProximoCiclo(ref int PeriodoCausado, int Periodicidad, int CicloDesc)
        {
            DateTime FechaIniCiclo = new DateTime(1950, 1, 1);
            DateTime FechaFinCiclo = new DateTime(1950, 1, 1);
            CalcuFecProximoCiclo(ref PeriodoCausado, Periodicidad, CicloDesc, ref FechaIniCiclo, ref FechaFinCiclo, false);
        }

        // Overload: with FechaIniCiclo and FechaFinCiclo but no ProximoCiclo
        public void CalcuFecProximoCiclo(ref int PeriodoCausado, int Periodicidad, int CicloDesc, ref DateTime FechaIniCiclo, ref DateTime FechaFinCiclo)
        {
            CalcuFecProximoCiclo(ref PeriodoCausado, Periodicidad, CicloDesc, ref FechaIniCiclo, ref FechaFinCiclo, false);
        }

        public void CalcuFecProximoCiclo(ref int PeriodoCausado, int Periodicidad, int CicloDesc, ref DateTime FechaIniCiclo, ref DateTime FechaFinCiclo, bool ProximoCiclo)
        {
            double Reciduo = 0;
            int Anio, mes, ciclos;
            Reciduo = 0;
            Anio = Convert.ToInt32(Strings.Mid(PeriodoCausado.ToString(), 1, 4));
            mes = Convert.ToInt32(Strings.Mid(PeriodoCausado.ToString(), 5));
            if (Periodicidad == 1)
            {
                ciclos = 1;
            }
            else
            {
                if (CicloDesc == 5)
                {
                    ciclos = 1;
                }
                else
                {
                    if (CicloDesc == 1)
                    {
                        if (mes % 2 == 0)
                        {
                            ciclos = 1;
                        }
                        else
                        {
                            ciclos = 2;
                        }
                    }
                    else
                    {
                        ciclos = Periodicidad + CicloDesc;
                    }
                }
            }

            switch (Periodicidad)
            {
                case 1:
                    if (ProximoCiclo == true)
                    {
                        mes += ciclos;
                        if (mes > 12)
                        {
                            Anio += 1;
                            mes = 1;
                        }
                    }
                    FechaIniCiclo = new DateTime(Anio, mes, 1);
                    FechaFinCiclo = new DateTime(Anio, mes, DateTime.DaysInMonth(Anio, mes));
                    break;

                case 2:
                    if (ProximoCiclo == true)
                    {
                        mes += ciclos;
                        if (mes > 24)
                        {
                            Anio += 1;
                            mes = 1;
                        }
                    }
                    FechaIniCiclo = new DateTime(Anio, 1, 1);
                    FechaFinCiclo = new DateTime(Anio, 1, 15);
                    {
                        int InDia = 0;
                        for (int i = 2; i <= mes; i++)
                        {
                            FechaIniCiclo = FechaIniCiclo.AddDays(15 + InDia);
                            FechaFinCiclo = FechaFinCiclo.AddDays(15);
                            if (DateTime.DaysInMonth(FechaFinCiclo.Year, FechaFinCiclo.Month) == 31 && FechaFinCiclo.Day == 30)
                            {
                                FechaFinCiclo = new DateTime(FechaFinCiclo.Year, FechaFinCiclo.Month, DateTime.DaysInMonth(FechaFinCiclo.Year, FechaFinCiclo.Month));
                                InDia = 1;
                            }
                            else
                            {
                                InDia = 0;
                            }

                            if (FechaIniCiclo.Month < FechaFinCiclo.Month)
                            {
                                FechaFinCiclo = new DateTime(FechaIniCiclo.Year, FechaFinCiclo.Month - 1,
                                    DateTime.DaysInMonth(FechaIniCiclo.Year, FechaIniCiclo.Month));
                                if (DateTime.DaysInMonth(FechaFinCiclo.Year, FechaFinCiclo.Month) < 30)
                                {
                                    InDia = -1;
                                }
                                else
                                {
                                    InDia = 0;
                                }
                            }
                        }
                    }
                    break;

                case 3:
                    if (ProximoCiclo == true)
                    {
                        mes += ciclos;
                        if (mes > 36)
                        {
                            Anio += 1;
                            mes = 1;
                        }
                    }
                    FechaIniCiclo = new DateTime(Anio, 1, 1);
                    FechaFinCiclo = new DateTime(Anio, 1, 10);
                    {
                        int InDia = 0;
                        for (int i = 2; i <= mes; i++)
                        {
                            FechaIniCiclo = FechaIniCiclo.AddDays(10 + InDia);
                            FechaFinCiclo = FechaFinCiclo.AddDays(10);
                            if (DateTime.DaysInMonth(FechaFinCiclo.Year, FechaFinCiclo.Month) == 31 && FechaFinCiclo.Day == 30)
                            {
                                FechaFinCiclo = new DateTime(FechaFinCiclo.Year, FechaFinCiclo.Month, DateTime.DaysInMonth(FechaFinCiclo.Year, FechaFinCiclo.Month));
                                InDia = 1;
                            }
                            else
                            {
                                InDia = 0;
                            }

                            if (FechaIniCiclo.Month < FechaFinCiclo.Month)
                            {
                                FechaFinCiclo = new DateTime(FechaIniCiclo.Year, FechaFinCiclo.Month - 1,
                                    DateTime.DaysInMonth(FechaIniCiclo.Year, FechaIniCiclo.Month));
                                if (DateTime.DaysInMonth(FechaFinCiclo.Year, FechaFinCiclo.Month) < 30)
                                {
                                    InDia = -1;
                                }
                                else
                                {
                                    InDia = 0;
                                }
                            }
                        }
                    }
                    break;

                case 4:
                    if (ProximoCiclo == true)
                    {
                        mes += ciclos;
                        if (mes > 48)
                        {
                            Anio += 1;
                            mes = 1;
                        }
                    }
                    FechaIniCiclo = new DateTime(Anio, 1, 1);
                    FechaFinCiclo = new DateTime(Anio, 1, 7);
                    for (int i = 2; i <= mes; i++)
                    {
                        FechaIniCiclo = FechaIniCiclo.AddDays(7);
                        FechaFinCiclo = FechaFinCiclo.AddDays(7);
                    }
                    break;
            }
            if (ProximoCiclo == true)
            {
                PeriodoCausado = Convert.ToInt32(Anio.ToString() + Strings.Right("00" + mes.ToString(), 2));
            }
        }

        #endregion

        #region CargaGrillaCuopen (VB line 16089)

        public DataTable CargaGrillaCuopen(string Codigoter, string Lincred, string Numero, int Periodo,
            OdbcConnection MyConnect, int periodoIni = 0, int periodoFin = 999999)
        {
            string Lineas = "";
            string periodoCausa = " AND a.periodo_causa BETWEEN " + periodoIni + " and " + periodoFin;

            if (Lincred == "0" && Numero == "0")
            {
                Lineas = " ";
            }
            else
            {
                Lineas = " and a.lincred = " + Lincred + " and a.numero = " + Numero + " ";
            }
            DataSet ds = new DataSet("Datos");
            switch (varini.pstTipoBD.ToUpper())
            {
                case "DB2":
                    stmysql = " SELECT ( a.lincred || ' - ' || a.numero) as Obligacion,a.periodo_causa as PERIODO,";
                    break;
                case "MYSQL":
                    stmysql = " SELECT {fn concat( {fn concat(CAST(a.lincred AS CHAR),' - ')},CAST(a.numero AS CHAR))} as Obligacion,a.periodo_causa as PERIODO,";
                    break;
                default:
                    stmysql = " SELECT {fn concat( {fn concat(rtrim(a.lincred),' - ')},rtrim(a.numero))} as Obligacion,a.periodo_causa as PERIODO,";
                    break;
            }
            stmysql = stmysql +
                " a.saldointeres + a.saldocapital + a.saldoextra  + a.saldoseguro  + a.saldoadmon  + a.saldootros + a.saldomora as TOTAL," +
                " diasmora AS DIAS_MORA," +
                " saldocapital AS CAPITAL,saldointeres AS INTERES,saldoextra AS EXTRAS," +
                " saldoseguro + saldoadmon AS OTROS,saldomora AS SALDO_MORA ,c.fecha_movto AS FECHA_MOV FROM cop_copmora a " +
                " inner join cop_cuopen c on c.lincred = a.lincred " +
                " and c.numero = a.numero and c.codigoter = a.codigoter and c.periodo_causa = a.periodo_causa inner join cop_concar12 b on b.lincred = a.lincred " +
                " left join cop_salmaecar d on d.periodo = a.periodo_contable and " +
                " d.lincred = a.lincred and d.numero = a.numero and d.codigoter = a.codigoter where " +
                " (a.saldointeres + a.saldocapital + a.saldoextra  + a.saldoseguro  + a.saldoadmon  + a.saldootros + a.saldomora ) <> 0 " +
                " AND ((a.lincred >= 1000 and d.saldo > 0)or (a.lincred < 1000))" +
                " and a.codigoter ='" + Codigoter + "' and a.periodo_contable =  " + Periodo + Lineas + periodoCausa + "  order by a.lincred,a.numero,a.periodo_causa";
            ds.Tables.Add("Cuopen");
            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, MyConnect, "CargaGrillaCuopen", ref ds, "Cuopen");
            return ds.Tables[0];
        }

        #endregion

        #region CargaGrillaEstaCuenta (VB line 16125)

        public DataTable CargaGrillaEstaCuenta(string Codigoter, int Periodo,
            OdbcConnection MyConnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet ds = new DataSet("Datos");

            switch (varini.pstTipoBD.ToUpper())
            {
                case "POSTGRES":
                    stbuilder.Append("select  RTRIM(a.LINCRED) AS LINCRED  ,RTRIM(A.NUMERO) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(",A.FECFACT AS FECHA, A.FECDESC AS PRI_DESC ,");
                    stbuilder.Append(" A.TASAINT AS TASA,RTRIM(A.PLAZO) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" A.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  A.periodd WHEN '1' Then");
                    stbuilder.Append(" 'M'");
                    stbuilder.Append(" WHEN '2' Then");
                    stbuilder.Append(" 'Q'");
                    stbuilder.Append(" WHEN '3' Then");
                    stbuilder.Append(" 'D'");
                    stbuilder.Append(" WHEN '4' Then");
                    stbuilder.Append(" 'S'");
                    stbuilder.Append(" WHEN '5' Then");
                    stbuilder.Append(" 'D'");
                    stbuilder.Append(" Else ' ' END  AS PER_PAGO, ");
                    stbuilder.Append(" A.CICLOD AS CICLO,");
                    stbuilder.Append(" CASE  A.clades WHEN '1' Then");
                    stbuilder.Append(" 'N' ");
                    stbuilder.Append(" WHEN '2' Then ");
                    stbuilder.Append(" 'C'");
                    stbuilder.Append(" else ' ' END  AS F_PAG,");
                    stbuilder.Append(" RTRIM(c.cuopen) AS CUO_X_PAG, ");
                    stbuilder.Append(" RTRIM((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + ");
                    stbuilder.Append(" SaldoAdmon + SaldoOtros)  from cop_copmora D where D.codigoter = ");
                    stbuilder.Append(" A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO ");
                    stbuilder.Append("  AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable)) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN");
                    stbuilder.Append(" 'SI' WHEN 'P' THEN 'SI'");
                    stbuilder.Append(" ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,0 AS EXTRAS ");
                    stbuilder.Append(", CASE A.reEst WHEN 'Y' THEN 'SI' ELSE ' ' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = ");
                    stbuilder.Append(" A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = ");
                    stbuilder.Append(" A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c");
                    stbuilder.Append(" on c.codigoter = a.codigoter");
                    stbuilder.Append(" and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "'");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter ='" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000");
                    stbuilder.Append(" AND ( C.SALDO <> 0 OR A.CUOTA <> 0)");
                    stbuilder.Append(" UNION ALL");
                    stbuilder.Append(" select NULL ,NULL,rtrim(' '),null,null,null,NULL,NULL ,SUM(A.CUOTA) AS CUOTA,");
                    stbuilder.Append(" SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,rtrim(' '),rtrim(' '),rtrim(' '),NULL, NULL, rtrim(' '),rtrim(' '),rtrim(' '),0,rtrim(' '),0,0,999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c ");
                    stbuilder.Append(" on c.codigoter = a.codigoter ");
                    stbuilder.Append(" and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000");
                    stbuilder.Append(" AND ( C.SALDO <> 0 OR A.CUOTA <> 0)");
                    stbuilder.Append("  UNION ALL ");
                    stbuilder.Append(" select  RTRIM(a.LINCRED) AS LINCRED  ,RTRIM(A.NUMERO) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(" ,A.FECFACT AS FECHA, A.FECDESC AS PRI_DESC ,");
                    stbuilder.Append(" A.TASAINT AS TASA,RTRIM(A.PLAZO) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" A.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  A.periodd WHEN '1' Then");
                    stbuilder.Append(" 'M'");
                    stbuilder.Append(" WHEN '2' Then");
                    stbuilder.Append(" 'Q'");
                    stbuilder.Append(" WHEN '3' Then");
                    stbuilder.Append(" 'D'");
                    stbuilder.Append(" WHEN '4' Then");
                    stbuilder.Append(" 'S' ");
                    stbuilder.Append(" WHEN '5' Then");
                    stbuilder.Append(" 'D' ");
                    stbuilder.Append("Else ' ' END  AS PER_PAGO,");
                    stbuilder.Append(" A.CICLOD AS CICLO,");
                    stbuilder.Append(" CASE  A.clades WHEN '1' Then");
                    stbuilder.Append(" 'N'");
                    stbuilder.Append(" WHEN '2' Then");
                    stbuilder.Append(" 'C'");
                    stbuilder.Append(" else ' ' END  AS F_PAG,");
                    stbuilder.Append(" RTRIM(c.cuopen) AS CUO_X_PAG,");
                    stbuilder.Append(" RTRIM((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + ");
                    stbuilder.Append(" SaldoAdmon + SaldoOtros)  from cop_copmora D where D.codigoter = ");
                    stbuilder.Append(" A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO ");
                    stbuilder.Append("  AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable)) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN");
                    stbuilder.Append(" 'SI' WHEN 'P' THEN 'SI'");
                    stbuilder.Append(" ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,");
                    stbuilder.Append(" (Select count(*) as extras from cop_salextras where codigoter = A.CODIGOTER and lincred = ");
                    stbuilder.Append("  A.LINCRED and numero = A.NUMERO and periodo = C.PERIODO and saldo <> 0 )  AS EXTRAS");
                    stbuilder.Append(", CASE A.reEst WHEN 'Y' THEN 'SI' ELSE ' ' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = ");
                    stbuilder.Append(" A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = ");
                    stbuilder.Append(" A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c ");
                    stbuilder.Append(" on c.codigoter = a.codigoter ");
                    stbuilder.Append(" and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED >= 1000");
                    stbuilder.Append(" AND ( C.SALDO <> 0) ");
                    stbuilder.Append("   UNION ALL ");
                    stbuilder.Append(" select  NULL,NULL,rtrim(' '),null,null,null,NULL,NULL ,SUM(A.CUOTA) AS CUOTA,");
                    stbuilder.Append(" SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,rtrim(' '),rtrim(' '),rtrim(' '),NULL, NULL, rtrim(' '),rtrim(' '),rtrim(' '),0,rtrim(' '),0,0,9999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c ");
                    stbuilder.Append(" on c.codigoter = a.codigoter ");
                    stbuilder.Append(" and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "'  AND b.estcta = 'Y' AND A.LINCRED >= 1000 ");
                    stbuilder.Append(" AND ( C.SALDO <> 0) ");
                    break;

                case "DB2":
                    stbuilder.Append("select  RTRIM(a.LINCRED) AS LINCRED  ,RTRIM(A.NUMERO) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(",cast(A.FECFACT as char(12)) AS FECHA, cast(A.FECDESC as char(12)) AS PRI_DESC  ,");
                    stbuilder.Append(" C.TASAINT AS TASA,RTRIM(A.PLAZO) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" C.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  C.periodd WHEN '1' Then 'M' WHEN '2' Then 'Q' WHEN '3' Then 'D' WHEN '4' Then 'S' WHEN '5' Then 'D' Else ' ' END  AS PER_PAGO, ");
                    stbuilder.Append("C.CICLOD AS CICLO, CASE  C.clades WHEN '1' Then 'N'  WHEN '2' Then  'C' else ' ' END  AS F_PAG,");
                    stbuilder.Append(" RTRIM(c.cuopen) AS CUO_X_PAG, ");
                    stbuilder.Append(" RTRIM((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros) from cop_copmora D where D.codigoter = A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable)) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN 'SI' WHEN 'P' THEN 'SI' ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,0 AS EXTRAS ");
                    stbuilder.Append(", CASE A.reEst WHEN 'Y' THEN 'SI' ELSE ' ' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "'");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter ='" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000 AND ( C.SALDO <> 0 OR C.CUOTA <> 0)");
                    stbuilder.Append(" UNION ALL");
                    stbuilder.Append(" select '' ,'',rtrim(' '),'','','','','' ,SUM(C.CUOTA) AS CUOTA, SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,rtrim(' '),rtrim(' '),rtrim(' '),'', '', rtrim(' '),rtrim(' '),rtrim(' '),0,rtrim(' '),0,0,999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000 AND ( C.SALDO <> 0 OR C.CUOTA <> 0)");
                    stbuilder.Append("  UNION ALL ");
                    stbuilder.Append(" select  RTRIM(a.LINCRED) AS LINCRED  ,RTRIM(A.NUMERO) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(" ,cast(A.FECFACT as char(12)) AS FECHA, cast(A.FECDESC as char(12)) AS PRI_DESC ,");
                    stbuilder.Append(" C.TASAINT AS TASA,RTRIM(A.PLAZO) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" C.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  C.periodd WHEN '1' Then 'M' WHEN '2' Then 'Q' WHEN '3' Then 'D' WHEN '4' Then 'S'  WHEN '5' Then 'D' Else ' ' END  AS PER_PAGO,");
                    stbuilder.Append(" C.CICLOD AS CICLO, CASE  C.clades WHEN '1' Then 'N' WHEN '2' Then 'C' else ' ' END  AS F_PAG,");
                    stbuilder.Append(" RTRIM(c.cuopen) AS CUO_X_PAG,");
                    stbuilder.Append(" RTRIM((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros) from cop_copmora D where D.codigoter = A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable)) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN 'SI' WHEN 'P' THEN 'SI' ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,");
                    stbuilder.Append(" (Select count(*) as extras from cop_salextras where codigoter = A.CODIGOTER and lincred = A.LINCRED and numero = A.NUMERO and periodo = C.PERIODO and saldo <> 0 )  AS EXTRAS");
                    stbuilder.Append(", CASE A.reEst WHEN 'Y' THEN 'SI' ELSE ' ' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED >= 1000 AND ( C.SALDO <> 0) ");
                    stbuilder.Append("   UNION ALL ");
                    stbuilder.Append(" select  '','',rtrim(' '),'','','','','' ,SUM(c.CUOTA) AS CUOTA, SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,rtrim(' '),rtrim(' '),rtrim(' '),'', '', rtrim(' '),rtrim(' '),rtrim(' '),0,rtrim(' '),0,0,9999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "'  AND b.estcta = 'Y' AND A.LINCRED >= 1000 AND ( C.SALDO <> 0) ");
                    break;

                case "MYSQL":
                    stbuilder.Append("select  CAST(a.LINCRED AS CHAR) AS LINCRED  ,CAST(A.NUMERO AS CHAR) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(",A.FECFACT AS FECHA, A.FECDESC AS PRI_DESC ,");
                    stbuilder.Append(" C.TASAINT AS TASA,CAST(A.PLAZO AS CHAR) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" C.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  C.periodd WHEN '1' Then 'M' WHEN '2' Then 'Q' WHEN '3' Then 'D' WHEN '4' Then 'S' WHEN '5' Then 'D' Else ' ' END  AS PER_PAGO, ");
                    stbuilder.Append(" C.CICLOD AS CICLO, CASE  C.clades WHEN '1' Then 'N'  WHEN '2' Then  'C' else ' ' END  AS F_PAG,");
                    stbuilder.Append(" CAST(c.cuopen AS CHAR) AS CUO_X_PAG, ");
                    stbuilder.Append(" CAST((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros) from cop_copmora D where D.codigoter = A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable) AS CHAR) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN 'SI' WHEN 'P' THEN 'SI' ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,0 AS EXTRAS ");
                    stbuilder.Append(", CASE a.reEst WHEN 'Y' THEN 'SI' ELSE '' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "'");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter ='" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000 AND ( C.SALDO <> 0 OR C.CUOTA <> 0)");
                    stbuilder.Append(" UNION ALL");
                    stbuilder.Append(" select  ' ' ,' ',' ',null,null,null,' ',NULL ,SUM(c.CUOTA) AS CUOTA, SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,' ',' ',' ',' ', ' ' , ' ',' ',' ',0,' ',0,0,999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000 AND ( C.SALDO <> 0 OR C.CUOTA <> 0)");
                    stbuilder.Append("  UNION ALL ");
                    stbuilder.Append(" select  CAST(a.LINCRED AS CHAR) AS LINCRED  ,CAST(A.NUMERO AS CHAR) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(" ,A.FECFACT AS FECHA, A.FECDESC AS PRI_DESC ,");
                    stbuilder.Append(" C.TASAINT AS TASA,CAST(A.PLAZO AS CHAR) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" C.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  C.periodd WHEN '1' Then 'M' WHEN '2' Then 'Q' WHEN '3' Then 'D' WHEN '4' Then 'S'  WHEN '5' Then 'D' Else ' ' END  AS PER_PAGO,");
                    stbuilder.Append(" C.CICLOD AS CICLO, CASE  C.clades WHEN '1' Then 'N' WHEN '2' Then 'C' else ' ' END  AS F_PAG,");
                    stbuilder.Append(" CAST(c.cuopen AS CHAR) AS CUO_X_PAG,");
                    stbuilder.Append(" CAST((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros) from cop_copmora D where D.codigoter = A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable) AS CHAR) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN 'SI' WHEN 'P' THEN 'SI' ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,");
                    stbuilder.Append(" (Select count(*) as extras from cop_salextras where codigoter = A.CODIGOTER and lincred = A.LINCRED and numero = A.NUMERO and periodo = C.PERIODO and saldo <> 0 )  AS EXTRAS");
                    stbuilder.Append(", CASE a.reEst WHEN 'Y' THEN 'SI' ELSE '' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED >= 1000 AND ( C.SALDO <> 0) ");
                    stbuilder.Append("   UNION ALL ");
                    stbuilder.Append(" select  ' ' ,' ',' ',null,null,null,' ',NULL ,SUM(c.CUOTA) AS CUOTA, SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,' ',' ',' ',' ', ' ' , ' ',' ',' ',0,' ',0,0,9999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "'  AND b.estcta = 'Y' AND A.LINCRED >= 1000 AND ( C.SALDO <> 0) ");
                    break;

                default:
                    stbuilder.Append("select  RTRIM(a.LINCRED) AS LINCRED  ,RTRIM(A.NUMERO) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(",A.FECFACT AS FECHA, A.FECDESC AS PRI_DESC ,");
                    stbuilder.Append(" C.TASAINT AS TASA,RTRIM(A.PLAZO) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" C.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  C.periodd WHEN '1' Then 'M' WHEN '2' Then 'Q' WHEN '3' Then 'D' WHEN '4' Then 'S' WHEN '5' Then 'D' Else ' ' END  AS PER_PAGO, ");
                    stbuilder.Append(" C.CICLOD AS CICLO, CASE  C.clades WHEN '1' Then 'N'  WHEN '2' Then  'C' else ' ' END  AS F_PAG,");
                    stbuilder.Append(" RTRIM(c.cuopen) AS CUO_X_PAG, ");
                    stbuilder.Append(" RTRIM((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros) from cop_copmora D where D.codigoter = A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable)) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN 'SI' WHEN 'P' THEN 'SI' ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,0 AS EXTRAS ");
                    stbuilder.Append(", CASE a.reEst WHEN 'Y' THEN 'SI' ELSE '' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "'");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter ='" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000 AND ( C.SALDO <> 0 OR C.CUOTA <> 0)");
                    stbuilder.Append(" UNION ALL");
                    stbuilder.Append(" select  ' ' ,' ',' ',null,null,null,' ',NULL ,SUM(c.CUOTA) AS CUOTA, SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,' ',' ',' ',' ', ' ' , ' ',' ',' ',0,' ',0,0,999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED < 1000 AND ( C.SALDO <> 0 OR C.CUOTA <> 0)");
                    stbuilder.Append("  UNION ALL ");
                    stbuilder.Append(" select  RTRIM(a.LINCRED) AS LINCRED  ,RTRIM(A.NUMERO) AS NUMERO ,B.DESCRIPCION AS DETALLE");
                    stbuilder.Append(" ,A.FECFACT AS FECHA, A.FECDESC AS PRI_DESC ,");
                    stbuilder.Append(" C.TASAINT AS TASA,RTRIM(A.PLAZO) AS PLAZO ,A.VALOROB AS VAL_INICIAL,");
                    stbuilder.Append(" C.CUOTA AS CUOTA, CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE null END AS SALDO,");
                    stbuilder.Append(" CASE  C.periodd WHEN '1' Then 'M' WHEN '2' Then 'Q' WHEN '3' Then 'D' WHEN '4' Then 'S'  WHEN '5' Then 'D' Else ' ' END  AS PER_PAGO,");
                    stbuilder.Append(" C.CICLOD AS CICLO, CASE  C.clades WHEN '1' Then 'N' WHEN '2' Then 'C' else ' ' END  AS F_PAG,");
                    stbuilder.Append(" RTRIM(c.cuopen) AS CUO_X_PAG,");
                    stbuilder.Append(" RTRIM((select sum(SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros) from cop_copmora D where D.codigoter = A.CODIGOTER and D.lincred = A.LINCRED AND D.NUMERO = A.NUMERO AND D.PERIODO_CONTABLE = C.PERIODO group by D.codigoter,D.lincred,D.numero,D.periodo_contable)) AS TOTAL_VENCIDO ,");
                    stbuilder.Append("  CASE A.cobrojur WHEN 'Y' THEN 'SI' WHEN 'P' THEN 'SI' ELSE 'NO' END AS EN_COBRO,a.filler1 as Pagare,a.empdsto as EMPRESA,");
                    stbuilder.Append(" (Select count(*) as extras from cop_salextras where codigoter = A.CODIGOTER and lincred = A.LINCRED and numero = A.NUMERO and periodo = C.PERIODO and saldo <> 0 )  AS EXTRAS");
                    stbuilder.Append(", CASE a.reEst WHEN 'Y' THEN 'SI' ELSE '' END AS REESTRUC");
                    stbuilder.Append(", (SELECT sum(SAL.VLR_DEBITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS DEBITOS, ");
                    stbuilder.Append(" (SELECT sum(SAL.VLR_CREDITO) FROM COP_SALMAECAR SAL WHERE SAL.codigoter = A.CODIGOTER and SAL.lincred = A.LINCRED AND SAL.NUMERO = A.NUMERO) AS CREDITOS, a.lincred as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "' AND b.estcta = 'Y' AND A.LINCRED >= 1000 AND ( C.SALDO <> 0) ");
                    stbuilder.Append("   UNION ALL ");
                    stbuilder.Append(" select  ' ' ,' ',' ',null,null,null,' ',NULL ,SUM(c.CUOTA) AS CUOTA, SUM(CASE b.versaldo WHEN 'Y' THEN c.saldo ELSE 0 END) AS SALDO,' ',' ',' ',' ', ' ' , ' ',' ',' ',0,' ',0,0,9999 as orden ");
                    stbuilder.Append(" from cop_maecar a left join cop_salmaecar c on c.codigoter = a.codigoter and c.lincred = a.lincred and c.numero = a.numero and c.periodo = '" + Periodo + "' ");
                    stbuilder.Append(" inner join cop_concar12 b on b.lincred = a.lincred ");
                    stbuilder.Append(" where a.codigoter = '" + Codigoter + "'  AND b.estcta = 'Y' AND A.LINCRED >= 1000 AND ( C.SALDO <> 0) ");
                    // NOTE: VB has a stray query appended here (preserved as-is from VB bug)
                    stbuilder.Append("select * from cop_salmaecar where codigoter = '" + Codigoter + "' and periodo=201601");
                    break;
            }

            ds.Tables.Add("Estacuenta");
            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), MyConnect, "CargaGrillaEstaCuenta", ref ds, "Estacuenta");
            return ds.Tables[0];
        }

        #endregion

        #region CargarVentanaGarantia (VB line 16667)

        public object CargarVentanaGarantia(string Codigoter, string Lincred, string Numero, string periodo_cartera, OdbcConnection MyConnect, Form Pertenece, string usuario)
        {
            // FrmGarantias f = new FrmGarantias(MyConnect); // ERROR: CS0246
            try
            {
                // f.codigoter = Strings.Right("0000000000000" + Codigoter, 14); // ERROR: CS0103
                // f.lincred = Lincred; // ERROR: CS0103
                // f.numero = Numero; // ERROR: CS0103
                // f.periodo = Convert.ToInt32(periodo_cartera); // ERROR: CS0103
                // f.usuario = usuario; // ERROR: CS0103
                // f.Owner = Pertenece; // ERROR: CS0103
                // f.Show(); // ERROR: CS0103
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return null;
        }

        #endregion

        #region CargarVentanaCuopen (VB line 16682)

        public object CargarVentanaCuopen(string Codigoter, string Lincred, string Numero, int Periodo,
            OdbcConnection MyConnect, Form Pertenece)
        {
            // cop_fconcuope01 f = new cop_fconcuope01(MyConnect); // ERROR: CS0246
            try
            {
                // f.INICI = true; // ERROR: CS0103
                // f.codigo.Text = Strings.Right("0000000000000" + Codigoter, 14); // ERROR: CS0103
                // f.periodo_cartera.Text = Periodo.ToString(); // ERROR: CS0103
                // f.lincred.Text = Lincred; // ERROR: CS0103
                // f.numero.Text = Numero; // ERROR: CS0103
                // f.Owner = Pertenece; // ERROR: CS0103
                // f.Show(); // ERROR: CS0103
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return null;
        }

        #endregion

        #region CargaGrillaExtras (VB line 17302)

        public DataTable CargaGrillaExtras(OdbcConnection MyConnect, bool SolicituCred, string Codigoter = "",
            string Lincred = "", string Numero = "",
            int Periodo = 0, int NumSolicitud = 0)
        {
            Codigoter = Strings.Right("0000000000000" + Codigoter, 14);
            DataSet ds = new DataSet("Datos");
            if (SolicituCred == false)
            {
                stmysql = "select a.NUM_EXTRA AS NUM ,a.SALDO,SALDO_INICIAL ,b.FECHA_PAGO,case b.forma_pago when '1' then " +
                 "'Nomina' else 'Caja' end as FORMA_PAGO " +
                 "from cop_salextras a inner join cop_extras b on b.codigoter = a.codigoter " +
                 "and b.lincred = a.lincred and b.numero = a.numero and b.num_extra = a.num_extra where  " +
                 " a.codigoter = '" + Codigoter + "' and a.lincred = '" + Lincred +
                 "' and a.numero = '" + Numero + "' and a.periodo = '" + Periodo + "' and a.saldo " +
                 " <> 0 order by a.num_extra asc ";
            }
            else
            {
                stmysql = "select a.NUMERO_EXTRA AS NUM ,b.FECHA,case b.forma_pago when '1' then " +
                  "'Nomina' else 'Caja' end as FORMA_PAGO " +
                  "from COP_EXTRASOLI a " +
                  " WHERE A.NUMERO = '" + NumSolicitud + "' order by a.num_extra asc ";
            }

            ds.Tables.Add("Extras");
            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, MyConnect, "CargaGrillaExtas", ref ds, "Extras");
            return ds.Tables[0];
        }

        #endregion

        #region CargaVentanaExtras (VB line 17327)

        public object CargaVentanaExtras(OdbcConnection MyConnect, bool SolicituCred,
            Form Pertenece, string Codigoter = "",
            string Lincred = "", string Numero = "",
            int Periodo = 0, int NumSolicitud = 0)
        {
            // credi_extras f = new credi_extras(); // ERROR: CS0246
            try
            {
                if (SolicituCred == false)
                {
                    // f.Text = "Extras Credito : " + Lincred + " - " + Numero; // ERROR: CS0103
                }
                // f.DgwExtras.DataSource = CargaGrillaExtras(MyConnect, SolicituCred, // ERROR: CS0103
                    // Codigoter, Lincred, Numero, Periodo, NumSolicitud); // ERROR: CS0103
                // f.Owner = Pertenece; // ERROR: CS0103
                // f.Show(); // ERROR: CS0103
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return null;
        }

        #endregion

        #region CalculaFechaVence (VB line 17462)

        public DateTime CalculaFechaVence(string NumSolicitud, string Couta, DateTime FechaDesc, int Plazo, OdbcConnection myconect, string periodicidad, string ciclodsto)
        {
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos msglidcred = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            DateTime FechaVence;
            int dia = FechaDesc.Day;
            int mes = FechaDesc.Month;
            int diacorrer = 0;
            DateTime fechaCalcular;
            DateTime fechaMesVence;
            int dias_periocidad;
            int diamesVence = DateTime.DaysInMonth(FechaDesc.Year, FechaDesc.Month);
            DateTime fechaFebrero;
            string UltFechaExtr = UltFechaExtra(NumSolicitud, myconect).ToString();

            if (Couta == "0" && Convert.ToDateTime(UltFechaExtr) > new DateTime(1950, 1, 1))
            {
                FechaVence = Convert.ToDateTime(UltFechaExtr);
            }
            else
            {
                if (Plazo != 0)
                {
                    switch (ciclodsto.ToString().Trim())
                    {
                        case "5":
                            // dias_periocidad = msglidcred.DiasPeriodicidad(periodicidad.ToString().Trim()); // ERROR: CS1503
                            break;
                        default:
                            // dias_periocidad = msglidcred.DiasPeriodicidad("1"); // ERROR: CS1503
                            break;
                    }

                    diacorrer = 0;
                    fechaCalcular = FechaDesc.AddMonths(Plazo);
                    switch (fechaCalcular.Month)
                    {
                        case 2:
                            if (fechaCalcular.Day >= 28)
                            {
                                if (DateTime.DaysInMonth(fechaCalcular.Year, fechaCalcular.Month) > 28)
                                {
                                    diacorrer = 1;
                                }
                                else
                                {
                                    diacorrer = 2;
                                }
                            }
                            break;
                        default:
                            if (fechaCalcular.Day > 30)
                            {
                                diacorrer = -1;
                            }
                            break;
                    }
                    FechaVence = fechaCalcular.AddDays(diacorrer);
                    // FechaVence = FechaVence.AddDays(dias_periocidad * -1); // ERROR: CS0165

                    if (FechaVence.Day > 15 && DateTime.DaysInMonth(FechaVence.Year, FechaVence.Month) > 30)
                    {
                        FechaVence = FechaVence.AddDays(-1);
                    }
                    else if (diacorrer == 0 && FechaVence.Month == 2 && FechaVence.Day < 15)
                    {
                        if (DateTime.DaysInMonth(FechaVence.Year, FechaVence.Month) > 28)
                        {
                            FechaVence = FechaVence.AddDays(1);
                        }
                        else
                        {
                            FechaVence = FechaVence.AddDays(2);
                        }
                    }
                }
                else
                {
                    FechaVence = FechaDesc;
                }
            }
            return FechaVence;
        }

        #endregion

    } // end partial class Clscartera
} // end namespace msgcop
