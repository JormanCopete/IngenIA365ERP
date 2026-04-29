namespace ERP.Core.Compartido.Forms
{
    partial class imprimir
    {
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.MenuStrip MenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem ArchivoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItem3;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItem4;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItem5;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItem6;
        private System.Windows.Forms.Timer Timer1;
        private System.Windows.Forms.ContextMenuStrip menureport;
        private System.Windows.Forms.ToolStripMenuItem SiguienteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem AnteriorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ExportarToolStripMenuItem;
#if CRYSTAL_LEGACY
        public CrystalDecisions.Windows.Forms.CrystalReportViewer CrystalReportViewer1;
#else
        public object CrystalReportViewer1;
#endif
        private System.Windows.Forms.ToolStripMenuItem ImprimirToolStripMenuItem;

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
            this.components = new System.ComponentModel.Container();
            this.menureport = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.SiguienteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.AnteriorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ExportarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ImprimirToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuStrip1 = new System.Windows.Forms.MenuStrip();
            this.ArchivoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItem3 = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItem5 = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItem6 = new System.Windows.Forms.ToolStripMenuItem();
            this.Timer1 = new System.Windows.Forms.Timer(this.components);
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1 = new CrystalDecisions.Windows.Forms.CrystalReportViewer();
#else
            this.CrystalReportViewer1 = new object();
#endif
            this.menureport.SuspendLayout();
            this.MenuStrip1.SuspendLayout();
            this.SuspendLayout();
            //
            // menureport
            //
            this.menureport.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.SiguienteToolStripMenuItem, this.AnteriorToolStripMenuItem, this.ExportarToolStripMenuItem, this.ImprimirToolStripMenuItem });
            this.menureport.Name = "menureport";
            this.menureport.Size = new System.Drawing.Size(147, 92);
            //
            // SiguienteToolStripMenuItem
            //
            this.SiguienteToolStripMenuItem.Name = "SiguienteToolStripMenuItem";
            this.SiguienteToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.SiguienteToolStripMenuItem.Text = "Siguiente     >";
            this.SiguienteToolStripMenuItem.Click += new System.EventHandler(this.SiguienteToolStripMenuItem_Click);
            //
            // AnteriorToolStripMenuItem
            //
            this.AnteriorToolStripMenuItem.Name = "AnteriorToolStripMenuItem";
            this.AnteriorToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.AnteriorToolStripMenuItem.Text = "Anterior      <";
            this.AnteriorToolStripMenuItem.Click += new System.EventHandler(this.AnteriorToolStripMenuItem_Click);
            //
            // ExportarToolStripMenuItem
            //
            this.ExportarToolStripMenuItem.Name = "ExportarToolStripMenuItem";
            this.ExportarToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.ExportarToolStripMenuItem.Text = "Exportar...";
            this.ExportarToolStripMenuItem.Click += new System.EventHandler(this.ExportarToolStripMenuItem_Click);
            //
            // ImprimirToolStripMenuItem
            //
            this.ImprimirToolStripMenuItem.Name = "ImprimirToolStripMenuItem";
            this.ImprimirToolStripMenuItem.Size = new System.Drawing.Size(146, 22);
            this.ImprimirToolStripMenuItem.Text = "Imprimir...";
            this.ImprimirToolStripMenuItem.Click += new System.EventHandler(this.ImprimirToolStripMenuItem_Click);
            //
            // MenuStrip1
            //
            this.MenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.ArchivoToolStripMenuItem });
            this.MenuStrip1.Location = new System.Drawing.Point(0, 0);
            this.MenuStrip1.Name = "MenuStrip1";
            this.MenuStrip1.Size = new System.Drawing.Size(806, 24);
            this.MenuStrip1.TabIndex = 1;
            this.MenuStrip1.Text = "MenuStrip1";
            //
            // ArchivoToolStripMenuItem
            //
            this.ArchivoToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.ToolStripMenuItem2, this.ToolStripMenuItem3, this.ToolStripMenuItem4, this.ToolStripMenuItem5, this.ToolStripMenuItem6 });
            this.ArchivoToolStripMenuItem.Name = "ArchivoToolStripMenuItem";
            this.ArchivoToolStripMenuItem.Size = new System.Drawing.Size(60, 20);
            this.ArchivoToolStripMenuItem.Text = "Archivo";
            //
            // ToolStripMenuItem2-6 (Siguiente, Anterior, Exportar, Imprimir, Salir)
            //
            this.ToolStripMenuItem2.Name = "ToolStripMenuItem2";
            this.ToolStripMenuItem2.Size = new System.Drawing.Size(146, 22);
            this.ToolStripMenuItem2.Text = "Siguiente     >";
            this.ToolStripMenuItem2.Click += new System.EventHandler(this.ToolStripMenuItem2_Click);
            this.ToolStripMenuItem3.Name = "ToolStripMenuItem3";
            this.ToolStripMenuItem3.Size = new System.Drawing.Size(146, 22);
            this.ToolStripMenuItem3.Text = "Anterior      <";
            this.ToolStripMenuItem3.Click += new System.EventHandler(this.ToolStripMenuItem3_Click);
            this.ToolStripMenuItem4.Name = "ToolStripMenuItem4";
            this.ToolStripMenuItem4.Size = new System.Drawing.Size(146, 22);
            this.ToolStripMenuItem4.Text = "Exportar...";
            this.ToolStripMenuItem4.Click += new System.EventHandler(this.ToolStripMenuItem4_Click);
            this.ToolStripMenuItem5.Name = "ToolStripMenuItem5";
            this.ToolStripMenuItem5.Size = new System.Drawing.Size(146, 22);
            this.ToolStripMenuItem5.Text = "Imprimir...";
            this.ToolStripMenuItem5.Click += new System.EventHandler(this.ToolStripMenuItem5_Click);
            this.ToolStripMenuItem6.Name = "ToolStripMenuItem6";
            this.ToolStripMenuItem6.ShortcutKeys = (System.Windows.Forms.Keys)(System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4);
            this.ToolStripMenuItem6.Size = new System.Drawing.Size(146, 22);
            this.ToolStripMenuItem6.Text = "Salir";
            this.ToolStripMenuItem6.Click += new System.EventHandler(this.ToolStripMenuItem6_Click);
            //
            // Timer1
            //
            this.Timer1.Enabled = true;
            this.Timer1.Interval = 1500;
            this.Timer1.Tick += new System.EventHandler(this.Timer1_Tick);
            //
            // CrystalReportViewer1
            //
