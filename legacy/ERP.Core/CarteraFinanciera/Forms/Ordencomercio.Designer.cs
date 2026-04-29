namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class Ordencomercio
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Ordencomercio));
            this.ok = new System.Windows.Forms.Button();
            this.cancel = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.Label4 = new System.Windows.Forms.Label();
            this.benef = new System.Windows.Forms.Label();
            this.valor = new System.Windows.Forms.Label();
            this.fecha = new System.Windows.Forms.Label();
            this.prove = new System.Windows.Forms.TextBox();
            this.membrete = new System.Windows.Forms.CheckBox();
            this.ayuda_codi = new System.Windows.Forms.Button();
            this.nombreprove = new System.Windows.Forms.Label();
            this.LblCuenta24 = new System.Windows.Forms.Label();
            this.Label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // ok
            //
            this.ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ok.Location = new System.Drawing.Point(358, 142);
            this.ok.Name = "ok";
            this.ok.Size = new System.Drawing.Size(84, 30);
            this.ok.TabIndex = 0;
            this.ok.Text = "Aceptar";
            this.ok.UseVisualStyleBackColor = true;
            this.ok.Click += new System.EventHandler(this.ok_Click);
            //
            // cancel
            //
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(459, 142);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(84, 30);
            this.cancel.TabIndex = 1;
            this.cancel.Text = "Cancelar";
            this.cancel.UseVisualStyleBackColor = true;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(12, 29);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(111, 13);
            this.Label1.TabIndex = 2;
            this.Label1.Text = "Codigo del proveedor:";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(12, 55);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(114, 13);
            this.Label2.TabIndex = 3;
            this.Label2.Text = "Codigo del beneficiario";
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(12, 83);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(34, 13);
            this.Label3.TabIndex = 4;
            this.Label3.Text = "Valor:";
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(12, 110);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(40, 13);
            this.Label4.TabIndex = 5;
            this.Label4.Text = "Fecha:";
            //
            // benef
            //
            this.benef.BackColor = System.Drawing.Color.White;
            this.benef.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.benef.Location = new System.Drawing.Point(129, 54);
            this.benef.Name = "benef";
            this.benef.Size = new System.Drawing.Size(153, 20);
            this.benef.TabIndex = 6;
            //
            // valor
            //
            this.valor.BackColor = System.Drawing.Color.White;
            this.valor.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.valor.Location = new System.Drawing.Point(58, 82);
            this.valor.Name = "valor";
            this.valor.Size = new System.Drawing.Size(121, 20);
            this.valor.TabIndex = 7;
            //
            // fecha
            //
            this.fecha.BackColor = System.Drawing.Color.White;
            this.fecha.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.fecha.Location = new System.Drawing.Point(58, 109);
            this.fecha.Name = "fecha";
            this.fecha.Size = new System.Drawing.Size(121, 20);
            this.fecha.TabIndex = 8;
            //
            // prove
            //
            this.prove.Location = new System.Drawing.Point(129, 26);
            this.prove.Name = "prove";
            this.prove.Size = new System.Drawing.Size(153, 20);
            this.prove.TabIndex = 9;
            this.prove.TextChanged += new System.EventHandler(this.prove_TextChanged);
            //
            // membrete
            //
            this.membrete.AutoSize = true;
            this.membrete.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.membrete.Location = new System.Drawing.Point(12, 142);
            this.membrete.Name = "membrete";
            this.membrete.Size = new System.Drawing.Size(138, 17);
            this.membrete.TabIndex = 10;
            this.membrete.Text = "La hoja es membretiada";
            this.membrete.UseVisualStyleBackColor = true;
            //
            // ayuda_codi
            //
            this.ayuda_codi.BackColor = System.Drawing.SystemColors.Menu;
            this.ayuda_codi.Image = ((System.Drawing.Image)(resources.GetObject("ayuda_codi.Image")));
            this.ayuda_codi.Location = new System.Drawing.Point(288, 24);
            this.ayuda_codi.Name = "ayuda_codi";
            this.ayuda_codi.Size = new System.Drawing.Size(24, 24);
            this.ayuda_codi.TabIndex = 159;
            this.ayuda_codi.TabStop = false;
            this.ayuda_codi.UseVisualStyleBackColor = false;
            this.ayuda_codi.Click += new System.EventHandler(this.ayuda_codi_Click);
            //
            // nombreprove
            //
            this.nombreprove.BackColor = System.Drawing.Color.White;
            this.nombreprove.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.nombreprove.Location = new System.Drawing.Point(318, 26);
            this.nombreprove.Name = "nombreprove";
            this.nombreprove.Size = new System.Drawing.Size(225, 20);
            this.nombreprove.TabIndex = 160;
            //
            // LblCuenta24
            //
            this.LblCuenta24.BackColor = System.Drawing.Color.White;
            this.LblCuenta24.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCuenta24.Location = new System.Drawing.Point(318, 82);
            this.LblCuenta24.Name = "LblCuenta24";
            this.LblCuenta24.Size = new System.Drawing.Size(121, 20);
            this.LblCuenta24.TabIndex = 162;
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(230, 85);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(86, 13);
            this.Label6.TabIndex = 161;
            this.Label6.Text = "Valor Cuenta 24:";
            //
            // Ordencomercio
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(555, 184);
            this.Controls.Add(this.LblCuenta24);
            this.Controls.Add(this.Label6);
            this.Controls.Add(this.nombreprove);
            this.Controls.Add(this.ayuda_codi);
            this.Controls.Add(this.membrete);
            this.Controls.Add(this.prove);
            this.Controls.Add(this.fecha);
            this.Controls.Add(this.valor);
            this.Controls.Add(this.benef);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.ok);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Ordencomercio";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Orden al comercio";
            this.Load += new System.EventHandler(this.Ordencomercio_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.me_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Button ok;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.Label benef;
        private System.Windows.Forms.Label valor;
        private System.Windows.Forms.Label fecha;
        private System.Windows.Forms.TextBox prove;
        private System.Windows.Forms.CheckBox membrete;
        private System.Windows.Forms.Button ayuda_codi;
        private System.Windows.Forms.Label nombreprove;
        private System.Windows.Forms.Label LblCuenta24;
        private System.Windows.Forms.Label Label6;
    }
}
