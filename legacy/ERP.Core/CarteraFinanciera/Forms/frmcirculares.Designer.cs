namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class frmcirculares
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

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            this.GroupBox1 = new System.Windows.Forms.GroupBox();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.BtnActualizar = new System.Windows.Forms.Button();
            this.TxtPeriodoFin = new System.Windows.Forms.TextBox();
            this.Label4 = new System.Windows.Forms.Label();
            this.TxtPeriodoIni = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.LblNomAsociado = new System.Windows.Forms.Label();
            this.TxtCodigoter = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.DgwMaestro = new System.Windows.Forms.DataGridView();
            this.clmaviso = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmfecha = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmclase = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmdireccion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmtelefono = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmciudad = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmclasecpto = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmperiodo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clmleyarrastre = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MenuGrilla = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.VerCircularDeudorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.VerCircularDeudorYCodeudoresToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.GroupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwMaestro)).BeginInit();
            this.MenuGrilla.SuspendLayout();
            this.SuspendLayout();
            //
            // GroupBox1
            //
            this.GroupBox1.Controls.Add(this.BtnSalir);
            this.GroupBox1.Controls.Add(this.BtnActualizar);
            this.GroupBox1.Controls.Add(this.TxtPeriodoFin);
            this.GroupBox1.Controls.Add(this.Label4);
            this.GroupBox1.Controls.Add(this.TxtPeriodoIni);
            this.GroupBox1.Controls.Add(this.Label3);
            this.GroupBox1.Controls.Add(this.LblNomAsociado);
            this.GroupBox1.Controls.Add(this.TxtCodigoter);
            this.GroupBox1.Controls.Add(this.Label1);
            this.GroupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GroupBox1.Location = new System.Drawing.Point(50, 27);
            this.GroupBox1.Name = "GroupBox1";
            this.GroupBox1.Size = new System.Drawing.Size(649, 95);
            this.GroupBox1.TabIndex = 0;
            this.GroupBox1.TabStop = false;
            this.GroupBox1.Text = "Datos Generales";
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = global::ERP.Core.Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(597, 52);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 1;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir");
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // BtnActualizar
            //
            //this.BtnActualizar.BackgroundImage = global::ERP.Core.Properties.Resources.replace2;
            this.BtnActualizar.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnActualizar.Location = new System.Drawing.Point(548, 52);
            this.BtnActualizar.Name = "BtnActualizar";
            this.BtnActualizar.Size = new System.Drawing.Size(38, 35);
            this.BtnActualizar.TabIndex = 7;
            this.ToolTip1.SetToolTip(this.BtnActualizar, "Actualizar");
            this.BtnActualizar.UseVisualStyleBackColor = true;
            this.BtnActualizar.Click += new System.EventHandler(this.BtnActualizar_Click);
            //
            // TxtPeriodoFin
            //
            this.TxtPeriodoFin.Location = new System.Drawing.Point(319, 58);
            this.TxtPeriodoFin.Name = "TxtPeriodoFin";
            this.TxtPeriodoFin.Size = new System.Drawing.Size(110, 21);
            this.TxtPeriodoFin.TabIndex = 6;
            //
            // Label4
            //
            this.Label4.AutoSize = true;
            this.Label4.Location = new System.Drawing.Point(265, 61);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(39, 15);
            this.Label4.TabIndex = 5;
            this.Label4.Text = "Hasta";
            //
            // TxtPeriodoIni
            //
            this.TxtPeriodoIni.Location = new System.Drawing.Point(122, 58);
            this.TxtPeriodoIni.Name = "TxtPeriodoIni";
            this.TxtPeriodoIni.Size = new System.Drawing.Size(117, 21);
            this.TxtPeriodoIni.TabIndex = 4;
            //
            // Label3
            //
            this.Label3.AutoSize = true;
            this.Label3.Location = new System.Drawing.Point(17, 61);
            this.Label3.Name = "Label3";
            this.Label3.Size = new System.Drawing.Size(87, 15);
            this.Label3.TabIndex = 3;
            this.Label3.Text = "Periodo desde";
            //
            // LblNomAsociado
            //
            this.LblNomAsociado.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblNomAsociado.Location = new System.Drawing.Point(254, 28);
            this.LblNomAsociado.Name = "LblNomAsociado";
            this.LblNomAsociado.Size = new System.Drawing.Size(389, 18);
            this.LblNomAsociado.TabIndex = 2;
            //
            // TxtCodigoter
            //
            this.TxtCodigoter.Location = new System.Drawing.Point(122, 25);
            this.TxtCodigoter.Name = "TxtCodigoter";
            this.TxtCodigoter.Size = new System.Drawing.Size(117, 21);
            this.TxtCodigoter.TabIndex = 1;
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(17, 28);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(99, 15);
            this.Label1.TabIndex = 0;
            this.Label1.Text = "Codigo Asociado";
            //
            // DgwMaestro
            //
            this.DgwMaestro.AllowUserToAddRows = false;
            this.DgwMaestro.AllowUserToDeleteRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.SlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwMaestro.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgwMaestro.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwMaestro.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.clmaviso, this.clmfecha, this.clmclase, this.clmdireccion, this.clmtelefono, this.clmciudad, this.clmclasecpto, this.clmperiodo, this.clmleyarrastre });
            this.DgwMaestro.EnableHeadersVisualStyles = false;
            this.DgwMaestro.Location = new System.Drawing.Point(12, 140);
            this.DgwMaestro.Name = "DgwMaestro";
            this.DgwMaestro.RowHeadersVisible = false;
            DataGridViewCellStyle8.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            this.DgwMaestro.RowsDefaultCellStyle = DataGridViewCellStyle8;
            this.DgwMaestro.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwMaestro.Size = new System.Drawing.Size(724, 174);
            this.DgwMaestro.TabIndex = 1;
            this.DgwMaestro.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DgwMaestro_MouseUp);
            //
            // clmaviso
            //
            this.clmaviso.DataPropertyName = "numaviso";
            DataGridViewCellStyle2.BackColor = System.Drawing.Color.Beige;
            this.clmaviso.DefaultCellStyle = DataGridViewCellStyle2;
            this.clmaviso.HeaderText = "Aviso";
            this.clmaviso.Name = "clmaviso";
            this.clmaviso.Width = 50;
            //
            // clmfecha
            //
            this.clmfecha.DataPropertyName = "fecha";
            DataGridViewCellStyle3.BackColor = System.Drawing.Color.Ivory;
            this.clmfecha.DefaultCellStyle = DataGridViewCellStyle3;
            this.clmfecha.HeaderText = "Fecha";
            this.clmfecha.Name = "clmfecha";
            this.clmfecha.Width = 80;
            //
            // clmclase
            //
            this.clmclase.DataPropertyName = "clase";
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.Ivory;
            this.clmclase.DefaultCellStyle = DataGridViewCellStyle4;
            this.clmclase.HeaderText = "Clase Cptos";
            this.clmclase.Name = "clmclase";
            //
            // clmdireccion
            //
            this.clmdireccion.DataPropertyName = "direccion";
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.Beige;
            this.clmdireccion.DefaultCellStyle = DataGridViewCellStyle5;
            this.clmdireccion.HeaderText = "Direccion";
            this.clmdireccion.Name = "clmdireccion";
            this.clmdireccion.Width = 250;
            //
            // clmtelefono
            //
            this.clmtelefono.DataPropertyName = "Telefono";
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Beige;
            this.clmtelefono.DefaultCellStyle = DataGridViewCellStyle6;
            this.clmtelefono.HeaderText = "Telefono";
            this.clmtelefono.Name = "clmtelefono";
            this.clmtelefono.Width = 80;
            //
            // clmciudad
            //
            this.clmciudad.DataPropertyName = "nombre_ciudad";
            DataGridViewCellStyle7.BackColor = System.Drawing.Color.Ivory;
            this.clmciudad.DefaultCellStyle = DataGridViewCellStyle7;
            this.clmciudad.HeaderText = "Ciudad";
            this.clmciudad.Name = "clmciudad";
            this.clmciudad.Width = 130;
            //
            // clmclasecpto
            //
            this.clmclasecpto.DataPropertyName = "clasecpto";
            this.clmclasecpto.HeaderText = "clasecpto";
            this.clmclasecpto.Name = "clmclasecpto";
            this.clmclasecpto.Visible = false;
            //
            // clmperiodo
            //
            this.clmperiodo.DataPropertyName = "periodo";
            this.clmperiodo.HeaderText = "periodo";
            this.clmperiodo.Name = "clmperiodo";
            this.clmperiodo.Visible = false;
            //
            // clmleyarrastre
            //
            this.clmleyarrastre.DataPropertyName = "leyarrastre";
            this.clmleyarrastre.HeaderText = "Ley arrastre";
            this.clmleyarrastre.Name = "clmleyarrastre";
            this.clmleyarrastre.Visible = false;
            //
            // MenuGrilla
            //
            this.MenuGrilla.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.VerCircularDeudorToolStripMenuItem, this.VerCircularDeudorYCodeudoresToolStripMenuItem });
            this.MenuGrilla.Name = "MenuGrilla";
            this.MenuGrilla.Size = new System.Drawing.Size(257, 48);
            //
            // VerCircularDeudorToolStripMenuItem
            //
            this.VerCircularDeudorToolStripMenuItem.Name = "VerCircularDeudorToolStripMenuItem";
            this.VerCircularDeudorToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.VerCircularDeudorToolStripMenuItem.Text = "Ver Circular Deudor";
            this.VerCircularDeudorToolStripMenuItem.Click += new System.EventHandler(this.VerCircularDeudorToolStripMenuItem_Click);
            //
            // VerCircularDeudorYCodeudoresToolStripMenuItem
            //
            this.VerCircularDeudorYCodeudoresToolStripMenuItem.Name = "VerCircularDeudorYCodeudoresToolStripMenuItem";
            this.VerCircularDeudorYCodeudoresToolStripMenuItem.Size = new System.Drawing.Size(256, 22);
            this.VerCircularDeudorYCodeudoresToolStripMenuItem.Text = "Ver Circular Deudor y Codeudor(es)";
            this.VerCircularDeudorYCodeudoresToolStripMenuItem.Click += new System.EventHandler(this.VerCircularDeudorYCodeudoresToolStripMenuItem_Click);
            //
            // frmcirculares
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(748, 326);
            this.Controls.Add(this.DgwMaestro);
            this.Controls.Add(this.GroupBox1);
            this.Name = "frmcirculares";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Consultar Circulares de Cobro";
            this.Load += new System.EventHandler(this.frmcirculares_Load);
            this.GroupBox1.ResumeLayout(false);
            this.GroupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgwMaestro)).EndInit();
            this.MenuGrilla.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox GroupBox1;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Label LblNomAsociado;
        internal System.Windows.Forms.TextBox TxtCodigoter;
        private System.Windows.Forms.TextBox TxtPeriodoFin;
        private System.Windows.Forms.Label Label4;
        private System.Windows.Forms.TextBox TxtPeriodoIni;
        private System.Windows.Forms.Label Label3;
        private System.Windows.Forms.Button BtnActualizar;
        private System.Windows.Forms.Button BtnSalir;
        private System.Windows.Forms.ToolTip ToolTip1;
        private System.Windows.Forms.DataGridView DgwMaestro;
        private System.Windows.Forms.ContextMenuStrip MenuGrilla;
        private System.Windows.Forms.ToolStripMenuItem VerCircularDeudorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem VerCircularDeudorYCodeudoresToolStripMenuItem;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmaviso;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmfecha;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmclase;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmdireccion;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmtelefono;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmciudad;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmclasecpto;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmperiodo;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmleyarrastre;
    }
}
