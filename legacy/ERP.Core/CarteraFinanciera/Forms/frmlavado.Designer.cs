namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmlavado
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && (components != null))
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.Label1 = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label2 = new System.Windows.Forms.Label();
            this.CbxTipoOperacion = new System.Windows.Forms.ComboBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.CbxDetalleOperacion = new System.Windows.Forms.ComboBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.TxtProducto = new System.Windows.Forms.TextBox();
            this.Label5 = new System.Windows.Forms.Label();
            this.GroupBox2 = new System.Windows.Forms.GroupBox();
            this.TxtApeCliente = new System.Windows.Forms.TextBox();
            this.Label20 = new System.Windows.Forms.Label();
            this.TxtTelCliente = new System.Windows.Forms.TextBox();
            this.Label10 = new System.Windows.Forms.Label();
            this.TxtDirCliente = new System.Windows.Forms.TextBox();
            this.Label9 = new System.Windows.Forms.Label();
            this.TxtIdCliente = new System.Windows.Forms.TextBox();
            this.Label8 = new System.Windows.Forms.Label();
            this.CbxTipoIdCliente = new System.Windows.Forms.ComboBox();
            this.Label7 = new System.Windows.Forms.Label();
            this.TxtNomCliente = new System.Windows.Forms.TextBox();
            this.Label6 = new System.Windows.Forms.Label();
            this.GroupBox3 = new System.Windows.Forms.GroupBox();
            this.TxtApeAsociado = new System.Windows.Forms.TextBox();
            this.Label19 = new System.Windows.Forms.Label();
            this.TxtOtrosIngAsociado = new System.Windows.Forms.TextBox();
            this.Label18 = new System.Windows.Forms.Label();
            this.TxtSalarioAsociado = new System.Windows.Forms.TextBox();
            this.Label17 = new System.Windows.Forms.Label();
            this.TxtActEcoAsociado = new System.Windows.Forms.TextBox();
            this.Label16 = new System.Windows.Forms.Label();
            this.TxtTelAsociado = new System.Windows.Forms.TextBox();
            this.Label11 = new System.Windows.Forms.Label();
            this.TxtDirAsociado = new System.Windows.Forms.TextBox();
            this.Label12 = new System.Windows.Forms.Label();
            this.TxtIdAsociado = new System.Windows.Forms.TextBox();
            this.Label13 = new System.Windows.Forms.Label();
            this.CbxTipoIdAsociado = new System.Windows.Forms.ComboBox();
            this.Label14 = new System.Windows.Forms.Label();
            this.TxtNomAsociado = new System.Windows.Forms.TextBox();
            this.Label15 = new System.Windows.Forms.Label();
            this.ChkRepiteInfo = new System.Windows.Forms.CheckBox();
            this.BtnImprimir = new System.Windows.Forms.Button();
            this.BtnActualizar = new System.Windows.Forms.Button();
            this.LblValorTransaccion = new System.Windows.Forms.Label();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.TxtObservaciones = new System.Windows.Forms.TextBox();
            this.Label21 = new System.Windows.Forms.Label();
            this.GroupBox2.SuspendLayout();
            this.GroupBox3.SuspendLayout();
            this.SuspendLayout();
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(183, 9);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(354, 16);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "DECLARACION DE OPERACIONES EN EFECTIVO";
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(-1, 22);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(723, 14);
            this.GroupBox1.TabIndex = 1;
            this.GroupBox1.TabStop = false;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(31, 47);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(95, 13);
            this.Label2.TabIndex = 2;
            this.Label2.Text = "Tipo de Operaci\u00f3n";
            //
            // CbxTipoOperacion
            //
            this.CbxTipoOperacion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxTipoOperacion.Enabled = false;
            this.CbxTipoOperacion.FormattingEnabled = true;
            this.CbxTipoOperacion.Items.AddRange(new object[] { "Retiro", "Deposito" });
            this.CbxTipoOperacion.Location = new System.Drawing.Point(132, 44);
            this.CbxTipoOperacion.Name = "CbxTipoOperacion";
            this.CbxTipoOperacion.Size = new System.Drawing.Size(121, 21);
            this.CbxTipoOperacion.TabIndex = 0;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(342, 47);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(118, 13);
            this.Label3.TabIndex = 4;
            this.Label3.Text = "Detalle de la Operaci\u00f3n";
            //
            // CbxDetalleOperacion
            //
            this.CbxDetalleOperacion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxDetalleOperacion.FormattingEnabled = true;
            this.CbxDetalleOperacion.Items.AddRange(new object[] { "", "Ahorros", "Cdats", "Pagos de Cartera" });
            this.CbxDetalleOperacion.Location = new System.Drawing.Point(466, 44);
            this.CbxDetalleOperacion.Name = "CbxDetalleOperacion";
            this.CbxDetalleOperacion.Size = new System.Drawing.Size(155, 21);
            this.CbxDetalleOperacion.TabIndex = 1;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(31, 76);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(96, 13);
            this.Label4.TabIndex = 6;
            this.Label4.Text = "Producto Afectado";
            //
            // TxtProducto
            //
            this.TxtProducto.Location = new System.Drawing.Point(132, 73);
            this.TxtProducto.Name = "TxtProducto";
            this.TxtProducto.Size = new System.Drawing.Size(121, 20);
            this.TxtProducto.TabIndex = 2;
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Location = new System.Drawing.Point(342, 76);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(93, 13);
            this.Label5.TabIndex = 8;
            this.Label5.Text = "Valor Transacci\u00f3n";
            //
            // GroupBox2
            //
            this.GroupBox2.Controls.Add(this.TxtApeCliente);
            this.GroupBox2.Controls.Add(this.Label20);
            this.GroupBox2.Controls.Add(this.TxtTelCliente);
            this.GroupBox2.Controls.Add(this.Label10);
            this.GroupBox2.Controls.Add(this.TxtDirCliente);
            this.GroupBox2.Controls.Add(this.Label9);
            this.GroupBox2.Controls.Add(this.TxtIdCliente);
            this.GroupBox2.Controls.Add(this.Label8);
            this.GroupBox2.Controls.Add(this.CbxTipoIdCliente);
            this.GroupBox2.Controls.Add(this.Label7);
            this.GroupBox2.Controls.Add(this.TxtNomCliente);
            this.GroupBox2.Controls.Add(this.Label6);
            this.GroupBox2.Location = new System.Drawing.Point(24, 263);
            this.GroupBox2.Name = "GroupBox2";
            this.GroupBox2.Size = new System.Drawing.Size(673, 106);
            this.GroupBox2.TabIndex = 6;
            this.GroupBox2.TabStop = false;
            this.GroupBox2.Text = "Datos de quien realiza la transacci\u00f3n";
            //
            // TxtApeCliente
            //
            this.TxtApeCliente.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.TxtApeCliente.Location = new System.Drawing.Point(421, 22);
            this.TxtApeCliente.Name = "TxtApeCliente";
            this.TxtApeCliente.Size = new System.Drawing.Size(224, 20);
            this.TxtApeCliente.TabIndex = 10;
            //
            // Label20
            //
            this.Label20.AutoSize = true;
            this.Label20.Location = new System.Drawing.Point(366, 25);
            this.Label20.Name = "Label20";
            this.Label20.Size = new System.Drawing.Size(49, 13);
            this.Label20.TabIndex = 9;
            this.Label20.Text = "Apellidos";
            //
            // TxtTelCliente
            //
            this.TxtTelCliente.Location = new System.Drawing.Point(500, 75);
            this.TxtTelCliente.Name = "TxtTelCliente";
            this.TxtTelCliente.Size = new System.Drawing.Size(145, 20);
            this.TxtTelCliente.TabIndex = 4;
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Location = new System.Drawing.Point(440, 78);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(49, 13);
            this.Label10.TabIndex = 8;
            this.Label10.Text = "Telefono";
            //
            // TxtDirCliente
            //
            this.TxtDirCliente.Location = new System.Drawing.Point(126, 75);
            this.TxtDirCliente.Name = "TxtDirCliente";
            this.TxtDirCliente.Size = new System.Drawing.Size(303, 20);
            this.TxtDirCliente.TabIndex = 3;
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Location = new System.Drawing.Point(27, 78);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(52, 13);
            this.Label9.TabIndex = 6;
            this.Label9.Text = "Direcci\u00f3n";
            //
            // TxtIdCliente
            //
            this.TxtIdCliente.Location = new System.Drawing.Point(500, 48);
            this.TxtIdCliente.Name = "TxtIdCliente";
            this.TxtIdCliente.Size = new System.Drawing.Size(145, 20);
            this.TxtIdCliente.TabIndex = 2;
            //
            // Label8
            //
            this.Label8.AutoSize = true;
            this.Label8.Location = new System.Drawing.Point(440, 51);
            this.Label8.Name = "Label8";
            this.Label8.Size = new System.Drawing.Size(59, 13);
            this.Label8.TabIndex = 4;
            this.Label8.Text = "N\u00famero Id.";
            //
            // CbxTipoIdCliente
            //
            this.CbxTipoIdCliente.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxTipoIdCliente.FormattingEnabled = true;
            this.CbxTipoIdCliente.Items.AddRange(new object[] { "", "T-Tarjeta Identidad", "C-Cedula Ciudadania", "E-Cedula Extranjeria", "N-Nit" });
            this.CbxTipoIdCliente.Location = new System.Drawing.Point(126, 48);
            this.CbxTipoIdCliente.Name = "CbxTipoIdCliente";
            this.CbxTipoIdCliente.Size = new System.Drawing.Size(146, 21);
            this.CbxTipoIdCliente.TabIndex = 1;
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Location = new System.Drawing.Point(27, 51);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(94, 13);
            this.Label7.TabIndex = 2;
            this.Label7.Text = "Tipo Identificaci\u00f3n";
            //
            // TxtNomCliente
            //
            this.TxtNomCliente.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.TxtNomCliente.Location = new System.Drawing.Point(126, 22);
            this.TxtNomCliente.Name = "TxtNomCliente";
            this.TxtNomCliente.Size = new System.Drawing.Size(221, 20);
            this.TxtNomCliente.TabIndex = 0;
            //
            // Label6
            //
            this.Label6.AutoSize = true;
            this.Label6.Location = new System.Drawing.Point(27, 25);
            this.Label6.Name = "Label6";
            this.Label6.Size = new System.Drawing.Size(44, 13);
            this.Label6.TabIndex = 0;
            this.Label6.Text = "Nombre";
            //
            // GroupBox3
            //
            this.GroupBox3.Controls.Add(this.TxtApeAsociado);
            this.GroupBox3.Controls.Add(this.Label19);
            this.GroupBox3.Controls.Add(this.TxtOtrosIngAsociado);
            this.GroupBox3.Controls.Add(this.Label18);
            this.GroupBox3.Controls.Add(this.TxtSalarioAsociado);
            this.GroupBox3.Controls.Add(this.Label17);
            this.GroupBox3.Controls.Add(this.TxtActEcoAsociado);
            this.GroupBox3.Controls.Add(this.Label16);
            this.GroupBox3.Controls.Add(this.TxtTelAsociado);
            this.GroupBox3.Controls.Add(this.Label11);
            this.GroupBox3.Controls.Add(this.TxtDirAsociado);
            this.GroupBox3.Controls.Add(this.Label12);
            this.GroupBox3.Controls.Add(this.TxtIdAsociado);
            this.GroupBox3.Controls.Add(this.Label13);
            this.GroupBox3.Controls.Add(this.CbxTipoIdAsociado);
            this.GroupBox3.Controls.Add(this.Label14);
            this.GroupBox3.Controls.Add(this.TxtNomAsociado);
            this.GroupBox3.Controls.Add(this.Label15);
            this.GroupBox3.Location = new System.Drawing.Point(24, 100);
            this.GroupBox3.Name = "GroupBox3";
            this.GroupBox3.Size = new System.Drawing.Size(673, 129);
            this.GroupBox3.TabIndex = 4;
            this.GroupBox3.TabStop = false;
            this.GroupBox3.Text = "Datos de la persona en nombre de la cual se realizo la operaci\u00f3n";
            //
            // TxtApeAsociado
            //
            this.TxtApeAsociado.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.TxtApeAsociado.Location = new System.Drawing.Point(421, 22);
            this.TxtApeAsociado.Name = "TxtApeAsociado";
            this.TxtApeAsociado.Size = new System.Drawing.Size(224, 20);
            this.TxtApeAsociado.TabIndex = 26;
            //
            // Label19
            //
            this.Label19.AutoSize = true;
            this.Label19.Location = new System.Drawing.Point(366, 25);
            this.Label19.Name = "Label19";
            this.Label19.Size = new System.Drawing.Size(49, 13);
            this.Label19.TabIndex = 25;
            this.Label19.Text = "Apellidos";
            //
            // TxtOtrosIngAsociado
            //
            this.TxtOtrosIngAsociado.Location = new System.Drawing.Point(533, 100);
            this.TxtOtrosIngAsociado.Name = "TxtOtrosIngAsociado";
            this.TxtOtrosIngAsociado.Size = new System.Drawing.Size(112, 20);
            this.TxtOtrosIngAsociado.TabIndex = 7;
            this.TxtOtrosIngAsociado.Text = "0";
            this.TxtOtrosIngAsociado.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label18
            //
            this.Label18.AutoSize = true;
            this.Label18.Location = new System.Drawing.Point(457, 103);
            this.Label18.Name = "Label18";
            this.Label18.Size = new System.Drawing.Size(75, 13);
            this.Label18.TabIndex = 24;
            this.Label18.Text = "Otros Ingresos";
            //
            // TxtSalarioAsociado
            //
            this.TxtSalarioAsociado.Location = new System.Drawing.Point(353, 100);
            this.TxtSalarioAsociado.Name = "TxtSalarioAsociado";
            this.TxtSalarioAsociado.Size = new System.Drawing.Size(98, 20);
            this.TxtSalarioAsociado.TabIndex = 6;
            this.TxtSalarioAsociado.Text = "0";
            this.TxtSalarioAsociado.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label17
            //
            this.Label17.AutoSize = true;
            this.Label17.Location = new System.Drawing.Point(308, 103);
            this.Label17.Name = "Label17";
            this.Label17.Size = new System.Drawing.Size(39, 13);
            this.Label17.TabIndex = 22;
            this.Label17.Text = "Salario";
            //
            // TxtActEcoAsociado
            //
            this.TxtActEcoAsociado.Location = new System.Drawing.Point(126, 100);
            this.TxtActEcoAsociado.Name = "TxtActEcoAsociado";
            this.TxtActEcoAsociado.Size = new System.Drawing.Size(177, 20);
            this.TxtActEcoAsociado.TabIndex = 5;
            //
            // Label16
            //
            this.Label16.AutoSize = true;
            this.Label16.Location = new System.Drawing.Point(16, 103);
            this.Label16.Name = "Label16";
            this.Label16.Size = new System.Drawing.Size(107, 13);
            this.Label16.TabIndex = 20;
            this.Label16.Text = "Actividad Economica";
            //
            // TxtTelAsociado
            //
            this.TxtTelAsociado.Location = new System.Drawing.Point(533, 75);
            this.TxtTelAsociado.Name = "TxtTelAsociado";
            this.TxtTelAsociado.Size = new System.Drawing.Size(112, 20);
            this.TxtTelAsociado.TabIndex = 4;
            //
            // Label11
            //
            this.Label11.AutoSize = true;
            this.Label11.Location = new System.Drawing.Point(457, 78);
            this.Label11.Name = "Label11";
            this.Label11.Size = new System.Drawing.Size(49, 13);
            this.Label11.TabIndex = 18;
            this.Label11.Text = "Telefono";
            //
            // TxtDirAsociado
            //
            this.TxtDirAsociado.Location = new System.Drawing.Point(126, 75);
            this.TxtDirAsociado.Name = "TxtDirAsociado";
            this.TxtDirAsociado.Size = new System.Drawing.Size(325, 20);
            this.TxtDirAsociado.TabIndex = 3;
            //
            // Label12
            //
            this.Label12.AutoSize = true;
            this.Label12.Location = new System.Drawing.Point(16, 78);
            this.Label12.Name = "Label12";
            this.Label12.Size = new System.Drawing.Size(52, 13);
            this.Label12.TabIndex = 16;
            this.Label12.Text = "Direcci\u00f3n";
            //
            // TxtIdAsociado
            //
            this.TxtIdAsociado.Enabled = false;
            this.TxtIdAsociado.Location = new System.Drawing.Point(533, 48);
            this.TxtIdAsociado.Name = "TxtIdAsociado";
            this.TxtIdAsociado.Size = new System.Drawing.Size(112, 20);
            this.TxtIdAsociado.TabIndex = 2;
            //
            // Label13
            //
            this.Label13.AutoSize = true;
            this.Label13.Location = new System.Drawing.Point(457, 51);
            this.Label13.Name = "Label13";
            this.Label13.Size = new System.Drawing.Size(59, 13);
            this.Label13.TabIndex = 14;
            this.Label13.Text = "N\u00famero Id.";
            //
            // CbxTipoIdAsociado
            //
            this.CbxTipoIdAsociado.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CbxTipoIdAsociado.FormattingEnabled = true;
            this.CbxTipoIdAsociado.Items.AddRange(new object[] { "", "T-Tarjeta Identidad", "C-Cedula Ciudadania", "E-Cedula Extranjeria", "N-Nit" });
            this.CbxTipoIdAsociado.Location = new System.Drawing.Point(126, 48);
            this.CbxTipoIdAsociado.Name = "CbxTipoIdAsociado";
            this.CbxTipoIdAsociado.Size = new System.Drawing.Size(146, 21);
            this.CbxTipoIdAsociado.TabIndex = 1;
            //
            // Label14
            //
            this.Label14.AutoSize = true;
            this.Label14.Location = new System.Drawing.Point(16, 51);
            this.Label14.Name = "Label14";
            this.Label14.Size = new System.Drawing.Size(94, 13);
            this.Label14.TabIndex = 12;
            this.Label14.Text = "Tipo Identificaci\u00f3n";
            //
            // TxtNomAsociado
            //
            this.TxtNomAsociado.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.TxtNomAsociado.Location = new System.Drawing.Point(126, 22);
            this.TxtNomAsociado.Name = "TxtNomAsociado";
            this.TxtNomAsociado.Size = new System.Drawing.Size(221, 20);
            this.TxtNomAsociado.TabIndex = 0;
            //
            // Label15
            //
            this.Label15.AutoSize = true;
            this.Label15.Location = new System.Drawing.Point(16, 25);
            this.Label15.Name = "Label15";
            this.Label15.Size = new System.Drawing.Size(44, 13);
            this.Label15.TabIndex = 10;
            this.Label15.Text = "Nombre";
            //
            // ChkRepiteInfo
            //
            this.ChkRepiteInfo.AutoSize = true;
            this.ChkRepiteInfo.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ChkRepiteInfo.Location = new System.Drawing.Point(218, 238);
            this.ChkRepiteInfo.Name = "ChkRepiteInfo";
            this.ChkRepiteInfo.Size = new System.Drawing.Size(285, 17);
            this.ChkRepiteInfo.TabIndex = 5;
            this.ChkRepiteInfo.Text = "La persona que realiza la operaci\u00f3n es el mismo titular?";
            this.ChkRepiteInfo.UseVisualStyleBackColor = true;
            this.ChkRepiteInfo.CheckedChanged += new System.EventHandler(this.ChkRepiteInfo_CheckedChanged);
            //
            // BtnImprimir
            //
            //this.BtnImprimir.BackgroundImage = global::ERP.Core.Properties.Resources.print;
            this.BtnImprimir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnImprimir.Location = new System.Drawing.Point(619, 231);
            this.BtnImprimir.Name = "BtnImprimir";
            this.BtnImprimir.Size = new System.Drawing.Size(38, 35);
            this.BtnImprimir.TabIndex = 8;
            this.ToolTip1.SetToolTip(this.BtnImprimir, "Imprimir Formato");
            this.BtnImprimir.UseVisualStyleBackColor = true;
            this.BtnImprimir.Click += new System.EventHandler(this.BtnImprimir_Click);
            //
            // BtnActualizar
            //
            //this.BtnActualizar.BackgroundImage = global::ERP.Core.Properties.Resources.replace2;
            this.BtnActualizar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnActualizar.Location = new System.Drawing.Point(578, 231);
            this.BtnActualizar.Name = "BtnActualizar";
            this.BtnActualizar.Size = new System.Drawing.Size(38, 35);
            this.BtnActualizar.TabIndex = 7;
            this.ToolTip1.SetToolTip(this.BtnActualizar, "Actualizar Datos Asociado");
            this.BtnActualizar.UseVisualStyleBackColor = true;
            this.BtnActualizar.Click += new System.EventHandler(this.BtnActualizar_Click);
            //
            // LblValorTransaccion
            //
            this.LblValorTransaccion.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblValorTransaccion.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblValorTransaccion.ForeColor = System.Drawing.Color.Maroon;
            this.LblValorTransaccion.Location = new System.Drawing.Point(466, 70);
            this.LblValorTransaccion.Name = "LblValorTransaccion";
            this.LblValorTransaccion.Size = new System.Drawing.Size(155, 23);
            this.LblValorTransaccion.TabIndex = 9;
            this.LblValorTransaccion.Text = "0";
            this.LblValorTransaccion.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(659, 231);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 10;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir");
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // TxtObservaciones
            //
            this.TxtObservaciones.Location = new System.Drawing.Point(40, 389);
            this.TxtObservaciones.Multiline = true;
            this.TxtObservaciones.Name = "TxtObservaciones";
            this.TxtObservaciones.Size = new System.Drawing.Size(639, 48);
            this.TxtObservaciones.TabIndex = 11;
            //
            // Label21
            //
            this.Label21.AutoSize = true;
            this.Label21.Location = new System.Drawing.Point(40, 373);
            this.Label21.Name = "Label21";
            this.Label21.Size = new System.Drawing.Size(78, 13);
            this.Label21.TabIndex = 12;
            this.Label21.Text = "Observaciones";
            //
            // frmlavado
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(721, 449);
            this.ControlBox = false;
            this.Controls.Add(this.Label21);
            this.Controls.Add(this.TxtObservaciones);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.LblValorTransaccion);
            this.Controls.Add(this.BtnActualizar);
            this.Controls.Add(this.BtnImprimir);
            this.Controls.Add(this.ChkRepiteInfo);
            this.Controls.Add(this.GroupBox3);
            this.Controls.Add(this.GroupBox2);
            this.Controls.Add(this.Label5);
            this.Controls.Add(this.TxtProducto);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.CbxDetalleOperacion);
            this.Controls.Add(this.Label3);
            this.Controls.Add(this.CbxTipoOperacion);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.GroupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmlavado";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.GroupBox2.ResumeLayout(false);
            this.GroupBox2.PerformLayout();
            this.GroupBox3.ResumeLayout(false);
            this.GroupBox3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.ComboBox CbxTipoOperacion;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.ComboBox CbxDetalleOperacion;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.TextBox TxtProducto;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.GroupBox GroupBox2;
        private System.Windows.Forms.TextBox TxtApeCliente;
        private System.Windows.Forms.Label Label20;
        private System.Windows.Forms.TextBox TxtTelCliente;
        private System.Windows.Forms.Label Label10;
        private System.Windows.Forms.TextBox TxtDirCliente;
        private System.Windows.Forms.Label Label9;
        private System.Windows.Forms.TextBox TxtIdCliente;
        private System.Windows.Forms.Label Label8;
        private System.Windows.Forms.ComboBox CbxTipoIdCliente;
        private System.Windows.Forms.Label Label7;
        private System.Windows.Forms.TextBox TxtNomCliente;
        private System.Windows.Forms.Label Label6;
        private System.Windows.Forms.GroupBox GroupBox3;
        private System.Windows.Forms.TextBox TxtApeAsociado;
        private System.Windows.Forms.Label Label19;
        private System.Windows.Forms.TextBox TxtOtrosIngAsociado;
        private System.Windows.Forms.Label Label18;
        private System.Windows.Forms.TextBox TxtSalarioAsociado;
        private System.Windows.Forms.Label Label17;
        private System.Windows.Forms.TextBox TxtActEcoAsociado;
        private System.Windows.Forms.Label Label16;
        private System.Windows.Forms.TextBox TxtTelAsociado;
        private System.Windows.Forms.Label Label11;
        private System.Windows.Forms.TextBox TxtDirAsociado;
        private System.Windows.Forms.Label Label12;
        private System.Windows.Forms.TextBox TxtIdAsociado;
        private System.Windows.Forms.Label Label13;
        private System.Windows.Forms.ComboBox CbxTipoIdAsociado;
        private System.Windows.Forms.Label Label14;
        private System.Windows.Forms.TextBox TxtNomAsociado;
        private System.Windows.Forms.Label Label15;
        private System.Windows.Forms.CheckBox ChkRepiteInfo;
        private System.Windows.Forms.Button BtnImprimir;
        private System.Windows.Forms.Button BtnActualizar;
        private System.Windows.Forms.Label LblValorTransaccion;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.TextBox TxtObservaciones;
        private System.Windows.Forms.Label Label21;
    }
}
