namespace ERP.Core.Nomina.Forms
{
    partial class num_solicitud
    {
        private System.ComponentModel.IContainer components = null;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(num_solicitud));
            this.texto = new System.Windows.Forms.Label();
            this.numero = new System.Windows.Forms.Label();
            this.Button1 = new System.Windows.Forms.Button();
            this.Pro = new System.Windows.Forms.ProgressBar();
            this.imagen = new System.Windows.Forms.PictureBox();
            this.l1 = new System.Windows.Forms.PictureBox();
            this.l3 = new System.Windows.Forms.PictureBox();
            this.l4 = new System.Windows.Forms.PictureBox();
            this.l2 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.imagen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.l1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.l3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.l4)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.l2)).BeginInit();
            this.SuspendLayout();
            //
            // texto
            //
            this.texto.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.texto.Location = new System.Drawing.Point(16, 16);
            this.texto.Name = "texto";
            this.texto.Size = new System.Drawing.Size(392, 24);
            this.texto.TabIndex = 0;
            this.texto.Text = "Su número de solicitud es: ";
            this.texto.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // numero
            //
            this.numero.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numero.ForeColor = System.Drawing.Color.Red;
            this.numero.Location = new System.Drawing.Point(16, 88);
            this.numero.Name = "numero";
            this.numero.Size = new System.Drawing.Size(376, 24);
            this.numero.TabIndex = 1;
            this.numero.Text = "Espere un momento... ";
            this.numero.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // Button1
            //
            this.Button1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.Button1.Location = new System.Drawing.Point(168, 120);
            this.Button1.Name = "Button1";
            this.Button1.Size = new System.Drawing.Size(80, 24);
            this.Button1.TabIndex = 2;
            this.Button1.Text = "Aceptar";
            this.Button1.Click += new System.EventHandler(this.Button1_Click);
            //
            // Pro
            //
            this.Pro.Location = new System.Drawing.Point(24, 70);
            this.Pro.Name = "Pro";
            this.Pro.Size = new System.Drawing.Size(368, 16);
            this.Pro.TabIndex = 3;
            this.Pro.Visible = false;
            //
            // imagen
            //
            this.imagen.Location = new System.Drawing.Point(40, 48);
            this.imagen.Name = "imagen";
            this.imagen.Size = new System.Drawing.Size(336, 24);
            this.imagen.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.imagen.TabIndex = 4;
            this.imagen.TabStop = false;
            this.imagen.Visible = false;
            //
            // l1
            //
            this.l1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.l1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.l1.Dock = System.Windows.Forms.DockStyle.Left;
            this.l1.Location = new System.Drawing.Point(0, 0);
            this.l1.Name = "l1";
            this.l1.Size = new System.Drawing.Size(8, 158);
            this.l1.TabIndex = 6;
            this.l1.TabStop = false;
            this.l1.Visible = false;
            //
            // l3
            //
            this.l3.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.l3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.l3.Dock = System.Windows.Forms.DockStyle.Right;
            this.l3.Location = new System.Drawing.Point(410, 0);
            this.l3.Name = "l3";
            this.l3.Size = new System.Drawing.Size(8, 158);
            this.l3.TabIndex = 7;
            this.l3.TabStop = false;
            this.l3.Visible = false;
            //
            // l4
            //
            this.l4.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.l4.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.l4.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.l4.Location = new System.Drawing.Point(8, 150);
            this.l4.Name = "l4";
            this.l4.Size = new System.Drawing.Size(402, 8);
            this.l4.TabIndex = 8;
            this.l4.TabStop = false;
            this.l4.Visible = false;
            //
            // l2
            //
            this.l2.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.l2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.l2.Dock = System.Windows.Forms.DockStyle.Top;
            this.l2.Location = new System.Drawing.Point(8, 0);
            this.l2.Name = "l2";
            this.l2.Size = new System.Drawing.Size(402, 8);
            this.l2.TabIndex = 9;
            this.l2.TabStop = false;
            this.l2.Visible = false;
            //
            // num_solicitud
            //
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(418, 158);
            this.Controls.Add(this.l2);
            this.Controls.Add(this.l4);
            this.Controls.Add(this.l3);
            this.Controls.Add(this.l1);
            this.Controls.Add(this.imagen);
            this.Controls.Add(this.texto);
            this.Controls.Add(this.Button1);
            this.Controls.Add(this.Pro);
            this.Controls.Add(this.numero);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "num_solicitud";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "num_solicitud";
            this.Load += new System.EventHandler(this.num_solicitud_Load);
            ((System.ComponentModel.ISupportInitialize)(this.imagen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.l1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.l3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.l4)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.l2)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        internal System.Windows.Forms.Button Button1;
        internal System.Windows.Forms.Label numero;
        internal System.Windows.Forms.Label texto;
        internal System.Windows.Forms.ProgressBar Pro;
        internal System.Windows.Forms.PictureBox imagen;
        internal System.Windows.Forms.PictureBox l1;
        internal System.Windows.Forms.PictureBox l3;
        internal System.Windows.Forms.PictureBox l4;
        internal System.Windows.Forms.PictureBox l2;
    }
}
