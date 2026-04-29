namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class FrmGarantias
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
            this.components = new System.ComponentModel.Container();
            this.GbxAsegurado = new System.Windows.Forms.GroupBox();
            this.Label10 = new System.Windows.Forms.Label();
            this.Label9 = new System.Windows.Forms.Label();
            this.Label8 = new System.Windows.Forms.Label();
            this.Label7 = new System.Windows.Forms.Label();
            this.Label6 = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.DtpFechavencimiento = new System.Windows.Forms.DateTimePicker();
            this.TxtPorcentaje = new System.Windows.Forms.TextBox();
            this.TxtRazonsocial = new System.Windows.Forms.TextBox();
            this.TxtMatricula = new System.Windows.Forms.TextBox();
            this.TxtPoliza = new System.Windows.Forms.TextBox();
            this.TxtNitaseguradora = new System.Windows.Forms.TextBox();
            this.DtpFechafinal = new System.Windows.Forms.DateTimePicker();
            this.DtpFechainicial = new System.Windows.Forms.DateTimePicker();
            this.Label12 = new System.Windows.Forms.Label();
            this.Label11 = new System.Windows.Forms.Label();
            this.GbxGarantia = new System.Windows.Forms.GroupBox();
            this.TxtNumerocdat = new System.Windows.Forms.TextBox();
            this.Label16 = new System.Windows.Forms.Label();
            this.TxtDescripcion = new System.Windows.Forms.TextBox();
            this.TxtAvaluocomercial = new System.Windows.Forms.TextBox();
            this.TxtAvaluocatastral = new System.Windows.Forms.TextBox();
            this.TxtClaseGarantia = new System.Windows.Forms.TextBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.LblEmpresa = new System.Windows.Forms.Label();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label13 = new System.Windows.Forms.Label();
            this.Label14 = new System.Windows.Forms.Label();
            this.Label15 = new System.Windows.Forms.Label();
            this.TxtServidor = new System.Windows.Forms.TextBox();
            this.TxtDb = new System.Windows.Forms.TextBox();
            this.TxtNameform = new System.Windows.Forms.TextBox();
            this.TxtUsuario = new System.Windows.Forms.TextBox();
            this.TxtFechahora = new System.Windows.Forms.TextBox();
            this.FechaHora = new System.Windows.Forms.Timer(this.components);
            this.BtnImpimir = new System.Windows.Forms.Button();
            this.GbxAsegurado.SuspendLayout();
            this.GbxGarantia.SuspendLayout();
            this.SuspendLayout();
            //
            // GbxAsegurado
            //
            this.GbxAsegurado.Controls.Add(this.Label10);
            this.GbxAsegurado.Controls.Add(this.Label9);
            this.GbxAsegurado.Controls.Add(this.Label8);
            this.GbxAsegurado.Controls.Add(this.Label7);
            this.GbxAsegurado.Controls.Add(this.Label6);
            this.GbxAsegurado.Controls.Add(this.Label5);
            this.GbxAsegurado.Controls.Add(this.DtpFechavencimiento);
            this.GbxAsegurado.Controls.Add(this.TxtPorcentaje);
            this.GbxAsegurado.Controls.Add(this.TxtRazonsocial);
            this.GbxAsegurado.Controls.Add(this.TxtMatricula);
            this.GbxAsegurado.Controls.Add(this.TxtPoliza);
            this.GbxAsegurado.Controls.Add(this.TxtNitaseguradora);
            this.GbxAsegurado.Location = new System.Drawing.Point(13, 165);
            this.GbxAsegurado.Name = "GbxAsegurado";
            this.GbxAsegurado.Size = new System.Drawing.Size(618, 127);
            this.GbxAsegurado.TabIndex = 0;
            this.GbxAsegurado.TabStop = false;
            this.GbxAsegurado.Text = "Esta Asegurado";
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Location = new System.Drawing.Point(9, 81);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(167, 13);
            this.Label10.TabIndex = 11;
            this.Label10.Text = "Fecha de Vencimiento del Seguro";
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Location = new System.Drawing.Point(406, 52);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(112, 13);
            this.Label9.TabIndex = 10;
            this.Label9.Text = "Porcentaje Asegurado";
            //
            // Label8
            //
            this.Label8.AutoSize = true;
            this.Label8.Location = new System.Drawing.Point(209, 52);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(50, 13);
            this.Label8.TabIndex = 9;
            this.Label8.Text = "Matricula";
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Location = new System.Drawing.Point(9, 52);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(90, 13);
            this.Label7.TabIndex = 8;
            this.Label7.Text = "Numero de Poliza";
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(208, 22);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(70, 13);
            this.Label6.TabIndex = 7;
            this.Label6.Text = "Razon Social";
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(9, 23);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(83, 13);
            this.Label5.TabIndex = 6;
            this.Label5.Text = "Nit Aseguradora";
            //
            // DtpFechavencimiento
            //
            this.DtpFechavencimiento.Enabled = false;
            this.DtpFechavencimiento.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFechavencimiento.Location = new System.Drawing.Point(185, 79);
            this.DtpFechavencimiento.Name = "DtpFechavencimiento";
            this.DtpFechavencimiento.Size = new System.Drawing.Size(97, 20);
            this.DtpFechavencimiento.TabIndex = 25;
            //
            // TxtPorcentaje
            //
            this.TxtPorcentaje.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtPorcentaje.Enabled = false;
            this.TxtPorcentaje.Location = new System.Drawing.Point(528, 49);
            this.TxtPorcentaje.Name = "TxtPorcentaje";
            this.TxtPorcentaje.ReadOnly = true;
            this.TxtPorcentaje.Size = new System.Drawing.Size(84, 20);
            this.TxtPorcentaje.TabIndex = 23;
            //
            // TxtRazonsocial
            //
            this.TxtRazonsocial.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtRazonsocial.Enabled = false;
            this.TxtRazonsocial.Location = new System.Drawing.Point(300, 19);
            this.TxtRazonsocial.Name = "TxtRazonsocial";
            this.TxtRazonsocial.ReadOnly = true;
            this.TxtRazonsocial.Size = new System.Drawing.Size(312, 20);
            this.TxtRazonsocial.TabIndex = 17;
            //
            // TxtMatricula
            //
            this.TxtMatricula.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtMatricula.Enabled = false;
            this.TxtMatricula.Location = new System.Drawing.Point(300, 49);
            this.TxtMatricula.Name = "TxtMatricula";
            this.TxtMatricula.ReadOnly = true;
            this.TxtMatricula.Size = new System.Drawing.Size(100, 20);
            this.TxtMatricula.TabIndex = 21;
            //
            // TxtPoliza
            //
            this.TxtPoliza.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtPoliza.Enabled = false;
            this.TxtPoliza.Location = new System.Drawing.Point(102, 49);
            this.TxtPoliza.Name = "TxtPoliza";
            this.TxtPoliza.ReadOnly = true;
            this.TxtPoliza.Size = new System.Drawing.Size(97, 20);
            this.TxtPoliza.TabIndex = 19;
            //
            // TxtNitaseguradora
            //
            this.TxtNitaseguradora.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtNitaseguradora.Enabled = false;
            this.TxtNitaseguradora.Location = new System.Drawing.Point(101, 20);
            this.TxtNitaseguradora.Name = "TxtNitaseguradora";
            this.TxtNitaseguradora.ReadOnly = true;
            this.TxtNitaseguradora.Size = new System.Drawing.Size(98, 20);
            this.TxtNitaseguradora.TabIndex = 15;
            //
            // DtpFechafinal
            //
            this.DtpFechafinal.Enabled = false;
            this.DtpFechafinal.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFechafinal.Location = new System.Drawing.Point(296, 81);
            this.DtpFechafinal.Name = "DtpFechafinal";
            this.DtpFechafinal.Size = new System.Drawing.Size(100, 20);
            this.DtpFechafinal.TabIndex = 11;
            //
            // DtpFechainicial
            //
            this.DtpFechainicial.Enabled = false;
            this.DtpFechainicial.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFechainicial.Location = new System.Drawing.Point(101, 81);
            this.DtpFechainicial.Name = "DtpFechainicial";
            this.DtpFechainicial.Size = new System.Drawing.Size(98, 20);
            this.DtpFechainicial.TabIndex = 9;
            //
            // Label12
            //
            this.Label12.AutoSize = true;
            this.Label12.Location = new System.Drawing.Point(6, 85);
            this.Label12.Name = "Label12";
            this.Label12.Size = new System.Drawing.Size(69, 13);
            this.Label12.TabIndex = 13;
            this.Label12.Text = "Fecha inicial ";
            //
            // Label11
            //
            this.Label11.AutoSize = true;
            this.Label11.Location = new System.Drawing.Point(208, 84);
            this.Label11.Name = "Label11";
            this.Label11.Size = new System.Drawing.Size(62, 13);
            this.Label11.TabIndex = 12;
            this.Label11.Text = "Fecha Final";
            //
            // GbxGarantia
            //
            this.GbxGarantia.Controls.Add(this.TxtNumerocdat);
            this.GbxGarantia.Controls.Add(this.Label16);
            this.GbxGarantia.Controls.Add(this.DtpFechafinal);
            this.GbxGarantia.Controls.Add(this.TxtDescripcion);
            this.GbxGarantia.Controls.Add(this.Label11);
            this.GbxGarantia.Controls.Add(this.DtpFechainicial);
            this.GbxGarantia.Controls.Add(this.TxtAvaluocomercial);
            this.GbxGarantia.Controls.Add(this.Label12);
            this.GbxGarantia.Controls.Add(this.TxtAvaluocatastral);
            this.GbxGarantia.Controls.Add(this.TxtClaseGarantia);
            this.GbxGarantia.Controls.Add(this.Label4);
            this.GbxGarantia.Controls.Add(this.Label3);
            this.GbxGarantia.Controls.Add(this.Label2);
            this.GbxGarantia.Controls.Add(this.Label1);
            this.GbxGarantia.Location = new System.Drawing.Point(13, 43);
            this.GbxGarantia.Name = "GbxGarantia";
            this.GbxGarantia.Size = new System.Drawing.Size(618, 116);
            this.GbxGarantia.TabIndex = 1;
            this.GbxGarantia.TabStop = false;
            this.GbxGarantia.Text = "Garantia";
            //
            // TxtNumerocdat
            //
            this.TxtNumerocdat.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtNumerocdat.Enabled = false;
            this.TxtNumerocdat.Location = new System.Drawing.Point(491, 82);
            this.TxtNumerocdat.Name = "TxtNumerocdat";
            this.TxtNumerocdat.ReadOnly = true;
            this.TxtNumerocdat.Size = new System.Drawing.Size(121, 20);
            this.TxtNumerocdat.TabIndex = 13;
            //
            // Label16
            //
            this.Label16.AutoSize = true;
            this.Label16.Location = new System.Drawing.Point(405, 85);
            this.Label16.Name = "Label16";
            this.Label16.Size = new System.Drawing.Size(86, 13);
            this.Label16.TabIndex = 16;
            this.Label16.Text = "Numero del Cdat";
            //
            // TxtDescripcion
            //
            this.TxtDescripcion.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtDescripcion.Enabled = false;
            this.TxtDescripcion.Location = new System.Drawing.Point(99, 53);
            this.TxtDescripcion.Name = "TxtDescripcion";
            this.TxtDescripcion.ReadOnly = true;
            this.TxtDescripcion.Size = new System.Drawing.Size(513, 20);
            this.TxtDescripcion.TabIndex = 7;
            //
            // TxtAvaluocomercial
            //
            this.TxtAvaluocomercial.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtAvaluocomercial.Enabled = false;
            this.TxtAvaluocomercial.Location = new System.Drawing.Point(296, 25);
            this.TxtAvaluocomercial.Name = "TxtAvaluocomercial";
            this.TxtAvaluocomercial.ReadOnly = true;
            this.TxtAvaluocomercial.Size = new System.Drawing.Size(100, 20);
            this.TxtAvaluocomercial.TabIndex = 3;
            //
            // TxtAvaluocatastral
            //
            this.TxtAvaluocatastral.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtAvaluocatastral.Enabled = false;
            this.TxtAvaluocatastral.Location = new System.Drawing.Point(490, 25);
            this.TxtAvaluocatastral.Name = "TxtAvaluocatastral";
            this.TxtAvaluocatastral.ReadOnly = true;
            this.TxtAvaluocatastral.Size = new System.Drawing.Size(122, 20);
            this.TxtAvaluocatastral.TabIndex = 5;
            //
            // TxtClaseGarantia
            //
            this.TxtClaseGarantia.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.TxtClaseGarantia.Enabled = false;
            this.TxtClaseGarantia.Location = new System.Drawing.Point(99, 25);
            this.TxtClaseGarantia.Name = "TxtClaseGarantia";
            this.TxtClaseGarantia.ReadOnly = true;
            this.TxtClaseGarantia.Size = new System.Drawing.Size(100, 20);
            this.TxtClaseGarantia.TabIndex = 1;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(6, 29);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(91, 13);
            this.Label4.TabIndex = 3;
            this.Label4.Text = "Clase de Garantia";
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(6, 56);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(63, 13);
            this.Label3.TabIndex = 2;
            this.Label3.Text = "Descripcion";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(406, 29);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(84, 13);
            this.Label2.TabIndex = 1;
            this.Label2.Text = "Avaluo Catastral";
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(206, 28);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(89, 13);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "Avaluo Comercial";
            //
            // LblEmpresa
            //
            this.LblEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblEmpresa.Location = new System.Drawing.Point(14, 9);
            this.LblEmpresa.Name = "LblEmpresa";
            this.LblEmpresa.Size = new System.Drawing.Size(615, 31);
            this.LblEmpresa.TabIndex = 2;
            this.LblEmpresa.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(578, 298);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(53, 41);
            this.BtnSalir.TabIndex = 3;
            this.BtnSalir.UseVisualStyleBackColor = true;
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(-6, 346);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(656, 10);
            this.GroupBox1.TabIndex = 4;
            this.GroupBox1.TabStop = false;
            //
            // Label13
            //
            this.Label13.AutoSize = true;
            this.Label13.Location = new System.Drawing.Point(3, 366);
            this.Label13.Name = "Label13";
            this.Label13.Size = new System.Drawing.Size(46, 13);
            this.Label13.TabIndex = 5;
            this.Label13.Text = "Servidor";
            //
            // Label14
            //
            this.Label14.AutoSize = true;
            this.Label14.Location = new System.Drawing.Point(138, 366);
            this.Label14.Name = "Label14";
            this.Label14.Size = new System.Drawing.Size(22, 13);
            this.Label14.TabIndex = 6;
            this.Label14.Text = "DB";
            //
            // Label15
            //
            this.Label15.AutoSize = true;
            this.Label15.Location = new System.Drawing.Point(367, 366);
            this.Label15.Name = "Label15";
            this.Label15.Size = new System.Drawing.Size(43, 13);
            this.Label15.TabIndex = 7;
            this.Label15.Text = "Usuario";
            //
            // TxtServidor
            //
            this.TxtServidor.Location = new System.Drawing.Point(51, 363);
            this.TxtServidor.Name = "TxtServidor";
            this.TxtServidor.ReadOnly = true;
            this.TxtServidor.Size = new System.Drawing.Size(74, 20);
            this.TxtServidor.TabIndex = 8;
            //
            // TxtDb
            //
            this.TxtDb.Location = new System.Drawing.Point(163, 363);
            this.TxtDb.Name = "TxtDb";
            this.TxtDb.ReadOnly = true;
            this.TxtDb.Size = new System.Drawing.Size(91, 20);
            this.TxtDb.TabIndex = 9;
            //
            // TxtNameform
            //
            this.TxtNameform.Location = new System.Drawing.Point(260, 363);
            this.TxtNameform.Name = "TxtNameform";
            this.TxtNameform.ReadOnly = true;
            this.TxtNameform.Size = new System.Drawing.Size(84, 20);
            this.TxtNameform.TabIndex = 10;
            //
            // TxtUsuario
            //
            this.TxtUsuario.Location = new System.Drawing.Point(412, 363);
            this.TxtUsuario.Name = "TxtUsuario";
            this.TxtUsuario.ReadOnly = true;
            this.TxtUsuario.Size = new System.Drawing.Size(84, 20);
            this.TxtUsuario.TabIndex = 11;
            //
            // TxtFechahora
            //
            this.TxtFechahora.Location = new System.Drawing.Point(502, 363);
            this.TxtFechahora.Name = "TxtFechahora";
            this.TxtFechahora.ReadOnly = true;
            this.TxtFechahora.Size = new System.Drawing.Size(136, 20);
            this.TxtFechahora.TabIndex = 12;
            this.TxtFechahora.Tag = "200";
            //
            // BtnImpimir
            //
            //this.BtnImpimir.BackgroundImage = global::ERP.Core.Properties.Resources.print;
            this.BtnImpimir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnImpimir.Location = new System.Drawing.Point(516, 298);
            this.BtnImpimir.Name = "BtnImpimir";
            this.BtnImpimir.Size = new System.Drawing.Size(54, 41);
            this.BtnImpimir.TabIndex = 13;
            this.BtnImpimir.UseVisualStyleBackColor = true;
            //
            // FrmGarantias
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(641, 388);
            this.Controls.Add(this.BtnImpimir);
            this.Controls.Add(this.TxtFechahora);
            this.Controls.Add(this.TxtUsuario);
            this.Controls.Add(this.TxtNameform);
            this.Controls.Add(this.TxtDb);
            this.Controls.Add(this.TxtServidor);
            this.Controls.Add(this.Label15);
            this.Controls.Add(this.Label14);
            this.Controls.Add(this.Label13);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.LblEmpresa);
            this.Controls.Add(this.GbxGarantia);
            this.Controls.Add(this.GbxAsegurado);
            this.Name = "FrmGarantias";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FrmGarantias";
            this.Load += new System.EventHandler(this.FrmGarantias_Load);
            this.GbxAsegurado.ResumeLayout(false);
            this.GbxAsegurado.PerformLayout();
            this.GbxGarantia.ResumeLayout(false);
            this.GbxGarantia.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
            //
            // Event wiring
            //
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            this.FechaHora.Tick += new System.EventHandler(this.FechaHora_Tick);
            this.BtnImpimir.Click += new System.EventHandler(this.BtnImpimir_Click);
        }

        #endregion

        private System.Windows.Forms.GroupBox GbxAsegurado;
        private System.Windows.Forms.Label Label10;
        private System.Windows.Forms.Label Label9;
        private System.Windows.Forms.Label Label8;
        private System.Windows.Forms.Label Label7;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.DateTimePicker DtpFechavencimiento;
        private System.Windows.Forms.TextBox TxtPorcentaje;
        private System.Windows.Forms.TextBox TxtRazonsocial;
        private System.Windows.Forms.TextBox TxtMatricula;
        private System.Windows.Forms.TextBox TxtPoliza;
        private System.Windows.Forms.TextBox TxtNitaseguradora;
        private System.Windows.Forms.GroupBox GbxGarantia;
        private System.Windows.Forms.TextBox TxtDescripcion;
        private System.Windows.Forms.TextBox TxtAvaluocomercial;
        private System.Windows.Forms.TextBox TxtAvaluocatastral;
        private System.Windows.Forms.TextBox TxtClaseGarantia;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label LblEmpresa;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.DateTimePicker DtpFechafinal;
        private System.Windows.Forms.DateTimePicker DtpFechainicial;
        private System.Windows.Forms.Label Label12;
        private System.Windows.Forms.Label Label11;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label13;
        private System.Windows.Forms.Label Label14;
        private System.Windows.Forms.Label Label15;
        private System.Windows.Forms.TextBox TxtServidor;
        private System.Windows.Forms.TextBox TxtDb;
        private System.Windows.Forms.TextBox TxtNameform;
        private System.Windows.Forms.TextBox TxtUsuario;
        private System.Windows.Forms.TextBox TxtFechahora;
        private System.Windows.Forms.TextBox TxtNumerocdat;
        private System.Windows.Forms.Label Label16;
        private System.Windows.Forms.Timer FechaHora;
        private System.Windows.Forms.Button BtnImpimir;
    }
}
