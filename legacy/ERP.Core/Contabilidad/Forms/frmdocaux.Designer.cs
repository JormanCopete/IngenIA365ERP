namespace ERP.Core.Contabilidad.Forms
{
    partial class frmdocaux
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough]
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DtgDocs = new System.Windows.Forms.DataGridView();
            this.clmClase = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmObligacion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmfecVence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmValInicial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmsaldo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.txtCuenta = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.txtnit = new System.Windows.Forms.TextBox();
            this.TxtClaseAux = new System.Windows.Forms.TextBox();
            this.TxtDocAux = new System.Windows.Forms.TextBox();
            this.Label14 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.DtFecVence = new System.Windows.Forms.DateTimePicker();
            this.CmbGrabar = new System.Windows.Forms.Button();
            this.Label4 = new System.Windows.Forms.Label();
            this.TxtPeriodo = new System.Windows.Forms.TextBox();
            this.Label5 = new System.Windows.Forms.Label();
            this.TxtDetalle = new System.Windows.Forms.TextBox();
            this.txtCredito = new System.Windows.Forms.TextBox();
            this.Label7 = new System.Windows.Forms.Label();
            this.txtDebito = new System.Windows.Forms.TextBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.LblTotal = new System.Windows.Forms.Label();
            this.Label8 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DtgDocs)).BeginInit();
            this.SuspendLayout();
            //
            // DtgDocs
            //
            this.DtgDocs.AllowUserToAddRows = false;
            this.DtgDocs.AllowUserToDeleteRows = false;
            this.DtgDocs.AllowUserToResizeColumns = false;
            this.DtgDocs.AllowUserToResizeRows = false;
            this.DtgDocs.BackgroundColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.DtgDocs.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DtgDocs.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DtgDocs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DtgDocs.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmClase, this.clmObligacion, this.clmfecVence, this.clmValInicial, this.clmsaldo });
            this.DtgDocs.GridColor = System.Drawing.SystemColors.AppWorkspace;
            this.DtgDocs.Location = new System.Drawing.Point(5, 44);
            this.DtgDocs.Name = "DtgDocs";
            this.DtgDocs.ReadOnly = true;
            this.DtgDocs.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Sunken;
            this.DtgDocs.RowHeadersVisible = false;
            DataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DtgDocs.RowsDefaultCellStyle = DataGridViewCellStyle2;
            this.DtgDocs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DtgDocs.Size = new System.Drawing.Size(526, 236);
            this.DtgDocs.StandardTab = true;
            this.DtgDocs.TabIndex = 7;
            this.DtgDocs.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DtgDocs_CellContentClick);
            this.DtgDocs.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.DtgDocs_CellMouseDoubleClick);
            this.DtgDocs.CellStateChanged += new System.Windows.Forms.DataGridViewCellStateChangedEventHandler(this.DtgDocs_CellStateChanged);
            this.DtgDocs.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.DtgDocs_CellValueChanged);
            this.DtgDocs.CurrentCellChanged += new System.EventHandler(this.DtgDocs_CurrentCellChanged);
            this.DtgDocs.DockChanged += new System.EventHandler(this.DtgDocs_DockChanged);
            this.DtgDocs.LocationChanged += new System.EventHandler(this.DtgDocs_LocationChanged);
            this.DtgDocs.MouseCaptureChanged += new System.EventHandler(this.DtgDocs_MouseCaptureChanged);
            this.DtgDocs.MultiSelectChanged += new System.EventHandler(this.DtgDocs_MultiSelectChanged);
            //
            // clmClase
            //
            this.clmClase.HeaderText = "Clase";
            this.clmClase.Name = "clmClase";
            this.clmClase.ReadOnly = true;
            //
            // clmObligacion
            //
            this.clmObligacion.HeaderText = "No. Obligaciòn";
            this.clmObligacion.Name = "clmObligacion";
            this.clmObligacion.ReadOnly = true;
            //
            // clmfecVence
            //
            this.clmfecVence.HeaderText = "Fecha Vence";
            this.clmfecVence.Name = "clmfecVence";
            this.clmfecVence.ReadOnly = true;
            //
            // clmValInicial
            //
            this.clmValInicial.HeaderText = "Val. Inicial";
            this.clmValInicial.Name = "clmValInicial";
            this.clmValInicial.ReadOnly = true;
            //
            // clmsaldo
            //
            this.clmsaldo.HeaderText = "Saldo";
            this.clmsaldo.Name = "clmsaldo";
            this.clmsaldo.ReadOnly = true;
            //
            // txtCuenta
            //
            this.txtCuenta.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.txtCuenta.Enabled = false;
            this.txtCuenta.Location = new System.Drawing.Point(60, 13);
            this.txtCuenta.Margin = new System.Windows.Forms.Padding(4);
            this.txtCuenta.MaxLength = 4;
            this.txtCuenta.Name = "txtCuenta";
            this.txtCuenta.Size = new System.Drawing.Size(136, 20);
            this.txtCuenta.TabIndex = 291;
            //
            // Label1
            //
            this.Label1.Location = new System.Drawing.Point(2, 13);
            this.Label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(56, 20);
            this.Label1.TabIndex = 292;
            this.Label1.Text = "Cuenta";
            //
            // Label2
            //
            this.Label2.Location = new System.Drawing.Point(206, 15);
            this.Label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(44, 17);
            this.Label2.TabIndex = 292;
            this.Label2.Text = "Nit.";
            //
            // txtnit
            //
            this.txtnit.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.txtnit.Enabled = false;
            this.txtnit.Location = new System.Drawing.Point(229, 13);
            this.txtnit.Margin = new System.Windows.Forms.Padding(4);
            this.txtnit.MaxLength = 4;
            this.txtnit.Name = "txtnit";
            this.txtnit.Size = new System.Drawing.Size(114, 20);
            this.txtnit.TabIndex = 291;
            //
            // TxtClaseAux
            //
            this.TxtClaseAux.Location = new System.Drawing.Point(73, 292);
            this.TxtClaseAux.Margin = new System.Windows.Forms.Padding(4);
            this.TxtClaseAux.MaxLength = 4;
            this.TxtClaseAux.Name = "TxtClaseAux";
            this.TxtClaseAux.Size = new System.Drawing.Size(33, 20);
            this.TxtClaseAux.TabIndex = 0;
            //
            // TxtDocAux
            //
            this.TxtDocAux.Location = new System.Drawing.Point(114, 292);
            this.TxtDocAux.Margin = new System.Windows.Forms.Padding(4);
            this.TxtDocAux.MaxLength = 10;
            this.TxtDocAux.Name = "TxtDocAux";
            this.TxtDocAux.Size = new System.Drawing.Size(89, 20);
            this.TxtDocAux.TabIndex = 1;
            this.TxtDocAux.TextChanged += new System.EventHandler(this.TxtDocAux_TextChanged);
            this.TxtDocAux.LostFocus += new System.EventHandler(this.TxtDocAux_LostFocus);
            //
            // Label14
            //
            this.Label14.Location = new System.Drawing.Point(2, 294);
            this.Label14.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label14.Name = "Label14";
            this.Label14.Size = new System.Drawing.Size(94, 19);
            this.Label14.TabIndex = 295;
            this.Label14.Text = "Doc. Auxiliar";
            //
            // Label3
            //
            this.Label3.Location = new System.Drawing.Point(2, 318);
            this.Label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(73, 19);
            this.Label3.TabIndex = 295;
            this.Label3.Text = "Fec. Vence";
            //
            // DtFecVence
            //
            this.DtFecVence.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtFecVence.Location = new System.Drawing.Point(73, 314);
            this.DtFecVence.Name = "DtFecVence";
            this.DtFecVence.Size = new System.Drawing.Size(91, 20);
            this.DtFecVence.TabIndex = 2;
            //
            // CmbGrabar
            //
            this.CmbGrabar.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.CmbGrabar.Location = new System.Drawing.Point(431, 320);
            this.CmbGrabar.Margin = new System.Windows.Forms.Padding(4);
            this.CmbGrabar.Name = "CmbGrabar";
            this.CmbGrabar.Size = new System.Drawing.Size(75, 30);
            this.CmbGrabar.TabIndex = 4;
            this.CmbGrabar.Text = "Grabar";
            this.CmbGrabar.Click += new System.EventHandler(this.CmbGrabar_Click);
            //
            // Label4
            //
            this.Label4.Location = new System.Drawing.Point(410, 13);
            this.Label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(44, 17);
            this.Label4.TabIndex = 292;
            this.Label4.Text = "Periodo";
            //
            // TxtPeriodo
            //
            this.TxtPeriodo.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.TxtPeriodo.Enabled = false;
            this.TxtPeriodo.Location = new System.Drawing.Point(462, 12);
            this.TxtPeriodo.Margin = new System.Windows.Forms.Padding(4);
            this.TxtPeriodo.MaxLength = 4;
            this.TxtPeriodo.Name = "TxtPeriodo";
            this.TxtPeriodo.Size = new System.Drawing.Size(68, 20);
            this.TxtPeriodo.TabIndex = 291;
            //
            // Label5
            //
            this.Label5.Location = new System.Drawing.Point(2, 338);
            this.Label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(56, 19);
            this.Label5.TabIndex = 295;
            this.Label5.Text = "Detalle";
            //
            // TxtDetalle
            //
            this.TxtDetalle.Location = new System.Drawing.Point(73, 337);
            this.TxtDetalle.Margin = new System.Windows.Forms.Padding(4);
            this.TxtDetalle.MaxLength = 40;
            this.TxtDetalle.Name = "TxtDetalle";
            this.TxtDetalle.Size = new System.Drawing.Size(297, 20);
            this.TxtDetalle.TabIndex = 3;
            //
            // txtCredito
            //
            this.txtCredito.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.txtCredito.Enabled = false;
            this.txtCredito.Location = new System.Drawing.Point(413, 292);
            this.txtCredito.Margin = new System.Windows.Forms.Padding(4);
            this.txtCredito.MaxLength = 0;
            this.txtCredito.Name = "txtCredito";
            this.txtCredito.Size = new System.Drawing.Size(93, 20);
            this.txtCredito.TabIndex = 299;
            this.txtCredito.Text = "0";
            this.txtCredito.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label7
            //
            this.Label7.Location = new System.Drawing.Point(361, 291);
            this.Label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(44, 20);
            this.Label7.TabIndex = 301;
            this.Label7.Text = "Credito";
            //
            // txtDebito
            //
            this.txtDebito.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.txtDebito.Enabled = false;
            this.txtDebito.Location = new System.Drawing.Point(263, 292);
            this.txtDebito.Margin = new System.Windows.Forms.Padding(4);
            this.txtDebito.MaxLength = 0;
            this.txtDebito.Name = "txtDebito";
            this.txtDebito.Size = new System.Drawing.Size(93, 20);
            this.txtDebito.TabIndex = 298;
            this.txtDebito.Text = "0";
            this.txtDebito.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label6
            //
            this.Label6.Location = new System.Drawing.Point(211, 292);
            this.Label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(44, 20);
            this.Label6.TabIndex = 300;
            this.Label6.Text = "Debito";
            //
            // LblTotal
            //
            this.LblTotal.BackColor = System.Drawing.Color.Gainsboro;
            this.LblTotal.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblTotal.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblTotal.Location = new System.Drawing.Point(285, 313);
            this.LblTotal.Name = "LblTotal";
            this.LblTotal.Size = new System.Drawing.Size(95, 21);
            this.LblTotal.TabIndex = 302;
            this.LblTotal.Text = "0";
            this.LblTotal.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label8
            //
            this.Label8.Location = new System.Drawing.Point(169, 316);
            this.Label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(110, 19);
            this.Label8.TabIndex = 303;
            this.Label8.Text = "Total Seleccionadas";
            //
            // frmdocaux
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(540, 366);
            this.ControlBox = false;
            this.Controls.Add(this.Label8);
            this.Controls.Add(this.LblTotal);
            this.Controls.Add(this.txtCredito);
            this.Controls.Add(this.Label7);
            this.Controls.Add(this.txtDebito);
            this.Controls.Add(this.Label6);
            this.Controls.Add(this.CmbGrabar);
            this.Controls.Add(this.DtFecVence);
            this.Controls.Add(this.TxtDetalle);
            this.Controls.Add(this.TxtClaseAux);
            this.Controls.Add(this.TxtDocAux);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.Label14);
            this.Controls.Add(this.TxtPeriodo);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.txtnit);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.txtCuenta);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.DtgDocs);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.Name = "frmdocaux";
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.frmfacturas_KeyUp);
            this.Load += new System.EventHandler(this.frmfacturas_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DtgDocs)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        internal System.Windows.Forms.DataGridView DtgDocs;
        internal System.Windows.Forms.TextBox txtCuenta;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.TextBox txtnit;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmClase;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmObligacion;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmfecVence;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmValInicial;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmsaldo;
        internal System.Windows.Forms.TextBox TxtClaseAux;
        internal System.Windows.Forms.TextBox TxtDocAux;
        internal System.Windows.Forms.Label Label14;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.DateTimePicker DtFecVence;
        internal System.Windows.Forms.Button CmbGrabar;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.TextBox TxtPeriodo;
        internal System.Windows.Forms.Label Label5;
        internal System.Windows.Forms.TextBox TxtDetalle;
        internal System.Windows.Forms.TextBox txtCredito;
        internal System.Windows.Forms.Label Label7;
        internal System.Windows.Forms.TextBox txtDebito;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.Label LblTotal;
        internal System.Windows.Forms.Label Label8;
    }
}
