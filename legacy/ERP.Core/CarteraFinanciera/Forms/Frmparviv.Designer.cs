namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class Frmparviv
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
            this.components = new System.ComponentModel.Container();
            this.LblNomEmpresa = new System.Windows.Forms.Label();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.BtnGrabar = new System.Windows.Forms.Button();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label8 = new System.Windows.Forms.Label();
            this.CbxMoneda = new System.Windows.Forms.ComboBox();
            this.ChkSubsidio = new System.Windows.Forms.CheckBox();
            this.ChkIntSocial = new System.Windows.Forms.CheckBox();
            this.CbxClaseVivienda = new System.Windows.Forms.ComboBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.Txtvalor = new System.Windows.Forms.TextBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.CbxDesembolso = new System.Windows.Forms.ComboBox();
            this.CbxEntidadRedescuento = new System.Windows.Forms.ComboBox();
            this.CbxTipoVivienda = new System.Windows.Forms.ComboBox();
            this.BtnEliminar = new System.Windows.Forms.Button();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.GroupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // LblNomEmpresa
            //
            this.LblNomEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomEmpresa.Location = new System.Drawing.Point(36, 9);
            this.LblNomEmpresa.Name = "LblNomEmpresa";
            this.LblNomEmpresa.Size = new System.Drawing.Size(523, 29);
            this.LblNomEmpresa.TabIndex = 6;
            this.LblNomEmpresa.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(544, 219);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 7;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir");
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // BtnGrabar
            //
            this.BtnGrabar.BackgroundImage = global::ERP.Core.Properties.Resources.disco;
            this.BtnGrabar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnGrabar.Location = new System.Drawing.Point(456, 219);
            this.BtnGrabar.Name = "BtnGrabar";
            this.BtnGrabar.Size = new System.Drawing.Size(38, 35);
            this.BtnGrabar.TabIndex = 128;
            this.ToolTip1.SetToolTip(this.BtnGrabar, "Grabar");
            this.BtnGrabar.UseVisualStyleBackColor = true;
            this.BtnGrabar.Click += new System.EventHandler(this.BtnGrabar_Click);
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.Label8);
            this.GroupBox1.Controls.Add(this.CbxMoneda);
            this.GroupBox1.Controls.Add(this.ChkSubsidio);
            this.GroupBox1.Controls.Add(this.ChkIntSocial);
            this.GroupBox1.Controls.Add(this.CbxClaseVivienda);
            this.GroupBox1.Controls.Add(this.Label6);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.Txtvalor);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Controls.Add(this.Label5);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.CbxDesembolso);
            this.GroupBox1.Controls.Add(this.CbxEntidadRedescuento);
            this.GroupBox1.Controls.Add(this.CbxTipoVivienda);
            this.GroupBox1.Location = new System.Drawing.Point(16, 55);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(575, 156);
            this.GroupBox1.TabIndex = 129;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Generales";
            //
            // Label8
            //
            this.Label8.Location = new System.Drawing.Point(305, 116);
            this.Label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(51, 20);
            this.Label8.TabIndex = 254;
            this.Label8.Text = "Moneda";
            //
            // CbxMoneda
            //
            this.CbxMoneda.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxMoneda.FormattingEnabled = true;
            this.CbxMoneda.Items.AddRange(new object[] { "", "Pesos", "Uvr" });
            this.CbxMoneda.Location = new System.Drawing.Point(363, 114);
            this.CbxMoneda.Name = "CbxMoneda";
            this.CbxMoneda.Size = new System.Drawing.Size(136, 21);
            this.CbxMoneda.TabIndex = 253;
            //
            // ChkSubsidio
            //
            this.ChkSubsidio.AutoSize = true;
            this.ChkSubsidio.Location = new System.Drawing.Point(398, 57);
            this.ChkSubsidio.Name = "ChkSubsidio";
            this.ChkSubsidio.Size = new System.Drawing.Size(66, 17);
            this.ChkSubsidio.TabIndex = 252;
            this.ChkSubsidio.Text = "Subsidio";
            this.ChkSubsidio.UseVisualStyleBackColor = true;
            //
            // ChkIntSocial
            //
            this.ChkIntSocial.AutoSize = true;
            this.ChkIntSocial.Location = new System.Drawing.Point(398, 25);
            this.ChkIntSocial.Name = "ChkIntSocial";
            this.ChkIntSocial.Size = new System.Drawing.Size(90, 17);
            this.ChkIntSocial.TabIndex = 251;
            this.ChkIntSocial.Text = "Interes Social";
            this.ChkIntSocial.UseVisualStyleBackColor = true;
            //
            // CbxClaseVivienda
            //
            this.CbxClaseVivienda.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxClaseVivienda.FormattingEnabled = true;
            this.CbxClaseVivienda.Items.AddRange(new object[] { "", "Nueva", "Usada", "Mejoramiento", "Lote con Servicios", "Construccion en sitio propio", "" });
            this.CbxClaseVivienda.Location = new System.Drawing.Point(140, 24);
            this.CbxClaseVivienda.Name = "CbxClaseVivienda";
            this.CbxClaseVivienda.Size = new System.Drawing.Size(228, 21);
            this.CbxClaseVivienda.TabIndex = 241;
            //
            // Label6
            //
            this.Label6.Location = new System.Drawing.Point(307, 88);
            this.Label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(48, 20);
            this.Label6.TabIndex = 250;
            this.Label6.Text = "Valor";
            //
            // Label1
            //
            this.Label1.Location = new System.Drawing.Point(15, 26);
            this.Label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(118, 20);
            this.Label1.TabIndex = 246;
            this.Label1.Text = "Clase Vivienda";
            //
            // Txtvalor
            //
            this.Txtvalor.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.Txtvalor.Location = new System.Drawing.Point(363, 84);
            this.Txtvalor.Margin = new System.Windows.Forms.Padding(4);
            this.Txtvalor.MaxLength = 14;
            this.Txtvalor.Name = "Txtvalor";
            this.Txtvalor.Size = new System.Drawing.Size(159, 20);
            this.Txtvalor.TabIndex = 244;
            this.Txtvalor.Text = "0";
            this.Txtvalor.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.Txtvalor.LostFocus += new System.EventHandler(this.Txtvalor_LostFocus);
            //
            // Label2
            //
            this.Label2.Location = new System.Drawing.Point(15, 56);
            this.Label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(107, 20);
            this.Label2.TabIndex = 247;
            this.Label2.Text = "Tipo de Vivienda";
            //
            // Label5
            //
            this.Label5.Location = new System.Drawing.Point(15, 87);
            this.Label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(118, 20);
            this.Label5.TabIndex = 249;
            this.Label5.Text = "Entidad  Redescuento";
            //
            // Label3
            //
            this.Label3.Location = new System.Drawing.Point(15, 117);
            this.Label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(90, 20);
            this.Label3.TabIndex = 248;
            this.Label3.Text = "Desembolso";
            //
            // CbxDesembolso
            //
            this.CbxDesembolso.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxDesembolso.FormattingEnabled = true;
            this.CbxDesembolso.Items.AddRange(new object[] { "", "Desembolso Directo", "Subrogacion" });
            this.CbxDesembolso.Location = new System.Drawing.Point(140, 114);
            this.CbxDesembolso.Name = "CbxDesembolso";
            this.CbxDesembolso.Size = new System.Drawing.Size(112, 21);
            this.CbxDesembolso.TabIndex = 245;
            //
            // CbxEntidadRedescuento
            //
            this.CbxEntidadRedescuento.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxEntidadRedescuento.FormattingEnabled = true;
            this.CbxEntidadRedescuento.Items.AddRange(new object[] { "Ninguna", "Findeter" });
            this.CbxEntidadRedescuento.Location = new System.Drawing.Point(140, 84);
            this.CbxEntidadRedescuento.Name = "CbxEntidadRedescuento";
            this.CbxEntidadRedescuento.Size = new System.Drawing.Size(112, 21);
            this.CbxEntidadRedescuento.TabIndex = 243;
            //
            // CbxTipoVivienda
            //
            this.CbxTipoVivienda.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxTipoVivienda.FormattingEnabled = true;
            this.CbxTipoVivienda.Items.AddRange(new object[] { "", "Rango 1, mayor a vis y <= 643.100 UVR", "Rango 2, mayor a 643.100 UVR  y <= 2,411,62 UVR" });
            this.CbxTipoVivienda.Location = new System.Drawing.Point(140, 54);
            this.CbxTipoVivienda.Name = "CbxTipoVivienda";
            this.CbxTipoVivienda.Size = new System.Drawing.Size(228, 21);
            this.CbxTipoVivienda.TabIndex = 242;
            //
            // BtnEliminar
            //
            //this.BtnEliminar.BackgroundImage = global::ERP.Core.Properties.Resources.Eliminar;
            this.BtnEliminar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnEliminar.Location = new System.Drawing.Point(500, 219);
            this.BtnEliminar.Name = "BtnEliminar";
            this.BtnEliminar.Size = new System.Drawing.Size(38, 35);
            this.BtnEliminar.TabIndex = 130;
            this.ToolTip1.SetToolTip(this.BtnEliminar, "Eliminar");
            this.BtnEliminar.UseVisualStyleBackColor = true;
            this.BtnEliminar.Click += new System.EventHandler(this.BtnEliminar_Click);
            //
            // Frmparviv
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(606, 264);
            this.Controls.Add(this.BtnEliminar);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.BtnGrabar);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.LblNomEmpresa);
            this.Name = "Frmparviv";
            this.Text = "Datos Creditos Vivienda";
            this.Load += new System.EventHandler(this.Frmparviv_Load);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label LblNomEmpresa;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.Button BtnGrabar;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label8;
        private System.Windows.Forms.ComboBox CbxMoneda;
        private System.Windows.Forms.CheckBox ChkSubsidio;
        private System.Windows.Forms.CheckBox ChkIntSocial;
        private System.Windows.Forms.ComboBox CbxClaseVivienda;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.TextBox Txtvalor;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.ComboBox CbxDesembolso;
        private System.Windows.Forms.ComboBox CbxEntidadRedescuento;
        private System.Windows.Forms.ComboBox CbxTipoVivienda;
        private System.Windows.Forms.Button BtnEliminar;
        private System.Windows.Forms.ToolTip ToolTip1;
    }
}
