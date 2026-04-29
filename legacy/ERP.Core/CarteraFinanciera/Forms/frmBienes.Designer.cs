namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmBienes
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmBienes));
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();

            this.CbxClaseRaiz = new System.Windows.Forms.ComboBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.TxtDireccion = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.TxtCodCiudad = new System.Windows.Forms.TextBox();
            this.LblNomCiudad = new System.Windows.Forms.Label();
            this.TxtValorComercial = new System.Windows.Forms.TextBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.TxtValorVehiculo = new System.Windows.Forms.TextBox();
            this.Label5 = new System.Windows.Forms.Label();
            this.TxtModelo = new System.Windows.Forms.TextBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.TxtMarca = new System.Windows.Forms.TextBox();
            this.Label7 = new System.Windows.Forms.Label();
            this.Label8 = new System.Windows.Forms.Label();
            this.CbxClaseVehiculo = new System.Windows.Forms.ComboBox();
            this.TabControl1 = new System.Windows.Forms.TabControl();
            this.TabPage1 = new System.Windows.Forms.TabPage();
            this.linklblbienesHojadeVida = new System.Windows.Forms.LinkLabel();
            this.LlbExitenBienes = new System.Windows.Forms.LinkLabel();
            this.BtnEliminarRaiz = new System.Windows.Forms.Button();
            this.DgwBienesRaices = new System.Windows.Forms.DataGridView();
            this.clmClaseRaiz = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmDireccion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmCiudad = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmValorCial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BtnGrabarBien = new System.Windows.Forms.Button();
            this.ayuda_codi = new System.Windows.Forms.Button();
            this.TabPage2 = new System.Windows.Forms.TabPage();
            this.linklblBuscaVehojaVida = new System.Windows.Forms.LinkLabel();
            this.LlbExitenBienes2 = new System.Windows.Forms.LinkLabel();
            this.BtnEliminarVehiculo = new System.Windows.Forms.Button();
            this.DgwVehiculos = new System.Windows.Forms.DataGridView();
            this.ClmClaseVeh = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmMarca = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmModelo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmVlrCial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BtnGrabarVeh = new System.Windows.Forms.Button();
            this.LblNomEmpresa = new System.Windows.Forms.Label();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.TabControl1.SuspendLayout();
            this.TabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwBienesRaices)).BeginInit();
            this.TabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwVehiculos)).BeginInit();
            this.SuspendLayout();
            //
            // CbxClaseRaiz
            //
            this.CbxClaseRaiz.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxClaseRaiz.FormattingEnabled = true;
            this.CbxClaseRaiz.Items.AddRange(new object[] { "Casa", "Apartamento", "Finca", "Lote" });
            this.CbxClaseRaiz.Location = new System.Drawing.Point(61, 18);
            this.CbxClaseRaiz.Name = "CbxClaseRaiz";
            this.CbxClaseRaiz.Size = new System.Drawing.Size(125, 21);
            this.CbxClaseRaiz.TabIndex = 0;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(8, 21);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(36, 13);
            this.Label1.TabIndex = 1;
            this.Label1.Text = "Clase:";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(209, 21);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(55, 13);
            this.Label2.TabIndex = 2;
            this.Label2.Text = "Direccion:";
            //
            // TxtDireccion
            //
            this.TxtDireccion.Location = new System.Drawing.Point(277, 20);
            this.TxtDireccion.MaxLength = 60;
            this.TxtDireccion.Name = "TxtDireccion";
            this.TxtDireccion.Size = new System.Drawing.Size(350, 20);
            this.TxtDireccion.TabIndex = 3;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(8, 53);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(43, 13);
            this.Label3.TabIndex = 4;
            this.Label3.Text = "Ciudad:";
            //
            // TxtCodCiudad
            //
            this.TxtCodCiudad.Location = new System.Drawing.Point(61, 50);
            this.TxtCodCiudad.Name = "TxtCodCiudad";
            this.TxtCodCiudad.Size = new System.Drawing.Size(89, 20);
            this.TxtCodCiudad.TabIndex = 5;
            this.TxtCodCiudad.LostFocus += new System.EventHandler(this.TxtCodCiudad_LostFocus);
            //
            // LblNomCiudad
            //
            this.LblNomCiudad.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomCiudad.Location = new System.Drawing.Point(192, 50);
            this.LblNomCiudad.Name = "LblNomCiudad";
            this.LblNomCiudad.Size = new System.Drawing.Size(241, 21);
            this.LblNomCiudad.TabIndex = 126;
            //
            // TxtValorComercial
            //
            this.TxtValorComercial.Location = new System.Drawing.Point(501, 50);
            this.TxtValorComercial.Name = "TxtValorComercial";
            this.TxtValorComercial.Size = new System.Drawing.Size(126, 20);
            this.TxtValorComercial.TabIndex = 7;
            this.TxtValorComercial.Text = "0";
            this.TxtValorComercial.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtValorComercial.LostFocus += new System.EventHandler(this.TxtValorComercial_LostFocus);
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(457, 53);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(34, 13);
            this.Label4.TabIndex = 6;
            this.Label4.Text = "Valor:";
            //
            // TxtValorVehiculo
            //
            this.TxtValorVehiculo.Location = new System.Drawing.Point(66, 53);
            this.TxtValorVehiculo.Name = "TxtValorVehiculo";
            this.TxtValorVehiculo.Size = new System.Drawing.Size(126, 20);
            this.TxtValorVehiculo.TabIndex = 15;
            this.TxtValorVehiculo.Text = "0";
            this.TxtValorVehiculo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtValorVehiculo.LostFocus += new System.EventHandler(this.TxtValorVehiculo_LostFocus);
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(8, 56);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(34, 13);
            this.Label5.TabIndex = 14;
            this.Label5.Text = "Valor:";
            //
            // TxtModelo
            //
            this.TxtModelo.Location = new System.Drawing.Point(540, 26);
            this.TxtModelo.Name = "TxtModelo";
            this.TxtModelo.Size = new System.Drawing.Size(89, 20);
            this.TxtModelo.TabIndex = 13;
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(482, 29);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(45, 13);
            this.Label6.TabIndex = 12;
            this.Label6.Text = "Modelo:";
            //
            // TxtMarca
            //
            this.TxtMarca.Location = new System.Drawing.Point(250, 26);
            this.TxtMarca.MaxLength = 60;
            this.TxtMarca.Name = "TxtMarca";
            this.TxtMarca.Size = new System.Drawing.Size(221, 20);
            this.TxtMarca.TabIndex = 11;
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Location = new System.Drawing.Point(199, 29);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(40, 13);
            this.Label7.TabIndex = 10;
            this.Label7.Text = "Marca:";
            //
            // Label8
            //
            this.Label8.AutoSize = true;
            this.Label8.Location = new System.Drawing.Point(8, 27);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(36, 13);
            this.Label8.TabIndex = 9;
            this.Label8.Text = "Clase:";
            //
            // CbxClaseVehiculo
            //
            this.CbxClaseVehiculo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxClaseVehiculo.FormattingEnabled = true;
            this.CbxClaseVehiculo.Items.AddRange(new object[] { "Particular", "P\u00fablico" });
            this.CbxClaseVehiculo.Location = new System.Drawing.Point(66, 24);
            this.CbxClaseVehiculo.Name = "CbxClaseVehiculo";
            this.CbxClaseVehiculo.Size = new System.Drawing.Size(125, 21);
            this.CbxClaseVehiculo.TabIndex = 8;
            //
            // TabControl1
            //
            this.TabControl1.Controls.Add(this.TabPage1);
            this.TabControl1.Controls.Add(this.TabPage2);
            this.TabControl1.Location = new System.Drawing.Point(12, 60);
            this.TabControl1.Name = "TabControl1";
            this.TabControl1.SelectedIndex = 0;
            this.TabControl1.Size = new System.Drawing.Size(641, 278);
            this.TabControl1.TabIndex = 2;
            //
            // TabPage1
            //
            this.TabPage1.Controls.Add(this.linklblbienesHojadeVida);
            this.TabPage1.Controls.Add(this.LlbExitenBienes);
            this.TabPage1.Controls.Add(this.BtnEliminarRaiz);
            this.TabPage1.Controls.Add(this.DgwBienesRaices);
            this.TabPage1.Controls.Add(this.BtnGrabarBien);
            this.TabPage1.Controls.Add(this.TxtValorComercial);
            this.TabPage1.Controls.Add(this.TxtDireccion);
            this.TabPage1.Controls.Add(this.Label4);
            this.TabPage1.Controls.Add(this.CbxClaseRaiz);
            this.TabPage1.Controls.Add(this.LblNomCiudad);
            this.TabPage1.Controls.Add(this.Label1);
            this.TabPage1.Controls.Add(this.ayuda_codi);
            this.TabPage1.Controls.Add(this.Label2);
            this.TabPage1.Controls.Add(this.TxtCodCiudad);
            this.TabPage1.Controls.Add(this.Label3);
            this.TabPage1.Location = new System.Drawing.Point(4, 22);
            this.TabPage1.Name = "TabPage1";
            this.TabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.TabPage1.Size = new System.Drawing.Size(633, 252);
            this.TabPage1.TabIndex = 0;
            this.TabPage1.Tag = "1";
            this.TabPage1.Text = "Bienes Raices";
            this.TabPage1.UseVisualStyleBackColor = true;
            //
            // linklblbienesHojadeVida
            //
            this.linklblbienesHojadeVida.AutoSize = true;
            this.linklblbienesHojadeVida.Location = new System.Drawing.Point(11, 104);
            this.linklblbienesHojadeVida.Name = "linklblbienesHojadeVida";
            this.linklblbienesHojadeVida.Size = new System.Drawing.Size(175, 13);
            this.linklblbienesHojadeVida.TabIndex = 130;
            this.linklblbienesHojadeVida.TabStop = true;
            this.linklblbienesHojadeVida.Text = "Buscar Bienes Raices Hoja de Vida";
            this.linklblbienesHojadeVida.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linklblbienesHojadeVida_LinkClicked);
            //
            // LlbExitenBienes
            //
            this.LlbExitenBienes.AutoSize = true;
            this.LlbExitenBienes.Location = new System.Drawing.Point(246, 104);
            this.LlbExitenBienes.Name = "LlbExitenBienes";
            this.LlbExitenBienes.Size = new System.Drawing.Size(225, 13);
            this.LlbExitenBienes.TabIndex = 129;
            this.LlbExitenBienes.TabStop = true;
            this.LlbExitenBienes.Text = "Existe Bienes Raices de Solicitudes Anteriores";
            this.LlbExitenBienes.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LlbExitenBienes_LinkClicked);
            //
            // BtnEliminarRaiz
            //
            this.BtnEliminarRaiz.BackgroundImage = global::ERP.Core.Properties.Resources.Eliminar;
            this.BtnEliminarRaiz.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnEliminarRaiz.Location = new System.Drawing.Point(589, 82);
            this.BtnEliminarRaiz.Name = "BtnEliminarRaiz";
            this.BtnEliminarRaiz.Size = new System.Drawing.Size(38, 35);
            this.BtnEliminarRaiz.TabIndex = 128;
            this.BtnEliminarRaiz.UseVisualStyleBackColor = true;
            this.BtnEliminarRaiz.Click += new System.EventHandler(this.BtnEliminarRaiz_Click);
            //
            // DgwBienesRaices
            //
            this.DgwBienesRaices.AllowUserToAddRows = false;
            this.DgwBienesRaices.AllowUserToDeleteRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwBienesRaices.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgwBienesRaices.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwBienesRaices.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmClaseRaiz, this.clmDireccion, this.clmCiudad, this.ClmValorCial });
            this.DgwBienesRaices.EnableHeadersVisualStyles = false;
            this.DgwBienesRaices.Location = new System.Drawing.Point(4, 123);
            this.DgwBienesRaices.Name = "DgwBienesRaices";
            this.DgwBienesRaices.ReadOnly = true;
            DataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle6.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwBienesRaices.RowHeadersDefaultCellStyle = DataGridViewCellStyle6;
            this.DgwBienesRaices.RowHeadersVisible = false;
            this.DgwBienesRaices.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwBienesRaices.Size = new System.Drawing.Size(624, 123);
            this.DgwBienesRaices.TabIndex = 128;
            //
            // clmClaseRaiz
            //
            this.clmClaseRaiz.DataPropertyName = "NOMCLASE";
            DataGridViewCellStyle2.BackColor = System.Drawing.Color.Beige;
            this.clmClaseRaiz.DefaultCellStyle = DataGridViewCellStyle2;
            this.clmClaseRaiz.HeaderText = "Clase";
            this.clmClaseRaiz.Name = "clmClaseRaiz";
            this.clmClaseRaiz.ReadOnly = true;
            //
            // clmDireccion
            //
            this.clmDireccion.DataPropertyName = "DIRECCION";
            DataGridViewCellStyle3.BackColor = System.Drawing.Color.Ivory;
            this.clmDireccion.DefaultCellStyle = DataGridViewCellStyle3;
            this.clmDireccion.HeaderText = "Direccion";
            this.clmDireccion.Name = "clmDireccion";
            this.clmDireccion.ReadOnly = true;
            this.clmDireccion.Width = 230;
            //
            // clmCiudad
            //
            this.clmCiudad.DataPropertyName = "NOMCIUDAD";
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.Beige;
            this.clmCiudad.DefaultCellStyle = DataGridViewCellStyle4;
            this.clmCiudad.HeaderText = "Ciudad";
            this.clmCiudad.Name = "clmCiudad";
            this.clmCiudad.ReadOnly = true;
            this.clmCiudad.Width = 150;
            //
            // ClmValorCial
            //
            this.ClmValorCial.DataPropertyName = "VALOR";
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle5.Format = "N2";
            DataGridViewCellStyle5.NullValue = "0";
            this.ClmValorCial.DefaultCellStyle = DataGridViewCellStyle5;
            this.ClmValorCial.HeaderText = "Valor Comercial";
            this.ClmValorCial.Name = "ClmValorCial";
            this.ClmValorCial.ReadOnly = true;
            this.ClmValorCial.Width = 120;
            //
            // BtnGrabarBien
            //
            this.BtnGrabarBien.BackgroundImage = global::ERP.Core.Properties.Resources.disco;
            this.BtnGrabarBien.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnGrabarBien.Location = new System.Drawing.Point(545, 82);
            this.BtnGrabarBien.Name = "BtnGrabarBien";
            this.BtnGrabarBien.Size = new System.Drawing.Size(38, 35);
            this.BtnGrabarBien.TabIndex = 127;
            this.BtnGrabarBien.UseVisualStyleBackColor = true;
            this.BtnGrabarBien.Click += new System.EventHandler(this.BtnGrabarBien_Click);
            //
            // ayuda_codi
            //
            this.ayuda_codi.BackColor = System.Drawing.SystemColors.Menu;
            this.ayuda_codi.Image = ((System.Drawing.Image)(resources.GetObject("ayuda_codi.Image")));
            this.ayuda_codi.Location = new System.Drawing.Point(157, 46);
            this.ayuda_codi.Margin = new System.Windows.Forms.Padding(4);
            this.ayuda_codi.Name = "ayuda_codi";
            this.ayuda_codi.Size = new System.Drawing.Size(28, 28);
            this.ayuda_codi.TabIndex = 125;
            this.ayuda_codi.TabStop = false;
            this.ayuda_codi.UseVisualStyleBackColor = false;
            this.ayuda_codi.Click += new System.EventHandler(this.ayuda_codi_Click);
            //
            // TabPage2
            //
            this.TabPage2.Controls.Add(this.linklblBuscaVehojaVida);
            this.TabPage2.Controls.Add(this.LlbExitenBienes2);
            this.TabPage2.Controls.Add(this.BtnEliminarVehiculo);
            this.TabPage2.Controls.Add(this.DgwVehiculos);
            this.TabPage2.Controls.Add(this.TxtValorVehiculo);
            this.TabPage2.Controls.Add(this.CbxClaseVehiculo);
            this.TabPage2.Controls.Add(this.Label5);
            this.TabPage2.Controls.Add(this.Label8);
            this.TabPage2.Controls.Add(this.TxtModelo);
            this.TabPage2.Controls.Add(this.Label7);
            this.TabPage2.Controls.Add(this.Label6);
            this.TabPage2.Controls.Add(this.TxtMarca);
            this.TabPage2.Controls.Add(this.BtnGrabarVeh);
            this.TabPage2.Location = new System.Drawing.Point(4, 22);
            this.TabPage2.Name = "TabPage2";
            this.TabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.TabPage2.Size = new System.Drawing.Size(633, 252);
            this.TabPage2.TabIndex = 1;
            this.TabPage2.Tag = "2";
            this.TabPage2.Text = "Vehiculos";
            this.TabPage2.UseVisualStyleBackColor = true;
            //
            // linklblBuscaVehojaVida
            //
            this.linklblBuscaVehojaVida.AutoSize = true;
            this.linklblBuscaVehojaVida.Location = new System.Drawing.Point(11, 103);
            this.linklblBuscaVehojaVida.Name = "linklblBuscaVehojaVida";
            this.linklblBuscaVehojaVida.Size = new System.Drawing.Size(153, 13);
            this.linklblBuscaVehojaVida.TabIndex = 132;
            this.linklblBuscaVehojaVida.TabStop = true;
            this.linklblBuscaVehojaVida.Text = "Buscar Vehiculos Hoja de Vida";
            this.linklblBuscaVehojaVida.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linklblBuscaVehojaVida_LinkClicked);
            //
            // LlbExitenBienes2
            //
            this.LlbExitenBienes2.AutoSize = true;
            this.LlbExitenBienes2.Location = new System.Drawing.Point(218, 104);
            this.LlbExitenBienes2.Name = "LlbExitenBienes2";
            this.LlbExitenBienes2.Size = new System.Drawing.Size(267, 13);
            this.LlbExitenBienes2.TabIndex = 131;
            this.LlbExitenBienes2.TabStop = true;
            this.LlbExitenBienes2.Text = "Existe Vehiculos como Bienes de Solicitudes Anteriores";
            this.LlbExitenBienes2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LlbExitenBienes2_LinkClicked);
            //
            // BtnEliminarVehiculo
            //
            this.BtnEliminarVehiculo.BackgroundImage = global::ERP.Core.Properties.Resources.Eliminar;
            this.BtnEliminarVehiculo.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnEliminarVehiculo.Location = new System.Drawing.Point(589, 82);
            this.BtnEliminarVehiculo.Name = "BtnEliminarVehiculo";
            this.BtnEliminarVehiculo.Size = new System.Drawing.Size(38, 35);
            this.BtnEliminarVehiculo.TabIndex = 130;
            this.BtnEliminarVehiculo.UseVisualStyleBackColor = true;
            this.BtnEliminarVehiculo.Click += new System.EventHandler(this.BtnEliminarVehiculo_Click);
            //
            // DgwVehiculos
            //
            this.DgwVehiculos.AllowUserToAddRows = false;
            DataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwVehiculos.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle7;
            this.DgwVehiculos.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwVehiculos.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.ClmClaseVeh, this.ClmMarca, this.ClmModelo, this.ClmVlrCial });
            this.DgwVehiculos.EnableHeadersVisualStyles = false;
            this.DgwVehiculos.Location = new System.Drawing.Point(5, 123);
            this.DgwVehiculos.Name = "DgwVehiculos";
            this.DgwVehiculos.ReadOnly = true;
            DataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle12.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle12.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle12.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle12.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle12.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwVehiculos.RowHeadersDefaultCellStyle = DataGridViewCellStyle12;
            this.DgwVehiculos.RowHeadersVisible = false;
            this.DgwVehiculos.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwVehiculos.Size = new System.Drawing.Size(624, 123);
            this.DgwVehiculos.TabIndex = 129;
            //
            // ClmClaseVeh
            //
            this.ClmClaseVeh.DataPropertyName = "NOMCLASE";
            DataGridViewCellStyle8.BackColor = System.Drawing.Color.Beige;
            this.ClmClaseVeh.DefaultCellStyle = DataGridViewCellStyle8;
            this.ClmClaseVeh.HeaderText = "Clase";
            this.ClmClaseVeh.Name = "ClmClaseVeh";
            this.ClmClaseVeh.ReadOnly = true;
            //
            // ClmMarca
            //
            this.ClmMarca.DataPropertyName = "MARCA";
            DataGridViewCellStyle9.BackColor = System.Drawing.Color.Ivory;
            this.ClmMarca.DefaultCellStyle = DataGridViewCellStyle9;
            this.ClmMarca.HeaderText = "Marca";
            this.ClmMarca.Name = "ClmMarca";
            this.ClmMarca.ReadOnly = true;
            this.ClmMarca.Width = 230;
            //
            // ClmModelo
            //
            this.ClmModelo.DataPropertyName = "MODELO";
            DataGridViewCellStyle10.BackColor = System.Drawing.Color.Beige;
            this.ClmModelo.DefaultCellStyle = DataGridViewCellStyle10;
            this.ClmModelo.HeaderText = "Modelo";
            this.ClmModelo.Name = "ClmModelo";
            this.ClmModelo.ReadOnly = true;
            this.ClmModelo.Width = 150;
            //
            // ClmVlrCial
            //
            this.ClmVlrCial.DataPropertyName = "VALOR";
            DataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle11.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle11.Format = "N2";
            DataGridViewCellStyle11.NullValue = "0";
            this.ClmVlrCial.DefaultCellStyle = DataGridViewCellStyle11;
            this.ClmVlrCial.HeaderText = "Valor Comercial";
            this.ClmVlrCial.Name = "ClmVlrCial";
            this.ClmVlrCial.ReadOnly = true;
            this.ClmVlrCial.Width = 120;
            //
            // BtnGrabarVeh
            //
            this.BtnGrabarVeh.BackgroundImage = global::ERP.Core.Properties.Resources.disco;
            this.BtnGrabarVeh.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnGrabarVeh.Location = new System.Drawing.Point(545, 82);
            this.BtnGrabarVeh.Name = "BtnGrabarVeh";
            this.BtnGrabarVeh.Size = new System.Drawing.Size(38, 35);
            this.BtnGrabarVeh.TabIndex = 5;
            this.BtnGrabarVeh.UseVisualStyleBackColor = true;
            this.BtnGrabarVeh.Click += new System.EventHandler(this.BtnGrabarVeh_Click);
            //
            // LblNomEmpresa
            //
            this.LblNomEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomEmpresa.Location = new System.Drawing.Point(65, 9);
            this.LblNomEmpresa.Name = "LblNomEmpresa";
            this.LblNomEmpresa.Size = new System.Drawing.Size(523, 29);
            this.LblNomEmpresa.TabIndex = 5;
            this.LblNomEmpresa.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(605, 344);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 4;
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // frmBienes
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(665, 387);
            this.Controls.Add(this.LblNomEmpresa);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.TabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "frmBienes";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Bienes";
            this.Load += new System.EventHandler(this.frmBienes_Load);
            this.TabControl1.ResumeLayout(false);
            this.TabPage1.ResumeLayout(false);
            this.TabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwBienesRaices)).EndInit();
            this.TabPage2.ResumeLayout(false);
            this.TabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwVehiculos)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.ComboBox CbxClaseRaiz;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.TextBox TxtDireccion;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.TextBox TxtCodCiudad;
        private System.Windows.Forms.Label LblNomCiudad;
        private System.Windows.Forms.TextBox TxtValorComercial;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.TextBox TxtValorVehiculo;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.TextBox TxtModelo;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.TextBox TxtMarca;
        private System.Windows.Forms.Label Label7;
        private System.Windows.Forms.Label Label8;
        private System.Windows.Forms.ComboBox CbxClaseVehiculo;
        private System.Windows.Forms.TabControl TabControl1;
        private System.Windows.Forms.TabPage TabPage1;
        private System.Windows.Forms.TabPage TabPage2;
        private System.Windows.Forms.Button BtnGrabarBien;
        private System.Windows.Forms.Button BtnGrabarVeh;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.Label LblNomEmpresa;
        private System.Windows.Forms.DataGridView DgwBienesRaices;
        private System.Windows.Forms.DataGridView DgwVehiculos;
        private System.Windows.Forms.Button BtnEliminarRaiz;
        private System.Windows.Forms.Button BtnEliminarVehiculo;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmClaseVeh;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmMarca;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmModelo;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmVlrCial;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmClaseRaiz;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmDireccion;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmCiudad;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmValorCial;
        private System.Windows.Forms.LinkLabel LlbExitenBienes;
        private System.Windows.Forms.LinkLabel LlbExitenBienes2;
        private System.Windows.Forms.LinkLabel linklblbienesHojadeVida;
        private System.Windows.Forms.LinkLabel linklblBuscaVehojaVida;
        private System.Windows.Forms.Button ayuda_codi;
    }
}
