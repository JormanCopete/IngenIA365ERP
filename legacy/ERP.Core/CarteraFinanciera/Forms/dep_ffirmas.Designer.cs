namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class dep_ffirmas
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(dep_ffirmas));
            this.MacToolB1 = new ERP.Core.Compartido.Controles.SasToolBar();
            this.PicFirma = new System.Windows.Forms.PictureBox();
            this.cmbsig = new System.Windows.Forms.Button();
            this.GroupBox5 = new System.Windows.Forms.GroupBox();
            this.lblNumFirma = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.TxtCuenta = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.CkBfirmareque = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.PicFirma)).BeginInit();
            this.GroupBox5.SuspendLayout();
            this.SuspendLayout();
            // MacToolB1
            this.MacToolB1.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.MacToolB1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.MacToolB1.Estilo_Barra = 1;
            this.MacToolB1.Imagen_Atras = (System.Drawing.Image)resources.GetObject("MacToolB1.Imagen_Atras");
            this.MacToolB1.Imagen_Eliminar = (System.Drawing.Image)resources.GetObject("MacToolB1.Imagen_Eliminar");
            this.MacToolB1.Imagen_Grabar = (System.Drawing.Image)resources.GetObject("MacToolB1.Imagen_Grabar");
            this.MacToolB1.Imagen_Primero = (System.Drawing.Image)resources.GetObject("MacToolB1.Imagen_Primero");
            this.MacToolB1.Imagen_Salir = (System.Drawing.Image)resources.GetObject("MacToolB1.Imagen_Salir");
            this.MacToolB1.Imagen_Siguiente = (System.Drawing.Image)resources.GetObject("MacToolB1.Imagen_Siguiente");
            this.MacToolB1.Imagen_Ultimo = (System.Drawing.Image)resources.GetObject("MacToolB1.Imagen_Ultimo");
            this.MacToolB1.Location = new System.Drawing.Point(16, 3);
            this.MacToolB1.Margin = new System.Windows.Forms.Padding(4);
            this.MacToolB1.Name = "MacToolB1";
            this.MacToolB1.Size = new System.Drawing.Size(192, 27);
            this.MacToolB1.TabIndex = 303;
            this.MacToolB1.Tooltip_Boton1 = "Salir";
            this.MacToolB1.Tooltip_Boton2 = "Guardar";
            this.MacToolB1.Tooltip_Boton3 = "Eliminar";
            this.MacToolB1.Tooltip_Boton4 = "Primero";
            this.MacToolB1.Tooltip_Boton5 = "Anterior";
            this.MacToolB1.Tooltip_Boton6 = "Siguiente";
            this.MacToolB1.Tooltip_Boton7 = "Ultimo";
            this.MacToolB1.ClickEvent += new System.EventHandler(this.MacToolB1_ClickEvent);
            // PicFirma
            this.PicFirma.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.PicFirma.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.PicFirma.Location = new System.Drawing.Point(16, 124);
            this.PicFirma.Margin = new System.Windows.Forms.Padding(4);
            this.PicFirma.Name = "PicFirma";
            this.PicFirma.Size = new System.Drawing.Size(572, 212);
            this.PicFirma.TabIndex = 300;
            this.PicFirma.TabStop = false;
            // cmbsig
            this.cmbsig.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.cmbsig.Location = new System.Drawing.Point(261, 343);
            this.cmbsig.Margin = new System.Windows.Forms.Padding(4);
            this.cmbsig.Name = "cmbsig";
            this.cmbsig.Size = new System.Drawing.Size(100, 28);
            this.cmbsig.TabIndex = 301;
            this.cmbsig.Text = "Capturar";
            this.cmbsig.UseVisualStyleBackColor = true;
            this.cmbsig.Click += new System.EventHandler(this.cmbsig_Click);
            // GroupBox5
            this.GroupBox5.Controls.Add(this.lblNumFirma);
            this.GroupBox5.Controls.Add(this.Label1);
            this.GroupBox5.Controls.Add(this.TxtCuenta);
            this.GroupBox5.Controls.Add(this.Label3);
            this.GroupBox5.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.GroupBox5.Location = new System.Drawing.Point(16, 47);
            this.GroupBox5.Margin = new System.Windows.Forms.Padding(4);
            this.GroupBox5.Name = "GroupBox5";
            this.GroupBox5.Padding = new System.Windows.Forms.Padding(4);
            this.GroupBox5.Size = new System.Drawing.Size(573, 49);
            this.GroupBox5.TabIndex = 302;
            this.GroupBox5.TabStop = false;
            // lblNumFirma
            this.lblNumFirma.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblNumFirma.Location = new System.Drawing.Point(462, 16);
            this.lblNumFirma.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblNumFirma.Name = "lblNumFirma";
            this.lblNumFirma.Size = new System.Drawing.Size(64, 30);
            this.lblNumFirma.TabIndex = 203;
            this.lblNumFirma.Text = "1";
            // Label1
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(404, 16);
            this.Label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(64, 30);
            this.Label1.TabIndex = 203;
            this.Label1.Text = "No.";
            // TxtCuenta
            this.TxtCuenta.BackColor = System.Drawing.SystemColors.Window;
            this.TxtCuenta.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.TxtCuenta.Location = new System.Drawing.Point(163, 16);
            this.TxtCuenta.Margin = new System.Windows.Forms.Padding(4);
            this.TxtCuenta.MaxLength = 14;
            this.TxtCuenta.Name = "TxtCuenta";
            this.TxtCuenta.ReadOnly = true;
            this.TxtCuenta.Size = new System.Drawing.Size(155, 22);
            this.TxtCuenta.TabIndex = 1;
            this.TxtCuenta.TextChanged += new System.EventHandler(this.TxtCuenta_TextChanged);
            // Label3
            this.Label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label3.Location = new System.Drawing.Point(8, 16);
            this.Label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(160, 30);
            this.Label3.TabIndex = 202;
            this.Label3.Text = "No. Cuenta";
            // CkBfirmareque
            this.CkBfirmareque.AutoSize = true;
            this.CkBfirmareque.Location = new System.Drawing.Point(235, 101);
            this.CkBfirmareque.Name = "CkBfirmareque";
            this.CkBfirmareque.Size = new System.Drawing.Size(165, 20);
            this.CkBfirmareque.TabIndex = 304;
            this.CkBfirmareque.Text = "Esta firma es requerida";
            this.CkBfirmareque.UseVisualStyleBackColor = true;
            // dep_ffirmas
            this.AutoScaleDimensions = new System.Drawing.SizeF(8f, 16f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(604, 380);
            this.Controls.Add(this.CkBfirmareque);
            this.Controls.Add(this.MacToolB1);
            this.Controls.Add(this.GroupBox5);
            this.Controls.Add(this.cmbsig);
            this.Controls.Add(this.PicFirma);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "dep_ffirmas";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Load += new System.EventHandler(this.dep_ffirmas_Load);
            ((System.ComponentModel.ISupportInitialize)(this.PicFirma)).EndInit();
            this.GroupBox5.ResumeLayout(false);
            this.GroupBox5.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private ERP.Core.Compartido.Controles.SasToolBar MacToolB1;
        private System.Windows.Forms.PictureBox PicFirma;
        private System.Windows.Forms.Button cmbsig;
        private System.Windows.Forms.GroupBox GroupBox5;
        private System.Windows.Forms.TextBox TxtCuenta;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label lblNumFirma;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.CheckBox CkBfirmareque;
    }
}
