namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class cop_fconcuope01
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

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(cop_fconcuope01));
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.empresappl = new System.Windows.Forms.TextBox();
            this.lincred = new System.Windows.Forms.TextBox();
            this.numero = new System.Windows.Forms.TextBox();
            this.codigo = new System.Windows.Forms.TextBox();
            this.nombreApellido = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.nom_obliga = new System.Windows.Forms.Label();
            this.periodo_cartera = new System.Windows.Forms.Label();
            this.Label14 = new System.Windows.Forms.Label();
            this.impre = new System.Windows.Forms.Button();
            this.ayuda_codi = new System.Windows.Forms.Button();
            this.Salir = new System.Windows.Forms.Button();
            this.DatGriCuopen = new System.Windows.Forms.DataGridView();
            this.DgvTotales = new System.Windows.Forms.DataGridView();
            this.Lbl_PerIni = new System.Windows.Forms.Label();
            this.Txt_perIni = new ERP.Core.Compartido.Controles.TexboxSoloNumeros();
            this.Txt_perFin = new ERP.Core.Compartido.Controles.TexboxSoloNumeros();
            this.Label3 = new System.Windows.Forms.Label();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.Button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.DatGriCuopen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DgvTotales)).BeginInit();
            this.SuspendLayout();
            //
            // empresappl
            //
            this.empresappl.BackColor = System.Drawing.SystemColors.Control;
            this.empresappl.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.empresappl.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.empresappl.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.empresappl.Location = new System.Drawing.Point(56, 3);
            this.empresappl.Multiline = true;
            this.empresappl.Name = "empresappl";
            this.empresappl.ReadOnly = true;
            this.empresappl.Size = new System.Drawing.Size(438, 39);
            this.empresappl.TabIndex = 193;
            this.empresappl.TabStop = false;
            this.empresappl.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            //
            // lincred
            //
            this.lincred.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.lincred.Location = new System.Drawing.Point(89, 82);
            this.lincred.MaxLength = 14;
            this.lincred.Name = "lincred";
            this.lincred.Size = new System.Drawing.Size(40, 20);
            this.lincred.TabIndex = 208;
            this.lincred.LostFocus += new System.EventHandler(this.lincred_LostFocus);
            //
            // numero
            //
            this.numero.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.numero.Location = new System.Drawing.Point(130, 82);
            this.numero.MaxLength = 14;
            this.numero.Name = "numero";
            this.numero.Size = new System.Drawing.Size(88, 20);
            this.numero.TabIndex = 207;
            this.numero.TextChanged += new System.EventHandler(this.numero_TextChanged);
            this.numero.LostFocus += new System.EventHandler(this.numero_LostFocus);
            //
            // codigo
            //
            this.codigo.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.codigo.Location = new System.Drawing.Point(89, 55);
            this.codigo.MaxLength = 14;
            this.codigo.Name = "codigo";
            this.codigo.Size = new System.Drawing.Size(121, 20);
            this.codigo.TabIndex = 205;
            this.codigo.TextChanged += new System.EventHandler(this.codigo_TextChanged);
            this.codigo.LostFocus += new System.EventHandler(this.codigo_LostFocus);
            //
            // nombreApellido
            //
            this.nombreApellido.BackColor = System.Drawing.SystemColors.Window;
            this.nombreApellido.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.nombreApellido.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.nombreApellido.Location = new System.Drawing.Point(244, 55);
            this.nombreApellido.Name = "nombreApellido";
            this.nombreApellido.Size = new System.Drawing.Size(405, 20);
            this.nombreApellido.TabIndex = 209;
            this.nombreApellido.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // Label1
            //
            this.Label1.Location = new System.Drawing.Point(13, 57);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(48, 16);
            this.Label1.TabIndex = 204;
            this.Label1.Text = "Codigo";
            //
            // Label5
            //
            this.Label5.Location = new System.Drawing.Point(13, 84);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(80, 16);
            this.Label5.TabIndex = 210;
            this.Label5.Text = "Credito Nro.";
            //
            // nom_obliga
            //
            this.nom_obliga.BackColor = System.Drawing.SystemColors.Window;
            this.nom_obliga.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.nom_obliga.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.nom_obliga.Location = new System.Drawing.Point(226, 82);
            this.nom_obliga.Name = "nom_obliga";
            this.nom_obliga.Size = new System.Drawing.Size(256, 20);
            this.nom_obliga.TabIndex = 212;
            this.nom_obliga.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // periodo_cartera
            //
            this.periodo_cartera.BackColor = System.Drawing.Color.White;
            this.periodo_cartera.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold);
            this.periodo_cartera.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.periodo_cartera.Location = new System.Drawing.Point(574, 4);
            this.periodo_cartera.Name = "periodo_cartera";
            this.periodo_cartera.Size = new System.Drawing.Size(84, 18);
            this.periodo_cartera.TabIndex = 225;
            //
            // Label14
            //
            this.Label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold);
            this.Label14.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label14.Location = new System.Drawing.Point(510, 3);
            this.Label14.Name = "Label14";
            this.Label14.Size = new System.Drawing.Size(75, 23);
            this.Label14.TabIndex = 224;
            this.Label14.Text = "Periodo";
            //
            // impre
            //
            this.impre.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.impre.Location = new System.Drawing.Point(3, 283);
            this.impre.Margin = new System.Windows.Forms.Padding(4);
            this.impre.Name = "impre";
            this.impre.Size = new System.Drawing.Size(85, 28);
            this.impre.TabIndex = 226;
            this.impre.Text = "&Imprimir";
            this.ToolTip1.SetToolTip(this.impre, "Imprimir");
            this.impre.Click += new System.EventHandler(this.impre_Click);
            //
            // ayuda_codi
            //
            this.ayuda_codi.BackColor = System.Drawing.SystemColors.Menu;
            this.ayuda_codi.Image = ((System.Drawing.Image)(resources.GetObject("ayuda_codi.Image")));
            this.ayuda_codi.Location = new System.Drawing.Point(215, 53);
            this.ayuda_codi.Name = "ayuda_codi";
            this.ayuda_codi.Size = new System.Drawing.Size(24, 24);
            this.ayuda_codi.TabIndex = 206;
            this.ayuda_codi.UseVisualStyleBackColor = false;
            this.ayuda_codi.Click += new System.EventHandler(this.ayuda_codi_Click);
            //
            // Salir
            //
            this.Salir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.Salir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.Salir.Location = new System.Drawing.Point(8, 3);
            this.Salir.Name = "Salir";
            this.Salir.Size = new System.Drawing.Size(31, 30);
            this.Salir.TabIndex = 202;
            this.ToolTip1.SetToolTip(this.Salir, "Salir");
            this.Salir.Click += new System.EventHandler(this.Salir_Click);
            //
            // DatGriCuopen
            //
            this.DatGriCuopen.AllowUserToAddRows = false;
            this.DatGriCuopen.AllowUserToDeleteRows = false;
            this.DatGriCuopen.AllowUserToResizeRows = false;
            this.DatGriCuopen.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DatGriCuopen.Location = new System.Drawing.Point(5, 135);
            this.DatGriCuopen.MultiSelect = false;
            this.DatGriCuopen.Name = "DatGriCuopen";
            this.DatGriCuopen.ReadOnly = true;
            this.DatGriCuopen.RowHeadersVisible = false;
            this.DatGriCuopen.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DatGriCuopen.Size = new System.Drawing.Size(656, 123);
            this.DatGriCuopen.TabIndex = 227;
            this.DatGriCuopen.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DatGriCuopen_CellContentClick);
            this.DatGriCuopen.ColumnWidthChanged += new System.Windows.Forms.DataGridViewColumnEventHandler(this.DatGriCuopen_ColumnWidthChanged);
            this.DatGriCuopen.Scroll += new System.Windows.Forms.ScrollEventHandler(this.DatGriCuopen_Scroll);
            //
            // DgvTotales
            //
            this.DgvTotales.AllowUserToAddRows = false;
            this.DgvTotales.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvTotales.ColumnHeadersVisible = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.Yellow;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.Yellow;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.DgvTotales.DefaultCellStyle = DataGridViewCellStyle1;
            this.DgvTotales.Location = new System.Drawing.Point(5, 258);
            this.DgvTotales.MultiSelect = false;
            this.DgvTotales.Name = "DgvTotales";
            this.DgvTotales.ReadOnly = true;
            this.DgvTotales.RowHeadersVisible = false;
            this.DgvTotales.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.DgvTotales.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgvTotales.Size = new System.Drawing.Size(656, 23);
            this.DgvTotales.TabIndex = 228;
            this.DgvTotales.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvTotales_CellContentClick);
            //
            // Lbl_PerIni
            //
            this.Lbl_PerIni.AutoSize = true;
            this.Lbl_PerIni.Location = new System.Drawing.Point(13, 112);
            this.Lbl_PerIni.Name = "Lbl_PerIni";
            this.Lbl_PerIni.Size = new System.Drawing.Size(72, 13);
            this.Lbl_PerIni.TabIndex = 229;
            this.Lbl_PerIni.Text = "Periodo inicial";
            //
            // Txt_perIni
            //
            this.Txt_perIni.Location = new System.Drawing.Point(89, 108);
            this.Txt_perIni.MaxLength = 6;
            this.Txt_perIni.Name = "Txt_perIni";
            this.Txt_perIni.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.Txt_perIni.Size = new System.Drawing.Size(46, 20);
            this.Txt_perIni.SoloNumeros = true;
            this.Txt_perIni.TabIndex = 230;
            this.Txt_perIni.Text = "0";
            this.ToolTip1.SetToolTip(this.Txt_perIni, "Periodo de causa inicial");
            //
            // Txt_perFin
            //
            this.Txt_perFin.Location = new System.Drawing.Point(172, 108);
            this.Txt_perFin.MaxLength = 6;
            this.Txt_perFin.Name = "Txt_perFin";
            this.Txt_perFin.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.Txt_perFin.Size = new System.Drawing.Size(46, 20);
            this.Txt_perFin.SoloNumeros = true;
            this.Txt_perFin.TabIndex = 232;
            this.Txt_perFin.Text = "999999";
            this.ToolTip1.SetToolTip(this.Txt_perFin, "Periodo de causa final");
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(139, 112);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(29, 13);
            this.Label3.TabIndex = 231;
            this.Label3.Text = "Final";
            //
            // Button1
            //
            //this.Button1.BackgroundImage = global::ERP.Core.Properties.Resources.replace2;
            this.Button1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.Button1.Location = new System.Drawing.Point(607, 98);
            this.Button1.Name = "Button1";
            this.Button1.Size = new System.Drawing.Size(47, 31);
            this.Button1.TabIndex = 233;
            this.Button1.UseVisualStyleBackColor = true;
            this.Button1.Click += new System.EventHandler(this.Button1_Click);
            //
            // cop_fconcuope01
            //
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(666, 315);
            this.Controls.Add(this.Button1);
            this.Controls.Add(this.Txt_perFin);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Txt_perIni);
            this.Controls.Add(this.Lbl_PerIni);
            this.Controls.Add(this.DgvTotales);
            this.Controls.Add(this.DatGriCuopen);
            this.Controls.Add(this.impre);
            this.Controls.Add(this.periodo_cartera);
            this.Controls.Add(this.Label14);
            this.Controls.Add(this.nom_obliga);
            this.Controls.Add(this.lincred);
            this.Controls.Add(this.numero);
            this.Controls.Add(this.codigo);
            this.Controls.Add(this.nombreApellido);
            this.Controls.Add(this.ayuda_codi);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.Salir);
            this.Controls.Add(this.empresappl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "cop_fconcuope01";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Consulta de cuotas pendientes";
            this.Load += new System.EventHandler(this.cop_fconcext01_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DatGriCuopen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DgvTotales)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button Salir;
        private System.Windows.Forms.TextBox empresappl;
        private System.Windows.Forms.TextBox lincred;
        private System.Windows.Forms.TextBox numero;
        private System.Windows.Forms.TextBox codigo;
        private System.Windows.Forms.Label nombreApellido;
        private System.Windows.Forms.Button ayuda_codi;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.Label periodo_cartera;
        private System.Windows.Forms.Label Label14;
        private System.Windows.Forms.Button impre;
        private System.Windows.Forms.Label nom_obliga;
        private System.Windows.Forms.DataGridView DatGriCuopen;
        private System.Windows.Forms.DataGridView DgvTotales;
        private System.Windows.Forms.Label Lbl_PerIni;
        private ERP.Core.Compartido.Controles.TexboxSoloNumeros Txt_perIni;
        private ERP.Core.Compartido.Controles.TexboxSoloNumeros Txt_perFin;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.Button Button1;
    }
}
