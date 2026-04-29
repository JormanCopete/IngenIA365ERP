using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Services
{
    public class clsmsgtiket
    {
        private StreamReader streamToPrint;
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.Inventario.Services.msginv msginv = new ERP.Core.Inventario.Services.msginv();
        private ERP.Core.Inventario.Services.ClsInvConfig MsgInvConf = new ERP.Core.Inventario.Services.ClsInvConfig();
        private ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgsyscop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private stdatos DatosTiket;

        public struct stdatos
        {
            public string NomRescomp;
            public string NombreComp;
            public string nit;
            public string Direccion;
            public string Telefono;
            public string Factura;
            public string Vendedor;
            public string Idcliente;
            public string NomCliente;
            public string Caja;
            public string Turno;
            public DateTime Fecha;
            public string Hora;
            public string DescTipoMovto;
            public double IdTipoMovto;
            public double ConseMovto;
            public double Efectivo;
            public double tarjDebito;
            public double TarjCredito;
            public double Cheque;
            public double Cuotas;
            public double Vlrtotal;
            public double Subtotal;
            public string Prefijo;
            public string Resolucion;
            public int RegimenIva;
            public DateTime FecResol;
            public string NumInicial;
            public string NumFinal;
            public string StImpTirilla;
            public string IdUsuario;
        }

        private string Lin = Strings.Replace(Strings.Space(60), Strings.Space(1), "-");
        private bool entrar_cierre_PrintPage = true;
        private int recorrer_cierre_PrintPage = 0;
        private string StCantBonos = "0";
        private double tamanorTirillaBonos = 0;
        private int contadorBonos = 0;
        private double TamanoHoja = 0;

        private double CantProd_cierre_PrintPage = 0, Total_cierre_PrintPage = 0;
        private bool entro_ImprimeMovimientos = false;
        private bool entro_ImprimeBasesIva = false;
        private int validProceso_ImprimeMovimientos = 0;
        private int validaProceso_ImprimeBasesIva = 0;
        private double ypos_historial = 0;
        private string forma_Impr_Ticket_Cierre = "D";

        public clsmsgtiket()
        {
            MyOdbcConet.MyOdbcConect(varini);
        }

        ~clsmsgtiket()
        {
        }

        // ── ImprimeTiket ───────────────────────────────────────────────────
        public void ImprimeTiket(int tipoMovto, double ConseTipoMovto, OdbcConnection myConnect)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrintPage += new PrintPageEventHandler(pd_PrintPage);
            BucaDatosTiket(tipoMovto, ConseTipoMovto, myConnect);

            float topMargin = 320;
            StCantBonos = "0";
            tamanorTirillaBonos = 0;
            contadorBonos = 0;
            TamanoHoja = 0;
            double topMarginD = topMargin;
            Calcular_Heihgt_PaperSize(ref topMarginD);
            topMargin = (float)topMarginD;
            if (topMargin > 4500)
            {
                topMargin = 4500;
            }
            PaperSize Mewpage = new PaperSize("Custom paper", 800, (int)topMargin);
            pd.DefaultPageSettings.PaperSize = Mewpage;
            entrar_cierre_PrintPage = true;

            pd.Print();
            pd.Dispose();
        }

        // ── ImprimeTiketPrestamo ───────────────────────────────────────────
        public void ImprimeTiketPrestamo(int tipoMovto, double ConseTipoMovto, OdbcConnection myConnect)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrintPage += new PrintPageEventHandler(pd_PrintPagePrestamo);
            PaperSize Mewpage = new PaperSize("Custom paper", 800, 1500);
            BucaDatosTiket(tipoMovto, ConseTipoMovto, myConnect);
            pd.DefaultPageSettings.PaperSize = Mewpage;
            pd.Print();
            pd.Dispose();
        }

        // ── BucaDatosTiket ─────────────────────────────────────────────────
        private void BucaDatosTiket(int tipoMovto, double ConseTipoMovto, OdbcConnection myConnect)
        {
            int Idpunto = 0, Idturno = 0;
            DateTime Fecing = DateTime.MinValue;
            string Idusuario = "admin";
            string Descripcion = " ", Nomusu = "admin", NomEmp = " ", NomCliente = " ";
            string Nomres = "", Nit = "", Direccion = "", Telefono = "";
            double Subtotal = 0;
            double Efectivo = 0, tarjDebito = 0, TarjCredito = 0, Cheque = 0, Cuotas = 0;
            double Vlrtotal = 0, NumFactura = 0;
            string Idcliente = "99999999999999", StImpTirilla = "N";
            string Resolucion = " ";
            DateTime FecResol = DateTime.MinValue;
            int RegimenIva = 0;
            string Prefijo = " ", NumInicial = " ", NumFinal = " ";
            string ctrlfactura = "9999";

            // MsgInvConf.BuscaTipomovto(tipoMovto, myConnect, ref Descripcion, ref Descripcion, ref Descripcion, ref Descripcion, ref Descripcion, ref Descripcion, ref Descripcion, ref Descripcion, ref Descripcion, ref Descripcion, ref ctrlfactura); // ERROR: CS7036

            // BuscaTipomovto: p3=Descripcion, rest are dummies up to p13=ctrlfactura
            // Reset Descripcion after call since dummies overwrote it
            {
                string _d1 = " ", _d2 = " ", _d3 = " ", _d4 = " ", _d5 = " ", _d6 = " ", _d7 = " ", _d8 = " ", _d9 = " ";
                Descripcion = " ";
                ctrlfactura = "9999";
                // MsgInvConf.BuscaTipomovto(tipoMovto, myConnect, ref Descripcion, ref _d1, ref _d2, ref _d3, ref _d4, ref _d5, ref _d6, ref _d7, ref _d8, ref _d9, ref ctrlfactura); // ERROR: CS7036
            }

            {
                // BuscaDatosFacturacion: p3=Resolucion, p4=FecResol, p5=RegimenIva, p6=Prefijo, p7=NumInicial, p8=NumFinal, then skip to p33=StImpTirilla
                string _s1 = "", _s2 = "", _s3 = "", _s4 = "", _s5 = "", _s6 = "", _s7 = "", _s8 = "", _s9 = "";
                string _s10 = "", _s11 = "", _s12 = "", _s13 = "", _s14 = "", _s15 = "", _s16 = "", _s17 = "";
                string _s18 = "", _s19 = "", _s20 = "", _s21 = "", _s22 = "", _s23 = "", _s24 = "";
                double _dNumIni = 0, _dNumFin = 0;
                int _iReg = 0;
                DateTime _dtFec = DateTime.MinValue;
                // MsgInvConf.BuscaDatosFacturacion(ctrlfactura, myConnect, ref Resolucion, ref FecResol, ref RegimenIva, ref Prefijo, ref _dNumIni, ref _dNumFin, // ERROR: CS7036
                    // ref _s1, ref _s2, ref _s3, ref _s4, ref _s5, ref _s6, ref _s7, ref _s8, ref _s9, ref _s10, ref _s11, ref _s12, ref _s13, ref _s14, // ERROR: CS7036
                    // ref _s15, ref _s16, ref _s17, ref _s18, ref _s19, ref _s20, ref _s21, ref _s22, ref _s23, ref _s24, ref StImpTirilla); // ERROR: CS7036
                NumInicial = _dNumIni.ToString();
                NumFinal = _dNumFin.ToString();
            }

            // msginv.BuscaTransaccion(tipoMovto, ConseTipoMovto, myConnect, ref Idcliente, ref Idpunto, ref Idturno, ref Fecing, ref Idusuario, // ERROR: CS1501
                // ref Efectivo, ref tarjDebito, ref TarjCredito, ref Cheque, ref Cuotas, ref Vlrtotal, ref NumFactura, // ERROR: CS1501
                // ref NumFactura, ref NumFactura, ref NumFactura, ref Subtotal); // ERROR: CS1501

            {
                string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "";
                string _p11 = "", _p12 = "", _p13 = "";
                // msgsys.BuscarCompania(varini.sptCodEmpr, myConnect, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                    // ref _p11, ref _p12, ref _p13, ref Nit, ref Direccion, ref Nomres, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, // ERROR: CS7036
                    // ref NomEmp, ref Telefono); // ERROR: CS7036
            }

            // msgsys.BuscaUsuario(Idusuario, myConnect, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref Nomusu); // ERROR: CS1501
            msgcnt.BuscarTercero(Idcliente, myConnect, ref NomCliente);

            DatosTiket.Caja = Idpunto.ToString();
            DatosTiket.DescTipoMovto = Descripcion;
            DatosTiket.Direccion = Direccion;
            DatosTiket.Factura = NumFactura.ToString();
            DatosTiket.Fecha = Fecing;
            DatosTiket.Hora = Strings.Format(DateTime.Now, "hh:ss tt");
            DatosTiket.nit = Nit;
            DatosTiket.NomRescomp = Nomres;
            DatosTiket.NombreComp = NomEmp;
            DatosTiket.Telefono = Telefono;
            DatosTiket.Turno = Idturno.ToString();
            DatosTiket.Vendedor = Nomusu;
            DatosTiket.Idcliente = Idcliente;
            DatosTiket.NomCliente = NomCliente;
            DatosTiket.IdTipoMovto = tipoMovto;
            DatosTiket.ConseMovto = ConseTipoMovto;
            DatosTiket.Efectivo = Efectivo;
            DatosTiket.TarjCredito = TarjCredito;
            DatosTiket.tarjDebito = tarjDebito;
            DatosTiket.Cheque = Cheque;
            DatosTiket.Cuotas = Cuotas;
            DatosTiket.Vlrtotal = Vlrtotal;
            DatosTiket.Subtotal = Subtotal;
            DatosTiket.Resolucion = Resolucion;
            DatosTiket.FecResol = FecResol;
            DatosTiket.RegimenIva = RegimenIva;
            DatosTiket.NumInicial = NumInicial;
            DatosTiket.NumFinal = NumFinal;
            DatosTiket.Prefijo = Prefijo;
            DatosTiket.StImpTirilla = StImpTirilla;
        }

        // ── pd_PrintPage ───────────────────────────────────────────────────
        private void pd_PrintPage(object sender, PrintPageEventArgs ev)
        {
            OdbcConnection myconect = new OdbcConnection(varini.pstMyconec);
            myconect.Open();
            double ypos = 0;
            float topMargin = 320;
            double count = 0;
            float leftMargin = 5;
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            Font printTitt = new Font("Times New Roman", 8, FontStyle.Bold);
            Font printTitulos = new Font("Times New Roman", 8, FontStyle.Bold);
            double Total = 0, Fila = 0;
            string line = null;
            DataSet dataset_RecorreProducto = new DataSet();
            int CantidadFilas;

            if (entrar_cierre_PrintPage == true)
            {
                if (Convert.ToDouble(StCantBonos) > 0)
                {
                    TamanoHoja = 500 + tamanorTirillaBonos;
                }
                else
                {
                    TamanoHoja = 500;
                }
                ImprimeCabeza(ev, ref topMargin);
            }
            else
            {
                ypos = 3;
            }

            string stmysql = "select movto.IdProducto,Descripcion,Cantidad,VlrUnidad,movto.TasaIva,SubTotal  from inv_movtos movto inner join "
                            + "inv_productos Catalogo on movto.IdProducto  = catalogo.IdProducto "
                            + "where IdTipoMovto = " + this.DatosTiket.IdTipoMovto + " and Secuencia = " + this.DatosTiket.ConseMovto;

            MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "ImprimeTiket", ref dataset_RecorreProducto, "ImprimeTicket");
            CantidadFilas = dataset_RecorreProducto.Tables["ImprimeTicket"].Rows.Count;

            while (recorrer_cierre_PrintPage < dataset_RecorreProducto.Tables["ImprimeTicket"].Rows.Count)
            {
                DataRow row = dataset_RecorreProducto.Tables["ImprimeTicket"].Rows[recorrer_cierre_PrintPage];
                if ((ypos + 45) < 4500)
                {
                    ypos = topMargin + count * printFont.GetHeight(ev.Graphics);

                    line = Strings.Right(Strings.Space(6) + row["IdProducto"], 6) + " ";
                    ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)ypos, new StringFormat());

                    line = Strings.Left(row["Descripcion"] + Strings.Space(12), 12) + " ";
                    ev.Graphics.DrawString(line, printFont, Brushes.Black, 50, (float)ypos, new StringFormat());

                    line = Strings.Right(Strings.Space(4) + Strings.FormatNumber(row["Cantidad"], 0), 4) + " ";
                    ev.Graphics.DrawString(line, printFont, Brushes.Black, 160, (float)ypos, new StringFormat());

                    line = Strings.Right(Strings.Space(8) + Strings.Format(row["Subtotal"], "####,###"), 8);
                    if (Convert.ToDouble(row["TasaIva"]) != 0)
                    {
                        line = line + "*";
                    }
                    ev.Graphics.DrawString(line, printFont, Brushes.Black, 190, (float)ypos, new StringFormat());

                    if (line == null)
                    {
                        break;
                    }

                    count += 1;
                    entrar_cierre_PrintPage = true;
                    recorrer_cierre_PrintPage += 1;
                }
                else
                {
                    entrar_cierre_PrintPage = false;
                    break;
                }
            }
            myconect.Close();

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
            }
            else
            {
                double prueba = (TamanoHoja + 700) - ypos;
                if ((TamanoHoja - ypos + 800) > 4500)
                {
                    ev.HasMorePages = true;
                    entrar_cierre_PrintPage = false;
                    TamanoHoja -= 4500;
                }
                else
                {
                    ev.HasMorePages = false;
                    entrar_cierre_PrintPage = true;
                }
            }

            if (entrar_cierre_PrintPage == true)
            {
                ypos += 10;
                ev.Graphics.DrawString(Lin, printFont, Brushes.Black, leftMargin, (float)ypos, new StringFormat());
                ypos += 10;
                line = "SUBTOTAL .........        " + Strings.Right(Strings.Space(12) + Strings.Format(this.DatosTiket.Subtotal, "$###,###,###"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)ypos, new StringFormat());
                myconect.Open();
                DetalleValores(ev, ref ypos, myconect);
                ImprimeTributaria(ev, ref ypos, myconect);
                ImprimeTotal(ev, ref ypos, myconect);
                ImprimeFormaPago(ev, ref ypos, myconect);
                ImprimePie(ev, ref ypos, myconect);
                switch (this.DatosTiket.StImpTirilla)
                {
                    case "Y":
                        if (Information.IsNumeric(StCantBonos))
                        {
                            if (Convert.ToDouble(StCantBonos) > 0)
                            {
                                for (double f = 0; f <= Convert.ToDouble(StCantBonos) - 1; f++)
                                {
                                    ImprimeTirillaBonos(ev, ref ypos, myconect);
                                }
                                line = "-";
                                ypos += 60;
                                ev.Graphics.DrawString(line, printFont, Brushes.Black, 15, (float)ypos, new StringFormat());
                            }
                        }
                        break;
                }
            }

            myconect.Close();
            myconect.Dispose();
        }

        // ── DetalleValores ─────────────────────────────────────────────────
        private void DetalleValores(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            double VentaGrabada = 0, VenNoGrabada = 0, Iva = 0;
            string line = null, stmysql;
            int totreg = 0, cont = 0;

            stmysql = "select TasaIva,Base,VlrIva  from inv_tasaiva_vw "
                    + "where IdTipoMovto = " + this.DatosTiket.IdTipoMovto + " and Secuencia = " + this.DatosTiket.ConseMovto;

            DataSet myRead = new DataSet();
            MyOdbcConet.ExecuteQueryDataset(stmysql, Myconnect, "DetalleValores", ref myRead, "TblDetValores");
            totreg = myRead.Tables["TblDetValores"].Rows.Count;

            while (cont < totreg)
            {
                DataRow row = myRead.Tables["TblDetValores"].Rows[cont];
                if (Convert.ToDouble(row["TasaIva"]) != 0)
                {
                    VentaGrabada += Convert.ToDouble(row["Base"]);
                    Iva += Convert.ToDouble(row["VlrIva"]);
                }
                else
                {
                    VenNoGrabada += Convert.ToDouble(row["Base"]);
                }
                cont += 1;
            }
            myRead.Dispose();
            TopMargen += 20;
            line = "-----------[DETALLE DEL IVA]-----------";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
            TopMargen += 15;
            line = "Vta Gravada (*)........   " + Strings.Right(Strings.Space(11) + Strings.Format(VentaGrabada, "###,###,##0"), 11) + " +";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
            TopMargen += 15;
            line = "Vta No Gravada ........   " + Strings.Right(Strings.Space(11) + Strings.Format(VenNoGrabada, "###,###,##0"), 11) + " +";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
            TopMargen += 15;
            line = "I V A .........           " + Strings.Right(Strings.Space(11) + Strings.Format(Iva, "###,###,##0"), 11) + " +";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
        }

        // ── ImprimeTributaria ──────────────────────────────────────────────
        private void ImprimeTributaria(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            string line = null, stmysql;
            int totreg = 0, cont = 0;

            TopMargen += 15;
            line = "-------[INFORMACION TRIBUTARIA]--------";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
            TopMargen += 15;
            line = "  %         Vlr Base      Vlr Impuesto";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
            TopMargen += 10;
            line = Lin;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
            TopMargen += 10;

            stmysql = "select TasaIva,Base,VlrIva  from inv_tasaiva_vw "
                    + "where IdTipoMovto = " + this.DatosTiket.IdTipoMovto + " and Secuencia = " + this.DatosTiket.ConseMovto;

            DataSet myRead = new DataSet();
            MyOdbcConet.ExecuteQueryDataset(stmysql, Myconnect, "ImprimeTributaria", ref myRead, "TblImpTribut");
            totreg = myRead.Tables["TblImpTribut"].Rows.Count;

            while (cont < totreg)
            {
                DataRow row = myRead.Tables["TblImpTribut"].Rows[cont];
                line = Strings.Right(Strings.Space(5) + Strings.Format(row["TasaIva"], "#0.00"), 5) + "   "
                     + Strings.Right(Strings.Space(14) + Strings.Format(row["Base"], "###,###,###.00"), 14) + "  "
                     + Strings.Right(Strings.Space(14) + Strings.Format(row["VlrIva"], "###,###,###.00"), 14);

                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                TopMargen += 10;
                cont += 1;
            }
            myRead.Dispose();
        }

        // ── ImprimeCabeza (ticket de venta) ────────────────────────────────
        private void ImprimeCabeza(PrintPageEventArgs ev, ref float topMargin)
        {
            float leftMargin = 5;
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            Font printTitt = new Font("Times New Roman", 8, FontStyle.Bold);
            string line = null;

            Rectangle Linea01 = new Rectangle(new Point(5, 30), new Size(320, 30));
            StringFormat drawFormat = new StringFormat(StringFormatFlags.NoClip);
            drawFormat.LineAlignment = StringAlignment.Near;
            drawFormat.Alignment = StringAlignment.Near;

            topMargin = 30;
            ev.Graphics.DrawString(DatosTiket.NombreComp.Trim(), printFont, Brushes.Black, (RectangleF)Linea01, drawFormat);
            topMargin = 50;
            ev.Graphics.DrawString(DatosTiket.nit.Trim() + " " + "REGIMEN COMUN", printFont, Brushes.Black, leftMargin, topMargin, new StringFormat());
            topMargin = 65;
            ev.Graphics.DrawString(DatosTiket.Direccion.Trim(), printFont, Brushes.Black, leftMargin, topMargin, new StringFormat());
            topMargin = 80;
            ev.Graphics.DrawString("TELF. " + DatosTiket.Telefono.Trim(), printFont, Brushes.Black, leftMargin, topMargin, new StringFormat());
            topMargin = 95;
            line = "FACTURA DE VENTA " + DatosTiket.Prefijo + "-" + Strings.Right("0000000" + DatosTiket.Factura, 7);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, topMargin, new StringFormat());
            topMargin = 110;
            line = "Caja  :" + Strings.Right("000000" + DatosTiket.Caja, 6) + "     " + "Turno :       " + Strings.Right("0000" + DatosTiket.Turno, 4);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, topMargin, new StringFormat());
            topMargin = 125;
            line = "Consec:" + Strings.Right("000000" + DatosTiket.ConseMovto, 6) + "     " + "Fecha :" + Strings.Format(DatosTiket.Fecha, "yyyy-MMM-dd");
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, topMargin, new StringFormat());
            line = "Hora  :" + DatosTiket.Hora;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 140, 140, new StringFormat());
            topMargin = 200;
            ev.Graphics.DrawString("Vendedor :" + DatosTiket.Vendedor, printFont, Brushes.Black, 5, 155, new StringFormat());
            ev.Graphics.DrawString("Cod. Pago: " + DatosTiket.IdTipoMovto + " " + DatosTiket.DescTipoMovto, printFont, Brushes.Black, 5, 170, new StringFormat());
            topMargin = 170;

            if (DatosTiket.Idcliente != "99999999999999")
            {
                topMargin += 15;
                line = "Cliente: " + DatosTiket.Idcliente.Trim() + " ";
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, topMargin, new StringFormat());
                topMargin += 15;
                line = Strings.Mid(DatosTiket.NomCliente.Trim(), 1, 32);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, topMargin, new StringFormat());
            }

            topMargin += 20;
            ev.Graphics.DrawString(Lin, printFont, Brushes.Black, 5, topMargin, new StringFormat());
            topMargin += 20;
            string Titulos = "Item       Descripcion              Cant    Total ";
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 5, topMargin, new StringFormat());
            topMargin += 20;
            Titulos = Lin;
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 5, topMargin, new StringFormat());
            topMargin += 20;
        }

        // ── ImprimeTotal ───────────────────────────────────────────────────
        public void ImprimeTotal(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            string line = null;

            ev.Graphics.DrawString(Lin, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
            line = "TOTAL ............        " + Strings.Right(Strings.Space(12) + Strings.Format(this.DatosTiket.Vlrtotal, "$###,###,###"), 12);
            TopMargen += 15;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
        }

        // ── ImprimeFormaPago ───────────────────────────────────────────────
        public void ImprimeFormaPago(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            string line = null;
            double Cambio = 0;

            TopMargen += 15;
            line = "------------[FORMA DE PAGO]------------";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 15;

            if (Convert.ToDouble(DatosTiket.Efectivo) != 0)
            {
                line = "Efectivo                  " + Strings.Right(Strings.Space(12) + Strings.Format(DatosTiket.Efectivo, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                TopMargen += 15;
            }

            if (Convert.ToDouble(DatosTiket.Cheque) != 0)
            {
                line = "Cheque                    " + Strings.Right(Strings.Space(12) + Strings.Format(DatosTiket.Cheque, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                TopMargen += 15;
            }

            if (Convert.ToDouble(DatosTiket.tarjDebito) != 0)
            {
                line = "Tarj. Debito              " + Strings.Right(Strings.Space(12) + Strings.Format(DatosTiket.tarjDebito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                TopMargen += 15;
            }

            if (Convert.ToDouble(DatosTiket.TarjCredito) != 0)
            {
                line = "Tarj. Credito             " + Strings.Right(Strings.Space(12) + Strings.Format(DatosTiket.TarjCredito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                TopMargen += 15;
            }

            if (Convert.ToDouble(DatosTiket.Cuotas) != 0)
            {
                line = "Diferido Cuotas           " + Strings.Right(Strings.Space(12) + Strings.Format(DatosTiket.Cuotas, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                TopMargen += 15;
            }

            Cambio = (DatosTiket.Efectivo + DatosTiket.tarjDebito + DatosTiket.TarjCredito + DatosTiket.Cheque + DatosTiket.Cuotas) - DatosTiket.Vlrtotal;
            line = "CAMBIO                    " + Strings.Right(Strings.Space(12) + Strings.Format(Cambio, "$###,###,##0"), 12);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
        }

        // ── ImprimePie ─────────────────────────────────────────────────────
        public void ImprimePie(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            string line = null;

            TopMargen += 15;
            line = "Resol. No. " + DatosTiket.Resolucion + "   " + Strings.Format(DatosTiket.FecResol, "dd-MMM-yyyy");
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 15;
            line = "Factura: " + DatosTiket.Prefijo + "-" + DatosTiket.NumInicial + "   al   " + DatosTiket.Prefijo + "-" + DatosTiket.NumFinal;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 25;
            line = "Factura elaborada en Maquina ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 15;
            line = "Registradora POS ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 15;
            line = "Informatica Creativa Ltda Nit 900046561-3";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            switch (this.DatosTiket.StImpTirilla)
            {
                case "N":
                    line = "-";
                    TopMargen += 80;
                    ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                    break;
                case "Y":
                    if (Information.IsNumeric(StCantBonos))
                    {
                        if (Convert.ToDouble(StCantBonos) == 0)
                        {
                            line = "-";
                            TopMargen += 80;
                            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
                        }
                    }
                    break;
            }
        }

        // ── pd_PrintPagePrestamo ───────────────────────────────────────────
        private void pd_PrintPagePrestamo(object sender, PrintPageEventArgs ev)
        {
            float topMargin = 320;
            this.ImprimeCabezaPrestamo(ev, ref topMargin);
            ImprimeDatosCredito(ev, ref topMargin);
        }

        // ── ImprimeDatosCredito ────────────────────────────────────────────
        private void ImprimeDatosCredito(PrintPageEventArgs ev, ref float topMargin)
        {
            string Titulos;
            double Neto = 0;
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            int Periodicidad = 0, Plazo = 0, Clades = 0;
            DateTime FecDsto = DateTime.MinValue;
            double Cuota = 0;
            decimal TasaInt = 0;
            string DescPerio = null;
            OdbcConnection myconect = new OdbcConnection(varini.pstMyconec);
            myconect.Open();

            // msginv.BuscaTransaccion(DatosTiket.IdTipoMovto, DatosTiket.ConseMovto, myconect, // ERROR: CS1501
                // ref DatosTiket.Idcliente, ref Periodicidad, ref Periodicidad, ref DatosTiket.Fecha, ref DatosTiket.IdUsuario, // ERROR: CS1501
                // ref Neto, ref Neto, ref Neto, ref Neto, ref Neto, ref Neto, ref Neto, // ERROR: CS1501
                // ref Neto, ref Neto, ref Neto, ref Neto, ref Neto, // ERROR: CS1501
                // ref Periodicidad, ref Plazo, ref Clades, ref FecDsto, ref Cuota, ref TasaInt); // ERROR: CS1501

            switch (Periodicidad)
            {
                case 1: DescPerio = "Mensual"; break;
                case 2: DescPerio = "Quincenal"; break;
                case 3: DescPerio = "Decadal"; break;
                case 4: DescPerio = "Semanal"; break;
            }

            topMargin += 20;
            Titulos = "Valor : " + Strings.Right("               " + Strings.Format(Neto, "###,###,###"), 11) + "     " + "Tasa  :" + Strings.Right("               " + Strings.Format(TasaInt, "####.00"), 7);
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 5, topMargin, new StringFormat());

            topMargin += 20;
            Titulos = "Pago  : " + Strings.Right("                " + DescPerio, 11) + "     " + "Plazo :" + Strings.Right("               " + Strings.Format(Plazo, "###,###"), 7);
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 5, topMargin, new StringFormat());

            topMargin += 20;
            Titulos = "Dsto  : " + FecDsto + "      " + "Cuota :" + Strings.Right("               " + Strings.Format(Cuota, "###,###"), 7);
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 5, topMargin, new StringFormat());

            topMargin += 20;
            ev.Graphics.DrawString(Lin, printFont, Brushes.Black, 5, topMargin, new StringFormat());

            topMargin += 40;
            Titulos = "Firma  _________________";
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 5, topMargin, new StringFormat());

            topMargin += 20;
            Titulos = "Cedula _________________";
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 5, topMargin, new StringFormat());

            topMargin += 80;
            Titulos = "-";
            ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, 15, topMargin, new StringFormat());
        }

        // ── ImprimeCabezaPrestamo ──────────────────────────────────────────
        private void ImprimeCabezaPrestamo(PrintPageEventArgs ev, ref float topMargin)
        {
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            string line = null;

            Rectangle Linea01 = new Rectangle(new Point(5, 30), new Size(320, 30));
            Rectangle Linea02 = new Rectangle(new Point(5, 65), new Size(320, 15));
            Rectangle Linea03 = new Rectangle(new Point(5, 80), new Size(320, 15));
            Rectangle Linea05 = new Rectangle(new Point(5, 100), new Size(320, 15));
            Rectangle Linea06 = new Rectangle(new Point(5, 120), new Size(320, 15));
            Rectangle Linea07 = new Rectangle(new Point(5, 140), new Size(320, 15));
            StringFormat drawFormat = new StringFormat(StringFormatFlags.NoClip);
            drawFormat.LineAlignment = StringAlignment.Near;
            drawFormat.Alignment = StringAlignment.Near;

            ev.Graphics.DrawString(DatosTiket.NombreComp.Trim(), printFont, Brushes.Black, (RectangleF)Linea01, drawFormat);
            ev.Graphics.DrawString(DatosTiket.nit.Trim() + " " + "REGIMEN COMUN", printFont, Brushes.Black, (RectangleF)Linea02, drawFormat);
            ev.Graphics.DrawString(DatosTiket.Direccion.Trim() + " " + "TELF." + DatosTiket.Telefono.Trim(), printFont, Brushes.Black, (RectangleF)Linea03, drawFormat);
            line = "FACTURA DE VENTA " + DatosTiket.Prefijo + "-" + Strings.Right("0000000" + DatosTiket.Factura, 7);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea05, drawFormat);
            line = "Caja  :" + Strings.Right("000000" + DatosTiket.Caja, 6) + "     " + "Turno :       " + Strings.Right("0000" + DatosTiket.Turno, 4);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea06, drawFormat);
            line = "Consec:" + Strings.Right("000000" + DatosTiket.ConseMovto, 6) + "     " + "Fecha :" + Strings.Format(DatosTiket.Fecha, "yyyy-MMM-dd");
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea07, drawFormat);
            line = "Hora  :" + DatosTiket.Hora;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 140, 160, new StringFormat());

            ev.Graphics.DrawString("Vendedor :" + DatosTiket.Vendedor, printFont, Brushes.Black, 5, 180, new StringFormat());
            ev.Graphics.DrawString("Cod. Pago: " + DatosTiket.IdTipoMovto + " " + DatosTiket.DescTipoMovto, printFont, Brushes.Black, 5, 200, new StringFormat());
            topMargin = 200;

            if (DatosTiket.Idcliente != "99999999999999")
            {
                topMargin += 20;
                line = "Cliente: " + DatosTiket.Idcliente.Trim() + " " + Strings.Mid(DatosTiket.NomCliente.Trim(), 1, 14);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, topMargin, new StringFormat());
            }

            topMargin += 20;
            ev.Graphics.DrawString(Lin, printFont, Brushes.Black, 5, topMargin, new StringFormat());
        }

        // ── ImprimeTirillaBonos ────────────────────────────────────────────
        public void ImprimeTirillaBonos(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 8, FontStyle.Regular);
            string line = null;

            TopMargen += 20;
            line = "FAVOR LLENE LOS SIGUIENTES DATOS:";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 30;
            line = "NOMBRE    :____________________________";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 30;
            line = "No. CEDULA:____________________________";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 30;
            line = "EMPRESA   :____________________________";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 30;
            line = "TOTAL COMPRA: **" + Strings.Format(this.DatosTiket.Vlrtotal, "$###,###,###");
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 20;
            line = "Fecha:" + Strings.Format(Convert.ToDateTime(this.DatosTiket.Fecha), "yyyy/MM/dd") + " No. Factura:" + Strings.Right("0000000" + this.DatosTiket.Factura, 7);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());
        }

        // ── Calcular_Heihgt_PaperSize ──────────────────────────────────────
        private void Calcular_Heihgt_PaperSize(ref double topMargin)
        {
            double ypos;
            string line = null;

            OdbcConnection myconect = new OdbcConnection(varini.pstMyconec);
            myconect.Open();
            string stmysql = "select movto.IdProducto,Descripcion,Cantidad,VlrUnidad,movto.TasaIva,SubTotal  from inv_movtos movto inner join "
                            + "inv_productos Catalogo on movto.IdProducto  = catalogo.IdProducto "
                            + "where IdTipoMovto = " + this.DatosTiket.IdTipoMovto + " and Secuencia = " + this.DatosTiket.ConseMovto;

            OdbcCommand mycommand = new OdbcCommand(stmysql, myconect);
            OdbcDataReader myread = mycommand.ExecuteReader();

            ypos = topMargin;
            while (myread.Read())
            {
                line = Strings.Right(Strings.Space(6) + myread["IdProducto"], 6) + " ";
                if (line == null) break;
                ypos += 20;
            }

            switch (this.DatosTiket.StImpTirilla)
            {
                case "Y":
                    StCantBonos = Interaction.InputBox("Cuantos bonos?", "SOLIDO - BONOS POR COMPRA", "1");
                    if (Information.IsNumeric(StCantBonos))
                    {
                        if (Convert.ToDouble(StCantBonos) > 0)
                        {
                            tamanorTirillaBonos = 160 * Convert.ToDouble(StCantBonos);
                        }
                    }
                    else
                    {
                        StCantBonos = "0";
                    }
                    break;
                default:
                    StCantBonos = "0";
                    break;
            }

            myconect.Close();
            myread.Close();
            mycommand.Dispose();

            topMargin = ypos + 1300 + tamanorTirillaBonos;
        }

        // ── ImprimeTiketCierre ─────────────────────────────────────────────
        public void ImprimeTiketCierre(string IdUsuario, int IdPunto, int Idturno, DateTime Fecing, OdbcConnection myConnect, string formaImpresion)
        {
            PrintDocument cierre = new PrintDocument();
            cierre.PrintPage += new PrintPageEventHandler(cierre_PrintPage);
            this.BuscaDatosCierre(IdUsuario, IdPunto, Idturno, Fecing, myConnect);
            float topMargin = 320;
            CantProd_cierre_PrintPage = 0;
            Total_cierre_PrintPage = 0;
            double topMarginD = topMargin;
            Calcular_Heihgt_PaperSizeCierre(ref topMarginD, formaImpresion);
            topMargin = (float)topMarginD;
            forma_Impr_Ticket_Cierre = formaImpresion;

            if (topMargin > 4500)
            {
                topMargin = 4500;
            }
            PaperSize Mewpage = new PaperSize("Custom paper", 800, (int)topMargin);
            cierre.DefaultPageSettings.PaperSize = Mewpage;
            cierre.Print();
            cierre.Dispose();
        }

        // ── cierre_PrintPage ───────────────────────────────────────────────
        private void cierre_PrintPage(object sender, PrintPageEventArgs ev)
        {
            OdbcConnection myconect = new OdbcConnection(varini.pstMyconec);
            myconect.Open();
            double ypos = 0;
            float topMargin = 320;
            double count = 0;
            float leftMargin = 5;
            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            Font printTitt = new Font("Times New Roman", 9, FontStyle.Bold);
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

            string stmysql = "select  idproducto as Producto, Resumido as descripcion ,cantidad as cant,VlrUnidad as Unidad,VlrVendido as Total from inv_removtos_vw"
                            + " where idproducto <> '99999999' and idpunto = " + DatosTiket.Caja + " and fecMovto  ='" + Strings.Format(DatosTiket.Fecha, varini.PstForFec) + "'  and Idturno = " + DatosTiket.Turno + " and Idusuario = '" + DatosTiket.IdUsuario + "'"
                            + " and ctrlexistencia = 1";

            MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "cierre_PrintPage", ref dataset_RecorreProducto, "cierre");
            CantidadFilas = dataset_RecorreProducto.Tables["cierre"].Rows.Count;

            while (recorrer_cierre_PrintPage < dataset_RecorreProducto.Tables["cierre"].Rows.Count)
            {
                DataRow row = dataset_RecorreProducto.Tables["cierre"].Rows[recorrer_cierre_PrintPage];
                if (forma_Impr_Ticket_Cierre == "D")
                {
                    if ((ypos + 45) < 4500)
                    {
                        ypos = topMargin + count * printFont.GetHeight(ev.Graphics);

                        line = Strings.Right(Strings.Space(6) + row["Producto"], 6) + " ";
                        ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)ypos, new StringFormat());

                        line = Strings.Left(row["descripcion"] + Strings.Space(12), 12) + " ";
                        ev.Graphics.DrawString(line, printFont, Brushes.Black, 50, (float)ypos, new StringFormat());

                        line = Strings.Right(Strings.Space(4) + row["Cant"], 4) + "  ";
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

                ImprimeMovimientos(ev, ref ypos, myconect);
                ImprimeBasesIva(ev, ref ypos, myconect);
            }

            myconect.Close();
            myconect.Dispose();
        }

        // ── ImprimeCabeza (cierre, overload sin ref topMargin) ─────────────
        private void ImprimeCabeza(PrintPageEventArgs ev)
        {
            string LinLocal = Strings.Replace(Strings.Space(60), Strings.Space(1), "-");
            string Titulos;
            float leftMargin = 5;
            float topMargin = 15;

            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            string line = null;

            Rectangle Linea01 = new Rectangle(new Point(5, 30), new Size(320, 30));
            Rectangle Linea02 = new Rectangle(new Point(5, 65), new Size(320, 15));
            Rectangle Linea03 = new Rectangle(new Point(5, 80), new Size(320, 15));
            Rectangle Linea04 = new Rectangle(new Point(5, 100), new Size(320, 15));
            Rectangle Linea05 = new Rectangle(new Point(5, 140), new Size(320, 15));
            Rectangle Linea06 = new Rectangle(new Point(5, 160), new Size(320, 15));
            Rectangle Linea07 = new Rectangle(new Point(5, 180), new Size(320, 15));
            StringFormat drawFormat = new StringFormat(StringFormatFlags.NoClip);
            drawFormat.LineAlignment = StringAlignment.Near;
            drawFormat.Alignment = StringAlignment.Near;

            ev.Graphics.DrawString(DatosTiket.NomRescomp.Trim(), printFont, Brushes.Black, (RectangleF)Linea01, drawFormat);
            ev.Graphics.DrawString(DatosTiket.nit.Trim() + " " + "REGIMEN COMUN", printFont, Brushes.Black, (RectangleF)Linea02, drawFormat);
            ev.Graphics.DrawString(DatosTiket.Direccion.Trim(), printFont, Brushes.Black, (RectangleF)Linea03, drawFormat);
            ev.Graphics.DrawString("TELF." + DatosTiket.Telefono.Trim(), printFont, Brushes.Black, (RectangleF)Linea04, drawFormat);

            line = "CIERRE DE CAJA - ARQUEOS ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea05, drawFormat);
            line = "Caja  :" + Strings.Right("000000" + DatosTiket.Caja, 6) + "     " + "Turno :       " + Strings.Right("0000" + DatosTiket.Turno, 4);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea06, drawFormat);
            line = "Fecha :" + Strings.Format(DatosTiket.Fecha, "yyyy-MMM-dd") + " " + "Hora:" + DatosTiket.Hora;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, (RectangleF)Linea07, drawFormat);

            ev.Graphics.DrawString("Vendedor :" + DatosTiket.Vendedor, printFont, Brushes.Black, leftMargin, 220, new StringFormat());

            if (forma_Impr_Ticket_Cierre == "D")
            {
                ev.Graphics.DrawString(LinLocal, printFont, Brushes.Black, leftMargin, 260, new StringFormat());
                Titulos = "Item       Descripcion              Cant    Total ";
                ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, leftMargin, 280, new StringFormat());
                Titulos = LinLocal;
                ev.Graphics.DrawString(Titulos, printFont, Brushes.Black, leftMargin, 300, new StringFormat());
            }
        }

        // ── ImprimeMovimientos ─────────────────────────────────────────────
        public void ImprimeMovimientos(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            string line = null;
            double baseVal = 0, total = 0, efectivo = 0, arqueo = 0, tarjdebito = 0, tarjcredito = 0, cheque = 0, arqueos = 0;
            double credito = 0, Cambio = 0;
            float leftMargin = 5;

            TopMargen += 20;
            line = "-------------[MOVIMIENTOS]-------------";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());

            // this.msginv.ResumenFormaPago(DatosTiket.Caja, DatosTiket.Turno, DatosTiket.Fecha, DatosTiket.IdUsuario, Myconnect, ref efectivo, ref credito, ref tarjdebito, ref tarjcredito, ref cheque, ref arqueos, ref Cambio); // ERROR: CS1503
            // this.MsgInvConf.BuscaPunto(DatosTiket.Caja, DatosTiket.Turno, DatosTiket.Fecha, Myconnect, ref baseVal, ref baseVal, ref baseVal, ref baseVal, ref baseVal); // ERROR: CS7036

            if (baseVal != 0)
            {
                TopMargen += 20;
                line = "BASE                       " + Strings.Right(Strings.Space(12) + Strings.Format(baseVal, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
            }
            if (efectivo != 0)
            {
                TopMargen += 20;
                line = "Efectivo                   " + Strings.Right(Strings.Space(12) + Strings.Format(efectivo, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
            }
            if (cheque != 0)
            {
                TopMargen += 20;
                line = "Cheque                     " + Strings.Right(Strings.Space(12) + Strings.Format(cheque, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
            }
            if (tarjdebito != 0)
            {
                TopMargen += 20;
                line = "Tarj. Debito               " + Strings.Right(Strings.Space(12) + Strings.Format(tarjdebito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
            }
            if (tarjcredito != 0)
            {
                TopMargen += 20;
                line = "Tarj. Credito              " + Strings.Right(Strings.Space(12) + Strings.Format(tarjcredito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
            }
            if (credito != 0)
            {
                TopMargen += 20;
                line = "Diferidos Cuotas           " + Strings.Right(Strings.Space(12) + Strings.Format(credito, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
            }
            if (arqueo != 0)
            {
                TopMargen += 20;
                line = "Arqueos                    " + Strings.Right(Strings.Space(12) + Strings.Format(arqueo, "$###,###,##0"), 12);
                ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
            }

            total = efectivo + tarjdebito + tarjcredito + cheque + baseVal + arqueo + credito;
            TopMargen += 20;
            line = "TOTAL                      " + Strings.Right(Strings.Space(12) + Strings.Format(total, "$###,###,##0"), 12);
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());

            TopMargen += 10;
            line = "   ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
        }

        // ── ImprimeBasesIva ────────────────────────────────────────────────
        public void ImprimeBasesIva(PrintPageEventArgs ev, ref double TopMargen, OdbcConnection Myconnect)
        {
            Font printFont = new Font("Times New Roman", 9, FontStyle.Regular);
            int reg = 0, tfila = 0;
            string line, stmysql;
            float leftMargin = 5;

            TopMargen += 10;
            stmysql = "----------[RESUMEN POR TASA]-----------";
            ev.Graphics.DrawString(stmysql, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());

            stmysql = "select fecMovto,idpunto,idturno,idusuario,TasaIva,CtrlExistencia,Base,Iva from inv_retasaiva_vw"
                    + " where idpunto = " + DatosTiket.Caja + " and fecMovto = '" + Strings.Format(DatosTiket.Fecha, varini.PstForFec) + "' and Idturno = " + DatosTiket.Turno + " and Idusuario = '" + DatosTiket.IdUsuario + "'"
                    + " and ctrlexistencia = 1";

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, Myconnect, "ImprimeBasesIva", ref myRead, "TblImpBaseIva");
            reg = myRead.Tables["TblImpBaseIva"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myRead.Tables["TblImpBaseIva"].Rows[tfila];
                TopMargen += 20;
                stmysql = "Base Iva            " + Strings.Format(row["TasaIva"], "###") + "% " + Strings.Right("               " + Strings.Format(row["Base"], "###,###,###"), 15);
                ev.Graphics.DrawString(stmysql, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
                TopMargen += 20;
                stmysql = "                    " + "    " + Strings.Right("               " + Strings.Format(row["Iva"], "###,###,###"), 15);
                ev.Graphics.DrawString(stmysql, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
                tfila += 1;
            }

            TopMargen += 25;
            line = "Factura elaborada en Maquina ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 15;
            line = "Registradora POS ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 15;
            line = "Informatica Creativa Ltda Nit 900046561-3";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 5, (float)TopMargen, new StringFormat());

            TopMargen += 60;
            line = "-";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, leftMargin, (float)TopMargen, new StringFormat());
        }

        // ── Calcular_Heihgt_PaperSizeCierre ────────────────────────────────
        private void Calcular_Heihgt_PaperSizeCierre(ref double topMargin, string formaImpresion)
        {
            double ypos;
            string line = null;
            OdbcConnection myconect = new OdbcConnection(varini.pstMyconec);

            Font printFont = new Font("Courier New", 10, FontStyle.Regular);
            decimal hoja_Height = 15.7335062m;

            if (formaImpresion == "D")
            {
                myconect.Open();

                string stmysql = "select  idproducto as Producto, Resumido as descripcion ,cantidad as cant,VlrUnidad as Unidad,VlrVendido as Total from inv_removtos_vw"
                                + " where idproducto <> '99999999' and idpunto = " + DatosTiket.Caja + " and fecMovto  ='" + Strings.Format(DatosTiket.Fecha, varini.PstForFec) + "'  and Idturno = " + DatosTiket.Turno + " and Idusuario = '" + DatosTiket.IdUsuario + "'"
                                + " and ctrlexistencia = 1";

                OdbcCommand mycommand = new OdbcCommand(stmysql, myconect);
                OdbcDataReader myread = mycommand.ExecuteReader();

                ypos = topMargin;
                while (myread.Read())
                {
                    line = Strings.Right("      " + myread["Producto"], 6) + " " + Strings.Left(myread["descripcion"] + "                   ", 19) + " "
                         + Strings.Right("   " + myread["Cant"], 3) + "  " + Strings.Right("       " + Strings.Format(myread["Total"], "###,###"), 7);
                    if (line == null) break;
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
            topMargin = ypos + 1600;
        }

        // ── BuscaDatosCierre ───────────────────────────────────────────────
        public void BuscaDatosCierre(string IdUsuario, int IdPunto, int Idturno, DateTime Fecing, OdbcConnection myConnect)
        {
            string Nomusu = "admin";
            string Nomres = " ", Nit = " ", Direccion = " ", Telefono = " ";
            double NumFactura = 0;
            string Resolucion = " ";
            DateTime FecResol = DateTime.MinValue;
            int RegimenIva = 0;
            string Prefijo = " ", NumInicial = " ", NumFinal = " ";
            int tipoMovto_cierre = 0;
            string ctrlfactura = "9999";

            {
                string _d1 = "", _d2 = "", _d3 = "", _d4 = "";
                int _i1 = 0;
                // this.MsgInvConf.BuscaPunto(IdPunto, Idturno, Fecing, myConnect, ref _d1, ref tipoMovto_cierre); // ERROR: CS7036
            }

            {
                string _d1 = "", _d2 = "", _d3 = "", _d4 = "", _d5 = "", _d6 = "", _d7 = "", _d8 = "", _d9 = "";
                string Descripcion = "";
                // MsgInvConf.BuscaTipomovto(tipoMovto_cierre, myConnect, ref Descripcion, ref _d1, ref _d2, ref _d3, ref _d4, ref _d5, ref _d6, ref _d7, ref _d8, ref _d9, ref ctrlfactura); // ERROR: CS7036
            }

            {
                double _dNumIni = 0, _dNumFin = 0;
                // MsgInvConf.BuscaDatosFacturacion(ctrlfactura, myConnect, ref Resolucion, ref FecResol, ref RegimenIva, ref Prefijo, ref _dNumIni, ref _dNumFin); // ERROR: CS7036
                NumInicial = _dNumIni.ToString();
                NumFinal = _dNumFin.ToString();
            }

            {
                string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "", _p9 = "", _p10 = "";
                string _p11 = "", _p12 = "", _p13 = "";
                // msgsys.BuscarCompania(varini.sptCodEmpr, myConnect, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                    // ref _p11, ref _p12, ref _p13, ref Nit, ref Direccion, ref Nomres, ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, // ERROR: CS7036
                    // ref _p1, ref Telefono); // ERROR: CS7036
            }

            // msgsys.BuscaUsuario(IdUsuario, myConnect, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref Nomusu); // ERROR: CS1501

            DatosTiket.Caja = IdPunto.ToString();
            DatosTiket.Direccion = Direccion;
            DatosTiket.Factura = NumFactura.ToString();
            DatosTiket.Fecha = Fecing;
            DatosTiket.Hora = Strings.Format(DateTime.Now, "hh:ss tt");
            DatosTiket.nit = Nit;
            DatosTiket.IdUsuario = IdUsuario;
            DatosTiket.NomRescomp = Nomres;
            DatosTiket.Telefono = Telefono;
            DatosTiket.Turno = Idturno.ToString();
            DatosTiket.Vendedor = Nomusu;
            DatosTiket.Resolucion = Resolucion;
            DatosTiket.FecResol = FecResol;
            DatosTiket.RegimenIva = RegimenIva;
            DatosTiket.NumInicial = NumInicial;
            DatosTiket.NumFinal = NumFinal;
            DatosTiket.Prefijo = Prefijo;
        }
    }
}