#if CRYSTAL_LEGACY
            this.CrystalReportViewer1.ActiveViewIndex = -1;
            this.CrystalReportViewer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.CrystalReportViewer1.ContextMenuStrip = this.menureport;
            this.CrystalReportViewer1.DisplayGroupTree = false;
            this.CrystalReportViewer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CrystalReportViewer1.Location = new System.Drawing.Point(0, 24);
            this.CrystalReportViewer1.Name = "CrystalReportViewer1";
            this.CrystalReportViewer1.SelectionFormula = "";
            this.CrystalReportViewer1.ShowGroupTreeButton = false;
            this.CrystalReportViewer1.ShowRefreshButton = false;
            this.CrystalReportViewer1.Size = new System.Drawing.Size(806, 621);
            this.CrystalReportViewer1.TabIndex = 2;
            this.CrystalReportViewer1.ViewTimeSelectionFormula = "";
#endif
            //
            // imprimir
            //
            this.AutoScaleBaseSize = new System.Drawing.Size(6, 15);
            this.ClientSize = new System.Drawing.Size(806, 645);
#if CRYSTAL_LEGACY
            this.Controls.Add((System.Windows.Forms.Control)this.CrystalReportViewer1);
#endif
            this.Controls.Add(this.MenuStrip1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MainMenuStrip = this.MenuStrip1;
            this.MaximizeBox = false;
            this.Name = "imprimir";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Imprimir";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Activated += new System.EventHandler(this.imprimir_Activated);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.imprimir_FormClosing);
            this.Load += new System.EventHandler(this.imprimir_Load);
            this.menureport.ResumeLayout(false);
            this.MenuStrip1.ResumeLayout(false);
            this.MenuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
