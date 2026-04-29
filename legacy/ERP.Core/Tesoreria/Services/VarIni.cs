using System;
using System.IO;
using System.Data.Odbc;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Tesoreria.Services
{
    // Traducción de: Module VarIni (VarIni.vb)
    public static class VarIni
    {
        public static string pstEmpresa;
        public static bool membrete = false;
        private static OdbcCommand mycomqueryconec = new OdbcCommand();
        private static ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();

        public static void GrabaFormapago(string comprobante, double ConseComprobante, double efectivo,
            int Cantcheque, string banco, string NumeroCheque, string NumCuenta,
            double ValTarjDebito, string NumTarjDebito, double ValTarjCredito, string NumTarjCredito,
            double otros, string nro_otros, OdbcConnection myconect)
        {
            bool ok = buscaFormapago(comprobante, ConseComprobante, myconect);
            string mysql;
            if (!ok)
            {
                mysql = "insert into sys_forpago(compronte,numero_domto,efectivo,cheque,banco,nro_cheque,nro_cuenta,tdebito,nro_tdebito,tcredito,nro_tcredito, otros, nro_otros) "
                      + "values('" + comprobante + "','" + ConseComprobante + "','" + efectivo + "','"
                      + Cantcheque + "','" + banco + "','" + NumeroCheque + "','" + NumCuenta + "','"
                      + ValTarjDebito + "','" + NumTarjDebito + "','" + ValTarjCredito + "','" + NumTarjCredito + "','"
                      + otros + "','" + nro_otros + "')";
            }
            else
            {
                mysql = "update sys_forpago set efectivo='" + efectivo + "', cheque='" + Cantcheque + "',tdebito='" + ValTarjDebito + "',"
                      + "tcredito='" + ValTarjCredito + "',banco='" + banco + "',nro_cheque='" + NumeroCheque + "',nro_cuenta='"
                      + NumCuenta + "',nro_tdebito='" + NumTarjDebito + "',nro_tcredito='" + NumTarjCredito + "',nro_otros='"
                      + nro_otros + "',otros='" + otros + "' where compronte='" + comprobante + "' and numero_domto='" + ConseComprobante + "'";
            }
            string _c1 = "", _c2 = "", _c3 = "", _c4 = "";
            connect.ExecuteQueryconec(mysql, myconect, "GrabaFormapago", ref _c1, ref _c2, ref _c3, ref _c4);
        }

        public static bool buscaFormapago(string comprobante, double ConseComprobante, OdbcConnection myconect)
        {
            string efectivo = "0", Valorcheque = "0", banco = "0", NumCheque = "0";
            string NumCuenta = "0", VlrDebito = "0", NumTarjDebito = "0", VlrCredito = "0";
            string NumTarjCredito = "0", otros = "0", nro_otros = "0";
            return buscaFormapago(comprobante, ConseComprobante, myconect,
                ref efectivo, ref Valorcheque, ref banco, ref NumCheque,
                ref NumCuenta, ref VlrDebito, ref NumTarjDebito, ref VlrCredito,
                ref NumTarjCredito, ref otros, ref nro_otros);
        }

        public static bool buscaFormapago(string comprobante, double ConseComprobante, OdbcConnection myconect,
            ref string efectivo, ref string Valorcheque, ref string banco, ref string NumCheque,
            ref string NumCuenta, ref string VlrDebito, ref string NumTarjDebito, ref string VlrCredito,
            ref string NumTarjCredito, ref string otros, ref string nro_otros)
        {
            string stmysql;
            stmysql = "select efectivo as campo1, cheque as campo2,banco as campo3, nro_cheque as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            bool ok = connect.ExecuteQueryconec(stmysql, myconect, "buscaFormapago", ref efectivo, ref Valorcheque, ref banco, ref NumCheque);

            stmysql = "select nro_cuenta as campo1,tdebito as campo2,nro_tdebito as campo3,tcredito as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            connect.ExecuteQueryconec(stmysql, myconect, "buscaFormapago", ref NumCuenta, ref VlrDebito, ref NumTarjDebito, ref VlrCredito);

            stmysql = "select nro_tcredito as campo1,otros as campo2,nro_otros as campo3 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            string _c4 = "";
            connect.ExecuteQueryconec(stmysql, myconect, "buscaFormapago", ref NumTarjCredito, ref otros, ref nro_otros, ref _c4);
            return ok;
        }

        public static bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            string er = null;
            bool result = false;
            try
            {
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0)
                    result = true;
                while (Myread.Read())
                {
                    if (stMysql.Contains("campo1"))
                    {
                        if (Myread["campo1"] == DBNull.Value)
                            Campo1 = "0";
                        else
                            Campo1 = Myread["campo1"].ToString();
                    }
                    if (stMysql.Contains("campo2"))
                    {
                        if (Myread["campo2"] == DBNull.Value)
                            Campo2 = "0";
                        else
                            Campo2 = Myread["campo2"].ToString();
                    }
                    if (stMysql.Contains("campo3"))
                    {
                        if (Myread["campo3"] == DBNull.Value)
                            Campo3 = "0";
                        else
                            Campo3 = Myread["campo3"].ToString();
                    }
                    if (stMysql.Contains("campo4"))
                    {
                        if (Myread["campo4"] == DBNull.Value)
                            Campo4 = "0";
                        else
                            Campo4 = Myread["campo4"].ToString();
                    }
                    result = true;
                }
                Myread.Close();
            }
            catch (Exception ex)
            {
                er = ex.Message;
                result = false;
            }
            if (er != null)
                throw new Exception(er + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql);
            return result;
        }
    }

    // Traducción de: Public Class Clsmsgsas (VarIni.vb)
    public class Clsmsgsas
    {
        public StreamReader streamToPrint;

        public struct RegParametros
        {
            public string TituloInforme;
            public string Titulos;
            public int TamLetra;
        }

        public RegParametros Validar;

        public Graphics prin2(PrintPageEventArgs g)
        {
            Font fr = new Font("Arial", 14);
            return null;
        }

        public Graphics prin(PrintPageEventArgs ev)
        {
            float linesPerPage = 0;
            float yPos = 0;
            int count = 0;
            float leftMargin = 15;
            float topMargin = 15;
            Font printFont = new Font("Courier New", Validar.TamLetra, FontStyle.Regular);
            Font printTitt = new Font("Times New Roman", 16, FontStyle.Bold);
            Font printTitulos = new Font("Times New Roman", 10, FontStyle.Bold);
            string line = null;
            Pen blackPen = new Pen(Color.Black, 3);
            Point point1 = new Point(15, 80);
            Point point2 = new Point(800, 80);
            Point point3 = new Point(15, 100);
            Point point4 = new Point(800, 100);

            linesPerPage = ev.MarginBounds.Height / printFont.GetHeight(ev.Graphics);

            line = Validar.TituloInforme;
            ev.Graphics.DrawString(line, printTitt, Brushes.Black, 15, topMargin, new StringFormat());
            topMargin = topMargin + 23;

            line = "Fecha y Hora :  " + DateTime.Now;
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 15, topMargin, new StringFormat());
            topMargin = topMargin + 15;

            line = VarIni.pstEmpresa;
            ev.Graphics.DrawString(line, printTitt, Brushes.Black, 90, topMargin, new StringFormat());

            topMargin = topMargin + 8;
            line = "Empresa :  ";
            ev.Graphics.DrawString(line, printFont, Brushes.Black, 15, topMargin, new StringFormat());

            topMargin = topMargin + 23;
            ev.Graphics.DrawLine(blackPen, point1, point2);
            topMargin = topMargin + 1;

            line = Validar.Titulos;
            ev.Graphics.DrawString(line, printTitulos, Brushes.Black, 15, topMargin, new StringFormat());

            ev.Graphics.DrawLine(blackPen, point3, point4);
            topMargin = topMargin + 23;

            while (count < linesPerPage)
            {
                line = streamToPrint.ReadLine();
                if (line == null)
                    break;
                yPos = topMargin + count * printFont.GetHeight(ev.Graphics);
                ev.Graphics.DrawString(line + "\n\r", printFont, Brushes.Black, leftMargin, yPos, new StringFormat());
                count++;
            }

            if (line != null)
                ev.HasMorePages = true;
            else
                ev.HasMorePages = false;

            return null;
        }

        public void ImprimirTexto(string Nombre, PrintPreviewDialog PrintPreview, PrintDocument PrintDoc)
        {
            streamToPrint = new StreamReader(Nombre);
            PrintPreview.Document = PrintDoc;
            PrintPreview.WindowState = FormWindowState.Maximized;
            PrintPreview.ShowDialog();
            streamToPrint.Close();
        }
    }
}
