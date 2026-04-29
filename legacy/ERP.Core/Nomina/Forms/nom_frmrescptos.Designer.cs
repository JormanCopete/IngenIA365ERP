namespace ERP.Core.Nomina.Forms
{
    partial class nom_frmrescptos
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle16 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            this.TxtDevengos = new System.Windows.Forms.TextBox();
            this.DtgPrestLib = new System.Windows.Forms.DataGridView();
            this.GroupBox2 = new System.Windows.Forms.GroupBox();
            this.TsysServer = new System.Windows.Forms.TextBox();
            this.TsysPrograName = new System.Windows.Forms.TextBox();
            this.TsysFechaNow = new System.Windows.Forms.TextBox();
            this.TsysUsuario = new System.Windows.Forms.TextBox();
            this.TsysBd = new System.Windows.Forms.TextBox();
            this.Label13 = new System.Windows.Forms.Label();
            this.Label34 = new System.Windows.Forms.Label();
            this.Label40 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.TsysEmpresa = new System.Windows.Forms.TextBox();
            this.Tsysperiodo = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.Label33 = new System.Windows.Forms.Label();
            this.TxtidCodigo = new System.Windows.Forms.TextBox();
            this.Label58 = new System.Windows.Forms.Label();
            this.TxtIdEmpresa = new System.Windows.Forms.TextBox();
            this.Label59 = new System.Windows.Forms.Label();
            this.CmbSalir = new System.Windows.Forms.Button();
            this.CmbAceptar = new System.Windows.Forms.Button();
            this.ClmObligacion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDescripcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmTiempo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDevengo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDeduccion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TxtDeducciones = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.DtgPrestLib)).BeginInit();
            this.SuspendLayout();
            //
            // TxtDevengos
            //
            this.TxtDevengos.BackColor = System.Drawing.Color.Khaki;
            this.TxtDevengos.Enabled = false;
            this.TxtDevengos.Location = new System.Drawing.Point(556, 326);
            this.TxtDevengos.MaxLength = 15;
            this.TxtDevengos.Name = "TxtDevengos";
            this.TxtDevengos.Size = new System.Drawing.Size(99, 20);
            this.TxtDevengos.TabIndex = 393;
            this.TxtDevengos.Text = "0";
            this.TxtDevengos.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // DtgPrestLib
            //
            this.DtgPrestLib.AllowUserToAddRows = false;
            this.DtgPrestLib.AllowUserToDeleteRows = false;
            this.DtgPrestLib.AllowUserToResizeColumns = false;
            this.DtgPrestLib.AllowUserToResizeRows = false;
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle9.BackColor = System.Drawing.Color.Sienna;
            dataGridViewCellStyle9.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle9.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle9.SelectionBackColor = System.Drawing.Color.Goldenrod;
            dataGridViewCellStyle9.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle9.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DtgPrestLib.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle9;
            this.DtgPrestLib.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DtgPrestLib.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.ClmObligacion, this.ClmDescripcion, this.ClmTiempo, this.ClmDevengo, this.ClmDeduccion });
            this.DtgPrestLib.EnableHeadersVisualStyles = false;
            this.DtgPrestLib.Location = new System.Drawing.Point(2, 90);
            this.DtgPrestLib.Name = "DtgPrestLib";
            dataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle15.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle15.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle15.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle15.SelectionBackColor = System.Drawing.Color.Goldenrod;
            dataGridViewCellStyle15.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle15.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DtgPrestLib.RowHeadersDefaultCellStyle = dataGridViewCellStyle15;
            this.DtgPrestLib.RowHeadersVisible = false;
            dataGridViewCellStyle16.SelectionBackColor = System.Drawing.Color.Goldenrod;
            this.DtgPrestLib.RowsDefaultCellStyle = dataGridViewCellStyle16;
            this.DtgPrestLib.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DtgPrestLib.Size = new System.Drawing.Size(775, 230);
            this.DtgPrestLib.TabIndex = 392;
            this.DtgPrestLib.Tag = "";
            //
            // GroupBox2
            //
            this.GroupBox2.Location = new System.Drawing.Point(10, 352);
            this.GroupBox2.Name = "GroupBox2";
            this.GroupBox2.Size = new System.Drawing.Size(775, 10);
            this.GroupBox2.TabIndex = 402;
            this.GroupBox2.TabStop = false;
            //
            // TsysServer
            //
            this.TsysServer.BackColor = System.Drawing.SystemColors.Menu;
            this.TsysServer.Enabled = false;
            this.TsysServer.Location = new System.Drawing.Point(71, 367);
            this.TsysServer.Margin = new System.Windows.Forms.Padding(4);
            this.TsysServer.MaxLength = 10;
            this.TsysServer.Name = "TsysServer";
            this.TsysServer.Size = new System.Drawing.Size(95, 20);
            this.TsysServer.TabIndex = 396;
            //
            // TsysPrograName
            //
            this.TsysPrograName.BackColor = System.Drawing.SystemColors.Menu;
            this.TsysPrograName.Enabled = false;
            this.TsysPrograName.Location = new System.Drawing.Point(332, 367);
            this.TsysPrograName.Margin = new System.Windows.Forms.Padding(4);
            this.TsysPrograName.MaxLength = 20;
            this.TsysPrograName.Name = "TsysPrograName";
            this.TsysPrograName.Size = new System.Drawing.Size(132, 20);
            this.TsysPrograName.TabIndex = 400;
            //
            // TsysFechaNow
            //
            this.TsysFechaNow.AcceptsReturn = true;
            this.TsysFechaNow.BackColor = System.Drawing.SystemColors.Menu;
            this.TsysFechaNow.Enabled = false;
            this.TsysFechaNow.Location = new System.Drawing.Point(661, 367);
            this.TsysFechaNow.Margin = new System.Windows.Forms.Padding(4);
            this.TsysFechaNow.Name = "TsysFechaNow";
            this.TsysFechaNow.Size = new System.Drawing.Size(117, 20);
            this.TsysFechaNow.TabIndex = 399;
            //
            // TsysUsuario
            //
            this.TsysUsuario.AcceptsTab = true;
            this.TsysUsuario.BackColor = System.Drawing.SystemColors.Menu;
            this.TsysUsuario.Enabled = false;
            this.TsysUsuario.Location = new System.Drawing.Point(539, 367);
            this.TsysUsuario.Margin = new System.Windows.Forms.Padding(4);
            this.TsysUsuario.MaxLength = 10;
            this.TsysUsuario.Name = "TsysUsuario";
            this.TsysUsuario.Size = new System.Drawing.Size(116, 20);
            this.TsysUsuario.TabIndex = 395;
            //
            // TsysBd
            //
            this.TsysBd.AcceptsReturn = true;
            this.TsysBd.BackColor = System.Drawing.SystemColors.Menu;
            this.TsysBd.Enabled = false;
            this.TsysBd.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this.TsysBd.Location = new System.Drawing.Point(208, 367);
            this.TsysBd.Margin = new System.Windows.Forms.Padding(4);
            this.TsysBd.MaxLength = 20;
            this.TsysBd.Name = "TsysBd";
            this.TsysBd.Size = new System.Drawing.Size(116, 20);
            this.TsysBd.TabIndex = 398;
            //
            // Label13
            //
            this.Label13.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label13.Location = new System.Drawing.Point(12, 367);
            this.Label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label13.Name = "Label13";
            this.Label13.Size = new System.Drawing.Size(64, 21);
            this.Label13.TabIndex = 401;
            this.Label13.Text = "Servidor";
            //
            // Label34
            //
            this.Label34.Enabled = false;
            this.Label34.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label34.Location = new System.Drawing.Point(469, 367);
            this.Label34.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label34.Name = "Label34";
            this.Label34.Size = new System.Drawing.Size(64, 21);
            this.Label34.TabIndex = 394;
            this.Label34.Text = "Usuario";
            //
            // Label40
            //
            this.Label40.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label40.Location = new System.Drawing.Point(172, 367);
            this.Label40.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label40.Name = "Label40";
            this.Label40.Size = new System.Drawing.Size(85, 21);
            this.Label40.TabIndex = 397;
            this.Label40.Text = "B.D.";
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(3, 33);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(775, 10);
            this.GroupBox1.TabIndex = 407;
            this.GroupBox1.TabStop = false;
            //
            // TsysEmpresa
            //
            this.TsysEmpresa.BackColor = System.Drawing.SystemColors.Control;
            this.TsysEmpresa.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.TsysEmpresa.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.TsysEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0F);
            this.TsysEmpresa.Location = new System.Drawing.Point(192, 3);
            this.TsysEmpresa.Margin = new System.Windows.Forms.Padding(4);
            this.TsysEmpresa.Multiline = true;
            this.TsysEmpresa.Name = "TsysEmpresa";
            this.TsysEmpresa.ReadOnly = true;
            this.TsysEmpresa.Size = new System.Drawing.Size(437, 36);
            this.TsysEmpresa.TabIndex = 404;
            this.TsysEmpresa.TabStop = false;
            this.TsysEmpresa.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            //
            // Tsysperiodo
            //
            this.Tsysperiodo.BackColor = System.Drawing.SystemColors.Control;
            this.Tsysperiodo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0F, System.Drawing.FontStyle.Bold);
            this.Tsysperiodo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Tsysperiodo.Location = new System.Drawing.Point(699, 11);
            this.Tsysperiodo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Tsysperiodo.Name = "Tsysperiodo";
            this.Tsysperiodo.Size = new System.Drawing.Size(78, 20);
            this.Tsysperiodo.TabIndex = 406;
            //
            // Label3
            //
            this.Label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0F, System.Drawing.FontStyle.Bold);
            this.Label3.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label3.Location = new System.Drawing.Point(634, 11);
            this.Label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(70, 21);
            this.Label3.TabIndex = 405;
            this.Label3.Text = "Periodo";
            //
            // Label33
            //
            this.Label33.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0F);
            this.Label33.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label33.Location = new System.Drawing.Point(100, 9);
            this.Label33.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label33.Name = "Label33";
            this.Label33.Size = new System.Drawing.Size(66, 30);
            this.Label33.TabIndex = 403;
            this.Label33.Text = "Empresa";
            //
            // TxtidCodigo
            //
            this.TxtidCodigo.Enabled = false;
            this.TxtidCodigo.Location = new System.Drawing.Point(230, 52);
            this.TxtidCodigo.Name = "TxtidCodigo";
            this.TxtidCodigo.Size = new System.Drawing.Size(128, 20);
            this.TxtidCodigo.TabIndex = 409;
            //
            // Label58
            //
            this.Label58.AutoSize = true;
            this.Label58.Location = new System.Drawing.Point(138, 56);
            this.Label58.Name = "Label58";
            this.Label58.Size = new System.Drawing.Size(90, 13);
            this.Label58.TabIndex = 410;
            this.Label58.Text = "Codigo Empleado";
            //
            // TxtIdEmpresa
            //
            this.TxtIdEmpresa.Enabled = false;
            this.TxtIdEmpresa.Location = new System.Drawing.Point(87, 52);
            this.TxtIdEmpresa.Name = "TxtIdEmpresa";
            this.TxtIdEmpresa.Size = new System.Drawing.Size(45, 20);
            this.TxtIdEmpresa.TabIndex = 408;
            this.TxtIdEmpresa.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label59
            //
            this.Label59.AutoSize = true;
            this.Label59.Location = new System.Drawing.Point(36, 56);
            this.Label59.Name = "Label59";
            this.Label59.Size = new System.Drawing.Size(48, 13);
            this.Label59.TabIndex = 411;
            this.Label59.Text = "Empresa";
            //
            // CmbSalir
            //
            this.CmbSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.CmbSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbSalir.Location = new System.Drawing.Point(728, 43);
            this.CmbSalir.Margin = new System.Windows.Forms.Padding(4);
            this.CmbSalir.Name = "CmbSalir";
            this.CmbSalir.Size = new System.Drawing.Size(50, 40);
            this.CmbSalir.TabIndex = 413;
            this.CmbSalir.Click += new System.EventHandler(this.CmbSalir_Click);
            //
            // CmbAceptar
            //
            // this.CmbAceptar.BackgroundImage = global::ERP.Core.Properties.Resources.print; // ERROR: CS0117
            this.CmbAceptar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbAceptar.Location = new System.Drawing.Point(675, 43);
            this.CmbAceptar.Margin = new System.Windows.Forms.Padding(4);
            this.CmbAceptar.Name = "CmbAceptar";
            this.CmbAceptar.Size = new System.Drawing.Size(52, 40);
            this.CmbAceptar.TabIndex = 412;
            this.CmbAceptar.Click += new System.EventHandler(this.CmbAceptar_Click);
            //
            // ClmObligacion
            //
            this.ClmObligacion.DataPropertyName = "Idcpto";
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ClmObligacion.DefaultCellStyle = dataGridViewCellStyle10;
            this.ClmObligacion.HeaderText = "Cpto";
            this.ClmObligacion.Name = "ClmObligacion";
            this.ClmObligacion.Width = 50;
            //
            // ClmDescripcion
            //
            this.ClmDescripcion.DataPropertyName = "Nombre";
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            this.ClmDescripcion.DefaultCellStyle = dataGridViewCellStyle11;
            this.ClmDescripcion.HeaderText = "Descripcion";
            this.ClmDescripcion.Name = "ClmDescripcion";
            this.ClmDescripcion.Width = 420;
            //
            // ClmTiempo
            //
            this.ClmTiempo.DataPropertyName = "tiempo";
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ClmTiempo.DefaultCellStyle = dataGridViewCellStyle12;
            this.ClmTiempo.HeaderText = "Tiempo";
            this.ClmTiempo.Name = "ClmTiempo";
            this.ClmTiempo.Width = 80;
            //
            // ClmDevengo
            //
            this.ClmDevengo.DataPropertyName = "devengo";
            dataGridViewCellStyle13.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle13.Format = "N0";
            dataGridViewCellStyle13.NullValue = null;
            this.ClmDevengo.DefaultCellStyle = dataGridViewCellStyle13;
            this.ClmDevengo.HeaderText = "Devengo";
            this.ClmDevengo.Name = "ClmDevengo";
            //
            // ClmDeduccion
            //
            this.ClmDeduccion.DataPropertyName = "Deduccion";
            dataGridViewCellStyle14.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ClmDeduccion.DefaultCellStyle = dataGridViewCellStyle14;
            this.ClmDeduccion.HeaderText = "Deduccion";
            this.ClmDeduccion.Name = "ClmDeduccion";
            //
            // TxtDeducciones
            //
            this.TxtDeducciones.BackColor = System.Drawing.Color.Khaki;
            this.TxtDeducciones.Enabled = false;
            this.TxtDeducciones.Location = new System.Drawing.Point(655, 326);
            this.TxtDeducciones.MaxLength = 15;
            this.TxtDeducciones.Name = "TxtDeducciones";
            this.TxtDeducciones.Size = new System.Drawing.Size(99, 20);
            this.TxtDeducciones.TabIndex = 414;
            this.TxtDeducciones.Text = "0";
            this.TxtDeducciones.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // nom_frmrescptos
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0F, 13.0F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(790, 393);
            this.Controls.Add(this.TxtDeducciones);
            this.Controls.Add(this.CmbSalir);
            this.Controls.Add(this.CmbAceptar);
            this.Controls.Add(this.TxtidCodigo);
            this.Controls.Add(this.Label58);
            this.Controls.Add(this.TxtIdEmpresa);
            this.Controls.Add(this.Label59);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.TsysEmpresa);
            this.Controls.Add(this.Tsysperiodo);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Label33);
            this.Controls.Add(this.GroupBox2);
            this.Controls.Add(this.TsysServer);
            this.Controls.Add(this.TsysPrograName);
            this.Controls.Add(this.TsysFechaNow);
            this.Controls.Add(this.TsysUsuario);
            this.Controls.Add(this.TsysBd);
            this.Controls.Add(this.Label13);
            this.Controls.Add(this.Label34);
            this.Controls.Add(this.Label40);
            this.Controls.Add(this.TxtDevengos);
            this.Controls.Add(this.DtgPrestLib);
            this.Name = "nom_frmrescptos";
            this.Text = "Consulta de Acumulados";
            this.Load += new System.EventHandler(this.nom_frmrescptos_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DtgPrestLib)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.TextBox TxtDevengos;
        private System.Windows.Forms.DataGridView DtgPrestLib;
        private System.Windows.Forms.GroupBox GroupBox2;
        private System.Windows.Forms.TextBox TsysServer;
        private System.Windows.Forms.TextBox TsysPrograName;
        private System.Windows.Forms.TextBox TsysFechaNow;
        private System.Windows.Forms.TextBox TsysUsuario;
        private System.Windows.Forms.TextBox TsysBd;
        private System.Windows.Forms.Label Label13;
        private System.Windows.Forms.Label Label34;
        private System.Windows.Forms.Label Label40;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.TextBox TsysEmpresa;
        private System.Windows.Forms.Label Tsysperiodo;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label Label33;
        private System.Windows.Forms.TextBox TxtidCodigo;
        private System.Windows.Forms.Label Label58;
        private System.Windows.Forms.TextBox TxtIdEmpresa;
        private System.Windows.Forms.Label Label59;
        private System.Windows.Forms.Button CmbSalir;
        private System.Windows.Forms.Button CmbAceptar;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmObligacion;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmDescripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmTiempo;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmDevengo;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmDeduccion;
        private System.Windows.Forms.TextBox TxtDeducciones;
    }
}
