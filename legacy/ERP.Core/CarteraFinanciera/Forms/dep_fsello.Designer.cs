namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class dep_fsello
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(dep_fsello));
            this.Opciones = new ERP.Core.Compartido.Controles.SasToolBar();
            this.GroupBox5 = new System.Windows.Forms.GroupBox();
            this.TxtCuenta = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.cmbsig = new System.Windows.Forms.Button();
            this.PicSello = new System.Windows.Forms.PictureBox();
            this.GroupBox5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PicSello)).BeginInit();
            this.SuspendLayout();
            // Opciones
            this.Opciones.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.Opciones.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Opciones.Estilo_Barra = 1;
            this.Opciones.Imagen_Atras = (System.Drawing.Image)resources.GetObject("Opciones.Imagen_Atras");
            this.Opciones.Imagen_Eliminar = (System.Drawing.Image)resources.GetObject("Opciones.Imagen_Eliminar");
            this.Opciones.Imagen_Grabar = (System.Drawing.Image)resources.GetObject("Opciones.Imagen_Grabar");
            this.Opciones.Imagen_Primero = (System.Drawing.Image)resources.GetObject("Opciones.Imagen_Primero");
            this.Opciones.Imagen_Salir = (System.Drawing.Image)resources.GetObject("Opciones.Imagen_Salir");
            this.Opciones.Imagen_Siguiente = (System.Drawing.Image)resources.GetObject("Opciones.Imagen_Siguiente");
            this.Opciones.Imagen_Ultimo = (System.Drawing.Image)resources.GetObject("Opciones.Imagen_Ultimo");
            this.Opciones.Location = new System.Drawing.Point(3, 4);
            this.Opciones.Margin = new System.Windows.Forms.Padding(4);
            this.Opciones.Name = "Opciones";
            this.Opciones.Size = new System.Drawing.Size(79, 27);
            this.Opciones.TabIndex = 308;
            this.Opciones.Tooltip_Boton1 = "Salir";
            this.Opciones.Tooltip_Boton2 = "Guardar";
            this.Opciones.Tooltip_Boton3 = "Eliminar";
            this.Opciones.Tooltip_Boton4 = "Primero";
            this.Opciones.Tooltip_Boton5 = "Anterior";
            this.Opciones.Tooltip_Boton6 = "Siguiente";
            this.Opciones.Tooltip_Boton7 = "Ultimo";
            this.Opciones.ClickEvent += new System.EventHandler(this.Opciones_ClickEvent);
            // GroupBox5
            this.GroupBox5.Controls.Add(this.TxtCuenta);
            this.GroupBox5.Controls.Add(this.Label3);
            this.GroupBox5.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.GroupBox5.Location = new System.Drawing.Point(13, 46);
            this.GroupBox5.Margin = new System.Windows.Forms.Padding(4);
            this.GroupBox5.Name = "GroupBox5";
            this.GroupBox5.Padding = new System.Windows.Forms.Padding(4);
            this.GroupBox5.Size = new System.Drawing.Size(567, 49);
            this.GroupBox5.TabIndex = 307;
            this.GroupBox5.TabStop = false;
            // TxtCuenta
            this.TxtCuenta.BackColor = System.Drawing.SystemColors.Window;
            this.TxtCuenta.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.TxtCuenta.Location = new System.Drawing.Point(257, 18);
            this.TxtCuenta.Margin = new System.Windows.Forms.Padding(4);
            this.TxtCuenta.MaxLength = 14;
            this.TxtCuenta.Name = "TxtCuenta";
            this.TxtCuenta.ReadOnly = true;
            this.TxtCuenta.Size = new System.Drawing.Size(155, 20);
            this.TxtCuenta.TabIndex = 1;
            // Label3
            this.Label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label3.Location = new System.Drawing.Point(133, 15);
            this.Label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(160, 30);
            this.Label3.TabIndex = 202;
            this.Label3.Text = "No. Cuenta";
            // cmbsig
            this.cmbsig.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.cmbsig.Location = new System.Drawing.Point(238, 341);
            this.cmbsig.Margin = new System.Windows.Forms.Padding(4);
            this.cmbsig.Name = "cmbsig";
            this.cmbsig.Size = new System.Drawing.Size(100, 28);
            this.cmbsig.TabIndex = 306;
            this.cmbsig.Text = "Capturar";
            this.cmbsig.UseVisualStyleBackColor = true;
            this.cmbsig.Click += new System.EventHandler(this.cmbsig_Click);
            // PicSello
            this.PicSello.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.PicSello.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.PicSello.Location = new System.Drawing.Point(13, 121);
            this.PicSello.Margin = new System.Windows.Forms.Padding(4);
            this.PicSello.Name = "PicSello";
            this.PicSello.Size = new System.Drawing.Size(567, 209);
            this.PicSello.TabIndex = 305;
            this.PicSello.TabStop = false;
            // dep_fsello
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(593, 382);
            this.Controls.Add(this.Opciones);
            this.Controls.Add(this.GroupBox5);
            this.Controls.Add(this.cmbsig);
            this.Controls.Add(this.PicSello);
            this.Name = "dep_fsello";
            this.Text = "Captura de sello";
            this.Load += new System.EventHandler(this.dep_fsello_Load);
            this.GroupBox5.ResumeLayout(false);
            this.GroupBox5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PicSello)).EndInit();
            this.ResumeLayout(false);
        }

        private ERP.Core.Compartido.Controles.SasToolBar Opciones;
        private System.Windows.Forms.GroupBox GroupBox5;
        private System.Windows.Forms.Button cmbsig;
        private System.Windows.Forms.PictureBox PicSello;
        private System.Windows.Forms.TextBox TxtCuenta;
        private System.Windows.Forms.Label Label3;
    }
}
