namespace ERP.Core.Contabilidad.Forms
{
    partial class cnt_cuadredoc
    {
        private System.ComponentModel.IContainer components = null;

        [System.Diagnostics.DebuggerStepThrough]
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(cnt_cuadredoc));
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.txtagencia = new System.Windows.Forms.TextBox();
            this.Txtcencos = new System.Windows.Forms.TextBox();
            this.DtpFecha = new System.Windows.Forms.DateTimePicker();
            this.Label17 = new System.Windows.Forms.Label();
            this.helpcencos = new System.Windows.Forms.Button();
            this.HelpAgencias = new System.Windows.Forms.Button();
            this.Label8 = new System.Windows.Forms.Label();
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.help_cuentacon = new System.Windows.Forms.Button();
            this.txtComprobante = new System.Windows.Forms.TextBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.TxtCreditos = new System.Windows.Forms.TextBox();
            this.txtDebitos = new System.Windows.Forms.TextBox();
            this.Label15 = new System.Windows.Forms.Label();
            this.Label14 = new System.Windows.Forms.Label();
            this.TxtDiferencia = new System.Windows.Forms.TextBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.txtCuentaCierre = new System.Windows.Forms.TextBox();
            this.txtDebito = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.txtCredito = new System.Windows.Forms.TextBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.cmbGrabar = new System.Windows.Forms.Button();
            this.TxtConseCpte = new System.Windows.Forms.TextBox();
            this.Label7 = new System.Windows.Forms.Label();
            this.HelpTerceros = new System.Windows.Forms.Button();
            this.LblnombreCajero = new System.Windows.Forms.Label();
            this.GroupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.txtagencia);
            this.GroupBox1.Controls.Add(this.Txtcencos);
            this.GroupBox1.Controls.Add(this.DtpFecha);
            this.GroupBox1.Controls.Add(this.Label17);
            this.GroupBox1.Controls.Add(this.helpcencos);
            this.GroupBox1.Controls.Add(this.HelpAgencias);
            this.GroupBox1.Controls.Add(this.Label8);
            this.GroupBox1.Controls.Add(this.TxtCodigoter);
            this.GroupBox1.Controls.Add(this.help_cuentacon);
            this.GroupBox1.Controls.Add(this.txtComprobante);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Controls.Add(this.TxtCreditos);
            this.GroupBox1.Controls.Add(this.txtDebitos);
            this.GroupBox1.Controls.Add(this.Label14);
            this.GroupBox1.Controls.Add(this.TxtDiferencia);
            this.GroupBox1.Controls.Add(this.Label4);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.txtCuentaCierre);
            this.GroupBox1.Controls.Add(this.txtDebito);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.txtCredito);
            this.GroupBox1.Controls.Add(this.Label6);
            this.GroupBox1.Controls.Add(this.cmbGrabar);
            this.GroupBox1.Controls.Add(this.TxtConseCpte);
            this.GroupBox1.Controls.Add(this.HelpTerceros);
            this.GroupBox1.Controls.Add(this.Label15);
            this.GroupBox1.Controls.Add(this.Label5);
            this.GroupBox1.Controls.Add(this.Label7);
            this.GroupBox1.Location = new System.Drawing.Point(8, 5);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(694, 117);
            this.GroupBox1.TabIndex = 288;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Comprobante";
            //
            // txtagencia
            //
            this.txtagencia.Enabled = false;
            this.txtagencia.Location = new System.Drawing.Point(595, 48);
            this.txtagencia.MaxLength = 4;
            this.txtagencia.Name = "txtagencia";
            this.txtagencia.Size = new System.Drawing.Size(56, 21);
            this.txtagencia.TabIndex = 9;
            this.txtagencia.Text = "9999";
            this.txtagencia.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Txtcencos
            //
            this.Txtcencos.Location = new System.Drawing.Point(433, 48);
            this.Txtcencos.MaxLength = 8;
            this.Txtcencos.Name = "Txtcencos";
            this.Txtcencos.Size = new System.Drawing.Size(84, 21);
            this.Txtcencos.TabIndex = 4;
            this.Txtcencos.LostFocus += new System.EventHandler(this.Txtcencos_LostFocus);
            //
            // DtpFecha
            //
            this.DtpFecha.CustomFormat = "dd-MMM-yyyy";
            this.DtpFecha.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFecha.Location = new System.Drawing.Point(85, 82);
            this.DtpFecha.Name = "DtpFecha";
            this.DtpFecha.Size = new System.Drawing.Size(111, 21);
            this.DtpFecha.TabIndex = 289;
            //
            // Label17
            //
            this.Label17.Location = new System.Drawing.Point(4, 82);
            this.Label17.Name = "Label17";
            this.Label17.Size = new System.Drawing.Size(47, 20);
            this.Label17.TabIndex = 290;
            this.Label17.Text = "Fecha :";
            //
            // helpcencos
            //
            this.helpcencos.BackColor = System.Drawing.SystemColors.Menu;
            this.helpcencos.Image = ((System.Drawing.Image)(resources.GetObject("helpcencos.Image")));
            this.helpcencos.Location = new System.Drawing.Point(520, 44);
            this.helpcencos.Name = "helpcencos";
            this.helpcencos.Size = new System.Drawing.Size(28, 28);
            this.helpcencos.TabIndex = 246;
            this.helpcencos.TabStop = false;
            this.helpcencos.UseVisualStyleBackColor = false;
            this.helpcencos.Click += new System.EventHandler(this.helpcencos_Click);
            //
            // HelpAgencias
            //
            this.HelpAgencias.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpAgencias.Image = ((System.Drawing.Image)(resources.GetObject("HelpAgencias.Image")));
            this.HelpAgencias.Location = new System.Drawing.Point(659, 44);
            this.HelpAgencias.Name = "HelpAgencias";
            this.HelpAgencias.Size = new System.Drawing.Size(28, 28);
            this.HelpAgencias.TabIndex = 245;
            this.HelpAgencias.TabStop = false;
            this.HelpAgencias.UseVisualStyleBackColor = false;
            this.HelpAgencias.Click += new System.EventHandler(this.HelpAgencias_Click);
            //
            // Label8
            //
            this.Label8.Location = new System.Drawing.Point(559, 49);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(45, 18);
            this.Label8.TabIndex = 241;
            this.Label8.Text = "Agen.";
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.Location = new System.Drawing.Point(256, 48);
            this.TxtCodigoter.MaxLength = 14;
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.Size = new System.Drawing.Size(102, 21);
            this.TxtCodigoter.TabIndex = 3;
            this.TxtCodigoter.LostFocus += new System.EventHandler(this.TxtCodigoter_LostFocus);
            //
            // help_cuentacon
            //
            this.help_cuentacon.BackColor = System.Drawing.SystemColors.Menu;
            this.help_cuentacon.Image = ((System.Drawing.Image)(resources.GetObject("help_cuentacon.Image")));
            this.help_cuentacon.Location = new System.Drawing.Point(199, 44);
            this.help_cuentacon.Name = "help_cuentacon";
            this.help_cuentacon.Size = new System.Drawing.Size(28, 28);
            this.help_cuentacon.TabIndex = 239;
            this.help_cuentacon.TabStop = false;
            this.help_cuentacon.UseVisualStyleBackColor = false;
            this.help_cuentacon.Click += new System.EventHandler(this.help_cuentacon_Click);
            //
            // txtComprobante
            //
            this.txtComprobante.Enabled = false;
            this.txtComprobante.Location = new System.Drawing.Point(85, 18);
            this.txtComprobante.MaxLength = 5;
            this.txtComprobante.Name = "txtComprobante";
            this.txtComprobante.Size = new System.Drawing.Size(46, 21);
            this.txtComprobante.TabIndex = 0;
            //
            // Label2
            //
            this.Label2.Location = new System.Drawing.Point(4, 19);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(88, 18);
            this.Label2.TabIndex = 0;
            this.Label2.Text = "Comprobante ";
            //
            // TxtCreditos
            //
            this.TxtCreditos.Location = new System.Drawing.Point(433, 18);
            this.TxtCreditos.MaxLength = 14;
            this.TxtCreditos.Name = "TxtCreditos";
            this.TxtCreditos.ReadOnly = true;
            this.TxtCreditos.Size = new System.Drawing.Size(102, 21);
            this.TxtCreditos.TabIndex = 10;
            this.TxtCreditos.TabStop = false;
            this.TxtCreditos.Text = "0";
            this.TxtCreditos.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // txtDebitos
            //
            this.txtDebitos.Location = new System.Drawing.Point(256, 18);
            this.txtDebitos.MaxLength = 14;
            this.txtDebitos.Name = "txtDebitos";
            this.txtDebitos.ReadOnly = true;
            this.txtDebitos.Size = new System.Drawing.Size(102, 21);
            this.txtDebitos.TabIndex = 9;
            this.txtDebitos.TabStop = false;
            this.txtDebitos.Text = "0";
            this.txtDebitos.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label15
            //
            this.Label15.Location = new System.Drawing.Point(381, 19);
            this.Label15.Name = "Label15";
            this.Label15.Size = new System.Drawing.Size(56, 18);
            this.Label15.TabIndex = 235;
            this.Label15.Text = "Creditos";
            //
            // Label14
            //
            this.Label14.Location = new System.Drawing.Point(208, 19);
            this.Label14.Name = "Label14";
            this.Label14.Size = new System.Drawing.Size(56, 18);
            this.Label14.TabIndex = 235;
            this.Label14.Text = "Debitos";
            //
            // TxtDiferencia
            //
            this.TxtDiferencia.Location = new System.Drawing.Point(576, 18);
            this.TxtDiferencia.MaxLength = 14;
            this.TxtDiferencia.Name = "TxtDiferencia";
            this.TxtDiferencia.ReadOnly = true;
            this.TxtDiferencia.Size = new System.Drawing.Size(111, 21);
            this.TxtDiferencia.TabIndex = 11;
            this.TxtDiferencia.TabStop = false;
            this.TxtDiferencia.Text = "0";
            this.TxtDiferencia.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label4
            //
            this.Label4.Location = new System.Drawing.Point(551, 19);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(38, 18);
            this.Label4.TabIndex = 235;
            this.Label4.Text = "Dif";
            //
            // Label1
            //
            this.Label1.Location = new System.Drawing.Point(4, 49);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(75, 18);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "Cuenta:";
            //
            // txtCuentaCierre
            //
            this.txtCuentaCierre.Location = new System.Drawing.Point(85, 48);
            this.txtCuentaCierre.MaxLength = 12;
            this.txtCuentaCierre.Name = "txtCuentaCierre";
            this.txtCuentaCierre.Size = new System.Drawing.Size(111, 21);
            this.txtCuentaCierre.TabIndex = 2;
            this.txtCuentaCierre.LostFocus += new System.EventHandler(this.txtCuentaCierre_LostFocus);
            //
            // txtDebito
            //
            this.txtDebito.Location = new System.Drawing.Point(256, 82);
            this.txtDebito.MaxLength = 14;
            this.txtDebito.Name = "txtDebito";
            this.txtDebito.Size = new System.Drawing.Size(110, 21);
            this.txtDebito.TabIndex = 11;
            this.txtDebito.Text = "0";
            this.txtDebito.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label3
            //
            this.Label3.Location = new System.Drawing.Point(209, 82);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(55, 20);
            this.Label3.TabIndex = 235;
            this.Label3.Text = "Debitos";
            //
            // Label5
            //
            this.Label5.Location = new System.Drawing.Point(389, 82);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(48, 20);
            this.Label5.TabIndex = 235;
            this.Label5.Text = "Credito";
            //
            // txtCredito
            //
            this.txtCredito.Location = new System.Drawing.Point(433, 82);
            this.txtCredito.MaxLength = 14;
            this.txtCredito.Name = "txtCredito";
            this.txtCredito.Size = new System.Drawing.Size(102, 21);
            this.txtCredito.TabIndex = 12;
            this.txtCredito.Text = "0";
            this.txtCredito.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label6
            //
            this.Label6.Location = new System.Drawing.Point(233, 49);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(31, 18);
            this.Label6.TabIndex = 235;
            this.Label6.Text = "Nit ";
            //
            // cmbGrabar
            //
            this.cmbGrabar.Location = new System.Drawing.Point(594, 79);
            this.cmbGrabar.Name = "cmbGrabar";
            this.cmbGrabar.Size = new System.Drawing.Size(93, 27);
            this.cmbGrabar.TabIndex = 13;
            this.cmbGrabar.Text = "Grabar";
            this.cmbGrabar.Click += new System.EventHandler(this.cmbGrabar_Click);
            //
            // TxtConseCpte
            //
            this.TxtConseCpte.Enabled = false;
            this.TxtConseCpte.Location = new System.Drawing.Point(141, 18);
            this.TxtConseCpte.MaxLength = 5;
            this.TxtConseCpte.Name = "TxtConseCpte";
            this.TxtConseCpte.Size = new System.Drawing.Size(55, 21);
            this.TxtConseCpte.TabIndex = 1;
            //
            // Label7
            //
            this.Label7.Location = new System.Drawing.Point(390, 49);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(47, 18);
            this.Label7.TabIndex = 235;
            this.Label7.Text = "Cencos";
            //
            // HelpTerceros
            //
            this.HelpTerceros.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpTerceros.Image = ((System.Drawing.Image)(resources.GetObject("HelpTerceros.Image")));
            this.HelpTerceros.Location = new System.Drawing.Point(360, 44);
            this.HelpTerceros.Name = "HelpTerceros";
            this.HelpTerceros.Size = new System.Drawing.Size(28, 28);
            this.HelpTerceros.TabIndex = 239;
            this.HelpTerceros.TabStop = false;
            this.HelpTerceros.UseVisualStyleBackColor = false;
            this.HelpTerceros.Click += new System.EventHandler(this.HelpTerceros_Click);
            //
            // LblnombreCajero
            //
            this.LblnombreCajero.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblnombreCajero.Location = new System.Drawing.Point(271, 111);
            this.LblnombreCajero.Name = "LblnombreCajero";
            this.LblnombreCajero.Size = new System.Drawing.Size(121, 28);
            this.LblnombreCajero.TabIndex = 2;
            //
            // cnt_cuadredoc
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7f, 15f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(710, 126);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.LblnombreCajero);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "cnt_cuadredoc";
            this.Text = "Cuadre de Documentos";
            this.Load += new System.EventHandler(this.cop_cuadredoc_Load);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.ResumeLayout(false);
        }

        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.TextBox txtagencia;
        internal System.Windows.Forms.TextBox Txtcencos;
        internal System.Windows.Forms.DateTimePicker DtpFecha;
        internal System.Windows.Forms.Label Label17;
        internal System.Windows.Forms.Button helpcencos;
        internal System.Windows.Forms.Button HelpAgencias;
        internal System.Windows.Forms.Label Label8;
        internal System.Windows.Forms.TextBox TxtCodigoter;
        internal System.Windows.Forms.Button help_cuentacon;
        internal System.Windows.Forms.TextBox txtComprobante;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.TextBox TxtCreditos;
        internal System.Windows.Forms.TextBox txtDebitos;
        internal System.Windows.Forms.Label Label15;
        internal System.Windows.Forms.Label Label14;
        internal System.Windows.Forms.TextBox TxtDiferencia;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.Label Label5;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.Button cmbGrabar;
        internal System.Windows.Forms.TextBox TxtConseCpte;
        internal System.Windows.Forms.Label Label7;
        internal System.Windows.Forms.Button HelpTerceros;
        internal System.Windows.Forms.Label LblnombreCajero;
        internal System.Windows.Forms.TextBox txtCuentaCierre;
        internal System.Windows.Forms.TextBox txtDebito;
        internal System.Windows.Forms.TextBox txtCredito;
    }
}
