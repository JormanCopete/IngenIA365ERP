namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class FrmGestionCob
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle16 = new System.Windows.Forms.DataGridViewCellStyle();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
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
            this.ClmId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmFechaGestion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmNovedad = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmfeccompromiso = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmTotal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmpago = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmEstado = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmest = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MenuProceso = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.VolverAGestionarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.BtnSalirPanel = new System.Windows.Forms.Button();
            this.PnlGrillaDetalles = new System.Windows.Forms.Panel();
            this.TxtDetalle = new System.Windows.Forms.RichTextBox();
            this.DtpFecCompromiso = new System.Windows.Forms.DateTimePicker();
            this.Label6 = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.DgwGrillaDetalles = new System.Windows.Forms.DataGridView();
            this.clmlincred = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmnumero = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmVlrAtraso = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmVlrPago = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmDiferencia = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmfecha = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmestadoObli = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Label4 = new System.Windows.Forms.Label();
            this.GroupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrilla)).BeginInit();
            this.MenuProceso.SuspendLayout();
            this.PnlGrillaDetalles.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrillaDetalles)).BeginInit();
            this.SuspendLayout();
            //
            // GroupBox1
            //
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
            this.GroupBox1.Size = new System.Drawing.Size(683, 100);
            this.GroupBox1.TabIndex = 0;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Generales";
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
            this.BtnSalir.Location = new System.Drawing.Point(620, 59);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 6;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir");
            this.BtnSalir.UseVisualStyleBackColor = true;
            //
            // BtnActualizar
            //
            //this.BtnActualizar.BackgroundImage = global::ERP.Core.Properties.Resources.replace2;
            this.BtnActualizar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnActualizar.Location = new System.Drawing.Point(573, 59);
            this.BtnActualizar.Name = "BtnActualizar";
            this.BtnActualizar.Size = new System.Drawing.Size(38, 35);
            this.BtnActualizar.TabIndex = 5;
            this.ToolTip1.SetToolTip(this.BtnActualizar, "Actualizar");
            this.BtnActualizar.UseVisualStyleBackColor = true;
            //
            // DtpFechaFinal
            //
            this.DtpFechaFinal.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFechaFinal.Location = new System.Drawing.Point(408, 66);
            this.DtpFechaFinal.Name = "DtpFechaFinal";
            this.DtpFechaFinal.Size = new System.Drawing.Size(128, 21);
            this.DtpFechaFinal.TabIndex = 4;
            //
            // DtpFechaInicial
            //
            this.DtpFechaInicial.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFechaInicial.Location = new System.Drawing.Point(130, 66);
            this.DtpFechaInicial.Name = "DtpFechaInicial";
            this.DtpFechaInicial.Size = new System.Drawing.Size(128, 21);
            this.DtpFechaInicial.TabIndex = 3;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(323, 69);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(74, 15);
            this.Label3.TabIndex = 2;
            this.Label3.Text = "Fecha Final:";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(22, 69);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(79, 15);
            this.Label2.TabIndex = 1;
            this.Label2.Text = "Fecha Inicial:";
            //
            // LblNombre
            //
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNombre.Location = new System.Drawing.Point(273, 35);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(396, 18);
            this.LblNombre.TabIndex = 2;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(22, 35);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(102, 15);
            this.Label1.TabIndex = 1;
            this.Label1.Text = "Codigo Asociado:";
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.BackColor = System.Drawing.SystemColors.Window;
            this.TxtCodigoter.Location = new System.Drawing.Point(130, 32);
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
            this.DgwGrilla.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.ClmId, this.ClmFechaGestion, this.ClmNovedad, this.clmfeccompromiso, this.clmTotal, this.clmpago, this.ClmEstado, this.clmest });
            this.DgwGrilla.ContextMenuStrip = this.MenuProceso;
            this.DgwGrilla.EnableHeadersVisualStyles = false;
            this.DgwGrilla.Location = new System.Drawing.Point(23, 135);
            this.DgwGrilla.MultiSelect = false;
            this.DgwGrilla.Name = "DgwGrilla";
            this.DgwGrilla.ReadOnly = true;
            this.DgwGrilla.RowHeadersVisible = false;
            DataGridViewCellStyle9.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle9.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.DgwGrilla.RowsDefaultCellStyle = DataGridViewCellStyle9;
            this.DgwGrilla.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwGrilla.Size = new System.Drawing.Size(703, 172);
            this.DgwGrilla.TabIndex = 1;
            this.ToolTip1.SetToolTip(this.DgwGrilla, "Doble clic sobre la fila para ver detalles");
            //
            // ClmId
            //
            this.ClmId.DataPropertyName = "idcodmaeges";
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle2.BackColor = System.Drawing.Color.Beige;
            this.ClmId.DefaultCellStyle = DataGridViewCellStyle2;
            this.ClmId.HeaderText = "Id";
            this.ClmId.Name = "ClmId";
            this.ClmId.ReadOnly = true;
            this.ClmId.Width = 40;
            //
            // ClmFechaGestion
            //
            this.ClmFechaGestion.DataPropertyName = "FECHAGESTION";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle3.BackColor = System.Drawing.Color.Ivory;
            this.ClmFechaGestion.DefaultCellStyle = DataGridViewCellStyle3;
            this.ClmFechaGestion.HeaderText = "Fecha Gestion";
            this.ClmFechaGestion.Name = "ClmFechaGestion";
            this.ClmFechaGestion.ReadOnly = true;
            this.ClmFechaGestion.Width = 130;
            //
            // ClmNovedad
            //
            this.ClmNovedad.DataPropertyName = "desestado";
            DataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.Beige;
            this.ClmNovedad.DefaultCellStyle = DataGridViewCellStyle4;
            this.ClmNovedad.HeaderText = "Novedad";
            this.ClmNovedad.Name = "ClmNovedad";
            this.ClmNovedad.ReadOnly = true;
            this.ClmNovedad.Width = 130;
            //
            // clmfeccompromiso
            //
            this.clmfeccompromiso.DataPropertyName = "fecha_compromiso";
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.Ivory;
            this.clmfeccompromiso.DefaultCellStyle = DataGridViewCellStyle5;
            this.clmfeccompromiso.HeaderText = "Fecha Compromiso";
            this.clmfeccompromiso.Name = "clmfeccompromiso";
            this.clmfeccompromiso.ReadOnly = true;
            this.clmfeccompromiso.Width = 130;
            //
            // clmTotal
            //
            this.clmTotal.DataPropertyName = "totaldeuda";
            DataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle6.Format = "N2";
            DataGridViewCellStyle6.NullValue = "0";
            this.clmTotal.DefaultCellStyle = DataGridViewCellStyle6;
            this.clmTotal.HeaderText = "Total Deuda";
            this.clmTotal.Name = "clmTotal";
            this.clmTotal.ReadOnly = true;
            //
            // clmpago
            //
            this.clmpago.DataPropertyName = "totalpago";
            DataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle7.Format = "N2";
            DataGridViewCellStyle7.NullValue = "0";
            this.clmpago.DefaultCellStyle = DataGridViewCellStyle7;
            this.clmpago.HeaderText = "Total Pago";
            this.clmpago.Name = "clmpago";
            this.clmpago.ReadOnly = true;
            //
            // ClmEstado
            //
            this.ClmEstado.DataPropertyName = "estadopago";
            DataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle8.BackColor = System.Drawing.Color.Beige;
            this.ClmEstado.DefaultCellStyle = DataGridViewCellStyle8;
            this.ClmEstado.HeaderText = "Estado";
            this.ClmEstado.Name = "ClmEstado";
            this.ClmEstado.ReadOnly = true;
            this.ClmEstado.Width = 50;
            //
            // clmest
            //
            this.clmest.DataPropertyName = "estado";
            this.clmest.HeaderText = "est";
            this.clmest.Name = "clmest";
            this.clmest.ReadOnly = true;
            this.clmest.Visible = false;
            this.clmest.Width = 30;
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
            // BtnSalirPanel
            //
            this.BtnSalirPanel.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalirPanel.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalirPanel.Location = new System.Drawing.Point(573, 26);
            this.BtnSalirPanel.Name = "BtnSalirPanel";
            this.BtnSalirPanel.Size = new System.Drawing.Size(38, 35);
            this.BtnSalirPanel.TabIndex = 7;
            this.ToolTip1.SetToolTip(this.BtnSalirPanel, "Salir");
            this.BtnSalirPanel.UseVisualStyleBackColor = true;
            //
            // PnlGrillaDetalles
            //
            this.PnlGrillaDetalles.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.PnlGrillaDetalles.Controls.Add(this.TxtDetalle);
            this.PnlGrillaDetalles.Controls.Add(this.BtnSalirPanel);
            this.PnlGrillaDetalles.Controls.Add(this.DtpFecCompromiso);
            this.PnlGrillaDetalles.Controls.Add(this.Label6);
            this.PnlGrillaDetalles.Controls.Add(this.Label5);
            this.PnlGrillaDetalles.Controls.Add(this.DgwGrillaDetalles);
            this.PnlGrillaDetalles.Controls.Add(this.Label4);
            this.PnlGrillaDetalles.Location = new System.Drawing.Point(58, 40);
            this.PnlGrillaDetalles.Name = "PnlGrillaDetalles";
            this.PnlGrillaDetalles.Size = new System.Drawing.Size(633, 267);
            this.PnlGrillaDetalles.TabIndex = 2;
            this.PnlGrillaDetalles.Visible = false;
            //
            // TxtDetalle
            //
            this.TxtDetalle.BackColor = System.Drawing.SystemColors.Window;
            this.TxtDetalle.Location = new System.Drawing.Point(19, 67);
            this.TxtDetalle.Name = "TxtDetalle";
            this.TxtDetalle.ReadOnly = true;
            this.TxtDetalle.Size = new System.Drawing.Size(592, 51);
            this.TxtDetalle.TabIndex = 8;
            this.TxtDetalle.Text = "";
            //
            // DtpFecCompromiso
            //
            this.DtpFecCompromiso.Enabled = false;
            this.DtpFecCompromiso.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFecCompromiso.Location = new System.Drawing.Point(131, 33);
            this.DtpFecCompromiso.Name = "DtpFecCompromiso";
            this.DtpFecCompromiso.Size = new System.Drawing.Size(101, 20);
            this.DtpFecCompromiso.TabIndex = 5;
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(295, 53);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(40, 13);
            this.Label6.TabIndex = 3;
            this.Label6.Text = "Detalle";
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(14, 37);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(112, 13);
            this.Label5.TabIndex = 2;
            this.Label5.Text = "Fecha de Compromiso";
            //
            // DgwGrillaDetalles
            //
            this.DgwGrillaDetalles.AllowUserToAddRows = false;
            DataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle10.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle10.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle10.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle10.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle10.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwGrillaDetalles.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle10;
            this.DgwGrillaDetalles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwGrillaDetalles.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmlincred, this.clmnumero, this.clmVlrAtraso, this.clmVlrPago, this.clmDiferencia, this.clmfecha, this.ClmestadoObli });
            this.DgwGrillaDetalles.EnableHeadersVisualStyles = false;
            this.DgwGrillaDetalles.Location = new System.Drawing.Point(19, 126);
            this.DgwGrillaDetalles.MultiSelect = false;
            this.DgwGrillaDetalles.Name = "DgwGrillaDetalles";
            this.DgwGrillaDetalles.ReadOnly = true;
            this.DgwGrillaDetalles.RowHeadersVisible = false;
            this.DgwGrillaDetalles.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwGrillaDetalles.Size = new System.Drawing.Size(592, 133);
            this.DgwGrillaDetalles.TabIndex = 1;
            //
            // clmlincred
            //
            this.clmlincred.DataPropertyName = "lincred";
            DataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle11.BackColor = System.Drawing.Color.Beige;
            this.clmlincred.DefaultCellStyle = DataGridViewCellStyle11;
            this.clmlincred.HeaderText = "Linea";
            this.clmlincred.Name = "clmlincred";
            this.clmlincred.ReadOnly = true;
            this.clmlincred.Width = 50;
            //
            // clmnumero
            //
            this.clmnumero.DataPropertyName = "numero";
            DataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle12.BackColor = System.Drawing.Color.Beige;
            this.clmnumero.DefaultCellStyle = DataGridViewCellStyle12;
            this.clmnumero.HeaderText = "Numero";
            this.clmnumero.Name = "clmnumero";
            this.clmnumero.ReadOnly = true;
            this.clmnumero.Width = 80;
            //
            // clmVlrAtraso
            //
            this.clmVlrAtraso.DataPropertyName = "deuda";
            DataGridViewCellStyle13.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle13.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle13.Format = "N2";
            DataGridViewCellStyle13.NullValue = "0";
            this.clmVlrAtraso.DefaultCellStyle = DataGridViewCellStyle13;
            this.clmVlrAtraso.HeaderText = "Valor Deuda";
            this.clmVlrAtraso.Name = "clmVlrAtraso";
            this.clmVlrAtraso.ReadOnly = true;
            //
            // clmVlrPago
            //
            this.clmVlrPago.DataPropertyName = "pago";
            DataGridViewCellStyle14.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle14.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle14.Format = "N2";
            DataGridViewCellStyle14.NullValue = "0";
            this.clmVlrPago.DefaultCellStyle = DataGridViewCellStyle14;
            this.clmVlrPago.HeaderText = "Valor Pago";
            this.clmVlrPago.Name = "clmVlrPago";
            this.clmVlrPago.ReadOnly = true;
            //
            // clmDiferencia
            //
            this.clmDiferencia.DataPropertyName = "diferencia";
            DataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle15.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle15.Format = "N2";
            DataGridViewCellStyle15.NullValue = "0";
            this.clmDiferencia.DefaultCellStyle = DataGridViewCellStyle15;
            this.clmDiferencia.HeaderText = "Diferencia";
            this.clmDiferencia.Name = "clmDiferencia";
            this.clmDiferencia.ReadOnly = true;
            //
            // clmfecha
            //
            this.clmfecha.DataPropertyName = "fecha";
            this.clmfecha.HeaderText = "Fecha";
            this.clmfecha.Name = "clmfecha";
            this.clmfecha.ReadOnly = true;
            this.clmfecha.Width = 70;
            //
            // ClmestadoObli
            //
            this.ClmestadoObli.DataPropertyName = "estadoObli";
            DataGridViewCellStyle16.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle16.BackColor = System.Drawing.Color.Beige;
            this.ClmestadoObli.DefaultCellStyle = DataGridViewCellStyle16;
            this.ClmestadoObli.HeaderText = "Estado";
            this.ClmestadoObli.Name = "ClmestadoObli";
            this.ClmestadoObli.ReadOnly = true;
            this.ClmestadoObli.Width = 70;
            //
            // Label4
            //
            this.Label4.BackColor = System.Drawing.SystemColors.Desktop;
            this.Label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label4.ForeColor = System.Drawing.SystemColors.Window;
            this.Label4.Location = new System.Drawing.Point(-1, 0);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(634, 23);
            this.Label4.TabIndex = 0;
            this.Label4.Text = "Informacion Detallada de la Gestion de Cobranzas";
            this.Label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // FrmGestionCob
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(748, 326);
            this.Controls.Add(this.PnlGrillaDetalles);
            this.Controls.Add(this.DgwGrilla);
            this.Controls.Add(this.GroupBox1);
            this.MaximizeBox = false;
            this.Name = "FrmGestionCob";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Consultar Gestiones de Cobro";
            this.Load += new System.EventHandler(this.FrmGestionCob_Load);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrilla)).EndInit();
            this.MenuProceso.ResumeLayout(false);
            this.PnlGrillaDetalles.ResumeLayout(false);
            this.PnlGrillaDetalles.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrillaDetalles)).EndInit();
            this.ResumeLayout(false);
            //
            // Event wiring
            //
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            this.BtnActualizar.Click += new System.EventHandler(this.BtnActualizar_Click);
            this.DgwGrilla.DoubleClick += new System.EventHandler(this.DgwGrilla_DoubleClick);
            this.BtnSalirPanel.Click += new System.EventHandler(this.BtnSalirPanel_Click);
            this.VolverAGestionarToolStripMenuItem.Click += new System.EventHandler(this.VolverAGestionarToolStripMenuItem_Click);
            this.DgwGrilla.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DgwGrilla_MouseUp);
            this.DgwGrilla.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgwGrilla_CellContentClick);
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
        private System.Windows.Forms.Panel PnlGrillaDetalles;
        private System.Windows.Forms.DataGridView DgwGrillaDetalles;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.Button BtnSalirPanel;
        private System.Windows.Forms.DateTimePicker DtpFecCompromiso;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmlincred;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmnumero;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmVlrAtraso;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmVlrPago;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmDiferencia;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmfecha;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmestadoObli;
        private System.Windows.Forms.RichTextBox TxtDetalle;
        private System.Windows.Forms.ContextMenuStrip MenuProceso;
        private System.Windows.Forms.ToolStripMenuItem VolverAGestionarToolStripMenuItem;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmId;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmFechaGestion;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmNovedad;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmfeccompromiso;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmTotal;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmpago;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmEstado;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmest;
    }
}
