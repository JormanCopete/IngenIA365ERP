using ERP.Core.CarteraFinanciera.Services.Depositos;
using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public class dep_fcanje01 : Form
    {
        private bool ok;
        private ERP.Core.Compartido.Utilidades.Ayuda msgAyuda = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private System.Data.Odbc.OdbcConnection conect = new System.Data.Odbc.OdbcConnection();
        private ERP.Core.CarteraFinanciera.Models.ParamCop Param = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ClsDepositos Depositos = new ClsDepositos();
        public int TipoCanje;
        public int diascanjeotras = 0;
        public int diascanjelocal = 0;
        private Button CmbCancel;
        private Label txtvalor;
        private TextBox txtbanco;
        private Label DtpFecha;
        private Label txtcanje;
        private Label DtpFecvence;
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera cop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private bool validado = false;
        public string Compronte;
        public double ConseCompronte;
        public string usuario;

        private GroupBox GroupBox3;
        private ComboBox cmbxformaTransaccion;
        private Label Label10;
        private Label Label12;
        private Label Label13;
        private Label Label1;
        private Label Label2;
        private Button cmbaceptar;
        private TextBox txtCuenta;
        private TextBox txtCheque;
        private Button Helpbanco;
        private Label lblcuenta;
        private Label Label3;
        private Label Label4;
        private Label Label5;
        private System.ComponentModel.IContainer components = null;

        public dep_fcanje01(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.conect = conexion;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(dep_fcanje01));
            this.GroupBox3 = new GroupBox();
            this.txtcanje = new Label();
            this.DtpFecvence = new Label();
            this.DtpFecha = new Label();
            this.txtvalor = new Label();
            this.CmbCancel = new Button();
            this.Label4 = new Label();
            this.lblcuenta = new Label();
            this.Helpbanco = new Button();
            this.txtCuenta = new TextBox();
            this.cmbxformaTransaccion = new ComboBox();
            this.Label10 = new Label();
            this.Label12 = new Label();
            this.Label13 = new Label();
            this.txtbanco = new TextBox();
            this.Label1 = new Label();
            this.cmbaceptar = new Button();
            this.Label2 = new Label();
            this.txtCheque = new TextBox();
            this.Label3 = new Label();
            this.Label5 = new Label();
            this.GroupBox3.SuspendLayout();
            this.SuspendLayout();
            // GroupBox3
            this.GroupBox3.Controls.Add(this.txtcanje);
            this.GroupBox3.Controls.Add(this.DtpFecvence);
            this.GroupBox3.Controls.Add(this.DtpFecha);
            this.GroupBox3.Controls.Add(this.txtvalor);
            this.GroupBox3.Controls.Add(this.CmbCancel);
            this.GroupBox3.Controls.Add(this.Label4);
            this.GroupBox3.Controls.Add(this.lblcuenta);
            this.GroupBox3.Controls.Add(this.Helpbanco);
            this.GroupBox3.Controls.Add(this.txtCuenta);
            this.GroupBox3.Controls.Add(this.cmbxformaTransaccion);
            this.GroupBox3.Controls.Add(this.Label10);
            this.GroupBox3.Controls.Add(this.Label12);
            this.GroupBox3.Controls.Add(this.Label13);
            this.GroupBox3.Controls.Add(this.txtbanco);
            this.GroupBox3.Controls.Add(this.Label1);
            this.GroupBox3.Controls.Add(this.cmbaceptar);
            this.GroupBox3.Controls.Add(this.Label2);
            this.GroupBox3.Controls.Add(this.txtCheque);
            this.GroupBox3.Controls.Add(this.Label3);
            this.GroupBox3.Controls.Add(this.Label5);
            this.GroupBox3.Location = new System.Drawing.Point(11, 10);
            this.GroupBox3.Margin = new System.Windows.Forms.Padding(4);
            this.GroupBox3.Name = "GroupBox3";
            this.GroupBox3.Padding = new System.Windows.Forms.Padding(4);
            this.GroupBox3.Size = new System.Drawing.Size(608, 158);
            this.GroupBox3.TabIndex = 3;
            this.GroupBox3.TabStop = false;
            this.GroupBox3.Text = "Datos Cheque";
            // txtcanje
            this.txtcanje.BackColor = System.Drawing.SystemColors.Window;
            this.txtcanje.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtcanje.Location = new System.Drawing.Point(295, 86);
            this.txtcanje.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtcanje.Name = "txtcanje";
            this.txtcanje.Size = new System.Drawing.Size(60, 20);
            this.txtcanje.TabIndex = 242;
            this.txtcanje.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // DtpFecvence
            this.DtpFecvence.BackColor = System.Drawing.SystemColors.Window;
            this.DtpFecvence.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DtpFecvence.Location = new System.Drawing.Point(460, 86);
            this.DtpFecvence.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.DtpFecvence.Name = "DtpFecvence";
            this.DtpFecvence.Size = new System.Drawing.Size(117, 20);
            this.DtpFecvence.TabIndex = 241;
            this.DtpFecvence.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // DtpFecha
            this.DtpFecha.BackColor = System.Drawing.SystemColors.Window;
            this.DtpFecha.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DtpFecha.Location = new System.Drawing.Point(89, 86);
            this.DtpFecha.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.DtpFecha.Name = "DtpFecha";
            this.DtpFecha.Size = new System.Drawing.Size(117, 20);
            this.DtpFecha.TabIndex = 240;
            this.DtpFecha.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // txtvalor
            this.txtvalor.BackColor = System.Drawing.SystemColors.Window;
            this.txtvalor.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtvalor.Location = new System.Drawing.Point(460, 55);
            this.txtvalor.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtvalor.Name = "txtvalor";
            this.txtvalor.Size = new System.Drawing.Size(132, 20);
            this.txtvalor.TabIndex = 239;
            this.txtvalor.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // CmbCancel
            this.CmbCancel.DialogResult = DialogResult.Cancel;
            this.CmbCancel.Location = new System.Drawing.Point(482, 117);
            this.CmbCancel.Margin = new System.Windows.Forms.Padding(4);
            this.CmbCancel.Name = "CmbCancel";
            this.CmbCancel.Size = new System.Drawing.Size(110, 34);
            this.CmbCancel.TabIndex = 239;
            this.CmbCancel.Text = "Cancelar";
            this.CmbCancel.Click += new System.EventHandler(this.CmbCancel_Click);
            // Label4
            this.Label4.Location = new System.Drawing.Point(213, 86);
            this.Label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(85, 20);
            this.Label4.TabIndex = 238;
            this.Label4.Text = "Dias Canje";
            // lblcuenta
            this.lblcuenta.Location = new System.Drawing.Point(11, 116);
            this.lblcuenta.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblcuenta.Name = "lblcuenta";
            this.lblcuenta.Size = new System.Drawing.Size(133, 28);
            this.lblcuenta.TabIndex = 236;
            this.lblcuenta.Text = "Label3";
            this.lblcuenta.Visible = false;
            // Helpbanco
            this.Helpbanco.BackColor = System.Drawing.SystemColors.Menu;
            this.Helpbanco.Image = (System.Drawing.Image)resources.GetObject("Helpbanco.Image");
            this.Helpbanco.Location = new System.Drawing.Point(530, 17);
            this.Helpbanco.Margin = new System.Windows.Forms.Padding(4);
            this.Helpbanco.Name = "Helpbanco";
            this.Helpbanco.Size = new System.Drawing.Size(32, 30);
            this.Helpbanco.TabIndex = 235;
            this.Helpbanco.TabStop = false;
            this.Helpbanco.UseVisualStyleBackColor = false;
            this.Helpbanco.Click += new System.EventHandler(this.Helpbanco_Click);
            // txtCuenta
            this.txtCuenta.Location = new System.Drawing.Point(88, 54);
            this.txtCuenta.Margin = new System.Windows.Forms.Padding(4);
            this.txtCuenta.MaxLength = 15;
            this.txtCuenta.Name = "txtCuenta";
            this.txtCuenta.Size = new System.Drawing.Size(167, 22);
            this.txtCuenta.TabIndex = 2;
            // cmbxformaTransaccion
            this.cmbxformaTransaccion.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbxformaTransaccion.Items.AddRange(new object[] { "Cheque Local ", "Cheque Otras Plazas" });
            this.cmbxformaTransaccion.Location = new System.Drawing.Point(88, 20);
            this.cmbxformaTransaccion.Margin = new System.Windows.Forms.Padding(4);
            this.cmbxformaTransaccion.Name = "cmbxformaTransaccion";
            this.cmbxformaTransaccion.Size = new System.Drawing.Size(252, 24);
            this.cmbxformaTransaccion.TabIndex = 0;
            this.cmbxformaTransaccion.SelectedIndexChanged += new System.EventHandler(this.cmbxformaTransaccion_SelectedIndexChanged);
            // Label10
            this.Label10.Location = new System.Drawing.Point(409, 55);
            this.Label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(43, 20);
            this.Label10.TabIndex = 4;
            this.Label10.Text = "Valor";
            this.Label10.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // Label12
            this.Label12.Location = new System.Drawing.Point(11, 22);
            this.Label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label12.Name = "Label12";
            this.Label12.Size = new System.Drawing.Size(96, 20);
            this.Label12.TabIndex = 0;
            this.Label12.Text = "Tipo Plaza";
            // Label13
            this.Label13.Location = new System.Drawing.Point(388, 22);
            this.Label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label13.Name = "Label13";
            this.Label13.Size = new System.Drawing.Size(64, 20);
            this.Label13.TabIndex = 0;
            this.Label13.Text = "Banco";
            this.Label13.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // txtbanco
            this.txtbanco.Location = new System.Drawing.Point(460, 21);
            this.txtbanco.Margin = new System.Windows.Forms.Padding(4);
            this.txtbanco.MaxLength = 15;
            this.txtbanco.Name = "txtbanco";
            this.txtbanco.Size = new System.Drawing.Size(63, 22);
            this.txtbanco.TabIndex = 1;
            // Label1
            this.Label1.Location = new System.Drawing.Point(11, 55);
            this.Label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(85, 20);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "No. Cuenta";
            // cmbaceptar
            this.cmbaceptar.DialogResult = DialogResult.OK;
            this.cmbaceptar.Location = new System.Drawing.Point(359, 117);
            this.cmbaceptar.Margin = new System.Windows.Forms.Padding(4);
            this.cmbaceptar.Name = "cmbaceptar";
            this.cmbaceptar.Size = new System.Drawing.Size(110, 34);
            this.cmbaceptar.TabIndex = 8;
            this.cmbaceptar.Text = "Aceptar";
            this.cmbaceptar.Click += new System.EventHandler(this.cmbaceptar_Click);
            // Label2
            this.Label2.Location = new System.Drawing.Point(255, 55);
            this.Label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(85, 20);
            this.Label2.TabIndex = 0;
            this.Label2.Text = "No. Cheque";
            // txtCheque
            this.txtCheque.Location = new System.Drawing.Point(341, 54);
            this.txtCheque.Margin = new System.Windows.Forms.Padding(4);
            this.txtCheque.MaxLength = 15;
            this.txtCheque.Name = "txtCheque";
            this.txtCheque.Size = new System.Drawing.Size(63, 22);
            this.txtCheque.TabIndex = 3;
            // Label3
            this.Label3.Location = new System.Drawing.Point(11, 86);
            this.Label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(85, 20);
            this.Label3.TabIndex = 0;
            this.Label3.Text = "Fecha ";
            // Label5
            this.Label5.Location = new System.Drawing.Point(356, 86);
            this.Label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(96, 20);
            this.Label5.TabIndex = 0;
            this.Label5.Text = "Fecha Vence";
            this.Label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // dep_fcanje01
            this.AutoScaleDimensions = new System.Drawing.SizeF(8f, 16f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(629, 174);
            this.Controls.Add(this.GroupBox3);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "dep_fcanje01";
            this.Text = "Cheque a canje";
            this.FormClosing += new FormClosingEventHandler(this.dep_fcanje01_FormClosing);
            this.Load += new System.EventHandler(this.dep_fcanje01_Load);
            this.GroupBox3.ResumeLayout(false);
            this.GroupBox3.PerformLayout();
            this.ResumeLayout(false);
        }

        private void dep_fcanje01_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!validado)
                e.Cancel = true;
        }

        private void dep_fcanje01_Load(object sender, EventArgs e)
        {
            diascanjelocal = Convert.ToInt32(txtcanje.Text == "" ? "0" : txtcanje.Text);
            DtpFecvence.Text = DateTime.Parse(DtpFecha.Text).AddDays(Convert.ToInt32(txtcanje.Text == "" ? "0" : txtcanje.Text)).ToString("dd/MM/yyyy");
            cmbxformaTransaccion.SelectedIndex = 0;
            cmbxformaTransaccion.Focus();
            this.CenterToScreen();
        }

        private void Helpbanco_Click(object sender, EventArgs e)
        {
            txtbanco.Text = msgAyuda.CargaAyuda("sys_banco03", "codigo_banco", "Nombre", "Nomres", conect, this, "Nombre Gerente");
            txtbanco.Focus();
        }

        private void cmbaceptar_Click(object sender, EventArgs e)
        {
            if (ValidaCampos())
                GrabaCanje();
        }

        private bool ValidaCampos()
        {
            if (!Param.BuscaBanco(txtbanco.Text, conect))
            {
                MessageBox.Show("Banco no esta Creado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtbanco.Text = "";
                txtbanco.Focus();
                return false;
            }
            if (txtCuenta.Text == "")
            {
                MessageBox.Show("Falta cuenta del cheque", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtCuenta.Focus();
                return false;
            }
            if (!Microsoft.VisualBasic.Information.IsNumeric(txtvalor.Text))
            {
                MessageBox.Show("Valor del Cheque Invalido", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        private void GrabaCanje()
        {
            string plaza = cmbxformaTransaccion.SelectedIndex == 0 ? "L" : "O";
            string estado = "L";

            if (TipoCanje == 2)
                ok = Depositos.GrabarChequeCanje(Convert.ToDouble(lblcuenta.Text), Convert.ToInt32(txtCheque.Text), DateTime.Parse(DtpFecha.Text), Convert.ToInt32(txtcanje.Text),
                    DateTime.Parse(DtpFecvence.Text), plaza, txtbanco.Text, Convert.ToDouble(txtvalor.Text), TipoCanje, conect, estado, Compronte, ConseCompronte, usuario);
            else
                ok = Depositos.GrabarChequeCanje(Convert.ToDouble(lblcuenta.Text), Convert.ToInt32(txtCheque.Text), DateTime.Parse(DtpFecha.Text), Convert.ToInt32(txtcanje.Text),
                    DateTime.Parse(DtpFecvence.Text), plaza, txtbanco.Text, Convert.ToDouble(txtvalor.Text), TipoCanje, conect, null, Compronte, ConseCompronte, usuario);

            validado = ok;
        }

        // campo 'ok' duplicado eliminado (original en linea 10)

        private void CmbCancel_Click(object sender, EventArgs e)
        {
            validado = true;
        }

        private void cmbxformaTransaccion_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtcanje.Text = cmbxformaTransaccion.SelectedIndex == 0
                ? diascanjelocal.ToString()
                : diascanjeotras.ToString();

            DtpFecvence.Text = DateTime.Parse(DtpFecha.Text)
                .AddDays(Convert.ToInt32(txtcanje.Text == "" ? "0" : txtcanje.Text))
                .ToString("dd/MM/yyyy");
        }
    }
}
