using System;
using System.Data;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Printing;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Services
{
    public partial class msginv
    {
        // methods from lines 2400-3800 of msginv.vb

        // Continuation of cierre_PrintPage (lines 2400-2512)
        // Duplicados eliminados: Calcular_Heihgt_PaperSize, ImprimeTiketCierre, BuscaDatosCierre, TrasladaNuevoCiclo, ImprimeBasesIva (originales en Part2)


















































































































































































        public void GrabaInvFisico(double Idproducto, string Periodo, double IdUbicacion, double IdBodega, double Invfisico, double InvTeorico, double Costo, System.Data.Odbc.OdbcConnection Myconnect)
        {
            double _invfisico = 0, _invteorico = 0, _costo = 0;
            ok = BuscaInvFisico(Idproducto, Periodo, IdUbicacion, IdBodega, Myconnect, ref _invfisico, ref _invteorico, ref _costo);
            switch (ok)
            {
                case false:
                    stmysql = "Insert into inv_invfisico(Periodo,Idproducto,idubicacion,idbodega,InvFisico,InvTeorico,Costo) values ('"
                              + Periodo + "','" + Idproducto + "','" + IdUbicacion + "','" + IdBodega + "','" + Invfisico + "','" + InvTeorico + "','" + Costo + "')";
                    break;
                case true:
                    stmysql = "Update inv_invfisico set InvFisico = '" + Invfisico + "',InvTeorico = '" + InvTeorico + "', Costo = '" + Costo + "'"
                        + " where Idproducto = '" + Idproducto + "' and Periodo = '" + Periodo + "' and idubicacion='" + IdUbicacion + "' and idbodega='" + IdBodega + "'";
                    break;
            }
            this.MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "GrabaInvFisico");
        }

        public bool BuscaInvFisico(double Idproducto, string Periodo, double IdUbicacion, double IdBodega, System.Data.Odbc.OdbcConnection Myconnect,
            ref double Invfisico, ref double InvTeorico, ref double Costo)
        {
            stmysql = "select InvFisico as campo1,InvTeorico as campo2,Costo as campo3 from inv_invfisico where Idproducto = '" + Idproducto + "' and Periodo = '" + Periodo + "' and idubicacion='" + IdUbicacion + "' and idbodega='" + IdBodega + "'";
            string _c1 = Invfisico.ToString(), _c2 = InvTeorico.ToString(), _c3 = Costo.ToString();
            // ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "BuscaInvFisico", ref _c1, ref _c2, ref _c3); // ERROR: CS1501
            double.TryParse(_c1, out Invfisico);
            double.TryParse(_c2, out InvTeorico);
            double.TryParse(_c3, out Costo);
            return ok;
        }

        public bool EliminaInvFisico(double Idproducto, string Periodo, double IdUbicacion, double IdBodega, System.Data.Odbc.OdbcConnection Myconnect)
        {
            stmysql = "delete from  inv_invfisico where Idproducto = '" + Idproducto + "' and Periodo = '" + Periodo + "' and idubicacion='" + IdUbicacion + "' and idbodega='" + IdBodega + "'";
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "EliminaInvFisico");
            return ok;
        }

        public void ConsolidadInventario(int Periodo, DateTime FechaMovto, string UsuCxC, string usuario, double IdUbicacion, double IdBodega, System.Windows.Forms.Form Myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            int TipMovAjuInv = 0;
            double Secuencia = 0, Cantidad = 0;
            decimal TasaIva = 0;
            double VlrIva = 0, VlrDsto = 0;
            int InvTeorico = 0, ActCont = 0;
            double Costo = 0, Neto = 0, Subtotal = 0;
            string ClaseTran = "z";
            int canreg = 0, fila = 0;
            double Precio = 0, VlrBruto = 0;
            string Cedula = "99999999999999";
            string CpteTran = "9999";
            DataSet DsDataSet = new DataSet();
            string StGrupo = " ";
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();

            string _r1 = "", _r2 = "", _r3 = "", _r4 = "", _r5 = "", _r6 = "", _r7 = "", _r8 = "", _r9 = "", _r10 = "", _r11 = "", _r12 = "", _r13 = "", _r14 = "", _r15 = "", _r16 = "", _r17 = "", _r18 = "", _r19 = "", _r20 = "", _r21 = "", _r22 = "";
            string _tipMovStr = TipMovAjuInv.ToString();
            // msginvconf.BuscaDatosFacturacion(1, myconnect, ref _r1, ref _r2, ref _r3, ref _r4, ref _r5, ref _r6, ref _r7, ref _r8, ref _r9, ref _r10, ref _r11, ref _r12, ref _r13, ref _r14, ref _r15, ref _r16, ref _r17, ref _r18, ref _r19, ref _r20, ref _r21, ref _r22, ref TipMovAjuInv); // ERROR: CS7036
            this.BuscaSecuencia(TipMovAjuInv, myconnect, ref Secuencia);
            string _t1 = "", _t2 = "";
            int _actCont = 0;
            // msginvconf.BuscaTipomovto(TipMovAjuInv, myconnect, ref _t1, ref _t2, ref CpteTran, ref _t1, ref _t1, ref _actCont, ref _t1, ref ActCont); // ERROR: CS7036
            string _u1 = "", _u2 = "", _u3 = "", _u4 = "", _u5 = "", _u6 = "", _u7 = "", _u8 = "", _u9 = "", _u10 = "", _u11 = "", _u12 = "", _u13 = "", _u14 = "";
            // this.msgsys.BuscaUsuario(UsuCxC, myconnect, ERP.Core.Compartido.Configuracion.ParamSys.Navega.Ninguno, ref _u1, ref _u2, ref _u3, ref _u4, ref _u5, ref _u6, ref _u7, ref _u8, ref _u9, ref _u10, ref _u11, ref _u12, ref _u13, ref _u14, ref _u15, ref Cedula); // ERROR: CS0103

            DsDataSet.Tables.Add("tblinforme");
            DsDataSet.Tables["tblinforme"].Columns.Add("IdProducto", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("nomproducto", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("IdGrupo", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("NomGrupo", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("VlrUnidad", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("Cantidad", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("ClaseTran", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("Valor", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("IdUbicacion", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("NomUbicacion", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("IdBodega", StGrupo.GetType());
            DsDataSet.Tables["tblinforme"].Columns.Add("NomBodega", StGrupo.GetType());

            //DsDataSet.Tables("tblinforme").WriteXml("D:\tblinforme.xml", XmlWriteMode.WriteSchema)

            StBuilder.Append("select invfisico.idproducto,invpro.descripcion as nompro,invpro.idgruproducto,grupo.descripcion as nomgru,invfisico.invfisico,invfisico.idubicacion,invfisico.idbodega,");
            StBuilder.Append("invfisico.invteorico,(invcrtl.CantInicial + invcrtl.CantCompra) - CantVendida as cantfinal,invfisico.costo ,invcrtl.Costo as invcosto,ubi.descripcion as NomUbicacion,bod.descripcion as NomBodega ");
            StBuilder.Append("from inv_invfisico invfisico inner join inv_ctrlinv invcrtl on invfisico.periodo = invcrtl.idperiodo and ");
            StBuilder.Append("invfisico.idproducto = invcrtl.idproducto and invfisico.idubicacion=invcrtl.idubicacion and invfisico.idbodega=invcrtl.idbodega ");
            StBuilder.Append("inner join inv_productos invpro on invfisico.idproducto = invpro.idproducto ");
            StBuilder.Append("left join inv_grupos grupo on invpro.idgruproducto = grupo.idgruproducto ");
            StBuilder.Append("left join inv_ubicacion ubi on invfisico.idubicacion=ubi.idubicacion ");
            StBuilder.Append("left join inv_bodegas bod on invfisico.idbodega=bod.idbodega ");
            StBuilder.Append("where invfisico.periodo = '" + Periodo + "' and invfisico.idubicacion=" + IdUbicacion + " and invfisico.idbodega=" + IdBodega);

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "ConsolidadInventario", ref myRead, "TblConsolInv");
            canreg = myRead.Tables["TblConsolInv"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblConsolInv"].Rows[fila];

                Precio = 0; Costo = 0; TasaIva = 0; VlrIva = 0; VlrDsto = 0; Neto = 0; Subtotal = 0;
                Cantidad = Convert.ToDouble(row["cantfinal"]) - Convert.ToDouble(row["Invfisico"]);

                if (Cantidad != 0)
                {
                    double _costo2 = 0;
                    double _ci = 0, _cc = 0, _cv = 0, _cf = 0, _uc = 0, _cosI = 0;
                    // BuscaInventario(row["idproducto"], Strings.Format(FechaMovto, "yyyyMM"), row["idubicacion"], row["idbodega"], myconnect, ref _ci, ref _cc, ref _cv, ref _cf, ref Costo); // ERROR: CS1503, CS1620
                    if (Cantidad < 0)
                    {
                        Subtotal = (Cantidad * -1) * Costo;
                        Neto = Subtotal + VlrIva - VlrDsto;
                        ClaseTran = "C";
                    }
                    else
                    {
                        Subtotal = Cantidad * Costo;
                        Neto = Subtotal + VlrIva - VlrDsto;
                        ClaseTran = "V";
                    }

                    // this.GrabaMovimiento(TipMovAjuInv, Secuencia, row["idproducto"], "U", row["idubicacion"], row["idbodega"], myconnect, FechaMovto, Cantidad, Costo, 0, TasaIva, 0, Cedula, VlrIva, VlrDsto, Neto, Subtotal, usuario, "", "", "", "Ajuste de Inventario Fisico"); // ERROR: CS1501

                    DsDataSet.Tables["tblinforme"].Rows.Add(row["idproducto"], row["nompro"], row["idgruproducto"], row["nomgru"], row["Costo"], Cantidad, ClaseTran, Neto, row["idubicacion"], row["NomUbicacion"], row["idbodega"], row["NomBodega"]);
                }

                if (Convert.ToDouble(row["costo"]) != Convert.ToDouble(row["invcosto"]))
                {
                    stmysql = "update inv_ctrlinv set costo = '" + row["costo"] + "', UltCostoPro = '" + row["costo"] + "' "
                            + "where Idperiodo = '" + Strings.Format(FechaMovto, "yyyyMM") + "' and Idproducto = '" + row["idproducto"] + "' "
                            + "and idubicacion=" + row["idubicacion"] + " and idbodega=" + row["idbodega"];
                    this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "ConsolidadInventario");
                }
                fila += 1;
            }
            myRead.Dispose();

            switch (ActCont)
            {
                case 3:
                    this.ActualizaContabilidadInvFisico(TipMovAjuInv, Secuencia, FechaMovto, varini.pstUsuario, myconnect, Myforma);
                    break;
            }
            GrabaEstado(TipMovAjuInv, Secuencia, myconnect, "C");

            ImprimeConsolInvFisico(Periodo.ToString(), DsDataSet, Myforma, myconnect);
        }

        public void BuscaDatosVenta(string Idproducto, double Cantidad, string Idcliente, string TipoVenta, System.Data.Odbc.OdbcConnection myconnect,
            ref double VlrIva, ref double Neto, ref double Subtotal, ref double VlrDsto, ref double PreVenta, ref double VlrBruto)
        {
            int ClaIva = 0;
            decimal TasaIva = 0, PorDsto = 0;
            double PreVentCant = 0, ValCant = 0;

            // BuscaProductos with required and optional params
            string _d1 = "", _d2 = "", _d3 = "", _d4 = "", _d5 = "", _d6 = "", _d7 = "";
            int _claIva2 = 0;
            decimal _tasaIva2 = 0;
            // msginvconf.BuscaProductos(Idproducto, myconnect, ref _d1, ref _d2, ref _d3, ref _d4, ref _d5, ref _d6, ref _d7, ref ClaIva, ref TasaIva, // ERROR: CS7036
                // ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref PorDsto, ref Idcliente, ref _d1, ref PreVentCant, ref ValCant); // ERROR: CS7036

            switch (TipoVenta)
            {
                case "P":
                    PreVenta = PreVentCant;
                    break;
            }

            switch (ClaIva)
            {
                case 0:
                    Subtotal = (PreVenta * Cantidad);
                    VlrIva = 0;
                    break;
                case 1:
                    VlrIva = Math.Round((PreVenta * Cantidad) * ((double)TasaIva / 100), 0);
                    Subtotal = (PreVenta * Cantidad);
                    break;
                case 2:
                    VlrIva = Math.Round(((PreVenta * Cantidad) * (double)TasaIva) / (100 + (double)TasaIva), 0);
                    Subtotal = (PreVenta * Cantidad) - VlrIva;
                    break;
            }

            VlrDsto = Math.Round((PreVenta * Cantidad) * ((double)PorDsto / 100), 0);
            VlrBruto = Cantidad * PreVenta;
            Neto = Subtotal + VlrIva - VlrDsto;
        }

        public void BuscaDatosCompra(string IdProducto, ref double cant, ref double PreVenta, ref decimal TasaIva, ref decimal PorDsto, ref double VlrIva, ref double VlrDsto, ref double neto, ref double Subtotal, System.Data.Odbc.OdbcConnection myconnect)
        {
            string descripcion = " ";
            int ClaIva = 0;
            double CrtlExistencia = 0;

            string _d1 = "", _d2 = "", _d3 = "", _d4 = "", _d5 = "", _d6 = "", _d7 = "";
            int _i1 = 0;
            decimal _dec1 = 0;
            // msginvconf.BuscaProductos(IdProducto, myconnect, ref descripcion, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _i1, ref _dec1, // ERROR: CS7036
                // ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref _d1, ref PorDsto); // ERROR: CS7036

            PreVenta = Convert.ToDouble(PreVenta);
            cant = Convert.ToDouble(cant);
            TasaIva = Convert.ToDecimal(Convert.ToDouble(TasaIva));

            VlrIva = Math.Round((PreVenta * cant) * ((double)TasaIva / 100), 0);
            VlrDsto = Math.Round((PreVenta * cant) * ((double)PorDsto / 100), 0);

            Subtotal = PreVenta * cant;
            neto = Subtotal + VlrIva - VlrDsto;
        }

        public void ActulizacionLotes(DateTime Fecini, DateTime Fecfin, System.Windows.Forms.Form Myforma, System.Data.Odbc.OdbcConnection Myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0, ValorCosto = 0;

            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actulizando informacion !!!", Myforma);

            stmysql = "update inv_ctrlinv set cantcompra = 0, Cantvendida = 0,costo = costoinicial, ultcostopro = costoinicial,CantFinal=CantInicial where idperiodo = '" + Strings.Format(Fecini, "yyyyMM") + "'";
            this.MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "ActulizacionLotes");

            StBuilder.Append("select movtos.idproducto, movtos.cantidad,movtos.fecmovto,movtos.tipoVenta, movtos.ClaMovto,movtos.Vlrunidad,movtos.idtipomovto, ");
            StBuilder.Append("movtos.subtotal,movtos.idubicacion,movtos.idbodega,tipmov.devolucion,movtos.costo,movtos.secuencia,movtos.fecsystem,doc.estado ");
            StBuilder.Append("from inv_movtos movtos ");
            StBuilder.Append("inner join inv_tipomovtos tipmov on movtos.idtipomovto=tipmov.idtipomovto ");
            StBuilder.Append("inner join inv_docs doc on movtos.idtipomovto=doc.idtipomovto and movtos.secuencia=doc.secuencia ");
            StBuilder.Append("where fecmovto between '" + Strings.Format(Fecini, varini.PstForFec) + "' and '" + Strings.Format(Fecfin, varini.PstForFec) + "' ");
            StBuilder.Append("order by movtos.fecmovto,movtos.fecsystem,movtos.consecmovto");

            this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), Myconnect, "ActulizacionLotes", ref DsDataSet, "tblMovtos");

            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblMovtos"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataSet.Tables["tblMovtos"].Rows.Count)
            {
                ValorCosto = 0;
                DataRow row = DsDataSet.Tables["tblMovtos"].Rows[(int)fila];
                switch (row["devolucion"].ToString())
                {
                    case "Y":
                        // this.GrabaExistenciaDevolucion(row["idproducto"], row["idubicacion"], row["idbodega"], row["cantidad"], row["Vlrunidad"], row["idtipomovto"], row["tipoVenta"], row["fecmovto"], Myconnect, ref ValorCosto, row["ClaMovto"], row["subtotal"], row["costo"]); // ERROR: CS1503, CS1620
                        break;
                    case "N":
                        // GrabaExistencia(row["idproducto"], row["idubicacion"], row["idbodega"], row["cantidad"], row["Vlrunidad"], row["idtipomovto"], row["tipoVenta"], row["fecmovto"], Myconnect, ref ValorCosto, row["ClaMovto"], row["subtotal"], row["estado"]); // ERROR: CS1503, CS1620
                        //                stmysql = "update inv_movtos set costo=" & ValorCosto & " where idtipomovto=" & .Item("idtipomovto") & " and secuencia=" & .Item("secuencia") & " and idproducto=" & .Item("idproducto") & " and cantidad=" & .Item("cantidad") & " and fecsystem='" & Format(.Item("fecsystem"), varini.pstForfecyHora) & "'"
                        //               Me.MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "ActualizaCosto")
                        break;
                }

                msgbarra.PerformStep();
                fila = fila + 1;
            }

            msgbarra.Close();
            msgbarra.Dispose();
        }

        public void ImprimeConsolInvFisico(string periodo, DataSet DsDataset, System.Windows.Forms.Form myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.reporte Informe = new ERP.Core.Compartido.Reportes.reporte("inv_invfisico01");
            ERP.Core.Compartido.Reportes.config_report Imprime = new ERP.Core.Compartido.Reportes.config_report();
            string nitcomp = " ", diremp = " ", nomemp = " ", telemp = " ";
            string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "", _p11 = "", _p12 = "", _p13 = "", _p14 = "", _p15 = "", _p16 = "";
            // this.msgsys.BuscarCompania(varini.sptCodEmpr, myconnect, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref nitcomp, ref diremp, ref _p14, ref _p15, ref _p16, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref nomemp, ref telemp); // ERROR: CS7036
            Informe.SetDataSource(DsDataset.Tables["tblinforme"]);
            Informe.SetParameterValue("empresa", nomemp);
            Informe.SetParameterValue("nit", nitcomp);
            Informe.SetParameterValue("direccion", diremp);
            Informe.SetParameterValue("telefono", telemp);
            Informe.SetParameterValue("periodo", periodo);
            Imprime.confi_reportes(myforma, Informe);
        }

        public void ActulizacionLotesCierreCosto(DateTime Fecini, DateTime Fecfin, System.Windows.Forms.Form Myforma, System.Data.Odbc.OdbcConnection Myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;

            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando informacion !!!", Myforma);

            stmysql = "update inv_ctrlinv set cantcompra = 0, Cantvendida = 0,costo = costoinicial, ultcostopro = costoinicial,CantFinal=CantInicial where idperiodo = '" + Strings.Format(Fecini, "yyyyMM") + "'";
            this.MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "ActulizacionLotesCierreCosto");

            StBuilder.Append("select movtos.idproducto,movtos.cantidad,movtos.fecmovto,movtos.tipoVenta,movtos.ClaMovto,movtos.Vlrunidad,movtos.idubicacion,movtos.idbodega,");
            StBuilder.Append("movtos.idtipomovto,movtos.subtotal,docs.factura,docs.IdUsuario,docs.idpunto,docs.idturno,movtos.tasaiva,movtos.pos,docs.Estado,tipmov.devolucion,movtos.costo ");
            StBuilder.Append("from inv_movtos movtos ");
            StBuilder.Append("inner join inv_docs docs on movtos.idtipomovto=docs.idtipomovto and movtos.Secuencia=docs.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tipmov on movtos.idtipomovto=tipmov.idtipomovto ");
            StBuilder.Append("where movtos.fecmovto between '" + Strings.Format(Fecini, varini.PstForFec) + "' and '" + Strings.Format(Fecfin, varini.PstForFec) + "' ");
            StBuilder.Append("order by movtos.fecmovto,movtos.fecsystem,movtos.consecmovto");

            this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), Myconnect, "ActulizacionLotesCierreCosto", ref DsDataSet, "tblMovtos");

            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblMovtos"].Rows.Count);
            msgbarra.Show();

            while (fila < DsDataSet.Tables["tblMovtos"].Rows.Count)
            {
                DataRow row = DsDataSet.Tables["tblMovtos"].Rows[(int)fila];
                switch (row["devolucion"].ToString())
                {
                    case "N":
                        GrabaExistenciaCierreCosto(row["idproducto"], row["idubicacion"], row["idbodega"], row["cantidad"], row["Vlrunidad"], row["idtipomovto"], row["tipoVenta"], row["fecmovto"],
                                            Myconnect, 0, row["ClaMovto"], row["subtotal"], row["factura"], row["idpunto"], row["idturno"], row["IdUsuario"], row["tasaiva"], row["pos"], row["Estado"]);
                        break;
                    case "Y":
                        // this.GrabaExistenciaDevolucionCierreCosto(row["idproducto"], row["idubicacion"], row["idbodega"], row["cantidad"], row["Vlrunidad"], row["idtipomovto"], row["tipoVenta"], row["fecmovto"], // ERROR: CS1503, CS1620
                                            // Myconnect, 0, row["ClaMovto"], row["subtotal"], row["costo"], row["factura"], row["idpunto"], row["idturno"], row["IdUsuario"], row["tasaiva"], row["pos"], row["Estado"]); // ERROR: CS1503, CS1620
                        break;
                }

                msgbarra.PerformStep();
                fila = fila + 1;
            }

            msgbarra.Close();
            msgbarra.Dispose();
        }

        // Commented-out old version of GrabaExistenciaCierreCosto (lines 2959-3031)
        //Function GrabaExistenciaCierreCosto(ByVal IdProducto As Double, ByRef Cantidad As Integer, ByVal ValorUnidad As Double, ...
        // ... kept as-is in VB, omitted here for brevity ...

        public virtual string GrabaExistenciaCierreCosto(object IdProducto, object Cencosto, object IdBodega, object CantidadObj, object ValorUnidadObj, object IdTipoMovtoObj, object TipoVenta, object FecmovtoObj, System.Data.Odbc.OdbcConnection myconnect,
            object VlrCostoObj, object TipoTran, object VlrSubtotalObj, object FacturaObj, object IdPuntoObj, object IdTurnoObj,
            object IdUsuarioObj, object TasaIvaObj, object MovtoPosObj, object EstadoObj)
        {
            double _IdProducto = Convert.ToDouble(IdProducto);
            string _Cencosto = Convert.ToString(Cencosto);
            double _IdBodega = Convert.ToDouble(IdBodega);
            double Cantidad = Convert.ToDouble(CantidadObj);
            double ValorUnidad = Convert.ToDouble(ValorUnidadObj);
            double IdTipoMovto = Convert.ToDouble(IdTipoMovtoObj);
            string _TipoVenta = Convert.ToString(TipoVenta);
            DateTime Fecmovto = Convert.ToDateTime(FecmovtoObj);
            double VlrCosto = Convert.ToDouble(VlrCostoObj);
            string _TipoTran = Convert.ToString(TipoTran);
            double VlrSubtotal = Convert.ToDouble(VlrSubtotalObj);
            double Factura = Convert.ToDouble(FacturaObj);
            int IdPunto = Convert.ToInt32(IdPuntoObj);
            int IdTurno = Convert.ToInt32(IdTurnoObj);
            string IdUsuario = Convert.ToString(IdUsuarioObj);
            double TasaIva = Convert.ToDouble(TasaIvaObj);
            string MovtoPos = Convert.ToString(MovtoPosObj);
            string Estado = Convert.ToString(EstadoObj);

            return GrabaExistenciaCierreCostoCore(_IdProducto, _Cencosto, _IdBodega, ref Cantidad, ValorUnidad, IdTipoMovto, _TipoVenta, Fecmovto, myconnect,
                ref VlrCosto, _TipoTran, ref VlrSubtotal, Factura, IdPunto, IdTurno, IdUsuario, TasaIva, MovtoPos, Estado);
        }

        public virtual string GrabaExistenciaCierreCosto(double IdProducto, string Cencosto, double IdBodega, ref double Cantidad, double ValorUnidad, double IdTipoMovto, string TipoVenta, DateTime Fecmovto, System.Data.Odbc.OdbcConnection myconnect,
            ref double VlrCosto, string TipoTran, ref double VlrSubtotal, double Factura, int IdPunto, int IdTurno,
            string IdUsuario, double TasaIva, string MovtoPos, string Estado)
        {
            return GrabaExistenciaCierreCostoCore(IdProducto, Cencosto, IdBodega, ref Cantidad, ValorUnidad, IdTipoMovto, TipoVenta, Fecmovto, myconnect,
                ref VlrCosto, TipoTran, ref VlrSubtotal, Factura, IdPunto, IdTurno, IdUsuario, TasaIva, MovtoPos, Estado);
        }

        private string GrabaExistenciaCierreCostoCore(double IdProducto, string Cencosto, double IdBodega, ref double Cantidad, double ValorUnidad, double IdTipoMovto, string TipoVenta, DateTime Fecmovto, System.Data.Odbc.OdbcConnection myconnect,
            ref double VlrCosto, string TipoTran, ref double VlrSubtotal, double Factura, int IdPunto, int IdTurno,
            string IdUsuario, double TasaIva, string MovtoPos, string Estado)
        {
            int CtrlExistencia = 0;
            double NewCosto = 0, CostoAnterior = 0;
            double CantInicial = 0, CantCompra = 0, cantvendida = 0, Cantfinal = 0, costo = 0, UltCosto = 0;
            double NumCant = 0;
            string ClaseTran = " ", CpteCosto = "9999";
            double CostoInicial = 0, VlrCostoVenta = 0;
            string IdGrupo = "9999", Costea = "N";

            string _t1 = "", _t2 = "", _t3 = "", _t4 = "";
            int _i1 = 0;
            // msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, ref _t1, ref _t2, ref _t3, ref CpteCosto, ref _t4, ref CtrlExistencia, ref _t1, ref _i1, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref Costea); // ERROR: CS7036
            string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "";
            int _claIva = 0;
            decimal _tasaIva = 0;
            // msginvconf.BuscaProductos(IdProducto, myconnect, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _claIva, ref _tasaIva, // ERROR: CS7036
                // ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref IdGrupo, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref _p1, ref NumCant); // ERROR: CS7036

            switch (TipoVenta)
            {
                case "P":
                    ValorUnidad = (ValorUnidad * Cantidad) / (Cantidad * NumCant);
                    Cantidad *= NumCant;
                    break;
            }

            this.BuscaInventario(IdProducto, Strings.Format(Fecmovto, "yyyyMM"), Cencosto, IdBodega, myconnect, ref CantInicial, ref CantCompra, ref cantvendida, ref Cantfinal, ref costo, ref UltCosto, ref CostoInicial);

            switch (CtrlExistencia)
            {
                case 0:
                    switch (Costea)
                    {
                        case "Y":
                            if (Estado.Trim() != "CA")
                            {
                                if (TipoVenta != "P")
                                {
                                    if (Cantidad != 0)
                                    {
                                        ValorUnidad = VlrSubtotal / Cantidad;
                                    }
                                }
                                NewCosto = CalculaCosotoProducto(IdProducto, Cantidad, ValorUnidad, myconnect, costo, Cantfinal);
                            }
                            else
                            {
                                NewCosto = costo;
                            }
                            break;
                        case "N":
                            NewCosto = costo;
                            costo = UltCosto;
                            break;
                    }

                    this.GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "C";
                    costo = NewCosto;
                    break;
                case 1:
                    GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "V";

                    if (Estado.Trim() != "CA")
                    {
                        if (CpteCosto != "9999")
                        {
                            VlrCostoVenta = costo * Cantidad;
                            this.GrabaDatosTempCostos(IdGrupo, (int)IdTipoMovto, Fecmovto, Factura, IdPunto, IdTurno, IdUsuario, TasaIva, VlrCostoVenta, MovtoPos, Cencosto, IdBodega, myconnect);
                        }
                    }
                    break;
                case 2:
                    if (TipoTran != "")
                    {
                        if (TipoTran == "C")
                        {
                            GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                        }
                        else
                        {
                            GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                        }
                    }
                    else
                    {
                        if (Cantidad < 0)
                        {
                            Cantidad = Cantidad * -1;
                            GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "C";
                        }
                        else
                        {
                            GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "V";
                        }
                    }
                    break;
                case 3:
                    if (TipoTran != "")
                    {
                        switch (TipoTran)
                        {
                            case "C":
                                if (Cantidad != 0)
                                {
                                    ValorUnidad = VlrSubtotal / Cantidad;
                                }
                                NewCosto = CalculaCosotoProducto(IdProducto, Cantidad, ValorUnidad, myconnect, costo, Cantfinal);
                                GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                                costo = NewCosto;
                                break;
                            default:
                                GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                                break;
                        }
                    }
                    else
                    {
                        if (Cantidad < 0)
                        {
                            Cantidad = Cantidad * -1;
                            if (Cantidad != 0)
                            {
                                ValorUnidad = VlrSubtotal / Cantidad;
                            }

                            NewCosto = CalculaCosotoProducto(IdProducto, Cantidad, ValorUnidad, myconnect, costo, Cantfinal);
                            GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "C";
                            costo = NewCosto;
                        }
                        else
                        {
                            GrabaInventario(IdProducto, Cencosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "V";
                        }
                    }
                    break;
            }
            VlrCosto = costo;
            return ClaseTran;
        }

        private bool EliminaDatosTempCostos(System.Data.Odbc.OdbcConnection Myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();

            StBuilder.Append("delete from inv_tmpcostos ");
            ok = this.MyOdbcConet.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "BuscaDatosTempCostos");
            return ok;
        }

        private bool BuscaDatosTempCostos(string IdGrupo, int IdTipoMovto, DateTime FecMovto, double NumFactura,
            double TasaIva, double IdUbicacion, double IdBodega, System.Data.Odbc.OdbcConnection Myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();

            StBuilder.Append("Select IdGruProducto,IdTipoMovto,FecMovto,NumFactura,Idpunto,Idturno,IdUsuario,TasaIva,idubicacion,idbodega,Costo,MovtoPos ");
            StBuilder.Append("from inv_tmpcostos ");
            StBuilder.Append("where IdGruProducto='" + IdGrupo + "' and idtipomovto=" + IdTipoMovto + " and fecmovto='" + Strings.Format(FecMovto, varini.PstForFec) + "' ");
            StBuilder.Append("and NumFactura=" + NumFactura + " and TasaIva=" + TasaIva + " and idubicacion=" + IdUbicacion + " and idbodega=" + IdBodega);
            ok = this.MyOdbcConet.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "BuscaDatosTempCostos");
            return ok;
        }

        public void GrabaDatosTempCostos(string IdGrupo, int IdTipoMovto, DateTime FecMovto, double NumFactura, int Idpunto,
            int Idturno, string IdUsuario, double TasaIva, double Costo, string MovtoPos,
            string IdUbicacion, double IdBodega, System.Data.Odbc.OdbcConnection Myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();

            ok = this.BuscaDatosTempCostos(IdGrupo, IdTipoMovto, FecMovto, NumFactura, TasaIva, Convert.ToDouble(IdUbicacion), IdBodega, Myconnect);

            switch (ok)
            {
                case false:
                    StBuilder.Append("insert into inv_tmpcostos (IdGruProducto,IdTipoMovto,FecMovto,NumFactura,TasaIva,Idpunto,Idturno,IdUsuario,");
                    StBuilder.Append("Costo,MovtoPos,idubicacion,idbodega) values ('");
                    StBuilder.Append(IdGrupo + "',");
                    StBuilder.Append(IdTipoMovto + ",'");
                    StBuilder.Append(Strings.Format(FecMovto, varini.PstForFec) + "',");
                    StBuilder.Append(NumFactura + ",'");
                    StBuilder.Append(TasaIva + "',");
                    StBuilder.Append(Idpunto + ",");
                    StBuilder.Append(Idturno + ",'");
                    StBuilder.Append(IdUsuario + "','");
                    StBuilder.Append(Costo + "','");
                    StBuilder.Append(MovtoPos + "',");
                    StBuilder.Append(IdUbicacion + ",");
                    StBuilder.Append(IdBodega + ")");
                    break;
                case true:
                    StBuilder.Append("update inv_tmpcostos set Costo = Costo + " + Costo);
                    StBuilder.Append(" where IdGruProducto='" + IdGrupo + "' and idtipomovto=" + IdTipoMovto + " and fecmovto='" + Strings.Format(FecMovto, varini.PstForFec) + "' ");
                    StBuilder.Append("and NumFactura=" + NumFactura + " and TasaIva=" + TasaIva + " and idubicacion=" + IdUbicacion + " and idbodega=" + IdBodega);
                    break;
            }
            this.MyOdbcConet.ExecuteQueryconec(StBuilder.ToString(), Myconnect, "GrabaDatosTempCostos");
        }

        public bool OrganizaDatosCierreCosto(string IdMovto, DateTime fecini, DateTime fecfin, string Usuario, System.Windows.Forms.Form Myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            EliminaDatosTempCostos(myconnect);
            this.ActulizacionLotesCierreCosto(fecini, fecfin, Myforma, myconnect);
            GrabaCierreCostos(IdMovto, fecini, fecfin, Usuario, myconnect, Myforma);
            return ok;
        }

        public bool GrabaCierreCostos(string TipoMovto, DateTime fechaInicial, DateTime fechaFinal, string UsuarioCierre,
            System.Data.Odbc.OdbcConnection myconect, System.Windows.Forms.Form Myforma)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfigLocal = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntLocal = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999";
            string ConseCpte = Convert.ToDouble(Strings.Format(fechaFinal, "yyMMdd")).ToString();
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999";
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            double Vlrcosto = 0;
            int canreg = 0, fila = 0;
            string StWhere = "", cerrado = "N";
            int sw1 = 0;

            //Select Case DesdePos
            //    Case True
            //        StWhere = " and costo.pos='Y' "
            //    Case False
            //        StWhere = " and costo.pos<>'Y' "
            //End Select

            switch (TipoMovto.Trim())
            {
                case "Todos":
                    StWhere = "";
                    break;
                default:
                    StWhere = " and costo.idtipomovto = " + TipoMovto;
                    break;
            }

            StBuilder.Append("select costo.IdGruProducto,costo.IdTipoMovto,costo.FecMovto,costo.NumFactura,costo.Idpunto,costo.Idturno,");
            StBuilder.Append("costo.IdUsuario,costo.TasaIva,costo.Costo,costo.MovtoPos,costo.idubicacion,costo.idbodega ");
            StBuilder.Append("from inv_tmpcostos costo ");
            StBuilder.Append("inner join inv_tipomovtos tipomov on costo.idtipomovto = tipomov.idtipomovto ");
            StBuilder.Append("inner join inv_facturas fac on tipomov.ctrlfactura = fac.IdCodigo ");
            StBuilder.Append("where tipomov.CtrlExistencia = '1' and fac.actcostos=0 " + StWhere);
            StBuilder.Append(" and costo.FecMovto between '" + Strings.Format(fechaInicial, varini.PstForFec) + "' and '" + Strings.Format(fechaFinal, varini.PstForFec) + "' ");
            StBuilder.Append("order by numfactura");

            //.DefineMaximo(StBuilder.ToString, myconect)
            msgbarra.Show();

            System.Windows.Forms.Application.DoEvents();

            //Dim mycommand As New Odbc.OdbcDataAdapter(StBuilder.ToString, myconect)
            //Dim myread As New DataSet("Inventario")
            //canreg = mycommand.Fill(myread, "TblCierreCostos")
            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconect, "GrabaCierreCostos", ref myRead, "TblCierreCostos");
            canreg = myRead.Tables["TblCierreCostos"].Rows.Count;

            msgbarra.ValorMinimoMaximo(0, canreg);

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCierreCostos"].Rows[fila];
                System.Windows.Forms.Application.DoEvents();
                sw1 = 0;
                string _n1 = "";
                // msginvconfigLocal.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], row["IdTipomovto"], myconect, ref _n1, ref _n1, ref _n1, ref _n1, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto); // ERROR: CS1503, CS1620
                string _t1 = "", _t2 = "";
                // msginvconfigLocal.BuscaTipomovto(row["IdTipoMovto"], myconect, ref _t1, ref _t2, ref _t1, ref CpteCost); // ERROR: CS7036
                if (CpteCost == "9999")
                {
                    MessageBox.Show("Comprobante Costos de Tipo Movimiento " + row["IdTipomovto"] + " no esta parametrizado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                }

                switch (fila)
                {
                    case 0:
                        ConseCpte = row["NumFactura"].ToString();
                        break;
                }

                if (Convert.ToDouble(row["NumFactura"]) != Convert.ToDouble(ConseCpte))
                {
                    string _cerr = "";
                    // ok = msgcntLocal.BuscaComprobante(CpteCost, ConseCpte, false, myconect, ref _cerr, ref _cerr, ref _cerr, ref _cerr, ref cerrado); // ERROR: CS7036
                    if (ok)
                    {
                        if (cerrado != "Y")
                        {
                            // ok = msgcntLocal.CierreDocumento(CpteCost, ConseCpte, myconect); // ERROR: CS1503
                        }
                    }
                    ConseCpte = row["NumFactura"].ToString();
                }

                string _c1 = "";
                // ok = msgcntLocal.BuscaComprobante(CpteCost, ConseCpte, false, myconect, ref _c1, ref _c1, ref _c1, ref _c1, ref cerrado); // ERROR: CS7036
                if (ok)
                {
                    if (cerrado == "Y")
                    {
                        sw1 = 1;
                    }
                }

                switch (sw1)
                {
                    case 0:
                        Vlrcosto = Convert.ToDouble(row["Costo"]);

                        if (Vlrcosto > 0)
                        {
                            if (Convert.ToDouble(row["TasaIva"]) > 0)
                            {
                                // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, CostoGravado, "9999", Strings.Format(Convert.ToDateTime(row["FecMovto"]), "yyyyMM"), "99999999999999", Convert.ToDateTime(row["FecMovto"]), "Cierre de costos en lotes del " + fechaInicial.ToString("dd-MM-yyyy") + " al " + fechaFinal.ToString("dd-MM-yyyy"), " ", row["Costo"], 0, 0, UsuarioCierre, myconect); // ERROR: CS1503
                                // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, InvGravado, "9999", Strings.Format(Convert.ToDateTime(row["FecMovto"]), "yyyyMM"), "99999999999999", Convert.ToDateTime(row["FecMovto"]), "Cierre de costos en lotes del " + fechaInicial.ToString("dd-MM-yyyy") + " al " + fechaFinal.ToString("dd-MM-yyyy"), " ", 0, row["Costo"], 0, UsuarioCierre, myconect); // ERROR: CS1503
                            }
                            else
                            {
                                // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, CostoNoGravado, "9999", Strings.Format(Convert.ToDateTime(row["FecMovto"]), "yyyyMM"), "99999999999999", Convert.ToDateTime(row["FecMovto"]), "Cierre de costos en lotes del " + fechaInicial.ToString("dd-MM-yyyy") + " al " + fechaFinal.ToString("dd-MM-yyyy"), " ", row["Costo"], 0, 0, UsuarioCierre, myconect); // ERROR: CS1503
                                // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, InvNoGravado, "9999", Strings.Format(Convert.ToDateTime(row["FecMovto"]), "yyyyMM"), "99999999999999", Convert.ToDateTime(row["FecMovto"]), "Cierre de costos en lotes del " + fechaInicial.ToString("dd-MM-yyyy") + " al " + fechaFinal.ToString("dd-MM-yyyy"), " ", 0, row["Costo"], 0, UsuarioCierre, myconect); // ERROR: CS1503
                            }
                        }
                        break;
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            // ok = msgcntLocal.CierreDocumento(CpteCost, ConseCpte, myconect); // ERROR: CS1503
            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        public bool CierreCostosLinea(int IdMovto, double Secuencia, int Idpunto, int idturno, DateTime fecha, string idUsuario, string UsuarioCierre, System.Data.Odbc.OdbcConnection myconect, System.Windows.Forms.Form Myforma,
            ref string Cpte, ref double Consecutivo, bool DesdePos)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfigLocal = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntLocal = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999";
            string ConseCpte = Convert.ToDouble(Idpunto.ToString() + idturno.ToString() + Strings.Format(fecha, "yyMMdd")).ToString();
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999";
            double Vlrcosto = 0;
            int canreg = 0, fila = 0;
            string StWhere = "";
            double factura = 0;
            string IdCliente = "99999999999999";

            switch (DesdePos)
            {
                case true:
                    StWhere = " and costo.pos='Y' ";
                    break;
                case false:
                    StWhere = " and costo.pos<>'Y' ";
                    break;
            }

            stmysql = " select idgrupo,costo.IdTipomovto, TasaIva, CostoPro,tipomov.ctrlfactura,costo.idubicacion,costo.idbodega "
                + "from inv_grucostopro02_vw costo inner join inv_tipomovtos tipomov on costo.idtipomovto = tipomov.idtipomovto "
                + " inner join inv_facturas fac on tipomov.ctrlfactura = fac.IdCodigo "
                + "where costo.IdTipomovto=" + IdMovto + " and costo.secuencia=" + Secuencia + " and tipomov.CtrlExistencia='1' and fac.actcostos=1 " + StWhere;

            //& " and IdPunto = '" & Idpunto & "' and  IdTurno = '" & idturno & "' and tipomov.CtrlExistencia = '1' and fac.actcostos=1 " _
            //                & " and FecMovto = '" & Format(fecha, varini.PstForFec) & "' and IdUsuario = '" & idUsuario & "' " & StWhere

            //With msgbarra
            //    .DefineMaximo(stmysql, myconect)
            //    .Show()
            //End With

            //Application.DoEvents()

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "CierreCostosLinea", ref myRead, "TblCierreCostos");
            canreg = myRead.Tables["TblCierreCostos"].Rows.Count;

            System.Windows.Forms.Application.DoEvents();

            msgbarra.ValorMinimoMaximo(0, canreg);
            msgbarra.Show();

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCierreCostos"].Rows[fila];
                System.Windows.Forms.Application.DoEvents();
                string _n1 = "";
                // msginvconfigLocal.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["idgrupo"], row["IdTipomovto"], myconect, ref _n1, ref _n1, ref _n1, ref _n1, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto); // ERROR: CS1503, CS1620
                string _t1 = "", _t2 = "";
                // msginvconfigLocal.BuscaTipomovto(row["IdTipomovto"], myconect, ref _t1, ref _t2, ref _t1, ref CpteCost); // ERROR: CS7036
                if (CpteCost == "9999")
                {
                    MessageBox.Show("Comprobante Costos de Tipo Movimiento " + row["IdTipomovto"] + " no esta parametrizado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                }
                string _ic = "";
                double _ip = 0, _it = 0;
                string _fm = "", _iu = "";
                double _ef = 0, _td = 0, _tc = 0, _ch = 0, _cu = 0, _vt = 0;
                double _vd = 0, _vi = 0;
                string _det = "";
                double _st = 0;
                int _cp = 0;
                // this.BuscaTransaccion(IdMovto, Secuencia, myconect, ref IdCliente, ref _ic, ref _ic, ref _ic, ref _ic, ref _ef, ref _td, ref _tc, ref _ch, ref _cu, ref _vt, ref factura); // ERROR: CS1501

                if (factura <= 0)
                {
                    //factura = Secuencia
                    factura = Convert.ToDouble(Idpunto.ToString() + idturno.ToString() + IdMovto.ToString() + Secuencia.ToString());
                }

                ConseCpte = factura.ToString();
                Vlrcosto = Convert.ToDouble(row["CostoPro"]);

                if (Vlrcosto > 0)
                {
                    if (Convert.ToDouble(row["TasaIva"]) > 0)
                    {
                        if (CostoGravado != "999999999999" && InvGravado != "999999999999")
                        {
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, CostoGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", row["CostoPro"], 0, 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", 0, row["CostoPro"], 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                        }
                    }
                    else
                    {
                        if (CostoNoGravado != "999999999999" && InvNoGravado != "999999999999")
                        {
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, CostoNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", row["CostoPro"], 0, 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", 0, row["CostoPro"], 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                        }
                    }
                }
                fila += 1;
                msgbarra.PerformStep();
            }
            Cpte = CpteCost;
            Consecutivo = Convert.ToDouble(ConseCpte);
            // ok = msgcntLocal.CierreDocumento(CpteCost, ConseCpte, myconect); // ERROR: CS1503
            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        public bool ActualizaContabilidadAnulacion(int IdTipomovto, double Secuencia, int IdTipoMovtoAnulacion, DateTime fecha, string Usuario, System.Data.Odbc.OdbcConnection myconect, System.Windows.Forms.Form Myforma, bool DesdeDevolucion, double ConseAnulacion)
        {
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntLocal = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            DataSet dsdatosDocs = new DataSet();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999", CtrlFactura = "9999";
            int ActCosto = 0;
            string ConseCpte;
            int canreg = 0, fila = 0;
            string StCuentaIva = "";
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999", CuentaRetFte = "999999999999", CtaIca = "999999999999";
            int CtrlExistencia = 0;
            string Cptecartera = "9999", CuentaCreditos = "99999999999999", cencos = "99999999";
            string StConsulta = "", StConsCampo = "", StDevVentaNoGravada = "999999999999", StDevVentaGravada = "999999999999", StNumDocCruce = "";
            int idpuntoDev = 0, idturnoDev = 0;

            string _t1 = "", _t2 = "", _t3 = "";
            int _i1 = 0;
            switch (DesdeDevolucion)
            {
                case false:
                    // this.msginvconf.BuscaTipomovto(IdTipomovto, myconect, ref _t1, ref _t2, ref _t3, ref _t1, ref _t1, ref CtrlExistencia, ref _t1, ref _i1, ref _t1, ref _t1, ref CtrlFactura); // ERROR: CS7036
                    // this.msginvconf.BuscaTipomovto(IdTipoMovtoAnulacion, myconect, ref _t1, ref _t2, ref CpteTran, ref CpteCost); // ERROR: CS7036
                    break;
                case true:
                    string _idpDev = idpuntoDev.ToString(), _itDev = idturnoDev.ToString();
                    // this.BuscaTransaccion(IdTipoMovtoAnulacion, ConseAnulacion, myconect, ref _t1, ref _idpDev, ref _itDev); // ERROR: CS1501
                    int.TryParse(_idpDev, out idpuntoDev);
                    int.TryParse(_itDev, out idturnoDev);
                    // this.msginvconf.BuscaTipomovto(IdTipoMovtoAnulacion, myconect, ref _t1, ref _t2, ref _t3, ref _t1, ref _t1, ref CtrlExistencia, ref _t1, ref _i1, ref _t1, ref _t1, ref CtrlFactura); // ERROR: CS7036
                    // this.msginvconf.BuscaTipomovto(IdTipomovto, myconect, ref _t1, ref _t2, ref CpteTran, ref CpteCost); // ERROR: CS7036

                    StConsulta = " left join inv_precios prec on invpro.IdProducto=prec.IdProducto and invpro.IdTipoPrecio=prec.IdTipoPrecio And TipoCliente = 2 ";
                    StConsCampo = ",SUM(invmovto.Costo * case invmovto.TipoVenta when 'P' then (prec.cantidad * invmovto.Cantidad) else invmovto.Cantidad end) AS CostoPro ";
                    break;
            }

            switch (CtrlExistencia)
            {
                case 3:
                    stmysql = "select IdGruProducto,invdoc.factura,clapago,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,invmovto.ClaMovto,invpro.tasaiva,sum(invmovto.cantidad) as cantidad, sum(invmovto.vlrIva) as Iva ,sum(invmovto.Vlrdsto) as dsto,"
                                + "sum(invmovto.vlrUnidad) as VlrUnidad,sum(invmovto.subtotal) as  Subtotal,sum(invmovto.neto) as Neto, sum(invmovto.VlrRetfte) as VlrRetfte,sum(invmovto.Vlrica) as VlrIca,"
                                + "sum(CASE WHEN invmovto.vlrIva <> 0 THEN invmovto.subtotal ELSE 0 END)as Vlr_base,sum(CASE WHEN invmovto.VlrRetfte <> 0 THEN invmovto.subtotal ELSE 0 END)as VlrBaseRet, "
                                + "sum(CASE WHEN invmovto.Vlrica <> 0 THEN invmovto.subtotal ELSE 0 END)as VlrBaseIca,invmovto.IdPunto,invmovto.IdTurno "
                                + "from inv_movtos invmovto inner join inv_docs invdoc on invmovto.idtipomovto = invdoc.idtipomovto "
                                + "and invmovto.secuencia = invdoc.secuencia inner join inv_productos invpro on invmovto.idproducto = invpro.idproducto "
                                + "where invmovto.idtipoMovto = " + IdTipomovto + " And invmovto.secuencia = " + Secuencia
                                + " group by IdGruProducto,invdoc.factura,clapago,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,invpro.tasaiva,invmovto.ClaMovto,invmovto.IdPunto,invmovto.IdTurno";
                    break;
                default:
                    stmysql = "select IdGruProducto,invdoc.factura,clapago,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,invmovto.TasaIva,sum(invmovto.cantidad) as cantidad, sum(invmovto.vlrIva) as Iva ,sum(invmovto.Vlrdsto) as dsto,"
                                + "sum(invmovto.vlrUnidad) as VlrUnidad,sum(invmovto.subtotal) as  Subtotal,sum(invmovto.neto) as Neto, sum(invmovto.VlrRetfte) as VlrRetfte, sum(invmovto.Vlrica) as VlrIca,"
                                + "sum(CASE WHEN invmovto.vlrIva <> 0 THEN invmovto.subtotal ELSE 0 END)as Vlr_base,sum(CASE WHEN invmovto.VlrRetfte <> 0 THEN invmovto.subtotal ELSE 0 END)as VlrBaseRet "
                                + ",sum(CASE WHEN invmovto.Vlrica <> 0 THEN invmovto.subtotal ELSE 0 END)as VlrBaseIca,invmovto.IdPunto,invmovto.IdTurno " + StConsCampo
                                + "from inv_movtos invmovto inner join inv_docs invdoc on invmovto.idtipomovto = invdoc.idtipomovto "
                                + "and invmovto.secuencia = invdoc.secuencia inner join inv_productos invpro on invmovto.idproducto = invpro.idproducto "
                                + StConsulta + " where invmovto.idtipoMovto = " + IdTipomovto + " And invmovto.secuencia = " + Secuencia
                                + " group by IdGruProducto,invdoc.factura,clapago,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,invmovto.TasaIva,invmovto.IdPunto,invmovto.IdTurno";
                    break;
            }

            //With msgbarra
            //    .DefineMaximo(stmysql, myconect)
            //    .Show()
            //End With

            System.Windows.Forms.Application.DoEvents();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "ActualizaContabilidadAnulacion", ref myRead, "TblActContab");
            canreg = myRead.Tables["TblActContab"].Rows.Count;

            msgbarra.ValorMinimoMaximo(0, canreg);
            msgbarra.Show();

            ConseCpte = "0";

            string _cc1 = "";
            // msgcntLocal.BuscaComprobante(CpteTran, 0, false, myconect, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref _cc1, ref CuentaCreditos); // ERROR: CS7036
            string _f1 = "";
            int _actCosto = 0;
            // this.msginvconf.BuscaDatosFacturacion(CtrlFactura, myconect, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref _f1, ref ActCosto); // ERROR: CS7036

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblActContab"].Rows[fila];
                System.Windows.Forms.Application.DoEvents();

                StCuentaIva = "";

                string _g1 = "", _g2 = "", _g3 = "";
                // this.msginvconf.BuscaGrupo(row["IdGruProducto"], myconect, ref _g1, ref _g2, ref _g3, ref cencos); // ERROR: CS7036
                switch (DesdeDevolucion)
                {
                    case false:
                        // this.msginvconf.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, myconect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto, ref CuentaRetFte, ref CtaIca); // ERROR: CS7036
                        break;
                    case true:
                        // this.msginvconf.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipoMovtoAnulacion, myconect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto, ref CuentaRetFte, ref CtaIca); // ERROR: CS7036
                        string _dv1 = "", _dv2 = "";
                        // this.msginvconf.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, myconect, ref _dv1, ref _dv2, ref StDevVentaGravada, ref StDevVentaNoGravada); // ERROR: CS1501
                        if (StDevVentaGravada != "999999999999")
                        {
                            VentasGrabadas = StDevVentaGravada;
                        }
                        if (StDevVentaNoGravada != "999999999999")
                        {
                            ventasNoGrabadas = StDevVentaNoGravada;
                        }
                        break;
                }

                if (Convert.ToDouble(ConseCpte) == 0)
                {
                    switch (DesdeDevolucion)
                    {
                        case false:
                            ConseCpte = row["IdPunto"].ToString() + row["IdTurno"].ToString() + IdTipoMovtoAnulacion.ToString() + ConseAnulacion.ToString();
                            break;
                        case true:
                            ConseCpte = row["IdPunto"].ToString() + row["IdTurno"].ToString() + IdTipomovto.ToString() + Secuencia.ToString();
                            break;
                    }

                    if (Information.IsNumeric(row["factura"]))
                    {
                        if (Convert.ToDouble(row["factura"]) != 0)
                        {
                            StNumDocCruce = row["factura"].ToString();
                        }
                        else
                        {
                            switch (DesdeDevolucion)
                            {
                                case false:
                                    StNumDocCruce = row["IdPunto"].ToString() + row["IdTurno"].ToString() + IdTipomovto.ToString() + Secuencia.ToString();
                                    break;
                                case true:
                                    StNumDocCruce = idpuntoDev.ToString() + idturnoDev.ToString() + IdTipoMovtoAnulacion.ToString() + ConseAnulacion.ToString();
                                    break;
                            }
                        }
                    }
                }

                if (CpteTran == "9999")
                {
                    MessageBox.Show("Comprobante de la linea " + row["IdTipomovto"] + " no esta parametrizado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                switch (CtrlExistencia)
                {
                    case 1:
                        if (Convert.ToInt32(row["clapago"]) == 3)
                        {
                            if (CuentaCreditos == "999999999999")
                            {
                                MessageBox.Show("Cuenta del comprobante, no esta parametrizada");
                                return false;
                            }
                        }
                        break;
                }

                string detalle = Strings.Mid(row["detalle"].ToString(), 1, 80);

                switch (CtrlExistencia)
                {
                    case 0:
                        if (Convert.ToDouble(row["neto"]) > 0)
                        {
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, StNumDocCruce, row["neto"], 0, 0, Usuario, myconect, "", "UC-" + StNumDocCruce, row["idcliente"], cencos, "", "", "POST", "", "UC", fecha); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Iva"]) > 0)
                        {
                            switch (DesdeDevolucion)
                            {
                                case false:
                                    // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                                    break;
                                case true:
                                    // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipoMovtoAnulacion, row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                                    break;
                            }
                            if (ok)
                            {
                                if (StCuentaIva.Trim() != "" && StCuentaIva.Trim() != "999999999999")
                                {
                                    CuentaIva = StCuentaIva.Trim();
                                }
                            }
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, CuentaIva, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["Iva"], row["Vlr_base"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Dsto"]) > 0)
                        {
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, Cuentadsto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", row["Dsto"], 0, row["Dsto"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["Subtotal"], row["Subtotal"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) == 0)
                        {
                            if (Convert.ToDouble(row["Dsto"]) > 0)
                            {
                                // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["Subtotal"], row["Subtotal"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                            }
                            else
                            {
                                // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["neto"], row["neto"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                            }
                        }
                        break;
                    case 1:
                        switch (Convert.ToInt32(row["clapago"]))
                        {
                            case 0:
                                if (Convert.ToDouble(row["neto"]) > 0)
                                {
                                    // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, StNumDocCruce, 0, row["neto"], 0, Usuario, myconect, "", "UC-" + StNumDocCruce, row["idcliente"], cencos, "", "", "POST", "", "UC", fecha); // ERROR: CS1503
                                }
                                break;
                            case 3:
                                if (Convert.ToDouble(row["neto"]) > 0)
                                {
                                    // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, CuentaCreditos, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, StNumDocCruce, 0, row["neto"], 0, Usuario, myconect, "", "UC-" + StNumDocCruce, row["idcliente"], cencos, "", "", "POST", "", "UC", fecha); // ERROR: CS1503
                                }
                                break;
                        }

                        if (Convert.ToDouble(row["Iva"]) > 0)
                        {
                            switch (DesdeDevolucion)
                            {
                                case false:
                                    // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                                    break;
                                case true:
                                    // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipoMovtoAnulacion, row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                                    break;
                            }
                            if (ok)
                            {
                                if (StCuentaIva.Trim() != "" && StCuentaIva.Trim() != "999999999999")
                                {
                                    CuentaIva = StCuentaIva.Trim();
                                }
                            }
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, CuentaIva, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", row["Iva"], 0, Convert.ToDouble(row["Vlr_base"]) * -1, Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Dsto"]) > 0)
                        {
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, Cuentadsto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["Dsto"], row["Dsto"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, VentasGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", row["Subtotal"], 0, row["Subtotal"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) == 0)
                        {
                            if (Convert.ToDouble(row["Dsto"]) > 0)
                            {
                                // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, ventasNoGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", row["Subtotal"], 0, row["Subtotal"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                            }
                            else
                            {
                                // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, ventasNoGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", row["neto"], 0, row["neto"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                            }
                        }

                        if (Convert.ToDouble(row["VlrRetfte"]) > 0)
                        {
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, CuentaRetFte, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["VlrRetfte"], row["VlrBaseRet"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }
                        if (Convert.ToDouble(row["Vlrica"]) > 0)
                        {
                            // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, CtaIca, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["VlrIca"], row["VlrBaseIca"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                        }

                        if (DesdeDevolucion)
                        {
                            if (ActCosto == 0)
                            {
                                if (Convert.ToDouble(row["iva"]) > 0)
                                {
                                    // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, CostoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["CostoPro"], row["CostoPro"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                    // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", row["CostoPro"], 0, row["CostoPro"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                }
                                else
                                {
                                    // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, CostoNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", 0, row["CostoPro"], row["CostoPro"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                    // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, " ", row["CostoPro"], 0, row["CostoPro"], Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                }
                            }
                        }
                        break;
                    case 3:
                        if (Convert.ToDouble(row["neto"]) > 0)
                        {
                            switch (row["ClaMovto"].ToString())
                            {
                                case "C":
                                    if (Convert.ToDouble(row["tasaiva"]) > 0)
                                    {
                                        // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, ConseCpte, 0, row["neto"], 0, Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                    }
                                    else
                                    {
                                        // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, ConseCpte, 0, row["neto"], 0, Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                    }
                                    break;
                                case "V":
                                    if (Convert.ToDouble(row["tasaiva"]) > 0)
                                    {
                                        // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, ConseCpte, row["neto"], 0, 0, Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                    }
                                    else
                                    {
                                        // msgcntLocal.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, detalle, ConseCpte, row["neto"], 0, 0, Usuario, myconect, "", "", row["idcliente"], cencos, "", "", "POST"); // ERROR: CS1503
                                    }
                                    break;
                            }
                        }
                        break;
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            if (DesdeDevolucion)
            {
                string nit = " ";
                string IdpuntoStr = "0", IdTurnoStr = "0", FechaMov = "0";
                int Clapago = 0;
                string TipoTercero = "0", ClientePatronal = "0";
                double ConseCartera = 0;
                int ActCont = 0;
                string Stdetalle = "", Lincred = "9999", clades = "9999", Plazo = "9999", CptoCap = "02";
                string CuentaCpte = "99999999999999", Clacuo = "9999", ClaseI = "9999";
                decimal TasaInt = 0;
                string Foradmon = "9999";

                string _b1 = "", _b2 = "", _b3 = "", _b4 = "";
                double _d1 = 0, _d2 = 0, _d3 = 0, _d4 = 0, _d5 = 0, _d6 = 0;
                int _clap = 0;
                // this.BuscaTransaccion(IdTipoMovtoAnulacion, ConseAnulacion, myconect, ref nit, ref IdpuntoStr, ref IdTurnoStr, ref FechaMov, ref _b1, ref _d1, ref _d2, ref _d3, ref _d4, ref _d5, ref _d6, ref _d1, ref _d1, ref _d1, ref _b1, ref _d1, ref Clapago); // ERROR: CS1501
                string _neto = "";
                double _d7 = 0;
                // this.BuscaTransaccion(IdTipomovto, Secuencia, myconect, ref _b1, ref _b1, ref _b1, ref _b1, ref _b1, ref _d7, ref _d7, ref _d7, ref _d7, ref _d7, ref _d7, ref _d7, ref _d7, ref _d7, ref _neto); // ERROR: CS1501
                // VB accesses Neto from BuscaTransaccion - use _neto placeholder
                // Neto is already a string variable in scope

                Stdetalle = "Devolucion Modulo Comercial Documento " + IdTipoMovtoAnulacion + " - " + ConseAnulacion;

                switch (Clapago)
                {
                    case 3:
                        string _tt1 = "", _cp1 = "";
                        // msgcntLocal.BuscarTercero(nit, myconect, ref _tt1, ref _tt1, ref _tt1, ref _tt1, ref TipoTercero, ref _tt1, ref ClientePatronal); // ERROR: CS7036
                        string _mf1 = "", _mf2 = "";
                        int _ctrlEx2 = 0;
                        // msginvconf.BuscaTipomovto(IdTipoMovtoAnulacion, myconect, ref _mf1, ref _mf2, ref _mf1, ref _mf1, ref _mf1, ref CtrlExistencia, ref _mf1, ref _i1, ref _mf1, ref _mf1, ref CtrlFactura); // ERROR: CS7036

                        if (CtrlExistencia == 0)
                        {
                            break;
                        }

                        if (Convert.ToInt32(IdTurnoStr) > 0)
                        {
                            switch (TipoTercero)
                            {
                                case "7":
                                    string _df1 = "";
                                    // msginvconf.BuscaDatosFacturacion(CtrlFactura, myconect, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref _df1, ref Cptecartera, ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                                    break;
                                default:
                                    switch (ClientePatronal)
                                    {
                                        case "Y":
                                            string _df2 = "";
                                            // msginvconf.BuscaDatosFacturacion(CtrlFactura, myconect, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref _df2, ref Cptecartera, ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                                            break;
                                        default:
                                            string _df3 = "";
                                            // msginvconf.BuscaDatosFacturacion(CtrlFactura, myconect, ref _df3, ref _df3, ref _df3, ref _df3, ref _df3, ref _df3, ref _df3, ref _df3, ref _df3, ref Cptecartera, ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                                            break;
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            string _df4 = "";
                            // msginvconf.BuscaDatosFacturacion(2, myconect, ref _df4, ref _df4, ref _df4, ref _df4, ref _df4, ref _df4, ref _df4, ref _df4, ref _df4, ref _df4, ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                        }

                        string _sc1 = "";
                        // msgsys.BuscarCompania(msgcop.varini.sptCodEmpr, myconect, ref _sc1, ref CptoCap); // ERROR: CS7036
                        // ok = msgcop.BuscaAsociado(nit, myconect); // ERROR: CS1620
                        switch (ok)
                        {
                            case true:
                                ConseCartera = Convert.ToDouble(IdpuntoStr + IdTurnoStr + Strings.Format(Convert.ToDateTime(fecha), "yyMMdd"));
                                string _bl1 = "";
                                // msgcop.BuscaLinea(Lincred, myconect, ref Clacuo, ref ClaseI, ref _bl1, ref cencos, ref _bl1, ref TasaInt, ref _bl1, ref _bl1, ref Foradmon); // ERROR: CS7036
                                string _bc1 = "";
                                // msgcop.BuscaComprobante(Cptecartera, 0, false, myconect, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref _bc1, ref CuentaCpte); // ERROR: CS1503, CS1620

                                // msgcop.GrabaMovimiento(Cptecartera, ConseCartera, nit, Lincred, ConseAnulacion, Strings.Format(Convert.ToDateTime(fecha), "yyyyMM"), CptoCap, fecha, 0, Neto, Stdetalle, Usuario, myconect, "", "", nit, "", nit); // ERROR: CS1503, CS1620
                                break;
                            case false:
                                MessageBox.Show("Asociado no existe ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return false;
                        }
                        break;
                }

                this.GrabaEstado(IdTipomovto, Secuencia, myconect, "C");
                //Me.GrabaEstado(IdtipoMovtoAnul, ConseAnulacion, myconect, "CA", Stdetalle)
                if (ActCosto == 1)
                {
                    int _actCont2 = 0;
                    string _tm1 = "";
                    // this.msginvconf.BuscaTipomovto(IdTipomovto, myconect, ref _tm1, ref _tm1, ref _tm1, ref _tm1, ref _tm1, ref _i1, ref _tm1, ref _actCont2); // ERROR: CS7036
                    if (_actCont2 == 3)
                    {
                        this.CierreCostosLineaAnulacion(IdTipomovto, Secuencia, IdTipoMovtoAnulacion, fecha, Usuario, myconect, Myforma, DesdeDevolucion, ConseAnulacion);
                    }
                }
            }

            // ok = msgcntLocal.CierreDocumento(CpteTran, ConseCpte, myconect); // ERROR: CS1503
            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        public bool CierreCostosLineaAnulacion(int IdMovto, double Secuencia, int IdMovtoAnulacion, DateTime fecha, string UsuarioCierre, System.Data.Odbc.OdbcConnection myconect, System.Windows.Forms.Form Myforma, bool DesdeDevolucion, double ConseAnulacion)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfigLocal = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntLocal = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999";
            string ConseCpte; //= CDbl(Idpunto & idturno & Format(fecha, "yyMMdd"))
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999";
            double Vlrcosto = 0;
            int canreg = 0, fila = 0;
            string StWhere = "";
            double factura = 0;
            string IdCliente = "99999999999999";
            double IdpuntoLocal = 0, idturnoLocal = 0;

            switch (DesdeDevolucion)
            {
                case false:
                    stmysql = " select idgrupo,costo.IdTipomovto, TasaIva, CostoPro,tipomov.ctrlfactura,costo.idubicacion,costo.idbodega from inv_grucostopro02_vw costo inner join inv_tipomovtos tipomov on costo.idtipomovto = tipomov.idtipomovto "
                              + " inner join inv_facturas fac on tipomov.ctrlfactura = fac.IdCodigo "
                              + "where costo.IdTipomovto=" + IdMovto + " and costo.secuencia=" + Secuencia + " and tipomov.CtrlExistencia = '1' and fac.actcostos=1 ";
                    break;
                case true:
                    stmysql = " select idgrupo,costo.IdTipomovto, TasaIva, CostoPro,tipomov.ctrlfactura,costo.idubicacion,costo.idbodega from inv_grucostopro02_vw costo inner join inv_tipomovtos tipomov on tipomov.idtipomovto = " + IdMovtoAnulacion + " "
                              + " inner join inv_facturas fac on tipomov.ctrlfactura = fac.IdCodigo "
                              + "where costo.IdTipomovto=" + IdMovto + " and costo.secuencia=" + Secuencia + " and tipomov.CtrlExistencia = '1' and fac.actcostos=1 ";
                    break;
            }

            msgbarra.DefineMaximo(stmysql, myconect);
            msgbarra.Show();

            System.Windows.Forms.Application.DoEvents();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "CierreCostosLineaAnulacion", ref myRead, "TblCierreCostos");
            canreg = myRead.Tables["TblCierreCostos"].Rows.Count;

            string _t1 = "", _t2 = "";
            string _idcli = "", _idpStr = "", _idtStr = "";
            switch (DesdeDevolucion)
            {
                case false:
                    // msginvconfigLocal.BuscaTipomovto(IdMovtoAnulacion, myconect, ref _t1, ref _t2, ref _t1, ref CpteCost); // ERROR: CS7036
                    _idpStr = IdpuntoLocal.ToString();
                    _idtStr = idturnoLocal.ToString();
                    // this.BuscaTransaccion(IdMovto, Secuencia, myconect, ref IdCliente, ref _idpStr, ref _idtStr, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref factura); // ERROR: CS1501
                    double.TryParse(_idpStr, out IdpuntoLocal);
                    double.TryParse(_idtStr, out idturnoLocal);
                    break;
                case true:
                    // msginvconfigLocal.BuscaTipomovto(IdMovto, myconect, ref _t1, ref _t2, ref _t1, ref CpteCost); // ERROR: CS7036
                    _idpStr = IdpuntoLocal.ToString();
                    _idtStr = idturnoLocal.ToString();
                    // this.BuscaTransaccion(IdMovtoAnulacion, ConseAnulacion, myconect, ref IdCliente, ref _idpStr, ref _idtStr, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref _t1, ref factura); // ERROR: CS1501
                    double.TryParse(_idpStr, out IdpuntoLocal);
                    double.TryParse(_idtStr, out idturnoLocal);
                    break;
            }

            switch (DesdeDevolucion)
            {
                case false:
                    if (factura <= 0)
                    {
                        factura = Convert.ToDouble(IdpuntoLocal.ToString() + idturnoLocal.ToString() + IdMovtoAnulacion.ToString() + ConseAnulacion.ToString());
                        //factura = Secuencia
                    }
                    break;
                case true:
                    factura = Convert.ToDouble(IdpuntoLocal.ToString() + idturnoLocal.ToString() + IdMovto.ToString() + Secuencia.ToString());
                    break;
            }

            ConseCpte = factura.ToString();

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCierreCostos"].Rows[fila];
                System.Windows.Forms.Application.DoEvents();

                string _n1 = "";
                switch (DesdeDevolucion)
                {
                    case false:
                        // msginvconfigLocal.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["idgrupo"], row["IdTipomovto"], myconect, ref _n1, ref _n1, ref _n1, ref _n1, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto); // ERROR: CS1503, CS1620
                        break;
                    case true:
                        // msginvconfigLocal.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["idgrupo"], IdMovtoAnulacion, myconect, ref _n1, ref _n1, ref _n1, ref _n1, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto); // ERROR: CS1503, CS1620
                        break;
                }

                if (CpteCost == "9999")
                {
                    MessageBox.Show("Comprobante Costos de Tipo Movimiento " + row["IdTipomovto"] + " no esta parametrizado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                }

                Vlrcosto = Convert.ToDouble(row["CostoPro"]);

                if (Vlrcosto > 0)
                {
                    if (Convert.ToDouble(row["TasaIva"]) > 0)
                    {
                        if (CostoGravado != "999999999999" && InvGravado != "999999999999")
                        {
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, CostoGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", 0, row["CostoPro"], 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", row["CostoPro"], 0, 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                        }
                    }
                    else
                    {
                        if (CostoNoGravado != "999999999999" && InvNoGravado != "999999999999")
                        {
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, CostoNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", 0, row["CostoPro"], 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                            // msgcntLocal.GrabaMovimiento(CpteCost, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), IdCliente, fecha, "Costo en linea - Trans. No." + IdMovto + " - " + Secuencia, " ", row["CostoPro"], 0, 0, UsuarioCierre, myconect, "", "", "", "", "", "", "POST"); // ERROR: CS1503
                        }
                    }
                }
                fila += 1;
                msgbarra.PerformStep();
            }

            // ok = msgcntLocal.CierreDocumento(CpteCost, ConseCpte, myconect); // ERROR: CS1503
            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        public void CargarVentanaCantidadesBodega(double IdProducto, double IdPeriodo, System.Windows.Forms.Form myForma, System.Data.Odbc.OdbcConnection myconnect)
        {
            // frmcantbodega frmcantbodegaObj = new frmcantbodega(); // ERROR: CS0246

            // frmcantbodegaObj.LblIdProducto.Text = IdProducto.ToString(); // ERROR: CS0103
            // frmcantbodegaObj.Lblperiodo.Text = IdPeriodo.ToString(); // ERROR: CS0103
            // frmcantbodegaObj.dsconsolidado = this.BuscaInventarioConsolidado(myconnect, IdProducto, IdPeriodo); // ERROR: CS1503
            // frmcantbodegaObj.dsdatos = this.BuscaInventario(myconnect, IdProducto, IdPeriodo); // ERROR: CS1503
            // frmcantbodegaObj.Show(myForma); // ERROR: CS0103
        }
    }
}
