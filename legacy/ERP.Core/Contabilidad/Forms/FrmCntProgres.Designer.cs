namespace ERP.Core.Contabilidad.Forms
{
    partial class FrmCntProgres
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough]
        private void InitializeComponent()
        {
            this.Progress = new System.Windows.Forms.ProgressBar();
            this.lblTittulo = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // Progress
            //
            this.Progress.ForeColor = System.Drawing.Color.Green;
            this.Progress.Location = new System.Drawing.Point(12, 34);
            this.Progress.Name = "Progress";
            this.Progress.Size = new System.Drawing.Size(331, 18);
            this.Progress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.Progress.TabIndex = 0;
            this.Progress.UseWaitCursor = true;
            //
            // lblTittulo
            //
            this.lblTittulo.AutoSize = true;
            this.lblTittulo.Location = new System.Drawing.Point(116, 9);
            this.lblTittulo.Name = "lblTittulo";
            this.lblTittulo.Size = new System.Drawing.Size(14, 13);
            this.lblTittulo.TabIndex = 1;
            this.lblTittulo.Text = "#";
            //
            // FrmCntProgres
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(355, 76);
            this.ControlBox = false;
            this.Controls.Add(this.lblTittulo);
            this.Controls.Add(this.Progress);
            this.Name = "FrmCntProgres";
            this.Load += new System.EventHandler(this.FrmProgres_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        internal System.Windows.Forms.ProgressBar Progress;
        internal System.Windows.Forms.Label lblTittulo;
    }
}
