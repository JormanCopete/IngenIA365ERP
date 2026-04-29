using ERP.Core.Compartido.Controles;
using ERP.Core.Compartido.Forms;
using ERP.Core.Compartido.Utilidades;
using System;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Forms
{
    public partial class imprimir : Form
    {
        private Barraprogress t;
        private int con = 0;
#if CRYSTAL_LEGACY
        public CrystalDecisions.CrystalReports.Engine.ReportDocument reporte1;
#else
        public object reporte1;
#endif
        private int con1 = 0;

        public imprimir()
        {
            InitializeComponent();
            t = new Barraprogress(this.Text, this, ProgressBarStyle.Marquee);
        }

        private void imprimir_Activated(object sender, EventArgs e)
        {
        }

        private void imprimir_FormClosing(object sender, FormClosingEventArgs e)
        {
#if CRYSTAL_LEGACY
            reporte1 = (CrystalDecisions.CrystalReports.Engine.ReportDocument)this.CrystalReportViewer1.ReportSource;
            reporte1.Dispose();
#endif
        }

        private void imprimir_Load(object sender, EventArgs e)
        {
            this.Top = 0;
            this.Left = 0;
            this.Height = Screen.PrimaryScreen.WorkingArea.Height;
            this.Width = Screen.PrimaryScreen.WorkingArea.Width;
            try
            {
                if (con == 0)
                {
                    t.Titulo(this.Text);
                    t.Show();
                    con += 2;
                    Timer1.Enabled = true;
                }
            }
            catch (Exception)
            {
            }
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                if (Timer1.Enabled == true)
                {
                    Application.DoEvents();
#if CRYSTAL_LEGACY
                    if (this.CrystalReportViewer1.GetCurrentPageNumber() > 0)
                    {
                        Timer1.Enabled = false;
                        t.Close();
                    }
#else
                    Timer1.Enabled = false;
                    t.Close();
#endif
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ToolStripMenuItem2_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.ShowNextPage();
#endif
        }

        private void ToolStripMenuItem3_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.ShowPreviousPage();
#endif
        }

        private void ToolStripMenuItem4_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.ExportReport();
#endif
        }

        private void ToolStripMenuItem5_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.PrintReport();
#endif
        }

        private void ToolStripMenuItem6_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SiguienteToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.ShowNextPage();
#endif
        }

        private void AnteriorToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.ShowPreviousPage();
#endif
        }

        private void ExportarToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.ExportReport();
#endif
        }

        private void ImprimirToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.PrintReport();
#endif
        }
    }
}
