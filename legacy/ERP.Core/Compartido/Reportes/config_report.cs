using ERP.Core.Compartido.Forms;
using ERP.Core.Compartido.Interfaces;
using System;
using System.Windows.Forms;
using System.IO;

namespace ERP.Core.Compartido.Reportes
{
#if CRYSTAL_LEGACY
    using CrystalDecisions.CrystalReports.Engine;
    using CrystalDecisions.Shared;

    public static class variable
    {
        public static ReportDocument reporte;
        public static imprimir impri_report = new imprimir();
        public static string Titulo = "Imprimiendo";
    }

    public class config_report : IReportService
    {
        private ERP.Core.Compartido.Datos.ClsConect ClsConect = new ERP.Core.Compartido.Datos.ClsConect();

        public void confi_reportes(Form Pertenese, ReportDocument r, bool Dialog = false, bool Inmediato = false)
        {
            variable.impri_report = new imprimir();
            variable.reporte = new ReportDocument();
            variable.reporte = r;
            variable.Titulo = Pertenese.Text;

            vista_report1 v = new vista_report1();
            FormCollection fc;

            fc = Application.OpenForms;
            for (int i = 0; i < fc.Count; i++)
            {
                if (fc[i].Owner != null)
                {
                    if (fc[i].Name == v.Name && fc[i].Owner.Name == Pertenese.Name)
                    {
                        return;
                    }
                }
            }
            if (Pertenese.Opacity > (double)0.1)
            {
                v.Owner = Pertenese;
            }
            v.ShowInTaskbar = false;
            if (Dialog == false)
            {
                v.Show();
                if (ClsConect.odbcConect.reporteinmediato == true)
                {
                    v.ok_Click(null, null);
                }
            }
            else
            {
                v.Show();
                if (ClsConect.odbcConect.reporteinmediato == true)
                {
                    v.ok_Click(null, null);
                }
            }
        }

        // --- IReportService implementation ---

        public void MostrarReporte(Form owner, object reportSource, bool dialog = false, bool inmediato = false)
        {
            confi_reportes(owner, (ReportDocument)reportSource, dialog, inmediato);
        }

        public void ExportarADisco(object reportSource, string rutaArchivo, string formato)
        {
            var report = (ReportDocument)reportSource;
            var formatType = formato.ToUpper() switch
            {
                "PDF" => ExportFormatType.PortableDocFormat,
                "EXCEL" => ExportFormatType.Excel,
                "WORD" => ExportFormatType.WordForWindows,
                "RTF" => ExportFormatType.RichText,
                _ => ExportFormatType.PortableDocFormat
            };
            report.ExportToDisk(formatType, rutaArchivo);
        }

        public void VistaPrevia(Form owner, object reportSource, string titulo)
        {
            variable.impri_report = new imprimir();
            variable.reporte = (ReportDocument)reportSource;
            variable.Titulo = titulo;
            variable.impri_report.CrystalReportViewer1.ReportSource = variable.reporte;
            variable.impri_report.Owner = owner;
            variable.impri_report.Text = "Imprimir " + titulo;
            variable.impri_report.Show();
        }

        public object CargarReporte(string nombreReporte, bool reemplazaBD = true)
        {
            return new reporte(nombreReporte, reemplazaBD);
        }
    }

    public class vista_report1 : System.Windows.Forms.Form
    {
        private string stArchivo;

        #region Windows Form Designer generated code

        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.Button ok;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.TextBox archivo;
        private System.Windows.Forms.Label Label8;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.ComboBox tip_archivo;
        private System.Windows.Forms.CheckBox imprime;
        private System.Windows.Forms.Button config;
        private System.Windows.Forms.Label Label1;

