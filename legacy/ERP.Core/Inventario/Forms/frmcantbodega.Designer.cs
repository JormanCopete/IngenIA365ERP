namespace ERP.Core.Inventario.Forms
{
    partial class frmcantbodega
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
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmcantbodega));
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DgvCantidades = new System.Windows.Forms.DataGridView();
            this.Label1 = new System.Windows.Forms.Label();
            this.LblIdProducto = new System.Windows.Forms.Label();
            this.LblNomProducto = new System.Windows.Forms.Label();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.Label9 = new System.Windows.Forms.Label();
            this.LblDisponible = new System.Windows.Forms.Label();
            this.Label7 = new System.Windows.Forms.Label();
            this.LblCantVentas = new System.Windows.Forms.Label();
            this.Label5 = new System.Windows.Forms.Label();
            this.LblCantCompra = new System.Windows.Forms.Label();
            this.Label4 = new System.Windows.Forms.Label();
            this.LblCantinicial = new System.Windows.Forms.Label();
            this.Label2 = new System.Windows.Forms.Label();
            this.LblCostoConsolidado = new System.Windows.Forms.Label();
            this.Label11 = new System.Windows.Forms.Label();
            this.Lblperiodo = new System.Windows.Forms.Label();
            this.Label18 = new System.Windows.Forms.Label();
            this.empresappl = new System.Windows.Forms.Label();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            this.clmubicacion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDescripcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmBodega = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDesBodega = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmInicial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmCompra = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmVenta = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmFinal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmCostoIni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmcosto = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmUltCosto = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.DgvCantidades)).BeginInit();
            this.GroupBox1.SuspendLayout();
            this.SuspendLayout();
            //
            // DgvCantidades
            //
            this.DgvCantidades.AllowUserToAddRows = false;
            this.DgvCantidades.AllowUserToDeleteRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            DataGridViewCellStyle1.ForeColor = System.Drawing.Color.Black;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgvCantidades.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgvCantidades.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvCantidades.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmubicacion, this.ClmDescripcion, this.clmBodega, this.ClmDesBodega, this.ClmInicial, this.clmCompra, this.clmVenta, this.clmFinal, this.clmCostoIni, this.clmcosto, this.clmUltCosto });
            this.DgvCantidades.EnableHeadersVisualStyles = false;
            this.DgvCantidades.Location = new System.Drawing.Point(7, 167);
            this.DgvCantidades.Name = "DgvCantidades";
            this.DgvCantidades.ReadOnly = true;
            this.DgvCantidades.RowHeadersVisible = false;
            this.DgvCantidades.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgvCantidades.Size = new System.Drawing.Size(784, 150);
            this.DgvCantidades.TabIndex = 0;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label1.Location = new System.Drawing.Point(10, 27);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(56, 15);
            this.Label1.TabIndex = 1;
            this.Label1.Text = "Producto";
            //
            // LblIdProducto
            //
            this.LblIdProducto.BackColor = System.Drawing.Color.Gainsboro;
            this.LblIdProducto.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblIdProducto.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblIdProducto.Location = new System.Drawing.Point(70, 23);
            this.LblIdProducto.Name = "LblIdProducto";
            this.LblIdProducto.Size = new System.Drawing.Size(88, 23);
            this.LblIdProducto.TabIndex = 2;
            this.LblIdProducto.Text = "0";
            this.LblIdProducto.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // LblNomProducto
            //
            this.LblNomProducto.BackColor = System.Drawing.Color.Gainsboro;
            this.LblNomProducto.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblNomProducto.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblNomProducto.Location = new System.Drawing.Point(159, 23);
            this.LblNomProducto.Name = "LblNomProducto";
            this.LblNomProducto.Size = new System.Drawing.Size(349, 23);
            this.LblNomProducto.TabIndex = 3;
            this.LblNomProducto.Text = "Producto";
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.Label9);
            this.GroupBox1.Controls.Add(this.LblDisponible);
            this.GroupBox1.Controls.Add(this.Label7);
            this.GroupBox1.Controls.Add(this.LblCantVentas);
            this.GroupBox1.Controls.Add(this.Label5);
            this.GroupBox1.Controls.Add(this.LblCantCompra);
            this.GroupBox1.Controls.Add(this.Label4);
            this.GroupBox1.Controls.Add(this.LblCantinicial);
            this.GroupBox1.Controls.Add(this.Label2);
            this.GroupBox1.Controls.Add(this.LblCostoConsolidado);
            this.GroupBox1.Controls.Add(this.LblNomProducto);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Controls.Add(this.LblIdProducto);
            this.GroupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.GroupBox1.Location = new System.Drawing.Point(12, 50);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(779, 85);
            this.GroupBox1.TabIndex = 4;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Informacion Consolidada";
            //
            // Label9
            //
            this.Label9.AutoSize = true;
            this.Label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label9.Location = new System.Drawing.Point(557, 57);
            this.Label9.Name = "Label9";
            this.Label9.Size = new System.Drawing.Size(97, 15);
            this.Label9.TabIndex = 12;
            this.Label9.Text = "Cant. Disponible";
            //
            // LblDisponible
            //
            this.LblDisponible.BackColor = System.Drawing.Color.Gainsboro;
            this.LblDisponible.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblDisponible.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblDisponible.ForeColor = System.Drawing.Color.SaddleBrown;
            this.LblDisponible.Location = new System.Drawing.Point(660, 56);
            this.LblDisponible.Name = "LblDisponible";
            this.LblDisponible.Size = new System.Drawing.Size(88, 23);
            this.LblDisponible.TabIndex = 13;
            this.LblDisponible.Text = "0";
            this.LblDisponible.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // Label7
            //
            this.Label7.AutoSize = true;
            this.Label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label7.Location = new System.Drawing.Point(382, 57);
            this.Label7.Name = "Label7";
            this.Label7.Size = new System.Drawing.Size(75, 15);
            this.Label7.TabIndex = 10;
            this.Label7.Text = "Cant. Ventas";
            //
            // LblCantVentas
            //
            this.LblCantVentas.BackColor = System.Drawing.Color.Gainsboro;
            this.LblCantVentas.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCantVentas.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblCantVentas.ForeColor = System.Drawing.Color.Brown;
            this.LblCantVentas.Location = new System.Drawing.Point(463, 56);
            this.LblCantVentas.Name = "LblCantVentas";
            this.LblCantVentas.Size = new System.Drawing.Size(88, 23);
            this.LblCantVentas.TabIndex = 11;
            this.LblCantVentas.Text = "0";
            this.LblCantVentas.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // Label5
            //
            this.Label5.AutoSize = true;
            this.Label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label5.Location = new System.Drawing.Point(200, 57);
            this.Label5.Name = "Label5";
            this.Label5.Size = new System.Drawing.Size(88, 15);
            this.Label5.TabIndex = 8;
            this.Label5.Text = "Cant. Compras";
            //
            // LblCantCompra
            //
            this.LblCantCompra.BackColor = System.Drawing.Color.Gainsboro;
            this.LblCantCompra.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCantCompra.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblCantCompra.ForeColor = System.Drawing.Color.Brown;
            this.LblCantCompra.Location = new System.Drawing.Point(288, 56);
            this.LblCantCompra.Name = "LblCantCompra";
            this.LblCantCompra.Size = new System.Drawing.Size(88, 23);
            this.LblCantCompra.TabIndex = 9;
            this.LblCantCompra.Text = "0";
            this.LblCantCompra.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label4.Location = new System.Drawing.Point(30, 57);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(70, 15);
            this.Label4.TabIndex = 6;
            this.Label4.Text = "Cant. Inicial";
            //
            // LblCantinicial
            //
            this.LblCantinicial.BackColor = System.Drawing.Color.Gainsboro;
            this.LblCantinicial.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCantinicial.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblCantinicial.ForeColor = System.Drawing.Color.Brown;
            this.LblCantinicial.Location = new System.Drawing.Point(106, 56);
            this.LblCantinicial.Name = "LblCantinicial";
            this.LblCantinicial.Size = new System.Drawing.Size(88, 23);
            this.LblCantinicial.TabIndex = 7;
            this.LblCantinicial.Text = "0";
            this.LblCantinicial.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // Label2
            //
            this.Label2.AutoSize = true;
            this.Label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label2.Location = new System.Drawing.Point(516, 27);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(146, 15);
            this.Label2.TabIndex = 4;
            this.Label2.Text = "Costo Prom. Consolidado";
            //
            // LblCostoConsolidado
            //
            this.LblCostoConsolidado.BackColor = System.Drawing.Color.Gainsboro;
            this.LblCostoConsolidado.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCostoConsolidado.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.LblCostoConsolidado.Location = new System.Drawing.Point(665, 23);
            this.LblCostoConsolidado.Name = "LblCostoConsolidado";
            this.LblCostoConsolidado.Size = new System.Drawing.Size(103, 23);
            this.LblCostoConsolidado.TabIndex = 5;
            this.LblCostoConsolidado.Text = "0";
            this.LblCostoConsolidado.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // Label11
            //
            this.Label11.AutoSize = true;
            this.Label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.Label11.ForeColor = System.Drawing.Color.DarkBlue;
            this.Label11.Location = new System.Drawing.Point(306, 140);
            this.Label11.Name = "Label11";
            this.Label11.Size = new System.Drawing.Size(186, 20);
            this.Label11.TabIndex = 5;
            this.Label11.Text = "Informacion Detallada";
            //
            // Lblperiodo
            //
            this.Lblperiodo.BackColor = System.Drawing.SystemColors.Control;
            this.Lblperiodo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold);
            this.Lblperiodo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Lblperiodo.Location = new System.Drawing.Point(713, 12);
            this.Lblperiodo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Lblperiodo.Name = "Lblperiodo";
            this.Lblperiodo.Size = new System.Drawing.Size(67, 20);
            this.Lblperiodo.TabIndex = 328;
            //
            // Label18
            //
            this.Label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.0f, System.Drawing.FontStyle.Bold);
            this.Label18.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.Label18.Location = new System.Drawing.Point(649, 12);
            this.Label18.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label18.Name = "Label18";
            this.Label18.Size = new System.Drawing.Size(65, 20);
            this.Label18.TabIndex = 327;
            this.Label18.Text = "Periodo";
            //
            // empresappl
            //
            this.empresappl.BackColor = System.Drawing.SystemColors.Control;
            this.empresappl.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.empresappl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.empresappl.Location = new System.Drawing.Point(99, 6);
            this.empresappl.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.empresappl.Name = "empresappl";
            this.empresappl.Size = new System.Drawing.Size(517, 37);
            this.empresappl.TabIndex = 326;
            this.empresappl.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // opcion
            //
            this.opcion.AccessibleRole = System.Windows.Forms.AccessibleRole.ToolBar;
            this.opcion.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.opcion.Enabled_Eliminar = true;
            this.opcion.Enabled_Grabar = true;
            this.opcion.Enabled_Salir = true;
            this.opcion.Estilo_Barra = 1;
            this.opcion.Imagen_Atras = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Atras");
            this.opcion.Imagen_Eliminar = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Eliminar");
            this.opcion.Imagen_Grabar = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Grabar");
            this.opcion.Imagen_Primero = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Primero");
            this.opcion.Imagen_Salir = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Salir");
            this.opcion.Imagen_Siguiente = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Siguiente");
            this.opcion.Imagen_Ultimo = (System.Drawing.Image)resources.GetObject("opcion.Imagen_Ultimo");
            this.opcion.Location = new System.Drawing.Point(9, 6);
            this.opcion.Margin = new System.Windows.Forms.Padding(4);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(27, 27);
            this.opcion.TabIndex = 325;
            // this.opcion.tooltip_Boton1 = "Salir"; // ERROR: CS1061
            // this.opcion.tooltip_Boton2 = "Guardar"; // ERROR: CS1061
            // this.opcion.tooltip_Boton3 = "Eliminar"; // ERROR: CS1061
            // this.opcion.tooltip_Boton4 = "Primero"; // ERROR: CS1061
            // this.opcion.tooltip_Boton5 = "Anterior"; // ERROR: CS1061
            // this.opcion.tooltip_Boton6 = "Siguiente"; // ERROR: CS1061
            // this.opcion.tooltip_Boton7 = "Ultimo"; // ERROR: CS1061
            //
            // clmubicacion
            //
            this.clmubicacion.DataPropertyName = "idubicacion";
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle2.BackColor = System.Drawing.Color.Beige;
            this.clmubicacion.DefaultCellStyle = DataGridViewCellStyle2;
            this.clmubicacion.HeaderText = "Id";
            this.clmubicacion.Name = "clmubicacion";
            this.clmubicacion.ReadOnly = true;
            this.clmubicacion.Width = 60;
            //
            // ClmDescripcion
            //
            this.ClmDescripcion.DataPropertyName = "NomUbicacion";
            this.ClmDescripcion.HeaderText = "Descripcion";
            this.ClmDescripcion.Name = "ClmDescripcion";
            this.ClmDescripcion.ReadOnly = true;
            this.ClmDescripcion.Width = 80;
            //
            // clmBodega
            //
            this.clmBodega.DataPropertyName = "idbodega";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle3.BackColor = System.Drawing.Color.Beige;
            this.clmBodega.DefaultCellStyle = DataGridViewCellStyle3;
            this.clmBodega.HeaderText = "Id";
            this.clmBodega.Name = "clmBodega";
            this.clmBodega.ReadOnly = true;
            this.clmBodega.Width = 60;
            //
            // ClmDesBodega
            //
            this.ClmDesBodega.DataPropertyName = "NomBodega";
            this.ClmDesBodega.HeaderText = "Bodega";
            this.ClmDesBodega.Name = "ClmDesBodega";
            this.ClmDesBodega.ReadOnly = true;
            this.ClmDesBodega.Width = 80;
            //
            // ClmInicial
            //
            this.ClmInicial.DataPropertyName = "CantInicial";
            DataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle4.Format = "N2";
            DataGridViewCellStyle4.NullValue = "0";
            this.ClmInicial.DefaultCellStyle = DataGridViewCellStyle4;
            this.ClmInicial.HeaderText = "Inicial";
            this.ClmInicial.Name = "ClmInicial";
            this.ClmInicial.ReadOnly = true;
            this.ClmInicial.Width = 70;
            //
            // clmCompra
            //
            this.clmCompra.DataPropertyName = "CantCompra";
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle5.Format = "N2";
            DataGridViewCellStyle5.NullValue = "0";
            this.clmCompra.DefaultCellStyle = DataGridViewCellStyle5;
            this.clmCompra.HeaderText = "Compras";
            this.clmCompra.Name = "clmCompra";
            this.clmCompra.ReadOnly = true;
            this.clmCompra.Width = 70;
            //
            // clmVenta
            //
            this.clmVenta.DataPropertyName = "Cantvendida";
            DataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle6.Format = "N2";
            DataGridViewCellStyle6.NullValue = "0";
            this.clmVenta.DefaultCellStyle = DataGridViewCellStyle6;
            this.clmVenta.HeaderText = "Ventas";
            this.clmVenta.Name = "clmVenta";
            this.clmVenta.ReadOnly = true;
            this.clmVenta.Width = 70;
            //
            // clmFinal
            //
            this.clmFinal.DataPropertyName = "CantFinal";
            DataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle7.Format = "N2";
            DataGridViewCellStyle7.NullValue = "0";
            this.clmFinal.DefaultCellStyle = DataGridViewCellStyle7;
            this.clmFinal.HeaderText = "Final";
            this.clmFinal.Name = "clmFinal";
            this.clmFinal.ReadOnly = true;
            this.clmFinal.Width = 70;
            //
            // clmCostoIni
            //
            this.clmCostoIni.DataPropertyName = "costoinicial";
            DataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle8.Format = "N4";
            DataGridViewCellStyle8.NullValue = "0";
            this.clmCostoIni.DefaultCellStyle = DataGridViewCellStyle8;
            this.clmCostoIni.HeaderText = "Costo Ini";
            this.clmCostoIni.Name = "clmCostoIni";
            this.clmCostoIni.ReadOnly = true;
            this.clmCostoIni.Width = 80;
            //
            // clmcosto
            //
            this.clmcosto.DataPropertyName = "costo";
            DataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle9.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle9.Format = "N4";
            DataGridViewCellStyle9.NullValue = "0";
            this.clmcosto.DefaultCellStyle = DataGridViewCellStyle9;
            this.clmcosto.HeaderText = "Costo Prom";
            this.clmcosto.Name = "clmcosto";
            this.clmcosto.ReadOnly = true;
            //
            // clmUltCosto
            //
            this.clmUltCosto.DataPropertyName = "ultcostopro";
            DataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle10.Format = "N4";
            DataGridViewCellStyle10.NullValue = "0";
            this.clmUltCosto.DefaultCellStyle = DataGridViewCellStyle10;
            this.clmUltCosto.HeaderText = "Ult. Costo";
            this.clmUltCosto.Name = "clmUltCosto";
            this.clmUltCosto.ReadOnly = true;
            //
            // frmcantbodega
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(798, 323);
            this.ControlBox = false;
            this.Controls.Add(this.Lblperiodo);
            this.Controls.Add(this.Label18);
            this.Controls.Add(this.empresappl);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.Label11);
            this.Controls.Add(this.GroupBox1);
            this.Controls.Add(this.DgvCantidades);
            this.Name = "frmcantbodega";
            this.Load += new System.EventHandler(this.frmcantbodega_Load);
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
            ((System.ComponentModel.ISupportInitialize)(this.DgvCantidades)).EndInit();
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.DataGridView DgvCantidades;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label LblIdProducto;
        private System.Windows.Forms.Label LblNomProducto;
        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Label LblCostoConsolidado;
        private System.Windows.Forms.Label Label9;
        private System.Windows.Forms.Label LblDisponible;
        private System.Windows.Forms.Label Label7;
        private System.Windows.Forms.Label LblCantVentas;
        private System.Windows.Forms.Label Label5;
        private System.Windows.Forms.Label LblCantCompra;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.Label LblCantinicial;
        private System.Windows.Forms.Label Label11;
        private System.Windows.Forms.Label Lblperiodo;
        private System.Windows.Forms.Label Label18;
        private System.Windows.Forms.Label empresappl;
        private ERP.Core.Compartido.Controles.SasToolBar opcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmubicacion;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmDescripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmBodega;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmDesBodega;
        private System.Windows.Forms.DataGridViewTextBoxColumn ClmInicial;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmCompra;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmVenta;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmFinal;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmCostoIni;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmcosto;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmUltCosto;
    }
}
