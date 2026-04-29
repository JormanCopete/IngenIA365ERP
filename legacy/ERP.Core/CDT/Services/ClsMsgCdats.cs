using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;

namespace ERP.Core.CDT.Services
{
    public class ClsMsgCdats
    {
        private bool ok;
        private string stmysql;
        private OdbcConnection myconnect = new OdbcConnection();
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera MsgClscartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Contabilidad.Services.ClsContabilidad ClsContabilidad = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        private ERP.Core.Compartido.Configuracion.ParamSys paramsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CDT.Models.ParamCdt paramcdat = new ERP.Core.CDT.Models.ParamCdt();
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private DataSet DatLiquidacion = new DataSet();
        private ArrayList lista = new ArrayList();

        public ClsMsgCdats()
        {
            connect.MyOdbcConect(ref varini);
        }

        public enum TipoPagoInteres : int { SoloIntereses = 0, InteresesAcumulados = 1 }
        public enum FormaPagoInteres : int { Concepto = 0, CuentaAhorros = 1, Tesoreria = 2, Consigna = 3 }
        public enum FormaLiquidacion : int { LiquidaIntereses = 0, Cancelacion = 1, Capitalizacion = 2 }

        public bool BuscarLiquidacionCdats(double numcdats, OdbcConnection myconnect,
            ref int lincred, ref string codigoter, ref DateTime FecCreacion, ref DateTime FecVence,
            ref DateTime Fecha_causacion, ref int plazo, ref double TasaInt,
            ref double ValorOblig, ref string estado, ref int periodicidad, ref string consignainteres)
        {
            ok = false;
            stmysql = "select codigoter as campo1, lincred as campo2,fec_crea as campo3,fecvence as campo4 from cdt_maecdats where num_cdat = " + numcdats;
            { string s1 = codigoter, s2 = lincred.ToString(), s3 = "0", s4 = "0";
              this.connect.ExecuteQueryconec(stmysql, myconnect, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              codigoter = s1; int.TryParse(s2, out lincred); DateTime.TryParse(s3, out FecCreacion); DateTime.TryParse(s4, out FecVence); }

            stmysql = "select Estado as campo1, fec_causacion as campo2,plazo as campo3,tasaint as campo4 from cdt_maecdats where num_cdat = " + numcdats;
            { string s1 = estado, s2 = "0", s3 = plazo.ToString(), s4 = "0";
              this.connect.ExecuteQueryconec(stmysql, myconnect, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              estado = s1; DateTime.TryParse(s2, out Fecha_causacion); int.TryParse(s3, out plazo); double.TryParse(s4, out TasaInt); }

            stmysql = "select valorob as campo1, periodicidad as campo2,consignainteres as campo3 from cdt_maecdats where num_cdat = " + numcdats;
            { string s1 = "0", s2 = periodicidad.ToString(), s3 = consignainteres, s4 = "";
              ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              double.TryParse(s1, out ValorOblig); int.TryParse(s2, out periodicidad); consignainteres = s3; }
            return ok;
        }

        public bool BuscarCdats(double NumCdats, OdbcConnection connection,
            ref string codigoter, ref int lincred, ref DateTime FecCreacion, ref string RepLegal,
            ref string NomRepresentante, ref string DirComercial, ref string Telefono, ref string Celular,
            ref string Estado, ref DateTime FecCausacion, ref DateTime FecVence, ref int Plazo,
            ref double TasaInt, ref string CcNitFirmaReq1, ref string CcNitFirmaReq2, ref string CcNitFirmaReq3,
            ref string NomFirmaReq1, ref string NomFirmaReq2, ref string NomFirmaReq3,
            ref string CcNitBenefi1, ref string CcNitBenefi2, ref string CcNitBenefi3,
            ref string CcNitBenefi4, ref string CcNitBenefi5,
            ref string NomBenefi1, ref string NomBenefi2, ref string NomBenefi3,
            ref string NomBenefi4, ref string NomBenefi5,
            ref double PorcenBenefi1, ref double PorcenBenefi2, ref double PorcenBenefi3,
            ref double PorcenBenefi4, ref double PorcenBenefi5,
            ref double ValorObligacion, ref int periodicidad, ref string consignainteres,
            ref int tipocdat, ref string Marca_Capitalizado)
        {
            ok = false;

            stmysql = "select codigoter as campo1, lincred as campo2,fec_crea as campo3,rep_legal as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = codigoter, s2 = lincred.ToString(), s3 = "0", s4 = RepLegal;
              this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              codigoter = s1; int.TryParse(s2, out lincred); DateTime.TryParse(s3, out FecCreacion); RepLegal = s4; }

            stmysql = "select nom_repres as campo1, dir_comerci as campo2,telefono as campo3,celular as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = NomRepresentante, s2 = DirComercial, s3 = Telefono, s4 = Celular;
              this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              NomRepresentante = s1; DirComercial = s2; Telefono = s3; Celular = s4; }

            stmysql = "select Estado as campo1, fec_causacion as campo2,plazo as campo3,fecvence as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = Estado, s2 = "0", s3 = Plazo.ToString(), s4 = "0";
              this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              Estado = s1; DateTime.TryParse(s2, out FecCausacion); int.TryParse(s3, out Plazo); DateTime.TryParse(s4, out FecVence); }

            stmysql = "select Tasaint as campo1, cc_nit_firmareq1 as campo2,cc_nit_firmareq2 as campo3,cc_nit_firmareq3 as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = "0", s2 = CcNitFirmaReq1, s3 = CcNitFirmaReq2, s4 = CcNitFirmaReq3;
              ok = this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              double.TryParse(s1, out TasaInt); CcNitFirmaReq1 = s2; CcNitFirmaReq2 = s3; CcNitFirmaReq3 = s4; }

