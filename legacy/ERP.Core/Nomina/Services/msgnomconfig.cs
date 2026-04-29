using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using ERP.Core.Compartido.Configuracion;

namespace ERP.Core.Nomina.Services
{
    public class msgnomconfig
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private OdbcDataAdapter MyDataAdater = new OdbcDataAdapter();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.Compartido.Datos.ClsConect msgodbc = new ERP.Core.Compartido.Datos.ClsConect();
        private ParamSys msgsys = new ParamSys();
        private ERP.Core.Compartido.Utilidades.Ayuda msgayusas = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private bool ok;

        public enum EstadoPeriodos : int
        {
            Activo = 0,
            Cerrado = 1
        }

        public void buscaPeriodo(OdbcConnection myconnet, ref DateTime FechaIni, ref DateTime fechaFin, DateTime fechaValidar, ref string estado, ref string Periodo, string año)
        {
            string fechaInicial = "0", fechaFinal = "0", estadoFecha = "A";
            int Anoperiodo = 0;
            string MesPeriodo = "0";
            string Añoactual;
            string stmysql;

            if (año == "9999")
                Añoactual = DateTime.Now.ToString("yyyy");
            else
                Añoactual = año;

            stmysql = "select anio as campo1, periodo as campo2  from sys_periodo where  modulo = 'nomp' and anio = '" + Añoactual + "'";
            object campo1 = Anoperiodo, campo2 = MesPeriodo;
            // ok = this.msgodbc.ExecuteQueryconec(stmysql, myconnet, "buscaPeriodo", ref campo1, ref campo2); // ERROR: CS1503
            Anoperiodo = Convert.ToInt32(campo1);
            MesPeriodo = campo2.ToString();

            if (!ok)
            {
                MessageBox.Show("El periodo de nomina y personal no creado");
                return;
            }

            if (Periodo != "999999" && Periodo != "" && Periodo != "P13")
            {
                if (Periodo.Substring(4, 2) == "13")
                {
                    fechaValidar = DateTime.Parse(Periodo.Substring(0, 4) + "/" + "12" + "/" + "01");
                    Periodo = "P13";
                }
                else
                {
                    fechaValidar = DateTime.Parse(Periodo.Substring(0, 4) + "/" + Periodo.Substring(4, 2) + "/" + "01");
                }
            }

            if (fechaValidar == new DateTime(1950, 1, 1))
            {
                int periodo1 = Convert.ToInt32(MesPeriodo);
                if (periodo1 > 12)
                {
                    periodo1 = 12;
                    Periodo = Anoperiodo.ToString() + ("00" + periodo1.ToString()).Substring(("00" + periodo1.ToString()).Length - 2);
                    fechaValidar = new DateTime(Convert.ToInt32(Periodo.Substring(0, 4)), Convert.ToInt32(Periodo.Substring(4, 2)), 1);
                }
                if (MesPeriodo == "13")
                {
                    Periodo = "P13";
                }
                else
                {
                    Periodo = Anoperiodo.ToString() + ("00" + periodo1.ToString()).Substring(("00" + periodo1.ToString()).Length - 2);
                    fechaValidar = new DateTime(Convert.ToInt32(Periodo.Substring(0, 4)), Convert.ToInt32(Periodo.Substring(4, 2)), 1);
                }
            }

            switch (fechaValidar.ToString("MM"))
            {
                case "01": fechaInicial = "fecha_ini01"; fechaFinal = "fecha_fin01"; estadoFecha = "estado_01"; break;
                case "02": fechaInicial = "fecha_ini02"; fechaFinal = "fecha_fin02"; estadoFecha = "estado_02"; break;
                case "03": fechaInicial = "fecha_ini03"; fechaFinal = "fecha_fin03"; estadoFecha = "estado_03"; break;
                case "04": fechaInicial = "fecha_ini04"; fechaFinal = "fecha_fin04"; estadoFecha = "estado_04"; break;
                case "05": fechaInicial = "fecha_ini05"; fechaFinal = "fecha_fin05"; estadoFecha = "estado_05"; break;
                case "06": fechaInicial = "fecha_ini06"; fechaFinal = "fecha_fin06"; estadoFecha = "estado_06"; break;
                case "07": fechaInicial = "fecha_ini07"; fechaFinal = "fecha_fin07"; estadoFecha = "estado_07"; break;
                case "08": fechaInicial = "fecha_ini08"; fechaFinal = "fecha_fin08"; estadoFecha = "estado_08"; break;
                case "09": fechaInicial = "fecha_ini09"; fechaFinal = "fecha_fin09"; estadoFecha = "estado_09"; break;
                case "10": fechaInicial = "fecha_ini10"; fechaFinal = "fecha_fin10"; estadoFecha = "estado_10"; break;
                case "11": fechaInicial = "fecha_ini11"; fechaFinal = "fecha_fin11"; estadoFecha = "estado_11"; break;
                case "12": fechaInicial = "fecha_ini12"; fechaFinal = "fecha_fin12"; estadoFecha = "estado_12"; break;
                case "13": fechaInicial = "fecha_ini13"; fechaFinal = "fecha_fin13"; estadoFecha = "estado_13"; break;
            }

            if (fechaValidar != new DateTime(1950, 1, 1))
            {
                if (Periodo == "P13")
                {
                    Periodo = fechaValidar.ToString("yyyy") + "13";
                    fechaInicial = "fecha_ini13";
                    fechaFinal = "fecha_fin13";
                    estadoFecha = "estado_13";
                }
                else
                {
                    Periodo = fechaValidar.ToString("yyyyMM");
                }

                stmysql = "select " + fechaInicial + " as campo1," + fechaFinal + " as campo2," + estadoFecha + " as campo3 from sys_periodo where  modulo = 'nomp' and anio = '" + Añoactual + "'";
                object c1 = FechaIni, c2 = fechaFin, c3 = estado;
                // this.msgodbc.ExecuteQueryconec(stmysql, myconnet, "buscaPeriodo", ref c1, ref c2, ref c3); // ERROR: CS1501
                FechaIni = Convert.ToDateTime(c1);
                fechaFin = Convert.ToDateTime(c2);
                estado = c3.ToString();
            }
        }

