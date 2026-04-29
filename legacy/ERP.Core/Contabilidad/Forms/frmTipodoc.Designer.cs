namespace ERP.Core.Contabilidad.Forms
{
    partial class frmTipodoc
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components != null)
                    components.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        [System.Diagnostics.DebuggerStepThrough]
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmTipodoc));
            this.CbxTipodocumento = new System.Windows.Forms.ComboBox();
            this.GbxTipodocumento = new System.Windows.Forms.GroupBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.LblTipodocumento = new System.Windows.Forms.Label();
            this.TxtNumeroDocumento = new System.Windows.Forms.TextBox();
            this.BtnAceptar = new System.Windows.Forms.Button();
            this.BtnCancelar = new System.Windows.Forms.Button();
            this.Label22 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.GbxTipodocumento.SuspendLayout();
            this.SuspendLayout();
            //
            // CbxTipodocumento
            //
            this.CbxTipodocumento.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxTipodocumento.FormattingEnabled = true;
            this.CbxTipodocumento.Items.AddRange(new object[] { "CH-Cheque", "CG-Consignacion", "ND-Nota Debito", "NC-Nota Credito", "TR-Traslado" });
            this.CbxTipodocumento.Location = new System.Drawing.Point(127, 24);
            this.CbxTipodocumento.Name = "CbxTipodocumento";
            this.CbxTipodocumento.Size = new System.Drawing.Size(120, 21);
            this.CbxTipodocumento.TabIndex = 0;
            //
            // GbxTipodocumento
            //
            this.GbxTipodocumento.Controls.Add(this.Label1);
            this.GbxTipodocumento.Controls.Add(this.LblTipodocumento);
            this.GbxTipodocumento.Controls.Add(this.TxtNumeroDocumento);
            this.GbxTipodocumento.Controls.Add(this.CbxTipodocumento);
            this.GbxTipodocumento.Location = new System.Drawing.Point(12, 17);
            this.GbxTipodocumento.Name = "GbxTipodocumento";
            this.GbxTipodocumento.Size = new System.Drawing.Size(314, 88);
            this.GbxTipodocumento.TabIndex = 1;
            this.GbxTipodocumento.TabStop = false;
            this.GbxTipodocumento.Text = "Datos para el documento";
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(8, 56);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(117, 13);
            this.Label1.TabIndex = 2;
            this.Label1.Text = "Número de Documento";
            //
            // LblTipodocumento
            //
            this.LblTipodocumento.AutoSize = true;
            this.LblTipodocumento.Location = new System.Drawing.Point(8, 24);
            this.LblTipodocumento.Name = "LblTipodocumento";
            this.LblTipodocumento.Size = new System.Drawing.Size(101, 13);
            this.LblTipodocumento.TabIndex = 3;
            this.LblTipodocumento.Text = "Tipo de Documento";
            //
            // TxtNumeroDocumento
            //
            this.TxtNumeroDocumento.Location = new System.Drawing.Point(127, 54);
            this.TxtNumeroDocumento.MaxLength = 12;
            this.TxtNumeroDocumento.Name = "TxtNumeroDocumento";
            this.TxtNumeroDocumento.Size = new System.Drawing.Size(120, 20);
            this.TxtNumeroDocumento.TabIndex = 1;
            //
            // BtnAceptar
            //
            this.BtnAceptar.Location = new System.Drawing.Point(75, 117);
            this.BtnAceptar.Name = "BtnAceptar";
            this.BtnAceptar.Size = new System.Drawing.Size(75, 23);
            this.BtnAceptar.TabIndex = 2;
            this.BtnAceptar.Text = "&Aceptar";
            this.BtnAceptar.UseVisualStyleBackColor = true;
            this.BtnAceptar.Click += new System.EventHandler(this.BtnAceptar_Click);
            //
            // BtnCancelar
            //
            this.BtnCancelar.Location = new System.Drawing.Point(184, 117);
            this.BtnCancelar.Name = "BtnCancelar";
            this.BtnCancelar.Size = new System.Drawing.Size(75, 23);
            this.BtnCancelar.TabIndex = 3;
            this.BtnCancelar.Text = "&Cancelar";
            this.BtnCancelar.UseVisualStyleBackColor = true;
            this.BtnCancelar.Click += new System.EventHandler(this.BtnCancelar_Click);
            //
            // Label22
            //
            this.Label22.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label22.ForeColor = System.Drawing.SystemColors.ActiveCaption;
            this.Label22.Location = new System.Drawing.Point(-1, 4);
            this.Label22.Name = "Label22";
            this.Label22.Size = new System.Drawing.Size(350, 10);
            this.Label22.TabIndex = 432;
            this.Label22.Text = resources.GetString("Label22.Text");
            //
            // Label2
            //
            this.Label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label2.ForeColor = System.Drawing.SystemColors.ActiveCaption;
            this.Label2.Location = new System.Drawing.Point(-1, 105);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(352, 10);
            this.Label2.TabIndex = 433;
            this.Label2.Text = resources.GetString("Label2.Text");
            //
            // frmTipodoc
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(347, 145);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label22);
            this.Controls.Add(this.BtnCancelar);
            this.Controls.Add(this.BtnAceptar);
            this.Controls.Add(this.GbxTipodocumento);
            this.Name = "frmTipodoc";
            this.Text = "Tipo de documento para conciliacion";
            this.Load += new System.EventHandler(this.frmTipodoc_Load);
            this.GbxTipodocumento.ResumeLayout(false);
            this.GbxTipodocumento.PerformLayout();
            this.ResumeLayout(false);
        }

        internal System.Windows.Forms.ComboBox CbxTipodocumento;
        internal System.Windows.Forms.GroupBox GbxTipodocumento;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.Label LblTipodocumento;
        internal System.Windows.Forms.TextBox TxtNumeroDocumento;
        internal System.Windows.Forms.Button BtnAceptar;
        internal System.Windows.Forms.Button BtnCancelar;
        internal System.Windows.Forms.Label Label22;
        internal System.Windows.Forms.Label Label2;
    }
}
