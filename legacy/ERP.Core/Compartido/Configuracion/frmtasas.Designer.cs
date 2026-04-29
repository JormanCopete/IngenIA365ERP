namespace ERP.Core.Compartido.Configuracion
{
    partial class frmtasas
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.BtnGrabar = new System.Windows.Forms.Button();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.DgvTasas = new System.Windows.Forms.DataGridView();
            this.ClmVlrIni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmVlrFinal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmPlazoIni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmPlazoFin = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmTasa = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Label1 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.CtmstripBorrarTabla = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.BorrarRegistroToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.DgvTasas)).BeginInit();
            this.CtmstripBorrarTabla.SuspendLayout();
            this.SuspendLayout();
            //
            // BtnGrabar
            //
            this.BtnGrabar.Location = new System.Drawing.Point(470, 256);
            this.BtnGrabar.Name = "BtnGrabar";
            this.BtnGrabar.Size = new System.Drawing.Size(38, 35);
            this.BtnGrabar.TabIndex = 9;
            this.BtnGrabar.Text = "G";
            this.BtnGrabar.UseVisualStyleBackColor = true;
            this.BtnGrabar.Click += new System.EventHandler(this.BtnGrabar_Click);
            //
            // BtnSalir
            //
            this.BtnSalir.Location = new System.Drawing.Point(514, 256);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 8;
            this.BtnSalir.Text = "X";
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
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
            this.ClmTasa});
            this.DgvTasas.ContextMenuStrip = this.CtmstripBorrarTabla;
            this.DgvTasas.EnableHeadersVisualStyles = false;
            this.DgvTasas.Location = new System.Drawing.Point(14, 49);
            this.DgvTasas.Name = "DgvTasas";
            this.DgvTasas.RowHeadersVisible = false;
            this.DgvTasas.Size = new System.Drawing.Size(541, 201);
            this.DgvTasas.TabIndex = 7;
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
            // ClmTasa
            //
            this.ClmTasa.DataPropertyName = "tasa";
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle6.Format = "N3";
            this.ClmTasa.DefaultCellStyle = dataGridViewCellStyle6;
            this.ClmTasa.HeaderText = "Tasa";
            this.ClmTasa.Name = "ClmTasa";
            this.ClmTasa.Width = 70;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(98, 5);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(373, 20);
            this.Label1.TabIndex = 5;
            this.Label1.Text = "TASAS DE CDATS POR PLAZOS Y MONTOS";
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(-16, 21);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(600, 12);
            this.GroupBox1.TabIndex = 6;
            this.GroupBox1.TabStop = false;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(23, 267);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(217, 13);
            this.Label2.TabIndex = 10;
            this.Label2.Text = "* Los plazos de los cdat estan dados en dias";
            //
            // CtmstripBorrarTabla
            //
            this.CtmstripBorrarTabla.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.BorrarRegistroToolStripMenuItem});
            this.CtmstripBorrarTabla.Name = "CtmstripBorrarTabla";
            this.CtmstripBorrarTabla.Size = new System.Drawing.Size(159, 26);
            //
            // BorrarRegistroToolStripMenuItem
            //
            this.BorrarRegistroToolStripMenuItem.Name = "BorrarRegistroToolStripMenuItem";
            this.BorrarRegistroToolStripMenuItem.Size = new System.Drawing.Size(158, 22);
            this.BorrarRegistroToolStripMenuItem.Text = "Borrar Registro";
            this.BorrarRegistroToolStripMenuItem.Click += new System.EventHandler(this.BorrarRegistroToolStripMenuItem_Click);
            //
            // frmtasas
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(568, 298);
            this.ControlBox = false;
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.BtnGrabar);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.DgvTasas);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.GroupBox1);
            this.Name = "frmtasas";
            this.Text = "Tasas de CDATS";
            ((System.ComponentModel.ISupportInitialize)(this.DgvTasas)).EndInit();
            this.CtmstripBorrarTabla.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.Button BtnGrabar;
        internal System.Windows.Forms.Button BtnSalir;
        internal System.Windows.Forms.DataGridView DgvTasas;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmVlrIni;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmVlrFinal;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmPlazoIni;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmPlazoFin;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmTasa;
        internal System.Windows.Forms.ContextMenuStrip CtmstripBorrarTabla;
        internal System.Windows.Forms.ToolStripMenuItem BorrarRegistroToolStripMenuItem;
    }
}
