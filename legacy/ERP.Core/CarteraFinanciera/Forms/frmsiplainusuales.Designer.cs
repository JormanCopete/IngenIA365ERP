using System.Data.Odbc;

namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmsiplainusuales
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
            this.Label2 = new System.Windows.Forms.Label();
            this.lblCodigoter = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.lblNumero = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.lblLinea = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.GroupBox2 = new System.Windows.Forms.GroupBox();
            this.txtObservaciones = new System.Windows.Forms.TextBox();
            this.GroupBox3 = new System.Windows.Forms.GroupBox();
            this.btnCancelar = new System.Windows.Forms.Button();
            this.btnAceptar = new System.Windows.Forms.Button();
            this.GroupBox1.SuspendLayout();
            this.GroupBox2.SuspendLayout();
            this.GroupBox3.SuspendLayout();
            this.SuspendLayout();
            //
            // Label2
            //
            this.Label2.Location = new System.Drawing.Point(7, 22);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(51, 13);
            this.Label2.TabIndex = 2;
            this.Label2.Text = "Asociado";
            //
            // lblCodigoter
            //
            this.lblCodigoter.Location = new System.Drawing.Point(80, 22);
            this.lblCodigoter.Name = "lblCodigoter";
            this.lblCodigoter.Size = new System.Drawing.Size(299, 19);
            this.lblCodigoter.TabIndex = 3;
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.lblNumero);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.lblLinea);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.lblCodigoter);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Location = new System.Drawing.Point(12, 12);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(393, 81);
            this.GroupBox1.TabIndex = 2;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Asociado";
            //
            // lblNumero
            //
            this.lblNumero.Location = new System.Drawing.Point(150, 53);
            this.lblNumero.Name = "lblNumero";
            this.lblNumero.Size = new System.Drawing.Size(100, 23);
            this.lblNumero.TabIndex = 7;
            //
            // Label3
            //
            this.Label3.Location = new System.Drawing.Point(101, 53);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(44, 23);
            this.Label3.TabIndex = 6;
            this.Label3.Text = "Numero";
            //
            // lblLinea
            //
            this.lblLinea.Location = new System.Drawing.Point(50, 53);
            this.lblLinea.Name = "lblLinea";
            this.lblLinea.Size = new System.Drawing.Size(45, 23);
            this.lblLinea.TabIndex = 5;
            //
            // Label1
            //
            this.Label1.Location = new System.Drawing.Point(10, 53);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(100, 23);
            this.Label1.TabIndex = 4;
            this.Label1.Text = "Linea";
            //
            // GroupBox2
            //
            this.GroupBox2.Controls.Add(this.txtObservaciones);
            this.GroupBox2.Location = new System.Drawing.Point(12, 99);
            this.GroupBox2.Name = "GroupBox2";
            this.GroupBox2.Size = new System.Drawing.Size(393, 86);
            this.GroupBox2.TabIndex = 0;
            this.GroupBox2.TabStop = false;
            this.GroupBox2.Text = "Observaciones";
            //
            // txtObservaciones
            //
            this.txtObservaciones.Location = new System.Drawing.Point(14, 19);
            this.txtObservaciones.Multiline = true;
            this.txtObservaciones.Name = "txtObservaciones";
            this.txtObservaciones.Size = new System.Drawing.Size(365, 55);
            this.txtObservaciones.TabIndex = 0;
            //
            // GroupBox3
            //
            this.GroupBox3.Controls.Add(this.btnCancelar);
            this.GroupBox3.Controls.Add(this.btnAceptar);
            this.GroupBox3.Location = new System.Drawing.Point(82, 191);
            this.GroupBox3.Name = "GroupBox3";
            this.GroupBox3.Size = new System.Drawing.Size(224, 56);
            this.GroupBox3.TabIndex = 1;
            this.GroupBox3.TabStop = false;
            this.GroupBox3.Text = "Marcarlo Como Inusual";
            //
            // btnCancelar
            //
            this.btnCancelar.Location = new System.Drawing.Point(131, 20);
            this.btnCancelar.Name = "btnCancelar";
            this.btnCancelar.Size = new System.Drawing.Size(75, 23);
            this.btnCancelar.TabIndex = 1;
            this.btnCancelar.Text = "Cancelar";
            this.btnCancelar.UseVisualStyleBackColor = true;
            this.btnCancelar.Click += new System.EventHandler(this.btnCancelar_Click);
            //
            // btnAceptar
            //
            this.btnAceptar.Location = new System.Drawing.Point(50, 20);
            this.btnAceptar.Name = "btnAceptar";
            this.btnAceptar.Size = new System.Drawing.Size(75, 23);
            this.btnAceptar.TabIndex = 0;
            this.btnAceptar.Text = "Aceptar";
            this.btnAceptar.UseVisualStyleBackColor = true;
            this.btnAceptar.Click += new System.EventHandler(this.btnAceptar_Click);
            //
            // frmsiplainusuales
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(415, 280);
            this.ControlBox = false;
            this.Controls.Add(this.GroupBox3);
            this.Controls.Add(this.GroupBox2);
            this.Controls.Add(this.GroupBox1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmsiplainusuales";
            this.Text = "Movimiento Inusual  SIPLA";
            this.Load += new System.EventHandler(this.frmsiplainusuales_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.frmsiplainusuales_KeyDown);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox2.ResumeLayout(false);
            this.GroupBox2.PerformLayout();
            this.GroupBox3.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label lblCodigoter;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.GroupBox GroupBox2;
        private System.Windows.Forms.TextBox txtObservaciones;
        private System.Windows.Forms.GroupBox GroupBox3;
        private System.Windows.Forms.Button btnCancelar;
        private System.Windows.Forms.Button btnAceptar;
        private OdbcConnection myconnect;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label lblLinea;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label lblNumero;

        public frmsiplainusuales(OdbcConnection conexion)
            : base()
        {
            InitializeComponent();
            this.myconnect = conexion;
        }
    }
}
