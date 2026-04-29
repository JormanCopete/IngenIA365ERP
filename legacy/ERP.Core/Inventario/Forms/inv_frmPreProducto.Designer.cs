namespace ERP.Core.Inventario.Forms
{
    partial class inv_frmPreProducto
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(inv_frmPreProducto));
            this.Lblperiodo = new System.Windows.Forms.Label();
            this.Label18 = new System.Windows.Forms.Label();
            this.empresappl = new System.Windows.Forms.Label();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.Label11 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.HelpProducto = new System.Windows.Forms.Button();
            this.txtIdproducto = new System.Windows.Forms.TextBox();
            this.lblInfoNombre = new System.Windows.Forms.Label();
            this.lblcodigoter = new System.Windows.Forms.Label();
            this.LblTodos = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.GroupBox2 = new System.Windows.Forms.GroupBox();
            this.LblCant = new System.Windows.Forms.Label();
            this.Label7 = new System.Windows.Forms.Label();
            this.LblPorc = new System.Windows.Forms.Label();
            this.Label4 = new System.Windows.Forms.Label();
            this.LblSobreCosto = new System.Windows.Forms.Label();
            this.Label8 = new System.Windows.Forms.Label();
            this.LblEspeciales = new System.Windows.Forms.Label();
            this.Label6 = new System.Windows.Forms.Label();
            this.LblAsociados = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.LblCantidad = new System.Windows.Forms.Label();
            this.GroupBox1.SuspendLayout();
            this.GroupBox2.SuspendLayout();
            this.SuspendLayout();
            //
            // Lblperiodo
            //
            this.Lblperiodo.BackColor = System.Drawing.SystemColors.Control;
            this.Lblperiodo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10f, System.Drawing.FontStyle.Bold);
            this.Lblperiodo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Lblperiodo.Location = new System.Drawing.Point(495, 13);
            this.Lblperiodo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Lblperiodo.Name = "Lblperiodo";
            this.Lblperiodo.Size = new System.Drawing.Size(67, 20);
            this.Lblperiodo.TabIndex = 335;
            //
            // Label18
            //
            this.Label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 10f, System.Drawing.FontStyle.Bold);
            this.Label18.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label18.Location = new System.Drawing.Point(427, 13);
            this.Label18.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label18.Name = "Label18";
            this.Label18.Size = new System.Drawing.Size(65, 20);
            this.Label18.TabIndex = 334;
            this.Label18.Text = "Periodo";
            //
            // empresappl
            //
            this.empresappl.BackColor = System.Drawing.SystemColors.Control;
            this.empresappl.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.empresappl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.empresappl.Location = new System.Drawing.Point(44, 7);
            this.empresappl.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.empresappl.Name = "empresappl";
            this.empresappl.Size = new System.Drawing.Size(366, 37);
            this.empresappl.TabIndex = 333;
            this.empresappl.TextAlign = System.Drawing.ContentAlignment.TopCenter;
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
            this.opcion.Location = new System.Drawing.Point(6, 7);
            this.opcion.Margin = new System.Windows.Forms.Padding(4);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(27, 27);
            this.opcion.TabIndex = 332;
            // this.opcion.tooltip_Boton1 = "Salir"; // ERROR: CS1061
            // this.opcion.tooltip_Boton2 = "Guardar"; // ERROR: CS1061
            // this.opcion.tooltip_Boton3 = "Eliminar"; // ERROR: CS1061
            // this.opcion.tooltip_Boton4 = "Primero"; // ERROR: CS1061
            // this.opcion.tooltip_Boton5 = "Anterior"; // ERROR: CS1061
            // this.opcion.tooltip_Boton6 = "Siguiente"; // ERROR: CS1061
            // this.opcion.tooltip_Boton7 = "Ultimo"; // ERROR: CS1061
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            //
            // Label11
            //
            this.Label11.AutoSize = true;
            this.Label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label11.ForeColor = System.Drawing.Color.DarkBlue;
            this.Label11.Location = new System.Drawing.Point(157, 120);
            this.Label11.Name = "Label11";
            this.Label11.Size = new System.Drawing.Size(255, 20);
            this.Label11.TabIndex = 331;
            this.Label11.Text = "Lista de Precios Segun Cliente";
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.HelpProducto);
            this.GroupBox1.Controls.Add(this.txtIdproducto);
            this.GroupBox1.Controls.Add(this.lblInfoNombre);
            this.GroupBox1.Controls.Add(this.lblcodigoter);
            this.GroupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GroupBox1.Location = new System.Drawing.Point(18, 51);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(533, 59);
            this.GroupBox1.TabIndex = 330;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Información ";
            //
            // HelpProducto
            //
            this.HelpProducto.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpProducto.Image = ((System.Drawing.Image)(resources.GetObject("HelpProducto.Image")));
            this.HelpProducto.Location = new System.Drawing.Point(152, 20);
            this.HelpProducto.Margin = new System.Windows.Forms.Padding(4);
            this.HelpProducto.Name = "HelpProducto";
            this.HelpProducto.Size = new System.Drawing.Size(28, 28);
            this.HelpProducto.TabIndex = 207;
            this.HelpProducto.TabStop = false;
            this.HelpProducto.UseVisualStyleBackColor = false;
            this.HelpProducto.Click += new System.EventHandler(this.HelpProducto_Click);
            //
            // txtIdproducto
            //
            this.txtIdproducto.AcceptsReturn = true;
            this.txtIdproducto.Location = new System.Drawing.Point(61, 24);
            this.txtIdproducto.Name = "txtIdproducto";
            this.txtIdproducto.Size = new System.Drawing.Size(87, 21);
            this.txtIdproducto.TabIndex = 10;
            this.txtIdproducto.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtIdproducto.LostFocus += new System.EventHandler(this.txtIdproducto_LostFocus);
            //
            // lblInfoNombre
            //
            this.lblInfoNombre.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblInfoNombre.Location = new System.Drawing.Point(187, 23);
            this.lblInfoNombre.Name = "lblInfoNombre";
            this.lblInfoNombre.Size = new System.Drawing.Size(334, 23);
            this.lblInfoNombre.TabIndex = 9;
            //
            // lblcodigoter
            //
            this.lblcodigoter.AutoSize = true;
            this.lblcodigoter.Location = new System.Drawing.Point(6, 27);
            this.lblcodigoter.Name = "lblcodigoter";
            this.lblcodigoter.Size = new System.Drawing.Size(46, 15);
            this.lblcodigoter.TabIndex = 6;
            this.lblcodigoter.Text = "Código";
            //
            // LblTodos
            //
            this.LblTodos.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblTodos.Location = new System.Drawing.Point(145, 28);
            this.LblTodos.Name = "LblTodos";
            this.LblTodos.Size = new System.Drawing.Size(246, 23);
            this.LblTodos.TabIndex = 9;
            this.LblTodos.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(33, 31);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(48, 16);
            this.Label1.TabIndex = 222;
            this.Label1.Text = "Todos";
            //
            // GroupBox2
            //
            this.GroupBox2.Controls.Add(this.LblCant);
            this.GroupBox2.Controls.Add(this.Label7);
            this.GroupBox2.Controls.Add(this.LblPorc);
            this.GroupBox2.Controls.Add(this.Label4);
            this.GroupBox2.Controls.Add(this.LblSobreCosto);
            this.GroupBox2.Controls.Add(this.Label8);
            this.GroupBox2.Controls.Add(this.LblEspeciales);
            this.GroupBox2.Controls.Add(this.Label6);
            this.GroupBox2.Controls.Add(this.LblAsociados);
            this.GroupBox2.Controls.Add(this.Label2);
            this.GroupBox2.Controls.Add(this.LblCantidad);
            this.GroupBox2.Controls.Add(this.Label1);
            this.GroupBox2.Controls.Add(this.LblTodos);
            this.GroupBox2.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.GroupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GroupBox2.Location = new System.Drawing.Point(17, 143);
            this.GroupBox2.Name = "GroupBox2";
            this.GroupBox2.Size = new System.Drawing.Size(535, 197);
            this.GroupBox2.TabIndex = 331;
            this.GroupBox2.TabStop = false;
            //
            // LblCant
            //
            this.LblCant.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCant.Location = new System.Drawing.Point(397, 92);
            this.LblCant.Name = "LblCant";
            this.LblCant.Size = new System.Drawing.Size(75, 23);
            this.LblCant.TabIndex = 233;
            this.LblCant.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label7.Location = new System.Drawing.Point(478, 155);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(23, 16);
            this.Label7.TabIndex = 232;
            this.Label7.Text = "% ";
            //
            // LblPorc
            //
            this.LblPorc.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblPorc.Location = new System.Drawing.Point(397, 152);
            this.LblPorc.Name = "LblPorc";
            this.LblPorc.Size = new System.Drawing.Size(75, 23);
            this.LblPorc.TabIndex = 231;
            this.LblPorc.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label4.Location = new System.Drawing.Point(33, 155);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(98, 16);
            this.Label4.TabIndex = 230;
            this.Label4.Text = "% Sobre Costo";
            //
            // LblSobreCosto
            //
            this.LblSobreCosto.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblSobreCosto.Location = new System.Drawing.Point(145, 152);
            this.LblSobreCosto.Name = "LblSobreCosto";
            this.LblSobreCosto.Size = new System.Drawing.Size(246, 23);
            this.LblSobreCosto.TabIndex = 229;
            this.LblSobreCosto.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label8
            //
            this.Label8.AutoSize = true;
            this.Label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label8.Location = new System.Drawing.Point(33, 124);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(76, 16);
            this.Label8.TabIndex = 228;
            this.Label8.Text = "Especiales";
            //
            // LblEspeciales
            //
            this.LblEspeciales.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblEspeciales.Location = new System.Drawing.Point(145, 121);
            this.LblEspeciales.Name = "LblEspeciales";
            this.LblEspeciales.Size = new System.Drawing.Size(246, 23);
            this.LblEspeciales.TabIndex = 227;
            this.LblEspeciales.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label6.Location = new System.Drawing.Point(33, 64);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(73, 16);
            this.Label6.TabIndex = 226;
            this.Label6.Text = "Asociados";
            //
            // LblAsociados
            //
            this.LblAsociados.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblAsociados.Location = new System.Drawing.Point(145, 61);
            this.LblAsociados.Name = "LblAsociados";
            this.LblAsociados.Size = new System.Drawing.Size(246, 23);
            this.LblAsociados.TabIndex = 225;
            this.LblAsociados.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label2.Location = new System.Drawing.Point(33, 95);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(62, 16);
            this.Label2.TabIndex = 224;
            this.Label2.Text = "Cantidad";
            //
            // LblCantidad
            //
            this.LblCantidad.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCantidad.Location = new System.Drawing.Point(145, 92);
            this.LblCantidad.Name = "LblCantidad";
            this.LblCantidad.Size = new System.Drawing.Size(246, 23);
            this.LblCantidad.TabIndex = 223;
            this.LblCantidad.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // inv_frmPreProducto
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(568, 352);
            this.Controls.Add(this.GroupBox2);
            this.Controls.Add(this.Lblperiodo);
            this.Controls.Add(this.Label18);
            this.Controls.Add(this.empresappl);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.Label11);
            this.Controls.Add(this.GroupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "inv_frmPreProducto";
            this.Text = "Precio de Venta";
            this.Load += new System.EventHandler(this.frmLimiteVentas_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.inv_frmPreProducto_KeyDown);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.GroupBox2.ResumeLayout(false);
            this.GroupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.Label Lblperiodo;
        internal System.Windows.Forms.Label Label18;
        internal System.Windows.Forms.Label empresappl;
        internal ERP.Core.Compartido.Controles.SasToolBar opcion;
        internal System.Windows.Forms.Label Label11;
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.Label lblcodigoter;
        internal System.Windows.Forms.Label lblInfoNombre;
        internal System.Windows.Forms.TextBox txtIdproducto;
        internal System.Windows.Forms.Button HelpProducto;
        internal System.Windows.Forms.Label LblTodos;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.GroupBox GroupBox2;
        internal System.Windows.Forms.Label Label8;
        internal System.Windows.Forms.Label LblEspeciales;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.Label LblAsociados;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label LblCantidad;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.Label LblSobreCosto;
        internal System.Windows.Forms.Label LblCant;
        internal System.Windows.Forms.Label Label7;
        internal System.Windows.Forms.Label LblPorc;
    }
}
