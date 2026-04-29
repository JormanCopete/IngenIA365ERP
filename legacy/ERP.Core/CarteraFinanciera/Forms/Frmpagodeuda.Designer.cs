namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class Frmpagodeuda
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
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Frmpagodeuda));
            this.LblNomEmpresa = new System.Windows.Forms.Label();
            this.Lblperiodo = new System.Windows.Forms.Label();
            this.Label13 = new System.Windows.Forms.Label();
            this.TxtNomForma = new System.Windows.Forms.TextBox();
            this.TxtfechaSys = new System.Windows.Forms.TextBox();
            this.Txtusuario = new System.Windows.Forms.TextBox();
            this.TxtBd = new System.Windows.Forms.TextBox();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.Label34 = new System.Windows.Forms.Label();
            this.Label40 = new System.Windows.Forms.Label();
            this.Label32 = new System.Windows.Forms.Label();
            this.DtgDatos = new System.Windows.Forms.DataGridView();
            this.ClmLinea = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmPagare = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDescripcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmtasa = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmSaldo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Clmmora = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmInteres = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmOtros = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmCapital = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmTotal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Menu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.VerCuotasPendientesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.EliminarPagoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.LblNombre = new System.Windows.Forms.Label();
            this.LblTotalPago = new System.Windows.Forms.Label();
            this.LblCapital = new System.Windows.Forms.Label();
            this.LblOtros = new System.Windows.Forms.Label();
            this.LblInteres = new System.Windows.Forms.Label();
            this.LblMora = new System.Windows.Forms.Label();
            this.LblSaldo = new System.Windows.Forms.Label();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.cmbsalir = new System.Windows.Forms.Button();
            this.ToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.CmbGuardar = new System.Windows.Forms.Button();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.GroupBox2 = new System.Windows.Forms.GroupBox();
            this.GroupBox3 = new System.Windows.Forms.GroupBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.Label10 = new System.Windows.Forms.Label();
            this.TxtInteres = new System.Windows.Forms.TextBox();
            this.Label9 = new System.Windows.Forms.Label();
            this.TxtValor = new System.Windows.Forms.TextBox();
            this.TxtNumero = new System.Windows.Forms.TextBox();
            this.TxtLinea = new System.Windows.Forms.TextBox();
            this.Label16 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DtgDatos)).BeginInit();
            this.Menu.SuspendLayout();
            this.SuspendLayout();
            //
            // LblNomEmpresa
            //
            this.LblNomEmpresa.BackColor = System.Drawing.SystemColors.Control;
            this.LblNomEmpresa.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.LblNomEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomEmpresa.Location = new System.Drawing.Point(65, 5);
            this.LblNomEmpresa.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.LblNomEmpresa.Name = "LblNomEmpresa";
            this.LblNomEmpresa.Size = new System.Drawing.Size(500, 42);
            this.LblNomEmpresa.TabIndex = 321;
            this.LblNomEmpresa.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // Lblperiodo
            //
            this.Lblperiodo.BackColor = System.Drawing.SystemColors.Control;
            this.Lblperiodo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold);
            this.Lblperiodo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Lblperiodo.Location = new System.Drawing.Point(633, 13);
            this.Lblperiodo.Name = "Lblperiodo";
            this.Lblperiodo.Size = new System.Drawing.Size(72, 16);
            this.Lblperiodo.TabIndex = 323;
            //
            // Label13
            //
            this.Label13.AutoSize = true;
            this.Label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label13.Location = new System.Drawing.Point(565, 13);
            this.Label13.Name = "Label13";
            this.Label13.Size = new System.Drawing.Size(69, 15);
            this.Label13.TabIndex = 322;
            this.Label13.Text = "PERIODO";
            //
            // TxtNomForma
            //
            this.TxtNomForma.BackColor = System.Drawing.SystemColors.Menu;
            this.TxtNomForma.Enabled = false;
            this.TxtNomForma.Location = new System.Drawing.Point(302, 358);
            this.TxtNomForma.MaxLength = 22;
            this.TxtNomForma.Name = "TxtNomForma";
            this.TxtNomForma.Size = new System.Drawing.Size(143, 20);
            this.TxtNomForma.TabIndex = 331;
            //
            // TxtfechaSys
            //
            this.TxtfechaSys.BackColor = System.Drawing.SystemColors.Menu;
            this.TxtfechaSys.Enabled = false;
            this.TxtfechaSys.Location = new System.Drawing.Point(604, 358);
            this.TxtfechaSys.Name = "TxtfechaSys";
            this.TxtfechaSys.Size = new System.Drawing.Size(87, 20);
            this.TxtfechaSys.TabIndex = 330;
            //
            // Txtusuario
            //
            this.Txtusuario.BackColor = System.Drawing.SystemColors.Menu;
            this.Txtusuario.Enabled = false;
            this.Txtusuario.Location = new System.Drawing.Point(510, 358);
            this.Txtusuario.MaxLength = 10;
            this.Txtusuario.Name = "Txtusuario";
            this.Txtusuario.Size = new System.Drawing.Size(88, 20);
            this.Txtusuario.TabIndex = 326;
            //
            // TxtBd
            //
            this.TxtBd.BackColor = System.Drawing.SystemColors.Menu;
            this.TxtBd.Enabled = false;
            this.TxtBd.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TxtBd.Location = new System.Drawing.Point(210, 358);
            this.TxtBd.MaxLength = 20;
            this.TxtBd.Name = "TxtBd";
            this.TxtBd.Size = new System.Drawing.Size(89, 20);
            this.TxtBd.TabIndex = 329;
            //
            // txtServer
            //
            this.txtServer.BackColor = System.Drawing.SystemColors.Menu;
            this.txtServer.Enabled = false;
            this.txtServer.Location = new System.Drawing.Point(68, 358);
            this.txtServer.MaxLength = 10;
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(107, 20);
            this.txtServer.TabIndex = 327;
            //
            // Label34
            //
            this.Label34.Enabled = false;
            this.Label34.Location = new System.Drawing.Point(448, 361);
            this.Label34.Name = "Label34";
            this.Label34.Size = new System.Drawing.Size(62, 15);
            this.Label34.TabIndex = 325;
            this.Label34.Text = "Usuario";
            //
            // Label40
            //
            this.Label40.Location = new System.Drawing.Point(187, 361);
            this.Label40.Name = "Label40";
            this.Label40.Size = new System.Drawing.Size(32, 15);
            this.Label40.TabIndex = 328;
            this.Label40.Text = "BD";
            //
            // Label32
            //
            this.Label32.Location = new System.Drawing.Point(8, 361);
            this.Label32.Name = "Label32";
            this.Label32.Size = new System.Drawing.Size(59, 16);
            this.Label32.TabIndex = 324;
            this.Label32.Text = "Servidor";
            //
            // DtgDatos
            //
            this.DtgDatos.AllowUserToAddRows = false;
            this.DtgDatos.AllowUserToDeleteRows = false;
            this.DtgDatos.AllowUserToResizeColumns = false;
            this.DtgDatos.AllowUserToResizeRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DtgDatos.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DtgDatos.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DtgDatos.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.ClmLinea, this.ClmPagare, this.ClmDescripcion, this.clmtasa, this.ClmSaldo, this.Clmmora, this.ClmInteres, this.ClmOtros, this.ClmCapital, this.ClmTotal });
            this.DtgDatos.ContextMenuStrip = this.Menu;
            DataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle11.BackColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle11.ForeColor = System.Drawing.SystemColors.ControlText;
            DataGridViewCellStyle11.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle11.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle11.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.DtgDatos.DefaultCellStyle = DataGridViewCellStyle11;
            this.DtgDatos.EnableHeadersVisualStyles = false;
            this.DtgDatos.Location = new System.Drawing.Point(7, 94);
            this.DtgDatos.Name = "DtgDatos";
            DataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle12.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle12.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle12.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle12.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle12.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DtgDatos.RowHeadersDefaultCellStyle = DataGridViewCellStyle12;
            this.DtgDatos.RowHeadersVisible = false;
            this.DtgDatos.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DtgDatos.Size = new System.Drawing.Size(720, 150);
            this.DtgDatos.StandardTab = true;
            this.DtgDatos.TabIndex = 332;
            this.DtgDatos.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.DtgDatos_CellMouseDoubleClick);
            this.DtgDatos.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DtgDatos_MouseUp);
            //
            // ClmLinea
            //
            this.ClmLinea.DataPropertyName = "linea";
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ClmLinea.DefaultCellStyle = DataGridViewCellStyle2;
            this.ClmLinea.HeaderText = "Linea";
            this.ClmLinea.Name = "ClmLinea";
            this.ClmLinea.ReadOnly = true;
            this.ClmLinea.Width = 40;
            //
            // ClmPagare
            //
            this.ClmPagare.DataPropertyName = "Numero";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.ClmPagare.DefaultCellStyle = DataGridViewCellStyle3;
            this.ClmPagare.HeaderText = "Obligaci\u00f2n";
            this.ClmPagare.Name = "ClmPagare";
            this.ClmPagare.ReadOnly = true;
            this.ClmPagare.Width = 70;
            //
            // ClmDescripcion
            //
            this.ClmDescripcion.DataPropertyName = "Descripcion";
            this.ClmDescripcion.HeaderText = "Descripcion";
            this.ClmDescripcion.Name = "ClmDescripcion";
            this.ClmDescripcion.ReadOnly = true;
            this.ClmDescripcion.Width = 130;
            //
            // clmtasa
            //
            this.clmtasa.DataPropertyName = "tasa";
            DataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle4.Format = "N2";
            DataGridViewCellStyle4.NullValue = "0";
            this.clmtasa.DefaultCellStyle = DataGridViewCellStyle4;
            this.clmtasa.HeaderText = "Tasa";
            this.clmtasa.Name = "clmtasa";
            this.clmtasa.Width = 40;
            //
            // ClmSaldo
            //
            this.ClmSaldo.DataPropertyName = "Saldo";
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.PaleGoldenrod;
            DataGridViewCellStyle5.Format = "N0";
            DataGridViewCellStyle5.NullValue = "0";
            this.ClmSaldo.DefaultCellStyle = DataGridViewCellStyle5;
            this.ClmSaldo.HeaderText = "Saldo";
            this.ClmSaldo.Name = "ClmSaldo";
            this.ClmSaldo.ReadOnly = true;
            this.ClmSaldo.Width = 80;
            //
            // Clmmora
            //
            this.Clmmora.DataPropertyName = "mora";
            DataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle6.Format = "N0";
            DataGridViewCellStyle6.NullValue = "0";
            this.Clmmora.DefaultCellStyle = DataGridViewCellStyle6;
            this.Clmmora.HeaderText = "Mora";
            this.Clmmora.Name = "Clmmora";
            this.Clmmora.ReadOnly = true;
            this.Clmmora.Width = 60;
            //
            // ClmInteres
            //
            this.ClmInteres.DataPropertyName = "interes";
            DataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle7.Format = "N0";
            DataGridViewCellStyle7.NullValue = "0";
            this.ClmInteres.DefaultCellStyle = DataGridViewCellStyle7;
            this.ClmInteres.HeaderText = "Interes";
            this.ClmInteres.Name = "ClmInteres";
            this.ClmInteres.ReadOnly = true;
            this.ClmInteres.Width = 70;
            //
            // ClmOtros
            //
            this.ClmOtros.DataPropertyName = "otros";
            DataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle8.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle8.Format = "N0";
            DataGridViewCellStyle8.NullValue = "0";
            this.ClmOtros.DefaultCellStyle = DataGridViewCellStyle8;
            this.ClmOtros.HeaderText = "Otros";
            this.ClmOtros.Name = "ClmOtros";
            this.ClmOtros.ReadOnly = true;
            this.ClmOtros.Width = 70;
            //
            // ClmCapital
            //
            this.ClmCapital.DataPropertyName = "capital";
            DataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle9.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle9.Format = "N0";
            DataGridViewCellStyle9.NullValue = "0";
            this.ClmCapital.DefaultCellStyle = DataGridViewCellStyle9;
            this.ClmCapital.HeaderText = "Capital";
            this.ClmCapital.Name = "ClmCapital";
            this.ClmCapital.ReadOnly = true;
            this.ClmCapital.Width = 70;
            //
            // ClmTotal
            //
            this.ClmTotal.DataPropertyName = "total";
            DataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle10.BackColor = System.Drawing.Color.PaleGoldenrod;
            DataGridViewCellStyle10.Format = "N0";
            DataGridViewCellStyle10.NullValue = "0";
            this.ClmTotal.DefaultCellStyle = DataGridViewCellStyle10;
            this.ClmTotal.HeaderText = "Total";
            this.ClmTotal.Name = "ClmTotal";
            this.ClmTotal.ReadOnly = true;
            this.ClmTotal.Width = 80;
            //
            // Menu
            //
            this.Menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.VerCuotasPendientesToolStripMenuItem, this.ToolStripSeparator1, this.EliminarPagoToolStripMenuItem });
            this.Menu.Name = "Menu";
            this.Menu.Size = new System.Drawing.Size(195, 54);
            //
            // VerCuotasPendientesToolStripMenuItem
            //
            this.VerCuotasPendientesToolStripMenuItem.Name = "VerCuotasPendientesToolStripMenuItem";
            this.VerCuotasPendientesToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.VerCuotasPendientesToolStripMenuItem.Text = "Ver Cuotas Pendientes";
            this.VerCuotasPendientesToolStripMenuItem.Click += new System.EventHandler(this.VerCuotasPendientesToolStripMenuItem_Click);
            //
            // ToolStripSeparator1
            //
            this.ToolStripSeparator1.Name = "ToolStripSeparator1";
            this.ToolStripSeparator1.Size = new System.Drawing.Size(191, 6);
            //
            // EliminarPagoToolStripMenuItem
            //
            this.EliminarPagoToolStripMenuItem.Name = "EliminarPagoToolStripMenuItem";
            this.EliminarPagoToolStripMenuItem.Size = new System.Drawing.Size(194, 22);
            this.EliminarPagoToolStripMenuItem.Text = "Eliminar Pago";
            this.EliminarPagoToolStripMenuItem.Click += new System.EventHandler(this.EliminarPagoToolStripMenuItem_Click);
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.Enabled = false;
            this.TxtCodigoter.Location = new System.Drawing.Point(61, 64);
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.Size = new System.Drawing.Size(128, 20);
            this.TxtCodigoter.TabIndex = 0;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(12, 67);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(40, 13);
            this.Label1.TabIndex = 334;
            this.Label1.Text = "Codigo";
            //
            // LblNombre
            //
            this.LblNombre.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNombre.Location = new System.Drawing.Point(197, 65);
            this.LblNombre.Name = "LblNombre";
            this.LblNombre.Size = new System.Drawing.Size(464, 19);
            this.LblNombre.TabIndex = 335;
            this.LblNombre.Text = "Codigo";
            //
            // LblTotalPago
            //
            this.LblTotalPago.BackColor = System.Drawing.Color.Gainsboro;
            this.LblTotalPago.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblTotalPago.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblTotalPago.ForeColor = System.Drawing.Color.Chocolate;
            this.LblTotalPago.Location = new System.Drawing.Point(636, 247);
            this.LblTotalPago.Name = "LblTotalPago";
            this.LblTotalPago.Size = new System.Drawing.Size(91, 18);
            this.LblTotalPago.TabIndex = 336;
            this.LblTotalPago.Text = "#";
            this.LblTotalPago.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // LblCapital
            //
            this.LblCapital.BackColor = System.Drawing.Color.Gainsboro;
            this.LblCapital.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCapital.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblCapital.ForeColor = System.Drawing.Color.Chocolate;
            this.LblCapital.Location = new System.Drawing.Point(568, 247);
            this.LblCapital.Name = "LblCapital";
            this.LblCapital.Size = new System.Drawing.Size(67, 18);
            this.LblCapital.TabIndex = 337;
            this.LblCapital.Text = "#";
            this.LblCapital.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // LblOtros
            //
            this.LblOtros.BackColor = System.Drawing.Color.Gainsboro;
            this.LblOtros.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblOtros.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblOtros.ForeColor = System.Drawing.Color.Chocolate;
            this.LblOtros.Location = new System.Drawing.Point(497, 247);
            this.LblOtros.Name = "LblOtros";
            this.LblOtros.Size = new System.Drawing.Size(70, 18);
            this.LblOtros.TabIndex = 338;
            this.LblOtros.Text = "#";
            this.LblOtros.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // LblInteres
            //
            this.LblInteres.BackColor = System.Drawing.Color.Gainsboro;
            this.LblInteres.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblInteres.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblInteres.ForeColor = System.Drawing.Color.Chocolate;
            this.LblInteres.Location = new System.Drawing.Point(426, 247);
            this.LblInteres.Name = "LblInteres";
            this.LblInteres.Size = new System.Drawing.Size(70, 18);
            this.LblInteres.TabIndex = 339;
            this.LblInteres.Text = "#";
            this.LblInteres.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // LblMora
            //
            this.LblMora.BackColor = System.Drawing.Color.Gainsboro;
            this.LblMora.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblMora.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblMora.ForeColor = System.Drawing.Color.Chocolate;
            this.LblMora.Location = new System.Drawing.Point(364, 247);
            this.LblMora.Name = "LblMora";
            this.LblMora.Size = new System.Drawing.Size(61, 18);
            this.LblMora.TabIndex = 340;
            this.LblMora.Text = "#";
            this.LblMora.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // LblSaldo
            //
            this.LblSaldo.BackColor = System.Drawing.Color.Gainsboro;
            this.LblSaldo.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblSaldo.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblSaldo.ForeColor = System.Drawing.Color.Chocolate;
            this.LblSaldo.Location = new System.Drawing.Point(265, 247);
            this.LblSaldo.Name = "LblSaldo";
            this.LblSaldo.Size = new System.Drawing.Size(97, 18);
            this.LblSaldo.TabIndex = 341;
            this.LblSaldo.Text = "#";
            this.LblSaldo.TextAlign = System.Drawing.ContentAlignment.TopRight;
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
            this.opcion.Location = new System.Drawing.Point(3, 4);
            this.opcion.Margin = new System.Windows.Forms.Padding(4);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(29, 27);
            this.opcion.TabIndex = 320;
            this.opcion.Tooltip_Boton1 = "Salir";
            this.opcion.Tooltip_Boton2 = "Guardar";
            this.opcion.Tooltip_Boton3 = "Eliminar";
            this.opcion.Tooltip_Boton4 = "Primero";
            this.opcion.Tooltip_Boton5 = "Anterior";
            this.opcion.Tooltip_Boton6 = "Siguiente";
            this.opcion.Tooltip_Boton7 = "Ultimo";
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            //
            // cmbsalir
            //
            this.cmbsalir.AutoSize = true;
            this.cmbsalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.cmbsalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.cmbsalir.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbsalir.Location = new System.Drawing.Point(650, 299);
            this.cmbsalir.Name = "cmbsalir";
            this.cmbsalir.Size = new System.Drawing.Size(50, 40);
            this.cmbsalir.TabIndex = 5;
            this.ToolTip.SetToolTip(this.cmbsalir, "Salir");
            this.cmbsalir.UseVisualStyleBackColor = true;
            this.cmbsalir.Click += new System.EventHandler(this.cmbsalir_Click);
            //
            // CmbGuardar
            //
            this.CmbGuardar.AutoSize = true;
            this.CmbGuardar.BackgroundImage = global::ERP.Core.Properties.Resources.disco;
            this.CmbGuardar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.CmbGuardar.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmbGuardar.Location = new System.Drawing.Point(595, 299);
            this.CmbGuardar.Name = "CmbGuardar";
            this.CmbGuardar.Size = new System.Drawing.Size(50, 40);
            this.CmbGuardar.TabIndex = 4;
            this.ToolTip.SetToolTip(this.CmbGuardar, "Grabar");
            this.CmbGuardar.UseVisualStyleBackColor = true;
            this.CmbGuardar.Click += new System.EventHandler(this.CmbGuardar_Click);
            //
            // GroupBox1
            //
            this.GroupBox1.Location = new System.Drawing.Point(7, 46);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(720, 10);
            this.GroupBox1.TabIndex = 344;
            this.GroupBox1.TabStop = false;
            //
            // GroupBox2
            //
            this.GroupBox2.Location = new System.Drawing.Point(7, 342);
            this.GroupBox2.Name = "GroupBox2";
            this.GroupBox2.Size = new System.Drawing.Size(718, 10);
            this.GroupBox2.TabIndex = 345;
            this.GroupBox2.TabStop = false;
            //
            // GroupBox3
            //
            this.GroupBox3.Location = new System.Drawing.Point(7, 283);
            this.GroupBox3.Name = "GroupBox3";
            this.GroupBox3.Size = new System.Drawing.Size(720, 10);
            this.GroupBox3.TabIndex = 353;
            this.GroupBox3.TabStop = false;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(200, 250);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(60, 13);
            this.Label4.TabIndex = 356;
            this.Label4.Text = "Totales = >";
            //
            // Label10
            //
            this.Label10.AutoSize = true;
            this.Label10.Location = new System.Drawing.Point(377, 313);
            this.Label10.Name = "Label10";
            this.Label10.Size = new System.Drawing.Size(39, 13);
            this.Label10.TabIndex = 363;
            this.Label10.Text = "Interes";
            //
            // TxtInteres
            //
            this.TxtInteres.BackColor = System.Drawing.SystemColors.Window;
            this.TxtInteres.Enabled = false;
            this.TxtInteres.Location = new System.Drawing.Point(422, 309);
            this.TxtInteres.Name = "TxtInteres";
            this.TxtInteres.ReadOnly = true;
            this.TxtInteres.Size = new System.Drawing.Size(100, 20);
            this.TxtInteres.TabIndex = 360;
            this.TxtInteres.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Location = new System.Drawing.Point(211, 313);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(31, 13);
            this.Label9.TabIndex = 362;
            this.Label9.Text = "Valor";
            //
            // TxtValor
            //
            this.TxtValor.BackColor = System.Drawing.SystemColors.Window;
            this.TxtValor.Enabled = false;
            this.TxtValor.Location = new System.Drawing.Point(248, 309);
            this.TxtValor.Name = "TxtValor";
            this.TxtValor.ReadOnly = true;
            this.TxtValor.Size = new System.Drawing.Size(123, 20);
            this.TxtValor.TabIndex = 359;
            this.TxtValor.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // TxtNumero
            //
            this.TxtNumero.BackColor = System.Drawing.SystemColors.Window;
            this.TxtNumero.Location = new System.Drawing.Point(94, 309);
            this.TxtNumero.Name = "TxtNumero";
            this.TxtNumero.ReadOnly = true;
            this.TxtNumero.Size = new System.Drawing.Size(100, 20);
            this.TxtNumero.TabIndex = 358;
            this.TxtNumero.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // TxtLinea
            //
            this.TxtLinea.BackColor = System.Drawing.SystemColors.Window;
            this.TxtLinea.Location = new System.Drawing.Point(47, 309);
            this.TxtLinea.Name = "TxtLinea";
            this.TxtLinea.ReadOnly = true;
            this.TxtLinea.Size = new System.Drawing.Size(43, 20);
            this.TxtLinea.TabIndex = 357;
            //
            // Label16
            //
            this.Label16.AutoSize = true;
            this.Label16.Location = new System.Drawing.Point(12, 313);
            this.Label16.Name = "Label16";
            this.Label16.Size = new System.Drawing.Size(33, 13);
            this.Label16.TabIndex = 361;
            this.Label16.Text = "Linea";
            //
            // Frmpagodeuda
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(731, 384);
            this.ControlBox = false;
            this.Controls.Add(this.Label10);
            this.Controls.Add(this.TxtInteres);
            this.Controls.Add(this.Label9);
            this.Controls.Add(this.TxtValor);
            this.Controls.Add(this.TxtNumero);
            this.Controls.Add(this.TxtLinea);
            this.Controls.Add(this.Label16);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.GroupBox3);
            this.Controls.Add(this.CmbGuardar);
            this.Controls.Add(this.GroupBox2);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.cmbsalir);
            this.Controls.Add(this.LblSaldo);
            this.Controls.Add(this.LblNombre);
            this.Controls.Add(this.LblMora);
            this.Controls.Add(this.LblInteres);
            this.Controls.Add(this.LblOtros);
            this.Controls.Add(this.LblCapital);
            this.Controls.Add(this.LblTotalPago);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.TxtCodigoter);
            this.Controls.Add(this.DtgDatos);
            this.Controls.Add(this.TxtNomForma);
            this.Controls.Add(this.TxtfechaSys);
            this.Controls.Add(this.Txtusuario);
            this.Controls.Add(this.TxtBd);
            this.Controls.Add(this.txtServer);
            this.Controls.Add(this.Label34);
            this.Controls.Add(this.Label40);
            this.Controls.Add(this.Label32);
            this.Controls.Add(this.Lblperiodo);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.Label13);
            this.Controls.Add(this.LblNomEmpresa);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "Frmpagodeuda";
            this.Load += new System.EventHandler(this.Frmpagodeuda_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DtgDatos)).EndInit();
            this.Menu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label LblNomEmpresa;
        private ERP.Core.Compartido.Controles.SasToolBar opcion;
        private System.Windows.Forms.Label Lblperiodo;
        private System.Windows.Forms.Label Label13;
        private System.Windows.Forms.TextBox TxtNomForma;
        private System.Windows.Forms.TextBox TxtfechaSys;
        private System.Windows.Forms.TextBox Txtusuario;
        private System.Windows.Forms.TextBox TxtBd;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label Label34;
        private System.Windows.Forms.Label Label40;
        private System.Windows.Forms.Label Label32;
        private System.Windows.Forms.DataGridView DtgDatos;
        private System.Windows.Forms.TextBox TxtCodigoter;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label LblNombre;
        private System.Windows.Forms.Label LblTotalPago;
        private System.Windows.Forms.Label LblCapital;
        private System.Windows.Forms.Label LblOtros;
        private System.Windows.Forms.Label LblInteres;
        private System.Windows.Forms.Label LblMora;
        private System.Windows.Forms.Label LblSaldo;
        private System.Windows.Forms.Button cmbsalir;
        private System.Windows.Forms.ToolTip ToolTip;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.GroupBox GroupBox2;
        private System.Windows.Forms.Button CmbGuardar;
        private System.Windows.Forms.GroupBox GroupBox3;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.Label Label10;
        private System.Windows.Forms.TextBox TxtInteres;
        private System.Windows.Forms.Label Label9;
        private System.Windows.Forms.TextBox TxtValor;
        private System.Windows.Forms.TextBox TxtNumero;
        private System.Windows.Forms.TextBox TxtLinea;
        private System.Windows.Forms.Label Label16;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmLinea;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmPagare;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmDescripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmtasa;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmSaldo;
        private System.Windows.Forms.DataGridViewTextBoxColumn Clmmora;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmInteres;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmOtros;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmCapital;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmTotal;
        private System.Windows.Forms.ContextMenuStrip Menu;
        private System.Windows.Forms.ToolStripMenuItem VerCuotasPendientesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator ToolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem EliminarPagoToolStripMenuItem;
    }
}
