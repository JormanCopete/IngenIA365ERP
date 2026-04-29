namespace ERP.Core.Seguridad.Forms
{
    partial class frmempbloq
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
            this.lblempbloqueda = new System.Windows.Forms.Label();
            this.pnlEmpbloq = new System.Windows.Forms.Panel();
            this.dtgEmpBloq = new System.Windows.Forms.DataGridView();
            this.Button1 = new System.Windows.Forms.Button();
            this.codigo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.nomempresa = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.pnlEmpbloq.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dtgEmpBloq)).BeginInit();
            this.SuspendLayout();
            //
            // lblempbloqueda
            //
            this.lblempbloqueda.AutoSize = true;
            this.lblempbloqueda.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblempbloqueda.Location = new System.Drawing.Point(139, 9);
            this.lblempbloqueda.Name = "lblempbloqueda";
            this.lblempbloqueda.Size = new System.Drawing.Size(139, 16);
            this.lblempbloqueda.TabIndex = 0;
            this.lblempbloqueda.Text = "Empresas Bloquedas";
            //
            // pnlEmpbloq
            //
            this.pnlEmpbloq.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pnlEmpbloq.Controls.Add(this.dtgEmpBloq);
            this.pnlEmpbloq.Location = new System.Drawing.Point(12, 48);
            this.pnlEmpbloq.Name = "pnlEmpbloq";
            this.pnlEmpbloq.Size = new System.Drawing.Size(379, 112);
            this.pnlEmpbloq.TabIndex = 1;
            //
            // dtgEmpBloq
            //
            this.dtgEmpBloq.AllowUserToAddRows = false;
            this.dtgEmpBloq.AllowUserToDeleteRows = false;
            this.dtgEmpBloq.AllowUserToOrderColumns = true;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dtgEmpBloq.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dtgEmpBloq.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dtgEmpBloq.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.codigo,
            this.nomempresa});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dtgEmpBloq.DefaultCellStyle = dataGridViewCellStyle2;
            this.dtgEmpBloq.EnableHeadersVisualStyles = false;
            this.dtgEmpBloq.Location = new System.Drawing.Point(8, 11);
            this.dtgEmpBloq.MultiSelect = false;
            this.dtgEmpBloq.Name = "dtgEmpBloq";
            this.dtgEmpBloq.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dtgEmpBloq.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dtgEmpBloq.RowHeadersVisible = false;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            this.dtgEmpBloq.RowsDefaultCellStyle = dataGridViewCellStyle4;
            this.dtgEmpBloq.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dtgEmpBloq.Size = new System.Drawing.Size(358, 89);
            this.dtgEmpBloq.TabIndex = 3;
            //
            // Button1
            //
            this.Button1.Location = new System.Drawing.Point(12, 9);
            this.Button1.Name = "Button1";
            this.Button1.Size = new System.Drawing.Size(36, 33);
            this.Button1.TabIndex = 2;
            this.Button1.Text = "X";
            this.Button1.UseVisualStyleBackColor = true;
            this.Button1.Click += new System.EventHandler(this.Button1_Click);
            //
            // codigo
            //
            this.codigo.DataPropertyName = "codigo_empresa";
            this.codigo.HeaderText = "Codigo";
            this.codigo.Name = "codigo";
            this.codigo.ReadOnly = true;
            this.codigo.Width = 50;
            //
            // nomempresa
            //
            this.nomempresa.DataPropertyName = "nombre";
            this.nomempresa.HeaderText = "Empresa";
            this.nomempresa.Name = "nomempresa";
            this.nomempresa.ReadOnly = true;
            this.nomempresa.Width = 300;
            //
            // frmempbloq
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(409, 173);
            this.Controls.Add(this.Button1);
            this.Controls.Add(this.pnlEmpbloq);
            this.Controls.Add(this.lblempbloqueda);
            this.Name = "frmempbloq";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Empresas bloquedas";
            this.Load += new System.EventHandler(this.frmempbloq_Load);
            this.pnlEmpbloq.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dtgEmpBloq)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.Label lblempbloqueda;
        internal System.Windows.Forms.Panel pnlEmpbloq;
        internal System.Windows.Forms.DataGridView dtgEmpBloq;
        internal System.Windows.Forms.Button Button1;
        internal System.Windows.Forms.DataGridViewTextBoxColumn codigo;
        internal System.Windows.Forms.DataGridViewTextBoxColumn nomempresa;
    }
}
