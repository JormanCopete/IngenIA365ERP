namespace ERP.Core.Inventario.Forms
{
    partial class frmLimiteVentas
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

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmLimiteVentas));
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.Lblperiodo = new System.Windows.Forms.Label();
            this.Label18 = new System.Windows.Forms.Label();
            this.empresappl = new System.Windows.Forms.Label();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.Label11 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.lblInfoNombre = new System.Windows.Forms.Label();
            this.lblNombre = new System.Windows.Forms.Label();
            this.lblInfoCodigoter = new System.Windows.Forms.Label();
            this.lblcodigoter = new System.Windows.Forms.Label();
            this.chkAsociado = new System.Windows.Forms.CheckBox();
            this.grpbLimeteVentas = new System.Windows.Forms.GroupBox();
            this.rdbProductos = new System.Windows.Forms.RadioButton();
            this.rdbGrupos = new System.Windows.Forms.RadioButton();
            this.DgvCantidades = new System.Windows.Forms.DataGridView();
            this.GroupBox1.SuspendLayout();
            this.grpbLimeteVentas.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgvCantidades)).BeginInit();
            this.SuspendLayout();
            //
            // Lblperiodo
            //
            this.Lblperiodo.BackColor = System.Drawing.SystemColors.Control;
            this.Lblperiodo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold);
            this.Lblperiodo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Lblperiodo.Location = new System.Drawing.Point(553, 13);
            this.Lblperiodo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Lblperiodo.Name = "Lblperiodo";
            this.Lblperiodo.Size = new System.Drawing.Size(67, 20);
            this.Lblperiodo.TabIndex = 335;
            //
            // Label18
            //
            this.Label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold);
            this.Label18.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label18.Location = new System.Drawing.Point(478, 13);
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
            this.empresappl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.empresappl.Location = new System.Drawing.Point(97, 7);
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
            this.opcion.Imagen_Atras = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Atras");
            this.opcion.Imagen_Eliminar = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Eliminar");
            this.opcion.Imagen_Grabar = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Grabar");
            this.opcion.Imagen_Primero = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Primero");
            this.opcion.Imagen_Salir = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Salir");
            this.opcion.Imagen_Siguiente = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Siguiente");
            this.opcion.Imagen_Ultimo = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Ultimo");
            this.opcion.Location = new System.Drawing.Point(14, 7);
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
            //
            // Label11
            //
            this.Label11.AutoSize = true;
            this.Label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label11.ForeColor = System.Drawing.Color.DarkBlue;
            this.Label11.Location = new System.Drawing.Point(223, 151);
            this.Label11.Name = "Label11";
            this.Label11.Size = new System.Drawing.Size(186, 20);
            this.Label11.TabIndex = 331;
            this.Label11.Text = "Informacion Detallada";
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.lblInfoNombre);
            this.GroupBox1.Controls.Add(this.lblNombre);
            this.GroupBox1.Controls.Add(this.lblInfoCodigoter);
            this.GroupBox1.Controls.Add(this.lblcodigoter);
            this.GroupBox1.Controls.Add(this.chkAsociado);
            this.GroupBox1.Controls.Add(this.grpbLimeteVentas);
            this.GroupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.GroupBox1.Location = new System.Drawing.Point(30, 51);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(573, 97);
            this.GroupBox1.TabIndex = 330;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Informacion ";
            //
            // lblInfoNombre
            //
            this.lblInfoNombre.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblInfoNombre.Location = new System.Drawing.Point(68, 66);
            this.lblInfoNombre.Name = "lblInfoNombre";
            this.lblInfoNombre.Size = new System.Drawing.Size(270, 19);
            this.lblInfoNombre.TabIndex = 9;
            //
            // lblNombre
            //
            this.lblNombre.AutoSize = true;
            this.lblNombre.Location = new System.Drawing.Point(13, 68);
            this.lblNombre.Name = "lblNombre";
            this.lblNombre.Size = new System.Drawing.Size(52, 15);
            this.lblNombre.TabIndex = 8;
            this.lblNombre.Text = "Nombre";
            //
            // lblInfoCodigoter
            //
            this.lblInfoCodigoter.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblInfoCodigoter.Location = new System.Drawing.Point(68, 42);
            this.lblInfoCodigoter.Name = "lblInfoCodigoter";
            this.lblInfoCodigoter.Size = new System.Drawing.Size(270, 19);
            this.lblInfoCodigoter.TabIndex = 7;
            //
            // lblcodigoter
            //
            this.lblcodigoter.AutoSize = true;
            this.lblcodigoter.Location = new System.Drawing.Point(13, 44);
            this.lblcodigoter.Name = "lblcodigoter";
            this.lblcodigoter.Size = new System.Drawing.Size(46, 15);
            this.lblcodigoter.TabIndex = 6;
            this.lblcodigoter.Text = "Codigo";
            //
            // chkAsociado
            //
            this.chkAsociado.AutoSize = true;
            this.chkAsociado.Location = new System.Drawing.Point(68, 20);
            this.chkAsociado.Name = "chkAsociado";
            this.chkAsociado.Size = new System.Drawing.Size(230, 19);
            this.chkAsociado.TabIndex = 5;
            this.chkAsociado.Text = "Buscar Por el Asociado Seleccionado";
            this.chkAsociado.UseVisualStyleBackColor = true;
            this.chkAsociado.CheckedChanged += new System.EventHandler(this.chkAsociado_CheckedChanged);
            //
            // grpbLimeteVentas
            //
            this.grpbLimeteVentas.Controls.Add(this.rdbProductos);
            this.grpbLimeteVentas.Controls.Add(this.rdbGrupos);
            this.grpbLimeteVentas.Location = new System.Drawing.Point(353, 34);
            this.grpbLimeteVentas.Name = "grpbLimeteVentas";
            this.grpbLimeteVentas.Size = new System.Drawing.Size(200, 51);
            this.grpbLimeteVentas.TabIndex = 4;
            this.grpbLimeteVentas.TabStop = false;
            this.grpbLimeteVentas.Text = "Ver Limite de Ventas Por";
            //
            // rdbProductos
            //
            this.rdbProductos.AutoSize = true;
            this.rdbProductos.Location = new System.Drawing.Point(102, 16);
            this.rdbProductos.Name = "rdbProductos";
            this.rdbProductos.Size = new System.Drawing.Size(80, 19);
            this.rdbProductos.TabIndex = 1;
            this.rdbProductos.TabStop = true;
            this.rdbProductos.Text = "Productos";
            this.rdbProductos.UseVisualStyleBackColor = true;
            this.rdbProductos.CheckedChanged += new System.EventHandler(this.rdbProductos_CheckedChanged);
            //
            // rdbGrupos
            //
            this.rdbGrupos.AutoSize = true;
            this.rdbGrupos.Location = new System.Drawing.Point(19, 16);
            this.rdbGrupos.Name = "rdbGrupos";
            this.rdbGrupos.Size = new System.Drawing.Size(65, 19);
            this.rdbGrupos.TabIndex = 0;
            this.rdbGrupos.TabStop = true;
            this.rdbGrupos.Text = "Grupos";
            this.rdbGrupos.UseVisualStyleBackColor = true;
            this.rdbGrupos.CheckedChanged += new System.EventHandler(this.rdbGrupos_CheckedChanged);
            //
            // DgvCantidades
            //
            this.DgvCantidades.AllowUserToAddRows = false;
            this.DgvCantidades.AllowUserToDeleteRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            DataGridViewCellStyle1.ForeColor = System.Drawing.Color.Black;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgvCantidades.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgvCantidades.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvCantidades.EnableHeadersVisualStyles = false;
            this.DgvCantidades.Location = new System.Drawing.Point(30, 174);
            this.DgvCantidades.Name = "DgvCantidades";
            this.DgvCantidades.ReadOnly = true;
            this.DgvCantidades.RowHeadersVisible = false;
            this.DgvCantidades.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgvCantidades.Size = new System.Drawing.Size(573, 150);
            this.DgvCantidades.TabIndex = 329;
            //
            // frmLimiteVentas
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(633, 336);
            this.Controls.Add(this.Lblperiodo);
            this.Controls.Add(this.Label18);
            this.Controls.Add(this.empresappl);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.Label11);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.DgvCantidades);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmLimiteVentas";
            this.Text = "Limite de Ventas";
            this.Load += new System.EventHandler(this.frmLimiteVentas_Load);
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.grpbLimeteVentas.ResumeLayout(false);
            this.grpbLimeteVentas.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgvCantidades)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label Lblperiodo;
        private System.Windows.Forms.Label Label18;
        private System.Windows.Forms.Label empresappl;
        private ERP.Core.Compartido.Controles.SasToolBar opcion;
        private System.Windows.Forms.Label Label11;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.DataGridView DgvCantidades;
        private System.Windows.Forms.GroupBox grpbLimeteVentas;
        private System.Windows.Forms.RadioButton rdbGrupos;
        private System.Windows.Forms.Label lblcodigoter;
        private System.Windows.Forms.CheckBox chkAsociado;
        private System.Windows.Forms.RadioButton rdbProductos;
        private System.Windows.Forms.Label lblInfoNombre;
        private System.Windows.Forms.Label lblNombre;
        private System.Windows.Forms.Label lblInfoCodigoter;
    }
}
