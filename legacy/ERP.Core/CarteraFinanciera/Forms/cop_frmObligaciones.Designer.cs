namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class cop_frmObligaciones
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(cop_frmObligaciones));
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            this.SasToolBar1 = new ERP.Core.Compartido.Controles.SasToolBar();
            this.Grp_Generar = new System.Windows.Forms.GroupBox();
            this.RdBtn_Obligacion = new System.Windows.Forms.RadioButton();
            this.RdBtn_Lineas = new System.Windows.Forms.RadioButton();
            this.Panel1 = new System.Windows.Forms.Panel();
            this.Dgv_Obligacion = new System.Windows.Forms.DataGridView();
            this.Panel2 = new System.Windows.Forms.Panel();
            this.Chk_Aplicar = new System.Windows.Forms.CheckBox();
            this.LblNombre = new System.Windows.Forms.Label();
            this.LblCodigo = new System.Windows.Forms.Label();
            this.DataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DataGridViewTextBoxColumn7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lincred = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.numero = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.descripcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cuota = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.saldo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Numcuotas = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.extras = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.periodd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Grp_Generar.SuspendLayout();
            this.Panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Dgv_Obligacion)).BeginInit();
            this.Panel2.SuspendLayout();
            this.SuspendLayout();
            //
            // SasToolBar1
            //
            this.SasToolBar1.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.SasToolBar1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.SasToolBar1.Enabled_Eliminar = true;
            this.SasToolBar1.Enabled_Grabar = true;
            this.SasToolBar1.Enabled_Salir = true;
            this.SasToolBar1.Estilo_Barra = 1;
            this.SasToolBar1.Imagen_Atras = ((System.Drawing.Image)(resources.GetObject("SasToolBar1.Imagen_Atras")));
            this.SasToolBar1.Imagen_Eliminar = ((System.Drawing.Image)(resources.GetObject("SasToolBar1.Imagen_Eliminar")));
            this.SasToolBar1.Imagen_Grabar = ((System.Drawing.Image)(resources.GetObject("SasToolBar1.Imagen_Grabar")));
            this.SasToolBar1.Imagen_Primero = ((System.Drawing.Image)(resources.GetObject("SasToolBar1.Imagen_Primero")));
            this.SasToolBar1.Imagen_Salir = ((System.Drawing.Image)(resources.GetObject("SasToolBar1.Imagen_Salir")));
            this.SasToolBar1.Imagen_Siguiente = ((System.Drawing.Image)(resources.GetObject("SasToolBar1.Imagen_Siguiente")));
            this.SasToolBar1.Imagen_Ultimo = ((System.Drawing.Image)(resources.GetObject("SasToolBar1.Imagen_Ultimo")));
            this.SasToolBar1.Location = new System.Drawing.Point(12, 12);
            this.SasToolBar1.Name = "SasToolBar1";
            this.SasToolBar1.Size = new System.Drawing.Size(28, 27);
            this.SasToolBar1.TabIndex = 13;
            this.SasToolBar1.Tooltip_Boton1 = "Salir";
            this.SasToolBar1.Tooltip_Boton2 = "Guardar";
            this.SasToolBar1.Tooltip_Boton3 = "Eliminar";
            this.SasToolBar1.Tooltip_Boton4 = "Primero";
            this.SasToolBar1.Tooltip_Boton5 = "Anterior";
            this.SasToolBar1.Tooltip_Boton6 = "Siguiente";
            this.SasToolBar1.Tooltip_Boton7 = "Ultimo";
            //
            // Grp_Generar
            //
            this.Grp_Generar.Controls.Add(this.RdBtn_Obligacion);
            this.Grp_Generar.Controls.Add(this.RdBtn_Lineas);
            this.Grp_Generar.Location = new System.Drawing.Point(585, 30);
            this.Grp_Generar.Name = "Grp_Generar";
            this.Grp_Generar.Size = new System.Drawing.Size(212, 48);
            this.Grp_Generar.TabIndex = 0;
            this.Grp_Generar.TabStop = false;
            this.Grp_Generar.Text = "Generar por:";
            //
            // RdBtn_Obligacion
            //
            this.RdBtn_Obligacion.AutoSize = true;
            this.RdBtn_Obligacion.Location = new System.Drawing.Point(105, 19);
            this.RdBtn_Obligacion.Name = "RdBtn_Obligacion";
            this.RdBtn_Obligacion.Size = new System.Drawing.Size(94, 17);
            this.RdBtn_Obligacion.TabIndex = 1;
            this.RdBtn_Obligacion.TabStop = true;
            this.RdBtn_Obligacion.Text = "Por Obligacion";
            this.RdBtn_Obligacion.UseVisualStyleBackColor = true;
            //
            // RdBtn_Lineas
            //
            this.RdBtn_Lineas.AutoSize = true;
            this.RdBtn_Lineas.Location = new System.Drawing.Point(13, 19);
            this.RdBtn_Lineas.Name = "RdBtn_Lineas";
            this.RdBtn_Lineas.Size = new System.Drawing.Size(75, 17);
            this.RdBtn_Lineas.TabIndex = 0;
            this.RdBtn_Lineas.TabStop = true;
            this.RdBtn_Lineas.Text = "Por Lineas";
            this.RdBtn_Lineas.UseVisualStyleBackColor = true;
            //
            // Panel1
            //
            this.Panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Panel1.Controls.Add(this.Dgv_Obligacion);
            this.Panel1.Location = new System.Drawing.Point(20, 84);
            this.Panel1.Name = "Panel1";
            this.Panel1.Size = new System.Drawing.Size(777, 196);
            this.Panel1.TabIndex = 16;
            //
            // Dgv_Obligacion
            //
            this.Dgv_Obligacion.AllowUserToAddRows = false;
            this.Dgv_Obligacion.AllowUserToDeleteRows = false;
            this.Dgv_Obligacion.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.Dgv_Obligacion.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.Dgv_Obligacion.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.Dgv_Obligacion.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.lincred, this.numero, this.descripcion, this.cuota, this.saldo, this.Numcuotas, this.extras, this.periodd });
            DataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            DataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.Dgv_Obligacion.DefaultCellStyle = DataGridViewCellStyle8;
            this.Dgv_Obligacion.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Dgv_Obligacion.Location = new System.Drawing.Point(16, 17);
            this.Dgv_Obligacion.Name = "Dgv_Obligacion";
            this.Dgv_Obligacion.RightToLeft = System.Windows.Forms.RightToLeft.No;
            DataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle9.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle9.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle9.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle9.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle9.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.Dgv_Obligacion.RowHeadersDefaultCellStyle = DataGridViewCellStyle9;
            this.Dgv_Obligacion.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.Dgv_Obligacion.Size = new System.Drawing.Size(747, 161);
            this.Dgv_Obligacion.TabIndex = 0;
            //
            // Panel2
            //
            this.Panel2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Panel2.Controls.Add(this.Chk_Aplicar);
            this.Panel2.Controls.Add(this.Panel1);
            this.Panel2.Controls.Add(this.Grp_Generar);
            this.Panel2.Controls.Add(this.LblNombre);
            this.Panel2.Controls.Add(this.LblCodigo);
            this.Panel2.Location = new System.Drawing.Point(7, 8);
            this.Panel2.Name = "Panel2";
            this.Panel2.Size = new System.Drawing.Size(816, 330);
            this.Panel2.TabIndex = 17;
            //
            // Chk_Aplicar
            //
            this.Chk_Aplicar.AutoSize = true;
            this.Chk_Aplicar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Chk_Aplicar.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Chk_Aplicar.Location = new System.Drawing.Point(653, 291);
            this.Chk_Aplicar.Name = "Chk_Aplicar";
            this.Chk_Aplicar.Size = new System.Drawing.Size(144, 19);
            this.Chk_Aplicar.TabIndex = 354;
            this.Chk_Aplicar.Text = "APLICAR CAMBIOS";
            this.Chk_Aplicar.UseVisualStyleBackColor = true;
            //
            // LblNombre
            //
            this.LblNombre.BackColor = System.Drawing.Color.Gainsboro;
            this.LblNombre.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblNombre.Location = new System.Drawing.Point(199, 43);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(376, 23);
            this.LblNombre.TabIndex = 353;
            this.LblNombre.Text = "#";
            //
            // LblCodigo
            //
            this.LblCodigo.BackColor = System.Drawing.Color.Gainsboro;
            this.LblCodigo.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCodigo.Location = new System.Drawing.Point(36, 43);
            this.LblCodigo.Name = "LblCodigo";
            this.LblCodigo.Size = new System.Drawing.Size(150, 23);
            this.LblCodigo.TabIndex = 352;
            this.LblCodigo.Text = "#";
            //
            // DataGridViewTextBoxColumn1
            //
            this.DataGridViewTextBoxColumn1.DataPropertyName = "lincred";
            DataGridViewCellStyle10.NullValue = "0";
            this.DataGridViewTextBoxColumn1.DefaultCellStyle = DataGridViewCellStyle10;
            this.DataGridViewTextBoxColumn1.HeaderText = "Linea";
            this.DataGridViewTextBoxColumn1.Name = "DataGridViewTextBoxColumn1";
            this.DataGridViewTextBoxColumn1.Width = 80;
            //
            // DataGridViewTextBoxColumn2
            //
            this.DataGridViewTextBoxColumn2.DataPropertyName = "numero";
            this.DataGridViewTextBoxColumn2.HeaderText = "numero";
            this.DataGridViewTextBoxColumn2.Name = "DataGridViewTextBoxColumn2";
            this.DataGridViewTextBoxColumn2.Width = 80;
            //
            // DataGridViewTextBoxColumn3
            //
            this.DataGridViewTextBoxColumn3.DataPropertyName = "descripcion";
            DataGridViewCellStyle11.Format = "N0";
            DataGridViewCellStyle11.NullValue = "0";
            this.DataGridViewTextBoxColumn3.DefaultCellStyle = DataGridViewCellStyle11;
            this.DataGridViewTextBoxColumn3.HeaderText = "descripcion";
            this.DataGridViewTextBoxColumn3.Name = "DataGridViewTextBoxColumn3";
            this.DataGridViewTextBoxColumn3.Width = 88;
            //
            // DataGridViewTextBoxColumn4
            //
            this.DataGridViewTextBoxColumn4.DataPropertyName = "valorOb";
            DataGridViewCellStyle12.Format = "N2";
            DataGridViewCellStyle12.NullValue = "0";
            this.DataGridViewTextBoxColumn4.DefaultCellStyle = DataGridViewCellStyle12;
            this.DataGridViewTextBoxColumn4.HeaderText = "Valor Inicial";
            this.DataGridViewTextBoxColumn4.Name = "DataGridViewTextBoxColumn4";
            this.DataGridViewTextBoxColumn4.Width = 60;
            //
            // DataGridViewTextBoxColumn5
            //
            this.DataGridViewTextBoxColumn5.DataPropertyName = "saldo";
            DataGridViewCellStyle13.Format = "N2";
            DataGridViewCellStyle13.NullValue = "0";
            this.DataGridViewTextBoxColumn5.DefaultCellStyle = DataGridViewCellStyle13;
            this.DataGridViewTextBoxColumn5.HeaderText = "saldo";
            this.DataGridViewTextBoxColumn5.Name = "DataGridViewTextBoxColumn5";
            this.DataGridViewTextBoxColumn5.Width = 59;
            //
            // DataGridViewTextBoxColumn6
            //
            this.DataGridViewTextBoxColumn6.DataPropertyName = "numCuotas";
            DataGridViewCellStyle14.BackColor = System.Drawing.Color.LightGray;
            this.DataGridViewTextBoxColumn6.DefaultCellStyle = DataGridViewCellStyle14;
            this.DataGridViewTextBoxColumn6.HeaderText = "Num. Cuotas";
            this.DataGridViewTextBoxColumn6.Name = "DataGridViewTextBoxColumn6";
            this.DataGridViewTextBoxColumn6.ToolTipText = "Cambiar Numero de Cuotas";
            this.DataGridViewTextBoxColumn6.Width = 80;
            //
            // DataGridViewTextBoxColumn7
            //
            this.DataGridViewTextBoxColumn7.DataPropertyName = "codigoter";
            this.DataGridViewTextBoxColumn7.HeaderText = "codigoter";
            this.DataGridViewTextBoxColumn7.Name = "DataGridViewTextBoxColumn7";
            this.DataGridViewTextBoxColumn7.Visible = false;
            this.DataGridViewTextBoxColumn7.Width = 118;
            //
            // lincred
            //
            this.lincred.DataPropertyName = "lincred";
            DataGridViewCellStyle2.NullValue = "0";
            this.lincred.DefaultCellStyle = DataGridViewCellStyle2;
            this.lincred.HeaderText = "Linea";
            this.lincred.Name = "lincred";
            this.lincred.Width = 58;
            //
            // numero
            //
            this.numero.DataPropertyName = "numero";
            this.numero.HeaderText = "Numero";
            this.numero.Name = "numero";
            this.numero.Width = 69;
            //
            // descripcion
            //
            this.descripcion.DataPropertyName = "descripcion";
            DataGridViewCellStyle3.Format = "N0";
            DataGridViewCellStyle3.NullValue = "0";
            this.descripcion.DefaultCellStyle = DataGridViewCellStyle3;
            this.descripcion.HeaderText = "Descripcion";
            this.descripcion.Name = "descripcion";
            this.descripcion.Width = 88;
            //
            // cuota
            //
            this.cuota.DataPropertyName = "cuota";
            DataGridViewCellStyle4.Format = "N2";
            DataGridViewCellStyle4.NullValue = "0";
            this.cuota.DefaultCellStyle = DataGridViewCellStyle4;
            this.cuota.HeaderText = "Cuota";
            this.cuota.Name = "cuota";
            this.cuota.Width = 60;
            //
            // saldo
            //
            this.saldo.DataPropertyName = "saldo";
            DataGridViewCellStyle5.Format = "N2";
            DataGridViewCellStyle5.NullValue = "0";
            this.saldo.DefaultCellStyle = DataGridViewCellStyle5;
            this.saldo.HeaderText = "Saldo";
            this.saldo.Name = "saldo";
            this.saldo.Width = 59;
            //
            // Numcuotas
            //
            this.Numcuotas.DataPropertyName = "numCuotas";
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Gainsboro;
            this.Numcuotas.DefaultCellStyle = DataGridViewCellStyle6;
            this.Numcuotas.HeaderText = "Num. Cuotas";
            this.Numcuotas.Name = "Numcuotas";
            this.Numcuotas.ToolTipText = "Cambiar Numero de Cuotas";
            this.Numcuotas.Width = 93;
            //
            // extras
            //
            this.extras.DataPropertyName = "extras";
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.Gainsboro;
            this.extras.DefaultCellStyle = DataGridViewCellStyle7;
            this.extras.HeaderText = "Aplica Cuotas Extras";
            this.extras.Name = "extras";
            this.extras.Width = 118;
            //
            // periodd
            //
            this.periodd.DataPropertyName = "periodd";
            this.periodd.HeaderText = "periodd";
            this.periodd.Name = "periodd";
            this.periodd.Visible = false;
            //
            // cop_frmObligaciones
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(831, 350);
            this.Controls.Add(this.SasToolBar1);
            this.Controls.Add(this.Panel2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "cop_frmObligaciones";
            this.Load += new System.EventHandler(this.cop_frmObligaciones_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.cop_frmObligaciones_KeyDown);
            this.Grp_Generar.ResumeLayout(false);
            this.Grp_Generar.PerformLayout();
            this.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.Dgv_Obligacion)).EndInit();
            this.Panel2.ResumeLayout(false);
            this.Panel2.PerformLayout();
            this.ResumeLayout(false);
            //
            // Event wiring
            //
            this.SasToolBar1.ClickEvent += new System.EventHandler(this.SasToolBar1_ClickEvent);
            this.RdBtn_Lineas.CheckedChanged += new System.EventHandler(this.RadioButton1_CheckedChanged);
            this.RdBtn_Obligacion.CheckedChanged += new System.EventHandler(this.RadioButton2_CheckedChanged);
            this.Dgv_Obligacion.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.Dgv_Obligacion_CellContentClick);
            this.Dgv_Obligacion.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.Dgv_Obligacion_CellEndEdit);
            this.Chk_Aplicar.CheckedChanged += new System.EventHandler(this.Chk_Aplicar_CheckedChanged);
        }

        #endregion

        private ERP.Core.Compartido.Controles.SasToolBar SasToolBar1;
        private System.Windows.Forms.GroupBox Grp_Generar;
        private System.Windows.Forms.Panel Panel1;
        private System.Windows.Forms.RadioButton RdBtn_Obligacion;
        private System.Windows.Forms.RadioButton RdBtn_Lineas;
        private System.Windows.Forms.Panel Panel2;
        private System.Windows.Forms.Label LblNombre;
        private System.Windows.Forms.Label LblCodigo;
        private System.Data.Odbc.OdbcConnection myconnect = new System.Data.Odbc.OdbcConnection();
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn3;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn4;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn5;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn6;
        private System.Windows.Forms.DataGridViewTextBoxColumn DataGridViewTextBoxColumn7;
        private System.Windows.Forms.DataGridView Dgv_Obligacion;
        private System.Windows.Forms.CheckBox Chk_Aplicar;
        private System.Windows.Forms.DataGridViewTextBoxColumn lincred;
        private System.Windows.Forms.DataGridViewTextBoxColumn numero;
        private System.Windows.Forms.DataGridViewTextBoxColumn descripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn cuota;
        private System.Windows.Forms.DataGridViewTextBoxColumn saldo;
        private System.Windows.Forms.DataGridViewTextBoxColumn Numcuotas;
        private System.Windows.Forms.DataGridViewTextBoxColumn extras;
        private System.Windows.Forms.DataGridViewTextBoxColumn periodd;
    }
}
