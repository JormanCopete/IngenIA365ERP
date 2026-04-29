namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmrectarj01
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmrectarj01));
            this.TsysEmpresa = new System.Windows.Forms.TextBox();
            this.Label33 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.TxtvalPendiente = new System.Windows.Forms.TextBox();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.CmbGuardar = new System.Windows.Forms.Button();
            this.CmbSalir = new System.Windows.Forms.Button();
            this.TxtPagar = new System.Windows.Forms.TextBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.Txtperiodo = new System.Windows.Forms.TextBox();
            this.Label7 = new System.Windows.Forms.Label();
            this.Label10 = new System.Windows.Forms.Label();
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.HelpAsociado = new System.Windows.Forms.Button();
            this.TxtCiclo = new System.Windows.Forms.TextBox();
            this.Label9 = new System.Windows.Forms.Label();
            this.TxtpendAvances = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
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
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(16, 93);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(82, 13);
            this.Label2.TabIndex = 259;
            this.Label2.Text = "Valor Pendiente";
            //
            // TxtvalPendiente
            //
            this.TxtvalPendiente.Enabled = false;
            this.TxtvalPendiente.Location = new System.Drawing.Point(95, 90);
            this.TxtvalPendiente.Name = "TxtvalPendiente";
            this.TxtvalPendiente.Size = new System.Drawing.Size(100, 20);
            this.TxtvalPendiente.TabIndex = 2;
            this.TxtvalPendiente.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // CmbGuardar
            //
            this.CmbGuardar.AutoSize = true;
            this.CmbGuardar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbGuardar.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CmbGuardar.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbGuardar.Image = global::ERP.Core.Properties.Resources.disco;
            this.CmbGuardar.Location = new System.Drawing.Point(292, 141);
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
            this.CmbSalir.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CmbSalir.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.CmbSalir.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Olive;
            this.CmbSalir.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(64)))), ((int)(((byte)(0)))));
            this.CmbSalir.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbSalir.Image = global::ERP.Core.Properties.Resources.Salir031;
            this.CmbSalir.Location = new System.Drawing.Point(333, 141);
            this.CmbSalir.Name = "CmbSalir";
            this.CmbSalir.Size = new System.Drawing.Size(38, 38);
            this.CmbSalir.TabIndex = 7;
            this.ToolTip1.SetToolTip(this.CmbSalir, "Salir");
            this.CmbSalir.UseVisualStyleBackColor = true;
            this.CmbSalir.Click += new System.EventHandler(this.CmbSalir_Click);
            //
            // TxtPagar
            //
            this.TxtPagar.BackColor = System.Drawing.Color.Gainsboro;
            this.TxtPagar.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtPagar.ForeColor = System.Drawing.Color.Chocolate;
            this.TxtPagar.Location = new System.Drawing.Point(95, 116);
            this.TxtPagar.Name = "TxtPagar";
            this.TxtPagar.Size = new System.Drawing.Size(100, 26);
            this.TxtPagar.TabIndex = 4;
            this.TxtPagar.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtPagar.TextChanged += new System.EventHandler(this.TxtPagar_TextChanged);
            this.TxtPagar.LostFocus += new System.EventHandler(this.TxtPagar_LostFocus);
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(16, 123);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(62, 13);
            this.Label6.TabIndex = 266;
            this.Label6.Text = "Valor Pagar";
            //
            // Txtperiodo
            //
            this.Txtperiodo.Enabled = false;
            this.Txtperiodo.Location = new System.Drawing.Point(303, 112);
            this.Txtperiodo.Name = "Txtperiodo";
            this.Txtperiodo.Size = new System.Drawing.Size(70, 20);
            this.Txtperiodo.TabIndex = 5;
            this.Txtperiodo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.Txtperiodo.TextChanged += new System.EventHandler(this.Txtperiodo_TextChanged);
            this.Txtperiodo.LostFocus += new System.EventHandler(this.Txtperiodo_LostFocus);
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Location = new System.Drawing.Point(258, 64);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(30, 13);
            this.Label7.TabIndex = 268;
            this.Label7.Text = "Ciclo";
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Location = new System.Drawing.Point(16, 63);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(40, 13);
            this.Label10.TabIndex = 275;
            this.Label10.Text = "Cedula";
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.Location = new System.Drawing.Point(95, 60);
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.Size = new System.Drawing.Size(100, 20);
            this.TxtCodigoter.TabIndex = 0;
            this.TxtCodigoter.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // HelpAsociado
            //
            this.HelpAsociado.Image = ((System.Drawing.Image)(resources.GetObject("HelpAsociado.Image")));
            this.HelpAsociado.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.HelpAsociado.Location = new System.Drawing.Point(199, 55);
            this.HelpAsociado.Margin = new System.Windows.Forms.Padding(4);
            this.HelpAsociado.Name = "HelpAsociado";
            this.HelpAsociado.Size = new System.Drawing.Size(36, 30);
            this.HelpAsociado.TabIndex = 276;
            this.HelpAsociado.TabStop = false;
            this.HelpAsociado.Click += new System.EventHandler(this.HelpAsociado_Click);
            //
            // TxtCiclo
            //
            this.TxtCiclo.Location = new System.Drawing.Point(301, 61);
            this.TxtCiclo.Name = "TxtCiclo";
            this.TxtCiclo.Size = new System.Drawing.Size(72, 20);
            this.TxtCiclo.TabIndex = 1;
            this.TxtCiclo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtCiclo.TextChanged += new System.EventHandler(this.TxtCiclo_TextChanged);
            this.TxtCiclo.LostFocus += new System.EventHandler(this.TxtCiclo_LostFocus);
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Location = new System.Drawing.Point(254, 115);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(43, 13);
            this.Label9.TabIndex = 277;
            this.Label9.Text = "Periodo";
            //
            // TxtpendAvances
            //
            this.TxtpendAvances.Enabled = false;
            this.TxtpendAvances.Location = new System.Drawing.Point(273, 86);
            this.TxtpendAvances.Name = "TxtpendAvances";
            this.TxtpendAvances.Size = new System.Drawing.Size(100, 20);
            this.TxtpendAvances.TabIndex = 3;
            this.TxtpendAvances.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(197, 89);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(76, 13);
            this.Label1.TabIndex = 279;
            this.Label1.Text = "Valor Avances";
            //
            // frmrectarj01
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(376, 184);
            this.ControlBox = false;
            this.Controls.Add(this.TxtpendAvances);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.Label9);
            this.Controls.Add(this.TxtCiclo);
            this.Controls.Add(this.HelpAsociado);
            this.Controls.Add(this.Label10);
            this.Controls.Add(this.TxtCodigoter);
            this.Controls.Add(this.Label7);
            this.Controls.Add(this.Txtperiodo);
            this.Controls.Add(this.TxtPagar);
            this.Controls.Add(this.Label6);
            this.Controls.Add(this.CmbGuardar);
            this.Controls.Add(this.CmbSalir);
            this.Controls.Add(this.TxtvalPendiente);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.TsysEmpresa);
            this.Controls.Add(this.Label33);
            this.Name = "frmrectarj01";
            this.Load += new System.EventHandler(this.frmbase_Load);
            this.LostFocus += new System.EventHandler(this.frmrecaudo_LostFocus);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.TextBox TsysEmpresa;
        private System.Windows.Forms.Label Label33;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.TextBox TxtvalPendiente;
        private System.Windows.Forms.Button CmbGuardar;
        private System.Windows.Forms.Button CmbSalir;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.TextBox TxtPagar;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.TextBox Txtperiodo;
        private System.Windows.Forms.Label Label7;
        private System.Windows.Forms.Label Label10;
        private System.Windows.Forms.TextBox TxtCodigoter;
        private System.Windows.Forms.Button HelpAsociado;
        private System.Windows.Forms.TextBox TxtCiclo;
        private System.Windows.Forms.Label Label9;
        private System.Windows.Forms.TextBox TxtpendAvances;
        private System.Windows.Forms.Label Label1;
    }
}
