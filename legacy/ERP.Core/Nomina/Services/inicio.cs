using System;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using ERP.Core.CarteraFinanciera.Models;
#if OFFICE_INTEROP
using Microsoft.Office.Interop;
using Excel = Microsoft.Office.Interop.Excel;
#endif
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;

namespace ERP.Core.Nomina.Services
{
    public static class vari
    {
        public static string constring;
        public static string Agencia;
        public static string Empresa;
        public static string Cencosto;
        public static string peri;
        public static string num;
        public static string periodicidad;
        public static string dire;
        public static string nombre;
        public static string stcodigo, stvalor, adisional, comcep;
        public static int PerNomina = 999999;
        public static string stwhere, stwhere2;
        public static string AgenciaUp;
        public static string EmpresaUp;
        public static string CencostoUp;
        public static string WhereAgencia2;
        public static string WhereEmpresa2;
        public static string WhereCencosto2;
        public static string FechaIncial, FechaFinal, NitOCodigo;
    }

    public class inicio
    {
        public enum Accion : int
        {
            Generar = 1,
            Cargar = 2
        }

        private ParamCop Config = new ParamCop();
        private ParamCop Consis = new ParamCop();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera cop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private OdbcConnection conect = new OdbcConnection();
        private OdbcConnection conect1 = new OdbcConnection();
#if OFFICE_INTEROP
        private Excel.Application m_Excel;
#endif
        private OdbcCommand mycomqueryconec = new OdbcCommand();

        public object run(string con, string Agenci, string Empre, string Cencos, string perio,
        string nume, string percidad, string direc, string nomb, int tipo)
        {
            vari.Agencia = Agenci;
            vari.Empresa = Empre;
            vari.Cencosto = Cencos;
            vari.peri = perio;
            vari.num = nume;
            vari.periodicidad = percidad;
            vari.dire = direc;
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            int CantiDescu = 0;

            if (vari.Agencia == "Todos")
            {
                WhereAgencia = "";
                vari.AgenciaUp = "";
                vari.WhereAgencia2 = "";
            }
            else
            {
                WhereAgencia = "  and a.Agencia = '" + ("0000" + vari.Agencia).Substring(("0000" + vari.Agencia).Length - 4) + "' ";
                vari.AgenciaUp = ("0000" + vari.Agencia).Substring(("0000" + vari.Agencia).Length - 4);
                vari.WhereAgencia2 = " and Agencia = '" + ("0000" + vari.Agencia).Substring(("0000" + vari.Agencia).Length - 4) + "' ";
            }
            if (vari.Empresa == "Todos")
            {
                WhereEmpresa = "";
                vari.EmpresaUp = "";
                vari.WhereEmpresa2 = "";
            }
            else
            {
                WhereEmpresa = " and a.Empresa = '" + ("0000" + vari.Empresa).Substring(("0000" + vari.Empresa).Length - 4) + "' ";
                vari.EmpresaUp = ("0000" + vari.Empresa).Substring(("0000" + vari.Empresa).Length - 4);
                vari.WhereEmpresa2 = " and Empresa = '" + ("0000" + vari.Empresa).Substring(("0000" + vari.Empresa).Length - 4) + "' ";
            }
            if (vari.Cencosto == "Todos")
            {
                WhereCencosto = "";
                vari.CencostoUp = "";
                vari.WhereCencosto2 = "";
            }
            else
            {
                WhereCencosto = " and a.cencosto = '" + ("00000000" + vari.Cencosto).Substring(("00000000" + vari.Cencosto).Length - 8) + "' ";
                vari.CencostoUp = ("00000000" + vari.Cencosto).Substring(("00000000" + vari.Cencosto).Length - 8);
                vari.WhereCencosto2 = " and cencosto = '" + ("00000000" + vari.Cencosto).Substring(("00000000" + vari.Cencosto).Length - 8) + "' ";
            }

            vari.stwhere = WhereAgencia + WhereEmpresa + WhereCencosto;
            vari.stwhere2 = vari.WhereAgencia2 + vari.WhereEmpresa2 + vari.WhereCencosto2;

            vari.constring = con;

            conect.ConnectionString = vari.constring;
            conect.Open();
            conect1.ConnectionString = vari.constring;
            conect1.Open();

            // cop.buscaPeriodo("copn", conect, ref vari.PerNomina, DateTime.Now.ToString("yyyy")); // ERROR: CS1501

            if (tipo == 1)
            {
                vari.nombre = Interaction.InputBox("Digite el nombre del archivo.", "SOLIDO", "");
                if (vari.nombre == "")
                {
                    vari.nombre = nomb;
                }
                generar();
            }
            else
            {
                leer();
            }

            return null;
        }

