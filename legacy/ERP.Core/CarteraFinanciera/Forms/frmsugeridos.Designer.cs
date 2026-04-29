namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmsugeridos
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmsugeridos));
            this.RbtMonto = new System.Windows.Forms.RadioButton();
            this.RbtPlazo = new System.Windows.Forms.RadioButton();
            this.Label1 = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label3 = new System.Windows.Forms.Label();
            this.LblSugerido = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.TxtTasaInt = new System.Windows.Forms.TextBox();
            this.TxtCuota = new System.Windows.Forms.TextBox();
            this.CbxPeriodicidad = new System.Windows.Forms.ComboBox();
            this.TxtSugerido = new System.Windows.Forms.TextBox();
            this.LblResultado = new System.Windows.Forms.Label();
            this.LblValorSugerido = new System.Windows.Forms.Label();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.GroupBox3 = new System.Windows.Forms.GroupBox();
            this.LblDetalleCampos = new System.Windows.Forms.Label();
            this.GroupBox4 = new System.Windows.Forms.GroupBox();
            this.CbxClaCuota = new System.Windows.Forms.ComboBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.LblLabel = new System.Windows.Forms.Label();
            this.GroupBox4.SuspendLayout();
            this.SuspendLayout();
            //
            // RbtMonto
            //
            this.RbtMonto.AutoSize = true;
            this.RbtMonto.Checked = true;
            this.RbtMonto.Font = new System.Drawing.Font("Times New Roman", 9.75f, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.RbtMonto.Location = new System.Drawing.Point(107, 15);
            this.RbtMonto.Name = "RbtMonto";
            this.RbtMonto.Size = new System.Drawing.Size(116, 20);
            this.RbtMonto.TabIndex = 0;
            this.RbtMonto.TabStop = true;
            this.RbtMonto.Text = "Monto Sugerido";
            this.RbtMonto.UseVisualStyleBackColor = true;
            this.RbtMonto.CheckedChanged += new System.EventHandler(this.RbtMonto_CheckedChanged);
            //
            // RbtPlazo
            //
            this.RbtPlazo.AutoSize = true;
            this.RbtPlazo.Font = new System.Drawing.Font("Times New Roman", 9.75f, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.RbtPlazo.Location = new System.Drawing.Point(284, 15);
            this.RbtPlazo.Name = "RbtPlazo";
            this.RbtPlazo.Size = new System.Drawing.Size(110, 20);
            this.RbtPlazo.TabIndex = 1;
            this.RbtPlazo.Text = "Plazo Sugerido";
            this.RbtPlazo.UseVisualStyleBackColor = true;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Times New Roman", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(101, 170);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(81, 17);
            this.Label1.TabIndex = 3;
            this.Label1.Text = "Tasa Interes";
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Font = new System.Drawing.Font("Times New Roman", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label2.Location = new System.Drawing.Point(101, 202);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(78, 17);
            this.Label2.TabIndex = 4;
            this.Label2.Text = "Valor Cuota";
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Font = new System.Drawing.Font("Times New Roman", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label3.Location = new System.Drawing.Point(101, 136);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(80, 17);
            this.Label3.TabIndex = 5;
            this.Label3.Text = "Periodicidad";
            //
            // LblSugerido
            //
            this.LblSugerido.AutoSize = true;
            this.LblSugerido.Font = new System.Drawing.Font("Times New Roman", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblSugerido.Location = new System.Drawing.Point(101, 234);
            this.LblSugerido.Name = "LblSugerido";
            this.LblSugerido.Size = new System.Drawing.Size(100, 17);
            this.LblSugerido.TabIndex = 6;
            this.LblSugerido.Text = "Plazo Solicitado";
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Font = new System.Drawing.Font("Times New Roman", 15.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label5.ForeColor = System.Drawing.Color.SteelBlue;
            this.Label5.Location = new System.Drawing.Point(133, 9);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(227, 24);
            this.Label5.TabIndex = 7;
            this.Label5.Text = "Monto / Plazo Sugeridos";
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(-2, 32);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(498, 11);
            this.GroupBox1.TabIndex = 8;
            this.GroupBox1.TabStop = false;
            //
            // TxtTasaInt
            //
            this.TxtTasaInt.Font = new System.Drawing.Font("Times New Roman", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtTasaInt.ForeColor = System.Drawing.Color.Maroon;
            this.TxtTasaInt.Location = new System.Drawing.Point(252, 165);
            this.TxtTasaInt.Name = "TxtTasaInt";
            this.TxtTasaInt.Size = new System.Drawing.Size(139, 26);
            this.TxtTasaInt.TabIndex = 2;
            this.TxtTasaInt.Text = "0";
            this.TxtTasaInt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtTasaInt.GotFocus += new System.EventHandler(this.TxtTasaInt_GotFocus);
            this.TxtTasaInt.LostFocus += new System.EventHandler(this.TxtTasaInt_LostFocus);
            //
            // TxtCuota
            //
            this.TxtCuota.Font = new System.Drawing.Font("Times New Roman", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtCuota.ForeColor = System.Drawing.Color.Maroon;
            this.TxtCuota.Location = new System.Drawing.Point(252, 197);
            this.TxtCuota.Name = "TxtCuota";
            this.TxtCuota.Size = new System.Drawing.Size(139, 26);
            this.TxtCuota.TabIndex = 3;
            this.TxtCuota.Text = "0";
            this.TxtCuota.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtCuota.GotFocus += new System.EventHandler(this.TxtCuota_GotFocus);
            this.TxtCuota.LostFocus += new System.EventHandler(this.TxtCuota_LostFocus);
            //
            // CbxPeriodicidad
            //
            this.CbxPeriodicidad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxPeriodicidad.Font = new System.Drawing.Font("Times New Roman", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CbxPeriodicidad.FormattingEnabled = true;
            this.CbxPeriodicidad.Items.AddRange(new object[] { "", "Mensual", "Quincenal", "Decadal", "Semanal", "Diaria" });
            this.CbxPeriodicidad.Location = new System.Drawing.Point(253, 131);
            this.CbxPeriodicidad.Name = "CbxPeriodicidad";
            this.CbxPeriodicidad.Size = new System.Drawing.Size(138, 27);
            this.CbxPeriodicidad.TabIndex = 1;
            this.CbxPeriodicidad.GotFocus += new System.EventHandler(this.CbxPeriodicidad_GotFocus);
            //
            // TxtSugerido
            //
            this.TxtSugerido.Font = new System.Drawing.Font("Times New Roman", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtSugerido.ForeColor = System.Drawing.Color.Maroon;
            this.TxtSugerido.Location = new System.Drawing.Point(252, 229);
            this.TxtSugerido.Name = "TxtSugerido";
            this.TxtSugerido.Size = new System.Drawing.Size(139, 26);
            this.TxtSugerido.TabIndex = 4;
            this.TxtSugerido.Text = "0";
            this.TxtSugerido.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.TxtSugerido.GotFocus += new System.EventHandler(this.TxtSugerido_GotFocus);
            this.TxtSugerido.LostFocus += new System.EventHandler(this.TxtSugerido_LostFocus);
            //
            // LblResultado
            //
            this.LblResultado.AutoSize = true;
            this.LblResultado.Font = new System.Drawing.Font("Times New Roman", 14.25f, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblResultado.Location = new System.Drawing.Point(101, 270);
            this.LblResultado.Name = "LblResultado";
            this.LblResultado.Size = new System.Drawing.Size(138, 22);
            this.LblResultado.TabIndex = 14;
            this.LblResultado.Text = "Monto Sugerido";
            //
            // LblValorSugerido
            //
            this.LblValorSugerido.BackColor = System.Drawing.Color.DarkGray;
            this.LblValorSugerido.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblValorSugerido.Font = new System.Drawing.Font("Times New Roman", 12.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblValorSugerido.ForeColor = System.Drawing.Color.Chocolate;
            this.LblValorSugerido.Location = new System.Drawing.Point(252, 267);
            this.LblValorSugerido.Name = "LblValorSugerido";
            this.LblValorSugerido.Size = new System.Drawing.Size(139, 25);
            this.LblValorSugerido.TabIndex = 5;
            this.LblValorSugerido.Text = "0";
            this.LblValorSugerido.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // opcion
            //
            this.opcion.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.opcion.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.opcion.Enabled_Eliminar = true;
            this.opcion.Enabled_Grabar = true;
            this.opcion.Enabled_Salir = true;
            this.opcion.Estilo_Barra = 1;
            this.opcion.Imagen_Atras = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Atras")));
            this.opcion.Imagen_Eliminar = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Eliminar")));
            this.opcion.Imagen_Grabar = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Grabar")));
            this.opcion.Imagen_Primero = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Primero")));
            this.opcion.Imagen_Salir = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Salir")));
            this.opcion.Imagen_Siguiente = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Siguiente")));
            this.opcion.Imagen_Ultimo = ((System.Drawing.Image)(resources.GetObject("opcion.Imagen_Ultimo")));
            this.opcion.Location = new System.Drawing.Point(5, 7);
            this.opcion.Margin = new System.Windows.Forms.Padding(4);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(27, 27);
            this.opcion.TabIndex = 6;
            this.opcion.Tooltip_Boton1 = "Salir";
            this.opcion.Tooltip_Boton2 = "Guardar";
            this.opcion.Tooltip_Boton3 = "Eliminar";
            this.opcion.Tooltip_Boton4 = "Primero";
            this.opcion.Tooltip_Boton5 = "Anterior";
            this.opcion.Tooltip_Boton6 = "Siguiente";
            this.opcion.Tooltip_Boton7 = "Ultimo";
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            //
            // GroupBox3
            //
            this.GroupBox3.Location = new System.Drawing.Point(-3, 296);
            this.GroupBox3.Name = "GroupBox3";
            this.GroupBox3.Size = new System.Drawing.Size(498, 11);
            this.GroupBox3.TabIndex = 16;
            this.GroupBox3.TabStop = false;
            //
            // LblDetalleCampos
            //
            this.LblDetalleCampos.AutoSize = true;
            this.LblDetalleCampos.Location = new System.Drawing.Point(12, 311);
            this.LblDetalleCampos.Name = "LblDetalleCampos";
            this.LblDetalleCampos.Size = new System.Drawing.Size(0, 15);
            this.LblDetalleCampos.TabIndex = 17;
            //
            // GroupBox4
            //
            this.GroupBox4.Controls.Add(this.CbxClaCuota);
            this.GroupBox4.Controls.Add(this.Label6);
            this.GroupBox4.Controls.Add(this.RbtMonto);
            this.GroupBox4.Controls.Add(this.RbtPlazo);
            this.GroupBox4.Location = new System.Drawing.Point(-3, 36);
            this.GroupBox4.Name = "GroupBox4";
            this.GroupBox4.Size = new System.Drawing.Size(498, 87);
            this.GroupBox4.TabIndex = 0;
            this.GroupBox4.TabStop = false;
            //
            // CbxClaCuota
            //
            this.CbxClaCuota.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxClaCuota.FormattingEnabled = true;
            this.CbxClaCuota.Items.AddRange(new object[] { "", "Fija", "Variable" });
            this.CbxClaCuota.Location = new System.Drawing.Point(196, 53);
            this.CbxClaCuota.Name = "CbxClaCuota";
            this.CbxClaCuota.Size = new System.Drawing.Size(121, 23);
            this.CbxClaCuota.TabIndex = 2;
            this.CbxClaCuota.LostFocus += new System.EventHandler(this.CbxClaCuota_LostFocus);
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(104, 56);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(71, 15);
            this.Label6.TabIndex = 0;
            this.Label6.Text = "Clase Cuota";
            //
            // LblLabel
            //
            this.LblLabel.AutoSize = true;
            this.LblLabel.Font = new System.Drawing.Font("Times New Roman", 14.25f, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblLabel.Location = new System.Drawing.Point(392, 270);
            this.LblLabel.Name = "LblLabel";
            this.LblLabel.Size = new System.Drawing.Size(57, 22);
            this.LblLabel.TabIndex = 385;
            this.LblLabel.Text = "Meses";
            //
            // frmsugeridos
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7.0f, 15.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(493, 339);
            this.ControlBox = false;
            this.Controls.Add(this.LblLabel);
            this.Controls.Add(this.LblDetalleCampos);
            this.Controls.Add(this.GroupBox3);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.LblValorSugerido);
            this.Controls.Add(this.LblResultado);
            this.Controls.Add(this.TxtSugerido);
            this.Controls.Add(this.CbxPeriodicidad);
            this.Controls.Add(this.TxtCuota);
            this.Controls.Add(this.TxtTasaInt);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.LblSugerido);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.GroupBox4);
            this.Font = new System.Drawing.Font("Times New Roman", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.KeyPreview = true;
            this.Name = "frmsugeridos";
            this.GroupBox4.ResumeLayout(false);
            this.GroupBox4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.RadioButton RbtMonto;
        private System.Windows.Forms.RadioButton RbtPlazo;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Label LblSugerido;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.TextBox TxtTasaInt;
        private System.Windows.Forms.TextBox TxtCuota;
        private System.Windows.Forms.ComboBox CbxPeriodicidad;
        private System.Windows.Forms.TextBox TxtSugerido;
        private System.Windows.Forms.Label LblResultado;
        private System.Windows.Forms.Label LblValorSugerido;
        private ERP.Core.Compartido.Controles.SasToolBar opcion;
        private System.Windows.Forms.GroupBox GroupBox3;
        private System.Windows.Forms.Label LblDetalleCampos;
        private System.Windows.Forms.GroupBox GroupBox4;
        private System.Windows.Forms.ComboBox CbxClaCuota;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.Label LblLabel;
    }
}
