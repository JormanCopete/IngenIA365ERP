namespace ERP.Core.Compartido.Configuracion
{
    partial class FrmActividades
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
            this.LblEmpresa = new System.Windows.Forms.Label();
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.ayuda_codi = new System.Windows.Forms.Button();
            this.LblNomAsociado = new System.Windows.Forms.Label();
            this.LblNomBeneficiario = new System.Windows.Forms.Label();
            this.HelpBeneficiario = new System.Windows.Forms.Button();
            this.Label3 = new System.Windows.Forms.Label();
            this.TxtBeneficiario = new System.Windows.Forms.TextBox();
            this.LblNomActividad = new System.Windows.Forms.Label();
            this.HelpActividad = new System.Windows.Forms.Button();
            this.Label4 = new System.Windows.Forms.Label();
            this.TxtActividad = new System.Windows.Forms.TextBox();
            this.DgwActividades = new System.Windows.Forms.DataGridView();
            this.BtnQuitar = new System.Windows.Forms.Button();
            this.BtnAgregar = new System.Windows.Forms.Button();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.TxtObservaciones = new System.Windows.Forms.TextBox();
            this.LblObservacion = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.DtpFecha = new System.Windows.Forms.DateTimePicker();
            this.TxtTipoActividad = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.DgwActividades)).BeginInit();
            this.GroupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // LblEmpresa
            //
            this.LblEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblEmpresa.Location = new System.Drawing.Point(44, 9);
            this.LblEmpresa.Name = "LblEmpresa";
            this.LblEmpresa.Size = new System.Drawing.Size(612, 44);
            this.LblEmpresa.TabIndex = 0;
            this.LblEmpresa.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.Enabled = false;
            this.TxtCodigoter.Location = new System.Drawing.Point(110, 20);
            this.TxtCodigoter.MaxLength = 14;
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.Size = new System.Drawing.Size(123, 20);
            this.TxtCodigoter.TabIndex = 1;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(9, 23);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(87, 13);
            this.Label1.TabIndex = 2;
            this.Label1.Text = "Codigo Asociado";
            //
            // ayuda_codi
            //
            this.ayuda_codi.BackColor = System.Drawing.SystemColors.Menu;
            this.ayuda_codi.Enabled = false;
            this.ayuda_codi.Location = new System.Drawing.Point(238, 15);
            this.ayuda_codi.Margin = new System.Windows.Forms.Padding(4);
            this.ayuda_codi.Name = "ayuda_codi";
            this.ayuda_codi.Size = new System.Drawing.Size(28, 28);
            this.ayuda_codi.TabIndex = 125;
            this.ayuda_codi.TabStop = false;
            this.ayuda_codi.Text = "?";
            this.ayuda_codi.UseVisualStyleBackColor = false;
            //
            // LblNomAsociado
            //
            this.LblNomAsociado.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomAsociado.Location = new System.Drawing.Point(286, 23);
            this.LblNomAsociado.Name = "LblNomAsociado";
            this.LblNomAsociado.Size = new System.Drawing.Size(365, 23);
            this.LblNomAsociado.TabIndex = 126;
            //
            // LblNomBeneficiario
            //
            this.LblNomBeneficiario.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomBeneficiario.Location = new System.Drawing.Point(286, 58);
            this.LblNomBeneficiario.Name = "LblNomBeneficiario";
            this.LblNomBeneficiario.Size = new System.Drawing.Size(365, 23);
            this.LblNomBeneficiario.TabIndex = 130;
            //
            // HelpBeneficiario
            //
            this.HelpBeneficiario.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpBeneficiario.Location = new System.Drawing.Point(238, 50);
            this.HelpBeneficiario.Margin = new System.Windows.Forms.Padding(4);
            this.HelpBeneficiario.Name = "HelpBeneficiario";
            this.HelpBeneficiario.Size = new System.Drawing.Size(28, 28);
            this.HelpBeneficiario.TabIndex = 129;
            this.HelpBeneficiario.TabStop = false;
            this.HelpBeneficiario.Text = "?";
            this.HelpBeneficiario.UseVisualStyleBackColor = false;
            this.HelpBeneficiario.Click += new System.EventHandler(this.HelpBeneficiario_Click);
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(9, 58);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(98, 13);
            this.Label3.TabIndex = 128;
            this.Label3.Text = "Codigo Beneficiario";
            //
            // TxtBeneficiario
            //
            this.TxtBeneficiario.Location = new System.Drawing.Point(110, 55);
            this.TxtBeneficiario.MaxLength = 14;
            this.TxtBeneficiario.Name = "TxtBeneficiario";
            this.TxtBeneficiario.Size = new System.Drawing.Size(123, 20);
            this.TxtBeneficiario.TabIndex = 127;
            this.TxtBeneficiario.Leave += new System.EventHandler(this.TxtBeneficiario_Leave);
            //
            // LblNomActividad
            //
            this.LblNomActividad.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomActividad.Location = new System.Drawing.Point(286, 94);
            this.LblNomActividad.Name = "LblNomActividad";
            this.LblNomActividad.Size = new System.Drawing.Size(365, 23);
            this.LblNomActividad.TabIndex = 134;
            //
            // HelpActividad
            //
            this.HelpActividad.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpActividad.Location = new System.Drawing.Point(238, 86);
            this.HelpActividad.Margin = new System.Windows.Forms.Padding(4);
            this.HelpActividad.Name = "HelpActividad";
            this.HelpActividad.Size = new System.Drawing.Size(28, 28);
            this.HelpActividad.TabIndex = 133;
            this.HelpActividad.TabStop = false;
            this.HelpActividad.Text = "?";
            this.HelpActividad.UseVisualStyleBackColor = false;
            this.HelpActividad.Click += new System.EventHandler(this.HelpActividad_Click);
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(9, 94);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(85, 13);
            this.Label4.TabIndex = 132;
            this.Label4.Text = "Aficion o Hobbie";
            //
            // TxtActividad
            //
            this.TxtActividad.Location = new System.Drawing.Point(110, 91);
            this.TxtActividad.MaxLength = 14;
            this.TxtActividad.Name = "TxtActividad";
            this.TxtActividad.Size = new System.Drawing.Size(123, 20);
            this.TxtActividad.TabIndex = 131;
            this.TxtActividad.Leave += new System.EventHandler(this.TxtActividad_Leave);
            //
            // DgwActividades
            //
            this.DgwActividades.AllowUserToAddRows = false;
            this.DgwActividades.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwActividades.Location = new System.Drawing.Point(20, 276);
            this.DgwActividades.Name = "DgwActividades";
            this.DgwActividades.RowHeadersVisible = false;
            this.DgwActividades.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwActividades.Size = new System.Drawing.Size(638, 135);
            this.DgwActividades.TabIndex = 135;
            //
            // BtnQuitar
            //
            this.BtnQuitar.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BtnQuitar.ForeColor = System.Drawing.Color.Red;
            this.BtnQuitar.Location = new System.Drawing.Point(623, 245);
            this.BtnQuitar.Name = "BtnQuitar";
            this.BtnQuitar.Size = new System.Drawing.Size(35, 28);
            this.BtnQuitar.TabIndex = 137;
            this.BtnQuitar.Text = "-";
            this.BtnQuitar.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.BtnQuitar.UseVisualStyleBackColor = true;
            this.BtnQuitar.Click += new System.EventHandler(this.BtnQuitar_Click);
            //
            // BtnAgregar
            //
            this.BtnAgregar.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BtnAgregar.ForeColor = System.Drawing.Color.Red;
            this.BtnAgregar.Location = new System.Drawing.Point(574, 245);
            this.BtnAgregar.Name = "BtnAgregar";
            this.BtnAgregar.Size = new System.Drawing.Size(36, 28);
            this.BtnAgregar.TabIndex = 136;
            this.BtnAgregar.Text = "+";
            this.BtnAgregar.UseVisualStyleBackColor = true;
            this.BtnAgregar.Click += new System.EventHandler(this.BtnAgregar_Click);
            //
            // opcion
            //
            this.opcion.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.opcion.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.opcion.Enabled_Eliminar = true;
            this.opcion.Enabled_Grabar = true;
            this.opcion.Enabled_Salir = true;
            this.opcion.Estilo_Barra = 1;
            this.opcion.Location = new System.Drawing.Point(3, 13);
            this.opcion.Margin = new System.Windows.Forms.Padding(4);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(25, 27);
            this.opcion.TabIndex = 203;
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.TxtObservaciones);
            this.GroupBox1.Controls.Add(this.LblObservacion);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Controls.Add(this.DtpFecha);
            this.GroupBox1.Controls.Add(this.ayuda_codi);
            this.GroupBox1.Controls.Add(this.TxtCodigoter);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.LblNomAsociado);
            this.GroupBox1.Controls.Add(this.TxtBeneficiario);
            this.GroupBox1.Controls.Add(this.LblNomActividad);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.HelpActividad);
            this.GroupBox1.Controls.Add(this.HelpBeneficiario);
            this.GroupBox1.Controls.Add(this.Label4);
            this.GroupBox1.Controls.Add(this.LblNomBeneficiario);
            this.GroupBox1.Controls.Add(this.TxtActividad);
            this.GroupBox1.Location = new System.Drawing.Point(12, 56);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(659, 182);
            this.GroupBox1.TabIndex = 204;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Informacion de Aficiones o Hobbies";
            //
            // TxtObservaciones
            //
            this.TxtObservaciones.Location = new System.Drawing.Point(109, 152);
            this.TxtObservaciones.MaxLength = 50;
            this.TxtObservaciones.Name = "TxtObservaciones";
            this.TxtObservaciones.Size = new System.Drawing.Size(327, 20);
            this.TxtObservaciones.TabIndex = 138;
            //
            // LblObservacion
            //
            this.LblObservacion.AutoSize = true;
            this.LblObservacion.Location = new System.Drawing.Point(9, 152);
            this.LblObservacion.Name = "LblObservacion";
            this.LblObservacion.Size = new System.Drawing.Size(78, 13);
            this.LblObservacion.TabIndex = 139;
            this.LblObservacion.Text = "Observaciones";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(9, 127);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(37, 13);
            this.Label2.TabIndex = 136;
            this.Label2.Text = "Fecha";
            //
            // DtpFecha
            //
            this.DtpFecha.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFecha.Location = new System.Drawing.Point(110, 123);
            this.DtpFecha.Name = "DtpFecha";
            this.DtpFecha.Size = new System.Drawing.Size(109, 20);
            this.DtpFecha.TabIndex = 135;
            //
            // TxtTipoActividad
            //
            this.TxtTipoActividad.Location = new System.Drawing.Point(24, 245);
            this.TxtTipoActividad.MaxLength = 14;
            this.TxtTipoActividad.Name = "TxtTipoActividad";
            this.TxtTipoActividad.Size = new System.Drawing.Size(36, 20);
            this.TxtTipoActividad.TabIndex = 205;
            this.TxtTipoActividad.Visible = false;
            //
            // FrmActividades
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(683, 437);
            this.Controls.Add(this.TxtTipoActividad);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.BtnQuitar);
            this.Controls.Add(this.BtnAgregar);
            this.Controls.Add(this.DgwActividades);
            this.Controls.Add(this.LblEmpresa);
            this.Name = "FrmActividades";
            this.StartPosition = System.Windows.Forms.FormStartPosition.WindowsDefaultBounds;
            this.Text = "Aficiones de los Asociados";
            this.Load += new System.EventHandler(this.FrmActividades_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DgwActividades)).EndInit();
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.Label LblEmpresa;
        public System.Windows.Forms.TextBox TxtCodigoter;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.Button ayuda_codi;
        internal System.Windows.Forms.Label LblNomAsociado;
        internal System.Windows.Forms.Label LblNomBeneficiario;
        internal System.Windows.Forms.Button HelpBeneficiario;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.TextBox TxtBeneficiario;
        internal System.Windows.Forms.Label LblNomActividad;
        internal System.Windows.Forms.Button HelpActividad;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.TextBox TxtActividad;
        internal System.Windows.Forms.DataGridView DgwActividades;
        internal System.Windows.Forms.Button BtnQuitar;
        internal System.Windows.Forms.Button BtnAgregar;
        internal ERP.Core.Compartido.Controles.SasToolBar opcion;
        internal System.Windows.Forms.GroupBox GroupBox1;
        public System.Windows.Forms.TextBox TxtTipoActividad;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.DateTimePicker DtpFecha;
        internal System.Windows.Forms.TextBox TxtObservaciones;
        internal System.Windows.Forms.Label LblObservacion;
    }
}
