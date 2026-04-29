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
        OdbcCommand mycomqueryconec = new OdbcCommand();
        OdbcDataAdapter MyDataAdater = new OdbcDataAdapter();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        ERP.Core.Compartido.Datos.ClsConect msgodbc = new ERP.Core.Compartido.Datos.ClsConect();
        ERP.Core.Nomina.Services.msgnomconfig msgconfig = new ERP.Core.Nomina.Services.msgnomconfig();
        ERP.Core.Nomina.Reportes.clsmsgimp msgimp = new ERP.Core.Nomina.Reportes.clsmsgimp();
        ERP.Core.CarteraFinanciera.Models.ParamCop msgconfigcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        bool ok;

        public msgnom()
        {
            msgodbc.MyOdbcConect(this.varini);
        }

        public void GrabaAusentismos(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, DateTime fechaInicial, DateTime FechaFinal,
            DateTime fecCauinivac, DateTime fecCaufinvac, int serie, int tipo, int Diag, int claseinc, int prorroga, double Base,
            int HorasAus, OdbcConnection Myconnect, int CptoProrroga = 0, double ConsecProrroga = 0)
        {
            StringBuilder StBuilder = new StringBuilder();

            ok = BuscaAusentismos(IdNomina, IdEmpleado, IdCpto, Consecutivo, Myconnect);
            if (!ok)
            {
                StBuilder.Append("Insert into nom_ausentismos (IdNomina, IdEmpleado,Idcpto,Consecutivo,FecInicial,FecFinal,horas)");
                StBuilder.Append("Values ('");
                StBuilder.Append(IdNomina + "','");
                StBuilder.Append(IdEmpleado + "','");
                StBuilder.Append(IdCpto + "','");
                StBuilder.Append(Consecutivo + "','");
                StBuilder.Append(Strings.Format(fechaInicial, varini.PstForFec) + "','");
                StBuilder.Append(Strings.Format(FechaFinal, varini.PstForFec) + "','");
                StBuilder.Append(HorasAus + "')");
            }
            else
            {
                StBuilder.Append("Update nom_ausentismos set ");
                StBuilder.Append("FecInicial = '");
                StBuilder.Append(Strings.Format(fechaInicial, varini.PstForFec) + "',");
                StBuilder.Append("FecFinal = '");
                StBuilder.Append(Strings.Format(FechaFinal, varini.PstForFec) + "', ");
                StBuilder.Append("horas = '");
                StBuilder.Append(HorasAus + "' ");
                StBuilder.Append("where IdNomina = '");
                StBuilder.Append(IdNomina + "' and IdEmpleado = '");
                StBuilder.Append(IdEmpleado + "' and Idcpto = '");
                StBuilder.Append(IdCpto + "' and Consecutivo = '");
                StBuilder.Append(Consecutivo + "'");
            }

            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "GrabaAusentismos");
            GrabaAusVacaciones(IdNomina, IdEmpleado, IdCpto, Consecutivo, fecCauinivac, fecCaufinvac, Myconnect);
            GrabaAusIncapacidad(IdNomina, IdEmpleado, IdCpto, Consecutivo, serie, tipo, Diag, claseinc, prorroga, Base, Myconnect, CptoProrroga, ConsecProrroga);
        }

        private void GrabaAusIncapacidad(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, int serie, int tipo, int Diag,
            int claseinc, int prorroga, double Base, OdbcConnection myconnect, int CptoProrroga = 0, double ConsecProrroga = 0)
        {
            StringBuilder StBuilder = new StringBuilder();
            StBuilder.Append("Update nom_ausentismos set ");
            StBuilder.Append("serie = '");
            StBuilder.Append(serie + "',");
            StBuilder.Append("tipo = '");
            StBuilder.Append(tipo + "',");
            StBuilder.Append("Diag = '");
            StBuilder.Append(Diag + "',");
            StBuilder.Append("claseinc = '");
            StBuilder.Append(claseinc + "',");
            StBuilder.Append("Base = '");
            StBuilder.Append(Base + "',");
            StBuilder.Append("prorroga = '");
            StBuilder.Append(prorroga + "', ");
            StBuilder.Append("cptoprorroga  = '");
            StBuilder.Append(CptoProrroga + "', ");
            StBuilder.Append("consecprorroga   = '");
            StBuilder.Append(ConsecProrroga + "' ");
            StBuilder.Append("where IdNomina = '");
            StBuilder.Append(IdNomina + "' and IdEmpleado = '");
            StBuilder.Append(IdEmpleado + "' and Idcpto = '");
            StBuilder.Append(IdCpto + "' and Consecutivo = '");
            StBuilder.Append(Consecutivo + "'");
            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaAusVacaciones");
        }

        private void GrabaAusVacaciones(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, DateTime fecCauinivac, DateTime fecCaufinvac, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            StBuilder.Append("Update nom_ausentismos set ");
            StBuilder.Append("fecCauinivac = '");
            StBuilder.Append(Strings.Format(fecCauinivac, varini.PstForFec) + "',");
            StBuilder.Append("fecCaufinvac = '");
            StBuilder.Append(Strings.Format(fecCaufinvac, varini.PstForFec) + "' ");
            StBuilder.Append("where IdNomina = '");
            StBuilder.Append(IdNomina + "' and IdEmpleado = '");
            StBuilder.Append(IdEmpleado + "' and Idcpto = '");
            StBuilder.Append(IdCpto + "' and Consecutivo = '");
            StBuilder.Append(Consecutivo + "'");
            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaAusVacaciones");
        }

        public bool BuscaAusentismos(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, OdbcConnection Myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();

            try
            {
                DsDataset.Tables.Remove("tblnovausent");
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select IdNomina, IdEmpleado,Idcpto,Consecutivo,FecInicial,FecFinal,fecCauinivac,fecCaufinvac,serie,tipo,Diag,claseinc,prorroga,base,horas,cptoprorroga,consecprorroga ");
            stbuilder.Append("from nom_ausentismos where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and Idcpto = '");
            stbuilder.Append(IdCpto + "' and Consecutivo = '");
            stbuilder.Append(Consecutivo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaAusentismos", ref DsDataNovedad, "tblnovausent");
            if (DsDataNovedad.Tables["tblnovausent"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDataNovedad.Tables["tblnovausent"].Copy());
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

        public bool BuscaAusentismos(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, OdbcConnection Myconnect)
        {
            DataSet DsDataset = null;
            return BuscaAusentismos(IdNomina, IdEmpleado, IdCpto, Consecutivo, Myconnect, ref DsDataset);
        }

        public bool BuscaAusentismoTrabajador(int IdNomina, double IdEmpleado, OdbcConnection Myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();

            stbuilder.Append("select IdNomina, IdEmpleado,nomaus.Idcpto,Consecutivo,cptos.nombre,FecInicial,FecFinal,cptos.clase,nomaus.base,cptos.basealq,nomaus.claseinc,nomaus.prorroga,nomaus.horas,nomaus.cptoprorroga,nomaus.consecprorroga ");
            stbuilder.Append("from nom_ausentismos nomaus inner join nom_cptos cptos on nomaus.idcpto = cptos.idcpto where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaAusentismoTrabajador", ref DsDataNovedad, "tblnovausent");
            if (DsDataNovedad.Tables["tblnovausent"].Rows.Count > 0)
            {
                DsDataset = DsDataNovedad;
                return true;
            }
            else
            {
                DsDataset = DsDataNovedad;
                return false;
            }
        }

        public void EliminaAusentismos(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("delete from  nom_ausentismos ");
            stbuilder.Append("where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and Idcpto = '");
            stbuilder.Append(IdCpto + "' and Consecutivo = '");
            stbuilder.Append(Consecutivo + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "EliminaAusentismos");
        }

        public void GrabaLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, string Comprobante, DateTime fecha,
            DateTime Fechadsto, double ValorInicial, int Ciclodsto, decimal TasaInteres, double Cuota, int TipoCuota, int BaseLiquidacion,
            double NumCuotas, double Cargos, double Abonos, int periodo, string usuario, OdbcConnection Myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            double CargosMes = 0, AbonosMes = 0;
            string periodoStr = periodo.ToString();
            DateTime FechaMovto = new DateTime(Convert.ToInt32(periodoStr.Substring(0, 4)), Convert.ToInt32(periodoStr.Substring(4, 2)), 1);

            ok = this.BuscaLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, Myconnect);
            if (!ok)
            {
                StBuilder.Append("Insert into nom_libranzas (");
                StBuilder.Append("IdNomina, IdEmpleado,Idcpto,Consecutivo,idCpte,fecha,Fechadsto,vlrInicial,Ciclodsto,TasaInt,Cuota,TipoCuota,BaseLiq,NumCuotas,usuario,");
                StBuilder.Append("fechasys) ");
                StBuilder.Append("Values ('");
                StBuilder.Append(IdNomina + "','");
                StBuilder.Append(IdEmpleado + "','");
                StBuilder.Append(IdCpto + "','");
                StBuilder.Append(Consecutivo + "','");
                StBuilder.Append(Comprobante + "','");
                StBuilder.Append(Strings.Format(fecha, varini.PstForFec) + "','");
                StBuilder.Append(Strings.Format(Fechadsto, varini.PstForFec) + "','");
                StBuilder.Append(ValorInicial + "','");
                StBuilder.Append(Ciclodsto + "','");
                StBuilder.Append(TasaInteres + "','");
                StBuilder.Append(Cuota + "','");
                StBuilder.Append(TipoCuota + "','");
                StBuilder.Append(BaseLiquidacion + "','");
                StBuilder.Append(NumCuotas + "','");
                StBuilder.Append(usuario + "','");
                StBuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "')");
                this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "GrabaLibranzas");
                GrabaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, fecha, 0, ValorInicial, Myconnect);
            }
            else
            {
                StBuilder.Append("Update nom_libranzas set ");
                StBuilder.Append("idCpte = '");
                StBuilder.Append(Comprobante + "',");
                StBuilder.Append("fecha = '");
                StBuilder.Append(Strings.Format(fecha, varini.PstForFec) + "', ");
                StBuilder.Append("Fechadsto = '");
                StBuilder.Append(Strings.Format(Fechadsto, varini.PstForFec) + "',");
                StBuilder.Append("vlrInicial = '");
                StBuilder.Append(ValorInicial + "',");
                StBuilder.Append("Ciclodsto = '");
                StBuilder.Append(Ciclodsto + "',");
                StBuilder.Append("TasaInt = '");
                StBuilder.Append(TasaInteres + "',");
                StBuilder.Append("Cuota = '");
                StBuilder.Append(Cuota + "',");
                StBuilder.Append("TipoCuota = '");
                StBuilder.Append(TipoCuota + "',");
                StBuilder.Append("BaseLiq = '");
                StBuilder.Append(BaseLiquidacion + "',");
                StBuilder.Append("NumCuotas = '");
                StBuilder.Append(NumCuotas + "' ");
                StBuilder.Append("where IdNomina = '");
                StBuilder.Append(IdNomina + "' and IdEmpleado = '");
                StBuilder.Append(IdEmpleado + "' and Idcpto = '");
                StBuilder.Append(IdCpto + "' and Consecutivo = '");
                StBuilder.Append(Consecutivo + "'");
                this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "GrabaLibranzas");
                double _saldo = 0, _salAnt = 0;
                BuscaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, FechaMovto, Myconnect, ref _saldo, ref _salAnt, ref CargosMes, ref AbonosMes);
                this.GrabaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, FechaMovto, AbonosMes * -1, CargosMes * -1, Myconnect);
                this.GrabaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, FechaMovto, Abonos, Cargos, Myconnect);
            }
        }

        public bool BuscaLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, OdbcConnection Myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();

            stbuilder.Append("select IdNomina, IdEmpleado,Idcpto,Consecutivo,idCpte,fecha,Fechadsto,vlrInicial,Ciclodsto,TasaInt,Cuota,TipoCuota,BaseLiq,NumCuotas, ");
            stbuilder.Append("fecnovedad,usuNov,estado ");
            stbuilder.Append("from nom_libranzas where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and Idcpto = '");
            stbuilder.Append(IdCpto + "' and Consecutivo = '");
            stbuilder.Append(Consecutivo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaLibranzas", ref DsDataNovedad, "tblnovLibranza");
            if (DsDataNovedad.Tables["tblnovLibranza"].Rows.Count > 0)
            {
                DsDataset = DsDataNovedad;
                return true;
            }
            else
            {
                DsDataset = DsDataNovedad;
                return false;
            }
        }

        public bool BuscaLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, OdbcConnection Myconnect)
        {
            DataSet DsDataset = null;
            return BuscaLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, Myconnect, ref DsDataset);
        }

        public bool BuscaLibranzasAsociado(int IdNomina, double IdEmpleado, OdbcConnection Myconnect, ref DataSet DsDataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();

            try
            {
                DsDataset.Tables.Remove("tblnovLibranza");
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select IdNomina, IdEmpleado,Idcpto,Consecutivo,idCpte,fecha,Fechadsto,vlrInicial,Ciclodsto,TasaInt,Cuota,TipoCuota,BaseLiq,estado,NumCuotas ");
            stbuilder.Append("from nom_libranzas ");
            stbuilder.Append(" where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaLibranzas", ref DsDataNovedad, "tblnovLibranza");
            if (DsDataNovedad.Tables["tblnovLibranza"].Rows.Count > 0)
            {
                DsDataset = DsDataNovedad;
                return true;
            }
            else
            {
                DsDataset = DsDataNovedad;
                return false;
            }
        }

        public void EliminaLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, int Periodo, double Saldo, string usuario, OdbcConnection Myconnect)
        {
            string periodoStr = Periodo.ToString();
            DateTime fechamovto = new DateTime(Convert.ToInt32(periodoStr.Substring(0, 4)), Convert.ToInt32(periodoStr.Substring(4, 2)), DateTime.Now.Day);
            StringBuilder stbuilder = new StringBuilder();

            GrabaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, fechamovto, Saldo, 0, Myconnect);

            stbuilder.Append("update nom_libranzas set fecnovedad = '");
            stbuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "',");
            stbuilder.Append("usuNov ='");
            stbuilder.Append(usuario + "',");
            stbuilder.Append("estado ='E' ");
            stbuilder.Append("where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and Idcpto = '");
            stbuilder.Append(IdCpto + "' and Consecutivo = '");
            stbuilder.Append(Consecutivo + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "EliminaLibranzas");
        }

        public void EliminaDstosFijos(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, string usuario, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("update nom_libranzas set fecnovedad = '");
            stbuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "',");
            stbuilder.Append("usuNov ='");
            stbuilder.Append(usuario + "',");
            stbuilder.Append("estado ='E' ");
            stbuilder.Append("where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and Idcpto = '");
            stbuilder.Append(IdCpto + "' and Consecutivo = '");
            stbuilder.Append(Consecutivo + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "EliminaLibranzas");
        }

        public void GrabaSaldoLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, DateTime FechaMoto, double Abono, double Cargo, OdbcConnection Myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            ok = this.BuscaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, FechaMoto, Myconnect);
            if (!ok)
            {
                StBuilder.Append("Insert into nom_sallib (");
                StBuilder.Append("IdNomina, IdEmpleado,Idcpto,Consecutivo,periodo,");
                switch (FechaMoto.Month)
                {
                    case 1:
                        StBuilder.Append("enecargo,eneabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 2:
                        StBuilder.Append("febcargo,febabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 3:
                        StBuilder.Append("marcargo,marabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 4:
                        StBuilder.Append("abrcargo,abrabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 5:
                        StBuilder.Append("maycargo,mayabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 6:
                        StBuilder.Append("juncargo,junabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 7:
                        StBuilder.Append("julcargo,julabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 8:
                        StBuilder.Append("agocargo,agoabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 9:
                        StBuilder.Append("sepcargo,sepabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 10:
                        StBuilder.Append("octcargo,octabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 11:
                        StBuilder.Append("novcargo,novabono) ");
                        StBuilder.Append("Values ('");
                        break;
                    case 12:
                        StBuilder.Append("diccargo,dicabono) ");
                        StBuilder.Append("Values ('");
                        break;
                }

                StBuilder.Append(IdNomina + "','");
                StBuilder.Append(IdEmpleado + "','");
                StBuilder.Append(IdCpto + "','");
                StBuilder.Append(Consecutivo + "','");
                StBuilder.Append(FechaMoto.Year + "','");
                StBuilder.Append(Cargo + "','");
                StBuilder.Append(Abono + "')");
            }
            else
            {
                StBuilder.Append("update nom_sallib set ");
                switch (FechaMoto.Month)
                {
                    case 1:
                        StBuilder.Append("enecargo = enecargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("eneabono = eneabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 2:
                        StBuilder.Append("febcargo = febcargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("febabono = febabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 3:
                        StBuilder.Append("marcargo = marcargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("marabono = marabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 4:
                        StBuilder.Append("abrcargo = abrcargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("abrabono = abrabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 5:
                        StBuilder.Append("maycargo = maycargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("mayabono = mayabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 6:
                        StBuilder.Append("juncargo = juncargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("junabono = junabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 7:
                        StBuilder.Append("julcargo = julcargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("julabono = julabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 8:
                        StBuilder.Append("agocargo = agocargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("agoabono = agoabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 9:
                        StBuilder.Append("sepcargo = sepcargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("sepabono = sepabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 10:
                        StBuilder.Append("octcargo = octcargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("octabono = octabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 11:
                        StBuilder.Append("novcargo = novcargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("novabono = novabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                    case 12:
                        StBuilder.Append("diccargo = diccargo + '");
                        StBuilder.Append(Cargo + "',");
                        StBuilder.Append("dicabono = dicabono + '");
                        StBuilder.Append(Abono + "'");
                        break;
                }
                StBuilder.Append(" where idnomina = '" + IdNomina + "' and ");
                StBuilder.Append("idempleado = '" + IdEmpleado + "' and ");
                StBuilder.Append("idcpto = '" + IdCpto + "' and ");
                StBuilder.Append("consecutivo = '" + Consecutivo + "' and ");
                StBuilder.Append("periodo = '" + FechaMoto.Year + "'");
            }
            this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "GrabaLibranzas");
        }

        private void GrabaSaldoInicialLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, int periodo, double Vlrinicial, OdbcConnection Myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            ok = this.BuscaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, periodo, Myconnect);
            if (!ok)
            {
                Stbuilder.Append("insert into nom_sallib (IdNomina, IdEmpleado,Idcpto,Consecutivo,periodo,vlrinicial)");
                Stbuilder.Append("values ('");
                Stbuilder.Append(IdNomina + "','");
                Stbuilder.Append(IdEmpleado + "','");
                Stbuilder.Append(IdCpto + "','");
                Stbuilder.Append(Consecutivo + "','");
                Stbuilder.Append(periodo + "','");
                Stbuilder.Append(Vlrinicial + "')");
            }
            else
            {
                Stbuilder.Append("update nom_sallib  set ");
                Stbuilder.Append("vlrinicial  = '");
                Stbuilder.Append(Vlrinicial + "' ");
                Stbuilder.Append("where idnomina = '" + IdNomina + "' and IdEmpleado = '" + IdEmpleado + "' and Idcpto = '" + IdCpto + "' and Consecutivo = '" + Consecutivo + "' and periodo = '" + periodo + "'");
            }
            this.msgodbc.ExecuteQueryconec(Stbuilder.ToString(), Myconnect, "GrabaSaldoInicialLIbranzas");
        }

        public virtual bool BuscaSaldoLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, int periodo, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();

            stbuilder.Append("select IdNomina, IdEmpleado,Idcpto,Consecutivo ");
            stbuilder.Append(" from nom_sallib_vw where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and Idcpto = '");
            stbuilder.Append(IdCpto + "' and Consecutivo = '");
            stbuilder.Append(Consecutivo + "' and periodo = '");
            stbuilder.Append(periodo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaSaldoLibranzas", ref DsDataNovedad, "tblSalLib");
            if (DsDataNovedad.Tables["tblSalLib"].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaSaldoLibranzasEmpleado(int IdNomina, double IdEmpleado, int periodo, OdbcConnection Myconnect, DataSet Dsdataset)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();
            string NomMes;
            try
            {
                Dsdataset.Tables.Remove("tblSalLib");
            }
            catch (Exception)
            {
            }

            string periodoStr = periodo.ToString();
            NomMes = "enero"; // this.msgconfig.BuscaNomMesSaldoLib(periodo); // ERROR: CS1061
            // stbuilder.Append("select sallib.IdNomina, sallib.IdEmpleado,sallib.Idcpto,sallib.Consecutivo, sallib." + NomMes + " as saldo,nomlib.cuota,nomlib.fecha,nomlib.Fechadsto,nomlib.vlrInicial,cptos.nomres"); // ERROR: CS0165
            stbuilder.Append(" from nom_sallib_vw sallib inner join nom_libranzas nomlib on  sallib.idnomina = nomlib.idnomina and sallib.idempleado = nomlib.idempleado ");
            stbuilder.Append(" and sallib.idcpto = nomlib.idcpto and sallib.consecutivo = nomlib.consecutivo ");
            stbuilder.Append("inner join nom_cptos cptos on  sallib.idcpto = cptos.idcpto ");
            stbuilder.Append("where sallib.IdNomina = '");
            stbuilder.Append(IdNomina + "' and sallib.IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and sallib.periodo = '");
            stbuilder.Append(periodoStr.Substring(0, 4) + "'");
            stbuilder.Append(" and sallib." + NomMes + " <> 0 and cptos.clase = '8'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaSaldoLibranzasEmpleado", ref Dsdataset, "tblSalLib");
            if (Dsdataset.Tables["tblSalLib"].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaDstosFijosEmpleado(int IdNomina, double IdEmpleado, int periodo, OdbcConnection Myconnect, DataSet Dsdataset, bool Todos = true)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();
            string NomMes;
            try
            {
                Dsdataset.Tables.Remove("tblfijos");
            }
            catch (Exception)
            {
            }

            string periodoStr = periodo.ToString();
            // NomMes = this.msgconfig.BuscaNomMesSaldoLib(periodo); // ERROR: CS1061
            // stbuilder.Append("select sallib.IdNomina, sallib.IdEmpleado,sallib.Idcpto,sallib.Consecutivo, sallib." + NomMes + " as saldo,nomlib.cuota,nomlib.fecha,nomlib.Fechadsto,nomlib.vlrInicial,cptos.nomres"); // ERROR: CS0165
            stbuilder.Append(" from nom_sallib_vw sallib inner join nom_libranzas nomlib on  sallib.idnomina = nomlib.idnomina and sallib.idempleado = nomlib.idempleado ");
            stbuilder.Append(" and sallib.idcpto = nomlib.idcpto and sallib.consecutivo = nomlib.consecutivo ");
            stbuilder.Append("inner join nom_cptos cptos on  sallib.idcpto = cptos.idcpto ");
            stbuilder.Append("where sallib.IdNomina = '");
            stbuilder.Append(IdNomina + "' and sallib.IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and sallib.periodo = '");
            stbuilder.Append(periodoStr.Substring(0, 4) + "'");
            stbuilder.Append(" and cptos.clase = '7' and numCuotas = '9999' ");
            if (!Todos)
            {
                stbuilder.Append(" and nomlib.estado<>'E' ");
            }

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaDstosFijosEmpleado", ref Dsdataset, "tblfijos");
            if (Dsdataset.Tables["tblfijos"].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual bool BuscaSaldoLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, DateTime fecha, OdbcConnection Myconnect, ref double Saldo, ref double SalSAnterior, ref double Cargos, ref double Abonos)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataNovedad = new DataSet();

            try
            {
                DsDataNovedad.Tables.Remove("tblSalLib");
            }
            catch (Exception)
            {
            }
            stbuilder.Append("select IdNomina, IdEmpleado,Idcpto,Consecutivo, ");
            switch (fecha.Month)
            {
                case 1:
                    stbuilder.Append("salene as saldo, vlrinicial as salAnt,enecargo as cargos,eneabono as abonos");
                    break;
                case 2:
                    stbuilder.Append("salfeb as saldo, salene as salAnt,febcargo as cargos,febabono as abonos");
                    break;
                case 3:
                    stbuilder.Append("salmar as saldo, salfeb as salAnt,marcargo as cargos,marabono as abonos");
                    break;
                case 4:
                    stbuilder.Append("salabr as saldo, salmar as salAnt,abrcargo as cargos,abrabono as abonos");
                    break;
                case 5:
                    stbuilder.Append("salmay as saldo,salabr as salAnt,maycargo as cargos,mayabono as abonos");
                    break;
                case 6:
                    stbuilder.Append("saljun as saldo, salmay as salAnt,juncargo as cargos,junabono as abonos");
                    break;
                case 7:
                    stbuilder.Append("saljul as saldo, saljun as salAnt,julcargo as cargos,julabono as abonos");
                    break;
                case 8:
                    stbuilder.Append("salago as saldo, saljul as salAnt,agocargo as cargos,agoabono as abonos");
                    break;
                case 9:
                    stbuilder.Append("salsep as saldo, salago as salAnt,sepcargo as cargos,sepabono as abonos");
                    break;
                case 10:
                    stbuilder.Append("saloct as saldo, salsep as salAnt,octcargo as cargos,octabono as abonos");
                    break;
                case 11:
                    stbuilder.Append("salnov as saldo, saloct as salAnt,novcargo as cargos,novabono as abonos");
                    break;
                case 12:
                    stbuilder.Append("saldic as saldo, salnov as salAnt,diccargo as cargos,dicabono as abonos");
                    break;
            }
            stbuilder.Append(" from nom_sallib_vw  where IdNomina = '");
            stbuilder.Append(IdNomina + "' and IdEmpleado = '");
            stbuilder.Append(IdEmpleado + "' and Idcpto = '");
            stbuilder.Append(IdCpto + "' and Consecutivo = '");
            stbuilder.Append(Consecutivo + "' and periodo = '");
            stbuilder.Append(fecha.Year + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaSaldoLibranzas", ref DsDataNovedad, "tblSalLib");
            if (DsDataNovedad.Tables["tblSalLib"].Rows.Count > 0)
            {
                Saldo = Convert.ToDouble(DsDataNovedad.Tables["tblSalLib"].Rows[0]["saldo"]);
                SalSAnterior = Convert.ToDouble(DsDataNovedad.Tables["tblSalLib"].Rows[0]["salAnt"]);
                Cargos = Convert.ToDouble(DsDataNovedad.Tables["tblSalLib"].Rows[0]["cargos"]);
                Abonos = Convert.ToDouble(DsDataNovedad.Tables["tblSalLib"].Rows[0]["abonos"]);
                return true;
            }
            else
            {
                Saldo = 0;
                SalSAnterior = 0;
                return false;
            }
        }

        public bool BuscaSaldoLibranzas(int IdNomina, double IdEmpleado, int IdCpto, double Consecutivo, DateTime fecha, OdbcConnection Myconnect)
        {
            double _saldo = 0, _salAnt = 0, _cargos = 0, _abonos = 0;
            return BuscaSaldoLibranzas(IdNomina, IdEmpleado, IdCpto, Consecutivo, fecha, Myconnect, ref _saldo, ref _salAnt, ref _cargos, ref _abonos);
        }

        public bool CierraPlanilla(int idPlanilla, int idempresa, string usuario, Form Myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            int fila = 0;
            DateTime Fechamovto;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Cerrando Planilla por favor espere...", Myforma);
            // ok = this.msgconfig.BuscaPeriodosPagos(idPlanilla, idempresa, myconnect, DsDataSet); // ERROR: CS1503
            if (!ok)
            {
                MessageBox.Show("Peridos de pagos no estan creados : " + idPlanilla);
                return false;
            }
            else
            {
                Fechamovto = Convert.ToDateTime(DsDataSet.Tables["tblperpagos"].Rows[0]["Fecinicial"]);
            }

            stbuilder.Append("select nomliq.idnomina,nomliq.idempleado,nomliq.idcpto,nomliq.consecutivo,nomliq.valor ");
            stbuilder.Append("from nom_liqplan nomliq inner join nom_cptos cptos on nomliq.idcpto = cptos.idcpto ");
            stbuilder.Append("where clase = 8 and idnomina = '" + idempresa + "' and idplanilla = '" + idPlanilla + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CierraPlanilla", ref DsDataSet, "tblliqlib");

            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblliqlib"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataSet.Tables["tblliqlib"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblliqlib"].Rows[fila];
                GrabaSaldoLibranzas(idempresa, Convert.ToDouble(row["idempleado"]), Convert.ToInt32(row["idcpto"]), Convert.ToDouble(row["consecutivo"]), Fechamovto, Convert.ToDouble(row["valor"]), 0, myconnect);
                msgbarra.PerformStep();
                fila += 1;
            }

            // this.msgconfig.GrabaEstadoPeriodo(idPlanilla, idempresa, ERP.Core.Nomina.Services.msgnomconfig.EstadoPeriodos.Cerrado, myconnect); // ERROR: CS1061

            msgbarra.Close();
            msgbarra.Dispose();
            return false;
        }
    }
}
