namespace ERP.Core.Inventario.Forms
{
    partial class Inv_frmforpag
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Inv_frmforpag));
            this.CmbEfectivo = new System.Windows.Forms.Button();
            this.CmbCheque = new System.Windows.Forms.Button();
            this.Cmbtarjeta = new System.Windows.Forms.Button();
            this.CmbDifCuotas = new System.Windows.Forms.Button();
            this.GrpBotones = new System.Windows.Forms.GroupBox();
            this.CmbAceptar = new System.Windows.Forms.Button();
            this.GrbTotfact = new System.Windows.Forms.GroupBox();
            this.txtNeto = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.txtIva = new System.Windows.Forms.TextBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.txtDsto = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.TxtSubtotal = new System.Windows.Forms.TextBox();
            this.lblTotal = new System.Windows.Forms.Label();
            this.txtEfectivo = new System.Windows.Forms.TextBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.GrbEfectivo = new System.Windows.Forms.GroupBox();
            this.GrpCheque = new System.Windows.Forms.GroupBox();
            this.Label10 = new System.Windows.Forms.Label();
            this.Label8 = new System.Windows.Forms.Label();
            this.txtValbanco = new System.Windows.Forms.TextBox();
            this.txtcuenta = new System.Windows.Forms.TextBox();
            this.HelpBanco = new System.Windows.Forms.Button();
            this.txtbanco = new System.Windows.Forms.TextBox();
            this.Label9 = new System.Windows.Forms.Label();
            this.txtcant = new System.Windows.Forms.TextBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.Grptarjeta = new System.Windows.Forms.GroupBox();
            this.RbTarjCredito = new System.Windows.Forms.RadioButton();
            this.RbTarjDebito = new System.Windows.Forms.RadioButton();
            this.Label11 = new System.Windows.Forms.Label();
            this.Label12 = new System.Windows.Forms.Label();
            this.txtValorTarjeta = new System.Windows.Forms.TextBox();
            this.txtNoTarjeta = new System.Windows.Forms.TextBox();
            this.Label13 = new System.Windows.Forms.Label();
            this.txtTotal = new System.Windows.Forms.TextBox();
            this.GrpCuotas = new System.Windows.Forms.GroupBox();
            this.txttasa1 = new ERP.Core.Compartido.Controles.TexboxDecimal();
            this.Label16 = new System.Windows.Forms.Label();
            this.btnExtras = new System.Windows.Forms.Button();
            this.dtpFechapri = new System.Windows.Forms.DateTimePicker();
            this.lblPridesc = new System.Windows.Forms.Label();
            this.HelpLinea = new System.Windows.Forms.Button();
            this.CbxPeriodicidad = new System.Windows.Forms.ComboBox();
            this.Label5 = new System.Windows.Forms.Label();
            this.Label14 = new System.Windows.Forms.Label();
            this.Label7 = new System.Windows.Forms.Label();
            this.lblPlazo = new System.Windows.Forms.Label();
            this.txtCuota = new System.Windows.Forms.TextBox();
            this.TxtLinea = new System.Windows.Forms.TextBox();
            this.TxtPlazo = new System.Windows.Forms.TextBox();
            this.Label15 = new System.Windows.Forms.Label();
            this.LblCambio = new System.Windows.Forms.Label();
            this.GrpBotones.SuspendLayout();
            this.GrbTotfact.SuspendLayout();
            this.GrbEfectivo.SuspendLayout();
            this.GrpCheque.SuspendLayout();
            this.Grptarjeta.SuspendLayout();
            this.GrpCuotas.SuspendLayout();
            this.SuspendLayout();
            //
            // CmbEfectivo
            //
            this.CmbEfectivo.Location = new System.Drawing.Point(8, 30);
            this.CmbEfectivo.Name = "CmbEfectivo";
            this.CmbEfectivo.Size = new System.Drawing.Size(75, 43);
            this.CmbEfectivo.TabIndex = 0;
            this.CmbEfectivo.Text = "&Efectivo";
            this.CmbEfectivo.UseVisualStyleBackColor = true;
            this.CmbEfectivo.Click += new System.EventHandler(this.CmbEfectivo_Click);
            //
            // CmbCheque
            //
            this.CmbCheque.Enabled = false;
            this.CmbCheque.Location = new System.Drawing.Point(8, 81);
            this.CmbCheque.Name = "CmbCheque";
            this.CmbCheque.Size = new System.Drawing.Size(75, 43);
            this.CmbCheque.TabIndex = 1;
            this.CmbCheque.Text = "&Cheque";
            this.CmbCheque.UseVisualStyleBackColor = true;
            this.CmbCheque.Click += new System.EventHandler(this.CmbCheque_Click);
            //
            // Cmbtarjeta
            //
            this.Cmbtarjeta.Enabled = false;
            this.Cmbtarjeta.Location = new System.Drawing.Point(8, 132);
            this.Cmbtarjeta.Name = "Cmbtarjeta";
            this.Cmbtarjeta.Size = new System.Drawing.Size(75, 43);
            this.Cmbtarjeta.TabIndex = 2;
            this.Cmbtarjeta.Text = "&Tarjeta";
            this.Cmbtarjeta.UseVisualStyleBackColor = true;
            this.Cmbtarjeta.Click += new System.EventHandler(this.Cmbtarjeta_Click);
            //
            // CmbDifCuotas
            //
            this.CmbDifCuotas.Location = new System.Drawing.Point(8, 183);
            this.CmbDifCuotas.Name = "CmbDifCuotas";
            this.CmbDifCuotas.Size = new System.Drawing.Size(75, 43);
            this.CmbDifCuotas.TabIndex = 3;
            this.CmbDifCuotas.Text = "&Difiere en Cuotas";
            this.CmbDifCuotas.UseVisualStyleBackColor = true;
            this.CmbDifCuotas.Click += new System.EventHandler(this.CmbDifCuotas_Click);
            //
            // GrpBotones
            //
            this.GrpBotones.Controls.Add(this.CmbEfectivo);
            this.GrpBotones.Controls.Add(this.CmbDifCuotas);
            this.GrpBotones.Controls.Add(this.CmbCheque);
            this.GrpBotones.Controls.Add(this.Cmbtarjeta);
            this.GrpBotones.Controls.Add(this.CmbAceptar);
            this.GrpBotones.Location = new System.Drawing.Point(269, 12);
            this.GrpBotones.Name = "GrpBotones";
            this.GrpBotones.Size = new System.Drawing.Size(89, 308);
            this.GrpBotones.TabIndex = 2;
            this.GrpBotones.TabStop = false;
            //
            // CmbAceptar
            //
            this.CmbAceptar.Location = new System.Drawing.Point(8, 233);
            this.CmbAceptar.Name = "CmbAceptar";
            this.CmbAceptar.Size = new System.Drawing.Size(75, 43);
            this.CmbAceptar.TabIndex = 4;
            this.CmbAceptar.Text = "&Aceptar";
            this.CmbAceptar.UseVisualStyleBackColor = true;
            this.CmbAceptar.Click += new System.EventHandler(this.CmbAceptar_Click);
            //
            // GrbTotfact
            //
            this.GrbTotfact.Controls.Add(this.txtNeto);
            this.GrbTotfact.Controls.Add(this.Label3);
            this.GrbTotfact.Controls.Add(this.txtIva);
            this.GrbTotfact.Controls.Add(this.Label2);
            this.GrbTotfact.Controls.Add(this.txtDsto);
            this.GrbTotfact.Controls.Add(this.Label1);
            this.GrbTotfact.Controls.Add(this.TxtSubtotal);
            this.GrbTotfact.Controls.Add(this.lblTotal);
            this.GrbTotfact.Location = new System.Drawing.Point(12, 194);
            this.GrbTotfact.Name = "GrbTotfact";
            this.GrbTotfact.Size = new System.Drawing.Size(251, 126);
            this.GrbTotfact.TabIndex = 3;
            this.GrbTotfact.TabStop = false;
            //
            // txtNeto
            //
            this.txtNeto.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtNeto.Location = new System.Drawing.Point(90, 90);
            this.txtNeto.Name = "txtNeto";
            this.txtNeto.ReadOnly = true;
            this.txtNeto.Size = new System.Drawing.Size(155, 22);
            this.txtNeto.TabIndex = 1;
            this.txtNeto.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label3.Location = new System.Drawing.Point(6, 96);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(37, 16);
            this.Label3.TabIndex = 0;
            this.Label3.Text = "Neto";
            //
            // txtIva
            //
            this.txtIva.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtIva.Location = new System.Drawing.Point(90, 62);
            this.txtIva.Name = "txtIva";
            this.txtIva.ReadOnly = true;
            this.txtIva.Size = new System.Drawing.Size(155, 22);
            this.txtIva.TabIndex = 1;
            this.txtIva.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label2.Location = new System.Drawing.Point(6, 63);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(26, 16);
            this.Label2.TabIndex = 0;
            this.Label2.Text = "Iva";
            //
            // txtDsto
            //
            this.txtDsto.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDsto.Location = new System.Drawing.Point(90, 38);
            this.txtDsto.Name = "txtDsto";
            this.txtDsto.ReadOnly = true;
            this.txtDsto.Size = new System.Drawing.Size(155, 22);
            this.txtDsto.TabIndex = 1;
            this.txtDsto.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(6, 42);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(36, 16);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "Dsto";
            //
            // TxtSubtotal
            //
            this.TxtSubtotal.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtSubtotal.Location = new System.Drawing.Point(90, 14);
            this.TxtSubtotal.Name = "TxtSubtotal";
            this.TxtSubtotal.ReadOnly = true;
            this.TxtSubtotal.Size = new System.Drawing.Size(155, 22);
            this.TxtSubtotal.TabIndex = 1;
            this.TxtSubtotal.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblTotal
            //
            this.lblTotal.AutoSize = true;
            this.lblTotal.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTotal.Location = new System.Drawing.Point(6, 17);
            this.lblTotal.Name = "lblTotal";
            this.lblTotal.Size = new System.Drawing.Size(63, 16);
            this.lblTotal.TabIndex = 0;
            this.lblTotal.Text = "SubTotal";
            //
            // txtEfectivo
            //
            this.txtEfectivo.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtEfectivo.Location = new System.Drawing.Point(90, 19);
            this.txtEfectivo.Name = "txtEfectivo";
            this.txtEfectivo.Size = new System.Drawing.Size(155, 22);
            this.txtEfectivo.TabIndex = 0;
            this.txtEfectivo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtEfectivo.LostFocus += new System.EventHandler(this.txtEfectivo_LostFocus);
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label4.Location = new System.Drawing.Point(13, 22);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(56, 16);
            this.Label4.TabIndex = 2;
            this.Label4.Text = "Efectivo";
            //
            // GrbEfectivo
            //
            this.GrbEfectivo.Controls.Add(this.txtEfectivo);
            this.GrbEfectivo.Controls.Add(this.Label4);
            this.GrbEfectivo.Location = new System.Drawing.Point(12, 12);
            this.GrbEfectivo.Name = "GrbEfectivo";
            this.GrbEfectivo.Size = new System.Drawing.Size(251, 117);
            this.GrbEfectivo.TabIndex = 0;
            this.GrbEfectivo.TabStop = false;
            //
            // GrpCheque
            //
            this.GrpCheque.Controls.Add(this.Label10);
            this.GrpCheque.Controls.Add(this.Label8);
            this.GrpCheque.Controls.Add(this.txtValbanco);
            this.GrpCheque.Controls.Add(this.txtcuenta);
            this.GrpCheque.Controls.Add(this.HelpBanco);
            this.GrpCheque.Controls.Add(this.txtbanco);
            this.GrpCheque.Controls.Add(this.Label9);
            this.GrpCheque.Controls.Add(this.txtcant);
            this.GrpCheque.Controls.Add(this.Label6);
            this.GrpCheque.Location = new System.Drawing.Point(364, 20);
            this.GrpCheque.Name = "GrpCheque";
            this.GrpCheque.Size = new System.Drawing.Size(251, 98);
            this.GrpCheque.TabIndex = 4;
            this.GrpCheque.TabStop = false;
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label10.Location = new System.Drawing.Point(6, 63);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(40, 16);
            this.Label10.TabIndex = 209;
            this.Label10.Text = "Valor";
            this.Label10.Click += new System.EventHandler(this.Label8_Click);
            //
            // Label8
            //
            this.Label8.AutoSize = true;
            this.Label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label8.Location = new System.Drawing.Point(6, 39);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(50, 16);
            this.Label8.TabIndex = 209;
            this.Label8.Text = "Cuenta";
            this.Label8.Click += new System.EventHandler(this.Label8_Click);
            //
            // txtValbanco
            //
            this.txtValbanco.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtValbanco.Location = new System.Drawing.Point(60, 60);
            this.txtValbanco.Name = "txtValbanco";
            this.txtValbanco.Size = new System.Drawing.Size(175, 22);
            this.txtValbanco.TabIndex = 3;
            this.txtValbanco.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtValbanco.TextChanged += new System.EventHandler(this.txtValbanco_TextChanged);
            //
            // txtcuenta
            //
            this.txtcuenta.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtcuenta.Location = new System.Drawing.Point(60, 36);
            this.txtcuenta.Name = "txtcuenta";
            this.txtcuenta.Size = new System.Drawing.Size(175, 22);
            this.txtcuenta.TabIndex = 2;
            this.txtcuenta.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // HelpBanco
            //
            this.HelpBanco.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpBanco.Image = ((System.Drawing.Image)(resources.GetObject("HelpBanco.Image")));
            this.HelpBanco.Location = new System.Drawing.Point(110, 9);
            this.HelpBanco.Margin = new System.Windows.Forms.Padding(4);
            this.HelpBanco.Name = "HelpBanco";
            this.HelpBanco.Size = new System.Drawing.Size(28, 28);
            this.HelpBanco.TabIndex = 207;
            this.HelpBanco.TabStop = false;
            this.HelpBanco.UseVisualStyleBackColor = false;
            this.HelpBanco.Click += new System.EventHandler(this.HelpBanco_Click);
            //
            // txtbanco
            //
            this.txtbanco.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtbanco.Location = new System.Drawing.Point(60, 12);
            this.txtbanco.Name = "txtbanco";
            this.txtbanco.Size = new System.Drawing.Size(43, 22);
            this.txtbanco.TabIndex = 0;
            this.txtbanco.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label9.Location = new System.Drawing.Point(6, 15);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(35, 16);
            this.Label9.TabIndex = 2;
            this.Label9.Text = "Ban.";
            //
            // txtcant
            //
            this.txtcant.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtcant.Location = new System.Drawing.Point(192, 12);
            this.txtcant.Name = "txtcant";
            this.txtcant.Size = new System.Drawing.Size(43, 22);
            this.txtcant.TabIndex = 1;
            this.txtcant.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label6.Location = new System.Drawing.Point(153, 15);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(35, 16);
            this.Label6.TabIndex = 2;
            this.Label6.Text = "Cant";
            //
            // Grptarjeta
            //
            this.Grptarjeta.Controls.Add(this.RbTarjCredito);
            this.Grptarjeta.Controls.Add(this.RbTarjDebito);
            this.Grptarjeta.Controls.Add(this.Label11);
            this.Grptarjeta.Controls.Add(this.Label12);
            this.Grptarjeta.Controls.Add(this.txtValorTarjeta);
            this.Grptarjeta.Controls.Add(this.txtNoTarjeta);
            this.Grptarjeta.Location = new System.Drawing.Point(364, 177);
            this.Grptarjeta.Name = "Grptarjeta";
            this.Grptarjeta.Size = new System.Drawing.Size(251, 99);
            this.Grptarjeta.TabIndex = 4;
            this.Grptarjeta.TabStop = false;
            //
            // RbTarjCredito
            //
            this.RbTarjCredito.AutoSize = true;
            this.RbTarjCredito.Location = new System.Drawing.Point(153, 13);
            this.RbTarjCredito.Name = "RbTarjCredito";
            this.RbTarjCredito.Size = new System.Drawing.Size(82, 17);
            this.RbTarjCredito.TabIndex = 1;
            this.RbTarjCredito.Text = "Tarj. Credito";
            this.RbTarjCredito.UseVisualStyleBackColor = true;
            //
            // RbTarjDebito
            //
            this.RbTarjDebito.AutoSize = true;
            this.RbTarjDebito.Checked = true;
            this.RbTarjDebito.Location = new System.Drawing.Point(60, 13);
            this.RbTarjDebito.Name = "RbTarjDebito";
            this.RbTarjDebito.Size = new System.Drawing.Size(80, 17);
            this.RbTarjDebito.TabIndex = 0;
            this.RbTarjDebito.TabStop = true;
            this.RbTarjDebito.Text = "Tarj. Debito";
            this.RbTarjDebito.UseVisualStyleBackColor = true;
            //
            // Label11
            //
            this.Label11.AutoSize = true;
            this.Label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label11.Location = new System.Drawing.Point(6, 63);
            this.Label11.Name = "Label11";
            this.Label11.Size = new System.Drawing.Size(40, 16);
            this.Label11.TabIndex = 209;
            this.Label11.Text = "Valor";
            this.Label11.Click += new System.EventHandler(this.Label8_Click);
            //
            // Label12
            //
            this.Label12.AutoSize = true;
            this.Label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label12.Location = new System.Drawing.Point(6, 39);
            this.Label12.Name = "Label12";
            this.Label12.Size = new System.Drawing.Size(29, 16);
            this.Label12.TabIndex = 209;
            this.Label12.Text = "No.";
            this.Label12.Click += new System.EventHandler(this.Label8_Click);
            //
            // txtValorTarjeta
            //
            this.txtValorTarjeta.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtValorTarjeta.Location = new System.Drawing.Point(60, 60);
            this.txtValorTarjeta.Name = "txtValorTarjeta";
            this.txtValorTarjeta.Size = new System.Drawing.Size(175, 22);
            this.txtValorTarjeta.TabIndex = 3;
            this.txtValorTarjeta.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtValorTarjeta.Validated += new System.EventHandler(this.txtValorTarjeta_Validated);
            //
            // txtNoTarjeta
            //
            this.txtNoTarjeta.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtNoTarjeta.Location = new System.Drawing.Point(60, 36);
            this.txtNoTarjeta.Name = "txtNoTarjeta";
            this.txtNoTarjeta.Size = new System.Drawing.Size(175, 22);
            this.txtNoTarjeta.TabIndex = 2;
            this.txtNoTarjeta.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label13
            //
            this.Label13.AutoSize = true;
            this.Label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label13.Location = new System.Drawing.Point(25, 141);
            this.Label13.Name = "Label13";
            this.Label13.Size = new System.Drawing.Size(39, 16);
            this.Label13.TabIndex = 3;
            this.Label13.Text = "Total";
            //
            // txtTotal
            //
            this.txtTotal.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtTotal.ForeColor = System.Drawing.Color.Maroon;
            this.txtTotal.Location = new System.Drawing.Point(98, 136);
            this.txtTotal.Name = "txtTotal";
            this.txtTotal.ReadOnly = true;
            this.txtTotal.Size = new System.Drawing.Size(159, 26);
            this.txtTotal.TabIndex = 1;
            this.txtTotal.Text = "0";
            this.txtTotal.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // GrpCuotas
            //
            this.GrpCuotas.Controls.Add(this.txttasa1);
            this.GrpCuotas.Controls.Add(this.Label16);
            this.GrpCuotas.Controls.Add(this.btnExtras);
            this.GrpCuotas.Controls.Add(this.dtpFechapri);
            this.GrpCuotas.Controls.Add(this.lblPridesc);
            this.GrpCuotas.Controls.Add(this.HelpLinea);
            this.GrpCuotas.Controls.Add(this.CbxPeriodicidad);
            this.GrpCuotas.Controls.Add(this.Label5);
            this.GrpCuotas.Controls.Add(this.Label14);
            this.GrpCuotas.Controls.Add(this.Label7);
            this.GrpCuotas.Controls.Add(this.lblPlazo);
            this.GrpCuotas.Controls.Add(this.txtCuota);
            this.GrpCuotas.Controls.Add(this.TxtLinea);
            this.GrpCuotas.Controls.Add(this.TxtPlazo);
            this.GrpCuotas.Location = new System.Drawing.Point(12, 12);
            this.GrpCuotas.Name = "GrpCuotas";
            this.GrpCuotas.Size = new System.Drawing.Size(251, 125);
            this.GrpCuotas.TabIndex = 0;
            this.GrpCuotas.TabStop = false;
            this.GrpCuotas.Visible = false;
            this.GrpCuotas.Enter += new System.EventHandler(this.GrpCuotas_Enter);
            //
            // txttasa1
            //
            this.txttasa1.Location = new System.Drawing.Point(202, 67);
            this.txttasa1.MaxLength = 20;
            this.txttasa1.Name = "txttasa1";
            this.txttasa1.Size = new System.Drawing.Size(43, 20);
            this.txttasa1.SoloDecimal = true;
            this.txttasa1.TabIndex = 216;
            //
            // Label16
            //
            this.Label16.AutoSize = true;
            this.Label16.Location = new System.Drawing.Point(144, 73);
            this.Label16.Name = "Label16";
            this.Label16.Size = new System.Drawing.Size(57, 13);
            this.Label16.TabIndex = 4;
            this.Label16.Text = "% Tasa Int";
            //
            // btnExtras
            //
            this.btnExtras.BackColor = System.Drawing.SystemColors.Menu;
            this.btnExtras.Image = ((System.Drawing.Image)(resources.GetObject("btnExtras.Image")));
            this.btnExtras.Location = new System.Drawing.Point(222, 94);
            this.btnExtras.Margin = new System.Windows.Forms.Padding(4);
            this.btnExtras.Name = "btnExtras";
            this.btnExtras.Size = new System.Drawing.Size(28, 28);
            this.btnExtras.TabIndex = 7;
            this.btnExtras.TabStop = false;
            this.btnExtras.UseVisualStyleBackColor = false;
            this.btnExtras.Click += new System.EventHandler(this.btnExtras_Click);
            //
            // dtpFechapri
            //
            this.dtpFechapri.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpFechapri.Location = new System.Drawing.Point(125, 99);
            this.dtpFechapri.Name = "dtpFechapri";
            this.dtpFechapri.Size = new System.Drawing.Size(90, 20);
            this.dtpFechapri.TabIndex = 6;
            this.dtpFechapri.ValueChanged += new System.EventHandler(this.dtpFechapri_ValueChanged);
            this.dtpFechapri.LostFocus += new System.EventHandler(this.dtpFechapri_LostFocus);
            //
            // lblPridesc
            //
            this.lblPridesc.AutoSize = true;
            this.lblPridesc.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPridesc.Location = new System.Drawing.Point(6, 101);
            this.lblPridesc.Name = "lblPridesc";
            this.lblPridesc.Size = new System.Drawing.Size(121, 16);
            this.lblPridesc.TabIndex = 5;
            this.lblPridesc.Text = "Fec Pri  Descuento";
            //
            // HelpLinea
            //
            this.HelpLinea.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpLinea.Image = ((System.Drawing.Image)(resources.GetObject("HelpLinea.Image")));
            this.HelpLinea.Location = new System.Drawing.Point(107, 10);
            this.HelpLinea.Margin = new System.Windows.Forms.Padding(4);
            this.HelpLinea.Name = "HelpLinea";
            this.HelpLinea.Size = new System.Drawing.Size(28, 28);
            this.HelpLinea.TabIndex = 211;
            this.HelpLinea.TabStop = false;
            this.HelpLinea.UseVisualStyleBackColor = false;
            this.HelpLinea.Click += new System.EventHandler(this.HelpLinea_Click);
            //
            // CbxPeriodicidad
            //
            this.CbxPeriodicidad.FormattingEnabled = true;
            this.CbxPeriodicidad.Items.AddRange(new object[] { "", "Mensual", "Quincenal", "Decadal", "Semanal" });
            this.CbxPeriodicidad.Location = new System.Drawing.Point(53, 39);
            this.CbxPeriodicidad.Name = "CbxPeriodicidad";
            this.CbxPeriodicidad.Size = new System.Drawing.Size(173, 21);
            this.CbxPeriodicidad.TabIndex = 2;
            this.CbxPeriodicidad.SelectedIndexChanged += new System.EventHandler(this.CbxPeriodicidad_SelectedIndexChanged);
            this.CbxPeriodicidad.LostFocus += new System.EventHandler(this.CbxPeriodicidad_LostFocus);
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label5.Location = new System.Drawing.Point(6, 70);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(43, 16);
            this.Label5.TabIndex = 209;
            this.Label5.Text = "Cuota";
            this.Label5.Click += new System.EventHandler(this.Label8_Click);
            //
            // Label14
            //
            this.Label14.AutoSize = true;
            this.Label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label14.Location = new System.Drawing.Point(6, 41);
            this.Label14.Name = "Label14";
            this.Label14.Size = new System.Drawing.Size(43, 16);
            this.Label14.TabIndex = 209;
            this.Label14.Text = "Perio.";
            this.Label14.Click += new System.EventHandler(this.Label8_Click);
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label7.Location = new System.Drawing.Point(6, 16);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(41, 16);
            this.Label7.TabIndex = 209;
            this.Label7.Text = "Linea";
            this.Label7.Click += new System.EventHandler(this.Label8_Click);
            //
            // lblPlazo
            //
            this.lblPlazo.AutoSize = true;
            this.lblPlazo.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPlazo.Location = new System.Drawing.Point(138, 16);
            this.lblPlazo.Name = "lblPlazo";
            this.lblPlazo.Size = new System.Drawing.Size(42, 16);
            this.lblPlazo.TabIndex = 209;
            this.lblPlazo.Text = "Plazo";
            this.lblPlazo.Click += new System.EventHandler(this.Label8_Click);
            //
            // txtCuota
            //
            this.txtCuota.BackColor = System.Drawing.Color.Silver;
            this.txtCuota.Enabled = false;
            this.txtCuota.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCuota.Location = new System.Drawing.Point(53, 64);
            this.txtCuota.Name = "txtCuota";
            this.txtCuota.Size = new System.Drawing.Size(85, 26);
            this.txtCuota.TabIndex = 3;
            this.txtCuota.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtCuota.Validated += new System.EventHandler(this.txtValorTarjeta_Validated);
            //
            // TxtLinea
            //
            this.TxtLinea.Enabled = false;
            this.TxtLinea.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtLinea.Location = new System.Drawing.Point(53, 13);
            this.TxtLinea.Name = "TxtLinea";
            this.TxtLinea.Size = new System.Drawing.Size(47, 22);
            this.TxtLinea.TabIndex = 0;
            this.TxtLinea.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // TxtPlazo
            //
            this.TxtPlazo.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtPlazo.Location = new System.Drawing.Point(192, 13);
            this.TxtPlazo.Name = "TxtPlazo";
            this.TxtPlazo.Size = new System.Drawing.Size(34, 22);
            this.TxtPlazo.TabIndex = 1;
            this.TxtPlazo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtPlazo.LostFocus += new System.EventHandler(this.TxtPlazo_LostFocus);
            //
            // Label15
            //
            this.Label15.AutoSize = true;
            this.Label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label15.Location = new System.Drawing.Point(25, 175);
            this.Label15.Name = "Label15";
            this.Label15.Size = new System.Drawing.Size(55, 16);
            this.Label15.TabIndex = 5;
            this.Label15.Text = "Cambio";
            //
            // LblCambio
            //
            this.LblCambio.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCambio.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblCambio.ForeColor = System.Drawing.Color.Maroon;
            this.LblCambio.Location = new System.Drawing.Point(98, 172);
            this.LblCambio.Name = "LblCambio";
            this.LblCambio.Size = new System.Drawing.Size(159, 23);
            this.LblCambio.TabIndex = 6;
            this.LblCambio.Text = "0";
            this.LblCambio.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // Inv_frmforpag
            //
            this.AcceptButton = this.CmbAceptar;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(362, 327);
            this.ControlBox = false;
            this.Controls.Add(this.LblCambio);
            this.Controls.Add(this.Label15);
            this.Controls.Add(this.Grptarjeta);
            this.Controls.Add(this.GrpCheque);
            this.Controls.Add(this.txtTotal);
            this.Controls.Add(this.Label13);
            this.Controls.Add(this.GrbTotfact);
            this.Controls.Add(this.GrpBotones);
            this.Controls.Add(this.GrbEfectivo);
            this.Controls.Add(this.GrpCuotas);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.Name = "Inv_frmforpag";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.Inv_frmforpag_KeyUp);
            this.Load += new System.EventHandler(this.Inv_frmforpag_Load);
            this.GrpBotones.ResumeLayout(false);
            this.GrbTotfact.ResumeLayout(false);
            this.GrbTotfact.PerformLayout();
            this.GrbEfectivo.ResumeLayout(false);
            this.GrbEfectivo.PerformLayout();
            this.GrpCheque.ResumeLayout(false);
            this.GrpCheque.PerformLayout();
            this.Grptarjeta.ResumeLayout(false);
            this.Grptarjeta.PerformLayout();
            this.GrpCuotas.ResumeLayout(false);
            this.GrpCuotas.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.Button CmbEfectivo;
        internal System.Windows.Forms.Button CmbCheque;
        internal System.Windows.Forms.Button Cmbtarjeta;
        internal System.Windows.Forms.Button CmbDifCuotas;
        internal System.Windows.Forms.GroupBox GrpBotones;
        internal System.Windows.Forms.GroupBox GrbTotfact;
        internal System.Windows.Forms.Label lblTotal;
        internal System.Windows.Forms.Button CmbAceptar;
        internal System.Windows.Forms.TextBox txtNeto;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.TextBox txtIva;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.TextBox txtDsto;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.TextBox TxtSubtotal;
        internal System.Windows.Forms.TextBox txtEfectivo;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.GroupBox GrbEfectivo;
        internal System.Windows.Forms.GroupBox GrpCheque;
        internal System.Windows.Forms.TextBox txtcant;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.Label Label8;
        internal System.Windows.Forms.TextBox txtcuenta;
        internal System.Windows.Forms.Button HelpBanco;
        internal System.Windows.Forms.TextBox txtbanco;
        internal System.Windows.Forms.Label Label9;
        internal System.Windows.Forms.Label Label10;
        internal System.Windows.Forms.TextBox txtValbanco;
        internal System.Windows.Forms.GroupBox Grptarjeta;
        internal System.Windows.Forms.RadioButton RbTarjCredito;
        internal System.Windows.Forms.RadioButton RbTarjDebito;
        internal System.Windows.Forms.Label Label11;
        internal System.Windows.Forms.Label Label12;
        internal System.Windows.Forms.TextBox txtValorTarjeta;
        internal System.Windows.Forms.TextBox txtNoTarjeta;
        internal System.Windows.Forms.Label Label13;
        internal System.Windows.Forms.TextBox txtTotal;
        internal System.Windows.Forms.GroupBox GrpCuotas;
        internal System.Windows.Forms.Button HelpLinea;
        internal System.Windows.Forms.ComboBox CbxPeriodicidad;
        internal System.Windows.Forms.Label Label5;
        internal System.Windows.Forms.Label Label14;
        internal System.Windows.Forms.Label Label7;
        internal System.Windows.Forms.Label lblPlazo;
        internal System.Windows.Forms.TextBox txtCuota;
        internal System.Windows.Forms.TextBox TxtLinea;
        internal System.Windows.Forms.TextBox TxtPlazo;
        internal System.Windows.Forms.Label Label15;
        internal System.Windows.Forms.Label LblCambio;
        internal System.Windows.Forms.DateTimePicker dtpFechapri;
        internal System.Windows.Forms.Label lblPridesc;
        internal System.Windows.Forms.Button btnExtras;
        internal ERP.Core.Compartido.Controles.TexboxDecimal txttasa1;
        internal System.Windows.Forms.Label Label16;
    }
}
