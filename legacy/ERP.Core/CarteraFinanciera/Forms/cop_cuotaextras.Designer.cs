namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class cop_cuotaextras
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
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            this.Dtgextras = new System.Windows.Forms.DataGridView();
            this.clmfecha = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Clmvalor = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmforpag = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmtipopago = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmclase = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Txtvalor = new System.Windows.Forms.TextBox();
            this.Dtpfecha = new System.Windows.Forms.DateTimePicker();
            this.cbxformaDsto = new System.Windows.Forms.ComboBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.CmbGuardar = new System.Windows.Forms.Button();
            this.CmbSalir = new System.Windows.Forms.Button();
            this.cmbeliminar = new System.Windows.Forms.Button();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.CbxClaExtra = new System.Windows.Forms.ComboBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.dtpVali = new System.Windows.Forms.DateTimePicker();
            this.Label5 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.Dtgextras)).BeginInit();
            this.SuspendLayout();
            //
            // Dtgextras
            //
            this.Dtgextras.AllowUserToAddRows = false;
            this.Dtgextras.AllowUserToDeleteRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.Dtgextras.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.Dtgextras.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.Dtgextras.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmfecha, this.Clmvalor, this.clmforpag, this.clmtipopago, this.clmclase });
            this.Dtgextras.Location = new System.Drawing.Point(12, 116);
            this.Dtgextras.Name = "Dtgextras";
            this.Dtgextras.ReadOnly = true;
            this.Dtgextras.RowHeadersVisible = false;
            this.Dtgextras.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.Dtgextras.Size = new System.Drawing.Size(362, 158);
            this.Dtgextras.StandardTab = true;
            this.Dtgextras.TabIndex = 3;
            this.Dtgextras.KeyDown += new System.Windows.Forms.KeyEventHandler(this.cop_cuotaextras_KeyDown);
            //
            // clmfecha
            //
            this.clmfecha.DataPropertyName = "fecha";
            DataGridViewCellStyle2.Format = "d";
            DataGridViewCellStyle2.NullValue = null;
            this.clmfecha.DefaultCellStyle = DataGridViewCellStyle2;
            this.clmfecha.HeaderText = "Fecha";
            this.clmfecha.Name = "clmfecha";
            this.clmfecha.ReadOnly = true;
            this.clmfecha.Width = 90;
            //
            // Clmvalor
            //
            this.Clmvalor.DataPropertyName = "valor";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle3.Format = "N0";
            DataGridViewCellStyle3.NullValue = "0";
            this.Clmvalor.DefaultCellStyle = DataGridViewCellStyle3;
            this.Clmvalor.HeaderText = "Valor";
            this.Clmvalor.Name = "Clmvalor";
            this.Clmvalor.ReadOnly = true;
            //
            // clmforpag
            //
            this.clmforpag.DataPropertyName = "DescPag";
            DataGridViewCellStyle4.NullValue = null;
            this.clmforpag.DefaultCellStyle = DataGridViewCellStyle4;
            this.clmforpag.HeaderText = "Forma Pago";
            this.clmforpag.Name = "clmforpag";
            this.clmforpag.ReadOnly = true;
            this.clmforpag.Width = 90;
            //
            // clmtipopago
            //
            this.clmtipopago.DataPropertyName = "forpag";
            this.clmtipopago.HeaderText = "Tipo pago";
            this.clmtipopago.Name = "clmtipopago";
            this.clmtipopago.ReadOnly = true;
            this.clmtipopago.Visible = false;
            //
            // clmclase
            //
            this.clmclase.DataPropertyName = "tipoextra";
            this.clmclase.HeaderText = "Clase";
            this.clmclase.Name = "clmclase";
            this.clmclase.ReadOnly = true;
            this.clmclase.Width = 50;
            //
            // Txtvalor
            //
            this.Txtvalor.Location = new System.Drawing.Point(122, 42);
            this.Txtvalor.Name = "Txtvalor";
            this.Txtvalor.Size = new System.Drawing.Size(100, 20);
            this.Txtvalor.TabIndex = 1;
            this.Txtvalor.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.Txtvalor.LostFocus += new System.EventHandler(this.Txtvalor_LostFocus);
            //
            // Dtpfecha
            //
            this.Dtpfecha.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.Dtpfecha.Location = new System.Drawing.Point(11, 41);
            this.Dtpfecha.Name = "Dtpfecha";
            this.Dtpfecha.Size = new System.Drawing.Size(89, 20);
            this.Dtpfecha.TabIndex = 0;
            //
            // cbxformaDsto
            //
            this.cbxformaDsto.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxformaDsto.IntegralHeight = false;
            this.cbxformaDsto.Items.AddRange(new object[] { "", "1-Pago por Nomina", "2-pago Por Caja" });
            this.cbxformaDsto.Location = new System.Drawing.Point(256, 43);
            this.cbxformaDsto.Margin = new System.Windows.Forms.Padding(4);
            this.cbxformaDsto.Name = "cbxformaDsto";
            this.cbxformaDsto.Size = new System.Drawing.Size(117, 21);
            this.cbxformaDsto.TabIndex = 2;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(28, 25);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(37, 13);
            this.Label1.TabIndex = 8;
            this.Label1.Text = "Fecha";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(146, 26);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(31, 13);
            this.Label2.TabIndex = 9;
            this.Label2.Text = "Valor";
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(280, 26);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(61, 13);
            this.Label3.TabIndex = 10;
            this.Label3.Text = "Forma Dsto";
            //
            // CmbGuardar
            //
            this.CmbGuardar.AutoSize = true;
            this.CmbGuardar.BackgroundImage = global::ERP.Core.Properties.Resources.disco;
            this.CmbGuardar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbGuardar.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbGuardar.Location = new System.Drawing.Point(293, 75);
            this.CmbGuardar.Name = "CmbGuardar";
            this.CmbGuardar.Size = new System.Drawing.Size(38, 35);
            this.CmbGuardar.TabIndex = 4;
            this.ToolTip1.SetToolTip(this.CmbGuardar, "Guardar");
            this.CmbGuardar.UseVisualStyleBackColor = true;
            this.CmbGuardar.Click += new System.EventHandler(this.CmbGuardar_Click);
            //
            // CmbSalir
            //
            this.CmbSalir.AutoSize = true;
            this.CmbSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.CmbSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbSalir.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.CmbSalir.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Olive;
            this.CmbSalir.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.CmbSalir.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.CmbSalir.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbSalir.Location = new System.Drawing.Point(337, 75);
            this.CmbSalir.Name = "CmbSalir";
            this.CmbSalir.Size = new System.Drawing.Size(38, 35);
            this.CmbSalir.TabIndex = 5;
            this.ToolTip1.SetToolTip(this.CmbSalir, "Salir");
            this.CmbSalir.UseVisualStyleBackColor = true;
            this.CmbSalir.Click += new System.EventHandler(this.CmbSalir_Click);
            //
            // cmbeliminar
            //
            this.cmbeliminar.AutoSize = true;
            this.cmbeliminar.BackgroundImage = global::ERP.Core.Properties.Resources.Eliminar;
            this.cmbeliminar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.cmbeliminar.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.cmbeliminar.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Olive;
            this.cmbeliminar.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.cmbeliminar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cmbeliminar.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbeliminar.Location = new System.Drawing.Point(249, 75);
            this.cmbeliminar.Name = "cmbeliminar";
            this.cmbeliminar.Size = new System.Drawing.Size(38, 35);
            this.cmbeliminar.TabIndex = 6;
            this.ToolTip1.SetToolTip(this.cmbeliminar, "Eliminar");
            this.cmbeliminar.UseVisualStyleBackColor = true;
            this.cmbeliminar.Visible = false;
            //
            // CbxClaExtra
            //
            this.CbxClaExtra.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxClaExtra.FormattingEnabled = true;
            this.CbxClaExtra.Items.AddRange(new object[] { "", "Primas", "Cesantias", "Vacaciones", "Quincena" });
            this.CbxClaExtra.Location = new System.Drawing.Point(11, 84);
            this.CbxClaExtra.Name = "CbxClaExtra";
            this.CbxClaExtra.Size = new System.Drawing.Size(121, 21);
            this.CbxClaExtra.TabIndex = 3;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(28, 68);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(60, 13);
            this.Label4.TabIndex = 12;
            this.Label4.Text = "Clase Extra";
            //
            // dtpVali
            //
            this.dtpVali.Enabled = false;
            this.dtpVali.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpVali.Location = new System.Drawing.Point(139, 84);
            this.dtpVali.Name = "dtpVali";
            this.dtpVali.Size = new System.Drawing.Size(83, 20);
            this.dtpVali.TabIndex = 13;
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(138, 68);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(84, 13);
            this.Label5.TabIndex = 14;
            this.Label5.Text = "Fecha Ult Cuota";
            //
            // cop_cuotaextras
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(386, 286);
            this.ControlBox = false;
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.dtpVali);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.CbxClaExtra);
            this.Controls.Add(this.cmbeliminar);
            this.Controls.Add(this.CmbGuardar);
            this.Controls.Add(this.CmbSalir);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.cbxformaDsto);
            this.Controls.Add(this.Dtpfecha);
            this.Controls.Add(this.Txtvalor);
            this.Controls.Add(this.Dtgextras);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.Name = "cop_cuotaextras";
            this.Load += new System.EventHandler(this.cop_extras_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.cop_cuotaextras_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.Dtgextras)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.DataGridView Dtgextras;
        private System.Windows.Forms.TextBox Txtvalor;
        private System.Windows.Forms.DateTimePicker Dtpfecha;
        private System.Windows.Forms.ComboBox cbxformaDsto;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Button CmbGuardar;
        private System.Windows.Forms.Button CmbSalir;
        private System.Windows.Forms.Button cmbeliminar;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.ComboBox CbxClaExtra;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmfecha;
        private System.Windows.Forms.DataGridViewTextBoxColumn Clmvalor;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmforpag;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmtipopago;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmclase;
        private System.Windows.Forms.DateTimePicker dtpVali;
        private System.Windows.Forms.Label Label5;
    }
}