        public vista_report1()
        {
            InitializeComponent();
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

        private void InitializeComponent()
        {
            this.ok = new System.Windows.Forms.Button();
            this.cancel = new System.Windows.Forms.Button();
            this.archivo = new System.Windows.Forms.TextBox();
            this.Label8 = new System.Windows.Forms.Label();
            this.Label6 = new System.Windows.Forms.Label();
            this.tip_archivo = new System.Windows.Forms.ComboBox();
            this.imprime = new System.Windows.Forms.CheckBox();
            this.config = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            this.ok.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right));
            this.ok.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.ok.Location = new System.Drawing.Point(131, 190);
            this.ok.Name = "ok";
            this.ok.Size = new System.Drawing.Size(104, 32);
            this.ok.TabIndex = 280;
            this.ok.Tag = "";
            this.ok.Text = "Aceptar";
            this.ok.Click += new System.EventHandler(this.ok_Click);
            this.cancel.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right));
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cancel.Location = new System.Drawing.Point(251, 190);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(104, 32);
            this.cancel.TabIndex = 281;
            this.cancel.Text = "Cancelar";
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            this.archivo.Location = new System.Drawing.Point(120, 80);
            this.archivo.MaxLength = 15;
            this.archivo.Name = "archivo";
            this.archivo.Size = new System.Drawing.Size(104, 20);
            this.archivo.TabIndex = 297;
            this.archivo.Text = "";
            this.Label8.Location = new System.Drawing.Point(24, 80);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(88, 16);
            this.Label8.TabIndex = 300;
            this.Label8.Text = "Nombre Archivo";
            this.Label6.Location = new System.Drawing.Point(24, 40);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(104, 23);
            this.Label6.TabIndex = 299;
            this.Label6.Text = "Exportar a Archivo";
            this.tip_archivo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.tip_archivo.Items.AddRange(new object[] { "", "Excel", "PDF", "Word", "Rtf", "" });
            this.tip_archivo.Location = new System.Drawing.Point(120, 40);
            this.tip_archivo.Name = "tip_archivo";
            this.tip_archivo.Size = new System.Drawing.Size(112, 21);
            this.tip_archivo.TabIndex = 296;
            this.imprime.Location = new System.Drawing.Point(24, 120);
            this.imprime.Name = "imprime";
            this.imprime.FlatStyle = FlatStyle.System;
            this.imprime.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.imprime.Size = new System.Drawing.Size(112, 16);
            this.imprime.TabIndex = 298;
            this.imprime.Text = "Ver Reporte";
            this.imprime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.imprime.Checked = true;
            this.config.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.config.Location = new System.Drawing.Point(112, 160);
            this.config.Name = "config";
            this.config.Size = new System.Drawing.Size(32, 24);
            this.config.TabIndex = 301;
            this.config.Text = "...";
            this.config.Visible = false;
            this.config.Click += new System.EventHandler(this.config_Click);
            this.Label1.Location = new System.Drawing.Point(16, 168);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(104, 16);
            this.Label1.TabIndex = 302;
            this.Label1.Text = "Configurar  pagina";
            this.Label1.Visible = false;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(362, 229);
            this.Controls.Add(this.config);
            this.Controls.Add(this.archivo);
            this.Controls.Add(this.Label8);
            this.Controls.Add(this.tip_archivo);
            this.Controls.Add(this.imprime);
            this.Controls.Add(this.ok);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.Label6);
            this.Controls.Add(this.Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "vista_report";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Opciones del reporte";
            this.ResumeLayout(false);
        }

        #endregion

        public void ok_Click(object sender, System.EventArgs e)
        {
            try
            {
                stArchivo = this.archivo.Text.Trim();
                if (stArchivo != "" && this.tip_archivo.Text != "")
                {
                    if (this.tip_archivo.Text.Substring(0, 1) == "P")
                    {
                        stArchivo = stArchivo + ".pdf";
                    }
                    else if (this.tip_archivo.Text.Substring(0, 1) == "E")
                    {
                        stArchivo = stArchivo + ".xls";
                    }
                    else if (this.tip_archivo.Text.Substring(0, 1) == "W")
                    {
                        stArchivo = stArchivo + ".doc";
                    }
                    else if (this.tip_archivo.Text.Substring(0, 1) == "T")
                    {
                        stArchivo = stArchivo + ".rtf";
                    }
                    try
                    {
                        Directory.CreateDirectory(Application.StartupPath + @"\rpt\");
                    }
                    catch (Exception)
                    {
                    }
                    stArchivo = Application.StartupPath + @"\rpt\" + stArchivo;

                    if (this.tip_archivo.Text.Substring(0, 1) == "P")
                    {
                        variable.reporte.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat, stArchivo);
                    }
                    else if (this.tip_archivo.Text.Substring(0, 1) == "E")
                    {
                        variable.reporte.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.Excel, stArchivo);
                    }
                    else if (this.tip_archivo.Text.Substring(0, 1) == "W")
                    {
                        variable.reporte.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.WordForWindows, stArchivo);
                    }
                    else if (this.tip_archivo.Text.Substring(0, 1) == "T")
                    {
                        variable.reporte.ExportToDisk(CrystalDecisions.Shared.ExportFormatType.RichText, stArchivo);
                    }
                }
                variable.impri_report.CrystalReportViewer1.ReportSource = variable.reporte;
                if (this.imprime.Checked == true)
                {
                    if (this.Owner != null)
                    {
                        variable.impri_report.Owner = this.Owner;
                        variable.impri_report.Text = "Imprimir " + variable.Titulo;
                    }
                    else
                    {
                        variable.impri_report.Text = "Imprimir ";
                    }
                    variable.impri_report.Show();
                    this.Close();
                }
                else
                {
                    if (this.archivo.Text.Trim() != "" && this.tip_archivo.Text != "")
                    {
                        MessageBox.Show("El archivo se genero correctamente en : '" + stArchivo + "'", "MsgSas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MsgSas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
        }

        private void config_Click(object sender, System.EventArgs e)
        {
        }

        private void cancel_Click(object sender, System.EventArgs e)
        {
            this.Close();
        }
    }
#else
    // Stubs cuando Crystal Reports no esta disponible.
    // Se usara IReportService con una implementacion alternativa.

    public static class variable
    {
        public static object reporte;
        public static imprimir impri_report = new imprimir();
        public static string Titulo = "Imprimiendo";
    }

    public class config_report : IReportService
    {
        public void confi_reportes(Form Pertenese, object r, bool Dialog = false, bool Inmediato = false)
        {
            throw new NotSupportedException("Crystal Reports no disponible. Use una implementacion de IReportService.");
        }

        public void MostrarReporte(Form owner, object reportSource, bool dialog = false, bool inmediato = false)
        {
            throw new NotSupportedException("Crystal Reports no disponible. Registre una implementacion de IReportService.");
        }

        public void ExportarADisco(object reportSource, string rutaArchivo, string formato)
        {
            throw new NotSupportedException("Crystal Reports no disponible. Registre una implementacion de IReportService.");
        }

        public void VistaPrevia(Form owner, object reportSource, string titulo)
        {
            throw new NotSupportedException("Crystal Reports no disponible. Registre una implementacion de IReportService.");
        }

        public object CargarReporte(string nombreReporte, bool reemplazaBD = true)
        {
            throw new NotSupportedException("Crystal Reports no disponible. Registre una implementacion de IReportService.");
        }
    }
#endif
}
