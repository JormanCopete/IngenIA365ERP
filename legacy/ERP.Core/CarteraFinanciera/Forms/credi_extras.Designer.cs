namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class credi_extras
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(credi_extras));
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.txtLinea = new System.Windows.Forms.TextBox();
            this.TxtValCredito = new System.Windows.Forms.TextBox();
            this.Txtfecha = new System.Windows.Forms.TextBox();
            this.TxtNumeroSoli = new System.Windows.Forms.TextBox();
            this.DgwExtras = new System.Windows.Forms.DataGridView();
            this.opcion = new ERP.Core.Compartido.Controles.SasToolBar();
            ((System.ComponentModel.ISupportInitialize)(this.DgwExtras)).BeginInit();
            this.SuspendLayout();
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.Location = new System.Drawing.Point(17, 94);
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.Size = new System.Drawing.Size(100, 22);
            this.TxtCodigoter.TabIndex = 304;
            this.TxtCodigoter.Visible = false;
            //
            // txtLinea
            //
            this.txtLinea.Location = new System.Drawing.Point(136, 94);
            this.txtLinea.Name = "txtLinea";
            this.txtLinea.Size = new System.Drawing.Size(100, 22);
            this.txtLinea.TabIndex = 304;
            this.txtLinea.Visible = false;
            //
            // TxtValCredito
            //
            this.TxtValCredito.Location = new System.Drawing.Point(242, 94);
            this.TxtValCredito.Name = "TxtValCredito";
            this.TxtValCredito.Size = new System.Drawing.Size(100, 22);
            this.TxtValCredito.TabIndex = 304;
            this.TxtValCredito.Visible = false;
            //
            // Txtfecha
            //
            this.Txtfecha.Location = new System.Drawing.Point(348, 94);
            this.Txtfecha.Name = "Txtfecha";
            this.Txtfecha.Size = new System.Drawing.Size(100, 22);
            this.Txtfecha.TabIndex = 304;
            this.Txtfecha.Visible = false;
            //
            // TxtNumeroSoli
            //
            this.TxtNumeroSoli.Location = new System.Drawing.Point(348, 122);
            this.TxtNumeroSoli.Name = "TxtNumeroSoli";
            this.TxtNumeroSoli.Size = new System.Drawing.Size(100, 22);
            this.TxtNumeroSoli.TabIndex = 304;
            this.TxtNumeroSoli.Visible = false;
            //
            // DgwExtras
            //
            this.DgwExtras.AllowDrop = true;
            this.DgwExtras.AllowUserToAddRows = false;
            this.DgwExtras.AllowUserToDeleteRows = false;
            this.DgwExtras.AllowUserToOrderColumns = true;
            this.DgwExtras.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwExtras.Location = new System.Drawing.Point(17, 38);
            this.DgwExtras.Name = "DgwExtras";
            this.DgwExtras.ReadOnly = true;
            this.DgwExtras.RowHeadersVisible = false;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.White;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.Color.Black;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.White;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.Black;
            this.DgwExtras.RowsDefaultCellStyle = DataGridViewCellStyle1;
            this.DgwExtras.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwExtras.Size = new System.Drawing.Size(515, 199);
            this.DgwExtras.TabIndex = 305;
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
            this.opcion.Location = new System.Drawing.Point(14, 4);
            this.opcion.Name = "opcion";
            this.opcion.Size = new System.Drawing.Size(29, 27);
            this.opcion.TabIndex = 306;
            this.opcion.Tooltip_Boton1 = "Salir";
            this.opcion.Tooltip_Boton2 = "Guardar";
            this.opcion.Tooltip_Boton3 = "Eliminar";
            this.opcion.Tooltip_Boton4 = "Primero";
            this.opcion.Tooltip_Boton5 = "Anterior";
            this.opcion.Tooltip_Boton6 = "Siguiente";
            this.opcion.Tooltip_Boton7 = "Ultimo";
            //
            // credi_extras
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8.0f, 16.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(559, 249);
            this.Controls.Add(this.opcion);
            this.Controls.Add(this.DgwExtras);
            this.Controls.Add(this.TxtNumeroSoli);
            this.Controls.Add(this.Txtfecha);
            this.Controls.Add(this.TxtValCredito);
            this.Controls.Add(this.txtLinea);
            this.Controls.Add(this.TxtCodigoter);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "credi_extras";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "credi_extras";
            this.Load += new System.EventHandler(this.credi_extras_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DgwExtras)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
            //
            // Event wiring
            //
            this.opcion.ClickEvent += new System.EventHandler(this.opcion_ClickEvent);
        }

        #endregion

        internal System.Windows.Forms.TextBox TxtCodigoter;
        internal System.Windows.Forms.TextBox txtLinea;
        internal System.Windows.Forms.TextBox TxtValCredito;
        internal System.Windows.Forms.TextBox Txtfecha;
        internal System.Windows.Forms.DataGridView DgwExtras;
        internal ERP.Core.Compartido.Controles.SasToolBar opcion;
        internal System.Windows.Forms.TextBox TxtNumeroSoli;
    }
}
