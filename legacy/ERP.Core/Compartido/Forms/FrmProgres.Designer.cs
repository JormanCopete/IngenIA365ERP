namespace ERP.Core.Compartido.Forms
{
    partial class FrmProgres
    {
        /// <summary>
        /// Variable del diseñador requerida.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén utilizando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.Progress = new System.Windows.Forms.ProgressBar();
            this.lblTittulo = new System.Windows.Forms.Label();
            this.mensaje = new System.Windows.Forms.Label();
            this.Time1 = new System.Windows.Forms.Label();
            this.Tiempo = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            //
            // Progress
            //
            this.Progress.ForeColor = System.Drawing.Color.Green;
            this.Progress.Location = new System.Drawing.Point(12, 40);
            this.Progress.Name = "Progress";
            this.Progress.Size = new System.Drawing.Size(363, 18);
            this.Progress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.Progress.TabIndex = 0;
            this.Progress.UseWaitCursor = true;
            //
            // lblTittulo
            //
            this.lblTittulo.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTittulo.ForeColor = System.Drawing.Color.Red;
            this.lblTittulo.Location = new System.Drawing.Point(-2, 14);
            this.lblTittulo.Name = "lblTittulo";
            this.lblTittulo.Size = new System.Drawing.Size(393, 17);
            this.lblTittulo.TabIndex = 1;
            this.lblTittulo.Text = "#";
            this.lblTittulo.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // mensaje
            //
            this.mensaje.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.mensaje.ForeColor = System.Drawing.SystemColors.ControlText;
            this.mensaje.Location = new System.Drawing.Point(-2, 68);
            this.mensaje.Name = "mensaje";
            this.mensaje.Size = new System.Drawing.Size(393, 17);
            this.mensaje.TabIndex = 2;
            this.mensaje.Text = "Espere por favor";
            this.mensaje.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // Time1
            //
            this.Time1.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Time1.Location = new System.Drawing.Point(290, 83);
            this.Time1.Name = "Time1";
            this.Time1.Size = new System.Drawing.Size(100, 23);
            this.Time1.TabIndex = 319;
            this.Time1.Text = "0";
            this.Time1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Tiempo
            //
            this.Tiempo.Tick += new System.EventHandler(this.Tiempo_Tick);
            //
            // FrmProgres
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0F, 13.0F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(387, 99);
            this.ControlBox = false;
            this.Controls.Add(this.Time1);
            this.Controls.Add(this.mensaje);
            this.Controls.Add(this.lblTittulo);
            this.Controls.Add(this.Progress);
            this.Name = "FrmProgres";
            this.Load += new System.EventHandler(this.FrmProgres_Load);
            this.ResumeLayout(false);
        }

        #endregion

        internal System.Windows.Forms.ProgressBar Progress;
        internal System.Windows.Forms.Label lblTittulo;
        internal System.Windows.Forms.Label mensaje;
        internal System.Windows.Forms.Label Time1;
        internal System.Windows.Forms.Timer Tiempo;
    }
}
