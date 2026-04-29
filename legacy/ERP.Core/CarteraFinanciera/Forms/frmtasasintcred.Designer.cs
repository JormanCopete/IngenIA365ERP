namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmtasasintcred
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            this.Label1 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.DgvTasas = new System.Windows.Forms.DataGridView();
            this.ClmVlrIni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmVlrFinal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmPlazoIni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmPlazoFin = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmantinicial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmantfinal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnGarantia = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmTasa = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PlazoMaximo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MontoMaximo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.BtnGrabar = new System.Windows.Forms.Button();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.lblInformacionGarantia = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DgvTasas)).BeginInit();
            this.SuspendLayout();
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(124, 4);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(518, 20);
            this.Label1.TabIndex = 7;
            this.Label1.Text = "TASAS DE INTERES POR ANTIGUEDAD, PLAZOS Y MONTOS";
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(-12, 20);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(784, 12);
            this.GroupBox1.TabIndex = 8;
            this.GroupBox1.TabStop = false;
            //
            // DgvTasas
            //
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgvTasas.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.DgvTasas.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvTasas.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ClmVlrIni,
            this.ClmVlrFinal,
            this.ClmPlazoIni,
            this.ClmPlazoFin,
            this.clmantinicial,
            this.clmantfinal,
            this.ColumnGarantia,
            this.ClmTasa,
            this.PlazoMaximo,
            this.MontoMaximo});
            this.DgvTasas.EnableHeadersVisualStyles = false;
            this.DgvTasas.Location = new System.Drawing.Point(14, 42);
            this.DgvTasas.Name = "DgvTasas";
            this.DgvTasas.RowHeadersVisible = false;
            this.DgvTasas.Size = new System.Drawing.Size(740, 201);
            this.DgvTasas.TabIndex = 9;
            this.DgvTasas.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.DgvTasas_DataError);
            //
            // ClmVlrIni
            //
            this.ClmVlrIni.DataPropertyName = "vlrinicial";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle2.Format = "N2";
            this.ClmVlrIni.DefaultCellStyle = dataGridViewCellStyle2;
            this.ClmVlrIni.HeaderText = "Monto Inicial";
            this.ClmVlrIni.Name = "ClmVlrIni";
            this.ClmVlrIni.Width = 120;
            //
            // ClmVlrFinal
            //
            this.ClmVlrFinal.DataPropertyName = "vlrfinal";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle3.Format = "N2";
            this.ClmVlrFinal.DefaultCellStyle = dataGridViewCellStyle3;
            this.ClmVlrFinal.HeaderText = "Monto Final";
            this.ClmVlrFinal.Name = "ClmVlrFinal";
            this.ClmVlrFinal.Width = 120;
            //
            // ClmPlazoIni
            //
            this.ClmPlazoIni.DataPropertyName = "plazoinicial";
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle4.Format = "N0";
            this.ClmPlazoIni.DefaultCellStyle = dataGridViewCellStyle4;
            this.ClmPlazoIni.HeaderText = "Plazo Inicial";
            this.ClmPlazoIni.Name = "ClmPlazoIni";
            //
            // ClmPlazoFin
            //
            this.ClmPlazoFin.DataPropertyName = "plazofinal";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle5.Format = "N0";
            this.ClmPlazoFin.DefaultCellStyle = dataGridViewCellStyle5;
            this.ClmPlazoFin.HeaderText = "Plazo Final";
            this.ClmPlazoFin.Name = "ClmPlazoFin";
            //
            // clmantinicial
            //
            this.clmantinicial.DataPropertyName = "antinicial";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle6.Format = "N0";
            dataGridViewCellStyle6.NullValue = null;
            this.clmantinicial.DefaultCellStyle = dataGridViewCellStyle6;
            this.clmantinicial.HeaderText = "Ant. Inicial";
            this.clmantinicial.Name = "clmantinicial";
            //
            // clmantfinal
            //
            this.clmantfinal.DataPropertyName = "antfinal";
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle7.Format = "N0";
            dataGridViewCellStyle7.NullValue = null;
            this.clmantfinal.DefaultCellStyle = dataGridViewCellStyle7;
            this.clmantfinal.HeaderText = "Ant. Final";
            this.clmantfinal.Name = "clmantfinal";
            //
            // ColumnGarantia
            //
            this.ColumnGarantia.DataPropertyName = "garantia";
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ColumnGarantia.DefaultCellStyle = dataGridViewCellStyle8;
            this.ColumnGarantia.HeaderText = "Garantia";
            this.ColumnGarantia.Name = "ColumnGarantia";
            this.ColumnGarantia.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnGarantia.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            //
            // ClmTasa
            //
            this.ClmTasa.DataPropertyName = "tasa";
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle9.Format = "N3";
            this.ClmTasa.DefaultCellStyle = dataGridViewCellStyle9;
            this.ClmTasa.HeaderText = "Tasa";
            this.ClmTasa.Name = "ClmTasa";
            this.ClmTasa.Width = 70;
            //
            // PlazoMaximo
            //
            this.PlazoMaximo.DataPropertyName = "PlazoMaximo";
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle10.NullValue = "0";
            this.PlazoMaximo.DefaultCellStyle = dataGridViewCellStyle10;
            this.PlazoMaximo.HeaderText = "Plazo Maximo";
            this.PlazoMaximo.Name = "PlazoMaximo";
            //
            // MontoMaximo
            //
            this.MontoMaximo.DataPropertyName = "MontoMaximo";
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle11.NullValue = "0";
            this.MontoMaximo.DefaultCellStyle = dataGridViewCellStyle11;
            this.MontoMaximo.HeaderText = "MontoMaximo";
            this.MontoMaximo.Name = "MontoMaximo";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(12, 255);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(173, 13);
            this.Label2.TabIndex = 11;
            this.Label2.Text = "* Los plazos estan dados en meses";
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(12, 275);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(180, 13);
            this.Label3.TabIndex = 12;
            this.Label3.Text = "* La antiguedad esta dada en meses";
            //
            // BtnGrabar
            //
            this.BtnGrabar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnGrabar.Location = new System.Drawing.Point(649, 249);
            this.BtnGrabar.Name = "BtnGrabar";
            this.BtnGrabar.Size = new System.Drawing.Size(50, 40);
            this.BtnGrabar.TabIndex = 14;
            this.BtnGrabar.Text = "Grabar";
            this.BtnGrabar.UseVisualStyleBackColor = true;
            this.BtnGrabar.Click += new System.EventHandler(this.BtnGrabar_Click);
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(704, 249);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(50, 40);
            this.BtnSalir.TabIndex = 13;
            this.BtnSalir.Text = "Salir";
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // lblInformacionGarantia
            //
            this.lblInformacionGarantia.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblInformacionGarantia.Location = new System.Drawing.Point(211, 246);
            this.lblInformacionGarantia.Name = "lblInformacionGarantia";
            this.lblInformacionGarantia.Size = new System.Drawing.Size(143, 78);
            this.lblInformacionGarantia.TabIndex = 15;
            this.lblInformacionGarantia.Text = "Garantia \r\n0 - No Aplica\r\n1 - Sin  Codeudor\r\n2 - Con Codeudor\r\n3 - Con  Garantia Real\r\n4 - Aportes";
            //
            // frmtasasintcred
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(766, 326);
            this.ControlBox = false;
            this.Controls.Add(this.lblInformacionGarantia);
            this.Controls.Add(this.BtnGrabar);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.DgvTasas);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.GroupBox1);
            this.Name = "frmtasasintcred";
            this.Text = "Tasas de Interes por Credito";
            ((System.ComponentModel.ISupportInitialize)(this.DgvTasas)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.DataGridView DgvTasas;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.Button BtnGrabar;
        internal System.Windows.Forms.Button BtnSalir;
        internal System.Windows.Forms.Label lblInformacionGarantia;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmVlrIni;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmVlrFinal;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmPlazoIni;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmPlazoFin;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmantinicial;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmantfinal;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ColumnGarantia;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmTasa;
        internal System.Windows.Forms.DataGridViewTextBoxColumn PlazoMaximo;
        internal System.Windows.Forms.DataGridViewTextBoxColumn MontoMaximo;
    }
}
