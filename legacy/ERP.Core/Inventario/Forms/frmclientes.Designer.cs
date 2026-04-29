namespace ERP.Core.Inventario.Forms
{
    partial class frmclientes
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

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmclientes));
            this.Label10 = new System.Windows.Forms.Label();
            this.TxtCliente = new System.Windows.Forms.TextBox();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.txtEmail = new System.Windows.Forms.TextBox();
            this.TxtTelefono = new System.Windows.Forms.TextBox();
            this.txtDireccion = new System.Windows.Forms.TextBox();
            this.TxtNombre = new System.Windows.Forms.TextBox();
            this.CmbGraba = new System.Windows.Forms.Button();
            this.Button1 = new System.Windows.Forms.Button();
            this.Label9 = new System.Windows.Forms.Label();
            this.HelpTransaciones = new System.Windows.Forms.Button();
            this.GroupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label10.Location = new System.Drawing.Point(32, 26);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(51, 16);
            this.Label10.TabIndex = 5;
            this.Label10.Text = "Cedula";
            //
            // TxtCliente
            //
            this.TxtCliente.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.TxtCliente.Location = new System.Drawing.Point(109, 26);
            this.TxtCliente.Name = "TxtCliente";
            this.TxtCliente.Size = new System.Drawing.Size(141, 22);
            this.TxtCliente.TabIndex = 0;
            this.TxtCliente.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtCliente.TextChanged += new System.EventHandler(this.TxtCliente_TextChanged);
            this.TxtCliente.LostFocus += new System.EventHandler(this.TxtCliente_LostFocus);
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.Label4);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.txtEmail);
            this.GroupBox1.Controls.Add(this.TxtTelefono);
            this.GroupBox1.Controls.Add(this.txtDireccion);
            this.GroupBox1.Controls.Add(this.TxtNombre);
            this.GroupBox1.Location = new System.Drawing.Point(12, 65);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(509, 136);
            this.GroupBox1.TabIndex = 1;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Generales";
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label4.Location = new System.Drawing.Point(20, 102);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(42, 16);
            this.Label4.TabIndex = 11;
            this.Label4.Text = "Email";
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label3.Location = new System.Drawing.Point(20, 74);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(62, 16);
            this.Label3.TabIndex = 10;
            this.Label3.Text = "Telefono";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label2.Location = new System.Drawing.Point(20, 46);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(65, 16);
            this.Label2.TabIndex = 13;
            this.Label2.Text = "Direccion";
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label1.Location = new System.Drawing.Point(20, 18);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(57, 16);
            this.Label1.TabIndex = 12;
            this.Label1.Text = "Nombre";
            //
            // txtEmail
            //
            this.txtEmail.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.txtEmail.Location = new System.Drawing.Point(97, 99);
            this.txtEmail.Name = "txtEmail";
            this.txtEmail.Size = new System.Drawing.Size(391, 22);
            this.txtEmail.TabIndex = 3;
            //
            // TxtTelefono
            //
            this.TxtTelefono.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.TxtTelefono.Location = new System.Drawing.Point(97, 71);
            this.TxtTelefono.Name = "TxtTelefono";
            this.TxtTelefono.Size = new System.Drawing.Size(95, 22);
            this.TxtTelefono.TabIndex = 2;
            //
            // txtDireccion
            //
            this.txtDireccion.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.txtDireccion.Location = new System.Drawing.Point(97, 43);
            this.txtDireccion.Name = "txtDireccion";
            this.txtDireccion.Size = new System.Drawing.Size(391, 22);
            this.txtDireccion.TabIndex = 1;
            //
            // TxtNombre
            //
            this.TxtNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.TxtNombre.Location = new System.Drawing.Point(97, 15);
            this.TxtNombre.Name = "TxtNombre";
            this.TxtNombre.Size = new System.Drawing.Size(391, 22);
            this.TxtNombre.TabIndex = 0;
            //
            // CmbGraba
            //
            this.CmbGraba.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.CmbGraba.Location = new System.Drawing.Point(369, 207);
            this.CmbGraba.Name = "CmbGraba";
            this.CmbGraba.Size = new System.Drawing.Size(74, 56);
            this.CmbGraba.TabIndex = 6;
            this.CmbGraba.Text = "Graba";
            this.CmbGraba.UseCompatibleTextRendering = true;
            this.CmbGraba.UseVisualStyleBackColor = true;
            this.CmbGraba.Click += new System.EventHandler(this.CmbGraba_Click);
            //
            // Button1
            //
            this.Button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Button1.Location = new System.Drawing.Point(447, 207);
            this.Button1.Name = "Button1";
            this.Button1.Size = new System.Drawing.Size(74, 56);
            this.Button1.TabIndex = 6;
            this.Button1.Text = "Salir";
            this.Button1.UseCompatibleTextRendering = true;
            this.Button1.UseVisualStyleBackColor = true;
            this.Button1.Click += new System.EventHandler(this.Button1_Click);
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.Label9.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.Label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label9.ForeColor = System.Drawing.SystemColors.AppWorkspace;
            this.Label9.Location = new System.Drawing.Point(12, 216);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(213, 18);
            this.Label9.TabIndex = 9;
            this.Label9.Text = "F5  = GRABA  ESC= SALIR";
            //
            // HelpTransaciones
            //
            this.HelpTransaciones.BackColor = System.Drawing.SystemColors.Menu;
            this.HelpTransaciones.Image = (System.Drawing.Image)resources.GetObject("HelpTransaciones.Image");
            this.HelpTransaciones.Location = new System.Drawing.Point(257, 23);
            this.HelpTransaciones.Margin = new System.Windows.Forms.Padding(4);
            this.HelpTransaciones.Name = "HelpTransaciones";
            this.HelpTransaciones.Size = new System.Drawing.Size(28, 28);
            this.HelpTransaciones.TabIndex = 244;
            this.HelpTransaciones.TabStop = false;
            this.HelpTransaciones.UseVisualStyleBackColor = false;
            this.HelpTransaciones.Click += new System.EventHandler(this.HelpTransaciones_Click);
            //
            // frmclientes
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(533, 268);
            this.ControlBox = false;
            this.Controls.Add(this.HelpTransaciones);
            this.Controls.Add(this.Label9);
            this.Controls.Add(this.Button1);
            this.Controls.Add(this.CmbGraba);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.Label10);
            this.Controls.Add(this.TxtCliente);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.Name = "frmclientes";
            this.Text = "Actualizacion de clientes";
            this.Load += new System.EventHandler(this.frmclientes_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.frmclientes_KeyDown);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label Label10;
        private System.Windows.Forms.TextBox TxtCliente;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.TextBox txtEmail;
        private System.Windows.Forms.TextBox TxtTelefono;
        private System.Windows.Forms.TextBox txtDireccion;
        private System.Windows.Forms.TextBox TxtNombre;
        private System.Windows.Forms.Button CmbGraba;
        private System.Windows.Forms.Button Button1;
        private System.Windows.Forms.Label Label9;
        private System.Windows.Forms.Button HelpTransaciones;
    }
}
