namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmrecaudo
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
            this.TsysEmpresa = new System.Windows.Forms.TextBox();
            this.Label33 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.TxtCodigoBarras = new System.Windows.Forms.TextBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.TxtFecha = new System.Windows.Forms.TextBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.Txtvalor = new System.Windows.Forms.TextBox();
            this.Label5 = new System.Windows.Forms.Label();
            this.TxtCedula = new System.Windows.Forms.TextBox();
            this.CmbGuardar = new System.Windows.Forms.Button();
            this.CmbSalir = new System.Windows.Forms.Button();
            this.LblMora = new System.Windows.Forms.Label();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.TxtFactura = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.TxtPagar = new System.Windows.Forms.TextBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // TsysEmpresa
            //
            this.TsysEmpresa.BackColor = System.Drawing.SystemColors.Control;
            this.TsysEmpresa.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.TsysEmpresa.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.TsysEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f);
            this.TsysEmpresa.Location = new System.Drawing.Point(73, 2);
            this.TsysEmpresa.Margin = new System.Windows.Forms.Padding(4);
            this.TsysEmpresa.Multiline = true;
            this.TsysEmpresa.Name = "TsysEmpresa";
            this.TsysEmpresa.ReadOnly = true;
            this.TsysEmpresa.Size = new System.Drawing.Size(321, 36);
            this.TsysEmpresa.TabIndex = 254;
            this.TsysEmpresa.TabStop = false;
            this.TsysEmpresa.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            //
            // Label33
            //
            this.Label33.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f);
            this.Label33.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label33.Location = new System.Drawing.Point(9, 2);
            this.Label33.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label33.Name = "Label33";
            this.Label33.Size = new System.Drawing.Size(66, 30);
            this.Label33.TabIndex = 253;
            this.Label33.Text = "Empresa";
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(2, 34);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(775, 10);
            this.GroupBox1.TabIndex = 258;
            this.GroupBox1.TabStop = false;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(12, 65);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(72, 13);
            this.Label1.TabIndex = 259;
            this.Label1.Text = "Codigo barras";
            //
            // TxtCodigoBarras
            //
            this.TxtCodigoBarras.Location = new System.Drawing.Point(95, 62);
            this.TxtCodigoBarras.Name = "TxtCodigoBarras";
            this.TxtCodigoBarras.Size = new System.Drawing.Size(284, 20);
            this.TxtCodigoBarras.TabIndex = 0;
            this.TxtCodigoBarras.TextChanged += new System.EventHandler(this.TxtCodigoBarras_TextChanged);
            this.TxtCodigoBarras.LostFocus += new System.EventHandler(this.TxtCodigoBarras_LostFocus);
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(12, 91);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(37, 13);
            this.Label2.TabIndex = 259;
            this.Label2.Text = "Fecha";
            //
            // TxtFecha
            //
            this.TxtFecha.Enabled = false;
            this.TxtFecha.Location = new System.Drawing.Point(95, 88);
            this.TxtFecha.Name = "TxtFecha";
            this.TxtFecha.Size = new System.Drawing.Size(100, 20);
            this.TxtFecha.TabIndex = 1;
            this.TxtFecha.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(12, 117);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(67, 13);
            this.Label4.TabIndex = 259;
            this.Label4.Text = "Valor factura";
            //
            // Txtvalor
            //
            this.Txtvalor.Enabled = false;
            this.Txtvalor.Location = new System.Drawing.Point(95, 114);
            this.Txtvalor.Name = "Txtvalor";
            this.Txtvalor.Size = new System.Drawing.Size(100, 20);
            this.Txtvalor.TabIndex = 3;
            this.Txtvalor.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(12, 143);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(40, 13);
            this.Label5.TabIndex = 259;
            this.Label5.Text = "Cedula";
            //
            // TxtCedula
            //
            this.TxtCedula.Enabled = false;
            this.TxtCedula.Location = new System.Drawing.Point(95, 140);
            this.TxtCedula.Name = "TxtCedula";
            this.TxtCedula.Size = new System.Drawing.Size(100, 20);
            this.TxtCedula.TabIndex = 5;
            this.TxtCedula.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // CmbGuardar
            //
            this.CmbGuardar.AutoSize = true;
            this.CmbGuardar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbGuardar.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbGuardar.Image = global::ERP.Core.Properties.Resources.disco;
            this.CmbGuardar.Location = new System.Drawing.Point(300, 149);
            this.CmbGuardar.Name = "CmbGuardar";
            this.CmbGuardar.Size = new System.Drawing.Size(38, 38);
            this.CmbGuardar.TabIndex = 6;
            this.ToolTip1.SetToolTip(this.CmbGuardar, "Guardar");
            this.CmbGuardar.UseVisualStyleBackColor = true;
            this.CmbGuardar.Click += new System.EventHandler(this.CmbGuardar_Click);
            //
            // CmbSalir
            //
            this.CmbSalir.AutoSize = true;
            this.CmbSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbSalir.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.CmbSalir.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Olive;
            this.CmbSalir.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.CmbSalir.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbSalir.Image = global::ERP.Core.Properties.Resources.Salir031;
            this.CmbSalir.Location = new System.Drawing.Point(341, 149);
            this.CmbSalir.Name = "CmbSalir";
            this.CmbSalir.Size = new System.Drawing.Size(38, 38);
            this.CmbSalir.TabIndex = 7;
            this.ToolTip1.SetToolTip(this.CmbSalir, "Salir");
            this.CmbSalir.UseVisualStyleBackColor = true;
            this.CmbSalir.Click += new System.EventHandler(this.CmbSalir_Click);
            //
            // LblMora
            //
            this.LblMora.Font = new System.Drawing.Font("Microsoft Sans Serif", 36.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblMora.ForeColor = System.Drawing.Color.SaddleBrown;
            this.LblMora.Location = new System.Drawing.Point(204, 143);
            this.LblMora.Name = "LblMora";
            this.LblMora.Size = new System.Drawing.Size(59, 50);
            this.LblMora.TabIndex = 263;
            this.LblMora.Text = "0";
            //
            // TxtFactura
            //
            this.TxtFactura.Location = new System.Drawing.Point(279, 88);
            this.TxtFactura.Name = "TxtFactura";
            this.TxtFactura.Size = new System.Drawing.Size(100, 20);
            this.TxtFactura.TabIndex = 2;
            this.TxtFactura.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(204, 94);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(51, 13);
            this.Label3.TabIndex = 264;
            this.Label3.Text = "No. Fact.";
            //
            // TxtPagar
            //
            this.TxtPagar.BackColor = System.Drawing.Color.Gainsboro;
            this.TxtPagar.ForeColor = System.Drawing.Color.Chocolate;
            this.TxtPagar.Location = new System.Drawing.Point(279, 117);
            this.TxtPagar.Name = "TxtPagar";
            this.TxtPagar.Size = new System.Drawing.Size(100, 20);
            this.TxtPagar.TabIndex = 4;
            this.TxtPagar.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtPagar.TextChanged += new System.EventHandler(this.TxtPagar_TextChanged);
            this.TxtPagar.LostFocus += new System.EventHandler(this.TxtPagar_LostFocus);
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(201, 120);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(62, 13);
            this.Label6.TabIndex = 266;
            this.Label6.Text = "Valor Pagar";
            //
            // frmrecaudo
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(399, 196);
            this.ControlBox = false;
            this.Controls.Add(this.TxtPagar);
            this.Controls.Add(this.Label6);
            this.Controls.Add(this.TxtFactura);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.LblMora);
            this.Controls.Add(this.CmbGuardar);
            this.Controls.Add(this.CmbSalir);
            this.Controls.Add(this.TxtCedula);
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.Txtvalor);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.TxtFecha);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.TxtCodigoBarras);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.TsysEmpresa);
            this.Controls.Add(this.Label33);
            this.Name = "frmrecaudo";
            this.Load += new System.EventHandler(this.frmbase_Load);
            this.LostFocus += new System.EventHandler(this.frmrecaudo_LostFocus);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.TextBox TsysEmpresa;
        private System.Windows.Forms.Label Label33;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.TextBox TxtCodigoBarras;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.TextBox TxtFecha;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.TextBox Txtvalor;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.TextBox TxtCedula;
        private System.Windows.Forms.Button CmbGuardar;
        private System.Windows.Forms.Button CmbSalir;
        private System.Windows.Forms.Label LblMora;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.TextBox TxtFactura;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.TextBox TxtPagar;
        private System.Windows.Forms.Label Label6;
    }
}
