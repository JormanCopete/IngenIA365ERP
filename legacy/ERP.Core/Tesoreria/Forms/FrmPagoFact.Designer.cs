namespace ERP.Core.Tesoreria.Forms
{
    // Traducción de: FrmPagoFact.Designer.vb
    partial class FrmPagoFact
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmPagoFact));
            this.CmdSalir = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.TxtCpte = new System.Windows.Forms.TextBox();
            this.HelpCpte = new System.Windows.Forms.Button();
            this.TxtConseCpte = new System.Windows.Forms.TextBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.TxtValGirar = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.txtNumCheque = new System.Windows.Forms.TextBox();
            this.LblNombre = new System.Windows.Forms.Label();
            this.Label4 = new System.Windows.Forms.Label();
            this.DtpFecha = new System.Windows.Forms.DateTimePicker();
            this.CmbAceptar = new System.Windows.Forms.Button();
            this.Label5 = new System.Windows.Forms.Label();
            this.Label6 = new System.Windows.Forms.Label();
            this.LblNumFacturas = new System.Windows.Forms.Label();
            this.Label7 = new System.Windows.Forms.Label();
            this.Txtbanco = new System.Windows.Forms.TextBox();
            this.helpBanco = new System.Windows.Forms.Button();
            this.Label8 = new System.Windows.Forms.Label();
            this.txtSaldoBanco = new System.Windows.Forms.TextBox();
            this.lblNomBanco = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.TxtDetalle = new System.Windows.Forms.TextBox();
            this.Label9 = new System.Windows.Forms.Label();
            this.LblCedula = new System.Windows.Forms.TextBox();
            this.HelpNit = new System.Windows.Forms.Button();
            this.LblForPago = new System.Windows.Forms.Label();
            this.Label10 = new System.Windows.Forms.Label();
            this.GroupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // CmdSalir
            //
            this.CmdSalir.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CmdSalir.Location = new System.Drawing.Point(344, 178);
            this.CmdSalir.Name = "CmdSalir";
            this.CmdSalir.Size = new System.Drawing.Size(75, 35);
            this.CmdSalir.TabIndex = 4;
            this.CmdSalir.Text = "Salir";
            this.CmdSalir.UseVisualStyleBackColor = true;
            // this.CmdSalir.Click += new System.EventHandler(this.CmdSalir_Click); // ERROR: CS1061
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(9, 22);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(70, 13);
            this.Label1.TabIndex = 1;
            this.Label1.Text = "Comprobante";
            //
            // TxtCpte
            //
            this.TxtCpte.Location = new System.Drawing.Point(85, 18);
            this.TxtCpte.Name = "TxtCpte";
            this.TxtCpte.Size = new System.Drawing.Size(46, 20);
            this.TxtCpte.TabIndex = 0;
            // this.TxtCpte.LostFocus += new System.EventHandler(this.TxtCpte_LostFocus); // ERROR: CS1061
            //
            // HelpCpte
            //
            this.HelpCpte.Image = ((System.Drawing.Image)(resources.GetObject("HelpCpte.Image")));
            this.HelpCpte.Location = new System.Drawing.Point(138, 16);
            this.HelpCpte.Margin = new System.Windows.Forms.Padding(4);
            this.HelpCpte.Name = "HelpCpte";
            this.HelpCpte.Size = new System.Drawing.Size(32, 25);
            this.HelpCpte.TabIndex = 144;
            this.HelpCpte.TabStop = false;
            // this.HelpCpte.Click += new System.EventHandler(this.HelpCpte_Click); // ERROR: CS1061
            //
            // TxtConseCpte
            //
            this.TxtConseCpte.Location = new System.Drawing.Point(177, 18);
            this.TxtConseCpte.Name = "TxtConseCpte";
            this.TxtConseCpte.Size = new System.Drawing.Size(83, 20);
            this.TxtConseCpte.TabIndex = 1;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(9, 47);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(56, 13);
            this.Label2.TabIndex = 1;
            this.Label2.Text = "Valor Girar";
            //
            // TxtValGirar
            //
            this.TxtValGirar.BackColor = System.Drawing.Color.Silver;
            this.TxtValGirar.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtValGirar.Location = new System.Drawing.Point(85, 41);
            this.TxtValGirar.Name = "TxtValGirar";
            this.TxtValGirar.Size = new System.Drawing.Size(175, 24);
            this.TxtValGirar.TabIndex = 2;
            this.TxtValGirar.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // this.TxtValGirar.LostFocus += new System.EventHandler(this.TxtValGirar_LostFocus); // ERROR: CS1061
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(293, 47);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(72, 13);
            this.Label3.TabIndex = 1;
            this.Label3.Text = "Num. Cheque";
            //
            // txtNumCheque
            //
            this.txtNumCheque.Enabled = false;
            this.txtNumCheque.Location = new System.Drawing.Point(391, 43);
            this.txtNumCheque.MaxLength = 9;
            this.txtNumCheque.Name = "txtNumCheque";
            this.txtNumCheque.Size = new System.Drawing.Size(72, 20);
            this.txtNumCheque.TabIndex = 4;
            this.txtNumCheque.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // LblNombre
            //
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNombre.Location = new System.Drawing.Point(12, 7);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(460, 23);
            this.LblNombre.TabIndex = 145;
            this.LblNombre.Text = "#";
            this.LblNombre.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(293, 22);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(37, 13);
            this.Label4.TabIndex = 146;
            this.Label4.Text = "Fecha";
            //
            // DtpFecha
            //
            this.DtpFecha.Enabled = false;
            this.DtpFecha.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFecha.Location = new System.Drawing.Point(364, 18);
            this.DtpFecha.Name = "DtpFecha";
            this.DtpFecha.Size = new System.Drawing.Size(99, 20);
            this.DtpFecha.TabIndex = 147;
            //
            // CmbAceptar
            //
            this.CmbAceptar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbAceptar.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CmbAceptar.Location = new System.Drawing.Point(255, 178);
            this.CmbAceptar.Name = "CmbAceptar";
            this.CmbAceptar.Size = new System.Drawing.Size(75, 35);
            this.CmbAceptar.TabIndex = 3;
            this.CmbAceptar.Text = "Aceptar";
            this.CmbAceptar.UseVisualStyleBackColor = true;
            // this.CmbAceptar.Click += new System.EventHandler(this.CmbAceptar_Click); // ERROR: CS1061
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(-1, 37);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(40, 13);
            this.Label5.TabIndex = 1;
            this.Label5.Text = "Cedula";
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(361, 37);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(56, 13);
            this.Label6.TabIndex = 1;
            this.Label6.Text = "Cant  fact.";
            //
            // LblNumFacturas
            //
            this.LblNumFacturas.BackColor = System.Drawing.Color.Silver;
            this.LblNumFacturas.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNumFacturas.Location = new System.Drawing.Point(423, 33);
            this.LblNumFacturas.Name = "LblNumFacturas";
            this.LblNumFacturas.Size = new System.Drawing.Size(43, 20);
            this.LblNumFacturas.TabIndex = 145;
            this.LblNumFacturas.Text = "#";
            this.LblNumFacturas.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Location = new System.Drawing.Point(10, 72);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(38, 13);
            this.Label7.TabIndex = 1;
            this.Label7.Text = "Banco";
            //
            // Txtbanco
            //
            this.Txtbanco.Enabled = false;
            this.Txtbanco.Location = new System.Drawing.Point(85, 68);
            this.Txtbanco.Name = "Txtbanco";
            this.Txtbanco.Size = new System.Drawing.Size(46, 20);
            this.Txtbanco.TabIndex = 3;
            // this.Txtbanco.LostFocus += new System.EventHandler(this.Txtbanco_LostFocus); // ERROR: CS1061
            //
            // helpBanco
            //
            this.helpBanco.Enabled = false;
            this.helpBanco.Image = ((System.Drawing.Image)(resources.GetObject("helpBanco.Image")));
            this.helpBanco.Location = new System.Drawing.Point(135, 66);
            this.helpBanco.Margin = new System.Windows.Forms.Padding(4);
            this.helpBanco.Name = "helpBanco";
            this.helpBanco.Size = new System.Drawing.Size(32, 25);
            this.helpBanco.TabIndex = 4;
            this.helpBanco.TabStop = false;
            // this.helpBanco.Click += new System.EventHandler(this.helpBanco_Click); // ERROR: CS1061
            //
            // Label8
            //
            this.Label8.AutoSize = true;
            this.Label8.Location = new System.Drawing.Point(293, 67);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(34, 13);
            this.Label8.TabIndex = 148;
            this.Label8.Text = "Saldo";
            //
            // txtSaldoBanco
            //
            this.txtSaldoBanco.BackColor = System.Drawing.Color.Silver;
            this.txtSaldoBanco.Enabled = false;
            this.txtSaldoBanco.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSaldoBanco.Location = new System.Drawing.Point(330, 66);
            this.txtSaldoBanco.Name = "txtSaldoBanco";
            this.txtSaldoBanco.Size = new System.Drawing.Size(133, 24);
            this.txtSaldoBanco.TabIndex = 2;
            this.txtSaldoBanco.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblNomBanco
            //
            this.lblNomBanco.Location = new System.Drawing.Point(167, 68);
            this.lblNomBanco.Name = "lblNomBanco";
            this.lblNomBanco.Size = new System.Drawing.Size(126, 20);
            this.lblNomBanco.TabIndex = 149;
            this.lblNomBanco.Text = "#";
            this.lblNomBanco.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.TxtDetalle);
            this.GroupBox1.Controls.Add(this.Label9);
            this.GroupBox1.Controls.Add(this.DtpFecha);
            this.GroupBox1.Controls.Add(this.lblNomBanco);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.Label8);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Controls.Add(this.Label7);
            this.GroupBox1.Controls.Add(this.Label4);
            this.GroupBox1.Controls.Add(this.TxtCpte);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.TxtValGirar);
            this.GroupBox1.Controls.Add(this.Txtbanco);
            this.GroupBox1.Controls.Add(this.helpBanco);
            this.GroupBox1.Controls.Add(this.txtNumCheque);
            this.GroupBox1.Controls.Add(this.HelpCpte);
            this.GroupBox1.Controls.Add(this.txtSaldoBanco);
            this.GroupBox1.Controls.Add(this.TxtConseCpte);
            this.GroupBox1.Location = new System.Drawing.Point(3, 56);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(469, 120);
            this.GroupBox1.TabIndex = 0;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Generales";
            //
            // TxtDetalle
            //
            this.TxtDetalle.Location = new System.Drawing.Point(85, 91);
            this.TxtDetalle.Name = "TxtDetalle";
            this.TxtDetalle.Size = new System.Drawing.Size(378, 20);
            this.TxtDetalle.TabIndex = 151;
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Location = new System.Drawing.Point(10, 95);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(40, 13);
            this.Label9.TabIndex = 150;
            this.Label9.Text = "Detalle";
            //
            // LblCedula (TextBox que actúa como label editable)
            //
            this.LblCedula.BackColor = System.Drawing.SystemColors.Window;
            this.LblCedula.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.LblCedula.Location = new System.Drawing.Point(44, 35);
            this.LblCedula.Multiline = true;
            this.LblCedula.Name = "LblCedula";
            this.LblCedula.Size = new System.Drawing.Size(100, 17);
            this.LblCedula.TabIndex = 146;
            // this.LblCedula.LostFocus += new System.EventHandler(this.LblCedula_LostFocus); // ERROR: CS1061
            //
            // HelpNit
            //
            this.HelpNit.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpNit.Image = ((System.Drawing.Image)(resources.GetObject("HelpNit.Image")));
            this.HelpNit.Location = new System.Drawing.Point(150, 30);
            this.HelpNit.Margin = new System.Windows.Forms.Padding(4);
            this.HelpNit.Name = "HelpNit";
            this.HelpNit.Size = new System.Drawing.Size(32, 27);
            this.HelpNit.TabIndex = 218;
            this.HelpNit.TabStop = false;
            this.HelpNit.UseVisualStyleBackColor = false;
            this.HelpNit.Visible = false;
            // this.HelpNit.Click += new System.EventHandler(this.HelpNit_Click); // ERROR: CS1061
            //
            // LblForPago
            //
            this.LblForPago.BackColor = System.Drawing.Color.Silver;
            this.LblForPago.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblForPago.Location = new System.Drawing.Point(280, 33);
            this.LblForPago.Name = "LblForPago";
            this.LblForPago.Size = new System.Drawing.Size(43, 20);
            this.LblForPago.TabIndex = 220;
            this.LblForPago.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Location = new System.Drawing.Point(213, 37);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(64, 13);
            this.Label10.TabIndex = 219;
            this.Label10.Text = "Forma Pago";
            //
            // FrmPagoFact
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(473, 216);
            this.ControlBox = false;
            this.Controls.Add(this.LblForPago);
            this.Controls.Add(this.Label10);
            this.Controls.Add(this.HelpNit);
            this.Controls.Add(this.LblCedula);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.LblNumFacturas);
            this.Controls.Add(this.LblNombre);
            this.Controls.Add(this.Label6);
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.CmbAceptar);
            this.Controls.Add(this.CmdSalir);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "FrmPagoFact";
            // this.Load += new System.EventHandler(this.FrmPagoFact_Load); // ERROR: CS1061
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        internal System.Windows.Forms.Button CmdSalir;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.TextBox TxtCpte;
        internal System.Windows.Forms.Button HelpCpte;
        internal System.Windows.Forms.TextBox TxtConseCpte;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.TextBox TxtValGirar;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.TextBox txtNumCheque;
        internal System.Windows.Forms.Label LblNombre;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.DateTimePicker DtpFecha;
        internal System.Windows.Forms.Button CmbAceptar;
        internal System.Windows.Forms.Label Label5;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.Label LblNumFacturas;
        internal System.Windows.Forms.Label Label7;
        internal System.Windows.Forms.TextBox Txtbanco;
        internal System.Windows.Forms.Button helpBanco;
        internal System.Windows.Forms.Label Label8;
        internal System.Windows.Forms.TextBox txtSaldoBanco;
        internal System.Windows.Forms.Label lblNomBanco;
        internal System.Windows.Forms.GroupBox GroupBox1;
        internal System.Windows.Forms.TextBox LblCedula;
        internal System.Windows.Forms.Button HelpNit;
        internal System.Windows.Forms.Label LblForPago;
        internal System.Windows.Forms.Label Label10;
        internal System.Windows.Forms.TextBox TxtDetalle;
        internal System.Windows.Forms.Label Label9;
    }
}
