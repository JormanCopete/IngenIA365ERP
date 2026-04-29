namespace ERP.Core.Compartido.Forms
{
    partial class helptable
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.DgvResultado = new System.Windows.Forms.DataGridView();
            this.TxtCodigo = new System.Windows.Forms.TextBox();
            this.TxtNombre = new System.Windows.Forms.TextBox();
            this.LblCodigo = new System.Windows.Forms.Label();
            this.LblNombre = new System.Windows.Forms.Label();
            this.Button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.DgvResultado)).BeginInit();
            this.SuspendLayout();
            //
            // DgvResultado
            //
            this.DgvResultado.AllowUserToAddRows = false;
            this.DgvResultado.AllowUserToDeleteRows = false;
            this.DgvResultado.AllowUserToResizeColumns = false;
            this.DgvResultado.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.DgvResultado.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.DgvResultado.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvResultado.Location = new System.Drawing.Point(7, 67);
            this.DgvResultado.MultiSelect = false;
            this.DgvResultado.Name = "DgvResultado";
            this.DgvResultado.ReadOnly = true;
            this.DgvResultado.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgvResultado.Size = new System.Drawing.Size(827, 369);
            this.DgvResultado.TabIndex = 0;
            this.DgvResultado.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvResultado_CellContentClick);
            this.DgvResultado.Click += new System.EventHandler(this.DgvProductos_Click);
            this.DgvResultado.DoubleClick += new System.EventHandler(this.DgvDatos_DoubleClick);
            this.DgvResultado.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.DgvDatos_MouseDoubleClick);
            this.DgvResultado.SelectionChanged += new System.EventHandler(this.DgvDatos_SelectionChanged);
            //
            // TxtCodigo
            //
            this.TxtCodigo.Location = new System.Drawing.Point(12, 41);
            this.TxtCodigo.Name = "TxtCodigo";
            this.TxtCodigo.Size = new System.Drawing.Size(286, 20);
            this.TxtCodigo.TabIndex = 1;
            this.TxtCodigo.TextChanged += new System.EventHandler(this.TxtFiltroReferencia_TextChanged);
            //
            // TxtNombre
            //
            this.TxtNombre.Location = new System.Drawing.Point(304, 41);
            this.TxtNombre.Name = "TxtNombre";
            this.TxtNombre.Size = new System.Drawing.Size(530, 20);
            this.TxtNombre.TabIndex = 2;
            this.TxtNombre.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.TxtFiltroProducto_KeyPress);
            this.TxtNombre.TextChanged += new System.EventHandler(this.TxtNombre_TextChanged);
            //
            // LblCodigo
            //
            this.LblCodigo.AutoSize = true;
            this.LblCodigo.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblCodigo.Location = new System.Drawing.Point(12, 25);
            this.LblCodigo.Name = "LblCodigo";
            this.LblCodigo.Size = new System.Drawing.Size(46, 13);
            this.LblCodigo.TabIndex = 3;
            this.LblCodigo.Text = "Codigo";
            //
            // LblNombre
            //
            this.LblNombre.AutoSize = true;
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblNombre.Location = new System.Drawing.Point(301, 25);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(132, 13);
            this.LblNombre.TabIndex = 4;
            this.LblNombre.Text = "Nombre o Descripcion";
            //
            // Button1
            //
            this.Button1.Location = new System.Drawing.Point(706, 4);
            this.Button1.Name = "Button1";
            this.Button1.Size = new System.Drawing.Size(75, 23);
            this.Button1.TabIndex = 5;
            this.Button1.Text = "Button1";
            this.Button1.UseVisualStyleBackColor = true;
            this.Button1.Visible = false;
            this.Button1.Click += new System.EventHandler(this.Button1_Click);
            //
            // helptable
            //
            this.ClientSize = new System.Drawing.Size(842, 449);
            this.Controls.Add(this.Button1);
            this.Controls.Add(this.LblNombre);
            this.Controls.Add(this.LblCodigo);
            this.Controls.Add(this.TxtNombre);
            this.Controls.Add(this.TxtCodigo);
            this.Controls.Add(this.DgvResultado);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.helptable_KeyDown);
            this.Load += new System.EventHandler(this.helptable_Load);
            this.Name = "helptable";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            ((System.ComponentModel.ISupportInitialize)(this.DgvResultado)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView DgvResultado;
        private System.Windows.Forms.TextBox TxtCodigo;
        private System.Windows.Forms.TextBox TxtNombre;
        private System.Windows.Forms.Label LblCodigo;
        private System.Windows.Forms.Label LblNombre;
        private System.Windows.Forms.Button Button1;
    }
}
