using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Compartido.Utilidades
{
    public class Ayuda
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini1 = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();

        public Ayuda(string user)
        {
            MyOdbcConet.MyOdbcConect(ref varini1);
        }

        public string UnloadHelp(
            string NameTable,
            string NameCampo1,
            Form Pertenese,
            string NameCampo2,
            string NameCampo3,
            string NameCampo4,
            string NameCampo5,
            string TituloColumn1,
            string TituloColumn2,
            string TituloColumn3,
            string TituloColumn4,
            string TituloColumn5,
            string Filtro,
            ref string Resultado,
            ref bool SiSelecciono,
            bool BuscaInmediato,
            ref string Resultado2)
        {
            // helptable Help = new helptable(); // ERROR: CS0246
            // Help.NameTable = NameTable; // ERROR: CS0117
            // Help.NameCampo1 = NameCampo1; // ERROR: CS0117
            // Help.NameCampo2 = NameCampo2; // ERROR: CS0117
            // Help.NameCampo3 = NameCampo3; // ERROR: CS0117
            // Help.NameCampo4 = NameCampo4; // ERROR: CS0117
            // Help.NameCampo5 = NameCampo5; // ERROR: CS0117
            // Help.NameColumna1 = TituloColumn1; // ERROR: CS0117
            // Help.NameColumna2 = TituloColumn2; // ERROR: CS0117
            // Help.NameColumna3 = TituloColumn3; // ERROR: CS0117
            // Help.NameColumna4 = TituloColumn4; // ERROR: CS0117
            // Help.NameColumna5 = TituloColumn5; // ERROR: CS0117
            // Help.Filtro = Filtro; // ERROR: CS0117
            // Help.DaleBuscar = BuscaInmediato; // ERROR: CS0117
            // Help.ShowDialog(Pertenese); // ERROR: CS0117
            // Resultado = Help.Resultado; // ERROR: CS0117
            // Resultado2 = Help.Resultado2; // ERROR: CS0117
            // SiSelecciono = Help.Selecciono; // ERROR: CS0117
            // return Help.Resultado; // ERROR: CS0117
            return string.Empty;
        }

        public string UnloadHelp(
            string NameTable,
            string NameCampo1,
            Form Pertenese)
        {
            string Resultado = null;
            bool SiSelecciono = true;
            string Resultado2 = null;
            return UnloadHelp(NameTable, NameCampo1, Pertenese,
                "", "", "", "", "", "", "", "", "", "",
                ref Resultado, ref SiSelecciono, false, ref Resultado2);
        }

        public string UnloadHelp(
            string NameTable,
            string NameCampo1,
            Form Pertenese,
            string NameCampo2,
            string NameCampo3,
            string NameCampo4,
            string NameCampo5,
            string TituloColumn1,
            string TituloColumn2,
            string TituloColumn3,
            string TituloColumn4,
            string TituloColumn5,
            string Filtro)
        {
            string Resultado = null;
            bool SiSelecciono = true;
            string Resultado2 = null;
            return UnloadHelp(NameTable, NameCampo1, Pertenese,
                NameCampo2, NameCampo3, NameCampo4, NameCampo5,
                TituloColumn1, TituloColumn2, TituloColumn3,
                TituloColumn4, TituloColumn5, Filtro,
                ref Resultado, ref SiSelecciono, false, ref Resultado2);
        }

        public DataSet AyudaDocsCop(ref string Comprobante, double Consecutivo, DateTime FechaInicial, DateTime fechaFinal, OdbcConnection myconnect)
        {
            Comprobante = ("0000" + Comprobante);
            Comprobante = Comprobante.Substring(Comprobante.Length - 4);
            string stmysql = "select COMPRONTE as Cpte,NUMERO_DOMTO as Consecutivo,DEBITO,CREDITO,FECHA,CERRADO as C, Anulado as A from cop_docmto  where COMPRONTE = '" + Comprobante + "' and NUMERO_DOMTO >= " + Consecutivo + " and FECHA between '" + FechaInicial.ToString(varini1.PstForFec) + "' and '" + fechaFinal.ToString(varini1.PstForFec) + "'";
            DataSet DtDatos = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "AyudaDocsCop",ref DtDatos, "Tbldocs");
            return DtDatos;
        }

        public DataSet AyudaDocspost(ref int IdTipoMovto, double Secuencia, DateTime FechaInicial, DateTime fechaFinal, OdbcConnection myconnect)
        {
            string stmysql = "select inv_docs.IdTipoMovto as Cpte,inv_docs.Secuencia as Consecutivo,inv_docs.FecIng,inv_docs.VlrTotal,inv_docs.VlrDsto,inv_docs.estado as Est,inv_docs.factura,cnt_nit.NOMBRE from inv_docs left join cnt_nit on inv_docs.IdCliente=cnt_nit.NIT where inv_docs.IdTipoMovto = '" + IdTipoMovto + "' and inv_docs.Secuencia >= " + Secuencia + " and inv_docs.FecIng between '" + FechaInicial.ToString(varini1.PstForFec) + "' and '" + fechaFinal.ToString(varini1.PstForFec) + "' order by inv_docs.FecIng";
            DataSet DtDatos = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "AyudaDocspost",ref DtDatos, "Tbldocs");
            return DtDatos;
        }

        public DataSet AyudaDocsCnt(ref string Comprobante, double Consecutivo, DateTime FechaInicial, DateTime fechaFinal, OdbcConnection myconnect)
        {
            Comprobante = ("0000" + Comprobante);
            Comprobante = Comprobante.Substring(Comprobante.Length - 4);
            string stmysql = "select COMPRONTE as Cpte,NUMERO as Consecutivo,DEBITO,CREDITO,FECHA,CERRADO as C, anulado as A from cnt_docmto  where COMPRONTE = '" + Comprobante + "' and NUMERO >= " + Consecutivo + " and FECHA between '" + FechaInicial.ToString(varini1.PstForFec) + "' and '" + fechaFinal.ToString(varini1.PstForFec) + "'";
            DataSet DtDatos = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "AyudaDocsCnt",ref DtDatos, "Tbldocs");
            return DtDatos;
        }

        public string CargaAyudaDocs(string Cpte, DateTime fecini, DateTime FecFin, Form paren, OdbcConnection mycon, string Modulo)
        {
            // FrmAyuDocs AyuDocs = new FrmAyuDocs(mycon); // ERROR: CS0246
            // AyuDocs.txtCpte.Text = Cpte; // ERROR: CS0103
            // AyuDocs.DtpFecini.Value = fecini; // ERROR: CS0103
            // AyuDocs.DtpFecFin.Value = FecFin; // ERROR: CS0103
            // AyuDocs.Tag = Modulo; // ERROR: CS0103
            // AyuDocs.ShowDialog(paren); // ERROR: CS0103
            // return AyuDocs.Consecutivo.ToString(); // ERROR: CS0103
            return string.Empty;
        }

        public string CargaAyudaDocs(string Cpte, DateTime fecini, DateTime FecFin, Form paren, OdbcConnection mycon)
        {
            return CargaAyudaDocs(Cpte, fecini, FecFin, paren, mycon, "cop");
        }

        public DataSet AyudaDocsSuspe(ref int IdTipoMovto, double Secuencia, DateTime FechaInicial, DateTime fechaFinal, OdbcConnection myconnect)
        {
            string stmysql = "select inv_docs.IdTipoMovto as Cpte,inv_docs.Secuencia as Consecutivo,inv_docs.FecIng,inv_docs.VlrTotal,inv_docs.VlrDsto,inv_docs.estado as Est,inv_docs.factura,cnt_nit.NOMBRE from inv_docs left join cnt_nit on inv_docs.IdCliente=cnt_nit.NIT where inv_docs.IdTipoMovto = '" + IdTipoMovto + "' and inv_docs.Secuencia >= " + Secuencia + " and inv_docs.FecIng between '" + FechaInicial.ToString(varini1.PstForFec) + "' and '" + fechaFinal.ToString(varini1.PstForFec) + "'" + " and inv_docs.estado in ('S','A') order by inv_docs.FecIng";
            DataSet DtDatos = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "AyudaDocsSuspe",ref DtDatos, "Tbldocs");
            return DtDatos;
        }

        private bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);

                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                while (Myread.Read())
                {
                    if (Campo1 != "")
                    {
                        Campo1 = (Myread["campo1"] is DBNull) ? "0" : Myread["campo1"].ToString().Trim();
                    }
                    if (Campo2 != "")
                    {
                        Campo2 = (Myread["campo2"] is DBNull) ? "0" : Myread["campo2"].ToString().Trim();
                    }
                    if (Campo3 != "")
                    {
                        Campo3 = (Myread["campo3"] is DBNull) ? "0" : Myread["campo3"].ToString().Trim();
                    }
                    if (Campo4 != "")
                    {
                        Campo4 = (Myread["campo4"] is DBNull) ? "0" : Myread["campo4"].ToString().Trim();
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

        ~Ayuda()
        {
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1,
            string TituloColumn2,
            string Str_codAso,
            string StMySql2,
            ref string RespCampo,
            string NomCampo4,
            string TituloColumn3)
        {
            // FormAyuda ayu = new FormAyuda(conect); // ERROR: CS0246
            // ayu.stNombretabla.Text = NomTabla; // ERROR: CS0103
            // ayu.stCampouno.Text = NomCampo1; // ERROR: CS0103
            // ayu.stCampodos.Text = NomCampo2; // ERROR: CS0103
            // ayu.stCampotres.Text = Nomcampo3; // ERROR: CS0103
            // ayu.stCampocuatro.Text = NomCampo4; // ERROR: CS0103
            // ayu.campo1.Text = NomCampo1; // ERROR: CS0103
            // ayu.campo2.Text = TituloColumn1; // ERROR: CS0103
            // ayu.Str_codAso = Str_codAso; // ERROR: CS0103
            // ayu.stMysql2 = StMySql2; // ERROR: CS0103
            if (TituloColumn2.Trim() != "")
            {
                // ayu.campo3.Text = TituloColumn2; // ERROR: CS0103
            }
            if (TituloColumn3.Trim() != "" && NomTabla == "sys_maenit")
            {
                // ayu.campo4.Text = "Codigo Interno"; // ERROR: CS0103
            }
            else if (TituloColumn3.Trim() != "")
            {
                // ayu.campo4.Text = TituloColumn3; // ERROR: CS0103
            }
            // ayu.ShowDialog(Pertenese); // ERROR: CS0103
            // RespCampo = ayu.stRes2; // ERROR: CS0103
            // return ayu.stRespu; // ERROR: CS0103
            return string.Empty;
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese)
        {
            string respCampo = null;
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                "Nombre", "Nombre Resumido", null, null, ref respCampo, "", "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            ref string RespCampo)
        {
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                "Nombre", "Nombre Resumido", null, null, ref RespCampo, "", "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1)
        {
            string respCampo = null;
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                TituloColumn1, "Nombre Resumido", null, null, ref respCampo, "", "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1,
            string TituloColumn2)
        {
            string respCampo = null;
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                TituloColumn1, TituloColumn2, null, null, ref respCampo, "", "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1,
            string TituloColumn2,
            ref string RespCampo)
        {
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                TituloColumn1, TituloColumn2, null, null, ref RespCampo, "", "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1,
            string TituloColumn2,
            string Str_codAso,
            string StMySql2)
        {
            string respCampo = null;
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                TituloColumn1, TituloColumn2, Str_codAso, StMySql2, ref respCampo, "", "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1,
            string TituloColumn2,
            string Str_codAso,
            string StMySql2,
            ref string RespCampo)
        {
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                TituloColumn1, TituloColumn2, Str_codAso, StMySql2, ref RespCampo, "", "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1,
            string TituloColumn2,
            string Str_codAso,
            string StMySql2,
            ref string RespCampo,
            string NomCampo4)
        {
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                TituloColumn1, TituloColumn2, Str_codAso, StMySql2, ref RespCampo, NomCampo4, "Numero Ficha");
        }

        public string CargaAyuda(
            string NomTabla,
            string NomCampo1,
            string NomCampo2,
            string Nomcampo3,
            OdbcConnection conect,
            Form Pertenese,
            string TituloColumn1,
            string TituloColumn2,
            string Str_codAso,
            string StMySql2,
            string NomCampo4,
            string TituloColumn3)
        {
            string respCampo = null;
            return CargaAyuda(NomTabla, NomCampo1, NomCampo2, Nomcampo3, conect, Pertenese,
                TituloColumn1, TituloColumn2, Str_codAso, StMySql2, ref respCampo, NomCampo4, TituloColumn3);
        }

        public void ConfiguraForma(Form Forma, ref string Empresa, ref string Servidor, ref string Bd, ref string Nomforma, ref string Usuario, ref string Fecha)
        {
            MyOdbcConet.LlenarVarini(ref varini1);
            Empresa = varini1.pstEmpresa;
            Servidor = varini1.pstServer;
            Bd = varini1.pstBdatos;
            Nomforma = Forma.Name;
            Usuario = varini1.pstUsuario;
            Fecha = DateTime.Now.ToString(varini1.PstForFec);
        }

        public void ConfiguraForma(Form Forma)
        {
            string Empresa = null;
            string Servidor = null;
            string Bd = null;
            string Nomforma = null;
            string Usuario = null;
            string Fecha = null;
            ConfiguraForma(Forma, ref Empresa, ref Servidor, ref Bd, ref Nomforma, ref Usuario, ref Fecha);
        }

        public void CalendarGrid(ref DataGridView grilla)
        {
            // CalendarColumn col = new CalendarColumn(); // ERROR: CS0246
            // grilla.Columns.Add(col); // ERROR: CS0103
        }

        public DataSet AyudaDocspostCosti(ref int IdTipoMovto, double Secuencia, DateTime FechaInicial, DateTime fechaFinal, OdbcConnection myconnect)
        {
            string stmysql = "select inv_docs.IdTipoMovto as Cpte,inv_docs.Secuencia as Consecutivo,inv_docs.FecIng,inv_docs.VlrTotal,inv_docs.VlrDsto,inv_docs.estado as Est,inv_docs.factura,cnt_nit.NOMBRE from inv_docs_orden  inv_docs  left join cnt_nit on inv_docs.IdCliente=cnt_nit.NIT where inv_docs.IdTipoMovto = '" + IdTipoMovto + "' and inv_docs.Secuencia >= " + Secuencia + " and inv_docs.FecIng between '" + FechaInicial.ToString(varini1.PstForFec) + "' and '" + fechaFinal.ToString(varini1.PstForFec) + "' order by inv_docs.FecIng";
            DataSet DtDatos = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "AyudaDocspostCosti",ref DtDatos, "Tbldocs");
            return DtDatos;
        }

        public DataSet AyudaDocspostOrdComp(ref int IdTipoMovto, double Secuencia, DateTime FechaInicial, DateTime fechaFinal, OdbcConnection myconnect)
        {
            string stmysql = "select inv_docs.IdTipoMovto as Cpte,inv_docs.Secuencia as Consecutivo,inv_docs.FecIng,inv_docs.VlrTotal,inv_docs.VlrDsto,inv_docs.estado as Est,inv_docs.factura,cnt_nit.NOMBRE from inv_docs_orden  inv_docs  left join cnt_nit on inv_docs.IdCliente=cnt_nit.NIT where inv_docs.IdTipoMovto = '" + IdTipoMovto + "' and inv_docs.Secuencia >= " + Secuencia + " and inv_docs.FecIng between '" + FechaInicial.ToString(varini1.PstForFec) + "' and '" + fechaFinal.ToString(varini1.PstForFec) + "' order by inv_docs.FecIng";
            DataSet DtDatos = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "AyudaDocspostCosti",ref DtDatos, "Tbldocs");
            return DtDatos;
        }
    }
}