            stmysql = "select nom_firmareq1 as campo1, nom_firmareq2 as campo2,nom_firmareq3 as campo3,cc_nit_benefi1 as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = NomFirmaReq1, s2 = NomFirmaReq2, s3 = NomFirmaReq3, s4 = CcNitBenefi1;
              ok = this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              NomFirmaReq1 = s1; NomFirmaReq2 = s2; NomFirmaReq3 = s3; CcNitBenefi1 = s4; }

            stmysql = "select cc_nit_benefi2 as campo1, cc_nit_benefi3 as campo2,cc_nit_benefi4 as campo3,cc_nit_benefi5 as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = CcNitBenefi2, s2 = CcNitBenefi3, s3 = CcNitBenefi4, s4 = CcNitBenefi5;
              ok = this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              CcNitBenefi2 = s1; CcNitBenefi3 = s2; CcNitBenefi4 = s3; CcNitBenefi5 = s4; }

            stmysql = "select nom_benefi1 as campo1, nom_benefi2 as campo2,nom_benefi3 as campo3,nom_benefi4 as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = NomBenefi1, s2 = NomBenefi2, s3 = NomBenefi3, s4 = NomBenefi4;
              ok = this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              NomBenefi1 = s1; NomBenefi2 = s2; NomBenefi3 = s3; NomBenefi4 = s4; }

            stmysql = "select nom_benefi5 as campo1, porcen_benefi1 as campo2,porcen_benefi2 as campo3,porcen_benefi3 as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = NomBenefi5, s2 = "0", s3 = "0", s4 = "0";
              ok = this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              NomBenefi5 = s1; double.TryParse(s2, out PorcenBenefi1); double.TryParse(s3, out PorcenBenefi2); double.TryParse(s4, out PorcenBenefi3); }

            stmysql = "select porcen_benefi4 as campo1, porcen_benefi5 as campo2,valorob as campo3, periodicidad as campo4 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = "0", s2 = "0", s3 = "0", s4 = periodicidad.ToString();
              ok = this.connect.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3, ref s4);
              double.TryParse(s1, out PorcenBenefi4); double.TryParse(s2, out PorcenBenefi5); double.TryParse(s3, out ValorObligacion); int.TryParse(s4, out periodicidad); }

            stmysql = "select consignainteres as campo1,tipocdat as campo2,Marca_Capitalizado as campo3 from cdt_maecdats where num_cdat = " + NumCdats;
            { string s1 = consignainteres, s2 = tipocdat.ToString(), s3 = Marca_Capitalizado;
              ok = this.ExecuteQueryconec(stmysql, connection, "BuscarCdats", ref s1, ref s2, ref s3);
              consignainteres = s1; int.TryParse(s2, out tipocdat); Marca_Capitalizado = s3; }
            return ok;
        }

        public bool BuscarCtaAhorros(string NumCuenta, string codigoter, OdbcConnection myconnect, ref int lincred)
        {
            codigoter = Microsoft.VisualBasic.Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "select lincred as campo1 from dep_maeahor  where num_cuenta = " + NumCuenta + " and codigoter='" + codigoter + "'";
            { string s1 = lincred.ToString(), s2 = "", s3 = "", s4 = "";
              ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "BuscaCtaAhorros", ref s1, ref s2, ref s3, ref s4);
              int.TryParse(s1, out lincred); }
            return ok;
        }

        public bool GrabaCdats(double NumCdats, OdbcConnection connection, string codigoter, int lincred,
            DateTime FecCreacion, string RepLegal, string NomRepresentante, string DirComercial,
            string Telefono, string Celular, string Estado, DateTime FecCausacion,
            DateTime FecVence, int Plazo, double TasaInt, string CcNitFirmaReq1, string CcNitFirmaReq2,
            string CcNitFirmaReq3, string NomFirmaReq1, string NomFirmaReq2, string NomFirmaReq3,
            string CcNitBenefi1, string CcNitBenefi2, string CcNitBenefi3, string CcNitBenefi4,
            string CcNitBenefi5, string NomBenefi1, string NomBenefi2, string NomBenefi3,
            string NomBenefi4, string NomBenefi5, double PorcenBenefi1,
            double PorcenBenefi2, double PorcenBenefi3, double PorcenBenefi4, double PorcenBenefi5,
            double ValorObligacion, string Usuario, string NomUsu,
            ref bool Grabar, int periodicidad, string consignainteres, int tipocdat)
        {
            ok = false;
            codigoter = Microsoft.VisualBasic.Strings.Right("00000000000000" + codigoter, 14);
            RepLegal = Microsoft.VisualBasic.Strings.Right("00000000000000" + RepLegal, 14);

            ok = BuscarCdats(NumCdats, connection,
                ref codigoter, ref lincred, ref FecCreacion, ref RepLegal,
                ref NomRepresentante, ref DirComercial, ref Telefono, ref Celular,
                ref Estado, ref FecCausacion, ref FecVence, ref Plazo,
                ref TasaInt, ref CcNitFirmaReq1, ref CcNitFirmaReq2, ref CcNitFirmaReq3,
                ref NomFirmaReq1, ref NomFirmaReq2, ref NomFirmaReq3,
                ref CcNitBenefi1, ref CcNitBenefi2, ref CcNitBenefi3, ref CcNitBenefi4, ref CcNitBenefi5,
                ref NomBenefi1, ref NomBenefi2, ref NomBenefi3, ref NomBenefi4, ref NomBenefi5,
                ref PorcenBenefi1, ref PorcenBenefi2, ref PorcenBenefi3, ref PorcenBenefi4, ref PorcenBenefi5,
                ref ValorObligacion, ref periodicidad, ref consignainteres, ref tipocdat, ref RepLegal);

            if (!ok)
            {
                stmysql = "insert into cdt_maecdats(num_cdat,codigoter,lincred,estado,fec_crea,rep_legal,nom_repres,dir_comerci,Telefono,celular,fec_causacion,plazo,Tasaint,fecvence," +
                    "cc_nit_firmareq1,cc_nit_firmareq2,cc_nit_firmareq3,nom_firmareq1,nom_firmareq2,nom_firmareq3, " +
                    "cc_nit_benefi1, cc_nit_benefi2,cc_nit_benefi3, cc_nit_benefi4, cc_nit_benefi5, nom_benefi1, nom_benefi2, nom_benefi3, nom_benefi4, nom_benefi5, " +
                    "porcen_benefi1, porcen_benefi2, porcen_benefi3, porcen_benefi4, porcen_benefi5,valorob,Usuario,NomUsu,fechasys,periodicidad,consignainteres,tipocdat)" +
                    " values (" + NumCdats + ",'" + codigoter + "'," + lincred + ",'" + Estado + "','" + FecCreacion.ToString(varini.PstForFec) + "','" + RepLegal + "','" + NomRepresentante + "','" + DirComercial + "','" + Telefono + "','" + Celular + "','" +
                    FecCausacion.ToString(varini.PstForFec) + "'," + Plazo + "," + TasaInt + ",'" + FecVence.ToString(varini.PstForFec) + "','" + CcNitFirmaReq1 + "','" + CcNitFirmaReq2 + "','" + CcNitFirmaReq3 + "','" + NomFirmaReq1 + "','" + NomFirmaReq2 + "','" + NomFirmaReq3 + "','" + CcNitBenefi1 + "','" + CcNitBenefi2 + "','" + CcNitBenefi3 + "','" + CcNitBenefi4 + "','" + CcNitBenefi5 + "','" + NomBenefi1 +
                    "','" + NomBenefi2 + "','" + NomBenefi3 + "','" + NomBenefi4 + "','" + NomBenefi5 + "','" + PorcenBenefi1 + "'," + PorcenBenefi2 + "," + PorcenBenefi3 + "," + PorcenBenefi4 + "," + PorcenBenefi5 + "," + ValorObligacion + ",'" + Usuario + "','" + NomUsu + "','" + DateTime.Now.ToString(varini.pstForfecyHora) + "'," + periodicidad + ",'" + consignainteres + "','" + tipocdat + "')";
            }
            else
            {
                stmysql = "update cdt_maecdats set codigoter = '" + codigoter + "',lincred = " + lincred + ",fec_crea ='" + FecCreacion.ToString(varini.PstForFec) + "', rep_legal = '" + RepLegal + "',nom_repres = '" + NomRepresentante + "',dir_comerci = '" + DirComercial + "',Telefono= '" + Telefono + "',celular = '" + Celular +
                    "',fec_Causacion = '" + FecCausacion.ToString(varini.PstForFec) + "',plazo = " + Plazo + ",Tasaint = " + TasaInt + ", fecvence = '" + FecVence.ToString(varini.PstForFec) + "',cc_nit_firmareq1 = '" + CcNitFirmaReq1 + "',cc_nit_firmareq2 = '" + CcNitFirmaReq2 + "',cc_nit_firmareq3 = '" + CcNitFirmaReq3 + "',nom_firmareq1 = '" + NomFirmaReq1 + "',nom_firmareq2= '" + NomFirmaReq2 + "',nom_firmareq3 = '" + NomFirmaReq3 +
                    "',cc_nit_benefi1 = '" + CcNitBenefi1 + "', cc_nit_benefi2 = '" + CcNitBenefi2 + "',cc_nit_benefi3 = '" + CcNitBenefi3 + "', cc_nit_benefi4 = '" + CcNitBenefi4 + "', cc_nit_benefi5 = '" + CcNitBenefi5 + "',nom_benefi1 = '" + NomBenefi1 + "', nom_benefi2 = '" + NomBenefi2 + "',nom_benefi3 = '" + NomBenefi3 + "', nom_benefi4 = '" + NomBenefi4 +
                    "', nom_benefi5 = '" + NomBenefi5 + "', porcen_benefi1 = " + PorcenBenefi1 + ", porcen_benefi2 = " + PorcenBenefi2 + ", porcen_benefi3 = " + PorcenBenefi3 + ", porcen_benefi4 = " + PorcenBenefi4 + ", porcen_benefi5 = " + PorcenBenefi5 + ",valorob = " + ValorObligacion +
                    ",usuario='" + Usuario + "',nomusu='" + NomUsu + "',fechasys='" + DateTime.Now.ToString(varini.pstForfecyHora) + "',periodicidad=" + periodicidad + ",consignainteres='" + consignainteres + "',tipocdat='" + tipocdat + "' where num_cdat = " + NumCdats;
                Grabar = false;
            }
            ok = this.connect.ExecuteQueryconec(stmysql, connection, "GrabaCdats");
            return ok;
        }

        public bool ValidaConsecutivoCdats(ref double Consecutivo, OdbcConnection myconnect)
        {
            double Conse = 0;
            bool FueAsignado = false;
            BuscarCompaniaConse(myconnect, ref Conse);
            if (Consecutivo < Conse)
            {
                FueAsignado = true;
                Consecutivo = Conse;
            }
            else
            {
                FueAsignado = false;
            }
            return FueAsignado;
        }

        public bool GrabaDatosLiqCdat(double numcdat, DateTime fechacausacion, string estado, string UsuarioAutoriza, DateTime feccancela, OdbcConnection myconnect)
        {
            string mysql = "";
            int _dummyLincred = 0;
            ok = BuscarCdats(numcdat, myconnect,
                ref mysql, ref _dummyLincred, ref fechacausacion, ref mysql,
                ref mysql, ref mysql, ref mysql, ref mysql,
                ref estado, ref fechacausacion, ref fechacausacion, ref numcdat_plazo_dummy,
                ref numcdat_tasaint_dummy, ref mysql, ref mysql, ref mysql,
                ref mysql, ref mysql, ref mysql,
                ref mysql, ref mysql, ref mysql, ref mysql, ref mysql,
                ref mysql, ref mysql, ref mysql, ref mysql, ref mysql,
                ref numcdat_double_dummy, ref numcdat_double_dummy, ref numcdat_double_dummy, ref numcdat_double_dummy, ref numcdat_double_dummy,
                ref numcdat_double_dummy, ref numcdat_int_dummy, ref mysql, ref numcdat_int_dummy, ref mysql);
            if (ok)
            {
                mysql = "update cdt_maecdats set fec_causacion = '" + fechacausacion.ToString(varini.PstForFec) +
                        "', estado = '" + estado + "',usucancelacdat='" + UsuarioAutoriza + "',feccancela = '" + feccancela.ToString(varini.PstForFec) +
                        "' where num_cdat = " + numcdat;
                ok = this.connect.ExecuteQueryconec(mysql, myconnect, "LiquidaCdats");
            }
            return ok;
        }

        // Dummy ref fields to avoid too many local variables in GrabaDatosLiqCdat
        private int numcdat_plazo_dummy;
        private double numcdat_tasaint_dummy;
        private double numcdat_double_dummy;
        private int numcdat_int_dummy;

        public bool EliminarCdats(double NumCdats, OdbcConnection connection)
        {
            ok = false;
            stmysql = "Delete from cdt_maecdats where num_cdat = " + NumCdats;
            ok = this.connect.ExecuteQueryconec(stmysql, connection, "EliminarCdats");
            return ok;
        }

        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string nombreProcedimiento,
            ref string campo1, ref string campo2, ref string campo3, ref string campo4)
        {
            mycomqueryconec.CommandText = stMysql.Replace("''", "' '");
            mycomqueryconec.Connection = appadoConect;
            bool result = false;
            try
            {
                using (OdbcDataReader myread = mycomqueryconec.ExecuteReader())
                {
                    if (myread.RecordsAffected > 0) result = true;
                    while (myread.Read())
                    {
                        if (campo1 != "") campo1 = myread["campo1"] == DBNull.Value ? "0" : myread["campo1"].ToString().Trim();
                        if (campo2 != "") campo2 = myread["campo2"] == DBNull.Value ? "0" : myread["campo2"].ToString().Trim();
                        if (campo3 != "") campo3 = myread["campo3"] == DBNull.Value ? "0" : myread["campo3"].ToString().Trim();
                        if (campo4 != "") campo4 = myread["campo4"] == DBNull.Value ? "0" : myread["campo4"].ToString().Trim();
                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + nombreProcedimiento + "\nquery :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        private bool ExecuteQueryconec(string sql, OdbcConnection conn, string proc)
        { string c1 = "", c2 = "", c3 = "", c4 = ""; return ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref c4); }

        private bool ExecuteQueryconec(string sql, OdbcConnection conn, string proc, ref string c1)
        { string c2 = "", c3 = "", c4 = ""; return ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref c4); }

        private bool ExecuteQueryconec(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2)
        { string c3 = "", c4 = ""; return ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref c4); }

        private bool ExecuteQueryconec(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2, ref string c3)
        { string c4 = ""; return ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref c4); }

        public bool LiquidaCdats(DateTime fecLiquidacion, DateTime FecCompronte, string usuario,
            OdbcConnection myconnect, double numCdats,
            ref int Dias, ref double ValorInteres, ref double ValorIncial,
            ref string PorPlan, ref double Saldo,
            ref double TasaInt, ref double rtefte, ref DateTime fecCausacion,
            ref bool ActualizaDatos, string cpte, double NumCpte, ref string codigoter,
            string CtaLinea, int LineaAhorros, double NumAhorros, bool cierre,
            FormaLiquidacion ForLiquidacion, ref double VlrIntAcumulado,
            ref FormaPagoInteres FormaPagoInt, ref TipoPagoInteres Pago,
            string UsuarioAutorizaCancela, int LineaCapitalizacion, double NumCdatCapitalizacion,
            string TipoInteres, string TercerizarContrapartida,
            int LineaCredGarantia, double NumCredGarantia,
            ref decimal tasaliquidacion, ref string ClaseInteresCdat)
        {
            decimal i = 0;
            double VlrDia = 0, VlrMinRet = 0, PorRetFte = 0, baseRetfte = 0;
            int lincred = 0, CptoInt = 0;
            double Numlinea = 0, NumLincred = 0;
            int linea = 0;
            string cptointcdats = "9999", cptoretfte = "9999", cptocdat = "9999", cptoahorro = "9999";
            string TipoTransacion = "9999", estado = "A";
            string CuentaCpte = " ", NitAsoc = "99999999999999";
            double DIF = 0;
            int anioIni = 0, MesIni = 0, DiaIni = 0;
            string CuentaTesoreria = "0", StConsigna = "N";
            int anioFin = 0, MesFin = 0, DiaFin = 0, EstadoPeriodo_Int = 0;
            string EstadoPeriodo = "A", CuentaAho = " ";
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos clsDepositos = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();
            string CuentaNit = " ";
            double CptoCta = 0;
            DateTime feccancela = new DateTime(1950, 1, 1);
            int Plazo = 0;
            string cptocapital = "02";
            double SaldoAbonar = 0, SaldoObligacion = 0;
            string detalleDocumento = "   ";
            string claseInteres = "";
            decimal tasa = 0;
            string validarPagarrtefte = "    ";

            BuscarCompaniaLiq(myconnect, ref cptocapital, ref cptoahorro, ref cptoretfte, ref cptocdat, ref cptointcdats);

            { string _cpte = cpte; double _conse = 0;
              string _p5 = " ", _p6 = " "; double _p7 = 0, _p8 = 0; string _p9 = "N", _p10 = "N"; double _p11 = 0; string _p12 = " ", _p13 = "NC", _p14 = " ";
              DateTime _p15 = new DateTime(1950, 1, 1); string _p16 = " ", _p17 = "99999999999999", _p18 = "99999999", _p19 = CuentaCpte;
              string _p20 = "N", _p21 = "0", _p22 = "0", _p24 = "0";
              paramsys.BuscaComprobante(ref _cpte, ref _conse, false, myconnect,
                  ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12,
                  ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19,
                  ref _p20, ref _p21, ref _p22, "Y", ref _p24);
              cpte = _cpte; CuentaCpte = _p19; }

            DateTime _fecCreDum = new DateTime(1950, 1, 1), _fecVenDum = new DateTime(1950, 1, 1);
            double _valObDum = 0; string _estadoDum = " ", _consigDum = StConsigna;
            int _perDum = 1;
            this.BuscarLiquidacionCdats(numCdats, myconnect, ref lincred, ref codigoter,
                ref _fecCreDum, ref _fecVenDum, ref fecCausacion, ref Plazo, ref TasaInt,
                ref ValorIncial, ref _estadoDum, ref _perDum, ref StConsigna);

            { double _bSaldo = Saldo; double _bCuota = 0; decimal _bTasaInt = 0;
              string _bCiclod = "0", _bPeriodd = "0", _bClades = "0";
              MsgClscartera.BuscaSaldoObligacion(codigoter, lincred, numCdats,
                  int.Parse(FecCompronte.ToString("yyyyMM")), myconnect,
                  ref _bSaldo, ref _bCuota, ref _bTasaInt, ref _bCiclod, ref _bPeriodd, ref _bClades);
              Saldo = _bSaldo; }

            { string _aNombre = " ", _aNit = NitAsoc, _aAgen = "9999", _aTel = " ", _aDir = " ", _aEmail = " ", _aCel = " ";
              int _aNatjur = 0; string _aBanco = "9999", _aCueBanco = CuentaAho, _aPass = " ", _aConLine = " ", _aEsta = " ", _aPeriod = " ", _aEmpresa = "9999", _aClase = "5", _aEstado = "R";
              MsgClscartera.BuscaAsociado(ref codigoter, myconnect,
                  ref _aNombre, ref _aNit, ref _aAgen, ref _aTel, ref _aDir, ref _aEmail, ref _aCel,
                  ref _aNatjur, ref _aBanco, ref _aCueBanco, ref _aPass, ref _aConLine, ref _aEsta,
                  ref _aPeriod, ref _aEmpresa, ref _aClase, ref _aEstado);
              NitAsoc = _aNit; CuentaAho = _aCueBanco; }

            { double _oCargAd = 0; decimal _oTasaI = 0; string _oClasei = claseInteres;
              int _oClacuo = 0; double _oCuota = 0; decimal _oTasaseg = 0, _oTasaadm = 0;
              int _oPeriodd = 0, _oPlazo = 0; DateTime _oFecPri = new DateTime(1950, 1, 1);
              int _oClades = 0, _oCiclod = 0; decimal _oValorob = 0; double _oNumSol = 0;
              double _oSaldo = 0, _oCapAtra = 0, _oIntAtra = 0, _oSegAtra = 0, _oAdmonAtra = 0;
              string _oCode1 = " ", _oCode2 = " ", _oCode3 = " ";
              DateTime _oFecFact = new DateTime(1950, 1, 1); string _oCode4 = " ";
              DateTime _oFecVem = new DateTime(1950, 1, 1); string _oFecUlt = "1/1/1950", _oEmpDsto = "9999", _oInclDeb = "N", _oAutori = "Y";
              double _oCuotaAdm = 0, _oCuotaSeg = 0; string _oForAdm = "0", _oCastigo = "N", _oForSeg = "10";
              decimal _oPuntos = 0; string _oReEst = "N"; DateTime _oFecRest = new DateTime(1950, 1, 1);
              string _oCalRest = "A", _oTrasCpto = "N"; DateTime _oFecIntProp = new DateTime(1950, 1, 1);
              string _oPeriodoSal = "999999";
              MsgClscartera.BuscaObligacion(codigoter, lincred, numCdats, myconnect,
                  ref _oCargAd, ref _oTasaI, ref _oClasei, ref _oClacuo, ref _oCuota,
                  ref _oTasaseg, ref _oTasaadm, ref _oPeriodd, ref _oPlazo, ref _oFecPri,
                  ref _oClades, ref _oCiclod, ref _oValorob, ref _oNumSol, ref _oSaldo,
                  ref _oCapAtra, ref _oIntAtra, ref _oSegAtra, ref _oAdmonAtra,
                  ref _oCode1, ref _oCode2, ref _oCode3, ref _oFecFact, ref _oCode4,
                  ref _oFecVem, ref _oFecUlt, ref _oEmpDsto, ref _oInclDeb, ref _oAutori,
                  ref _oCuotaAdm, ref _oCuotaSeg, ref _oForAdm, ref _oCastigo, ref _oForSeg,
                  ref _oPuntos, ref _oReEst, ref _oFecRest, ref _oCalRest, ref _oTrasCpto,
                  ref _oFecIntProp, _oPeriodoSal);
              claseInteres = _oClasei; }

            Saldo = Saldo * -1;

            double _cuentaAhoDbl = 0;
            if (!double.TryParse(CuentaAho, out _cuentaAhoDbl))
                CuentaAho = "0";

            { string _pdesc = ""; double _ptasaMin = 0, _pPorRet = 0, _pVlrMin = 0;
              int _pCptoInt = 0, _pIncreMen = 0;
              ERP.Core.CDT.Models.ParamCdt.Navega _pNav = ERP.Core.CDT.Models.ParamCdt.Navega.Ninguno;
              int _pIdFmt = 0, _pIdCpto = 0, _pFuente = 0; string _pCueTes = CuentaTesoreria;
              int _pForPag = 0; string _pTipoInt = TipoInteres;
              // paramcdat.BuscaParamCdats(ref lincred, myconnect, // ERROR: CS1615
                  // ref _pdesc, ref _ptasaMin, ref _pPorRet, ref _pVlrMin, // ERROR: CS1615
                  // ref _pCptoInt, ref _pIncreMen, ref _pNav, // ERROR: CS1615
                  // ref _pIdFmt, ref _pIdCpto, ref _pFuente, // ERROR: CS1615
                  // ref _pCueTes, ref _pForPag, ref _pTipoInt); // ERROR: CS1615
              PorRetFte = _pPorRet; VlrMinRet = _pVlrMin;
              CptoInt = _pCptoInt; CuentaTesoreria = _pCueTes; TipoInteres = _pTipoInt; }

            anioIni = int.Parse(fecCausacion.ToString("yyyy"));
            MesIni = int.Parse(fecCausacion.ToString("MM"));
            DiaIni = int.Parse(fecCausacion.ToString("dd"));
            anioFin = int.Parse(fecLiquidacion.ToString("yyyy"));
            MesFin = int.Parse(fecLiquidacion.ToString("MM"));
            DiaFin = int.Parse(fecLiquidacion.ToString("dd"));

            if (DiaFin > 30) DiaFin = 30;
            if (DiaIni > 30) DiaIni = 30;
            if (MesFin == 2 && DiaFin >= 28) DiaFin = 30;
            if (MesIni == 2 && DiaIni >= 28) DiaIni = 30;

            Dias = ((anioFin - anioIni) * 360) + ((MesFin - MesIni) * 30) + (DiaFin - DiaIni);
            if (Dias > 360) Dias = 360;

            if (TipoInteres == "0")
            {
                i = (decimal)paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaNominalAnual, (double)TasaInt);
                ValorInteres = ValorInteres + Math.Round((double)Saldo * (double)i) * Dias;
                tasaliquidacion = i;
                ClaseInteresCdat = "0";
            }
            else if (TipoInteres == "1")
            {
                i = (decimal)paramsys.Conversion_TasaFinanciera(ERP.Core.Compartido.Configuracion.ParamSys.FormaliquidarFinanciera.TasaEfectivaAnual, (double)TasaInt, Dias, claseInteres);
                tasaliquidacion = i;
                ValorInteres = ValorInteres + Math.Round((double)Saldo * (double)i);
                ClaseInteresCdat = "1";
            }

            PorPlan = Math.Round(((double)Saldo / ValorIncial) * 100, 2).ToString();
            VlrDia = Math.Round(ValorInteres / Dias);

            if (VlrDia > VlrMinRet)
            {
                ok = ClsContabilidad.BuscarTercero(NitAsoc, myconnect,
                    ref _tNombreDum, ref _tDirDum, ref _tTelDum, ref _tEmailDum, ref _tTipoDum,
                    ref _tPrecDum, ref _tClipaDum, ref _tEstadoDum, ref validarPagarrtefte,
                    ref _tTipersDum, ref _tDiasDum, ref _tCupoDum, ref _tAsesorDum, ref _tContactDum);
                if (!ok)
                    rtefte = rtefte + Math.Round(ValorInteres * (PorRetFte / 100));
                else
                {
                    if (validarPagarrtefte == "Y")
                        rtefte = rtefte + Math.Round(ValorInteres * (PorRetFte / 100));
                    else
                        rtefte = 0;
                }

                if (PorRetFte > 0)
                    baseRetfte = Math.Round(rtefte / (PorRetFte / 100));
                else
                    baseRetfte = 0;
            }

            if (ActualizaDatos)
            {
                DateTime _bpFecIni = new DateTime(1950, 1, 1), _bpFecFin = new DateTime(1950, 1, 1);
                string _bpEstado = EstadoPeriodo, _bpPeriodo = "999999";
                MsgClscartera.buscaPeriodo("copc", myconnect, ref _bpFecIni, ref _bpFecFin,
                    FecCompronte, ref _bpEstado, ref _bpPeriodo, FecCompronte.ToString("yyyy"));
                EstadoPeriodo = _bpEstado;

                if (EstadoPeriodo == "C")
                {
                    MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                if (EstadoPeriodo == "P")
                {
                    if (MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
                        return false;
                }

                NumLincred = numCdats;
                TipoTransacion = cptointcdats;
                switch (FormaPagoInt)
                {
                    case FormaPagoInteres.Concepto:
                        linea = CptoInt; Numlinea = NumLincred; break;
                    case FormaPagoInteres.CuentaAhorros:
                        linea = LineaAhorros; Numlinea = NumAhorros; TipoTransacion = cptoahorro; break;
                    case FormaPagoInteres.Tesoreria:
                        linea = CptoInt; Numlinea = NumLincred; break;
                    case FormaPagoInteres.Consigna:
                        double _cAhoDbl = 0; double.TryParse(CuentaAho, out _cAhoDbl);
                        ok = BuscarCuentaAhorroSimple(ref _cAhoDbl, myconnect, ref CuentaNit, ref CptoCta);
                        CuentaAho = _cAhoDbl.ToString("F0");
                        if (ok)
                        {
                            if (CuentaNit == codigoter)
                            { CptoInt = (int)CptoCta; NumLincred = _cAhoDbl; cptointcdats = cptoahorro; }
                            else { linea = CptoInt; Numlinea = NumLincred; }
                        }
                        else { linea = CptoInt; Numlinea = NumLincred; }
                        break;
                }

                if (Dias > 0)
                {
                    double _cr = ValorInteres - rtefte;
                    MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred,
                        int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte,
                        0, ref _cr, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect,
                        999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false,
                        "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999",
                        999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "",
                        fecCausacion, 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999);
                    GrabaCausacionCdats((int)numCdats, fecLiquidacion, myconnect, usuario);
                }

                if (rtefte > 0)
                {
                    double _cr2 = rtefte;
                    MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, lincred, numCdats,
                        int.Parse(FecCompronte.ToString("yyyyMM")), cptoretfte, FecCompronte,
                        0, ref _cr2, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect,
                        999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false,
                        "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999",
                        999999, "99999999", " ", baseRetfte, "9999", false, 0, "CC", false, 0, "",
                        new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999);
                }

                if (Dias > 0 && TercerizarContrapartida == "Y" && CuentaCpte != "999999999999")
                {
                    double _cr3 = 0;
                    MsgClscartera.GrabaMovimiento(cpte, NumCpte, "99999999999999", 9999, 0,
                        int.Parse(FecCompronte.ToString("yyyyMM")), cptocapital, FecCompronte,
                        ValorInteres, ref _cr3, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect,
                        999999, CuentaCpte, NitAsoc, "", "99999999999999", ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false,
                        "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999",
                        999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "",
                        new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999);
                }

                if (Dias > 0 && ForLiquidacion == FormaLiquidacion.LiquidaIntereses)
                {
                    switch (FormaPagoInt)
                    {
                        case FormaPagoInteres.Concepto:
                            if (StConsigna == "Y")
                            {
                                double _cAhoDbl2 = 0; double.TryParse(CuentaAho, out _cAhoDbl2);
                                ok = BuscarCuentaAhorroSimple(ref _cAhoDbl2, myconnect, ref CuentaNit, ref CptoCta);
                                CuentaAho = _cAhoDbl2.ToString("F0");
                                if (ok)
                                {
                                    switch (Pago)
                                    {
                                        case TipoPagoInteres.SoloIntereses:
                                            { double _c = ValorInteres - rtefte;
                                              MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte, ValorInteres - rtefte, ref _c, "Liquidaci\u00f3n Automatica de Int. de Cdats - Consigna CTA", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, true, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                            { double _c = ValorInteres - rtefte;
                                              MsgClscartera.GrabaMovimiento(cpte, NumCpte, CuentaNit, (int)CptoCta, _cAhoDbl2, int.Parse(FecCompronte.ToString("yyyyMM")), cptoahorro, FecCompronte, 0, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats - Consigna CTA", usuario, myconnect, 999999, " ", " ", "", CuentaNit, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, true, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                            break;
                                        case TipoPagoInteres.InteresesAcumulados:
                                            { double _c = VlrIntAcumulado + (ValorInteres - rtefte);
                                              MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte, VlrIntAcumulado + (ValorInteres - rtefte), ref _c, "Liquidaci\u00f3n Automatica de Int. de Cdats - Consigna CTA", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, true, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                            { double _c = VlrIntAcumulado + (ValorInteres - rtefte);
                                              MsgClscartera.GrabaMovimiento(cpte, NumCpte, CuentaNit, (int)CptoCta, _cAhoDbl2, int.Parse(FecCompronte.ToString("yyyyMM")), cptoahorro, FecCompronte, 0, ref _c, "Liquidaci\u00f3n Automatica de Int. de Cdats - Consigna CTA", usuario, myconnect, 999999, " ", " ", "", CuentaNit, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, true, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                            break;
                                    }
                                }
                            }
                            break;
                        case FormaPagoInteres.CuentaAhorros:
                            switch (Pago)
                            {
                                case TipoPagoInteres.SoloIntereses:
                                    { double _c = ValorInteres - rtefte; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte, ValorInteres - rtefte, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    { double _c = ValorInteres - rtefte; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, linea, Numlinea, int.Parse(FecCompronte.ToString("yyyyMM")), TipoTransacion, FecCompronte, 0, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    break;
                                case TipoPagoInteres.InteresesAcumulados:
                                    { double _c = VlrIntAcumulado + (ValorInteres - rtefte); MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte, VlrIntAcumulado + (ValorInteres - rtefte), ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    { double _c = VlrIntAcumulado + (ValorInteres - rtefte); MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, linea, Numlinea, int.Parse(FecCompronte.ToString("yyyyMM")), TipoTransacion, FecCompronte, 0, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    break;
                            }
                            break;
                        case FormaPagoInteres.Tesoreria:
                            switch (Pago)
                            {
                                case TipoPagoInteres.SoloIntereses:
                                    { double _c = ValorInteres - rtefte; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte, ValorInteres - rtefte, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    { double _c = ValorInteres - rtefte; MsgClscartera.GrabaMovimiento(cpte, NumCpte, "99999999999999", 9999, 0, int.Parse(FecCompronte.ToString("yyyyMM")), "2", FecCompronte, 0, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, CuentaTesoreria, NitAsoc, "IC - " + NumCpte, codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "IC", NumCpte.ToString("F0"), "Liquidaci\u00f3n Automatica de Intereses de Cdats", FecCompronte, "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    break;
                                case TipoPagoInteres.InteresesAcumulados:
                                    { double _c = VlrIntAcumulado + (ValorInteres - rtefte); MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte, VlrIntAcumulado + (ValorInteres - rtefte), ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    { double _c = VlrIntAcumulado + (ValorInteres - rtefte); MsgClscartera.GrabaMovimiento(cpte, NumCpte, "99999999999999", 9999, 0, int.Parse(FecCompronte.ToString("yyyyMM")), cptocapital, FecCompronte, 0, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, CuentaTesoreria, NitAsoc, "IC - " + NumCpte, codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "IC", NumCpte.ToString("F0"), "Liquidaci\u00f3n Automatica de Intereses de Cdats", FecCompronte, "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    break;
                            }
                            break;
                    }
                }

                if (ForLiquidacion != FormaLiquidacion.LiquidaIntereses)
                {
                    estado = "C";
                    { double _c = 0; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, lincred, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptocdat, FecCompronte, Saldo, ref _c, "Cancelaci\u00f3n de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }

                    if (VlrIntAcumulado > 0)
                    { double _c = 0; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, CptoInt, NumLincred, int.Parse(FecCompronte.ToString("yyyyMM")), cptointcdats, FecCompronte, VlrIntAcumulado, ref _c, "Cancelaci\u00f3n de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }

                    if (CuentaCpte != "999999999999")
                    { double _c = 0; MsgClscartera.GrabaMovimiento(cpte, NumCpte, "99999999999999", 9999, 0, int.Parse(FecCompronte.ToString("yyyyMM")), "2", FecCompronte, ValorInteres, ref _c, "Liquidaci\u00f3n Automatica de Intereses de Cdats", usuario, myconnect, 999999, CuentaCpte, NitAsoc, "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }

                    switch (ForLiquidacion)
                    {
                        case FormaLiquidacion.Capitalizacion:
                            { double _c = Saldo + VlrIntAcumulado; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, LineaCapitalizacion, NumCdatCapitalizacion, int.Parse(FecCompronte.ToString("yyyyMM")), cptocdat, FecCompronte, 0, ref _c, "Capitalizacion Automatica del Cdats No. " + lincred + "-" + NumLincred + " con el Cdats No. " + NumCdatCapitalizacion, usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                            MarcarCdat_Capitalizados((int)NumCdatCapitalizacion, LineaCapitalizacion, codigoter, (int)numCdats, myconnect, usuario);
                            break;
                        case FormaLiquidacion.Cancelacion:
                            SaldoAbonar = Saldo + VlrIntAcumulado;
                            if (LineaCredGarantia >= 1000)
                            {
                                { double _c = SaldoAbonar; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, LineaCredGarantia, NumCredGarantia, int.Parse(FecCompronte.ToString("yyyyMM")), "99", FecCompronte, 0, ref _c, "Cancelaci\u00f3n de Cdats - Abono credito", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                if (SaldoAbonar > 0)
                                {
                                    { double _bSalObl = SaldoObligacion; double _bCuota = 0; decimal _bTasa = 0; string _bCiclod = "0", _bPeriodd = "0", _bClades = "0";
                                      MsgClscartera.BuscaSaldoObligacion(codigoter, LineaCredGarantia, NumCredGarantia, int.Parse(FecCompronte.ToString("yyyyMM")), myconnect, ref _bSalObl, ref _bCuota, ref _bTasa, ref _bCiclod, ref _bPeriodd, ref _bClades);
                                      SaldoObligacion = _bSalObl; }
                                    if (SaldoAbonar > SaldoObligacion) SaldoAbonar = SaldoObligacion;
                                    { double _c = SaldoAbonar; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, LineaCredGarantia, NumCredGarantia, int.Parse(FecCompronte.ToString("yyyyMM")), cptocapital, FecCompronte, 0, ref _c, "Cancelaci\u00f3n de Cdats - Abono credito", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    { string _bCpte = cpte; double _bConse = NumCpte;
                                      string _p5 = " ", _p6 = " "; double _p7 = 0, _p8 = 0; string _p9 = "N", _p10 = "N"; double _p11 = SaldoAbonar; string _p12 = " ", _p13 = "NC", _p14 = " ";
                                      DateTime _p15 = new DateTime(1950, 1, 1); string _p16 = " ", _p17 = "99999999999999", _p18 = "999999999999", _p19 = "N", _p20 = "0";
                                      string _p21 = "0"; int _p22 = 0; string _p23 = "99999999", _p24 = "N", _p25 = "", _p26 = "N", _p27 = "N";
                                      MsgClscartera.BuscaComprobante(ref _bCpte, ref _bConse, false, myconnect, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27);
                                      SaldoAbonar = _p11; }
                                    if (SaldoAbonar > 0) SaldoAbonar = 0;
                                    else SaldoAbonar = SaldoAbonar * -1;
                                }
                            }
                            switch (FormaPagoInt)
                            {
                                case FormaPagoInteres.Concepto:
                                    { string _bCpte = cpte; double _bConse = NumCpte;
                                      string _p5 = " ", _p6 = " "; double _p7 = 0, _p8 = 0; string _p9 = "N", _p10 = "N"; double _p11 = 0; string _p12 = detalleDocumento, _p13 = "NC", _p14 = " ";
                                      DateTime _p15 = new DateTime(1950, 1, 1); string _p16 = " ", _p17 = "99999999999999", _p18 = "999999999999", _p19 = "N", _p20 = "0";
                                      string _p21 = "0"; int _p22 = 0; string _p23 = "99999999", _p24 = "N", _p25 = "", _p26 = "N", _p27 = "N";
                                      MsgClscartera.BuscaComprobante(ref _bCpte, ref _bConse, false, myconnect, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27);
                                      detalleDocumento = _p12; }
                                    MsgClscartera.CuadreDocumento(cpte, NumCpte, usuario, myconnect, detalleDocumento);
                                    { string _bCpte = cpte; double _bConse = NumCpte;
                                      string _p5 = " ", _p6 = " "; double _p7 = 0, _p8 = 0; string _p9 = "N", _p10 = "N"; double _p11 = DIF; string _p12 = " ", _p13 = "NC", _p14 = " ";
                                      DateTime _p15 = new DateTime(1950, 1, 1); string _p16 = " ", _p17 = "99999999999999", _p18 = "999999999999", _p19 = "N", _p20 = "0";
                                      string _p21 = "0"; int _p22 = 0; string _p23 = "99999999", _p24 = "N", _p25 = "", _p26 = "N", _p27 = "N";
                                      MsgClscartera.BuscaComprobante(ref _bCpte, ref _bConse, false, myconnect, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27);
                                      DIF = _p11; }
                                    if (DIF != 0)
                                        MessageBox.Show("El comprobante No. " + cpte + "-" + NumCpte + " no esta cuadrado, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    break;
                                case FormaPagoInteres.CuentaAhorros:
                                    { double _c = SaldoAbonar; MsgClscartera.GrabaMovimiento(cpte, NumCpte, codigoter, linea, Numlinea, int.Parse(FecCompronte.ToString("yyyyMM")), TipoTransacion, FecCompronte, 0, ref _c, "Cancelaci\u00f3n de Cdats", usuario, myconnect, 999999, " ", " ", "", codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "", "", "", new DateTime(1950, 1, 1), "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    break;
                                case FormaPagoInteres.Tesoreria:
                                    { double _c = SaldoAbonar; MsgClscartera.GrabaMovimiento(cpte, NumCpte, "99999999999999", 9999, 0, int.Parse(FecCompronte.ToString("yyyyMM")), cptocapital, FecCompronte, 0, ref _c, "Cancelaci\u00f3n de Cdats", usuario, myconnect, 999999, CuentaTesoreria, NitAsoc, "IC - " + NumCpte, codigoter, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo, 0, false, "IC", NumCpte.ToString("F0"), "Cancelaci\u00f3n de Cdats", FecCompronte, "9999", " ", "99999999999999", 999999, "99999999", " ", 0, "9999", false, 0, "CC", false, 0, "", new DateTime(1900, 1, 1), 0, "Y", 0, true, ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos, " ", 9999); }
                                    break;
                            }
                            break;
                    }
                    feccancela = FecCompronte;
                }

                if (cierre)
                    MsgClscartera.TrasladaContabilidad(cpte, NumCpte, myconnect, usuario, "99999999999999", "9999", "0", "99999999999999", true);

                this.GrabaDatosLiqCdat(numCdats, fecLiquidacion, estado, UsuarioAutorizaCancela, feccancela, myconnect);
                return true;
            }
            return false;
        }

        // BuscarTercero dummy fields for LiquidaCdats
        private string _tNombreDum = " ", _tDirDum = " ", _tTelDum = "", _tEmailDum = " ";
        private string _tTipoDum = "0", _tPrecDum = "N", _tClipaDum = "N";
        private int _tEstadoDum = 0;
        private string _tTipersDum = ""; private int _tDiasDum = 0;
        private double _tCupoDum = 0; private string _tAsesorDum = "99999999999999", _tContactDum = "";

        public DataSet CargaGrillaCdats(DateTime FechaLiquidacion, int lincred, OdbcConnection myconect)
        {
            string stmysql2 = null, StFeb = null;
            DataSet DatCdats = new DataSet();
            int Forpagint = 0;
            { int _lRef = lincred; string _pdesc = ""; double _ptMin = 0, _pPorR = 0, _pVlrM = 0;
              int _pCptoI = 0, _pInc = 0;
              ERP.Core.CDT.Models.ParamCdt.Navega _pNav = ERP.Core.CDT.Models.ParamCdt.Navega.Ninguno;
              int _pIdFmt = 0, _pIdCpto = 0, _pFuente = 0; string _pCueTes = " ", _pTipoInt = "0";
              // paramcdat.BuscaParamCdats(ref _lRef, myconect, ref _pdesc, ref _ptMin, ref _pPorR, ref _pVlrM, // ERROR: CS1615
                  // ref _pCptoI, ref _pInc, ref _pNav, ref _pIdFmt, ref _pIdCpto, ref _pFuente, // ERROR: CS1615
                  // ref _pCueTes, ref Forpagint, ref _pTipoInt); // ERROR: CS1615
            } // restored: block closing brace was on commented line

            if (Forpagint == 0)
            {
                if (FechaLiquidacion.Month == 2 && FechaLiquidacion.Day == 28)
                    StFeb = "=";
                stmysql2 = "select cdtmae.num_cdat as NoCdats,cdtmae.lincred as Concepto, {fn concat({fn concat(maenit.apellido , ' ')},maenit.nombre)} as Nombre," +
                    "cdtmae.fec_causacion as FechaCausacion,cdtmae.tasaint as TasaInt,cdtmae.Plazo,cdtmae.codigoter " +
                    "from cdt_maecdats cdtmae inner join sys_maenit maenit on cdtmae.codigoter = maenit.codigoter " +
                    "inner join cop_salmaecar salmae on cdtmae.codigoter = salmae.codigoter and cdtmae.lincred = salmae.lincred and cdtmae.num_cdat = salmae.numero and salmae.periodo = " + FechaLiquidacion.ToString("yyyyMM") +
                    " where cdtmae.lincred = " + lincred + " and fec_causacion <" + StFeb + "  '" + FechaLiquidacion.ToString(varini.PstForFec) + "'" +
                    " and cdtmae.estado='A' and FecVence >=  '" + FechaLiquidacion.ToString(varini.PstForFec) + "'  and salmae.saldo < 0";
            }
            else
            {
                stmysql2 = "select cdtmae.num_cdat as NoCdats,cdtmae.lincred as Concepto, {fn concat({fn concat(maenit.apellido , ' ')},maenit.nombre)} as Nombre," +
                    "cdtmae.fec_causacion as FechaCausacion,cdtmae.tasaint as TasaInt,cdtmae.Plazo,cdtmae.codigoter " +
                    "from cdt_maecdats cdtmae inner join sys_maenit maenit on cdtmae.codigoter = maenit.codigoter " +
                    "inner join cop_salmaecar salmae on cdtmae.codigoter = salmae.codigoter and cdtmae.lincred = salmae.lincred and cdtmae.num_cdat = salmae.numero and salmae.periodo = " + FechaLiquidacion.ToString("yyyyMM") +
                    " where cdtmae.lincred = " + lincred + " and dateadd(mm,1,fec_causacion)  = '" + FechaLiquidacion.ToString(varini.PstForFec) + "'" +
                    " and cdtmae.estado='A' and  FecVence >= '" + FechaLiquidacion.ToString(varini.PstForFec) + "'  and salmae.saldo < 0";
            }

            this.connect.ExecuteQueryDataset(stmysql2, myconect, "CargaGrillaCdats", ref DatCdats, "TblCdats");
            return DatCdats;
        }

        public void AgregaColumnasLiquidacion()
        {
            double b = 0; string a = " "; DateTime c = new DateTime(1950, 1, 1); decimal d = 0;
            DatLiquidacion.Tables.Add("Datos");
            DataColumnCollection cols = DatLiquidacion.Tables[0].Columns;
            cols.Add("Numero", b.GetType());
            cols.Add("Codigo", a.GetType());
            cols.Add("Nombre", a.GetType());
            cols.Add("FechaL", c.GetType());
            cols.Add("FechaCau", c.GetType());
            cols.Add("Int", d.GetType());
            cols.Add("Dias", b.GetType());
            cols.Add("BaseLiqu", b.GetType());
            cols.Add("Interes", b.GetType());
            cols.Add("RetFte", b.GetType());
            cols.Add("Neto", b.GetType());
            cols.Add("NuevoSaldo", b.GetType());
            cols.Add("TipoLiq", a.GetType());
            cols.Add("TasaLiq", b.GetType());
        }

        public void CargaArregloLiquidacion(double NumCdat, string codigo, string nombre,
            DateTime fechaliquidacion, decimal tasaint, double dias, double saldo,
            double vlrinteres, double retfte, DateTime fechacausacion, string tipoliq, double tasaLiq)
        {
            lista.Clear();
            lista.Add(NumCdat); lista.Add(codigo); lista.Add(nombre);
            lista.Add(fechaliquidacion); lista.Add(fechacausacion); lista.Add(tasaint);
            lista.Add(dias); lista.Add(saldo); lista.Add(vlrinteres); lista.Add(retfte);
            lista.Add(vlrinteres - retfte); lista.Add((saldo + vlrinteres) - retfte);
            lista.Add(tipoliq); lista.Add(tasaLiq);
            DatLiquidacion.Tables["Datos"].Rows.Add(lista.ToArray());
        }

        public void LimpiaDatasetLiquidacion()
        {
            DatLiquidacion.Tables[0].Rows.Clear();
        }

        public void ImprimirReporte(Form pertenese, int periodo, int lincred)
        {
            // MsgSas.reporte r = new MsgSas.reporte("cdat_rcdatliquid", false); // ERROR: CS0246
            // MsgSas.config_report confi_report = new MsgSas.config_report(); // ERROR: CS0246
            connect.LlenarVarini(ref varini);
            // r.SetDataSource(DatLiquidacion.Tables["Datos"]); // ERROR: CS0103
            // r.SetParameterValue("Empresa", varini.pstEmpresa); // ERROR: CS0103
            // r.SetParameterValue("Periodo", periodo); // ERROR: CS0103
            // r.SetParameterValue("nit", varini.stnit); // ERROR: CS0103
            // r.SetParameterValue("direccion", varini.stdircompa�ia); // ERROR: CS1061
            // r.SetParameterValue("telefono", varini.sttelcompa�ia); // ERROR: CS1061
            // r.SetParameterValue("linea", lincred); // ERROR: CS0103
            // confi_report.confi_reportes(pertenese, r); // ERROR: CS0103
        }

        public bool BuscarCdatsPorAsociado(string codigoter, string periodo, OdbcConnection myconnect,
            ref int Cantidad, ref double ValorTotal)
        {
            codigoter = Microsoft.VisualBasic.Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "select count(a.num_cdat) as campo1,sum(d.saldo) as campo2 from cdt_maecdats a " +
                      "inner join cop_concar12 c on c.lincred=a.lincred and c.CODAHOR='6' " +
                      "inner join cop_salmaecar d on d.codigoter=a.codigoter and d.lincred=a.lincred and d.numero=a.num_cdat " +
                      "and d.periodo=" + periodo + " and d.saldo<>0 where a.codigoter='" + codigoter + "' group by a.codigoter";
            { string s1 = Cantidad.ToString(), s2 = "0", s3 = "", s4 = "";
              ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "BuscarCdatsPorAsociado", ref s1, ref s2, ref s3, ref s4);
              int.TryParse(s1, out Cantidad); double.TryParse(s2, out ValorTotal); }
            if (ok && ValorTotal < 0) ValorTotal *= -1;
            return ok;
        }

        public bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string nombreProcedimiento, ref DataSet dsDataset, string nombreTabla)
        {
            mycomqueryconec.CommandText = stMysql.Replace("''", "' '");
            mycomqueryconec.Connection = appadoConect;
            using (OdbcDataAdapter myread = new OdbcDataAdapter())
            {
                myread.SelectCommand = mycomqueryconec;
                myread.Fill(dsDataset, nombreTabla);
            }
            return dsDataset.Tables[nombreTabla].Rows.Count > 0;
        }

        public bool RenovarCdats(int numcdat, double TasaIntAct, string Usuario, string TipoNovedad, OdbcConnection myconnect)
        {
            DataSet DsdataSet = new DataSet();
            bool ok2 = false;
            DateTime FechaCdat, NewFecVemce, NewFecCausa, fechaApertura;
            string nomusu = "  ";
            BuscaUsuarioNombre(ref Usuario, myconnect, ref nomusu);

            stmysql = "select lincred,codigoter,ValorOb,tasaint,plazo,fec_crea, fecvence,fec_causacion from cdt_maecdats where num_cdat=" + numcdat;
            ok2 = this.connect.ExecuteQueryDataset(stmysql, myconnect, "RenovarCdats", ref DsdataSet, "TblRenovarCdats");
            if (ok2)
            {
                DataRow row = DsdataSet.Tables["TblRenovarCdats"].Rows[0];
                FechaCdat = Convert.ToDateTime(row["fecvence"]);
                NewFecVemce = this.CalculaFechaVemto(Convert.ToInt32(row["plazo"]), FechaCdat);
                NewFecCausa = Convert.ToDateTime(row["fec_causacion"]);
                fechaApertura = Convert.ToDateTime(row["fec_crea"]);

                stmysql = "update cdt_maecdats set fec_crea='" + FechaCdat.ToString(varini.PstForFec) + "', fecvence='" + NewFecVemce.ToString(varini.PstForFec) + "',fec_causacion='" + NewFecCausa.ToString(varini.PstForFec) + "'," +
                        "tasaint =" + TasaIntAct + ",usuario='" + Usuario + "',nomusu='" + nomusu + "'  where num_cdat=" + numcdat;

                ok2 = this.connect.ExecuteQueryconec(stmysql, myconnect, "RenovarCdats");
                if (ok2)
                {
                    stmysql = "update cop_maecar set FECFACT='" + FechaCdat.ToString(varini.PstForFec) + "',FECDESC='" + FechaCdat.ToString(varini.PstForFec) + "',FECAPROB='" + FechaCdat.ToString(varini.PstForFec) + "', fecvemto='" + NewFecVemce.ToString(varini.PstForFec) + "'," +
                        "tasaint =" + TasaIntAct + " where codigoter='" + row["codigoter"] + "' and lincred=" + row["lincred"] + " and numero = " + numcdat;
                    ok2 = this.connect.ExecuteQueryconec(stmysql, myconnect, "RenovarCdats/ActualizarCartera");
                    ok2 = GrabarNovedadCdat(numcdat, Convert.ToInt32(row["lincred"]), row["codigoter"].ToString(),
                        FechaCdat, fechaApertura, TasaIntAct, Convert.ToDouble(row["tasaint"]),
                        Convert.ToDouble(row["ValorOb"]), TipoNovedad, Usuario, myconnect);
                }
            }
            return ok2;
        }

        public bool GrabarNovedadCdat(int numcdat, int lincred, string codigoter, DateTime FecNovedad,
            DateTime FecApertura, double TasaIntAct, double TasaIntAnterior, double ValorCdat,
            string TipoNovedad, string usuario, OdbcConnection myconnect)
        {
            string nomusu = " ";
            BuscaUsuarioNombre(ref usuario, myconnect, ref nomusu);
            stmysql = "insert into cdt_novcdats(lincred, codigoter, numcdat,FechaNovedad,FechaApertura, TasaIntAnt, TasaIntAct, ValorCdat, TipoNovedad, Usuario, NomUsu,fechasys ) " +
                " Values (" + lincred + ",'" + codigoter + "'," + numcdat + ",'" + FecNovedad.ToString(varini.PstForFec) + "','" + FecApertura.ToString(varini.PstForFec) + "'," + TasaIntAnterior + "," + TasaIntAct + "," + ValorCdat + ",'" + TipoNovedad + "'," +
                "'" + usuario + "','" + nomusu + "','" + DateTime.Now.ToString(varini.pstForfecyHora) + "')";
            ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "GrabarNovedadCdat");
            return ok;
        }

        public bool BuscaNovedadCdat(int numcdat, OdbcConnection myconnect, ref DataSet DsDataset)
        {
            DataSet DsDataNovedad = new DataSet();
            try { DsDataset.Tables.Remove("tblnovcdat"); } catch { }

            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("select lincred, codigoter, numcdat,FechaNovedad,FechaApertura, TasaIntAnt, TasaIntAct, ValorCdat, TipoNovedad, Usuario, NomUsu,fechasys ");
            stbuilder.Append("from cdt_novcdats where numcdat = '");
            stbuilder.Append(numcdat + "'");

            this.connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaAusentismos", ref DsDataNovedad, "tblnovcdat");
            if (DsDataNovedad.Tables["tblnovcdat"].Rows.Count > 0)
            {
                try { DsDataset.Tables.Add(DsDataNovedad.Tables["tblnovcdat"].Copy()); } catch { }
                return true;
            }
            return false;
        }

        private void GrabaCausacionCdats(int numcdat, DateTime FecCausacion, OdbcConnection myconnect, string usuarioRegistro, string estado = "")
        {
            string nomusu = "  ";
            BuscaUsuarioNombre(ref usuarioRegistro, myconnect, ref nomusu);
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("Update cdt_maecdats set  fec_causacion = '" + FecCausacion.ToString(varini.PstForFec) + "',usuario= '" + usuarioRegistro + "',nomusu='" + nomusu + "'  ");
            if (estado.Trim() != "")
                stbuilder.Append(",estado='" + estado + "' ");
            stbuilder.Append(" where num_cdat  = " + numcdat);
            this.connect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaCausacionCdats");
        }

        public DataSet CargaGrillaRenovacion(DateTime FechaIncial, DateTime FechaFinal, int Periodo, int lincred, int plazo, OdbcConnection myconect)
        {
            DataSet DatCdats = new DataSet();
            string stmysql2 = "select cdtmae.num_cdat,{fn concat({fn concat(maenit.apellido , ' ')},maenit.nombre)} as Nombre," +
                "cdtmae.fec_causacion,cdtmae.Fecvence,cdtmae.tasaint,cdtmae.Plazo,cdtmae.valorob " +
                "from cdt_maecdats cdtmae inner join sys_maenit maenit on cdtmae.codigoter = maenit.codigoter " +
                "inner join cop_salmaecar salmae on cdtmae.codigoter = salmae.codigoter and cdtmae.lincred = salmae.lincred and cdtmae.num_cdat = salmae.numero and salmae.periodo = " + Periodo +
                " where cdtmae.lincred = " + lincred + " and Fecvence between '" + FechaIncial.ToString(varini.PstForFec) + "' and '" + FechaFinal.ToString(varini.PstForFec) + "'" +
                " and cdtmae.estado='A' and salmae.saldo <> 0 and cdtmae.plazo=" + plazo;
            this.connect.ExecuteQueryDataset(stmysql2, myconect, "CargaGrillaRenovacion", ref DatCdats, "TblRenovacion");
            return DatCdats;
        }

        public bool BuscarCdatEnGarantia(string codigoter, int NumCdat, int periodo, OdbcConnection myconnect,
            ref int lincred, ref double numero)
        {
            stmysql = "select a.lincred as campo1,a.numero as campo2 from cop_garantia a inner join cop_salmaecar b on a.codigoter = b.codigoter " +
                      " and a.lincred=b.lincred and a.numero=b.numero and b.periodo=" + periodo +
                      " where a.codigoter='" + codigoter + "' and numcdat=" + NumCdat + " and b.saldo<>0";
            { string s1 = lincred.ToString(), s2 = numero.ToString(), s3 = "", s4 = "";
              ok = this.connect.ExecuteQueryconec(stmysql, myconnect, "BuscarCdatEnGarantia", ref s1, ref s2, ref s3, ref s4);
              int.TryParse(s1, out lincred); double.TryParse(s2, out numero); }
            return ok;
        }

        public DateTime CalculaFechaVemto(int plazo, DateTime fechaCdat)
        {
            double meses = (double)plazo / 30.0;
            int mesesEnteros = (int)Math.Floor(meses);
            DateTime FechaVence = fechaCdat.AddMonths(mesesEnteros);
            meses = (double)plazo / 30.0;
            double fraccion = meses - Math.Floor(meses);
            if (fraccion > 0)
            {
                int extraDias = (int)Math.Round(fraccion * 30, 0);
                if (FechaVence.Month == 2)
                {
                    if (FechaVence.AddDays(extraDias).Month != FechaVence.Month)
                    {
                        if (DateTime.DaysInMonth(FechaVence.Year, FechaVence.Month) > 28)
                            FechaVence = FechaVence.AddDays(extraDias - 1);
                        else
                            FechaVence = FechaVence.AddDays(extraDias);
                    }
                    else
                        FechaVence = FechaVence.AddDays(extraDias);
                }
                else
                {
                    if (DateTime.DaysInMonth(FechaVence.Year, FechaVence.Month) > 30)
                    {
                        if (FechaVence.AddDays(extraDias).Month != FechaVence.Month)
                            FechaVence = FechaVence.AddDays(extraDias + 1);
                        else
                            FechaVence = FechaVence.AddDays(extraDias);
                    }
                    else
                        FechaVence = FechaVence.AddDays(extraDias);
                }
            }
            return FechaVence;
        }

        public DataSet BuscarReferenciaCdat(string codigoter, int lincred, double numcdat, OdbcConnection myconnect)
        {
            DataSet dsdataset = new DataSet();
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("select NumCdat,codigoter,lincred, case TipoReferencia when '1' then '1-Familiares' when '2' then '2-Personales' when '3' then '3-Comerciales' when '4' then '4-Financieras' end as TipoReferencia,");
            stbuilder.Append("a.Nombre, direccion, telefono, Celular,a.ciudad,b.nombre_ciudad from cdt_asoreferencia a inner join sys_ciudad57 b on a.ciudad=b.ciudad ");
            stbuilder.Append("where NumCdat=" + numcdat + " and codigoter='" + codigoter + "' and lincred=" + lincred);
            this.connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarReferenciaCdat", ref dsdataset, "TblReferencia");
            return dsdataset;
        }

        public bool GrabarReferenciaCdat(DataSet dsdata, OdbcConnection myconnect)
        {
            string stmysql2 = "";
            int i = 0;
            while (i < dsdata.Tables["TblReferencia"].Rows.Count)
            {
                DataRow row = dsdata.Tables["TblReferencia"].Rows[i];
                if (i == 0)
                {
                    stmysql2 = "delete from cdt_asoreferencia where NumCdat=" + row["NumCdat"] + " and codigoter='" + row["codigoter"] + "' and lincred=" + row["lincred"];
                    this.connect.ExecuteQueryconec(stmysql2, myconnect, "GrabarReferenciaCdat(Borrando)");
                }
                string tipoRef = row["TipoReferencia"].ToString();
                tipoRef = tipoRef.Length > 0 ? tipoRef.Substring(0, 1) : tipoRef;
                stmysql2 = "insert into cdt_asoreferencia (NumCdat,codigoter,lincred,TipoReferencia,Nombre,direccion,ciudad,telefono,Celular) values " +
                        "(" + row["NumCdat"] + ",'" + row["codigoter"] + "'," + row["lincred"] + ",'" + tipoRef + "','" + row["Nombre"] +
                        "','" + row["direccion"] + "'," + row["ciudad"] + ",'" + row["telefono"] + "','" + row["Celular"] + "')";
                this.connect.ExecuteQueryconec(stmysql2, myconnect, "GrabarReferenciaCdat");
                i++;
            }
            return false;
        }

        public void MarcarCdat_Capitalizados(int numcdat, int lincred, string codigoter, int num_cdat_Anterior, OdbcConnection myconnect, string usuarioRegistro)
        {
            string nomusu = "  ";
            BuscaUsuarioNombre(ref usuarioRegistro, myconnect, ref nomusu);
            string stmysql2 = " update cdt_maecdats set Marca_Capitalizado='Y',num_cdat_Anterior=" + num_cdat_Anterior + ",usuario='" + usuarioRegistro + "',nomusu='" + nomusu + "'    where num_cdat=" + numcdat;
            this.connect.ExecuteQueryconec(stmysql2, myconnect, "MarcarCdat_Capitalizados");
        }

        // --- Private helpers for external DLL calls with many params ---

        private void BuscarCompaniaLiq(OdbcConnection conn,
            ref string cptocapital, ref string cptoahorro, ref string cptoretfte,
            ref string cptocdat, ref string cptointcdats)
        {
            string p3 = " ", p4 = cptocapital, p5 = "N", p6 = "0";
            int p7 = 0; decimal p8 = 0; int p9 = 0; decimal p10 = 0; int p11 = 0, p12 = 0, p13 = 0;
            string p14 = "00", p15 = "0", p16 = " ", p17 = " ", p18 = "9999", p19 = "9999";
            string p20 = cptoahorro, p21 = "9999", p22 = cptoretfte, p23 = "", p24 = cptocdat, p25 = cptointcdats;
            string p26 = " ", p27 = "0", p28 = "9999", p29 = "99", p30 = "9999";
            double p31 = 0, p32 = 0; int p33 = 0;
            string p34 = "9999", p35 = "9999", p36 = " "; double p37 = 0;
            string p38 = " ", p39 = "", p40 = "9999", p41 = "9999", p42 = "9999";
            double p43 = 0, p44 = 0;
            string p45 = "N", p46 = "N", p47 = "0", p48 = "0", p49 = "", p50 = "", p51 = "", p52 = "", p53 = " ", p54 = " ", p55 = " ", p56 = "9999", p57 = "9999";
            char p58 = 'N'; double p59 = 0, p60 = 0; string p61 = "999999999999", p62 = " ", p63 = "   ", p64 = " ";
            char p65 = 'N'; int p66 = 0; string p67 = "N", p68 = "0", p69 = "Y", p70 = "Y", p71 = "N", p72 = "1"; int p73 = 0; string p74 = "EST";
            // paramsys.BuscarCompania(varini.sptCodEmpr, conn, // ERROR: CS1503
                // ref p3, ref p4, ref p5, ref p6, ref p7, ref p8, ref p9, ref p10, ref p11, ref p12, ref p13, // ERROR: CS1503
                // ref p14, ref p15, ref p16, ref p17, ref p18, ref p19, ref p20, ref p21, ref p22, ref p23, ref p24, ref p25, // ERROR: CS1503
                // ref p26, ref p27, ref p28, ref p29, ref p30, ref p31, ref p32, ref p33, ref p34, ref p35, ref p36, ref p37, // ERROR: CS1503
                // ref p38, ref p39, ref p40, ref p41, ref p42, ref p43, ref p44, ref p45, ref p46, ref p47, ref p48, // ERROR: CS1503
                // ref p49, ref p50, ref p51, ref p52, ref p53, ref p54, ref p55, ref p56, ref p57, ref p58, ref p59, // ERROR: CS1503
                // ref p60, ref p61, ref p62, ref p63, ref p64, ref p65, ref p66, ref p67, ref p68, ref p69, ref p70, // ERROR: CS1503
                // ref p71, ref p72, ref p73, ref p74); // ERROR: CS1503
            cptocapital = p4; cptoahorro = p20; cptoretfte = p22; cptocdat = p24; cptointcdats = p25;
        }

        private void BuscarCompaniaConse(OdbcConnection conn, ref double ConseCdat)
        {
            string p3 = " ", p4 = "00", p5 = "N", p6 = "0";
            int p7 = 0; decimal p8 = 0; int p9 = 0; decimal p10 = 0; int p11 = 0, p12 = 0, p13 = 0;
            string p14 = "00", p15 = "0", p16 = " ", p17 = " ", p18 = "9999", p19 = "9999", p20 = "9999", p21 = "9999", p22 = "9999", p23 = "", p24 = "", p25 = "9999";
            string p26 = " ", p27 = "0", p28 = "9999", p29 = "99", p30 = "9999";
            double p31 = ConseCdat, p32 = 0; int p33 = 0;
            string p34 = "9999", p35 = "9999", p36 = " "; double p37 = 0;
            string p38 = " ", p39 = "", p40 = "9999", p41 = "9999", p42 = "9999";
            double p43 = 0, p44 = 0;
            string p45 = "N", p46 = "N", p47 = "0", p48 = "0", p49 = "", p50 = "", p51 = "", p52 = "", p53 = " ", p54 = " ", p55 = " ", p56 = "9999", p57 = "9999";
            char p58 = 'N'; double p59 = 0, p60 = 0; string p61 = "999999999999", p62 = " ", p63 = "   ", p64 = " ";
            char p65 = 'N'; int p66 = 0; string p67 = "N", p68 = "0", p69 = "Y", p70 = "Y", p71 = "N", p72 = "1"; int p73 = 0; string p74 = "EST";
            // paramsys.BuscarCompania(varini.sptCodEmpr, conn, // ERROR: CS1503
                // ref p3, ref p4, ref p5, ref p6, ref p7, ref p8, ref p9, ref p10, ref p11, ref p12, ref p13, // ERROR: CS1503
                // ref p14, ref p15, ref p16, ref p17, ref p18, ref p19, ref p20, ref p21, ref p22, ref p23, ref p24, ref p25, // ERROR: CS1503
                // ref p26, ref p27, ref p28, ref p29, ref p30, ref p31, ref p32, ref p33, ref p34, ref p35, ref p36, ref p37, // ERROR: CS1503
                // ref p38, ref p39, ref p40, ref p41, ref p42, ref p43, ref p44, ref p45, ref p46, ref p47, ref p48, // ERROR: CS1503
                // ref p49, ref p50, ref p51, ref p52, ref p53, ref p54, ref p55, ref p56, ref p57, ref p58, ref p59, // ERROR: CS1503
                // ref p60, ref p61, ref p62, ref p63, ref p64, ref p65, ref p66, ref p67, ref p68, ref p69, ref p70, // ERROR: CS1503
                // ref p71, ref p72, ref p73, ref p74); // ERROR: CS1503
            ConseCdat = p31;
        }

        private bool BuscarCuentaAhorroSimple(ref double cuenta, OdbcConnection conn, ref string codigoter, ref double cptoCta)
        {
            string _cod = codigoter; int _linc = (int)cptoCta;
            DateTime _fecCrea = new DateTime(1950, 1, 1);
            string _repLegal = "", _nomRep = "", _dirCial = " ", _tel = " ", _cel = " ";
            string _estado = " "; DateTime _fecExep = new DateTime(1950, 1, 1); bool _debAuto = false;
            DateTime _fecPriDes = new DateTime(1950, 1, 1); int _tipoDes = 0, _perioci = 0; string _ciclo = " ";
            bool _sello = false, _protec = false; string _firmasReg = " ", _firmasReq = " ";
            string _nf1 = " ", _nf2 = " ", _nf3 = " ", _nf_nom1 = " ", _nf_nom2 = " ", _nf_nom3 = " ";
            string _nb1 = " ", _nb2 = " ", _nb3 = " ", _nb4 = " ", _nb5 = " ";
            string _nom_b1 = " ", _nom_b2 = " ", _nom_b3 = " ", _nom_b4 = " ", _nom_b5 = " ";
            string _p1 = " ", _p2 = " ", _p3 = " ", _p4 = " ", _p5 = " ";
            bool _excenta = false; DateTime _fechasys = new DateTime(1950, 1, 1), _fecCau = new DateTime(1950, 1, 1), _fecVem = new DateTime(1950, 1, 1);
            int _tipoCta = 0;
            bool res = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos().BuscarCuentaAhorro(ref cuenta, conn,
                ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos.Navega.Ninguno,
                ref _cod, ref _linc, ref _fecCrea, ref _repLegal, ref _nomRep, ref _dirCial, ref _tel, ref _cel,
                ref _estado, ref _fecExep, ref _debAuto, ref _fecPriDes, ref _tipoDes, ref _perioci, ref _ciclo,
                ref _sello, ref _protec, ref _firmasReg, ref _firmasReq,
                ref _nf1, ref _nf2, ref _nf3, ref _nf_nom1, ref _nf_nom2, ref _nf_nom3,
                ref _nb1, ref _nb2, ref _nb3, ref _nb4, ref _nb5,
                ref _nom_b1, ref _nom_b2, ref _nom_b3, ref _nom_b4, ref _nom_b5,
                ref _p1, ref _p2, ref _p3, ref _p4, ref _p5,
                ref _excenta, ref _fechasys, ref _fecCau, ref _fecVem, ref _tipoCta);
            codigoter = _cod; cptoCta = _linc;
            return res;
        }

        private void BuscaUsuarioNombre(ref string login, OdbcConnection conn, ref string nombre)
        {
            double _credMin = 0, _credMax = 0; object _fecVence = new DateTime(1950, 1, 1);
            int _tipoAcc = 0; string _pass = "", _grupo = "", _estatus = "";
            object _fecCrea = new DateTime(1950, 1, 1), _fecCamb = new DateTime(1950, 1, 1);
            double _grabMin = 0, _grabMax = 0; string _cedula = "";
            bool _grabRet = false, _sobreGiro = false, _validaCpte = false;
            double _autMin = 0, _autMax = 0;
            bool _cancelaCdats = false, _grabCanPAP = false, _grabFacVenc = false;
            // paramsys.BuscaUsuario(ref login, conn, ERP.Core.Compartido.Configuracion.ParamSys.Navega.Ninguno, // ERROR: CS1501
                // ref nombre, ref _credMin, ref _credMax, ref _fecVence, // ERROR: CS1501
                // ref _tipoAcc, ref _pass, ref _grupo, ref _estatus, ref _fecCrea, ref _fecCamb, // ERROR: CS1501
                // ref _grabMin, ref _grabMax, ref _cedula, ref _grabRet, ref _sobreGiro, ref _validaCpte, // ERROR: CS1501
                // ref _autMin, ref _autMax, ref _cancelaCdats, ref _grabCanPAP, ref _grabFacVenc); // ERROR: CS1501
        }
    }
}
