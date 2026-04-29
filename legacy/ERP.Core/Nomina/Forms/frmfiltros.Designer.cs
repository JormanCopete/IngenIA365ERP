namespace ERP.Core.Nomina.Forms
{
    partial class frmfiltros
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
            this.Label1 = new System.Windows.Forms.Label();
            this.CbxEmpresa = new System.Windows.Forms.ComboBox();
            this.TxtEmpresa = new System.Windows.Forms.TextBox();
            this.TxtCencos = new System.Windows.Forms.TextBox();
            this.CbxCencos = new System.Windows.Forms.ComboBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.CbxPeriodicidad = new System.Windows.Forms.ComboBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.TxtCiudad = new System.Windows.Forms.TextBox();
            this.CbxCiudad = new System.Windows.Forms.ComboBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.CmbSalir = new System.Windows.Forms.Button();
            this.CmbAceptar = new System.Windows.Forms.Button();
            this.CmbPeriodicidad = new System.Windows.Forms.ComboBox();
            this.CbxEstado = new System.Windows.Forms.ComboBox();
            this.Label5 = new System.Windows.Forms.Label();
            this.CBxSeccion = new System.Windows.Forms.ComboBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.TxtSeccion = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(41, 42);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(48, 13);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "Empresa";
            //
            // CbxEmpresa
            //
            this.CbxEmpresa.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxEmpresa.FormattingEnabled = true;
            this.CbxEmpresa.Items.AddRange(new object[] { "", "=", ">=", "<=" });
            this.CbxEmpresa.Location = new System.Drawing.Point(97, 38);
            this.CbxEmpresa.Name = "CbxEmpresa";
            this.CbxEmpresa.Size = new System.Drawing.Size(51, 21);
            this.CbxEmpresa.TabIndex = 0;
            //
            // TxtEmpresa
            //
            this.TxtEmpresa.Location = new System.Drawing.Point(154, 38);
            this.TxtEmpresa.MaxLength = 14;
            this.TxtEmpresa.Name = "TxtEmpresa";
            this.TxtEmpresa.Size = new System.Drawing.Size(65, 20);
            this.TxtEmpresa.TabIndex = 1;
            //
            // TxtCencos
            //
            this.TxtCencos.Location = new System.Drawing.Point(154, 65);
            this.TxtCencos.MaxLength = 8;
            this.TxtCencos.Name = "TxtCencos";
            this.TxtCencos.Size = new System.Drawing.Size(65, 20);
            this.TxtCencos.TabIndex = 3;
            //
            // CbxCencos
            //
            this.CbxCencos.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxCencos.FormattingEnabled = true;
            this.CbxCencos.Items.AddRange(new object[] { "", "=", ">=", "<=" });
            this.CbxCencos.Location = new System.Drawing.Point(97, 65);
            this.CbxCencos.Name = "CbxCencos";
            this.CbxCencos.Size = new System.Drawing.Size(51, 21);
            this.CbxCencos.TabIndex = 2;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(46, 69);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(43, 13);
            this.Label2.TabIndex = 3;
            this.Label2.Text = "Cencos";
            //
            // CbxPeriodicidad
            //
            this.CbxPeriodicidad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxPeriodicidad.FormattingEnabled = true;
            this.CbxPeriodicidad.Items.AddRange(new object[] { "=", ">=", "<=", "" });
            this.CbxPeriodicidad.Location = new System.Drawing.Point(97, 118);
            this.CbxPeriodicidad.Name = "CbxPeriodicidad";
            this.CbxPeriodicidad.Size = new System.Drawing.Size(51, 21);
            this.CbxPeriodicidad.TabIndex = 6;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(24, 122);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(65, 13);
            this.Label3.TabIndex = 6;
            this.Label3.Text = "Periodicidad";
            //
            // TxtCiudad
            //
            this.TxtCiudad.Location = new System.Drawing.Point(154, 146);
            this.TxtCiudad.Name = "TxtCiudad";
            this.TxtCiudad.Size = new System.Drawing.Size(65, 20);
            this.TxtCiudad.TabIndex = 9;
            //
            // CbxCiudad
            //
            this.CbxCiudad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxCiudad.FormattingEnabled = true;
            this.CbxCiudad.Items.AddRange(new object[] { "=", ">=", "<=", "" });
            this.CbxCiudad.Location = new System.Drawing.Point(97, 146);
            this.CbxCiudad.Name = "CbxCiudad";
            this.CbxCiudad.Size = new System.Drawing.Size(51, 21);
            this.CbxCiudad.TabIndex = 8;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(49, 150);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(40, 13);
            this.Label4.TabIndex = 9;
            this.Label4.Text = "Ciudad";
            //
            // CmbSalir
            //
            this.CmbSalir.Location = new System.Drawing.Point(221, 216);
            this.CmbSalir.Margin = new System.Windows.Forms.Padding(4);
            this.CmbSalir.Name = "CmbSalir";
            this.CmbSalir.Size = new System.Drawing.Size(50, 40);
            this.CmbSalir.TabIndex = 12;
            this.CmbSalir.TabStop = false;
            this.CmbSalir.Text = "Salir";
            this.CmbSalir.Click += new System.EventHandler(this.CmbSalir_Click);
            //
            // CmbAceptar
            //
            this.CmbAceptar.Location = new System.Drawing.Point(167, 216);
            this.CmbAceptar.Margin = new System.Windows.Forms.Padding(4);
            this.CmbAceptar.Name = "CmbAceptar";
            this.CmbAceptar.Size = new System.Drawing.Size(52, 40);
            this.CmbAceptar.TabIndex = 11;
            this.CmbAceptar.Text = "Aceptar";
            this.CmbAceptar.Click += new System.EventHandler(this.CmbAceptar_Click);
            //
            // CmbPeriodicidad
            //
            this.CmbPeriodicidad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CmbPeriodicidad.FormattingEnabled = true;
            this.CmbPeriodicidad.Items.AddRange(new object[] { "", "Mensual", "Quincenal", "Decadal", "Semanal" });
            this.CmbPeriodicidad.Location = new System.Drawing.Point(154, 118);
            this.CmbPeriodicidad.Name = "CmbPeriodicidad";
            this.CmbPeriodicidad.Size = new System.Drawing.Size(118, 21);
            this.CmbPeriodicidad.TabIndex = 7;
            //
            // CbxEstado
            //
            this.CbxEstado.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxEstado.FormattingEnabled = true;
            this.CbxEstado.Items.AddRange(new object[] { "Todos", "Activos", "Retirados" });
            this.CbxEstado.Location = new System.Drawing.Point(97, 172);
            this.CbxEstado.Name = "CbxEstado";
            this.CbxEstado.Size = new System.Drawing.Size(174, 21);
            this.CbxEstado.TabIndex = 10;
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(49, 176);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(40, 13);
            this.Label5.TabIndex = 13;
            this.Label5.Text = "Estado";
            //
            // CBxSeccion
            //
            this.CBxSeccion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CBxSeccion.FormattingEnabled = true;
            this.CBxSeccion.Items.AddRange(new object[] { "", "=", ">=", "<=" });
            this.CBxSeccion.Location = new System.Drawing.Point(97, 91);
            this.CBxSeccion.Name = "CBxSeccion";
            this.CBxSeccion.Size = new System.Drawing.Size(51, 21);
            this.CBxSeccion.TabIndex = 4;
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(43, 95);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(46, 13);
            this.Label6.TabIndex = 15;
            this.Label6.Text = "Seccion";
            //
            // TxtSeccion
            //
            this.TxtSeccion.Location = new System.Drawing.Point(154, 91);
            this.TxtSeccion.MaxLength = 4;
            this.TxtSeccion.Name = "TxtSeccion";
            this.TxtSeccion.Size = new System.Drawing.Size(65, 20);
            this.TxtSeccion.TabIndex = 5;
            //
            // frmfiltros
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(290, 269);
            this.ControlBox = false;
            this.Controls.Add(this.TxtSeccion);
            this.Controls.Add(this.CBxSeccion);
            this.Controls.Add(this.Label6);
            this.Controls.Add(this.CbxEstado);
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.CmbPeriodicidad);
            this.Controls.Add(this.CmbSalir);
            this.Controls.Add(this.CmbAceptar);
            this.Controls.Add(this.TxtCiudad);
            this.Controls.Add(this.CbxCiudad);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.CbxPeriodicidad);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.TxtCencos);
            this.Controls.Add(this.CbxCencos);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.TxtEmpresa);
            this.Controls.Add(this.CbxEmpresa);
            this.Controls.Add(this.Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "frmfiltros";
            this.Load += new System.EventHandler(this.frmfiltros_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.ComboBox CbxEmpresa;
        internal System.Windows.Forms.TextBox TxtEmpresa;
        internal System.Windows.Forms.TextBox TxtCencos;
        internal System.Windows.Forms.ComboBox CbxCencos;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.ComboBox CbxPeriodicidad;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.TextBox TxtCiudad;
        internal System.Windows.Forms.ComboBox CbxCiudad;
        internal System.Windows.Forms.Label Label4;
        internal System.Windows.Forms.Button CmbSalir;
        internal System.Windows.Forms.Button CmbAceptar;
        internal System.Windows.Forms.ComboBox CmbPeriodicidad;
        internal System.Windows.Forms.ComboBox CbxEstado;
        internal System.Windows.Forms.Label Label5;
        internal System.Windows.Forms.ComboBox CBxSeccion;
        internal System.Windows.Forms.Label Label6;
        internal System.Windows.Forms.TextBox TxtSeccion;
    }
}
