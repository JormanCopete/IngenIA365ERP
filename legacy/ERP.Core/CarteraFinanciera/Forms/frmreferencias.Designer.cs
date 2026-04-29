namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmreferencias
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
            this.DgwReferencias = new System.Windows.Forms.DataGridView();
            this.ClmMarca = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmReferencia = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmNombre = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDireccion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmTelefono = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmcodCiudad = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.LblNomEmpresa = new System.Windows.Forms.Label();
            this.BtnAgregar = new System.Windows.Forms.Button();
            this.Menu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.IncluirReferenciaToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.QuitarRefereToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.DgwReferencias)).BeginInit();
            this.Menu.SuspendLayout();
            this.SuspendLayout();
            //
            // DgwReferencias
            //
            this.DgwReferencias.AllowUserToAddRows = false;
            this.DgwReferencias.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwReferencias.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ClmMarca,
            this.ClmReferencia,
            this.ClmNombre,
            this.ClmDireccion,
            this.ClmTelefono,
            this.ClmcodCiudad});
            this.DgwReferencias.Location = new System.Drawing.Point(12, 80);
            this.DgwReferencias.Name = "DgwReferencias";
            this.DgwReferencias.RowHeadersVisible = false;
            this.DgwReferencias.Size = new System.Drawing.Size(660, 200);
            this.DgwReferencias.TabIndex = 0;
            this.DgwReferencias.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DgwReferencias_MouseUp);
            //
            // ClmMarca
            //
            this.ClmMarca.HeaderText = "";
            this.ClmMarca.Name = "ClmMarca";
            this.ClmMarca.Width = 30;
            //
            // ClmReferencia
            //
            this.ClmReferencia.HeaderText = "Referencia";
            this.ClmReferencia.Name = "ClmReferencia";
            //
            // ClmNombre
            //
            this.ClmNombre.HeaderText = "Nombre";
            this.ClmNombre.Name = "ClmNombre";
            this.ClmNombre.Width = 200;
            //
            // ClmDireccion
            //
            this.ClmDireccion.HeaderText = "Direccion";
            this.ClmDireccion.Name = "ClmDireccion";
            this.ClmDireccion.Width = 150;
            //
            // ClmTelefono
            //
            this.ClmTelefono.HeaderText = "Telefono";
            this.ClmTelefono.Name = "ClmTelefono";
            //
            // ClmcodCiudad
            //
            this.ClmcodCiudad.HeaderText = "Ciudad";
            this.ClmcodCiudad.Name = "ClmcodCiudad";
            this.ClmcodCiudad.Width = 60;
            //
            // opcion
            //
            this.opcion.Location = new System.Drawing.Point(12, 12);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(25, 27);
            this.opcion.TabIndex = 1;
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            //
            // LblNomEmpresa
            //
            this.LblNomEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold);
            this.LblNomEmpresa.Location = new System.Drawing.Point(50, 12);
            this.LblNomEmpresa.Name = "LblNomEmpresa";
            this.LblNomEmpresa.Size = new System.Drawing.Size(400, 23);
            this.LblNomEmpresa.TabIndex = 2;
            //
            // BtnAgregar
            //
            this.BtnAgregar.Location = new System.Drawing.Point(597, 45);
            this.BtnAgregar.Name = "BtnAgregar";
            this.BtnAgregar.Size = new System.Drawing.Size(75, 29);
            this.BtnAgregar.TabIndex = 3;
            this.BtnAgregar.Text = "Agregar";
            this.BtnAgregar.UseVisualStyleBackColor = true;
            this.BtnAgregar.Click += new System.EventHandler(this.BtnAgregar_Click);
            //
            // Menu
            //
            this.Menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.IncluirReferenciaToolStripMenuItem,
            this.QuitarRefereToolStripMenuItem});
            this.Menu.Name = "Menu";
            this.Menu.Size = new System.Drawing.Size(170, 48);
            //
            // IncluirReferenciaToolStripMenuItem
            //
            this.IncluirReferenciaToolStripMenuItem.Name = "IncluirReferenciaToolStripMenuItem";
            this.IncluirReferenciaToolStripMenuItem.Size = new System.Drawing.Size(169, 22);
            this.IncluirReferenciaToolStripMenuItem.Text = "Incluir Referencia";
            this.IncluirReferenciaToolStripMenuItem.Click += new System.EventHandler(this.IncluirReferenciaToolStripMenuItem_Click);
            //
            // QuitarRefereToolStripMenuItem
            //
            this.QuitarRefereToolStripMenuItem.Name = "QuitarRefereToolStripMenuItem";
            this.QuitarRefereToolStripMenuItem.Size = new System.Drawing.Size(169, 22);
            this.QuitarRefereToolStripMenuItem.Text = "Quitar Referencia";
            this.QuitarRefereToolStripMenuItem.Click += new System.EventHandler(this.QuitarRefereToolStripMenuItem_Click);
            //
            // frmreferencias
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 292);
            this.Controls.Add(this.BtnAgregar);
            this.Controls.Add(this.LblNomEmpresa);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.DgwReferencias);
            this.Name = "frmreferencias";
            this.Text = "Referencias";
            this.Load += new System.EventHandler(this.frmreferencias_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DgwReferencias)).EndInit();
            this.Menu.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        internal System.Windows.Forms.DataGridView DgwReferencias;
        internal ERP.Core.Compartido.Controles.SasToolBar opcion;
        internal System.Windows.Forms.Label LblNomEmpresa;
        internal System.Windows.Forms.Button BtnAgregar;
        internal System.Windows.Forms.ContextMenuStrip Menu;
        internal System.Windows.Forms.ToolStripMenuItem IncluirReferenciaToolStripMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem QuitarRefereToolStripMenuItem;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmMarca;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmReferencia;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmNombre;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmDireccion;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmTelefono;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmcodCiudad;
    }
}
