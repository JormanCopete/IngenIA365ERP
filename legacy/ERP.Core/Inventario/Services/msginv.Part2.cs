using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Services
{
    public partial class msginv
    {
        // Methods from VB source lines ~1100-2400

        // GrabaInventario y CalculaCosotoProducto: duplicados eliminados (originales en msginv.cs:1371 y 1432)















































        private void GrabaMovtoTransaccion(double IdTipoMovto, double secuencia, double IdProducto, string Tipoventa, string ClaMovto, OdbcConnection myconect, DateTime FecMovto, double Cantidad, string NumFactura, decimal TasaIva, decimal TasaDscto, string IdCliente, double VlrIva,
            double Subtotal, double VlrDsto, double Neto, double VlrUnidad, string Idusuario, int Idpunto, int IdTurno,
            double Valadm, double IvaAdm, double IvaTiq, double OtroImp, double AeroPort, double ImpComb, double Costo, decimal retfte, decimal VlrRetfte,
            decimal TasaIca, decimal VlrIca, string MovtoPos)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();

            ok = this.BuscaMovtoTransaccion(IdTipoMovto, secuencia, IdProducto, Tipoventa, myconect);
            switch (ok)
            {
                case false:
                    stbuilder.Append("Insert into inv_movtos(IdTipoMovto,Secuencia,FecMovto,NumFactura,IdProducto,Cantidad,TasaIva,TasaDscto,FecSystem,IdCliente,VlrIva,VlrDsto,VlrUnidad,Idusuario,IdPunto,IdTurno,SubTotal,neto,Tipoventa,periodo,ClaMovto,");
                    stbuilder.Append("Valadm,IvaAdm,IvaTiq,OtroImp,AeroPort,costo,ImpComb,retfte,VlrRetfte,vlrica,tasaica,pos) values ('");
                    stbuilder.Append(IdTipoMovto + "','");
                    stbuilder.Append(secuencia + "','");
                    stbuilder.Append(Strings.Format(FecMovto, varini.PstForFec) + "','");
                    stbuilder.Append(NumFactura + "','");
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
                    stbuilder.Append(MovtoPos + "')");

                    this.MyOdbcConet.ExecuteQueryconec(stbuilder.ToString(), myconect, "GrabaMovimiento");
                    break;
                case true:
                    stbuilder.Append("update inv_movtos set ");
                    stbuilder.Append("Neto = Neto + " + Neto + ",");
                    stbuilder.Append("SubTotal =  SubTotal + " + Subtotal + ",");
                    stbuilder.Append("Cantidad = Cantidad + " + Cantidad + ",");
                    stbuilder.Append("VlrIva = VlrIva + " + VlrIva + ",");
                    stbuilder.Append("VlrDsto = VlrDsto + " + VlrDsto + ",");
                    stbuilder.Append("VlrRetfte = VlrRetfte + " + VlrRetfte + ",");
                    stbuilder.Append("vlrica = vlrica + " + VlrIca + ",");
                    stbuilder.Append("Pos = '" + MovtoPos + "' ");
                    stbuilder.Append(" where IdTipoMovto = " + IdTipoMovto + " and Secuencia = " + secuencia + " and IdProducto = " + IdProducto + " and Tipoventa = '" + Tipoventa + "'");
                    this.MyOdbcConet.ExecuteQueryconec(stbuilder.ToString(), myconect, "GrabaMovtoTransaccion");
                    break;
            }
        }

        // Overload without optional params
        private void GrabaMovtoTransaccion(double IdTipoMovto, double secuencia, double IdProducto, string Tipoventa, string ClaMovto, OdbcConnection myconect, DateTime FecMovto, double Cantidad, string NumFactura)
        {
            GrabaMovtoTransaccion(IdTipoMovto, secuencia, IdProducto, Tipoventa, ClaMovto, myconect, FecMovto, Cantidad, NumFactura, 0, 0, null, 0, 0, 0, 0, 0, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "N");
        }

        public virtual void GrabaMovtoTransaccion(double IdTipoMovto, double secuencia, string Cencosto, double Idbodega, double IdProducto, string Tipoventa, string ClaMovto, OdbcConnection myconect, DateTime FecMovto, double Cantidad, string NumFactura, decimal TasaIva, decimal TasaDscto, string IdCliente, double VlrIva,
            double Subtotal, double VlrDsto, double Neto, double VlrUnidad, string Idusuario, int Idpunto, int IdTurno,
            double Valadm, double IvaAdm, double IvaTiq, double OtroImp, double AeroPort, double ImpComb, double Costo, decimal retfte, decimal VlrRetfte,
            decimal TasaIca, decimal VlrIca, string MovtoPos)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            double ConsecMovto = 0;

            ConsecMovto = this.ConsecutivoItemsMovto(IdTipoMovto, secuencia, myconect);
            ok = false;
            //ok = this.BuscaMovtoTransaccion(IdTipoMovto, secuencia, Cencosto, Idbodega, IdProducto, Tipoventa, myconect);

            //Cencosto = Strings.Right("00000000" + Cencosto, 8);

            switch (ok)
            {
                case false:
                    stbuilder.Append("Insert into inv_movtos(IdTipoMovto,Secuencia,FecMovto,NumFactura,idubicacion,idbodega,IdProducto,Cantidad,TasaIva,TasaDscto,FecSystem,IdCliente,VlrIva,VlrDsto,VlrUnidad,Idusuario,IdPunto,IdTurno,SubTotal,neto,Tipoventa,periodo,ClaMovto,");
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
                    stbuilder.Append("update inv_movtos set ");
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

        // Overload without optional params
        public virtual void GrabaMovtoTransaccion(double IdTipoMovto, double secuencia, string Cencosto, double Idbodega, double IdProducto, string Tipoventa, string ClaMovto, OdbcConnection myconect, DateTime FecMovto, double Cantidad, string NumFactura)
        {
            GrabaMovtoTransaccion(IdTipoMovto, secuencia, Cencosto, Idbodega, IdProducto, Tipoventa, ClaMovto, myconect, FecMovto, Cantidad, NumFactura, 0, 0, null, 0, 0, 0, 0, 0, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "N");
        }

        private double ConsecutivoItemsMovto(double IdTipoMovto, double Secuencia, OdbcConnection myconnect)
        {
            double Consecutivo = 0;
            string _consecutivo = "0";
            stmysql = "select (max(consecmovto)+1) as campo1 from inv_movtos where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia;
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaMovtoTransaccion", ref _consecutivo);
            double.TryParse(_consecutivo, out Consecutivo);
            return Consecutivo;
        }

        private bool BuscaMovtoTransaccion(double IdTipoMovto, double Secuencia, double IdProducto, string Tipoventa, OdbcConnection myconect)
        {
            double Neto = 0;
            return BuscaMovtoTransaccion(IdTipoMovto, Secuencia, IdProducto, Tipoventa, myconect, ref Neto);
        }

        private bool BuscaMovtoTransaccion(double IdTipoMovto, double Secuencia, double IdProducto, string Tipoventa, OdbcConnection myconect, ref double Neto)
        {
            stmysql = "select Neto as campo1 from inv_movtos  where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia + " and IdProducto =" + IdProducto + " and TipoVenta = '" + Tipoventa + "'";
            string _neto1 = "0";
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaMovtoTransaccion", ref _neto1);
            double.TryParse(_neto1, out Neto);
            return ok;
        }

        private bool EliminaMovtoTransaccion(double IdTipoMovto, double Secuencia, double IdProducto, string Tipoventa, OdbcConnection myconect)
        {
            stmysql = "delete from inv_movtos  where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia + " and IdProducto =" + IdProducto + " and TipoVenta = '" + Tipoventa + "'";
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaMovtoTransaccion");
            return ok;
        }

        public virtual bool BuscaMovtoTransaccion(double IdTipoMovto, double Secuencia, string Cencosto, double Idbodega, double IdProducto, string Tipoventa, OdbcConnection myconect)
        {
            double Neto = 0;
            return BuscaMovtoTransaccion(IdTipoMovto, Secuencia, Cencosto, Idbodega, IdProducto, Tipoventa, myconect, ref Neto);
        }

        public virtual bool BuscaMovtoTransaccion(double IdTipoMovto, double Secuencia, string Cencosto, double Idbodega, double IdProducto, string Tipoventa, OdbcConnection myconect, ref double Neto)
        {
            stmysql = "select Neto as campo1 from inv_movtos  where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia + " and IdProducto =" + IdProducto + " and TipoVenta = '" + Tipoventa + "' and idubicacion='" + Cencosto + "' and idbodega=" + Idbodega;
            string _neto2 = "0";
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaMovtoTransaccion", ref _neto2);
            double.TryParse(_neto2, out Neto);
            return ok;
        }

        public virtual bool EliminaMovtoTransaccion(double IdTipoMovto, double Secuencia, string Cencosto, double Idbodega, double IdProducto, string Tipoventa, double ConsecMovto, OdbcConnection myconect)
        {
            stmysql = "delete from inv_movtos  where  IdTipoMovto =" + IdTipoMovto + " and Secuencia = " + Secuencia + " and IdProducto =" + IdProducto + " and TipoVenta = '" + Tipoventa + "' and idubicacion='" + Cencosto + "' and idbodega=" + Idbodega + " and consecmovto=" + ConsecMovto;
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaMovtoTransaccion");
            return ok;
        }

        public DataTable ResumenMovtoArqueos(int idPunto, int Idturno, DateTime Fecha, string Idusuario, OdbcConnection Myconnect)
        {
            DataSet DataPuntos = new DataSet();

            stmysql = "select  idproducto as Producto, Resumido as descripcion ,cantidad as cant,VlrUnidad as Unidad,VlrVendido as Total from inv_removtos_vw"
                     + " where idproducto <> '99999999' and idpunto = " + idPunto + " and fecMovto = '" + Strings.Format(Fecha, varini.PstForFec) + "' and Idturno = " + Idturno + " and Idusuario = '" + Idusuario + "'";
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, Myconnect, "ResumenMovtoArqueos", ref DataPuntos, "Tblrepuntos");

            return DataPuntos.Tables["Tblrepuntos"];
        }

        public void ResumenFormaPago(int Idpunto, int idturno, DateTime fecha, string idUsuario, OdbcConnection myconect,
            ref double Efectivo, ref double Credito, ref double TarDebito, ref double TarCredito,
            ref double Cheque, ref double Arqueos, ref double Cambio)
        {
            stmysql = " select efectivo as campo1,credito as campo2,tardebito as campo3,tarcredito as campo4  from inv_forpago_vw  where idpunto = " + Idpunto + " and idusuario = '" + idUsuario + "' and idturno = " + idturno + " and fecmovto = '" + Strings.Format(fecha, varini.PstForFec) + "'";
            string _efe = "0", _cre = "0", _tdb = "0", _tcr = "0";
            this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "ResumenFormaPago", ref _efe, ref _cre, ref _tdb, ref _tcr);
            double.TryParse(_efe, out Efectivo); double.TryParse(_cre, out Credito); double.TryParse(_tdb, out TarDebito); double.TryParse(_tcr, out TarCredito);
            stmysql = " select cheque as campo1, arqueos as campo2, Cambio as campo3  from inv_forpago_vw  where idpunto = " + Idpunto + " and idusuario = '" + idUsuario + "' and idturno = " + idturno + " and fecmovto = '" + Strings.Format(fecha, varini.PstForFec) + "'";
            string _chq = "0", _arq = "0", _cam = "0";
            // this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "ResumenFormaPago", ref _chq, ref _arq, ref _cam); // ERROR: CS1501
            double.TryParse(_chq, out Cheque); double.TryParse(_arq, out Arqueos); double.TryParse(_cam, out Cambio);
        }

        public bool BuscaTransaccionesAbiertas(int Idpunto, int idturno, DateTime fecha, OdbcConnection myconect, DataSet DsDataset)
        {
            System.Text.StringBuilder Stbuilder = new System.Text.StringBuilder();
            DataSet DsDataAbiertos = new DataSet();
            try
            {
                DsDataset.Tables.Remove("tblabiertos");
            }
            catch (Exception)
            {
            }

            Stbuilder.Append("select idtipomovto,secuencia,docs.idcliente,case cntnit.tipo_persona when  'N' then cntnit.nombre else cntnit.razon_social end as nombre,Vlrtotal,VlrIva,docs.estado from inv_docs docs ");
            Stbuilder.Append("inner join cnt_nit cntnit on docs.idcliente = cntnit.nit ");
            Stbuilder.Append("where docs.estado in  ('A','S') and docs.fecing = '" + fecha + "' ");
            Stbuilder.Append("and docs.idpunto = '" + Idpunto + "' and docs.idturno = '" + idturno + "'");

            this.MyOdbcConet.ExecuteQueryDataset(Stbuilder.ToString(), myconect, "BuscaTransaccionesAbiertas", ref DsDataAbiertos, "tblabiertos");
            if (DsDataAbiertos.Tables["tblabiertos"].Rows.Count > 0)
            {
                try
                {
                    DsDataset.Tables.Add(DsDataAbiertos.Tables["tblabiertos"].Copy());
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

        public bool CierreCaja(int Idpunto, int idturno, DateTime fecha, string idUsuario, string UsuarioCierre, double ValorCaja, OdbcConnection myconect, Form Myforma,
            ref string Cpte, ref double Consecutivo)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();

            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999";
            int canreg = 0, fila = 0;
            double VlrNetoVentas = 0;
            string CpteVentas = "9999";
            double CpteConseVentas = 0;
            string ConseCpte = Convert.ToString(Convert.ToDouble(Idpunto.ToString() + idturno.ToString() + Strings.Format(fecha, "yyMMdd")));
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999";
            int CtrlExistencia = 0;
            string Cptecartera = "9999", CuentaCreditos = "99999999999999", CuentaTurno = "999999999999";
            string NitTurno = "99999999999999";
            double debito = 0, credito = 0, Dif = 0;
            string CuentaCaja = "999999999999";
            string CpteCompras = "9999";
            double CpteConseCompras = 0;
            string StCuentaIva = "";

            stmysql = " select idgrupo, IdTipomovto, Subtotal, Neto ,Iva,Dsto,clapago,idcliente,idubicacion,idbodega,TasaIva from inv_cierrecaja_vw where IdPunto = '" + Idpunto + "' and  IdTurno = '" + idturno
                    + "' and FecMovto = '" + Strings.Format(fecha, varini.PstForFec) + "' and IdUsuario = '" + idUsuario + "'";

            msgbarra.DefineMaximo(stmysql, myconect);
            msgbarra.Show();

            Application.DoEvents();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "CierreCaja", ref myRead, "TblCierreCaja");
            canreg = myRead.Tables["TblCierreCaja"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCierreCaja"].Rows[fila];
                Application.DoEvents();
                StCuentaIva = "";
                // msginvconfig.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["idgrupo"], row["IdTipomovto"], myconect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto); // ERROR: CS1503, CS1620
                // msginvconfig.BuscaTipomovto(row["IdTipomovto"], myconect, ref CpteTran, ref CtrlExistencia); // ERROR: CS7036
                // msgcnt.BuscaComprobante(CpteTran, 0, false, myconect, ref CuentaCreditos); // ERROR: CS7036

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

                switch (CtrlExistencia)
                {
                    case 0:
                        CpteCompras = CpteTran;
                        CpteConseCompras = Convert.ToDouble(ConseCpte);

                        if (Convert.ToDouble(row["neto"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, ConseCpte, 0, row["neto"], 0, UsuarioCierre, myconect, "UC-" + ConseCpte, "POST", "UC", fecha); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["idgrupo"], row["IdTipomovto"], row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                            if (ok)
                            {
                                if (StCuentaIva.Trim() != "" && StCuentaIva.Trim() != "999999999999")
                                {
                                    CuentaIva = StCuentaIva.Trim();
                                }
                            }
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaIva, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", row["Iva"], 0, Convert.ToDouble(row["Subtotal"]) * -1, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Dsto"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Cuentadsto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", row["Dsto"], 0, row["Dsto"], UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", row["Subtotal"], 0, row["Subtotal"], UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) == 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", row["neto"], 0, row["neto"], UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }
                        break;

                    case 1:
                        switch (Convert.ToInt32(row["clapago"]))
                        {
                            case 0:
                                if (Convert.ToDouble(row["neto"]) > 0)
                                {
                                    VlrNetoVentas += Convert.ToDouble(row["neto"]);
                                    CpteVentas = CpteTran;
                                    CpteConseVentas = Convert.ToDouble(ConseCpte);
                                    CuentaCaja = Neto;
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", row["neto"], 0, 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                                }
                                break;
                            case 3:
                                if (Convert.ToDouble(row["neto"]) > 0)
                                {
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaCreditos, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", row["neto"], 0, 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                                }
                                break;
                        }

                        if (Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["idgrupo"], row["IdTipomovto"], row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                            if (ok)
                            {
                                if (StCuentaIva.Trim() != "" && StCuentaIva.Trim() != "999999999999")
                                {
                                    CuentaIva = StCuentaIva.Trim();
                                }
                            }
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaIva, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", 0, row["Iva"], row["Subtotal"], UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Dsto"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Cuentadsto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", 0, row["Dsto"], row["Dsto"], UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, VentasGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", 0, row["Subtotal"], row["Subtotal"], UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) == 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, ventasNoGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, "cierre de cajas No." + Idpunto, " ", 0, row["neto"], row["neto"], UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        }
                        break;
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            if (VlrNetoVentas > 0)
            {
                // msgsys.BuscaUsuario(idUsuario, myconect, ref NitTurno); // ERROR: CS1615, CS1620
                // msginvconf.BuscaTurno(Idpunto, myconect, ref CuentaTurno); // ERROR: CS7036
                Dif = VlrNetoVentas - ValorCaja;
                if (Dif > 0)
                {
                    credito = Dif;
                    debito = 0;
                }
                else
                {
                    debito = Dif * -1;
                    credito = 0;
                }

                if (Dif != 0)
                {
                    // msgcnt.GrabaMovimiento(CpteVentas, CpteConseVentas, CuentaCaja, "9999", Strings.Format(fecha, "yyyyMM"), NitTurno, fecha, "cierre de cajas No." + Idpunto, " ", debito, credito, 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                    // msgcnt.GrabaMovimiento(CpteVentas, CpteConseVentas, CuentaTurno, "9999", Strings.Format(fecha, "yyyyMM"), NitTurno, fecha, "cierre de cajas No." + Idpunto, " ", credito, debito, 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                }
            }

            Cpte = CpteTran;
            Consecutivo = Convert.ToDouble(ConseCpte);
            ok = msgcnt.CierreDocumento(CpteVentas, CpteConseVentas, myconect);
            ok = msgcnt.CierreDocumento(CpteCompras, CpteConseCompras, myconect);

            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();
            return ok;
        }

        // Overload without ref params
        public bool CierreCaja(int Idpunto, int idturno, DateTime fecha, string idUsuario, string UsuarioCierre, double ValorCaja, OdbcConnection myconect, Form Myforma)
        {
            string Cpte = "9999";
            double Consecutivo = 0;
            return CierreCaja(Idpunto, idturno, fecha, idUsuario, UsuarioCierre, ValorCaja, myconect, Myforma, ref Cpte, ref Consecutivo);
        }

        public void CierraCpteCreditos(int Idpunto, int idturno, DateTime fecha, string usuario, OdbcConnection myconect)
        {
            string CpteCartera = " ";
            string ConseCpte = Convert.ToString(Convert.ToDouble(Idpunto.ToString() + idturno.ToString() + Strings.Format(fecha, "yyMMdd")));
            // this.msginvconf.BuscaDatosFacturacion(1, myconect, ref CpteCartera); // ERROR: CS7036
            // msgcop.TrasladaContabilidad(CpteCartera, ConseCpte, myconect, usuario); // ERROR: CS1503
        }

        public bool ActualizaContabilidad(int IdTipomovto, double Secuencia, DateTime fecha, string Usuario, OdbcConnection myconect, Form Myforma,
            bool Anulacion)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            DataSet dsdatosDocs = new DataSet();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999", StCuentaIva = "";
            string ConseCpte;
            int canreg = 0, fila = 0;
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999", CuentaRetFte = "999999999999", CtaIca = "999999999999";
            int CtrlExistencia = 0;
            string ClaDoc = "9999", Cptecartera = "9999", CuentaCreditos = "99999999999999", cencos = "99999999";

            stmysql = "select IdGruProducto,invdoc.factura,clapago,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,sum(invmovto.cantidad) as cantidad, sum(invmovto.vlrIva) as Iva ,sum(invmovto.Vlrdsto) as dsto,"
                    + "sum(invmovto.costo) as costo,sum(invmovto.costo * invmovto.cantidad) as costotot, sum(invmovto.vlrUnidad) as VlrUnidad,sum(invmovto.subtotal) as  Subtotal,sum(invmovto.neto) as Neto, sum(invmovto.VlrRetfte) as VlrRetfte, sum(invmovto.Vlrica) as VlrIca"
                    + ",sum(CASE WHEN invmovto.vlrIva <> 0 THEN invmovto.subtotal ELSE 0 END)as Vlr_base,sum(CASE WHEN invmovto.VlrRetfte <> 0 THEN invmovto.subtotal ELSE 0 END)as VlrBaseRet "
                    + ",sum(CASE WHEN invmovto.Vlrica <> 0 THEN invmovto.subtotal ELSE 0 END)as VlrBaseIca,invmovto.tasaiva,invmovto.tasaica,invmovto.retfte,invmovto.IdPunto,invmovto.IdTurno "
                    + "from inv_movtos invmovto inner join inv_docs invdoc on invmovto.idtipomovto = invdoc.idtipomovto "
                    + "and invmovto.secuencia = invdoc.secuencia inner join inv_productos invpro on invmovto.idproducto = invpro.idproducto "
                    + "where invmovto.idtipoMovto = " + IdTipomovto + " And invmovto.secuencia = " + Secuencia
                    + " group by IdGruProducto,invdoc.factura,clapago,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,invmovto.TasaIva,invmovto.tasaica,invmovto.retfte,invmovto.IdPunto,invmovto.IdTurno";

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "ActualizaContabilidad", ref myRead, "TblActContab");
            canreg = myRead.Tables["TblActContab"].Rows.Count;

            Application.DoEvents();

            msgbarra.ValorMinimoMaximo(0, canreg);
            msgbarra.Show();

            ConseCpte = "0";

            // msginvconfig.BuscaTipomovto(IdTipomovto, myconect, ref CpteTran, ref CtrlExistencia, ref ClaDoc); // ERROR: CS7036
            // msgcnt.BuscaComprobante(CpteTran, 0, false, myconect, ref CuentaCreditos); // ERROR: CS7036

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblActContab"].Rows[fila];
                Application.DoEvents();
                StCuentaIva = "";
                // msginvconfig.BuscaGrupo(row["IdGruProducto"], myconect, ref cencos); // ERROR: CS7036
                // msginvconfig.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, myconect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto, ref CuentaRetFte, ref CtaIca, row["tasaiva"], row["tasaica"], row["retfte"]); // ERROR: CS1503

                if (Anulacion)
                {
                    ConseCpte = Convert.ToString(row["IdPunto"]) + Convert.ToString(row["IdTurno"]) + IdTipomovto + Secuencia;
                }
                else
                {
                    if (Information.IsNumeric(row["factura"]))
                    {
                        if (Convert.ToDouble(row["factura"]) != 0)
                        {
                            ConseCpte = Convert.ToString(row["factura"]);
                        }
                        else
                        {
                            if (ConseCpte == "0")
                            {
                                ConseCpte = Convert.ToString(row["IdPunto"]) + Convert.ToString(row["IdTurno"]) + IdTipomovto + Secuencia;
                            }
                        }
                    }
                    else
                    {
                        if (ConseCpte == "0")
                        {
                            ConseCpte = Convert.ToString(row["IdPunto"]) + Convert.ToString(row["IdTurno"]) + IdTipomovto + Secuencia;
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

                row["detalle"] = Strings.Mid(Convert.ToString(row["detalle"]), 1, 80);

                switch (CtrlExistencia)
                {
                    case 0:
                        if (Convert.ToDouble(row["neto"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, 0, row["neto"], 0, Usuario, myconect, "UC-" + ConseCpte, "POST", row["idcliente"], cencos, "UC", fecha); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                            if (ok)
                            {
                                if (StCuentaIva.Trim() != "" && StCuentaIva.Trim() != "999999999999")
                                {
                                    CuentaIva = StCuentaIva.Trim();
                                }
                            }
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaIva, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["Iva"], 0, Convert.ToDouble(row["Vlr_base"]) * -1, Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Dsto"]) > 0)
                        {
                            //Jaime solicita cambio de debito a credito
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Cuentadsto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["Dsto"], row["Dsto"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["Subtotal"], 0, row["Subtotal"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) == 0)
                        {
                            if (Convert.ToDouble(row["Dsto"]) > 0 || Convert.ToDouble(row["Vlrica"]) > 0)
                            {
                                // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["Subtotal"], 0, row["Subtotal"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                            }
                            else
                            {
                                // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["neto"], 0, row["neto"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                            }
                        }

                        if (Convert.ToDouble(row["VlrRetfte"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaRetFte, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["VlrRetfte"], row["VlrBaseRet"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                        }

                        if (Convert.ToDouble(row["Vlrica"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CtaIca, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["VlrIca"], row["VlrBaseIca"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                        }
                        break;

                    case 1:
                        switch (ClaDoc)
                        {
                            case "5":
                                if (Convert.ToDouble(row["costo"]) > 0)
                                {
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, row["costotot"], 0, 0, Usuario, myconect, "UC-" + ConseCpte, "POST", row["idcliente"], cencos, "UC", fecha); // ERROR: CS1503
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, ventasNoGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["costotot"], 0, Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                }
                                break;
                            default:
                                switch (Convert.ToInt32(row["clapago"]))
                                {
                                    case 0:
                                        if (Convert.ToDouble(row["neto"]) > 0)
                                        {
                                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, row["neto"], 0, 0, Usuario, myconect, "UC-" + ConseCpte, "POST", row["idcliente"], cencos, "UC", fecha); // ERROR: CS1503
                                        }
                                        break;
                                    case 3:
                                        if (Convert.ToDouble(row["neto"]) > 0)
                                        {
                                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaCreditos, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, row["neto"], 0, 0, Usuario, myconect, "UC-" + ConseCpte, "POST", row["idcliente"], cencos, "UC", fecha); // ERROR: CS1503
                                        }
                                        break;
                                }

                                if (Convert.ToDouble(row["Iva"]) > 0)
                                {
                                    // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, row["tasaiva"], 1, myconect, ref StCuentaIva); // ERROR: CS1503
                                    if (ok)
                                    {
                                        if (StCuentaIva.Trim() != "" && StCuentaIva.Trim() != "999999999999")
                                        {
                                            CuentaIva = StCuentaIva.Trim();
                                        }
                                    }
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaIva, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["Iva"], row["Vlr_base"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                }

                                if (Convert.ToDouble(row["Dsto"]) > 0)
                                {
                                    //Jaime solicita cambio de credito a debito
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Cuentadsto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["Dsto"], 0, row["Dsto"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                    //REGISTRA EL CREDITO DEL DESCUENTO AL INGRESO
                                    if (Convert.ToDouble(row["neto"]) == 0)
                                    {
                                        // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, VentasGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["Dsto"], row["Dsto"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                    }
                                }

                                if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) > 0)
                                {
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, VentasGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["Subtotal"], row["Subtotal"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                }

                                if (Convert.ToDouble(row["neto"]) > 0 && Convert.ToDouble(row["Iva"]) == 0)
                                {
                                    if (Convert.ToDouble(row["Dsto"]) > 0)
                                    {
                                        // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, ventasNoGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["Subtotal"], row["Subtotal"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                    }
                                    else
                                    {
                                        // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, ventasNoGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["neto"], row["neto"], Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                    }
                                }

                                if (Convert.ToDouble(row["VlrRetfte"]) > 0)
                                {
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CuentaRetFte, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["VlrRetfte"], 0, Convert.ToDouble(row["VlrBaseRet"]) * -1, Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                }
                                if (Convert.ToDouble(row["Vlrica"]) > 0)
                                {
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CtaIca, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["VlrIca"], 0, Convert.ToDouble(row["VlrBaseIca"]) * -1, Usuario, myconect, "", "POST", row["idcliente"], cencos); // ERROR: CS1503
                                }
                                break;
                        }
                        break;
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            // ok = msgcnt.CierreDocumento(CpteTran, ConseCpte, myconect); // ERROR: CS1503
            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        // Overload without Anulacion
        public bool ActualizaContabilidad(int IdTipomovto, double Secuencia, DateTime fecha, string Usuario, OdbcConnection myconect, Form Myforma)
        {
            return ActualizaContabilidad(IdTipomovto, Secuencia, fecha, Usuario, myconect, Myforma, false);
        }

        public bool ActualizaContabilidadInvFisico(int IdTipomovto, double Secuencia, DateTime fecha, string Usuario, OdbcConnection myconect, Form Myforma,
            bool Anulacion)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            DataSet DsDataSet = new DataSet();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999";
            string ConseCpte;
            int canreg = 0, fila = 0;
            string StGrupo = " ";
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999", CuentaRetFte = "999999999999";
            int CtrlExistencia = 0;
            string Cptecartera = "9999", CuentaPuente = "99999999999999";
            ConseCpte = Strings.Format(fecha, "yyMMdd");

            // msginvconfig.BuscaTipomovto(IdTipomovto, myconect, ref CpteTran, ref CtrlExistencia); // ERROR: CS7036
            // ok = msgcnt.BuscaComprobante(CpteTran, ConseCpte, false, myconect, ref CuentaPuente); // ERROR: CS7036
            if (ok)
            {
                MessageBox.Show("Inventario fisico ya fue contabilizado");
                return false;
            }

            stmysql = "select IdGruProducto,invdoc.idcliente,invdoc.detalle,invmovto.clamovto,invmovto.idubicacion,invmovto.idbodega,invpro.ClaIva,sum(invmovto.cantidad) as cantidad, sum(invmovto.vlrIva) as Iva ,sum(invmovto.Vlrdsto) as dsto,"
                    + "sum(invmovto.vlrUnidad) as VlrUnidad,sum(invmovto.subtotal) as  Subtotal,sum(invmovto.neto) as Neto, sum(invmovto.VlrRetfte) as VlrRetfte "
                    + "from inv_movtos invmovto inner join inv_docs invdoc on invmovto.idtipomovto = invdoc.idtipomovto "
                    + "and invmovto.secuencia = invdoc.secuencia inner join inv_productos invpro on invmovto.idproducto = invpro.idproducto "
                    + "where invmovto.idtipoMovto = " + IdTipomovto + " And invmovto.secuencia = " + Secuencia
                    + " group by IdGruProducto,invdoc.idcliente,invdoc.detalle,invmovto.clamovto,invmovto.idubicacion,invmovto.idbodega,invpro.ClaIva";

            msgbarra.DefineMaximo(stmysql, myconect);
            msgbarra.Show();

            Application.DoEvents();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "ActualizaContabilidadInvFisico", ref myRead, "TblActContab");
            canreg = myRead.Tables["TblActContab"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblActContab"].Rows[fila];
                Application.DoEvents();

                // msginvconfig.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, myconect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto, ref CuentaRetFte); // ERROR: CS7036

                if (CpteTran == "9999")
                {
                    MessageBox.Show("Comprobante de la linea " + row["IdTipomovto"] + " no esta parametrizado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                row["detalle"] = Strings.Mid(Convert.ToString(row["detalle"]), 1, 80);

                switch (Convert.ToString(row["clamovto"]))
                {
                    case "C":
                        if (Convert.ToInt32(row["ClaIva"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["neto"], 0, row["neto"], Usuario, myconect, "", "POST"); // ERROR: CS1503
                        }
                        else
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", row["neto"], 0, row["neto"], Usuario, myconect, "", "POST"); // ERROR: CS1503
                        }

                        if (Neto != "999999999999")
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, 0, row["neto"], 0, Usuario, myconect, "UC-" + ConseCpte, "POST", "UC", fecha); // ERROR: CS1503
                        }
                        else
                        {
                            if (Convert.ToInt32(row["ClaIva"]) > 0)
                            {
                                if (CostoGravado != "999999999999")
                                {
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CostoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, 0, row["neto"], 0, Usuario, myconect, "UC-" + ConseCpte, "POST", "UC", fecha); // ERROR: CS1503
                                }
                            }
                            else
                            {
                                if (CostoNoGravado != "999999999999")
                                {
                                    // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CostoNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, 0, row["neto"], 0, Usuario, myconect, "UC-" + ConseCpte, "POST", "UC", fecha); // ERROR: CS1503
                                }
                            }
                        }
                        break;

                    case "V":
                        if (Neto != "999999999999")
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, Neto, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, row["neto"], 0, 0, Usuario, myconect, "UC-" + ConseCpte, "POST", "UC", fecha); // ERROR: CS1503
                        }

                        if (Convert.ToInt32(row["ClaIva"]) > 0)
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["neto"], row["neto"], Usuario, myconect, "", "POST"); // ERROR: CS1503
                            if (VentasGrabadas != "999999999999")
                            {
                                // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, VentasGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["neto"], row["neto"], Usuario, myconect, "", "POST"); // ERROR: CS1503
                            }
                            if (CostoGravado != "999999999999")
                            {
                                // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CostoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, row["neto"], 0, 0, Usuario, myconect, "UC-" + ConseCpte, "POST", "UC", fecha); // ERROR: CS1503
                            }
                        }
                        else
                        {
                            // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["neto"], row["neto"], Usuario, myconect, "", "POST"); // ERROR: CS1503
                            if (ventasNoGrabadas != "999999999999")
                            {
                                // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, ventasNoGrabadas, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], " ", 0, row["neto"], row["neto"], Usuario, myconect, "", "POST"); // ERROR: CS1503
                            }
                            if (CostoNoGravado != "999999999999")
                            {
                                // msgcnt.GrabaMovimiento(CpteTran, ConseCpte, CostoNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"], fecha, row["detalle"], ConseCpte, row["neto"], 0, 0, Usuario, myconect, "UC-" + ConseCpte, "POST", "UC", fecha); // ERROR: CS1503
                            }
                        }
                        break;
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            // ok = msgcnt.CierreDocumento(CpteTran, ConseCpte, myconect); // ERROR: CS1503

            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        // Overload without Anulacion
        public bool ActualizaContabilidadInvFisico(int IdTipomovto, double Secuencia, DateTime fecha, string Usuario, OdbcConnection myconect, Form Myforma)
        {
            return ActualizaContabilidadInvFisico(IdTipomovto, Secuencia, fecha, Usuario, myconect, Myforma, false);
        }

        public bool CierreCostos(int Idpunto, int idturno, DateTime fecha, string idUsuario, string UsuarioCierre, OdbcConnection myconect, Form Myforma,
            ref string Cpte, ref double Consecutivo, bool DesdePos)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999";
            string ConseCpte = Convert.ToString(Convert.ToDouble(Idpunto.ToString() + idturno.ToString() + Strings.Format(fecha, "yyMMdd")));
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999";
            double Vlrcosto = 0;
            int canreg = 0, fila = 0;
            string StWhere = "";

            if (DesdePos)
            {
                StWhere = " and costo.pos='Y' ";
            }
            else
            {
                StWhere = " and costo.pos<>'Y' ";
            }

            stmysql = " select idgrupo,costo.IdTipomovto, TasaIva, CostoPro,tipomov.ctrlfactura,costo.idubicacion,costo.idbodega from inv_GruCostoPro_vw costo inner join inv_tipomovtos tipomov on costo.idtipomovto = tipomov.idtipomovto "
                    + " inner join inv_facturas fac on tipomov.ctrlfactura = fac.IdCodigo "
                    + "where IdPunto = '" + Idpunto + "' and  IdTurno = '" + idturno + "' and tipomov.CtrlExistencia = '1' and fac.actcostos=1 "
                    + " and FecMovto = '" + Strings.Format(fecha, varini.PstForFec) + "' and IdUsuario = '" + idUsuario + "'" + StWhere;
            msgbarra.DefineMaximo(stmysql, myconect);
            msgbarra.Show();
            Application.DoEvents();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "CierreCostos", ref myRead, "TblCierreCostos");
            canreg = myRead.Tables["TblCierreCostos"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCierreCostos"].Rows[fila];
                Application.DoEvents();
                // msginvconfig.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["idgrupo"], row["IdTipomovto"], myconect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto); // ERROR: CS1503, CS1620
                // msginvconfig.BuscaTipomovto(row["IdTipomovto"], myconect, ref CpteCost); // ERROR: CS7036
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
                        // msgcnt.GrabaMovimiento(CpteCost, ConseCpte, CostoGravado, "9999", Strings.Format(fecha, "yyyyMM"), "99999999999999", fecha, "cierre de cajas No." + Idpunto, " ", row["CostoPro"], 0, 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        // msgcnt.GrabaMovimiento(CpteCost, ConseCpte, InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), "99999999999999", fecha, "cierre de cajas No." + Idpunto, " ", 0, row["CostoPro"], 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                    }
                    else
                    {
                        // msgcnt.GrabaMovimiento(CpteCost, ConseCpte, CostoNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), "99999999999999", fecha, "cierre de cajas No." + Idpunto, " ", row["CostoPro"], 0, 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                        // msgcnt.GrabaMovimiento(CpteCost, ConseCpte, InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), "99999999999999", fecha, "cierre de cajas No." + Idpunto, " ", 0, row["CostoPro"], 0, UsuarioCierre, myconect, "", "POST"); // ERROR: CS1503
                    }
                }
                fila += 1;
                msgbarra.PerformStep();
            }
            Cpte = CpteCost;
            Consecutivo = Convert.ToDouble(ConseCpte);
            // ok = msgcnt.CierreDocumento(CpteCost, ConseCpte, myconect); // ERROR: CS1503
            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        // Overload without ref/optional params
        public bool CierreCostos(int Idpunto, int idturno, DateTime fecha, string idUsuario, string UsuarioCierre, OdbcConnection myconect, Form Myforma)
        {
            string Cpte = "9999";
            double Consecutivo = 0;
            return CierreCostos(Idpunto, idturno, fecha, idUsuario, UsuarioCierre, myconect, Myforma, ref Cpte, ref Consecutivo, true);
        }

        public void GrabaCliente(Form Myforma, OdbcConnection myconec, ref string Nit, ref string Nombre, ref string Direccion, ref string Telefono, ref string email)
        {
            // frmclientes FrmCliente = new frmclientes(myconec); // ERROR: CS0246
            // FrmCliente.ShowDialog(Myforma); // ERROR: CS0103
            // Nit = FrmCliente.TxtCliente.Text; // ERROR: CS0103
            // Nombre = FrmCliente.TxtNombre.Text; // ERROR: CS0103
            // Direccion = FrmCliente.txtDireccion.Text; // ERROR: CS0103
            // Telefono = FrmCliente.TxtTelefono.Text; // ERROR: CS0103
            // email = FrmCliente.txtEmail.Text; // ERROR: CS0103
            // FrmCliente.Close(); // ERROR: CS0103
            // FrmCliente.Dispose(); // ERROR: CS0103
        }

        // Overload without ref params
        public void GrabaCliente(Form Myforma, OdbcConnection myconec)
        {
            string Nit = null, Nombre = null, Direccion = null, Telefono = null, email = null;
            GrabaCliente(Myforma, myconec, ref Nit, ref Nombre, ref Direccion, ref Telefono, ref email);
        }

        public void CalculaCuotas(int Linea, int Periodicidad, double ValorPres, OdbcConnection Myconnect, ref double Cuota, int Plazo, decimal tasaInteres, decimal IntSeg, decimal IntAdm, string soloundescuento)
        {
            int Clacuo = 0, ClaseI = 0, Foradmon = 0;
            string Cencos = " ";
            decimal TasaInt = 0;
            int PlazoLinea = 0;
            // msgcop.BuscaLinea(Linea, Myconnect, ref Clacuo, ref ClaseI, ref Cencos, ref TasaInt, ref Foradmon, ref PlazoLinea); // ERROR: CS7036
            if (Plazo == 0)
            {
                Plazo = PlazoLinea;
            }

            if (Plazo <= 0)
            {
                Plazo = 1;
            }

            switch (Periodicidad)
            {
                case 4:
                    TasaInt = Math.Round(((TasaInt / 30) * 7) / 100, 8);
                    Plazo = Convert.ToInt32((Plazo * 52) / 12);
                    break;
                default:
                    TasaInt = Math.Round((TasaInt / Periodicidad) / 100, 8);
                    Plazo = Plazo * Periodicidad;
                    break;
            }

            if (soloundescuento == "Y")
            {
                Plazo = 1;
            }

            if (tasaInteres != 0)
            {
                TasaInt = tasaInteres;
            }

            switch (Clacuo)
            {
                case 1:
                    Cuota = Convert.ToDouble(Strings.FormatNumber(Microsoft.VisualBasic.Financial.Pmt((double)(TasaInt + IntSeg + IntAdm), Plazo, -ValorPres), 0));
                    break;
                case 2:
                    Cuota = Math.Round(Convert.ToDouble(ValorPres / Plazo), 0);
                    break;
            }
        }

        // Overload without optional params
        public void CalculaCuotas(int Linea, int Periodicidad, double ValorPres, OdbcConnection Myconnect, ref double Cuota)
        {
            CalculaCuotas(Linea, Periodicidad, ValorPres, Myconnect, ref Cuota, 0, 0, 0, 0, "N");
        }

        private bool GrabaTransaccion(double IdTipoMovto, double Secuencia, OdbcConnection myconect, string IdCliente, DateTime FecIng,
            double VlrTotal, double VlrSubTotal, double VlrDsto, double VlrIva, string Estado, string IdUsuario, int ClaPago,
            double VlrEfectivo, double VlrCredito, double VlrTarDebito, double VlrTarCredito, int Idpunto,
            int IdTurno, double VlrArqueo, string detalle, DateTime FecVence, decimal VlrRetfte,
            decimal VlrIca, bool DesdeConv, string NumFacturaConv, int idvendedor, int idtipomovdev, double secuenciaDev)
        {
            double ConseFact = 0;
            int Cladoc = 9;
            string codfactura = "0";
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();

            ok = this.BuscaTransaccion(IdTipoMovto, Secuencia, myconect);
            switch (ok)
            {
                case false:
                    if (DesdeConv)
                    {
                        ConseFact = Convert.ToDouble(NumFacturaConv);
                    }
                    else
                    {
                        // this.msginvconf.BuscaTipomovto(IdTipoMovto, myconect, ref Cladoc, ref codfactura); // ERROR: CS7036
                        if (Cladoc == 0 || Cladoc == 2)
                        {
                            this.msginvconf.BuscaConseFactura(codfactura, myconect, ref ConseFact);
                        }
                    }
                    stbuilder.Append("insert into inv_docs(IdTipoMovto,Secuencia,IdCliente,FecIng,VlrTotal,vlrSubTotal,VlrDsto,VlrIva,Estado,IdUsuario,ClaPago,VlrEfectivo,VlrCredito,VlrTarDebito,VlrTarCredito,IdPunto,IdTurno,VlrArqueos,detalle,Factura,fecvence,VlrRetfte,vlrica,idVendedor,idtipomovdev,secuenciadev) values ('");
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
                    stbuilder.Append("update inv_docs set ");
                    stbuilder.Append("VlrTotal= VlrTotal + '" + VlrTotal + "',");
                    stbuilder.Append("VlrSubTotal = VlrSubTotal + '" + VlrSubTotal + "',");
                    stbuilder.Append("VlrDsto= VlrDsto + '" + VlrDsto + "',");
                    stbuilder.Append("VlrIva = VlrIva + '" + VlrIva + "',");
                    stbuilder.Append("vlrica = vlrica + '" + VlrIca + "',");
                    stbuilder.Append("VlrRetfte = VlrRetfte + '" + VlrRetfte + "', ");
                    stbuilder.Append("detalle = '" + detalle + "' ");
                    stbuilder.Append("where IdTipoMovto = " + IdTipoMovto + " and Secuencia = " + Secuencia);
                    break;
            }

            ok = this.MyOdbcConet.ExecuteQueryconec(stbuilder.ToString(), myconect, "GrabaTransaccion");
            return ok;
        }

        // Overload without optional params
        private bool GrabaTransaccion(double IdTipoMovto, double Secuencia, OdbcConnection myconect)
        {
            return GrabaTransaccion(IdTipoMovto, Secuencia, myconect, null, new DateTime(2007, 1, 1), 0, 0, 0, 0, "A", null, 0, 0, 0, 0, 0, 0, 0, 0, null, new DateTime(1950, 1, 1), 0, 0, false, "0", 1, 0, 0);
        }

        public void BuscaSecuencia(double IdTipoMovto, OdbcConnection myconnect, ref double Secuencia)
        {
            stmysql = "select  Secuencia + 1 as campo1 from  inv_tipomovtos  where IdTipoMovto = " + IdTipoMovto;
            // this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaSecuencia", ref Secuencia); // ERROR: CS1503
            stmysql = "update inv_tipomovtos  set Secuencia =" + Secuencia + " where IdTipoMovto =" + IdTipoMovto;
            this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaSecuencia");
        }

        public bool BuscaTransaccion(double IdTipoMovto, double Secuencia, OdbcConnection myconect)
        {
            string _dummy1 = null, _dummy2 = null, _dummy3 = null, _dummy4 = null, _dummy5 = " ";
            double _d1 = 0, _d2 = 0, _d3 = 0, _d4 = 0, _d5 = 0, _d6 = 0, _d7 = 0, _d8 = 0, _d9 = 0;
            string _s1 = " ", _s2 = " ", _s3 = "";
            int _i1 = 0, _i2 = 0, _i3 = 0;
            decimal _dec1 = 0;
            double _d10 = 0;
            string _ced = "99999999999999";
            // return BuscaTransaccion(IdTipoMovto, Secuencia, myconect, ref _dummy1, ref _dummy2, ref _dummy3, ref _dummy4, ref _dummy5, // ERROR: CS1501
                // ref _d1, ref _d2, ref _d3, ref _d4, ref _d5, ref _d6, ref _d7, ref _d8, ref _d9, // ERROR: CS1501
                // ref _s1, ref _d10, ref _i1, ref _i2, ref _i3, ref _s2, ref _s3, ref _dec1, ref _ced); // ERROR: CS1501
            return false;
        }

        public bool BuscaTransaccion(double IdTipoMovto, double Secuencia, OdbcConnection myconect, ref string IdCliente,
            ref string IdPunto, ref string IdTurno, ref string FecIng, ref string IdUsuario,
            ref double Efectivo, ref double TarjDebito, ref double TarjCredito, ref double Cheque, ref double Cuotas,
            ref double ValTotal, ref double NumFactura, ref double VlrDsto, ref double VlrIva,
            ref string detalle, ref double Subtotal, ref int ClaPago,
            ref int Periodicidad, ref int Plazo, ref int clades, ref string fecdsto,
            ref string estado, ref decimal TasaInt, ref string cedula)
        {
            double cuota = 0;
            double VlrRetfte = 0, VlrIca = 0;
            int idvendedor = 0;
            return BuscaTransaccion(IdTipoMovto, Secuencia, myconect, ref IdCliente, ref IdPunto, ref IdTurno, ref FecIng, ref IdUsuario,
                ref Efectivo, ref TarjDebito, ref TarjCredito, ref Cheque, ref Cuotas,
                ref ValTotal, ref NumFactura, ref VlrDsto, ref VlrIva,
                ref detalle, ref Subtotal, ref ClaPago,
                ref Periodicidad, ref Plazo, ref clades, ref fecdsto,
                ref cuota, ref TasaInt, ref estado, ref VlrRetfte,
                ref VlrIca, ref idvendedor, ref cedula);
        }

        public bool BuscaTransaccion(double IdTipoMovto, double Secuencia, OdbcConnection myconect, ref string IdCliente,
            ref string IdPunto, ref string IdTurno, ref string FecIng, ref string IdUsuario,
            ref double Efectivo, ref double TarjDebito, ref double TarjCredito, ref double Cheque, ref double Cuotas,
            ref double ValTotal, ref double NumFactura, ref double VlrDsto, ref double VlrIva,
            ref string detalle, ref double Subtotal, ref int ClaPago,
            ref int Periodicidad, ref int Plazo, ref int clades, ref string fecdsto,
            ref double cuota, ref decimal TasaInt, ref string estado, ref double VlrRetfte,
            ref double VlrIca, ref int idvendedor, ref string cedula)
        {
            double vlr = 0;
            DataTable TablaDatos = new DataTable();

            stmysql = "select IdCliente, IdPunto, IdTurno, FecIng,IdUsuario,VlrEfectivo,VlrTarDebito, VlrTarCredito ,VlrCheque,VlrCredito,"
                    + "VlrTotal,Factura,Vlrdsto,VlrIva,Detalle,VlrSubtotal,clapago, periodicidad, plazo, clades,fecdsto, cuota, Tasa,estado,"
                    + "Vlrretfte,vlrica,idVendedor  from inv_docs  where IdTipoMovto = " + IdTipoMovto + " and Secuencia =" + Secuencia;
            if (this.MyOdbcConet.ExecuteConsulta(stmysql, myconect, "BuscaTransaccion", ref TablaDatos) == true)
            {
                DataRow row = TablaDatos.Rows[0];
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

                ok = true;
                stmysql = "select cedula as campo1 from inv_vendedor  where idVendedor = " + idvendedor;
                this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "BuscaTransaccion", ref cedula);
            }
            else
            {
                ok = false;
            }
            TablaDatos.Dispose();

            return ok;
        }

        public void GrabaEstado(double IdTipoMovto, double Secuencia, OdbcConnection myconect, string estado, string Detalle)
        {
            string Stdetalle = "";

            if (Detalle.Trim() != "")
            {
                Stdetalle = ",Detalle='" + Detalle + "' ";
            }

            stmysql = "update inv_docs set estado = '" + estado + "' " + Stdetalle
                    + " where IdTipoMovto = " + IdTipoMovto + " and Secuencia =" + Secuencia;
            this.MyOdbcConet.ExecuteQueryconec(stmysql, myconect, "GrabaEstado");
        }

        // Overload with defaults
        public void GrabaEstado(double IdTipoMovto, double Secuencia, OdbcConnection myconect, string estado)
        {
            GrabaEstado(IdTipoMovto, Secuencia, myconect, estado, "");
        }

        public void GrabaEstado(double IdTipoMovto, double Secuencia, OdbcConnection myconect)
        {
            GrabaEstado(IdTipoMovto, Secuencia, myconect, "A", "");
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
            try
            {
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                while (Myread.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myread["campo1"] is DBNull)
                        {
                            Campo1 = "0";
                        }
                        else
                        {
                            Campo1 = Myread["campo1"].ToString().Trim();
                        }
                    }
                    if (Campo2 != "")
                    {
                        if (Myread["campo2"] is DBNull)
                        {
                            Campo2 = "0";
                        }
                        else
                        {
                            Campo2 = Myread["campo2"].ToString().Trim();
                        }
                    }
                    if (Campo3 != "")
                    {
                        if (Myread["campo3"] is DBNull)
                        {
                            Campo3 = "0";
                        }
                        else
                        {
                            Campo3 = Myread["campo3"].ToString().Trim();
                        }
                    }
                    if (Campo4 != "")
                    {
                        if (Myread["campo4"] is DBNull)
                        {
                            Campo4 = "0";
                        }
                        else
                        {
                            Campo4 = Myread["campo4"].ToString().Trim();
                        }
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

        // Overload without campos
        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento)
        {
            string c1 = "", c2 = "", c3 = "", c4 = "";
            return ExecuteQueryconec(stMysql, appadoConect, NombreProcedimiento, ref c1, ref c2, ref c3, ref c4);
        }

        public DataSet ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref DataSet DsDataSet, string Nomtabla)
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

        private void ImprimeCabeza(PrintPageEventArgs ev)
        {
            string Titulos;
            float leftMargin = 5;

            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            Font printTitt = new Font("Times New Roman", 9, FontStyle.Bold);
            Font printTitulos = new Font("Times New Roman", 9, FontStyle.Bold);

            Rectangle Linea01 = new Rectangle(new Point(5, 30), new Size(320, 30));
            Rectangle Linea02 = new Rectangle(new Point(5, 65), new Size(320, 15));
            Rectangle Linea03 = new Rectangle(new Point(5, 80), new Size(320, 15));
            Rectangle Linea04 = new Rectangle(new Point(5, 100), new Size(320, 15));
            Rectangle Linea05 = new Rectangle(new Point(5, 140), new Size(320, 15));
            Rectangle Linea06 = new Rectangle(new Point(5, 160), new Size(320, 15));
            Rectangle Linea07 = new Rectangle(new Point(5, 180), new Size(320, 15));
            Rectangle Linea08 = new Rectangle(new Point(5, 220), new Size(320, 15));
            StringFormat drawFormat = new StringFormat(StringFormatFlags.NoClip);

            drawFormat.LineAlignment = StringAlignment.Near;
            drawFormat.Alignment = StringAlignment.Near;

            ev.Graphics.DrawString(this.datoscierre.NomRescomp.Trim(), printFont, Brushes.Black, (RectangleF)Linea01, drawFormat);
            ev.Graphics.DrawString(this.datoscierre.nit.Trim() + " " + "REGIMEN COMUN", printFont, Brushes.Black, (RectangleF)Linea02, drawFormat);
            ev.Graphics.DrawString(this.datoscierre.Direccion.Trim(), printFont, Brushes.Black, (RectangleF)Linea03, drawFormat);
            ev.Graphics.DrawString("TELF." + this.datoscierre.Telefono.Trim(), printFont, Brushes.Black, (RectangleF)Linea04, drawFormat);

            string line = "CIERRE DE CAJA - ARQUEOS ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea05, drawFormat);
            line = "Caja  :" + Strings.Right("000000" + this.datoscierre.Caja, 6) + "     " + "Turno :       " + Strings.Right("0000" + this.datoscierre.Turno, 4);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea06, drawFormat);
            line = "Fecha :" + Strings.Format(this.datoscierre.Fecha, "yyyy-MMM-dd") + " " + "Hora:" + this.datoscierre.Hora;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea07, drawFormat);

            ev.Graphics.DrawString("Vendedor :" + this.datoscierre.Vendedor, printFont, Brushes.Black, leftMargin, 220, new StringFormat());

            if (forma_Impr_Ticket_Cierre == "D")
            {
                string Lin2 = Strings.Replace(Strings.Space(60), Strings.Space(1), "-");
                ev.Graphics.DrawString(Lin2, printFont, Brushes.Black, leftMargin, 260, new StringFormat());
                Titulos = "Item       Descripcion              Cant    Total ";
                ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, leftMargin, 280, new StringFormat());
                Titulos = Lin2;
                ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, leftMargin, 300, new StringFormat());
            }
        }

        public void ImprimeMovimientos(PrintPageEventArgs ev, ref float TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            string line = null;
            double total = 0, efectivo = 0, arqueo = 0, tarjdebito = 0, tarjcredito = 0, cheque = 0, arqueos = 0;
            double credito = 0, Cambio = 0, baseVal = 0;
            float leftMargin = 5;

            TopMargen += 20;
            line = "-------------[MOVIMIENTOS]-------------";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());

            this.ResumenFormaPago(Convert.ToInt32(this.datoscierre.Caja), Convert.ToInt32(this.datoscierre.Turno), this.datoscierre.Fecha, this.datoscierre.IdUsuario, Myconnect, ref efectivo, ref credito, ref tarjdebito, ref tarjcredito, ref cheque, ref arqueos, ref Cambio);
            // this.msginvconf.BuscaPunto(Convert.ToInt32(this.datoscierre.Caja), Convert.ToInt32(this.datoscierre.Turno), this.datoscierre.Fecha, Myconnect, ref baseVal); // ERROR: CS7036

            if (baseVal != 0)
            {
                TopMargen += 20;
                line = "BASE                       " + Strings.Right(Strings.Space(12) + Strings.Format(baseVal, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
            }
            if (efectivo != 0)
            {
                TopMargen += 20;
                line = "Efectivo                   " + Strings.Right(Strings.Space(12) + Strings.Format(efectivo, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
            }
            if (cheque != 0)
            {
                TopMargen += 20;
                line = "Cheque                     " + Strings.Right(Strings.Space(12) + Strings.Format(cheque, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
            }
            if (tarjdebito != 0)
            {
                TopMargen += 20;
                line = "Tarj. Debito               " + Strings.Right(Strings.Space(12) + Strings.Format(tarjdebito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
            }
            if (tarjcredito != 0)
            {
                TopMargen += 20;
                line = "Tarj. Credito              " + Strings.Right(Strings.Space(12) + Strings.Format(tarjcredito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
            }
            if (credito != 0)
            {
                TopMargen += 20;
                line = "Diferidos Cuotas           " + Strings.Right(Strings.Space(12) + Strings.Format(credito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
            }
            if (arqueo != 0)
            {
                TopMargen += 20;
                line = "Arqueos                    " + Strings.Right(Strings.Space(12) + Strings.Format(arqueo, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
            }

            total = efectivo + tarjdebito + tarjcredito + cheque + baseVal + arqueo + credito;
            TopMargen += 20;
            line = "TOTAL                      " + Strings.Right(Strings.Space(12) + Strings.Format(total, "$###,###,##0"), 12);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());

            TopMargen += 10;
            line = "   ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
        }

        private void cierre_PrintPage(object sender, PrintPageEventArgs ev)
        {
            OdbcConnection myconect = new OdbcConnection(varini.pstMyconec);
            myconect.Open();
            double ypos;
            float topMargin = 320;
            double count = 0;
            float leftMargin = 5;
            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            Font printTitt = new Font("Times New Roman", 9, FontStyle.Bold);
            Font printTitulos = new Font("Times New Roman", 9, FontStyle.Bold);
            double Total = 0, Cant = 0;
            string line = null;

            int CantidadFilas;
            DataSet dataset_RecorreProducto = new DataSet();

            if (entrar_cierre_PrintPage == true && entro_ImprimeMovimientos == false && entro_ImprimeBasesIva == false)
            {
                ImprimeCabeza(ev);
                recorrer_cierre_PrintPage = 0;
                CantProd_cierre_PrintPage = 0;
                Total_cierre_PrintPage = 0;
            }
            else
            {
                topMargin = 3;
            }

            string stmysqlLocal = "select  idproducto as Producto, Resumido as descripcion ,cantidad as cant,VlrUnidad as Unidad,VlrVendido as Total from inv_removtos_vw"
                                + " where idproducto <> '99999999' and idpunto = " + datoscierre.Caja + " and fecMovto  ='" + Strings.Format(datoscierre.Fecha, varini.PstForFec) + "'  and Idturno = " + datoscierre.Turno + " and Idusuario = '" + datoscierre.IdUsuario + "'"
                                + " and ctrlexistencia = 1";

            MyOdbcConet.ExecuteQueryDataset(stmysqlLocal, myconect, "cierre_PrintPage", ref dataset_RecorreProducto, "cierre");

            CantidadFilas = dataset_RecorreProducto.Tables["cierre"].Rows.Count;

            ypos = 0;
            while (recorrer_cierre_PrintPage < dataset_RecorreProducto.Tables["cierre"].Rows.Count)
            {
                DataRow row = dataset_RecorreProducto.Tables["cierre"].Rows[recorrer_cierre_PrintPage];
                if (forma_Impr_Ticket_Cierre == "D")
                {
                    if ((ypos + 45) < 4500)
                    {
                        ypos = topMargin + count * printFont.GetHeight(ev.Graphics);

                        line = Strings.Right(Strings.Space(6) + Convert.ToString(row["Producto"]), 6) + " ";
                        ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)ypos, new StringFormat());

                        line = Strings.Left(Convert.ToString(row["descripcion"]) + Strings.Space(12), 12) + " ";
                        ev.Graphics.DrawString(line, printFont, Brushes.Black, 50, (float)ypos, new StringFormat());

                        line = Strings.Right(Strings.Space(4) + Convert.ToString(row["Cant"]), 4) + "  ";
                        ev.Graphics.DrawString(line, printFont, Brushes.Black, 160, (float)ypos, new StringFormat());

                        line = Strings.Right(Strings.Space(8) + Strings.Format(row["Total"], "###,###"), 8);
                        ev.Graphics.DrawString(line, printFont, Brushes.Black, 190, (float)ypos, new StringFormat());

                        CantProd_cierre_PrintPage += Convert.ToDouble(row["Cant"]);
                        Total_cierre_PrintPage += Convert.ToDouble(row["Total"]);

                        count += 1;
                        recorrer_cierre_PrintPage += 1;
                        entrar_cierre_PrintPage = true;
                    }
                    else
                    {
                        entrar_cierre_PrintPage = false;
                        break;
                    }
                }
                else
                {
                    CantProd_cierre_PrintPage += Convert.ToDouble(row["Cant"]);
                    Total_cierre_PrintPage += Convert.ToDouble(row["Total"]);
                    count += 1;
                    recorrer_cierre_PrintPage += 1;
                }
            }
            if (forma_Impr_Ticket_Cierre == "D")
            {
                if (recorrer_cierre_PrintPage >= CantidadFilas && entrar_cierre_PrintPage == true)
                {
                    entrar_cierre_PrintPage = true;
                    line = null;
                }
                else
                {
                    entrar_cierre_PrintPage = false;
                }

                if (!(line == null) || 4500 <= (ypos + 45))
                {
                    ev.HasMorePages = true;
                    entrar_cierre_PrintPage = false;
                    ypos_historial = ypos;
                }
                else
                {
                    ev.HasMorePages = false;
                    entrar_cierre_PrintPage = true;
                }
            }
            else
            {
                entrar_cierre_PrintPage = true;
                ypos = 220;
            }

            if (entrar_cierre_PrintPage == true)
            {
                ypos += 20;
                ev.Graphics.DrawString(Lin, printFont, Brushes.Black, leftMargin, (float)ypos, new StringFormat());

                ypos += 20;
                line = "         Total ....  " + Strings.Right(Strings.Space(6) + Strings.Format(CantProd_cierre_PrintPage, "##,###"), 6) + " $" + Strings.Right(Strings.Space(9) + Strings.Format(Total_cierre_PrintPage, "#####,###"), 9);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)ypos, new StringFormat());

                myconect.Close();
                myconect.Open();

                float yposFloat = (float)ypos;
                ImprimeMovimientos(ev, ref yposFloat, myconect);
                ypos = yposFloat;

                //ImprimeBasesIva entra cuando haya terminado todos los proceso
                // en ImprimeMovimientos
                float yposFloat2 = (float)ypos;
                ImprimeBasesIva(ev, ref yposFloat2, myconect);
                ypos = yposFloat2;
            }

            myconect.Close();
            myconect.Dispose();
        }

        private void Calcular_Heihgt_PaperSize(ref double topMarginParam, string formaImpresion)
        {
            double ypos;
            string line = null;
            OdbcConnection myconect = new OdbcConnection(varini.pstMyconec);

            if (formaImpresion == "D")
            {
                myconect.Open();

                string stmysqlLocal = "select  idproducto as Producto, Resumido as descripcion ,cantidad as cant,VlrUnidad as Unidad,VlrVendido as Total from inv_removtos_vw"
                                    + " where idproducto <> '99999999' and idpunto = " + datoscierre.Caja + " and fecMovto  ='" + Strings.Format(datoscierre.Fecha, varini.PstForFec) + "'  and Idturno = " + datoscierre.Turno + " and Idusuario = '" + datoscierre.IdUsuario + "'"
                                    + " and ctrlexistencia = 1";

                OdbcCommand mycommand = new OdbcCommand(stmysqlLocal, myconect);
                OdbcDataReader myread = mycommand.ExecuteReader();

                ypos = topMarginParam;
                while (myread.Read())
                {
                    line = Strings.Right("      " + Convert.ToString(myread["Producto"]), 6) + " " + Strings.Left(Convert.ToString(myread["descripcion"]) + "                   ", 19) + " "
                          + Strings.Right("   " + Convert.ToString(myread["Cant"]), 3) + "  " + Strings.Right("       " + Strings.Format(myread["Total"], "###,###"), 7);

                    if (line == null)
                    {
                        break;
                    }
                    ypos += 20;
                }

                myconect.Close();
                myread.Close();
                mycommand.Dispose();
            }
            else
            {
                ypos = 0;
            }
            topMarginParam = ypos + 1500;
        }

        public void ImprimeTiketCierre(string IdUsuario, int IdPunto, int Idturno, DateTime Fecing, OdbcConnection myConnect, string formaImpresion)
        {
            ERP.Core.Inventario.Services.clsmsgtiket msgtiket = new ERP.Core.Inventario.Services.clsmsgtiket();
            msgtiket.ImprimeTiketCierre(IdUsuario, IdPunto, Idturno, Fecing, myConnect, formaImpresion);
        }

        public void BuscaDatosCierre(string IdUsuario, int IdPunto, int Idturno, DateTime Fecing, OdbcConnection myConnect)
        {
            string Nomusu = "admin";
            string Nomres = " ", Nit = " ", Direccion = " ", Telefono = " ";

            double NumFactura = 0;
            string Resolucion = " ";
            DateTime FecResol = DateTime.MinValue;
            int RegimenIva = 0;
            string Prefijo = " ", NumInicial = " ", NumFinal = " ";

            // msginvconf.BuscaDatosFacturacion(varini.sptCodEmpr, myConnect, ref Resolucion, ref FecResol, ref RegimenIva, ref Prefijo, ref NumInicial, ref NumFinal); // ERROR: CS7036
            // msgsys.BuscarCompania(varini.sptCodEmpr, myConnect, ref Nit, ref Direccion, ref Nomres); // ERROR: CS7036
            // msgsys.BuscaUsuario(IdUsuario, myConnect, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref Nomusu); // ERROR: CS1501

            datoscierre.Caja = IdPunto.ToString();
            datoscierre.Direccion = Direccion;
            datoscierre.Factura = NumFactura.ToString();
            datoscierre.Fecha = Fecing;
            datoscierre.Hora = Strings.Format(DateTime.Now, "hh:ss tt");
            datoscierre.nit = Nit;
            datoscierre.IdUsuario = IdUsuario;
            datoscierre.NomRescomp = Nomres;
            datoscierre.Telefono = Telefono;
            datoscierre.Turno = Idturno.ToString();
            datoscierre.Vendedor = Nomusu;
            datoscierre.Resolucion = Resolucion;
            datoscierre.FecResol = FecResol;
            datoscierre.RegimenIva = RegimenIva;
            datoscierre.NumInicial = NumInicial;
            datoscierre.NumFinal = NumFinal;
            datoscierre.Prefijo = Prefijo;
        }

        public void TrasladaNuevoCiclo(string PeriodoActual, string PeriodoNuevo, OdbcConnection Myconnect, Form myforma)
        {
            double Costo = 0, UltCosto = 0;

            ERP.Core.Compartido.Controles.Barraprogress BarraProgres = new ERP.Core.Compartido.Controles.Barraprogress("Traslado nuevo ciclo", myforma);
            int canreg = 0, fila = 0;
            stmysql = "select Idproducto,CantFinal,Costo,UltCostoPro,idbodega,idubicacion from inv_ctrlInv  where idperiodo = '" + PeriodoActual + "'";

            BarraProgres.DefineMaximo(stmysql, Myconnect);
            BarraProgres.Show();

            DataSet myread = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, Myconnect, "TrasladaNuevoCiclo", ref myread, "TblTrasladoCiclo");
            canreg = myread.Tables["TblTrasladoCiclo"].Rows.Count;

            for (fila = 0; fila <= myread.Tables["TblTrasladoCiclo"].Rows.Count - 1; fila++)
            {
                DataRow row = myread.Tables["TblTrasladoCiclo"].Rows[fila];
                this.GrabaInventario(Convert.ToDouble(row["Idproducto"]), Convert.ToString(row["idubicacion"]), Convert.ToDouble(row["idbodega"]), Convert.ToDouble(row["CantFinal"]), 0, 0, Convert.ToDouble(row["Costo"]), Convert.ToDouble(row["Costo"]), Convert.ToDouble(row["UltCostoPro"]), PeriodoNuevo, Myconnect);
                BarraProgres.PerformStep();
            }

            myread.Dispose();
            BarraProgres.Close();
            BarraProgres.Dispose();
        }

        public void ImprimeBasesIva(PrintPageEventArgs ev, ref float TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            int reg = 0, tfila2 = 0;
            string line;
            float leftMargin = 5;
            TopMargen += 10;
            string stHeader = "----------[RESUMEN POR TASA]-----------";
            ev.Graphics.DrawString(stHeader, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());

            stmysql = "select fecMovto,idpunto,idturno,idusuario,TasaIva,CtrlExistencia,Base,Iva from inv_retasaiva_vw"
                    + " where idpunto = " + datoscierre.Caja + " and fecMovto = '" + Strings.Format(datoscierre.Fecha, varini.PstForFec) + "' and Idturno = " + datoscierre.Turno + " and Idusuario = '" + datoscierre.IdUsuario + "'"
                    + " and ctrlexistencia = 1";

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, Myconnect, "ImprimeBasesIva", ref myRead, "TblImpBaseIva");
            reg = myRead.Tables["TblImpBaseIva"].Rows.Count;

            while (tfila2 < reg)
            {
                DataRow row = myRead.Tables["TblImpBaseIva"].Rows[tfila2];

                TopMargen += 20;
                string stLine = "Base Iva            " + Strings.Format(row["TasaIva"], "###") + "% " + Strings.Right("               " + Strings.Format(row["Base"], "###,###,###"), 15);
                ev.Graphics.DrawString(stLine, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
                TopMargen += 20;
                stLine = "                    " + "    " + Strings.Right("               " + Strings.Format(row["Iva"], "###,###,###"), 15);
                ev.Graphics.DrawString(stLine, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
                tfila2 += 1;
            }
            TopMargen += 60;
            line = "-";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, TopMargen, new StringFormat());
        }
    }
}
