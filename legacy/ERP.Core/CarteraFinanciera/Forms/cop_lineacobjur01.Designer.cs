namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class cop_lineacobjur01
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
            try
            {
                if (disposing && (components != null))
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DtgDocs = new System.Windows.Forms.DataGridView();
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.LblNombre = new System.Windows.Forms.Label();
            this.BtnAceptar = new System.Windows.Forms.Button();
            this.LblPeriodo = new System.Windows.Forms.Label();
            this.clmLinea = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmObligacion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmdiasMora = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.cmlCobro = new System.Windows.Forms.DataGridViewTextBoxColumn();
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
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DtgDocs.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DtgDocs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DtgDocs.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmLinea, this.clmObligacion, this.clmdiasMora, this.cmlCobro });
            this.DtgDocs.EnableHeadersVisualStyles = false;
            this.DtgDocs.GridColor = System.Drawing.SystemColors.AppWorkspace;
            this.DtgDocs.Location = new System.Drawing.Point(28, 65);
            this.DtgDocs.MultiSelect = false;
            this.DtgDocs.Name = "DtgDocs";
            this.DtgDocs.ReadOnly = true;
            this.DtgDocs.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Sunken;
            this.DtgDocs.RowHeadersVisible = false;
            DataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle6.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            this.DtgDocs.RowsDefaultCellStyle = DataGridViewCellStyle6;
            this.DtgDocs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DtgDocs.Size = new System.Drawing.Size(425, 236);
            this.DtgDocs.StandardTab = true;
            this.DtgDocs.TabIndex = 8;
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.BackColor = System.Drawing.SystemColors.Window;
            this.TxtCodigoter.Location = new System.Drawing.Point(69, 23);
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.ReadOnly = true;
            this.TxtCodigoter.Size = new System.Drawing.Size(127, 20);
            this.TxtCodigoter.TabIndex = 9;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(8, 26);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(55, 16);
            this.Label1.TabIndex = 10;
            this.Label1.Text = "Codigo ";
            //
            // LblNombre
            //
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNombre.Location = new System.Drawing.Point(202, 23);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(277, 25);
            this.LblNombre.TabIndex = 11;
            //
            // BtnAceptar
            //
            this.BtnAceptar.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BtnAceptar.Location = new System.Drawing.Point(195, 313);
            this.BtnAceptar.Name = "BtnAceptar";
            this.BtnAceptar.Size = new System.Drawing.Size(75, 30);
            this.BtnAceptar.TabIndex = 12;
            this.BtnAceptar.Text = "Aceptar";
            this.BtnAceptar.UseVisualStyleBackColor = true;
            this.BtnAceptar.Click += new System.EventHandler(this.BtnAceptar_Click);
            //
            // LblPeriodo
            //
            this.LblPeriodo.Location = new System.Drawing.Point(32, 315);
            this.LblPeriodo.Name = "LblPeriodo";
            this.LblPeriodo.Size = new System.Drawing.Size(100, 23);
            this.LblPeriodo.TabIndex = 13;
            this.LblPeriodo.Text = "periodo";
            this.LblPeriodo.Visible = false;
            //
            // clmLinea
            //
            this.clmLinea.DataPropertyName = "linea";
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle2.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clmLinea.DefaultCellStyle = DataGridViewCellStyle2;
            this.clmLinea.HeaderText = "Linea";
            this.clmLinea.Name = "clmLinea";
            this.clmLinea.ReadOnly = true;
            this.clmLinea.ToolTipText = "Linea de credito";
            this.clmLinea.Width = 50;
            //
            // clmObligacion
            //
            this.clmObligacion.DataPropertyName = "Obligacion";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clmObligacion.DefaultCellStyle = DataGridViewCellStyle3;
            this.clmObligacion.HeaderText = "No. Obligacion";
            this.clmObligacion.Name = "clmObligacion";
            this.clmObligacion.ReadOnly = true;
            this.clmObligacion.ToolTipText = "Obligacion";
            this.clmObligacion.Width = 130;
            //
            // clmdiasMora
            //
            this.clmdiasMora.DataPropertyName = "DiasMora";
            DataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clmdiasMora.DefaultCellStyle = DataGridViewCellStyle4;
            this.clmdiasMora.HeaderText = "Dias Mora";
            this.clmdiasMora.Name = "clmdiasMora";
            this.clmdiasMora.ReadOnly = true;
            this.clmdiasMora.ToolTipText = "Dias en Mora";
            //
            // cmlCobro
            //
            this.cmlCobro.DataPropertyName = "CobroJuridico";
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.cmlCobro.DefaultCellStyle = DataGridViewCellStyle5;
            this.cmlCobro.HeaderText = "Cobro Juridico";
            this.cmlCobro.Name = "cmlCobro";
            this.cmlCobro.ReadOnly = true;
            this.cmlCobro.Width = 120;
            //
            // cop_lineacobjur01
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(492, 357);
            this.Controls.Add(this.LblPeriodo);
            this.Controls.Add(this.BtnAceptar);
            this.Controls.Add(this.LblNombre);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.TxtCodigoter);
            this.Controls.Add(this.DtgDocs);
            this.Name = "cop_lineacobjur01";
            this.Text = "Obligaciones de asociados con cobro juridico";
            this.Load += new System.EventHandler(this.cop_lineacobjur01_Load);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.cop_lineacobjur01_KeyUp);
            ((System.ComponentModel.ISupportInitialize)(this.DtgDocs)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView DtgDocs;
        public System.Windows.Forms.TextBox TxtCodigoter;
        private System.Windows.Forms.Label Label1;
        public System.Windows.Forms.Label LblNombre;
        private System.Windows.Forms.Button BtnAceptar;
        public System.Windows.Forms.Label LblPeriodo;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmLinea;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmObligacion;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmdiasMora;
        private System.Windows.Forms.DataGridViewTextBoxColumn cmlCobro;
    }
}
