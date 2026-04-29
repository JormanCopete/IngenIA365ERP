namespace ERP.Core.Compartido.Configuracion
{
    partial class frmtasacptos
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
            this.DgvTasas = new System.Windows.Forms.DataGridView();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.LblLinea = new System.Windows.Forms.Label();
            this.LblConcepto = new System.Windows.Forms.Label();
            this.TxtPLazoInicial = new System.Windows.Forms.TextBox();
            this.TxtPlazoFInal = new System.Windows.Forms.TextBox();
            this.TxtValor = new System.Windows.Forms.TextBox();
            this.CbxTipoDscto = new System.Windows.Forms.ComboBox();
            this.BtnAgregar = new System.Windows.Forms.Button();
            this.BtnQuitar = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.Label4 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DgvTasas)).BeginInit();
            this.SuspendLayout();
            //
            // DgvTasas
            //
            this.DgvTasas.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvTasas.Location = new System.Drawing.Point(12, 120);
            this.DgvTasas.Name = "DgvTasas";
            this.DgvTasas.Size = new System.Drawing.Size(460, 150);
            this.DgvTasas.TabIndex = 0;
            //
            // opcion
            //
            this.opcion.Location = new System.Drawing.Point(12, 12);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(25, 27);
            this.opcion.TabIndex = 1;
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            //
            // LblLinea
            //
            this.LblLinea.Location = new System.Drawing.Point(70, 12);
            this.LblLinea.Name = "LblLinea";
            this.LblLinea.Size = new System.Drawing.Size(100, 23);
            this.LblLinea.TabIndex = 2;
            //
            // LblConcepto
            //
            this.LblConcepto.Location = new System.Drawing.Point(200, 12);
            this.LblConcepto.Name = "LblConcepto";
            this.LblConcepto.Size = new System.Drawing.Size(100, 23);
            this.LblConcepto.TabIndex = 3;
            //
            // TxtPLazoInicial
            //
            this.TxtPLazoInicial.Location = new System.Drawing.Point(100, 50);
            this.TxtPLazoInicial.Name = "TxtPLazoInicial";
            this.TxtPLazoInicial.Size = new System.Drawing.Size(80, 20);
            this.TxtPLazoInicial.TabIndex = 4;
            //
            // TxtPlazoFInal
            //
            this.TxtPlazoFInal.Location = new System.Drawing.Point(260, 50);
            this.TxtPlazoFInal.Name = "TxtPlazoFInal";
            this.TxtPlazoFInal.Size = new System.Drawing.Size(80, 20);
            this.TxtPlazoFInal.TabIndex = 5;
            //
            // TxtValor
            //
            this.TxtValor.Location = new System.Drawing.Point(100, 80);
            this.TxtValor.Name = "TxtValor";
            this.TxtValor.Size = new System.Drawing.Size(80, 20);
            this.TxtValor.TabIndex = 6;
            //
            // CbxTipoDscto
            //
            this.CbxTipoDscto.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxTipoDscto.Items.AddRange(new object[] { "", "Porcentaje", "Valor" });
            this.CbxTipoDscto.Location = new System.Drawing.Point(260, 80);
            this.CbxTipoDscto.Name = "CbxTipoDscto";
            this.CbxTipoDscto.Size = new System.Drawing.Size(121, 21);
            this.CbxTipoDscto.TabIndex = 7;
            //
            // BtnAgregar
            //
            this.BtnAgregar.Location = new System.Drawing.Point(400, 50);
            this.BtnAgregar.Name = "BtnAgregar";
            this.BtnAgregar.Size = new System.Drawing.Size(75, 23);
            this.BtnAgregar.TabIndex = 8;
            this.BtnAgregar.Text = "+";
            this.BtnAgregar.UseVisualStyleBackColor = true;
            this.BtnAgregar.Click += new System.EventHandler(this.BtnAgregar_Click);
            //
            // BtnQuitar
            //
            this.BtnQuitar.Location = new System.Drawing.Point(400, 80);
            this.BtnQuitar.Name = "BtnQuitar";
            this.BtnQuitar.Size = new System.Drawing.Size(75, 23);
            this.BtnQuitar.TabIndex = 9;
            this.BtnQuitar.Text = "-";
            this.BtnQuitar.UseVisualStyleBackColor = true;
            this.BtnQuitar.Click += new System.EventHandler(this.BtnQuitar_Click);
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(12, 53);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(68, 13);
            this.Label1.TabIndex = 10;
            this.Label1.Text = "Plazo Inicial:";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(190, 53);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(62, 13);
            this.Label2.TabIndex = 11;
            this.Label2.Text = "Plazo Final:";
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(12, 83);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(34, 13);
            this.Label3.TabIndex = 12;
            this.Label3.Text = "Valor:";
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(190, 83);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(64, 13);
            this.Label4.TabIndex = 13;
            this.Label4.Text = "Tipo Dscto:";
            //
            // frmtasacptos
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 281);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.BtnQuitar);
            this.Controls.Add(this.BtnAgregar);
            this.Controls.Add(this.CbxTipoDscto);
            this.Controls.Add(this.TxtValor);
            this.Controls.Add(this.TxtPlazoFInal);
            this.Controls.Add(this.TxtPLazoInicial);
            this.Controls.Add(this.LblConcepto);
            this.Controls.Add(this.LblLinea);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.DgvTasas);
            this.Name = "frmtasacptos";
            this.Text = "Tasas por Conceptos";
            ((System.ComponentModel.ISupportInitialize)(this.DgvTasas)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.DataGridView DgvTasas;
        internal ERP.Core.Compartido.Controles.SasToolBar opcion;
        public System.Windows.Forms.Label LblLinea;
        public System.Windows.Forms.Label LblConcepto;
        internal System.Windows.Forms.TextBox TxtPLazoInicial;
        internal System.Windows.Forms.TextBox TxtPlazoFInal;
        internal System.Windows.Forms.TextBox TxtValor;
        internal System.Windows.Forms.ComboBox CbxTipoDscto;
        internal System.Windows.Forms.Button BtnAgregar;
        internal System.Windows.Forms.Button BtnQuitar;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.Label Label4;
    }
}
