namespace ERP.Core.Nomina.Forms
{
    partial class nom_frmayuausen
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DgwAusentismo = new System.Windows.Forms.DataGridView();
            this.clmconsecutivo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmFecInicial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmFecFinal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CmbSalir = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.DgwAusentismo)).BeginInit();
            this.SuspendLayout();
            //
            // DgwAusentismo
            //
            this.DgwAusentismo.AllowUserToAddRows = false;
            this.DgwAusentismo.AllowUserToDeleteRows = false;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle5.BackColor = System.Drawing.Color.LightSlateGray;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwAusentismo.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.DgwAusentismo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwAusentismo.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmconsecutivo, this.clmFecInicial, this.clmFecFinal });
            this.DgwAusentismo.EnableHeadersVisualStyles = false;
            this.DgwAusentismo.Location = new System.Drawing.Point(12, 49);
            this.DgwAusentismo.Name = "DgwAusentismo";
            this.DgwAusentismo.RowHeadersVisible = false;
            this.DgwAusentismo.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwAusentismo.Size = new System.Drawing.Size(368, 150);
            this.DgwAusentismo.TabIndex = 0;
            this.DgwAusentismo.DoubleClick += new System.EventHandler(this.DgwAusentismo_DoubleClick);
            //
            // clmconsecutivo
            //
            this.clmconsecutivo.DataPropertyName = "Consecutivo";
            this.clmconsecutivo.HeaderText = "No.";
            this.clmconsecutivo.Name = "clmconsecutivo";
            //
            // clmFecInicial
            //
            this.clmFecInicial.DataPropertyName = "FecInicial";
            this.clmFecInicial.HeaderText = "Fec. Inicial";
            this.clmFecInicial.Name = "clmFecInicial";
            this.clmFecInicial.Width = 120;
            //
            // clmFecFinal
            //
            this.clmFecFinal.DataPropertyName = "FecFinal";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle6.BackColor = System.Drawing.Color.Ivory;
            this.clmFecFinal.DefaultCellStyle = dataGridViewCellStyle6;
            this.clmFecFinal.HeaderText = "Fec. Final";
            this.clmFecFinal.Name = "clmFecFinal";
            this.clmFecFinal.Width = 120;
            //
            // CmbSalir
            //
            this.CmbSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.CmbSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbSalir.Location = new System.Drawing.Point(330, 206);
            this.CmbSalir.Margin = new System.Windows.Forms.Padding(4);
            this.CmbSalir.Name = "CmbSalir";
            this.CmbSalir.Size = new System.Drawing.Size(50, 40);
            this.CmbSalir.TabIndex = 6;
            this.CmbSalir.TabStop = false;
            this.CmbSalir.Click += new System.EventHandler(this.CmbSalir_Click);
            //
            // nom_frmayuausen
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0F, 13.0F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(398, 256);
            this.Controls.Add(this.CmbSalir);
            this.Controls.Add(this.DgwAusentismo);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "nom_frmayuausen";
            this.Text = "Ausentismos por Empleado";
            ((System.ComponentModel.ISupportInitialize)(this.DgwAusentismo)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.DataGridView DgwAusentismo;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmconsecutivo;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmFecInicial;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmFecFinal;
        private System.Windows.Forms.Button CmbSalir;
    }
}
