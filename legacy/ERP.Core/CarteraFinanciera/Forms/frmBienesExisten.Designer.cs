namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmBienesExisten
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && (components != null))
                    components.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();

            this.DgwBienesRaicesExisten = new System.Windows.Forms.DataGridView();
            this.Clase = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Direccion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Ciudad = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Valor = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.codciudad = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Marcado = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Menu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.MenAgregabien = new System.Windows.Forms.ToolStripMenuItem();
            this.MenQuitarbien = new System.Windows.Forms.ToolStripMenuItem();
            this.LblNomCompania = new System.Windows.Forms.Label();
            this.BtnSalir = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.DgwBienesRaicesExisten)).BeginInit();
            this.Menu.SuspendLayout();
            this.SuspendLayout();
            //
            // DgwBienesRaicesExisten
            //
            this.DgwBienesRaicesExisten.AllowUserToAddRows = false;
            this.DgwBienesRaicesExisten.AllowUserToDeleteRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwBienesRaicesExisten.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgwBienesRaicesExisten.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwBienesRaicesExisten.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.Clase, this.Direccion, this.Ciudad, this.Valor, this.codciudad, this.Marcado });
            this.DgwBienesRaicesExisten.ContextMenuStrip = this.Menu;
            this.DgwBienesRaicesExisten.EnableHeadersVisualStyles = false;
            this.DgwBienesRaicesExisten.Location = new System.Drawing.Point(5, 66);
            this.DgwBienesRaicesExisten.MultiSelect = false;
            this.DgwBienesRaicesExisten.Name = "DgwBienesRaicesExisten";
            this.DgwBienesRaicesExisten.ReadOnly = true;
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.DgwBienesRaicesExisten.RowHeadersDefaultCellStyle = DataGridViewCellStyle3;
            this.DgwBienesRaicesExisten.RowHeadersVisible = false;
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.GhostWhite;
            DataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            this.DgwBienesRaicesExisten.RowsDefaultCellStyle = DataGridViewCellStyle4;
            this.DgwBienesRaicesExisten.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwBienesRaicesExisten.Size = new System.Drawing.Size(644, 144);
            this.DgwBienesRaicesExisten.TabIndex = 0;
            //
            // Clase
            //
            DataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Info;
            this.Clase.DefaultCellStyle = DataGridViewCellStyle2;
            this.Clase.HeaderText = "Clase";
            this.Clase.Name = "Clase";
            this.Clase.ReadOnly = true;
            this.Clase.Width = 80;
            //
            // Direccion
            //
            this.Direccion.HeaderText = "Direccion";
            this.Direccion.Name = "Direccion";
            this.Direccion.ReadOnly = true;
            this.Direccion.Width = 260;
            //
            // Ciudad
            //
            this.Ciudad.HeaderText = "Ciudad";
            this.Ciudad.Name = "Ciudad";
            this.Ciudad.ReadOnly = true;
            this.Ciudad.Width = 190;
            //
            // Valor
            //
            this.Valor.HeaderText = "Valor Comercial";
            this.Valor.Name = "Valor";
            this.Valor.ReadOnly = true;
            this.Valor.Width = 110;
            //
            // codciudad
            //
            this.codciudad.HeaderText = "Codigo ciudad";
            this.codciudad.Name = "codciudad";
            this.codciudad.ReadOnly = true;
            this.codciudad.Visible = false;
            //
            // Marcado
            //
            this.Marcado.HeaderText = "Incluida";
            this.Marcado.Name = "Marcado";
            this.Marcado.ReadOnly = true;
            this.Marcado.Visible = false;
            //
            // Menu
            //
            this.Menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.MenAgregabien, this.MenQuitarbien });
            this.Menu.Name = "Menu";
            this.Menu.Size = new System.Drawing.Size(143, 48);
            this.Menu.Opening += new System.ComponentModel.CancelEventHandler(this.Menu_Opening);
            //
            // MenAgregabien
            //
            this.MenAgregabien.Name = "MenAgregabien";
            this.MenAgregabien.Size = new System.Drawing.Size(142, 22);
            this.MenAgregabien.Text = "Agregar Bien";
            this.MenAgregabien.Click += new System.EventHandler(this.MenAgregabien_Click);
            //
            // MenQuitarbien
            //
            this.MenQuitarbien.Name = "MenQuitarbien";
            this.MenQuitarbien.Size = new System.Drawing.Size(142, 22);
            this.MenQuitarbien.Text = "Quitar bien";
            this.MenQuitarbien.Click += new System.EventHandler(this.MenQuitarbien_Click);
            //
            // LblNomCompania
            //
            this.LblNomCompania.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomCompania.Location = new System.Drawing.Point(37, 9);
            this.LblNomCompania.Name = "LblNomCompania";
            this.LblNomCompania.Size = new System.Drawing.Size(551, 37);
            this.LblNomCompania.TabIndex = 1;
            this.LblNomCompania.Text = "Label1";
            this.LblNomCompania.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(604, 216);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(45, 36);
            this.BtnSalir.TabIndex = 2;
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // frmBienesExisten
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(661, 264);
            this.Controls.Add(this.LblNomCompania);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.DgwBienesRaicesExisten);
            this.Name = "frmBienesExisten";
            this.Text = "Bienes raices o vehiculos existentes";
            this.Load += new System.EventHandler(this.frmBienesExisten_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DgwBienesRaicesExisten)).EndInit();
            this.Menu.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.DataGridView DgwBienesRaicesExisten;
        private System.Windows.Forms.Label LblNomCompania;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.ContextMenuStrip Menu;
        private System.Windows.Forms.ToolStripMenuItem MenAgregabien;
        private System.Windows.Forms.ToolStripMenuItem MenQuitarbien;
        private System.Windows.Forms.DataGridViewTextBoxColumn Clase;
        private System.Windows.Forms.DataGridViewTextBoxColumn Direccion;
        private System.Windows.Forms.DataGridViewTextBoxColumn Ciudad;
        private System.Windows.Forms.DataGridViewTextBoxColumn Valor;
        private System.Windows.Forms.DataGridViewTextBoxColumn codciudad;
        private System.Windows.Forms.DataGridViewTextBoxColumn Marcado;
    }
}
