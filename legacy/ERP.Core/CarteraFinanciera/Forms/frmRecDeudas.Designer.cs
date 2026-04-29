namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmRecDeudas
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();

            this.DatGriCreditos = new System.Windows.Forms.DataGridView();
            this.ClmLinea = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmNumero = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmSaldo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmCapital = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmInteres = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmMora = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmOtros = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.porcentaje = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Label1 = new System.Windows.Forms.Label();
            this.txtCodigoter = new System.Windows.Forms.TextBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.txtLinea = new System.Windows.Forms.TextBox();
            this.LblCredito = new System.Windows.Forms.Label();
            this.GrpGenerales = new System.Windows.Forms.GroupBox();
            this.DtpFecha = new System.Windows.Forms.DateTimePicker();
            this.Label3 = new System.Windows.Forms.Label();
            this.LblDescLinea = new System.Windows.Forms.Label();
            this.Label6 = new System.Windows.Forms.Label();
            this.LblDesembolso = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.LblNombre = new System.Windows.Forms.Label();
            this.GrpCreditoRecogido = new System.Windows.Forms.GroupBox();
            this.CmbSalir = new System.Windows.Forms.Button();
            this.cmbgrabar = new System.Windows.Forms.Button();
            this.Label10 = new System.Windows.Forms.Label();
            this.TxtIntRec = new System.Windows.Forms.TextBox();
            this.Label9 = new System.Windows.Forms.Label();
            this.TxtValorRec = new System.Windows.Forms.TextBox();
            this.TxtNumRec = new System.Windows.Forms.TextBox();
            this.TxtLineaRec = new System.Windows.Forms.TextBox();
            this.MenuLinea = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.IncluirConceptoAdicionalToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Label16 = new System.Windows.Forms.Label();
            this.CtmMenuPendientes = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.VerCuotasPendientesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Label4 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DatGriCreditos)).BeginInit();
            this.GrpGenerales.SuspendLayout();
            this.GrpCreditoRecogido.SuspendLayout();
            this.MenuLinea.SuspendLayout();
            this.CtmMenuPendientes.SuspendLayout();
            this.SuspendLayout();
            //
            // DatGriCreditos
            //
            this.DatGriCreditos.AllowUserToAddRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DatGriCreditos.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DatGriCreditos.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DatGriCreditos.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.ClmLinea, this.ClmNumero, this.ClmSaldo, this.ClmCapital,
                this.ClmInteres, this.ClmMora, this.ClmOtros, this.porcentaje });
            this.DatGriCreditos.EnableHeadersVisualStyles = false;
            this.DatGriCreditos.Location = new System.Drawing.Point(12, 99);
            this.DatGriCreditos.MultiSelect = false;
            this.DatGriCreditos.Name = "DatGriCreditos";
            this.DatGriCreditos.ReadOnly = true;
            this.DatGriCreditos.RowHeadersVisible = false;
            DataGridViewCellStyle10.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            this.DatGriCreditos.RowsDefaultCellStyle = DataGridViewCellStyle10;
            this.DatGriCreditos.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DatGriCreditos.Size = new System.Drawing.Size(780, 240);
            this.DatGriCreditos.TabIndex = 2;
            this.DatGriCreditos.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DatGriCreditos_CellContentClick);
            this.DatGriCreditos.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.DatGriCreditos_CellMouseDoubleClick);
            this.DatGriCreditos.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DatGriCreditos_MouseUp);
            //
            // ClmLinea
            //
            this.ClmLinea.DataPropertyName = "Linea";
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ClmLinea.DefaultCellStyle = DataGridViewCellStyle2;
            this.ClmLinea.HeaderText = "Linea";
            this.ClmLinea.Name = "ClmLinea";
            this.ClmLinea.ReadOnly = true;
            this.ClmLinea.Width = 50;
            //
            // ClmNumero
            //
            this.ClmNumero.DataPropertyName = "Numero";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ClmNumero.DefaultCellStyle = DataGridViewCellStyle3;
            this.ClmNumero.HeaderText = "Numero";
            this.ClmNumero.Name = "ClmNumero";
            this.ClmNumero.ReadOnly = true;
            //
            // ClmSaldo
            //
            this.ClmSaldo.DataPropertyName = "Saldo";
            DataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle4.Format = "N2";
            DataGridViewCellStyle4.NullValue = "0";
            this.ClmSaldo.DefaultCellStyle = DataGridViewCellStyle4;
            this.ClmSaldo.HeaderText = "Saldo";
            this.ClmSaldo.Name = "ClmSaldo";
            this.ClmSaldo.ReadOnly = true;
            this.ClmSaldo.Width = 112;
            //
            // ClmCapital
            //
            this.ClmCapital.DataPropertyName = "Capital";
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle5.Format = "N2";
            DataGridViewCellStyle5.NullValue = "0";
            this.ClmCapital.DefaultCellStyle = DataGridViewCellStyle5;
            this.ClmCapital.HeaderText = "Capital";
            this.ClmCapital.Name = "ClmCapital";
            this.ClmCapital.ReadOnly = true;
            this.ClmCapital.Width = 112;
            //
            // ClmInteres
            //
            this.ClmInteres.DataPropertyName = "Interes";
            DataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle6.Format = "N2";
            DataGridViewCellStyle6.NullValue = "0";
            this.ClmInteres.DefaultCellStyle = DataGridViewCellStyle6;
            this.ClmInteres.HeaderText = "Interes";
            this.ClmInteres.Name = "ClmInteres";
            this.ClmInteres.ReadOnly = true;
            this.ClmInteres.Width = 112;
            //
            // ClmMora
            //
            this.ClmMora.DataPropertyName = "Mora";
            DataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle7.Format = "N2";
            DataGridViewCellStyle7.NullValue = "0";
            this.ClmMora.DefaultCellStyle = DataGridViewCellStyle7;
            this.ClmMora.HeaderText = "Mora";
            this.ClmMora.Name = "ClmMora";
            this.ClmMora.ReadOnly = true;
            this.ClmMora.Width = 112;
            //
            // ClmOtros
            //
            this.ClmOtros.DataPropertyName = "Otros";
            DataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle8.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle8.Format = "N2";
            DataGridViewCellStyle8.NullValue = "0";
            this.ClmOtros.DefaultCellStyle = DataGridViewCellStyle8;
            this.ClmOtros.HeaderText = "Otros";
            this.ClmOtros.Name = "ClmOtros";
            this.ClmOtros.ReadOnly = true;
            this.ClmOtros.Width = 112;
            //
            // porcentaje
            //
            this.porcentaje.DataPropertyName = "porcentaje";
            DataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle9.Format = "N0";
            DataGridViewCellStyle9.NullValue = "0";
            this.porcentaje.DefaultCellStyle = DataGridViewCellStyle9;
            this.porcentaje.HeaderText = "% Amortizacion";
            this.porcentaje.Name = "porcentaje";
            this.porcentaje.ReadOnly = true;
            this.porcentaje.ToolTipText = "Porcentaje del Pago de la obligacion";
            this.porcentaje.Width = 70;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(23, 20);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(40, 13);
            this.Label1.TabIndex = 4;
            this.Label1.Text = "Codigo";
            //
            // txtCodigoter
            //
            this.txtCodigoter.Enabled = false;
            this.txtCodigoter.Location = new System.Drawing.Point(69, 16);
            this.txtCodigoter.Name = "txtCodigoter";
            this.txtCodigoter.Size = new System.Drawing.Size(100, 20);
            this.txtCodigoter.TabIndex = 3;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(23, 53);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(33, 13);
            this.Label2.TabIndex = 6;
            this.Label2.Text = "Linea";
            //
            // txtLinea
            //
            this.txtLinea.Enabled = false;
            this.txtLinea.Location = new System.Drawing.Point(69, 49);
            this.txtLinea.Name = "txtLinea";
            this.txtLinea.Size = new System.Drawing.Size(43, 20);
            this.txtLinea.TabIndex = 5;
            //
            // LblCredito
            //
            this.LblCredito.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblCredito.Location = new System.Drawing.Point(581, 16);
            this.LblCredito.Name = "LblCredito";
            this.LblCredito.Size = new System.Drawing.Size(159, 21);
            this.LblCredito.TabIndex = 9;
            this.LblCredito.Text = "Varlor";
            this.LblCredito.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // GrpGenerales
            //
            this.GrpGenerales.Controls.Add(this.DtpFecha);
            this.GrpGenerales.Controls.Add(this.Label3);
            this.GrpGenerales.Controls.Add(this.LblDescLinea);
            this.GrpGenerales.Controls.Add(this.Label6);
            this.GrpGenerales.Controls.Add(this.LblDesembolso);
            this.GrpGenerales.Controls.Add(this.Label5);
            this.GrpGenerales.Controls.Add(this.LblNombre);
            this.GrpGenerales.Controls.Add(this.txtCodigoter);
            this.GrpGenerales.Controls.Add(this.LblCredito);
            this.GrpGenerales.Controls.Add(this.Label1);
            this.GrpGenerales.Controls.Add(this.txtLinea);
            this.GrpGenerales.Controls.Add(this.Label2);
            this.GrpGenerales.Location = new System.Drawing.Point(21, 12);
            this.GrpGenerales.Name = "GrpGenerales";
            this.GrpGenerales.Size = new System.Drawing.Size(762, 81);
            this.GrpGenerales.TabIndex = 1;
            this.GrpGenerales.TabStop = false;
            this.GrpGenerales.Text = "Datos Generales";
            //
            // DtpFecha
            //
            this.DtpFecha.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFecha.Location = new System.Drawing.Point(339, 49);
            this.DtpFecha.Name = "DtpFecha";
            this.DtpFecha.Size = new System.Drawing.Size(102, 20);
            this.DtpFecha.TabIndex = 16;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(296, 53);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(37, 13);
            this.Label3.TabIndex = 15;
            this.Label3.Text = "Fecha";
            //
            // LblDescLinea
            //
            this.LblDescLinea.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblDescLinea.Location = new System.Drawing.Point(118, 47);
            this.LblDescLinea.Name = "LblDescLinea";
            this.LblDescLinea.Size = new System.Drawing.Size(163, 25);
            this.LblDescLinea.TabIndex = 14;
            this.LblDescLinea.Text = "lblNombreLinea";
            this.LblDescLinea.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // Label6
            //
            this.Label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label6.Location = new System.Drawing.Point(478, 49);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(111, 21);
            this.Label6.TabIndex = 13;
            this.Label6.Text = "Desembolso :";
            this.Label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // LblDesembolso
            //
            this.LblDesembolso.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblDesembolso.Location = new System.Drawing.Point(581, 49);
            this.LblDesembolso.Name = "LblDesembolso";
            this.LblDesembolso.Size = new System.Drawing.Size(159, 21);
            this.LblDesembolso.TabIndex = 12;
            this.LblDesembolso.Text = "Varlor";
            this.LblDesembolso.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label5
            //
            this.Label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label5.Location = new System.Drawing.Point(478, 16);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(111, 21);
            this.Label5.TabIndex = 11;
            this.Label5.Text = "Vlr Credito     :";
            this.Label5.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // LblNombre
            //
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNombre.Location = new System.Drawing.Point(175, 14);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(297, 25);
            this.LblNombre.TabIndex = 10;
            this.LblNombre.Text = "LblNombre";
            this.LblNombre.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // GrpCreditoRecogido
            //
            this.GrpCreditoRecogido.Controls.Add(this.CmbSalir);
            this.GrpCreditoRecogido.Controls.Add(this.cmbgrabar);
            this.GrpCreditoRecogido.Controls.Add(this.Label10);
            this.GrpCreditoRecogido.Controls.Add(this.TxtIntRec);
            this.GrpCreditoRecogido.Controls.Add(this.Label9);
            this.GrpCreditoRecogido.Controls.Add(this.TxtValorRec);
            this.GrpCreditoRecogido.Controls.Add(this.TxtNumRec);
            this.GrpCreditoRecogido.Controls.Add(this.TxtLineaRec);
            this.GrpCreditoRecogido.Controls.Add(this.Label16);
            this.GrpCreditoRecogido.Location = new System.Drawing.Point(25, 345);
            this.GrpCreditoRecogido.Name = "GrpCreditoRecogido";
            this.GrpCreditoRecogido.Size = new System.Drawing.Size(758, 56);
            this.GrpCreditoRecogido.TabIndex = 0;
            this.GrpCreditoRecogido.TabStop = false;
            this.GrpCreditoRecogido.Text = "Datos Credito Recogido";
            //
            // CmbSalir
            //
            this.CmbSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.CmbSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbSalir.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbSalir.Location = new System.Drawing.Point(685, 14);
            this.CmbSalir.Name = "CmbSalir";
            this.CmbSalir.Size = new System.Drawing.Size(47, 36);
            this.CmbSalir.TabIndex = 5;
            this.CmbSalir.UseVisualStyleBackColor = true;
            this.CmbSalir.Click += new System.EventHandler(this.CmbSalir_Click);
            //
            // cmbgrabar
            //
            this.cmbgrabar.BackgroundImage = global::ERP.Core.Properties.Resources.disco;
            this.cmbgrabar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.cmbgrabar.Enabled = false;
            this.cmbgrabar.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbgrabar.Location = new System.Drawing.Point(632, 14);
            this.cmbgrabar.Name = "cmbgrabar";
            this.cmbgrabar.Size = new System.Drawing.Size(47, 36);
            this.cmbgrabar.TabIndex = 4;
            this.cmbgrabar.UseVisualStyleBackColor = true;
            this.cmbgrabar.Click += new System.EventHandler(this.cmbgrabar_Click);
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Location = new System.Drawing.Point(394, 26);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(39, 13);
            this.Label10.TabIndex = 11;
            this.Label10.Text = "Interes";
            //
            // TxtIntRec
            //
            this.TxtIntRec.Enabled = false;
            this.TxtIntRec.Location = new System.Drawing.Point(445, 22);
            this.TxtIntRec.Name = "TxtIntRec";
            this.TxtIntRec.Size = new System.Drawing.Size(100, 20);
            this.TxtIntRec.TabIndex = 3;
            this.TxtIntRec.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtIntRec.LostFocus += new System.EventHandler(this.TxtIntRec_LostFocus);
            this.TxtIntRec.TextChanged += new System.EventHandler(this.TxtIntRec_TextChanged);
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Location = new System.Drawing.Point(228, 26);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(31, 13);
            this.Label9.TabIndex = 9;
            this.Label9.Text = "Valor";
            //
            // TxtValorRec
            //
            this.TxtValorRec.Enabled = false;
            this.TxtValorRec.Location = new System.Drawing.Point(265, 22);
            this.TxtValorRec.Name = "TxtValorRec";
            this.TxtValorRec.Size = new System.Drawing.Size(123, 20);
            this.TxtValorRec.TabIndex = 2;
            this.TxtValorRec.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtValorRec.LostFocus += new System.EventHandler(this.TxtValorRec_LostFocus);
            this.TxtValorRec.TextChanged += new System.EventHandler(this.TxtValorRec_TextChanged);
            //
            // TxtNumRec
            //
            this.TxtNumRec.Location = new System.Drawing.Point(121, 22);
            this.TxtNumRec.Name = "TxtNumRec";
            this.TxtNumRec.Size = new System.Drawing.Size(100, 20);
            this.TxtNumRec.TabIndex = 1;
            this.TxtNumRec.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtNumRec.LostFocus += new System.EventHandler(this.TxtNumRec_LostFocus);
            this.TxtNumRec.TextChanged += new System.EventHandler(this.TxtNumRec_TextChanged);
            //
            // TxtLineaRec
            //
            this.TxtLineaRec.ContextMenuStrip = this.MenuLinea;
            this.TxtLineaRec.Location = new System.Drawing.Point(72, 22);
            this.TxtLineaRec.Name = "TxtLineaRec";
            this.TxtLineaRec.Size = new System.Drawing.Size(43, 20);
            this.TxtLineaRec.TabIndex = 0;
            //
            // MenuLinea
            //
            this.MenuLinea.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.IncluirConceptoAdicionalToolStripMenuItem });
            this.MenuLinea.Name = "MenuLinea";
            this.MenuLinea.Size = new System.Drawing.Size(209, 26);
            //
            // IncluirConceptoAdicionalToolStripMenuItem
            //
            this.IncluirConceptoAdicionalToolStripMenuItem.Name = "IncluirConceptoAdicionalToolStripMenuItem";
            this.IncluirConceptoAdicionalToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.IncluirConceptoAdicionalToolStripMenuItem.Text = "Incluir Concepto Adicional";
            this.IncluirConceptoAdicionalToolStripMenuItem.Click += new System.EventHandler(this.IncluirConceptoAdicionalToolStripMenuItem_Click);
            //
            // Label16
            //
            this.Label16.AutoSize = true;
            this.Label16.Location = new System.Drawing.Point(26, 26);
            this.Label16.Name = "Label16";
            this.Label16.Size = new System.Drawing.Size(33, 13);
            this.Label16.TabIndex = 6;
            this.Label16.Text = "Linea";
            //
            // CtmMenuPendientes
            //
            this.CtmMenuPendientes.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.VerCuotasPendientesToolStripMenuItem });
            this.CtmMenuPendientes.Name = "CtmMenuPendientes";
            this.CtmMenuPendientes.Size = new System.Drawing.Size(195, 26);
            //
            // VerCuotasPendientesToolStripMenuItem
            //
            this.VerCuotasPendientesToolStripMenuItem.Name = "VerCuotasPendientesToolStripMenuItem";
            this.VerCuotasPendientesToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.VerCuotasPendientesToolStripMenuItem.Text = "Ver Cuotas Pendientes";
            this.VerCuotasPendientesToolStripMenuItem.Click += new System.EventHandler(this.VerCuotasPendientesToolStripMenuItem_Click);
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(40, 403);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(335, 13);
            this.Label4.TabIndex = 4;
            this.Label4.Text = "* Clic derecho sobre el campo linea para incluir un concepto adicional";
            //
            // frmRecDeudas
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(804, 421);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.GrpCreditoRecogido);
            this.Controls.Add(this.GrpGenerales);
            this.Controls.Add(this.DatGriCreditos);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmRecDeudas";
            this.Load += new System.EventHandler(this.frmRecDeudas_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DatGriCreditos)).EndInit();
            this.GrpGenerales.ResumeLayout(false);
            this.GrpGenerales.PerformLayout();
            this.GrpCreditoRecogido.ResumeLayout(false);
            this.GrpCreditoRecogido.PerformLayout();
            this.MenuLinea.ResumeLayout(false);
            this.CtmMenuPendientes.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.DataGridView DatGriCreditos;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.TextBox txtCodigoter;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.TextBox txtLinea;
        private System.Windows.Forms.Label LblCredito;
        private System.Windows.Forms.GroupBox GrpGenerales;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.Label LblNombre;
        private System.Windows.Forms.Label LblDescLinea;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.Label LblDesembolso;
        private System.Windows.Forms.GroupBox GrpCreditoRecogido;
        private System.Windows.Forms.Label Label10;
        private System.Windows.Forms.TextBox TxtIntRec;
        private System.Windows.Forms.Label Label9;
        private System.Windows.Forms.TextBox TxtValorRec;
        private System.Windows.Forms.TextBox TxtNumRec;
        private System.Windows.Forms.TextBox TxtLineaRec;
        private System.Windows.Forms.Label Label16;
        private System.Windows.Forms.Button cmbgrabar;
        private System.Windows.Forms.Button CmbSalir;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.DateTimePicker DtpFecha;
        private System.Windows.Forms.ContextMenuStrip CtmMenuPendientes;
        private System.Windows.Forms.ToolStripMenuItem VerCuotasPendientesToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip MenuLinea;
        private System.Windows.Forms.ToolStripMenuItem IncluirConceptoAdicionalToolStripMenuItem;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmLinea;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmNumero;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmSaldo;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmCapital;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmInteres;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmMora;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmOtros;
        private System.Windows.Forms.DataGridViewTextBoxColumn porcentaje;
    }
}
