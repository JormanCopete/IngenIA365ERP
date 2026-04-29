namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class FrmSolicitudesUsu
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
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.RbtAuxilio = new System.Windows.Forms.RadioButton();
            this.RbtCredito = new System.Windows.Forms.RadioButton();
            this.LblPeriodo = new System.Windows.Forms.Label();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.BtnActualizar = new System.Windows.Forms.Button();
            this.DtpFechaFinal = new System.Windows.Forms.DateTimePicker();
            this.DtpFechaInicial = new System.Windows.Forms.DateTimePicker();
            this.Label3 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.LblNombre = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.DgwGrilla = new System.Windows.Forms.DataGridView();
            this.MenuProceso = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.VolverAGestionarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Numero = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Estado = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmEnte = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Fecha = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValorS = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValorA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Lineac = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmdescripcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Plazo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmNomBenef = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.FechaG = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmPagador = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.GroupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrilla)).BeginInit();
            this.MenuProceso.SuspendLayout();
            this.SuspendLayout();
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.RbtAuxilio);
            this.GroupBox1.Controls.Add(this.RbtCredito);
            this.GroupBox1.Controls.Add(this.LblPeriodo);
            this.GroupBox1.Controls.Add(this.BtnSalir);
            this.GroupBox1.Controls.Add(this.BtnActualizar);
            this.GroupBox1.Controls.Add(this.DtpFechaFinal);
            this.GroupBox1.Controls.Add(this.DtpFechaInicial);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Controls.Add(this.LblNombre);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.TxtCodigoter);
            this.GroupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GroupBox1.Location = new System.Drawing.Point(33, 25);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(683, 114);
            this.GroupBox1.TabIndex = 0;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Generales";
            //
            // RbtAuxilio
            //
            this.RbtAuxilio.AutoSize = true;
            this.RbtAuxilio.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.RbtAuxilio.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.RbtAuxilio.Location = new System.Drawing.Point(361, 16);
            this.RbtAuxilio.Name = "RbtAuxilio";
            this.RbtAuxilio.Size = new System.Drawing.Size(163, 19);
            this.RbtAuxilio.TabIndex = 8;
            this.RbtAuxilio.Text = "Solicitudes de Auxilio";
            this.RbtAuxilio.UseVisualStyleBackColor = true;
            //
            // RbtCredito
            //
            this.RbtCredito.AutoSize = true;
            this.RbtCredito.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.RbtCredito.Checked = true;
            this.RbtCredito.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.RbtCredito.Location = new System.Drawing.Point(151, 16);
            this.RbtCredito.Name = "RbtCredito";
            this.RbtCredito.Size = new System.Drawing.Size(166, 19);
            this.RbtCredito.TabIndex = 7;
            this.RbtCredito.TabStop = true;
            this.RbtCredito.Text = "Solicitudes de Credito";
            this.RbtCredito.UseVisualStyleBackColor = true;
            this.RbtCredito.CheckedChanged += new System.EventHandler(this.RbtCredito_CheckedChanged);
            //
            // LblPeriodo
            //
            this.LblPeriodo.Location = new System.Drawing.Point(638, -16);
            this.LblPeriodo.Name = "LblPeriodo";
            this.LblPeriodo.Size = new System.Drawing.Size(100, 23);
            this.LblPeriodo.TabIndex = 2;
            this.LblPeriodo.Visible = false;
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(620, 73);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 6;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir");
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // BtnActualizar
            //
            //this.BtnActualizar.BackgroundImage = global::ERP.Core.Properties.Resources.replace2;
            this.BtnActualizar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnActualizar.Location = new System.Drawing.Point(573, 73);
            this.BtnActualizar.Name = "BtnActualizar";
            this.BtnActualizar.Size = new System.Drawing.Size(38, 35);
            this.BtnActualizar.TabIndex = 5;
            this.ToolTip1.SetToolTip(this.BtnActualizar, "Actualizar");
            this.BtnActualizar.UseVisualStyleBackColor = true;
            this.BtnActualizar.Click += new System.EventHandler(this.BtnActualizar_Click);
            //
            // DtpFechaFinal
            //
            this.DtpFechaFinal.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFechaFinal.Location = new System.Drawing.Point(408, 80);
            this.DtpFechaFinal.Name = "DtpFechaFinal";
            this.DtpFechaFinal.Size = new System.Drawing.Size(128, 21);
            this.DtpFechaFinal.TabIndex = 4;
            //
            // DtpFechaInicial
            //
            this.DtpFechaInicial.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFechaInicial.Location = new System.Drawing.Point(130, 80);
            this.DtpFechaInicial.Name = "DtpFechaInicial";
            this.DtpFechaInicial.Size = new System.Drawing.Size(128, 21);
            this.DtpFechaInicial.TabIndex = 3;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(323, 83);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(74, 15);
            this.Label3.TabIndex = 2;
            this.Label3.Text = "Fecha Final:";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(22, 83);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(79, 15);
            this.Label2.TabIndex = 1;
            this.Label2.Text = "Fecha Inicial:";
            //
            // LblNombre
            //
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNombre.Location = new System.Drawing.Point(273, 49);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(396, 18);
            this.LblNombre.TabIndex = 2;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(22, 49);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(102, 15);
            this.Label1.TabIndex = 1;
            this.Label1.Text = "Codigo Asociado:";
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.BackColor = System.Drawing.SystemColors.Window;
            this.TxtCodigoter.Location = new System.Drawing.Point(130, 46);
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.ReadOnly = true;
            this.TxtCodigoter.Size = new System.Drawing.Size(128, 21);
            this.TxtCodigoter.TabIndex = 0;
            //
            // DgwGrilla
            //
            this.DgwGrilla.AllowUserToAddRows = false;
            this.DgwGrilla.AllowUserToDeleteRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwGrilla.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgwGrilla.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwGrilla.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.Numero, this.Estado, this.ClmEnte, this.Fecha, this.ValorS, this.ValorA, this.Lineac, this.clmdescripcion, this.Plazo, this.ClmNomBenef, this.FechaG, this.clmPagador });
            this.DgwGrilla.EnableHeadersVisualStyles = false;
            this.DgwGrilla.Location = new System.Drawing.Point(33, 145);
            this.DgwGrilla.MultiSelect = false;
            this.DgwGrilla.Name = "DgwGrilla";
            this.DgwGrilla.ReadOnly = true;
            this.DgwGrilla.RowHeadersVisible = false;
            DataGridViewCellStyle4.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle4.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.DgwGrilla.RowsDefaultCellStyle = DataGridViewCellStyle4;
            this.DgwGrilla.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwGrilla.Size = new System.Drawing.Size(692, 172);
            this.DgwGrilla.TabIndex = 1;
            this.ToolTip1.SetToolTip(this.DgwGrilla, "Doble clic sobre la fila para ver detalles");
            //
            // MenuProceso
            //
            this.MenuProceso.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.VolverAGestionarToolStripMenuItem });
            this.MenuProceso.Name = "MenuProceso";
            this.MenuProceso.Size = new System.Drawing.Size(170, 26);
            //
            // VolverAGestionarToolStripMenuItem
            //
            this.VolverAGestionarToolStripMenuItem.Name = "VolverAGestionarToolStripMenuItem";
            this.VolverAGestionarToolStripMenuItem.Size = new System.Drawing.Size(169, 22);
            this.VolverAGestionarToolStripMenuItem.Text = "Volver a Gestionar";
            //
            // Numero
            //
            this.Numero.DataPropertyName = "numero";
            this.Numero.HeaderText = "Numero";
            this.Numero.Name = "Numero";
            this.Numero.ReadOnly = true;
            this.Numero.Width = 70;
            //
            // Estado
            //
            this.Estado.DataPropertyName = "estado";
            this.Estado.HeaderText = "Estado";
            this.Estado.Name = "Estado";
            this.Estado.ReadOnly = true;
            this.Estado.Width = 50;
            //
            // ClmEnte
            //
            this.ClmEnte.DataPropertyName = "aprobado";
            this.ClmEnte.HeaderText = "Ente";
            this.ClmEnte.Name = "ClmEnte";
            this.ClmEnte.ReadOnly = true;
            this.ClmEnte.Visible = false;
            this.ClmEnte.Width = 50;
            //
            // Fecha
            //
            this.Fecha.DataPropertyName = "fecha_soli";
            this.Fecha.HeaderText = "Fecha";
            this.Fecha.Name = "Fecha";
            this.Fecha.ReadOnly = true;
            this.Fecha.Width = 80;
            //
            // ValorS
            //
            this.ValorS.DataPropertyName = "vlr_solicitud";
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle2.Format = "N2";
            DataGridViewCellStyle2.NullValue = "0";
            this.ValorS.DefaultCellStyle = DataGridViewCellStyle2;
            this.ValorS.HeaderText = "Valor Solicitado";
            this.ValorS.Name = "ValorS";
            this.ValorS.ReadOnly = true;
            //
            // ValorA
            //
            this.ValorA.DataPropertyName = "valor_aprobado";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle3.Format = "N2";
            DataGridViewCellStyle3.NullValue = "0";
            this.ValorA.DefaultCellStyle = DataGridViewCellStyle3;
            this.ValorA.HeaderText = "Valor Aprobado";
            this.ValorA.Name = "ValorA";
            this.ValorA.ReadOnly = true;
            //
            // Lineac
            //
            this.Lineac.DataPropertyName = "lincred";
            this.Lineac.HeaderText = "Linea";
            this.Lineac.Name = "Lineac";
            this.Lineac.ReadOnly = true;
            this.Lineac.Width = 60;
            //
            // clmdescripcion
            //
            this.clmdescripcion.DataPropertyName = "descripcion";
            this.clmdescripcion.HeaderText = "Descripcion";
            this.clmdescripcion.Name = "clmdescripcion";
            this.clmdescripcion.ReadOnly = true;
            this.clmdescripcion.Width = 130;
            //
            // Plazo
            //
            this.Plazo.DataPropertyName = "plazo";
            this.Plazo.HeaderText = "Plazo";
            this.Plazo.Name = "Plazo";
            this.Plazo.ReadOnly = true;
            this.Plazo.Width = 60;
            //
            // ClmNomBenef
            //
            this.ClmNomBenef.DataPropertyName = "nombenef";
            this.ClmNomBenef.HeaderText = "Nombre";
            this.ClmNomBenef.Name = "ClmNomBenef";
            this.ClmNomBenef.ReadOnly = true;
            this.ClmNomBenef.Visible = false;
            //
            // FechaG
            //
            this.FechaG.DataPropertyName = "fecha_graba";
            this.FechaG.HeaderText = "Fecha Grabacion";
            this.FechaG.Name = "FechaG";
            this.FechaG.ReadOnly = true;
            this.FechaG.Width = 90;
            //
            // clmPagador
            //
            this.clmPagador.DataPropertyName = "envpagador";
            this.clmPagador.HeaderText = "Pagador";
            this.clmPagador.Name = "clmPagador";
            this.clmPagador.ReadOnly = true;
            this.clmPagador.Width = 55;
            //
            // FrmSolicitudesUsu
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(748, 326);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.DgwGrilla);
            this.MaximizeBox = false;
            this.Name = "FrmSolicitudesUsu";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Consultar Solicitudes por Asociado";
            this.Load += new System.EventHandler(this.FrmGestionCob_Load);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrilla)).EndInit();
            this.MenuProceso.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.DateTimePicker DtpFechaFinal;
        private System.Windows.Forms.DateTimePicker DtpFechaInicial;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label LblNombre;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.Button BtnActualizar;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.DataGridView DgwGrilla;
        public System.Windows.Forms.TextBox TxtCodigoter;
        public System.Windows.Forms.Label LblPeriodo;
        private System.Windows.Forms.ContextMenuStrip MenuProceso;
        private System.Windows.Forms.ToolStripMenuItem VolverAGestionarToolStripMenuItem;
        private System.Windows.Forms.RadioButton RbtAuxilio;
        private System.Windows.Forms.RadioButton RbtCredito;
        private System.Windows.Forms.DataGridViewTextBoxColumn Numero;
        private System.Windows.Forms.DataGridViewTextBoxColumn Estado;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmEnte;
        private System.Windows.Forms.DataGridViewTextBoxColumn Fecha;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValorS;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValorA;
        private System.Windows.Forms.DataGridViewTextBoxColumn Lineac;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmdescripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn Plazo;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmNomBenef;
        private System.Windows.Forms.DataGridViewTextBoxColumn FechaG;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmPagador;
    }
}
