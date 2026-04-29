namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class FrmProgres
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

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
            // FrmProgres
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(355, 76);
            this.ControlBox = false;
            this.Controls.Add(this.lblTittulo);
            this.Controls.Add(this.Progress);
            this.Name = "FrmProgres";
            this.Load += new System.EventHandler(this.FrmProgres_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.ProgressBar Progress;
        internal System.Windows.Forms.Label lblTittulo;
    }
}