        public void buscaPeriodo(OdbcConnection myconnet)
        {
            DateTime FechaIni = new DateTime(1950, 1, 1);
            DateTime fechaFin = new DateTime(1950, 1, 1);
            DateTime fechaValidar = new DateTime(1950, 1, 1);
            string estado = "C";
            string Periodo = "999999";
            string año = "9999";
            buscaPeriodo(myconnet, ref FechaIni, ref fechaFin, fechaValidar, ref estado, ref Periodo, año);
        }

        public void GrabaCptos(string Idcptos, string Nombre, string Nomres, int clase, int natur, double valor, decimal factor, int baseVal,
            int Salario, string cdias, int extie, int exval, int basealq, double Saltope, string Lineacer, string Columna,
            int Afeprest, int AfeRetFte, int Esprest, int Msaldo, string Prior, decimal tasaprov, int BaseProv, string nit,
            int baseces, int basepri, int basevac, int baseind, string IdAdministradora, string IdAuto, OdbcConnection myconnect,
            int consal = 0, int clasecpto = 0)
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = BuscaCptos(Idcptos, myconnect);
            if (!ok)
            {
                stbuilder.Append("Insert into nom_cptos (idcpto,nombre,nomres,clase,natur,valor,factor,base,salario,cdias,extie,exval,basealq,saltope,lineacer,columna,");
                stbuilder.Append("afeprest,AFERETFTE,ESPREST,MSALDO,tasaprov,BaseProv,nit,priori,baseces,basepri,basevac,IdAdmin,baseind,IdAuto,consal,clasecpto) values ('");
                stbuilder.Append(Idcptos + "','");
                stbuilder.Append(Nombre + "','");
                stbuilder.Append(Nomres + "','");
                stbuilder.Append(clase + "','");
                stbuilder.Append(natur + "','");
                stbuilder.Append(valor + "','");
                stbuilder.Append(factor + "','");
                stbuilder.Append(baseVal + "','");
                stbuilder.Append(Salario + "','");
                stbuilder.Append(cdias + "','");
                stbuilder.Append(extie + "','");
                stbuilder.Append(exval + "','");
                stbuilder.Append(basealq + "','");
                stbuilder.Append(Saltope + "','");
                stbuilder.Append(Lineacer + "','");
                stbuilder.Append(Columna + "','");
                stbuilder.Append(Afeprest + "','");
                stbuilder.Append(AfeRetFte + "','");
                stbuilder.Append(Esprest + "','");
                stbuilder.Append(Msaldo + "','");
                stbuilder.Append(tasaprov + "','");
                stbuilder.Append(BaseProv + "','");
                stbuilder.Append(nit + "','");
                stbuilder.Append(Prior + "','");
                stbuilder.Append(baseces + "','");
                stbuilder.Append(basepri + "','");
                stbuilder.Append(basevac + "','");
                stbuilder.Append(IdAdministradora + "','");
                stbuilder.Append(baseind + "','");
                stbuilder.Append(IdAuto + "',");
                stbuilder.Append(consal + ",");
                stbuilder.Append(clasecpto + ")");
            }
            else
            {
                stbuilder.Append("update nom_cptos set ");
                stbuilder.Append("nombre ='" + Nombre + "',");
                stbuilder.Append("nomres ='" + Nomres + "',");
                stbuilder.Append("clase ='" + clase + "',");
                stbuilder.Append("natur ='" + natur + "',");
                stbuilder.Append("valor ='" + valor + "',");
                stbuilder.Append("factor ='" + factor + "',");
                stbuilder.Append("base ='" + baseVal + "',");
                stbuilder.Append("salario ='" + Salario + "',");
                stbuilder.Append("cdias ='" + cdias + "',");
                stbuilder.Append("extie ='" + extie + "',");
                stbuilder.Append("exval ='" + exval + "',");
                stbuilder.Append("basealq ='" + basealq + "',");
                stbuilder.Append("Saltope ='" + Saltope + "',");
                stbuilder.Append("Lineacer ='" + Lineacer + "',");
                stbuilder.Append("Columna ='" + Columna + "',");
                stbuilder.Append("Afeprest ='" + Afeprest + "',");
                stbuilder.Append("AfeRetFte ='" + AfeRetFte + "',");
                stbuilder.Append("Esprest ='" + Esprest + "',");
                stbuilder.Append("Msaldo ='" + Msaldo + "',");
                stbuilder.Append("tasaprov ='" + tasaprov + "',");
                stbuilder.Append("BaseProv ='" + BaseProv + "',");
                stbuilder.Append("nit ='" + nit + "',");
                stbuilder.Append("baseces ='" + baseces + "',");
                stbuilder.Append("basepri ='" + basepri + "',");
                stbuilder.Append("basevac ='" + basevac + "',");
                stbuilder.Append("baseind ='" + baseind + "',");
                stbuilder.Append("IdAdmin ='" + IdAdministradora + "',");
                stbuilder.Append("IdAuto ='" + IdAuto + "',");
                stbuilder.Append("priori ='" + Prior + "',");
                stbuilder.Append("consal =" + consal + ", ");
                stbuilder.Append("clasecpto=" + clasecpto + " ");
                stbuilder.Append(" where idcpto = '" + Idcptos + "'");
            }
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaCptos");
        }

        public bool BuscaCptos(string Idcptos, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataCptos = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblcptos"); } catch { }

            stbuilder.Append("select idcpto,nombre,nomres,clase,natur,valor,factor,base,salario,cdias,extie,exval,basealq,saltope,lineacer,columna,");
            stbuilder.Append("afeprest,AFERETFTE,ESPREST,MSALDO,PRIORI,tasaprov,BaseProv,nit,consal,exuni,baseadm,cptorel,cptopag,tasaiva,equiv,tasamen,tasamay,");
            stbuilder.Append("baseces,basepri,basevac,baseind,IdAdmin,idauto,clasecpto ");
            stbuilder.Append("from nom_cptos where idcpto = '" + Idcptos + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaCptos", DsDataCptos, "tblcptos");
            if (DsDataCptos.Tables["tblcptos"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataCptos.Tables["tblcptos"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaCptos(string Idcptos, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_cptos where idcpto = '" + Idcptos + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaCptos");
        }

        public string HelpCptos(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_cptos", "idcpto", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public string HelpEmpleados(OdbcConnection Mycnnect, Form Myforma)
        {
            string IdCodigo = msgayusas.CargaAyuda("nom_empleados", "IdEmpleado", "Apellidos", "Nombres", Mycnnect, Myforma, "Apellido", "Nombre");
            return IdCodigo;
        }

        public virtual bool BuscaEmpleado(string idnomina, string IdEmpleado, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataEmpleados = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblempleados"); } catch { }

            stbuilder.Append("select idempleado,idnomina,apellidos,nombres,Cedula,Expedida,Clase,Sexo,LibMilitar,DisMilitar,LicCon,CarLic,Direccion,IdCiudad,Telefono,Movil,email,");
            stbuilder.Append("GrupSang,FactorRh,Idbanco,ClaCta,NumCta,NivelAca,ClaNom,FormaPago,Area,IdSeccion,Idcargo,Salario,ClaseSalario,FECHAEFE,ClauxTra,Contrato,FecVence,Tallaoverol,");
            stbuilder.Append("CicloPtra,TasaRetFte,CicloApo,FecRetiro,MotRet,FecReingreso,FecNace,TallaPan,TallaCa,TallaZap,TallaCasco,GastRep,PrimaTec,OtraPro,IdEps,Idpension,IdArp,estado,");
            stbuilder.Append("Idcesantias,IdSena,IdIcbf,IdSubsFam,FecCausaCesan,DiasPrima,DisVac,Indem,PromCes,PromPri,PromeInde,PromeVac,DiasFe,AfiFondo,idcencos,Fecing,fecvenlic,feccauvac,");
            stbuilder.Append("feccaupri,TipEmpleado,IdtarifaArp,regespecial,diascesan,diasindem,Liquidado,FecLiquidado,primextra,vlrdeduciblertefte,cicloretfte,tipproretfte ");
            stbuilder.Append("from nom_empleados where idempleado = '" + IdEmpleado + "' and idnomina = '" + idnomina + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaCptos", DsDataEmpleados, "tblempleados");
            if (DsDataEmpleados.Tables["tblempleados"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataEmpleados.Tables["tblempleados"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual bool BuscaEmpleado(int idnomini, OdbcConnection myconnect, string idnomfin, ref DataSet DsDataset, string CenCosto = "Todos")
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataEmpleados = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblempleados"); } catch { }

            stbuilder.Append("select idempleado,idnomina,apellidos,nombres,Cedula,Expedida,Clase,Sexo,LibMilitar,DisMilitar,LicCon,CarLic,Direccion,IdCiudad,Telefono,Movil,email,");
            stbuilder.Append("GrupSang,FactorRh,Idbanco,ClaCta,NumCta,NivelAca,ClaNom,FormaPago,Area,IdSeccion,Idcargo,Salario,ClaseSalario,FECHAEFE,ClauxTra,Contrato,FecVence,Tallaoverol,");
            stbuilder.Append("CicloPtra,TasaRetFte,CicloApo,FecRetiro,MotRet,FecReingreso,FecNace,TallaPan,TallaCa,TallaZap,TallaCasco,GastRep,PrimaTec,OtraPro,IdEps,Idpension,IdArp,estado,");
            stbuilder.Append("Idcesantias,IdSena,IdIcbf,IdSubsFam,FecCausaCesan,DiasPrima,DisVac,Indem,PromCes,PromPri,PromeInde,PromeVac,DiasFe,AfiFondo,idcencos,Fecing,fecvenlic,feccauvac,");
            stbuilder.Append("feccaupri,TipEmpleado,IdtarifaArp,regespecial,diascesan,diasindem,Liquidado,FecLiquidado,primextra,vlrdeduciblertefte,cicloretfte,tipproretfte ");
            stbuilder.Append("from nom_empleados where idnomina between '" + idnomini + "' and '" + idnomfin + "'");
            double d;
            if (double.TryParse(CenCosto, out d))
            {
                stbuilder.Append(" and idcencos='" + CenCosto.Trim() + "'");
            }

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaEmpleado", DsDataEmpleados, "tblempleados");
            if (DsDataEmpleados.Tables["tblempleados"].Rows.Count > 0)
            {
                try { DsDataset.Tables.Add(DsDataEmpleados.Tables["tblempleados"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscaEmpleadoCedula(string IdCedula, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataEmpleados = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblempleados"); } catch { }

            stbuilder.Append("select idempleado,idnomina,apellidos,nombres,Cedula,Expedida,Clase,Sexo,LibMilitar,DisMilitar,LicCon,CarLic,Direccion,IdCiudad,Telefono,Movil,email,");
            stbuilder.Append("GrupSang,FactorRh,Idbanco,ClaCta,NumCta,NivelAca,ClaNom,FormaPago,Area,IdSeccion,Idcargo,Salario,ClaseSalario,FECHAEFE,ClauxTra,Contrato,FecVence,Tallaoverol,");
            stbuilder.Append("CicloPtra,TasaRetFte,CicloApo,FecRetiro,MotRet,FecReingreso,FecNace,TallaPan,TallaCa,TallaZap,TallaCasco,GastRep,PrimaTec,OtraPro,IdEps,Idpension,IdArp,estado,");
            stbuilder.Append("Idcesantias,IdSena,IdIcbf,IdSubsFam,FecCausaCesan,DiasPrima,DisVac,Indem,PromCes,PromPri,PromeInde,PromeVac,DiasFe,AfiFondo,idcencos,Fecing,fecvenlic,feccauvac,feccaupri,TipEmpleado,IdtarifaArp,regespecial,diascesan,diasindem,Liquidado,FecLiquidado,primextra,vlrdeduciblertefte,cicloretfte,tipproretfte ");
            stbuilder.Append("from nom_empleados where Cedula = '" + IdCedula + "' and estado < 2 ");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaCptos", DsDataEmpleados, "tblempleados");
            if (DsDataEmpleados.Tables["tblempleados"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataEmpleados.Tables["tblempleados"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void RetiraEmpleado(int idnomina, double idempleado, DateTime FecRetiro, int Motret, string usuario, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("update nom_empleados set FecRetiro ='");
            stbuilder.Append(FecRetiro.ToString(varini.PstForFec) + "',");
            stbuilder.Append("estado = '2',");
            stbuilder.Append("MotRet = '");
            stbuilder.Append(Motret + "' ");
            stbuilder.Append(" where idempleado = '" + idempleado + "' and idnomina = '" + idnomina + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "RetiraEmpleado");
        }

        public void ReIngresaEmpleado(int idnomina, double idempleado, DateTime FecReingreso, string usuario, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("update nom_empleados set fecreingreso ='");
            stbuilder.Append(FecReingreso.ToString(varini.PstForFec) + "',");
            stbuilder.Append("estado = '1' ");
            stbuilder.Append(" where idempleado = '" + idempleado + "' and idnomina = '" + idnomina + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "RetiraEmpleado");
        }

        public int CalculaDias(DateTime Fecinicial, DateTime FechaFinal)
        {
            int Dias;
            DateTime FecIni = DateTime.Parse(Fecinicial.ToString("yyyy/MM/dd"));
            DateTime FecFin = DateTime.Parse(FechaFinal.ToString("yyyy/MM/dd"));

            int DiaIni = FecIni.Day;
            int DiaFin = FecFin.Day;

            if (DiaIni > 30) DiaIni = 30;
            if (DiaFin > 30) DiaFin = 30;

            Dias = ((FecIni.Year - FecFin.Year) * 360) + ((FecIni.Month - FecFin.Month) * 30) + (DiaIni - DiaFin) + 1;
            return Dias;
        }

        public bool BuscaEmpresa(int IdEmpresa, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataEmpresa = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblempresas"); } catch { }

            stbuilder.Append("select IdEmpresa,Nombre,NomRes,Nit,TasaArp,TipoCuenta,TipoEmpresa,CtaOrgFondos,ValFijoProv,ValSalMinimo,");
            stbuilder.Append("IdSalBasico,IdTrasporte,IdCesantias,IdIntCesantias,IdPrimaServ,IdVacasiones,IdSalIntegral,IdFdoSolid,IdIndem,");
            stbuilder.Append("IdApoSoc,IdApoSoc1,IdApoSoc2,IdAdmArp,IdAprSena,IdAntCesan,IdRetfte,ForLiqAdmon,IdSena,");
            stbuilder.Append("IdIcbf,IdRecNotur,IdAusVaca,IdPrimaServ1,IdPrimaServ2,IdPrimaServ3,IdVacaConsol,IdDescTrasp,");
            stbuilder.Append("IdMaxDed,IncluProv,EmiteFact,IdCompAnual,IdCompSem,IdCompDesc,TrasTer,IdCpteCont,IdCntrprtida,ForActCont,SalarioBase,");
            stbuilder.Append("fdosolmay1,fdotasamay1,fdosolmay2,fdotasamay2,fdosolmay3,fdotasamay3,fdosolmay4,fdotasamay4,fdosolmay5,fdotasamay5, ");
            stbuilder.Append("diastope,cptoincapcxc,periodoprueba,idvacconsol,factorincap,vlruvt,porcrenta,porcdeduc ");
            stbuilder.Append("from nom_empresas where IdEmpresa = '" + IdEmpresa + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaEmpresa", DsDataEmpresa, "tblempresas");
            if (DsDataEmpresa.Tables["tblempresas"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataEmpresa.Tables["tblempresas"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaEmpresa(string IdEmpresa, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_empresas where IdEmpresa = '" + IdEmpresa + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaEmpresa");
        }

        public string HelpEmpresa(OdbcConnection Mycnnect, Form Myforma)
        {
            string idEmpresa = msgayusas.CargaAyuda("nom_empresas", "idempresa", "nombre", "nomres", Mycnnect, Myforma);
            return idEmpresa;
        }

        public virtual bool BuscaPeriodosPagos(int IdPlanilla, string IdEmpresa, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataPerPagos = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblperpagos"); } catch { }

            stbuilder.Append("select IdPlanilla,IdEmpresa,Detalle,FechaPago,IdEmpLiq,CicloMes,HoraCiclo,Fecinicial,FechaFinal,Periodicidad,CptoAdi1,");
            stbuilder.Append("CptoAdi2,CptoAdi3,CptoAdi4,SoloNov,NoliqSalAut,NoLiqAusen,NoLiqLib,estado,mensaje,idperiodo,LiqAnt,CruceAnt ");
            stbuilder.Append("from nom_perpagos where IdPlanilla = '" + IdPlanilla + "' and IdEmpresa = '" + IdEmpresa + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaEmpresa", DsDataPerPagos, "tblperpagos");
            if (DsDataPerPagos.Tables["tblperpagos"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataPerPagos.Tables["tblperpagos"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public string HelpPeriodosPagos(OdbcConnection Mycnnect, Form Myforma)
        {
            string Idplanilla = msgayusas.CargaAyuda("nom_perpagos", "IdPlanilla", "Detalle", " ", Mycnnect, Myforma);
            return Idplanilla;
        }

        public DataTable CargaFiltro(Form myforma, ref bool cancelar)
        {
            // frmfiltros Myfiltro = new frmfiltros(); // ERROR: CS0246
            // Myfiltro.ShowDialog(myforma); // ERROR: CS0103
            // cancelar = Myfiltro.cancelar; // ERROR: CS0103
            // return Myfiltro.DataSetwhere.Tables["tblwhere"]; // ERROR: CS0103
            return new DataTable();
        }

        public virtual bool BuscaPeriodosPagos(OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataPerPagos = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblperpagos"); } catch { }

            stbuilder.Append("select IdPlanilla,IdEmpresa,Detalle,FechaPago,IdEmpLiq,CicloMes,HoraCiclo,Fecinicial,FechaFinal,Periodicidad,CptoAdi1,");
            stbuilder.Append("CptoAdi2,CptoAdi3,CptoAdi4,SoloNov,NoliqSalAut,NoLiqAusen,NoLiqLib,estado,mensaje,LiqAnt,CruceAnt ");
            stbuilder.Append("from nom_perpagos ");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaEmpresa", DsDataPerPagos, "tblperpagos");
            if (DsDataPerPagos.Tables["tblperpagos"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataPerPagos.Tables["tblperpagos"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaPeriodosPagos(int IdPlanilla, string IdEmpresa, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_perpagos where IdPlanilla = '" + IdPlanilla + "' and IdEmpresa = '" + IdEmpresa + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaPeriodosPagos");
        }

        public bool BuscaCentroCosto(string IdCencos, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataPerPagos = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblcencos"); } catch { }

            stbuilder.Append("select idcencos,nombre,nomres ");
            stbuilder.Append("from nom_cencos where Idcencos = '" + IdCencos + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaCentroCosto", DsDataPerPagos, "tblcencos");
            if (DsDataPerPagos.Tables["tblcencos"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataPerPagos.Tables["tblcencos"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaCentroCostos(string IdCencos, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_cencos where Idcencos = '" + IdCencos + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaCentroCostos");
        }

        public string HelpCentroCostos(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_cencos", "idcencos", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public bool BuscaCuentasContables(int Idcpto, string idcencos, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataPerPagos = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblcuentas"); } catch { }

            stbuilder.Append("select idcpto,idcencos,ctagasto,ctacontra,ctaprov ");
            stbuilder.Append("from nom_cuentas where idcpto = '" + Idcpto + "' and idcencos = '" + idcencos + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaCuentasContables", DsDataPerPagos, "tblcuentas");
            if (DsDataPerPagos.Tables["tblcuentas"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataPerPagos.Tables["tblcuentas"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaCuentasContables(int Idcpto, string idcencos, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_cuentas where idcpto = '" + Idcpto + "' and idcencos = '" + idcencos + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaCuentasContables");
        }

        public bool BuscaCausalesRetiro(int IdCodigo, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataPerPagos = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblcauret"); } catch { }

            stbuilder.Append("select idcodigo,nombre,nomres,Indem,DstosAuto ");
            stbuilder.Append("from nom_cauret where idcodigo = '" + IdCodigo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaCausalesRetiro", DsDataPerPagos, "tblcauret");
            if (DsDataPerPagos.Tables["tblcauret"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataPerPagos.Tables["tblcauret"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaCausalesRetiro(int IdCodigo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_cauret where idcodigo = '" + IdCodigo + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaCausalesRetiro");
        }

        public string HelpCusalesRetiro(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_cauret", "idcodigo", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public bool BuscaEps(int IdCodigo, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataEps = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tbleps"); } catch { }

            stbuilder.Append("select idcodigo,nombre,nomres,nit,dv ");
            stbuilder.Append("from nom_eps where idcodigo = '" + IdCodigo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaEps", DsDataEps, "tbleps");
            if (DsDataEps.Tables["tbleps"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsDataEps.Tables["tbleps"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaEps(int IdCodigo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_eps where idcodigo = '" + IdCodigo + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaEps");
        }

        public string HelpEps(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_eps", "idcodigo", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public bool BuscaAdmCesantias(int IdCodigo, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsData = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblcesantias"); } catch { }

            stbuilder.Append("select idcodigo,nombre,nomres,nit,dv ");
            stbuilder.Append("from nom_cesantias where idcodigo = '" + IdCodigo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAdmCesantias", DsData, "tblcesantias");
            if (DsData.Tables["tblcesantias"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tblcesantias"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaAdmCesantias(int IdCodigo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_cesantias where idcodigo = '" + IdCodigo + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaAdmCesantias");
        }

        public string HelpAdmCesantias(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_cesantias", "idcodigo", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public bool BuscaAdmArp(int IdCodigo, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsData = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblarp"); } catch { }

            stbuilder.Append("select idcodigo,nombre,nomres,nit,dv ");
            stbuilder.Append("from nom_arp where idcodigo = '" + IdCodigo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAdmArp", DsData, "tblarp");
            if (DsData.Tables["tblarp"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tblarp"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaAdmArp(int IdCodigo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_arp where idcodigo = '" + IdCodigo + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaAdmCesantias");
        }

        public string HelpAdmarp(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_arp", "idcodigo", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public bool BuscaAdmPensiones(int IdCodigo, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsData = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblpensiones"); } catch { }

            stbuilder.Append("select idcodigo,nombre,nomres,nit,dv ");
            stbuilder.Append("from nom_pensiones where idcodigo = '" + IdCodigo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAdmPensiones", DsData, "tblpensiones");
            if (DsData.Tables["tblpensiones"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tblpensiones"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaAdmPensiones(int IdCodigo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_pensiones where idcodigo = '" + IdCodigo + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaAdmPensiones");
        }

        public string HelpAdmPensiones(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_pensiones", "idcodigo", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public bool BuscaParentescos(ref string idparent, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsData = new DataSet();
            idparent = ("0000" + idparent.Trim()).Substring(("0000" + idparent.Trim()).Length - 4);

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblparent"); } catch { }

            stbuilder.Append("select codigo,nombre,nomres ");
            stbuilder.Append("from sys_parent51 where codigo = '" + idparent + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaParentescos", DsData, "tblparent");
            if (DsData.Tables["tblparent"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tblparent"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminaParentescos(int idparent, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from nom_parent where idparent = '" + idparent + "'");
            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaParentescos");
        }

        public string HelpParentescos(OdbcConnection Mycnnect, Form Myforma)
        {
            string idcpto = msgayusas.CargaAyuda("nom_parent", "idparent", "nombre", "nomres", Mycnnect, Myforma);
            return idcpto;
        }

        public bool BuscaParAutLiqAportes(int IdCodigo, OdbcConnection myconnect, DataSet DsDataset = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsData = new DataSet();

            try { if (DsDataset != null) DsDataset.Tables.Remove("tblparautApor"); } catch { }

            stbuilder.Append("select IdCodigo,tipoIden,NumIdent,Dv,Nombre,Direccion,Telefono,Fax,IdMunicipio,DesMunicipio,IdDepart,DesDepartamento,Salud,Pension,Arp,");
            stbuilder.Append("Ccf,Sena,Icbf,FdoSol,Esap,MinEdu,AdmCcf,AdmArp,TasaMora,TipVin,TipoApor,CobSalud,BaseCot,NumPatronal,SMLV,RegPro,NumFor,FecCorrecion,claseApo,NatJur,IdActEco,email,idreprelegal,Dvrep,PrimerApellido,SegundoApellido,PrimerNombre,SegundoNombre,forpresenta ");
            stbuilder.Append("from nom_parautapo where idcodigo = '" + IdCodigo + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaParAutLiqAportes", DsData, "tblparautApor");
            if (DsData.Tables["tblparautApor"].Rows.Count > 0)
            {
                try { if (DsDataset != null) DsDataset.Tables.Add(DsData.Tables["tblparautApor"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool BuscarParametrosRetencion(int idempresa, OdbcConnection myconnect, DataSet DsDtaset = null)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            try { if (DsDtaset != null) DsDtaset.Tables.Remove("tblparretfte"); } catch { }

            StBuilder.Append("select idempresa, uvtinicial, uvtfinal, tarifa, uvtadicional ");
            StBuilder.Append("from nom_parretfte ");
            StBuilder.Append("where idempresa=" + idempresa);
            StBuilder.Append(" order by idempresa, uvtinicial, uvtfinal ");

            ok = this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscarParametrosRetencion", dsdata, "tblparretfte");

            if (ok)
            {
                try { if (DsDtaset != null) DsDtaset.Tables.Add(dsdata.Tables["tblparretfte"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual bool BuscarParametrosRetencion(int idempresa, int Unidaduvt, OdbcConnection myconnect, DataSet DsDtaset = null)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();

            try { if (DsDtaset != null) DsDtaset.Tables.Remove("tblparretfte"); } catch { }

            StBuilder.Append("select idempresa, uvtinicial, uvtfinal, tarifa, uvtadicional ");
            StBuilder.Append("from nom_parretfte ");
            StBuilder.Append("where idempresa=" + idempresa + " and " + Unidaduvt + ">uvtinicial and " + Unidaduvt + "<=uvtfinal");
            StBuilder.Append(" order by idempresa, uvtinicial, uvtfinal ");

            ok = this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscarParametrosRetencion", dsdata, "tblparretfte");

            if (ok)
            {
                try { if (DsDtaset != null) DsDtaset.Tables.Add(dsdata.Tables["tblparretfte"].Copy()); } catch { }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void EliminarParametrosRetencion(int Idempresa, OdbcConnection myconnect)
        {
            string StMysql = "delete from nom_parretfte where idempresa=" + Idempresa;
            this.msgodbc.ExecuteQueryconec(StMysql, myconnect, "EliminarParametrosRetencion");
        }

        public bool GrabarParametrosRetencion(int idempresa, int uvtinicial, int uvtfinal, double tarifa, int uvtadicional, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("insert into nom_parretfte (idempresa, uvtinicial, uvtfinal, tarifa, uvtadicional) values('");
            StBuilder.Append(idempresa + "','");
            StBuilder.Append(uvtinicial + "','");
            StBuilder.Append(uvtfinal + "','");
            StBuilder.Append(tarifa + "','");
            StBuilder.Append(uvtadicional + "')");

            ok = this.msgodbc.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabarParametrosRetencion");
            return ok;
        }

        public double BuscarNovedadesSalarioEmpleadoAnt(int idnomina, double idempleado, DateTime fecha, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            double salario = 0;

            StBuilder.Append("select emp.idnomina, emp.idempleado, emp.salario, nov.salario as salarionovedad ");
            StBuilder.Append("from nom_empleados emp ");
            StBuilder.Append("left join nom_novsalario nov on emp.idnomina=nov.idnomina and emp.idempleado=nov.idempleado and nov.fecha<='" + fecha.ToString(varini.PstForFec) + "' ");
            StBuilder.Append("where emp.idnomina=" + idnomina + " and emp.idempleado=" + idempleado + " ");
            StBuilder.Append(" order by emp.idnomina, emp.idempleado, nov.fecha desc ");

            ok = this.msgodbc.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscarNovedadesSalarioEmpleado", dsdata, "tblnovedad");

            if (ok)
            {
                DataRow row = dsdata.Tables["tblnovedad"].Rows[0];
                if (row["salarionovedad"] != DBNull.Value)
                {
                    salario = Convert.ToDouble(row["salarionovedad"]);
                }
                else
                {
                    salario = Convert.ToDouble(row["salario"]);
                }
                return salario;
            }
            else
            {
                return salario;
            }
        }

        public msgnomconfig()
        {
            msgodbc.MyOdbcConect(varini);
        }
    }
}
