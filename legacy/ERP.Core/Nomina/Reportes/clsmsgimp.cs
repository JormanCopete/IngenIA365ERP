using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace ERP.Core.Nomina.Reportes
{
    // Traducción de: Public Class clsmsgimp (clsmsgimp.vb)
    public class clsmsgimp
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private OdbcDataAdapter MyDataAdater = new OdbcDataAdapter();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.Compartido.Datos.ClsConect msgodbc = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Nomina.Services.msgnomconfig msgnompar = new ERP.Core.Nomina.Services.msgnomconfig();
        private bool ok;

        public clsmsgimp()
        {
            msgodbc.MyOdbcConect(ref varini);
        }

        // Helper: BuscarCompania extrayendo p15=nit, p16=dir, p26=nombre, p27=tel
        private void BuscarCompaniaHelper(OdbcConnection myconnect,
            ref string nit, ref string dir, ref string nombre, ref string tel)
        {
            string _u3="",_u4="",_u5="",_u6="";
            int _u7=0; decimal _u8=0m; int _u9=0; decimal _u10=0m;
            int _u11=0,_u12=0,_u13=0;
            string _u14="",_u17="",_u18="",_u19="",_u20="",_u21="",
                   _u22="",_u23="",_u24="",_u25="",
                   _u28="",_u29="",_u30="",_u31="",_u32="",
                   _u33="",_u34="",_u35="",_u36="",_u37="",
                   _u38="",_u39="",_u40="",_u41="",_u42="",
                   _u43="",_u44="",_u45="",_u46="",_u47="",
                   _u48="",_u49="",_u50="",_u51="",_u52="",
                   _u53="",_u54="",_u55="",_u56="",_u57="",
                   _u58="",_u59="",_u60="",_u61="",_u62="",
                   _u63="",_u64="",_u65="",_u66="",_u67="",
                   _u68="",_u69="",_u70="",_u71="",_u72="",
                   _u73="",_u74="";
            // msgparsys.BuscarCompania(varini.sptCodEmpr, myconnect, // ERROR: CS1503
                // ref _u3, ref _u4, ref _u5, ref _u6, // ERROR: CS1503
                // ref _u7, ref _u8, ref _u9, ref _u10, // ERROR: CS1503
                // ref _u11, ref _u12, ref _u13, // ERROR: CS1503
                // ref _u14, ref nit, ref dir, // ERROR: CS1503
                // ref _u17, ref _u18, ref _u19, ref _u20, ref _u21, // ERROR: CS1503
                // ref _u22, ref _u23, ref _u24, ref _u25, // ERROR: CS1503
                // ref nombre, ref tel, // ERROR: CS1503
                // ref _u28, ref _u29, ref _u30, ref _u31, ref _u32, // ERROR: CS1503
                // ref _u33, ref _u34, ref _u35, ref _u36, ref _u37, // ERROR: CS1503
                // ref _u38, ref _u39, ref _u40, ref _u41, ref _u42, // ERROR: CS1503
                // ref _u43, ref _u44, ref _u45, ref _u46, ref _u47, // ERROR: CS1503
                // ref _u48, ref _u49, ref _u50, ref _u51, ref _u52, // ERROR: CS1503
                // ref _u53, ref _u54, ref _u55, ref _u56, ref _u57, // ERROR: CS1503
                // ref _u58, ref _u59, ref _u60, ref _u61, ref _u62, // ERROR: CS1503
                // ref _u63, ref _u64, ref _u65, ref _u66, ref _u67, // ERROR: CS1503
                // ref _u68, ref _u69, ref _u70, ref _u71, ref _u72, // ERROR: CS1503
                // ref _u73, ref _u74); // ERROR: CS1503
        }

        public void ImprimeInformes(int ClaseInforme, int Idplanilla, int idnomina, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            int IdnomIni = 0, double idcodigoini = 0, int Idnomfin = 0, double idcodigofin = 0,
            string Idcpto = null, string IdPlanillafinal = null,
            string CedulaIni = "", string CedulaFin = "",
            string idSeccionIni = "0000", string idSeccionFin = "9999",
            string idCencosIni = "00000000", string idCencosFin = "99999999")
        {
            int idPlaFin = 0, idCptoInt = 0;
            switch (ClaseInforme)
            {
                case 0:
                    switch (OrdenReg)
                    {
                        case 0:
                            ImprimePlanillaCedula(Idplanilla, idnomina, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                            break;
                        case 1:
                            ImprimePlanillaCedulaNombre(Idplanilla, idnomina, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                            break;
                    }
                    break;
                case 1:
                    int.TryParse(IdPlanillafinal, out idPlaFin);
                    ImprimePlanillaResumidaCpto(Idplanilla, idPlaFin, idnomina, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 2:
                    ImprimeDetalladoMovimientos(idnomina, Idplanilla, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 3:
                    ImprimeDespredibles(idnomina, Idplanilla, OrdenReg, CedulaIni, CedulaFin, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 4:
                    int.TryParse(IdPlanillafinal, out idPlaFin);
                    ImprimeplanillaFirmas(Idplanilla, idPlaFin, idnomina, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 5:
                    ImprimeplanillaBancos(Idplanilla, idnomina, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 6:
                    if (!Microsoft.VisualBasic.Information.IsNumeric(Idcpto))
                        Idcpto = "0";
                    int.TryParse(IdPlanillafinal, out idPlaFin);
                    int.TryParse(Idcpto, out idCptoInt);
                    ImprimeMovtoCicloConcepto(idnomina, Idplanilla, idPlaFin, OrdenReg, idCptoInt, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 7:
                    ImprimeResumenCencos(idnomina, Idplanilla, IdPlanillafinal, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 8:
                    ImprimeDesprediblesDetallado(idnomina, Idplanilla, OrdenReg, CedulaIni, CedulaFin, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 9:
                    ImprimePlanillaCencos(Idplanilla, idnomina, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
            }
        }

        public void InformesEmpleados(int ClaseInforme, int IdnomIni, double idcodigoini,
            int Idnomfin, double idcodigofin, int OrdenReg,
            DateTime Fecini, DateTime Fecfin, string Periodo,
            Form Myforma, OdbcConnection myconnect,
            string perini = "0", string perfin = "0",
            string idSeccionIni = "0000", string idSeccionFin = "9999",
            string idCencosIni = "00000000", string idCencosFin = "99999999")
        {
            switch (ClaseInforme)
            {
                case 0:
                    ImprimeDetalladoEmpleados(IdnomIni, idcodigoini, Idnomfin, idcodigofin, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 1:
                    ImprimeDetalladoAusentismos(IdnomIni, idcodigoini, Idnomfin, idcodigofin, OrdenReg, Fecini, Fecfin, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 2:
                    ImprimeDetalladoLibranzas(IdnomIni, idcodigoini, Idnomfin, idcodigofin, OrdenReg, Periodo, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 3:
                    ImprimeDstosFijos(IdnomIni, idcodigoini, Idnomfin, idcodigofin, OrdenReg, Myforma, myconnect, idSeccionIni, idSeccionFin, idCencosIni, idCencosFin);
                    break;
                case 4:
                {
                    if (!Microsoft.VisualBasic.Information.IsNumeric(perini)) perini = "0";
                    if (!Microsoft.VisualBasic.Information.IsNumeric(perfin)) perfin = "0";
                    int periniInt = 0, perfinInt = 0;
                    int.TryParse(perini, out periniInt);
                    int.TryParse(perfin, out perfinInt);
                    ImprimeCumpleanios(periniInt, perfinInt, OrdenReg, Myforma, myconnect);
                    break;
                }
                case 5:
                    ImprimeVenContratos(Fecini, Fecfin, OrdenReg, Myforma, myconnect);
                    break;
            }
        }

        public void InformesGenerales(int ClaseInforme, string Idplaini, string IdPlafin,
            string idnomina, string idnomini, string idcodini,
            string idnomfin, string idcodfin,
            Form Myforma, OdbcConnection myconnect,
            string CencosIni = "", string CencosFin = "",
            string SeccionIni = "", string SeccionFin = "", string usuario = "")
        {
            int idplainInt = 0, idplafin = 0, idnominaInt = 0,
                idnominiInt = 0, idnomfinInt = 0;
            int.TryParse(Idplaini, out idplainInt);
            int.TryParse(IdPlafin, out idplafin);
            int.TryParse(idnomina, out idnominaInt);
            int.TryParse(idnomini, out idnominiInt);
            int.TryParse(idnomfin, out idnomfinInt);

            switch (ClaseInforme)
            {
                case 0:
                    ImprimeBaseLiquidacion(idplainInt, idplafin, idnominaInt, Myforma, myconnect);
                    break;
                case 1:
                    ImprimeBeneficarios(idnominiInt, idcodini, idnomfinInt, idcodfin, Myforma, myconnect);
                    break;
                case 2:
                    ImprimeCostosMensual(idnominiInt, idcodini, idnomfinInt, idcodfin, CencosIni, CencosFin, SeccionIni, SeccionFin, Idplaini, IdPlafin, usuario, Myforma, myconnect);
                    break;
            }
        }

        public void InformesAutoLiquidaciones(int ClaseInforme, string idnomina, string IdEps,
            Form Myforma, OdbcConnection myconnect)
        {
            switch (ClaseInforme)
            {
                case 0:
                    if (!Microsoft.VisualBasic.Information.IsNumeric(IdEps))
                    {
                        MessageBox.Show("Codigo de la Eps Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!Microsoft.VisualBasic.Information.IsNumeric(idnomina))
                    {
                        MessageBox.Show("Codigo de la Empresa Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    ImprimeAutoLiqEps(int.Parse(idnomina), int.Parse(IdEps), Myforma, myconnect);
                    break;
                case 1:
                    if (!Microsoft.VisualBasic.Information.IsNumeric(IdEps))
                    {
                        MessageBox.Show("Codigo de la Eps Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!Microsoft.VisualBasic.Information.IsNumeric(idnomina))
                    {
                        MessageBox.Show("Codigo de la Empresa Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    ImprimeAutoLiqArp(int.Parse(idnomina), int.Parse(IdEps), Myforma, myconnect);
                    break;
                case 2:
                    if (!Microsoft.VisualBasic.Information.IsNumeric(IdEps))
                    {
                        MessageBox.Show("Codigo de la Eps Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!Microsoft.VisualBasic.Information.IsNumeric(idnomina))
                    {
                        MessageBox.Show("Codigo de la Empresa Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    ImprimeAutoLiqPension(int.Parse(idnomina), int.Parse(IdEps), Myforma, myconnect);
                    break;
                case 3:
                    if (!Microsoft.VisualBasic.Information.IsNumeric(idnomina))
                    {
                        MessageBox.Show("Codigo de la Empresa Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    ImprimeAutoLiqParafiscales(int.Parse(idnomina), Myforma, myconnect);
                    break;
            }
        }

        private void ImprimeAutoLiqEps(int idnomina, int IdEps, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfaut00");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            msgnompar.BuscaEps(IdEps, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("NomEps", DsDataset.Tables["tbleps"].Rows[0]["Nombre"]);
            Report.SetParameterValue("Ideps", IdEps);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeAutoLiqArp(int idnomina, int IdArp, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfaut01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            msgnompar.BuscaAdmArp(IdArp, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("NomEps", DsDataset.Tables["tblarp"].Rows[0]["Nombre"]);
            Report.SetParameterValue("Idarp", IdArp);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeAutoLiqPension(int idnomina, int IdPension, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfaut02");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            msgnompar.BuscaAdmPensiones(IdPension, myconnect, DsDataset);
            // msgnompar.BuscaParAutLiqAportes(varini.sptCodEmpr, myconnect, DsDataset); // ERROR: CS1503
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("NomEps", DsDataset.Tables["tblpensiones"].Rows[0]["Nombre"]);
            Report.SetParameterValue("Idpension", IdPension);
            Report.SetParameterValue("TasaLiq", DsDataset.Tables["tblparautApor"].Rows[0]["pension"]);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeAutoLiqParafiscales(int idnomina, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfaut03");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            // msgnompar.BuscaParAutLiqAportes(varini.sptCodEmpr, myconnect, DsDataset); // ERROR: CS1503
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("TasaSena", DsDataset.Tables["tblparautApor"].Rows[0]["Sena"]);
            Report.SetParameterValue("TasaCcf", DsDataset.Tables["tblparautApor"].Rows[0]["Ccf"]);
            Report.SetParameterValue("TasaIcbf", DsDataset.Tables["tblparautApor"].Rows[0]["Icbf"]);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeBaseLiquidacion(int Idplaini, int Idplafin, int idnomina,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfgen00");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("plaini", Idplaini);
            Report.SetParameterValue("plafin", Idplafin);
            Report.SetParameterValue("idnomina", idnomina);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeBeneficarios(int Idnominaini, string idempini, int Idnominafin,
            string idempfin, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfgen01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idcodfin", idempfin);
            Report.SetParameterValue("idcodini", idempini);
            Report.SetParameterValue("idnomfin", Idnominafin);
            Report.SetParameterValue("idnomini", Idnominaini);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimePlanillaCedula(int Idplanilla, int idnomina, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes00");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            // msgnompar.BuscaPeriodosPagos(Idplanilla, idnomina, myconnect, DsDataset); // ERROR: CS1503
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplanilla", Idplanilla);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("desplanilla", DsDataset.Tables["tblperpagos"].Rows[0]["detalle"]);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimePlanillaCedulaNombre(int Idplanilla, int idnomina, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes00A");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            // msgnompar.BuscaPeriodosPagos(Idplanilla, idnomina, myconnect, DsDataset); // ERROR: CS1503
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplanilla", Idplanilla);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("desplanilla", DsDataset.Tables["tblperpagos"].Rows[0]["detalle"]);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimePlanillaResumidaCpto(int Idplanilla, int Idplafin, int idnomina,
            int OrdenReg, Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplanilla", Idplanilla);
            Report.SetParameterValue("idplafinal", Idplafin);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeDetalladoEmpleados(int IdnominaIni, double IdempleadoIni,
            int idnominafin, double Idempleadofin, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfempl00");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("nomini", IdnominaIni);
            Report.SetParameterValue("nomfin", idnominafin);
            Report.SetParameterValue("emplini", IdempleadoIni);
            Report.SetParameterValue("emplfin", Idempleadofin);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeDetalladoAusentismos(int IdnominaIni, double IdempleadoIni,
            int idnominafin, double Idempleadofin, int OrdenReg,
            DateTime fecini, DateTime Fecfin,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfempl01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("nomini", IdnominaIni);
            Report.SetParameterValue("nomfin", idnominafin);
            Report.SetParameterValue("emplini", IdempleadoIni);
            Report.SetParameterValue("emplfin", Idempleadofin);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("fecini", Microsoft.VisualBasic.Strings.Format(fecini, varini.PstForFec));
            Report.SetParameterValue("fecfin", Microsoft.VisualBasic.Strings.Format(Fecfin, varini.PstForFec));
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeDetalladoLibranzas(int IdnominaIni, double IdempleadoIni,
            int idnominafin, double Idempleadofin, int OrdenReg,
            string Periodo, Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfempl02");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            string NomMes;
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            // NomMes = msgnompar.BuscaNomMesSaldoLib(Periodo); // ERROR: CS1061
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("nomini", IdnominaIni);
            Report.SetParameterValue("nomfin", idnominafin);
            Report.SetParameterValue("emplini", IdempleadoIni);
            Report.SetParameterValue("emplfin", Idempleadofin);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("año", Periodo.Substring(0, 4));
            // Report.SetParameterValue("messaldo", NomMes); // ERROR: CS0165
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeDstosFijos(int IdnominaIni, double IdempleadoIni,
            int idnominafin, double Idempleadofin, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfempl03");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("nomini", IdnominaIni);
            Report.SetParameterValue("nomfin", idnominafin);
            Report.SetParameterValue("emplini", IdempleadoIni);
            Report.SetParameterValue("emplfin", Idempleadofin);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeCumpleanios(int perini, int perfin, int OrdenReg,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfempl04");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("perini", perini);
            Report.SetParameterValue("perfin", perfin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeVenContratos(DateTime fecini, DateTime fecfin, int OrdenReg,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfempl05");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("fecini", fecini);
            Report.SetParameterValue("fecfin", fecfin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeDetalladoMovimientos(int Idnomina, int Planilla, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes02");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("nomina", Idnomina);
            Report.SetParameterValue("planilla", Planilla);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeDespredibles(int Idnomina, int Planilla, int OrdenReg,
            string CedulaIni, string CedulaFinal,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes03");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            // msgnompar.BuscaPeriodosPagos(Planilla, Idnomina, myconnect, DsDataset); // ERROR: CS1503
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", Idnomina);
            Report.SetParameterValue("idplanilla", Planilla);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("Mensaje", DsDataset.Tables["tblperpagos"].Rows[0]["Mensaje"]);
            Report.SetParameterValue("ciclo", DsDataset.Tables["tblperpagos"].Rows[0]["detalle"]);
            Report.SetParameterValue("Fechapago", DsDataset.Tables["tblperpagos"].Rows[0]["FechaPago"]);
            Report.SetParameterValue("cedulaini", CedulaIni);
            Report.SetParameterValue("cedulafin", CedulaFinal);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeplanillaFirmas(int Idplaini, int Idplafin, int idnomina,
            int OrdenReg, Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes04");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplaini", Idplaini);
            Report.SetParameterValue("idplafin", Idplafin);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("desplanilla", Idplaini.ToString() + " Hasta : " + Idplafin.ToString());
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeplanillaBancos(int Idplanilla, int idnomina, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes05");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            // msgnompar.BuscaPeriodosPagos(Idplanilla, idnomina, myconnect, DsDataset); // ERROR: CS1503
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplanilla", Idplanilla);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("desplanilla", DsDataset.Tables["tblperpagos"].Rows[0]["detalle"]);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeCertificados(int idnomina, string Idempleado, string idnominafinal,
            string idempleadofinal, string Anio, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmcerting01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("idempleado", Idempleado);
            Report.SetParameterValue("año", Anio);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("Idempleadofinal", idempleadofinal);
            Report.SetParameterValue("Idnominafinal", idnominafinal);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeMovtoCicloConcepto(int Idnomina, int Plaini, int PlaFin,
            int OrdenReg, int Idcpto,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes06");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            // msgnompar.BuscaPeriodosPagos(Plaini, Idnomina, myconnect, DsDataset); // ERROR: CS1503
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", Idnomina);
            Report.SetParameterValue("idplaini", Plaini);
            Report.SetParameterValue("idplafin", PlaFin);
            Report.SetParameterValue("OrdenRegistros", OrdenReg);
            Report.SetParameterValue("ciclo", DsDataset.Tables["tblperpagos"].Rows[0]["detalle"]);
            Report.SetParameterValue("cpto", Idcpto);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeResumenCencos(int Idnomina, int PlanInicial, string PlanFinal,
            int OrdenReg, Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes07");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            msgnompar.BuscaEmpresa(Idnomina, myconnect, DsDataset);
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplanilla", PlanInicial);
            Report.SetParameterValue("idplanillafinal", PlanFinal);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nombre"]);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            //Imprimir.CrystalReportViewer1.ReportSource = Report;
            Imprimir.Show(Myforma);
        }

        private void ImprimeDesprediblesDetallado(int Idnomina, int Planilla, int OrdenReg,
            string CedulaIni, string CedulaFin,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            int rawKind = 0;
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes08");
            PrintDocument prindoc = new PrintDocument();
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            // msgnompar.BuscaPeriodosPagos(Planilla, Idnomina, myconnect, DsDataset); // ERROR: CS1503
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", Idnomina);
            Report.SetParameterValue("idplanilla", Planilla);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("Mensaje", DsDataset.Tables["tblperpagos"].Rows[0]["Mensaje"]);
            Report.SetParameterValue("ciclo", DsDataset.Tables["tblperpagos"].Rows[0]["detalle"]);
            Report.SetParameterValue("Fechapago", DsDataset.Tables["tblperpagos"].Rows[0]["FechaPago"]);
            Report.SetParameterValue("cedulaini", CedulaIni);
            Report.SetParameterValue("cedulafinal", CedulaFin);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);

            try
            {
                for (int i = 0; i < prindoc.PrinterSettings.PaperSizes.Count; i++)
                {
                    if (prindoc.PrinterSettings.PaperSizes[i].PaperName == "MediaHoja")
                    {
                        rawKind = (int)prindoc.PrinterSettings.PaperSizes[i]
                            .GetType()
                            .GetField("kind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .GetValue(prindoc.PrinterSettings.PaperSizes[i]);
                        // Report.PrintOptions.PaperSize = (CrystalDecisions.Shared.PaperSize)rawKind; // ERROR: CS0246
                        break;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Problemas con la impresora , no responde ");
                return;
            }

            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeErrores(int Idplanilla, int idnomina, DataTable dsDataTable,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmerrores");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(dsDataTable);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimedatosImpo(int Idplanilla, int idnomina, DataTable dsDataTable,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmplano01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(dsDataTable);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeContPlanilla(int Idplanilla, int idnomina,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmconpla01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplanilla", Idplanilla);
            Report.SetParameterValue("idnomina", idnomina);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeLiqPretaciones(DataSet Dsdataset, double TotalPagar,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Utilidades.Numeros_A_Letras ValPagarLet = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmprestsoc01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            string ValLetras = ValPagarLet.Num_a_Letras(TotalPagar);
            Report.SetDataSource(Dsdataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("ValorLetras", ValLetras);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeLiqVacaciones(DataSet Dsdataset, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmvac01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(Dsdataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeLiqPrimaServicio(DataSet Dsdataset, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmprima01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(Dsdataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeAcumuladosPlanillas(int idnomina, int Plaini, int Plafinal,
            double idempleado, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmconacum02");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("idempleado", idempleado);
            Report.SetParameterValue("plaini", Plaini);
            Report.SetParameterValue("plafin", Plafinal);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeAcumulados(int idnomina, double idempleado,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmrescptos01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("idempleado", idempleado);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeTodosAcumulados(int idnomina, int idplaini, int idplafin,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmconacum01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("idplaini", idplaini);
            Report.SetParameterValue("idplafin", idplafin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimirComprobante(string Cpte, double Conse, bool Copias,
            string usuario, OdbcConnection myconect)
        {
            ERP.Core.Contabilidad.Reportes.ImpreDoc msgimpcnt = new ERP.Core.Contabilidad.Reportes.ImpreDoc(usuario);
            // msgimpcnt.impre(Cpte, Conse, Copias, "", myconect); // ERROR: CS1503
        }

        public void InformesPorPunto(int TipoInforme, int Idpunto, DateTime fecini,
            DateTime fecfin, Form myforma, OdbcConnection myconnect)
        {
            switch (TipoInforme)
            {
                case 0:
                    ImprimeAnuladasPorPunto(Idpunto, fecini, fecfin, myforma, myconnect);
                    break;
                case 1:
                    ImprimeFacturasPorPunto(fecini, Idpunto, myforma, myconnect);
                    break;
                case 2:
                    ImprimeResumenClienteServicioPorPunto(Idpunto, fecini, fecfin, myforma, myconnect);
                    break;
                case 3:
                    ImprimeDetalladoClientePorPunto(Idpunto, fecini, fecfin, myforma, myconnect);
                    break;
                case 4:
                    ImprimeAnexosFacturasPorPunto(Idpunto, fecini, fecfin, myforma, myconnect);
                    break;
            }
        }

        private void ImprimeAnuladasPorPunto(int idpunto, DateTime fecini, DateTime fecfin,
            Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("ord_frminfopunto00");
            string Nomcompania = " ", stnit = " ", Diremp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref Diremp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", Diremp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("fecini", Microsoft.VisualBasic.Strings.Format(fecini, varini.PstForFec));
            Report.SetParameterValue("fecfin", Microsoft.VisualBasic.Strings.Format(fecfin, varini.PstForFec));
            Report.SetParameterValue("idpunto", idpunto);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(myforma);
        }

        public void ImprimeFacturasPorPunto(DateTime FechaFacturacion, int Idpunto,
            Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.reporte Informe = new ERP.Core.Compartido.Reportes.reporte("ord_frminfopunto01");
            ERP.Core.Compartido.Forms.imprimir configreport = new ERP.Core.Compartido.Forms.imprimir();
            string nitcomp = " ", diremp = " ", nomemp = " ", telemp = " ";
            BuscarCompaniaHelper(myconnect, ref nitcomp, ref diremp, ref nomemp, ref telemp);
            Informe.SetParameterValue("empresa", nomemp);
            Informe.SetParameterValue("fecha", Microsoft.VisualBasic.Strings.Format(FechaFacturacion, varini.PstForFec));
            Informe.SetParameterValue("nit", nitcomp);
            Informe.SetParameterValue("direccion", diremp);
            Informe.SetParameterValue("telefono", telemp);
            Informe.SetParameterValue("idpunto", Idpunto);
            // configreport.CrystalReportViewer1.ReportSource = Informe; // ERROR: CS1061
            configreport.Show(myforma);
        }

        private void ImprimeResumenClienteServicioPorPunto(int Idpunto, DateTime fecini,
            DateTime fecfin, Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("ord_frminfopunto02");
            string Nomcompania = " ";
            string stnit = " ", diremp = " ", nomemp = " ", telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref diremp, ref nomemp, ref telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", diremp);
            Report.SetParameterValue("telefono", telemp);
            Report.SetParameterValue("fecini", Microsoft.VisualBasic.Strings.Format(fecini, varini.PstForFec));
            Report.SetParameterValue("fecfin", Microsoft.VisualBasic.Strings.Format(fecfin, varini.PstForFec));
            Report.SetParameterValue("idpunto", Idpunto);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(myforma);
        }

        private void ImprimeDetalladoClientePorPunto(int Idpunto, DateTime fecini,
            DateTime fecfin, Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("ord_frminfopunto03");
            string nitcomp = " ", diremp = " ", nomemp = " ", telemp = " ";
            BuscarCompaniaHelper(myconnect, ref nitcomp, ref diremp, ref nomemp, ref telemp);
            Report.SetParameterValue("empresa", nomemp);
            Report.SetParameterValue("nit", nitcomp);
            Report.SetParameterValue("direccion", diremp);
            Report.SetParameterValue("telefono", telemp);
            Report.SetParameterValue("fecini", Microsoft.VisualBasic.Strings.Format(fecini, varini.PstForFec));
            Report.SetParameterValue("fecfin", Microsoft.VisualBasic.Strings.Format(fecfin, varini.PstForFec));
            Report.SetParameterValue("idpunto", Idpunto);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(myforma);
        }

        private void ImprimeAnexosFacturasPorPunto(int Idpunto, DateTime fecini,
            DateTime fecfin, Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("ord_frminfopunto04");
            string nitcomp = " ", diremp = " ", nomemp = " ", telemp = " ";
            BuscarCompaniaHelper(myconnect, ref nitcomp, ref diremp, ref nomemp, ref telemp);
            Report.SetParameterValue("empresa", nomemp);
            Report.SetParameterValue("nit", nitcomp);
            Report.SetParameterValue("direccion", diremp);
            Report.SetParameterValue("telefono", telemp);
            Report.SetParameterValue("fecini", Microsoft.VisualBasic.Strings.Format(fecini, varini.PstForFec));
            Report.SetParameterValue("fecfin", Microsoft.VisualBasic.Strings.Format(fecfin, varini.PstForFec));
            Report.SetParameterValue("idpunto", Idpunto);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(myforma);
        }

        public void ImprimeLiqPlanUnica(int periodo, int idnomina,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmautliq01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nombre"]);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string NombreProcedimiento)
        {
            string c1 = "", c2 = "", c3 = "", c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento,
                ref c1, ref c2, ref c3, ref c4);
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string NombreProcedimiento,
            ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            try
            {
                result = false;
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.CommandText = mycomqueryconec.CommandText.Replace("''", "' '");
                mycomqueryconec.Connection = appadoConect;
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0)
                    result = true;
                while (Myread.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myread["campo1"] == DBNull.Value) Campo1 = "0";
                        else Campo1 = Myread["campo1"].ToString();
                    }
                    if (Campo2 != "")
                    {
                        if (Myread["campo2"] == DBNull.Value) Campo2 = "0";
                        else Campo2 = Myread["campo2"].ToString();
                    }
                    if (Campo3 != "")
                    {
                        if (Myread["campo3"] == DBNull.Value) Campo3 = "0";
                        else Campo3 = Myread["campo3"].ToString();
                    }
                    if (Campo4 != "")
                    {
                        if (Myread["campo4"] == DBNull.Value) Campo4 = "0";
                        else Campo4 = Myread["campo4"].ToString();
                    }
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        public DataSet ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect,
            string NombreProcedimiento, ref DataSet DsDataSet, string Nomtabla)
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
                MessageBox.Show(ex.Message + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql, "SORTEC", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
        }

        public void ImprimeLiqIntCesantias(DataSet Dsdataset, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmintcesant01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(Dsdataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeCertLaboral(Form Myforma, OdbcConnection myconnect,
            string cedulaini, string cedulafin,
            string idnominaIni, string idnominaFin,
            string seccionini, string seccionfin,
            string cencosIni, string cencosFin,
            DateTime fecha, string nombre_firma, string cargo_firma,
            string salPromedio, string planillaIni, string planillafin, string user)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmcertlab01");
            if (salPromedio == "N")
            {
                planillaIni = "0";
                planillafin = "0";
            }
            Report.SetParameterValue("user", user);
            Report.SetParameterValue("nombre_firma", nombre_firma);
            Report.SetParameterValue("cargo_firma", cargo_firma);
            Report.SetParameterValue("planillaIni", planillaIni);
            Report.SetParameterValue("planillaFin", planillafin);
            Report.SetParameterValue("salPromedio", salPromedio);
            Report.SetParameterValue("cedulaIni", cedulaini);
            Report.SetParameterValue("cedulaFin", cedulafin);
            Report.SetParameterValue("idnominaIni", idnominaIni);
            Report.SetParameterValue("idnominaFin", idnominaFin);
            Report.SetParameterValue("cencosIni", cencosIni);
            Report.SetParameterValue("cencosFin", cencosFin);
            Report.SetParameterValue("seccionIni", seccionini);
            Report.SetParameterValue("seccionFin", seccionfin);
            Report.SetParameterValue("fecha", fecha);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        private void ImprimeCostosMensual(int Idnominaini, string idempini, int Idnominafin,
            string idempfin, string CencosIni, string CencosFin,
            string SeccionIni, string SeccionFin,
            string Idplaini, string IdPlafin,
            string usuario, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfgen02");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            DataSet DsDataSet = new DataSet();
            bool okLocal = false;
            DateTime FechaIni = new DateTime(1950, 1, 1), FechaFin = new DateTime(1950, 1, 1);
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            int idPlainInt = 0, idPlafin = 0;
            int.TryParse(Idplaini, out idPlainInt);
            int.TryParse(IdPlafin, out idPlafin);
            // okLocal = msgnompar.BuscaPeriodosPagos(idPlainInt, Idnominaini, myconnect, DsDataSet); // ERROR: CS1503
            if (okLocal)
                FechaIni = Convert.ToDateTime(DsDataSet.Tables["tblperpagos"].Rows[0]["Fecinicial"]);
            // okLocal = msgnompar.BuscaPeriodosPagos(idPlafin, Idnominaini, myconnect, DsDataSet); // ERROR: CS1503
            if (okLocal)
                FechaFin = Convert.ToDateTime(DsDataSet.Tables["tblperpagos"].Rows[0]["FechaFinal"]);
            Report.SetParameterValue("planillaIni", Idplaini);
            Report.SetParameterValue("planillaFin", IdPlafin);
            Report.SetParameterValue("CencosIni", CencosIni);
            Report.SetParameterValue("CencosFin", CencosFin);
            Report.SetParameterValue("SeccionIni", SeccionIni);
            Report.SetParameterValue("SeccionFin", SeccionFin);
            Report.SetParameterValue("idnominaIni", Idnominaini);
            Report.SetParameterValue("idempini", idempini);
            Report.SetParameterValue("idnominaFin", Idnominafin);
            Report.SetParameterValue("idempFin", idempfin);
            Report.SetParameterValue("usuario", usuario);
            Report.SetParameterValue("nom_coop", Nomcompania);
            Report.SetParameterValue("tel_coop", Telemp);
            Report.SetParameterValue("dir_coop", DirEmp);
            Report.SetParameterValue("nit_coop", stnit);
            Report.SetParameterValue("FechaIni", FechaIni);
            Report.SetParameterValue("FechaFin", FechaFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeIndemnizacion(DataSet DsDataImp, int empIni, int EmpFin,
            string cedulaIni, string CedulaFin,
            string CencosIni, string CencosFin,
            string SeccionIni, string SeccionFin,
            string PlanillaIni, DateTime FecLiq, string Causa,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfindem01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(DsDataImp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idNomIni", empIni);
            Report.SetParameterValue("IdNomFin", EmpFin);
            Report.SetParameterValue("codIni", cedulaIni);
            Report.SetParameterValue("codFin", CedulaFin);
            Report.SetParameterValue("CencosIni", CencosIni);
            Report.SetParameterValue("CencosFin", CencosFin);
            Report.SetParameterValue("seccionIni", SeccionIni);
            Report.SetParameterValue("seccionFin", SeccionFin);
            Report.SetParameterValue("Planilla", PlanillaIni);
            Report.SetParameterValue("fechaLiq", FecLiq);
            Report.SetParameterValue("causa", Causa);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeLiqAntCesantias(int Idnomina, int idplanilla, int idempleado,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmantcesant01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("Idnomina", Idnomina);
            Report.SetParameterValue("idplanilla", idplanilla);
            Report.SetParameterValue("idempleado", idempleado);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeConsolidacionCesantias(DataSet Dsdataset, Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmconcesant01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(Dsdataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeInformePagos(int ClaseInforme, int empIni, int EmpFin,
            string cedulaIni, string CedulaFin,
            string CencosIni, string CencosFin,
            string SeccionIni, string SeccionFin,
            int PeriodoIni, int PeriodoFin,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = null;
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            switch (ClaseInforme)
            {
                case 0: Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfpagos01"); break;
                case 1: Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfpagos02"); break;
                case 2: Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminfpagos03"); break;
            }
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("empresaIni", empIni);
            Report.SetParameterValue("empresaFin", EmpFin);
            Report.SetParameterValue("cedulaIni", cedulaIni);
            Report.SetParameterValue("cedulaFin", CedulaFin);
            Report.SetParameterValue("CencosIni", CencosIni);
            Report.SetParameterValue("CencosFin", CencosFin);
            Report.SetParameterValue("seccionIni", SeccionIni);
            Report.SetParameterValue("seccionFin", SeccionFin);
            Report.SetParameterValue("PeriodoIni", PeriodoIni);
            Report.SetParameterValue("PeriodoFin", PeriodoFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeConsolidacionVacaciones(DataSet Dsdataset, DateTime FecCorte,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmconvaca01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(Dsdataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("feccorte", FecCorte);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        // Método para imprimir informe de planilla por centro de costo
        private void ImprimePlanillaCencos(int Idplanilla, int idnomina, int OrdenReg,
            Form Myforma, OdbcConnection myconnect,
            string idSeccionIni = "", string idSeccionFin = "",
            string idCencosIni = "", string idCencosFin = "")
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frminformes09");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            // msgnompar.BuscaPeriodosPagos(Idplanilla, idnomina, myconnect, DsDataset); // ERROR: CS1503
            msgnompar.BuscaEmpresa(idnomina, myconnect, DsDataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("idplanilla", Idplanilla);
            Report.SetParameterValue("idnomina", idnomina);
            Report.SetParameterValue("orden", OrdenReg);
            Report.SetParameterValue("desplanilla", DsDataset.Tables["tblperpagos"].Rows[0]["detalle"]);
            Report.SetParameterValue("desnomina", DsDataset.Tables["tblempresas"].Rows[0]["nomres"]);
            Report.SetParameterValue("seccionIni", idSeccionIni);
            Report.SetParameterValue("seccionFin", idSeccionFin);
            Report.SetParameterValue("cencosIni", idCencosIni);
            Report.SetParameterValue("cencosFin", idCencosFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }

        public void ImprimeRestructuradosDetallado(DateTime fecini, DateTime Fecfin,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.config_report Imprimir = new ERP.Core.Compartido.Reportes.config_report();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("cop_frestru01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("fecini", fecini);
            Report.SetParameterValue("fecfin", Fecfin);
            Imprimir.confi_reportes(Myforma, Report, true);
        }

        public void ImprimeLiquidacionRetencion(DataSet Dsdataset, int TipoProc,
            DateTime FecIni, DateTime FecFin,
            Form Myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Forms.imprimir Imprimir = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte Report = new ERP.Core.Compartido.Reportes.reporte("nom_frmliqretfte01");
            string Nomcompania = " ", stnit = " ", DirEmp = " ", Telemp = " ";
            BuscarCompaniaHelper(myconnect, ref stnit, ref DirEmp, ref Nomcompania, ref Telemp);
            Report.SetDataSource(Dsdataset);
            Report.SetParameterValue("empresa", Nomcompania);
            Report.SetParameterValue("nit", stnit);
            Report.SetParameterValue("direccion", DirEmp);
            Report.SetParameterValue("telefono", Telemp);
            Report.SetParameterValue("TipoProc", TipoProc);
            Report.SetParameterValue("FecIni", FecIni);
            Report.SetParameterValue("FecFin", FecFin);
            // Imprimir.CrystalReportViewer1.ReportSource = Report; // ERROR: CS1061
            Imprimir.Show(Myforma);
        }
    }
}
