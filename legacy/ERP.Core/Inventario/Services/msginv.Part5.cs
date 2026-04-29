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
        // Methods from VB source lines ~5100-6194
        // ImprimeInformeCuadreOtrasCtas duplicado eliminado (original en Part4)













































        public string GrabaExistenciaDevolucionCierreCosto(double IdProducto, string CenCosto, double IdBodega, ref double Cantidad, double ValorUnidad, double IdTipoMovto, string TipoVenta, DateTime Fecmovto, System.Data.Odbc.OdbcConnection myconnect,
            ref double VlrCosto, string TipoTran, ref double VlrSubtotal, double CostoDevolucion, double Factura, int IdPunto, int IdTurno,
            string IdUsuario, double TasaIva, string MovtoPos, string Estado)
        {
            int CtrlExistencia = 0;
            double NewCosto = 0;
            double CostoAnterior = 0;
            double CantInicial = 0;
            double CantCompra = 0;
            double cantvendida = 0;
            double Cantfinal = 0;
            double costo = 0;
            double UltCosto = 0;
            double NumCant = 0;
            string ClaseTran = " ";
            double CostoInicial = 0;
            string Costea = "N";
            string CpteCosto = "9999";
            string IdGrupo = "9999";
            double VlrCostoVenta = 0;

            // BuscaTipomovto: positional optional params up to Costea (param 15)
            string _bt1 = ""; string _bt2 = ""; string _bt3 = "";
            string _bt4 = ""; int _bt5 = 0; int _bt6 = 0;
            string _bt7 = ""; string _bt8 = ""; string _bt9 = "";
            string _bt10 = "";
            // this.msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, ref _bt1, ref _bt2, ref _bt3, ref CpteCosto, ref _bt4, ref CtrlExistencia, ref _bt5, ref _bt6, ref _bt7, ref _bt8, ref _bt9, ref _bt10, ref Costea); // ERROR: CS7036

            // BuscaProductos: positional optional params up to NumCant (param 28)
            string _bp1 = ""; string _bp2 = ""; string _bp3 = "";
            string _bp4 = ""; string _bp5 = ""; string _bp6 = "";
            string _bp7 = ""; int _bp8 = 0; decimal _bp9 = 0;
            string _bp10 = ""; string _bp11 = ""; string _bp12 = "";
            string _bp13 = ""; string _bp14 = ""; string _bp15 = "";
            string _bp16 = ""; string _bp17 = ""; string _bp18 = "";
            string _bp19 = ""; string _bp20 = ""; string _bp21 = "";
            string _bp22 = ""; string _bp23 = ""; string _bp24 = "";
            string _bp25 = "";
            // this.msginvconf.BuscaProductos(IdProducto, myconnect, ref _bp1, ref _bp2, ref _bp3, ref _bp4, ref _bp5, ref _bp6, ref _bp7, ref _bp8, ref _bp9, ref _bp10, ref _bp11, ref _bp12, ref _bp13, ref _bp14, ref _bp15, ref IdGrupo, ref _bp16, ref _bp17, ref _bp18, ref _bp19, ref _bp20, ref _bp21, ref _bp22, ref _bp23, ref _bp24, ref NumCant); // ERROR: CS7036

            switch (TipoVenta)
            {
                case "P":
                    ValorUnidad = (ValorUnidad * Cantidad) / (Cantidad * NumCant);
                    Cantidad *= NumCant;
                    break;
            }

            this.BuscaInventario(IdProducto, Strings.Format(Fecmovto, "yyyyMM"), CenCosto, IdBodega, myconnect, ref CantInicial, ref CantCompra, ref cantvendida, ref Cantfinal, ref costo, ref UltCosto, ref CostoInicial);

            if (CostoDevolucion == 0)
            {
                if (Cantidad != 0)
                {
                    CostoDevolucion = VlrSubtotal / Cantidad;
                }
            }

            switch (CtrlExistencia)
            {
                case 0:
                    switch (Costea)
                    {
                        case "Y":
                            NewCosto = CalculaCosotoProducto(IdProducto, Cantidad, CostoDevolucion, myconnect, costo, Cantfinal);
                            break;
                        case "N":
                            NewCosto = costo;
                            costo = UltCosto;
                            break;
                    }
                    this.GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "C";
                    costo = NewCosto;
                    break;
                case 1:
                    NewCosto = CalculaCosotoProducto(IdProducto, Cantidad * -1, CostoDevolucion, myconnect, costo, Cantfinal);
                    GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "V";
                    costo = NewCosto;
                    if (Estado.Trim() != "CA")
                    {
                        if (CpteCosto != "9999")
                        {
                            VlrCostoVenta = costo * Cantidad;
                            this.GrabaDatosTempCostos(IdGrupo, Convert.ToInt32(IdTipoMovto), Fecmovto, Factura, IdPunto, IdTurno, IdUsuario, TasaIva, VlrCostoVenta, MovtoPos, CenCosto, IdBodega, myconnect);
                        }
                    }
                    break;
            }
            VlrCosto = costo;
            return ClaseTran;
        }

        public bool validadLimeteVentasPOS(System.Data.Odbc.OdbcConnection myconnect, int periodo, string nit, string Producto, double cantidad, int idtipoMov, double Cantidad_Grupo)
        {
            string CtrlExist = "  ";
            string stmysqlGrupos = "  ";
            string grupo = " ";
            int cantiRestingProducto = 0;
            string vereficarestringprod = " ";
            int cantiRestingGrupo = 0;
            string vereficarestringGrup = " ";
            string descripcionProd = "";
            string descripcionGrupo = "";
            string execidoProd = "  ";
            string execidoGrupo = "";

            // BuscaProductos: positional optional params up to vereficarestringprod (param 34) and cantiRestingProducto (param 35)
            string _bp1 = ""; string _bp2 = ""; string _bp3 = "";
            string _bp4 = ""; string _bp5 = ""; string _bp6 = "";
            int _bp7 = 0; decimal _bp8 = 0;
            string _bp9 = ""; string _bp10 = ""; string _bp11 = "";
            string _bp12 = ""; string _bp13 = ""; string _bp14 = "";
            string _bp15 = ""; string _bp16 = ""; string _bp17 = "";
            string _bp18 = ""; string _bp19 = ""; string _bp20 = "";
            string _bp21 = ""; string _bp22 = ""; string _bp23 = "";
            double _bp24 = 0; string _bp25 = ""; string _bp26 = "";
            string _bp27 = ""; string _bp28 = ""; string _bp29 = "";
            // msginvconf.BuscaProductos(Producto, myconnect, ref descripcionProd, ref _bp1, ref _bp2, ref _bp3, ref _bp4, ref _bp5, ref _bp6, ref _bp7, ref _bp8, ref _bp9, ref _bp10, ref _bp11, ref _bp12, ref _bp13, ref _bp14, ref grupo, ref _bp15, ref _bp16, ref _bp17, ref _bp18, ref _bp19, ref _bp20, ref _bp21, ref _bp22, ref _bp23, ref _bp24, ref _bp25, ref _bp26, ref _bp27, ref _bp28, ref _bp29, ref vereficarestringprod, ref cantiRestingProducto); // ERROR: CS7036

            // BuscaGrupo: positional optional params up to vereficarestringGrup (param 9) and cantiRestingGrupo (param 10)
            string _bg1 = ""; string _bg2 = ""; string _bg3 = "";
            string _bg4 = ""; string _bg5 = "";
            // msginvconf.BuscaGrupo(grupo, myconnect, ref descripcionGrupo, ref _bg1, ref _bg2, ref _bg3, ref _bg4, ref _bg5, ref vereficarestringGrup, ref cantiRestingGrupo); // ERROR: CS1615, CS1620

            // BuscaTipomovto: positional optional params up to CtrlExist (param 8)
            string _bt1 = ""; string _bt2 = ""; string _bt3 = "";
            string _bt4 = ""; string _bt5 = "";
            int _bt6 = 0;
            // this.msginvconf.BuscaTipomovto(Convert.ToDouble(idtipoMov), myconnect, ref _bt1, ref _bt2, ref _bt3, ref _bt4, ref _bt5, ref CtrlExist); // ERROR: CS7036

            bool ok1 = false;
            string limiteGrupo1 = "   ";
            string descripgrupo1 = "   ";

            switch (CtrlExist)
            {
                case "1":
                    if ((vereficarestringGrup == "Y" && cantiRestingGrupo > 0) || (vereficarestringprod == "Y" && cantiRestingProducto > 0))
                    {
                        stmysql = " select  invpro.idproducto,invpro.Descripcion as descrip,sum(invmov.Cantidad) as campo1, invpro.cantRestringVentas as  cantrest "
                                + " from inv_movtos invmov "
                                + "inner join inv_docs  invdoc on invmov.idtipomovto = invdoc.idtipomovto "
                                + "and invmov.secuencia = invdoc.secuencia  "
                                + "inner join inv_tipomovtos tipoMovto on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                                + "inner join inv_productos invpro on invmov.idproducto = invpro.idproducto  "
                                + "inner join cnt_nit tercero  on invdoc.IdCliente = tercero.NIT "
                                + " where invmov.periodo = " + periodo + " and  tercero.NIT =  '" + nit + "'  "
                                + " and  tipoMovto.CtrlExistencia = 1  and invpro.idproducto  = " + Producto + "  and  invpro.rstingLimeteVentas = 'Y' "
                                + " and invpro.cantRestringVentas > 0 "
                                + " group by invpro.idproducto,invpro.Descripcion ,invpro.cantRestringVentas ";
                        MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "validadLimeteVentas_Productos", ref execidoProd);

                        stmysqlGrupos = " select  gruposterc.IdGruProducto,gruposterc.Descripcion as descGrupo,sum(invmov.Cantidad) as campo1,gruposterc.cantRestringVentas as cantRestring"
                                      + "  from inv_movtos invmov "
                                      + "inner join inv_docs  invdoc "
                                      + "on invmov.idtipomovto = invdoc.idtipomovto "
                                      + "and invmov.secuencia = invdoc.secuencia  "
                                      + "inner join inv_tipomovtos tipoMovto  "
                                      + "on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                                      + "inner join inv_productos invpro    "
                                      + " on invmov.idproducto = invpro.idproducto "
                                      + " inner join  inv_grupos gruposterc "
                                      + " on invpro.IdGruProducto  =  gruposterc.IdGruProducto "
                                      + " inner join cnt_nit tercero "
                                      + " on invdoc.IdCliente = tercero.NIT "
                                      + "  where invmov.periodo = " + periodo + "  "
                                      + " and  tercero.NIT =  '" + nit + "'  "
                                      + " and tipoMovto.CtrlExistencia = 1 "
                                      + " and gruposterc.IdGruProducto = '" + grupo + "'  AND gruposterc.rstingLimeteVentas = 'Y' "
                                      + " and gruposterc.cantRestringVentas > 0 "
                                      + " group by gruposterc.IdGruProducto,gruposterc.Descripcion,gruposterc.cantRestringVentas  ";
                        MyOdbcConet.ExecuteQueryconec(stmysqlGrupos, myconnect, "validadLimeteVentas_grupo", ref execidoGrupo);

                        if (Information.IsNumeric(execidoProd) == false)
                        {
                            execidoProd = "0";
                        }

                        if (Information.IsNumeric(execidoGrupo) == false)
                        {
                            execidoGrupo = "0";
                        }
                        cantidad = cantidad + Convert.ToDouble(execidoProd);
                        Cantidad_Grupo = Cantidad_Grupo + Convert.ToDouble(execidoGrupo);

                        if (cantidad > cantiRestingProducto && (vereficarestringprod == "Y" && cantiRestingProducto > 0))
                        {
                            MessageBox.Show(" Productos  " + Producto + " - " + descripcionProd + " " + "\r\n"
                                   + " Limete Maximo por este Producto " + cantiRestingProducto + " Unidades  " + "\r\n"
                                   + " Unidades compradas " + cantidad + " Unidades" + "\r\n"
                                   + " Pertenece al Grupo  " + grupo + " - " + descripcionGrupo + "\r\n"
                                   + " Limete Maximo por este Grupo  es " + cantiRestingGrupo + " Unidades " + "\r\n"
                                   + " Unidades Compradas por este Grupo es " + Cantidad_Grupo + " Unidades " + "\r\n"
                                   + " Disponible por Producto  " + (Convert.ToInt32(cantiRestingProducto) - cantidad) + " Unidades " + "\r\n"
                                   + " Disponible por Grupo " + (Convert.ToInt32(cantiRestingGrupo) - Cantidad_Grupo) + " Unidades ", "Solido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return true;
                        }
                        else if (Cantidad_Grupo > cantiRestingGrupo && (vereficarestringGrup == "Y" && cantiRestingGrupo > 0))
                        {
                            MessageBox.Show(" ha completado el Limte de Productos que puede  llevar  " + "\r\n" + "  por este Grupo " + grupo + " - " + descripcionGrupo + " " + "\r\n"
                                   + " Y el Limite Maximo es de " + cantiRestingGrupo + " Unidades  " + "\r\n"
                                   + " Unidades Compradas por este Grupo " + Cantidad_Grupo + " Unidades" + "\r\n"
                                   + " Disponible por este Grupo " + (Convert.ToInt32(cantiRestingGrupo) - Cantidad_Grupo), "Solido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
            return false;
        }

        public void EliminaMovtoTransaccionPOS(int Idtipomovto, double Secuencia, System.Data.Odbc.OdbcConnection myconnect)
        {
            System.Text.StringBuilder Stbuilder = new System.Text.StringBuilder();
            DataSet dsdata = new DataSet();
            double fila = 0;

            Stbuilder.Append("select idubicacion,idbodega,idproducto,tipoventa,cantidad,vlrunidad,fecmovto,vlriva,subtotal,vlrdsto,neto,consecmovto,");
            Stbuilder.Append("vlrretfte,retfte,vlrica,tasaica  ");
            Stbuilder.Append("from inv_movtos ");
            Stbuilder.Append("where idtipomovto=" + Idtipomovto + " and secuencia=" + Secuencia);

            ok = this.MyOdbcConet.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "EliminaMovtoTransaccionPOS", ref dsdata, "tblmovto");
            if (ok == true)
            {
                for (fila = 0; fila <= dsdata.Tables["tblmovto"].Rows.Count - 1; fila++)
                {
                    DataRow row = dsdata.Tables["tblmovto"].Rows[Convert.ToInt32(fila)];
                    // this.EliminaMovimiento(Idtipomovto, Secuencia, Convert.ToString(row["idubicacion"]), Convert.ToDouble(row["idbodega"]), Convert.ToString(row["idproducto"]), Convert.ToString(row["tipoventa"]), Convert.ToDouble(row["cantidad"]), Convert.ToDouble(row["vlrunidad"]), Convert.ToDateTime(row["fecmovto"]), Convert.ToDouble(row["vlriva"]), Convert.ToDouble(row["subtotal"]), Convert.ToDouble(row["vlrdsto"]), Convert.ToDouble(row["neto"]), Convert.ToDouble(row["consecmovto"]), myconnect, Convert.ToDecimal(row["vlrretfte"]), Convert.ToDecimal(row["retfte"]), Convert.ToDecimal(row["vlrica"]), Convert.ToDecimal(row["tasaica"])); // ERROR: CS7036
                }
            }
        }

        private bool GrabaTransaccion_ordenPedido(double IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection myconect, string IdCliente, DateTime FecIng,
            double VlrTotal, double VlrSubTotal, double VlrDsto, double VlrIva, string Estado, string IdUsuario, int ClaPago,
            double VlrEfectivo, double VlrCredito, double VlrTarDebito, double VlrTarCredito, int Idpunto,
            int IdTurno, double VlrArqueo, string detalle, DateTime FecVence, decimal VlrRetfte,
            decimal VlrIca, bool DesdeConv, string NumFacturaConv, int idvendedor, int idtipomovdev, double secuenciaDev)
        {
            double ConseFact = 0;
            int Cladoc = 9;
            string codfactura = "0";
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();

            ok = this.BuscaTransaccion_ordenPedido(IdTipoMovto, Secuencia, myconect);
            switch (ok)
            {
                case false:
                    stbuilder.Append("insert into inv_docs_orden(IdTipoMovto,Secuencia,IdCliente,FecIng,VlrTotal,vlrSubTotal,VlrDsto,VlrIva,Estado,IdUsuario,ClaPago,VlrEfectivo,VlrCredito,VlrTarDebito,VlrTarCredito,IdPunto,IdTurno,VlrArqueos,detalle,Factura,fecvence,VlrRetfte,vlrica,idVendedor,idtipomovdev,secuenciadev) values ('");
                    stbuilder.Append(IdTipoMovto + "','");
                    stbuilder.Append(Secuencia + "','");
                    stbuilder.Append(IdCliente + "','");
                    stbuilder.Append(Strings.Format(FecIng, varini.PstForFec) + "','");
                    stbuilder.Append(VlrTotal + "','");
                    stbuilder.Append(VlrSubTotal + "','");
                    stbuilder.Append(VlrDsto + "','");
                    stbuilder.Append(VlrIva + "','");
                    stbuilder.Append(Estado + "','");
                    stbuilder.Append(IdUsuario + "','");
                    stbuilder.Append(ClaPago + "','");
                    stbuilder.Append(VlrEfectivo + "','");
                    stbuilder.Append(VlrCredito + "','");
                    stbuilder.Append(VlrTarDebito + "','");
                    stbuilder.Append(VlrTarCredito + "',");
                    stbuilder.Append(Idpunto + ",");
                    stbuilder.Append(IdTurno + ",");
                    stbuilder.Append(VlrArqueo + ",'");
                    stbuilder.Append(detalle + "','");
                    stbuilder.Append(ConseFact + "','");
                    stbuilder.Append(Strings.Format(FecVence, varini.PstForFec) + "','");
                    stbuilder.Append(VlrRetfte + "','");
                    stbuilder.Append(VlrIca + "','");
                    stbuilder.Append(idvendedor + "','");
                    stbuilder.Append(idtipomovdev + "','");
                    stbuilder.Append(secuenciaDev + "')");
                    break;
                case true:
                    stbuilder.Append("update inv_docs_orden set ");
                    stbuilder.Append("VlrTotal= VlrTotal + '" + VlrTotal + "',");
                    stbuilder.Append("VlrSubTotal = VlrSubTotal + '" + VlrSubTotal + "',");
                    stbuilder.Append("VlrDsto= VlrDsto + '" + VlrDsto + "',");
                    stbuilder.Append("VlrIva = VlrIva + '" + VlrIva + "',");
                    stbuilder.Append("vlrica = vlrica + '" + VlrIca + "',");
                    stbuilder.Append("VlrRetfte = VlrRetfte + '" + VlrRetfte + "' ");
                    stbuilder.Append("where IdTipoMovto = " + IdTipoMovto + " and Secuencia = " + Secuencia);
                    break;
            }

            ok = this.MyOdbcConet.ExecuteQueryconec(stbuilder.ToString(), myconect, "GrabaTransaccion");
            return ok;
        }

        public void GrabaMovtoTransaccion_orden_Pedido(double IdTipoMovto, double secuencia, string Cencosto, double Idbodega, double IdProducto, string Tipoventa, string ClaMovto, System.Data.Odbc.OdbcConnection myconect, DateTime FecMovto, double Cantidad, string NumFactura,
            decimal TasaIva, decimal TasaDscto, string IdCliente, double VlrIva,
            double Subtotal, double VlrDsto, double Neto, double VlrUnidad, string Idusuario, int Idpunto, int IdTurno,
            double Valadm, double IvaAdm, double IvaTiq, double OtroImp, double AeroPort, double ImpComb, double Costo, decimal retfte, decimal VlrRetfte,
            decimal TasaIca, decimal VlrIca, string MovtoPos)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            double ConsecMovto = 0;

            ConsecMovto = this.ConsecutivoItemsMovto_orden_Pedido(IdTipoMovto, secuencia, myconect);
            ok = false;

            switch (ok)
            {
                case false:
                    stbuilder.Append("Insert into inv_movtos_orden(IdTipoMovto,Secuencia,FecMovto,NumFactura,idubicacion,idbodega,IdProducto,Cantidad,TasaIva,TasaDscto,FecSystem,IdCliente,VlrIva,VlrDsto,VlrUnidad,Idusuario,IdPunto,IdTurno,SubTotal,neto,Tipoventa,periodo,ClaMovto,");
                    stbuilder.Append("Valadm,IvaAdm,IvaTiq,OtroImp,AeroPort,costo,ImpComb,retfte,VlrRetfte,vlrica,tasaica,pos,consecmovto) values ('");
                    stbuilder.Append(IdTipoMovto + "','");
                    stbuilder.Append(secuencia + "','");
                    stbuilder.Append(Strings.Format(FecMovto, varini.PstForFec) + "','");
                    stbuilder.Append(NumFactura + "','");
                    stbuilder.Append(Cencosto + "','");
                    stbuilder.Append(Idbodega + "','");
                    stbuilder.Append(IdProducto + "','");
                    stbuilder.Append(Cantidad + "','");
                    stbuilder.Append(TasaIva + "','");
                    stbuilder.Append(TasaDscto + "','");
                    stbuilder.Append(Strings.Format(DateTime.Now, varini.pstForfecyHora) + "','");
                    stbuilder.Append(IdCliente + "','");
                    stbuilder.Append(VlrIva + "','");
                    stbuilder.Append(VlrDsto + "','");
                    stbuilder.Append(VlrUnidad + "','");
                    stbuilder.Append(Idusuario + "','");
                    stbuilder.Append(Idpunto + "','");
                    stbuilder.Append(IdTurno + "','");
                    stbuilder.Append(Subtotal + "','");
                    stbuilder.Append(Neto + "','");
                    stbuilder.Append(Tipoventa + "','");
                    stbuilder.Append(Strings.Format(FecMovto, "yyyyMM") + "','");
                    stbuilder.Append(ClaMovto + "','");
                    stbuilder.Append(Valadm + "','");
                    stbuilder.Append(IvaAdm + "','");
                    stbuilder.Append(IvaTiq + "','");
                    stbuilder.Append(OtroImp + "','");
                    stbuilder.Append(AeroPort + "','");
                    stbuilder.Append(Costo + "','");
                    stbuilder.Append(ImpComb + "','");
                    stbuilder.Append(retfte + "','");
                    stbuilder.Append(VlrRetfte + "','");
                    stbuilder.Append(VlrIca + "','");
                    stbuilder.Append(TasaIca + "','");
                    stbuilder.Append(MovtoPos + "',");
                    stbuilder.Append(ConsecMovto + ")");

                    this.MyOdbcConet.ExecuteQueryconec(stbuilder.ToString(), myconect, "GrabaMovimiento");
                    break;
                case true:
                    stbuilder.Append("update inv_movtos_orden set ");
                    stbuilder.Append("Neto = Neto + " + Neto + ",");
                    stbuilder.Append("SubTotal =  SubTotal + " + Subtotal + ",");
                    stbuilder.Append("Cantidad = Cantidad + " + Cantidad + ",");
                    stbuilder.Append("VlrIva = VlrIva + " + VlrIva + ",");
                    stbuilder.Append("VlrDsto = VlrDsto + " + VlrDsto + ",");
                    stbuilder.Append("VlrRetfte = VlrRetfte + " + VlrRetfte + ",");
                    stbuilder.Append("vlrica = vlrica + " + VlrIca + ",");
                    stbuilder.Append("Pos = '" + MovtoPos + "' ");
                    stbuilder.Append("where IdTipoMovto = " + IdTipoMovto + " and Secuencia = " + secuencia + " and IdProducto = " + IdProducto);
                    stbuilder.Append(" and Tipoventa = '" + Tipoventa + "' and idubicacion='" + Cencosto + "' and idbodega=" + Idbodega);

                    this.MyOdbcConet.ExecuteQueryconec(stbuilder.ToString(), myconect, "GrabaMovtoTransaccion");
                    break;
            }
        }

        private double ConsecutivoItemsMovto_orden_Pedido(double IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection myconnect)
        {
            double Consecutivo = 0;
            stmysql = "select (max(consecmovto)+1) as campo1 from inv_movtos_orden where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia;
            // ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaMovtoTransaccion", ref Consecutivo); // ERROR: CS1503
            return Consecutivo;
        }

        public bool BuscaTransaccion_ordenPedido(double IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection myconect,
            ref string IdCliente, ref string IdPunto, ref string IdTurno, ref string FecIng, ref string IdUsuario,
            ref double Efectivo, ref double TarjDebito, ref double TarjCredito, ref double Cheque, ref double Cuotas,
            ref double ValTotal, ref double NumFactura, ref double VlrDsto, ref double VlrIva,
            ref string detalle, ref double Subtotal, ref int ClaPago,
            ref int Periodicidad, ref int Plazo, ref int clades, ref string fecdsto,
            ref double cuota, ref decimal TasaInt, ref string estado, ref double VlrRetfte,
            ref double VlrIca, ref int idvendedor, ref string cedula,
            ref string sostienePrecio, ref string NombreVendedor,
            ref DateTime FechaVence, ref string IdTipoMovto_trans, ref string Secuencia_trans)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            DataSet datasetBuscaTransaccion_ordenPedido = new DataSet();

            stbuilder.Append(" select docs.IdCliente ,docs.IdPunto , docs.IdTurno , docs.FecIng, ");
            stbuilder.Append(" docs.IdUsuario ,docs.VlrEfectivo ,docs.VlrTarDebito , docs.VlrTarCredito, ");
            stbuilder.Append(" docs.VlrCheque ,docs.VlrCredito , docs.VlrTotal ,docs.Factura,");
            stbuilder.Append(" docs.Vlrdsto ,docs.VlrIva ,docs.Detalle ,docs.VlrSubtotal,");
            stbuilder.Append(" docs.clapago , docs.periodicidad , docs.plazo , docs.clades,");
            stbuilder.Append(" docs.fecdsto , docs.cuota , docs.Tasa ,docs.estado, ");
            stbuilder.Append(" docs.Vlrretfte ,docs.vlrica ,docs.idVendedor,vendedor.cedula,tipomovtos.OrdenPedido_sost_precio ");
            stbuilder.Append(" ,vendedor.nombre as nomVendedor ,docs.fecvence,docs.IdTipoMovto_trans,docs.Secuencia_trans");
            stbuilder.Append(" from inv_docs_orden docs ");
            stbuilder.Append(" inner join inv_tipomovtos tipomovtos on  docs.IdTipoMovto = tipomovtos.IdTipoMovto ");
            stbuilder.Append(" inner join inv_vendedor vendedor  on docs.idVendedor = vendedor.idVendedor ");
            stbuilder.Append(" where docs.IdTipoMovto = " + IdTipoMovto + "  and docs.Secuencia = " + Secuencia);

            this.MyOdbcConet.ExecuteQueryDataset(stbuilder.ToString(), myconect, "BuscaTransaccion_ordenPedido", ref datasetBuscaTransaccion_ordenPedido, "ordenPedido");
            if (datasetBuscaTransaccion_ordenPedido.Tables["ordenPedido"].Rows.Count > 0)
            {
                DataRow row = datasetBuscaTransaccion_ordenPedido.Tables["ordenPedido"].Rows[0];

                IdCliente = Convert.ToString(row["IdCliente"]);
                IdPunto = Convert.ToString(row["IdPunto"]);
                IdTurno = Convert.ToString(row["IdTurno"]);
                FecIng = Convert.ToString(row["FecIng"]);

                IdUsuario = Convert.ToString(row["IdUsuario"]);
                Efectivo = Convert.ToDouble(row["VlrEfectivo"]);
                TarjDebito = Convert.ToDouble(row["VlrTarDebito"]);
                TarjCredito = Convert.ToDouble(row["VlrTarCredito"]);

                Cheque = Convert.ToDouble(row["VlrCheque"]);
                Cuotas = Convert.ToDouble(row["VlrCredito"]);
                ValTotal = Convert.ToDouble(row["VlrTotal"]);
                NumFactura = Convert.ToDouble(row["Factura"]);

                VlrDsto = Convert.ToDouble(row["Vlrdsto"]);
                VlrIva = Convert.ToDouble(row["VlrIva"]);
                detalle = Convert.ToString(row["Detalle"]);
                Subtotal = Convert.ToDouble(row["VlrSubtotal"]);

                ClaPago = Convert.ToInt32(row["clapago"]);
                Periodicidad = Convert.ToInt32(row["periodicidad"]);
                Plazo = Convert.ToInt32(row["plazo"]);
                clades = Convert.ToInt32(row["clades"]);

                fecdsto = Convert.ToString(row["fecdsto"]);
                cuota = Convert.ToDouble(row["cuota"]);
                TasaInt = Convert.ToDecimal(row["Tasa"]);
                estado = Convert.ToString(row["estado"]);

                VlrRetfte = Convert.ToDouble(row["Vlrretfte"]);
                VlrIca = Convert.ToDouble(row["vlrica"]);
                idvendedor = Convert.ToInt32(row["idVendedor"]);
                cedula = Convert.ToString(row["cedula"]);

                sostienePrecio = Convert.ToString(row["OrdenPedido_sost_precio"]);
                NombreVendedor = Convert.ToString(row["nomVendedor"]);
                FechaVence = Convert.ToDateTime(row["fecvence"]);

                if (row.IsNull("IdTipoMovto_trans") == true)
                {
                    IdTipoMovto_trans = "Null";
                }
                else
                {
                    IdTipoMovto_trans = Convert.ToString(row["IdTipoMovto_trans"]);
                }
                if (row.IsNull("Secuencia_trans") == true)
                {
                    Secuencia_trans = "Null";
                }
                else
                {
                    Secuencia_trans = Convert.ToString(row["Secuencia_trans"]);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        // Overload without optional ref params
        public bool BuscaTransaccion_ordenPedido(double IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection myconect)
        {
            string _IdCliente = null; string _IdPunto = null; string _IdTurno = null; string _FecIng = null; string _IdUsuario = " ";
            double _Efectivo = 0; double _TarjDebito = 0; double _TarjCredito = 0; double _Cheque = 0; double _Cuotas = 0;
            double _ValTotal = 0; double _NumFactura = 0; double _VlrDsto = 0; double _VlrIva = 0;
            string _detalle = " "; double _Subtotal = 0; int _ClaPago = 0;
            int _Periodicidad = 0; int _Plazo = 0; int _clades = 0; string _fecdsto = " ";
            double _cuota = 0; decimal _TasaInt = 0; string _estado = ""; double _VlrRetfte = 0;
            double _VlrIca = 0; int _idvendedor = 0; string _cedula = "99999999999999";
            string _sostienePrecio = "N"; string _NombreVendedor = "";
            DateTime _FechaVence = new DateTime(1950, 1, 1); string _IdTipoMovto_trans = "Null"; string _Secuencia_trans = "Null";
            return BuscaTransaccion_ordenPedido(IdTipoMovto, Secuencia, myconect,
                ref _IdCliente, ref _IdPunto, ref _IdTurno, ref _FecIng, ref _IdUsuario,
                ref _Efectivo, ref _TarjDebito, ref _TarjCredito, ref _Cheque, ref _Cuotas,
                ref _ValTotal, ref _NumFactura, ref _VlrDsto, ref _VlrIva,
                ref _detalle, ref _Subtotal, ref _ClaPago,
                ref _Periodicidad, ref _Plazo, ref _clades, ref _fecdsto,
                ref _cuota, ref _TasaInt, ref _estado, ref _VlrRetfte,
                ref _VlrIca, ref _idvendedor, ref _cedula,
                ref _sostienePrecio, ref _NombreVendedor,
                ref _FechaVence, ref _IdTipoMovto_trans, ref _Secuencia_trans);
        }

        public void CargaDocumento_orden_Pedido(DataGridView GrillaMovtoCpte, int IdTipoMovto, string ConseCpte, System.Data.Odbc.OdbcConnection Myconect, Form Myforma)
        {
            string stmysqlLocal;
            double item = 0;
            ERP.Core.Compartido.Controles.Barraprogress FrmProgres = new ERP.Core.Compartido.Controles.Barraprogress("Cargando Movimientos", Myforma);
            int canreg = 0;
            int fila = 0;
            DataGridViewCellStyle style2 = new DataGridViewCellStyle();
            style2.BackColor = Color.LightBlue;

            stmysqlLocal = "Select movtos.IdProducto, maepro.Descripcion, movtos.Tipoventa,movtos.Cantidad, movtos.VlrUnidad,movtos.SubTotal, "
                + "movtos.TasaIva,movtos.TasaDscto,invdoc.estado,movtos.idubicacion,movtos.idbodega,movtos.consecmovto,movtos.costo, "
                + "movtos.retfte,movtos.tasaica,movtos.Vlrica,movtos.VlrRetfte,movtos.aplica_orden  "
                + "from  inv_movtos_orden movtos inner join inv_productos maepro on movtos.IdProducto = maepro.IdProducto"
                + " inner join inv_docs_orden invdoc on movtos.IdTipoMovto = invdoc.IdTipoMovto and movtos.Secuencia = invdoc.Secuencia"
                + " where movtos.IdTipoMovto=" + IdTipoMovto + " and movtos.Secuencia = '" + ConseCpte + "' "
                + "order by movtos.aplica_orden,movtos.consecmovto";

            FrmProgres.DefineMaximo(stmysqlLocal, Myconect);
            FrmProgres.Show();

            System.Windows.Forms.Application.DoEvents();
            GrillaMovtoCpte.Rows.Clear();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysqlLocal, Myconect, "CargaDocumento", ref myRead, "TblCargaDomto");
            canreg = myRead.Tables["TblCargaDomto"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCargaDomto"].Rows[fila];
                GrillaMovtoCpte.Rows.Add(row["IdProducto"], row["Descripcion"], row["Tipoventa"], row["Cantidad"], row["VlrUnidad"],
                                 row["SubTotal"], row["TasaIva"], row["TasaDscto"], row["estado"], row["idubicacion"], row["idbodega"],
                                 Convert.ToDouble(row["consecmovto"]), row["costo"], row["retfte"], row["tasaica"], row["Vlrica"], row["VlrRetfte"], row["aplica_orden"]);

                if (Convert.ToString(row["aplica_orden"]).Trim() == "Y")
                {
                    GrillaMovtoCpte.Rows[fila].DefaultCellStyle = style2;
                }

                FrmProgres.PerformStep();
                fila += 1;
            }
            myRead.Dispose();
            FrmProgres.Close();
        }

        public bool EliminaMovtoTransaccion_orden_Pedido(double IdTipoMovto, double Secuencia, string Cencosto, double Idbodega, double IdProducto, string Tipoventa, double ConsecMovto, System.Data.Odbc.OdbcConnection myconect)
        {
            stmysql = "delete from inv_movtos_orden  where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia + " and IdProducto =" + IdProducto + " and TipoVenta = '" + Tipoventa + "' and idubicacion='" + Cencosto + "' and idbodega=" + Idbodega + " and consecmovto=" + ConsecMovto;
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaMovtoTransaccion");
            return ok;
        }

        private bool Imprime_OrdenPedido(int IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection Myconnect, Form myforma, int Cladoc)
        {
            string nitVal = " ";
            int Regimen = 0;
            string Direccion = " ";
            string Telefono = " ";
            string Nombre = " ";
            string Resolucion = " ";
            DateTime Fecresol = default(DateTime);
            string Prefijo = " ";
            int NumInicial = 0;
            int NumFinal = 0;
            string NomRegimen = null;
            string Ciudad = " ";
            string IdCtrlFactura = " ";
            ERP.Core.Compartido.Forms.imprimir Impresion = new ERP.Core.Compartido.Forms.imprimir();
            // CrystalDecisions.CrystalReports.Engine.ReportDocument factura; // ERROR: CS0246
            switch (Cladoc)
            {
                case 9:
                    // factura = new ERP.Core.Compartido.Reportes.reporte("inv_rordencompra"); // ERROR: CS0103
                    break;
                default:
                    // factura = new ERP.Core.Compartido.Reportes.reporte("inv_rordenPedido"); // ERROR: CS0103
                    break;
            }
            ERP.Core.Compartido.Reportes.config_report ConfRep = new ERP.Core.Compartido.Reportes.config_report();
            ERP.Core.Compartido.Utilidades.Numeros_A_Letras num = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
            string stNumeletras;
            double valor = 0;

            // BuscaTipomovto: positional optional params up to IdCtrlFactura (param 13)
            string _bt1 = ""; string _bt2 = ""; string _bt3 = "";
            string _bt4 = ""; string _bt5 = ""; int _bt6 = 0;
            int _bt7 = 0; string _bt8 = ""; string _bt9 = "";
            string _bt10 = "";
            // this.msginvconf.BuscaTipomovto(Convert.ToDouble(IdTipoMovto), Myconnect, ref _bt1, ref _bt2, ref _bt3, ref _bt4, ref _bt5, ref _bt6, ref _bt7, ref _bt8, ref _bt9, ref _bt10, ref IdCtrlFactura); // ERROR: CS7036

            // BuscarCompania: positional optional params
            string _bc1 = ""; string _bc2 = ""; string _bc3 = "";
            string _bc4 = ""; string _bc5 = ""; string _bc6 = "";
            string _bc7 = ""; string _bc8 = ""; string _bc9 = "";
            string _bc10 = ""; string _bc11 = ""; string _bc12 = "";
            string _bc13 = ""; string _bc14 = ""; string _bc15 = "";
            string _bc16 = ""; string _bc17 = ""; string _bc18 = "";
            string _bc19 = ""; string _bc20 = ""; string _bc21 = "";
            string _bc22 = ""; string _bc23 = ""; string _bc24 = "";
            string _bc25 = ""; string _bc26 = ""; string _bc27 = "";
            string _bc28 = ""; string _bc29 = ""; string _bc30 = "";
            string _bc31 = "";
            // msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _bc1, ref _bc2, ref _bc3, ref _bc4, ref _bc5, ref _bc6, ref _bc7, ref _bc8, ref _bc9, ref _bc10, ref _bc11, ref _bc12, ref nitVal, ref Direccion, ref _bc13, ref _bc14, ref _bc15, ref _bc16, ref _bc17, ref _bc18, ref _bc19, ref _bc20, ref Nombre, ref Telefono, ref _bc21, ref _bc22, ref _bc23, ref _bc24, ref _bc25, ref _bc26, ref Ciudad); // ERROR: CS7036

            // this.msginvconf.BuscaDatosFacturacion(IdCtrlFactura, Myconnect, ref Resolucion, ref Fecresol, ref Regimen, ref Prefijo, ref NumInicial, ref NumFinal); // ERROR: CS7036

            switch (Regimen)
            {
                case 0:
                    NomRegimen = "Regimen Comun";
                    break;
                case 1:
                    NomRegimen = "Regimen Simplificado";
                    break;
            }

            // BuscaTransaccion: positional optional params up to valor (param 14)
            string _btr1 = null; string _btr2 = null; string _btr3 = null;
            string _btr4 = " "; double _btr5 = 0; double _btr6 = 0;
            double _btr7 = 0; double _btr8 = 0; double _btr9 = 0;
            double _btr10 = 0;
            // this.BuscaTransaccion(IdTipoMovto, Secuencia, Myconnect, ref _btr1, ref _btr2, ref _btr3, ref _btr4, ref _btr5, ref _btr6, ref _btr7, ref _btr8, ref _btr9, ref _btr10, ref valor); // ERROR: CS1501

            stNumeletras = num.Num_a_Letras(valor) + "MLC";
            // factura.SetParameterValue("IdTipoMovto", IdTipoMovto); // ERROR: CS0103
            // factura.SetParameterValue("Secuencia", Secuencia); // ERROR: CS0103
            // factura.SetParameterValue("nit", nitVal); // ERROR: CS0103
            // factura.SetParameterValue("regimen", Regimen); // ERROR: CS0103
            // factura.SetParameterValue("direccion", Direccion); // ERROR: CS0103
            // factura.SetParameterValue("telefono", Telefono); // ERROR: CS0103
            // factura.SetParameterValue("Nombre", Nombre); // ERROR: CS0103
            // factura.SetParameterValue("resolucion", Resolucion); // ERROR: CS0103
            // factura.SetParameterValue("Fecresol", Fecresol); // ERROR: CS0103
            // factura.SetParameterValue("telefono", Telefono); // ERROR: CS0103
            // factura.SetParameterValue("NumInicial", NumInicial); // ERROR: CS0103
            // factura.SetParameterValue("NumFinal", NumFinal); // ERROR: CS0103
            // factura.SetParameterValue("Prefijo", Prefijo); // ERROR: CS0103
            // factura.SetParameterValue("Enletras", stNumeletras); // ERROR: CS0103
            // factura.SetParameterValue("Ciudad", Ciudad); // ERROR: CS0103

            // ConfRep.confi_reportes(myforma, factura, "", true); // ERROR: CS0103
            return ok;
        }

        private bool Imprime_Remision(int IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection Myconnect, Form myforma)
        {
            string nitVal = " ";
            int Regimen = 0;
            string Direccion = " ";
            string Telefono = " ";
            string Nombre = " ";
            string Resolucion = " ";
            DateTime Fecresol = default(DateTime);
            string Prefijo = " ";
            int NumInicial = 0;
            int NumFinal = 0;
            string NomRegimen = null;
            string Ciudad = " ";
            string IdCtrlFactura = " ";
            ERP.Core.Compartido.Forms.imprimir Impresion = new ERP.Core.Compartido.Forms.imprimir();
            // CrystalDecisions.CrystalReports.Engine.ReportDocument factura = new ERP.Core.Compartido.Reportes.reporte("inv_remision"); // ERROR: CS0246
            ERP.Core.Compartido.Reportes.config_report ConfRep = new ERP.Core.Compartido.Reportes.config_report();
            ERP.Core.Compartido.Utilidades.Numeros_A_Letras num = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
            string stNumeletras;
            double valor = 0;

            // BuscaTipomovto: positional optional params up to IdCtrlFactura (param 13)
            string _bt1 = ""; string _bt2 = ""; string _bt3 = "";
            string _bt4 = ""; string _bt5 = ""; int _bt6 = 0;
            int _bt7 = 0; string _bt8 = ""; string _bt9 = "";
            string _bt10 = "";
            // this.msginvconf.BuscaTipomovto(Convert.ToDouble(IdTipoMovto), Myconnect, ref _bt1, ref _bt2, ref _bt3, ref _bt4, ref _bt5, ref _bt6, ref _bt7, ref _bt8, ref _bt9, ref _bt10, ref IdCtrlFactura); // ERROR: CS7036

            // BuscarCompania: same pattern as Imprime_OrdenPedido
            string _bc1 = ""; string _bc2 = ""; string _bc3 = "";
            string _bc4 = ""; string _bc5 = ""; string _bc6 = "";
            string _bc7 = ""; string _bc8 = ""; string _bc9 = "";
            string _bc10 = ""; string _bc11 = ""; string _bc12 = "";
            string _bc13 = ""; string _bc14 = ""; string _bc15 = "";
            string _bc16 = ""; string _bc17 = ""; string _bc18 = "";
            string _bc19 = ""; string _bc20 = ""; string _bc21 = "";
            string _bc22 = ""; string _bc23 = ""; string _bc24 = "";
            string _bc25 = ""; string _bc26 = ""; string _bc27 = "";
            string _bc28 = ""; string _bc29 = ""; string _bc30 = "";
            string _bc31 = "";
            // msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect, ref _bc1, ref _bc2, ref _bc3, ref _bc4, ref _bc5, ref _bc6, ref _bc7, ref _bc8, ref _bc9, ref _bc10, ref _bc11, ref _bc12, ref nitVal, ref Direccion, ref _bc13, ref _bc14, ref _bc15, ref _bc16, ref _bc17, ref _bc18, ref _bc19, ref _bc20, ref Nombre, ref Telefono, ref _bc21, ref _bc22, ref _bc23, ref _bc24, ref _bc25, ref _bc26, ref Ciudad); // ERROR: CS7036

            // this.msginvconf.BuscaDatosFacturacion(IdCtrlFactura, Myconnect, ref Resolucion, ref Fecresol, ref Regimen, ref Prefijo, ref NumInicial, ref NumFinal); // ERROR: CS7036

            switch (Regimen)
            {
                case 0:
                    NomRegimen = "Regimen Comun";
                    break;
                case 1:
                    NomRegimen = "Regimen Simplificado";
                    break;
            }

            // BuscaTransaccion: positional optional params up to valor (param 14)
            string _btr1 = null; string _btr2 = null; string _btr3 = null;
            string _btr4 = " "; double _btr5 = 0; double _btr6 = 0;
            double _btr7 = 0; double _btr8 = 0; double _btr9 = 0;
            double _btr10 = 0;
            // this.BuscaTransaccion(IdTipoMovto, Secuencia, Myconnect, ref _btr1, ref _btr2, ref _btr3, ref _btr4, ref _btr5, ref _btr6, ref _btr7, ref _btr8, ref _btr9, ref _btr10, ref valor); // ERROR: CS1501

            stNumeletras = num.Num_a_Letras(valor) + "MLC";
            // factura.SetParameterValue("IdTipoMovto", IdTipoMovto); // ERROR: CS0103
            // factura.SetParameterValue("Secuencia", Secuencia); // ERROR: CS0103
            // factura.SetParameterValue("nit", nitVal); // ERROR: CS0103
            // factura.SetParameterValue("regimen", Regimen); // ERROR: CS0103
            // factura.SetParameterValue("direccion", Direccion); // ERROR: CS0103
            // factura.SetParameterValue("telefono", Telefono); // ERROR: CS0103
            // factura.SetParameterValue("Nombre", Nombre); // ERROR: CS0103
            // factura.SetParameterValue("resolucion", Resolucion); // ERROR: CS0103
            // factura.SetParameterValue("Fecresol", Fecresol); // ERROR: CS0103
            // factura.SetParameterValue("telefono", Telefono); // ERROR: CS0103
            // factura.SetParameterValue("NumInicial", NumInicial); // ERROR: CS0103
            // factura.SetParameterValue("NumFinal", NumFinal); // ERROR: CS0103
            // factura.SetParameterValue("Prefijo", Prefijo); // ERROR: CS0103
            // factura.SetParameterValue("Enletras", stNumeletras); // ERROR: CS0103
            // factura.SetParameterValue("Ciudad", Ciudad); // ERROR: CS0103

            // ConfRep.confi_reportes(myforma, factura, "", true); // ERROR: CS0103
            return ok;
        }

        public bool GrabaEstado_OrdenPedido(double IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection myconect, string estado, string Detalle)
        {
            string Stdetalle = "";

            if (Detalle.Trim() != "")
            {
                Stdetalle = ",Detalle='" + Detalle + "' ";
            }

            stmysql = "update inv_docs_orden set estado = '" + estado + "' " + Stdetalle
                + " where IdTipoMovto = " + IdTipoMovto + " and Secuencia =" + Secuencia;
            this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "GrabaEstado_OrdenPedido");
            return ok;
        }

        public void RelacionarOrdenPedido_TrasaccinVoC(double IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection myconect, double IdTipoMovto_trans, double Secuencia_trans)
        {
            stmysql = "update inv_docs_orden set IdTipoMovto_trans = " + IdTipoMovto_trans + ",Secuencia_trans =" + Secuencia_trans
                + " where IdTipoMovto = " + IdTipoMovto + " and Secuencia =" + Secuencia;
            this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "RelacionarOrdenPedido_TrasaccinVoC");
        }

        public void RelacionarMovtoOrdenPedido_TrasaccinVoC(double IdTipoMovto, double Secuencia, double IdProducto, string Tipoventa, double ConsecMovto, System.Data.Odbc.OdbcConnection myconect, string aplica_orden, string RegistroTrasab)
        {
            stmysql = "update inv_movtos_orden  set aplica_orden ='" + aplica_orden + "' ,RegistroTrasab = '" + RegistroTrasab + "'   where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia + " and IdProducto =" + IdProducto + " and TipoVenta = '" + Tipoventa + "' and consecmovto=" + ConsecMovto;
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaMovtoTransaccion");
        }

        public virtual void RelacionarMovtoOrdenPedido_TrasaccinVoC(double IdTipoMovto, double Secuencia, double IdProducto, double ConsecMovto, System.Data.Odbc.OdbcConnection myconect, string aplica_orden, string RegistroTrasab)
        {
            stmysql = "update inv_movtos_orden  set aplica_orden ='" + aplica_orden + "' ,RegistroTrasab = '" + RegistroTrasab + "'   where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia + " and IdProducto =" + IdProducto + " and consecmovto=" + ConsecMovto;
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaMovtoTransaccion");
        }

        public double ConsecutivoItemsMovto_orden(double IdTipoMovto, double Secuencia, System.Data.Odbc.OdbcConnection myconnect)
        {
            double Consecutivo = 0;
            stmysql = "select max(consecmovto) as campo1 from inv_movtos where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia;
            // ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaMovtoTransaccion", ref Consecutivo); // ERROR: CS1503
            return Consecutivo;
        }

        public void Retroceder_Aplicacion_orden_Item(string RegistroTrasab, System.Data.Odbc.OdbcConnection myconnect)
        {
            stmysql = "update inv_movtos_orden  set aplica_orden ='N'  where RegistroTrasab = '" + RegistroTrasab + "'   ";
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaMovtoTransaccion");
        }

        public DataSet GeneraInformeDocFaltantesInv(double Ubicacion, double IdBodega, DateTime FecInicial, DateTime FecFinal, Form myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Informe Documentos faltantes", myforma);
            DataSet Dsdata = new DataSet();
            DataSet DsReporte = new DataSet();
            string StUbicacion = "";
            string StBodega = "";
            double fila = 0;
            string StCadConcat = "";
            string StCadConcatDos = "";

            MsgBarra.ValorMinimoMaximo(0, 5);
            MsgBarra.Show();

            if (Ubicacion != 0)
            {
                StUbicacion = " and mov.idubicacion = " + Ubicacion;
            }

            if (IdBodega != 0)
            {
                StBodega = " and mov.idbodega = " + IdBodega;
            }

            // QUERY PARA CONSULTAR LAS TRANSACCIONES QUE NO SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.CpteTran,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))})})})} "
                                + " else {fn concat(tip.CpteTran,rtrim(doc.factura))} end not in (select {fn concat(compronte,rtrim(numero))} ";
                    break;
                default:
                    StCadConcat = " concat(tip.CpteTran,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))))) "
                                + " else concat(tip.CpteTran,rtrim(doc.factura)) end not in (select concat(compronte,rtrim(numero)) ";
                    break;
            }

            StBuilder.Append("select doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,SUM(mov.subtotal) as Subtotal,");
            StBuilder.Append("SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva,tip.CpteTran as cpte,tip.Descripcion,doc.estado ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos<>'Y' ");
            StBuilder.Append("and case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcat);
            StBuilder.Append(" from cnt_docmto where FECHA between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and (DEBITO<>0 or CREDITO<>0)) ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.CpteTran,tip.Descripcion,doc.estado");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocFaltantesInv(1)", ref Dsdata, "tbltransnopos");

            DsReporte.Tables.Add(Dsdata.Tables["tbltransnopos"].Copy());

            // QUERY PARA CONSULTAR LAS TRANSACCIONES DE COSTO QUE NO SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            MsgBarra.PerformStep();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.cptecost,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))})})})} "
                                + " else {fn concat(tip.cptecost,rtrim(doc.factura))} end not in (select {fn concat(compronte,rtrim(numero))} ";
                    break;
                default:
                    StCadConcat = " concat(tip.cptecost,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))))) "
                                + " else concat(tip.cptecost,rtrim(doc.factura)) end not in (select concat(compronte,rtrim(numero)) ";
                    break;
            }

            StBuilder.Replace(StBuilder.ToString(), "");

            StBuilder.Append("select doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,SUM(mov.subtotal) as Subtotal,");
            StBuilder.Append("SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva,tip.cptecost as cpte,tip.Descripcion,doc.estado ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos<>'Y' and tip.cptecost<>'9999' ");
            StBuilder.Append("and case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcat);
            StBuilder.Append(" from cnt_docmto where FECHA between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and (DEBITO<>0 or CREDITO<>0)) ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.cptecost,tip.Descripcion,doc.estado");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocFaltantesInv(2)", ref Dsdata, "tblcostosnopos");

            DsReporte.Tables.Add(Dsdata.Tables["tblcostosnopos"].Copy());

            // QUERY PARA CONSULTAR LAS TRANSACCIONES QUE SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            MsgBarra.PerformStep();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.CpteTran,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(RIGHT({fn CONCAT('00', RTRIM(year(FecIng)))}, 2),{fn concat(RIGHT({fn CONCAT('00', RTRIM(MONTH(FecIng)))}, 2),RIGHT({fn CONCAT('00', RTRIM(day(FecIng)))}, 2))})})})})} "
                                + " not in (select {fn concat(compronte,rtrim(numero))} ";
                    break;
                case "ORACLE":
                    StCadConcat = " concat(tip.CpteTran,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),to_char(doc.fecing,'yyMMdd')))) "
                                + " not in (select concat(compronte,rtrim(numero)) ";
                    break;
                default:
                    StCadConcat = " concat(tip.CpteTran,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(RIGHT(CONCAT('00', RTRIM(year(FecIng))), 2),concat(RIGHT(CONCAT('00', RTRIM(MONTH(FecIng))), 2),RIGHT(CONCAT('00', RTRIM(day(FecIng))), 2)))))) "
                                + " not in (select concat(compronte,rtrim(numero)) ";
                    break;
            }

            StBuilder.Replace(StBuilder.ToString(), "");

            StBuilder.Append("select doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,SUM(mov.subtotal) as Subtotal,");
            StBuilder.Append("SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva,tip.CpteTran as cpte,tip.Descripcion,doc.estado ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos='Y' ");
            StBuilder.Append("and " + StCadConcat);
            StBuilder.Append(" from cnt_docmto where FECHA between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and (DEBITO<>0 or CREDITO<>0)) ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.CpteTran,tip.Descripcion,doc.estado");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocFaltantesInv(3)", ref Dsdata, "tbltranspos");

            DsReporte.Tables.Add(Dsdata.Tables["tbltranspos"].Copy());

            // QUERY PARA CONSULTAR LAS TRANSACCIONES DE COSTOS QUE SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            MsgBarra.PerformStep();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.cptecost,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(RIGHT({fn CONCAT('00', RTRIM(year(FecIng)))}, 2),{fn concat(RIGHT({fn CONCAT('00', RTRIM(MONTH(FecIng)))}, 2),RIGHT({fn CONCAT('00', RTRIM(day(FecIng)))}, 2))})})})})} "
                                + " not in (select {fn concat(compronte,rtrim(numero))} ";
                    break;
                case "ORACLE":
                    StCadConcat = " concat(tip.cptecost,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),to_char(doc.fecing,'yyMMdd')))) "
                                + " not in (select concat(compronte,rtrim(numero)) ";
                    break;
                default:
                    StCadConcat = " concat(tip.cptecost,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(RIGHT(CONCAT('00', RTRIM(year(FecIng))), 2),concat(RIGHT(CONCAT('00', RTRIM(MONTH(FecIng))), 2),RIGHT(CONCAT('00', RTRIM(day(FecIng))), 2)))))) "
                                + " not in (select concat(compronte,rtrim(numero)) ";
                    break;
            }

            StBuilder.Replace(StBuilder.ToString(), "");

            StBuilder.Append("select doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,SUM(mov.subtotal) as Subtotal,");
            StBuilder.Append("SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva,tip.cptecost as cpte,tip.Descripcion,doc.estado ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos='Y' and tip.cptecost<>'9999' ");
            StBuilder.Append("and " + StCadConcat);
            StBuilder.Append(" from cnt_docmto where FECHA between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and (DEBITO<>0 or CREDITO<>0)) ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.cptecost,tip.Descripcion,doc.estado");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocFaltantesInv(4)", ref Dsdata, "tblcostopos");

            DsReporte.Tables.Add(Dsdata.Tables["tblcostopos"].Copy());

            if (DsReporte.Tables.Count != 0)
            {
                this.ImprimeInformeCuadreDocFaltan(DsReporte, Ubicacion, IdBodega, FecInicial, FecFinal, myforma, myconnect);
            }

            MsgBarra.Close();
            MsgBarra.Dispose();

            return DsReporte;
        }

        private void ImprimeInformeCuadreDocFaltan(DataSet dsreporte, double IdUbicacion, double IdBodega, DateTime FecIni, DateTime FecFin, Form myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.reporte msgrep = new ERP.Core.Compartido.Reportes.reporte("inv_fcuadreinv01b");
            ERP.Core.Compartido.Reportes.config_report config = new ERP.Core.Compartido.Reportes.config_report();
            DataSet dscompania = new DataSet();
            string StNomBodega = " ";
            string StNomUbicacion = " ";

            // this.msgsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, myconnect); // ERROR: CS1620

            if (IdUbicacion != 0)
            {
                // this.msginvconf.BuscaUbicacion(IdUbicacion, myconnect, ref StNomUbicacion); // ERROR: CS7036
            }
            else
            {
                StNomUbicacion = "Todas las Ubicaciones";
            }

            if (IdBodega != 0)
            {
                // this.msginvconf.BuscaBodega(IdBodega, IdUbicacion, myconnect, ref StNomBodega); // ERROR: CS1501
            }
            else
            {
                StNomBodega = "Todas las Bodegas";
            }

            // msgrep.Subreports[0].SetDataSource(dsreporte); // ERROR: CS1061
            // msgrep.Subreports[1].SetDataSource(dsreporte); // ERROR: CS1061
            // msgrep.Subreports[2].SetDataSource(dsreporte); // ERROR: CS1061
            // msgrep.Subreports[3].SetDataSource(dsreporte); // ERROR: CS1061
            msgrep.SetParameterValue("empresa", dscompania.Tables["tblcompania"].Rows[0]["nombre"]);
            msgrep.SetParameterValue("nit", dscompania.Tables["tblcompania"].Rows[0]["NIT"]);
            msgrep.SetParameterValue("direccion", dscompania.Tables["tblcompania"].Rows[0]["Direccion"]);
            msgrep.SetParameterValue("telefono", dscompania.Tables["tblcompania"].Rows[0]["TELEFONO"]);
            msgrep.SetParameterValue("IdUbicacion", IdUbicacion);
            msgrep.SetParameterValue("IdBodega", IdBodega);
            msgrep.SetParameterValue("NomUbicacion", StNomUbicacion);
            msgrep.SetParameterValue("NomBodega", StNomBodega);
            msgrep.SetParameterValue("fecinicio", FecIni);
            msgrep.SetParameterValue("fecfinal", FecFin);

            config.confi_reportes(myforma, msgrep);
        }

        public DataSet GeneraInformeDocDescuadradosInvCnt(double Ubicacion, double IdBodega, DateTime FecInicial, DateTime FecFinal, Form myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Informe Documentos Descuadrados", myforma);
            DataSet Dsdata = new DataSet();
            DataSet DsReporte = new DataSet();
            string StUbicacion = "";
            string StBodega = "";
            double fila = 0;
            string StCadConcat = "";
            string StCadConcatCampos = "";

            MsgBarra.ValorMinimoMaximo(0, 5);
            MsgBarra.Show();

            if (Ubicacion != 0)
            {
                StUbicacion = " and mov.idubicacion = " + Ubicacion;
            }

            if (IdBodega != 0)
            {
                StBodega = " and mov.idbodega = " + IdBodega;
            }

            // QUERY PARA CONSULTAR LAS TRANSACCIONES QUE NO SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.CpteTran,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))})})})} "
                                + " else {fn concat(tip.CpteTran,rtrim(doc.factura))} end = {fn concat(doccnt.compronte,rtrim(doccnt.numero))} ";
                    StCadConcatCampos = " {fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))})})} "
                                      + " else rtrim(doc.factura) ";
                    break;
                default:
                    StCadConcat = " concat(tip.CpteTran,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))))) "
                                + " else concat(tip.CpteTran,rtrim(doc.factura)) end = concat(doccnt.compronte,rtrim(doccnt.numero)) ";
                    StCadConcatCampos = " concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia)))) "
                                      + " else rtrim(doc.factura) ";
                    break;
            }

            StBuilder.Append("select doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.CpteTran as cpte,tip.Descripcion,doc.estado,doccnt.DEBITO,doccnt.CREDITO,");
            StBuilder.Append("case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcatCampos + " end as Documento,");
            StBuilder.Append("SUM(mov.subtotal) as Subtotal,SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("inner join cnt_docmto doccnt on case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcat);
            StBuilder.Append(" where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos<>'Y' ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.CpteTran,tip.Descripcion,doc.estado,doccnt.DEBITO,doccnt.CREDITO,");
            StBuilder.Append("case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcatCampos + " end ");
            StBuilder.Append("having (round(SUM(mov.neto),0)<>round(doccnt.DEBITO,0) or round(SUM(mov.neto),0)<>round(doccnt.CREDITO,0)) ");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocDescuadradosInvCnt(1)", ref Dsdata, "tbltransdescnopos");

            DsReporte.Tables.Add(Dsdata.Tables["tbltransdescnopos"].Copy());

            // QUERY PARA CONSULTAR LAS TRANSACCIONES DE COSTO QUE NO SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            MsgBarra.PerformStep();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.cptecost,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))})})})} "
                                + " else {fn concat(tip.cptecost,rtrim(doc.factura))} end = {fn concat(doccnt.compronte,rtrim(doccnt.numero))} ";
                    StCadConcatCampos = " {fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))})})} "
                                      + " else rtrim(doc.factura) ";
                    break;
                default:
                    StCadConcat = " concat(tip.cptecost,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia))))) "
                                + " else concat(tip.cptecost,rtrim(doc.factura)) end = concat(doccnt.compronte,rtrim(doccnt.numero)) ";
                    StCadConcatCampos = " concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(rtrim(doc.idtipomovto),rtrim(doc.secuencia)))) "
                                      + " else rtrim(doc.factura) ";
                    break;
            }

            StBuilder.Replace(StBuilder.ToString(), "");

            StBuilder.Append("select doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.cptecost as cpte,tip.Descripcion,doc.estado,doccnt.DEBITO,doccnt.CREDITO,");
            StBuilder.Append("case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcatCampos + " end as Documento,");
            StBuilder.Append("SUM(mov.subtotal) as Subtotal,SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("inner join cnt_docmto doccnt on case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcat);
            StBuilder.Append(" where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos<>'Y' and tip.cptecost<>'9999' ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by doc.idtipomovto,doc.secuencia,doc.factura,doc.FecIng,mov.pos,tip.cptecost,tip.Descripcion,doc.estado,doccnt.DEBITO,doccnt.CREDITO,");
            StBuilder.Append("case case tip.devolucion when 'Y' then 0 else doc.factura end when 0 then " + StCadConcatCampos + " end ");
            StBuilder.Append("having (round(SUM(mov.cantidad*mov.costo),0)<>round(doccnt.DEBITO,0) or round(SUM(mov.cantidad*mov.costo),0)<>round(doccnt.CREDITO,0)) ");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocDescuadradosInvCnt(2)", ref Dsdata, "tblcostosdescnopos");

            DsReporte.Tables.Add(Dsdata.Tables["tblcostosdescnopos"].Copy());

            // QUERY PARA CONSULTAR LAS TRANSACCIONES QUE SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            MsgBarra.PerformStep();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.CpteTran,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(RIGHT({fn CONCAT('00', RTRIM(year(FecIng)))}, 2),{fn concat(RIGHT({fn CONCAT('00', RTRIM(MONTH(FecIng)))}, 2),RIGHT({fn CONCAT('00', RTRIM(day(FecIng)))}, 2))})})})})} = {fn concat(doccnt.compronte,rtrim(doccnt.numero))} ";
                    StCadConcatCampos = " {fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(RIGHT({fn CONCAT('00', RTRIM(year(FecIng)))}, 2),{fn concat(RIGHT({fn CONCAT('00', RTRIM(MONTH(FecIng)))}, 2),RIGHT({fn CONCAT('00', RTRIM(day(FecIng)))}, 2))})})})} ";
                    break;
                case "ORACLE":
                    StCadConcat = " concat(tip.CpteTran,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),to_char(doc.fecing,'yyMMdd')))) = concat(doccnt.compronte,rtrim(doccnt.numero)) ";
                    StCadConcatCampos = " concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),to_char(doc.fecing,'yyMMdd'))) ";
                    break;
                default:
                    StCadConcat = " concat(tip.CpteTran,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(RIGHT(CONCAT('00', RTRIM(year(FecIng))), 2),concat(RIGHT(CONCAT('00', RTRIM(MONTH(FecIng))), 2),RIGHT(CONCAT('00', RTRIM(day(FecIng))), 2)))))) = concat(doccnt.compronte,rtrim(doccnt.numero)) ";
                    StCadConcatCampos = " concat(rtrim(doc.idpunto), concat(rtrim(doc.idturno),concat(RIGHT(CONCAT('00', RTRIM(year(FecIng))), 2),concat(RIGHT(CONCAT('00', RTRIM(MONTH(FecIng))), 2),RIGHT(CONCAT('00', RTRIM(day(FecIng))), 2))))) ";
                    break;
            }

            StBuilder.Replace(StBuilder.ToString(), "");

            StBuilder.Append("select tip.CpteTran," + StCadConcatCampos + " as Documento,doc.idpunto,doc.idturno,doc.FecIng,doccnt.DEBITO,doccnt.CREDITO,");
            StBuilder.Append("SUM(mov.subtotal) as Subtotal,SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("inner join cnt_docmto doccnt on " + StCadConcat);
            StBuilder.Append(" where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos='Y' ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by tip.CpteTran," + StCadConcatCampos + ",doc.FecIng,doc.idpunto,doc.idturno,doccnt.DEBITO,doccnt.CREDITO ");
            StBuilder.Append("having (round(SUM(mov.neto),0)<>round(doccnt.DEBITO,0) or round(SUM(mov.neto),0)<>round(doccnt.CREDITO,0))");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocDescuadradosInvCnt(3)", ref Dsdata, "tbltransdescpos");

            DsReporte.Tables.Add(Dsdata.Tables["tbltransdescpos"].Copy());

            // QUERY PARA CONSULTAR LAS TRANSACCIONES DE COSTOS QUE SEAN DEL POS Y NO ESTEN EN CONTABILIDAD

            MsgBarra.PerformStep();

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                    StCadConcat = " {fn concat(tip.cptecost,{fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(RIGHT({fn CONCAT('00', RTRIM(year(FecIng)))}, 2),{fn concat(RIGHT({fn CONCAT('00', RTRIM(MONTH(FecIng)))}, 2),RIGHT({fn CONCAT('00', RTRIM(day(FecIng)))}, 2))})})})})} = {fn concat(doccnt.compronte,rtrim(doccnt.numero))} ";
                    StCadConcatCampos = " {fn concat(rtrim(doc.idpunto),{fn concat(rtrim(doc.idturno),{fn concat(RIGHT({fn CONCAT('00', RTRIM(year(FecIng)))}, 2),{fn concat(RIGHT({fn CONCAT('00', RTRIM(MONTH(FecIng)))}, 2),RIGHT({fn CONCAT('00', RTRIM(day(FecIng)))}, 2))})})})} ";
                    break;
                case "ORACLE":
                    StCadConcat = " concat(tip.cptecost,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),to_char(doc.fecing,'yyMMdd')))) = concat(doccnt.compronte,rtrim(doccnt.numero)) ";
                    StCadConcatCampos = " concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),to_char(doc.fecing,'yyMMdd'))) ";
                    break;
                default:
                    StCadConcat = " concat(tip.cptecost,concat(rtrim(doc.idpunto),concat(rtrim(doc.idturno),concat(RIGHT(CONCAT('00', RTRIM(year(FecIng))), 2),concat(RIGHT(CONCAT('00', RTRIM(MONTH(FecIng))), 2),RIGHT(CONCAT('00', RTRIM(day(FecIng))), 2)))))) = concat(doccnt.compronte,rtrim(doccnt.numero)) ";
                    StCadConcatCampos = " concat(rtrim(doc.idpunto), concat(rtrim(doc.idturno),concat(RIGHT(CONCAT('00', RTRIM(year(FecIng))), 2),concat(RIGHT(CONCAT('00', RTRIM(MONTH(FecIng))), 2),RIGHT(CONCAT('00', RTRIM(day(FecIng))), 2))))) ";
                    break;
            }

            StBuilder.Replace(StBuilder.ToString(), "");

            StBuilder.Append("select tip.cptecost," + StCadConcatCampos + " as Documento,doc.FecIng,doc.idpunto,doc.idturno,doccnt.DEBITO,doccnt.CREDITO,");
            StBuilder.Append("SUM(mov.subtotal) as Subtotal,SUM(mov.neto) as neto, SUM(mov.cantidad*mov.costo) as Costo,SUM(mov.VlrIva) as Iva ");
            StBuilder.Append("from inv_docs doc ");
            StBuilder.Append("inner join inv_movtos mov on doc.idtipomovto=mov.idtipomovto and doc.secuencia=mov.secuencia ");
            StBuilder.Append("inner join inv_tipomovtos tip on doc.IdTipoMovto=tip.IdTipoMovto ");
            StBuilder.Append("inner join cnt_docmto doccnt on " + StCadConcat);
            StBuilder.Append(" where doc.FecIng between '" + Strings.Format(FecInicial, varini.PstForFec) + "' and '" + Strings.Format(FecFinal, varini.PstForFec) + "' and mov.pos='Y' and tip.cptecost<>'9999' ");
            StBuilder.Append(StUbicacion + StBodega);
            StBuilder.Append(" group by tip.cptecost," + StCadConcatCampos + ",doc.FecIng,doc.idpunto,doc.idturno,doccnt.DEBITO,doccnt.CREDITO ");
            StBuilder.Append("having (round(SUM(mov.cantidad*mov.costo),0)<>round(doccnt.DEBITO,0) or round(SUM(mov.cantidad*mov.costo),0)<>round(doccnt.CREDITO,0))");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeDocDescuadradosInvCnt(4)", ref Dsdata, "tblcostodescpos");

            DsReporte.Tables.Add(Dsdata.Tables["tblcostodescpos"].Copy());

            if (DsReporte.Tables.Count != 0)
            {
                this.ImprimeInformeCuadreDocDescuadradosInvCnt(DsReporte, Ubicacion, IdBodega, FecInicial, FecFinal, myforma, myconnect);
            }

            MsgBarra.Close();
            MsgBarra.Dispose();

            return DsReporte;
        }

        private void ImprimeInformeCuadreDocDescuadradosInvCnt(DataSet dsreporte, double IdUbicacion, double IdBodega, DateTime FecIni, DateTime FecFin, Form myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.reporte msgrep = new ERP.Core.Compartido.Reportes.reporte("inv_fcuadreinv01c");
            ERP.Core.Compartido.Reportes.config_report config = new ERP.Core.Compartido.Reportes.config_report();
            DataSet dscompania = new DataSet();
            string StNomBodega = " ";
            string StNomUbicacion = " ";

            // this.msgsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, myconnect); // ERROR: CS1620

            if (IdUbicacion != 0)
            {
                // this.msginvconf.BuscaUbicacion(IdUbicacion, myconnect, ref StNomUbicacion); // ERROR: CS7036
            }
            else
            {
                StNomUbicacion = "Todas las Ubicaciones";
            }

            if (IdBodega != 0)
            {
                // this.msginvconf.BuscaBodega(IdBodega, IdUbicacion, myconnect, ref StNomBodega); // ERROR: CS1501
            }
            else
            {
                StNomBodega = "Todas las Bodegas";
            }

            // msgrep.Subreports[0].SetDataSource(dsreporte); // ERROR: CS1061
            // msgrep.Subreports[1].SetDataSource(dsreporte); // ERROR: CS1061
            // msgrep.Subreports[2].SetDataSource(dsreporte); // ERROR: CS1061
            // msgrep.Subreports[3].SetDataSource(dsreporte); // ERROR: CS1061
            msgrep.SetParameterValue("empresa", dscompania.Tables["tblcompania"].Rows[0]["nombre"]);
            msgrep.SetParameterValue("nit", dscompania.Tables["tblcompania"].Rows[0]["NIT"]);
            msgrep.SetParameterValue("direccion", dscompania.Tables["tblcompania"].Rows[0]["Direccion"]);
            msgrep.SetParameterValue("telefono", dscompania.Tables["tblcompania"].Rows[0]["TELEFONO"]);
            msgrep.SetParameterValue("IdUbicacion", IdUbicacion);
            msgrep.SetParameterValue("IdBodega", IdBodega);
            msgrep.SetParameterValue("NomUbicacion", StNomUbicacion);
            msgrep.SetParameterValue("NomBodega", StNomBodega);
            msgrep.SetParameterValue("fecinicio", FecIni);
            msgrep.SetParameterValue("fecfinal", FecFin);

            config.confi_reportes(myforma, msgrep);
        }

        public bool ValidaCuentasInventario(string IdUbicacion, string IdBodega, string CodGrupo, string Cuenta, string TipoCuenta, System.Data.Odbc.OdbcConnection myconnect)
        {
            System.Text.StringBuilder StBuilder = new System.Text.StringBuilder();
            double Cantidad = 0;

            StBuilder.Append("select count(distinct(a." + TipoCuenta + ")) as campo1 ");
            StBuilder.Append("from inv_cuentas a ");
            StBuilder.Append("inner join inv_tipomovtos b on a.IdTipoMovto=b.IdTipoMovto ");
            StBuilder.Append("where a.idubicacion =" + IdUbicacion + " and a.idbodega =" + IdBodega + " ");
            StBuilder.Append("and a.IdGruProducto='" + CodGrupo + "' and a.IdTipoMovto not in (select TipMovAjuInv from inv_facturas) ");
            StBuilder.Append("and a." + TipoCuenta + "<>'" + Cuenta + "' ");

            // ok = this.MyOdbcConet.ExecuteQueryconec(StBuilder.ToString(), myconnect, "ValidaCuentasInventario", ref Cantidad); // ERROR: CS1503

            return true;
        }
    }
}
