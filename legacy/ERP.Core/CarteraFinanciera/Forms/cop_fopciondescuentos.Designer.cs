namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class cop_fopciondescuentos
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
            try
            {
                if (disposing && (components != null))
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(cop_fopciondescuentos));
            this.BtnOpcion2 = new System.Windows.Forms.Button();
            this.BtnOpcion1 = new System.Windows.Forms.Button();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.TxtCiclo = new System.Windows.Forms.TextBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.Splitter1 = new System.Windows.Forms.Splitter();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // BtnOpcion2
            //
            this.BtnOpcion2.BackColor = System.Drawing.SystemColors.Control;
            this.BtnOpcion2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BtnOpcion2.Location = new System.Drawing.Point(146, 103);
            this.BtnOpcion2.Name = "BtnOpcion2";
            this.BtnOpcion2.Size = new System.Drawing.Size(98, 71);
            this.BtnOpcion2.TabIndex = 0;
            this.BtnOpcion2.Text = "Todo lo atrasado incluyendo cuotas extras";
            this.ToolTip1.SetToolTip(this.BtnOpcion2, "Esta opcion permitira incluir en la planilla todo lo atrasado ");
            this.BtnOpcion2.UseVisualStyleBackColor = false;
            this.BtnOpcion2.Click += new System.EventHandler(this.BtnOpcion2_Click);
            //
            // BtnOpcion1
            //
            this.BtnOpcion1.BackColor = System.Drawing.SystemColors.Control;
            this.BtnOpcion1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BtnOpcion1.Location = new System.Drawing.Point(13, 103);
            this.BtnOpcion1.Name = "BtnOpcion1";
            this.BtnOpcion1.Size = new System.Drawing.Size(99, 71);
            this.BtnOpcion1.TabIndex = 2;
            this.BtnOpcion1.Text = "Solo las cuotas extras atrasadas";
            this.ToolTip1.SetToolTip(this.BtnOpcion1, "Esta opcion permitira incluir cuotas atrasadas de ciclos anteriores");
            this.BtnOpcion1.UseVisualStyleBackColor = false;
            this.BtnOpcion1.Click += new System.EventHandler(this.BtnOpcion1_Click);
            //
            // BtnSalir
            //
            this.BtnSalir.BackColor = System.Drawing.SystemColors.Control;
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(285, 138);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(41, 35);
            this.BtnSalir.TabIndex = 3;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir sin incluir cuotas atrasadas");
            this.BtnSalir.UseVisualStyleBackColor = false;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // Label1
            //
            this.Label1.BackColor = System.Drawing.Color.Gray;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25f, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.ForeColor = System.Drawing.SystemColors.Control;
            this.Label1.Location = new System.Drawing.Point(3, 16);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(328, 44);
            this.Label1.TabIndex = 4;
            this.Label1.Text = "Desea incluir cuotas atrasadas ?";
            this.Label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // ToolTip1
            //
            this.ToolTip1.AutoPopDelay = 10000;
            this.ToolTip1.InitialDelay = 500;
            this.ToolTip1.ReshowDelay = 100;
            //
            // TxtCiclo
            //
            this.TxtCiclo.Location = new System.Drawing.Point(191, 67);
            this.TxtCiclo.MaxLength = 6;
            this.TxtCiclo.Name = "TxtCiclo";
            this.TxtCiclo.Size = new System.Drawing.Size(65, 20);
            this.TxtCiclo.TabIndex = 442;
            this.ToolTip1.SetToolTip(this.TxtCiclo, "Debe digitar el ciclo de causacion hasta el cual se incluiran las cuotas atrasada" +
                "s");
            //
            // Label4
            //
            this.Label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label4.ForeColor = System.Drawing.SystemColors.ActiveCaption;
            this.Label4.Location = new System.Drawing.Point(5, 178);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(346, 14);
            this.Label4.TabIndex = 439;
            this.Label4.Text = resources.GetString("Label4.Text");
            //
            // Splitter1
            //
            this.Splitter1.Location = new System.Drawing.Point(0, 0);
            this.Splitter1.Name = "Splitter1";
            this.Splitter1.Size = new System.Drawing.Size(3, 197);
            this.Splitter1.TabIndex = 440;
            this.Splitter1.TabStop = false;
            //
            // Label2
            //
            this.Label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label2.ForeColor = System.Drawing.SystemColors.ActiveCaption;
            this.Label2.Location = new System.Drawing.Point(0, 0);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(368, 20);
            this.Label2.TabIndex = 441;
            this.Label2.Text = resources.GetString("Label2.Text");
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label3.ForeColor = System.Drawing.SystemColors.Control;
            this.Label3.Location = new System.Drawing.Point(12, 67);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(175, 15);
            this.Label3.TabIndex = 443;
            this.Label3.Text = "Hasta que ciclo las cuotas";
            //
            // cop_fopciondescuentos
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gray;
            this.ClientSize = new System.Drawing.Size(338, 197);
            this.ControlBox = false;
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.TxtCiclo);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Splitter1);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.BtnOpcion1);
            this.Controls.Add(this.BtnOpcion2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.Name = "cop_fopciondescuentos";
            this.Opacity = 0.9;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Load += new System.EventHandler(this.cop_fopcionesproyeccion_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button BtnOpcion2;
        private System.Windows.Forms.Button BtnOpcion1;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.Splitter Splitter1;
        private System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.TextBox TxtCiclo;
        private System.Windows.Forms.Label Label3;
    }
}
