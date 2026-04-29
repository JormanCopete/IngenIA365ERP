#if CRYSTAL_LEGACY
using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Reportes.Services
{
    public class clsmscext02
    {
        private ERP.Core.Compartido.Reportes.config_report msgsas = new ERP.Core.Compartido.Reportes.config_report();
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Datos.ClsConect msgconec = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect VarIni = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private OdbcConnection Conect = new OdbcConnection();
        private OdbcConnection Conect2 = new OdbcConnection();
        private ERP.Core.Compartido.Configuracion.ParamSys paramSys = new ERP.Core.Compartido.Configuracion.ParamSys();

        public enum opciones : int
        {
            Impresora = 0,
            Correo_electronico = 1,
            A_disco_formato_pdf = 2,
            A_disco_Un_Solo_Archivo = 3,
            A_archivo_plano = 4
        }

        public clsmscext02()
        {
            msgconec.MyOdbcConect(VarIni);
            Conect.ConnectionString = VarIni.pstMyconec;
            Conect.Open();

            Conect2.ConnectionString = VarIni.pstMyconec;
            Conect2.Open();
        }

        public void ImprimeEstadoCta(string CodigoterIni, string CodigoterFin, string periodo,
            Form myforma, opciones opcion,
            bool Uno = false, int TipoAso = 0,
            string Cencosto = "", int Orden = 0,
            string Agencia = "", string Empresa = "",
            string Seccion = "", string detalle = "",
            string estado = "", DateTime? FechaPago = null,
            bool validaDirEnvio = false, string formato = "1")
        {
            if (FechaPago == null) FechaPago = new DateTime(1950, 1, 1);

            int InAnio;
            int InMes;
            string ordenaPor = "";
            ERP.Core.Compartido.Controles.Barraprogress pro = new ERP.Core.Compartido.Controles.Barraprogress("Generando archivo.", myforma);

            if (Uno == true)
            {
                if (CodigoterIni.Trim() == "")
                {
                    MessageBox.Show("Falta el codigo del asociado.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                else
                {
                    CodigoterIni = ("0000000000000" + CodigoterIni).Substring(Math.Max(0, ("0000000000000" + CodigoterIni).Length - 14));
                }
                CodigoterFin = CodigoterIni;
            }
            else
            {
                CodigoterIni = ("0000000000000" + CodigoterIni).Substring(Math.Max(0, ("0000000000000" + CodigoterIni).Length - 14));
                CodigoterFin = ("0000000000000" + CodigoterFin).Substring(Math.Max(0, ("0000000000000" + CodigoterFin).Length - 14));
            }

            try
            {
                string mysql;
                string texto = "";
                SaveFileDialog archi = new SaveFileDialog();
                CrystalDecisions.CrystalReports.Engine.ReportDocument rep;

                rep = new ERP.Core.Compartido.Reportes.reporte("extracto02");

                if (opcion == opciones.Impresora)
                {
                    texto = "Enviando a la impresora.";
                }
                else if (opcion == opciones.Correo_electronico)
                {
                    texto = "Enviando al correo electronico.";
                }
                else if (opcion == opciones.A_disco_formato_pdf || opcion == opciones.A_disco_Un_Solo_Archivo)
                {
                    if (archi.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                    texto = "Enviando al disco.";
                }

                InAnio = int.Parse(periodo.Substring(0, 4));
                InMes = int.Parse(periodo.Substring(4));

                rep.SetParameterValue("Cencosto", Cencosto);
                rep.SetParameterValue("periodo", periodo);
                rep.SetParameterValue("detalle", detalle);
                rep.SetParameterValue("agencia", Agencia);
                rep.SetParameterValue("seccion", Seccion);
                rep.SetParameterValue("empresa", Empresa);
                rep.SetParameterValue("TipoAso", TipoAso);
                rep.SetParameterValue("Orden", Orden);
                rep.SetParameterValue("estado", estado);
                try
                {
                    rep.SetParameterValue("FechaPago", FechaPago);
                }
                catch { }

                if (opcion != opciones.A_disco_Un_Solo_Archivo && opcion != opciones.Impresora && opcion != opciones.A_archivo_plano)
                {
                    switch (Orden)
                    {
                        case 1:
                            ordenaPor = " order by m.empresa,m.codigoter ";
                            break;
                        case 2:
                            ordenaPor = " order by m.agencia,m.codigoter ";
                            break;
                        case 3:
                            ordenaPor = " order by m.cencosto,m.codigoter ";
                            break;
                        case 4:
                            ordenaPor = " order by m.SECCION_EMPRESA,m.codigoter ";
                            break;
                        default:
                            ordenaPor = " order by m.codigoter  ";
                            break;
                    }

                    mysql = "select m.codigoter,m.email,c.serverSmtp,c.correoEnvio,c.PasswordEnvio,c.puertoSMTP,c.EnabledSSL from  sys_maenit m, sys_compania c where c.codigo='0001' and " +
                                               (Empresa == "Todas" ? "" : " m.empresa = '" + ("0000" + Empresa).Substring(Math.Max(0, ("0000" + Empresa).Length - 4)) + "' and ") +
                                               (Seccion == "Todas" ? "" : " m.SECCION_EMPRESA = '" + ("0000" + Seccion).Substring(Math.Max(0, ("0000" + Seccion).Length - 4)) + "' and ") +
                                               (Agencia == "Todas" ? "" : " m.agencia = '" + ("0000" + Agencia).Substring(Math.Max(0, ("0000" + Agencia).Length - 4)) + "' and ") +
                                               (Cencosto == "Todos" ? "" : " m.cencosto = '" + ("00000000" + Cencosto).Substring(Math.Max(0, ("00000000" + Cencosto).Length - 8)) + "' and ") +
                                               (estado == "O" ? "" : (estado == "A" ? " m.estado in ('A','S') and " : " m.estado = '" + estado + "' and ")) +
                                               " m.codigoter >= '" + CodigoterIni + "' and m.codigoter <= '" + CodigoterFin + "'" +
                                               (TipoAso == 1 ? " and m.clase = '5'" : (TipoAso == 2 ? " and m.clase = '6'" : " ")) +
                                               (validaDirEnvio ? " and m.ENVIO_DIR = '3' " : " ") + ordenaPor;

                    OdbcCommand comand = new OdbcCommand(mysql, Conect);
                    if (Uno == false)
                    {
                        pro.DefineMaximo(mysql, Conect, texto);
                        pro.Show();
                    }
                    else
                    {
                        pro.ValorMinimoMaximo(0, 2);
                        pro.Show();
                    }

                    OdbcDataReader reader = comand.ExecuteReader();
                    int conta = 1;
                    DateTime mes = DateTime.Parse("25/" + periodo.Substring(4) + "/2009");
                    string anno = periodo.Substring(0, 4);
                    string mmes = mes.ToString("MMMM");
                    string fallos = "";
                    int cont = 0;
                    string nombre = "";
                    TextWriter arc_Txt = null;

                    string ruta_temp = Path.GetTempPath();

                    if (!Directory.Exists(ruta_temp + "\\Archivos planos"))
                    {
                        Directory.CreateDirectory(ruta_temp + "\\Archivos planos");
                    }

                    pro.PerformStep();
                    while (reader.Read())
                    {
                        rep.SetParameterValue("codigo_asociado", reader["codigoter"]);
                        rep.SetParameterValue("codigoterFin", reader["codigoter"]);

                        string valemail = reader["email"].ToString();
                        bool verdad = paramSys.validaEmails(valemail);

                        if (opcion == opciones.Correo_electronico && verdad == true)
                        {
                            rep.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat,
                                                               ruta_temp + "\\Archivos planos\\extracto a " + reader["codigoter"] + ".pdf");

                            string email = reader["correoEnvio"].ToString();
                            string smtpp = reader["serverSmtp"].ToString();
                            string password = reader["PasswordEnvio"].ToString();
                            string dir_envio = reader["email"].ToString();
                            int puertoSMTP;
                            if (Information.IsNumeric(reader["puertoSMTP"]))
                            {
                                puertoSMTP = Convert.ToInt32(reader["puertoSMTP"]);
                            }
                            else
                            {
                                puertoSMTP = 587;
                            }
                            string EnabledSSL = reader["EnabledSSL"].ToString();

                            string asunto, cuerpo;
                            try
                            {
                                asunto = "Estado de cuenta a: " + mmes + " del " + anno;
                                cuerpo = "Se Adjunto Estado de cuenta a: " + mmes + " del " + anno;

                                if (smtpp.Trim() == "")
                                {
                                    ArrayList RutasArchivo = new ArrayList();
                                    RutasArchivo.Add(ruta_temp + "\\Archivos planos\\extracto a " + reader["codigoter"] + ".pdf");
                                    paramSys.EnviarCorreoporOutlook(smtpp, password, email, dir_envio, asunto, cuerpo, RutasArchivo);
                                }
                                else
                                {
                                    MailMessage MyMailMsg = new MailMessage();
                                    Attachment AddFile = new Attachment(ruta_temp + "\\Archivos planos\\extracto a " + reader["codigoter"] + ".pdf");
                                    string[] Destinatarios = dir_envio.Split(';');
                                    if (Destinatarios.Length > 1)
                                    {
                                        for (int i = 1; i < Destinatarios.Length; i++)
                                        {
                                            MyMailMsg.CC.Add(Destinatarios[i]);
                                        }
                                    }
                                    MyMailMsg.From = new MailAddress(email);
                                    MyMailMsg.To.Add(Destinatarios[0]);
                                    MyMailMsg.Subject = asunto;
                                    MyMailMsg.Attachments.Add(AddFile);
                                    MyMailMsg.Body = cuerpo;
                                    MyMailMsg.IsBodyHtml = false;
                                    MyMailMsg.Priority = MailPriority.Normal;

                                    SmtpClient SMTP = new SmtpClient(smtpp);
                                    SMTP.UseDefaultCredentials = false;
                                    SMTP.Port = puertoSMTP;
                                    SMTP.DeliveryMethod = SmtpDeliveryMethod.Network;
                                    SMTP.Credentials = new NetworkCredential(email, password);
                                    if (EnabledSSL == "Y")
                                    {
                                        SMTP.EnableSsl = true;
                                    }

                                    MyMailMsg.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
                                    ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);

                                    SMTP.Send(MyMailMsg);
                                    MyMailMsg.Dispose();
                                    AddFile.Dispose();
                                }
                            }
                            catch (Exception ex)
                            {
                                fallos += dir_envio + "  " + reader["codigoter"] + " Error: " + ex.Message + "\r\n";
                                cont += 1;
                            }
                            Uno = false;
                        }
                        else if (opcion == opciones.A_disco_formato_pdf)
                        {
                            rep.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat,
                            archi.FileName + " - " + reader["codigoter"] + ".pdf");
                            Uno = false;
                        }

                        pro.PerformStep();
                        Application.DoEvents();
                        conta += 1;
                    }
                    reader.Close();

                    if (cont != 0)
                    {
                        nombre = "Correo-" + DateTime.Now.ToString("yyyyMMdd-hh=mm") + ".txt";
                        string appPath = System.Windows.Forms.Application.StartupPath;
                        if (!Directory.Exists(appPath + "\\Archivos planos"))
                        {
                            Directory.CreateDirectory(appPath + "\\Archivos planos");
                        }
                        arc_Txt = File.CreateText(appPath + "\\Archivos planos\\" + nombre);
                        arc_Txt.WriteLine(" EMAIL                     CODIGO ASOCIADO ");
                        arc_Txt.WriteLine(fallos);
                        arc_Txt.Close();
                        MessageBox.Show("Los siguientes archivos tuvieron fallos en su envio, ubicacion:\r\n" + appPath + "\\Archivos planos\\" + nombre, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Uno = true;
                    }

                    try
                    {
                        Directory.Delete(ruta_temp + "\\Archivos planos", true);
                    }
                    catch { }

                    if (Uno == false)
                    {
                        pro.Close();
                    }
                }
                else if (opcion == opciones.Impresora || opcion == opciones.A_disco_Un_Solo_Archivo)
                {
                    rep.SetParameterValue("codigo_asociado", CodigoterIni);
                    rep.SetParameterValue("codigoterFin", CodigoterFin);

                    if (opcion == opciones.Impresora)
                    {
                        PrintDialog prin = new PrintDialog();
                        if (Uno == false)
                        {
                            if (prin.ShowDialog() == DialogResult.OK)
                            {
                                rep.PrintOptions.PrinterName = prin.PrinterSettings.PrinterName;
                                rep.PrintToPrinter(1, false, 1, 9999);
                            }
                        }
                        else
                        {
                            msgsas.confi_reportes(myforma, rep);
                        }
                    }
                    else
                    {
                        pro = new ERP.Core.Compartido.Controles.Barraprogress("Generando archivo.", myforma, ProgressBarStyle.Marquee);
                        pro.Show();
                        rep.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.RichText,
                                     archi.FileName + ".rtf");
                        pro.Close();
                    }
                }
                else if (opcion == opciones.A_archivo_plano)
                {
                    string consulta, consulta2 = "", feec = "";

                    paramSys.BuscarCompania("0001", Conect, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, ref feec);

                    if (Orden == 1)
                    {
                        ordenaPor = " ORDER BY c.empresa,a.codigoter";
                    }
                    else
                    {
                        ordenaPor = " ORDER BY a.codigoter";
                    }

                    switch (feec.Trim())
                    {
                        case "EST":
                        case "":
                            consulta = BuildConsultaEST(periodo, CodigoterIni, CodigoterFin, Empresa, Seccion, Agencia, Cencosto, estado, TipoAso, ordenaPor);
                            break;
                        case "MOV":
                            consulta = BuildConsultaMOV(periodo, CodigoterIni, CodigoterFin, Empresa, Seccion, Agencia, Cencosto, estado, TipoAso);
                            consulta2 = BuildConsulta2MOV(periodo, CodigoterIni, CodigoterFin, Empresa, Seccion, Agencia, Cencosto, estado, TipoAso);
                            break;
                        default:
                            consulta = BuildConsultaEST(periodo, CodigoterIni, CodigoterFin, Empresa, Seccion, Agencia, Cencosto, estado, TipoAso, ordenaPor);
                            break;
                    }

                    OdbcCommand myCommand2 = new OdbcCommand(consulta, Conect);
                    myCommand2.CommandTimeout = 0;
                    OdbcDataReader myReader = myCommand2.ExecuteReader();

                    string appPath = System.Windows.Forms.Application.StartupPath;
                    if (!Directory.Exists(appPath + "\\Archivos planos"))
                    {
                        Directory.CreateDirectory(appPath + "\\Archivos planos");
                    }

                    TextWriter read11;
                    TextWriter readApor = null;

                    switch (feec.Trim())
                    {
                        case "EST":
                        case "":
                            read11 = File.CreateText(appPath + "\\Archivos planos\\PLANO.txt");
                            break;
                        case "MOV":
                            read11 = File.CreateText(appPath + "\\Archivos planos\\movimientos.txt");
                            readApor = File.CreateText(appPath + "\\Archivos planos\\aportes.txt");
                            msgconec.ExecuteQueryconec("delete from cop_tempmovextra", Conect2, "ImprimrEstadoCta");
                            break;
                        default:
                            read11 = File.CreateText(appPath + "\\Archivos planos\\PLANO.txt");
                            break;
                    }

                    bool OtraLinea = true;

                    while (myReader.Read())
                    {
                        switch (feec.Trim())
                        {
                            case "EST":
                            case "":
                                WriteESTLine(read11, myReader, periodo);
                                break;
                            case "MOV":
                                OtraLinea = WriteMOVLine(read11, readApor, myReader, periodo, Conect2);
                                break;
                            default:
                                WriteESTLine(read11, myReader, periodo);
                                break;
                        }
                    }

                    if (feec.Trim() == "MOV")
                    {
                        DataTable TblMov = new DataTable();
                        if (msgconec.ExecuteConsulta("select * from cop_tempmovextra", Conect, "", TblMov) == true)
                        {
                            for (int conta2 = 0; conta2 < TblMov.Rows.Count; conta2++)
                            {
                                DataRow row = TblMov.Rows[conta2];
                                read11.Write(row["descripcion"].ToString() + "," + row["saldoanterior"] + "," + row["saldoenmoraanterior"] + "," + row["causacapital"] +
                                "," + row["causainteres"] + "," + row["pagos"] + "," + row["saldoenmora"] + "," + row["saldofinal"] + "," + row["nit"]);
                                read11.WriteLine();
                            }
                        }
                        if (readApor != null) readApor.Close();
                    }
                    read11.Close();

                    // Consultar cuotas extras
                    string consultaExtras = BuildConsultaExtras(periodo, CodigoterIni, CodigoterFin, Empresa, Seccion, Agencia, Cencosto, estado, TipoAso);

                    OdbcCommand myCommand3 = new OdbcCommand(consultaExtras, Conect);
                    myCommand3.CommandTimeout = 0;
                    OdbcDataReader myReader2 = myCommand3.ExecuteReader();

                    TextWriter read12 = File.CreateText(appPath + "\\Archivos planos\\EXTRAS.txt");

                    while (myReader2.Read())
                    {
                        WriteExtrasLine(read12, myReader2, feec);
                    }
                    read12.Close();

                    // Nuevos archivos para MOV
                    if (feec.Trim() == "MOV" && !string.IsNullOrEmpty(consulta2))
                    {
                        OdbcCommand myCommandAso = new OdbcCommand(consulta2, Conect);
                        myCommandAso.CommandTimeout = 0;
                        OdbcDataReader myReadAso = myCommandAso.ExecuteReader();

                        TextWriter readAso = File.CreateText(appPath + "\\Archivos planos\\asociados.txt");

                        while (myReadAso.Read())
                        {
                            WriteAsociadosLine(readAso, myReadAso);
                        }
                        readAso.Close();
                    }

                    Uno = false;
                    MessageBox.Show("Ubicacion:\r\n" + appPath + "\\Archivos planos\\PLANO.txt", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                if (opcion == opciones.Correo_electronico)
                {
                    rep.Dispose();
                    pro.Close();
                }

                if (Uno == false)
                {
                    if (opcion != opciones.A_disco_Un_Solo_Archivo)
                    {
                        rep.Dispose();
                        MessageBox.Show("El proceso termino correctamente.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string BuildConsultaEST(string periodo, string CodigoterIni, string CodigoterFin, string Empresa, string Seccion, string Agencia, string Cencosto, string estado, int TipoAso, string ordenaPor)
        {
            return "SELECT DISTINCT a.CODIGOTER,a.LINCRED,a.NUMERO,a.FECFACT, a.fecdesc,a.plazo,g.tasaint, a.valorob,g.cuota, g.periodd, " +
                   "g.clades, g.ciclod, g.saldo_inicial, max(d.capital_abono) as capital_abono,max(d.interes_abono) as interes_abono, " +
                   "max(d.mora_abono) as mora_abono, c.empresa, c.nit, c.nombre, c.apellido, b.DESCRIPCION,case  b.VERSALDO when 'Y' then g.saldo else 0 end as saldo,g.cuopen " +
                   ",g.cuopen,(select count(mor.diasMora) from cop_copmora mor where a.CODIGOTER = mor.codigoter AND a.LINCRED = mor.lincred AND a.NUMERO = mor.numero AND mor.periodo_contable = " + periodo + " and mor.diasMora > 0) as cuotVencidas" +
                   ",sum(d.SaldoCapital + d.SaldoExtra + d.SaldoInteres + d.SaldoMora + d.SaldoSeguro + d.SaldoAdmon + d.SaldoOtros) as PendPago " +
                   " FROM cop_maecar a" +
                   " LEFT JOIN cop_salmaecar g ON a.CODIGOTER = g.CODIGOTER AND a.LINCRED = g.LINCRED AND a.NUMERO = g.NUMERO  and g.periodo = " + periodo +
                   " LEFT OUTER JOIN cop_copmora d ON a.CODIGOTER = d.codigoter AND a.LINCRED = d.lincred AND a.NUMERO = d.numero AND d.periodo_contable = " + periodo +
                   " INNER JOIN sys_maenit c ON a.CODIGOTER  = c.CODIGOTER" +
                   " INNER JOIN cop_concar12 b ON a.LINCRED = b.LINCRED" +
                   " WHERE a.codigoter between '" + CodigoterIni + "' and '" + CodigoterFin + "'" +
                   " AND b.ESTCTA = 'Y' AND ((a.lincred >= 1000 and g.saldo <> 0) or (a.lincred < 1000))" +
                   " and c.estado = 'A' and ((g.saldo <> 0 And a.LINCRED >= 1000) Or ((a.LINCRED < 1000)" +
                   " and (g.saldo <> 0 Or g.cuota <> 0))) " +
                   (Empresa == "Todas" ? "" : " AND c.empresa = '" + ("0000" + Empresa).Substring(Math.Max(0, ("0000" + Empresa).Length - 4)) + "' ") +
                   (Seccion == "Todas" ? "" : " AND c.SECCION_EMPRESA = '" + ("0000" + Seccion).Substring(Math.Max(0, ("0000" + Seccion).Length - 4)) + "' ") +
                   (Agencia == "Todas" ? "" : " AND c.agencia = '" + ("0000" + Agencia).Substring(Math.Max(0, ("0000" + Agencia).Length - 4)) + "' ") +
                   (Cencosto == "Todos" ? "" : " AND c.cencosto = '" + ("00000000" + Cencosto).Substring(Math.Max(0, ("00000000" + Cencosto).Length - 8)) + "' ") +
                   (estado == "O" ? "" : " AND c.estado = '" + estado + "' ") +
                   (TipoAso == 1 ? " AND c.clase = '5' " : (TipoAso == 2 ? "  AND c.clase = '6' " : " ")) +
                   " GROUP BY a.CODIGOTER,a.LINCRED,a.NUMERO,a.FECFACT, a.fecdesc,a.plazo,g.tasaint, a.valorob,g.cuota, g.periodd, " +
                   "g.clades, g.ciclod, g.saldo_inicial, c.empresa, c.nit, c.nombre, c.apellido, b.DESCRIPCION, b.VERSALDO, g.saldo,g.cuopen " + ordenaPor;
        }

        private string BuildConsultaMOV(string periodo, string CodigoterIni, string CodigoterFin, string Empresa, string Seccion, string Agencia, string Cencosto, string estado, int TipoAso)
        {
            return "SELECT   a.CODIGOTER,c.nit, a.LINCRED, a.cobrojur,  a.NUMERO, b.ESTCTA, b.VERSALDO,g.saldo_inicial, g.PERIODO, g.SALDO, a.fecdesc, " +
                   "a.CUOTA,a.valorob,a.plazo,a.clades, b.DESCRIPCION,b.CONSAL, b.CODAHOR, a.tasaint, " +
                   "e.NOMBRE AS cencosto,c.estado,f.nombre AS sgencia, a.FECFACT, a.FECVEMTO,g.cuopen,c.nombre as nomaso,c.apellido as apeaso,c.telefono1, " +
                   "c.fecha_ingreso,t.nombre_ciudad,q.nombre as nomempresa,c.direccion,1 as can,c.seccion_empresa,c.AGENCIA, " +
                   "c.CENCOSTO as ccosto, c.empresa,c.clase, g.vlr_debito, g.vlr_credito as salCredito, h.vlr_credito, j.tipo_movto, k.NOMBRE as nomcoop, " +
                   "k.NOMRES ,k.NIT as nitcoop,k.TELEFONO as telcoop, k.ciudad as ciucoop, k.dpto as dptocoop,k.direccion as dirCoop, m.anulado, n.restri_tesoreria, h.secuencia,c.fecha_reingreso, h.COMPRONTE, " +
                   "sum(d.SaldoCapital) as SaldoCapital,sum(d.SaldoInteres) as SaldoInteres, " +
                   "sum(d.SaldoMora) as SaldoMora,sum(d.SaldoOtros) as SaldoOtros, " +
                   "sum(d.SaldoAdmon) SaldoAdmon,sum(d.SaldoSeguro) SaldoSeguro, " +
                   "sum(d.SaldoExtra) as SaldoExtra, " +
                   "sum(d.Saldo_AntCapital) as Saldo_AntCapital,sum(d.Saldo_AntExtra) as Saldo_AntExtra, " +
                   "sum(d.Saldo_AntInteres) as Saldo_AntInteres,sum(d.Saldo_AntMora) as Saldo_AntMora, " +
                   "sum(d.Saldo_AntSeguro) as Saldo_AntSeguro,sum(d.Saldo_AntAdmon) as Saldo_AntAdmon, " +
                   "sum(d.Saldo_AntOtros) as Saldo_AntOtros, " +
                   "sum(d.Capital_causado) as Capital_causado,sum(d.Extra_causado) as Extra_causado, " +
                   "sum(d.Interes_causado) as Interes_causado,sum(d.Mora_causado) as Mora_causado, " +
                   "sum(d.Seguro_causado) as Seguro_causado,sum(d.Admon_causado) as Admon_causado, " +
                   "sum(d.Otros_causado) as Otros_causado, a.periodd " +
                   "FROM      cop_maecar a INNER JOIN " +
                   "cop_concar12 b ON a.LINCRED = b.LINCRED INNER JOIN " +
                   "sys_maenit c ON c.CODIGOTER = a.CODIGOTER left JOIN " +
                   "cop_salmaecar g ON g.CODIGOTER = a.CODIGOTER AND g.LINCRED = a.LINCRED AND " +
                   "g.NUMERO = a.NUMERO  and g.periodo =  " + periodo + " LEFT OUTER JOIN " +
                   "cop_copmora d ON a.CODIGOTER = d.codigoter AND " +
                   "a.LINCRED = d.lincred AND a.NUMERO = d.numero AND " +
                   "d.periodo_contable =   " + periodo + "   INNER JOIN " +
                   "sys_cencos e ON e.CCOSTO = c.CENCOSTO INNER JOIN " +
                   "sys_agencia f ON f.codigo = c.AGENCIA inner join " +
                   "sys_ciudad57 t ON t.ciudad = c.dpto_ciudad INNER JOIN " +
                   "cop_empresa13 q ON q.codigo_empresa = c.empresa LEFT JOIN " +
                   "cop_movimto h ON g.CODIGOTER = h.CODIGOTER AND g.LINCRED = h.LINCRED AND " +
                   "g.NUMERO = h.NUMERO AND h.periodo= " + periodo + " LEFT JOIN " +
                   "cop_codmov j ON h.cod_movto = j.cod_movto left join " +
                   "cop_docmto m ON h.compronte = m.compronte AND h.numero_domto = m.numero_domto left Join " +
                   "sys_compro02 n ON m.compronte = n.codigo INNER JOIN " +
                   "sys_compania  k ON k.CODIGO = '0001' " +
                   "where a.codigoter between '" + CodigoterIni + "' and '" + CodigoterFin + "' AND b.ESTCTA = 'Y' " +
                   (Empresa == "Todas" ? "" : " AND c.empresa = '" + ("0000" + Empresa).Substring(Math.Max(0, ("0000" + Empresa).Length - 4)) + "' ") +
                   (Seccion == "Todas" ? "" : " AND c.SECCION_EMPRESA = '" + ("0000" + Seccion).Substring(Math.Max(0, ("0000" + Seccion).Length - 4)) + "' ") +
                   (Agencia == "Todas" ? "" : " AND c.agencia = '" + ("0000" + Agencia).Substring(Math.Max(0, ("0000" + Agencia).Length - 4)) + "' ") +
                   (Cencosto == "Todos" ? "" : " AND c.cencosto = '" + ("00000000" + Cencosto).Substring(Math.Max(0, ("00000000" + Cencosto).Length - 8)) + "' ") +
                   (estado == "O" ? "" : " AND c.estado = '" + estado + "' ") +
                   (TipoAso == 1 ? " AND c.clase = '5' " : (TipoAso == 2 ? "  AND c.clase = '6' " : " ")) +
                   "and ((a.lincred >= 1000 and g.saldo<>0) or (a.lincred < 1000) or (a.lincred >= 1000 and g.saldo=0 and g.saldo_inicial<>0)) " +
                   "AND a.codigoter <> '99999999999999' " +
                   "GROUP BY a.CODIGOTER,c.nit, a.LINCRED, a.cobrojur,  a.NUMERO, b.ESTCTA, b.VERSALDO,g.saldo_inicial, g.PERIODO, g.SALDO, a.fecdesc, " +
                   "a.CUOTA,a.valorob,a.plazo,a.clades, b.DESCRIPCION,b.CONSAL, b.CODAHOR, a.tasaint, " +
                   "e.NOMBRE ,c.estado,f.nombre, a.FECFACT, a.FECVEMTO,g.cuopen,c.nombre ,c.apellido ,c.telefono1, " +
                   "c.fecha_ingreso,t.nombre_ciudad,q.nombre ,c.direccion,c.seccion_empresa,c.AGENCIA, " +
                   "c.CENCOSTO , c.empresa,c.clase, g.vlr_debito, g.vlr_credito , h.vlr_credito, j.tipo_movto, k.NOMBRE, " +
                   "k.NOMRES ,k.NIT ,k.TELEFONO, k.ciudad , k.dpto ,k.direccion , m.anulado, n.restri_tesoreria, h.secuencia,c.fecha_reingreso, h.COMPRONTE, a.periodd " +
                   "Order By a.CODIGOTER, a.LINCRED, a.NUMERO ";
        }

        private string BuildConsulta2MOV(string periodo, string CodigoterIni, string CodigoterFin, string Empresa, string Seccion, string Agencia, string Cencosto, string estado, int TipoAso)
        {
            return "SELECT  a.CODIGOTER,c.nit, e.NOMBRE AS cencosto,c.estado,f.nombre AS sgencia, c.nombre as nomaso,c.apellido as apeaso,c.telefono1, c.fecha_ingreso," +
                   "t.nombre_ciudad,q.nombre as nomempresa,c.direccion,c.seccion_empresa,sec.nombre as nomseccion,c.AGENCIA, c.CENCOSTO as ccosto, c.empresa," +
                   "c.clase, c.fecha_reingreso " +
                   "FROM      cop_maecar a INNER JOIN " +
                   "cop_concar12 b ON a.LINCRED = b.LINCRED INNER JOIN " +
                   "sys_maenit c ON c.CODIGOTER = a.CODIGOTER left JOIN " +
                   "cop_salmaecar g ON g.CODIGOTER = a.CODIGOTER AND g.LINCRED = a.LINCRED AND " +
                   "g.NUMERO = a.NUMERO  and g.periodo =  " + periodo + " LEFT OUTER JOIN " +
                   "cop_copmora d ON a.CODIGOTER = d.codigoter AND " +
                   "a.LINCRED = d.lincred AND a.NUMERO = d.numero AND " +
                   "d.periodo_contable =   " + periodo + "   INNER JOIN " +
                   "sys_cencos e ON e.CCOSTO = c.CENCOSTO INNER JOIN " +
                   "sys_agencia f ON f.codigo = c.AGENCIA inner join " +
                   "sys_seccion56 sec on c.seccion_empresa = sec.codigo_seccion  inner join " +
                   "sys_ciudad57 t ON t.ciudad = c.dpto_ciudad INNER JOIN " +
                   "cop_empresa13 q ON q.codigo_empresa = c.empresa LEFT JOIN " +
                   "cop_movimto h ON g.CODIGOTER = h.CODIGOTER AND g.LINCRED = h.LINCRED AND " +
                   "g.NUMERO = h.NUMERO AND h.periodo= " + periodo + " LEFT JOIN " +
                   "cop_codmov j ON h.cod_movto = j.cod_movto left join " +
                   "cop_docmto m ON h.compronte = m.compronte AND h.numero_domto = m.numero_domto left Join " +
                   "sys_compro02 n ON m.compronte = n.codigo INNER JOIN " +
                   "sys_compania  k ON k.CODIGO = '0001' " +
                   "where a.codigoter between '" + CodigoterIni + "' and '" + CodigoterFin + "' AND b.ESTCTA = 'Y' " +
                   (Empresa == "Todas" ? "" : " AND c.empresa = '" + ("0000" + Empresa).Substring(Math.Max(0, ("0000" + Empresa).Length - 4)) + "' ") +
                   (Seccion == "Todas" ? "" : " AND c.SECCION_EMPRESA = '" + ("0000" + Seccion).Substring(Math.Max(0, ("0000" + Seccion).Length - 4)) + "' ") +
                   (Agencia == "Todas" ? "" : " AND c.agencia = '" + ("0000" + Agencia).Substring(Math.Max(0, ("0000" + Agencia).Length - 4)) + "' ") +
                   (Cencosto == "Todos" ? "" : " AND c.cencosto = '" + ("00000000" + Cencosto).Substring(Math.Max(0, ("00000000" + Cencosto).Length - 8)) + "' ") +
                   (estado == "O" ? "" : " AND c.estado = '" + estado + "' ") +
                   (TipoAso == 1 ? " AND c.clase = '5' " : (TipoAso == 2 ? "  AND c.clase = '6' " : " ")) +
                   "and ((a.lincred >= 1000 and g.saldo<>0) or (a.lincred < 1000) or (a.lincred >= 1000 and g.saldo=0 and g.saldo_inicial<>0)) " +
                   "AND a.codigoter <> '99999999999999' " +
                   "GROUP BY a.CODIGOTER,c.nit, e.NOMBRE,c.estado,f.nombre, c.nombre,c.apellido,c.telefono1, c.fecha_ingreso, " +
                   "t.nombre_ciudad, q.nombre, c.direccion, c.seccion_empresa, sec.nombre, c.AGENCIA, c.CENCOSTO, c.empresa, c.clase, c.fecha_reingreso";
        }

        private string BuildConsultaExtras(string periodo, string CodigoterIni, string CodigoterFin, string Empresa, string Seccion, string Agencia, string Cencosto, string estado, int TipoAso)
        {
            return "SELECT DISTINCT mae.lincred,mae.numero,c.nit,c.empresa,ex.FECHA_PAGO,ex.valor,ex.forma_pago,salex.NUM_EXTRA,salex.SALDO,ex.tipoextra " +
                   " from cop_maecar mae" +
                   " LEFT JOIN  cop_salmaecar g ON mae.CODIGOTER = g.CODIGOTER AND mae.LINCRED = g.LINCRED AND mae.NUMERO = g.NUMERO and g.periodo = " + periodo +
                   " INNER JOIN sys_maenit c ON mae.CODIGOTER = c.CODIGOTER " +
                   " INNER join cop_salextras salex ON mae.codigoter = salex.codigoter and mae.lincred = salex.lincred and mae.numero = salex.numero and salex.periodo = " + periodo +
                   " INNER join cop_extras ex on mae.codigoter = ex.codigoter and mae.lincred = ex.lincred and mae.numero = ex.numero and salex.num_extra = ex.num_extra" +
                   " INNER JOIN cop_concar12 b ON mae.LINCRED = b.LINCRED" +
                   " WHERE mae.codigoter between '" + CodigoterIni + "' and '" + CodigoterFin + "'" +
                   " AND salex.saldo <> 0 AND b.ESTCTA = 'Y' AND ((mae.lincred >= 1000 and g.saldo <> 0) or (mae.lincred < 1000)) " +
                   " AND c.estado = 'A' and ((g.saldo <> 0 And mae.LINCRED >= 1000) Or ((mae.LINCRED < 1000) " +
                   " And (g.saldo <> 0 Or g.cuota <> 0)))  " +
                   (Empresa == "Todas" ? "" : " AND c.empresa = '" + ("0000" + Empresa).Substring(Math.Max(0, ("0000" + Empresa).Length - 4)) + "' ") +
                   (Seccion == "Todas" ? "" : " AND c.SECCION_EMPRESA = '" + ("0000" + Seccion).Substring(Math.Max(0, ("0000" + Seccion).Length - 4)) + "' ") +
                   (Agencia == "Todas" ? "" : " AND c.agencia = '" + ("0000" + Agencia).Substring(Math.Max(0, ("0000" + Agencia).Length - 4)) + "' ") +
                   (Cencosto == "Todos" ? "" : " AND c.cencosto = '" + ("00000000" + Cencosto).Substring(Math.Max(0, ("00000000" + Cencosto).Length - 8)) + "' ") +
                   (estado == "O" ? "" : " AND c.estado = '" + estado + "' ") +
                   (TipoAso == 1 ? " AND c.clase = '5' " : (TipoAso == 2 ? "  AND c.clase = '6' " : " ")) +
                   " order by c.empresa,c.nit,salex.num_extra asc";
        }

        private void WriteESTLine(TextWriter writer, OdbcDataReader reader, string periodo)
        {
            string empresaVal = reader["empresa"].ToString();
            writer.Write((empresaVal.Length >= 2 ? empresaVal.Substring(empresaVal.Length - 2) : empresaVal) + ",");
            string codigoterVal = reader["CODIGOTER"].ToString();
            writer.Write((codigoterVal.Length >= 12 ? codigoterVal.Substring(codigoterVal.Length - 12) : codigoterVal) + ",");
            string nitVal = ("              " + reader["nit"]);
            writer.Write(nitVal.Substring(nitVal.Length - 14) + ",'");

            string nombreCompleto = reader["nombre"] + " " + reader["apellido"] + "                                       ";
            writer.Write(nombreCompleto.Substring(0, 40) + "',");

            int lincred = Convert.ToInt32(reader["LINCRED"]);
            if (lincred < 10) writer.Write("0");
            writer.Write(lincred + ",'");

            string descripcion = reader["DESCRIPCION"] + "             ";
            writer.Write(descripcion.Substring(0, 13) + "',");

            string numero = ("00000000000000000" + reader["NUMERO"]);
            writer.Write(numero.Substring(numero.Length - 14) + ",");
            writer.Write(Convert.ToDateTime(reader["FECFACT"]).ToString("yyyyMMdd") + ",");
            writer.Write(Convert.ToDateTime(reader["fecdesc"]).ToString("yyyyMMdd") + ",");

            int plazo = Convert.ToInt32(reader["plazo"]);
            if (plazo == 0)
            {
                writer.Write("999,");
            }
            else
            {
                string plazoStr = ("000" + plazo);
                writer.Write(plazoStr.Substring(plazoStr.Length - 3) + ",");
            }

            string tasa = reader["tasaint"].ToString();
            if (tasa.Length > 4) tasa = tasa.Substring(0, 4);
            tasa = tasa.Replace(".", "");
            string tasaStr = ("000000" + tasa);
            writer.Write(tasaStr.Substring(tasaStr.Length - 6) + ", ");

            string valorob = ("0000000000000" + (int)Convert.ToDouble(reader["valorob"]));
            writer.Write(valorob.Substring(valorob.Length - 12) + ",");

            WriteNumericField(writer, reader, "saldo_inicial");
            writer.Write(" 000000000000,");
            WriteNumericField(writer, reader, "capital_abono");
            WriteNumericField(writer, reader, "interes_abono");
            WriteNumericField(writer, reader, "mora_abono");
            WriteNumericField(writer, reader, "saldo");

            string cuota = ("0000000" + (int)Convert.ToDouble(reader["cuota"]));
            writer.Write(cuota.Substring(cuota.Length - 7) + ",");
            writer.Write(DateTime.Now.ToString("yyyyMMdd") + ",");
            writer.Write(DateTime.Now.ToString("MM") + ",");

            string mes;
            switch (reader["periodd"].ToString())
            {
                case "1": mes = "M"; break;
                case "2": mes = "Q"; break;
                case "3": mes = "D"; break;
                case "4": mes = "S"; break;
                case "5": mes = "d"; break;
                default: mes = "M"; break;
            }
            writer.Write(mes + ",");
            writer.Write(reader["ciclod"] + ",");

            string clase = reader["clades"].ToString() == "1" ? "N" : "C";
            writer.Write(clase + ",");

            if (reader["cuopen"] == DBNull.Value)
            {
                writer.Write("000,");
            }
            else
            {
                string cuopen = ("000" + (int)Convert.ToDouble(reader["cuopen"]));
                writer.Write(cuopen.Substring(cuopen.Length - 3) + ",");
            }

            if (reader["cuotVencidas"] == DBNull.Value)
            {
                writer.Write("000,");
            }
            else
            {
                string cuotVencidas = ("000" + Convert.ToInt32(reader["cuotVencidas"]));
                writer.Write(cuotVencidas.Substring(cuotVencidas.Length - 3) + ",");
            }

            if (reader["PendPago"] == DBNull.Value)
            {
                writer.Write(" 000000000000");
            }
            else
            {
                double pendPago = Convert.ToDouble(reader["PendPago"]);
                if (pendPago < 0)
                {
                    writer.Write("-");
                    pendPago = pendPago * -1;
                }
                else
                {
                    writer.Write(" ");
                }
                string pendPagoStr = ("000000000000" + (int)pendPago);
                writer.Write(pendPagoStr.Substring(pendPagoStr.Length - 12));
            }
            writer.WriteLine();
        }

        private void WriteNumericField(TextWriter writer, OdbcDataReader reader, string fieldName)
        {
            if (reader[fieldName] == DBNull.Value)
            {
                writer.Write(" 000000000000,");
            }
            else
            {
                double val = Convert.ToDouble(reader[fieldName]);
                if (val < 0)
                {
                    writer.Write("-");
                    val = val * -1;
                }
                else
                {
                    writer.Write(" ");
                }
                string valStr = ("000000000000" + (int)val);
                writer.Write(valStr.Substring(valStr.Length - 12) + ",");
            }
        }

        private bool WriteMOVLine(TextWriter read11, TextWriter readApor, OdbcDataReader myReader, string periodo, OdbcConnection myConect)
        {
            double saldoanterior = 0, saldoenmoraanterior = 0, causacapital = 0, causainteres = 0, pagos = 0, saldoenmora = 0, saldofinal = 0;
            double saldo_inicial = 0, saldo = 0, vlr_credito = 0;
            double SaldoCapital, SaldoInteres, SaldoMora, SaldoOtros, SaldoAdmon, SaldoSeguro, SaldoExtra;
            double Saldo_AntCapital, Saldo_AntExtra, Saldo_AntInteres, Saldo_AntMora, Saldo_AntSeguro, Saldo_AntAdmon, Saldo_AntOtros;
            double Capital_causado, Extra_causado, Interes_causado, Mora_causado, Seguro_causado, Admon_causado, Otros_causado;
            bool OtraLinea = true;
            string grupo = "";

            SaldoCapital = myReader["SaldoCapital"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["SaldoCapital"]);
            SaldoInteres = myReader["SaldoInteres"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["SaldoInteres"]);
            SaldoMora = myReader["SaldoMora"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["SaldoMora"]);
            SaldoOtros = myReader["SaldoOtros"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["SaldoOtros"]);
            SaldoAdmon = myReader["SaldoAdmon"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["SaldoAdmon"]);
            SaldoSeguro = myReader["SaldoSeguro"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["SaldoSeguro"]);
            SaldoExtra = myReader["SaldoExtra"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["SaldoExtra"]);
            vlr_credito = myReader["vlr_credito"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["vlr_credito"]);

            Saldo_AntCapital = myReader["Saldo_AntCapital"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Saldo_AntCapital"]);
            Saldo_AntExtra = myReader["Saldo_AntExtra"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Saldo_AntExtra"]);
            Saldo_AntInteres = myReader["Saldo_AntInteres"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Saldo_AntInteres"]);
            Saldo_AntMora = myReader["Saldo_AntMora"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Saldo_AntMora"]);
            Saldo_AntSeguro = myReader["Saldo_AntSeguro"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Saldo_AntSeguro"]);
            Saldo_AntAdmon = myReader["Saldo_AntAdmon"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Saldo_AntAdmon"]);
            Saldo_AntOtros = myReader["Saldo_AntOtros"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Saldo_AntOtros"]);

            Capital_causado = myReader["Capital_causado"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Capital_causado"]);
            Extra_causado = myReader["Extra_causado"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Extra_causado"]);
            Interes_causado = myReader["Interes_causado"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Interes_causado"]);
            Mora_causado = myReader["Mora_causado"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Mora_causado"]);
            Seguro_causado = myReader["Seguro_causado"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Seguro_causado"]);
            Admon_causado = myReader["Admon_causado"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Admon_causado"]);
            Otros_causado = myReader["Otros_causado"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["Otros_causado"]);
            saldo_inicial = myReader["saldo_inicial"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["saldo_inicial"]);
            saldo = myReader["saldo"] == DBNull.Value ? 0 : Convert.ToDouble(myReader["saldo"]);

            int lincred = Convert.ToInt32(myReader["LINCRED"]);
            if (lincred >= 1000)
            {
                grupo = "2";
            }
            else if (lincred < 1000)
            {
                if (myReader["CODAHOR"].ToString() == "3")
                {
                    grupo = "3";
                }
            }
            else
            {
                grupo = "1";
            }

            string descripcion = myReader["descripcion"].ToString();
            string VERSALDO = myReader["VERSALDO"].ToString();

            if (VERSALDO == "Y" && lincred < 1000)
            {
                saldoanterior = saldo_inicial * -1;
            }
            else if (VERSALDO == "Y" && lincred > 999)
            {
                saldoanterior = saldo_inicial;
            }

            if (VERSALDO == "Y")
            {
                saldoenmoraanterior = Saldo_AntCapital + Saldo_AntExtra + Saldo_AntInteres + Saldo_AntMora + Saldo_AntSeguro + Saldo_AntAdmon + Saldo_AntOtros;
                causacapital = Capital_causado;
                causainteres = Extra_causado + Interes_causado + Mora_causado + Seguro_causado + Admon_causado + Otros_causado;
            }

            string anulado = myReader["anulado"].ToString();
            string restri_tesoreria = myReader["restri_tesoreria"].ToString();
            string CODAHOR = myReader["CODAHOR"].ToString();
            string tipo_movto = myReader["tipo_movto"].ToString();

            if (anulado != "Y" && restri_tesoreria != "Y")
            {
                if (grupo == "2")
                {
                    if ((CODAHOR == "4" || CODAHOR == "5" || CODAHOR == "3") && (tipo_movto == "1" || tipo_movto == "10"))
                    {
                        pagos = vlr_credito;
                    }
                }
                else if ((CODAHOR == "1" || CODAHOR == "2") && (tipo_movto == "7" || tipo_movto == "4" || tipo_movto == "1"))
                {
                    pagos = vlr_credito;
                }
                else if ((CODAHOR == "6" || CODAHOR == "7") && tipo_movto == "11")
                {
                    pagos = vlr_credito;
                }
                else if (CODAHOR == "3" && tipo_movto == "1")
                {
                    pagos = vlr_credito;
                }
            }

            if (anulado != "Y" && restri_tesoreria != "Y")
            {
                if ((CODAHOR == "4" || CODAHOR == "5") && tipo_movto == "2")
                {
                    pagos += vlr_credito;
                }
                else if ((CODAHOR == "6" || CODAHOR == "7") && tipo_movto == "12")
                {
                    pagos += vlr_credito;
                }
                else if ((CODAHOR == "1" || CODAHOR == "2") && tipo_movto == "2")
                {
                    pagos += vlr_credito;
                }
                else if (CODAHOR == "3" && tipo_movto == "2")
                {
                    pagos += vlr_credito;
                }
            }

            if (anulado != "Y" && restri_tesoreria != "Y")
            {
                if ((CODAHOR == "4" || CODAHOR == "5") && tipo_movto == "3")
                {
                    pagos += vlr_credito;
                }
            }

            if (anulado != "Y" && restri_tesoreria != "Y")
            {
                if (tipo_movto == "5" || tipo_movto == "6" || tipo_movto == "9")
                {
                    pagos += vlr_credito;
                }
            }

            if (VERSALDO == "Y")
            {
                saldoenmora = SaldoCapital + SaldoInteres + SaldoMora + SaldoOtros + SaldoAdmon + SaldoSeguro + SaldoExtra;
            }

            if (VERSALDO == "Y" && lincred < 1000)
            {
                saldofinal = saldo * -1;
            }
            else if (VERSALDO == "Y" && lincred > 999)
            {
                saldofinal = saldo;
            }

            double cuota = Convert.ToDouble(myReader["CUOTA"]);
            if ((saldo != 0 && lincred >= 1000) || (lincred < 1000 && (saldo != 0 || cuota != 0)) || ((lincred >= 1000 || lincred < 1000) && saldo == 0 && saldo_inicial != 0))
            {
                if (cop_tempmovextra(myReader["codigoter"].ToString(), lincred, Convert.ToDouble(myReader["numero"]), descripcion, saldoanterior, saldoenmoraanterior, causacapital, causainteres, pagos, saldoenmora, saldofinal, myReader["nit"].ToString(), myConect) == 2)
                {
                    OtraLinea = false;
                }
                else
                {
                    OtraLinea = true;
                }

                if (OtraLinea && readApor != null)
                {
                    readApor.Write(myReader["LINCRED"] + ",");
                    readApor.Write(myReader["NUMERO"] + ",");
                    readApor.Write(descripcion + ",");
                    readApor.Write(saldoanterior + ",");
                    readApor.Write(myReader["FECFACT"] + ",");
                    readApor.Write(myReader["fecdesc"] + ",");
                    readApor.Write(myReader["plazo"] + ",");
                    readApor.Write(myReader["tasaint"] + ",");
                    readApor.Write(myReader["FECVEMTO"] + ",");
                    readApor.Write(myReader["valorob"] + ",");
                    readApor.Write(myReader["SALDO"] + ",");
                    readApor.Write(myReader["CUOTA"] + ",");
                    readApor.Write(myReader["cuopen"] + ",");

                    switch (myReader["periodd"].ToString())
                    {
                        case "1": readApor.Write("Mensual,"); break;
                        case "2": readApor.Write("Quincenal,"); break;
                        case "3": readApor.Write("Decadal,"); break;
                        case "4": readApor.Write("Semanal,"); break;
                        case "5": readApor.Write("Diaria,"); break;
                        default: readApor.Write("Mensual,"); break;
                    }

                    readApor.Write(myReader["nit"] + ",");

                    switch (CODAHOR)
                    {
                        case "1":
                        case "2":
                            readApor.WriteLine("A");
                            break;
                        case "3":
                            readApor.WriteLine("S");
                            break;
                        case "4":
                        case "5":
                            readApor.WriteLine("C");
                            break;
                        default:
                            readApor.WriteLine("S");
                            break;
                    }
                }
            }
            else
            {
                OtraLinea = false;
            }

            return OtraLinea;
        }

        private void WriteExtrasLine(TextWriter writer, OdbcDataReader reader, string feec)
        {
            if (feec.Trim() == "MOV")
            {
                writer.Write(reader["nit"] + ",");
                writer.Write(reader["LINCRED"] + ",");
                writer.Write(reader["NUMERO"] + ",");
                writer.Write(Convert.ToInt64(Convert.ToDouble(reader["valor"])).ToString() + ",");
                writer.Write(Convert.ToDateTime(reader["fecha_pago"]).ToString("dd/MM/yyyy") + ",");
                switch (reader["tipoextra"].ToString())
                {
                    case "P": writer.WriteLine("Prima"); break;
                    case "V": writer.WriteLine("Vacaciones"); break;
                    case "C": writer.WriteLine("Cesantias"); break;
                    case "Q": writer.WriteLine("Quincena"); break;
                    default: writer.WriteLine("Prima"); break;
                }
            }
            else
            {
                string empresaVal = reader["empresa"].ToString();
                writer.Write((empresaVal.Length >= 2 ? empresaVal.Substring(empresaVal.Length - 2) : empresaVal) + ",");
                writer.Write(reader["nit"] + ",");
                int lincred = Convert.ToInt32(reader["LINCRED"]);
                if (lincred < 10) writer.Write("0");
                writer.Write(lincred + ",");
                string numero = ("00000000000000000" + reader["NUMERO"]);
                writer.Write(numero.Substring(numero.Length - 14) + ",");
                int numExtra = Convert.ToInt32(reader["NUM_EXTRA"]);
                if (numExtra < 10) writer.Write("0");
                writer.Write(numExtra + ", ");
                string saldoStr = ("0000000000000000" + (int)Convert.ToDouble(reader["SALDO"]));
                writer.Write(saldoStr.Substring(saldoStr.Length - 13) + ",");
                writer.Write(Convert.ToDateTime(reader["fecha_pago"]).ToString("yyyyMMdd") + ",");
                writer.Write(reader["forma_pago"] + ",");
                writer.Write(reader["tipoextra"]);
                writer.WriteLine();
            }
        }

        private void WriteAsociadosLine(TextWriter writer, OdbcDataReader reader)
        {
            writer.Write(reader["CODIGOTER"] + ",");
            writer.Write(reader["apeaso"] + " " + reader["nomaso"] + ",");
            writer.Write(reader["nit"] + ",");
            writer.Write(reader["direccion"] + ",");
            writer.Write(reader["telefono1"] + ",");
            writer.Write(reader["fecha_ingreso"] + ",");
            writer.Write(reader["nomempresa"] + ",");
            writer.Write(reader["nombre_ciudad"] + ",");
            writer.Write(reader["sgencia"] + ",");
            writer.Write(reader["nomseccion"] + ",");

            switch (reader["estado"].ToString())
            {
                case "A": writer.Write("Activo,"); break;
                case "R": writer.Write("Retirado,"); break;
                case "T": writer.Write("Trasladado,"); break;
                case "S": writer.Write("Suspendido,"); break;
                default: writer.Write("Activo,"); break;
            }
            writer.WriteLine(DateTime.Now.ToString("dd/MM/yyyy"));
        }

        public int cop_tempmovextra(string codigoter, int lincred, double numero, string descripcion, double saldoanterior, double saldoenmoraanterior,
            double causacapital, double causainteres, double pagos, double saldoenmora, double saldofinal, string nit, OdbcConnection myConect)
        {
            string sql = "select nit from cop_tempmovextra where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            int Retorno = 1;

            if (msgconec.ExecuteQueryconec(sql, myConect, "") == false)
            {
                sql = "insert into cop_tempmovextra(codigoter, lincred, numero, descripcion, saldoanterior, saldoenmoraanterior, causacapital, causainteres, pagos, saldoenmora, saldofinal,nit) " +
                "values('" + codigoter + "'," + lincred + "," + numero + ",'" + descripcion + "'," + saldoanterior + "," + saldoenmoraanterior +
                "," + causacapital + "," + causainteres + "," + pagos + "," + saldoenmora + "," + saldofinal + ",'" + nit + "')";
            }
            else
            {
                sql = "update cop_tempmovextra set pagos = pagos + " + pagos + " where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
                Retorno = 2;
            }
            msgconec.ExecuteQueryconec(sql, myConect, "");

            return Retorno;
        }

        ~clsmscext02()
        {
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
#endif
