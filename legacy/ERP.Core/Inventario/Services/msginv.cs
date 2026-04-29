using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Services
{
    public partial class msginv
    {
        // ── Fields ──────────────────────────────────────────────────────────
        private OdbcDataAdapter MyDataAdater = new OdbcDataAdapter();
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Inventario.Services.ClsInvConfig msginvconf = new ERP.Core.Inventario.Services.ClsInvConfig();
        private ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgsyscop = new ERP.Core.CarteraFinanciera.Models.ParamCop();

        private string stmysql;
        private bool ok;
        private string Lin = "---------------------------------------";

        private stdatos datoscierre;

        private bool entrar_cierre_PrintPage = true;
        private int recorrer_cierre_PrintPage = 0;
        private double CantProd_cierre_PrintPage = 0, Total_cierre_PrintPage = 0;
        private bool entro_ImprimeMovimientos = false;
        private bool entro_ImprimeBasesIva = false;
        private int validProceso_ImprimeMovimientos = 0;
        private int validaProceso_ImprimeBasesIva = 0;
        private double ypos_historial = 0;
        private int tfila = 0;
        private int validar_tfila = 1;
        private string forma_Impr_Ticket_Cierre = "D";

        // ── Struct ──────────────────────────────────────────────────────────
        public struct stdatos
        {
            public string NomRescomp;
            public string NombreComp;
            public string nit;
            public string Direccion;
            public string Telefono;
            public string Factura;
            public string Vendedor;
            public string Caja;
            public string Turno;
            public DateTime Fecha;
            public string Hora;
            public string DescTipoMovto;
            public string Prefijo;
            public string Resolucion;
            public int RegimenIva;
            public DateTime FecResol;
            public string NumInicial;
            public string NumFinal;
            public string IdUsuario;
        }

        // ── ExecSql helpers ─────────────────────────────────────────────────
        private void ExecSql(string sql, OdbcConnection conn, string proc)
        {
            string _a = " ", _b = " ", _c = " ", _d = " ";
            this.MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref _a, ref _b, ref _c, ref _d);
        }

        private bool ExecSql1(string sql, OdbcConnection conn, string proc, ref string c1)
        {
            string _b = " ", _c = " ", _d = " ";
            return this.MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref _b, ref _c, ref _d);
        }

        private bool ExecSql2(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2)
        {
            string _c = " ", _d = " ";
            return this.MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref _c, ref _d);
        }

        private bool ExecSql3(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2, ref string c3)
        {
            string _d = " ";
            return this.MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref _d);
        }

        // ── ConsultaPuntos ──────────────────────────────────────────────────
        public DataSet ConsultaPuntos(OdbcConnection Myconect, DateTime Fecini, DateTime fecfin)
        {
            DataSet DataPuntos = new DataSet();

            string sql = "select puntos.IdPunto as Punto, puntos.idturno,puntos.IdUsuario as Usuario, puntos.IdFecha as Fecha,movto.cantidad,movto.VlrVendido as Vendido, case puntos.estado  when 1 then 'Cerrado' when 2 then 'Detenido' else 'Activo' end  as Est "
                       + "from inv_puntos puntos left join inv_movtos_vw movto on puntos.IdUsuario = movto.IdUsuario and "
                       + "puntos.idpunto = movto.idpunto and puntos.idturno = movto.idturno and puntos.IdFecha = movto.fecmovto "
                       + "where  puntos.idfecha between '" + Strings.Format(Fecini, varini.PstForFec) + "' and '" + Strings.Format(fecfin, varini.PstForFec) + "'";

            this.MyOdbcConet.ExecuteQueryDataset(sql, Myconect, "ConsultaPuntos", DataPuntos, "Tblpuntos");
            return DataPuntos;
        }

        // ── CargaFormapago ──────────────────────────────────────────────────
        public bool CargaFormapago(double Subtotal, double Dsto, double Iva, double Neto,
            Form MyForma, int CodTransa, ref double Secuencia, string nit, string Usuario,
            int Idturno, int IdPunto, DateTime FecMovto, OdbcConnection myconec,
            string MovtoPos, double VlrTotal, double VlrSubTotal, double VlrDsto,
            double VlrIva, string Estado, double VlrArqueo, string detalle, double Factura,
            DateTime FecVence, decimal VlrRetfte, decimal VlrIca, bool idVendedor,
            int idtipomovdev, double secuenciaDev)
        {
            double Total = 0, Efectivo = 0, TarDebito = 0, TarCredito = 0, Credito = 0, Cheque = 0;
            int ClasePago = 0;

            // Inv_frmforpag Formapago = new Inv_frmforpag(myconec); // ERROR: CS0246
            // Formapago.StartPosition = FormStartPosition.CenterScreen; // ERROR: CS0103
            // Formapago.txtIva.Text = Iva.ToString(); // ERROR: CS0103
            // Formapago.TxtSubtotal.Text = Subtotal.ToString(); // ERROR: CS0103
            // Formapago.txtNeto.Text = Neto.ToString(); // ERROR: CS0103
            // Formapago.txtDsto.Text = Dsto.ToString(); // ERROR: CS0103
            // Formapago.txtTotal.Text = Neto.ToString(); // ERROR: CS0103
            // Formapago.Owner = MyForma; // ERROR: CS0103
            // Formapago.FormPago.CodTransa = CodTransa; // ERROR: CS0103
            // Formapago.FormPago.secuencia = Secuencia; // ERROR: CS0103
            // Formapago.FormPago.Nit = nit; // ERROR: CS0103
            // Formapago.FormPago.IdTurno = Idturno; // ERROR: CS0103
            // Formapago.FormPago.Idpunto = IdPunto; // ERROR: CS0103
            // Formapago.FormPago.FecMovto = FecMovto; // ERROR: CS0103
            // Formapago.FormPago.Usuario = Usuario; // ERROR: CS0103
            // Formapago.pstmyconect = varini.pstMyconec; // ERROR: CS0103
            // Formapago.FormPago.MovtoPos = MovtoPos; // ERROR: CS0103
            // Formapago.FormPago.VlrTotal = VlrTotal; // ERROR: CS0103
            // Formapago.FormPago.vlrSubTotal = VlrSubTotal; // ERROR: CS0103
            // Formapago.FormPago.VlrDsto = VlrDsto; // ERROR: CS0103
            // Formapago.FormPago.VlrIva = VlrIva; // ERROR: CS0103
            // Formapago.FormPago.Estado = Estado; // ERROR: CS0103
            // Formapago.FormPago.VlrArqueo = VlrArqueo; // ERROR: CS0103
            // Formapago.FormPago.detalle = detalle; // ERROR: CS0103
            // Formapago.FormPago.Factura = Factura; // ERROR: CS0103
            // Formapago.FormPago.fecvence = FecVence; // ERROR: CS0103
            // Formapago.FormPago.VlrRetfte = VlrRetfte; // ERROR: CS0103
            // Formapago.FormPago.vlrica = VlrIca; // ERROR: CS0103
            // Formapago.FormPago.idVendedor = idVendedor; // ERROR: CS0103
            // Formapago.FormPago.idtipomovdev = idtipomovdev; // ERROR: CS0103
            // Formapago.FormPago.secuenciadev = secuenciaDev; // ERROR: CS0103
            // Formapago.ShowDialog(MyForma); // ERROR: CS0103

            // switch (Formapago.FormPago.MovtoPos) // ERROR: CS0103
            // {
                // case "Y":
                    // Secuencia = Formapago.FormPago.secuencia; // ERROR: CS0103
                //     break;
            // }

            this.BuscaFormaPago(CodTransa, Secuencia, ref ClasePago, ref Efectivo, ref Cheque, ref TarDebito, ref TarCredito, ref Credito);
            Total = Efectivo + TarDebito + TarCredito + Credito;

            if (Total > 0)
                return true;
            else
                return false;
        }

        // ── GrabaFormaPago ──────────────────────────────────────────────────
        public void GrabaFormaPago(int IdTipoMovto, double Secuencia, int Formpago,
            double Efectivo, double cheque, double VlrTarDebito, double VlrTarCredito,
            string banco, int CantCheques, string Cuenta, double Diferido,
            int Periodicidad, int Plazo, int clades, DateTime fecdsto,
            double cuota, decimal TasaInt, string idcliente, DateTime fecing,
            double cambio, string MovtoPos, string idusuario, double VlrTotal,
            double VlrSubTotal, double VlrDsto, double VlrIva, string Estado,
            int Idpunto, int IdTurno, double VlrArqueo, string detalle,
            double Factura, DateTime FecVence, decimal VlrRetfte, decimal VlrIca,
            int idVendedor, int idtipomovdev, double secuenciaDev)
        {
            double ConseFact = 0;
            int Cladoc = 9;
            string codfactura = "0";
            OdbcConnection myconnect = new OdbcConnection(varini.pstMyconec);
            myconnect.Open();

            if (banco == "")
                banco = "9999";

            switch (MovtoPos)
            {
                case "N":
                    stmysql = "update inv_docs set ClaPago = '" + Formpago + "', VlrEfectivo ='" + Efectivo + "',VlrCheque ='" + cheque + "',"
                            + " Idbanco = '" + banco + "',Cant =' " + CantCheques + "',CuentaBanco= '" + Cuenta + "',"
                            + " VlrTarDebito = '" + VlrTarDebito + "', VlrTarCredito ='" + VlrTarCredito + "', VlrCredito = '" + Diferido + "',"
                            + " Cambio = vlrtotal - '" + (Efectivo + cheque + VlrTarDebito + VlrTarCredito + Diferido) + "', periodicidad = '" + Periodicidad + "',"
                            + " plazo = '" + Plazo + "',clades  ='" + clades + "',fecdsto = '" + Strings.Format(fecdsto, varini.PstForFec) + "',cuota = '" + cuota + "',Tasa = '" + TasaInt + "'"
                            + " where IdTipoMovto ='" + IdTipoMovto + "' and Secuencia= '" + Secuencia + "'";
                    break;
                default:
                    switch (BuscaTransaccion(IdTipoMovto, Secuencia, myconnect))
                    {
                        case true:
                            stmysql = "update inv_docs set ClaPago = '" + Formpago + "', VlrEfectivo ='" + Efectivo + "',VlrCheque ='" + cheque + "',"
                                    + " Idbanco = '" + banco + "',Cant =' " + CantCheques + "',CuentaBanco= '" + Cuenta + "',"
                                    + " VlrTarDebito = '" + VlrTarDebito + "', VlrTarCredito ='" + VlrTarCredito + "', VlrCredito = '" + Diferido + "',"
                                    + " Cambio = vlrtotal - '" + (Efectivo + cheque + VlrTarDebito + VlrTarCredito + Diferido) + "', periodicidad = '" + Periodicidad + "',"
                                    + " plazo = '" + Plazo + "',clades  ='" + clades + "',fecdsto = '" + Strings.Format(fecdsto, varini.PstForFec) + "',cuota = '" + cuota + "',Tasa = '" + TasaInt + "'"
                                    + " where IdTipoMovto ='" + IdTipoMovto + "' and Secuencia= '" + Secuencia + "'";
                            break;
                        case false:
                            // this.msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, "", "", "", "", "", "", ref Cladoc, "", "", "", ref codfactura); // ERROR: CS7036
                            if (Cladoc == 0 || Cladoc == 2)
                            {
                                this.msginvconf.BuscaConseFactura(codfactura, myconnect, ref ConseFact);
                            }
                            stmysql = "insert into inv_docs (ClaPago,VlrEfectivo,VlrCheque,Idbanco,Cant,CuentaBanco,VlrTarDebito, VlrTarCredito, VlrCredito,"
                                    + " Cambio,periodicidad,plazo,clades,fecdsto,cuota,Tasa,IdTipoMovto,Secuencia,idcliente,fecing,idusuario,"
                                    + "VlrTotal,vlrSubTotal,VlrDsto,VlrIva,Estado,IdPunto,IdTurno,VlrArqueos,detalle,Factura,fecvence,"
                                    + "VlrRetfte,vlrica,idVendedor,idtipomovdev,secuenciadev) "
                                    + " values('" + Formpago + "','" + Efectivo + "','" + cheque + "','" + banco + "','" + CantCheques + "','" + Cuenta + "',"
                                    + " '" + VlrTarDebito + "','" + VlrTarCredito + "','" + Diferido + "','" + (cambio * -1) + "','" + Periodicidad + "',"
                                    + " '" + Plazo + "','" + clades + "','" + Strings.Format(fecdsto, varini.PstForFec) + "','" + cuota + "','" + TasaInt + "','"
                                    + IdTipoMovto + "','" + Secuencia + "','" + Convert.ToDouble(idcliente) + "','" + Strings.Format(fecing, varini.PstForFec) + "','" + idusuario + "','"
                                    + VlrTotal + "','" + VlrSubTotal + "','" + VlrDsto + "','" + VlrIva + "','" + Estado + "','" + Idpunto + "','"
                                    + IdTurno + "','" + VlrArqueo + "','" + detalle + "','" + ConseFact + "','" + Strings.Format(FecVence, varini.PstForFec) + "','" + VlrRetfte + "','"
                                    + VlrIca + "','" + idVendedor + "','" + idtipomovdev + "','" + secuenciaDev + "')";
                            break;
                    }
                    break;
            }

            ExecSql(stmysql, myconnect, "GrabaFormaPago");

            myconnect.Close();
            myconnect.Dispose();
        }

        // ── ActualizaCambio ─────────────────────────────────────────────────
        public void ActualizaCambio(int IdTipoMovto, double Secuencia, OdbcConnection myconnect)
        {
            stmysql = "update inv_docs set Cambio = vlrtotal -  (VlrEfectivo + VlrCheque + VlrTarDebito + VlrTarCredito + VlrCredito)  where IdTipoMovto ='" + IdTipoMovto + "' and Secuencia= '" + Secuencia + "'";
            ExecSql(stmysql, myconnect, "ActualizaCambio");
        }

        // ── BuscaFormaPago ──────────────────────────────────────────────────
        public void BuscaFormaPago(int IdTipoMovto, double Secuencia,
            ref int Formpago, ref double Efectivo, ref double cheque,
            ref double VlrTarDebito, ref double VlrTarCredito, ref double Credito)
        {
            double Cambio = 0;
            BuscaFormaPago(IdTipoMovto, Secuencia, ref Formpago, ref Efectivo, ref cheque, ref VlrTarDebito, ref VlrTarCredito, ref Credito, ref Cambio);
        }

        public void BuscaFormaPago(int IdTipoMovto, double Secuencia,
            ref int Formpago, ref double Efectivo, ref double cheque,
            ref double VlrTarDebito, ref double VlrTarCredito, ref double Credito,
            ref double Cambio)
        {
            OdbcConnection myconnect = new OdbcConnection(varini.pstMyconec);
            myconnect.Open();

            // First query: campo1=ClaPago, campo2=VlrEfectivo, campo3=VlrCheque, campo4=VlrTarDebito
            stmysql = "select ClaPago as campo1, VlrEfectivo as campo2,VlrCheque as campo3,VlrTarDebito as campo4 from inv_docs "
                    + " where IdTipoMovto ='" + IdTipoMovto + "' and Secuencia= '" + Secuencia + "'";

            string sFormpago = Formpago.ToString();
            string sEfectivo = Efectivo.ToString();
            string sCheque = cheque.ToString();
            string sVlrTarDebito = VlrTarDebito.ToString();
            this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "GrabaFormaPago", ref sFormpago, ref sEfectivo, ref sCheque, ref sVlrTarDebito);
            int.TryParse(sFormpago, out Formpago);
            double.TryParse(sEfectivo, out Efectivo);
            double.TryParse(sCheque, out cheque);
            double.TryParse(sVlrTarDebito, out VlrTarDebito);

            // Second query: campo1=VlrTarCredito, campo2=VlrCredito, campo3=Cambio
            stmysql = "select VlrTarCredito as campo1, VlrCredito as campo2,Cambio as campo3 from inv_docs "
                    + " where IdTipoMovto ='" + IdTipoMovto + "' and Secuencia= '" + Secuencia + "'";

            string sVlrTarCredito = VlrTarCredito.ToString();
            string sCredito = Credito.ToString();
            string sCambio = Cambio.ToString();
            ExecSql3(stmysql, myconnect, "GrabaFormaPago", ref sVlrTarCredito, ref sCredito, ref sCambio);
            double.TryParse(sVlrTarCredito, out VlrTarCredito);
            double.TryParse(sCredito, out Credito);
            double.TryParse(sCambio, out Cambio);

            myconnect.Close();
            myconnect.Dispose();
        }

        // ── ImpremeAlgo ─────────────────────────────────────────────────────
        public void ImpremeAlgo(int IdTipoMovto, double Secuencia, OdbcConnection Myconnect, Form myforma, string opcion)
        {
            int Cladoc = 0, ClaPago = 0;
            string descripcion = "";

            // this.msginvconf.BuscaTipomovto(IdTipoMovto, Myconnect, ref descripcion, "", "", "", "", "", ref Cladoc); // ERROR: CS7036
            switch (Cladoc)
            {
                case 0:
                    ImprimeFactura(IdTipoMovto, Secuencia, Myconnect, myforma);
                    break;
                case 1:
                case 4:
                case 5:
                case 6:
                    ImprimeDevolucion(IdTipoMovto, Secuencia, Myconnect, myforma, descripcion, Cladoc);
                    break;
                case 7:
                    this.ImprimeTraslado(IdTipoMovto, Secuencia, Myconnect, myforma, descripcion, Cladoc);
                    break;
                case 2:
                    ImprimeTikect(IdTipoMovto, Secuencia, Myconnect);
                    // BuscaTransaccion(IdTipoMovto, Secuencia, Myconnect, "", 0, 0, "", "", "", "", "", "", "", 0, "", "", "", "", "", ref ClaPago); // ERROR: CS1501
                    switch (ClaPago)
                    {
                        case 3:
                            ImprimeTikectPrestamo(IdTipoMovto, Secuencia, Myconnect);
                            break;
                    }
                    break;
                case 8:
                    switch (opcion)
                    {
                        case "0":
                            Imprime_OrdenPedido(IdTipoMovto, Secuencia, Myconnect, myforma, Cladoc);
                            break;
                        default:
                            if (MessageBox.Show("Desea imprimir la REMISION", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                Imprime_Remision(IdTipoMovto, Secuencia, Myconnect, myforma);
                            }
                            else
                            {
                                // Imprime_OrdenPedido(IdTipoMovto, Secuencia, Myconnect, myforma); // ERROR: CS7036
                            }
                            break;
                    }
                    break;
                case 9:
                    Imprime_OrdenPedido(IdTipoMovto, Secuencia, Myconnect, myforma, Cladoc);
                    break;
            }
        }

        // ── ImprimeTikect ───────────────────────────────────────────────────
        public void ImprimeTikect(int IdTipoMovto, double Secuencia, OdbcConnection Myconnect)
        {
            ERP.Core.Inventario.Services.clsmsgtiket msgtiketObj = new ERP.Core.Inventario.Services.clsmsgtiket();
            string ctrlfactura = "9999", AbreCajon = "Y";

            // this.msginvconf.BuscaTipomovto(IdTipoMovto, Myconnect, "", "", "", "", "", "", 0, "", "", "", ref ctrlfactura); // ERROR: CS7036
            // this.msginvconf.BuscaDatosFacturacion(ctrlfactura, Myconnect, "", DateTime.MinValue, 0, "", 0, 0, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref AbreCajon); // ERROR: CS7036

            try
            {
                // OpenCajon.Clsopencajon openObj = new OpenCajon.Clsopencajon(); // ERROR: CS0246

                switch (AbreCajon)
                {
                    case "Y":
                        // openObj.OpenCajon(); // ERROR: CS0103
                        // openObj.IntializeImpresora(); // ERROR: CS0103
                        break;
                }

                msgtiketObj.ImprimeTiket(IdTipoMovto, Secuencia, Myconnect);

                switch (AbreCajon)
                {
                    case "Y":
                        // openObj.CortePapel(); // ERROR: CS0103
                        break;
                }
            }
            catch (Exception ex)
            {
                switch (AbreCajon)
                {
                    case "N":
                        msgtiketObj.ImprimeTiket(IdTipoMovto, Secuencia, Myconnect);
                        break;
                }
            }
        }

        // ── ImprimeTikectPrestamo ───────────────────────────────────────────
        private void ImprimeTikectPrestamo(int IdTipoMovto, double Secuencia, OdbcConnection Myconnect)
        {
            ERP.Core.Inventario.Services.clsmsgtiket msgtiketObj = new ERP.Core.Inventario.Services.clsmsgtiket();
            msgtiketObj.ImprimeTiketPrestamo(IdTipoMovto, Secuencia, Myconnect);
        }

        // ── ImprimeTranAbiertos ─────────────────────────────────────────────
        public void ImprimeTranAbiertos(int Idpunto, int Idturno, DateTime fecmovto, DataTable DsDataTable, OdbcConnection Myconnect)
        {
            string nit = " ", Direccion = " ", Telefono = " ", Nombre = " ";
            int Regimen = 0;
            ERP.Core.Compartido.Forms.imprimir Impresion = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte factura = new ERP.Core.Compartido.Reportes.reporte("inv_fctrlpuntos01");

            // msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect, "", "", "", "", "", "", "", "", "", "", "", "", ref nit, ref Direccion, "", "", "", "", "", "", "", "", "", ref Nombre, ref Telefono); // ERROR: CS7036

            factura.SetDataSource(DsDataTable);
            factura.SetParameterValue("nit", nit);
            factura.SetParameterValue("empresa", Nombre);
            factura.SetParameterValue("direccion", Direccion);
            factura.SetParameterValue("telefono", Telefono);
            factura.SetParameterValue("fecha", fecmovto);
            factura.SetParameterValue("punto", Idpunto);
            factura.SetParameterValue("turno", Idturno);

            Impresion.Visible = true;
            // Impresion.CrystalReportViewer1.ReportSource = factura; // ERROR: CS1061
            // Impresion.CrystalReportViewer1.Show(); // ERROR: CS1061
        }

        // ── ImprimeFactura ──────────────────────────────────────────────────
        private void ImprimeFactura(int IdTipoMovto, double Secuencia, OdbcConnection Myconnect, Form myforma)
        {
            string nit = " ", Direccion = " ", Telefono = " ", Nombre = " ";
            int Regimen = 0;
            string Resolucion = " ", Prefijo = " ";
            DateTime Fecresol = DateTime.MinValue;
            int NumInicial = 0, NumFinal = 0;
            string NomRegimen = null, Ciudad = " ", IdCtrlFactura = " ";
            ERP.Core.Compartido.Forms.imprimir Impresion = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte factura = new ERP.Core.Compartido.Reportes.reporte("factura");
            ERP.Core.Compartido.Reportes.config_report ConfRep = new ERP.Core.Compartido.Reportes.config_report();
            ERP.Core.Compartido.Utilidades.Numeros_A_Letras num = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
            string stNumeletras;
            double valor = 0;

            // this.msginvconf.BuscaTipomovto(IdTipoMovto, Myconnect, "", "", "", "", "", "", 0, "", "", "", ref IdCtrlFactura); // ERROR: CS7036
            // msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect, "", "", "", "", "", "", "", "", "", "", "", "", ref nit, ref Direccion, "", "", "", "", "", "", "", "", "", ref Nombre, ref Telefono, "", "", "", "", "", "", "", ref Ciudad); // ERROR: CS7036
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

            // this.BuscaTransaccion(IdTipoMovto, Secuencia, Myconnect, "", 0, 0, "", "", "", "", "", "", "", ref valor); // ERROR: CS1501
            stNumeletras = num.Num_a_Letras(valor) + "MLC";

            factura.SetParameterValue("IdTipoMovto", IdTipoMovto);
            factura.SetParameterValue("Secuencia", Secuencia);
            factura.SetParameterValue("nit", nit);
            factura.SetParameterValue("regimen", Regimen);
            factura.SetParameterValue("direccion", Direccion);
            factura.SetParameterValue("telefono", Telefono);
            factura.SetParameterValue("Nombre", Nombre);
            factura.SetParameterValue("resolucion", Resolucion);
            factura.SetParameterValue("Fecresol", Fecresol);
            factura.SetParameterValue("telefono", Telefono);
            factura.SetParameterValue("NumInicial", NumInicial);
            factura.SetParameterValue("NumFinal", NumFinal);
            factura.SetParameterValue("Prefijo", Prefijo);
            factura.SetParameterValue("Enletras", stNumeletras);
            factura.SetParameterValue("Ciudad", Ciudad);

            // ConfRep.confi_reportes(myforma, factura, "", true); // ERROR: CS1503
        }

        // ── GrabaAnulacion ──────────────────────────────────────────────────
        public bool GrabaAnulacion(double IdTipoMovto, double secuencia, double IdtipoMovtoAnul,
            double ConseAnulacion, DateTime FecAnulacion, string Idusuario, string Detalle,
            OdbcConnection myconect, Form myforma)
        {
            DialogResult MSGOK;
            string estado = "C";
            int ActCont = 0;
            double Neto = 0, NetoAnulacion = 0;
            int CtrlExistencia = 0;
            string TipoTercero = "0", ClientePatronal = "N";
            int CptoCap = 0;
            string Nit = " ";
            double Credito = 0;
            int Clapago = 0, IdTurno = 0;
            string CpteCartera = "9999";
            int Lincred = 0, clades = 0, Plazo = 0;
            string Stdetalle = "";
            int Clacuo = 0, ClaseI = 0;
            string Cencos = "99999999";
            decimal TasaInt = 0, Foradmon = 0;
            string ConseCartera, FechaMov = "0";
            int Idpunto = 0;
            string CuentaCpte = "999999999999";
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntObj = new ERP.Core.Contabilidad.Services.ClsContabilidad();

            // this.msginvconf.buscaPeriodo("post", myconect, "", "", FecAnulacion, ref estado, "", Strings.Format(FecAnulacion, "yyyy")); // ERROR: CS1503, CS1620
            switch (estado)
            {
                case "P":
                    MSGOK = MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    switch (MSGOK)
                    {
                        case DialogResult.No:
                            return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            GrabaMovimientoAnulacion(IdTipoMovto, secuencia, IdtipoMovtoAnul, ConseAnulacion, FecAnulacion, Idusuario, Detalle, myconect);

            // this.BuscaTransaccion(IdTipoMovto, secuencia, myconect, ref Nit, ref Idpunto, ref IdTurno, ref FechaMov, "", "", "", "", "", ref Credito, ref Neto, "", "", "", "", "", ref Clapago); // ERROR: CS1501
            // this.BuscaTransaccion(IdtipoMovtoAnul, ConseAnulacion, myconect, "", 0, 0, "", "", "", "", "", "", "", ref NetoAnulacion); // ERROR: CS1501

            if (Neto == NetoAnulacion)
            {
                switch (Clapago)
                {
                    case 3:
                        // msgcntObj.BuscarTercero(Nit, myconect, "", "", "", "", ref TipoTercero, "", ref ClientePatronal); // ERROR: CS7036

                        if (IdTurno > 0)
                        {
                            switch (TipoTercero)
                            {
                                case "7":
                                    // msginvconf.BuscaDatosFacturacion(1, myconect, "", DateTime.MinValue, 0, "", 0, 0, "", "", "", "", "", "", ref CpteCartera, ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                                    break;
                                default:
                                    switch (ClientePatronal)
                                    {
                                        case "Y":
                                            // msginvconf.BuscaDatosFacturacion(1, myconect, "", DateTime.MinValue, 0, "", 0, 0, "", "", "", "", "", "", "", "", "", "", ref CpteCartera, ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                                            break;
                                        default:
                                            // msginvconf.BuscaDatosFacturacion(1, myconect, "", DateTime.MinValue, 0, "", 0, 0, "", "", ref CpteCartera, ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                                            break;
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            // msginvconf.BuscaDatosFacturacion(2, myconect, "", DateTime.MinValue, 0, "", 0, 0, "", "", "", ref Lincred, ref clades, ref Plazo); // ERROR: CS7036
                        }

                        // msginvconf.BuscaTipomovto(IdTipoMovto, myconect, "", "", "", "", "", ref CtrlExistencia); // ERROR: CS7036

                        if (CtrlExistencia == 0)
                        {
                            break;
                        }

                        // msgsys.BuscarCompania(msgcop.varini.sptCodEmpr, myconect, "", ref CptoCap); // ERROR: CS7036
                        // ok = msgcop.BuscaAsociado(Nit, myconect); // ERROR: CS1620
                        switch (ok)
                        {
                            case true:
                                ConseCartera = Idpunto.ToString() + IdTurno.ToString() + Strings.Format(Convert.ToDateTime(FechaMov), "yyMMdd");
                                // msgcop.BuscaLinea(Lincred, myconect, ref Clacuo, ref ClaseI, 0, ref Cencos, "", ref TasaInt, 0, 0, ref Foradmon); // ERROR: CS7036
                                // msgcop.BuscaComprobante(CpteCartera, 0, false, myconect, "", "", "", "", "", "", "", "", "", "", "", "", "", ref CuentaCpte); // ERROR: CS1620

                                // msgcop.GrabaMovimiento(CpteCartera, ConseCartera, Nit, Lincred, secuencia, Strings.Format(Convert.ToDateTime(FechaMov), "yyyyMM"), CptoCap, FechaMov, 0, Neto, " ", Idusuario, myconect, "", "", Nit, "", Nit); // ERROR: CS1503, CS1620
                                break;
                            case false:
                                MessageBox.Show("Asociado no existe ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return false;
                        }
                        break;
                }

                Stdetalle = "Anulacion del Documento " + IdTipoMovto + " - " + secuencia;

                this.GrabaEstado(IdTipoMovto, secuencia, myconect, "CA");
                this.GrabaEstado(IdtipoMovtoAnul, ConseAnulacion, myconect, "CA", Stdetalle);
                // this.msginvconf.BuscaTipomovto(IdtipoMovtoAnul, myconect, "", "", "", "", "", "", "", ref ActCont); // ERROR: CS7036
                switch (ActCont)
                {
                    case 3:
                        // this.ActualizaContabilidadAnulacion(IdTipoMovto, secuencia, IdtipoMovtoAnul, FecAnulacion, Idusuario, myconect, myforma, "", ConseAnulacion); // ERROR: CS1503
                        // this.CierreCostosLineaAnulacion(IdTipoMovto, secuencia, IdtipoMovtoAnul, FecAnulacion, Idusuario, myconect, myforma, "", ConseAnulacion); // ERROR: CS1503
                        break;
                }
            }

            return false; // VB Function without explicit Return at end returns default
        }

        // ── GrabaMovimientoAnulacion ────────────────────────────────────────
        private void GrabaMovimientoAnulacion(double IdTipoMovto, double secuencia,
            double IdtipoMovtoAnul, double ConseAnulacion, DateTime FecAnulacion,
            string Idusuario, string Detalle, OdbcConnection myconect)
        {
            int CtrlExistencia = 0, CtrlExistenciaOrg = 0;
            double CantInicial = 0, CantDispo = 0, Costo = 0, UltCosto = 0, NewCosto = 0;
            double ValorUnidad = 0, NumCant = 0, Cantidad = 0;
            string ClaMovto = " ";
            double CostoInicial = 0;
            int canreg = 0, fila = 0;

            // this.msginvconf.BuscaTipomovto(IdtipoMovtoAnul, myconect, "", "", "", "", "", ref CtrlExistencia); // ERROR: CS7036
            // this.msginvconf.BuscaTipomovto(IdTipoMovto, myconect, "", "", "", "", "", ref CtrlExistenciaOrg); // ERROR: CS7036

            stmysql = "Select movtos.IdProducto, maepro.Descripcion, movtos.Tipoventa,movtos.Cantidad, movtos.VlrUnidad,Movtos.neto,movtos.vlrdsto,movtos.idubicacion,movtos.idbodega, "
                    + " movtos.vlriva,movtos.SubTotal,movtos.TasaIva,movtos.TasaDscto,movtos.Idpunto,movtos.idturno,movtos.NumFactura,invdoc.estado,invdoc.idcliente,invdoc.clapago,invdoc.vlrcredito,movtos.ClaMovto "
                    + "from inv_movtos movtos inner join inv_productos maepro on movtos.IdProducto = maepro.IdProducto"
                    + " inner join inv_docs invdoc on movtos.IdTipoMovto = invdoc.IdTipoMovto and movtos.Secuencia = invdoc.Secuencia"
                    + " where movtos.IdTipoMovto=" + IdTipoMovto + " and movtos.Secuencia = '" + secuencia + "'";

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "GrabaMovimientoAnulacion", myRead, "TblGrabaMovtoAnula");
            canreg = myRead.Tables["TblGrabaMovtoAnula"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblGrabaMovtoAnula"].Rows[fila];
                Cantidad = Convert.ToDouble(row["Cantidad"]);
                ValorUnidad = Convert.ToDouble(row["VlrUnidad"]);

                switch (CtrlExistencia)
                {
                    case 0:
                        ClaMovto = "C";
                        break;
                    case 1:
                        ClaMovto = "V";
                        break;
                }

                switch (CtrlExistenciaOrg)
                {
                    case 2:
                    case 3:
                        switch (row["ClaMovto"].ToString())
                        {
                            case "C":
                                ClaMovto = "V";
                                break;
                            case "V":
                                ClaMovto = "C";
                                break;
                        }
                        if (Cantidad != 0)
                        {
                            ValorUnidad = Convert.ToDouble(row["SubTotal"]) / Cantidad;
                        }
                        break;
                }

                // GrabaTransaccion(IdtipoMovtoAnul, ConseAnulacion, myconect, row["idcliente"].ToString(), FecAnulacion, Convert.ToDouble(row["Neto"]), Convert.ToDouble(row["SubTotal"]), Convert.ToDouble(row["VlrDsto"]), Convert.ToDouble(row["VlrIva"]), "", Idusuario, "", "", "", "", "", Convert.ToInt32(row["Idpunto"]), Convert.ToInt32(row["Idturno"]), 0, Detalle); // ERROR: CS7036
                // GrabaMovtoTransaccion(IdtipoMovtoAnul, ConseAnulacion, row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), Convert.ToDouble(row["idproducto"]), row["Tipoventa"].ToString(), ClaMovto, myconect, FecAnulacion, Convert.ToDouble(row["Cantidad"]), row["NumFactura"].ToString(), Convert.ToDecimal(row["Tasaiva"]), Convert.ToDecimal(row["TasaDscto"]), row["idcliente"].ToString(), Convert.ToDouble(row["VlrIva"]), Convert.ToDouble(row["SubTotal"]), Convert.ToDouble(row["VlrDsto"]), Convert.ToDouble(row["Neto"]), Convert.ToDouble(row["VlrUnidad"]), Idusuario, Convert.ToInt32(row["Idpunto"]), Convert.ToInt32(row["Idturno"])); // ERROR: CS1501
                // this.msginvconf.BuscaProductos(Convert.ToDouble(row["idproducto"]), myconect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref NumCant); // ERROR: CS7036

                switch (row["Tipoventa"].ToString())
                {
                    case "P":
                        ValorUnidad = (Convert.ToDouble(row["VlrUnidad"]) * Cantidad) / (Cantidad * NumCant);
                        Cantidad *= NumCant;
                        break;
                }

                this.BuscaInventario(Convert.ToDouble(row["idproducto"]), Strings.Format(FecAnulacion, "yyyyMM"), row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), myconect, ref CantInicial, ref CantDispo, ref CantDispo, ref CantDispo, ref Costo, ref UltCosto, ref CostoInicial);

                switch (CtrlExistenciaOrg)
                {
                    case 2:
                        switch (ClaMovto)
                        {
                            case "C":
                                this.GrabaInventario(Convert.ToDouble(row["idproducto"]), row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), CantInicial, Cantidad, 0, CostoInicial, Costo, UltCosto, Strings.Format(FecAnulacion, "yyyyMM"), myconect);
                                break;
                            case "V":
                                this.GrabaInventario(Convert.ToDouble(row["idproducto"]), row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), CantInicial, 0, Cantidad, CostoInicial, Costo, UltCosto, Strings.Format(FecAnulacion, "yyyyMM"), myconect);
                                break;
                        }
                        break;
                    case 3:
                        switch (ClaMovto)
                        {
                            case "C":
                                this.GrabaInventario(Convert.ToDouble(row["idproducto"]), row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), CantInicial, Cantidad, 0, CostoInicial, Costo, UltCosto, Strings.Format(FecAnulacion, "yyyyMM"), myconect);
                                break;
                            case "V":
                                NewCosto = BorraCosto(Convert.ToDouble(row["idproducto"]), Cantidad, ValorUnidad, myconect, Costo, CantDispo);
                                this.GrabaInventario(Convert.ToDouble(row["idproducto"]), row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), CantInicial, 0, Cantidad, CostoInicial, NewCosto, Costo, Strings.Format(FecAnulacion, "yyyyMM"), myconect);
                                break;
                        }
                        break;
                    default:
                        switch (CtrlExistencia)
                        {
                            case 0:
                                this.GrabaInventario(Convert.ToDouble(row["idproducto"]), row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), CantInicial, Cantidad, 0, CostoInicial, Costo, UltCosto, Strings.Format(FecAnulacion, "yyyyMM"), myconect);
                                break;
                            case 1:
                                NewCosto = BorraCosto(Convert.ToDouble(row["idproducto"]), Cantidad, ValorUnidad, myconect, Costo, CantDispo);
                                this.GrabaInventario(Convert.ToDouble(row["idproducto"]), row["idubicacion"].ToString(), Convert.ToDouble(row["idbodega"]), CantInicial, 0, Cantidad, CostoInicial, NewCosto, Costo, Strings.Format(FecAnulacion, "yyyyMM"), myconect);
                                break;
                        }
                        break;
                }
                fila += 1;
            }

            myRead.Dispose();
        }

        // ── GrabaMovimiento (overload 1 - without Cencosto/IdBodega) ────────
        public object GrabaMovimiento(double IdTipoMovto, double secuencia, double IdProducto,
            string TipoVenta, OdbcConnection myconect, DateTime FecMovto, double Cantidad,
            double VlrUnidad, string NumFactura, decimal TasaIva, decimal TasaDscto,
            string IdCliente, double VlrIva, double VlrDsto, double VlrTotal,
            double Subtotal, string Idusuario, int Idpunto, int IdTurno,
            double VlrArqueo, string detalle, DateTime FecVence,
            decimal TasaRetFte, decimal VlrRetFte, decimal TasaIca, decimal VlrIca,
            string MovtoPos, int idvendedor)
        {
            string estado = "C";
            DialogResult MSGOK;
            int CtrlExistencia = 0;
            string ClaMovto = "N";
            string OtrosImp = "N";
            DataTable DsDatosImp = new DataTable();
            double VlrCosto = 0;
            double Valadm = 0, IvaAdm = 0, IvaTiq = 0, OtroImp = 0, AeroPort = 0, ImpComb = 0;

            if (IdCliente == "0" || IdCliente == null)
            {
                IdCliente = "99999999999999";
            }

            // this.msginvconf.buscaPeriodo("post", myconect, "", "", FecMovto, ref estado, "", Strings.Format(FecMovto, "yyyy")); // ERROR: CS1503, CS1620
            switch (estado)
            {
                case "P":
                    MSGOK = MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    switch (MSGOK)
                    {
                        case DialogResult.No:
                            return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            // msginvconf.BuscaProductos(IdProducto, myconect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref OtrosImp); // ERROR: CS7036

            switch (OtrosImp)
            {
                case "Y":
                    DsDatosImp = CargaOtrosImpuestos();
                    if (DsDatosImp.Rows.Count > 0)
                    {
                        DataRow impRow = DsDatosImp.Rows[0];
                        Valadm = Convert.ToDouble(impRow["Valadm"]);
                        IvaAdm = Convert.ToDouble(impRow["IvaAdm"]);
                        IvaTiq = Convert.ToDouble(impRow["IvaTiq"]);
                        OtroImp = Convert.ToDouble(impRow["OtroImp"]);
                        AeroPort = Convert.ToDouble(impRow["AeroPort"]);
                        ImpComb = Convert.ToDouble(impRow["ImpComb"]);
                    }
                    break;
            }

            ClaMovto = GrabaExistencia(IdProducto, ref Cantidad, VlrUnidad, IdTipoMovto, TipoVenta, FecMovto, myconect, ref VlrCosto, "", ref Subtotal);

            // GrabaTransaccion(IdTipoMovto, secuencia, myconect, IdCliente, FecMovto, VlrTotal, Subtotal, VlrDsto, VlrIva, "", Idusuario, "", "", "", "", "", Idpunto, IdTurno, VlrArqueo, detalle, FecVence, VlrRetFte, VlrIca, false, "", idvendedor); // ERROR: CS7036
            GrabaMovtoTransaccion(IdTipoMovto, secuencia, IdProducto, TipoVenta, ClaMovto, myconect, FecMovto, Cantidad, NumFactura, TasaIva, TasaDscto, IdCliente, VlrIva, Subtotal, VlrDsto, VlrTotal, VlrUnidad, Idusuario, Idpunto, IdTurno,
                Valadm, IvaAdm, IvaTiq, OtroImp, AeroPort, ImpComb, VlrCosto, TasaRetFte, VlrRetFte, TasaIca, VlrIca, MovtoPos);

            return null;
        }

        // ── GrabaMovimiento (overload 2 - with Cencosto/IdBodega) ───────────
        public virtual object GrabaMovimiento(double IdTipoMovto, double secuencia,
            double IdProducto, string TipoVenta, string Cencosto, double IdBodega,
            OdbcConnection myconect, DateTime FecMovto, double Cantidad, double VlrUnidad,
            string NumFactura, decimal TasaIva, decimal TasaDscto, string IdCliente,
            double VlrIva, double VlrDsto, double VlrTotal, double Subtotal,
            string Idusuario, int Idpunto, int IdTurno, double VlrArqueo,
            string detalle, DateTime FecVence, decimal TasaRetFte, decimal VlrRetFte,
            decimal TasaIca, decimal VlrIca, string MovtoPos, bool DesdeConv,
            int idvendedor, string orden_Pedido)
        {
            string estado = "C";
            DialogResult MSGOK;
            int CtrlExistencia = 0;
            string ClaMovto = "N";
            string OtrosImp = "N";
            DataTable DsDatosImp = new DataTable();
            double VlrCosto = 0;
            double Valadm = 0, IvaAdm = 0, IvaTiq = 0, OtroImp = 0, AeroPort = 0, ImpComb = 0;

            if (IdCliente == "0" || IdCliente == null)
            {
                IdCliente = "99999999999999";
            }

            // this.msginvconf.buscaPeriodo("post", myconect, "", "", FecMovto, ref estado, "", Strings.Format(FecMovto, "yyyy")); // ERROR: CS1503, CS1620
            switch (estado)
            {
                case "P":
                    MSGOK = MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    switch (MSGOK)
                    {
                        case DialogResult.No:
                            return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            // msginvconf.BuscaProductos(IdProducto, myconect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref OtrosImp); // ERROR: CS7036

            switch (OtrosImp)
            {
                case "Y":
                    DsDatosImp = CargaOtrosImpuestos();
                    if (DsDatosImp.Rows.Count > 0)
                    {
                        DataRow impRow = DsDatosImp.Rows[0];
                        Valadm = Convert.ToDouble(impRow["Valadm"]);
                        IvaAdm = Convert.ToDouble(impRow["IvaAdm"]);
                        IvaTiq = Convert.ToDouble(impRow["IvaTiq"]);
                        OtroImp = Convert.ToDouble(impRow["OtroImp"]);
                        AeroPort = Convert.ToDouble(impRow["AeroPort"]);
                        ImpComb = Convert.ToDouble(impRow["ImpComb"]);
                    }
                    break;
            }

            switch (orden_Pedido.Trim())
            {
                case "Y":
                    // GrabaTransaccion_ordenPedido(IdTipoMovto, secuencia, myconect, IdCliente, FecMovto, VlrTotal, Subtotal, VlrDsto, VlrIva, "", Idusuario, "", "", "", "", "", Idpunto, IdTurno, VlrArqueo, detalle, FecVence, VlrRetFte, VlrIca, DesdeConv, NumFactura, idvendedor); // ERROR: CS7036
                    GrabaMovtoTransaccion_orden_Pedido(IdTipoMovto, secuencia, Cencosto, IdBodega, IdProducto, TipoVenta, ClaMovto, myconect, FecMovto, Cantidad, NumFactura, TasaIva, TasaDscto, IdCliente, VlrIva, Subtotal, VlrDsto, VlrTotal, VlrUnidad, Idusuario, Idpunto, IdTurno,
                        Valadm, IvaAdm, IvaTiq, OtroImp, AeroPort, ImpComb, VlrCosto, TasaRetFte, VlrRetFte, TasaIca, VlrIca, MovtoPos);
                    break;
                default:
                    ClaMovto = GrabaExistencia(IdProducto, Cencosto, IdBodega, ref Cantidad, VlrUnidad, IdTipoMovto, TipoVenta, FecMovto, myconect, ref VlrCosto, "", ref Subtotal);
                    // GrabaTransaccion(IdTipoMovto, secuencia, myconect, IdCliente, FecMovto, VlrTotal, Subtotal, VlrDsto, VlrIva, "", Idusuario, "", "", "", "", "", Idpunto, IdTurno, VlrArqueo, detalle, FecVence, VlrRetFte, VlrIca, DesdeConv, NumFactura, idvendedor); // ERROR: CS7036
                    GrabaMovtoTransaccion(IdTipoMovto, secuencia, Cencosto, IdBodega, IdProducto, TipoVenta, ClaMovto, myconect, FecMovto, Cantidad, NumFactura, TasaIva, TasaDscto, IdCliente, VlrIva, Subtotal, VlrDsto, VlrTotal, VlrUnidad, Idusuario, Idpunto, IdTurno,
                        Valadm, IvaAdm, IvaTiq, OtroImp, AeroPort, ImpComb, VlrCosto, TasaRetFte, VlrRetFte, TasaIca, VlrIca, MovtoPos);
                    break;
            }

            return null;
        }

        // ── CargaOtrosImpuestos ─────────────────────────────────────────────
        private DataTable CargaOtrosImpuestos()
        {
            // Inv_frmotroimp frmOtrosIMp = new Inv_frmotroimp(); // ERROR: CS0246
            // frmOtrosIMp.ShowDialog(); // ERROR: CS0103
            // return frmOtrosIMp.DsdatosImp.Copy(); // ERROR: CS0103
            return new DataTable();
        }

        // ── EliminaMovimiento (overload 1 - without Cencosto/IdBodega) ──────
        public object EliminaMovimiento(int IdTipoMovto, double Secuencia, string idproducto,
            string Tipoventa, double Cantidad, double ValorUnidad, DateTime fecmovto,
            double Vlriva, double Subtotal, double VlrDsto, double neto,
            OdbcConnection myconnect, double VlrRetFte, decimal TasaRetFte,
            double VlrIca, decimal TasaIca)
        {
            string estado = "C";
            double CantDispo = 0, Costo = 0;
            DialogResult msgok;
            int CrtlExistencia = 0;
            double NewCosto = 0;
            double CantInicial = 0, UltCosto = 0, CostoInicial = 0;

            // this.msginvconf.buscaPeriodo("post", myconnect, "", "", fecmovto, ref estado, "", Strings.Format(fecmovto, "yyyy")); // ERROR: CS1503, CS1620
            switch (estado)
            {
                case "P":
                    msgok = MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    switch (msgok)
                    {
                        case DialogResult.No:
                            return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            double _dummy1 = 0, _dummy2 = 0;
            this.BuscaInventario(Convert.ToDouble(idproducto), Strings.Format(fecmovto, "yyyyMM"), myconnect, ref CantInicial, ref _dummy1, ref _dummy2, ref CantDispo, ref Costo, ref UltCosto, ref CostoInicial);

            // msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, "", "", "", "", "", ref CrtlExistencia); // ERROR: CS7036
            switch (CrtlExistencia)
            {
                case 0:
                    NewCosto = BorraCosto(Convert.ToDouble(idproducto), Cantidad, ValorUnidad, myconnect, Costo, CantDispo);
                    this.GrabaInventario(Convert.ToDouble(idproducto), CantInicial, Cantidad * -1, 0, CostoInicial, NewCosto, Costo, Strings.Format(fecmovto, "yyyyMM"), myconnect);
                    break;
                case 1:
                    this.GrabaInventario(Convert.ToDouble(idproducto), CantInicial, 0, Cantidad * -1, CostoInicial, Costo, UltCosto, Strings.Format(fecmovto, "yyyyMM"), myconnect);
                    break;
            }

            // GrabaMovtoTransaccion(IdTipoMovto, Secuencia, Convert.ToDouble(idproducto), Tipoventa, " ", myconnect, fecmovto, Cantidad * -1, "0", 0, 0, null, Vlriva * -1, Subtotal * -1, VlrDsto * -1, neto * -1, ValorUnidad * -1, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, TasaRetFte * -1, (decimal)(VlrRetFte * -1), TasaIca * -1, (decimal)(VlrIca * -1)); // ERROR: CS1501
            // GrabaTransaccion(IdTipoMovto, Secuencia, myconnect, "", fecmovto, neto * -1, Subtotal * -1, VlrDsto * -1, Vlriva * -1, "", "", "", "", "", "", "", 0, 0, 0, "", DateTime.MinValue, (decimal)(VlrRetFte * -1), (decimal)(VlrIca * -1)); // ERROR: CS7036

            // ok = this.BuscaMovtoTransaccion(IdTipoMovto, Secuencia, idproducto, Tipoventa, myconnect, ref neto); // ERROR: CS1503
            if (neto == 0)
            {
                // EliminaMovtoTransaccion(IdTipoMovto, Secuencia, idproducto, Tipoventa, myconnect); // ERROR: CS1503
            }

            return null;
        }

        // ── EliminaMovimiento (overload 2 - with Cencosto/IdBodega) ─────────
        public virtual object EliminaMovimiento(int IdTipoMovto, double Secuencia,
            string Cencosto, double IdBodega, string idproducto, string Tipoventa,
            double Cantidad, double ValorUnidad, DateTime fecmovto, double Vlriva,
            double Subtotal, double VlrDsto, double neto, double ConsecMovto,
            OdbcConnection myconnect, double VlrRetFte, decimal TasaRetFte,
            double VlrIca, decimal TasaIca, string orden_Pedido)
        {
            string estado = "C";
            double CantDispo = 0, Costo = 0;
            DialogResult msgok;
            int CrtlExistencia = 0;
            double NewCosto = 0;
            double CantInicial = 0, UltCosto = 0, CostoInicial = 0;

            // this.msginvconf.buscaPeriodo("post", myconnect, "", "", fecmovto, ref estado, "", Strings.Format(fecmovto, "yyyy")); // ERROR: CS1503, CS1620
            switch (estado)
            {
                case "P":
                    msgok = MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    switch (msgok)
                    {
                        case DialogResult.No:
                            return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            switch (orden_Pedido)
            {
                case "N":
                    double _dummy3 = 0, _dummy4 = 0;
                    this.BuscaInventario(Convert.ToDouble(idproducto), Strings.Format(fecmovto, "yyyyMM"), Cencosto, IdBodega, myconnect, ref CantInicial, ref _dummy3, ref _dummy4, ref CantDispo, ref Costo, ref UltCosto, ref CostoInicial);
                    // msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, "", "", "", "", "", ref CrtlExistencia); // ERROR: CS7036
                    switch (CrtlExistencia)
                    {
                        case 0:
                            NewCosto = BorraCosto(Convert.ToDouble(idproducto), Cantidad, ValorUnidad, myconnect, Costo, CantDispo);
                            this.GrabaInventario(Convert.ToDouble(idproducto), Cencosto, IdBodega, CantInicial, Cantidad * -1, 0, CostoInicial, NewCosto, Costo, Strings.Format(fecmovto, "yyyyMM"), myconnect);
                            break;
                        case 1:
                            this.GrabaInventario(Convert.ToDouble(idproducto), Cencosto, IdBodega, CantInicial, 0, Cantidad * -1, CostoInicial, Costo, UltCosto, Strings.Format(fecmovto, "yyyyMM"), myconnect);
                            break;
                    }
                    // GrabaTransaccion(IdTipoMovto, Secuencia, myconnect, "", fecmovto, neto * -1, Subtotal * -1, VlrDsto * -1, Vlriva * -1, "", "", "", "", "", "", "", 0, 0, 0, "", DateTime.MinValue, (decimal)(VlrRetFte * -1), (decimal)(VlrIca * -1)); // ERROR: CS7036
                    // EliminaMovtoTransaccion(IdTipoMovto, Secuencia, Cencosto, IdBodega, idproducto, Tipoventa, ConsecMovto, myconnect); // ERROR: CS1503
                    break;
                default:
                    // GrabaTransaccion_ordenPedido(IdTipoMovto, Secuencia, myconnect, "", fecmovto, neto * -1, Subtotal * -1, VlrDsto * -1, Vlriva * -1, "", "", "", "", "", "", "", 0, 0, 0, "", DateTime.MinValue, (decimal)(VlrRetFte * -1), (decimal)(VlrIca * -1)); // ERROR: CS7036
                    // EliminaMovtoTransaccion_orden_Pedido(IdTipoMovto, Secuencia, Cencosto, IdBodega, idproducto, Tipoventa, ConsecMovto, myconnect); // ERROR: CS1503
                    break;
            }

            return null;
        }

        // ── BorraCosto ──────────────────────────────────────────────────────
        public double BorraCosto(double IdProducto, double Cantidad, double ValorUnidad,
            OdbcConnection myconnect, double Costopro, double CantDispo)
        {
            double Valor = 0;
            double ValCompra = 0, NewCosto = 0;
            Valor = Costopro * CantDispo;
            ValCompra = Cantidad * ValorUnidad;
            if ((CantDispo - Cantidad) != 0)
            {
                NewCosto = Math.Round((Valor - ValCompra) / (CantDispo - Cantidad), 4);
            }
            else
            {
                NewCosto = 0;
            }
            return NewCosto;
        }

        // ── CargaDocumento ──────────────────────────────────────────────────
        public void CargaDocumento(DataGridView GrillaMovtoCpte, int IdTipoMovto,
            string ConseCpte, OdbcConnection Myconect, Form Myforma)
        {
            string stmysqlLocal;
            double item = 0;
            ERP.Core.Compartido.Controles.Barraprogress FrmProgres = new ERP.Core.Compartido.Controles.Barraprogress("Cargando Movimientos", Myforma);
            int canreg = 0, fila = 0;

            stmysqlLocal = "Select movtos.IdProducto, maepro.Descripcion, movtos.Tipoventa,movtos.Cantidad, movtos.VlrUnidad,movtos.SubTotal,movtos.TasaIva,movtos.TasaDscto,invdoc.estado,movtos.idubicacion,movtos.idbodega,movtos.consecmovto,movtos.costo,movtos.retfte,movtos.tasaica,movtos.Vlrica,movtos.VlrRetfte  "
                         + "from inv_movtos movtos inner join inv_productos maepro on movtos.IdProducto = maepro.IdProducto"
                         + " inner join inv_docs invdoc on movtos.IdTipoMovto = invdoc.IdTipoMovto and movtos.Secuencia = invdoc.Secuencia"
                         + " where movtos.IdTipoMovto=" + IdTipoMovto + " and movtos.Secuencia = '" + ConseCpte + "' "
                         + "order by movtos.consecmovto";

            FrmProgres.DefineMaximo(stmysqlLocal, Myconect);
            FrmProgres.Show();

            Application.DoEvents();
            GrillaMovtoCpte.Rows.Clear();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysqlLocal, Myconect, "CargaDocumento", myRead, "TblCargaDomto");
            canreg = myRead.Tables["TblCargaDomto"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblCargaDomto"].Rows[fila];
                GrillaMovtoCpte.Rows.Add(row["IdProducto"], row["Descripcion"], row["Tipoventa"], row["Cantidad"], row["VlrUnidad"],
                    row["SubTotal"], row["TasaIva"], row["TasaDscto"], row["estado"], row["idubicacion"], row["idbodega"], Convert.ToDouble(row["consecmovto"]), row["costo"], row["retfte"], row["tasaica"], row["Vlrica"], row["VlrRetfte"]);
                FrmProgres.PerformStep();
                fila += 1;
            }
            myRead.Dispose();
            FrmProgres.Close();
        }

        // ── BuscaInventario (overload 1 - without Cencosto/IdBodega) ────────
        public bool BuscaInventario(double IdProducto, string Periodo, OdbcConnection myconnect,
            ref double CantInicial, ref double CantCompra, ref double CantVendida,
            ref double Cantfinal, ref double Costo, ref double UltCosto, ref double costoinicial)
        {
            string sCantInicial = CantInicial.ToString();
            string sCantCompra = CantCompra.ToString();
            string sCantVendida = CantVendida.ToString();
            string sCantfinal = Cantfinal.ToString();

            stmysql = "select CantInicial as campo1, CantCompra as campo2, Cantvendida as campo3,CantFinal as campo4 from inv_ctrlinv where Idperiodo = '" + Periodo + "' And IdProducto = " + IdProducto;
            ok = this.MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaExistencia", ref sCantInicial, ref sCantCompra, ref sCantVendida, ref sCantfinal);
            double.TryParse(sCantInicial, out CantInicial);
            double.TryParse(sCantCompra, out CantCompra);
            double.TryParse(sCantVendida, out CantVendida);
            double.TryParse(sCantfinal, out Cantfinal);

            string sCosto = Costo.ToString();
            string sUltCosto = UltCosto.ToString();
            string scostoinicial = costoinicial.ToString();

            stmysql = "select Costo as campo1,UltCostoPro as campo2,costoinicial as campo3 from inv_ctrlinv where Idperiodo = '" + Periodo + "' And IdProducto = " + IdProducto;
            ExecSql3(stmysql, myconnect, "BuscaExistencia", ref sCosto, ref sUltCosto, ref scostoinicial);
            double.TryParse(sCosto, out Costo);
            double.TryParse(sUltCosto, out UltCosto);
            double.TryParse(scostoinicial, out costoinicial);

            return ok;
        }

        // ── BuscaInventario (overload 2 - with Cencosto/IdBodega) ───────────
        public virtual bool BuscaInventario(double IdProducto, string Periodo,
            string Cencosto, double Idbodega, OdbcConnection myconnect,
            ref double CantInicial, ref double CantCompra, ref double CantVendida,
            ref double Cantfinal, ref double Costo, ref double UltCosto, ref double costoinicial)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdataset = new DataSet();

            StBuilder.Append("select CantInicial,CantCompra,Cantvendida,CantFinal,Costo,UltCostoPro,costoinicial ");
            StBuilder.Append("from inv_ctrlinv ");
            StBuilder.Append("where Idperiodo = '" + Periodo + "' And IdProducto = " + IdProducto);
            StBuilder.Append(" and idubicacion='" + Cencosto + "' and idbodega=" + Idbodega);

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaInventario", Dsdataset, "tblctrlinv");

            switch (ok)
            {
                case true:
                    DataRow row = Dsdataset.Tables["tblctrlinv"].Rows[0];
                    CantInicial = Convert.ToDouble(row["CantInicial"]);
                    CantCompra = Convert.ToDouble(row["CantCompra"]);
                    CantVendida = Convert.ToDouble(row["Cantvendida"]);
                    Cantfinal = Convert.ToDouble(row["CantFinal"]);
                    Costo = Convert.ToDouble(row["Costo"]);
                    UltCosto = Convert.ToDouble(row["UltCostoPro"]);
                    costoinicial = Convert.ToDouble(row["costoinicial"]);
                    break;
                case false:
                    CantInicial = 0; CantCompra = 0; CantVendida = 0; Cantfinal = 0;
                    Costo = 0; UltCosto = 0; costoinicial = 0;
                    break;
            }

            return ok;
        }

        // ── BuscaInventario (overload 3 - returns DataTable) ────────────────
        public virtual DataTable BuscaInventario(OdbcConnection myconnect, double IdProducto, string Periodo)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdataset = new DataSet();

            StBuilder.Append("select ctrlinv.idubicacion,ctrlinv.idbodega,ctrlinv.CantInicial,ctrlinv.CantCompra,ctrlinv.Cantvendida,ctrlinv.CantFinal,");
            StBuilder.Append("ctrlinv.Costo,ctrlinv.UltCostoPro,ctrlinv.costoinicial,ubi.descripcion as NomUbicacion,bod.descripcion as NomBodega ");
            StBuilder.Append("from inv_ctrlinv ctrlinv ");
            StBuilder.Append("inner join inv_ubicacion ubi on ctrlinv.idubicacion=ubi.idubicacion ");
            StBuilder.Append("inner join inv_bodegas bod on ctrlinv.idubicacion=bod.idubicacion and ctrlinv.idbodega=bod.idbodega ");
            StBuilder.Append("where ctrlinv.Idperiodo = '" + Periodo + "' And ctrlinv.IdProducto = " + IdProducto);

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaInventario", Dsdataset, "tblctrlinv");

            return Dsdataset.Tables["tblctrlinv"];
        }

        // ── BuscaInventarioConsolidado ──────────────────────────────────────
        public virtual DataTable BuscaInventarioConsolidado(OdbcConnection myconnect, double IdProducto, string Periodo)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdataset = new DataSet();

            StBuilder.Append("select ctrlinv.Idperiodo,ctrlinv.IdProducto,ctrlinv.descripcion,ctrlinv.CantInicial,ctrlinv.CantCompra,ctrlinv.Cantvendida,");
            StBuilder.Append("ctrlinv.CantFinal,ctrlinv.costoprom ");
            StBuilder.Append("from inv_costoconsolidado_vw ctrlinv ");
            StBuilder.Append("where ctrlinv.Idperiodo = '" + Periodo + "' And ctrlinv.IdProducto = " + IdProducto);

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaInventarioConsolidado", Dsdataset, "tblinvcons");

            return Dsdataset.Tables["tblinvcons"];
        }

        // ── GrabaExistencia (overload 1 - without CenCosto/IdBodega) ────────
        public string GrabaExistencia(double IdProducto, ref double Cantidad, double ValorUnidad,
            double IdTipoMovto, string TipoVenta, DateTime Fecmovto, OdbcConnection myconnect,
            ref double VlrCosto, string TipoTran, ref double VlrSubtotal)
        {
            int CtrlExistencia = 0;
            double NewCosto = 0, CostoAnterior = 0;
            double CantInicial = 0, CantCompra = 0, cantvendida = 0, Cantfinal = 0, costo = 0, UltCosto = 0;
            double NumCant = 0;
            string ClaseTran = " ";
            double CostoInicial = 0;
            string Costea = "N";

            // this.msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, "", "", "", "", "", ref CtrlExistencia, 0, "", "", "", "", "", ref Costea); // ERROR: CS7036
            // this.msginvconf.BuscaProductos(IdProducto, myconnect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref NumCant); // ERROR: CS7036

            switch (TipoVenta)
            {
                case "P":
                    ValorUnidad = (ValorUnidad * Cantidad) / (Cantidad * NumCant);
                    Cantidad *= NumCant;
                    break;
            }

            this.BuscaInventario(IdProducto, Strings.Format(Fecmovto, "yyyyMM"), myconnect, ref CantInicial, ref CantCompra, ref cantvendida, ref Cantfinal, ref costo, ref UltCosto, ref CostoInicial);

            switch (CtrlExistencia)
            {
                case 0:
                    switch (Costea)
                    {
                        case "Y":
                            if (Cantidad != 0)
                            {
                                ValorUnidad = VlrSubtotal / Cantidad;
                            }
                            NewCosto = CalculaCosotoProducto(IdProducto, Cantidad, ValorUnidad, myconnect, costo, Cantfinal);
                            break;
                        case "N":
                            NewCosto = costo;
                            costo = UltCosto;
                            break;
                    }
                    this.GrabaInventario(IdProducto, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "C";
                    break;
                case 1:
                    GrabaInventario(IdProducto, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "V";
                    break;
                case 2:
                    if (TipoTran != "")
                    {
                        if (TipoTran == "C")
                        {
                            GrabaInventario(IdProducto, CantInicial, Cantidad, 0, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                        }
                        else
                        {
                            GrabaInventario(IdProducto, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                        }
                    }
                    else
                    {
                        if (Cantidad < 0)
                        {
                            Cantidad = Cantidad * -1;
                            GrabaInventario(IdProducto, CantInicial, Cantidad, 0, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "C";
                        }
                        else
                        {
                            GrabaInventario(IdProducto, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "V";
                        }
                    }
                    break;
            }

            VlrCosto = costo;
            return ClaseTran;
        }

        // ── GrabaExistencia (overload 2 - with CenCosto/IdBodega) ───────────
        public virtual string GrabaExistencia(double IdProducto, string CenCosto, double IdBodega,
            ref double Cantidad, double ValorUnidad, double IdTipoMovto, string TipoVenta,
            DateTime Fecmovto, OdbcConnection myconnect, ref double VlrCosto,
            string TipoTran, ref double VlrSubtotal, string Estado)
        {
            int CtrlExistencia = 0;
            double NewCosto = 0, CostoAnterior = 0;
            double CantInicial = 0, CantCompra = 0, cantvendida = 0, Cantfinal = 0, costo = 0, UltCosto = 0;
            double NumCant = 0;
            string ClaseTran = " ";
            double CostoInicial = 0;
            string Costea = "N";
            double ValCantidad = 0;

            // this.msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, "", "", "", "", "", ref CtrlExistencia, 0, "", "", "", "", "", ref Costea); // ERROR: CS7036
            // this.msginvconf.BuscaProductos(IdProducto, myconnect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref NumCant); // ERROR: CS7036

            ValCantidad = Cantidad;

            switch (TipoVenta)
            {
                case "P":
                    ValorUnidad = (ValorUnidad * Cantidad) / (Cantidad * NumCant);
                    Cantidad *= NumCant;
                    break;
            }

            this.BuscaInventario(IdProducto, Strings.Format(Fecmovto, "yyyyMM"), CenCosto, IdBodega, myconnect, ref CantInicial, ref CantCompra, ref cantvendida, ref Cantfinal, ref costo, ref UltCosto, ref CostoInicial);

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
                    this.GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "C";
                    costo = NewCosto;
                    break;
                case 1:
                    GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "V";
                    break;
                case 2:
                    if (TipoTran != "")
                    {
                        if (TipoTran == "C")
                        {
                            GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                        }
                        else
                        {
                            GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                        }
                    }
                    else
                    {
                        if (Cantidad < 0)
                        {
                            Cantidad = Cantidad * -1;
                            ValCantidad = ValCantidad * -1;
                            GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "C";
                        }
                        else
                        {
                            GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
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
                                GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                                costo = NewCosto;
                                break;
                            default:
                                GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                                break;
                        }
                    }
                    else
                    {
                        if (Cantidad < 0)
                        {
                            ValCantidad = ValCantidad * -1;
                            Cantidad = Cantidad * -1;
                            if (Cantidad != 0)
                            {
                                ValorUnidad = VlrSubtotal / Cantidad;
                            }
                            NewCosto = CalculaCosotoProducto(IdProducto, Cantidad, ValorUnidad, myconnect, costo, Cantfinal);
                            GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "C";
                            costo = NewCosto;
                        }
                        else
                        {
                            GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, costo, UltCosto, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                            ClaseTran = "V";
                        }
                    }
                    break;
            }

            Cantidad = ValCantidad;
            VlrCosto = costo;
            return ClaseTran;
        }

        // Convenience overload without Estado parameter
        public virtual string GrabaExistencia(double IdProducto, string CenCosto, double IdBodega,
            ref double Cantidad, double ValorUnidad, double IdTipoMovto, string TipoVenta,
            DateTime Fecmovto, OdbcConnection myconnect, ref double VlrCosto,
            string TipoTran, ref double VlrSubtotal)
        {
            return GrabaExistencia(IdProducto, CenCosto, IdBodega, ref Cantidad, ValorUnidad, IdTipoMovto, TipoVenta, Fecmovto, myconnect, ref VlrCosto, TipoTran, ref VlrSubtotal, "");
        }

        // ── GrabaInventario (overload 1 - without Cencosto/IdBodega) ────────
        public void GrabaInventario(double IdProducto, double CantInicial, double CantCompra,
            double CantVendida, double CostoInicial, double Costo, double UltCostoPro,
            string Periodo, OdbcConnection myconnect)
        {
            ok = this.BuscaInventario(IdProducto, Periodo, myconnect);
            switch (ok)
            {
                case false:
                    stmysql = "insert into inv_ctrlinv(Idperiodo,Idproducto,CantInicial,CantCompra,CantVendida,CostoInicial,CantFinal,Costo,UltCostoPro) "
                            + "values ('" + Periodo + "','" + IdProducto + "','" + CantInicial + "','" + CantCompra + "','"
                            + CantVendida + "','" + CostoInicial + "','" + (CantInicial + CantCompra - CantVendida) + "','" + Costo + "','" + UltCostoPro + "')";
                    ExecSql(stmysql, myconnect, "GrabaInventario");
                    break;
                case true:
                    stmysql = "update inv_ctrlinv  set CantInicial = " + CantInicial + ", CantFinal = CantInicial + ( CantCompra + " + CantCompra + ")  - (CantVendida + " + CantVendida + "), CantCompra = CantCompra + " + CantCompra
                            + ", CantVendida = CantVendida +" + CantVendida + ",  Costo = " + Costo + ",UltCostoPro = " + UltCostoPro
                            + ",CostoInicial = " + CostoInicial
                            + " where Idperiodo = " + Periodo + " and Idproducto = " + IdProducto;
                    ExecSql(stmysql, myconnect, "GrabaInventario");
                    break;
            }
        }

        // Helper overload for BuscaInventario without all ref params
        private bool BuscaInventario(double IdProducto, string Periodo, OdbcConnection myconnect)
        {
            double _ci = 0, _cc = 0, _cv = 0, _cf = 0, _co = 0, _uc = 0, _coi = 0;
            return BuscaInventario(IdProducto, Periodo, myconnect, ref _ci, ref _cc, ref _cv, ref _cf, ref _co, ref _uc, ref _coi);
        }

        // ── GrabaInventario (overload 2 - with Cencosto/IdBodega) ───────────
        public virtual void GrabaInventario(double IdProducto, string CenCosto, double IdBodega,
            double CantInicial, double CantCompra, double CantVendida, double CostoInicial,
            double Costo, double UltCostoPro, string Periodo, OdbcConnection myconnect)
        {
            ok = this.BuscaInventario(IdProducto, Periodo, CenCosto, IdBodega, myconnect);
            switch (ok)
            {
                case false:
                    stmysql = "insert into inv_ctrlinv(Idperiodo,Idproducto,idubicacion,idbodega,CantInicial,CantCompra,CantVendida,CostoInicial,CantFinal,Costo,UltCostoPro) "
                            + "values ('" + Periodo + "','" + IdProducto + "','" + CenCosto + "','" + IdBodega + "','" + CantInicial + "','" + CantCompra + "','"
                            + CantVendida + "','" + CostoInicial + "','" + (CantInicial + CantCompra - CantVendida) + "','" + Costo + "','" + UltCostoPro + "')";
                    break;
                case true:
                    stmysql = "update inv_ctrlinv  set CantInicial = " + CantInicial + ", CantFinal = CantInicial + ( CantCompra + " + CantCompra + ")  - (CantVendida + " + CantVendida + "), CantCompra = CantCompra + " + CantCompra
                            + ", CantVendida = CantVendida +" + CantVendida + ",  Costo = " + Costo + ",UltCostoPro = " + UltCostoPro
                            + ",CostoInicial = " + CostoInicial
                            + " where Idperiodo = " + Periodo + " and Idproducto = " + IdProducto + " and idubicacion='" + CenCosto + "' and idbodega=" + IdBodega;
                    break;
            }
            ExecSql(stmysql, myconnect, "GrabaInventario");
        }

        // Helper overload for BuscaInventario with Cencosto/IdBodega without all ref params
        private bool BuscaInventario(double IdProducto, string Periodo, string Cencosto, double Idbodega, OdbcConnection myconnect)
        {
            double _ci = 0, _cc = 0, _cv = 0, _cf = 0, _co = 0, _uc = 0, _coi = 0;
            return BuscaInventario(IdProducto, Periodo, Cencosto, Idbodega, myconnect, ref _ci, ref _cc, ref _cv, ref _cf, ref _co, ref _uc, ref _coi);
        }

        // ── CalculaCosotoProducto ───────────────────────────────────────────
        public double CalculaCosotoProducto(double IdProducto, double Cantidad, double ValorUnidad,
            OdbcConnection myconnect, double Costopro, double CantDispo)
        {
            double Valor = 0;
            double ValCompra = 0, NewCosto = 0;

            if (Costopro < 0)
                Costopro = 0;

            if (CantDispo < 0)
                CantDispo = 0;

            Valor = Costopro * CantDispo;
            ValCompra = Cantidad * ValorUnidad;
            if ((CantDispo + Cantidad) > 0)
            {
                NewCosto = Math.Round((Valor + ValCompra) / (CantDispo + Cantidad), 4);
            }
            else
            {
                NewCosto = Costopro;
            }
            return NewCosto;
        }

    } // end partial class msginv
} // end namespace msginv
