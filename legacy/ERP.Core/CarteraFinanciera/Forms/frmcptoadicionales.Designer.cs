namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmcptoadicionales
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmcptoadicionales));
            this.Label1 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.TxtLinea = new System.Windows.Forms.TextBox();
            this.HelpLincred = new System.Windows.Forms.Button();
            this.TxtNumero = new System.Windows.Forms.TextBox();
            this.LblNomLinea = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.TxtValor = new System.Windows.Forms.TextBox();
            this.LblExisteCpto = new System.Windows.Forms.Label();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.SuspendLayout();
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.ForeColor = System.Drawing.Color.DarkBlue;
            this.Label1.Location = new System.Drawing.Point(198, 10);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(168, 16);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "Conceptos Adicionales";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(20, 52);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(41, 16);
            this.Label2.TabIndex = 1;
            this.Label2.Text = "Linea";
            //
            // TxtLinea
            //
            this.TxtLinea.Location = new System.Drawing.Point(67, 49);
            this.TxtLinea.MaxLength = 4;
            this.TxtLinea.Name = "TxtLinea";
            this.TxtLinea.Size = new System.Drawing.Size(42, 22);
            this.TxtLinea.TabIndex = 2;
            this.TxtLinea.LostFocus += new System.EventHandler(this.TxtLinea_LostFocus);
            //
            // HelpLincred
            //
            this.HelpLincred.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpLincred.Image = ((System.Drawing.Image)(resources.GetObject("HelpLincred.Image")));
            this.HelpLincred.Location = new System.Drawing.Point(113, 47);
            this.HelpLincred.Margin = new System.Windows.Forms.Padding(4);
            this.HelpLincred.Name = "HelpLincred";
            this.HelpLincred.Size = new System.Drawing.Size(32, 26);
            this.HelpLincred.TabIndex = 383;
            this.HelpLincred.TabStop = false;
            this.HelpLincred.UseVisualStyleBackColor = false;
            this.HelpLincred.Click += new System.EventHandler(this.HelpLincred_Click);
            //
            // TxtNumero
            //
            this.TxtNumero.Location = new System.Drawing.Point(152, 49);
            this.TxtNumero.Name = "TxtNumero";
            this.TxtNumero.Size = new System.Drawing.Size(100, 22);
            this.TxtNumero.TabIndex = 384;
            this.TxtNumero.Text = "0";
            this.TxtNumero.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtNumero.LostFocus += new System.EventHandler(this.TxtNumero_LostFocus);
            //
            // LblNomLinea
            //
            this.LblNomLinea.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomLinea.Location = new System.Drawing.Point(260, 52);
            this.LblNomLinea.Name = "LblNomLinea";
            this.LblNomLinea.Size = new System.Drawing.Size(284, 19);
            this.LblNomLinea.TabIndex = 385;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(20, 88);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(40, 16);
            this.Label3.TabIndex = 386;
            this.Label3.Text = "Valor";
            //
            // TxtValor
            //
            this.TxtValor.Location = new System.Drawing.Point(67, 85);
            this.TxtValor.Name = "TxtValor";
            this.TxtValor.Size = new System.Drawing.Size(100, 22);
            this.TxtValor.TabIndex = 387;
            this.TxtValor.Text = "0";
            this.TxtValor.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtValor.LostFocus += new System.EventHandler(this.TxtValor_LostFocus);
            //
            // LblExisteCpto
            //
            this.LblExisteCpto.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblExisteCpto.Location = new System.Drawing.Point(260, 88);
            this.LblExisteCpto.Name = "LblExisteCpto";
            this.LblExisteCpto.Size = new System.Drawing.Size(284, 19);
            this.LblExisteCpto.TabIndex = 388;
            //
            // opcion
            //
            this.opcion.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.opcion.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.opcion.Enabled_Eliminar = true;
            this.opcion.Enabled_Grabar = true;
            this.opcion.Enabled_Salir = true;
            this.opcion.Estilo_Barra = 1;
            this.opcion.Imagen_Atras = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Atras")));
            this.opcion.Imagen_Eliminar = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Eliminar")));
            this.opcion.Imagen_Grabar = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Grabar")));
            this.opcion.Imagen_Primero = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Primero")));
            this.opcion.Imagen_Salir = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Salir")));
            this.opcion.Imagen_Siguiente = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Siguiente")));
            this.opcion.Imagen_Ultimo = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Ultimo")));
            this.opcion.Location = new System.Drawing.Point(9, 5);
            this.opcion.Margin = new System.Windows.Forms.Padding(4);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(52, 27);
            this.opcion.TabIndex = 389;
            this.opcion.Tooltip_Boton1 = "Salir";
            this.opcion.Tooltip_Boton2 = "Guardar";
            this.opcion.Tooltip_Boton3 = "Eliminar";
            this.opcion.Tooltip_Boton4 = "Primero";
            this.opcion.Tooltip_Boton5 = "Anterior";
            this.opcion.Tooltip_Boton6 = "Siguiente";
            this.opcion.Tooltip_Boton7 = "Ultimo";
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(-6, 27);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(576, 13);
            this.GroupBox1.TabIndex = 390;
            this.GroupBox1.TabStop = false;
            //
            // frmcptoadicionales
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8.0f, 16.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(564, 118);
            this.ControlBox = false;
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.LblExisteCpto);
            this.Controls.Add(this.TxtValor);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.LblNomLinea);
            this.Controls.Add(this.TxtNumero);
            this.Controls.Add(this.HelpLincred);
            this.Controls.Add(this.TxtLinea);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.GroupBox1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "frmcptoadicionales";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.TextBox TxtLinea;
        private System.Windows.Forms.Button HelpLincred;
        private System.Windows.Forms.TextBox TxtNumero;
        private System.Windows.Forms.Label LblNomLinea;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.TextBox TxtValor;
        private System.Windows.Forms.Label LblExisteCpto;
        private ERP.Core.Compartido.Controles.SasToolBar opcion;
        private System.Windows.Forms.GroupBox GroupBox1;
    }
}
