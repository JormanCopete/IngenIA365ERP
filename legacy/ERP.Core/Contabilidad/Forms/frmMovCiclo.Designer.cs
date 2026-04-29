namespace ERP.Core.Contabilidad.Forms
{
    partial class frmMovCiclo
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough]
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMovCiclo));
            this.DgwGrilla = new System.Windows.Forms.DataGridView();
            this.clmPeriodo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmSalAnt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmDebito = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmCredito = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmNuevoSal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.empresappl = new System.Windows.Forms.Label();
            this.Lblperiodo = new System.Windows.Forms.Label();
            this.Label18 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.LblCuenta = new System.Windows.Forms.Label();
            this.LblNombre = new System.Windows.Forms.Label();
            this.BtnImprimir = new System.Windows.Forms.Button();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.LblTotDeb = new System.Windows.Forms.Label();
            this.LblTotCred = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.cadena = new System.Windows.Forms.Label();
            this.LblTelefono = new System.Windows.Forms.Label();
            this.LblDireccion = new System.Windows.Forms.Label();
            this.LblNit = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrilla)).BeginInit();
            this.SuspendLayout();
            //
            // DgwGrilla
            //
            this.DgwGrilla.AllowUserToAddRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwGrilla.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgwGrilla.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwGrilla.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmPeriodo, this.clmSalAnt, this.clmDebito, this.clmCredito, this.clmNuevoSal });
            this.DgwGrilla.Location = new System.Drawing.Point(62, 97);
            this.DgwGrilla.MultiSelect = false;
            this.DgwGrilla.Name = "DgwGrilla";
            this.DgwGrilla.ReadOnly = true;
            this.DgwGrilla.RowHeadersVisible = false;
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle7.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle7.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.DgwGrilla.RowsDefaultCellStyle = DataGridViewCellStyle7;
            this.DgwGrilla.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwGrilla.Size = new System.Drawing.Size(502, 134);
            this.DgwGrilla.TabIndex = 0;
            //
            // clmPeriodo
            //
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle2.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle2.NullValue = "0";
            this.clmPeriodo.DefaultCellStyle = DataGridViewCellStyle2;
            this.clmPeriodo.HeaderText = "Periodo";
            this.clmPeriodo.Name = "clmPeriodo";
            this.clmPeriodo.ReadOnly = true;
            this.clmPeriodo.Width = 80;
            //
            // clmSalAnt
            //
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle3.Format = "N2";
            DataGridViewCellStyle3.NullValue = "0";
            this.clmSalAnt.DefaultCellStyle = DataGridViewCellStyle3;
            this.clmSalAnt.HeaderText = "Saldo Anterior";
            this.clmSalAnt.Name = "clmSalAnt";
            this.clmSalAnt.ReadOnly = true;
            //
            // clmDebito
            //
            DataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle4.Format = "N2";
            DataGridViewCellStyle4.NullValue = "0";
            this.clmDebito.DefaultCellStyle = DataGridViewCellStyle4;
            this.clmDebito.HeaderText = "Mov. Debito";
            this.clmDebito.Name = "clmDebito";
            this.clmDebito.ReadOnly = true;
            //
            // clmCredito
            //
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle5.Format = "N2";
            DataGridViewCellStyle5.NullValue = "0";
            this.clmCredito.DefaultCellStyle = DataGridViewCellStyle5;
            this.clmCredito.HeaderText = "Mov. Credito";
            this.clmCredito.Name = "clmCredito";
            this.clmCredito.ReadOnly = true;
            //
            // clmNuevoSal
            //
            DataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle6.Format = "N2";
            DataGridViewCellStyle6.NullValue = "0";
            this.clmNuevoSal.DefaultCellStyle = DataGridViewCellStyle6;
            this.clmNuevoSal.HeaderText = "Nuevo Saldo";
            this.clmNuevoSal.Name = "clmNuevoSal";
            this.clmNuevoSal.ReadOnly = true;
            //
            // empresappl
            //
            this.empresappl.BackColor = System.Drawing.SystemColors.Control;
            this.empresappl.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.empresappl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.empresappl.Location = new System.Drawing.Point(12, 9);
            this.empresappl.Name = "empresappl";
            this.empresappl.Size = new System.Drawing.Size(451, 36);
            this.empresappl.TabIndex = 335;
            this.empresappl.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // Lblperiodo
            //
            this.Lblperiodo.BackColor = System.Drawing.SystemColors.Control;
            this.Lblperiodo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10f, System.Drawing.FontStyle.Bold);
            this.Lblperiodo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Lblperiodo.Location = new System.Drawing.Point(544, 15);
            this.Lblperiodo.Name = "Lblperiodo";
            this.Lblperiodo.Size = new System.Drawing.Size(72, 16);
            this.Lblperiodo.TabIndex = 334;
            //
            // Label18
            //
            this.Label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 10f, System.Drawing.FontStyle.Bold);
            this.Label18.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label18.Location = new System.Drawing.Point(479, 15);
            this.Label18.Name = "Label18";
            this.Label18.Size = new System.Drawing.Size(64, 24);
            this.Label18.TabIndex = 333;
            this.Label18.Text = "Periodo";
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(15, 63);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(110, 16);
            this.Label1.TabIndex = 336;
            this.Label1.Text = "Cuenta Contable:";
            //
            // LblCuenta
            //
            this.LblCuenta.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblCuenta.Location = new System.Drawing.Point(128, 63);
            this.LblCuenta.Name = "LblCuenta";
            this.LblCuenta.Size = new System.Drawing.Size(113, 23);
            this.LblCuenta.TabIndex = 337;
            //
            // LblNombre
            //
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNombre.Location = new System.Drawing.Point(261, 63);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(331, 23);
            this.LblNombre.TabIndex = 338;
            //
            // BtnImprimir
            //
            this.BtnImprimir.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("BtnImprimir.BackgroundImage")));
            this.BtnImprimir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnImprimir.Location = new System.Drawing.Point(478, 239);
            this.BtnImprimir.Name = "BtnImprimir";
            this.BtnImprimir.Size = new System.Drawing.Size(38, 35);
            this.BtnImprimir.TabIndex = 339;
            this.ToolTip1.SetToolTip(this.BtnImprimir, "Imprimir");
            this.BtnImprimir.UseVisualStyleBackColor = true;
            this.BtnImprimir.Click += new System.EventHandler(this.BtnImprimir_Click);
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("BtnSalir.BackgroundImage")));
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(525, 239);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 340;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir");
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // LblTotDeb
            //
            this.LblTotDeb.BackColor = System.Drawing.Color.Gainsboro;
            this.LblTotDeb.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblTotDeb.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.LblTotDeb.ForeColor = System.Drawing.Color.Chocolate;
            this.LblTotDeb.Location = new System.Drawing.Point(184, 235);
            this.LblTotDeb.Name = "LblTotDeb";
            this.LblTotDeb.Size = new System.Drawing.Size(109, 18);
            this.LblTotDeb.TabIndex = 341;
            this.LblTotDeb.Text = "0";
            this.LblTotDeb.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // LblTotCred
            //
            this.LblTotCred.BackColor = System.Drawing.Color.Gainsboro;
            this.LblTotCred.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblTotCred.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.LblTotCred.ForeColor = System.Drawing.Color.Chocolate;
            this.LblTotCred.Location = new System.Drawing.Point(296, 235);
            this.LblTotCred.Name = "LblTotCred";
            this.LblTotCred.Size = new System.Drawing.Size(109, 18);
            this.LblTotCred.TabIndex = 342;
            this.LblTotCred.Text = "0";
            this.LblTotCred.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label2.Location = new System.Drawing.Point(63, 237);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(81, 16);
            this.Label2.TabIndex = 343;
            this.Label2.Text = "TOTALES:";
            //
            // cadena
            //
            this.cadena.AutoSize = true;
            this.cadena.Location = new System.Drawing.Point(577, 148);
            this.cadena.Name = "cadena";
            this.cadena.Size = new System.Drawing.Size(39, 13);
            this.cadena.TabIndex = 344;
            this.cadena.Text = "Label3";
            this.cadena.Visible = false;
            //
            // LblTelefono
            //
            this.LblTelefono.AutoSize = true;
            this.LblTelefono.Location = new System.Drawing.Point(577, 135);
            this.LblTelefono.Name = "LblTelefono";
            this.LblTelefono.Size = new System.Drawing.Size(45, 13);
            this.LblTelefono.TabIndex = 345;
            this.LblTelefono.Text = "telefono";
            this.LblTelefono.Visible = false;
            //
            // LblDireccion
            //
            this.LblDireccion.AutoSize = true;
            this.LblDireccion.Location = new System.Drawing.Point(577, 122);
            this.LblDireccion.Name = "LblDireccion";
            this.LblDireccion.Size = new System.Drawing.Size(50, 13);
            this.LblDireccion.TabIndex = 346;
            this.LblDireccion.Text = "direccion";
            this.LblDireccion.Visible = false;
            //
            // LblNit
            //
            this.LblNit.AutoSize = true;
            this.LblNit.Location = new System.Drawing.Point(577, 109);
            this.LblNit.Name = "LblNit";
            this.LblNit.Size = new System.Drawing.Size(18, 13);
            this.LblNit.TabIndex = 347;
            this.LblNit.Text = "nit";
            this.LblNit.Visible = false;
            //
            // frmMovCiclo
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(626, 286);
            this.Controls.Add(this.LblNit);
            this.Controls.Add(this.LblDireccion);
            this.Controls.Add(this.LblTelefono);
            this.Controls.Add(this.cadena);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.LblTotCred);
            this.Controls.Add(this.LblTotDeb);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.BtnImprimir);
            this.Controls.Add(this.LblNombre);
            this.Controls.Add(this.LblCuenta);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.empresappl);
            this.Controls.Add(this.Lblperiodo);
            this.Controls.Add(this.Label18);
            this.Controls.Add(this.DgwGrilla);
            this.Name = "frmMovCiclo";
            this.Text = "Movimientos Por Periodo";
            this.Load += new System.EventHandler(this.frmMovCiclo_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DgwGrilla)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        internal System.Windows.Forms.DataGridView DgwGrilla;
        internal System.Windows.Forms.Label Label18;
        internal System.Windows.Forms.Button BtnImprimir;
        internal System.Windows.Forms.ToolTip ToolTip1;
        internal System.Windows.Forms.Button BtnSalir;
        internal System.Windows.Forms.Label LblTotDeb;
        internal System.Windows.Forms.Label LblTotCred;
        internal System.Windows.Forms.Label Label2;
        public System.Windows.Forms.Label empresappl;
        public System.Windows.Forms.Label Lblperiodo;
        public System.Windows.Forms.Label Label1;
        public System.Windows.Forms.Label LblCuenta;
        public System.Windows.Forms.Label LblNombre;
        public System.Windows.Forms.Label cadena;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmPeriodo;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmSalAnt;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmDebito;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmCredito;
        internal System.Windows.Forms.DataGridViewTextBoxColumn clmNuevoSal;
        public System.Windows.Forms.Label LblTelefono;
        public System.Windows.Forms.Label LblDireccion;
        public System.Windows.Forms.Label LblNit;
    }
}
