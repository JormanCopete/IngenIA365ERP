namespace ERP.Core.Seguridad.Forms
{
    partial class sys_ffirmas
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
            this.MacToolB1 = new ERP.Core.Compartido.Controles.SasToolBar();
            this.PicFirma = new System.Windows.Forms.PictureBox();
            this.Btn_capturar = new System.Windows.Forms.Button();
            this.GroupBox5 = new System.Windows.Forms.GroupBox();
            this.Lbl_Nombre = new System.Windows.Forms.Label();
            this.Lbl_cargo = new System.Windows.Forms.Label();
            this.Lbl_nomb = new System.Windows.Forms.Label();
            this.lbl_carg = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.PicFirma)).BeginInit();
            this.GroupBox5.SuspendLayout();
            this.SuspendLayout();
            //
            // MacToolB1
            //
            this.MacToolB1.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.MacToolB1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.MacToolB1.Location = new System.Drawing.Point(16, 3);
            this.MacToolB1.Margin = new System.Windows.Forms.Padding(4);
            this.MacToolB1.Name = "MacToolB1";
            this.MacToolB1.Size = new System.Drawing.Size(86, 27);
            this.MacToolB1.TabIndex = 303;
            this.MacToolB1.ClickEvent += new System.EventHandler(this.MacToolB1_ClickEvent);
            this.MacToolB1.Load += new System.EventHandler(this.MacToolB1_Load);
            //
            // PicFirma
            //
            this.PicFirma.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PicFirma.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.PicFirma.Location = new System.Drawing.Point(16, 90);
            this.PicFirma.Margin = new System.Windows.Forms.Padding(4);
            this.PicFirma.Name = "PicFirma";
            this.PicFirma.Size = new System.Drawing.Size(572, 178);
            this.PicFirma.TabIndex = 300;
            this.PicFirma.TabStop = false;
            //
            // Btn_capturar
            //
            this.Btn_capturar.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.Btn_capturar.Location = new System.Drawing.Point(250, 276);
            this.Btn_capturar.Margin = new System.Windows.Forms.Padding(4);
            this.Btn_capturar.Name = "Btn_capturar";
            this.Btn_capturar.Size = new System.Drawing.Size(100, 28);
            this.Btn_capturar.TabIndex = 301;
            this.Btn_capturar.Text = "Capturar";
            this.Btn_capturar.UseVisualStyleBackColor = true;
            this.Btn_capturar.Click += new System.EventHandler(this.Btn_capturar_Click);
            //
            // GroupBox5
            //
            this.GroupBox5.Controls.Add(this.Lbl_Nombre);
            this.GroupBox5.Controls.Add(this.Lbl_cargo);
            this.GroupBox5.Controls.Add(this.Lbl_nomb);
            this.GroupBox5.Controls.Add(this.lbl_carg);
            this.GroupBox5.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.GroupBox5.Location = new System.Drawing.Point(16, 27);
            this.GroupBox5.Margin = new System.Windows.Forms.Padding(4);
            this.GroupBox5.Name = "GroupBox5";
            this.GroupBox5.Padding = new System.Windows.Forms.Padding(4);
            this.GroupBox5.Size = new System.Drawing.Size(573, 55);
            this.GroupBox5.TabIndex = 302;
            this.GroupBox5.TabStop = false;
            //
            // Lbl_nomb
            //
            this.Lbl_nomb.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Lbl_nomb.Location = new System.Drawing.Point(221, 19);
            this.Lbl_nomb.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Lbl_nomb.Name = "Lbl_nomb";
            this.Lbl_nomb.Size = new System.Drawing.Size(80, 27);
            this.Lbl_nomb.TabIndex = 205;
            this.Lbl_nomb.Text = "Nombre:";
            //
            // lbl_carg
            //
            this.lbl_carg.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbl_carg.Location = new System.Drawing.Point(8, 19);
            this.lbl_carg.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lbl_carg.Name = "lbl_carg";
            this.lbl_carg.Size = new System.Drawing.Size(65, 27);
            this.lbl_carg.TabIndex = 204;
            this.lbl_carg.Text = "Cargo:";
            //
            // Lbl_Nombre
            //
            this.Lbl_Nombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Lbl_Nombre.Location = new System.Drawing.Point(286, 19);
            this.Lbl_Nombre.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Lbl_Nombre.Name = "Lbl_Nombre";
            this.Lbl_Nombre.Size = new System.Drawing.Size(279, 30);
            this.Lbl_Nombre.TabIndex = 203;
            //
            // Lbl_cargo
            //
            this.Lbl_cargo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Lbl_cargo.Location = new System.Drawing.Point(63, 19);
            this.Lbl_cargo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Lbl_cargo.Name = "Lbl_cargo";
            this.Lbl_cargo.Size = new System.Drawing.Size(150, 27);
            this.Lbl_cargo.TabIndex = 202;
            //
            // sys_ffirmas
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(604, 315);
            this.Controls.Add(this.MacToolB1);
            this.Controls.Add(this.GroupBox5);
            this.Controls.Add(this.Btn_capturar);
            this.Controls.Add(this.PicFirma);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "sys_ffirmas";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Firmas";
            this.Load += new System.EventHandler(this.sys_ffirmas_Load);
            ((System.ComponentModel.ISupportInitialize)(this.PicFirma)).EndInit();
            this.GroupBox5.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        internal System.Windows.Forms.PictureBox PicFirma;
        internal System.Windows.Forms.Button Btn_capturar;
        internal System.Windows.Forms.GroupBox GroupBox5;
        internal System.Windows.Forms.Label Lbl_cargo;
        internal System.Windows.Forms.Label Lbl_Nombre;
        internal ERP.Core.Compartido.Controles.SasToolBar MacToolB1;
        internal System.Windows.Forms.Label Lbl_nomb;
        internal System.Windows.Forms.Label lbl_carg;
    }
}
