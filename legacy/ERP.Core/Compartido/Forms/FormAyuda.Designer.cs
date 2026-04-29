namespace ERP.Core.Compartido.Forms
{
    partial class FormAyuda
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
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormAyuda));
            this.campo2 = new System.Windows.Forms.Label();
            this.campo1 = new System.Windows.Forms.Label();
            this.campo3 = new System.Windows.Forms.Label();
            this.valor1 = new System.Windows.Forms.TextBox();
            this.valor2 = new System.Windows.Forms.TextBox();
            this.valor3 = new System.Windows.Forms.TextBox();
            this.ComboBox2 = new System.Windows.Forms.ComboBox();
            this.ComboBox1 = new System.Windows.Forms.ComboBox();
            this.ComboBox3 = new System.Windows.Forms.ComboBox();
            this.Busca = new System.Windows.Forms.Button();
            this.Label4 = new System.Windows.Forms.Label();
            this.Ok = new System.Windows.Forms.Button();
            this.cancel = new System.Windows.Forms.Button();
            this.limit = new System.Windows.Forms.ComboBox();
            this.stNombretabla = new System.Windows.Forms.TextBox();
            this.stCampouno = new System.Windows.Forms.TextBox();
            this.stCampodos = new System.Windows.Forms.TextBox();
            this.stCampotres = new System.Windows.Forms.TextBox();
            this.lis3 = new System.Windows.Forms.DataGridView();
            this.txtValor4 = new System.Windows.Forms.TextBox();
            this.ComboBox4 = new System.Windows.Forms.ComboBox();
            this.stCampocuatro = new System.Windows.Forms.TextBox();
            this.campo4 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.lis3)).BeginInit();
            this.SuspendLayout();
            //
            // campo2
            //
            this.campo2.AutoSize = true;
            this.campo2.Location = new System.Drawing.Point(136, 16);
            this.campo2.Name = "campo2";
            this.campo2.Size = new System.Drawing.Size(0, 13);
            this.campo2.TabIndex = 0;
            //
            // campo1
            //
            this.campo1.AutoSize = true;
            this.campo1.Location = new System.Drawing.Point(13, 16);
            this.campo1.Name = "campo1";
            this.campo1.Size = new System.Drawing.Size(0, 13);
            this.campo1.TabIndex = 1;
            //
            // campo3
            //
            this.campo3.AutoSize = true;
            this.campo3.Location = new System.Drawing.Point(276, 15);
            this.campo3.Name = "campo3";
            this.campo3.Size = new System.Drawing.Size(0, 13);
            this.campo3.TabIndex = 2;
            //
            // valor1
            //
            this.valor1.Location = new System.Drawing.Point(13, 56);
            this.valor1.Name = "valor1";
            this.valor1.Size = new System.Drawing.Size(115, 20);
            this.valor1.TabIndex = 4;
            this.valor1.TextChanged += new System.EventHandler(this.valor1_TextChanged);
            //
            // valor2
            //
            this.valor2.Location = new System.Drawing.Point(136, 56);
            this.valor2.Name = "valor2";
            this.valor2.Size = new System.Drawing.Size(123, 20);
            this.valor2.TabIndex = 5;
            this.valor2.TextChanged += new System.EventHandler(this.valor2_TextChanged);
            //
            // valor3
            //
            this.valor3.Location = new System.Drawing.Point(272, 56);
            this.valor3.Name = "valor3";
            this.valor3.Size = new System.Drawing.Size(111, 20);
            this.valor3.TabIndex = 6;
            this.valor3.TextChanged += new System.EventHandler(this.valor3_TextChanged);
            //
            // ComboBox2
            //
            this.ComboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboBox2.Items.AddRange(new object[] { "Inicia con", "Contiene", "Igual a" });
            this.ComboBox2.Location = new System.Drawing.Point(13, 32);
            this.ComboBox2.Name = "ComboBox2";
            this.ComboBox2.Size = new System.Drawing.Size(75, 21);
            this.ComboBox2.TabIndex = 1;
            //
            // ComboBox1
            //
            this.ComboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboBox1.Items.AddRange(new object[] { "Inicia con", "Contiene", "Igual a" });
            this.ComboBox1.Location = new System.Drawing.Point(136, 32);
            this.ComboBox1.Name = "ComboBox1";
            this.ComboBox1.Size = new System.Drawing.Size(80, 21);
            this.ComboBox1.TabIndex = 2;
            //
            // ComboBox3
            //
            this.ComboBox3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboBox3.Items.AddRange(new object[] { "Inicia con", "Contiene", "Igual a" });
            this.ComboBox3.Location = new System.Drawing.Point(272, 32);
            this.ComboBox3.Name = "ComboBox3";
            this.ComboBox3.Size = new System.Drawing.Size(80, 21);
            this.ComboBox3.TabIndex = 3;
            //
            // Busca
            //
            this.Busca.BackColor = System.Drawing.Color.LawnGreen;
            this.Busca.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.Busca.Location = new System.Drawing.Point(532, 14);
            this.Busca.Name = "Busca";
            this.Busca.Size = new System.Drawing.Size(80, 32);
            this.Busca.TabIndex = 8;
            this.Busca.Text = "Buscar";
            this.Busca.UseVisualStyleBackColor = false;
            this.Busca.Click += new System.EventHandler(this.Button1_Click);
            //
            // Label4
            //
            this.Label4.Location = new System.Drawing.Point(524, 54);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(104, 16);
            this.Label4.TabIndex = 83;
            this.Label4.Text = "Limite de Registros";
            //
            // Ok
            //
            this.Ok.Enabled = false;
            this.Ok.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.Ok.Location = new System.Drawing.Point(389, 372);
            this.Ok.Name = "Ok";
            this.Ok.Size = new System.Drawing.Size(96, 31);
            this.Ok.TabIndex = 9;
            this.Ok.Text = "Aceptar";
            this.Ok.Click += new System.EventHandler(this.Ok_Click);
            //
            // cancel
            //
            this.cancel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cancel.Location = new System.Drawing.Point(501, 372);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(96, 31);
            this.cancel.TabIndex = 10;
            this.cancel.Text = "Cancelar";
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            //
            // limit
            //
            this.limit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.limit.Items.AddRange(new object[] { "500", "400", "300", "200", "150", "100", "50", "40", "30", "20", "10", "999999999" });
            this.limit.Location = new System.Drawing.Point(540, 71);
            this.limit.Name = "limit";
            this.limit.Size = new System.Drawing.Size(64, 21);
            this.limit.TabIndex = 7;
            this.limit.SelectedIndexChanged += new System.EventHandler(this.limit_SelectedIndexChanged);
            //
            // stNombretabla
            //
            this.stNombretabla.Location = new System.Drawing.Point(88, 368);
            this.stNombretabla.Name = "stNombretabla";
            this.stNombretabla.Size = new System.Drawing.Size(120, 20);
            this.stNombretabla.TabIndex = 93;
            this.stNombretabla.Visible = false;
            //
            // stCampouno
            //
            this.stCampouno.Location = new System.Drawing.Point(88, 392);
            this.stCampouno.Name = "stCampouno";
            this.stCampouno.Size = new System.Drawing.Size(120, 20);
            this.stCampouno.TabIndex = 94;
            this.stCampouno.Visible = false;
            //
            // stCampodos
            //
            this.stCampodos.Location = new System.Drawing.Point(232, 368);
            this.stCampodos.Name = "stCampodos";
            this.stCampodos.Size = new System.Drawing.Size(120, 20);
            this.stCampodos.TabIndex = 95;
            this.stCampodos.Visible = false;
            //
            // stCampotres
            //
            this.stCampotres.Location = new System.Drawing.Point(232, 392);
            this.stCampotres.Name = "stCampotres";
            this.stCampotres.Size = new System.Drawing.Size(120, 20);
            this.stCampotres.TabIndex = 96;
            this.stCampotres.Visible = false;
            //
            // lis3
            //
            this.lis3.AllowUserToAddRows = false;
            this.lis3.AllowUserToDeleteRows = false;
            this.lis3.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.lis3.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.lis3.BackgroundColor = System.Drawing.SystemColors.Window;
            this.lis3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.lis3.Location = new System.Drawing.Point(18, 100);
            this.lis3.MultiSelect = false;
            this.lis3.Name = "lis3";
            this.lis3.ReadOnly = true;
            this.lis3.RowHeadersVisible = false;
            this.lis3.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.lis3.Size = new System.Drawing.Size(584, 262);
            this.lis3.TabIndex = 97;
            this.lis3.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.lis3_CellContentClick);
            this.lis3.SelectionChanged += new System.EventHandler(this.lis3_SelectionChanged);
            this.lis3.DoubleClick += new System.EventHandler(this.lis3_DoubleClick);
            //
            // txtValor4
            //
            this.txtValor4.Location = new System.Drawing.Point(398, 56);
            this.txtValor4.Name = "txtValor4";
            this.txtValor4.Size = new System.Drawing.Size(115, 20);
            this.txtValor4.TabIndex = 99;
            this.txtValor4.TextChanged += new System.EventHandler(this.txtValor4_TextChanged);
            //
            // ComboBox4
            //
            this.ComboBox4.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboBox4.Items.AddRange(new object[] { "Inicia con", "Contiene", "Igual a" });
            this.ComboBox4.Location = new System.Drawing.Point(398, 32);
            this.ComboBox4.Name = "ComboBox4";
            this.ComboBox4.Size = new System.Drawing.Size(75, 21);
            this.ComboBox4.TabIndex = 98;
            //
            // stCampocuatro
            //
            this.stCampocuatro.Location = new System.Drawing.Point(8, 368);
            this.stCampocuatro.Name = "stCampocuatro";
            this.stCampocuatro.Size = new System.Drawing.Size(74, 20);
            this.stCampocuatro.TabIndex = 100;
            this.stCampocuatro.Visible = false;
            //
            // campo4
            //
            this.campo4.AutoSize = true;
            this.campo4.Location = new System.Drawing.Point(401, 14);
            this.campo4.Name = "campo4";
            this.campo4.Size = new System.Drawing.Size(0, 13);
            this.campo4.TabIndex = 101;
            //
            // FormAyuda
            //
            this.AcceptButton = this.Busca;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(626, 415);
            this.Controls.Add(this.campo4);
            this.Controls.Add(this.stCampocuatro);
            this.Controls.Add(this.txtValor4);
            this.Controls.Add(this.ComboBox4);
            this.Controls.Add(this.lis3);
            this.Controls.Add(this.stCampotres);
            this.Controls.Add(this.stCampodos);
            this.Controls.Add(this.stCampouno);
            this.Controls.Add(this.stNombretabla);
            this.Controls.Add(this.valor3);
            this.Controls.Add(this.valor2);
            this.Controls.Add(this.valor1);
            this.Controls.Add(this.limit);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.Ok);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.Busca);
            this.Controls.Add(this.ComboBox3);
            this.Controls.Add(this.ComboBox1);
            this.Controls.Add(this.ComboBox2);
            this.Controls.Add(this.campo3);
            this.Controls.Add(this.campo1);
            this.Controls.Add(this.campo2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "FormAyuda";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Ayuda";
            this.Load += new System.EventHandler(this.Form2_Load);
            ((System.ComponentModel.ISupportInitialize)(this.lis3)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ComboBox ComboBox2;
        private System.Windows.Forms.ComboBox ComboBox1;
        private System.Windows.Forms.ComboBox ComboBox3;
        private System.Windows.Forms.Label Label4;
        public System.Windows.Forms.Label campo2;
        public System.Windows.Forms.Label campo1;
        public System.Windows.Forms.Label campo3;
        public System.Windows.Forms.Button Ok;
        public System.Windows.Forms.Button cancel;
        public System.Windows.Forms.TextBox valor1;
        public System.Windows.Forms.TextBox valor2;
        public System.Windows.Forms.TextBox valor3;
        public System.Windows.Forms.ComboBox limit;
        internal System.Windows.Forms.TextBox stNombretabla;
        internal System.Windows.Forms.TextBox stCampouno;
        internal System.Windows.Forms.TextBox stCampodos;
        internal System.Windows.Forms.TextBox stCampotres;
        public System.Windows.Forms.DataGridView lis3;
        public System.Windows.Forms.Button Busca;
        public System.Windows.Forms.TextBox txtValor4;
        public System.Windows.Forms.ComboBox ComboBox4;
        internal System.Windows.Forms.TextBox stCampocuatro;
        public System.Windows.Forms.Label campo4;
    }
}
