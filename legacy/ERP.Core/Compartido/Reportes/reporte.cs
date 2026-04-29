#if CRYSTAL_LEGACY
using System;
using System.Windows.Forms;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;

namespace ERP.Core.Compartido.Reportes
{
    public class reporte : CrystalDecisions.CrystalReports.Engine.ReportDocument
    {
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private System.ComponentModel.IContainer components;

        public reporte(string nombre, bool reemplazaBD = true)
        {
            try
            {
                MyOdbcConet.MyOdbcConect(ref varini);
                MyOdbcConet.LlenarVarini(ref varini);
                string dire;
                if (varini.pstUndred == "")
                {
                    throw new Exception("No se ha especificado una unidad de red para los reportes o no  es valida.");
                }
                dire = varini.pstUndred + @":\repor\";

                if (System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(dire + nombre + ".rpt"), System.IO.Path.GetFileName(dire + nombre + ".rpt")).Length == 0)
                {
                    throw new Exception(nombre + ".rpt No existe." + "\r\n" + "\r\n" + "Posibles Causas:" + "\r\n" + "No se ha especificado una unidad de red para los reportes o no  es valida.");
                }

                this.Load(dire + nombre + ".rpt", CrystalDecisions.Shared.OpenReportMethod.OpenReportByTempCopy);
            }
            catch (Exception ex)
            {
                throw new Exception(nombre + ".rpt" + "\r\n" + "\r\n" + ex.Message);
            }

            CrystalDecisions.Shared.ConnectionInfo coninf = new CrystalDecisions.Shared.ConnectionInfo();
            if (reemplazaBD == true)
            {
                coninf.DatabaseName = varini.pstBdatos;
            }
            coninf.IntegratedSecurity = false;
            coninf.UserID = varini.pstUID;
            coninf.Password = varini.pstPascon;
            coninf.Type = CrystalDecisions.Shared.ConnectionInfoType.CRQE;
            coninf.UType = CrystalDecisions.Shared.ConnectionInfoType.CRQE;
            if (varini.pstTipoBD.ToUpper() == "DB2")
            {
                coninf.ServerName = varini.pstDNS;
            }
            else
            {
                coninf.ServerName = "MySql";
            }

            if (reemplazaBD == true)
            {
                foreach (ReportDocument crSubReport in this.Subreports)
                {
                    Database crDatabase1 = crSubReport.Database;
                    Tables crTables1 = crDatabase1.Tables;
                    foreach (Table crTable in crTables1)
                    {
                        TableLogOnInfo crTableLogOnInfo = crTable.LogOnInfo;

                        if ((string)crTableLogOnInfo.ConnectionInfo.Attributes.Collection.Lookup("Database DLL") == "crdb_odbc.dll")
                        {
                            crTableLogOnInfo.ConnectionInfo = coninf;
                            crTable.ApplyLogOnInfo(crTableLogOnInfo);

                            if (varini.pstTipoBD.ToUpper() == "ORACLE")
                            {
                                if (crTable.Location.ToUpper().Contains("COMAN") == false)
                                {
                                    crTable.Location = crTableLogOnInfo.TableName.ToUpper();
                                }
                            }
                        }
                    }
                    try
                    {
                        crSubReport.VerifyDatabase();
                    }
                    catch (Exception)
                    {
                    }
                }

                Database crDatabase2 = this.Database;
                Tables crTables2 = crDatabase2.Tables;
                foreach (Table crTable in crTables2)
                {
                    TableLogOnInfo crTableLogOnInfo = crTable.LogOnInfo;

                    if ((string)crTableLogOnInfo.ConnectionInfo.Attributes.Collection.Lookup("Database DLL") == "crdb_odbc.dll")
                    {
                        crTableLogOnInfo.ConnectionInfo = coninf;
                        crTable.ApplyLogOnInfo(crTableLogOnInfo);

                        if (varini.pstTipoBD.ToUpper() == "ORACLE")
                        {
                            if (crTable.Location.ToUpper().Contains("COMAN") == false)
                            {
                                crTable.Location = crTableLogOnInfo.TableName.ToUpper();
                            }
                        }
                    }
                }
            }

            try
            {
                this.VerifyDatabase();
            }
            catch (Exception)
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}

#else
// ==========================================================================
// STUB: reporte sin Crystal Reports
// Permite que el codigo que referencia "new reporte(...)" compile.
// Los metodos lanzan NotImplementedException hasta que se implemente
// un motor de reportes alternativo (RDLC, FastReport, etc.)
// ==========================================================================
using System;
using System.Data;

namespace ERP.Core.Compartido.Reportes
{
    /// <summary>
    /// Stub de opciones de impresion para compatibilidad sin Crystal Reports.
    /// </summary>
    public class ReportPrintOptions
    {
        public string PrinterName { get; set; } = "";
        public int PaperSize { get; set; }
        public object PaperOrientation { get; set; } = 0;
        public void ApplyPageMargins(object margins) { }
    }

    /// <summary>
    /// Stub de reporte para compilacion sin Crystal Reports.
    /// Implementar con motor de reportes alternativo.
    /// </summary>
    public class reporte : IDisposable
    {
        private string _nombre;
        private bool _reemplazaBD;

        public reporte(string nombre, bool reemplazaBD = true)
        {
            _nombre = nombre;
            _reemplazaBD = reemplazaBD;
            // TODO: Cargar reporte con motor alternativo
        }

        /// <summary>
        /// Establece el valor de un parametro del reporte.
        /// </summary>
        public void SetParameterValue(string nombre, object valor)
        {
            // TODO: Implementar con nuevo motor de reportes
        }

        /// <summary>
        /// Establece el DataSource del reporte.
        /// </summary>
        public void SetDataSource(DataSet dataSet)
        {
            // TODO: Implementar con nuevo motor de reportes
        }

        /// <summary>
        /// Establece el DataSource del reporte desde un DataTable.
        /// </summary>
        public void SetDataSource(DataTable dataTable)
        {
            // TODO: Implementar con nuevo motor de reportes
        }

        /// <summary>
        /// Exporta el reporte a disco en el formato indicado.
        /// </summary>
        public void ExportToDisk(int formatType, string fileName)
        {
            // TODO: Implementar con nuevo motor de reportes
        }

        /// <summary>
        /// Imprime el reporte directamente a la impresora.
        /// </summary>
        public void PrintToPrinter(int nCopies, bool collated, int startPage, int endPage)
        {
            // TODO: Implementar con nuevo motor de reportes
        }

        /// <summary>
        /// Opciones de impresion del reporte (stub).
        /// </summary>
        public ReportPrintOptions PrintOptions { get; } = new ReportPrintOptions();

        /// <summary>
        /// Objeto fuente del reporte para binding a visor.
        /// </summary>
        public object ReportSource => this;

        /// <summary>
        /// Refresca los datos del reporte.
        /// </summary>
        public void Refresh()
        {
            // TODO: Implementar con nuevo motor de reportes
        }

        /// <summary>
        /// Cierra el reporte y libera recursos.
        /// </summary>
        public void Close()
        {
            // No-op en stub
        }

        public void Dispose()
        {
            // No-op en stub
        }

        protected virtual void Dispose(bool disposing)
        {
            // No-op en stub
        }
    }
}
#endif
