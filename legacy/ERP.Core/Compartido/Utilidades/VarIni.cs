using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Compartido.Utilidades
{
    public static class VarIni
    {
        public static string pstEmpresa;
        public static bool membrete = false;
        private static OdbcCommand mycomqueryconec = new OdbcCommand();
        private static ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();

        public static void GrabaFormapago(string comprobante, double ConseComprobante, double efectivo,
            double Cantcheque, string banco, string NumeroCheque, string NumCuenta,
            double ValTarjDebito, string NumTarjDebito, double ValTarjCredito, string NumTarjCredito,
            double otros, string nro_otros, double Vlrtitulo, string Nrotitulo, string MotivoPago,
            OdbcConnection myconect)
        {
            bool ok = false;
            string mysql;
            DataSet datas = new DataSet();

            string _efectivo = "0";
            string _valorcheque = "0";
            string _banco = "0";
            string _numCheque = "0";
            string _numCuenta = "0";
            string _vlrDebito = "0";
            string _numTarjDebito = "0";
            string _vlrCredito = "0";
            string _numTarjCredito = "0";
            string _otros = "0";
            string _nro_otros = "0";
            string _vlrtitulo = "0";
            string _nrotitulo = "0";
            string _motivoPago = "0";

            ok = buscaFormapago(comprobante, ConseComprobante, myconect,
                ref _efectivo, ref _valorcheque, ref _banco, ref _numCheque, ref _numCuenta,
                ref _vlrDebito, ref _numTarjDebito, ref _vlrCredito, ref _numTarjCredito,
                ref _otros, ref _nro_otros, ref _vlrtitulo, ref _nrotitulo, ref _motivoPago, ref datas);

            switch (ok)
            {
                case false:
                    mysql = "insert into sys_forpago(compronte,numero_domto,efectivo,cheque,banco,nro_cheque,nro_cuenta,"
                          + "tdebito,nro_tdebito,tcredito,nro_tcredito, otros, nro_otros,VLRTITULO,NROTITULO,MotivoPago) "
                          + "values('" + comprobante + "','" + ConseComprobante + "','" + efectivo + "','"
                          + Cantcheque + "','" + banco + "','" + NumeroCheque + "','" + NumCuenta + "','"
                          + ValTarjDebito + "','" + NumTarjDebito + "','" + ValTarjCredito + "','" + NumTarjCredito + "','"
                          + otros + "','" + nro_otros + "'," + Vlrtitulo + ",'" + Nrotitulo + "','" + MotivoPago + "')";
                    break;
                case true:
                    mysql = "update sys_forpago set efectivo='" + efectivo + "', cheque='" + Cantcheque + "',tdebito='" + ValTarjDebito + "',"
                          + "tcredito='" + ValTarjCredito + "',banco='" + banco + "',nro_cheque='" + NumeroCheque + "',nro_cuenta='"
                          + NumCuenta + "',nro_tdebito='" + NumTarjDebito + "',nro_tcredito='" + NumTarjCredito + "',nro_otros='"
                          + nro_otros + "',otros='" + otros + "',VLRTITULO=" + Vlrtitulo + ",NROTITULO='" + Nrotitulo + "',MotivoPago='" + MotivoPago
                          + "' where compronte='" + comprobante + "' and numero_domto='" + ConseComprobante + "'";
                    break;
                default:
                    mysql = "";
                    break;
            }
            connect.ExecuteQueryconec(mysql, myconect, "GrabaFormapago");
        }

        public static void GrabaFormapagoCheque(string comprobante, double ConseComprobante, DataSet datas,
            OdbcConnection myconnect, string usuario)
        {
            int i = 0;
            StringBuilder Stbulider = new StringBuilder();
            string stmysql;
            stmysql = "DELETE FROM sys_forpago_Cheq WHERE compronte = '" + comprobante + "' AND numero_domto = " + ConseComprobante;
            connect.ExecuteQueryconec(stmysql, myconnect, "Grabar/ElimiarForPagCheq");

            while (i < datas.Tables["forpago_Cheq"].Rows.Count)
            {
                Stbulider.Append("insert into sys_forpago_Cheq(");
                Stbulider.Append("compronte, numero_domto, cheque ,nro_cheque ,nro_cuenta ,banco,usuario) ");
                Stbulider.Append("values ('");
                Stbulider.Append(comprobante + "',");
                Stbulider.Append(ConseComprobante + ",");
                Stbulider.Append(datas.Tables["forpago_Cheq"].Rows[i][0] + ",'");
                Stbulider.Append(datas.Tables["forpago_Cheq"].Rows[i][1] + "','");
                Stbulider.Append(datas.Tables["forpago_Cheq"].Rows[i][2] + "','");
                Stbulider.Append(datas.Tables["forpago_Cheq"].Rows[i][3] + "','");
                Stbulider.Append(usuario + "')");
                connect.ExecuteQueryconec(Stbulider.ToString(), myconnect, "GrabarForPagcheq");
                Stbulider.Replace(Stbulider.ToString(), "");
                i = i + 1;
            }
        }

        public static bool buscaFormapago(string comprobante, double ConseComprobante, OdbcConnection myconect,
            ref string efectivo, ref string Valorcheque, ref string banco, ref string NumCheque, ref string NumCuenta,
            ref string VlrDebito, ref string NumTarjDebito, ref string VlrCredito, ref string NumTarjCredito,
            ref string otros, ref string nro_otros, ref string Vlrtitulo, ref string Nrotitulo,
            ref string MotivoPago, ref DataSet datas)
        {
            string stmysql;
            bool ok;

            stmysql = "select efectivo as campo1, cheque as campo2,banco as campo3, nro_cheque as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            ok = connect.ExecuteQueryconec(stmysql, myconect, "buscaFormapago", ref efectivo, ref Valorcheque, ref banco, ref NumCheque);

            stmysql = "select nro_cuenta as campo1,tdebito as campo2,nro_tdebito as campo3,tcredito as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            connect.ExecuteQueryconec(stmysql, myconect, "buscaFormapago", ref NumCuenta, ref VlrDebito, ref NumTarjDebito, ref VlrCredito);

            stmysql = "select nro_tcredito as campo1,otros as campo2,nro_otros as campo3,VLRTITULO as campo4 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            connect.ExecuteQueryconec(stmysql, myconect, "buscaFormapago", ref NumTarjCredito, ref otros, ref nro_otros, ref Vlrtitulo);

            stmysql = "select Nrotitulo as campo1,MotivoPago as campo2 from sys_forpago where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            connect.ExecuteQueryconec(stmysql, myconect, "buscaFormapago", ref Nrotitulo, ref MotivoPago);

            // Query nueva tabla sys_forpago_cheq, "multiples cheques"
            stmysql = "select cheque ,nro_cheque ,nro_cuenta ,banco  from sys_forpago_Cheq where compronte = '" + comprobante + "' and numero_domto = " + ConseComprobante;
            connect.ExecuteQueryDataset(stmysql, myconect, "buscaFormapagoCheque", ref datas, "forpago_Cheq");

            return ok;
        }

        public static bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect,
            string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            string er = null;
            bool result = false;
            try
            {
                result = false;
                mycomqueryconec.CommandText = stMysql;
                mycomqueryconec.Connection = appadoConect;
                mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);

                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();
                if (Myread.RecordsAffected > 0)
                {
                    result = true;
                }
                while (Myread.Read())
                {
                    if (stMysql.Contains("campo1") == true)
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
                    if (stMysql.Contains("campo2") == true)
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
                    if (stMysql.Contains("campo3") == true)
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
                    if (stMysql.Contains("campo4") == true)
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
            catch
            {
                er = Information.Err().Description;
                result = false;
            }
            if (er != null)
            {
                throw new Exception(er + "\n" + " Procedimiento Origen : " + NombreProcedimiento + "\n" + "query :" + stMysql);
            }
            return result;
        }

        public static bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect,
            string NombreProcedimiento, ref DataSet DsDataset, string NombreTabla)
        {
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandTimeout = 800;
            mycomqueryconec.ExecuteNonQuery();

            try
            {
                Myread.SelectCommand = mycomqueryconec;
                Myread.Fill(DsDataset, NombreTabla);
                return true;
            }
            catch (Exception ex)
            {
                DsDataset.Tables.Add(NombreTabla);
                return false;
            }
        }
    }

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
            // Create points that define line.
            Point point1 = new Point(15, 80);
            Point point2 = new Point(800, 80);
            Point point3 = new Point(15, 100);
            Point point4 = new Point(800, 100);

            // Calculate the number of lines per page.
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

            // Print each line of the file.
            while (count < linesPerPage)
            {
                line = streamToPrint.ReadLine();
                if (line == null)
                {
                    break;
                }
                yPos = topMargin + count * printFont.GetHeight(ev.Graphics);
                ev.Graphics.DrawString(line + "\n\r", printFont, Brushes.Black, leftMargin, yPos, new StringFormat());
                count += 1;
            }

            // If more lines exist, print another page.
            if (line != null)
            {
                ev.HasMorePages = true;
            }
            else
            {
                ev.HasMorePages = false;
            }

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
