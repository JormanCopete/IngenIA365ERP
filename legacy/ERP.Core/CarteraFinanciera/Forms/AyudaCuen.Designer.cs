namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class AyudaCuen
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle s1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle s2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle s3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle s4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle s5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AyudaCuen));
            this.DgvGrilla = new System.Windows.Forms.DataGridView();
            this.clmNumCuenta = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmlinea = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmdescripcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmExcenta = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BtnOk = new System.Windows.Forms.Button();
            this.BtnCancel = new System.Windows.Forms.Button();
            this.GbxCredito = new System.Windows.Forms.GroupBox();
            this.TxtValor = new System.Windows.Forms.TextBox();
            this.GrbCuenta = new System.Windows.Forms.GroupBox();
            this.LblNombre = new System.Windows.Forms.Label();
            this.ayuda_codi = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.TxtCodigoter = new ERP.Core.Compartido.Controles.TexboxSoloNumeros();
            ((System.ComponentModel.ISupportInitialize)(this.DgvGrilla)).BeginInit();
            this.GbxCredito.SuspendLayout();
            this.GrbCuenta.SuspendLayout();
            this.SuspendLayout();
            // DgvGrilla
            this.DgvGrilla.AllowUserToAddRows = false;
            this.DgvGrilla.AllowUserToDeleteRows = false;
            this.DgvGrilla.BackgroundColor = System.Drawing.SystemColors.Window;
            s1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            s1.BackColor = System.Drawing.Color.LightSlateGray;
            s1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            s1.ForeColor = System.Drawing.SystemColors.Window;
            s1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            s1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            s1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgvGrilla.ColumnHeadersDefaultCellStyle = s1;
            this.DgvGrilla.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.DgvGrilla.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmNumCuenta, this.clmlinea, this.clmdescripcion, this.clmExcenta });
            this.DgvGrilla.EnableHeadersVisualStyles = false;
            this.DgvGrilla.Location = new System.Drawing.Point(12, 32);
            this.DgvGrilla.MultiSelect = false;
            this.DgvGrilla.Name = "DgvGrilla";
            this.DgvGrilla.ReadOnly = true;
            this.DgvGrilla.RowHeadersVisible = false;
            this.DgvGrilla.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.DgvGrilla.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgvGrilla.Size = new System.Drawing.Size(498, 221);
            this.DgvGrilla.TabIndex = 0;
            this.DgvGrilla.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvGrilla_CellDoubleClick);
            this.DgvGrilla.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvGrilla_CellContentClick);
            this.DgvGrilla.SelectionChanged += new System.EventHandler(this.DgvGrilla_SelectionChanged);
            this.DgvGrilla.DoubleClick += new System.EventHandler(this.DgvGrilla_DoubleClick);
            // clmNumCuenta
            this.clmNumCuenta.DataPropertyName = "num_cuenta";
            s2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            s2.BackColor = System.Drawing.Color.Beige;
            s2.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            s2.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.clmNumCuenta.DefaultCellStyle = s2;
            this.clmNumCuenta.HeaderText = "No Cuenta";
            this.clmNumCuenta.Name = "clmNumCuenta";
            this.clmNumCuenta.ReadOnly = true;
            this.clmNumCuenta.Width = 120;
            // clmlinea
            this.clmlinea.DataPropertyName = "linea";
            s3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            s3.BackColor = System.Drawing.Color.Ivory;
            s3.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            s3.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.clmlinea.DefaultCellStyle = s3;
            this.clmlinea.HeaderText = "Linea";
            this.clmlinea.Name = "clmlinea";
            this.clmlinea.ReadOnly = true;
            this.clmlinea.Width = 60;
            // clmdescripcion
            this.clmdescripcion.DataPropertyName = "descripcion";
            s4.BackColor = System.Drawing.Color.Ivory;
            s4.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            s4.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.clmdescripcion.DefaultCellStyle = s4;
            this.clmdescripcion.HeaderText = "Descripcion";
            this.clmdescripcion.Name = "clmdescripcion";
            this.clmdescripcion.ReadOnly = true;
            this.clmdescripcion.Width = 250;
            // clmExcenta
            this.clmExcenta.DataPropertyName = "excenta";
            s5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            s5.BackColor = System.Drawing.Color.Beige;
            s5.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            s5.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.clmExcenta.DefaultCellStyle = s5;
            this.clmExcenta.HeaderText = "Excenta";
            this.clmExcenta.Name = "clmExcenta";
            this.clmExcenta.ReadOnly = true;
            this.clmExcenta.Width = 50;
            // BtnOk
            this.BtnOk.Location = new System.Drawing.Point(330, 266);
            this.BtnOk.Name = "BtnOk";
            this.BtnOk.Size = new System.Drawing.Size(86, 32);
            this.BtnOk.TabIndex = 1;
            this.BtnOk.Text = "Aceptar";
            this.BtnOk.UseVisualStyleBackColor = true;
            this.BtnOk.Click += new System.EventHandler(this.BtnOk_Click);
            // BtnCancel
            this.BtnCancel.Location = new System.Drawing.Point(426, 266);
            this.BtnCancel.Name = "BtnCancel";
            this.BtnCancel.Size = new System.Drawing.Size(86, 32);
            this.BtnCancel.TabIndex = 2;
            this.BtnCancel.Text = "Cancelar";
            this.BtnCancel.UseVisualStyleBackColor = true;
            this.BtnCancel.Click += new System.EventHandler(this.BtnCancel_Click);
            // GbxCredito
            this.GbxCredito.Controls.Add(this.TxtValor);
            this.GbxCredito.Location = new System.Drawing.Point(12, 255);
            this.GbxCredito.Name = "GbxCredito";
            this.GbxCredito.Size = new System.Drawing.Size(138, 44);
            this.GbxCredito.TabIndex = 3;
            this.GbxCredito.TabStop = false;
            this.GbxCredito.Text = "Valor a desembolsar";
            this.GbxCredito.Visible = false;
            // TxtValor
            this.TxtValor.Location = new System.Drawing.Point(9, 18);
            this.TxtValor.Name = "TxtValor";
            this.TxtValor.Size = new System.Drawing.Size(116, 20);
            this.TxtValor.TabIndex = 0;
            this.TxtValor.Text = "0";
            this.TxtValor.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtValor.LostFocus += new System.EventHandler(this.TxtValor_LostFocus);
            // GrbCuenta
            this.GrbCuenta.Controls.Add(this.LblNombre);
            this.GrbCuenta.Controls.Add(this.ayuda_codi);
            this.GrbCuenta.Controls.Add(this.Label1);
            this.GrbCuenta.Controls.Add(this.TxtCodigoter);
            this.GrbCuenta.Location = new System.Drawing.Point(12, -3);
            this.GrbCuenta.Name = "GrbCuenta";
            this.GrbCuenta.Size = new System.Drawing.Size(498, 37);
            this.GrbCuenta.TabIndex = 4;
            this.GrbCuenta.TabStop = false;
            this.GrbCuenta.Visible = false;
            // LblNombre
            this.LblNombre.BackColor = System.Drawing.SystemColors.Control;
            this.LblNombre.Location = new System.Drawing.Point(178, 14);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(314, 17);
            this.LblNombre.TabIndex = 127;
            this.LblNombre.Text = " ";
            // ayuda_codi
            this.ayuda_codi.BackColor = System.Drawing.SystemColors.Menu;
            this.ayuda_codi.Image = (System.Drawing.Image)resources.GetObject("ayuda_codi.Image");
            this.ayuda_codi.Location = new System.Drawing.Point(146, 8);
            this.ayuda_codi.Margin = new System.Windows.Forms.Padding(4);
            this.ayuda_codi.Name = "ayuda_codi";
            this.ayuda_codi.Size = new System.Drawing.Size(25, 25);
            this.ayuda_codi.TabIndex = 126;
            this.ayuda_codi.TabStop = false;
            this.ayuda_codi.UseVisualStyleBackColor = false;
            this.ayuda_codi.Click += new System.EventHandler(this.ayuda_codi_Click);
            // Label1
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(7, 14);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(40, 13);
            this.Label1.TabIndex = 2;
            this.Label1.Text = "Codigo";
            // TxtCodigoter
            this.TxtCodigoter.Location = new System.Drawing.Point(47, 11);
            this.TxtCodigoter.MaxLength = 14;
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.Size = new System.Drawing.Size(94, 20);
            this.TxtCodigoter.SoloNumeros = true;
            this.TxtCodigoter.TabIndex = 0;
            this.TxtCodigoter.LostFocus += new System.EventHandler(this.TxtCodigoter_LostFocus);
            // AyudaCuen
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(522, 304);
            this.Controls.Add(this.GbxCredito);
            this.Controls.Add(this.BtnCancel);
            this.Controls.Add(this.BtnOk);
            this.Controls.Add(this.DgvGrilla);
            this.Controls.Add(this.GrbCuenta);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AyudaCuen";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Ayuda Cuentas";
            this.Load += new System.EventHandler(this.AyudaCuen_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DgvGrilla)).EndInit();
            this.GbxCredito.ResumeLayout(false);
            this.GbxCredito.PerformLayout();
            this.GrbCuenta.ResumeLayout(false);
            this.GrbCuenta.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.DataGridView DgvGrilla;
        private System.Windows.Forms.Button BtnOk;
        private System.Windows.Forms.Button BtnCancel;
        private System.Windows.Forms.GroupBox GbxCredito;
        private System.Windows.Forms.TextBox TxtValor;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmNumCuenta;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmlinea;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmdescripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmExcenta;
        private System.Windows.Forms.GroupBox GrbCuenta;
        private System.Windows.Forms.Label Label1;
        private ERP.Core.Compartido.Controles.TexboxSoloNumeros TxtCodigoter;
        private System.Windows.Forms.Button ayuda_codi;
        private System.Windows.Forms.Label LblNombre;
    }
}