        public void generar()
        {
            string formato = "";

            string mysql1 = "select fec_ini, fec_fin from cop_nompla where periodo = '" + vari.peri + "' " + vari.stwhere2 + " and adicional ='" + vari.num + "' and periodicidad = '" + vari.periodicidad + "'";

            OdbcCommand myCommandPla = new OdbcCommand(mysql1, conect);
            OdbcDataReader myReaderpla = myCommandPla.ExecuteReader();
            while (myReaderpla.Read())
            {
                DateTime fecFin = Convert.ToDateTime(myReaderpla["fec_fin"]);
                vari.FechaIncial = fecFin.ToString("yy") + fecFin.ToString("MM") + fecFin.ToString("dd");
                vari.FechaFinal = fecFin.ToString("yyyy") + fecFin.ToString("MM") + fecFin.ToString("dd");
            }
            myReaderpla.Close();

            // Config.BuscaEmpresa(vari.Empresa, conect, ref formato); // ERROR: CS1620

            switch (formato.Trim())
            {
                case "EST_TXT_NIT":
                    FormatoEstandar(1, ".txt");
                    break;
                case "EST_CSV_NIT":
                    FormatoEstandar(2, ".csv");
                    break;
                case "EST_XLS_NIT":
                    // FormatoExcel(1); // ERROR: CS0103
                    break;
                case "EST_TXT_COD":
                    FormatoEstandar(3, ".txt");
                    break;
                case "EST_CSV_COD":
                    FormatoEstandar(4, ".csv");
                    break;
                case "EST_XLS_COD":
                    // FormatoExcel(2); // ERROR: CS0103
                    break;
                case "NM-UNO5":
                    FormatoNmUno5();
                    break;
                case "NM-UNO8.5C":
                    FormatoNmUno85C();
                    break;
                case "PRODUFEN":
                    Produfen();
                    break;
                default:
                    FormatoEstandar(5, ".txt");
                    break;
            }
        }

#if OFFICE_INTEROP
        public bool FormatoExcel(int opcion)
        {
            Excel.Workbook objLibroExcel;
            Excel.Worksheet objHojaExcel;
            m_Excel = new Excel.Application();
            m_Excel.Visible = false;
            bool si1 = false;
            objLibroExcel = m_Excel.Workbooks.Add();
            objHojaExcel = (Excel.Worksheet)objLibroExcel.Worksheets[1];
            objHojaExcel.PageSetup.Orientation = Excel.XlPageOrientation.xlPortrait;
            objHojaExcel.Visible = Excel.XlSheetVisibility.xlSheetVisible;
            objHojaExcel.Activate();
            int fila = 2;
            objHojaExcel.Name = vari.nombre.Replace(".txt", "");

            switch (opcion)
            {
                case 1:
                    objHojaExcel.Range["A1"].Value = "CEDULA";
                    break;
                case 2:
                    objHojaExcel.Range["A1"].Value = "CODIGO_INTERNO";
                    break;
            }
            objHojaExcel.Range["B1"].Value = "CONCEPTO_DE_DESCUENTO";
            objHojaExcel.Range["C1"].Value = "VALOR_DESCOTADO";

            string mysql1 = "select  b.copto_nomina,c.codigo_empresa,c.nit,sum(a.vlr_aportes + a.vlr_prestamos + a.vlr_interes + a.vlr_extras + a.vlr_seguro + a.vlr_mora + a.vlr_admon + a.vlr_otras) as total " +
                           " from cop_nomdes  a left join cop_nomconce b on" +
                           " b.empresa = case  when '" + vari.EmpresaUp + "'  = '' then b.empresa else a.empresa end and " +
                           " b.cencosto = case  when '" + vari.CencostoUp + "'  = '' then b.cencosto else a.cencosto end and " +
                           " b.agencia = case  when '" + vari.AgenciaUp + "'  = '' then b.agencia else a.agencia end inner join sys_maenit c on c.codigoter = a.codigoter" +
                            " inner join cop_maecar d on d.codigoter = a.codigoter and d.lincred = a.lincred and d.numero = a.numero " +
                            " where a.periodo = '" + vari.peri + "' " + vari.stwhere + " and a.adicional ='" + vari.num +
                            "' and a.periodicidad = '" + vari.periodicidad + "' and a.lincred = b.lincred group by b.copto_nomina,c.codigo_empresa,c.nit order by c.nit,total";

            try
            {
                OdbcCommand myCommand2 = new OdbcCommand(mysql1, conect);
                OdbcDataReader myReader2 = myCommand2.ExecuteReader();

                int conce = 1;
                while (myReader2.Read())
                {
                    if (Convert.ToDouble(myReader2["total"]) != 0)
                    {
                        objHojaExcel.Range["A" + fila].Value = opcion == 1 ? myReader2["NIT"].ToString() : myReader2["CODIGO_EMPRESA"].ToString();
                        objHojaExcel.Range["B" + fila].Value = myReader2["copto_nomina"].ToString();
                        objHojaExcel.Range["C" + fila].Value = Convert.ToInt32(myReader2["total"]).ToString();
                        fila += 1;
                    }
                    si1 = true;
                    conce += 1;
                }
                myReader2.Close();
                m_Excel.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return si1;
        }
#endif

        public bool FormatoEstandar(int opcion, string extension)
        {
            DateTime fecha = DateTime.Now;
            string dia = DateTime.Now.Day.ToString();
            string mes = DateTime.Now.Month.ToString();

            string mysql1 = "select  b.copto_nomina,c.codigo_empresa,c.nit,sum(a.vlr_aportes + a.vlr_prestamos + a.vlr_interes + a.vlr_extras + a.vlr_seguro + a.vlr_mora + a.vlr_admon + a.vlr_otras) as total " +
                           " from cop_nomdes  a left join cop_nomconce b on" +
                           " b.empresa = case  when '" + vari.EmpresaUp + "'  = '' then b.empresa else a.empresa end and " +
                           " b.cencosto = case  when '" + vari.CencostoUp + "'  = '' then b.cencosto else a.cencosto end and " +
                           " b.agencia = case  when '" + vari.AgenciaUp + "'  = '' then b.agencia else a.agencia end inner join sys_maenit c on c.codigoter = a.codigoter" +
                            " inner join cop_maecar d on d.codigoter = a.codigoter and d.lincred = a.lincred and d.numero = a.numero " +
                            " where a.periodo = '" + vari.peri + "' " + vari.stwhere + " and a.adicional ='" + vari.num +
                            "' and a.periodicidad = '" + vari.periodicidad + "' and a.lincred = b.lincred group by b.copto_nomina,c.codigo_empresa,c.nit order by c.nit,total";

            OdbcConnection pmyConec03 = new OdbcConnection();
            pmyConec03.Close();
            pmyConec03.ConnectionString = vari.constring;

            OdbcCommand myCommand2 = new OdbcCommand(mysql1, pmyConec03);
            myCommand2.Connection.Open();
            OdbcDataReader myReader2 = myCommand2.ExecuteReader();
            TextWriter read11 = File.CreateText(Application.StartupPath + "\\archivos planos\\export\\" + vari.nombre + extension);
            bool si1 = false;
            int conce = 1;
            while (myReader2.Read())
            {
                switch (opcion)
                {
                    case 1:
                    case 2:
                        read11.Write(myReader2["nit"].ToString() + "," + myReader2["copto_nomina"].ToString() + "," + Convert.ToInt32(myReader2["total"]).ToString());
                        read11.WriteLine();
                        si1 = true;
                        conce += 1;
                        break;
                    case 3:
                    case 4:
                        read11.Write(myReader2["codigo_empresa"].ToString() + "," + myReader2["copto_nomina"].ToString() + "," + Convert.ToInt32(myReader2["total"]).ToString());
                        read11.WriteLine();
                        si1 = true;
                        conce += 1;
                        break;
                    default:
                        read11.Write(myReader2["nit"].ToString() + "," + myReader2["copto_nomina"].ToString() + "," + Convert.ToInt32(myReader2["total"]).ToString());
                        read11.WriteLine();
                        si1 = true;
                        conce += 1;
                        break;
                }
            }
            read11.Close();
            pmyConec03.Close();
            myCommand2.Connection.Close();
            return si1;
        }

        public void leer()
        {
            string formato = "";
            // Config.BuscaEmpresa(vari.Empresa, conect, ref formato); // ERROR: CS1620

            switch (formato.Trim())
            {
                case "EST_TXT_NIT":
                case "EST_CSV_NIT":
                case "EST_XLS_NIT":
                    leerOtras(1);
                    break;
                case "EST_TXT_COD":
                case "EST_CSV_COD":
                case "EST_XLS_COD":
                    leerOtras(2);
                    break;
                case "NM-UNO8.5C":
                    leerNmUno85C();
                    break;
                case "PRODUFEN":
                    Produfen_envio();
                    break;
                default:
                    leerOtras(1);
                    break;
            }
        }

        public void leerOtras(int opcion)
        {
            // num_solicitud progreso = new num_solicitud(); // ERROR: CS0246
            double numeroLineas = 0;
            int f = 0;
            try
            {
                string mysql;
                string TextLine, Nombre = "", Tipo = "";

                if (vari.Agencia == "Todos")
                {
                    vari.AgenciaUp = "";
                }
                else
                {
                    vari.AgenciaUp = ("0000" + vari.Agencia).Substring(("0000" + vari.Agencia).Length - 4);
                }
                if (vari.Empresa == "Todos")
                {
                    vari.EmpresaUp = "";
                }
                else
                {
                    vari.EmpresaUp = ("0000" + vari.Empresa).Substring(("0000" + vari.Empresa).Length - 4);
                }
                if (vari.Cencosto == "Todos")
                {
                    vari.CencostoUp = "";
                }
                else
                {
                    vari.CencostoUp = ("00000000" + vari.Cencosto).Substring(("00000000" + vari.Cencosto).Length - 8);
                }

                // progreso.texto.Text = "GENERANDO ARCHIVO."; // ERROR: CS0103
                // progreso.numero.Text = "Espere por favor..."; // ERROR: CS0103
                // progreso.Button1.Visible = false; // ERROR: CS0103
                // progreso.ControlBox = false; // ERROR: CS0103
                // progreso.Pro.Visible = true; // ERROR: CS0103
                // progreso.Text = ""; // ERROR: CS0103

                int len = (int)new FileInfo(vari.dire).Length;

                // f = FileSystem.FreeFile(); // ERROR: CS0104
                // FileSystem.FileOpen(f, vari.dire, OpenMode.Input); // ERROR: CS0104
                // progreso.Pro.Value = 0; // ERROR: CS0103
                // progreso.Show(); // ERROR: CS0103
                mysql = "delete from cop_valdesc  where " +
                               " periodo = '" + vari.peri.Trim() +
                               "' and periodicidad = '" + vari.periodicidad +
                               "' and adicional = '" + vari.num + "' " + vari.stwhere.Replace("a.", "");
                coneccion_1(mysql);

                int c = 0;

                // while (!FileSystem.EOF(f)) // ERROR: CS0104
                {
                    vari.stcodigo = "";
                    vari.comcep = "";
                    vari.stvalor = "";
                    // FileSystem.Input(f, ref vari.stcodigo); // ERROR: CS0104
                    // FileSystem.Input(f, ref vari.comcep); // ERROR: CS0104
                    // FileSystem.Input(f, ref vari.stvalor); // ERROR: CS0104

                    switch (opcion)
                    {
                        case 1:
                            mysql = "select codigoter from sys_maenit where codigoter='" + ("00000000000000" + vari.stcodigo).Substring(("00000000000000" + vari.stcodigo).Length - 14) + "'";
                            break;
                        case 2:
                            mysql = "select codigoter from sys_maenit where codigo_empresa='" + vari.stcodigo + "'";
                            break;
                    }

                    numeroLineas += 1;
                    TextLine = vari.stcodigo + vari.stvalor + vari.comcep;
                    if (c == 0)
                    {
                        // progreso.Pro.Maximum = (int)(len / TextLine.Length); // ERROR: CS0103
                        c += 1;
                    }

                    OdbcConnection pmyConec04 = new OdbcConnection();
                    pmyConec04.ConnectionString = vari.constring;
                    OdbcCommand myCommand71 = new OdbcCommand(mysql, pmyConec04);
                    myCommand71.Connection.Open();
                    OdbcDataReader myReader71 = myCommand71.ExecuteReader();

                    if (myReader71.Read())
                    {
                        vari.stcodigo = myReader71["codigoter"].ToString();
                    }
                    else
                    {
                        MessageBox.Show("No se pudo encontrar el asociado con cedula: " + Convert.ToDouble(vari.stcodigo).ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        vari.stcodigo = ("000000000000000" + vari.stcodigo).Substring(("000000000000000" + vari.stcodigo).Length - 14);
                    }

                    myCommand71.Connection.Close();
                    pmyConec04.Close();

                    mysql = "select * from cop_valdesc a where " +
                    " periodo = '" + vari.peri.Trim() +
                    "' and periodicidad = '" + vari.periodicidad +
                    "' and codigoter = '" + vari.stcodigo +
                     "' and comcep = '" + vari.comcep +
                     "' and adicional = '" + vari.num + "' " + vari.stwhere;

                    OdbcConnection pmyConec05 = new OdbcConnection();
                    pmyConec05.ConnectionString = vari.constring;
                    OdbcCommand myCommand72 = new OdbcCommand(mysql, pmyConec05);
                    myCommand72.Connection.Open();

                    OdbcDataReader myReader72 = myCommand72.ExecuteReader();

                    if (myReader72.Read())
                    {
                        mysql = "update cop_valdesc set valor = '" + (Convert.ToDouble(myReader72["valor"]) + Convert.ToDouble(vari.stvalor)) +
                                              "' where  periodo = '" + vari.peri +
                                              "' and periodicidad = '" + vari.periodicidad + "' and codigoter = '" + vari.stcodigo.Trim() +
                                              "'  and comcep = '" + vari.comcep + "' and adicional = '" + vari.num + "' " +
                                              vari.stwhere.Replace("a.", "");
                    }
                    else
                    {
                        mysql = "insert into cop_valdesc (empresa,agencia,cencosto,periodo,periodicidad,codigoter,valor,adicional,comcep) values ('" +
                                               vari.EmpresaUp + "','" + vari.AgenciaUp + "','" + vari.CencostoUp + "','" + vari.peri.Trim() + "','" +
                                               vari.periodicidad + "','" + vari.stcodigo.Trim() + "','" +
                                              Convert.ToDouble(vari.stvalor) + "','" + vari.num + "','" + vari.comcep + "')";
                    }
                    mysql = mysql.Replace("''", "' '");
                    coneccion_1(mysql);
                    myCommand72.Connection.Close();
                    pmyConec05.Close();

                    // progreso.texto.Text = "GENERANDO ARCHIVO."; // ERROR: CS0103
                    // progreso.numero.Text = "Espere por favor..."; // ERROR: CS0103
                    // progreso.Pro.Value += 1; // ERROR: CS0103

                    // Debug.WriteLine(FileSystem.Seek(f)); // ERROR: CS0104
                }
                // FileSystem.FileClose(f); // ERROR: CS0104
                // progreso.Close(); // ERROR: CS0103
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString() + " Error en la Linea " + numeroLineas + "   Nombre" + vari.nombre, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // FileSystem.FileClose(f); // ERROR: CS0104
                // progreso.Close(); // ERROR: CS0103
            }
        }

        public void coneccion_1(string stMysql)
        {
            try
            {
                OdbcCommand mycommand = new OdbcCommand(stMysql, conect1);
                mycommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString() + " " + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref string Campo1, ref string Campo2, ref string Campo3, ref string Campo4)
        {
            bool result = false;
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandTimeout = 400;
            mycomqueryconec.CommandText = mycomqueryconec.CommandText.Replace("''", "' '");

            try
            {
                OdbcDataReader Myread = mycomqueryconec.ExecuteReader();

                if (Myread.RecordsAffected > 0)
                {
                    result = true;
                }

                if (Myread.Read())
                {
                    if (!string.IsNullOrEmpty(Campo1))
                    {
                        if (Myread["campo1"] == DBNull.Value)
                        {
                            Campo1 = "0";
                        }
                        else
                        {
                            Campo1 = Myread["campo1"].ToString().Trim();
                        }
                    }
                    if (!string.IsNullOrEmpty(Campo2))
                    {
                        if (Myread["campo2"] == DBNull.Value)
                        {
                            Campo2 = "0";
                        }
                        else
                        {
                            Campo2 = Myread["campo2"].ToString().Trim();
                        }
                    }
                    if (!string.IsNullOrEmpty(Campo3))
                    {
                        if (Myread["campo3"] == DBNull.Value)
                        {
                            Campo3 = "0";
                        }
                        else
                        {
                            Campo3 = Myread["campo3"].ToString().Trim();
                        }
                    }
                    if (!string.IsNullOrEmpty(Campo4))
                    {
                        if (Myread["campo4"] == DBNull.Value)
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
            catch (Exception)
            {
                result = false;
                MessageBox.Show("Error\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return result;
        }

        public bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref DataSet DsDataset, string NombreTabla)
        {
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = mycomqueryconec.CommandText.Replace("''", "' '");
            mycomqueryconec.ExecuteNonQuery();

            Myread.SelectCommand = mycomqueryconec;
            Myread.Fill(DsDataset, NombreTabla);

            if (DsDataset.Tables[NombreTabla].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool FormatoNmUno5()
        {
            DateTime fecha = DateTime.Now;
            string dia = DateTime.Now.Day.ToString();
            string mes = DateTime.Now.Month.ToString();
            int Contador = 0;
            string CadenaComcepto = "";
            DataSet dsComcep = new DataSet();
            string ComcepEscogido = "", Wherecomcep = "";
            bool ComcepValido = false;

            string mysql1 = "select  b.copto_nomina,c.codigo_empresa,c.nit,sum(a.vlr_aportes + a.vlr_prestamos + a.vlr_interes + a.vlr_extras + a.vlr_seguro + a.vlr_mora + a.vlr_admon + a.vlr_otras) as total " +
                                       " from cop_nomdes  a left join cop_nomconce b on" +
                                       " b.empresa = case  when '" + vari.EmpresaUp + "'  = '' then b.empresa else a.empresa end and " +
                                       " b.cencosto = case  when '" + vari.CencostoUp + "'  = '' then b.cencosto else a.cencosto end and " +
                                       " b.agencia = case  when '" + vari.AgenciaUp + "'  = '' then b.agencia else a.agencia end inner join sys_maenit c on c.codigoter = a.codigoter" +
                                        " inner join cop_maecar d on d.codigoter = a.codigoter and d.lincred = a.lincred and d.numero = a.numero " +
                                        " where a.periodo = '" + vari.peri + "' " + vari.stwhere + " and a.adicional ='" + vari.num +
                                        "'and a.periodicidad = '" + vari.periodicidad + "'and a.lincred = b.lincred group by b.copto_nomina,c.codigo_empresa,c.nit order by c.nit,total";

            OdbcConnection pmyConec03 = new OdbcConnection();
            pmyConec03.Close();
            pmyConec03.ConnectionString = vari.constring;

            OdbcCommand myCommand2 = new OdbcCommand(mysql1, pmyConec03);
            myCommand2.Connection.Open();
            OdbcDataReader myReader2 = myCommand2.ExecuteReader();
            TextWriter read11 = File.CreateText(Application.StartupPath + "\\archivos planos\\export\\" + vari.nombre + ".dat");
            bool si1 = false;
            int conce = 1;
            while (myReader2.Read())
            {
                read11.Write((myReader2["nit"].ToString() + new string(' ', 9)).Substring(0, 9));
                read11.Write(("000" + myReader2["copto_nomina"].ToString()).Substring(("000" + myReader2["copto_nomina"].ToString()).Length - 3) + new string(' ', 12) + vari.FechaIncial);
                read11.Write(new string(' ', 12) + "000" + new string(' ', 16) + "000000+" + ("00000000000" + Convert.ToInt32(myReader2["total"]).ToString()).Substring(("00000000000" + Convert.ToInt32(myReader2["total"]).ToString()).Length - 11) + "00+");
                read11.Write("0000000+000" + new string(' ', 6));
                read11.WriteLine();
                si1 = true;
                conce += 1;
            }
            read11.Close();
            pmyConec03.Close();
            myCommand2.Connection.Close();
            return si1;
        }

        public bool FormatoNmUno85C()
        {
            DateTime fecha = DateTime.Now;
            string dia = DateTime.Now.Day.ToString();
            string mes = DateTime.Now.Month.ToString();
            int Contador = 0;
            string CadenaComcepto = "";
            DataSet dsComcep = new DataSet();
            string ComcepEscogido = "", Wherecomcep = "";
            bool ComcepValido = false;

            string mysql1 = "select  b.copto_nomina,c.codigo_empresa,c.nit,sum(a.vlr_aportes + a.vlr_prestamos + a.vlr_interes + a.vlr_extras + a.vlr_seguro + a.vlr_mora + a.vlr_admon + a.vlr_otras) as total " +
                                           " from cop_nomdes  a left join cop_nomconce b on" +
                                           " b.empresa = case  when '" + vari.EmpresaUp + "'  = '' then b.empresa else a.empresa end and " +
                                           " b.cencosto = case  when '" + vari.CencostoUp + "'  = '' then b.cencosto else a.cencosto end and " +
                                           " b.agencia = case  when '" + vari.AgenciaUp + "'  = '' then b.agencia else a.agencia end inner join sys_maenit c on c.codigoter = a.codigoter" +
                                            " inner join cop_maecar d on d.codigoter = a.codigoter and d.lincred = a.lincred and d.numero = a.numero " +
                                            " where a.periodo = '" + vari.peri + "' " + vari.stwhere + " and a.adicional ='" + vari.num +
                                            "'and a.periodicidad = '" + vari.periodicidad + "'and a.lincred = b.lincred group by b.copto_nomina,c.codigo_empresa,c.nit order by c.nit,total";

            OdbcConnection pmyConec03 = new OdbcConnection();
            pmyConec03.Close();
            pmyConec03.ConnectionString = vari.constring;

            OdbcCommand myCommand2 = new OdbcCommand(mysql1, pmyConec03);
            myCommand2.Connection.Open();
            OdbcDataReader myReader2 = myCommand2.ExecuteReader();
            TextWriter read11 = File.CreateText(Application.StartupPath + "\\archivos planos\\export\\" + vari.nombre + ".dat");
            bool si1 = false;
            int conce = 1;
            while (myReader2.Read())
            {
                read11.Write((myReader2["nit"].ToString() + new string(' ', 8)).Substring(0, 13));
                read11.Write("00" + ("000" + myReader2["copto_nomina"].ToString()).Substring(("000" + myReader2["copto_nomina"].ToString()).Length - 3) + new string(' ', 11) + vari.FechaFinal);
                read11.Write(new string(' ', 16) + "000" + new string(' ', 16) + "000000+" + ("00000000000" + Convert.ToInt32(myReader2["total"]).ToString()).Substring(("00000000000" + Convert.ToInt32(myReader2["total"]).ToString()).Length - 11) + "00+");
                read11.Write("0000000+000" + new string(' ', 8));
                read11.Write(("0000000000000" + myReader2["nit"].ToString()).Substring(("0000000000000" + myReader2["nit"].ToString()).Length - 13));
                read11.WriteLine(new string(' ', 10));
                si1 = true;
                conce += 1;
            }
            read11.Close();
            pmyConec03.Close();
            myCommand2.Connection.Close();
            return si1;
        }

        public void leerNmUno85C()
        {
            // num_solicitud progreso = new num_solicitud(); // ERROR: CS0246
            double numeroLineas = 0;
            int f = 0;
            try
            {
                string mysql;
                string TextLine, Nombre = "", Tipo = "";

                if (vari.Agencia == "Todos")
                {
                    vari.AgenciaUp = "";
                }
                else
                {
                    vari.AgenciaUp = ("0000" + vari.Agencia).Substring(("0000" + vari.Agencia).Length - 4);
                }
                if (vari.Empresa == "Todos")
                {
                    vari.EmpresaUp = "";
                }
                else
                {
                    vari.EmpresaUp = ("0000" + vari.Empresa).Substring(("0000" + vari.Empresa).Length - 4);
                }
                if (vari.Cencosto == "Todos")
                {
                    vari.CencostoUp = "";
                }
                else
                {
                    vari.CencostoUp = ("00000000" + vari.Cencosto).Substring(("00000000" + vari.Cencosto).Length - 8);
                }

                // progreso.texto.Text = "GENERANDO ARCHIVO."; // ERROR: CS0103
                // progreso.numero.Text = "Espere por favor..."; // ERROR: CS0103
                // progreso.Button1.Visible = false; // ERROR: CS0103
                // progreso.ControlBox = false; // ERROR: CS0103
                // progreso.Pro.Visible = true; // ERROR: CS0103
                // progreso.Text = ""; // ERROR: CS0103

                int len = (int)new FileInfo(vari.dire).Length;

                // f = FileSystem.FreeFile(); // ERROR: CS0104
                // FileSystem.FileOpen(f, vari.dire, OpenMode.Input); // ERROR: CS0104
                // progreso.Pro.Value = 0; // ERROR: CS0103
                // progreso.Show(); // ERROR: CS0103
                mysql = "delete from cop_valdesc  where " +
                               " periodo = '" + vari.peri.Trim() +
                               "' and periodicidad = '" + vari.periodicidad +
                               "' and adicional = '" + vari.num + "' " + vari.stwhere.Replace("a.", "");
                coneccion_1(mysql);

                int c = 0;

                // while (!FileSystem.EOF(f)) // ERROR: CS0104
                {
                    // TextLine = FileSystem.LineInput(f); // ERROR: CS0104
                    // if (TextLine.Trim() != "") // ERROR: CS0165
                    {
                        vari.stcodigo = ""; // TextLine.Substring(0, 13).Trim();
                        vari.comcep = ""; // TextLine.Substring(15, 3).Trim();
                        vari.stvalor = ""; // TextLine.Substring(79, 11).Trim();

                        mysql = "select codigoter from sys_maenit where codigoter='" + ("00000000000000" + Convert.ToDouble(vari.stcodigo)).Substring(("00000000000000" + Convert.ToDouble(vari.stcodigo)).Length - 14) + "'";

                        numeroLineas += 1;
                        TextLine = vari.stcodigo + vari.stvalor + vari.comcep;
                        if (c == 0)
                        {
                            // progreso.Pro.Maximum = (int)(len / TextLine.Length); // ERROR: CS0103
                            c += 1;
                        }

                        OdbcConnection pmyConec04 = new OdbcConnection();
                        pmyConec04.ConnectionString = vari.constring;
                        OdbcCommand myCommand71 = new OdbcCommand(mysql, pmyConec04);
                        myCommand71.Connection.Open();
                        OdbcDataReader myReader71 = myCommand71.ExecuteReader();

                        if (myReader71.Read())
                        {
                            vari.stcodigo = myReader71["codigoter"].ToString();
                        }
                        else
                        {
                            Interaction.InputBox("", "No se pudo encontrar el asociado con cedula:", vari.stcodigo);
                            vari.stcodigo = ("000000000000000" + vari.stcodigo).Substring(("000000000000000" + vari.stcodigo).Length - 14);
                        }

                        myCommand71.Connection.Close();
                        pmyConec04.Close();

                        mysql = "select * from cop_valdesc a where " +
                        " periodo = '" + vari.peri.Trim() +
                        "' and periodicidad = '" + vari.periodicidad +
                        "' and codigoter = '" + vari.stcodigo +
                         "' and comcep = '" + vari.comcep +
                         "' and adicional = '" + vari.num + "' " + vari.stwhere;

                        OdbcConnection pmyConec05 = new OdbcConnection();
                        pmyConec05.ConnectionString = vari.constring;
                        OdbcCommand myCommand72 = new OdbcCommand(mysql, pmyConec05);
                        myCommand72.Connection.Open();

                        OdbcDataReader myReader72 = myCommand72.ExecuteReader();

                        if (myReader72.Read())
                        {
                            mysql = "update cop_valdesc set valor = '" + (Convert.ToDouble(myReader72["valor"]) + Convert.ToDouble(vari.stvalor)) +
                                                  "' where  periodo = '" + vari.peri +
                                                  "' and periodicidad = '" + vari.periodicidad + "' and codigoter = '" + vari.stcodigo.Trim() +
                                                  "'  and comcep = '" + vari.comcep + "' and adicional = '" + vari.num + "' " +
                                                  vari.stwhere.Replace("a.", "");
                        }
                        else
                        {
                            mysql = "insert into cop_valdesc (empresa,agencia,cencosto,periodo,periodicidad,codigoter,valor,adicional,comcep) values ('" +
                                                   vari.EmpresaUp + "','" + vari.AgenciaUp + "','" + vari.CencostoUp + "','" + vari.peri.Trim() + "','" +
                                                   vari.periodicidad + "','" + vari.stcodigo.Trim() + "','" +
                                                  Convert.ToDouble(vari.stvalor) + "','" + vari.num + "','" + vari.comcep + "')";
                        }
                        mysql = mysql.Replace("''", "' '");
                        coneccion_1(mysql);
                        myCommand72.Connection.Close();
                        pmyConec05.Close();

                        // progreso.texto.Text = "GENERANDO ARCHIVO."; // ERROR: CS0103
                        // progreso.numero.Text = "Espere por favor..."; // ERROR: CS0103
                        // progreso.Pro.Value += 1; // ERROR: CS0103
                    }
                    // Debug.WriteLine(FileSystem.Seek(f)); // ERROR: CS0104
                }
                // FileSystem.FileClose(f); // ERROR: CS0104
                // progreso.Close(); // ERROR: CS0103
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString() + " Error en la Linea " + numeroLineas + "   Nombre" + vari.nombre, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // FileSystem.FileClose(f); // ERROR: CS0104
                // progreso.Close(); // ERROR: CS0103
            }
        }

        public bool Produfen()
        {
            DateTime fecha = DateTime.Now;
            string dia = DateTime.Now.Day.ToString();
            string mes = DateTime.Now.Month.ToString();

            string mysql1 = "select  b.copto_nomina,c.codigo_empresa,c.nit,sum(a.vlr_aportes + a.vlr_prestamos + a.vlr_interes + a.vlr_extras + a.vlr_seguro + a.vlr_mora + a.vlr_admon + a.vlr_otras) as total " +
                           " from cop_nomdes  a left join cop_nomconce b on" +
                           " b.empresa = case  when '" + vari.EmpresaUp + "'  = '' then b.empresa else a.empresa end and " +
                           " b.cencosto = case  when '" + vari.CencostoUp + "'  = '' then b.cencosto else a.cencosto end and " +
                           " b.agencia = case  when '" + vari.AgenciaUp + "'  = '' then b.agencia else a.agencia end inner join sys_maenit c on c.codigoter = a.codigoter" +
                            " inner join cop_maecar d on d.codigoter = a.codigoter and d.lincred = a.lincred and d.numero = a.numero " +
                            " where a.periodo = '" + vari.peri + "' " + vari.stwhere + " and a.adicional ='" + vari.num +
                            "' and a.periodicidad = '" + vari.periodicidad + "' and a.lincred = b.lincred group by b.copto_nomina,c.codigo_empresa,c.nit order by c.nit,total";

            OdbcConnection pmyConec03 = new OdbcConnection();
            pmyConec03.Close();
            pmyConec03.ConnectionString = vari.constring;

            OdbcCommand myCommand2 = new OdbcCommand(mysql1, pmyConec03);
            myCommand2.Connection.Open();
            OdbcDataReader myReader2 = myCommand2.ExecuteReader();
            TextWriter read11 = File.CreateText(Application.StartupPath + "\\archivos planos\\export\\" + vari.nombre + ".csv");
            bool si1 = false;
            int conce = 1;
            while (myReader2.Read())
            {
                read11.Write(myReader2["codigo_empresa"].ToString() + ";" + Convert.ToInt32(myReader2["total"]).ToString() + ";" + vari.FechaFinal);
                read11.WriteLine();
                si1 = true;
                conce += 1;
            }
            read11.Close();
            pmyConec03.Close();
            myCommand2.Connection.Close();
            return si1;
        }

        public void Produfen_envio()
        {
            // num_solicitud progreso = new num_solicitud(); // ERROR: CS0246
            double numeroLineas = 0;
            string fechaPlano;
            try
            {
                string mysql;
                string TextLine, Nombre = "", Tipo = "";

                if (vari.Agencia == "Todos")
                {
                    vari.AgenciaUp = "";
                }
                else
                {
                    vari.AgenciaUp = ("0000" + vari.Agencia).Substring(("0000" + vari.Agencia).Length - 4);
                }
                if (vari.Empresa == "Todos")
                {
                    vari.EmpresaUp = "";
                }
                else
                {
                    vari.EmpresaUp = ("0000" + vari.Empresa).Substring(("0000" + vari.Empresa).Length - 4);
                }
                if (vari.Cencosto == "Todos")
                {
                    vari.CencostoUp = "";
                }
                else
                {
                    vari.CencostoUp = ("00000000" + vari.Cencosto).Substring(("00000000" + vari.Cencosto).Length - 8);
                }

                TextFieldParser MyReader_envio = new TextFieldParser(vari.dire);
                MyReader_envio.TextFieldType = FieldType.Delimited;
                MyReader_envio.SetDelimiters(";");

                string[] currentRow;

                mysql = "delete from cop_valdesc  where " +
                               " periodo = '" + vari.peri.Trim() +
                               "' and periodicidad = '" + vari.periodicidad +
                               "' and adicional = '" + vari.num + "' " + vari.stwhere.Replace("a.", "");
                coneccion_1(mysql);

                int c = 0;

                while (!MyReader_envio.EndOfData)
                {
                    currentRow = MyReader_envio.ReadFields();

                    vari.stcodigo = currentRow[0];
                    vari.stvalor = currentRow[1];
                    fechaPlano = currentRow[2];

                    mysql = "select a.codigoter ,b.copto_nomina  from sys_maenit a inner join cop_nomconce b on " +
                          " b.empresa = a.empresa where a.codigo_empresa ='" + vari.stcodigo + "' and  a.empresa ='" + vari.EmpresaUp + "' group by a.codigoter,b.copto_nomina";

                    numeroLineas += 1;

                    OdbcConnection pmyConec04 = new OdbcConnection();
                    pmyConec04.ConnectionString = vari.constring;
                    OdbcCommand myCommand71 = new OdbcCommand(mysql, pmyConec04);
                    myCommand71.Connection.Open();
                    OdbcDataReader myReader71 = myCommand71.ExecuteReader();

                    if (myReader71.Read())
                    {
                        vari.stcodigo = myReader71["codigoter"].ToString();
                        vari.comcep = myReader71["copto_nomina"].ToString();
                    }
                    else
                    {
                        Interaction.InputBox("No se pudo encontrar el asociado con cedula: ", "SOLIDO", vari.stcodigo);
                        vari.stcodigo = ("000000000000000" + vari.stcodigo).Substring(("000000000000000" + vari.stcodigo).Length - 14);
                    }

                    myCommand71.Connection.Close();
                    pmyConec04.Close();

                    mysql = "select * from cop_valdesc a where " +
                    " periodo = '" + vari.peri.Trim() +
                    "' and periodicidad = '" + vari.periodicidad +
                    "' and codigoter = '" + vari.stcodigo +
                     "' and comcep = '" + vari.comcep +
                     "' and adicional = '" + vari.num + "' " + vari.stwhere;

                    OdbcConnection pmyConec05 = new OdbcConnection();
                    pmyConec05.ConnectionString = vari.constring;
                    OdbcCommand myCommand72 = new OdbcCommand(mysql, pmyConec05);
                    myCommand72.Connection.Open();

                    OdbcDataReader myReader72 = myCommand72.ExecuteReader();

                    if (myReader72.Read())
                    {
                        mysql = "update cop_valdesc set valor = '" + (Convert.ToDouble(myReader72["valor"]) + Convert.ToDouble(vari.stvalor)) +
                                              "' where  periodo = '" + vari.peri +
                                              "' and periodicidad = '" + vari.periodicidad + "' and codigoter = '" + vari.stcodigo.Trim() +
                                              "'  and comcep = '" + vari.comcep + "' and adicional = '" + vari.num + "' " +
                                              vari.stwhere.Replace("a.", "");
                    }
                    else
                    {
                        mysql = "insert into cop_valdesc (empresa,agencia,cencosto,periodo,periodicidad,codigoter,valor,adicional,comcep) values ('" +
                                               vari.EmpresaUp + "','" + vari.AgenciaUp + "','" + vari.CencostoUp + "','" + vari.peri.Trim() + "','" +
                                               vari.periodicidad + "','" + vari.stcodigo.Trim() + "','" +
                                              Convert.ToDouble(vari.stvalor) + "','" + vari.num + "','" + vari.comcep + "')";
                    }
                    mysql = mysql.Replace("''", "' '");
                    coneccion_1(mysql);
                    myCommand72.Connection.Close();
                    pmyConec05.Close();
                }
                MyReader_envio.Close();
                // progreso.Close(); // ERROR: CS0103
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString() + " Error en la Linea " + numeroLineas + "   Nombre" + vari.nombre, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // progreso.Close(); // ERROR: CS0103
            }
        }
    }
}
